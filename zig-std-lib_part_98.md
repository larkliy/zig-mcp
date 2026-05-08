```
);
        },
        else => |e| return e,
    };
}

/// Writes gnu extended header: gnu_long_name or gnu_long_link.
fn writeExtendedHeader(w: *Writer, typeflag: Header.FileType, buffers: []const []const u8) Error!void {
    var len: usize = 0;
    for (buffers) |buf| len += buf.len;

    var header: Header = .init(typeflag);
    try header.setSize(len);
    try header.write(w.underlying_writer);
    for (buffers) |buf|
        try w.underlying_writer.writeAll(buf);
    try w.writePadding(len);
}

fn writePadding(w: *Writer, bytes: usize) Io.Writer.Error!void {
    return writePaddingPos(w, bytes % block_size);
}

fn writePadding64(w: *Writer, bytes: u64) Io.Writer.Error!void {
    return writePaddingPos(w, @intCast(bytes % block_size));
}

fn writePaddingPos(w: *Writer, pos: usize) Io.Writer.Error!void {
    if (pos == 0) return;
    try w.underlying_writer.splatByteAll(0, block_size - pos);
}

/// According to the specification, tar should finish with two zero blocks, but
/// "reasonable system must not assume that such a block exists when reading an
/// archive". Therefore, the Zig standard library recommends to not call this
/// function.
pub fn finishPedantically(w: *Writer) Io.Writer.Error!void {
    try w.underlying_writer.splatByteAll(0, block_size * 2);
}

/// A struct that is exactly 512 bytes and matches tar file format. This is
/// intended to be used for outputting tar files; for parsing there is
/// `std.tar.Header`.
pub const Header = extern struct {
    // This struct was originally copied from
    // https://github.com/mattnite/tar/blob/main/src/main.zig which is MIT
    // licensed.
    //
    // The name, linkname, magic, uname, and gname are null-terminated character
    // strings. All other fields are zero-filled octal numbers in ASCII. Each
    // numeric field of width w contains w minus 1 digits, and a null.
    // Reference: https://www.gnu.org/software/tar/manual/html_node/Standard.html
    // POSIX header:                                  byte offset
    name: [100]u8 = [_]u8{0} ** 100, //                         0
    mode: [7:0]u8 = default_mode.file, //                     100
    uid: [7:0]u8 = [_:0]u8{0} ** 7, // unused                 108
    gid: [7:0]u8 = [_:0]u8{0} ** 7, // unused                 116
    size: [11:0]u8 = [_:0]u8{'0'} ** 11, //                   124
    mtime: [11:0]u8 = [_:0]u8{'0'} ** 11, //                  136
    checksum: [7:0]u8 = [_:0]u8{' '} ** 7, //                 148
    typeflag: FileType = .regular, //                         156
    linkname: [100]u8 = [_]u8{0} ** 100, //                   157
    magic: [6]u8 = [_]u8{ 'u', 's', 't', 'a', 'r', 0 }, //    257
    version: [2]u8 = [_]u8{ '0', '0' }, //                    263
    uname: [32]u8 = [_]u8{0} ** 32, // unused                 265
    gname: [32]u8 = [_]u8{0} ** 32, // unused                 297
    devmajor: [7:0]u8 = [_:0]u8{0} ** 7, // unused            329
    devminor: [7:0]u8 = [_:0]u8{0} ** 7, // unused            337
    prefix: [155]u8 = [_]u8{0} ** 155, //                     345
    pad: [12]u8 = [_]u8{0} ** 12, // unused                   500

    pub const FileType = enum(u8) {
        regular = '0',
        symbolic_link = '2',
        directory = '5',
        gnu_long_name = 'L',
        gnu_long_link = 'K',
    };

    const default_mode = struct {
        const file = [_:0]u8{ '0', '0', '0', '0', '6', '6', '4' }; // 0o664
        const dir = [_:0]u8{ '0', '0', '0', '0', '7', '7', '5' }; // 0o775
        const sym_link = [_:0]u8{ '0', '0', '0', '0', '7', '7', '7' }; // 0o777
        const other = [_:0]u8{ '0', '0', '0', '0', '0', '0', '0' }; // 0o000
    };

    pub fn init(typeflag: FileType) Header {
        return .{
            .typeflag = typeflag,
            .mode = switch (typeflag) {
                .directory => default_mode.dir,
                .symbolic_link => default_mode.sym_link,
                .regular => default_mode.file,
                else => default_mode.other,
            },
        };
    }

    pub fn setSize(w: *Header, size: u64) error{OctalOverflow}!void {
        try octal(&w.size, size);
    }

    fn octal(buf: []u8, value: u64) error{OctalOverflow}!void {
        var remainder: u64 = value;
        var pos: usize = buf.len;
        while (remainder > 0 and pos > 0) {
            pos -= 1;
            const c: u8 = @as(u8, @intCast(remainder % 8)) + '0';
            buf[pos] = c;
            remainder /= 8;
            if (pos == 0 and remainder > 0) return error.OctalOverflow;
        }
    }

    pub fn setMode(w: *Header, mode: u32) error{OctalOverflow}!void {
        try octal(&w.mode, mode);
    }

    // Integer number of seconds since January 1, 1970, 00:00 Coordinated Universal Time.
    pub fn setMtime(w: *Header, mtime: u64) error{OctalOverflow}!void {
        try octal(&w.mtime, mtime);
    }

    pub fn updateChecksum(w: *Header) !void {
        var checksum: usize = ' '; // other 7 w.checksum bytes are initialized to ' '
        for (std.mem.asBytes(w)) |val|
            checksum += val;
        try octal(&w.checksum, checksum);
    }

    pub fn write(h: *Header, bw: *Io.Writer) error{ OctalOverflow, WriteFailed }!void {
        try h.updateChecksum();
        try bw.writeAll(std.mem.asBytes(h));
    }

    pub fn setLinkname(w: *Header, link: []const u8) !void {
        if (link.len > w.linkname.len) return error.NameTooLong;
        @memcpy(w.linkname[0..link.len], link);
    }

    pub fn setPath(w: *Header, prefix: []const u8, sub_path: []const u8) !void {
        const max_prefix = w.prefix.len;
        const max_name = w.name.len;
        const sep = std.fs.path.sep_posix;

        if (prefix.len + sub_path.len > max_name + max_prefix or prefix.len > max_prefix)
            return error.NameTooLong;

        // both fit into name
        if (prefix.len > 0 and prefix.len + sub_path.len < max_name) {
            @memcpy(w.name[0..prefix.len], prefix);
            w.name[prefix.len] = sep;
            @memcpy(w.name[prefix.len + 1 ..][0..sub_path.len], sub_path);
            return;
        }

        // sub_path fits into name
        // there is no prefix or prefix fits into prefix
        if (sub_path.len <= max_name) {
            @memcpy(w.name[0..sub_path.len], sub_path);
            @memcpy(w.prefix[0..prefix.len], prefix);
            return;
        }

        if (prefix.len > 0) {
            @memcpy(w.prefix[0..prefix.len], prefix);
            w.prefix[prefix.len] = sep;
        }
        const prefix_pos = if (prefix.len > 0) prefix.len + 1 else 0;

        // add as much to prefix as you can, must split at /
        const prefix_remaining = max_prefix - prefix_pos;
        if (std.mem.lastIndexOf(u8, sub_path[0..@min(prefix_remaining, sub_path.len)], &.{'/'})) |sep_pos| {
            @memcpy(w.prefix[prefix_pos..][0..sep_pos], sub_path[0..sep_pos]);
            if ((sub_path.len - sep_pos - 1) > max_name) return error.NameTooLong;
            @memcpy(w.name[0..][0 .. sub_path.len - sep_pos - 1], sub_path[sep_pos + 1 ..]);
            return;
        }

        return error.NameTooLong;
    }

    comptime {
        assert(@sizeOf(Header) == 512);
    }

    test "setPath" {
        const cases = [_]struct {
            in: []const []const u8,
            out: []const []const u8,
        }{
            .{
                .in = &.{ "", "123456789" },
                .out = &.{ "", "123456789" },
            },
            // can fit into name
            .{
                .in = &.{ "prefix", "sub_path" },
                .out = &.{ "", "prefix/sub_path" },
            },
            // no more both fits into name
            .{
                .in = &.{ "prefix", "0123456789/" ** 8 ++ "basename" },
                .out = &.{ "prefix", "0123456789/" ** 8 ++ "basename" },
            },
            // put as much as you can into prefix the rest goes into name
            .{
                .in = &.{ "prefix", "0123456789/" ** 10 ++ "basename" },
                .out = &.{ "prefix/" ++ "0123456789/" ** 9 ++ "0123456789", "basename" },
            },

            .{
                .in = &.{ "prefix", "0123456789/" ** 15 ++ "basename" },
                .out = &.{ "prefix/" ++ "0123456789/" ** 12 ++ "0123456789", "0123456789/0123456789/basename" },
            },
            .{
                .in = &.{ "prefix", "0123456789/" ** 21 ++ "basename" },
                .out = &.{ "prefix/" ++ "0123456789/" ** 12 ++ "0123456789", "0123456789/" ** 8 ++ "basename" },
            },
            .{
                .in = &.{ "", "012345678/" ** 10 ++ "foo" },
                .out = &.{ "012345678/" ** 9 ++ "012345678", "foo" },
            },
        };

        for (cases) |case| {
            var header = Header.init(.regular);
            try header.setPath(case.in[0], case.in[1]);
            try testing.expectEqualStrings(case.out[0], std.mem.sliceTo(&header.prefix, 0));
            try testing.expectEqualStrings(case.out[1], std.mem.sliceTo(&header.name, 0));
        }

        const error_cases = [_]struct {
            in: []const []const u8,
        }{
            // basename can't fit into name (106 characters)
            .{ .in = &.{ "zig", "test/cases/compile_errors/regression_test_2980_base_type_u32_is_not_type_checked_properly_when_assigning_a_value_within_a_struct.zig" } },
            // cant fit into 255 + sep
            .{ .in = &.{ "prefix", "0123456789/" ** 22 ++ "basename" } },
            // can fit but sub_path can't be split (there is no separator)
            .{ .in = &.{ "prefix", "0123456789" ** 10 ++ "a" } },
            .{ .in = &.{ "prefix", "0123456789" ** 14 ++ "basename" } },
        };

        for (error_cases) |case| {
            var header = Header.init(.regular);
            try testing.expectError(
                error.NameTooLong,
                header.setPath(case.in[0], case.in[1]),
            );
        }
    }
};

test {
    _ = Header;
}

test "write files" {
    const files = [_]struct {
        path: []const u8,
        content: []const u8,
    }{
        .{ .path = "foo", .content = "bar" },
        .{ .path = "a12345678/" ** 10 ++ "foo", .content = "a" ** 511 },
        .{ .path = "b12345678/" ** 24 ++ "foo", .content = "b" ** 512 },
        .{ .path = "c12345678/" ** 25 ++ "foo", .content = "c" ** 513 },
        .{ .path = "d12345678/" ** 51 ++ "foo", .content = "d" ** 1025 },
        .{ .path = "e123456789" ** 11, .content = "e" },
    };

    var file_name_buffer: [std.fs.max_path_bytes]u8 = undefined;
    var link_name_buffer: [std.fs.max_path_bytes]u8 = undefined;

    // with root
    {
        const root = "root";

        var output: Io.Writer.Allocating = .init(testing.allocator);
        var w: Writer = .{ .underlying_writer = &output.writer };
        defer output.deinit();
        try w.setRoot(root);
        for (files) |file|
            try w.writeFileBytes(file.path, file.content, .{});

        var input: Io.Reader = .fixed(output.written());
        var it: std.tar.Iterator = .init(&input, .{
            .file_name_buffer = &file_name_buffer,
            .link_name_buffer = &link_name_buffer,
        });

        // first entry is directory with prefix
        {
            const actual = (try it.next()).?;
            try testing.expectEqualStrings(root, actual.name);
            try testing.expectEqual(std.tar.FileKind.directory, actual.kind);
        }

        var i: usize = 0;
        while (try it.next()) |actual| {
            defer i += 1;
            const expected = files[i];
            try testing.expectEqualStrings(root, actual.name[0..root.len]);
            try testing.expectEqual('/', actual.name[root.len..][0]);
            try testing.expectEqualStrings(expected.path, actual.name[root.len + 1 ..]);

            var content: Io.Writer.Allocating = .init(testing.allocator);
            defer content.deinit();
            try it.streamRemaining(actual, &content.writer);
            try testing.expectEqualSlices(u8, expected.content, content.written());
        }
    }
    // without root
    {
        var output: Io.Writer.Allocating = .init(testing.allocator);
        var w: Writer = .{ .underlying_writer = &output.writer };
        defer output.deinit();
        for (files) |file| {
            var content: Io.Reader = .fixed(file.content);
            try w.writeFileStream(file.path, file.content.len, &content, .{});
        }

        var input: Io.Reader = .fixed(output.written());
        var it: std.tar.Iterator = .init(&input, .{
            .file_name_buffer = &file_name_buffer,
            .link_name_buffer = &link_name_buffer,
        });

        var i: usize = 0;
        while (try it.next()) |actual| {
            defer i += 1;
            const expected = files[i];
            try testing.expectEqualStrings(expected.path, actual.name);

            var content: Io.Writer.Allocating = .init(testing.allocator);
            defer content.deinit();
            try it.streamRemaining(actual, &content.writer);
            try testing.expectEqualSlices(u8, expected.content, content.written());
        }
        try w.finishPedantically();
    }
}



---
File: /std/Target/aarch64.zig
---

//! This file is auto-generated by tools/update_cpu_features.zig.

const std = @import("../std.zig");
const CpuFeature = std.Target.Cpu.Feature;
const CpuModel = std.Target.Cpu.Model;

pub const Feature = enum {
    a320,
    addr_lsl_slow_14,
    aes,
    aggressive_fma,
    alternate_sextload_cvt_f32_pattern,
    altnzcv,
    alu_lsl_fast,
    am,
    amvs,
    arith_bcc_fusion,
    arith_cbz_fusion,
    ascend_store_address,
    avoid_ldapur,
    balance_fp_ops,
    bf16,
    brbe,
    bti,
    call_saved_x10,
    call_saved_x11,
    call_saved_x12,
    call_saved_x13,
    call_saved_x14,
    call_saved_x15,
    call_saved_x18,
    call_saved_x8,
    call_saved_x9,
    ccdp,
    ccidx,
    ccpp,
    chk,
    clrbhb,
    cmp_bcc_fusion,
    cmpbr,
    complxnum,
    contextidr_el2,
    cpa,
    crc,
    crypto,
    cssc,
    d128,
    disable_fast_inc_vl,
    disable_latency_sched_heuristic,
    disable_ldp,
    disable_stp,
    dit,
    dotprod,
    ecv,
    el2vmsa,
    el3,
    enable_select_opt,
    ete,
    execute_only,
    exynos_cheap_as_move,
    f32mm,
    f64mm,
    f8f16mm,
    f8f32mm,
    faminmax,
    fgt,
    fix_cortex_a53_835769,
    flagm,
    fmv,
    force_32bit_jump_tables,
    fp16fml,
    fp8,
    fp8dot2,
    fp8dot4,
    fp8fma,
    fp_armv8,
    fpac,
    fprcvt,
    fptoint,
    fujitsu_monaka,
    fullfp16,
    fuse_address,
    fuse_addsub_2reg_const1,
    fuse_adrp_add,
    fuse_aes,
    fuse_arith_logic,
    fuse_crypto_eor,
    fuse_csel,
    fuse_literals,
    gcs,
    harden_sls_blr,
    harden_sls_nocomdat,
    harden_sls_retbr,
    hbc,
    hcx,
    i8mm,
    ite,
    jsconv,
    ldp_aligned_only,
    lor,
    ls64,
    lse,
    lse128,
    lse2,
    lsfe,
    lsui,
    lut,
    mec,
    mops,
    mpam,
    mte,
    neon,
    nmi,
    no_bti_at_return_twice,
    no_neg_immediates,
    no_sve_fp_ld1r,
    no_zcz_fp,
    nv,
    occmo,
    olympus,
    outline_atomics,
    pan,
    pan_rwv,
    pauth,
    pauth_lr,
    pcdphint,
    perfmon,
    pops,
    predictable_select_expensive,
    predres,
    prfm_slc_target,
    rand,
    ras,
    rasv2,
    rcpc,
    rcpc3,
    rcpc_immo,
    rdm,
    reserve_lr_for_ra,
    reserve_x1,
    reserve_x10,
    reserve_x11,
    reserve_x12,
    reserve_x13,
    reserve_x14,
    reserve_x15,
    reserve_x18,
    reserve_x2,
    reserve_x20,
    reserve_x21,
    reserve_x22,
    reserve_x23,
    reserve_x24,
    reserve_x25,
    reserve_x26,
    reserve_x27,
    reserve_x28,
    reserve_x3,
    reserve_x4,
    reserve_x5,
    reserve_x6,
    reserve_x7,
    reserve_x9,
    rme,
    sb,
    sel2,
    sha2,
    sha3,
    slow_misaligned_128store,
    slow_paired_128,
    slow_strqro_store,
    sm4,
    sme,
    sme2,
    sme2p1,
    sme2p2,
    sme_b16b16,
    sme_f16f16,
    sme_f64f64,
    sme_f8f16,
    sme_f8f32,
    sme_fa64,
    sme_i16i64,
    sme_lutv2,
    sme_mop4,
    sme_tmop,
    spe,
    spe_eef,
    specres2,
    specrestrict,
    ssbs,
    ssve_aes,
    ssve_bitperm,
    ssve_fexpa,
    ssve_fp8dot2,
    ssve_fp8dot4,
    ssve_fp8fma,
    store_pair_suppress,
    stp_aligned_only,
    strict_align,
    sve,
    sve2,
    sve2_aes,
    sve2_bitperm,
    sve2_sha3,
    sve2_sm4,
    sve2p1,
    sve2p2,
    sve_aes,
    sve_aes2,
    sve_b16b16,
    sve_bfscale,
    sve_bitperm,
    sve_f16f32mm,
    sve_sha3,
    sve_sm4,
    tagged_globals,
    the,
    tlb_rmi,
    tlbiw,
    tme,
    tpidr_el1,
    tpidr_el2,
    tpidr_el3,
    tpidrro_el0,
    tracev8_4,
    trbe,
    uaops,
    use_experimental_zeroing_pseudos,
    use_fixed_over_scalable_if_equal_cost,
    use_postra_scheduler,
    use_reciprocal_square_root,
    v8_1a,
    v8_2a,
    v8_3a,
    v8_4a,
    v8_5a,
    v8_6a,
    v8_7a,
    v8_8a,
    v8_9a,
    v8a,
    v8r,
    v9_1a,
    v9_2a,
    v9_3a,
    v9_4a,
    v9_5a,
    v9_6a,
    v9a,
    vh,
    wfxt,
    xs,
    zcm_fpr32,
    zcm_fpr64,
    zcm_gpr32,
    zcm_gpr64,
    zcz,
    zcz_fp_workaround,
    zcz_gp,
};

pub const featureSet = CpuFeature.FeatureSetFns(Feature).featureSet;
pub const featureSetHas = CpuFeature.FeatureSetFns(Feature).featureSetHas;
pub const featureSetHasAny = CpuFeature.FeatureSetFns(Feature).featureSetHasAny;
pub const featureSetHasAll = CpuFeature.FeatureSetFns(Feature).featureSetHasAll;

pub const all_features = blk: {
    @setEvalBranchQuota(2000);
    const len = @typeInfo(Feature).@"enum".fields.len;
    std.debug.assert(len <= CpuFeature.Set.needed_bit_count);
    var result: [len]CpuFeature = undefined;
    result[@intFromEnum(Feature.a320)] = .{
        .llvm_name = "a320",
        .description = "Cortex-A320 ARM processors",
        .dependencies = featureSet(&[_]Feature{
            .fuse_adrp_add,
            .fuse_aes,
            .use_postra_scheduler,
        }),
    };
    result[@intFromEnum(Feature.addr_lsl_slow_14)] = .{
        .llvm_name = "addr-lsl-slow-14",
        .description = "Address operands with shift amount of 1 or 4 are slow",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.aes)] = .{
        .llvm_name = "aes",
        .description = "Enable AES support",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.aggressive_fma)] = .{
        .llvm_name = "aggressive-fma",
        .description = "Enable Aggressive FMA for floating-point.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.alternate_sextload_cvt_f32_pattern)] = .{
        .llvm_name = "alternate-sextload-cvt-f32-pattern",
        .description = "Use alternative pattern for sextload convert to f32",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.altnzcv)] = .{
        .llvm_name = "altnzcv",
        .description = "Enable alternative NZCV format for floating point comparisons",
        .dependencies = featureSet(&[_]Feature{
            .flagm,
        }),
    };
    result[@intFromEnum(Feature.alu_lsl_fast)] = .{
        .llvm_name = "alu-lsl-fast",
        .description = "Add/Sub operations with lsl shift <= 4 are cheap",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.am)] = .{
        .llvm_name = "am",
        .description = "Enable Armv8.4-A Activity Monitors extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.amvs)] = .{
        .llvm_name = "amvs",
        .description = "Enable Armv8.6-A Activity Monitors Virtualization support",
        .dependencies = featureSet(&[_]Feature{
            .am,
        }),
    };
    result[@intFromEnum(Feature.arith_bcc_fusion)] = .{
        .llvm_name = "arith-bcc-fusion",
        .description = "CPU fuses arithmetic+bcc operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.arith_cbz_fusion)] = .{
        .llvm_name = "arith-cbz-fusion",
        .description = "CPU fuses arithmetic + cbz/cbnz operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ascend_store_address)] = .{
        .llvm_name = "ascend-store-address",
        .description = "Schedule vector stores by ascending address",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.avoid_ldapur)] = .{
        .llvm_name = "avoid-ldapur",
        .description = "Prefer add+ldapr to offset ldapur",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.balance_fp_ops)] = .{
        .llvm_name = "balance-fp-ops",
        .description = "balance mix of odd and even D-registers for fp multiply(-accumulate) ops",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.bf16)] = .{
        .llvm_name = "bf16",
        .description = "Enable BFloat16 Extension",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.brbe)] = .{
        .llvm_name = "brbe",
        .description = "Enable Branch Record Buffer Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.bti)] = .{
        .llvm_name = "bti",
        .description = "Enable Branch Target Identification",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.call_saved_x10)] = .{
        .llvm_name = "call-saved-x10",
        .description = "Make X10 callee saved.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.call_saved_x11)] = .{
        .llvm_name = "call-saved-x11",
        .description = "Make X11 callee saved.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.call_saved_x12)] = .{
        .llvm_name = "call-saved-x12",
        .description = "Make X12 callee saved.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.call_saved_x13)] = .{
        .llvm_name = "call-saved-x13",
        .description = "Make X13 callee saved.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.call_saved_x14)] = .{
        .llvm_name = "call-saved-x14",
        .description = "Make X14 callee saved.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.call_saved_x15)] = .{
        .llvm_name = "call-saved-x15",
        .description = "Make X15 callee saved.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.call_saved_x18)] = .{
        .llvm_name = "call-saved-x18",
        .description = "Make X18 callee saved.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.call_saved_x8)] = .{
        .llvm_name = "call-saved-x8",
        .description = "Make X8 callee saved.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.call_saved_x9)] = .{
        .llvm_name = "call-saved-x9",
        .description = "Make X9 callee saved.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ccdp)] = .{
        .llvm_name = "ccdp",
        .description = "Enable Armv8.5-A Cache Clean to Point of Deep Persistence",
        .dependencies = featureSet(&[_]Feature{
            .ccpp,
        }),
    };
    result[@intFromEnum(Feature.ccidx)] = .{
        .llvm_name = "ccidx",
        .description = "Enable Armv8.3-A Extend of the CCSIDR number of sets",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ccpp)] = .{
        .llvm_name = "ccpp",
        .description = "Enable Armv8.2-A data Cache Clean to Point of Persistence",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.chk)] = .{
        .llvm_name = "chk",
        .description = "Enable Armv8.0-A Check Feature Status Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.clrbhb)] = .{
        .llvm_name = "clrbhb",
        .description = "Enable Clear BHB instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.cmp_bcc_fusion)] = .{
        .llvm_name = "cmp-bcc-fusion",
        .description = "CPU fuses cmp+bcc operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.cmpbr)] = .{
        .llvm_name = "cmpbr",
        .description = "Enable Armv9.6-A base compare and branch instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.complxnum)] = .{
        .llvm_name = "complxnum",
        .description = "Enable Armv8.3-A Floating-point complex number support",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.contextidr_el2)] = .{
        .llvm_name = "CONTEXTIDREL2",
        .description = "Enable RW operand Context ID Register (EL2)",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.cpa)] = .{
        .llvm_name = "cpa",
        .description = "Enable Armv9.5-A Checked Pointer Arithmetic",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.crc)] = .{
        .llvm_name = "crc",
        .description = "Enable Armv8.0-A CRC-32 checksum instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.crypto)] = .{
        .llvm_name = "crypto",
        .description = "Enable cryptographic instructions",
        .dependencies = featureSet(&[_]Feature{
            .aes,
            .sha2,
        }),
    };
    result[@intFromEnum(Feature.cssc)] = .{
        .llvm_name = "cssc",
        .description = "Enable Common Short Sequence Compression (CSSC) instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.d128)] = .{
        .llvm_name = "d128",
        .description = "Enable Armv9.4-A 128-bit Page Table Descriptors, System Registers and instructions",
        .dependencies = featureSet(&[_]Feature{
            .lse128,
        }),
    };
    result[@intFromEnum(Feature.disable_fast_inc_vl)] = .{
        .llvm_name = "disable-fast-inc-vl",
        .description = "Do not prefer INC/DEC, ALL, { 1, 2, 4 } over ADDVL",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.disable_latency_sched_heuristic)] = .{
        .llvm_name = "disable-latency-sched-heuristic",
        .description = "Disable latency scheduling heuristic",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.disable_ldp)] = .{
        .llvm_name = "disable-ldp",
        .description = "Do not emit ldp",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.disable_stp)] = .{
        .llvm_name = "disable-stp",
        .description = "Do not emit stp",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dit)] = .{
        .llvm_name = "dit",
        .description = "Enable Armv8.4-A Data Independent Timing instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dotprod)] = .{
        .llvm_name = "dotprod",
        .description = "Enable dot product support",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.ecv)] = .{
        .llvm_name = "ecv",
        .description = "Enable enhanced counter virtualization extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.el2vmsa)] = .{
        .llvm_name = "el2vmsa",
        .description = "Enable Exception Level 2 Virtual Memory System Architecture",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.el3)] = .{
        .llvm_name = "el3",
        .description = "Enable Exception Level 3",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.enable_select_opt)] = .{
        .llvm_name = "enable-select-opt",
        .description = "Enable the select optimize pass for select loop heuristics",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ete)] = .{
        .llvm_name = "ete",
        .description = "Enable Embedded Trace Extension",
        .dependencies = featureSet(&[_]Feature{
            .trbe,
        }),
    };
    result[@intFromEnum(Feature.execute_only)] = .{
        .llvm_name = "execute-only",
        .description = "Enable the generation of execute only code.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.exynos_cheap_as_move)] = .{
        .llvm_name = "exynos-cheap-as-move",
        .description = "Use Exynos specific handling of cheap instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.f32mm)] = .{
        .llvm_name = "f32mm",
        .description = "Enable Matrix Multiply FP32 Extension",
        .dependencies = featureSet(&[_]Feature{
            .sve,
        }),
    };
    result[@intFromEnum(Feature.f64mm)] = .{
        .llvm_name = "f64mm",
        .description = "Enable Matrix Multiply FP64 Extension",
        .dependencies = featureSet(&[_]Feature{
            .sve,
        }),
    };
    result[@intFromEnum(Feature.f8f16mm)] = .{
        .llvm_name = "f8f16mm",
        .description = "Enable Armv9.6-A FP8 to Half-Precision Matrix Multiplication",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
        }),
    };
    result[@intFromEnum(Feature.f8f32mm)] = .{
        .llvm_name = "f8f32mm",
        .description = "Enable Armv9.6-A FP8 to Single-Precision Matrix Multiplication",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
        }),
    };
    result[@intFromEnum(Feature.faminmax)] = .{
        .llvm_name = "faminmax",
        .description = "Enable FAMIN and FAMAX instructions",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.fgt)] = .{
        .llvm_name = "fgt",
        .description = "Enable fine grained virtualization traps extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fix_cortex_a53_835769)] = .{
        .llvm_name = "fix-cortex-a53-835769",
        .description = "Mitigate Cortex-A53 Erratum 835769",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flagm)] = .{
        .llvm_name = "flagm",
        .description = "Enable Armv8.4-A Flag Manipulation instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fmv)] = .{
        .llvm_name = "fmv",
        .description = "Enable Function Multi Versioning support.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.force_32bit_jump_tables)] = .{
        .llvm_name = "force-32bit-jump-tables",
        .description = "Force jump table entries to be 32-bits wide except at MinSize",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fp16fml)] = .{
        .llvm_name = "fp16fml",
        .description = "Enable FP16 FML instructions",
        .dependencies = featureSet(&[_]Feature{
            .fullfp16,
            .neon,
        }),
    };
    result[@intFromEnum(Feature.fp8)] = .{
        .llvm_name = "fp8",
        .description = "Enable FP8 instructions",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.fp8dot2)] = .{
        .llvm_name = "fp8dot2",
        .description = "Enable FP8 2-way dot instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
        }),
    };
    result[@intFromEnum(Feature.fp8dot4)] = .{
        .llvm_name = "fp8dot4",
        .description = "Enable FP8 4-way dot instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
        }),
    };
    result[@intFromEnum(Feature.fp8fma)] = .{
        .llvm_name = "fp8fma",
        .description = "Enable Armv9.5-A FP8 multiply-add instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
        }),
    };
    result[@intFromEnum(Feature.fp_armv8)] = .{
        .llvm_name = "fp-armv8",
        .description = "Enable Armv8.0-A Floating Point Extensions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fpac)] = .{
        .llvm_name = "fpac",
        .description = "Enable Armv8.3-A Pointer Authentication Faulting enhancement",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fprcvt)] = .{
        .llvm_name = "fprcvt",
        .description = "Enable Armv9.6-A base convert instructions for SIMD&FP scalar register operands of different input and output sizes",
        .dependencies = featureSet(&[_]Feature{
            .fp_armv8,
        }),
    };
    result[@intFromEnum(Feature.fptoint)] = .{
        .llvm_name = "fptoint",
        .description = "Enable FRInt[32|64][Z|X] instructions that round a floating-point number to an integer (in FP format) forcing it to fit into a 32- or 64-bit int",
        .dependencies = featureSet(&[_]Feature{
            .fp_armv8,
        }),
    };
    result[@intFromEnum(Feature.fujitsu_monaka)] = .{
        .llvm_name = "fujitsu-monaka",
        .description = "Fujitsu FUJITSU-MONAKA processors",
        .dependencies = featureSet(&[_]Feature{
            .arith_bcc_fusion,
            .enable_select_opt,
            .predictable_select_expensive,
            .use_postra_scheduler,
        }),
    };
    result[@intFromEnum(Feature.fullfp16)] = .{
        .llvm_name = "fullfp16",
        .description = "Enable half-precision floating-point data processing",
        .dependencies = featureSet(&[_]Feature{
            .fp_armv8,
        }),
    };
    result[@intFromEnum(Feature.fuse_address)] = .{
        .llvm_name = "fuse-address",
        .description = "CPU fuses address generation and memory operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fuse_addsub_2reg_const1)] = .{
        .llvm_name = "fuse-addsub-2reg-const1",
        .description = "CPU fuses (a + b + 1) and (a - b - 1)",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fuse_adrp_add)] = .{
        .llvm_name = "fuse-adrp-add",
        .description = "CPU fuses adrp+add operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fuse_aes)] = .{
        .llvm_name = "fuse-aes",
        .description = "CPU fuses AES crypto operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fuse_arith_logic)] = .{
        .llvm_name = "fuse-arith-logic",
        .description = "CPU fuses arithmetic and logic operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fuse_crypto_eor)] = .{
        .llvm_name = "fuse-crypto-eor",
        .description = "CPU fuses AES/PMULL and EOR operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fuse_csel)] = .{
        .llvm_name = "fuse-csel",
        .description = "CPU fuses conditional select operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fuse_literals)] = .{
        .llvm_name = "fuse-literals",
        .description = "CPU fuses literal generation operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gcs)] = .{
        .llvm_name = "gcs",
        .description = "Enable Armv9.4-A Guarded Call Stack Extension",
        .dependencies = featureSet(&[_]Feature{
            .chk,
        }),
    };
    result[@intFromEnum(Feature.harden_sls_blr)] = .{
        .llvm_name = "harden-sls-blr",
        .description = "Harden against straight line speculation across BLR instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.harden_sls_nocomdat)] = .{
        .llvm_name = "harden-sls-nocomdat",
        .description = "Generate thunk code for SLS mitigation in the normal text section",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.harden_sls_retbr)] = .{
        .llvm_name = "harden-sls-retbr",
        .description = "Harden against straight line speculation across RET and BR instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.hbc)] = .{
        .llvm_name = "hbc",
        .description = "Enable Armv8.8-A Hinted Conditional Branches Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.hcx)] = .{
        .llvm_name = "hcx",
        .description = "Enable Armv8.7-A HCRX_EL2 system register",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.i8mm)] = .{
        .llvm_name = "i8mm",
        .description = "Enable Matrix Multiply Int8 Extension",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.ite)] = .{
        .llvm_name = "ite",
        .description = "Enable Armv9.4-A Instrumentation Extension",
        .dependencies = featureSet(&[_]Feature{
            .ete,
        }),
    };
    result[@intFromEnum(Feature.jsconv)] = .{
        .llvm_name = "jsconv",
        .description = "Enable Armv8.3-A JavaScript FP conversion instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp_armv8,
        }),
    };
    result[@intFromEnum(Feature.ldp_aligned_only)] = .{
        .llvm_name = "ldp-aligned-only",
        .description = "In order to emit ldp, first check if the load will be aligned to 2 * element_size",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.lor)] = .{
        .llvm_name = "lor",
        .description = "Enable Armv8.1-A Limited Ordering Regions extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ls64)] = .{
        .llvm_name = "ls64",
        .description = "Enable Armv8.7-A LD64B/ST64B Accelerator Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.lse)] = .{
        .llvm_name = "lse",
        .description = "Enable Armv8.1-A Large System Extension (LSE) atomic instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.lse128)] = .{
        .llvm_name = "lse128",
        .description = "Enable Armv9.4-A 128-bit Atomic instructions",
        .dependencies = featureSet(&[_]Feature{
            .lse,
        }),
    };
    result[@intFromEnum(Feature.lse2)] = .{
        .llvm_name = "lse2",
        .description = "Enable Armv8.4-A Large System Extension 2 (LSE2) atomicity rules",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.lsfe)] = .{
        .llvm_name = "lsfe",
        .description = "Enable Armv9.6-A base Atomic floating-point in-memory instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp_armv8,
        }),
    };
    result[@intFromEnum(Feature.lsui)] = .{
        .llvm_name = "lsui",
        .description = "Enable Armv9.6-A unprivileged load/store instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.lut)] = .{
        .llvm_name = "lut",
        .description = "Enable Lookup Table instructions",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.mec)] = .{
        .llvm_name = "mec",
        .description = "Enable Memory Encryption Contexts Extension",
        .dependencies = featureSet(&[_]Feature{
            .rme,
        }),
    };
    result[@intFromEnum(Feature.mops)] = .{
        .llvm_name = "mops",
        .description = "Enable Armv8.8-A memcpy and memset acceleration instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mpam)] = .{
        .llvm_name = "mpam",
        .description = "Enable Armv8.4-A Memory system Partitioning and Monitoring extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mte)] = .{
        .llvm_name = "mte",
        .description = "Enable Memory Tagging Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.neon)] = .{
        .llvm_name = "neon",
        .description = "Enable Advanced SIMD instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp_armv8,
        }),
    };
    result[@intFromEnum(Feature.nmi)] = .{
        .llvm_name = "nmi",
        .description = "Enable Armv8.8-A Non-maskable Interrupts",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.no_bti_at_return_twice)] = .{
        .llvm_name = "no-bti-at-return-twice",
        .description = "Don't place a BTI instruction after a return-twice",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.no_neg_immediates)] = .{
        .llvm_name = "no-neg-immediates",
        .description = "Convert immediates and instructions to their negated or complemented equivalent when the immediate does not fit in the encoding.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.no_sve_fp_ld1r)] = .{
        .llvm_name = "no-sve-fp-ld1r",
        .description = "Avoid using LD1RX instructions for FP",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.no_zcz_fp)] = .{
        .llvm_name = "no-zcz-fp",
        .description = "Has no zero-cycle zeroing instructions for FP registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.nv)] = .{
        .llvm_name = "nv",
        .description = "Enable Armv8.4-A Nested Virtualization Enchancement",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.occmo)] = .{
        .llvm_name = "occmo",
        .description = "Enable Armv9.6-A Outer cacheable cache maintenance operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.olympus)] = .{
        .llvm_name = "olympus",
        .description = "NVIDIA Olympus processors",
        .dependencies = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .enable_select_opt,
            .fuse_adrp_add,
            .fuse_aes,
            .predictable_select_expensive,
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
        }),
    };
    result[@intFromEnum(Feature.outline_atomics)] = .{
        .llvm_name = "outline-atomics",
        .description = "Enable out of line atomics to support LSE instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.pan)] = .{
        .llvm_name = "pan",
        .description = "Enable Armv8.1-A Privileged Access-Never extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.pan_rwv)] = .{
        .llvm_name = "pan-rwv",
        .description = "Enable Armv8.2-A PAN s1e1R and s1e1W Variants",
        .dependencies = featureSet(&[_]Feature{
            .pan,
        }),
    };
    result[@intFromEnum(Feature.pauth)] = .{
        .llvm_name = "pauth",
        .description = "Enable Armv8.3-A Pointer Authentication extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.pauth_lr)] = .{
        .llvm_name = "pauth-lr",
        .description = "Enable Armv9.5-A PAC enhancements",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.pcdphint)] = .{
        .llvm_name = "pcdphint",
        .description = "Enable Armv9.6-A Producer Consumer Data Placement hints",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.perfmon)] = .{
        .llvm_name = "perfmon",
        .description = "Enable Armv8.0-A PMUv3 Performance Monitors extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.pops)] = .{
        .llvm_name = "pops",
        .description = "Enable Armv9.6-A Point Of Physical Storage (PoPS) DC instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.predictable_select_expensive)] = .{
        .llvm_name = "predictable-select-expensive",
        .description = "Prefer likely predicted branches over selects",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.predres)] = .{
        .llvm_name = "predres",
        .description = "Enable Armv8.5-A execution and data prediction invalidation instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.prfm_slc_target)] = .{
        .llvm_name = "prfm-slc-target",
        .description = "Enable SLC target for PRFM instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.rand)] = .{
        .llvm_name = "rand",
        .description = "Enable Random Number generation instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ras)] = .{
        .llvm_name = "ras",
        .description = "Enable Armv8.0-A Reliability, Availability and Serviceability Extensions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.rasv2)] = .{
        .llvm_name = "rasv2",
        .description = "Enable Armv8.9-A Reliability, Availability and Serviceability Extensions",
        .dependencies = featureSet(&[_]Feature{
            .ras,
        }),
    };
    result[@intFromEnum(Feature.rcpc)] = .{
        .llvm_name = "rcpc",
        .description = "Enable support for RCPC extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.rcpc3)] = .{
        .llvm_name = "rcpc3",
        .description = "Enable Armv8.9-A RCPC instructions for A64 and Advanced SIMD and floating-point instruction set",
        .dependencies = featureSet(&[_]Feature{
            .rcpc_immo,
        }),
    };
    result[@intFromEnum(Feature.rcpc_immo)] = .{
        .llvm_name = "rcpc-immo",
        .description = "Enable Armv8.4-A RCPC instructions with Immediate Offsets",
        .dependencies = featureSet(&[_]Feature{
            .rcpc,
        }),
    };
    result[@intFromEnum(Feature.rdm)] = .{
        .llvm_name = "rdm",
        .description = "Enable Armv8.1-A Rounding Double Multiply Add/Subtract instructions",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.reserve_lr_for_ra)] = .{
        .llvm_name = "reserve-lr-for-ra",
        .description = "Reserve LR for call use only",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x1)] = .{
        .llvm_name = "reserve-x1",
        .description = "Reserve X1, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x10)] = .{
        .llvm_name = "reserve-x10",
        .description = "Reserve X10, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x11)] = .{
        .llvm_name = "reserve-x11",
        .description = "Reserve X11, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x12)] = .{
        .llvm_name = "reserve-x12",
        .description = "Reserve X12, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x13)] = .{
        .llvm_name = "reserve-x13",
        .description = "Reserve X13, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x14)] = .{
        .llvm_name = "reserve-x14",
        .description = "Reserve X14, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x15)] = .{
        .llvm_name = "reserve-x15",
        .description = "Reserve X15, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x18)] = .{
        .llvm_name = "reserve-x18",
        .description = "Reserve X18, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x2)] = .{
        .llvm_name = "reserve-x2",
        .description = "Reserve X2, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x20)] = .{
        .llvm_name = "reserve-x20",
        .description = "Reserve X20, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x21)] = .{
        .llvm_name = "reserve-x21",
        .description = "Reserve X21, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x22)] = .{
        .llvm_name = "reserve-x22",
        .description = "Reserve X22, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x23)] = .{
        .llvm_name = "reserve-x23",
        .description = "Reserve X23, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x24)] = .{
        .llvm_name = "reserve-x24",
        .description = "Reserve X24, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x25)] = .{
        .llvm_name = "reserve-x25",
        .description = "Reserve X25, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x26)] = .{
        .llvm_name = "reserve-x26",
        .description = "Reserve X26, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x27)] = .{
        .llvm_name = "reserve-x27",
        .description = "Reserve X27, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x28)] = .{
        .llvm_name = "reserve-x28",
        .description = "Reserve X28, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x3)] = .{
        .llvm_name = "reserve-x3",
        .description = "Reserve X3, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x4)] = .{
        .llvm_name = "reserve-x4",
        .description = "Reserve X4, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x5)] = .{
        .llvm_name = "reserve-x5",
        .description = "Reserve X5, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x6)] = .{
        .llvm_name = "reserve-x6",
        .description = "Reserve X6, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x7)] = .{
        .llvm_name = "reserve-x7",
        .description = "Reserve X7, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.reserve_x9)] = .{
        .llvm_name = "reserve-x9",
        .description = "Reserve X9, making it unavailable as a GPR",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.rme)] = .{
        .llvm_name = "rme",
        .description = "Enable Realm Management Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sb)] = .{
        .llvm_name = "sb",
        .description = "Enable Armv8.5-A Speculation Barrier",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sel2)] = .{
        .llvm_name = "sel2",
        .description = "Enable Armv8.4-A Secure Exception Level 2 extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sha2)] = .{
        .llvm_name = "sha2",
        .description = "Enable SHA1 and SHA256 support",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.sha3)] = .{
        .llvm_name = "sha3",
        .description = "Enable SHA512 and SHA3 support",
        .dependencies = featureSet(&[_]Feature{
            .sha2,
        }),
    };
    result[@intFromEnum(Feature.slow_misaligned_128store)] = .{
        .llvm_name = "slow-misaligned-128store",
        .description = "Misaligned 128 bit stores are slow",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.slow_paired_128)] = .{
        .llvm_name = "slow-paired-128",
        .description = "Paired 128 bit loads and stores are slow",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.slow_strqro_store)] = .{
        .llvm_name = "slow-strqro-store",
        .description = "STR of Q register with register offset is slow",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sm4)] = .{
        .llvm_name = "sm4",
        .description = "Enable SM3 and SM4 support",
        .dependencies = featureSet(&[_]Feature{
            .neon,
        }),
    };
    result[@intFromEnum(Feature.sme)] = .{
        .llvm_name = "sme",
        .description = "Enable Scalable Matrix Extension (SME)",
        .dependencies = featureSet(&[_]Feature{
            .bf16,
            .fullfp16,
        }),
    };
    result[@intFromEnum(Feature.sme2)] = .{
        .llvm_name = "sme2",
        .description = "Enable Scalable Matrix Extension 2 (SME2) instructions",
        .dependencies = featureSet(&[_]Feature{
            .sme,
        }),
    };
    result[@intFromEnum(Feature.sme2p1)] = .{
        .llvm_name = "sme2p1",
        .description = "Enable Scalable Matrix Extension 2.1 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.sme2p2)] = .{
        .llvm_name = "sme2p2",
        .description = "Enable Armv9.6-A Scalable Matrix Extension 2.2 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sme2p1,
        }),
    };
    result[@intFromEnum(Feature.sme_b16b16)] = .{
        .llvm_name = "sme-b16b16",
        .description = "Enable SME2.1 ZA-targeting non-widening BFloat16 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sme2,
            .sve_b16b16,
        }),
    };
    result[@intFromEnum(Feature.sme_f16f16)] = .{
        .llvm_name = "sme-f16f16",
        .description = "Enable SME non-widening Float16 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.sme_f64f64)] = .{
        .llvm_name = "sme-f64f64",
        .description = "Enable Scalable Matrix Extension (SME) F64F64 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sme,
        }),
    };
    result[@intFromEnum(Feature.sme_f8f16)] = .{
        .llvm_name = "sme-f8f16",
        .description = "Enable Scalable Matrix Extension (SME) F8F16 instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.sme_f8f32)] = .{
        .llvm_name = "sme-f8f32",
        .description = "Enable Scalable Matrix Extension (SME) F8F32 instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.sme_fa64)] = .{
        .llvm_name = "sme-fa64",
        .description = "Enable the full A64 instruction set in streaming SVE mode",
        .dependencies = featureSet(&[_]Feature{
            .sme,
            .sve2,
        }),
    };
    result[@intFromEnum(Feature.sme_i16i64)] = .{
        .llvm_name = "sme-i16i64",
        .description = "Enable Scalable Matrix Extension (SME) I16I64 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sme,
        }),
    };
    result[@intFromEnum(Feature.sme_lutv2)] = .{
        .llvm_name = "sme-lutv2",
        .description = "Enable Scalable Matrix Extension (SME) LUTv2 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.sme_mop4)] = .{
        .llvm_name = "sme-mop4",
        .description = "Enable SME Quarter-tile outer product instructions",
        .dependencies = featureSet(&[_]Feature{
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.sme_tmop)] = .{
        .llvm_name = "sme-tmop",
        .description = "Enable SME Structured sparsity outer product instructions.",
        .dependencies = featureSet(&[_]Feature{
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.spe)] = .{
        .llvm_name = "spe",
        .description = "Enable Statistical Profiling extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.spe_eef)] = .{
        .llvm_name = "spe-eef",
        .description = "Enable extra register in the Statistical Profiling Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.specres2)] = .{
        .llvm_name = "specres2",
        .description = "Enable Speculation Restriction Instruction",
        .dependencies = featureSet(&[_]Feature{
            .predres,
        }),
    };
    result[@intFromEnum(Feature.specrestrict)] = .{
        .llvm_name = "specrestrict",
        .description = "Enable architectural speculation restriction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ssbs)] = .{
        .llvm_name = "ssbs",
        .description = "Enable Speculative Store Bypass Safe bit",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ssve_aes)] = .{
        .llvm_name = "ssve-aes",
        .description = "Enable Armv9.6-A SVE AES support in streaming SVE mode",
        .dependencies = featureSet(&[_]Feature{
            .sme2,
            .sve_aes,
        }),
    };
    result[@intFromEnum(Feature.ssve_bitperm)] = .{
        .llvm_name = "ssve-bitperm",
        .description = "Enable Armv9.6-A SVE BitPerm support in streaming SVE mode",
        .dependencies = featureSet(&[_]Feature{
            .sme2,
            .sve_bitperm,
        }),
    };
    result[@intFromEnum(Feature.ssve_fexpa)] = .{
        .llvm_name = "ssve-fexpa",
        .description = "Enable SVE FEXPA instruction in Streaming SVE mode",
        .dependencies = featureSet(&[_]Feature{
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.ssve_fp8dot2)] = .{
        .llvm_name = "ssve-fp8dot2",
        .description = "Enable SVE2 FP8 2-way dot product instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.ssve_fp8dot4)] = .{
        .llvm_name = "ssve-fp8dot4",
        .description = "Enable SVE2 FP8 4-way dot product instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.ssve_fp8fma)] = .{
        .llvm_name = "ssve-fp8fma",
        .description = "Enable SVE2 FP8 multiply-add instructions",
        .dependencies = featureSet(&[_]Feature{
            .fp8,
            .sme2,
        }),
    };
    result[@intFromEnum(Feature.store_pair_suppress)] = .{
        .llvm_name = "store-pair-suppress",
        .description = "Enable Store Pair Suppression heuristics",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.stp_aligned_only)] = .{
        .llvm_name = "stp-aligned-only",
        .description = "In order to emit stp, first check if the store will be aligned to 2 * element_size",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.strict_align)] = .{
        .llvm_name = "strict-align",
        .description = "Disallow all unaligned memory access",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sve)] = .{
        .llvm_name = "sve",
        .description = "Enable Scalable Vector Extension (SVE) instructions",
        .dependencies = featureSet(&[_]Feature{
            .fullfp16,
        }),
    };
    result[@intFromEnum(Feature.sve2)] = .{
        .llvm_name = "sve2",
        .description = "Enable Scalable Vector Extension 2 (SVE2) instructions",
        .dependencies = featureSet(&[_]Feature{
            .sve,
        }),
    };
    result[@intFromEnum(Feature.sve2_aes)] = .{
        .llvm_name = "sve2-aes",
        .description = "Shorthand for +sve2+sve-aes",
        .dependencies = featureSet(&[_]Feature{
            .sve2,
            .sve_aes,
        }),
    };
    result[@intFromEnum(Feature.sve2_bitperm)] = .{
        .llvm_name = "sve2-bitperm",
        .description = "Shorthand for +sve2+sve-bitperm",
        .dependencies = featureSet(&[_]Feature{
            .sve2,
            .sve_bitperm,
        }),
    };
    result[@intFromEnum(Feature.sve2_sha3)] = .{
        .llvm_name = "sve2-sha3",
        .description = "Shorthand for +sve2+sve-sha3",
        .dependencies = featureSet(&[_]Feature{
            .sve2,
            .sve_sha3,
        }),
    };
    result[@intFromEnum(Feature.sve2_sm4)] = .{
        .llvm_name = "sve2-sm4",
        .description = "Shorthand for +sve2+sve-sm4",
        .dependencies = featureSet(&[_]Feature{
            .sve2,
            .sve_sm4,
        }),
    };
    result[@intFromEnum(Feature.sve2p1)] = .{
        .llvm_name = "sve2p1",
        .description = "Enable Scalable Vector Extension 2.1 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sve2,
        }),
    };
    result[@intFromEnum(Feature.sve2p2)] = .{
        .llvm_name = "sve2p2",
        .description = "Enable Armv9.6-A Scalable Vector Extension 2.2 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sve2p1,
        }),
    };
    result[@intFromEnum(Feature.sve_aes)] = .{
        .llvm_name = "sve-aes",
        .description = "Enable SVE AES and quadword SVE polynomial multiply instructions",
        .dependencies = featureSet(&[_]Feature{
            .aes,
        }),
    };
    result[@intFromEnum(Feature.sve_aes2)] = .{
        .llvm_name = "sve-aes2",
        .description = "Enable Armv9.6-A SVE multi-vector AES and multi-vector quadword polynomial multiply instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sve_b16b16)] = .{
        .llvm_name = "sve-b16b16",
        .description = "Enable SVE2 non-widening and SME2 Z-targeting non-widening BFloat16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sve_bfscale)] = .{
        .llvm_name = "sve-bfscale",
        .description = "Enable Armv9.6-A SVE BFloat16 scaling instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sve_bitperm)] = .{
        .llvm_name = "sve-bitperm",
        .description = "Enable bit permutation SVE2 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sve_f16f32mm)] = .{
        .llvm_name = "sve-f16f32mm",
        .description = "Enable Armv9.6-A FP16 to FP32 Matrix Multiply instructions",
        .dependencies = featureSet(&[_]Feature{
            .sve,
        }),
    };
    result[@intFromEnum(Feature.sve_sha3)] = .{
        .llvm_name = "sve-sha3",
        .description = "Enable SVE SHA3 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sha3,
        }),
    };
    result[@intFromEnum(Feature.sve_sm4)] = .{
        .llvm_name = "sve-sm4",
        .description = "Enable SVE SM4 instructions",
        .dependencies = featureSet(&[_]Feature{
            .sm4,
        }),
    };
    result[@intFromEnum(Feature.tagged_globals)] = .{
        .llvm_name = "tagged-globals",
        .description = "Use an instruction sequence for taking the address of a global that allows a memory tag in the upper address bits",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.the)] = .{
        .llvm_name = "the",
        .description = "Enable Armv8.9-A Translation Hardening Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.tlb_rmi)] = .{
        .llvm_name = "tlb-rmi",
        .description = "Enable Armv8.4-A TLB Range and Maintenance instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.tlbiw)] = .{
        .llvm_name = "tlbiw",
        .description = "Enable Armv9.5-A TLBI VMALL for Dirty State",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.tme)] = .{
        .llvm_name = "tme",
        .description = "Enable Transactional Memory Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.tpidr_el1)] = .{
        .llvm_name = "tpidr-el1",
        .description = "Permit use of TPIDR_EL1 for the TLS base",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.tpidr_el2)] = .{
        .llvm_name = "tpidr-el2",
        .description = "Permit use of TPIDR_EL2 for the TLS base",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.tpidr_el3)] = .{
        .llvm_name = "tpidr-el3",
        .description = "Permit use of TPIDR_EL3 for the TLS base",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.tpidrro_el0)] = .{
        .llvm_name = "tpidrro-el0",
        .description = "Permit use of TPIDRRO_EL0 for the TLS base",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.tracev8_4)] = .{
        .llvm_name = "tracev8.4",
        .description = "Enable Armv8.4-A Trace extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.trbe)] = .{
        .llvm_name = "trbe",
        .description = "Enable Trace Buffer Extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.uaops)] = .{
        .llvm_name = "uaops",
        .description = "Enable Armv8.2-A UAO PState",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.use_experimental_zeroing_pseudos)] = .{
        .llvm_name = "use-experimental-zeroing-pseudos",
        .description = "Hint to the compiler that the MOVPRFX instruction is merged with destructive operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.use_fixed_over_scalable_if_equal_cost)] = .{
        .llvm_name = "use-fixed-over-scalable-if-equal-cost",
        .description = "Prefer fixed width loop vectorization over scalable if the cost-model assigns equal costs",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.use_postra_scheduler)] = .{
        .llvm_name = "use-postra-scheduler",
        .description = "Schedule again after register allocation",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.use_reciprocal_square_root)] = .{
        .llvm_name = "use-reciprocal-square-root",
        .description = "Use the reciprocal square root approximation",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.v8_1a)] = .{
        .llvm_name = "v8.1a",
        .description = "Support ARM v8.1a architecture",
        .dependencies = featureSet(&[_]Feature{
            .crc,
            .lor,
            .lse,
            .pan,
            .rdm,
            .v8a,
            .vh,
        }),
    };
    result[@intFromEnum(Feature.v8_2a)] = .{
        .llvm_name = "v8.2a",
        .description = "Support ARM v8.2a architecture",
        .dependencies = featureSet(&[_]Feature{
            .ccpp,
            .pan_rwv,
            .ras,
            .uaops,
            .v8_1a,
        }),
    };
    result[@intFromEnum(Feature.v8_3a)] = .{
        .llvm_name = "v8.3a",
        .description = "Support ARM v8.3a architecture",
        .dependencies = featureSet(&[_]Feature{
            .ccidx,
            .complxnum,
            .jsconv,
            .pauth,
            .rcpc,
            .v8_2a,
        }),
    };
    result[@intFromEnum(Feature.v8_4a)] = .{
        .llvm_name = "v8.4a",
        .description = "Support ARM v8.4a architecture",
        .dependencies = featureSet(&[_]Feature{
            .am,
            .dit,
            .dotprod,
            .flagm,
            .lse2,
            .mpam,
            .nv,
            .rcpc_immo,
            .sel2,
            .tlb_rmi,
            .tracev8_4,
            .v8_3a,
        }),
    };
    result[@intFromEnum(Feature.v8_5a)] = .{
        .llvm_name = "v8.5a",
        .description = "Support ARM v8.5a architecture",
        .dependencies = featureSet(&[_]Feature{
            .altnzcv,
            .bti,
            .ccdp,
            .fptoint,
            .predres,
            .sb,
            .specrestrict,
            .ssbs,
            .v8_4a,
        }),
    };
    result[@intFromEnum(Feature.v8_6a)] = .{
        .llvm_name = "v8.6a",
        .description = "Support ARM v8.6a architecture",
        .dependencies = featureSet(&[_]Feature{
            .amvs,
            .bf16,
            .ecv,
            .fgt,
            .i8mm,
            .v8_5a,
        }),
    };
    result[@intFromEnum(Feature.v8_7a)] = .{
        .llvm_name = "v8.7a",
        .description = "Support ARM v8.7a architecture",
        .dependencies = featureSet(&[_]Feature{
            .hcx,
            .spe_eef,
            .v8_6a,
            .wfxt,
            .xs,
        }),
    };
    result[@intFromEnum(Feature.v8_8a)] = .{
        .llvm_name = "v8.8a",
        .description = "Support ARM v8.8a architecture",
        .dependencies = featureSet(&[_]Feature{
            .hbc,
            .mops,
            .nmi,
            .v8_7a,
        }),
    };
    result[@intFromEnum(Feature.v8_9a)] = .{
        .llvm_name = "v8.9a",
        .description = "Support ARM v8.9a architecture",
        .dependencies = featureSet(&[_]Feature{
            .chk,
            .clrbhb,
            .cssc,
            .prfm_slc_target,
            .rasv2,
            .specres2,
            .v8_8a,
        }),
    };
    result[@intFromEnum(Feature.v8a)] = .{
        .llvm_name = "v8a",
        .description = "Support ARM v8a architecture",
        .dependencies = featureSet(&[_]Feature{
            .el2vmsa,
            .el3,
            .neon,
        }),
    };
    result[@intFromEnum(Feature.v8r)] = .{
        .llvm_name = "v8r",
        .description = "Support ARM v8r architecture",
        .dependencies = featureSet(&[_]Feature{
            .ccidx,
            .ccpp,
            .complxnum,
            .contextidr_el2,
            .crc,
            .dit,
            .dotprod,
            .flagm,
            .fp16fml,
            .jsconv,
            .lse,
            .pan_rwv,
            .pauth,
            .ras,
            .rcpc_immo,
            .rdm,
            .sb,
            .sel2,
            .specrestrict,
            .ssbs,
            .tlb_rmi,
            .tracev8_4,
            .uaops,
        }),
    };
    result[@intFromEnum(Feature.v9_1a)] = .{
        .llvm_name = "v9.1a",
        .description = "Support ARM v9.1a architecture",
        .dependencies = featureSet(&[_]Feature{
            .rme,
            .v8_6a,
            .v9a,
        }),
    };
    result[@intFromEnum(Feature.v9_2a)] = .{
        .llvm_name = "v9.2a",
        .description = "Support ARM v9.2a architecture",
        .dependencies = featureSet(&[_]Feature{
            .mec,
            .v8_7a,
            .v9_1a,
        }),
    };
    result[@intFromEnum(Feature.v9_3a)] = .{
        .llvm_name = "v9.3a",
        .description = "Support ARM v9.3a architecture",
        .dependencies = featureSet(&[_]Feature{
            .v8_8a,
            .v9_2a,
        }),
    };
    result[@intFromEnum(Feature.v9_4a)] = .{
        .llvm_name = "v9.4a",
        .description = "Support ARM v9.4a architecture",
        .dependencies = featureSet(&[_]Feature{
            .sve2p1,
            .v8_9a,
            .v9_3a,
        }),
    };
    result[@intFromEnum(Feature.v9_5a)] = .{
        .llvm_name = "v9.5a",
        .description = "Support ARM v9.5a architecture",
        .dependencies = featureSet(&[_]Feature{
            .cpa,
            .faminmax,
            .lut,
            .v9_4a,
        }),
    };
    result[@intFromEnum(Feature.v9_6a)] = .{
        .llvm_name = "v9.6a",
        .description = "Support ARM v9.6a architecture",
        .dependencies = featureSet(&[_]Feature{
            .cmpbr,
            .lsui,
            .occmo,
            .v9_5a,
        }),
    };
    result[@intFromEnum(Feature.v9a)] = .{
        .llvm_name = "v9a",
        .description = "Support ARM v9a architecture",
        .dependencies = featureSet(&[_]Feature{
            .sve2,
            .v8_5a,
        }),
    };
    result[@intFromEnum(Feature.vh)] = .{
        .llvm_name = "vh",
        .description = "Enable Armv8.1-A Virtual Host extension",
        .dependencies = featureSet(&[_]Feature{
            .contextidr_el2,
        }),
    };
    result[@intFromEnum(Feature.wfxt)] = .{
        .llvm_name = "wfxt",
        .description = "Enable Armv8.7-A WFET and WFIT instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.xs)] = .{
        .llvm_name = "xs",
        .description = "Enable Armv8.7-A limited-TLB-maintenance instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.zcm_fpr32)] = .{
        .llvm_name = "zcm-fpr32",
        .description = "Has zero-cycle register moves for FPR32 registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.zcm_fpr64)] = .{
        .llvm_name = "zcm-fpr64",
        .description = "Has zero-cycle register moves for FPR64 registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.zcm_gpr32)] = .{
        .llvm_name = "zcm-gpr32",
        .description = "Has zero-cycle register moves for GPR32 registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.zcm_gpr64)] = .{
        .llvm_name = "zcm-gpr64",
        .description = "Has zero-cycle register moves for GPR64 registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.zcz)] = .{
        .llvm_name = "zcz",
        .description = "Has zero-cycle zeroing instructions",
        .dependencies = featureSet(&[_]Feature{
            .zcz_gp,
        }),
    };
    result[@intFromEnum(Feature.zcz_fp_workaround)] = .{
        .llvm_name = "zcz-fp-workaround",
        .description = "The zero-cycle floating-point zeroing instruction has a bug",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.zcz_gp)] = .{
        .llvm_name = "zcz-gp",
        .description = "Has zero-cycle zeroing instructions for generic registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    const ti = @typeInfo(Feature);
    for (&result, 0..) |*elem, i| {
        elem.index = i;
        elem.name = ti.@"enum".fields[i].name;
    }
    break :blk result;
};

pub const cpu = struct {
    pub const a64fx: CpuModel = .{
        .name = "a64fx",
        .llvm_name = "a64fx",
        .features = featureSet(&[_]Feature{
            .aes,
            .aggressive_fma,
            .arith_bcc_fusion,
            .complxnum,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .store_pair_suppress,
            .sve,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const ampere1: CpuModel = .{
        .name = "ampere1",
        .llvm_name = "ampere1",
        .features = featureSet(&[_]Feature{
            .aes,
            .aggressive_fma,
            .alu_lsl_fast,
            .arith_bcc_fusion,
            .cmp_bcc_fusion,
            .fullfp16,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_literals,
            .ldp_aligned_only,
            .perfmon,
            .rand,
            .sha3,
            .store_pair_suppress,
            .stp_aligned_only,
            .use_postra_scheduler,
            .v8_6a,
        }),
    };
    pub const ampere1a: CpuModel = .{
        .name = "ampere1a",
        .llvm_name = "ampere1a",
        .features = featureSet(&[_]Feature{
            .aes,
            .aggressive_fma,
            .alu_lsl_fast,
            .arith_bcc_fusion,
            .cmp_bcc_fusion,
            .fullfp16,
            .fuse_address,
            .fuse_addsub_2reg_const1,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_literals,
            .ldp_aligned_only,
            .mte,
            .perfmon,
            .rand,
            .sha3,
            .sm4,
            .store_pair_suppress,
            .stp_aligned_only,
            .use_postra_scheduler,
            .v8_6a,
        }),
    };
    pub const ampere1b: CpuModel = .{
        .name = "ampere1b",
        .llvm_name = "ampere1b",
        .features = featureSet(&[_]Feature{
            .aes,
            .aggressive_fma,
            .alu_lsl_fast,
            .arith_bcc_fusion,
            .cmp_bcc_fusion,
            .cssc,
            .enable_select_opt,
            .fullfp16,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_literals,
            .ldp_aligned_only,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .rand,
            .sha3,
            .sm4,
            .store_pair_suppress,
            .stp_aligned_only,
            .use_postra_scheduler,
            .v8_7a,
        }),
    };
    pub const apple_a10: CpuModel = .{
        .name = "apple_a10",
        .llvm_name = "apple-a10",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .crc,
            .disable_latency_sched_heuristic,
            .fuse_aes,
            .fuse_crypto_eor,
            .lor,
            .pan,
            .perfmon,
            .rdm,
            .sha2,
            .store_pair_suppress,
            .v8a,
            .vh,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_a11: CpuModel = .{
        .name = "apple_a11",
        .llvm_name = "apple-a11",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fullfp16,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha2,
            .store_pair_suppress,
            .v8_2a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_a12: CpuModel = .{
        .name = "apple_a12",
        .llvm_name = "apple-a12",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fullfp16,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha2,
            .store_pair_suppress,
            .v8_3a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_a13: CpuModel = .{
        .name = "apple_a13",
        .llvm_name = "apple-a13",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_4a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_a14: CpuModel = .{
        .name = "apple_a14",
        .llvm_name = "apple-a14",
        .features = featureSet(&[_]Feature{
            .aes,
            .aggressive_fma,
            .alternate_sextload_cvt_f32_pattern,
            .altnzcv,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .ccdp,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fptoint,
            .fuse_address,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .perfmon,
            .predres,
            .sb,
            .sha3,
            .specrestrict,
            .ssbs,
            .store_pair_suppress,
            .v8_4a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_a15: CpuModel = .{
        .name = "apple_a15",
        .llvm_name = "apple-a15",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fpac,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_6a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_a16: CpuModel = .{
        .name = "apple_a16",
        .llvm_name = "apple-a16",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fpac,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .hcx,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_6a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_a17: CpuModel = .{
        .name = "apple_a17",
        .llvm_name = "apple-a17",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fpac,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .hcx,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_6a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_a18: CpuModel = .{
        .name = "apple_a18",
        .llvm_name = "apple-a18",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fpac,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .perfmon,
            .sha3,
            .sme2,
            .sme_f64f64,
            .sme_i16i64,
            .v8_7a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_a7: CpuModel = .{
        .name = "apple_a7",
        .llvm_name = "apple-a7",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha2,
            .store_pair_suppress,
            .v8a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
            .zcz_fp_workaround,
        }),
    };
    pub const apple_a8: CpuModel = .{
        .name = "apple_a8",
        .llvm_name = "apple-a8",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha2,
            .store_pair_suppress,
            .v8a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
            .zcz_fp_workaround,
        }),
    };
    pub const apple_a9: CpuModel = .{
        .name = "apple_a9",
        .llvm_name = "apple-a9",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha2,
            .store_pair_suppress,
            .v8a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
            .zcz_fp_workaround,
        }),
    };
    pub const apple_m1: CpuModel = .{
        .name = "apple_m1",
        .llvm_name = "apple-m1",
        .features = featureSet(&[_]Feature{
            .aes,
            .aggressive_fma,
            .alternate_sextload_cvt_f32_pattern,
            .altnzcv,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .ccdp,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fptoint,
            .fuse_address,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .perfmon,
            .predres,
            .sb,
            .sha3,
            .specrestrict,
            .ssbs,
            .store_pair_suppress,
            .v8_4a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_m2: CpuModel = .{
        .name = "apple_m2",
        .llvm_name = "apple-m2",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fpac,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_6a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_m3: CpuModel = .{
        .name = "apple_m3",
        .llvm_name = "apple-m3",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fpac,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .hcx,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_6a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_m4: CpuModel = .{
        .name = "apple_m4",
        .llvm_name = "apple-m4",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fpac,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .perfmon,
            .sha3,
            .sme2,
            .sme_f64f64,
            .sme_i16i64,
            .v8_7a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_s10: CpuModel = .{
        .name = "apple_s10",
        .llvm_name = "apple-s10",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fpac,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .hcx,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_6a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_s4: CpuModel = .{
        .name = "apple_s4",
        .llvm_name = "apple-s4",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fullfp16,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha2,
            .store_pair_suppress,
            .v8_3a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_s5: CpuModel = .{
        .name = "apple_s5",
        .llvm_name = "apple-s5",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fullfp16,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha2,
            .store_pair_suppress,
            .v8_3a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_s6: CpuModel = .{
        .name = "apple_s6",
        .llvm_name = "apple-s6",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_4a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_s7: CpuModel = .{
        .name = "apple_s7",
        .llvm_name = "apple-s7",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_4a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_s8: CpuModel = .{
        .name = "apple_s8",
        .llvm_name = "apple-s8",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_4a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const apple_s9: CpuModel = .{
        .name = "apple_s9",
        .llvm_name = "apple-s9",
        .features = featureSet(&[_]Feature{
            .aes,
            .alternate_sextload_cvt_f32_pattern,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .disable_latency_sched_heuristic,
            .fp16fml,
            .fpac,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_crypto_eor,
            .fuse_csel,
            .fuse_literals,
            .hcx,
            .perfmon,
            .sha3,
            .store_pair_suppress,
            .v8_6a,
            .zcm_fpr64,
            .zcm_gpr64,
            .zcz,
        }),
    };
    pub const carmel: CpuModel = .{
        .name = "carmel",
        .llvm_name = "carmel",
        .features = featureSet(&[_]Feature{
            .aes,
            .fullfp16,
            .sha2,
            .v8_2a,
        }),
    };
    pub const cobalt_100: CpuModel = .{
        .name = "cobalt_100",
        .llvm_name = "cobalt-100",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .bf16,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .i8mm,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .sve_bitperm,
            .use_postra_scheduler,
            .v9a,
        }),
    };
    pub const cortex_a320: CpuModel = .{
        .name = "cortex_a320",
        .llvm_name = "cortex-a320",
        .features = featureSet(&[_]Feature{
            .a320,
            .ete,
            .fp16fml,
            .mte,
            .perfmon,
            .sve_bitperm,
            .v9_2a,
        }),
    };
    pub const cortex_a34: CpuModel = .{
        .name = "cortex_a34",
        .llvm_name = "cortex-a34",
        .features = featureSet(&[_]Feature{
            .aes,
            .crc,
            .perfmon,
            .sha2,
            .v8a,
        }),
    };
    pub const cortex_a35: CpuModel = .{
        .name = "cortex_a35",
        .llvm_name = "cortex-a35",
        .features = featureSet(&[_]Feature{
            .aes,
            .crc,
            .perfmon,
            .sha2,
            .v8a,
        }),
    };
    pub const cortex_a510: CpuModel = .{
        .name = "cortex_a510",
        .llvm_name = "cortex-a510",
        .features = featureSet(&[_]Feature{
            .bf16,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .i8mm,
            .mte,
            .perfmon,
            .sve_bitperm,
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
            .v9a,
        }),
    };
    pub const cortex_a520: CpuModel = .{
        .name = "cortex_a520",
        .llvm_name = "cortex-a520",
        .features = featureSet(&[_]Feature{
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .mte,
            .perfmon,
            .sve_bitperm,
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const cortex_a520ae: CpuModel = .{
        .name = "cortex_a520ae",
        .llvm_name = "cortex-a520ae",
        .features = featureSet(&[_]Feature{
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .mte,
            .perfmon,
            .sve_bitperm,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const cortex_a53: CpuModel = .{
        .name = "cortex_a53",
        .llvm_name = "cortex-a53",
        .features = featureSet(&[_]Feature{
            .aes,
            .balance_fp_ops,
            .crc,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .sha2,
            .use_postra_scheduler,
            .v8a,
        }),
    };
    pub const cortex_a55: CpuModel = .{
        .name = "cortex_a55",
        .llvm_name = "cortex-a55",
        .features = featureSet(&[_]Feature{
            .aes,
            .dotprod,
            .fullfp16,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .rcpc,
            .sha2,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const cortex_a57: CpuModel = .{
        .name = "cortex_a57",
        .llvm_name = "cortex-a57",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .balance_fp_ops,
            .crc,
            .enable_select_opt,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_literals,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .use_postra_scheduler,
            .v8a,
        }),
    };
    pub const cortex_a65: CpuModel = .{
        .name = "cortex_a65",
        .llvm_name = "cortex-a65",
        .features = featureSet(&[_]Feature{
            .aes,
            .dotprod,
            .enable_select_opt,
            .fullfp16,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_literals,
            .perfmon,
            .predictable_select_expensive,
            .rcp
```
