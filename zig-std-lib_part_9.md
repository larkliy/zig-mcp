```
bool = false,

        cp: bool = false,
        dp: bool = false,
        sp: bool = false,
        lr: bool = false,
        sr: bool = false,
    },
    .xtensa, .xtensaeb => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        sar: bool = false,
        lbeg: bool = false,
        lend: bool = false,
        lcount: bool = false,
        atomctl: bool = false,
        scompare1: bool = false,
        threadptr: bool = false,
        litbase: bool = false,
        windowbase: bool = false,
        windowstart: bool = false,
        ps: bool = false,

        a0: bool = false,
        a1: bool = false,
        a2: bool = false,
        a3: bool = false,
        a4: bool = false,
        a5: bool = false,
        a6: bool = false,
        a7: bool = false,
        a8: bool = false,
        a9: bool = false,
        a10: bool = false,
        a11: bool = false,
        a12: bool = false,
        a13: bool = false,
        a14: bool = false,
        a15: bool = false,

        br: bool = false,
        b0: bool = false,
        b1: bool = false,
        b2: bool = false,
        b3: bool = false,
        b4: bool = false,
        b5: bool = false,
        b6: bool = false,
        b7: bool = false,
        b8: bool = false,
        b9: bool = false,
        b10: bool = false,
        b11: bool = false,
        b12: bool = false,
        b13: bool = false,
        b14: bool = false,
        b15: bool = false,

        acchi: bool = false,
        acclo: bool = false,
        m0: bool = false,
        m1: bool = false,
        m2: bool = false,
        m3: bool = false,
        fcr: bool = false,
        fsr: bool = false,

        f0: bool = false,
        f1: bool = false,
        f2: bool = false,
        f3: bool = false,
        f4: bool = false,
        f5: bool = false,
        f6: bool = false,
        f7: bool = false,
        f8: bool = false,
        f9: bool = false,
        f10: bool = false,
        f11: bool = false,
        f12: bool = false,
        f13: bool = false,
        f14: bool = false,
        f15: bool = false,
    },
    .kvx => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        cs: bool = false,

        ra: bool = false,

        ls: bool = false,
        le: bool = false,
        lc: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,
        r32: bool = false,
        r33: bool = false,
        r34: bool = false,
        r35: bool = false,
        r36: bool = false,
        r37: bool = false,
        r38: bool = false,
        r39: bool = false,
        r40: bool = false,
        r41: bool = false,
        r42: bool = false,
        r43: bool = false,
        r44: bool = false,
        r45: bool = false,
        r46: bool = false,
        r47: bool = false,
        r48: bool = false,
        r49: bool = false,
        r50: bool = false,
        r51: bool = false,
        r52: bool = false,
        r53: bool = false,
        r54: bool = false,
        r55: bool = false,
        r56: bool = false,
        r57: bool = false,
        r58: bool = false,
        r59: bool = false,
        r60: bool = false,
        r61: bool = false,
        r62: bool = false,
        r63: bool = false,

        a0: bool = false,
        a1: bool = false,
        a2: bool = false,
        a3: bool = false,
        a4: bool = false,
        a5: bool = false,
        a6: bool = false,
        a7: bool = false,
        a8: bool = false,
        a9: bool = false,
        a10: bool = false,
        a11: bool = false,
        a12: bool = false,
        a13: bool = false,
        a14: bool = false,
        a15: bool = false,
        a16: bool = false,
        a17: bool = false,
        a18: bool = false,
        a19: bool = false,
        a20: bool = false,
        a21: bool = false,
        a22: bool = false,
        a23: bool = false,
        a24: bool = false,
        a25: bool = false,
        a26: bool = false,
        a27: bool = false,
        a28: bool = false,
        a29: bool = false,
        a30: bool = false,
        a31: bool = false,
        a32: bool = false,
        a33: bool = false,
        a34: bool = false,
        a35: bool = false,
        a36: bool = false,
        a37: bool = false,
        a38: bool = false,
        a39: bool = false,
        a40: bool = false,
        a41: bool = false,
        a42: bool = false,
        a43: bool = false,
        a44: bool = false,
        a45: bool = false,
        a46: bool = false,
        a47: bool = false,
        a48: bool = false,
        a49: bool = false,
        a50: bool = false,
        a51: bool = false,
        a52: bool = false,
        a53: bool = false,
        a54: bool = false,
        a55: bool = false,
        a56: bool = false,
        a57: bool = false,
        a58: bool = false,
        a59: bool = false,
        a60: bool = false,
        a61: bool = false,
        a62: bool = false,
        a63: bool = false,

        a0_lo: bool = false,
        a0_hi: bool = false,
        a1_lo: bool = false,
        a1_hi: bool = false,
        a2_lo: bool = false,
        a2_hi: bool = false,
        a3_lo: bool = false,
        a3_hi: bool = false,
        a4_lo: bool = false,
        a4_hi: bool = false,
        a5_lo: bool = false,
        a5_hi: bool = false,
        a6_lo: bool = false,
        a6_hi: bool = false,
        a7_lo: bool = false,
        a7_hi: bool = false,
        a8_lo: bool = false,
        a8_hi: bool = false,
        a9_lo: bool = false,
        a9_hi: bool = false,
        a10_lo: bool = false,
        a10_hi: bool = false,
        a11_lo: bool = false,
        a11_hi: bool = false,
        a12_lo: bool = false,
        a12_hi: bool = false,
        a13_lo: bool = false,
        a13_hi: bool = false,
        a14_lo: bool = false,
        a14_hi: bool = false,
        a15_lo: bool = false,
        a15_hi: bool = false,
        a16_lo: bool = false,
        a16_hi: bool = false,
        a17_lo: bool = false,
        a17_hi: bool = false,
        a18_lo: bool = false,
        a18_hi: bool = false,
        a19_lo: bool = false,
        a19_hi: bool = false,
        a20_lo: bool = false,
        a20_hi: bool = false,
        a21_lo: bool = false,
        a21_hi: bool = false,
        a22_lo: bool = false,
        a22_hi: bool = false,
        a23_lo: bool = false,
        a23_hi: bool = false,
        a24_lo: bool = false,
        a24_hi: bool = false,
        a25_lo: bool = false,
        a25_hi: bool = false,
        a26_lo: bool = false,
        a26_hi: bool = false,
        a27_lo: bool = false,
        a27_hi: bool = false,
        a28_lo: bool = false,
        a28_hi: bool = false,
        a29_lo: bool = false,
        a29_hi: bool = false,
        a30_lo: bool = false,
        a30_hi: bool = false,
        a31_lo: bool = false,
        a31_hi: bool = false,
        a32_lo: bool = false,
        a32_hi: bool = false,
        a33_lo: bool = false,
        a33_hi: bool = false,
        a34_lo: bool = false,
        a34_hi: bool = false,
        a35_lo: bool = false,
        a35_hi: bool = false,
        a36_lo: bool = false,
        a36_hi: bool = false,
        a37_lo: bool = false,
        a37_hi: bool = false,
        a38_lo: bool = false,
        a38_hi: bool = false,
        a39_lo: bool = false,
        a39_hi: bool = false,
        a40_lo: bool = false,
        a40_hi: bool = false,
        a41_lo: bool = false,
        a41_hi: bool = false,
        a42_lo: bool = false,
        a42_hi: bool = false,
        a43_lo: bool = false,
        a43_hi: bool = false,
        a44_lo: bool = false,
        a44_hi: bool = false,
        a45_lo: bool = false,
        a45_hi: bool = false,
        a46_lo: bool = false,
        a46_hi: bool = false,
        a47_lo: bool = false,
        a47_hi: bool = false,
        a48_lo: bool = false,
        a48_hi: bool = false,
        a49_lo: bool = false,
        a49_hi: bool = false,
        a50_lo: bool = false,
        a50_hi: bool = false,
        a51_lo: bool = false,
        a51_hi: bool = false,
        a52_lo: bool = false,
        a52_hi: bool = false,
        a53_lo: bool = false,
        a53_hi: bool = false,
        a54_lo: bool = false,
        a54_hi: bool = false,
        a55_lo: bool = false,
        a55_hi: bool = false,
        a56_lo: bool = false,
        a56_hi: bool = false,
        a57_lo: bool = false,
        a57_hi: bool = false,
        a58_lo: bool = false,
        a58_hi: bool = false,
        a59_lo: bool = false,
        a59_hi: bool = false,
        a60_lo: bool = false,
        a60_hi: bool = false,
        a61_lo: bool = false,
        a61_hi: bool = false,
        a62_lo: bool = false,
        a62_hi: bool = false,
        a63_lo: bool = false,
        a63_hi: bool = false,

        a0_x: bool = false,
        a0_y: bool = false,
        a0_z: bool = false,
        a0_t: bool = false,
        a1_x: bool = false,
        a1_y: bool = false,
        a1_z: bool = false,
        a1_t: bool = false,
        a2_x: bool = false,
        a2_y: bool = false,
        a2_z: bool = false,
        a2_t: bool = false,
        a3_x: bool = false,
        a3_y: bool = false,
        a3_z: bool = false,
        a3_t: bool = false,
        a4_x: bool = false,
        a4_y: bool = false,
        a4_z: bool = false,
        a4_t: bool = false,
        a5_x: bool = false,
        a5_y: bool = false,
        a5_z: bool = false,
        a5_t: bool = false,
        a6_x: bool = false,
        a6_y: bool = false,
        a6_z: bool = false,
        a6_t: bool = false,
        a7_x: bool = false,
        a7_y: bool = false,
        a7_z: bool = false,
        a7_t: bool = false,
        a8_x: bool = false,
        a8_y: bool = false,
        a8_z: bool = false,
        a8_t: bool = false,
        a9_x: bool = false,
        a9_y: bool = false,
        a9_z: bool = false,
        a9_t: bool = false,
        a10_x: bool = false,
        a10_y: bool = false,
        a10_z: bool = false,
        a10_t: bool = false,
        a11_x: bool = false,
        a11_y: bool = false,
        a11_z: bool = false,
        a11_t: bool = false,
        a12_x: bool = false,
        a12_y: bool = false,
        a12_z: bool = false,
        a12_t: bool = false,
        a13_x: bool = false,
        a13_y: bool = false,
        a13_z: bool = false,
        a13_t: bool = false,
        a14_x: bool = false,
        a14_y: bool = false,
        a14_z: bool = false,
        a14_t: bool = false,
        a15_x: bool = false,
        a15_y: bool = false,
        a15_z: bool = false,
        a15_t: bool = false,
        a16_x: bool = false,
        a16_y: bool = false,
        a16_z: bool = false,
        a16_t: bool = false,
        a17_x: bool = false,
        a17_y: bool = false,
        a17_z: bool = false,
        a17_t: bool = false,
        a18_x: bool = false,
        a18_y: bool = false,
        a18_z: bool = false,
        a18_t: bool = false,
        a19_x: bool = false,
        a19_y: bool = false,
        a19_z: bool = false,
        a19_t: bool = false,
        a20_x: bool = false,
        a20_y: bool = false,
        a20_z: bool = false,
        a20_t: bool = false,
        a21_x: bool = false,
        a21_y: bool = false,
        a21_z: bool = false,
        a21_t: bool = false,
        a22_x: bool = false,
        a22_y: bool = false,
        a22_z: bool = false,
        a22_t: bool = false,
        a23_x: bool = false,
        a23_y: bool = false,
        a23_z: bool = false,
        a23_t: bool = false,
        a24_x: bool = false,
        a24_y: bool = false,
        a24_z: bool = false,
        a24_t: bool = false,
        a25_x: bool = false,
        a25_y: bool = false,
        a25_z: bool = false,
        a25_t: bool = false,
        a26_x: bool = false,
        a26_y: bool = false,
        a26_z: bool = false,
        a26_t: bool = false,
        a27_x: bool = false,
        a27_y: bool = false,
        a27_z: bool = false,
        a27_t: bool = false,
        a28_x: bool = false,
        a28_y: bool = false,
        a28_z: bool = false,
        a28_t: bool = false,
        a29_x: bool = false,
        a29_y: bool = false,
        a29_z: bool = false,
        a29_t: bool = false,
        a30_x: bool = false,
        a30_y: bool = false,
        a30_z: bool = false,
        a30_t: bool = false,
        a31_x: bool = false,
        a31_y: bool = false,
        a31_z: bool = false,
        a31_t: bool = false,
        a32_x: bool = false,
        a32_y: bool = false,
        a32_z: bool = false,
        a32_t: bool = false,
        a33_x: bool = false,
        a33_y: bool = false,
        a33_z: bool = false,
        a33_t: bool = false,
        a34_x: bool = false,
        a34_y: bool = false,
        a34_z: bool = false,
        a34_t: bool = false,
        a35_x: bool = false,
        a35_y: bool = false,
        a35_z: bool = false,
        a35_t: bool = false,
        a36_x: bool = false,
        a36_y: bool = false,
        a36_z: bool = false,
        a36_t: bool = false,
        a37_x: bool = false,
        a37_y: bool = false,
        a37_z: bool = false,
        a37_t: bool = false,
        a38_x: bool = false,
        a38_y: bool = false,
        a38_z: bool = false,
        a38_t: bool = false,
        a39_x: bool = false,
        a39_y: bool = false,
        a39_z: bool = false,
        a39_t: bool = false,
        a40_x: bool = false,
        a40_y: bool = false,
        a40_z: bool = false,
        a40_t: bool = false,
        a41_x: bool = false,
        a41_y: bool = false,
        a41_z: bool = false,
        a41_t: bool = false,
        a42_x: bool = false,
        a42_y: bool = false,
        a42_z: bool = false,
        a42_t: bool = false,
        a43_x: bool = false,
        a43_y: bool = false,
        a43_z: bool = false,
        a43_t: bool = false,
        a44_x: bool = false,
        a44_y: bool = false,
        a44_z: bool = false,
        a44_t: bool = false,
        a45_x: bool = false,
        a45_y: bool = false,
        a45_z: bool = false,
        a45_t: bool = false,
        a46_x: bool = false,
        a46_y: bool = false,
        a46_z: bool = false,
        a46_t: bool = false,
        a47_x: bool = false,
        a47_y: bool = false,
        a47_z: bool = false,
        a47_t: bool = false,
        a48_x: bool = false,
        a48_y: bool = false,
        a48_z: bool = false,
        a48_t: bool = false,
        a49_x: bool = false,
        a49_y: bool = false,
        a49_z: bool = false,
        a49_t: bool = false,
        a50_x: bool = false,
        a50_y: bool = false,
        a50_z: bool = false,
        a50_t: bool = false,
        a51_x: bool = false,
        a51_y: bool = false,
        a51_z: bool = false,
        a51_t: bool = false,
        a52_x: bool = false,
        a52_y: bool = false,
        a52_z: bool = false,
        a52_t: bool = false,
        a53_x: bool = false,
        a53_y: bool = false,
        a53_z: bool = false,
        a53_t: bool = false,
        a54_x: bool = false,
        a54_y: bool = false,
        a54_z: bool = false,
        a54_t: bool = false,
        a55_x: bool = false,
        a55_y: bool = false,
        a55_z: bool = false,
        a55_t: bool = false,
        a56_x: bool = false,
        a56_y: bool = false,
        a56_z: bool = false,
        a56_t: bool = false,
        a57_x: bool = false,
        a57_y: bool = false,
        a57_z: bool = false,
        a57_t: bool = false,
        a58_x: bool = false,
        a58_y: bool = false,
        a58_z: bool = false,
        a58_t: bool = false,
        a59_x: bool = false,
        a59_y: bool = false,
        a59_z: bool = false,
        a59_t: bool = false,
        a60_x: bool = false,
        a60_y: bool = false,
        a60_z: bool = false,
        a60_t: bool = false,
        a61_x: bool = false,
        a61_y: bool = false,
        a61_z: bool = false,
        a61_t: bool = false,
        a62_x: bool = false,
        a62_y: bool = false,
        a62_z: bool = false,
        a62_t: bool = false,
        a63_x: bool = false,
        a63_y: bool = false,
        a63_z: bool = false,
        a63_t: bool = false,
    },
    .lanai => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,
        /// Condition flags which aren't accessible outside of conditional execution.
        sw: bool = false,

        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,
    },
    .avr => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,
        flags: bool = false,
        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,
    },
    .msp430 => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,

        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
    },
    .m68k => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        ccr: bool = false,

        d0: bool = false,
        d1: bool = false,
        d2: bool = false,
        d3: bool = false,
        d4: bool = false,
        d5: bool = false,
        d6: bool = false,
        d7: bool = false,

        a0: bool = false,
        a1: bool = false,
        a2: bool = false,
        a3: bool = false,
        a4: bool = false,
        a5: bool = false,
        a6: bool = false,
        a7: bool = false,

        macsr: bool = false,
        acc: bool = false,
        acc0: bool = false,
        acc1: bool = false,
        acc2: bool = false,
        acc3: bool = false,

        mask: bool = false,
        fpcr: bool = false,
        fpsr: bool = false,

        fp0: bool = false,
        fp1: bool = false,
        fp2: bool = false,
        fp3: bool = false,
        fp4: bool = false,
        fp5: bool = false,
        fp6: bool = false,
        fp7: bool = false,
    },
    .sparc, .sparc64 => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        psr: bool = false,
        gsr: bool = false,
        y: bool = false,

        /// asr2; v9+
        ccr: bool = false,
        /// Lower bits of `ccr`.
        icc: bool = false,
        /// Upper bits of `ccr`.
        xcc: bool = false,

        g1: bool = false,
        g2: bool = false,
        g3: bool = false,
        g4: bool = false,
        g5: bool = false,
        g6: bool = false,
        g7: bool = false,

        o0: bool = false,
        o1: bool = false,
        o2: bool = false,
        o3: bool = false,
        o4: bool = false,
        o5: bool = false,
        o6: bool = false,
        o7: bool = false,

        l0: bool = false,
        l1: bool = false,
        l2: bool = false,
        l3: bool = false,
        l4: bool = false,
        l5: bool = false,
        l6: bool = false,
        l7: bool = false,

        i0: bool = false,
        i1: bool = false,
        i2: bool = false,
        i3: bool = false,
        i4: bool = false,
        i5: bool = false,
        i6: bool = false,
        i7: bool = false,

        fsr: bool = false,
        fprs: bool = false,

        q0: bool = false,
        q1: bool = false,
        q2: bool = false,
        q3: bool = false,
        q4: bool = false,
        q5: bool = false,
        q6: bool = false,
        q7: bool = false,
        q8: bool = false,
        q9: bool = false,
        q10: bool = false,
        q11: bool = false,
        q12: bool = false,
        q13: bool = false,
        q14: bool = false,
        q15: bool = false,
    },
    .bpfel, .bpfeb => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,

        w0: bool = false,
        w1: bool = false,
        w2: bool = false,
        w3: bool = false,
        w4: bool = false,
        w5: bool = false,
        w6: bool = false,
        w7: bool = false,
        w8: bool = false,
        w9: bool = false,
    },
    .hexagon => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        sa0: bool = false,
        sa1: bool = false,
        lc0: bool = false,
        lc1: bool = false,
        m0: bool = false,
        m1: bool = false,
        usr: bool = false,
        ugp: bool = false,
        gp: bool = false,
        cs0: bool = false,
        cs1: bool = false,
        framelimit: bool = false,
        framekey: bool = false,

        p0: bool = false,
        p1: bool = false,
        p2: bool = false,
        p3: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,

        q0: bool = false,
        q1: bool = false,
        q2: bool = false,
        q3: bool = false,

        v0: bool = false,
        v1: bool = false,
        v2: bool = false,
        v3: bool = false,
        v4: bool = false,
        v5: bool = false,
        v6: bool = false,
        v7: bool = false,
        v8: bool = false,
        v9: bool = false,
        v10: bool = false,
        v11: bool = false,
        v12: bool = false,
        v13: bool = false,
        v14: bool = false,
        v15: bool = false,
        v16: bool = false,
        v17: bool = false,
        v18: bool = false,
        v19: bool = false,
        v20: bool = false,
        v21: bool = false,
        v22: bool = false,
        v23: bool = false,
        v24: bool = false,
        v25: bool = false,
        v26: bool = false,
        v27: bool = false,
        v28: bool = false,
        v29: bool = false,
        v30: bool = false,
        v31: bool = false,
    },
    .s390x => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        ps: bool = false,
        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,

        fpc: bool = false,

        v0: bool = false,
        v1: bool = false,
        v2: bool = false,
        v3: bool = false,
        v4: bool = false,
        v5: bool = false,
        v6: bool = false,
        v7: bool = false,
        v8: bool = false,
        v9: bool = false,
        v10: bool = false,
        v11: bool = false,
        v12: bool = false,
        v13: bool = false,
        v14: bool = false,
        v15: bool = false,
        v16: bool = false,
        v17: bool = false,
        v18: bool = false,
        v19: bool = false,
        v20: bool = false,
        v21: bool = false,
        v22: bool = false,
        v23: bool = false,
        v24: bool = false,
        v25: bool = false,
        v26: bool = false,
        v27: bool = false,
        v28: bool = false,
        v29: bool = false,
        v30: bool = false,
        v31: bool = false,

        f0: bool = false,
        f1: bool = false,
        f2: bool = false,
        f3: bool = false,
        f4: bool = false,
        f5: bool = false,
        f6: bool = false,
        f7: bool = false,
        f8: bool = false,
        f9: bool = false,
        f10: bool = false,
        f11: bool = false,
        f12: bool = false,
        f13: bool = false,
        f14: bool = false,
        f15: bool = false,
    },
    .ve => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        psw: bool = false,

        s0: bool = false,
        s1: bool = false,
        s2: bool = false,
        s3: bool = false,
        s4: bool = false,
        s5: bool = false,
        s6: bool = false,
        s7: bool = false,
        s8: bool = false,
        s9: bool = false,
        s10: bool = false,
        s11: bool = false,
        s12: bool = false,
        s13: bool = false,
        s14: bool = false,
        s15: bool = false,
        s16: bool = false,
        s17: bool = false,
        s18: bool = false,
        s19: bool = false,
        s20: bool = false,
        s21: bool = false,
        s22: bool = false,
        s23: bool = false,
        s24: bool = false,
        s25: bool = false,
        s26: bool = false,
        s27: bool = false,
        s28: bool = false,
        s29: bool = false,
        s30: bool = false,
        s31: bool = false,
        s32: bool = false,
        s33: bool = false,
        s34: bool = false,
        s35: bool = false,
        s36: bool = false,
        s37: bool = false,
        s38: bool = false,
        s39: bool = false,
        s40: bool = false,
        s41: bool = false,
        s42: bool = false,
        s43: bool = false,
        s44: bool = false,
        s45: bool = false,
        s46: bool = false,
        s47: bool = false,
        s48: bool = false,
        s49: bool = false,
        s50: bool = false,
        s51: bool = false,
        s52: bool = false,
        s53: bool = false,
        s54: bool = false,
        s55: bool = false,
        s56: bool = false,
        s57: bool = false,
        s58: bool = false,
        s59: bool = false,
        s60: bool = false,
        s61: bool = false,
        s62: bool = false,
        s63: bool = false,

        vixr: bool = false,
        vl: bool = false,

        vm0: bool = false,
        vm1: bool = false,
        vm2: bool = false,
        vm3: bool = false,
        vm4: bool = false,
        vm5: bool = false,
        vm6: bool = false,
        vm7: bool = false,
        vm8: bool = false,
        vm9: bool = false,
        vm10: bool = false,
        vm11: bool = false,
        vm12: bool = false,
        vm13: bool = false,
        vm14: bool = false,
        vm15: bool = false,

        v0: bool = false,
        v1: bool = false,
        v2: bool = false,
        v3: bool = false,
        v4: bool = false,
        v5: bool = false,
        v6: bool = false,
        v7: bool = false,
        v8: bool = false,
        v9: bool = false,
        v10: bool = false,
        v11: bool = false,
        v12: bool = false,
        v13: bool = false,
        v14: bool = false,
        v15: bool = false,
        v16: bool = false,
        v17: bool = false,
        v18: bool = false,
        v19: bool = false,
        v20: bool = false,
        v21: bool = false,
        v22: bool = false,
        v23: bool = false,
        v24: bool = false,
        v25: bool = false,
        v26: bool = false,
        v27: bool = false,
        v28: bool = false,
        v29: bool = false,
        v30: bool = false,
        v31: bool = false,
        v32: bool = false,
        v33: bool = false,
        v34: bool = false,
        v35: bool = false,
        v36: bool = false,
        v37: bool = false,
        v38: bool = false,
        v39: bool = false,
        v40: bool = false,
        v41: bool = false,
        v42: bool = false,
        v43: bool = false,
        v44: bool = false,
        v45: bool = false,
        v46: bool = false,
        v47: bool = false,
        v48: bool = false,
        v49: bool = false,
        v50: bool = false,
        v51: bool = false,
        v52: bool = false,
        v53: bool = false,
        v54: bool = false,
        v55: bool = false,
        v56: bool = false,
        v57: bool = false,
        v58: bool = false,
        v59: bool = false,
        v60: bool = false,
        v61: bool = false,
        v62: bool = false,
        v63: bool = false,
    },
    .kalimba => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        i0: bool = false,
        i1: bool = false,
        i2: bool = false,
        i3: bool = false,
        i4: bool = false,
        i5: bool = false,
        i6: bool = false,
        i7: bool = false,

        m0: bool = false,
        m1: bool = false,
        m2: bool = false,
        m3: bool = false,
        l0: bool = false,
        l1: bool = false,
        l2: bool = false,
        l3: bool = false,
        l4: bool = false,
        l5: bool = false,
        doloopstart: bool = false,
        doloopend: bool = false,
        divresult: bool = false,
        divremainder: bool = false,
        rmac: bool = false,
        rmac0: bool = false,
        rmac1: bool = false,
        rmac2: bool = false,
        rlink: bool = false,
        rflags: bool = false,
        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
    },
    .or1k => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        maclo: bool = false,
        machi: bool = false,
        fpcsr: bool = false,
        fpmaddlo: bool = false,
        fpmaddhi: bool = false,
        vmaclo: bool = false,
        vmachi: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,
    },
    .csky => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        psr: bool = false,
        hi: bool = false,
        lo: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,

        vr0: bool = false,
        vr1: bool = false,
        vr2: bool = false,
        vr3: bool = false,
        vr4: bool = false,
        vr5: bool = false,
        vr6: bool = false,
        vr7: bool = false,
        vr8: bool = false,
        vr9: bool = false,
        vr10: bool = false,
        vr11: bool = false,
        vr12: bool = false,
        vr13: bool = false,
        vr14: bool = false,
        vr15: bool = false,
        vr16: bool = false,
        vr17: bool = false,
        vr18: bool = false,
        vr19: bool = false,
        vr20: bool = false,
        vr21: bool = false,
        vr22: bool = false,
        vr23: bool = false,
        vr24: bool = false,
        vr25: bool = false,
        vr26: bool = false,
        vr27: bool = false,
        vr28: bool = false,
        vr29: bool = false,
        vr30: bool = false,
        vr31: bool = false,
    },
    .arc, .arceb => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        status32: bool = false,
        aux_macmode: bool = false,
        mulhi: bool = false,
        lp_start: bool = false,
        lp_end: bool = false,
        jli_base: bool = false,
        ldi_base: bool = false,
        ei_base: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,
        r32: bool = false,
        r33: bool = false,
        r34: bool = false,
        r35: bool = false,
        r36: bool = false,
        r37: bool = false,
        r38: bool = false,
        r39: bool = false,
        r40: bool = false,
        r41: bool = false,
        r42: bool = false,
        r43: bool = false,
        r44: bool = false,
        r45: bool = false,
        r46: bool = false,
        r47: bool = false,
        r48: bool = false,
        r49: bool = false,
        r50: bool = false,
        r51: bool = false,
        r52: bool = false,
        r53: bool = false,
        r54: bool = false,
        r55: bool = false,
        r56: bool = false,
        r57: bool = false,
        r58: bool = false,
        r59: bool = false,
        r60: bool = false,

        fmp_ctrl: bool = false,
        dsp_ctrl: bool = false,
        acc0_lo: bool = false,
        acc0_glo: bool = false,
        acc0_hi: bool = false,
        acc0_ghi: bool = false,
        fp_ctrl: bool = false,
        fpu_status: bool = false,
        vfpu_status: bool = false,

        f0: bool = false,
        f1: bool = false,
        f2: bool = false,
        f3: bool = false,
        f4: bool = false,
        f5: bool = false,
        f6: bool = false,
        f7: bool = false,
        f8: bool = false,
        f9: bool = false,
        f10: bool = false,
        f11: bool = false,
        f12: bool = false,
        f13: bool = false,
        f14: bool = false,
        f15: bool = false,
        f16: bool = false,
        f17: bool = false,
        f18: bool = false,
        f19: bool = false,
        f20: bool = false,
        f21: bool = false,
        f22: bool = false,
        f23: bool = false,
        f24: bool = false,
        f25: bool = false,
        f26: bool = false,
        f27: bool = false,
        f28: bool = false,
        f29: bool = false,
        f30: bool = false,
        f31: bool = false,
    },
    .loongarch32, .loongarch64 => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,

        fcc0: bool = false,
        fcc1: bool = false,
        fcc2: bool = false,
        fcc3: bool = false,
        fcc4: bool = false,
        fcc5: bool = false,
        fcc6: bool = false,
        fcc7: bool = false,

        fcsr0: bool = false,
        fcsr1: bool = false,
        fcsr2: bool = false,
        fcsr3: bool = false,

        xr0: bool = false,
        xr1: bool = false,
        xr2: bool = false,
        xr3: bool = false,
        xr4: bool = false,
        xr5: bool = false,
        xr6: bool = false,
        xr7: bool = false,
        xr8: bool = false,
        xr9: bool = false,
        xr10: bool = false,
        xr11: bool = false,
        xr12: bool = false,
        xr13: bool = false,
        xr14: bool = false,
        xr15: bool = false,
        xr16: bool = false,
        xr17: bool = false,
        xr18: bool = false,
        xr19: bool = false,
        xr20: bool = false,
        xr21: bool = false,
        xr22: bool = false,
        xr23: bool = false,
        xr24: bool = false,
        xr25: bool = false,
        xr26: bool = false,
        xr27: bool = false,
        xr28: bool = false,
        xr29: bool = false,
        xr30: bool = false,
        xr31: bool = false,

        vr0: bool = false,
        vr1: bool = false,
        vr2: bool = false,
        vr3: bool = false,
        vr4: bool = false,
        vr5: bool = false,
        vr6: bool = false,
        vr7: bool = false,
        vr8: bool = false,
        vr9: bool = false,
        vr10: bool = false,
        vr11: bool = false,
        vr12: bool = false,
        vr13: bool = false,
        vr14: bool = false,
        vr15: bool = false,
        vr16: bool = false,
        vr17: bool = false,
        vr18: bool = false,
        vr19: bool = false,
        vr20: bool = false,
        vr21: bool = false,
        vr22: bool = false,
        vr23: bool = false,
        vr24: bool = false,
        vr25: bool = false,
        vr26: bool = false,
        vr27: bool = false,
        vr28: bool = false,
        vr29: bool = false,
        vr30: bool = false,
        vr31: bool = false,

        f0: bool = false,
        f1: bool = false,
        f2: bool = false,
        f3: bool = false,
        f4: bool = false,
        f5: bool = false,
        f6: bool = false,
        f7: bool = false,
        f8: bool = false,
        f9: bool = false,
        f10: bool = false,
        f11: bool = false,
        f12: bool = false,
        f13: bool = false,
        f14: bool = false,
        f15: bool = false,
        f16: bool = false,
        f17: bool = false,
        f18: bool = false,
        f19: bool = false,
        f20: bool = false,
        f21: bool = false,
        f22: bool = false,
        f23: bool = false,
        f24: bool = false,
        f25: bool = false,
        f26: bool = false,
        f27: bool = false,
        f28: bool = false,
        f29: bool = false,
        f30: bool = false,
        f31: bool = false,
    },
    .powerpc, .powerpcle, .powerpc64, .powerpc64le => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        cr0: bool = false,
        cr1: bool = false,
        cr2: bool = false,
        cr3: bool = false,
        cr4: bool = false,
        cr5: bool = false,
        cr6: bool = false,
        cr7: bool = false,

        xer: bool = false,
        ctr: bool = false,
        lr: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,

        fpscr: bool = false,
        vscr: bool = false,

        vs0: bool = false,
        vs1: bool = false,
        vs2: bool = false,
        vs3: bool = false,
        vs4: bool = false,
        vs5: bool = false,
        vs6: bool = false,
        vs7: bool = false,
        vs8: bool = false,
        vs9: bool = false,
        vs10: bool = false,
        vs11: bool = false,
        vs12: bool = false,
        vs13: bool = false,
        vs14: bool = false,
        vs15: bool = false,
        vs16: bool = false,
        vs17: bool = false,
        vs18: bool = false,
        vs19: bool = false,
        vs20: bool = false,
        vs21: bool = false,
        vs22: bool = false,
        vs23: bool = false,
        vs24: bool = false,
        vs25: bool = false,
        vs26: bool = false,
        vs27: bool = false,
        vs28: bool = false,
        vs29: bool = false,
        vs30: bool = false,
        vs31: bool = false,
        vs32: bool = false,
        vs33: bool = false,
        vs34: bool = false,
        vs35: bool = false,
        vs36: bool = false,
        vs37: bool = false,
        vs38: bool = false,
        vs39: bool = false,
        vs40: bool = false,
        vs41: bool = false,
        vs42: bool = false,
        vs43: bool = false,
        vs44: bool = false,
        vs45: bool = false,
        vs46: bool = false,
        vs47: bool = false,
        vs48: bool = false,
        vs49: bool = false,
        vs50: bool = false,
        vs51: bool = false,
        vs52: bool = false,
        vs53: bool = false,
        vs54: bool = false,
        vs55: bool = false,
        vs56: bool = false,
        vs57: bool = false,
        vs58: bool = false,
        vs59: bool = false,
        vs60: bool = false,
        vs61: bool = false,
        vs62: bool = false,
        vs63: bool = false,

        f0: bool = false,
        f1: bool = false,
        f2: bool = false,
        f3: bool = false,
        f4: bool = false,
        f5: bool = false,
        f6: bool = false,
        f7: bool = false,
        f8: bool = false,
        f9: bool = false,
        f10: bool = false,
        f11: bool = false,
        f12: bool = false,
        f13: bool = false,
        f14: bool = false,
        f15: bool = false,
        f16: bool = false,
        f17: bool = false,
        f18: bool = false,
        f19: bool = false,
        f20: bool = false,
        f21: bool = false,
        f22: bool = false,
        f23: bool = false,
        f24: bool = false,
        f25: bool = false,
        f26: bool = false,
        f27: bool = false,
        f28: bool = false,
        f29: bool = false,
        f30: bool = false,
        f31: bool = false,

        v0: bool = false,
        v1: bool = false,
        v2: bool = false,
        v3: bool = false,
        v4: bool = false,
        v5: bool = false,
        v6: bool = false,
        v7: bool = false,
        v8: bool = false,
        v9: bool = false,
        v10: bool = false,
        v11: bool = false,
        v12: bool = false,
        v13: bool = false,
        v14: bool = false,
        v15: bool = false,
        v16: bool = false,
        v17: bool = false,
        v18: bool = false,
        v19: bool = false,
        v20: bool = false,
        v21: bool = false,
        v22: bool = false,
        v23: bool = false,
        v24: bool = false,
        v25: bool = false,
        v26: bool = false,
        v27: bool = false,
        v28: bool = false,
        v29: bool = false,
        v30: bool = false,
        v31: bool = false,

        acc0: bool = false,
        acc1: bool = false,
        acc2: bool = false,
        acc3: bool = false,
        acc4: bool = false,
        acc5: bool = false,
        acc6: bool = false,
        acc7: bool = false,

        acc: bool = false,
        spefsc: bool = false,
    },
    .mips, .mipsel, .mips64, .mips64el => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        lr: bool = false,

        hi: bool = false,
        lo: bool = false,
        ac0: bool = false,
        ac1: bool = false,
        ac2: bool = false,
        ac3: bool = false,
        acx: bool = false,

        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,

        fcsr: bool = false,
        fcc0: bool = false,
        fcc1: bool = false,
        fcc2: bool = false,
        fcc3: bool = false,
        fcc4: bool = false,
        fcc5: bool = false,
        fcc6: bool = false,
        fcc7: bool = false,

        w0: bool = false,
        w1: bool = false,
        w2: bool = false,
        w3: bool = false,
        w4: bool = false,
        w5: bool = false,
        w6: bool = false,
        w7: bool = false,
        w8: bool = false,
        w9: bool = false,
        w10: bool = false,
        w11: bool = false,
        w12: bool = false,
        w13: bool = false,
        w14: bool = false,
        w15: bool = false,
        w16: bool = false,
        w17: bool = false,
        w18: bool = false,
        w19: bool = false,
        w20: bool = false,
        w21: bool = false,
        w22: bool = false,
        w23: bool = false,
        w24: bool = false,
        w25: bool = false,
        w26: bool = false,
        w27: bool = false,
        w28: bool = false,
        w29: bool = false,
        w30: bool = false,
        w31: bool = false,

        f0: bool = false,
        f1: bool = false,
        f2: bool = false,
        f3: bool = false,
        f4: bool = false,
        f5: bool = false,
        f6: bool = false,
        f7: bool = false,
        f8: bool = false,
        f9: bool = false,
        f10: bool = false,
        f11: bool = false,
        f12: bool = false,
        f13: bool = false,
        f14: bool = false,
        f15: bool = false,
        f16: bool = false,
        f17: bool = false,
        f18: bool = false,
        f19: bool = false,
        f20: bool = false,
        f21: bool = false,
        f22: bool = false,
        f23: bool = false,
        f24: bool = false,
        f25: bool = false,
        f26: bool = false,
        f27: bool = false,
        f28: bool = false,
        f29: bool = false,
        f30: bool = false,
        f31: bool = false,

        mpl0: bool = false,
        mpl1: bool = false,
        mpl2: bool = false,

        p0: bool = false,
        p1: bool = false,
        p2: bool = false,

        msa_ir: bool = false,
        msa_csr: bool = false,
        msa_access: bool = false,
        msa_save: bool = false,
        msa_modify: bool = false,
        msa_request: bool = false,
        msa_map: bool = false,
        msa_unmap: bool = false,
    },
    .alpha => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,

        f0: bool = false,
        f1: bool = false,
        f2: bool = false,
        f3: bool = false,
        f4: bool = false,
        f5: bool = false,
        f6: bool = false,
        f7: bool = false,
        f8: bool = false,
        f9: bool = false,
        f10: bool = false,
        f11: bool = false,
        f12: bool = false,
        f13: bool = false,
        f14: bool = false,
        f15: bool = false,
        f16: bool = false,
        f17: bool = false,
        f18: bool = false,
        f19: bool = false,
        f20: bool = false,
        f21: bool = false,
        f22: bool = false,
        f23: bool = false,
        f24: bool = false,
        f25: bool = false,
        f26: bool = false,
        f27: bool = false,
        f28: bool = false,
        f29: bool = false,
        f30: bool = false,
    },
    .hppa, .hppa64 => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        sar: bool = false,

        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,

        fr4: bool = false,
        fr5: bool = false,
        fr6: bool = false,
        fr7: bool = false,
        fr8: bool = false,
        fr9: bool = false,
        fr10: bool = false,
        fr11: bool = false,
        fr12: bool = false,
        fr13: bool = false,
        fr14: bool = false,
        fr15: bool = false,
        fr16: bool = false,
        fr17: bool = false,
        fr18: bool = false,
        fr19: bool = false,
        fr20: bool = false,
        fr21: bool = false,
        fr22: bool = false,
        fr23: bool = false,
        fr24: bool = false,
        fr25: bool = false,
        fr26: bool = false,
        fr27: bool = false,
        fr28: bool = false,
        fr29: bool = false,
        fr30: bool = false,
        fr31: bool = false,

        fr4r: bool = false,
        fr5r: bool = false,
        fr6r: bool = false,
        fr7r: bool = false,
        fr8r: bool = false,
        fr9r: bool = false,
        fr10r: bool = false,
        fr11r: bool = false,
        fr12r: bool = false,
        fr13r: bool = false,
        fr14r: bool = false,
        fr15r: bool = false,
        fr16r: bool = false,
        fr17r: bool = false,
        fr18r: bool = false,
        fr19r: bool = false,
        fr20r: bool = false,
        fr21r: bool = false,
        fr22r: bool = false,
        fr23r: bool = false,
        fr24r: bool = false,
        fr25r: bool = false,
        fr26r: bool = false,
        fr27r: bool = false,
        fr28r: bool = false,
        fr29r: bool = false,
        fr30r: bool = false,
        fr31r: bool = false,
    },
    .microblaze, .microblazeel => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        rmsr: bool = false,

        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        r16: bool = false,
        r17: bool = false,
        r18: bool = false,
        r19: bool = false,
        r20: bool = false,
        r21: bool = false,
        r22: bool = false,
        r23: bool = false,
        r24: bool = false,
        r25: bool = false,
        r26: bool = false,
        r27: bool = false,
        r28: bool = false,
        r29: bool = false,
        r30: bool = false,
        r31: bool = false,
    },
    .sh, .sheb => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        sr: bool = false,
        gbr: bool = false,
        pr: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,

        mach: bool = false,
        macl: bool = false,

        fr0: bool = false,
        fr1: bool = false,
        fr2: bool = false,
        fr3: bool = false,
        fr4: bool = false,
        fr5: bool = false,
        fr6: bool = false,
        fr7: bool = false,
        fr8: bool = false,
        fr9: bool = false,
        fr10: bool = false,
        fr11: bool = false,
        fr12: bool = false,
        fr13: bool = false,
        fr14: bool = false,
        fr15: bool = false,

        dr0: bool = false,
        dr2: bool = false,
        dr4: bool = false,
        dr6: bool = false,
        dr8: bool = false,
        dr10: bool = false,
        dr12: bool = false,
        dr14: bool = false,

        fv0: bool = false,
        fv4: bool = false,
        fv8: bool = false,
        fv12: bool = false,

        xf0: bool = false,
        xf1: bool = false,
        xf2: bool = false,
        xf3: bool = false,
        xf4: bool = false,
        xf5: bool = false,
        xf6: bool = false,
        xf7: bool = false,
        xf8: bool = false,
        xf9: bool = false,
        xf10: bool = false,
        xf11: bool = false,
        xf12: bool = false,
        xf13: bool = false,
        xf14: bool = false,
        xf15: bool = false,

        xd0: bool = false,
        xd2: bool = false,
        xd4: bool = false,
        xd6: bool = false,
        xd8: bool = false,
        xd10: bool = false,
        xd12: bool = false,
        xd14: bool = false,

        xmtrx: bool = false,

        fpul: bool = false,
        fpscr: bool = false,

        ms: bool = false,
        me: bool = false,

        rs: bool = false,
        re: bool = false,

        a0: bool = false,
        a0g: bool = false,
        a1: bool = false,
        a1g: bool = false,
        m0: bool = false,
        m1: bool = false,
        x0: bool = false,
        x1: bool = false,
        y0: bool = false,
        y1: bool = false,

        dsr: bool = false,
    },
    else => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,
    },
};



---
File: /std/c/darwin/dispatch.zig
---

// dispatch/base.h
pub const function_t = *const fn (?*anyopaque) callconv(.c) void;

// dispatch/object.h
pub const object_t = *_os_object_s;
pub const retain = dispatch_retain;
pub const release = dispatch_release;
pub const get_context = dispatch_get_context;
pub const set_context = dispatch_set_context;
pub const set_finalizer_f = dispatch_set_finalizer_f;
pub const activate = dispatch_activate;
pub const @"suspend" = dispatch_suspend;
pub const @"resume" = dispatch_resume;

const _os_object_s = opaque {
    pub const retain = dispatch_retain;
    pub const release = dispatch_release;
    pub const get_context = dispatch_get_context;
    pub const set_context = dispatch_set_context;
    pub const set_finalizer = dispatch_set_finalizer_f;
    pub const activate = dispatch_activate;
    pub const @"suspend" = dispatch_suspend;
    pub const @"resume" = dispatch_resume;
    pub const set_target_queue = dispatch_set_target_queue;
};
extern "c" fn dispatch_retain(object: object_t) void;
extern "c" fn dispatch_release(object: object_t) void;
extern "c" fn dispatch_get_context(object: object_t) ?*anyopaque;
extern "c" fn dispatch_set_context(object: object_t, context: ?*anyopaque) void;
extern "c" fn dispatch_set_finalizer_f(object: object_t, finalizer: ?function_t) void;
extern "c" fn dispatch_activate(object: object_t) void;
extern "c" fn dispatch_suspend(object: object_t) void;
extern "c" fn dispatch_resume(object: object_t) void;

// dispatch/once.h
pub const once_t = enum(isize) {
    init = 0,
    done = -1,
    _,

    pub inline fn once(predicate: *once_t, context: ?*anyopaque, function: function_t) void {
        if (predicate.* != .done) {
            @branchHint(.unlikely);
            once_f(predicate, context, function);
        } else asm volatile ("" ::: .{ .memory = true });
        switch (builtin.mode) {
            .Debug, .ReleaseSafe => {},
            .ReleaseFast, .ReleaseSmall => if (predicate.* != .done) unreachable,
        }
    }
};
pub const once_f = dispatch_once_f;

extern "c" fn dispatch_once_f(predicate: *once_t, context: ?*anyopaque, function: function_t) void;

// dispatch/queue.h
pub const queue_t = *queue_s;
pub const queue_global_t = queue_t;
pub const queue_serial_executor_t = queue_t;
pub const queue_serial_t = queue_t;
pub const queue_main_t = queue_serial_t;
pub const queue_concurrent_t = queue_t;
pub const async_f = dispatch_async_f;
pub const sync_f = dispatch_sync_f;
pub const async_and_wait_f = dispatch_async_and_wait_f;
pub const apply_f = dispatch_apply_f;
pub const get_current_queue = dispatch_get_current_queue;
pub inline fn get_main_queue() queue_main_t {
    return &_dispatch_main_q;
}
pub const queue_priority_t = enum(c_long) {
    HIGH = 2,
    DEFAULT = 0,
    LOW = -1,
    BACKGROUND = std.math.minInt(i16),
    _,
};
pub const get_global_queue = dispatch_get_global_queue;
pub const queue_attr_t = ?*queue_attr_s;
pub inline fn QUEUE_SERIAL() queue_attr_t {
    return null;
}
pub inline fn QUEUE_INACTIVE() queue_attr_t {
    return queue_attr_make_initially_inactive(QUEUE_SERIAL());
}
pub inline fn QUEUE_CONCURRENT() queue_attr_t {
    return &_dispatch_queue_attr_concurrent;
}
pub inline fn QUEUE_CONCURRENT_INACTIVE() queue_attr_t {
    return queue_attr_make_initially_inactive(QUEUE_CONCURRENT());
}
pub const queue_attr_make_initially_inactive = dispatch_queue_attr_make_initially_inactive;
pub const TARGET_QUEUE_DEFAULT: ?queue_t = null;
pub const queue_create_with_target = dispatch_queue_create_with_target;
pub const queue_create = dispatch_queue_create;
pub const CURRENT_QUEUE_LABEL: ?[*:0]const u8 = null;
pub const queue_get_label = dispatch_queue_get_label;
pub const main = dispatch_main;
pub const after_f = dispatch_after_f;

const queue_s = opaque {
    pub inline fn as_object(queue: queue_t) object_t {
        return @ptrCast(queue);
    }
    pub const async = async_f;
    pub const sync = sync_f;
    pub const async_and_wait = async_and_wait_f;
    pub const apply = apply_f;
    pub const get_current = get_current_queue;
    pub const get_main = get_main_queue;
    pub const get_global = get_global_queue;
    pub const TARGET_DEFAULT = TARGET_QUEUE_DEFAULT;
    pub const create_with_target = queue_create_with_target;
    pub const create = queue_create;
    pub const get_label = queue_get_label;
};
extern "c" fn dispatch_async_f(queue: queue_t, context: ?*anyopaque, work: function_t) void;
extern "c" fn dispatch_sync_f(queue: queue_t, context: ?*anyopaque, work: function_t) void;
extern "c" fn dispatch_async_and_wait_f(queue: queue_t, context: ?*anyopaque, work: function_t) void;
extern "c" fn dispatch_apply_f(iterations: usize, queue: ?queue_t, context: ?*anyopaque, work: *const fn (context: ?*anyopaque, iteration: usize) callconv(.c) void) void;
extern "c" fn dispatch_get_current_queue() queue_t;
extern "c" var _dispatch_main_q: queue_s;
extern "c" fn dispatch_get_global_queue(identifier: isize, flags: usize) queue_global_t;
const queue_attr_s = opaque {
    pub inline fn as_object(queue_attr: queue_attr_t) object_t {
        return @ptrCast(queue_attr);
    }
    pub const SERIAL = QUEUE_SERIAL;
    pub const INACTIVE = QUEUE_INACTIVE;
    pub const CONCURRENT = QUEUE_CONCURRENT;
    pub const CONCURRENT_INACTIVE = QUEUE_CONCURRENT_INACTIVE;
};
extern "c" var _dispatch_queue_attr_concurrent: queue_attr_s;
extern "c" fn dispatch_queue_attr_make_initially_inactive(attr: queue_attr_t) queue_attr_t;
extern "c" fn dispatch_queue_create_with_target(label: ?[*:0]const u8, attr: queue_attr_t, target: ?queue_t) ?queue_t;
extern "c" fn dispatch_queue_create(label: ?[*:0]const u8, attr: queue_attr_t) ?queue_t;
extern "c" fn dispatch_queue_get_label(queue: ?queue_t) [*:0]const u8;
extern "c" fn dispatch_set_target_queue(object: object_t, queue: ?queue_t) void;
extern "c" fn dispatch_main() noreturn;
extern "c" fn dispatch_after_f(when: time_t, queue: queue_t, context: ?*anyopaque, work: function_t) void;

// dispatch/semaphore.h
pub const semaphore_t = *semaphore_s;
pub const semaphore_create = dispatch_semaphore_create;
pub const semaphore_wait = dispatch_semaphore_wait;
pub const semaphore_signal = dispatch_semaphore_signal;

const semaphore_s = opaque {
    pub inline fn as_object(semaphore: semaphore_t) object_t {
        return @ptrCast(semaphore);
    }
    pub const create = semaphore_create;
    pub const wait = semaphore_wait;
    pub const signal = semaphore_signal;
};
extern "c" fn dispatch_semaphore_create(value: isize) ?semaphore_t;
extern "c" fn dispatch_semaphore_wait(dsema: semaphore_t, timeout: time_t) isize;
extern "c" fn dispatch_semaphore_signal(dsema: semaphore_t) isize;

// dispatch/source.h
pub const source_t = *source_s;
pub const source_type_t = *const source_type_s;
pub const SOURCE_TYPE_DATA_ADD = &_dispatch_source_type_data_add;
pub const SOURCE_TYPE_DATA_OR = &_dispatch_source_type_data_or;
pub const SOURCE_TYPE_DATA_REPLACE = &_dispatch_source_type_data_replace;
pub const SOURCE_TYPE_MACH_SEND = &_dispatch_source_type_mach_send;
pub const SOURCE_TYPE_MACH_RECV = &_dispatch_source_type_mach_recv;
pub const SOURCE_TYPE_MEMORYPRESSURE = &_dispatch_source_type_memorypressure;
pub const SOURCE_TYPE_PROC = &_dispatch_source_type_proc;
pub const SOURCE_TYPE_READ = &_dispatch_source_type_read;
pub const SOURCE_TYPE_SIGNAL = &_dispatch_source_type_signal;
pub const SOURCE_TYPE_TIMER = &_dispatch_source_type_timer;
pub const SOURCE_TYPE_VNODE = &_dispatch_source_type_vnode;
pub const SOURCE_TYPE_WRITE = &_dispatch_source_type_write;
pub const source_mach_send_flags_t = packed struct(usize) {
    DEAD: bool = false,
    unused1: @Int(.unsigned, @bitSizeOf(usize) - 1) = 0,
};
pub const source_mach_recv_flags_t = packed struct(usize) {
    unused0: @Int(.unsigned, @bitSizeOf(usize) - 0) = 0,
};
pub const source_memorypressure_flags_t = packed struct(usize) {
    NORMAL: bool = false,
    WARN: bool = false,
    CRITICAL: bool = false,
    unused3: @Int(.unsigned, @bitSizeOf(usize) - 3) = 0,
};
pub const source_proc_flags_t = packed struct(usize) {
    unused0: u27 = 0,
    SIGNAL: bool = false,
    unused28: u1 = 0,
    EXEC: bool = false,
    FORK: bool = false,
    EXIT: bool = false,
    unused32: @Int(.unsigned, @bitSizeOf(usize) - 32) = 0,
};
pub const source_vnode_flags_t = packed struct(usize) {
    DELETE: bool = false,
    WRITE: bool = false,
    EXTEND: bool = false,
    ATTRIB: bool = false,
    LINK: bool = false,
    RENAME: bool = false,
    REVOKE: bool = false,
    unused7: u1 = 0,
    FUNLOCK: bool = false,
    unused9: @Int(.unsigned, @bitSizeOf(usize) - 9) = 0,
};
pub const source_timer_flags_t = packed struct(usize) {
    STRICT: bool = false,
    unused1: @Int(.unsigned, @bitSizeOf(usize) - 1) = 0,
};
pub const source_flags_t = packed union(usize) {
    raw: usize,
    MACH_SEND: source_mach_send_flags_t,
    MACH_RECV: source_mach_recv_flags_t,
    MEMORYPRESSURE: source_memorypressure_flags_t,
    PROC: source_proc_flags_t,
    VNODE: source_vnode_flags_t,
    pub const none: source_flags_t = .{ .raw = 0 };
};
pub const source_create = dispatch_source_create;
pub const source_set_event_handler_f = dispatch_source_set_event_handler_f;
pub const source_set_cancel_handler_f = dispatch_source_set_cancel_handler_f;
pub const source_cancel = dispatch_source_cancel;
pub const source_testcancel = dispatch_source_testcancel;
pub const source_get_handle = dispatch_source_get_handle;
pub const source_get_mask = dispatch_source_get_mask;
pub const source_get_data = dispatch_source_get_data;
pub const source_merge_data = dispatch_source_merge_data;
pub const source_set_timer = dispatch_source_set_timer;
pub const source_set_registration_handler_f = dispatch_source_set_registration_handler_f;

const source_s = opaque {
    pub inline fn as_object(source: source_t) object_t {
        return @ptrCast(source);
    }
    pub const set_event_handler = source_set_event_handler_f;
    pub const set_cancel_handler = source_set_cancel_handler_f;
    pub const cancel = source_cancel;
    pub const testcancel = source_testcancel;
    pub const get_handle = source_get_handle;
    pub const get_mask = source_get_mask;
    pub const get_data = source_get_data;
    pub const merge_data = source_merge_data;
    pub const set_timer = source_set_timer;
    pub const set_registration_handler = source_set_registration_handler_f;
};
const source_type_s = opaque {
    pub const DATA_ADD = SOURCE_TYPE_DATA_ADD;
    pub const DATA_OR = SOURCE_TYPE_DATA_OR;
    pub const DATA_REPLACE = SOURCE_TYPE_DATA_REPLACE;
    pub const MACH_SEND = SOURCE_TYPE_MACH_SEND;
    pub const MACH_RECV = SOURCE_TYPE_MACH_RECV;
    pub const MEMORYPRESSURE = SOURCE_TYPE_MEMORYPRESSURE;
    pub const PROC = SOURCE_TYPE_PROC;
    pub const READ = SOURCE_TYPE_READ;
    pub const SIGNAL = SOURCE_TYPE_SIGNAL;
    pub const TIMER = SOURCE_TYPE_TIMER;
    pub const VNODE = SOURCE_TYPE_VNODE;
    pub const WRITE = SOURCE_TYPE_WRITE;
};
extern "c" const _dispatch_source_type_data_add: source_type_s;
extern "c" const _dispatch_source_type_data_or: source_type_s;
extern "c" const _dispatch_source_type_data_replace: source_type_s;
extern "c" const _dispatch_source_type_mach_send: source_type_s;
extern "c" const _dispatch_source_type_mach_recv: source_type_s;
extern "c" const _dispatch_source_type_memorypressure: source_type_s;
extern "c" const _dispatch_source_type_proc: source_type_s;
extern "c" const _dispatch_source_type_read: source_type_s;
extern "c" const _dispatch_source_type_signal: source_type_s;
extern "c" const _dispatch_source_type_timer: source_type_s;
extern "c" const _dispatch_source_type_vnode: source_type_s;
extern "c" const _dispatch_source_type_write: source_type_s;
extern "c" fn dispatch_source_create(type: source_type_t, handle: usize, mask: source_flags_t, queue: ?queue_t) ?source_t;
extern "c" fn dispatch_source_set_event_handler_f(source: source_t, handler: ?function_t) void;
extern "c" fn dispatch_source_set_cancel_handler_f(source: source_t, handler: ?function_t) void;
extern "c" fn dispatch_source_cancel(source: source_t) void;
extern "c" fn dispatch_source_testcancel(source: source_t) isize;
extern "c" fn dispatch_source_get_handle(source: source_t) usize;
extern "c" fn dispatch_source_get_mask(source: source_t) source_flags_t;
extern "c" fn dispatch_source_get_data(source: source_t) usize;
extern "c" fn dispatch_source_merge_data(source: source_t, value: usize) void;
extern "c" fn dispatch_source_set_timer(source: source_t, start: time_t, interval: u64, leeway: u64) void;
extern "c" fn dispatch_source_set_registration_handler_f(source: source_t, handler: ?function_t) void;

// dispatch/time.h
pub const time_t = enum(u64) {
    WALL_NOW = WALLTIME_NOW,
    NOW = TIME_NOW,
    FOREVER = TIME_FOREVER,
    _,

    pub const time = dispatch_time;
    pub const walltime = dispatch_walltime;
    pub const after = dispatch_after_f;
};
pub const WALLTIME_NOW = ~@as(u64, 1);
pub const TIME_NOW: u64 = 0;
pub const TIME_FOREVER = ~@as(u64, 0);
pub const time = dispatch_time;
pub const walltime = dispatch_walltime;

extern "c" fn dispatch_time(when: time_t, delta: i64) time_t;
extern "c" fn dispatch_walltime(when: ?*const std.c.timespec, delta: i64) time_t;

const builtin = @import("builtin");
const std = @import("std");



---
File: /std/c/darwin.zig
---

const std = @import("std");
const builtin = @import("builtin");
const native_arch = builtin.target.cpu.arch;
const assert = std.debug.assert;
const AF = std.c.AF;
const PROT = std.c.PROT;
const caddr_t = std.c.caddr_t;
const fd_t = std.c.fd_t;
const iovec_const = std.posix.iovec_const;
const mode_t = std.c.mode_t;
const off_t = std.c.off_t;
const pid_t = std.c.pid_t;
const pthread_attr_t = std.c.pthread_attr_t;
const timespec = std.c.timespec;
const sf_hdtr = std.c.sf_hdtr;

comptime {
    assert(builtin.os.tag.isDarwin()); // Prevent access of std.c symbols on wrong OS.
}

// Grand Central Dispatch is exposed by libSystem.
pub const dispatch = @import("darwin/dispatch.zig");

pub const mach_port_t = c_uint;

pub const EXC = enum(exception_type_t) {
    NULL = 0,
    /// Could not access memory
    BAD_ACCESS = 1,
    /// Instruction failed
    BAD_INSTRUCTION = 2,
    /// Arithmetic exception
    ARITHMETIC = 3,
    /// Emulation instruction
    EMULATION = 4,
    /// Software generated exception
    SOFTWARE = 5,
    /// Trace, breakpoint, etc.
    BREAKPOINT = 6,
    /// System calls.
    SYSCALL = 7,
    /// Mach system calls.
    MACH_SYSCALL = 8,
    /// RPC alert
    RPC_ALERT = 9,
    /// Abnormal process exit
    CRASH = 10,
    /// Hit resource consumption limit
    RESOURCE = 11,
    /// Violated guarded resource protections
    GUARD = 12,
    /// Abnormal process exited to corpse state
    CORPSE_NOTIFY = 13,

    _,

    pub const TYPES_COUNT = @typeInfo(EXC).@"enum".fields.len;
    pub const SOFT_SIGNAL = 0x10003;

    pub const MASK = packed struct(u32) {
        _0: u1 = 0,
        BAD_ACCESS: bool = false,
        BAD_INSTRUCTION: bool = false,
        ARITHMETIC: bool = false,
        EMULATION: bool = false,
        SOFTWARE: bool = false,
        BREAKPOINT: bool = false,
        SYSCALL: bool = false,
        MACH_SYSCALL: bool = false,
        RPC_ALERT: bool = false,
        CRASH: bool = false,
        RESOURCE: bool = false,
        GUARD: bool = false,
        CORPSE_NOTIFY: bool = false,
        _14: u18 = 0,

        pub const MACHINE: MASK = @bitCast(@as(u32, 0));

        pub const ALL: MASK = .{
            .BAD_ACCESS = true,
            .BAD_INSTRUCTION = true,
            .ARITHMETIC = true,
            .EMULATION = true,
            .SOFTWARE = true,
            .BREAKPOINT = true,
            .SYSCALL = true,
            .MACH_SYSCALL = true,
            .RPC_ALERT = true,
            .CRASH = true,
            .RESOURCE = true,
            .GUARD = true,
            .CORPSE_NOTIFY = true,
        };
    };
};

pub const EXCEPTION = enum(u32) {
    /// Send a catch_exception_raise message including the identity.
    DEFAULT = 1,
    /// Send a catch_exception_raise_state message including the
    /// thread state.
    STATE = 2,
    /// Send a catch_exception_raise_state_identity message including
    /// the thread identity and state.
    STATE_IDENTITY = 3,
    /// Send a catch_exception_raise_identity_protected message including protected task
    /// and thread identity.
    IDENTITY_PROTECTED = 4,

    _,
};

pub const KEVENT = struct {
    /// Used as the `flags` arg for `kevent64`.
    pub const FLAG = packed struct(c_uint) {
        /// immediate timeout
        IMMEDIATE: bool = false,
        /// output events only include change
        ERROR_EVENTS: bool = false,
        _: u30 = 0,

        /// no flag value
        pub const NONE: KEVENT.FLAG = .{
            .IMMEDIATE = false,
            .ERROR_EVENTS = false,
        };
    };
};

pub const MACH = struct {
    pub const EXCEPTION = packed struct(exception_mask_t) {
        _: u29 = 0,
        /// Prefer sending a catch_exception_raice_backtrace message, if applicable.
        BACKTRACE_PREFERRED: bool = false,
        /// include additional exception specific errors, not used yet.
        ERRORS: bool = false,
        /// Send 64-bit code and subcode in the exception header */
        CODES: bool = false,

        pub const MASK: exception_mask_t = @bitCast(MACH.EXCEPTION{
            .BACKTRACE_PREFERRED = true,
            .ERRORS = true,
            .CODES = true,
        });
    };

    pub const MSG = packed struct(kern_return_t) {
        _0: u10 = 0,
        /// Kernel resource shortage handling an IPC capability.
        VM_KERNEL: bool = false,
        /// Kernel resource shortage handling out-of-line memory.
        IPC_KERNEL: bool = false,
        /// No room in VM address space for out-of-line memory.
        VM_SPACE: bool = false,
        /// No room in IPC name space for another capability name.
        IPC_SPACE: bool = false,
        _14: u18 = 0,

        pub const MASK: kern_return_t = @bitCast(MACH.MSG{
            .VM_KERNEL = true,
            .IPC_KERNEL = true,
            .VM_SPACE = true,
            .IPC_SPACE = true,
        });

        pub const TIMEOUT_NONE: mach_msg_timeout_t = .NONE;
        pub const OPTION_NONE: mach_msg_option_t = .NONE;
        pub const STRICT_REPLY = @compileError("use MACH.RCV.STRICT_REPLY and/or MACH.SEND.STRICT_REPLY");

        pub const TYPE = mach_msg_type_name_t;
    };

    pub const PORT = struct {
        pub const NULL: mach_port_t = 0;
        pub const RIGHT = mach_port_right_t;
    };

    pub const RCV = packed struct(integer_t) {
        _0: u1 = 0,
        /// Other flags are only valid if this one is set.
        MSG: bool = true,
        LARGE: bool = false,
        LARGE_IDENTITY: bool = false,
        _4: u4 = 0,
        TIMEOUT: bool = false,
        /// Shared between `RCV` and `SEND`. Used to be `MACH_RCV_NOTIFY`.
        STRICT_REPLY: bool = false,
        INTERRUPT: bool = false,
        VOUCHER: bool = false,
        GUARDED_DESC: bool = false,
        _13: u1 = 0,
        SYNC_WAIT: bool = false,
        SYNC_PEEK: bool = false,
        _16: u16 = 0,
    };

    pub const SEND = packed struct(integer_t) {
        /// Other flags are only valid if this one is set.
        MSG: bool = true,
        _1: u3 = 0,
        TIMEOUT: bool = false,
        OVERRIDE: bool = false,
        INTERRUPT: bool = false,
        NOTIFY: bool = false,
        _8: u1 = 0,
        /// Shared between `RCV` and `SEND`.
        STRICT_REPLY: bool = false,
        _10: u6 = 0,
        /// User-only. If you're the kernel, this bit is `MACH_SEND_ALWAYS`.
        FILTER_NONFATAL: bool = false,
        TRAILER: bool = false,
        /// Synonymous to `MACH_SEND_NODENAP`.
        NOIMPORTANCE: bool = false,
        /// Kernel-only.
        IMPORTANCE: bool = false,
        SYNC_OVERRIDE: bool = false,
        /// Synonymous to `MACH_SEND_SYNC_USE_THRPRI`.
        PROPAGATE_QOS: bool = false,
        /// Kernel-only.
        KERNEL: bool = false,
        SYNC_BOOTSTRAP_CHECKIN: bool = false,
        _24: u8 = 0,
    };

    pub const TASK = struct {
        pub const BASIC = struct {
            pub const INFO = 20;
            pub const INFO_COUNT: mach_msg_type_number_t = @sizeOf(mach_task_basic_info) / @sizeOf(natural_t);
        };
    };
};

pub const MACH_MSG_TYPE = @compileError("use MACH.MSG.TYPE");
pub const MACH_PORT_RIGHT = @compileError("use MACH.PORT.RIGHT");
pub const MACH_TASK_BASIC_INFO = @compileError("use MACH.TASK.BASIC.INFO");
pub const MACH_TASK_BASIC_INFO_COUNT = @compileError("use MACH.TASK.BASIC.INFO_COUNT");

pub const MATTR = struct {
    /// Cachability
    pub const CACHE: vm_machine_attribute_t = 1;
    /// Migrability
    pub const MIGRATE: vm_machine_attribute_t = 2;
    /// Replicability
    pub const REPLICATE: vm_machine_attribute_t = 4;
    /// (Generic) turn attribute off
    pub const VAL_OFF: vm_machine_attribute_t = 0;
    /// (Generic) turn attribute on
    pub const VAL_ON: vm_machine_attribute_t = 1;
    /// (Generic) return current value
    pub const VAL_GET: vm_machine_attribute_t = 2;
    /// Flush from all caches
    pub const VAL_CACHE_FLUSH: vm_machine_attribute_t = 6;
    /// Flush from data caches
    pub const VAL_DCACHE_FLUSH: vm_machine_attribute_t = 7;
    /// Flush from instruction caches
    pub const VAL_ICACHE_FLUSH: vm_machine_attribute_t = 8;
    /// Sync I+D caches
    pub const VAL_CACHE_SYNC: vm_machine_attribute_t = 9;
    /// Get page info (stats)
    pub const VAL_GET_INFO: vm_machine_attribute_t = 10;
};

pub const OS = struct {
    pub const LOG_CATEGORY = struct {
        pub const POINTS_OF_INTEREST: *const u8 = "PointsOfInterest";
        pub const DYNAMIC_TRACING: *const u8 = "DynamicTracing";
        pub const DYNAMIC_STACK_TRACING: *const u8 = "DynamicStackTracing";
    };
};

pub const TASK = struct {
    pub const NULL: task_t = 0;

    pub const VM = struct {
        pub const INFO = 22;
        pub const INFO_COUNT: mach_msg_type_number_t = @sizeOf(task_vm_info_data_t) / @sizeOf(natural_t);
    };
};

pub const TASK_NULL = @compileError("use TASK.NULL");
pub const TASK_VM_INFO = @compileError("use TASK.VM.INFO");
pub const TASK_VM_INFO_COUNT = @compileError("use TASK.VM.INFO_COUNT");

pub const THREAD = struct {
    pub const NULL: thread_t = 0;

    pub const BASIC = struct {
        pub const INFO = 3;
        pub const INFO_COUNT: mach_msg_type_number_t = @sizeOf(thread_basic_info) / @sizeOf(natural_t);
    };

    pub const IDENTIFIER = struct {
        pub const INFO = 4;
        pub const INFO_COUNT: mach_msg_type_number_t = @sizeOf(thread_identifier_info) / @sizeOf(natural_t);
    };

    pub const STATE = struct {
        pub const NONE = switch (native_arch) {
            .aarch64 => 5,
            .x86_64 => 13,
            else => @compileError("unsupported arch"),
        };
    };
};

pub const THREAD_NULL = @compileError("use THREAD.NULL");
pub const THREAD_BASIC_INFO = @compileError("use THREAD.BASIC.INFO");
pub const THREAD_BASIC_INFO_COUNT = @compileError("use THREAD.BASIC.INFO_COUNT");
pub const THREAD_IDENTIFIER_INFO_COUNT = @compileError("use THREAD.IDENTIFIER.INFO_COUNT");
pub const THREAD_STATE_NONE = @compileError("use THREAD.STATE.NONE");

pub const VM = struct {
    pub const INHERIT = struct {
        pub const SHARE: vm_inherit_t = 0;
        pub const COPY: vm_inherit_t = 1;
        pub const NONE: vm_inherit_t = 2;
        pub const DONATE_COPY: vm_inherit_t = 3;
        pub const DEFAULT = VM.INHERIT.COPY;
    };

    pub const BEHAVIOR = struct {
        pub const DEFAULT: vm_behavior_t = 0;
        pub const RANDOM: vm_behavior_t = 1;
        pub const SEQUENTIAL: vm_behavior_t = 2;
        pub const RSEQNTL: vm_behavior_t = 3;
        pub const WILLNEED: vm_behavior_t = 4;
        pub const DONTNEED: vm_behavior_t = 5;
        pub const FREE: vm_behavior_t = 6;
        pub const ZERO_WIRED_PAGES: vm_behavior_t = 7;
        pub const REUSABLE: vm_behavior_t = 8;
        pub const REUSE: vm_behavior_t = 9;
        pub const CAN_REUSE: vm_behavior_t = 10;
        pub const PAGEOUT: vm_behavior_t = 11;
    };

    pub const REGION = struct {
        pub const BASIC_INFO_64 = 9;
        pub const EXTENDED_INFO = 13;
        pub const TOP_INFO = 12;
        pub const SUBMAP_INFO_COUNT_64: mach_msg_type_number_t = @sizeOf(vm_region_submap_info_64) / @sizeOf(natural_t);
        pub const SUBMAP_SHORT_INFO_COUNT_64: mach_msg_type_number_t = @sizeOf(vm_region_submap_short_info_64) / @sizeOf(natural_t);
        pub const BASIC_INFO_COUNT: mach_msg_type_number_t = @sizeOf(vm_region_basic_info_64) / @sizeOf(c_int);
        pub const EXTENDED_INFO_COUNT: mach_msg_type_number_t = @sizeOf(vm_region_extended_info) / @sizeOf(natural_t);
        pub const TOP_INFO_COUNT: mach_msg_type_number_t = @sizeOf(vm_region_top_info) / @sizeOf(natural_t);
    };

    pub fn MAKE_TAG(tag: u8) u32 {
        return @as(u32, tag) << 24;
    }
};

pub const exception_type_t = c_int;

pub extern "c" fn NSVersionOfRunTimeLibrary(library_name: [*:0]const u8) u32;
pub extern "c" fn _NSGetExecutablePath(buf: [*]u8, bufsize: *u32) c_int;
pub extern "c" fn _dyld_image_count() u32;
pub extern "c" fn _dyld_get_image_header(image_index: u32) ?*mach_header;
pub extern "c" fn _dyld_get_image_vmaddr_slide(image_index: u32) usize;
pub extern "c" fn _dyld_get_image_name(image_index: u32) [*:0]const u8;
pub extern "c" fn _dyld_get_image_header_containing_address(address: *const anyopaque) ?*mach_header;
pub extern "c" fn dyld_image_path_containing_address(address: *const anyopaque) ?[*:0]const u8;
pub extern "c" fn dladdr(addr: *const anyopaque, info: *dl_info) c_int;

pub const dl_info = extern struct {
    fname: [*:0]const u8,
    fbase: *anyopaque,
    sname: ?[*:0]const u8,
    saddr: ?*anyopaque,
};

pub const COPYFILE = packed struct(u32) {
    ACL: bool = false,
    STAT: bool = false,
    XATTR: bool = false,
    DATA: bool = false,
    _: u28 = 0,
};

pub const copyfile_state_t = *opaque {};
pub extern "c" fn fcopyfile(from: fd_t, to: fd_t, state: ?copyfile_state_t, flags: COPYFILE) c_int;
pub extern "c" fn __getdirentries64(fd: c_int, buf_ptr: [*]u8, buf_len: usize, basep: *i64) isize;

pub extern "c" fn mach_absolute_time() u64;
pub extern "c" fn mach_continuous_time() u64;
pub extern "c" fn mach_timebase_info(tinfo: ?*mach_timebase_info_data) kern_return_t;

pub extern "c" fn kevent64(
    kq: c_int,
    changelist: [*]const kevent64_s,
    nchanges: c_int,
    eventlist: [*]kevent64_s,
    nevents: c_int,
    flags: KEVENT.FLAG,
    timeout: ?*const timespec,
) c_int;

pub const mach_hdr = if (@sizeOf(usize) == 8) mach_header_64 else mach_header;

pub const mach_header_64 = std.macho.mach_header_64;
pub const mach_header = std.macho.mach_header;

pub extern "c" fn @"close$NOCANCEL"(fd: fd_t) c_int;
pub extern "c" fn mach_host_self() mach_port_t;
pub extern "c" fn clock_get_time(clock_serv: clock_serv_t, cur_time: *mach_timespec_t) kern_return_t;
pub extern "c" fn shm_open(name: [*:0]const u8, flag: c_int, ...) c_int;

pub const exception_data_type_t = integer_t;
pub const exception_data_t = ?*mach_exception_data_type_t;
pub const mach_exception_data_type_t = i64;
pub const mach_exception_data_t = ?*mach_exception_data_type_t;
pub const vm_map_t = mach_port_t;
pub const vm_map_read_t = mach_port_t;
pub const vm_region_flavor_t = c_int;
pub const vm_region_info_t = *c_int;
pub const vm_region_recurse_info_t = *c_int;
pub const mach_vm_address_t = usize;
pub const vm_offset_t = usize;
pub const mach_vm_size_t = u64;
pub const mach_msg_bits_t = c_uint;
pub const mach_msg_id_t = integer_t;
pub const mach_msg_type_number_t = natural_t;
pub const mach_msg_size_t = natural_t;
pub const task_t = mach_port_t;
pub const thread_port_t = task_t;
pub const thread_t = thread_port_t;
pub const exception_mask_t = c_uint;
pub const exception_mask_array_t = [*]exception_mask_t;
pub const exception_handler_t = mach_port_t;
pub const exception_handler_array_t = [*]exception_handler_t;
pub const exception_port_t = exception_handler_t;
pub const exception_port_array_t = exception_handler_array_t;
pub const exception_flavor_array_t = [*]thread_state_flavor_t;
pub const exception_behavior_t = c_uint;
pub const exception_behavior_array_t = [*]exception_behavior_t;
pub const thread_state_flavor_t = c_int;
pub const ipc_space_t = mach_port_t;
pub const ipc_space_port_t = ipc_space_t;

pub const mach_msg_option_t = packed union(integer_t) {
    RCV: MACH.RCV,
    SEND: MACH.SEND,

    pub const NONE: mach_msg_option_t = @bitCast(@as(integer_t, 0));

    pub fn sendAndRcv(send: MACH.SEND, rcv: MACH.RCV) mach_msg_option_t {
        return @bitCast(@as(integer_t, @bitCast(send)) | @as(integer_t, @bitCast(rcv)));
    }
};

pub const mach_msg_timeout_t = enum(natural_t) {
    NONE = 0,
    _,
};

pub const mach_msg_type_name_t = enum(c_uint) {
    /// Must hold receive right
    MOVE_RECEIVE = 16,
    /// Must hold send right(s)
    MOVE_SEND = 17,
    /// Must hold sendonce right
    MOVE_SEND_ONCE = 18,
    /// Must hold send right(s)
    COPY_SEND = 19,
    /// Must hold receive right
    MAKE_SEND = 20,
    /// Must hold receive right
    MAKE_SEND_ONCE = 21,
    /// NOT VALID
    COPY_RECEIVE = 22,
    /// Must hold receive right
    DISPOSE_RECEIVE = 24,
    /// Must hold send right(s)
    DISPOSE_SEND = 25,
    /// Must hold sendonce right
    DISPOSE_SEND_ONCE = 26,

    _,
};

pub const mach_port_right_t = enum(natural_t) {
    SEND = 0,
    RECEIVE = 1,
    SEND_ONCE = 2,
    PORT_SET = 3,
    DEAD_NAME = 4,
    /// Obsolete right
    LABELH = 5,
    /// Right not implemented
    NUMBER = 6,

    _,
};

extern "c" var mach_task_self_: mach_port_t;
pub fn mach_task_self() callconv(.c) mach_port_t {
    return mach_task_self_;
}

pub extern "c" fn mach_msg(
    msg: ?*mach_msg_header_t,
    option: mach_msg_option_t,
    send_size: mach_msg_size_t,
    rcv_size: mach_msg_size_t,
    rcv_name: mach_port_name_t,
    timeout: mach_msg_timeout_t,
    notify: mach_port_name_t,
) mach_msg_return_t;

pub const mach_msg_header_t = extern struct {
    msgh_bits: mach_msg_bits_t,
    msgh_size: mach_msg_size_t,
    msgh_remote_port: mach_port_t,
    msgh_local_port: mach_port_t,
    msgh_voucher_port: mach_port_name_t,
    msgh_id: mach_msg_id_t,
};

pub extern "c" fn task_get_exception_ports(
    task: task_t,
    exception_mask: exception_mask_t,
    masks: exception_mask_array_t,
    masks_cnt: *mach_msg_type_number_t,
    old_handlers: exception_handler_array_t,
    old_behaviors: exception_behavior_array_t,
    old_flavors: exception_flavor_array_t,
) kern_return_t;
pub extern "c" fn task_set_exception_ports(
    task: task_t,
    exception_mask: exception_mask_t,
    new_port: mach_port_t,
    behavior: exception_behavior_t,
    new_flavor: thread_state_flavor_t,
) kern_return_t;

pub const task_read_t = mach_port_t;

pub extern "c" fn task_resume(target_task: task_read_t) kern_return_t;
pub extern "c" fn task_suspend(target_task: task_read_t) kern_return_t;

pub extern "c" fn task_for_pid(target_tport: mach_port_name_t, pid: pid_t, t: *mach_port_name_t) kern_return_t;
pub extern "c" fn pid_for_task(target_tport: mach_port_name_t, pid: *pid_t) kern_return_t;
pub extern "c" fn mach_vm_read(
    target_task: vm_map_read_t,
    address: mach_vm_address_t,
    size: mach_vm_size_t,
    data: *vm_offset_t,
    data_cnt: *mach_msg_type_number_t,
) kern_return_t;
pub extern "c" fn mach_vm_write(
    target_task: vm_map_t,
    address: mach_vm_address_t,
    data: vm_offset_t,
    data_cnt: mach_msg_type_number_t,
) kern_return_t;
pub extern "c" fn mach_vm_region(
    target_task: vm_map_t,
    address: *mach_vm_address_t,
    size: *mach_vm_size_t,
    flavor: vm_region_flavor_t,
    info: vm_region_info_t,
    info_cnt: *mach_msg_type_number_t,
    object_name: *mach_port_t,
) kern_return_t;
pub extern "c" fn mach_vm_region_recurse(
    target_task: vm_map_t,
    address: *mach_vm_address_t,
    size: *mach_vm_size_t,
    nesting_depth: *natural_t,
    info: vm_region_recurse_info_t,
    info_cnt: *mach_msg_type_number_t,
) kern_return_t;

pub const vm_inherit_t = u32;
pub const memory_object_offset_t = u64;
pub const vm_behavior_t = i32;
pub const vm32_object_id_t = u32;
pub const vm_object_id_t = u64;

pub const vm_region_basic_info_64 = extern struct {
    protection: vm_prot_t,
    max_protection: vm_prot_t,
    inheritance: vm_inherit_t,
    shared: boolean_t,
    reserved: boolean_t,
    offset: memory_object_offset_t,
    behavior: vm_behavior_t,
    user_wired_count: u16,
};

pub const vm_region_extended_info = extern struct {
    protection: vm_prot_t,
    user_tag: u32,
    pages_resident: u32,
    pages_shared_now_private: u32,
    pages_swapped_out: u32,
    pages_dirtied: u32,
    ref_count: u32,
    shadow_depth: u16,
    external_pager: u8,
    share_mode: u8,
    pages_reusable: u32,
};

pub const vm_region_top_info = extern struct {
    obj_id: u32,
    ref_count: u32,
    private_pages_resident: u32,
    shared_pages_resident: u32,
    share_mode: u8,
};

pub const vm_region_submap_info_64 = extern struct {
    // present across protection
    protection: vm_prot_t,
    // max avail through vm_prot
    max_protection: vm_prot_t,
    // behavior of map/obj on fork
    inheritance: vm_inherit_t,
    // offset into object/map
    offset: memory_object_offset_t,
    // user tag on map entry
    user_tag: u32,
    // only valid for objects
    pages_resident: u32,
    // only for objects
    pages_shared_now_private: u32,
    // only for objects
    pages_swapped_out: u32,
    // only for objects
    pages_dirtied: u32,
    // obj/map mappers, etc.
    ref_count: u32,
    // only for obj
    shadow_depth: u16,
    // only for obj
    external_pager: u8,
    // see enumeration
    share_mode: u8,
    // submap vs obj
    is_submap: boolean_t,
    // access behavior hint
    behavior: vm_behavior_t,
    // obj/map name, not a handle
    object_id: vm32_object_id_t,
    user_wired_count: u16,
    pages_reusable: u32,
    object_id_full: vm_object_id_t,
};

pub const vm_region_submap_short_info_64 = extern struct {
    // present access protection
    protection: vm_prot_t,
    // max avail through vm_prot
    max_protection: vm_prot_t,
    // behavior of map/obj on fork
    inheritance: vm_inherit_t,
    // offset into object/map
    offset: memory_object_offset_t,
    // user tag on map entry
    user_tag: u32,
    // obj/map mappers, etc
    ref_count: u32,
    // only for obj
    shadow_depth: u16,
    // only for obj
    external_pager: u8,
    // see enumeration
    share_mode: u8,
    //  submap vs obj
    is_submap: boolean_t,
    // access behavior hint
    behavior: vm_behavior_t,
    // obj/map name, not a handle
    object_id: vm32_object_id_t,
    user_wired_count: u16,
};

pub const thread_act_t = mach_port_t;
pub const thread_state_t = *natural_t;
pub const mach_port_array_t = [*]mach_port_t;

pub extern "c" fn task_threads(
    target_task: mach_port_t,
    init_port_set: *mach_port_array_t,
    init_port_count: *mach_msg_type_number_t,
) kern_return_t;
pub extern "c" fn thread_get_state(
    thread: thread_act_t,
    flavor: thread_flavor_t,
    state: thread_state_t,
    count: *mach_msg_type_number_t,
) kern_return_t;
pub extern "c" fn thread_set_state(
    thread: thread_act_t,
    flavor: thread_flavor_t,
    new_state: thread_state_t,
    count: mach_msg_type_number_t,
) kern_return_t;
pub extern "c" fn thread_info(
    thread: thread_act_t,
    flavor: thread_flavor_t,
    info: thread_info_t,
    count: *mach_msg_type_number_t,
) kern_return_t;
pub extern "c" fn thread_resume(thread: thread_act_t) kern_return_t;

pub const thread_flavor_t = natural_t;
pub const thread_info_t = *integer_t;
pub const time_value_t = time_value;
pub const task_policy_flavor_t = natural_t;
pub const task_policy_t = *integer_t;
pub const policy_t = c_int;

pub const time_value = extern struct {
    seconds: integer_t,
    microseconds: integer_t,
};

pub const thread_basic_info = extern struct {
    // user run time
    user_time: time_value_t,
    // system run time
    system_time: time_value_t,
    // scaled cpu usage percentage
    cpu_usage: integer_t,
    // scheduling policy in effect
    policy: policy_t,
    // run state
    run_state: integer_t,
    // various flags
    flags: integer_t,
    // suspend count for thread
    suspend_count: integer_t,
    // number of seconds that thread has been sleeping
    sleep_time: integer_t,
};

pub const thread_identifier_info = extern struct {
    /// System-wide unique 64-bit thread id
    thread_id: u64,

    /// Handle to be used by libproc
    thread_handle: u64,

    /// libdispatch queue address
    dispatch_qaddr: u64,
};

pub const task_vm_info = extern struct {
    // virtual memory size (bytes)
    virtual_size: mach_vm_size_t,
    // number of memory regions
    region_count: integer_t,
    page_size: integer_t,
    // resident memory size (bytes)
    resident_size: mach_vm_size_t,
    // peak resident size (bytes)
    resident_size_peak: mach_vm_size_t,

    device: mach_vm_size_t,
    device_peak: mach_vm_size_t,
    internal: mach_vm_size_t,
    internal_peak: mach_vm_size_t,
    external: mach_vm_size_t,
    external_peak: mach_vm_size_t,
    reusable: mach_vm_size_t,
    reusable_peak: mach_vm_size_t,
    purgeable_volatile_pmap: mach_vm_size_t,
    purgeable_volatile_resident: mach_vm_size_t,
    purgeable_volatile_virtual: mach_vm_size_t,
    compressed: mach_vm_size_t,
    compressed_peak: mach_vm_size_t,
    compressed_lifetime: mach_vm_size_t,

    // added for rev1
    phys_footprint: mach_vm_size_t,

    // added for rev2
    min_address: mach_vm_address_t,
    max_address: mach_vm_address_t,

    // added for rev3
    ledger_phys_footprint_peak: i64,
    ledger_purgeable_nonvolatile: i64,
    ledger_purgeable_novolatile_compressed: i64,
    ledger_purgeable_volatile: i64,
    ledger_purgeable_volatile_compressed: i64,
    ledger_tag_network_nonvolatile: i64,
    ledger_tag_network_nonvolati
```
