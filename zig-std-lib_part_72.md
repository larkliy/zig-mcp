```
 comptime T: type,
    allocator: Allocator,
    source: anytype,
    options: ParseOptions,
) ParseError(@TypeOf(source.*))!T {
    switch (@typeInfo(T)) {
        .bool => {
            return switch (try source.next()) {
                .true => true,
                .false => false,
                else => error.UnexpectedToken,
            };
        },
        .float, .comptime_float => {
            const token = try source.nextAllocMax(allocator, .alloc_if_needed, options.max_value_len.?);
            defer freeAllocated(allocator, token);
            const slice = switch (token) {
                inline .number, .allocated_number, .string, .allocated_string => |slice| slice,
                else => return error.UnexpectedToken,
            };
            return try std.fmt.parseFloat(T, slice);
        },
        .int, .comptime_int => {
            const token = try source.nextAllocMax(allocator, .alloc_if_needed, options.max_value_len.?);
            defer freeAllocated(allocator, token);
            const slice = switch (token) {
                inline .number, .allocated_number, .string, .allocated_string => |slice| slice,
                else => return error.UnexpectedToken,
            };
            return sliceToInt(T, slice);
        },
        .optional => |optionalInfo| {
            switch (try source.peekNextTokenType()) {
                .null => {
                    _ = try source.next();
                    return null;
                },
                else => {
                    return try innerParse(optionalInfo.child, allocator, source, options);
                },
            }
        },
        .@"enum" => {
            if (std.meta.hasFn(T, "jsonParse")) {
                return T.jsonParse(allocator, source, options);
            }

            const token = try source.nextAllocMax(allocator, .alloc_if_needed, options.max_value_len.?);
            defer freeAllocated(allocator, token);
            const slice = switch (token) {
                inline .number, .allocated_number, .string, .allocated_string => |slice| slice,
                else => return error.UnexpectedToken,
            };
            return sliceToEnum(T, slice);
        },
        .@"union" => |unionInfo| {
            if (std.meta.hasFn(T, "jsonParse")) {
                return T.jsonParse(allocator, source, options);
            }

            if (unionInfo.tag_type == null) @compileError("Unable to parse into untagged union '" ++ @typeName(T) ++ "'");

            if (.object_begin != try source.next()) return error.UnexpectedToken;

            var result: ?T = null;
            var name_token: ?Token = try source.nextAllocMax(allocator, .alloc_if_needed, options.max_value_len.?);
            const field_name = switch (name_token.?) {
                inline .string, .allocated_string => |slice| slice,
                else => {
                    return error.UnexpectedToken;
                },
            };

            inline for (unionInfo.fields) |u_field| {
                if (std.mem.eql(u8, u_field.name, field_name)) {
                    // Free the name token now in case we're using an allocator that optimizes freeing the last allocated object.
                    // (Recursing into innerParse() might trigger more allocations.)
                    freeAllocated(allocator, name_token.?);
                    name_token = null;
                    if (u_field.type == void) {
                        // void isn't really a json type, but we can support void payload union tags with {} as a value.
                        if (.object_begin != try source.next()) return error.UnexpectedToken;
                        if (.object_end != try source.next()) return error.UnexpectedToken;
                        result = @unionInit(T, u_field.name, {});
                    } else {
                        // Recurse.
                        result = @unionInit(T, u_field.name, try innerParse(u_field.type, allocator, source, options));
                    }
                    break;
                }
            } else {
                // Didn't match anything.
                return error.UnknownField;
            }

            if (.object_end != try source.next()) return error.UnexpectedToken;

            return result.?;
        },

        .@"struct" => |structInfo| {
            if (structInfo.is_tuple) {
                if (.array_begin != try source.next()) return error.UnexpectedToken;

                var r: T = undefined;
                inline for (0..structInfo.fields.len) |i| {
                    r[i] = try innerParse(structInfo.fields[i].type, allocator, source, options);
                }

                if (.array_end != try source.next()) return error.UnexpectedToken;

                return r;
            }

            if (std.meta.hasFn(T, "jsonParse")) {
                return T.jsonParse(allocator, source, options);
            }

            if (.object_begin != try source.next()) return error.UnexpectedToken;

            var r: T = undefined;
            var fields_seen = [_]bool{false} ** structInfo.fields.len;

            while (true) {
                var name_token: ?Token = try source.nextAllocMax(allocator, .alloc_if_needed, options.max_value_len.?);
                const field_name = switch (name_token.?) {
                    inline .string, .allocated_string => |slice| slice,
                    .object_end => { // No more fields.
                        break;
                    },
                    else => {
                        return error.UnexpectedToken;
                    },
                };

                inline for (structInfo.fields, 0..) |field, i| {
                    if (field.is_comptime) @compileError("comptime fields are not supported: " ++ @typeName(T) ++ "." ++ field.name);
                    if (std.mem.eql(u8, field.name, field_name)) {
                        // Free the name token now in case we're using an allocator that optimizes freeing the last allocated object.
                        // (Recursing into innerParse() might trigger more allocations.)
                        freeAllocated(allocator, name_token.?);
                        name_token = null;
                        if (fields_seen[i]) {
                            switch (options.duplicate_field_behavior) {
                                .use_first => {
                                    // Parse and ignore the redundant value.
                                    // We don't want to skip the value, because we want type checking.
                                    _ = try innerParse(field.type, allocator, source, options);
                                    break;
                                },
                                .@"error" => return error.DuplicateField,
                                .use_last => {},
                            }
                        }
                        @field(r, field.name) = try innerParse(field.type, allocator, source, options);
                        fields_seen[i] = true;
                        break;
                    }
                } else {
                    // Didn't match anything.
                    freeAllocated(allocator, name_token.?);
                    if (options.ignore_unknown_fields) {
                        try source.skipValue();
                    } else {
                        return error.UnknownField;
                    }
                }
            }
            try fillDefaultStructValues(T, &r, &fields_seen);
            return r;
        },

        .array => |arrayInfo| {
            switch (try source.peekNextTokenType()) {
                .array_begin => {
                    // Typical array.
                    return internalParseArray(T, arrayInfo.child, allocator, source, options);
                },
                .string => {
                    if (arrayInfo.child != u8) return error.UnexpectedToken;
                    // Fixed-length string.

                    var r: T = undefined;
                    var i: usize = 0;
                    while (true) {
                        switch (try source.next()) {
                            .string => |slice| {
                                if (i + slice.len != r.len) return error.LengthMismatch;
                                @memcpy(r[i..][0..slice.len], slice);
                                break;
                            },
                            .partial_string => |slice| {
                                if (i + slice.len > r.len) return error.LengthMismatch;
                                @memcpy(r[i..][0..slice.len], slice);
                                i += slice.len;
                            },
                            .partial_string_escaped_1 => |arr| {
                                if (i + arr.len > r.len) return error.LengthMismatch;
                                @memcpy(r[i..][0..arr.len], arr[0..]);
                                i += arr.len;
                            },
                            .partial_string_escaped_2 => |arr| {
                                if (i + arr.len > r.len) return error.LengthMismatch;
                                @memcpy(r[i..][0..arr.len], arr[0..]);
                                i += arr.len;
                            },
                            .partial_string_escaped_3 => |arr| {
                                if (i + arr.len > r.len) return error.LengthMismatch;
                                @memcpy(r[i..][0..arr.len], arr[0..]);
                                i += arr.len;
                            },
                            .partial_string_escaped_4 => |arr| {
                                if (i + arr.len > r.len) return error.LengthMismatch;
                                @memcpy(r[i..][0..arr.len], arr[0..]);
                                i += arr.len;
                            },
                            else => unreachable,
                        }
                    }

                    return r;
                },

                else => return error.UnexpectedToken,
            }
        },

        .vector => |vector_info| {
            switch (try source.peekNextTokenType()) {
                .array_begin => {
                    const A = [vector_info.len]vector_info.child;
                    return try internalParseArray(A, vector_info.child, allocator, source, options);
                },
                else => return error.UnexpectedToken,
            }
        },

        .pointer => |ptrInfo| {
            switch (ptrInfo.size) {
                .one => {
                    const r: *ptrInfo.child = try allocator.create(ptrInfo.child);
                    r.* = try innerParse(ptrInfo.child, allocator, source, options);
                    return r;
                },
                .slice => {
                    switch (try source.peekNextTokenType()) {
                        .array_begin => {
                            _ = try source.next();

                            // Typical array.
                            var arraylist = ArrayList(ptrInfo.child).init(allocator);
                            while (true) {
                                switch (try source.peekNextTokenType()) {
                                    .array_end => {
                                        _ = try source.next();
                                        break;
                                    },
                                    else => {},
                                }

                                try arraylist.ensureUnusedCapacity(1);
                                arraylist.appendAssumeCapacity(try innerParse(ptrInfo.child, allocator, source, options));
                            }

                            if (ptrInfo.sentinel()) |s| {
                                return try arraylist.toOwnedSliceSentinel(s);
                            }

                            return try arraylist.toOwnedSlice();
                        },
                        .string => {
                            if (ptrInfo.child != u8) return error.UnexpectedToken;

                            // Dynamic length string.
                            if (ptrInfo.sentinel()) |s| {
                                // Use our own array list so we can append the sentinel.
                                var value_list = ArrayList(u8).init(allocator);
                                _ = try source.allocNextIntoArrayList(&value_list, .alloc_always);
                                return try value_list.toOwnedSliceSentinel(s);
                            }
                            if (ptrInfo.is_const) {
                                switch (try source.nextAllocMax(allocator, options.allocate.?, options.max_value_len.?)) {
                                    inline .string, .allocated_string => |slice| return slice,
                                    else => unreachable,
                                }
                            } else {
                                // Have to allocate to get a mutable copy.
                                switch (try source.nextAllocMax(allocator, .alloc_always, options.max_value_len.?)) {
                                    .allocated_string => |slice| return slice,
                                    else => unreachable,
                                }
                            }
                        },
                        else => return error.UnexpectedToken,
                    }
                },
                else => @compileError("Unable to parse into type '" ++ @typeName(T) ++ "'"),
            }
        },
        else => @compileError("Unable to parse into type '" ++ @typeName(T) ++ "'"),
    }
    unreachable;
}

fn internalParseArray(
    comptime T: type,
    comptime Child: type,
    allocator: Allocator,
    source: anytype,
    options: ParseOptions,
) !T {
    assert(.array_begin == try source.next());

    var r: T = undefined;
    for (&r) |*elem| {
        elem.* = try innerParse(Child, allocator, source, options);
    }

    if (.array_end != try source.next()) return error.UnexpectedToken;

    return r;
}

/// This is an internal function called recursively
/// during the implementation of `parseFromValueLeaky`.
/// It is exposed primarily to enable custom `jsonParseFromValue()` methods to call back into the `parseFromValue*` system,
/// such as if you're implementing a custom container of type `T`;
/// you can call `innerParseFromValue(T, ...)` for each of the container's items.
pub fn innerParseFromValue(
    comptime T: type,
    allocator: Allocator,
    source: Value,
    options: ParseOptions,
) ParseFromValueError!T {
    switch (@typeInfo(T)) {
        .bool => {
            switch (source) {
                .bool => |b| return b,
                else => return error.UnexpectedToken,
            }
        },
        .float, .comptime_float => {
            switch (source) {
                .float => |f| return @as(T, @floatCast(f)),
                .integer => |i| return @as(T, @floatFromInt(i)),
                .number_string, .string => |s| return std.fmt.parseFloat(T, s),
                else => return error.UnexpectedToken,
            }
        },
        .int, .comptime_int => {
            switch (source) {
                .float => |f| {
                    if (@round(f) != f) return error.InvalidNumber;
                    if (f > @as(@TypeOf(f), @floatFromInt(std.math.maxInt(T)))) return error.Overflow;
                    if (f < @as(@TypeOf(f), @floatFromInt(std.math.minInt(T)))) return error.Overflow;
                    return @intFromFloat(f);
                },
                .integer => |i| {
                    if (i > std.math.maxInt(T)) return error.Overflow;
                    if (i < std.math.minInt(T)) return error.Overflow;
                    return @intCast(i);
                },
                .number_string, .string => |s| {
                    return sliceToInt(T, s);
                },
                else => return error.UnexpectedToken,
            }
        },
        .optional => |optionalInfo| {
            switch (source) {
                .null => return null,
                else => return try innerParseFromValue(optionalInfo.child, allocator, source, options),
            }
        },
        .@"enum" => {
            if (std.meta.hasFn(T, "jsonParseFromValue")) {
                return T.jsonParseFromValue(allocator, source, options);
            }

            switch (source) {
                .float => return error.InvalidEnumTag,
                .integer => |i| return std.enums.fromInt(T, i) orelse return error.InvalidEnumTag,
                .number_string, .string => |s| return sliceToEnum(T, s),
                else => return error.UnexpectedToken,
            }
        },
        .@"union" => |unionInfo| {
            if (std.meta.hasFn(T, "jsonParseFromValue")) {
                return T.jsonParseFromValue(allocator, source, options);
            }

            if (unionInfo.tag_type == null) @compileError("Unable to parse into untagged union '" ++ @typeName(T) ++ "'");

            if (source != .object) return error.UnexpectedToken;
            if (source.object.count() != 1) return error.UnexpectedToken;

            var it = source.object.iterator();
            const kv = it.next().?;
            const field_name = kv.key_ptr.*;

            inline for (unionInfo.fields) |u_field| {
                if (std.mem.eql(u8, u_field.name, field_name)) {
                    if (u_field.type == void) {
                        // void isn't really a json type, but we can support void payload union tags with {} as a value.
                        if (kv.value_ptr.* != .object) return error.UnexpectedToken;
                        if (kv.value_ptr.*.object.count() != 0) return error.UnexpectedToken;
                        return @unionInit(T, u_field.name, {});
                    }
                    // Recurse.
                    return @unionInit(T, u_field.name, try innerParseFromValue(u_field.type, allocator, kv.value_ptr.*, options));
                }
            }
            // Didn't match anything.
            return error.UnknownField;
        },

        .@"struct" => |structInfo| {
            if (structInfo.is_tuple) {
                if (source != .array) return error.UnexpectedToken;
                if (source.array.items.len != structInfo.fields.len) return error.UnexpectedToken;

                var r: T = undefined;
                inline for (0..structInfo.fields.len, source.array.items) |i, item| {
                    r[i] = try innerParseFromValue(structInfo.fields[i].type, allocator, item, options);
                }

                return r;
            }

            if (std.meta.hasFn(T, "jsonParseFromValue")) {
                return T.jsonParseFromValue(allocator, source, options);
            }

            if (source != .object) return error.UnexpectedToken;

            var r: T = undefined;
            var fields_seen = [_]bool{false} ** structInfo.fields.len;

            var it = source.object.iterator();
            while (it.next()) |kv| {
                const field_name = kv.key_ptr.*;

                inline for (structInfo.fields, 0..) |field, i| {
                    if (field.is_comptime) @compileError("comptime fields are not supported: " ++ @typeName(T) ++ "." ++ field.name);
                    if (std.mem.eql(u8, field.name, field_name)) {
                        assert(!fields_seen[i]); // Can't have duplicate keys in a Value.object.
                        @field(r, field.name) = try innerParseFromValue(field.type, allocator, kv.value_ptr.*, options);
                        fields_seen[i] = true;
                        break;
                    }
                } else {
                    // Didn't match anything.
                    if (!options.ignore_unknown_fields) return error.UnknownField;
                }
            }
            try fillDefaultStructValues(T, &r, &fields_seen);
            return r;
        },

        .array => |arrayInfo| {
            switch (source) {
                .array => |array| {
                    // Typical array.
                    return innerParseArrayFromArrayValue(T, arrayInfo.child, arrayInfo.len, allocator, array, options);
                },
                .string => |s| {
                    if (arrayInfo.child != u8) return error.UnexpectedToken;
                    // Fixed-length string.

                    if (s.len != arrayInfo.len) return error.LengthMismatch;

                    var r: T = undefined;
                    @memcpy(r[0..], s);
                    return r;
                },

                else => return error.UnexpectedToken,
            }
        },

        .vector => |vecInfo| {
            switch (source) {
                .array => |array| {
                    return innerParseArrayFromArrayValue(T, vecInfo.child, vecInfo.len, allocator, array, options);
                },
                else => return error.UnexpectedToken,
            }
        },

        .pointer => |ptrInfo| {
            switch (ptrInfo.size) {
                .one => {
                    const r: *ptrInfo.child = try allocator.create(ptrInfo.child);
                    r.* = try innerParseFromValue(ptrInfo.child, allocator, source, options);
                    return r;
                },
                .slice => {
                    switch (source) {
                        .array => |array| {
                            const r = if (ptrInfo.sentinel()) |sentinel|
                                try allocator.allocSentinel(ptrInfo.child, array.items.len, sentinel)
                            else
                                try allocator.alloc(ptrInfo.child, array.items.len);

                            for (array.items, r) |item, *dest| {
                                dest.* = try innerParseFromValue(ptrInfo.child, allocator, item, options);
                            }

                            return r;
                        },
                        .string => |s| {
                            if (ptrInfo.child != u8) return error.UnexpectedToken;
                            // Dynamic length string.

                            const r = if (ptrInfo.sentinel()) |sentinel|
                                try allocator.allocSentinel(ptrInfo.child, s.len, sentinel)
                            else
                                try allocator.alloc(ptrInfo.child, s.len);
                            @memcpy(r[0..], s);

                            return r;
                        },
                        else => return error.UnexpectedToken,
                    }
                },
                else => @compileError("Unable to parse into type '" ++ @typeName(T) ++ "'"),
            }
        },
        else => @compileError("Unable to parse into type '" ++ @typeName(T) ++ "'"),
    }
}

fn innerParseArrayFromArrayValue(
    comptime T: type,
    comptime Child: type,
    comptime len: comptime_int,
    allocator: Allocator,
    array: Array,
    options: ParseOptions,
) !T {
    if (array.items.len != len) return error.LengthMismatch;

    var r: T = undefined;
    for (array.items, 0..) |item, i| {
        r[i] = try innerParseFromValue(Child, allocator, item, options);
    }

    return r;
}

fn sliceToInt(comptime T: type, slice: []const u8) !T {
    if (isNumberFormattedLikeAnInteger(slice))
        return std.fmt.parseInt(T, slice, 10);
    // Try to coerce a float to an integer.
    const float = try std.fmt.parseFloat(f128, slice);
    if (@round(float) != float) return error.InvalidNumber;
    if (float > @as(f128, @floatFromInt(std.math.maxInt(T))) or float < @as(f128, @floatFromInt(std.math.minInt(T)))) return error.Overflow;
    return @as(T, @intCast(@as(i128, @intFromFloat(float))));
}

fn sliceToEnum(comptime T: type, slice: []const u8) !T {
    // Check for a named value.
    if (std.meta.stringToEnum(T, slice)) |value| return value;
    // Check for a numeric value.
    if (!isNumberFormattedLikeAnInteger(slice)) return error.InvalidEnumTag;
    const n = std.fmt.parseInt(@typeInfo(T).@"enum".tag_type, slice, 10) catch return error.InvalidEnumTag;
    return std.enums.fromInt(T, n) orelse return error.InvalidEnumTag;
}

fn fillDefaultStructValues(comptime T: type, r: *T, fields_seen: *[@typeInfo(T).@"struct".fields.len]bool) !void {
    inline for (@typeInfo(T).@"struct".fields, 0..) |field, i| {
        if (!fields_seen[i]) {
            if (field.defaultValue()) |default| {
                @field(r, field.name) = default;
            } else {
                return error.MissingField;
            }
        }
    }
}

fn freeAllocated(allocator: Allocator, token: Token) void {
    switch (token) {
        .allocated_number, .allocated_string => |slice| {
            allocator.free(slice);
        },
        else => {},
    }
}

test {
    _ = @import("./static_test.zig");
}



---
File: /std/json/Stringify.zig
---

//! Writes JSON ([RFC8259](https://tools.ietf.org/html/rfc8259)) formatted data
//! to a stream.
//!
//! The sequence of method calls to write JSON content must follow this grammar:
//! ```
//!  <once> = <value>
//!  <value> =
//!    | <object>
//!    | <array>
//!    | write
//!    | print
//!    | <writeRawStream>
//!  <object> = beginObject ( <field> <value> )* endObject
//!  <field> = objectField | objectFieldRaw | <objectFieldRawStream>
//!  <array> = beginArray ( <value> )* endArray
//!  <writeRawStream> = beginWriteRaw ( stream.writeAll )* endWriteRaw
//!  <objectFieldRawStream> = beginObjectFieldRaw ( stream.writeAll )* endObjectFieldRaw
//! ```

