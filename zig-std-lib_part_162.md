```
alStrings(expected, actual);
}

/// This function is intended to be used only in tests. When the actual value is
/// not approximately equal to the expected value, prints diagnostics to stderr
/// to show exactly how they are not equal, then returns a test failure error.
/// See `math.approxEqAbs` for more information on the tolerance parameter.
/// The types must be floating-point.
/// `actual` and `expected` are coerced to a common type using peer type resolution.
pub inline fn expectApproxEqAbs(expected: anytype, actual: anytype, tolerance: anytype) !void {
    const T = @TypeOf(expected, actual, tolerance);
    return expectApproxEqAbsInner(T, expected, actual, tolerance);
}

fn expectApproxEqAbsInner(comptime T: type, expected: T, actual: T, tolerance: T) !void {
    switch (@typeInfo(T)) {
        .float => if (!math.approxEqAbs(T, expected, actual, tolerance)) {
            print("actual {}, not within absolute tolerance {} of expected {}\n", .{ actual, tolerance, expected });
            return error.TestExpectedApproxEqAbs;
        },

        .comptime_float => @compileError("Cannot approximately compare two comptime_float values"),

        else => @compileError("Unable to compare non floating point values"),
    }
}

test expectApproxEqAbs {
    inline for ([_]type{ f16, f32, f64, f128 }) |T| {
        const pos_x: T = 12.0;
        const pos_y: T = 12.06;
        const neg_x: T = -12.0;
        const neg_y: T = -12.06;

        try expectApproxEqAbs(pos_x, pos_y, 0.1);
        try expectApproxEqAbs(neg_x, neg_y, 0.1);
    }
}

/// This function is intended to be used only in tests. When the actual value is
/// not approximately equal to the expected value, prints diagnostics to stderr
/// to show exactly how they are not equal, then returns a test failure error.
/// See `math.approxEqRel` for more information on the tolerance parameter.
/// The types must be floating-point.
/// `actual` and `expected` are coerced to a common type using peer type resolution.
pub inline fn expectApproxEqRel(expected: anytype, actual: anytype, tolerance: anytype) !void {
    const T = @TypeOf(expected, actual, tolerance);
    return expectApproxEqRelInner(T, expected, actual, tolerance);
}

fn expectApproxEqRelInner(comptime T: type, expected: T, actual: T, tolerance: T) !void {
    switch (@typeInfo(T)) {
        .float => if (!math.approxEqRel(T, expected, actual, tolerance)) {
            print("actual {}, not within relative tolerance {} of expected {}\n", .{ actual, tolerance, expected });
            return error.TestExpectedApproxEqRel;
        },

        .comptime_float => @compileError("Cannot approximately compare two comptime_float values"),

        else => @compileError("Unable to compare non floating point values"),
    }
}

test expectApproxEqRel {
    inline for ([_]type{ f16, f32, f64, f128 }) |T| {
        const eps_value = comptime math.floatEps(T);
        const sqrt_eps_value = comptime @sqrt(eps_value);

        const pos_x: T = 12.0;
        const pos_y: T = pos_x + 2 * eps_value;
        const neg_x: T = -12.0;
        const neg_y: T = neg_x - 2 * eps_value;

        try expectApproxEqRel(pos_x, pos_y, sqrt_eps_value);
        try expectApproxEqRel(neg_x, neg_y, sqrt_eps_value);
    }
}

/// This function is intended to be used only in tests. When the two slices are
/// not equal, prints diagnostics to stderr to show exactly how they are not
/// equal (with the differences highlighted in red), then returns a test
/// failure error.
pub fn expectEqualSlices(comptime T: type, expected: []const T, actual: []const T) !void {
    const diff_index: usize = diff_index: {
        const shortest = @min(expected.len, actual.len);
        var index: usize = 0;
        while (index < shortest) : (index += 1) {
            if (!std.meta.eql(actual[index], expected[index])) break :diff_index index;
        }
        break :diff_index if (expected.len == actual.len) return else shortest;
    };
    if (!backend_can_print) return error.TestExpectedEqual;
    // Intentionally using the debug Io instance rather than the testing Io instance.
    const stderr = std.debug.lockStderr(&.{});
    defer std.debug.unlockStderr();
    const w = &stderr.file_writer.interface;
    failEqualSlices(T, expected, actual, diff_index, w, stderr.terminal_mode) catch {};
    return error.TestExpectedEqual;
}

fn failEqualSlices(
    comptime T: type,
    expected: []const T,
    actual: []const T,
    diff_index: usize,
    w: *Io.Writer,
    terminal_mode: Io.Terminal.Mode,
) !void {
    try w.print("slices differ. first difference occurs at index {d} (0x{X})\n", .{ diff_index, diff_index });

    // TODO: Should this be configurable by the caller?
    const max_lines: usize = 16;
    const max_window_size: usize = if (T == u8) max_lines * 16 else max_lines;

    // Print a maximum of max_window_size items of each input, starting just before the
    // first difference to give a bit of context.
    var window_start: usize = 0;
    if (@max(actual.len, expected.len) > max_window_size) {
        const alignment = if (T == u8) 16 else 2;
        window_start = std.mem.alignBackward(usize, diff_index - @min(diff_index, alignment), alignment);
    }
    const expected_window = expected[window_start..@min(expected.len, window_start + max_window_size)];
    const expected_truncated = window_start + expected_window.len < expected.len;
    const actual_window = actual[window_start..@min(actual.len, window_start + max_window_size)];
    const actual_truncated = window_start + actual_window.len < actual.len;

    var differ = if (T == u8) BytesDiffer{
        .expected = expected_window,
        .actual = actual_window,
        .terminal_mode = terminal_mode,
    } else SliceDiffer(T){
        .start_index = window_start,
        .expected = expected_window,
        .actual = actual_window,
        .terminal_mode = terminal_mode,
    };

    // Print indexes as hex for slices of u8 since it's more likely to be binary data where
    // that is usually useful.
    const index_fmt = if (T == u8) "0x{X}" else "{}";

    try w.print("\n============ expected this output: =============  len: {} (0x{X})\n\n", .{ expected.len, expected.len });
    if (window_start > 0) {
        if (T == u8) {
            try w.print("... truncated, start index: " ++ index_fmt ++ " ...\n", .{window_start});
        } else {
            try w.print("... truncated ...\n", .{});
        }
    }
    differ.write(w) catch {};
    if (expected_truncated) {
        const end_offset = window_start + expected_window.len;
        const num_missing_items = expected.len - (window_start + expected_window.len);
        if (T == u8) {
            try w.print("... truncated, indexes [" ++ index_fmt ++ "..] not shown, remaining bytes: " ++ index_fmt ++ " ...\n", .{ end_offset, num_missing_items });
        } else {
            try w.print("... truncated, remaining items: " ++ index_fmt ++ " ...\n", .{num_missing_items});
        }
    }

    // now reverse expected/actual and print again
    differ.expected = actual_window;
    differ.actual = expected_window;
    try w.print("\n============= instead found this: ==============  len: {} (0x{X})\n\n", .{ actual.len, actual.len });
    if (window_start > 0) {
        if (T == u8) {
            try w.print("... truncated, start index: " ++ index_fmt ++ " ...\n", .{window_start});
        } else {
            try w.print("... truncated ...\n", .{});
        }
    }
    differ.write(w) catch {};
    if (actual_truncated) {
        const end_offset = window_start + actual_window.len;
        const num_missing_items = actual.len - (window_start + actual_window.len);
        if (T == u8) {
            try w.print("... truncated, indexes [" ++ index_fmt ++ "..] not shown, remaining bytes: " ++ index_fmt ++ " ...\n", .{ end_offset, num_missing_items });
        } else {
            try w.print("... truncated, remaining items: " ++ index_fmt ++ " ...\n", .{num_missing_items});
        }
    }
    try w.print("\n================================================\n\n", .{});

    return error.TestExpectedEqual;
}

fn SliceDiffer(comptime T: type) type {
    return struct {
        start_index: usize,
        expected: []const T,
        actual: []const T,
        terminal_mode: Io.Terminal.Mode,

        const Self = @This();

        pub fn write(self: Self, writer: *Io.Writer) !void {
            const t: Io.Terminal = .{ .writer = writer, .mode = self.terminal_mode };
            for (self.expected, 0..) |value, i| {
                const full_index = self.start_index + i;
                const diff = if (i < self.actual.len) !std.meta.eql(self.actual[i], value) else true;
                if (diff) try t.setColor(.red);
                if (@typeInfo(T) == .pointer) {
                    try writer.print("[{}]{*}: {any}\n", .{ full_index, value, value });
                } else {
                    try writer.print("[{}]: {any}\n", .{ full_index, value });
                }
                if (diff) try t.setColor(.reset);
            }
        }
    };
}

const BytesDiffer = struct {
    expected: []const u8,
    actual: []const u8,
    terminal_mode: Io.Terminal.Mode,

    pub fn write(self: BytesDiffer, writer: *Io.Writer) !void {
        var expected_iterator = std.mem.window(u8, self.expected, 16, 16);
        var row: usize = 0;
        while (expected_iterator.next()) |chunk| {
            // to avoid having to calculate diffs twice per chunk
            var diffs: std.bit_set.IntegerBitSet(16) = .{ .mask = 0 };
            for (chunk, 0..) |byte, col| {
                const absolute_byte_index = col + row * 16;
                const diff = if (absolute_byte_index < self.actual.len) self.actual[absolute_byte_index] != byte else true;
                if (diff) diffs.set(col);
                try self.writeDiff(writer, "{X:0>2} ", .{byte}, diff);
                if (col == 7) try writer.writeByte(' ');
            }
            try writer.writeByte(' ');
            if (chunk.len < 16) {
                var missing_columns = (16 - chunk.len) * 3;
                if (chunk.len < 8) missing_columns += 1;
                try writer.splatByteAll(' ', missing_columns);
            }
            for (chunk, 0..) |byte, col| {
                const diff = diffs.isSet(col);
                if (std.ascii.isPrint(byte)) {
                    try self.writeDiff(writer, "{c}", .{byte}, diff);
                } else {
                    // TODO: remove this `if` when https://github.com/ziglang/zig/issues/7600 is fixed
                    if (self.terminal_mode == .windows_api) {
                        try self.writeDiff(writer, ".", .{}, diff);
                        continue;
                    }

                    // Let's print some common control codes as graphical Unicode symbols.
                    // We don't want to do this for all control codes because most control codes apart from
                    // the ones that Zig has escape sequences for are likely not very useful to print as symbols.
                    switch (byte) {
                        '\n' => try self.writeDiff(writer, "␊", .{}, diff),
                        '\r' => try self.writeDiff(writer, "␍", .{}, diff),
                        '\t' => try self.writeDiff(writer, "␉", .{}, diff),
                        else => try self.writeDiff(writer, ".", .{}, diff),
                    }
                }
            }
            try writer.writeByte('\n');
            row += 1;
        }
    }

    fn terminal(self: *const BytesDiffer, writer: *Io.Writer) Io.Terminal {
        return .{ .writer = writer, .mode = self.terminal_mode };
    }

    fn writeDiff(self: BytesDiffer, writer: *Io.Writer, comptime fmt: []const u8, args: anytype, diff: bool) !void {
        if (diff) try self.terminal(writer).setColor(.red);
        try writer.print(fmt, args);
        if (diff) try self.terminal(writer).setColor(.reset);
    }
};

test {
    try expectEqualSlices(u8, "foo\x00", "foo\x00");
    try expectEqualSlices(u16, &[_]u16{ 100, 200, 300, 400 }, &[_]u16{ 100, 200, 300, 400 });
    const E = enum { foo, bar };
    const S = struct {
        v: E,
    };
    try expectEqualSlices(
        S,
        &[_]S{ .{ .v = .foo }, .{ .v = .bar }, .{ .v = .foo }, .{ .v = .bar } },
        &[_]S{ .{ .v = .foo }, .{ .v = .bar }, .{ .v = .foo }, .{ .v = .bar } },
    );
}

/// This function is intended to be used only in tests. Checks that two slices or two arrays are equal,
/// including that their sentinel (if any) are the same. Will error if given another type.
pub fn expectEqualSentinel(comptime T: type, comptime sentinel: T, expected: [:sentinel]const T, actual: [:sentinel]const T) !void {
    try expectEqualSlices(T, expected, actual);

    const expected_value_sentinel = blk: {
        switch (@typeInfo(@TypeOf(expected))) {
            .pointer => {
                break :blk expected[expected.len];
            },
            .array => |array_info| {
                const indexable_outside_of_bounds = @as([]const array_info.child, &expected);
                break :blk indexable_outside_of_bounds[indexable_outside_of_bounds.len];
            },
            else => {},
        }
    };

    const actual_value_sentinel = blk: {
        switch (@typeInfo(@TypeOf(actual))) {
            .pointer => {
                break :blk actual[actual.len];
            },
            .array => |array_info| {
                const indexable_outside_of_bounds = @as([]const array_info.child, &actual);
                break :blk indexable_outside_of_bounds[indexable_outside_of_bounds.len];
            },
            else => {},
        }
    };

    if (!std.meta.eql(sentinel, expected_value_sentinel)) {
        print("expectEqualSentinel: 'expected' sentinel in memory is different from its type sentinel. type sentinel {}, in memory sentinel {}\n", .{ sentinel, expected_value_sentinel });
        return error.TestExpectedEqual;
    }

    if (!std.meta.eql(sentinel, actual_value_sentinel)) {
        print("expectEqualSentinel: 'actual' sentinel in memory is different from its type sentinel. type sentinel {}, in memory sentinel {}\n", .{ sentinel, actual_value_sentinel });
        return error.TestExpectedEqual;
    }
}

/// This function is intended to be used only in tests.
/// When `ok` is false, returns a test failure error.
pub fn expect(ok: bool) !void {
    if (!ok) return error.TestUnexpectedResult;
}

pub const TmpDir = struct {
    dir: Io.Dir,
    parent_dir: Io.Dir,
    sub_path: [sub_path_len]u8,

    const random_bytes_count = 12;
    const sub_path_len = std.base64.url_safe.Encoder.calcSize(random_bytes_count);

    pub fn cleanup(self: *TmpDir) void {
        self.dir.close(io);
        self.parent_dir.deleteTree(io, &self.sub_path) catch {};
        self.parent_dir.close(io);
        self.* = undefined;
    }
};

pub fn tmpDir(opts: Io.Dir.OpenOptions) TmpDir {
    comptime assert(builtin.is_test);
    var random_bytes: [TmpDir.random_bytes_count]u8 = undefined;
    io.random(&random_bytes);
    var sub_path: [TmpDir.sub_path_len]u8 = undefined;
    _ = std.base64.url_safe.Encoder.encode(&sub_path, &random_bytes);

    const cwd = Io.Dir.cwd();
    var cache_dir = cwd.createDirPathOpen(io, ".zig-cache", .{}) catch
        @panic("unable to make tmp dir for testing: unable to make and open .zig-cache dir");
    defer cache_dir.close(io);
    const parent_dir = cache_dir.createDirPathOpen(io, "tmp", .{}) catch
        @panic("unable to make tmp dir for testing: unable to make and open .zig-cache/tmp dir");
    const dir = parent_dir.createDirPathOpen(io, &sub_path, .{ .open_options = opts }) catch
        @panic("unable to make tmp dir for testing: unable to make and open the tmp dir");

    return .{
        .dir = dir,
        .parent_dir = parent_dir,
        .sub_path = sub_path,
    };
}

pub fn expectEqualStrings(expected: []const u8, actual: []const u8) !void {
    if (std.mem.findDiff(u8, actual, expected)) |diff_index| {
        if (@inComptime()) {
            @compileError(std.fmt.comptimePrint("\nexpected:\n{s}\nfound:\n{s}\ndifference starts at index {d}", .{
                expected, actual, diff_index,
            }));
        }
        print("\n====== expected this output: =========\n", .{});
        printWithVisibleNewlines(expected);
        print("\n======== instead found this: =========\n", .{});
        printWithVisibleNewlines(actual);
        print("\n======================================\n", .{});

        var diff_line_number: usize = 1;
        for (expected[0..diff_index]) |value| {
            if (value == '\n') diff_line_number += 1;
        }
        print("First difference occurs on line {d}:\n", .{diff_line_number});

        print("expected:\n", .{});
        printIndicatorLine(expected, diff_index);

        print("found:\n", .{});
        printIndicatorLine(actual, diff_index);

        return error.TestExpectedEqual;
    }
}

pub fn expectStringStartsWith(actual: []const u8, expected_starts_with: []const u8) !void {
    if (std.mem.startsWith(u8, actual, expected_starts_with))
        return;

    const shortened_actual = if (actual.len >= expected_starts_with.len)
        actual[0..expected_starts_with.len]
    else
        actual;

    print("\n====== expected to start with: =========\n", .{});
    printWithVisibleNewlines(expected_starts_with);
    print("\n====== instead started with: ===========\n", .{});
    printWithVisibleNewlines(shortened_actual);
    print("\n========= full output: ==============\n", .{});
    printWithVisibleNewlines(actual);
    print("\n======================================\n", .{});

    return error.TestExpectedStartsWith;
}

pub fn expectStringEndsWith(actual: []const u8, expected_ends_with: []const u8) !void {
    if (std.mem.endsWith(u8, actual, expected_ends_with))
        return;

    const shortened_actual = if (actual.len >= expected_ends_with.len)
        actual[(actual.len - expected_ends_with.len)..]
    else
        actual;

    print("\n====== expected to end with: =========\n", .{});
    printWithVisibleNewlines(expected_ends_with);
    print("\n====== instead ended with: ===========\n", .{});
    printWithVisibleNewlines(shortened_actual);
    print("\n========= full output: ==============\n", .{});
    printWithVisibleNewlines(actual);
    print("\n======================================\n", .{});

    return error.TestExpectedEndsWith;
}

/// This function is intended to be used only in tests. When the two values are not
/// deeply equal, prints diagnostics to stderr to show exactly how they are not equal,
/// then returns a test failure error.
/// `actual` and `expected` are coerced to a common type using peer type resolution.
///
/// Deeply equal is defined as follows:
/// Primitive types are deeply equal if they are equal using `==` operator.
/// Struct values are deeply equal if their corresponding fields are deeply equal.
/// Container types(like Array/Slice/Vector) deeply equal when their corresponding elements are deeply equal.
/// Pointer values are deeply equal if values they point to are deeply equal.
///
/// Note: Self-referential structs are supported (e.g. things like std.SinglyLinkedList)
/// but may cause infinite recursion or stack overflow when a container has a pointer to itself.
pub inline fn expectEqualDeep(expected: anytype, actual: anytype) error{TestExpectedEqual}!void {
    const T = @TypeOf(expected, actual);
    return expectEqualDeepInner(T, expected, actual);
}

fn expectEqualDeepInner(comptime T: type, expected: T, actual: T) error{TestExpectedEqual}!void {
    switch (@typeInfo(@TypeOf(actual))) {
        .noreturn,
        .@"opaque",
        .frame,
        .@"anyframe",
        => @compileError("value of type " ++ @typeName(@TypeOf(actual)) ++ " encountered"),

        .undefined,
        .null,
        .void,
        => return,

        .type => {
            if (actual != expected) {
                print("expected type {s}, found type {s}\n", .{ @typeName(expected), @typeName(actual) });
                return error.TestExpectedEqual;
            }
        },

        .bool,
        .int,
        .float,
        .comptime_float,
        .comptime_int,
        .enum_literal,
        .@"enum",
        .@"fn",
        .error_set,
        => {
            if (actual != expected) {
                print("expected {any}, found {any}\n", .{ expected, actual });
                return error.TestExpectedEqual;
            }
        },

        .pointer => |pointer| {
            switch (pointer.size) {
                // We have no idea what is behind those pointers, so the best we can do is `==` check.
                .c, .many => {
                    if (actual != expected) {
                        print("expected {*}, found {*}\n", .{ expected, actual });
                        return error.TestExpectedEqual;
                    }
                },
                .one => {
                    // Length of those pointers are runtime value, so the best we can do is `==` check.
                    switch (@typeInfo(pointer.child)) {
                        .@"fn", .@"opaque" => {
                            if (actual != expected) {
                                print("expected {*}, found {*}\n", .{ expected, actual });
                                return error.TestExpectedEqual;
                            }
                        },
                        else => try expectEqualDeep(expected.*, actual.*),
                    }
                },
                .slice => {
                    if (expected.len != actual.len) {
                        print("Slice len not the same, expected {d}, found {d}\n", .{ expected.len, actual.len });
                        return error.TestExpectedEqual;
                    }
                    var i: usize = 0;
                    while (i < expected.len) : (i += 1) {
                        expectEqualDeep(expected[i], actual[i]) catch |e| {
                            print("index {d} incorrect. expected {any}, found {any}\n", .{
                                i, expected[i], actual[i],
                            });
                            return e;
                        };
                    }
                },
            }
        },

        .array => {
            if (expected.len != actual.len) {
                print("Array len not the same, expected {d}, found {d}\n", .{ expected.len, actual.len });
                return error.TestExpectedEqual;
            }
            var i: usize = 0;
            while (i < expected.len) : (i += 1) {
                expectEqualDeep(expected[i], actual[i]) catch |e| {
                    print("index {d} incorrect. expected {any}, found {any}\n", .{
                        i, expected[i], actual[i],
                    });
                    return e;
                };
            }
        },

        .vector => |info| {
            if (info.len != @typeInfo(@TypeOf(actual)).vector.len) {
                print("Vector len not the same, expected {d}, found {d}\n", .{ info.len, @typeInfo(@TypeOf(actual)).vector.len });
                return error.TestExpectedEqual;
            }
            inline for (0..info.len) |i| {
                expectEqualDeep(expected[i], actual[i]) catch |e| {
                    print("index {d} incorrect. expected {any}, found {any}\n", .{
                        i, expected[i], actual[i],
                    });
                    return e;
                };
            }
        },

        .@"struct" => |structType| {
            inline for (structType.fields) |field| {
                expectEqualDeep(@field(expected, field.name), @field(actual, field.name)) catch |e| {
                    print("Field {s} incorrect. expected {any}, found {any}\n", .{ field.name, @field(expected, field.name), @field(actual, field.name) });
                    return e;
                };
            }
        },

        .@"union" => |union_info| {
            if (union_info.tag_type == null) {
                @compileError("Unable to compare untagged union values for type " ++ @typeName(@TypeOf(actual)));
            }

            const Tag = std.meta.Tag(@TypeOf(expected));

            const expectedTag = @as(Tag, expected);
            const actualTag = @as(Tag, actual);

            try expectEqual(expectedTag, actualTag);

            // we only reach this switch if the tags are equal
            switch (expected) {
                inline else => |val, tag| {
                    try expectEqualDeep(val, @field(actual, @tagName(tag)));
                },
            }
        },

        .optional => {
            if (expected) |expected_payload| {
                if (actual) |actual_payload| {
                    try expectEqualDeep(expected_payload, actual_payload);
                } else {
                    print("expected {any}, found null\n", .{expected_payload});
                    return error.TestExpectedEqual;
                }
            } else {
                if (actual) |actual_payload| {
                    print("expected null, found {any}\n", .{actual_payload});
                    return error.TestExpectedEqual;
                }
            }
        },

        .error_union => {
            if (expected) |expected_payload| {
                if (actual) |actual_payload| {
                    try expectEqualDeep(expected_payload, actual_payload);
                } else |actual_err| {
                    print("expected {any}, found {any}\n", .{ expected_payload, actual_err });
                    return error.TestExpectedEqual;
                }
            } else |expected_err| {
                if (actual) |actual_payload| {
                    print("expected {any}, found {any}\n", .{ expected_err, actual_payload });
                    return error.TestExpectedEqual;
                } else |actual_err| {
                    try expectEqualDeep(expected_err, actual_err);
                }
            }
        },
    }
}

test "expectEqualDeep primitive type" {
    try expectEqualDeep(1, 1);
    try expectEqualDeep(true, true);
    try expectEqualDeep(1.5, 1.5);
    try expectEqualDeep(u8, u8);
    try expectEqualDeep(error.Bad, error.Bad);

    // optional
    {
        const foo: ?u32 = 1;
        const bar: ?u32 = 1;
        try expectEqualDeep(foo, bar);
        try expectEqualDeep(?u32, ?u32);
    }
    // function type
    {
        const fnType = struct {
            fn foo() void {
                unreachable;
            }
        }.foo;
        try expectEqualDeep(fnType, fnType);
    }
    // enum with formatter
    {
        const TestEnum = enum {
            a,
            b,

            pub fn format(self: @This(), writer: *Io.Writer) !void {
                try writer.writeAll(@tagName(self));
            }
        };
        try expectEqualDeep(TestEnum.b, TestEnum.b);
    }
}

test "expectEqualDeep pointer" {
    try comptime expectEqualDeep(&1, &1);
    try expectEqualDeep(&@as(u32, 1), &@as(u32, 1));
}

test "expectEqualDeep composite type" {
    try expectEqualDeep("abc", "abc");
    const s1: []const u8 = "abc";
    const s2 = "abcd";
    const s3: []const u8 = s2[0..3];
    try expectEqualDeep(s1, s3);

    const TestStruct = struct { s: []const u8 };
    try expectEqualDeep(TestStruct{ .s = "abc" }, TestStruct{ .s = "abc" });
    try expectEqualDeep([_][]const u8{ "a", "b", "c" }, [_][]const u8{ "a", "b", "c" });

    // vector
    try expectEqualDeep(@as(@Vector(4, u32), @splat(4)), @as(@Vector(4, u32), @splat(4)));

    // nested array
    {
        const a = [2][2]f32{
            [_]f32{ 1.0, 0.0 },
            [_]f32{ 0.0, 1.0 },
        };

        const b = [2][2]f32{
            [_]f32{ 1.0, 0.0 },
            [_]f32{ 0.0, 1.0 },
        };

        try expectEqualDeep(a, b);
        try expectEqualDeep(&a, &b);
    }

    // inferred union
    const TestStruct2 = struct {
        const A = union(enum) { b: B, c: C };
        const B = struct {};
        const C = struct { a: *const A };
    };

    const union1 = TestStruct2.A{ .b = .{} };
    try expectEqualDeep(
        TestStruct2.A{ .c = .{ .a = &union1 } },
        TestStruct2.A{ .c = .{ .a = &union1 } },
    );
}

fn printIndicatorLine(source: []const u8, indicator_index: usize) void {
    const line_begin_index = if (std.mem.lastIndexOfScalar(u8, source[0..indicator_index], '\n')) |line_begin|
        line_begin + 1
    else
        0;
    const line_end_index = if (std.mem.findScalar(u8, source[indicator_index..], '\n')) |line_end|
        (indicator_index + line_end)
    else
        source.len;

    printLine(source[line_begin_index..line_end_index]);
    for (line_begin_index..indicator_index) |_|
        print(" ", .{});
    if (indicator_index >= source.len)
        print("^ (end of string)\n", .{})
    else
        print("^ ('\\x{x:0>2}')\n", .{source[indicator_index]});
}

fn printWithVisibleNewlines(source: []const u8) void {
    var i: usize = 0;
    while (std.mem.findScalar(u8, source[i..], '\n')) |nl| : (i += nl + 1) {
        printLine(source[i..][0..nl]);
    }
    print("{s}␃\n", .{source[i..]}); // End of Text symbol (ETX)
}

fn printLine(line: []const u8) void {
    if (line.len != 0) switch (line[line.len - 1]) {
        ' ', '\t' => return print("{s}⏎\n", .{line}), // Return symbol
        else => {},
    };
    print("{s}\n", .{line});
}

test {
    try expectEqualStrings("foo", "foo");
}

/// Exhaustively check that allocation failures within `test_fn` are handled without
/// introducing memory leaks. If used with the `testing.allocator` as the `backing_allocator`,
/// it will also be able to detect double frees, etc (when runtime safety is enabled).
///
/// The provided `test_fn` must have a `std.mem.Allocator` as its first argument,
/// and must have a return type of `!void`. Any extra arguments of `test_fn` can
/// be provided via the `extra_args` tuple.
///
/// Any relevant state shared between runs of `test_fn` *must* be reset within `test_fn`.
///
/// The strategy employed is to:
/// - Run the test function once to get the total number of allocations.
/// - Then, iterate and run the function X more times, incrementing
///   the failing index each iteration (where X is the total number of
///   allocations determined previously)
///
/// Expects that `test_fn` has a deterministic number of memory allocations:
/// - If an allocation was made to fail during a run of `test_fn`, but `test_fn`
///   didn't return `error.OutOfMemory`, then `error.SwallowedOutOfMemoryError`
///   is returned from `checkAllAllocationFailures`. You may want to ignore this
///   depending on whether or not the code you're testing includes some strategies
///   for recovering from `error.OutOfMemory`.
/// - If a run of `test_fn` with an expected allocation failure executes without
///   an allocation failure being induced, then `error.NondeterministicMemoryUsage`
///   is returned. This error means that there are allocation points that won't be
///   tested by the strategy this function employs (that is, there are sometimes more
///   points of allocation than the initial run of `test_fn` detects).
///
/// ---
///
/// Here's an example using a simple test case that will cause a leak when the
/// allocation of `bar` fails (but will pass normally):
///
/// ```zig
/// test {
///     const length: usize = 10;
///     const allocator = std.testing.allocator;
///     var foo = try allocator.alloc(u8, length);
///     var bar = try allocator.alloc(u8, length);
///
///     allocator.free(foo);
///     allocator.free(bar);
/// }
/// ```
///
/// The test case can be converted to something that this function can use by
/// doing:
///
/// ```zig
/// fn testImpl(allocator: std.mem.Allocator, length: usize) !void {
///     var foo = try allocator.alloc(u8, length);
///     var bar = try allocator.alloc(u8, length);
///
///     allocator.free(foo);
///     allocator.free(bar);
/// }
///
/// test {
///     const length: usize = 10;
///     const allocator = std.testing.allocator;
///     try std.testing.checkAllAllocationFailures(allocator, testImpl, .{length});
/// }
/// ```
///
/// Running this test will show that `foo` is leaked when the allocation of
/// `bar` fails. The simplest fix, in this case, would be to use defer like so:
///
/// ```zig
/// fn testImpl(allocator: std.mem.Allocator, length: usize) !void {
///     var foo = try allocator.alloc(u8, length);
///     defer allocator.free(foo);
///     var bar = try allocator.alloc(u8, length);
///     defer allocator.free(bar);
/// }
/// ```
pub fn checkAllAllocationFailures(
    backing_allocator: std.mem.Allocator,
    comptime test_fn: anytype,
    extra_args: CheckAllAllocationFailuresExtraArgs(@TypeOf(test_fn)),
) !void {
    // Try it once with unlimited memory, make sure it works
    const needed_alloc_count = x: {
        var failing_allocator_inst = std.testing.FailingAllocator.init(backing_allocator, .{});

        try @call(.auto, test_fn, .{failing_allocator_inst.allocator()} ++ extra_args);
        break :x failing_allocator_inst.alloc_index;
    };

    for (0..needed_alloc_count) |fail_index| {
        var failing_allocator_inst = std.testing.FailingAllocator.init(backing_allocator, .{
            .fail_index = fail_index,
        });

        if (@call(.auto, test_fn, .{failing_allocator_inst.allocator()} ++ extra_args)) |_| {
            if (failing_allocator_inst.has_induced_failure) {
                return error.SwallowedOutOfMemoryError;
            } else {
                return error.NondeterministicMemoryUsage;
            }
        } else |err| switch (err) {
            error.OutOfMemory => {
                if (failing_allocator_inst.allocated_bytes != failing_allocator_inst.freed_bytes) {
                    print(
                        "\nfail_index: {d}/{d}\nallocated bytes: {d}\nfreed bytes: {d}\nallocations: {d}\ndeallocations: {d}\nallocation that was made to fail: {f}",
                        .{
                            fail_index,
                            needed_alloc_count,
                            failing_allocator_inst.allocated_bytes,
                            failing_allocator_inst.freed_bytes,
                            failing_allocator_inst.allocations,
                            failing_allocator_inst.deallocations,
                            std.debug.FormatStackTrace{
                                .stack_trace = failing_allocator_inst.getStackTrace(),
                            },
                        },
                    );
                    return error.MemoryLeakDetected;
                }
            },
            else => |e| return e,
        }
    }
}

