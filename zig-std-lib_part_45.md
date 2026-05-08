```
broutine` DIEs will have a range which is a sub-range of
    // their caller, and we want to return the callee's name, not the caller's.
    var i: usize = di.func_list.items.len;
    while (i > 0) {
        i -= 1;
        const func = &di.func_list.items[i];
        if (func.pc_range) |range| {
            if (address >= range.start and address < range.end) {
                return func.name;
            }
        }
    }

    return null;
}

pub const ScanError = error{
    InvalidDebugInfo,
    MissingDebugInfo,
    ReadFailed,
    EndOfStream,
    Overflow,
    StreamTooLong,
} || Allocator.Error;

fn scanAllFunctions(di: *Dwarf, gpa: Allocator, endian: Endian) ScanError!void {
    var fr: Reader = .fixed(di.section(.debug_info).?);
    var this_unit_offset: u64 = 0;

    while (this_unit_offset < fr.buffer.len) {
        fr.seek = @intCast(this_unit_offset);

        const unit_header = try readUnitHeader(&fr, endian);
        if (unit_header.unit_length == 0) return;
        const next_offset = unit_header.header_length + unit_header.unit_length;

        const version = try fr.takeInt(u16, endian);
        if (version < 2 or version > 5) return bad();

        var address_size: u8 = undefined;
        var debug_abbrev_offset: u64 = undefined;
        if (version >= 5) {
            const unit_type = try fr.takeByte();
            if (unit_type != DW.UT.compile) return bad();
            address_size = try fr.takeByte();
            debug_abbrev_offset = try readFormatSizedInt(&fr, unit_header.format, endian);
        } else {
            debug_abbrev_offset = try readFormatSizedInt(&fr, unit_header.format, endian);
            address_size = try fr.takeByte();
        }

        const abbrev_table = try di.getAbbrevTable(gpa, debug_abbrev_offset);

        var max_attrs: usize = 0;
        var zig_padding_abbrev_code: u7 = 0;
        for (abbrev_table.abbrevs) |abbrev| {
            max_attrs = @max(max_attrs, abbrev.attrs.len);
            if (cast(u7, abbrev.code)) |code| {
                if (abbrev.tag_id == DW.TAG.ZIG_padding and
                    !abbrev.has_children and
                    abbrev.attrs.len == 0)
                {
                    zig_padding_abbrev_code = code;
                }
            }
        }
        const attrs_buf = try gpa.alloc(Die.Attr, max_attrs * 3);
        defer gpa.free(attrs_buf);
        var attrs_bufs: [3][]Die.Attr = undefined;
        for (&attrs_bufs, 0..) |*buf, index| buf.* = attrs_buf[index * max_attrs ..][0..max_attrs];

        const next_unit_pos = this_unit_offset + next_offset;

        var compile_unit: CompileUnit = .{
            .version = version,
            .format = unit_header.format,
            .addr_size_bytes = address_size,
            .die = undefined,
            .pc_range = null,

            .str_offsets_base = 0,
            .addr_base = 0,
            .rnglists_base = 0,
            .loclists_base = 0,
            .frame_base = null,
            .src_loc_cache = null,
        };

        while (true) {
            fr.seek = std.mem.findNonePos(u8, fr.buffer, fr.seek, &.{
                zig_padding_abbrev_code, 0,
            }) orelse fr.buffer.len;
            if (fr.seek >= next_unit_pos) break;
            var die_obj = (try parseDie(
                &fr,
                attrs_bufs[0],
                abbrev_table,
                unit_header.format,
                endian,
                address_size,
            )) orelse continue;

            switch (die_obj.tag_id) {
                DW.TAG.compile_unit => {
                    compile_unit.die = die_obj;
                    compile_unit.die.attrs = attrs_bufs[1][0..die_obj.attrs.len];
                    @memcpy(compile_unit.die.attrs, die_obj.attrs);

                    compile_unit.str_offsets_base = if (die_obj.getAttr(AT.str_offsets_base)) |fv| try fv.getUInt(usize) else 0;
                    compile_unit.addr_base = if (die_obj.getAttr(AT.addr_base)) |fv| try fv.getUInt(usize) else 0;
                    compile_unit.rnglists_base = if (die_obj.getAttr(AT.rnglists_base)) |fv| try fv.getUInt(usize) else 0;
                    compile_unit.loclists_base = if (die_obj.getAttr(AT.loclists_base)) |fv| try fv.getUInt(usize) else 0;
                    compile_unit.frame_base = die_obj.getAttr(AT.frame_base);
                },
                DW.TAG.subprogram, DW.TAG.inlined_subroutine, DW.TAG.subroutine, DW.TAG.entry_point => {
                    const fn_name = x: {
                        var this_die_obj = die_obj;
                        // Prevent endless loops
                        for (0..3) |_| {
                            if (this_die_obj.getAttr(AT.name)) |_| {
                                break :x try this_die_obj.getAttrString(di, endian, AT.name, di.section(.debug_str), &compile_unit);
                            } else if (this_die_obj.getAttr(AT.abstract_origin)) |_| {
                                const after_die_offset = fr.seek;
                                defer fr.seek = after_die_offset;

                                // Follow the DIE it points to and repeat
                                const ref_offset = try this_die_obj.getAttrRef(AT.abstract_origin, this_unit_offset, next_offset);
                                fr.seek = @intCast(ref_offset);
                                this_die_obj = (try parseDie(
                                    &fr,
                                    attrs_bufs[2],
                                    abbrev_table, // wrong abbrev table for different cu
                                    unit_header.format,
                                    endian,
                                    address_size,
                                )) orelse return bad();
                            } else if (this_die_obj.getAttr(AT.specification)) |_| {
                                const after_die_offset = fr.seek;
                                defer fr.seek = after_die_offset;

                                // Follow the DIE it points to and repeat
                                const ref_offset = try this_die_obj.getAttrRef(AT.specification, this_unit_offset, next_offset);
                                fr.seek = @intCast(ref_offset);
                                this_die_obj = (try parseDie(
                                    &fr,
                                    attrs_bufs[2],
                                    abbrev_table, // wrong abbrev table for different cu
                                    unit_header.format,
                                    endian,
                                    address_size,
                                )) orelse return bad();
                            } else {
                                break :x null;
                            }
                        }

                        break :x null;
                    };

                    var range_added = if (die_obj.getAttrAddr(di, endian, AT.low_pc, &compile_unit)) |low_pc| blk: {
                        if (die_obj.getAttr(AT.high_pc)) |high_pc_value| {
                            const pc_end = switch (high_pc_value.*) {
                                .addr => |value| value,
                                .udata => |offset| low_pc + offset,
                                else => return bad(),
                            };

                            try di.func_list.append(gpa, .{
                                .name = fn_name,
                                .pc_range = .{
                                    .start = low_pc,
                                    .end = pc_end,
                                },
                            });

                            break :blk true;
                        }

                        break :blk false;
                    } else |err| blk: {
                        if (err != error.MissingDebugInfo) return err;
                        break :blk false;
                    };

                    if (die_obj.getAttr(AT.ranges)) |ranges_value| blk: {
                        var iter = DebugRangeIterator.init(ranges_value, di, endian, &compile_unit) catch |err| {
                            if (err != error.MissingDebugInfo) return err;
                            break :blk;
                        };

                        while (try iter.next()) |range| {
                            range_added = true;
                            try di.func_list.append(gpa, .{
                                .name = fn_name,
                                .pc_range = .{
                                    .start = range.start,
                                    .end = range.end,
                                },
                            });
                        }
                    }

                    if (fn_name != null and !range_added) {
                        try di.func_list.append(gpa, .{
                            .name = fn_name,
                            .pc_range = null,
                        });
                    }
                },
                else => {},
            }
        }

        this_unit_offset += next_offset;
    }
}

fn scanAllCompileUnits(di: *Dwarf, gpa: Allocator, endian: Endian) ScanError!void {
    var fr: Reader = .fixed(di.section(.debug_info).?);
    var this_unit_offset: u64 = 0;

    var attrs_buf = std.array_list.Managed(Die.Attr).init(gpa);
    defer attrs_buf.deinit();

    while (this_unit_offset < fr.buffer.len) {
        fr.seek = @intCast(this_unit_offset);

        const unit_header = try readUnitHeader(&fr, endian);
        if (unit_header.unit_length == 0) return;
        const next_offset = unit_header.header_length + unit_header.unit_length;

        const version = try fr.takeInt(u16, endian);
        if (version < 2 or version > 5) return bad();

        var address_size: u8 = undefined;
        var debug_abbrev_offset: u64 = undefined;
        if (version >= 5) {
            const unit_type = try fr.takeByte();
            if (unit_type != UT.compile) return bad();
            address_size = try fr.takeByte();
            debug_abbrev_offset = try readFormatSizedInt(&fr, unit_header.format, endian);
        } else {
            debug_abbrev_offset = try readFormatSizedInt(&fr, unit_header.format, endian);
            address_size = try fr.takeByte();
        }

        const abbrev_table = try di.getAbbrevTable(gpa, debug_abbrev_offset);

        var max_attrs: usize = 0;
        for (abbrev_table.abbrevs) |abbrev| {
            max_attrs = @max(max_attrs, abbrev.attrs.len);
        }
        try attrs_buf.resize(max_attrs);

        var compile_unit_die = (try parseDie(
            &fr,
            attrs_buf.items,
            abbrev_table,
            unit_header.format,
            endian,
            address_size,
        )) orelse return bad();

        if (compile_unit_die.tag_id != DW.TAG.compile_unit) return bad();

        compile_unit_die.attrs = try gpa.dupe(Die.Attr, compile_unit_die.attrs);

        var compile_unit: CompileUnit = .{
            .version = version,
            .format = unit_header.format,
            .addr_size_bytes = address_size,
            .pc_range = null,
            .die = compile_unit_die,
            .str_offsets_base = if (compile_unit_die.getAttr(AT.str_offsets_base)) |fv| try fv.getUInt(usize) else 0,
            .addr_base = if (compile_unit_die.getAttr(AT.addr_base)) |fv| try fv.getUInt(usize) else 0,
            .rnglists_base = if (compile_unit_die.getAttr(AT.rnglists_base)) |fv| try fv.getUInt(usize) else 0,
            .loclists_base = if (compile_unit_die.getAttr(AT.loclists_base)) |fv| try fv.getUInt(usize) else 0,
            .frame_base = compile_unit_die.getAttr(AT.frame_base),
            .src_loc_cache = null,
        };

        compile_unit.pc_range = x: {
            if (compile_unit_die.getAttrAddr(di, endian, AT.low_pc, &compile_unit)) |low_pc| {
                if (compile_unit_die.getAttr(AT.high_pc)) |high_pc_value| {
                    const pc_end = switch (high_pc_value.*) {
                        .addr => |value| value,
                        .udata => |offset| low_pc + offset,
                        else => return bad(),
                    };
                    break :x PcRange{
                        .start = low_pc,
                        .end = pc_end,
                    };
                } else {
                    break :x null;
                }
            } else |err| {
                if (err != error.MissingDebugInfo) return err;
                break :x null;
            }
        };

        try di.compile_unit_list.append(gpa, compile_unit);

        this_unit_offset += next_offset;
    }
}

pub fn populateRanges(d: *Dwarf, gpa: Allocator, endian: Endian) ScanError!void {
    assert(d.ranges.items.len == 0);

    for (d.compile_unit_list.items, 0..) |*cu, cu_index| {
        if (cu.pc_range) |range| {
            try d.ranges.append(gpa, .{
                .start = range.start,
                .end = range.end,
                .compile_unit_index = cu_index,
            });
            continue;
        }
        const ranges_value = cu.die.getAttr(AT.ranges) orelse continue;
        var iter = DebugRangeIterator.init(ranges_value, d, endian, cu) catch continue;
        while (try iter.next()) |range| {
            // Not sure why LLVM thinks it's OK to emit these...
            if (range.start == range.end) continue;

            try d.ranges.append(gpa, .{
                .start = range.start,
                .end = range.end,
                .compile_unit_index = cu_index,
            });
        }
    }

    std.mem.sortUnstable(Range, d.ranges.items, {}, struct {
        pub fn lessThan(ctx: void, a: Range, b: Range) bool {
            _ = ctx;
            return a.start < b.start;
        }
    }.lessThan);
}

