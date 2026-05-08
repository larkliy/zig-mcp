```
   days_left -= days_in_month;
            month = @as(Month, @enumFromInt(@intFromEnum(month) + 1));
        }
        return .{ .month = month, .day_index = @as(u5, @intCast(days_left)) };
    }
};

pub const MonthAndDay = struct {
    month: Month,
    day_index: u5, // days into the month (0 to 30)
};

/// days since epoch Jan 1, 1970
pub const EpochDay = struct {
    day: u47, // u47 = u64 - u17 (because day = sec(u64) / secs_per_day(u17)
    pub fn calculateYearDay(self: EpochDay) YearAndDay {
        var year_day = self.day;
        var year: Year = epoch_year;
        while (true) {
            const year_size = getDaysInYear(year);
            if (year_day < year_size)
                break;
            year_day -= year_size;
            year += 1;
        }
        return .{ .year = year, .day = @as(u9, @intCast(year_day)) };
    }
};

/// seconds since start of day
pub const DaySeconds = struct {
    secs: u17, // max is 24*60*60 = 86400

    /// the number of hours past the start of the day (0 to 23)
    pub fn getHoursIntoDay(self: DaySeconds) u5 {
        return @as(u5, @intCast(@divTrunc(self.secs, 3600)));
    }
    /// the number of minutes past the hour (0 to 59)
    pub fn getMinutesIntoHour(self: DaySeconds) u6 {
        return @as(u6, @intCast(@divTrunc(@mod(self.secs, 3600), 60)));
    }
    /// the number of seconds past the start of the minute (0 to 59)
    pub fn getSecondsIntoMinute(self: DaySeconds) u6 {
        return math.comptimeMod(self.secs, 60);
    }
};

/// seconds since epoch Jan 1, 1970 at 12:00 AM
pub const EpochSeconds = struct {
    secs: u64,

    /// Returns the number of days since the epoch as an EpochDay.
    /// Use EpochDay to get information about the day of this time.
    pub fn getEpochDay(self: EpochSeconds) EpochDay {
        return EpochDay{ .day = @as(u47, @intCast(@divTrunc(self.secs, secs_per_day))) };
    }

    /// Returns the number of seconds into the day as DaySeconds.
    /// Use DaySeconds to get information about the time.
    pub fn getDaySeconds(self: EpochSeconds) DaySeconds {
        return DaySeconds{ .secs = math.comptimeMod(self.secs, secs_per_day) };
    }
};

fn testEpoch(secs: u64, expected_year_day: YearAndDay, expected_month_day: MonthAndDay, expected_day_seconds: struct {
    /// 0 to 23
    hours_into_day: u5,
    /// 0 to 59
    minutes_into_hour: u6,
    /// 0 to 59
    seconds_into_minute: u6,
}) !void {
    const epoch_seconds = EpochSeconds{ .secs = secs };
    const epoch_day = epoch_seconds.getEpochDay();
    const day_seconds = epoch_seconds.getDaySeconds();
    const year_day = epoch_day.calculateYearDay();
    try testing.expectEqual(expected_year_day, year_day);
    try testing.expectEqual(expected_month_day, year_day.calculateMonthDay());
    try testing.expectEqual(expected_day_seconds.hours_into_day, day_seconds.getHoursIntoDay());
    try testing.expectEqual(expected_day_seconds.minutes_into_hour, day_seconds.getMinutesIntoHour());
    try testing.expectEqual(expected_day_seconds.seconds_into_minute, day_seconds.getSecondsIntoMinute());
}

test "epoch decoding" {
    try testEpoch(0, .{ .year = 1970, .day = 0 }, .{
        .month = .jan,
        .day_index = 0,
    }, .{ .hours_into_day = 0, .minutes_into_hour = 0, .seconds_into_minute = 0 });

    try testEpoch(31535999, .{ .year = 1970, .day = 364 }, .{
        .month = .dec,
        .day_index = 30,
    }, .{ .hours_into_day = 23, .minutes_into_hour = 59, .seconds_into_minute = 59 });

    try testEpoch(1622924906, .{ .year = 2021, .day = 31 + 28 + 31 + 30 + 31 + 4 }, .{
        .month = .jun,
        .day_index = 4,
    }, .{ .hours_into_day = 20, .minutes_into_hour = 28, .seconds_into_minute = 26 });

    try testEpoch(1625159473, .{ .year = 2021, .day = 31 + 28 + 31 + 30 + 31 + 30 }, .{
        .month = .jul,
        .day_index = 0,
    }, .{ .hours_into_day = 17, .minutes_into_hour = 11, .seconds_into_minute = 13 });
}



---
File: /std/unicode/throughput_test.zig
---




---
File: /std/valgrind/cachegrind.zig
---

const std = @import("../std.zig");
const valgrind = std.valgrind;

pub const ClientRequest = enum(usize) {
    StartInstrumentation = valgrind.ToolBase("CG".*),
    StopInstrumentation,
};

fn doClientRequestExpr(default: usize, request: ClientRequest, a1: usize, a2: usize, a3: usize, a4: usize, a5: usize) usize {
    return valgrind.doClientRequest(default, @as(usize, @intCast(@intFromEnum(request))), a1, a2, a3, a4, a5);
}

fn doClientRequestStmt(request: ClientRequest, a1: usize, a2: usize, a3: usize, a4: usize, a5: usize) void {
    _ = doClientRequestExpr(0, request, a1, a2, a3, a4, a5);
}

/// Start Cachegrind instrumentation if not already enabled. Use this in
/// combination with `std.valgrind.cachegrind.stopInstrumentation` and
/// `--instr-at-start` to measure only part of a client program's execution.
pub fn startInstrumentation() void {
    doClientRequestStmt(.StartInstrumentation, 0, 0, 0, 0, 0);
}

/// Stop Cachegrind instrumentation if not already disabled. Use this in
/// combination with `std.valgrind.cachegrind.startInstrumentation` and
/// `--instr-at-start` to measure only part of a client program's execution.
pub fn stopInstrumentation() void {
    doClientRequestStmt(.StopInstrumentation, 0, 0, 0, 0, 0);
}



---
File: /std/valgrind/callgrind.zig
---

const std = @import("../std.zig");
const valgrind = std.valgrind;

pub const ClientRequest = enum(usize) {
    DumpStats = valgrind.ToolBase("CT".*),
    ZeroStats,
    ToggleCollect,
    DumpStatsAt,
    StartInstrumentation,
    StopInstrumentation,
};

fn doClientRequestExpr(default: usize, request: ClientRequest, a1: usize, a2: usize, a3: usize, a4: usize, a5: usize) usize {
    return valgrind.doClientRequest(default, @as(usize, @intCast(@intFromEnum(request))), a1, a2, a3, a4, a5);
}

fn doClientRequestStmt(request: ClientRequest, a1: usize, a2: usize, a3: usize, a4: usize, a5: usize) void {
    _ = doClientRequestExpr(0, request, a1, a2, a3, a4, a5);
}

/// Dump current state of cost centers, and zero them afterwards
pub fn dumpStats() void {
    doClientRequestStmt(.DumpStats, 0, 0, 0, 0, 0);
}

/// Dump current state of cost centers, and zero them afterwards.
/// The argument is appended to a string stating the reason which triggered
/// the dump. This string is written as a description field into the
/// profile data dump.
pub fn dumpStatsAt(pos_str: [*:0]const u8) void {
    doClientRequestStmt(.DumpStatsAt, @intFromPtr(pos_str), 0, 0, 0, 0);
}

/// Zero cost centers
pub fn zeroStats() void {
    doClientRequestStmt(.ZeroStats, 0, 0, 0, 0, 0);
}

/// Toggles collection state.
/// The collection state specifies whether the happening of events
/// should be noted or if they are to be ignored. Events are noted
/// by increment of counters in a cost center
pub fn toggleCollect() void {
    doClientRequestStmt(.ToggleCollect, 0, 0, 0, 0, 0);
}

/// Start full callgrind instrumentation if not already switched on.
/// When cache simulation is done, it will flush the simulated cache;
/// this will lead to an artificial cache warmup phase afterwards with
/// cache misses which would not have happened in reality.
pub fn startInstrumentation() void {
    doClientRequestStmt(.StartInstrumentation, 0, 0, 0, 0, 0);
}

/// Stop full callgrind instrumentation if not already switched off.
/// This flushes Valgrinds translation cache, and does no additional
/// instrumentation afterwards, which effectively will run at the same
/// speed as the "none" tool (ie. at minimal slowdown).
/// Use this to bypass Callgrind aggregation for uninteresting code parts.
/// To start Callgrind in this mode to ignore the setup phase, use
/// the option "--instr-atstart=no".
pub fn stopInstrumentation() void {
    doClientRequestStmt(.StopInstrumentation, 0, 0, 0, 0, 0);
}



---
File: /std/valgrind/memcheck.zig
---

const std = @import("../std.zig");
const testing = std.testing;
const valgrind = std.valgrind;

pub const ClientRequest = enum(usize) {
    MakeMemNoAccess = valgrind.ToolBase("MC".*),
    MakeMemUndefined,
    MakeMemDefined,
    Discard,
    CheckMemIsAddressable,
    CheckMemIsDefined,
    DoLeakCheck,
    CountLeaks,
    GetVbits,
    SetVbits,
    CreateBlock,
    MakeMemDefinedIfAddressable,
    CountLeakBlocks,
    EnableAddrErrorReportingInRange,
    DisableAddrErrorReportingInRange,
};

fn doClientRequestExpr(default: usize, request: ClientRequest, a1: usize, a2: usize, a3: usize, a4: usize, a5: usize) usize {
    return valgrind.doClientRequest(default, @as(usize, @intCast(@intFromEnum(request))), a1, a2, a3, a4, a5);
}

fn doClientRequestStmt(request: ClientRequest, a1: usize, a2: usize, a3: usize, a4: usize, a5: usize) void {
    _ = doClientRequestExpr(0, request, a1, a2, a3, a4, a5);
}

/// Mark memory at qzz.ptr as unaddressable for qzz.len bytes.
pub fn makeMemNoAccess(qzz: []const u8) void {
    _ = doClientRequestExpr(0, // default return
        .MakeMemNoAccess, @intFromPtr(qzz.ptr), qzz.len, 0, 0, 0);
}

/// Mark memory at qzz.ptr as addressable but undefined for qzz.len bytes.
pub fn makeMemUndefined(qzz: []const u8) void {
    _ = doClientRequestExpr(0, // default return
        .MakeMemUndefined, @intFromPtr(qzz.ptr), qzz.len, 0, 0, 0);
}

/// Mark memory at qzz.ptr as addressable and defined or qzz.len bytes.
pub fn makeMemDefined(qzz: []const u8) void {
    _ = doClientRequestExpr(0, // default return
        .MakeMemDefined, @intFromPtr(qzz.ptr), qzz.len, 0, 0, 0);
}

/// Similar to makeMemDefined except that addressability is
/// not altered: bytes which are addressable are marked as defined,
/// but those which are not addressable are left unchanged.
pub fn makeMemDefinedIfAddressable(qzz: []const u8) void {
    _ = doClientRequestExpr(0, // default return
        .MakeMemDefinedIfAddressable, @intFromPtr(qzz.ptr), qzz.len, 0, 0, 0);
}

/// Create a block-description handle.  The description is an ascii
/// string which is included in any messages pertaining to addresses
/// within the specified memory range.  Has no other effect on the
/// properties of the memory range.
pub fn createBlock(qzz: []const u8, desc: [*:0]const u8) usize {
    return doClientRequestExpr(0, // default return
        .CreateBlock, @intFromPtr(qzz.ptr), qzz.len, @intFromPtr(desc), 0, 0);
}

/// Discard a block-description-handle. Returns 1 for an
/// invalid handle, 0 for a valid handle.
pub fn discard(blkindex: usize) bool {
    return doClientRequestExpr(0, // default return
        .Discard, 0, blkindex, 0, 0, 0) != 0;
}

/// Check that memory at qzz.ptr is addressable for qzz.len bytes.
/// If suitable addressability is not established, Valgrind prints an
/// error message and returns the address of the first offending byte.
/// Otherwise it returns zero.
pub fn checkMemIsAddressable(qzz: []const u8) usize {
    return doClientRequestExpr(0, .CheckMemIsAddressable, @intFromPtr(qzz.ptr), qzz.len, 0, 0, 0);
}

/// Check that memory at qzz.ptr is addressable and defined for
/// qzz.len bytes.  If suitable addressability and definedness are not
/// established, Valgrind prints an error message and returns the
/// address of the first offending byte.  Otherwise it returns zero.
pub fn checkMemIsDefined(qzz: []const u8) usize {
    return doClientRequestExpr(0, .CheckMemIsDefined, @intFromPtr(qzz.ptr), qzz.len, 0, 0, 0);
}

/// Do a full memory leak check (like --leak-check=full) mid-execution.
pub fn doLeakCheck() void {
    doClientRequestStmt(.DoLeakCheck, 0, 0, 0, 0, 0);
}

/// Same as doLeakCheck() but only showing the entries for
/// which there was an increase in leaked bytes or leaked nr of blocks
/// since the previous leak search.
pub fn doAddedLeakCheck() void {
    doClientRequestStmt(.DoLeakCheck, 0, 1, 0, 0, 0);
}

/// Same as doAddedLeakCheck() but showing entries with
/// increased or decreased leaked bytes/blocks since previous leak
/// search.
pub fn doChangedLeakCheck() void {
    doClientRequestStmt(.DoLeakCheck, 0, 2, 0, 0, 0);
}

/// Do a summary memory leak check (like --leak-check=summary) mid-execution.
pub fn doQuickLeakCheck() void {
    doClientRequestStmt(.DoLeakCheck, 1, 0, 0, 0, 0);
}

/// Return number of leaked, dubious, reachable and suppressed bytes found by
/// all previous leak checks.
const CountResult = struct {
    leaked: usize,
    dubious: usize,
    reachable: usize,
    suppressed: usize,
};

pub fn countLeaks() CountResult {
    var res: CountResult = .{
        .leaked = 0,
        .dubious = 0,
        .reachable = 0,
        .suppressed = 0,
    };
    doClientRequestStmt(
        .CountLeaks,
        @intFromPtr(&res.leaked),
        @intFromPtr(&res.dubious),
        @intFromPtr(&res.reachable),
        @intFromPtr(&res.suppressed),
        0,
    );
    return res;
}

test countLeaks {
    try testing.expectEqual(
        @as(CountResult, .{
            .leaked = 0,
            .dubious = 0,
            .reachable = 0,
            .suppressed = 0,
        }),
        countLeaks(),
    );
}

pub fn countLeakBlocks() CountResult {
    var res: CountResult = .{
        .leaked = 0,
        .dubious = 0,
        .reachable = 0,
        .suppressed = 0,
    };
    doClientRequestStmt(
        .CountLeakBlocks,
        @intFromPtr(&res.leaked),
        @intFromPtr(&res.dubious),
        @intFromPtr(&res.reachable),
        @intFromPtr(&res.suppressed),
        0,
    );
    return res;
}

test countLeakBlocks {
    try testing.expectEqual(
        @as(CountResult, .{
            .leaked = 0,
            .dubious = 0,
            .reachable = 0,
            .suppressed = 0,
        }),
        countLeakBlocks(),
    );
}

/// Get the validity data for addresses zza and copy it
/// into the provided zzvbits array.  Return values:
///    0   if not running on valgrind
///    1   success
///    2   [previously indicated unaligned arrays;  these are now allowed]
///    3   if any parts of zzsrc/zzvbits are not addressable.
/// The metadata is not copied in cases 0, 2 or 3 so it should be
/// impossible to segfault your system by using this call.
pub fn getVbits(zza: []u8, zzvbits: []u8) u2 {
    std.debug.assert(zzvbits.len >= zza.len / 8);
    return @as(u2, @intCast(doClientRequestExpr(0, .GetVbits, @intFromPtr(zza.ptr), @intFromPtr(zzvbits.ptr), zza.len, 0, 0)));
}

/// Set the validity data for addresses zza, copying it
/// from the provided zzvbits array.  Return values:
///    0   if not running on valgrind
///    1   success
///    2   [previously indicated unaligned arrays;  these are now allowed]
///    3   if any parts of zza/zzvbits are not addressable.
/// The metadata is not copied in cases 0, 2 or 3 so it should be
/// impossible to segfault your system by using this call.
pub fn setVbits(zzvbits: []u8, zza: []u8) u2 {
    std.debug.assert(zzvbits.len >= zza.len / 8);
    return @as(u2, @intCast(doClientRequestExpr(0, .SetVbits, @intFromPtr(zza.ptr), @intFromPtr(zzvbits.ptr), zza.len, 0, 0)));
}

/// Disable and re-enable reporting of addressing errors in the
/// specified address range.
pub fn disableAddrErrorReportingInRange(qzz: []u8) usize {
    return doClientRequestExpr(0, // default return
        .DisableAddrErrorReportingInRange, @intFromPtr(qzz.ptr), qzz.len, 0, 0, 0);
}

pub fn enableAddrErrorReportingInRange(qzz: []u8) usize {
    return doClientRequestExpr(0, // default return
        .EnableAddrErrorReportingInRange, @intFromPtr(qzz.ptr), qzz.len, 0, 0, 0);
}



---
File: /std/zig/Ast/Render.zig
---

const std = @import("../../std.zig");
const assert = std.debug.assert;
const mem = std.mem;
const Allocator = std.mem.Allocator;
const meta = std.meta;
const Ast = std.zig.Ast;
const Token = std.zig.Token;
const primitives = std.zig.primitives;
const Writer = std.Io.Writer;

const Render = @This();

gpa: Allocator,
ais: *AutoIndentingStream,
tree: Ast,
fixups: Fixups,

const indent_delta = 4;
const asm_indent_delta = 2;

pub const Error = error{
    /// Ran out of memory allocating call stack frames to complete rendering.
    OutOfMemory,
    /// Transitive failure from
    WriteFailed,
};

pub const Fixups = struct {
    /// The key is the mut token (`var`/`const`) of the variable declaration
    /// that should have a `_ = foo;` inserted afterwards.
    unused_var_decls: std.AutoHashMapUnmanaged(Ast.TokenIndex, void) = .empty,
    /// The functions in this unordered set of AST fn decl nodes will render
    /// with a function body of `@trap()` instead, with all parameters
    /// discarded.
    gut_functions: std.AutoHashMapUnmanaged(Ast.Node.Index, void) = .empty,
    /// These global declarations will be omitted.
    omit_nodes: std.AutoHashMapUnmanaged(Ast.Node.Index, void) = .empty,
    /// These expressions will be replaced with the string value.
    replace_nodes_with_string: std.AutoHashMapUnmanaged(Ast.Node.Index, []const u8) = .empty,
    /// The string value will be inserted directly after the node.
    append_string_after_node: std.AutoHashMapUnmanaged(Ast.Node.Index, []const u8) = .empty,
    /// These nodes will be replaced with a different node.
    replace_nodes_with_node: std.AutoHashMapUnmanaged(Ast.Node.Index, Ast.Node.Index) = .empty,
    /// Change all identifier names matching the key to be value instead.
    rename_identifiers: std.StringArrayHashMapUnmanaged([]const u8) = .empty,

    /// All `@import` builtin calls which refer to a file path will be prefixed
    /// with this path.
    rebase_imported_paths: ?[]const u8 = null,

    pub fn count(f: Fixups) usize {
        return f.unused_var_decls.count() +
            f.gut_functions.count() +
            f.omit_nodes.count() +
            f.replace_nodes_with_string.count() +
            f.append_string_after_node.count() +
            f.replace_nodes_with_node.count() +
            f.rename_identifiers.count() +
            @intFromBool(f.rebase_imported_paths != null);
    }

    pub fn clearRetainingCapacity(f: *Fixups) void {
        f.unused_var_decls.clearRetainingCapacity();
        f.gut_functions.clearRetainingCapacity();
        f.omit_nodes.clearRetainingCapacity();
        f.replace_nodes_with_string.clearRetainingCapacity();
        f.append_string_after_node.clearRetainingCapacity();
        f.replace_nodes_with_node.clearRetainingCapacity();
        f.rename_identifiers.clearRetainingCapacity();

        f.rebase_imported_paths = null;
    }

    pub fn deinit(f: *Fixups, gpa: Allocator) void {
        f.unused_var_decls.deinit(gpa);
        f.gut_functions.deinit(gpa);
        f.omit_nodes.deinit(gpa);
        f.replace_nodes_with_string.deinit(gpa);
        f.append_string_after_node.deinit(gpa);
        f.replace_nodes_with_node.deinit(gpa);
        f.rename_identifiers.deinit(gpa);
        f.* = undefined;
    }
};

pub fn renderTree(gpa: Allocator, w: *Writer, tree: Ast, fixups: Fixups) Error!void {
    assert(tree.errors.len == 0); // Cannot render an invalid tree.
    var auto_indenting_stream: AutoIndentingStream = .init(gpa, w, indent_delta);
    defer auto_indenting_stream.deinit();
    var r: Render = .{
        .gpa = gpa,
        .ais = &auto_indenting_stream,
        .tree = tree,
        .fixups = fixups,
    };

    // Render all the line comments at the beginning of the file.
    const comment_end_loc = tree.tokenStart(0);
    _ = try renderComments(&r, 0, comment_end_loc);

    if (tree.tokenTag(0) == .container_doc_comment) {
        try renderContainerDocComments(&r, 0);
    }

    switch (tree.mode) {
        .zig => try renderMembers(&r, tree.rootDecls()),
        .zon => {
            try renderExpression(
                &r,
                tree.rootDecls()[0],
                .newline,
            );
        },
    }

    if (auto_indenting_stream.disabled_offset) |disabled_offset| {
        try writeFixingWhitespace(auto_indenting_stream.underlying_writer, tree.source[disabled_offset..]);
    }
}

/// Render all members in the given slice, keeping empty lines where appropriate
fn renderMembers(r: *Render, members: []const Ast.Node.Index) Error!void {
    const tree = r.tree;
    if (members.len == 0) return;
    const container: Container = for (members) |member| {
        if (tree.fullContainerField(member)) |field| if (!field.ast.tuple_like) break .other;
    } else .tuple;
    try renderMember(r, container, members[0], .newline);
    for (members[1..]) |member| {
        try renderExtraNewline(r, member);
        try renderMember(r, container, member, .newline);
    }
}

const Container = enum {
    @"enum",
    tuple,
    other,
};

fn renderMember(
    r: *Render,
    container: Container,
    decl: Ast.Node.Index,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    if (r.fixups.omit_nodes.contains(decl)) return;
    try renderDocComments(r, tree.firstToken(decl));
    switch (tree.nodeTag(decl)) {
        .fn_decl => {
            // Some examples:
            // pub extern "foo" fn ...
            // export fn ...
            const fn_proto, const body_node = tree.nodeData(decl).node_and_node;
            const fn_token = tree.nodeMainToken(fn_proto);
            // Go back to the first token we should render here.
            var i = fn_token;
            while (i > 0) {
                i -= 1;
                switch (tree.tokenTag(i)) {
                    .keyword_extern,
                    .keyword_export,
                    .keyword_pub,
                    .string_literal,
                    .keyword_inline,
                    .keyword_noinline,
                    => continue,

                    else => {
                        i += 1;
                        break;
                    },
                }
            }

            while (i < fn_token) : (i += 1) {
                try renderToken(r, i, .space);
            }
            switch (tree.nodeTag(fn_proto)) {
                .fn_proto_one, .fn_proto => {
                    var buf: [1]Ast.Node.Index = undefined;
                    const opt_callconv_expr = if (tree.nodeTag(fn_proto) == .fn_proto_one)
                        tree.fnProtoOne(&buf, fn_proto).ast.callconv_expr
                    else
                        tree.fnProto(fn_proto).ast.callconv_expr;

                    // Keep in sync with logic in `renderFnProto`. Search this file for the marker PROMOTE_CALLCONV_INLINE
                    if (opt_callconv_expr.unwrap()) |callconv_expr| {
                        if (tree.nodeTag(callconv_expr) == .enum_literal) {
                            if (mem.eql(u8, "@\"inline\"", tree.tokenSlice(tree.nodeMainToken(callconv_expr)))) {
                                try ais.underlying_writer.writeAll("inline ");
                            }
                        }
                    }
                },
                .fn_proto_simple, .fn_proto_multi => {},
                else => unreachable,
            }
            try renderExpression(r, fn_proto, .space);
            if (r.fixups.gut_functions.contains(decl)) {
                try ais.pushIndent(.normal);
                const lbrace = tree.nodeMainToken(body_node);
                try renderToken(r, lbrace, .newline);
                try discardAllParams(r, fn_proto);
                try ais.writeAll("@trap();");
                ais.popIndent();
                try ais.insertNewline();
                try renderToken(r, tree.lastToken(body_node), space); // rbrace
            } else if (r.fixups.unused_var_decls.count() != 0) {
                try ais.pushIndent(.normal);
                const lbrace = tree.nodeMainToken(body_node);
                try renderToken(r, lbrace, .newline);

                var fn_proto_buf: [1]Ast.Node.Index = undefined;
                const full_fn_proto = tree.fullFnProto(&fn_proto_buf, fn_proto).?;
                var it = full_fn_proto.iterate(&tree);
                while (it.next()) |param| {
                    const name_ident = param.name_token.?;
                    assert(tree.tokenTag(name_ident) == .identifier);
                    if (r.fixups.unused_var_decls.contains(name_ident)) {
                        try ais.writeAll("_ = ");
                        try ais.writeAll(tokenSliceForRender(r.tree, name_ident));
                        try ais.writeAll(";\n");
                    }
                }
                var statements_buf: [2]Ast.Node.Index = undefined;
                const statements = tree.blockStatements(&statements_buf, body_node).?;
                return finishRenderBlock(r, body_node, statements, space);
            } else {
                return renderExpression(r, body_node, space);
            }
        },
        .fn_proto_simple,
        .fn_proto_multi,
        .fn_proto_one,
        .fn_proto,
        => {
            // Extern function prototypes are parsed as these tags.
            // Go back to the first token we should render here.
            const fn_token = tree.nodeMainToken(decl);
            var i = fn_token;
            while (i > 0) {
                i -= 1;
                switch (tree.tokenTag(i)) {
                    .keyword_extern,
                    .keyword_export,
                    .keyword_pub,
                    .string_literal,
                    .keyword_inline,
                    .keyword_noinline,
                    => continue,

                    else => {
                        i += 1;
                        break;
                    },
                }
            }
            while (i < fn_token) : (i += 1) {
                try renderToken(r, i, .space);
            }
            try renderExpression(r, decl, .none);
            return renderToken(r, tree.lastToken(decl) + 1, space); // semicolon
        },

        .global_var_decl,
        .local_var_decl,
        .simple_var_decl,
        .aligned_var_decl,
        => {
            try ais.pushSpace(.semicolon);
            try renderVarDecl(r, tree.fullVarDecl(decl).?, false, .semicolon);
            ais.popSpace();
        },

        .test_decl => {
            const test_token = tree.nodeMainToken(decl);
            const opt_name_token, const block_node = tree.nodeData(decl).opt_token_and_node;
            try renderToken(r, test_token, .space);
            if (opt_name_token.unwrap()) |name_token| {
                switch (tree.tokenTag(name_token)) {
                    .string_literal => try renderToken(r, name_token, .space),
                    .identifier => try renderIdentifier(r, name_token, .space, .preserve_when_shadowing),
                    else => unreachable,
                }
            }
            try renderExpression(r, block_node, space);
        },

        .container_field_init,
        .container_field_align,
        .container_field,
        => return renderContainerField(r, container, tree.fullContainerField(decl).?, space),

        .@"comptime" => return renderExpression(r, decl, space),

        .root => unreachable,
        else => unreachable,
    }
}

/// Render all expressions in the slice, keeping empty lines where appropriate
fn renderExpressions(r: *Render, expressions: []const Ast.Node.Index, space: Space) Error!void {
    if (expressions.len == 0) return;
    try renderExpression(r, expressions[0], space);
    for (expressions[1..]) |expression| {
        try renderExtraNewline(r, expression);
        try renderExpression(r, expression, space);
    }
}

fn renderExpression(r: *Render, node: Ast.Node.Index, space: Space) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    if (r.fixups.replace_nodes_with_string.get(node)) |replacement| {
        try ais.writeAll(replacement);
        try renderOnlySpace(r, space);
        return;
    } else if (r.fixups.replace_nodes_with_node.get(node)) |replacement| {
        return renderExpression(r, replacement, space);
    }
    switch (tree.nodeTag(node)) {
        .identifier => {
            const token_index = tree.nodeMainToken(node);
            return renderIdentifier(r, token_index, space, .preserve_when_shadowing);
        },

        .number_literal,
        .char_literal,
        .unreachable_literal,
        .anyframe_literal,
        .string_literal,
        => return renderToken(r, tree.nodeMainToken(node), space),

        .multiline_string_literal => {
            try ais.maybeInsertNewline();

            const first_tok, const last_tok = tree.nodeData(node).token_and_token;
            for (first_tok..last_tok) |i| {
                try renderToken(r, @intCast(i), .newline);
            }
            if (space != .skip) {
                try renderToken(r, last_tok, .newline);
            } else {
                try renderToken(r, last_tok, .skip);
                try ais.insertNewline(); // A newline is part of the token, so it still needs
                // rendered here.
            }

            const next_token = last_tok + 1;
            const next_token_tag = tree.tokenTag(next_token);

            // dedent the next thing that comes after a multiline string literal
            if (next_token_tag != .colon and
                !ais.indentStackEmpty() and
                ais.lastSpaceModeIndent() < ais.currentIndent())
            {
                const indent_top = &ais.indent_stack.items[ais.indent_stack.items.len - 1];
                if (indent_top.realized) {
                    indent_top.realized = false;
                    ais.indent_count -= 1;
                }
            }

            switch (space) {
                .none, .space, .newline, .maybe_space, .skip => {},
                .semicolon => if (next_token_tag == .semicolon)
                    try renderTokenOverrideSpaceMode(r, next_token, .newline, .semicolon),
                .comma => if (next_token_tag == .comma)
                    try renderTokenOverrideSpaceMode(r, next_token, .newline, .comma),
                .comma_space => if (next_token_tag == .comma)
                    try renderToken(r, next_token, .space),
                .comma_maybe_space => if (next_token_tag == .comma)
                    try renderToken(r, next_token, .maybe_space),
            }
        },

        .error_value => {
            const main_token = tree.nodeMainToken(node);
            try renderToken(r, main_token, .none);
            try renderToken(r, main_token + 1, .none);
            return renderIdentifier(r, main_token + 2, space, .eagerly_unquote);
        },

        .block_two,
        .block_two_semicolon,
        .block,
        .block_semicolon,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const statements = tree.blockStatements(&buf, node).?;
            return renderBlock(r, node, statements, space);
        },

        .@"errdefer" => {
            const defer_token = tree.nodeMainToken(node);
            const maybe_payload_token, const expr = tree.nodeData(node).opt_token_and_node;

            try renderToken(r, defer_token, .maybe_space);
            if (maybe_payload_token.unwrap()) |payload_token| {
                try renderToken(r, payload_token - 1, .none); // |
                try renderIdentifier(r, payload_token, .none, .preserve_when_shadowing); // identifier
                try renderToken(r, payload_token + 1, .maybe_space); // |
            }
            return renderExpression(r, expr, space);
        },

        .@"defer",
        .@"comptime",
        .@"nosuspend",
        .@"suspend",
        => {
            const main_token = tree.nodeMainToken(node);
            const item = tree.nodeData(node).node;
            try renderToken(r, main_token, .maybe_space);
            return renderExpression(r, item, space);
        },

        .@"catch" => {
            const main_token = tree.nodeMainToken(node);
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            const fallback_first = tree.firstToken(rhs);

            const seperate_line = !tree.tokensOnSameLine(main_token, fallback_first) or
                tree.tokenTag(fallback_first) == .multiline_string_literal_line;
            const after_op_space: Space = if (seperate_line) .newline else .space;

            try renderExpression(r, lhs, .space); // target

            try ais.pushIndent(.normal);
            if (tree.tokenTag(fallback_first - 1) == .pipe) {
                try renderToken(r, main_token, .space); // catch keyword
                try renderToken(r, main_token + 1, .none); // pipe
                try renderIdentifier(r, main_token + 2, .none, .preserve_when_shadowing); // payload identifier
                try renderToken(r, main_token + 3, after_op_space); // pipe
            } else {
                assert(tree.tokenTag(fallback_first - 1) == .keyword_catch);
                try renderToken(r, main_token, after_op_space); // catch keyword
            }
            try renderExpression(r, rhs, space); // fallback
            ais.popIndent();
        },

        .field_access => {
            const lhs, const name_token = tree.nodeData(node).node_and_token;
            const dot_token = name_token - 1;

            try ais.pushIndent(.field_access);

            const lhs_last_token = tree.lastToken(lhs);
            const same_line = tree.tokensOnSameLine(lhs_last_token, name_token);
            // Keeping a space after the number ensures it will not turn into a decimal number
            // (e.g. "0xF .A").
            const number_space: Space = if (tree.tokenTag(lhs_last_token) == .number_literal and same_line) .space else .none;
            try renderExpression(r, lhs, number_space);

            // Allow a line break between the lhs and the dot if the lhs and rhs
            // are on different lines.
            if (!same_line and !hasComment(tree, lhs_last_token, dot_token))
                try ais.insertNewline();

            try renderToken(r, dot_token, .none);

            try renderIdentifier(r, name_token, space, .eagerly_unquote); // field
            ais.popIndent();
        },

        .error_union,
        .switch_range,
        => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            try renderExpression(r, lhs, .none);
            try renderToken(r, tree.nodeMainToken(node), .none);
            return renderExpression(r, rhs, space);
        },
        .for_range => {
            const start, const opt_end = tree.nodeData(node).node_and_opt_node;
            try renderExpression(r, start, .none);
            if (opt_end.unwrap()) |end| {
                try renderToken(r, tree.nodeMainToken(node), .none);
                return renderExpression(r, end, space);
            } else {
                return renderToken(r, tree.nodeMainToken(node), space);
            }
        },

        .assign,
        .assign_bit_and,
        .assign_bit_or,
        .assign_shl,
        .assign_shl_sat,
        .assign_shr,
        .assign_bit_xor,
        .assign_div,
        .assign_sub,
        .assign_sub_wrap,
        .assign_sub_sat,
        .assign_mod,
        .assign_add,
        .assign_add_wrap,
        .assign_add_sat,
        .assign_mul,
        .assign_mul_wrap,
        .assign_mul_sat,
        => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            try renderExpression(r, lhs, .space);
            const op_token = tree.nodeMainToken(node);
            try ais.pushIndent(.after_equals);
            const rhs_seperate_line = !tree.tokensOnSameLine(op_token, op_token + 1) or
                tree.tokenTag(op_token + 1) == .multiline_string_literal_line;
            try renderToken(r, op_token, if (rhs_seperate_line) .newline else .space);
            try renderExpression(r, rhs, space);
            ais.popIndent();
        },

        .add,
        .add_wrap,
        .add_sat,
        .array_cat,
        .array_mult,
        .bang_equal,
        .bit_and,
        .bit_or,
        .shl,
        .shl_sat,
        .shr,
        .bit_xor,
        .bool_and,
        .bool_or,
        .div,
        .equal_equal,
        .greater_or_equal,
        .greater_than,
        .less_or_equal,
        .less_than,
        .merge_error_sets,
        .mod,
        .mul,
        .mul_wrap,
        .mul_sat,
        .sub,
        .sub_wrap,
        .sub_sat,
        .@"orelse",
        => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            try renderExpression(r, lhs, .space);
            const op_token = tree.nodeMainToken(node);
            try ais.pushIndent(.binop);
            const rhs_seperate_line = !tree.tokensOnSameLine(op_token, op_token + 1) or
                tree.tokenTag(op_token + 1) == .multiline_string_literal_line;
            try renderToken(r, op_token, if (rhs_seperate_line) .newline else .space);
            try renderExpression(r, rhs, space);
            ais.popIndent();
        },

        .assign_destructure => {
            const full = tree.assignDestructure(node);
            if (full.comptime_token) |comptime_token| {
                try renderToken(r, comptime_token, .maybe_space);
            }

            for (full.ast.variables, 0..) |variable_node, i| {
                const variable_space: Space = if (i == full.ast.variables.len - 1) .maybe_space else .comma_maybe_space;
                switch (tree.nodeTag(variable_node)) {
                    .global_var_decl,
                    .local_var_decl,
                    .simple_var_decl,
                    .aligned_var_decl,
                    => {
                        try renderVarDecl(r, tree.fullVarDecl(variable_node).?, true, variable_space);
                    },
                    else => try renderExpression(r, variable_node, variable_space),
                }
            }
            try ais.pushIndent(.after_equals);
            const expr_seperate_line =
                !tree.tokensOnSameLine(full.ast.equal_token, full.ast.equal_token + 1) or
                tree.tokenTag(full.ast.equal_token + 1) == .multiline_string_literal_line;
            try renderToken(r, full.ast.equal_token, if (expr_seperate_line) .newline else .space);
            try renderExpression(r, full.ast.value_expr, space);
            ais.popIndent();
        },

        .bit_not,
        .bool_not,
        .negation,
        .negation_wrap,
        .optional_type,
        .address_of,
        => {
            try renderToken(r, tree.nodeMainToken(node), .none);
            return renderExpression(r, tree.nodeData(node).node, space);
        },

        .@"try",
        .@"resume",
        => {
            try renderToken(r, tree.nodeMainToken(node), .maybe_space);
            return renderExpression(r, tree.nodeData(node).node, space);
        },

        .array_type,
        .array_type_sentinel,
        => return renderArrayType(r, tree.fullArrayType(node).?, space),

        .ptr_type_aligned,
        .ptr_type_sentinel,
        .ptr_type,
        .ptr_type_bit_range,
        => return renderPtrType(r, tree.fullPtrType(node).?, space),

        .array_init_one,
        .array_init_one_comma,
        .array_init_dot_two,
        .array_init_dot_two_comma,
        .array_init_dot,
        .array_init_dot_comma,
        .array_init,
        .array_init_comma,
        => {
            var elements: [2]Ast.Node.Index = undefined;
            return renderArrayInit(r, tree.fullArrayInit(&elements, node).?, space);
        },

        .struct_init_one,
        .struct_init_one_comma,
        .struct_init_dot_two,
        .struct_init_dot_two_comma,
        .struct_init_dot,
        .struct_init_dot_comma,
        .struct_init,
        .struct_init_comma,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            return renderStructInit(r, node, tree.fullStructInit(&buf, node).?, space);
        },

        .call_one,
        .call_one_comma,
        .call,
        .call_comma,
        => {
            var buf: [1]Ast.Node.Index = undefined;
            return renderCall(r, tree.fullCall(&buf, node).?, space);
        },

        .array_access => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            const lbracket = tree.firstToken(rhs) - 1;
            const rbracket = tree.lastToken(rhs) + 1;
            try renderExpression(r, lhs, .none);
            // One lien check must come after rendering lhs since it can influence
            // isLineOverIndented
            const one_line = tree.tokensOnSameLine(lbracket, rbracket) and
                !try rendersMultiline(r, rhs);
            const inner_space = if (one_line) Space.none else Space.newline;
            try ais.pushIndent(.normal);
            try renderToken(r, lbracket, inner_space); // [
            try renderExpression(r, rhs, inner_space);
            ais.popIndent();
            return renderToken(r, rbracket, space); // ]
        },

        .slice_open,
        .slice,
        .slice_sentinel,
        => return renderSlice(r, node, tree.fullSlice(node).?, space),

        .deref => {
            try renderExpression(r, tree.nodeData(node).node, .none);
            return renderToken(r, tree.nodeMainToken(node), space);
        },

        .unwrap_optional => {
            const lhs, const question_mark = tree.nodeData(node).node_and_token;
            const dot_token = question_mark - 1;
            try renderExpression(r, lhs, .none);
            try renderToken(r, dot_token, .none);
            return renderToken(r, question_mark, space);
        },

        .@"break", .@"continue" => {
            const main_token = tree.nodeMainToken(node);
            const opt_label_token, const opt_target = tree.nodeData(node).opt_token_and_opt_node;

            const before_target_space: Space = if (opt_target != .none) .maybe_space else space;
            const before_label_space: Space = if (opt_label_token != .none) .space else before_target_space;

            try renderToken(r, main_token, before_label_space);
            if (opt_label_token.unwrap()) |label_token| {
                try renderToken(r, label_token - 1, .none); // :
                try renderIdentifier(r, label_token, before_target_space, .eagerly_unquote); // identifier
            }
            if (opt_target.unwrap()) |target| {
                try renderExpression(r, target, space);
            }
        },

        .@"return" => {
            if (tree.nodeData(node).opt_node.unwrap()) |expr| {
                try renderToken(r, tree.nodeMainToken(node), .maybe_space);
                try renderExpression(r, expr, space);
            } else {
                try renderToken(r, tree.nodeMainToken(node), space);
            }
        },

        .grouped_expression => {
            const expr, const rparen = tree.nodeData(node).node_and_token;
            try ais.pushIndent(.normal);
            try renderToken(r, tree.nodeMainToken(node), .none); // lparen
            try renderExpression(r, expr, .none);
            ais.popIndent();
            return renderToken(r, rparen, space);
        },

        .container_decl,
        .container_decl_trailing,
        .container_decl_arg,
        .container_decl_arg_trailing,
        .container_decl_two,
        .container_decl_two_trailing,
        .tagged_union,
        .tagged_union_trailing,
        .tagged_union_enum_tag,
        .tagged_union_enum_tag_trailing,
        .tagged_union_two,
        .tagged_union_two_trailing,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            return renderContainerDecl(r, node, tree.fullContainerDecl(&buf, node).?, space);
        },

        .error_set_decl => {
            const error_token = tree.nodeMainToken(node);
            const lbrace, const rbrace = tree.nodeData(node).token_and_token;

            try renderToken(r, error_token, .none);

            if (lbrace + 1 == rbrace) {
                // There is nothing between the braces so render condensed: `error{}`
                try renderToken(r, lbrace, .none);
                return renderToken(r, rbrace, space);
            } else if (lbrace + 2 == rbrace and tree.tokenTag(lbrace + 1) == .identifier) {
                // There is exactly one member and no trailing comma or
                // comments, so render without surrounding spaces: `error{Foo}`
                try renderToken(r, lbrace, .none);
                try renderIdentifier(r, lbrace + 1, .none, .eagerly_unquote); // identifier
                return renderToken(r, rbrace, space);
            } else if (!isOneLineErrorSetDecl(tree, lbrace, rbrace)) {
                // Render each member on a new line.
                try ais.pushIndent(.normal);
                try renderToken(r, lbrace, .newline);
                var i = lbrace + 1;
                while (i < rbrace) : (i += 1) {
                    const tag = tree.tokenTag(i);
                    if (tag == .comma) {
                        assert(tree.tokenTag(i - 1) == .identifier);
                        continue;
                    }
                    if (i > lbrace + 1) try renderExtraNewlineToken(r, i);
                    switch (tag) {
                        .doc_comment => try renderToken(r, i, .newline),
                        .identifier => {
                            try ais.pushSpace(.comma);
                            try renderIdentifier(r, i, .comma, .eagerly_unquote);
                            ais.popSpace();
                        },
                        else => unreachable,
                    }
                }
                ais.popIndent();
                return renderToken(r, rbrace, space);
            } else {
                // Render each member on one line.
                try renderToken(r, lbrace, .space);
                var i = lbrace + 1;
                while (i < rbrace) : (i += 1) {
                    switch (tree.tokenTag(i)) {
                        .identifier => try renderIdentifier(r, i, .comma_space, .eagerly_unquote),
                        .comma => {},
                        else => unreachable,
                    }
                }
                return renderToken(r, rbrace, space);
            }
        },

        .builtin_call_two,
        .builtin_call_two_comma,
        .builtin_call,
        .builtin_call_comma,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const params = tree.builtinCallParams(&buf, node).?;
            var builtin_token = tree.nodeMainToken(node);

            canonicalize: {
                if (params.len != 1) break :canonicalize;

                const CastKind = enum(u8) {
                    ptrCast,
                    alignCast,
                    addrSpaceCast,
                    constCast,
                    volatileCast,
                };
                const kind = meta.stringToEnum(
                    CastKind,
                    tree.tokenSlice(builtin_token)[1..],
                ) orelse break :canonicalize;

                var cast_map = std.EnumMap(CastKind, Ast.TokenIndex).init(.{});
                cast_map.put(kind, builtin_token);

                var casts_before: usize = 0;
                var prev_builtin_token = builtin_token;
                while (prev_builtin_token >= 2) {
                    prev_builtin_token -= 2;
                    if (tree.tokenTag(prev_builtin_token) != .builtin) break;
                    const builtin_name = tree.tokenSlice(prev_builtin_token)[1..];
                    const prev_kind = meta.stringToEnum(CastKind, builtin_name) orelse break;
                    if (cast_map.contains(prev_kind)) break :canonicalize;
                    // This must be checked after so that cast builtins as arguments to other
                    // builtins containing comments are reordered.
                    if (hasComment(tree, prev_builtin_token, prev_builtin_token + 2))
                        break :canonicalize;
                    cast_map.put(prev_kind, prev_builtin_token);
                    casts_before += 1;
                }

                var next_builtin_token = builtin_token + 2;
                while (true) {
                    if (hasComment(tree, next_builtin_token - 2, next_builtin_token))
                        break :canonicalize;
                    if (tree.tokenTag(next_builtin_token) != .builtin) break;
                    const builtin_name = tree.tokenSlice(next_builtin_token)[1..];
                    const next_kind = meta.stringToEnum(CastKind, builtin_name) orelse break;
                    if (cast_map.contains(next_kind)) break :canonicalize;
                    cast_map.put(next_kind, next_builtin_token);
                    next_builtin_token += 2;
                }

                var it = cast_map.iterator();
                builtin_token = it.next().?.value.*;
                while (casts_before > 0) : (casts_before -= 1) {
                    builtin_token = it.next().?.value.*;
                }
            }
            return renderBuiltinCall(r, builtin_token, params, space);
        },

        .fn_proto_simple,
        .fn_proto_multi,
        .fn_proto_one,
        .fn_proto,
        => {
            var buf: [1]Ast.Node.Index = undefined;
            return renderFnProto(r, tree.fullFnProto(&buf, node).?, space);
        },

        .anyframe_type => {
            const main_token = tree.nodeMainToken(node);
            try renderToken(r, main_token, .none); // anyframe
            try renderToken(r, main_token + 1, .none); // ->
            return renderExpression(r, tree.nodeData(node).token_and_node[1], space);
        },

        .@"switch",
        .switch_comma,
        => {
            const full = tree.switchFull(node);

            if (full.label_token) |label_token| {
                try renderIdentifier(r, label_token, .none, .eagerly_unquote); // label
                try renderToken(r, label_token + 1, .space); // :
            }

            const rparen = tree.lastToken(full.ast.condition) + 1;

            try renderToken(r, full.ast.switch_token, .space); // switch
            try renderToken(r, full.ast.switch_token + 1, .none); // (
            try renderExpression(r, full.ast.condition, .none); // condition expression
            try renderToken(r, rparen, .space); // )

            try ais.pushIndent(.normal);
            if (full.ast.cases.len == 0) {
                try renderToken(r, rparen + 1, .none); // {
            } else {
                try renderToken(r, rparen + 1, .newline); // {
                try ais.pushSpace(.comma);
                try renderExpressions(r, full.ast.cases, .comma);
                ais.popSpace();
            }
            ais.popIndent();
            return renderToken(r, tree.lastToken(node), space); // }
        },

        .switch_case_one,
        .switch_case_inline_one,
        .switch_case,
        .switch_case_inline,
        => return renderSwitchCase(r, tree.fullSwitchCase(node).?, space),

        .while_simple,
        .while_cont,
        .@"while",
        => return renderWhile(r, tree.fullWhile(node).?, space),

        .for_simple,
        .@"for",
        => return renderFor(r, tree.fullFor(node).?, space),

        .if_simple,
        .@"if",
        => return renderIf(r, tree.fullIf(node).?, space),

        .asm_simple,
        .@"asm",
        => return renderAsm(r, tree.fullAsm(node).?, space),

        .enum_literal => {
            try renderToken(r, tree.nodeMainToken(node) - 1, .none); // .
            return renderIdentifier(r, tree.nodeMainToken(node), space, .eagerly_unquote); // name
        },

        .fn_decl => unreachable,
        .container_field => unreachable,
        .container_field_init => unreachable,
        .container_field_align => unreachable,
        .root => unreachable,
        .global_var_decl => unreachable,
        .local_var_decl => unreachable,
        .simple_var_decl => unreachable,
        .aligned_var_decl => unreachable,
        .test_decl => unreachable,
        .asm_output => unreachable,
        .asm_input => unreachable,
    }
}

/// Same as `renderExpression`, but afterwards looks for any
/// append_string_after_node fixups to apply
fn renderExpressionFixup(r: *Render, node: Ast.Node.Index, space: Space) Error!void {
    const ais = r.ais;
    try renderExpression(r, node, space);
    if (r.fixups.append_string_after_node.get(node)) |bytes| {
        try ais.writeAll(bytes);
    }
}

fn drainNoNewline(w: *Writer, data: []const []const u8, splat: usize) Writer.Error!usize {
    if (std.mem.indexOfScalar(u8, w.buffered(), '\n') != null) {
        return error.WriteFailed;
    }

    var n: usize = 0;
    for (data[0 .. data.len - 1]) |v| {
        if (std.mem.indexOfScalar(u8, v, '\n') != null) {
            return error.WriteFailed;
        }
        n += v.len;
    }

    const pattern = data[data.len - 1];
    if (splat != 0 and std.mem.indexOfScalar(u8, pattern, '\n') != null) {
        return error.WriteFailed;
    }
    n += pattern.len * splat;

    w.end = 0;
    return n;
}

fn rendersMultiline(r: *const Render, node: Ast.Node.Index) error{OutOfMemory}!bool {
    var no_nl_buf: [64]u8 = undefined;
    var no_nl_w: Writer = .{
        .vtable = &.{ .drain = drainNoNewline },
        .buffer = &no_nl_buf,
    };

    if (r.ais.disabled_offset != null) return true;
    var sub_ais: AutoIndentingStream = .init(r.gpa, &no_nl_w, r.ais.indent_delta);
    defer sub_ais.deinit();
    // The following are needed to make sure isLineOverIndented is correct
    sub_ais.indent_count = r.ais.indent_count;
    sub_ais.applied_indent = r.ais.applied_indent;
    sub_ais.current_line_empty = r.ais.current_line_empty;

    var sub_r: Render = .{
        .gpa = r.gpa,
        .ais = &sub_ais,
        .tree = r.tree,
        .fixups = r.fixups,
    };

    renderExpression(&sub_r, node, .none) catch |e| return switch (e) {
        error.OutOfMemory => return error.OutOfMemory,
        error.WriteFailed => return true,
    };
    if (sub_ais.disabled_offset != null) return true;
    if (std.mem.indexOfScalar(u8, no_nl_w.buffered(), '\n') != null) {
        return true;
    }

    return false;
}

fn renderArrayType(
    r: *Render,
    array_type: Ast.full.ArrayType,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    const rbracket = tree.firstToken(array_type.ast.elem_type) - 1;
    const one_line = tree.tokensOnSameLine(array_type.ast.lbracket, rbracket) and
        !try rendersMultiline(r, array_type.ast.elem_count) and
        (if (array_type.ast.sentinel.unwrap()) |s| !try rendersMultiline(r, s) else true);
    const inner_space = if (one_line) Space.none else Space.newline;
    try ais.pushIndent(.normal);
    try renderToken(r, array_type.ast.lbracket, inner_space); // lbracket
    try renderExpression(r, array_type.ast.elem_count, inner_space);
    if (array_type.ast.sentinel.unwrap()) |sentinel| {
        try renderToken(r, tree.firstToken(sentinel) - 1, inner_space); // colon
        try renderExpression(r, sentinel, inner_space);
    }
    ais.popIndent();
    try renderToken(r, rbracket, .none); // rbracket
    return renderExpression(r, array_type.ast.elem_type, space);
}

fn renderPtrType(r: *Render, ptr_type: Ast.full.PtrType, space: Space) Error!void {
    const tree = r.tree;
    const main_token = ptr_type.ast.main_token;

    switch (ptr_type.size) {
        .one => {
            // Since ** tokens exist and the same token is shared by two
            // nested pointer types, we check to see if we are the parent
            // in such a relationship. If so, skip rendering anything for
            // this pointer type and rely on the child to render our asterisk
            // as well when it renders the ** token.
            if (tree.tokenTag(main_token) == .asterisk_asterisk and
                main_token == tree.nodeMainToken(ptr_type.ast.child_type))
            {
                return renderExpression(r, ptr_type.ast.child_type, space);
            }
            try renderToken(r, main_token, .none); // asterisk
        },
        .many => {
            if (ptr_type.ast.sentinel.unwrap()) |sentinel| {
                try renderToken(r, main_token, .none); // lbracket
                try renderToken(r, main_token + 1, .none); // asterisk
                try renderToken(r, main_token + 2, .none); // colon
                try renderExpression(r, sentinel, .none);
                try renderToken(r, tree.lastToken(sentinel) + 1, .none); // rbracket
            } else {
                try renderToken(r, main_token, .none); // lbracket
                try renderToken(r, main_token + 1, .none); // asterisk
                try renderToken(r, main_token + 2, .none); // rbracket
            }
        },
        .c => {
            try renderToken(r, main_token, .none); // lbracket
            try renderToken(r, main_token + 1, .none); // asterisk
            try renderToken(r, main_token + 2, .none); // c
            try renderToken(r, main_token + 3, .none); // rbracket
        },
        .slice => {
            if (ptr_type.ast.sentinel.unwrap()) |sentinel| {
                try renderToken(r, main_token, .none); // lbracket
                try renderToken(r, main_token + 1, .none); // colon
                try renderExpression(r, sentinel, .none);
                try renderToken(r, tree.lastToken(sentinel) + 1, .none); // rbracket
            } else {
                try renderToken(r, main_token, .none); // lbracket
                try renderToken(r, main_token + 1, .none); // rbracket
            }
        },
    }

    // .maybe_space cannot be used at the end of each qualifier since they may be reordered
    const final_qual: enum {
        @"volatile",
        @"const",
        @"addrspace",
        @"align",
        @"allowzero",
        none,
    } = if (ptr_type.volatile_token != null)
        .@"volatile"
    else if (ptr_type.const_token != null)
        .@"const"
    else if (ptr_type.ast.addrspace_node != .none)
        .@"addrspace"
    else if (ptr_type.ast.align_node != .none)
        .@"align"
    else if (ptr_type.allowzero_token != null)
        .@"allowzero"
    else
        .none;
    const final_qual_space: Space = if (tree.tokenTag(tree.firstToken(ptr_type.ast.child_type)) !=
        .multiline_string_literal_line) .space else .none;

    if (ptr_type.allowzero_token) |allowzero_token| {
        const this_space: Space = if (final_qual == .@"allowzero") final_qual_space else .space;
        try renderToken(r, allowzero_token, this_space);
    }

    if (ptr_type.ast.align_node.unwrap()) |align_node| {
        const this_space: Space = if (final_qual == .@"align") final_qual_space else .space;
        const align_first = tree.firstToken(align_node);
        try renderToken(r, align_first - 2, .none); // align
        try renderToken(r, align_first - 1, .none); // lparen
        try renderExpression(r, align_node, .none);
        if (ptr_type.ast.bit_range_start.unwrap()) |bit_range_start| {
            const bit_range_end = ptr_type.ast.bit_range_end.unwrap().?;
            try renderToken(r, tree.firstToken(bit_range_start) - 1, .none); // colon
            try renderExpression(r, bit_range_start, .none);
            try renderToken(r, tree.firstToken(bit_range_end) - 1, .none); // colon
            try renderExpression(r, bit_range_end, .none);
            try renderToken(r, tree.lastToken(bit_range_end) + 1, this_space); // rparen
        } else {
            try renderToken(r, tree.lastToken(align_node) + 1, this_space); // rparen
        }
    }

    if (ptr_type.ast.addrspace_node.unwrap()) |addrspace_node| {
        const this_space: Space = if (final_qual == .@"addrspace") final_qual_space else .space;
        const addrspace_first = tree.firstToken(addrspace_node);
        try renderToken(r, addrspace_first - 2, .none); // addrspace
        try renderToken(r, addrspace_first - 1, .none); // lparen
        try renderExpression(r, addrspace_node, .none);
        try renderToken(r, tree.lastToken(addrspace_node) + 1, this_space); // rparen
    }

    if (ptr_type.const_token) |const_token| {
        const this_space: Space = if (final_qual == .@"const") final_qual_space else .space;
        try renderToken(r, const_token, this_space);
    }

    if (ptr_type.volatile_token) |volatile_token| {
        const this_space: Space = if (final_qual == .@"volatile") final_qual_space else unreachable;
        try renderToken(r, volatile_token, this_space);
    }

    try renderExpression(r, ptr_type.ast.child_type, space);
}

fn renderSlice(
    r: *Render,
    slice_node: Ast.Node.Index,
    slice: Ast.full.Slice,
    space: Space,
) Error!void {
    const tree = r.tree;
    const space_around_dots = nodeCausesSliceOpSpace(tree.nodeTag(slice.ast.start)) or
        if (slice.ast.end.unwrap()) |end| nodeCausesSliceOpSpace(tree.nodeTag(end)) else false;
    const after_start_space: Space = if (space_around_dots) .space else .none;
    const before_sentinel_space: Space = if (slice.ast.sentinel != .none) .space else .none;
    const after_dots_space: Space = if (slice.ast.end != .none)
        if (space_around_dots) .maybe_space else .none
    else
        before_sentinel_space;

    try renderExpression(r, slice.ast.sliced, .none);
    try renderToken(r, slice.ast.lbracket, .none); // lbracket

    const start_last = tree.lastToken(slice.ast.start);
    try renderExpression(r, slice.ast.start, after_start_space);
    try renderToken(r, start_last + 1, after_dots_space); // ellipsis2 ("..")

    if (slice.ast.end.unwrap()) |end| {
        try renderExpression(r, end, before_sentinel_space);
    }

    if (slice.ast.sentinel.unwrap()) |sentinel| {
        try renderToken(r, tree.firstToken(sentinel) - 1, .none); // colon
        try renderExpression(r, sentinel, .none);
    }

    try renderToken(r, tree.lastToken(slice_node), space); // rbracket
}

fn renderAsmOutput(
    r: *Render,
    asm_output: Ast.Node.Index,
    space: Space,
) Error!void {
    const tree = r.tree;
    assert(tree.nodeTag(asm_output) == .asm_output);
    const symbolic_name = tree.nodeMainToken(asm_output);

    try renderToken(r, symbolic_name - 1, .none); // lbracket
    try renderIdentifier(r, symbolic_name, .none, .eagerly_unquote); // ident
    try renderToken(r, symbolic_name + 1, .space); // rbracket
    try renderToken(r, symbolic_name + 2, .space); // "constraint"
    try renderToken(r, symbolic_name + 3, .none); // lparen

    if (tree.tokenTag(symbolic_name + 4) == .arrow) {
        const type_expr, const rparen = tree.nodeData(asm_output).opt_node_and_token;
        try renderToken(r, symbolic_name + 4, .maybe_space); // ->
        try renderExpression(r, type_expr.unwrap().?, Space.none);
        return renderToken(r, rparen, space);
    } else {
        try renderIdentifier(r, symbolic_name + 4, .none, .eagerly_unquote); // ident
        return renderToken(r, symbolic_name + 5, space); // rparen
    }
}

fn renderAsmInput(
    r: *Render,
    asm_input: Ast.Node.Index,
    space: Space,
) Error!void {
    const tree = r.tree;
    assert(tree.nodeTag(asm_input) == .asm_input);
    const symbolic_name = tree.nodeMainToken(asm_input);
    const expr, const rparen = tree.nodeData(asm_input).node_and_token;

    try renderToken(r, symbolic_name - 1, .none); // lbracket
    try renderIdentifier(r, symbolic_name, .none, .eagerly_unquote); // ident
    try renderToken(r, symbolic_name + 1, .space); // rbracket
    try renderToken(r, symbolic_name + 2, .space); // "constraint"
    try renderToken(r, symbolic_name + 3, .none); // lparen
    try renderExpression(r, expr, Space.none);
    return renderToken(r, rparen, space);
}

fn renderVarDecl(
    r: *Render,
    var_decl: Ast.full.VarDecl,
    /// Destructures intentionally ignore leading `comptime` tokens.
    ignore_comptime_token: bool,
    /// `comma_space` and `space` are used for destructure LHS decls.
    space: Space,
) Error!void {
    try renderVarDeclWithoutFixups(r, var_decl, ignore_comptime_token, space);
    if (r.fixups.unused_var_decls.contains(var_decl.ast.mut_token + 1)) {
        // Discard the variable like this: `_ = foo;`
        const ais = r.ais;
        try ais.writeAll("_ = ");
        try ais.writeAll(tokenSliceForRender(r.tree, var_decl.ast.mut_token + 1));
        try ais.writeAll(";\n");
    }
}

fn renderVarDeclWithoutFixups(
    r: *Render,
    var_decl: Ast.full.VarDecl,
    /// Destructures intentionally ignore leading `comptime` tokens.
    ignore_comptime_token: bool,
    /// `comma_space` and `space` are used for destructure LHS decls.
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;

    if (var_decl.visib_token) |visib_token| {
        try renderToken(r, visib_token, Space.space); // pub
    }

    if (var_decl.extern_export_token) |extern_export_token| {
        try renderToken(r, extern_export_token, Space.space); // extern

        if (var_decl.lib_name) |lib_name| {
            try renderToken(r, lib_name, Space.space); // "lib"
        }
    }

    if (var_decl.threadlocal_token) |thread_local_token| {
        try renderToken(r, thread_local_token, Space.space); // threadlocal
    }

    if (!ignore_comptime_token) {
        if (var_decl.comptime_token) |comptime_token| {
            try renderToken(r, comptime_token, Space.space); // comptime
        }
    }

    try renderToken(r, var_decl.ast.mut_token, .space); // var

    const last_component: enum {
        value,
        @"linksection",
        @"addrspace",
        @"align",
        type,
        identifier,
    } = if (var_decl.ast.init_node != .none)
        .value
    else if (var_decl.ast.section_node != .none)
        .@"linksection"
    else if (var_decl.ast.addrspace_node != .none)
        .@"addrspace"
    else if (var_decl.ast.align_node != .none)
        .@"align"
    else if (var_decl.ast.type_node != .none)
        .type
    else
        .identifier;

    if (last_component == .identifier) {
        return renderIdentifier(r, var_decl.ast.mut_token + 1, space, .preserve_when_shadowing);
    }
    const after_ident_space: Space = if (var_decl.ast.type_node != .none) .none else .space;
    try renderIdentifier(r, var_decl.ast.mut_token + 1, after_ident_space, .preserve_when_shadowing);

    if (var_decl.ast.type_node.unwrap()) |type_node| {
        try renderToken(r, var_decl.ast.mut_token + 2, .maybe_space); // :
        if (last_component == .type) {
            return renderExpression(r, type_node, space);
        }
        try renderExpression(r, type_node, .space);
    }

    if (var_decl.ast.align_node.unwrap()) |align_node| {
        const lparen = tree.firstToken(align_node) - 1;
        const align_kw = lparen - 1;
        const rparen = tree.lastToken(align_node) + 1;
        try renderToken(r, align_kw, Space.none); // align
        try renderToken(r, lparen, Space.none); // (
        try renderExpression(r, align_node, Space.none);
        if (last_component == .@"align") {
            return renderToken(r, rparen, space); // )
        }
        try renderToken(r, rparen, .space); // )
    }

    if (var_decl.ast.addrspace_node.unwrap()) |addrspace_node| {
        const lparen = tree.firstToken(addrspace_node) - 1;
        const addrspace_kw = lparen - 1;
        const rparen = tree.lastToken(addrspace_node) + 1;
        try renderToken(r, addrspace_kw, Space.none); // addrspace
        try renderToken(r, lparen, Space.none); // (
        try renderExpression(r, addrspace_node, Space.none);
        if (last_component == .@"addrspace") {
            return renderToken(r, rparen, space); // )
        }
        try renderToken(r, rparen, .space); // )
    }

    if (var_decl.ast.section_node.unwrap()) |section_node| {
        const lparen = tree.firstToken(section_node) - 1;
        const section_kw = lparen - 1;
        const rparen = tree.lastToken(section_node) + 1;
        try renderToken(r, section_kw, Space.none); // linksection
        try renderToken(r, lparen, Space.none); // (
        try renderExpression(r, section_node, Space.none);
        if (last_component == .@"linksection") {
            return renderToken(r, rparen, space); // )
        }
        try renderToken(r, rparen, .space); // )
    }

    assert(last_component == .value);
    const init_node = var_decl.ast.init_node.unwrap().?;

    const eq_token = tree.firstToken(init_node) - 1;
    const rhs_seperate_line = !tree.tokensOnSameLine(eq_token, eq_token + 1) or
        tree.tokenTag(eq_token + 1) == .multiline_string_literal_line;
    const eq_space: Space = if (rhs_seperate_line) .newline else .space;
    try ais.pushIndent(.after_equals);
    try renderToken(r, eq_token, eq_space); // =
    try renderExpression(r, init_node, space); // ;
    ais.popIndent();
}

fn renderIf(r: *Render, if_node: Ast.full.If, space: Space) Error!void {
    return renderWhile(r, .{
        .ast = .{
            .while_token = if_node.ast.if_token,
            .cond_expr = if_node.ast.cond_expr,
            .cont_expr = .none,
            .then_expr = if_node.ast.then_expr,
            .else_expr = if_node.ast.else_expr,
        },
        .inline_token = null,
        .label_token = null,
        .payload_token = if_node.payload_token,
        .else_token = if_node.else_token,
        .error_token = if_node.error_token,
    }, space);
}

/// Note that this function is additionally used to render if expressions, with
/// respective values set to null.
fn renderWhile(r: *Render, while_node: Ast.full.While, space: Space) Error!void {
    const tree = r.tree;

    if (while_node.label_token) |label| {
        try renderIdentifier(r, label, .none, .eagerly_unquote); // label
        try renderToken(r, label + 1, .space); // :
    }

    if (while_node.inline_token) |inline_token| {
        try renderToken(r, inline_token, .space); // inline
    }

    try renderToken(r, while_node.ast.while_token, .space); // if/for/while
    try renderToken(r, while_node.ast.while_token + 1, .none); // lparen
    try renderExpression(r, while_node.ast.cond_expr, .none); // condition

    var last_prefix_token = tree.lastToken(while_node.ast.cond_expr) + 1; // rparen

    if (while_node.payload_token) |payload_token| {
        try renderToken(r, last_prefix_token, .space);
        try renderToken(r, payload_token - 1, .none); // |
        const ident = blk: {
            if (tree.tokenTag(payload_token) == .asterisk) {
                try renderToken(r, payload_token, .none); // *
                break :blk payload_token + 1;
            } else {
                break :blk payload_token;
            }
        };
        try renderIdentifier(r, ident, .none, .preserve_when_shadowing); // identifier
        const pipe = blk: {
            if (tree.tokenTag(ident + 1) == .comma) {
                try renderToken(r, ident + 1, .space); // ,
                try renderIdentifier(r, ident + 2, .none, .preserve_when_shadowing); // index
                break :blk ident + 3;
            } else {
                break :blk ident + 1;
            }
        };
        last_prefix_token = pipe;
    }

    if (while_node.ast.cont_expr.unwrap()) |cont_expr| {
        try renderToken(r, last_prefix_token, .space);
        const lparen = tree.firstToken(cont_expr) - 1;
        try renderToken(r, lparen - 1, .space); // :
        try renderToken(r, lparen, .none); // lparen
        try renderExpression(r, cont_expr, .none);
        last_prefix_token = tree.lastToken(cont_expr) + 1; // rparen
    }

    try renderThenElse(
        r,
        last_prefix_token,
        while_node.ast.then_expr,
        while_node.else_token,
        while_node.error_token,
        while_node.ast.else_expr,
        space,
    );
}

fn renderThenElse(
    r: *Render,
    last_prefix_token: Ast.TokenIndex,
    then_expr: Ast.Node.Index,
    else_token: ?Ast.TokenIndex,
    maybe_error_token: ?Ast.TokenIndex,
    opt_else_expr: Ast.Node.OptionalIndex,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    const then_expr_is_block = nodeIsBlock(tree.nodeTag(then_expr));
    const then_expr_first_token = tree.firstToken(then_expr);
    const indent_then_expr = !then_expr_is_block and
        (!tree.tokensOnSameLine(last_prefix_token, then_expr_first_token) or
            tree.tokenTag(then_expr_first_token) == .multiline_string_literal_line);

    if (indent_then_expr) try ais.pushIndent(.normal);

    if (then_expr_is_block and ais.isLineOverIndented()) {
        ais.disableIndentCommitting();
        try renderToken(r, last_prefix_token, .newline);
        ais.enableIndentCommitting();
    } else if (indent_then_expr) {
        try renderToken(r, last_prefix_token, .newline);
    } else {
        try renderToken(r, last_prefix_token, .space);
    }

    if (opt_else_expr.unwrap()) |else_expr| {
        if (indent_then_expr) {
            try renderExpression(r, then_expr, .newline);
        } else {
            try renderExpression(r, then_expr, .space);
        }

        if (indent_then_expr) ais.popIndent();

        var last_else_token = else_token.?;

        if (maybe_error_token) |error_token| {
            try renderToken(r, last_else_token, .space); // else
            try renderToken(r, error_token - 1, .none); // |
            try renderIdentifier(r, error_token, .none, .preserve_when_shadowing); // identifier
            last_else_token = error_token + 1; // |
        }

        const indent_else_expr = indent_then_expr and
            !nodeIsBlock(tree.nodeTag(else_expr)) and
            !nodeIsIfForWhileSwitch(tree.nodeTag(else_expr)) or
            tree.tokenTag(tree.firstToken(else_expr)) ==
                .multiline_string_literal_line;
        if (indent_else_expr) {
            try ais.pushIndent(.normal);
            try renderToken(r, last_else_token, .newline);
            try renderExpression(r, else_expr, space);
            ais.popIndent();
        } else {
            try renderToken(r, last_else_token, .space);
            try renderExpression(r, else_expr, space);
        }
    } else {
        try renderExpression(r, then_expr, space);
        if (indent_then_expr) ais.popIndent();
    }
}

fn renderFor(r: *Render, for_node: Ast.full.For, space: Space) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    const token_tags = tree.tokens.items(.tag);

    if (for_node.label_token) |label| {
        try renderIdentifier(r, label, .none, .eagerly_unquote); // label
        try renderToken(r, label + 1, .space); // :
    }

    if (for_node.inline_token) |inline_token| {
        try renderToken(r, inline_token, .space); // inline
    }

    try renderToken(r, for_node.ast.for_token, .space); // if/for/while

    const lparen = for_node.ast.for_token + 1;
    try renderParamList(r, lparen, for_node.ast.inputs, .space);

    var cur = for_node.payload_token;
    const pipe = std.mem.findScalarPos(Token.Tag, token_tags, cur, .pipe).?;
    const capture_trailing_comma = token_tags[@intCast(pipe - 1)] == .comma;

    if (capture_trailing_comma)
        try ais.pushIndent(.normal);
    try renderToken(r, cur - 1, if (capture_trailing_comma) .newline else .none); // |
    while (true) {
        if (token_tags[cur] == .asterisk) {
            try renderToken(r, cur, .none); // *
            cur += 1;
        }
        try renderIdentifier(r, cur, .none, .preserve_when_shadowing); // identifier
        cur += 1;
        if (token_tags[cur] == .comma) {
            try renderToken(r, cur, if (capture_trailing_comma) .newline else .space); // ,
            cur += 1;
        }
        if (token_tags[cur] == .pipe) {
            break;
        }
    }
    if (capture_trailing_comma)
        ais.popIndent();

    try renderThenElse(
        r,
        cur,
        for_node.ast.then_expr,
        for_node.else_token,
        null,
        for_node.ast.else_expr,
        space,
    );
}

fn renderContainerField(
    r: *Render,
    container: Container,
    field_param: Ast.full.ContainerField,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    var field = field_param;
    if (container != .tuple) field.convertToNonTupleLike(&tree);
    const quote: QuoteBehavior = switch (container) {
        .@"enum" => .eagerly_unquote_except_underscore,
        .tuple, .other => .eagerly_unquote,
    };

    if (field.comptime_token) |t| {
        try renderToken(r, t, .maybe_space); // comptime
    }

    const last_component: enum {
        value,
        @"align",
        type,
        identifier,
    } = if (field.ast.value_expr != .none)
        .value
    else if (field.ast.align_expr != .none)
        .@"align"
    else if (field.ast.type_expr != .none)
        .type
    else if (!field.ast.tuple_like)
        .identifier
    else
        unreachable;

    if (!field.ast.tuple_like) {
        if (last_component == .identifier) {
            return renderIdentifierComma(r, field.ast.main_token, space, quote); // name
        }
        const this_space: Space = if (field.ast.type_expr != .none) .none else .space;
        try renderIdentifier(r, field.ast.main_token, this_space, quote); // name
    }

    if (field.ast.type_expr.unwrap()) |type_expr| {
        if (!field.ast.tuple_like) {
            try renderToken(r, field.ast.main_token + 1, .maybe_space); // :
        }

        if (last_component == .type) {
            return renderExpressionComma(r, type_expr, space); // type
        }
        try renderExpression(r, type_expr, .space); // type
    }

    if (field.ast.align_expr.unwrap()) |align_expr| {
        const align_token = tree.firstToken(align_expr) - 2;
        try renderToken(r, align_token, .none); // align
        try renderToken(r, align_token + 1, .none); // (
        try renderExpression(r, align_expr, .none); // alignment
        const rparen = tree.lastToken(align_expr) + 1;
        if (last_component == .@"align") {
            return renderTokenComma(r, rparen, space); // )
        }
        try renderToken(r, rparen, .space);
    }

    if (field.ast.value_expr.unwrap()) |value_expr| {
        assert(last_component == .value);
        const eq_token = tree.firstToken(value_expr) - 1;
        const seperate_line = !tree.tokensOnSameLine(eq_token, eq_token + 1) or
            tree.tokenTag(eq_token + 1) == .multiline_string_literal_line;
        const eq_space: Space = if (seperate_line) .newline else .space;

        try ais.pushIndent(.after_equals);
        try renderToken(r, eq_token, eq_space); // =
        if (eq_space == .space) {
            ais.popIndent();
            return renderExpressionComma(r, value_expr, space); // value
        }

        const maybe_comma = tree.lastToken(value_expr) + 1;
        if (tree.tokenTag(maybe_comma) == .comma) {
            try renderExpression(r, value_expr, .none); // value
            ais.popIndent();
            try renderToken(r, maybe_comma, .newline);
        } else {
            try renderExpression(r, value_expr, space); // value
            ais.popIndent();
        }
    } else unreachable;
}

fn renderBuiltinCall(
    r: *Render,
    builtin_token: Ast.TokenIndex,
    params: []const Ast.Node.Index,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;

    try renderToken(r, builtin_token, .none); // @name

    if (r.fixups.rebase_imported_paths) |prefix| {
        const slice = tree.tokenSlice(builtin_token);
        if (params.len != 0 and mem.eql(u8, slice, "@import")) f: {
            const param = params[0];
            const str_lit_token = tree.nodeMainToken(param);
            assert(tree.tokenTag(str_lit_token) == .string_literal);
            const token_bytes = tree.tokenSlice(str_lit_token);
            const imported_string = std.zig.string_literal.parseAlloc(r.gpa, token_bytes) catch |err| switch (err) {
                error.OutOfMemory => return error.OutOfMemory,
                error.InvalidLiteral => break :f,
            };
            defer r.gpa.free(imported_string);
            const new_string = try std.fs.path.resolvePosix(r.gpa, &.{ prefix, imported_string });
            defer r.gpa.free(new_string);

            try renderToken(r, builtin_token + 1, .none); // (
            try ais.print("\"{f}\"", .{std.zig.fmtString(new_string)});
            return renderToken(r, str_lit_token + 1, space); // )
        }
    }

    return renderParamList(r, builtin_token + 1, params, space);
}

fn fnProtoRparen(tree: Ast, fn_proto: Ast.full.FnProto, maybe_bang: Ast.TokenIndex) Ast.TokenIndex {
    // These may appear in any order, so we have to check the token_starts array
    // to find out which is first.
    var rparen = if (tree.tokenTag(maybe_bang) == .bang) maybe_bang - 1 else maybe_bang;
    var smallest_start = tree.tokenStart(maybe_bang);
    if (fn_proto.ast.align_expr.unwrap()) |align_expr| {
        const tok = tree.firstToken(align_expr) - 3;
        const start = tree.tokenStart(tok);
        if (start < smallest_start) {
            rparen = tok;
            smallest_start = start;
        }
    }
    if (fn_proto.ast.addrspace_expr.unwrap()) |addrspace_expr| {
        const tok = tree.firstToken(addrspace_expr) - 3;
        const start = tree.tokenStart(tok);
        if (start < smallest_start) {
            rparen = tok;
            smallest_start = start;
        }
    }
    if (fn_proto.ast.section_expr.unwrap()) |section_expr| {
        const tok = tree.firstToken(section_expr) - 3;
        const start = tree.tokenStart(tok);
        if (start < smallest_start) {
            rparen = tok;
            smallest_start = start;
        }
    }
    if (fn_proto.ast.callconv_expr.unwrap()) |callconv_expr| {
        const tok = tree.firstToken(callconv_expr) - 3;
        const start = tree.tokenStart(tok);
        if (start < smallest_start) {
            rparen = tok;
            smallest_start = start;
        }
    }
    assert(tree.tokenTag(rparen) == .r_paren);
    return rparen;
}

fn isOneLineFnProto(
    tree: Ast,
    fn_proto: Ast.full.FnProto,
    lparen: Ast.TokenIndex,
    rparen: Ast.TokenIndex,
) bool {
    const trailing_comma = tree.tokenTag(rparen - 1) == .comma;
    if (trailing_comma or hasComment(tree, lparen, rparen))
        return false;

    // Check that there are no doc comments
    var after_last_param = lparen + 1;
    for (fn_proto.ast.params) |expr| {
        // Looking before each param is insufficient since anytype is not included in `params`
        if (hasDocComment(tree, after_last_param, tree.firstToken(expr)))
            return false;
        after_last_param = tree.lastToken(expr) + 1;
    }
    return !hasDocComment(tree, after_last_param, rparen);
}

fn renderFnProto(r: *Render, fn_proto: Ast.full.FnProto, space: Space) Error!void {
    const tree = r.tree;
    const ais = r.ais;

    const after_fn_token = fn_proto.ast.fn_token + 1;
    const lparen = if (tree.tokenTag(after_fn_token) == .identifier) blk: {
        try renderToken(r, fn_proto.ast.fn_token, .space); // fn
        try renderIdentifier(r, after_fn_token, .none, .preserve_when_shadowing); // name
        break :blk after_fn_token + 1;
    } else blk: {
        try renderToken(r, fn_proto.ast.fn_token, .space); // fn
        break :blk fn_proto.ast.fn_token + 1;
    };
    assert(tree.tokenTag(lparen) == .l_paren);

    const return_type = fn_proto.ast.return_type.unwrap().?;
    const maybe_bang = tree.firstToken(return_type) - 1;
    const rparen = fnProtoRparen(tree, fn_proto, maybe_bang);

    // The params list is a sparse set that does *not* include anytype or ... parameters.

    if (isOneLineFnProto(tree, fn_proto, lparen, rparen)) {
        // Render all on one line, no trailing comma.
        try renderToken(r, lparen, .none); // (

        var param_i: usize = 0;
        var last_param_token = lparen;
        while (true) {
            last_param_token += 1;
            switch (tree.tokenTag(last_param_token)) {
                .doc_comment => unreachable,
                .ellipsis3 => {
                    try renderToken(r, last_param_token, .none); // ...
                    break;
                },
                .keyword_noalias, .keyword_comptime => {
                    try renderToken(r, last_param_token, .maybe_space);
                    last_param_token += 1;
                },
                .identifier => {},
                .keyword_anytype => {
                    try renderToken(r, last_param_token, .none); // anytype
                    continue;
                },
                .r_paren => break,
                .comma => {
                    try renderToken(r, last_param_token, .maybe_space); // ,
                    continue;
                },
                else => {}, // Parameter type without a name.
            }
            if (tree.tokenTag(last_param_token) == .identifier and
                tree.tokenTag(last_param_token + 1) == .colon)
            {
                try renderIdentifier(r, last_param_token, .none, .preserve_when_shadowing); // name
                last_param_token = last_param_token + 1;
                try renderToken(r, last_param_token, .maybe_space); // :
                last_param_token += 1;
            }
            if (tree.tokenTag(last_param_token) == .keyword_anytype) {
                try renderToken(r, last_param_token, .none); // anytype
                continue;
            }
            const param = fn_proto.ast.params[param_i];
            param_i += 1;
            try renderExpression(r, param, .none);
            last_param_token = tree.lastToken(param);
        }
    } else {
        // One param per line.
        try ais.pushIndent(.normal);
        try renderToken(r, lparen, .newline); // (

        var param_i: usize = 0;
        var last_param_token = lparen;
        while (true) {
            last_param_token += 1;
            switch (tree.tokenTag(last_param_token)) {
                .doc_comment => {
                    try renderToken(r, last_param_token, .newline);
                    continue;
                },
                .ellipsis3 => {
                    try renderToken(r, last_param_token, .comma); // ...
                    break;
                },
                .keyword_noalias, .keyword_comptime => {
                    try renderToken(r, last_param_token, .maybe_space);
                    last_param_token += 1;
                },
                .identifier => {},
                .keyword_anytype => {
                    try renderToken(r, last_param_token, .comma); // anytype
                    if (tree.tokenTag(last_param_token + 1) == .comma)
                        last_param_token += 1;
                    continue;
                },
                .r_paren => break,
                else => {}, // Parameter type without a name.
            }
            if (tree.tokenTag(last_param_token) == .identifier and
                tree.tokenTag(last_param_token + 1) == .colon)
            {
                try renderIdentifier(r, last_param_token, .none, .preserve_when_shadowing); // name
                last_param_token += 1;
                try renderToken(r, last_param_token, .maybe_space); // :
                last_param_token += 1;
            }
            if (tree.tokenTag(last_param_token) == .keyword_anytype) {
                try renderToken(r, last_param_token, .comma); // anytype
                if (tree.tokenTag(last_param_token + 1) == .comma)
                    last_param_token += 1;
                continue;
            }
            const param = fn_proto.ast.params[param_i];
            param_i += 1;
            try ais.pushSpace(.comma);
            try renderExpression(r, param, .comma);
            ais.popSpace();
            last_param_token = tree.lastToken(param);
            if (tree.tokenTag(last_param_token + 1) == .comma) last_param_token += 1;
        }
        ais.popIndent();
    }

    try renderToken(r, rparen, .maybe_space); // )

    if (fn_proto.ast.align_expr.unwrap()) |align_expr| {
        const align_lparen = tree.firstToken(align_expr) - 1;
        const align_rparen = tree.lastToken(align_expr) + 1;

        try renderToken(r, align_lparen - 1, .none); // align
        try renderToken(r, align_lparen, .none); // (
        try renderExpression(r, align_expr, .none);
        try renderToken(r, align_rparen, .maybe_space); // )
    }

    if (fn_proto.ast.addrspace_expr.unwrap()) |addrspace_expr| {
        const align_lparen = tree.firstToken(addrspace_expr) - 1;
        const align_rparen = tree.lastToken(addrspace_expr) + 1;

        try renderToken(r, align_lparen - 1, .none); // addrspace
        try renderToken(r, align_lparen, .none); // (
        try renderExpression(r, addrspace_expr, .none);
        try renderToken(r, align_rparen, .maybe_space); // )
    }

    if (fn_proto.ast.section_expr.unwrap()) |section_expr| {
        const section_lparen = tree.firstToken(section_expr) - 1;
        const section_rparen = tree.lastToken(section_expr) + 1;

        try renderToken(r, section_lparen - 1, .none); // section
        try renderToken(r, section_lparen, .none); // (
        try renderExpression(r, section_expr, .none);
        try renderToken(r, section_rparen, .maybe_space); // )
    }

    if (fn_proto.ast.callconv_expr.unwrap()) |callconv_expr| {
        // Keep in sync with logic in `renderMember`. Search this file for the marker PROMOTE_CALLCONV_INLINE
        const is_callconv_inline = mem.eql(u8, "@\"inline\"", tree.tokenSlice(tree.nodeMainToken(callconv_expr)));
        const is_declaration = fn_proto.name_token != null;
        if (!(is_declaration and is_callconv_inline)) {
            const callconv_lparen = tree.firstToken(callconv_expr) - 1;
            const callconv_rparen = tree.lastToken(callconv_expr) + 1;

            try renderToken(r, callconv_lparen - 1, .none); // callconv
            try renderToken(r, callconv_lparen, .none); // (
            try renderExpression(r, callconv_expr, .none);
            try renderToken(r, callconv_rparen, .maybe_space); // )
        }
    }

    if (tree.tokenTag(maybe_bang) == .bang) {
        try renderToken(r, maybe_bang, .none); // !
    }
    return renderExpression(r, return_type, space);
}

fn renderSwitchCase(
    r: *Render,
    switch_case: Ast.full.SwitchCase,
    space: Space,
) Error!void {
    const ais = r.ais;
    const tree = r.tree;
    const trailing_comma = tree.tokenTag(switch_case.ast.arrow_token - 1) == .comma;
    const has_comment_before_arrow = blk: {
        if (switch_case.ast.values.len == 0) break :blk false;
        break :blk hasComment(tree, tree.firstToken(switch_case.ast.values[0]), switch_case.ast.arrow_token);
    };

    // render inline keyword
    if (switch_case.inline_token) |some| {
        try renderToken(r, some, .maybe_space);
    }

    // Render everything before the arrow
    if (switch_case.ast.values.len == 0) {
        try renderToken(r, switch_case.ast.arrow_token - 1, .space); // else keyword
    } else if (trailing_comma or has_comment_before_arrow) {
        // Render each value on a new line
        try ais.pushSpace(.comma);
        try renderExpressions(r, switch_case.ast.values, .comma);
        ais.popSpace();
    } else {
        // Render on one line
        for (switch_case.ast.values) |value_expr| {
            try renderExpression(r, value_expr, .comma_maybe_space);
        }
    }

    try renderToken(r, switch_case.ast.arrow_token, .maybe_space); // =>

    if (switch_case.payload_token) |payload_token| {
        try renderToken(r, payload_token - 1, .none); // pipe
        var ident = payload_token;
        if (tree.tokenTag(ident) == .asterisk) {
            try renderToken(r, payload_token, .none); // asterisk
            ident += 1;
        }
        try renderIdentifier(r, ident, .none, .preserve_when_shadowing); // identifier
        if (tree.tokenTag(ident + 1) == .comma) {
            ident += 2;
            try renderToken(r, ident - 1, .space); // ,
            try renderIdentifier(r, ident, .none, .preserve_when_shadowing); // identifier
        }
        try renderToken(r, ident + 1, .maybe_space); // pipe
    }

    try renderExpression(r, switch_case.ast.target_expr, space);
}

fn renderBlock(
    r: *Render,
    block_node: Ast.Node.Index,
    statements: []const Ast.Node.Index,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    const lbrace = tree.nodeMainToken(block_node);

    if (tree.isTokenPrecededByTags(lbrace, &.{ .identifier, .colon })) {
        try renderIdentifier(r, lbrace - 2, .none, .eagerly_unquote); // identifier
        try renderToken(r, lbrace - 1, .space); // :
    }
    try ais.pushIndent(.normal);
    if (statements.len == 0) {
        try renderToken(r, lbrace, .none);
        ais.popIndent();
        try renderToken(r, tree.lastToken(block_node), space); // rbrace
        return;
    }
    try renderToken(r, lbrace, .newline);
    return finishRenderBlock(r, block_node, statements, space);
}

fn finishRenderBlock(
    r: *Render,
    block_node: Ast.Node.Index,
    statements: []const Ast.Node.Index,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    for (statements, 0..) |stmt, i| {
        if (i != 0) try renderExtraNewline(r, stmt);
        if (r.fixups.omit_nodes.contains(stmt)) continue;
        try ais.pushSpace(.semicolon);
        switch (tree.nodeTag(stmt)) {
            .global_var_decl,
            .local_var_decl,
            .simple_var_decl,
            .aligned_var_decl,
            => try renderVarDecl(r, tree.fullVarDecl(stmt).?, false, .semicolon),

            else => try renderExpression(r, stmt, .semicolon),
        }
        ais.popSpace();
    }
    ais.popIndent();

    try renderToken(r, tree.lastToken(block_node), space); // rbrace
}

fn renderStructInit(
    r: *Render,
    struct_node: Ast.Node.Index,
    struct_init: Ast.full.StructInit,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;

    if (struct_init.ast.type_expr.unwrap()) |type_expr| {
        try renderExpression(r, type_expr, .none); // T
    } else {
        try renderToken(r, struct_init.ast.lbrace - 1, .none); // .
    }

    if (struct_init.ast.fields.len == 0) {
        try ais.pushIndent(.normal);
        try renderToken(r, struct_init.ast.lbrace, .none); // lbrace
        ais.popIndent();
        return renderToken(r, struct_init.ast.lbrace + 1, space); // rbrace
    }

    const rbrace = tree.lastToken(struct_node);
    const trailing_comma = tree.tokenTag(rbrace - 1) == .comma;
    if (trailing_comma or hasComment(tree, struct_init.ast.lbrace, rbrace)) {
        // Render one field init per line.
        try ais.pushIndent(.normal);
        try renderToken(r, struct_init.ast.lbrace, .newline);

        for (0.., struct_init.ast.fields) |i, field_init| {
            const init_token = tree.firstToken(field_init);
            if (i != 0)
                try renderExtraNewlineToken(r, init_token - 3);
            try renderToken(r, init_token - 3, .none); // .
            try renderIdentifier(r, init_token - 2, .space, .eagerly_unquote); // name
            try renderToken(r, init_token - 1, .maybe_space); // =

            try ais.pushSpace(.comma);
            try renderExpressionFixup(r, field_init, .comma);
            ais.popSpace();
        }

        ais.popIndent();
    } else {
        // Render all on one line, no trailing comma.
        try renderToken(r, struct_init.ast.lbrace, .space);

        for (struct_init.ast.fields) |field_init| {
            const init_token = tree.firstToken(field_init);
            try renderToken(r, init_token - 3, .none); // .
            try renderIdentifier(r, init_token - 2, .space, .eagerly_unquote); // name
            try renderToken(r, init_token - 1, .maybe_space); // =
            try renderExpressionFixup(r, field_init, .comma_space);
        }
    }

    return renderToken(r, rbrace, space);
}

fn renderArrayInit(
    r: *Render,
    array_init: Ast.full.ArrayInit,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    const gpa = r.gpa;

    if (array_init.ast.type_expr.unwrap()) |type_expr| {
        try renderExpression(r, type_expr, .none); // T
    } else {
        try renderToken(r, array_init.ast.lbrace - 1, .none); // .
    }

    if (array_init.ast.elements.len == 0) {
        try ais.pushIndent(.normal);
        try renderToken(r, array_init.ast.lbrace, .none); // lbrace
        ais.popIndent();
        return renderToken(r, array_init.ast.lbrace + 1, space); // rbrace
    }

    const last_elem = array_init.ast.elements[array_init.ast.elements.len - 1];
    const last_elem_token = tree.lastToken(last_elem);
    const trailing_comma = tree.tokenTag(last_elem_token + 1) == .comma;
    const rbrace = if (trailing_comma) last_elem_token + 2 else last_elem_token + 1;
    assert(tree.tokenTag(rbrace) == .r_brace);

    if (array_init.ast.elements.len == 1) {
        const only_elem = array_init.ast.elements[0];
        const first_token = tree.firstToken(only_elem);
        if (tree.tokenTag(first_token) != .multiline_string_literal_line and
            !anythingBetween(tree, last_elem_token, rbrace))
        {
            try renderToken(r, array_init.ast.lbrace, .none);
            try renderExpression(r, only_elem, .none);
            return renderToken(r, rbrace, space);
        }
    }

    const contains_comment = hasComment(tree, array_init.ast.lbrace, rbrace);
    const contains_multiline_string = hasMultilineString(tree, array_init.ast.lbrace, rbrace);

    if (!trailing_comma and !contains_comment and !contains_multiline_string) {
        // Render all on one line, no trailing comma.
        if (array_init.ast.elements.len == 1) {
            // If there is only one element, we don't use spaces
            try renderToken(r, array_init.ast.lbrace, .none);
            try renderExpression(r, array_init.ast.elements[0], .none);
        } else {
            try renderToken(r, array_init.ast.lbrace, .space);
            for (array_init.ast.elements) |elem| {
                try renderExpression(r, elem, .comma_space);
            }
        }
        return renderToken(r, last_elem_token + 1, space); // rbrace
    }

    try ais.pushIndent(.normal);
    try renderToken(r, array_init.ast.lbrace, .newline);
    try ais.pushSpace(.comma);

    const expr_widths = try gpa.alloc(enum(usize) {
        /// The expression contains non-printable characters (e.g. unicode / newlines)
        /// or has formatting disabled at the start or end.
        nonprint = std.math.maxInt(usize),
        _,
    }, array_init.ast.elements.len);
    defer gpa.free(expr_widths);
    {
        var buf: Writer.Allocating = .init(gpa);
        defer buf.deinit();
        var sub_ais: AutoIndentingStream = .init(gpa, &buf.writer, indent_delta);
        sub_ais.disabled_offset = ais.disabled_offset;
        defer sub_ais.deinit();
        var sub_r: Render = .{
            .gpa = r.gpa,
            .ais = &sub_ais,
            .tree = r.tree,
            .fixups = r.fixups,
        };
        for (array_init.ast.elements, expr_widths) |e, *width| {
            const begin_disabled = sub_ais.disabled_offset != null;
            // `.skip` space so trailing commments aren't included
            try renderExpressionComma(&sub_r, e, .skip);
            if (!begin_disabled and sub_ais.disabled_offset == null) {
                const w = buf.written();
                width.* = for (w) |c| {
                    if (!std.ascii.isPrint(c))
                        break .nonprint;
                } else @enumFromInt(w.len - @intFromBool(w[w.len - 1] == ','));
            } else {
                width.* = .nonprint;
            }

            // Write trailing comments since they may enable/disable zig fmt
            buf.clearRetainingCapacity();
            var after_expr = tree.la
```
