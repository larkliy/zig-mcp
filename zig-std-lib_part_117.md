```
TYPE plist PUBLIC "-//Apple Computer//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            \\<plist version="1.0">
            \\<dict>
            \\ <key>ProductBuildVersion</key>
            \\ <string>7W98</string>
            \\ <key>ProductCopyright</key>
            \\ <string>Apple Computer, Inc. 1983-2004</string>
            \\ <key>ProductName</key>
            \\ <string>Mac OS X</string>
            \\ <key>ProductUserVisibleVersion</key>
            \\ <string>10.3.9</string>
            \\ <key>ProductVersion</key>
            \\ <string>10.3.9</string>
            \\</dict>
            \\</plist>
            ,
            .{ .major = 10, .minor = 3, .patch = 9 },
        },
        .{
            \\<?xml version="1.0" encoding="UTF-8"?>
            \\<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            \\<plist version="1.0">
            \\<dict>
            \\ <key>ProductBuildVersion</key>
            \\ <string>19G68</string>
            \\ <key>ProductCopyright</key>
            \\ <string>1983-2020 Apple Inc.</string>
            \\ <key>ProductName</key>
            \\ <string>Mac OS X</string>
            \\ <key>ProductUserVisibleVersion</key>
            \\ <string>10.15.6</string>
            \\ <key>ProductVersion</key>
            \\ <string>10.15.6</string>
            \\ <key>iOSSupportVersion</key>
            \\ <string>13.6</string>
            \\</dict>
            \\</plist>
            ,
            .{ .major = 10, .minor = 15, .patch = 6 },
        },
        .{
            \\<?xml version="1.0" encoding="UTF-8"?>
            \\<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            \\<plist version="1.0">
            \\<dict>
            \\ <key>ProductBuildVersion</key>
            \\ <string>20A2408</string>
            \\ <key>ProductCopyright</key>
            \\ <string>1983-2020 Apple Inc.</string>
            \\ <key>ProductName</key>
            \\ <string>macOS</string>
            \\ <key>ProductUserVisibleVersion</key>
            \\ <string>11.0</string>
            \\ <key>ProductVersion</key>
            \\ <string>11.0</string>
            \\ <key>iOSSupportVersion</key>
            \\ <string>14.2</string>
            \\</dict>
            \\</plist>
            ,
            .{ .major = 11, .minor = 0, .patch = 0 },
        },
        .{
            \\<?xml version="1.0" encoding="UTF-8"?>
            \\<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            \\<plist version="1.0">
            \\<dict>
            \\ <key>ProductBuildVersion</key>
            \\ <string>20C63</string>
            \\ <key>ProductCopyright</key>
            \\ <string>1983-2020 Apple Inc.</string>
            \\ <key>ProductName</key>
            \\ <string>macOS</string>
            \\ <key>ProductUserVisibleVersion</key>
            \\ <string>11.1</string>
            \\ <key>ProductVersion</key>
            \\ <string>11.1</string>
            \\ <key>iOSSupportVersion</key>
            \\ <string>14.3</string>
            \\</dict>
            \\</plist>
            ,
            .{ .major = 11, .minor = 1, .patch = 0 },
        },
    };

    inline for (cases) |case| {
        const ver0 = try parseSystemVersion(case[0]);
        const ver1 = case[1];
        try testing.expectEqual(std.math.Order.eq, ver0.order(ver1));
    }
}

pub fn detectNativeCpuAndFeatures() ?Target.Cpu {
    var cpu_family: std.c.CPUFAMILY = undefined;
    var len: usize = @sizeOf(std.c.CPUFAMILY);
    switch (posix.errno(posix.system.sysctlbyname("hw.cpufamily", &cpu_family, &len, null, 0))) {
        .SUCCESS => {},
        .FAULT => unreachable, // segmentation fault
        .PERM => unreachable, // only when setting values,
        .NOMEM => unreachable, // memory already on the stack
        .NOENT => unreachable, // constant, known good value
        else => unreachable,
    }

    const current_arch = builtin.cpu.arch;
    switch (current_arch) {
        .aarch64, .aarch64_be => {
            const model = switch (cpu_family) {
                .ARM_CYCLONE => &Target.aarch64.cpu.apple_a7,
                .ARM_TYPHOON => &Target.aarch64.cpu.apple_a8,
                .ARM_TWISTER => &Target.aarch64.cpu.apple_a9,
                .ARM_HURRICANE => &Target.aarch64.cpu.apple_a10,
                .ARM_MONSOON_MISTRAL => &Target.aarch64.cpu.apple_a11,
                .ARM_VORTEX_TEMPEST => &Target.aarch64.cpu.apple_a12,
                .ARM_LIGHTNING_THUNDER => &Target.aarch64.cpu.apple_a13,
                .ARM_FIRESTORM_ICESTORM => &Target.aarch64.cpu.apple_m1, // a14
                .ARM_BLIZZARD_AVALANCHE => &Target.aarch64.cpu.apple_m2, // a15
                .ARM_EVEREST_SAWTOOTH => &Target.aarch64.cpu.apple_m3, // a16
                .ARM_IBIZA => &Target.aarch64.cpu.apple_m3, // base
                .ARM_PALMA => &Target.aarch64.cpu.apple_m3, // max
                .ARM_LOBOS => &Target.aarch64.cpu.apple_m3, // pro
                .ARM_COLL => &Target.aarch64.cpu.apple_a17, // a17 pro
                .ARM_DONAN => &Target.aarch64.cpu.apple_m4, // base
                .ARM_BRAVA => &Target.aarch64.cpu.apple_m4, // pro/max
                .ARM_TAHITI => &Target.aarch64.cpu.apple_m4, // a18 pro
                .ARM_TUPAI => &Target.aarch64.cpu.apple_m4, // a18
                else => return null,
            };

            return Target.Cpu{
                .arch = current_arch,
                .model = model,
                .features = model.features,
            };
        },
        else => {},
    }

    return null;
}



---
File: /std/zig/system/arm.zig
---

const std = @import("std");
const Target = std.Target;

pub const CoreInfo = struct {
    architecture: u8 = 0,
    implementer: u8 = 0,
    variant: u8 = 0,
    part: u16 = 0,
};

pub const cpu_models = struct {
    // Shorthands to simplify the tables below.
    const A32 = Target.arm.cpu;
    const A64 = Target.aarch64.cpu;

    const E = struct {
        part: u16,
        variant: ?u8 = null, // null if matches any variant
        m32: ?*const Target.Cpu.Model = null,
        m64: ?*const Target.Cpu.Model = null,
    };

    // implementer = 0x41
    const ARM = [_]E{
        E{ .part = 0x926, .m32 = &A32.arm926ej_s },
        E{ .part = 0xb02, .m32 = &A32.mpcore },
        E{ .part = 0xb36, .m32 = &A32.arm1136j_s },
        E{ .part = 0xb56, .m32 = &A32.arm1156t2_s },
        E{ .part = 0xb76, .m32 = &A32.arm1176jz_s },
        E{ .part = 0xc05, .m32 = &A32.cortex_a5 },
        E{ .part = 0xc07, .m32 = &A32.cortex_a7 },
        E{ .part = 0xc08, .m32 = &A32.cortex_a8 },
        E{ .part = 0xc09, .m32 = &A32.cortex_a9 },
        E{ .part = 0xc0d, .m32 = &A32.cortex_a17 },
        E{ .part = 0xc0e, .m32 = &A32.cortex_a17 },
        E{ .part = 0xc0f, .m32 = &A32.cortex_a15 },
        E{ .part = 0xc14, .m32 = &A32.cortex_r4 },
        E{ .part = 0xc15, .m32 = &A32.cortex_r5 },
        E{ .part = 0xc17, .m32 = &A32.cortex_r7 },
        E{ .part = 0xc18, .m32 = &A32.cortex_r8 },
        E{ .part = 0xc20, .m32 = &A32.cortex_m0 },
        E{ .part = 0xc21, .m32 = &A32.cortex_m1 },
        E{ .part = 0xc23, .m32 = &A32.cortex_m3 },
        E{ .part = 0xc24, .m32 = &A32.cortex_m4 },
        E{ .part = 0xc27, .m32 = &A32.cortex_m7 },
        E{ .part = 0xc60, .m32 = &A32.cortex_m0plus },
        E{ .part = 0xd01, .m32 = &A32.cortex_a32 },
        E{ .part = 0xd02, .m64 = &A64.cortex_a34 },
        E{ .part = 0xd03, .m32 = &A32.cortex_a53, .m64 = &A64.cortex_a53 },
        E{ .part = 0xd04, .m32 = &A32.cortex_a35, .m64 = &A64.cortex_a35 },
        E{ .part = 0xd05, .m32 = &A32.cortex_a55, .m64 = &A64.cortex_a55 },
        E{ .part = 0xd06, .m64 = &A64.cortex_a65 },
        E{ .part = 0xd07, .m32 = &A32.cortex_a57, .m64 = &A64.cortex_a57 },
        E{ .part = 0xd08, .m32 = &A32.cortex_a72, .m64 = &A64.cortex_a72 },
        E{ .part = 0xd09, .m32 = &A32.cortex_a73, .m64 = &A64.cortex_a73 },
        E{ .part = 0xd0a, .m32 = &A32.cortex_a75, .m64 = &A64.cortex_a75 },
        E{ .part = 0xd0b, .m32 = &A32.cortex_a76, .m64 = &A64.cortex_a76 },
        E{ .part = 0xd0c, .m32 = &A32.neoverse_n1, .m64 = &A64.neoverse_n1 },
        E{ .part = 0xd0d, .m32 = &A32.cortex_a77, .m64 = &A64.cortex_a77 },
        E{ .part = 0xd0e, .m32 = &A32.cortex_a76ae, .m64 = &A64.cortex_a76ae },
        E{ .part = 0xd13, .m32 = &A32.cortex_r52 },
        E{ .part = 0xd14, .m64 = &A64.cortex_r82ae },
        E{ .part = 0xd15, .m64 = &A64.cortex_r82 },
        E{ .part = 0xd16, .m32 = &A32.cortex_r52plus },
        E{ .part = 0xd20, .m32 = &A32.cortex_m23 },
        E{ .part = 0xd21, .m32 = &A32.cortex_m33 },
        E{ .part = 0xd40, .m32 = &A32.neoverse_v1, .m64 = &A64.neoverse_v1 },
        E{ .part = 0xd41, .m32 = &A32.cortex_a78, .m64 = &A64.cortex_a78 },
        E{ .part = 0xd42, .m32 = &A32.cortex_a78ae, .m64 = &A64.cortex_a78ae },
        E{ .part = 0xd43, .m64 = &A64.cortex_a65ae },
        E{ .part = 0xd44, .m32 = &A32.cortex_x1, .m64 = &A64.cortex_x1 },
        E{ .part = 0xd46, .m64 = &A64.cortex_a510 },
        E{ .part = 0xd47, .m32 = &A32.cortex_a710, .m64 = &A64.cortex_a710 },
        E{ .part = 0xd48, .m64 = &A64.cortex_x2 },
        E{ .part = 0xd49, .m32 = &A32.neoverse_n2, .m64 = &A64.neoverse_n2 },
        E{ .part = 0xd4a, .m64 = &A64.neoverse_e1 },
        E{ .part = 0xd4b, .m32 = &A32.cortex_a78c, .m64 = &A64.cortex_a78c },
        E{ .part = 0xd4c, .m32 = &A32.cortex_x1c, .m64 = &A64.cortex_x1c },
        E{ .part = 0xd4d, .m64 = &A64.cortex_a715 },
        E{ .part = 0xd4e, .m64 = &A64.cortex_x3 },
        E{ .part = 0xd4f, .m64 = &A64.neoverse_v2 },
        E{ .part = 0xd80, .m64 = &A64.cortex_a520 },
        E{ .part = 0xd81, .m64 = &A64.cortex_a720 },
        E{ .part = 0xd82, .m64 = &A64.cortex_x4 },
        E{ .part = 0xd83, .m64 = &A64.neoverse_v3ae },
        E{ .part = 0xd84, .m64 = &A64.neoverse_v3 },
        E{ .part = 0xd85, .m64 = &A64.cortex_x925 },
        E{ .part = 0xd87, .m64 = &A64.cortex_a725 },
        E{ .part = 0xd88, .m64 = &A64.cortex_a520ae },
        E{ .part = 0xd89, .m64 = &A64.cortex_a720ae },
        E{ .part = 0xd8e, .m64 = &A64.neoverse_n3 },
        E{ .part = 0xd8f, .m64 = &A64.cortex_a320 },
    };
    // implementer = 0x42
    const Broadcom = [_]E{
        E{ .part = 0x516, .m64 = &A64.thunderx2t99 },
    };
    // implementer = 0x43
    const Cavium = [_]E{
        E{ .part = 0x0a0, .m64 = &A64.thunderx },
        E{ .part = 0x0a2, .m64 = &A64.thunderxt81 },
        E{ .part = 0x0a3, .m64 = &A64.thunderxt83 },
        E{ .part = 0x0a1, .m64 = &A64.thunderxt88 },
        E{ .part = 0x0af, .m64 = &A64.thunderx2t99 },
    };
    // implementer = 0x46
    const Fujitsu = [_]E{
        E{ .part = 0x001, .m64 = &A64.a64fx },
    };
    // implementer = 0x48
    const HiSilicon = [_]E{
        E{ .part = 0xd01, .m64 = &A64.tsv110 },
    };
    // implementer = 0x4e
    const Nvidia = [_]E{
        E{ .part = 0x004, .m64 = &A64.carmel },
        E{ .part = 0x010, .m64 = &A64.olympus },
    };
    // implementer = 0x50
    const Ampere = [_]E{
        E{ .part = 0x000, .variant = 3, .m64 = &A64.emag },
        E{ .part = 0x000, .m64 = &A64.xgene1 },
    };
    // implementer = 0x51
    const Qualcomm = [_]E{
        E{ .part = 0x001, .m64 = &A64.oryon_1 },
        E{ .part = 0x06f, .m32 = &A32.krait },
        E{ .part = 0x201, .m64 = &A64.kryo, .m32 = &A64.kryo },
        E{ .part = 0x205, .m64 = &A64.kryo, .m32 = &A64.kryo },
        E{ .part = 0x211, .m64 = &A64.kryo, .m32 = &A64.kryo },
        E{ .part = 0x800, .m64 = &A64.cortex_a73, .m32 = &A64.cortex_a73 },
        E{ .part = 0x801, .m64 = &A64.cortex_a73, .m32 = &A64.cortex_a73 },
        E{ .part = 0x802, .m64 = &A64.cortex_a75, .m32 = &A64.cortex_a75 },
        E{ .part = 0x803, .m64 = &A64.cortex_a75, .m32 = &A64.cortex_a75 },
        E{ .part = 0x804, .m64 = &A64.cortex_a76, .m32 = &A64.cortex_a76 },
        E{ .part = 0x805, .m64 = &A64.cortex_a76, .m32 = &A64.cortex_a76 },
        E{ .part = 0xc00, .m64 = &A64.falkor },
        E{ .part = 0xc01, .m64 = &A64.saphira },
    };
    // implementer = 0x61
    const Apple = [_]E{
        E{ .part = 0x022, .m64 = &A64.apple_m1 },
        E{ .part = 0x023, .m64 = &A64.apple_m1 },
        E{ .part = 0x024, .m64 = &A64.apple_m1 },
        E{ .part = 0x025, .m64 = &A64.apple_m1 },
        E{ .part = 0x028, .m64 = &A64.apple_m1 },
        E{ .part = 0x029, .m64 = &A64.apple_m1 },
        E{ .part = 0x032, .m64 = &A64.apple_m2 },
        E{ .part = 0x033, .m64 = &A64.apple_m2 },
        E{ .part = 0x034, .m64 = &A64.apple_m2 },
        E{ .part = 0x035, .m64 = &A64.apple_m2 },
        E{ .part = 0x038, .m64 = &A64.apple_m2 },
        E{ .part = 0x039, .m64 = &A64.apple_m2 },
    };

    pub fn isKnown(core: CoreInfo, is_64bit: bool) ?*const Target.Cpu.Model {
        const models = switch (core.implementer) {
            0x41 => &ARM,
            0x42 => &Broadcom,
            0x43 => &Cavium,
            0x46 => &Fujitsu,
            0x48 => &HiSilicon,
            0x4e => &Nvidia,
            0x50 => &Ampere,
            0x51 => &Qualcomm,
            0x61 => &Apple,
            else => return null,
        };

        for (models) |model| {
            if (model.part == core.part and
                (model.variant == null or model.variant.? == core.variant))
                return if (is_64bit) model.m64 else model.m32;
        }

        return null;
    }
};

pub const aarch64 = struct {
    fn setFeature(cpu: *Target.Cpu, feature: Target.aarch64.Feature, enabled: bool) void {
        const idx = @as(Target.Cpu.Feature.Set.Index, @intFromEnum(feature));

        if (enabled) cpu.features.addFeature(idx) else cpu.features.removeFeature(idx);
    }

    inline fn bitField(input: u64, offset: u6) u4 {
        return @as(u4, @truncate(input >> offset));
    }

    /// Input array should consist of readouts from 12 system registers such that:
    /// 0  -> MIDR_EL1
    /// 1  -> ID_AA64PFR0_EL1
    /// 2  -> ID_AA64PFR1_EL1
    /// 3  -> ID_AA64DFR0_EL1
    /// 4  -> ID_AA64DFR1_EL1
    /// 5  -> ID_AA64AFR0_EL1
    /// 6  -> ID_AA64AFR1_EL1
    /// 7  -> ID_AA64ISAR0_EL1
    /// 8  -> ID_AA64ISAR1_EL1
    /// 9  -> ID_AA64MMFR0_EL1
    /// 10 -> ID_AA64MMFR1_EL1
    /// 11 -> ID_AA64MMFR2_EL1
    pub fn detectNativeCpuAndFeatures(arch: Target.Cpu.Arch, registers: [12]u64) ?Target.Cpu {
        const info = detectNativeCoreInfo(registers[0]);
        const model = cpu_models.isKnown(info, true) orelse return null;

        var cpu = Target.Cpu{
            .arch = arch,
            .model = model,
            .features = Target.Cpu.Feature.Set.empty,
        };

        detectNativeCpuFeatures(&cpu, registers[1..12]);
        addInstructionFusions(&cpu, info);

        return cpu;
    }

    /// Takes readout of MIDR_EL1 register as input.
    fn detectNativeCoreInfo(midr: u64) CoreInfo {
        var info = CoreInfo{
            .implementer = @as(u8, @truncate(midr >> 24)),
            .part = @as(u12, @truncate(midr >> 4)),
        };

        blk: {
            if (info.implementer == 0x41) {
                // ARM Ltd.
                const special_bits: u4 = @truncate(info.part >> 8);
                if (special_bits == 0x0 or special_bits == 0x7) {
                    // TODO Variant and arch encoded differently.
                    break :blk;
                }
            }

            info.variant |= @as(u8, @intCast(@as(u4, @truncate(midr >> 20)))) << 4;
            info.variant |= @as(u4, @truncate(midr));
            info.architecture = @as(u4, @truncate(midr >> 16));
        }

        return info;
    }

    /// Input array should consist of readouts from 11 system registers such that:
    /// 0  -> ID_AA64PFR0_EL1
    /// 1  -> ID_AA64PFR1_EL1
    /// 2  -> ID_AA64DFR0_EL1
    /// 3  -> ID_AA64DFR1_EL1
    /// 4  -> ID_AA64AFR0_EL1
    /// 5  -> ID_AA64AFR1_EL1
    /// 6  -> ID_AA64ISAR0_EL1
    /// 7  -> ID_AA64ISAR1_EL1
    /// 8  -> ID_AA64MMFR0_EL1
    /// 9  -> ID_AA64MMFR1_EL1
    /// 10 -> ID_AA64MMFR2_EL1
    fn detectNativeCpuFeatures(cpu: *Target.Cpu, registers: *const [11]u64) void {
        // ID_AA64PFR0_EL1
        setFeature(cpu, .dit, bitField(registers[0], 48) >= 1);
        setFeature(cpu, .am, bitField(registers[0], 44) >= 1);
        setFeature(cpu, .amvs, bitField(registers[0], 44) >= 2);
        setFeature(cpu, .mpam, bitField(registers[0], 40) >= 1); // MPAM v1.0
        setFeature(cpu, .sel2, bitField(registers[0], 36) >= 1);
        setFeature(cpu, .sve, bitField(registers[0], 32) >= 1);
        setFeature(cpu, .el3, bitField(registers[0], 12) >= 1);
        setFeature(cpu, .ras, bitField(registers[0], 28) >= 1);

        if (bitField(registers[0], 20) < 0xF) blk: {
            if (bitField(registers[0], 16) != bitField(registers[0], 20)) break :blk; // This should never occur

            setFeature(cpu, .neon, true);
            setFeature(cpu, .fp_armv8, true);
            setFeature(cpu, .fullfp16, bitField(registers[0], 20) > 0);
        }

        // ID_AA64PFR1_EL1
        setFeature(cpu, .mpam, bitField(registers[1], 16) > 0 and bitField(registers[0], 40) == 0); // MPAM v0.1
        setFeature(cpu, .mte, bitField(registers[1], 8) >= 1);
        setFeature(cpu, .ssbs, bitField(registers[1], 4) >= 1);
        setFeature(cpu, .bti, bitField(registers[1], 0) >= 1);

        // ID_AA64DFR0_EL1
        setFeature(cpu, .tracev8_4, bitField(registers[2], 40) >= 1);
        setFeature(cpu, .spe, bitField(registers[2], 32) >= 1);
        setFeature(cpu, .perfmon, bitField(registers[2], 8) >= 1 and bitField(registers[2], 8) < 0xF);

        // ID_AA64DFR1_EL1 reserved
        // ID_AA64AFR0_EL1 reserved / implementation defined
        // ID_AA64AFR1_EL1 reserved

        // ID_AA64ISAR0_EL1
        setFeature(cpu, .rand, bitField(registers[6], 60) >= 1);
        setFeature(cpu, .tlb_rmi, bitField(registers[6], 56) >= 1);
        setFeature(cpu, .flagm, bitField(registers[6], 52) >= 1);
        setFeature(cpu, .fp16fml, bitField(registers[6], 48) >= 1);
        setFeature(cpu, .dotprod, bitField(registers[6], 44) >= 1);
        setFeature(cpu, .sm4, bitField(registers[6], 40) >= 1 and bitField(registers[6], 36) >= 1);
        setFeature(cpu, .sha3, bitField(registers[6], 32) >= 1 and bitField(registers[6], 12) >= 2);
        setFeature(cpu, .rdm, bitField(registers[6], 28) >= 1);
        setFeature(cpu, .lse, bitField(registers[6], 20) >= 1);
        setFeature(cpu, .crc, bitField(registers[6], 16) >= 1);
        setFeature(cpu, .sha2, bitField(registers[6], 12) >= 1 and bitField(registers[6], 8) >= 1);
        setFeature(cpu, .aes, bitField(registers[6], 4) >= 1);

        // ID_AA64ISAR1_EL1
        setFeature(cpu, .i8mm, bitField(registers[7], 52) >= 1);
        setFeature(cpu, .bf16, bitField(registers[7], 44) >= 1);
        setFeature(cpu, .predres, bitField(registers[7], 40) >= 1);
        setFeature(cpu, .sb, bitField(registers[7], 36) >= 1);
        setFeature(cpu, .fptoint, bitField(registers[7], 32) >= 1);
        setFeature(cpu, .rcpc, bitField(registers[7], 20) >= 1);
        setFeature(cpu, .rcpc_immo, bitField(registers[7], 20) >= 2);
        setFeature(cpu, .complxnum, bitField(registers[7], 16) >= 1);
        setFeature(cpu, .jsconv, bitField(registers[7], 12) >= 1);
        setFeature(cpu, .pauth, bitField(registers[7], 8) >= 1 or bitField(registers[7], 4) >= 1);
        setFeature(cpu, .ccpp, bitField(registers[7], 0) >= 1);
        setFeature(cpu, .ccdp, bitField(registers[7], 0) >= 2);

        // ID_AA64MMFR0_EL1
        setFeature(cpu, .ecv, bitField(registers[8], 60) >= 1);
        setFeature(cpu, .fgt, bitField(registers[8], 56) >= 1);

        // ID_AA64MMFR1_EL1
        setFeature(cpu, .pan, bitField(registers[9], 20) >= 1);
        setFeature(cpu, .pan_rwv, bitField(registers[9], 20) >= 2);
        setFeature(cpu, .lor, bitField(registers[9], 16) >= 1);
        setFeature(cpu, .vh, bitField(registers[9], 8) >= 1);
        setFeature(cpu, .contextidr_el2, bitField(registers[9], 8) >= 1);

        // ID_AA64MMFR2_EL1
        setFeature(cpu, .nv, bitField(registers[10], 24) >= 1);
        setFeature(cpu, .ccidx, bitField(registers[10], 20) >= 1);
        setFeature(cpu, .uaops, bitField(registers[10], 4) >= 1);
    }

    fn addInstructionFusions(cpu: *Target.Cpu, info: CoreInfo) void {
        switch (info.implementer) {
            0x41 => switch (info.part) {
                0xd4b, 0xd4c => {
                    // According to A78C/X1C Core Software Optimization Guide, CPU fuses certain instructions.
                    setFeature(cpu, .cmp_bcc_fusion, true);
                    setFeature(cpu, .fuse_aes, true);
                },
                else => {},
            },
            else => {},
        }
    }
};



---
File: /std/zig/system/darwin.zig
---

const std = @import("std");
const Io = std.Io;
const mem = std.mem;
const Allocator = std.mem.Allocator;
const Target = std.Target;
const Version = std.SemanticVersion;

pub const macos = @import("darwin/macos.zig");

/// Check if SDK is installed on Darwin without triggering CLT installation popup window.
///
/// Simply invoking `xcrun` will inevitably trigger the CLT installation popup.
/// Therefore, we resort to invoking `xcode-select --print-path` and checking
/// if the status is nonzero.
///
/// stderr from xcode-select is ignored.
///
/// If error.OutOfMemory occurs in Allocator, this function returns null.
pub fn isSdkInstalled(gpa: Allocator, io: Io) bool {
    const result = std.process.run(gpa, io, .{
        .argv = &.{ "xcode-select", "--print-path" },
    }) catch return false;
    defer {
        gpa.free(result.stderr);
        gpa.free(result.stdout);
    }
    return switch (result.term) {
        .exited => |code| if (code == 0) result.stdout.len > 0 else false,
        else => false,
    };
}

/// Detect SDK on Darwin.
/// Calls `xcrun --sdk <target_sdk> --show-sdk-path` which fetches the path to the SDK.
/// Caller owns the memory.
/// stderr from xcrun is ignored.
/// If error.OutOfMemory occurs in Allocator, this function returns null.
pub fn getSdk(gpa: Allocator, io: Io, target: *const Target) ?[]const u8 {
    const is_simulator_abi = target.abi == .simulator;
    const sdk = switch (target.os.tag) {
        .driverkit => "driverkit",
        .ios => if (is_simulator_abi) "iphonesimulator" else "iphoneos",
        .maccatalyst, .macos => "macosx",
        .tvos => if (is_simulator_abi) "appletvsimulator" else "appletvos",
        .visionos => if (is_simulator_abi) "xrsimulator" else "xros",
        .watchos => if (is_simulator_abi) "watchsimulator" else "watchos",
        else => return null,
    };
    const argv = &[_][]const u8{ "xcrun", "--sdk", sdk, "--show-sdk-path" };
    const result = std.process.run(gpa, io, .{ .argv = argv }) catch return null;
    defer {
        gpa.free(result.stderr);
        gpa.free(result.stdout);
    }
    switch (result.term) {
        .exited => |code| if (code != 0) return null,
        else => return null,
    }
    return gpa.dupe(u8, mem.trimEnd(u8, result.stdout, "\r\n")) catch null;
}

test {
    _ = macos;
}



---
File: /std/zig/system/linux.zig
---

const builtin = @import("builtin");

const std = @import("std");
const Io = std.Io;
const mem = std.mem;
const fs = std.fs;
const fmt = std.fmt;
const testing = std.testing;
const Target = std.Target;
const assert = std.debug.assert;

const SparcCpuinfoImpl = struct {
    model: ?*const Target.Cpu.Model = null,

    const cpu_names = .{
        .{ "SuperSparc", &Target.sparc.cpu.supersparc },
        .{ "HyperSparc", &Target.sparc.cpu.hypersparc },
        .{ "SpitFire", &Target.sparc.cpu.ultrasparc },
        .{ "BlackBird", &Target.sparc.cpu.ultrasparc },
        .{ "Sabre", &Target.sparc.cpu.ultrasparc },
        .{ "Hummingbird", &Target.sparc.cpu.ultrasparc },
        .{ "Cheetah", &Target.sparc.cpu.ultrasparc3 },
        .{ "Jalapeno", &Target.sparc.cpu.ultrasparc3 },
        .{ "Jaguar", &Target.sparc.cpu.ultrasparc3 },
        .{ "Panther", &Target.sparc.cpu.ultrasparc3 },
        .{ "Serrano", &Target.sparc.cpu.ultrasparc3 },
        .{ "UltraSparc T1", &Target.sparc.cpu.niagara },
        .{ "UltraSparc T2", &Target.sparc.cpu.niagara2 },
        .{ "UltraSparc T3", &Target.sparc.cpu.niagara3 },
        .{ "UltraSparc T4", &Target.sparc.cpu.niagara4 },
        .{ "UltraSparc T5", &Target.sparc.cpu.niagara4 },
        .{ "LEON", &Target.sparc.cpu.leon3 },
    };

    fn line_hook(self: *SparcCpuinfoImpl, key: []const u8, value: []const u8) !bool {
        if (mem.eql(u8, key, "cpu")) {
            inline for (cpu_names) |pair| {
                if (mem.findPos(u8, value, 0, pair[0]) != null) {
                    self.model = pair[1];
                    break;
                }
            }
        }

        return true;
    }

    fn finalize(self: *const SparcCpuinfoImpl, arch: Target.Cpu.Arch) ?Target.Cpu {
        const model = self.model orelse return null;
        return Target.Cpu{
            .arch = arch,
            .model = model,
            .features = model.features,
        };
    }
};

const SparcCpuinfoParser = CpuinfoParser(SparcCpuinfoImpl);

test "cpuinfo: SPARC" {
    try testParser(SparcCpuinfoParser, .sparc64, &Target.sparc.cpu.niagara2,
        \\cpu             : UltraSparc T2 (Niagara2)
        \\fpu             : UltraSparc T2 integrated FPU
        \\pmu             : niagara2
        \\type            : sun4v
    );
}

const RiscvCpuinfoImpl = struct {
    model: ?*const Target.Cpu.Model = null,

    const cpu_names = .{
        .{ "sifive,u54", &Target.riscv.cpu.sifive_u54 },
        .{ "sifive,u54-mc", &Target.riscv.cpu.sifive_u54 },
        .{ "sifive,u7", &Target.riscv.cpu.sifive_7_series },
        .{ "sifive,u74", &Target.riscv.cpu.sifive_u74 },
        .{ "sifive,u74-mc", &Target.riscv.cpu.sifive_u74 },
        .{ "spacemit,x60", &Target.riscv.cpu.spacemit_x60 },
    };

    fn line_hook(self: *RiscvCpuinfoImpl, key: []const u8, value: []const u8) !bool {
        if (mem.eql(u8, key, "uarch")) {
            inline for (cpu_names) |pair| {
                if (mem.eql(u8, value, pair[0])) {
                    self.model = pair[1];
                    break;
                }
            }
            return false;
        }

        return true;
    }

    fn finalize(self: *const RiscvCpuinfoImpl, arch: Target.Cpu.Arch) ?Target.Cpu {
        const model = self.model orelse return null;
        return Target.Cpu{
            .arch = arch,
            .model = model,
            .features = model.features,
        };
    }
};

const RiscvCpuinfoParser = CpuinfoParser(RiscvCpuinfoImpl);

test "cpuinfo: RISC-V" {
    try testParser(RiscvCpuinfoParser, .riscv64, &Target.riscv.cpu.sifive_u74,
        \\processor : 0
        \\hart      : 1
        \\isa       : rv64imafdc
        \\mmu       : sv39
        \\isa-ext   :
        \\uarch     : sifive,u74-mc
    );
}

const PowerpcCpuinfoImpl = struct {
    model: ?*const Target.Cpu.Model = null,

    const cpu_names = .{
        .{ "604e", &Target.powerpc.cpu.@"604e" },
        .{ "604", &Target.powerpc.cpu.@"604" },
        .{ "7400", &Target.powerpc.cpu.@"7400" },
        .{ "7410", &Target.powerpc.cpu.@"7400" },
        .{ "7447", &Target.powerpc.cpu.@"7400" },
        .{ "7455", &Target.powerpc.cpu.@"7450" },
        .{ "G4", &Target.powerpc.cpu.g4 },
        .{ "POWER4", &Target.powerpc.cpu.@"970" },
        .{ "PPC970FX", &Target.powerpc.cpu.@"970" },
        .{ "PPC970MP", &Target.powerpc.cpu.@"970" },
        .{ "G5", &Target.powerpc.cpu.g5 },
        .{ "POWER5", &Target.powerpc.cpu.g5 },
        .{ "A2", &Target.powerpc.cpu.a2 },
        .{ "POWER6", &Target.powerpc.cpu.pwr6 },
        .{ "POWER7", &Target.powerpc.cpu.pwr7 },
        .{ "POWER8", &Target.powerpc.cpu.pwr8 },
        .{ "POWER8E", &Target.powerpc.cpu.pwr8 },
        .{ "POWER8NVL", &Target.powerpc.cpu.pwr8 },
        .{ "POWER9", &Target.powerpc.cpu.pwr9 },
        .{ "POWER10", &Target.powerpc.cpu.pwr10 },
        .{ "POWER11", &Target.powerpc.cpu.pwr11 },
    };

    fn line_hook(self: *PowerpcCpuinfoImpl, key: []const u8, value: []const u8) !bool {
        if (mem.eql(u8, key, "cpu")) {
            // The model name is often followed by a comma or space and extra
            // info.
            inline for (cpu_names) |pair| {
                const end_index = mem.findAny(u8, value, ", ") orelse value.len;
                if (mem.eql(u8, value[0..end_index], pair[0])) {
                    self.model = pair[1];
                    break;
                }
            }

            // Stop the detection once we've seen the first core.
            return false;
        }

        return true;
    }

    fn finalize(self: *const PowerpcCpuinfoImpl, arch: Target.Cpu.Arch) ?Target.Cpu {
        const model = self.model orelse return null;
        return Target.Cpu{
            .arch = arch,
            .model = model,
            .features = model.features,
        };
    }
};

const PowerpcCpuinfoParser = CpuinfoParser(PowerpcCpuinfoImpl);

test "cpuinfo: PowerPC" {
    try testParser(PowerpcCpuinfoParser, .powerpc, &Target.powerpc.cpu.@"970",
        \\processor : 0
        \\cpu       : PPC970MP, altivec supported
        \\clock     : 1250.000000MHz
        \\revision  : 1.1 (pvr 0044 0101)
    );
    try testParser(PowerpcCpuinfoParser, .powerpc64le, &Target.powerpc.cpu.pwr8,
        \\processor : 0
        \\cpu       : POWER8 (raw), altivec supported
        \\clock     : 2926.000000MHz
        \\revision  : 2.0 (pvr 004d 0200)
    );
}

const S390xCpuinfoImpl = struct {
    model: ?*const Target.Cpu.Model = null,

    const cpu_names = .{
        // z900: 2064, 2066
        // z990: 2084, 2086
        // z9: 2094, 2096

        .{ "2097", &Target.s390x.cpu.z10 },
        .{ "2098", &Target.s390x.cpu.z10 },
        .{ "2817", &Target.s390x.cpu.z196 },
        .{ "2818", &Target.s390x.cpu.z196 },
        .{ "2827", &Target.s390x.cpu.zEC12 },
        .{ "2828", &Target.s390x.cpu.zEC12 },
        .{ "2964", &Target.s390x.cpu.z13 },
        .{ "2965", &Target.s390x.cpu.z13 },
        .{ "3906", &Target.s390x.cpu.z14 },
        .{ "3907", &Target.s390x.cpu.z14 },
        .{ "8561", &Target.s390x.cpu.z15 },
        .{ "8562", &Target.s390x.cpu.z15 },
        .{ "3931", &Target.s390x.cpu.z16 },
        .{ "3932", &Target.s390x.cpu.z16 },
        .{ "9175", &Target.s390x.cpu.z17 },
        .{ "9176", &Target.s390x.cpu.z17 },
    };

    fn line_hook(self: *S390xCpuinfoImpl, key: []const u8, value: []const u8) !bool {
        if (mem.eql(u8, key, "machine")) {
            inline for (cpu_names) |pair| {
                if (mem.eql(u8, value, pair[0])) {
                    self.model = pair[1];
                    break;
                }
            }

            return false;
        }

        return true;
    }

    fn finalize(self: *const S390xCpuinfoImpl, arch: Target.Cpu.Arch) ?Target.Cpu {
        const model = self.model orelse return null;
        return Target.Cpu{
            .arch = arch,
            .model = model,
            .features = model.features,
        };
    }
};

const S390xCpuinfoParser = CpuinfoParser(S390xCpuinfoImpl);

test "cpuinfo: S390x" {
    try testParser(S390xCpuinfoParser, .s390x, &Target.s390x.cpu.z15,
        \\physical id     : 5
        \\core id         : 5
        \\book id         : 5
        \\drawer id       : 5
        \\dedicated       : 0
        \\address         : 5
        \\siblings        : 1
        \\cpu cores       : 1
        \\version         : FF
        \\identification  : 09DD98
        \\machine         : 8561
        \\cpu MHz dynamic : 5200
        \\cpu MHz static  : 5200
    );
}

const ArmCpuinfoImpl = struct {
    const num_cores = 4;

    cores: [num_cores]CoreInfo = undefined,
    core_no: usize = 0,
    have_fields: usize = 0,

    const CoreInfo = struct {
        architecture: u8 = 0,
        implementer: u8 = 0,
        variant: u8 = 0,
        part: u16 = 0,
        is_really_v6: bool = false,
    };

    const cpu_models = @import("arm.zig").cpu_models;

    fn addOne(self: *ArmCpuinfoImpl) void {
        if (self.have_fields == 4 and self.core_no < num_cores) {
            if (self.core_no > 0) {
                // Deduplicate the core info.
                for (self.cores[0..self.core_no]) |it| {
                    if (std.meta.eql(it, self.cores[self.core_no]))
                        return;
                }
            }
            self.core_no += 1;
        }
    }

    fn line_hook(self: *ArmCpuinfoImpl, key: []const u8, value: []const u8) !bool {
        const info = &self.cores[self.core_no];

        if (mem.eql(u8, key, "processor")) {
            // Handle both old-style and new-style cpuinfo formats.
            // The former prints a sequence of "processor: N" lines for each
            // core and then the info for the core that's executing this code(!)
            // while the latter prints the infos for each core right after the
            // "processor" key.
            self.have_fields = 0;
            self.cores[self.core_no] = .{};
        } else if (mem.eql(u8, key, "CPU implementer")) {
            info.implementer = try fmt.parseInt(u8, value, 0);
            self.have_fields += 1;
        } else if (mem.eql(u8, key, "CPU architecture")) {
            // "AArch64" on older kernels.
            info.architecture = if (mem.startsWith(u8, value, "AArch64"))
                8
            else
                try fmt.parseInt(u8, value, 0);
            self.have_fields += 1;
        } else if (mem.eql(u8, key, "CPU variant")) {
            info.variant = try fmt.parseInt(u8, value, 0);
            self.have_fields += 1;
        } else if (mem.eql(u8, key, "CPU part")) {
            info.part = try fmt.parseInt(u16, value, 0);
            self.have_fields += 1;
        } else if (mem.eql(u8, key, "model name")) {
            // ARMv6 cores report "CPU architecture" equal to 7.
            if (mem.find(u8, value, "(v6l)")) |_| {
                info.is_really_v6 = true;
            }
        } else if (mem.eql(u8, key, "CPU revision")) {
            // This field is always the last one for each CPU section.
            _ = self.addOne();
        }

        return true;
    }

    fn finalize(self: *ArmCpuinfoImpl, arch: Target.Cpu.Arch) ?Target.Cpu {
        if (self.core_no == 0) return null;

        const is_64bit = switch (arch) {
            .aarch64, .aarch64_be => true,
            else => false,
        };

        var known_models: [num_cores]?*const Target.Cpu.Model = undefined;
        for (self.cores[0..self.core_no], 0..) |core, i| {
            known_models[i] = cpu_models.isKnown(.{
                .architecture = core.architecture,
                .implementer = core.implementer,
                .variant = core.variant,
                .part = core.part,
            }, is_64bit);
        }

        // XXX We pick the first core on big.LITTLE systems, hopefully the
        // LITTLE one.
        const model = known_models[0] orelse return null;
        return Target.Cpu{
            .arch = arch,
            .model = model,
            .features = model.features,
        };
    }
};

const ArmCpuinfoParser = CpuinfoParser(ArmCpuinfoImpl);

test "cpuinfo: ARM" {
    try testParser(ArmCpuinfoParser, .arm, &Target.arm.cpu.arm1176jz_s,
        \\processor       : 0
        \\model name      : ARMv6-compatible processor rev 7 (v6l)
        \\BogoMIPS        : 997.08
        \\Features        : half thumb fastmult vfp edsp java tls
        \\CPU implementer : 0x41
        \\CPU architecture: 7
        \\CPU variant     : 0x0
        \\CPU part        : 0xb76
        \\CPU revision    : 7
    );
    try testParser(ArmCpuinfoParser, .arm, &Target.arm.cpu.cortex_a7,
        \\processor : 0
        \\model name : ARMv7 Processor rev 3 (v7l)
        \\BogoMIPS : 18.00
        \\Features : half thumb fastmult vfp edsp neon vfpv3 tls vfpv4 idiva idivt vfpd32 lpae
        \\CPU implementer : 0x41
        \\CPU architecture: 7
        \\CPU variant : 0x0
        \\CPU part : 0xc07
        \\CPU revision : 3
        \\
        \\processor : 4
        \\model name : ARMv7 Processor rev 3 (v7l)
        \\BogoMIPS : 90.00
        \\Features : half thumb fastmult vfp edsp neon vfpv3 tls vfpv4 idiva idivt vfpd32 lpae
        \\CPU implementer : 0x41
        \\CPU architecture: 7
        \\CPU variant : 0x2
        \\CPU part : 0xc0f
        \\CPU revision : 3
    );
    try testParser(ArmCpuinfoParser, .aarch64, &Target.aarch64.cpu.cortex_a72,
        \\processor       : 0
        \\BogoMIPS        : 108.00
        \\Features        : fp asimd evtstrm crc32 cpuid
        \\CPU implementer : 0x41
        \\CPU architecture: 8
        \\CPU variant     : 0x0
        \\CPU part        : 0xd08
        \\CPU revision    : 3
    );
}

fn testParser(
    parser: anytype,
    arch: Target.Cpu.Arch,
    expected_model: *const Target.Cpu.Model,
    input: []const u8,
) !void {
    var r: Io.Reader = .fixed(input);
    const result = try parser.parse(arch, &r);
    try testing.expectEqual(expected_model, result.?.model);
    try testing.expect(expected_model.features.eql(result.?.features));
}

// The generic implementation of a /proc/cpuinfo parser.
// For every line it invokes the line_hook method with the key and value strings
// as first and second parameters. Returning false from the hook function stops
// the iteration without raising an error.
// When all the lines have been analyzed the finalize method is called.
fn CpuinfoParser(comptime impl: anytype) type {
    return struct {
        fn parse(arch: Target.Cpu.Arch, reader: *Io.Reader) !?Target.Cpu {
            var obj: impl = .{};
            while (try reader.takeDelimiter('\n')) |line| {
                const colon_pos = mem.findScalar(u8, line, ':') orelse continue;
                const key = mem.trimEnd(u8, line[0..colon_pos], " \t");
                const value = mem.trimStart(u8, line[colon_pos + 1 ..], " \t");
                if (!try obj.line_hook(key, value)) break;
            }
            return obj.finalize(arch);
        }
    };
}

inline fn getAArch64CpuFeature(comptime feat_reg: []const u8) u64 {
    return asm ("mrs %[ret], " ++ feat_reg
        : [ret] "=r" (-> u64),
    );
}

pub fn detectNativeCpuAndFeatures(io: Io) ?Target.Cpu {
    var file = Io.Dir.openFileAbsolute(io, "/proc/cpuinfo", .{}) catch |err| switch (err) {
        else => return null,
    };
    defer file.close(io);

    var buffer: [4096]u8 = undefined; // "flags" lines can get pretty long.
    var file_reader = file.reader(io, &buffer);

    const current_arch = builtin.cpu.arch;
    switch (current_arch) {
        .arm, .armeb, .thumb, .thumbeb => {
            return ArmCpuinfoParser.parse(current_arch, &file_reader.interface) catch null;
        },
        .aarch64, .aarch64_be => {
            const registers = [12]u64{
                getAArch64CpuFeature("MIDR_EL1"),
                getAArch64CpuFeature("ID_AA64PFR0_EL1"),
                getAArch64CpuFeature("ID_AA64PFR1_EL1"),
                getAArch64CpuFeature("ID_AA64DFR0_EL1"),
                getAArch64CpuFeature("ID_AA64DFR1_EL1"),
                getAArch64CpuFeature("ID_AA64AFR0_EL1"),
                getAArch64CpuFeature("ID_AA64AFR1_EL1"),
                getAArch64CpuFeature("ID_AA64ISAR0_EL1"),
                getAArch64CpuFeature("ID_AA64ISAR1_EL1"),
                getAArch64CpuFeature("ID_AA64MMFR0_EL1"),
                getAArch64CpuFeature("ID_AA64MMFR1_EL1"),
                getAArch64CpuFeature("ID_AA64MMFR2_EL1"),
            };

            const core = @import("arm.zig").aarch64.detectNativeCpuAndFeatures(current_arch, registers);
            return core;
        },
        .sparc, .sparc64 => {
            return SparcCpuinfoParser.parse(current_arch, &file_reader.interface) catch null;
        },
        .powerpc, .powerpcle, .powerpc64, .powerpc64le => {
            return PowerpcCpuinfoParser.parse(current_arch, &file_reader.interface) catch null;
        },
        .riscv64, .riscv32 => {
            return RiscvCpuinfoParser.parse(current_arch, &file_reader.interface) catch null;
        },
        .s390x => {
            return S390xCpuinfoParser.parse(current_arch, &file_reader.interface) catch null;
        },
        else => {},
    }

    return null;
}



---
File: /std/zig/system/loongarch.zig
---

const builtin = @import("builtin");
const std = @import("std");

inline fn bit(input: u32, offset: u5) bool {
    return (input >> offset) & 1 != 0;
}

fn setFeature(cpu: *std.Target.Cpu, feature: std.Target.loongarch.Feature, enabled: bool) void {
    const idx = @as(std.Target.Cpu.Feature.Set.Index, @intFromEnum(feature));

    if (enabled) cpu.features.addFeature(idx) else cpu.features.removeFeature(idx);
}

pub fn detectNativeCpuAndFeatures(
    arch: std.Target.Cpu.Arch,
    os: std.Target.Os,
    query: std.Target.Query,
) ?std.Target.Cpu {
    _ = os;
    _ = query;

    var cpu: std.Target.Cpu = .{
        .arch = arch,
        .model = switch (cpucfg(0) & 0xf000) {
            else => return null,
            0xc000 => &std.Target.loongarch.cpu.la464,
            0xd000 => &std.Target.loongarch.cpu.la664,
        },
        .features = .empty,
    };

    cpu.features.addFeatureSet(cpu.model.features);

    const cfg1 = cpucfg(1);
    const cfg2 = cpucfg(2);
    const cfg3 = cpucfg(3);

    setFeature(&cpu, .ual, bit(cfg1, 20));

    const has_fpu = bit(cfg2, 0);
    setFeature(&cpu, .f, has_fpu and bit(cfg2, 1));
    setFeature(&cpu, .d, has_fpu and bit(cfg2, 2));

    setFeature(&cpu, .lsx, bit(cfg2, 6));
    setFeature(&cpu, .lasx, bit(cfg2, 7));
    setFeature(&cpu, .lvz, bit(cfg2, 10));

    setFeature(&cpu, .lbt, bit(cfg2, 18) and bit(cfg2, 19) and bit(cfg2, 20));

    setFeature(&cpu, .frecipe, bit(cfg2, 25));
    setFeature(&cpu, .div32, bit(cfg2, 26));
    setFeature(&cpu, .lam_bh, bit(cfg2, 27));
    setFeature(&cpu, .lamcas, bit(cfg2, 28));
    setFeature(&cpu, .scq, bit(cfg2, 30));

    setFeature(&cpu, .ld_seq_sa, bit(cfg3, 23));

    cpu.features.populateDependencies(cpu.arch.allFeaturesList());

    return cpu;
}

/// This is a workaround for the C backend until zig has the ability to put
/// C code in inline assembly.
extern fn zig_loongarch_cpucfg(word: u32, result: *u32) callconv(.c) void;

fn cpucfg(word: u32) u32 {
    var result: u32 = undefined;

    if (builtin.zig_backend == .stage2_c) {
        zig_loongarch_cpucfg(word, &result);
    } else {
        asm ("cpucfg %[result], %[word]"
            : [result] "=r" (result),
            : [word] "r" (word),
        );
    }

    return result;
}



---
File: /std/zig/system/NativePaths.zig
---

const NativePaths = @This();
const builtin = @import("builtin");

const std = @import("../../std.zig");
const Io = std.Io;
const Allocator = std.mem.Allocator;
const process = std.process;
const mem = std.mem;

arena: Allocator,
include_dirs: std.ArrayList([]const u8) = .empty,
lib_dirs: std.ArrayList([]const u8) = .empty,
framework_dirs: std.ArrayList([]const u8) = .empty,
rpaths: std.ArrayList([]const u8) = .empty,
warnings: std.ArrayList([]const u8) = .empty,

pub fn detect(
    arena: Allocator,
    io: Io,
    native_target: *const std.Target,
    environ_map: *const process.Environ.Map,
) !NativePaths {
    var self: NativePaths = .{ .arena = arena };
    var is_nix = false;

    if (std.zig.EnvVar.NIX_CFLAGS_COMPILE.get(environ_map)) |nix_cflags_compile| {
        is_nix = true;
        var it = mem.tokenizeScalar(u8, nix_cflags_compile, ' ');
        while (true) {
            const word = it.next() orelse break;
            if (mem.eql(u8, word, "-isystem") or mem.eql(u8, word, "-idirafter")) {
                const include_path = it.next() orelse {
                    try self.addWarningFmt("Expected argument after {s} in NIX_CFLAGS_COMPILE", .{word});
                    break;
                };
                try self.addIncludeDir(include_path);
            } else if (mem.eql(u8, word, "-iframework")) {
                const framework_path = it.next() orelse {
                    try self.addWarning("Expected argument after -iframework in NIX_CFLAGS_COMPILE");
                    break;
                };
                try self.addFrameworkDir(framework_path);
            } else if (mem.startsWith(u8, word, "-frandom-seed=") or
                mem.startsWith(u8, word, "-fmacro-prefix-map="))
            {
                // Ignore this argument.
            } else {
                try self.addWarningFmt("Unrecognized C flag from NIX_CFLAGS_COMPILE: {s}", .{word});
            }
        }
    }

    if (std.zig.EnvVar.NIX_LDFLAGS.get(environ_map)) |nix_ldflags| {
        is_nix = true;
        var it = mem.tokenizeScalar(u8, nix_ldflags, ' ');
        while (true) {
            const word = it.next() orelse break;
            if (mem.eql(u8, word, "-rpath")) {
                const rpath = it.next() orelse {
                    try self.addWarning("Expected argument after -rpath in NIX_LDFLAGS");
                    break;
                };
                try self.addRPath(rpath);
            } else if (mem.eql(u8, word, "-L") or mem.eql(u8, word, "-l")) {
                _ = it.next() orelse {
                    try self.addWarning("Expected argument after -L or -l in NIX_LDFLAGS");
                    break;
                };
            } else if (mem.startsWith(u8, word, "-L")) {
                const lib_path = word[2..];
                try self.addLibDir(lib_path);
                try self.addRPath(lib_path);
            } else if (mem.startsWith(u8, word, "-l") or mem.startsWith(u8, word, "-static")) {
                // Ignore this argument.
            } else {
                try self.addWarningFmt("Unrecognized C flag from NIX_LDFLAGS: {s}", .{word});
                break;
            }
        }
    }

    if (std.zig.EnvVar.NIX_CFLAGS_LINK.get(environ_map)) |nix_cflags_link| {
        is_nix = true;
        var it = mem.tokenizeScalar(u8, nix_cflags_link, ' ');
        while (true) {
            const word = it.next() orelse break;
            if (mem.eql(u8, word, "-rpath")) {
                const rpath = it.next() orelse {
                    try self.addWarning("Expected argument after -rpath in NIX_CFLAGS_LINK");
                    break;
                };
                try self.addRPath(rpath);
            } else if (mem.eql(u8, word, "-L") or mem.eql(u8, word, "-l")) {
                _ = it.next() orelse {
                    try self.addWarning("Expected argument after -L or -l in NIX_CFLAGS_LINK");
                    break;
                };
            } else if (mem.startsWith(u8, word, "-L")) {
                const lib_path = word[2..];
                try self.addLibDir(lib_path);
                try self.addRPath(lib_path);
            } else if (mem.startsWith(u8, word, "-l") or mem.startsWith(u8, word, "-static")) {
                // Ignore this argument.
            } else {
                try self.addWarningFmt("Unrecognized C flag from NIX_CFLAGS_LINK: {s}", .{word});
                break;
            }
        }
    }

    if (is_nix) {
        return self;
    }

    // TODO: consider also adding macports paths
    if (builtin.target.os.tag.isDarwin()) {
        if (std.zig.system.darwin.isSdkInstalled(arena, io)) sdk: {
            const sdk = std.zig.system.darwin.getSdk(arena, io, native_target) orelse break :sdk;
            try self.addLibDir(try std.fs.path.join(arena, &.{ sdk, "usr/lib" }));
            try self.addFrameworkDir(try std.fs.path.join(arena, &.{ sdk, "System/Library/Frameworks" }));
            try self.addIncludeDir(try std.fs.path.join(arena, &.{ sdk, "usr/include" }));
        }

        // Check for homebrew paths
        if (std.zig.EnvVar.HOMEBREW_PREFIX.get(environ_map)) |prefix| {
            try self.addLibDir(try std.fs.path.join(arena, &.{ prefix, "/lib" }));
            try self.addIncludeDir(try std.fs.path.join(arena, &.{ prefix, "/include" }));
        }

        return self;
    }

    if (builtin.os.tag == .illumos) {
        try self.addLibDir("/usr/lib/64");
        try self.addLibDir("/usr/local/lib/64");
        try self.addLibDir("/lib/64");

        try self.addIncludeDir("/usr/include");
        try self.addIncludeDir("/usr/local/include");

        return self;
    }

    if (builtin.os.tag == .haiku) {
        try self.addLibDir("/system/non-packaged/lib");
        try self.addLibDir("/system/develop/lib");
        try self.addLibDir("/system/lib");
        return self;
    }

    if (builtin.os.tag != .windows and builtin.os.tag != .wasi) {
        const triple = try native_target.linuxTriple(arena);

        const qual = native_target.ptrBitWidth();

        // TODO: $ ld --verbose | grep SEARCH_DIR
        // the output contains some paths that end with lib64, maybe include them too?
        // TODO: what is the best possible order of things?
        // TODO: some of these are suspect and should only be added on some systems. audit needed.

        try self.addIncludeDir("/usr/local/include");
        try self.addLibDirFmt("/usr/local/lib{d}", .{qual});
        try self.addLibDir("/usr/local/lib");

        try self.addIncludeDirFmt("/usr/include/{s}", .{triple});
        try self.addLibDirFmt("/usr/lib/{s}", .{triple});

        try self.addIncludeDir("/usr/include");
        try self.addLibDirFmt("/lib{d}", .{qual});
        try self.addLibDir("/lib");
        try self.addLibDirFmt("/usr/lib{d}", .{qual});
        try self.addLibDir("/usr/lib");

        // example: on a 64-bit debian-based linux distro, with zlib installed from apt:
        // zlib.h is in /usr/include (added above)
        // libz.so.1 is in /lib/x86_64-linux-gnu (added here)
        try self.addLibDirFmt("/lib/{s}", .{triple});

        // Distros like guix don't use FHS, so they rely on environment
        // variables to search for headers and libraries.
        if (std.zig.EnvVar.C_INCLUDE_PATH.get(environ_map)) |c_include_path| {
            var it = mem.tokenizeScalar(u8, c_include_path, ':');
            while (it.next()) |dir| {
                try self.addIncludeDir(dir);
            }
        }

        if (std.zig.EnvVar.CPLUS_INCLUDE_PATH.get(environ_map)) |cplus_include_path| {
            var it = mem.tokenizeScalar(u8, cplus_include_path, ':');
            while (it.next()) |dir| {
                try self.addIncludeDir(dir);
            }
        }

        if (std.zig.EnvVar.LIBRARY_PATH.get(environ_map)) |library_path| {
            var it = mem.tokenizeScalar(u8, library_path, ':');
            while (it.next()) |dir| {
                try self.addLibDir(dir);
            }
        }
    }

    return self;
}

pub fn addIncludeDir(self: *NativePaths, s: []const u8) !void {
    return self.include_dirs.append(self.arena, s);
}

pub fn addIncludeDirFmt(self: *NativePaths, comptime fmt: []const u8, args: anytype) !void {
    const item = try std.fmt.allocPrint(self.arena, fmt, args);
    try self.include_dirs.append(self.arena, item);
}

pub fn addLibDir(self: *NativePaths, s: []const u8) !void {
    try self.lib_dirs.append(self.arena, s);
}

pub fn addLibDirFmt(self: *NativePaths, comptime fmt: []const u8, args: anytype) !void {
    const item = try std.fmt.allocPrint(self.arena, fmt, args);
    try self.lib_dirs.append(self.arena, item);
}

pub fn addWarning(self: *NativePaths, s: []const u8) !void {
    return self.warnings.append(self.arena, s);
}

pub fn addFrameworkDir(self: *NativePaths, s: []const u8) !void {
    return self.framework_dirs.append(self.arena, s);
}

pub fn addFrameworkDirFmt(self: *NativePaths, comptime fmt: []const u8, args: anytype) !void {
    const item = try std.fmt.allocPrint(self.arena, fmt, args);
    try self.framework_dirs.append(self.arena, item);
}

pub fn addWarningFmt(self: *NativePaths, comptime fmt: []const u8, args: anytype) !void {
    const item = try std.fmt.allocPrint(self.arena, fmt, args);
    try self.warnings.append(self.arena, item);
}

pub fn addRPath(self: *NativePaths, s: []const u8) !void {
    try self.rpaths.append(self.arena, s);
}



---
File: /std/zig/system/windows.zig
---

const std = @import("std");
const builtin = @import("builtin");
const assert = std.debug.assert;
const mem = std.mem;
const Target = std.Target;

pub const WindowsVersion = std.Target.Os.WindowsVersion;
pub const PF = std.os.windows.PF;
pub const REG = std.os.windows.REG;
pub const IsProcessorFeaturePresent = std.os.windows.IsProcessorFeaturePresent;

/// Returns the highest known WindowsVersion deduced from reported runtime information.
/// Discards information about in-between versions we don't differentiate.
pub fn detectRuntimeVersion() WindowsVersion {
    var version_info: std.os.windows.RTL_OSVERSIONINFOW = undefined;
    version_info.dwOSVersionInfoSize = @sizeOf(@TypeOf(version_info));

    switch (std.os.windows.ntdll.RtlGetVersion(&version_info)) {
        .SUCCESS => {},
        else => unreachable,
    }

    // Starting from the system infos build a NTDDI-like version
    // constant whose format is:
    //   B0 B1 B2 B3
    //   `---` `` ``--> Sub-version (Starting from Windows 10 onwards)
    //     \    `--> Service pack (Always zero in the constants defined)
    //      `--> OS version (Major & minor)
    const os_ver: u16 = @as(u16, @intCast(version_info.dwMajorVersion & 0xff)) << 8 |
        @as(u16, @intCast(version_info.dwMinorVersion & 0xff));
    const sp_ver: u8 = 0;
    const sub_ver: u8 = if (os_ver >= 0x0A00) subver: {
        // There's no other way to obtain this info beside
        // checking the build number against a known set of
        // values
        var last_idx: usize = 0;
        for (WindowsVersion.known_win10_build_numbers, 0..) |build, i| {
            if (version_info.dwBuildNumber >= build)
                last_idx = i;
        }
        break :subver @as(u8, @truncate(last_idx));
    } else 0;

    const version: u32 = @as(u32, os_ver) << 16 | @as(u16, sp_ver) << 8 | sub_ver;

    return @as(WindowsVersion, @enumFromInt(version));
}