const DebugRangeIterator = struct {
    base_address: u64,
    section_type: Section.Id,
    di: *const Dwarf,
    endian: Endian,
    compile_unit: *const CompileUnit,
    fr: Reader,

    pub fn init(ranges_value: *const FormValue, di: *const Dwarf, endian: Endian, compile_unit: *const CompileUnit) !@This() {
        const section_type = if (compile_unit.version >= 5) Section.Id.debug_rnglists else Section.Id.debug_ranges;
        const debug_ranges = di.section(section_type) orelse return error.MissingDebugInfo;

        const ranges_offset = switch (ranges_value.*) {
            .sec_offset, .udata => |off| off,
            .rnglistx => |idx| off: {
                switch (compile_unit.format) {
                    .@"32" => {
                        const offset_loc = compile_unit.rnglists_base + 4 * idx;
                        if (offset_loc + 4 > debug_ranges.len) return bad();
                        const offset = mem.readInt(u32, debug_ranges[@intCast(offset_loc)..][0..4], endian);
                        break :off compile_unit.rnglists_base + offset;
                    },
                    .@"64" => {
                        const offset_loc = compile_unit.rnglists_base + 8 * idx;
                        if (offset_loc + 8 > debug_ranges.len) return bad();
                        const offset = mem.readInt(u64, debug_ranges[@intCast(offset_loc)..][0..8], endian);
                        break :off compile_unit.rnglists_base + offset;
                    },
                }
            },
            else => return bad(),
        };

        // All the addresses in the list are relative to the value
        // specified by DW_AT.low_pc or to some other value encoded
        // in the list itself.
        // If no starting value is specified use zero.
        const base_address = compile_unit.die.getAttrAddr(di, endian, AT.low_pc, compile_unit) catch |err| switch (err) {
            error.MissingDebugInfo => 0,
            else => return err,
        };

        var fr: Reader = .fixed(debug_ranges);
        fr.seek = cast(usize, ranges_offset) orelse return bad();

        return .{
            .base_address = base_address,
            .section_type = section_type,
            .di = di,
            .endian = endian,
            .compile_unit = compile_unit,
            .fr = fr,
        };
    }

    // Returns the next range in the list, or null if the end was reached.
    pub fn next(self: *@This()) !?PcRange {
        const endian = self.endian;
        const addr_size_bytes = self.compile_unit.addr_size_bytes;
        switch (self.section_type) {
            .debug_rnglists => {
                const kind = try self.fr.takeByte();
                switch (kind) {
                    RLE.end_of_list => return null,
                    RLE.base_addressx => {
                        const index = try self.fr.takeLeb128(u64);
                        self.base_address = try self.di.readDebugAddr(endian, self.compile_unit, index);
                        return try self.next();
                    },
                    RLE.startx_endx => {
                        const start_index = try self.fr.takeLeb128(u64);
                        const start_addr = try self.di.readDebugAddr(endian, self.compile_unit, start_index);

                        const end_index = try self.fr.takeLeb128(u64);
                        const end_addr = try self.di.readDebugAddr(endian, self.compile_unit, end_index);

                        return .{
                            .start = start_addr,
                            .end = end_addr,
                        };
                    },
                    RLE.startx_length => {
                        const start_index = try self.fr.takeLeb128(u64);
                        const start_addr = try self.di.readDebugAddr(endian, self.compile_unit, start_index);

                        const len = try self.fr.takeLeb128(u64);
                        const end_addr = start_addr + len;

                        return .{
                            .start = start_addr,
                            .end = end_addr,
                        };
                    },
                    RLE.offset_pair => {
                        const start_addr = try self.fr.takeLeb128(u64);
                        const end_addr = try self.fr.takeLeb128(u64);

                        // This is the only kind that uses the base address
                        return .{
                            .start = self.base_address + start_addr,
                            .end = self.base_address + end_addr,
                        };
                    },
                    RLE.base_address => {
                        self.base_address = try readAddress(&self.fr, endian, addr_size_bytes);
                        return try self.next();
                    },
                    RLE.start_end => {
                        const start_addr = try readAddress(&self.fr, endian, addr_size_bytes);
                        const end_addr = try readAddress(&self.fr, endian, addr_size_bytes);

                        return .{
                            .start = start_addr,
                            .end = end_addr,
                        };
                    },
                    RLE.start_length => {
                        const start_addr = try readAddress(&self.fr, endian, addr_size_bytes);
                        const len = try self.fr.takeLeb128(u64);
                        const end_addr = start_addr + len;

                        return .{
                            .start = start_addr,
                            .end = end_addr,
                        };
                    },
                    else => return bad(),
                }
            },
            .debug_ranges => {
                const start_addr = try readAddress(&self.fr, endian, addr_size_bytes);
                const end_addr = try readAddress(&self.fr, endian, addr_size_bytes);
                if (start_addr == 0 and end_addr == 0) return null;

                // The entry with start_addr = max_representable_address selects a new value for the base address
                const max_representable_address = ~@as(u64, 0) >> @intCast(64 - addr_size_bytes);
                if (start_addr == max_representable_address) {
                    self.base_address = end_addr;
                    return try self.next();
                }

                return .{
                    .start = self.base_address + start_addr,
                    .end = self.base_address + end_addr,
                };
            },
            else => unreachable,
        }
    }
};

/// TODO: change this to binary searching the sorted compile unit list
pub fn findCompileUnit(di: *const Dwarf, endian: Endian, target_address: u64) !*CompileUnit {
    for (di.compile_unit_list.items) |*compile_unit| {
        if (compile_unit.pc_range) |range| {
            if (target_address >= range.start and target_address < range.end) return compile_unit;
        }

        const ranges_value = compile_unit.die.getAttr(AT.ranges) orelse continue;
        var iter = DebugRangeIterator.init(ranges_value, di, endian, compile_unit) catch continue;
        while (try iter.next()) |range| {
            if (target_address >= range.start and target_address < range.end) return compile_unit;
        }
    }

    return missing();
}

/// Gets an already existing AbbrevTable given the abbrev_offset, or if not found,
/// seeks in the stream and parses it.
fn getAbbrevTable(di: *Dwarf, gpa: Allocator, abbrev_offset: u64) !*const Abbrev.Table {
    for (di.abbrev_table_list.items) |*table| {
        if (table.offset == abbrev_offset) {
            return table;
        }
    }
    try di.abbrev_table_list.append(
        gpa,
        try di.parseAbbrevTable(gpa, abbrev_offset),
    );
    return &di.abbrev_table_list.items[di.abbrev_table_list.items.len - 1];
}

fn parseAbbrevTable(di: *Dwarf, gpa: Allocator, offset: u64) !Abbrev.Table {
    var fr: Reader = .fixed(di.section(.debug_abbrev).?);
    fr.seek = cast(usize, offset) orelse return bad();

    var abbrevs = std.array_list.Managed(Abbrev).init(gpa);
    defer {
        for (abbrevs.items) |*abbrev| {
            abbrev.deinit(gpa);
        }
        abbrevs.deinit();
    }

    var attrs = std.array_list.Managed(Abbrev.Attr).init(gpa);
    defer attrs.deinit();

    while (true) {
        const code = try fr.takeLeb128(u64);
        if (code == 0) break;
        const tag_id = try fr.takeLeb128(u64);
        const has_children = (try fr.takeByte()) == DW.CHILDREN.yes;

        while (true) {
            const attr_id = try fr.takeLeb128(u64);
            const form_id = try fr.takeLeb128(u64);
            if (attr_id == 0 and form_id == 0) break;
            try attrs.append(.{
                .id = attr_id,
                .form_id = form_id,
                .payload = switch (form_id) {
                    FORM.implicit_const => try fr.takeLeb128(i64),
                    else => undefined,
                },
            });
        }

        try abbrevs.append(.{
            .code = code,
            .tag_id = tag_id,
            .has_children = has_children,
            .attrs = try attrs.toOwnedSlice(),
        });
    }

    return .{
        .offset = offset,
        .abbrevs = try abbrevs.toOwnedSlice(),
    };
}

fn parseDie(
    fr: *Reader,
    attrs_buf: []Die.Attr,
    abbrev_table: *const Abbrev.Table,
    format: Format,
    endian: Endian,
    addr_size_bytes: u8,
) ScanError!?Die {
    const abbrev_code = try fr.takeLeb128(u64);
    if (abbrev_code == 0) return null;
    const table_entry = abbrev_table.get(abbrev_code) orelse return bad();

    const attrs = attrs_buf[0..table_entry.attrs.len];
    for (attrs, table_entry.attrs) |*result_attr, attr| result_attr.* = .{
        .id = attr.id,
        .value = try parseFormValue(fr, attr.form_id, format, endian, addr_size_bytes, attr.payload),
    };
    return .{
        .tag_id = table_entry.tag_id,
        .has_children = table_entry.has_children,
        .attrs = attrs,
    };
}