fn CheckAllAllocationFailuresExtraArgs(comptime TestFn: type) type {
    switch (@typeInfo(@typeInfo(TestFn).@"fn".return_type.?)) {
        .error_union => |info| {
            if (info.payload != void) {
                @compileError("Return type must be !void");
            }
        },
        else => @compileError("Return type must be !void"),
    }

    const ArgsTuple = std.meta.ArgsTuple(TestFn);

    const fields = @typeInfo(ArgsTuple).@"struct".fields;
    if (fields.len == 0 or fields[0].type != std.mem.Allocator) {
        @compileError("The provided function must have an " ++ @typeName(std.mem.Allocator) ++ " as its first argument");
    }

    var extra_args: [fields.len - 1]type = undefined;
    for (&extra_args, fields[1..]) |*arg, field| {
        arg.* = field.type;
    }

    return @Tuple(&extra_args);
}

test "checkAllAllocationFailures provide result type to 'extra_args' argument" {
    try checkAllAllocationFailures(
        std.testing.allocator,
        struct {
            fn f(ally: std.mem.Allocator, params: struct {
                foo_len: u32,
                bar_len: u32,
            }) !void {
                const foo = try ally.alloc(u8, params.foo_len);
                defer ally.free(foo);
                const bar = try ally.alloc(u8, params.bar_len);
                defer ally.free(bar);
            }
        }.f,
        .{
            .{
                .foo_len = 3,
                .bar_len = 5,
            },
        },
    );
}