// Technically, a registry value can be as long as 1MB. However, MS recommends storing
// values larger than 2048 bytes in a file rather than directly in the registry, and since we
// are only accessing a system hive \Registry\Machine, we stick to MS guidelines.
// https://learn.microsoft.com/en-us/windows/win32/sysinfo/registry-element-size-limits
const max_value_len = 2048;

fn getCpuInfoFromRegistry(core: usize, args: anytype) !void {
    const ArgsType = @TypeOf(args);
    const args_type_info = @typeInfo(ArgsType);

    if (args_type_info != .@"struct") {
        @compileError("expected tuple or struct argument, found " ++ @typeName(ArgsType));
    }

    const fields_info = args_type_info.@"struct".fields;

    // Originally, I wanted to issue a single call with a more complex table structure such that we
    // would sequentially visit each CPU#d subkey in the registry and pull the value of interest into
    // a buffer, however, NT seems to be expecting a single buffer per each table meaning we would
    // end up pulling only the last CPU core info, overwriting everything else.
    // If anyone can come up with a solution to this, please do!
    const table_size = 1 + fields_info.len;
    var table: [table_size + 1]std.os.windows.RTL_QUERY_REGISTRY_TABLE = undefined;

    const topkey = std.unicode.utf8ToUtf16LeStringLiteral("\\Registry\\Machine\\HARDWARE\\DESCRIPTION\\System\\CentralProcessor");

    const max_cpu_buf = 4;
    var next_cpu_buf: [max_cpu_buf]u8 = undefined;
    const next_cpu = try std.fmt.bufPrint(&next_cpu_buf, "{d}", .{core});

    var subkey: [max_cpu_buf + 1]u16 = undefined;
    const subkey_len = try std.unicode.utf8ToUtf16Le(&subkey, next_cpu);
    subkey[subkey_len] = 0;

    table[0] = .{
        .QueryRoutine = null,
        .Flags = std.os.windows.RTL_QUERY_REGISTRY_SUBKEY | std.os.windows.RTL_QUERY_REGISTRY_REQUIRED,
        .Name = subkey[0..subkey_len :0],
        .EntryContext = null,
        .DefaultType = .NONE,
        .DefaultData = null,
        .DefaultLength = 0,
    };

    var tmp_bufs: [fields_info.len][max_value_len]u8 align(@alignOf(std.os.windows.UNICODE_STRING)) = undefined;

    inline for (fields_info, 0..) |field, i| {
        const ctx: *anyopaque = blk: {
            switch (@field(args, field.name).value_type) {
                .SZ,
                .EXPAND_SZ,
                .MULTI_SZ,
                => {
                    comptime assert(@sizeOf(std.os.windows.UNICODE_STRING) % 2 == 0);
                    const unicode: *std.os.windows.UNICODE_STRING = @ptrCast(&tmp_bufs[i]);
                    unicode.* = .{
                        .Length = 0,
                        .MaximumLength = max_value_len - @sizeOf(std.os.windows.UNICODE_STRING),
                        .Buffer = @ptrCast(tmp_bufs[i][@sizeOf(std.os.windows.UNICODE_STRING)..]),
                    };
                    break :blk unicode;
                },

                .DWORD,
                .DWORD_BIG_ENDIAN,
                .QWORD,
                => break :blk &tmp_bufs[i],

                else => unreachable,
            }
        };

        var key_buf: [max_value_len / 2 + 1]u16 = undefined;
        const key_len = try std.unicode.utf8ToUtf16Le(&key_buf, @field(args, field.name).key);
        key_buf[key_len] = 0;

        table[i + 1] = .{
            .QueryRoutine = null,
            .Flags = std.os.windows.RTL_QUERY_REGISTRY_DIRECT | std.os.windows.RTL_QUERY_REGISTRY_REQUIRED,
            .Name = key_buf[0..key_len :0],
            .EntryContext = ctx,
            .DefaultType = .NONE,
            .DefaultData = null,
            .DefaultLength = 0,
        };
    }

    // Table sentinel
    table[table_size] = .{
        .QueryRoutine = null,
        .Flags = 0,
        .Name = null,
        .EntryContext = null,
        .DefaultType = .NONE,
        .DefaultData = null,
        .DefaultLength = 0,
    };

    const res = std.os.windows.ntdll.RtlQueryRegistryValues(
        std.os.windows.RTL_REGISTRY_ABSOLUTE,
        topkey,
        &table,
        null,
        null,
    );
    switch (res) {
        .SUCCESS => {
            inline for (fields_info, 0..) |field, i| switch (@field(args, field.name).value_type) {
                .SZ,
                .EXPAND_SZ,
                .MULTI_SZ,
                => {
                    var buf = @field(args, field.name).value_buf;
                    const entry: *const std.os.windows.UNICODE_STRING = @ptrCast(table[i + 1].EntryContext);
                    const len = try std.unicode.utf16LeToUtf8(buf, entry.slice());
                    buf[len] = 0;
                },

                .DWORD,
                .DWORD_BIG_ENDIAN,
                .QWORD,
                => {
                    const entry: [*]const u8 = @ptrCast(table[i + 1].EntryContext);
                    switch (@field(args, field.name).value_type) {
                        .DWORD, .DWORD_BIG_ENDIAN => {
                            @memcpy(@field(args, field.name).value_buf[0..4], entry[0..4]);
                        },
                        .QWORD => {
                            @memcpy(@field(args, field.name).value_buf[0..8], entry[0..8]);
                        },
                        else => unreachable,
                    }
                },

                else => unreachable,
            };
        },
        else => return error.Unexpected,
    }
}

