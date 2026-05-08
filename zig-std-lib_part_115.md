```
ilder, lower_bound: ?Metadata, count: ?Metadata) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.subrange, Metadata.Subrange{
        .lower_bound = .wrap(lower_bound),
        .count = .wrap(count),
    });
}

fn debugExpressionAssumeCapacity(self: *Builder, elements: []const u32) Metadata {
    assert(!self.strip);
    const Key = struct {
        elements: []const u32,
    };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            var hasher =
                comptime std.hash.Wyhash.init(std.hash.int(@intFromEnum(Metadata.Tag.expression)));
            hasher.update(std.mem.sliceAsBytes(key.elements));
            return @truncate(hasher.final());
        }

        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (Metadata.Tag.expression != ctx.builder.metadata_items.items(.tag)[rhs_index])
                return false;
            const rhs_data = ctx.builder.metadata_items.items(.data)[rhs_index];
            var rhs_extra = ctx.builder.metadataExtraDataTrail(Metadata.Expression, rhs_data);
            return std.mem.eql(
                u32,
                lhs_key.elements,
                rhs_extra.trail.next(rhs_extra.data.elements_len, u32, ctx.builder),
            );
        }
    };

    const gop = self.metadata_map.getOrPutAssumeCapacityAdapted(
        Key{ .elements = elements },
        Adapter{ .builder = self },
    );

    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.metadata_items.appendAssumeCapacity(.{
            .tag = .expression,
            .data = self.addMetadataExtraAssumeCapacity(Metadata.Expression{
                .elements_len = @intCast(elements.len),
            }),
        });
        self.metadata_extra.appendSliceAssumeCapacity(@ptrCast(elements));
    }
    return .{ .index = @intCast(gop.index), .kind = .node };
}

fn metadataTupleOptionalsAssumeCapacity(self: *Builder, elements: []const Metadata.Optional) Metadata {
    const Key = struct {
        elements: []const Metadata.Optional,
    };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            var hasher = comptime std.hash.Wyhash.init(std.hash.int(@intFromEnum(Metadata.Tag.tuple)));
            hasher.update(std.mem.sliceAsBytes(key.elements));
            return @truncate(hasher.final());
        }

        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (Metadata.Tag.tuple != ctx.builder.metadata_items.items(.tag)[rhs_index]) return false;
            const rhs_data = ctx.builder.metadata_items.items(.data)[rhs_index];
            var rhs_extra = ctx.builder.metadataExtraDataTrail(Metadata.Tuple, rhs_data);
            return std.mem.eql(
                Metadata.Optional,
                lhs_key.elements,
                rhs_extra.trail.next(rhs_extra.data.elements_len, Metadata.Optional, ctx.builder),
            );
        }
    };

    const gop = self.metadata_map.getOrPutAssumeCapacityAdapted(
        Key{ .elements = elements },
        Adapter{ .builder = self },
    );

    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.metadata_items.appendAssumeCapacity(.{
            .tag = .tuple,
            .data = self.addMetadataExtraAssumeCapacity(Metadata.Tuple{
                .elements_len = @intCast(elements.len),
            }),
        });
        self.metadata_extra.appendSliceAssumeCapacity(@ptrCast(elements));
    }
    return .{ .index = @intCast(gop.index), .kind = .node };
}

fn debugLocalVarAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    ty: ?Metadata,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.local_var, Metadata.LocalVar{
        .name = .wrap(name),
        .file = .wrap(file),
        .scope = .wrap(scope),
        .line = line,
        .ty = .wrap(ty),
    });
}

fn debugParameterAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    ty: ?Metadata,
    arg_no: u32,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.parameter, Metadata.Parameter{
        .name = .wrap(name),
        .file = .wrap(file),
        .scope = .wrap(scope),
        .line = line,
        .ty = .wrap(ty),
        .arg_no = arg_no,
    });
}

fn debugGlobalVarAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    linkage_name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    ty: ?Metadata,
    variable: Variable.Index,
    options: Metadata.GlobalVar.Options,
) Metadata {
    assert(!self.strip);
    return self.metadataDistinctAssumeCapacity(
        if (options.local) .@"global_var local" else .global_var,
        Metadata.GlobalVar{
            .name = .wrap(name),
            .linkage_name = .wrap(linkage_name),
            .file = .wrap(file),
            .scope = .wrap(scope),
            .line = line,
            .ty = .wrap(ty),
            .variable = variable,
        },
    );
}

fn debugGlobalVarExpressionAssumeCapacity(
    self: *Builder,
    variable: ?Metadata,
    expression: ?Metadata,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.global_var_expression, Metadata.GlobalVarExpression{
        .variable = .wrap(variable),
        .expression = .wrap(expression),
    });
}

fn metadataConstantAssumeCapacity(self: *Builder, constant: Constant) Metadata {
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Constant) u32 {
            var hasher = comptime std.hash.Wyhash.init(std.hash.int(@intFromEnum(Metadata.Tag.constant)));
            hasher.update(std.mem.asBytes(&key));
            return @truncate(hasher.final());
        }

        pub fn eql(ctx: @This(), lhs_key: Constant, _: void, rhs_index: usize) bool {
            if (Metadata.Tag.constant != ctx.builder.metadata_items.items(.tag)[rhs_index]) return false;
            const rhs_data: Constant = @enumFromInt(ctx.builder.metadata_items.items(.data)[rhs_index]);
            return rhs_data == lhs_key;
        }
    };

    const gop = self.metadata_map.getOrPutAssumeCapacityAdapted(
        constant,
        Adapter{ .builder = self },
    );

    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.metadata_items.appendAssumeCapacity(.{
            .tag = .constant,
            .data = @intFromEnum(constant),
        });
    }
    return .{ .index = @intCast(gop.index), .kind = .node };
}

pub const Producer = struct {
    name: []const u8,
    version: std.SemanticVersion,
};

pub fn toBitcode(self: *Builder, allocator: Allocator, producer: Producer) bitcode_writer.Error![]const u32 {
    const BitcodeWriter = bitcode_writer.BitcodeWriter(&.{ Type, FunctionAttributes });
    var bitcode = BitcodeWriter.init(allocator, .{
        std.math.log2_int_ceil(usize, self.type_items.items.len),
        std.math.log2_int_ceil(usize, 1 + self.function_attributes_set.count()),
    });
    errdefer bitcode.deinit();

    // Write LLVM IR magic
    try bitcode.writeBits(ir.MAGIC, 32);

    var record: std.ArrayList(u64) = .empty;
    defer record.deinit(self.gpa);

    // IDENTIFICATION_BLOCK
    {
        const IdentificationBlock = ir.IdentificationBlock;
        var identification_block = try bitcode.enterTopBlock(IdentificationBlock);

        const producer_str = try std.fmt.allocPrint(self.gpa, "{s} {d}.{d}.{d}", .{
            producer.name,
            producer.version.major,
            producer.version.minor,
            producer.version.patch,
        });
        defer self.gpa.free(producer_str);

        try identification_block.writeAbbrev(IdentificationBlock.Version{ .string = producer_str });
        try identification_block.writeAbbrev(IdentificationBlock.Epoch{ .epoch = 0 });

        try identification_block.end();
    }

    // MODULE_BLOCK
    {
        const ModuleBlock = ir.ModuleBlock;
        var module_block = try bitcode.enterTopBlock(ModuleBlock);

        try module_block.writeAbbrev(ModuleBlock.Version{});

        if (self.target_triple.slice(self)) |triple| {
            try module_block.writeAbbrev(ModuleBlock.String{
                .code = 2,
                .string = triple,
            });
        }

        if (self.data_layout.slice(self)) |data_layout| {
            try module_block.writeAbbrev(ModuleBlock.String{
                .code = 3,
                .string = data_layout,
            });
        }

        if (self.source_filename.slice(self)) |source_filename| {
            try module_block.writeAbbrev(ModuleBlock.String{
                .code = 16,
                .string = source_filename,
            });
        }

        if (self.module_asm.items.len != 0) {
            try module_block.writeAbbrev(ModuleBlock.String{
                .code = 4,
                .string = self.module_asm.items,
            });
        }

        // TYPE_BLOCK
        {
            const TypeBlock = ir.ModuleBlock.TypeBlock;
            var type_block = try module_block.enterSubBlock(TypeBlock, true);

            try type_block.writeAbbrev(TypeBlock.NumEntry{ .num = @intCast(self.type_items.items.len) });

            for (self.type_items.items, 0..) |item, i| {
                const ty: Type = @enumFromInt(i);

                switch (item.tag) {
                    .simple => try type_block.writeAbbrev(TypeBlock.Simple{ .code = @enumFromInt(item.data) }),
                    .integer => try type_block.writeAbbrev(TypeBlock.Integer{ .width = item.data }),
                    .structure,
                    .packed_structure,
                    => |kind| {
                        const is_packed = switch (kind) {
                            .structure => false,
                            .packed_structure => true,
                            else => unreachable,
                        };
                        var extra = self.typeExtraDataTrail(Type.Structure, item.data);
                        try type_block.writeAbbrev(TypeBlock.StructAnon{
                            .is_packed = is_packed,
                            .types = extra.trail.next(extra.data.fields_len, Type, self),
                        });
                    },
                    .named_structure => {
                        const extra = self.typeExtraData(Type.NamedStructure, item.data);
                        try type_block.writeAbbrev(TypeBlock.StructName{
                            .string = extra.id.slice(self).?,
                        });

                        switch (extra.body) {
                            .none => try type_block.writeAbbrev(TypeBlock.Opaque{}),
                            else => {
                                const real_struct = self.type_items.items[@intFromEnum(extra.body)];
                                const is_packed: bool = switch (real_struct.tag) {
                                    .structure => false,
                                    .packed_structure => true,
                                    else => unreachable,
                                };

                                var real_extra = self.typeExtraDataTrail(Type.Structure, real_struct.data);
                                try type_block.writeAbbrev(TypeBlock.StructNamed{
                                    .is_packed = is_packed,
                                    .types = real_extra.trail.next(real_extra.data.fields_len, Type, self),
                                });
                            },
                        }
                    },
                    .array,
                    .small_array,
                    => try type_block.writeAbbrev(TypeBlock.Array{
                        .len = ty.aggregateLen(self),
                        .child = ty.childType(self),
                    }),
                    .vector,
                    .scalable_vector,
                    => try type_block.writeAbbrev(TypeBlock.Vector{
                        .len = ty.aggregateLen(self),
                        .child = ty.childType(self),
                    }),
                    .pointer => try type_block.writeAbbrev(TypeBlock.Pointer{
                        .addr_space = ty.pointerAddrSpace(self),
                    }),
                    .target => {
                        var extra = self.typeExtraDataTrail(Type.Target, item.data);
                        try type_block.writeAbbrev(TypeBlock.StructName{
                            .string = extra.data.name.slice(self).?,
                        });

                        const types = extra.trail.next(extra.data.types_len, Type, self);
                        const ints = extra.trail.next(extra.data.ints_len, u32, self);

                        try type_block.writeAbbrev(TypeBlock.Target{
                            .num_types = extra.data.types_len,
                            .types = types,
                            .ints = ints,
                        });
                    },
                    .function, .vararg_function => |kind| {
                        const is_vararg = switch (kind) {
                            .function => false,
                            .vararg_function => true,
                            else => unreachable,
                        };
                        var extra = self.typeExtraDataTrail(Type.Function, item.data);
                        try type_block.writeAbbrev(TypeBlock.Function{
                            .is_vararg = is_vararg,
                            .return_type = extra.data.ret,
                            .param_types = extra.trail.next(extra.data.params_len, Type, self),
                        });
                    },
                }
            }

            try type_block.end();
        }

        var attributes_set: std.AutoArrayHashMapUnmanaged(struct {
            attributes: Attributes,
            index: u32,
        }, void) = .{};
        defer attributes_set.deinit(self.gpa);

        // PARAMATTR_GROUP_BLOCK
        {
            const ParamattrGroupBlock = ir.ModuleBlock.ParamattrGroupBlock;

            var paramattr_group_block = try module_block.enterSubBlock(ParamattrGroupBlock, true);

            for (self.function_attributes_set.keys()) |func_attributes| {
                for (func_attributes.slice(self), 0..) |attributes, i| {
                    const attributes_slice = attributes.slice(self);
                    if (attributes_slice.len == 0) continue;

                    const attr_gop = try attributes_set.getOrPut(self.gpa, .{
                        .attributes = attributes,
                        .index = @intCast(i),
                    });

                    if (attr_gop.found_existing) continue;

                    record.clearRetainingCapacity();
                    try record.ensureUnusedCapacity(self.gpa, 2);

                    record.appendAssumeCapacity(attr_gop.index);
                    record.appendAssumeCapacity(switch (i) {
                        0 => 0xffffffff,
                        else => i - 1,
                    });

                    for (attributes_slice) |attr_index| {
                        const kind = attr_index.getKind(self);
                        switch (attr_index.toAttribute(self)) {
                            .zeroext,
                            .signext,
                            .inreg,
                            .@"noalias",
                            .nocapture,
                            .nofree,
                            .nest,
                            .returned,
                            .nonnull,
                            .swiftself,
                            .swiftasync,
                            .swifterror,
                            .immarg,
                            .noundef,
                            .allocalign,
                            .allocptr,
                            .readnone,
                            .readonly,
                            .writeonly,
                            .alwaysinline,
                            .builtin,
                            .cold,
                            .convergent,
                            .disable_sanitizer_information,
                            .fn_ret_thunk_extern,
                            .hot,
                            .inlinehint,
                            .jumptable,
                            .minsize,
                            .naked,
                            .nobuiltin,
                            .nocallback,
                            .noduplicate,
                            .noimplicitfloat,
                            .@"noinline",
                            .nomerge,
                            .nonlazybind,
                            .noprofile,
                            .skipprofile,
                            .noredzone,
                            .noreturn,
                            .norecurse,
                            .willreturn,
                            .nosync,
                            .nounwind,
                            .nosanitize_bounds,
                            .nosanitize_coverage,
                            .null_pointer_is_valid,
                            .optforfuzzing,
                            .optnone,
                            .optsize,
                            .returns_twice,
                            .safestack,
                            .sanitize_address,
                            .sanitize_memory,
                            .sanitize_thread,
                            .sanitize_hwaddress,
                            .sanitize_memtag,
                            .speculative_load_hardening,
                            .speculatable,
                            .ssp,
                            .sspstrong,
                            .sspreq,
                            .strictfp,
                            .nocf_check,
                            .shadowcallstack,
                            .mustprogress,
                            .no_sanitize_address,
                            .no_sanitize_hwaddress,
                            .sanitize_address_dyninit,
                            => {
                                try record.ensureUnusedCapacity(self.gpa, 2);
                                record.appendAssumeCapacity(0);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                            },
                            .byval,
                            .byref,
                            .preallocated,
                            .inalloca,
                            .sret,
                            .elementtype,
                            => |ty| {
                                try record.ensureUnusedCapacity(self.gpa, 3);
                                record.appendAssumeCapacity(6);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                                record.appendAssumeCapacity(@intFromEnum(ty));
                            },
                            .@"align",
                            .alignstack,
                            => |alignment| {
                                try record.ensureUnusedCapacity(self.gpa, 3);
                                record.appendAssumeCapacity(1);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                                record.appendAssumeCapacity(alignment.resolve(self).toByteUnits() orelse 0);
                            },
                            .dereferenceable,
                            .dereferenceable_or_null,
                            => |size| {
                                try record.ensureUnusedCapacity(self.gpa, 3);
                                record.appendAssumeCapacity(1);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                                record.appendAssumeCapacity(size);
                            },
                            .nofpclass => |fpclass| {
                                try record.ensureUnusedCapacity(self.gpa, 3);
                                record.appendAssumeCapacity(1);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                                record.appendAssumeCapacity(@as(u32, @bitCast(fpclass)));
                            },
                            .allockind => |allockind| {
                                try record.ensureUnusedCapacity(self.gpa, 3);
                                record.appendAssumeCapacity(1);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                                record.appendAssumeCapacity(@as(u32, @bitCast(allockind)));
                            },

                            .allocsize => |allocsize| {
                                try record.ensureUnusedCapacity(self.gpa, 3);
                                record.appendAssumeCapacity(1);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                                record.appendAssumeCapacity(@bitCast(allocsize.toLlvm()));
                            },
                            .memory => |memory| {
                                try record.ensureUnusedCapacity(self.gpa, 3);
                                record.appendAssumeCapacity(1);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                                record.appendAssumeCapacity(@as(u32, @bitCast(memory)));
                            },
                            .uwtable => |uwtable| if (uwtable != .none) {
                                try record.ensureUnusedCapacity(self.gpa, 3);
                                record.appendAssumeCapacity(1);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                                record.appendAssumeCapacity(@intFromEnum(uwtable));
                            },
                            .vscale_range => |vscale_range| {
                                try record.ensureUnusedCapacity(self.gpa, 3);
                                record.appendAssumeCapacity(1);
                                record.appendAssumeCapacity(@intFromEnum(kind));
                                record.appendAssumeCapacity(@bitCast(vscale_range.toLlvm()));
                            },
                            .string => |string_attr| {
                                const string_attr_kind_slice = string_attr.kind.slice(self).?;
                                const string_attr_value_slice = if (string_attr.value != .none)
                                    string_attr.value.slice(self).?
                                else
                                    null;

                                try record.ensureUnusedCapacity(
                                    self.gpa,
                                    2 + string_attr_kind_slice.len + if (string_attr_value_slice) |slice| slice.len + 1 else 0,
                                );
                                record.appendAssumeCapacity(if (string_attr.value == .none) 3 else 4);
                                for (string_attr.kind.slice(self).?) |c| {
                                    record.appendAssumeCapacity(c);
                                }
                                record.appendAssumeCapacity(0);
                                if (string_attr_value_slice) |slice| {
                                    for (slice) |c| {
                                        record.appendAssumeCapacity(c);
                                    }
                                    record.appendAssumeCapacity(0);
                                }
                            },
                            .none => unreachable,
                        }
                    }

                    try paramattr_group_block.writeUnabbrev(3, record.items);
                }
            }

            try paramattr_group_block.end();
        }

        // PARAMATTR_BLOCK
        {
            const ParamattrBlock = ir.ModuleBlock.ParamattrBlock;
            var paramattr_block = try module_block.enterSubBlock(ParamattrBlock, true);

            for (self.function_attributes_set.keys()) |func_attributes| {
                const func_attributes_slice = func_attributes.slice(self);
                record.clearRetainingCapacity();
                try record.ensureUnusedCapacity(self.gpa, func_attributes_slice.len);
                for (func_attributes_slice, 0..) |attributes, i| {
                    const attributes_slice = attributes.slice(self);
                    if (attributes_slice.len == 0) continue;

                    const group_index = attributes_set.getIndex(.{
                        .attributes = attributes,
                        .index = @intCast(i),
                    }).?;
                    record.appendAssumeCapacity(@intCast(group_index));
                }

                try paramattr_block.writeAbbrev(ParamattrBlock.Entry{ .group_indices = record.items });
            }

            try paramattr_block.end();
        }

        var globals: std.AutoArrayHashMapUnmanaged(Global.Index, void) = .empty;
        defer globals.deinit(self.gpa);
        try globals.ensureUnusedCapacity(
            self.gpa,
            self.variables.items.len +
                self.functions.items.len +
                self.aliases.items.len,
        );

        for (self.variables.items, 0..) |variable, variable_i| {
            // Skip the variable if its global has been repurposed for something else.
            switch (variable.global.ptrConst(self).kind) {
                .variable => |v| if (@intFromEnum(v) != variable_i) continue,
                else => continue,
            }

            globals.putAssumeCapacity(variable.global, {});
        }

        for (self.functions.items, 0..) |function, function_i| {
            // Skip the function if its global has been repurposed for something else.
            switch (function.global.ptrConst(self).kind) {
                .function => |f| if (@intFromEnum(f) != function_i) continue,
                else => continue,
            }

            globals.putAssumeCapacity(function.global, {});
        }

        for (self.aliases.items, 0..) |alias, alias_i| {
            // Skip the alias if its global has been repurposed for something else.
            switch (alias.global.ptrConst(self).kind) {
                .alias => |a| if (@intFromEnum(a) != alias_i) continue,
                else => continue,
            }

            globals.putAssumeCapacity(alias.global, {});
        }

        const ConstantAdapter = struct {
            builder: *const Builder,
            globals: *const std.AutoArrayHashMapUnmanaged(Global.Index, void),

            pub fn get(adapter: @This(), param: anytype) switch (@TypeOf(param)) {
                Constant => u32,
                else => |Param| Param,
            } {
                return switch (@TypeOf(param)) {
                    Constant => adapter.getConstantIndex(param),
                    else => param,
                };
            }

            pub fn getConstantIndex(adapter: @This(), constant: Constant) u32 {
                return switch (constant.unwrap()) {
                    .constant => |c| c + adapter.numGlobals(),
                    .global => |global| @intCast(adapter.globals.getIndex(global.unwrap(adapter.builder)).?),
                };
            }

            pub fn numConstants(adapter: @This()) u32 {
                return @intCast(adapter.globals.count() + adapter.builder.constant_items.len);
            }

            pub fn numGlobals(adapter: @This()) u32 {
                return @intCast(adapter.globals.count());
            }
        };
        const constant_adapter: ConstantAdapter = .{ .builder = self, .globals = &globals };

        // Globals
        {
            var section_map: std.AutoArrayHashMapUnmanaged(String, void) = .empty;
            defer section_map.deinit(self.gpa);
            try section_map.ensureUnusedCapacity(self.gpa, globals.count());

            for (self.variables.items, 0..) |variable, variable_i| {
                // Skip the variable if its global has been repurposed for something else.
                switch (variable.global.ptrConst(self).kind) {
                    .variable => |v| if (@intFromEnum(v) != variable_i) continue,
                    else => continue,
                }

                const section = blk: {
                    if (variable.section == .none) break :blk 0;
                    const gop = section_map.getOrPutAssumeCapacity(variable.section);
                    if (!gop.found_existing) {
                        try module_block.writeAbbrev(ModuleBlock.String{
                            .code = 5,
                            .string = variable.section.slice(self).?,
                        });
                    }
                    break :blk gop.index + 1;
                };

                const initid = if (variable.init == .no_init)
                    0
                else
                    (constant_adapter.getConstantIndex(variable.init) + 1);

                const strtab = variable.global.strtab(self);

                const global = variable.global.ptrConst(self);
                try module_block.writeAbbrev(ModuleBlock.Variable{
                    .strtab_offset = strtab.offset,
                    .strtab_size = strtab.size,
                    .type_index = global.type,
                    .is_const = .{
                        .is_const = switch (variable.mutability) {
                            .global => false,
                            .constant => true,
                        },
                        .addr_space = global.addr_space,
                    },
                    .initid = initid,
                    .linkage = global.linkage,
                    .alignment = variable.alignment.toLlvm(),
                    .section = section,
                    .visibility = global.visibility,
                    .thread_local = variable.thread_local,
                    .unnamed_addr = global.unnamed_addr,
                    .externally_initialized = global.externally_initialized,
                    .dllstorageclass = global.dll_storage_class,
                    .preemption = global.preemption,
                });
            }

            for (self.functions.items, 0..) |func, func_i| {
                // Skip the function if its global has been repurposed for something else.
                switch (func.global.ptrConst(self).kind) {
                    .function => |f| if (@intFromEnum(f) != func_i) continue,
                    else => continue,
                }

                const section = blk: {
                    if (func.section == .none) break :blk 0;
                    const gop = section_map.getOrPutAssumeCapacity(func.section);
                    if (!gop.found_existing) {
                        try module_block.writeAbbrev(ModuleBlock.String{
                            .code = 5,
                            .string = func.section.slice(self).?,
                        });
                    }
                    break :blk gop.index + 1;
                };

                const paramattr_index = if (self.function_attributes_set.getIndex(func.attributes)) |index|
                    index + 1
                else
                    0;

                const strtab = func.global.strtab(self);

                const global = func.global.ptrConst(self);
                try module_block.writeAbbrev(ModuleBlock.Function{
                    .strtab_offset = strtab.offset,
                    .strtab_size = strtab.size,
                    .type_index = global.type,
                    .call_conv = func.call_conv,
                    .is_proto = func.instructions.len == 0,
                    .linkage = global.linkage,
                    .paramattr = paramattr_index,
                    .alignment = func.alignment.toLlvm(),
                    .section = section,
                    .visibility = global.visibility,
                    .unnamed_addr = global.unnamed_addr,
                    .dllstorageclass = global.dll_storage_class,
                    .preemption = global.preemption,
                    .addr_space = global.addr_space,
                });
            }

            for (self.aliases.items, 0..) |alias, alias_i| {
                // Skip the alias if its global has been repurposed for something else.
                switch (alias.global.ptrConst(self).kind) {
                    .alias => |a| if (@intFromEnum(a) != alias_i) continue,
                    else => continue,
                }

                const strtab = alias.global.strtab(self);

                const global = alias.global.ptrConst(self);
                try module_block.writeAbbrev(ModuleBlock.Alias{
                    .strtab_offset = strtab.offset,
                    .strtab_size = strtab.size,
                    .type_index = global.type,
                    .addr_space = global.addr_space,
                    .aliasee = constant_adapter.getConstantIndex(alias.aliasee),
                    .linkage = global.linkage,
                    .visibility = global.visibility,
                    .thread_local = alias.thread_local,
                    .unnamed_addr = global.unnamed_addr,
                    .dllstorageclass = global.dll_storage_class,
                    .preemption = global.preemption,
                });
            }
        }

        // CONSTANTS_BLOCK
        {
            const ConstantsBlock = ir.ModuleBlock.ConstantsBlock;
            var constants_block = try module_block.enterSubBlock(ConstantsBlock, true);

            var current_type: Type = .none;
            const tags = self.constant_items.items(.tag);
            const datas = self.constant_items.items(.data);
            for (0..self.constant_items.len) |index| {
                record.clearRetainingCapacity();
                const constant: Constant = @enumFromInt(index);
                const constant_type = constant.typeOf(self);
                if (constant_type != current_type) {
                    try constants_block.writeAbbrev(ConstantsBlock.SetType{ .type_id = constant_type });
                    current_type = constant_type;
                }
                const data = datas[index];
                switch (tags[index]) {
                    .null,
                    .zeroinitializer,
                    .none,
                    => try constants_block.writeAbbrev(ConstantsBlock.Null{}),
                    .undef => try constants_block.writeAbbrev(ConstantsBlock.Undef{}),
                    .poison => try constants_block.writeAbbrev(ConstantsBlock.Poison{}),
                    .positive_integer,
                    .negative_integer,
                    => |tag| {
                        const extra: *align(@alignOf(std.math.big.Limb)) Constant.Integer =
                            @ptrCast(self.constant_limbs.items[data..][0..Constant.Integer.limbs]);
                        const bigint: std.math.big.int.Const = .{
                            .limbs = self.constant_limbs
                                .items[data + Constant.Integer.limbs ..][0..extra.limbs_len],
                            .positive = switch (tag) {
                                .positive_integer => true,
                                .negative_integer => false,
                                else => unreachable,
                            },
                        };
                        const bit_count = extra.type.scalarBits(self);
                        const val: i64 = if (bit_count <= 64)
                            bigint.toInt(i64) catch unreachable
                        else if (bigint.toInt(u64)) |val|
                            @bitCast(val)
                        else |_| {
                            const limbs = try record.addManyAsSlice(
                                self.gpa,
                                std.math.divCeil(u24, bit_count, 64) catch unreachable,
                            );
                            bigint.writeTwosComplement(std.mem.sliceAsBytes(limbs), .little);
                            for (limbs) |*limb| {
                                const val = std.mem.littleToNative(i64, @bitCast(limb.*));
                                limb.* = @bitCast(if (val >= 0)
                                    val << 1 | 0
                                else
                                    -%val << 1 | 1);
                            }
                            try constants_block.writeUnabbrev(5, record.items);
                            continue;
                        };
                        try constants_block.writeAbbrev(ConstantsBlock.Integer{
                            .value = @bitCast(if (val >= 0)
                                val << 1 | 0
                            else
                                -%val << 1 | 1),
                        });
                    },
                    .half,
                    .bfloat,
                    => try constants_block.writeAbbrev(ConstantsBlock.Half{ .value = @truncate(data) }),
                    .float => try constants_block.writeAbbrev(ConstantsBlock.Float{ .value = data }),
                    .double => {
                        const extra = self.constantExtraData(Constant.Double, data);
                        try constants_block.writeAbbrev(ConstantsBlock.Double{
                            .value = (@as(u64, extra.hi) << 32) | extra.lo,
                        });
                    },
                    .x86_fp80 => {
                        const extra = self.constantExtraData(Constant.Fp80, data);
                        try constants_block.writeAbbrev(ConstantsBlock.Fp80{
                            .hi = @as(u64, extra.hi) << 48 | @as(u64, extra.lo_hi) << 16 |
                                extra.lo_lo >> 16,
                            .lo = @truncate(extra.lo_lo),
                        });
                    },
                    .fp128,
                    .ppc_fp128,
                    => {
                        const extra = self.constantExtraData(Constant.Fp128, data);
                        try constants_block.writeAbbrev(ConstantsBlock.Fp128{
                            .lo = @as(u64, extra.lo_hi) << 32 | @as(u64, extra.lo_lo),
                            .hi = @as(u64, extra.hi_hi) << 32 | @as(u64, extra.hi_lo),
                        });
                    },
                    .array,
                    .vector,
                    .structure,
                    .packed_structure,
                    => {
                        var extra = self.constantExtraDataTrail(Constant.Aggregate, data);
                        const len: u32 = @intCast(extra.data.type.aggregateLen(self));
                        const values = extra.trail.next(len, Constant, self);

                        try constants_block.writeAbbrevAdapted(
                            ConstantsBlock.Aggregate{ .values = values },
                            constant_adapter,
                        );
                    },
                    .splat => {
                        const ConstantsBlockWriter = @TypeOf(constants_block);
                        const extra = self.constantExtraData(Constant.Splat, data);
                        const vector_len = extra.type.vectorLen(self);
                        const c = constant_adapter.getConstantIndex(extra.value);

                        try bitcode.writeBits(
                            ConstantsBlockWriter.abbrevId(ConstantsBlock.Aggregate),
                            ConstantsBlockWriter.abbrev_len,
                        );
                        try bitcode.writeVbr(vector_len, 6);
                        for (0..vector_len) |_| {
                            try bitcode.writeBits(c, ConstantsBlock.Aggregate.ops[1].array_fixed);
                        }
                    },
                    .string => {
                        const str: String = @enumFromInt(data);
                        if (str == .none) {
                            try constants_block.writeAbbrev(ConstantsBlock.Null{});
                        } else {
                            const slice = str.slice(self).?;
                            if (slice.len > 0 and slice[slice.len - 1] == 0)
                                try constants_block.writeAbbrev(ConstantsBlock.CString{ .string = slice[0 .. slice.len - 1] })
                            else
                                try constants_block.writeAbbrev(ConstantsBlock.String{ .string = slice });
                        }
                    },
                    .bitcast,
                    .inttoptr,
                    .ptrtoint,
                    .addrspacecast,
                    .trunc,
                    => |tag| {
                        const extra = self.constantExtraData(Constant.Cast, data);
                        try constants_block.writeAbbrevAdapted(ConstantsBlock.Cast{
                            .type_index = extra.type,
                            .val = extra.val,
                            .opcode = tag.toCastOpcode(),
                        }, constant_adapter);
                    },
                    .add,
                    .@"add nsw",
                    .@"add nuw",
                    .sub,
                    .@"sub nsw",
                    .@"sub nuw",
                    .shl,
                    .xor,
                    => |tag| {
                        const extra = self.constantExtraData(Constant.Binary, data);
                        try constants_block.writeAbbrevAdapted(ConstantsBlock.Binary{
                            .opcode = tag.toBinaryOpcode(),
                            .lhs = extra.lhs,
                            .rhs = extra.rhs,
                        }, constant_adapter);
                    },
                    .getelementptr,
                    .@"getelementptr inbounds",
                    => |tag| {
                        var extra = self.constantExtraDataTrail(Constant.GetElementPtr, data);
                        const indices = extra.trail.next(extra.data.info.indices_len, Constant, self);
                        try record.ensureUnusedCapacity(self.gpa, 1 + 2 + 2 * indices.len);

                        record.appendAssumeCapacity(@intFromEnum(extra.data.type));

                        record.appendAssumeCapacity(@intFromEnum(extra.data.base.typeOf(self)));
                        record.appendAssumeCapacity(constant_adapter.getConstantIndex(extra.data.base));

                        for (indices) |i| {
                            record.appendAssumeCapacity(@intFromEnum(i.typeOf(self)));
                            record.appendAssumeCapacity(constant_adapter.getConstantIndex(i));
                        }

                        try constants_block.writeUnabbrev(switch (tag) {
                            .getelementptr => 12,
                            .@"getelementptr inbounds" => 20,
                            else => unreachable,
                        }, record.items);
                    },
                    .@"asm",
                    .@"asm sideeffect",
                    .@"asm alignstack",
                    .@"asm sideeffect alignstack",
                    .@"asm inteldialect",
                    .@"asm sideeffect inteldialect",
                    .@"asm alignstack inteldialect",
                    .@"asm sideeffect alignstack inteldialect",
                    .@"asm unwind",
                    .@"asm sideeffect unwind",
                    .@"asm alignstack unwind",
                    .@"asm sideeffect alignstack unwind",
                    .@"asm inteldialect unwind",
                    .@"asm sideeffect inteldialect unwind",
                    .@"asm alignstack inteldialect unwind",
                    .@"asm sideeffect alignstack inteldialect unwind",
                    => |tag| {
                        const extra = self.constantExtraData(Constant.Assembly, data);

                        const assembly_slice = extra.assembly.slice(self).?;
                        const constraints_slice = extra.constraints.slice(self).?;

                        try record.ensureUnusedCapacity(self.gpa, 4 + assembly_slice.len + constraints_slice.len);

                        record.appendAssumeCapacity(@intFromEnum(extra.type));
                        record.appendAssumeCapacity(switch (tag) {
                            .@"asm" => 0,
                            .@"asm sideeffect" => 0b0001,
                            .@"asm sideeffect alignstack" => 0b0011,
                            .@"asm sideeffect inteldialect" => 0b0101,
                            .@"asm sideeffect alignstack inteldialect" => 0b0111,
                            .@"asm sideeffect unwind" => 0b1001,
                            .@"asm sideeffect alignstack unwind" => 0b1011,
                            .@"asm sideeffect inteldialect unwind" => 0b1101,
                            .@"asm sideeffect alignstack inteldialect unwind" => 0b1111,
                            .@"asm alignstack" => 0b0010,
                            .@"asm inteldialect" => 0b0100,
                            .@"asm alignstack inteldialect" => 0b0110,
                            .@"asm unwind" => 0b1000,
                            .@"asm alignstack unwind" => 0b1010,
                            .@"asm inteldialect unwind" => 0b1100,
                            .@"asm alignstack inteldialect unwind" => 0b1110,
                            else => unreachable,
                        });

                        record.appendAssumeCapacity(assembly_slice.len);
                        for (assembly_slice) |c| record.appendAssumeCapacity(c);

                        record.appendAssumeCapacity(constraints_slice.len);
                        for (constraints_slice) |c| record.appendAssumeCapacity(c);

                        try constants_block.writeUnabbrev(30, record.items);
                    },
                    .blockaddress => {
                        const extra = self.constantExtraData(Constant.BlockAddress, data);
                        try constants_block.writeAbbrev(ConstantsBlock.BlockAddress{
                            .type_id = extra.function.typeOf(self),
                            .function = constant_adapter.getConstantIndex(extra.function.toConst(self)),
                            .block = @intFromEnum(extra.block),
                        });
                    },
                    .dso_local_equivalent,
                    .no_cfi,
                    => |tag| {
                        const function: Function.Index = @enumFromInt(data);
                        try constants_block.writeAbbrev(ConstantsBlock.DsoLocalEquivalentOrNoCfi{
                            .code = switch (tag) {
                                .dso_local_equivalent => .DSO_LOCAL_EQUIVALENT,
                                .no_cfi => .NO_CFI_VALUE,
                                else => unreachable,
                            },
                            .type_id = function.typeOf(self),
                            .function = constant_adapter.getConstantIndex(function.toConst(self)),
                        });
                    },
                }
            }

            try constants_block.end();
        }

        // METADATA_KIND_BLOCK
        {
            const MetadataKindBlock = ir.ModuleBlock.MetadataKindBlock;
            var metadata_kind_block = try module_block.enterSubBlock(MetadataKindBlock, true);

            inline for (@typeInfo(ir.FixedMetadataKind).@"enum".fields) |field| {
                // don't include `dbg` in stripped functions
                if (!(self.strip and std.mem.eql(u8, field.name, "dbg"))) {
                    try metadata_kind_block.writeAbbrev(MetadataKindBlock.Kind{
                        .id = field.value,
                        .name = field.name,
                    });
                }
            }

            try metadata_kind_block.end();
        }

        const MetadataAdapter = struct {
            constant_adapter: ConstantAdapter,

            pub fn get(adapter: @This(), param: anytype) switch (@TypeOf(param)) {
                Metadata, Metadata.Optional, Metadata.String, Metadata.String.Optional, Constant => u32,
                else => |Result| Result,
            } {
                return switch (@TypeOf(param)) {
                    Metadata => adapter.getMetadataIndex(param),
                    Metadata.Optional => adapter.getOptionalMetadataIndex(param),
                    Metadata.String => adapter.getMetadataIndex(param.toMetadata()),
                    Metadata.String.Optional => adapter.getOptionalMetadataIndex(param.toMetadata()),
                    Constant => adapter.constant_adapter.getConstantIndex(param),
                    else => param,
                };
            }

            pub fn getMetadataIndex(adapter: @This(), metadata: Metadata) u32 {
                const builder = adapter.constant_adapter.builder;
                const unwrapped_metadata = metadata.unwrap(builder);
                return switch (unwrapped_metadata.kind) {
                    .string => unwrapped_metadata.index,
                    .node => @intCast(builder.metadata_string_map.count() + unwrapped_metadata.index),
                    .forward, .local => unreachable,
                };
            }

            pub fn getOptionalMetadataIndex(adapter: @This(), metadata: Metadata.Optional) u32 {
                return if (metadata.unwrap()) |m| 1 + adapter.getMetadataIndex(m) else 0;
            }
        };
        const metadata_adapter: MetadataAdapter = .{ .constant_adapter = constant_adapter };

        // METADATA_BLOCK
        {
            const MetadataBlock = ir.ModuleBlock.MetadataBlock;
            var metadata_block = try module_block.enterSubBlock(MetadataBlock, true);

            const MetadataBlockWriter = @TypeOf(metadata_block);

            // Emit all Metadata.Strings
            const strings_len: u32 = @intCast(self.metadata_string_map.count());
            if (strings_len > 0) {
                const string_bytes_offset = string_bytes_offset: {
                    var string_bytes_bit_offset: u32 = 0;
                    for (
                        self.metadata_string_indices.items[0..strings_len],
                        self.metadata_string_indices.items[1..],
                    ) |start, end| string_bytes_bit_offset += BitcodeWriter.bitsVbr(end - start, 6);
                    break :string_bytes_offset @divExact(
                        std.mem.alignForward(u32, string_bytes_bit_offset, 32),
                        8,
                    );
                };
                const string_bytes_len =
                    std.mem.alignForward(u32, @intCast(self.metadata_string_bytes.items.len), 4);

                try bitcode.writeBits(
                    comptime MetadataBlockWriter.abbrevId(MetadataBlock.Strings),
                    MetadataBlockWriter.abbrev_len,
                );

                try bitcode.writeVbr(strings_len, 6);
                try bitcode.writeVbr(string_bytes_offset, 6);

                try bitcode.writeVbr(string_bytes_offset + string_bytes_len, 6);

                try bitcode.alignTo32();

                for (
                    self.metadata_string_indices.items[0..strings_len],
                    self.metadata_string_indices.items[1..],
                ) |start, end| try bitcode.writeVbr(end - start, 6);

                try bitcode.writeBlob(self.metadata_string_bytes.items);
            }

            for (self.metadata_items.items(.tag), self.metadata_items.items(.data)) |tag, data| {
                record.clearRetainingCapacity();
                switch (tag) {
                    .file => {
                        const extra = self.metadataExtraData(Metadata.File, data);

                        try metadata_block.writeAbbrevAdapted(MetadataBlock.File{
                            .filename = extra.filename,
                            .directory = extra.directory,
                        }, metadata_adapter);
                    },
                    .compile_unit,
                    .@"compile_unit optimized",
                    => |kind| {
                        const extra = self.metadataExtraData(Metadata.CompileUnit, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.CompileUnit{
                            .file = extra.file,
                            .producer = extra.producer,
                            .is_optimized = switch (kind) {
                                .compile_unit => false,
                                .@"compile_unit optimized" => true,
                                else => unreachable,
                            },
                            .enums = extra.enums,
                            .globals = extra.globals,
                        }, metadata_adapter);
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
                        const extra = self.metadataExtraData(Metadata.Subprogram, data);

                        try metadata_block.writeAbbrevAdapted(MetadataBlock.Subprogram{
                            .scope = extra.file,
                            .name = extra.name,
                            .linkage_name = extra.linkage_name,
                            .file = extra.file,
                            .line = extra.line,
                            .ty = extra.ty,
                            .scope_line = extra.scope_line,
                            .sp_flags = @bitCast(@as(u32, @as(u3, @intCast(
                                @intFromEnum(kind) - @intFromEnum(Metadata.Tag.subprogram),
                            ))) << 2),
                            .flags = extra.di_flags,
                            .compile_unit = extra.compile_unit,
                        }, metadata_adapter);
                    },
                    .lexical_block => {
                        const extra = self.metadataExtraData(Metadata.LexicalBlock, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.LexicalBlock{
                            .scope = extra.scope,
                            .file = extra.file,
                            .line = extra.line,
                            .column = extra.column,
                        }, metadata_adapter);
                    },
                    .location => {
                        const extra = self.metadataExtraData(Metadata.Location, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.Location{
                            .line = extra.line,
                            .column = extra.column,
                            .scope = extra.scope,
                            .inlined_at = extra.inlined_at,
                        }, metadata_adapter);
                    },
                    .basic_bool_type,
                    .basic_unsigned_type,
                    .basic_signed_type,
                    .basic_float_type,
                    => |kind| {
                        const extra = self.metadataExtraData(Metadata.BasicType, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.BasicType{
                            .name = extra.name,
                            .size_in_bits = extra.bitSize(),
                            .encoding = switch (kind) {
                                .basic_bool_type => DW.ATE.boolean,
                                .basic_unsigned_type => DW.ATE.unsigned,
                                .basic_signed_type => DW.ATE.signed,
                                .basic_float_type => DW.ATE.float,
                                else => unreachable,
                            },
                        }, metadata_adapter);
                    },
                    .composite_struct_type,
                    .composite_union_type,
                    .composite_enumeration_type,
                    .composite_array_type,
                    .composite_vector_type,
                    => |kind| {
                        const extra = self.metadataExtraData(Metadata.CompositeType, data);

                        try metadata_block.writeAbbrevAdapted(MetadataBlock.CompositeType{
                            .tag = switch (kind) {
                                .composite_struct_type => DW.TAG.structure_type,
                                .composite_union_type => DW.TAG.union_type,
                                .composite_enumeration_type => DW.TAG.enumeration_type,
                                .composite_array_type, .composite_vector_type => DW.TAG.array_type,
                                else => unreachable,
                            },
                            .name = extra.name,
                            .file = extra.file,
                            .line = extra.line,
                            .scope = extra.scope,
                            .underlying_type = extra.underlying_type,
                            .size_in_bits = extra.bitSize(),
                            .align_in_bits = extra.bitAlign(),
                            .flags = if (kind == .composite_vector_type) .{ .Vector = true } else .{},
                            .elements = extra.fields_tuple,
                        }, metadata_adapter);
                    },
                    .derived_pointer_type,
                    .derived_member_type,
                    .derived_typedef_type,
                    => |kind| {
                        const extra = self.metadataExtraData(Metadata.DerivedType, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.DerivedType{
                            .tag = switch (kind) {
                                .derived_pointer_type => DW.TAG.pointer_type,
                                .derived_member_type => DW.TAG.member,
                                .derived_typedef_type => DW.TAG.typedef,
                                else => unreachable,
                            },
                            .name = extra.name,
                            .file = extra.file,
                            .line = extra.line,
                            .scope = extra.scope,
                            .underlying_type = extra.underlying_type,
                            .size_in_bits = extra.bitSize(),
                            .align_in_bits = extra.bitAlign(),
                            .offset_in_bits = extra.bitOffset(),
                        }, metadata_adapter);
                    },
                    .subroutine_type => {
                        const extra = self.metadataExtraData(Metadata.SubroutineType, data);

                        try metadata_block.writeAbbrevAdapted(MetadataBlock.SubroutineType{
                            .types = extra.types_tuple,
                        }, metadata_adapter);
                    },
                    .enumerator_unsigned,
                    .enumerator_signed_positive,
                    .enumerator_signed_negative,
                    => |kind| {
                        const extra = self.metadataExtraData(Metadata.Enumerator, data);
                        const bigint: std.math.big.int.Const = .{
                            .limbs = self.metadata_limbs.items[extra.limbs_index..][0..extra.limbs_len],
                            .positive = switch (kind) {
                                .enumerator_unsigned,
                                .enumerator_signed_positive,
                                => true,
                                .enumerator_signed_negative => false,
                                else => unreachable,
                            },
                        };
                        const flags: MetadataBlock.Enumerator.Flags = .{
                            .unsigned = switch (kind) {
                                .enumerator_unsigned => true,
                                .enumerator_signed_positive,
                                .enumerator_signed_negative,
                                => false,
                                else => unreachable,
                            },
                        };
                        const val: i64 = if (bigint.toInt(i64)) |val|
                            val
                        else |_| if (bigint.toInt(u64)) |val|
                            @bitCast(val)
                        else |_| {
                            const limbs_len = std.math.divCeil(u32, extra.bit_width, 64) catch unreachable;
                            try record.ensureTotalCapacity(self.gpa, 3 + limbs_len);
                            record.appendAssumeCapacity(@as(
                                @typeInfo(MetadataBlock.Enumerator.Flags).@"struct".backing_integer.?,
                                @bitCast(flags),
                            ));
                            record.appendAssumeCapacity(extra.bit_width);
                            record.appendAssumeCapacity(metadata_adapter.getOptionalMetadataIndex(extra.name.toMetadata()));
                            const limbs = record.addManyAsSliceAssumeCapacity(limbs_len);
                            bigint.writeTwosComplement(std.mem.sliceAsBytes(limbs), .little);
                            for (limbs) |*limb| {
                                const val = std.mem.littleToNative(i64, @bitCast(limb.*));
                                limb.* = @bitCast(if (val >= 0)
                                    val << 1 | 0
                                else
                                    -%val << 1 | 1);
                            }
                            try metadata_block.writeUnabbrev(@intFromEnum(MetadataBlock.Code.ENUMERATOR), record.items);
                            continue;
                        };
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.Enumerator{
                            .flags = flags,
                            .bit_width = extra.bit_width,
                            .name = extra.name,
                            .value = @bitCast(if (val >= 0)
                                val << 1 | 0
                            else
                                -%val << 1 | 1),
                        }, metadata_adapter);
                    },
                    .subrange => {
                        const extra = self.metadataExtraData(Metadata.Subrange, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.Subrange{
                            .count = extra.count,
                            .lower_bound = extra.lower_bound,
                        }, metadata_adapter);
                    },
                    .expression => {
                        var extra = self.metadataExtraDataTrail(Metadata.Expression, data);
                        const elements = extra.trail.next(extra.data.elements_len, u32, self);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.Expression{
                            .elements = elements,
                        }, metadata_adapter);
                    },
                    .tuple => {
                        var extra = self.metadataExtraDataTrail(Metadata.Tuple, data);
                        const elements =
                            extra.trail.next(extra.data.elements_len, Metadata.Optional, self);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.Node{
                            .elements = elements,
                        }, metadata_adapter);
                    },
                    .local_var => {
                        const extra = self.metadataExtraData(Metadata.LocalVar, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.LocalVar{
                            .scope = extra.scope,
                            .name = extra.name,
                            .file = extra.file,
                            .line = extra.line,
                            .ty = extra.ty,
                        }, metadata_adapter);
                    },
                    .parameter => {
                        const extra = self.metadataExtraData(Metadata.Parameter, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.Parameter{
                            .scope = extra.scope,
                            .name = extra.name,
                            .file = extra.file,
                            .line = extra.line,
                            .ty = extra.ty,
                            .arg = extra.arg_no,
                        }, metadata_adapter);
                    },
                    .global_var,
                    .@"global_var local",
                    => |kind| {
                        const extra = self.metadataExtraData(Metadata.GlobalVar, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.GlobalVar{
                            .scope = extra.scope,
                            .name = extra.name,
                            .linkage_name = extra.linkage_name,
                            .file = extra.file,
                            .line = extra.line,
                            .ty = extra.ty,
                            .local = kind == .@"global_var local",
                        }, metadata_adapter);
                    },
                    .global_var_expression => {
                        const extra = self.metadataExtraData(Metadata.GlobalVarExpression, data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.GlobalVarExpression{
                            .variable = extra.variable,
                            .expression = extra.expression,
                        }, metadata_adapter);
                    },
                    .constant => {
                        const constant: Constant = @enumFromInt(data);
                        try metadata_block.writeAbbrevAdapted(MetadataBlock.Constant{
                            .ty = constant.typeOf(self),
                            .constant = constant,
                        }, metadata_adapter);
                    },
                }
            }

            // Write named metadata
            for (self.metadata_named.keys(), self.metadata_named.values()) |name, operands| {
                try metadata_block.writeAbbrev(MetadataBlock.Name{ .name = name.slice(self).? });
                try metadata_block.writeAbbrevAdapted(MetadataBlock.NamedNode{
                    .elements = @ptrCast(self.metadata_extra.items[operands.index..][0..operands.len]),
                }, metadata_adapter);
            }

            // Write global attached metadata
            {
                for (globals.keys()) |global_index| {
                    const global = global_index.ptrConst(self);
                    if (global.dbg.unwrap()) |dbg| {
                        switch (global.kind) {
                            .function => |f| if (f.ptrConst(self).instructions.len != 0) continue,
                            else => {},
                        }

                        try metadata_block.writeAbbrevAdapted(MetadataBlock.GlobalDeclAttachment{
                            .value = global_index.toConst(),
                            .kind = .dbg,
                            .metadata = dbg,
                        }, metadata_adapter);
                    }
                }
            }

            try metadata_block.end();
        }

        // OPERAND_BUNDLE_TAGS_BLOCK
        {
            const OperandBundleTagsBlock = ir.ModuleBlock.OperandBundleTagsBlock;
            var operand_bundle_tags_block = try module_block.enterSubBlock(OperandBundleTagsBlock, true);

            try operand_bundle_tags_block.writeAbbrev(OperandBundleTagsBlock.OperandBundleTag{
                .tag = "cold",
            });

            try operand_bundle_tags_block.end();
        }

        // Block info
        {
            const BlockInfoBlock = ir.BlockInfoBlock;
            var block_info_block = try module_block.enterSubBlock(BlockInfoBlock, true);

            try block_info_block.writeUnabbrev(BlockInfoBlock.set_block_id, &.{
                @intFromEnum(ir.ModuleBlock.FunctionBlock.id),
            });
            inline for (ir.ModuleBlock.FunctionBlock.abbrevs) |abbrev| {
                try block_info_block.defineAbbrev(&abbrev.ops);
            }

            try block_info_block.writeUnabbrev(BlockInfoBlock.set_block_id, &.{
                @intFromEnum(ir.ModuleBlock.FunctionBlock.ValueSymtabBlock.id),
            });
            inline for (ir.ModuleBlock.FunctionBlock.ValueSymtabBlock.abbrevs) |abbrev| {
                try block_info_block.defineAbbrev(&abbrev.ops);
            }

            try block_info_block.writeUnabbrev(BlockInfoBlock.set_block_id, &.{
                @intFromEnum(ir.ModuleBlock.FunctionBlock.MetadataBlock.id),
            });
            inline for (ir.ModuleBlock.FunctionBlock.MetadataBlock.abbrevs) |abbrev| {
                try block_info_block.defineAbbrev(&abbrev.ops);
            }

            try block_info_block.writeUnabbrev(BlockInfoBlock.set_block_id, &.{
                @intFromEnum(ir.ModuleBlock.FunctionBlock.MetadataAttachmentBlock.id),
            });
            inline for (ir.ModuleBlock.FunctionBlock.MetadataAttachmentBlock.abbrevs) |abbrev| {
                try block_info_block.defineAbbrev(&abbrev.ops);
            }

            try block_info_block.end();
        }

        // FUNCTION_BLOCKS
        {
            const FunctionAdapter = struct {
                metadata_adapter: MetadataAdapter,
                func: *const Function,
                instruction_index: Function.Instruction.Index,

                pub fn get(adapter: @This(), param: anytype) switch (@TypeOf(param)) {
                    Value, Constant, FunctionAttributes => u32,
                    else => |Result| Result,
                } {
                    return switch (@TypeOf(param)) {
                        Value => adapter.getOffsetValueIndex(param),
                        Constant => adapter.getOffsetConstantIndex(param),
                        FunctionAttributes => switch (param) {
                            .none => 0,
                            else => @intCast(1 + adapter.metadata_adapter.constant_adapter.builder
                                .function_attributes_set.getIndex(param).?),
                        },
                        else => param,
                    };
                }

                pub fn getValueIndex(adapter: @This(), value: Value) u32 {
                    return @intCast(switch (value.unwrap()) {
                        .instruction => |instruction| instruction.valueIndex(adapter.func) + adapter.firstInstr(),
                        .constant => |constant| adapter.metadata_adapter.constant_adapter.getConstantIndex(constant),
                        .metadata => |metadata| {
                            const builder = adapter.metadata_adapter.constant_adapter.builder;
                            const unwrapped_metadata = metadata.unwrap(builder);
                            return switch (unwrapped_metadata.kind) {
                                .string, .node => adapter.metadata_adapter.getMetadataIndex(unwrapped_metadata),
                                .forward => unreachable,
                                .local => @intCast(builder.metadata_string_map.count() +
                                    builder.metadata_map.count() +
                                    unwrapped_metadata.index),
                            };
                        },
                    });
                }

                pub fn getOffsetValueIndex(adapter: @This(), value: Value) u32 {
                    return adapter.offset() -% adapter.getValueIndex(value);
                }

                pub fn getOffsetValueSignedIndex(adapter: @This(), value: Value) i32 {
                    const signed_offset: i32 = @intCast(adapter.offset());
                    const signed_value: i32 = @intCast(adapter.getValueIndex(value));
                    return signed_offset - signed_value;
                }

                pub fn getOffsetConstantIndex(adapter: @This(), constant: Constant) u32 {
                    return adapter.offset() - adapter.constant_adapter.getConstantIndex(constant);
                }

                pub fn offset(adapter: @This()) u32 {
                    return adapter.instruction_index.valueIndex(adapter.func) + adapter.firstInstr();
                }

                fn firstInstr(adapter: @This()) u32 {
                    return adapter.metadata_adapter.constant_adapter.numConstants();
                }
            };

            for (self.functions.items, 0..) |func, func_index| {
                // Skip the function if its global has been repurposed for something else.
                switch (func.global.ptrConst(self).kind) {
                    .function => |f| if (@intFromEnum(f) != func_index) continue,
                    else => continue,
                }

                const FunctionBlock = ir.ModuleBlock.FunctionBlock;

                if (func.instructions.len == 0) continue;

                var function_block = try module_block.enterSubBlock(FunctionBlock, false);

                try function_block.writeAbbrev(FunctionBlock.DeclareBlocks{ .num_blocks = func.blocks.len });

                var adapter: FunctionAdapter = .{
                    .metadata_adapter = metadata_adapter,
                    .func = &func,
                    .instruction_index = @enumFromInt(0),
                };

                // Emit function level metadata block
                if (!func.strip and func.debug_values.len > 0) {
                    const MetadataBlock = ir.ModuleBlock.FunctionBlock.MetadataBlock;
                    var metadata_block = try function_block.enterSubBlock(MetadataBlock, false);

                    for (func.debug_values) |value| {
                        try metadata_block.writeAbbrev(MetadataBlock.Value{
                            .ty = value.typeOf(@enumFromInt(func_index), self),
                            .value = @enumFromInt(adapter.getValueIndex(value.toValue())),
                        });
                    }

                    try metadata_block.end();
                }

                const tags = func.instructions.items(.tag);
                const datas = func.instructions.items(.data);

                var has_location = false;

                var block_incoming_len: u32 = undefined;
                for (tags, datas, 0..) |tag, data, instr_index| {
                    adapter.instruction_index = @enumFromInt(instr_index);
                    record.clearRetainingCapacity();

                    switch (tag) {
                        .arg => continue,
                        .block => {
                            block_incoming_len = data;
                            continue;
                        },
                        .@"unreachable" => try function_block.writeAbbrev(FunctionBlock.Unreachable{}),
                        .call,
                        .@"musttail call",
                        .@"notail call",
                        .@"tail call",
                        => |kind| {
                            var extra = func.extraDataTrail(Function.Instruction.Call, data);

                            if (extra.data.info.has_op_bundle_cold) {
                                try function_block.writeAbbrev(FunctionBlock.ColdOperandBundle{});
                            }

                            const call_conv = extra.data.info.call_conv;
                            const args = extra.trail.next(extra.data.args_len, Value, &func);
                            try function_block.writeAbbrevAdapted(FunctionBlock.Call{
                                .attributes = extra.data.attributes,
                                .call_type = switch (kind) {
                                    .call => .{ .call_conv = call_conv },
                                    .@"tail call" => .{ .tail = true, .call_conv = call_conv },
                                    .@"musttail call" => .{ .must_tail = true, .call_conv = call_conv },
                                    .@"notail call" => .{ .no_tail = true, .call_conv = call_conv },
                                    else => unreachable,
                                },
                                .type_id = extra.data.ty,
                                .callee = extra.data.callee,
                                .args = args,
                            }, adapter);
                        },
                        .@"call fast",
                        .@"musttail call fast",
                        .@"notail call fast",
                        .@"tail call fast",
                        => |kind| {
                            var extra = func.extraDataTrail(Function.Instruction.Call, data);

                            if (extra.data.info.has_op_bundle_cold) {
                                try function_block.writeAbbrev(FunctionBlock.ColdOperandBundle{});
                            }

                            const call_conv = extra.data.info.call_conv;
                            const args = extra.trail.next(extra.data.args_len, Value, &func);
                            try function_block.writeAbbrevAdapted(FunctionBlock.CallFast{
                                .attributes = extra.data.attributes,
                                .call_type = switch (kind) {
                                    .@"call fast" => .{ .call_conv = call_conv },
                                    .@"tail call fast" => .{ .tail = true, .call_conv = call_conv },
                                    .@"musttail call fast" => .{ .must_tail = true, .call_conv = call_conv },
                                    .@"notail call fast" => .{ .no_tail = true, .call_conv = call_conv },
                                    else => unreachable,
                                },
                                .fast_math = FastMath.fast,
                                .type_id = extra.data.ty,
                                .callee = extra.data.callee,
                                .args = args,
                            }, adapter);
                        },
                        .add,
                        .@"and",
                        .fadd,
                        .fdiv,
                        .fmul,
                        .mul,
                        .frem,
                        .fsub,
                        .sdiv,
                        .sub,
                        .udiv,
                        .xor,
                        .shl,
                        .lshr,
                        .@"or",
                        .urem,
                        .srem,
                        .ashr,
                        => |kind| {
                            const extra = func.extraData(Function.Instruction.Binary, data);
                            try function_block.writeAbbrev(FunctionBlock.Binary{
                                .opcode = kind.toBinaryOpcode(),
                                .lhs = adapter.getOffsetValueIndex(extra.lhs),
                                .rhs = adapter.getOffsetValueIndex(extra.rhs),
                            });
                        },
                        .@"sdiv exact",
                        .@"udiv exact",
                        .@"lshr exact",
                        .@"ashr exact",
                        => |kind| {
                            const extra = func.extraData(Function.Instruction.Binary, data);
                            try function_block.writeAbbrev(FunctionBlock.BinaryExact{
                                .opcode = kind.toBinaryOpcode(),
                                .lhs = adapter.getOffsetValueIndex(extra.lhs),
                                .rhs = adapter.getOffsetValueIndex(extra.rhs),
                            });
                        },
                        .@"add nsw",
                        .@"add nuw",
                        .@"add nuw nsw",
                        .@"mul nsw",
                        .@"mul nuw",
                        .@"mul nuw nsw",
                        .@"sub nsw",
                        .@"sub nuw",
                        .@"sub nuw nsw",
                        .@"shl nsw",
                        .@"shl nuw",
                        .@"shl nuw nsw",
                        => |kind| {
                            const extra = func.extraData(Function.Instruction.Binary, data);
                            try function_block.writeAbbrev(FunctionBlock.BinaryNoWrap{
                                .opcode = kind.toBinaryOpcode(),
                                .lhs = adapter.getOffsetValueIndex(extra.lhs),
                                .rhs = adapter.getOffsetValueIndex(extra.rhs),
                                .flags = switch (kind) {
                                    .@"add nsw",
                                    .@"mul nsw",
                                    .@"sub nsw",
                                    .@"shl nsw",
                                    => .{ .no_unsigned_wrap = false, .no_signed_wrap = true },
                                    .@"add nuw",
                                    .@"mul nuw",
                                    .@"sub nuw",
                                    .@"shl nuw",
                                    => .{ .no_unsigned_wrap = true, .no_signed_wrap = false },
                                    .@"add nuw nsw",
                                    .@"mul nuw nsw",
                                    .@"sub nuw nsw",
                                    .@"shl nuw nsw",
                                    => .{ .no_unsigned_wrap = true, .no_signed_wrap = true },
                                    else => unreachable,
                                },
                            });
                        },
                        .@"fadd fast",
                        .@"fdiv fast",
                        .@"fmul fast",
                        .@"frem fast",
                        .@"fsub fast",
                        => |kind| {
                            const extra = func.extraData(Function.Instruction.Binary, data);
                            try function_block.writeAbbrev(FunctionBlock.BinaryFast{
                                .opcode = kind.toBinaryOpcode(),
                                .lhs = adapter.getOffsetValueIndex(extra.lhs),
                                .rhs = adapter.getOffsetValueIndex(extra.rhs),
                                .fast_math = FastMath.fast,
                            });
                        },
                        .alloca,
                        .@"alloca inalloca",
                        => |kind| {
                            const extra = func.extraData(Function.Instruction.Alloca, data);
                            const alignment = extra.info.alignment.toLlvm();
                            try function_block.writeAbbrev(FunctionBlock.Alloca{
                                .inst_type = extra.type,
                                .len_type = extra.len.typeOf(@enumFromInt(func_index), self),
                                .len_value = adapter.getValueIndex(extra.len),
                                .flags = .{
                                    .align_lower = @truncate(alignment),
                                    .inalloca = kind == .@"alloca inalloca",
                                    .explicit_type = true,
                                    .swift_error = false,
                                    .align_upper = @truncate(alignment << 5),
                                },
                            });
                        },
                        .bitcast,
                        .inttoptr,
                        .ptrtoint,
                        .fptosi,
                        .fptoui,
                        .sitofp,
                        .uitofp,
                        .addrspacecast,
                        .fptrunc,
                        .trunc,
                        .fpext,
                        .sext,
                        .zext,
                        => |kind| {
                            const extra = func.extraData(Function.Instruction.Cast, data);
                            try function_block.writeAbbrev(FunctionBlock.Cast{
                                .val = adapter.getOffsetValueIndex(extra.val),
                                .type_index = extra.type,
                                .opcode = kind.toCastOpcode(),
                            });
                        },
                        .@"fcmp false",
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
                        => |kind| {
                            const extra = func.extraData(Function.Instruction.Binary, data);
                            try function_block.writeAbbrev(FunctionBlock.Cmp{
                                .lhs = adapter.getOffsetValueIndex(extra.lhs),
                                .rhs = adapter.getOffsetValueIndex(extra.rhs),
                                .pred = kind.toCmpPredicate(),
                            });
                        },
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
                        => |kind| {
                            const extra = func.extraData(Function.Instruction.Binary, data);
                            try function_block.writeAbbrev(FunctionBlock.CmpFast{
                                .lhs = adapter.getOffsetValueIndex(extra.lhs),
                                .rhs = adapter.getOffsetValueIndex(extra.rhs),
                                .pred = kind.toCmpPredicate(),
                                .fast_math = FastMath.fast,
                            });
                        },
                        .fneg => try function_block.writeAbbrev(FunctionBlock.FNeg{
                            .val = adapter.getOffsetValueIndex(@enumFromInt(data)),
                        }),
                        .@"fneg fast" => try function_block.writeAbbrev(FunctionBlock.FNegFast{
                            .val = adapter.getOffsetValueIndex(@enumFromInt(data)),
                            .fast_math = FastMath.fast,
                        }),
                        .extractvalue => {
                            var extra = func.extraDataTrail(Function.Instruction.ExtractValue, data);
                            const indices = extra.trail.next(extra.data.indices_len, u32, &func);
                            try function_block.writeAbbrev(FunctionBlock.ExtractValue{
                                .val = adapter.getOffsetValueIndex(extra.data.val),
                                .indices = indices,
                            });
                        },
                        .extractelement => {
                            const extra = func.extraData(Function.Instruction.ExtractElement, data);
                            try function_block.writeAbbrev(FunctionBlock.ExtractElement{
                                .val = adapter.getOffsetValueIndex(extra.val),
                                .index = adapter.getOffsetValueIndex(extra.index),
                            });
                        },
                        .indirectbr => {
                            var extra =
                                func.extraDataTrail(Function.Instruction.IndirectBr, datas[instr_index]);
                            const targets =
                                extra.trail.next(extra.data.targets_len, Function.Block.Index, &func);
                            try function_block.writeAbbrevAdapted(
                                FunctionBlock.IndirectBr{
                                    .ty = extra.data.addr.typeOf(@enumFromInt(func_index), self),
                                    .addr = extra.data.addr,
                                    .targets = targets,
                                },
                                adapter,
                            );
                        },
                        .insertelement => {
                            const extra = func.extraData(Function.Instruction.InsertElement, data);
                            try function_block.writeAbbrev(FunctionBlock.InsertElement{
                                .val = adapter.getOffsetValueIndex(extra.val),
                                .elem = adapter.getOffsetValueIndex(extra.elem),
                                .index = adapter.getOffsetValueIndex(extra.index),
                            });
                        },
                        .insertvalue => {
                            var extra = func.extraDataTrail(Function.Instruction.InsertValue, datas[instr_index]);
                            const indices = extra.trail.next(extra.data.indices_len, u32, &func);
                            try function_block.writeAbbrev(FunctionBlock.InsertValue{
                                .val = adapter.getOffsetValueIndex(extra.data.val),
                                .elem = adapter.getOffsetValueIndex(extra.data.elem),
                                .indices = indices,
                            });
                        },
                        .select => {
                            const extra = func.extraData(Function.Instruction.Select, data);
                            try function_block.writeAbbrev(FunctionBlock.Select{
                                .lhs = adapter.getOffsetValueIndex(extra.lhs),
                                .rhs = adapter.getOffsetValueIndex(extra.rhs),
                                .cond = adapter.getOffsetValueIndex(extra.cond),
                            });
                        },
                        .@"select fast" => {
                            const extra = func.extraData(Function.Instruction.Select, data);
                            try function_block.writeAbbrev(FunctionBlock.SelectFast{
                                .lhs = adapter.getOffsetValueIndex(extra.lhs),
                                .rhs = adapter.getOffsetValueIndex(extra.rhs),
                                .cond = adapter.getOffsetValueIndex(extra.cond),
                                .fast_math = FastMath.fast,
                            });
                        },
                        .shufflevector => {
                            const extra = func.extraData(Function.Instruction.ShuffleVector, data);
                            try function_block.writeAbbrev(FunctionBlock.ShuffleVector{
                                .lhs = adapter.getOffsetValueIndex(extra.lhs),
                                .rhs = adapter.getOffsetValueIndex(extra.rhs),
                                .mask = adapter.getOffsetValueIndex(extra.mask),
                            });
                        },
                        .getelementptr,
                        .@"getelementptr inbounds",
                        => |kind| {
                            var extra = func.extraDataTrail(Function.Instruction.GetElementPtr, data);
                            const indices = extra.trail.next(extra.data.indices_len, Value, &func);
                            try function_block.writeAbbrevAdapted(
                                FunctionBlock.GetElementPtr{
                                    .is_inbounds = kind == .@"getelementptr inbounds",
                                    .type_index = extra.data.type,
                                    .base = extra.data.base,
                                    .indices = indices,
                                },
                                adapter,
                            );
                        },
                        .load => {
                            const extra = func.extraData(Function.Instruction.Load, data);
                            try function_block.writeAbbrev(FunctionBlock.Load{
                                .ptr = adapter.getOffsetValueIndex(extra.ptr),
                                .ty = extra.type,
                                .alignment = extra.info.alignment.toLlvm(),
                                .is_volatile = extra.info.access_kind == .@"volatile",
                            });
                        },
                        .@"load atomic" => {
                            const extra = func.extraData(Function.Instruction.Load, data);
                            try function_block.writeAbbrev(FunctionBlock.LoadAtomic{
                                .ptr = adapter.getOffsetValueIndex(extra.ptr),
                                .ty = extra.type,
                                .alignment = extra.info.alignment.toLlvm(),
                                .is_volatile = extra.info.access_kind == .@"volatile",
                                .success_ordering = extra.info.success_ordering,
                                .sync_scope = extra.info.sync_scope,
                            });
                        },
                        .store => {
                            const extra = func.extraData(Function.Instruction.Store, data);
                            try function_block.writeAbbrev(FunctionBlock.Store{
                                .ptr = adapter.getOffsetValueIndex(extra.ptr),
                                .val = adapter.getOffsetValueIndex(extra.val),
                                .alignment = extra.info.alignment.toLlvm(),
                                .is_volatile = extra.info.access_kind == .@"volatile",
                            });
                        },
                        .@"store atomic" => {
                            const extra = func.extraData(Function.Instruction.Store, data);
                            try function_block.writeAbbrev(FunctionBlock.StoreAtomic{
                                .ptr = adapter.getOffsetValueIndex(extra.ptr),
                                .val = adapter.getOffsetValueIndex(extra.val),
                                .alignment = extra.info.alignment.toLlvm(),
                                .is_volatile = extra.info.access_kind == .@"volatile",
                                .success_ordering = extra.info.success_ordering,
                                .sync_scope = extra.info.sync_scope,
                            });
                        },
                        .br => {
                            try function_block.writeAbbrev(FunctionBlock.BrUnconditional{
                                .block = data,
                            });
                        },
                        .br_cond => {
                            const extra = func.extraData(Function.Instruction.BrCond, data);
                            try function_block.writeAbbrev(FunctionBlock.BrConditional{
                                .then_block = @intFromEnum(extra.then),
                                .else_block = @intFromEnum(extra.@"else"),
                                .condition = adapter.getOffsetValueIndex(extra.cond),
                            });
                        },
                        .@"switch" => {
                            var extra = func.extraDataTrail(Function.Instruction.Switch, data);

                            try record.ensureUnusedCapacity(self.gpa, 3 + extra.data.cases_len * 2);

                            // Conditional type
                            record.appendAssumeCapacity(@intFromEnum(extra.data.val.typeOf(@enumFromInt(func_index), self)));

                            // Conditional
                            record.appendAssumeCapacity(adapter.getOffsetValueIndex(extra.data.val));

                            // Default block
                            record.appendAssumeCapacity(@intFromEnum(extra.data.default));

                            const vals = extra.trail.next(extra.data.cases_len, Constant, &func);
                            const blocks = extra.trail.next(extra.data.cases_len, Function.Block.Index, &func);
                            for (vals, blocks) |val, block| {
                                record.appendAssumeCapacity(adapter.metadata_adapter.constant_adapter.getConstantIndex(val));
                                record.appendAssumeCapacity(@intFromEnum(block));
                            }

                            try function_block.writeUnabbrev(12, record.items);
                        },
                        .va_arg => {
                            const extra = func.extraData(Function.Instruction.VaArg, data);
                            try function_block.writeAbbrev(FunctionBlock.VaArg{
                                .list_type = extra.list.typeOf(@enumFromInt(func_index), self),
                                .list = adapter.getOffsetValueIndex(extra.list),
                                .type = extra.type,
                            });
                        },
                        .phi,
                        .@"phi fast",
                        => |kind| {
                            var extra =
```