const std = @import("../std.zig");
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;
const ArrayList = std.ArrayList;
const BitStack = std.BitStack;
const Stringify = @This();
const Writer = std.Io.Writer;

const IndentationMode = enum(u1) {
    object = 0,
    array = 1,
};

writer: *Writer,
options: Options = .{},
indent_level: usize = 0,
next_punctuation: enum {
    the_beginning,
    none,
    comma,
    colon,
} = .the_beginning,

nesting_stack: switch (safety_checks) {
    .checked_to_fixed_depth => |fixed_buffer_size| [(fixed_buffer_size + 7) >> 3]u8,
    .assumed_correct => void,
} = switch (safety_checks) {
    .checked_to_fixed_depth => @splat(0),
    .assumed_correct => {},
},

raw_streaming_mode: if (build_mode_has_safety)
    enum { none, value, objectField }
else
    void = if (build_mode_has_safety) .none else {},

const build_mode_has_safety = switch (@import("builtin").mode) {
    .Debug, .ReleaseSafe => true,
    .ReleaseFast, .ReleaseSmall => false,
};

/// The `safety_checks_hint` parameter determines how much memory is used to enable assertions that the above grammar is being followed,
/// e.g. tripping an assertion rather than allowing `endObject` to emit the final `}` in `[[[]]}`.
/// "Depth" in this context means the depth of nested `[]` or `{}` expressions
/// (or equivalently the amount of recursion on the `<value>` grammar expression above).
/// For example, emitting the JSON `[[[]]]` requires a depth of 3.
/// If `.checked_to_fixed_depth` is used, there is additionally an assertion that the nesting depth never exceeds the given limit.
/// `.checked_to_fixed_depth` embeds the storage required in the `Stringify` struct.
/// `.assumed_correct` requires no space and performs none of these assertions.
/// In `ReleaseFast` and `ReleaseSmall` mode, the given `safety_checks_hint` is ignored and is always treated as `.assumed_correct`.
const safety_checks_hint: union(enum) {
    /// Rounded up to the nearest multiple of 8.
    checked_to_fixed_depth: usize,
    assumed_correct,
} = .{ .checked_to_fixed_depth = 256 };

const safety_checks: @TypeOf(safety_checks_hint) = if (build_mode_has_safety)
    safety_checks_hint
else
    .assumed_correct;

pub const Error = Writer.Error;

pub fn beginArray(self: *Stringify) Error!void {
    if (build_mode_has_safety) assert(self.raw_streaming_mode == .none);
    try self.valueStart();
    try self.writer.writeByte('[');
    try self.pushIndentation(.array);
    self.next_punctuation = .none;
}

pub fn beginObject(self: *Stringify) Error!void {
    if (build_mode_has_safety) assert(self.raw_streaming_mode == .none);
    try self.valueStart();
    try self.writer.writeByte('{');
    try self.pushIndentation(.object);
    self.next_punctuation = .none;
}