fn setFeature(comptime Feature: type, cpu: *Target.Cpu, feature: Feature, enabled: bool) void {
    const idx = @as(Target.Cpu.Feature.Set.Index, @intFromEnum(feature));

    if (enabled) cpu.features.addFeature(idx) else cpu.features.removeFeature(idx);
}

fn getCpuCount() usize {
    return std.os.windows.peb().NumberOfProcessors;
}

/// If the fine-grained detection of CPU features via Win registry fails,
/// we fallback to a generic CPU model but we override the feature set
/// using `SharedUserData` contents.
/// This is effectively what LLVM does for all ARM chips on Windows.
fn genericCpuAndNativeFeatures(arch: Target.Cpu.Arch) Target.Cpu {
    var cpu = Target.Cpu{
        .arch = arch,
        .model = Target.Cpu.Model.generic(arch),
        .features = Target.Cpu.Feature.Set.empty,
    };

    switch (arch) {
        .aarch64, .aarch64_be => {
            const Feature = Target.aarch64.Feature;

            // Override any features that are either present or absent
            setFeature(Feature, &cpu, .neon, IsProcessorFeaturePresent(PF.ARM_NEON_INSTRUCTIONS_AVAILABLE));
            setFeature(Feature, &cpu, .crc, IsProcessorFeaturePresent(PF.ARM_V8_CRC32_INSTRUCTIONS_AVAILABLE));
            setFeature(Feature, &cpu, .crypto, IsProcessorFeaturePresent(PF.ARM_V8_CRYPTO_INSTRUCTIONS_AVAILABLE));
            setFeature(Feature, &cpu, .lse, IsProcessorFeaturePresent(PF.ARM_V81_ATOMIC_INSTRUCTIONS_AVAILABLE));
            setFeature(Feature, &cpu, .dotprod, IsProcessorFeaturePresent(PF.ARM_V82_DP_INSTRUCTIONS_AVAILABLE));
            setFeature(Feature, &cpu, .jsconv, IsProcessorFeaturePresent(PF.ARM_V83_JSCVT_INSTRUCTIONS_AVAILABLE));
        },
        else => {},
    }

    return cpu;
}

