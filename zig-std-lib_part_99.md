```
c,
            .sha2,
            .ssbs,
            .v8_2a,
        }),
    };
    pub const cortex_a65ae: CpuModel = .{
        .name = "cortex_a65ae",
        .llvm_name = "cortex-a65ae",
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
            .rcpc,
            .sha2,
            .ssbs,
            .v8_2a,
        }),
    };
    pub const cortex_a710: CpuModel = .{
        .name = "cortex_a710",
        .llvm_name = "cortex-a710",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .bf16,
            .cmp_bcc_fusion,
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
    pub const cortex_a715: CpuModel = .{
        .name = "cortex_a715",
        .llvm_name = "cortex-a715",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .bf16,
            .cmp_bcc_fusion,
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
            .spe,
            .sve_bitperm,
            .use_postra_scheduler,
            .v9a,
        }),
    };
    pub const cortex_a72: CpuModel = .{
        .name = "cortex_a72",
        .llvm_name = "cortex-a72",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .crc,
            .enable_select_opt,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_literals,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .v8a,
        }),
    };
    pub const cortex_a720: CpuModel = .{
        .name = "cortex_a720",
        .llvm_name = "cortex-a720",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .spe,
            .sve_bitperm,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const cortex_a720ae: CpuModel = .{
        .name = "cortex_a720ae",
        .llvm_name = "cortex-a720ae",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .spe,
            .sve_bitperm,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const cortex_a725: CpuModel = .{
        .name = "cortex_a725",
        .llvm_name = "cortex-a725",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .spe,
            .sve_bitperm,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const cortex_a73: CpuModel = .{
        .name = "cortex_a73",
        .llvm_name = "cortex-a73",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .crc,
            .enable_select_opt,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .v8a,
        }),
    };
    pub const cortex_a75: CpuModel = .{
        .name = "cortex_a75",
        .llvm_name = "cortex-a75",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .dotprod,
            .enable_select_opt,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .predictable_select_expensive,
            .rcpc,
            .sha2,
            .v8_2a,
        }),
    };
    pub const cortex_a76: CpuModel = .{
        .name = "cortex_a76",
        .llvm_name = "cortex-a76",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .dotprod,
            .enable_select_opt,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .predictable_select_expensive,
            .rcpc,
            .sha2,
            .ssbs,
            .v8_2a,
        }),
    };
    pub const cortex_a76ae: CpuModel = .{
        .name = "cortex_a76ae",
        .llvm_name = "cortex-a76ae",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .dotprod,
            .enable_select_opt,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .predictable_select_expensive,
            .rcpc,
            .sha2,
            .ssbs,
            .v8_2a,
        }),
    };
    pub const cortex_a77: CpuModel = .{
        .name = "cortex_a77",
        .llvm_name = "cortex-a77",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .dotprod,
            .enable_select_opt,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .predictable_select_expensive,
            .rcpc,
            .sha2,
            .ssbs,
            .v8_2a,
        }),
    };
    pub const cortex_a78: CpuModel = .{
        .name = "cortex_a78",
        .llvm_name = "cortex-a78",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .dotprod,
            .enable_select_opt,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .predictable_select_expensive,
            .rcpc,
            .sha2,
            .spe,
            .ssbs,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const cortex_a78ae: CpuModel = .{
        .name = "cortex_a78ae",
        .llvm_name = "cortex-a78ae",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .dotprod,
            .enable_select_opt,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .predictable_select_expensive,
            .rcpc,
            .sha2,
            .spe,
            .ssbs,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const cortex_a78c: CpuModel = .{
        .name = "cortex_a78c",
        .llvm_name = "cortex-a78c",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .dotprod,
            .enable_select_opt,
            .flagm,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .pauth,
            .perfmon,
            .predictable_select_expensive,
            .rcpc,
            .sha2,
            .spe,
            .ssbs,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const cortex_r82: CpuModel = .{
        .name = "cortex_r82",
        .llvm_name = "cortex-r82",
        .features = featureSet(&[_]Feature{
            .ccdp,
            .fpac,
            .perfmon,
            .predres,
            .use_postra_scheduler,
            .v8r,
        }),
    };
    pub const cortex_r82ae: CpuModel = .{
        .name = "cortex_r82ae",
        .llvm_name = "cortex-r82ae",
        .features = featureSet(&[_]Feature{
            .ccdp,
            .fpac,
            .perfmon,
            .predres,
            .use_postra_scheduler,
            .v8r,
        }),
    };
    pub const cortex_x1: CpuModel = .{
        .name = "cortex_x1",
        .llvm_name = "cortex-x1",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .dotprod,
            .enable_select_opt,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .predictable_select_expensive,
            .rcpc,
            .sha2,
            .spe,
            .ssbs,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const cortex_x1c: CpuModel = .{
        .name = "cortex_x1c",
        .llvm_name = "cortex-x1c",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .cmp_bcc_fusion,
            .dotprod,
            .enable_select_opt,
            .flagm,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .lse2,
            .pauth,
            .perfmon,
            .predictable_select_expensive,
            .rcpc_immo,
            .sha2,
            .spe,
            .ssbs,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const cortex_x2: CpuModel = .{
        .name = "cortex_x2",
        .llvm_name = "cortex-x2",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .bf16,
            .cmp_bcc_fusion,
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
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
            .v9a,
        }),
    };
    pub const cortex_x3: CpuModel = .{
        .name = "cortex_x3",
        .llvm_name = "cortex-x3",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .avoid_ldapur,
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
            .spe,
            .sve_bitperm,
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
            .v9a,
        }),
    };
    pub const cortex_x4: CpuModel = .{
        .name = "cortex_x4",
        .llvm_name = "cortex-x4",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .avoid_ldapur,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .spe,
            .sve_bitperm,
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const cortex_x925: CpuModel = .{
        .name = "cortex_x925",
        .llvm_name = "cortex-x925",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .avoid_ldapur,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .spe,
            .sve_bitperm,
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const cyclone: CpuModel = .{
        .name = "cyclone",
        .llvm_name = "cyclone",
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
    pub const emag: CpuModel = .{
        .name = "emag",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{
            .crc,
            .crypto,
            .perfmon,
            .v8a,
        }),
    };
    pub const exynos_m1: CpuModel = .{
        .name = "exynos_m1",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{
            .crc,
            .crypto,
            .exynos_cheap_as_move,
            .force_32bit_jump_tables,
            .fuse_aes,
            .perfmon,
            .slow_misaligned_128store,
            .slow_paired_128,
            .use_postra_scheduler,
            .use_reciprocal_square_root,
            .v8a,
        }),
    };
    pub const exynos_m2: CpuModel = .{
        .name = "exynos_m2",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{
            .crc,
            .crypto,
            .exynos_cheap_as_move,
            .force_32bit_jump_tables,
            .fuse_aes,
            .perfmon,
            .slow_misaligned_128store,
            .slow_paired_128,
            .use_postra_scheduler,
            .v8a,
        }),
    };
    pub const exynos_m3: CpuModel = .{
        .name = "exynos_m3",
        .llvm_name = "exynos-m3",
        .features = featureSet(&[_]Feature{
            .aes,
            .alu_lsl_fast,
            .crc,
            .exynos_cheap_as_move,
            .force_32bit_jump_tables,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_csel,
            .fuse_literals,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8a,
        }),
    };
    pub const exynos_m4: CpuModel = .{
        .name = "exynos_m4",
        .llvm_name = "exynos-m4",
        .features = featureSet(&[_]Feature{
            .aes,
            .alu_lsl_fast,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .dotprod,
            .exynos_cheap_as_move,
            .force_32bit_jump_tables,
            .fullfp16,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_csel,
            .fuse_literals,
            .perfmon,
            .sha2,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8_2a,
            .zcz,
        }),
    };
    pub const exynos_m5: CpuModel = .{
        .name = "exynos_m5",
        .llvm_name = "exynos-m5",
        .features = featureSet(&[_]Feature{
            .aes,
            .alu_lsl_fast,
            .arith_bcc_fusion,
            .arith_cbz_fusion,
            .dotprod,
            .exynos_cheap_as_move,
            .force_32bit_jump_tables,
            .fullfp16,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_arith_logic,
            .fuse_csel,
            .fuse_literals,
            .perfmon,
            .sha2,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8_2a,
            .zcz,
        }),
    };
    pub const falkor: CpuModel = .{
        .name = "falkor",
        .llvm_name = "falkor",
        .features = featureSet(&[_]Feature{
            .aes,
            .alu_lsl_fast,
            .crc,
            .perfmon,
            .predictable_select_expensive,
            .rdm,
            .sha2,
            .slow_strqro_store,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8a,
            .zcz,
        }),
    };
    pub const fujitsu_monaka: CpuModel = .{
        .name = "fujitsu_monaka",
        .llvm_name = "fujitsu-monaka",
        .features = featureSet(&[_]Feature{
            .clrbhb,
            .ete,
            .faminmax,
            .fp16fml,
            .fp8dot2,
            .fp8dot4,
            .fp8fma,
            .fpac,
            .fujitsu_monaka,
            .ls64,
            .lut,
            .perfmon,
            .rand,
            .specres2,
            .sve_aes,
            .sve_bitperm,
            .sve_sha3,
            .sve_sm4,
            .v9_3a,
        }),
    };
    pub const gb10: CpuModel = .{
        .name = "gb10",
        .llvm_name = "gb10",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .avoid_ldapur,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .spe,
            .sve_aes,
            .sve_bitperm,
            .sve_sha3,
            .sve_sm4,
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const generic: CpuModel = .{
        .name = "generic",
        .llvm_name = "generic",
        .features = featureSet(&[_]Feature{
            .enable_select_opt,
            .ete,
            .fuse_adrp_add,
            .fuse_aes,
            .neon,
            .use_postra_scheduler,
        }),
    };
    pub const grace: CpuModel = .{
        .name = "grace",
        .llvm_name = "grace",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .avoid_ldapur,
            .bf16,
            .cmp_bcc_fusion,
            .disable_latency_sched_heuristic,
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
            .rand,
            .spe,
            .sve_aes,
            .sve_bitperm,
            .sve_sha3,
            .sve_sm4,
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
            .v9a,
        }),
    };
    pub const kryo: CpuModel = .{
        .name = "kryo",
        .llvm_name = "kryo",
        .features = featureSet(&[_]Feature{
            .aes,
            .alu_lsl_fast,
            .crc,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8a,
            .zcz,
        }),
    };
    pub const neoverse_512tvb: CpuModel = .{
        .name = "neoverse_512tvb",
        .llvm_name = "neoverse-512tvb",
        .features = featureSet(&[_]Feature{
            .aes,
            .alu_lsl_fast,
            .bf16,
            .ccdp,
            .enable_select_opt,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .i8mm,
            .perfmon,
            .predictable_select_expensive,
            .rand,
            .sha3,
            .sm4,
            .spe,
            .ssbs,
            .sve,
            .use_postra_scheduler,
            .v8_4a,
        }),
    };
    pub const neoverse_e1: CpuModel = .{
        .name = "neoverse_e1",
        .llvm_name = "neoverse-e1",
        .features = featureSet(&[_]Feature{
            .aes,
            .dotprod,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .rcpc,
            .sha2,
            .ssbs,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const neoverse_n1: CpuModel = .{
        .name = "neoverse_n1",
        .llvm_name = "neoverse-n1",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .dotprod,
            .enable_select_opt,
            .fullfp16,
            .fuse_adrp_add,
            .fuse_aes,
            .perfmon,
            .predictable_select_expensive,
            .rcpc,
            .sha2,
            .spe,
            .ssbs,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const neoverse_n2: CpuModel = .{
        .name = "neoverse_n2",
        .llvm_name = "neoverse-n2",
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
    pub const neoverse_n3: CpuModel = .{
        .name = "neoverse_n3",
        .llvm_name = "neoverse-n3",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .rand,
            .spe,
            .sve_bitperm,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const neoverse_v1: CpuModel = .{
        .name = "neoverse_v1",
        .llvm_name = "neoverse-v1",
        .features = featureSet(&[_]Feature{
            .addr_lsl_slow_14,
            .aes,
            .alu_lsl_fast,
            .bf16,
            .ccdp,
            .enable_select_opt,
            .fp16fml,
            .fuse_adrp_add,
            .fuse_aes,
            .i8mm,
            .no_sve_fp_ld1r,
            .perfmon,
            .predictable_select_expensive,
            .rand,
            .sha3,
            .sm4,
            .spe,
            .ssbs,
            .sve,
            .use_postra_scheduler,
            .v8_4a,
        }),
    };
    pub const neoverse_v2: CpuModel = .{
        .name = "neoverse_v2",
        .llvm_name = "neoverse-v2",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .avoid_ldapur,
            .bf16,
            .cmp_bcc_fusion,
            .disable_latency_sched_heuristic,
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
            .rand,
            .spe,
            .sve_bitperm,
            .use_fixed_over_scalable_if_equal_cost,
            .use_postra_scheduler,
            .v9a,
        }),
    };
    pub const neoverse_v3: CpuModel = .{
        .name = "neoverse_v3",
        .llvm_name = "neoverse-v3",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .avoid_ldapur,
            .brbe,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .ls64,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .rand,
            .spe,
            .sve_bitperm,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const neoverse_v3ae: CpuModel = .{
        .name = "neoverse_v3ae",
        .llvm_name = "neoverse-v3ae",
        .features = featureSet(&[_]Feature{
            .alu_lsl_fast,
            .avoid_ldapur,
            .brbe,
            .enable_select_opt,
            .ete,
            .fp16fml,
            .fpac,
            .fuse_adrp_add,
            .fuse_aes,
            .ls64,
            .mte,
            .perfmon,
            .predictable_select_expensive,
            .rand,
            .spe,
            .sve_bitperm,
            .use_postra_scheduler,
            .v9_2a,
        }),
    };
    pub const olympus: CpuModel = .{
        .name = "olympus",
        .llvm_name = "olympus",
        .features = featureSet(&[_]Feature{
            .brbe,
            .chk,
            .ete,
            .faminmax,
            .fp16fml,
            .fp8dot2,
            .fp8dot4,
            .fp8fma,
            .fpac,
            .ls64,
            .lut,
            .mte,
            .olympus,
            .perfmon,
            .rand,
            .spe,
            .sve_aes,
            .sve_bitperm,
            .sve_sha3,
            .sve_sm4,
            .v9_2a,
        }),
    };
    pub const oryon_1: CpuModel = .{
        .name = "oryon_1",
        .llvm_name = "oryon-1",
        .features = featureSet(&[_]Feature{
            .aes,
            .enable_select_opt,
            .fp16fml,
            .fuse_address,
            .fuse_adrp_add,
            .fuse_aes,
            .fuse_crypto_eor,
            .perfmon,
            .rand,
            .sha3,
            .sm4,
            .spe,
            .use_postra_scheduler,
            .v8_6a,
        }),
    };
    pub const saphira: CpuModel = .{
        .name = "saphira",
        .llvm_name = "saphira",
        .features = featureSet(&[_]Feature{
            .aes,
            .alu_lsl_fast,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .spe,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8_4a,
            .zcz,
        }),
    };
    pub const thunderx: CpuModel = .{
        .name = "thunderx",
        .llvm_name = "thunderx",
        .features = featureSet(&[_]Feature{
            .aes,
            .crc,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8a,
        }),
    };
    pub const thunderx2t99: CpuModel = .{
        .name = "thunderx2t99",
        .llvm_name = "thunderx2t99",
        .features = featureSet(&[_]Feature{
            .aes,
            .aggressive_fma,
            .arith_bcc_fusion,
            .predictable_select_expensive,
            .sha2,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8_1a,
        }),
    };
    pub const thunderx3t110: CpuModel = .{
        .name = "thunderx3t110",
        .llvm_name = "thunderx3t110",
        .features = featureSet(&[_]Feature{
            .aes,
            .aggressive_fma,
            .arith_bcc_fusion,
            .balance_fp_ops,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .store_pair_suppress,
            .strict_align,
            .use_postra_scheduler,
            .v8_3a,
        }),
    };
    pub const thunderxt81: CpuModel = .{
        .name = "thunderxt81",
        .llvm_name = "thunderxt81",
        .features = featureSet(&[_]Feature{
            .aes,
            .crc,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8a,
        }),
    };
    pub const thunderxt83: CpuModel = .{
        .name = "thunderxt83",
        .llvm_name = "thunderxt83",
        .features = featureSet(&[_]Feature{
            .aes,
            .crc,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8a,
        }),
    };
    pub const thunderxt88: CpuModel = .{
        .name = "thunderxt88",
        .llvm_name = "thunderxt88",
        .features = featureSet(&[_]Feature{
            .aes,
            .crc,
            .perfmon,
            .predictable_select_expensive,
            .sha2,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8a,
        }),
    };
    pub const tsv110: CpuModel = .{
        .name = "tsv110",
        .llvm_name = "tsv110",
        .features = featureSet(&[_]Feature{
            .aes,
            .complxnum,
            .dotprod,
            .fp16fml,
            .fuse_aes,
            .jsconv,
            .perfmon,
            .sha2,
            .spe,
            .store_pair_suppress,
            .use_postra_scheduler,
            .v8_2a,
        }),
    };
    pub const xgene1: CpuModel = .{
        .name = "xgene1",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{
            .perfmon,
            .v8a,
        }),
    };
};



---
File: /std/Target/alpha.zig
---

//! This file is auto-generated by tools/update_cpu_features.zig.

const std = @import("../std.zig");
const CpuFeature = std.Target.Cpu.Feature;
const CpuModel = std.Target.Cpu.Model;

pub const Feature = enum {
    bwx,
    cix,
    fix,
    max,
};

pub const featureSet = CpuFeature.FeatureSetFns(Feature).featureSet;
pub const featureSetHas = CpuFeature.FeatureSetFns(Feature).featureSetHas;
pub const featureSetHasAny = CpuFeature.FeatureSetFns(Feature).featureSetHasAny;
pub const featureSetHasAll = CpuFeature.FeatureSetFns(Feature).featureSetHasAll;

pub const all_features = blk: {
    const len = @typeInfo(Feature).@"enum".fields.len;
    std.debug.assert(len <= CpuFeature.Set.needed_bit_count);
    var result: [len]CpuFeature = undefined;
    result[@intFromEnum(Feature.bwx)] = .{
        .llvm_name = null,
        .description = "Enable byte/word extensions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.cix)] = .{
        .llvm_name = null,
        .description = "Enable counting extensions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fix)] = .{
        .llvm_name = null,
        .description = "Enable floating point move and square root extensions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.max)] = .{
        .llvm_name = null,
        .description = "Enable motion video extensions",
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
    pub const ev4: CpuModel = .{
        .name = "ev4",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{}),
    };
    pub const ev45: CpuModel = .{
        .name = "ev45",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{}),
    };
    pub const ev5: CpuModel = .{
        .name = "ev5",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{}),
    };
    pub const ev56: CpuModel = .{
        .name = "ev56",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{
            .bwx,
        }),
    };
    pub const ev6: CpuModel = .{
        .name = "ev6",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{
            .bwx,
            .fix,
            .max,
        }),
    };
    pub const ev67: CpuModel = .{
        .name = "ev67",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{
            .bwx,
            .cix,
            .fix,
            .max,
        }),
    };
    pub const pca56: CpuModel = .{
        .name = "pca56",
        .llvm_name = null,
        .features = featureSet(&[_]Feature{
            .bwx,
            .max,
        }),
    };
};



---
File: /std/Target/amdgcn.zig
---

//! This file is auto-generated by tools/update_cpu_features.zig.

const std = @import("../std.zig");
const CpuFeature = std.Target.Cpu.Feature;
const CpuModel = std.Target.Cpu.Model;

pub const Feature = enum {
    @"16_bit_insts",
    @"64_bit_literals",
    a16,
    add_no_carry_insts,
    addressablelocalmemorysize163840,
    addressablelocalmemorysize32768,
    addressablelocalmemorysize65536,
    agent_scope_fine_grained_remote_memory_atomics,
    allocate1_5xvgprs,
    aperture_regs,
    architected_flat_scratch,
    architected_sgprs,
    ashr_pk_insts,
    atomic_buffer_global_pk_add_f16_insts,
    atomic_buffer_global_pk_add_f16_no_rtn_insts,
    atomic_buffer_pk_add_bf16_inst,
    atomic_csub_no_rtn_insts,
    atomic_ds_pk_add_16_insts,
    atomic_fadd_no_rtn_insts,
    atomic_fadd_rtn_insts,
    atomic_flat_pk_add_16_insts,
    atomic_fmin_fmax_flat_f32,
    atomic_fmin_fmax_flat_f64,
    atomic_fmin_fmax_global_f32,
    atomic_fmin_fmax_global_f64,
    atomic_global_pk_add_bf16_inst,
    auto_waitcnt_before_barrier,
    back_off_barrier,
    bf16_cvt_insts,
    bf16_trans_insts,
    bf8_cvt_scale_insts,
    bitop3_insts,
    block_vgpr_csr,
    bvh_dual_bvh_8_insts,
    ci_insts,
    cumode,
    cvt_fp8_vop1_bug,
    cvt_pk_f16_f32_inst,
    default_component_broadcast,
    default_component_zero,
    dl_insts,
    dot10_insts,
    dot11_insts,
    dot12_insts,
    dot13_insts,
    dot1_insts,
    dot2_insts,
    dot3_insts,
    dot4_insts,
    dot5_insts,
    dot6_insts,
    dot7_insts,
    dot8_insts,
    dot9_insts,
    dpp,
    dpp8,
    dpp_64bit,
    dpp_src1_sgpr,
    ds128,
    ds_src2_insts,
    dynamic_vgpr,
    dynamic_vgpr_block_size_32,
    extended_image_insts,
    f16bf16_to_fp6bf6_cvt_scale_insts,
    f32_to_f16bf16_cvt_sr_insts,
    fast_denormal_f32,
    fast_fmaf,
    flat_address_space,
    flat_atomic_fadd_f32_inst,
    flat_buffer_global_fadd_f64_inst,
    flat_for_global,
    flat_global_insts,
    flat_inst_offsets,
    flat_scratch,
    flat_scratch_insts,
    flat_segment_offset_bug,
    fma_mix_insts,
    fmacf64_inst,
    fmaf,
    fp4_cvt_scale_insts,
    fp64,
    fp6bf6_cvt_scale_insts,
    fp8_conversion_insts,
    fp8_cvt_scale_insts,
    fp8_insts,
    fp8e5m3_insts,
    full_rate_64_ops,
    g16,
    gcn3_encoding,
    gds,
    get_wave_id_inst,
    gfx10,
    gfx10_3_insts,
    gfx10_a_encoding,
    gfx10_b_encoding,
    gfx10_insts,
    gfx11,
    gfx11_insts,
    gfx12,
    gfx1250_insts,
    gfx12_insts,
    gfx7_gfx8_gfx9_insts,
    gfx8_insts,
    gfx9,
    gfx90a_insts,
    gfx940_insts,
    gfx950_insts,
    gfx9_insts,
    gws,
    half_rate_64_ops,
    ieee_minimum_maximum_insts,
    image_gather4_d16_bug,
    image_insts,
    image_store_d16_bug,
    inst_fwd_prefetch_bug,
    int_clamp_insts,
    inv_2pi_inline_imm,
    kernarg_preload,
    lds_barrier_arrive_atomic,
    lds_branch_vmem_war_hazard,
    lds_misaligned_bug,
    ldsbankcount16,
    ldsbankcount32,
    load_store_opt,
    lshl_add_u64_inst,
    mad_intra_fwd_bug,
    mad_mac_f32_insts,
    mad_mix_insts,
    mai_insts,
    max_hard_clause_length_32,
    max_hard_clause_length_63,
    max_private_element_size_16,
    max_private_element_size_4,
    max_private_element_size_8,
    memory_atomic_fadd_f32_denormal_support,
    mfma_inline_literal_bug,
    mimg_r128,
    minimum3_maximum3_f16,
    minimum3_maximum3_f32,
    minimum3_maximum3_pkf16,
    movrel,
    msaa_load_dst_sel_bug,
    negative_scratch_offset_bug,
    negative_unaligned_scratch_offset_bug,
    no_data_dep_hazard,
    no_sdst_cmpx,
    nsa_clause_bug,
    nsa_encoding,
    nsa_to_vmem_bug,
    offset_3f_bug,
    packed_fp32_ops,
    packed_tid,
    partial_nsa_encoding,
    permlane16_swap,
    permlane32_swap,
    pk_fmac_f16_inst,
    point_sample_accel,
    precise_memory,
    priv_enabled_trap2_nop_bug,
    prng_inst,
    promote_alloca,
    prt_strict_null,
    pseudo_scalar_trans,
    r128_a16,
    real_true16,
    relaxed_buffer_oob_mode,
    required_export_priority,
    requires_cov6,
    restricted_soffset,
    s_memrealtime,
    s_memtime_inst,
    safe_smem_prefetch,
    salu_float,
    scalar_atomics,
    scalar_dwordx3_loads,
    scalar_flat_scratch_insts,
    scalar_stores,
    sdwa,
    sdwa_mav,
    sdwa_omod,
    sdwa_out_mods_vopc,
    sdwa_scalar,
    sdwa_sdst,
    sea_islands,
    setprio_inc_wg_inst,
    sgpr_init_bug,
    shader_cycles_hi_lo_registers,
    shader_cycles_register,
    si_scheduler,
    smem_to_vector_write_hazard,
    southern_islands,
    sramecc,
    sramecc_support,
    tgsplit,
    transpose_load_f4f6_insts,
    trap_handler,
    trig_reduced_range,
    true16,
    unaligned_access_mode,
    unaligned_buffer_access,
    unaligned_ds_access,
    unaligned_scratch_access,
    unpacked_d16_vmem,
    unsafe_ds_offset_folding,
    user_sgpr_init16_bug,
    valu_trans_use_hazard,
    vcmpx_exec_war_hazard,
    vcmpx_permlane_hazard,
    vgpr_index_mode,
    vmem_to_lds_load_insts,
    vmem_to_scalar_write_hazard,
    vmem_write_vgpr_in_order,
    volcanic_islands,
    vop3_literal,
    vop3p,
    vopd,
    vscnt,
    wait_xcnt,
    wavefrontsize16,
    wavefrontsize32,
    wavefrontsize64,
    xf32_insts,
    xnack,
    xnack_support,
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
    result[@intFromEnum(Feature.@"16_bit_insts")] = .{
        .llvm_name = "16-bit-insts",
        .description = "Has i16/f16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.@"64_bit_literals")] = .{
        .llvm_name = "64-bit-literals",
        .description = "Can use 64-bit literals with single DWORD instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.a16)] = .{
        .llvm_name = "a16",
        .description = "Support A16 for 16-bit coordinates/gradients/lod/clamp/mip image operands",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.add_no_carry_insts)] = .{
        .llvm_name = "add-no-carry-insts",
        .description = "Have VALU add/sub instructions without carry out",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.addressablelocalmemorysize163840)] = .{
        .llvm_name = "addressablelocalmemorysize163840",
        .description = "The size of local memory in bytes",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.addressablelocalmemorysize32768)] = .{
        .llvm_name = "addressablelocalmemorysize32768",
        .description = "The size of local memory in bytes",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.addressablelocalmemorysize65536)] = .{
        .llvm_name = "addressablelocalmemorysize65536",
        .description = "The size of local memory in bytes",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.agent_scope_fine_grained_remote_memory_atomics)] = .{
        .llvm_name = "agent-scope-fine-grained-remote-memory-atomics",
        .description = "Agent (device) scoped atomic operations, excluding those directly supported by PCIe (i.e. integer atomic add, exchange, and compare-and-swap), are functional for allocations in host or peer device memory.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.allocate1_5xvgprs)] = .{
        .llvm_name = "allocate1_5xvgprs",
        .description = "Has 50% more physical VGPRs and 50% larger allocation granule",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.aperture_regs)] = .{
        .llvm_name = "aperture-regs",
        .description = "Has Memory Aperture Base and Size Registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.architected_flat_scratch)] = .{
        .llvm_name = "architected-flat-scratch",
        .description = "Flat Scratch register is a readonly SPI initialized architected register",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.architected_sgprs)] = .{
        .llvm_name = "architected-sgprs",
        .description = "Enable the architected SGPRs",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ashr_pk_insts)] = .{
        .llvm_name = "ashr-pk-insts",
        .description = "Has Arithmetic Shift Pack instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.atomic_buffer_global_pk_add_f16_insts)] = .{
        .llvm_name = "atomic-buffer-global-pk-add-f16-insts",
        .description = "Has buffer_atomic_pk_add_f16 and global_atomic_pk_add_f16 instructions that can return original value",
        .dependencies = featureSet(&[_]Feature{
            .flat_global_insts,
        }),
    };
    result[@intFromEnum(Feature.atomic_buffer_global_pk_add_f16_no_rtn_insts)] = .{
        .llvm_name = "atomic-buffer-global-pk-add-f16-no-rtn-insts",
        .description = "Has buffer_atomic_pk_add_f16 and global_atomic_pk_add_f16 instructions that don't return original value",
        .dependencies = featureSet(&[_]Feature{
            .flat_global_insts,
        }),
    };
    result[@intFromEnum(Feature.atomic_buffer_pk_add_bf16_inst)] = .{
        .llvm_name = "atomic-buffer-pk-add-bf16-inst",
        .description = "Has buffer_atomic_pk_add_bf16 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.atomic_csub_no_rtn_insts)] = .{
        .llvm_name = "atomic-csub-no-rtn-insts",
        .description = "Has buffer_atomic_csub and global_atomic_csub instructions that don't return original value",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.atomic_ds_pk_add_16_insts)] = .{
        .llvm_name = "atomic-ds-pk-add-16-insts",
        .description = "Has ds_pk_add_bf16, ds_pk_add_f16, ds_pk_add_rtn_bf16, ds_pk_add_rtn_f16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.atomic_fadd_no_rtn_insts)] = .{
        .llvm_name = "atomic-fadd-no-rtn-insts",
        .description = "Has buffer_atomic_add_f32 and global_atomic_add_f32 instructions that don't return original value",
        .dependencies = featureSet(&[_]Feature{
            .flat_global_insts,
        }),
    };
    result[@intFromEnum(Feature.atomic_fadd_rtn_insts)] = .{
        .llvm_name = "atomic-fadd-rtn-insts",
        .description = "Has buffer_atomic_add_f32 and global_atomic_add_f32 instructions that return original value",
        .dependencies = featureSet(&[_]Feature{
            .flat_global_insts,
        }),
    };
    result[@intFromEnum(Feature.atomic_flat_pk_add_16_insts)] = .{
        .llvm_name = "atomic-flat-pk-add-16-insts",
        .description = "Has flat_atomic_pk_add_f16 and flat_atomic_pk_add_bf16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.atomic_fmin_fmax_flat_f32)] = .{
        .llvm_name = "atomic-fmin-fmax-flat-f32",
        .description = "Has flat memory instructions for atomicrmw fmin/fmax for float",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.atomic_fmin_fmax_flat_f64)] = .{
        .llvm_name = "atomic-fmin-fmax-flat-f64",
        .description = "Has flat memory instructions for atomicrmw fmin/fmax for double",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.atomic_fmin_fmax_global_f32)] = .{
        .llvm_name = "atomic-fmin-fmax-global-f32",
        .description = "Has global/buffer instructions for atomicrmw fmin/fmax for float",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.atomic_fmin_fmax_global_f64)] = .{
        .llvm_name = "atomic-fmin-fmax-global-f64",
        .description = "Has global/buffer instructions for atomicrmw fmin/fmax for float",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.atomic_global_pk_add_bf16_inst)] = .{
        .llvm_name = "atomic-global-pk-add-bf16-inst",
        .description = "Has global_atomic_pk_add_bf16 instruction",
        .dependencies = featureSet(&[_]Feature{
            .flat_global_insts,
        }),
    };
    result[@intFromEnum(Feature.auto_waitcnt_before_barrier)] = .{
        .llvm_name = "auto-waitcnt-before-barrier",
        .description = "Hardware automatically inserts waitcnt before barrier",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.back_off_barrier)] = .{
        .llvm_name = "back-off-barrier",
        .description = "Hardware supports backing off s_barrier if an exception occurs",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.bf16_cvt_insts)] = .{
        .llvm_name = "bf16-cvt-insts",
        .description = "Has bf16 conversion instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.bf16_trans_insts)] = .{
        .llvm_name = "bf16-trans-insts",
        .description = "Has bf16 transcendental instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.bf8_cvt_scale_insts)] = .{
        .llvm_name = "bf8-cvt-scale-insts",
        .description = "Has bf8 conversion scale instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.bitop3_insts)] = .{
        .llvm_name = "bitop3-insts",
        .description = "Has v_bitop3_b32/v_bitop3_b16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.block_vgpr_csr)] = .{
        .llvm_name = "block-vgpr-csr",
        .description = "Use block load/store for VGPR callee saved registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.bvh_dual_bvh_8_insts)] = .{
        .llvm_name = "bvh-dual-bvh-8-insts",
        .description = "Has image_bvh_dual_intersect_ray and image_bvh8_intersect_ray instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ci_insts)] = .{
        .llvm_name = "ci-insts",
        .description = "Additional instructions for CI+",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.cumode)] = .{
        .llvm_name = "cumode",
        .description = "Enable CU wavefront execution mode",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.cvt_fp8_vop1_bug)] = .{
        .llvm_name = "cvt-fp8-vop1-bug",
        .description = "FP8/BF8 VOP1 form of conversion to F32 is unreliable",
        .dependencies = featureSet(&[_]Feature{
            .fp8_conversion_insts,
        }),
    };
    result[@intFromEnum(Feature.cvt_pk_f16_f32_inst)] = .{
        .llvm_name = "cvt-pk-f16-f32-inst",
        .description = "Has cvt_pk_f16_f32 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.default_component_broadcast)] = .{
        .llvm_name = "default-component-broadcast",
        .description = "BUFFER/IMAGE store instructions set unspecified components to x component (GFX12)",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.default_component_zero)] = .{
        .llvm_name = "default-component-zero",
        .description = "BUFFER/IMAGE store instructions set unspecified components to zero (before GFX12)",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dl_insts)] = .{
        .llvm_name = "dl-insts",
        .description = "Has v_fmac_f32 and v_xnor_b32 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot10_insts)] = .{
        .llvm_name = "dot10-insts",
        .description = "Has v_dot2_f32_f16 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot11_insts)] = .{
        .llvm_name = "dot11-insts",
        .description = "Has v_dot4_f32_fp8_fp8, v_dot4_f32_fp8_bf8, v_dot4_f32_bf8_fp8, v_dot4_f32_bf8_bf8 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot12_insts)] = .{
        .llvm_name = "dot12-insts",
        .description = "Has v_dot2_f32_bf16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot13_insts)] = .{
        .llvm_name = "dot13-insts",
        .description = "Has v_dot2c_f32_bf16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot1_insts)] = .{
        .llvm_name = "dot1-insts",
        .description = "Has v_dot4_i32_i8 and v_dot8_i32_i4 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot2_insts)] = .{
        .llvm_name = "dot2-insts",
        .description = "Has v_dot2_i32_i16, v_dot2_u32_u16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot3_insts)] = .{
        .llvm_name = "dot3-insts",
        .description = "Has v_dot8c_i32_i4 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot4_insts)] = .{
        .llvm_name = "dot4-insts",
        .description = "Has v_dot2c_i32_i16 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot5_insts)] = .{
        .llvm_name = "dot5-insts",
        .description = "Has v_dot2c_f32_f16 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot6_insts)] = .{
        .llvm_name = "dot6-insts",
        .description = "Has v_dot4c_i32_i8 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot7_insts)] = .{
        .llvm_name = "dot7-insts",
        .description = "Has v_dot4_u32_u8, v_dot8_u32_u4 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot8_insts)] = .{
        .llvm_name = "dot8-insts",
        .description = "Has v_dot4_i32_iu8, v_dot8_i32_iu4 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dot9_insts)] = .{
        .llvm_name = "dot9-insts",
        .description = "Has v_dot2_f16_f16, v_dot2_bf16_bf16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dpp)] = .{
        .llvm_name = "dpp",
        .description = "Support DPP (Data Parallel Primitives) extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dpp8)] = .{
        .llvm_name = "dpp8",
        .description = "Support DPP8 (Data Parallel Primitives) extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dpp_64bit)] = .{
        .llvm_name = "dpp-64bit",
        .description = "Support DPP (Data Parallel Primitives) extension in DP ALU",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dpp_src1_sgpr)] = .{
        .llvm_name = "dpp-src1-sgpr",
        .description = "Support SGPR for Src1 of DPP instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ds128)] = .{
        .llvm_name = "enable-ds128",
        .description = "Use ds_{read|write}_b128",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ds_src2_insts)] = .{
        .llvm_name = "ds-src2-insts",
        .description = "Has ds_*_src2 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dynamic_vgpr)] = .{
        .llvm_name = "dynamic-vgpr",
        .description = "Enable dynamic VGPR mode",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.dynamic_vgpr_block_size_32)] = .{
        .llvm_name = "dynamic-vgpr-block-size-32",
        .description = "Use a block size of 32 for dynamic VGPR allocation (default is 16)",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.extended_image_insts)] = .{
        .llvm_name = "extended-image-insts",
        .description = "Support mips != 0, lod != 0, gather4, and get_lod",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.f16bf16_to_fp6bf6_cvt_scale_insts)] = .{
        .llvm_name = "f16bf16-to-fp6bf6-cvt-scale-insts",
        .description = "Has f16bf16 to fp6bf6 conversion scale instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.f32_to_f16bf16_cvt_sr_insts)] = .{
        .llvm_name = "f32-to-f16bf16-cvt-sr-insts",
        .description = "Has f32 to f16bf16 conversion scale instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fast_denormal_f32)] = .{
        .llvm_name = "fast-denormal-f32",
        .description = "Enabling denormals does not cause f32 instructions to run at f64 rates",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fast_fmaf)] = .{
        .llvm_name = "fast-fmaf",
        .description = "Assuming f32 fma is at least as fast as mul + add",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flat_address_space)] = .{
        .llvm_name = "flat-address-space",
        .description = "Support flat address space",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flat_atomic_fadd_f32_inst)] = .{
        .llvm_name = "flat-atomic-fadd-f32-inst",
        .description = "Has flat_atomic_add_f32 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flat_buffer_global_fadd_f64_inst)] = .{
        .llvm_name = "flat-buffer-global-fadd-f64-inst",
        .description = "Has flat, buffer, and global instructions for f64 atomic fadd",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flat_for_global)] = .{
        .llvm_name = "flat-for-global",
        .description = "Force to generate flat instruction for global",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flat_global_insts)] = .{
        .llvm_name = "flat-global-insts",
        .description = "Have global_* flat memory instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flat_inst_offsets)] = .{
        .llvm_name = "flat-inst-offsets",
        .description = "Flat instructions have immediate offset addressing mode",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flat_scratch)] = .{
        .llvm_name = "enable-flat-scratch",
        .description = "Use scratch_* flat memory instructions to access scratch",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flat_scratch_insts)] = .{
        .llvm_name = "flat-scratch-insts",
        .description = "Have scratch_* flat memory instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.flat_segment_offset_bug)] = .{
        .llvm_name = "flat-segment-offset-bug",
        .description = "GFX10 bug where inst_offset is ignored when flat instructions access global memory",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fma_mix_insts)] = .{
        .llvm_name = "fma-mix-insts",
        .description = "Has v_fma_mix_f32, v_fma_mixlo_f16, v_fma_mixhi_f16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fmacf64_inst)] = .{
        .llvm_name = "fmacf64-inst",
        .description = "Has v_fmac_f64 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fmaf)] = .{
        .llvm_name = "fmaf",
        .description = "Enable single precision FMA (not as fast as mul+add, but fused)",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fp4_cvt_scale_insts)] = .{
        .llvm_name = "fp4-cvt-scale-insts",
        .description = "Has fp4 conversion scale instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fp64)] = .{
        .llvm_name = "fp64",
        .description = "Enable double precision operations",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fp6bf6_cvt_scale_insts)] = .{
        .llvm_name = "fp6bf6-cvt-scale-insts",
        .description = "Has fp6 and bf6 conversion scale instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fp8_conversion_insts)] = .{
        .llvm_name = "fp8-conversion-insts",
        .description = "Has fp8 and bf8 conversion instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fp8_cvt_scale_insts)] = .{
        .llvm_name = "fp8-cvt-scale-insts",
        .description = "Has fp8 conversion scale instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fp8_insts)] = .{
        .llvm_name = "fp8-insts",
        .description = "Has fp8 and bf8 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.fp8e5m3_insts)] = .{
        .llvm_name = "fp8e5m3-insts",
        .description = "Has fp8 e5m3 format support",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.full_rate_64_ops)] = .{
        .llvm_name = "full-rate-64-ops",
        .description = "Most fp64 instructions are full rate",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.g16)] = .{
        .llvm_name = "g16",
        .description = "Support G16 for 16-bit gradient image operands",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gcn3_encoding)] = .{
        .llvm_name = "gcn3-encoding",
        .description = "Encoding format for VI",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gds)] = .{
        .llvm_name = "gds",
        .description = "Has Global Data Share",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.get_wave_id_inst)] = .{
        .llvm_name = "get-wave-id-inst",
        .description = "Has s_get_waveid_in_workgroup instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx10)] = .{
        .llvm_name = "gfx10",
        .description = "GFX10 GPU generation",
        .dependencies = featureSet(&[_]Feature{
            .@"16_bit_insts",
            .a16,
            .add_no_carry_insts,
            .addressablelocalmemorysize65536,
            .aperture_regs,
            .atomic_fmin_fmax_flat_f32,
            .atomic_fmin_fmax_flat_f64,
            .atomic_fmin_fmax_global_f32,
            .atomic_fmin_fmax_global_f64,
            .ci_insts,
            .default_component_zero,
            .dpp,
            .dpp8,
            .extended_image_insts,
            .fast_denormal_f32,
            .fast_fmaf,
            .flat_address_space,
            .flat_global_insts,
            .flat_inst_offsets,
            .flat_scratch_insts,
            .fma_mix_insts,
            .fp64,
            .g16,
            .gds,
            .gfx10_insts,
            .gfx8_insts,
            .gfx9_insts,
            .gws,
            .image_insts,
            .int_clamp_insts,
            .inv_2pi_inline_imm,
            .max_hard_clause_length_63,
            .mimg_r128,
            .movrel,
            .no_data_dep_hazard,
            .no_sdst_cmpx,
            .pk_fmac_f16_inst,
            .s_memrealtime,
            .s_memtime_inst,
            .sdwa,
            .sdwa_omod,
            .sdwa_scalar,
            .sdwa_sdst,
            .unaligned_buffer_access,
            .unaligned_ds_access,
            .unaligned_scratch_access,
            .vmem_to_lds_load_insts,
            .vmem_write_vgpr_in_order,
            .vop3_literal,
            .vop3p,
            .vscnt,
        }),
    };
    result[@intFromEnum(Feature.gfx10_3_insts)] = .{
        .llvm_name = "gfx10-3-insts",
        .description = "Additional instructions for GFX10.3",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx10_a_encoding)] = .{
        .llvm_name = "gfx10_a-encoding",
        .description = "Has BVH ray tracing instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx10_b_encoding)] = .{
        .llvm_name = "gfx10_b-encoding",
        .description = "Encoding format GFX10_B",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx10_insts)] = .{
        .llvm_name = "gfx10-insts",
        .description = "Additional instructions for GFX10+",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx11)] = .{
        .llvm_name = "gfx11",
        .description = "GFX11 GPU generation",
        .dependencies = featureSet(&[_]Feature{
            .@"16_bit_insts",
            .a16,
            .add_no_carry_insts,
            .addressablelocalmemorysize65536,
            .aperture_regs,
            .atomic_fmin_fmax_flat_f32,
            .atomic_fmin_fmax_global_f32,
            .ci_insts,
            .default_component_zero,
            .dpp,
            .dpp8,
            .extended_image_insts,
            .fast_denormal_f32,
            .fast_fmaf,
            .flat_address_space,
            .flat_global_insts,
            .flat_inst_offsets,
            .flat_scratch_insts,
            .fma_mix_insts,
            .fp64,
            .g16,
            .gds,
            .gfx10_3_insts,
            .gfx10_a_encoding,
            .gfx10_b_encoding,
            .gfx10_insts,
            .gfx11_insts,
            .gfx8_insts,
            .gfx9_insts,
            .gws,
            .int_clamp_insts,
            .inv_2pi_inline_imm,
            .max_hard_clause_length_32,
            .mimg_r128,
            .movrel,
            .no_data_dep_hazard,
            .no_sdst_cmpx,
            .pk_fmac_f16_inst,
            .true16,
            .unaligned_buffer_access,
            .unaligned_ds_access,
            .unaligned_scratch_access,
            .vmem_write_vgpr_in_order,
            .vop3_literal,
            .vop3p,
            .vopd,
            .vscnt,
        }),
    };
    result[@intFromEnum(Feature.gfx11_insts)] = .{
        .llvm_name = "gfx11-insts",
        .description = "Additional instructions for GFX11+",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx12)] = .{
        .llvm_name = "gfx12",
        .description = "GFX12 GPU generation",
        .dependencies = featureSet(&[_]Feature{
            .@"16_bit_insts",
            .a16,
            .add_no_carry_insts,
            .addressablelocalmemorysize65536,
            .agent_scope_fine_grained_remote_memory_atomics,
            .aperture_regs,
            .atomic_fmin_fmax_flat_f32,
            .atomic_fmin_fmax_global_f32,
            .ci_insts,
            .default_component_broadcast,
            .dpp,
            .dpp8,
            .fast_denormal_f32,
            .fast_fmaf,
            .flat_address_space,
            .flat_global_insts,
            .flat_inst_offsets,
            .flat_scratch_insts,
            .fma_mix_insts,
            .fp64,
            .g16,
            .gfx10_3_insts,
            .gfx10_a_encoding,
            .gfx10_b_encoding,
            .gfx10_insts,
            .gfx11_insts,
            .gfx12_insts,
            .gfx8_insts,
            .gfx9_insts,
            .ieee_minimum_maximum_insts,
            .int_clamp_insts,
            .inv_2pi_inline_imm,
            .max_hard_clause_length_32,
            .mimg_r128,
            .minimum3_maximum3_f16,
            .minimum3_maximum3_f32,
            .movrel,
            .no_data_dep_hazard,
            .no_sdst_cmpx,
            .pk_fmac_f16_inst,
            .true16,
            .unaligned_buffer_access,
            .unaligned_ds_access,
            .unaligned_scratch_access,
            .vop3_literal,
            .vop3p,
            .vopd,
            .vscnt,
        }),
    };
    result[@intFromEnum(Feature.gfx1250_insts)] = .{
        .llvm_name = "gfx1250-insts",
        .description = "Additional instructions for GFX1250+",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx12_insts)] = .{
        .llvm_name = "gfx12-insts",
        .description = "Additional instructions for GFX12+",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx7_gfx8_gfx9_insts)] = .{
        .llvm_name = "gfx7-gfx8-gfx9-insts",
        .description = "Instructions shared in GFX7, GFX8, GFX9",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx8_insts)] = .{
        .llvm_name = "gfx8-insts",
        .description = "Additional instructions for GFX8+",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx9)] = .{
        .llvm_name = "gfx9",
        .description = "GFX9 GPU generation",
        .dependencies = featureSet(&[_]Feature{
            .@"16_bit_insts",
            .a16,
            .add_no_carry_insts,
            .aperture_regs,
            .ci_insts,
            .default_component_zero,
            .dpp,
            .fast_denormal_f32,
            .fast_fmaf,
            .flat_address_space,
            .flat_global_insts,
            .flat_inst_offsets,
            .flat_scratch_insts,
            .fp64,
            .gcn3_encoding,
            .gfx7_gfx8_gfx9_insts,
            .gfx8_insts,
            .gfx9_insts,
            .gws,
            .int_clamp_insts,
            .inv_2pi_inline_imm,
            .negative_scratch_offset_bug,
            .r128_a16,
            .s_memrealtime,
            .s_memtime_inst,
            .scalar_atomics,
            .scalar_flat_scratch_insts,
            .scalar_stores,
            .sdwa,
            .sdwa_omod,
            .sdwa_scalar,
            .sdwa_sdst,
            .unaligned_buffer_access,
            .unaligned_ds_access,
            .unaligned_scratch_access,
            .vgpr_index_mode,
            .vmem_to_lds_load_insts,
            .vmem_write_vgpr_in_order,
            .vop3p,
            .wavefrontsize64,
            .xnack_support,
        }),
    };
    result[@intFromEnum(Feature.gfx90a_insts)] = .{
        .llvm_name = "gfx90a-insts",
        .description = "Additional instructions for GFX90A+",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx940_insts)] = .{
        .llvm_name = "gfx940-insts",
        .description = "Additional instructions for GFX940+",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gfx950_insts)] = .{
        .llvm_name = "gfx950-insts",
        .description = "Additional instructions for GFX950+",
        .dependencies = featureSet(&[_]Feature{
            .ashr_pk_insts,
            .bf8_cvt_scale_insts,
            .cvt_pk_f16_f32_inst,
            .f16bf16_to_fp6bf6_cvt_scale_insts,
            .f32_to_f16bf16_cvt_sr_insts,
            .fp4_cvt_scale_insts,
            .fp6bf6_cvt_scale_insts,
            .fp8_cvt_scale_insts,
            .minimum3_maximum3_f32,
            .minimum3_maximum3_pkf16,
            .permlane16_swap,
            .permlane32_swap,
        }),
    };
    result[@intFromEnum(Feature.gfx9_insts)] = .{
        .llvm_name = "gfx9-insts",
        .description = "Additional instructions for GFX9+",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.gws)] = .{
        .llvm_name = "gws",
        .description = "Has Global Wave Sync",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.half_rate_64_ops)] = .{
        .llvm_name = "half-rate-64-ops",
        .description = "Most fp64 instructions are half rate instead of quarter",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ieee_minimum_maximum_insts)] = .{
        .llvm_name = "ieee-minimum-maximum-insts",
        .description = "Has v_minimum/maximum_f16/f32/f64, v_minimummaximum/maximumminimum_f16/f32 and v_pk_minimum/maximum_f16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.image_gather4_d16_bug)] = .{
        .llvm_name = "image-gather4-d16-bug",
        .description = "Image Gather4 D16 hardware bug",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.image_insts)] = .{
        .llvm_name = "image-insts",
        .description = "Support image instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.image_store_d16_bug)] = .{
        .llvm_name = "image-store-d16-bug",
        .description = "Image Store D16 hardware bug",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.inst_fwd_prefetch_bug)] = .{
        .llvm_name = "inst-fwd-prefetch-bug",
        .description = "S_INST_PREFETCH instruction causes shader to hang",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.int_clamp_insts)] = .{
        .llvm_name = "int-clamp-insts",
        .description = "Support clamp for integer destination",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.inv_2pi_inline_imm)] = .{
        .llvm_name = "inv-2pi-inline-imm",
        .description = "Has 1 / (2 * pi) as inline immediate",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.kernarg_preload)] = .{
        .llvm_name = "kernarg-preload",
        .description = "Hardware supports preloading of kernel arguments in user SGPRs.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.lds_barrier_arrive_atomic)] = .{
        .llvm_name = "lds-barrier-arrive-atomic",
        .description = "Has LDS barrier-arrive atomic instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.lds_branch_vmem_war_hazard)] = .{
        .llvm_name = "lds-branch-vmem-war-hazard",
        .description = "Switching between LDS and VMEM-tex not waiting VM_VSRC=0",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.lds_misaligned_bug)] = .{
        .llvm_name = "lds-misaligned-bug",
        .description = "Some GFX10 bug with multi-dword LDS and flat access that is not naturally aligned in WGP mode",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ldsbankcount16)] = .{
        .llvm_name = "ldsbankcount16",
        .description = "The number of LDS banks per compute unit.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.ldsbankcount32)] = .{
        .llvm_name = "ldsbankcount32",
        .description = "The number of LDS banks per compute unit.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.load_store_opt)] = .{
        .llvm_name = "load-store-opt",
        .description = "Enable SI load/store optimizer pass",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.lshl_add_u64_inst)] = .{
        .llvm_name = "lshl-add-u64-inst",
        .description = "Has v_lshl_add_u64 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mad_intra_fwd_bug)] = .{
        .llvm_name = "mad-intra-fwd-bug",
        .description = "MAD_U64/I64 intra instruction forwarding bug",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mad_mac_f32_insts)] = .{
        .llvm_name = "mad-mac-f32-insts",
        .description = "Has v_mad_f32/v_mac_f32/v_madak_f32/v_madmk_f32 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mad_mix_insts)] = .{
        .llvm_name = "mad-mix-insts",
        .description = "Has v_mad_mix_f32, v_mad_mixlo_f16, v_mad_mixhi_f16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mai_insts)] = .{
        .llvm_name = "mai-insts",
        .description = "Has mAI instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.max_hard_clause_length_32)] = .{
        .llvm_name = "max-hard-clause-length-32",
        .description = "Maximum number of instructions in an explicit S_CLAUSE is 32",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.max_hard_clause_length_63)] = .{
        .llvm_name = "max-hard-clause-length-63",
        .description = "Maximum number of instructions in an explicit S_CLAUSE is 63",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.max_private_element_size_16)] = .{
        .llvm_name = "max-private-element-size-16",
        .description = "Maximum private access size may be 16",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.max_private_element_size_4)] = .{
        .llvm_name = "max-private-element-size-4",
        .description = "Maximum private access size may be 4",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.max_private_element_size_8)] = .{
        .llvm_name = "max-private-element-size-8",
        .description = "Maximum private access size may be 8",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.memory_atomic_fadd_f32_denormal_support)] = .{
        .llvm_name = "memory-atomic-fadd-f32-denormal-support",
        .description = "global/flat/buffer atomic fadd for float supports denormal handling",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mfma_inline_literal_bug)] = .{
        .llvm_name = "mfma-inline-literal-bug",
        .description = "MFMA cannot use inline literal as SrcC",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.mimg_r128)] = .{
        .llvm_name = "mimg-r128",
        .description = "Support 128-bit texture resources",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.minimum3_maximum3_f16)] = .{
        .llvm_name = "minimum3-maximum3-f16",
        .description = "Has v_minimum3_f16 and v_maximum3_f16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.minimum3_maximum3_f32)] = .{
        .llvm_name = "minimum3-maximum3-f32",
        .description = "Has v_minimum3_f32 and v_maximum3_f32 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.minimum3_maximum3_pkf16)] = .{
        .llvm_name = "minimum3-maximum3-pkf16",
        .description = "Has v_pk_minimum3_f16 and v_pk_maximum3_f16 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.movrel)] = .{
        .llvm_name = "movrel",
        .description = "Has v_movrel*_b32 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.msaa_load_dst_sel_bug)] = .{
        .llvm_name = "msaa-load-dst-sel-bug",
        .description = "MSAA loads not honoring dst_sel bug",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.negative_scratch_offset_bug)] = .{
        .llvm_name = "negative-scratch-offset-bug",
        .description = "Negative immediate offsets in scratch instructions with an SGPR offset page fault on GFX9",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.negative_unaligned_scratch_offset_bug)] = .{
        .llvm_name = "negative-unaligned-scratch-offset-bug",
        .description = "Scratch instructions with a VGPR offset and a negative immediate offset that is not a multiple of 4 read wrong memory on GFX10",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.no_data_dep_hazard)] = .{
        .llvm_name = "no-data-dep-hazard",
        .description = "Does not need SW waitstates",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.no_sdst_cmpx)] = .{
        .llvm_name = "no-sdst-cmpx",
        .description = "V_CMPX does not write VCC/SGPR in addition to EXEC",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.nsa_clause_bug)] = .{
        .llvm_name = "nsa-clause-bug",
        .description = "MIMG-NSA in a hard clause has unpredictable results on GFX10.1",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.nsa_encoding)] = .{
        .llvm_name = "nsa-encoding",
        .description = "Support NSA encoding for image instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.nsa_to_vmem_bug)] = .{
        .llvm_name = "nsa-to-vmem-bug",
        .description = "MIMG-NSA followed by VMEM fail if EXEC_LO or EXEC_HI equals zero",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.offset_3f_bug)] = .{
        .llvm_name = "offset-3f-bug",
        .description = "Branch offset of 3f hardware bug",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.packed_fp32_ops)] = .{
        .llvm_name = "packed-fp32-ops",
        .description = "Support packed fp32 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.packed_tid)] = .{
        .llvm_name = "packed-tid",
        .description = "Workitem IDs are packed into v0 at kernel launch",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.partial_nsa_encoding)] = .{
        .llvm_name = "partial-nsa-encoding",
        .description = "Support partial NSA encoding for image instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.permlane16_swap)] = .{
        .llvm_name = "permlane16-swap",
        .description = "Has v_permlane16_swap_b32 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.permlane32_swap)] = .{
        .llvm_name = "permlane32-swap",
        .description = "Has v_permlane32_swap_b32 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.pk_fmac_f16_inst)] = .{
        .llvm_name = "pk-fmac-f16-inst",
        .description = "Has v_pk_fmac_f16 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.point_sample_accel)] = .{
        .llvm_name = "point-sample-accel",
        .description = "Has point sample acceleration feature",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.precise_memory)] = .{
        .llvm_name = "precise-memory",
        .description = "Enable precise memory mode",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.priv_enabled_trap2_nop_bug)] = .{
        .llvm_name = "priv-enabled-trap2-nop-bug",
        .description = "Hardware that runs with PRIV=1 interpreting 's_trap 2' as a nop bug",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.prng_inst)] = .{
        .llvm_name = "prng-inst",
        .description = "Has v_prng_b32 instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.promote_alloca)] = .{
        .llvm_name = "promote-alloca",
        .description = "Enable promote alloca pass",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.prt_strict_null)] = .{
        .llvm_name = "enable-prt-strict-null",
        .description = "Enable zeroing of result registers for sparse texture fetches",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.pseudo_scalar_trans)] = .{
        .llvm_name = "pseudo-scalar-trans",
        .description = "Has Pseudo Scalar Transcendental instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.r128_a16)] = .{
        .llvm_name = "r128-a16",
        .description = "Support gfx9-style A16 for 16-bit coordinates/gradients/lod/clamp/mip image operands, where a16 is aliased with r128",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.real_true16)] = .{
        .llvm_name = "real-true16",
        .description = "Use true 16-bit registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.relaxed_buffer_oob_mode)] = .{
        .llvm_name = "relaxed-buffer-oob-mode",
        .description = "Disable strict out-of-bounds buffer guarantees. An OOB access may potentially cause an adjacent access to be treated as if it were also OOB",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.required_export_priority)] = .{
        .llvm_name = "required-export-priority",
        .description = "Export priority must be explicitly manipulated on GFX11.5",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.requires_cov6)] = .{
        .llvm_name = "requires-cov6",
        .description = "Target Requires Code Object V6",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.restricted_soffset)] = .{
        .llvm_name = "restricted-soffset",
        .description = "Has restricted SOffset (immediate not supported).",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.s_memrealtime)] = .{
        .llvm_name = "s-memrealtime",
        .description = "Has s_memrealtime instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.s_memtime_inst)] = .{
        .llvm_name = "s-memtime-inst",
        .description = "Has s_memtime instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.safe_smem_prefetch)] = .{
        .llvm_name = "safe-smem-prefetch",
        .description = "SMEM prefetches do not fail on illegal address",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.salu_float)] = .{
        .llvm_name = "salu-float",
        .description = "Has SALU floating point instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.scalar_atomics)] = .{
        .llvm_name = "scalar-atomics",
        .description = "Has atomic scalar memory instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.scalar_dwordx3_loads)] = .{
        .llvm_name = "scalar-dwordx3-loads",
        .description = "Has 96-bit scalar load instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.scalar_flat_scratch_insts)] = .{
        .llvm_name = "scalar-flat-scratch-insts",
        .description = "Have s_scratch_* flat memory instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.scalar_stores)] = .{
        .llvm_name = "scalar-stores",
        .description = "Has store scalar memory instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sdwa)] = .{
        .llvm_name = "sdwa",
        .description = "Support SDWA (Sub-DWORD Addressing) extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sdwa_mav)] = .{
        .llvm_name = "sdwa-mav",
        .description = "Support v_mac_f32/f16 with SDWA (Sub-DWORD Addressing) extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sdwa_omod)] = .{
        .llvm_name = "sdwa-omod",
        .description = "Support OMod with SDWA (Sub-DWORD Addressing) extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sdwa_out_mods_vopc)] = .{
        .llvm_name = "sdwa-out-mods-vopc",
        .description = "Support clamp for VOPC with SDWA (Sub-DWORD Addressing) extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sdwa_scalar)] = .{
        .llvm_name = "sdwa-scalar",
        .description = "Support scalar register with SDWA (Sub-DWORD Addressing) extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sdwa_sdst)] = .{
        .llvm_name = "sdwa-sdst",
        .description = "Support scalar dst for VOPC with SDWA (Sub-DWORD Addressing) extension",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sea_islands)] = .{
        .llvm_name = "sea-islands",
        .description = "SEA_ISLANDS GPU generation",
        .dependencies = featureSet(&[_]Feature{
            .addressablelocalmemorysize65536,
            .atomic_fmin_fmax_flat_f32,
            .atomic_fmin_fmax_flat_f64,
            .atomic_fmin_fmax_global_f32,
            .atomic_fmin_fmax_global_f64,
            .ci_insts,
            .default_component_zero,
            .ds_src2_insts,
            .extended_image_insts,
            .flat_address_space,
            .fp64,
            .gds,
            .gfx7_gfx8_gfx9_insts,
            .gws,
            .image_insts,
            .mad_mac_f32_insts,
            .mimg_r128,
            .movrel,
            .s_memtime_inst,
            .trig_reduced_range,
            .unaligned_buffer_access,
            .vmem_write_vgpr_in_order,
            .wavefrontsize64,
        }),
    };
    result[@intFromEnum(Feature.setprio_inc_wg_inst)] = .{
        .llvm_name = "setprio-inc-wg-inst",
        .description = "Has s_setprio_inc_wg instruction.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sgpr_init_bug)] = .{
        .llvm_name = "sgpr-init-bug",
        .description = "VI SGPR initialization bug requiring a fixed SGPR allocation size",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.shader_cycles_hi_lo_registers)] = .{
        .llvm_name = "shader-cycles-hi-lo-registers",
        .description = "Has SHADER_CYCLES_HI/LO hardware registers",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.shader_cycles_register)] = .{
        .llvm_name = "shader-cycles-register",
        .description = "Has SHADER_CYCLES hardware register",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.si_scheduler)] = .{
        .llvm_name = "si-scheduler",
        .description = "Enable SI Machine Scheduler",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.smem_to_vector_write_hazard)] = .{
        .llvm_name = "smem-to-vector-write-hazard",
        .description = "s_load_dword followed by v_cmp page faults",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.southern_islands)] = .{
        .llvm_name = "southern-islands",
        .description = "SOUTHERN_ISLANDS GPU generation",
        .dependencies = featureSet(&[_]Feature{
            .addressablelocalmemorysize32768,
            .atomic_fmin_fmax_global_f32,
            .atomic_fmin_fmax_global_f64,
            .default_component_zero,
            .ds_src2_insts,
            .extended_image_insts,
            .fp64,
            .gds,
            .gws,
            .image_insts,
            .ldsbankcount32,
            .mad_mac_f32_insts,
            .mimg_r128,
            .movrel,
            .s_memtime_inst,
            .trig_reduced_range,
            .vmem_write_vgpr_in_order,
            .wavefrontsize64,
        }),
    };
    result[@intFromEnum(Feature.sramecc)] = .{
        .llvm_name = "sramecc",
        .description = "Enable SRAMECC",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.sramecc_support)] = .{
        .llvm_name = "sramecc-support",
        .description = "Hardware supports SRAMECC",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.tgsplit)] = .{
        .llvm_name = "tgsplit",
        .description = "Enable threadgroup split execution",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.transpose_load_f4f6_insts)] = .{
        .llvm_name = "transpose-load-f4f6-insts",
        .description = "Has ds_load_tr4/tr6 and global_load_tr4/tr6 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.trap_handler)] = .{
        .llvm_name = "trap-handler",
        .description = "Trap handler support",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.trig_reduced_range)] = .{
        .llvm_name = "trig-reduced-range",
        .description = "Requires use of fract on arguments to trig instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.true16)] = .{
        .llvm_name = "true16",
        .description = "True 16-bit operand instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.unaligned_access_mode)] = .{
        .llvm_name = "unaligned-access-mode",
        .description = "Enable unaligned global, local and region loads and stores if the hardware supports it",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.unaligned_buffer_access)] = .{
        .llvm_name = "unaligned-buffer-access",
        .description = "Hardware supports unaligned global loads and stores",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.unaligned_ds_access)] = .{
        .llvm_name = "unaligned-ds-access",
        .description = "Hardware supports unaligned local and region loads and stores",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.unaligned_scratch_access)] = .{
        .llvm_name = "unaligned-scratch-access",
        .description = "Support unaligned scratch loads and stores",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.unpacked_d16_vmem)] = .{
        .llvm_name = "unpacked-d16-vmem",
        .description = "Has unpacked d16 vmem instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.unsafe_ds_offset_folding)] = .{
        .llvm_name = "unsafe-ds-offset-folding",
        .description = "Force using DS instruction immediate offsets on SI",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.user_sgpr_init16_bug)] = .{
        .llvm_name = "user-sgpr-init16-bug",
        .description = "Bug requiring at least 16 user+system SGPRs to be enabled",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.valu_trans_use_hazard)] = .{
        .llvm_name = "valu-trans-use-hazard",
        .description = "Hazard when TRANS instructions are closely followed by a use of the result",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.vcmpx_exec_war_hazard)] = .{
        .llvm_name = "vcmpx-exec-war-hazard",
        .description = "V_CMPX WAR hazard on EXEC (V_CMPX issue ONLY)",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.vcmpx_permlane_hazard)] = .{
        .llvm_name = "vcmpx-permlane-hazard",
        .description = "TODO: describe me",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.vgpr_index_mode)] = .{
        .llvm_name = "vgpr-index-mode",
        .description = "Has VGPR mode register indexing",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.vmem_to_lds_load_insts)] = .{
        .llvm_name = "vmem-to-lds-load-insts",
        .description = "The platform has memory to lds instructions (global_load w/lds bit set, buffer_load w/lds bit set or global_load_lds. This does not include scratch_load_lds.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.vmem_to_scalar_write_hazard)] = .{
        .llvm_name = "vmem-to-scalar-write-hazard",
        .description = "VMEM instruction followed by scalar writing to EXEC mask, M0 or SGPR leads to incorrect execution.",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.vmem_write_vgpr_in_order)] = .{
        .llvm_name = "vmem-write-vgpr-in-order",
        .description = "VMEM instructions of the same type write VGPR results in order",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.volcanic_islands)] = .{
        .llvm_name = "volcanic-islands",
        .description = "VOLCANIC_ISLANDS GPU generation",
        .dependencies = featureSet(&[_]Feature{
            .@"16_bit_insts",
            .addressablelocalmemorysize65536,
            .ci_insts,
            .default_component_zero,
            .dpp,
            .ds_src2_insts,
            .extended_image_insts,
            .fast_denormal_f32,
            .flat_address_space,
            .fp64,
            .gcn3_encoding,
            .gds,
            .gfx7_gfx8_gfx9_insts,
            .gfx8_insts,
            .gws,
            .image_insts,
            .int_clamp_insts,
            .inv_2pi_inline_imm,
            .mad_mac_f32_insts,
            .mimg_r128,
            .movrel,
            .s_memrealtime,
            .s_memtime_inst,
            .scalar_stores,
            .sdwa,
            .sdwa_mav,
            .sdwa_out_mods_vopc,
            .trig_reduced_range,
            .unaligned_buffer_access,
            .vgpr_index_mode,
            .vmem_write_vgpr_in_order,
            .wavefrontsize64,
        }),
    };
    result[@intFromEnum(Feature.vop3_literal)] = .{
        .llvm_name = "vop3-literal",
        .description = "Can use one literal in VOP3",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.vop3p)] = .{
        .llvm_name = "vop3p",
        .description = "Has VOP3P packed instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.vopd)] = .{
        .llvm_name = "vopd",
        .description = "Has VOPD dual issue wave32 instructions",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.vscnt)] = .{
        .llvm_name = "vscnt",
        .description = "Has separate store vscnt counter",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.wait_xcnt)] = .{
        .llvm_name = "wait-xcnt",
        .description = "Has s_wait_xcnt instruction",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.wavefrontsize16)] = .{
        .llvm_name = "wavefrontsize16",
        .description = "The number of threads per wavefront",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.wavefrontsize32)] = .{
        .llvm_name = "wavefrontsize32",
        .description = "The number of threads per wavefront",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.wavefrontsize64)] = .{
        .llvm_name = "wavefrontsize64",
        .description = "The number of threads per wavefront",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.xf32_insts)] = .{
        .llvm_name = "xf32-insts",
        .description = "Has instructions that support xf32 format, such as v_mfma_f32_16x16x8_xf32 and v_mfma_f32_32x32x4_xf32",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.xnack)] = .{
        .llvm_name = "xnack",
        .description = "Enable XNACK support",
        .dependencies = featureSet(&[_]Feature{}),
    };
    result[@intFromEnum(Feature.xnack_support)] = .{
        .llvm_name = "xnack-support",
        .description = "Hardware supports XNACK",
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
    pub const bonaire: CpuModel = .{
        .name = "bonaire",
        .llvm_name = "bonaire",
        .features = featureSet(&[_]Feature{
            .ldsbankcount32,
            .sea_islands,
        }),
    };
    pub const carrizo: CpuModel = .{
        .name = "carrizo",
        .llvm_name = "carrizo",
        .features = featureSet(&[_]Feature{
            .fast_fmaf,
            .half_rate_64_ops,
            .ldsbankcount32,
            .unpacked_d16_vmem,
            .volcanic_islands,
            .xnack_support,
        }),
    };
    pub const fiji: CpuModel = .{
        .name = "fiji",
        .llvm_name = "fiji",
        .features = featureSet(&[_]Feature{
            .ldsbankcount32,
            .unpacked_d16_vmem,
            .volcanic_islands,
        }),
    };
    pub const generic: CpuModel = .{
        .name = "generic",
        .llvm_name = "generic",
        .features = featureSet(&[_]Feature{}),
    };
    pub const generic_hsa: CpuModel = .{
        .name = "generic_hsa",
        .llvm_name = "generic-hsa",
        .features = featureSet(&[_]Feature{
            .flat_address_space,
        }),
    };
    pub const gfx1010: CpuModel = .{
        .name = "gfx1010",
        .llvm_name = "gfx1010",
        .features = featureSet(&[_]Feature{
            .back_off_barrier,
            .dl_insts,
            .ds_src2_insts,
            .flat_segment_offset_bug,
            .get_wave_id_inst,
            .gfx10,
            .inst_fwd_prefetch_bug,
            .lds_branch_vmem_war_hazard,
            .lds_misaligned_bug,
            .ldsbankcount32,
            .mad_mac_f32_insts,
            .negative_unaligned_scratch_offset_bug,
            .nsa_clause_bug,
            .nsa_encoding,
            .nsa_to_vmem_bug,
  
```
