```
s.len == man.cache.prefixes_len);
        assert(man.cache.prefixes_len == 4);
        for (man.files.keys()) |file| {
            const prefixed_path: PrefixedPath = .{
                .prefix = prefix_map[file.prefixed_path.prefix],
                .sub_path = try gpa.dupe(u8, file.prefixed_path.sub_path),
            };
            errdefer gpa.free(prefixed_path.sub_path);

            const gop = try other.files.getOrPutAdapted(gpa, prefixed_path, FilesAdapter{});
            errdefer _ = other.files.pop();

            if (gop.found_existing) {
                gpa.free(prefixed_path.sub_path);
                continue;
            }

            gop.key_ptr.* = .{
                .prefixed_path = prefixed_path,
                .max_file_size = file.max_file_size,
                .handle = file.handle,
                .stat = file.stat,
                .bin_digest = file.bin_digest,
                .contents = null,
            };

            other.hash.hasher.update(&gop.key_ptr.bin_digest);
        }
    }
};

fn hashFile(io: Io, file: Io.File, bin_digest: *[Hasher.mac_length]u8) Io.File.ReadPositionalError!void {
    var buffer: [2048]u8 = undefined;
    var hasher = hasher_init;
    var offset: u64 = 0;
    while (true) {
        const n = try file.readPositional(io, &.{&buffer}, offset);
        if (n == 0) break;
        hasher.update(buffer[0..n]);
        offset += n;
    }
    hasher.final(bin_digest);
}

// Create/Write a file, close it, then grab its stat.mtime timestamp.
fn testGetCurrentFileTimestamp(io: Io, dir: Io.Dir) !Io.Timestamp {
    const test_out_file = "test-filetimestamp.tmp";

    var file = try dir.createFile(io, test_out_file, .{
        .read = true,
        .truncate = true,
    });
    defer {
        file.close(io);
        dir.deleteFile(io, test_out_file) catch {};
    }

    return (try file.stat(io)).mtime;
}

test "cache file and then recall it" {
    const io = testing.io;

    var tmp = testing.tmpDir(.{});
    defer tmp.cleanup();

    const cwd = try std.process.currentPathAlloc(io, testing.allocator);
    defer testing.allocator.free(cwd);

    const temp_file = "test.txt";
    const temp_manifest_dir = "temp_manifest_dir";

    try tmp.dir.writeFile(io, .{ .sub_path = temp_file, .data = "Hello, world!\n" });

    // Wait for file timestamps to tick
    const initial_time = try testGetCurrentFileTimestamp(io, tmp.dir);
    while ((try testGetCurrentFileTimestamp(io, tmp.dir)).nanoseconds == initial_time.nanoseconds) {
        try std.Io.Clock.Duration.sleep(.{ .clock = .boot, .raw = .fromNanoseconds(1) }, io);
    }

    var digest1: HexDigest = undefined;
    var digest2: HexDigest = undefined;

    {
        var cache: Cache = .{
            .io = io,
            .gpa = testing.allocator,
            .manifest_dir = try tmp.dir.createDirPathOpen(io, temp_manifest_dir, .{}),
            .cwd = cwd,
        };
        cache.addPrefix(.{ .path = null, .handle = tmp.dir });
        defer cache.manifest_dir.close(io);

        {
            var ch = cache.obtain();
            defer ch.deinit();

            ch.hash.add(true);
            ch.hash.add(@as(u16, 1234));
            ch.hash.addBytes("1234");
            _ = try ch.addFile(temp_file, null);

            // There should be nothing in the cache
            try testing.expectEqual(false, try ch.hit());

            digest1 = ch.final();
            try ch.writeManifest();
        }
        {
            var ch = cache.obtain();
            defer ch.deinit();

            ch.hash.add(true);
            ch.hash.add(@as(u16, 1234));
            ch.hash.addBytes("1234");
            _ = try ch.addFile(temp_file, null);

            // Cache hit! We just "built" the same file
            try testing.expect(try ch.hit());
            digest2 = ch.final();

            try testing.expectEqual(false, ch.have_exclusive_lock);
        }

        try testing.expectEqual(digest1, digest2);
    }
}

test "check that changing a file makes cache fail" {
    const io = testing.io;

    var tmp = testing.tmpDir(.{});
    defer tmp.cleanup();

    const cwd = try std.process.currentPathAlloc(io, testing.allocator);
    defer testing.allocator.free(cwd);

    const temp_file = "cache_hash_change_file_test.txt";
    const temp_manifest_dir = "cache_hash_change_file_manifest_dir";
    const original_temp_file_contents = "Hello, world!\n";
    const updated_temp_file_contents = "Hello, world; but updated!\n";

    try tmp.dir.writeFile(io, .{ .sub_path = temp_file, .data = original_temp_file_contents });

    // Wait for file timestamps to tick
    const initial_time = try testGetCurrentFileTimestamp(io, tmp.dir);
    while ((try testGetCurrentFileTimestamp(io, tmp.dir)).nanoseconds == initial_time.nanoseconds) {
        try std.Io.Clock.Duration.sleep(.{ .clock = .boot, .raw = .fromNanoseconds(1) }, io);
    }

    var digest1: HexDigest = undefined;
    var digest2: HexDigest = undefined;

    {
        var cache: Cache = .{
            .io = io,
            .gpa = testing.allocator,
            .manifest_dir = try tmp.dir.createDirPathOpen(io, temp_manifest_dir, .{}),
            .cwd = cwd,
        };
        cache.addPrefix(.{ .path = null, .handle = tmp.dir });
        defer cache.manifest_dir.close(io);

        {
            var ch = cache.obtain();
            defer ch.deinit();

            ch.hash.addBytes("1234");
            const temp_file_idx = try ch.addFile(temp_file, 100);

            // There should be nothing in the cache
            try testing.expectEqual(false, try ch.hit());

            try testing.expect(mem.eql(u8, original_temp_file_contents, ch.files.keys()[temp_file_idx].contents.?));

            digest1 = ch.final();

            try ch.writeManifest();
        }

        try tmp.dir.writeFile(io, .{ .sub_path = temp_file, .data = updated_temp_file_contents });

        {
            var ch = cache.obtain();
            defer ch.deinit();

            ch.hash.addBytes("1234");
            const temp_file_idx = try ch.addFile(temp_file, 100);

            // A file that we depend on has been updated, so the cache should not contain an entry for it
            try testing.expectEqual(false, try ch.hit());

            // The cache system does not keep the contents of re-hashed input files.
            try testing.expect(ch.files.keys()[temp_file_idx].contents == null);

            digest2 = ch.final();

            try ch.writeManifest();
        }

        try testing.expect(!mem.eql(u8, digest1[0..], digest2[0..]));
    }
}

test "no file inputs" {
    const io = testing.io;

    var tmp = testing.tmpDir(.{});
    defer tmp.cleanup();

    const cwd = try std.process.currentPathAlloc(io, testing.allocator);
    defer testing.allocator.free(cwd);

    const temp_manifest_dir = "no_file_inputs_manifest_dir";

    var digest1: HexDigest = undefined;
    var digest2: HexDigest = undefined;

    var cache: Cache = .{
        .io = io,
        .gpa = testing.allocator,
        .manifest_dir = try tmp.dir.createDirPathOpen(io, temp_manifest_dir, .{}),
        .cwd = cwd,
    };
    cache.addPrefix(.{ .path = null, .handle = tmp.dir });
    defer cache.manifest_dir.close(io);

    {
        var man = cache.obtain();
        defer man.deinit();

        man.hash.addBytes("1234");

        // There should be nothing in the cache
        try testing.expectEqual(false, try man.hit());

        digest1 = man.final();

        try man.writeManifest();
    }
    {
        var man = cache.obtain();
        defer man.deinit();

        man.hash.addBytes("1234");

        try testing.expect(try man.hit());
        digest2 = man.final();
        try testing.expectEqual(false, man.have_exclusive_lock);
    }

    try testing.expectEqual(digest1, digest2);
}

test "Manifest with files added after initial hash work" {
    const io = testing.io;

    var tmp = testing.tmpDir(.{});
    defer tmp.cleanup();

    const cwd = try std.process.currentPathAlloc(io, testing.allocator);
    defer testing.allocator.free(cwd);

    const temp_file1 = "cache_hash_post_file_test1.txt";
    const temp_file2 = "cache_hash_post_file_test2.txt";
    const temp_manifest_dir = "cache_hash_post_file_manifest_dir";

    try tmp.dir.writeFile(io, .{ .sub_path = temp_file1, .data = "Hello, world!\n" });
    try tmp.dir.writeFile(io, .{ .sub_path = temp_file2, .data = "Hello world the second!\n" });

    // Wait for file timestamps to tick
    const initial_time = try testGetCurrentFileTimestamp(io, tmp.dir);
    while ((try testGetCurrentFileTimestamp(io, tmp.dir)).nanoseconds == initial_time.nanoseconds) {
        try std.Io.Clock.Duration.sleep(.{ .clock = .boot, .raw = .fromNanoseconds(1) }, io);
    }

    var digest1: HexDigest = undefined;
    var digest2: HexDigest = undefined;
    var digest3: HexDigest = undefined;

    {
        var cache: Cache = .{
            .io = io,
            .gpa = testing.allocator,
            .manifest_dir = try tmp.dir.createDirPathOpen(io, temp_manifest_dir, .{}),
            .cwd = cwd,
        };
        cache.addPrefix(.{ .path = null, .handle = tmp.dir });
        defer cache.manifest_dir.close(io);

        {
            var ch = cache.obtain();
            defer ch.deinit();

            ch.hash.addBytes("1234");
            _ = try ch.addFile(temp_file1, null);

            // There should be nothing in the cache
            try testing.expectEqual(false, try ch.hit());

            _ = try ch.addFilePost(temp_file2);

            digest1 = ch.final();
            try ch.writeManifest();
        }
        {
            var ch = cache.obtain();
            defer ch.deinit();

            ch.hash.addBytes("1234");
            _ = try ch.addFile(temp_file1, null);

            try testing.expect(try ch.hit());
            digest2 = ch.final();

            try testing.expectEqual(false, ch.have_exclusive_lock);
        }
        try testing.expect(mem.eql(u8, &digest1, &digest2));

        // Modify the file added after initial hash
        try tmp.dir.writeFile(io, .{ .sub_path = temp_file2, .data = "Hello world the second, updated\n" });

        // Wait for file timestamps to tick
        const initial_time2 = try testGetCurrentFileTimestamp(io, tmp.dir);
        while ((try testGetCurrentFileTimestamp(io, tmp.dir)).nanoseconds == initial_time2.nanoseconds) {
            try std.Io.Clock.Duration.sleep(.{ .clock = .boot, .raw = .fromNanoseconds(1) }, io);
        }

        {
            var ch = cache.obtain();
            defer ch.deinit();

            ch.hash.addBytes("1234");
            _ = try ch.addFile(temp_file1, null);

            // A file that we depend on has been updated, so the cache should not contain an entry for it
            try testing.expectEqual(false, try ch.hit());

            _ = try ch.addFilePost(temp_file2);

            digest3 = ch.final();

            try ch.writeManifest();
        }

        try testing.expect(!mem.eql(u8, &digest1, &digest3));
    }
}



---
File: /std/Build/Fuzz.zig
---

const std = @import("../std.zig");
const Io = std.Io;
const Build = std.Build;
const Cache = Build.Cache;
const Step = std.Build.Step;
const assert = std.debug.assert;
const fatal = std.process.fatal;
const Allocator = std.mem.Allocator;
const log = std.log;
const Coverage = std.debug.Coverage;
const abi = Build.abi.fuzz;

const Fuzz = @This();
const build_runner = @import("root");

gpa: Allocator,
io: Io,
mode: Mode,

/// Allocated into `gpa`.
run_steps: []const *Step.Run,

group: Io.Group,
root_prog_node: std.Progress.Node,
prog_node: std.Progress.Node,

/// Protects `coverage_files`.
coverage_mutex: Io.Mutex,
coverage_files: std.AutoArrayHashMapUnmanaged(u64, CoverageMap),

queue_mutex: Io.Mutex,
queue_cond: Io.Condition,
msg_queue: std.ArrayList(Msg),

pub const Mode = union(enum) {
    forever: struct { ws: *Build.WebServer },
    limit: Limited,

    pub const Limited = struct {
        amount: u64,
    };
};

const Msg = union(enum) {
    coverage: struct {
        id: u64,
        cumulative: struct {
            runs: u64,
            unique: u64,
            coverage: u64,
        },
        run: *Step.Run,
    },
    entry_point: struct {
        coverage_id: u64,
        addr: u64,
    },
};

const CoverageMap = struct {
    mapped_memory: []align(std.heap.page_size_min) const u8,
    coverage: Coverage,
    source_locations: []Coverage.SourceLocation,
    /// Elements are indexes into `source_locations` pointing to the unit tests that are being fuzz tested.
    entry_points: std.ArrayList(u32),
    start_timestamp: i64,
    start_n_runs: u64,

    fn deinit(cm: *CoverageMap, gpa: Allocator) void {
        std.posix.munmap(cm.mapped_memory);
        cm.coverage.deinit(gpa);
        cm.* = undefined;
    }
};

pub fn init(
    gpa: Allocator,
    io: Io,
    all_steps: []const *Build.Step,
    root_prog_node: std.Progress.Node,
    mode: Mode,
) error{ OutOfMemory, Canceled }!Fuzz {
    const run_steps: []const *Step.Run = steps: {
        var steps: std.ArrayList(*Step.Run) = .empty;
        defer steps.deinit(gpa);
        const rebuild_node = root_prog_node.start("Rebuilding Unit Tests", 0);
        defer rebuild_node.end();
        var rebuild_group: Io.Group = .init;
        defer rebuild_group.cancel(io);

        for (all_steps) |step| {
            const run = step.cast(Step.Run) orelse continue;
            if (run.producer == null) continue;
            if (run.fuzz_tests.items.len == 0) continue;
            try steps.append(gpa, run);
            rebuild_group.async(io, rebuildTestsWorkerRun, .{ run, gpa, rebuild_node });
        }

        if (steps.items.len == 0) fatal("no fuzz tests found", .{});
        rebuild_node.setEstimatedTotalItems(steps.items.len);
        const run_steps = try gpa.dupe(*Step.Run, steps.items);
        try rebuild_group.await(io);
        break :steps run_steps;
    };
    errdefer gpa.free(run_steps);

    for (run_steps) |run| {
        assert(run.fuzz_tests.items.len > 0);
        if (run.rebuilt_executable == null)
            fatal("one or more unit tests failed to be rebuilt in fuzz mode", .{});
    }

    return .{
        .gpa = gpa,
        .io = io,
        .mode = mode,
        .run_steps = run_steps,
        .group = .init,
        .root_prog_node = root_prog_node,
        .prog_node = .none,
        .coverage_files = .empty,
        .coverage_mutex = .init,
        .queue_mutex = .init,
        .queue_cond = .init,
        .msg_queue = .empty,
    };
}

pub fn start(fuzz: *Fuzz) void {
    const io = fuzz.io;
    fuzz.prog_node = fuzz.root_prog_node.start("Fuzzing", 0);

    if (fuzz.mode == .forever) {
        // For polling messages and sending updates to subscribers.
        fuzz.group.concurrent(io, coverageRun, .{fuzz}) catch |err|
            fatal("unable to spawn coverage task: {t}", .{err});
    }

    for (fuzz.run_steps) |run| {
        assert(run.rebuilt_executable != null);
        fuzz.group.async(io, fuzzWorkerRun, .{ fuzz, run });
    }
}

pub fn deinit(fuzz: *Fuzz) void {
    const io = fuzz.io;
    fuzz.group.cancel(io);
    fuzz.prog_node.end();
    fuzz.gpa.free(fuzz.run_steps);
}

fn rebuildTestsWorkerRun(run: *Step.Run, gpa: Allocator, parent_prog_node: std.Progress.Node) void {
    rebuildTestsWorkerRunFallible(run, gpa, parent_prog_node) catch |err| {
        const compile = run.producer.?;
        log.err("step '{s}': failed to rebuild in fuzz mode: {t}", .{ compile.step.name, err });
    };
}

fn rebuildTestsWorkerRunFallible(run: *Step.Run, gpa: Allocator, parent_prog_node: std.Progress.Node) !void {
    const graph = run.step.owner.graph;
    const io = graph.io;
    const compile = run.producer.?;
    const prog_node = parent_prog_node.start(compile.step.name, 0);
    defer prog_node.end();

    const result = compile.rebuildInFuzzMode(gpa, prog_node);

    const show_compile_errors = compile.step.result_error_bundle.errorMessageCount() > 0;
    const show_error_msgs = compile.step.result_error_msgs.items.len > 0;
    const show_stderr = compile.step.result_stderr.len > 0;

    if (show_error_msgs or show_compile_errors or show_stderr) {
        var buf: [256]u8 = undefined;
        const stderr = try io.lockStderr(&buf, graph.stderr_mode);
        defer io.unlockStderr();
        build_runner.printErrorMessages(gpa, &compile.step, .{}, stderr.terminal(), .verbose, .indent) catch {};
    }

    const rebuilt_bin_path = result catch |err| switch (err) {
        error.MakeFailed => return,
        else => |other| return other,
    };
    run.rebuilt_executable = try rebuilt_bin_path.join(gpa, compile.out_filename);
}

fn fuzzWorkerRun(fuzz: *Fuzz, run: *Step.Run) void {
    const owner = run.step.owner;
    const gpa = owner.allocator;
    const graph = owner.graph;
    const io = graph.io;

    run.rerunInFuzzMode(fuzz, fuzz.prog_node) catch |err| switch (err) {
        error.MakeFailed => {
            var buf: [256]u8 = undefined;
            const stderr = io.lockStderr(&buf, graph.stderr_mode) catch |e| switch (e) {
                error.Canceled => return,
            };
            defer io.unlockStderr();
            build_runner.printErrorMessages(gpa, &run.step, .{}, stderr.terminal(), .verbose, .indent) catch {};
            return;
        },
        else => {
            log.err("step '{s}': failed to rerun in fuzz mode: {t}", .{ run.step.name, err });
            return;
        },
    };
}

pub fn serveSourcesTar(fuzz: *Fuzz, req: *std.http.Server.Request) !void {
    assert(fuzz.mode == .forever);

    var arena_state: std.heap.ArenaAllocator = .init(fuzz.gpa);
    defer arena_state.deinit();
    const arena = arena_state.allocator();

    const DedupTable = std.ArrayHashMapUnmanaged(Build.Cache.Path, void, Build.Cache.Path.TableAdapter, false);
    var dedup_table: DedupTable = .empty;
    defer dedup_table.deinit(fuzz.gpa);

    for (fuzz.run_steps) |run_step| {
        const compile_inputs = run_step.producer.?.step.inputs.table;
        for (compile_inputs.keys(), compile_inputs.values()) |dir_path, *file_list| {
            try dedup_table.ensureUnusedCapacity(fuzz.gpa, file_list.items.len);
            for (file_list.items) |sub_path| {
                if (!std.mem.endsWith(u8, sub_path, ".zig")) continue;
                const joined_path = try dir_path.join(arena, sub_path);
                dedup_table.putAssumeCapacity(joined_path, {});
            }
        }
    }

    const deduped_paths = dedup_table.keys();
    const SortContext = struct {
        pub fn lessThan(this: @This(), lhs: Build.Cache.Path, rhs: Build.Cache.Path) bool {
            _ = this;
            return switch (std.mem.order(u8, lhs.root_dir.path orelse ".", rhs.root_dir.path orelse ".")) {
                .lt => true,
                .gt => false,
                .eq => std.mem.lessThan(u8, lhs.sub_path, rhs.sub_path),
            };
        }
    };
    std.mem.sortUnstable(Build.Cache.Path, deduped_paths, SortContext{}, SortContext.lessThan);
    return fuzz.mode.forever.ws.serveTarFile(req, deduped_paths);
}

pub const Previous = struct {
    unique_runs: usize,
    entry_points: usize,
    sent_source_index: bool,
    pub const init: Previous = .{
        .unique_runs = 0,
        .entry_points = 0,
        .sent_source_index = false,
    };
};
pub fn sendUpdate(
    fuzz: *Fuzz,
    socket: *std.http.Server.WebSocket,
    prev: *Previous,
) !void {
    const io = fuzz.io;

    try fuzz.coverage_mutex.lock(io);
    defer fuzz.coverage_mutex.unlock(io);

    const coverage_maps = fuzz.coverage_files.values();
    if (coverage_maps.len == 0) return;
    // TODO: handle multiple fuzz steps in the WebSocket packets
    const coverage_map = &coverage_maps[0];
    const cov_header: *const abi.SeenPcsHeader = @ptrCast(coverage_map.mapped_memory[0..@sizeOf(abi.SeenPcsHeader)]);
    // TODO: this isn't sound! We need to do volatile reads of these bits rather than handing the
    // buffer off to the kernel, because we might race with the fuzzer process[es]. This brings the
    // whole mmap strategy into question. Incidentally, I wonder if post-writergate we could pass
    // this data straight to the socket with sendfile...
    const seen_pcs = cov_header.seenBits();
    const n_runs = @atomicLoad(usize, &cov_header.n_runs, .monotonic);
    const unique_runs = @atomicLoad(usize, &cov_header.unique_runs, .monotonic);
    {
        if (!prev.sent_source_index) {
            prev.sent_source_index = true;
            // We need to send initial context.
            const header: abi.SourceIndexHeader = .{
                .directories_len = @intCast(coverage_map.coverage.directories.entries.len),
                .files_len = @intCast(coverage_map.coverage.files.entries.len),
                .source_locations_len = @intCast(coverage_map.source_locations.len),
                .string_bytes_len = @intCast(coverage_map.coverage.string_bytes.items.len),
                .start_timestamp = coverage_map.start_timestamp,
                .start_n_runs = coverage_map.start_n_runs,
            };
            var iovecs: [5][]const u8 = .{
                @ptrCast(&header),
                @ptrCast(coverage_map.coverage.directories.keys()),
                @ptrCast(coverage_map.coverage.files.keys()),
                @ptrCast(coverage_map.source_locations),
                coverage_map.coverage.string_bytes.items,
            };
            try socket.writeMessageVec(&iovecs, .binary);
        }

        const header: abi.CoverageUpdateHeader = .{
            .n_runs = n_runs,
            .unique_runs = unique_runs,
        };
        var iovecs: [2][]const u8 = .{
            @ptrCast(&header),
            @ptrCast(seen_pcs),
        };
        try socket.writeMessageVec(&iovecs, .binary);

        prev.unique_runs = unique_runs;
    }

    if (prev.entry_points != coverage_map.entry_points.items.len) {
        const header: abi.EntryPointHeader = .init(@intCast(coverage_map.entry_points.items.len));
        var iovecs: [2][]const u8 = .{
            @ptrCast(&header),
            @ptrCast(coverage_map.entry_points.items),
        };
        try socket.writeMessageVec(&iovecs, .binary);

        prev.entry_points = coverage_map.entry_points.items.len;
    }
}

fn coverageRun(fuzz: *Fuzz) void {
    coverageRunCancelable(fuzz) catch |err| switch (err) {
        error.Canceled => return,
    };
}

fn coverageRunCancelable(fuzz: *Fuzz) Io.Cancelable!void {
    const io = fuzz.io;

    try fuzz.queue_mutex.lock(io);
    defer fuzz.queue_mutex.unlock(io);

    while (true) {
        try fuzz.queue_cond.wait(io, &fuzz.queue_mutex);
        for (fuzz.msg_queue.items) |msg| switch (msg) {
            .coverage => |coverage| prepareTables(fuzz, coverage.run, coverage.id) catch |err| switch (err) {
                error.AlreadyReported => continue,
                error.Canceled => return,
                else => |e| log.err("failed to prepare code coverage tables: {t}", .{e}),
            },
            .entry_point => |entry_point| addEntryPoint(fuzz, entry_point.coverage_id, entry_point.addr) catch |err| switch (err) {
                error.AlreadyReported => continue,
                error.Canceled => return,
                else => |e| log.err("failed to prepare code coverage tables: {t}", .{e}),
            },
        };
        fuzz.msg_queue.clearRetainingCapacity();
    }
}
fn prepareTables(fuzz: *Fuzz, run_step: *Step.Run, coverage_id: u64) error{ OutOfMemory, AlreadyReported, Canceled }!void {
    assert(fuzz.mode == .forever);
    const ws = fuzz.mode.forever.ws;
    const gpa = fuzz.gpa;
    const io = fuzz.io;

    try fuzz.coverage_mutex.lock(io);
    defer fuzz.coverage_mutex.unlock(io);

    const gop = try fuzz.coverage_files.getOrPut(gpa, coverage_id);
    if (gop.found_existing) {
        // We are fuzzing the same executable with multiple threads.
        // Perhaps the same unit test; perhaps a different one. In any
        // case, since the coverage file is the same, we only have to
        // notice changes to that one file in order to learn coverage for
        // this particular executable.
        return;
    }
    errdefer _ = fuzz.coverage_files.pop();

    gop.value_ptr.* = .{
        .coverage = std.debug.Coverage.init,
        .mapped_memory = undefined, // populated below
        .source_locations = undefined, // populated below
        .entry_points = .empty,
        .start_timestamp = ws.now(),
        .start_n_runs = undefined, // populated below
    };
    errdefer gop.value_ptr.coverage.deinit(gpa);

    const rebuilt_exe_path = run_step.rebuilt_executable.?;
    const target = run_step.producer.?.rootModuleTarget();
    var debug_info = std.debug.Info.load(
        gpa,
        io,
        rebuilt_exe_path,
        &gop.value_ptr.coverage,
        target.ofmt,
        target.cpu.arch,
    ) catch |err| {
        log.err("step '{s}': failed to load debug information for '{f}': {t}", .{
            run_step.step.name, rebuilt_exe_path, err,
        });
        return error.AlreadyReported;
    };
    defer debug_info.deinit(gpa);

    const coverage_file_path: Build.Cache.Path = .{
        .root_dir = run_step.step.owner.cache_root,
        .sub_path = "v/" ++ std.fmt.hex(coverage_id),
    };
    var coverage_file = coverage_file_path.root_dir.handle.openFile(io, coverage_file_path.sub_path, .{}) catch |err| {
        log.err("step '{s}': failed to load coverage file '{f}': {t}", .{
            run_step.step.name, coverage_file_path, err,
        });
        return error.AlreadyReported;
    };
    defer coverage_file.close(io);

    const file_size = coverage_file.length(io) catch |err| {
        log.err("unable to check len of coverage file '{f}': {t}", .{ coverage_file_path, err });
        return error.AlreadyReported;
    };

    const mapped_memory = std.posix.mmap(
        null,
        file_size,
        .{ .READ = true },
        .{ .TYPE = .SHARED },
        coverage_file.handle,
        0,
    ) catch |err| {
        log.err("failed to map coverage file '{f}': {t}", .{ coverage_file_path, err });
        return error.AlreadyReported;
    };
    gop.value_ptr.mapped_memory = mapped_memory;

    const header: *const abi.SeenPcsHeader = @ptrCast(mapped_memory[0..@sizeOf(abi.SeenPcsHeader)]);
    const pcs = header.pcAddrs();
    const source_locations = try gpa.alloc(Coverage.SourceLocation, pcs.len);
    errdefer gpa.free(source_locations);

    // Unfortunately the PCs array that LLVM gives us from the 8-bit PC
    // counters feature is not sorted.
    var sorted_pcs: std.MultiArrayList(struct { pc: u64, index: u32, sl: Coverage.SourceLocation }) = .empty;
    defer sorted_pcs.deinit(gpa);
    try sorted_pcs.resize(gpa, pcs.len);
    @memcpy(sorted_pcs.items(.pc), pcs);
    for (sorted_pcs.items(.index), 0..) |*v, i| v.* = @intCast(i);
    sorted_pcs.sortUnstable(struct {
        addrs: []const u64,

        pub fn lessThan(ctx: @This(), a_index: usize, b_index: usize) bool {
            return ctx.addrs[a_index] < ctx.addrs[b_index];
        }
    }{ .addrs = sorted_pcs.items(.pc) });

    debug_info.resolveAddresses(gpa, io, sorted_pcs.items(.pc), sorted_pcs.items(.sl)) catch |err| {
        log.err("failed to resolve addresses to source locations: {t}", .{err});
        return error.AlreadyReported;
    };

    for (sorted_pcs.items(.index), sorted_pcs.items(.sl)) |i, sl| source_locations[i] = sl;
    gop.value_ptr.source_locations = source_locations;
    gop.value_ptr.start_n_runs = header.n_runs;

    ws.notifyUpdate();
}

fn addEntryPoint(fuzz: *Fuzz, coverage_id: u64, addr: u64) error{ AlreadyReported, OutOfMemory, Canceled }!void {
    const io = fuzz.io;

    try fuzz.coverage_mutex.lock(io);
    defer fuzz.coverage_mutex.unlock(io);

    const coverage_map = fuzz.coverage_files.getPtr(coverage_id).?;
    const header: *const abi.SeenPcsHeader = @ptrCast(coverage_map.mapped_memory[0..@sizeOf(abi.SeenPcsHeader)]);
    const pcs = header.pcAddrs();

    // Since this pcs list is unsorted, we must linear scan for the best index.
    const index = i: {
        var best: usize = 0;
        for (pcs[1..], 1..) |elem_addr, i| {
            if (elem_addr == addr) break :i i;
            if (elem_addr > addr) continue;
            if (elem_addr > pcs[best]) best = i;
        }
        break :i best;
    };
    if (index >= pcs.len) {
        log.err("unable to find unit test entry address 0x{x} in source locations (range: 0x{x} to 0x{x})", .{
            addr, pcs[0], pcs[pcs.len - 1],
        });
        return error.AlreadyReported;
    }
    if (false) {
        const sl = coverage_map.source_locations[index];
        const file_name = coverage_map.coverage.stringAt(coverage_map.coverage.fileAt(sl.file).basename);
        if (pcs.len == 1) {
            log.debug("server found entry point for 0x{x} at {s}:{d}:{d} - index 0 (final)", .{
                addr, file_name, sl.line, sl.column,
            });
        } else if (index == 0) {
            log.debug("server found entry point for 0x{x} at {s}:{d}:{d} - index 0 before {x}", .{
                addr, file_name, sl.line, sl.column, pcs[index + 1],
            });
        } else if (index == pcs.len - 1) {
            log.debug("server found entry point for 0x{x} at {s}:{d}:{d} - index {d} (final) after {x}", .{
                addr, file_name, sl.line, sl.column, index, pcs[index - 1],
            });
        } else {
            log.debug("server found entry point for 0x{x} at {s}:{d}:{d} - index {d} between {x} and {x}", .{
                addr, file_name, sl.line, sl.column, index, pcs[index - 1], pcs[index + 1],
            });
        }
    }
    try coverage_map.entry_points.append(fuzz.gpa, @intCast(index));
}

pub fn waitAndPrintReport(fuzz: *Fuzz) Io.Cancelable!void {
    assert(fuzz.mode == .limit);
    const io = fuzz.io;

    try fuzz.group.await(io);
    fuzz.group = .init;

    std.debug.print("======= FUZZING REPORT =======\n", .{});
    for (fuzz.msg_queue.items) |msg| {
        if (msg != .coverage) continue;

        const cov = msg.coverage;
        const coverage_file_path: std.Build.Cache.Path = .{
            .root_dir = cov.run.step.owner.cache_root,
            .sub_path = "v/" ++ std.fmt.hex(cov.id),
        };
        var coverage_file = coverage_file_path.root_dir.handle.openFile(io, coverage_file_path.sub_path, .{}) catch |err| {
            fatal("step '{s}': failed to load coverage file '{f}': {t}", .{
                cov.run.step.name, coverage_file_path, err,
            });
        };
        defer coverage_file.close(io);

        const fuzz_abi = std.Build.abi.fuzz;
        var rbuf: [0x1000]u8 = undefined;
        var r = coverage_file.reader(io, &rbuf);

        var header: fuzz_abi.SeenPcsHeader = undefined;
        r.interface.readSliceAll(std.mem.asBytes(&header)) catch |err| {
            fatal("step '{s}': failed to read from coverage file '{f}': {t}", .{
                cov.run.step.name, coverage_file_path, err,
            });
        };

        if (header.pcs_len == 0) {
            fatal("step '{s}': corrupted coverage file '{f}': pcs_len was zero", .{
                cov.run.step.name, coverage_file_path,
            });
        }

        var seen_count: usize = 0;
        const chunk_count = fuzz_abi.SeenPcsHeader.seenElemsLen(header.pcs_len);
        for (0..chunk_count) |_| {
            const seen = r.interface.takeInt(usize, .little) catch |err| {
                fatal("step '{s}': failed to read from coverage file '{f}': {t}", .{
                    cov.run.step.name, coverage_file_path, err,
                });
            };
            seen_count += @popCount(seen);
        }

        const seen_f: f64 = @floatFromInt(seen_count);
        const total_f: f64 = @floatFromInt(header.pcs_len);
        const ratio = seen_f / total_f;
        std.debug.print(
            \\Step: {s}
            \\Fuzz test: "{s}" ({x})
            \\Runs: {} -> {}
            \\Unique runs: {} -> {}
            \\Coverage: {}/{} -> {}/{} ({:.02}%)
            \\
        , .{
            cov.run.step.name,
            cov.run.fuzz_tests.items[0],
            cov.id,
            cov.cumulative.runs,
            header.n_runs,
            cov.cumulative.unique,
            header.unique_runs,
            cov.cumulative.coverage,
            header.pcs_len,
            seen_count,
            header.pcs_len,
            ratio * 100,
        });

        std.debug.print("------------------------------\n", .{});
    }
    std.debug.print(
        \\Values are accumulated across multiple runs when preserving the cache.
        \\==============================
        \\
    , .{});
}



---
File: /std/Build/Module.zig
---

/// The one responsible for creating this module.
owner: *std.Build,
root_source_file: ?LazyPath,
/// The modules that are mapped into this module's import table.
/// Use `addImport` rather than modifying this field directly in order to
/// maintain step dependency edges.
import_table: std.StringArrayHashMapUnmanaged(*Module),

resolved_target: ?std.Build.ResolvedTarget = null,
optimize: ?std.builtin.OptimizeMode = null,
dwarf_format: ?std.dwarf.Format,

c_macros: ArrayList([]const u8),
include_dirs: ArrayList(IncludeDir),
lib_paths: ArrayList(LazyPath),
rpaths: ArrayList(RPath),
frameworks: std.StringArrayHashMapUnmanaged(LinkFrameworkOptions),
link_objects: ArrayList(LinkObject),

strip: ?bool,
unwind_tables: ?std.builtin.UnwindTables,
single_threaded: ?bool,
stack_protector: ?bool,
stack_check: ?bool,
sanitize_c: ?std.zig.SanitizeC,
sanitize_thread: ?bool,
fuzz: ?bool,
code_model: std.builtin.CodeModel,
valgrind: ?bool,
pic: ?bool,
red_zone: ?bool,
omit_frame_pointer: ?bool,
error_tracing: ?bool,
link_libc: ?bool,
link_libcpp: ?bool,
no_builtin: ?bool,

/// Symbols to be exported when compiling to WebAssembly.
export_symbol_names: []const []const u8 = &.{},

/// Caches the result of `getGraph` when called multiple times.
/// Use `getGraph` instead of accessing this field directly.
cached_graph: Graph = .{ .modules = &.{}, .names = &.{} },

pub const RPath = union(enum) {
    lazy_path: LazyPath,
    special: []const u8,
};

pub const LinkObject = union(enum) {
    static_path: LazyPath,
    other_step: *Step.Compile,
    system_lib: SystemLib,
    assembly_file: LazyPath,
    c_source_file: *CSourceFile,
    c_source_files: *CSourceFiles,
    win32_resource_file: *RcSourceFile,
};

pub const SystemLib = struct {
    name: []const u8,
    needed: bool,
    weak: bool,
    use_pkg_config: UsePkgConfig,
    preferred_link_mode: std.builtin.LinkMode,
    search_strategy: SystemLib.SearchStrategy,

    pub const UsePkgConfig = enum {
        /// Don't use pkg-config, just pass -lfoo where foo is name.
        no,
        /// Try to get information on how to link the library from pkg-config.
        /// If that fails, fall back to passing -lfoo where foo is name.
        yes,
        /// Try to get information on how to link the library from pkg-config.
        /// If that fails, error out.
        force,
    };

    pub const SearchStrategy = enum { paths_first, mode_first, no_fallback };
};

pub const CSourceLanguage = enum {
    c,
    cpp,

    objective_c,
    objective_cpp,

    /// Standard assembly
    assembly,
    /// Assembly with the C preprocessor
    assembly_with_preprocessor,

    pub fn internalIdentifier(self: CSourceLanguage) []const u8 {
        return switch (self) {
            .c => "c",
            .cpp => "c++",
            .objective_c => "objective-c",
            .objective_cpp => "objective-c++",
            .assembly => "assembler",
            .assembly_with_preprocessor => "assembler-with-cpp",
        };
    }
};

pub const CSourceFiles = struct {
    root: LazyPath,
    /// `files` is relative to `root`, which is
    /// the build root by default
    files: []const []const u8,
    flags: []const []const u8,
    /// By default, determines language of each file individually based on its file extension
    language: ?CSourceLanguage,
};

pub const CSourceFile = struct {
    file: LazyPath,
    flags: []const []const u8 = &.{},
    /// By default, determines language of each file individually based on its file extension
    language: ?CSourceLanguage = null,

    pub fn dupe(file: CSourceFile, b: *std.Build) CSourceFile {
        return .{
            .file = file.file.dupe(b),
            .flags = b.dupeStrings(file.flags),
            .language = file.language,
        };
    }
};

pub const RcSourceFile = struct {
    file: LazyPath,
    /// Any option that rc.exe accepts will work here, with the exception of:
    /// - `/fo`: The output filename is set by the build system
    /// - `/p`: Only running the preprocessor is not supported in this context
    /// - `/:no-preprocess` (non-standard option): Not supported in this context
    /// - Any MUI-related option
    /// https://learn.microsoft.com/en-us/windows/win32/menurc/using-rc-the-rc-command-line-
    ///
    /// Implicitly defined options:
    ///  /x (ignore the INCLUDE environment variable)
    ///  /D_DEBUG or /DNDEBUG depending on the optimization mode
    flags: []const []const u8 = &.{},
    /// Include paths that may or may not exist yet and therefore need to be
    /// specified as a LazyPath. Each path will be appended to the flags
    /// as `/I <resolved path>`.
    include_paths: []const LazyPath = &.{},

    pub fn dupe(file: RcSourceFile, b: *std.Build) RcSourceFile {
        const include_paths = b.allocator.alloc(LazyPath, file.include_paths.len) catch @panic("OOM");
        for (include_paths, file.include_paths) |*dest, lazy_path| dest.* = lazy_path.dupe(b);
        return .{
            .file = file.file.dupe(b),
            .flags = b.dupeStrings(file.flags),
            .include_paths = include_paths,
        };
    }
};

pub const IncludeDir = union(enum) {
    path: LazyPath,
    path_system: LazyPath,
    path_after: LazyPath,
    framework_path: LazyPath,
    framework_path_system: LazyPath,
    other_step: *Step.Compile,
    config_header_step: *Step.ConfigHeader,
    embed_path: LazyPath,

    pub fn appendZigProcessFlags(
        include_dir: IncludeDir,
        b: *std.Build,
        zig_args: *std.array_list.Managed([]const u8),
        asking_step: ?*Step,
    ) !void {
        const flag: []const u8, const lazy_path: LazyPath = switch (include_dir) {
            // zig fmt: off
            .path                  => |lp|   .{ "-I",          lp },
            .path_system           => |lp|   .{ "-isystem",    lp },
            .path_after            => |lp|   .{ "-idirafter",  lp },
            .framework_path        => |lp|   .{ "-F",          lp },
            .framework_path_system => |lp|   .{ "-iframework", lp },
            .config_header_step    => |ch|   .{ "-I",          ch.getOutputDir() },
            .other_step            => |comp| .{ "-I",          comp.installed_headers_include_tree.?.getDirectory() },
            // zig fmt: on
            .embed_path => |lazy_path| {
                // Special case: this is a single arg.
                const resolved = lazy_path.getPath3(b, asking_step);
                const arg = b.fmt("--embed-dir={f}", .{resolved});
                return zig_args.append(arg);
            },
        };
        const resolved_str = try lazy_path.getPath3(b, asking_step).toString(b.graph.arena);
        return zig_args.appendSlice(&.{ flag, resolved_str });
    }
};

pub const LinkFrameworkOptions = struct {
    /// Causes dynamic libraries to be linked regardless of whether they are
    /// actually depended on. When false, dynamic libraries with no referenced
    /// symbols will be omitted by the linker.
    needed: bool = false,
    /// Marks all referenced symbols from this library as weak, meaning that if
    /// a same-named symbol is provided by another compilation unit, instead of
    /// emitting a "duplicate symbol" error, the linker will resolve all
    /// references to the symbol with the strong version.
    ///
    /// When the linker encounters two weak symbols, the chosen one is
    /// determined by the order compilation units are provided to the linker,
    /// priority given to later ones.
    weak: bool = false,
};

/// Unspecified options here will be inherited from parent `Module` when
/// inserted into an import table.
pub const CreateOptions = struct {
    /// This could either be a generated file, in which case the module
    /// contains exactly one file, or it could be a path to the root source
    /// file of directory of files which constitute the module.
    /// If `null`, it means this module is made up of only `link_objects`.
    root_source_file: ?LazyPath = null,

    /// The table of other modules that this module can access via `@import`.
    /// Imports are allowed to be cyclical, so this table can be added to after
    /// the `Module` is created via `addImport`.
    imports: []const Import = &.{},

    target: ?std.Build.ResolvedTarget = null,
    optimize: ?std.builtin.OptimizeMode = null,

    /// `true` requires a compilation that includes this Module to link libc.
    /// `false` causes a build failure if a compilation that includes this Module would link libc.
    /// `null` neither requires nor prevents libc from being linked.
    link_libc: ?bool = null,
    /// `true` requires a compilation that includes this Module to link libc++.
    /// `false` causes a build failure if a compilation that includes this Module would link libc++.
    /// `null` neither requires nor prevents libc++ from being linked.
    link_libcpp: ?bool = null,
    single_threaded: ?bool = null,
    strip: ?bool = null,
    unwind_tables: ?std.builtin.UnwindTables = null,
    dwarf_format: ?std.dwarf.Format = null,
    code_model: std.builtin.CodeModel = .default,
    stack_protector: ?bool = null,
    stack_check: ?bool = null,
    sanitize_c: ?std.zig.SanitizeC = null,
    sanitize_thread: ?bool = null,
    fuzz: ?bool = null,
    /// Whether to emit machine code that integrates with Valgrind.
    valgrind: ?bool = null,
    /// Position Independent Code
    pic: ?bool = null,
    red_zone: ?bool = null,
    /// Whether to omit the stack frame pointer. Frees up a register and makes it
    /// more difficult to obtain stack traces. Has target-dependent effects.
    omit_frame_pointer: ?bool = null,
    error_tracing: ?bool = null,
    no_builtin: ?bool = null,
};

pub const Import = struct {
    name: []const u8,
    module: *Module,
};

pub fn init(
    m: *Module,
    owner: *std.Build,
    value: union(enum) { options: CreateOptions, existing: *const Module },
) void {
    const allocator = owner.allocator;

    switch (value) {
        .options => |options| {
            m.* = .{
                .owner = owner,
                .root_source_file = if (options.root_source_file) |lp| lp.dupe(owner) else null,
                .import_table = .empty,
                .resolved_target = options.target,
                .optimize = options.optimize,
                .link_libc = options.link_libc,
                .link_libcpp = options.link_libcpp,
                .dwarf_format = options.dwarf_format,
                .c_macros = .empty,
                .include_dirs = .empty,
                .lib_paths = .empty,
                .rpaths = .empty,
                .frameworks = .empty,
                .link_objects = .empty,
                .strip = options.strip,
                .unwind_tables = options.unwind_tables,
                .single_threaded = options.single_threaded,
                .stack_protector = options.stack_protector,
                .stack_check = options.stack_check,
                .sanitize_c = options.sanitize_c,
                .sanitize_thread = options.sanitize_thread,
                .fuzz = options.fuzz,
                .code_model = options.code_model,
                .valgrind = options.valgrind,
                .pic = options.pic,
                .red_zone = options.red_zone,
                .omit_frame_pointer = options.omit_frame_pointer,
                .error_tracing = options.error_tracing,
                .export_symbol_names = &.{},
                .no_builtin = options.no_builtin,
            };

            m.import_table.ensureUnusedCapacity(allocator, options.imports.len) catch @panic("OOM");
            for (options.imports) |dep| {
                m.import_table.putAssumeCapacity(dep.name, dep.module);
            }
        },
        .existing => |existing| {
            m.* = existing.*;
        },
    }
}

pub fn create(owner: *std.Build, options: CreateOptions) *Module {
    const m = owner.allocator.create(Module) catch @panic("OOM");
    m.init(owner, .{ .options = options });
    return m;
}

/// Adds an existing module to be used with `@import`.
pub fn addImport(m: *Module, name: []const u8, module: *Module) void {
    const b = m.owner;
    m.import_table.put(b.allocator, b.dupe(name), module) catch @panic("OOM");
}

/// Creates a new module and adds it to be used with `@import`.
pub fn addAnonymousImport(m: *Module, name: []const u8, options: CreateOptions) void {
    const module = create(m.owner, options);
    return addImport(m, name, module);
}

/// Converts a set of key-value pairs into a Zig source file, and then inserts it into
/// the Module's import table with the specified name. This makes the options importable
/// via `@import("module_name")`.
pub fn addOptions(m: *Module, module_name: []const u8, options: *Step.Options) void {
    addImport(m, module_name, options.createModule());
}

pub const LinkSystemLibraryOptions = struct {
    /// Causes dynamic libraries to be linked regardless of whether they are
    /// actually depended on. When false, dynamic libraries with no referenced
    /// symbols will be omitted by the linker.
    needed: bool = false,
    /// Marks all referenced symbols from this library as weak, meaning that if
    /// a same-named symbol is provided by another compilation unit, instead of
    /// emitting a "duplicate symbol" error, the linker will resolve all
    /// references to the symbol with the strong version.
    ///
    /// When the linker encounters two weak symbols, the chosen one is
    /// determined by the order compilation units are provided to the linker,
    /// priority given to later ones.
    weak: bool = false,
    use_pkg_config: SystemLib.UsePkgConfig = .yes,
    preferred_link_mode: std.builtin.LinkMode = .dynamic,
    search_strategy: SystemLib.SearchStrategy = .paths_first,
};

pub fn linkSystemLibrary(
    m: *Module,
    name: []const u8,
    options: LinkSystemLibraryOptions,
) void {
    const b = m.owner;

    const target = m.requireKnownTarget();
    if (std.zig.target.isLibCLibName(target, name)) {
        m.link_libc = true;
        return;
    }
    if (std.zig.target.isLibCxxLibName(target, name)) {
        m.link_libcpp = true;
        return;
    }

    m.link_objects.append(b.allocator, .{
        .system_lib = .{
            .name = b.dupe(name),
            .needed = options.needed,
            .weak = options.weak,
            .use_pkg_config = options.use_pkg_config,
            .preferred_link_mode = options.preferred_link_mode,
            .search_strategy = options.search_strategy,
        },
    }) catch @panic("OOM");
}

pub fn linkFramework(m: *Module, name: []const u8, options: LinkFrameworkOptions) void {
    const b = m.owner;
    m.frameworks.put(b.allocator, b.dupe(name), options) catch @panic("OOM");
}

pub const AddCSourceFilesOptions = struct {
    /// When provided, `files` are relative to `root` rather than the
    /// package that owns the `Compile` step.
    root: ?LazyPath = null,
    files: []const []const u8,
    flags: []const []const u8 = &.{},
    /// By default, determines language of each file individually based on its file extension
    language: ?CSourceLanguage = null,
};

/// Handy when you have many non-Zig source files and want them all to have the same flags.
pub fn addCSourceFiles(m: *Module, options: AddCSourceFilesOptions) void {
    const b = m.owner;
    const allocator = b.allocator;

    for (options.files) |path| {
        if (std.fs.path.isAbsolute(path)) {
            std.debug.panic(
                "file paths added with 'addCSourceFiles' must be relative, found absolute path '{s}'",
                .{path},
            );
        }
    }

    const c_source_files = allocator.create(CSourceFiles) catch @panic("OOM");
    c_source_files.* = .{
        .root = options.root orelse b.path(""),
        .files = b.dupeStrings(options.files),
        .flags = b.dupeStrings(options.flags),
        .language = options.language,
    };
    m.link_objects.append(allocator, .{ .c_source_files = c_source_files }) catch @panic("OOM");
}

pub fn addCSourceFile(m: *Module, source: CSourceFile) void {
    const b = m.owner;
    const allocator = b.allocator;
    const c_source_file = allocator.create(CSourceFile) catch @panic("OOM");
    c_source_file.* = source.dupe(b);
    m.link_objects.append(allocator, .{ .c_source_file = c_source_file }) catch @panic("OOM");
}

/// Resource files must have the extension `.rc`.
/// Can be called regardless of target. The .rc file will be ignored
/// if the target object format does not support embedded resources.
pub fn addWin32ResourceFile(m: *Module, source: RcSourceFile) void {
    const b = m.owner;
    const allocator = b.allocator;
    const target = m.requireKnownTarget();
    // Only the PE/COFF format has a Resource Table, so for any other target
    // the resource file is ignored.
    if (target.ofmt != .coff) return;

    const rc_source_file = allocator.create(RcSourceFile) catch @panic("OOM");
    rc_source_file.* = source.dupe(b);
    m.link_objects.append(allocator, .{ .win32_resource_file = rc_source_file }) catch @panic("OOM");
}

pub fn addAssemblyFile(m: *Module, source: LazyPath) void {
    const b = m.owner;
    m.link_objects.append(b.allocator, .{ .assembly_file = source.dupe(b) }) catch @panic("OOM");
}

pub fn addObjectFile(m: *Module, object: LazyPath) void {
    const b = m.owner;
    m.link_objects.append(b.allocator, .{ .static_path = object.dupe(b) }) catch @panic("OOM");
}

pub fn addObject(m: *Module, object: *Step.Compile) void {
    assert(object.kind == .obj or object.kind == .test_obj);
    m.linkLibraryOrObject(object);
}

pub fn linkLibrary(m: *Module, library: *Step.Compile) void {
    assert(library.kind == .lib);
    m.linkLibraryOrObject(library);
}

pub fn addAfterIncludePath(m: *Module, lazy_path: LazyPath) void {
    const b = m.owner;
    m.include_dirs.append(b.allocator, .{ .path_after = lazy_path.dupe(b) }) catch @panic("OOM");
}

pub fn addSystemIncludePath(m: *Module, lazy_path: LazyPath) void {
    const b = m.owner;
    m.include_dirs.append(b.allocator, .{ .path_system = lazy_path.dupe(b) }) catch @panic("OOM");
}

pub fn addIncludePath(m: *Module, lazy_path: LazyPath) void {
    const b = m.owner;
    m.include_dirs.append(b.allocator, .{ .path = lazy_path.dupe(b) }) catch @panic("OOM");
}

pub fn addConfigHeader(m: *Module, config_header: *Step.ConfigHeader) void {
    const allocator = m.owner.allocator;
    m.include_dirs.append(allocator, .{ .config_header_step = config_header }) catch @panic("OOM");
}

pub fn addSystemFrameworkPath(m: *Module, directory_path: LazyPath) void {
    const b = m.owner;
    m.include_dirs.append(b.allocator, .{ .framework_path_system = directory_path.dupe(b) }) catch
        @panic("OOM");
}

pub fn addFrameworkPath(m: *Module, directory_path: LazyPath) void {
    const b = m.owner;
    m.include_dirs.append(b.allocator, .{ .framework_path = directory_path.dupe(b) }) catch
        @panic("OOM");
}

pub fn addEmbedPath(m: *Module, lazy_path: LazyPath) void {
    const b = m.owner;
    m.include_dirs.append(b.allocator, .{ .embed_path = lazy_path.dupe(b) }) catch @panic("OOM");
}

pub fn addLibraryPath(m: *Module, directory_path: LazyPath) void {
    const b = m.owner;
    m.lib_paths.append(b.allocator, directory_path.dupe(b)) catch @panic("OOM");
}

pub fn addRPath(m: *Module, directory_path: LazyPath) void {
    const b = m.owner;
    m.rpaths.append(b.allocator, .{ .lazy_path = directory_path.dupe(b) }) catch @panic("OOM");
}

pub fn addRPathSpecial(m: *Module, bytes: []const u8) void {
    const b = m.owner;
    m.rpaths.append(b.allocator, .{ .special = b.dupe(bytes) }) catch @panic("OOM");
}

/// Equvialent to the following C code, applied to all C source files owned by
/// this `Module`:
/// ```c
/// #define name value
/// ```
/// `name` and `value` need not live longer than the function call.
pub fn addCMacro(m: *Module, name: []const u8, value: []const u8) void {
    const b = m.owner;
    m.c_macros.append(b.allocator, b.fmt("-D{s}={s}", .{ name, value })) catch @panic("OOM");
}