pub fn detectNativeCpuAndFeatures() ?Target.Cpu {
    const current_arch = builtin.cpu.arch;
    const cpu: ?Target.Cpu = switch (current_arch) {
        .aarch64, .aarch64_be => blk: {
            var cores: [128]Target.Cpu = undefined;
            const core_count = getCpuCount();

            if (core_count > cores.len) break :blk null;

            var i: usize = 0;
            while (i < core_count) : (i += 1) {
                // Backing datastore
                var registers: [12]u64 = undefined;

                // Registry key to system ID register mapping
                // CP 4000 -> MIDR_EL1
                // CP 4020 -> ID_AA64PFR0_EL1
                // CP 4021 -> ID_AA64PFR1_EL1
                // CP 4028 -> ID_AA64DFR0_EL1
                // CP 4029 -> ID_AA64DFR1_EL1
                // CP 402C -> ID_AA64AFR0_EL1
                // CP 402D -> ID_AA64AFR1_EL1
                // CP 4030 -> ID_AA64ISAR0_EL1
                // CP 4031 -> ID_AA64ISAR1_EL1
                // CP 4038 -> ID_AA64MMFR0_EL1
                // CP 4039 -> ID_AA64MMFR1_EL1
                // CP 403A -> ID_AA64MMFR2_EL1
                getCpuInfoFromRegistry(i, .{
                    .{ .key = "CP 4000", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[0])) },
                    .{ .key = "CP 4020", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[1])) },
                    .{ .key = "CP 4021", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[2])) },
                    .{ .key = "CP 4028", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[3])) },
                    .{ .key = "CP 4029", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[4])) },
                    .{ .key = "CP 402C", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[5])) },
                    .{ .key = "CP 402D", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[6])) },
                    .{ .key = "CP 4030", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[7])) },
                    .{ .key = "CP 4031", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[8])) },
                    .{ .key = "CP 4038", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[9])) },
                    .{ .key = "CP 4039", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[10])) },
                    .{ .key = "CP 403A", .value_type = REG.ValueType.QWORD, .value_buf = @as(*[8]u8, @ptrCast(&registers[11])) },
                }) catch break :blk null;

                cores[i] = @import("arm.zig").aarch64.detectNativeCpuAndFeatures(current_arch, registers) orelse
                    break :blk null;
            }

            // Pick the first core, usually LITTLE in big.LITTLE architecture.
            break :blk cores[0];
        },
        else => null,
    };
    return cpu orelse genericCpuAndNativeFeatures(current_arch);
}