/// Given a type, references all the declarations inside, so that the semantic analyzer sees them.
pub fn refAllDecls(comptime T: type) void {
    if (!builtin.is_test) return;
    inline for (comptime std.meta.declarations(T)) |decl| {
        _ = &@field(T, decl.name);
    }
}

pub const Smith = @import("testing/Smith.zig");

pub const FuzzInputOptions = struct {
    corpus: []const []const u8 = &.{},
};

/// Inline to avoid coverage instrumentation.
pub inline fn fuzz(
    context: anytype,
    comptime testOne: fn (context: @TypeOf(context), smith: *Smith) anyerror!void,
    options: FuzzInputOptions,
) anyerror!void {
    return @import("root").fuzz(context, testOne, options);
}

/// A `Io.Reader` that writes a predetermined list of buffers during `stream`.
pub const Reader = struct {
    calls: []const Call,
    interface: Io.Reader,
    next_call_index: usize,
    next_offset: usize,
    /// Further reduces how many bytes are written in each `stream` call.
    artificial_limit: Io.Limit = .unlimited,

    pub const Call = struct {
        buffer: []const u8,
    };

    pub fn init(buffer: []u8, calls: []const Call) Reader {
        return .{
            .next_call_index = 0,
            .next_offset = 0,
            .interface = .{
                .vtable = &.{ .stream = stream },
                .buffer = buffer,
                .seek = 0,
                .end = 0,
            },
            .calls = calls,
        };
    }

    fn stream(io_r: *Io.Reader, w: *Io.Writer, limit: Io.Limit) Io.Reader.StreamError!usize {
        const r: *Reader = @alignCast(@fieldParentPtr("interface", io_r));
        if (r.calls.len - r.next_call_index == 0) return error.EndOfStream;
        const call = r.calls[r.next_call_index];
        const buffer = r.artificial_limit.sliceConst(limit.sliceConst(call.buffer[r.next_offset..]));
        const n = try w.write(buffer);
        r.next_offset += n;
        if (call.buffer.len - r.next_offset == 0) {
            r.next_call_index += 1;
            r.next_offset = 0;
        }
        return n;
    }
};