pub fn endArray(self: *Stringify) Error!void {
    if (build_mode_has_safety) assert(self.raw_streaming_mode == .none);
    self.popIndentation(.array);
    switch (self.next_punctuation) {
        .none => {},
        .comma => {
            try self.indent();
        },
        .the_beginning, .colon => unreachable,
    }
    try self.writer.writeByte(']');
    self.valueDone();
}

pub fn endObject(self: *Stringify) Error!void {
    if (build_mode_has_safety) assert(self.raw_streaming_mode == .none);
    self.popIndentation(.object);
    switch (self.next_punctuation) {
        .none => {},
        .comma => {
            try self.indent();
        },
        .the_beginning, .colon => unreachable,
    }
    try self.writer.writeByte('}');
    self.valueDone();
}

fn pushIndentation(self: *Stringify, mode: IndentationMode) !void {
    switch (safety_checks) {
        .checked_to_fixed_depth => {
            BitStack.pushWithStateAssumeCapacity(&self.nesting_stack, &self.indent_level, @intFromEnum(mode));
        },
        .assumed_correct => {
            self.indent_level += 1;
        },
    }
}
fn popIndentation(self: *Stringify, expected_mode: IndentationMode) void {
    switch (safety_checks) {
        .checked_to_fixed_depth => {
            assert(BitStack.popWithState(&self.nesting_stack, &self.indent_level) == @intFromEnum(expected_mode));
        },
        .assumed_correct => {
            self.indent_level -= 1;
        },
    }
}

fn indent(self: *Stringify) !void {
    var char: u8 = ' ';
    const n_chars = switch (self.options.whitespace) {
        .minified => return,
        .indent_1 => 1 * self.indent_level,
        .indent_2 => 2 * self.indent_level,
        .indent_3 => 3 * self.indent_level,
        .indent_4 => 4 * self.indent_level,
        .indent_8 => 8 * self.indent_level,
        .indent_tab => blk: {
            char = '\t';
            break :blk self.indent_level;
        },
    };
    try self.writer.writeByte('\n');
    try self.writer.splatByteAll(char, n_chars);
}

fn valueStart(self: *Stringify) !void {
    if (self.isObjectKeyExpected()) |is_it| assert(!is_it); // Call objectField*(), not write(), for object keys.
    return self.valueStartAssumeTypeOk();
}
fn objectFieldStart(self: *Stringify) !void {
    if (self.isObjectKeyExpected()) |is_it| assert(is_it); // Expected write(), not objectField*().
    return self.valueStartAssumeTypeOk();
}
fn valueStartAssumeTypeOk(self: *Stringify) !void {
    assert(!self.isComplete()); // JSON document already complete.
    switch (self.next_punctuation) {
        .the_beginning => {
            // No indentation for the very beginning.
        },
        .none => {
            // First item in a container.
            try self.indent();
        },
        .comma => {
            // Subsequent item in a container.
            try self.writer.writeByte(',');
            try self.indent();
        },
        .colon => {
            try self.writer.writeByte(':');
            if (self.options.whitespace != .minified) {
                try self.writer.writeByte(' ');
            }
        },
    }
}
fn valueDone(self: *Stringify) void {
    self.next_punctuation = .comma;
}

// Only when safety is enabled:
fn isObjectKeyExpected(self: *const Stringify) ?bool {
    switch (safety_checks) {
        .checked_to_fixed_depth => return self.indent_level > 0 and
            BitStack.peekWithState(&self.nesting_stack, self.indent_level) == @intFromEnum(IndentationMode.object) and
            self.next_punctuation != .colon,
        .assumed_correct => return null,
    }
}
fn isComplete(self: *const Stringify) bool {
    return self.indent_level == 0 and self.next_punctuation == .comma;
}

/// An alternative to calling `write` that formats a value with `std.fmt`.
/// This function does the usual punctuation and indentation formatting
/// assuming the resulting formatted string represents a single complete value;
/// e.g. `"1"`, `"[]"`, `"[1,2]"`, not `"1,2"`.
/// This function may be useful for doing your own number formatting.
pub fn print(self: *Stringify, comptime fmt: []const u8, args: anytype) Error!void {
    if (build_mode_has_safety) assert(self.raw_streaming_mode == .none);
    try self.valueStart();
    try self.writer.print(fmt, args);
    self.valueDone();
}

test print {
    var out_buf: [1024]u8 = undefined;
    var out: Writer = .fixed(&out_buf);

    var w: Stringify = .{ .writer = &out, .options = .{ .whitespace = .indent_2 } };

    try w.beginObject();
    try w.objectField("a");
    try w.print("[  ]", .{});
    try w.objectField("b");
    try w.beginArray();
    try w.print("[{s}] ", .{"[]"});
    try w.print("  {}", .{12345});
    try w.endArray();
    try w.endObject();

    const expected =
        \\{
        \\  "a": [  ],
        \\  "b": [
        \\    [[]] ,
        \\      12345
        \\  ]
        \\}
    ;
    try std.testing.expectEqualStrings(expected, out.buffered());
}

/// An alternative to calling `write` that allows you to write directly to the `.writer` field, e.g. with `.writer.writeAll()`.
/// Call `beginWriteRaw()`, then write a complete value (including any quotes if necessary) directly to the `.writer` field,
/// then call `endWriteRaw()`.
/// This can be useful for streaming very long strings into the output without needing it all buffered in memory.
pub fn beginWriteRaw(self: *Stringify) !void {
    if (build_mode_has_safety) {
        assert(self.raw_streaming_mode == .none);
        self.raw_streaming_mode = .value;
    }
    try self.valueStart();
}

/// See `beginWriteRaw`.
pub fn endWriteRaw(self: *Stringify) void {
    if (build_mode_has_safety) {
        assert(self.raw_streaming_mode == .value);
        self.raw_streaming_mode = .none;
    }
    self.valueDone();
}

/// See `Stringify` for when to call this method.
/// `key` is the string content of the property name.
/// Surrounding quotes will be added and any special characters will be escaped.
/// See also `objectFieldRaw`.
pub fn objectField(self: *Stringify, key: []const u8) Error!void {
    if (build_mode_has_safety) assert(self.raw_streaming_mode == .none);
    try self.objectFieldStart();
    try encodeJsonString(key, self.options, self.writer);
    self.next_punctuation = .colon;
}
/// See `Stringify` for when to call this method.
/// `quoted_key` is the complete bytes of the key including quotes and any necessary escape sequences.
/// A few assertions are performed on the given value to ensure that the caller of this function understands the API contract.
/// See also `objectField`.
pub fn objectFieldRaw(self: *Stringify, quoted_key: []const u8) Error!void {
    if (build_mode_has_safety) assert(self.raw_streaming_mode == .none);
    assert(quoted_key.len >= 2 and quoted_key[0] == '"' and quoted_key[quoted_key.len - 1] == '"'); // quoted_key should be "quoted".
    try self.objectFieldStart();
    try self.writer.writeAll(quoted_key);
    self.next_punctuation = .colon;
}

/// In the rare case that you need to write very long object field names,
/// this is an alternative to `objectField` and `objectFieldRaw` that allows you to write directly to the `.writer` field
/// similar to `beginWriteRaw`.
/// Call `endObjectFieldRaw()` when you're done.
pub fn beginObjectFieldRaw(self: *Stringify) !void {
    if (build_mode_has_safety) {
        assert(self.raw_streaming_mode == .none);
        self.raw_streaming_mode = .objectField;
    }
    try self.objectFieldStart();
}

/// See `beginObjectFieldRaw`.
pub fn endObjectFieldRaw(self: *Stringify) void {
    if (build_mode_has_safety) {
        assert(self.raw_streaming_mode == .objectField);
        self.raw_streaming_mode = .none;
    }
    self.next_punctuation = .colon;
}

/// Renders the given Zig value as JSON.
///
/// Supported types:
///  * Zig `bool` -> JSON `true` or `false`.
///  * Zig `?T` -> `null` or the rendering of `T`.
///  * Zig `i32`, `u64`, etc. -> JSON number or string.
///      * When option `emit_nonportable_numbers_as_strings` is true, if the value is outside the range `+-1<<53` (the precise integer range of f64), it is rendered as a JSON string in base 10. Otherwise, it is rendered as JSON number.
///  * Zig floats -> JSON number or string.
///      * If the value cannot be precisely represented by an f64, it is rendered as a JSON string. Otherwise, it is rendered as JSON number.
///      * TODO: Float rendering will likely change in the future, e.g. to remove the unnecessary "e+00".
///  * Zig `[]const u8`, `[]u8`, `*[N]u8`, `@Vector(N, u8)`, and similar -> JSON string.
///      * See `Options.emit_strings_as_arrays`.
///      * If the content is not valid UTF-8, rendered as an array of numbers instead.
///  * Zig `[]T`, `[N]T`, `*[N]T`, `@Vector(N, T)`, and similar -> JSON array of the rendering of each item.
///  * Zig tuple -> JSON array of the rendering of each item.
///  * Zig `struct` -> JSON object with each field in declaration order.
///      * If the struct declares a method `pub fn jsonStringify(self: *@This(), jw: anytype) !void`, it is called to do the serialization instead of the default behavior. The given `jw` is a pointer to this `Stringify`. See `std.json.Value` for an example.
///      * See `Options.emit_null_optional_fields`.
///  * Zig `union(enum)` -> JSON object with one field named for the active tag and a value representing the payload.
///      * If the payload is `void`, then the emitted value is `{}`.
///      * If the union declares a method `pub fn jsonStringify(self: *@This(), jw: anytype) !void`, it is called to do the serialization instead of the default behavior. The given `jw` is a pointer to this `Stringify`.
///  * Zig `enum` -> JSON string naming the active tag.
///      * If the enum declares a method `pub fn jsonStringify(self: *@This(), jw: anytype) !void`, it is called to do the serialization instead of the default behavior. The given `jw` is a pointer to this `Stringify`.
///      * If the enum is non-exhaustive, unnamed values are rendered as integers.
///  * Zig untyped enum literal -> JSON string naming the active tag.
///  * Zig error -> JSON string naming the error.
///  * Zig `*T` -> the rendering of `T`. Note there is no guard against circular-reference infinite recursion.
///
/// See also alternative functions `print` and `beginWriteRaw`.
/// For writing object field names, use `objectField` instead.
pub fn write(self: *Stringify, v: anytype) Error!void {
    if (build_mode_has_safety) assert(self.raw_streaming_mode == .none);
    const T = @TypeOf(v);
    switch (@typeInfo(T)) {
        .int => {
            try self.valueStart();
            if (self.options.emit_nonportable_numbers_as_strings and
                (v <= -(1 << 53) or v >= (1 << 53)))
            {
                try self.writer.print("\"{}\"", .{v});
            } else {
                try self.writer.print("{}", .{v});
            }
            self.valueDone();
            return;
        },
        .comptime_int => {
            return self.write(@as(std.math.IntFittingRange(v, v), v));
        },
        .float, .comptime_float => {
            if (@as(f64, @floatCast(v)) == v) {
                try self.valueStart();
                try self.writer.print("{}", .{@as(f64, @floatCast(v))});
                self.valueDone();
                return;
            }
            try self.valueStart();
            try self.writer.print("\"{}\"", .{v});
            self.valueDone();
            return;
        },

        .bool => {
            try self.valueStart();
            try self.writer.writeAll(if (v) "true" else "false");
            self.valueDone();
            return;
        },
        .null => {
            try self.valueStart();
            try self.writer.writeAll("null");
            self.valueDone();
            return;
        },
        .optional => {
            if (v) |payload| {
                return try self.write(payload);
            } else {
                return try self.write(null);
            }
        },
        .@"enum" => |enum_info| {
            if (std.meta.hasFn(T, "jsonStringify")) {
                return v.jsonStringify(self);
            }

            if (!enum_info.is_exhaustive) {
                inline for (enum_info.fields) |field| {
                    if (v == @field(T, field.name)) {
                        break;
                    }
                } else {
                    return self.write(@intFromEnum(v));
                }
            }

            return self.stringValue(@tagName(v));
        },
        .enum_literal => {
            return self.stringValue(@tagName(v));
        },
        .@"union" => {
            if (std.meta.hasFn(T, "jsonStringify")) {
                return v.jsonStringify(self);
            }

            const info = @typeInfo(T).@"union";
            if (info.tag_type) |UnionTagType| {
                try self.beginObject();
                inline for (info.fields) |u_field| {
                    if (v == @field(UnionTagType, u_field.name)) {
                        try self.objectField(u_field.name);
                        if (u_field.type == void) {
                            // void v is {}
                            try self.beginObject();
                            try self.endObject();
                        } else {
                            try self.write(@field(v, u_field.name));
                        }
                        break;
                    }
                } else {
                    unreachable; // No active tag?
                }
                try self.endObject();
                return;
            } else {
                @compileError("Unable to stringify untagged union '" ++ @typeName(T) ++ "'");
            }
        },
        .@"struct" => |S| {
            if (std.meta.hasFn(T, "jsonStringify")) {
                return v.jsonStringify(self);
            }

            if (S.is_tuple) {
                try self.beginArray();
            } else {
                try self.beginObject();
            }
            inline for (S.fields) |Field| {
                // don't include void fields
                if (Field.type == void) continue;

                var emit_field = true;

                // don't include optional fields that are null when emit_null_optional_fields is set to false
                if (@typeInfo(Field.type) == .optional) {
                    if (self.options.emit_null_optional_fields == false) {
                        if (@field(v, Field.name) == null) {
                            emit_field = false;
                        }
                    }
                }

                if (emit_field) {
                    if (!S.is_tuple) {
                        try self.objectField(Field.name);
                    }
                    try self.write(@field(v, Field.name));
                }
            }
            if (S.is_tuple) {
                try self.endArray();
            } else {
                try self.endObject();
            }
            return;
        },
        .error_set => return self.stringValue(@errorName(v)),
        .pointer => |ptr_info| switch (ptr_info.size) {
            .one => switch (@typeInfo(ptr_info.child)) {
                .array => {
                    // Coerce `*[N]T` to `[]const T`.
                    const Slice = []const std.meta.Elem(ptr_info.child);
                    return self.write(@as(Slice, v));
                },
                else => {
                    return self.write(v.*);
                },
            },
            .many, .slice => {
                if (ptr_info.size == .many and ptr_info.sentinel() == null)
                    @compileError("unable to stringify type '" ++ @typeName(T) ++ "' without sentinel");
                const slice = if (ptr_info.size == .many) std.mem.span(v) else v;

                if (ptr_info.child == u8) {
                    // This is a []const u8, or some similar Zig string.
                    if (!self.options.emit_strings_as_arrays and std.unicode.utf8ValidateSlice(slice)) {
                        return self.stringValue(slice);
                    }
                }

                try self.beginArray();
                for (slice) |x| {
                    try self.write(x);
                }
                try self.endArray();
                return;
            },
            else => @compileError("Unable to stringify type '" ++ @typeName(T) ++ "'"),
        },
        .array => {
            // Coerce `[N]T` to `*const [N]T` (and then to `[]const T`).
            return self.write(&v);
        },
        .vector => |info| {
            const array: [info.len]info.child = v;
            return self.write(&array);
        },
        else => @compileError("Unable to stringify type '" ++ @typeName(T) ++ "'"),
    }
    unreachable;
}