---
File: /std/zig/system/x86.zig
---

const std = @import("std");
const builtin = @import("builtin");
const Target = std.Target;

/// Only covers EAX for now.
const Xcr0 = packed struct(u32) {
    x87: bool,
    sse: bool,
    avx: bool,
    bndreg: bool,
    bndcsr: bool,
    opmask: bool,
    zmm_hi256: bool,
    hi16_zmm: bool,
    pt: bool,
    pkru: bool,
    pasid: bool,
    cet_u: bool,
    cet_s: bool,
    hdc: bool,
    uintr: bool,
    lbr: bool,
    hwp: bool,
    xtilecfg: bool,
    xtiledata: bool,
    apx: bool,
    _reserved: u12,
};

fn setFeature(cpu: *Target.Cpu, feature: Target.x86.Feature, enabled: bool) void {
    const idx = @as(Target.Cpu.Feature.Set.Index, @intFromEnum(feature));

    if (enabled) cpu.features.addFeature(idx) else cpu.features.removeFeature(idx);
}

inline fn bit(input: u32, offset: u5) bool {
    return (input >> offset) & 1 != 0;
}

inline fn hasMask(input: u32, mask: u32) bool {
    return (input & mask) == mask;
}

pub fn detectNativeCpuAndFeatures(arch: Target.Cpu.Arch, os: Target.Os, query: Target.Query) Target.Cpu {
    _ = query;
    var cpu = Target.Cpu{
        .arch = arch,
        .model = Target.Cpu.Model.generic(arch),
        .features = Target.Cpu.Feature.Set.empty,
    };

    // First we detect features, to use as hints when detecting CPU Model.
    detectNativeFeatures(&cpu, os.tag);

    var leaf = cpuid(0, 0);
    const max_leaf = leaf.eax;
    const vendor = leaf.ebx;

    if (max_leaf > 0) {
        leaf = cpuid(0x1, 0);

        const brand_id = leaf.ebx & 0xff;

        // Detect model and family
        var family = (leaf.eax >> 8) & 0xf;
        var model = (leaf.eax >> 4) & 0xf;
        if (family == 6 or family == 0xf) {
            if (family == 0xf) {
                family += (leaf.eax >> 20) & 0xff;
            }
            model += ((leaf.eax >> 16) & 0xf) << 4;
        }

        // Now we detect the model.
        switch (vendor) {
            0x756e6547 => {
                detectIntelProcessor(&cpu, family, model, brand_id);
            },
            0x68747541 => {
                if (detectAMDProcessor(cpu, family, model)) |m| cpu.model = m;
            },
            else => {},
        }
    }

    // Add the CPU model's feature set into the working set, but then
    // override with actual detected features again.
    cpu.features.addFeatureSet(cpu.model.features);
    detectNativeFeatures(&cpu, os.tag);

    cpu.features.populateDependencies(cpu.arch.allFeaturesList());

    return cpu;
}

