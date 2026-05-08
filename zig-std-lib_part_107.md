```
delay_shuffle,
            .nopl,
            .pconfig,
            .pku,
            .popcnt,
            .prefer_256_bit,
            .prefetchi,
            .prfchw,
            .ptwrite,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .serialize,
            .sha,
            .shstk,
            .tsxldtrk,
            .tuning_fast_imm_vector_shift,
            .uintr,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .waitpkg,
            .wbnoinvd,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const haswell: CpuModel = .{
        .name = "haswell",
        .llvm_name = "haswell",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .allow_light_256_bit,
            .avx2,
            .bmi,
            .bmi2,
            .cmov,
            .cx16,
            .ermsb,
            .f16c,
            .false_deps_lzcnt_tzcnt,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fma,
            .fsgsbase,
            .fxsr,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pclmul,
            .popcnt,
            .rdrnd,
            .sahf,
            .slow_3ops_lea,
            .smep,
            .vzeroupper,
            .x87,
            .xsaveopt,
        }),
    };
    pub const @"i386": CpuModel = .{
        .name = "i386",
        .llvm_name = "i386",
        .features = featureSet(&[_]Feature{
            .bsf_bsr_0_clobbers_result,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const @"i486": CpuModel = .{
        .name = "i486",
        .llvm_name = "i486",
        .features = featureSet(&[_]Feature{
            .bsf_bsr_0_clobbers_result,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const @"i586": CpuModel = .{
        .name = "i586",
        .llvm_name = "i586",
        .features = featureSet(&[_]Feature{
            .cx8,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const @"i686": CpuModel = .{
        .name = "i686",
        .llvm_name = "i686",
        .features = featureSet(&[_]Feature{
            .cmov,
            .cx8,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const @"i86": CpuModel = .{
        .name = "i86",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{
            .@"16bit_mode",
        }),
    };
    pub const icelake_client: CpuModel = .{
        .name = "icelake_client",
        .llvm_name = "icelake-client",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avx512bitalg,
            .avx512cd,
            .avx512dq,
            .avx512ifma,
            .avx512vbmi,
            .avx512vbmi2,
            .avx512vl,
            .avx512vnni,
            .avx512vpopcntdq,
            .bmi,
            .bmi2,
            .clflushopt,
            .cmov,
            .cx16,
            .ermsb,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fsgsbase,
            .fsrm,
            .fxsr,
            .gfni,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pku,
            .popcnt,
            .prefer_256_bit,
            .prfchw,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .sha,
            .tuning_fast_imm_vector_shift,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const icelake_server: CpuModel = .{
        .name = "icelake_server",
        .llvm_name = "icelake-server",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avx512bitalg,
            .avx512cd,
            .avx512dq,
            .avx512ifma,
            .avx512vbmi,
            .avx512vbmi2,
            .avx512vl,
            .avx512vnni,
            .avx512vpopcntdq,
            .bmi,
            .bmi2,
            .clflushopt,
            .clwb,
            .cmov,
            .cx16,
            .ermsb,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fsgsbase,
            .fsrm,
            .fxsr,
            .gfni,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pconfig,
            .pku,
            .popcnt,
            .prefer_256_bit,
            .prfchw,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .sha,
            .tuning_fast_imm_vector_shift,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .wbnoinvd,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const ivybridge: CpuModel = .{
        .name = "ivybridge",
        .llvm_name = "ivybridge",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .cmov,
            .cx16,
            .f16c,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fsgsbase,
            .fxsr,
            .idivq_to_divl,
            .macrofusion,
            .mmx,
            .no_bypass_delay_mov,
            .nopl,
            .pclmul,
            .popcnt,
            .rdrnd,
            .sahf,
            .slow_3ops_lea,
            .slow_unaligned_mem_32,
            .smep,
            .vzeroupper,
            .x87,
            .xsaveopt,
        }),
    };
    pub const k6: CpuModel = .{
        .name = "k6",
        .llvm_name = "k6",
        .features = featureSet(&[_]Feature{
            .cx8,
            .mmx,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const k6_2: CpuModel = .{
        .name = "k6_2",
        .llvm_name = "k6-2",
        .features = featureSet(&[_]Feature{
            .@"3dnow",
            .cx8,
            .prfchw,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const k6_3: CpuModel = .{
        .name = "k6_3",
        .llvm_name = "k6-3",
        .features = featureSet(&[_]Feature{
            .@"3dnow",
            .cx8,
            .prfchw,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const k8: CpuModel = .{
        .name = "k8",
        .llvm_name = "k8",
        .features = featureSet(&[_]Feature{
            .@"3dnowa",
            .@"64bit",
            .cmov,
            .cx8,
            .fast_scalar_shift_masks,
            .fxsr,
            .nopl,
            .prfchw,
            .sbb_dep_breaking,
            .slow_shld,
            .slow_unaligned_mem_16,
            .sse2,
            .vzeroupper,
            .x87,
        }),
    };
    pub const k8_sse3: CpuModel = .{
        .name = "k8_sse3",
        .llvm_name = "k8-sse3",
        .features = featureSet(&[_]Feature{
            .@"3dnowa",
            .@"64bit",
            .cmov,
            .cx16,
            .fast_scalar_shift_masks,
            .fxsr,
            .nopl,
            .prfchw,
            .sbb_dep_breaking,
            .slow_shld,
            .slow_unaligned_mem_16,
            .sse3,
            .vzeroupper,
            .x87,
        }),
    };
    pub const knl: CpuModel = .{
        .name = "knl",
        .llvm_name = "knl",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .aes,
            .avx512cd,
            .avx512er,
            .avx512pf,
            .bmi,
            .bmi2,
            .cmov,
            .cx16,
            .fast_gather,
            .fast_imm16,
            .fast_movbe,
            .fsgsbase,
            .fxsr,
            .idivq_to_divl,
            .lzcnt,
            .mmx,
            .movbe,
            .nopl,
            .pclmul,
            .popcnt,
            .prefer_mask_registers,
            .prefetchwt1,
            .prfchw,
            .rdrnd,
            .rdseed,
            .sahf,
            .slow_3ops_lea,
            .slow_incdec,
            .slow_pmaddwd,
            .slow_two_mem_ops,
            .x87,
            .xsaveopt,
        }),
    };
    pub const knm: CpuModel = .{
        .name = "knm",
        .llvm_name = "knm",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .aes,
            .avx512cd,
            .avx512er,
            .avx512pf,
            .avx512vpopcntdq,
            .bmi,
            .bmi2,
            .cmov,
            .cx16,
            .fast_gather,
            .fast_imm16,
            .fast_movbe,
            .fsgsbase,
            .fxsr,
            .idivq_to_divl,
            .lzcnt,
            .mmx,
            .movbe,
            .nopl,
            .pclmul,
            .popcnt,
            .prefer_mask_registers,
            .prefetchwt1,
            .prfchw,
            .rdrnd,
            .rdseed,
            .sahf,
            .slow_3ops_lea,
            .slow_incdec,
            .slow_pmaddwd,
            .slow_two_mem_ops,
            .x87,
            .xsaveopt,
        }),
    };
    pub const lakemont: CpuModel = .{
        .name = "lakemont",
        .llvm_name = "lakemont",
        .features = featureSet(&[_]Feature{
            .cx8,
            .slow_unaligned_mem_16,
            .soft_float,
            .vzeroupper,
        }),
    };
    pub const lunarlake: CpuModel = .{
        .name = "lunarlake",
        .llvm_name = "lunarlake",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avxifma,
            .avxneconvert,
            .avxvnni,
            .avxvnniint16,
            .avxvnniint8,
            .bmi,
            .bmi2,
            .clflushopt,
            .clwb,
            .cmov,
            .cmpccxadd,
            .cx16,
            .enqcmd,
            .f16c,
            .false_deps_perm,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fma,
            .fsgsbase,
            .fxsr,
            .gfni,
            .hreset,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .movdir64b,
            .movdiri,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pconfig,
            .pku,
            .popcnt,
            .prefer_movmsk_over_vtest,
            .prfchw,
            .ptwrite,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .serialize,
            .sha,
            .sha512,
            .shstk,
            .slow_3ops_lea,
            .sm3,
            .sm4,
            .tuning_fast_imm_vector_shift,
            .uintr,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .waitpkg,
            .widekl,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const meteorlake: CpuModel = .{
        .name = "meteorlake",
        .llvm_name = "meteorlake",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avxvnni,
            .bmi,
            .bmi2,
            .clflushopt,
            .clwb,
            .cmov,
            .cx16,
            .f16c,
            .false_deps_perm,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fma,
            .fsgsbase,
            .fxsr,
            .gfni,
            .hreset,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .movdir64b,
            .movdiri,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pconfig,
            .pku,
            .popcnt,
            .prefer_movmsk_over_vtest,
            .prfchw,
            .ptwrite,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .serialize,
            .sha,
            .shstk,
            .slow_3ops_lea,
            .smap,
            .smep,
            .tuning_fast_imm_vector_shift,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .waitpkg,
            .widekl,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const nehalem: CpuModel = .{
        .name = "nehalem",
        .llvm_name = "nehalem",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .cmov,
            .cx16,
            .fxsr,
            .idivq_to_divl,
            .macrofusion,
            .mmx,
            .no_bypass_delay_mov,
            .nopl,
            .popcnt,
            .sahf,
            .sse4_2,
            .vzeroupper,
            .x87,
        }),
    };
    pub const nocona: CpuModel = .{
        .name = "nocona",
        .llvm_name = "nocona",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .cmov,
            .cx16,
            .fxsr,
            .mmx,
            .nopl,
            .slow_unaligned_mem_16,
            .sse3,
            .vzeroupper,
            .x87,
        }),
    };
    pub const opteron: CpuModel = .{
        .name = "opteron",
        .llvm_name = "opteron",
        .features = featureSet(&[_]Feature{
            .@"3dnowa",
            .@"64bit",
            .cmov,
            .cx8,
            .fast_scalar_shift_masks,
            .fxsr,
            .nopl,
            .prfchw,
            .sbb_dep_breaking,
            .slow_shld,
            .slow_unaligned_mem_16,
            .sse2,
            .vzeroupper,
            .x87,
        }),
    };
    pub const opteron_sse3: CpuModel = .{
        .name = "opteron_sse3",
        .llvm_name = "opteron-sse3",
        .features = featureSet(&[_]Feature{
            .@"3dnowa",
            .@"64bit",
            .cmov,
            .cx16,
            .fast_scalar_shift_masks,
            .fxsr,
            .nopl,
            .prfchw,
            .sbb_dep_breaking,
            .slow_shld,
            .slow_unaligned_mem_16,
            .sse3,
            .vzeroupper,
            .x87,
        }),
    };
    pub const pantherlake: CpuModel = .{
        .name = "pantherlake",
        .llvm_name = "pantherlake",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avxifma,
            .avxneconvert,
            .avxvnni,
            .avxvnniint16,
            .avxvnniint8,
            .bmi,
            .bmi2,
            .clflushopt,
            .clwb,
            .cmov,
            .cmpccxadd,
            .cx16,
            .enqcmd,
            .f16c,
            .false_deps_perm,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fma,
            .fsgsbase,
            .fxsr,
            .gfni,
            .hreset,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .movdir64b,
            .movdiri,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pconfig,
            .pku,
            .popcnt,
            .prefer_movmsk_over_vtest,
            .prefetchi,
            .prfchw,
            .ptwrite,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .serialize,
            .sha,
            .sha512,
            .shstk,
            .slow_3ops_lea,
            .sm3,
            .sm4,
            .tuning_fast_imm_vector_shift,
            .uintr,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .waitpkg,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const penryn: CpuModel = .{
        .name = "penryn",
        .llvm_name = "penryn",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .cmov,
            .cx16,
            .fxsr,
            .macrofusion,
            .mmx,
            .nopl,
            .sahf,
            .slow_unaligned_mem_16,
            .sse4_1,
            .vzeroupper,
            .x87,
        }),
    };
    pub const pentium: CpuModel = .{
        .name = "pentium",
        .llvm_name = "pentium",
        .features = featureSet(&[_]Feature{
            .cx8,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const pentium2: CpuModel = .{
        .name = "pentium2",
        .llvm_name = "pentium2",
        .features = featureSet(&[_]Feature{
            .cmov,
            .cx8,
            .fxsr,
            .mmx,
            .nopl,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const pentium3: CpuModel = .{
        .name = "pentium3",
        .llvm_name = "pentium3",
        .features = featureSet(&[_]Feature{
            .cmov,
            .cx8,
            .fxsr,
            .mmx,
            .nopl,
            .slow_unaligned_mem_16,
            .sse,
            .vzeroupper,
            .x87,
        }),
    };
    pub const pentium3m: CpuModel = .{
        .name = "pentium3m",
        .llvm_name = "pentium3m",
        .features = featureSet(&[_]Feature{
            .cmov,
            .cx8,
            .fxsr,
            .mmx,
            .nopl,
            .slow_unaligned_mem_16,
            .sse,
            .vzeroupper,
            .x87,
        }),
    };
    pub const pentium4: CpuModel = .{
        .name = "pentium4",
        .llvm_name = "pentium4",
        .features = featureSet(&[_]Feature{
            .cmov,
            .cx8,
            .fxsr,
            .mmx,
            .nopl,
            .slow_unaligned_mem_16,
            .sse2,
            .vzeroupper,
            .x87,
        }),
    };
    pub const pentium_m: CpuModel = .{
        .name = "pentium_m",
        .llvm_name = "pentium-m",
        .features = featureSet(&[_]Feature{
            .cmov,
            .cx8,
            .fxsr,
            .mmx,
            .nopl,
            .slow_unaligned_mem_16,
            .sse2,
            .vzeroupper,
            .x87,
        }),
    };
    pub const pentium_mmx: CpuModel = .{
        .name = "pentium_mmx",
        .llvm_name = "pentium-mmx",
        .features = featureSet(&[_]Feature{
            .cx8,
            .mmx,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const pentiumpro: CpuModel = .{
        .name = "pentiumpro",
        .llvm_name = "pentiumpro",
        .features = featureSet(&[_]Feature{
            .cmov,
            .cx8,
            .nopl,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const prescott: CpuModel = .{
        .name = "prescott",
        .llvm_name = "prescott",
        .features = featureSet(&[_]Feature{
            .cmov,
            .cx8,
            .fxsr,
            .mmx,
            .nopl,
            .slow_unaligned_mem_16,
            .sse3,
            .vzeroupper,
            .x87,
        }),
    };
    pub const raptorlake: CpuModel = .{
        .name = "raptorlake",
        .llvm_name = "raptorlake",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avxvnni,
            .bmi,
            .bmi2,
            .clflushopt,
            .clwb,
            .cmov,
            .cx16,
            .f16c,
            .false_deps_perm,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fma,
            .fsgsbase,
            .fxsr,
            .gfni,
            .hreset,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .movdir64b,
            .movdiri,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pconfig,
            .pku,
            .popcnt,
            .prefer_movmsk_over_vtest,
            .prfchw,
            .ptwrite,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .serialize,
            .sha,
            .shstk,
            .slow_3ops_lea,
            .smap,
            .smep,
            .tuning_fast_imm_vector_shift,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .waitpkg,
            .widekl,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const rocketlake: CpuModel = .{
        .name = "rocketlake",
        .llvm_name = "rocketlake",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avx512bitalg,
            .avx512cd,
            .avx512dq,
            .avx512ifma,
            .avx512vbmi,
            .avx512vbmi2,
            .avx512vl,
            .avx512vnni,
            .avx512vpopcntdq,
            .bmi,
            .bmi2,
            .clflushopt,
            .cmov,
            .cx16,
            .ermsb,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fsgsbase,
            .fsrm,
            .fxsr,
            .gfni,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pku,
            .popcnt,
            .prefer_256_bit,
            .prfchw,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .sha,
            .smap,
            .smep,
            .tuning_fast_imm_vector_shift,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const sandybridge: CpuModel = .{
        .name = "sandybridge",
        .llvm_name = "sandybridge",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .avx,
            .cmov,
            .cx16,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fxsr,
            .idivq_to_divl,
            .macrofusion,
            .mmx,
            .no_bypass_delay_mov,
            .nopl,
            .pclmul,
            .popcnt,
            .sahf,
            .slow_3ops_lea,
            .slow_unaligned_mem_32,
            .vzeroupper,
            .x87,
            .xsaveopt,
        }),
    };
    pub const sapphirerapids: CpuModel = .{
        .name = "sapphirerapids",
        .llvm_name = "sapphirerapids",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .amx_bf16,
            .amx_int8,
            .avx512bf16,
            .avx512bitalg,
            .avx512cd,
            .avx512dq,
            .avx512fp16,
            .avx512ifma,
            .avx512vbmi,
            .avx512vbmi2,
            .avx512vl,
            .avx512vnni,
            .avx512vpopcntdq,
            .avxvnni,
            .bmi,
            .bmi2,
            .cldemote,
            .clflushopt,
            .clwb,
            .cmov,
            .cx16,
            .enqcmd,
            .ermsb,
            .false_deps_getmant,
            .false_deps_mulc,
            .false_deps_mullq,
            .false_deps_perm,
            .false_deps_range,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fsgsbase,
            .fsrm,
            .fxsr,
            .gfni,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .movdir64b,
            .movdiri,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pconfig,
            .pku,
            .popcnt,
            .prefer_256_bit,
            .prfchw,
            .ptwrite,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .serialize,
            .sha,
            .shstk,
            .smap,
            .smep,
            .tsxldtrk,
            .tuning_fast_imm_vector_shift,
            .uintr,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .waitpkg,
            .wbnoinvd,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const sierraforest: CpuModel = .{
        .name = "sierraforest",
        .llvm_name = "sierraforest",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .avxifma,
            .avxneconvert,
            .avxvnni,
            .avxvnniint8,
            .bmi,
            .bmi2,
            .cldemote,
            .clflushopt,
            .clwb,
            .cmov,
            .cmpccxadd,
            .cx16,
            .enqcmd,
            .f16c,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_scalar_fsqrt,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fma,
            .fsgsbase,
            .fxsr,
            .gfni,
            .hreset,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .movdir64b,
            .movdiri,
            .nopl,
            .pconfig,
            .pku,
            .popcnt,
            .prfchw,
            .ptwrite,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .serialize,
            .sha,
            .shstk,
            .slow_3ops_lea,
            .uintr,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .waitpkg,
            .widekl,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const silvermont: CpuModel = .{
        .name = "silvermont",
        .llvm_name = "silvermont",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .cmov,
            .cx16,
            .false_deps_popcnt,
            .fast_7bytenop,
            .fast_imm16,
            .fast_movbe,
            .fxsr,
            .idivq_to_divl,
            .mmx,
            .movbe,
            .no_bypass_delay,
            .nopl,
            .pclmul,
            .popcnt,
            .prfchw,
            .rdrnd,
            .sahf,
            .slow_incdec,
            .slow_lea,
            .slow_pmulld,
            .slow_two_mem_ops,
            .smep,
            .sse4_2,
            .use_slm_arith_costs,
            .vzeroupper,
            .x87,
        }),
    };
    pub const skx: CpuModel = .{
        .name = "skx",
        .llvm_name = "skx",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .aes,
            .allow_light_256_bit,
            .avx512bw,
            .avx512cd,
            .avx512dq,
            .avx512vl,
            .bmi,
            .bmi2,
            .clflushopt,
            .clwb,
            .cmov,
            .cx16,
            .ermsb,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .faster_shift_than_shuffle,
            .fsgsbase,
            .fxsr,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pclmul,
            .pku,
            .popcnt,
            .prefer_256_bit,
            .prfchw,
            .rdrnd,
            .rdseed,
            .sahf,
            .slow_3ops_lea,
            .smap,
            .smep,
            .tuning_fast_imm_vector_shift,
            .vzeroupper,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const skylake: CpuModel = .{
        .name = "skylake",
        .llvm_name = "skylake",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .aes,
            .allow_light_256_bit,
            .avx2,
            .bmi,
            .bmi2,
            .clflushopt,
            .cmov,
            .cx16,
            .ermsb,
            .f16c,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fma,
            .fsgsbase,
            .fxsr,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pclmul,
            .popcnt,
            .prfchw,
            .rdrnd,
            .rdseed,
            .sahf,
            .slow_3ops_lea,
            .smap,
            .smep,
            .vzeroupper,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const skylake_avx512: CpuModel = .{
        .name = "skylake_avx512",
        .llvm_name = "skylake-avx512",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .aes,
            .allow_light_256_bit,
            .avx512bw,
            .avx512cd,
            .avx512dq,
            .avx512vl,
            .bmi,
            .bmi2,
            .clflushopt,
            .clwb,
            .cmov,
            .cx16,
            .ermsb,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .faster_shift_than_shuffle,
            .fsgsbase,
            .fxsr,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pclmul,
            .pku,
            .popcnt,
            .prefer_256_bit,
            .prfchw,
            .rdrnd,
            .rdseed,
            .sahf,
            .slow_3ops_lea,
            .tuning_fast_imm_vector_shift,
            .vzeroupper,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const slm: CpuModel = .{
        .name = "slm",
        .llvm_name = "slm",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .cmov,
            .cx16,
            .false_deps_popcnt,
            .fast_7bytenop,
            .fast_imm16,
            .fast_movbe,
            .fxsr,
            .idivq_to_divl,
            .mmx,
            .movbe,
            .no_bypass_delay,
            .nopl,
            .pclmul,
            .popcnt,
            .prfchw,
            .rdrnd,
            .sahf,
            .slow_incdec,
            .slow_lea,
            .slow_pmulld,
            .slow_two_mem_ops,
            .sse4_2,
            .use_slm_arith_costs,
            .vzeroupper,
            .x87,
        }),
    };
    pub const tigerlake: CpuModel = .{
        .name = "tigerlake",
        .llvm_name = "tigerlake",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avx512bitalg,
            .avx512cd,
            .avx512dq,
            .avx512ifma,
            .avx512vbmi,
            .avx512vbmi2,
            .avx512vl,
            .avx512vnni,
            .avx512vp2intersect,
            .avx512vpopcntdq,
            .bmi,
            .bmi2,
            .clflushopt,
            .clwb,
            .cmov,
            .cx16,
            .ermsb,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fsgsbase,
            .fsrm,
            .fxsr,
            .gfni,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .movdir64b,
            .movdiri,
            .no_bypass_delay_blend,
            .no_bypass_delay_mov,
            .no_bypass_delay_shuffle,
            .nopl,
            .pku,
            .popcnt,
            .prefer_256_bit,
            .prfchw,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .sha,
            .shstk,
            .smap,
            .smep,
            .tuning_fast_imm_vector_shift,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const tremont: CpuModel = .{
        .name = "tremont",
        .llvm_name = "tremont",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .aes,
            .clflushopt,
            .clwb,
            .cmov,
            .cx16,
            .fast_imm16,
            .fast_movbe,
            .fsgsbase,
            .fxsr,
            .gfni,
            .mmx,
            .movbe,
            .no_bypass_delay,
            .nopl,
            .pclmul,
            .popcnt,
            .prfchw,
            .ptwrite,
            .rdpid,
            .rdrnd,
            .rdseed,
            .sahf,
            .sha,
            .slow_incdec,
            .slow_lea,
            .slow_two_mem_ops,
            .sse4_2,
            .use_glm_div_sqrt_costs,
            .vzeroupper,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const westmere: CpuModel = .{
        .name = "westmere",
        .llvm_name = "westmere",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .cmov,
            .cx16,
            .fxsr,
            .idivq_to_divl,
            .macrofusion,
            .mmx,
            .no_bypass_delay_mov,
            .nopl,
            .pclmul,
            .popcnt,
            .sahf,
            .sse4_2,
            .vzeroupper,
            .x87,
        }),
    };
    pub const winchip2: CpuModel = .{
        .name = "winchip2",
        .llvm_name = "winchip2",
        .features = featureSet(&[_]Feature{
            .@"3dnow",
            .prfchw,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const winchip_c6: CpuModel = .{
        .name = "winchip_c6",
        .llvm_name = "winchip-c6",
        .features = featureSet(&[_]Feature{
            .mmx,
            .slow_unaligned_mem_16,
            .vzeroupper,
            .x87,
        }),
    };
    pub const x86_64: CpuModel = .{
        .name = "x86_64",
        .llvm_name = "x86-64",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .cmov,
            .cx8,
            .fxsr,
            .idivq_to_divl,
            .macrofusion,
            .mmx,
            .nopl,
            .slow_3ops_lea,
            .slow_incdec,
            .sse2,
            .vzeroupper,
            .x87,
        }),
    };
    pub const x86_64_v2: CpuModel = .{
        .name = "x86_64_v2",
        .llvm_name = "x86-64-v2",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .cmov,
            .cx16,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fxsr,
            .idivq_to_divl,
            .macrofusion,
            .mmx,
            .nopl,
            .popcnt,
            .sahf,
            .slow_3ops_lea,
            .slow_unaligned_mem_32,
            .sse4_2,
            .vzeroupper,
            .x87,
        }),
    };
    pub const x86_64_v3: CpuModel = .{
        .name = "x86_64_v3",
        .llvm_name = "x86-64-v3",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .allow_light_256_bit,
            .avx2,
            .bmi,
            .bmi2,
            .cmov,
            .cx16,
            .f16c,
            .false_deps_lzcnt_tzcnt,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fma,
            .fxsr,
            .idivq_to_divl,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .nopl,
            .popcnt,
            .sahf,
            .slow_3ops_lea,
            .vzeroupper,
            .x87,
            .xsave,
        }),
    };
    pub const x86_64_v4: CpuModel = .{
        .name = "x86_64_v4",
        .llvm_name = "x86-64-v4",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .allow_light_256_bit,
            .avx512bw,
            .avx512cd,
            .avx512dq,
            .avx512vl,
            .bmi,
            .bmi2,
            .cmov,
            .cx16,
            .false_deps_popcnt,
            .fast_15bytenop,
            .fast_gather,
            .fast_scalar_fsqrt,
            .fast_shld_rotate,
            .fast_variable_crosslane_shuffle,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fxsr,
            .idivq_to_divl,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .nopl,
            .popcnt,
            .prefer_256_bit,
            .sahf,
            .slow_3ops_lea,
            .vzeroupper,
            .x87,
            .xsave,
        }),
    };
    pub const yonah: CpuModel = .{
        .name = "yonah",
        .llvm_name = "yonah",
        .features = featureSet(&[_]Feature{
            .cmov,
            .cx8,
            .fxsr,
            .mmx,
            .nopl,
            .slow_unaligned_mem_16,
            .sse3,
            .vzeroupper,
            .x87,
        }),
    };
    pub const znver1: CpuModel = .{
        .name = "znver1",
        .llvm_name = "znver1",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .aes,
            .allow_light_256_bit,
            .avx2,
            .bmi,
            .bmi2,
            .branchfusion,
            .clflushopt,
            .clzero,
            .cmov,
            .cx16,
            .f16c,
            .fast_15bytenop,
            .fast_bextr,
            .fast_imm16,
            .fast_lzcnt,
            .fast_movbe,
            .fast_scalar_fsqrt,
            .fast_scalar_shift_masks,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fma,
            .fsgsbase,
            .fxsr,
            .idivq_to_divl,
            .lzcnt,
            .mmx,
            .movbe,
            .mwaitx,
            .nopl,
            .pclmul,
            .popcnt,
            .prfchw,
            .rdrnd,
            .rdseed,
            .sahf,
            .sbb_dep_breaking,
            .sha,
            .slow_shld,
            .smap,
            .smep,
            .sse4a,
            .vzeroupper,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const znver2: CpuModel = .{
        .name = "znver2",
        .llvm_name = "znver2",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .aes,
            .allow_light_256_bit,
            .avx2,
            .bmi,
            .bmi2,
            .branchfusion,
            .clflushopt,
            .clwb,
            .clzero,
            .cmov,
            .cx16,
            .f16c,
            .fast_15bytenop,
            .fast_bextr,
            .fast_imm16,
            .fast_lzcnt,
            .fast_movbe,
            .fast_scalar_fsqrt,
            .fast_scalar_shift_masks,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fma,
            .fsgsbase,
            .fxsr,
            .idivq_to_divl,
            .lzcnt,
            .mmx,
            .movbe,
            .mwaitx,
            .nopl,
            .pclmul,
            .popcnt,
            .prfchw,
            .rdpid,
            .rdpru,
            .rdrnd,
            .rdseed,
            .sahf,
            .sbb_dep_breaking,
            .sha,
            .slow_shld,
            .smap,
            .smep,
            .sse4a,
            .vzeroupper,
            .wbnoinvd,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const znver3: CpuModel = .{
        .name = "znver3",
        .llvm_name = "znver3",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .bmi,
            .bmi2,
            .branchfusion,
            .clflushopt,
            .clwb,
            .clzero,
            .cmov,
            .cx16,
            .f16c,
            .fast_15bytenop,
            .fast_bextr,
            .fast_imm16,
            .fast_lzcnt,
            .fast_movbe,
            .fast_scalar_fsqrt,
            .fast_scalar_shift_masks,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fma,
            .fsgsbase,
            .fsrm,
            .fxsr,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .mwaitx,
            .nopl,
            .pku,
            .popcnt,
            .prfchw,
            .rdpid,
            .rdpru,
            .rdrnd,
            .rdseed,
            .sahf,
            .sbb_dep_breaking,
            .sha,
            .smap,
            .smep,
            .sse4a,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .wbnoinvd,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const znver4: CpuModel = .{
        .name = "znver4",
        .llvm_name = "znver4",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avx512bf16,
            .avx512bitalg,
            .avx512cd,
            .avx512dq,
            .avx512ifma,
            .avx512vbmi,
            .avx512vbmi2,
            .avx512vl,
            .avx512vnni,
            .avx512vpopcntdq,
            .bmi,
            .bmi2,
            .branchfusion,
            .clflushopt,
            .clwb,
            .clzero,
            .cmov,
            .cx16,
            .fast_15bytenop,
            .fast_bextr,
            .fast_dpwssd,
            .fast_imm16,
            .fast_lzcnt,
            .fast_movbe,
            .fast_scalar_fsqrt,
            .fast_scalar_shift_masks,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fsgsbase,
            .fsrm,
            .fxsr,
            .gfni,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .mwaitx,
            .nopl,
            .pku,
            .popcnt,
            .prfchw,
            .rdpid,
            .rdpru,
            .rdrnd,
            .rdseed,
            .sahf,
            .sbb_dep_breaking,
            .sha,
            .shstk,
            .smap,
            .smep,
            .sse4a,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .wbnoinvd,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
    pub const znver5: CpuModel = .{
        .name = "znver5",
        .llvm_name = "znver5",
        .features = featureSet(&[_]Feature{
            .@"64bit",
            .adx,
            .allow_light_256_bit,
            .avx512bf16,
            .avx512bitalg,
            .avx512cd,
            .avx512dq,
            .avx512ifma,
            .avx512vbmi,
            .avx512vbmi2,
            .avx512vl,
            .avx512vnni,
            .avx512vp2intersect,
            .avx512vpopcntdq,
            .avxvnni,
            .bmi,
            .bmi2,
            .branchfusion,
            .clflushopt,
            .clwb,
            .clzero,
            .cmov,
            .cx16,
            .fast_15bytenop,
            .fast_bextr,
            .fast_dpwssd,
            .fast_imm16,
            .fast_lzcnt,
            .fast_movbe,
            .fast_scalar_fsqrt,
            .fast_scalar_shift_masks,
            .fast_variable_perlane_shuffle,
            .fast_vector_fsqrt,
            .fsgsbase,
            .fsrm,
            .fxsr,
            .gfni,
            .idivq_to_divl,
            .invpcid,
            .lzcnt,
            .macrofusion,
            .mmx,
            .movbe,
            .movdir64b,
            .movdiri,
            .mwaitx,
            .nopl,
            .pku,
            .popcnt,
            .prefetchi,
            .prfchw,
            .rdpid,
            .rdpru,
            .rdrnd,
            .rdseed,
            .sahf,
            .sbb_dep_breaking,
            .sha,
            .shstk,
            .smap,
            .smep,
            .sse4a,
            .vaes,
            .vpclmulqdq,
            .vzeroupper,
            .wbnoinvd,
            .x87,
            .xsavec,
            .xsaveopt,
            .xsaves,
        }),
    };
};



---
File: /std/Target/xcore.zig
---

//! This file is auto-generated by tools/update_cpu_features.zig.

const std = @import("../std.zig");
const CpuFeature = std.Target.Cpu.Feature;
const CpuModel = std.Target.Cpu.Model;

pub const Feature = enum {};

pub const featureSet = CpuFeature.FeatureSetFns(Feature).featureSet;
pub const featureSetHas = CpuFeature.FeatureSetFns(Feature).featureSetHas;
pub const featureSetHasAny = CpuFeature.FeatureSetFns(Feature).featureSetHasAny;
pub const featureSetHasAll = CpuFeature.FeatureSetFns(Feature).featureSetHasAll;

pub const all_features = blk: {
    const len = @typeInfo(Feature).@"enum".fields.len;
    std.debug.assert(len <= CpuFeature.Set.needed_bit_count);
    var result: [len]CpuFeature = undefined;
    const ti = @typeInfo(Feature);
    for (&result, 0..) |*elem, i| {
        elem.index = i;
        elem.name = ti.@"enum".fields[i].name;
    }
    break :blk result;
};

pub const cpu = struct {
    pub const generic: CpuModel = .{
        .name = "generic",
        .llvm_name = "generic",
        .features = featureSet(&[_]Feature{}),
    };
    pub const xs1b_generic: CpuModel = .{
        .name = "xs1b_generic",
        .llvm_name = "xs1b-generic",
        .features = featureSet(&[_]Feature{}),
    };
};



---
File: /std/Target/xtensa.zig
---

//! This file is auto-generated by tools/update_cpu_features.zig.

const std = @import("../std.zig");
const CpuFeature = std.Target.Cpu.Feature;
const CpuModel = std.Target.Cpu.Model;

pub const Feature = enum {
    bool,
    clamps,
    coprocessor,
    dcache,
    debug,
    density,
    dfpaccel,
    div32,
    exception,
    extendedl32r,
    fp,
    highpriinterrupts,
    highpriinterrupts_level3,
    highpriinterrupts_level4,
    highpriinterrupts_level5,
    highpriinterrupts_level6,
    highpriinterrupts_level7,
    interrupt,
    loop,
    mac16,
    minmax,
    miscsr,
    mul16,
    mul32,
    mul32high,
    nsa,
    prid,
    regprotect,
    rvector,
    sext,
    threadptr,
    timers1,
    timers2,
    timers3,
    windowed,
};

pub const featureSet = CpuFeature.FeatureSetFns(Feature).featureSet;
pub const featureSetHas = CpuFeature.FeatureSetFns(Feature).featureSetHas;
pub const featureSetHasAny = CpuFeature.FeatureSetFns(Feature).featureSetHasAny;
pub const featureSetHasAll = CpuFeature.FeatureSetFns(Feature).featureSetHasAll;

pub const all_features = blk: {
    const len = @typeInfo(Feature).@"enum".fields.len;
    std.debug.assert(len <= CpuFeature.Set.needed_bit_count);
    var result: [len]CpuFeature = undefined;
    result[@intFromEnum(Feature.bool)] = .{
        .llvm_name = "bool",
        .description = "Enable Xtensa Boolean extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.clamps)] = .{
        .llvm_name = "clamps",
        .description = "Enable Xtensa CLAMPS option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.coprocessor)] = .{
        .llvm_name = "coprocessor",
        .description = "Enable Xtensa Coprocessor option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dcache)] = .{
        .llvm_name = "dcache",
        .description = "Enable Xtensa Data Cache option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.debug)] = .{
        .llvm_name = "debug",
        .description = "Enable Xtensa Debug option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.density)] = .{
        .llvm_name = "density",
        .description = "Enable Density instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dfpaccel)] = .{
        .llvm_name = "dfpaccel",
        .description = "Enable Xtensa Double Precision FP acceleration",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.div32)] = .{
        .llvm_name = "div32",
        .description = "Enable Xtensa Div32 option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.exception)] = .{
        .llvm_name = "exception",
        .description = "Enable Xtensa Exception option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.extendedl32r)] = .{
        .llvm_name = "extendedl32r",
        .description = "Enable Xtensa Extended L32R option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fp)] = .{
        .llvm_name = "fp",
        .description = "Enable Xtensa Single FP instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.highpriinterrupts)] = .{
        .llvm_name = "highpriinterrupts",
        .description = "Enable Xtensa HighPriInterrupts option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.highpriinterrupts_level3)] = .{
        .llvm_name = "highpriinterrupts-level3",
        .description = "Enable Xtensa HighPriInterrupts Level3",
        .dependencies = featureSet(&[_]Feature{
            .highpriinterrupts,
        }),
    };
    result[@intFromEnum(Feature.highpriinterrupts_level4)] = .{
        .llvm_name = "highpriinterrupts-level4",
        .description = "Enable Xtensa HighPriInterrupts Level4",
        .dependencies = featureSet(&[_]Feature{
            .highpriinterrupts,
        }),
    };
    result[@intFromEnum(Feature.highpriinterrupts_level5)] = .{
        .llvm_name = "highpriinterrupts-level5",
        .description = "Enable Xtensa HighPriInterrupts Level5",
        .dependencies = featureSet(&[_]Feature{
            .highpriinterrupts,
        }),
    };
    result[@intFromEnum(Feature.highpriinterrupts_level6)] = .{
        .llvm_name = "highpriinterrupts-level6",
        .description = "Enable Xtensa HighPriInterrupts Level6",
        .dependencies = featureSet(&[_]Feature{
            .highpriinterrupts,
        }),
    };
    result[@intFromEnum(Feature.highpriinterrupts_level7)] = .{
        .llvm_name = "highpriinterrupts-level7",
        .description = "Enable Xtensa HighPriInterrupts Level7",
        .dependencies = featureSet(&[_]Feature{
            .highpriinterrupts,
        }),
    };
    result[@intFromEnum(Feature.interrupt)] = .{
        .llvm_name = "interrupt",
        .description = "Enable Xtensa Interrupt option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.loop)] = .{
        .llvm_name = "loop",
        .description = "Enable Xtensa Loop extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mac16)] = .{
        .llvm_name = "mac16",
        .description = "Enable Xtensa MAC16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.minmax)] = .{
        .llvm_name = "minmax",
        .description = "Enable Xtensa MINMAX option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.miscsr)] = .{
        .llvm_name = "miscsr",
        .description = "Enable Xtensa Miscellaneous SR option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mul16)] = .{
        .llvm_name = "mul16",
        .description = "Enable Xtensa Mul16 option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mul32)] = .{
        .llvm_name = "mul32",
        .description = "Enable Xtensa Mul32 option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mul32high)] = .{
        .llvm_name = "mul32high",
        .description = "Enable Xtensa Mul32High option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.nsa)] = .{
        .llvm_name = "nsa",
        .description = "Enable Xtensa NSA option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.prid)] = .{
        .llvm_name = "prid",
        .description = "Enable Xtensa Processor ID option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.regprotect)] = .{
        .llvm_name = "regprotect",
        .description = "Enable Xtensa Region Protection option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.rvector)] = .{
        .llvm_name = "rvector",
        .description = "Enable Xtensa Relocatable Vector option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sext)] = .{
        .llvm_name = "sext",
        .description = "Enable Xtensa Sign Extend option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.threadptr)] = .{
        .llvm_name = "threadptr",
        .description = "Enable Xtensa THREADPTR option",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.timers1)] = .{
        .llvm_name = "timers1",
        .description = "Enable Xtensa Timers 1",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.timers2)] = .{
        .llvm_name = "timers2",
        .description = "Enable Xtensa Timers 2",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.timers3)] = .{
        .llvm_name = "timers3",
        .description = "Enable Xtensa Timers 3",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.windowed)] = .{
        .llvm_name = "windowed",
        .description = "Enable Xtensa Windowed Register option",
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
    pub const generic: CpuModel = .{
        .name = "generic",
        .llvm_name = "generic",
        .features = featureSet(&[_]Feature{}),
    };
};



---
File: /std/testing/FailingAllocator.zig
---

//! Allocator that fails after N allocations, useful for making sure out of
//! memory conditions are handled correctly.
const std = @import("../std.zig");
const mem = std.mem;
const FailingAllocator = @This();

alloc_index: usize,
resize_index: usize,
internal_allocator: mem.Allocator,
allocated_bytes: usize,
freed_bytes: usize,
allocations: usize,
deallocations: usize,
stack_addresses: [num_stack_frames]usize,
has_induced_failure: bool,
fail_index: usize,
resize_fail_index: usize,

const num_stack_frames = if (std.debug.sys_can_stack_trace) 16 else 0;

pub const Config = struct {
    /// The number of successful allocations you can expect from this allocator.
    /// The next allocation will fail.
    fail_index: usize = std.math.maxInt(usize),

    /// Number of successful resizes to expect from this allocator. The next resize will fail.
    resize_fail_index: usize = std.math.maxInt(usize),
};

pub fn init(internal_allocator: mem.Allocator, config: Config) FailingAllocator {
    return FailingAllocator{
        .internal_allocator = internal_allocator,
        .alloc_index = 0,
        .resize_index = 0,
        .allocated_bytes = 0,
        .freed_bytes = 0,
        .allocations = 0,
        .deallocations = 0,
        .stack_addresses = undefined,
        .has_induced_failure = false,
        .fail_index = config.fail_index,
        .resize_fail_index = config.resize_fail_index,
    };
}

pub fn allocator(self: *FailingAllocator) mem.Allocator {
    return .{
        .ptr = self,
        .vtable = &.{
            .alloc = alloc,
            .resize = resize,
            .remap = remap,
            .free = free,
        },
    };
}

fn alloc(
    ctx: *anyopaque,
    len: usize,
    alignment: mem.Alignment,
    return_address: usize,
) ?[*]u8 {
    const self: *FailingAllocator = @ptrCast(@alignCast(ctx));
    if (self.alloc_index == self.fail_index) {
        if (!self.has_induced_failure) {
            const st = std.debug.captureCurrentStackTrace(.{ .first_address = return_address }, &self.stack_addresses);
            @memset(self.stack_addresses[@min(st.return_addresses.len, self.stack_addresses.len)..], 0);
            self.has_induced_failure = true;
        }
        return null;
    }
    const result = self.internal_allocator.rawAlloc(len, alignment, return_address) orelse
        return null;
    self.allocated_bytes += len;
    self.allocations += 1;
    self.alloc_index += 1;
    return result;
}

fn resize(
    ctx: *anyopaque,
    memory: []u8,
    alignment: mem.Alignment,
    new_len: usize,
    ra: usize,
) bool {
    const self: *FailingAllocator = @ptrCast(@alignCast(ctx));
    if (self.resize_index == self.resize_fail_index)
        return false;
    if (!self.internal_allocator.rawResize(memory, alignment, new_len, ra))
        return false;
    if (new_len < memory.len) {
        self.freed_bytes += memory.len - new_len;
    } else {
        self.allocated_bytes += new_len - memory.len;
    }
    self.resize_index += 1;
    return true;
}

fn remap(
    ctx: *anyopaque,
    memory: []u8,
    alignment: mem.Alignment,
    new_len: usize,
    ra: usize,
) ?[*]u8 {
    const self: *FailingAllocator = @ptrCast(@alignCast(ctx));
    if (self.resize_index == self.resize_fail_index) return null;
    const new_ptr = self.internal_allocator.rawRemap(memory, alignment, new_len, ra) orelse return null;
    if (new_len < memory.len) {
        self.freed_bytes += memory.len - new_len;
    } else {
        self.allocated_bytes += new_len - memory.len;
    }
    self.resize_index += 1;
    return new_ptr;
}

fn free(
    ctx: *anyopaque,
    old_mem: []u8,
    alignment: mem.Alignment,
    ra: usize,
) void {
    const self: *FailingAllocator = @ptrCast(@alignCast(ctx));
    self.internal_allocator.rawFree(old_mem, alignment, ra);
    self.deallocations += 1;
    self.freed_bytes += old_mem.len;
}

/// Only valid once `has_induced_failure == true`
pub fn getStackTrace(self: *FailingAllocator) std.debug.StackTrace {
    std.debug.assert(self.has_induced_failure);
    var len: usize = 0;
    while (len < self.stack_addresses.len and self.stack_addresses[len] != 0) {
        len += 1;
    }
    return .{
        .return_addresses = self.stack_addresses[0..len],
        .skipped = if (len == self.stack_addresses.len) .unknown else .none,
    };
}

test FailingAllocator {
    // Fail on allocation
    {
        var failing_allocator_state = FailingAllocator.init(std.testing.allocator, .{
            .fail_index = 2,
        });
        const failing_alloc = failing_allocator_state.allocator();

        const a = try failing_alloc.create(i32);
        defer failing_alloc.destroy(a);
        const b = try failing_alloc.create(i32);
        defer failing_alloc.destroy(b);
        try std.testing.expectError(error.OutOfMemory, failing_alloc.create(i32));
    }
    // Fail on resize
    {
        var failing_allocator_state = FailingAllocator.init(std.testing.allocator, .{
            .resize_fail_index = 1,
        });
        const failing_alloc = failing_allocator_state.allocator();

        const resized_slice = blk: {
            const slice = try failing_alloc.alloc(u8, 8);
            errdefer failing_alloc.free(slice);

            break :blk failing_alloc.remap(slice, 6) orelse return error.UnexpectedRemapFailure;
        };
        defer failing_alloc.free(resized_slice);

        // Remap and resize should fail from here on out
        try std.testing.expectEqual(null, failing_alloc.remap(resized_slice, 4));
        try std.testing.expectEqual(false, failing_alloc.resize(resized_slice, 4));

        // Note: realloc could succeed because it falls back to free+alloc
    }
}



---
File: /std/testing/Smith.zig
---

//! Used in conjuncation with `std.testing.fuzz` to generate values

const builtin = @import("builtin");
const std = @import("../std.zig");
const assert = std.debug.assert;
const fuzz_abi = std.Build.abi.fuzz;
const Smith = @This();

/// Null if the fuzzer is being used, in which case this struct will not be mutated.
///
/// Intended to be initialized directly.
in: ?[]const u8,

pub const Weight = fuzz_abi.Weight;

fn intUid(hash: u32) fuzz_abi.Uid {
    @disableInstrumentation();
    return @bitCast(hash << 1);
}

fn bytesUid(hash: u32) fuzz_abi.Uid {
    @disableInstrumentation();
    return @bitCast(hash | 1);
}

fn Backing(T: type) type {
    return @Int(.unsigned, @bitSizeOf(T));
}

fn toExcessK(T: type, x: T) Backing(T) {
    return @bitCast(x -% std.math.minInt(T));
}

fn fromExcessK(T: type, x: Backing(T)) T {
    return @as(T, @bitCast(x)) +% std.math.minInt(T);
}

fn enumFieldLessThan(_: void, a: std.builtin.Type.EnumField, b: std.builtin.Type.EnumField) bool {
    return a.value < b.value;
}

/// Returns an array of weights containing each possible value of `T`.
//
// `inline` to propogate the `comptime`ness of the result
pub inline fn baselineWeights(T: type) []const Weight {
    return comptime switch (@typeInfo(T)) {
        .bool, .int, .float => i: {
            // Reject types that don't have a fixed bitsize (esp. usize)
            // since they are not gauraunteed to fit in a u64 across targets.
            if (std.mem.indexOfScalar(type, &.{
                isize,      usize,
                c_char,     c_longdouble,
                c_short,    c_ushort,
                c_int,      c_uint,
                c_long,     c_ulong,
                c_longlong, c_ulonglong,
            }, T) != null) {
                @compileError("type does not have a fixed bitsize: " ++ @typeName(T));
            }
            break :i &.{.rangeAtMost(Backing(T), 0, (1 << @bitSizeOf(T)) - 1, 1)};
        },
        .@"struct" => |s| if (s.backing_integer) |B|
            baselineWeights(B)
        else
            @compileError("non-packed structs cannot be weighted"),
        .@"union" => |u| if (u.layout == .@"packed")
            baselineWeights(Backing(T))
        else
            @compileError("non-packed unions cannot be weighted"),
        .@"enum" => |e| if (!e.is_exhaustive)
            baselineWeights(e.tag_type)
        else if (e.fields.len == 0)
            // Cannot be included in below branch due to `log2_int_ceil`
            @compileError("exhaustive zero-field enums cannot be weighted")
        else e: {
            @setEvalBranchQuota(@intCast(4 * e.fields.len *
                std.math.log2_int_ceil(usize, e.fields.len)));

            var sorted_fields = e.fields[0..e.fields.len].*;
            std.mem.sortUnstable(std.builtin.Type.EnumField, &sorted_fields, {}, enumFieldLessThan);

            var weights: []const Weight = &.{};
            var seq_first: u64 = sorted_fields[0].value;
            for (sorted_fields[0 .. sorted_fields.len - 1], sorted_fields[1..]) |prev, field| {
                if (field.value != prev.value + 1) {
                    weights = weights ++ .{Weight.rangeAtMost(u64, seq_first, prev.value, 1)};
                    seq_first = field.value;
                }
            }
            weights = weights ++ .{Weight.rangeAtMost(
                u64,
                seq_first,
                sorted_fields[sorted_fields.len - 1].value,
                1,
            )};

            break :e weights;
        },
        else => @compileError("unexpected type: " ++ @typeName(T)),
    };
}

test baselineWeights {
    try std.testing.expectEqualSlices(
        Weight,
        &.{.rangeAtMost(bool, false, true, 1)},
        baselineWeights(bool),
    );
    try std.testing.expectEqualSlices(
        Weight,
        &.{.rangeAtMost(u4, 0, 15, 1)},
        baselineWeights(u4),
    );
    try std.testing.expectEqualSlices(
        Weight,
        &.{.rangeAtMost(u4, 0, 15, 1)},
        baselineWeights(i4),
    );
    try std.testing.expectEqualSlices(
        Weight,
        &.{.rangeAtMost(u16, 0, 0xffff, 1)},
        baselineWeights(f16),
    );
    try std.testing.expectEqualSlices(
        Weight,
        &.{.rangeAtMost(u4, 0, 15, 1)},
        baselineWeights(packed struct(u4) { _: u4 }),
    );
    try std.testing.expectEqualSlices(
        Weight,
        &.{.rangeAtMost(u4, 0, 15, 1)},
        baselineWeights(packed union { _: u4 }),
    );
    try std.testing.expectEqualSlices(
        Weight,
        &.{.rangeAtMost(u4, 0, 15, 1)},
        baselineWeights(enum(u4) { _ }),
    );
    try std.testing.expectEqualSlices(Weight, &.{
        .rangeAtMost(u4, 0, 1, 1),
        .value(u4, 3, 1),
        .value(u4, 5, 1),
        .rangeAtMost(u4, 8, 10, 1),
    }, baselineWeights(enum(u4) {
        a = 1,
        b = 5,
        c = 8,
        d = 3,
        e = 0,
        f = 9,
        g = 10,
    }));
}

fn valueFromInt(T: anytype, int: Backing(T)) T {
    @disableInstrumentation();
    return switch (@typeInfo(T)) {
        .@"enum" => @enumFromInt(int),
        else => @bitCast(int),
    };
}

fn checkWeights(weights: []const Weight, max_incl: u64) void {
    @disableInstrumentation();
    const w0 = weights[0]; // Sum of weights is zero
    assert(w0.weight != 0);
    assert(w0.max <= max_incl);

    var incl_sum: u64 = (w0.max - w0.min) * w0.weight + (w0.weight - 1); // Sum of weights greater than 2^64
    for (weights[1..]) |w| {
        assert(w.weight != 0);
        assert(w.max <= max_incl);
        // This addition will not overflow except with an illegal combination of weights since
        // the exclusive sum must be at least one so a span of all values is impossible.
        incl_sum += (w.max - w.min + 1) * w.weight; // Sum of weights greater than 2^64
    }
}

// `inline` to propogate callee's unique return address
inline fn firstHash() u32 {
    return @truncate(std.hash.int(@returnAddress()));
}

// `noinline` to capture a unique return address
pub noinline fn value(s: *Smith, T: type) T {
    @disableInstrumentation();
    return s.valueWithHash(T, firstHash());
}

// `noinline` to capture a unique return address
pub noinline fn valueWeighted(s: *Smith, T: type, weights: []const Weight) T {
    @disableInstrumentation();
    return s.valueWeightedWithHash(T, weights, firstHash());
}

// `noinline` to capture a unique return address
pub noinline fn valueRangeAtMost(s: *Smith, T: type, at_least: T, at_most: T) T {
    @disableInstrumentation();
    return s.valueRangeAtMostWithHash(T, at_least, at_most, firstHash());
}

// `noinline` to capture a unique return address
pub noinline fn valueRangeLessThan(s: *Smith, T: type, at_least: T, less_than: T) T {
    @disableInstrumentation();
    return s.valueRangeLessThanWithHash(T, at_least, less_than, firstHash());
}

/// It is asserted `len` is nonzero.
/// It is asserted `len` fits within 64 bits.
//
// `noinline` to capture a unique return address
pub noinline fn index(s: *Smith, len: usize) usize {
    @disableInstrumentation();
    return s.indexWithHash(len, firstHash());
}

/// It is asserted that the weight of `false` is non-zero.
/// It is asserted that the weight of `true` is non-zero.
//
// `noinline` to capture a unique return address
pub noinline fn boolWeighted(s: *Smith, false_weight: u64, true_weight: u64) bool {
    @disableInstrumentation();
    return s.boolWeightedWithHash(false_weight, true_weight, firstHash());
}

/// This is similar to `value(bool)` however it is gauraunteed to eventually
/// return `true` and provides the fuzzer with an extra hint about the data.
//
// `noinline` to capture a unique return address
pub noinline fn eos(s: *Smith) bool {
    @disableInstrumentation();
    return s.eosWithHash(firstHash());
}

/// This is similar to `value(bool)` however it is gauraunteed to eventually
/// return `true` and provides the fuzzer with an extra hint about the data.
///
/// It is asserted that the weight of `true` is non-zero.
//
// `noinline` to capture a unique return address
pub noinline fn eosWeighted(s: *Smith, weights: []const Weight) bool {
    @disableInstrumentation();
    return s.eosWeightedWithHash(weights, firstHash());
}

/// This is similar to `value(bool)` however it is gauraunteed to eventually
/// return `true` and provides the fuzzer with an extra hint about the data.
///
/// It is asserted that the weight of `false` is non-zero.
/// It is asserted that the weight of `true` is non-zero.
//
// `noinline` to capture a unique return address
pub noinline fn eosWeightedSimple(s: *Smith, false_weight: u64, true_weight: u64) bool {
    @disableInstrumentation();
    return s.eosWeightedSimpleWithHash(false_weight, true_weight, firstHash());
}

// `noinline` to capture a unique return address
pub noinline fn bytes(s: *Smith, out: []u8) void {
    @disableInstrumentation();
    return s.bytesWithHash(out, firstHash());
}

// `noinline` to capture a unique return address
pub noinline fn bytesWeighted(s: *Smith, out: []u8, weights: []const Weight) void {
    @disableInstrumentation();
    return s.bytesWeightedWithHash(out, weights, firstHash());
}

/// Returns the length of the filled slice
///
/// It is asserted that `buf.len` fits within a u32
// `noinline` to capture a unique return address
pub noinline fn slice(s: *Smith, buf: []u8) u32 {
    @disableInstrumentation();
    return s.sliceWithHash(buf, firstHash());
}

/// Returns the length of the filled slice
///
/// It is asserted that `buf.len` fits within a u32
//
// `noinline` to capture a unique return address
pub noinline fn sliceWeightedBytes(s: *Smith, buf: []u8, byte_weights: []const Weight) u32 {
    @disableInstrumentation();
    return s.sliceWeightedBytesWithHash(buf, byte_weights, firstHash());
}

/// Returns the length of the filled slice
///
/// It is asserted that `buf.len` fits within a u32
//
// `noinline` to capture a unique return address
pub noinline fn sliceWeighted(
    s: *Smith,
    buf: []u8,
    len_weights: []const Weight,
    byte_weights: []const Weight,
) u32 {
    @disableInstrumentation();
    return s.sliceWeightedWithHash(buf, len_weights, byte_weights, firstHash());
}

fn weightsContain(int: u64, weights: []const Weight) bool {
    @disableInstrumentation();
    var contains: bool = false;
    for (weights) |w| {
        contains |= w.min <= int and int <= w.max;
    }
    return contains;
}

/// Asserts `T` can be a member of a packed type
//
// `inline` to propogate the `comptime`ness of the result
inline fn allBitPatternsValid(T: type) bool {
    return comptime switch (@typeInfo(T)) {
        .void, .bool, .int, .float => true,
        inline .@"struct", .@"union" => |c| c.layout == .@"packed" and for (c.fields) |f| {
            if (!allBitPatternsValid(f.type)) break false;
        } else true,
        .@"enum" => |e| !e.is_exhaustive,
        else => unreachable,
    };
}

test allBitPatternsValid {
    try std.testing.expect(allBitPatternsValid(packed struct {
        a: void,
        b: u8,
        c: f16,
        d: packed union {
            a: u16,
            b: i16,
            c: f16,
        },
        e: enum(u4) { _ },
    }));
    try std.testing.expect(!allBitPatternsValid(packed union {
        a: i4,
        b: enum(u4) { a },
    }));
}

fn UnionTagWithoutUninitializable(T: type) type {
    const u = @typeInfo(T).@"union";
    const Tag = u.tag_type orelse @compileError("union must have tag");
    const e = @typeInfo(Tag).@"enum";
    var field_names: [e.fields.len][]const u8 = undefined;
    var field_values: [e.fields.len]e.tag_type = undefined;
    var n_fields = 0;
    for (u.fields) |f| {
        switch (f.type) {
            noreturn => continue,
            else => {},
        }
        field_names[n_fields] = f.name;
        field_values[n_fields] = @intFromEnum(@field(Tag, f.name));
        n_fields += 1;
    }
    return @Enum(e.tag_type, .exhaustive, field_names[0..n_fields], field_values[0..n_fields]);
}

pub fn valueWithHash(s: *Smith, T: type, hash: u32) T {
    @disableInstrumentation();
    return switch (@typeInfo(T)) {
        .void => {},
        .bool, .int, .float => full: {
            var int: Backing(T) = 0;
            comptime var biti = 0;
            var rhash = hash; // 'running' hash
            inline while (biti < @bitSizeOf(T)) {
                const n = @min(@bitSizeOf(T) - biti, 64);
                const P = @Int(.unsigned, n);
                int |= @as(
                    @TypeOf(int),
                    s.valueWeightedWithHash(P, baselineWeights(P), rhash),
                ) << biti;
                biti += n;
                rhash = std.hash.int(rhash);
            }
            break :full @bitCast(int);
        },
        .@"enum" => |e| if (e.is_exhaustive) v: {
            if (@bitSizeOf(e.tag_type) <= 64) {
                break :v s.valueWeightedWithHash(T, baselineWeights(T), hash);
            }
            break :v std.enums.fromInt(T, s.valueWithHash(e.tag_type, hash)) orelse
                @enumFromInt(e.fields[0].value);
        } else @enumFromInt(s.valueWithHash(e.tag_type, hash)),
        .optional => |o| if (s.valueWithHash(bool, hash))
            null
        else
            s.valueWithHash(o.child, std.hash.int(hash)),
        inline .array, .vector => |a| arr: {
            var arr: [a.len]a.child = undefined; // `T` cannot be used due to the vector case
            if (a.child != u8) {
                for (&arr) |*v| {
                    v.* = s.valueWithHash(a.child, hash);
                }
            } else {
                s.bytesWithHash(&arr, hash);
            }
            break :arr arr;
        },
        .@"struct" => |st| if (!allBitPatternsValid(T)) v: {
            var v: T = undefined;
            var rhash = hash;
            inline for (st.fields) |f| {
                // rhash is incremented in the call so our rhash state is not reused (e.g. with
                // two nested structs. note that xor cannot work for this case as the bit would
                // be flipped back here)
                @field(v, f.name) = s.valueWithHash(f.type, rhash +% 1);
                rhash = std.hash.int(rhash);
            }
            break :v v;
        } else @bitCast(s.valueWithHash(st.backing_integer.?, hash)),
        .@"union" => if (!allBitPatternsValid(T))
            switch (s.valueWithHash(
                UnionTagWithoutUninitializable(T),
                // hash is incremented in the call so our hash state is not reused for below
                std.hash.int(hash +% 1),
            )) {
                inline else => |t| @unionInit(
                    T,
                    @tagName(t),
                    s.valueWithHash(@FieldType(T, @tagName(t)), hash),
                ),
            }
        else
            @bitCast(s.valueWithHash(Backing(T), hash)),
        else => @compileError("unexpected type '" ++ @typeName(T) ++ "'"),
    };
}

pub fn valueWeightedWithHash(s: *Smith, T: type, weights: []const Weight, hash: u32) T {
    @disableInstrumentation();
    checkWeights(weights, (1 << @bitSizeOf(T)) - 1);
    return valueFromInt(T, @intCast(s.valueWeightedWithHashInner(weights, hash)));
}

fn valueWeightedWithHashInner(s: *Smith, weights: []const Weight, hash: u32) u64 {
    @disableInstrumentation();
    return if (s.in) |*in| int: {
        if (in.len < 8) {
            @branchHint(.unlikely);
            in.* = &.{};
            break :int weights[0].min;
        }
        const int = std.mem.readInt(u64, in.*[0..8], .little);
        in.* = in.*[8..];
        break :int if (weightsContain(int, weights)) int else weights[0].min;
    } else if (builtin.fuzz) int: {
        @branchHint(.likely);
        break :int fuzz_abi.fuzzer_int(intUid(hash), .fromSlice(weights));
    } else unreachable;
}

pub fn valueRangeAtMostWithHash(s: *Smith, T: type, at_least: T, at_most: T, hash: u32) T {
    @disableInstrumentation();
    if (@typeInfo(T) == .int and @typeInfo(T).int.signedness == .signed) {
        return fromExcessK(T, s.valueRangeAtMostWithHash(
            Backing(T),
            toExcessK(T, at_least),
            toExcessK(T, at_most),
            hash,
        ));
    }
    return s.valueWeightedWithHash(T, &.{.rangeAtMost(T, at_least, at_most, 1)}, hash);
}

pub fn valueRangeLessThanWithHash(s: *Smith, T: type, at_least: T, less_than: T, hash: u32) T {
    @disableInstrumentation();
    if (@typeInfo(T) == .int and @typeInfo(T).int.signedness == .signed) {
        return fromExcessK(T, s.valueRangeLessThanWithHash(
            Backing(T),
            toExcessK(T, at_least),
            toExcessK(T, less_than),
            hash,
        ));
    }
    return s.valueWeightedWithHash(T, &.{.rangeLessThan(T, at_least, less_than, 1)}, hash);
}

/// It is asserted `len` is nonzero.
/// It is asserted `len` fits within 64 bits.
pub fn indexWithHash(s: *Smith, len: usize, hash: u32) usize {
    @disableInstrumentation();
    assert(len != 0);
    return @intCast(s.valueWeightedWithHash(u64, &.{.rangeLessThan(u64, 0, @intCast(len), 1)}, hash));
}

/// It is asserted that the weight of `false` is non-zero.
/// It is asserted that the weight of `true` is non-zero.
pub fn boolWeightedWithHash(s: *Smith, false_weight: u64, true_weight: u64, hash: u32) bool {
    @disableInstrumentation();
    return s.valueWeightedWithHash(bool, &.{
        .value(bool, false, false_weight),
        .value(bool, true, true_weight),
    }, hash);
}

/// This is similar to `value(bool)` however it is gauraunteed to eventually
/// return `true` and provides the fuzzer with an extra hint about the data.
pub fn eosWithHash(s: *Smith, hash: u32) bool {
    @disableInstrumentation();
    return s.eosWeightedWithHash(baselineWeights(bool), hash);
}

/// This is similar to `value(bool)` however it is gauraunteed to eventually
/// return `true` and provides the fuzzer with an extra hint about the data.
///
/// It is asserted that the weight of `true` is non-zero.
pub fn eosWeightedWithHash(s: *Smith, weights: []const Weight, hash: u32) bool {
    @disableInstrumentation();
    checkWeights(weights, 1);
    for (weights) |w| (if (w.max == 1) break) else unreachable; // `true` must have non-zero weight

    if (s.in) |*in| {
        if (in.len == 0) {
            @branchHint(.unlikely);
            return true;
        }
        const eos_val = in.*[0] != 0;
        in.* = in.*[1..];
        return eos_val or b: {
            var only_true: bool = true;
            for (weights) |w| {
                only_true &= @as(u1, @intCast(w.min)) == 1;
            }
            break :b only_true;
        };
    } else if (builtin.fuzz) {
        @branchHint(.likely);
        return fuzz_abi.fuzzer_eos(intUid(hash), .fromSlice(weights));
    } else unreachable;
}

/// This is similar to `value(bool)` however it is gauraunteed to eventually
/// return `true` and provides the fuzzer with an extra hint about the data.
///
/// It is asserted that the weight of `false` is non-zero.
/// It is asserted that the weight of `true` is non-zero.
pub fn eosWeightedSimpleWithHash(s: *Smith, false_weight: u64, true_weight: u64, hash: u32) bool {
    @disableInstrumentation();
    return s.eosWeightedWithHash(&.{
        .value(bool, false, false_weight),
        .value(bool, true, true_weight),
    }, hash);
}

pub fn bytesWithHash(s: *Smith, out: []u8, hash: u32) void {
    @disableInstrumentation();
    return s.bytesWeightedWithHash(out, baselineWeights(u8), hash);
}

pub fn bytesWeightedWithHash(s: *Smith, out: []u8, weights: []const Weight, hash: u32) void {
    @disableInstrumentation();
    checkWeights(weights, 255);

    if (s.in) |*in| {
        var present_weights: [256]bool = @splat(false);
        for (weights) |w| {
            @memset(present_weights[@intCast(w.min)..@intCast(w.max + 1)], true);
        }
        const default: u8 = @intCast(weights[0].min);

        const copy_len = @min(out.len, in.len);
        for (in.*[0..copy_len], out[0..copy_len]) |i, *o| {
            o.* = if (present_weights[i]) i else default;
        }
        in.* = in.*[copy_len..];
        @memset(out[copy_len..], default);
    } else if (builtin.fuzz) {
        @branchHint(.likely);
        fuzz_abi.fuzzer_bytes(bytesUid(hash), .fromSlice(out), .fromSlice(weights));
    } else unreachable;
}

/// Returns the length of the filled slice
///
/// It is asserted that `buf.len` fits within a u32
pub fn sliceWithHash(s: *Smith, buf: []u8, hash: u32) u32 {
    @disableInstrumentation();
    return s.sliceWeightedBytesWithHash(buf, baselineWeights(u8), hash);
}

/// Returns the length of the filled slice
///
/// It is asserted that `buf.len` fits within a u32
pub fn sliceWeightedBytesWithHash(
    s: *Smith,
    buf: []u8,
    byte_weights: []const Weight,
    hash: u32,
) u32 {
    @disableInstrumentation();
    return s.sliceWeightedWithHash(
        buf,
        &.{.rangeAtMost(u32, 0, @intCast(buf.len), 1)},
        byte_weights,
        hash,
    );
}

/// Returns the length of the filled slice
///
/// It is asserted that `buf.len` fits within a u32
pub fn sliceWeightedWithHash(
    s: *Smith,
    buf: []u8,
    len_weights: []const Weight,
    byte_weights: []const Weight,
    hash: u32,
) u32 {
    @disableInstrumentation();
    checkWeights(byte_weights, 255);
    checkWeights(len_weights, @as(u32, @intCast(buf.len)));

    if (s.in) |*in| {
        const in_len = len: {
            if (in.len < 4) {
                @branchHint(.unlikely);
                in.* = &.{};
                break :len 0;
            }
            const len = std.mem.readInt(u32, in.*[0..4], .little);
            in.* = in.*[4..];
            break :len @min(len, in.len);
        };
        const out_len: u32 = if (weightsContain(in_len, len_weights))
            in_len
        else
            @intCast(len_weights[0].min);

        var present_weights: [256]bool = @splat(false);
        for (byte_weights) |w| {
            @memset(present_weights[@intCast(w.min)..@intCast(w.max + 1)], true);
        }
        const default: u8 = @intCast(byte_weights[0].min);

        const copy_len = @min(out_len, in_len);
        for (in.*[0..copy_len], buf[0..copy_len]) |i, *o| {
            o.* = if (present_weights[i]) i else default;
        }
        in.* = in.*[in_len..];
        @memset(buf[copy_len..], default);
        return out_len;
    } else if (builtin.fuzz) {
        @branchHint(.likely);
        return fuzz_abi.fuzzer_slice(
            bytesUid(hash),
            .fromSlice(buf),
            .fromSlice(len_weights),
            .fromSlice(byte_weights),
        );
    } else unreachable;
}

fn constructInput(comptime values: []const union(enum) {
    eos: bool,
    int: u64,
    bytes: []const u8,
    slice: []const u8,
}) []const u8 {
    const result = comptime result: {
        var result: [
            len: {
                var len = 0;
                for (values) |v| len += switch (v) {
                    .eos => 1,
                    .int => 8,
                    .bytes => |b| b.len,
                    .slice => |s| 4 + s.len,
                };
                break :len len;
            }
        ]u8 = undefined;
        var w: std.Io.Writer = .fixed(&result);

        for (values) |v| switch (v) {
            .eos => |e| w.writeByte(@intFromBool(e)) catch unreachable,
            .int => |i| w.writeInt(u64, i, .little) catch unreachable,
            .bytes => |b| w.writeAll(b) catch unreachable,
            .slice => |s| {
                w.writeInt(u32, @intCast(s.len), .little) catch unreachable;
                w.writeAll(s) catch unreachable;
            },
        };

        break :result result;
    };
    return &result;
}

test value {
    if (@import("builtin").zig_backend == .stage2_c) return error.SkipZigTest; // TODO

    const S = struct {
        v: void = {},
        b: bool = true,
        ih: u16 = 123,
        iq: u64 = 55555,
        io: u128 = (1 << 80) | (1 << 23),
        fd: f64 = std.math.pi,
        ft: f80 = std.math.e,
        eh: enum(u16) { a, _ } = @enumFromInt(999),
        eo: enum(u128) { a, b, _ } = .b,
        aw: [3]u32 = .{ 1 << 30, 1 << 20, 1 << 10 },
        vw: @Vector(3, u32) = .{ 1 << 10, 1 << 20, 1 << 30 },
        ab: [3]u8 = .{ 55, 33, 88 },
        vb: @Vector(3, u8) = .{ 22, 44, 99 },
        s: struct { q: u64 } = .{ .q = 1 },
        sz: struct {} = .{},
        sp: packed struct(u8) { a: u5, b: u3 } = .{ .a = 31, .b = 3 },
        si: packed struct(u8) { a: u5, b: enum(u3) { a, b } } = .{ .a = 15, .b = .b },
        u: union(enum(u2)) {
            a: u64,
            b: u64,
            c: noreturn,
        } = .{ .b = 777777 },
        up: packed union {
            a: u16,
            b: f16,
        } = .{ .b = std.math.phi },

        invalid: struct {
            ib: u8 = 0,
            eb: enum(u8) { a, b } = .a,
            eo: enum(u128) { a, b } = .a,
            u: union(enum(u1)) { a: noreturn, b: void } = .{ .b = {} },
        } = .{},
    };
    const s: S = .{};
    const ft_bits: u80 = @bitCast(s.ft);
    const eo_bits = @intFromEnum(s.eo);

    var smith: Smith = .{
        .in = constructInput(&.{
            // v
            .{ .int = @intFromBool(s.b) }, // b
            .{ .int = s.ih }, // ih
            .{ .int = s.iq }, // iq
            .{ .int = @truncate(s.io) }, .{ .int = @intCast(s.io >> 64) }, // io
            .{ .int = @bitCast(s.fd) }, // fd
            .{ .int = @truncate(ft_bits) }, .{ .int = @intCast(ft_bits >> 64) }, // ft
            .{ .int = @intFromEnum(s.eh) }, // eh
            .{ .int = @truncate(eo_bits) }, .{ .int = @intCast(eo_bits >> 64) }, // eo
            .{ .int = s.aw[0] }, .{ .int = s.aw[1] }, .{ .int = s.aw[2] }, // aw
            .{ .int = s.vw[0] }, .{ .int = s.vw[1] }, .{ .int = s.vw[2] }, // vw
            .{ .bytes = &s.ab }, // ab
            .{ .bytes = &@as([3]u8, s.vb) }, // vb
            .{ .int = s.s.q }, // s.q
            //sz
            .{ .int = @as(u8, @bitCast(s.sp)) }, // sp
            .{ .int = s.si.a }, .{ .int = @intFromEnum(s.si.b) }, // si
            .{ .int = @intFromEnum(s.u) }, .{ .int = s.u.b }, // u
            .{ .int = @as(u16, @bitCast(s.up)) }, // up
            // invalid values
            .{ .int = 555 }, // invalid.ib
            .{ .int = 123 }, // invalid.eb
            .{ .int = 0 }, .{ .int = 1 }, // invalid.eo
            .{ .int = 0 }, // invalid.u
        }),
    };

    try std.testing.expectEqual(s, smith.value(S));
}

test valueWeighted {
    var smith: Smith = .{
        .in = constructInput(&.{
            .{ .int = 200 },
            .{ .int = 200 },
            .{ .int = 300 },
            .{ .int = 400 },
        }),
    };

    try std.testing.expectEqual(200, smith.valueWeighted(u8, &.{.rangeAtMost(u8, 50, 200, 1)}));
    try std.testing.expectEqual(50, smith.valueWeighted(u8, &.{.rangeLessThan(u8, 50, 200, 1)}));
    const E = enum(u64) { a = 100, b = 200, c = 300 };
    try std.testing.expectEqual(E.c, smith.valueWeighted(E, baselineWeights(E)));
    try std.testing.expectEqual(E.a, smith.valueWeighted(E, baselineWeights(E)));
    try std.testing.expectEqual(12345, smith.valueWeighted(u64, &.{.value(u64, 12345, 1)}));
}

test valueRangeAtMost {
    var smith: Smith = .{
        .in = constructInput(&.{
            .{ .int = 100 },
            .{ .int = 100 },
            .{ .int = 200 },
            .{ .int = 100 },
            .{ .int = 200 },
            .{ .int = 0 },
        }),
    };
    try std.testing.expectEqual(100, smith.valueRangeAtMost(u8, 0, 250));
    try std.testing.expectEqual(100, smith.valueRangeAtMost(u8, 100, 100));
    try std.testing.expectEqual(0, smith.valueRangeAtMost(u8, 0, 100));
    try std.testing.expectEqual(100 - 128, smith.valueRangeAtMost(i8, -100, 100));
    try std.testing.expectEqual(200 - 128, smith.valueRangeAtMost(i8, -100, 100));
    try std.testing.expectEqual(-100, smith.valueRangeAtMost(i8, -100, 100));
}

test valueRangeLessThan {
    var smith: Smith = .{
        .in = constructInput(&.{
            .{ .int = 100 },
            .{ .int = 100 },
            .{ .int = 100 },
            .{ .int = 100 + 128 },
        }),
    };
    try std.testing.expectEqual(100, smith.valueRangeLessThan(u8, 0, 250));
    try std.testing.expectEqual(0, smith.valueRangeLessThan(u8, 0, 100));
    try std.testing.expectEqual(100 - 128, smith.valueRangeLessThan(i8, -100, 100));
    try std.testing.expectEqual(-100, smith.valueRangeLessThan(i8, -100, 100));
}

test eos {
    var smith: Smith = .{
        .in = constructInput(&.{
            .{ .eos = false },
            .{ .eos = true },
        }),
    };
    try std.testing.expect(!smith.eos());
    try std.testing.expect(smith.eos());
    try std.testing.expect(smith.eos());
}

test eosWeighted {
    var smith: Smith = .{ .in = constructInput(&.{.{ .eos = false }}) };
    try std.testing.expect(smith.eosWeighted(&.{.value(bool, true, std.math.maxInt(u64))}));
}

test bytes {
    var smith: Smith = .{ .in = constructInput(&.{
        .{ .bytes = "testing!" },
        .{ .bytes = "ab" },
    }) };
    var buf: [8]u8 = undefined;

    smith.bytes(&buf);
    try std.testing.expectEqualSlices(u8, "testing!", &buf);
    smith.bytes(buf[0..0]);
    smith.bytes(buf[0..3]);
    try std.testing.expectEqualSlices(u8, "ab\x00", buf[0..3]);
}

test bytesWeighted {
    var smith: Smith = .{ .in = constructInput(&.{
        .{ .bytes = "testing!" },
        .{ .bytes = "ab" },
    }) };
    const weights: []const Weight = &.{.rangeAtMost(u8, 'a', 'z', 1)};
    var buf: [8]u8 = undefined;

    smith.bytesWeighted(&buf, weights);
    try std.testing.expectEqualSlices(u8, "testinga", &buf);
    smith.bytesWeighted(buf[0..0], weights);
    smith.bytesWeighted(buf[0..3], weights);
    try std.testing.expectEqualSlices(u8, "aba", buf[0..3]);
}

test slice {
    var smith: Smith = .{
        .in = constructInput(&.{
            .{ .slice = "testing!" },
            .{ .slice = "" },
            .{ .slice = "ab" },
            .{ .bytes = std.mem.asBytes(&std.mem.nativeToLittle(u32, 4)) }, // length past end
        }),
    };
    var buf: [8]u8 = undefined;

    try std.testing.expectEqualSlices(u8, "testing!", buf[0..smith.slice(&buf)]);
    try std.testing.expectEqualSlices(u8, "", buf[0..smith.slice(&buf)]);
    try std.testing.expectEqualSlices(u8, "ab", buf[0..smith.slice(&buf)]);
    try std.testing.expectEqualSlices(u8, "", buf[0..smith.slice(&buf)]);
}

test sliceWeightedBytes {
    const weights: []const Weight = &.{.rangeAtMost(u8, 'a', 'z', 1)};
    var smith: Smith = .{ .in = constructInput(&.{
        .{ .slice = "testing!" },
    }) };
    var buf: [8]u8 = undefined;

    try std.testing.expectEqualSlices(
        u8,
        "testinga",
        buf[0..smith.sliceWeightedBytes(&buf, weights)],
    );
    try std.testing.expectEqualSlices(u8, "", buf[0..smith.sliceWeightedBytes(&buf, weights)]);
}

test sliceWeighted {
    const len_weights: []const Weight = &.{.rangeAtMost(u8, 3, 6, 1)};
    const weights: []const Weight = &.{.rangeAtMost(u8, 'a', 'z', 1)};
    var smith: Smith = .{ .in = constructInput(&.{
        .{ .slice = "testing!" },
        .{ .slice = "ing!" },
        .{ .slice = "ab" },
    }) };
    var buf: [8]u8 = undefined;

    try std.testing.expectEqualSlices(
        u8,
        "tes",
        buf[0..smith.sliceWeighted(&buf, len_weights, weights)],
    );
    try std.testing.expectEqualSlices(
        u8,
        "inga",
        buf[0..smith.sliceWeighted(&buf, len_weights, weights)],
    );
    try std.testing.expectEqualSlices(
        u8,
        "aba",
        buf[0..smith.sliceWeighted(&buf, len_weights, weights)],
    );
    try std.testing.expectEqualSlices(
        u8,
        "aaa",
        buf[0..smith.sliceWeighted(&buf, len_weights, weights)],
    );
}



---
File: /std/time/epoch.zig
---

//! Epoch reference times in terms of their difference from
//! UTC 1970-01-01 in seconds.
const std = @import("../std.zig");
const testing = std.testing;
const math = std.math;

/// Jan 01, 1970 AD
pub const posix = 0;
/// Jan 01, 1980 AD
pub const dos = 315532800;
/// Jan 01, 2001 AD
pub const ios = 978307200;
/// Nov 17, 1858 AD
pub const openvms = -3506716800;
/// Jan 01, 1900 AD
pub const zos = -2208988800;
/// Jan 01, 1601 AD
pub const windows = -11644473600;
/// Jan 01, 1978 AD
pub const amiga = 252460800;
/// Dec 31, 1967 AD
pub const pickos = -63244800;
/// Jan 06, 1980 AD
pub const gps = 315964800;
/// Jan 01, 0001 AD
pub const clr = -62135769600;

pub const unix = posix;
pub const android = posix;
pub const os2 = dos;
pub const bios = dos;
pub const vfat = dos;
pub const ntfs = windows;
pub const ntp = zos;
pub const jbase = pickos;
pub const aros = amiga;
pub const morphos = amiga;
pub const brew = gps;
pub const atsc = gps;
pub const go = clr;

/// The type that holds the current year, i.e. 2016
pub const Year = u16;

pub const epoch_year = 1970;
pub const secs_per_day: u17 = 24 * 60 * 60;

pub fn isLeapYear(year: Year) bool {
    if (@mod(year, 4) != 0)
        return false;
    if (@mod(year, 100) != 0)
        return true;
    return (0 == @mod(year, 400));
}

test isLeapYear {
    try testing.expectEqual(false, isLeapYear(2095));
    try testing.expectEqual(true, isLeapYear(2096));
    try testing.expectEqual(false, isLeapYear(2100));
    try testing.expectEqual(true, isLeapYear(2400));
}

pub fn getDaysInYear(year: Year) u9 {
    return if (isLeapYear(year)) 366 else 365;
}

pub const Month = enum(u4) {
    jan = 1,
    feb,
    mar,
    apr,
    may,
    jun,
    jul,
    aug,
    sep,
    oct,
    nov,
    dec,

    /// return the numeric calendar value for the given month
    /// i.e. jan=1, feb=2, etc
    pub fn numeric(self: Month) u4 {
        return @intFromEnum(self);
    }
};

/// Get the number of days in the given month and year
pub fn getDaysInMonth(year: Year, month: Month) u5 {
    return switch (month) {
        .jan => 31,
        .feb => switch (isLeapYear(year)) {
            true => 29,
            false => 28,
        },
        .mar => 31,
        .apr => 30,
        .may => 31,
        .jun => 30,
        .jul => 31,
        .aug => 31,
        .sep => 30,
        .oct => 31,
        .nov => 30,
        .dec => 31,
    };
}

pub const YearAndDay = struct {
    year: Year,
    /// The number of days into the year (0 to 365)
    day: u9,

    pub fn calculateMonthDay(self: YearAndDay) MonthAndDay {
        var month: Month = .jan;
        var days_left = self.day;
        while (true) {
            const days_in_month = getDaysInMonth(self.year, month);
            if (days_left < days_in_month)
                break;
         
```
