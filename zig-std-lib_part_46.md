```
return {
    @branchHint(.cold);
    @trap();
}

pub fn integerPartOutOfBounds() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn corruptSwitch() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn shiftRhsTooBig() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn invalidEnumValue() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn forLenMismatch() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn copyLenMismatch() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn memcpyAlias() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn noreturnReturned() noreturn {
    @branchHint(.cold);
    @trap();
}



---
File: /std/debug/Pdb.zig
---

const std = @import("../std.zig");
const Io = std.Io;
const File = Io.File;
const Allocator = std.mem.Allocator;
const pdb = std.pdb;
const assert = std.debug.assert;

const Pdb = @This();

file_reader: *File.Reader,
msf: Msf,
allocator: Allocator,
string_table: ?*MsfStream,
ipi: ?[]u8,
modules: []Module,
sect_contribs: []pdb.SectionContribEntry,
guid: [16]u8,
age: u32,

pub const Module = struct {
    mod_info: pdb.ModInfo,
    module_name: []u8,
    obj_file_name: []u8,
    // The fields below are filled on demand.
    populated: bool,
    symbols: []u8,
    subsect_info: []u8,
    checksum_offset: ?usize,
    /// The inlinee source lines, sorted by inlinee. This saves us from repeatedly doing linear
    /// searches over all inlinees. We prefer binary search over a hashmap as LLVM somtimes outputs
    /// multiple entries for a single inlinee ID, see `getInlineeSourceLines` for more info.
    inlinee_source_lines: []InlineeSourceLine,

    pub fn deinit(self: *Module, allocator: Allocator) void {
        allocator.free(self.module_name);
        allocator.free(self.obj_file_name);
        if (self.populated) {
            allocator.free(self.symbols);
            allocator.free(self.subsect_info);
            allocator.free(self.inlinee_source_lines);
        }
    }
};

pub fn init(gpa: Allocator, file_reader: *File.Reader) !Pdb {
    return .{
        .file_reader = file_reader,
        .allocator = gpa,
        .string_table = null,
        .ipi = null,
        .msf = try Msf.init(gpa, file_reader),
        .modules = &.{},
        .sect_contribs = &.{},
        .guid = undefined,
        .age = undefined,
    };
}

pub fn deinit(self: *Pdb) void {
    const gpa = self.allocator;
    self.msf.deinit(gpa);
    if (self.ipi) |ipi| gpa.free(ipi);
    for (self.modules) |*module| {
        module.deinit(gpa);
    }
    gpa.free(self.modules);
    gpa.free(self.sect_contribs);
}

pub fn parseDbiStream(self: *Pdb) !void {
    var stream = self.getStream(pdb.StreamType.dbi) orelse
        return error.InvalidDebugInfo;

    const gpa = self.allocator;
    const reader = &stream.interface;

    const header = try reader.takeStruct(pdb.DbiStreamHeader, .little);
    if (header.version_header != 19990903) // V70, only value observed by LLVM team
        return error.UnknownPDBVersion;
    // if (header.Age != age)
    //     return error.UnmatchingPDB;

    const mod_info_size = header.mod_info_size;
    const section_contrib_size = header.section_contribution_size;

    var modules = std.array_list.Managed(Module).init(gpa);
    errdefer modules.deinit();

    // Module Info Substream
    var mod_info_offset: usize = 0;
    while (mod_info_offset != mod_info_size) {
        const mod_info = try reader.takeStruct(pdb.ModInfo, .little);
        var this_record_len: usize = @sizeOf(pdb.ModInfo);

        var module_name: Io.Writer.Allocating = .init(gpa);
        defer module_name.deinit();
        this_record_len += try reader.streamDelimiterLimit(&module_name.writer, 0, .limited(1024));
        assert(reader.buffered()[0] == 0); // TODO change streamDelimiterLimit API
        reader.toss(1);
        this_record_len += 1;

        var obj_file_name: Io.Writer.Allocating = .init(gpa);
        defer obj_file_name.deinit();
        this_record_len += try reader.streamDelimiterLimit(&obj_file_name.writer, 0, .limited(1024));
        assert(reader.buffered()[0] == 0); // TODO change streamDelimiterLimit API
        reader.toss(1);
        this_record_len += 1;

        if (this_record_len % 4 != 0) {
            const round_to_next_4 = (this_record_len | 0x3) + 1;
            const march_forward_bytes = round_to_next_4 - this_record_len;
            try stream.seekBy(@as(isize, @intCast(march_forward_bytes)));
            this_record_len += march_forward_bytes;
        }

        try modules.append(.{
            .mod_info = mod_info,
            .module_name = try module_name.toOwnedSlice(),
            .obj_file_name = try obj_file_name.toOwnedSlice(),

            .populated = false,
            .symbols = undefined,
            .subsect_info = undefined,
            .checksum_offset = null,
            .inlinee_source_lines = undefined,
        });

        mod_info_offset += this_record_len;
        if (mod_info_offset > mod_info_size)
            return error.InvalidDebugInfo;
    }

    // Section Contribution Substream
    var sect_contribs = std.array_list.Managed(pdb.SectionContribEntry).init(gpa);
    errdefer sect_contribs.deinit();

    var sect_cont_offset: usize = 0;
    if (section_contrib_size != 0) {
        const version = reader.takeEnum(pdb.SectionContrSubstreamVersion, .little) catch |err| switch (err) {
            error.InvalidEnumTag, error.EndOfStream => return error.InvalidDebugInfo,
            error.ReadFailed => return error.ReadFailed,
        };
        _ = version;
        sect_cont_offset += @sizeOf(u32);
    }
    while (sect_cont_offset != section_contrib_size) {
        const entry = try sect_contribs.addOne();
        entry.* = try reader.takeStruct(pdb.SectionContribEntry, .little);
        sect_cont_offset += @sizeOf(pdb.SectionContribEntry);

        if (sect_cont_offset > section_contrib_size)
            return error.InvalidDebugInfo;
    }

    self.modules = try modules.toOwnedSlice();
    self.sect_contribs = try sect_contribs.toOwnedSlice();
}

pub fn parseIpiStream(self: *Pdb) !void {
    const gpa = self.allocator;
    const stream = self.getStream(.ipi) orelse return;
    const header = try stream.interface.peekStruct(pdb.IpiStreamHeader, .little);
    if (header.version != .v80) // only value observed by LLVM team
        return error.UnknownPDBVersion;
    self.ipi = try stream.interface.readAlloc(gpa, @sizeOf(pdb.IpiStreamHeader) + header.type_record_bytes);
}

pub fn parseInfoStream(self: *Pdb) !void {
    var stream = self.getStream(pdb.StreamType.pdb) orelse return error.InvalidDebugInfo;
    const reader = &stream.interface;

    // Parse the InfoStreamHeader.
    const version = try reader.takeInt(u32, .little);
    const signature = try reader.takeInt(u32, .little);
    _ = signature;
    const age = try reader.takeInt(u32, .little);
    const guid = try reader.takeArray(16);

    if (version != 20000404) // VC70, only value observed by LLVM team
        return error.UnknownPDBVersion;

    self.guid = guid.*;
    self.age = age;

    const gpa = self.allocator;

    // Find the string table.
    const string_table_index = str_tab_index: {
        const name_bytes_len = try reader.takeInt(u32, .little);
        const name_bytes = try reader.readAlloc(gpa, name_bytes_len);
        defer gpa.free(name_bytes);

        const HashTableHeader = extern struct {
            size: u32,
            capacity: u32,

            fn maxLoad(cap: u32) u32 {
                return cap * 2 / 3 + 1;
            }
        };
        const hash_tbl_hdr = try reader.takeStruct(HashTableHeader, .little);
        if (hash_tbl_hdr.capacity == 0)
            return error.InvalidDebugInfo;

        if (hash_tbl_hdr.size > HashTableHeader.maxLoad(hash_tbl_hdr.capacity))
            return error.InvalidDebugInfo;

        const present = try readSparseBitVector(reader, gpa);
        defer gpa.free(present);
        if (present.len != hash_tbl_hdr.size)
            return error.InvalidDebugInfo;
        const deleted = try readSparseBitVector(reader, gpa);
        defer gpa.free(deleted);

        for (present) |_| {
            const name_offset = try reader.takeInt(u32, .little);
            const name_index = try reader.takeInt(u32, .little);
            if (name_offset > name_bytes.len)
                return error.InvalidDebugInfo;
            const name = std.mem.sliceTo(name_bytes[name_offset..], 0);
            if (std.mem.eql(u8, name, "/names")) {
                break :str_tab_index name_index;
            }
        }
        return error.MissingDebugInfo;
    };

    self.string_table = self.getStreamById(string_table_index) orelse
        return error.MissingDebugInfo;
}

pub fn getProcSym(self: *Pdb, module: *Module, address: u64) ?*align(1) pdb.ProcSym {
    _ = self;
    std.debug.assert(module.populated);
    var reader: Io.Reader = .fixed(module.symbols);
    while (true) {
        const prefix = reader.takeStructPointer(pdb.RecordPrefix) catch return null;
        if (prefix.record_len < 2)
            return null;
        reader.discardAll(prefix.record_len - @sizeOf(u16)) catch return null;
        switch (prefix.record_kind) {
            .lproc32, .gproc32 => {
                const proc_sym: *align(1) pdb.ProcSym = @ptrCast(prefix);
                if (address >= proc_sym.code_offset and address < proc_sym.code_offset + proc_sym.code_size) {
                    return proc_sym;
                }
            },
            else => {},
        }
    }
    return null;
}

pub const InlineSiteSymIterator = struct {
    module_index: usize,
    offset: usize,
    end: usize,

    const empty: InlineSiteSymIterator = .{
        .module_index = 0,
        .offset = 0,
        .end = 0,
    };

    pub fn next(iter: *InlineSiteSymIterator, module: *Module) ?*align(1) pdb.InlineSiteSym {
        while (iter.offset < iter.end) {
            const inline_prefix: *align(1) pdb.RecordPrefix = @ptrCast(&module.symbols[iter.offset]);
            const end = iter.offset + inline_prefix.record_len + @sizeOf(u16);
            if (end > iter.end) return null;
            defer iter.offset = end;
            switch (inline_prefix.record_kind) {
                // Skip nested procedures
                .lproc32,
                .lproc32_st,
                .gproc32,
                .gproc32_st,
                .lproc32_id,
                .gproc32_id,
                .lproc32_dpc,
                .lproc32_dpc_id,
                => {
                    const skip: *align(1) pdb.ProcSym = @ptrCast(inline_prefix);
                    iter.offset = skip.end;
                },
                .inlinesite,
                .inlinesite2,
                => return @ptrCast(inline_prefix),
                else => {},
            }
        }

        return null;
    }
};

pub const BinaryAnnotation = union(enum) {
    code_offset: u32,
    change_code_offset_base: u32,
    change_code_offset: u32,
    change_code_length: u32,
    change_file: u32,
    change_line_offset: i32,
    change_line_end_delta: u32,
    change_range_kind: RangeKind,
    change_column_start: u32,
    change_column_end_delta: i32,
    change_code_offset_and_line_offset: struct { code_delta: u32, line_delta: i32 },
    change_code_length_and_code_offset: struct { length: u32, delta: u32 },
    change_column_end: u32,

    pub const RangeKind = enum(u32) { expression = 0, statement = 1 };

    /// A virtual machine that processed binary annotations.
    pub const RangeIterator = struct {
        annotations: Iterator,
        curr: PartialRange,
        /// The previous range is tracked as the code length is sometimes implied by the subsequent
        /// range.
        prev: ?PartialRange,

        const PartialRange = struct {
            line_offset: i32,
            file_id: ?u32,
            code_offset: u32,
            code_length: ?u32,

            /// Resolves a partial range to a range with a definite length, or returns null if this
            /// is not possible.
            fn resolve(self: PartialRange, next_code_offset: ?u32) ?Range {
                return .{
                    .line_offset = self.line_offset,
                    .file_id = self.file_id,
                    .code_offset = self.code_offset,
                    .code_length = b: {
                        if (self.code_length) |l| break :b l;
                        const end = next_code_offset orelse return null;
                        break :b end - self.code_offset;
                    },
                };
            }
        };

        pub fn init(annotations: Iterator) RangeIterator {
            return .{
                .annotations = annotations,
                .curr = .{
                    .line_offset = 0,
                    .file_id = null,
                    .code_offset = 0,
                    .code_length = null,
                },
                .prev = null,
            };
        }

        pub const Range = struct {
            line_offset: i32,
            file_id: ?u32,
            code_offset: u32,
            code_length: u32,

            pub fn contains(self: Range, offset_in_func: usize) bool {
                return self.code_offset <= offset_in_func and
                    offset_in_func < self.code_offset + self.code_length;
            }
        };

        pub fn next(self: *RangeIterator) error{InvalidDebugInfo}!?Range {
            while (try self.annotations.next()) |annotation| {
                switch (annotation) {
                    .change_code_offset => |delta| {
                        self.curr.code_offset += delta;
                    },
                    .change_code_length => |length| {
                        if (self.prev) |*prev| prev.code_length = prev.code_length orelse length;
                        self.curr.code_offset += length;
                    },
                    // LLVM has code to emit these, but I wasn't able to figure out how trigger it
                    // so this logic is untested.
                    .change_file => |file_id| {
                        self.curr.file_id = file_id;
                    },
                    // LLVM never emits this opcode, but it's clear enough how to interpret it so we
                    // may as well handle it in case they emit it in the future
                    .change_code_length_and_code_offset => |info| {
                        self.curr.code_length = info.length;
                        self.curr.code_offset += info.delta;
                    },
                    .change_line_offset => |delta| {
                        self.curr.line_offset += delta;
                    },
                    .change_code_offset_and_line_offset => |info| {
                        self.curr.code_offset += info.code_delta;
                        self.curr.line_offset += info.line_delta;
                    },

                    // Not emitted by LLVM at the time of writing, and we don't want to add support
                    // without a test case. Safe to ignore since we don't use this info right now.
                    .change_line_end_delta,
                    .change_column_start,
                    .change_column_end_delta,
                    .change_column_end,
                    => {},

                    // Not emitted by LLVM at the time of writing. Various sources conflict on how
                    // these opcodes should be interpreted, so we make no attempt to handle them.
                    .code_offset,
                    .change_code_offset_base,
                    .change_range_kind,
                    => {
                        self.annotations = .empty;
                        self.prev = null;
                        return null;
                    },
                }

                // If we have a new code offset, return the previous range if it exists, resolving
                // its length if necessary.
                switch (annotation) {
                    .change_code_offset,
                    .change_code_offset_and_line_offset,
                    .change_code_length_and_code_offset,
                    => {},
                    else => continue,
                }
                defer self.prev = self.curr;
                const prev = self.prev orelse continue;
                return prev.resolve(self.curr.code_offset);
            }

            // If we've processed all the binary operations but still have a previous range leftover
            // with a known length, return it.
            const prev = self.prev orelse return null;
            defer self.prev = null;
            return prev.resolve(null);
        }
    };

    pub const Iterator = struct {
        reader: Io.Reader,

        pub const empty: Iterator = .{ .reader = .ending_instance };

        pub fn next(self: *Iterator) error{InvalidDebugInfo}!?BinaryAnnotation {
            return take(&self.reader) catch |err| switch (err) {
                error.ReadFailed => return error.InvalidDebugInfo,
                error.EndOfStream => return null,
            };
        }
    };

    pub fn take(reader: *Io.Reader) Io.Reader.Error!BinaryAnnotation {
        const op = std.enums.fromInt(
            pdb.BinaryAnnotationOpcode,
            try takePackedU32(reader),
        ) orelse return error.ReadFailed;
        switch (op) {
            // Microsoft's docs say that invalid is used as padding, though it is left ambiguous
            // whether padding is allowed internally or only after all instructions are complete.
            // Empirically, the latter appears to be the case, at least with the output from LLVM
            // that I've tested.
            .invalid => return error.EndOfStream,
            .code_offset => return .{
                .code_offset = try expect(takePackedU32(reader)),
            },
            .change_code_offset_base => return .{
                .change_code_offset_base = try expect(takePackedU32(reader)),
            },
            .change_code_offset => return .{
                .change_code_offset = try expect(takePackedU32(reader)),
            },
            .change_code_length => return .{
                .change_code_length = try expect(takePackedU32(reader)),
            },
            .change_file => return .{
                .change_file = try expect(takePackedU32(reader)),
            },
            .change_line_offset => return .{
                .change_line_offset = try expect(takePackedI32(reader)),
            },
            .change_line_end_delta => return .{
                .change_line_end_delta = try expect(takePackedU32(reader)),
            },
            .change_range_kind => return .{
                .change_range_kind = std.enums.fromInt(
                    RangeKind,
                    try expect(takePackedU32(reader)),
                ) orelse return error.ReadFailed,
            },
            .change_column_start => return .{
                .change_column_start = try expect(takePackedU32(reader)),
            },
            .change_column_end_delta => return .{
                .change_column_end_delta = try expect(takePackedI32(reader)),
            },
            .change_code_offset_and_line_offset => {
                const EncodedArgs = packed struct(u32) {
                    code_delta: u4,
                    encoded_line_delta: u28,
                };
                const args: EncodedArgs = @bitCast(try expect(takePackedU32(reader)));
                return .{
                    .change_code_offset_and_line_offset = .{
                        .code_delta = args.code_delta,
                        .line_delta = decodeI32(args.encoded_line_delta),
                    },
                };
            },
            .change_code_length_and_code_offset => return .{
                .change_code_length_and_code_offset = .{
                    .length = try expect(takePackedU32(reader)),
                    .delta = try expect(takePackedU32(reader)),
                },
            },
            .change_column_end => return .{
                .change_column_end = try expect(takePackedU32(reader)),
            },
        }
    }

    // Adapted from:
    // https://github.com/microsoft/microsoft-pdb/blob/805655a28bd8198004be2ac27e6e0290121a5e89/include/cvinfo.h#L4942
    pub fn takePackedU32(reader: *Io.Reader) Io.Reader.Error!u32 {
        const b0: u32 = try reader.takeByte();
        if (b0 & 0x80 == 0x00) return b0;

        const b1: u32 = try reader.takeByte();
        if (b0 & 0xC0 == 0x80) return ((b0 & 0x3F) << 8) | b1;

        const b2: u32 = try reader.takeByte();
        const b3: u32 = try reader.takeByte();
        if (b0 & 0xE0 == 0xC0) return ((b0 & 0x1f) << 24) | (b1 << 16) | (b2 << 8) | b3;

        return error.ReadFailed;
    }

    pub fn takePackedI32(reader: *Io.Reader) Io.Reader.Error!i32 {
        return decodeI32(try takePackedU32(reader));
    }

    pub fn decodeI32(u: u32) i32 {
        const i: i32 = @bitCast(u);
        if (i & 1 != 0) {
            return -(i >> 1);
        } else {
            return i >> 1;
        }
    }

    fn expect(value: anytype) error{ReadFailed}!@typeInfo(@TypeOf(value)).error_union.payload {
        comptime assert(@typeInfo(@TypeOf(value)).error_union.error_set == Io.Reader.Error);
        return value catch error.ReadFailed;
    }
};

pub fn findInlineeName(self: *const Pdb, inlinee: u32) ?[]const u8 {
    // According to LLVM, the high bit *can* be used to indicate that a type index comes from the
    // ipi stream in which case that bit needs to be cleared. LLVM doesn't generate data in this
    // manner, but we may as well handle it since it just involves a single bitwise and.
    // https://llvm.org/docs/PDB/TpiStream.html#type-indices
    const type_index = inlinee & 0x7FFFFFFF;

    var reader: Io.Reader = .fixed(self.ipi orelse return null);
    const header = reader.takeStructPointer(pdb.IpiStreamHeader) catch return null;
    for (header.type_index_begin..header.type_index_end) |curr_type_index| {
        const prefix = reader.takeStructPointer(pdb.LfRecordPrefix) catch return null;
        if (prefix.len < 2) return null;
        reader.discardAll(prefix.len - @sizeOf(u16)) catch return null;

        if (curr_type_index == type_index) {
            switch (prefix.kind) {
                .func_id => {
                    const func: *align(1) pdb.LfFuncId = @ptrCast(prefix);
                    return std.mem.sliceTo(@as([*:0]const u8, @ptrCast(&func.name[0])), 0);
                },
                .mfunc_id => {
                    const func: *align(1) pdb.LfMFuncId = @ptrCast(prefix);
                    return std.mem.sliceTo(@as([*:0]const u8, @ptrCast(&func.name[0])), 0);
                },
                else => return null,
            }
        }
    }
    return null;
}

pub fn getInlinees(self: *Pdb, module: *Module, proc_sym: *align(1) const pdb.ProcSym) InlineSiteSymIterator {
    const module_index = module - self.modules.ptr;
    const offset = @intFromPtr(proc_sym) -
        @intFromPtr(module.symbols.ptr) +
        proc_sym.record_len +
        @sizeOf(u16);
    const symbols_end = @intFromPtr(module.symbols.ptr) + module.symbols.len;
    if (offset > symbols_end or proc_sym.end > symbols_end) return .empty;
    return .{
        .module_index = module_index,
        .offset = offset,
        .end = proc_sym.end,
    };
}

pub fn getBinaryAnnotations(self: *Pdb, module: *Module, site: *align(1) const pdb.InlineSiteSym) BinaryAnnotation.Iterator {
    _ = self;
    var start: usize = @intFromPtr(site) + @sizeOf(pdb.InlineSiteSym);
    var end = start + site.record_len + @sizeOf(u16) - @sizeOf(pdb.InlineSiteSym);
    switch (site.record_kind) {
        .inlinesite => {},
        .inlinesite2 => start += @sizeOf(pdb.InlineSiteSym2) - @sizeOf(pdb.InlineSiteSym),
        else => end = start,
    }
    if (start < @intFromPtr(module.symbols.ptr) or end > @intFromPtr(module.symbols.ptr) + module.symbols.len) return .empty;
    const len = end - start;
    const ptr: [*]const u8 = @ptrFromInt(start);
    const slice = ptr[0..len];
    return .{ .reader = Io.Reader.fixed(slice) };
}

pub fn getInlineSiteSourceLocation(
    self: *Pdb,
    gpa: Allocator,
    mod: *Module,
    site: *align(1) const pdb.InlineSiteSym,
    inlinee_src_line: *align(1) const pdb.InlineeSourceLine,
    offset_in_func: usize,
) !?std.debug.SourceLocation {
    var ranges: BinaryAnnotation.RangeIterator = .init(self.getBinaryAnnotations(mod, site));
    while (try ranges.next()) |range| {
        if (!range.contains(offset_in_func)) continue;

        const file_id = range.file_id orelse inlinee_src_line.file_id;
        const file_name = try self.getFileName(gpa, mod, file_id);
        errdefer self.allocator.free(file_name);

        return .{
            .line = inlinee_src_line.source_line_num +% @as(u32, @bitCast(range.line_offset)),
            // LLVM doesn't currently emit column information for inlined calls in PDBs.
            .column = 0,
            .file_name = file_name,
        };
    }
    return null;
}

pub fn getFileName(self: *Pdb, gpa: Allocator, mod: *Module, file_id: u32) ![]const u8 {
    const checksum_offset = mod.checksum_offset orelse return error.MissingDebugInfo;
    const subsect_index = checksum_offset + file_id;
    const chksum_hdr: *align(1) pdb.FileChecksumEntryHeader = @ptrCast(&mod.subsect_info[subsect_index]);
    const strtab_offset = @sizeOf(pdb.StringTableHeader) + chksum_hdr.file_name_offset;
    self.string_table.?.seekTo(strtab_offset) catch return error.InvalidDebugInfo;
    const string_reader = &self.string_table.?.interface;
    var source_file_name: Io.Writer.Allocating = .init(gpa);
    defer source_file_name.deinit();
    _ = try string_reader.streamDelimiterLimit(&source_file_name.writer, 0, .limited(1024));
    assert(string_reader.buffered()[0] == 0); // TODO change streamDelimiterLimit API
    string_reader.toss(1);
    return try source_file_name.toOwnedSlice();
}

pub fn getSymbolName(self: *Pdb, proc_sym: *align(1) const pdb.ProcSym) []const u8 {
    _ = self;
    return std.mem.sliceTo(@as([*:0]const u8, @ptrCast(&proc_sym.name[0])), 0);
}

pub const InlineeSourceLine = struct {
    signature: pdb.InlineeSourceLineSignature,
    info: *align(1) const pdb.InlineeSourceLine,

    fn lessThan(_: void, lhs: InlineeSourceLine, rhs: InlineeSourceLine) bool {
        return lhs.info.inlinee < rhs.info.inlinee;
    }

    fn compare(inlinee: u32, self: InlineeSourceLine) std.math.Order {
        return std.math.order(inlinee, self.info.inlinee);
    }
};

/// Returns all `InlineeSourceLine`s for a given module with the given inlinee. Ideally there would
/// only be one entry per inlinee, but LLVM appears to assign all functions that share a name the
/// same inlinee ID. This appears to be a bug, so the best the caller can do right now is print all
/// the results.
pub fn getInlineeSourceLines(
    self: *Pdb,
    mod: *Module,
    inlinee: u32,
) []const InlineeSourceLine {
    _ = self;

    // Binary search to an arbitrary match, if there are other matches they will be adjacent
    const any = std.sort.binarySearch(
        InlineeSourceLine,
        mod.inlinee_source_lines,
        inlinee,
        InlineeSourceLine.compare,
    ) orelse return &.{};

    // Linearly scan to the first match
    const begin = b: {
        var begin = any;
        while (begin > 0) {
            const prev = begin - 1;
            if (mod.inlinee_source_lines[prev].info.inlinee != inlinee) break;
            begin = prev;
        }
        break :b begin;
    };

    // Linearly scan to the last match
    const end = b: {
        var end = any + 1;
        while (end < mod.inlinee_source_lines.len and
            mod.inlinee_source_lines[end].info.inlinee == inlinee) : (end += 1)
        {}
        break :b end;
    };

    // Return a slice of all the matches
    return mod.inlinee_source_lines[begin..end];
}

pub fn getLineNumberInfo(self: *Pdb, gpa: Allocator, module: *Module, address: u64) !std.debug.SourceLocation {
    std.debug.assert(module.populated);
    const subsect_info = module.subsect_info;

    var sect_offset: usize = 0;
    var skip_len: usize = undefined;
    while (sect_offset != subsect_info.len) : (sect_offset += skip_len) {
        const subsect_hdr: *align(1) pdb.DebugSubsectionHeader = @ptrCast(&subsect_info[sect_offset]);
        skip_len = subsect_hdr.length;
        sect_offset += @sizeOf(pdb.DebugSubsectionHeader);

        switch (subsect_hdr.kind) {
            .lines => {
                var line_index = sect_offset;

                const line_hdr: *align(1) pdb.LineFragmentHeader = @ptrCast(&subsect_info[line_index]);
                if (line_hdr.reloc_segment == 0)
                    return error.MissingDebugInfo;
                line_index += @sizeOf(pdb.LineFragmentHeader);
                const frag_vaddr_start = line_hdr.reloc_offset;
                const frag_vaddr_end = frag_vaddr_start + line_hdr.code_size;

                if (address >= frag_vaddr_start and address < frag_vaddr_end) {
                    // There is an unknown number of LineBlockFragmentHeaders (and their accompanying line and column records)
                    // from now on. We will iterate through them, and eventually find a SourceLocation that we're interested in,
                    // breaking out to :subsections. If not, we will make sure to not read anything outside of this subsection.
                    const subsection_end_index = sect_offset + subsect_hdr.length;

                    while (line_index < subsection_end_index) {
                        const block_hdr: *align(1) pdb.LineBlockFragmentHeader = @ptrCast(&subsect_info[line_index]);
                        line_index += @sizeOf(pdb.LineBlockFragmentHeader);
                        const start_line_index = line_index;

                        const has_column = line_hdr.flags.have_columns;

                        // All line entries are stored inside their line block by ascending start address.
                        // Heuristic: we want to find the last line entry
                        // that has a vaddr_start <= address.
                        // This is done with a simple linear search.
                        var line_i: u32 = 0;
                        while (line_i < block_hdr.num_lines) : (line_i += 1) {
                            const line_num_entry: *align(1) pdb.LineNumberEntry = @ptrCast(&subsect_info[line_index]);
                            line_index += @sizeOf(pdb.LineNumberEntry);

                            const vaddr_start = frag_vaddr_start + line_num_entry.offset;
                            if (address < vaddr_start) {
                                break;
                            }
                        }

                        // line_i == 0 would mean that no matching pdb.LineNumberEntry was found.
                        if (line_i > 0) {
                            const file_name = try self.getFileName(gpa, module, block_hdr.name_index);
                            errdefer gpa.free(file_name);

                            const line_entry_idx = line_i - 1;

                            const column = if (has_column) blk: {
                                const start_col_index = start_line_index + @sizeOf(pdb.LineNumberEntry) * block_hdr.num_lines;
                                const col_index = start_col_index + @sizeOf(pdb.ColumnNumberEntry) * line_entry_idx;
                                const col_num_entry: *align(1) pdb.ColumnNumberEntry = @ptrCast(&subsect_info[col_index]);
                                break :blk col_num_entry.start_column;
                            } else 0;

                            const found_line_index = start_line_index + line_entry_idx * @sizeOf(pdb.LineNumberEntry);
                            const line_num_entry: *align(1) pdb.LineNumberEntry = @ptrCast(&subsect_info[found_line_index]);

                            return .{
                                .file_name = file_name,
                                .line = line_num_entry.flags.start,
                                .column = column,
                            };
                        }
                    }

                    // Checking that we are not reading garbage after the (possibly) multiple block fragments.
                    if (line_index != subsection_end_index) {
                        return error.InvalidDebugInfo;
                    }
                }
            },
            else => {},
        }

        if (sect_offset > subsect_info.len)
            return error.InvalidDebugInfo;
    }

    return error.MissingDebugInfo;
}

pub fn getModule(self: *Pdb, index: usize) !?*Module {
    if (index >= self.modules.len)
        return null;

    const mod = &self.modules[index];
    if (mod.populated)
        return mod;

    // At most one can be non-zero.
    if (mod.mod_info.c11_byte_size != 0 and mod.mod_info.c13_byte_size != 0)
        return error.InvalidDebugInfo;
    if (mod.mod_info.c13_byte_size == 0)
        return error.InvalidDebugInfo;

    const stream = self.getStreamById(mod.mod_info.module_sym_stream) orelse
        return error.MissingDebugInfo;
    const reader = &stream.interface;

    const signature = try reader.takeInt(u32, .little);
    if (signature != 4)
        return error.InvalidDebugInfo;

    const gpa = self.allocator;

    mod.symbols = try reader.readAlloc(gpa, mod.mod_info.sym_byte_size - 4);
    errdefer gpa.free(mod.symbols);
    mod.subsect_info = try reader.readAlloc(gpa, mod.mod_info.c13_byte_size);
    errdefer gpa.free(mod.subsect_info);
    mod.inlinee_source_lines = b: {
        var inlinee_source_lines: std.ArrayList(InlineeSourceLine) = .empty;
        defer inlinee_source_lines.deinit(gpa);
        var subsects: Io.Reader = .fixed(mod.subsect_info);
        while (subsects.takeStructPointer(pdb.DebugSubsectionHeader) catch null) |subsect_hdr| {
            var subsect: Io.Reader = .fixed(subsects.take(subsect_hdr.length) catch return null);
            if (subsect_hdr.kind == .inlinee_lines) {
                const inlinee_source_line_signature = subsect.takeEnum(pdb.InlineeSourceLineSignature, .little) catch return error.InvalidDebugInfo;
                const has_extra_files = switch (inlinee_source_line_signature) {
                    .normal => false,
                    .ex => true,
                    else => continue,
                };
                while (subsect.takeStructPointer(pdb.InlineeSourceLine) catch null) |info| {
                    if (has_extra_files) {
                        const file_count = subsect.takeInt(u32, .little) catch
                            return error.InvalidDebugInfo;
                        const file_bytes = std.math.mul(usize, file_count, @sizeOf(u32)) catch return error.InvalidDebugInfo;
                        subsect.discardAll(file_bytes) catch
                            return error.InvalidDebugInfo;
                    }

                    try inlinee_source_lines.append(gpa, .{
                        .signature = inlinee_source_line_signature,
                        .info = info,
                    });
                }
            }
        }

        std.mem.sortUnstable(InlineeSourceLine, inlinee_source_lines.items, {}, InlineeSourceLine.lessThan);
        break :b try inlinee_source_lines.toOwnedSlice(gpa);
    };
    errdefer gpa.free(mod.inlinee_source_lines);

    var sect_offset: usize = 0;
    var skip_len: usize = undefined;
    while (sect_offset != mod.subsect_info.len) : (sect_offset += skip_len) {
        const subsect_hdr: *align(1) pdb.DebugSubsectionHeader = @ptrCast(&mod.subsect_info[sect_offset]);
        skip_len = subsect_hdr.length;
        sect_offset += @sizeOf(pdb.DebugSubsectionHeader);

        switch (subsect_hdr.kind) {
            .file_checksums => {
                mod.checksum_offset = sect_offset;
                break;
            },
            else => {},
        }

        if (sect_offset > mod.subsect_info.len)
            return error.InvalidDebugInfo;
    }

    mod.populated = true;
    return mod;
}

pub fn getStreamById(self: *Pdb, id: u32) ?*MsfStream {
    if (id >= self.msf.streams.len) return null;
    return &self.msf.streams[id];
}

pub fn getStream(self: *Pdb, stream: pdb.StreamType) ?*MsfStream {
    const id = @intFromEnum(stream);
    return self.getStreamById(id);
}

/// https://llvm.org/docs/PDB/MsfFile.html
const Msf = struct {
    directory: MsfStream,
    streams: []MsfStream,

    fn init(gpa: Allocator, file_reader: *File.Reader) !Msf {
        const superblock = try file_reader.interface.takeStruct(pdb.SuperBlock, .little);

        if (!std.mem.eql(u8, &superblock.file_magic, pdb.SuperBlock.expect_magic))
            return error.InvalidDebugInfo;
        if (superblock.free_block_map_block != 1 and superblock.free_block_map_block != 2)
            return error.InvalidDebugInfo;
        if (superblock.num_blocks * superblock.block_size != try file_reader.getSize())
            return error.InvalidDebugInfo;
        switch (superblock.block_size) {
            // llvm only supports 4096 but we can handle any of these values
            512, 1024, 2048, 4096 => {},
            else => return error.InvalidDebugInfo,
        }

        const dir_block_count = blockCountFromSize(superblock.num_directory_bytes, superblock.block_size);
        if (dir_block_count > superblock.block_size / @sizeOf(u32))
            return error.UnhandledBigDirectoryStream; // cf. BlockMapAddr comment.

        try file_reader.seekTo(superblock.block_size * superblock.block_map_addr);
        const dir_blocks = try gpa.alloc(u32, dir_block_count);
        errdefer gpa.free(dir_blocks);
        for (dir_blocks) |*b| {
            b.* = try file_reader.interface.takeInt(u32, .little);
        }
        var directory_buffer: [64]u8 = undefined;
        var directory = MsfStream.init(superblock.block_size, file_reader, dir_blocks, &directory_buffer);

        const begin = directory.logicalPos();
        const stream_count = try directory.interface.takeInt(u32, .little);
        const stream_sizes = try gpa.alloc(u32, stream_count);
        defer gpa.free(stream_sizes);

        // Microsoft's implementation uses @as(u32, -1) for inexistent streams.
        // These streams are not used, but still participate in the file
        // and must be taken into account when resolving stream indices.
        const nil_size = 0xFFFFFFFF;
        for (stream_sizes) |*s| {
            const size = try directory.interface.takeInt(u32, .little);
            s.* = if (size == nil_size) 0 else blockCountFromSize(size, superblock.block_size);
        }

        const streams = try gpa.alloc(MsfStream, stream_count);
        errdefer gpa.free(streams);

        for (streams, stream_sizes) |*stream, size| {
            if (size == 0) {
                stream.* = .empty;
                continue;
            }
            const blocks = try gpa.alloc(u32, size);
            errdefer gpa.free(blocks);
            for (blocks) |*block| {
                const block_id = try directory.interface.takeInt(u32, .little);
                // Index 0 is reserved for the superblock.
                // In theory, every page which is `n * block_size + 1` or `n * block_size + 2`
                // is also reserved, for one of the FPMs. However, LLVM has been observed to map
                // these into actual streams, so allow it for compatibility.
                if (block_id == 0 or block_id >= superblock.num_blocks) return error.InvalidBlockIndex;
                block.* = block_id;
            }
            const buffer = try gpa.alloc(u8, 64);
            errdefer gpa.free(buffer);
            stream.* = .init(superblock.block_size, file_reader, blocks, buffer);
        }

        const end = directory.logicalPos();
        if (end - begin != superblock.num_directory_bytes)
            return error.InvalidStreamDirectory;

        return .{
            .directory = directory,
            .streams = streams,
        };
    }

    fn deinit(self: *Msf, gpa: Allocator) void {
        gpa.free(self.directory.blocks);
        for (self.streams) |*stream| {
            gpa.free(stream.interface.buffer);
            gpa.free(stream.blocks);
        }
        gpa.free(self.streams);
    }
};

const MsfStream = struct {
    file_reader: *File.Reader,
    next_read_pos: u64,
    blocks: []u32,
    block_size: u32,
    interface: Io.Reader,
    err: ?Error,

    const Error = File.Reader.SeekError;

    const empty: MsfStream = .{
        .file_reader = undefined,
        .next_read_pos = 0,
        .blocks = &.{},
        .block_size = undefined,
        .interface = .ending_instance,
        .err = null,
    };

    fn init(block_size: u32, file_reader: *File.Reader, blocks: []u32, buffer: []u8) MsfStream {
        return .{
            .file_reader = file_reader,
            .next_read_pos = 0,
            .blocks = blocks,
            .block_size = block_size,
            .interface = .{
                .vtable = &.{ .stream = stream },
                .buffer = buffer,
                .seek = 0,
                .end = 0,
            },
            .err = null,
        };
    }

    fn stream(r: *Io.Reader, w: *Io.Writer, limit: Io.Limit) Io.Reader.StreamError!usize {
        const ms: *MsfStream = @alignCast(@fieldParentPtr("interface", r));

        var block_id: usize = @intCast(ms.next_read_pos / ms.block_size);
        if (block_id >= ms.blocks.len) return error.EndOfStream;
        var block = ms.blocks[block_id];
        var offset = ms.next_read_pos % ms.block_size;

        ms.file_reader.seekTo(block * ms.block_size + offset) catch |err| {
            ms.err = err;
            return error.ReadFailed;
        };

        var remaining = @intFromEnum(limit);
        while (remaining != 0) {
            const stream_len: usize = @min(remaining, ms.block_size - offset);
            const n = try ms.file_reader.interface.stream(w, .limited(stream_len));
            remaining -= n;
            offset += n;

            // If we're at the end of a block, go to the next one.
            if (offset == ms.block_size) {
                offset = 0;
                block_id += 1;
                if (block_id >= ms.blocks.len) break; // End of Stream
                block = ms.blocks[block_id];
                ms.file_reader.seekTo(block * ms.block_size) catch |err| {
                    ms.err = err;
                    return error.ReadFailed;
                };
            }
        }

        const total = @intFromEnum(limit) - remaining;
        ms.next_read_pos += total;
        return total;
    }

    pub fn logicalPos(ms: *const MsfStream) u64 {
        return ms.next_read_pos - ms.interface.bufferedLen();
    }

    pub fn seekBy(ms: *MsfStream, len: i64) !void {
        ms.next_read_pos = @as(u64, @intCast(@as(i64, @intCast(ms.logicalPos())) + len));
        if (ms.next_read_pos >= ms.blocks.len * ms.block_size) return error.EOF;
        ms.interface.tossBuffered();
    }

    pub fn seekTo(ms: *MsfStream, len: u64) !void {
        ms.next_read_pos = len;
        if (ms.next_read_pos >= ms.blocks.len * ms.block_size) return error.EOF;
        ms.interface.tossBuffered();
    }

    fn getSize(ms: *const MsfStream) u64 {
        return ms.blocks.len * ms.block_size;
    }

    fn getFilePos(ms: *const MsfStream) u64 {
        const pos = ms.logicalPos();
        const block_id = pos / ms.block_size;
        const block = ms.blocks[block_id];
        const offset = pos % ms.block_size;

        return block * ms.block_size + offset;
    }
};

fn readSparseBitVector(reader: *Io.Reader, allocator: Allocator) ![]u32 {
    const num_words = try reader.takeInt(u32, .little);
    var list = std.array_list.Managed(u32).init(allocator);
    errdefer list.deinit();
    var word_i: u32 = 0;
    while (word_i != num_words) : (word_i += 1) {
        const word = try reader.takeInt(u32, .little);
        var bit_i: u5 = 0;
        while (true) : (bit_i += 1) {
            if (word & (@as(u32, 1) << bit_i) != 0) {
                try list.append(word_i * 32 + bit_i);
            }
            if (bit_i == std.math.maxInt(u5)) break;
        }
    }
    return try list.toOwnedSlice();
}

fn blockCountFromSize(size: u32, block_size: u32) u32 {
    return (size + block_size - 1) / block_size;
}



---
File: /std/debug/simple_panic.zig
---

//! This namespace is the default one used by the Zig compiler to emit various
//! kinds of safety panics, due to the logic in `std.builtin.panic`.
//!
//! Since Zig does not have interfaces, this file serves as an example template
//! for users to provide their own alternative panic handling.
//!
//! As an alternative, see `std.debug.FullPanic`.

const std = @import("../std.zig");

/// Prints the message to stderr without a newline and then traps.
///
/// Explicit calls to `@panic` lower to calling this function.
pub fn call(msg: []const u8, ra: ?usize) noreturn {
    @branchHint(.cold);
    _ = ra;
    const stderr_writer = &std.debug.lockStderr(&.{}).file_writer.interface;
    stderr_writer.writeAll(msg) catch {};
    @trap();
}

pub fn sentinelMismatch(expected: anytype, found: @TypeOf(expected)) noreturn {
    _ = found;
    call("sentinel mismatch", null);
}

pub fn unwrapError(err: anyerror) noreturn {
    _ = &err;
    call("attempt to unwrap error", null);
}

pub fn outOfBounds(index: usize, len: usize) noreturn {
    _ = index;
    _ = len;
    call("index out of bounds", null);
}

pub fn startGreaterThanEnd(start: usize, end: usize) noreturn {
    _ = start;
    _ = end;
    call("start index is larger than end index", null);
}

pub fn inactiveUnionField(active: anytype, accessed: @TypeOf(active)) noreturn {
    _ = accessed;
    call("access of inactive union field", null);
}

pub fn sliceCastLenRemainder(src_len: usize) noreturn {
    _ = src_len;
    call("slice length does not divide exactly into destination elements", null);
}

pub fn reachedUnreachable() noreturn {
    call("reached unreachable code", null);
}

pub fn unwrapNull() noreturn {
    call("attempt to use null value", null);
}

pub fn castToNull() noreturn {
    call("cast causes pointer to be null", null);
}

pub fn incorrectAlignment() noreturn {
    call("incorrect alignment", null);
}

pub fn invalidErrorCode() noreturn {
    call("invalid error code", null);
}

pub fn integerOutOfBounds() noreturn {
    call("integer does not fit in destination type", null);
}

pub fn integerOverflow() noreturn {
    call("integer overflow", null);
}

pub fn shlOverflow() noreturn {
    call("left shift overflowed bits", null);
}

pub fn shrOverflow() noreturn {
    call("right shift overflowed bits", null);
}

pub fn divideByZero() noreturn {
    call("division by zero", null);
}

pub fn exactDivisionRemainder() noreturn {
    call("exact division produced remainder", null);
}

pub fn integerPartOutOfBounds() noreturn {
    call("integer part of floating point value out of bounds", null);
}

pub fn corruptSwitch() noreturn {
    call("switch on corrupt value", null);
}

pub fn shiftRhsTooBig() noreturn {
    call("shift amount is greater than the type size", null);
}

pub fn invalidEnumValue() noreturn {
    call("invalid enum value", null);
}

pub fn forLenMismatch() noreturn {
    call("for loop over objects with non-equal lengths", null);
}

pub fn copyLenMismatch() noreturn {
    call("source and destination have non-equal lengths", null);
}

pub fn memcpyAlias() noreturn {
    call("@memcpy arguments alias", null);
}

pub fn noreturnReturned() noreturn {
    call("'noreturn' function returned", null);
}



---
File: /std/dwarf/AT.zig
---

pub const sibling = 0x01;
pub const location = 0x02;
pub const name = 0x03;
pub const ordering = 0x09;
pub const subscr_data = 0x0a;
pub const byte_size = 0x0b;
pub const bit_offset = 0x0c;
pub const bit_size = 0x0d;
pub const element_list = 0x0f;
pub const stmt_list = 0x10;
pub const low_pc = 0x11;
pub const high_pc = 0x12;
pub const language = 0x13;
pub const member = 0x14;
pub const discr = 0x15;
pub const discr_value = 0x16;
pub const visibility = 0x17;
pub const import = 0x18;
pub const string_length = 0x19;
pub const common_reference = 0x1a;
pub const comp_dir = 0x1b;
pub const const_value = 0x1c;
pub const containing_type = 0x1d;
pub const default_value = 0x1e;
pub const @"inline" = 0x20;
pub const is_optional = 0x21;
pub const lower_bound = 0x22;
pub const producer = 0x25;
pub const prototyped = 0x27;
pub const return_addr = 0x2a;
pub const start_scope = 0x2c;
pub const bit_stride = 0x2e;
pub const upper_bound = 0x2f;
pub const abstract_origin = 0x31;
pub const accessibility = 0x32;
pub const address_class = 0x33;
pub const artificial = 0x34;
pub const base_types = 0x35;
pub const calling_convention = 0x36;
pub const count = 0x37;
pub const data_member_location = 0x38;
pub const decl_column = 0x39;
pub const decl_file = 0x3a;
pub const decl_line = 0x3b;
pub const declaration = 0x3c;
pub const discr_list = 0x3d;
pub const encoding = 0x3e;
pub const external = 0x3f;
pub const frame_base = 0x40;
pub const friend = 0x41;
pub const identifier_case = 0x42;
pub const macro_info = 0x43;
pub const namelist_items = 0x44;
pub const priority = 0x45;
pub const segment = 0x46;
pub const specification = 0x47;
pub const static_link = 0x48;
pub const @"type" = 0x49;
pub const use_location = 0x4a;
pub const variable_parameter = 0x4b;
pub const virtuality = 0x4c;
pub const vtable_elem_location = 0x4d;

// DWARF 3 values.
pub const allocated = 0x4e;
pub const associated = 0x4f;
pub const data_location = 0x50;
pub const byte_stride = 0x51;
pub const entry_pc = 0x52;
pub const use_UTF8 = 0x53;
pub const extension = 0x54;
pub const ranges = 0x55;
pub const trampoline = 0x56;
pub const call_column = 0x57;
pub const call_file = 0x58;
pub const call_line = 0x59;
pub const description = 0x5a;
pub const binary_scale = 0x5b;
pub const decimal_scale = 0x5c;
pub const small = 0x5d;
pub const decimal_sign = 0x5e;
pub const digit_count = 0x5f;
pub const picture_string = 0x60;
pub const mutable = 0x61;
pub const threads_scaled = 0x62;
pub const explicit = 0x63;
pub const object_pointer = 0x64;
pub const endianity = 0x65;
pub const elemental = 0x66;
pub const pure = 0x67;
pub const recursive = 0x68;

// DWARF 4.
pub const signature = 0x69;
pub const main_subprogram = 0x6a;
pub const data_bit_offset = 0x6b;
pub const const_expr = 0x6c;
pub const enum_class = 0x6d;
pub const linkage_name = 0x6e;

// DWARF 5
pub const string_length_bit_size = 0x6f;
pub const string_length_byte_size = 0x70;
pub const rank = 0x71;
pub const str_offsets_base = 0x72;
pub const addr_base = 0x73;
pub const rnglists_base = 0x74;
pub const dwo_name = 0x76;
pub const reference = 0x77;
pub const rvalue_reference = 0x78;
pub const macros = 0x79;
pub const call_all_calls = 0x7a;
pub const call_all_source_calls = 0x7b;
pub const call_all_tail_calls = 0x7c;
pub const call_return_pc = 0x7d;
pub const call_value = 0x7e;
pub const call_origin = 0x7f;
pub const call_parameter = 0x80;
pub const call_pc = 0x81;
pub const call_tail_call = 0x82;
pub const call_target = 0x83;
pub const call_target_clobbered = 0x84;
pub const call_data_location = 0x85;
pub const call_data_value = 0x86;
pub const @"noreturn" = 0x87;
pub const alignment = 0x88;
pub const export_symbols = 0x89;
pub const deleted = 0x8a;
pub const defaulted = 0x8b;
pub const loclists_base = 0x8c;

pub const lo_user = 0x2000; // Implementation-defined range start.
pub const hi_user = 0x3fff; // Implementation-defined range end.

// SGI/MIPS extensions.
pub const MIPS_fde = 0x2001;
pub const MIPS_loop_begin = 0x2002;
pub const MIPS_tail_loop_begin = 0x2003;
pub const MIPS_epilog_begin = 0x2004;
pub const MIPS_loop_unroll_factor = 0x2005;
pub const MIPS_software_pipeline_depth = 0x2006;
pub const MIPS_linkage_name = 0x2007;
pub const MIPS_stride = 0x2008;
pub const MIPS_abstract_name = 0x2009;
pub const MIPS_clone_origin = 0x200a;
pub const MIPS_has_inlines = 0x200b;

// HP extensions.
pub const HP_block_index = 0x2000;
pub const HP_unmodifiable = 0x2001; // Same as AT.MIPS_fde.
pub const HP_prologue = 0x2005; // Same as AT.MIPS_loop_unroll.
pub const HP_epilogue = 0x2008; // Same as AT.MIPS_stride.
pub const HP_actuals_stmt_list = 0x2010;
pub const HP_proc_per_section = 0x2011;
pub const HP_raw_data_ptr = 0x2012;
pub const HP_pass_by_reference = 0x2013;
pub const HP_opt_level = 0x2014;
pub const HP_prof_version_id = 0x2015;
pub const HP_opt_flags = 0x2016;
pub const HP_cold_region_low_pc = 0x2017;
pub const HP_cold_region_high_pc = 0x2018;
pub const HP_all_variables_modifiable = 0x2019;
pub const HP_linkage_name = 0x201a;
pub const HP_prof_flags = 0x201b; // In comp unit of procs_info for -g.
pub const HP_unit_name = 0x201f;
pub const HP_unit_size = 0x2020;
pub const HP_widened_byte_size = 0x2021;
pub const HP_definition_points = 0x2022;
pub const HP_default_location = 0x2023;
pub const HP_is_result_param = 0x2029;

// GNU extensions.
pub const sf_names = 0x2101;
pub const src_info = 0x2102;
pub const mac_info = 0x2103;
pub const src_coords = 0x2104;
pub const body_begin = 0x2105;
pub const body_end = 0x2106;
pub const GNU_vector = 0x2107;
// Thread-safety annotations.
// See http://gcc.gnu.org/wiki/ThreadSafetyAnnotation .
pub const GNU_guarded_by = 0x2108;
pub const GNU_pt_guarded_by = 0x2109;
pub const GNU_guarded = 0x210a;
pub const GNU_pt_guarded = 0x210b;
pub const GNU_locks_excluded = 0x210c;
pub const GNU_exclusive_locks_required = 0x210d;
pub const GNU_shared_locks_required = 0x210e;
// One-definition rule violation detection.
// See http://gcc.gnu.org/wiki/DwarfSeparateTypeInfo .
pub const GNU_odr_signature = 0x210f;
// Template template argument name.
// See http://gcc.gnu.org/wiki/TemplateParmsDwarf .
pub const GNU_template_name = 0x2110;
// The GNU call site extension.
// See http://www.dwarfstd.org/ShowIssue.php?issue=100909.2&type=open .
pub const GNU_call_site_value = 0x2111;
pub const GNU_call_site_data_value = 0x2112;
pub const GNU_call_site_target = 0x2113;
pub const GNU_call_site_target_clobbered = 0x2114;
pub const GNU_tail_call = 0x2115;
pub const GNU_all_tail_call_sites = 0x2116;
pub const GNU_all_call_sites = 0x2117;
pub const GNU_all_source_call_sites = 0x2118;
// Section offset into .debug_macro section.
pub const GNU_macros = 0x2119;
// Extensions for Fission.  See http://gcc.gnu.org/wiki/DebugFission.
pub const GNU_dwo_name = 0x2130;
pub const GNU_dwo_id = 0x2131;
pub const GNU_ranges_base = 0x2132;
pub const GNU_addr_base = 0x2133;
pub const GNU_pubnames = 0x2134;
pub const GNU_pubtypes = 0x2135;
// VMS extensions.
pub const VMS_rtnbeg_pd_address = 0x2201;
// GNAT extensions.
// GNAT descriptive type.
// See http://gcc.gnu.org/wiki/DW_AT_GNAT_descriptive_type .
pub const use_GNAT_descriptive_type = 0x2301;
pub const GNAT_descriptive_type = 0x2302;

// Zig extensions.
pub const ZIG_parent = 0x2ccd;
pub const ZIG_padding = 0x2cce;
pub const ZIG_relative_decl = 0x2cd0;
pub const ZIG_decl_line_relative = 0x2cd1;
pub const ZIG_comptime_value = 0x2cd2;
pub const ZIG_sentinel = 0x2ce2;

// UPC extension.
pub const upc_threads_scaled = 0x3210;
// PGI (STMicroelectronics) extensions.
pub const PGI_lbase = 0x3a00;
pub const PGI_soffset = 0x3a01;
pub const PGI_lstride = 0x3a02;



---
File: /std/dwarf/ATE.zig
---

pub const @"void" = 0x0;
pub const address = 0x1;
pub const boolean = 0x2;
pub const complex_float = 0x3;
pub const float = 0x4;
pub const signed = 0x5;
pub const signed_char = 0x6;
pub const unsigned = 0x7;
pub const unsigned_char = 0x8;

// DWARF 3.
pub const imaginary_float = 0x9;
pub const packed_decimal = 0xa;
pub const numeric_string = 0xb;
pub const edited = 0xc;
pub const signed_fixed = 0xd;
pub const unsigned_fixed = 0xe;
pub const decimal_float = 0xf;

// DWARF 4.
pub const UTF = 0x10;

// DWARF 5.
pub const UCS = 0x11;
pub const ASCII = 0x12;

pub const lo_user = 0x80;
pub const hi_user = 0xff;

// HP extensions.
pub const HP_float80 = 0x80; // Floating-point (80 bit).
pub const HP_complex_float80 = 0x81; // Complex floating-point (80 bit).
pub const HP_float128 = 0x82; // Floating-point (128 bit).
pub const HP_complex_float128 = 0x83; // Complex fp (128 bit).
pub const HP_floathpintel = 0x84; // Floating-point (82 bit IA64).
pub const HP_imaginary_float80 = 0x85;
pub const HP_imaginary_float128 = 0x86;
pub const HP_VAX_float = 0x88; // F or G floating.
pub const HP_VAX_float_d = 0x89; // D floating.
pub const HP_packed_decimal = 0x8a; // Cobol.
pub const HP_zoned_decimal = 0x8b; // Cobol.
pub const HP_edited = 0x8c; // Cobol.
pub const HP_signed_fixed = 0x8d; // Cobol.
pub const HP_unsigned_fixed = 0x8e; // Cobol.
pub const HP_VAX_complex_float = 0x8f; // F or G floating complex.
pub const HP_VAX_complex_float_d = 0x90; // D floating complex.



---
File: /std/dwarf/EH.zig
---

pub const PE = packed struct(u8) {
    type: Type,
    rel: Rel,
    /// Undocumented GCC extension
    indirect: bool = false,

    /// This is a special encoding which does not correspond to named `type`/`rel` values.
    pub const omit: PE = @bitCast(@as(u8, 0xFF));

    pub const Type = enum(u4) {
        absptr = 0x0,
        uleb128 = 0x1,
        udata2 = 0x2,
        udata4 = 0x3,
        udata8 = 0x4,
        sleb128 = 0x9,
        sdata2 = 0xA,
        sdata4 = 0xB,
        sdata8 = 0xC,
        _,
    };

    /// The specification considers this a `u4`, but the GCC `indirect` field extension conflicts
    /// with that, so we consider it a `u3` instead.
    pub const Rel = enum(u3) {
        abs = 0x0,
        pcrel = 0x1,
        textrel = 0x2,
        datarel = 0x3,
        funcrel = 0x4,
        aligned = 0x5,
        _,
    };
};



---
File: /std/dwarf/FORM.zig
---

pub const addr = 0x01;
pub const block2 = 0x03;
pub const block4 = 0x04;
pub const data2 = 0x05;
pub const data4 = 0x06;
pub const data8 = 0x07;
pub const string = 0x08;
pub const block = 0x09;
pub const block1 = 0x0a;
pub const data1 = 0x0b;
pub const flag = 0x0c;
pub const sdata = 0x0d;
pub const strp = 0x0e;
pub const udata = 0x0f;
pub const ref_addr = 0x10;
pub const ref1 = 0x11;
pub const ref2 = 0x12;
pub const ref4 = 0x13;
pub const ref8 = 0x14;
pub const ref_udata = 0x15;
pub const indirect = 0x16;
pub const sec_offset = 0x17;
pub const exprloc = 0x18;
pub const flag_present = 0x19;
pub const strx = 0x1a;
pub const addrx = 0x1b;
pub const ref_sup4 = 0x1c;
pub const strp_sup = 0x1d;
pub const data16 = 0x1e;
pub const line_strp = 0x1f;
pub const ref_sig8 = 0x20;
pub const implicit_const = 0x21;
pub const loclistx = 0x22;
pub const rnglistx = 0x23;
pub const ref_sup8 = 0x24;
pub const strx1 = 0x25;
pub const strx2 = 0x26;
pub const strx3 = 0x27;
pub const strx4 = 0x28;
pub const addrx1 = 0x29;
pub const addrx2 = 0x2a;
pub const addrx3 = 0x2b;
pub const addrx4 = 0x2c;

// Extensions for Fission.  See http://gcc.gnu.org/wiki/DebugFission.
pub const GNU_addr_index = 0x1f01;
pub const GNU_str_index = 0x1f02;

// Extensions for DWZ multifile.
// See http://www.dwarfstd.org/ShowIssue.php?issue=120604.1&type=open .
pub const GNU_ref_alt = 0x1f20;
pub const GNU_strp_alt = 0x1f21;



---
File: /std/dwarf/LANG.zig
---

pub const C89 = 0x0001;
pub const C = 0x0002;
pub const Ada83 = 0x0003;
pub const C_plus_plus = 0x0004;
pub const Cobol74 = 0x0005;
pub const Cobol85 = 0x0006;
pub const Fortran77 = 0x0007;
pub const Fortran90 = 0x0008;
pub const Pascal83 = 0x0009;
pub const Modula2 = 0x000a;
pub const Java = 0x000b;
pub const C99 = 0x000c;
pub const Ada95 = 0x000d;
pub const Fortran95 = 0x000e;
pub const PLI = 0x000f;
pub const ObjC = 0x0010;
pub const ObjC_plus_plus = 0x0011;
pub const UPC = 0x0012;
pub const D = 0x0013;
pub const Python = 0x0014;
pub const OpenCL = 0x0015;
pub const Go = 0x0016;
pub const Modula3 = 0x0017;
pub const Haskell = 0x0018;
pub const C_plus_plus_03 = 0x0019;
pub const C_plus_plus_11 = 0x001a;
pub const OCaml = 0x001b;
pub const Rust = 0x001c;
pub const C11 = 0x001d;
pub const Swift = 0x001e;
pub const Julia = 0x001f;
pub const Dylan = 0x0020;
pub const C_plus_plus_14 = 0x0021;
pub const Fortran03 = 0x0022;
pub const Fortran08 = 0x0023;
pub const RenderScript = 0x0024;
pub const BLISS = 0x0025;
pub const Kotlin = 0x0026;
pub const Zig = 0x0027;
pub const Crystal = 0x0028;
pub const C_plus_plus_17 = 0x002a;
pub const C_plus_plus_20 = 0x002b;
pub const C17 = 0x002c;
pub const Fortran18 = 0x002d;
pub const Ada2005 = 0x002e;
pub const Ada2012 = 0x002f;
pub const HIP = 0x0030;
pub const Assembly = 0x0031;
pub const C_sharp = 0x0032;
pub const Mojo = 0x0033;
pub const GLSL = 0x0034;
pub const GLSL_ES = 0x0035;
pub const HLSL = 0x0036;
pub const OpenCL_CPP = 0x0037;
pub const CPP_for_OpenCL = 0x0038;
pub const SYCL = 0x0039;
pub const C_plus_plus_23 = 0x003a;
pub const Odin = 0x003b;
pub const Ruby = 0x0040;
pub const Move = 0x0041;
pub const Hylo = 0x0042;

pub const lo_user = 0x8000;
pub const hi_user = 0xffff;

pub const Mips_Assembler = 0x8001;
pub const Upc = 0x8765;
pub const HP_Bliss = 0x8003;
pub const HP_Basic91 = 0x8004;
pub const HP_Pascal91 = 0x8005;
pub const HP_IMacro = 0x8006;
pub const HP_Assembler = 0x8007;



---
File: /std/dwarf/OP.zig
---

pub const addr = 0x03;
pub const deref = 0x06;
pub const const1u = 0x08;
pub const const1s = 0x09;
pub const const2u = 0x0a;
pub const const2s = 0x0b;
pub const const4u = 0x0c;
pub const const4s = 0x0d;
pub const const8u = 0x0e;
pub const const8s = 0x0f;
pub const constu = 0x10;
pub const consts = 0x11;
pub const dup = 0x12;
pub const drop = 0x13;
pub const over = 0x14;
pub const pick = 0x15;
pub const swap = 0x16;
pub const rot = 0x17;
pub const xderef = 0x18;
pub const abs = 0x19;
pub const @"and" = 0x1a;
pub const div = 0x1b;
pub const minus = 0x1c;
pub const mod = 0x1d;
pub const mul = 0x1e;
pub const neg = 0x1f;
pub const not = 0x20;
pub const @"or" = 0x21;
pub const plus = 0x22;
pub const plus_uconst = 0x23;
pub const shl = 0x24;
pub const shr = 0x25;
pub const shra = 0x26;
pub const xor = 0x27;
pub const bra = 0x28;
pub const eq = 0x29;
pub const ge = 0x2a;
pub const gt = 0x2b;
pub const le = 0x2c;
pub const lt = 0x2d;
pub const ne = 0x2e;
pub const skip = 0x2f;
pub const lit0 = 0x30;
pub const lit1 = 0x31;
pub const lit2 = 0x32;
pub const lit3 = 0x33;
pub const lit4 = 0x34;
pub const lit5 = 0x35;
pub const lit6 = 0x36;
pub const lit7 = 0x37;
pub const lit8 = 0x38;
pub const lit9 = 0x39;
pub const lit10 = 0x3a;
pub const lit11 = 0x3b;
pub const lit12 = 0x3c;
pub const lit13 = 0x3d;
pub const lit14 = 0x3e;
pub const lit15 = 0x3f;
pub const lit16 = 0x40;
pub const lit17 = 0x41;
pub const lit18 = 0x42;
pub const lit19 = 0x43;
pub const lit20 = 0x44;
pub const lit21 = 0x45;
pub const lit22 = 0x46;
pub const lit23 = 0x47;
pub const lit24 = 0x48;
pub const lit25 = 0x49;
pub const lit26 = 0x4a;
pub const lit27 = 0x4b;
pub const lit28 = 0x4c;
pub const lit29 = 0x4d;
pub const lit30 = 0x4e;
pub const lit31 = 0x4f;
pub const reg0 = 0x50;
pub const reg1 = 0x51;
pub const reg2 = 0x52;
pub const reg3 = 0x53;
pub const reg4 = 0x54;
pub const reg5 = 0x55;
pub const reg6 = 0x56;
pub const reg7 = 0x57;
pub const reg8 = 0x58;
pub const reg9 = 0x59;
pub const reg10 = 0x5a;
pub const reg11 = 0x5b;
pub const reg12 = 0x5c;
pub const reg13 = 0x5d;
pub const reg14 = 0x5e;
pub const reg15 = 0x5f;
pub const reg16 = 0x60;
pub const reg17 = 0x61;
pub const reg18 = 0x62;
pub const reg19 = 0x63;
pub const reg20 = 0x64;
pub const reg21 = 0x65;
pub const reg22 = 0x66;
pub const reg23 = 0x67;
pub const reg24 = 0x68;
pub const reg25 = 0x69;
pub const reg26 = 0x6a;
pub const reg27 = 0x6b;
pub const reg28 = 0x6c;
pub const reg29 = 0x6d;
pub const reg30 = 0x6e;
pub const reg31 = 0x6f;
pub const breg0 = 0x70;
pub const breg1 = 0x71;
pub const breg2 = 0x72;
pub const breg3 = 0x73;
pub const breg4 = 0x74;
pub const breg5 = 0x75;
pub const breg6 = 0x76;
pub const breg7 = 0x77;
pub const breg8 = 0x78;
pub const breg9 = 0x79;
pub const breg10 = 0x7a;
pub const breg11 = 0x7b;
pub const breg12 = 0x7c;
pub const breg13 = 0x7d;
pub const breg14 = 0x7e;
pub const breg15 = 0x7f;
pub const breg16 = 0x80;
pub const breg17 = 0x81;
pub const breg18 = 0x82;
pub const breg19 = 0x83;
pub const breg20 = 0x84;
pub const breg21 = 0x85;
pub const breg22 = 0x86;
pub const breg23 = 0x87;
pub const breg24 = 0x88;
pub const breg25 = 0x89;
pub const breg26 = 0x8a;
pub const breg27 = 0x8b;
pub const breg28 = 0x8c;
pub const breg29 = 0x8d;
pub const breg30 = 0x8e;
pub const breg31 = 0x8f;
pub const regx = 0x90;
pub const fbreg = 0x91;
pub const bregx = 0x92;
pub const piece = 0x93;
pub const deref_size = 0x94;
pub const xderef_size = 0x95;
pub const nop = 0x96;

// DWARF 3 extensions.
pub const push_object_address = 0x97;
pub const call2 = 0x98;
pub const call4 = 0x99;
pub const call_ref = 0x9a;
pub const form_tls_address = 0x9b;
pub const call_frame_cfa = 0x9c;
pub const bit_piece = 0x9d;

// DWARF 4 extensions.
pub const implicit_value = 0x9e;
pub const stack_value = 0x9f;

// DWARF 5 extensions.
pub const implicit_pointer = 0xa0;
pub const addrx = 0xa1;
pub const constx = 0xa2;
pub const entry_value = 0xa3;
pub const const_type = 0xa4;
pub const regval_type = 0xa5;
pub const deref_type = 0xa6;
pub const xderef_type = 0xa7;
pub const convert = 0xa8;
pub const reinterpret = 0xa9;

pub const lo_user = 0xe0; // Implementation-defined range start.
pub const hi_user = 0xff; // Implementation-defined range end.

// GNU extensions.
pub const GNU_push_tls_address = 0xe0;
// The following is for marking variables that are uninitialized.
pub const GNU_uninit = 0xf0;
pub const GNU_encoded_addr = 0xf1;
// The GNU implicit pointer extension.
// See http://www.dwarfstd.org/ShowIssue.php?issue=100831.1&type=open .
pub const GNU_implicit_pointer = 0xf2;
// The GNU entry value extension.
// See http://www.dwarfstd.org/ShowIssue.php?issue=100909.1&type=open .
pub const GNU_entry_value = 0xf3;
// The GNU typed stack extension.
// See http://www.dwarfstd.org/doc/040408.1.html .
pub const GNU_const_type = 0xf4;
pub const GNU_regval_type = 0xf5;
pub const GNU_deref_type = 0xf6;
pub const GNU_convert = 0xf7;
pub const GNU_reinterpret = 0xf9;
// The GNU parameter ref extension.
pub const GNU_parameter_ref = 0xfa;
// Extension for Fission.  See http://gcc.gnu.org/wiki/DebugFission.
pub const GNU_addr_index = 0xfb;
pub const GNU_const_index = 0xfc;
// HP extensions.
pub const HP_unknown = 0xe0; // Ouch, the same as GNU_push_tls_address.
pub const HP_is_value = 0xe1;
pub const HP_fltconst4 = 0xe2;
pub const HP_fltconst8 = 0xe3;
pub const HP_mod_range = 0xe4;
pub const HP_unmod_range = 0xe5;
pub const HP_tls = 0xe6;
// PGI (STMicroelectronics) extensions.
pub const PGI_omp_thread_num = 0xf8;
// Wasm extensions.
pub const WASM_location = 0xed;
pub const WASM_local = 0x00;
pub const WASM_global = 0x01;
pub const WASM_global_u32 = 0x03;
pub const WASM_operand_stack = 0x02;



---
File: /std/dwarf/TAG.zig
---

pub const padding = 0x00;
pub const array_type = 0x01;
pub const class_type = 0x02;
pub const entry_point = 0x03;
pub const enumeration_type = 0x04;
pub const formal_parameter = 0x05;
pub const imported_declaration = 0x08;
pub const label = 0x0a;
pub const lexical_block = 0x0b;
pub const member = 0x0d;
pub const pointer_type = 0x0f;
pub const reference_type = 0x10;
pub const compile_unit = 0x11;
pub const string_type = 0x12;
pub const structure_type = 0x13;
pub const subroutine = 0x14;
pub const subroutine_type = 0x15;
pub const typedef = 0x16;
pub const union_type = 0x17;
pub const unspecified_parameters = 0x18;
pub const variant = 0x19;
pub const common_block = 0x1a;
pub const common_inclusion = 0x1b;
pub const inheritance = 0x1c;
pub const inlined_subroutine = 0x1d;
pub const module = 0x1e;
pub const ptr_to_member_type = 0x1f;
pub const set_type = 0x20;
pub const subrange_type = 0x21;
pub const with_stmt = 0x22;
pub const access_declaration = 0x23;
pub const base_type = 0x24;
pub const catch_block = 0x25;
pub const const_type = 0x26;
pub const constant = 0x27;
pub const enumerator = 0x28;
pub const file_type = 0x29;
pub const friend = 0x2a;
pub const namelist = 0x2b;
pub const namelist_item = 0x2c;
pub const packed_type = 0x2d;
pub const subprogram = 0x2e;
pub const template_type_param = 0x2f;
pub const template_value_param = 0x30;
pub const thrown_type = 0x31;
pub const try_block = 0x32;
pub const variant_part = 0x33;
pub const variable = 0x34;
pub const volatile_type = 0x35;

// DWARF 3
pub const dwarf_procedure = 0x36;
pub const restrict_type = 0x37;
pub const interface_type = 0x38;
pub const namespace = 0x39;
pub const imported_module = 0x3a;
pub const unspecified_type = 0x3b;
pub const partial_unit = 0x3c;
pub const imported_unit = 0x3d;
pub const condition = 0x3f;
pub const shared_type = 0x40;

// DWARF 4
pub const type_unit = 0x41;
pub const rvalue_reference_type = 0x42;
pub const template_alias = 0x43;

// DWARF 5
pub const coarray_type = 0x44;
pub const generic_subrange = 0x45;
pub const dynamic_type = 0x46;
pub const atomic_type = 0x47;
pub const call_site = 0x48;
pub const call_site_parameter = 0x49;
pub const skeleton_unit = 0x4a;
pub const immutable_type = 0x4b;

pub const lo_user = 0x4080;
pub const hi_user = 0xffff;

// SGI/MIPS Extensions.
pub const MIPS_loop = 0x4081;

// HP extensions.  See: ftp://ftp.hp.com/pub/lang/tools/WDB/wdb-4.0.tar.gz .
pub const HP_array_descriptor = 0x4090;
pub const HP_Bliss_field = 0x4091;
pub const HP_Bliss_field_set = 0x4092;

// GNU extensions.
pub const format_label = 0x4101; // For FORTRAN 77 and Fortran 90.
pub const function_template = 0x4102; // For C++.
pub const class_template = 0x4103; //For C++.
pub const GNU_BINCL = 0x4104;
pub const GNU_EINCL = 0x4105;

// Template template parameter.
// See http://gcc.gnu.org/wiki/TemplateParmsDwarf .
pub const GNU_template_template_param = 0x4106;

// Template parameter pack extension = specified at
// http://wiki.dwarfstd.org/index.php?title=C%2B%2B0x:_Variadic_templates
// The values of these two TAGS are in the DW_TAG_GNU_* space until the tags
// are properly part of DWARF 5.
pub const GNU_template_parameter_pack = 0x4107;
pub const GNU_formal_parameter_pack = 0x4108;
// The GNU call site extension = specified at
// http://www.dwarfstd.org/ShowIssue.php?issue=100909.2&type=open .
// The values of these two TAGS are in the DW_TAG_GNU_* space until the tags
// are properly part of DWARF 5.
pub const GNU_call_site = 0x4109;
pub const GNU_call_site_parameter = 0x410a;
// Extensions for UPC.  See: http://dwarfstd.org/doc/DWARF4.pdf.
pub const upc_shared_type = 0x8765;
pub const upc_strict_type = 0x8766;
pub const upc_relaxed_type = 0x8767;
// PGI (STMicroelectronics; extensions.  No documentation available.
pub const PGI_kanji_type = 0xA000;
pub const PGI_interface_block = 0xA020;

// ZIG extensions.
pub const ZIG_padding = 0xfdb1;
pub const ZIG_comptime_value = 0xfdb2;



---
File: /std/fmt/parse_float/common.zig
---

const std = @import("std");

/// A custom N-bit floating point type, representing `f * 2^e`.
/// e is biased, so it be directly shifted into the exponent bits.
/// Negative exponent indicates an invalid result.
pub fn BiasedFp(comptime T: type) type {
    const MantissaT = mantissaType(T);

    return struct {
        const Self = @This();

        /// The significant digits.
        f: MantissaT,
        /// The biased, binary exponent.
        e: i32,

        pub fn zero() Self {
            return .{ .f = 0, .e = 0 };
        }

        pub fn zeroPow2(e: i32) Self {
            return .{ .f = 0, .e = e };
        }

        pub fn inf(comptime FloatT: type) Self {
            const e = (1 << std.math.floatExponentBits(FloatT)) - 1;
            return switch (FloatT) {
                f80 => .{ .f = 0x8000000000000000, .e = e },
                else => .{ .f = 0, .e = e },
            };
        }

        pub fn eql(self: Self, other: Self) bool {
            return self.f == other.f and self.e == other.e;
        }

        pub fn toFloat(self: Self, comptime FloatT: type, negative: bool) FloatT {
            var word = self.f;
            word |= @as(MantissaT, @intCast(self.e)) << std.math.floatMantissaBits(FloatT);
            var f = floatFromUnsigned(FloatT, MantissaT, word);
            if (negative) f = -f;
            return f;
        }
    };
}

pub fn floatFromUnsigned(comptime T: type, comptime MantissaT: type, v: MantissaT) T {
    return switch (T) {
        f16 => @as(f16, @bitCast(@as(u16, @truncate(v)))),
        f32 => @as(f32, @bitCast(@as(u32, @truncate(v)))),
        f64 => @as(f64, @bitCast(@as(u64, @truncate(v)))),
        f80 => @as(f80, @bitCast(@as(u80, @truncate(v)))),
        f128 => @as(f128, @bitCast(v)),
        else => unreachable,
    };
}

/// Represents a parsed floating point value as its components.
pub fn Number(comptime T: type) type {
    return struct {
        exponent: i64,
        mantissa: mantissaType(T),
        negative: bool,
        /// More than max_mantissa digits were found during parse
        many_digits: bool,
        /// The number was a hex-float (e.g. 0x1.234p567)
        hex: bool,
    };
}

/// Determine if 8 bytes are all decimal digits.
/// This does not care about the order in which the bytes were loaded.
pub fn isEightDigits(v: u64) bool {
    const a = v +% 0x4646_4646_4646_4646;
    const b = v -% 0x3030_3030_3030_3030;
    return ((a | b) & 0x8080_8080_8080_8080) == 0;
}

pub fn isDigit(c: u8, comptime base: u8) bool {
    std.debug.assert(base == 10 or base == 16);

    return if (base == 10)
        '0' <= c and c <= '9'
    else
        '0' <= c and c <= '9' or 'a' <= c and c <= 'f' or 'A' <= c and c <= 'F';
}

/// Returns the underlying storage type used for the mantissa of floating-point type.
/// The output unsigned type must have at least as many bits as the input floating-point type.
pub fn mantissaType(comptime T: type) type {
    return switch (T) {
        f16, f32, f64 => u64,
        f80, f128 => u128,
        else => unreachable,
    };
}



---
File: /std/fmt/parse_float/convert_eisel_lemire.zig
---

const std = @import("std");
const math = std.math;
const common = @import("common.zig");
const FloatInfo = @import("FloatInfo.zig");
const BiasedFp = common.BiasedFp;
const Number = common.Number;

/// Compute a float using an extended-precision representation.
///
/// Fast conversion of a the significant digits and decimal exponent
/// a float to an extended representation with a binary float. This
/// algorithm will accurately parse the vast majority of cases,
/// and uses a 128-bit representation (with a fallback 192-bit
/// representation).
///
/// This algorithm scales the exponent by the decimal exponent
/// using pre-computed powers-of-5, and calculates if the
/// representation can be unambiguously rounded to the nearest
/// machine float. Near-halfway cases are not handled here,
/// and are represented by a negative, biased binary exponent.
///
/// The algorithm is described in detail in "Daniel Lemire, Number Parsing
/// at a Gigabyte per Second" in section 5, "Fast Algorithm", and
/// section 6, "Exact Numbers And Ties", available online:
/// <https://arxiv.org/abs/2101.11408.pdf>.
pub fn convertEiselLemire(comptime T: type, q: i64, w_: u64) ?BiasedFp(f64) {
    std.debug.assert(T == f16 or T == f32 or T == f64);
    var w = w_;
    const float_info = FloatInfo.from(T);

    // Short-circuit if the value can only be a literal 0 or infinity.
    if (w == 0 or q < float_info.smallest_power_of_ten) {
        return BiasedFp(f64).zero();
    } else if (q > float_info.largest_power_of_ten) {
        return BiasedFp(f64).inf(T);
    }

    // Normalize our significant digits, so the most-significant bit is set.
    const lz = @clz(@as(u64, @bitCast(w)));
    w = math.shl(u64, w, lz);

    const r = computeProductApprox(q, w, float_info.mantissa_explicit_bits + 3);
    if (r.lo == 0xffff_ffff_ffff_ffff) {
        // If we have failed to approximate w x 5^-q with our 128-bit value.
        // Since the addition of 1 could lead to an overflow which could then
        // round up over the half-way point, this can lead to improper rounding
        // of a float.
        //
        // However, this can only occur if q ∈ [-27, 55]. The upper bound of q
        // is 55 because 5^55 < 2^128, however, this can only happen if 5^q > 2^64,
        // since otherwise the product can be represented in 64-bits, producing
        // an exact result. For negative exponents, rounding-to-even can
        // only occur if 5^-q < 2^64.
        //
        // For detailed explanations of rounding for negative exponents, see
        // <https://arxiv.org/pdf/2101.11408.pdf#section.9.1>. For detailed
        // explanations of rounding for positive exponents, see
        // <https://arxiv.org/pdf/2101.11408.pdf#section.8>.
        const inside_safe_exponent = q >= -27 and q <= 55;
        if (!inside_safe_exponent) {
            return null;
        }
    }

    const upper_bit = @as(i32, @intCast(r.hi >> 63));
    var mantissa = math.shr(u64, r.hi, upper_bit + 64 - @as(i32, @intCast(float_info.mantissa_explicit_bits)) - 3);
    var power2 = power(@as(i32, @intCast(q))) + upper_bit - @as(i32, @intCast(lz)) - float_info.minimum_exponent;
    if (power2 <= 0) {
        if (-power2 + 1 >= 64) {
            // Have more than 64 bits below the minimum exponent, must be 0.
            return BiasedFp(f64).zero();
        }
        // Have a subnormal value.
        mantissa = math.shr(u64, mantissa, -power2 + 1);
        mantissa += mantissa & 1;
        mantissa >>= 1;
        power2 = @intFromBool(mantissa >= (1 << float_info.mantissa_explicit_bits));
        return BiasedFp(f64){ .f = mantissa, .e = power2 };
    }

    // Need to handle rounding ties. Normally, we need to round up,
    // but if we fall right in between and and we have an even basis, we
    // need to round down.
    //
    // This will only occur if:
    //  1. The lower 64 bits of the 128-bit representation is 0.
    //      IE, 5^q fits in single 64-bit word.
    //  2. The least-significant bit prior to truncated mantissa is odd.
    //  3. All the bits truncated when shifting to mantissa bits + 1 are 0.
    //
    // Or, we may fall between two floats: we are exactly halfway.
    if (r.lo <= 1 and
        q >= float_info.min_exponent_round_to_even and
        q <= float_info.max_exponent_round_to_even and
        mantissa & 3 == 1 and
        math.shl(u64, mantissa, (upper_bit + 64 - @as(i32, @intCast(float_info.mantissa_explicit_bits)) - 3)) == r.hi)
    {
        // Zero the lowest bit, so we don't round up.
        mantissa &= ~@as(u64, 1);
    }

    // Round-to-even, then shift the significant digits into place.
    mantissa += mantissa & 1;
    mantissa >>= 1;
    if (mantissa >= 2 << float_info.mantissa_explicit_bits) {
        // Rounding up overflowed, so the carry bit is set. Set the
        // mantissa to 1 (only the implicit, hidden bit is set) and
        // increase the exponent.
        mantissa = 1 << float_info.mantissa_explicit_bits;
        power2 += 1;
    }

    // Zero out the hidden bit
    mantissa &= ~(@as(u64, 1) << float_info.mantissa_explicit_bits);
    if (power2 >= float_info.infinite_power) {
        // Exponent is above largest normal value, must be infinite
        return BiasedFp(f64).inf(T);
    }

    return BiasedFp(f64){ .f = mantissa, .e = power2 };
}

/// Calculate a base 2 exponent from a decimal exponent.
/// This uses a pre-computed integer approximation for
/// log2(10), where 217706 / 2^16 is accurate for the
/// entire range of non-finite decimal exponents.
fn power(q: i32) i32 {
    return ((q *% (152170 + 65536)) >> 16) + 63;
}

const U128 = struct {
    lo: u64,
    hi: u64,

    pub fn new(lo: u64, hi: u64) U128 {
        return .{ .lo = lo, .hi = hi };
    }

    pub fn mul(a: u64, b: u64) U128 {
        const x = @as(u128, a) * b;
        return .{
            .hi = @as(u64, @truncate(x >> 64)),
            .lo = @as(u64, @truncate(x)),
        };
    }
};

// This will compute or rather approximate w * 5**q and return a pair of 64-bit words
// approximating the result, with the "high" part corresponding to the most significant
// bits and the low part corresponding to the least significant bits.
fn computeProductApprox(q: i64, w: u64, comptime precision: usize) U128 {
    std.debug.assert(q >= eisel_lemire_smallest_power_of_five);
    std.debug.assert(q <= eisel_lemire_largest_power_of_five);
    std.debug.assert(precision <= 64);

    const mask = if (precision < 64)
        0xffff_ffff_ffff_ffff >> precision
    else
        0xffff_ffff_ffff_ffff;

    // 5^q < 2^64, then the multiplication always provides an exact value.
    // That means whenever we need to round ties to even, we always have
    // an exact value.
    const index = @as(usize, @intCast(q - @as(i64, @intCast(eisel_lemire_smallest_power_of_five))));
    const pow5 = eisel_lemire_table_powers_of_five_128[index];

    // Only need one multiplication as long as there is 1 zero but
    // in the explicit mantissa bits, +1 for the hidden bit, +1 to
    // determine the rounding direction, +1 for if the computed
    // product has a leading zero.
    var first = U128.mul(w, pow5.lo);
    if (first.hi & mask == mask) {
        // Need to do a second multiplication to get better precision
        // for the lower product. This will always be exact
        // where q is < 55, since 5^55 < 2^128. If this wraps,
        // then we need to need to round up the hi product.
        const second = U128.mul(w, pow5.hi);

        first.lo +%= second.hi;
        if (second.hi > first.lo) {
            first.hi += 1;
        }
    }

    return .{ .lo = first.lo, .hi = first.hi };
}

// Eisel-Lemire tables ~10Kb
const eisel_lemire_smallest_power_of_five = -342;
const eisel_lemire_largest_power_of_five = 308;
const eisel_lemire_table_powers_of_five_128 = [_]U128{
    U128.new(0xeef453d6923bd65a, 0x113faa2906a13b3f), // 5^-342
    U128.new(0x9558b4661b6565f8, 0x4ac7ca59a424c507), // 5^-341
    U128.new(0xbaaee17fa23ebf76, 0x5d79bcf00d2df649), // 5^-340
    U128.new(0xe95a99df8ace6f53, 0xf4d82c2c107973dc), // 5^-339
    U128.new(0x91d8a02bb6c10594, 0x79071b9b8a4be869), // 5^-338
    U128.new(0xb64ec836a47146f9, 0x9748e2826cdee284), // 5^-337
    U128.new(0xe3e27a444d8d98b7, 0xfd1b1b2308169b25), // 5^-336
    U128.new(0x8e6d8c6ab0787f72, 0xfe30f0f5e50e20f7), // 5^-335
    U128.new(0xb208ef855c969f4f, 0xbdbd2d335e51a935), // 5^-334
    U128.new(0xde8b2b66b3bc4723, 0xad2c788035e61382), // 5^-333
    U128.new(0x8b16fb203055ac76, 0x4c3bcb5021afcc31), // 5^-332
    U128.new(0xaddcb9e83c6b1793, 0xdf4abe242a1bbf3d), // 5^-331
    U128.new(0xd953e8624b85dd78, 0xd71d6dad34a2af0d), // 5^-330
    U128.new(0x87d4713d6f33aa6b, 0x8672648c40e5ad68), // 5^-329
    U128.new(0xa9c98d8ccb009506, 0x680efdaf511f18c2), // 5^-328
    U128.new(0xd43bf0effdc0ba48, 0x212bd1b2566def2), // 5^-327
    U128.new(0x84a57695fe98746d, 0x14bb630f7604b57), // 5^-326
    U128.new(0xa5ced43b7e3e9188, 0x419ea3bd35385e2d), // 5^-325
    U128.new(0xcf42894a5dce35ea, 0x52064cac828675b9), // 5^-324
    U128.new(0x818995ce7aa0e1b2, 0x7343efebd1940993), // 5^-323
    U128.new(0xa1ebfb4219491a1f, 0x1014ebe6c5f90bf8), // 5^-322
    U128.new(0xca66fa129f9b60a6, 0xd41a26e077774ef6), // 5^-321
    U128.new(0xfd00b897478238d0, 0x8920b098955522b4), // 5^-320
    U128.new(0x9e20735e8cb16382, 0x55b46e5f5d5535b0), // 5^-319
    U128.new(0xc5a890362fddbc62, 0xeb2189f734aa831d), // 5^-318
    U128.new(0xf712b443bbd52b7b, 0xa5e9ec7501d523e4), // 5^-317
    U128.new(0x9a6bb0aa55653b2d, 0x47b233c92125366e), // 5^-316
    U128.new(0xc1069cd4eabe89f8, 0x999ec0bb696e840a), // 5^-315
    U128.new(0xf148440a256e2c76, 0xc00670ea43ca250d), // 5^-314
    U128.new(0x96cd2a865764dbca, 0x380406926a5e5728), // 5^-313
    U128.new(0xbc807527ed3e12bc, 0xc605083704f5ecf2), // 5^-312
    U128.new(0xeba09271e88d976b, 0xf7864a44c633682e), // 5^-311
    U128.new(0x93445b8731587ea3, 0x7ab3ee6afbe0211d), // 5^-310
    U128.new(0xb8157268fdae9e4c, 0x5960ea05bad82964), // 5^-309
    U128.new(0xe61acf033d1a45df, 0x6fb92487298e33bd), // 5^-308
    U128.new(0x8fd0c16206306bab, 0xa5d3b6d479f8e056), // 5^-307
    U128.new(0xb3c4f1ba87bc8696, 0x8f48a4899877186c), // 5^-306
    U128.new(0xe0b62e2929aba83c, 0x331acdabfe94de87), // 5^-305
    U128.new(0x8c71dcd9ba0b4925, 0x9ff0c08b7f1d0b14), // 5^-304
    U128.new(0xaf8e5410288e1b6f, 0x7ecf0ae5ee44dd9), // 5^-303
    U128.new(0xdb71e91432b1a24a, 0xc9e82cd9f69d6150), // 5^-302
    U128.new(0x892731ac9faf056e, 0xbe311c083a225cd2), // 5^-301
    U128.new(0xab70fe17c79ac6ca, 0x6dbd630a48aaf406), // 5^-300
    U128.new(0xd64d3d9db981787d, 0x92cbbccdad5b108), // 5^-299
    U128.new(0x85f0468293f0eb4e, 0x25bbf56008c58ea5), // 5^-298
    U128.new(0xa76c582338ed2621, 0xaf2af2b80af6f24e), // 5^-297
    U128.new(0xd1476e2c07286faa, 0x1af5af660db4aee1), // 5^-296
    U128.new(0x82cca4db847945ca, 0x50d98d9fc890ed4d), // 5^-295
    U128.new(0xa37fce126597973c, 0xe50ff107bab528a0), // 5^-294
    U128.new(0xcc5fc196fefd7d0c, 0x1e53ed49a96272c8), // 5^-293
    U128.new(0xff77b1fcbebcdc4f, 0x25e8e89c13bb0f7a), // 5^-292
    U128.new(0x9faacf3df73609b1, 0x77b191618c54e9ac), // 5^-291
    U128.new(0xc795830d75038c1d, 0xd59df5b9ef6a2417), // 5^-290
    U128.new(0xf97ae3d0d2446f25, 0x4b0573286b44ad1d), // 5^-289
    U128.new(0x9becce62836ac577, 0x4ee367f9430aec32), // 5^-288
    U128.new(0xc2e801fb244576d5, 0x229c41f793cda73f), // 5^-287
    U128.new(0xf3a20279ed56d48a, 0x6b43527578c1110f), // 5^-286
    U128.new(0x9845418c345644d6, 0x830a13896b78aaa9), // 5^-285
    U128.new(0xbe5691ef416bd60c, 0x23cc986bc656d553), // 5^-284
    U128.new(0xedec366b11c6cb8f, 0x2cbfbe86b7ec8aa8), // 5^-283
    U128.new(0x94b3a202eb1c3f39, 0x7bf7d71432f3d6a9), // 5^-282
    U128.new(0xb9e08a83a5e34f07, 0xdaf5ccd93fb0cc53), // 5^-281
    U128.new(0xe858ad248f5c22c9, 0xd1b3400f8f9cff68), // 5^-280
    U128.new(0x91376c36d99995be, 0x23100809b9c21fa1), // 5^-279
    U128.new(0xb58547448ffffb2d, 0xabd40a0c2832a78a), // 5^-278
    U128.new(0xe2e69915b3fff9f9, 0x16c90c8f323f516c), // 5^-277
    U128.new(0x8dd01fad907ffc3b, 0xae3da7d97f6792e3), // 5^-276
    U128.new(0xb1442798f49ffb4a, 0x99cd11cfdf41779c), // 5^-275
    U128.new(0xdd95317f31c7fa1d, 0x40405643d711d583), // 5^-274
    U128.new(0x8a7d3eef7f1cfc52, 0x482835ea666b2572), // 5^-273
    U128.new(0xad1c8eab5ee43b66, 0xda3243650005eecf), // 5^-272
    U128.new(0xd863b256369d4a40, 0x90bed43e40076a82), // 5^-271
    U128.new(0x873e4f75e2224e68, 0x5a7744a6e804a291), // 5^-270
    U128.new(0xa90de3535aaae202, 0x711515d0a205cb36), // 5^-269
    U128.new(0xd3515c2831559a83, 0xd5a5b44ca873e03), // 5^-268
    U128.new(0x8412d9991ed58091, 0xe858790afe9486c2), // 5^-267
    U128.new(0xa5178fff668ae0b6, 0x626e974dbe39a872), // 5^-266
    U128.new(0xce5d73ff402d98e3, 0xfb0a3d212dc8128f), // 5^-265
    U128.new(0x80fa687f881c7f8e, 0x7ce66634bc9d0b99), // 5^-264
    U128.new(0xa139029f6a239f72, 0x1c1fffc1ebc44e80), // 5^-263
    U128.new(0xc987434744ac874e, 0xa327ffb266b56220), // 5^-262
    U128.new(0xfbe9141915d7a922, 0x4bf1ff9f0062baa8), // 5^-261
    U128.new(0x9d71ac8fada6c9b5, 0x6f773fc3603db4a9), // 5^-260
    U128.new(0xc4ce17b399107c22, 0xcb550fb4384d21d3), // 5^-259
    U128.new(0xf6019da07f549b2b, 0x7e2a53a146606a48), // 5^-258
    U128.new(0x99c102844f94e0fb, 0x2eda7444cbfc426d), // 5^-257
    U128.new(0xc0314325637a1939, 0xfa911155fefb5308), // 5^-256
    U128.new(0xf03d93eebc589f88, 0x793555ab7eba27ca), // 5^-255
    U128.new(0x96267c7535b763b5, 0x4bc1558b2f3458de), // 5^-254
    U128.new(0xbbb01b9283253ca2, 0x9eb1aaedfb016f16), // 5^-253
    U128.new(0xea9c227723ee8bcb, 0x465e15a979c1cadc), // 5^-252
    U128.new(0x92a1958a7675175f, 0xbfacd89ec191ec9), // 5^-251
    U128.new(0xb749faed14125d36, 0xcef980ec671f667b), // 5^-250
    U128.new(0xe51c79a85916f484, 0x82b7e12780e7401a), // 5^-249
    U128.new(0x8f31cc0937ae58d2, 0xd1b2ecb8b0908810), // 5^-248
    U128.new(0xb2fe3f0b8599ef07, 0x861fa7e6dcb4aa15), // 5^-247
    U128.new(0xdfbdcece67006ac9, 0x67a791e093e1d49a), // 5^-246
    U128.new(0x8bd6a141006042bd, 0xe0c8bb2c5c6d24e0), // 5^-245
    U128.new(0xaecc49914078536d, 0x58fae9f773886e18), // 5^-244
    U128.new(0xda7f5bf590966848, 0xaf39a475506a899e), // 5^-243
    U128.new(0x888f99797a5e012d, 0x6d8406c952429603), // 5^-242
    U128.new(0xaab37fd7d8f58178, 0xc8e5087ba6d33b83), // 5^-241
    U128.new(0xd5605fcdcf32e1d6, 0xfb1e4a9a90880a64), // 5^-240
    U128.new(0x855c3be0a17fcd26, 0x5cf2eea09a55067f), // 5^-239
    U128.new(0xa6b34ad8c9dfc06f, 0xf42faa48c0ea481e), // 5^-238
    U128.new(0xd0601d8efc57b08b, 0xf13b94daf124da26), // 5^-237
    U128.new(0x823c12795db6ce57, 0x76c53d08d6b70858), // 5^-236
    U128.new(0xa2cb1717b52481ed, 0x54768c4b0c64ca6e), // 5^-235
    U128.new(0xcb7ddcdda26da268, 0xa9942f5dcf7dfd09), // 5^-234
    U128.new(0xfe5d54150b090b02, 0xd3f93b35435d7c4c), // 5^-233
    U128.new(0x9efa548d26e5a6e1, 0xc47bc5014a1a6daf), // 5^-232
    U128.new(0xc6b8e9b0709f109a, 0x359ab6419ca1091b), // 5^-231
    U128.new(0xf867241c8cc6d4c0, 0xc30163d203c94b62), // 5^-230
    U128.new(0x9b407691d7fc44f8, 0x79e0de63425dcf1d), // 5^-229
    U128.new(0xc21094364dfb5636, 0x985915fc12f542e4), // 5^-228
    U128.new(0xf294b943e17a2bc4, 0x3e6f5b7b17b2939d), // 5^-227
    U128.new(0x979cf3ca6cec5b5a, 0xa705992ceecf9c42), // 5^-226
    U128.new(0xbd8430bd08277231, 0x50c6ff782a838353), // 5^-225
    U128.new(0xece53cec4a314ebd, 0xa4f8bf5635246428), // 5^-224
    U128.new(0x940f4613ae5ed136, 0x871b7795e136be99), // 5^-223
    U128.new(0xb913179899f68584, 0x28e2557b59846e3f), // 5^-222
    U128.new(0xe757dd7ec07426e5, 0x331aeada2fe589cf), // 5^-221
    U128.new(0x9096ea6f3848984f, 0x3ff0d2c85def7621), // 5^-220
    U128.new(0xb4bca50b065abe63, 0xfed077a756b53a9), // 5^-219
    U128.new(0xe1ebce4dc7f16dfb, 0xd3e8495912c62894), // 5^-218
    U128.new(0x8d3360f09cf6e4bd, 0x64712dd7abbbd95c), // 5^-217
    U128.new(0xb080392cc4349dec, 0xbd8d794d96aacfb3), // 5^-216
    U128.new(0xdca04777f541c567, 0xecf0d7a0fc5583a0), // 5^-215
    U128.new(0x89e42caaf9491b60, 0xf41686c49db57244), // 5^-214
    U128.new(0xac5d37d5b79b6239, 0x311c2875c522ced5), // 5^-213
    U128.new(0xd77485cb25823ac7, 0x7d633293366b828b), // 5^-212
    U128.new(0x86a8d39ef77164bc, 0xae5dff9c02033197), // 5^-211
    U128.new(0xa8530886b54dbdeb, 0xd9f57f830283fdfc), // 5^-210
    U128.new(0xd267caa862a12d66, 0xd072df63c324fd7b), // 5^-209
    U128.new(0x8380dea93da4bc60, 0x4247cb9e59f71e6d), // 5^-208
    U128.new(0xa46116538d0deb78, 0x52d9be85f074e608), // 5^-207
    U128.new(0xcd795be870516656, 0x67902e276c921f8b), // 5^-206
    U128.new(0x806bd9714632dff6, 0xba1cd8a3db53b6), // 5^-205
    U128.new(0xa086cfcd97bf97f3, 0x80e8a40eccd228a4), // 5^-204
    U128.new(0xc8a883c0fdaf7df0, 0x6122cd128006b2cd), // 5^-203
    U128.new(0xfad2a4b13d1b5d6c, 0x796b805720085f81), // 5^-202
    U128.new(0x9cc3a6eec6311a63, 0xcbe3303674053bb0), // 5^-201
    U128.new(0xc3f490aa77bd60fc, 0xbedbfc4411068a9c), // 5^-200
    U128.new(0xf4f1b4d515acb93b, 0xee92fb5515482d44), // 5^-199
    U128.new(0x991711052d8bf3c5, 0x751bdd152d4d1c4a), // 5^-198
    U128.new(0xbf5cd54678eef0b6, 0xd262d45a78a0635d), // 5^-197
    U128.new(0xef340a98172aace4, 0x86fb897116c87c34), // 5^-196
    U128.new(0x9580869f0e7aac0e, 0xd45d35e6ae3d4da0), // 5^-195
    U128.new(0xbae0a846d2195712, 0x8974836059cca109), // 5^-194
    U128.new(0xe998d258869facd7, 0x2bd1a438703fc94b), // 5^-193
    U128.new(0x91ff83775423cc06, 0x7b6306a34627ddcf), // 5^-192
    U128.new(0xb67f6455292cbf08, 0x1a3bc84c17b1d542), // 5^-191
    U128.new(0xe41f3d6a7377eeca, 0x20caba5f1d9e4a93), // 5^-190
    U128.new(0x8e938662882af53e, 0x547eb47b7282ee9c), // 5^-189
    U128.new(0xb23867fb2a35b28d, 0xe99e619a4f23aa43), // 5^-188
    U128.new(0xdec681f9f4c31f31, 0x6405fa00e2ec94d4), // 5^-187
    U128.new(0x8b3c113c38f9f37e, 0xde83bc408dd3dd04), // 5^-186
    U128.new(0xae0b158b4738705e, 0x9624ab50b148d445), // 5^-185
    U128.new(0xd98ddaee19068c76, 0x3badd624dd9b0957), // 5^-184
    U128.new(0x87f8a8d4cfa417c9, 0xe54ca5d70a80e5d6), // 5^-183
    U128.new(0xa9f6d30a038d1dbc, 0x5e9fcf4ccd211f4c), // 5^-182
    U128.new(0xd47487cc8470652b, 0x7647c3200069671f), // 5^-181
    U128.new(0x84c8d4dfd2c63f3b, 0x29ecd9f40041e073), // 5^-180
    U128.new(0xa5fb0a17c777cf09, 0xf468107100525890), // 5^-179
    U128.new(0xcf79cc9db955c2cc, 0x7182148d4066eeb4), // 5^-178
    U128.new(0x81ac1fe293d599bf, 0xc6f14cd848405530), // 5^-177
    U128.new(0xa21727db38cb002f, 0xb8ada00e5a506a7c), // 5^-176
    U128.new(0xca9cf1d206fdc03b, 0xa6d90811f0e4851c), // 5^-175
    U128.new(0xfd442e4688bd304a, 0x908f4a166d1da663), // 5^-174
    U128.new(0x9e4a9cec15763e2e, 0x9a598e4e043287fe), // 5^-173
    U128.new(0xc5dd44271ad3cdba, 0x40eff1e1853f29fd), // 5^-172
    U128.new(0xf7549530e188c128, 0xd12bee59e68ef47c), // 5^-171
    U128.new(0x9a94dd3e8cf578b9, 0x82bb74f8301958ce), // 5^-170
    U128.new(0xc13a148e3032d6e7, 0xe36a52363c1faf01), // 5^-169
    U128.new(0xf18899b1bc3f8ca1, 0xdc44e6c3cb279ac1), // 5^-168
    U128.new(0x96f5600f15a7b7e5, 0x29ab103a5ef8c0b9), // 5^-167
    U128.new(0xbcb2b812db11a5de, 0x7415d448f6b6f0e7), // 5^-166
    U128.new(0xebdf661791d60f56, 0x111b495b3464ad21), // 5^-165
    U128.new(0x936b9fcebb25c995, 0xcab10dd900beec34), // 5^-164
    U128.new(0xb84687c269ef3bfb, 0x3d5d514f40eea742), // 5^-163
    U128.new(0xe65829b3046b0afa, 0xcb4a5a3112a5112), // 5^-162
    U128.new(0x8ff71a0fe2c2e6dc, 0x47f0e785eaba72ab), // 5^-161
    U128.new(0xb3f4e093db73a093, 0x59ed216765690f56), // 5^-160
    U128.new(0xe0f218b8d25088b8, 0x306869c13ec3532c), // 5^-159
    U128.new(0x8c974f7383725573, 0x1e414218c73a13fb), // 5^-158
    U128.new(0xafbd2350644eeacf, 0xe5d1929ef90898fa), // 5^-157
    U128.new(0xdbac6c247d62a583, 0xdf45f746b74abf39), // 5^-156
    U128.new(0x894bc396ce5da772, 0x6b8bba8c328eb783), // 5^-155
    U128.new(0xab9eb47c81f5114f, 0x66ea92f3f326564), // 5^-154
    U128.new(0xd686619ba27255a2, 0xc80a537b0efefebd), // 5^-153
    U128.new(0x8613fd0145877585, 0xbd06742ce95f5f36), // 5^-152
    U128.new(0xa798fc4196e952e7, 0x2c48113823b73704), // 5^-151
    U128.new(0xd17f3b51fca3a7a0, 0xf75a15862ca504c5), // 5^-150
    U128.new(0x82ef85133de648c4, 0x9a984d73dbe722fb), // 5^-149
    U128.new(0xa3ab66580d5fdaf5, 0xc13e60d0d2e0ebba), // 5^-148
    U128.new(0xcc963fee10b7d1b3, 0x318df905079926a8), // 5^-147
    U128.new(0xffbbcfe994e5c61f, 0xfdf17746497f7052), // 5^-146
    U128.new(0x9fd561f1fd0f9bd3, 0xfeb6ea8bedefa633), // 5^-145
    U128.new(0xc7caba6e7c5382c8, 0xfe64a52ee96b8fc0), // 5^-144
    U128.new(0xf9bd690a1b68637b, 0x3dfdce7aa3c673b0), // 5^-143
    U128.new(0x9c1661a651213e2d, 0x6bea10ca65c084e), // 5^-142
    U128.new(0xc31bfa0fe5698db8, 0x486e494fcff30a62), // 5^-141
    U128.new(0xf3e2f893dec3f126, 0x5a89dba3c3efccfa), // 5^-140
    U128.new(0x986ddb5c6b3a76b7, 0xf89629465a75e01c), // 5^-139
    U128.new(0xbe89523386091465, 0xf6bbb397f1135823), // 5^-138
    U128.new(0xee2ba6c0678b597f, 0x746aa07ded582e2c), // 5^-137
    U128.new(0x94db483840b717ef, 0xa8c2a44eb4571cdc), // 5^-136
    U128.new(0xba121a4650e4ddeb, 0x92f34d62616ce413), // 5^-135
    U128.new(0xe896a0d7e51e1566, 0x77b020baf9c81d17), // 5^-134
    U128.new(0x915e2486ef32cd60, 0xace1474dc1d122e), // 5^-133
    U128.new(0xb5b5ada8aaff80b8, 0xd819992132456ba), // 5^-132
    U128.new(0xe3231912d5bf60e6, 0x10e1fff697ed6c69), // 5^-131
    U128.new(0x8df5efabc5979c8f, 0xca8d3ffa1ef463c1), // 5^-130
    U128.new(0xb1736b96b6fd83b3, 0xbd308ff8a6b17cb2), // 5^-129
    U128.new(0xddd0467c64bce4a0, 0xac7cb3f6d05ddbde), // 5^-128
    U128.new(0x8aa22c0dbef60ee4, 0x6bcdf07a423aa96b), // 5^-127
    U128.new(0xad4ab7112eb3929d, 0x86c16c98d2c953c6), // 5^-126
    U128.new(0xd89d64d57a607744, 0xe871c7bf077ba8b7), // 5^-125
    U128.new(0x87625f056c7c4a8b, 0x11471cd764ad4972), // 5^-124
    U128.new(0xa93af6c6c79b5d2d, 0xd598e40d3dd89bcf), // 5^-123
    U128.new(0xd389b47879823479, 0x4aff1d108d4ec2c3), // 5^-122
    U128.new(0x843610cb4bf160cb, 0xcedf722a585139ba), // 5^-121
    U128.new(0xa54394fe1eedb8fe, 0xc2974eb4ee658828), // 5^-120
    U128.new(0xce947a3da6a9273e, 0x733d226229feea32), // 5^-119
    U128.new(0x811ccc668829b887, 0x806357d5a3f525f), // 5^-118
    U128.new(0xa163ff802a3426a8, 0xca07c2dcb0cf26f7), // 5^-117
    U128.new(0xc9bcff6034c13052, 0xfc89b393dd02f0b5), // 5^-116
    U128.new(0xfc2c3f3841f17c67, 0xbbac2078d443ace2), // 5^-115
    U128.new(0x9d9ba7832936edc0, 0xd54b944b84aa4c0d), // 5^-114
    U128.new(0xc5029163f384a931, 0xa9e795e65d4df11), // 5^-113
    U128.new(0xf64335bcf065d37d, 0x4d4617b5ff4a16d5), // 5^-112
    U128.new(0x99ea0196163fa42e, 0x504bced1bf8e4e45), // 5^-111
    U128.new(0xc06481fb9bcf8d39, 0xe45ec2862f71e1d6), // 5^-110
    U128.new(0xf07da27a82c37088, 0x5d767327bb4e5a4c), // 5^-109
    U128.new(0x964e858c91ba2655, 0x3a6a07f8d510f86f), // 5^-108
    U128.new(0xbbe226efb628afea, 0x890489f70a55368b), // 5^-107
    U128.new(0xeadab0aba3b2dbe5, 0x2b45ac74ccea842e), // 5^-106
    U128.new(0x92c8ae6b464fc96f, 0x3b0b8bc90012929d), // 5^-105
    U128.new(0xb77ada0617e3bbcb, 0x9ce6ebb40173744), // 5^-104
    U128.new(0xe55990879ddcaabd, 0xcc420a6a101d0515), // 5^-103
    U128.new(0x8f57fa54c2a9eab6, 0x9fa946824a12232d), // 5^-102
    U128.new(0xb32df8e9f3546564, 0x47939822dc96abf9), // 5^-101
    U128.new(0xdff9772470297ebd, 0x59787e2b93bc56f7), // 5^-100
    U128.new(0x8bfbea76c619ef36, 0x57eb4edb3c55b65a), // 5^-99
    U128.new(0xaefae51477a06b03, 0xede622920b6b23f1), // 5^-98
    U128.new(0xdab99e59958885c4, 0xe95fab368e45eced), // 5^-97
    U128.new(0x88b402f7fd75539b, 0x11dbcb0218ebb414), // 5^-96
    U128.new(0xaae103b5fcd2a881, 0xd652bdc29f26a119), // 5^-95
    U128.new(0xd59944a37c0752a2, 0x4be76d3346f0495f), // 5^-94
    U128.new(0x857fcae62d8493a5, 0x6f70a4400c562ddb), // 5^-93
    U128.new(0xa6dfbd9fb8e5b88e, 0xcb4ccd500f6bb952), // 5^-92
    U128.new(0xd097ad07a71f26b2, 0x7e2000a41346a7a7), // 5^-91
    U128.new(0x825ecc24c873782f, 0x8ed400668c0c28c8), // 5^-90
    U128.new(0xa2f67f2dfa90563b, 0x728900802f0f32fa), // 5^-89
    U128.new(0xcbb41ef979346bca, 0x4f2b40a03ad2ffb9), // 5^-88
    U128.new(0xfea126b7d78186bc, 0xe2f610c84987bfa8), // 5^-87
    U128.new(0x9f24b832e6b0f436, 0xdd9ca7d2df4d7c9), // 5^-86
    U128.new(0xc6ede63fa05d3143, 0x91503d1c79720dbb), // 5^-85
    U128.new(0xf8a95fcf88747d94, 0x75a44c6397ce912a), // 5^-84
    U128.new(0x9b69dbe1b548ce7c, 0xc986afbe3ee11aba), // 5^-83
    U128.new(0xc24452da229b021b, 0xfbe85badce996168), // 5^-82
    U128.new(0xf2d56790ab41c2a2, 0xfae27299423fb9c3), // 5^-81
    U128.new(0x97c560ba6b0919a5, 0xdccd879fc967d41a), // 5^-80
    U128.new(0xbdb6b8e905cb600f, 0x5400e987bbc1c920), // 5^-79
    U128.new(0xed246723473e3813, 0x290123e9aab23b68), // 5^-78
    U128.new(0x9436c0760c86e30b, 0xf9a0b6720aaf6521), // 5^-77
    U128.new(0xb94470938fa89bce, 0xf808e40e8d5b3e69), // 5^-76
    U128.new(0xe7958cb87392c2c2, 0xb60b1d1230b20e04), // 5^-75
    U128.new(0x90bd77f3483bb9b9, 0xb1c6f22b5e6f48c2), // 5^-74
    U128.new(0xb4ecd5f01a4aa828, 0x1e38aeb6360b1af3), // 5^-73
    U128.new(0xe2280b6c20dd5232, 0x25c6da63c38de1b0), // 5^-72
    U128.new(0x8d590723948a535f, 0x579c487e5a38ad0e), // 5^-71
    U128.new(0xb0af48ec79ace837, 0x2d835a9df0c6d851), // 5^-70
    U128.new(0xdcdb1b2798182244, 0xf8e431456cf88e65), // 5^-69
    U128.new(0x8a08f0f8bf0f156b, 0x1b8e9ecb641b58ff), // 5^-68
    U128.new(0xac8b2d36eed2dac5, 0xe272467e3d222f3f), // 5^-67
    U128.new(0xd7adf884aa879177, 0x5b0ed81dcc6abb0f), // 5^-66
    U128.new(0x86ccbb52ea94baea, 0x98e947129fc2b4e9), // 5^-65
    U128.new(0xa87fea27a539e9a5, 0x3f2398d747
```
