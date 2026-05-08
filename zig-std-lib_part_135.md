```
ner.end();
}

/// Serialize an integer.
pub fn int(self: *Serializer, val: anytype) Error!void {
    try self.writer.printInt(val, 10, .lower, .{});
}

/// Serialize a float.
pub fn float(self: *Serializer, val: anytype) Error!void {
    switch (@typeInfo(@TypeOf(val))) {
        .float => if (std.math.isNan(val)) {
            return self.writer.writeAll("nan");
        } else if (std.math.isPositiveInf(val)) {
            return self.writer.writeAll("inf");
        } else if (std.math.isNegativeInf(val)) {
            return self.writer.writeAll("-inf");
        } else if (std.math.isNegativeZero(val)) {
            return self.writer.writeAll("-0.0");
        } else {
            try self.writer.print("{d}", .{val});
        },
        .comptime_float => if (val == 0) {
            return self.writer.writeAll("0");
        } else {
            try self.writer.print("{d}", .{val});
        },
        else => comptime unreachable,
    }
}

/// Serialize `name` as an identifier prefixed with `.`.
///
/// Escapes the identifier if necessary.
pub fn ident(self: *Serializer, name: []const u8) Error!void {
    try self.writer.print(".{f}", .{std.zig.fmtIdPU(name)});
}

pub const CodePointError = Error || error{InvalidCodepoint};

/// Serialize `val` as a Unicode codepoint.
///
/// Returns `error.InvalidCodepoint` if `val` is not a valid Unicode codepoint.
pub fn codePoint(self: *Serializer, val: u21) CodePointError!void {
    try self.writer.print("'{f}'", .{std.zig.fmtChar(val)});
}

/// Like `value`, but always serializes `val` as a tuple.
///
/// Will fail at comptime if `val` is not a tuple, array, pointer to an array, or slice.
pub fn tuple(self: *Serializer, val: anytype, options: ValueOptions) Error!void {
    comptime assert(!typeIsRecursive(@TypeOf(val)));
    try self.tupleArbitraryDepth(val, options);
}

/// Like `tuple`, but recursive types are allowed.
///
/// Returns `error.ExceededMaxDepth` if `depth` is exceeded.
pub fn tupleMaxDepth(
    self: *Serializer,
    val: anytype,
    options: ValueOptions,
    depth: usize,
) DepthError!void {
    try checkValueDepth(val, depth);
    try self.tupleArbitraryDepth(val, options);
}

/// Like `tuple`, but recursive types are allowed.
///
/// It is the caller's responsibility to ensure that `val` does not contain cycles.
pub fn tupleArbitraryDepth(
    self: *Serializer,
    val: anytype,
    options: ValueOptions,
) Error!void {
    try self.tupleImpl(val, options);
}

fn tupleImpl(self: *Serializer, val: anytype, options: ValueOptions) Error!void {
    comptime assert(canSerializeType(@TypeOf(val)));
    switch (@typeInfo(@TypeOf(val))) {
        .@"struct" => {
            var container = try self.beginTuple(.{ .whitespace_style = .{ .fields = val.len } });
            inline for (val) |item_val| {
                try container.fieldArbitraryDepth(item_val, options);
            }
            try container.end();
        },
        .pointer, .array => {
            var container = try self.beginTuple(.{ .whitespace_style = .{ .fields = val.len } });
            for (val) |item_val| {
                try container.fieldArbitraryDepth(item_val, options);
            }
            try container.end();
        },
        else => comptime unreachable,
    }
}

/// Like `value`, but always serializes `val` as a string.
pub fn string(self: *Serializer, val: []const u8) Error!void {
    try self.writer.print("\"{f}\"", .{std.zig.fmtString(val)});
}

/// Options for formatting multiline strings.
pub const MultilineStringOptions = struct {
    /// If top level is true, whitespace before and after the multiline string is elided.
    /// If it is true, a newline is printed, then the value, followed by a newline, and if
    /// whitespace is true any necessary indentation follows.
    top_level: bool = false,
};

pub const MultilineStringError = Error || error{InnerCarriageReturn};

/// Like `value`, but always serializes to a multiline string literal.
///
/// Returns `error.InnerCarriageReturn` if `val` contains a CR not followed by a newline,
/// since multiline strings cannot represent CR without a following newline.
pub fn multilineString(
    self: *Serializer,
    val: []const u8,
    options: MultilineStringOptions,
) MultilineStringError!void {
    // Make sure the string does not contain any carriage returns not followed by a newline
    var i: usize = 0;
    while (i < val.len) : (i += 1) {
        if (val[i] == '\r') {
            if (i + 1 < val.len) {
                if (val[i + 1] == '\n') {
                    i += 1;
                    continue;
                }
            }
            return error.InnerCarriageReturn;
        }
    }

    if (!options.top_level) {
        try self.newline();
        try self.indent();
    }

    try self.writer.writeAll("\\\\");
    for (val) |c| {
        if (c != '\r') {
            try self.writer.writeByte(c); // We write newlines here even if whitespace off
            if (c == '\n') {
                try self.indent();
                try self.writer.writeAll("\\\\");
            }
        }
    }

    if (!options.top_level) {
        try self.writer.writeByte('\n'); // Even if whitespace off
        try self.indent();
    }
}

/// Create a `Struct` for writing ZON structs field by field.
pub fn beginStruct(self: *Serializer, options: ContainerOptions) Error!Struct {
    return Struct.begin(self, options);
}

/// Creates a `Tuple` for writing ZON tuples field by field.
pub fn beginTuple(self: *Serializer, options: ContainerOptions) Error!Tuple {
    return Tuple.begin(self, options);
}

fn indent(self: *Serializer) Error!void {
    if (self.options.whitespace) {
        try self.writer.splatByteAll(' ', 4 * self.indent_level);
    }
}

fn newline(self: *Serializer) Error!void {
    if (self.options.whitespace) {
        try self.writer.writeByte('\n');
    }
}

fn newlineOrSpace(self: *Serializer, len: usize) Error!void {
    if (self.containerShouldWrap(len)) {
        try self.newline();
    } else {
        try self.space();
    }
}

fn space(self: *Serializer) Error!void {
    if (self.options.whitespace) {
        try self.writer.writeByte(' ');
    }
}

/// Writes ZON tuples field by field.
pub const Tuple = struct {
    container: Container,

    fn begin(parent: *Serializer, options: ContainerOptions) Error!Tuple {
        return .{
            .container = try Container.begin(parent, .anon, options),
        };
    }

    /// Finishes serializing the tuple.
    ///
    /// Prints a trailing comma as configured when appropriate, and the closing bracket.
    pub fn end(self: *Tuple) Error!void {
        try self.container.end();
        self.* = undefined;
    }

    /// Serialize a field. Equivalent to calling `fieldPrefix` followed by `value`.
    pub fn field(
        self: *Tuple,
        val: anytype,
        options: ValueOptions,
    ) Error!void {
        try self.container.field(null, val, options);
    }

    /// Serialize a field. Equivalent to calling `fieldPrefix` followed by `valueMaxDepth`.
    /// Returns `error.ExceededMaxDepth` if `depth` is exceeded.
    pub fn fieldMaxDepth(
        self: *Tuple,
        val: anytype,
        options: ValueOptions,
        depth: usize,
    ) DepthError!void {
        try self.container.fieldMaxDepth(null, val, options, depth);
    }

    /// Serialize a field. Equivalent to calling `fieldPrefix` followed by
    /// `valueArbitraryDepth`.
    pub fn fieldArbitraryDepth(
        self: *Tuple,
        val: anytype,
        options: ValueOptions,
    ) Error!void {
        try self.container.fieldArbitraryDepth(null, val, options);
    }

    /// Starts a field with a struct as a value. Returns the struct.
    pub fn beginStructField(
        self: *Tuple,
        options: ContainerOptions,
    ) Error!Struct {
        try self.fieldPrefix();
        return self.container.serializer.beginStruct(options);
    }

    /// Starts a field with a tuple as a value. Returns the tuple.
    pub fn beginTupleField(
        self: *Tuple,
        options: ContainerOptions,
    ) Error!Tuple {
        try self.fieldPrefix();
        return self.container.serializer.beginTuple(options);
    }

    /// Print a field prefix. This prints any necessary commas, and whitespace as
    /// configured. Useful if you want to serialize the field value yourself.
    pub fn fieldPrefix(self: *Tuple) Error!void {
        try self.container.fieldPrefix(null);
    }
};

/// Writes ZON structs field by field.
pub const Struct = struct {
    container: Container,

    fn begin(parent: *Serializer, options: ContainerOptions) Error!Struct {
        return .{
            .container = try Container.begin(parent, .named, options),
        };
    }

    /// Finishes serializing the struct.
    ///
    /// Prints a trailing comma as configured when appropriate, and the closing bracket.
    pub fn end(self: *Struct) Error!void {
        try self.container.end();
        self.* = undefined;
    }

    /// Serialize a field. Equivalent to calling `fieldPrefix` followed by `value`.
    pub fn field(
        self: *Struct,
        name: []const u8,
        val: anytype,
        options: ValueOptions,
    ) Error!void {
        try self.container.field(name, val, options);
    }

    /// Serialize a field. Equivalent to calling `fieldPrefix` followed by `valueMaxDepth`.
    /// Returns `error.ExceededMaxDepth` if `depth` is exceeded.
    pub fn fieldMaxDepth(
        self: *Struct,
        name: []const u8,
        val: anytype,
        options: ValueOptions,
        depth: usize,
    ) DepthError!void {
        try self.container.fieldMaxDepth(name, val, options, depth);
    }

    /// Serialize a field. Equivalent to calling `fieldPrefix` followed by
    /// `valueArbitraryDepth`.
    pub fn fieldArbitraryDepth(
        self: *Struct,
        name: []const u8,
        val: anytype,
        options: ValueOptions,
    ) Error!void {
        try self.container.fieldArbitraryDepth(name, val, options);
    }

    /// Starts a field with a struct as a value. Returns the struct.
    pub fn beginStructField(
        self: *Struct,
        name: []const u8,
        options: ContainerOptions,
    ) Error!Struct {
        try self.fieldPrefix(name);
        return self.container.serializer.beginStruct(options);
    }

    /// Starts a field with a tuple as a value. Returns the tuple.
    pub fn beginTupleField(
        self: *Struct,
        name: []const u8,
        options: ContainerOptions,
    ) Error!Tuple {
        try self.fieldPrefix(name);
        return self.container.serializer.beginTuple(options);
    }

    /// Print a field prefix. This prints any necessary commas, the field name (escaped if
    /// necessary) and whitespace as configured. Useful if you want to serialize the field
    /// value yourself.
    pub fn fieldPrefix(self: *Struct, name: []const u8) Error!void {
        try self.container.fieldPrefix(name);
    }
};

const Container = struct {
    const FieldStyle = enum { named, anon };

    serializer: *Serializer,
    field_style: FieldStyle,
    options: ContainerOptions,
    empty: bool,

    fn begin(
        sz: *Serializer,
        field_style: FieldStyle,
        options: ContainerOptions,
    ) Error!Container {
        if (options.shouldWrap()) sz.indent_level +|= 1;
        try sz.writer.writeAll(".{");
        return .{
            .serializer = sz,
            .field_style = field_style,
            .options = options,
            .empty = true,
        };
    }

    fn end(self: *Container) Error!void {
        if (self.options.shouldWrap()) self.serializer.indent_level -|= 1;
        if (!self.empty) {
            if (self.options.shouldWrap()) {
                if (self.serializer.options.whitespace) {
                    try self.serializer.writer.writeByte(',');
                }
                try self.serializer.newline();
                try self.serializer.indent();
            } else if (!self.shouldElideSpaces()) {
                try self.serializer.space();
            }
        }
        try self.serializer.writer.writeByte('}');
        self.* = undefined;
    }

    fn fieldPrefix(self: *Container, name: ?[]const u8) Error!void {
        if (!self.empty) {
            try self.serializer.writer.writeByte(',');
        }
        self.empty = false;
        if (self.options.shouldWrap()) {
            try self.serializer.newline();
        } else if (!self.shouldElideSpaces()) {
            try self.serializer.space();
        }
        if (self.options.shouldWrap()) try self.serializer.indent();
        if (name) |n| {
            try self.serializer.ident(n);
            try self.serializer.space();
            try self.serializer.writer.writeByte('=');
            try self.serializer.space();
        }
    }

    fn field(
        self: *Container,
        name: ?[]const u8,
        val: anytype,
        options: ValueOptions,
    ) Error!void {
        comptime assert(!typeIsRecursive(@TypeOf(val)));
        try self.fieldArbitraryDepth(name, val, options);
    }

    /// Returns `error.ExceededMaxDepth` if `depth` is exceeded.
    fn fieldMaxDepth(
        self: *Container,
        name: ?[]const u8,
        val: anytype,
        options: ValueOptions,
        depth: usize,
    ) DepthError!void {
        try checkValueDepth(val, depth);
        try self.fieldArbitraryDepth(name, val, options);
    }

    fn fieldArbitraryDepth(
        self: *Container,
        name: ?[]const u8,
        val: anytype,
        options: ValueOptions,
    ) Error!void {
        try self.fieldPrefix(name);
        try self.serializer.valueArbitraryDepth(val, options);
    }

    fn shouldElideSpaces(self: *const Container) bool {
        return switch (self.options.whitespace_style) {
            .fields => |fields| self.field_style != .named and fields == 1,
            else => false,
        };
    }
};

test Serializer {
    var discarding: Writer.Discarding = .init(&.{});
    var s: Serializer = .{ .writer = &discarding.writer };
    var vec2 = try s.beginStruct(.{});
    try vec2.field("x", 1.5, .{});
    try vec2.fieldPrefix("prefix");
    try s.value(2.5, .{});
    try vec2.end();
}

inline fn typeIsRecursive(comptime T: type) bool {
    return comptime typeIsRecursiveInner(T, &.{});
}

fn typeIsRecursiveInner(comptime T: type, comptime prev_visited: []const type) bool {
    for (prev_visited) |V| {
        if (V == T) return true;
    }
    const visited = prev_visited ++ .{T};

    return switch (@typeInfo(T)) {
        .pointer => |pointer| typeIsRecursiveInner(pointer.child, visited),
        .optional => |optional| typeIsRecursiveInner(optional.child, visited),
        .array => |array| typeIsRecursiveInner(array.child, visited),
        .vector => |vector| typeIsRecursiveInner(vector.child, visited),
        .@"struct" => |@"struct"| for (@"struct".fields) |field| {
            if (typeIsRecursiveInner(field.type, visited)) break true;
        } else false,
        .@"union" => |@"union"| inline for (@"union".fields) |field| {
            if (typeIsRecursiveInner(field.type, visited)) break true;
        } else false,
        else => false,
    };
}

test typeIsRecursive {
    try std.testing.expect(!typeIsRecursive(bool));
    try std.testing.expect(!typeIsRecursive(struct { x: i32, y: i32 }));
    try std.testing.expect(!typeIsRecursive(struct { i32, i32 }));
    try std.testing.expect(typeIsRecursive(struct { x: i32, y: i32, z: *@This() }));
    try std.testing.expect(typeIsRecursive(struct {
        a: struct {
            const A = @This();
            b: struct {
                c: *struct {
                    a: ?A,
                },
            },
        },
    }));
    try std.testing.expect(typeIsRecursive(struct {
        a: [3]*@This(),
    }));
    try std.testing.expect(typeIsRecursive(struct {
        a: union { a: i32, b: *@This() },
    }));
}

fn checkValueDepth(val: anytype, depth: usize) error{ExceededMaxDepth}!void {
    if (depth == 0) return error.ExceededMaxDepth;
    const child_depth = depth - 1;

    switch (@typeInfo(@TypeOf(val))) {
        .pointer => |pointer| switch (pointer.size) {
            .one => try checkValueDepth(val.*, child_depth),
            .slice => for (val) |item| {
                try checkValueDepth(item, child_depth);
            },
            .c, .many => {},
        },
        .array => for (val) |item| {
            try checkValueDepth(item, child_depth);
        },
        .@"struct" => |@"struct"| inline for (@"struct".fields) |field_info| {
            try checkValueDepth(@field(val, field_info.name), child_depth);
        },
        .@"union" => |@"union"| if (@"union".tag_type == null) {
            return;
        } else switch (val) {
            inline else => |payload| {
                return checkValueDepth(payload, child_depth);
            },
        },
        .optional => if (val) |inner| try checkValueDepth(inner, child_depth),
        else => {},
    }
}

fn expectValueDepthEquals(expected: usize, v: anytype) !void {
    try checkValueDepth(v, expected);
    try std.testing.expectError(error.ExceededMaxDepth, checkValueDepth(v, expected - 1));
}

test checkValueDepth {
    try expectValueDepthEquals(1, 10);
    try expectValueDepthEquals(2, .{ .x = 1, .y = 2 });
    try expectValueDepthEquals(2, .{ 1, 2 });
    try expectValueDepthEquals(3, .{ 1, .{ 2, 3 } });
    try expectValueDepthEquals(3, .{ .{ 1, 2 }, 3 });
    try expectValueDepthEquals(3, .{ .x = 0, .y = 1, .z = .{ .x = 3 } });
    try expectValueDepthEquals(3, .{ .x = 0, .y = .{ .x = 1 }, .z = 2 });
    try expectValueDepthEquals(3, .{ .x = .{ .x = 0 }, .y = 1, .z = 2 });
    try expectValueDepthEquals(2, @as(?u32, 1));
    try expectValueDepthEquals(1, @as(?u32, null));
    try expectValueDepthEquals(1, null);
    try expectValueDepthEquals(3, &@as(?u32, 1));

    // The pointer drops the implicit comptime-ness, so we need to specify 'comptime' here
    try comptime expectValueDepthEquals(2, &1);

    const Union = union(enum) {
        x: u32,
        y: struct { x: u32 },
    };
    try expectValueDepthEquals(2, Union{ .x = 1 });
    try expectValueDepthEquals(3, Union{ .y = .{ .x = 1 } });

    const Recurse = struct { r: ?*const @This() };
    try expectValueDepthEquals(2, Recurse{ .r = null });
    try expectValueDepthEquals(5, Recurse{ .r = &Recurse{ .r = null } });
    try expectValueDepthEquals(8, Recurse{ .r = &Recurse{ .r = &Recurse{ .r = null } } });

    try expectValueDepthEquals(2, @as([]const u8, &.{ 1, 2, 3 }));
    try expectValueDepthEquals(3, @as([]const []const u8, &.{&.{ 1, 2, 3 }}));
}

inline fn canSerializeType(T: type) bool {
    comptime return canSerializeTypeInner(T, &.{}, false);
}

fn canSerializeTypeInner(
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
        .comptime_float,
        .comptime_int,
        .null,
        .enum_literal,
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
        => false,

        .@"enum" => |@"enum"| @"enum".is_exhaustive,

        .pointer => |pointer| switch (pointer.size) {
            .one => canSerializeTypeInner(pointer.child, visited, parent_is_optional),
            .slice => canSerializeTypeInner(pointer.child, visited, false),
            .many, .c => false,
        },

        .optional => |optional| if (parent_is_optional)
            false
        else
            canSerializeTypeInner(optional.child, visited, true),

        .array => |array| canSerializeTypeInner(array.child, visited, false),
        .vector => |vector| canSerializeTypeInner(vector.child, visited, false),

        .@"struct" => |@"struct"| {
            for (visited) |V| if (T == V) return true;
            const new_visited = visited ++ .{T};
            for (@"struct".fields) |field| {
                if (!canSerializeTypeInner(field.type, new_visited, false)) return false;
            }
            return true;
        },
        .@"union" => |@"union"| {
            for (visited) |V| if (T == V) return true;
            const new_visited = visited ++ .{T};
            if (@"union".tag_type == null) return false;
            for (@"union".fields) |field| {
                if (field.type != void and !canSerializeTypeInner(field.type, new_visited, false)) {
                    return false;
                }
            }
            return true;
        },
    };
}

test canSerializeType {
    try std.testing.expect(!comptime canSerializeType(void));
    try std.testing.expect(!comptime canSerializeType(struct { f: [*]u8 }));
    try std.testing.expect(!comptime canSerializeType(struct { error{foo} }));
    try std.testing.expect(!comptime canSerializeType(union(enum) { a: void, f: [*c]u8 }));
    try std.testing.expect(!comptime canSerializeType(@Vector(0, [*c]u8)));
    try std.testing.expect(!comptime canSerializeType(*?[*c]u8));
    try std.testing.expect(!comptime canSerializeType(enum(u8) { _ }));
    try std.testing.expect(!comptime canSerializeType(union { foo: void }));
    try std.testing.expect(comptime canSerializeType(union(enum) { foo: void }));
    try std.testing.expect(comptime canSerializeType(comptime_float));
    try std.testing.expect(comptime canSerializeType(comptime_int));
    try std.testing.expect(!comptime canSerializeType(struct { comptime foo: ??u8 = null }));
    try std.testing.expect(comptime canSerializeType(@TypeOf(.foo)));
    try std.testing.expect(comptime canSerializeType(?u8));
    try std.testing.expect(comptime canSerializeType(*?*u8));
    try std.testing.expect(comptime canSerializeType(?struct {
        foo: ?struct {
            ?union(enum) {
                a: ?@Vector(0, ?*u8),
            },
            ?struct {
                f: ?[]?u8,
            },
        },
    }));
    try std.testing.expect(!comptime canSerializeType(??u8));
    try std.testing.expect(!comptime canSerializeType(?*?u8));
    try std.testing.expect(!comptime canSerializeType(*?*?*u8));
    try std.testing.expect(comptime canSerializeType(struct { x: comptime_int = 2 }));
    try std.testing.expect(comptime canSerializeType(struct { x: comptime_float = 2 }));
    try std.testing.expect(comptime canSerializeType(struct { comptime_int }));
    try std.testing.expect(comptime canSerializeType(struct { comptime x: @TypeOf(.foo) = .foo }));
    const Recursive = struct { foo: ?*@This() };
    try std.testing.expect(comptime canSerializeType(Recursive));

    // Make sure we validate nested optional before we early out due to already having seen
    // a type recursion!
    try std.testing.expect(!comptime canSerializeType(struct {
        add_to_visited: ?u8,
        retrieve_from_visited: ??u8,
    }));
}



---
File: /std/zon/stringify.zig
---

//! ZON can be serialized with `serialize`.
//!
//! The following functions are provided for serializing recursive types:
//! * `serializeMaxDepth`
//! * `serializeArbitraryDepth`
//!
//! For additional control over serialization, see `Serializer`.
//!
//! The following types and any types that contain them may not be serialized:
//! * `type`
//! * `void`, except as a union payload
//! * `noreturn`
//! * Error sets/error unions
//! * Untagged unions
//! * Non-exhaustive enums
//! * Many-pointers or C-pointers
//! * Opaque types, including `anyopaque`
//! * Async frame types, including `anyframe` and `anyframe->T`
//! * Functions
//!
//! All other types are valid. Unsupported types will fail to serialize at compile time. Pointers
//! are followed.

const std = @import("std");
const assert = std.debug.assert;
const Writer = std.Io.Writer;
const Serializer = std.zon.Serializer;

pub const SerializeOptions = struct {
    /// If false, whitespace is omitted. Otherwise whitespace is emitted in standard Zig style.
    whitespace: bool = true,
    /// Determines when to emit Unicode code point literals as opposed to integer literals.
    emit_codepoint_literals: Serializer.EmitCodepointLiterals = .never,
    /// If true, slices of `u8`s, and pointers to arrays of `u8` are serialized as containers.
    /// Otherwise they are serialized as string literals.
    emit_strings_as_containers: bool = false,
    /// If false, struct fields are not written if they are equal to their default value. Comparison
    /// is done by `std.meta.eql`.
    emit_default_optional_fields: bool = true,
};

/// Serialize the given value as ZON.
///
/// It is asserted at comptime that `@TypeOf(val)` is not a recursive type.
pub fn serialize(val: anytype, options: SerializeOptions, writer: *Writer) Writer.Error!void {
    var s: Serializer = .{
        .writer = writer,
        .options = .{ .whitespace = options.whitespace },
    };
    try s.value(val, .{
        .emit_codepoint_literals = options.emit_codepoint_literals,
        .emit_strings_as_containers = options.emit_strings_as_containers,
        .emit_default_optional_fields = options.emit_default_optional_fields,
    });
}

/// Like `serialize`, but recursive types are allowed.
///
/// Returns `error.ExceededMaxDepth` if `depth` is exceeded. Every nested value adds one to a
/// value's depth.
pub fn serializeMaxDepth(
    val: anytype,
    options: SerializeOptions,
    writer: *Writer,
    depth: usize,
) Serializer.DepthError!void {
    var s: Serializer = .{
        .writer = writer,
        .options = .{ .whitespace = options.whitespace },
    };
    try s.valueMaxDepth(val, .{
        .emit_codepoint_literals = options.emit_codepoint_literals,
        .emit_strings_as_containers = options.emit_strings_as_containers,
        .emit_default_optional_fields = options.emit_default_optional_fields,
    }, depth);
}

/// Like `serialize`, but recursive types are allowed.
///
/// It is the caller's responsibility to ensure that `val` does not contain cycles.
pub fn serializeArbitraryDepth(
    val: anytype,
    options: SerializeOptions,
    writer: *Writer,
) Serializer.Error!void {
    var s: Serializer = .{
        .writer = writer,
        .options = .{ .whitespace = options.whitespace },
    };
    try s.valueArbitraryDepth(val, .{
        .emit_codepoint_literals = options.emit_codepoint_literals,
        .emit_strings_as_containers = options.emit_strings_as_containers,
        .emit_default_optional_fields = options.emit_default_optional_fields,
    });
}

fn isNestedOptional(T: type) bool {
    comptime switch (@typeInfo(T)) {
        .optional => |optional| return isNestedOptionalInner(optional.child),
        else => return false,
    };
}

fn isNestedOptionalInner(T: type) bool {
    switch (@typeInfo(T)) {
        .pointer => |pointer| {
            if (pointer.size == .one) {
                return isNestedOptionalInner(pointer.child);
            } else {
                return false;
            }
        },
        .optional => return true,
        else => return false,
    }
}

fn expectSerializeEqual(
    expected: []const u8,
    value: anytype,
    options: SerializeOptions,
) !void {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    const bw = &aw.writer;
    defer aw.deinit();

    try serialize(value, options, bw);
    try std.testing.expectEqualStrings(expected, aw.written());
}

test "std.zon stringify whitespace, high level API" {
    try expectSerializeEqual(".{}", .{}, .{});
    try expectSerializeEqual(".{}", .{}, .{ .whitespace = false });

    try expectSerializeEqual(".{1}", .{1}, .{});
    try expectSerializeEqual(".{1}", .{1}, .{ .whitespace = false });

    try expectSerializeEqual(".{1}", @as([1]u32, .{1}), .{});
    try expectSerializeEqual(".{1}", @as([1]u32, .{1}), .{ .whitespace = false });

    try expectSerializeEqual(".{1}", @as([]const u32, &.{1}), .{});
    try expectSerializeEqual(".{1}", @as([]const u32, &.{1}), .{ .whitespace = false });

    try expectSerializeEqual(".{ .x = 1 }", .{ .x = 1 }, .{});
    try expectSerializeEqual(".{.x=1}", .{ .x = 1 }, .{ .whitespace = false });

    try expectSerializeEqual(".{ 1, 2 }", .{ 1, 2 }, .{});
    try expectSerializeEqual(".{1,2}", .{ 1, 2 }, .{ .whitespace = false });

    try expectSerializeEqual(".{ 1, 2 }", @as([2]u32, .{ 1, 2 }), .{});
    try expectSerializeEqual(".{1,2}", @as([2]u32, .{ 1, 2 }), .{ .whitespace = false });

    try expectSerializeEqual(".{ 1, 2 }", @as([]const u32, &.{ 1, 2 }), .{});
    try expectSerializeEqual(".{1,2}", @as([]const u32, &.{ 1, 2 }), .{ .whitespace = false });

    try expectSerializeEqual(".{ .x = 1, .y = 2 }", .{ .x = 1, .y = 2 }, .{});
    try expectSerializeEqual(".{.x=1,.y=2}", .{ .x = 1, .y = 2 }, .{ .whitespace = false });

    try expectSerializeEqual(
        \\.{
        \\    1,
        \\    2,
        \\    3,
        \\}
    , .{ 1, 2, 3 }, .{});
    try expectSerializeEqual(".{1,2,3}", .{ 1, 2, 3 }, .{ .whitespace = false });

    try expectSerializeEqual(
        \\.{
        \\    1,
        \\    2,
        \\    3,
        \\}
    , @as([3]u32, .{ 1, 2, 3 }), .{});
    try expectSerializeEqual(".{1,2,3}", @as([3]u32, .{ 1, 2, 3 }), .{ .whitespace = false });

    try expectSerializeEqual(
        \\.{
        \\    1,
        \\    2,
        \\    3,
        \\}
    , @as([]const u32, &.{ 1, 2, 3 }), .{});
    try expectSerializeEqual(
        ".{1,2,3}",
        @as([]const u32, &.{ 1, 2, 3 }),
        .{ .whitespace = false },
    );

    try expectSerializeEqual(
        \\.{
        \\    .x = 1,
        \\    .y = 2,
        \\    .z = 3,
        \\}
    , .{ .x = 1, .y = 2, .z = 3 }, .{});
    try expectSerializeEqual(
        ".{.x=1,.y=2,.z=3}",
        .{ .x = 1, .y = 2, .z = 3 },
        .{ .whitespace = false },
    );

    const Union = union(enum) { a: bool, b: i32, c: u8 };

    try expectSerializeEqual(".{ .b = 1 }", Union{ .b = 1 }, .{});
    try expectSerializeEqual(".{.b=1}", Union{ .b = 1 }, .{ .whitespace = false });

    // Nested indentation where outer object doesn't wrap
    try expectSerializeEqual(
        \\.{ .inner = .{
        \\    1,
        \\    2,
        \\    3,
        \\} }
    , .{ .inner = .{ 1, 2, 3 } }, .{});

    const UnionWithVoid = union(enum) { a, b: void, c: u8 };

    try expectSerializeEqual(
        \\.a
    , UnionWithVoid.a, .{});
}

test "std.zon stringify whitespace, low level API" {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    var s: Serializer = .{ .writer = &aw.writer };
    defer aw.deinit();

    for ([2]bool{ true, false }) |whitespace| {
        s.options = .{ .whitespace = whitespace };

        // Empty containers
        {
            var container = try s.beginStruct(.{});
            try container.end();
            try std.testing.expectEqualStrings(".{}", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{});
            try container.end();
            try std.testing.expectEqualStrings(".{}", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginStruct(.{ .whitespace_style = .{ .wrap = false } });
            try container.end();
            try std.testing.expectEqualStrings(".{}", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{ .whitespace_style = .{ .wrap = false } });
            try container.end();
            try std.testing.expectEqualStrings(".{}", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginStruct(.{ .whitespace_style = .{ .fields = 0 } });
            try container.end();
            try std.testing.expectEqualStrings(".{}", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{ .whitespace_style = .{ .fields = 0 } });
            try container.end();
            try std.testing.expectEqualStrings(".{}", aw.written());
            aw.clearRetainingCapacity();
        }

        // Size 1
        {
            var container = try s.beginStruct(.{});
            try container.field("a", 1, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(
                    \\.{
                    \\    .a = 1,
                    \\}
                , aw.written());
            } else {
                try std.testing.expectEqualStrings(".{.a=1}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{});
            try container.field(1, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(
                    \\.{
                    \\    1,
                    \\}
                , aw.written());
            } else {
                try std.testing.expectEqualStrings(".{1}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginStruct(.{ .whitespace_style = .{ .wrap = false } });
            try container.field("a", 1, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(".{ .a = 1 }", aw.written());
            } else {
                try std.testing.expectEqualStrings(".{.a=1}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            // We get extra spaces here, since we didn't know up front that there would only be one
            // field.
            var container = try s.beginTuple(.{ .whitespace_style = .{ .wrap = false } });
            try container.field(1, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(".{ 1 }", aw.written());
            } else {
                try std.testing.expectEqualStrings(".{1}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginStruct(.{ .whitespace_style = .{ .fields = 1 } });
            try container.field("a", 1, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(".{ .a = 1 }", aw.written());
            } else {
                try std.testing.expectEqualStrings(".{.a=1}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{ .whitespace_style = .{ .fields = 1 } });
            try container.field(1, .{});
            try container.end();
            try std.testing.expectEqualStrings(".{1}", aw.written());
            aw.clearRetainingCapacity();
        }

        // Size 2
        {
            var container = try s.beginStruct(.{});
            try container.field("a", 1, .{});
            try container.field("b", 2, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(
                    \\.{
                    \\    .a = 1,
                    \\    .b = 2,
                    \\}
                , aw.written());
            } else {
                try std.testing.expectEqualStrings(".{.a=1,.b=2}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{});
            try container.field(1, .{});
            try container.field(2, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(
                    \\.{
                    \\    1,
                    \\    2,
                    \\}
                , aw.written());
            } else {
                try std.testing.expectEqualStrings(".{1,2}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginStruct(.{ .whitespace_style = .{ .wrap = false } });
            try container.field("a", 1, .{});
            try container.field("b", 2, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(".{ .a = 1, .b = 2 }", aw.written());
            } else {
                try std.testing.expectEqualStrings(".{.a=1,.b=2}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{ .whitespace_style = .{ .wrap = false } });
            try container.field(1, .{});
            try container.field(2, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(".{ 1, 2 }", aw.written());
            } else {
                try std.testing.expectEqualStrings(".{1,2}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginStruct(.{ .whitespace_style = .{ .fields = 2 } });
            try container.field("a", 1, .{});
            try container.field("b", 2, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(".{ .a = 1, .b = 2 }", aw.written());
            } else {
                try std.testing.expectEqualStrings(".{.a=1,.b=2}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{ .whitespace_style = .{ .fields = 2 } });
            try container.field(1, .{});
            try container.field(2, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(".{ 1, 2 }", aw.written());
            } else {
                try std.testing.expectEqualStrings(".{1,2}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        // Size 3
        {
            var container = try s.beginStruct(.{});
            try container.field("a", 1, .{});
            try container.field("b", 2, .{});
            try container.field("c", 3, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(
                    \\.{
                    \\    .a = 1,
                    \\    .b = 2,
                    \\    .c = 3,
                    \\}
                , aw.written());
            } else {
                try std.testing.expectEqualStrings(".{.a=1,.b=2,.c=3}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{});
            try container.field(1, .{});
            try container.field(2, .{});
            try container.field(3, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(
                    \\.{
                    \\    1,
                    \\    2,
                    \\    3,
                    \\}
                , aw.written());
            } else {
                try std.testing.expectEqualStrings(".{1,2,3}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginStruct(.{ .whitespace_style = .{ .wrap = false } });
            try container.field("a", 1, .{});
            try container.field("b", 2, .{});
            try container.field("c", 3, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(".{ .a = 1, .b = 2, .c = 3 }", aw.written());
            } else {
                try std.testing.expectEqualStrings(".{.a=1,.b=2,.c=3}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{ .whitespace_style = .{ .wrap = false } });
            try container.field(1, .{});
            try container.field(2, .{});
            try container.field(3, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(".{ 1, 2, 3 }", aw.written());
            } else {
                try std.testing.expectEqualStrings(".{1,2,3}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginStruct(.{ .whitespace_style = .{ .fields = 3 } });
            try container.field("a", 1, .{});
            try container.field("b", 2, .{});
            try container.field("c", 3, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(
                    \\.{
                    \\    .a = 1,
                    \\    .b = 2,
                    \\    .c = 3,
                    \\}
                , aw.written());
            } else {
                try std.testing.expectEqualStrings(".{.a=1,.b=2,.c=3}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            var container = try s.beginTuple(.{ .whitespace_style = .{ .fields = 3 } });
            try container.field(1, .{});
            try container.field(2, .{});
            try container.field(3, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(
                    \\.{
                    \\    1,
                    \\    2,
                    \\    3,
                    \\}
                , aw.written());
            } else {
                try std.testing.expectEqualStrings(".{1,2,3}", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        // Nested objects where the outer container doesn't wrap but the inner containers do
        {
            var container = try s.beginStruct(.{ .whitespace_style = .{ .wrap = false } });
            try container.field("first", .{ 1, 2, 3 }, .{});
            try container.field("second", .{ 4, 5, 6 }, .{});
            try container.end();
            if (whitespace) {
                try std.testing.expectEqualStrings(
                    \\.{ .first = .{
                    \\    1,
                    \\    2,
                    \\    3,
                    \\}, .second = .{
                    \\    4,
                    \\    5,
                    \\    6,
                    \\} }
                , aw.written());
            } else {
                try std.testing.expectEqualStrings(
                    ".{.first=.{1,2,3},.second=.{4,5,6}}",
                    aw.written(),
                );
            }
            aw.clearRetainingCapacity();
        }
    }
}

test "std.zon stringify utf8 codepoints" {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    var s: Serializer = .{ .writer = &aw.writer };
    defer aw.deinit();

    // Printable ASCII
    try s.int('a');
    try std.testing.expectEqualStrings("97", aw.written());
    aw.clearRetainingCapacity();

    try s.codePoint('a');
    try std.testing.expectEqualStrings("'a'", aw.written());
    aw.clearRetainingCapacity();

    try s.value('a', .{ .emit_codepoint_literals = .always });
    try std.testing.expectEqualStrings("'a'", aw.written());
    aw.clearRetainingCapacity();

    try s.value('a', .{ .emit_codepoint_literals = .printable_ascii });
    try std.testing.expectEqualStrings("'a'", aw.written());
    aw.clearRetainingCapacity();

    try s.value('a', .{ .emit_codepoint_literals = .never });
    try std.testing.expectEqualStrings("97", aw.written());
    aw.clearRetainingCapacity();

    // Short escaped codepoint
    try s.int('\n');
    try std.testing.expectEqualStrings("10", aw.written());
    aw.clearRetainingCapacity();

    try s.codePoint('\n');
    try std.testing.expectEqualStrings("'\\n'", aw.written());
    aw.clearRetainingCapacity();

    try s.value('\n', .{ .emit_codepoint_literals = .always });
    try std.testing.expectEqualStrings("'\\n'", aw.written());
    aw.clearRetainingCapacity();

    try s.value('\n', .{ .emit_codepoint_literals = .printable_ascii });
    try std.testing.expectEqualStrings("10", aw.written());
    aw.clearRetainingCapacity();

    try s.value('\n', .{ .emit_codepoint_literals = .never });
    try std.testing.expectEqualStrings("10", aw.written());
    aw.clearRetainingCapacity();

    // Large codepoint
    try s.int('⚡');
    try std.testing.expectEqualStrings("9889", aw.written());
    aw.clearRetainingCapacity();

    try s.codePoint('⚡');
    try std.testing.expectEqualStrings("'\\u{26a1}'", aw.written());
    aw.clearRetainingCapacity();

    try s.value('⚡', .{ .emit_codepoint_literals = .always });
    try std.testing.expectEqualStrings("'\\u{26a1}'", aw.written());
    aw.clearRetainingCapacity();

    try s.value('⚡', .{ .emit_codepoint_literals = .printable_ascii });
    try std.testing.expectEqualStrings("9889", aw.written());
    aw.clearRetainingCapacity();

    try s.value('⚡', .{ .emit_codepoint_literals = .never });
    try std.testing.expectEqualStrings("9889", aw.written());
    aw.clearRetainingCapacity();

    // Invalid codepoint
    try s.codePoint(0x110000 + 1);
    try std.testing.expectEqualStrings("'\\u{110001}'", aw.written());
    aw.clearRetainingCapacity();

    try s.int(0x110000 + 1);
    try std.testing.expectEqualStrings("1114113", aw.written());
    aw.clearRetainingCapacity();

    try s.value(0x110000 + 1, .{ .emit_codepoint_literals = .always });
    try std.testing.expectEqualStrings("1114113", aw.written());
    aw.clearRetainingCapacity();

    try s.value(0x110000 + 1, .{ .emit_codepoint_literals = .printable_ascii });
    try std.testing.expectEqualStrings("1114113", aw.written());
    aw.clearRetainingCapacity();

    try s.value(0x110000 + 1, .{ .emit_codepoint_literals = .never });
    try std.testing.expectEqualStrings("1114113", aw.written());
    aw.clearRetainingCapacity();

    // Valid codepoint, not a codepoint type
    try s.value(@as(u22, 'a'), .{ .emit_codepoint_literals = .always });
    try std.testing.expectEqualStrings("97", aw.written());
    aw.clearRetainingCapacity();

    try s.value(@as(u22, 'a'), .{ .emit_codepoint_literals = .printable_ascii });
    try std.testing.expectEqualStrings("97", aw.written());
    aw.clearRetainingCapacity();

    try s.value(@as(i32, 'a'), .{ .emit_codepoint_literals = .never });
    try std.testing.expectEqualStrings("97", aw.written());
    aw.clearRetainingCapacity();

    // Make sure value options are passed to children
    try s.value(.{ .c = '⚡' }, .{ .emit_codepoint_literals = .always });
    try std.testing.expectEqualStrings(".{ .c = '\\u{26a1}' }", aw.written());
    aw.clearRetainingCapacity();

    try s.value(.{ .c = '⚡' }, .{ .emit_codepoint_literals = .never });
    try std.testing.expectEqualStrings(".{ .c = 9889 }", aw.written());
    aw.clearRetainingCapacity();
}

test "std.zon stringify strings" {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    var s: Serializer = .{ .writer = &aw.writer };
    defer aw.deinit();

    // Minimal case
    try s.string("abc⚡\n");
    try std.testing.expectEqualStrings("\"abc\\xe2\\x9a\\xa1\\n\"", aw.written());
    aw.clearRetainingCapacity();

    try s.tuple("abc⚡\n", .{});
    try std.testing.expectEqualStrings(
        \\.{
        \\    97,
        \\    98,
        \\    99,
        \\    226,
        \\    154,
        \\    161,
        \\    10,
        \\}
    , aw.written());
    aw.clearRetainingCapacity();

    try s.value("abc⚡\n", .{});
    try std.testing.expectEqualStrings("\"abc\\xe2\\x9a\\xa1\\n\"", aw.written());
    aw.clearRetainingCapacity();

    try s.value("abc⚡\n", .{ .emit_strings_as_containers = true });
    try std.testing.expectEqualStrings(
        \\.{
        \\    97,
        \\    98,
        \\    99,
        \\    226,
        \\    154,
        \\    161,
        \\    10,
        \\}
    , aw.written());
    aw.clearRetainingCapacity();

    // Value options are inherited by children
    try s.value(.{ .str = "abc" }, .{});
    try std.testing.expectEqualStrings(".{ .str = \"abc\" }", aw.written());
    aw.clearRetainingCapacity();

    try s.value(.{ .str = "abc" }, .{ .emit_strings_as_containers = true });
    try std.testing.expectEqualStrings(
        \\.{ .str = .{
        \\    97,
        \\    98,
        \\    99,
        \\} }
    , aw.written());
    aw.clearRetainingCapacity();

    // Arrays (rather than pointers to arrays) of u8s are not considered strings, so that data can
    // round trip correctly.
    try s.value("abc".*, .{});
    try std.testing.expectEqualStrings(
        \\.{
        \\    97,
        \\    98,
        \\    99,
        \\}
    , aw.written());
    aw.clearRetainingCapacity();
}

test "std.zon stringify multiline strings" {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    var s: Serializer = .{ .writer = &aw.writer };
    defer aw.deinit();

    inline for (.{ true, false }) |whitespace| {
        s.options.whitespace = whitespace;

        {
            try s.multilineString("", .{ .top_level = true });
            try std.testing.expectEqualStrings("\\\\", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            try s.multilineString("abc⚡", .{ .top_level = true });
            try std.testing.expectEqualStrings("\\\\abc⚡", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            try s.multilineString("abc⚡\ndef", .{ .top_level = true });
            try std.testing.expectEqualStrings("\\\\abc⚡\n\\\\def", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            try s.multilineString("abc⚡\r\ndef", .{ .top_level = true });
            try std.testing.expectEqualStrings("\\\\abc⚡\n\\\\def", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            try s.multilineString("\nabc⚡", .{ .top_level = true });
            try std.testing.expectEqualStrings("\\\\\n\\\\abc⚡", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            try s.multilineString("\r\nabc⚡", .{ .top_level = true });
            try std.testing.expectEqualStrings("\\\\\n\\\\abc⚡", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            try s.multilineString("abc\ndef", .{});
            if (whitespace) {
                try std.testing.expectEqualStrings("\n\\\\abc\n\\\\def\n", aw.written());
            } else {
                try std.testing.expectEqualStrings("\\\\abc\n\\\\def\n", aw.written());
            }
            aw.clearRetainingCapacity();
        }

        {
            const str: []const u8 = &.{ 'a', '\r', 'c' };
            try s.string(str);
            try std.testing.expectEqualStrings("\"a\\rc\"", aw.written());
            aw.clearRetainingCapacity();
        }

        {
            try std.testing.expectError(
                error.InnerCarriageReturn,
                s.multilineString(@as([]const u8, &.{ 'a', '\r', 'c' }), .{}),
            );
            try std.testing.expectError(
                error.InnerCarriageReturn,
                s.multilineString(@as([]const u8, &.{ 'a', '\r', 'c', '\n' }), .{}),
            );
            try std.testing.expectError(
                error.InnerCarriageReturn,
                s.multilineString(@as([]const u8, &.{ 'a', '\r', 'c', '\r', '\n' }), .{}),
            );
            try std.testing.expectEqualStrings("", aw.written());
            aw.clearRetainingCapacity();
        }
    }
}

test "std.zon stringify skip default fields" {
    const Struct = struct {
        x: i32 = 2,
        y: i8,
        z: u32 = 4,
        inner1: struct { a: u8 = 'z', b: u8 = 'y', c: u8 } = .{
            .a = '1',
            .b = '2',
            .c = '3',
        },
        inner2: struct { u8, u8, u8 } = .{
            'a',
            'b',
            'c',
        },
        inner3: struct { u8, u8, u8 } = .{
            'a',
            'b',
            'c',
        },
    };

    // Not skipping if not set
    try expectSerializeEqual(
        \\.{
        \\    .x = 2,
        \\    .y = 3,
        \\    .z = 4,
        \\    .inner1 = .{
        \\        .a = '1',
        \\        .b = '2',
        \\        .c = '3',
        \\    },
        \\    .inner2 = .{
        \\        'a',
        \\        'b',
        \\        'c',
        \\    },
        \\    .inner3 = .{
        \\        'a',
        \\        'b',
        \\        'd',
        \\    },
        \\}
    ,
        Struct{
            .y = 3,
            .z = 4,
            .inner1 = .{
                .a = '1',
                .b = '2',
                .c = '3',
            },
            .inner3 = .{
                'a',
                'b',
                'd',
            },
        },
        .{ .emit_codepoint_literals = .always },
    );

    // Top level defaults
    try expectSerializeEqual(
        \\.{ .y = 3, .inner3 = .{
        \\    'a',
        \\    'b',
        \\    'd',
        \\} }
    ,
        Struct{
            .y = 3,
            .z = 4,
            .inner1 = .{
                .a = '1',
                .b = '2',
                .c = '3',
            },
            .inner3 = .{
                'a',
                'b',
                'd',
            },
        },
        .{
            .emit_default_optional_fields = false,
            .emit_codepoint_literals = .always,
        },
    );

    // Inner types having defaults, and defaults changing the number of fields affecting the
    // formatting
    try expectSerializeEqual(
        \\.{
        \\    .y = 3,
        \\    .inner1 = .{ .b = '2', .c = '3' },
        \\    .inner3 = .{
        \\        'a',
        \\        'b',
        \\        'd',
        \\    },
        \\}
    ,
        Struct{
            .y = 3,
            .z = 4,
            .inner1 = .{
                .a = 'z',
                .b = '2',
                .c = '3',
            },
            .inner3 = .{
                'a',
                'b',
                'd',
            },
        },
        .{
            .emit_default_optional_fields = false,
            .emit_codepoint_literals = .always,
        },
    );

    const DefaultStrings = struct {
        foo: []const u8 = "abc",
    };
    try expectSerializeEqual(
        \\.{}
    ,
        DefaultStrings{ .foo = "abc" },
        .{ .emit_default_optional_fields = false },
    );
    try expectSerializeEqual(
        \\.{ .foo = "abcd" }
    ,
        DefaultStrings{ .foo = "abcd" },
        .{ .emit_default_optional_fields = false },
    );
}

test "std.zon depth limits" {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    const bw = &aw.writer;
    defer aw.deinit();

    const Recurse = struct { r: []const @This() };

    // Normal operation
    try serializeMaxDepth(.{ 1, .{ 2, 3 } }, .{}, bw, 16);
    try std.testing.expectEqualStrings(".{ 1, .{ 2, 3 } }", aw.written());
    aw.clearRetainingCapacity();

    try serializeArbitraryDepth(.{ 1, .{ 2, 3 } }, .{}, bw);
    try std.testing.expectEqualStrings(".{ 1, .{ 2, 3 } }", aw.written());
    aw.clearRetainingCapacity();

    // Max depth failing on non recursive type
    try std.testing.expectError(
        error.ExceededMaxDepth,
        serializeMaxDepth(.{ 1, .{ 2, .{ 3, 4 } } }, .{}, bw, 3),
    );
    try std.testing.expectEqualStrings("", aw.written());
    aw.clearRetainingCapacity();

    // Max depth passing on recursive type
    {
        const maybe_recurse = Recurse{ .r = &.{} };
        try serializeMaxDepth(maybe_recurse, .{}, bw, 2);
        try std.testing.expectEqualStrings(".{ .r = .{} }", aw.written());
        aw.clearRetainingCapacity();
    }

    // Unchecked passing on recursive type
    {
        const maybe_recurse = Recurse{ .r = &.{} };
        try serializeArbitraryDepth(maybe_recurse, .{}, bw);
        try std.testing.expectEqualStrings(".{ .r = .{} }", aw.written());
        aw.clearRetainingCapacity();
    }

    // Max depth failing on recursive type due to depth
    {
        var maybe_recurse = Recurse{ .r = &.{} };
        maybe_recurse.r = &.{.{ .r = &.{} }};
        try std.testing.expectError(
            error.ExceededMaxDepth,
            serializeMaxDepth(maybe_recurse, .{}, bw, 2),
        );
        try std.testing.expectEqualStrings("", aw.written());
        aw.clearRetainingCapacity();
    }

    // Same but for a slice
    {
        var temp: [1]Recurse = .{.{ .r = &.{} }};
        const maybe_recurse: []const Recurse = &temp;

        try std.testing.expectError(
            error.ExceededMaxDepth,
            serializeMaxDepth(maybe_recurse, .{}, bw, 2),
        );
        try std.testing.expectEqualStrings("", aw.written());
        aw.clearRetainingCapacity();

        var s: Serializer = .{ .writer = bw };

        try std.testing.expectError(
            error.ExceededMaxDepth,
            s.tupleMaxDepth(maybe_recurse, .{}, 2),
        );
        try std.testing.expectEqualStrings("", aw.written());
        aw.clearRetainingCapacity();

        try s.tupleArbitraryDepth(maybe_recurse, .{});
        try std.testing.expectEqualStrings(".{.{ .r = .{} }}", aw.written());
        aw.clearRetainingCapacity();
    }

    // A slice succeeding
    {
        var temp: [1]Recurse = .{.{ .r = &.{} }};
        const maybe_recurse: []const Recurse = &temp;

        try serializeMaxDepth(maybe_recurse, .{}, bw, 3);
        try std.testing.expectEqualStrings(".{.{ .r = .{} }}", aw.written());
        aw.clearRetainingCapacity();

        var s: Serializer = .{ .writer = bw };

        try s.tupleMaxDepth(maybe_recurse, .{}, 3);
        try std.testing.expectEqualStrings(".{.{ .r = .{} }}", aw.written());
        aw.clearRetainingCapacity();

        try s.tupleArbitraryDepth(maybe_recurse, .{});
        try std.testing.expectEqualStrings(".{.{ .r = .{} }}", aw.written());
        aw.clearRetainingCapacity();
    }

    // Max depth failing on recursive type due to recursion
    {
        var temp: [1]Recurse = .{.{ .r = &.{} }};
        temp[0].r = &temp;
        const maybe_recurse: []const Recurse = &temp;

        try std.testing.expectError(
            error.ExceededMaxDepth,
            serializeMaxDepth(maybe_recurse, .{}, bw, 128),
        );
        try std.testing.expectEqualStrings("", aw.written());
        aw.clearRetainingCapacity();

        var s: Serializer = .{ .writer = bw };
        try std.testing.expectError(
            error.ExceededMaxDepth,
            s.tupleMaxDepth(maybe_recurse, .{}, 128),
        );
        try std.testing.expectEqualStrings("", aw.written());
        aw.clearRetainingCapacity();
    }

    // Max depth on other parts of the lower level API
    {
        var s: Serializer = .{ .writer = bw };

        const maybe_recurse: []const Recurse = &.{};

        try std.testing.expectError(error.ExceededMaxDepth, s.valueMaxDepth(1, .{}, 0));
        try s.valueMaxDepth(2, .{}, 1);
        try s.value(3, .{});
        try s.valueArbitraryDepth(maybe_recurse, .{});

        var wip_struct = try s.beginStruct(.{});
        try std.testing.expectError(error.ExceededMaxDepth, wip_struct.fieldMaxDepth("a", 1, .{}, 0));
        try wip_struct.fieldMaxDepth("b", 4, .{}, 1);
        try wip_struct.field("c", 5, .{});
        try wip_struct.fieldArbitraryDepth("d", maybe_recurse, .{});
        try wip_struct.end();

        var t = try s.beginTuple(.{});
        try std.testing.expectError(error.ExceededMaxDepth, t.fieldMaxDepth(1, .{}, 0));
        try t.fieldMaxDepth(6, .{}, 1);
        try t.field(7, .{});
        try t.fieldArbitraryDepth(maybe_recurse, .{});
        try t.end();

        var a = try s.beginTuple(.{});
        try std.testing.expectError(error.ExceededMaxDepth, a.fieldMaxDepth(1, .{}, 0));
        try a.fieldMaxDepth(8, .{}, 1);
        try a.field(9, .{});
        try a.fieldArbitraryDepth(maybe_recurse, .{});
        try a.end();

        try std.testing.expectEqualStrings(
            \\23.{}.{
            \\    .b = 4,
            \\    .c = 5,
            \\    .d = .{},
            \\}.{
            \\    6,
            \\    7,
            \\    .{},
            \\}.{
            \\    8,
            \\    9,
            \\    .{},
            \\}
        , aw.written());
    }
}

test "std.zon stringify primitives" {
    // Issue: https://github.com/ziglang/zig/issues/20880
    if (@import("builtin").zig_backend == .stage2_c) return error.SkipZigTest;

    try expectSerializeEqual(
        \\.{
        \\    .a = 1.5,
        \\    .b = 0.3333333333333333333333333333333333,
        \\    .c = 3.1415926535897932384626433832795028,
        \\    .d = 0,
        \\    .e = 0,
        \\    .f = -0.0,
        \\    .g = inf,
        \\    .h = -inf,
        \\    .i = nan,
        \\}
    ,
        .{
            .a = @as(f128, 1.5), // Make sure explicit f128s work
            .b = 1.0 / 3.0,
            .c = std.math.pi,
            .d = 0.0,
            .e = -0.0,
            .f = @as(f128, -0.0),
            .g = std.math.inf(f32),
            .h = -std.math.inf(f32),
            .i = std.math.nan(f32),
        },
        .{},
    );

    try expectSerializeEqual(
        \\.{
        \\    .a = 18446744073709551616,
        \\    .b = -18446744073709551616,
        \\    .c = 680564733841876926926749214863536422912,
        \\    .d = -680564733841876926926749214863536422912,
        \\    .e = 0,
        \\}
    ,
        .{
            .a = 18446744073709551616,
            .b = -18446744073709551616,
            .c = 680564733841876926926749214863536422912,
            .d = -680564733841876926926749214863536422912,
            .e = 0,
        },
        .{},
    );

    try expectSerializeEqual(
        \\.{
        \\    .a = true,
        \\    .b = false,
        \\    .c = .foo,
        \\    .e = null,
        \\}
    ,
        .{
            .a = true,
            .b = false,
            .c = .foo,
            .e = null,
        },
        .{},
    );

    const Struct = struct { x: f32, y: f32 };
    try expectSerializeEqual(
        ".{ .a = .{ .x = 1, .y = 2 }, .b = null }",
        .{
            .a = @as(?Struct, .{ .x = 1, .y = 2 }),
            .b = @as(?Struct, null),
        },
        .{},
    );

    const E = enum(u8) {
        foo,
        bar,
    };
    try expectSerializeEqual(
        ".{ .a = .foo, .b = .foo }",
        .{
            .a = .foo,
            .b = E.foo,
        },
        .{},
    );
}

test "std.zon stringify ident" {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    var s: Serializer = .{ .writer = &aw.writer };
    defer aw.deinit();

    try expectSerializeEqual(".{ .a = 0 }", .{ .a = 0 }, .{});
    try s.ident("a");
    try std.testing.expectEqualStrings(".a", aw.written());
    aw.clearRetainingCapacity();

    try s.ident("foo_1");
    try std.testing.expectEqualStrings(".foo_1", aw.written());
    aw.clearRetainingCapacity();

    try s.ident("_foo_1");
    try std.testing.expectEqualStrings("._foo_1", aw.written());
    aw.clearRetainingCapacity();

    try s.ident("foo bar");
    try std.testing.expectEqualStrings(".@\"foo bar\"", aw.written());
    aw.clearRetainingCapacity();

    try s.ident("1foo");
    try std.testing.expectEqualStrings(".@\"1foo\"", aw.written());
    aw.clearRetainingCapacity();

    try s.ident("var");
    try std.testing.expectEqualStrings(".@\"var\"", aw.written());
    aw.clearRetainingCapacity();

    try s.ident("true");
    try std.testing.expectEqualStrings(".true", aw.written());
    aw.clearRetainingCapacity();

    try s.ident("_");
    try std.testing.expectEqualStrings("._", aw.written());
    aw.clearRetainingCapacity();

    const Enum = enum {
        @"foo bar",
    };
    try expectSerializeEqual(".{ .@\"var\" = .@\"foo bar\", .@\"1\" = .@\"foo bar\" }", .{
        .@"var" = .@"foo bar",
        .@"1" = Enum.@"foo bar",
    }, .{});
}

test "std.zon stringify as tuple" {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    var s: Serializer = .{ .writer = &aw.writer };
    defer aw.deinit();

    // Tuples
    try s.tuple(.{ 1, 2 }, .{});
    try std.testing.expectEqualStrings(".{ 1, 2 }", aw.written());
    aw.clearRetainingCapacity();

    // Slice
    try s.tuple(@as([]const u8, &.{ 1, 2 }), .{});
    try std.testing.expectEqualStrings(".{ 1, 2 }", aw.written());
    aw.clearRetainingCapacity();

    // Array
    try s.tuple([2]u8{ 1, 2 }, .{});
    try std.testing.expectEqualStrings(".{ 1, 2 }", aw.written());
    aw.clearRetainingCapacity();
}

test "std.zon stringify as float" {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    var s: Serializer = .{ .writer = &aw.writer };
    defer aw.deinit();

    // Comptime float
    try s.float(2.5);
    try std.testing.expectEqualStrings("2.5", aw.written());
    aw.clearRetainingCapacity();

    // Sized float
    try s.float(@as(f32, 2.5));
    try std.testing.expectEqualStrings("2.5", aw.written());
    aw.clearRetainingCapacity();
}

test "std.zon stringify vector" {
    try expectSerializeEqual(
        \\.{
        \\    .{},
        \\    .{
        \\        true,
        \\        false,
        \\        true,
        \\    },
        \\    .{},
        \\    .{
        \\        1.5,
        \\        2.5,
        \\        3.5,
        \\    },
        \\    .{},
        \\    .{
        \\        2,
        \\        4,
        \\        6,
        \\    },
        \\    .{ 1, 2 },
        \\    .{
        \\        3,
        \\        4,
        \\        null,
        \\    },
        \\}
    ,
        .{
            @Vector(0, bool){},
            @Vector(3, bool){ true, false, true },
            @Vector(0, f32){},
            @Vector(3, f32){ 1.5, 2.5, 3.5 },
            @Vector(0, u8){},
            @Vector(3, u8){ 2, 4, 6 },
            @Vector(2, *const u8){ &1, &2 },
            @Vector(3, ?*const u8){ &3, &4, null },
        },
        .{},
    );
}

test "std.zon pointers" {
    // Primitive with varying levels of pointers
    try expectSerializeEqual("10", &@as(u32, 10), .{});
    try expectSerializeEqual("10", &&@as(u32, 10), .{});
    try expectSerializeEqual("10", &&&@as(u32, 10), .{});

    // Primitive optional with varying levels of pointers
    try expectSerializeEqual("10", @as(?*const u32, &10), .{});
    try expectSerializeEqual("null", @as(?*const u32, null), .{});
    try expectSerializeEqual("10", @as(?*const u32, &10), .{});
    try expectSerializeEqual("null", @as(*const ?u32, &null), .{});

    try expectSerializeEqual("10", @as(?*const *const u32, &&10), .{});
    try expectSerializeEqual("null", @as(?*const *const u32, null), .{});
    try expectSerializeEqual("10", @as(*const ?*const u32, &&10), .{});
    try expectSerializeEqual("null", @as(*const ?*const u32, &null), .{});
    try expectSerializeEqual("10", @as(*const *const ?u32, &&10), .{});
    try expectSerializeEqual("null", @as(*const *const ?u32, &&null), .{});

    try expectSerializeEqual(".{ 1, 2 }", &[2]u32{ 1, 2 }, .{});

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
        const val: ?*const Outer = &.{
            .f1 = &&.{
                .f1 = &null,
                .f2 = &&"foo",
            },
            .f2 = &null,
        };

        try expectSerializeEqual(
            \\.{ .f1 = .{ .f1 = null, .f2 = "foo" }, .f2 = null }
        , val, .{});
    }
}

test "std.zon tuple/struct field" {
    var aw: Writer.Allocating = .init(std.testing.allocator);
    var s: Serializer = .{ .writer = &aw.writer };
    defer aw.deinit();

    // Test on structs
    {
        var root = try s.beginStruct(.{});
        {
            var tuple = try root.beginTupleField("foo", .{});
            try tuple.field(0, .{});
            try tuple.field(1, .{});
            try tuple.end();
        }
        {
            var strct = try root.beginStructField("bar", .{});
            try strct.field("a", 0, .{});
            try strct.field("b", 1, .{});
            try strct.end();
        }
        try root.end();

        try std.testing.expectEqualStrings(
            \\.{
            \\    .foo = .{
            \\        0,
            \\        1,
            \\    },
            \\    .bar = .{
            \\        .a = 0,
            \\        .b = 1,
            \\    },
            \\}
        , aw.written());
        aw.clearRetainingCapacity();
    }

    // Test on tuples
    {
        var root = try s.beginTuple(.{});
        {
            var tuple = try root.beginTupleField(.{});
            try tuple.field(0, .{});
            try tuple.field(1, .{});
            try tuple.end();
        }
        {
            var strct = try root.beginStructField(.{});
            try strct.field("a", 0, .{});
            try strct.field("b", 1, .{});
            try strct.end();
        }
        try root.end();

        try std.testing.expectEqualStrings(
            \\.{
            \\    .{
            \\        0,
            \\        1,
            \\    },
            \\    .{
            \\        .a = 0,
            \\        .b = 1,
            \\    },
            \\}
        , aw.written());
        aw.clearRetainingCapacity();
    }
}



---
File: /std/array_hash_map.zig
---

const std = @import("std.zig");
const debug = std.debug;
const assert = debug.assert;
const testing = std.testing;
const math = std.math;
const mem = std.mem;
const autoHash = std.hash.autoHash;
const Wyhash = std.hash.Wyhash;
const Allocator = mem.Allocator;
const hash_map = @This();

/// An `ArrayHashMap` with default hash and equal functions.
///
/// See `AutoContext` for a description of the hash and equal implementations.
pub fn Auto(comptime K: type, comptime V: type) type {
    return ArrayHashMap(K, V, AutoContext(K), !autoEqlIsCheap(K));
}

/// An `ArrayHashMap` with strings as keys.
pub fn String(comptime V: type) type {
    return ArrayHashMap([]const u8, V, StringContext, true);
}

pub const StringContext = struct {
    pub fn hash(self: @This(), s: []const u8) u32 {
        _ = self;
        return hashString(s);
    }
    pub fn eql(self: @This(), a: []const u8, b: []const u8, b_index: usize) bool {
        _ = self;
        _ = b_index;
        return eqlString(a, b);
    }
};

pub fn eqlString(a: []const u8, b: []const u8) bool {
    return mem.eql(u8, a, b);
}

pub fn hashString(s: []const u8) u32 {
    return @truncate(std.hash.Wyhash.hash(0, s));
}

/// Deprecated; use `Custom`.
pub const ArrayHashMap = Custom;

/// A hash table of keys and values, each stored sequentially.
///
/// Insertion order is preserved. In general, this data structure supports the same
/// operations as `std.ArrayList`.
///
/// Deletion operations:
/// * `swapRemove` - O(1)
/// * `orderedRemove` - O(N)
///
/// Modifying the hash map while iterating is allowed, however, one must understand
/// the (well defined) behavior when mixing insertions and deletions with iteration.
///
/// This type does not store an `Allocator` field - the `Allocator` must be passed in
/// with each function call that requires it. See `ArrayHashMap` for a type that stores
/// an `Allocator` field for convenience.
///
/// Can be initialized directly using the default field values.
///
/// This type is designed to have low overhead for small numbers of entries. When
/// `store_hash` is `false` and the number of entries in the map is less than 9,
/// the overhead cost of using `ArrayHashMap` rather than `std.ArrayList` is
/// only a single pointer-sized integer.
///
/// Default initialization of this struct is deprecated; use `.empty` instead.
pub fn Custom(
    comptime K: type,
    comptime V: type,
    /// A namespace that provides these two functions:
    /// * `pub fn hash(self, K) u32`
    /// * `pub fn eql(self, K, K, usize) bool`
    ///
    /// The final `usize` in the `eql` function represents the index of the key
    /// that's already inside the map.
    comptime Context: type,
    /// When `false`, this data structure is biased towards cheap `eql`
    /// functions and avoids storing each key's hash in the table. Setting
    /// `store_hash` to `true` incurs more memory cost but limits `eql` to
    /// being called only once per insertion/deletion (provided there are no
    /// hash collisions).
    comptime store_hash: bool,
) type {
    return struct {
        /// It is permitted to access this field directly.
        /// After any modification to the keys, consider calling `reIndex`.
        entries: DataList = .{},

        /// When entries length is less than `linear_scan_max`, this remains `null`.
        /// Once entries length grows big enough, this field is allocated. There is
        /// an IndexHeader followed by an array of Index(I) structs, where I is defined
        /// by how many total indexes there are.
        index_header: ?*IndexHeader = null,

        /// Used to detect memory safety violations.
        pointer_stability: std.debug.SafetyLock = .{},

        /// A map containing no keys or values.
        pub const empty: Self = .{
            .entries = .{},
            .index_header = null,
        };

        /// Modifying the key is allowed only if it does not change the hash.
        /// Modifying the value is allowed.
        /// Entry pointers become invalid whenever this ArrayHashMap is modified,
        /// unless `ensureTotalCapacity`/`ensureUnusedCapacity` was previously used.
        pub const Entry = struct {
            key_ptr: *K,
            value_ptr: *V,
        };

        /// A KV pair which has been copied out of the backing store
        pub const KV = struct {
            key: K,
            value: V,
        };

        /// The Data type used for the MultiArrayList backing this map
        pub const Data = struct {
            hash: Hash,
            key: K,
            value: V,
        };

        /// The MultiArrayList type backing this map
        pub const DataList = std.MultiArrayList(Data);

        /// The stored hash type, either u32 or void.
        pub const Hash = if (store_hash) u32 else void;

        /// getOrPut variants return this structure, with pointers
        /// to the backing store and a flag to indicate whether an
        /// existing entry was found.
        /// Modifying the key is allowed only if it does not change the hash.
        /// Modifying the value is allowed.
        /// Entry pointers become invalid whenever this ArrayHashMap is modified,
        /// unless `ensureTotalCapacity`/`ensureUnusedCapacity` was previously used.
        pub const GetOrPutResult = struct {
            key_ptr: *K,
            value_ptr: *V,
            found_existing: bool,
            index: usize,
        };

        /// Some functions require a context only if hashes are not stored.
        /// To keep the api simple, this type is only used internally.
        const ByIndexContext = if (store_hash) void else Context;

        const Self = @This();

        const linear_scan_max = @as(comptime_int, @max(1, @as(comptime_int, @min(
            std.atomic.cache_line / @as(comptime_int, @max(1, @sizeOf(Hash))),
            std.atomic.cache_line / @as(comptime_int, @max(1, @sizeOf(K))),
        ))));

        const RemovalType = enum {
            swap,
            ordered,
        };

        const Oom = Allocator.Error;

        pub fn init(gpa: Allocator, key_list: []const K, value_list: []const V) Oom!Self {
            var self: Self = .{};
            errdefer self.deinit(gpa);
            try self.reinit(gpa, key_list, value_list);
            return self;
        }

        /// An empty `value_list` may be passed, in which case the values array becomes `undefined`.
        pub fn reinit(self: *Self, gpa: Allocator, key_list: []const K, value_list: []const V) Oom!void {
            try self.entries.resize(gpa, key_list.len);
            @memcpy(self.keys(), key_list);
            if (value_list.len == 0) {
                @memset(self.values(), undefined);
            } else {
                assert(key_list.len == value_list.len);
                @memcpy(self.values(), value_list);
            }
            try self.reIndex(gpa);
        }

        /// Frees the backing allocation and leaves the map in an undefined state.
        /// Note that this does not free keys or values.  You must take care of that
        /// before calling this function, if it is needed.
        pub fn deinit(self: *Self, gpa: Allocator) void {
            self.pointer_stability.assertUnlocked();
            self.entries.deinit(gpa);
            if (self.index_header) |header| {
                header.free(gpa);
            }
            self.* = undefined;
        }

        /// Puts the hash map into a state where any method call that would
        /// cause an existing key or value pointer to become invalidated will
        /// instead trigger an assertion.
        ///
        /// An additional call to `lockPointers` in such state also triggers an
        /// assertion.
        ///
        /// `unlockPointers` returns the hash map to the previous state.
        pub fn lockPointers(self: *Self) void {
            self.pointer_stability.lock();
        }

        /// Undoes a call to `lockPointers`.
        pub fn unlockPointers(self: *Self) void {
            self.pointer_stability.unlock();
        }

        /// Clears the map but retains the backing allocation for future use.
        pub fn clearRetainingCapacity(self: *Self) void {
            self.pointer_stability.lock();
            defer self.pointer_stability.unlock();

            self.entries.len = 0;
            if (self.index_header) |header| {
                switch (header.capacityIndexType()) {
                    .u8 => @memset(header.indexes(u8), Index(u8).empty),
                    .u16 => @memset(header.indexes(u16), Index(u16).empty),
                    .u32 => @memset(header.indexes(u32), Index(u32).empty),
                }
            }
        }

        /// Clears the map and releases the backing allocation
        pub fn clearAndFree(self: *Self, gpa: Allocator) void {
            self.pointer_stability.lock();
            defer self.pointer_stability.unlock();

            self.entries.shrinkAndFree(gpa, 0);
            if (self.index_header) |header| {
                header.free(gpa);
                self.index_header = null;
            }
        }

        /// Returns the number of KV pairs stored in this map.
        pub fn count(self: Self) usize {
            return self.entries.len;
        }

        /// Returns the backing array of keys in this map. Modifying the map may
        /// invalidate this array. Modifying this array in a way that changes
        /// key hashes or key equality puts the map into an unusable state until
        /// `reIndex` is called.
        pub fn keys(self: Self) []K {
            return self.entries.items(.key);
        }
        /// Returns the backing array of values in this map. Modifying the map
        /// may invalidate this array. It is permitted to modify the values in
        /// this array.
        pub fn values(self: Self) []V {
            return self.entries.items(.value);
        }

        /// Returns an iterator over the pairs in this map.
        /// Modifying the map may invalidate this iterator.
        pub fn iterator(self: Self) Iterator {
            const slice = self.entries.slice();
            return .{
                .keys = slice.items(.key).ptr,
                .values = slice.items(.value).ptr,
                .len = @as(u32, @intCast(slice.len)),
            };
        }
        pub const Iterator = struct {
            keys: [*]K,
            values: [*]V,
            len: u32,
            index: u32 = 0,

            pub fn next(it: *Iterator) ?Entry {
                if (it.index >= it.len) return null;
                const result = Entry{
                    .key_ptr = &it.keys[it.index],
                    // workaround for #6974
                    .value_ptr = if (@sizeOf(*V) == 0) undefined else &it.values[it.index],
                };
                it.index += 1;
                return result;
            }

            /// Reset the iterator to the initial index
            pub fn reset(it: *Iterator) void {
                it.index = 0;
            }
        };

        /// If key exists this function cannot fail.
        /// If there is an existing item with `key`, then the result
        /// `Entry` pointer points to it, and found_existing is true.
        /// Otherwise, puts a new item with undefined value, and
        /// the `Entry` pointer points to it. Caller should then initialize
        /// the value (but not the key).
        pub fn getOrPut(self: *Self, gpa: Allocator, key: K) Oom!GetOrPutResult {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getOrPutContext instead.");
            return self.getOrPutContext(gpa, key, undefined);
        }
        pub fn getOrPutContext(self: *Self, gpa: Allocator, key: K, ctx: Context) Oom!GetOrPutResult {
            const gop = try self.getOrPutContextAdapted(gpa, key, ctx, ctx);
            if (!gop.found_existing) {
                gop.key_ptr.* = key;
            }
            return gop;
        }
        pub fn getOrPutAdapted(self: *Self, gpa: Allocator, key: anytype, key_ctx: anytype) Oom!GetOrPutResult {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getOrPutContextAdapted instead.");
            return self.getOrPutContextAdapted(gpa, key, key_ctx, undefined);
        }
        pub fn getOrPutContextAdapted(self: *Self, gpa: Allocator, key: anytype, key_ctx: anytype, ctx: Context) Oom!GetOrPutResult {
            self.ensureTotalCapacityContext(gpa, self.entries.len + 1, ctx) catch |err| {
                // "If key exists this function cannot fail."
                const index = self.getIndexAdapted(key, key_ctx) orelse return err;
                const slice = self.entries.slice();
                return GetOrPutResult{
                    .key_ptr = &slice.items(.key)[index],
                    // workaround for #6974
                    .value_ptr = if (@sizeOf(*V) == 0) undefined else &slice.items(.value)[index],
                    .found_existing = true,
                    .index = index,
                };
            };
            return self.getOrPutAssumeCapacityAdapted(key, key_ctx);
        }

        /// If there is an existing item with `key`, then the result
        /// `Entry` pointer points to it, and found_existing is true.
        /// Otherwise, puts a new item with undefined value, and
        /// the `Entry` pointer points to it. Caller should then initialize
        /// the value (but not the key).
        /// If a new entry needs to be stored, this function asserts there
        /// is enough capacity to store it.
        pub fn getOrPutAssumeCapacity(self: *Self, key: K) GetOrPutResult {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getOrPutAssumeCapacityContext instead.");
            return self.getOrPutAssumeCapacityContext(key, undefined);
        }
        pub fn getOrPutAssumeCapacityContext(self: *Self, key: K, ctx: Context) GetOrPutResult {
            const gop = self.getOrPutAssumeCapacityAdapted(key, ctx);
            if (!gop.found_existing) {
                gop.key_ptr.* = key;
            }
            return gop;
        }
        /// If there is an existing item with `key`, then the result
        /// `Entry` pointers point to it, and found_existing is true.
        /// Otherwise, puts a new item with undefined key and value, and
        /// the `Entry` pointers point to it. Caller must then initialize
        /// both the key and the value.
        /// If a new entry needs to be stored, this function asserts there
        /// is enough capacity to store it.
        pub fn getOrPutAssumeCapacityAdapted(self: *Self, key: anytype, ctx: anytype) GetOrPutResult {
            const header = self.index_header orelse {
                // Linear scan.
                const h = if (store_hash) checkedHash(ctx, key) else {};
                const slice = self.entries.slice();
                const hashes_array = slice.items(.hash);
                const keys_array = slice.items(.key);
                for (keys_array, 0..) |*item_key, i| {
                    if (hashes_array[i] == h and checkedEql(ctx, key, item_key.*, i)) {
                        return GetOrPutResult{
                            .key_ptr = item_key,
                            // workaround for #6974
                            .value_ptr = if (@sizeOf(*V) == 0) undefined else &slice.items(.value)[i],
                            .found_existing = true,
                            .index = i,
                        };
                    }
                }

                const index = self.entries.addOneAssumeCapacity();
                // The slice length changed, so we directly index the pointer.
                if (store_hash) hashes_array.ptr[index] = h;

                return GetOrPutResult{
                    .key_ptr = &keys_array.ptr[index],
                    // workaround for #6974
                    .value_ptr = if (@sizeOf(*V) == 0) undefined else &slice.items(.value).ptr[index],
                    .found_existing = false,
                    .index = index,
                };
            };

            switch (header.capacityIndexType()) {
                .u8 => return self.getOrPutInternal(key, ctx, header, u8),
                .u16 => return self.getOrPutInternal(key, ctx, header, u16),
                .u32 => return self.getOrPutInternal(key, ctx, header, u32),
            }
        }

        pub fn getOrPutValue(self: *Self, gpa: Allocator, key: K, value: V) Oom!GetOrPutResult {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getOrPutValueContext instead.");
            return self.getOrPutValueContext(gpa, key, value, undefined);
        }
        pub fn getOrPutValueContext(self: *Self, gpa: Allocator, key: K, value: V, ctx: Context) Oom!GetOrPutResult {
            const res = try self.getOrPutContextAdapted(gpa, key, ctx, ctx);
            if (!res.found_existing) {
                res.key_ptr.* = key;
                res.value_ptr.* = value;
            }
            return res;
        }

        /// Increases capacity, guaranteeing that insertions up until the
        /// `expected_count` will not cause an allocation, and therefore cannot fail.
        pub fn ensureTotalCapacity(self: *Self, gpa: Allocator, new_capacity: usize) Oom!void {
            if (@sizeOf(ByIndexContext) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call ensureTotalCapacityContext instead.");
            return self.ensureTotalCapacityContext(gpa, new_capacity, undefined);
        }
        pub fn ensureTotalCapacityContext(self: *Self, gpa: Allocator, new_capacity: usize, ctx: Context) Oom!void {
            self.pointer_stability.lock();
            defer self.pointer_stability.unlock();

            try self.entries.ensureTotalCapacity(gpa, new_capacity);
            if (new_capacity <= linear_scan_max) return;
            if (self.index_header) |header| if (new_capacity <= header.capacity()) return;

            const new_bit_index = try IndexHeader.findBitIndex(new_capacity);
            const new_header = try IndexHeader.alloc(gpa, new_bit_index);

            if (self.index_header) |old_header| old_header.free(gpa);
            self.insertAllEntriesIntoNewHeader(if (store_hash) {} else ctx, new_header);
            self.index_header = new_header;
        }

        /// Increases capacity, guaranteeing that insertions up until
        /// `additional_count` **more** items will not cause an allocation, and
        /// therefore cannot fail.
        pub fn ensureUnusedCapacity(
            self: *Self,
            gpa: Allocator,
            additional_capacity: usize,
        ) Oom!void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call ensureTotalCapacityContext instead.");
            return self.ensureUnusedCapacityContext(gpa, additional_capacity, undefined);
        }
        pub fn ensureUnusedCapacityContext(
            self: *Self,
            gpa: Allocator,
            additional_capacity: usize,
            ctx: Context,
        ) Oom!void {
            return self.ensureTotalCapacityContext(gpa, self.count() + additional_capacity, ctx);
        }

        /// Returns the number of total elements which may be present before it is
        /// no longer guaranteed that no allocations will be performed.
        pub fn capacity(self: Self) usize {
            const entry_cap = self.entries.capacity;
            const header = self.index_header orelse return @min(linear_scan_max, entry_cap);
            const indexes_cap = header.capacity();
            return @min(entry_cap, indexes_cap);
        }

        /// Clobbers any existing data. To detect if a put would clobber
        /// existing data, see `getOrPut`.
        pub fn put(self: *Self, gpa: Allocator, key: K, value: V) Oom!void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call putContext instead.");
            return self.putContext(gpa, key, value, undefined);
        }
        pub fn putContext(self: *Self, gpa: Allocator, key: K, value: V, ctx: Context) Oom!void {
            const result = try self.getOrPutContext(gpa, key, ctx);
            result.value_ptr.* = value;
        }

        /// Inserts a key-value pair into the hash map, asserting that no previous
        /// entry with the same key is already present
        pub fn putNoClobber(self: *Self, gpa: Allocator, key: K, value: V) Oom!void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call putNoClobberContext instead.");
            return self.putNoClobberContext(gpa, key, value, undefined);
        }
        pub fn putNoClobberContext(self: *Self, gpa: Allocator, key: K, value: V, ctx: Context) Oom!void {
            const result = try self.getOrPutContext(gpa, key, ctx);
            assert(!result.found_existing);
            result.value_ptr.* = value;
        }

        /// Asserts there is enough capacity to store the new key-value pair.
        /// Clobbers any existing data. To detect if a put would clobber
        /// existing data, see `getOrPutAssumeCapacity`.
        pub fn putAssumeCapacity(self: *Self, key: K, value: V) void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call putAssumeCapacityContext instead.");
            return self.putAssumeCapacityContext(key, value, undefined);
        }
        pub fn putAssumeCapacityContext(self: *Self, key: K, value: V, ctx: Context) void {
            const result = self.getOrPutAssumeCapacityContext(key, ctx);
            result.value_ptr.* = value;
        }

        /// Asserts there is enough capacity to store the new key-value pair.
        /// Asserts that it does not clobber any existing data.
        /// To detect if a put would clobber existing data, see `getOrPutAssumeCapacity`.
        pub fn putAssumeCapacityNoClobber(self: *Self, key: K, value: V) void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call putAssumeCapacityNoClobberContext instead.");
            return self.putAssumeCapacityNoClobberContext(key, value, undefined);
        }
        pub fn putAssumeCapacityNoClobberContext(self: *Self, key: K, value: V, ctx: Context) void {
            const result = self.getOrPutAssumeCapacityContext(key, ctx);
            assert(!result.found_existing);
            result.value_ptr.* = value;
        }

        /// Inserts a new `Entry` into the hash map, returning the previous one, if any.
        pub fn fetchPut(self: *Self, gpa: Allocator, key: K, value: V) Oom!?KV {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call fetchPutContext instead.");
            return self.fetchPutContext(gpa, key, value, undefined);
        }
        pub fn fetchPutContext(self: *Self, gpa: Allocator, key: K, value: V, ctx: Context) Oom!?KV {
            const gop = try self.getOrPutContext(gpa, key, ctx);
            var result: ?KV = null;
            if (gop.found_existing) {
                result = KV{
                    .key = gop.key_ptr.*,
                    .value = gop.value_ptr.*,
                };
            }
            gop.value_ptr.* = value;
            return result;
        }

        /// Inserts a new `Entry` into the hash map, returning the previous one, if any.
        /// If insertion happens, asserts there is enough capacity without allocating.
        pub fn fetchPutAssumeCapacity(self: *Self, key: K, value: V) ?KV {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call fetchPutAssumeCapacityContext instead.");
            return self.fetchPutAssumeCapacityContext(key, value, undefined);
        }
        pub fn fetchPutAssumeCapacityContext(self: *Self, key: K, value: V, ctx: Context) ?KV {
            const gop = self.getOrPutAssumeCapacityContext(key, ctx);
            var result: ?KV = null;
            if (gop.found_existing) {
                result = KV{
                    .key = gop.key_ptr.*,
                    .value = gop.value_ptr.*,
                };
            }
            gop.value_ptr.* = value;
            return result;
        }

        /// Finds pointers to the key and value storage associated with a key.
        pub fn getEntry(self: Self, key: K) ?Entry {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getEntryContext instead.");
            return self.getEntryContext(key, undefined);
        }
        pub fn getEntryContext(self: Self, key: K, ctx: Context) ?Entry {
            return self.getEntryAdapted(key, ctx);
        }
        pub fn getEntryAdapted(self: Self, key: anytype, ctx: anytype) ?Entry {
            const index = self.getIndexAdapted(key, ctx) orelse return null;
            const slice = self.entries.slice();
            return Entry{
                .key_ptr = &slice.items(.key)[index],
                // workaround for #6974
                .value_ptr = if (@sizeOf(*V) == 0) undefined else &slice.items(.value)[index],
            };
        }

        /// Finds the index in the `entries` array where a key is stored
        pub fn getIndex(self: Self, key: K) ?usize {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getIndexContext instead.");
            return self.getIndexContext(key, undefined);
        }
        pub fn getIndexContext(self: Self, key: K, ctx: Context) ?usize {
            return self.getIndexAdapted(key, ctx);
        }
        pub fn getIndexAdapted(self: Self, key: anytype, ctx: anytype) ?usize {
            const header = self.index_header orelse {
                // Linear scan.
                const h = if (store_hash) checkedHash(ctx, key) else {};
                const slice = self.entries.slice();
                const hashes_array = slice.items(.hash);
                const keys_array = slice.items(.key);
                for (keys_array, 0..) |*item_key, i| {
                    if (hashes_array[i] == h and checkedEql(ctx, key, item_key.*, i)) {
                        return i;
                    }
                }
                return null;
            };
            switch (header.capacityIndexType()) {
                .u8 => return self.getIndexWithHeaderGeneric(key, ctx, header, u8),
                .u16 => return self.getIndexWithHeaderGeneric(key, ctx, header, u16),
                .u32 => return self.getIndexWithHeaderGeneric(key, ctx, header, u32),
            }
        }
        fn getIndexWithHeaderGeneric(self: Self, key: anytype, ctx: anytype, header: *IndexHeader, comptime I: type) ?usize {
            const indexes = header.indexes(I);
            const slot = self.getSlotByKey(key, ctx, header, I, indexes) orelse return null;
            return indexes[slot].entry_index;
        }

        /// Find the value associated with a key
        pub fn get(self: Self, key: K) ?V {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getContext instead.");
            return self.getContext(key, undefined);
        }
        pub fn getContext(self: Self, key: K, ctx: Context) ?V {
            return self.getAdapted(key, ctx);
        }
        pub fn getAdapted(self: Self, key: anytype, ctx: anytype) ?V {
            const index = self.getIndexAdapted(key, ctx) orelse return null;
            return self.values()[index];
        }

        /// Find a pointer to the value associated with a key
        pub fn getPtr(self: Self, key: K) ?*V {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getPtrContext instead.");
            return self.getPtrContext(key, undefined);
        }
        pub fn getPtrContext(self: Self, key: K, ctx: Context) ?*V {
            return self.getPtrAdapted(key, ctx);
        }
        pub fn getPtrAdapted(self: Self, key: anytype, ctx: anytype) ?*V {
            const index = self.getIndexAdapted(key, ctx) orelse return null;
            // workaround for #6974
            return if (@sizeOf(*V) == 0) @as(*V, undefined) else &self.values()[index];
        }

        /// Find the actual key associated with an adapted key
        pub fn getKey(self: Self, key: K) ?K {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getKeyContext instead.");
            return self.getKeyContext(key, undefined);
        }
        pub fn getKeyContext(self: Self, key: K, ctx: Context) ?K {
            return self.getKeyAdapted(key, ctx);
        }
        pub fn getKeyAdapted(self: Self, key: anytype, ctx: anytype) ?K {
            const index = self.getIndexAdapted(key, ctx) orelse return null;
            return self.keys()[index];
        }

        /// Find a pointer to the actual key associated with an adapted key
        pub fn getKeyPtr(self: Self, key: K) ?*K {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getKeyPtrContext instead.");
            return self.getKeyPtrContext(key, undefined);
        }
    
```