fn stringValue(self: *Stringify, s: []const u8) !void {
    try self.valueStart();
    try encodeJsonString(s, self.options, self.writer);
    self.valueDone();
}

pub const Options = struct {
    /// Controls the whitespace emitted.
    /// The default `.minified` is a compact encoding with no whitespace between tokens.
    /// Any setting other than `.minified` will use newlines, indentation, and a space after each ':'.
    /// `.indent_1` means 1 space for each indentation level, `.indent_2` means 2 spaces, etc.
    /// `.indent_tab` uses a tab for each indentation level.
    whitespace: enum {
        minified,
        indent_1,
        indent_2,
        indent_3,
        indent_4,
        indent_8,
        indent_tab,
    } = .minified,

    /// Should optional fields with null value be written?
    emit_null_optional_fields: bool = true,

    /// Arrays/slices of u8 are typically encoded as JSON strings.
    /// This option emits them as arrays of numbers instead.
    /// Does not affect calls to `objectField*()`.
    emit_strings_as_arrays: bool = false,

    /// Should unicode characters be escaped in strings?
    escape_unicode: bool = false,

    /// When true, renders numbers outside the range `+-1<<53` (the precise integer range of f64) as JSON strings in base 10.
    emit_nonportable_numbers_as_strings: bool = false,
};

/// Writes the given value to the `Writer` writer.
/// See `Stringify` for how the given value is serialized into JSON.
/// The maximum nesting depth of the output JSON document is 256.
pub fn value(v: anytype, options: Options, writer: *Writer) Error!void {
    var s: Stringify = .{ .writer = writer, .options = options };
    try s.write(v);
}

test value {
    var out: Writer.Allocating = .init(std.testing.allocator);
    const writer = &out.writer;
    defer out.deinit();

    const T = struct { a: i32, b: []const u8 };
    try value(T{ .a = 123, .b = "xy" }, .{}, writer);
    try std.testing.expectEqualSlices(u8, "{\"a\":123,\"b\":\"xy\"}", out.written());

    try testStringify("9999999999999999", 9999999999999999, .{});
    try testStringify("\"9999999999999999\"", 9999999999999999, .{ .emit_nonportable_numbers_as_strings = true });

    try testStringify("[1,1]", @as(@Vector(2, u32), @splat(1)), .{});
    try testStringify("\"AA\"", @as(@Vector(2, u8), @splat('A')), .{});
    try testStringify("[65,65]", @as(@Vector(2, u8), @splat('A')), .{ .emit_strings_as_arrays = true });

    // void field
    try testStringify("{\"foo\":42}", struct {
        foo: u32,
        bar: void = {},
    }{ .foo = 42 }, .{});

    const Tuple = struct { []const u8, usize };
    try testStringify("[\"foo\",42]", Tuple{ "foo", 42 }, .{});

    comptime {
        testStringify("false", false, .{}) catch unreachable;
        const MyStruct = struct { foo: u32 };
        testStringify("[{\"foo\":42},{\"foo\":100},{\"foo\":1000}]", [_]MyStruct{
            MyStruct{ .foo = 42 },
            MyStruct{ .foo = 100 },
            MyStruct{ .foo = 1000 },
        }, .{}) catch unreachable;
    }
}

/// Calls `value` and stores the result in dynamically allocated memory instead
/// of taking a writer.
///
/// Caller owns returned memory.
pub fn valueAlloc(gpa: Allocator, v: anytype, options: Options) error{OutOfMemory}![]u8 {
    var aw: Writer.Allocating = .init(gpa);
    defer aw.deinit();
    value(v, options, &aw.writer) catch return error.OutOfMemory;
    return aw.toOwnedSlice();
}

test valueAlloc {
    const allocator = std.testing.allocator;
    const expected =
        \\{"foo":"bar","answer":42,"my_friend":"sammy"}
    ;
    const actual = try valueAlloc(allocator, .{ .foo = "bar", .answer = 42, .my_friend = "sammy" }, .{});
    defer allocator.free(actual);

    try std.testing.expectEqualStrings(expected, actual);
}

fn outputUnicodeEscape(codepoint: u21, w: *Writer) Error!void {
    if (codepoint <= 0xFFFF) {
        // If the character is in the Basic Multilingual Plane (U+0000 through U+FFFF),
        // then it may be represented as a six-character sequence: a reverse solidus, followed
        // by the lowercase letter u, followed by four hexadecimal digits that encode the character's code point.
        try w.writeAll("\\u");
        try w.printInt(codepoint, 16, .lower, .{ .width = 4, .fill = '0' });
    } else {
        assert(codepoint <= 0x10FFFF);
        // To escape an extended character that is not in the Basic Multilingual Plane,
        // the character is represented as a 12-character sequence, encoding the UTF-16 surrogate pair.
        const high = @as(u16, @intCast((codepoint - 0x10000) >> 10)) + 0xD800;
        const low = @as(u16, @intCast(codepoint & 0x3FF)) + 0xDC00;
        try w.writeAll("\\u");
        try w.printInt(high, 16, .lower, .{ .width = 4, .fill = '0' });
        try w.writeAll("\\u");
        try w.printInt(low, 16, .lower, .{ .width = 4, .fill = '0' });
    }
}

fn outputSpecialEscape(c: u8, writer: *Writer) Error!void {
    switch (c) {
        '\\' => try writer.writeAll("\\\\"),
        '\"' => try writer.writeAll("\\\""),
        0x08 => try writer.writeAll("\\b"),
        0x0C => try writer.writeAll("\\f"),
        '\n' => try writer.writeAll("\\n"),
        '\r' => try writer.writeAll("\\r"),
        '\t' => try writer.writeAll("\\t"),
        else => try outputUnicodeEscape(c, writer),
    }
}

/// Write `string` to `writer` as a JSON encoded string.
pub fn encodeJsonString(string: []const u8, options: Options, writer: *Writer) Error!void {
    try writer.writeByte('\"');
    try encodeJsonStringChars(string, options, writer);
    try writer.writeByte('\"');
}

/// Write `chars` to `writer` as JSON encoded string characters.
pub fn encodeJsonStringChars(chars: []const u8, options: Options, writer: *Writer) Error!void {
    var write_cursor: usize = 0;
    var i: usize = 0;
    if (options.escape_unicode) {
        while (i < chars.len) : (i += 1) {
            switch (chars[i]) {
                // normal ascii character
                0x20...0x21, 0x23...0x5B, 0x5D...0x7E => {},
                0x00...0x1F, '\\', '\"' => {
                    // Always must escape these.
                    try writer.writeAll(chars[write_cursor..i]);
                    try outputSpecialEscape(chars[i], writer);
                    write_cursor = i + 1;
                },
                0x7F...0xFF => {
                    try writer.writeAll(chars[write_cursor..i]);
                    const ulen = std.unicode.utf8ByteSequenceLength(chars[i]) catch unreachable;
                    const codepoint = std.unicode.utf8Decode(chars[i..][0..ulen]) catch unreachable;
                    try outputUnicodeEscape(codepoint, writer);
                    i += ulen - 1;
                    write_cursor = i + 1;
                },
            }
        }
    } else {
        while (i < chars.len) : (i += 1) {
            switch (chars[i]) {
                // normal bytes
                0x20...0x21, 0x23...0x5B, 0x5D...0xFF => {},
                0x00...0x1F, '\\', '\"' => {
                    // Always must escape these.
                    try writer.writeAll(chars[write_cursor..i]);
                    try outputSpecialEscape(chars[i], writer);
                    write_cursor = i + 1;
                },
            }
        }
    }
    try writer.writeAll(chars[write_cursor..chars.len]);
}

test "json write stream" {
    var out_buf: [1024]u8 = undefined;
    var out: Writer = .fixed(&out_buf);
    var w: Stringify = .{ .writer = &out, .options = .{ .whitespace = .indent_2 } };
    try testBasicWriteStream(&w);
}

fn testBasicWriteStream(w: *Stringify) !void {
    w.writer.end = 0;

    try w.beginObject();

    try w.objectField("object");
    var arena_allocator = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena_allocator.deinit();
    try w.write(try getJsonObject(arena_allocator.allocator()));

    try w.objectFieldRaw("\"string\"");
    try w.write("This is a string");

    try w.objectField("array");
    try w.beginArray();
    try w.write("Another string");
    try w.write(@as(i32, 1));
    try w.write(@as(f32, 3.5));
    try w.endArray();

    try w.objectField("int");
    try w.write(@as(i32, 10));

    try w.objectField("float");
    try w.write(@as(f32, 3.5));

    try w.endObject();

    const expected =
        \\{
        \\  "object": {
        \\    "one": 1,
        \\    "two": 2
        \\  },
        \\  "string": "This is a string",
        \\  "array": [
        \\    "Another string",
        \\    1,
        \\    3.5
        \\  ],
        \\  "int": 10,
        \\  "float": 3.5
        \\}
    ;
    try std.testing.expectEqualStrings(expected, w.writer.buffered());
}