pub fn appendZigProcessFlags(
    m: *Module,
    zig_args: *std.array_list.Managed([]const u8),
    asking_step: ?*Step,
) !void {
    const b = m.owner;

    try addFlag(zig_args, m.strip, "-fstrip", "-fno-strip");
    try addFlag(zig_args, m.single_threaded, "-fsingle-threaded", "-fno-single-threaded");
    try addFlag(zig_args, m.stack_check, "-fstack-check", "-fno-stack-check");
    try addFlag(zig_args, m.stack_protector, "-fstack-protector", "-fno-stack-protector");
    try addFlag(zig_args, m.omit_frame_pointer, "-fomit-frame-pointer", "-fno-omit-frame-pointer");
    try addFlag(zig_args, m.error_tracing, "-ferror-tracing", "-fno-error-tracing");
    try addFlag(zig_args, m.sanitize_thread, "-fsanitize-thread", "-fno-sanitize-thread");
    try addFlag(zig_args, m.fuzz, "-ffuzz", "-fno-fuzz");
    try addFlag(zig_args, m.valgrind, "-fvalgrind", "-fno-valgrind");
    try addFlag(zig_args, m.pic, "-fPIC", "-fno-PIC");
    try addFlag(zig_args, m.red_zone, "-mred-zone", "-mno-red-zone");
    try addFlag(zig_args, m.no_builtin, "-fno-builtin", "-fbuiltin");

    if (m.sanitize_c) |sc| switch (sc) {
        .off => try zig_args.append("-fno-sanitize-c"),
        .trap => try zig_args.append("-fsanitize-c=trap"),
        .full => try zig_args.append("-fsanitize-c=full"),
    };

    if (m.dwarf_format) |dwarf_format| {
        try zig_args.append(switch (dwarf_format) {
            .@"32" => "-gdwarf32",
            .@"64" => "-gdwarf64",
        });
    }

    if (m.unwind_tables) |unwind_tables| {
        try zig_args.append(switch (unwind_tables) {
            .none => "-fno-unwind-tables",
            .sync => "-funwind-tables",
            .async => "-fasync-unwind-tables",
        });
    }

    try zig_args.ensureUnusedCapacity(1);
    if (m.optimize) |optimize| switch (optimize) {
        .Debug => zig_args.appendAssumeCapacity("-ODebug"),
        .ReleaseSmall => zig_args.appendAssumeCapacity("-OReleaseSmall"),
        .ReleaseFast => zig_args.appendAssumeCapacity("-OReleaseFast"),
        .ReleaseSafe => zig_args.appendAssumeCapacity("-OReleaseSafe"),
    };

    if (m.code_model != .default) {
        try zig_args.append("-mcmodel");
        try zig_args.append(@tagName(m.code_model));
    }

    if (m.resolved_target) |*target| {
        // Communicate the query via CLI since it's more compact.
        if (!target.query.isNative()) {
            try zig_args.appendSlice(&.{
                "-target", try target.query.zigTriple(b.allocator),
                "-mcpu",   try target.query.serializeCpuAlloc(b.allocator),
            });
            if (target.query.dynamic_linker) |*dynamic_linker| {
                if (dynamic_linker.get()) |dynamic_linker_path| {
                    try zig_args.append("--dynamic-linker");
                    try zig_args.append(dynamic_linker_path);
                } else {
                    try zig_args.append("--no-dynamic-linker");
                }
            }
        }
    }

    for (m.export_symbol_names) |symbol_name| {
        try zig_args.append(b.fmt("--export={s}", .{symbol_name}));
    }

    for (m.include_dirs.items) |include_dir| {
        try include_dir.appendZigProcessFlags(b, zig_args, asking_step);
    }

    try zig_args.appendSlice(m.c_macros.items);

    try zig_args.ensureUnusedCapacity(2 * m.lib_paths.items.len);
    for (m.lib_paths.items) |lib_path| {
        zig_args.appendAssumeCapacity("-L");
        zig_args.appendAssumeCapacity(lib_path.getPath2(b, asking_step));
    }

    try zig_args.ensureUnusedCapacity(2 * m.rpaths.items.len);
    for (m.rpaths.items) |rpath| switch (rpath) {
        .lazy_path => |lp| {
            zig_args.appendAssumeCapacity("-rpath");
            zig_args.appendAssumeCapacity(lp.getPath2(b, asking_step));
        },
        .special => |bytes| {
            zig_args.appendAssumeCapacity("-rpath");
            zig_args.appendAssumeCapacity(bytes);
        },
    };
}