fn detectIntelProcessor(cpu: *Target.Cpu, family: u32, model: u32, brand_id: u32) void {
    if (brand_id != 0) {
        return;
    }
    switch (family) {
        3 => {
            cpu.model = &Target.x86.cpu.i386;
            return;
        },
        4 => {
            cpu.model = &Target.x86.cpu.i486;
            return;
        },
        5 => {
            if (cpu.has(.x86, .mmx)) {
                cpu.model = &Target.x86.cpu.pentium_mmx;
                return;
            }
            cpu.model = &Target.x86.cpu.pentium;
            return;
        },
        6 => {
            switch (model) {
                0x01 => {
                    cpu.model = &Target.x86.cpu.pentiumpro;
                    return;
                },
                0x03, 0x05, 0x06 => {
                    cpu.model = &Target.x86.cpu.pentium2;
                    return;
                },
                0x07, 0x08, 0x0a, 0x0b => {
                    cpu.model = &Target.x86.cpu.pentium3;
                    return;
                },
                0x09, 0x0d, 0x15 => {
                    cpu.model = &Target.x86.cpu.pentium_m;
                    return;
                },
                0x0e => {
                    cpu.model = &Target.x86.cpu.yonah;
                    return;
                },
                0x0f, 0x16 => {
                    cpu.model = &Target.x86.cpu.core2;
                    return;
                },
                0x17, 0x1d => {
                    cpu.model = &Target.x86.cpu.penryn;
                    return;
                },
                0x1a, 0x1e, 0x1f, 0x2e => {
                    cpu.model = &Target.x86.cpu.nehalem;
                    return;
                },
                0x25, 0x2c, 0x2f => {
                    cpu.model = &Target.x86.cpu.westmere;
                    return;
                },
                0x2a, 0x2d => {
                    cpu.model = &Target.x86.cpu.sandybridge;
                    return;
                },
                0x3a, 0x3e => {
                    cpu.model = &Target.x86.cpu.ivybridge;
                    return;
                },
                0x3c, 0x3f, 0x45, 0x46 => {
                    cpu.model = &Target.x86.cpu.haswell;
                    return;
                },
                0x3d, 0x47, 0x4f, 0x56 => {
                    cpu.model = &Target.x86.cpu.broadwell;
                    return;
                },
                0x4e, 0x5e, 0x8e, 0x9e, 0xa5, 0xa6 => {
                    cpu.model = &Target.x86.cpu.skylake;
                    return;
                },
                0xa7 => {
                    cpu.model = &Target.x86.cpu.rocketlake;
                    return;
                },
                0x55 => {
                    if (cpu.has(.x86, .avx512bf16)) {
                        cpu.model = &Target.x86.cpu.cooperlake;
                        return;
                    } else if (cpu.has(.x86, .avx512vnni)) {
                        cpu.model = &Target.x86.cpu.cascadelake;
                        return;
                    } else {
                        cpu.model = &Target.x86.cpu.skylake_avx512;
                        return;
                    }
                },
                0x66 => {
                    cpu.model = &Target.x86.cpu.cannonlake;
                    return;
                },
                0x7d, 0x7e => {
                    cpu.model = &Target.x86.cpu.icelake_client;
                    return;
                },
                0x6a, 0x6c => {
                    cpu.model = &Target.x86.cpu.icelake_server;
                    return;
                },
                0x8c, 0x8d => {
                    cpu.model = &Target.x86.cpu.tigerlake;
                    return;
                },
                0x97, 0x9a => {
                    cpu.model = &Target.x86.cpu.alderlake;
                    return;
                },
                0xbe => {
                    cpu.model = &Target.x86.cpu.gracemont;
                    return;
                },
                0xb7, 0xba, 0xbf => {
                    cpu.model = &Target.x86.cpu.raptorlake;
                    return;
                },
                0xaa, 0xac => {
                    cpu.model = &Target.x86.cpu.meteorlake;
                    return;
                },
                0xc5, 0xb5 => {
                    cpu.model = &Target.x86.cpu.arrowlake;
                    return;
                },
                0xc6 => {
                    cpu.model = &Target.x86.cpu.arrowlake_s;
                    return;
                },
                0xbd => {
                    cpu.model = &Target.x86.cpu.lunarlake;
                    return;
                },
                0xcc => {
                    cpu.model = &Target.x86.cpu.pantherlake;
                    return;
                },
                0xad => {
                    cpu.model = &Target.x86.cpu.graniterapids;
                    return;
                },
                0xae => {
                    cpu.model = &Target.x86.cpu.graniterapids_d;
                    return;
                },
                0xcf => {
                    cpu.model = &Target.x86.cpu.emeraldrapids;
                    return;
                },
                0x8f => {
                    cpu.model = &Target.x86.cpu.sapphirerapids;
                    return;
                },
                0x1c, 0x26, 0x27, 0x35, 0x36 => {
                    cpu.model = &Target.x86.cpu.bonnell;
                    return;
                },
                0x37, 0x4a, 0x4d, 0x5a, 0x5d, 0x4c => {
                    cpu.model = &Target.x86.cpu.silvermont;
                    return;
                },
                0x5c, 0x5f => {
                    cpu.model = &Target.x86.cpu.goldmont;
                    return;
                },
                0x7a => {
                    cpu.model = &Target.x86.cpu.goldmont_plus;
                    return;
                },
                0x86, 0x8a, 0x96, 0x9c => {
                    cpu.model = &Target.x86.cpu.tremont;
                    return;
                },
                0xaf => {
                    cpu.model = &Target.x86.cpu.sierraforest;
                    return;
                },
                0xb6 => {
                    cpu.model = &Target.x86.cpu.grandridge;
                    return;
                },
                0xdd => {
                    cpu.model = &Target.x86.cpu.clearwaterforest;
                    return;
                },
                0x57 => {
                    cpu.model = &Target.x86.cpu.knl;
                    return;
                },
                0x85 => {
                    cpu.model = &Target.x86.cpu.knm;
                    return;
                },
                else => return, // Unknown CPU Model
            }
        },
        15 => {
            if (cpu.has(.x86, .@"64bit")) {
                cpu.model = &Target.x86.cpu.nocona;
                return;
            }
            if (cpu.has(.x86, .sse3)) {
                cpu.model = &Target.x86.cpu.prescott;
                return;
            }
            cpu.model = &Target.x86.cpu.pentium4;
            return;
        },
        else => return, // Unknown CPU Model
    }
}

fn detectAMDProcessor(cpu: Target.Cpu, family: u32, model: u32) ?*const Target.Cpu.Model {
    return switch (family) {
        4 => &Target.x86.cpu.i486,
        5 => switch (model) {
            6, 7 => &Target.x86.cpu.k6,
            8 => &Target.x86.cpu.k6_2,
            9, 13 => &Target.x86.cpu.k6_3,
            10 => &Target.x86.cpu.geode,
            else => &Target.x86.cpu.pentium,
        },
        6 => if (cpu.has(.x86, .sse))
            &Target.x86.cpu.athlon_xp
        else
            &Target.x86.cpu.athlon,
        15 => if (cpu.has(.x86, .sse3))
            &Target.x86.cpu.k8_sse3
        else
            &Target.x86.cpu.k8,
        16, 18 => &Target.x86.cpu.amdfam10,
        20 => &Target.x86.cpu.btver1,
        21 => switch (model) {
            0x60...0x7f => &Target.x86.cpu.bdver4,
            0x30...0x3f => &Target.x86.cpu.bdver3,
            0x02, 0x10...0x1f => &Target.x86.cpu.bdver2,
            else => &Target.x86.cpu.bdver1,
        },
        22 => &Target.x86.cpu.btver2,
        23 => switch (model) {
            0x30...0x3f, 0x47, 0x60...0x6f, 0x70...0x7f, 0x84...0x87, 0x90...0x9f, 0xa0...0xaf => &Target.x86.cpu.znver2,
            else => &Target.x86.cpu.znver1,
        },
        25 => switch (model) {
            0x10...0x1f, 0x60...0x6f, 0x70...0x7f, 0xa0...0xaf => &Target.x86.cpu.znver4,
            else => &Target.x86.cpu.znver3,
        },
        26 => &Target.x86.cpu.znver5,
        else => null,
    };
}