/// A `Io.Reader` that gets its data from another `Io.Reader`, and always
/// writes to its own buffer (and returns 0) during `stream` and `readVec`.
pub const ReaderIndirect = struct {
    in: *Io.Reader,
    interface: Io.Reader,

    pub fn init(in: *Io.Reader, buffer: []u8) ReaderIndirect {
        return .{
            .in = in,
            .interface = .{
                .vtable = &.{
                    .stream = stream,
                    .readVec = readVec,
                },
                .buffer = buffer,
                .seek = 0,
                .end = 0,
            },
        };
    }

    fn readVec(r: *Io.Reader, _: [][]u8) Io.Reader.Error!usize {
        try streamInner(r);
        return 0;
    }

    fn stream(r: *Io.Reader, _: *Io.Writer, _: Io.Limit) Io.Reader.StreamError!usize {
        try streamInner(r);
        return 0;
    }

    fn streamInner(r: *Io.Reader) Io.Reader.Error!void {
        const r_indirect: *ReaderIndirect = @alignCast(@fieldParentPtr("interface", r));

        // If there's no room remaining in the buffer at all, make room.
        if (r.buffer.len == r.end) {
            try r.rebase(r.buffer.len);
        }

        var writer: Io.Writer = .{
            .buffer = r.buffer,
            .end = r.end,
            .vtable = &.{
                .drain = Io.Writer.unreachableDrain,
                .rebase = Io.Writer.unreachableRebase,
            },
        };
        defer r.end = writer.end;

        r_indirect.in.streamExact(&writer, r.buffer.len - r.end) catch |err| switch (err) {
            // Only forward EndOfStream if no new bytes were written to the buffer
            error.EndOfStream => |e| if (r.end == writer.end) {
                return e;
            },
            error.WriteFailed => unreachable,
            else => |e| return e,
        };
    }
};

test {
    _ = &Smith;
}



---
File: /std/Thread.zig
---

//! This struct represents a kernel thread.
const Thread = @This();

const builtin = @import("builtin");
const target = builtin.target;
const native_os = builtin.os.tag;

const std = @import("std.zig");
const Io = std.Io;
const math = std.math;
const assert = std.debug.assert;
const posix = std.posix;
const windows = std.os.windows;
const testing = std.testing;

pub const use_pthreads = native_os != .windows and native_os != .wasi and builtin.link_libc;

const Impl = if (native_os == .windows)
    WindowsThreadImpl
else if (use_pthreads)
    PosixThreadImpl
else if (native_os == .linux)
    LinuxThreadImpl
else if (native_os == .wasi)
    WasiThreadImpl
else
    UnsupportedImpl;

impl: Impl,

pub const max_name_len = switch (native_os) {
    .linux => 15,
    .windows => 31,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => 63,
    .netbsd => 31,
    .freebsd => 15,
    .openbsd => 23,
    .dragonfly => 1023,
    .illumos => 31,
    // https://github.com/SerenityOS/serenity/blob/6b4c300353da49d3508b5442cf61da70bd04d757/Kernel/Tasks/Thread.h#L102
    .serenity => 63,
    else => 0,
};

pub const SetNameError = error{
    NameTooLong,
    Unsupported,
    Unexpected,
    InvalidWtf8,
} || posix.PrctlError || Io.File.Writer.Error || Io.File.OpenError || std.fmt.BufPrintError;

pub fn setName(self: Thread, io: Io, name: []const u8) SetNameError!void {
    if (name.len > max_name_len) return error.NameTooLong;

    const name_with_terminator = blk: {
        var name_buf: [max_name_len:0]u8 = undefined;
        @memcpy(name_buf[0..name.len], name);
        name_buf[name.len] = 0;
        break :blk name_buf[0..name.len :0];
    };

    switch (native_os) {
        .linux => if (use_pthreads) {
            if (self.getHandle() == std.c.pthread_self()) {
                // Set the name of the calling thread (no thread id required).
                assert(try posix.prctl(.SET_NAME, .{@intFromPtr(name_with_terminator.ptr)}) == 0);
                return;
            } else {
                const err = std.c.pthread_setname_np(self.getHandle(), name_with_terminator.ptr);
                switch (@as(posix.E, @enumFromInt(err))) {
                    .SUCCESS => return,
                    .RANGE => unreachable,
                    else => |e| return posix.unexpectedErrno(e),
                }
            }
        } else {
            var buf: [32]u8 = undefined;
            const path = try std.fmt.bufPrint(&buf, "/proc/self/task/{d}/comm", .{self.getHandle()});

            const file = try Io.Dir.cwd().openFile(io, path, .{ .mode = .write_only });
            defer file.close(io);

            try file.writeStreamingAll(io, name);
            return;
        },
        .windows => {
            var buf: [max_name_len]u16 = undefined;
            switch (windows.ntdll.NtSetInformationThread(
                self.getHandle(),
                .NameInformation,
                &windows.UNICODE_STRING.init(buf[0..try std.unicode.wtf8ToWtf16Le(&buf, name)]),
                @sizeOf(windows.UNICODE_STRING),
            )) {
                .SUCCESS => return,
                .NOT_IMPLEMENTED => return error.Unsupported,
                else => |err| return windows.unexpectedStatus(err),
            }
        },
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => if (use_pthreads) {
            // There doesn't seem to be a way to set the name for an arbitrary thread, only the current one.
            if (self.getHandle() != std.c.pthread_self()) return error.Unsupported;

            const err = std.c.pthread_setname_np(name_with_terminator.ptr);
            switch (@as(posix.E, @enumFromInt(err))) {
                .SUCCESS => return,
                else => |e| return posix.unexpectedErrno(e),
            }
        },
        .serenity => if (use_pthreads) {
            const err = std.c.pthread_setname_np(self.getHandle(), name_with_terminator.ptr);
            switch (@as(posix.E, @enumFromInt(err))) {
                .SUCCESS => return,
                .NAMETOOLONG => unreachable,
                .SRCH => unreachable,
                else => |e| return posix.unexpectedErrno(e),
            }
        },
        .netbsd, .illumos => if (use_pthreads) {
            const err = std.c.pthread_setname_np(self.getHandle(), name_with_terminator.ptr, null);
            switch (@as(posix.E, @enumFromInt(err))) {
                .SUCCESS => return,
                .INVAL => unreachable,
                .SRCH => unreachable,
                .NOMEM => unreachable,
                else => |e| return posix.unexpectedErrno(e),
            }
        },
        .freebsd, .openbsd => if (use_pthreads) {
            // Use pthread_set_name_np for FreeBSD because pthread_setname_np is FreeBSD 12.2+ only.
            // TODO maybe revisit this if depending on FreeBSD 12.2+ is acceptable because
            // pthread_setname_np can return an error.

            std.c.pthread_set_name_np(self.getHandle(), name_with_terminator.ptr);
            return;
        },
        .dragonfly => if (use_pthreads) {
            const err = std.c.pthread_setname_np(self.getHandle(), name_with_terminator.ptr);
            switch (@as(posix.E, @enumFromInt(err))) {
                .SUCCESS => return,
                .INVAL => unreachable,
                .FAULT => unreachable,
                .NAMETOOLONG => unreachable, // already checked
                .SRCH => unreachable,
                else => |e| return posix.unexpectedErrno(e),
            }
        },
        else => {},
    }
    return error.Unsupported;
}

pub const GetNameError = error{
    Unsupported,
    Unexpected,
} || posix.PrctlError || posix.ReadError || Io.File.OpenError || std.fmt.BufPrintError;

/// On Windows, the result is encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On other platforms, the result is an opaque sequence of bytes with no particular encoding.
pub fn getName(self: Thread, buffer_ptr: *[max_name_len:0]u8) GetNameError!?[]const u8 {
    buffer_ptr[max_name_len] = 0;
    var buffer: [:0]u8 = buffer_ptr;

    switch (native_os) {
        .linux => if (use_pthreads) {
            if (self.getHandle() == std.c.pthread_self()) {
                // Get the name of the calling thread (no thread id required).
                assert(try posix.prctl(.GET_NAME, .{@intFromPtr(buffer.ptr)}) == 0);
                return std.mem.sliceTo(buffer, 0);
            } else {
                const err = std.c.pthread_getname_np(self.getHandle(), buffer.ptr, max_name_len + 1);
                switch (@as(posix.E, @enumFromInt(err))) {
                    .SUCCESS => return std.mem.sliceTo(buffer, 0),
                    .RANGE => unreachable,
                    else => |e| return posix.unexpectedErrno(e),
                }
            }
        } else {
            var buf: [32]u8 = undefined;
            const path = try std.fmt.bufPrint(&buf, "/proc/self/task/{d}/comm", .{self.getHandle()});

            const io = std.Options.debug_io;

            const file = try Io.Dir.cwd().openFile(io, path, .{});
            defer file.close(io);

            var file_reader = file.readerStreaming(io, &.{});
            const data_len = file_reader.interface.readSliceShort(buffer_ptr[0 .. max_name_len + 1]) catch |err| switch (err) {
                error.ReadFailed => return file_reader.err.?,
            };
            return if (data_len >= 1) buffer[0 .. data_len - 1] else null;
        },
        .windows => {
            const buf_capacity = @sizeOf(windows.UNICODE_STRING) + (@sizeOf(u16) * max_name_len);
            var buf: [buf_capacity]u8 align(@alignOf(windows.UNICODE_STRING)) = undefined;

            switch (windows.ntdll.NtQueryInformationThread(
                self.getHandle(),
                .NameInformation,
                &buf,
                buf_capacity,
                null,
            )) {
                .SUCCESS => {
                    const string: *const windows.UNICODE_STRING = @ptrCast(&buf);
                    const len = std.unicode.wtf16LeToWtf8(buffer, string.slice());
                    return if (len > 0) buffer[0..len] else null;
                },
                .NOT_IMPLEMENTED => return error.Unsupported,
                else => |err| return windows.unexpectedStatus(err),
            }
        },
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => if (use_pthreads) {
            const err = std.c.pthread_getname_np(self.getHandle(), buffer.ptr, max_name_len + 1);
            switch (@as(posix.E, @enumFromInt(err))) {
                .SUCCESS => return std.mem.sliceTo(buffer, 0),
                .SRCH => unreachable,
                else => |e| return posix.unexpectedErrno(e),
            }
        },
        .serenity => if (use_pthreads) {
            const err = std.c.pthread_getname_np(self.getHandle(), buffer.ptr, max_name_len + 1);
            switch (@as(posix.E, @enumFromInt(err))) {
                .SUCCESS => return,
                .NAMETOOLONG => unreachable,
                .SRCH => unreachable,
                .FAULT => unreachable,
                else => |e| return posix.unexpectedErrno(e),
            }
        },
        .netbsd, .illumos => if (use_pthreads) {
            const err = std.c.pthread_getname_np(self.getHandle(), buffer.ptr, max_name_len + 1);
            switch (@as(posix.E, @enumFromInt(err))) {
                .SUCCESS => return std.mem.sliceTo(buffer, 0),
                .INVAL => unreachable,
                .SRCH => unreachable,
                else => |e| return posix.unexpectedErrno(e),
            }
        },
        .freebsd, .openbsd => if (use_pthreads) {
            // Use pthread_get_name_np for FreeBSD because pthread_getname_np is FreeBSD 12.2+ only.
            // TODO maybe revisit this if depending on FreeBSD 12.2+ is acceptable because pthread_getname_np can return an error.

            std.c.pthread_get_name_np(self.getHandle(), buffer.ptr, max_name_len + 1);
            return std.mem.sliceTo(buffer, 0);
        },
        .dragonfly => if (use_pthreads) {
            const err = std.c.pthread_getname_np(self.getHandle(), buffer.ptr, max_name_len + 1);
            switch (@as(posix.E, @enumFromInt(err))) {
                .SUCCESS => return std.mem.sliceTo(buffer, 0),
                .INVAL => unreachable,
                .FAULT => unreachable,
                .SRCH => unreachable,
                else => |e| return posix.unexpectedErrno(e),
            }
        },
        else => {},
    }
    return error.Unsupported;
}