/// Ensures that addresses in the returned LineTable are monotonically increasing.
fn runLineNumberProgram(d: *Dwarf, gpa: Allocator, endian: Endian, compile_unit: *const CompileUnit) !CompileUnit.SrcLocCache {
    const compile_unit_cwd = try compile_unit.die.getAttrString(d, endian, AT.comp_dir, d.section(.debug_line_str), compile_unit);
    const line_info_offset = try compile_unit.die.getAttrSecOffset(AT.stmt_list);

    var fr: Reader = .fixed(d.section(.debug_line).?);
    fr.seek = @intCast(line_info_offset);

    const unit_header = try readUnitHeader(&fr, endian);
    if (unit_header.unit_length == 0) return missing();

    const next_offset = unit_header.header_length + unit_header.unit_length;

    const version = try fr.takeInt(u16, endian);
    if (version < 2) return bad();

    const addr_size_bytes: u8, const seg_size: u8 = if (version >= 5) .{
        try fr.takeByte(),
        try fr.takeByte(),
    } else .{
        compile_unit.addr_size_bytes,
        0,
    };
    if (seg_size != 0) return bad(); // unsupported

    const prologue_length = try readFormatSizedInt(&fr, unit_header.format, endian);
    const prog_start_offset = fr.seek + prologue_length;

    const minimum_instruction_length = try fr.takeByte();
    if (minimum_instruction_length == 0) return bad();

    if (version >= 4) {
        const maximum_operations_per_instruction = try fr.takeByte();
        _ = maximum_operations_per_instruction;
    }

    const default_is_stmt = (try fr.takeByte()) != 0;
    const line_base = try fr.takeByteSigned();

    const line_range = try fr.takeByte();
    if (line_range == 0) return bad();

    const opcode_base = try fr.takeByte();

    const standard_opcode_lengths = try fr.take(opcode_base - 1);

    var directories: ArrayList(FileEntry) = .empty;
    defer directories.deinit(gpa);
    var file_entries: ArrayList(FileEntry) = .empty;
    defer file_entries.deinit(gpa);

    if (version < 5) {
        try directories.append(gpa, .{ .path = compile_unit_cwd });

        while (true) {
            const dir = try fr.takeSentinel(0);
            if (dir.len == 0) break;
            try directories.append(gpa, .{ .path = dir });
        }

        while (true) {
            const file_name = try fr.takeSentinel(0);
            if (file_name.len == 0) break;
            const dir_index = try fr.takeLeb128(u32);
            const mtime = try fr.takeLeb128(u64);
            const size = try fr.takeLeb128(u64);
            try file_entries.append(gpa, .{
                .path = file_name,
                .dir_index = dir_index,
                .mtime = mtime,
                .size = size,
            });
        }
    } else {
        const FileEntFmt = struct {
            content_type_code: u16,
            form_code: u16,
        };
        {
            var dir_ent_fmt_buf: [10]FileEntFmt = undefined;
            const directory_entry_format_count = try fr.takeByte();
            if (directory_entry_format_count > dir_ent_fmt_buf.len) return bad();
            for (dir_ent_fmt_buf[0..directory_entry_format_count]) |*ent_fmt| {
                ent_fmt.* = .{
                    .content_type_code = try fr.takeLeb128(u8),
                    .form_code = try fr.takeLeb128(u16),
                };
            }

            const directories_count = try fr.takeLeb128(usize);

            for (try directories.addManyAsSlice(gpa, directories_count)) |*e| {
                e.* = .{ .path = &.{} };
                for (dir_ent_fmt_buf[0..directory_entry_format_count]) |ent_fmt| {
                    const form_value = try parseFormValue(&fr, ent_fmt.form_code, unit_header.format, endian, addr_size_bytes, null);
                    switch (ent_fmt.content_type_code) {
                        DW.LNCT.path => e.path = try form_value.getString(d.*),
                        DW.LNCT.directory_index => e.dir_index = try form_value.getUInt(u32),
                        DW.LNCT.timestamp => e.mtime = try form_value.getUInt(u64),
                        DW.LNCT.size => e.size = try form_value.getUInt(u64),
                        DW.LNCT.MD5 => e.md5 = switch (form_value) {
                            .data16 => |data16| data16.*,
                            else => return bad(),
                        },
                        else => continue,
                    }
                }
            }
        }

        var file_ent_fmt_buf: [10]FileEntFmt = undefined;
        const file_name_entry_format_count = try fr.takeByte();
        if (file_name_entry_format_count > file_ent_fmt_buf.len) return bad();
        for (file_ent_fmt_buf[0..file_name_entry_format_count]) |*ent_fmt| {
            ent_fmt.* = .{
                .content_type_code = try fr.takeLeb128(u16),
                .form_code = try fr.takeLeb128(u16),
            };
        }

        const file_names_count = try fr.takeLeb128(usize);
        try file_entries.ensureUnusedCapacity(gpa, file_names_count);

        for (try file_entries.addManyAsSlice(gpa, file_names_count)) |*e| {
            e.* = .{ .path = &.{} };
            for (file_ent_fmt_buf[0..file_name_entry_format_count]) |ent_fmt| {
                const form_value = try parseFormValue(&fr, ent_fmt.form_code, unit_header.format, endian, addr_size_bytes, null);
                switch (ent_fmt.content_type_code) {
                    DW.LNCT.path => e.path = try form_value.getString(d.*),
                    DW.LNCT.directory_index => e.dir_index = try form_value.getUInt(u32),
                    DW.LNCT.timestamp => e.mtime = try form_value.getUInt(u64),
                    DW.LNCT.size => e.size = try form_value.getUInt(u64),
                    DW.LNCT.MD5 => e.md5 = switch (form_value) {
                        .data16 => |data16| data16.*,
                        else => return bad(),
                    },
                    else => continue,
                }
            }
        }
    }

    var prog = LineNumberProgram.init(default_is_stmt, version);
    var line_table: CompileUnit.SrcLocCache.LineTable = .{};
    errdefer line_table.deinit(gpa);

    fr.seek = @intCast(prog_start_offset);

    const next_unit_pos = line_info_offset + next_offset;

    while (fr.seek < next_unit_pos) {
        const opcode = try fr.takeByte();

        if (opcode == DW.LNS.extended_op) {
            const op_size = try fr.takeLeb128(u64);
            if (op_size < 1) return bad();
            const sub_op = try fr.takeByte();
            switch (sub_op) {
                DW.LNE.end_sequence => {
                    // The row being added here is an "end" address, meaning
                    // that it does not map to the source location here -
                    // rather it marks the previous address as the last address
                    // that maps to this source location.

                    // In this implementation we don't mark end of addresses.
                    // This is a performance optimization based on the fact
                    // that we don't need to know if an address is missing
                    // source location info; we are only interested in being
                    // able to look up source location info for addresses that
                    // are known to have debug info.
                    //if (debug_debug_mode) assert(!line_table.contains(prog.address));
                    //try line_table.put(gpa, prog.address, CompileUnit.SrcLocCache.LineEntry.invalid);
                    prog.reset();
                },
                DW.LNE.set_address => {
                    prog.address = try readAddress(&fr, endian, addr_size_bytes);
                },
                DW.LNE.define_file => {
                    const path = try fr.takeSentinel(0);
                    const dir_index = try fr.takeLeb128(u32);
                    const mtime = try fr.takeLeb128(u64);
                    const size = try fr.takeLeb128(u64);
                    try file_entries.append(gpa, .{
                        .path = path,
                        .dir_index = dir_index,
                        .mtime = mtime,
                        .size = size,
                    });
                },
                else => try fr.discardAll64(op_size - 1),
            }
        } else if (opcode >= opcode_base) {
            // special opcodes
            const adjusted_opcode = opcode - opcode_base;
            const inc_addr = minimum_instruction_length * (adjusted_opcode / line_range);
            const inc_line = @as(i32, line_base) + @as(i32, adjusted_opcode % line_range);
            prog.line += inc_line;
            prog.address += inc_addr;
            try prog.addRow(gpa, &line_table);
            prog.basic_block = false;
        } else {
            switch (opcode) {
                DW.LNS.copy => {
                    try prog.addRow(gpa, &line_table);
                    prog.basic_block = false;
                },
                DW.LNS.advance_pc => {
                    const arg = try fr.takeLeb128(u64);
                    prog.address += arg * minimum_instruction_length;
                },
                DW.LNS.advance_line => {
                    const arg = try fr.takeLeb128(i64);
                    prog.line += arg;
                },
                DW.LNS.set_file => {
                    const arg = try fr.takeLeb128(usize);
                    prog.file = arg;
                },
                DW.LNS.set_column => {
                    const arg = try fr.takeLeb128(u64);
                    prog.column = arg;
                },
                DW.LNS.negate_stmt => {
                    prog.is_stmt = !prog.is_stmt;
                },
                DW.LNS.set_basic_block => {
                    prog.basic_block = true;
                },
                DW.LNS.const_add_pc => {
                    const inc_addr = minimum_instruction_length * ((255 - opcode_base) / line_range);
                    prog.address += inc_addr;
                },
                DW.LNS.fixed_advance_pc => {
                    const arg = try fr.takeInt(u16, endian);
                    prog.address += arg;
                },
                DW.LNS.set_prologue_end => {},
                else => {
                    if (opcode - 1 >= standard_opcode_lengths.len) return bad();
                    try fr.discardAll(standard_opcode_lengths[opcode - 1]);
                },
            }
        }
    }

    // Dwarf standard v5, 6.2.5 says
    // > Within a sequence, addresses and operation pointers may only increase.
    // However, this is empirically not the case in reality, so we sort here.
    line_table.sortUnstable(struct {
        keys: []const u64,

        pub fn lessThan(ctx: @This(), a_index: usize, b_index: usize) bool {
            return ctx.keys[a_index] < ctx.keys[b_index];
        }
    }{ .keys = line_table.keys() });

    return .{
        .line_table = line_table,
        .directories = try directories.toOwnedSlice(gpa),
        .files = try file_entries.toOwnedSlice(gpa),
        .version = version,
    };
}

pub fn populateSrcLocCache(d: *Dwarf, gpa: Allocator, endian: Endian, cu: *CompileUnit) ScanError!void {
    if (cu.src_loc_cache != null) return;
    cu.src_loc_cache = try d.runLineNumberProgram(gpa, endian, cu);
}

pub fn getLineNumberInfo(
    d: *Dwarf,
    gpa: Allocator,
    text_arena: Allocator,
    endian: Endian,
    compile_unit: *CompileUnit,
    target_address: u64,
) !std.debug.SourceLocation {
    try d.populateSrcLocCache(gpa, endian, compile_unit);
    const slc = &compile_unit.src_loc_cache.?;
    const entry = try slc.findSource(target_address);
    const file_index = entry.file - @intFromBool(slc.version < 5);
    if (file_index >= slc.files.len) return bad();
    const file_entry = &slc.files[file_index];
    if (file_entry.dir_index >= slc.directories.len) return bad();
    const dir_name = slc.directories[file_entry.dir_index].path;
    const file_name = try std.fs.path.join(text_arena, &.{ dir_name, file_entry.path });
    return .{
        .line = entry.line,
        .column = entry.column,
        .file_name = file_name,
    };
}

fn getString(di: Dwarf, offset: u64) ![:0]const u8 {
    return getStringGeneric(di.section(.debug_str), offset);
}

fn getLineString(di: Dwarf, offset: u64) ![:0]const u8 {
    return getStringGeneric(di.section(.debug_line_str), offset);
}

fn readDebugAddr(di: Dwarf, endian: Endian, compile_unit: *const CompileUnit, index: u64) !u64 {
    const debug_addr = di.section(.debug_addr) orelse return bad();

    // addr_base points to the first item after the header, however we
    // need to read the header to know the size of each item. Empirically,
    // it may disagree with is_64 on the compile unit.
    // The header is 8 or 12 bytes depending on is_64.
    if (compile_unit.addr_base < 8) return bad();

    const version = mem.readInt(u16, debug_addr[compile_unit.addr_base - 4 ..][0..2], endian);
    if (version != 5) return bad();

    const addr_size = debug_addr[compile_unit.addr_base - 2];
    const seg_size = debug_addr[compile_unit.addr_base - 1];

    const byte_offset = compile_unit.addr_base + (addr_size + seg_size) * index;
    if (byte_offset + addr_size > debug_addr.len) return bad();
    return switch (addr_size) {
        1 => debug_addr[@intCast(byte_offset)],
        2 => mem.readInt(u16, debug_addr[@intCast(byte_offset)..][0..2], endian),
        4 => mem.readInt(u32, debug_addr[@intCast(byte_offset)..][0..4], endian),
        8 => mem.readInt(u64, debug_addr[@intCast(byte_offset)..][0..8], endian),
        else => bad(),
    };
}

fn parseFormValue(
    r: *Reader,
    form_id: u64,
    format: Format,
    endian: Endian,
    addr_size_bytes: u8,
    implicit_const: ?i64,
) ScanError!FormValue {
    return switch (form_id) {
        // DWARF5.pdf page 213: the size of this value is encoded in the
        // compilation unit header as address size.
        FORM.addr => .{ .addr = try readAddress(r, endian, addr_size_bytes) },
        FORM.addrx1 => .{ .addrx = try r.takeByte() },
        FORM.addrx2 => .{ .addrx = try r.takeInt(u16, endian) },
        FORM.addrx3 => .{ .addrx = try r.takeInt(u24, endian) },
        FORM.addrx4 => .{ .addrx = try r.takeInt(u32, endian) },
        FORM.addrx => .{ .addrx = try r.takeLeb128(u64) },

        FORM.block1 => .{ .block = try r.take(try r.takeByte()) },
        FORM.block2 => .{ .block = try r.take(try r.takeInt(u16, endian)) },
        FORM.block4 => .{ .block = try r.take(try r.takeInt(u32, endian)) },
        FORM.block => .{ .block = try r.take(try r.takeLeb128(usize)) },

        FORM.data1 => .{ .udata = try r.takeByte() },
        FORM.data2 => .{ .udata = try r.takeInt(u16, endian) },
        FORM.data4 => .{ .udata = try r.takeInt(u32, endian) },
        FORM.data8 => .{ .udata = try r.takeInt(u64, endian) },
        FORM.data16 => .{ .data16 = try r.takeArray(16) },
        FORM.udata => .{ .udata = try r.takeLeb128(u64) },
        FORM.sdata => .{ .sdata = try r.takeLeb128(i64) },
        FORM.exprloc => .{ .exprloc = try r.take(try r.takeLeb128(usize)) },
        FORM.flag => .{ .flag = (try r.takeByte()) != 0 },
        FORM.flag_present => .{ .flag = true },
        FORM.sec_offset => .{ .sec_offset = try readFormatSizedInt(r, format, endian) },

        FORM.ref1 => .{ .ref = try r.takeByte() },
        FORM.ref2 => .{ .ref = try r.takeInt(u16, endian) },
        FORM.ref4 => .{ .ref = try r.takeInt(u32, endian) },
        FORM.ref8 => .{ .ref = try r.takeInt(u64, endian) },
        FORM.ref_udata => .{ .ref = try r.takeLeb128(u64) },

        FORM.ref_addr => .{ .ref_addr = try readFormatSizedInt(r, format, endian) },
        FORM.ref_sig8 => .{ .ref = try r.takeInt(u64, endian) },

        FORM.string => .{ .string = try r.takeSentinel(0) },
        FORM.strp => .{ .strp = try readFormatSizedInt(r, format, endian) },
        FORM.strx1 => .{ .strx = try r.takeByte() },
        FORM.strx2 => .{ .strx = try r.takeInt(u16, endian) },
        FORM.strx3 => .{ .strx = try r.takeInt(u24, endian) },
        FORM.strx4 => .{ .strx = try r.takeInt(u32, endian) },
        FORM.strx => .{ .strx = try r.takeLeb128(usize) },
        FORM.line_strp => .{ .line_strp = try readFormatSizedInt(r, format, endian) },
        FORM.indirect => parseFormValue(r, try r.takeLeb128(u64), format, endian, addr_size_bytes, implicit_const),
        FORM.implicit_const => .{ .sdata = implicit_const orelse return bad() },
        FORM.loclistx => .{ .loclistx = try r.takeLeb128(u64) },
        FORM.rnglistx => .{ .rnglistx = try r.takeLeb128(u64) },
        else => {
            //debug.print("unrecognized form id: {x}\n", .{form_id});
            return bad();
        },
    };
}