fn getJsonObject(allocator: std.mem.Allocator) !std.json.Value {
    var v: std.json.Value = .{ .object = .empty };
    try v.object.put(allocator, "one", std.json.Value{ .integer = @as(i64, @intCast(1)) });
    try v.object.put(allocator, "two", std.json.Value{ .float = 2.0 });
    return v;
}

test "stringify null optional fields" {
    const MyStruct = struct {
        optional: ?[]const u8 = null,
        required: []const u8 = "something",
        another_optional: ?[]const u8 = null,
        another_required: []const u8 = "something else",
    };
    try testStringify(
        \\{"optional":null,"required":"something","another_optional":null,"another_required":"something else"}
    ,
        MyStruct{},
        .{},
    );
    try testStringify(
        \\{"required":"something","another_required":"something else"}
    ,
        MyStruct{},
        .{ .emit_null_optional_fields = false },
    );
}

test "stringify basic types" {
    try testStringify("false", false, .{});
    try testStringify("true", true, .{});
    try testStringify("null", @as(?u8, null), .{});
    try testStringify("null", @as(?*u32, null), .{});
    try testStringify("42", 42, .{});
    try testStringify("42", 42.0, .{});
    try testStringify("42", @as(u8, 42), .{});
    try testStringify("42", @as(u128, 42), .{});
    try testStringify("9999999999999999", 9999999999999999, .{});
    try testStringify("42", @as(f32, 42), .{});
    try testStringify("42", @as(f64, 42), .{});
    try testStringify("\"ItBroke\"", @as(anyerror, error.ItBroke), .{});
    try testStringify("\"ItBroke\"", error.ItBroke, .{});
}

test "stringify string" {
    try testStringify("\"hello\"", "hello", .{});
    try testStringify("\"with\\nescapes\\r\"", "with\nescapes\r", .{});
    try testStringify("\"with\\nescapes\\r\"", "with\nescapes\r", .{ .escape_unicode = true });
    try testStringify("\"with unicode\\u0001\"", "with unicode\u{1}", .{});
    try testStringify("\"with unicode\\u0001\"", "with unicode\u{1}", .{ .escape_unicode = true });
    try testStringify("\"with unicode\u{80}\"", "with unicode\u{80}", .{});
    try testStringify("\"with unicode\\u0080\"", "with unicode\u{80}", .{ .escape_unicode = true });
    try testStringify("\"with unicode\u{FF}\"", "with unicode\u{FF}", .{});
    try testStringify("\"with unicode\\u00ff\"", "with unicode\u{FF}", .{ .escape_unicode = true });
    try testStringify("\"with unicode\u{100}\"", "with unicode\u{100}", .{});
    try testStringify("\"with unicode\\u0100\"", "with unicode\u{100}", .{ .escape_unicode = true });
    try testStringify("\"with unicode\u{800}\"", "with unicode\u{800}", .{});
    try testStringify("\"with unicode\\u0800\"", "with unicode\u{800}", .{ .escape_unicode = true });
    try testStringify("\"with unicode\u{8000}\"", "with unicode\u{8000}", .{});
    try testStringify("\"with unicode\\u8000\"", "with unicode\u{8000}", .{ .escape_unicode = true });
    try testStringify("\"with unicode\u{D799}\"", "with unicode\u{D799}", .{});
    try testStringify("\"with unicode\\ud799\"", "with unicode\u{D799}", .{ .escape_unicode = true });
    try testStringify("\"with unicode\u{10000}\"", "with unicode\u{10000}", .{});
    try testStringify("\"with unicode\\ud800\\udc00\"", "with unicode\u{10000}", .{ .escape_unicode = true });
    try testStringify("\"with unicode\u{10FFFF}\"", "with unicode\u{10FFFF}", .{});
    try testStringify("\"with unicode\\udbff\\udfff\"", "with unicode\u{10FFFF}", .{ .escape_unicode = true });
}

test "stringify many-item sentinel-terminated string" {
    try testStringify("\"hello\"", @as([*:0]const u8, "hello"), .{});
    try testStringify("\"with\\nescapes\\r\"", @as([*:0]const u8, "with\nescapes\r"), .{ .escape_unicode = true });
    try testStringify("\"with unicode\\u0001\"", @as([*:0]const u8, "with unicode\u{1}"), .{ .escape_unicode = true });
}

test "stringify enums" {
    const E = enum {
        foo,
        bar,
    };
    try testStringify("\"foo\"", E.foo, .{});
    try testStringify("\"bar\"", E.bar, .{});
}

test "stringify non-exhaustive enum" {
    const E = enum(u8) {
        foo = 0,
        _,
    };
    try testStringify("\"foo\"", E.foo, .{});
    try testStringify("1", @as(E, @enumFromInt(1)), .{});
}

test "stringify enum literals" {
    try testStringify("\"foo\"", .foo, .{});
    try testStringify("\"bar\"", .bar, .{});
}

test "stringify tagged unions" {
    const T = union(enum) {
        nothing,
        foo: u32,
        bar: bool,
    };
    try testStringify("{\"nothing\":{}}", T{ .nothing = {} }, .{});
    try testStringify("{\"foo\":42}", T{ .foo = 42 }, .{});
    try testStringify("{\"bar\":true}", T{ .bar = true }, .{});
}

test "stringify struct" {
    try testStringify("{\"foo\":42}", struct {
        foo: u32,
    }{ .foo = 42 }, .{});
}

test "emit_strings_as_arrays" {
    // Should only affect string values, not object keys.
    try testStringify("{\"foo\":\"bar\"}", .{ .foo = "bar" }, .{});
    try testStringify("{\"foo\":[98,97,114]}", .{ .foo = "bar" }, .{ .emit_strings_as_arrays = true });
    // Should *not* affect these types:
    try testStringify("\"foo\"", @as(enum { foo, bar }, .foo), .{ .emit_strings_as_arrays = true });
    try testStringify("\"ItBroke\"", error.ItBroke, .{ .emit_strings_as_arrays = true });
    // Should work on these:
    try testStringify("\"bar\"", @Vector(3, u8){ 'b', 'a', 'r' }, .{});
    try testStringify("[98,97,114]", @Vector(3, u8){ 'b', 'a', 'r' }, .{ .emit_strings_as_arrays = true });
    try testStringify("\"bar\"", [3]u8{ 'b', 'a', 'r' }, .{});
    try testStringify("[98,97,114]", [3]u8{ 'b', 'a', 'r' }, .{ .emit_strings_as_arrays = true });
}

test "stringify struct with indentation" {
    try testStringify(
        \\{
        \\    "foo": 42,
        \\    "bar": [
        \\        1,
        \\        2,
        \\        3
        \\    ]
        \\}
    ,
        struct {
            foo: u32,
            bar: [3]u32,
        }{
            .foo = 42,
            .bar = .{ 1, 2, 3 },
        },
        .{ .whitespace = .indent_4 },
    );
    try testStringify(
        "{\n\t\"foo\": 42,\n\t\"bar\": [\n\t\t1,\n\t\t2,\n\t\t3\n\t]\n}",
        struct {
            foo: u32,
            bar: [3]u32,
        }{
            .foo = 42,
            .bar = .{ 1, 2, 3 },
        },
        .{ .whitespace = .indent_tab },
    );
    try testStringify(
        \\{"foo":42,"bar":[1,2,3]}
    ,
        struct {
            foo: u32,
            bar: [3]u32,
        }{
            .foo = 42,
            .bar = .{ 1, 2, 3 },
        },
        .{ .whitespace = .minified },
    );
}

test "stringify array of structs" {
    const MyStruct = struct {
        foo: u32,
    };
    try testStringify("[{\"foo\":42},{\"foo\":100},{\"foo\":1000}]", [_]MyStruct{
        MyStruct{ .foo = 42 },
        MyStruct{ .foo = 100 },
        MyStruct{ .foo = 1000 },
    }, .{});
}

test "stringify struct with custom stringifier" {
    try testStringify("[\"something special\",42]", struct {
        foo: u32,
        const Self = @This();
        pub fn jsonStringify(v: @This(), jws: anytype) !void {
            _ = v;
            try jws.beginArray();
            try jws.write("something special");
            try jws.write(42);
            try jws.endArray();
        }
    }{ .foo = 42 }, .{});
}

fn testStringify(expected: []const u8, v: anytype, options: Options) !void {
    var buffer: [4096]u8 = undefined;
    var w: Writer = .fixed(&buffer);
    try value(v, options, &w);
    try std.testing.expectEqualStrings(expected, w.buffered());
}

test "raw streaming" {
    var out_buf: [1024]u8 = undefined;
    var out: Writer = .fixed(&out_buf);

    var w: Stringify = .{ .writer = &out, .options = .{ .whitespace = .indent_2 } };
    try w.beginObject();
    try w.beginObjectFieldRaw();
    try w.writer.writeAll("\"long");
    try w.writer.writeAll(" key\"");
    w.endObjectFieldRaw();
    try w.beginWriteRaw();
    try w.writer.writeAll("\"long");
    try w.writer.writeAll(" value\"");
    w.endWriteRaw();
    try w.endObject();

    const expected =
        \\{
        \\  "long key": "long value"
        \\}
    ;
    try std.testing.expectEqualStrings(expected, w.writer.buffered());
}



---
File: /std/json/test.zig
---




---
File: /std/math/big/int_test.zig
---




---
File: /std/math/big/int.zig
---

const std = @import("../../std.zig");
const builtin = @import("builtin");
const math = std.math;
const Limb = std.math.big.Limb;
const limb_bits = @typeInfo(Limb).int.bits;
const HalfLimb = std.math.big.HalfLimb;
const half_limb_bits = @typeInfo(HalfLimb).int.bits;
const DoubleLimb = std.math.big.DoubleLimb;
const SignedDoubleLimb = std.math.big.SignedDoubleLimb;
const Log2Limb = std.math.big.Log2Limb;
const Allocator = std.mem.Allocator;
const mem = std.mem;
const maxInt = std.math.maxInt;
const minInt = std.math.minInt;
const assert = std.debug.assert;
const Endian = std.builtin.Endian;
const Signedness = std.builtin.Signedness;
const native_endian = builtin.cpu.arch.endian();

// Comptime-computed constants for supported bases (2 - 36)
// all values are set to 0 for bases 0 - 1, to make it possible to
// access a constant for a given base b using `constants.value[b]`
const Constants = struct {
    // big_bases[b] is the biggest power of b that fit in a single Limb
    // i.e. big_bases[b] = b^k < 2^@bitSizeOf(Limb) and b^(k+1) >= 2^@bitSizeOf(Limb)
    big_bases: [37]Limb,
    // digits_per_limb[b] is the value of k used in the previous field
    digits_per_limb: [37]u8,
};
const constants: Constants = blk: {
    @setEvalBranchQuota(2000);
    var digits_per_limb = [_]u8{0} ** 37;
    var bases = [_]Limb{0} ** 37;
    for (2..37) |base| {
        digits_per_limb[base] = @intCast(math.log(Limb, base, math.maxInt(Limb)));
        bases[base] = std.math.pow(Limb, base, digits_per_limb[base]);
    }
    break :blk Constants{ .big_bases = bases, .digits_per_limb = digits_per_limb };
};