fn addFlag(
    args: *std.array_list.Managed([]const u8),
    opt: ?bool,
    then_name: []const u8,
    else_name: []const u8,
) !void {
    const cond = opt orelse return;
    return args.append(if (cond) then_name else else_name);
}

fn linkLibraryOrObject(m: *Module, other: *Step.Compile) void {
    const allocator = m.owner.allocator;
    _ = other.getEmittedBin(); // Indicate there is a dependency on the outputted binary.

    if (other.rootModuleTarget().os.tag == .windows and other.isDynamicLibrary()) {
        _ = other.getEmittedImplib(); // Indicate dependency on the outputted implib.
    }

    m.link_objects.append(allocator, .{ .other_step = other }) catch @panic("OOM");
    m.include_dirs.append(allocator, .{ .other_step = other }) catch @panic("OOM");
}

fn requireKnownTarget(m: *Module) *const std.Target {
    const resolved_target = &(m.resolved_target orelse
        @panic("this API requires the Module to be created with a known 'target' field"));
    return &resolved_target.result;
}

/// Elements of `modules` and `names` are matched one-to-one.
pub const Graph = struct {
    modules: []const *Module,
    names: []const []const u8,
};

/// Intended to be used during the make phase only.
///
/// Given that `root` is the root `Module` of a compilation, return all `Module`s
/// in the module graph, including `root` itself. `root` is guaranteed to be the
/// first module in the returned slice.
pub fn getGraph(root: *Module) Graph {
    if (root.cached_graph.modules.len != 0) {
        return root.cached_graph;
    }

    const arena = root.owner.graph.arena;

    var modules: std.AutoArrayHashMapUnmanaged(*std.Build.Module, []const u8) = .empty;
    var next_idx: usize = 0;

    modules.putNoClobber(arena, root, "root") catch @panic("OOM");

    while (next_idx < modules.count()) {
        const mod = modules.keys()[next_idx];
        next_idx += 1;
        modules.ensureUnusedCapacity(arena, mod.import_table.count()) catch @panic("OOM");
        for (mod.import_table.keys(), mod.import_table.values()) |import_name, other_mod| {
            modules.putAssumeCapacity(other_mod, import_name);
        }
    }

    const result: Graph = .{
        .modules = modules.keys(),
        .names = modules.values(),
    };
    root.cached_graph = result;
    return result;
}