const FileEntry = struct {
    path: []const u8,
    dir_index: u32 = 0,
    mtime: u64 = 0,
    size: u64 = 0,
    md5: [16]u8 = [1]u8{0} ** 16,
};

const LineNumberProgram = struct {
    address: u64,
    file: usize,
    line: i64,
    column: u64,
    version: u16,
    is_stmt: bool,
    basic_block: bool,

    default_is_stmt: bool,

    // Reset the state machine following the DWARF specification
    pub fn reset(self: *LineNumberProgram) void {
        self.address = 0;
        self.file = 1;
        self.line = 1;
        self.column = 0;
        self.is_stmt = self.default_is_stmt;
        self.basic_block = false;
    }

    pub fn init(is_stmt: bool, version: u16) LineNumberProgram {
        return .{
            .address = 0,
            .file = 1,
            .line = 1,
            .column = 0,
            .version = version,
            .is_stmt = is_stmt,
            .basic_block = false,
            .default_is_stmt = is_stmt,
        };
    }

    pub fn addRow(prog: *LineNumberProgram, gpa: Allocator, table: *CompileUnit.SrcLocCache.LineTable) !void {
        if (prog.line == 0) {
            //if (debug_debug_mode) @panic("garbage line data");
            return;
        }
        if (debug_debug_mode) assert(!table.contains(prog.address));
        try table.put(gpa, prog.address, .{
            .line = cast(u32, prog.line) orelse maxInt(u32),
            .column = cast(u32, prog.column) orelse maxInt(u32),
            .file = cast(u32, prog.file) orelse return bad(),
        });
    }
};

const UnitHeader = struct {
    format: Format,
    header_length: u4,
    unit_length: u64,
};

pub fn readUnitHeader(r: *Reader, endian: Endian) ScanError!UnitHeader {
    return switch (try r.takeInt(u32, endian)) {
        0...0xfffffff0 - 1 => |unit_length| .{
            .format = .@"32",
            .header_length = 4,
            .unit_length = unit_length,
        },
        0xfffffff0...0xffffffff - 1 => bad(),
        0xffffffff => .{
            .format = .@"64",
            .header_length = 12,
            .unit_length = try r.takeInt(u64, endian),
        },
    };
}

/// Returns the DWARF register number for an x86_64 register number found in compact unwind info
pub fn compactUnwindToDwarfRegNumber(unwind_reg_number: u3) !u16 {
    return switch (unwind_reg_number) {
        1 => 3, // RBX
        2 => 12, // R12
        3 => 13, // R13
        4 => 14, // R14
        5 => 15, // R15
        6 => 6, // RBP
        else => error.InvalidRegister,
    };
}

/// Returns `null` for CPU architectures without an instruction pointer register.
pub fn ipRegNum(arch: std.Target.Cpu.Arch) ?u16 {
    return switch (arch) {
        .aarch64, .aarch64_be => 32,
        .arc, .arceb => 160,
        .arm, .armeb, .thumb, .thumbeb => 15,
        .csky => 64,
        .hexagon => 76,
        .kvx => 64,
        .lanai => 2,
        .loongarch32, .loongarch64 => 64,
        .m68k => 26,
        .mips, .mipsel, .mips64, .mips64el => 66,
        .or1k => 35,
        .powerpc, .powerpcle, .powerpc64, .powerpc64le => 67,
        .riscv32, .riscv32be, .riscv64, .riscv64be => 65,
        .s390x => 65,
        .sparc, .sparc64 => 32,
        .ve => 144,
        .x86 => 8,
        .x86_64 => 16,
        else => null,
    };
}

pub fn fpRegNum(arch: std.Target.Cpu.Arch) u16 {
    return switch (arch) {
        .aarch64, .aarch64_be => 29,
        .arc, .arceb => 27,
        .arm, .armeb, .thumb, .thumbeb => 11,
        .csky => 14,
        .hexagon => 30,
        .kvx => 14,
        .lanai => 5,
        .loongarch32, .loongarch64 => 22,
        .m68k => 14,
        .mips, .mipsel, .mips64, .mips64el => 30,
        .or1k => 2,
        .powerpc, .powerpcle, .powerpc64, .powerpc64le => 1,
        .riscv32, .riscv32be, .riscv64, .riscv64be => 8,
        .s390x => 11,
        .sparc, .sparc64 => 30,
        .ve => 9,
        .x86 => 5,
        .x86_64 => 6,
        else => unreachable,
    };
}

pub fn spRegNum(arch: std.Target.Cpu.Arch) u16 {
    return switch (arch) {
        .aarch64, .aarch64_be => 31,
        .arc, .arceb => 28,
        .arm, .armeb, .thumb, .thumbeb => 13,
        .csky => 14,
        .hexagon => 29,
        .kvx => 12,
        .lanai => 4,
        .loongarch32, .loongarch64 => 3,
        .m68k => 15,
        .mips, .mipsel, .mips64, .mips64el => 29,
        .or1k => 1,
        .powerpc, .powerpcle, .powerpc64, .powerpc64le => 1,
        .riscv32, .riscv32be, .riscv64, .riscv64be => 2,
        .s390x => 15,
        .sparc, .sparc64 => 14,
        .ve => 11,
        .x86 => 4,
        .x86_64 => 7,
        else => unreachable,
    };
}

/// Tells whether unwinding for this target is supported by the Dwarf standard.
///
/// See also `std.debug.SelfInfo.can_unwind` which tells whether the Zig standard
/// library has a working implementation of unwinding for the current target.
pub fn supportsUnwinding(target: *const std.Target) bool {
    return switch (target.cpu.arch) {
        .amdgcn,
        .nvptx,
        .nvptx64,
        .spirv32,
        .spirv64,
        => false,

        // Conservative guess. Feel free to update this logic with any targets
        // that are known to not support Dwarf unwinding.
        else => true,
    };
}

/// This function is to make it handy to comment out the return and make it
/// into a crash when working on this file.
pub fn bad() error{InvalidDebugInfo} {
    invalidDebugInfoDetected();
    return error.InvalidDebugInfo;
}

pub fn invalidDebugInfoDetected() void {
    if (debug_debug_mode) @panic("bad dwarf");
}

pub fn missing() error{MissingDebugInfo} {
    if (debug_debug_mode) @panic("missing dwarf");
    return error.MissingDebugInfo;
}

fn getStringGeneric(opt_str: ?[]const u8, offset: u64) ![:0]const u8 {
    const str = opt_str orelse return bad();
    if (offset > str.len) return bad();
    const casted_offset = cast(usize, offset) orelse return bad();
    // Valid strings always have a terminating zero byte
    const last = std.mem.findScalarPos(u8, str, casted_offset, 0) orelse return bad();
    return str[casted_offset..last :0];
}

pub fn getSymbols(
    di: *Dwarf,
    symbol_allocator: Allocator,
    text_arena: Allocator,
    endian: Endian,
    address: u64,
    resolve_inline_callers: bool,
    symbols: *std.ArrayList(std.debug.Symbol),
) std.debug.SelfInfoError!void {
    _ = resolve_inline_callers;
    const gpa = std.debug.getDebugInfoAllocator();

    const compile_unit = di.findCompileUnit(endian, address) catch |err| switch (err) {
        error.EndOfStream => return error.MissingDebugInfo,
        error.Overflow => return error.InvalidDebugInfo,
        error.ReadFailed, error.InvalidDebugInfo, error.MissingDebugInfo => |e| return e,
    };
    try symbols.append(symbol_allocator, .{
        .name = di.getSymbolName(address),
        .compile_unit_name = compile_unit.die.getAttrString(di, endian, std.dwarf.AT.name, di.section(.debug_str), compile_unit) catch |err| switch (err) {
            error.MissingDebugInfo, error.InvalidDebugInfo => null,
        },
        .source_location = di.getLineNumberInfo(gpa, text_arena, endian, compile_unit, address) catch |err| switch (err) {
            error.MissingDebugInfo, error.InvalidDebugInfo => null,
            error.ReadFailed,
            error.EndOfStream,
            error.Overflow,
            error.StreamTooLong,
            => return error.InvalidDebugInfo,
            else => |e| return e,
        },
    });
}

/// DWARF5 7.4: "In the 32-bit DWARF format, all values that represent lengths of DWARF sections and
/// offsets relative to the beginning of DWARF sections are represented using four bytes. In the
/// 64-bit DWARF format, all values that represent lengths of DWARF sections and offsets relative to
/// the beginning of DWARF sections are represented using eight bytes".
///
/// This function is for reading such values.
fn readFormatSizedInt(r: *Reader, format: std.dwarf.Format, endian: Endian) !u64 {
    return switch (format) {
        .@"32" => try r.takeInt(u32, endian),
        .@"64" => try r.takeInt(u64, endian),
    };
}

fn readAddress(r: *Reader, endian: Endian, addr_size_bytes: u8) !u64 {
    return switch (addr_size_bytes) {
        2 => try r.takeInt(u16, endian),
        4 => try r.takeInt(u32, endian),
        8 => try r.takeInt(u64, endian),
        else => return bad(),
    };
}



---
File: /std/debug/ElfFile.zig
---

//! A helper type for loading an ELF file and collecting its DWARF debug information, unwind
//! information, and symbol table.
const ElfFile = @This();

const std = @import("std");
const Io = std.Io;
const Endian = std.builtin.Endian;
const Dwarf = std.debug.Dwarf;
const Allocator = std.mem.Allocator;
const elf = std.elf;

is_64: bool,
endian: Endian,

/// This is `null` iff any of the required DWARF sections were missing. `ElfFile.load` does *not*
/// call `Dwarf.open`, `Dwarf.scanAllFunctions`, etc; that is the caller's responsibility.
dwarf: ?Dwarf,

/// If non-`null`, describes the `.eh_frame` section, which can be used with `Dwarf.Unwind`.
eh_frame: ?UnwindSection,
/// If non-`null`, describes the `.debug_frame` section, which can be used with `Dwarf.Unwind`.
debug_frame: ?UnwindSection,

/// If non-`null`, this is the contents of the `.strtab` section.
strtab: ?[]const u8,
/// If non-`null`, describes the `.symtab` section.
symtab: ?SymtabSection,

/// Binary search table lazily populated by `searchSymtab`.
symbol_search_table: ?[]usize,