/// Represents an ID per thread guaranteed to be unique only within a process.
pub const Id = switch (native_os) {
    .linux,
    .dragonfly,
    .netbsd,
    .freebsd,
    .openbsd,
    .haiku,
    .wasi,
    .serenity,
    => u32,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => u64,
    .windows => windows.DWORD,
    else => usize,
};

/// Returns the platform ID of the callers thread.
/// Attempts to use thread locals and avoid syscalls when possible.
pub fn getCurrentId() Id {
    return Impl.getCurrentId();
}

pub const CpuCountError = error{
    PermissionDenied,
    SystemResources,
    Unsupported,
    Unexpected,
};

/// Returns the platforms view on the number of logical CPU cores available.
///
/// Returned value guaranteed to be >= 1.
pub fn getCpuCount() CpuCountError!usize {
    return try Impl.getCpuCount();
}

/// Configuration options for hints on how to spawn threads.
pub const SpawnConfig = struct {
    // TODO compile-time call graph analysis to determine stack upper bound
    // https://github.com/ziglang/zig/issues/157

    /// Size in bytes of the Thread's stack
    stack_size: usize = default_stack_size,
    /// The allocator to be used to allocate memory for the to-be-spawned thread
    allocator: ?std.mem.Allocator = null,

    pub const default_stack_size = 16 * 1024 * 1024;
};

pub const SpawnError = error{
    /// A system-imposed limit on the number of threads was encountered.
    /// There are a number of limits that may trigger this error:
    /// *  the  RLIMIT_NPROC soft resource limit (set via setrlimit(2)),
    ///    which limits the number of processes and threads for  a  real
    ///    user ID, was reached;
    /// *  the kernel's system-wide limit on the number of processes and
    ///    threads,  /proc/sys/kernel/threads-max,  was   reached   (see
    ///    proc(5));
    /// *  the  maximum  number  of  PIDs, /proc/sys/kernel/pid_max, was
    ///    reached (see proc(5)); or
    /// *  the PID limit (pids.max) imposed by the cgroup "process  num‐
    ///    ber" (PIDs) controller was reached.
    ThreadQuotaExceeded,

    /// The kernel cannot allocate sufficient memory to allocate a task structure
    /// for the child, or to copy those parts of the caller's context that need to
    /// be copied.
    SystemResources,

    /// Not enough userland memory to spawn the thread.
    OutOfMemory,

    /// `mlockall` is enabled, and the memory needed to spawn the thread
    /// would exceed the limit.
    LockedMemoryLimitExceeded,

    Unexpected,
};

/// Spawns a new thread which executes `function` using `args` and returns a handle to the spawned thread.
/// `config` can be used as hints to the platform for how to spawn and execute the `function`.
/// The caller must eventually either call `join()` to wait for the thread to finish and free its resources
/// or call `detach()` to excuse the caller from calling `join()` and have the thread clean up its resources on completion.
pub fn spawn(config: SpawnConfig, comptime function: anytype, args: anytype) SpawnError!Thread {
    if (builtin.single_threaded) {
        @compileError("Cannot spawn thread when building in single-threaded mode");
    }

    const impl = try Impl.spawn(config, function, args);
    return Thread{ .impl = impl };
}

/// Represents a kernel thread handle.
/// May be an integer or a pointer depending on the platform.
pub const Handle = Impl.ThreadHandle;

/// Returns the handle of this thread
pub fn getHandle(self: Thread) Handle {
    return self.impl.getHandle();
}

/// Release the obligation of the caller to call `join()` and have the thread clean up its own resources on completion.
/// Once called, this consumes the Thread object and invoking any other functions on it is considered undefined behavior.
pub fn detach(self: Thread) void {
    return self.impl.detach();
}

/// Waits for the thread to complete, then deallocates any resources created on `spawn()`.
/// Once called, this consumes the Thread object and invoking any other functions on it is considered undefined behavior.
pub fn join(self: Thread) void {
    return self.impl.join();
}

pub const YieldError = error{
    /// The system is not configured to allow yielding
    SystemCannotYield,
};

/// Yields the current thread potentially allowing other threads to run.
pub fn yield() YieldError!void {
    if (native_os == .windows) switch (windows.ntdll.NtYieldExecution()) {
        .SUCCESS, .NO_YIELD_PERFORMED => return,
        else => return error.SystemCannotYield,
    };
    switch (posix.errno(posix.system.sched_yield())) {
        .SUCCESS => return,
        .NOSYS => return error.SystemCannotYield,
        else => return error.SystemCannotYield,
    }
}

/// State to synchronize detachment of spawner thread to spawned thread
const Completion = std.atomic.Value(enum(if (builtin.zig_backend == .stage2_riscv64) u32 else u8) {
    running,
    detached,
    completed,
});

/// Performs implementation-agnostic thread setup (`maybeAttachSignalStack`), then calls the given
/// thread entry point `f` with `args` and handles the result.
fn callFn(comptime f: anytype, args: anytype) switch (Impl) {
    WindowsThreadImpl => windows.NTSTATUS,
    LinuxThreadImpl => u8,
    PosixThreadImpl => ?*anyopaque,
    else => unreachable,
} {
    maybeAttachSignalStack();

    const default_value = switch (Impl) {
        WindowsThreadImpl => .SUCCESS,
        LinuxThreadImpl => 0,
        PosixThreadImpl => null,
        else => unreachable,
    };
    const bad_fn_ret = "expected return type of startFn to be 'u8', 'noreturn', '!noreturn', 'void', or '!void'";

    switch (@typeInfo(@typeInfo(@TypeOf(f)).@"fn".return_type.?)) {
        .noreturn => {
            @call(.auto, f, args);
        },
        .void => {
            @call(.auto, f, args);
            return default_value;
        },
        .int => |info| {
            if (info.bits != 8) {
                @compileError(bad_fn_ret);
            }

            const status = @call(.auto, f, args);
            switch (Impl) {
                WindowsThreadImpl => return @enumFromInt(status),
                LinuxThreadImpl => return status,
                // pthreads don't support exit status, ignore value
                PosixThreadImpl => return default_value,
                else => unreachable,
            }
        },
        .error_union => |info| {
            switch (info.payload) {
                void, noreturn => {
                    @call(.auto, f, args) catch |err| {
                        std.debug.print("error: {s}\n", .{@errorName(err)});
                        if (@errorReturnTrace()) |trace| {
                            std.debug.dumpErrorReturnTrace(trace);
                        }
                    };

                    return default_value;
                },
                else => {
                    @compileError(bad_fn_ret);
                },
            }
        },
        else => {
            @compileError(bad_fn_ret);
        },
    }
}

/// We can't compile error in the `Impl` switch statement as its eagerly evaluated.
/// So instead, we compile-error on the methods themselves for platforms which don't support threads.
const UnsupportedImpl = struct {
    pub const ThreadHandle = void;

    fn getCurrentId() usize {
        return unsupported({});
    }

    fn getCpuCount() !usize {
        return unsupported({});
    }

    fn spawn(config: SpawnConfig, comptime f: anytype, args: anytype) !Impl {
        return unsupported(.{ config, f, args });
    }

    fn getHandle(self: Impl) ThreadHandle {
        return unsupported(self);
    }

    fn detach(self: Impl) void {
        return unsupported(self);
    }

    fn join(self: Impl) void {
        return unsupported(self);
    }

    fn unsupported(unused: anytype) noreturn {
        _ = unused;
        @compileError("Unsupported operating system " ++ @tagName(native_os));
    }
};