/// Returns the number of limbs needed to store `scalar`, which must be a
/// primitive integer or float value.
/// Note: A comptime-known upper bound of this value that may be used
/// instead if `scalar` is not already comptime-known is
/// `calcTwosCompLimbCount(@typeInfo(@TypeOf(scalar)).int.bits)`
pub fn calcLimbLen(scalar: anytype) usize {
    switch (@typeInfo(@TypeOf(scalar))) {
        .int, .comptime_int => {
            if (scalar == 0) return 1;
            const w_value = @abs(scalar);
            return @as(usize, @intCast(@divFloor(@as(Limb, @intCast(math.log2(w_value))), limb_bits) + 1));
        },
        .float => {
            const repr: std.math.FloatRepr(@TypeOf(scalar)) = @bitCast(scalar);
            return switch (repr.exponent) {
                .denormal => 1,
                else => return calcNonZeroTwosCompLimbCount(@as(usize, 2) + @max(repr.exponent.unbias(), 0)),
                .infinite => 0,
            };
        },
        .comptime_float => return calcLimbLen(@as(f128, scalar)),
        else => @compileError("expected float or int, got " ++ @typeName(@TypeOf(scalar))),
    }
}

/// Same as `calcToStringLimbsBufferLen`, without the useless base check.
pub fn calcLog10LimbsBufferLen(a_len: usize) usize {
    return a_len + 2 + a_len + calcDivLimbsBufferLen(a_len, 1);
}

pub fn calcToStringLimbsBufferLen(a_len: usize, base: u8) usize {
    if (math.isPowerOfTwo(base))
        return 0;
    return a_len + 2 + a_len + calcDivLimbsBufferLen(a_len, 1);
}

pub fn calcDivLimbsBufferLen(a_len: usize, b_len: usize) usize {
    return a_len + b_len + 4;
}

pub fn calcMulLimbsBufferLen(a_len: usize, b_len: usize, aliases: usize) usize {
    return aliases * @max(a_len, b_len);
}

pub fn calcMulWrapLimbsBufferLen(bit_count: usize, a_len: usize, b_len: usize, aliases: usize) usize {
    const req_limbs = calcTwosCompLimbCount(bit_count);
    return aliases * @min(req_limbs, @max(a_len, b_len));
}

pub fn calcSetStringLimbsBufferLen(base: u8, string_len: usize) usize {
    const limb_count = calcSetStringLimbCount(base, string_len);
    return calcMulLimbsBufferLen(limb_count, limb_count, 2);
}

/// Assumes `string_len` doesn't account for minus signs if the number is negative.
pub fn calcSetStringLimbCount(base: u8, string_len: usize) usize {
    const base_f: f32 = @floatFromInt(base);
    const string_len_f: f32 = @floatFromInt(string_len);
    return 1 + @as(usize, @intFromFloat(@ceil(string_len_f * std.math.log2(base_f) / limb_bits)));
}

pub fn calcPowLimbsBufferLen(a_bit_count: usize, y: usize) usize {
    // The 2 accounts for the minimum space requirement for llmulacc
    return 2 + (a_bit_count * y + (limb_bits - 1)) / limb_bits;
}

pub fn calcSqrtLimbsBufferLen(a_bit_count: usize) usize {
    const a_limb_count = (a_bit_count - 1) / limb_bits + 1;
    const shift = (a_bit_count + 1) / 2;
    const u_s_rem_limb_count = 1 + ((shift / limb_bits) + 1);
    return a_limb_count + 3 * u_s_rem_limb_count + calcDivLimbsBufferLen(a_limb_count, u_s_rem_limb_count);
}

/// Compute the number of limbs required to store a 2s-complement number of `bit_count` bits.
pub fn calcNonZeroTwosCompLimbCount(bit_count: usize) usize {
    assert(bit_count != 0);
    return calcTwosCompLimbCount(bit_count);
}

/// Compute the number of limbs required to store a 2s-complement number of `bit_count` bits.
///
/// Special cases `bit_count == 0` to return 1. Zero-bit integers can only store the value zero
/// and this big integer implementation stores zero using one limb.
pub fn calcTwosCompLimbCount(bit_count: usize) usize {
    return @max(std.math.divCeil(usize, bit_count, @bitSizeOf(Limb)) catch unreachable, 1);
}

/// a + b * c + *carry, sets carry to the overflow bits
pub fn addMulLimbWithCarry(a: Limb, b: Limb, c: Limb, carry: *Limb) Limb {
    // ov1[0] = a + *carry
    const ov1 = @addWithOverflow(a, carry.*);

    // r2 = b * c
    const bc = @as(DoubleLimb, math.mulWide(Limb, b, c));
    const r2 = @as(Limb, @truncate(bc));
    const c2 = @as(Limb, @truncate(bc >> limb_bits));

    // ov2[0] = ov1[0] + r2
    const ov2 = @addWithOverflow(ov1[0], r2);

    // This never overflows, c1, c3 are either 0 or 1 and if both are 1 then
    // c2 is at least <= maxInt(Limb) - 2.
    carry.* = ov1[1] + c2 + ov2[1];

    return ov2[0];
}

/// a - b * c - *carry, sets carry to the overflow bits
fn subMulLimbWithBorrow(a: Limb, b: Limb, c: Limb, carry: *Limb) Limb {
    // ov1[0] = a - *carry
    const ov1 = @subWithOverflow(a, carry.*);

    // r2 = b * c
    const bc = @as(DoubleLimb, std.math.mulWide(Limb, b, c));
    const r2 = @as(Limb, @truncate(bc));
    const c2 = @as(Limb, @truncate(bc >> limb_bits));

    // ov2[0] = ov1[0] - r2
    const ov2 = @subWithOverflow(ov1[0], r2);
    carry.* = ov1[1] + c2 + ov2[1];

    return ov2[0];
}

/// Used to indicate either limit of a 2s-complement integer.
pub const TwosCompIntLimit = enum {
    // The low limit, either 0x00 (unsigned) or (-)0x80 (signed) for an 8-bit integer.
    min,

    // The high limit, either 0xFF (unsigned) or 0x7F (signed) for an 8-bit integer.
    max,
};

pub const Round = enum {
    /// Round to the nearest representable value, with ties broken by the representation
    /// that ends with a 0 bit.
    nearest_even,
    /// Round away from zero.
    away,
    /// Round towards zero.
    trunc,
    /// Round towards negative infinity.
    floor,
    /// Round towards positive infinity.
    ceil,
};

pub const Exactness = enum { inexact, exact };