/// The memory-mapped ELF file, which is referenced by `dwarf`. This field is here only so that
/// this memory can be unmapped by `ElfFile.deinit`.
mapped_file: []align(std.heap.page_size_min) const u8,
/// Sometimes, debug info is stored separately to the main ELF file. In that case, `mapped_file`
/// is the mapped ELF binary, and `mapped_debug_file` is the mapped debug info file. Both must
/// be unmapped by `ElfFile.deinit`.
mapped_debug_file: ?[]align(std.heap.page_size_min) const u8,

arena: std.heap.ArenaAllocator.State,

pub const UnwindSection = struct {
    vaddr: u64,
    bytes: []const u8,
};
pub const SymtabSection = struct {
    entry_size: u64,
    bytes: []const u8,
};

pub const DebugInfoSearchPaths = struct {
    /// The location of a debuginfod client directory, which acts as a search path for build IDs. If
    /// given, we can load from this directory opportunistically, but make no effort to populate it.
    /// To avoid allocation when building the search paths, this is given as two components which
    /// will be concatenated.
    debuginfod_client: ?[2][]const u8,
    /// All "global debug directories" on the system. These are used as search paths for both debug
    /// links and build IDs. On typical systems this is just "/usr/lib/debug".
    global_debug: []const []const u8,
    /// The path to the dirname of the ELF file, which acts as a search path for debug links.
    exe_dir: ?[]const u8,

    pub const none: DebugInfoSearchPaths = .{
        .debuginfod_client = null,
        .global_debug = &.{},
        .exe_dir = null,
    };

    pub fn native(exe_path: []const u8) DebugInfoSearchPaths {
        if (std.Options.elf_debug_info_search_paths) |f| return f(exe_path);
        if (std.Options.debug_threaded_io) |t| return .{
            .debuginfod_client = p: {
                if (t.environString("DEBUGINFOD_CACHE_PATH")) |p| {
                    break :p .{ p, "" };
                }
                if (t.environString("XDG_CACHE_HOME")) |cache_path| {
                    break :p .{ cache_path, "/debuginfod_client" };
                }
                if (t.environString("HOME")) |home_path| {
                    break :p .{ home_path, "/.cache/debuginfod_client" };
                }
                break :p null;
            },
            .global_debug = &.{
                "/usr/lib/debug",
            },
            .exe_dir = std.fs.path.dirname(exe_path) orelse ".",
        };
        @compileError("std.Options.elf_debug_info_search_paths must be provided");
    }
};

pub fn deinit(ef: *ElfFile, gpa: Allocator) void {
    if (ef.dwarf) |*dwarf| dwarf.deinit(gpa);
    if (ef.symbol_search_table) |t| gpa.free(t);
    var arena = ef.arena.promote(gpa);
    arena.deinit();

    std.posix.munmap(ef.mapped_file);
    if (ef.mapped_debug_file) |m| std.posix.munmap(m);

    ef.* = undefined;
}

pub const LoadError = error{
    OutOfMemory,
    Overflow,
    TruncatedElfFile,
    InvalidCompressedSection,
    InvalidElfMagic,
    InvalidElfVersion,
    InvalidElfClass,
    InvalidElfEndian,
    // The remaining errors all occur when attemping to stat or mmap a file.
    SystemResources,
    MemoryMappingNotSupported,
    AccessDenied,
    LockedMemoryLimitExceeded,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    Streaming,
    Canceled,
    Unexpected,
};

pub fn load(
    gpa: Allocator,
    io: Io,
    elf_file: Io.File,
    opt_build_id: ?[]const u8,
    di_search_paths: *const DebugInfoSearchPaths,
) LoadError!ElfFile {
    var arena_instance: std.heap.ArenaAllocator = .init(gpa);
    errdefer arena_instance.deinit();
    const arena = arena_instance.allocator();

    var result = loadInner(arena, io, elf_file, null) catch |err| switch (err) {
        error.CrcMismatch => unreachable, // we passed crc as null
        else => |e| return e,
    };
    errdefer std.posix.munmap(result.mapped_mem);

    // `loadInner` did most of the work, but we might need to load an external debug info file

    const di_mapped_mem: ?[]align(std.heap.page_size_min) const u8 = load_di: {
        if (result.sections.get(.debug_info) != null and
            result.sections.get(.debug_abbrev) != null and
            result.sections.get(.debug_str) != null and
            result.sections.get(.debug_line) != null)
        {
            // The info is already loaded from this file alone!
            break :load_di null;
        }

        // We're missing some debug info---let's try and load it from a separate file.

        build_id: {
            const build_id = opt_build_id orelse break :build_id;
            if (build_id.len < 3) break :build_id;

            for (di_search_paths.global_debug) |global_debug| {
                if (try loadSeparateDebugFile(arena, io, &result, null, "{s}/.build-id/{x}/{x}.debug", .{
                    global_debug,
                    build_id[0..1],
                    build_id[1..],
                })) |mapped| break :load_di mapped;
            }

            if (di_search_paths.debuginfod_client) |components| {
                if (try loadSeparateDebugFile(arena, io, &result, null, "{s}{s}/{x}/debuginfo", .{
                    components[0],
                    components[1],
                    build_id,
                })) |mapped| break :load_di mapped;
            }
        }

        debug_link: {
            const section = result.sections.get(.gnu_debuglink) orelse break :debug_link;
            const debug_filename = std.mem.sliceTo(section.bytes, 0);
            const crc_offset = std.mem.alignForward(usize, debug_filename.len + 1, 4);
            if (section.bytes.len < crc_offset + 4) break :debug_link;
            const debug_crc = std.mem.readInt(u32, section.bytes[crc_offset..][0..4], result.endian);

            const exe_dir = di_search_paths.exe_dir orelse break :debug_link;

            if (try loadSeparateDebugFile(arena, io, &result, debug_crc, "{s}/{s}", .{
                exe_dir,
                debug_filename,
            })) |mapped| break :load_di mapped;
            if (try loadSeparateDebugFile(arena, io, &result, debug_crc, "{s}/.debug/{s}", .{
                exe_dir,
                debug_filename,
            })) |mapped| break :load_di mapped;
            for (di_search_paths.global_debug) |global_debug| {
                // This looks like a bug; it isn't. They really do embed the absolute path to the
                // exe's dirname, *under* the global debug path.
                if (try loadSeparateDebugFile(arena, io, &result, debug_crc, "{s}/{s}/{s}", .{
                    global_debug,
                    exe_dir,
                    debug_filename,
                })) |mapped| break :load_di mapped;
            }
        }

        break :load_di null;
    };
    errdefer comptime unreachable;

    return .{
        .is_64 = result.is_64,
        .endian = result.endian,
        .dwarf = dwarf: {
            if (result.sections.get(.debug_info) == null or
                result.sections.get(.debug_abbrev) == null or
                result.sections.get(.debug_str) == null or
                result.sections.get(.debug_line) == null)
            {
                break :dwarf null; // debug info not present
            }
            var sections: Dwarf.SectionArray = @splat(null);
            inline for (@typeInfo(Dwarf.Section.Id).@"enum".fields) |f| {
                if (result.sections.get(@field(Section.Id, f.name))) |s| {
                    sections[f.value] = .{ .data = s.bytes, .owned = false };
                }
            }
            break :dwarf .{ .sections = sections };
        },
        .eh_frame = if (result.sections.get(.eh_frame)) |s| .{
            .vaddr = s.header.sh_addr,
            .bytes = s.bytes,
        } else null,
        .debug_frame = if (result.sections.get(.debug_frame)) |s| .{
            .vaddr = s.header.sh_addr,
            .bytes = s.bytes,
        } else null,
        .strtab = if (result.sections.get(.strtab)) |s| s.bytes else null,
        .symtab = if (result.sections.get(.symtab)) |s| .{
            .entry_size = s.header.sh_entsize,
            .bytes = s.bytes,
        } else null,
        .symbol_search_table = null,
        .mapped_file = result.mapped_mem,
        .mapped_debug_file = di_mapped_mem,
        .arena = arena_instance.state,
    };
}

pub fn searchSymtab(ef: *ElfFile, gpa: Allocator, vaddr: u64) error{
    NoSymtab,
    NoStrtab,
    BadSymtab,
    OutOfMemory,
}!std.debug.Symbol {
    const symtab = ef.symtab orelse return error.NoSymtab;
    const strtab = ef.strtab orelse return error.NoStrtab;

    if (symtab.bytes.len % symtab.entry_size != 0) return error.BadSymtab;

    const swap_endian = ef.endian != @import("builtin").cpu.arch.endian();

    switch (ef.is_64) {
        inline true, false => |is_64| {
            const Sym = if (is_64) elf.Elf64_Sym else elf.Elf32_Sym;
            if (symtab.entry_size != @sizeOf(Sym)) return error.BadSymtab;
            const symbols: []align(1) const Sym = @ptrCast(symtab.bytes);
            if (ef.symbol_search_table == null) {
                ef.symbol_search_table = try buildSymbolSearchTable(gpa, ef.endian, Sym, symbols);
            }
            const search_table = ef.symbol_search_table.?;
            const SearchContext = struct {
                swap_endian: bool,
                target: u64,
                symbols: []align(1) const Sym,
                fn predicate(ctx: @This(), sym_index: usize) bool {
                    // We need to return `true` for the first N items, then `false` for the rest --
                    // the index we'll get out is the first `false` one. So, we'll return `true` iff
                    // the target address is after the *end* of this symbol. This synchronizes with
                    // the logic in `buildSymbolSearchTable` which sorts by *end* address.
                    var sym = ctx.symbols[sym_index];
                    if (ctx.swap_endian) std.mem.byteSwapAllFields(Sym, &sym);
                    const sym_end = sym.st_value + sym.st_size;
                    return ctx.target >= sym_end;
                }
            };
            const sym_index_index = std.sort.partitionPoint(usize, search_table, @as(SearchContext, .{
                .swap_endian = swap_endian,
                .target = vaddr,
                .symbols = symbols,
            }), SearchContext.predicate);
            if (sym_index_index == search_table.len) return .unknown;
            var sym = symbols[search_table[sym_index_index]];
            if (swap_endian) std.mem.byteSwapAllFields(Sym, &sym);
            if (vaddr < sym.st_value or vaddr >= sym.st_value + sym.st_size) return .unknown;
            return .{
                .name = std.mem.sliceTo(strtab[sym.st_name..], 0),
                .compile_unit_name = null,
                .source_location = null,
            };
        },
    }
}

fn buildSymbolSearchTable(gpa: Allocator, endian: Endian, comptime Sym: type, symbols: []align(1) const Sym) error{
    OutOfMemory,
    BadSymtab,
}![]usize {
    var result: std.ArrayList(usize) = .empty;
    defer result.deinit(gpa);

    const swap_endian = endian != @import("builtin").cpu.arch.endian();

    for (symbols, 0..) |sym_orig, sym_index| {
        var sym = sym_orig;
        if (swap_endian) std.mem.byteSwapAllFields(Sym, &sym);
        if (sym.st_name == 0) continue;
        if (sym.st_shndx == elf.SHN_UNDEF) continue;
        try result.append(gpa, sym_index);
    }

    const SortContext = struct {
        swap_endian: bool,
        symbols: []align(1) const Sym,
        fn lessThan(ctx: @This(), lhs_sym_index: usize, rhs_sym_index: usize) bool {
            // We sort by *end* address, not start address. This matches up with logic in `searchSymtab`.
            var lhs_sym = ctx.symbols[lhs_sym_index];
            var rhs_sym = ctx.symbols[rhs_sym_index];
            if (ctx.swap_endian) {
                std.mem.byteSwapAllFields(Sym, &lhs_sym);
                std.mem.byteSwapAllFields(Sym, &rhs_sym);
            }
            const lhs_val = lhs_sym.st_value + lhs_sym.st_size;
            const rhs_val = rhs_sym.st_value + rhs_sym.st_size;
            return lhs_val < rhs_val;
        }
    };
    std.mem.sort(usize, result.items, @as(SortContext, .{
        .swap_endian = swap_endian,
        .symbols = symbols,
    }), SortContext.lessThan);

    return result.toOwnedSlice(gpa);
}

