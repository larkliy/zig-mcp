```
  \\
                            \\========= expected not to find: ===================
                            \\{f}
                            \\========= but parsed file does contain it: ========
                            \\{f}
                            \\========= file path: ==============================
                            \\{f}
                        , .{
                            fmtMessageString(chk.kind, act.phrase.resolve(b, step)),
                            fmtMessageString(chk.kind, output),
                            src_path,
                        });
                    }
                },

                .extract => {
                    while (it.next()) |line| {
                        if (try act.extract(b, step, line, &vars)) break;
                    } else {
                        return step.fail(
                            \\
                            \\========= expected to find and extract: ==============
                            \\{f}
                            \\========= but parsed file does not contain it: =======
                            \\{f}
                            \\========= file path: ==============================
                            \\{f}
                        , .{
                            fmtMessageString(chk.kind, act.phrase.resolve(b, step)),
                            fmtMessageString(chk.kind, output),
                            src_path,
                        });
                    }
                },

                .compute_cmp => unreachable,
            }
        }
    }
}

const MachODumper = struct {
    const dyld_rebase_label = "dyld rebase data";
    const dyld_bind_label = "dyld bind data";
    const dyld_weak_bind_label = "dyld weak bind data";
    const dyld_lazy_bind_label = "dyld lazy bind data";
    const exports_label = "exports data";
    const symtab_label = "symbol table";
    const indirect_symtab_label = "indirect symbol table";

    fn parseAndDump(step: *Step, check: Check, bytes: []const u8) ![]const u8 {
        // TODO: handle archives and fat files
        return parseAndDumpObject(step, check, bytes);
    }

    const ObjectContext = struct {
        gpa: Allocator,
        data: []const u8,
        header: macho.mach_header_64,
        segments: std.ArrayList(macho.segment_command_64) = .empty,
        sections: std.ArrayList(macho.section_64) = .empty,
        symtab: std.ArrayList(macho.nlist_64) = .empty,
        strtab: std.ArrayList(u8) = .empty,
        indsymtab: std.ArrayList(u32) = .empty,
        imports: std.ArrayList([]const u8) = .empty,

        fn parse(ctx: *ObjectContext) !void {
            var it = try ctx.getLoadCommandIterator();
            var i: usize = 0;
            while (try it.next()) |cmd| {
                switch (cmd.hdr.cmd) {
                    .SEGMENT_64 => {
                        const seg = cmd.cast(macho.segment_command_64).?;
                        try ctx.segments.append(ctx.gpa, seg);
                        try ctx.sections.ensureUnusedCapacity(ctx.gpa, seg.nsects);
                        for (cmd.getSections()) |sect| {
                            ctx.sections.appendAssumeCapacity(sect);
                        }
                    },
                    .SYMTAB => {
                        const lc = cmd.cast(macho.symtab_command).?;
                        const symtab = @as([*]align(1) const macho.nlist_64, @ptrCast(ctx.data.ptr + lc.symoff))[0..lc.nsyms];
                        const strtab = ctx.data[lc.stroff..][0..lc.strsize];
                        try ctx.symtab.appendUnalignedSlice(ctx.gpa, symtab);
                        try ctx.strtab.appendSlice(ctx.gpa, strtab);
                    },
                    .DYSYMTAB => {
                        const lc = cmd.cast(macho.dysymtab_command).?;
                        const indexes = @as([*]align(1) const u32, @ptrCast(ctx.data.ptr + lc.indirectsymoff))[0..lc.nindirectsyms];
                        try ctx.indsymtab.appendUnalignedSlice(ctx.gpa, indexes);
                    },
                    .LOAD_DYLIB,
                    .LOAD_WEAK_DYLIB,
                    .REEXPORT_DYLIB,
                    => {
                        try ctx.imports.append(ctx.gpa, cmd.getDylibPathName());
                    },
                    else => {},
                }

                i += 1;
            }
        }

        fn getString(ctx: ObjectContext, off: u32) [:0]const u8 {
            assert(off < ctx.strtab.items.len);
            return mem.sliceTo(@as([*:0]const u8, @ptrCast(ctx.strtab.items.ptr + off)), 0);
        }

        fn getLoadCommandIterator(ctx: ObjectContext) !macho.LoadCommandIterator {
            return .init(&ctx.header, ctx.data[@sizeOf(macho.mach_header_64)..]);
        }

        fn getLoadCommand(ctx: ObjectContext, cmd: macho.LC) !?macho.LoadCommandIterator.LoadCommand {
            var it = try ctx.getLoadCommandIterator();
            while (try it.next()) |lc| if (lc.hdr.cmd == cmd) {
                return lc;
            };
            return null;
        }

        fn getSegmentByName(ctx: ObjectContext, name: []const u8) ?macho.segment_command_64 {
            for (ctx.segments.items) |seg| {
                if (mem.eql(u8, seg.segName(), name)) return seg;
            }
            return null;
        }

        fn getSectionByName(ctx: ObjectContext, segname: []const u8, sectname: []const u8) ?macho.section_64 {
            for (ctx.sections.items) |sect| {
                if (mem.eql(u8, sect.segName(), segname) and mem.eql(u8, sect.sectName(), sectname)) return sect;
            }
            return null;
        }

        fn dumpHeader(hdr: macho.mach_header_64, writer: anytype) !void {
            const cputype = switch (hdr.cputype) {
                macho.CPU_TYPE_ARM64 => "ARM64",
                macho.CPU_TYPE_X86_64 => "X86_64",
                else => "Unknown",
            };
            const filetype = switch (hdr.filetype) {
                macho.MH_OBJECT => "MH_OBJECT",
                macho.MH_EXECUTE => "MH_EXECUTE",
                macho.MH_FVMLIB => "MH_FVMLIB",
                macho.MH_CORE => "MH_CORE",
                macho.MH_PRELOAD => "MH_PRELOAD",
                macho.MH_DYLIB => "MH_DYLIB",
                macho.MH_DYLINKER => "MH_DYLINKER",
                macho.MH_BUNDLE => "MH_BUNDLE",
                macho.MH_DYLIB_STUB => "MH_DYLIB_STUB",
                macho.MH_DSYM => "MH_DSYM",
                macho.MH_KEXT_BUNDLE => "MH_KEXT_BUNDLE",
                else => "Unknown",
            };

            try writer.print(
                \\header
                \\cputype {s}
                \\filetype {s}
                \\ncmds {d}
                \\sizeofcmds {x}
                \\flags
            , .{
                cputype,
                filetype,
                hdr.ncmds,
                hdr.sizeofcmds,
            });

            if (hdr.flags > 0) {
                if (hdr.flags & macho.MH_NOUNDEFS != 0) try writer.writeAll(" NOUNDEFS");
                if (hdr.flags & macho.MH_INCRLINK != 0) try writer.writeAll(" INCRLINK");
                if (hdr.flags & macho.MH_DYLDLINK != 0) try writer.writeAll(" DYLDLINK");
                if (hdr.flags & macho.MH_BINDATLOAD != 0) try writer.writeAll(" BINDATLOAD");
                if (hdr.flags & macho.MH_PREBOUND != 0) try writer.writeAll(" PREBOUND");
                if (hdr.flags & macho.MH_SPLIT_SEGS != 0) try writer.writeAll(" SPLIT_SEGS");
                if (hdr.flags & macho.MH_LAZY_INIT != 0) try writer.writeAll(" LAZY_INIT");
                if (hdr.flags & macho.MH_TWOLEVEL != 0) try writer.writeAll(" TWOLEVEL");
                if (hdr.flags & macho.MH_FORCE_FLAT != 0) try writer.writeAll(" FORCE_FLAT");
                if (hdr.flags & macho.MH_NOMULTIDEFS != 0) try writer.writeAll(" NOMULTIDEFS");
                if (hdr.flags & macho.MH_NOFIXPREBINDING != 0) try writer.writeAll(" NOFIXPREBINDING");
                if (hdr.flags & macho.MH_PREBINDABLE != 0) try writer.writeAll(" PREBINDABLE");
                if (hdr.flags & macho.MH_ALLMODSBOUND != 0) try writer.writeAll(" ALLMODSBOUND");
                if (hdr.flags & macho.MH_SUBSECTIONS_VIA_SYMBOLS != 0) try writer.writeAll(" SUBSECTIONS_VIA_SYMBOLS");
                if (hdr.flags & macho.MH_CANONICAL != 0) try writer.writeAll(" CANONICAL");
                if (hdr.flags & macho.MH_WEAK_DEFINES != 0) try writer.writeAll(" WEAK_DEFINES");
                if (hdr.flags & macho.MH_BINDS_TO_WEAK != 0) try writer.writeAll(" BINDS_TO_WEAK");
                if (hdr.flags & macho.MH_ALLOW_STACK_EXECUTION != 0) try writer.writeAll(" ALLOW_STACK_EXECUTION");
                if (hdr.flags & macho.MH_ROOT_SAFE != 0) try writer.writeAll(" ROOT_SAFE");
                if (hdr.flags & macho.MH_SETUID_SAFE != 0) try writer.writeAll(" SETUID_SAFE");
                if (hdr.flags & macho.MH_NO_REEXPORTED_DYLIBS != 0) try writer.writeAll(" NO_REEXPORTED_DYLIBS");
                if (hdr.flags & macho.MH_PIE != 0) try writer.writeAll(" PIE");
                if (hdr.flags & macho.MH_DEAD_STRIPPABLE_DYLIB != 0) try writer.writeAll(" DEAD_STRIPPABLE_DYLIB");
                if (hdr.flags & macho.MH_HAS_TLV_DESCRIPTORS != 0) try writer.writeAll(" HAS_TLV_DESCRIPTORS");
                if (hdr.flags & macho.MH_NO_HEAP_EXECUTION != 0) try writer.writeAll(" NO_HEAP_EXECUTION");
                if (hdr.flags & macho.MH_APP_EXTENSION_SAFE != 0) try writer.writeAll(" APP_EXTENSION_SAFE");
                if (hdr.flags & macho.MH_NLIST_OUTOFSYNC_WITH_DYLDINFO != 0) try writer.writeAll(" NLIST_OUTOFSYNC_WITH_DYLDINFO");
            }

            try writer.writeByte('\n');
        }

        fn dumpLoadCommand(lc: macho.LoadCommandIterator.LoadCommand, index: usize, writer: anytype) !void {
            // print header first
            try writer.print(
                \\LC {d}
                \\cmd {s}
                \\cmdsize {d}
            , .{ index, @tagName(lc.hdr.cmd), lc.hdr.cmdsize });

            switch (lc.hdr.cmd) {
                .SEGMENT_64 => {
                    const seg = lc.cast(macho.segment_command_64).?;
                    try writer.writeByte('\n');
                    try writer.print(
                        \\segname {s}
                        \\vmaddr {x}
                        \\vmsize {x}
                        \\fileoff {x}
                        \\filesz {x}
                    , .{
                        seg.segName(),
                        seg.vmaddr,
                        seg.vmsize,
                        seg.fileoff,
                        seg.filesize,
                    });

                    for (lc.getSections()) |sect| {
                        try writer.writeByte('\n');
                        try writer.print(
                            \\sectname {s}
                            \\addr {x}
                            \\size {x}
                            \\offset {x}
                            \\align {x}
                        , .{
                            sect.sectName(),
                            sect.addr,
                            sect.size,
                            sect.offset,
                            sect.@"align",
                        });
                    }
                },

                .ID_DYLIB,
                .LOAD_DYLIB,
                .LOAD_WEAK_DYLIB,
                .REEXPORT_DYLIB,
                => {
                    const dylib = lc.cast(macho.dylib_command).?;
                    try writer.writeByte('\n');
                    try writer.print(
                        \\name {s}
                        \\timestamp {d}
                        \\current version {x}
                        \\compatibility version {x}
                    , .{
                        lc.getDylibPathName(),
                        dylib.dylib.timestamp,
                        dylib.dylib.current_version,
                        dylib.dylib.compatibility_version,
                    });
                },

                .MAIN => {
                    const main = lc.cast(macho.entry_point_command).?;
                    try writer.writeByte('\n');
                    try writer.print(
                        \\entryoff {x}
                        \\stacksize {x}
                    , .{ main.entryoff, main.stacksize });
                },

                .RPATH => {
                    try writer.writeByte('\n');
                    try writer.print(
                        \\path {s}
                    , .{
                        lc.getRpathPathName(),
                    });
                },

                .UUID => {
                    const uuid = lc.cast(macho.uuid_command).?;
                    try writer.writeByte('\n');
                    try writer.print("uuid {x}", .{&uuid.uuid});
                },

                .DATA_IN_CODE,
                .FUNCTION_STARTS,
                .CODE_SIGNATURE,
                => {
                    const llc = lc.cast(macho.linkedit_data_command).?;
                    try writer.writeByte('\n');
                    try writer.print(
                        \\dataoff {x}
                        \\datasize {x}
                    , .{ llc.dataoff, llc.datasize });
                },

                .DYLD_INFO_ONLY => {
                    const dlc = lc.cast(macho.dyld_info_command).?;
                    try writer.writeByte('\n');
                    try writer.print(
                        \\rebaseoff {x}
                        \\rebasesize {x}
                        \\bindoff {x}
                        \\bindsize {x}
                        \\weakbindoff {x}
                        \\weakbindsize {x}
                        \\lazybindoff {x}
                        \\lazybindsize {x}
                        \\exportoff {x}
                        \\exportsize {x}
                    , .{
                        dlc.rebase_off,
                        dlc.rebase_size,
                        dlc.bind_off,
                        dlc.bind_size,
                        dlc.weak_bind_off,
                        dlc.weak_bind_size,
                        dlc.lazy_bind_off,
                        dlc.lazy_bind_size,
                        dlc.export_off,
                        dlc.export_size,
                    });
                },

                .SYMTAB => {
                    const slc = lc.cast(macho.symtab_command).?;
                    try writer.writeByte('\n');
                    try writer.print(
                        \\symoff {x}
                        \\nsyms {x}
                        \\stroff {x}
                        \\strsize {x}
                    , .{
                        slc.symoff,
                        slc.nsyms,
                        slc.stroff,
                        slc.strsize,
                    });
                },

                .DYSYMTAB => {
                    const dlc = lc.cast(macho.dysymtab_command).?;
                    try writer.writeByte('\n');
                    try writer.print(
                        \\ilocalsym {x}
                        \\nlocalsym {x}
                        \\iextdefsym {x}
                        \\nextdefsym {x}
                        \\iundefsym {x}
                        \\nundefsym {x}
                        \\indirectsymoff {x}
                        \\nindirectsyms {x}
                    , .{
                        dlc.ilocalsym,
                        dlc.nlocalsym,
                        dlc.iextdefsym,
                        dlc.nextdefsym,
                        dlc.iundefsym,
                        dlc.nundefsym,
                        dlc.indirectsymoff,
                        dlc.nindirectsyms,
                    });
                },

                .BUILD_VERSION => {
                    const blc = lc.cast(macho.build_version_command).?;
                    try writer.writeByte('\n');
                    try writer.print(
                        \\platform {s}
                        \\minos {d}.{d}.{d}
                        \\sdk {d}.{d}.{d}
                        \\ntools {d}
                    , .{
                        @tagName(blc.platform),
                        blc.minos >> 16,
                        @as(u8, @truncate(blc.minos >> 8)),
                        @as(u8, @truncate(blc.minos)),
                        blc.sdk >> 16,
                        @as(u8, @truncate(blc.sdk >> 8)),
                        @as(u8, @truncate(blc.sdk)),
                        blc.ntools,
                    });
                    for (lc.getBuildVersionTools()) |tool| {
                        try writer.writeByte('\n');
                        switch (tool.tool) {
                            .CLANG, .SWIFT, .LD, .LLD, .ZIG => try writer.print("tool {s}\n", .{@tagName(tool.tool)}),
                            else => |x| try writer.print("tool {d}\n", .{@intFromEnum(x)}),
                        }
                        try writer.print(
                            \\version {d}.{d}.{d}
                        , .{
                            tool.version >> 16,
                            @as(u8, @truncate(tool.version >> 8)),
                            @as(u8, @truncate(tool.version)),
                        });
                    }
                },

                .VERSION_MIN_MACOSX,
                .VERSION_MIN_IPHONEOS,
                .VERSION_MIN_WATCHOS,
                .VERSION_MIN_TVOS,
                => {
                    const vlc = lc.cast(macho.version_min_command).?;
                    try writer.writeByte('\n');
                    try writer.print(
                        \\version {d}.{d}.{d}
                        \\sdk {d}.{d}.{d}
                    , .{
                        vlc.version >> 16,
                        @as(u8, @truncate(vlc.version >> 8)),
                        @as(u8, @truncate(vlc.version)),
                        vlc.sdk >> 16,
                        @as(u8, @truncate(vlc.sdk >> 8)),
                        @as(u8, @truncate(vlc.sdk)),
                    });
                },

                else => {},
            }
        }

        fn dumpSymtab(ctx: ObjectContext, writer: anytype) !void {
            try writer.writeAll(symtab_label ++ "\n");

            for (ctx.symtab.items) |sym| {
                const sym_name = ctx.getString(sym.n_strx);
                if (sym.n_type.bits.is_stab != 0) {
                    const tt = switch (sym.n_type.stab) {
                        _ => "UNKNOWN STAB",
                        else => @tagName(sym.n_type.stab),
                    };
                    try writer.print("{x}", .{sym.n_value});
                    if (sym.n_sect > 0) {
                        const sect = ctx.sections.items[sym.n_sect - 1];
                        try writer.print(" ({s},{s})", .{ sect.segName(), sect.sectName() });
                    }
                    try writer.print(" {s} (stab) {s}\n", .{ tt, sym_name });
                } else if (sym.n_type.bits.type == .sect) {
                    const sect = ctx.sections.items[sym.n_sect - 1];
                    try writer.print("{x} ({s},{s})", .{
                        sym.n_value,
                        sect.segName(),
                        sect.sectName(),
                    });
                    if (sym.n_desc.referenced_dynamically) try writer.writeAll(" [referenced dynamically]");
                    if (sym.n_desc.weak_def_or_ref_to_weak) try writer.writeAll(" weak");
                    if (sym.n_desc.weak_ref) try writer.writeAll(" weakref");
                    if (sym.n_type.bits.ext) {
                        if (sym.n_type.bits.pext) try writer.writeAll(" private");
                        try writer.writeAll(" external");
                    } else if (sym.n_type.bits.pext) try writer.writeAll(" (was private external)");
                    try writer.print(" {s}\n", .{sym_name});
                } else if (sym.tentative()) {
                    const alignment = (@as(u16, @bitCast(sym.n_desc)) >> 8) & 0x0F;
                    try writer.print("  0x{x:0>16} (common) (alignment 2^{d})", .{ sym.n_value, alignment });
                    if (sym.n_type.bits.ext) try writer.writeAll(" external");
                    try writer.print(" {s}\n", .{sym_name});
                } else if (sym.n_type.bits.type == .undf) {
                    const ordinal = @divFloor(@as(i16, @bitCast(sym.n_desc)), macho.N_SYMBOL_RESOLVER);
                    const import_name = blk: {
                        if (ordinal <= 0) {
                            if (ordinal == macho.BIND_SPECIAL_DYLIB_SELF)
                                break :blk "self import";
                            if (ordinal == macho.BIND_SPECIAL_DYLIB_MAIN_EXECUTABLE)
                                break :blk "main executable";
                            if (ordinal == macho.BIND_SPECIAL_DYLIB_FLAT_LOOKUP)
                                break :blk "flat lookup";
                            unreachable;
                        }
                        const full_path = ctx.imports.items[@as(u16, @bitCast(ordinal)) - 1];
                        const basename = fs.path.basename(full_path);
                        assert(basename.len > 0);
                        const ext = mem.lastIndexOfScalar(u8, basename, '.') orelse basename.len;
                        break :blk basename[0..ext];
                    };
                    try writer.writeAll("(undefined)");
                    if (sym.n_desc.weak_ref) try writer.writeAll(" weakref");
                    if (sym.n_type.bits.ext) try writer.writeAll(" external");
                    try writer.print(" {s} (from {s})\n", .{
                        sym_name,
                        import_name,
                    });
                }
            }
        }

        fn dumpIndirectSymtab(ctx: ObjectContext, writer: anytype) !void {
            try writer.writeAll(indirect_symtab_label ++ "\n");

            var sects_buffer: [3]macho.section_64 = undefined;
            const sects = blk: {
                var count: usize = 0;
                if (ctx.getSectionByName("__TEXT", "__stubs")) |sect| {
                    sects_buffer[count] = sect;
                    count += 1;
                }
                if (ctx.getSectionByName("__DATA_CONST", "__got")) |sect| {
                    sects_buffer[count] = sect;
                    count += 1;
                }
                if (ctx.getSectionByName("__DATA", "__la_symbol_ptr")) |sect| {
                    sects_buffer[count] = sect;
                    count += 1;
                }
                break :blk sects_buffer[0..count];
            };

            const sortFn = struct {
                fn sortFn(c: void, lhs: macho.section_64, rhs: macho.section_64) bool {
                    _ = c;
                    return lhs.reserved1 < rhs.reserved1;
                }
            }.sortFn;
            mem.sort(macho.section_64, sects, {}, sortFn);

            var i: usize = 0;
            while (i < sects.len) : (i += 1) {
                const sect = sects[i];
                const start = sect.reserved1;
                const end = if (i + 1 >= sects.len) ctx.indsymtab.items.len else sects[i + 1].reserved1;
                const entry_size = blk: {
                    if (mem.eql(u8, sect.sectName(), "__stubs")) break :blk sect.reserved2;
                    break :blk @sizeOf(u64);
                };

                try writer.print("{s},{s}\n", .{ sect.segName(), sect.sectName() });
                try writer.print("nentries {d}\n", .{end - start});
                for (ctx.indsymtab.items[start..end], 0..) |index, j| {
                    const sym = ctx.symtab.items[index];
                    const addr = sect.addr + entry_size * j;
                    try writer.print("0x{x} {d} {s}\n", .{ addr, index, ctx.getString(sym.n_strx) });
                }
            }
        }

        fn dumpRebaseInfo(ctx: ObjectContext, data: []const u8, writer: anytype) !void {
            var rebases = std.array_list.Managed(u64).init(ctx.gpa);
            defer rebases.deinit();
            try ctx.parseRebaseInfo(data, &rebases);
            mem.sort(u64, rebases.items, {}, std.sort.asc(u64));
            for (rebases.items) |addr| {
                try writer.print("0x{x}\n", .{addr});
            }
        }

        fn parseRebaseInfo(ctx: ObjectContext, data: []const u8, rebases: *std.array_list.Managed(u64)) !void {
            var reader: std.Io.Reader = .fixed(data);

            var seg_id: ?u8 = null;
            var offset: u64 = 0;
            while (true) {
                const byte = reader.takeByte() catch break;
                const opc = byte & macho.REBASE_OPCODE_MASK;
                const imm = byte & macho.REBASE_IMMEDIATE_MASK;
                switch (opc) {
                    macho.REBASE_OPCODE_DONE => break,
                    macho.REBASE_OPCODE_SET_TYPE_IMM => {},
                    macho.REBASE_OPCODE_SET_SEGMENT_AND_OFFSET_ULEB => {
                        seg_id = imm;
                        offset = try reader.takeLeb128(u64);
                    },
                    macho.REBASE_OPCODE_ADD_ADDR_IMM_SCALED => {
                        offset += imm * @sizeOf(u64);
                    },
                    macho.REBASE_OPCODE_ADD_ADDR_ULEB => {
                        const addend = try reader.takeLeb128(u64);
                        offset += addend;
                    },
                    macho.REBASE_OPCODE_DO_REBASE_ADD_ADDR_ULEB => {
                        const addend = try reader.takeLeb128(u64);
                        const seg = ctx.segments.items[seg_id.?];
                        const addr = seg.vmaddr + offset;
                        try rebases.append(addr);
                        offset += addend + @sizeOf(u64);
                    },
                    macho.REBASE_OPCODE_DO_REBASE_IMM_TIMES,
                    macho.REBASE_OPCODE_DO_REBASE_ULEB_TIMES,
                    macho.REBASE_OPCODE_DO_REBASE_ULEB_TIMES_SKIPPING_ULEB,
                    => {
                        var ntimes: u64 = 1;
                        var skip: u64 = 0;
                        switch (opc) {
                            macho.REBASE_OPCODE_DO_REBASE_IMM_TIMES => {
                                ntimes = imm;
                            },
                            macho.REBASE_OPCODE_DO_REBASE_ULEB_TIMES => {
                                ntimes = try reader.takeLeb128(u64);
                            },
                            macho.REBASE_OPCODE_DO_REBASE_ULEB_TIMES_SKIPPING_ULEB => {
                                ntimes = try reader.takeLeb128(u64);
                                skip = try reader.takeLeb128(u64);
                            },
                            else => unreachable,
                        }
                        const seg = ctx.segments.items[seg_id.?];
                        const base_addr = seg.vmaddr;
                        var count: usize = 0;
                        while (count < ntimes) : (count += 1) {
                            const addr = base_addr + offset;
                            try rebases.append(addr);
                            offset += skip + @sizeOf(u64);
                        }
                    },
                    else => break,
                }
            }
        }

        const Binding = struct {
            address: u64,
            addend: i64,
            ordinal: u16,
            tag: Tag,
            name: []const u8,

            fn deinit(binding: *Binding, gpa: Allocator) void {
                gpa.free(binding.name);
            }

            fn lessThan(ctx: void, lhs: Binding, rhs: Binding) bool {
                _ = ctx;
                return lhs.address < rhs.address;
            }

            const Tag = enum {
                ord,
                self,
                exe,
                flat,
            };
        };

        fn dumpBindInfo(ctx: ObjectContext, data: []const u8, writer: anytype) !void {
            var bindings = std.array_list.Managed(Binding).init(ctx.gpa);
            defer {
                for (bindings.items) |*b| {
                    b.deinit(ctx.gpa);
                }
                bindings.deinit();
            }
            var data_reader: std.Io.Reader = .fixed(data);
            try ctx.parseBindInfo(&data_reader, &bindings);
            mem.sort(Binding, bindings.items, {}, Binding.lessThan);
            for (bindings.items) |binding| {
                try writer.print("0x{x} [addend: {d}]", .{ binding.address, binding.addend });
                try writer.writeAll(" (");
                switch (binding.tag) {
                    .self => try writer.writeAll("self"),
                    .exe => try writer.writeAll("main executable"),
                    .flat => try writer.writeAll("flat lookup"),
                    .ord => try writer.writeAll(std.fs.path.basename(ctx.imports.items[binding.ordinal - 1])),
                }
                try writer.print(") {s}\n", .{binding.name});
            }
        }

        fn parseBindInfo(ctx: ObjectContext, reader: *std.Io.Reader, bindings: *std.array_list.Managed(Binding)) !void {
            var seg_id: ?u8 = null;
            var tag: Binding.Tag = .self;
            var ordinal: u16 = 0;
            var offset: u64 = 0;
            var addend: i64 = 0;

            var name_buf = std.array_list.Managed(u8).init(ctx.gpa);
            defer name_buf.deinit();

            while (true) {
                const byte = reader.takeByte() catch break;
                const opc = byte & macho.BIND_OPCODE_MASK;
                const imm = byte & macho.BIND_IMMEDIATE_MASK;
                switch (opc) {
                    macho.BIND_OPCODE_DONE,
                    macho.BIND_OPCODE_SET_TYPE_IMM,
                    => {},
                    macho.BIND_OPCODE_SET_DYLIB_ORDINAL_IMM => {
                        tag = .ord;
                        ordinal = imm;
                    },
                    macho.BIND_OPCODE_SET_DYLIB_SPECIAL_IMM => {
                        switch (imm) {
                            0 => tag = .self,
                            0xf => tag = .exe,
                            0xe => tag = .flat,
                            else => unreachable,
                        }
                    },
                    macho.BIND_OPCODE_SET_SEGMENT_AND_OFFSET_ULEB => {
                        seg_id = imm;
                        offset = try reader.takeLeb128(u64);
                    },
                    macho.BIND_OPCODE_SET_SYMBOL_TRAILING_FLAGS_IMM => {
                        name_buf.clearRetainingCapacity();
                        try name_buf.appendSlice(try reader.takeDelimiterInclusive(0));
                    },
                    macho.BIND_OPCODE_SET_ADDEND_SLEB => {
                        addend = try reader.takeLeb128(i64);
                    },
                    macho.BIND_OPCODE_ADD_ADDR_ULEB => {
                        const x = try reader.takeLeb128(u64);
                        offset = @intCast(@as(i64, @intCast(offset)) + @as(i64, @bitCast(x)));
                    },
                    macho.BIND_OPCODE_DO_BIND,
                    macho.BIND_OPCODE_DO_BIND_ADD_ADDR_ULEB,
                    macho.BIND_OPCODE_DO_BIND_ADD_ADDR_IMM_SCALED,
                    macho.BIND_OPCODE_DO_BIND_ULEB_TIMES_SKIPPING_ULEB,
                    => {
                        var add_addr: u64 = 0;
                        var count: u64 = 1;
                        var skip: u64 = 0;

                        switch (opc) {
                            macho.BIND_OPCODE_DO_BIND => {},
                            macho.BIND_OPCODE_DO_BIND_ADD_ADDR_ULEB => {
                                add_addr = try reader.takeLeb128(u64);
                            },
                            macho.BIND_OPCODE_DO_BIND_ADD_ADDR_IMM_SCALED => {
                                add_addr = imm * @sizeOf(u64);
                            },
                            macho.BIND_OPCODE_DO_BIND_ULEB_TIMES_SKIPPING_ULEB => {
                                count = try reader.takeLeb128(u64);
                                skip = try reader.takeLeb128(u64);
                            },
                            else => unreachable,
                        }

                        const seg = ctx.segments.items[seg_id.?];
                        var i: u64 = 0;
                        while (i < count) : (i += 1) {
                            const addr: u64 = @intCast(@as(i64, @intCast(seg.vmaddr + offset)));
                            try bindings.append(.{
                                .address = addr,
                                .addend = addend,
                                .tag = tag,
                                .ordinal = ordinal,
                                .name = try ctx.gpa.dupe(u8, name_buf.items),
                            });
                            offset += skip + @sizeOf(u64) + add_addr;
                        }
                    },
                    else => break,
                }
            }
        }

        fn dumpExportsTrie(ctx: ObjectContext, data: []const u8, writer: anytype) !void {
            const seg = ctx.getSegmentByName("__TEXT") orelse return;

            var arena = std.heap.ArenaAllocator.init(ctx.gpa);
            defer arena.deinit();

            var exports = std.array_list.Managed(Export).init(arena.allocator());
            var it: TrieIterator = .{ .stream = .fixed(data) };
            try parseTrieNode(arena.allocator(), &it, "", &exports);

            mem.sort(Export, exports.items, {}, Export.lessThan);

            for (exports.items) |exp| {
                switch (exp.tag) {
                    .@"export" => {
                        const info = exp.data.@"export";
                        if (info.kind != .regular or info.weak) {
                            try writer.writeByte('[');
                        }
                        switch (info.kind) {
                            .regular => {},
                            .absolute => try writer.writeAll("ABS, "),
                            .tlv => try writer.writeAll("THREAD_LOCAL, "),
                        }
                        if (info.weak) try writer.writeAll("WEAK");
                        if (info.kind != .regular or info.weak) {
                            try writer.writeAll("] ");
                        }
                        try writer.print("{x} ", .{seg.vmaddr + info.vmoffset});
                    },
                    else => {},
                }

                try writer.print("{s}\n", .{exp.name});
            }
        }

        const TrieIterator = struct {
            stream: std.Io.Reader,

            fn takeLeb128(it: *TrieIterator) !u64 {
                return it.stream.takeLeb128(u64);
            }

            fn readString(it: *TrieIterator) ![:0]const u8 {
                return it.stream.takeSentinel(0);
            }

            fn takeByte(it: *TrieIterator) !u8 {
                return it.stream.takeByte();
            }
        };

        const Export = struct {
            name: []const u8,
            tag: enum { @"export", reexport, stub_resolver },
            data: union {
                @"export": struct {
                    kind: enum { regular, absolute, tlv },
                    weak: bool = false,
                    vmoffset: u64,
                },
                reexport: u64,
                stub_resolver: struct {
                    stub_offset: u64,
                    resolver_offset: u64,
                },
            },

            inline fn rankByTag(@"export": Export) u3 {
                return switch (@"export".tag) {
                    .@"export" => 1,
                    .reexport => 2,
                    .stub_resolver => 3,
                };
            }

            fn lessThan(ctx: void, lhs: Export, rhs: Export) bool {
                _ = ctx;
                if (lhs.rankByTag() == rhs.rankByTag()) {
                    return switch (lhs.tag) {
                        .@"export" => lhs.data.@"export".vmoffset < rhs.data.@"export".vmoffset,
                        .reexport => lhs.data.reexport < rhs.data.reexport,
                        .stub_resolver => lhs.data.stub_resolver.stub_offset < rhs.data.stub_resolver.stub_offset,
                    };
                }
                return lhs.rankByTag() < rhs.rankByTag();
            }
        };

        fn parseTrieNode(
            arena: Allocator,
            it: *TrieIterator,
            prefix: []const u8,
            exports: *std.array_list.Managed(Export),
        ) !void {
            const size = try it.takeLeb128();
            if (size > 0) {
                const flags = try it.takeLeb128();
                switch (flags) {
                    macho.EXPORT_SYMBOL_FLAGS_REEXPORT => {
                        const ord = try it.takeLeb128();
                        const name = try arena.dupe(u8, try it.readString());
                        try exports.append(.{
                            .name = if (name.len > 0) name else prefix,
                            .tag = .reexport,
                            .data = .{ .reexport = ord },
                        });
                    },
                    macho.EXPORT_SYMBOL_FLAGS_STUB_AND_RESOLVER => {
                        const stub_offset = try it.takeLeb128();
                        const resolver_offset = try it.takeLeb128();
                        try exports.append(.{
                            .name = prefix,
                            .tag = .stub_resolver,
                            .data = .{ .stub_resolver = .{
                                .stub_offset = stub_offset,
                                .resolver_offset = resolver_offset,
                            } },
                        });
                    },
                    else => {
                        const vmoff = try it.takeLeb128();
                        try exports.append(.{
                            .name = prefix,
                            .tag = .@"export",
                            .data = .{ .@"export" = .{
                                .kind = switch (flags & macho.EXPORT_SYMBOL_FLAGS_KIND_MASK) {
                                    macho.EXPORT_SYMBOL_FLAGS_KIND_REGULAR => .regular,
                                    macho.EXPORT_SYMBOL_FLAGS_KIND_ABSOLUTE => .absolute,
                                    macho.EXPORT_SYMBOL_FLAGS_KIND_THREAD_LOCAL => .tlv,
                                    else => unreachable,
                                },
                                .weak = flags & macho.EXPORT_SYMBOL_FLAGS_WEAK_DEFINITION != 0,
                                .vmoffset = vmoff,
                            } },
                        });
                    },
                }
            }

            const nedges = try it.takeByte();
            for (0..nedges) |_| {
                const label = try it.readString();
                const off = try it.takeLeb128();
                const prefix_label = try std.fmt.allocPrint(arena, "{s}{s}", .{ prefix, label });
                const curr = it.stream.seek;
                it.stream.seek = off;
                try parseTrieNode(arena, it, prefix_label, exports);
                it.stream.seek = curr;
            }
        }

        fn dumpSection(ctx: ObjectContext, sect: macho.section_64, writer: anytype) !void {
            const data = ctx.data[sect.offset..][0..sect.size];
            try writer.print("{s}", .{data});
        }
    };

    fn parseAndDumpObject(step: *Step, check: Check, bytes: []const u8) ![]const u8 {
        const gpa = step.owner.allocator;
        const hdr = @as(*align(1) const macho.mach_header_64, @ptrCast(bytes.ptr)).*;
        if (hdr.magic != macho.MH_MAGIC_64) {
            return error.InvalidMagicNumber;
        }

        var ctx = ObjectContext{ .gpa = gpa, .data = bytes, .header = hdr };
        try ctx.parse();

        var output: std.Io.Writer.Allocating = .init(gpa);
        defer output.deinit();
        const writer = &output.writer;

        switch (check.kind) {
            .headers => {
                try ObjectContext.dumpHeader(ctx.header, writer);

                var it = try ctx.getLoadCommandIterator();
                var i: usize = 0;
                while (try it.next()) |cmd| {
                    try ObjectContext.dumpLoadCommand(cmd, i, writer);
                    try writer.writeByte('\n');

                    i += 1;
                }
            },

            .symtab => if (ctx.symtab.items.len > 0) {
                try ctx.dumpSymtab(writer);
            } else return step.fail("no symbol table found", .{}),

            .indirect_symtab => if (ctx.symtab.items.len > 0 and ctx.indsymtab.items.len > 0) {
                try ctx.dumpIndirectSymtab(writer);
            } else return step.fail("no indirect symbol table found", .{}),

            .dyld_rebase,
            .dyld_bind,
            .dyld_weak_bind,
            .dyld_lazy_bind,
            => {
                const cmd = try ctx.getLoadCommand(.DYLD_INFO_ONLY) orelse
                    return step.fail("no dyld info found", .{});
                const lc = cmd.cast(macho.dyld_info_command).?;

                switch (check.kind) {
                    .dyld_rebase => if (lc.rebase_size > 0) {
                        const data = ctx.data[lc.rebase_off..][0..lc.rebase_size];
                        try writer.writeAll(dyld_rebase_label ++ "\n");
                        try ctx.dumpRebaseInfo(data, writer);
                    } else return step.fail("no rebase data found", .{}),

                    .dyld_bind => if (lc.bind_size > 0) {
                        const data = ctx.data[lc.bind_off..][0..lc.bind_size];
                        try writer.writeAll(dyld_bind_label ++ "\n");
                        try ctx.dumpBindInfo(data, writer);
                    } else return step.fail("no bind data found", .{}),

                    .dyld_weak_bind => if (lc.weak_bind_size > 0) {
                        const data = ctx.data[lc.weak_bind_off..][0..lc.weak_bind_size];
                        try writer.writeAll(dyld_weak_bind_label ++ "\n");
                        try ctx.dumpBindInfo(data, writer);
                    } else return step.fail("no weak bind data found", .{}),

                    .dyld_lazy_bind => if (lc.lazy_bind_size > 0) {
                        const data = ctx.data[lc.lazy_bind_off..][0..lc.lazy_bind_size];
                        try writer.writeAll(dyld_lazy_bind_label ++ "\n");
                        try ctx.dumpBindInfo(data, writer);
                    } else return step.fail("no lazy bind data found", .{}),

                    else => unreachable,
                }
            },

            .exports => blk: {
                if (try ctx.getLoadCommand(.DYLD_INFO_ONLY)) |cmd| {
                    const lc = cmd.cast(macho.dyld_info_command).?;
                    if (lc.export_size > 0) {
                        const data = ctx.data[lc.export_off..][0..lc.export_size];
                        try writer.writeAll(exports_label ++ "\n");
                        try ctx.dumpExportsTrie(data, writer);
                        break :blk;
                    }
                }
                return step.fail("no exports data found", .{});
            },

            .dump_section => {
                const name = mem.sliceTo(@as([*:0]const u8, @ptrCast(check.data.items.ptr + check.payload.dump_section)), 0);
                const sep_index = mem.findScalar(u8, name, ',') orelse
                    return step.fail("invalid section name: {s}", .{name});
                const segname = name[0..sep_index];
                const sectname = name[sep_index + 1 ..];
                const sect = ctx.getSectionByName(segname, sectname) orelse
                    return step.fail("section '{s}' not found", .{name});
                try ctx.dumpSection(sect, writer);
            },

            else => return step.fail("invalid check kind for MachO file format: {s}", .{@tagName(check.kind)}),
        }

        return output.toOwnedSlice();
    }
};

const ElfDumper = struct {
    const symtab_label = "symbol table";
    const dynamic_symtab_label = "dynamic symbol table";
    const dynamic_section_label = "dynamic section";
    const archive_symtab_label = "archive symbol table";

    fn parseAndDump(step: *Step, check: Check, bytes: []const u8) ![]const u8 {
        return parseAndDumpArchive(step, check, bytes) catch |err| switch (err) {
            error.InvalidArchiveMagicNumber => try parseAndDumpObject(step, check, bytes),
            else => |e| return e,
        };
    }

    fn parseAndDumpArchive(step: *Step, check: Check, bytes: []const u8) ![]const u8 {
        const gpa = step.owner.allocator;
        var reader: std.Io.Reader = .fixed(bytes);

        const magic = try reader.takeArray(elf.ARMAG.len);
        if (!mem.eql(u8, magic, elf.ARMAG)) {
            return error.InvalidArchiveMagicNumber;
        }

        if (!mem.isAligned(bytes.len, 2)) {
            return error.InvalidArchivePadding;
        }

        var ctx = ArchiveContext{
            .gpa = gpa,
            .data = bytes,
            .strtab = &[0]u8{},
        };
        defer {
            for (ctx.objects.items) |*object| {
                gpa.free(object.name);
            }
            ctx.objects.deinit(gpa);
        }

        while (true) {
            if (!mem.isAligned(reader.seek, 2)) reader.seek += 1;
            if (reader.seek >= ctx.data.len) break;

            const hdr = try reader.takeStruct(elf.ar_hdr, .little);

            if (!mem.eql(u8, &hdr.ar_fmag, elf.ARFMAG)) return error.InvalidArchiveHeaderMagicNumber;

            const size = try hdr.size();
            defer reader.seek += size;

            if (hdr.isSymtab()) {
                try ctx.parseSymtab(ctx.data[reader.seek..][0..size], .p32);
                continue;
            }
            if (hdr.isSymtab64()) {
                try ctx.parseSymtab(ctx.data[reader.seek..][0..size], .p64);
                continue;
            }
            if (hdr.isStrtab()) {
                ctx.strtab = ctx.data[reader.seek..][0..size];
                continue;
            }
            if (hdr.isSymdef() or hdr.isSymdefSorted()) continue;

            const name = if (hdr.name()) |name|
                try gpa.dupe(u8, name)
            else if (try hdr.nameOffset()) |off|
                try gpa.dupe(u8, ctx.getString(off))
            else
                unreachable;

            try ctx.objects.append(gpa, .{ .name = name, .off = reader.seek, .len = size });
        }

        var output: std.Io.Writer.Allocating = .init(gpa);
        defer output.deinit();
        const writer = &output.writer;

        switch (check.kind) {
            .archive_symtab => if (ctx.symtab.items.len > 0) {
                try ctx.dumpSymtab(writer);
            } else return step.fail("no archive symbol table found", .{}),

            else => if (ctx.objects.items.len > 0) {
                try ctx.dumpObjects(step, check, writer);
            } else return step.fail("empty archive", .{}),
        }

        return output.toOwnedSlice();
    }

    const ArchiveContext = struct {
        gpa: Allocator,
        data: []const u8,
        symtab: std.ArrayList(ArSymtabEntry) = .empty,
        strtab: []const u8,
        objects: std.ArrayList(struct { name: []const u8, off: usize, len: usize }) = .empty,

        fn parseSymtab(ctx: *ArchiveContext, raw: []const u8, ptr_width: enum { p32, p64 }) !void {
            var reader: std.Io.Reader = .fixed(raw);
            const num = switch (ptr_width) {
                .p32 => try reader.takeInt(u32, .big),
                .p64 => try reader.takeInt(u64, .big),
            };
            const ptr_size: usize = switch (ptr_width) {
                .p32 => @sizeOf(u32),
                .p64 => @sizeOf(u64),
            };
            const strtab_off = (num + 1) * ptr_size;
            const strtab_len = raw.len - strtab_off;
            const strtab = raw[strtab_off..][0..strtab_len];

            try ctx.symtab.ensureTotalCapacityPrecise(ctx.gpa, num);

            var stroff: usize = 0;
            for (0..num) |_| {
                const off = switch (ptr_width) {
                    .p32 => try reader.takeInt(u32, .big),
                    .p64 => try reader.takeInt(u64, .big),
                };
                const name = mem.sliceTo(@as([*:0]const u8, @ptrCast(strtab.ptr + stroff)), 0);
                stroff += name.len + 1;
                ctx.symtab.appendAssumeCapacity(.{ .off = off, .name = name });
            }
        }

        fn dumpSymtab(ctx: ArchiveContext, writer: anytype) !void {
            var files = std.AutoHashMap(usize, []const u8).init(ctx.gpa);
            defer files.deinit();
            try files.ensureUnusedCapacity(@intCast(ctx.objects.items.len));

            for (ctx.objects.items) |object| {
                files.putAssumeCapacityNoClobber(object.off - @sizeOf(elf.ar_hdr), object.name);
            }

            var symbols: std.array_hash_map.Auto(usize, std.array_list.Managed([]const u8)) = .empty;
            defer {
                for (symbols.values()) |*value| {
                    value.deinit();
                }
                symbols.deinit(ctx.gpa);
            }

            for (ctx.symtab.items) |entry| {
                const gop = try symbols.getOrPut(ctx.gpa, @intCast(entry.off));
                if (!gop.found_existing) {
                    gop.value_ptr.* = std.array_list.Managed([]const u8).init(ctx.gpa);
                }
                try gop.value_ptr.append(entry.name);
            }

            try writer.print("{s}\n", .{archive_symtab_label});
            for (symbols.keys(), symbols.values()) |off, values| {
                try writer.print("in object {s}\n", .{files.get(off).?});
                for (values.items) |value| {
                    try writer.print("{s}\n", .{value});
                }
            }
        }

        fn dumpObjects(ctx: ArchiveContext, step: *Step, check: Check, writer: anytype) !void {
            for (ctx.objects.items) |object| {
                try writer.print("object {s}\n", .{object.name});
                const output = try parseAndDumpObject(step, check, ctx.data[object.off..][0..object.len]);
                defer ctx.gpa.free(output);
                try writer.print("{s}\n", .{output});
            }
        }

        fn getString(ctx: ArchiveContext, off: u32) []const u8 {
            assert(off < ctx.strtab.len);
            const name = mem.sliceTo(@as([*:'\n']const u8, @ptrCast(ctx.strtab.ptr + off)), 0);
            return name[0 .. name.len - 1];
        }

        const ArSymtabEntry = struct {
            name: [:0]const u8,
            off: u64,
        };
    };

    fn parseAndDumpObject(step: *Step, check: Check, bytes: []const u8) ![]const u8 {
        const gpa = step.owner.allocator;

        // `std.elf.Header` takes care of endianness issues for us.
        var reader: std.Io.Reader = .fixed(bytes);
        const hdr = try elf.Header.read(&reader);

        var shdrs = try gpa.alloc(elf.Elf64_Shdr, hdr.shnum);
        defer gpa.free(shdrs);
        {
            var shdr_it = hdr.iterateSectionHeadersBuffer(bytes);
            var shdr_i: usize = 0;
            while (try shdr_it.next()) |shdr| : (shdr_i += 1) shdrs[shdr_i] = shdr;
        }

        var phdrs = try gpa.alloc(elf.Elf64_Phdr, hdr.shnum);
        defer gpa.free(phdrs);
        {
            var phdr_it = hdr.iterateProgramHeadersBuffer(bytes);
            var phdr_i: usize = 0;
            while (try phdr_it.next()) |phdr| : (phdr_i += 1) phdrs[phdr_i] = phdr;
        }

        var ctx = ObjectContext{
            .gpa = gpa,
            .data = bytes,
            .hdr = hdr,
            .shdrs = shdrs,
            .phdrs = phdrs,
            .shstrtab = undefined,
        };
        ctx.shstrtab = ctx.getSectionContents(ctx.hdr.shstrndx);

        defer gpa.free(ctx.symtab.symbols);
        defer gpa.free(ctx.dysymtab.symbols);
        defer gpa.free(ctx.dyns);

        for (ctx.shdrs, 0..) |shdr, i| switch (shdr.sh_type) {
            elf.SHT_SYMTAB, elf.SHT_DYNSYM => {
                const raw = ctx.getSectionContents(i);
                const nsyms = @divExact(raw.len, @sizeOf(elf.Elf64_Sym));
                const symbols = try gpa.alloc(elf.Elf64_Sym, nsyms);

                var r: std.Io.Reader = .fixed(raw);
                for (0..nsyms) |si| symbols[si] = r.takeStruct(elf.Elf64_Sym, ctx.hdr.endian) catch unreachable;

                const strings = ctx.getSectionContents(shdr.sh_link);

                switch (shdr.sh_type) {
                    elf.SHT_SYMTAB => {
                        ctx.symtab = .{
                            .symbols = symbols,
                            .strings = strings,
                        };
                    },
                    elf.SHT_DYNSYM => {
                        ctx.dysymtab = .{
                            .symbols = symbols,
                            .strings = strings,
                        };
                    },
                    else => unreachable,
                }
            },
            elf.SHT_DYNAMIC => {
                const raw = ctx.getSectionContents(i);
                const ndyns = @divExact(raw.len, @sizeOf(elf.Elf64_Dyn));
                const dyns = try gpa.alloc(elf.Elf64_Dyn, ndyns);

                var r: std.Io.Reader = .fixed(raw);
                for (0..ndyns) |si| dyns[si] = r.takeStruct(elf.Elf64_Dyn, ctx.hdr.endian) catch unreachable;

                ctx.dyns = dyns;
                ctx.dyns_strings = ctx.getSectionContents(shdr.sh_link);
            },

            else => {},
        };

        var output: std.Io.Writer.Allocating = .init(gpa);
        defer output.deinit();
        const writer = &output.writer;

        switch (check.kind) {
            .headers => {
                try ctx.dumpHeader(writer);
                try ctx.dumpShdrs(writer);
                try ctx.dumpPhdrs(writer);
            },

            .symtab => if (ctx.symtab.symbols.len > 0) {
                try ctx.dumpSymtab(.symtab, writer);
            } else return step.fail("no symbol table found", .{}),

            .dynamic_symtab => if (ctx.dysymtab.symbols.len > 0) {
                try ctx.dumpSymtab(.dysymtab, writer);
            } else return step.fail("no dynamic symbol table found", .{}),

            .dynamic_section => if (ctx.dyns.len > 0) {
                try ctx.dumpDynamicSection(writer);
            } else return step.fail("no dynamic section found", .{}),

            .dump_section => {
                const name = mem.sliceTo(@as([*:0]const u8, @ptrCast(check.data.items.ptr + check.payload.dump_section)), 0);
                const shndx = ctx.getSectionByName(name) orelse return step.fail("no '{s}' section found", .{name});
                try ctx.dumpSection(shndx, writer);
            },

            else => return step.fail("invalid check kind for ELF file format: {s}", .{@tagName(check.kind)}),
        }

        return output.toOwnedSlice();
    }

    const ObjectContext = struct {
        gpa: Allocator,
        data: []const u8,
        hdr: elf.Header,
        shdrs: []const elf.Elf64_Shdr,
        phdrs: []const elf.Elf64_Phdr,
        shstrtab: []const u8,
        symtab: Symtab = .{},
        dysymtab: Symtab = .{},
        dyns: []const elf.Elf64_Dyn = &.{},
        dyns_strings: []const u8 = &.{},

        fn dumpHeader(ctx: ObjectContext, writer: anytype) !void {
            try writer.writeAll("header\n");
            try writer.print("type {s}\n", .{@tagName(ctx.hdr.type)});
            try writer.print("entry {x}\n", .{ctx.hdr.entry});
        }

        fn dumpPhdrs(ctx: ObjectContext, writer: anytype) !void {
            if (ctx.phdrs.len == 0) return;

            try writer.writeAll("program headers\n");

            for (ctx.phdrs, 0..) |phdr, phndx| {
                try writer.print("phdr {d}\n", .{phndx});
                try writer.print("type {f}\n", .{fmtPhType(phdr.p_type)});
                try writer.print("vaddr {x}\n", .{phdr.p_vaddr});
                try writer.print("paddr {x}\n", .{phdr.p_paddr});
                try writer.print("offset {x}\n", .{phdr.p_offset});
                try writer.print("memsz {x}\n", .{phdr.p_memsz});
                try writer.print("filesz {x}\n", .{phdr.p_filesz});
                try writer.print("align {x}\n", .{phdr.p_align});

                {
                    const flags = phdr.p_flags;
                    try writer.writeAll("flags");
                    if (flags > 0) try writer.writeByte(' ');
                    if (flags & elf.PF_R != 0) {
                        try writer.writeByte('R');
                    }
                    if (flags & elf.PF_W != 0) {
                        try writer.writeByte('W');
                    }
                    if (flags & elf.PF_X != 0) {
                        try writer.writeByte('E');
                    }
                    if (flags & elf.PF_MASKOS != 0) {
                        try writer.writeAll("OS");
                    }
                    if (flags & elf.PF_MASKPROC != 0) {
                        try writer.writeAll("PROC");
                    }
                    try writer.writeByte('\n');
                }
            }
        }

        fn dumpShdrs(ctx: ObjectContext, writer: anytype) !void {
            if (ctx.shdrs.len == 0) return;

            try writer.writeAll("section headers\n");

            for (ctx.shdrs, 0..) |shdr, shndx| {
                try writer.print("shdr {d}\n", .{shndx});
                try writer.print("name {s}\n", .{ctx.getSectionName(shndx)});
                try writer.print("type {f}\n", .{fmtShType(shdr.sh_type)});
                try writer.print("addr {x}\n", .{shdr.sh_addr});
                try writer.print("offset {x}\n", .{shdr.sh_offset});
                try writer.print("size {x}\n", .{shdr.sh_size});
                try writer.print("addralign {x}\n", .{shdr.sh_addralign});
                // TODO dump formatted sh_flags
            }
        }

        fn dumpDynamicSection(ctx: ObjectContext, writer: anytype) !void {
            try writer.writeAll(ElfDumper.dynamic_section_label ++ "\n");

            for (ctx.dyns) |entry| {
                const key = @as(u64, @bitCast(entry.d_tag));
                const value = entry.d_val;

                const key_str = switch (key) {
                    elf.DT_NEEDED => "NEEDED",
                    elf.DT_SONAME => "SONAME",
                    elf.DT_INIT_ARRAY => "INIT_ARRAY",
                    elf.DT_INIT_ARRAYSZ => "INIT_ARRAYSZ",
                    elf.DT_FINI_ARRAY => "FINI_ARRAY",
                    elf.DT_FINI_ARRAYSZ => "FINI_ARRAYSZ",
                    elf.DT_HASH => "HASH",
                    elf.DT_GNU_HASH => "GNU_HASH",
                    elf.DT_STRTAB => "STRTAB",
                    elf.DT_SYMTAB => "SYMTAB",
                    elf.DT_STRSZ => "STRSZ",
                    elf.DT_SYMENT => "SYMENT",
                    elf.DT_PLTGOT => "PLTGOT",
                    elf.DT_PLTRELSZ => "PLTRELSZ",
                    elf.DT_PLTREL => "PLTREL",
                    elf.DT_JMPREL => "JMPREL",
                    elf.DT_RELA => "RELA",
                    elf.DT_RELASZ => "RELASZ",
                    elf.DT_RELAENT => "RELAENT",
                    elf.DT_VERDEF => "VERDEF",
                    elf.DT_VERDEFNUM => "VERDEFNUM",
                    elf.DT_FLAGS => "FLAGS",
                    elf.DT_FLAGS_1 => "FLAGS_1",
                    elf.DT_VERNEED => "VERNEED",
                    elf.DT_VERNEEDNUM => "VERNEEDNUM",
                    elf.DT_VERSYM => "VERSYM",
                    elf.DT_RELACOUNT => "RELACOUNT",
                    elf.DT_RPATH => "RPATH",
                    elf.DT_RUNPATH => "RUNPATH",
                    elf.DT_INIT => "INIT",
                    elf.DT_FINI => "FINI",
                    elf.DT_NULL => "NULL",
                    else => "UNKNOWN",
                };
                try writer.print("{s}", .{key_str});

                switch (key) {
                    elf.DT_NEEDED,
                    elf.DT_SONAME,
                    elf.DT_RPATH,
                    elf.DT_RUNPATH,
                    => {
                        const name = getString(ctx.dyns_strings, @intCast(value));
                        try writer.print(" {s}", .{name});
                    },

                    elf.DT_INIT_ARRAY,
                    elf.DT_FINI_ARRAY,
                    elf.DT_HASH,
                    elf.DT_GNU_HASH,
                    elf.DT_STRTAB,
                    elf.DT_SYMTAB,
                    elf.DT_PLTGOT,
                    elf.DT_JMPREL,
                    elf.DT_RELA,
                    elf.DT_VERDEF,
                    elf.DT_VERNEED,
                    elf.DT_VERSYM,
                    elf.DT_INIT,
                    elf.DT_FINI,
                    elf.DT_NULL,
                    => try writer.print(" {x}", .{value}),

                    elf.DT_INIT_ARRAYSZ,
                    elf.DT_FINI_ARRAYSZ,
                    elf.DT_STRSZ,
                    elf.DT_SYMENT,
                    elf.DT_PLTRELSZ,
                    elf.DT_RELASZ,
                    elf.DT_RELAENT,
                    elf.DT_RELACOUNT,
                    => try writer.print(" {d}", .{value}),

                    elf.DT_PLTREL => try writer.writeAll(switch (value) {
                        elf.DT_REL => " REL",
                        elf.DT_RELA => " RELA",
                        else => " UNKNOWN",
                    }),

                    elf.DT_FLAGS => if (value > 0) {
                        if (value & elf.DF_ORIGIN != 0) try writer.writeAll(" ORIGIN");
                        if (value & elf.DF_SYMBOLIC != 0) try writer.writeAll(" SYMBOLIC");
                        if (value & elf.DF_TEXTREL != 0) try writer.writeAll(" TEXTREL");
                        if (value & elf.DF_BIND_NOW != 0) try writer.writeAll(" BIND_NOW");
                        if (value & elf.DF_STATIC_TLS != 0) try writer.writeAll(" STATIC_TLS");
                    },

                    elf.DT_FLAGS_1 => if (value > 0) {
                        if (value & elf.DF_1_NOW != 0) try writer.writeAll(" NOW");
                        if (value & elf.DF_1_GLOBAL != 0) try writer.writeAll(" GLOBAL");
                        if (value & elf.DF_1_GROUP != 0) try writer.writeAll(" GROUP");
                        if (value & elf.DF_1_NODELETE != 0) try writer.writeAll(" NODELETE");
                        if (value & elf.DF_1_LOADFLTR != 0) try writer.writeAll(" LOADFLTR");
                        if (value & elf.DF_1_INITFIRST != 0) try writer.writeAll(" INITFIRST");
                        if (value & elf.DF_1_NOOPEN != 0) try writer.writeAll(" NOOPEN");
                        if (value & elf.DF_1_ORIGIN != 0) try writer.writeAll(" ORIGIN");
                        if (value & elf.DF_1_DIRECT != 0) try writer.writeAll(" DIRECT");
                        if (value & elf.DF_1_TRANS != 0) try writer.writeAll(" TRANS");
                        if (value & elf.DF_1_INTERPOSE != 0) try writer.writeAll(" INTERPOSE");
                        if (value & elf.DF_1_NODEFLIB != 0) try writer.writeAll(" NODEFLIB");
                        if (value & elf.DF_1_NODUMP != 0) try writer.writeAll(" NODUMP");
                        if (value & elf.DF_1_CONFALT != 0) try writer.writeAll(" CONFALT");
                        if (value & elf.DF_1_ENDFILTEE != 0) try writer.writeAll(" ENDFILTEE");
                        if (value & elf.DF_1_DISPRELDNE != 0) try writer.writeAll(" DISPRELDNE");
                        if (value & elf.DF_1_DISPRELPND != 0) try writer.writeAll(" DISPRELPND");
                        if (value & elf.DF_1_NODIRECT != 0) try writer.writeAll(" NODIRECT");
                        if (value & elf.DF_1_IGNMULDEF != 0) try writer.writeAll(" IGNMULDEF");
                        if (value & elf.DF_1_NOKSYMS != 0) try writer.writeAll(" NOKSYMS");
                        if (value & elf.DF_1_NOHDR != 0) try writer.writeAll(" NOHDR");
                        if (value & elf.DF_1_EDITED != 0) try writer.writeAll(" EDITED");
                        if (value & elf.DF_1_NORELOC != 0) try writer.writeAll(" NORELOC");
                        if (value & elf.DF_1_SYMINTPOSE != 0) try writer.writeAll(" SYMINTPOSE");
                        if (value & elf.DF_1_GLOBAUDIT != 0) try writer.writeAll(" GLOBAUDIT");
                        if (value & elf.DF_1_SINGLETON != 0) try writer.writeAll(" SINGLETON");
                        if (value & elf.DF_1_STUB != 0) try writer.writeAll(" STUB");
                        if (value & elf.DF_1_PIE != 0) try writer.writeAll(" PIE");
                    },

                    else => try writer.print(" {x}", .{value}),
                }
                try writer.writeByte('\n');
            }
        }

        fn dumpSymtab(ctx: ObjectContext, comptime @"type": enum { symtab, dysymtab }, writer: anytype) !void {
            const symtab = switch (@"type") {
                .symtab => ctx.symtab,
                .dysymtab => ctx.dysymtab,
            };

            try writer.writeAll(switch (@"type") {
                .symtab => symtab_label,
                .dysymtab => dynamic_symtab_label,
            } ++ "\n");

            for (symtab.symbols, 0..) |sym, index| {
                try writer.print("{x} {x}", .{ sym.st_value, sym.st_size });

                {
                    if (elf.SHN_LORESERVE <= sym.st_shndx and sym.st_shndx < elf.SHN_HIRESERVE) {
                        if (elf.SHN_LOPROC <= sym.st_shndx and sym.st_shndx < elf.SHN_HIPROC) {
                            try writer.print(" LO+{d}", .{sym.st_shndx - elf.SHN_LOPROC});
                        } else {
                            const sym_ndx = switch (sym.st_shndx) {
                                elf.SHN_ABS => "ABS",
                                elf.SHN_COMMON => "COM",
                                elf.SHN_LIVEPATCH => "LIV",
                                else => "UNK",
                            };
                            try writer.print(" {s}", .{sym_ndx});
                        }
                    } else if (sym.st_shndx == elf.SHN_UNDEF) {
                        try writer.writeAll(" UND");
                    } else {
                        try writer.print(" {x}", .{sym.st_shndx});
                    }
                }

                blk: {
                    const tt = sym.st_type();
                    const sym_type = switch (tt) {
                        elf.STT_NOTYPE => "NOTYPE",
                        elf.STT_OBJECT => "OBJECT",
                        elf.STT_FUNC => "FUNC",
                        elf.STT_SECTION => "SECTION",
                        elf.STT_FILE => "FILE",
                        elf.STT_COMMON => "COMMON",
                        elf.STT_TLS => "TLS",
                        elf.STT_NUM => "NUM",
                        elf.STT_GNU_IFUNC => "IFUNC",
                        else => if (elf.STT_LOPROC <= tt and tt < elf.STT_HIPROC) {
                            break :blk try writer.print(" LOPROC+{d}", .{tt - elf.STT_LOPROC});
                        } else if (elf.STT_LOOS <= tt and tt < elf.STT_HIOS) {
                            break :blk try writer.print(" LOOS+{d}", .{tt - elf.STT_LOOS});
                        } else "UNK",
                    };
                    try writer.print(" {s}", .{sym_type});
                }

                blk: {
                    const bind = sym.st_bind();
                    const sym_bind = switch (bind) {
                        elf.STB_LOCAL => "LOCAL",
                        elf.STB_GLOBAL => "GLOBAL",
                        elf.STB_WEAK => "WEAK",
                        elf.STB_NUM => "NUM",
                        else => if (elf.STB_LOPROC <= bind and bind < elf.STB_HIPROC) {
                            break :blk try writer.print(" LOPROC+{d}", .{bind - elf.STB_LOPROC});
                        } else if (elf.STB_LOOS <= bind and bind < elf.STB_HIOS) {
                            break :blk try writer.print(" LOOS+{d}", .{bind - elf.STB_LOOS});
                        } else "UNKNOWN",
                    };
                    try writer.print(" {s}", .{sym_bind});
                }

                const sym_vis = @as(elf.STV, @enumFromInt(@as(u3, @truncate(sym.st_other))));
                try writer.print(" {s}", .{@tagName(sym_vis)});

                const sym_name = switch (sym.st_type()) {
                    elf.STT_SECTION => ctx.getSectionName(sym.st_shndx),
                    else => symtab.getName(index).?,
                };
                try writer.print(" {s}\n", .{sym_name});
            }
        }

        fn dumpSection(ctx: ObjectContext, shndx: usize, writer: anytype) !void {
            const data = ctx.getSectionContents(shndx);
            try writer.print("{s}", .{data});
        }

        inline fn getSectionName(ctx: ObjectContext, shndx: usize) []const u8 {
            const shdr = ctx.shdrs[shndx];
            return getString(ctx.shstrtab, shdr.sh_name);
        }

        fn getSectionContents(ctx: ObjectContext, shndx: usize) []const u8 {
            const shdr = ctx.shdrs[shndx];
            assert(shdr.sh_offset < ctx.data.len);
            assert(shdr.sh_offset + shdr.sh_size <= ctx.data.len);
            return ctx.data[shdr.sh_offset..][0..shdr.sh_size];
        }

        fn getSectionByName(ctx: ObjectContext, name: []const u8) ?usize {
            for (0..ctx.shdrs.len) |shndx| {
                if (mem.eql(u8, ctx.getSectionName(shndx), name)) return shndx;
            } else return null;
        }
    };

    const Symtab = struct {
        symbols: []const elf.Elf64_Sym = &.{},
        strings: []const u8 = &.{},

        fn get(st: Symtab, index: usize) ?elf.Elf64_Sym {
            if (index >= st.symbols.len) return null;
            return st.symbols[index];
        }

        fn getName(st: Symtab, index: usize) ?[]const u8 {
            const sym = st.get(index) orelse return null;
            return getString(st.strings, sym.st_name);
        }
    };

    fn getString(strtab: []const u8, off: u32) []const u8 {
        assert(off < strtab.len);
        return mem.sliceTo(@as([*:0]const u8, @ptrCast(strtab.ptr + off)), 0);
    }

    fn fmtShType(sh_type: u32) std.fmt.Alt(u32, formatShType) {
        return .{ .data = sh_type };
    }

    fn formatShType(sh_type: u32, writer: *Writer) Writer.Error!void {
        const name = switch (sh_type) {
            elf.SHT_NULL => "NULL",
            elf.SHT_PROGBITS => "PROGBITS",
            elf.SHT_SYMTAB => "SYMTAB",
            elf.SHT_STRTAB => "STRTAB",
            elf.SHT_RELA => "RELA",
            elf.SHT_HASH => "HASH",
            elf.SHT_DYNAMIC => "DYNAMIC",
            elf.SHT_NOTE => "NOTE",
            elf.SHT_NOBITS => "NOBITS",
            elf.SHT_REL => "REL",
            elf.SHT_SHLIB => "SHLIB",
            elf.SHT_DYNSYM => "DYNSYM",
            elf.SHT_INIT_ARRAY => "INIT_ARRAY",
            elf.SHT_FINI_ARRAY => "FINI_ARRAY",
            elf.SHT_PREINIT_ARRAY => "PREINIT_ARRAY",
            elf.SHT_GROUP => "GROUP",
            elf.SHT_SYMTAB_SHNDX => "SYMTAB_SHNDX",
            elf.SHT_X86_64_UNWIND => "X86_64_UNWIND",
            elf.SHT_LLVM_ADDRSIG => "LLVM_ADDRSIG",
            elf.SHT_GNU_HASH => "GNU_HASH",
            elf.SHT_GNU_VERDEF => "VERDEF",
            elf.SHT_GNU_VERNEED => "VERNEED",
            elf.SHT_GNU_VERSYM => "VERSYM",
            else => if (elf.SHT_LOOS <= sh_type and sh_type < elf.SHT_HIOS) {
                return try writer.print("LOOS+0x{x}", .{sh_type - elf.SHT_LOOS});
            } else if (elf.SHT_LOPROC <= sh_type and sh_type < elf.SHT_HIPROC) {
                return try writer.print("LOPROC+0x{x}", .{sh_type - elf.SHT_LOPROC});
            } else if (elf.SHT_LOUSER <= sh_type and sh_type < elf.SHT_HIUSER) {
                return try writer.print("LOUSER+0x{x}", .{sh_type - elf.SHT_LOUSER});
            } else "UNKNOWN",
        };
        try writer.writeAll(name);
    }

    fn fmtPhType(ph_type: u32) std.fmt.Alt(u32, formatPhType) {
        return .{ .data = ph_type };
    }

    fn formatPhType(ph_type: u32, writer: *Writer) Writer.Error!void {
        const p_type = switch (ph_type) {
            elf.PT_NULL => "NULL",
            elf.PT_LOAD => "LOAD",
            elf.PT_DYNAMIC => "DYNAMIC",
            elf.PT_INTERP => "INTERP",
            elf.PT_NOTE => "NOTE",
            elf.PT_SHLIB => "SHLIB",
            elf.PT_PHDR => "PHDR",
            elf.PT_TLS => "TLS",
            elf.PT_NUM => "NUM",
            elf.PT_GNU_EH_FRAME => "GNU_EH_FRAME",
            elf.PT_GNU_STACK => "GNU_STACK",
            elf.PT_GNU_RELRO => "GNU_RELRO",
            else => if (elf.PT_LOOS <= ph_type and ph_type < elf.PT_HIOS) {
                return try writer.print("LOOS+0x{x}", .{ph_type - elf.PT_LOOS});
            } else if (elf.PT_LOPROC <= ph_type and ph_type < elf.PT_HIPROC) {
                return try writer.print("LOPROC+0x{x}", .{ph_type - elf.PT_LOPROC});
            } else "UNKNOWN",
        };
        try writer.writeAll(p_type);
    }
};

const WasmDumper = struct {
    const symtab_label = "symbols";

    fn parseAndDump(step: *Step, check: Check, bytes: []const u8) ![]const u8 {
        const gpa = step.owner.allocator;
        var reader: std.Io.Reader = .fixed(bytes);

        const buf = try reader.takeArray(8);
        if (!mem.eql(u8, buf[0..4], &std.wasm.magic)) {
            return error.InvalidMagicByte;
        }
        if (!mem.eql(u8, buf[4..], &std.wasm.version)) {
            return error.UnsupportedWasmVersion;
        }

        var output: std.Io.Writer.Allocating = .init(gpa);
        defer output.deinit();
        parseAndDumpInner(step, check, bytes, &reader, &output.writer) catch |err| switch (err) {
            error.EndOfStream => try output.writer.writeAll("\n<UnexpectedEndOfStream>"),
            else => |e| return e,
        };
        return output.toOwnedSlice();
    }

    fn parseAndDumpInner(
        step: *Step,
        check: Check,
        bytes: []const u8,
        reader: *std.Io.Reader,
        writer: *std.Io.Writer,
    ) !void {
        switch (check.kind) {
            .headers => {
                while (reader.takeByte()) |current_byte| {
                    const section = std.enums.fromInt(std.wasm.Section, current_byte) orelse {
                        return step.fail("Found invalid section id '{d}'", .{current_byte});
                    };

                    const section_length = try reader.takeLeb128(u32);
                    try parseAndDumpSection(step, section, bytes[reader.seek..][0..section_length], writer);
                    reader.seek += section_length;
                } else |_| {} // reached end of stream
            },

            else => return step.fail("invalid check kind for Wasm file format: {s}", .{@tagName(check.kind)}),
        }
    }

    fn parseAndDumpSection(
        step: *Step,
        section: std.wasm.Section,
        data: []const u8,
        writer: *std.Io.Writer,
    ) !void {
        var reader: std.Io.Reader = .fixed(data);

        try writer.print(
            \\Section {s}
            \\size {d}
        , .{ @tagName(section), data.len });

        switch (section) {
            .type,
            .import,
            .function,
            .table,
            .memory,
            .global,
            .@"export",
            .element,
            .code,
            .data,
            => {
                const entries = try reader.takeLeb128(u32);
                try writer.print("\nentries {d}\n", .{entries});
                try parseSection(step, section, data[reader.seek..], entries, writer);
            },
            .custom => {
                const name_length = try reader.takeLeb128(u32);
                const name = data[reader.seek..][0..name_length];
                reader.seek += name_length;
                try writer.print("\nname {s}\n", .{name});

                if (mem.eql(u8, name, "name")) {
                    try parseDumpNames(step, &reader, writer, data);
                } else if (mem.eql(u8, name, "producers")) {
                    try parseDumpProducers(&reader, writer, data);
                } else if (mem.eql(u8, name, "target_features")) {
                    try parseDumpFeatures(&reader, writer, data);
                }
                // TODO: Implement parsing and dumping other custom sections (such as relocations)
            },
            .start => {
                const start = try reader.takeLeb128(u32);
                try writer.print("\nstart {d}\n", .{start});
            },
            .data_count => {
                const count = try reader.takeLeb128(u32);
                try writer.print("\ncount {d}\n", .{count});
            },
            else => {}, // skip unknown sections
        }
    }

    fn parseSection(step: *Step, section: std.wasm.Section, data: []const u8, entries: u32, writer: anytype) !void {
        var reader: std.Io.Reader = .fixed(data);

        switch (section) {
            .type => {
                var i: u32 = 0;
                while (i < entries) : (i += 1) {
                    const func_type = try reader.takeByte();
                    if (func_type != std.wasm.function_type) {
                        return step.fail("expected function type, found byte '{d}'", .{func_type});
                    }
                    const params = try reader.takeLeb128(u32);
                    try writer.print("params {d}\n", .{params});
                    var index: u32 = 0;
                    while (index < params) : (index += 1) {
                        _ = try parseDumpType(step, std.wasm.Valtype, &reader, writer);
                    } else index = 0;
                    const returns = try reader.takeLeb128(u32);
                    try writer.print("returns {d}\n", .{returns});
                    while (index < returns) : (index += 1) {
                        _ = try parseDumpType(step, std.wasm.Valtype, &reader, writer);
                    }
                }
            },
            .import => {
                var i: u32 = 0;
                while (i < entries) : (i += 1) {
                    const module_name_len = try reader.takeLeb128(u32);
                    const module_name = data[reader.seek..][0..module_name_len];
                    reader.seek += module_name_len;
                    const name_len = try reader.takeLeb128(u32);
                    const name = data[reader.seek..][0..name_len];
                    reader.seek += name_len;

                    const kind = std.enums.fromInt(std.wasm.ExternalKind, try reader.takeByte()) orelse {
                        return step.fail("invalid import kind", .{});
                    };

                    try writer.print(
                        \\module {s}
                        \\name {s}
                        \\kind {s}
                    , .{ module_name, name, @tagName(kind) });
                    try writer.writeByte('\n');
                    switch (kind) {
                        .function => {
                            try writer.print("index {d}\n", .{try reader.takeLeb128(u32)});
                        },
                        .memory => {
                            try parseDumpLimits(&reader, writer);
                        },
                        .global => {
                            _ = try parseDumpType(step, std.wasm.Valtype, &reader, writer);
                            try writer.print("mutable {}\n", .{0x01 == try reader.takeLeb128(u32)});
                        },
                        .table => {
                            _ = try parseDumpType(step, std.wasm.RefType, &reader, writer);
                            try parseDumpLimits(&reader, writer);
                        },
                    }
                }
            },
            .function => {
                var i: u32 = 0;
                while (i < entries) : (i += 1) {
                    try writer.print("index {d}\n", .{try reader.takeLeb128(u32)});
                }
            },
            .table => {
                var i: u32 = 0;
                while (i < entries) : (i += 1) {
                    _ = try parseDumpType(step, std.wasm.RefType, &reader, writer);
                    try parseDumpLimits(&reader, writer);
                }
            },
            .memory => {
                var i: u32 = 0;
                while (i < entries) : (i += 1) {
                    try parseDumpLimits(&reader, writer);
                }
            },
            .global => {
                var i: u32 = 0;
                while (i < entries) : (i += 1) {
                    _ = try parseDumpType(step, std.wasm.Valtype, &reader, writer);
                    try writer.print("mutable {}\n", .{0x01 == try reader.takeLeb128(u1)});
                    try parseDumpInit(step, &reader, writer);
                }
            },
            .@"export" => {
                var i: u32 = 0;
                while (i < entries) : (i += 1) {
                    const name_len = try reader.takeLeb128(u32);
                    const name = data[reader.seek..][0..name_len];
                    reader.seek += name_len;
                    const kind_byte = try reader.takeLeb128(u8);
                    const kind = std.enums.fromInt(std.wasm.ExternalKind, kind_byte) orelse {
                        return step.fail("invalid export kind value '{d}'", .{kind_byte});
                    };
                    const index = try reader.takeLeb128(u32);
                    try writer.print(
                        \\name {s}
                        \\kind {s}
                        \\index {d}
                    , .{ name, @tagName(kind), index });
                    try writer.writeByte('\n');
                }
            },
            .element => {
                var i: u32 = 0;
                while (i < entries) : (i += 1) {
                    try writer.print("table index {d}\n", .{try reader.takeLeb128(u32)});
                    try parseDumpInit(step, &reader, writer);

                    const function_indexes = try reader.takeLeb128(u32);
                    var function_index: u32 = 0;
                    try writer.print("indexes {d}\n", .{function_indexes});
                    while (function_index < function_indexes) : (function_index += 1) {
                        try writer.print("index {d}\n", .{try reader.takeLeb128(u32)});
                    }
                }
            },
            .code => {}, // code section is considered opaque to linker
            .data => {
                var i: u32 = 0;
                while (i < entries) : (i += 1) {
                    const flags = try reader.takeLeb128(u32);
                    const index = if (flags & 0x02 != 0)
                        try reader.takeLeb128(u32)
                    else
                        0;
                    try writer.print("memory index 0x{x}\n", .{index});
                    if (flags == 0) {
                        try parseDumpInit(step, &reader, writer);
                    }

                    const size = try reader.takeLeb128(u32);
                    try writer.print("size {d}\n", .{size});
                    try reader.discardAll(size); // we do not care about the content of the segments
                }
            },
            else => unreachable,
        }
    }

    fn parseDumpType(step: *Step, comptime E: type, reader: *std.Io.Reader, writer: *std.Io.Writer) !E {
        const byte = try reader.takeByte();
        const tag = std.enums.fromInt(E, byte) orelse {
            return step.fail("invalid wasm type value '{d}'", .{byte});
        };
        try writer.print("type {s}\n", .{@tagName(tag)});
        return tag;
    }

    fn parseDumpLimits(reader: anytype, writer: anytype) !void {
        const flags = try reader.takeLeb128(u8);
        const min = try reader.takeLeb128(u32);

        try writer.print("min {x}\n", .{min});
        if (flags != 0) {
            try writer.print("max {x}\n", .{try reader.takeLeb128(u32)});
        }
    }

    fn parseDumpInit(step: *Step, reader: *std.Io.Reader, writer: *std.Io.Writer) !void {
        const byte = try reader.takeByte();
        const opcode = std.enums.fromInt(std.wasm.Opcode, byte) orelse {
            return step.fail("invalid wasm opcode '{d}'", .{byte});
        };
        switch (opcode) {
            .i32_const => try writer.print("i32.const {x}\n", .{try reader.takeLeb128(i32)}),
            .i64_const => try writer.print("i64.const {x}\n", .{try reader.takeLeb128(i64)}),
            .f32_const => try writer.print("f32.const {x}\n", .{@as(f32, @bitCast(try reader.takeInt(u32, .little)))}),
            .f64_const => try writer.print("f64.const {x}\n", .{@as(f64, @bitCast(try reader.takeInt(u64, .little)))}),
            .global_get => try writer.print("global.get {x}\n", .{try reader.takeLeb128(u32)}),
            else => unreachable,
        }
        const end_opcode = try reader.takeLeb128(u8);
        if (end_opcode != @intFromEnum(std.wasm.Opcode.end)) {
            return step.fail("expected 'end' opcode in init expression", .{});
        }
    }

    /// https://webassembly.github.io/spec/core/appendix/custom.html
    fn parseDumpNames(step: *Step, reader: *std.Io.Reader, writer: *std.Io.Writer, data: []const u8) !void {
        while (reader.seek < data.len) {
            switch (try parseDumpType(step, std.wasm.NameSubsection, reader, writer)) {
                // The module name subsection ... consists of a single name
                // that is assigned to the module itself.
                .module => {
                    const size = try reader.takeLeb128(u32);
                    const name_len = try reader.takeLeb128(u32);
                    if (size != name_len + 1) return error.BadSubsectionSize;
                    if (reader.seek + name_len > data.len) return error.UnexpectedEndOfStream;
                    try writer.print("name {s}\n", .{data[reader.seek..][0..name_len]});
                    reader.seek += name_len;
                },

                // The function name subsection ... consists of a name map
                // assigning function names to function indices.
                .function, .global, .data_segment => {
                    const size = try reader.takeLeb128(u32);
                    const entries = try reader.takeLeb128(u32);
                    try writer.print(
                        \\size {d}
                        \\names {d}
                        \\
                    , .{ size, entries });
                    for (0..entries) |_| {
                        const index = try reader.takeLeb128(u32);
                        const name_len = try reader.takeLeb128(u32);
                        if (reader.seek + name_len > data.len) return error.UnexpectedEndOfStream;
                        const name = data[reader.seek..][0..name_len];
                        reader.seek += name.len;

                        try writer.print(
                            \\index {d}
                            \\name {s}
                            \\
                        , .{ index, name });
                    }
                },

                // The local name subsection ... consists of an indirect name
                // map assigning local names to local indices grouped by
                // function indices.
                .local => {
                    return step.fail("TODO implement parseDumpNames for local subsections", .{});
                },

                else => |t| return step.fail("invalid subsection type: {s}", .{@tagName(t)}),
            }
        }
    }

    fn parseDumpProducers(reader: *std.Io.Reader, writer: *std.Io.Writer, data: []const u8) !void {
        const field_count = try reader.takeLeb128(u32);
        try writer.print("fields {d}\n", .{field_count});
        var current_field: u32 = 0;
        while (current_field < field_count) : (current_field += 1) {
            const field_name_length = try reader.takeLeb128(u32);
            const field_name = data[reader.seek..][0..field_name_length];
            reader.seek += field_name_length;

            const value_count = try reader.takeLeb128(u32);
            try writer.print(
                \\field_name {s}
                \\values {d}
            , .{ field_name, value_count });
            try writer.writeByte('\n');
            var current_value: u32 = 0;
            while (current_value < value_count) : (current_value += 1) {
                const value_length = try reader.takeLeb128(u32);
                const value = data[reader.seek..][0..value_length];
                reader.seek += value_length;

                const version_length = try reader.takeLeb128(u32);
                const version = data[reader.seek..][0..version_length];
                reader.seek += version_length;

                try writer.print(
                    \\value_name {s}
                    \\version {s}
                , .{ value, version });
                try writer.writeByte('\n');
            }
        }
    }

    fn parseDumpFeatures(reader: *std.Io.Reader, writer: *std.Io.Writer, data: []const u8) !void {
        const feature_count = try reader.takeLeb128(u32);
        try writer.print("features {d}\n", .{feature_count});

        var index: u32 = 0;
        while (index < feature_count) : (index += 1) {
            const prefix_byte = try reader.takeLeb128(u8);
            const name_length = try reader.takeLeb128(u32);
            const feature_name = data[reader.seek..][0..name_length];
            reader.seek += name_length;

            try writer.print("{c} {s}\n", .{ prefix_byte, feature_name });
        }
    }
};



---
File: /std/Build/Step/Compile.zig
---

const Compile = @This();
const builtin = @import("builtin");

const std = @import("std");
const Io = std.Io;
const mem = std.mem;
const fs = std.fs;
const assert = std.debug.assert;
const panic = std.debug.panic;
const StringHashMap = std.StringHashMap;
const Sha256 = std.crypto.hash.sha2.Sha256;
const Allocator = std.mem.Allocator;
const Step = std.Build.Step;
const LazyPath = std.Build.LazyPath;
const PkgConfigPkg = std.Build.PkgConfigPkg;
const PkgConfigError = std.Build.PkgConfigError;
const RunError = std.Build.RunError;
const Module = std.Build.Module;
const InstallDir = std.Build.InstallDir;
const GeneratedFile = std.Build.GeneratedFile;
const Path = std.Build.Cache.Path;

pub const base_id: Step.Id = .compile;

step: Step,
root_module: *Module,

name: []const u8,
linker_script: ?LazyPath = null,
version_script: ?LazyPath = null,
out_filename: []const u8,
out_lib_filename: []const u8,
linkage: ?std.builtin.LinkMode = null,
version: ?std.SemanticVersion,
kind: Kind,
major_only_filename: ?[]const u8,
name_only_filename: ?[]const u8,
formatted_panics: ?bool = null,
compress_debug_sections: std.zig.CompressDebugSections = .none,
verbose_link: bool,
verbose_cc: bool,
bundle_compiler_rt: ?bool = null,
bundle_ubsan_rt: ?bool = null,
rdynamic: bool,
import_memory: bool = false,
export_memory: bool = false,
/// For WebAssembly targets, this will allow for undefined symbols to
/// be imported from the host environment.
import_symbols: bool = false,
import_table: bool = false,
export_table: bool = false,
initial_memory: ?u64 = null,
max_memory: ?u64 = null,
shared_memory: bool = false,
global_base: ?u64 = null,
/// Set via options; intended to be read-only after that.
zig_lib_dir: ?LazyPath,
exec_cmd_args: ?[]const ?[]const u8,
filters: []const []const u8,
test_runner: ?TestRunner,
wasi_exec_model: ?std.builtin.WasiExecModel = null,

installed_headers: std.array_list.Managed(HeaderInstallation),

/// This step is used to create an include tree that dependent modules can add to their include
/// search paths. Installed headers are copied to this step.
/// This step is created the first time a module links with this artifact and is not
/// created otherwise.
installed_headers_include_tree: ?*Step.WriteFile = null,

/// Behavior of automatic detection of include directories when compiling .rc files.
///  any: Use MSVC if available, fall back to MinGW.
///  msvc: Use MSVC include paths (must be present on the system).
///  gnu: Use MinGW include paths (distributed with Zig).
///  none: Do not use any autodetected include paths.
rc_includes: std.zig.RcIncludes = .any,

/// (Windows) .manifest file to embed in the compilation
/// Set via options; intended to be read-only after that.
win32_manifest: ?LazyPath = null,

/// (Windows) .def file to embed in the compilation (dll)
/// Set via options; intended to be read-only after that.
win32_module_definition: ?LazyPath = null,

installed_path: ?[]const u8,

/// Base address for an executable image.
image_base: ?u64 = null,

libc_file: ?LazyPath = null,

each_lib_rpath: ?bool = null,
/// On ELF targets, this will emit a link section called ".note.gnu.build-id"
/// which can be used to coordinate a stripped binary with its debug symbols.
/// As an example, the bloaty project refuses to work unless its inputs have
/// build ids, in order to prevent accidental mismatches.
/// The default is to not include this section because it slows down linking.
build_id: ?std.zig.BuildId = null,

/// Create a .eh_frame_hdr section and a PT_GNU_EH_FRAME segment in the ELF
/// file.
link_eh_frame_hdr: bool = false,
link_emit_relocs: bool = false,

/// Place every function in its own section so that unused ones may be
/// safely garbage-collected during the linking phase.
link_function_sections: bool = false,

/// Place every data in its own section so that unused ones may be
/// safely garbage-collected during the linking phase.
link_data_sections: bool = false,

/// Remove functions and data that are unreachable by the entry point or
/// exported symbols.
link_gc_sections: ?bool = null,

/// (Windows) Whether or not to enable ASLR. Maps to the /DYNAMICBASE[:NO] linker argument.
linker_dynamicbase: bool = true,

linker_allow_shlib_undefined: ?bool = null,

/// Allow version scripts to refer to undefined symbols.
linker_allow_undefined_version: ?bool = null,

// Enable (or disable) the new DT_RUNPATH tag in the dynamic section.
linker_enable_new_dtags: ?bool = null,

/// Permit read-only relocations in read-only segments. Disallowed by default.
link_z_notext: bool = false,

/// Force all relocations to be read-only after processing.
link_z_relro: bool = true,

/// Allow relocations to be lazily processed after load.
link_z_lazy: bool = false,

/// Common page size
link_z_common_page_size: ?u64 = null,

/// Maximum page size
link_z_max_page_size: ?u64 = null,

/// Force a fatal error if any undefined symbols remain.
link_z_defs: bool = false,

/// (Darwin) Install name for the dylib
install_name: ?[]const u8 = null,

/// (Darwin) Path to entitlements file
entitlements: ?[]const u8 = null,

/// (Darwin) Size of the pagezero segment.
pagezero_size: ?u64 = null,

/// (Darwin) Set size of the padding between the end of load commands
/// and start of `__TEXT,__text` section.
headerpad_size: ?u32 = null,

/// (Darwin) Automatically Set size of the padding between the end of load commands
/// and start of `__TEXT,__text` section to a value fitting all paths expanded to MAXPATHLEN.
headerpad_max_install_names: bool = false,

/// (Darwin) Remove dylibs that are unreachable by the entry point or exported symbols.
dead_strip_dylibs: bool = false,

/// (Darwin) Force load all members of static archives that implement an Objective-C class or category
force_load_objc: bool = false,

/// Whether local symbols should be discarded from the symbol table.
discard_local_symbols: bool = false,

/// Position Independent Executable
pie: ?bool = null,

/// Link Time Optimization mode
lto: ?std.zig.LtoMode = null,

dll_export_fns: ?bool = null,

subsystem: ?std.zig.Subsystem = null,

/// (Windows) When targeting the MinGW ABI, use the unicode entry point (wmain/wWinMain)
mingw_unicode_entry_point: bool = false,

/// How the linker must handle the entry point of the executable.
entry: Entry = .default,

/// List of symbols forced as undefined in the symbol table
/// thus forcing their resolution by the linker.
/// Corresponds to `-u <symbol>` for ELF/MachO and `/include:<symbol>` for COFF/PE.
force_undefined_symbols: std.StringHashMap(void),

/// Overrides the default stack size
stack_size: ?u64 = null,

use_llvm: ?bool,
use_lld: ?bool,
use_new_linker: ?bool,

/// Corresponds to the `-fallow-so-scripts` / `-fno-allow-so-scripts` CLI
/// flags, overriding the global user setting provided to the `zig build`
/// command.
///
/// The compiler defaults this value to off so that users whose system shared
/// libraries are all ELF files don't have to pay the cost of checking every
/// file to find out if it is a text file instead.
allow_so_scripts: ?bool = null,

/// This is an advanced setting that can change the intent of this Compile step.
/// If this value is non-null, it means that this Compile step exists to
/// check for compile errors and return *success* if they match, and failure
/// otherwise.
expect_errors: ?ExpectedCompileErrors = null,

emit_directory: ?*GeneratedFile,

generated_docs: ?*GeneratedFile,
generated_asm: ?*GeneratedFile,
generated_bin: ?*GeneratedFile,
generated_pdb: ?*GeneratedFile,
// hack for stage2_x86_64 + coff
generated_compiler_rt_dyn_lib: ?*GeneratedFile,
generated_implib: ?*GeneratedFile,
generated_llvm_bc: ?*GeneratedFile,
generated_llvm_ir: ?*GeneratedFile,
generated_h: ?*GeneratedFile,

/// The maximum number of distinct errors within a compilation step
/// Defaults to `std.math.maxInt(u16)`
error_limit: ?u32 = null,

/// Computed during make().
is_linking_libc: bool = false,
/// Computed during make().
is_linking_libcpp: bool = false,

/// Populated during the make phase when there is a long-lived compiler process.
/// Managed by the build runner, not user build script.
zig_process: ?*Step.ZigProcess,

/// Enables coverage instrumentation that is only useful if you are using third
/// party fuzzers that depend on it. Otherwise, slows down the instrumented
/// binary with unnecessary function calls.
///
/// This kind of coverage instrumentation is used by AFLplusplus v4.21c,
/// however, modern fuzzers - including Zig - have switched to using "inline
/// 8-bit counters" or "inline bool flag" which incurs only a single
/// instruction for coverage, along with "trace cmp" which instruments
/// comparisons and reports the operands.
///
/// To instead enable fuzz testing instrumentation on a compilation using Zig's
/// builtin fuzzer, see the `fuzz` flag in `Module`.
sanitize_coverage_trace_pc_guard: ?bool = null,

pub const ExpectedCompileErrors = union(enum) {
    contains: []const u8,
    exact: []const []const u8,
    starts_with: []const u8,
    stderr_contains: []const u8,
};

pub const Entry = union(enum) {
    /// Let the compiler decid
```