fn detectNativeFeatures(cpu: *Target.Cpu, os_tag: Target.Os.Tag) void {
    var leaf = cpuid(0, 0);

    const max_level = leaf.eax;

    leaf = cpuid(1, 0);

    setFeature(cpu, .sse3, bit(leaf.ecx, 0));
    setFeature(cpu, .pclmul, bit(leaf.ecx, 1));
    setFeature(cpu, .ssse3, bit(leaf.ecx, 9));
    setFeature(cpu, .cx16, bit(leaf.ecx, 13));
    setFeature(cpu, .sse4_1, bit(leaf.ecx, 19));
    setFeature(cpu, .sse4_2, bit(leaf.ecx, 20));
    setFeature(cpu, .movbe, bit(leaf.ecx, 22));
    setFeature(cpu, .popcnt, bit(leaf.ecx, 23));
    setFeature(cpu, .aes, bit(leaf.ecx, 25));
    setFeature(cpu, .rdrnd, bit(leaf.ecx, 30));

    setFeature(cpu, .cx8, bit(leaf.edx, 8));
    setFeature(cpu, .cmov, bit(leaf.edx, 15));
    setFeature(cpu, .mmx, bit(leaf.edx, 23));
    setFeature(cpu, .fxsr, bit(leaf.edx, 24));
    setFeature(cpu, .sse, bit(leaf.edx, 25));
    setFeature(cpu, .sse2, bit(leaf.edx, 26));

    const has_xsave = bit(leaf.ecx, 27);
    const has_avx = bit(leaf.ecx, 28);

    // Make sure not to call xgetbv if xsave is not supported
    const xcr0: Xcr0 = if (has_xsave and has_avx) @bitCast(getXCR0()) else @bitCast(@as(u32, 0));

    const has_avx_save = xcr0.sse and xcr0.avx;

    // LLVM approaches avx512_save by hardcoding it to true on Darwin,
    // because the kernel saves the context even if the bit is not set.
    // https://github.com/llvm/llvm-project/blob/bca373f73fc82728a8335e7d6cd164e8747139ec/llvm/lib/Support/Host.cpp#L1378
    //
    // Google approaches this by using a different series of checks and flags,
    // and this may report the feature more accurately on a technically correct
    // but ultimately less useful level.
    // https://github.com/google/cpu_features/blob/b5c271c53759b2b15ff91df19bd0b32f2966e275/src/cpuinfo_x86.c#L113
    // (called from https://github.com/google/cpu_features/blob/b5c271c53759b2b15ff91df19bd0b32f2966e275/src/cpuinfo_x86.c#L1052)
    //
    // Right now, we use LLVM's approach, because even if the target doesn't support
    // the feature, the kernel should provide the same functionality transparently,
    // so the implementation details don't make a difference.
    // That said, this flag impacts other CPU features' availability,
    // so until we can verify that this doesn't come with side affects,
    // we'll say TODO verify this.

    // Darwin lazily saves the AVX512 context on first use: trust that the OS will
    // save the AVX512 context if we use AVX512 instructions, even if the bit is not
    // set right now.
    const has_avx512_save = if (os_tag.isDarwin())
        true
    else
        xcr0.zmm_hi256 and xcr0.hi16_zmm;

    // AMX requires additional context to be saved by the OS.
    const has_amx_save = xcr0.xtilecfg and xcr0.xtiledata;

    setFeature(cpu, .avx, has_avx_save);
    setFeature(cpu, .fma, bit(leaf.ecx, 12) and has_avx_save);
    // Only enable XSAVE if OS has enabled support for saving YMM state.
    setFeature(cpu, .xsave, bit(leaf.ecx, 26) and has_avx_save);
    setFeature(cpu, .f16c, bit(leaf.ecx, 29) and has_avx_save);

    leaf = cpuid(0x80000000, 0);
    const max_ext_level = leaf.eax;

    if (max_ext_level >= 0x80000001) {
        leaf = cpuid(0x80000001, 0);

        setFeature(cpu, .sahf, bit(leaf.ecx, 0));
        setFeature(cpu, .lzcnt, bit(leaf.ecx, 5));
        setFeature(cpu, .sse4a, bit(leaf.ecx, 6));
        setFeature(cpu, .prfchw, bit(leaf.ecx, 8));
        setFeature(cpu, .xop, bit(leaf.ecx, 11) and has_avx_save);
        setFeature(cpu, .lwp, bit(leaf.ecx, 15));
        setFeature(cpu, .fma4, bit(leaf.ecx, 16) and has_avx_save);
        setFeature(cpu, .tbm, bit(leaf.ecx, 21));
        setFeature(cpu, .mwaitx, bit(leaf.ecx, 29));

        setFeature(cpu, .@"64bit", bit(leaf.edx, 29));
    } else {
        for ([_]Target.x86.Feature{
            .sahf,
            .lzcnt,
            .sse4a,
            .prfchw,
            .xop,
            .lwp,
            .fma4,
            .tbm,
            .mwaitx,

            .@"64bit",
        }) |feat| {
            setFeature(cpu, feat, false);
        }
    }

    // Misc. memory-related features.
    if (max_ext_level >= 0x80000008) {
        leaf = cpuid(0x80000008, 0);

        setFeature(cpu, .clzero, bit(leaf.ebx, 0));
        setFeature(cpu, .rdpru, bit(leaf.ebx, 4));
        setFeature(cpu, .wbnoinvd, bit(leaf.ebx, 9));
    } else {
        for ([_]Target.x86.Feature{
            .clzero,
            .rdpru,
            .wbnoinvd,
        }) |feat| {
            setFeature(cpu, feat, false);
        }
    }

    if (max_level >= 0x7) {
        leaf = cpuid(0x7, 0);

        setFeature(cpu, .fsgsbase, bit(leaf.ebx, 0));
        setFeature(cpu, .sgx, bit(leaf.ebx, 2));
        setFeature(cpu, .bmi, bit(leaf.ebx, 3));
        // AVX2 is only supported if we have the OS save support from AVX.
        setFeature(cpu, .avx2, bit(leaf.ebx, 5) and has_avx_save);
        setFeature(cpu, .smep, bit(leaf.ebx, 7));
        setFeature(cpu, .bmi2, bit(leaf.ebx, 8));
        setFeature(cpu, .invpcid, bit(leaf.ebx, 10));
        setFeature(cpu, .rtm, bit(leaf.ebx, 11));
        // AVX512 is only supported if the OS supports the context save for it.
        setFeature(cpu, .avx512f, bit(leaf.ebx, 16) and has_avx512_save);
        setFeature(cpu, .evex512, bit(leaf.ebx, 16) and has_avx512_save);
        setFeature(cpu, .avx512dq, bit(leaf.ebx, 17) and has_avx512_save);
        setFeature(cpu, .rdseed, bit(leaf.ebx, 18));
        setFeature(cpu, .adx, bit(leaf.ebx, 19));
        setFeature(cpu, .smap, bit(leaf.ebx, 20));
        setFeature(cpu, .avx512ifma, bit(leaf.ebx, 21) and has_avx512_save);
        setFeature(cpu, .clflushopt, bit(leaf.ebx, 23));
        setFeature(cpu, .clwb, bit(leaf.ebx, 24));
        setFeature(cpu, .avx512pf, bit(leaf.ebx, 26) and has_avx512_save);
        setFeature(cpu, .avx512er, bit(leaf.ebx, 27) and has_avx512_save);
        setFeature(cpu, .avx512cd, bit(leaf.ebx, 28) and has_avx512_save);
        setFeature(cpu, .sha, bit(leaf.ebx, 29));
        setFeature(cpu, .avx512bw, bit(leaf.ebx, 30) and has_avx512_save);
        setFeature(cpu, .avx512vl, bit(leaf.ebx, 31) and has_avx512_save);

        setFeature(cpu, .prefetchwt1, bit(leaf.ecx, 0));
        setFeature(cpu, .avx512vbmi, bit(leaf.ecx, 1) and has_avx512_save);
        setFeature(cpu, .pku, bit(leaf.ecx, 4));
        setFeature(cpu, .waitpkg, bit(leaf.ecx, 5));
        setFeature(cpu, .avx512vbmi2, bit(leaf.ecx, 6) and has_avx512_save);
        setFeature(cpu, .shstk, bit(leaf.ecx, 7));
        setFeature(cpu, .gfni, bit(leaf.ecx, 8));
        setFeature(cpu, .vaes, bit(leaf.ecx, 9) and has_avx_save);
        setFeature(cpu, .vpclmulqdq, bit(leaf.ecx, 10) and has_avx_save);
        setFeature(cpu, .avx512vnni, bit(leaf.ecx, 11) and has_avx512_save);
        setFeature(cpu, .avx512bitalg, bit(leaf.ecx, 12) and has_avx512_save);
        setFeature(cpu, .avx512vpopcntdq, bit(leaf.ecx, 14) and has_avx512_save);
        setFeature(cpu, .rdpid, bit(leaf.ecx, 22));
        setFeature(cpu, .kl, bit(leaf.ecx, 23));
        setFeature(cpu, .cldemote, bit(leaf.ecx, 25));
        setFeature(cpu, .movdiri, bit(leaf.ecx, 27));
        setFeature(cpu, .movdir64b, bit(leaf.ecx, 28));
        setFeature(cpu, .enqcmd, bit(leaf.ecx, 29));

        // There are two CPUID leafs which information associated with the pconfig
        // instruction:
        // EAX=0x7, ECX=0x0 indicates the availability of the instruction (via the 18th
        // bit of EDX), while the EAX=0x1b leaf returns information on the
        // availability of specific pconfig leafs.
        // The target feature here only refers to the the first of these two.
        // Users might need to check for the availability of specific pconfig
        // leaves using cpuid, since that information is ignored while
        // detecting features using the "-march=native" flag.
        // For more info, see X86 ISA docs.
        setFeature(cpu, .uintr, bit(leaf.edx, 5));
        setFeature(cpu, .avx512vp2intersect, bit(leaf.edx, 8) and has_avx512_save);
        setFeature(cpu, .serialize, bit(leaf.edx, 14));
        setFeature(cpu, .tsxldtrk, bit(leaf.edx, 16));
        setFeature(cpu, .pconfig, bit(leaf.edx, 18));
        setFeature(cpu, .amx_bf16, bit(leaf.edx, 22) and has_amx_save);
        setFeature(cpu, .avx512fp16, bit(leaf.edx, 23) and has_avx512_save);
        setFeature(cpu, .amx_tile, bit(leaf.edx, 24) and has_amx_save);
        setFeature(cpu, .amx_int8, bit(leaf.edx, 25) and has_amx_save);

        if (leaf.eax >= 1) {
            leaf = cpuid(0x7, 0x1);

            setFeature(cpu, .sha512, bit(leaf.eax, 0));
            setFeature(cpu, .sm3, bit(leaf.eax, 1));
            setFeature(cpu, .sm4, bit(leaf.eax, 2));
            setFeature(cpu, .raoint, bit(leaf.eax, 3));
            setFeature(cpu, .avxvnni, bit(leaf.eax, 4) and has_avx_save);
            setFeature(cpu, .avx512bf16, bit(leaf.eax, 5) and has_avx512_save);
            setFeature(cpu, .cmpccxadd, bit(leaf.eax, 7));
            setFeature(cpu, .amx_fp16, bit(leaf.eax, 21) and has_amx_save);
            setFeature(cpu, .hreset, bit(leaf.eax, 22));
            setFeature(cpu, .avxifma, bit(leaf.eax, 23) and has_avx_save);

            setFeature(cpu, .avxvnniint8, bit(leaf.edx, 4) and has_avx_save);
            setFeature(cpu, .avxneconvert, bit(leaf.edx, 5) and has_avx_save);
            setFeature(cpu, .amx_complex, bit(leaf.edx, 8) and has_amx_save);
            setFeature(cpu, .avxvnniint16, bit(leaf.edx, 10) and has_avx_save);
            setFeature(cpu, .prefetchi, bit(leaf.edx, 14));
            setFeature(cpu, .usermsr, bit(leaf.edx, 15));
            // APX
            setFeature(cpu, .egpr, bit(leaf.edx, 21));
            setFeature(cpu, .push2pop2, bit(leaf.edx, 21));
            setFeature(cpu, .ppx, bit(leaf.edx, 21));
            setFeature(cpu, .ndd, bit(leaf.edx, 21));
            setFeature(cpu, .ccmp, bit(leaf.edx, 21));
            setFeature(cpu, .cf, bit(leaf.edx, 21));
        } else {
            for ([_]Target.x86.Feature{
                .sha512,
                .sm3,
                .sm4,
                .raoint,
                .avxvnni,
                .avx512bf16,
                .cmpccxadd,
                .amx_fp16,
                .hreset,
                .avxifma,

                .avxvnniint8,
                .avxneconvert,
                .amx_complex,
                .avxvnniint16,
                .prefetchi,
                .usermsr,
                .egpr,
                .push2pop2,
                .ppx,
                .ndd,
                .ccmp,
                .cf,
            }) |feat| {
                setFeature(cpu, feat, false);
            }
        }
    } else {
        for ([_]Target.x86.Feature{
            .fsgsbase,
            .sgx,
            .bmi,
            .avx2,
            .smep,
            .bmi2,
            .invpcid,
            .rtm,
            .avx512f,
            .evex512,
            .avx512dq,
            .rdseed,
            .adx,
            .smap,
            .avx512ifma,
            .clflushopt,
            .clwb,
            .avx512pf,
            .avx512er,
            .avx512cd,
            .sha,
            .avx512bw,
            .avx512vl,

            .prefetchwt1,
            .avx512vbmi,
            .pku,
            .waitpkg,
            .avx512vbmi2,
            .shstk,
            .gfni,
            .vaes,
            .vpclmulqdq,
            .avx512vnni,
            .avx512bitalg,
            .avx512vpopcntdq,
            .rdpid,
            .kl,
            .cldemote,
            .movdiri,
            .movdir64b,
            .enqcmd,

            .uintr,
            .avx512vp2intersect,
            .serialize,
            .tsxldtrk,
            .pconfig,
            .amx_bf16,
            .avx512fp16,
            .amx_tile,
            .amx_int8,

            .sha512,
            .sm3,
            .sm4,
            .raoint,
            .avxvnni,
            .avx512bf16,
            .cmpccxadd,
            .amx_fp16,
            .hreset,
            .avxifma,

            .avxvnniint8,
            .avxneconvert,
            .amx_complex,
            .avxvnniint16,
            .prefetchi,
            .usermsr,
            .egpr,
            .push2pop2,
            .ppx,
            .ndd,
            .ccmp,
            .cf,
        }) |feat| {
            setFeature(cpu, feat, false);
        }
    }

    if (max_level >= 0xD and has_avx_save) {
        leaf = cpuid(0xD, 0x1);

        // Only enable XSAVE if OS has enabled support for saving YMM state.
        setFeature(cpu, .xsaveopt, bit(leaf.eax, 0));
        setFeature(cpu, .xsavec, bit(leaf.eax, 1));
        setFeature(cpu, .xsaves, bit(leaf.eax, 3));
    } else {
        for ([_]Target.x86.Feature{
            .xsaveopt,
            .xsavec,
            .xsaves,
        }) |feat| {
            setFeature(cpu, feat, false);
        }
    }

    if (max_level >= 0x14) {
        leaf = cpuid(0x14, 0);

        setFeature(cpu, .ptwrite, bit(leaf.ebx, 4));
    } else {
        for ([_]Target.x86.Feature{
            .ptwrite,
        }) |feat| {
            setFeature(cpu, feat, false);
        }
    }

    if (max_level >= 0x19) {
        leaf = cpuid(0x19, 0);

        setFeature(cpu, .widekl, bit(leaf.ebx, 2));
    } else {
        for ([_]Target.x86.Feature{
            .widekl,
        }) |feat| {
            setFeature(cpu, feat, false);
        }
    }

    if (max_level >= 0x24) {
        leaf = cpuid(0x24, 0);

        setFeature(cpu, .avx10_1, bit(leaf.ebx, 18));
    } else {
        for ([_]Target.x86.Feature{
            .avx10_1,
        }) |feat| {
            setFeature(cpu, feat, false);
        }
    }
}