/// A arbitrary-precision big integer, with a fixed set of mutable limbs.
pub const Mutable = struct {
    /// Raw digits. These are:
    ///
    /// * Little-endian ordered
    /// * limbs.len >= 1
    /// * Zero is represented as limbs.len == 1 with limbs[0] == 0.
    ///
    /// Accessing limbs directly should be avoided.
    /// These are allocated limbs; the `len` field tells the valid range.
    limbs: []Limb,
    len: usize,
    positive: bool,

    pub fn toConst(self: Mutable) Const {
        return .{
            .limbs = self.limbs[0..self.len],
            .positive = self.positive,
        };
    }

    pub const ConvertError = Const.ConvertError;

    /// Convert `self` to `Int`.
    ///
    /// Returns an error if self cannot be narrowed into the requested type without truncation.
    pub fn toInt(self: Mutable, comptime Int: type) ConvertError!Int {
        return self.toConst().toInt(Int);
    }

    /// Convert `self` to `Float`.
    pub fn toFloat(self: Mutable, comptime Float: type, round: Round) struct { Float, Exactness } {
        return self.toConst().toFloat(Float, round);
    }

    /// Returns true if `a == 0`.
    pub fn eqlZero(self: Mutable) bool {
        return self.toConst().eqlZero();
    }

    /// Asserts that the allocator owns the limbs memory. If this is not the case,
    /// use `toConst().toManaged()`.
    pub fn toManaged(self: Mutable, allocator: Allocator) Managed {
        return .{
            .allocator = allocator,
            .limbs = self.limbs,
            .metadata = if (self.positive)
                self.len & ~Managed.sign_bit
            else
                self.len | Managed.sign_bit,
        };
    }

    /// `value` is a primitive integer type.
    /// Asserts the value fits within the provided `limbs_buffer`.
    /// Note: `calcLimbLen` can be used to figure out how big an array to allocate for `limbs_buffer`.
    pub fn init(limbs_buffer: []Limb, value: anytype) Mutable {
        limbs_buffer[0] = 0;
        var self: Mutable = .{
            .limbs = limbs_buffer,
            .len = 1,
            .positive = true,
        };
        self.set(value);
        return self;
    }

    /// Copies the value of a Const to an existing Mutable so that they both have the same value.
    /// Asserts the value fits in the limbs buffer.
    pub fn copy(self: *Mutable, other: Const) void {
        if (self.limbs.ptr != other.limbs.ptr) {
            @memcpy(self.limbs[0..other.limbs.len], other.limbs[0..other.limbs.len]);
        }
        // Normalize before setting `positive` so the `eqlZero` doesn't need to iterate
        // over the extra zero limbs.
        self.normalize(other.limbs.len);
        self.positive = other.positive or other.eqlZero();
    }

    /// Efficiently swap an Mutable with another. This swaps the limb pointers and a full copy is not
    /// performed. The address of the limbs field will not be the same after this function.
    pub fn swap(self: *Mutable, other: *Mutable) void {
        mem.swap(Mutable, self, other);
    }

    pub fn dump(self: Mutable) void {
        for (self.limbs[0..self.len]) |limb| {
            std.debug.print("{x} ", .{limb});
        }
        std.debug.print("len={} capacity={} positive={}\n", .{ self.len, self.limbs.len, self.positive });
    }

    /// Clones an Mutable and returns a new Mutable with the same value. The new Mutable is a deep copy and
    /// can be modified separately from the original.
    /// Asserts that limbs is big enough to store the value.
    pub fn clone(other: Mutable, limbs: []Limb) Mutable {
        @memcpy(limbs[0..other.len], other.limbs[0..other.len]);
        return .{
            .limbs = limbs,
            .len = other.len,
            .positive = other.positive,
        };
    }

    pub fn negate(self: *Mutable) void {
        self.positive = !self.positive;
    }

    /// Modify to become the absolute value
    pub fn abs(self: *Mutable) void {
        self.positive = true;
    }

    /// Sets the Mutable to value. Value must be an primitive integer type.
    /// Asserts the value fits within the limbs buffer.
    /// Note: `calcLimbLen` can be used to figure out how big the limbs buffer
    /// needs to be to store a specific value.
    pub fn set(self: *Mutable, value: anytype) void {
        const T = @TypeOf(value);
        const needed_limbs = calcLimbLen(value);
        assert(needed_limbs <= self.limbs.len); // value too big

        self.len = needed_limbs;
        self.positive = value >= 0;

        switch (@typeInfo(T)) {
            .int => |info| {
                var w_value = @abs(value);

                if (info.bits <= limb_bits) {
                    self.limbs[0] = w_value;
                } else {
                    var i: usize = 0;
                    while (true) : (i += 1) {
                        self.limbs[i] = @as(Limb, @truncate(w_value));
                        w_value >>= limb_bits;

                        if (w_value == 0) break;
                    }
                }
            },
            .comptime_int => {
                comptime var w_value = @abs(value);

                if (w_value <= maxInt(Limb)) {
                    self.limbs[0] = w_value;
                } else {
                    const mask = (1 << limb_bits) - 1;

                    comptime var i = 0;
                    inline while (true) : (i += 1) {
                        self.limbs[i] = w_value & mask;
                        w_value >>= limb_bits;

                        if (w_value == 0) break;
                    }
                }
            },
            else => @compileError("cannot set Mutable using type " ++ @typeName(T)),
        }
    }

    /// Set self from the string representation `value`.
    ///
    /// `value` must contain only digits <= `base` and is case insensitive.  Base prefixes are
    /// not allowed (e.g. 0x43 should simply be 43).  Underscores in the input string are
    /// ignored and can be used as digit separators.
    ///
    /// There must be enough memory for the value in `self.limbs`. An upper bound on number of limbs can
    /// be determined with `calcSetStringLimbCount`.
    /// Asserts the base is in the range [2, 36].
    ///
    /// Returns an error if the value has invalid digits for the requested base.
    pub fn setString(
        self: *Mutable,
        base: u8,
        value: []const u8,
    ) error{InvalidCharacter}!void {
        assert(base >= 2);
        assert(base <= 36);

        var i: usize = 0;
        var positive = true;
        if (value.len > 0 and value[0] == '-') {
            positive = false;
            i += 1;
        }

        @memset(self.limbs, 0);
        self.len = 1;

        var limb: Limb = 0;
        var j: usize = 0;
        for (value[i..]) |ch| {
            if (ch == '_') {
                continue;
            }
            const d = try std.fmt.charToDigit(ch, base);
            limb *= base;
            limb += d;
            j += 1;

            if (j == constants.digits_per_limb[base]) {
                const len = @min(self.len + 1, self.limbs.len);
                // r = a * b = a + a * (b - 1)
                // we assert when self.limbs is not large enough to store the number
                assert(!llmulLimb(.add, self.limbs[0..len], self.limbs[0..len], constants.big_bases[base] - 1));
                assert(lladdcarry(self.limbs[0..len], self.limbs[0..len], &[1]Limb{limb}) == 0);

                if (self.limbs.len > self.len and self.limbs[self.len] != 0)
                    self.len += 1;
                j = 0;
                limb = 0;
            }
        }
        if (j > 0) {
            const len = @min(self.len + 1, self.limbs.len);
            // we assert when self.limbs is not large enough to store the number
            assert(!llmulLimb(.add, self.limbs[0..len], self.limbs[0..len], math.pow(Limb, base, j) - 1));
            assert(lladdcarry(self.limbs[0..len], self.limbs[0..len], &[1]Limb{limb}) == 0);

            if (self.limbs.len > self.len and self.limbs[self.len] != 0)
                self.len += 1;
        }
        self.positive = positive;
    }

    /// Set self to either bound of a 2s-complement integer.
    /// Note: The result is still sign-magnitude, not twos complement! In order to convert the
    /// result to twos complement, it is sufficient to take the absolute value.
    ///
    /// Asserts the result fits in `r`. An upper bound on the number of limbs needed by
    /// r is `calcTwosCompLimbCount(bit_count)`.
    pub fn setTwosCompIntLimit(
        r: *Mutable,
        limit: TwosCompIntLimit,
        signedness: Signedness,
        bit_count: usize,
    ) void {
        // Handle zero-bit types.
        if (bit_count == 0) {
            r.set(0);
            return;
        }

        const req_limbs = calcTwosCompLimbCount(bit_count);
        const bit: Log2Limb = @truncate(bit_count - 1);
        const signmask = @as(Limb, 1) << bit; // 0b0..010..0 where 1 is the sign bit.
        const mask = (signmask << 1) -% 1; // 0b0..011..1 where the leftmost 1 is the sign bit.

        r.positive = true;

        switch (signedness) {
            .signed => switch (limit) {
                .min => {
                    // Negative bound, signed = -0x80.
                    r.len = req_limbs;
                    @memset(r.limbs[0 .. r.len - 1], 0);
                    r.limbs[r.len - 1] = signmask;
                    r.positive = false;
                },
                .max => {
                    // Positive bound, signed = 0x7F
                    // Note, in this branch we need to normalize because the first bit is
                    // supposed to be 0.

                    // Special case for 1-bit integers.
                    if (bit_count == 1) {
                        r.set(0);
                    } else {
                        const new_req_limbs = calcTwosCompLimbCount(bit_count - 1);
                        const msb = @as(Log2Limb, @truncate(bit_count - 2));
                        const new_signmask = @as(Limb, 1) << msb; // 0b0..010..0 where 1 is the sign bit.
                        const new_mask = (new_signmask << 1) -% 1; // 0b0..001..1 where the rightmost 0 is the sign bit.

                        r.len = new_req_limbs;
                        @memset(r.limbs[0 .. r.len - 1], maxInt(Limb));
                        r.limbs[r.len - 1] = new_mask;
                    }
                },
            },
            .unsigned => switch (limit) {
                .min => {
                    // Min bound, unsigned = 0x00
                    r.set(0);
                },
                .max => {
                    // Max bound, unsigned = 0xFF
                    r.len = req_limbs;
                    @memset(r.limbs[0 .. r.len - 1], maxInt(Limb));
                    r.limbs[r.len - 1] = mask;
                },
            },
        }
    }

    /// Sets the Mutable to a float value rounded according to `round`.
    /// Returns whether the conversion was exact (`round` had no effect on the result).
    pub fn setFloat(self: *Mutable, value: anytype, round: Round) Exactness {
        const Float = @TypeOf(value);
        if (Float == comptime_float) return self.setFloat(@as(f128, value), round);
        const abs_value = @abs(value);
        if (abs_value < 1.0) {
            if (abs_value == 0.0) {
                self.set(0);
                return .exact;
            }
            self.set(@as(i2, round: switch (round) {
                .nearest_even => if (abs_value <= 0.5) 0 else continue :round .away,
                .away => if (value < 0.0) -1 else 1,
                .trunc => 0,
                .floor => -@as(i2, @intFromBool(value < 0.0)),
                .ceil => @intFromBool(value > 0.0),
            }));
            return .inexact;
        }
        const Repr = std.math.FloatRepr(Float);
        const repr: Repr = @bitCast(value);
        const exponent = repr.exponent.unbias();
        assert(exponent >= 0);
        const int_bit: Repr.Mantissa = 1 << (@bitSizeOf(Repr.Mantissa) - 1);
        const mantissa = int_bit | repr.mantissa;
        if (exponent >= @bitSizeOf(Repr.Normalized.Fraction)) {
            self.set(mantissa);
            self.shiftLeft(self.toConst(), @intCast(exponent - @bitSizeOf(Repr.Normalized.Fraction)));
            self.positive = repr.sign == .positive;
            return .exact;
        }
        self.set(mantissa >> @intCast(@bitSizeOf(Repr.Normalized.Fraction) - exponent));
        const round_bits: Repr.Normalized.Fraction = @truncate(mantissa << @intCast(exponent));
        if (round_bits == 0) {
            self.positive = repr.sign == .positive;
            return .exact;
        }
        round: switch (round) {
            .nearest_even => {
                const half: Repr.Normalized.Fraction = 1 << (@bitSizeOf(Repr.Normalized.Fraction) - 1);
                if (round_bits >= half) self.addScalar(self.toConst(), 1);
                if (round_bits == half) self.limbs[0] &= ~@as(Limb, 1);
            },
            .away => self.addScalar(self.toConst(), 1),
            .trunc => {},
            .floor => switch (repr.sign) {
                .positive => {},
                .negative => continue :round .away,
            },
            .ceil => switch (repr.sign) {
                .positive => continue :round .away,
                .negative => {},
            },
        }
        self.positive = repr.sign == .positive;
        return .inexact;
    }

    /// r = a + scalar
    ///
    /// r and a may be aliases.
    /// scalar is a primitive integer type.
    ///
    /// Asserts the result fits in `r`. An upper bound on the number of limbs needed by
    /// r is `@max(a.limbs.len, calcLimbLen(scalar)) + 1`.
    pub fn addScalar(r: *Mutable, a: Const, scalar: anytype) void {
        // Normally we could just determine the number of limbs needed with calcLimbLen,
        // but that is not comptime-known when scalar is not a comptime_int.  Instead, we
        // use calcTwosCompLimbCount for a non-comptime_int scalar, which can be pessimistic
        // in the case that scalar happens to be small in magnitude within its type, but it
        // is well worth being able to use the stack and not needing an allocator passed in.
        // Note that Mutable.init still sets len to calcLimbLen(scalar) in any case.
        const limbs_len = comptime switch (@typeInfo(@TypeOf(scalar))) {
            .comptime_int => calcLimbLen(scalar),
            .int => |info| calcTwosCompLimbCount(info.bits),
            else => @compileError("expected scalar to be an int"),
        };
        var limbs: [limbs_len]Limb = undefined;
        const operand = init(&limbs, scalar).toConst();
        return add(r, a, operand);
    }

    /// Base implementation for addition. Adds `@max(a.limbs.len, b.limbs.len)` elements from a and b,
    /// and returns whether any overflow occurred.
    /// r, a and b may be aliases.
    ///
    /// Asserts r has enough elements to hold the result. The upper bound is `@max(a.limbs.len, b.limbs.len)`.
    fn addCarry(r: *Mutable, a: Const, b: Const) bool {
        if (a.eqlZero()) {
            r.copy(b);
            return false;
        } else if (b.eqlZero()) {
            r.copy(a);
            return false;
        } else if (a.positive != b.positive) {
            if (a.positive) {
                // (a) + (-b) => a - b
                return r.subCarry(a, b.abs());
            } else {
                // (-a) + (b) => b - a
                return r.subCarry(b, a.abs());
            }
        } else {
            r.positive = a.positive;
            if (a.limbs.len >= b.limbs.len) {
                const c = lladdcarry(r.limbs, a.limbs, b.limbs);
                r.normalize(a.limbs.len);
                return c != 0;
            } else {
                const c = lladdcarry(r.limbs, b.limbs, a.limbs);
                r.normalize(b.limbs.len);
                return c != 0;
            }
        }
    }

    /// r = a + b
    ///
    /// r, a and b may be aliases.
    ///
    /// Asserts the result fits in `r`. An upper bound on the number of limbs needed by
    /// r is `@max(a.limbs.len, b.limbs.len) + 1`.
    pub fn add(r: *Mutable, a: Const, b: Const) void {
        if (r.addCarry(a, b)) {
            // Fix up the result. Note that addCarry normalizes by a.limbs.len or b.limbs.len,
            // so we need to set the length here.
            const msl = @max(a.limbs.len, b.limbs.len);
            // `[add|sub]Carry` normalizes by `msl`, so we need to fix up the result manually here.
            // Note, the fact that it normalized means that the intermediary limbs are zero here.
            r.len = msl + 1;
            r.limbs[msl] = 1; // If this panics, there wasn't enough space in `r`.
        }
    }

    /// r = a + b with 2s-complement wrapping semantics. Returns whether overflow occurred.
    /// r, a and b may be aliases
    ///
    /// Asserts the result fits in `r`. An upper bound on the number of limbs needed by
    /// r is `calcTwosCompLimbCount(bit_count)`.
    pub fn addWrap(r: *Mutable, a: Const, b: Const, signedness: Signedness, bit_count: usize) bool {
        const req_limbs = calcTwosCompLimbCount(bit_count);

        // Slice of the upper bits if they exist, these will be ignored and allows us to use addCarry to determine
        // if an overflow occurred.
        const x: Const = .{
            .positive = a.positive,
            .limbs = a.limbs[0..@min(req_limbs, a.limbs.len)],
        };

        const y: Const = .{
            .positive = b.positive,
            .limbs = b.limbs[0..@min(req_limbs, b.limbs.len)],
        };

        var carry_truncated = false;
        if (r.addCarry(x, y)) {
            // There are two possibilities here:
            // - We overflowed req_limbs. In this case, the carry is ignored, as it would be removed by
            //   truncate anyway.
            // - a and b had less elements than req_limbs, and those were overflowed. This case needs to be handled.
            //   Note: after this we still might need to wrap.
            const msl = @max(a.limbs.len, b.limbs.len);
            if (msl < req_limbs) {
                r.len = msl + 1;
                r.limbs[msl] = 1;
            } else {
                carry_truncated = true;
            }
        }

        if (!r.toConst().fitsInTwosComp(signedness, bit_count)) {
            r.truncate(r.toConst(), signedness, bit_count);
            return true;
        }

        return carry_truncated;
    }

    /// r = a + b with 2s-complement saturating semantics.
    /// r, a and b may be aliases.
    ///
    /// Assets the result fits in `r`. Upper bound on the number of limbs needed by
    /// r is `calcTwosCompLimbCount(bit_count)`.
    pub fn addSat(r: *Mutable, a: Const, b: Const, signedness: Signedness, bit_count: usize) void {
        const req_limbs = calcTwosCompLimbCount(bit_count);

        // Slice of the upper bits if they exist, these will be ignored and allows us to use addCarry to determine
        // if an overflow occurred.
        const x: Const = .{
            .positive = a.positive,
            .limbs = a.limbs[0..@min(req_limbs, a.limbs.len)],
        };

        const y: Const = .{
            .positive = b.positive,
            .limbs = b.limbs[0..@min(req_limbs, b.limbs.len)],
        };

        if (r.addCarry(x, y)) {
            // There are two possibilities here:
            // - We overflowed req_limbs, in which case we need to saturate.
            // - a and b had less elements than req_limbs, and those were overflowed.
            //   Note: In this case, might _also_ need to saturate.
            const msl = @max(a.limbs.len, b.limbs.len);
            if (msl < req_limbs) {
                r.len = msl + 1;
                r.limbs[msl] = 1;
                // Note: Saturation may still be required if msl == req_limbs - 1
            } else {
                // Overflowed req_limbs, definitely saturate.
                return r.setTwosCompIntLimit(if (r.positive) .max else .min, signedness, bit_count);
            }
        }

        // Saturate if the result didn't fit.
        r.saturate(r.toConst(), signedness, bit_count);
    }

    /// Base implementation for subtraction. Subtracts `@max(a.limbs.len, b.limbs.len)` elements from a and b,
    /// and returns whether any overflow occurred.
    /// r, a and b may be aliases.
    ///
    /// Asserts r has enough elements to hold the result. The upper bound is `@max(a.limbs.len, b.limbs.len)`.
    fn subCarry(r: *Mutable, a: Const, b: Const) bool {
        if (a.eqlZero()) {
            r.copy(b);
            r.positive = !b.positive;
            return false;
        } else if (b.eqlZero()) {
            r.copy(a);
            return false;
        } else if (a.positive != b.positive) {
            if (a.positive) {
                // (a) - (-b) => a + b
                return r.addCarry(a, b.abs());
            } else {
                // (-a) - (b) => -a + -b
                return r.addCarry(a, b.negate());
            }
        } else if (a.positive) {
            if (a.order(b) != .lt) {
                // (a) - (b) => a - b
                const c = llsubcarry(r.limbs, a.limbs, b.limbs);
                r.normalize(a.limbs.len);
                r.positive = true;
                return c != 0;
            } else {
                // (a) - (b) => -b + a => -(b - a)
                const c = llsubcarry(r.limbs, b.limbs, a.limbs);
                r.normalize(b.limbs.len);
                r.positive = false;
                return c != 0;
            }
        } else {
            if (a.order(b) == .lt) {
                // (-a) - (-b) => -(a - b)
                const c = llsubcarry(r.limbs, a.limbs, b.limbs);
                r.normalize(a.limbs.len);
                r.positive = false;
                return c != 0;
            } else {
                // (-a) - (-b) => --b + -a => b - a
                const c = llsubcarry(r.limbs, b.limbs, a.limbs);
                r.normalize(b.limbs.len);
                r.positive = true;
                return c != 0;
            }
        }
    }

    /// r = a - b
    ///
    /// r, a and b may be aliases.
    ///
    /// Asserts the result fits in `r`. An upper bound on the number of limbs needed by
    /// r is `@max(a.limbs.len, b.limbs.len) + 1`. The +1 is not needed if both operands are positive.
    pub fn sub(r: *Mutable, a: Const, b: Const) void {
        r.add(a, b.negate());
    }

    /// r = a - b with 2s-complement wrapping semantics. Returns whether any overflow occurred.
    ///
    /// r, a and b may be aliases
    /// Asserts the result fits in `r`. An upper bound on the number of limbs needed by
    /// r is `calcTwosCompLimbCount(bit_count)`.
    pub fn subWrap(r: *Mutable, a: Const, b: Const, signedness: Signedness, bit_count: usize) bool {
        return r.addWrap(a, b.negate(), signedness, bit_count);
    }

    /// r = a - b with 2s-complement saturating semantics.
    /// r, a and b may be aliases.
    ///
    /// Assets the result fits in `r`. Upper bound on the number of limbs needed by
    /// r is `calcTwosCompLimbCount(bit_count)`.
    pub fn subSat(r: *Mutable, a: Const, b: Const, signedness: Signedness, bit_count: usize) void {
        r.addSat(a, b.negate(), signedness, bit_count);
    }

    /// rma = a * b
    ///
    /// `rma` may alias with `a` or `b`.
    /// `a` and `b` may alias with each other.
    ///
    /// Asserts the result fits in `rma`. An upper bound on the number of limbs needed by
    /// rma is given by `a.limbs.len + b.limbs.len`.
    ///
    /// `limbs_buffer` is used for temporary storage. The amount required is given by `calcMulLimbsBufferLen`.
    pub fn mul(rma: *Mutable, a: Const, b: Const, limbs_buffer: []Limb, allocator: ?Allocator) void {
        var buf_index: usize = 0;

        const a_copy = if (rma.limbs.ptr == a.limbs.ptr) blk: {
            const start = buf_index;
            @memcpy(limbs_buffer[buf_index..][0..a.limbs.len], a.limbs);
            buf_index += a.limbs.len;
            break :blk a.toMutable(limbs_buffer[start..buf_index]).toConst();
        } else a;

        const b_copy = if (rma.limbs.ptr == b.limbs.ptr) blk: {
            const start = buf_index;
            @memcpy(limbs_buffer[buf_index..][0..b.limbs.len], b.limbs);
            buf_index += b.limbs.len;
            break :blk b.toMutable(limbs_buffer[start..buf_index]).toConst();
        } else b;

        return rma.mulNoAlias(a_copy, b_copy, allocator);
    }

    /// rma = a * b
    ///
    /// `rma` may not alias with `a` or `b`.
    /// `a` and `b` may alias with each other.
    ///
    /// Asserts the result fits in `rma`. An upper bound on the number of limbs needed by
    /// rma is given by `a.limbs.len + b.limbs.len`.
    ///
    /// If `allocator` is provided, it will be used for temporary storage to improve
    /// multiplication performance. `error.OutOfMemory` is handled with a fallback algorithm.
    pub fn mulNoAlias(rma: *Mutable, a: Const, b: Const, allocator: ?Allocator) void {
        assert(rma.limbs.ptr != a.limbs.ptr); // illegal aliasing
        assert(rma.limbs.ptr != b.limbs.ptr); // illegal aliasing

        if (a.limbs.len == 1 and b.limbs.len == 1) {
            rma.limbs[0], const overflow_bit = @mulWithOverflow(a.limbs[0], b.limbs[0]);
            if (overflow_bit == 0) {
                rma.len = 1;
                rma.positive = (a.positive == b.positive) or rma.limbs[0] == 0;
                return;
            }
        }

        @memset(rma.limbs[0 .. a.limbs.len + b.limbs.len], 0);

        llmulacc(.add, allocator, rma.limbs, a.limbs, b.limbs);

        rma.normalize(a.limbs.len + b.limbs.len);
        rma.positive = (a.positive == b.positive);
    }

    /// rma = a * b with 2s-complement wrapping semantics.
    ///
    /// `rma` may alias with `a` or `b`.
    /// `a` and `b` may alias with each other.
    ///
    /// Asserts the result fits in `rma`. An upper bound on the number of limbs needed by
    /// rma is given by `a.limbs.len + b.limbs.len`.
    ///
    /// `limbs_buffer` is used for temporary storage. The amount required is given by `calcMulWrapLimbsBufferLen`.
    pub fn mulWrap(
        rma: *Mutable,
        a: Const,
        b: Const,
        signedness: Signedness,
        bit_count: usize,
        limbs_buffer: []Limb,
        allocator: ?Allocator,
    ) void {
        var buf_index: usize = 0;
        const req_limbs = calcTwosCompLimbCount(bit_count);

        const a_copy = if (rma.limbs.ptr == a.limbs.ptr) blk: {
            const start = buf_index;
            const a_len = @min(req_limbs, a.limbs.len);
            @memcpy(limbs_buffer[buf_index..][0..a_len], a.limbs[0..a_len]);
            buf_index += a_len;
            break :blk a.toMutable(limbs_buffer[start..buf_index]).toConst();
        } else a;

        const b_copy = if (rma.limbs.ptr == b.limbs.ptr) blk: {
            const start = buf_index;
            const b_len = @min(req_limbs, b.limbs.len);
            @memcpy(limbs_buffer[buf_index..][0..b_len], b.limbs[0..b_len]);
            buf_index += b_len;
            break :blk a.toMutable(limbs_buffer[start..buf_index]).toConst();
        } else b;

        return rma.mulWrapNoAlias(a_copy, b_copy, signedness, bit_count, allocator);
    }

    /// rma = a * b with 2s-complement wrapping semantics.
    ///
    /// `rma` may not alias with `a` or `b`.
    /// `a` and `b` may alias with each other.
    ///
    /// Asserts the result fits in `rma`. An upper bound on the number of limbs needed by
    /// rma is given by `a.limbs.len + b.limbs.len`.
    ///
    /// If `allocator` is provided, it will be used for temporary storage to improve
    /// multiplication performance. `error.OutOfMemory` is handled with a fallback algorithm.
    pub fn mulWrapNoAlias(
        rma: *Mutable,
        a: Const,
        b: Const,
        signedness: Signedness,
        bit_count: usize,
        allocator: ?Allocator,
    ) void {
        assert(rma.limbs.ptr != a.limbs.ptr); // illegal aliasing
        assert(rma.limbs.ptr != b.limbs.ptr); // illegal aliasing

        const req_limbs = calcTwosCompLimbCount(bit_count);

        // We can ignore the upper bits here, those results will be discarded anyway.
        const a_limbs = a.limbs[0..@min(req_limbs, a.limbs.len)];
        const b_limbs = b.limbs[0..@min(req_limbs, b.limbs.len)];

        @memset(rma.limbs[0..req_limbs], 0);

        llmulacc(.add, allocator, rma.limbs, a_limbs, b_limbs);
        rma.normalize(@min(req_limbs, a.limbs.len + b.limbs.len));
        rma.positive = (a.positive == b.positive);
        rma.truncate(rma.toConst(), signedness, bit_count);
    }

    /// r = @bitReverse(a) with 2s-complement semantics.
    /// r and a may be aliases.
    ///
    /// Asserts the result fits in `r`. Upper bound on the number of limbs needed by
    /// r is `calcTwosCompLimbCount(bit_count)`.
    pub fn bitReverse(r: *Mutable, a: Const, signedness: Signedness, bit_count: usize) void {
        if (bit_count == 0) {
            r.limbs[0] = 0;
            r.len = 1;
            r.positive = true;
            return;
        }

        r.copy(a);

        const limbs_required = calcTwosCompLimbCount(bit_count);

        if (!a.positive) {
            r.positive = true; // Negate.
            r.bitNotWrap(r.toConst(), .unsigned, bit_count); // Bitwise NOT.
            r.addScalar(r.toConst(), 1); // Add one.
        } else if (limbs_required > a.limbs.len) {
            // Zero-extend to our output length
            for (r.limbs[a.limbs.len..limbs_required]) |*limb| {
                limb.* = 0;
            }
            r.len = limbs_required;
        }

        // 0b0..01..1000 with @log2(@sizeOf(Limb)) consecutive ones
        const endian_mask: usize = (@sizeOf(Limb) - 1) << 3;

        const bytes = std.mem.sliceAsBytes(r.limbs);

        var k: usize = 0;
        while (k < ((bit_count + 1) / 2)) : (k += 1) {
            var i = k;
            var rev_i = bit_count - i - 1;

    
```
