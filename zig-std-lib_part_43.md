```
aller must
/// validate that `pc` is indeed in its range -- if it is not, then no FDE matches `pc`.
pub fn lookupPc(unwind: *const Unwind, pc: u64, addr_size_bytes: u8, endian: Endian) !?u64 {
    const sorted_fdes: []const SortedFdeEntry = switch (unwind.lookup.?) {
        .eh_frame_hdr => |eh_frame_hdr| {
            const fde_vaddr = try eh_frame_hdr.table.findEntry(
                eh_frame_hdr.vaddr,
                pc,
                addr_size_bytes,
                endian,
            ) orelse return null;
            return std.math.sub(u64, fde_vaddr, unwind.frame_section.vaddr) catch bad(); // convert vaddr to offset
        },
        .sorted_fdes => |sorted_fdes| sorted_fdes,
    };
    if (sorted_fdes.len == 0) return null;
    var start: usize = 0;
    var len: usize = sorted_fdes.len;
    while (len > 1) {
        const half = len / 2;
        if (pc < sorted_fdes[start + half].pc_begin) {
            len = half;
        } else {
            start += half;
            len -= half;
        }
    }
    // If any FDE matches, it'll be the one at `start` (maybe false positive).
    return sorted_fdes[start].fde_offset;
}

/// Get the FDE at a given offset, as well as its associated CIE. This offset typically comes from
/// `lookupPc`. The CFI instructions within can be evaluated with `VirtualMachine`.
pub fn getFde(unwind: *const Unwind, fde_offset: u64, endian: Endian) !struct { *const CommonInformationEntry, FrameDescriptionEntry } {
    const section = unwind.frame_section;

    if (fde_offset > section.bytes.len) return error.EndOfStream;
    var fde_reader: Reader = .fixed(section.bytes[@intCast(fde_offset)..]);
    const fde_info = switch (try EntryHeader.read(&fde_reader, fde_offset, section.id, endian)) {
        .fde => |info| info,
        .cie, .terminator => return bad(), // This is meant to be an FDE
    };

    const cie = unwind.findCie(fde_info.cie_offset) orelse return error.InvalidDebugInfo;
    const fde: FrameDescriptionEntry = try .parse(
        section.vaddr + fde_offset + fde_reader.seek,
        try fde_reader.take(cast(usize, fde_info.bytes_len) orelse return error.EndOfStream),
        cie,
        endian,
    );

    return .{ cie, fde };
}

const EhPointerContext = struct {
    /// The address of the pointer field itself
    pc_rel_base: u64,
    // These relative addressing modes are only used in specific cases, and
    // might not be available / required in all parsing contexts
    data_rel_base: ?u64 = null,
    text_rel_base: ?u64 = null,
    function_rel_base: ?u64 = null,
};
/// Returns `error.InvalidDebugInfo` if the encoding is `EH.PE.omit`.
fn readEhPointerAbs(r: *Reader, enc_ty: EH.PE.Type, addr_size_bytes: u8, endian: Endian) !union(enum) {
    signed: i64,
    unsigned: u64,
} {
    return switch (enc_ty) {
        .absptr => .{
            .unsigned = switch (addr_size_bytes) {
                2 => try r.takeInt(u16, endian),
                4 => try r.takeInt(u32, endian),
                8 => try r.takeInt(u64, endian),
                else => return error.UnsupportedAddrSize,
            },
        },
        .uleb128 => .{ .unsigned = try r.takeLeb128(u64) },
        .udata2 => .{ .unsigned = try r.takeInt(u16, endian) },
        .udata4 => .{ .unsigned = try r.takeInt(u32, endian) },
        .udata8 => .{ .unsigned = try r.takeInt(u64, endian) },
        .sleb128 => .{ .signed = try r.takeLeb128(i64) },
        .sdata2 => .{ .signed = try r.takeInt(i16, endian) },
        .sdata4 => .{ .signed = try r.takeInt(i32, endian) },
        .sdata8 => .{ .signed = try r.takeInt(i64, endian) },
        else => return bad(),
    };
}
/// Returns `error.InvalidDebugInfo` if the encoding is `EH.PE.omit`.
fn readEhPointer(r: *Reader, enc: EH.PE, addr_size_bytes: u8, ctx: EhPointerContext, endian: Endian) !u64 {
    const offset = try readEhPointerAbs(r, enc.type, addr_size_bytes, endian);
    if (enc.indirect) return bad(); // GCC extension; not supported
    const base: u64 = switch (enc.rel) {
        .abs, .aligned => 0,
        .pcrel => ctx.pc_rel_base,
        .textrel => ctx.text_rel_base orelse return bad(),
        .datarel => ctx.data_rel_base orelse return bad(),
        .funcrel => ctx.function_rel_base orelse return bad(),
        _ => return bad(),
    };
    return switch (offset) {
        .signed => |s| if (s >= 0)
            try std.math.add(u64, base, @intCast(s))
        else
            try std.math.sub(u64, base, @intCast(-s)),
        // absptr can actually contain signed values in some cases (aarch64 MachO)
        .unsigned => |u| u +% base,
    };
}

/// Like `Reader.fixed`, but when the length of the data is unknown and we just want to allow
/// reading indefinitely.
fn maxSlice(ptr: [*]const u8) []const u8 {
    const len = std.math.maxInt(usize) - @intFromPtr(ptr);
    return ptr[0..len];
}

const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const bad = Dwarf.bad;
const cast = std.math.cast;
const DW = std.dwarf;
const Dwarf = std.debug.Dwarf;
const EH = DW.EH;
const Endian = std.builtin.Endian;
const Format = DW.Format;
const maxInt = std.math.maxInt;
const missing = Dwarf.missing;
const Reader = std.Io.Reader;
const std = @import("std");
const Unwind = @This();



---
File: /std/debug/SelfInfo/Elf.zig
---

rwlock: Io.RwLock,

modules: std.ArrayList(Module),
ranges: std.ArrayList(Module.Range),

unwind_cache: if (can_unwind) ?[]Dwarf.SelfUnwinder.CacheEntry else ?noreturn,

pub const init: SelfInfo = .{
    .rwlock = .init,
    .modules = .empty,
    .ranges = .empty,
    .unwind_cache = null,
};
pub fn deinit(si: *SelfInfo, io: Io) void {
    _ = io;
    const gpa = std.debug.getDebugInfoAllocator();
    for (si.modules.items) |*mod| {
        unwind: {
            const u = &(mod.unwind orelse break :unwind catch break :unwind);
            for (u.buf[0..u.len]) |*unwind| unwind.deinit(gpa);
        }
        loaded: {
            const l = &(mod.loaded_elf orelse break :loaded catch break :loaded);
            l.file.deinit(gpa);
        }
    }

    si.modules.deinit(gpa);
    si.ranges.deinit(gpa);
    if (si.unwind_cache) |cache| gpa.free(cache);
}

pub fn getSymbols(
    si: *SelfInfo,
    io: Io,
    symbol_allocator: Allocator,
    text_arena: Allocator,
    address: usize,
    resolve_inline_callers: bool,
    symbols: *std.ArrayList(std.debug.Symbol),
) Error!void {
    const gpa = std.debug.getDebugInfoAllocator();
    const module = try si.findModule(gpa, io, address, .exclusive);
    defer si.rwlock.unlock(io);

    const vaddr = address - module.load_offset;

    const loaded_elf = try module.getLoadedElf(gpa, io);
    if (loaded_elf.file.dwarf) |*dwarf| {
        if (!loaded_elf.scanned_dwarf) {
            dwarf.open(gpa, native_endian) catch |err| switch (err) {
                error.InvalidDebugInfo,
                error.MissingDebugInfo,
                error.OutOfMemory,
                => |e| return e,
                error.EndOfStream,
                error.Overflow,
                error.ReadFailed,
                error.StreamTooLong,
                => return error.InvalidDebugInfo,
            };
            loaded_elf.scanned_dwarf = true;
        }
        return dwarf.getSymbols(
            symbol_allocator,
            text_arena,
            native_endian,
            vaddr,
            resolve_inline_callers,
            symbols,
        );
    }
    // When DWARF is unavailable, fall back to searching the symtab.
    try symbols.append(symbol_allocator, loaded_elf.file.searchSymtab(gpa, vaddr) catch |err| switch (err) {
        error.NoSymtab, error.NoStrtab => return error.MissingDebugInfo,
        error.BadSymtab => return error.InvalidDebugInfo,
        error.OutOfMemory => |e| return e,
    });
}
pub fn getModuleName(si: *SelfInfo, io: Io, address: usize) Error![]const u8 {
    const gpa = std.debug.getDebugInfoAllocator();
    const module = try si.findModule(gpa, io, address, .shared);
    defer si.rwlock.unlockShared(io);
    if (module.name.len == 0) return error.MissingDebugInfo;
    return module.name;
}
pub fn getModuleSlide(si: *SelfInfo, io: Io, address: usize) Error!usize {
    const gpa = std.debug.getDebugInfoAllocator();
    const module = try si.findModule(gpa, io, address, .shared);
    defer si.rwlock.unlockShared(io);
    return module.load_offset;
}

pub const can_unwind: bool = s: {
    // The DWARF code can't deal with ILP32 ABIs yet: https://github.com/ziglang/zig/issues/25447
    switch (builtin.target.abi) {
        .gnuabin32,
        .muslabin32,
        .gnux32,
        .muslx32,
        => break :s false,
        else => {},
    }

    // Notably, we are yet to support unwinding on ARM. There, unwinding is not done through
    // `.eh_frame`, but instead with the `.ARM.exidx` section, which has a different format.
    const archs: []const std.Target.Cpu.Arch = switch (builtin.target.os.tag) {
        // Not supported yet: arm
        .haiku => &.{
            .aarch64,
            .m68k,
            .riscv64,
            .x86,
            .x86_64,
        },
        // Not supported yet: arm/armeb/thumb/thumbeb, xtensa/xtensaeb
        .linux => &.{
            .aarch64,
            .aarch64_be,
            .arc,
            .csky,
            .loongarch32,
            .loongarch64,
            .m68k,
            .mips,
            .mipsel,
            .mips64,
            .mips64el,
            .or1k,
            .riscv32,
            .riscv64,
            .s390x,
            .x86,
            .x86_64,
        },
        .serenity => &.{
            .aarch64,
            .x86_64,
            .riscv64,
        },

        .dragonfly => &.{
            .x86_64,
        },
        // Not supported yet: arm
        .freebsd => &.{
            .aarch64,
            .riscv64,
            .x86_64,
        },
        // Not supported yet: arm/armeb, mips64/mips64el
        .netbsd => &.{
            .aarch64,
            .aarch64_be,
            .m68k,
            .mips,
            .mipsel,
            .x86,
            .x86_64,
        },
        // Not supported yet: arm
        .openbsd => &.{
            .aarch64,
            .mips64,
            .mips64el,
            .riscv64,
            .x86,
            .x86_64,
        },

        .illumos => &.{
            .x86,
            .x86_64,
        },

        else => unreachable,
    };
    for (archs) |a| {
        if (builtin.target.cpu.arch == a) break :s true;
    }
    break :s false;
};
comptime {
    if (can_unwind) {
        std.debug.assert(Dwarf.supportsUnwinding(&builtin.target));
    }
}
pub const UnwindContext = Dwarf.SelfUnwinder;
pub fn unwindFrame(si: *SelfInfo, io: Io, context: *UnwindContext) Error!usize {
    comptime assert(can_unwind);
    const gpa = std.debug.getDebugInfoAllocator();

    {
        si.rwlock.lockSharedUncancelable(io);
        defer si.rwlock.unlockShared(io);
        if (si.unwind_cache) |cache| {
            if (Dwarf.SelfUnwinder.CacheEntry.find(cache, context.pc)) |entry| {
                return context.next(gpa, entry);
            }
        }
    }

    const module = try si.findModule(gpa, io, context.pc, .exclusive);
    defer si.rwlock.unlock(io);

    if (si.unwind_cache == null) {
        si.unwind_cache = try gpa.alloc(Dwarf.SelfUnwinder.CacheEntry, 2048);
        @memset(si.unwind_cache.?, .empty);
    }

    const unwind_sections = try module.getUnwindSections(gpa, io);
    for (unwind_sections) |*unwind| {
        if (context.computeRules(gpa, unwind, module.load_offset, null)) |entry| {
            entry.populate(si.unwind_cache.?);
            return context.next(gpa, &entry);
        } else |err| switch (err) {
            error.MissingDebugInfo => continue,

            error.InvalidDebugInfo,
            error.UnsupportedDebugInfo,
            error.OutOfMemory,
            => |e| return e,

            error.EndOfStream,
            error.StreamTooLong,
            error.ReadFailed,
            error.Overflow,
            error.InvalidOpcode,
            error.InvalidOperation,
            error.InvalidOperand,
            => return error.InvalidDebugInfo,

            error.UnimplementedUserOpcode,
            error.UnsupportedAddrSize,
            => return error.UnsupportedDebugInfo,
        }
    }
    return error.MissingDebugInfo;
}

const Module = struct {
    load_offset: usize,
    name: []const u8,
    build_id: ?[]const u8,
    gnu_eh_frame: ?[]const u8,

    /// `null` means unwind information has not yet been loaded.
    unwind: ?(Error!UnwindSections),

    /// `null` means the ELF file has not yet been loaded.
    loaded_elf: ?(Error!LoadedElf),

    const LoadedElf = struct {
        file: std.debug.ElfFile,
        scanned_dwarf: bool,
    };

    const UnwindSections = struct {
        buf: [2]Dwarf.Unwind,
        len: usize,
    };

    const Range = struct {
        start: usize,
        len: usize,
        /// Index into `modules`
        module_index: usize,
    };

    /// Assumes we already hold an exclusive lock.
    fn getUnwindSections(mod: *Module, gpa: Allocator, io: Io) Error![]Dwarf.Unwind {
        if (mod.unwind == null) mod.unwind = loadUnwindSections(mod, gpa, io);
        const us = &(mod.unwind.? catch |err| return err);
        return us.buf[0..us.len];
    }
    fn loadUnwindSections(mod: *Module, gpa: Allocator, io: Io) Error!UnwindSections {
        var us: UnwindSections = .{
            .buf = undefined,
            .len = 0,
        };
        if (mod.gnu_eh_frame) |section_bytes| {
            const section_vaddr: u64 = @intFromPtr(section_bytes.ptr) - mod.load_offset;
            const header = Dwarf.Unwind.EhFrameHeader.parse(section_vaddr, section_bytes, @sizeOf(usize), native_endian) catch |err| switch (err) {
                error.ReadFailed => unreachable, // it's all fixed buffers
                error.InvalidDebugInfo => |e| return e,
                error.EndOfStream, error.Overflow => return error.InvalidDebugInfo,
                error.UnsupportedAddrSize => return error.UnsupportedDebugInfo,
            };
            us.buf[us.len] = .initEhFrameHdr(header, section_vaddr, @ptrFromInt(@as(usize, @intCast(mod.load_offset + header.eh_frame_vaddr))));
            us.len += 1;
        } else {
            // There is no `.eh_frame_hdr` section. There may still be an `.eh_frame` or `.debug_frame`
            // section, but we'll have to load the binary to get at it.
            const loaded = try mod.getLoadedElf(gpa, io);
            // If both are present, we can't just pick one -- the info could be split between them.
            // `.debug_frame` is likely to be the more complete section, so we'll prioritize that one.
            if (loaded.file.debug_frame) |*debug_frame| {
                us.buf[us.len] = .initSection(.debug_frame, debug_frame.vaddr, debug_frame.bytes);
                us.len += 1;
            }
            if (loaded.file.eh_frame) |*eh_frame| {
                us.buf[us.len] = .initSection(.eh_frame, eh_frame.vaddr, eh_frame.bytes);
                us.len += 1;
            }
        }
        errdefer for (us.buf[0..us.len]) |*u| u.deinit(gpa);
        for (us.buf[0..us.len]) |*u| u.prepare(gpa, @sizeOf(usize), native_endian, true, false) catch |err| switch (err) {
            error.ReadFailed => unreachable, // it's all fixed buffers
            error.InvalidDebugInfo,
            error.MissingDebugInfo,
            error.OutOfMemory,
            => |e| return e,
            error.EndOfStream,
            error.Overflow,
            error.StreamTooLong,
            error.InvalidOperand,
            error.InvalidOpcode,
            error.InvalidOperation,
            => return error.InvalidDebugInfo,
            error.UnsupportedAddrSize,
            error.UnsupportedDwarfVersion,
            error.UnimplementedUserOpcode,
            => return error.UnsupportedDebugInfo,
        };
        return us;
    }

    /// Assumes we already hold an exclusive lock.
    fn getLoadedElf(mod: *Module, gpa: Allocator, io: Io) Error!*LoadedElf {
        if (mod.loaded_elf == null) mod.loaded_elf = loadElf(mod, gpa, io);
        return if (mod.loaded_elf.?) |*elf| elf else |err| err;
    }

    fn loadElf(mod: *Module, gpa: Allocator, io: Io) Error!LoadedElf {
        const load_result = if (mod.name.len > 0) res: {
            var file = Io.Dir.cwd().openFile(io, mod.name, .{}) catch return error.MissingDebugInfo;
            defer file.close(io);
            break :res std.debug.ElfFile.load(gpa, io, file, mod.build_id, &.native(mod.name));
        } else res: {
            const path = std.process.executablePathAlloc(io, gpa) catch |err| switch (err) {
                error.OutOfMemory => |e| return e,
                else => return error.ReadFailed,
            };
            defer gpa.free(path);
            var file = Io.Dir.cwd().openFile(io, path, .{}) catch return error.MissingDebugInfo;
            defer file.close(io);
            break :res std.debug.ElfFile.load(gpa, io, file, mod.build_id, &.native(path));
        };

        var elf_file = load_result catch |err| switch (err) {
            error.OutOfMemory,
            error.Unexpected,
            error.Canceled,
            => |e| return e,

            error.Overflow,
            error.TruncatedElfFile,
            error.InvalidCompressedSection,
            error.InvalidElfMagic,
            error.InvalidElfVersion,
            error.InvalidElfClass,
            error.InvalidElfEndian,
            => return error.InvalidDebugInfo,

            error.SystemResources,
            error.MemoryMappingNotSupported,
            error.AccessDenied,
            error.LockedMemoryLimitExceeded,
            error.ProcessFdQuotaExceeded,
            error.SystemFdQuotaExceeded,
            error.Streaming,
            => return error.ReadFailed,
        };
        errdefer elf_file.deinit(gpa);

        if (elf_file.endian != native_endian) return error.InvalidDebugInfo;
        if (elf_file.is_64 != (@sizeOf(usize) == 8)) return error.InvalidDebugInfo;

        return .{
            .file = elf_file,
            .scanned_dwarf = false,
        };
    }
};

fn findModule(si: *SelfInfo, gpa: Allocator, io: Io, address: usize, lock: enum { shared, exclusive }) Error!*Module {
    // With the requested lock, scan the module ranges looking for `address`.
    switch (lock) {
        .shared => si.rwlock.lockSharedUncancelable(io),
        .exclusive => si.rwlock.lockUncancelable(io),
    }
    for (si.ranges.items) |*range| {
        if (address >= range.start and address < range.start + range.len) {
            return &si.modules.items[range.module_index];
        }
    }
    // The address wasn't in a known range. We will rebuild the module/range lists, since it's possible
    // a new module was loaded. Upgrade to an exclusive lock if necessary.
    switch (lock) {
        .shared => {
            si.rwlock.unlockShared(io);
            si.rwlock.lockUncancelable(io);
        },
        .exclusive => {},
    }
    // Rebuild module list with the exclusive lock.
    {
        errdefer si.rwlock.unlock(io);
        for (si.modules.items) |*mod| {
            unwind: {
                const u = &(mod.unwind orelse break :unwind catch break :unwind);
                for (u.buf[0..u.len]) |*unwind| unwind.deinit(gpa);
            }
            loaded: {
                const l = &(mod.loaded_elf orelse break :loaded catch break :loaded);
                l.file.deinit(gpa);
            }
        }
        si.modules.clearRetainingCapacity();
        si.ranges.clearRetainingCapacity();
        var ctx: DlIterContext = .{ .si = si, .gpa = gpa };
        try std.posix.dl_iterate_phdr(&ctx, error{OutOfMemory}, DlIterContext.callback);
    }
    // Downgrade the lock back to shared if necessary.
    switch (lock) {
        .shared => {
            si.rwlock.unlock(io);
            si.rwlock.lockSharedUncancelable(io);
        },
        .exclusive => {},
    }
    // Scan the newly rebuilt module ranges.
    for (si.ranges.items) |*range| {
        if (address >= range.start and address < range.start + range.len) {
            return &si.modules.items[range.module_index];
        }
    }
    // Still nothing; unlock and error.
    switch (lock) {
        .shared => si.rwlock.unlockShared(io),
        .exclusive => si.rwlock.unlock(io),
    }
    return error.MissingDebugInfo;
}
const DlIterContext = struct {
    si: *SelfInfo,
    gpa: Allocator,

    fn callback(info: *std.posix.dl_phdr_info, size: usize, context: *@This()) !void {
        _ = size;

        var build_id: ?[]const u8 = null;
        var gnu_eh_frame: ?[]const u8 = null;

        // Populate `build_id` and `gnu_eh_frame`
        for (info.phdr[0..info.phnum]) |phdr| {
            switch (phdr.type) {
                .NOTE => {
                    // Look for .note.gnu.build-id
                    const segment_ptr: [*]const u8 = @ptrFromInt(info.addr + phdr.vaddr);
                    var r: std.Io.Reader = .fixed(segment_ptr[0..phdr.memsz]);
                    const name_size = r.takeInt(u32, native_endian) catch continue;
                    const desc_size = r.takeInt(u32, native_endian) catch continue;
                    const note_type = r.takeInt(u32, native_endian) catch continue;
                    const name = r.take(name_size) catch continue;
                    if (note_type != std.elf.NT_GNU_BUILD_ID) continue;
                    if (!std.mem.eql(u8, name, "GNU\x00")) continue;
                    const desc = r.take(desc_size) catch continue;
                    build_id = desc;
                },
                std.elf.PT.GNU_EH_FRAME => {
                    const segment_ptr: [*]const u8 = @ptrFromInt(info.addr + phdr.vaddr);
                    gnu_eh_frame = segment_ptr[0..phdr.memsz];
                },
                else => {},
            }
        }

        const gpa = context.gpa;
        const si = context.si;

        const module_index = si.modules.items.len;
        try si.modules.append(gpa, .{
            .load_offset = info.addr,
            // Android libc uses NULL instead of "" to mark the main program
            .name = std.mem.sliceTo(info.name, 0) orelse "",
            .build_id = build_id,
            .gnu_eh_frame = gnu_eh_frame,
            .unwind = null,
            .loaded_elf = null,
        });

        for (info.phdr[0..info.phnum]) |phdr| {
            if (phdr.type != .LOAD) continue;
            try context.si.ranges.append(gpa, .{
                // Overflowing addition handles VSDOs having p_vaddr = 0xffffffffff700000
                .start = info.addr +% phdr.vaddr,
                .len = phdr.memsz,
                .module_index = module_index,
            });
        }
    }
};

const std = @import("std");
const Io = std.Io;
const Allocator = std.mem.Allocator;
const Dwarf = std.debug.Dwarf;
const Error = std.debug.SelfInfoError;
const assert = std.debug.assert;

const builtin = @import("builtin");
const native_endian = builtin.target.cpu.arch.endian();

const SelfInfo = @This();



---
File: /std/debug/SelfInfo/MachO.zig
---

mutex: Io.Mutex,
/// Accessed through `Module.Adapter`.
modules: std.ArrayHashMapUnmanaged(Module, void, Module.Context, false),

pub const init: SelfInfo = .{
    .mutex = .init,
    .modules = .empty,
};
pub fn deinit(si: *SelfInfo, io: Io) void {
    _ = io;
    const gpa = std.debug.getDebugInfoAllocator();
    for (si.modules.keys()) |*module| {
        unwind: {
            const u = &(module.unwind orelse break :unwind catch break :unwind);
            if (u.dwarf) |*dwarf| dwarf.deinit(gpa);
        }
        file: {
            const f = &(module.file orelse break :file catch break :file);
            f.deinit(gpa);
        }
    }
    si.modules.deinit(gpa);
}

pub fn getSymbols(
    si: *SelfInfo,
    io: Io,
    symbol_allocator: Allocator,
    text_arena: Allocator,
    address: usize,
    resolve_inline_callers: bool,
    symbols: *std.ArrayList(std.debug.Symbol),
) Error!void {
    _ = resolve_inline_callers;
    const gpa = std.debug.getDebugInfoAllocator();

    const module = try si.findModule(gpa, io, address);
    defer si.mutex.unlock(io);

    const file = try module.getFile(gpa, io);

    // This is not necessarily the same as the vmaddr_slide that dyld would report. This is
    // because the segments in the file on disk might differ from the ones in memory. Normally
    // we wouldn't necessarily expect that to work, but /usr/lib/dyld is incredibly annoying:
    // it exists on disk (necessarily, because the kernel needs to load it!), but is also in
    // the dyld cache (dyld actually restart itself from cache after loading it), and the two
    // versions have (very) different segment base addresses. It's sort of like a large slide
    // has been applied to all addresses in memory. For an optimal experience, we consider the
    // on-disk vmaddr instead of the in-memory one.
    const vaddr_offset = module.text_base - file.text_vmaddr;

    const vaddr = address - vaddr_offset;

    const ofile_dwarf, const ofile_vaddr = file.getDwarfForAddress(gpa, io, vaddr) catch {
        // Return at least the symbol name if available.
        return symbols.append(symbol_allocator, .{
            .name = try file.lookupSymbolName(vaddr),
            .compile_unit_name = null,
            .source_location = null,
        });
    };

    const compile_unit = ofile_dwarf.findCompileUnit(native_endian, ofile_vaddr) catch {
        // Return at least the symbol name if available.
        return symbols.append(symbol_allocator, .{
            .name = try file.lookupSymbolName(vaddr),
            .compile_unit_name = null,
            .source_location = null,
        });
    };

    try symbols.append(symbol_allocator, .{
        .name = ofile_dwarf.getSymbolName(ofile_vaddr) orelse
            try file.lookupSymbolName(vaddr),
        .compile_unit_name = compile_unit.die.getAttrString(
            ofile_dwarf,
            native_endian,
            std.dwarf.AT.name,
            ofile_dwarf.section(.debug_str),
            compile_unit,
        ) catch |err| switch (err) {
            error.MissingDebugInfo, error.InvalidDebugInfo => null,
        },
        .source_location = ofile_dwarf.getLineNumberInfo(
            gpa,
            text_arena,
            native_endian,
            compile_unit,
            ofile_vaddr,
        ) catch null,
    });
}
pub fn getModuleName(si: *SelfInfo, io: Io, address: usize) Error![]const u8 {
    _ = si;
    _ = io;
    // This function is marked as deprecated; however, it is significantly more
    // performant than `dladdr` (since the latter also does a very slow symbol
    // lookup), so let's use it since it's still available.
    return std.mem.span(std.c.dyld_image_path_containing_address(
        @ptrFromInt(address),
    ) orelse return error.MissingDebugInfo);
}
pub fn getModuleSlide(si: *SelfInfo, io: Io, address: usize) Error!usize {
    const gpa = std.debug.getDebugInfoAllocator();
    const module = try si.findModule(gpa, io, address);
    defer si.mutex.unlock(io);
    const header: *std.macho.mach_header_64 = @ptrFromInt(module.text_base);
    const raw_macho: [*]u8 = @ptrCast(header);
    var it = macho.LoadCommandIterator.init(header, raw_macho[@sizeOf(macho.mach_header_64)..][0..header.sizeofcmds]) catch unreachable;
    const text_vmaddr = while (it.next() catch unreachable) |load_cmd| {
        if (load_cmd.hdr.cmd != .SEGMENT_64) continue;
        const segment_cmd = load_cmd.cast(macho.segment_command_64).?;
        if (!mem.eql(u8, segment_cmd.segName(), "__TEXT")) continue;
        break segment_cmd.vmaddr;
    } else unreachable;
    return module.text_base - text_vmaddr;
}

pub const can_unwind: bool = true;
pub const UnwindContext = std.debug.Dwarf.SelfUnwinder;
/// Unwind a frame using MachO compact unwind info (from `__unwind_info`).
/// If the compact encoding can't encode a way to unwind a frame, it will
/// defer unwinding to DWARF, in which case `__eh_frame` will be used if available.
pub fn unwindFrame(si: *SelfInfo, io: Io, context: *UnwindContext) Error!usize {
    return unwindFrameInner(si, io, context) catch |err| switch (err) {
        error.InvalidDebugInfo,
        error.MissingDebugInfo,
        error.UnsupportedDebugInfo,
        error.ReadFailed,
        error.OutOfMemory,
        error.Unexpected,
        error.Canceled,
        => |e| return e,

        error.UnsupportedRegister,
        error.UnsupportedAddrSize,
        error.UnimplementedUserOpcode,
        => return error.UnsupportedDebugInfo,

        error.Overflow,
        error.EndOfStream,
        error.StreamTooLong,
        error.InvalidOpcode,
        error.InvalidOperation,
        error.InvalidOperand,
        error.InvalidRegister,
        error.IncompatibleRegisterSize,
        => return error.InvalidDebugInfo,
    };
}
fn unwindFrameInner(si: *SelfInfo, io: Io, context: *UnwindContext) !usize {
    const gpa = std.debug.getDebugInfoAllocator();
    const module = try si.findModule(gpa, io, context.pc);
    defer si.mutex.unlock(io);

    const unwind: *Module.Unwind = try module.getUnwindInfo(gpa);

    const ip_reg_num = comptime Dwarf.ipRegNum(builtin.target.cpu.arch).?;
    const fp_reg_num = comptime Dwarf.fpRegNum(builtin.target.cpu.arch);
    const sp_reg_num = comptime Dwarf.spRegNum(builtin.target.cpu.arch);

    const unwind_info = unwind.unwind_info orelse return error.MissingDebugInfo;
    if (unwind_info.len < @sizeOf(macho.unwind_info_section_header)) return error.InvalidDebugInfo;
    const header: *align(1) const macho.unwind_info_section_header = @ptrCast(unwind_info);

    const index_byte_count = header.indexCount * @sizeOf(macho.unwind_info_section_header_index_entry);
    if (unwind_info.len < header.indexSectionOffset + index_byte_count) return error.InvalidDebugInfo;
    const indices: []align(1) const macho.unwind_info_section_header_index_entry = @ptrCast(unwind_info[header.indexSectionOffset..][0..index_byte_count]);
    if (indices.len == 0) return error.MissingDebugInfo;

    // offset of the PC into the `__TEXT` segment
    const pc_text_offset = context.pc - module.text_base;

    const start_offset: u32, const first_level_offset: u32 = index: {
        var left: usize = 0;
        var len: usize = indices.len;
        while (len > 1) {
            const mid = left + len / 2;
            if (pc_text_offset < indices[mid].functionOffset) {
                len /= 2;
            } else {
                left = mid;
                len -= len / 2;
            }
        }
        break :index .{ indices[left].secondLevelPagesSectionOffset, indices[left].functionOffset };
    };
    // An offset of 0 is a sentinel indicating a range does not have unwind info.
    if (start_offset == 0) return error.MissingDebugInfo;

    const common_encodings_byte_count = header.commonEncodingsArrayCount * @sizeOf(macho.compact_unwind_encoding_t);
    if (unwind_info.len < header.commonEncodingsArraySectionOffset + common_encodings_byte_count) return error.InvalidDebugInfo;
    const common_encodings: []align(1) const macho.compact_unwind_encoding_t = @ptrCast(
        unwind_info[header.commonEncodingsArraySectionOffset..][0..common_encodings_byte_count],
    );

    if (unwind_info.len < start_offset + @sizeOf(macho.UNWIND_SECOND_LEVEL)) return error.InvalidDebugInfo;
    const kind: *align(1) const macho.UNWIND_SECOND_LEVEL = @ptrCast(unwind_info[start_offset..]);

    const entry: struct {
        function_offset: usize,
        raw_encoding: u32,
    } = switch (kind.*) {
        .REGULAR => entry: {
            if (unwind_info.len < start_offset + @sizeOf(macho.unwind_info_regular_second_level_page_header)) return error.InvalidDebugInfo;
            const page_header: *align(1) const macho.unwind_info_regular_second_level_page_header = @ptrCast(unwind_info[start_offset..]);

            const entries_byte_count = page_header.entryCount * @sizeOf(macho.unwind_info_regular_second_level_entry);
            if (unwind_info.len < start_offset + entries_byte_count) return error.InvalidDebugInfo;
            const entries: []align(1) const macho.unwind_info_regular_second_level_entry = @ptrCast(
                unwind_info[start_offset + page_header.entryPageOffset ..][0..entries_byte_count],
            );
            if (entries.len == 0) return error.InvalidDebugInfo;

            var left: usize = 0;
            var len: usize = entries.len;
            while (len > 1) {
                const mid = left + len / 2;
                if (pc_text_offset < entries[mid].functionOffset) {
                    len /= 2;
                } else {
                    left = mid;
                    len -= len / 2;
                }
            }
            break :entry .{
                .function_offset = entries[left].functionOffset,
                .raw_encoding = entries[left].encoding,
            };
        },
        .COMPRESSED => entry: {
            if (unwind_info.len < start_offset + @sizeOf(macho.unwind_info_compressed_second_level_page_header)) return error.InvalidDebugInfo;
            const page_header: *align(1) const macho.unwind_info_compressed_second_level_page_header = @ptrCast(unwind_info[start_offset..]);

            const entries_byte_count = page_header.entryCount * @sizeOf(macho.UnwindInfoCompressedEntry);
            if (unwind_info.len < start_offset + entries_byte_count) return error.InvalidDebugInfo;
            const entries: []align(1) const macho.UnwindInfoCompressedEntry = @ptrCast(
                unwind_info[start_offset + page_header.entryPageOffset ..][0..entries_byte_count],
            );
            if (entries.len == 0) return error.InvalidDebugInfo;

            var left: usize = 0;
            var len: usize = entries.len;
            while (len > 1) {
                const mid = left + len / 2;
                if (pc_text_offset < first_level_offset + entries[mid].funcOffset) {
                    len /= 2;
                } else {
                    left = mid;
                    len -= len / 2;
                }
            }
            const entry = entries[left];

            const function_offset = first_level_offset + entry.funcOffset;
            if (entry.encodingIndex < common_encodings.len) {
                break :entry .{
                    .function_offset = function_offset,
                    .raw_encoding = common_encodings[entry.encodingIndex],
                };
            }

            const local_index = entry.encodingIndex - common_encodings.len;
            const local_encodings_byte_count = page_header.encodingsCount * @sizeOf(macho.compact_unwind_encoding_t);
            if (unwind_info.len < start_offset + page_header.encodingsPageOffset + local_encodings_byte_count) return error.InvalidDebugInfo;
            const local_encodings: []align(1) const macho.compact_unwind_encoding_t = @ptrCast(
                unwind_info[start_offset + page_header.encodingsPageOffset ..][0..local_encodings_byte_count],
            );
            if (local_index >= local_encodings.len) return error.InvalidDebugInfo;
            break :entry .{
                .function_offset = function_offset,
                .raw_encoding = local_encodings[local_index],
            };
        },
        else => return error.InvalidDebugInfo,
    };

    if (entry.raw_encoding == 0) return error.MissingDebugInfo;

    const encoding: macho.CompactUnwindEncoding = @bitCast(entry.raw_encoding);
    const new_ip = switch (builtin.cpu.arch) {
        .x86_64 => switch (encoding.mode.x86_64) {
            .OLD => return error.UnsupportedDebugInfo,
            .RBP_FRAME => ip: {
                const frame = encoding.value.x86_64.frame;

                const fp = (try dwarfRegNative(&context.cpu_state, fp_reg_num)).*;
                const new_sp = fp + 2 * @sizeOf(usize);

                const ip_ptr = fp + @sizeOf(usize);
                const new_ip = @as(*const usize, @ptrFromInt(ip_ptr)).*;
                const new_fp = @as(*const usize, @ptrFromInt(fp)).*;

                (try dwarfRegNative(&context.cpu_state, fp_reg_num)).* = new_fp;
                (try dwarfRegNative(&context.cpu_state, sp_reg_num)).* = new_sp;
                (try dwarfRegNative(&context.cpu_state, ip_reg_num)).* = new_ip;

                const regs: [5]u3 = .{
                    frame.reg0,
                    frame.reg1,
                    frame.reg2,
                    frame.reg3,
                    frame.reg4,
                };
                for (regs, 0..) |reg, i| {
                    if (reg == 0) continue;
                    const addr = fp - frame.frame_offset * @sizeOf(usize) + i * @sizeOf(usize);
                    const reg_number = try Dwarf.compactUnwindToDwarfRegNumber(reg);
                    (try dwarfRegNative(&context.cpu_state, reg_number)).* = @as(*const usize, @ptrFromInt(addr)).*;
                }

                break :ip new_ip;
            },
            .STACK_IMMD,
            .STACK_IND,
            => ip: {
                const frameless = encoding.value.x86_64.frameless;

                const sp = (try dwarfRegNative(&context.cpu_state, sp_reg_num)).*;
                const stack_size: usize = stack_size: {
                    if (encoding.mode.x86_64 == .STACK_IMMD) {
                        break :stack_size @as(usize, frameless.stack.direct.stack_size) * @sizeOf(usize);
                    }
                    // In .STACK_IND, the stack size is inferred from the subq instruction at the beginning of the function.
                    const sub_offset_addr =
                        module.text_base +
                        entry.function_offset +
                        frameless.stack.indirect.sub_offset;
                    // `sub_offset_addr` points to the offset of the literal within the instruction
                    const sub_operand = @as(*align(1) const u32, @ptrFromInt(sub_offset_addr)).*;
                    break :stack_size sub_operand + @sizeOf(usize) * @as(usize, frameless.stack.indirect.stack_adjust);
                };

                // Decode the Lehmer-coded sequence of registers.
                // For a description of the encoding see lib/libc/include/any-macos.13-any/mach-o/compact_unwind_encoding.h

                // Decode the variable-based permutation number into its digits. Each digit represents
                // an index into the list of register numbers that weren't yet used in the sequence at
                // the time the digit was added.
                const reg_count = frameless.stack_reg_count;
                const ip_ptr = ip_ptr: {
                    var digits: [6]u3 = undefined;
                    var accumulator: usize = frameless.stack_reg_permutation;
                    var base: usize = 2;
                    for (0..reg_count) |i| {
                        const div = accumulator / base;
                        digits[digits.len - 1 - i] = @intCast(accumulator - base * div);
                        accumulator = div;
                        base += 1;
                    }

                    var registers: [6]u3 = undefined;
                    var used_indices: [6]bool = @splat(false);
                    for (digits[digits.len - reg_count ..], 0..) |target_unused_index, i| {
                        var unused_count: u8 = 0;
                        const unused_index = for (used_indices, 0..) |used, index| {
                            if (!used) {
                                if (target_unused_index == unused_count) break index;
                                unused_count += 1;
                            }
                        } else unreachable;
                        registers[i] = @intCast(unused_index + 1);
                        used_indices[unused_index] = true;
                    }

                    var reg_addr = sp + stack_size - @sizeOf(usize) * @as(usize, reg_count + 1);
                    for (0..reg_count) |i| {
                        const reg_number = try Dwarf.compactUnwindToDwarfRegNumber(registers[i]);
                        (try dwarfRegNative(&context.cpu_state, reg_number)).* = @as(*const usize, @ptrFromInt(reg_addr)).*;
                        reg_addr += @sizeOf(usize);
                    }

                    break :ip_ptr reg_addr;
                };

                const new_ip = @as(*const usize, @ptrFromInt(ip_ptr)).*;
                const new_sp = ip_ptr + @sizeOf(usize);

                (try dwarfRegNative(&context.cpu_state, sp_reg_num)).* = new_sp;
                (try dwarfRegNative(&context.cpu_state, ip_reg_num)).* = new_ip;

                break :ip new_ip;
            },
            .DWARF => {
                const dwarf = &(unwind.dwarf orelse return error.MissingDebugInfo);
                const rules = try context.computeRules(gpa, dwarf, unwind.vmaddr_slide, encoding.value.x86_64.dwarf);
                return context.next(gpa, &rules);
            },
        },
        .aarch64 => switch (encoding.mode.arm64) {
            .OLD => return error.UnsupportedDebugInfo,
            .FRAMELESS => ip: {
                const sp = (try dwarfRegNative(&context.cpu_state, sp_reg_num)).*;
                const new_sp = sp + encoding.value.arm64.frameless.stack_size * 16;
                const new_ip = (try dwarfRegNative(&context.cpu_state, 30)).*;
                (try dwarfRegNative(&context.cpu_state, sp_reg_num)).* = new_sp;
                break :ip new_ip;
            },
            .DWARF => {
                const dwarf = &(unwind.dwarf orelse return error.MissingDebugInfo);
                const rules = try context.computeRules(gpa, dwarf, unwind.vmaddr_slide, encoding.value.arm64.dwarf);
                return context.next(gpa, &rules);
            },
            .FRAME => ip: {
                const frame = encoding.value.arm64.frame;

                const fp = (try dwarfRegNative(&context.cpu_state, fp_reg_num)).*;
                const ip_ptr = fp + @sizeOf(usize);

                var reg_addr = fp - @sizeOf(usize);
                inline for (@typeInfo(@TypeOf(frame.x_reg_pairs)).@"struct".fields, 0..) |field, i| {
                    if (@field(frame.x_reg_pairs, field.name) != 0) {
                        (try dwarfRegNative(&context.cpu_state, 19 + i)).* = @as(*const usize, @ptrFromInt(reg_addr)).*;
                        reg_addr += @sizeOf(usize);
                        (try dwarfRegNative(&context.cpu_state, 20 + i)).* = @as(*const usize, @ptrFromInt(reg_addr)).*;
                        reg_addr += @sizeOf(usize);
                    }
                }

                // We intentionally skip restoring `frame.d_reg_pairs`; we know we don't support
                // vector registers in the AArch64 `cpu_context` anyway, so there's no reason to
                // fail a legitimate unwind just because we're asked to restore the registers here.
                // If some weird/broken unwind info tells us to read them later, we will fail then.
                reg_addr += 16 * @as(usize, @popCount(@as(u4, @bitCast(frame.d_reg_pairs))));

                const new_ip = @as(*const usize, @ptrFromInt(ip_ptr)).*;
                const new_fp = @as(*const usize, @ptrFromInt(fp)).*;

                (try dwarfRegNative(&context.cpu_state, fp_reg_num)).* = new_fp;
                (try dwarfRegNative(&context.cpu_state, ip_reg_num)).* = new_ip;

                break :ip new_ip;
            },
        },
        else => comptime unreachable, // unimplemented
    };

    const ret_addr = std.debug.stripInstructionPtrAuthCode(new_ip);

    // Like `Dwarf.SelfUnwinder.next`, adjust our next lookup pc in case the `call` was this
    // function's last instruction making `ret_addr` one byte past its end.
    context.pc = ret_addr -| 1;

    return ret_addr;
}

/// Acquires the mutex on success.
fn findModule(si: *SelfInfo, gpa: Allocator, io: Io, address: usize) Error!*Module {
    // This function is marked as deprecated; however, it is significantly more
    // performant than `dladdr` (since the latter also does a very slow symbol
    // lookup), so let's use it since it's still available.
    const text_base = std.c._dyld_get_image_header_containing_address(
        @ptrFromInt(address),
    ) orelse return error.MissingDebugInfo;
    try si.mutex.lock(io);
    errdefer si.mutex.unlock(io);
    const gop = try si.modules.getOrPutAdapted(gpa, @intFromPtr(text_base), Module.Adapter{});
    errdefer comptime unreachable;
    if (!gop.found_existing) gop.key_ptr.* = .{
        .text_base = @intFromPtr(text_base),
        .unwind = null,
        .file = null,
    };
    return gop.key_ptr;
}

const Module = struct {
    text_base: usize,
    unwind: ?(Error!Unwind),
    file: ?(Error!MachOFile),

    const Adapter = struct {
        pub fn hash(_: Adapter, text_base: usize) u32 {
            return @truncate(std.hash.int(text_base));
        }
        pub fn eql(_: Adapter, a_text_base: usize, b_module: Module, b_index: usize) bool {
            _ = b_index;
            return a_text_base == b_module.text_base;
        }
    };
    const Context = struct {
        pub fn hash(_: Context, module: Module) u32 {
            return @truncate(std.hash.int(module.text_base));
        }
        pub fn eql(_: Context, a_module: Module, b_module: Module, b_index: usize) bool {
            _ = b_index;
            return a_module.text_base == b_module.text_base;
        }
    };

    const Unwind = struct {
        /// The slide applied to the `__unwind_info` and `__eh_frame` sections.
        /// So, `unwind_info.ptr` is this many bytes higher than the section's vmaddr.
        vmaddr_slide: u64,
        /// Backed by the in-memory section mapped by the loader.
        unwind_info: ?[]const u8,
        /// Backed by the in-memory `__eh_frame` section mapped by the loader.
        dwarf: ?Dwarf.Unwind,
    };

    fn getUnwindInfo(module: *Module, gpa: Allocator) Error!*Unwind {
        if (module.unwind == null) module.unwind = loadUnwindInfo(module, gpa);
        return if (module.unwind.?) |*unwind| unwind else |err| err;
    }
    fn loadUnwindInfo(module: *const Module, gpa: Allocator) Error!Unwind {
        const header: *std.macho.mach_header_64 = @ptrFromInt(module.text_base);

        const raw_macho: [*]u8 = @ptrCast(header);
        var it = macho.LoadCommandIterator.init(header, raw_macho[@sizeOf(macho.mach_header_64)..][0..header.sizeofcmds]) catch unreachable;
        const sections, const text_vmaddr = while (it.next() catch unreachable) |load_cmd| {
            if (load_cmd.hdr.cmd != .SEGMENT_64) continue;
            const segment_cmd = load_cmd.cast(macho.segment_command_64).?;
            if (!mem.eql(u8, segment_cmd.segName(), "__TEXT")) continue;
            break .{ load_cmd.getSections(), segment_cmd.vmaddr };
        } else unreachable;

        const vmaddr_slide = module.text_base - text_vmaddr;

        var opt_unwind_info: ?[]const u8 = null;
        var opt_eh_frame: ?[]const u8 = null;
        for (sections) |sect| {
            if (mem.eql(u8, sect.sectName(), "__unwind_info")) {
                const sect_ptr: [*]u8 = @ptrFromInt(@as(usize, @intCast(vmaddr_slide + sect.addr)));
                opt_unwind_info = sect_ptr[0..@intCast(sect.size)];
            } else if (mem.eql(u8, sect.sectName(), "__eh_frame")) {
                const sect_ptr: [*]u8 = @ptrFromInt(@as(usize, @intCast(vmaddr_slide + sect.addr)));
                opt_eh_frame = sect_ptr[0..@intCast(sect.size)];
            }
        }
        const eh_frame = opt_eh_frame orelse return .{
            .vmaddr_slide = vmaddr_slide,
            .unwind_info = opt_unwind_info,
            .dwarf = null,
        };
        var dwarf: Dwarf.Unwind = .initSection(.eh_frame, @intFromPtr(eh_frame.ptr) - vmaddr_slide, eh_frame);
        errdefer dwarf.deinit(gpa);
        // We don't need lookups, so this call is just for scanning CIEs.
        dwarf.prepare(gpa, @sizeOf(usize), native_endian, false, true) catch |err| switch (err) {
            error.ReadFailed => unreachable, // it's all fixed buffers
            error.InvalidDebugInfo,
            error.MissingDebugInfo,
            error.OutOfMemory,
            => |e| return e,
            error.EndOfStream,
            error.Overflow,
            error.StreamTooLong,
            error.InvalidOperand,
            error.InvalidOpcode,
            error.InvalidOperation,
            => return error.InvalidDebugInfo,
            error.UnsupportedAddrSize,
            error.UnsupportedDwarfVersion,
            error.UnimplementedUserOpcode,
            => return error.UnsupportedDebugInfo,
        };

        return .{
            .vmaddr_slide = vmaddr_slide,
            .unwind_info = opt_unwind_info,
            .dwarf = dwarf,
        };
    }

    fn getFile(module: *Module, gpa: Allocator, io: Io) Error!*MachOFile {
        if (module.file == null) {
            const path = std.mem.span(
                std.c.dyld_image_path_containing_address(@ptrFromInt(module.text_base)).?,
            );
            module.file = MachOFile.load(gpa, io, path, builtin.cpu.arch) catch |err| switch (err) {
                error.InvalidMachO, error.InvalidDwarf => error.InvalidDebugInfo,
                error.MissingDebugInfo, error.OutOfMemory, error.UnsupportedDebugInfo, error.ReadFailed => |e| e,
            };
        }
        return if (module.file.?) |*f| f else |err| err;
    }
};

const MachoSymbol = struct {
    strx: u32,
    addr: u64,
    /// Value may be `unknown_ofile`.
    ofile: u32,
    const unknown_ofile = std.math.maxInt(u32);
    fn addressLessThan(context: void, lhs: MachoSymbol, rhs: MachoSymbol) bool {
        _ = context;
        return lhs.addr < rhs.addr;
    }
    /// Assumes that `symbols` is sorted in order of ascending `addr`.
    fn find(symbols: []const MachoSymbol, address: usize) ?*const MachoSymbol {
        if (symbols.len == 0) return null; // no potential match
        if (address < symbols[0].addr) return null; // address is before the lowest-address symbol
        var left: usize = 0;
        var len: usize = symbols.len;
        while (len > 1) {
            const mid = left + len / 2;
            if (address < symbols[mid].addr) {
                len /= 2;
            } else {
                left = mid;
                len -= len / 2;
            }
        }
        return &symbols[left];
    }

    test find {
        const symbols: []const MachoSymbol = &.{
            .{ .addr = 100, .strx = undefined, .ofile = undefined },
            .{ .addr = 200, .strx = undefined, .ofile = undefined },
            .{ .addr = 300, .strx = undefined, .ofile = undefined },
        };

        try testing.expectEqual(null, find(symbols, 0));
        try testing.expectEqual(null, find(symbols, 99));
        try testing.expectEqual(&symbols[0], find(symbols, 100).?);
        try testing.expectEqual(&symbols[0], find(symbols, 150).?);
        try testing.expectEqual(&symbols[0], find(symbols, 199).?);

        try testing.expectEqual(&symbols[1], find(symbols, 200).?);
        try testing.expectEqual(&symbols[1], find(symbols, 250).?);
        try testing.expectEqual(&symbols[1], find(symbols, 299).?);

        try testing.expectEqual(&symbols[2], find(symbols, 300).?);
        try testing.expectEqual(&symbols[2], find(symbols, 301).?);
        try testing.expectEqual(&symbols[2], find(symbols, 5000).?);
    }
};
test {
    _ = MachoSymbol;
}

/// Uses `mmap` to map the file at `path` into memory.
fn mapDebugInfoFile(io: Io, path: []const u8) ![]align(std.heap.page_size_min) const u8 {
    const file = Io.Dir.cwd().openFile(io, path, .{}) catch |err| switch (err) {
        error.FileNotFound => return error.MissingDebugInfo,
        else => return error.ReadFailed,
    };
    defer file.close(io);

    const file_end_pos = file.length(io) catch |err| switch (err) {
        error.Unexpected => |e| return e,
        else => return error.ReadFailed,
    };
    const file_len = std.math.cast(usize, file_end_pos) orelse return error.InvalidDebugInfo;

    return posix.mmap(
        null,
        file_len,
        .{ .READ = true },
        .{ .TYPE = .SHARED },
        file.handle,
        0,
    ) catch |err| switch (err) {
        error.Unexpected => |e| return e,
        else => return error.ReadFailed,
    };
}

const std = @import("std");
const Io = std.Io;
const Allocator = std.mem.Allocator;
const Dwarf = std.debug.Dwarf;
const Error = std.debug.SelfInfoError;
const MachOFile = std.debug.MachOFile;
const assert = std.debug.assert;
const posix = std.posix;
const macho = std.macho;
const mem = std.mem;
const testing = std.testing;
const dwarfRegNative = std.debug.Dwarf.SelfUnwinder.regNative;

const builtin = @import("builtin");
const native_endian = builtin.target.cpu.arch.endian();

const SelfInfo = @This();



---
File: /std/debug/SelfInfo/Windows.zig
---

lock: Io.RwLock,
ntdll_handle: ?if (load_dll_notification_procs) *anyopaque else noreturn,
notification_cookie: ?LDR.DLL_NOTIFICATION.COOKIE,
modules: std.ArrayList(Module),

pub const init: SelfInfo = .{
    .lock = .init,
    .ntdll_handle = null,
    .notification_cookie = null,
    .modules = .empty,
};
pub fn deinit(si: *SelfInfo, io: Io) void {
    const gpa = std.debug.getDebugInfoAllocator();
    if (si.notification_cookie) |cookie| unregister: {
        switch ((si.getNtdllProc(.LdrUnregisterDllNotification) catch break :unregister)(cookie)) {
            .SUCCESS => {},
            else => |status| windows.unexpectedStatus(status) catch break :unregister,
        }
    }
    if (si.ntdll_handle) |handle| switch (windows.ntdll.LdrUnloadDll(handle)) {
        .SUCCESS => {},
        else => |status| windows.unexpectedStatus(status) catch {},
    };
    for (si.modules.items) |*module| module.deinit(gpa, io);
    si.modules.deinit(gpa);
}

pub fn getSymbols(
    si: *SelfInfo,
    io: Io,
    symbol_allocator: Allocator,
    text_arena: Allocator,
    address: usize,
    resolve_inline_callers: bool,
    symbols: *std.ArrayList(std.debug.Symbol),
) Error!void {
    const gpa = std.debug.getDebugInfoAllocator();
    try si.lock.lockShared(io);
    defer si.lock.unlockShared(io);
    const module = try si.findModule(gpa, address);
    const di = try module.getDebugInfo(gpa, io);
    return di.getSymbols(
        symbol_allocator,
        text_arena,
        address - @intFromPtr(module.entry.DllBase),
        resolve_inline_callers,
        symbols,
    );
}

pub fn getModuleName(si: *SelfInfo, io: Io, address: usize) Error![]const u8 {
    const gpa = std.debug.getDebugInfoAllocator();
    try si.lock.lockShared(io);
    defer si.lock.unlockShared(io);
    const module = try si.findModule(gpa, address);
    return module.name orelse {
        const name = try std.unicode.wtf16LeToWtf8Alloc(gpa, module.entry.BaseDllName.slice());
        module.name = name;
        return name;
    };
}
pub fn getModuleSlide(si: *SelfInfo, io: Io, address: usize) Error!usize {
    const gpa = std.debug.getDebugInfoAllocator();
    try si.lock.lockShared(io);
    defer si.lock.unlockShared(io);
    const module = try si.findModule(gpa, address);
    return module.base_address;
}

pub const can_unwind: bool = switch (builtin.cpu.arch) {
    else => true,
    // On x86, `RtlVirtualUnwind` does not exist. We could in theory use `RtlCaptureStackBackTrace`
    // instead, but on x86, it turns out that function is just... doing FP unwinding with esp! It's
    // hard to find implementation details to confirm that, but the most authoritative source I have
    // is an entry in the LLVM mailing list from 2020/08/16 which contains this quote:
    //
    // > x86 doesn't have what most architectures would consider an "unwinder" in the sense of
    // > restoring registers; there is simply a linked list of frames that participate in SEH and
    // > that desire to be called for a dynamic unwind operation, so RtlCaptureStackBackTrace
    // > assumes that EBP-based frames are in use and walks an EBP-based frame chain on x86 - not
    // > all x86 code is written with EBP-based frames so while even though we generally build the
    // > OS that way, you might always run the risk of encountering external code that uses EBP as a
    // > general purpose register for which such an unwind attempt for a stack trace would fail.
    //
    // Regardless, it's easy to effectively confirm this hypothesis just by compiling some code with
    // `-fomit-frame-pointer -OReleaseFast` and observing that `RtlCaptureStackBackTrace` returns an
    // empty trace when it's called in such an application. Note that without `-OReleaseFast` or
    // similar, LLVM seems reluctant to ever clobber ebp, so you'll get a trace returned which just
    // contains all of the kernel32/ntdll frames but none of your own. Don't be deceived---this is
    // just coincidental!
    //
    // Anyway, the point is, the only stack walking primitive on x86-windows is FP unwinding. We
    // *could* ask Microsoft to do that for us with `RtlCaptureStackBackTrace`... but better to just
    // use our existing FP unwinder in `std.debug`!
    .x86 => false,
};
pub const UnwindContext = struct {
    pc: usize,
    cur: windows.CONTEXT,
    history_table: windows.UNWIND_HISTORY_TABLE,
    pub fn init(ctx: *const std.debug.cpu_context.Native) UnwindContext {
        return .{
            .pc = @returnAddress(),
            .cur = switch (builtin.cpu.arch) {
                .x86_64 => std.mem.zeroInit(windows.CONTEXT, .{
                    .Rax = ctx.gprs.get(.rax),
                    .Rcx = ctx.gprs.get(.rcx),
                    .Rdx = ctx.gprs.get(.rdx),
                    .Rbx = ctx.gprs.get(.rbx),
                    .Rsp = ctx.gprs.get(.rsp),
                    .Rbp = ctx.gprs.get(.rbp),
                    .Rsi = ctx.gprs.get(.rsi),
                    .Rdi = ctx.gprs.get(.rdi),
                    .R8 = ctx.gprs.get(.r8),
                    .R9 = ctx.gprs.get(.r9),
                    .R10 = ctx.gprs.get(.r10),
                    .R11 = ctx.gprs.get(.r11),
                    .R12 = ctx.gprs.get(.r12),
                    .R13 = ctx.gprs.get(.r13),
                    .R14 = ctx.gprs.get(.r14),
                    .R15 = ctx.gprs.get(.r15),
                    .Rip = ctx.gprs.get(.rip),
                }),
                .aarch64 => .{
                    .ContextFlags = 0,
                    .Cpsr = 0,
                    .DUMMYUNIONNAME = .{ .X = ctx.x },
                    .Sp = ctx.sp,
                    .Pc = ctx.pc,
                    .V = @splat(.{ .B = @splat(0) }),
                    .Fpcr = 0,
                    .Fpsr = 0,
                    .Bcr = @splat(0),
                    .Bvr = @splat(0),
                    .Wcr = @splat(0),
                    .Wvr = @splat(0),
                },
                .thumb => .{
                    .ContextFlags = 0,
                    .R0 = ctx.r[0],
                    .R1 = ctx.r[1],
                    .R2 = ctx.r[2],
                    .R3 = ctx.r[3],
                    .R4 = ctx.r[4],
                    .R5 = ctx.r[5],
                    .R6 = ctx.r[6],
                    .R7 = ctx.r[7],
                    .R8 = ctx.r[8],
                    .R9 = ctx.r[9],
                    .R10 = ctx.r[10],
                    .R11 = ctx.r[11],
                    .R12 = ctx.r[12],
                    .Sp = ctx.r[13],
                    .Lr = ctx.r[14],
                    .Pc = ctx.r[15],
                    .Cpsr = 0,
                    .Fpcsr = 0,
                    .Padding = 0,
                    .DUMMYUNIONNAME = .{ .S = @splat(0) },
                    .Bvr = @splat(0),
                    .Bcr = @splat(0),
                    .Wvr = @splat(0),
                    .Wcr = @splat(0),
                    .Padding2 = @splat(0),
                },
                else => comptime unreachable,
            },
            .history_table = std.mem.zeroes(windows.UNWIND_HISTORY_TABLE),
        };
    }
    pub fn deinit(ctx: *UnwindContext) void {
        _ = ctx;
    }
    pub fn getFp(ctx: *UnwindContext) usize {
        return ctx.cur.getRegs().bp;
    }
};
pub fn unwindFrame(si: *SelfInfo, io: Io, context: *UnwindContext) Error!usize {
    _ = si;
    _ = io;

    const current_regs = context.cur.getRegs();
    var image_base: usize = undefined;
    if (windows.ntdll.RtlLookupFunctionEntry(current_regs.ip, &image_base, &context.history_table)) |runtime_function| {
        var handler_data: ?*anyopaque = null;
        var establisher_frame: usize = undefined;
        _ = windows.ntdll.RtlVirtualUnwind(
            windows.UNW_FLAG_NHANDLER,
            image_base,
            current_regs.ip,
            runtime_function,
            &context.cur,
            &handler_data,
            &establisher_frame,
            null,
        );
    } else {
        // leaf function
        context.cur.setIp(@as(*const usize, @ptrFromInt(current_regs.sp)).*);
        context.cur.setSp(current_regs.sp + @sizeOf(usize));
    }

    const next_regs = context.cur.getRegs();
    const tib = &windows.teb().NtTib;
    if (next_regs.sp < @intFromPtr(tib.StackLimit) or next_regs.sp > @intFromPtr(tib.StackBase)) {
        context.pc = 0;
        return 0;
    }
    // Like `DwarfUnwindContext.unwindFrame`, adjust our next lookup pc in case the `call` was this
    // function's last instruction making `next_regs.ip` one byte past its end.
    context.pc = next_regs.ip -| 1;
    return next_regs.ip;
}

const Module = struct {
    entry: *const LDR.DATA_TABLE_ENTRY,
    name: ?[]const u8,
    di: ?(Error!DebugInfo),

    const DebugInfo = struct {
        arena: std.heap.ArenaAllocator.State,
        coff_image_base: u64,
        mapped_file: ?MappedFile,
        dwarf: ?Dwarf,
        pdb: ?Pdb,
        coff_section_headers: []coff.SectionHeader,

        const MappedFile = struct {
            file: Io.File,
            section_handle: windows.HANDLE,
            section_view: []const u8,
            fn deinit(mf: *const MappedFile, io: Io) void {
                const process_handle = windows.GetCurrentProcess();
                switch (windows.ntdll.NtUnmapViewOfSection(
                    process_handle,
                    @constCast(mf.section_view.ptr),
                )) {
                    .SUCCESS => {},
                    else => |status| windows.unexpectedStatus(status) catch {},
                }
                windows.CloseHandle(mf.section_handle);
                mf.file.close(io);
            }
        };

        fn deinit(di: *DebugInfo, gpa: Allocator, io: Io) void {
            if (di.dwarf) |*dwarf| dwarf.deinit(gpa);
            if (di.pdb) |*pdb| {
                pdb.file_reader.file.close(io);
                pdb.deinit();
            }
            if (di.mapped_file) |*mf| mf.deinit(io);

            var arena = di.arena.promote(gpa);
            arena.deinit();
        }

        fn getSymbols(
            di: *DebugInfo,
            symbol_allocator: Allocator,
            text_arena: Allocator,
            vaddr: usize,
            resolve_inline_callers: bool,
            symbols: *std.ArrayList(std.debug.Symbol),
        ) Error!void {
            pdb: {
                const pdb = &(di.pdb orelse break :pdb);
                var coff_section: *align(1) const coff.SectionHeader = undefined;
                const mod_index = for (pdb.sect_contribs) |sect_contrib| {
                    if (sect_contrib.section > di.coff_section_headers.len) continue;
                    // Remember that SectionContribEntry.Section is 1-based.
                    coff_section = &di.coff_section_headers[sect_contrib.section - 1];

                    const vaddr_start = coff_section.virtual_address + sect_contrib.offset;
                    const vaddr_end = vaddr_start + sect_contrib.size;
                    if (vaddr >= vaddr_start and vaddr < vaddr_end) {
                        break sect_contrib.module_index;
                    }
                } else {
                    // we have no information to add to the address
                    break :pdb;
                };
                const module = pdb.getModule(mod_index) catch |err| switch (err) {
                    error.InvalidDebugInfo,
                    error.MissingDebugInfo,
                    error.OutOfMemory,
                    => |e| return e,

                    error.ReadFailed,
                    error.EndOfStream,
                    => return error.InvalidDebugInfo,
                } orelse {
                    return error.InvalidDebugInfo; // bad module index
                };

                const addr = vaddr - coff_section.virtual_address;
                const maybe_proc = pdb.getProcSym(module, addr);
                const compile_unit_name = fs.path.basename(module.obj_file_name);
                const symbols_top = symbols.items.len;
                if (maybe_proc) |proc| {
                    const offset_in_func = addr - proc.code_offset;
                    var last_inlinee: ?u32 = null;
                    var iter = pdb.getInlinees(module, proc);
                    while (iter.next(module)) |inline_site| {
                        // Filter out duplicate inline sites. Tools like llvm-addr2line output
                        // duplicate sites in the same cases as us if we elide this check,
                        // implying that they exist in the underlying data and are not indicative
                        // of a parser bug. No useful information is lost here since an inline site
                        // can't actually reference itself.
                        if (inline_site.inlinee == last_inlinee) continue;

                        // If our address points into this site, get the source location(s) it
                        // points at
                        for (pdb.getInlineeSourceLines(
                            module,
                            inline_site.inlinee,
                        )) |inlinee_src_line| {
                            const maybe_loc = pdb.getInlineSiteSourceLocation(
                                text_arena,
                                module,
                                inline_site,
                                inlinee_src_line.info,
                                offset_in_func,
                            ) catch continue;
                            const loc = maybe_loc orelse continue;

                            // If we aren't trying to resolve inline callers, and we've matched a
                            // new inline site, we want to overwrite the previously appended
                            // results.
                            if (!resolve_inline_callers and inline_site.inlinee != last_inlinee) {
                                symbols.items.len = symbols_top;
                            }

                            // Only resolve the name if we're resolving inline callers, otherwise
                            // wait until we're done to avoid duplicated work.
                            const name = if (resolve_inline_callers)
                                pdb.findInlineeName(inline_site.inlinee)
                            else
                                null;

                            try symbols.append(symbol_allocator, .{
                                .name = name,
                                .compile_unit_name = compile_unit_name,
                                .source_location = loc,
                            });

                            last_inlinee = inline_site.inlinee;
                        }
                    }

                    if (resolve_inline_callers) {
                        // Inline sites are stored in the pdb in reverse order, so we reverse the
                        // matching sites here. We could alternatively use the parent fields to
                        // determine the order, but this would introduce seemingly unecessary
                        // complexity.
                        std.mem.reverse(std.debug.Symbol, symbols.items);
                    } else if (last_inlinee) |inlinee| {
                        // If we aren't resolving inline callers, then all results will have the
                        // same inline site, and we resolve its name once at the end.
                        const name = pdb.findInlineeName(inlinee);
                        for (symbols.items) |*symbol| symbol.name = name;
                    }
                }

                // If there's room for another symbol, add the actual proc
                if (resolve_inline_callers or symbols.items.len == 0) {
                    try symbols.append(symbol_allocator, .{
                        .name = if (maybe_proc) |proc| pdb.getSymbolName(proc) else null,
                        .compile_unit_name = compile_unit_name,
                        .source_location = pdb.getLineNumberInfo(text_arena, module, addr) catch null,
                    });
                }

                return;
            }

            dwarf: {
                const dwarf = &(di.dwarf orelse break :dwarf);
                const addr = vaddr + di.coff_image_base;
                return dwarf.getSymbols(
                    symbol_allocator,
                    text_arena,
                    native_endian,
                    addr,
                    resolve_inline_callers,
                    symbols,
                );
            }

            return error.MissingDebugInfo;
        }
    };

    fn deinit(module: *Module, gpa: Allocator, io: Io) void {
        if (module.name) |name| gpa.free(name);
        if (module.di) |*di_or_err| if (di_or_err.*) |*di| di.deinit(gpa, io) else |_| {};
        module.* = undefined;
    }

    fn getDebugInfo(module: *Module, gpa: Allocator, io: Io) Error!*DebugInfo {
        if (module.di == null) module.di = loadDebugInfo(module, gpa, io);
        return if (module.di.?) |*di| di else |err| err;
    }
    fn loadDebugInfo(module: *const Module, gpa: Allocator, io: Io) Error!DebugInfo {
        const mapped_ptr: [*]const u8 = @ptrCast(module.entry.DllBase);
        const mapped = mapped_ptr[0..module.entry.SizeOfImage];
        var coff_obj = coff.Coff.init(mapped, true) catch return error.InvalidDebugInfo;

        var arena_instance: std.heap.ArenaAllocator = .init(gpa);
        errdefer arena_instance.deinit();
        const arena = arena_instance.allocator();

        // The string table is not mapped into memory by the loader, so if a section name is in the
        // string table then we have to map the full image file from disk. This can happen when
        // a binary is produced with -gdwarf, since the section names are longer than 8 bytes.
        const mapped_file: ?DebugInfo.MappedFile = mapped: {
            if (!coff_obj.strtabRequired()) break :mapped null;
            var path_buffer: [4 + windows.PATH_MAX_WIDE]u16 = undefined;
            path_buffer[0..4].* = .{ '\\', '?', '?', '\\' }; // openFileAbsoluteW requires the prefix to be present
            const path_slice = module.entry.FullDllName.slice();
            @memcpy(path_buffer[4..][0..path_slice.len], path_slice);
            const coff_file = Io.Threaded.dirOpenFileWtf16(
                null,
                path_buffer[0 .. 4 + path_slice.len],
                .{},
            ) catch |err| switch (err) {
                error.Canceled => |e| return e,
                error.Unexpected => |e| return e,
                error.FileNotFound => return error.MissingDebugInfo,

                error.FileTooBig,
                error.IsDir,
                error.NotDir,
                error.SymLinkLoop,
                error.NameTooLong,
                error.BadPathName,
                => return error.InvalidDebugInfo,

                error.SystemResources,
                error.WouldBlock,
                error.AccessDenied,
                error.PermissionDenied,
                error.NoSpaceLeft,
                error.DeviceBusy,
                error.NoDevice,
                error.PathAlreadyExists,
                error.PipeBusy,
                error.NetworkNotFound,
                error.AntivirusInterference,
                error.ProcessFdQuotaExceeded,
                error.SystemFdQuotaExceeded,
                error.FileLocksUnsupported,
                error.FileBusy,
                error.ReadOnlyFileSystem,
                => return error.ReadFailed,
            };
            errdefer coff_file.close(io);
            var section_handle: windows.HANDLE = undefined;
            const create_section_rc = windows.ntdll.NtCreateSection(
                &section_handle,
                .{
                    .SPECIFIC = .{ .SECTION = .{
                        .QUERY = true,
                        .MAP_READ = true,
                    } },
                    .STANDARD = .{ .RIGHTS = .REQUIRED },
                },
                null,
                null,
                .{ .READONLY = true },
                // The documentation states that if no AllocationAttribute is specified,
                // then SEC_COMMIT is the default.
                // In practice, this isn't the case and specifying 0 will result in INVALID_PARAMETER_6.
                .{ .COMMIT = true },
                coff_file.handle,
            );
            if (create_section_rc != .SUCCESS) return error.MissingDebugInfo;
            errdefer windows.CloseHandle(section_handle);
            var coff_len: usize = 0;
            var section_view_ptr: ?[*]const u8 = null;
            const process_handle = windows.GetCurrentProcess();
            const map_section_rc = windows.ntdll.NtMapViewOfSection(
                section_handle,
                process_handle,
                @ptrCast(&section_view_ptr),
                null,
                0,
                null,
                &coff_len,
                .Unmap,
                .{},
                .{ .READONLY = true },
            );
            if (map_section_rc != .SUCCESS) return error.MissingDebugInfo;
            errdefer switch (windows.ntdll.NtUnmapViewOfSection(
                process_handle,
                @constCast(section_view_ptr.?),
            )) {
                .SUCCESS => {},
                else => |status| windows.unexpectedStatus(status) catch {},
            };
            const section_view = section_view_ptr.?[0..coff_len];
            coff_obj = coff.Coff.init(section_view, false) catch return error.InvalidDebugInfo;
            break :mapped .{
                .file = coff_file,
                .section_handle = section_handle,
                .section_view = section_view,
            };
        };
        errdefer if (mapped_file) |*mf| mf.deinit(io);

        const coff_image_base = coff_obj.getImageBase();

        var opt_dwarf: ?Dwarf = dwarf: {
            if (coff_obj.getSectionByName(".debug_info") == null) break :dwarf null;

            var sections: Dwarf.SectionArray = undefined;
            inline for (@typeInfo(Dwarf.Section.Id).@"enum".fields, 0..) |section, i| {
                sections[i] = if (coff_obj.getSectionByName("." ++ section.name)) |section_header| .{
                    .data = try coff_obj.getSectionDataAlloc(section_header, arena),
                    .owned = false,
                } else null;
            }
            break :dwarf .{ .sections = sections };
        };
        errdefer if (opt_dwarf) |*dwarf| dwarf.deinit(gpa);

        if (opt_dwarf) |*dwarf| {
            dwarf.open(gpa, native_endian) catch |err| switch (err) {
                error.Overflow,
                error.EndOfStream,
                error.StreamTooLong,
                error.ReadFailed,
                => return error.InvalidDebugInfo,

                error.InvalidDebugInfo,
                error.MissingDebugInfo,
                error.OutOfMemory,
                => |e| return e,
            };
        }

        var opt_pdb: ?Pdb = pdb: {
            const path = coff_obj.getPdbPath() catch {
                return error.InvalidDebugInfo;
            } orelse {
                break :pdb null;
            };
            const pdb_file_open_result = if (fs.path.isAbsolute(path)) res: {
                break :res Io.Dir.cwd().openFile(io, path, .{});
            } else res: {
                const self_dir = std.process.executableDirPathAlloc(io, gpa) catch |err| switch (err) {
                    error.OutOfMemory, error.Unexpected => |e| return e,
                    else => return error.ReadFailed,
                };
                defer gpa.free(self_dir);
                const abs_path = try fs.path.join(gpa, &.{ self_dir, path });
                defer gpa.free(abs_path);
                break :res Io.Dir.cwd().openFile(io, abs_path, .{});
            };
            const pdb_file = pdb_file_open_result catch |err| switch (err) {
                error.FileNotFound, error.IsDir => break :pdb null,
                else => return error.ReadFailed,
            };
            errdefer pdb_file.close(io);

            const pdb_reader = try arena.create(Io.File.Reader);
            pdb_reader.* = pdb_file.reader(io, try arena.alloc(u8, 4096));

            var pdb = Pdb.init(gpa, pdb_reader) catch |err| switch (err) {
                error.OutOfMemory, error.ReadFailed, error.Unexpected => |e| return e,
                else => return error.InvalidDebugInfo,
            };
            errdefer pdb.deinit();
            pdb.parseInfoStream() catch |err| switch (err) {
                error.UnknownPDBVersion => return error.UnsupportedDebugInfo,
                error.EndOfStream => return error.InvalidDebugInfo,

                error.InvalidDebugInfo,
                error.MissingDebugInfo,
                error.OutOfMemory,
                error.ReadFailed,
                => |e| return e,
            };
            pdb.parseDbiStream() catch |err| switch (err) {
                error.UnknownPDBVersion => return error.UnsupportedDebugInfo,

                error.EndOfStream,
                error.EOF,
                error.StreamTooLong,
                error.WriteFailed,
                => return error.InvalidDebugInfo,

                error.InvalidDebugInfo,
                error.OutOfMemory,
                error.ReadFailed,
                => |e| return e,
            };
            pdb.parseIpiStream() catch |err| switch (err) {
                error.UnknownPDBVersion => return error.UnsupportedDebugInfo,

                error.EndOfStream,
                => return error.InvalidDebugInfo,

                error.OutOfMemory,
                error.ReadFailed,
                => |e| return e,
            };

            if (!std.mem.eql(u8, &coff_obj.guid, &pdb.guid) or coff_obj.age != pdb.age)
                return error.InvalidDebugInfo;

            break :pdb pdb;
        };
        errdefer if (opt_pdb) |*pdb| {
            pdb.file_reader.file.close(io);
            pdb.deinit();
        };

        const coff_section_headers: []coff.SectionHeader = if (opt_pdb != null) csh: {
            break :csh try coff_obj.getSectionHeadersAlloc(arena);
        } else &.{};

        return .{
            .arena = arena_instance.state,
            .coff_image_base = coff_image_base,
            .mapped_file = mapped_file,
            .dwarf = opt_dwarf,
            .pdb = opt_pdb,
            .coff_section_headers = coff_section_headers,
        };
    }
};

/// Assumes we already hold `si.lock`.
fn findModule(si: *SelfInfo, gpa: Allocator, address: usize) error{ MissingDebugInfo, OutOfMemory, Unexpected }!*Module {
    for (si.modules.items) |*mod| {
        const base = @intFromPtr(mod.entry.DllBase);
        if (address >= base and address < base + mod.entry.SizeOfImage) return mod;
    }
    try si.modules.ensureUnusedCapacity(gpa, 1);
    var entry: *LDR.DATA_TABLE_ENTRY = undefined;
    switch (windows.ntdll.LdrFindEntryForAddress(@ptrFromInt(address), &entry)) {
        .SUCCESS => {},
        .DLL_NOT_FOUND => return error.MissingDebugInfo,
        else => |status| return windows.unexpectedStatus(status),
    }
    if (si.notification_cookie == null) {
        var notification_cookie: LDR.DLL_NOTIFICATION.COOKIE = undefined;
        switch ((try si.getNtdllProc(.LdrRegisterDllNotification))(
            .{},
            &dllNotification,
            si,
            &notification_cookie,
        )) {
            .SUCCESS => si.notification_cookie = notification_cookie,
            else => |status| return windows.unexpectedStatus(status),
        }
    }
    const mod = si.modules.addOneAssumeCapacity();
    mod.* = .{ .entry = entry, .name = null, .di = null };
    return mod;
}

inline fn getNtdllProc(
    si: *SelfInfo,
    comptime proc: std.meta.DeclEnum(windows.ntdll),
) !@TypeOf(&@field(windows.ntdll, @tagName(proc))) {
    return if (load_dll_notification_procs)
        @ptrCast(try si.loadNtdllProc(@tagName(proc)))
    else
        &@field(windows.ntdll, @tagName(proc));
}
fn loadNtdllProc(si: *SelfInfo, name: []const u8) Io.UnexpectedError!*anyopaque {
    const ntdll_handle = si.ntdll_handle orelse ntdll_handle: {
        var ntdll_handle: *anyopaque = undefined;
        switch (windows.ntdll.LdrLoadDll(null, null, &.init(
            &.{ 'n', 't', 'd', 'l', 'l', '.', 'd', 'l', 'l' },
        ), &ntdll_handle)) {
            .SUCCESS => {},
            .DLL_NOT_FOUND => return error.Unexpected,
            else => |status| return windows.unexpectedStatus(status),
        }
        si.ntdll_handle = ntdll_handle;
        break :ntdll_handle ntdll_handle;
    };
    var proc_addr: *anyopaque = undefined;
    switch (windows.ntdll.LdrGetProcedureAddress(ntdll_handle, &.init(name), 0, &proc_addr)) {
        .SUCCESS => {},
        else => |status| return windows.unexpectedStatus(status),
    }
    return proc_addr;
}

fn dllNotification(
    reason: LDR.DLL_NOTIFICATION.REASON,
    data: *const LDR.DLL_NOTIFICATION.DATA,
    context: ?*anyopaque,
) callconv(.winapi) void {
    const si: *SelfInfo = @ptrCast(@alignCast(context));
    switch (reason) {
        .LOADED => {},
        .UNLOADED => {
            const io = std.Options.debug_io;
            si.lock.lockUncancelable(io);
            defer si.lock.unlock(io);
            for (si.modules.items, 0..) |*mod, mod_index| {
                if (mod.entry.DllBase != data.Unloaded.DllBase) continue;
                mod.deinit(std.debug.getDebugInfoAllocator(), io);
                _ = si.modules.swapRemove(mod_index);
                break;
            }
        },
    }
}

const std = @import("std");
const Io = std.Io;
const Allocator = std.mem.Allocator;
const Dwarf = std.debug.Dwarf;
const Pdb = std.debug.Pdb;
const Error = std.debug.SelfInfoError;
const coff = std.coff;
const fs = std.fs;
const windows = std.os.windows;
const LDR = windows.LDR;

const builtin = @import("builtin");
const native_endian = builtin.target.cpu.arch.endian();
const load_dll_notification_procs = builtin.abi == .msvc and switch (builtin.zig_backend) {
    .stage2_c => true,
    else => switch (builtin.output_mode) {
        .Exe => false,
        .Lib => switch (builtin.link_mode) {
            .static => true,
            .dynamic => false,
        },
        .Obj => true,
    },
};

const SelfInfo = @This();



---
File: /std/debug/Coverage.zig
---

const Coverage = @This();

const std = @import("../std.zig");
const Io = std.Io;
const Allocator = std.mem.Allocator;
const Hash = std.hash.Wyhash;
const Dwarf = std.debug.Dwarf;
const assert = std.debug.assert;

/// Provides a globally-scoped integer index for directories.
///
/// As opposed to, for example, a directory index that is compilation-unit
/// scoped inside a single ELF module.
///
/// String memory references the memory-mapped debug information.
///
/// Protected by `mutex`.
directories: std.ArrayHashMapUnmanaged(String, void, String.MapContext, false),
/// Provides a globally-scoped integer index for files.
///
/// String memory references the memory-mapped debug information.
///
/// Protected by `mutex`.
files: std.ArrayHashMapUnmanaged(File, void, File.MapContext, false),
string_bytes: std.ArrayList(u8),
/// Protects the other fields.
mutex: Io.Mutex,

pub const init: Coverage = .{
    .directories = .empty,
    .files = .empty,
    .mutex = .init,
    .string_bytes = .empty,
};

pub const String = enum(u32) {
    _,

    pub const MapContext = struct {
        string_bytes: []const u8,

        pub fn eql(self: @This(), a: String, b: String, b_index: usize) bool {
            _ = b_index;
            const a_slice = span(self.string_bytes[@intFromEnum(a)..]);
            const b_slice = span(self.string_bytes[@intFromEnum(b)..]);
            return std.mem.eql(u8, a_slice, b_slice);
        }

        pub fn hash(self: @This(), a: String) u32 {
            return @truncate(Hash.hash(0, span(self.string_bytes[@intFromEnum(a)..])));
        }
    };

    pub const SliceAdapter = struct {
        string_bytes: []const u8,

        pub fn eql(self: @This(), a_slice: []const u8, b: String, b_index: usize) bool {
            _ = b_index;
            const b_slice = span(self.string_bytes[@intFromEnum(b)..]);
            return std.mem.eql(u8, a_slice, b_slice);
        }
        pub fn hash(self: @This(), a: []const u8) u32 {
            _ = self;
            return @truncate(Hash.hash(0, a));
        }
    };
};

pub const SourceLocation = extern struct {
    file: File.Index,
    line: u32,
    column: u32,

    pub const invalid: SourceLocation = .{
        .file = .invalid,
        .line = 0,
        .column = 0,
    };
};

pub const File = extern struct {
    directory_index: u32,
    basename: String,

    pub const Index = enum(u32) {
        invalid = std.math.maxInt(u32),
        _,
    };

    pub const MapContext = struct {
        string_bytes: []const u8,

        pub fn hash(self: MapContext, a: File) u32 {
            const a_basename = span(self.string_bytes[@intFromEnum(a.basename)..]);
            return @truncate(Hash.hash(a.directory_index, a_basename));
        }

        pub fn eql(self: MapContext, a: File, b: File, b_index: usize) bool {
            _ = b_index;
            if (a.directory_index != b.directory_index) return false;
            const a_basename = span(self.string_bytes[@intFromEnum(a.basename)..]);
            const b_basename = span(self.string_bytes[@intFromEnum(b.basename)..]);
            return std.mem.eql(u8, a_basename, b_basename);
        }
    };

    pub const SliceAdapter = struct {
        string_bytes: []const u8,

        pub const Entry = struct {
            directory_index: u32,
            basename: []const u8,
        };

        pub fn hash(self: @This(), a: Entry) u32 {
            _ = self;
            return @truncate(Hash.hash(a.directory_index, a.basename));
        }

        pub fn eql(self: @This(), a: Entry, b: File, b_index: usize) bool {
            _ = b_index;
            if (a.directory_index != b.directory_index) return false;
            const b_basename = span(self.string_bytes[@intFromEnum(b.basename)..]);
            return std.mem.eql(u8, a.basename, b_basename);
        }
    };
};

pub fn deinit(cov: *Coverage, gpa: Allocator) void {
    cov.directories.deinit(gpa);
    cov.files.deinit(gpa);
    cov.string_bytes.deinit(gpa);
    cov.* = undefined;
}

pub fn fileAt(cov: *Coverage, index: File.Index) *File {
    return &cov.files.keys()[@intFromEnum(index)];
}

pub fn stringAt(cov: *Coverage, index: String) [:0]const u8 {
    return span(cov.string_bytes.items[@intFromEnum(index)..]);
}

pub const ResolveAddressesDwarfError = Dwarf.ScanError || Io.Cancelable;

pub fn resolveAddressesDwarf(
    cov: *Coverage,
    gpa: Allocator,
    io: Io,
    endian: std.builtin.Endian,
    /// Asserts the addresses are in ascending order.
    sorted_pc_addrs: []const u64,
    /// Asserts its length equals length of `sorted_pc_addrs`.
    output: []SourceLocation,
    d: *Dwarf,
) ResolveAddressesDwarfError!void {
    assert(sorted_pc_addrs.len == output.len);
    assert(d.ranges.items.len != 0); // call `populateRanges` first.

    var range_i: usize = 0;
    var range: *std.debug.Dwarf.Range = &d.ranges.items[0];
    var line_table_i: usize = undefined;
    var prev_pc: u64 = 0;
    var prev_cu: ?*std.debug.Dwarf.CompileUnit = null;
    // Protects directories and files tables from other threads.
    try cov.mutex.lock(io);
    defer cov.mutex.unlock(io);
    next_pc: for (sorted_pc_addrs, output) |pc, *out| {
        assert(pc >= prev_pc);
        prev_pc = pc;

        while (pc >= range.end) {
            range_i += 1;
            if (range_i >= d.ranges.items.len) {
                out.* = SourceLocation.invalid;
                continue :next_pc;
            }
            range = &d.ranges.items[range_i];
        }
        if (pc < range.start) {
            out.* = SourceLocation.invalid;
            continue :next_pc;
        }
        const cu = &d.compile_unit_list.items[range.compile_unit_index];
        if (cu != prev_cu) {
            prev_cu = cu;
            if (cu.src_loc_cache == null) {
                cov.mutex.unlock(io);
                defer cov.mutex.lockUncancelable(io);
                d.populateSrcLocCache(gpa, endian, cu) catch |err| switch (err) {
                    error.MissingDebugInfo, error.InvalidDebugInfo => {
                        out.* = SourceLocation.invalid;
                        continue :next_pc;
                    },
                    else => |e| return e,
                };
            }
            const slc = &cu.src_loc_cache.?;
            const table_addrs = slc.line_table.keys();
            line_table_i = std.sort.upperBound(u64, table_addrs, pc, struct {
                fn order(context: u64, item: u64) std.math.Order {
                    return std.math.order(context, item);
                }
            }.order);
        }
        const slc = &cu.src_loc_cache.?;
        const table_addrs = slc.line_table.keys();
        while (line_table_i < table_addrs.len and table_addrs[line_table_i] <= pc) line_table_i += 1;

        const entry = slc.line_table.values()[line_table_i - 1];
        const corrected_file_index = entry.file - @intFromBool(slc.version < 5);
        const file_entry = slc.files[corrected_file_index];
        const dir_path = slc.directories[file_entry.dir_index].path;
        try cov.string_bytes.ensureUnusedCapacity(gpa, dir_path.len + file_entry.path.len + 2);
        const dir_gop = try cov.directories.getOrPutContextAdapted(gpa, dir_path, String.SliceAdapter{
            .string_bytes = cov.string_bytes.items,
        }, String.MapContext{
            .string_bytes = cov.string_bytes.items,
        });
        if (!dir_gop.found_existing)
            dir_gop.key_ptr.* = addStringAssumeCapacity(cov, dir_path);
        const file_gop = try cov.files.getOrPutContextAdapted(gpa, File.SliceAdapter.Entry{
            .directory_index = @intCast(dir_gop.index),
            .basename = file_entry.path,
        }, File.SliceAdapter{
            .string_bytes = cov.string_bytes.items,
        }, File.MapContext{
            .string_bytes = cov.string_bytes.items,
        });
        if (!file_gop.found_existing) file_gop.key_ptr.* = .{
            .directory_index = @intCast(dir_gop.index),
            .basename = addStringAssumeCapacity(cov, file_entry.path),
        };
        out.* = .{
            .file = @enumFromInt(file_gop.index),
            .line = entry.line,
            .column = entry.column,
        };
    }
}

pub fn addStringAssumeCapacity(cov: *Coverage, s: []const u8) String {
    const result: String = @enumFromInt(cov.string_bytes.items.len);
    cov.string_bytes.appendSliceAssumeCapacity(s);
    cov.string_bytes.appendAssumeCapacity(0);
    return result;
}

fn span(s: []const u8) [:0]const u8 {
    return std.mem.sliceTo(@as([:0]const u8, @ptrCast(s)), 0);
}



---
File: /std/debug/cpu_context.zig
---

/// Register state for the native architecture, used by `std.debug` for stack unwinding.
/// `noreturn` if there is no implementation for the native architecture.
/// This can be overriden by exposing a declaration `root.debug.CpuContext`.
pub const Native = if (@hasDecl(root, "debug") and @hasDecl(root.debug, "CpuContext"))
    root.debug.CpuContext
else switch (native_arch) {
    .aarch64, .aarch64_be => Aarch64,
    .arc, .arceb => Arc,
    .arm, .armeb, .thumb, .thumbeb => Arm,
    .csky => Csky,
    .hexagon => Hexagon,
    .kvx => Kvx,
    .lanai => Lanai,
    .loongarch32, .loongarch64 => LoongArch,
    .m68k => M68k,
    .mips, .mipsel, .mips64, .mips64el => Mips,
    .or1k => Or1k,
    .powerpc, .powerpcle, .powerpc64, .powerpc64le => Powerpc,
    .sparc, .sparc64 => Sparc,
    .riscv32, .riscv32be, .riscv64, .riscv64be => Riscv,
    .ve => Ve,
    .s390x => S390x,
    .x86_16 => X86_16,
    .x86 => X86,
    .x86_64 => X86_64,
    else => noreturn,
};

pub const DwarfRegisterError = error{
    InvalidRegister,
    UnsupportedRegister,
};

pub fn fromPosixSignalContext(ctx_ptr: ?*const anyopaque) ?Native {
    if (signal_ucontext_t == void) return null;

    // In general, we include the hardwired zero register in the context if applicable.
    const uc: *const signal_ucontext_t = @ptrCast(@alignCast(ctx_ptr));

    // Deal with some special cases first.
    if (native_arch.isArc() and native_os == .linux) {
        var native: Native = .{
            .r = [_]u32{ uc.mcontext.r31, uc.mcontext.r30, 0, uc.mcontext.r28 } ++
                uc.mcontext.r27_26 ++
                uc.mcontext.r25_13 ++
                uc.mcontext.r12_0,
            .pcl = uc.mcontext.pcl,
        };

        // I have no idea why the kernel is storing these registers in such a bizarre order...
        std.mem.reverse(native.r[0..]);

        return native;
    } else if (native_arch == .loongarch32 and native_os == .linux) {
        // The 32-bit kABI (added later) kept the 64-bit layout.
        return .{
            .r = s: {
                var regs: [32]LoongArch.Gpr = undefined;
                for (uc.mcontext.r, 0..) |r, i| regs[i] = @truncate(r);
                break :s regs;
            },
            .pc = @truncate(uc.mcontext.pc),
        };
    } else if (native_arch.isMIPS32() and native_os == .linux) {
        // The O32 kABI uses 64-bit fields for some reason.
        return .{
            .r = s: {
                var regs: [32]Mips.Gpr = undefined;
                for (uc.mcontext.r, 0..) |r, i| regs[i] = @truncate(r);
                break :s regs;
            },
            .pc = @truncate(uc.mcontext.pc),
        };
    } else if (native_arch.isSPARC() and native_os == .linux) {
        const SparcStackFrame = extern struct {
            l: [8]usize,
            i: [8]usize,
            _x: [8]usize,
        };

        // When invoking a signal handler, the kernel builds an `rt_signal_frame` structure on the
        // stack and passes a pointer to its `info` field to the signal handler. This implies that
        // prior to said `info` field, we will find the `ss` field which, among other things,
        // contains the incoming and local registers of the interrupted code.
        const frame = @as(*const SparcStackFrame, @ptrFromInt(@as(usize, @intFromPtr(ctx_ptr)) - @sizeOf(SparcStackFrame)));

        return .{
            .g = uc.mcontext.g,
            .o = uc.mcontext.o,
            .l = frame.l,
            .i = frame.i,
            .pc = uc.mcontext.pc,
        };
    }

    // Only unified conversions from here.
    return switch (native_arch) {
        .arm, .armeb, .thumb, .thumbeb => .{
            .r = uc.mcontext.r ++ [_]u32{uc.mcontext.pc},
        },
        .aarch64, .aarch64_be => .{
            .x = uc.mcontext.x ++ [_]u64{uc.mcontext.lr},
            .sp = uc.mcontext.sp,
            .pc = uc.mcontext.pc,
        },
        .csky => .{
            .r = uc.mcontext.r0_13 ++
                [_]u32{ uc.mcontext.r14, uc.mcontext.r15 } ++
                uc.mcontext.r16_30 ++
                [_]u32{uc.mcontext.r31},
            .pc = uc.mcontext.pc,
        },
        .hexagon, .loongarch32, .loongarch64, .mips, .mipsel, .mips64, .mips64el, .or1k => .{
            .r = uc.mcontext.r,
            .pc = uc.mcontext.pc,
        },
        .m68k => .{
            .d = uc.mcontext.d,
            .a = uc.mcontext.a,
            .pc = uc.mcontext.pc,
        },
        .powerpc, .powerpcle, .powerpc64, .powerpc64le => .{
            .r = uc.mcontext.r,
            .pc = uc.mcontext.pc,
            .lr = uc.mcontext.lr,
        },
        .riscv32, .riscv32be, .riscv64, .riscv64be => .{
            // You can thank FreeBSD and OpenBSD for this silliness; they decided to be cute and
            // group the registers by ABI mnemonic rather than register number.
            .x = [_]Riscv.Gpr{0} ++
                uc.mcontext.ra_sp_gp_tp ++
                uc.mcontext.t0_2 ++
                uc.mcontext.s0_1 ++
                uc.mcontext.a ++
                uc.mcontext.s2_11 ++
                uc.mcontext.t3_6,
            .pc = uc.mcontext.pc,
        },
        .s390x => .{
            .r = uc.mcontext.r,
            .psw = .{
                .mask = uc.mcontext.psw.mask,
                .addr = uc.mcontext.psw.addr,
            },
        },
        .x86 => .{ .gprs = .init(.{
            .eax = uc.mcontext.eax,
            .ecx = uc.mcontext.ecx,
            .edx = uc.mcontext.edx,
            .ebx = uc.mcontext.ebx,
            .esp = uc.mcontext.esp,
            .ebp = uc.mcontext.ebp,
            .esi = uc.mcontext.esi,
            .edi = uc.mcontext.edi,
            .eip = uc.mcontext.eip,
        }) },
        .x86_64 => .{ .gprs = .init(.{
            .rax = uc.mcontext.rax,
            .rdx = uc.mcontext.rdx,
            .rcx = uc.mcontext.rcx,
            .rbx = uc.mcontext.rbx,
            .rsi = uc.mcontext.rsi,
            .rdi = uc.mcontext.rdi,
            .rbp = uc.mcontext.rbp,
            .rsp = uc.mcontext.rsp,
            .r8 = uc.mcontext.r8,
            .r9 = uc.mcontext.r9,
            .r10 = uc.mcontext.r10,
            .r11 = uc.mcontext.r11,
            .r12 = uc.mcontext.r12,
            .r13 = uc.mcontext.r13,
            .r14 = uc.mcontext.r14,
            .r15 = uc.mcontext.r15,
            .rip = uc.mcontext.rip,
        }) },
        else => comptime unreachable,
    };
}

pub fn fromWindowsContext(ctx: *const std.os.windows.CONTEXT) Native {
    return switch (native_arch) {
        .x86 => .{ .gprs = .init(.{
            .eax = ctx.Eax,
            .ecx = ctx.Ecx,
            .edx = ctx.Edx,
            .ebx = ctx.Ebx,
            .esp = ctx.Esp,
            .ebp = ctx.Ebp,
            .esi = ctx.Esi,
            .edi = ctx.Edi,
            .eip = ctx.Eip,
        }) },
        .x86_64 => .{ .gprs = .init(.{
            .rax = ctx.Rax,
            .rdx = ctx.Rdx,
            .rcx = ctx.Rcx,
            .rbx = ctx.Rbx,
            .rsi = ctx.Rsi,
            .rdi = ctx.Rdi,
            .rbp = ctx.Rbp,
            .rsp = ctx.Rsp,
            .r8 = ctx.R8,
            .r9 = ctx.R9,
            .r10 = ctx.R10,
            .r11 = ctx.R11,
            .r12 = ctx.R12,
            .r13 = ctx.R13,
            .r14 = ctx.R14,
            .r15 = ctx.R15,
            .rip = ctx.Rip,
        }) },
        .aarch64 => .{
            .x = ctx.DUMMYUNIONNAME.X[0..31].*,
            .sp = ctx.Sp,
            .pc = ctx.Pc,
        },
        .thumb => .{ .r = .{
            ctx.R0,  ctx.R1, ctx.R2,  ctx.R3,
            ctx.R4,  ctx.R5, ctx.R6,  ctx.R7,
            ctx.R8,  ctx.R9, ctx.R1
```