/// Only used locally, during `load`.
const Section = struct {
    header: elf.Elf64_Shdr,
    bytes: []const u8,
    const Id = enum {
        // DWARF sections: see `Dwarf.Section.Id`.
        debug_info,
        debug_abbrev,
        debug_str,
        debug_str_offsets,
        debug_line,
        debug_line_str,
        debug_ranges,
        debug_loclists,
        debug_rnglists,
        debug_addr,
        debug_names,
        // Then anything else we're interested in.
        gnu_debuglink,
        eh_frame,
        debug_frame,
        symtab,
        strtab,
    };
    const Array = std.enums.EnumArray(Section.Id, ?Section);
};

fn loadSeparateDebugFile(
    arena: Allocator,
    io: Io,
    main_loaded: *LoadInnerResult,
    opt_crc: ?u32,
    comptime fmt: []const u8,
    args: anytype,
) Allocator.Error!?[]align(std.heap.page_size_min) const u8 {
    const path = try std.fmt.allocPrint(arena, fmt, args);
    const elf_file = Io.Dir.cwd().openFile(io, path, .{}) catch return null;
    defer elf_file.close(io);

    const result = loadInner(arena, io, elf_file, opt_crc) catch |err| switch (err) {
        error.OutOfMemory => |e| return e,
        error.CrcMismatch => return null,
        else => return null,
    };
    errdefer comptime unreachable;

    const have_debug_sections = inline for (@as([]const []const u8, &.{
        "debug_info",
        "debug_abbrev",
        "debug_str",
        "debug_line",
    })) |name| {
        const s = @field(Section.Id, name);
        if (main_loaded.sections.get(s) == null and result.sections.get(s) == null) {
            break false;
        }
    } else true;

    if (result.is_64 != main_loaded.is_64 or
        result.endian != main_loaded.endian or
        !have_debug_sections)
    {
        std.posix.munmap(result.mapped_mem);
        return null;
    }

    inline for (@typeInfo(Dwarf.Section.Id).@"enum".fields) |f| {
        const id = @field(Section.Id, f.name);
        if (main_loaded.sections.get(id) == null) {
            main_loaded.sections.set(id, result.sections.get(id));
        }
    }

    return result.mapped_mem;
}

const LoadInnerResult = struct {
    is_64: bool,
    endian: Endian,
    sections: Section.Array,
    mapped_mem: []align(std.heap.page_size_min) const u8,
};
fn loadInner(
    arena: Allocator,
    io: Io,
    elf_file: Io.File,
    opt_crc: ?u32,
) (LoadError || error{ CrcMismatch, Streaming, Canceled })!LoadInnerResult {
    const mapped_mem: []align(std.heap.page_size_min) const u8 = mapped: {
        const file_len = std.math.cast(
            usize,
            elf_file.length(io) catch |err| switch (err) {
                error.PermissionDenied => unreachable, // not asking for PROT_EXEC
                else => |e| return e,
            },
        ) orelse return error.Overflow;

        break :mapped std.posix.mmap(
            null,
            file_len,
            .{ .READ = true },
            .{ .TYPE = .SHARED },
            elf_file.handle,
            0,
        ) catch |err| switch (err) {
            error.MappingAlreadyExists => unreachable, // not using FIXED_NOREPLACE
            error.PermissionDenied => unreachable, // not asking for PROT_EXEC
            else => |e| return e,
        };
    };

    if (opt_crc) |crc| {
        if (std.hash.crc.Crc32.hash(mapped_mem) != crc) {
            return error.CrcMismatch;
        }
    }
    errdefer std.posix.munmap(mapped_mem);

    var fr: std.Io.Reader = .fixed(mapped_mem);

    const header = elf.Header.read(&fr) catch |err| switch (err) {
        error.ReadFailed => unreachable,
        error.EndOfStream => return error.TruncatedElfFile,

        error.InvalidElfMagic,
        error.InvalidElfVersion,
        error.InvalidElfClass,
        error.InvalidElfEndian,
        => |e| return e,
    };
    const endian = header.endian;

    const shstrtab_shdr_off = try std.math.add(
        u64,
        header.shoff,
        try std.math.mul(u64, header.shstrndx, header.shentsize),
    );
    fr.seek = std.math.cast(usize, shstrtab_shdr_off) orelse return error.Overflow;
    const shstrtab: []const u8 = if (header.is_64) shstrtab: {
        const shdr = fr.takeStruct(elf.Elf64_Shdr, endian) catch return error.TruncatedElfFile;
        if (shdr.sh_offset + shdr.sh_size > mapped_mem.len) return error.TruncatedElfFile;
        break :shstrtab mapped_mem[@intCast(shdr.sh_offset)..][0..@intCast(shdr.sh_size)];
    } else shstrtab: {
        const shdr = fr.takeStruct(elf.Elf32_Shdr, endian) catch return error.TruncatedElfFile;
        if (shdr.sh_offset + shdr.sh_size > mapped_mem.len) return error.TruncatedElfFile;
        break :shstrtab mapped_mem[@intCast(shdr.sh_offset)..][0..@intCast(shdr.sh_size)];
    };

    var sections: Section.Array = .initFill(null);

    var it = header.iterateSectionHeadersBuffer(mapped_mem);
    while (it.next() catch return error.TruncatedElfFile) |shdr| {
        if (shdr.sh_type == elf.SHT_NULL or shdr.sh_type == elf.SHT_NOBITS) continue;
        if (shdr.sh_name > shstrtab.len) return error.TruncatedElfFile;
        const name = std.mem.sliceTo(shstrtab[@intCast(shdr.sh_name)..], 0);

        const section_id: Section.Id = inline for (@typeInfo(Section.Id).@"enum".fields) |s| {
            if (std.mem.eql(u8, "." ++ s.name, name)) {
                break @enumFromInt(s.value);
            }
        } else continue;

        if (sections.get(section_id) != null) continue;

        if (shdr.sh_offset + shdr.sh_size > mapped_mem.len) return error.TruncatedElfFile;
        const raw_section_bytes = mapped_mem[@intCast(shdr.sh_offset)..][0..@intCast(shdr.sh_size)];
        const section_bytes: []const u8 = bytes: {
            if ((shdr.sh_flags & elf.SHF_COMPRESSED) == 0) break :bytes raw_section_bytes;

            var section_reader: std.Io.Reader = .fixed(raw_section_bytes);
            const ch_type: elf.COMPRESS, const ch_size: u64 = if (header.is_64) ch: {
                const chdr = section_reader.takeStruct(elf.Elf64_Chdr, endian) catch return error.InvalidCompressedSection;
                break :ch .{ chdr.ch_type, chdr.ch_size };
            } else ch: {
                const chdr = section_reader.takeStruct(elf.Elf32_Chdr, endian) catch return error.InvalidCompressedSection;
                break :ch .{ chdr.ch_type, chdr.ch_size };
            };
            if (ch_type != .ZLIB) {
                // The compression algorithm is unsupported, but don't make that a hard error; the
                // file might still be valid, and we might still be okay without this section.
                continue;
            }

            const buf = try arena.alloc(u8, std.math.cast(usize, ch_size) orelse return error.Overflow);
            var fw: std.Io.Writer = .fixed(buf);
            var decompress: std.compress.flate.Decompress = .init(&section_reader, .zlib, &.{});
            const n = decompress.reader.streamRemaining(&fw) catch |err| switch (err) {
                // If a write failed, then `buf` filled up, so `ch_size` was incorrect
                error.WriteFailed => return error.InvalidCompressedSection,
                // If a read failed, flate expected the section to have more data
                error.ReadFailed => return error.InvalidCompressedSection,
            };
            // It's also an error if the data is shorter than expected.
            if (n != buf.len) return error.InvalidCompressedSection;
            break :bytes buf;
        };
        sections.set(section_id, .{ .header = shdr, .bytes = section_bytes });
    }

    return .{
        .is_64 = header.is_64,
        .endian = endian,
        .sections = sections,
        .mapped_mem = mapped_mem,
    };
}



---
File: /std/debug/Info.zig
---

//! Cross-platform abstraction for loading debug information into an in-memory
//! format that supports queries such as "what is the source location of this
//! virtual memory address?"
//!
//! Unlike `std.debug.SelfInfo`, this API does not assume the debug information
//! in question happens to match the host CPU architecture, OS, or other target
//! properties.
const Info = @This();

const std = @import("../std.zig");
const Io = std.Io;
const Allocator = std.mem.Allocator;
const Path = std.Build.Cache.Path;
const assert = std.debug.assert;
const Coverage = std.debug.Coverage;
const SourceLocation = std.debug.Coverage.SourceLocation;
const ElfFile = std.debug.ElfFile;
const MachOFile = std.debug.MachOFile;

impl: union(enum) {
    elf: ElfFile,
    macho: MachOFile,
},
/// Externally managed, outlives this `Info` instance.
coverage: *Coverage,

pub const LoadError = error{
    MissingDebugInfo,
    UnsupportedDebugInfo,
} || Io.File.OpenError || ElfFile.LoadError || MachOFile.Error || std.debug.Dwarf.ScanError;

pub fn load(
    gpa: Allocator,
    io: Io,
    path: Path,
    coverage: *Coverage,
    format: std.Target.ObjectFormat,
    arch: std.Target.Cpu.Arch,
) LoadError!Info {
    switch (format) {
        .elf => {
            var file = try path.root_dir.handle.openFile(io, path.sub_path, .{});
            defer file.close(io);

            var elf_file: ElfFile = try .load(gpa, io, file, null, &.none);
            errdefer elf_file.deinit(gpa);

            if (elf_file.dwarf == null) return error.MissingDebugInfo;
            try elf_file.dwarf.?.open(gpa, elf_file.endian);
            try elf_file.dwarf.?.populateRanges(gpa, elf_file.endian);

            return .{
                .impl = .{ .elf = elf_file },
                .coverage = coverage,
            };
        },
        .macho => {
            const path_str = try path.toString(gpa);
            defer gpa.free(path_str);

            var macho_file: MachOFile = try .load(gpa, io, path_str, arch);
            errdefer macho_file.deinit(gpa);

            return .{
                .impl = .{ .macho = macho_file },
                .coverage = coverage,
            };
        },
        else => return error.UnsupportedDebugInfo,
    }
}

pub fn deinit(info: *Info, gpa: Allocator) void {
    switch (info.impl) {
        .elf => |*ef| ef.deinit(gpa),
        .macho => |*mf| mf.deinit(gpa),
    }
    info.* = undefined;
}

pub const ResolveAddressesError = Coverage.ResolveAddressesDwarfError || error{UnsupportedDebugInfo};

/// Given an array of virtual memory addresses, sorted ascending, outputs a
/// corresponding array of source locations.
pub fn resolveAddresses(
    info: *Info,
    gpa: Allocator,
    io: Io,
    /// Asserts the addresses are in ascending order.
    sorted_pc_addrs: []const u64,
    /// Asserts its length equals length of `sorted_pc_addrs`.
    output: []SourceLocation,
) ResolveAddressesError!void {
    assert(sorted_pc_addrs.len == output.len);
    switch (info.impl) {
        .elf => |*ef| return info.coverage.resolveAddressesDwarf(gpa, io, ef.endian, sorted_pc_addrs, output, &ef.dwarf.?),
        .macho => |*mf| {
            // Resolving all of the addresses at once unfortunately isn't so easy in Mach-O binaries
            // due to split debug information. For now, we'll just resolve the addreses one by one.
            for (sorted_pc_addrs, output) |pc_addr, *src_loc| {
                const dwarf, const dwarf_pc_addr = mf.getDwarfForAddress(gpa, io, pc_addr) catch |err| switch (err) {
                    error.MissingDebugInfo => {
                        src_loc.* = .invalid;
                        continue;
                    },
                    error.InvalidMachO, error.InvalidDwarf => return error.InvalidDebugInfo,
                    else => |e| return e,
                };
                if (dwarf.ranges.items.len == 0) {
                    dwarf.populateRanges(gpa, .little) catch |err| switch (err) {
                        error.EndOfStream,
                        error.Overflow,
                        error.StreamTooLong,
                        error.ReadFailed,
                        => return error.InvalidDebugInfo,
                        else => |e| return e,
                    };
                }
                try info.coverage.resolveAddressesDwarf(gpa, io, .little, &.{dwarf_pc_addr}, src_loc[0..1], dwarf);
            }
        },
    }
}