const Module = @This();
const std = @import("std");
const assert = std.debug.assert;
const LazyPath = std.Build.LazyPath;
const Step = std.Build.Step;
const ArrayList = std.ArrayList;



---
File: /std/Build/Step.zig
---

const Step = @This();
const builtin = @import("builtin");

const std = @import("../std.zig");
const Io = std.Io;
const Build = std.Build;
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const Cache = Build.Cache;
const Path = Cache.Path;
const ArrayList = std.ArrayList;

id: Id,
name: []const u8,
owner: *Build,
makeFn: MakeFn,

dependencies: std.array_list.Managed(*Step),
/// This field is empty during execution of the user's build script, and
/// then populated during dependency loop checking in the build runner.
dependants: ArrayList(*Step),
/// Collects the set of files that retrigger this step to run.
///
/// This is used by the build system's implementation of `--watch` but it can
/// also be potentially useful for IDEs to know what effects editing a
/// particular file has.
///
/// Populated within `make`. Implementation may choose to clear and repopulate,
/// retain previous value, or update.
inputs: Inputs,

/// Set this field to declare an upper bound on the amount of bytes of memory it will
/// take to run the step. Zero means no limit.
///
/// The idea to annotate steps that might use a high amount of RAM with an
/// upper bound. For example, perhaps a particular set of unit tests require 4
/// GiB of RAM, and those tests will be run under 4 different build
/// configurations at once. This would potentially require 16 GiB of memory on
/// the system if all 4 steps executed simultaneously, which could easily be
/// greater than what is actually available, potentially causing the system to
/// crash when using `zig build` at the default concurrency level.
///
/// This field causes the build runner to do two things:
/// 1. ulimit child processes, so that they will fail if it would exceed this
/// memory limit. This serves to enforce that this upper bound value is
/// correct.
/// 2. Ensure that the set of concurrent steps at any given time have a total
/// max_rss value that does not exceed the `max_total_rss` value of the build
/// runner. This value is configurable on the command line, and defaults to the
/// total system memory available.
max_rss: usize,