const WindowsThreadImpl = struct {
    pub const ThreadHandle = windows.HANDLE;

    fn getCurrentId() windows.DWORD {
        return windows.GetCurrentThreadId();
    }

    fn getCpuCount() !usize {
        // Faster than calling into GetSystemInfo(), even if amortized.
        return windows.peb().NumberOfProcessors;
    }

    thread: *ThreadCompletion,

    const ThreadCompletion = struct {
        completion: Completion,
        heap_ptr: windows.PVOID,
        heap_handle: *windows.HEAP,
        thread_handle: windows.HANDLE = undefined,

        fn free(self: ThreadCompletion) void {
            const status = windows.ntdll.RtlFreeHeap(self.heap_handle, .{}, self.heap_ptr);
            assert(status != 0);
        }
    };

    fn spawn(config: SpawnConfig, comptime f: anytype, args: anytype) !Impl {
        const Args = @TypeOf(args);
        const Instance = struct {
            fn_args: Args,
            thread: ThreadCompletion,

            fn entryFn(raw_ptr: windows.PVOID) callconv(.winapi) windows.NTSTATUS {
                const self: *@This() = @ptrCast(@alignCast(raw_ptr));
                defer switch (self.thread.completion.swap(.completed, .seq_cst)) {
                    .running => {},
                    .completed => unreachable,
                    .detached => self.thread.free(),
                };
                return callFn(f, self.fn_args);
            }
        };

        const heap_handle = windows.GetProcessHeap() orelse return error.OutOfMemory;
        const alloc_bytes = @alignOf(Instance) + @sizeOf(Instance);
        const alloc_ptr = windows.ntdll.RtlAllocateHeap(heap_handle, .{}, alloc_bytes) orelse return error.OutOfMemory;
        errdefer assert(windows.ntdll.RtlFreeHeap(heap_handle, .{}, alloc_ptr) != 0);

        const instance_bytes = @as([*]u8, @ptrCast(alloc_ptr))[0..alloc_bytes];
        var fba = std.heap.FixedBufferAllocator.init(instance_bytes);
        const instance = fba.allocator().create(Instance) catch unreachable;
        instance.* = .{
            .fn_args = args,
            .thread = .{
                .completion = Completion.init(.running),
                .heap_ptr = alloc_ptr,
                .heap_handle = heap_handle,
            },
        };

        // Windows appears to only support SYSTEM.BASIC_INFORMATION.AllocationGranularity
        // minimum stack size. Going lower makes it default to that specified in the executable
        // (~1mb). Its also fine if the limit here is incorrect as stack size is only a hint.
        const stack_size = @max(64 * 1024, std.math.lossyCast(u32, config.stack_size));

        // Intended to be equivalent to a kernel32.CreateThread call with no flags set.
        // However, CreateThread is just a wrapper around CreateRemoteThreadEx,
        // so that's the more relevant function in this context.
        //
        // https://github.com/wine-mirror/wine/blob/3d128be6400b3869119d293d0c8fa9e7702978f8/dlls/kernelbase/thread.c#L85
        instance.thread.thread_handle = blk: {
            var active_ctx: ?windows.HANDLE = undefined;
            // Note: Can return null on SUCCESS
            switch (windows.ntdll.RtlGetActiveActivationContext(&active_ctx)) {
                .SUCCESS => {},
                else => |status| return windows.unexpectedStatus(status),
            }
            defer if (active_ctx) |ctx| windows.ntdll.RtlReleaseActivationContext(ctx);

            var teb: *windows.TEB = undefined;
            var attr_list = windows.PS.ATTRIBUTE.LIST{
                .TotalLength = @sizeOf(windows.PS.ATTRIBUTE.LIST),
                .Attributes = .{
                    .{
                        .Attribute = .TEB_ADDRESS,
                        .Size = @sizeOf(*windows.TEB),
                        .u = .{
                            .ValuePtr = @ptrCast(&teb),
                        },
                        .ReturnLength = null,
                    },
                },
            };

            var thread_handle: windows.HANDLE = undefined;
            switch (windows.ntdll.NtCreateThreadEx(
                &thread_handle,
                .{ .MAXIMUM_ALLOWED = true },
                &.{},
                windows.GetCurrentProcess(),
                Instance.entryFn,
                instance,
                .{ .CREATE_SUSPENDED = true },
                0,
                @enumFromInt(stack_size),
                .default,
                &attr_list,
            )) {
                .SUCCESS => {},
                else => |status| return windows.unexpectedStatus(status),
            }

            if (active_ctx) |ctx| {
                var cookie: windows.ULONG = 0;
                switch (windows.ntdll.RtlActivateActivationContextEx(0, teb, ctx, &cookie)) {
                    .SUCCESS => {},
                    else => |status| return windows.unexpectedStatus(status),
                }
            }

            switch (windows.ntdll.NtResumeThread(thread_handle, null)) {
                .SUCCESS => {},
                else => |status| return windows.unexpectedStatus(status),
            }

            break :blk thread_handle;
        };

        return Impl{ .thread = &instance.thread };
    }

    fn getHandle(self: Impl) ThreadHandle {
        return self.thread.thread_handle;
    }

    fn detach(self: Impl) void {
        windows.CloseHandle(self.thread.thread_handle);
        switch (self.thread.completion.swap(.detached, .seq_cst)) {
            .running => {},
            .completed => self.thread.free(),
            .detached => unreachable,
        }
    }

    fn join(self: Impl) void {
        const infinite_timeout: windows.LARGE_INTEGER = std.math.minInt(windows.LARGE_INTEGER);
        switch (windows.ntdll.NtWaitForSingleObject(self.thread.thread_handle, .FALSE, &infinite_timeout)) {
            windows.NTSTATUS.WAIT_0 => {},
            else => |status| windows.unexpectedStatus(status) catch unreachable,
        }
        windows.CloseHandle(self.thread.thread_handle);
        assert(self.thread.completion.load(.seq_cst) == .completed);
        self.thread.free();
    }
};

