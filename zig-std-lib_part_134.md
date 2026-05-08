```
                 },
                    else => unreachable, // Can't be out of bounds
                }
            }
        };

        // Fill in the fields we found
        for (0..fields.names.len) |i| {
            const name = fields.names[i].get(self.zoir);
            const field_index = field_indices.get(name) orelse {
                if (self.options.ignore_unknown_fields) continue;
                return self.failUnexpected(T, "field", node, i, name);
            };
            if (field_index == comptime_field) {
                return self.failComptimeField(node, i);
            }

            // Mark the field as found. Assert that the found array is not zero length to satisfy
            // the type checker (it can't be since we made it into an iteration of this loop.)
            if (field_found.len == 0) unreachable;
            field_found[field_index] = true;

            switch (field_index) {
                inline 0...(field_infos.len - 1) => |j| {
                    if (field_infos[j].is_comptime) unreachable;

                    @field(result, field_infos[j].name) = try self.parseExpr(
                        field_infos[j].type,
                        fields.vals.at(@intCast(i)),
                    );
                },
                else => unreachable, // Can't be out of bounds
            }

            initialized += 1;
        }

        // Fill in any missing default fields
        inline for (field_found, 0..) |found, i| {
            if (!found) {
                const field_info = field_infos[i];
                if (field_info.default_value_ptr) |default| {
                    const typed: *const field_info.type = @ptrCast(@alignCast(default));
                    @field(result, field_info.name) = typed.*;
                } else {
                    return self.failNodeFmt(
                        node,
                        "missing required field {s}",
                        .{field_infos[i].name},
                    );
                }
            }
        }

        return result;
    }

    fn parseTuple(self: *@This(), T: type, node: Zoir.Node.Index) !T {
        const nodes: Zoir.Node.Index.Range = switch (node.get(self.zoir)) {
            .array_literal => |nodes| nodes,
            .empty_literal => .{ .start = node, .len = 0 },
            else => return error.WrongType,
        };

        var result: T = undefined;
        const field_infos = @typeInfo(T).@"struct".fields;

        if (nodes.len > field_infos.len) {
            return self.failNodeFmt(
                nodes.at(field_infos.len),
                "index {} outside of tuple length {}",
                .{ field_infos.len, field_infos.len },
            );
        }

        inline for (0..field_infos.len) |i| {
            // Check if we're out of bounds
            if (i >= nodes.len) {
                if (field_infos[i].default_value_ptr) |default| {
                    const typed: *const field_infos[i].type = @ptrCast(@alignCast(default));
                    @field(result, field_infos[i].name) = typed.*;
                } else {
                    return self.failNodeFmt(node, "missing tuple field with index {}", .{i});
                }
            } else {
                // If we fail to parse this field, free all fields before it
                errdefer if (self.options.free_on_error) {
                    inline for (0..i) |j| {
                        if (j >= i) break;
                        free(self.gpa, result[j]);
                    }
                };

                if (field_infos[i].is_comptime) {
                    return self.failComptimeField(node, i);
                } else {
                    result[i] = try self.parseExpr(field_infos[i].type, nodes.at(i));
                }
            }
        }

        return result;
    }

    fn parseUnion(self: *@This(), T: type, node: Zoir.Node.Index) !T {
        const @"union" = @typeInfo(T).@"union";
        const field_infos = @"union".fields;

        if (field_infos.len == 0) comptime unreachable;

        // Gather info on the fields
        const field_indices = b: {
            comptime var kvs_list: [field_infos.len]struct { []const u8, usize } = undefined;
            inline for (field_infos, 0..) |field, i| {
                kvs_list[i] = .{ field.name, i };
            }
            break :b std.StaticStringMap(usize).initComptime(kvs_list);
        };

        // Parse the union
        switch (node.get(self.zoir)) {
            .enum_literal => |field_name| {
                // The union must be tagged for an enum literal to coerce to it
                if (@"union".tag_type == null) {
                    return error.WrongType;
                }

                // Get the index of the named field. We don't use `parseEnum` here as
                // the order of the enum and the order of the union might not match!
                const field_index = b: {
                    const field_name_str = field_name.get(self.zoir);
                    break :b field_indices.get(field_name_str) orelse
                        return self.failUnexpected(T, "field", node, null, field_name_str);
                };

                // Initialize the union from the given field.
                switch (field_index) {
                    inline 0...field_infos.len - 1 => |i| {
                        // Fail if the field is not void
                        if (field_infos[i].type != void)
                            return self.failNode(node, "expected union");

                        // Instantiate the union
                        return @unionInit(T, field_infos[i].name, {});
                    },
                    else => unreachable, // Can't be out of bounds
                }
            },
            .struct_literal => |struct_fields| {
                if (struct_fields.names.len != 1) {
                    return error.WrongType;
                }

                // Fill in the field we found
                const field_name = struct_fields.names[0];
                const field_name_str = field_name.get(self.zoir);
                const field_val = struct_fields.vals.at(0);
                const field_index = field_indices.get(field_name_str) orelse
                    return self.failUnexpected(T, "field", node, 0, field_name_str);

                switch (field_index) {
                    inline 0...field_infos.len - 1 => |i| {
                        if (field_infos[i].type == void) {
                            return self.failNode(field_val, "expected type 'void'");
                        } else {
                            const value = try self.parseExpr(field_infos[i].type, field_val);
                            return @unionInit(T, field_infos[i].name, value);
                        }
                    },
                    else => unreachable, // Can't be out of bounds
                }
            },
            else => return error.WrongType,
        }
    }

    fn failTokenFmt(
        self: @This(),
        token: Ast.TokenIndex,
        offset: u32,
        comptime fmt: []const u8,
        args: anytype,
    ) error{ OutOfMemory, ParseZon } {
        @branchHint(.cold);
        return self.failTokenFmtNote(token, offset, fmt, args, null);
    }

    fn failTokenFmtNote(
        self: @This(),
        token: Ast.TokenIndex,
        offset: u32,
        comptime fmt: []const u8,
        args: anytype,
        note: ?Error.TypeCheckFailure.Note,
    ) error{ OutOfMemory, ParseZon } {
        @branchHint(.cold);
        comptime assert(args.len > 0);
        if (self.diag) |s| s.type_check = .{
            .token = token,
            .offset = offset,
            .message = std.fmt.allocPrint(self.gpa, fmt, args) catch |err| {
                if (note) |n| n.deinit(self.gpa);
                return err;
            },
            .owned = true,
            .note = note,
        };
        return error.ParseZon;
    }

    fn failNodeFmt(
        self: @This(),
        node: Zoir.Node.Index,
        comptime fmt: []const u8,
        args: anytype,
    ) error{ OutOfMemory, ParseZon } {
        @branchHint(.cold);
        const token = self.ast.nodeMainToken(node.getAstNode(self.zoir));
        return self.failTokenFmt(token, 0, fmt, args);
    }

    fn failToken(
        self: @This(),
        failure: Error.TypeCheckFailure,
    ) error{ParseZon} {
        @branchHint(.cold);
        if (self.diag) |s| s.type_check = failure;
        return error.ParseZon;
    }

    fn failNode(
        self: @This(),
        node: Zoir.Node.Index,
        message: []const u8,
    ) error{ParseZon} {
        @branchHint(.cold);
        const token = self.ast.nodeMainToken(node.getAstNode(self.zoir));
        return self.failToken(.{
            .token = token,
            .offset = 0,
            .message = message,
            .owned = false,
            .note = null,
        });
    }

    fn failCannotRepresent(
        self: @This(),
        T: type,
        node: Zoir.Node.Index,
    ) error{ OutOfMemory, ParseZon } {
        @branchHint(.cold);
        return self.failNodeFmt(node, "type '{s}' cannot represent value", .{@typeName(T)});
    }

    fn failUnexpected(
        self: @This(),
        T: type,
        item_kind: []const u8,
        node: Zoir.Node.Index,
        field: ?usize,
        name: []const u8,
    ) error{ OutOfMemory, ParseZon } {
        @branchHint(.cold);
        const gpa = self.gpa;
        const token = if (field) |f| b: {
            var buf: [2]Ast.Node.Index = undefined;
            const struct_init = self.ast.fullStructInit(&buf, node.getAstNode(self.zoir)).?;
            const field_node = struct_init.ast.fields[f];
            break :b self.ast.firstToken(field_node) - 2;
        } else self.ast.nodeMainToken(node.getAstNode(self.zoir));
        switch (@typeInfo(T)) {
            inline .@"struct", .@"union", .@"enum" => |info| {
                const note: Error.TypeCheckFailure.Note = if (info.fields.len == 0) b: {
                    break :b .{
                        .token = token,
                        .offset = 0,
                        .msg = "none expected",
                        .owned = false,
                    };
                } else b: {
                    const msg = "supported: ";
                    var buf: std.ArrayList(u8) = try .initCapacity(gpa, 64);
                    defer buf.deinit(gpa);
                    try buf.appendSlice(gpa, msg);
                    inline for (info.fields, 0..) |field_info, i| {
                        if (i != 0) try buf.appendSlice(gpa, ", ");
                        try buf.print(gpa, "'{f}'", .{std.zig.fmtIdFlags(field_info.name, .{
                            .allow_primitive = true,
                            .allow_underscore = true,
                        })});
                    }
                    break :b .{
                        .token = token,
                        .offset = 0,
                        .msg = try buf.toOwnedSlice(gpa),
                        .owned = true,
                    };
                };
                return self.failTokenFmtNote(
                    token,
                    0,
                    "unexpected {s} '{s}'",
                    .{ item_kind, name },
                    note,
                );
            },
            else => comptime unreachable,
        }
    }

    // Technically we could do this if we were willing to do a deep equal to verify
    // the value matched, but doing so doesn't seem to support any real use cases
    // so isn't worth the complexity at the moment.
    fn failComptimeField(
        self: @This(),
        node: Zoir.Node.Index,
        field: usize,
    ) error{ OutOfMemory, ParseZon } {
        @branchHint(.cold);
        const ast_node = node.getAstNode(self.zoir);
        var buf: [2]Ast.Node.Index = undefined;
        const token = if (self.ast.fullStructInit(&buf, ast_node)) |struct_init| b: {
            const field_node = struct_init.ast.fields[field];
            break :b self.ast.firstToken(field_node);
        } else b: {
            const array_init = self.ast.fullArrayInit(&buf, ast_node).?;
            const value_node = array_init.ast.elements[field];
            break :b self.ast.firstToken(value_node);
        };
        return self.failToken(.{
            .token = token,
            .offset = 0,
            .message = "cannot initialize comptime field",
            .owned = false,
            .note = null,
        });
    }
};

fn intFromFloatExact(T: type, value: anytype) ?T {
    if (value > std.math.maxInt(T) or value < std.math.minInt(T)) {
        return null;
    }

    if (std.math.isNan(value) or std.math.trunc(value) != value) {
        return null;
    }

    return @intFromFloat(value);
}

fn canParseType(T: type) bool {
    comptime return canParseTypeInner(T, &.{}, false);
}

fn canParseTypeInner(
    T: type,
    /// Visited structs and unions, to avoid infinite recursion.
    /// Tracking more types is unnecessary, and a little complex due to optional nesting.
    visited: []const type,
    parent_is_optional: bool,
) bool {
    return switch (@typeInfo(T)) {
        .bool,
        .int,
        .float,
        .null,
        .@"enum",
        => true,

        .noreturn,
        .void,
        .type,
        .undefined,
        .error_union,
        .error_set,
        .@"fn",
        .frame,
        .@"anyframe",
        .@"opaque",
        .comptime_int,
        .comptime_float,
        .enum_literal,
        => false,

        .pointer => |pointer| switch (pointer.size) {
            .one => canParseTypeInner(pointer.child, visited, parent_is_optional),
            .slice => canParseTypeInner(pointer.child, visited, false),
            .many, .c => false,
        },

        .optional => |optional| if (parent_is_optional)
            false
        else
            canParseTypeInner(optional.child, visited, true),

        .array => |array| canParseTypeInner(array.child, visited, false),
        .vector => |vector| canParseTypeInner(vector.child, visited, false),

        .@"struct" => |@"struct"| {
            for (visited) |V| if (T == V) return true;
            const new_visited = visited ++ .{T};
            for (@"struct".fields) |field| {
                if (!field.is_comptime and !canParseTypeInner(field.type, new_visited, false)) {
                    return false;
                }
            }
            return true;
        },
        .@"union" => |@"union"| {
            for (visited) |V| if (T == V) return true;
            const new_visited = visited ++ .{T};
            for (@"union".fields) |field| {
                if (field.type != void and !canParseTypeInner(field.type, new_visited, false)) {
                    return false;
                }
            }
            return true;
        },
    };
}

test "std.zon parse canParseType" {
    try std.testing.expect(!comptime canParseType(void));
    try std.testing.expect(!comptime canParseType(struct { f: [*]u8 }));
    try std.testing.expect(!comptime canParseType(struct { error{foo} }));
    try std.testing.expect(!comptime canParseType(union(enum) { a: void, b: [*c]u8 }));
    try std.testing.expect(!comptime canParseType(@Vector(0, [*c]u8)));
    try std.testing.expect(!comptime canParseType(*?[*c]u8));
    try std.testing.expect(comptime canParseType(enum(u8) { _ }));
    try std.testing.expect(comptime canParseType(union { foo: void }));
    try std.testing.expect(comptime canParseType(union(enum) { foo: void }));
    try std.testing.expect(!comptime canParseType(comptime_float));
    try std.testing.expect(!comptime canParseType(comptime_int));
    try std.testing.expect(comptime canParseType(struct { comptime foo: ??u8 = null }));
    try std.testing.expect(!comptime canParseType(@TypeOf(.foo)));
    try std.testing.expect(comptime canParseType(?u8));
    try std.testing.expect(comptime canParseType(*?*u8));
    try std.testing.expect(comptime canParseType(?struct {
        foo: ?struct {
            ?union(enum) {
                a: ?@Vector(0, ?*u8),
            },
            ?struct {
                f: ?[]?u8,
            },
        },
    }));
    try std.testing.expect(!comptime canParseType(??u8));
    try std.testing.expect(!comptime canParseType(?*?u8));
    try std.testing.expect(!comptime canParseType(*?*?*u8));
    try std.testing.expect(!comptime canParseType(struct { x: comptime_int = 2 }));
    try std.testing.expect(!comptime canParseType(struct { x: comptime_float = 2 }));
    try std.testing.expect(comptime canParseType(struct { comptime x: @TypeOf(.foo) = .foo }));
    try std.testing.expect(!comptime canParseType(struct { comptime_int }));
    const Recursive = struct { foo: ?*@This() };
    try std.testing.expect(comptime canParseType(Recursive));

    // Make sure we validate nested optional before we early out due to already having seen
    // a type recursion!
    try std.testing.expect(!comptime canParseType(struct {
        add_to_visited: ?u8,
        retrieve_from_visited: ??u8,
    }));
}

test "std.zon requiresAllocator" {
    try std.testing.expect(!requiresAllocator(u8));
    try std.testing.expect(!requiresAllocator(f32));
    try std.testing.expect(!requiresAllocator(enum { foo }));
    try std.testing.expect(!requiresAllocator(struct { f32 }));
    try std.testing.expect(!requiresAllocator(struct { x: f32 }));
    try std.testing.expect(!requiresAllocator([0][]const u8));
    try std.testing.expect(!requiresAllocator([2]u8));
    try std.testing.expect(!requiresAllocator(union { x: f32, y: f32 }));
    try std.testing.expect(!requiresAllocator(union(enum) { x: f32, y: f32 }));
    try std.testing.expect(!requiresAllocator(?f32));
    try std.testing.expect(!requiresAllocator(void));
    try std.testing.expect(!requiresAllocator(@TypeOf(null)));
    try std.testing.expect(!requiresAllocator(@Vector(3, u8)));
    try std.testing.expect(!requiresAllocator(@Vector(0, *const u8)));

    try std.testing.expect(requiresAllocator([]u8));
    try std.testing.expect(requiresAllocator(*struct { u8, u8 }));
    try std.testing.expect(requiresAllocator([1][]const u8));
    try std.testing.expect(requiresAllocator(struct { x: i32, y: []u8 }));
    try std.testing.expect(requiresAllocator(union { x: i32, y: []u8 }));
    try std.testing.expect(requiresAllocator(union(enum) { x: i32, y: []u8 }));
    try std.testing.expect(requiresAllocator(?[]u8));
    try std.testing.expect(requiresAllocator(@Vector(3, *const u8)));
}

test "std.zon ast errors" {
    const gpa = std.testing.allocator;
    var diag: Diagnostics = .{};
    defer diag.deinit(gpa);
    try std.testing.expectError(
        error.ParseZon,
        fromSlice(struct {}, gpa, ".{.x = 1 .y = 2}", &diag, .{}),
    );
    try std.testing.expectFmt("1:13: error: expected ',' after initializer\n", "{f}", .{diag});
}

test "std.zon comments" {
    const gpa = std.testing.allocator;

    try std.testing.expectEqual(@as(u8, 10), fromSlice(u8, gpa,
        \\// comment
        \\10 // comment
        \\// comment
    , null, .{}));

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(u8, gpa,
            \\//! comment
            \\10 // comment
            \\// comment
        , &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: expected expression, found 'a document comment'\n",
            "{f}",
            .{diag},
        );
    }
}

test "std.zon failure/oom formatting" {
    const gpa = std.testing.allocator;
    var failing_allocator = std.testing.FailingAllocator.init(gpa, .{
        .fail_index = 0,
        .resize_fail_index = 0,
    });
    var diag: Diagnostics = .{};
    defer diag.deinit(gpa);
    try std.testing.expectError(error.OutOfMemory, fromSliceAlloc(
        []const u8,
        failing_allocator.allocator(),
        "\"foo\"",
        &diag,
        .{},
    ));
    try std.testing.expectFmt("", "{f}", .{diag});
}

test "std.zon fromSliceAlloc syntax error" {
    try std.testing.expectError(
        error.ParseZon,
        fromSlice(u8, std.testing.allocator, ".{", null, .{}),
    );
}

test "std.zon optional" {
    const gpa = std.testing.allocator;

    // Basic usage
    {
        const none = try fromSlice(?u32, gpa, "null", null, .{});
        try std.testing.expect(none == null);
        const some = try fromSlice(?u32, gpa, "1", null, .{});
        try std.testing.expect(some.? == 1);
    }

    // Deep free
    {
        const none = try fromSliceAlloc(?[]const u8, gpa, "null", null, .{});
        try std.testing.expect(none == null);
        const some = try fromSliceAlloc(?[]const u8, gpa, "\"foo\"", null, .{});
        defer free(gpa, some);
        try std.testing.expectEqualStrings("foo", some.?);
    }
}

test "std.zon unions" {
    const gpa = std.testing.allocator;

    // Unions
    {
        const Tagged = union(enum) { x: f32, @"y y": bool, z, @"z z" };
        const Untagged = union { x: f32, @"y y": bool, z: void, @"z z": void };

        const tagged_x = try fromSlice(Tagged, gpa, ".{.x = 1.5}", null, .{});
        try std.testing.expectEqual(Tagged{ .x = 1.5 }, tagged_x);
        const tagged_y = try fromSlice(Tagged, gpa, ".{.@\"y y\" = true}", null, .{});
        try std.testing.expectEqual(Tagged{ .@"y y" = true }, tagged_y);
        const tagged_z_shorthand = try fromSlice(Tagged, gpa, ".z", null, .{});
        try std.testing.expectEqual(@as(Tagged, .z), tagged_z_shorthand);
        const tagged_zz_shorthand = try fromSlice(Tagged, gpa, ".@\"z z\"", null, .{});
        try std.testing.expectEqual(@as(Tagged, .@"z z"), tagged_zz_shorthand);

        const untagged_x = try fromSlice(Untagged, gpa, ".{.x = 1.5}", null, .{});
        try std.testing.expect(untagged_x.x == 1.5);
        const untagged_y = try fromSlice(Untagged, gpa, ".{.@\"y y\" = true}", null, .{});
        try std.testing.expect(untagged_y.@"y y");
    }

    // Deep free
    {
        const Union = union(enum) { bar: []const u8, baz: bool };

        const noalloc = try fromSliceAlloc(Union, gpa, ".{.baz = false}", null, .{});
        try std.testing.expectEqual(Union{ .baz = false }, noalloc);

        const alloc = try fromSliceAlloc(Union, gpa, ".{.bar = \"qux\"}", null, .{});
        defer free(gpa, alloc);
        try std.testing.expectEqualDeep(Union{ .bar = "qux" }, alloc);
    }

    // Unknown field
    {
        const Union = union { x: f32, y: f32 };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(Union, gpa, ".{.z=2.5}", &diag, .{}),
        );
        try std.testing.expectFmt(
            \\1:4: error: unexpected field 'z'
            \\1:4: note: supported: 'x', 'y'
            \\
        ,
            "{f}",
            .{diag},
        );
    }

    // Explicit void field
    {
        const Union = union(enum) { x: void };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(Union, gpa, ".{.x=1}", &diag, .{}),
        );
        try std.testing.expectFmt("1:6: error: expected type 'void'\n", "{f}", .{diag});
    }

    // Extra field
    {
        const Union = union { x: f32, y: bool };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(Union, gpa, ".{.x = 1.5, .y = true}", &diag, .{}),
        );
        try std.testing.expectFmt("1:2: error: expected union\n", "{f}", .{diag});
    }

    // No fields
    {
        const Union = union { x: f32, y: bool };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(Union, gpa, ".{}", &diag, .{}),
        );
        try std.testing.expectFmt("1:2: error: expected union\n", "{f}", .{diag});
    }

    // Enum literals cannot coerce into untagged unions
    {
        const Union = union { x: void };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSliceAlloc(Union, gpa, ".x", &diag, .{}));
        try std.testing.expectFmt("1:2: error: expected union\n", "{f}", .{diag});
    }

    // Unknown field for enum literal coercion
    {
        const Union = union(enum) { x: void };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSliceAlloc(Union, gpa, ".y", &diag, .{}));
        try std.testing.expectFmt(
            \\1:2: error: unexpected field 'y'
            \\1:2: note: supported: 'x'
            \\
        ,
            "{f}",
            .{diag},
        );
    }

    // Non void field for enum literal coercion
    {
        const Union = union(enum) { x: f32 };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSliceAlloc(Union, gpa, ".x", &diag, .{}));
        try std.testing.expectFmt("1:2: error: expected union\n", "{f}", .{diag});
    }
}

test "std.zon structs" {
    const gpa = std.testing.allocator;

    // Structs (various sizes tested since they're parsed differently)
    {
        const Vec0 = struct {};
        const Vec1 = struct { x: f32 };
        const Vec2 = struct { x: f32, y: f32 };
        const Vec3 = struct { x: f32, y: f32, z: f32 };

        const zero = try fromSlice(Vec0, gpa, ".{}", null, .{});
        try std.testing.expectEqual(Vec0{}, zero);

        const one = try fromSlice(Vec1, gpa, ".{.x = 1.2}", null, .{});
        try std.testing.expectEqual(Vec1{ .x = 1.2 }, one);

        const two = try fromSlice(Vec2, gpa, ".{.x = 1.2, .y = 3.4}", null, .{});
        try std.testing.expectEqual(Vec2{ .x = 1.2, .y = 3.4 }, two);

        const three = try fromSlice(Vec3, gpa, ".{.x = 1.2, .y = 3.4, .z = 5.6}", null, .{});
        try std.testing.expectEqual(Vec3{ .x = 1.2, .y = 3.4, .z = 5.6 }, three);
    }

    // Deep free (structs and arrays)
    {
        const Foo = struct { bar: []const u8, baz: []const []const u8 };

        const parsed = try fromSliceAlloc(
            Foo,
            gpa,
            ".{.bar = \"qux\", .baz = .{\"a\", \"b\"}}",
            null,
            .{},
        );
        defer free(gpa, parsed);
        try std.testing.expectEqualDeep(Foo{ .bar = "qux", .baz = &.{ "a", "b" } }, parsed);
    }

    // Unknown field
    {
        const Vec2 = struct { x: f32, y: f32 };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Vec2, gpa, ".{.x=1.5, .z=2.5}", &diag, .{}),
        );
        try std.testing.expectFmt(
            \\1:12: error: unexpected field 'z'
            \\1:12: note: supported: 'x', 'y'
            \\
        ,
            "{f}",
            .{diag},
        );
    }

    // Duplicate field
    {
        const Vec2 = struct { x: f32, y: f32 };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Vec2, gpa, ".{.x=1.5, .x=2.5, .x=3.5}", &diag, .{}),
        );
        try std.testing.expectFmt(
            \\1:4: error: duplicate struct field name
            \\1:12: note: duplicate name here
            \\
        , "{f}", .{diag});
    }

    // Ignore unknown fields
    {
        const Vec2 = struct { x: f32, y: f32 = 2.0 };
        const parsed = try fromSlice(Vec2, gpa, ".{ .x = 1.0, .z = 3.0 }", null, .{
            .ignore_unknown_fields = true,
        });
        try std.testing.expectEqual(Vec2{ .x = 1.0, .y = 2.0 }, parsed);
    }

    // Unknown field when struct has no fields (regression test)
    {
        const Vec2 = struct {};
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Vec2, gpa, ".{.x=1.5, .z=2.5}", &diag, .{}),
        );
        try std.testing.expectFmt(
            \\1:4: error: unexpected field 'x'
            \\1:4: note: none expected
            \\
        , "{f}", .{diag});
    }

    // Missing field
    {
        const Vec2 = struct { x: f32, y: f32 };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Vec2, gpa, ".{.x=1.5}", &diag, .{}),
        );
        try std.testing.expectFmt("1:2: error: missing required field y\n", "{f}", .{diag});
    }

    // Default field
    {
        const Vec2 = struct { x: f32, y: f32 = 1.5 };
        const parsed = try fromSlice(Vec2, gpa, ".{.x = 1.2}", null, .{});
        try std.testing.expectEqual(Vec2{ .x = 1.2, .y = 1.5 }, parsed);
    }

    // Comptime field
    {
        const Vec2 = struct { x: f32, comptime y: f32 = 1.5 };
        const parsed = try fromSlice(Vec2, gpa, ".{.x = 1.2}", null, .{});
        try std.testing.expectEqual(Vec2{ .x = 1.2, .y = 1.5 }, parsed);
    }

    // Comptime field assignment
    {
        const Vec2 = struct { x: f32, comptime y: f32 = 1.5 };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        const parsed = fromSlice(Vec2, gpa, ".{.x = 1.2, .y = 1.5}", &diag, .{});
        try std.testing.expectError(error.ParseZon, parsed);
        try std.testing.expectFmt(
            \\1:18: error: cannot initialize comptime field
            \\
        , "{f}", .{diag});
    }

    // Enum field (regression test, we were previously getting the field name in an
    // incorrect way that broke for enum values)
    {
        const Vec0 = struct { x: enum { x } };
        const parsed = try fromSlice(Vec0, gpa, ".{ .x = .x }", null, .{});
        try std.testing.expectEqual(Vec0{ .x = .x }, parsed);
    }

    // Enum field and struct field with @
    {
        const Vec0 = struct { @"x x": enum { @"x x" } };
        const parsed = try fromSlice(Vec0, gpa, ".{ .@\"x x\" = .@\"x x\" }", null, .{});
        try std.testing.expectEqual(Vec0{ .@"x x" = .@"x x" }, parsed);
    }

    // Type expressions are not allowed
    {
        // Structs
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            const parsed = fromSlice(struct {}, gpa, "Empty{}", &diag, .{});
            try std.testing.expectError(error.ParseZon, parsed);
            try std.testing.expectFmt(
                \\1:1: error: types are not available in ZON
                \\1:1: note: replace the type with '.'
                \\
            , "{f}", .{diag});
        }

        // Arrays
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            const parsed = fromSlice([3]u8, gpa, "[3]u8{1, 2, 3}", &diag, .{});
            try std.testing.expectError(error.ParseZon, parsed);
            try std.testing.expectFmt(
                \\1:1: error: types are not available in ZON
                \\1:1: note: replace the type with '.'
                \\
            , "{f}", .{diag});
        }

        // Slices
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            const parsed = fromSliceAlloc([]u8, gpa, "[]u8{1, 2, 3}", &diag, .{});
            try std.testing.expectError(error.ParseZon, parsed);
            try std.testing.expectFmt(
                \\1:1: error: types are not available in ZON
                \\1:1: note: replace the type with '.'
                \\
            , "{f}", .{diag});
        }

        // Tuples
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            const parsed = fromSlice(
                struct { u8, u8, u8 },
                gpa,
                "Tuple{1, 2, 3}",
                &diag,
                .{},
            );
            try std.testing.expectError(error.ParseZon, parsed);
            try std.testing.expectFmt(
                \\1:1: error: types are not available in ZON
                \\1:1: note: replace the type with '.'
                \\
            , "{f}", .{diag});
        }

        // Nested
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            const parsed = fromSlice(struct {}, gpa, ".{ .x = Tuple{1, 2, 3} }", &diag, .{});
            try std.testing.expectError(error.ParseZon, parsed);
            try std.testing.expectFmt(
                \\1:9: error: types are not available in ZON
                \\1:9: note: replace the type with '.'
                \\
            , "{f}", .{diag});
        }
    }
}

test "std.zon tuples" {
    const gpa = std.testing.allocator;

    // Structs (various sizes tested since they're parsed differently)
    {
        const Tuple0 = struct {};
        const Tuple1 = struct { f32 };
        const Tuple2 = struct { f32, bool };
        const Tuple3 = struct { f32, bool, u8 };

        const zero = try fromSlice(Tuple0, gpa, ".{}", null, .{});
        try std.testing.expectEqual(Tuple0{}, zero);

        const one = try fromSlice(Tuple1, gpa, ".{1.2}", null, .{});
        try std.testing.expectEqual(Tuple1{1.2}, one);

        const two = try fromSlice(Tuple2, gpa, ".{1.2, true}", null, .{});
        try std.testing.expectEqual(Tuple2{ 1.2, true }, two);

        const three = try fromSlice(Tuple3, gpa, ".{1.2, false, 3}", null, .{});
        try std.testing.expectEqual(Tuple3{ 1.2, false, 3 }, three);
    }

    // Deep free
    {
        const Tuple = struct { []const u8, []const u8 };
        const parsed = try fromSliceAlloc(Tuple, gpa, ".{\"hello\", \"world\"}", null, .{});
        defer free(gpa, parsed);
        try std.testing.expectEqualDeep(Tuple{ "hello", "world" }, parsed);
    }

    // Extra field
    {
        const Tuple = struct { f32, bool };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Tuple, gpa, ".{0.5, true, 123}", &diag, .{}),
        );
        try std.testing.expectFmt("1:14: error: index 2 outside of tuple length 2\n", "{f}", .{diag});
    }

    // Extra field
    {
        const Tuple = struct { f32, bool };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Tuple, gpa, ".{0.5}", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:2: error: missing tuple field with index 1\n",
            "{f}",
            .{diag},
        );
    }

    // Tuple with unexpected field names
    {
        const Tuple = struct { f32 };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Tuple, gpa, ".{.foo = 10.0}", &diag, .{}),
        );
        try std.testing.expectFmt("1:2: error: expected tuple\n", "{f}", .{diag});
    }

    // Struct with missing field names
    {
        const Struct = struct { foo: f32 };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Struct, gpa, ".{10.0}", &diag, .{}),
        );
        try std.testing.expectFmt("1:2: error: expected struct\n", "{f}", .{diag});
    }

    // Comptime field
    {
        const Vec2 = struct { f32, comptime f32 = 1.5 };
        const parsed = try fromSlice(Vec2, gpa, ".{ 1.2 }", null, .{});
        try std.testing.expectEqual(Vec2{ 1.2, 1.5 }, parsed);
    }

    // Comptime field assignment
    {
        const Vec2 = struct { f32, comptime f32 = 1.5 };
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        const parsed = fromSlice(Vec2, gpa, ".{ 1.2, 1.5}", &diag, .{});
        try std.testing.expectError(error.ParseZon, parsed);
        try std.testing.expectFmt(
            \\1:9: error: cannot initialize comptime field
            \\
        , "{f}", .{diag});
    }
}

// Test sizes 0 to 3 since small sizes get parsed differently
test "std.zon arrays and slices" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/20881

    const gpa = std.testing.allocator;

    // Literals
    {
        // Arrays
        {
            const zero = try fromSlice([0]u8, gpa, ".{}", null, .{});
            try std.testing.expectEqualSlices(u8, &@as([0]u8, .{}), &zero);

            const one = try fromSlice([1]u8, gpa, ".{'a'}", null, .{});
            try std.testing.expectEqualSlices(u8, &@as([1]u8, .{'a'}), &one);

            const two = try fromSlice([2]u8, gpa, ".{'a', 'b'}", null, .{});
            try std.testing.expectEqualSlices(u8, &@as([2]u8, .{ 'a', 'b' }), &two);

            const two_comma = try fromSlice([2]u8, gpa, ".{'a', 'b',}", null, .{});
            try std.testing.expectEqualSlices(u8, &@as([2]u8, .{ 'a', 'b' }), &two_comma);

            const three = try fromSlice([3]u8, gpa, ".{'a', 'b', 'c'}", null, .{});
            try std.testing.expectEqualSlices(u8, &.{ 'a', 'b', 'c' }, &three);

            const sentinel = try fromSlice([3:'z']u8, gpa, ".{'a', 'b', 'c'}", null, .{});
            const expected_sentinel: [3:'z']u8 = .{ 'a', 'b', 'c' };
            try std.testing.expectEqualSlices(u8, &expected_sentinel, &sentinel);
        }

        // Slice literals
        {
            const zero = try fromSliceAlloc([]const u8, gpa, ".{}", null, .{});
            defer free(gpa, zero);
            try std.testing.expectEqualSlices(u8, @as([]const u8, &.{}), zero);

            const one = try fromSliceAlloc([]u8, gpa, ".{'a'}", null, .{});
            defer free(gpa, one);
            try std.testing.expectEqualSlices(u8, &.{'a'}, one);

            const two = try fromSliceAlloc([]const u8, gpa, ".{'a', 'b'}", null, .{});
            defer free(gpa, two);
            try std.testing.expectEqualSlices(u8, &.{ 'a', 'b' }, two);

            const two_comma = try fromSliceAlloc([]const u8, gpa, ".{'a', 'b',}", null, .{});
            defer free(gpa, two_comma);
            try std.testing.expectEqualSlices(u8, &.{ 'a', 'b' }, two_comma);

            const three = try fromSliceAlloc([]u8, gpa, ".{'a', 'b', 'c'}", null, .{});
            defer free(gpa, three);
            try std.testing.expectEqualSlices(u8, &.{ 'a', 'b', 'c' }, three);

            const sentinel = try fromSliceAlloc([:'z']const u8, gpa, ".{'a', 'b', 'c'}", null, .{});
            defer free(gpa, sentinel);
            const expected_sentinel: [:'z']const u8 = &.{ 'a', 'b', 'c' };
            try std.testing.expectEqualSlices(u8, expected_sentinel, sentinel);
        }
    }

    // Deep free
    {
        // Arrays
        {
            const parsed = try fromSliceAlloc([1][]const u8, gpa, ".{\"abc\"}", null, .{});
            defer free(gpa, parsed);
            const expected: [1][]const u8 = .{"abc"};
            try std.testing.expectEqualDeep(expected, parsed);
        }

        // Slice literals
        {
            const parsed = try fromSliceAlloc([]const []const u8, gpa, ".{\"abc\"}", null, .{});
            defer free(gpa, parsed);
            const expected: []const []const u8 = &.{"abc"};
            try std.testing.expectEqualDeep(expected, parsed);
        }
    }

    // Sentinels and alignment
    {
        // Arrays
        {
            const sentinel = try fromSlice([1:2]u8, gpa, ".{1}", null, .{});
            try std.testing.expectEqual(@as(usize, 1), sentinel.len);
            try std.testing.expectEqual(@as(u8, 1), sentinel[0]);
            try std.testing.expectEqual(@as(u8, 2), sentinel[1]);
        }

        // Slice literals
        {
            const sentinel = try fromSliceAlloc([:2]align(4) u8, gpa, ".{1}", null, .{});
            defer free(gpa, sentinel);
            try std.testing.expectEqual(@as(usize, 1), sentinel.len);
            try std.testing.expectEqual(@as(u8, 1), sentinel[0]);
            try std.testing.expectEqual(@as(u8, 2), sentinel[1]);
        }
    }

    // Expect 0 find 3
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice([0]u8, gpa, ".{'a', 'b', 'c'}", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:3: error: index 0 outside of array of length 0\n",
            "{f}",
            .{diag},
        );
    }

    // Expect 1 find 2
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice([1]u8, gpa, ".{'a', 'b'}", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:8: error: index 1 outside of array of length 1\n",
            "{f}",
            .{diag},
        );
    }

    // Expect 2 find 1
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice([2]u8, gpa, ".{'a'}", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:2: error: expected 2 array elements; found 1\n",
            "{f}",
            .{diag},
        );
    }

    // Expect 3 find 0
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice([3]u8, gpa, ".{}", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:2: error: expected 3 array elements; found 0\n",
            "{f}",
            .{diag},
        );
    }

    // Wrong inner type
    {
        // Array
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSlice([3]bool, gpa, ".{'a', 'b', 'c'}", &diag, .{}),
            );
            try std.testing.expectFmt("1:3: error: expected type 'bool'\n", "{f}", .{diag});
        }

        // Slice
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([]bool, gpa, ".{'a', 'b', 'c'}", &diag, .{}),
            );
            try std.testing.expectFmt("1:3: error: expected type 'bool'\n", "{f}", .{diag});
        }
    }

    // Complete wrong type
    {
        // Array
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSlice([3]u8, gpa, "'a'", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }

        // Slice
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([]u8, gpa, "'a'", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }
    }

    // Address of is not allowed (indirection for slices in ZON is implicit)
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc([]u8, gpa, "  &.{'a', 'b', 'c'}", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:3: error: pointers are not available in ZON\n",
            "{f}",
            .{diag},
        );
    }
}

test "std.zon string literal" {
    const gpa = std.testing.allocator;

    // Basic string literal
    {
        const parsed = try fromSliceAlloc([]const u8, gpa, "\"abc\"", null, .{});
        defer free(gpa, parsed);
        try std.testing.expectEqualStrings(@as([]const u8, "abc"), parsed);
    }

    // String literal with escape characters
    {
        const parsed = try fromSliceAlloc([]const u8, gpa, "\"ab\\nc\"", null, .{});
        defer free(gpa, parsed);
        try std.testing.expectEqualStrings(@as([]const u8, "ab\nc"), parsed);
    }

    // String literal with embedded null
    {
        const parsed = try fromSliceAlloc([]const u8, gpa, "\"ab\\x00c\"", null, .{});
        defer free(gpa, parsed);
        try std.testing.expectEqualStrings(@as([]const u8, "ab\x00c"), parsed);
    }

    // Passing string literal to a mutable slice
    {
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([]u8, gpa, "\"abcd\"", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }

        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([]u8, gpa, "\\\\abcd", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }
    }

    // Passing string literal to a array
    {
        {
            var ast = try std.zig.Ast.parse(gpa, "\"abcd\"", .zon);
            defer ast.deinit(gpa);
            var zoir = try ZonGen.generate(gpa, ast, .{ .parse_str_lits = false });
            defer zoir.deinit(gpa);
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSlice([4:0]u8, gpa, "\"abcd\"", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }

        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSlice([4:0]u8, gpa, "\\\\abcd", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }
    }

    // Zero terminated slices
    {
        {
            const parsed: [:0]const u8 = try fromSliceAlloc(
                [:0]const u8,
                gpa,
                "\"abc\"",
                null,
                .{},
            );
            defer free(gpa, parsed);
            try std.testing.expectEqualStrings("abc", parsed);
            try std.testing.expectEqual(@as(u8, 0), parsed[3]);
        }

        {
            const parsed: [:0]const u8 = try fromSliceAlloc(
                [:0]const u8,
                gpa,
                "\\\\abc",
                null,
                .{},
            );
            defer free(gpa, parsed);
            try std.testing.expectEqualStrings("abc", parsed);
            try std.testing.expectEqual(@as(u8, 0), parsed[3]);
        }
    }

    // Other value terminated slices
    {
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([:1]const u8, gpa, "\"foo\"", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }

        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([:1]const u8, gpa, "\\\\foo", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }
    }

    // Expecting string literal, getting something else
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc([]const u8, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected string\n", "{f}", .{diag});
    }

    // Expecting string literal, getting an incompatible tuple
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc([]const u8, gpa, ".{false}", &diag, .{}),
        );
        try std.testing.expectFmt("1:3: error: expected type 'u8'\n", "{f}", .{diag});
    }

    // Invalid string literal
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc([]const i8, gpa, "\"\\a\"", &diag, .{}),
        );
        try std.testing.expectFmt("1:3: error: invalid escape character: 'a'\n", "{f}", .{diag});
    }

    // Slice wrong child type
    {
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([]const i8, gpa, "\"a\"", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }

        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([]const i8, gpa, "\\\\a", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }
    }

    // Bad alignment
    {
        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([]align(2) const u8, gpa, "\"abc\"", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }

        {
            var diag: Diagnostics = .{};
            defer diag.deinit(gpa);
            try std.testing.expectError(
                error.ParseZon,
                fromSliceAlloc([]align(2) const u8, gpa, "\\\\abc", &diag, .{}),
            );
            try std.testing.expectFmt("1:1: error: expected array\n", "{f}", .{diag});
        }
    }

    // Multi line strings
    inline for (.{ []const u8, [:0]const u8 }) |String| {
        // Nested
        {
            const S = struct {
                message: String,
                message2: String,
                message3: String,
            };
            const parsed = try fromSliceAlloc(S, gpa,
                \\.{
                \\    .message =
                \\        \\hello, world!
                \\
                \\        \\this is a multiline string!
                \\        \\
                \\        \\...
                \\
                \\    ,
                \\    .message2 =
                \\        \\this too...sort of.
                \\    ,
                \\    .message3 =
                \\        \\
                \\        \\and this.
                \\}
            , null, .{});
            defer free(gpa, parsed);
            try std.testing.expectEqualStrings(
                "hello, world!\nthis is a multiline string!\n\n...",
                parsed.message,
            );
            try std.testing.expectEqualStrings("this too...sort of.", parsed.message2);
            try std.testing.expectEqualStrings("\nand this.", parsed.message3);
        }
    }
}

test "std.zon enum literals" {
    const gpa = std.testing.allocator;

    const Enum = enum {
        foo,
        bar,
        baz,
        @"ab\nc",
    };

    // Tags that exist
    try std.testing.expectEqual(Enum.foo, try fromSlice(Enum, gpa, ".foo", null, .{}));
    try std.testing.expectEqual(Enum.bar, try fromSlice(Enum, gpa, ".bar", null, .{}));
    try std.testing.expectEqual(Enum.baz, try fromSlice(Enum, gpa, ".baz", null, .{}));
    try std.testing.expectEqual(
        Enum.@"ab\nc",
        try fromSlice(Enum, gpa, ".@\"ab\\nc\"", null, .{}),
    );

    // Bad tag
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Enum, gpa, ".qux", &diag, .{}),
        );
        try std.testing.expectFmt(
            \\1:2: error: unexpected enum literal 'qux'
            \\1:2: note: supported: 'foo', 'bar', 'baz', '@"ab\nc"'
            \\
        ,
            "{f}",
            .{diag},
        );
    }

    // Bad tag that's too long for parser
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Enum, gpa, ".@\"foobarbaz\"", &diag, .{}),
        );
        try std.testing.expectFmt(
            \\1:2: error: unexpected enum literal 'foobarbaz'
            \\1:2: note: supported: 'foo', 'bar', 'baz', '@"ab\nc"'
            \\
        ,
            "{f}",
            .{diag},
        );
    }

    // Bad type
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Enum, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected enum literal\n", "{f}", .{diag});
    }

    // Test embedded nulls in an identifier
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(Enum, gpa, ".@\"\\x00\"", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:2: error: identifier cannot contain null bytes\n",
            "{f}",
            .{diag},
        );
    }
}

test "std.zon parse bool" {
    const gpa = std.testing.allocator;

    // Correct bools
    try std.testing.expectEqual(true, try fromSlice(bool, gpa, "true", null, .{}));
    try std.testing.expectEqual(false, try fromSlice(bool, gpa, "false", null, .{}));

    // Errors
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(bool, gpa, " foo", &diag, .{}),
        );
        try std.testing.expectFmt(
            \\1:2: error: invalid expression
            \\1:2: note: ZON allows identifiers 'true', 'false', 'null', 'inf', and 'nan'
            \\1:2: note: precede identifier with '.' for an enum literal
            \\
        , "{f}", .{diag});
    }
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(bool, gpa, "123", &diag, .{}));
        try std.testing.expectFmt("1:1: error: expected type 'bool'\n", "{f}", .{diag});
    }
}

test "std.zon intFromFloatExact" {
    // Valid conversions
    try std.testing.expectEqual(@as(u8, 10), intFromFloatExact(u8, @as(f32, 10.0)).?);
    try std.testing.expectEqual(@as(i8, -123), intFromFloatExact(i8, @as(f64, @as(f64, -123.0))).?);
    try std.testing.expectEqual(@as(i16, 45), intFromFloatExact(i16, @as(f128, @as(f128, 45.0))).?);

    // Out of range
    try std.testing.expectEqual(@as(?u4, null), intFromFloatExact(u4, @as(f32, 16.0)));
    try std.testing.expectEqual(@as(?i4, null), intFromFloatExact(i4, @as(f64, -17.0)));
    try std.testing.expectEqual(@as(?u8, null), intFromFloatExact(u8, @as(f128, -2.0)));

    // Not a whole number
    try std.testing.expectEqual(@as(?u8, null), intFromFloatExact(u8, @as(f32, 0.5)));
    try std.testing.expectEqual(@as(?i8, null), intFromFloatExact(i8, @as(f64, 0.01)));

    // Infinity and NaN
    try std.testing.expectEqual(@as(?u8, null), intFromFloatExact(u8, std.math.inf(f32)));
    try std.testing.expectEqual(@as(?u8, null), intFromFloatExact(u8, -std.math.inf(f32)));
    try std.testing.expectEqual(@as(?u8, null), intFromFloatExact(u8, std.math.nan(f32)));
}

test "std.zon parse int" {
    const gpa = std.testing.allocator;

    // Test various numbers and types
    try std.testing.expectEqual(@as(u8, 10), try fromSlice(u8, gpa, "10", null, .{}));
    try std.testing.expectEqual(@as(i16, 24), try fromSlice(i16, gpa, "24", null, .{}));
    try std.testing.expectEqual(@as(i14, -4), try fromSlice(i14, gpa, "-4", null, .{}));
    try std.testing.expectEqual(@as(i32, -123), try fromSlice(i32, gpa, "-123", null, .{}));

    // Test limits
    try std.testing.expectEqual(@as(i8, 127), try fromSlice(i8, gpa, "127", null, .{}));
    try std.testing.expectEqual(@as(i8, -128), try fromSlice(i8, gpa, "-128", null, .{}));

    // Test characters
    try std.testing.expectEqual(@as(u8, 'a'), try fromSlice(u8, gpa, "'a'", null, .{}));
    try std.testing.expectEqual(@as(u8, 'z'), try fromSlice(u8, gpa, "'z'", null, .{}));

    // Test big integers
    try std.testing.expectEqual(
        @as(u65, 36893488147419103231),
        try fromSlice(u65, gpa, "36893488147419103231", null, .{}),
    );
    try std.testing.expectEqual(
        @as(u65, 36893488147419103231),
        try fromSlice(u65, gpa, "368934_881_474191032_31", null, .{}),
    );

    // Test big integer limits
    try std.testing.expectEqual(
        @as(i66, 36893488147419103231),
        try fromSlice(i66, gpa, "36893488147419103231", null, .{}),
    );
    try std.testing.expectEqual(
        @as(i66, -36893488147419103232),
        try fromSlice(i66, gpa, "-36893488147419103232", null, .{}),
    );
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(
            i66,
            gpa,
            "36893488147419103232",
            &diag,
            .{},
        ));
        try std.testing.expectFmt(
            "1:1: error: type 'i66' cannot represent value\n",
            "{f}",
            .{diag},
        );
    }
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(
            i66,
            gpa,
            "-36893488147419103233",
            &diag,
            .{},
        ));
        try std.testing.expectFmt(
            "1:1: error: type 'i66' cannot represent value\n",
            "{f}",
            .{diag},
        );
    }

    // Test parsing whole number floats as integers
    try std.testing.expectEqual(@as(i8, -1), try fromSlice(i8, gpa, "-1.0", null, .{}));
    try std.testing.expectEqual(@as(i8, 123), try fromSlice(i8, gpa, "123.0", null, .{}));

    // Test non-decimal integers
    try std.testing.expectEqual(@as(i16, 0xff), try fromSlice(i16, gpa, "0xff", null, .{}));
    try std.testing.expectEqual(@as(i16, -0xff), try fromSlice(i16, gpa, "-0xff", null, .{}));
    try std.testing.expectEqual(@as(i16, 0o77), try fromSlice(i16, gpa, "0o77", null, .{}));
    try std.testing.expectEqual(@as(i16, -0o77), try fromSlice(i16, gpa, "-0o77", null, .{}));
    try std.testing.expectEqual(@as(i16, 0b11), try fromSlice(i16, gpa, "0b11", null, .{}));
    try std.testing.expectEqual(@as(i16, -0b11), try fromSlice(i16, gpa, "-0b11", null, .{}));

    // Test non-decimal big integers
    try std.testing.expectEqual(@as(u65, 0x1ffffffffffffffff), try fromSlice(
        u65,
        gpa,
        "0x1ffffffffffffffff",
        null,
        .{},
    ));
    try std.testing.expectEqual(@as(i66, 0x1ffffffffffffffff), try fromSlice(
        i66,
        gpa,
        "0x1ffffffffffffffff",
        null,
        .{},
    ));
    try std.testing.expectEqual(@as(i66, -0x1ffffffffffffffff), try fromSlice(
        i66,
        gpa,
        "-0x1ffffffffffffffff",
        null,
        .{},
    ));
    try std.testing.expectEqual(@as(u65, 0x1ffffffffffffffff), try fromSlice(
        u65,
        gpa,
        "0o3777777777777777777777",
        null,
        .{},
    ));
    try std.testing.expectEqual(@as(i66, 0x1ffffffffffffffff), try fromSlice(
        i66,
        gpa,
        "0o3777777777777777777777",
        null,
        .{},
    ));
    try std.testing.expectEqual(@as(i66, -0x1ffffffffffffffff), try fromSlice(
        i66,
        gpa,
        "-0o3777777777777777777777",
        null,
        .{},
    ));
    try std.testing.expectEqual(@as(u65, 0x1ffffffffffffffff), try fromSlice(
        u65,
        gpa,
        "0b11111111111111111111111111111111111111111111111111111111111111111",
        null,
        .{},
    ));
    try std.testing.expectEqual(@as(i66, 0x1ffffffffffffffff), try fromSlice(
        i66,
        gpa,
        "0b11111111111111111111111111111111111111111111111111111111111111111",
        null,
        .{},
    ));
    try std.testing.expectEqual(@as(i66, -0x1ffffffffffffffff), try fromSlice(
        i66,
        gpa,
        "-0b11111111111111111111111111111111111111111111111111111111111111111",
        null,
        .{},
    ));

    // Number with invalid character in the middle
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(u8, gpa, "32a32", &diag, .{}));
        try std.testing.expectFmt(
            "1:3: error: invalid digit 'a' for decimal base\n",
            "{f}",
            .{diag},
        );
    }

    // Failing to parse as int
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(u8, gpa, "true", &diag, .{}));
        try std.testing.expectFmt("1:1: error: expected type 'u8'\n", "{f}", .{diag});
    }

    // Failing because an int is out of range
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(u8, gpa, "256", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: type 'u8' cannot represent value\n",
            "{f}",
            .{diag},
        );
    }

    // Failing because a negative int is out of range
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(i8, gpa, "-129", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: type 'i8' cannot represent value\n",
            "{f}",
            .{diag},
        );
    }

    // Failing because an unsigned int is negative
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(u8, gpa, "-1", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: type 'u8' cannot represent value\n",
            "{f}",
            .{diag},
        );
    }

    // Failing because a float is non-whole
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(u8, gpa, "1.5", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: type 'u8' cannot represent value\n",
            "{f}",
            .{diag},
        );
    }

    // Failing because a float is negative
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(u8, gpa, "-1.0", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: type 'u8' cannot represent value\n",
            "{f}",
            .{diag},
        );
    }

    // Negative integer zero
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(i8, gpa, "-0", &diag, .{}));
        try std.testing.expectFmt(
            \\1:2: error: integer literal '-0' is ambiguous
            \\1:2: note: use '0' for an integer zero
            \\1:2: note: use '-0.0' for a floating-point signed zero
            \\
        , "{f}", .{diag});
    }

    // Negative integer zero casted to float
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(f32, gpa, "-0", &diag, .{}));
        try std.testing.expectFmt(
            \\1:2: error: integer literal '-0' is ambiguous
            \\1:2: note: use '0' for an integer zero
            \\1:2: note: use '-0.0' for a floating-point signed zero
            \\
        , "{f}", .{diag});
    }

    // Negative float 0 is allowed
    try std.testing.expect(
        std.math.isNegativeZero(try fromSlice(f32, gpa, "-0.0", null, .{})),
    );
    try std.testing.expect(std.math.isPositiveZero(try fromSlice(f32, gpa, "0.0", null, .{})));

    // Double negation is not allowed
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(i8, gpa, "--2", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: expected number or 'inf' after '-'\n",
            "{f}",
            .{diag},
        );
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(f32, gpa, "--2.0", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:1: error: expected number or 'inf' after '-'\n",
            "{f}",
            .{diag},
        );
    }

    // Invalid int literal
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(u8, gpa, "0xg", &diag, .{}));
        try std.testing.expectFmt("1:3: error: invalid digit 'g' for hex base\n", "{f}", .{diag});
    }

    // Notes on invalid int literal
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(u8, gpa, "0123", &diag, .{}));
        try std.testing.expectFmt(
            \\1:1: error: number '0123' has leading zero
            \\1:1: note: use '0o' prefix for octal literals
            \\
        , "{f}", .{diag});
    }
}

test "std.zon negative char" {
    const gpa = std.testing.allocator;

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(f32, gpa, "-'a'", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: expected number or 'inf' after '-'\n",
            "{f}",
            .{diag},
        );
    }
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(i16, gpa, "-'a'", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: expected number or 'inf' after '-'\n",
            "{f}",
            .{diag},
        );
    }
}

test "std.zon parse float" {
    if (builtin.cpu.arch == .x86) return error.SkipZigTest;

    const gpa = std.testing.allocator;

    // Test decimals
    try std.testing.expectEqual(@as(f16, 0.5), try fromSlice(f16, gpa, "0.5", null, .{}));
    try std.testing.expectEqual(
        @as(f32, 123.456),
        try fromSlice(f32, gpa, "123.456", null, .{}),
    );
    try std.testing.expectEqual(
        @as(f64, -123.456),
        try fromSlice(f64, gpa, "-123.456", null, .{}),
    );
    try std.testing.expectEqual(@as(f128, 42.5), try fromSlice(f128, gpa, "42.5", null, .{}));

    // Test whole numbers with and without decimals
    try std.testing.expectEqual(@as(f16, 5.0), try fromSlice(f16, gpa, "5.0", null, .{}));
    try std.testing.expectEqual(@as(f16, 5.0), try fromSlice(f16, gpa, "5", null, .{}));
    try std.testing.expectEqual(@as(f32, -102), try fromSlice(f32, gpa, "-102.0", null, .{}));
    try std.testing.expectEqual(@as(f32, -102), try fromSlice(f32, gpa, "-102", null, .{}));

    // Test characters and negated characters
    try std.testing.expectEqual(@as(f32, 'a'), try fromSlice(f32, gpa, "'a'", null, .{}));
    try std.testing.expectEqual(@as(f32, 'z'), try fromSlice(f32, gpa, "'z'", null, .{}));

    // Test big integers
    try std.testing.expectEqual(
        @as(f32, 36893488147419103231.0),
        try fromSlice(f32, gpa, "36893488147419103231", null, .{}),
    );
    try std.testing.expectEqual(
        @as(f32, -36893488147419103231.0),
        try fromSlice(f32, gpa, "-36893488147419103231", null, .{}),
    );
    try std.testing.expectEqual(@as(f128, 0x1ffffffffffffffff), try fromSlice(
        f128,
        gpa,
        "0x1ffffffffffffffff",
        null,
        .{},
    ));
    try std.testing.expectEqual(@as(f32, @floatFromInt(0x1ffffffffffffffff)), try fromSlice(
        f32,
        gpa,
        "0x1ffffffffffffffff",
        null,
        .{},
    ));

    // Exponents, underscores
    try std.testing.expectEqual(
        @as(f32, 123.0E+77),
        try fromSlice(f32, gpa, "12_3.0E+77", null, .{}),
    );

    // Hexadecimal
    try std.testing.expectEqual(
        @as(f32, 0x103.70p-5),
        try fromSlice(f32, gpa, "0x103.70p-5", null, .{}),
    );
    try std.testing.expectEqual(
        @as(f32, -0x103.70),
        try fromSlice(f32, gpa, "-0x103.70", null, .{}),
    );
    try std.testing.expectEqual(
        @as(f32, 0x1234_5678.9ABC_CDEFp-10),
        try fromSlice(f32, gpa, "0x1234_5678.9ABC_CDEFp-10", null, .{}),
    );

    // inf, nan
    try std.testing.expect(std.math.isPositiveInf(try fromSlice(f32, gpa, "inf", null, .{})));
    try std.testing.expect(std.math.isNegativeInf(try fromSlice(f32, gpa, "-inf", null, .{})));
    try std.testing.expect(std.math.isNan(try fromSlice(f32, gpa, "nan", null, .{})));

    // Negative nan not allowed
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(f32, gpa, "-nan", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: expected number or 'inf' after '-'\n",
            "{f}",
            .{diag},
        );
    }

    // nan as int not allowed
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(i8, gpa, "nan", &diag, .{}));
        try std.testing.expectFmt("1:1: error: expected type 'i8'\n", "{f}", .{diag});
    }

    // nan as int not allowed
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(i8, gpa, "nan", &diag, .{}));
        try std.testing.expectFmt("1:1: error: expected type 'i8'\n", "{f}", .{diag});
    }

    // inf as int not allowed
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(i8, gpa, "inf", &diag, .{}));
        try std.testing.expectFmt("1:1: error: expected type 'i8'\n", "{f}", .{diag});
    }

    // -inf as int not allowed
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(i8, gpa, "-inf", &diag, .{}));
        try std.testing.expectFmt("1:1: error: expected type 'i8'\n", "{f}", .{diag});
    }

    // Bad identifier as float
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(f32, gpa, "foo", &diag, .{}));
        try std.testing.expectFmt(
            \\1:1: error: invalid expression
            \\1:1: note: ZON allows identifiers 'true', 'false', 'null', 'inf', and 'nan'
            \\1:1: note: precede identifier with '.' for an enum literal
            \\
        , "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(error.ParseZon, fromSlice(f32, gpa, "-foo", &diag, .{}));
        try std.testing.expectFmt(
            "1:1: error: expected number or 'inf' after '-'\n",
            "{f}",
            .{diag},
        );
    }

    // Non float as float
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(f32, gpa, "\"foo\"", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected type 'f32'\n", "{f}", .{diag});
    }
}

test "std.zon free on error" {
    // Test freeing partially allocated structs
    {
        const Struct = struct {
            x: []const u8,
            y: []const u8,
            z: bool,
        };
        try std.testing.expectError(error.ParseZon, fromSliceAlloc(Struct, std.testing.allocator,
            \\.{
            \\    .x = "hello",
            \\    .y = "world",
            \\    .z = "fail",
            \\}
        , null, .{}));
    }

    // Test freeing partially allocated tuples
    {
        const Struct = struct {
            []const u8,
            []const u8,
            bool,
        };
        try std.testing.expectError(error.ParseZon, fromSliceAlloc(Struct, std.testing.allocator,
            \\.{
            \\    "hello",
            \\    "world",
            \\    "fail",
            \\}
        , null, .{}));
    }

    // Test freeing structs with missing fields
    {
        const Struct = struct {
            x: []const u8,
            y: bool,
        };
        try std.testing.expectError(error.ParseZon, fromSliceAlloc(Struct, std.testing.allocator,
            \\.{
            \\    .x = "hello",
            \\}
        , null, .{}));
    }

    // Test freeing partially allocated arrays
    {
        try std.testing.expectError(error.ParseZon, fromSliceAlloc(
            [3][]const u8,
            std.testing.allocator,
            \\.{
            \\    "hello",
            \\    false,
            \\    false,
            \\}
        ,
            null,
            .{},
        ));
    }

    // Test freeing partially allocated slices
    {
        try std.testing.expectError(error.ParseZon, fromSliceAlloc(
            [][]const u8,
            std.testing.allocator,
            \\.{
            \\    "hello",
            \\    "world",
            \\    false,
            \\}
        ,
            null,
            .{},
        ));
    }

    // We can parse types that can't be freed, as long as they contain no allocations, e.g. untagged
    // unions.
    try std.testing.expectEqual(
        @as(f32, 1.5),
        (try fromSlice(union { x: f32 }, std.testing.allocator, ".{ .x = 1.5 }", null, .{})).x,
    );

    // We can also parse types that can't be freed if it's impossible for an error to occur after
    // the allocation, as is the case here.
    {
        const result = try fromSliceAlloc(
            union { x: []const u8 },
            std.testing.allocator,
            ".{ .x = \"foo\" }",
            null,
            .{},
        );
        defer free(std.testing.allocator, result.x);
        try std.testing.expectEqualStrings("foo", result.x);
    }

    // However, if it's possible we could get an error requiring we free the value, but the value
    // cannot be freed (e.g. untagged unions) then we need to turn off `free_on_error` for it to
    // compile.
    {
        const S = struct {
            union { x: []const u8 },
            bool,
        };
        const result = try fromSliceAlloc(
            S,
            std.testing.allocator,
            ".{ .{ .x = \"foo\" }, true }",
            null,
            .{ .free_on_error = false },
        );
        defer free(std.testing.allocator, result[0].x);
        try std.testing.expectEqualStrings("foo", result[0].x);
        try std.testing.expect(result[1]);
    }

    // Again but for structs.
    {
        const S = struct {
            a: union { x: []const u8 },
            b: bool,
        };
        const result = try fromSliceAlloc(
            S,
            std.testing.allocator,
            ".{ .a = .{ .x = \"foo\" }, .b = true }",
            null,
            .{
                .free_on_error = false,
            },
        );
        defer free(std.testing.allocator, result.a.x);
        try std.testing.expectEqualStrings("foo", result.a.x);
        try std.testing.expect(result.b);
    }

    // Again but for arrays.
    {
        const S = [2]union { x: []const u8 };
        const result = try fromSliceAlloc(
            S,
            std.testing.allocator,
            ".{ .{ .x = \"foo\" }, .{ .x = \"bar\" } }",
            null,
            .{
                .free_on_error = false,
            },
        );
        defer free(std.testing.allocator, result[0].x);
        defer free(std.testing.allocator, result[1].x);
        try std.testing.expectEqualStrings("foo", result[0].x);
        try std.testing.expectEqualStrings("bar", result[1].x);
    }

    // Again but for slices.
    {
        const S = []union { x: []const u8 };
        const result = try fromSliceAlloc(
            S,
            std.testing.allocator,
            ".{ .{ .x = \"foo\" }, .{ .x = \"bar\" } }",
            null,
            .{
                .free_on_error = false,
            },
        );
        defer std.testing.allocator.free(result);
        defer free(std.testing.allocator, result[0].x);
        defer free(std.testing.allocator, result[1].x);
        try std.testing.expectEqualStrings("foo", result[0].x);
        try std.testing.expectEqualStrings("bar", result[1].x);
    }
}

test "std.zon vector" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/15330
    if (builtin.zig_backend == .stage2_llvm and builtin.cpu.arch == .s390x) return error.SkipZigTest; // github.com/ziglang/zig/issues/25957

    const gpa = std.testing.allocator;

    // Passing cases
    try std.testing.expectEqual(
        @Vector(0, bool){},
        try fromSlice(@Vector(0, bool), gpa, ".{}", null, .{}),
    );
    try std.testing.expectEqual(
        @Vector(3, bool){ true, false, true },
        try fromSlice(@Vector(3, bool), gpa, ".{true, false, true}", null, .{}),
    );

    try std.testing.expectEqual(
        @Vector(0, f32){},
        try fromSlice(@Vector(0, f32), gpa, ".{}", null, .{}),
    );
    try std.testing.expectEqual(
        @Vector(3, f32){ 1.5, 2.5, 3.5 },
        try fromSlice(@Vector(3, f32), gpa, ".{1.5, 2.5, 3.5}", null, .{}),
    );

    try std.testing.expectEqual(
        @Vector(0, u8){},
        try fromSlice(@Vector(0, u8), gpa, ".{}", null, .{}),
    );
    try std.testing.expectEqual(
        @Vector(3, u8){ 2, 4, 6 },
        try fromSlice(@Vector(3, u8), gpa, ".{2, 4, 6}", null, .{}),
    );

    {
        try std.testing.expectEqual(
            @Vector(0, *const u8){},
            try fromSliceAlloc(@Vector(0, *const u8), gpa, ".{}", null, .{}),
        );
        const pointers = try fromSliceAlloc(@Vector(3, *const u8), gpa, ".{2, 4, 6}", null, .{});
        defer free(gpa, pointers);
        try std.testing.expectEqualDeep(@Vector(3, *const u8){ &2, &4, &6 }, pointers);
    }

    {
        try std.testing.expectEqual(
            @Vector(0, ?*const u8){},
            try fromSliceAlloc(@Vector(0, ?*const u8), gpa, ".{}", null, .{}),
        );
        const pointers = try fromSliceAlloc(@Vector(3, ?*const u8), gpa, ".{2, null, 6}", null, .{});
        defer free(gpa, pointers);
        try std.testing.expectEqualDeep(@Vector(3, ?*const u8){ &2, null, &6 }, pointers);
    }

    // Too few fields
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(@Vector(2, f32), gpa, ".{0.5}", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:2: error: expected 2 array elements; found 1\n",
            "{f}",
            .{diag},
        );
    }

    // Too many fields
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(@Vector(2, f32), gpa, ".{0.5, 1.5, 2.5}", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:13: error: index 2 outside of array of length 2\n",
            "{f}",
            .{diag},
        );
    }

    // Wrong type fields
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(@Vector(3, f32), gpa, ".{0.5, true, 2.5}", &diag, .{}),
        );
        try std.testing.expectFmt(
            "1:8: error: expected type 'f32'\n",
            "{f}",
            .{diag},
        );
    }

    // Wrong type
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSlice(@Vector(3, u8), gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected type '@Vector(3, u8)'\n", "{f}", .{diag});
    }

    // Elements should get freed on error
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(@Vector(3, *u8), gpa, ".{1, true, 3}", &diag, .{}),
        );
        try std.testing.expectFmt("1:6: error: expected type 'u8'\n", "{f}", .{diag});
    }
}

test "std.zon add pointers" {
    const gpa = std.testing.allocator;

    // Primitive with varying levels of pointers
    {
        const result = try fromSliceAlloc(*u32, gpa, "10", null, .{});
        defer free(gpa, result);
        try std.testing.expectEqual(@as(u32, 10), result.*);
    }

    {
        const result = try fromSliceAlloc(**u32, gpa, "10", null, .{});
        defer free(gpa, result);
        try std.testing.expectEqual(@as(u32, 10), result.*.*);
    }

    {
        const result = try fromSliceAlloc(***u32, gpa, "10", null, .{});
        defer free(gpa, result);
        try std.testing.expectEqual(@as(u32, 10), result.*.*.*);
    }

    // Primitive optional with varying levels of pointers
    {
        const some = try fromSliceAlloc(?*u32, gpa, "10", null, .{});
        defer free(gpa, some);
        try std.testing.expectEqual(@as(u32, 10), some.?.*);

        const none = try fromSliceAlloc(?*u32, gpa, "null", null, .{});
        defer free(gpa, none);
        try std.testing.expectEqual(null, none);
    }

    {
        const some = try fromSliceAlloc(*?u32, gpa, "10", null, .{});
        defer free(gpa, some);
        try std.testing.expectEqual(@as(u32, 10), some.*.?);

        const none = try fromSliceAlloc(*?u32, gpa, "null", null, .{});
        defer free(gpa, none);
        try std.testing.expectEqual(null, none.*);
    }

    {
        const some = try fromSliceAlloc(?**u32, gpa, "10", null, .{});
        defer free(gpa, some);
        try std.testing.expectEqual(@as(u32, 10), some.?.*.*);

        const none = try fromSliceAlloc(?**u32, gpa, "null", null, .{});
        defer free(gpa, none);
        try std.testing.expectEqual(null, none);
    }

    {
        const some = try fromSliceAlloc(*?*u32, gpa, "10", null, .{});
        defer free(gpa, some);
        try std.testing.expectEqual(@as(u32, 10), some.*.?.*);

        const none = try fromSliceAlloc(*?*u32, gpa, "null", null, .{});
        defer free(gpa, none);
        try std.testing.expectEqual(null, none.*);
    }

    {
        const some = try fromSliceAlloc(**?u32, gpa, "10", null, .{});
        defer free(gpa, some);
        try std.testing.expectEqual(@as(u32, 10), some.*.*.?);

        const none = try fromSliceAlloc(**?u32, gpa, "null", null, .{});
        defer free(gpa, none);
        try std.testing.expectEqual(null, none.*.*);
    }

    // Pointer to an array
    {
        const result = try fromSliceAlloc(*[3]u8, gpa, ".{ 1, 2, 3 }", null, .{});
        defer free(gpa, result);
        try std.testing.expectEqual([3]u8{ 1, 2, 3 }, result.*);
    }

    // A complicated type with nested internal pointers and string allocations
    {
        const Inner = struct {
            f1: *const ?*const []const u8,
            f2: *const ?*const []const u8,
        };
        const Outer = struct {
            f1: *const ?*const Inner,
            f2: *const ?*const Inner,
        };
        const expected: Outer = .{
            .f1 = &&.{
                .f1 = &null,
                .f2 = &&"foo",
            },
            .f2 = &null,
        };

        const found = try fromSliceAlloc(?*Outer, gpa,
            \\.{
            \\    .f1 = .{
            \\        .f1 = null,
            \\        .f2 = "foo",
            \\    },
            \\    .f2 = null,
            \\}
        , null, .{});
        defer free(gpa, found);

        try std.testing.expectEqualDeep(expected, found.?.*);
    }

    // Test that optional types are flattened correctly in errors
    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const u8, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected type '?u8'\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const f32, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected type '?f32'\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const @Vector(3, u8), gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected type '?@Vector(3, u8)'\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const bool, gpa, "10", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected type '?bool'\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const struct { a: i32 }, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected optional struct\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const struct { i32 }, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected optional tuple\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const union { x: void }, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected optional union\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const [3]u8, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected optional array\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(?[3]u8, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected optional array\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const []u8, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected optional array\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(?[]u8, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected optional array\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const []const u8, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected optional string\n", "{f}", .{diag});
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        try std.testing.expectError(
            error.ParseZon,
            fromSliceAlloc(*const ?*const enum { foo }, gpa, "true", &diag, .{}),
        );
        try std.testing.expectFmt("1:1: error: expected optional enum literal\n", "{f}", .{diag});
    }
}

test "std.zon stop on node" {
    const gpa = std.testing.allocator;

    {
        const Vec2 = struct {
            x: Zoir.Node.Index,
            y: f32,
        };

        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        const result = try fromSlice(Vec2, gpa, ".{ .x = 1.5, .y = 2.5 }", &diag, .{});
        try std.testing.expectEqual(result.y, 2.5);
        try std.testing.expectEqual(Zoir.Node{ .float_literal = 1.5 }, result.x.get(diag.zoir));
    }

    {
        var diag: Diagnostics = .{};
        defer diag.deinit(gpa);
        const result = try fromSlice(Zoir.Node.Index, gpa, "1.23", &diag, .{});
        try std.testing.expectEqual(Zoir.Node{ .float_literal = 1.23 }, result.get(diag.zoir));
    }
}

test "std.zon no alloc" {
    const gpa = std.testing.allocator;

    try std.testing.expectEqual(
        [3]u8{ 1, 2, 3 },
        try fromSlice([3]u8, gpa, ".{ 1, 2, 3 }", null, .{}),
    );

    const Nested = struct { u8, u8, struct { u8, u8 } };

    var ast = try std.zig.Ast.parse(gpa, ".{ 1, 2, .{ 3, 4 } }", .zon);
    defer ast.deinit(gpa);

    var zoir = try ZonGen.generate(gpa, ast, .{ .parse_str_lits = false });
    defer zoir.deinit(gpa);

    try std.testing.expectEqual(
        Nested{ 1, 2, .{ 3, 4 } },
        try fromZoir(Nested, ast, zoir, null, .{}),
    );

    try std.testing.expectEqual(
        Nested{ 1, 2, .{ 3, 4 } },
        try fromZoirNode(Nested, ast, zoir, .root, null, .{}),
    );
}



---
File: /std/zon/Serializer.zig
---

//! Lower level control over serialization, you can create a new instance with `serializer`.
//!
//! Useful when you want control over which fields are serialized, how they're represented,
//! or want to write a ZON object that does not exist in memory.
//!
//! You can serialize values with `value`. To serialize recursive types, the following are provided:
//! * `valueMaxDepth`
//! * `valueArbitraryDepth`
//!
//! You can also serialize values using specific notations:
//! * `int`
//! * `float`
//! * `codePoint`
//! * `tuple`
//! * `tupleMaxDepth`
//! * `tupleArbitraryDepth`
//! * `string`
//! * `multilineString`
//!
//! For manual serialization of containers, see:
//! * `beginStruct`
//! * `beginTuple`

options: Options = .{},
indent_level: u8 = 0,
writer: *Writer,

const Serializer = @This();
const std = @import("std");
const assert = std.debug.assert;
const Writer = std.Io.Writer;

pub const Error = Writer.Error;
pub const DepthError = Error || error{ExceededMaxDepth};

pub const Options = struct {
    /// If false, only syntactically necessary whitespace is emitted.
    whitespace: bool = true,
};

/// Options for manual serialization of container types.
pub const ContainerOptions = struct {
    /// The whitespace style that should be used for this container. Ignored if whitespace is off.
    whitespace_style: union(enum) {
        /// If true, wrap every field. If false do not.
        wrap: bool,
        /// Automatically decide whether to wrap or not based on the number of fields. Following
        /// the standard rule of thumb, containers with more than two fields are wrapped.
        fields: usize,
    } = .{ .wrap = true },

    fn shouldWrap(self: ContainerOptions) bool {
        return switch (self.whitespace_style) {
            .wrap => |wrap| wrap,
            .fields => |fields| fields > 2,
        };
    }
};

/// Options for serialization of an individual value.
///
/// See `SerializeOptions` for more information on these options.
pub const ValueOptions = struct {
    emit_codepoint_literals: EmitCodepointLiterals = .never,
    emit_strings_as_containers: bool = false,
    emit_default_optional_fields: bool = true,
};

/// Determines when to emit Unicode code point literals as opposed to integer literals.
pub const EmitCodepointLiterals = enum {
    /// Never emit Unicode code point literals.
    never,
    /// Emit Unicode code point literals for any `u8` in the printable ASCII range.
    printable_ascii,
    /// Emit Unicode code point literals for any unsigned integer with 21 bits or fewer
    /// whose value is a valid non-surrogate code point.
    always,

    /// If the value should be emitted as a Unicode codepoint, return it as a u21.
    fn emitAsCodepoint(self: @This(), val: anytype) ?u21 {
        // Rule out incompatible integer types
        switch (@typeInfo(@TypeOf(val))) {
            .int => |int_info| if (int_info.signedness == .signed or int_info.bits > 21) {
                return null;
            },
            .comptime_int => {},
            else => comptime unreachable,
        }

        // Return null if the value shouldn't be printed as a Unicode codepoint, or the value casted
        // to a u21 if it should.
        switch (self) {
            .always => {
                const c = std.math.cast(u21, val) orelse return null;
                if (!std.unicode.utf8ValidCodepoint(c)) return null;
                return c;
            },
            .printable_ascii => {
                const c = std.math.cast(u8, val) orelse return null;
                if (!std.ascii.isPrint(c)) return null;
                return c;
            },
            .never => {
                return null;
            },
        }
    }
};

/// Serialize a value, similar to `serialize`.
pub fn value(self: *Serializer, val: anytype, options: ValueOptions) Error!void {
    comptime assert(!typeIsRecursive(@TypeOf(val)));
    return self.valueArbitraryDepth(val, options);
}

/// Serialize a value, similar to `serializeMaxDepth`.
/// Can return `error.ExceededMaxDepth`.
pub fn valueMaxDepth(self: *Serializer, val: anytype, options: ValueOptions, depth: usize) DepthError!void {
    try checkValueDepth(val, depth);
    return self.valueArbitraryDepth(val, options);
}

/// Serialize a value, similar to `serializeArbitraryDepth`.
pub fn valueArbitraryDepth(self: *Serializer, val: anytype, options: ValueOptions) Error!void {
    comptime assert(canSerializeType(@TypeOf(val)));
    switch (@typeInfo(@TypeOf(val))) {
        .int, .comptime_int => if (options.emit_codepoint_literals.emitAsCodepoint(val)) |c| {
            self.codePoint(c) catch |err| switch (err) {
                error.InvalidCodepoint => unreachable, // Already validated
                else => |e| return e,
            };
        } else {
            try self.int(val);
        },
        .float, .comptime_float => try self.float(val),
        .bool, .null => try self.writer.print("{}", .{val}),
        .enum_literal => try self.ident(@tagName(val)),
        .@"enum" => try self.ident(@tagName(val)),
        .pointer => |pointer| {
            // Try to serialize as a string
            const item: ?type = switch (@typeInfo(pointer.child)) {
                .array => |array| array.child,
                else => if (pointer.size == .slice) pointer.child else null,
            };
            if (item == u8 and
                (pointer.sentinel() == null or pointer.sentinel() == 0) and
                !options.emit_strings_as_containers)
            {
                return try self.string(val);
            }

            // Serialize as either a tuple or as the child type
            switch (pointer.size) {
                .slice => try self.tupleImpl(val, options),
                .one => try self.valueArbitraryDepth(val.*, options),
                else => comptime unreachable,
            }
        },
        .array => {
            try valueArbitraryDepthArray(self, @TypeOf(val), &val, options);
        },
        .vector => |vector| {
            const array: [vector.len]vector.child = val;
            try valueArbitraryDepthArray(self, @TypeOf(array), &array, options);
        },
        .@"struct" => |@"struct"| if (@"struct".is_tuple) {
            var container = try self.beginTuple(
                .{ .whitespace_style = .{ .fields = @"struct".fields.len } },
            );
            inline for (val) |field_value| {
                try container.fieldArbitraryDepth(field_value, options);
            }
            try container.end();
        } else {
            // Decide which fields to emit
            const fields, const skipped: [@"struct".fields.len]bool = if (options.emit_default_optional_fields) b: {
                break :b .{ @"struct".fields.len, @splat(false) };
            } else b: {
                var fields = @"struct".fields.len;
                var skipped: [@"struct".fields.len]bool = @splat(false);
                inline for (@"struct".fields, &skipped) |field_info, *skip| {
                    if (field_info.default_value_ptr) |ptr| {
                        const default: *const field_info.type = @ptrCast(@alignCast(ptr));
                        const field_value = @field(val, field_info.name);
                        if (std.meta.eql(field_value, default.*)) {
                            skip.* = true;
                            fields -= 1;
                        }
                    }
                }
                break :b .{ fields, skipped };
            };

            // Emit those fields
            var container = try self.beginStruct(
                .{ .whitespace_style = .{ .fields = fields } },
            );
            inline for (@"struct".fields, skipped) |field_info, skip| {
                if (!skip) {
                    try container.fieldArbitraryDepth(
                        field_info.name,
                        @field(val, field_info.name),
                        options,
                    );
                }
            }
            try container.end();
        },
        .@"union" => |@"union"| {
            comptime assert(@"union".tag_type != null);
            switch (val) {
                inline else => |pl, tag| if (@TypeOf(pl) == void)
                    try self.writer.print(".{s}", .{@tagName(tag)})
                else {
                    var container = try self.beginStruct(.{ .whitespace_style = .{ .fields = 1 } });

                    try container.fieldArbitraryDepth(
                        @tagName(tag),
                        pl,
                        options,
                    );

                    try container.end();
                },
            }
        },
        .optional => if (val) |inner| {
            try self.valueArbitraryDepth(inner, options);
        } else {
            try self.writer.writeAll("null");
        },

        else => comptime unreachable,
    }
}

fn valueArbitraryDepthArray(s: *Serializer, comptime A: type, array: *const A, options: ValueOptions) Error!void {
    var container = try s.beginTuple(
        .{ .whitespace_style = .{ .fields = array.len } },
    );
    for (array) |elem| {
        try container.fieldArbitraryDepth(elem, options);
    }
    try contai
```