const CpuidLeaf = packed struct {
    eax: u32,
    ebx: u32,
    ecx: u32,
    edx: u32,
};

/// This is a workaround for the C backend until zig has the ability to put
/// C code in inline assembly.
extern fn zig_x86_cpuid(leaf_id: u32, subid: u32, eax: *u32, ebx: *u32, ecx: *u32, edx: *u32) callconv(.c) void;

fn cpuid(leaf_id: u32, subid: u32) CpuidLeaf {
    // valid for both x86 and x86_64
    var eax: u32 = undefined;
    var ebx: u32 = undefined;
    var ecx: u32 = undefined;
    var edx: u32 = undefined;

    if (builtin.zig_backend == .stage2_c) {
        zig_x86_cpuid(leaf_id, subid, &eax, &ebx, &ecx, &edx);
    } else {
        asm volatile ("cpuid"
            : [_] "={eax}" (eax),
              [_] "={ebx}" (ebx),
              [_] "={ecx}" (ecx),
              [_] "={edx}" (edx),
            : [_] "{eax}" (leaf_id),
              [_] "{ecx}" (subid),
        );
    }

    return .{ .eax = eax, .ebx = ebx, .ecx = ecx, .edx = edx };
}

/// This is a workaround for the C backend until zig has the ability to put
/// C code in inline assembly.
extern fn zig_x86_get_xcr0() callconv(.c) u32;

// Read control register 0 (XCR0). Used to detect features such as AVX.
fn getXCR0() u32 {
    if (builtin.zig_backend == .stage2_c) {
        return zig_x86_get_xcr0();
    }

    return asm volatile (
        \\ xor %%ecx, %%ecx
        \\ xgetbv
        : [_] "={eax}" (-> u32),
        :
        : .{ .edx = true, .ecx = true });
}



---
File: /std/zig/Ast.zig
---

//! Abstract Syntax Tree for Zig source code.
//! For Zig syntax, the root node is at nodes[0] and contains the list of
//! sub-nodes.
//! For Zon syntax, the root node is at nodes[0] and contains lhs as the node
//! index of the main expression.

const std = @import("../std.zig");
const assert = std.debug.assert;
const testing = std.testing;
const mem = std.mem;
const Token = std.zig.Token;
const Ast = @This();
const Allocator = std.mem.Allocator;
const Parse = @import("Parse.zig");
const Writer = std.Io.Writer;

/// Reference to externally-owned data.
source: [:0]const u8,

tokens: TokenList.Slice,
nodes: NodeList.Slice,
extra_data: []u32,
mode: Mode = .zig,

errors: []const Error,

pub const ByteOffset = u32;

pub const TokenList = std.MultiArrayList(struct {
    tag: Token.Tag,
    start: ByteOffset,
});
pub const NodeList = std.MultiArrayList(Node);

/// Index into `tokens`.
pub const TokenIndex = u32;

/// Index into `tokens`, or null.
pub const OptionalTokenIndex = enum(u32) {
    none = std.math.maxInt(u32),
    _,

    pub fn unwrap(oti: OptionalTokenIndex) ?TokenIndex {
        return if (oti == .none) null else @intFromEnum(oti);
    }

    pub fn fromToken(ti: TokenIndex) OptionalTokenIndex {
        return @enumFromInt(ti);
    }

    pub fn fromOptional(oti: ?TokenIndex) OptionalTokenIndex {
        return if (oti) |ti| @enumFromInt(ti) else .none;
    }
};

/// A relative token index.
pub const TokenOffset = enum(i32) {
    zero = 0,
    _,

    pub fn init(base: TokenIndex, destination: TokenIndex) TokenOffset {
        const base_i64: i64 = base;
        const destination_i64: i64 = destination;
        return @enumFromInt(destination_i64 - base_i64);
    }

    pub fn toOptional(to: TokenOffset) OptionalTokenOffset {
        const result: OptionalTokenOffset = @enumFromInt(@intFromEnum(to));
        assert(result != .none);
        return result;
    }

    pub fn toAbsolute(offset: TokenOffset, base: TokenIndex) TokenIndex {
        return @intCast(@as(i64, base) + @intFromEnum(offset));
    }
};

/// A relative token index, or null.
pub const OptionalTokenOffset = enum(i32) {
    none = std.math.maxInt(i32),
    _,

    pub fn unwrap(oto: OptionalTokenOffset) ?TokenOffset {
        return if (oto == .none) null else @enumFromInt(@intFromEnum(oto));
    }
};

pub fn tokenTag(tree: *const Ast, token_index: TokenIndex) Token.Tag {
    return tree.tokens.items(.tag)[token_index];
}

pub fn tokenStart(tree: *const Ast, token_index: TokenIndex) ByteOffset {
    return tree.tokens.items(.start)[token_index];
}

pub fn nodeTag(tree: *const Ast, node: Node.Index) Node.Tag {
    return tree.nodes.items(.tag)[@intFromEnum(node)];
}

pub fn nodeMainToken(tree: *const Ast, node: Node.Index) TokenIndex {
    return tree.nodes.items(.main_token)[@intFromEnum(node)];
}

pub fn nodeData(tree: *const Ast, node: Node.Index) Node.Data {
    return tree.nodes.items(.data)[@intFromEnum(node)];
}

pub fn isTokenPrecededByTags(
    tree: *const Ast,
    ti: TokenIndex,
    expected_token_tags: []const Token.Tag,
) bool {
    return std.mem.endsWith(
        Token.Tag,
        tree.tokens.items(.tag)[0..ti],
        expected_token_tags,
    );
}

pub const Location = struct {
    line: usize,
    column: usize,
    line_start: usize,
    line_end: usize,
};

pub const Span = struct {
    start: u32,
    end: u32,
    main: u32,
};

pub fn deinit(tree: *Ast, gpa: Allocator) void {
    tree.tokens.deinit(gpa);
    tree.nodes.deinit(gpa);
    gpa.free(tree.extra_data);
    gpa.free(tree.errors);
    tree.* = undefined;
}

pub const Mode = enum { zig, zon };

/// Result should be freed with tree.deinit() when there are
/// no more references to any of the tokens or nodes.
pub fn parse(gpa: Allocator, source: [:0]const u8, mode: Mode) Allocator.Error!Ast {
    var tokens = Ast.TokenList{};
    defer tokens.deinit(gpa);

    // Empirically, the zig std lib has an 8:1 ratio of source bytes to token count.
    const estimated_token_count = source.len / 8;
    try tokens.ensureTotalCapacity(gpa, estimated_token_count);

    var tokenizer = std.zig.Tokenizer.init(source);
    while (true) {
        const token = tokenizer.next();
        try tokens.append(gpa, .{
            .tag = token.tag,
            .start = @intCast(token.loc.start),
        });
        if (token.tag == .eof) break;
    }

    var tokens_slice = tokens.toOwnedSlice();
    errdefer tokens_slice.deinit(gpa);
    return parseTokens(gpa, source, tokens_slice, mode);
}

pub fn parseTokens(
    gpa: Allocator,
    source: [:0]const u8,
    tokens: Ast.TokenList.Slice,
    mode: Mode,
) Allocator.Error!Ast {
    var parser: Parse = .{
        .source = source,
        .gpa = gpa,
        .tokens = tokens,
        .errors = .empty,
        .nodes = .empty,
        .extra_data = .empty,
        .scratch = .empty,
        .tok_i = 0,
    };
    defer parser.errors.deinit(gpa);
    defer parser.nodes.deinit(gpa);
    defer parser.extra_data.deinit(gpa);
    defer parser.scratch.deinit(gpa);

    // Empirically, Zig source code has a 2:1 ratio of tokens to AST nodes.
    // Make sure at least 1 so we can use appendAssumeCapacity on the root node below.
    const estimated_node_count = (tokens.len + 2) / 2;
    try parser.nodes.ensureTotalCapacity(gpa, estimated_node_count);

    switch (mode) {
        .zig => try parser.parseRoot(),
        .zon => try parser.parseZon(),
    }

    const extra_data = try parser.extra_data.toOwnedSlice(gpa);
    errdefer gpa.free(extra_data);
    const errors = try parser.errors.toOwnedSlice(gpa);
    errdefer gpa.free(errors);

    // TODO experiment with compacting the MultiArrayList slices here
    return Ast{
        .source = source,
        .mode = mode,
        .tokens = tokens,
        .nodes = parser.nodes.toOwnedSlice(),
        .extra_data = extra_data,
        .errors = errors,
    };
}

/// `gpa` is used for allocating the resulting formatted source code.
/// Caller owns the returned slice of bytes, allocated with `gpa`.
pub fn renderAlloc(tree: Ast, gpa: Allocator) error{OutOfMemory}![]u8 {
    var aw: std.Io.Writer.Allocating = .init(gpa);
    defer aw.deinit();
    render(tree, gpa, &aw.writer, .{}) catch |err| switch (err) {
        error.WriteFailed, error.OutOfMemory => return error.OutOfMemory,
    };
    return aw.toOwnedSlice();
}

pub const Render = @import("Ast/Render.zig");

pub fn render(tree: Ast, gpa: Allocator, w: *Writer, fixups: Render.Fixups) Render.Error!void {
    return Render.renderTree(gpa, w, tree, fixups);
}

/// Returns an extra offset for column and byte offset of errors that
/// should point after the token in the error message.
pub fn errorOffset(tree: Ast, parse_error: Error) u32 {
    return if (parse_error.token_is_prev) @intCast(tree.tokenSlice(parse_error.token).len) else 0;
}

pub fn tokenLocation(self: Ast, start_offset: ByteOffset, token_index: TokenIndex) Location {
    var loc = Location{
        .line = 0,
        .column = 0,
        .line_start = start_offset,
        .line_end = self.source.len,
    };
    const token_start = self.tokenStart(token_index);

    // Scan to by line until we go past the token start
    while (std.mem.findScalarPos(u8, self.source, loc.line_start, '\n')) |i| {
        if (i >= token_start) {
            break; // Went past
        }
        loc.line += 1;
        loc.line_start = i + 1;
    }

    const offset = loc.line_start;
    for (self.source[offset..], 0..) |c, i| {
        if (i + offset == token_start) {
            loc.line_end = i + offset;
            while (loc.line_end < self.source.len and self.source[loc.line_end] != '\n') {
                loc.line_end += 1;
            }
            return loc;
        }
        if (c == '\n') {
            loc.line += 1;
            loc.column = 0;
            loc.line_start = i + 1;
        } else {
            loc.column += 1;
        }
    }
    return loc;
}

pub fn tokenSlice(tree: Ast, token_index: TokenIndex) []const u8 {
    const token_tag = tree.tokenTag(token_index);

    // Many tokens can be determined entirely by their tag.
    if (token_tag.lexeme()) |lexeme| {
        return lexeme;
    }

    // For some tokens, re-tokenization is needed to find the end.
    var tokenizer: std.zig.Tokenizer = .{
        .buffer = tree.source,
        .index = tree.tokenStart(token_index),
    };
    const token = tokenizer.next();
    assert(token.tag == token_tag);
    return tree.source[token.loc.start..token.loc.end];
}

pub fn extraDataSlice(tree: Ast, range: Node.SubRange, comptime T: type) []const T {
    return @ptrCast(tree.extra_data[@intFromEnu
```