const PosixThreadImpl = struct {
    const c = std.c;

    pub const ThreadHandle = c.pthread_t;

    fn getCurrentId() Id {
        switch (native_os) {
            .linux => {
                return LinuxThreadImpl.getCurrentId();
            },
            .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => {
                var thread_id: u64 = undefined;
                // Pass thread=null to get the current thread ID.
                assert(c.pthread_threadid_np(null, &thread_id) == 0);
                return thread_id;
            },
            .dragonfly => {
                return @as(u32, @bitCast(c.lwp_gettid()));
            },
            .netbsd => {
                return @as(u32, @bitCast(c._lwp_self()));
            },
            .freebsd => {
                return @as(u32, @bitCast(c.pthread_getthreadid_np()));
            },
            .openbsd => {
                return @as(u32, @bitCast(c.getthrid()));
            },
            .haiku => {
                return @as(u32, @bitCast(c.find_thread(null)));
            },
            .serenity => {
                return @as(u32, @bitCast(c.pthread_self()));
            },
            else => {
                return @intFromPtr(c.pthread_self());
            },
        }
    }

    fn getCpuCount() !usize {
        switch (native_os) {
            .linux => {
                return LinuxThreadImpl.getCpuCount();
            },
            .openbsd => {
                var count: c_int = undefined;
                var count_size: usize = @sizeOf(c_int);
                const mib = [_]c_int{ std.c.CTL.HW, std.c.HW.NCPUONLINE };
                posix.sysctl(&mib, &count, &count_size, null, 0) catch |err| switch (err) {
                    error.NameTooLong, error.UnknownName => unreachable,
                    else => |e| return e,
                };
                return @as(usize, @intCast(count));
            },
            .illumos, .serenity => {
                // The "proper" way to get the cpu count would be to query
                // /dev/kstat via ioctls, and traverse a linked list for each
                // cpu. (illumos)
                const rc = c.sysconf(@intFromEnum(std.c._SC.NPROCESSORS_ONLN));
                return switch (posix.errno(rc)) {
                    .SUCCESS => @as(usize, @intCast(rc)),
                    else => |err| posix.unexpectedErrno(err),
                };
            },
            .haiku => {
                var system_info: std.c.system_info = undefined;
                const rc = std.c.get_system_info(&system_info); // always returns B_OK
                return switch (posix.errno(rc)) {
                    .SUCCESS => @as(usize, @intCast(system_info.cpu_count)),
                    else => |err| posix.unexpectedErrno(err),
                };
            },
            else => {
                var count: c_int = undefined;
                var count_len: usize = @sizeOf(c_int);
                const name = comptime if (target.os.tag.isDarwin()) "hw.logicalcpu" else "hw.ncpu";
                switch (posix.errno(posix.system.sysctlbyname(name, &count, &count_len, null, 0))) {
                    .SUCCESS => return @intCast(count),
                    .FAULT => unreachable,
                    .PERM => return error.PermissionDenied,
                    .NOMEM => return error.SystemResources,
                    .NOENT => unreachable,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }

    handle: ThreadHandle,

    fn spawn(config: SpawnConfig, comptime f: anytype, args: anytype) !Impl {
        const Args = @TypeOf(args);
        const allocator = std.heap.c_allocator;

        const Instance = struct {
            fn entryFn(raw_arg: ?*anyopaque) callconv(.c) ?*anyopaque {
                const args_ptr: *Args = @ptrCast(@alignCast(raw_arg));
                defer allocator.destroy(args_ptr);
                return callFn(f, args_ptr.*);
            }
        };

        const args_ptr = try allocator.create(Args);
        args_ptr.* = args;
        errdefer allocator.destroy(args_ptr);

        var attr: c.pthread_attr_t = undefined;
        if (c.pthread_attr_init(&attr) != .SUCCESS) return error.SystemResources;
        defer assert(c.pthread_attr_destroy(&attr) == .SUCCESS);

        // Use the same set of parameters used by the libc-less impl.
        const stack_size = @max(config.stack_size, 16 * 1024);
        assert(c.pthread_attr_setstacksize(&attr, stack_size) == .SUCCESS);
        assert(c.pthread_attr_setguardsize(&attr, std.heap.pageSize()) == .SUCCESS);

        var handle: c.pthread_t = undefined;
        switch (c.pthread_create(
            &handle,
            &attr,
            Instance.entryFn,
            @ptrCast(args_ptr),
        )) {
            .SUCCESS => return Impl{ .handle = handle },
            .AGAIN => return error.SystemResources,
            .PERM => unreachable,
            .INVAL => unreachable,
            else => |err| return posix.unexpectedErrno(err),
        }
    }

    fn getHandle(self: Impl) ThreadHandle {
        return self.handle;
    }

    fn detach(self: Impl) void {
        switch (c.pthread_detach(self.handle)) {
            .SUCCESS => {},
            .INVAL => unreachable, // thread handle is not joinable
            .SRCH => unreachable, // thread handle is invalid
            else => unreachable,
        }
    }

    fn join(self: Impl) void {
        switch (c.pthread_join(self.handle, null)) {
            .SUCCESS => {},
            .INVAL => unreachable, // thread handle is not joinable (or another thread is already joining in)
            .SRCH => unreachable, // thread handle is invalid
            .DEADLK => unreachable, // two threads tried to join each other
            else => unreachable,
        }
    }
};

const WasiThreadImpl = struct {
    thread: *WasiThread,

    pub const ThreadHandle = i32;
    threadlocal var tls_thread_id: Id = 0;

    const WasiThread = struct {
        /// Thread ID
        tid: std.atomic.Value(i32) = std.atomic.Value(i32).init(0),
        /// Contains all memory which was allocated to bootstrap this thread, including:
        /// - Guard page
        /// - Stack
        /// - TLS segment
        /// - `Instance`
        /// All memory is freed upon call to `join`
        memory: []u8,
        /// The allocator used to allocate the thread's memory,
        /// which is also used during `join` to ensure clean-up.
        allocator: std.mem.Allocator,
        /// The current state of the thread.
        state: State = State.init(.running),
    };

    /// A meta-data structure used to bootstrap a thread
    const Instance = struct {
        thread: WasiThread,
        /// Contains the offset to the new __tls_base.
        /// The offset starting from the memory's base.
        tls_offset: usize,
        /// Contains the offset to the stack for the newly spawned thread.
        /// The offset is calculated starting from the memory's base.
        stack_offset: usize,
        /// Contains the raw pointer value to the wrapper which holds all arguments
        /// for the callback.
        raw_ptr: usize,
        /// Function pointer to a wrapping function which will call the user's
        /// function upon thread spawn. The above mentioned pointer will be passed
        /// to this function pointer as its argument.
        call_back: *const fn (usize) void,
        /// When a thread is in `detached` state, we must free all of its memory
        /// upon thread completion. However, as this is done while still within
        /// the thread, we must first jump back to the main thread's stack or else
        /// we end up freeing the stack that we're currently using.
        original_stack_pointer: [*]u8,
    };

    const State = std.atomic.Value(enum(u8) { running, completed, detached });

    fn getCurrentId() Id {
        return tls_thread_id;
    }

    fn getCpuCount() error{Unsupported}!noreturn {
        return error.Unsupported;
    }

    fn getHandle(self: Impl) ThreadHandle {
        return self.thread.tid.load(.seq_cst);
    }

    fn detach(self: Impl) void {
        switch (self.thread.state.swap(.detached, .seq_cst)) {
            .running => {},
            .completed => self.join(),
            .detached => unreachable,
        }
    }

    fn join(self: Impl) void {
        defer {
            // Create a copy of the allocator so we do not free the reference to the
            // original allocator while freeing the memory.
            var allocator = self.thread.allocator;
            allocator.free(self.thread.memory);
        }

        while (true) {
            const tid = self.thread.tid.load(.seq_cst);
            if (tid == 0) break;

            const result = asm (
                \\ local.get %[ptr]
                \\ local.get %[expected]
                \\ i64.const -1 # infinite
                \\ memory.atomic.wait32 0
                \\ local.set %[ret]
                : [ret] "=r" (-> u32),
                : [ptr] "r" (&self.thread.tid.raw),
                  [expected] "r" (tid),
            );
            switch (result) {
                0 => continue, // ok
                1 => continue, // expected =! loaded
                2 => unreachable, // timeout (infinite)
                else => unreachable,
            }
        }
    }

    fn spawn(config: std.Thread.SpawnConfig, comptime f: anytype, args: anytype) SpawnError!WasiThreadImpl {
        if (config.allocator == null) {
            @panic("an allocator is required to spawn a WASI thread");
        }

        // Wrapping struct required to hold the user-provided function arguments.
        const Wrapper = struct {
            args: @TypeOf(args),
            fn entry(ptr: usize) void {
                const w: *@This() = @ptrFromInt(ptr);
                const bad_fn_ret = "expected return type of startFn to be 'u8', 'noreturn', 'void', or '!void'";
                switch (@typeInfo(@typeInfo(@TypeOf(f)).@"fn".return_type.?)) {
                    .noreturn, .void => {
                        @call(.auto, f, w.args);
                    },
                    .int => |info| {
                        if (info.bits != 8) {
                            @compileError(bad_fn_ret);
                        }
                        _ = @call(.auto, f, w.args); // WASI threads don't support exit status, ignore value
                    },
                    .error_union => |info| {
                        if (info.payload != void) {
                            @compileError(bad_fn_ret);
                        }
                        @call(.auto, f, w.args) catch |err| {
                            std.debug.print("error: {s}\n", .{@errorName(err)});
                            if (@errorReturnTrace()) |trace| {
                                std.debug.dumpErrorReturnTrace(trace);
                            }
                        };
                    },
                    else => {
                        @compileError(bad_fn_ret);
                    },
                }
            }
        };

        var stack_offset: usize = undefined;
        var tls_offset: usize = undefined;
        var wrapper_offset: usize = undefined;
        var instance_offset: usize = undefined;

        // Calculate the bytes we have to allocate to store all thread information, including:
        // - The actual stack for the thread
        // - The TLS segment
        // - `Instance` - containing information about how to call the user's function.
        const map_bytes = blk: {
            // start with atleast a single page, which is used as a guard to prevent
            // other threads clobbering our new thread.
            // Unfortunately, WebAssembly has no notion of read-only segments, so this
            // is only a best effort.
            var bytes: usize = std.wasm.page_size;

            bytes = std.mem.alignForward(usize, bytes, 16); // align stack to 16 bytes
            stack_offset = bytes;
            bytes += @max(std.wasm.page_size, config.stack_size);

            bytes = std.mem.alignForward(usize, bytes, __tls_align());
            tls_offset = bytes;
            bytes += __tls_size();

            bytes = std.mem.alignForward(usize, bytes, @alignOf(Wrapper));
            wrapper_offset = bytes;
            bytes += @sizeOf(Wrapper);

            bytes = std.mem.alignForward(usize, bytes, @alignOf(Instance));
            instance_offset = bytes;
            bytes += @sizeOf(Instance);

            bytes = std.mem.alignForward(usize, bytes, std.wasm.page_size);
            break :blk bytes;
        };

        // Allocate the amount of memory required for all meta data.
        const allocated_memory = try config.allocator.?.alloc(u8, map_bytes);

        const wrapper: *Wrapper = @ptrCast(@alignCast(&allocated_memory[wrapper_offset]));
        wrapper.* = .{ .args = args };

        const instance: *Instance = @ptrCast(@alignCast(&allocated_memory[instance_offset]));
        instance.* = .{
            .thread = .{ .memory = allocated_memory, .allocator = config.allocator.? },
            .tls_offset = tls_offset,
            .stack_offset = stack_offset,
            .raw_ptr = @intFromPtr(wrapper),
            .call_back = &Wrapper.entry,
            .original_stack_pointer = __get_stack_pointer(),
        };

        const tid = spawnWasiThread(instance);
        // The specification says any value lower than 0 indicates an error.
        // The values of such error are unspecified. WASI-Libc treats it as EAGAIN.
        if (tid < 0) {
            return error.SystemResources;
        }
        instance.thread.tid.store(tid, .seq_cst);

        return .{ .thread = &instance.thread };
    }

    comptime {
        if (!builtin.single_threaded) {
            @export(&wasi_thread_start, .{ .name = "wasi_thread_start" });
        }
    }

    /// Called by the host environment after thread creation.
    fn wasi_thread_start(tid: i32, arg: *Instance) callconv(.c) void {
        comptime assert(!builtin.single_threaded);
        __set_stack_pointer(arg.thread.memory.ptr + arg.stack_offset);
        __wasm_init_tls(arg.thread.memory.ptr + arg.tls_offset);
        @atomicStore(u32, &WasiThreadImpl.tls_thread_id, @intCast(tid), .seq_cst);

        // Finished bootstrapping, call user's procedure.
        arg.call_back(arg.raw_ptr);

        switch (arg.thread.state.swap(.completed, .seq_cst)) {
            .running => {
                // reset the Thread ID
                asm volatile (
                    \\ local.get %[ptr]
                    \\ i32.const 0
                    \\ i32.atomic.store 0
                    :
                    : [ptr] "r" (&arg.thread.tid.raw),
                );

                // Wake the main thread listening to this thread
                asm volatile (
                    \\ local.get %[ptr]
                    \\ i32.const 1 # waiters
                    \\ memory.atomic.notify 0
                    \\ drop # no need to know the waiters
                    :
                    : [ptr] "r" (&arg.thread.tid.raw),
                );
            },
            .completed => unreachable,
            .detached => {
                // restore the original stack pointer so we can free the memory
                // without having to worry about freeing the stack
                __set_stack_pointer(arg.original_stack_pointer);
                // Ensure a copy so we don't free the allocator reference itself
                var allocator = arg.thread.allocator;
                allocator.free(arg.thread.memory);
            },
        }
    }

    /// Asks the host to create a new thread for us.
    /// Newly created thread will call `wasi_tread_start` with the thread ID as well
    /// as the input `arg` that was provided to `spawnWasiThread`
    const spawnWasiThread = @"thread-spawn";
    extern "wasi" fn @"thread-spawn"(arg: *Instance) i32;

    /// Initializes the TLS data segment starting at `memory`.
    /// This is a synthetic function, generated by the linker.
    extern fn __wasm_init_tls(memory: [*]u8) void;

    /// Returns a pointer to the base of the TLS data segment for the current thread
    inline fn __tls_base() [*]u8 {
        return asm (
            \\ .globaltype __tls_base, i32
            \\ global.get __tls_base
            \\ local.set %[ret]
            : [ret] "=r" (-> [*]u8),
        );
    }

    /// Returns the size of the TLS segment
    inline fn __tls_size() u32 {
        return asm volatile (
            \\ .globaltype __tls_size, i32, immutable
            \\ global.get __tls_size
            \\ local.set %[ret]
            : [ret] "=r" (-> u32),
        );
    }

    /// Returns the alignment of the TLS segment
    inline fn __tls_align() u32 {
        return asm (
            \\ .globaltype __tls_align, i32, immutable
            \\ global.get __tls_align
            \\ local.set %[ret]
            : [ret] "=r" (-> u32),
        );
    }

    /// Allows for setting the stack pointer in the WebAssembly module.
    inline fn __set_stack_pointer(addr: [*]u8) void {
        asm volatile (
            \\ local.get %[ptr]
            \\ global.set __stack_pointer
            :
            : [ptr] "r" (addr),
        );
    }

    /// Returns the current value of the stack pointer
    inline fn __get_stack_pointer() [*]u8 {
        return asm (
            \\ global.get __stack_pointer
            \\ local.set %[stack_ptr]
            : [stack_ptr] "=r" (-> [*]u8),
        );
    }
};

const LinuxThreadImpl = struct {
    const linux = std.os.linux;

    pub const ThreadHandle = i32;

    threadlocal var tls_thread_id: ?Id = null;

    fn getCurrentId() Id {
        return tls_thread_id orelse {
            const tid: u32 = @bitCast(linux.gettid());
            tls_thread_id = tid;
            return tid;
        };
    }

    fn getCpuCount() !usize {
        const cpu_set = try posix.sched_getaffinity(0);
        return posix.CPU_COUNT(cpu_set);
    }

    thread: *ThreadCompletion,

    const ThreadCompletion = struct {
        completion: Completion = Completion.init(.running),
        child_tid: std.atomic.Value(i32) = std.atomic.Value(i32).init(1),
        parent_tid: i32 = undefined,
        mapped: []align(std.heap.page_size_min) u8,

        /// Calls `munmap(mapped.ptr, mapped.len)` then `exit(1)` without touching the stack (which lives in `mapped.ptr`).
        /// Ported over from musl libc's pthread detached implementation:
        /// https://github.com/ifduyue/musl/search?q=__unmapself
        fn freeAndExit(self: *ThreadCompletion) noreturn {
            // If we do not reset the child_tidptr to null here, the kernel would later write the
            // value zero to that address, which is inside the block we're unmapping below, after
            // our thread exits.  This can sometimes corrupt memory in other mmap blocks from
            // unrelated concurrent threads.
            _ = linux.set_tid_address(null);
            // If a signal were delivered between SYS_munmap and SYS_exit, any installed signal
            // handler would immediately segfault due to the stack being unmapped. To avoid this,
            // we need to mask all signals before entering the inline asm.
            posix.sigprocmask(std.posix.SIG.BLOCK, &std.os.linux.sigfillset(), null);
            switch (target.cpu.arch) {
                .x86 => asm volatile (
                    \\  movl $91, %%eax # SYS_munmap
                    \\  int $128
                    \\  movl $1, %%eax # SYS_exit
                    \\  movl $0, %%ebx
                    \\  int $128
                    :
                    : [ptr] "{ebx}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{ecx}" (self.mapped.len),
                ),
                .x86_64 => asm volatile (switch (target.abi) {
                        .gnux32, .muslx32 =>
                        \\  movl $0x4000000b, %%eax # SYS_munmap
                        \\  syscall
                        \\  movl $0x4000003c, %%eax # SYS_exit
                        \\  xor %%rdi, %%rdi
                        \\  syscall
                        ,
                        else =>
                        \\  movl $11, %%eax # SYS_munmap
                        \\  syscall
                        \\  movl $60, %%eax # SYS_exit
                        \\  xor %%rdi, %%rdi
                        \\  syscall
                        ,
                    }
                    :
                    : [ptr] "{rdi}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{rsi}" (self.mapped.len),
                ),
                .arm, .armeb, .thumb, .thumbeb => asm volatile (
                    \\  mov r7, #91 // SYS_munmap
                    \\  svc 0
                    \\  mov r7, #1 // SYS_exit
                    \\  mov r0, #0
                    \\  svc 0
                    :
                    : [ptr] "{r0}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r1}" (self.mapped.len),
                ),
                .aarch64, .aarch64_be => asm volatile (
                    \\  mov x8, #215 // SYS_munmap
                    \\  svc 0
                    \\  mov x8, #93 // SYS_exit
                    \\  mov x0, #0
                    \\  svc 0
                    :
                    : [ptr] "{x0}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{x1}" (self.mapped.len),
                ),
                .alpha => asm volatile (
                    \\ ldi $0, 73 # SYS_munmap
                    \\ callsys
                    \\ ldi $0, 1 # SYS_exit
                    \\ ldi $16, 0
                    \\ callsys
                    :
                    : [ptr] "{r16}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r17}" (self.mapped.len),
                ),
                .hexagon => asm volatile (
                    \\  r6 = #215 // SYS_munmap
                    \\  trap0(#1)
                    \\  r6 = #93 // SYS_exit
                    \\  r0 = #0
                    \\  trap0(#1)
                    :
                    : [ptr] "{r0}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r1}" (self.mapped.len),
                ),
                .hppa => asm volatile (
                    \\ ldi 91, %%r20 /* SYS_munmap */
                    \\ ble 0x100(%%sr2, %%r0)
                    \\ ldi 1, %%r20 /* SYS_exit */
                    \\ ldi 0, %%r26
                    \\ ble 0x100(%%sr2, %%r0)
                    :
                    : [ptr] "{r26}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r25}" (self.mapped.len),
                ),
                .m68k => asm volatile (
                    \\ move.l #91, %%d0 // SYS_munmap
                    \\ trap #0
                    \\ move.l #1, %%d0 // SYS_exit
                    \\ move.l #0, %%d1
                    \\ trap #0
                    :
                    : [ptr] "{d1}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{d2}" (self.mapped.len),
                ),
                .microblaze, .microblazeel => asm volatile (
                    \\ ori r12, r0, 91 # SYS_munmap
                    \\ brki r14, 0x8
                    \\ ori r12, r0, 1 # SYS_exit
                    \\ or r5, r0, r0
                    \\ brki r14, 0x8
                    :
                    : [ptr] "{r5}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r6}" (self.mapped.len),
                ),
                // We set `sp` to the address of the current function as a workaround for a Linux
                // kernel bug that caused syscalls to return EFAULT if the stack pointer is invalid.
                // The bug was introduced in 46e12c07b3b9603c60fc1d421ff18618241cb081 and fixed in
                // 7928eb0370d1133d0d8cd2f5ddfca19c309079d5.
                .mips, .mipsel => asm volatile (
                    \\ move $sp, $t9
                    \\ li $v0, 4091 # SYS_munmap
                    \\ syscall
                    \\ li $v0, 4001 # SYS_exit
                    \\ li $a0, 0
                    \\ syscall
                    :
                    : [ptr] "{$4}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{$5}" (self.mapped.len),
                ),
                .mips64, .mips64el => asm volatile (switch (target.abi) {
                        .gnuabin32, .muslabin32 =>
                        \\ li $v0, 6011 # SYS_munmap
                        \\ syscall
                        \\ li $v0, 6058 # SYS_exit
                        \\ li $a0, 0
                        \\ syscall
                        ,
                        else =>
                        \\ li $v0, 5011 # SYS_munmap
                        \\ syscall
                        \\ li $v0, 5058 # SYS_exit
                        \\ li $a0, 0
                        \\ syscall
                        ,
                    }
                    :
                    : [ptr] "{$4}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{$5}" (self.mapped.len),
                ),
                .or1k => asm volatile (
                    \\ l.ori r11, r0, 215 # SYS_munmap
                    \\ l.sys 1
                    \\ l.ori r11, r0, 93 # SYS_exit
                    \\ l.ori r3, r0, r0
                    \\ l.sys 1
                    :
                    : [ptr] "{r3}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r4}" (self.mapped.len),
                ),
                .powerpc, .powerpcle, .powerpc64, .powerpc64le => asm volatile (
                    \\  li 0, 91 # SYS_munmap
                    \\  sc
                    \\  li 0, 1 # SYS_exit
                    \\  li 3, 0
                    \\  sc
                    \\  blr
                    :
                    : [ptr] "{r3}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r4}" (self.mapped.len),
                ),
                .riscv32, .riscv64 => asm volatile (
                    \\  li a7, 215 # SYS_munmap
                    \\  ecall
                    \\  li a7, 93 # SYS_exit
                    \\  mv a0, zero
                    \\  ecall
                    :
                    : [ptr] "{a0}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{a1}" (self.mapped.len),
                ),
                .s390x => asm volatile (
                    \\  svc 91 # SYS_munmap
                    \\  lghi %%r2, 0
                    \\  svc 1 # SYS_exit
                    :
                    : [ptr] "{r2}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r3}" (self.mapped.len),
                ),
                .sh, .sheb => asm volatile (
                    \\ mov #91, r3 ! SYS_munmap
                    \\ trapa #31
                    \\ or r0, r0
                    \\ or r0, r0
                    \\ or r0, r0
                    \\ or r0, r0
                    \\ or r0, r0
                    \\ mov #1, r3 ! SYS_exit
                    \\ mov #0, r4
                    \\ trapa #31
                    \\ or r0, r0
                    \\ or r0, r0
                    \\ or r0, r0
                    \\ or r0, r0
                    \\ or r0, r0
                    :
                    : [ptr] "{r4}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r5}" (self.mapped.len),
                ),
                .sparc => asm volatile (
                    \\ # See sparc64 comments below.
                    \\ 1:
                    \\  cmp %%fp, 0
                    \\  beq 2f
                    \\  nop
                    \\  ba 1b
                    \\  restore
                    \\ 2:
                    \\  mov %%g1, %%o0 // ptr
                    \\  mov %%g2, %%o1 // len
                    \\  mov 73, %%g1 // SYS_munmap
                    \\  t 0x3 # ST_FLUSH_WINDOWS
                    \\  t 0x10
                    \\  mov 1, %%g1 // SYS_exit
                    \\  mov 0, %%o0
                    \\  t 0x10
                    :
                    : [ptr] "{g1}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{g2}" (self.mapped.len),
                    : .{ .memory = true }),
                .sparc64 => asm volatile (
                    \\ # SPARCs really don't like it when active stack frames
                    \\ # is unmapped (it will result in a segfault), so we
                    \\ # force-deactivate it by running `restore` until
                    \\ # all frames are cleared.
                    \\ 1:
                    \\  cmp %%fp, 0
                    \\  beq 2f
                    \\  nop
                    \\  ba 1b
                    \\  restore
                    \\ 2:
                    \\  mov %%g1, %%o0 // ptr
                    \\  mov %%g2, %%o1 // len
                    \\  mov 73, %%g1 // SYS_munmap
                    \\  # Flush register window contents to prevent background
                    \\  # memory access before unmapping the stack.
                    \\  flushw
                    \\  t 0x6d
                    \\  mov 1, %%g1 // SYS_exit
                    \\  mov 0, %%o0
                    \\  t 0x6d
                    :
                    : [ptr] "{g1}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{g2}" (self.mapped.len),
                    : .{ .memory = true }),
                .loongarch32, .loongarch64 => asm volatile (
                    \\ ori     $a7, $zero, 215     # SYS_munmap
                    \\ syscall 0                   # call munmap
                    \\ ori     $a0, $zero, 0
                    \\ ori     $a7, $zero, 93      # SYS_exit
                    \\ syscall 0                   # call exit
                    :
                    : [ptr] "{r4}" (@intFromPtr(self.mapped.ptr)),
                      [len] "{r5}" (self.mapped.len),
                    : .{ .memory = true }),
                else => |cpu_arch| @compileError("Unsupported linux arch: " ++ @tagName(cpu_arch)),
            }
            unreachable;
        }
    };

    fn spawn(config: SpawnConfig, comptime f: anytype, args: anytype) !Impl {
        const page_size = std.heap.pageSize();
        const Args = @TypeOf(args);
        const Instance = struct {
            fn_args: Args,
            thread: ThreadCompletion,

            fn entryFn(raw_arg: usize) callconv(.c) u8 {
                const self = @as(*@This(), @ptrFromInt(raw_arg));
                defer switch (self.thread.completion.swap(.completed, .seq_cst)) {
                    .running => {},
                    .completed => unreachable,
                    .detached => self.thread.freeAndExit(),
                };
                return callFn(f, self.fn_args);
            }
        };

        var guard_offset: usize = undefined;
        var stack_offset: usize = undefined;
        var tls_offset: usize = undefined;
        var instance_offset: usize = undefined;

        const map_bytes = blk: {
            var bytes: usize = page_size;
            guard_offset = bytes;

            bytes += @max(page_size, config.stack_size);
            bytes = std.mem.alignForward(usize, bytes, page_size);
            stack_offset = bytes;

            bytes = std.mem.alignForward(usize, bytes, linux.tls.area_desc.alignment);
            tls_offset = bytes;
            bytes += linux.tls.area_desc.size;

            bytes = std.mem.alignForward(usize, bytes, @alignOf(Instance));
            instance_offset = bytes;
            bytes += @sizeOf(Instance);

            bytes = std.mem.alignForward(usize, bytes, page_size);
            break :blk bytes;
        };

        // map all memory needed without read/write permissions
        // to avoid committing the whole region right away
        // anonymous mapping ensures file descriptor limits are not exceeded
        const mapped = posix.mmap(
            null,
            map_bytes,
            .{},
            .{ .TYPE = .PRIVATE, .ANONYMOUS = true },
            -1,
            0,
        ) catch |err| switch (err) {
            error.MemoryMappingNotSupported => unreachable,
            error.AccessDenied => unreachable,
            error.PermissionDenied => unreachable,
            error.ProcessFdQuotaExceeded => unreachable,
            error.SystemFdQuotaExceeded => unreachable,
            error.MappingAlreadyExists => unreachable,
            else => |e| return e,
        };
        assert(mapped.len >= map_bytes);
        errdefer posix.munmap(mapped);

        // Map everything but the guard page as read/write.
        const guarded: []align(std.heap.page_size_min) u8 = @alignCast(mapped[guard_offset..]);
        const protection: posix.PROT = .{ .READ = true, .WRITE = true };
        switch (posix.errno(posix.system.mprotect(guarded.ptr, guarded.len, protection))) {
            .SUCCESS => {},
            .NOMEM => return error.OutOfMemory,
            else => |err| return posix.unexpectedErrno(err),
        }

        // Prepare the TLS segment and prepare a user_desc struct when needed on x86
        var tls_ptr = linux.tls.prepareArea(mapped[tls_offset..][0..linux.tls.area_desc.size]);
        var user_desc: if (target.cpu.arch == .x86) linux.user_desc else void = undefined;
        if (target.cpu.arch == .x86) {
            defer tls_ptr = @intFromPtr(&user_desc);
            user_desc = .{
                .entry_number = linux.tls.area_desc.gdt_entry_number,
                .base_addr = tls_ptr,
                .limit = 0xfffff,
                .flags = .{
                    .seg_32bit = 1,
                    .contents = 0, // Data
                    .read_exec_only = 0,
                    .limit_in_pages = 1,
                    .seg_not_present = 0,
                    .useable = 1,
                },
            };
        }

        const instance: *Instance = @ptrCast(@alignCast(&mapped[instance_offset]));
        instance.* = .{
            .fn_args = args,
            .thread = .{ .mapped = mapped },
        };

        const flags: u32 = linux.CLONE.THREAD | linux.CLONE.DETACHED |
            linux.CLONE.VM | linux.CLONE.FS | linux.CLONE.FILES |
            linux.CLONE.PARENT_SETTID | linux.CLONE.CHILD_CLEARTID |
            linux.CLONE.SIGHAND | linux.CLONE.SYSVSEM | linux.CLONE.SETTLS;

        switch (linux.errno(linux.clone(
            Instance.entryFn,
            @intFromPtr(&mapped[stack_offset]),
            flags,
            @intFromPtr(instance),
            &instance.thread.parent_tid,
            tls_ptr,
            &instance.thread.child_tid.raw,
        ))) {
            .SUCCESS => return Impl{ .thread = &instance.thread },

```