state: State,
pending_deps: u32,

result_error_msgs: ArrayList([]const u8),
result_error_bundle: std.zig.ErrorBundle,
result_stderr: []const u8,
result_cached: bool,
result_duration_ns: ?u64,
/// 0 means unavailable or not reported.
result_peak_rss: usize,
/// If the step is failed and this field is populated, this is the command which failed.
/// This field may be populated even if the step succeeded.
result_failed_command: ?[]const u8,
test_results: TestResults,

/// The return address associated with creation of this step that can be useful
/// to print along with debugging messages.
debug_stack_trace: std.debug.StackTrace,

pub const TestResults = struct {
    /// The total number of tests in the step. Every test has a "status" from the following:
    /// * passed
    /// * skipped
    /// * failed cleanly
    /// * crashed
    /// * timed out
    test_count: u32 = 0,

    /// The number of tests which were skipped (`error.SkipZigTest`).
    skip_count: u32 = 0,
    /// The number of tests which failed cleanly.
    fail_count: u32 = 0,
    /// The number of tests which terminated unexpectedly, i.e. crashed.
    crash_count: u32 = 0,
    /// The number of tests which timed out.
    timeout_count: u32 = 0,

    /// The number of detected memory leaks. The associated test may still have passed; indeed, *all*
    /// individual tests may have passed. However, the step as a whole fails if any test has leaks.
    leak_count: u32 = 0,
    /// The number of detected error logs. The associated test may still have passed; indeed, *all*
    /// individual tests may have passed. However, the step as a whole fails if any test logs errors.
    log_err_count: u32 = 0,

    pub fn isSuccess(tr: TestResults) bool {
        // all steps are success or skip
        return tr.fail_count == 0 and
            tr.crash_count == 0 and
            tr.timeout_count == 0 and
            // no (otherwise successful) step leaked memory or logged errors
            tr.leak_count == 0 and
            tr.log_err_count == 0;
    }

    /// Computes the number of tests which passed from the other values.
    pub fn passCount(tr: TestResults) u32 {
        return tr.test_count - tr.skip_count - tr.fail_count - tr.crash_count - tr.timeout_count;
    }
};