---
File: /std/debug/MachOFile.zig
---

mapped_memory: []align(std.heap.page_size_min) const u8,
symbols: []const Symbol,
strings: []const u8,
text_vmaddr: u64,

/// Key is index into `strings` of the file path.
ofiles: std.AutoArrayHashMapUnmanaged(u32, Error!OFile),

pub const Error = error{
    InvalidMachO,
    InvalidDwarf,
    MissingDebugInfo,
    UnsupportedDebugInfo,
    ReadFailed,
    OutOfMemory,
};

pub fn deinit(mf: *MachOFile, gpa: Allocator) void {
    for (mf.ofiles.values()) |*maybe_of| {
        const of = &(maybe_of.* catch continue);
        posix.munmap(of.mapped_memory);
        of.dwarf.deinit(gpa);
        of.symbols_by_name.deinit(gpa);
    }
    mf.ofiles.deinit(gpa);
    gpa.free(mf.symbols);
    posix.munmap(mf.mapped_memory);
}

pub fn load(gpa: Allocator, io: Io, path: []const u8, arch: std.Target.Cpu.Arch) Error!MachOFile {
    switch (arch) {
        .x86_64, .aarch64 => {},
        else => unreachable,
    }

    const all_mapped_memory = try mapDebugInfoFile(io, path);
    errdefer posix.munmap(all_mapped_memory);

    // In most cases, the file we just mapped is a Mach-O binary. However, it could be a "universal
    // binary": a simple file format which contains Mach-O binaries for multiple targets. For
    // instance, `/usr/lib/dyld` is currently distributed as a universal binary containing images
    // for both ARM64 macOS and x86_64 macOS.
    if (all_mapped_memory.len < 4) return error.InvalidMachO;
    const magic = std.mem.readInt(u32, all_mapped_memory.ptr[0..4], .little);

    // The contents of a Mach-O file, which may or may not be the whole of `all_mapped_memory`.
    const mapped_macho = switch (magic) {
        macho.MH_MAGIC_64 => all_mapped_memory,

        macho.FAT_CIGAM => mapped_macho: {
            // This is the universal binary format (aka a "fat binary").
            var fat_r: Io.Reader = .fixed(all_mapped_memory);
            const hdr = fat_r.takeStruct(macho.fat_header, .big) catch |err| switch (err) {
                error.ReadFailed => unreachable,
                error.EndOfStream => return error.InvalidMachO,
            };
            const want_cpu_type = switch (arch) {
                .x86_64 => macho.CPU_TYPE_X86_64,
                .aarch64 => macho.CPU_TYPE_ARM64,
                else => unreachable,
            };
            for (0..hdr.nfat_arch) |_| {
                const fat_arch = fat_r.takeStruct(macho.fat_arch, .big) catch |err| switch (err) {
                    error.ReadFailed => unreachable,
                    error.EndOfStream => return error.InvalidMachO,
                };
                if (fat_arch.cputype != want_cpu_type) continue;
                if (fat_arch.offset + fat_arch.size > all_mapped_memory.len) return error.InvalidMachO;
                break :mapped_macho all_mapped_memory[fat_arch.offset..][0..fat_arch.size];
            }
            // `arch` was not present in the fat binary.
            return error.MissingDebugInfo;
        },

        // Even on modern 64-bit targets, this format doesn't seem to be too extensively used. It
        // will be fairly easy to add support here if necessary; it's very similar to above.
        macho.FAT_CIGAM_64 => return error.UnsupportedDebugInfo,

        else => return error.InvalidMachO,
    };

    var r: Io.Reader = .fixed(mapped_macho);
    const hdr = r.takeStruct(macho.mach_header_64, .little) catch |err| switch (err) {
        error.ReadFailed => unreachable,
        error.EndOfStream => return error.InvalidMachO,
    };

    if (hdr.magic != macho.MH_MAGIC_64)
        return error.InvalidMachO;

    const symtab: macho.symtab_command, const text_vmaddr: u64 = lcs: {
        var it: macho.LoadCommandIterator = try .init(&hdr, mapped_macho[@sizeOf(macho.mach_header_64)..]);
        var symtab: ?macho.symtab_command = null;
        var text_vmaddr: ?u64 = null;
        while (try it.next()) |cmd| switch (cmd.hdr.cmd) {
            .SYMTAB => symtab = cmd.cast(macho.symtab_command) orelse return error.InvalidMachO,
            .SEGMENT_64 => if (cmd.cast(macho.segment_command_64)) |seg_cmd| {
                if (!mem.eql(u8, seg_cmd.segName(), "__TEXT")) continue;
                text_vmaddr = seg_cmd.vmaddr;
            },
            else => {},
        };
        break :lcs .{
            symtab orelse return error.MissingDebugInfo,
            text_vmaddr orelse return error.MissingDebugInfo,
        };
    };

    const strings = mapped_macho[symtab.stroff..][0 .. symtab.strsize - 1];

    var symbols: std.ArrayList(Symbol) = try .initCapacity(gpa, symtab.nsyms);
    defer symbols.deinit(gpa);

    // This map is temporary; it is used only to detect duplicates here. This is
    // necessary because we prefer to use STAB ("symbolic debugging table") symbols,
    // but they might not be present, so we track normal symbols too.
    // Indices match 1-1 with those of `symbols`.
    var symbol_names: std.StringArrayHashMapUnmanaged(void) = .empty;
    defer symbol_names.deinit(gpa);
    try symbol_names.ensureUnusedCapacity(gpa, symtab.nsyms);

    var ofile: u32 = undefined;
    var last_sym: Symbol = undefined;
    var state: enum {
        init,
        oso_open,
        oso_close,
        bnsym,
        fun_strx,
        fun_size,
        ensym,
    } = .init;

    var sym_r: Io.Reader = .fixed(mapped_macho[symtab.symoff..]);
    for (0..symtab.nsyms) |_| {
        const sym = sym_r.takeStruct(macho.nlist_64, .little) catch |err| switch (err) {
            error.ReadFailed => unreachable,
            error.EndOfStream => return error.InvalidMachO,
        };
        if (sym.n_type.bits.is_stab == 0) {
            if (sym.n_strx == 0) continue;
            switch (sym.n_type.bits.type) {
                .undf, .pbud, .indr, .abs, _ => continue,
                .sect => {
                    const name = std.mem.sliceTo(strings[sym.n_strx..], 0);
                    const gop = symbol_names.getOrPutAssumeCapacity(name);
                    if (!gop.found_existing) {
                        assert(gop.index == symbols.items.len);
                        symbols.appendAssumeCapacity(.{
                            .strx = sym.n_strx,
                            .addr = sym.n_value,
                            .ofile = Symbol.unknown_ofile,
                        });
                    }
                },
            }
            continue;
        }

        // TODO handle globals N_GSYM, and statics N_STSYM
        //
        // NOTE: ld64.lld and Apple's ld differ in STABS layout.
        // Apple's ld emit N_BNSYM and N_ENSYM to mark the start and end of
        // functions, while ld64.lld doesn't.
        switch (sym.n_type.stab) {
            .oso => switch (state) {
                .init, .oso_close => {
                    state = .oso_open;
                    ofile = sym.n_strx;
                },
                else => return error.InvalidMachO,
            },
            .bnsym => switch (state) {
                .oso_open, .ensym => {
                    state = .bnsym;
                    last_sym = .{
                        .strx = 0,
                        .addr = sym.n_value,
                        .ofile = ofile,
                    };
                },
                else => return error.InvalidMachO,
            },
            .fun => switch (state) {
                .oso_open => {
                    state = .fun_strx;
                    last_sym = .{
                        .strx = sym.n_strx,
                        .addr = sym.n_value,
                        .ofile = ofile,
                    };
                },
                .bnsym => {
                    state = .fun_strx;
                    last_sym.strx = sym.n_strx;
                },
                .fun_strx => {
                    state = .fun_size;
                },
                .fun_size => {
                    if (last_sym.strx != 0) {
                        appendStabSymbol(&symbols, &symbol_names, strings, last_sym);
                    }
                    last_sym = .{
                        .strx = sym.n_strx,
                        .addr = sym.n_value,
                        .ofile = ofile,
                    };
                    state = .fun_strx;
                },
                else => return error.InvalidMachO,
            },
            .ensym => switch (state) {
                .fun_size => {
                    state = .ensym;
                    if (last_sym.strx != 0) {
                        appendStabSymbol(&symbols, &symbol_names, strings, last_sym);
                    }
                },
                else => return error.InvalidMachO,
            },
            .so => switch (state) {
                .init, .oso_close => {},
                .oso_open, .ensym => {
                    state = .oso_close;
                },
                .fun_size => {
                    state = .oso_close;
                    if (last_sym.strx != 0) {
                        appendStabSymbol(&symbols, &symbol_names, strings, last_sym);
                    }
                },
                else => return error.InvalidMachO,
            },
            else => {},
        }
    }

    switch (state) {
        .init => {
            // Missing STAB symtab entries is still okay, unless there were also no normal symbols.
            if (symbols.items.len == 0) return error.MissingDebugInfo;
        },
        .oso_close => {},
        else => return error.InvalidMachO, // corrupted STAB entries in symtab
    }

    const symbols_slice = try symbols.toOwnedSlice(gpa);
    errdefer gpa.free(symbols_slice);

    // Even though lld emits symbols in ascending order, this debug code
    // should work for programs linked in any valid way.
    // This sort is so that we can binary search later.
    mem.sort(Symbol, symbols_slice, {}, Symbol.addressLessThan);

    return .{
        .mapped_memory = all_mapped_memory,
        .symbols = symbols_slice,
        .strings = strings,
        .ofiles = .empty,
        .text_vmaddr = text_vmaddr,
    };
}
pub fn getDwarfForAddress(mf: *MachOFile, gpa: Allocator, io: Io, vaddr: u64) !struct { *Dwarf, u64 } {
    const symbol = Symbol.find(mf.symbols, vaddr) orelse return error.MissingDebugInfo;

    if (symbol.ofile == Symbol.unknown_ofile) return error.MissingDebugInfo;

    // offset of `address` from start of `symbol`
    const address_symbol_offset = vaddr - symbol.addr;

    // Take the symbol name from the N_FUN STAB entry, we're going to
    // use it if we fail to find the DWARF infos
    const stab_symbol = mem.sliceTo(mf.strings[symbol.strx..], 0);

    const gop = try mf.ofiles.getOrPut(gpa, symbol.ofile);
    if (!gop.found_existing) {
        const name = mem.sliceTo(mf.strings[symbol.ofile..], 0);
        gop.value_ptr.* = loadOFile(gpa, io, name);
    }
    const of = &(gop.value_ptr.* catch |err| return err);

    const symbol_index = of.symbols_by_name.getKeyAdapted(
        @as([]const u8, stab_symbol),
        @as(OFile.SymbolAdapter, .{ .strtab = of.strtab, .symtab_raw = of.symtab_raw }),
    ) orelse return error.MissingDebugInfo;

    const symbol_ofile_vaddr = vaddr: {
        var sym = of.symtab_raw[symbol_index];
        if (builtin.cpu.arch.endian() != .little) std.mem.byteSwapAllFields(macho.nlist_64, &sym);
        break :vaddr sym.n_value;
    };

    return .{ &of.dwarf, symbol_ofile_vaddr + address_symbol_offset };
}
pub fn lookupSymbolName(mf: *MachOFile, vaddr: u64) error{MissingDebugInfo}![]const u8 {
    const symbol = Symbol.find(mf.symbols, vaddr) orelse return error.MissingDebugInfo;
    return mem.sliceTo(mf.strings[symbol.strx..], 0);
}

