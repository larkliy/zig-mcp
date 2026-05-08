```
  write_file.maybeUpdateName();
    source.addStepDependencies(&write_file.step);
    return .{
        .generated = .{
            .file = &write_file.generated_directory,
            .sub_path = dir.sub_path,
        },
    };
}

/// Returns a `LazyPath` representing the base directory that contains all the
/// files from this `WriteFile`.
pub fn getDirectory(write_file: *WriteFile) std.Build.LazyPath {
    return .{ .generated = .{ .file = &write_file.generated_directory } };
}

fn maybeUpdateName(write_file: *WriteFile) void {
    if (write_file.files.items.len == 1 and write_file.directories.items.len == 0) {
        // First time adding a file; update name.
        if (std.mem.eql(u8, write_file.step.name, "WriteFile")) {
            write_file.step.name = write_file.step.owner.fmt("WriteFile {s}", .{write_file.files.items[0].sub_path});
        }
    } else if (write_file.directories.items.len == 1 and write_file.files.items.len == 0) {
        // First time adding a directory; update name.
        if (std.mem.eql(u8, write_file.step.name, "WriteFile")) {
            write_file.step.name = write_file.step.owner.fmt("WriteFile {s}", .{write_file.directories.items[0].sub_path});
        }
    }
}

fn make(step: *Step, options: Step.MakeOptions) !void {
    _ = options;
    const b = step.owner;
    const graph = b.graph;
    const io = graph.io;
    const arena = b.allocator;
    const gpa = graph.cache.gpa;
    const write_file: *WriteFile = @fieldParentPtr("step", step);

    const open_dir_cache = try arena.alloc(Io.Dir, write_file.directories.items.len);
    var open_dirs_count: usize = 0;
    defer Io.Dir.closeMany(io, open_dir_cache[0..open_dirs_count]);

    switch (write_file.mode) {
        .whole_cached => {
            step.clearWatchInputs();

            // The cache is used here not really as a way to speed things up - because writing
            // the data to a file would probably be very fast - but as a way to find a canonical
            // location to put build artifacts.

            // If, for example, a hard-coded path was used as the location to put WriteFile
            // files, then two WriteFiles executing in parallel might clobber each other.

            var man = b.graph.cache.obtain();
            defer man.deinit();

            for (write_file.files.items) |file| {
                man.hash.addBytes(file.sub_path);

                switch (file.contents) {
                    .bytes => |bytes| {
                        man.hash.addBytes(bytes);
                    },
                    .copy => |lazy_path| {
                        const path = lazy_path.getPath3(b, step);
                        _ = try man.addFilePath(path, null);
                        try step.addWatchInput(lazy_path);
                    },
                }
            }

            for (write_file.directories.items, open_dir_cache) |dir, *open_dir_cache_elem| {
                man.hash.addBytes(dir.sub_path);
                for (dir.options.exclude_extensions) |ext| man.hash.addBytes(ext);
                if (dir.options.include_extensions) |incs| for (incs) |inc| man.hash.addBytes(inc);

                const need_derived_inputs = try step.addDirectoryWatchInput(dir.source);
                const src_dir_path = dir.source.getPath3(b, step);

                var src_dir = src_dir_path.root_dir.handle.openDir(io, src_dir_path.subPathOrDot(), .{ .iterate = true }) catch |err| {
                    return step.fail("unable to open source directory '{f}': {s}", .{
                        src_dir_path, @errorName(err),
                    });
                };
                open_dir_cache_elem.* = src_dir;
                open_dirs_count += 1;

                var it = try src_dir.walk(gpa);
                defer it.deinit();
                while (try it.next(io)) |entry| {
                    if (!dir.options.pathIncluded(entry.path)) continue;

                    switch (entry.kind) {
                        .directory => {
                            if (need_derived_inputs) {
                                const entry_path = try src_dir_path.join(arena, entry.path);
                                try step.addDirectoryWatchInputFromPath(entry_path);
                            }
                        },
                        .file => {
                            const entry_path = try src_dir_path.join(arena, entry.path);
                            _ = try man.addFilePath(entry_path, null);
                        },
                        else => continue,
                    }
                }
            }

            if (try step.cacheHit(&man)) {
                const digest = man.final();
                write_file.generated_directory.path = try b.cache_root.join(arena, &.{ "o", &digest });
                assert(step.result_cached);
                return;
            }

            const digest = man.final();
            const cache_path = "o" ++ Dir.path.sep_str ++ digest;

            write_file.generated_directory.path = try b.cache_root.join(arena, &.{cache_path});

            try operate(write_file, open_dir_cache, .{
                .root_dir = b.cache_root,
                .sub_path = cache_path,
            });

            try step.writeManifest(&man);
        },
        .tmp => {
            step.result_cached = false;

            var rand_int: u64 = undefined;
            io.random(@ptrCast(&rand_int));
            const tmp_dir_sub_path = "tmp" ++ Dir.path.sep_str ++ std.fmt.hex(rand_int);

            write_file.generated_directory.path = try b.cache_root.join(arena, &.{tmp_dir_sub_path});

            try operate(write_file, open_dir_cache, .{
                .root_dir = b.cache_root,
                .sub_path = tmp_dir_sub_path,
            });
        },
        .mutate => |lp| {
            step.result_cached = false;
            const root_path = try lp.getPath4(b, step);
            write_file.generated_directory.path = try root_path.toString(arena);
            try operate(write_file, open_dir_cache, root_path);
        },
    }
}

fn operate(write_file: *WriteFile, open_dir_cache: []const Io.Dir, root_path: std.Build.Cache.Path) !void {
    const step = &write_file.step;
    const b = step.owner;
    const io = b.graph.io;
    const gpa = b.graph.cache.gpa;
    const arena = b.allocator;

    var cache_dir = root_path.root_dir.handle.createDirPathOpen(io, root_path.sub_path, .{}) catch |err|
        return step.fail("unable to make path {f}: {t}", .{ root_path, err });
    defer cache_dir.close(io);

    for (write_file.files.items) |file| {
        if (Dir.path.dirname(file.sub_path)) |dirname| {
            cache_dir.createDirPath(io, dirname) catch |err| {
                return step.fail("unable to make path '{f}{c}{s}': {t}", .{
                    root_path, Dir.path.sep, dirname, err,
                });
            };
        }
        switch (file.contents) {
            .bytes => |bytes| {
                cache_dir.writeFile(io, .{ .sub_path = file.sub_path, .data = bytes }) catch |err| {
                    return step.fail("unable to write file '{f}{c}{s}': {t}", .{
                        root_path, Dir.path.sep, file.sub_path, err,
                    });
                };
            },
            .copy => |file_source| {
                const source_path = file_source.getPath2(b, step);
                const prev_status = Io.Dir.updateFile(.cwd(), io, source_path, cache_dir, file.sub_path, .{}) catch |err| {
                    return step.fail("unable to update file from '{s}' to '{f}{c}{s}': {t}", .{
                        source_path, root_path, Dir.path.sep, file.sub_path, err,
                    });
                };
                // At this point we already will mark the step as a cache miss.
                // But this is kind of a partial cache hit since individual
                // file copies may be avoided. Oh well, this information is
                // discarded.
                _ = prev_status;
            },
        }
    }

    for (write_file.directories.items, open_dir_cache) |dir, already_open_dir| {
        const src_dir_path = dir.source.getPath3(b, step);
        const dest_dirname = dir.sub_path;

        if (dest_dirname.len != 0) {
            cache_dir.createDirPath(io, dest_dirname) catch |err| {
                return step.fail("unable to make path '{f}{c}{s}': {t}", .{
                    root_path, Dir.path.sep, dest_dirname, err,
                });
            };
        }

        var it = try already_open_dir.walk(gpa);
        defer it.deinit();
        while (try it.next(io)) |entry| {
            if (!dir.options.pathIncluded(entry.path)) continue;

            const src_entry_path = try src_dir_path.join(arena, entry.path);
            const dest_path = b.pathJoin(&.{ dest_dirname, entry.path });
            switch (entry.kind) {
                .directory => try cache_dir.createDirPath(io, dest_path),
                .file => {
                    const prev_status = Io.Dir.updateFile(
                        src_entry_path.root_dir.handle,
                        io,
                        src_entry_path.sub_path,
                        cache_dir,
                        dest_path,
                        .{},
                    ) catch |err| {
                        return step.fail("unable to update file from '{f}' to '{f}{c}{s}': {t}", .{
                            src_entry_path, root_path, Dir.path.sep, dest_path, err,
                        });
                    };
                    _ = prev_status;
                },
                else => continue,
            }
        }
    }
}



---
File: /std/Build/Watch/FsEvents.zig
---

//! An implementation of file-system watching based on the `FSEventStream` API in macOS.
//! While macOS supports kqueue, it does not allow detecting changes to files without
//! placing watches on each individual file, meaning FD limits are reached incredibly
//! quickly. The File System Events API works differently: it implements *recursive*
//! directory watches, managed by a system service. Rather than being in libc, the API is
//! exposed by the CoreServices framework. To avoid a compile dependency on the framework
//! bundle, we dynamically load CoreServices with `std.DynLib`.
//!
//! While the logic in this file *is* specialized to `std.Build.Watch`, efforts have been
//! made to keep that specialization to a minimum. Other use cases could be served with
//! relatively minimal modifications to the `watch_paths` field and its usages (in
//! particular the `setPaths` function). We avoid using the global GCD dispatch queue in
//! favour of creating our own and synchronizing with an explicit semaphore, meaning this
//! logic is thread-safe and does not affect process-global state.
//!
//! In theory, this API is quite good at avoiding filesystem race conditions. In practice,
//! the logic that would avoid them is currently disabled, because the build system kind
//! of relies on them at the time of writing to avoid redundant work -- see the comment at
//! the top of `wait` for details.

const enable_debug_logs = false;

core_services: std.DynLib,
resolved_symbols: ResolvedSymbols,

paths_arena: std.heap.ArenaAllocator.State,
/// The roots of the recursive watches. FSEvents has relatively small limits on the number
/// of watched paths, so this slice must not be too long. The paths themselves are allocated
/// into `paths_arena`, but this slice is allocated into the GPA.
watch_roots: [][:0]const u8,
/// All of the paths being watched. Value is the set of steps which depend on the file/directory.
/// Keys and values are in `paths_arena`, but this map is allocated into the GPA.
watch_paths: std.StringArrayHashMapUnmanaged([]const *std.Build.Step),

/// The semaphore we use to block the thread calling `wait` until the callback determines a relevant
/// event has occurred. This is retained across `wait` calls for simplicity and efficiency.
waiting_semaphore: dispatch.semaphore_t,
/// This dispatch queue is created by us and executes serially. It exists exclusively to trigger the
/// callbacks of the FSEventStream we create. This is not in use outside of `wait`, but is retained
/// across `wait` calls for simplicity and efficiency.
dispatch_queue: dispatch.queue_t,
/// In theory, this field avoids race conditions. In practice, it is essentially unused at the time
/// of writing. See the comment at the start of `wait` for details.
since_event: FSEventStreamEventId,

cwd_path: []const u8,

/// All of the symbols we pull from the `dlopen`ed CoreServices framework. If any of these symbols
/// is not present, `init` will close the framework and return an error.
const ResolvedSymbols = struct {
    FSEventStreamCreate: *const fn (
        allocator: CFAllocatorRef,
        callback: FSEventStreamCallback,
        ctx: ?*const FSEventStreamContext,
        paths_to_watch: CFArrayRef,
        since_when: FSEventStreamEventId,
        latency: CFTimeInterval,
        flags: FSEventStreamCreateFlags,
    ) callconv(.c) FSEventStreamRef,
    FSEventStreamSetDispatchQueue: *const fn (stream: FSEventStreamRef, queue: dispatch.queue_t) callconv(.c) void,
    FSEventStreamStart: *const fn (stream: FSEventStreamRef) callconv(.c) bool,
    FSEventStreamStop: *const fn (stream: FSEventStreamRef) callconv(.c) void,
    FSEventStreamInvalidate: *const fn (stream: FSEventStreamRef) callconv(.c) void,
    FSEventStreamRelease: *const fn (stream: FSEventStreamRef) callconv(.c) void,
    FSEventStreamGetLatestEventId: *const fn (stream: ConstFSEventStreamRef) callconv(.c) FSEventStreamEventId,
    FSEventsGetCurrentEventId: *const fn () callconv(.c) FSEventStreamEventId,
    CFRelease: *const fn (cf: *const anyopaque) callconv(.c) void,
    CFArrayCreate: *const fn (
        allocator: CFAllocatorRef,
        values: [*]const usize,
        num_values: CFIndex,
        call_backs: ?*const CFArrayCallBacks,
    ) callconv(.c) CFArrayRef,
    CFStringCreateWithCString: *const fn (
        alloc: CFAllocatorRef,
        c_str: [*:0]const u8,
        encoding: CFStringEncoding,
    ) callconv(.c) CFStringRef,
    CFAllocatorCreate: *const fn (allocator: CFAllocatorRef, context: *const CFAllocatorContext) callconv(.c) CFAllocatorRef,
    kCFAllocatorUseContext: *const CFAllocatorRef,
};

pub fn init(cwd_path: []const u8) error{ OpenFrameworkFailed, MissingCoreServicesSymbol, SystemResources }!FsEvents {
    var core_services = std.DynLib.open("/System/Library/Frameworks/CoreServices.framework/CoreServices") catch
        return error.OpenFrameworkFailed;
    errdefer core_services.close();

    var resolved_symbols: ResolvedSymbols = undefined;
    inline for (@typeInfo(ResolvedSymbols).@"struct".fields) |f| {
        @field(resolved_symbols, f.name) = core_services.lookup(f.type, f.name) orelse return error.MissingCoreServicesSymbol;
    }

    return .{
        .core_services = core_services,
        .resolved_symbols = resolved_symbols,
        .paths_arena = .{},
        .watch_roots = &.{},
        .watch_paths = .empty,
        .waiting_semaphore = dispatch.semaphore_create(0) orelse return error.SystemResources,
        .dispatch_queue = dispatch.queue_create("zig-watch", .SERIAL()) orelse return error.SystemResources,
        // Not `.since_now`, because this means we can init `FsEvents` *before* we do work in order
        // to notice any changes which happened during said work.
        .since_event = resolved_symbols.FSEventsGetCurrentEventId(),
        .cwd_path = cwd_path,
    };
}

pub fn deinit(fse: *FsEvents, gpa: Allocator, io: Io) void {
    fse.waiting_semaphore.as_object().release();
    fse.dispatch_queue.as_object().release();
    fse.core_services.close(io);

    gpa.free(fse.watch_roots);
    fse.watch_paths.deinit(gpa);
    {
        var paths_arena = fse.paths_arena.promote(gpa);
        paths_arena.deinit();
    }
}

pub fn setPaths(fse: *FsEvents, gpa: Allocator, steps: []const *std.Build.Step) !void {
    var paths_arena_instance = fse.paths_arena.promote(gpa);
    defer fse.paths_arena = paths_arena_instance.state;
    const paths_arena = paths_arena_instance.allocator();

    var need_dirs: std.StringArrayHashMapUnmanaged(void) = .empty;
    defer need_dirs.deinit(gpa);

    fse.watch_paths.clearRetainingCapacity();

    // We take `step` by pointer for a slight memory optimization in a moment.
    for (steps) |*step| {
        for (step.*.inputs.table.keys(), step.*.inputs.table.values()) |path, *files| {
            const resolved_dir = try std.fs.path.resolvePosix(paths_arena, &.{
                fse.cwd_path, path.root_dir.path orelse ".", path.sub_path,
            });
            try need_dirs.put(gpa, resolved_dir, {});
            for (files.items) |file_name| {
                const watch_path = if (std.mem.eql(u8, file_name, "."))
                    resolved_dir
                else
                    try std.fs.path.join(paths_arena, &.{ resolved_dir, file_name });
                const gop = try fse.watch_paths.getOrPut(gpa, watch_path);
                if (gop.found_existing) {
                    const old_steps = gop.value_ptr.*;
                    const new_steps = try paths_arena.alloc(*std.Build.Step, old_steps.len + 1);
                    @memcpy(new_steps[0..old_steps.len], old_steps);
                    new_steps[old_steps.len] = step.*;
                    gop.value_ptr.* = new_steps;
                } else {
                    // This is why we captured `step` by pointer! We can avoid allocating a slice of one
                    // step in the arena in the common case where a file is referenced by only one step.
                    gop.value_ptr.* = step[0..1];
                }
            }
        }
    }

    {
        // There's no point looking at directories inside other ones (e.g. "/foo" and "/foo/bar").
        // To eliminate these, we'll re-add directories in order of path length with a redundancy check.
        const old_dirs = try gpa.dupe([]const u8, need_dirs.keys());
        defer gpa.free(old_dirs);
        std.mem.sort([]const u8, old_dirs, {}, struct {
            fn lessThan(ctx: void, a: []const u8, b: []const u8) bool {
                ctx;
                return std.mem.lessThan(u8, a, b);
            }
        }.lessThan);
        need_dirs.clearRetainingCapacity();
        for (old_dirs) |dir_path| {
            var it: std.fs.path.ComponentIterator(.posix, u8) = .init(dir_path);
            while (it.next()) |component| {
                if (need_dirs.contains(component.path)) {
                    // this path is '/foo/bar/qux', but '/foo' or '/foo/bar' was already added
                    break;
                }
            } else {
                need_dirs.putAssumeCapacityNoClobber(dir_path, {});
            }
        }
    }

    // `need_dirs` is now a set of directories to watch with no redundancy. In practice, this is very
    // likely to have reduced it to a quite small set (e.g. it'll typically coalesce a full `src/`
    // directory into one entry). However, the FSEventStream API has a fairly low undocumented limit
    // on total watches (supposedly 4096), so we should handle the case where we exceed it. To be
    // safe, because this API can be a little unpredictable, we'll cap ourselves a little *below*
    // that known limit.
    if (need_dirs.count() > 2048) {
        // Fallback: watch the whole filesystem. This is excessive, but... it *works* :P
        if (enable_debug_logs) watch_log.debug("too many dirs; recursively watching root", .{});
        fse.watch_roots = try gpa.realloc(fse.watch_roots, 1);
        fse.watch_roots[0] = "/";
    } else {
        fse.watch_roots = try gpa.realloc(fse.watch_roots, need_dirs.count());
        for (fse.watch_roots, need_dirs.keys()) |*out, in| {
            out.* = try paths_arena.dupeZ(u8, in);
        }
    }
    if (enable_debug_logs) {
        watch_log.debug("watching {d} paths using {d} recursive watches:", .{ fse.watch_paths.count(), fse.watch_roots.len });
        for (fse.watch_roots) |dir_path| {
            watch_log.debug("- '{s}'", .{dir_path});
        }
    }
}

pub fn wait(fse: *FsEvents, gpa: Allocator, timeout_ns: ?u64) error{ OutOfMemory, StartFailed }!std.Build.Watch.WaitResult {
    if (fse.watch_roots.len == 0) @panic("nothing to watch");

    const rs = fse.resolved_symbols;

    // At the time of writing, using `since_event` in the obvious way causes redundant rebuilds
    // to occur, because one step modifies a file which is an input to another step. The solution
    // to this problem will probably be either:
    //
    // a) Don't include the output of one step as a watch input of another; only mark external
    //    files as watch inputs. Or...
    //
    // b) Note the current event ID when a step begins, and disregard events preceding that ID
    //    when considering whether to dirty that step in `eventCallback`.
    //
    // For now, to avoid the redundant rebuilds, we bypass this `since_event` mechanism. This does
    // introduce race conditions, but the other `std.Build.Watch` implementations suffer from those
    // too at the time of writing, so this is kind of expected.
    fse.since_event = .since_now;

    const cf_allocator = rs.CFAllocatorCreate(rs.kCFAllocatorUseContext.*, &.{
        .version = 0,
        .info = @constCast(&gpa),
        .retain = null,
        .release = null,
        .copy_description = null,
        .allocate = &cf_alloc_callbacks.allocate,
        .reallocate = &cf_alloc_callbacks.reallocate,
        .deallocate = &cf_alloc_callbacks.deallocate,
        .preferred_size = null,
    }) orelse return error.OutOfMemory;
    defer rs.CFRelease(cf_allocator);

    const cf_paths = try gpa.alloc(?CFStringRef, fse.watch_roots.len);
    @memset(cf_paths, null);
    defer {
        for (cf_paths) |o| if (o) |p| rs.CFRelease(p);
        gpa.free(cf_paths);
    }
    for (fse.watch_roots, cf_paths) |raw_path, *cf_path| {
        cf_path.* = rs.CFStringCreateWithCString(cf_allocator, raw_path, .utf8);
    }
    const cf_paths_array = rs.CFArrayCreate(cf_allocator, @ptrCast(cf_paths), @intCast(cf_paths.len), null);
    defer rs.CFRelease(cf_paths_array);

    const callback_ctx: EventCallbackCtx = .{
        .fse = fse,
        .gpa = gpa,
    };
    const event_stream = rs.FSEventStreamCreate(
        null,
        &eventCallback,
        &.{
            .version = 0,
            .info = @constCast(&callback_ctx),
            .retain = null,
            .release = null,
            .copy_description = null,
        },
        cf_paths_array,
        fse.since_event,
        0.05, // 0.05s latency; higher values increase efficiency by coalescing more events
        .{ .watch_root = true, .file_events = true },
    );
    defer rs.FSEventStreamRelease(event_stream);
    rs.FSEventStreamSetDispatchQueue(event_stream, fse.dispatch_queue);
    defer rs.FSEventStreamInvalidate(event_stream);
    if (!rs.FSEventStreamStart(event_stream)) return error.StartFailed;
    defer rs.FSEventStreamStop(event_stream);
    const result = fse.waiting_semaphore.wait(timeout: {
        const ns = timeout_ns orelse break :timeout .FOREVER;
        break :timeout .time(.NOW, @intCast(ns));
    });
    return switch (result) {
        0 => .dirty,
        else => .timeout,
    };
}

const cf_alloc_callbacks = struct {
    const log = std.log.scoped(.cf_alloc);
    fn allocate(size: CFIndex, hint: CFOptionFlags, info: ?*const anyopaque) callconv(.c) ?*const anyopaque {
        if (enable_debug_logs) log.debug("allocate {d}", .{size});
        _ = hint;
        const gpa: *const Allocator = @ptrCast(@alignCast(info));
        const mem = gpa.alignedAlloc(u8, .of(usize), @intCast(size + @sizeOf(usize))) catch return null;
        const metadata: *usize = @ptrCast(mem);
        metadata.* = @intCast(size);
        return mem[@sizeOf(usize)..].ptr;
    }
    fn reallocate(ptr: ?*anyopaque, new_size: CFIndex, hint: CFOptionFlags, info: ?*const anyopaque) callconv(.c) ?*const anyopaque {
        if (enable_debug_logs) log.debug("reallocate @{*} {d}", .{ ptr, new_size });
        _ = hint;
        if (ptr == null or new_size == 0) return null; // not a bug: documentation explicitly states that realloc on NULL should return NULL
        const gpa: *const Allocator = @ptrCast(@alignCast(info));
        const old_base: [*]align(@alignOf(usize)) u8 = @alignCast(@as([*]u8, @ptrCast(ptr)) - @sizeOf(usize));
        const old_size = @as(*const usize, @ptrCast(old_base)).*;
        const old_mem = old_base[0 .. old_size + @sizeOf(usize)];
        const new_mem = gpa.realloc(old_mem, @intCast(new_size + @sizeOf(usize))) catch return null;
        const metadata: *usize = @ptrCast(new_mem);
        metadata.* = @intCast(new_size);
        return new_mem[@sizeOf(usize)..].ptr;
    }
    fn deallocate(ptr: *anyopaque, info: ?*const anyopaque) callconv(.c) void {
        if (enable_debug_logs) log.debug("deallocate @{*}", .{ptr});
        const gpa: *const Allocator = @ptrCast(@alignCast(info));
        const old_base: [*]align(@alignOf(usize)) u8 = @alignCast(@as([*]u8, @ptrCast(ptr)) - @sizeOf(usize));
        const old_size = @as(*const usize, @ptrCast(old_base)).*;
        const old_mem = old_base[0 .. old_size + @sizeOf(usize)];
        gpa.free(old_mem);
    }
};

const EventCallbackCtx = struct {
    fse: *FsEvents,
    gpa: Allocator,
};

fn eventCallback(
    stream: ConstFSEventStreamRef,
    client_callback_info: ?*anyopaque,
    num_events: usize,
    events_paths_ptr: *anyopaque,
    events_flags_ptr: [*]const FSEventStreamEventFlags,
    events_ids_ptr: [*]const FSEventStreamEventId,
) callconv(.c) void {
    const ctx: *const EventCallbackCtx = @ptrCast(@alignCast(client_callback_info));
    const fse = ctx.fse;
    const gpa = ctx.gpa;
    const rs = fse.resolved_symbols;
    const events_paths_ptr_casted: [*]const [*:0]const u8 = @ptrCast(@alignCast(events_paths_ptr));
    const events_paths = events_paths_ptr_casted[0..num_events];
    const events_ids = events_ids_ptr[0..num_events];
    const events_flags = events_flags_ptr[0..num_events];
    var any_dirty = false;
    for (events_paths, events_ids, events_flags) |event_path_nts, event_id, event_flags| {
        _ = event_id;
        if (event_flags.history_done) continue; // sentinel
        const event_path = std.mem.span(event_path_nts);
        switch (event_flags.must_scan_sub_dirs) {
            false => {
                if (fse.watch_paths.get(event_path)) |steps| {
                    assert(steps.len > 0);
                    for (steps) |s| {
                        if (s.invalidateResult(gpa)) any_dirty = true;
                    }
                }
                if (std.fs.path.dirname(event_path)) |event_dirname| {
                    // Modifying '/foo/bar' triggers the watch on '/foo'.
                    if (fse.watch_paths.get(event_dirname)) |steps| {
                        assert(steps.len > 0);
                        for (steps) |s| {
                            if (s.invalidateResult(gpa)) any_dirty = true;
                        }
                    }
                }
            },
            true => {
                // This is unlikely, but can occasionally happen when bottlenecked: events have been
                // coalesced into one. We want to see if any of these events are actually relevant
                // to us. The only way we can reasonably do that in this rare edge case is iterate
                // the watch paths and see if any is under this directory. That's acceptable because
                // we would otherwise kick off a rebuild which would be clearing those paths anyway.
                const changed_path = std.fs.path.dirname(event_path) orelse event_path;
                for (fse.watch_paths.keys(), fse.watch_paths.values()) |watching_path, steps| {
                    if (dirStartsWith(watching_path, changed_path)) {
                        for (steps) |s| {
                            if (s.invalidateResult(gpa)) any_dirty = true;
                        }
                    }
                }
            },
        }
    }
    if (any_dirty) {
        fse.since_event = rs.FSEventStreamGetLatestEventId(stream);
        _ = fse.waiting_semaphore.signal();
    }
}
fn dirStartsWith(path: []const u8, prefix: []const u8) bool {
    if (std.mem.eql(u8, path, prefix)) return true;
    if (!std.mem.startsWith(u8, path, prefix)) return false;
    if (path[prefix.len] != '/') return false; // `path` is `/foo/barx`, `prefix` is `/foo/bar`
    return true; // `path` is `/foo/bar/...`, `prefix` is `/foo/bar`
}

const CFAllocatorRef = ?*const opaque {};
const CFArrayRef = *const opaque {};
const CFStringRef = *const opaque {};
const CFTimeInterval = f64;
const CFIndex = i32;
const CFOptionFlags = enum(u32) { _ };
const CFAllocatorRetainCallBack = *const fn (info: ?*const anyopaque) callconv(.c) *const anyopaque;
const CFAllocatorReleaseCallBack = *const fn (info: ?*const anyopaque) callconv(.c) void;
const CFAllocatorCopyDescriptionCallBack = *const fn (info: ?*const anyopaque) callconv(.c) CFStringRef;
const CFAllocatorAllocateCallBack = *const fn (alloc_size: CFIndex, hint: CFOptionFlags, info: ?*const anyopaque) callconv(.c) ?*const anyopaque;
const CFAllocatorReallocateCallBack = *const fn (ptr: ?*anyopaque, new_size: CFIndex, hint: CFOptionFlags, info: ?*const anyopaque) callconv(.c) ?*const anyopaque;
const CFAllocatorDeallocateCallBack = *const fn (ptr: *anyopaque, info: ?*const anyopaque) callconv(.c) void;
const CFAllocatorPreferredSizeCallBack = *const fn (size: CFIndex, hint: CFOptionFlags, info: ?*const anyopaque) callconv(.c) CFIndex;
const CFAllocatorContext = extern struct {
    version: CFIndex,
    info: ?*anyopaque,
    retain: ?CFAllocatorRetainCallBack,
    release: ?CFAllocatorReleaseCallBack,
    copy_description: ?CFAllocatorCopyDescriptionCallBack,
    allocate: CFAllocatorAllocateCallBack,
    reallocate: ?CFAllocatorReallocateCallBack,
    deallocate: ?CFAllocatorDeallocateCallBack,
    preferred_size: ?CFAllocatorPreferredSizeCallBack,
};
const CFArrayCallBacks = opaque {};
const CFStringEncoding = enum(u32) {
    invalid_id = std.math.maxInt(u32),
    mac_roman = 0,
    windows_latin_1 = 0x500,
    iso_latin_1 = 0x201,
    next_step_latin = 0xB01,
    ascii = 0x600,
    unicode = 0x100,
    utf8 = 0x8000100,
    non_lossy_ascii = 0xBFF,
};

const FSEventStreamRef = *opaque {};
const ConstFSEventStreamRef = *const @typeInfo(FSEventStreamRef).pointer.child;
const FSEventStreamCallback = *const fn (
    stream: ConstFSEventStreamRef,
    client_callback_info: ?*anyopaque,
    num_events: usize,
    event_paths: *anyopaque,
    event_flags: [*]const FSEventStreamEventFlags,
    event_ids: [*]const FSEventStreamEventId,
) callconv(.c) void;
const FSEventStreamContext = extern struct {
    version: CFIndex,
    info: ?*anyopaque,
    retain: ?CFAllocatorRetainCallBack,
    release: ?CFAllocatorReleaseCallBack,
    copy_description: ?CFAllocatorCopyDescriptionCallBack,
};
const FSEventStreamEventId = enum(u64) {
    since_now = std.math.maxInt(u64),
    _,
};
const FSEventStreamCreateFlags = packed struct(u32) {
    use_cf_types: bool = false,
    no_defer: bool = false,
    watch_root: bool = false,
    ignore_self: bool = false,
    file_events: bool = false,
    _: u27 = 0,
};
const FSEventStreamEventFlags = packed struct(u32) {
    must_scan_sub_dirs: bool,
    user_dropped: bool,
    kernel_dropped: bool,
    event_ids_wrapped: bool,
    history_done: bool,
    root_changed: bool,
    mount: bool,
    unmount: bool,
    _: u24 = 0,
};

const dispatch = std.c.dispatch;
const std = @import("std");
const Io = std.Io;
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;
const watch_log = std.log.scoped(.watch);
const FsEvents = @This();



---
File: /std/Build/abi.zig
---

//! This file is shared among Zig code running in wildly different contexts:
//! * The build runner, running on the host computer
//! * The build system web interface Wasm code, running in the browser
//! * `libfuzzer`, compiled alongside unit tests
//!
//! All of these components interface to some degree via an ABI:
//! * The build runner communicates with the web interface over a WebSocket connection
//! * The build runner communicates with `libfuzzer` over a shared memory-mapped file
const std = @import("std");

// Check that no WebSocket message type has implicit padding bits. This ensures we never send any
// undefined bits over the wire, and also helps validate that the layout doesn't differ between, for
// instance, the web server in `std.Build` and the Wasm client.
comptime {
    const check = struct {
        fn check(comptime T: type) void {
            std.debug.assert(@typeInfo(T) == .@"struct");
            std.debug.assert(@typeInfo(T).@"struct".layout == .@"extern");
            std.debug.assert(std.meta.hasUniqueRepresentation(T));
        }
    }.check;

    // server->client
    check(Hello);
    check(StatusUpdate);
    check(StepUpdate);
    check(fuzz.SourceIndexHeader);
    check(fuzz.CoverageUpdateHeader);
    check(fuzz.EntryPointHeader);
    check(time_report.GenericResult);
    check(time_report.CompileResult);

    // client->server
    check(Rebuild);
}

/// All WebSocket messages sent by the server to the client begin with a `ToClientTag` byte. This
/// enum is non-exhaustive only to avoid Illegal Behavior when malformed messages are sent over the
/// socket; unnamed tags are an error condition and should terminate the connection.
///
/// Every tag has a curresponding `extern struct` representing the full message (or a header of the
/// message if it is variable-length). For instance, `.hello` corresponds to `Hello`.
///
/// When introducing a tag, make sure to add a corresponding `extern struct` whose first field is
/// this enum, and `check` its layout in the `comptime` block above.
pub const ToClientTag = enum(u8) {
    hello,
    status_update,
    step_update,

    // `--fuzz`
    fuzz_source_index,
    fuzz_coverage_update,
    fuzz_entry_points,

    // `--time-report`
    time_report_generic_result,
    time_report_compile_result,
    time_report_run_test_result,

    _,
};

/// Like `ToClientTag`, but for messages sent by the client to the server.
pub const ToServerTag = enum(u8) {
    rebuild,

    _,
};

/// The current overall status of the build runner.
/// Keep in sync with indices in web UI `main.js:updateBuildStatus`.
pub const BuildStatus = enum(u8) {
    idle,
    watching,
    running,
    fuzz_init,
};

/// WebSocket server->client.
///
/// Sent by the server as the first message after a WebSocket connection opens to provide basic
/// information about the server, the build graph, etc.
///
/// Trailing:
/// * `step_name_len: u32` for each `steps_len`
/// * `step_name: [step_name_len]u8` for each `step_name_len`
/// * `step_status: u8` for every 4 `steps_len`; every 2 bits is a `StepUpdate.Status`, LSBs first
pub const Hello = extern struct {
    tag: ToClientTag = .hello,

    status: BuildStatus,
    flags: Flags,

    /// Any message containing a timestamp represents it as a number of nanoseconds relative to when
    /// the build began. This field is the current timestamp, represented in that form.
    timestamp: i64 align(4),

    /// The number of steps in the build graph which are reachable from the top-level step[s] being
    /// run; in other words, the number of steps which will be executed by this build. The name of
    /// each step trails this message.
    steps_len: u32 align(1),

    pub const Flags = packed struct(u16) {
        /// Whether time reporting is enabled.
        time_report: bool,
        _: u15 = 0,
    };
};
/// WebSocket server->client.
///
/// Indicates that the build status has changed.
pub const StatusUpdate = extern struct {
    tag: ToClientTag = .status_update,
    new: BuildStatus,
};
/// WebSocket server->client.
///
/// Indicates a change in a step's status.
pub const StepUpdate = extern struct {
    tag: ToClientTag = .step_update,
    step_idx: u32 align(1),
    bits: packed struct(u8) {
        status: Status,
        _: u6 = 0,
    },
    /// Keep in sync with indices in web UI `main.js:updateStepStatus`.
    pub const Status = enum(u2) {
        pending,
        wip,
        success,
        failure,
    };
};

pub const Rebuild = extern struct {
    tag: ToServerTag = .rebuild,
};

/// ABI bits specifically relating to the fuzzer interface.
pub const fuzz = struct {
    /// Returns if `error.SkipZigTest` was indicated
    pub const TestOne = *const fn () callconv(.c) bool;

    /// A unique value to identify the related requests across runs
    pub const Uid = packed struct(u32) {
        kind: enum(u1) { int, bytes },
        hash: u31,

        pub const hashmap_ctx = struct {
            pub fn hash(_: @This(), u: Uid) u32 {
                // We can ignore `kind` since `hash` should be unique regardless
                return u.hash;
            }

            pub fn eql(_: @This(), a: Uid, b: Uid, _: usize) bool {
                return a == b;
            }
        };
    };

    pub extern fn fuzzer_init(cache_dir_path: Slice) void;
    /// `fuzzer_init` must be called first.
    pub extern fn fuzzer_coverage() Coverage;
    pub extern fn fuzzer_unslide_address(addr: usize) usize;

    /// Performs all the fuzzing work and selects tests to run
    ///
    /// `fuzzer_init` must be called first.
    pub extern fn fuzzer_main(
        n_tests: u32,
        seed: u32,
        limit_kind: LimitKind,
        amount_or_instance: u64,
    ) void;
    pub extern fn runner_test_run(i: u32) void;
    pub extern fn runner_test_name(i: u32) Slice;
    // Since the runner owns the `std.zig.Server` instance, it also controls the
    // concurrent Io instance so reads can be canceled. As such, the fuzzer has
    // to call into the runner for any zig server / concurrent operation.
    pub extern fn runner_start_input_poller() void;
    pub extern fn runner_stop_input_poller() void;
    /// Returns if cancelation has been indicated.
    pub extern fn runner_futex_wait(*const u32, expected: u32) bool;
    pub extern fn runner_futex_wake(*const u32, waiters: u32) void;
    pub extern fn runner_broadcast_input(test_i: u32, bytes: Slice) void;
    /// `fuzzer_main` must be called first.
    ///
    /// Called concurrently with `fuzzer_main`. Returns if cancelation has been indicated.
    pub extern fn fuzzer_receive_input(test_i: u32, bytes: Slice) bool;

    /// Must be called from inside a test function
    pub extern fn fuzzer_set_test(test_one: TestOne) void;
    /// Must be called from inside a test function where `fuzzer_set_test` has been called first.
    pub extern fn fuzzer_new_input(bytes: Slice) void;
    /// Must be called from inside a test function where `fuzzer_set_test` has been called first.
    pub extern fn fuzzer_start_test() void;

    pub extern fn fuzzer_int(uid: Uid, weights: Weights) u64;
    pub extern fn fuzzer_eos(uid: Uid, weights: Weights) bool;
    pub extern fn fuzzer_bytes(uid: Uid, out: MutSlice, weights: Weights) void;
    pub extern fn fuzzer_slice(
        uid: Uid,
        buf: MutSlice,
        len_weights: Weights,
        byte_weights: Weights,
    ) u32;

    pub const Slice = extern struct {
        ptr: [*]const u8,
        len: usize,

        pub fn toSlice(s: Slice) []const u8 {
            return s.ptr[0..s.len];
        }

        pub fn fromSlice(s: []const u8) Slice {
            return .{ .ptr = s.ptr, .len = s.len };
        }
    };

    pub const MutSlice = extern struct {
        ptr: [*]u8,
        len: usize,

        pub fn toSlice(s: MutSlice) []u8 {
            return s.ptr[0..s.len];
        }

        pub fn fromSlice(s: []u8) MutSlice {
            return .{ .ptr = s.ptr, .len = s.len };
        }
    };

    pub const Weights = extern struct {
        ptr: [*]const Weight,
        len: usize,

        pub fn toSlice(s: Weights) []const Weight {
            return s.ptr[0..s.len];
        }

        pub fn fromSlice(s: []const Weight) Weights {
            return .{ .ptr = s.ptr, .len = s.len };
        }
    };

    /// Increases the probability of values being selected by the fuzzer.
    ///
    /// `weight` applies to each value in the range (i.e. not evenly across
    /// the range) and must be nonzero.
    ///
    /// In a set of weights, the total weight must not exceed 2^64 and be
    /// nonzero.
    pub const Weight = extern struct {
        /// Inclusive
        min: u64,
        /// Inclusive
        max: u64,
        weight: u64,

        /// `inline` to propogate comptimeness
        inline fn intFromValue(x: anytype) u64 {
            const T = @TypeOf(x);
            return switch (@typeInfo(T)) {
                .comptime_int => x,
                .bool => @intFromBool(x),
                .@"enum" => @intFromEnum(x),
                else => @as(std.meta.Int(.unsigned, @bitSizeOf(T)), @bitCast(x)),

                .int => |i| x: {
                    comptime {
                        if (i.signedness == .signed) {
                            @compileError("type does not have a continous range: " ++ @typeName(T));
                        }
                        // Reject types that don't have a fixed bitsize (esp. usize)
                        // since they are not gauraunteed to fit in a u64 across targets.
                        //
                        // std.mem.indexOfScalar is not used to avoid backward branches
                        // and preserve the eval branch quota.
                        if (T == usize or T == c_char or T == c_ushort or
                            T == c_uint or T == c_ulong or T == c_ulonglong)
                        {
                            @compileError("type does not have a fixed bitsize: " ++ @typeName(T));
                        }
                    }
                    break :x x;
                },

                .comptime_float,
                .float,
                => @compileError("type does not have a continous range: " ++ @typeName(T)),
                .pointer => @compileError("type does not have a fixed bitsize: " ++ @typeName(T)),
            };
        }

        /// `inline` to propogate comptimeness
        pub inline fn value(T: type, x: T, weight: u64) Weight {
            return .{ .min = intFromValue(x), .max = intFromValue(x), .weight = weight };
        }

        /// `inline` to propogate comptimeness
        pub inline fn rangeAtMost(T: type, at_least: T, at_most: T, weight: u64) Weight {
            std.debug.assert(intFromValue(at_least) <= intFromValue(at_most));
            return .{
                .min = intFromValue(at_least),
                .max = intFromValue(at_most),
                .weight = weight,
            };
        }

        /// `inline` to propogate comptimeness
        pub inline fn rangeLessThan(T: type, at_least: T, less_than: T, weight: u64) Weight {
            std.debug.assert(intFromValue(at_least) < intFromValue(less_than));
            return .{
                .min = intFromValue(at_least),
                .max = intFromValue(less_than) - 1,
                .weight = weight,
            };
        }
    };

    pub const LimitKind = enum(u8) { forever, iterations };

    /// libfuzzer uses this and its usize is the one that counts. To match the ABI,
    /// make the ints be the size of the target used with libfuzzer.
    ///
    /// Trailing:
    /// * 1 bit per pc_addr, usize elements
    /// * pc_addr: usize for each pcs_len
    pub const SeenPcsHeader = extern struct {
        n_runs: usize,
        unique_runs: usize,
        pcs_len: usize,

        /// Used for comptime assertions. Provides a mechanism for strategically
        /// causing compile errors.
        pub const trailing = .{
            .pc_bits_usize,
            .pc_addr,
        };

        pub fn headerEnd(header: *const SeenPcsHeader) []const usize {
            const ptr: [*]align(@alignOf(usize)) const u8 = @ptrCast(header);
            const header_end_ptr: [*]const usize = @ptrCast(ptr + @sizeOf(SeenPcsHeader));
            const pcs_len = header.pcs_len;
            return header_end_ptr[0 .. pcs_len + seenElemsLen(pcs_len)];
        }

        pub fn seenBits(header: *const SeenPcsHeader) []const usize {
            return header.headerEnd()[0..seenElemsLen(header.pcs_len)];
        }

        pub fn seenElemsLen(pcs_len: usize) usize {
            return (pcs_len + @bitSizeOf(usize) - 1) / @bitSizeOf(usize);
        }

        pub fn pcAddrs(header: *const SeenPcsHeader) []const usize {
            const pcs_len = header.pcs_len;
            return header.headerEnd()[seenElemsLen(pcs_len)..][0..pcs_len];
        }
    };

    /// Fields are little-endian
    pub const MmapInputHeader = extern struct {
        pc_digest: u64 align(4), // aligned so header does not have padding
        instance_id: u32,
        test_i: u32,
        len: u32,
    };

    /// WebSocket server->client.
    ///
    /// Sent once, when fuzzing starts, to indicate the available coverage data.
    ///
    /// Trailing:
    /// * std.debug.Coverage.String for each directories_len
    /// * std.debug.Coverage.File for each files_len
    /// * std.debug.Coverage.SourceLocation for each source_locations_len
    /// * u8 for each string_bytes_len
    pub const SourceIndexHeader = extern struct {
        tag: ToClientTag = .fuzz_source_index,
        _: [3]u8 = @splat(0),
        directories_len: u32,
        files_len: u32,
        source_locations_len: u32,
        string_bytes_len: u32,
        /// When, according to the server, fuzzing started.
        start_timestamp: i64 align(4),
        start_n_runs: u64 align(4),
    };

    /// WebSocket server->client.
    ///
    /// Sent whenever the set of covered source locations is updated.
    ///
    /// Trailing:
    /// * one bit per source_locations_len, contained in u64 elements
    pub const CoverageUpdateHeader = extern struct {
        tag: ToClientTag = .fuzz_coverage_update,
        _: [7]u8 = @splat(0),
        n_runs: u64,
        unique_runs: u64,

        pub const trailing = .{
            .pc_bits_usize,
        };
    };

    /// WebSocket server->client.
    ///
    /// Sent whenever the set of entry points is updated.
    ///
    /// Trailing:
    /// * one u32 index of source_locations per locsLen()
    pub const EntryPointHeader = extern struct {
        tag: ToClientTag = .fuzz_entry_points,
        locs_len_raw: [3]u8,

        pub fn locsLen(hdr: EntryPointHeader) u24 {
            return @bitCast(hdr.locs_len_raw);
        }
        pub fn init(locs_len: u24) EntryPointHeader {
            return .{ .locs_len_raw = @bitCast(locs_len) };
        }
    };

    /// Sent by lib/fuzzer to test_runner to obtain information about the
    /// active memory mapped input file and cumulative stats about previous
    /// fuzzing runs.
    pub const Coverage = extern struct {
        id: u64,
        runs: u64,
        unique: u64,
        seen: u64,
    };
};

/// ABI bits specifically relating to the time report interface.
pub const time_report = struct {
    /// WebSocket server->client.
    ///
    /// Sent after a `Step` finishes, providing the time taken to execute the step.
    pub const GenericResult = extern struct {
        tag: ToClientTag = .time_report_generic_result,
        step_idx: u32 align(1),
        ns_total: u64 align(1),
    };

    /// WebSocket server->client.
    ///
    /// Sent after a `Step.Compile` finishes, providing the step's time report.
    ///
    /// Trailing:
    /// * `llvm_pass_timings: [llvm_pass_timings_len]u8` (ASCII-encoded)
    /// * for each `files_len`:
    ///   * `name` (null-terminated UTF-8 string)
    /// * for each `decls_len`:
    ///   * `name` (null-terminated UTF-8 string)
    ///   * `file: u32` (index of file this decl is in)
    ///   * `sema_ns: u64` (nanoseconds spent semantically analyzing this decl)
    ///   * `codegen_ns: u64` (nanoseconds spent semantically analyzing this decl)
    ///   * `link_ns: u64` (nanoseconds spent semantically analyzing this decl)
    pub const CompileResult = extern struct {
        tag: ToClientTag = .time_report_compile_result,

        step_idx: u32 align(1),

        flags: Flags,
        stats: Stats align(1),
        ns_total: u64 align(1),

        llvm_pass_timings_len: u32 align(1),
        files_len: u32 align(1),
        decls_len: u32 align(1),

        pub const Flags = packed struct(u8) {
            use_llvm: bool,
            _: u7 = 0,
        };

        pub const Stats = extern struct {
            n_reachable_files: u32,
            n_imported_files: u32,
            n_generic_instances: u32,
            n_inline_calls: u32,

            cpu_ns_parse: u64,
            cpu_ns_astgen: u64,
            cpu_ns_sema: u64,
            cpu_ns_codegen: u64,
            cpu_ns_link: u64,

            real_ns_files: u64,
            real_ns_decls: u64,
            real_ns_llvm_emit: u64,
            real_ns_link_flush: u64,

            pub const init: Stats = .{
                .n_reachable_files = 0,
                .n_imported_files = 0,
                .n_generic_instances = 0,
                .n_inline_calls = 0,
                .cpu_ns_parse = 0,
                .cpu_ns_astgen = 0,
                .cpu_ns_sema = 0,
                .cpu_ns_codegen = 0,
                .cpu_ns_link = 0,
                .real_ns_files = 0,
                .real_ns_decls = 0,
                .real_ns_llvm_emit = 0,
                .real_ns_link_flush = 0,
            };
        };
    };

    /// WebSocket server->client.
    ///
    /// Sent after a `Step.Run` for a Zig test executable finishes, providing the test's time report.
    ///
    /// Trailing:
    /// * for each `tests_len`:
    ///   * `test_ns: u64` (nanoseconds spent running this test)
    /// * for each `tests_len`:
    ///   * `name` (null-terminated UTF-8 string)
    pub const RunTestResult = extern struct {
        tag: ToClientTag = .time_report_run_test_result,
        step_idx: u32 align(1),
        tests_len: u32 align(1),
    };
};



---
File: /std/Build/Cache.zig
---

//! Manages `zig-cache` directories.
//! This is not a general-purpose cache. It is designed to be fast and simple,
//! not to withstand attacks using specially-crafted input.

const Cache = @This();
const builtin = @import("builtin");

const std = @import("std");
const Io = std.Io;
const crypto = std.crypto;
const assert = std.debug.assert;
const testing = std.testing;
const mem = std.mem;
const fmt = std.fmt;
const Allocator = std.mem.Allocator;
const log = std.log.scoped(.cache);

gpa: Allocator,
io: Io,
manifest_dir: Io.Dir,
hash: HashHelper = .{},
/// This value is accessed from multiple threads, protected by mutex.
recent_problematic_timestamp: Io.Timestamp = .zero,
mutex: Io.Mutex = .init,

/// A set of strings such as the zig library directory or project source root, which
/// are stripped from the file paths before putting into the cache. They
/// are replaced with single-character indicators. This is not to save
/// space but to eliminate absolute file paths. This improves portability
/// and usefulness of the cache for advanced use cases.
prefixes_buffer: [4]Directory = undefined,
prefixes_len: usize = 0,
/// Used to identify prefixes. References external memory.
cwd: []const u8,

pub const Path = @import("Cache/Path.zig");
pub const Directory = @import("Cache/Directory.zig");
pub const DepTokenizer = @import("Cache/DepTokenizer.zig");

pub fn addPrefix(cache: *Cache, directory: Directory) void {
    cache.prefixes_buffer[cache.prefixes_len] = directory;
    cache.prefixes_len += 1;
}

/// Be sure to call `Manifest.deinit` after successful initialization.
pub fn obtain(cache: *Cache) Manifest {
    return .{
        .cache = cache,
        .hash = cache.hash,
        .manifest_file = null,
        .manifest_dirty = false,
        .hex_digest = undefined,
    };
}

pub fn prefixes(cache: *const Cache) []const Directory {
    return cache.prefixes_buffer[0..cache.prefixes_len];
}

const PrefixedPath = struct {
    prefix: u8,
    sub_path: []const u8,

    fn eql(a: PrefixedPath, b: PrefixedPath) bool {
        return a.prefix == b.prefix and std.mem.eql(u8, a.sub_path, b.sub_path);
    }

    fn hash(pp: PrefixedPath) u32 {
        return @truncate(std.hash.Wyhash.hash(pp.prefix, pp.sub_path));
    }
};

fn findPrefix(cache: *const Cache, file_path: []const u8) !PrefixedPath {
    const gpa = cache.gpa;
    const resolved_path = try std.fs.path.resolve(gpa, &.{file_path});
    errdefer gpa.free(resolved_path);
    return findPrefixResolved(cache, resolved_path);
}

/// Takes ownership of `resolved_path` on success.
fn findPrefixResolved(cache: *const Cache, resolved_path: []u8) !PrefixedPath {
    const gpa = cache.gpa;
    const cwd = cache.cwd;
    const prefixes_slice = cache.prefixes();
    var i: u8 = 1; // Start at 1 to skip over checking the null prefix.
    while (i < prefixes_slice.len) : (i += 1) {
        const p = prefixes_slice[i].path.?;
        const sub_path = getPrefixSubpath(gpa, cwd, p, resolved_path) catch |err| switch (err) {
            error.NotASubPath => continue,
            else => |e| return e,
        };
        // Free the resolved path since we're not going to return it
        gpa.free(resolved_path);
        return PrefixedPath{
            .prefix = i,
            .sub_path = sub_path,
        };
    }

    return PrefixedPath{
        .prefix = 0,
        .sub_path = resolved_path,
    };
}

fn getPrefixSubpath(gpa: Allocator, cwd: []const u8, prefix: []const u8, path: []u8) ![]u8 {
    const relative = try std.fs.path.relative(gpa, cwd, null, prefix, path);
    errdefer gpa.free(relative);
    var component_iterator: std.fs.path.NativeComponentIterator = .init(relative);
    if (component_iterator.root() != null) {
        return error.NotASubPath;
    }
    const first_component = component_iterator.first();
    if (first_component != null and std.mem.eql(u8, first_component.?.name, "..")) {
        return error.NotASubPath;
    }
    return relative;
}

/// This is 128 bits - Even with 2^54 cache entries, the probably of a collision would be under 10^-6
pub const bin_digest_len = 16;
pub const hex_digest_len = bin_digest_len * 2;
pub const BinDigest = [bin_digest_len]u8;
pub const HexDigest = [hex_digest_len]u8;

/// This is currently just an arbitrary non-empty string that can't match another manifest line.
const manifest_header = "0";
pub const manifest_file_size_max = 100 * 1024 * 1024;

/// The type used for hashing file contents. Currently, this is SipHash128(1, 3), because it
/// provides enough collision resistance for the Manifest use cases, while being one of our
/// fastest options right now.
pub const Hasher = crypto.auth.siphash.SipHash128(1, 3);

/// Initial state with random bytes, that can be copied.
/// Refresh this with new random bytes when the manifest
/// format is modified in a non-backwards-compatible way.
pub const hasher_init: Hasher = Hasher.init(&.{
    0x33, 0x52, 0xa2, 0x84,
    0xcf, 0x17, 0x56, 0x57,
    0x01, 0xbb, 0xcd, 0xe4,
    0x77, 0xd6, 0xf0, 0x60,
});

pub const File = struct {
    prefixed_path: PrefixedPath,
    max_file_size: ?usize,
    /// Populated if the user calls `addOpenedFile`.
    /// The handle is not owned here.
    handle: ?Io.File,
    stat: Stat,
    bin_digest: BinDigest,
    contents: ?[]const u8,

    pub const Stat = struct {
        inode: Io.File.INode,
        size: u64,
        mtime: Io.Timestamp,

        pub fn fromFs(fs_stat: Io.File.Stat) Stat {
            return .{
                .inode = fs_stat.inode,
                .size = fs_stat.size,
                .mtime = fs_stat.mtime,
            };
        }
    };

    pub fn deinit(self: *File, gpa: Allocator) void {
        gpa.free(self.prefixed_path.sub_path);
        if (self.contents) |contents| {
            gpa.free(contents);
            self.contents = null;
        }
        self.* = undefined;
    }

    pub fn updateMaxSize(file: *File, new_max_size: ?usize) void {
        const new = new_max_size orelse return;
        file.max_file_size = if (file.max_file_size) |old| @max(old, new) else new;
    }

    pub fn updateHandle(file: *File, new_handle: ?Io.File) void {
        const handle = new_handle orelse return;
        file.handle = handle;
    }
};

pub const HashHelper = struct {
    hasher: Hasher = hasher_init,

    /// Record a slice of bytes as a dependency of the process being cached.
    pub fn addBytes(hh: *HashHelper, bytes: []const u8) void {
        hh.hasher.update(mem.asBytes(&bytes.len));
        hh.hasher.update(bytes);
    }

    pub fn addOptionalBytes(hh: *HashHelper, optional_bytes: ?[]const u8) void {
        hh.add(optional_bytes != null);
        hh.addBytes(optional_bytes orelse return);
    }

    pub fn addListOfBytes(hh: *HashHelper, list_of_bytes: []const []const u8) void {
        hh.add(list_of_bytes.len);
        for (list_of_bytes) |bytes| hh.addBytes(bytes);
    }

    pub fn addOptionalListOfBytes(hh: *HashHelper, optional_list_of_bytes: ?[]const []const u8) void {
        hh.add(optional_list_of_bytes != null);
        hh.addListOfBytes(optional_list_of_bytes orelse return);
    }

    /// Convert the input value into bytes and record it as a dependency of the process being cached.
    pub fn add(hh: *HashHelper, x: anytype) void {
        switch (@TypeOf(x)) {
            std.SemanticVersion => {
                hh.add(x.major);
                hh.add(x.minor);
                hh.add(x.patch);
            },
            std.Target.Os.TaggedVersionRange => {
                switch (x) {
                    .hurd => |hurd| {
                        hh.add(hurd.range.min);
                        hh.add(hurd.range.max);
                        hh.add(hurd.glibc);
                    },
                    .linux => |linux| {
                        hh.add(linux.range.min);
                        hh.add(linux.range.max);
                        hh.add(linux.glibc);
                        hh.add(linux.android);
                    },
                    .windows => |windows| {
                        hh.add(windows.min);
                        hh.add(windows.max);
                    },
                    .semver => |semver| {
                        hh.add(semver.min);
                        hh.add(semver.max);
                    },
                    .none => {},
                }
            },
            std.zig.BuildId => switch (x) {
                .none, .fast, .uuid, .sha1, .md5 => hh.add(std.meta.activeTag(x)),
                .hexstring => |hex_string| hh.addBytes(hex_string.toSlice()),
            },
            else => switch (@typeInfo(@TypeOf(x))) {
                .bool, .int, .@"enum", .array => hh.addBytes(mem.asBytes(&x)),
                else => @compileError("unable to hash type " ++ @typeName(@TypeOf(x))),
            },
        }
    }

    pub fn addOptional(hh: *HashHelper, optional: anytype) void {
        hh.add(optional != null);
        hh.add(optional orelse return);
    }

    /// Returns a hex encoded hash of the inputs, without modifying state.
    pub fn peek(hh: HashHelper) [hex_digest_len]u8 {
        var copy = hh;
        return copy.final();
    }

    pub fn peekBin(hh: HashHelper) BinDigest {
        var copy = hh;
        var bin_digest: BinDigest = undefined;
        copy.hasher.final(&bin_digest);
        return bin_digest;
    }

    /// Returns a hex encoded hash of the inputs, mutating the state of the hasher.
    pub fn final(hh: *HashHelper) HexDigest {
        var bin_digest: BinDigest = undefined;
        hh.hasher.final(&bin_digest);
        return binToHex(bin_digest);
    }

    pub fn oneShot(bytes: []const u8) [hex_digest_len]u8 {
        var hasher: Hasher = hasher_init;
        hasher.update(bytes);
        var bin_digest: BinDigest = undefined;
        hasher.final(&bin_digest);
        return binToHex(bin_digest);
    }
};

pub fn binToHex(bin_digest: BinDigest) HexDigest {
    var out_digest: HexDigest = undefined;
    var w: std.Io.Writer = .fixed(&out_digest);
    w.printHex(&bin_digest, .lower) catch unreachable;
    return out_digest;
}

pub const Lock = struct {
    manifest_file: Io.File,

    pub fn release(lock: *Lock, io: Io) void {
        if (builtin.os.tag == .windows) {
            // Windows does not guarantee that locks are immediately unlocked when
            // the file handle is closed. See LockFileEx documentation.
            lock.manifest_file.unlock(io);
        }

        lock.manifest_file.close(io);
        lock.* = undefined;
    }
};

pub const Manifest = struct {
    cache: *Cache,
    /// Current state for incremental hashing.
    hash: HashHelper,
    manifest_file: ?Io.File,
    manifest_dirty: bool,
    /// Set this flag to true before calling hit() in order to indicate that
    /// upon a cache hit, the code using the cache will not modify the files
    /// within the cache directory. This allows multiple processes to utilize
    /// the same cache directory at the same time.
    want_shared_lock: bool = true,
    have_exclusive_lock: bool = false,
    // Indicate that we want isProblematicTimestamp to perform a filesystem write in
    // order to obtain a problematic timestamp for the next call. Calls after that
    // will then use the same timestamp, to avoid unnecessary filesystem writes.
    want_refresh_timestamp: bool = true,
    files: Files = .{},
    hex_digest: HexDigest,
    diagnostic: Diagnostic = .none,
    /// Keeps track of the last time we performed a file system write to observe
    /// what time the file system thinks it is, according to its own granularity.
    recent_problematic_timestamp: Io.Timestamp = .zero,

    pub const Diagnostic = union(enum) {
        none,
        manifest_create: Io.File.OpenError,
        manifest_read: Io.File.Reader.Error,
        manifest_lock: Io.File.LockError,
        file_open: FileOp,
        file_stat: FileOp,
        file_read: FileOp,
        file_hash: FileOp,

        pub const FileOp = struct {
            file_index: usize,
            err: anyerror,
        };
    };

    pub const Files = std.ArrayHashMapUnmanaged(File, void, FilesContext, false);

    pub const FilesContext = struct {
        pub fn hash(fc: FilesContext, file: File) u32 {
            _ = fc;
            return file.prefixed_path.hash();
        }

        pub fn eql(fc: FilesContext, a: File, b: File, b_index: usize) bool {
            _ = fc;
            _ = b_index;
            return a.prefixed_path.eql(b.prefixed_path);
        }
    };

    const FilesAdapter = struct {
        pub fn eql(context: @This(), a: PrefixedPath, b: File, b_index: usize) bool {
            _ = context;
            _ = b_index;
            return a.eql(b.prefixed_path);
        }

        pub fn hash(context: @This(), key: PrefixedPath) u32 {
            _ = context;
            return key.hash();
        }
    };

    /// Add a file as a dependency of process being cached. When `hit` is
    /// called, the file's contents will be checked to ensure that it matches
    /// the contents from previous times.
    ///
    /// Max file size will be used to determine the amount of space the file contents
    /// are allowed to take up in memory. If max_file_size is null, then the contents
    /// will not be loaded into memory.
    ///
    /// Returns the index of the entry in the `files` array list. You can use it
    /// to access the contents of the file after calling `hit()` like so:
    ///
    /// ```
    /// var file_contents = cache_hash.files.keys()[file_index].contents.?;
    /// ```
    pub fn addFilePath(m: *Manifest, file_path: Path, max_file_size: ?usize) !usize {
        return addOpenedFile(m, file_path, null, max_file_size);
    }

    /// Same as `addFilePath` except the file has already been opened.
    pub fn addOpenedFile(m: *Manifest, path: Path, handle: ?Io.File, max_file_size: ?usize) !usize {
        const gpa = m.cache.gpa;
        try m.files.ensureUnusedCapacity(gpa, 1);
        const resolved_path = try std.fs.path.resolve(gpa, &.{
            path.root_dir.path orelse ".",
            path.subPathOrDot(),
        });
        errdefer gpa.free(resolved_path);
        const prefixed_path = try m.cache.findPrefixResolved(resolved_path);
        return addFileInner(m, prefixed_path, handle, max_file_size);
    }

    /// Deprecated; use `addFilePath`.
    pub fn addFile(self: *Manifest, file_path: []const u8, max_file_size: ?usize) !usize {
        assert(self.manifest_file == null);

        const gpa = self.cache.gpa;
        try self.files.ensureUnusedCapacity(gpa, 1);
        const prefixed_path = try self.cache.findPrefix(file_path);
        errdefer gpa.free(prefixed_path.sub_path);

        return addFileInner(self, prefixed_path, null, max_file_size);
    }

    fn addFileInner(self: *Manifest, prefixed_path: PrefixedPath, handle: ?Io.File, max_file_size: ?usize) usize {
        const gop = self.files.getOrPutAssumeCapacityAdapted(prefixed_path, FilesAdapter{});
        if (gop.found_existing) {
            self.cache.gpa.free(prefixed_path.sub_path);
            gop.key_ptr.updateMaxSize(max_file_size);
            gop.key_ptr.updateHandle(handle);
            return gop.index;
        }
        gop.key_ptr.* = .{
            .prefixed_path = prefixed_path,
            .contents = null,
            .max_file_size = max_file_size,
            .stat = undefined,
            .bin_digest = undefined,
            .handle = handle,
        };

        self.hash.add(prefixed_path.prefix);
        self.hash.addBytes(prefixed_path.sub_path);

        return gop.index;
    }

    /// Deprecated, use `addOptionalFilePath`.
    pub fn addOptionalFile(self: *Manifest, optional_file_path: ?[]const u8) !void {
        self.hash.add(optional_file_path != null);
        const file_path = optional_file_path orelse return;
        _ = try self.addFile(file_path, null);
    }

    pub fn addOptionalFilePath(self: *Manifest, optional_file_path: ?Path) !void {
        self.hash.add(optional_file_path != null);
        const file_path = optional_file_path orelse return;
        _ = try self.addFilePath(file_path, null);
    }

    pub fn addListOfFiles(self: *Manifest, list_of_files: []const []const u8) !void {
        self.hash.add(list_of_files.len);
        for (list_of_files) |file_path| {
            _ = try self.addFile(file_path, null);
        }
    }

    pub fn addDepFile(self: *Manifest, dir: Io.Dir, dep_file_sub_path: []const u8) !void {
        assert(self.manifest_file == null);
        return self.addDepFileMaybePost(dir, dep_file_sub_path);
    }

    pub const HitError = error{
        /// Unable to check the cache for a reason that has been recorded into
        /// the `diagnostic` field.
        CacheCheckFailed,
        /// A cache manifest file exists however it could not be parsed.
        InvalidFormat,
        OutOfMemory,
        Canceled,
    };

    /// Check the cache to see if the input exists in it. If it exists, returns `true`.
    /// A hex encoding of its hash is available by calling `final`.
    ///
    /// This function will also acquire an exclusive lock to the manifest file. This means
    /// that a process holding a Manifest will block any other process attempting to
    /// acquire the lock. If `want_shared_lock` is `true`, a cache hit guarantees the
    /// manifest file to be locked in shared mode, and a cache miss guarantees the manifest
    /// file to be locked in exclusive mode.
    ///
    /// The lock on the manifest file is released when `deinit` is called. As another
    /// option, one may call `toOwnedLock` to obtain a smaller object which can represent
    /// the lock. `deinit` is safe to call whether or not `toOwnedLock` has been called.
    pub fn hit(self: *Manifest) HitError!bool {
        assert(self.manifest_file == null);

        self.diagnostic = .none;

        const ext = ".txt";
        var manifest_file_path: [hex_digest_len + ext.len]u8 = undefined;

        var bin_digest: BinDigest = undefined;
        self.hash.hasher.final(&bin_digest);

        self.hex_digest = binToHex(bin_digest);

        @memcpy(manifest_file_path[0..self.hex_digest.len], &self.hex_digest);
        manifest_file_path[hex_digest_len..][0..ext.len].* = ext.*;

        const io = self.cache.io;

        // We'll try to open the cache with an exclusive lock, but if that would block
        // and `want_shared_lock` is set, a shared lock might be sufficient, so we'll
        // open with a shared lock instead.
        while (true) {
            if (self.cache.manifest_dir.createFile(io, &manifest_file_path, .{
                .read = true,
                .truncate = false,
                .lock = .exclusive,
                .lock_nonblocking = self.want_shared_lock,
            })) |manifest_file| {
                self.manifest_file = manifest_file;
                self.have_exclusive_lock = true;
                break;
            } else |err| switch (err) {
                error.WouldBlock => {
                    self.manifest_file = self.cache.manifest_dir.openFile(io, &manifest_file_path, .{
                        .mode = .read_write,
                        .lock = .shared,
                    }) catch |e| {
                        self.diagnostic = .{ .manifest_create = e };
                        return error.CacheCheckFailed;
                    };
                    break;
                },
                error.FileNotFound => {
                    // There are no dir components, so the only possibility
                    // should be that the directory behind the handle has been
                    // deleted, however we have observed on macOS two processes
                    // racing to do openat() with O_CREAT manifest in ENOENT.
                    //
                    // As a workaround, we retry with exclusive=true which
                    // disambiguates by returning EEXIST, indicating original
                    // failure was a race, or ENOENT, indicating deletion of
                    // the directory of our open handle.
                    if (!builtin.os.tag.isDarwin()) {
                        self.diagnostic = .{ .manifest_create = error.FileNotFound };
                        return error.CacheCheckFailed;
                    }

                    if (self.cache.manifest_dir.createFile(io, &manifest_file_path, .{
                        .read = true,
                        .truncate = false,
                        .lock = .exclusive,
                        .lock_nonblocking = self.want_shared_lock,
                        .exclusive = true,
                    })) |manifest_file| {
                        self.manifest_file = manifest_file;
                        self.have_exclusive_lock = true;
                        break;
                    } else |excl_err| switch (excl_err) {
                        error.WouldBlock, error.PathAlreadyExists => continue,
                        error.FileNotFound => {
                            self.diagnostic = .{ .manifest_create = error.FileNotFound };
                            return error.CacheCheckFailed;
                        },
                        error.Canceled => return error.Canceled,
                        else => |e| {
                            self.diagnostic = .{ .manifest_create = e };
                            return error.CacheCheckFailed;
                        },
                    }
                },
                error.Canceled => return error.Canceled,
                else => |e| {
                    self.diagnostic = .{ .manifest_create = e };
                    return error.CacheCheckFailed;
                },
            }
        }

        self.want_refresh_timestamp = true;

        const input_file_count = self.files.entries.len;

        // We're going to construct a second hash. Its input will begin with the digest we've
        // already computed (`bin_digest`), and then it'll have the digests of each input file,
        // including "post" files (see `addFilePost`). If this is a hit, we learn the set of "post"
        // files from the manifest on disk. If this is a miss, we'll learn those from future calls
        // to `addFilePost` etc. As such, the state of `self.hash.hasher` after this function
        // depends on whether this is a hit or a miss.
        //
        // If we return `true` indicating a cache hit, then `self.hash.hasher` must already include
        // the digests of the "post" files, so the caller can call `final`. Otherwise, on a cache
        // miss, `self.hash.hasher` will include the digests of all non-"post" files -- that is,
        // the ones we've already been told about. The rest will be discovered through calls to
        // `addFilePost` etc, which will update the hasher. After all files are added, the user can
        // use `final`, and will at some point `writeManifest` the file list to disk.

        self.hash.hasher = hasher_init;
        self.hash.hasher.update(&bin_digest);

        hit: {
            const file_digests_populated: usize = digests: {
                switch (try self.hitWithCurrentLock()) {
                    .hit => break :hit,
                    .miss => |m| if (!try self.upgradeToExclusiveLock()) {
                        break :digests m.file_digests_populated;
                    },
                }
                // We've just had a miss with the shared lock, and upgraded to an exclusive lock. Someone
                // else might have modified the digest, so we need to check again before deciding to miss.
                // Before trying again, we must reset `self.hash.hasher` and `self.files`.
                // This is basically just the first half of `unhit`.
                self.hash.hasher = hasher_init;
                self.hash.hasher.update(&bin_digest);
                while (self.files.count() != input_file_count) {
                    var file = self.files.pop().?;
                    file.key.deinit(self.cache.gpa);
                }
                switch (try self.hitWithCurrentLock()) {
                    .hit => break :hit,
                    .miss => |m| break :digests m.file_digests_populated,
                }
            };

            // This is a guaranteed cache miss. We're almost ready to return `false`, but there's a
            // little bookkeeping to do first. The first `file_digests_populated` entries in `files`
            // have their `bin_digest` populated; there may be some left in `input_file_count` which
            // we'll need to populate ourselves. Other than that, this is basically `unhit`.
            self.manifest_dirty = true;
            self.hash.hasher = hasher_init;
            self.hash.hasher.update(&bin_digest);
            while (self.files.count() != input_file_count) {
                var file = self.files.pop().?;
                file.key.deinit(self.cache.gpa);
            }
            for (self.files.keys(), 0..) |*file, idx| {
                if (idx < file_digests_populated) {
                    // `bin_digest` is already populated by `hitWithCurrentLock`, so we can use it directly.
                    self.hash.hasher.update(&file.bin_digest);
                } else {
                    self.populateFileHash(file) catch |err| {
                        self.diagnostic = .{ .file_hash = .{
                            .file_index = idx,
                            .err = err,
                        } };
                        return error.CacheCheckFailed;
                    };
                }
            }
            return false;
        }

        if (self.want_shared_lock) {
            self.downgradeToSharedLock() catch |err| {
                self.diagnostic = .{ .manifest_lock = err };
                return error.CacheCheckFailed;
            };
        }

        return true;
    }

    /// Assumes that `self.hash.hasher` has been updated only with the original digest and that
    /// `self.files` contains only the original input files.
    fn hitWithCurrentLock(self: *Manifest) HitError!union(enum) {
        hit,
        miss: struct {
            file_digests_populated: usize,
        },
    } {
        const gpa = self.cache.gpa;
        const io = self.cache.io;
        const input_file_count = self.files.entries.len;
        var tiny_buffer: [1]u8 = undefined; // allows allocRemaining to detect limit exceeded
        var manifest_reader = self.manifest_file.?.reader(io, &tiny_buffer); // Reads positionally from zero.
        const limit: std.Io.Limit = .limited(manifest_file_size_max);
        const file_contents = manifest_reader.interface.allocRemaining(gpa, limit) catch |err| switch (err) {
            error.OutOfMemory => return error.OutOfMemory,
            error.StreamTooLong => return error.OutOfMemory,
            error.ReadFailed => {
                self.diagnostic = .{ .manifest_read = manifest_reader.err.? };
                return error.CacheCheckFailed;
            },
        };
        defer gpa.free(file_contents);

        var any_file_changed = false;
        var line_iter = mem.tokenizeScalar(u8, file_contents, '\n');
        var idx: usize = 0;
        const header_valid = valid: {
            const line = line_iter.next() orelse break :valid false;
            break :valid std.mem.eql(u8, line, manifest_header);
        };
        if (!header_valid) {
            return .{ .miss = .{ .file_digests_populated = 0 } };
        }
        while (line_iter.next()) |line| {
            defer idx += 1;

            var iter = mem.tokenizeScalar(u8, line, ' ');
            const size = iter.next() orelse return error.InvalidFormat;
            const inode = iter.next() orelse return error.InvalidFormat;
            const mtime_nsec_str = iter.next() orelse return error.InvalidFormat;
            const digest_str = iter.next() orelse return error.InvalidFormat;
            const prefix_str = iter.next() orelse return error.InvalidFormat;
            const file_path = iter.rest();

            const stat_size = fmt.parseInt(u64, size, 10) catch return error.InvalidFormat;
            const stat_inode = fmt.parseInt(Io.File.INode, inode, 10) catch return error.InvalidFormat;
            const stat_mtime = fmt.parseInt(i64, mtime_nsec_str, 10) catch return error.InvalidFormat;
            const file_bin_digest = b: {
                if (digest_str.len != hex_digest_len) return error.InvalidFormat;
                var bd: BinDigest = undefined;
                _ = fmt.hexToBytes(&bd, digest_str) catch return error.InvalidFormat;
                break :b bd;
            };

            const prefix = fmt.parseInt(u8, prefix_str, 10) catch return error.InvalidFormat;
            if (prefix >= self.cache.prefixes_len) return error.InvalidFormat;

            if (file_path.len == 0) return error.InvalidFormat;

            const cache_hash_file = f: {
                const prefixed_path: PrefixedPath = .{
                    .prefix = prefix,
                    .sub_path = file_path, // expires with file_contents
                };
                if (idx < input_file_count) {
                    const file = &self.files.keys()[idx];
                    if (!file.prefixed_path.eql(prefixed_path))
                        return error.InvalidFormat;

                    file.stat = .{
                        .size = stat_size,
                        .inode = stat_inode,
                        .mtime = .{ .nanoseconds = stat_mtime },
                    };
                    file.bin_digest = file_bin_digest;
                    break :f file;
                }
                const gop = try self.files.getOrPutAdapted(gpa, prefixed_path, FilesAdapter{});
                errdefer _ = self.files.pop();
                if (!gop.found_existing) {
                    gop.key_ptr.* = .{
                        .prefixed_path = .{
                            .prefix = prefix,
                            .sub_path = try gpa.dupe(u8, file_path),
                        },
                        .contents = null,
                        .max_file_size = null,
                        .handle = null,
                        .stat = .{
                            .size = stat_size,
                            .inode = stat_inode,
                            .mtime = .{ .nanoseconds = stat_mtime },
                        },
                        .bin_digest = file_bin_digest,
                    };
                }
                break :f gop.key_ptr;
            };

            const pp = cache_hash_file.prefixed_path;
            const dir = self.cache.prefixes()[pp.prefix].handle;
            const this_file = dir.openFile(io, pp.sub_path, .{ .mode = .read_only }) catch |err| switch (err) {
                error.FileNotFound => {
                    // Every digest before this one has been populated successfully.
                    return .{ .miss = .{ .file_digests_populated = idx } };
                },
                error.Canceled => return error.Canceled,
                else => |e| {
                    self.diagnostic = .{ .file_open = .{
                        .file_index = idx,
                        .err = e,
                    } };
                    return error.CacheCheckFailed;
                },
            };
            defer this_file.close(io);

            const actual_stat = this_file.stat(io) catch |err| {
                self.diagnostic = .{ .file_stat = .{
                    .file_index = idx,
                    .err = err,
                } };
                return error.CacheCheckFailed;
            };
            const size_match = actual_stat.size == cache_hash_file.stat.size;
            const mtime_match = actual_stat.mtime.nanoseconds == cache_hash_file.stat.mtime.nanoseconds;
            const inode_match = actual_stat.inode == cache_hash_file.stat.inode;

            if (!size_match or !mtime_match or !inode_match) {
                cache_hash_file.stat = .{
                    .size = actual_stat.size,
                    .mtime = actual_stat.mtime,
                    .inode = actual_stat.inode,
                };

                if (try self.isProblematicTimestamp(cache_hash_file.stat.mtime)) {
                    // The actual file has an unreliable timestamp, force it to be hashed
                    cache_hash_file.stat.mtime = .zero;
                    cache_hash_file.stat.inode = 0;
                }

                var actual_digest: BinDigest = undefined;
                hashFile(io, this_file, &actual_digest) catch |err| {
                    self.diagnostic = .{ .file_read = .{
                        .file_index = idx,
                        .err = err,
                    } };
                    return error.CacheCheckFailed;
                };

                if (!mem.eql(u8, &cache_hash_file.bin_digest, &actual_digest)) {
                    cache_hash_file.bin_digest = actual_digest;
                    // keep going until we have the input file digests
                    any_file_changed = true;
                }
            }

            if (!any_file_changed) {
                self.hash.hasher.update(&cache_hash_file.bin_digest);
            }
        }

        // If the manifest was somehow missing one of our input files, or if any file hash has changed,
        // then this is a cache miss. However, we have successfully populated some or all of the file
        // digests.
        if (any_file_changed or idx < input_file_count) {
            return .{ .miss = .{ .file_digests_populated = idx } };
        }

        return .hit;
    }

    /// Reset `self.hash.hasher` to the state it should be in after `hit` returns `false`.
    /// The hasher contains the original input digest, and all original input file digests (i.e.
    /// not including post files).
    /// Assumes that `bin_digest` is populated for all files up to `input_file_count`. As such,
    /// this is not necessarily safe to call within `hit`.
    pub fn unhit(self: *Manifest, bin_digest: BinDigest, input_file_count: usize) void {
        // Reset the hash.
        self.hash.hasher = hasher_init;
        self.hash.hasher.update(&bin_digest);

        // Remove files not in the initial hash.
        while (self.files.count() != input_file_count) {
            var file = self.files.pop().?;
            file.key.deinit(self.cache.gpa);
        }

        for (self.files.keys()) |file| {
            self.hash.hasher.update(&file.bin_digest);
        }
    }

    fn isProblematicTimestamp(man: *Manifest, timestamp: Io.Timestamp) error{Canceled}!bool {
        const io = man.cache.io;

        // If the file_time is prior to the most recent problematic timestamp
        // then we don't need to access the filesystem.
        if (timestamp.nanoseconds < man.recent_problematic_timestamp.nanoseconds)
            return false;

        // Next we will check the globally shared Cache timestamp, which is accessed
        // from multiple threads.
        try man.cache.mutex.lock(io);
        defer man.cache.mutex.unlock(io);

        // Save the global one to our local one to avoid locking next time.
        man.recent_problematic_timestamp = man.cache.recent_problematic_timestamp;
        if (timestamp.nanoseconds < man.recent_problematic_timestamp.nanoseconds)
            return false;

        // This flag prevents multiple filesystem writes for the same hit() call.
        if (man.want_refresh_timestamp) {
            man.want_refresh_timestamp = false;

            var file = man.cache.manifest_dir.createFile(io, "timestamp", .{
                .read = true,
                .truncate = true,
            }) catch |err| switch (err) {
                error.Canceled => return error.Canceled,
                else => return true,
            };
            defer file.close(io);

            // Save locally and also save globally (we still hold the global lock).
            const stat = file.stat(io) catch |err| switch (err) {
                error.Canceled => return error.Canceled,
                else => return true,
            };
            man.recent_problematic_timestamp = stat.mtime;
            man.cache.recent_problematic_timestamp = man.recent_problematic_timestamp;
        }

        return timestamp.nanoseconds >= man.recent_problematic_timestamp.nanoseconds;
    }

    fn populateFileHash(self: *Manifest, ch_file: *File) !void {
        const io = self.cache.io;

        if (ch_file.handle) |handle| {
            return populateFileHashHandle(self, ch_file, handle);
        } else {
            const pp = ch_file.prefixed_path;
            const dir = self.cache.prefixes()[pp.prefix].handle;
            const handle = try dir.openFile(io, pp.sub_path, .{});
            defer handle.close(io);
            return populateFileHashHandle(self, ch_file, handle);
        }
    }

    fn populateFileHashHandle(self: *Manifest, ch_file: *File, io_file: Io.File) !void {
        const io = self.cache.io;
        const gpa = self.cache.gpa;

        const actual_stat = try io_file.stat(io);
        ch_file.stat = .{
            .size = actual_stat.size,
            .mtime = actual_stat.mtime,
            .inode = actual_stat.inode,
        };

        if (try self.isProblematicTimestamp(ch_file.stat.mtime)) {
            // The actual file has an unreliable timestamp, force it to be hashed
            ch_file.stat.mtime = .zero;
            ch_file.stat.inode = 0;
        }

        if (ch_file.max_file_size) |max_file_size| {
            if (ch_file.stat.size > max_file_size) return error.FileTooBig;

            // Hash while reading from disk, to keep the contents in the cpu
            // cache while doing hashing.
            const contents = try gpa.alloc(u8, @intCast(ch_file.stat.size));
            errdefer gpa.free(contents);

            var hasher = hasher_init;
            var off: usize = 0;
            while (true) {
                const bytes_read = try io_file.readPositional(io, &.{contents[off..]}, off);
                if (bytes_read == 0) break;
                hasher.update(contents[off..][0..bytes_read]);
                off += bytes_read;
            }
            hasher.final(&ch_file.bin_digest);

            ch_file.contents = contents;
        } else {
            try hashFile(io, io_file, &ch_file.bin_digest);
        }

        self.hash.hasher.update(&ch_file.bin_digest);
    }

    /// Add a file as a dependency of process being cached, after the initial hash has been
    /// calculated. This is useful for processes that don't know all the files that
    /// are depended on ahead of time. For example, a source file that can import other files
    /// will need to be recompiled if the imported file is changed.
    pub fn addFilePostFetch(self: *Manifest, file_path: []const u8, max_file_size: usize) ![]const u8 {
        assert(self.manifest_file != null);

        const gpa = self.cache.gpa;
        const prefixed_path = try self.cache.findPrefix(file_path);
        errdefer gpa.free(prefixed_path.sub_path);

        const gop = try self.files.getOrPutAdapted(gpa, prefixed_path, FilesAdapter{});
        errdefer _ = self.files.pop();

        if (gop.found_existing) {
            gpa.free(prefixed_path.sub_path);
            return gop.key_ptr.contents.?;
        }

        gop.key_ptr.* = .{
            .prefixed_path = prefixed_path,
            .max_file_size = max_file_size,
            .stat = undefined,
            .bin_digest = undefined,
            .contents = null,
        };

        self.files.lockPointers();
        defer self.files.unlockPointers();

        try self.populateFileHash(gop.key_ptr);
        return gop.key_ptr.contents.?;
    }

    /// Add a file as a dependency of process being cached, after the initial hash has been
    /// calculated.
    ///
    /// This is useful for processes that don't know the all the files that are
    /// depended on ahead of time. For example, a source file that can import
    /// other files will need to be recompiled if the imported file is changed.
    pub fn addFilePost(self: *Manifest, file_path: []const u8) !void {
        assert(self.manifest_file != null);

        const gpa = self.cache.gpa;
        const prefixed_path = try self.cache.findPrefix(file_path);
        errdefer gpa.free(prefixed_path.sub_path);

        const gop = try self.files.getOrPutAdapted(gpa, prefixed_path, FilesAdapter{});
        errdefer _ = self.files.pop();

        if (gop.found_existing) {
            gpa.free(prefixed_path.sub_path);
            return;
        }

        gop.key_ptr.* = .{
            .prefixed_path = prefixed_path,
            .max_file_size = null,
            .handle = null,
            .stat = undefined,
            .bin_digest = undefined,
            .contents = null,
        };

        self.files.lockPointers();
        defer self.files.unlockPointers();

        try self.populateFileHash(gop.key_ptr);
    }

    /// Like `addFilePost` but when the file contents have already been loaded from disk.
    pub fn addFilePostContents(
        self: *Manifest,
        file_path: []const u8,
        bytes: []const u8,
        stat: File.Stat,
    ) !void {
        assert(self.manifest_file != null);
        const gpa = self.cache.gpa;

        const prefixed_path = try self.cache.findPrefix(file_path);
        errdefer gpa.free(prefixed_path.sub_path);

        const gop = try self.files.getOrPutAdapted(gpa, prefixed_path, FilesAdapter{});
        errdefer _ = self.files.pop();

        if (gop.found_existing) {
            gpa.free(prefixed_path.sub_path);
            return;
        }

        const new_file = gop.key_ptr;

        new_file.* = .{
            .prefixed_path = prefixed_path,
            .max_file_size = null,
            .handle = null,
            .stat = stat,
            .bin_digest = undefined,
            .contents = null,
        };

        if (try self.isProblematicTimestamp(new_file.stat.mtime)) {
            // The actual file has an unreliable timestamp, force it to be hashed
            new_file.stat.mtime = .zero;
            new_file.stat.inode = 0;
        }

        {
            var hasher = hasher_init;
            hasher.update(bytes);
            hasher.final(&new_file.bin_digest);
        }

        self.hash.hasher.update(&new_file.bin_digest);
    }

    pub fn addDepFilePost(self: *Manifest, dir: Io.Dir, dep_file_sub_path: []const u8) !void {
        assert(self.manifest_file != null);
        return self.addDepFileMaybePost(dir, dep_file_sub_path);
    }

    fn addDepFileMaybePost(self: *Manifest, dir: Io.Dir, dep_file_sub_path: []const u8) !void {
        const gpa = self.cache.gpa;
        const io = self.cache.io;
        const dep_file_contents = try dir.readFileAlloc(io, dep_file_sub_path, gpa, .limited(manifest_file_size_max));
        defer gpa.free(dep_file_contents);

        var error_buf: std.ArrayList(u8) = .empty;
        defer error_buf.deinit(gpa);

        var resolve_buf: std.ArrayList(u8) = .empty;
        defer resolve_buf.deinit(gpa);

        var it: DepTokenizer = .{ .bytes = dep_file_contents };
        while (it.next()) |token| {
            switch (token) {
                // We don't care about targets, we only want the prereqs
                // Clang is invoked in single-source mode but other programs may not
                .target, .target_must_resolve => {},
                .prereq => |file_path| if (self.manifest_file == null) {
                    _ = try self.addFile(file_path, null);
                } else try self.addFilePost(file_path),
                .prereq_must_resolve => {
                    resolve_buf.clearRetainingCapacity();
                    try token.resolve(gpa, &resolve_buf);
                    if (self.manifest_file == null) {
                        _ = try self.addFile(resolve_buf.items, null);
                    } else try self.addFilePost(resolve_buf.items);
                },
                else => |err| {
                    try err.printError(gpa, &error_buf);
                    log.err("failed parsing {s}: {s}", .{ dep_file_sub_path, error_buf.items });
                    return error.InvalidDepFile;
                },
            }
        }
    }

    /// Returns a binary hash of the inputs.
    pub fn finalBin(self: *Manifest) BinDigest {
        assert(self.manifest_file != null);

        // We don't close the manifest file yet, because we want to
        // keep it locked until the API user is done using it.
        // We also don't write out the manifest yet, because until
        // cache_release is called we still might be working on creating
        // the artifacts to cache.

        var bin_digest: BinDigest = undefined;
        self.hash.hasher.final(&bin_digest);
        return bin_digest;
    }

    /// Returns a hex encoded hash of the inputs.
    pub fn final(self: *Manifest) HexDigest {
        const bin_digest = self.finalBin();
        return binToHex(bin_digest);
    }

    /// If `want_shared_lock` is true, this function automatically downgrades the
    /// lock from exclusive to shared.
    pub fn writeManifest(self: *Manifest) !void {
        assert(self.have_exclusive_lock);
        const io = self.cache.io;
        const manifest_file = self.manifest_file.?;
        if (self.manifest_dirty) {
            self.manifest_dirty = false;

            var buffer: [4000]u8 = undefined;
            var fw = manifest_file.writer(io, &buffer);
            writeDirtyManifestToStream(self, &fw) catch |err| switch (err) {
                error.WriteFailed => return fw.err.?,
                else => |e| return e,
            };
        }

        if (self.want_shared_lock) {
            try self.downgradeToSharedLock();
        }
    }

    fn writeDirtyManifestToStream(self: *Manifest, fw: *Io.File.Writer) !void {
        try fw.interface.writeAll(manifest_header ++ "\n");
        for (self.files.keys()) |file| {
            try fw.interface.print("{d} {d} {d} {x} {d} {s}\n", .{
                file.stat.size,
                file.stat.inode,
                file.stat.mtime,
                &file.bin_digest,
                file.prefixed_path.prefix,
                file.prefixed_path.sub_path,
            });
        }
        try fw.end();
    }

    fn downgradeToSharedLock(self: *Manifest) !void {
        if (!self.have_exclusive_lock) return;
        const io = self.cache.io;

        if (std.process.can_spawn or !builtin.single_threaded) {
            const manifest_file = self.manifest_file.?;
            try manifest_file.downgradeLock(io);
        }

        self.have_exclusive_lock = false;
    }

    fn upgradeToExclusiveLock(self: *Manifest) error{CacheCheckFailed}!bool {
        if (self.have_exclusive_lock) return false;
        assert(self.manifest_file != null);
        const io = self.cache.io;

        if (std.process.can_spawn or !builtin.single_threaded) {
            const manifest_file = self.manifest_file.?;
            // Here we intentionally have a period where the lock is released, in case there are
            // other processes holding a shared lock.
            manifest_file.unlock(io);
            manifest_file.lock(io, .exclusive) catch |err| {
                self.diagnostic = .{ .manifest_lock = err };
                return error.CacheCheckFailed;
            };
        }
        self.have_exclusive_lock = true;
        return true;
    }

    /// Obtain only the data needed to maintain a lock on the manifest file.
    /// The `Manifest` remains safe to deinit.
    /// Don't forget to call `writeManifest` before this!
    pub fn toOwnedLock(self: *Manifest) Lock {
        defer self.manifest_file = null;
        return .{ .manifest_file = self.manifest_file.? };
    }

    /// Releases the manifest file and frees any memory the Manifest was using.
    /// `Manifest.hit` must be called first.
    /// Don't forget to call `writeManifest` before this!
    pub fn deinit(self: *Manifest) void {
        const io = self.cache.io;

        if (self.manifest_file) |file| {
            if (builtin.os.tag == .windows) {
                // See Lock.release for why this is required on Windows
                file.unlock(io);
            }

            file.close(io);
        }
        for (self.files.keys()) |*file| {
            file.deinit(self.cache.gpa);
        }
        self.files.deinit(self.cache.gpa);
    }

    pub fn populateFileSystemInputs(man: *Manifest, buf: *std.ArrayList(u8)) Allocator.Error!void {
        assert(@typeInfo(std.zig.Server.Message.PathPrefix).@"enum".fields.len == man.cache.prefixes_len);
        buf.clearRetainingCapacity();
        const gpa = man.cache.gpa;
        const files = man.files.keys();
        if (files.len > 0) {
            for (files) |file| {
                try buf.ensureUnusedCapacity(gpa, file.prefixed_path.sub_path.len + 2);
                buf.appendAssumeCapacity(file.prefixed_path.prefix + 1);
                buf.appendSliceAssumeCapacity(file.prefixed_path.sub_path);
                buf.appendAssumeCapacity(0);
            }
            // The null byte is a separator, not a terminator.
            buf.items.len -= 1;
        }
    }

    pub fn populateOtherManifest(man: *Manifest, other: *Manifest, prefix_map: [4]u8) Allocator.Error!void {
        const gpa = other.cache.gpa;
        assert(@typeInfo(std.zig.Server.Message.PathPrefix).@"enum".field
```