pub const MakeOptions = struct {
    progress_node: std.Progress.Node,
    watch: bool,
    web_server: ?*Build.WebServer,
    /// If set, this is a timeout to enforce on all individual unit tests, in nanoseconds.
    unit_test_timeout_ns: ?u64,
    /// Not to be confused with `Build.allocator`, which is an alias of `Build.graph.arena`.
    gpa: Allocator,
};

pub const MakeFn = *const fn (step: *Step, options: MakeOptions) anyerror!void;

pub const State = enum {
    precheck_unstarted,
    precheck_started,
    /// This is also used to indicate "dirty" steps that have been modified
    /// after a previous build completed, in which case, the step may or may
    /// not have been completed before. Either way, one or more of its direct
    /// file system inputs have been modified, meaning that the step needs to
    /// be re-evaluated.
    precheck_done,
    dependency_failure,
    success,
    failure,
    /// This state indicates that the step did not complete, however, it also did not fail,
    /// and it is safe to continue executing its dependencies.
    skipped,
    /// This step was skipped because it specified a max_rss that exceeded the runner's maximum.
    /// It is not safe to run its dependencies.
    skipped_oom,
};

pub const Id = enum {
    top_level,
    compile,
    install_artifact,
    install_file,
    install_dir,
    remove_dir,
    fail,
    fmt,
    translate_c,
    write_file,
    update_source_files,
    run,
    check_file,
    check_object,
    config_header,
    objcopy,
    options,
    custom,

    pub fn Type(comptime id: Id) type {
        return switch (id) {
            .top_level => Build.TopLevelStep,
            .compile => Compile,
            .install_artifact => InstallArtifact,
            .install_file => InstallFile,
            .install_dir => InstallDir,
            .fail => Fail,
            .fmt => Fmt,
            .translate_c => TranslateC,
            .write_file => WriteFile,
            .update_source_files => UpdateSourceFiles,
            .run => Run,
            .check_file => CheckFile,
            .check_object => CheckObject,
            .config_header => ConfigHeader,
            .objcopy => ObjCopy,
            .options => Options,
            .custom => @compileError("no type available for custom step"),
        };
    }
};

pub const CheckFile = @import("Step/CheckFile.zig");
pub const CheckObject = @import("Step/CheckObject.zig");
pub const ConfigHeader = @import("Step/ConfigHeader.zig");
pub const Fail = @import("Step/Fail.zig");
pub const Fmt = @import("Step/Fmt.zig");
pub const InstallArtifact = @import("Step/InstallArtifact.zig");
pub const InstallDir = @import("Step/InstallDir.zig");
pub const InstallFile = @import("Step/InstallFile.zig");
pub const ObjCopy = @import("Step/ObjCopy.zig");
pub const Compile = @import("Step/Compile.zig");
pub const Options = @import("Step/Options.zig");
pub const Run = @import("Step/Run.zig");
pub const TranslateC = @import("Step/TranslateC.zig");
pub const WriteFile = @import("Step/WriteFile.zig");
pub const UpdateSourceFiles = @import("Step/UpdateSourceFiles.zig");

pub const Inputs = struct {
    table: Table,

    pub const init: Inputs = .{
        .table = .{},
    };

    pub const Table = std.ArrayHashMapUnmanaged(Build.Cache.Path, Files, Build.Cache.Path.TableAdapter, false);
    /// The special file name "." means any changes inside the directory.
    pub const Files = ArrayList([]const u8);

    pub fn populated(inputs: *Inputs) bool {
        return inputs.table.count() != 0;
    }

    pub fn clear(inputs: *Inputs, gpa: Allocator) void {
        for (inputs.table.values()) |*files| files.deinit(gpa);
        inputs.table.clearRetainingCapacity();
    }
};

pub const StepOptions = struct {
    id: Id,
    name: []const u8,
    owner: *Build,
    makeFn: MakeFn = makeNoOp,
    first_ret_addr: ?usize = null,
    max_rss: usize = 0,
};

pub fn init(options: StepOptions) Step {
    const arena = options.owner.allocator;

    return .{
        .id = options.id,
        .name = arena.dupe(u8, options.name) catch @panic("OOM"),
        .owner = options.owner,
        .makeFn = options.makeFn,
        .dependencies = std.array_list.Managed(*Step).init(arena),
        .dependants = .empty,
        .inputs = Inputs.init,
        .state = .precheck_unstarted,
        .pending_deps = undefined, // initialized by build runner
        .max_rss = options.max_rss,
        .debug_stack_trace = blk: {
            const addr_buf = arena.alloc(usize, options.owner.debug_stack_frames_count) catch @panic("OOM");
            const first_ret_addr = options.first_ret_addr orelse @returnAddress();
            break :blk std.debug.captureCurrentStackTrace(.{ .first_address = first_ret_addr }, addr_buf);
        },
        .result_error_msgs = .empty,
        .result_error_bundle = std.zig.ErrorBundle.empty,
        .result_stderr = "",
        .result_cached = false,
        .result_duration_ns = null,
        .result_peak_rss = 0,
        .result_failed_command = null,
        .test_results = .{},
    };
}

/// If the Step's `make` function reports `error.MakeFailed`, it indicates they
/// have already reported the error. Otherwise, we add a simple error report
/// here.
pub fn make(s: *Step, options: MakeOptions) error{ MakeFailed, MakeSkipped }!void {
    const arena = s.owner.allocator;
    const graph = s.owner.graph;
    const io = graph.io;

    var start_ts: ?Io.Timestamp = t: {
        if (!graph.time_report) break :t null;
        if (s.id == .compile) break :t null;
        if (s.id == .run and s.cast(Run).?.stdio == .zig_test) break :t null;
        break :t Io.Clock.awake.now(io);
    };
    const make_result = s.makeFn(s, options);
    if (start_ts) |*ts| {
        const duration = ts.untilNow(io, .awake);
        options.web_server.?.updateTimeReportGeneric(s, duration);
    }

    make_result catch |err| switch (err) {
        error.MakeFailed => return error.MakeFailed,
        error.MakeSkipped => return error.MakeSkipped,
        else => {
            s.result_error_msgs.append(arena, @errorName(err)) catch @panic("OOM");
            return error.MakeFailed;
        },
    };

    if (!s.test_results.isSuccess()) {
        return error.MakeFailed;
    }

    if (s.max_rss != 0 and s.result_peak_rss > s.max_rss) {
        const msg = std.fmt.allocPrint(arena, "memory usage peaked at {0B:.2} ({0d} bytes), exceeding the declared upper bound of {1B:.2} ({1d} bytes)", .{
            s.result_peak_rss, s.max_rss,
        }) catch @panic("OOM");
        s.result_error_msgs.append(arena, msg) catch @panic("OOM");
    }
}

pub fn dependOn(step: *Step, other: *Step) void {
    step.dependencies.append(other) catch @panic("OOM");
}

fn makeNoOp(step: *Step, options: MakeOptions) anyerror!void {
    _ = options;

    var all_cached = true;

    for (step.dependencies.items) |dep| {
        all_cached = all_cached and dep.result_cached;
    }

    step.result_cached = all_cached;
}

pub fn cast(step: *Step, comptime T: type) ?*T {
    if (step.id == T.base_id) {
        return @fieldParentPtr("step", step);
    }
    return null;
}

/// For debugging purposes, prints identifying information about this Step.
pub fn dump(step: *Step, t: Io.Terminal) void {
    const w = t.writer;
    if (step.debug_stack_trace.return_addresses.len > 0) {
        w.print("name: '{s}'. creation stack trace:\n", .{step.name}) catch {};
        std.debug.writeStackTrace(&step.debug_stack_trace, t) catch {};
    } else {
        const field = "debug_stack_frames_count";
        comptime assert(@hasField(Build, field));
        t.setColor(.yellow) catch {};
        w.print("name: '{s}'. no stack trace collected for this step, see std.Build." ++ field ++ "\n", .{step.name}) catch {};
        t.setColor(.reset) catch {};
    }
}

/// Populates `s.result_failed_command`.
pub fn captureChildProcess(
    s: *Step,
    gpa: Allocator,
    progress_node: std.Progress.Node,
    argv: []const []const u8,
) !std.process.RunResult {
    const graph = s.owner.graph;
    const arena = graph.arena;
    const io = graph.io;

    // If an error occurs, it's happened in this command:
    assert(s.result_failed_command == null);
    s.result_failed_command = try allocPrintCmd(gpa, .inherit, null, argv);

    try handleChildProcUnsupported(s);
    try handleVerbose(s.owner, .inherit, argv);

    const result = std.process.run(arena, io, .{
        .argv = argv,
        .environ_map = &graph.environ_map,
        .progress_node = progress_node,
    }) catch |err| return s.fail("failed to run {s}: {t}", .{ argv[0], err });

    if (result.stderr.len > 0) {
        try s.result_error_msgs.append(arena, result.stderr);
    }

    return result;
}

pub fn fail(step: *Step, comptime fmt: []const u8, args: anytype) error{ OutOfMemory, MakeFailed } {
    try step.addError(fmt, args);
    return error.MakeFailed;
}

pub fn addError(step: *Step, comptime fmt: []const u8, args: anytype) error{OutOfMemory}!void {
    const arena = step.owner.allocator;
    const msg = try std.fmt.allocPrint(arena, fmt, args);
    try step.result_error_msgs.append(arena, msg);
}

pub const ZigProcess = struct {
    child: std.process.Child,
    multi_reader_buffer: Io.File.MultiReader.Buffer(2),
    multi_reader: Io.File.MultiReader,
    progress_ipc_index: ?if (std.Progress.have_ipc) std.Progress.Ipc.Index else noreturn,

    pub const StreamEnum = enum { stdout, stderr };

    pub fn saveState(zp: *ZigProcess, prog_node: std.Progress.Node) void {
        zp.progress_ipc_index = if (std.Progress.have_ipc) prog_node.takeIpcIndex() else null;
    }

    pub fn deinit(zp: *ZigProcess, io: Io) void {
        zp.child.kill(io);
        zp.multi_reader.deinit();
        zp.* = undefined;
    }
};