const OFile = struct {
    mapped_memory: []align(std.heap.page_size_min) const u8,
    dwarf: Dwarf,
    strtab: []const u8,
    symtab_raw: []align(1) const macho.nlist_64,
    /// All named symbols in `symtab_raw`. Stored `u32` key is the index into `symtab_raw`. Accessed
    /// through `SymbolAdapter`, so that the symbol name is used as the logical key.
    symbols_by_name: std.ArrayHashMapUnmanaged(u32, void, void, true),

    const SymbolAdapter = struct {
        strtab: []const u8,
        symtab_raw: []align(1) const macho.nlist_64,
        pub fn hash(ctx: SymbolAdapter, sym_name: []const u8) u32 {
            _ = ctx;
            return @truncate(std.hash.Wyhash.hash(0, sym_name));
        }
        pub fn eql(ctx: SymbolAdapter, a_sym_name: []const u8, b_sym_index: u32, b_index: usize) bool {
            _ = b_index;
            var b_sym = ctx.symtab_raw[b_sym_index];
            if (builtin.cpu.arch.endian() != .little) std.mem.byteSwapAllFields(macho.nlist_64, &b_sym);
            const b_sym_name = std.mem.sliceTo(ctx.strtab[b_sym.n_strx..], 0);
            return mem.eql(u8, a_sym_name, b_sym_name);
        }
    };
};

const Symbol = struct {
    strx: u32,
    addr: u64,
    /// Value may be `unknown_ofile`.
    ofile: u32,
    const unknown_ofile = std.math.maxInt(u32);
    fn addressLessThan(context: void, lhs: Symbol, rhs: Symbol) bool {
        _ = context;
        return lhs.addr < rhs.addr;
    }
    /// Assumes that `symbols` is sorted in order of ascending `addr`.
    fn find(symbols: []const Symbol, address: usize) ?*const Symbol {
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
        const symbols: []const Symbol = &.{
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
    _ = Symbol;
}

fn appendStabSymbol(
    symbols: *std.ArrayList(Symbol),
    symbol_names: *std.StringArrayHashMapUnmanaged(void),
    strings: []const u8,
    last_sym: Symbol,
) void {
    const name = std.mem.sliceTo(strings[last_sym.strx..], 0);
    const gop = symbol_names.getOrPutAssumeCapacity(name);
    if (!gop.found_existing) {
        assert(gop.index == symbols.items.len);
        symbols.appendAssumeCapacity(last_sym);
    } else {
        symbols.items[gop.index] = last_sym;
    }
}

fn loadOFile(gpa: Allocator, io: Io, o_file_name: []const u8) !OFile {
    const all_mapped_memory, const mapped_ofile = map: {
        const open_paren = paren: {
            if (std.mem.endsWith(u8, o_file_name, ")")) {
                if (std.mem.findScalarLast(u8, o_file_name, '(')) |i| {
                    break :paren i;
                }
            }
            // Not an archive, just a normal path to a .o file
            const m = try mapDebugInfoFile(io, o_file_name);
            break :map .{ m, m };
        };

        // We have the form 'path/to/archive.a(entry.o)'. Map the archive and find the object file in question.

        const archive_path = o_file_name[0..open_paren];
        const target_name_in_archive = o_file_name[open_paren + 1 .. o_file_name.len - 1];
        const mapped_archive = try mapDebugInfoFile(io, archive_path);
        errdefer posix.munmap(mapped_archive);

        var ar_reader: Io.Reader = .fixed(mapped_archive);
        const ar_magic = ar_reader.take(8) catch return error.InvalidMachO;
        if (!std.mem.eql(u8, ar_magic, "!<arch>\n")) return error.InvalidMachO;
        while (true) {
            if (ar_reader.seek == ar_reader.buffer.len) return error.MissingDebugInfo;

            const raw_name = ar_reader.takeArray(16) catch return error.InvalidMachO;
            ar_reader.discardAll(12 + 6 + 6 + 8) catch return error.InvalidMachO;
            const raw_size = ar_reader.takeArray(10) catch return error.InvalidMachO;
            const file_magic = ar_reader.takeArray(2) catch return error.InvalidMachO;
            if (!std.mem.eql(u8, file_magic, "`\n")) return error.InvalidMachO;

            const size = std.fmt.parseInt(u32, mem.sliceTo(raw_size, ' '), 10) catch return error.InvalidMachO;
            const raw_data = ar_reader.take(size) catch return error.InvalidMachO;

            const entry_name: []const u8, const entry_contents: []const u8 = entry: {
                if (!std.mem.startsWith(u8, raw_name, "#1/")) {
                    break :entry .{ mem.sliceTo(raw_name, '/'), raw_data };
                }
                const len = std.fmt.parseInt(u32, mem.sliceTo(raw_name[3..], ' '), 10) catch return error.InvalidMachO;
                if (len > size) return error.InvalidMachO;
                break :entry .{ mem.sliceTo(raw_data[0..len], 0), raw_data[len..] };
            };

            if (std.mem.eql(u8, entry_name, target_name_in_archive)) {
                break :map .{ mapped_archive, entry_contents };
            }
        }
    };
    errdefer posix.munmap(all_mapped_memory);

    var r: Io.Reader = .fixed(mapped_ofile);
    const hdr = r.takeStruct(macho.mach_header_64, .little) catch |err| switch (err) {
        error.ReadFailed => unreachable,
        error.EndOfStream => return error.InvalidMachO,
    };
    if (hdr.magic != std.macho.MH_MAGIC_64) return error.InvalidMachO;

    const seg_cmd: macho.LoadCommandIterator.LoadCommand, const symtab_cmd: macho.symtab_command = cmds: {
        var seg_cmd: ?macho.LoadCommandIterator.LoadCommand = null;
        var symtab_cmd: ?macho.symtab_command = null;
        var it: macho.LoadCommandIterator = try .init(&hdr, mapped_ofile[@sizeOf(macho.mach_header_64)..]);
        while (try it.next()) |lc| switch (lc.hdr.cmd) {
            .SEGMENT_64 => seg_cmd = lc,
            .SYMTAB => symtab_cmd = lc.cast(macho.symtab_command) orelse return error.InvalidMachO,
            else => {},
        };
        break :cmds .{
            seg_cmd orelse return error.MissingDebugInfo,
            symtab_cmd orelse return error.MissingDebugInfo,
        };
    };

    if (mapped_ofile.len < symtab_cmd.stroff + symtab_cmd.strsize) return error.InvalidMachO;
    if (mapped_ofile[symtab_cmd.stroff + symtab_cmd.strsize - 1] != 0) return error.InvalidMachO;
    const strtab = mapped_ofile[symtab_cmd.stroff..][0 .. symtab_cmd.strsize - 1];

    const n_sym_bytes = symtab_cmd.nsyms * @sizeOf(macho.nlist_64);
    if (mapped_ofile.len < symtab_cmd.symoff + n_sym_bytes) return error.InvalidMachO;
    const symtab_raw: []align(1) const macho.nlist_64 = @ptrCast(mapped_ofile[symtab_cmd.symoff..][0..n_sym_bytes]);

    // TODO handle tentative (common) symbols
    var symbols_by_name: std.ArrayHashMapUnmanaged(u32, void, void, true) = .empty;
    defer symbols_by_name.deinit(gpa);
    try symbols_by_name.ensureUnusedCapacity(gpa, @intCast(symtab_raw.len));
    for (symtab_raw, 0..) |sym_raw, sym_index| {
        var sym = sym_raw;
        if (builtin.cpu.arch.endian() != .little) std.mem.byteSwapAllFields(macho.nlist_64, &sym);
        if (sym.n_strx == 0) continue;
        switch (sym.n_type.bits.type) {
            .undf => continue, // includes tentative symbols
            .abs => continue,
            else => {},
        }
        const sym_name = mem.sliceTo(strtab[sym.n_strx..], 0);
        const gop = symbols_by_name.getOrPutAssumeCapacityAdapted(
            @as([]const u8, sym_name),
            @as(OFile.SymbolAdapter, .{ .strtab = strtab, .symtab_raw = symtab_raw }),
        );
        if (gop.found_existing) return error.InvalidMachO;
        gop.key_ptr.* = @intCast(sym_index);
    }

    var sections: Dwarf.SectionArray = @splat(null);
    for (seg_cmd.getSections()) |sect_raw| {
        var sect = sect_raw;
        if (builtin.cpu.arch.endian() != .little) std.mem.byteSwapAllFields(macho.section_64, &sect);

        if (!std.mem.eql(u8, "__DWARF", sect.segName())) continue;

        const section_index: usize = inline for (@typeInfo(Dwarf.Section.Id).@"enum".fields, 0..) |section, i| {
            if (mem.eql(u8, "__" ++ section.name, sect.sectName())) break i;
        } else continue;

        if (mapped_ofile.len < sect.offset + sect.size) return error.InvalidMachO;
        const section_bytes = mapped_ofile[sect.offset..][0..sect.size];
        sections[section_index] = .{
            .data = section_bytes,
            .owned = false,
        };
    }

    if (sections[@intFromEnum(Dwarf.Section.Id.debug_info)] == null or
        sections[@intFromEnum(Dwarf.Section.Id.debug_abbrev)] == null or
        sections[@intFromEnum(Dwarf.Section.Id.debug_str)] == null or
        sections[@intFromEnum(Dwarf.Section.Id.debug_line)] == null)
    {
        return error.MissingDebugInfo;
    }

    var dwarf: Dwarf = .{ .sections = sections };
    errdefer dwarf.deinit(gpa);
    dwarf.open(gpa, .little) catch |err| switch (err) {
        error.InvalidDebugInfo,
        error.EndOfStream,
        error.Overflow,
        error.StreamTooLong,
        => return error.InvalidDwarf,

        error.MissingDebugInfo,
        error.ReadFailed,
        error.OutOfMemory,
        => |e| return e,
    };

    return .{
        .mapped_memory = all_mapped_memory,
        .dwarf = dwarf,
        .strtab = strtab,
        .symtab_raw = symtab_raw,
        .symbols_by_name = symbols_by_name.move(),
    };
}

/// Uses `mmap` to map the file at `path` into memory.
fn mapDebugInfoFile(io: Io, path: []const u8) ![]align(std.heap.page_size_min) const u8 {
    const file = Io.Dir.cwd().openFile(io, path, .{}) catch |err| switch (err) {
        error.FileNotFound => return error.MissingDebugInfo,
        else => return error.ReadFailed,
    };
    defer file.close(io);

    const file_len = std.math.cast(
        usize,
        file.length(io) catch return error.ReadFailed,
    ) orelse return error.ReadFailed;

    return posix.mmap(
        null,
        file_len,
        .{ .READ = true },
        .{ .TYPE = .SHARED },
        file.handle,
        0,
    ) catch return error.ReadFailed;
}

const std = @import("std");
const Allocator = std.mem.Allocator;
const Dwarf = std.debug.Dwarf;
const Io = std.Io;
const assert = std.debug.assert;
const posix = std.posix;
const macho = std.macho;
const mem = std.mem;
const testing = std.testing;

const builtin = @import("builtin");

const MachOFile = @This();



---
File: /std/debug/no_panic.zig
---

//! This namespace can be used with `pub const panic = std.debug.no_panic;` in the root file.
//! It emits as little code as possible, for testing purposes.
//!
//! For a functional alternative, see `std.debug.FullPanic`.

const std = @import("../std.zig");

pub fn call(_: []const u8, _: ?usize) noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn sentinelMismatch(_: anytype, _: anytype) noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn unwrapError(_: anyerror) noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn outOfBounds(_: usize, _: usize) noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn startGreaterThanEnd(_: usize, _: usize) noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn inactiveUnionField(_: anytype, _: anytype) noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn sliceCastLenRemainder(_: usize) noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn reachedUnreachable() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn unwrapNull() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn castToNull() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn incorrectAlignment() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn invalidErrorCode() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn integerOutOfBounds() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn integerOverflow() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn shlOverflow() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn shrOverflow() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn divideByZero() noreturn {
    @branchHint(.cold);
    @trap();
}

pub fn exactDivisionRemainder() no
```