/// Assumes that argv contains `--listen=-` and that the process being spawned
/// is the zig compiler - the same version that compiled the build runner.
/// Populates `s.result_failed_command`.
pub fn evalZigProcess(
    s: *Step,
    argv: []const []const u8,
    prog_node: std.Progress.Node,
    watch: bool,
    web_server: ?*Build.WebServer,
    gpa: Allocator,
) !?Path {
    const b = s.owner;
    const io = b.graph.io;

    // If an error occurs, it's happened in this command:
    assert(s.result_failed_command == null);
    s.result_failed_command = try allocPrintCmd(gpa, .inherit, null, argv);

    if (s.getZigProcess()) |zp| update: {
        assert(watch);
        if (zp.progress_ipc_index) |ipc_index| prog_node.setIpcIndex(ipc_index);
        zp.progress_ipc_index = null;
        var exited = false;
        defer if (exited) {
            s.cast(Compile).?.zig_process = null;
            zp.deinit(io);
            gpa.destroy(zp);
        } else zp.saveState(prog_node);
        const result = zigProcessUpdate(s, zp, watch, web_server, gpa) catch |err| switch (err) {
            error.BrokenPipe, error.EndOfStream => |reason| {
                std.log.info("{s} restart required: {t}", .{ argv[0], reason });
                // Process restart required.
                const term = zp.child.wait(io) catch |e| {
                    return s.fail("unable to wait for {s}: {t}", .{ argv[0], e });
                };
                _ = term;
                exited = true;
                break :update;
            },
            else => |e| return e,
        };

        if (s.result_error_bundle.errorMessageCount() > 0) {
            return s.fail("{d} compilation errors", .{s.result_error_bundle.errorMessageCount()});
        }

        if (s.result_error_msgs.items.len > 0 and result == null) {
            // Crash detected.
            const term = zp.child.wait(io) catch |e| {
                return s.fail("unable to wait for {s}: {t}", .{ argv[0], e });
            };
            s.result_peak_rss = zp.child.resource_usage_statistics.getMaxRss() orelse 0;
            exited = true;
            try handleChildProcessTerm(s, term);
            return error.MakeFailed;
        }

        return result;
    }
    assert(argv.len != 0);

    try handleChildProcUnsupported(s);
    try handleVerbose(s.owner, .inherit, argv);

    const zp = try gpa.create(ZigProcess);
    defer if (!watch) gpa.destroy(zp);

    zp.child = std.process.spawn(io, .{
        .argv = argv,
        .environ_map = &b.graph.environ_map,
        .stdin = .pipe,
        .stdout = .pipe,
        .stderr = .pipe,
        .request_resource_usage_statistics = true,
        .progress_node = prog_node,
    }) catch |err| return s.fail("failed to spawn zig compiler {s}: {t}", .{ argv[0], err });

    zp.multi_reader.init(gpa, io, zp.multi_reader_buffer.toStreams(), &.{
        zp.child.stdout.?, zp.child.stderr.?,
    });
    if (watch) s.cast(Compile).?.zig_process = zp;
    defer if (!watch) zp.deinit(io);

    const result = result: {
        defer if (watch) zp.saveState(prog_node);
        break :result try zigProcessUpdate(s, zp, watch, web_server, gpa);
    };

    if (!watch) {
        // Send EOF to stdin.
        zp.child.stdin.?.close(io);
        zp.child.stdin = null;

        const term = zp.child.wait(io) catch |err| {
            return s.fail("unable to wait for {s}: {t}", .{ argv[0], err });
        };
        s.result_peak_rss = zp.child.resource_usage_statistics.getMaxRss() orelse 0;

        // Special handling for Compile step that is expecting compile errors.
        if (s.cast(Compile)) |compile| switch (term) {
            .exited => {
                // Note that the exit code may be 0 in this case due to the
                // compiler server protocol.
                if (compile.expect_errors != null) {
                    return error.NeedCompileErrorCheck;
                }
            },
            else => {},
        };

        try handleChildProcessTerm(s, term);
    }

    if (s.result_error_bundle.errorMessageCount() > 0) {
        return s.fail("{d} compilation errors", .{s.result_error_bundle.errorMessageCount()});
    }

    return result;
}

/// Wrapper around `Io.Dir.updateFile` that handles verbose and error output.
pub fn installFile(s: *Step, src_lazy_path: Build.LazyPath, dest_path: []const u8) !Io.Dir.PrevStatus {
    const b = s.owner;
    const io = b.graph.io;
    const src_path = src_lazy_path.getPath3(b, s);
    try handleVerbose(b, .inherit, &.{ "install", "-C", b.fmt("{f}", .{src_path}), dest_path });
    return Io.Dir.updateFile(src_path.root_dir.handle, io, src_path.sub_path, .cwd(), dest_path, .{}) catch |err|
        return s.fail("unable to update file from '{f}' to '{s}': {t}", .{ src_path, dest_path, err });
}

/// Wrapper around `Io.Dir.createDirPathStatus` that handles verbose and error output.
pub fn installDir(s: *Step, dest_path: []const u8) !Io.Dir.CreatePathStatus {
    const b = s.owner;
    const io = b.graph.io;
    try handleVerbose(b, .inherit, &.{ "install", "-d", dest_path });
    return Io.Dir.cwd().createDirPathStatus(io, dest_path, .default_dir) catch |err|
        return s.fail("unable to create dir '{s}': {t}", .{ dest_path, err });
}

fn zigProcessUpdate(s: *Step, zp: *ZigProcess, watch: bool, web_server: ?*Build.WebServer, gpa: Allocator) !?Path {
    const b = s.owner;
    const arena = b.allocator;
    const io = b.graph.io;

    const start_ts = Io.Clock.awake.now(io);

    try sendMessage(io, zp.child.stdin.?, .update);
    if (!watch) try sendMessage(io, zp.child.stdin.?, .exit);

    var result: ?Path = null;
    var eos_err: error{EndOfStream}!void = {};

    const stdout = zp.multi_reader.fileReader(0);

    while (true) {
        const Header = std.zig.Server.Message.Header;
        const header = stdout.interface.takeStruct(Header, .little) catch |err| switch (err) {
            error.EndOfStream => break,
            error.ReadFailed => return stdout.err.?,
        };
        const body = stdout.interface.take(header.bytes_len) catch |err| switch (err) {
            error.EndOfStream => |e| {
                // Better to report the crash with stderr below, but we set
                // this in case the child exits successfully while violating
                // this protocol.
                eos_err = e;
                break;
            },
            error.ReadFailed => return stdout.err.?,
        };
        switch (header.tag) {
            .zig_version => {
                if (!std.mem.eql(u8, builtin.zig_version_string, body)) {
                    return s.fail(
                        "zig version mismatch build runner vs compiler: '{s}' vs '{s}'",
                        .{ builtin.zig_version_string, body },
                    );
                }
            },
            .error_bundle => {
                s.result_error_bundle = try std.zig.Server.allocErrorBundle(gpa, body);
                // This message indicates the end of the update.
                if (watch) break;
            },
            .emit_digest => {
                const EmitDigest = std.zig.Server.Message.EmitDigest;
                const emit_digest: *align(1) const EmitDigest = @ptrCast(body);
                s.result_cached = emit_digest.flags.cache_hit;
                const digest = body[@sizeOf(EmitDigest)..][0..Cache.bin_digest_len];
                result = .{
                    .root_dir = b.cache_root,
                    .sub_path = try arena.dupe(u8, "o" ++ std.fs.path.sep_str ++ Cache.binToHex(digest.*)),
                };
            },
            .file_system_inputs => {
                s.clearWatchInputs();
                var it = std.mem.splitScalar(u8, body, 0);
                while (it.next()) |prefixed_path| {
                    const prefix_index: std.zig.Server.Message.PathPrefix = @enumFromInt(prefixed_path[0] - 1);
                    const sub_path = try arena.dupe(u8, prefixed_path[1..]);
                    const sub_path_dirname = std.fs.path.dirname(sub_path) orelse "";
                    switch (prefix_index) {
                        .cwd => {
                            const path: Build.Cache.Path = .{
                                .root_dir = Build.Cache.Directory.cwd(),
                                .sub_path = sub_path_dirname,
                            };
                            try addWatchInputFromPath(s, path, std.fs.path.basename(sub_path));
                        },
                        .zig_lib => zl: {
                            if (s.cast(Step.Compile)) |compile| {
                                if (compile.zig_lib_dir) |zig_lib_dir| {
                                    const lp = try zig_lib_dir.join(arena, sub_path);
                                    try addWatchInput(s, lp);
                                    break :zl;
                                }
                            }
                            const path: Build.Cache.Path = .{
                                .root_dir = s.owner.graph.zig_lib_directory,
                                .sub_path = sub_path_dirname,
                            };
                            try addWatchInputFromPath(s, path, std.fs.path.basename(sub_path));
                        },
                        .local_cache => {
                            const path: Build.Cache.Path = .{
                                .root_dir = b.cache_root,
                                .sub_path = sub_path_dirname,
                            };
                            try addWatchInputFromPath(s, path, std.fs.path.basename(sub_path));
                        },
                        .global_cache => {
                            const path: Build.Cache.Path = .{
                                .root_dir = s.owner.graph.global_cache_root,
                                .sub_path = sub_path_dirname,
                            };
                            try addWatchInputFromPath(s, path, std.fs.path.basename(sub_path));
                        },
                    }
                }
            },
            .time_report => if (web_server) |ws| {
                const TimeReport = std.zig.Server.Message.TimeReport;
                const tr: *align(1) const TimeReport = @ptrCast(body[0..@sizeOf(TimeReport)]);
                ws.updateTimeReportCompile(.{
                    .compile = s.cast(Step.Compile).?,
                    .use_llvm = tr.flags.use_llvm,
                    .stats = tr.stats,
                    .ns_total = @intCast(start_ts.untilNow(io, .awake).toNanoseconds()),
                    .llvm_pass_timings_len = tr.llvm_pass_timings_len,
                    .files_len = tr.files_len,
                    .decls_len = tr.decls_len,
                    .trailing = body[@sizeOf(TimeReport)..],
                });
            },
            else => {}, // ignore other messages
        }
    }

    s.result_duration_ns = @intCast(start_ts.untilNow(io, .awake).toNanoseconds());

    const stderr_contents = zp.multi_reader.reader(1).buffered();
    if (stderr_contents.len > 0) {
        try s.result_error_msgs.append(arena, try arena.dupe(u8, stderr_contents));
    }

    try eos_err;

    return result;
}

pub fn getZigProcess(s: *Step) ?*ZigProcess {
    return switch (s.id) {
        .compile => s.cast(Compile).?.zig_process,
        else => null,
    };
}

fn sendMessage(io: Io, file: Io.File, tag: std.zig.Client.Message.Tag) !void {
    const header: std.zig.Client.Message.Header = .{
        .tag = tag,
        .bytes_len = 0,
    };
    var w = file.writer(io, &.{});
    w.interface.writeStruct(header, .little) catch |err| switch (err) {
        error.WriteFailed => return w.err.?,
    };
}

pub fn handleVerbose(
    b: *Build,
    cwd: std.process.Child.Cwd,
    argv: []const []const u8,
) error{OutOfMemory}!void {
    return handleVerbose2(b, cwd, null, argv);
}

pub fn handleVerbose2(
    b: *Build,
    cwd: std.process.Child.Cwd,
    opt_env: ?*const std.process.Environ.Map,
    argv: []const []const u8,
) error{OutOfMemory}!void {
    if (b.verbose) {
        const graph = b.graph;
        // Intention of verbose is to print all sub-process command lines to
        // stderr before spawning them.
        const text = try allocPrintCmd(b.allocator, cwd, if (opt_env) |env| .{
            .child = env,
            .parent = &graph.environ_map,
        } else null, argv);
        std.debug.print("{s}\n", .{text});
    }
}

/// Asserts that the caller has already populated `s.result_failed_command`.
pub inline fn handleChildProcUnsupported(s: *Step) error{ OutOfMemory, MakeFailed }!void {
    if (!std.process.can_spawn) {
        return s.fail("unable to spawn process: host cannot spawn child processes", .{});
    }
}

/// Asserts that the caller has already populated `s.result_failed_command`.
pub fn handleChildProcessTerm(s: *Step, term: std.process.Child.Term) error{ MakeFailed, OutOfMemory }!void {
    assert(s.result_failed_command != null);
    return switch (term) {
        .exited => |code| if (code != 0) s.fail("process exited with error code {d}", .{code}),
        .signal => |sig| s.fail("process terminated with signal {t}", .{sig}),
        .stopped => |sig| s.fail("process stopped with signal {t}", .{sig}),
        .unknown => s.fail("process terminated unexpectedly", .{}),
    };
}

pub fn allocPrintCmd(
    gpa: Allocator,
    cwd: std.process.Child.Cwd,
    opt_env: ?struct {
        child: *const std.process.Environ.Map,
        parent: *const std.process.Environ.Map,
    },
    argv: []const []const u8,
) Allocator.Error![]u8 {
    const shell = struct {
        fn escape(writer: *Io.Writer, string: []const u8, is_argv0: bool) !void {
            for (string) |c| {
                if (switch (c) {
                    else => true,
                    '%', '+'...':', '@'...'Z', '_', 'a'...'z' => false,
                    '=' => is_argv0,
                }) break;
            } else return writer.writeAll(string);

            try writer.writeByte('"');
            for (string) |c| {
                if (switch (c) {
                    std.ascii.control_code.nul => break,
                    '!', '"', '$', '\\', '`' => true,
                    else => !std.ascii.isPrint(c),
                }) try writer.writeByte('\\');
                switch (c) {
                    std.ascii.control_code.nul => unreachable,
                    std.ascii.control_code.bel => try writer.writeByte('a'),
                    std.ascii.control_code.bs => try writer.writeByte('b'),
                    std.ascii.control_code.ht => try writer.writeByte('t'),
                    std.ascii.control_code.lf => try writer.writeByte('n'),
                    std.ascii.control_code.vt => try writer.writeByte('v'),
                    std.ascii.control_code.ff => try writer.writeByte('f'),
                    std.ascii.control_code.cr => try writer.writeByte('r'),
                    std.ascii.control_code.esc => try writer.writeByte('E'),
                    ' '...'~' => try writer.writeByte(c),
                    else => try writer.print("{o:0>3}", .{c}),
                }
            }
            try writer.writeByte('"');
        }
    };

    var aw: Io.Writer.Allocating = .init(gpa);
    defer aw.deinit();
    const writer = &aw.writer;
    switch (cwd) {
        .inherit => {},
        .path => |path| writer.print("cd {s} && ", .{path}) catch return error.OutOfMemory,
        .dir => @panic("TODO"),
    }
    if (opt_env) |env| {
        var it = env.child.iterator();
        while (it.next()) |entry| {
            const key = entry.key_ptr.*;
            const value = entry.value_ptr.*;
            if (env.parent.get(key)) |process_value| {
                if (std.mem.eql(u8, value, process_value)) continue;
            }
            writer.print("{s}=", .{key}) catch return error.OutOfMemory;
            shell.escape(writer, value, false) catch return error.OutOfMemory;
            writer.writeByte(' ') catch return error.OutOfMemory;
        }
    }
    shell.escape(writer, argv[0], true) catch return error.OutOfMemory;
    for (argv[1..]) |arg| {
        writer.writeByte(' ') catch return error.OutOfMemory;
        shell.escape(writer, arg, false) catch return error.OutOfMemory;
    }
    return aw.toOwnedSlice();
}

/// Prefer `cacheHitAndWatch` unless you already added watch inputs
/// separately from using the cache system.
pub fn cacheHit(s: *Step, man: *Build.Cache.Manifest) !bool {
    s.result_cached = man.hit() catch |err| return failWithCacheError(s, man, err);
    return s.result_cached;
}

/// Clears previous watch inputs, if any, and then populates watch inputs from
/// the full set of files picked up by the cache manifest.
///
/// Must be accompanied with `writeManifestAndWatch`.
pub fn cacheHitAndWatch(s: *Step, man: *Build.Cache.Manifest) !bool {
    const is_hit = man.hit() catch |err| return failWithCacheError(s, man, err);
    s.result_cached = is_hit;
    // The above call to hit() populates the manifest with files, so in case of
    // a hit, we need to populate watch inputs.
    if (is_hit) try setWatchInputsFromManifest(s, man);
    return is_hit;
}

fn failWithCacheError(
    s: *Step,
    man: *const Build.Cache.Manifest,
    err: Build.Cache.Manifest.HitError,
) error{ OutOfMemory, Canceled, MakeFailed } {
    switch (err) {
        error.CacheCheckFailed => switch (man.diagnostic) {
            .none => unreachable,
            .manifest_create, .manifest_read, .manifest_lock => |e| return s.fail("failed to check cache: {t} {t}", .{
                man.diagnostic, e,
            }),
            .file_open, .file_stat, .file_read, .file_hash => |op| {
                const pp = man.files.keys()[op.file_index].prefixed_path;
                const prefix = man.cache.prefixes()[pp.prefix].path orelse "";
                return s.fail("failed to check cache: '{s}{c}{s}' {t} {t}", .{
                    prefix, std.fs.path.sep, pp.sub_path, man.diagnostic, op.err,
                });
            },
        },
        error.OutOfMemory => return error.OutOfMemory,
        error.Canceled => return error.Canceled,
        error.InvalidFormat => return s.fail("failed to check cache: invalid manifest file format", .{}),
    }
}

/// Prefer `writeManifestAndWatch` unless you already added watch inputs
/// separately from using the cache system.
pub fn writeManifest(s: *Step, man: *Build.Cache.Manifest) !void {
    if (s.test_results.isSuccess()) {
        man.writeManifest() catch |err| {
            try s.addError("unable to write cache manifest: {t}", .{err});
        };
    }
}

/// Clears previous watch inputs, if any, and then populates watch inputs from
/// the full set of files picked up by the cache manifest.
///
/// Must be accompanied with `cacheHitAndWatch`.
pub fn writeManifestAndWatch(s: *Step, man: *Build.Cache.Manifest) !void {
    try writeManifest(s, man);
    try setWatchInputsFromManifest(s, man);
}

fn setWatchInputsFromManifest(s: *Step, man: *Build.Cache.Manifest) !void {
    const arena = s.owner.allocator;
    const prefixes = man.cache.prefixes();
    clearWatchInputs(s);
    for (man.files.keys()) |file| {
        // The file path data is freed when the cache manifest is cleaned up at the end of `make`.
        const sub_path = try arena.dupe(u8, file.prefixed_path.sub_path);
        try addWatchInputFromPath(s, .{
            .root_dir = prefixes[file.prefixed_path.prefix],
            .sub_path = std.fs.path.dirname(sub_path) orelse "",
        }, std.fs.path.basename(sub_path));
    }
}

/// For steps that have a single input that never changes when re-running `make`.
pub fn singleUnchangingWatchInput(step: *Step, lazy_path: Build.LazyPath) Allocator.Error!void {
    if (!step.inputs.populated()) try step.addWatchInput(lazy_path);
}

pub fn clearWatchInputs(step: *Step) void {
    const gpa = step.owner.allocator;
    step.inputs.clear(gpa);
}

/// Places a *file* dependency on the path.
pub fn addWatchInput(step: *Step, lazy_file: Build.LazyPath) Allocator.Error!void {
    switch (lazy_file) {
        .src_path => |src_path| try addWatchInputFromBuilder(step, src_path.owner, src_path.sub_path),
        .dependency => |d| try addWatchInputFromBuilder(step, d.dependency.builder, d.sub_path),
        .cwd_relative => |path_string| {
            try addWatchInputFromPath(step, .{
                .root_dir = .{
                    .path = null,
                    .handle = Io.Dir.cwd(),
                },
                .sub_path = std.fs.path.dirname(path_string) orelse "",
            }, std.fs.path.basename(path_string));
        },
        // Nothing to watch because this dependency edge is modeled instead via `dependants`.
        .generated => {},
    }
}

/// Any changes inside the directory will trigger invalidation.
///
/// See also `addDirectoryWatchInputFromPath` which takes a `Build.Cache.Path` instead.
///
/// Paths derived from this directory should also be manually added via
/// `addDirectoryWatchInputFromPath` if and only if this function returns
/// `true`.
pub fn addDirectoryWatchInput(step: *Step, lazy_directory: Build.LazyPath) Allocator.Error!bool {
    switch (lazy_directory) {
        .src_path => |src_path| try addDirectoryWatchInputFromBuilder(step, src_path.owner, src_path.sub_path),
        .dependency => |d| try addDirectoryWatchInputFromBuilder(step, d.dependency.builder, d.sub_path),
        .cwd_relative => |path_string| {
            try addDirectoryWatchInputFromPath(step, .{
                .root_dir = .{
                    .path = null,
                    .handle = Io.Dir.cwd(),
                },
                .sub_path = path_string,
            });
        },
        // Nothing to watch because this dependency edge is modeled instead via `dependants`.
        .generated => return false,
    }
    return true;
}

/// Any changes inside the directory will trigger invalidation.
///
/// See also `addDirectoryWatchInput` which takes a `Build.LazyPath` instead.
///
/// This function should only be called when it has been verified that the
/// dependency on `path` is not already accounted for by a `Step` dependency.
/// In other words, before calling this function, first check that the
/// `Build.LazyPath` which this `path` is derived from is not `generated`.
pub fn addDirectoryWatchInputFromPath(step: *Step, path: Build.Cache.Path) !void {
    return addWatchInputFromPath(step, path, ".");
}

fn addWatchInputFromBuilder(step: *Step, builder: *Build, sub_path: []const u8) !void {
    return addWatchInputFromPath(step, .{
        .root_dir = builder.build_root,
        .sub_path = std.fs.path.dirname(sub_path) orelse "",
    }, std.fs.path.basename(sub_path));
}

fn addDirectoryWatchInputFromBuilder(step: *Step, builder: *Build, sub_path: []const u8) !void {
    return addDirectoryWatchInputFromPath(step, .{
        .root_dir = builder.build_root,
        .sub_path = sub_path,
    });
}

fn addWatchInputFromPath(step: *Step, path: Build.Cache.Path, basename: []const u8) !void {
    const gpa = step.owner.allocator;
    const gop = try step.inputs.table.getOrPut(gpa, path);
    if (!gop.found_existing) gop.value_ptr.* = .empty;
    try gop.value_ptr.append(gpa, basename);
}

/// Implementation detail of file watching and forced rebuilds. Prepares the step for being re-evaluated.
pub fn reset(step: *Step, gpa: Allocator) void {
    assert(step.state == .precheck_done);

    if (step.result_failed_command) |cmd| gpa.free(cmd);

    step.result_error_msgs.clearRetainingCapacity();
    step.result_stderr = "";
    step.result_cached = false;
    step.result_duration_ns = null;
    step.result_peak_rss = 0;
    step.result_failed_command = null;
    step.test_results = .{};

    step.result_error_bundle.deinit(gpa);
    step.result_error_bundle = std.zig.ErrorBundle.empty;
}

/// Implementation detail of file watching. Prepares the step for being re-evaluated.
/// Returns `true` if the step was newly invalidated, `false` if it was already invalidated.
pub fn invalidateResult(step: *Step, gpa: Allocator) bool {
    if (step.state == .precheck_done) return false;
    assert(step.pending_deps == 0);
    step.state = .precheck_done;
    step.reset(gpa);
    for (step.dependants.items) |dependant| {
        _ = dependant.invalidateResult(gpa);
        dependant.pending_deps += 1;
    }
    return true;
}

test {
    _ = CheckFile;
    _ = CheckObject;
    _ = Fail;
    _ = Fmt;
    _ = InstallArtifact;
    _ = InstallDir;
    _ = InstallFile;
    _ = ObjCopy;
    _ = Compile;
    _ = Options;
    _ = Run;
    _ = TranslateC;
    _ = WriteFile;
    _ = UpdateSourceFiles;
}



---
File: /std/Build/Watch.zig
---

const builtin = @import("builtin");

const std = @import("../std.zig");
const Io = std.Io;
const Step = std.Build.Step;
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const fatal = std.process.fatal;
const Watch = @This();
const FsEvents = @import("Watch/FsEvents.zig");

os: Os,
/// The number to show as the number of directories being watched.
dir_count: usize,
// These fields are common to most implementations so are kept here for simplicity.
// They are `undefined` on implementations which do not utilize then.
dir_table: DirTable,
generation: Generation,

pub const have_impl = Os != void;

/// Key is the directory to watch which contains one or more files we are
/// interested in noticing changes to.
///
/// Value is generation.
const DirTable = std.ArrayHashMapUnmanaged(Cache.Path, void, Cache.Path.TableAdapter, false);

/// Special key of "." means any changes in this directory trigger the steps.
const ReactionSet = std.StringArrayHashMapUnmanaged(StepSet);
const StepSet = std.AutoArrayHashMapUnmanaged(*Step, Generation);

const Generation = u8;

const Hash = std.hash.Wyhash;
const Cache = std.Build.Cache;

const Os = switch (builtin.os.tag) {
    .linux => struct {
        const posix = std.posix;

        /// Keyed differently but indexes correspond 1:1 with `dir_table`.
        handle_table: HandleTable,
        /// fanotify file descriptors are keyed by mount id since marks
        /// are limited to a single filesystem.
        poll_fds: std.AutoArrayHashMapUnmanaged(MountId, posix.pollfd),

        const MountId = i
```
