```
_paddr: Elf32_Addr,
    p_filesz: Word,
    p_memsz: Word,
    p_flags: Word,
    p_align: Word,
};
/// Deprecated, use `std.elf.Elf64.Phdr`
pub const Elf64_Phdr = extern struct {
    p_type: Word,
    p_flags: Word,
    p_offset: Elf64_Off,
    p_vaddr: Elf64_Addr,
    p_paddr: Elf64_Addr,
    p_filesz: Elf64_Xword,
    p_memsz: Elf64_Xword,
    p_align: Elf64_Xword,
};
/// Deprecated, use `std.elf.Elf32.Shdr`
pub const Elf32_Shdr = extern struct {
    sh_name: Word,
    sh_type: Word,
    sh_flags: Word,
    sh_addr: Elf32_Addr,
    sh_offset: Elf32_Off,
    sh_size: Word,
    sh_link: Word,
    sh_info: Word,
    sh_addralign: Word,
    sh_entsize: Word,
};
/// Deprecated, use `std.elf.Elf64.Shdr`
pub const Elf64_Shdr = extern struct {
    sh_name: Word,
    sh_type: Word,
    sh_flags: Elf64_Xword,
    sh_addr: Elf64_Addr,
    sh_offset: Elf64_Off,
    sh_size: Elf64_Xword,
    sh_link: Word,
    sh_info: Word,
    sh_addralign: Elf64_Xword,
    sh_entsize: Elf64_Xword,
};
/// Deprecated, use `std.elf.Elf32.Chdr`
pub const Elf32_Chdr = extern struct {
    ch_type: COMPRESS,
    ch_size: Word,
    ch_addralign: Word,
};
/// Deprecated, use `std.elf.Elf64.Chdr`
pub const Elf64_Chdr = extern struct {
    ch_type: COMPRESS,
    ch_reserved: Word = 0,
    ch_size: Elf64_Xword,
    ch_addralign: Elf64_Xword,
};
/// Deprecated, use `std.elf.Elf32.Sym`
pub const Elf32_Sym = extern struct {
    st_name: Word,
    st_value: Elf32_Addr,
    st_size: Word,
    st_info: u8,
    st_other: u8,
    st_shndx: Elf32_Section,

    pub inline fn st_type(self: @This()) u4 {
        return @truncate(self.st_info);
    }
    pub inline fn st_bind(self: @This()) u4 {
        return @truncate(self.st_info >> 4);
    }
};
/// Deprecated, use `std.elf.Elf64.Sym`
pub const Elf64_Sym = extern struct {
    st_name: Word,
    st_info: u8,
    st_other: u8,
    st_shndx: Elf64_Section,
    st_value: Elf64_Addr,
    st_size: Elf64_Xword,

    pub inline fn st_type(self: @This()) u4 {
        return @truncate(self.st_info);
    }
    pub inline fn st_bind(self: @This()) u4 {
        return @truncate(self.st_info >> 4);
    }
};
pub const Elf32_Syminfo = extern struct {
    si_boundto: Half,
    si_flags: Half,
};
pub const Elf64_Syminfo = extern struct {
    si_boundto: Half,
    si_flags: Half,
};
pub const Elf32_Rel = extern struct {
    r_offset: Elf32_Addr,
    r_info: Word,

    pub inline fn r_sym(self: @This()) u24 {
        return @truncate(self.r_info >> 8);
    }
    pub inline fn r_type(self: @This()) u8 {
        return @truncate(self.r_info);
    }
};
pub const Elf64_Rel = extern struct {
    r_offset: Elf64_Addr,
    r_info: Elf64_Xword,

    pub inline fn r_sym(self: @This()) u32 {
        return @truncate(self.r_info >> 32);
    }
    pub inline fn r_type(self: @This()) u32 {
        return @truncate(self.r_info);
    }
};
pub const Elf32_Rela = extern struct {
    r_offset: Elf32_Addr,
    r_info: Word,
    r_addend: Sword,

    pub inline fn r_sym(self: @This()) u24 {
        return @truncate(self.r_info >> 8);
    }
    pub inline fn r_type(self: @This()) u8 {
        return @truncate(self.r_info);
    }
};
pub const Elf64_Rela = extern struct {
    r_offset: Elf64_Addr,
    r_info: Elf64_Xword,
    r_addend: Elf64_Sxword,

    pub inline fn r_sym(self: @This()) u32 {
        return @truncate(self.r_info >> 32);
    }
    pub inline fn r_type(self: @This()) u32 {
        return @truncate(self.r_info);
    }
};
pub const Elf32_Relr = Word;
pub const Elf64_Relr = Elf64_Xword;
pub const Elf32_Dyn = extern struct {
    d_tag: Sword,
    d_val: Elf32_Addr,
};
pub const Elf64_Dyn = extern struct {
    d_tag: Elf64_Sxword,
    d_val: Elf64_Addr,
};
pub const Verdef = extern struct {
    version: Half,
    flags: Half,
    ndx: VER_NDX,
    cnt: Half,
    hash: Word,
    aux: Word,
    next: Word,
};
pub const Verdaux = extern struct {
    name: Word,
    next: Word,
};
pub const Elf32_Verneed = extern struct {
    vn_version: Half,
    vn_cnt: Half,
    vn_file: Word,
    vn_aux: Word,
    vn_next: Word,
};
pub const Elf64_Verneed = extern struct {
    vn_version: Half,
    vn_cnt: Half,
    vn_file: Word,
    vn_aux: Word,
    vn_next: Word,
};
pub const Vernaux = extern struct {
    hash: Word,
    flags: Half,
    other: Half,
    name: Word,
    next: Word,
};
pub const Elf32_auxv_t = extern struct {
    a_type: u32,
    a_un: extern union {
        a_val: u32,
    },
};
pub const Elf64_auxv_t = extern struct {
    a_type: u64,
    a_un: extern union {
        a_val: u64,
    },
};
pub const Elf32_Nhdr = extern struct {
    n_namesz: Word,
    n_descsz: Word,
    n_type: Word,
};
pub const Elf64_Nhdr = extern struct {
    n_namesz: Word,
    n_descsz: Word,
    n_type: Word,
};
pub const Elf32_Move = extern struct {
    m_value: Elf32_Xword,
    m_info: Word,
    m_poffset: Word,
    m_repeat: Half,
    m_stride: Half,
};
pub const Elf64_Move = extern struct {
    m_value: Elf64_Xword,
    m_info: Elf64_Xword,
    m_poffset: Elf64_Xword,
    m_repeat: Half,
    m_stride: Half,
};
pub const Elf32_gptab = extern union {
    gt_header: extern struct {
        gt_current_g_value: Word,
        gt_unused: Word,
    },
    gt_entry: extern struct {
        gt_g_value: Word,
        gt_bytes: Word,
    },
};
pub const Elf32_RegInfo = extern struct {
    ri_gprmask: Word,
    ri_cprmask: [4]Word,
    ri_gp_value: Sword,
};
pub const Elf_Options = extern struct {
    kind: u8,
    size: u8,
    section: Elf32_Section,
    info: Word,
};
pub const Elf_Options_Hw = extern struct {
    hwp_flags1: Word,
    hwp_flags2: Word,
};
pub const Elf32_Lib = extern struct {
    l_name: Word,
    l_time_stamp: Word,
    l_checksum: Word,
    l_version: Word,
    l_flags: Word,
};
pub const Elf64_Lib = extern struct {
    l_name: Word,
    l_time_stamp: Word,
    l_checksum: Word,
    l_version: Word,
    l_flags: Word,
};
pub const Elf32_Conflict = Elf32_Addr;
pub const Elf_MIPS_ABIFlags_v0 = extern struct {
    version: Half,
    isa_level: u8,
    isa_rev: u8,
    gpr_size: u8,
    cpr1_size: u8,
    cpr2_size: u8,
    fp_abi: u8,
    isa_ext: Word,
    ases: Word,
    flags1: Word,
    flags2: Word,
};

pub const Auxv = switch (@sizeOf(usize)) {
    4 => Elf32_auxv_t,
    8 => Elf64_auxv_t,
    else => @compileError("expected pointer size of 32 or 64"),
};
/// Deprecated, use `std.elf.ElfN.Ehdr`
pub const Ehdr = switch (@sizeOf(usize)) {
    4 => Elf32_Ehdr,
    8 => Elf64_Ehdr,
    else => @compileError("expected pointer size of 32 or 64"),
};
/// Deprecated, use `std.elf.ElfN.Phdr`
pub const Phdr = switch (@sizeOf(usize)) {
    4 => Elf32_Phdr,
    8 => Elf64_Phdr,
    else => @compileError("expected pointer size of 32 or 64"),
};
pub const Dyn = switch (@sizeOf(usize)) {
    4 => Elf32_Dyn,
    8 => Elf64_Dyn,
    else => @compileError("expected pointer size of 32 or 64"),
};
pub const Rel = switch (@sizeOf(usize)) {
    4 => Elf32_Rel,
    8 => Elf64_Rel,
    else => @compileError("expected pointer size of 32 or 64"),
};
pub const Rela = switch (@sizeOf(usize)) {
    4 => Elf32_Rela,
    8 => Elf64_Rela,
    else => @compileError("expected pointer size of 32 or 64"),
};
pub const Relr = switch (@sizeOf(usize)) {
    4 => Elf32_Relr,
    8 => Elf64_Relr,
    else => @compileError("expected pointer size of 32 or 64"),
};
pub const Shdr = switch (@sizeOf(usize)) {
    4 => Elf32_Shdr,
    8 => Elf64_Shdr,
    else => @compileError("expected pointer size of 32 or 64"),
};
/// Deprecated, use `std.elf.ElfN.Chdr`
pub const Chdr = switch (@sizeOf(usize)) {
    4 => Elf32_Chdr,
    8 => Elf64_Chdr,
    else => @compileError("expected pointer size of 32 or 64"),
};
/// Deprecated, use `std.elf.ElfN.Sym`
pub const Sym = switch (@sizeOf(usize)) {
    4 => Elf32_Sym,
    8 => Elf64_Sym,
    else => @compileError("expected pointer size of 32 or 64"),
};
/// Deprecated, use `std.elf.ElfN.Addr`
pub const Addr = ElfN.Addr;

/// Deprecated, use `@intFromEnum(std.elf.CLASS.NONE)`
pub const ELFCLASSNONE = @intFromEnum(CLASS.NONE);
/// Deprecated, use `@intFromEnum(std.elf.CLASS.@"32")`
pub const ELFCLASS32 = @intFromEnum(CLASS.@"32");
/// Deprecated, use `@intFromEnum(std.elf.CLASS.@"64")`
pub const ELFCLASS64 = @intFromEnum(CLASS.@"64");
/// Deprecated, use `@intFromEnum(std.elf.CLASS.NUM)`
pub const ELFCLASSNUM = CLASS.NUM;
pub const CLASS = enum(u8) {
    NONE = 0,
    @"32" = 1,
    @"64" = 2,
    _,

    pub const NUM = @typeInfo(CLASS).@"enum".fields.len;

    pub fn ElfN(comptime class: CLASS) type {
        return switch (class) {
            .NONE, _ => comptime unreachable,
            .@"32" => Elf32,
            .@"64" => Elf64,
        };
    }
};

/// Deprecated, use `@intFromEnum(std.elf.DATA.NONE)`
pub const ELFDATANONE = @intFromEnum(DATA.NONE);
/// Deprecated, use `@intFromEnum(std.elf.DATA.@"2LSB")`
pub const ELFDATA2LSB = @intFromEnum(DATA.@"2LSB");
/// Deprecated, use `@intFromEnum(std.elf.DATA.@"2MSB")`
pub const ELFDATA2MSB = @intFromEnum(DATA.@"2MSB");
/// Deprecated, use `@intFromEnum(std.elf.DATA.NUM)`
pub const ELFDATANUM = DATA.NUM;
pub const DATA = enum(u8) {
    NONE = 0,
    @"2LSB" = 1,
    @"2MSB" = 2,
    _,

    pub const NUM = @typeInfo(DATA).@"enum".fields.len;
};

pub const OSABI = enum(u8) {
    /// UNIX System V ABI
    NONE = 0,
    /// HP-UX operating system
    HPUX = 1,
    /// NetBSD
    NETBSD = 2,
    /// GNU (Hurd/Linux)
    GNU = 3,
    /// Solaris
    SOLARIS = 6,
    /// AIX
    AIX = 7,
    /// IRIX
    IRIX = 8,
    /// FreeBSD
    FREEBSD = 9,
    /// TRU64 UNIX
    TRU64 = 10,
    /// Novell Modesto
    MODESTO = 11,
    /// OpenBSD
    OPENBSD = 12,
    /// OpenVMS
    OPENVMS = 13,
    /// Hewlett-Packard Non-Stop Kernel
    NSK = 14,
    /// AROS
    AROS = 15,
    /// FenixOS
    FENIXOS = 16,
    /// Nuxi CloudABI
    CLOUDABI = 17,
    /// Stratus Technologies OpenVOS
    OPENVOS = 18,
    /// NVIDIA CUDA architecture (not gABI assigned)
    CUDA = 51,
    /// AMD HSA Runtime (not gABI assigned)
    AMDGPU_HSA = 64,
    /// AMD PAL Runtime (not gABI assigned)
    AMDGPU_PAL = 65,
    /// AMD Mesa3D Runtime (not gABI assigned)
    AMDGPU_MESA3D = 66,
    /// ARM (not gABI assigned)
    ARM = 97,
    /// Standalone (embedded) application (not gABI assigned)
    STANDALONE = 255,

    _,
};

/// Machine architectures.
///
/// See current registered ELF machine architectures at:
/// http://www.sco.com/developers/gabi/latest/ch4.eheader.html
pub const EM = enum(u16) {
    /// No machine
    NONE = 0,
    /// AT&T WE 32100
    M32 = 1,
    /// SUN SPARC
    SPARC = 2,
    /// Intel 80386
    @"386" = 3,
    /// Motorola m68k family
    @"68K" = 4,
    /// Motorola m88k family
    @"88K" = 5,
    /// Intel MCU
    IAMCU = 6,
    /// Intel 80860
    @"860" = 7,
    /// MIPS R3000 (officially, big-endian only)
    MIPS = 8,
    /// IBM System/370
    S370 = 9,
    /// MIPS R3000 (and R4000) little-endian, Oct 4 1993 Draft (deprecated)
    MIPS_RS3_LE = 10,
    /// Old version of Sparc v9, from before the ABI (not gABI assigned)
    OLD_SPARCV9 = 11,
    /// HPPA
    PARISC = 15,
    /// Fujitsu VPP500 (also old version of PowerPC, which was not gABI assigned)
    VPP500 = 17,
    /// Sun's "v8plus"
    SPARC32PLUS = 18,
    /// Intel 80960
    @"960" = 19,
    /// PowerPC
    PPC = 20,
    /// 64-bit PowerPC
    PPC64 = 21,
    /// IBM S/390
    S390 = 22,
    /// Sony/Toshiba/IBM SPU
    SPU = 23,
    /// NEC V800 series
    V800 = 36,
    /// Fujitsu FR20
    FR20 = 37,
    /// TRW RH32
    RH32 = 38,
    /// Motorola M*Core, aka RCE (also old Fujitsu MMA, which was not gABI assigned)
    MCORE = 39,
    /// ARM
    ARM = 40,
    /// Digital Alpha
    OLD_ALPHA = 41,
    /// Renesas (formerly Hitachi) / SuperH SH
    SH = 42,
    /// SPARC v9 64-bit
    SPARCV9 = 43,
    /// Siemens Tricore embedded processor
    TRICORE = 44,
    /// ARC Cores
    ARC = 45,
    /// Renesas (formerly Hitachi) H8/300
    H8_300 = 46,
    /// Renesas (formerly Hitachi) H8/300H
    H8_300H = 47,
    /// Renesas (formerly Hitachi) H8S
    H8S = 48,
    /// Renesas (formerly Hitachi) H8/500
    H8_500 = 49,
    /// Intel IA-64 Processor
    IA_64 = 50,
    /// Stanford MIPS-X
    MIPS_X = 51,
    /// Motorola Coldfire
    COLDFIRE = 52,
    /// Motorola M68HC12
    @"68HC12" = 53,
    /// Fujitsu Multimedia Accelerator
    MMA = 54,
    /// Siemens PCP
    PCP = 55,
    /// Sony nCPU embedded RISC processor
    NCPU = 56,
    /// Denso NDR1 microprocessor
    NDR1 = 57,
    /// Motorola Star*Core processor
    STARCORE = 58,
    /// Toyota ME16 processor
    ME16 = 59,
    /// STMicroelectronics ST100 processor
    ST100 = 60,
    /// Advanced Logic Corp. TinyJ embedded processor
    TINYJ = 61,
    /// Advanced Micro Devices X86-64 processor
    X86_64 = 62,
    /// Sony DSP Processor
    PDSP = 63,
    /// Digital Equipment Corp. PDP-10
    PDP10 = 64,
    /// Digital Equipment Corp. PDP-11
    PDP11 = 65,
    /// Siemens FX66 microcontroller
    FX66 = 66,
    /// STMicroelectronics ST9+ 8/16 bit microcontroller
    ST9PLUS = 67,
    /// STMicroelectronics ST7 8-bit microcontroller
    ST7 = 68,
    /// Motorola MC68HC16 Microcontroller
    @"68HC16" = 69,
    /// Motorola MC68HC11 Microcontroller
    @"68HC11" = 70,
    /// Motorola MC68HC08 Microcontroller
    @"68HC08" = 71,
    /// Motorola MC68HC05 Microcontroller
    @"68HC05" = 72,
    /// Silicon Graphics SVx
    SVX = 73,
    /// STMicroelectronics ST19 8-bit cpu
    ST19 = 74,
    /// Digital VAX
    VAX = 75,
    /// Axis Communications 32-bit embedded processor
    CRIS = 76,
    /// Infineon Technologies 32-bit embedded cpu
    JAVELIN = 77,
    /// Element 14 64-bit DSP processor
    FIREPATH = 78,
    /// LSI Logic's 16-bit DSP processor
    ZSP = 79,
    /// Donald Knuth's educational 64-bit processor
    MMIX = 80,
    /// Harvard's machine-independent format
    HUANY = 81,
    /// SiTera Prism
    PRISM = 82,
    /// Atmel AVR 8-bit microcontroller
    AVR = 83,
    /// Fujitsu FR30
    FR30 = 84,
    /// Mitsubishi D10V
    D10V = 85,
    /// Mitsubishi D30V
    D30V = 86,
    /// Renesas V850 (formerly NEC V850)
    V850 = 87,
    /// Renesas M32R (formerly Mitsubishi M32R)
    M32R = 88,
    /// Matsushita MN10300
    MN10300 = 89,
    /// Matsushita MN10200
    MN10200 = 90,
    /// picoJava
    PJ = 91,
    /// OpenRISC 1000 32-bit embedded processor
    OR1K = 92,
    /// ARC International ARCompact processor
    ARC_COMPACT = 93,
    /// Tensilica Xtensa Architecture
    XTENSA = 94,
    /// Alphamosaic VideoCore processor (also old Sunplus S+core7 backend magic number, which was not gABI assigned)
    VIDEOCORE = 95,
    /// Thompson Multimedia General Purpose Processor
    TMM_GPP = 96,
    /// National Semiconductor 32000 series
    NS32K = 97,
    /// Tenor Network TPC processor
    TPC = 98,
    /// Trebia SNP 1000 processor (also old value for picoJava, which was not gABI assigned)
    SNP1K = 99,
    /// STMicroelectronics ST200 microcontroller
    ST200 = 100,
    /// Ubicom IP2022 micro controller
    IP2K = 101,
    /// MAX Processor
    MAX = 102,
    /// National Semiconductor CompactRISC
    CR = 103,
    /// Fujitsu F2MC16
    F2MC16 = 104,
    /// TI msp430 micro controller
    MSP430 = 105,
    /// ADI Blackfin
    BLACKFIN = 106,
    /// S1C33 Family of Seiko Epson processors
    SE_C33 = 107,
    /// Sharp embedded microprocessor
    SEP = 108,
    /// Arca RISC Microprocessor
    ARCA = 109,
    /// Microprocessor series from PKU-Unity Ltd. and MPRC of Peking University
    UNICORE = 110,
    /// eXcess: 16/32/64-bit configurable embedded CPU
    EXCESS = 111,
    /// Icera Semiconductor Inc. Deep Execution Processor
    DXP = 112,
    /// Altera Nios II soft-core processor
    ALTERA_NIOS2 = 113,
    /// National Semiconductor CRX
    CRX = 114,
    /// Motorola XGATE embedded processor (also old value for National Semiconductor CompactRISC, which was not gABI assigned)
    XGATE = 115,
    /// Infineon C16x/XC16x processor
    C166 = 116,
    /// Renesas M16C series microprocessors
    M16C = 117,
    /// Microchip Technology dsPIC30F Digital Signal Controller
    DSPIC30F = 118,
    /// Freescale Communication Engine RISC core
    CE = 119,
    /// Renesas M32C series microprocessors
    M32C = 120,
    /// Altium TSK3000 core
    TSK3000 = 131,
    /// Freescale RS08 embedded processor
    RS08 = 132,
    /// Analog Devices SHARC family of 32-bit DSP processors
    SHARC = 133,
    /// Cyan Technology eCOG2 microprocessor
    ECOG2 = 134,
    /// Sunplus S+core (and S+core7) RISC processor
    SCORE = 135,
    /// New Japan Radio (NJR) 24-bit DSP Processor
    DSP24 = 136,
    /// Broadcom VideoCore III processor
    VIDEOCORE3 = 137,
    /// RISC processor for Lattice FPGA architecture
    LATTICEMICO32 = 138,
    /// Seiko Epson C17 family
    SE_C17 = 139,
    /// Texas Instruments TMS320C6000 DSP family
    TI_C6000 = 140,
    /// Texas Instruments TMS320C2000 DSP family
    TI_C2000 = 141,
    /// Texas Instruments TMS320C55x DSP family
    TI_C5500 = 142,
    /// Texas Instruments Application Specific RISC Processor, 32bit fetch
    TI_ARP32 = 143,
    /// Texas Instruments Programmable Realtime Unit
    TI_PRU = 144,
    /// STMicroelectronics 64bit VLIW Data Signal Processor
    MMDSP_PLUS = 160,
    /// Cypress M8C microprocessor
    CYPRESS_M8C = 161,
    /// Renesas R32C series microprocessors
    R32C = 162,
    /// NXP Semiconductors TriMedia architecture family
    TRIMEDIA = 163,
    /// QUALCOMM DSP6 Processor
    QDSP6 = 164,
    /// Intel 8051 and variants
    @"8051" = 165,
    /// STMicroelectronics STxP7x family
    STXP7X = 166,
    /// Andes Technology compact code size embedded RISC processor family
    NDS32 = 167,
    /// Cyan Technology eCOG1X family
    ECOG1X = 168,
    /// Dallas Semiconductor MAXQ30 Core Micro-controllers
    MAXQ30 = 169,
    /// New Japan Radio (NJR) 16-bit DSP Processor
    XIMO16 = 170,
    /// M2000 Reconfigurable RISC Microprocessor
    MANIK = 171,
    /// Cray Inc. NV2 vector architecture
    CRAYNV2 = 172,
    /// Renesas RX family
    RX = 173,
    /// Imagination Technologies Meta processor architecture
    METAG = 174,
    /// MCST Elbrus general purpose hardware architecture
    MCST_ELBRUS = 175,
    /// Cyan Technology eCOG16 family
    ECOG16 = 176,
    /// National Semiconductor CompactRISC 16-bit processor
    CR16 = 177,
    /// Freescale Extended Time Processing Unit
    ETPU = 178,
    /// Infineon Technologies SLE9X core
    SLE9X = 179,
    /// Intel L10M
    L10M = 180,
    /// Intel K10M
    K10M = 181,
    /// ARM 64-bit architecture
    AARCH64 = 183,
    /// Atmel Corporation 32-bit microprocessor family
    AVR32 = 185,
    /// STMicroeletronics STM8 8-bit microcontroller
    STM8 = 186,
    /// Tilera TILE64 multicore architecture family
    TILE64 = 187,
    /// Tilera TILEPro multicore architecture family
    TILEPRO = 188,
    /// Xilinx MicroBlaze 32-bit RISC soft processor core
    MICROBLAZE = 189,
    /// NVIDIA CUDA architecture
    CUDA = 190,
    /// Tilera TILE-Gx multicore architecture family
    TILEGX = 191,
    /// CloudShield architecture family
    CLOUDSHIELD = 192,
    /// KIPO-KAIST Core-A 1st generation processor family
    COREA_1ST = 193,
    /// KIPO-KAIST Core-A 2nd generation processor family
    COREA_2ND = 194,
    /// Synopsys ARCompact V2
    ARC_COMPACT2 = 195,
    /// Open8 8-bit RISC soft processor core
    OPEN8 = 196,
    /// Renesas RL78 family
    RL78 = 197,
    /// Broadcom VideoCore V processor
    VIDEOCORE5 = 198,
    /// Renesas 78K0R
    @"78K0R" = 199,
    /// Freescale 56800EX Digital Signal Controller (DSC)
    @"56800EX" = 200,
    /// Beyond BA1 CPU architecture
    BA1 = 201,
    /// Beyond BA2 CPU architecture
    BA2 = 202,
    /// XMOS xCORE processor family
    XCORE = 203,
    /// Microchip 8-bit PIC(r) family
    MCHP_PIC = 204,
    /// Intel Graphics Technology
    INTELGT = 205,
    /// KM211 KM32 32-bit processor
    KM32 = 210,
    /// KM211 KMX32 32-bit processor
    KMX32 = 211,
    /// KM211 KMX16 16-bit processor
    KMX16 = 212,
    /// KM211 KMX8 8-bit processor
    KMX8 = 213,
    /// KM211 KVARC processor
    KVARC = 214,
    /// Paneve CDP architecture family
    CDP = 215,
    /// Cognitive Smart Memory Processor
    COGE = 216,
    /// Bluechip Systems CoolEngine
    COOL = 217,
    /// Nanoradio Optimized RISC
    NORC = 218,
    /// CSR Kalimba architecture family
    CSR_KALIMBA = 219,
    /// Zilog Z80
    Z80 = 220,
    /// Controls and Data Services VISIUMcore processor
    VISIUM = 221,
    /// FTDI Chip FT32 high performance 32-bit RISC architecture
    FT32 = 222,
    /// Moxie processor family
    MOXIE = 223,
    /// AMD GPU architecture
    AMDGPU = 224,
    /// RISC-V
    RISCV = 243,
    /// Lanai 32-bit processor
    LANAI = 244,
    /// CEVA Processor Architecture Family
    CEVA = 245,
    /// CEVA X2 Processor Family
    CEVA_X2 = 246,
    /// Linux BPF - in-kernel virtual machine
    BPF = 247,
    /// Graphcore Intelligent Processing Unit
    GRAPHCORE_IPU = 248,
    /// Imagination Technologies
    IMG1 = 249,
    /// Netronome Flow Processor
    NFP = 250,
    /// NEC Vector Engine
    VE = 251,
    /// C-SKY processor family
    CSKY = 252,
    /// Synopsys ARCv2.3 64-bit
    ARC_COMPACT3_64 = 253,
    /// MOS Technology MCS 6502 processor
    MCS6502 = 254,
    /// Synopsys ARCv2.3 32-bit
    ARC_COMPACT3 = 255,
    /// Kalray VLIW core of the MPPA processor family
    KVX = 256,
    /// WDC 65816/65C816
    @"65816" = 257,
    /// LoongArch
    LOONGARCH = 258,
    /// ChipON KungFu32
    KF32 = 259,
    /// LAPIS nX-U16/U8
    U16_U8CORE = 260,
    /// Tachyum
    TACHYUM = 261,
    /// NXP 56800EF Digital Signal Controller (DSC)
    @"56800EF" = 262,
    /// Solana Bytecode Format
    SBF = 263,
    /// AMD/Xilinx AIEngine architecture
    AIENGINE = 264,
    /// SiMa MLA
    SIMA_MLA = 265,
    /// Cambricon BANG
    BANG = 266,
    /// Loongson LoongGPU
    LOONGGPU = 267,
    /// Wuxi Institute of Advanced Technology SW64
    SW64 = 268,
    /// AVR
    AVR_OLD = 0x1057,
    /// MSP430
    MSP430_OLD = 0x1059,
    /// Morpho MT
    MT = 0x2530,
    /// FR30
    CYGNUS_FR30 = 0x3330,
    /// WebAssembly (as used by LLVM)
    WEBASSEMBLY = 0x4157,
    /// Infineon Technologies 16-bit microcontroller with C166-V2 core
    XC16X = 0x4688,
    /// Freescale S12Z
    S12Z = 0x4def,
    /// DLX
    DLX = 0x5aa5,
    /// FRV
    CYGNUS_FRV = 0x5441,
    /// D10V
    CYGNUS_D10V = 0x7650,
    /// D30V
    CYGNUS_D30V = 0x7676,
    /// Ubicom IP2xxx
    IP2K_OLD = 0x8217,
    /// Cygnus PowerPC ELF
    CYGNUS_POWERPC = 0x9025,
    /// Alpha
    ALPHA = 0x9026,
    /// Cygnus M32R ELF
    CYGNUS_M32R = 0x9041,
    /// V850
    CYGNUS_V850 = 0x9080,
    /// Old S/390
    S390_OLD = 0xa390,
    /// Old unofficial value for Xtensa
    XTENSA_OLD = 0xabc7,
    /// Xstormy16
    XSTORMY16 = 0xad45,
    /// MN10300
    CYGNUS_MN10300 = 0xbeef,
    /// MN10200
    CYGNUS_MN10200 = 0xdead,
    /// Renesas M32C and M16C
    M32C_OLD = 0xfeb0,
    /// Vitesse IQ2000
    IQ2000 = 0xfeba,
    /// NIOS
    NIOS32 = 0xfebb,
    /// Toshiba MeP
    CYGNUS_MEP = 0xf00d,
    /// Old unofficial value for Moxie
    MOXIE_OLD = 0xfeed,
    /// Old MicroBlaze
    MICROBLAZE_OLD = 0xbaab,
    /// Adapteva's Epiphany architecture
    ADAPTEVA_EPIPHANY = 0x1223,

    /// Parallax Propeller (P1)
    /// This value is an unofficial ELF value used in: https://github.com/parallaxinc/propgcc
    PROPELLER = 0x5072,

    /// Parallax Propeller 2 (P2)
    /// This value is an unofficial ELF value used in: https://github.com/ne75/llvm-project
    PROPELLER2 = 300,

    _,
};

pub const GRP_COMDAT = 1;

/// Section data should be writable during execution.
pub const SHF_WRITE = 0x1;

/// Section occupies memory during program execution.
pub const SHF_ALLOC = 0x2;

/// Section contains executable machine instructions.
pub const SHF_EXECINSTR = 0x4;

/// The data in this section may be merged.
pub const SHF_MERGE = 0x10;

/// The data in this section is null-terminated strings.
pub const SHF_STRINGS = 0x20;

/// A field in this section holds a section header table index.
pub const SHF_INFO_LINK = 0x40;

/// Adds special ordering requirements for link editors.
pub const SHF_LINK_ORDER = 0x80;

/// This section requires special OS-specific processing to avoid incorrect
/// behavior.
pub const SHF_OS_NONCONFORMING = 0x100;

/// This section is a member of a section group.
pub const SHF_GROUP = 0x200;

/// This section holds Thread-Local Storage.
pub const SHF_TLS = 0x400;

/// Identifies a section containing compressed data.
pub const SHF_COMPRESSED = 0x800;

/// Not to be GCed by the linker
pub const SHF_GNU_RETAIN = 0x200000;

/// This section is excluded from the final executable or shared library.
pub const SHF_EXCLUDE = 0x80000000;

/// Start of target-specific flags.
pub const SHF_MASKOS = 0x0ff00000;

/// Bits indicating processor-specific flags.
pub const SHF_MASKPROC = 0xf0000000;

/// All sections with the "d" flag are grouped together by the linker to form
/// the data section and the dp register is set to the start of the section by
/// the boot code.
pub const XCORE_SHF_DP_SECTION = 0x10000000;

/// All sections with the "c" flag are grouped together by the linker to form
/// the constant pool and the cp register is set to the start of the constant
/// pool by the boot code.
pub const XCORE_SHF_CP_SECTION = 0x20000000;

/// If an object file section does not have this flag set, then it may not hold
/// more than 2GB and can be freely referred to in objects using smaller code
/// models. Otherwise, only objects using larger code models can refer to them.
/// For example, a medium code model object can refer to data in a section that
/// sets this flag besides being able to refer to data in a section that does
/// not set it; likewise, a small code model object can refer only to code in a
/// section that does not set this flag.
pub const SHF_X86_64_LARGE = 0x10000000;

/// All sections with the GPREL flag are grouped into a global data area
/// for faster accesses
pub const SHF_HEX_GPREL = 0x10000000;

/// Section contains text/data which may be replicated in other sections.
/// Linker must retain only one copy.
pub const SHF_MIPS_NODUPES = 0x01000000;

/// Linker must generate implicit hidden weak names.
pub const SHF_MIPS_NAMES = 0x02000000;

/// Section data local to process.
pub const SHF_MIPS_LOCAL = 0x04000000;

/// Do not strip this section.
pub const SHF_MIPS_NOSTRIP = 0x08000000;

/// Section must be part of global data area.
pub const SHF_MIPS_GPREL = 0x10000000;

/// This section should be merged.
pub const SHF_MIPS_MERGE = 0x20000000;

/// Address size to be inferred from section entry size.
pub const SHF_MIPS_ADDR = 0x40000000;

/// Section data is string data by default.
pub const SHF_MIPS_STRING = 0x80000000;

/// Make code section unreadable when in execute-only mode
pub const SHF_ARM_PURECODE = 0x2000000;

pub const SHF = packed struct(Word) {
    /// Section data should be writable during execution.
    WRITE: bool = false,
    /// Section occupies memory during program execution.
    ALLOC: bool = false,
    /// Section contains executable machine instructions.
    EXECINSTR: bool = false,
    unused3: u1 = 0,
    /// The data in this section may be merged.
    MERGE: bool = false,
    /// The data in this section is null-terminated strings.
    STRINGS: bool = false,
    /// A field in this section holds a section header table index.
    INFO_LINK: bool = false,
    /// Adds special ordering requirements for link editors.
    LINK_ORDER: bool = false,
    /// This section requires special OS-specific processing to avoid incorrect behavior.
    OS_NONCONFORMING: bool = false,
    /// This section is a member of a section group.
    GROUP: bool = false,
    /// This section holds Thread-Local Storage.
    TLS: bool = false,
    /// Identifies a section containing compressed data.
    COMPRESSED: bool = false,
    unused12: u8 = 0,
    OS: packed union {
        MASK: u8,
        GNU: packed struct(u8) {
            unused0: u1 = 0,
            /// Not to be GCed by the linker
            RETAIN: bool = false,
            unused2: u6 = 0,
        },
        MIPS: packed struct(u8) {
            unused0: u4 = 0,
            /// Section contains text/data which may be replicated in other sections.
            /// Linker must retain only one copy.
            NODUPES: bool = false,
            /// Linker must generate implicit hidden weak names.
            NAMES: bool = false,
            /// Section data local to process.
            LOCAL: bool = false,
            /// Do not strip this section.
            NOSTRIP: bool = false,
        },
        ARM: packed struct(u8) {
            unused0: u5 = 0,
            /// Make code section unreadable when in execute-only mode
            PURECODE: bool = false,
            unused6: u2 = 0,
        },
    } = .{ .MASK = 0 },
    PROC: packed union {
        MASK: u4,
        XCORE: packed struct(u4) {
            /// All sections with the "d" flag are grouped together by the linker to form
            /// the data section and the dp register is set to the start of the section by
            /// the boot code.
            DP_SECTION: bool = false,
            /// All sections with the "c" flag are grouped together by the linker to form
            /// the constant pool and the cp register is set to the start of the constant
            /// pool by the boot code.
            CP_SECTION: bool = false,
            unused2: u1 = 0,
            /// This section is excluded from the final executable or shared library.
            EXCLUDE: bool = false,
        },
        X86_64: packed struct(u4) {
            /// If an object file section does not have this flag set, then it may not hold
            /// more than 2GB and can be freely referred to in objects using smaller code
            /// models. Otherwise, only objects using larger code models can refer to them.
            /// For example, a medium code model object can refer to data in a section that
            /// sets this flag besides being able to refer to data in a section that does
            /// not set it; likewise, a small code model object can refer only to code in a
            /// section that does not set this flag.
            LARGE: bool = false,
            unused1: u2 = 0,
            /// This section is excluded from the final executable or shared library.
            EXCLUDE: bool = false,
        },
        HEX: packed struct(u4) {
            /// All sections with the GPREL flag are grouped into a global data area
            /// for faster accesses
            GPREL: bool = false,
            unused1: u2 = 0,
            /// This section is excluded from the final executable or shared library.
            EXCLUDE: bool = false,
        },
        MIPS: packed struct(u4) {
            /// All sections with the GPREL flag are grouped into a global data area
            /// for faster accesses
            GPREL: bool = false,
            /// This section should be merged.
            MERGE: bool = false,
            /// Address size to be inferred from section entry size.
            ADDR: bool = false,
            /// Section data is string data by default.
            STRING: bool = false,
        },
    } = .{ .MASK = 0 },
};

/// Execute
pub const PF_X = 1;

/// Write
pub const PF_W = 2;

/// Read
pub const PF_R = 4;

/// Bits for operating system-specific semantics.
pub const PF_MASKOS = 0x0ff00000;

/// Bits for processor-specific semantics.
pub const PF_MASKPROC = 0xf0000000;

pub const PF = packed struct(Word) {
    X: bool = false,
    W: bool = false,
    R: bool = false,
    unused3: u17 = 0,
    OS: packed union {
        MASK: u8,
    } = .{ .MASK = 0 },
    PROC: packed union {
        MASK: u4,
    } = .{ .MASK = 0 },
};

/// Undefined section
pub const SHN_UNDEF = 0;
/// Start of reserved indices
pub const SHN_LORESERVE = 0xff00;
/// Start of processor-specific
pub const SHN_LOPROC = 0xff00;
/// End of processor-specific
pub const SHN_HIPROC = 0xff1f;
pub const SHN_LIVEPATCH = 0xff20;
/// Associated symbol is absolute
pub const SHN_ABS = 0xfff1;
/// Associated symbol is common
pub const SHN_COMMON = 0xfff2;
/// End of reserved indices
pub const SHN_HIRESERVE = 0xffff;

// Legal values for ch_type (compression algorithm).
pub const COMPRESS = enum(u32) {
    ZLIB = 1,
    ZSTD = 2,
    LOOS = 0x60000000,
    HIOS = 0x6fffffff,
    LOPROC = 0x70000000,
    HIPROC = 0x7fffffff,
    _,
};

/// AMD x86-64 relocations.
pub const R_X86_64 = enum(u32) {
    /// No reloc
    NONE = 0,
    /// Direct 64 bit
    @"64" = 1,
    /// PC relative 32 bit signed
    PC32 = 2,
    /// 32 bit GOT entry
    GOT32 = 3,
    /// 32 bit PLT address
    PLT32 = 4,
    /// Copy symbol at runtime
    COPY = 5,
    /// Create GOT entry
    GLOB_DAT = 6,
    /// Create PLT entry
    JUMP_SLOT = 7,
    /// Adjust by program base
    RELATIVE = 8,
    /// 32 bit signed PC relative offset to GOT
    GOTPCREL = 9,
    /// Direct 32 bit zero extended
    @"32" = 10,
    /// Direct 32 bit sign extended
    @"32S" = 11,
    /// Direct 16 bit zero extended
    @"16" = 12,
    /// 16 bit sign extended pc relative
    PC16 = 13,
    /// Direct 8 bit sign extended
    @"8" = 14,
    /// 8 bit sign extended pc relative
    PC8 = 15,
    /// ID of module containing symbol
    DTPMOD64 = 16,
    /// Offset in module's TLS block
    DTPOFF64 = 17,
    /// Offset in initial TLS block
    TPOFF64 = 18,
    /// 32 bit signed PC relative offset to two GOT entries for GD symbol
    TLSGD = 19,
    /// 32 bit signed PC relative offset to two GOT entries for LD symbol
    TLSLD = 20,
    /// Offset in TLS block
    DTPOFF32 = 21,
    /// 32 bit signed PC relative offset to GOT entry for IE symbol
    GOTTPOFF = 22,
    /// Offset in initial TLS block
    TPOFF32 = 23,
    /// PC relative 64 bit
    PC64 = 24,
    /// 64 bit offset to GOT
    GOTOFF64 = 25,
    /// 32 bit signed pc relative offset to GOT
    GOTPC32 = 26,
    /// 64 bit GOT entry offset
    GOT64 = 27,
    /// 64 bit PC relative offset to GOT entry
    GOTPCREL64 = 28,
    /// 64 bit PC relative offset to GOT
    GOTPC64 = 29,
    /// Like GOT64, says PLT entry needed
    GOTPLT64 = 30,
    /// 64-bit GOT relative offset to PLT entry
    PLTOFF64 = 31,
    /// Size of symbol plus 32-bit addend
    SIZE32 = 32,
    /// Size of symbol plus 64-bit addend
    SIZE64 = 33,
    /// GOT offset for TLS descriptor
    GOTPC32_TLSDESC = 34,
    /// Marker for call through TLS descriptor
    TLSDESC_CALL = 35,
    /// TLS descriptor
    TLSDESC = 36,
    /// Adjust indirectly by program base
    IRELATIVE = 37,
    /// 64-bit adjust by program base
    RELATIVE64 = 38,
    /// 39 Reserved was PC32_BND
    /// 40 Reserved was PLT32_BND
    /// Load from 32 bit signed pc relative offset to GOT entry without REX prefix, relaxable
    GOTPCRELX = 41,
    /// Load from 32 bit signed PC relative offset to GOT entry with REX prefix, relaxable
    REX_GOTPCRELX = 42,
    _,
};

/// AArch64 relocations.
pub const R_AARCH64 = enum(u32) {
    /// No relocation.
    NONE = 0,
    /// ILP32 AArch64 relocs.
    /// Direct 32 bit.
    P32_ABS32 = 1,
    /// Copy symbol at runtime.
    P32_COPY = 180,
    /// Create GOT entry.
    P32_GLOB_DAT = 181,
    /// Create PLT entry.
    P32_JUMP_SLOT = 182,
    /// Adjust by program base.
    P32_RELATIVE = 183,
    /// Module number, 32 bit.
    P32_TLS_DTPMOD = 184,
    /// Module-relative offset, 32 bit.
    P32_TLS_DTPREL = 185,
    /// TP-relative offset, 32 bit.
    P32_TLS_TPREL = 186,
    /// TLS Descriptor.
    P32_TLSDESC = 187,
    /// STT_GNU_IFUNC relocation.
    P32_IRELATIVE = 188,
    /// LP64 AArch64 relocs.
    /// Direct 64 bit.
    ABS64 = 257,
    /// Direct 32 bit.
    ABS32 = 258,
    /// Direct 16-bit.
    ABS16 = 259,
    /// PC-relative 64-bit.
    PREL64 = 260,
    /// PC-relative 32-bit.
    PREL32 = 261,
    /// PC-relative 16-bit.
    PREL16 = 262,
    /// Dir. MOVZ imm. from bits 15:0.
    MOVW_UABS_G0 = 263,
    /// Likewise for MOVK; no check.
    MOVW_UABS_G0_NC = 264,
    /// Dir. MOVZ imm. from bits 31:16.
    MOVW_UABS_G1 = 265,
    /// Likewise for MOVK; no check.
    MOVW_UABS_G1_NC = 266,
    /// Dir. MOVZ imm. from bits 47:32.
    MOVW_UABS_G2 = 267,
    /// Likewise for MOVK; no check.
    MOVW_UABS_G2_NC = 268,
    /// Dir. MOV{K,Z} imm. from 63:48.
    MOVW_UABS_G3 = 269,
    /// Dir. MOV{N,Z} imm. from 15:0.
    MOVW_SABS_G0 = 270,
    /// Dir. MOV{N,Z} imm. from 31:16.
    MOVW_SABS_G1 = 271,
    /// Dir. MOV{N,Z} imm. from 47:32.
    MOVW_SABS_G2 = 272,
    /// PC-rel. LD imm. from bits 20:2.
    LD_PREL_LO19 = 273,
    /// PC-rel. ADR imm. from bits 20:0.
    ADR_PREL_LO21 = 274,
    /// Page-rel. ADRP imm. from 32:12.
    ADR_PREL_PG_HI21 = 275,
    /// Likewise; no overflow check.
    ADR_PREL_PG_HI21_NC = 276,
    /// Dir. ADD imm. from bits 11:0.
    ADD_ABS_LO12_NC = 277,
    /// Likewise for LD/ST; no check.
    LDST8_ABS_LO12_NC = 278,
    /// PC-rel. TBZ/TBNZ imm. from 15:2.
    TSTBR14 = 279,
    /// PC-rel. cond. br. imm. from 20:2.
    CONDBR19 = 280,
    /// PC-rel. B imm. from bits 27:2.
    JUMP26 = 282,
    /// Likewise for CALL.
    CALL26 = 283,
    /// Dir. ADD imm. from bits 11:1.
    LDST16_ABS_LO12_NC = 284,
    /// Likewise for bits 11:2.
    LDST32_ABS_LO12_NC = 285,
    /// Likewise for bits 11:3.
    LDST64_ABS_LO12_NC = 286,
    /// PC-rel. MOV{N,Z} imm. from 15:0.
    MOVW_PREL_G0 = 287,
    /// Likewise for MOVK; no check.
    MOVW_PREL_G0_NC = 288,
    /// PC-rel. MOV{N,Z} imm. from 31:16.
    MOVW_PREL_G1 = 289,
    /// Likewise for MOVK; no check.
    MOVW_PREL_G1_NC = 290,
    /// PC-rel. MOV{N,Z} imm. from 47:32.
    MOVW_PREL_G2 = 291,
    /// Likewise for MOVK; no check.
    MOVW_PREL_G2_NC = 292,
    /// PC-rel. MOV{N,Z} imm. from 63:48.
    MOVW_PREL_G3 = 293,
    /// Dir. ADD imm. from bits 11:4.
    LDST128_ABS_LO12_NC = 299,
    /// GOT-rel. off. MOV{N,Z} imm. 15:0.
    MOVW_GOTOFF_G0 = 300,
    /// Likewise for MOVK; no check.
    MOVW_GOTOFF_G0_NC = 301,
    /// GOT-rel. o. MOV{N,Z} imm. 31:16.
    MOVW_GOTOFF_G1 = 302,
    /// Likewise for MOVK; no check.
    MOVW_GOTOFF_G1_NC = 303,
    /// GOT-rel. o. MOV{N,Z} imm. 47:32.
    MOVW_GOTOFF_G2 = 304,
    /// Likewise for MOVK; no check.
    MOVW_GOTOFF_G2_NC = 305,
    /// GOT-rel. o. MOV{N,Z} imm. 63:48.
    MOVW_GOTOFF_G3 = 306,
    /// GOT-relative 64-bit.
    GOTREL64 = 307,
    /// GOT-relative 32-bit.
    GOTREL32 = 308,
    /// PC-rel. GOT off. load imm. 20:2.
    GOT_LD_PREL19 = 309,
    /// GOT-rel. off. LD/ST imm. 14:3.
    LD64_GOTOFF_LO15 = 310,
    /// P-page-rel. GOT off. ADRP 32:12.
    ADR_GOT_PAGE = 311,
    /// Dir. GOT off. LD/ST imm. 11:3.
    LD64_GOT_LO12_NC = 312,
    /// GOT-page-rel. GOT off. LD/ST 14:3
    LD64_GOTPAGE_LO15 = 313,
    /// PC-relative ADR imm. 20:0.
    TLSGD_ADR_PREL21 = 512,
    /// page-rel. ADRP imm. 32:12.
    TLSGD_ADR_PAGE21 = 513,
    /// direct ADD imm. from 11:0.
    TLSGD_ADD_LO12_NC = 514,
    /// GOT-rel. MOV{N,Z} 31:16.
    TLSGD_MOVW_G1 = 515,
    /// GOT-rel. MOVK imm. 15:0.
    TLSGD_MOVW_G0_NC = 516,
    /// Like 512; local dynamic model.
    TLSLD_ADR_PREL21 = 517,
    /// Like 513; local dynamic model.
    TLSLD_ADR_PAGE21 = 518,
    /// Like 514; local dynamic model.
    TLSLD_ADD_LO12_NC = 519,
    /// Like 515; local dynamic model.
    TLSLD_MOVW_G1 = 520,
    /// Like 516; local dynamic model.
    TLSLD_MOVW_G0_NC = 521,
    /// TLS PC-rel. load imm. 20:2.
    TLSLD_LD_PREL19 = 522,
    /// TLS DTP-rel. MOV{N,Z} 47:32.
    TLSLD_MOVW_DTPREL_G2 = 523,
    /// TLS DTP-rel. MOV{N,Z} 31:16.
    TLSLD_MOVW_DTPREL_G1 = 524,
    /// Likewise; MOVK; no check.
    TLSLD_MOVW_DTPREL_G1_NC = 525,
    /// TLS DTP-rel. MOV{N,Z} 15:0.
    TLSLD_MOVW_DTPREL_G0 = 526,
    /// Likewise; MOVK; no check.
    TLSLD_MOVW_DTPREL_G0_NC = 527,
    /// DTP-rel. ADD imm. from 23:12.
    TLSLD_ADD_DTPREL_HI12 = 528,
    /// DTP-rel. ADD imm. from 11:0.
    TLSLD_ADD_DTPREL_LO12 = 529,
    /// Likewise; no ovfl. check.
    TLSLD_ADD_DTPREL_LO12_NC = 530,
    /// DTP-rel. LD/ST imm. 11:0.
    TLSLD_LDST8_DTPREL_LO12 = 531,
    /// Likewise; no check.
    TLSLD_LDST8_DTPREL_LO12_NC = 532,
    /// DTP-rel. LD/ST imm. 11:1.
    TLSLD_LDST16_DTPREL_LO12 = 533,
    /// Likewise; no check.
    TLSLD_LDST16_DTPREL_LO12_NC = 534,
    /// DTP-rel. LD/ST imm. 11:2.
    TLSLD_LDST32_DTPREL_LO12 = 535,
    /// Likewise; no check.
    TLSLD_LDST32_DTPREL_LO12_NC = 536,
    /// DTP-rel. LD/ST imm. 11:3.
    TLSLD_LDST64_DTPREL_LO12 = 537,
    /// Likewise; no check.
    TLSLD_LDST64_DTPREL_LO12_NC = 538,
    /// GOT-rel. MOV{N,Z} 31:16.
    TLSIE_MOVW_GOTTPREL_G1 = 539,
    /// GOT-rel. MOVK 15:0.
    TLSIE_MOVW_GOTTPREL_G0_NC = 540,
    /// Page-rel. ADRP 32:12.
    TLSIE_ADR_GOTTPREL_PAGE21 = 541,
    /// Direct LD off. 11:3.
    TLSIE_LD64_GOTTPREL_LO12_NC = 542,
    /// PC-rel. load imm. 20:2.
    TLSIE_LD_GOTTPREL_PREL19 = 543,
    /// TLS TP-rel. MOV{N,Z} 47:32.
    TLSLE_MOVW_TPREL_G2 = 544,
    /// TLS TP-rel. MOV{N,Z} 31:16.
    TLSLE_MOVW_TPREL_G1 = 545,
    /// Likewise; MOVK; no check.
    TLSLE_MOVW_TPREL_G1_NC = 546,
    /// TLS TP-rel. MOV{N,Z} 15:0.
    TLSLE_MOVW_TPREL_G0 = 547,
    /// Likewise; MOVK; no check.
    TLSLE_MOVW_TPREL_G0_NC = 548,
    /// TP-rel. ADD imm. 23:12.
    TLSLE_ADD_TPREL_HI12 = 549,
    /// TP-rel. ADD imm. 11:0.
    TLSLE_ADD_TPREL_LO12 = 550,
    /// Likewise; no ovfl. check.
    TLSLE_ADD_TPREL_LO12_NC = 551,
    /// TP-rel. LD/ST off. 11:0.
    TLSLE_LDST8_TPREL_LO12 = 552,
    /// Likewise; no ovfl. check.
    TLSLE_LDST8_TPREL_LO12_NC = 553,
    /// TP-rel. LD/ST off. 11:1.
    TLSLE_LDST16_TPREL_LO12 = 554,
    /// Likewise; no check.
    TLSLE_LDST16_TPREL_LO12_NC = 555,
    /// TP-rel. LD/ST off. 11:2.
    TLSLE_LDST32_TPREL_LO12 = 556,
    /// Likewise; no check.
    TLSLE_LDST32_TPREL_LO12_NC = 557,
    /// TP-rel. LD/ST off. 11:3.
    TLSLE_LDST64_TPREL_LO12 = 558,
    /// Likewise; no check.
    TLSLE_LDST64_TPREL_LO12_NC = 559,
    /// PC-rel. load immediate 20:2.
    TLSDESC_LD_PREL19 = 560,
    /// PC-rel. ADR immediate 20:0.
    TLSDESC_ADR_PREL21 = 561,
    /// Page-rel. ADRP imm. 32:12.
    TLSDESC_ADR_PAGE21 = 562,
    /// Direct LD off. from 11:3.
    TLSDESC_LD64_LO12 = 563,
    /// Direct ADD imm. from 11:0.
    TLSDESC_ADD_LO12 = 564,
    /// GOT-rel. MOV{N,Z} imm. 31:16.
    TLSDESC_OFF_G1 = 565,
    /// GOT-rel. MOVK imm. 15:0; no ck.
    TLSDESC_OFF_G0_NC = 566,
    /// Relax LDR.
    TLSDESC_LDR = 567,
    /// Relax ADD.
    TLSDESC_ADD = 568,
    /// Relax BLR.
    TLSDESC_CALL = 569,
    /// TP-rel. LD/ST off. 11:4.
    TLSLE_LDST128_TPREL_LO12 = 570,
    /// Likewise; no check.
    TLSLE_LDST128_TPREL_LO12_NC = 571,
    /// DTP-rel. LD/ST imm. 11:4.
    TLSLD_LDST128_DTPREL_LO12 = 572,
    /// Likewise; no check.
    TLSLD_LDST128_DTPREL_LO12_NC = 573,
    /// Copy symbol at runtime.
    COPY = 1024,
    /// Create GOT entry.
    GLOB_DAT = 1025,
    /// Create PLT entry.
    JUMP_SLOT = 1026,
    /// Adjust by program base.
    RELATIVE = 1027,
    /// Module number, 64 bit.
    TLS_DTPMOD = 1028,
    /// Module-relative offset, 64 bit.
    TLS_DTPREL = 1029,
    /// TP-relative offset, 64 bit.
    TLS_TPREL = 1030,
    /// TLS Descriptor.
    TLSDESC = 1031,
    /// STT_GNU_IFUNC relocation.
    IRELATIVE = 1032,
    _,
};

/// RISC-V relocations.
pub const R_RISCV = enum(u32) {
    NONE = 0,
    @"32" = 1,
    @"64" = 2,
    RELATIVE = 3,
    COPY = 4,
    JUMP_SLOT = 5,
    TLS_DTPMOD32 = 6,
    TLS_DTPMOD64 = 7,
    TLS_DTPREL32 = 8,
    TLS_DTPREL64 = 9,
    TLS_TPREL32 = 10,
    TLS_TPREL64 = 11,
    TLSDESC = 12,
    BRANCH = 16,
    JAL = 17,
    CALL = 18,
    CALL_PLT = 19,
    GOT_HI20 = 20,
    TLS_GOT_HI20 = 21,
    TLS_GD_HI20 = 22,
    PCREL_HI20 = 23,
    PCREL_LO12_I = 24,
    PCREL_LO12_S = 25,
    HI20 = 26,
    LO12_I = 27,
    LO12_S = 28,
    TPREL_HI20 = 29,
    TPREL_LO12_I = 30,
    TPREL_LO12_S = 31,
    TPREL_ADD = 32,
    ADD8 = 33,
    ADD16 = 34,
    ADD32 = 35,
    ADD64 = 36,
    SUB8 = 37,
    SUB16 = 38,
    SUB32 = 39,
    SUB64 = 40,
    GNU_VTINHERIT = 41,
    GNU_VTENTRY = 42,
    ALIGN = 43,
    RVC_BRANCH = 44,
    RVC_JUMP = 45,
    RVC_LUI = 46,
    GPREL_I = 47,
    GPREL_S = 48,
    TPREL_I = 49,
    TPREL_S = 50,
    RELAX = 51,
    SUB6 = 52,
    SET6 = 53,
    SET8 = 54,
    SET16 = 55,
    SET32 = 56,
    @"32_PCREL" = 57,
    IRELATIVE = 58,
    PLT32 = 59,
    SET_ULEB128 = 60,
    SUB_ULEB128 = 61,
    _,
};

/// PowerPC64 relocations.
pub const R_PPC64 = enum(u32) {
    NONE = 0,
    ADDR32 = 1,
    ADDR24 = 2,
    ADDR16 = 3,
    ADDR16_LO = 4,
    ADDR16_HI = 5,
    ADDR16_HA = 6,
    ADDR14 = 7,
    ADDR14_BRTAKEN = 8,
    ADDR14_BRNTAKEN = 9,
    REL24 = 10,
    REL14 = 11,
    REL14_BRTAKEN = 12,
    REL14_BRNTAKEN = 13,
    GOT16 = 14,
    GOT16_LO = 15,
    GOT16_HI = 16,
    GOT16_HA = 17,
    COPY = 19,
    GLOB_DAT = 20,
    JMP_SLOT = 21,
    RELATIVE = 22,
    REL32 = 26,
    PLT16_LO = 29,
    PLT16_HI = 30,
    PLT16_HA = 31,
    ADDR64 = 38,
    ADDR16_HIGHER = 39,
    ADDR16_HIGHERA = 40,
    ADDR16_HIGHEST = 41,
    ADDR16_HIGHESTA = 42,
    REL64 = 44,
    TOC16 = 47,
    TOC16_LO = 48,
    TOC16_HI = 49,
    TOC16_HA = 50,
    TOC = 51,
    ADDR16_DS = 56,
    ADDR16_LO_DS = 57,
    GOT16_DS = 58,
    GOT16_LO_DS = 59,
    PLT16_LO_DS = 60,
    TOC16_DS = 63,
    TOC16_LO_DS = 64,
    TLS = 67,
    DTPMOD64 = 68,
    TPREL16 = 69,
    TPREL16_LO = 70,
    TPREL16_HI = 71,
    TPREL16_HA = 72,
    TPREL64 = 73,
    DTPREL16 = 74,
    DTPREL16_LO = 75,
    DTPREL16_HI = 76,
    DTPREL16_HA = 77,
    DTPREL64 = 78,
    GOT_TLSGD16 = 79,
    GOT_TLSGD16_LO = 80,
    GOT_TLSGD16_HI = 81,
    GOT_TLSGD16_HA = 82,
    GOT_TLSLD16 = 83,
    GOT_TLSLD16_LO = 84,
    GOT_TLSLD16_HI = 85,
    GOT_TLSLD16_HA = 86,
    GOT_TPREL16_DS = 87,
    GOT_TPREL16_LO_DS = 88,
    GOT_TPREL16_HI = 89,
    GOT_TPREL16_HA = 90,
    GOT_DTPREL16_DS = 91,
    GOT_DTPREL16_LO_DS = 92,
    GOT_DTPREL16_HI = 93,
    GOT_DTPREL16_HA = 94,
    TPREL16_DS = 95,
    TPREL16_LO_DS = 96,
    TPREL16_HIGHER = 97,
    TPREL16_HIGHERA = 98,
    TPREL16_HIGHEST = 99,
    TPREL16_HIGHESTA = 100,
    DTPREL16_DS = 101,
    DTPREL16_LO_DS = 102,
    DTPREL16_HIGHER = 103,
    DTPREL16_HIGHERA = 104,
    DTPREL16_HIGHEST = 105,
    DTPREL16_HIGHESTA = 106,
    TLSGD = 107,
    TLSLD = 108,
    ADDR16_HIGH = 110,
    ADDR16_HIGHA = 111,
    TPREL16_HIGH = 112,
    TPREL16_HIGHA = 113,
    DTPREL16_HIGH = 114,
    DTPREL16_HIGHA = 115,
    REL24_NOTOC = 116,
    PLTSEQ = 119,
    PLTCALL = 120,
    PLTSEQ_NOTOC = 121,
    PLTCALL_NOTOC = 122,
    PCREL_OPT = 123,
    PCREL34 = 132,
    GOT_PCREL34 = 133,
    PLT_PCREL34 = 134,
    PLT_PCREL34_NOTOC = 135,
    TPREL34 = 146,
    DTPREL34 = 147,
    GOT_TLSGD_PCREL34 = 148,
    GOT_TLSLD_PCREL34 = 149,
    GOT_TPREL_PCREL34 = 150,
    IRELATIVE = 248,
    REL16 = 249,
    REL16_LO = 250,
    REL16_HI = 251,
    REL16_HA = 252,
    _,
};

pub const ar_hdr = extern struct {
    /// Member file name, sometimes / terminated.
    ar_name: [16]u8,

    /// File date, decimal seconds since Epoch.
    ar_date: [12]u8,

    /// User ID, in ASCII format.
    ar_uid: [6]u8,

    /// Group ID, in ASCII format.
    ar_gid: [6]u8,

    /// File mode, in ASCII octal.
    ar_mode: [8]u8,

    /// File size, in ASCII decimal.
    ar_size: [10]u8,

    /// Always contains ARFMAG.
    ar_fmag: [2]u8,

    pub fn date(self: ar_hdr) std.fmt.ParseIntError!u64 {
        const value = mem.trimEnd(u8, &self.ar_date, &[_]u8{0x20});
        return std.fmt.parseInt(u64, value, 10);
    }

    pub fn size(self: ar_hdr) std.fmt.ParseIntError!u32 {
        const value = mem.trimEnd(u8, &self.ar_size, &[_]u8{0x20});
        return std.fmt.parseInt(u32, value, 10);
    }

    pub fn isStrtab(self: ar_hdr) bool {
        return mem.eql(u8, &self.ar_name, STRNAME);
    }

    pub fn isSymtab(self: ar_hdr) bool {
        return mem.eql(u8, &self.ar_name, SYMNAME);
    }

    pub fn isSymtab64(self: ar_hdr) bool {
        return mem.eql(u8, &self.ar_name, SYM64NAME);
    }

    pub fn isSymdef(self: ar_hdr) bool {
        return mem.eql(u8, &self.ar_name, SYMDEFNAME);
    }

    pub fn isSymdefSorted(self: ar_hdr) bool {
        return mem.eql(u8, &self.ar_name, SYMDEFSORTEDNAME);
    }

    pub fn name(self: *const ar_hdr) ?[]const u8 {
        const value = &self.ar_name;
        if (value[0] == '/') return null;
        const sentinel = mem.findScalar(u8, value, '/') orelse value.len;
        return value[0..sentinel];
    }

    pub fn nameOffset(self: ar_hdr) std.fmt.ParseIntError!?u32 {
        const value = &self.ar_name;
        if (value[0] != '/') return null;
        const trimmed = mem.trimEnd(u8, value, &[_]u8{0x20});
        return try std.fmt.parseInt(u32, trimmed[1..], 10);
    }
};

fn genSpecialMemberName(comptime name: []const u8) *const [16]u8 {
    assert(name.len <= 16);
    const padding = 16 - name.len;
    return name ++ &[_]u8{0x20} ** padding;
}

// Archive files start with the ARMAG identifying string.  Then follows a
// `struct ar_hdr', and as many bytes of member file data as its `ar_size'
// member indicates, for each member file.
/// String that begins an archive file.
pub const ARMAG = "!<arch>\n";
/// String that begins a thin archive file.
pub const ARMAG_THIN = "!<thin>\n";
/// String in ar_fmag at the end of each header.
pub const ARFMAG = "`\n";
/// 32-bit symtab identifier
pub const SYMNAME = genSpecialMemberName("/");
/// Strtab identifier
pub const STRNAME = genSpecialMemberName("//");
/// 64-bit symtab identifier
pub const SYM64NAME = genSpecialMemberName("/SYM64/");
pub const SYMDEFNAME = genSpecialMemberName("__.SYMDEF");
pub const SYMDEFSORTEDNAME = genSpecialMemberName("__.SYMDEF SORTED");

pub const gnu_hash = struct {

    // See https://flapenguin.me/elf-dt-gnu-hash

    pub const Header = extern struct {
        nbuckets: u32,
        symoffset: u32,
        bloom_size: u32,
        bloom_shift: u32,
    };

    pub const ChainEntry = packed struct(u32) {
        end_of_chain: bool,
        /// Contains the top bits of the hash value.
        hash: u31,
    };

    /// Calculate the hash value for a name
    pub fn calculate(name: []const u8) u32 {
        var hash: u32 = 5381;

        for (name) |char| {
            hash = (hash << 5) +% hash +% char;
        }

        return hash;
    }

    test calculate {
        try std.testing.expectEqual(0x00001505, calculate(""));
        try std.testing.expectEqual(0x156b2bb8, calculate("printf"));
        try std.testing.expectEqual(0x7c967e3f, calculate("exit"));
        try std.testing.expectEqual(0xbac212a0, calculate("syscall"));
        try std.testing.expectEqual(0x8ae9f18e, calculate("flapenguin.me"));
    }
};



---
File: /std/enums.zig
---

//! This module contains utilities and data structures for working with enums.

const std = @import("std");
const assert = std.debug.assert;
const testing = std.testing;
const EnumField = std.builtin.Type.EnumField;

/// Increment this value when adding APIs that add single backwards branches.
const eval_branch_quota_cushion = 10;

pub fn fromInt(comptime E: type, integer: anytype) ?E {
    const enum_info = @typeInfo(E).@"enum";
    if (!enum_info.is_exhaustive) {
        if (std.math.cast(enum_info.tag_type, integer)) |tag| {
            return @enumFromInt(tag);
        }
        return null;
    }
    // We don't directly iterate over the fields of E, as that
    // would require an inline loop. Instead, we create an array of
    // values that is comptime-know, but can be iterated at runtime
    // without requiring an inline loop.
    // This generates better machine code.
    for (values(E)) |value| {
        if (@intFromEnum(value) == integer) return value;
    }
    return null;
}

/// Returns a struct with a field matching each unique named enum element.
/// If the enum is extern and has multiple names for the same value, only
/// the first name is used.  Each field is of type Data and has the provided
/// default, which may be undefined.
pub fn EnumFieldStruct(comptime E: type, comptime Data: type, comptime field_default: ?Data) type {
    @setEvalBranchQuota(@typeInfo(E).@"enum".fields.len + eval_branch_quota_cushion);
    const default_ptr: ?*const anyopaque = if (field_default) |d| @ptrCast(&d) else null;
    return @Struct(.auto, null, std.meta.fieldNames(E), &@splat(Data), &@splat(.{ .default_value_ptr = default_ptr }));
}

/// Looks up the supplied fields in the given enum type.
/// Uses only the field names, field values are ignored.
/// The result array is in the same order as the input.
pub inline fn valuesFromFields(comptime E: type, comptime fields: []const EnumField) []const E {
    comptime {
        @setEvalBranchQuota(@typeInfo(E).@"enum".fields.len + eval_branch_quota_cushion);
        var result: [fields.len]E = undefined;
        for (&result, fields) |*r, f| {
            r.* = @enumFromInt(f.value);
        }
        const final = result;
        return &final;
    }
}

/// Returns the set of all named values in the given enum, in
/// declaration order.
pub inline fn values(comptime E: type) []const E {
    return comptime valuesFromFields(E, @typeInfo(E).@"enum".fields);
}

/// A safe alternative to @tagName() for non-exhaustive enums that doesn't
/// panic when `e` has no tagged value.
/// Returns the tag name for `e` or null if no tag exists.
pub fn tagName(comptime E: type, e: E) ?[:0]const u8 {
    const fields = @typeInfo(E).@"enum".fields;
    @setEvalBranchQuota(fields.len);
    return inline for (fields) |f| {
        if (@intFromEnum(e) == f.value) break f.name;
    } else null;
}

test tagName {
    const E = enum(u8) { a, b, _ };
    try testing.expect(tagName(E, .a) != null);
    try testing.expectEqualStrings("a", tagName(E, .a).?);
    try testing.expect(tagName(E, @as(E, @enumFromInt(42))) == null);
}

/// Determines the length of a direct-mapped enum array, indexed by
/// @intCast(usize, @intFromEnum(enum_value)).
/// If the enum is non-exhaustive, the resulting length will only be enough
/// to hold all explicit fields.
/// If the enum contains any fields with values that cannot be represented
/// by usize, a compile error is issued.  The max_unused_slots parameter limits
/// the total number of items which have no matching enum key (holes in the enum
/// numbering).  So for example, if an enum has values 1, 2, 5, and 6, max_unused_slots
/// must be at least 3, to allow unused slots 0, 3, and 4.
pub fn directEnumArrayLen(comptime E: type, comptime max_unused_slots: comptime_int) comptime_int {
    var max_value: comptime_int = -1;
    const max_usize: comptime_int = ~@as(usize, 0);
    const fields = @typeInfo(E).@"enum".fields;
    for (fields) |f| {
        if (f.value < 0) {
            @compileError("Cannot create a direct enum array for " ++ @typeName(E) ++ ", field ." ++ f.name ++ " has a negative value.");
        }
        if (f.value > max_value) {
            if (f.value > max_usize) {
                @compileError("Cannot create a direct enum array for " ++ @typeName(E) ++ ", field ." ++ f.name ++ " is larger than the max value of usize.");
            }
            max_value = f.value;
        }
    }

    const unused_slots = max_value + 1 - fields.len;
    if (unused_slots > max_unused_slots) {
        const unused_str = std.fmt.comptimePrint("{d}", .{unused_slots});
        const allowed_str = std.fmt.comptimePrint("{d}", .{max_unused_slots});
        @compileError("Cannot create a direct enum array for " ++ @typeName(E) ++ ". It would have " ++ unused_str ++ " unused slots, but only " ++ allowed_str ++ " are allowed.");
    }

    return max_value + 1;
}

/// Initializes an array of Data which can be indexed by
/// @intCast(usize, @intFromEnum(enum_value)).
/// If the enum is non-exhaustive, the resulting array will only be large enough
/// to hold all explicit fields.
/// If the enum contains any fields with values that cannot be represented
/// by usize, a compile error is issued.  The max_unused_slots parameter limits
/// the total number of items which have no matching enum key (holes in the enum
/// numbering).  So for example, if an enum has values 1, 2, 5, and 6, max_unused_slots
/// must be at least 3, to allow unused slots 0, 3, and 4.
/// The init_values parameter must be a struct with field names that match the enum values.
/// If the enum has multiple fields with the same value, the name of the first one must
/// be used.
pub fn directEnumArray(
    comptime E: type,
    comptime Data: type,
    comptime max_unused_slots: comptime_int,
    init_values: EnumFieldStruct(E, Data, null),
) [directEnumArrayLen(E, max_unused_slots)]Data {
    return directEnumArrayDefault(E, Data, null, max_unused_slots, init_values);
}

test directEnumArray {
    const E = enum(i4) { a = 4, b = 6, c = 2 };
    var runtime_false: bool = false;
    _ = &runtime_false;
    const array = directEnumArray(E, bool, 4, .{
        .a = true,
        .b = runtime_false,
        .c = true,
    });

    try testing.expectEqual([7]bool, @TypeOf(array));
    try testing.expectEqual(true, array[4]);
    try testing.expectEqual(false, array[6]);
    try testing.expectEqual(true, array[2]);
}

/// Initializes an array of Data which can be indexed by
/// @intCast(usize, @intFromEnum(enum_value)).  The enum must be exhaustive.
/// If the enum contains any fields with values that cannot be represented
/// by usize, a compile error is issued.  The max_unused_slots parameter limits
/// the total number of items which have no matching enum key (holes in the enum
/// numbering).  So for example, if an enum has values 1, 2, 5, and 6, max_unused_slots
/// must be at least 3, to allow unused slots 0, 3, and 4.
/// The init_values parameter must be a struct with field names that match the enum values.
/// If the enum has multiple fields with the same value, the name of the first one must
/// be used.
pub fn directEnumArrayDefault(
    comptime E: type,
    comptime Data: type,
    comptime default: ?Data,
    comptime max_unused_slots: comptime_int,
    init_values: EnumFieldStruct(E, Data, default),
) [directEnumArrayLen(E, max_unused_slots)]Data {
    const len = comptime directEnumArrayLen(E, max_unused_slots);
    var result: [len]Data = if (default) |d| [_]Data{d} ** len else undefined;
    inline for (@typeInfo(@TypeOf(init_values)).@"struct".fields) |f| {
        const enum_value = @field(E, f.name);
        const index = @as(usize, @intCast(@intFromEnum(enum_value)));
        result[index] = @field(init_values, f.name);
    }
    return result;
}

test directEnumArrayDefault {
    const E = enum(i4) { a = 4, b = 6, c = 2 };
    var runtime_false: bool = false;
    _ = &runtime_false;
    const array = directEnumArrayDefault(E, bool, false, 4, .{
        .a = true,
        .b = runtime_false,
    });

    try testing.expectEqual([7]bool, @TypeOf(array));
    try testing.expectEqual(true, array[4]);
    try testing.expectEqual(false, array[6]);
    try testing.expectEqual(false, array[2]);
}

test "directEnumArrayDefault slice" {
    const E = enum(i4) { a = 4, b = 6, c = 2 };
    var runtime_b = "b";
    _ = &runtime_b;
    const array = directEnumArrayDefault(E, []const u8, "default", 4, .{
        .a = "a",
        .b = runtime_b,
    });

    try testing.expectEqual([7][]const u8, @TypeOf(array));
    try testing.expectEqualSlices(u8, "a", array[4]);
    try testing.expectEqualSlices(u8, "b", array[6]);
    try testing.expectEqualSlices(u8, "default", array[2]);
}

test fromInt {
    const E1 = enum {
        A,
    };
    const E2 = enum {
        A,
        B,
    };
    const E3 = enum(i8) { A, _ };
    const E4 = enum(u8) { A };

    var zero: u8 = 0;
    var one: u16 = 1;
    _ = &zero;
    _ = &one;
    try testing.expect(fromInt(E1, zero).? == E1.A);
    try testing.expect(fromInt(E2, one).? == E2.B);
    try testing.expect(fromInt(E3, zero).? == E3.A);
    try testing.expect(fromInt(E3, 127).? == @as(E3, @enumFromInt(127)));
    try testing.expect(fromInt(E3, -128).? == @as(E3, @enumFromInt(-128)));
    try testing.expectEqual(null, fromInt(E1, one));
    try testing.expectEqual(null, fromInt(E3, 128));
    try testing.expectEqual(null, fromInt(E3, -129));

    // `fromInt` used to produce a compiler error instead of `null` if trying to convert an integer
    // that wasn't out of range, but also wasn't a valid value.
    try testing.expectEqual(null, fromInt(E4, 1));
}

/// A set of enum elements, backed by a bitfield.  If the enum
/// is exhaustive but not dense, a mapping will be constructed from enum values
/// to dense indices.  This type does no dynamic allocation and
/// can be copied by value.
pub fn EnumSet(comptime E: type) type {
    return struct {
        const Self = @This();

        /// The indexing rules for converting between keys and indices.
        pub const Indexer = EnumIndexer(E);
        /// The element type for this set.
        pub const Key = Indexer.Key;

        const BitSet = std.StaticBitSet(Indexer.count);

        /// The maximum number of items in this set.
        pub const len = Indexer.count;

        bits: BitSet = .empty,

        /// Initializes the set using a struct of bools
        pub fn init(init_values: EnumFieldStruct(E, bool, false)) Self {
            @setEvalBranchQuota(2 * @typeInfo(E).@"enum".fields.len);
            var result: Self = .{};
            if (@typeInfo(E).@"enum".is_exhaustive) {
                inline for (0..Self.len) |i| {
                    const key = comptime Indexer.keyForIndex(i);
                    const tag = @tagName(key);
                    if (@field(init_values, tag)) {
                        result.bits.set(i);
                    }
                }
            } else {
                inline for (std.meta.fields(E)) |field| {
                    const key = @field(E, field.name);
                    if (@field(init_values, field.name)) {
                        const i = comptime Indexer.indexOf(key);
                        result.bits.set(i);
                    }
                }
            }
            return result;
        }

        /// A set containing no keys.
        pub const empty: Self = .{ .bits = .empty };

        /// A set containing all possible keys.
        pub const full: Self = .{ .bits = .full };

        /// Deprecated: use `.empty`.
        /// Returns a set containing no keys.
        pub fn initEmpty() Self {
            return .empty;
        }

        /// Deprecated: use `.full`.
        /// Returns a set containing all possible keys.
        pub fn initFull() Self {
            return .full;
        }

        /// Returns a set containing multiple keys.
        pub fn initMany(keys: []const Key) Self {
            var set: Self = .empty;
            for (keys) |key| set.insert(key);
            return set;
        }

        /// Returns a set containing a single key.
        pub fn initOne(key: Key) Self {
            return initMany(&[_]Key{key});
        }

        /// Returns the number of keys in the set.
        pub fn count(self: Self) usize {
            return self.bits.count();
        }

        /// Checks if a key is in the set.
        pub fn contains(self: Self, key: Key) bool {
            return self.bits.isSet(Indexer.indexOf(key));
        }

        /// Puts a key in the set.
        pub fn insert(self: *Self, key: Key) void {
            self.bits.set(Indexer.indexOf(key));
        }

        /// Removes a key from the set.
        pub fn remove(self: *Self, key: Key) void {
            self.bits.unset(Indexer.indexOf(key));
        }

        /// Changes the presence of a key in the set to match the passed bool.
        pub fn setPresent(self: *Self, key: Key, present: bool) void {
            self.bits.setValue(Indexer.indexOf(key), present);
        }

        /// Toggles the presence of a key in the set.  If the key is in
        /// the set, removes it.  Otherwise adds it.
        pub fn toggle(self: *Self, key: Key) void {
            self.bits.toggle(Indexer.indexOf(key));
        }

        /// Toggles the presence of all keys in the passed set.
        pub fn toggleSet(self: *Self, other: Self) void {
            self.bits.toggleSet(other.bits);
        }

        /// Toggles all possible keys in the set.
        pub fn toggleAll(self: *Self) void {
            self.bits.toggleAll();
        }

        /// Adds all keys in the passed set to this set.
        pub fn setUnion(self: *Self, other: Self) void {
            self.bits.setUnion(other.bits);
        }

        /// Removes all keys which are not in the passed set.
        pub fn setIntersection(self: *Self, other: Self) void {
            self.bits.setIntersection(other.bits);
        }

        /// Returns true iff both sets have the same keys.
        pub fn eql(self: Self, other: Self) bool {
            return self.bits.eql(other.bits);
        }

        /// Returns true iff all the keys in this set are
        /// in the other set. The other set may have keys
        /// not found in this set.
        pub fn subsetOf(self: Self, other: Self) bool {
            return self.bits.subsetOf(other.bits);
        }

        /// Returns true iff this set contains all the keys
        /// in the other set. This set may have keys not
        /// found in the other set.
        pub fn supersetOf(self: Self, other: Self) bool {
            return self.bits.supersetOf(other.bits);
        }

        /// Returns a set with all the keys not in this set.
        pub fn complement(self: Self) Self {
            return .{ .bits = self.bits.complement() };
        }

        /// Returns a set with keys that are in either this
        /// set or the other set.
        pub fn unionWith(self: Self, other: Self) Self {
            return .{ .bits = self.bits.unionWith(other.bits) };
        }

        /// Returns a set with keys that are in both this
        /// set and the other set.
        pub fn intersectWith(self: Self, other: Self) Self {
            return .{ .bits = self.bits.intersectWith(other.bits) };
        }

        /// Returns a set with keys that are in either this
        /// set or the other set, but not both.
        pub fn xorWith(self: Self, other: Self) Self {
            return .{ .bits = self.bits.xorWith(other.bits) };
        }

        /// Returns a set with keys that are in this set
        /// except for keys in the other set.
        pub fn differenceWith(self: Self, other: Self) Self {
            return .{ .bits = self.bits.differenceWith(other.bits) };
        }

        /// Returns an iterator over this set, which iterates in
        /// index order.  Modifications to the set during iteration
        /// may or may not be observed by the iterator, but will
        /// not invalidate it.
        pub fn iterator(self: *const Self) Iterator {
            return .{ .inner = self.bits.iterator(.{}) };
        }

        pub const Iterator = struct {
            inner: BitSet.Iterator(.{}),

            pub fn next(self: *Iterator) ?Key {
                return if (self.inner.next()) |index|
                    Indexer.keyForIndex(index)
                else
                    null;
            }
        };
    };
}

/// A map keyed by an enum, backed by a bitfield and a dense array.
/// If the enum is exhaustive but not dense, a mapping will be constructed from
/// enum values to dense indices.  This type does no dynamic
/// allocation and can be copied by value.
pub fn EnumMap(comptime E: type, comptime V: type) type {
    return struct {
        const Self = @This();

        /// The index mapping for this map
        pub const Indexer = EnumIndexer(E);
        /// The key type used to index this map
        pub const Key = Indexer.Key;
        /// The value type stored in this map
        pub const Value = V;
        /// The number of possible keys in the map
        pub const len = Indexer.count;

        const BitSet = std.StaticBitSet(Indexer.count);

        /// Bits determining whether items are in the map
        bits: BitSet = .empty,
        /// Values of items in the map.  If the associated
        /// bit is zero, the value is undefined.
        values: [Indexer.count]Value = undefined,

        /// Initializes the map using a sparse struct of optionals
        pub fn init(init_values: EnumFieldStruct(E, ?Value, @as(?Value, null))) Self {
            @setEvalBranchQuota(2 * @typeInfo(E).@"enum".fields.len);
            var result: Self = .{};
            if (@typeInfo(E).@"enum".is_exhaustive) {
                inline for (0..Self.len) |i| {
                    const key = comptime Indexer.keyForIndex(i);
                    const tag = @tagName(key);
                    if (@field(init_values, tag)) |*v| {
                        result.bits.set(i);
                        result.values[i] = v.*;
                    }
                }
            } else {
                inline for (std.meta.fields(E)) |field| {
                    const key = @field(E, field.name);
                    if (@field(init_values, field.name)) |*v| {
                        const i = comptime Indexer.indexOf(key);
                        result.bits.set(i);
                        result.values[i] = v.*;
                    }
                }
            }
            return result;
        }

        /// Initializes a full mapping with all keys set to value.
        /// Consider using EnumArray instead if the map will remain full.
        pub fn initFull(value: Value) Self {
            var result: Self = .{
                .bits = .full,
                .values = undefined,
            };
            @memset(&result.values, value);
            return result;
        }

        /// Initializes a full mapping with supplied values.
        /// Consider using EnumArray instead if the map will remain full.
        pub fn initFullWith(init_values: EnumFieldStruct(E, Value, null)) Self {
            return initFullWithDefault(null, init_values);
        }

        /// Initializes a full mapping with a provided default.
        /// Consider using EnumArray instead if the map will remain full.
        pub fn initFullWithDefault(comptime default: ?Value, init_values: EnumFieldStruct(E, Value, default)) Self {
            @setEvalBranchQuota(2 * @typeInfo(E).@"enum".fields.len);
            var result: Self = .{
                .bits = .full,
                .values = undefined,
            };
            inline for (0..Self.len) |i| {
                const key = comptime Indexer.keyForIndex(i);
                const tag = @tagName(key);
                result.values[i] = @field(init_values, tag);
            }
            return result;
        }

        /// The number of items in the map.
        pub fn count(self: *const Self) usize {
            return self.bits.count();
        }

        /// Checks if the map contains an item.
        pub fn contains(self: *const Self, key: Key) bool {
            return self.bits.isSet(Indexer.indexOf(key));
        }

        /// Gets the value associated with a key.
        /// If the key is not in the map, returns null.
        pub fn get(self: *const Self, key: Key) ?Value {
            const index = Indexer.indexOf(key);
            return if (self.bits.isSet(index)) self.values[index] else null;
        }

        /// Gets the value associated with a key, which must
        /// exist in the map.
        pub fn getAssertContains(self: *const Self, key: Key) Value {
            const index = Indexer.indexOf(key);
            assert(self.bits.isSet(index));
            return self.values[index];
        }

        /// Gets the address of the value associated with a key.
        /// If the key is not in the map, returns null.
        pub fn getPtr(self: *Self, key: Key) ?*Value {
            const index = Indexer.indexOf(key);
            return if (self.bits.isSet(index)) &self.values[index] else null;
        }

        /// Gets the address of the const value associated with a key.
        /// If the key is not in the map, returns null.
        pub fn getPtrConst(self: *const Self, key: Key) ?*const Value {
            const index = Indexer.indexOf(key);
            return if (self.bits.isSet(index)) &self.values[index] else null;
        }

        /// Gets the address of the value associated with a key.
        /// The key must be present in the map.
        pub fn getPtrAssertContains(self: *Self, key: Key) *Value {
            const index = Indexer.indexOf(key);
            assert(self.bits.isSet(index));
            return &self.values[index];
        }

        /// Gets the address of the const value associated with a key.
        /// The key must be present in the map.
        pub fn getPtrConstAssertContains(self: *const Self, key: Key) *const Value {
            const index = Indexer.indexOf(key);
            assert(self.bits.isSet(index));
            return &self.values[index];
        }

        /// Adds the key to the map with the supplied value.
        /// If the key is already in the map, overwrites the value.
        pub fn put(self: *Self, key: Key, value: Value) void {
            const index = Indexer.indexOf(key);
            self.bits.set(index);
            self.values[index] = value;
        }

        /// Adds the key to the map with an undefined value.
        /// If the key is already in the map, the value becomes undefined.
        /// A pointer to the value is returned, which should be
        /// used to initialize the value.
        pub fn putUninitialized(self: *Self, key: Key) *Value {
            const index = Indexer.indexOf(key);
            self.bits.set(index);
            self.values[index] = undefined;
            return &self.values[index];
        }

        /// Sets the value associated with the key in the map,
        /// and returns the old value.  If the key was not in
        /// the map, returns null.
        pub fn fetchPut(self: *Self, key: Key, value: Value) ?Value {
            const index = Indexer.indexOf(key);
            const result: ?Value = if (self.bits.isSet(index)) self.values[index] else null;
            self.bits.set(index);
            self.values[index] = value;
            return result;
        }

        /// Removes a key from the map.  If the key was not in the map,
        /// does nothing.
        pub fn remove(self: *Self, key: Key) void {
            const index = Indexer.indexOf(key);
            self.bits.unset(index);
            self.values[index] = undefined;
        }

        /// Removes a key from the map, and returns the old value.
        /// If the key was not in the map, returns null.
        pub fn fetchRemove(self: *Self, key: Key) ?Value {
            const index = Indexer.indexOf(key);
            const result: ?Value = if (self.bits.isSet(index)) self.values[index] else null;
            self.bits.unset(index);
            self.values[index] = undefined;
            return result;
        }

        /// Returns an iterator over the map, which visits items in index order.
        /// Modifications to the underlying map may or may not be observed by
        /// the iterator, but will not invalidate it.
        pub fn iterator(self: *Self) Iterator {
            return .{
                .inner = self.bits.iterator(.{}),
                .values = &self.values,
            };
        }

        /// An entry in the map.
        pub const Entry = struct {
            /// The key associated with this entry.
            /// Modifying this key will not change the map.
            key: Key,

            /// A pointer to the value in the map associated
            /// with this key.  Modifications through this
            /// pointer will modify the underlying data.
            value: *Value,
        };

        pub const Iterator = struct {
            inner: BitSet.Iterator(.{}),
            values: *[Indexer.count]Value,

            pub fn next(self: *Iterator) ?Entry {
                return if (self.inner.next()) |index|
                    Entry{
                        .key = Indexer.keyForIndex(index),
                        .value = &self.values[index],
                    }
                else
                    null;
            }
        };
    };
}

test EnumMap {
    const Ball = enum { red, green, blue };

    const some = EnumMap(Ball, u8).init(.{
        .green = 0xff,
        .blue = 0x80,
    });
    try testing.expectEqual(2, some.count());
    try testing.expectEqual(null, some.get(.red));
    try testing.expectEqual(0xff, some.get(.green));
    try testing.expectEqual(0x80, some.get(.blue));
}

/// A multiset of enum elements up to a count of usize. Backed
/// by an EnumArray. This type does no dynamic allocation and can
/// be copied by value.
pub fn EnumMultiset(comptime E: type) type {
    return BoundedEnumMultiset(E, usize);
}

/// A multiset of enum elements up to CountSize. Backed by an
/// EnumArray. This type does no dynamic allocation and can be
/// copied by value.
pub fn BoundedEnumMultiset(comptime E: type, comptime CountSize: type) type {
    return struct {
        const Self = @This();

        counts: EnumArray(E, CountSize),

        /// Initializes the multiset using a struct of counts.
        pub fn init(init_counts: EnumFieldStruct(E, CountSize, 0)) Self {
            @setEvalBranchQuota(2 * @typeInfo(E).@"enum".fields.len);
            var self = initWithCount(0);
            inline for (@typeInfo(E).@"enum".fields) |field| {
                const c = @field(init_counts, field.name);
                const key = @as(E, @enumFromInt(field.value));
                self.counts.set(key, c);
            }
            return self;
        }

        /// A multiset with a count of zero.
        pub const empty: Self = .initWithCount(0);

        /// Initializes the multiset with all keys at the
        /// same count.
        pub fn initWithCount(comptime c: CountSize) Self {
            return .{
                .counts = .initDefault(c, .{}),
            };
        }

        /// Returns the total number of key counts in the multiset.
        pub fn count(self: Self) usize {
            var sum: usize = 0;
            for (self.counts.values) |c| {
                sum += c;
            }
            return sum;
        }

        /// Checks if at least one key in multiset.
        pub fn contains(self: Self, key: E) bool {
            return self.counts.get(key) > 0;
        }

        /// Removes all instance of a key from multiset. Same as
        /// setCount(key, 0).
        pub fn removeAll(self: *Self, key: E) void {
            return self.counts.set(key, 0);
        }

        /// Increases the key count by given amount. Caller asserts
        /// operation will not overflow.
        pub fn addAssertSafe(self: *Self, key: E, c: CountSize) void {
            self.counts.getPtr(key).* += c;
        }

        /// Increases the key count by given amount.
        pub fn add(self: *Self, key: E, c: CountSize) error{Overflow}!void {
            self.counts.set(key, try std.math.add(CountSize, self.counts.get(key), c));
        }

        /// Decreases the key count by given amount. If amount is
        /// greater than the number of keys in multset, then key count
        /// will be set to zero.
        pub fn remove(self: *Self, key: E, c: CountSize) void {
            self.counts.getPtr(key).* -= @min(self.getCount(key), c);
        }

        /// Returns the count for a key.
        pub fn getCount(self: Self, key: E) CountSize {
            return self.counts.get(key);
        }

        /// Set the count for a key.
        pub fn setCount(self: *Self, key: E, c: CountSize) void {
            self.counts.set(key, c);
        }

        /// Increases the all key counts by given multiset. Caller
        /// asserts operation will not overflow any key.
        pub fn addSetAssertSafe(self: *Self, other: Self) void {
            inline for (@typeInfo(E).@"enum".fields) |field| {
                const key = @as(E, @enumFromInt(field.value));
                self.addAssertSafe(key, other.getCount(key));
            }
        }

        /// Increases the all key counts by given multiset.
        pub fn addSet(self: *Self, other: Self) error{Overflow}!void {
            inline for (@typeInfo(E).@"enum".fields) |field| {
                const key = @as(E, @enumFromInt(field.value));
                try self.add(key, other.getCount(key));
            }
        }

        /// Decreases the all key counts by given multiset. If
        /// the given multiset has more key counts than this,
        /// then that key will have a key count of zero.
        pub fn removeSet(self: *Self, other: Self) void {
            inline for (@typeInfo(E).@"enum".fields) |field| {
                const key = @as(E, @enumFromInt(field.value));
                self.remove(key, other.getCount(key));
            }
        }

        /// Returns true iff all key counts are the same as
        /// given multiset.
        pub fn eql(self: Self, other: Self) bool {
            inline for (@typeInfo(E).@"enum".fields) |field| {
                const key = @as(E, @enumFromInt(field.value));
                if (self.getCount(key) != other.getCount(key)) {
                    return false;
                }
            }
            return true;
        }

        /// Returns true iff all key counts less than or
        /// equal to the given multiset.
        pub fn subsetOf(self: Self, other: Self) bool {
            inline for (@typeInfo(E).@"enum".fields) |field| {
                const key = @as(E, @enumFromInt(field.value));
                if (self.getCount(key) > other.getCount(key)) {
                    return false;
                }
            }
            return true;
        }

        /// Returns true iff all key counts greater than or
        /// equal to the given multiset.
        pub fn supersetOf(self: Self, other: Self) bool {
            inline for (@typeInfo(E).@"enum".fields) |field| {
                const key = @as(E, @enumFromInt(field.value));
                if (self.getCount(key) < other.getCount(key)) {
                    return false;
                }
            }
            return true;
        }

        /// Returns a multiset with the total key count of this
        /// multiset and the other multiset. Caller asserts
        /// operation will not overflow any key.
        pub fn plusAssertSafe(self: Self, other: Self) Self {
            var result = self;
            result.addSetAssertSafe(other);
            return result;
        }

        /// Returns a multiset with the total key count of this
        /// multiset and the other multiset.
        pub fn plus(self: Self, other: Self) error{Overflow}!Self {
            var result = self;
            try result.addSet(other);
            return result;
        }

        /// Returns a multiset with the key count of this
        /// multiset minus the corresponding key count in the
        /// other multiset. If the other multiset contains
        /// more key count than this set, that key will have
        /// a count of zero.
        pub fn minus(self: Self, other: Self) Self {
            var result = self;
            result.removeSet(other);
            return result;
        }

        pub const Entry = EnumArray(E, CountSize).Entry;
        pub const Iterator = EnumArray(E, CountSize).Iterator;

        /// Returns an iterator over this multiset. Keys with zero
        /// counts are included. Modifications to the set during
        /// iteration may or may not be observed by the iterator,
        /// but will not invalidate it.
        pub fn iterator(self: *Self) Iterator {
            return self.counts.iterator();
        }
    };
}

test EnumMultiset {
    const Ball = enum { red, green, blue };

    const empty = EnumMultiset(Ball).empty;
    const r0_g1_b2 = EnumMultiset(Ball).init(.{
        .red = 0,
        .green = 1,
        .blue = 2,
    });
    const ten_of_each = EnumMultiset(Ball).initWithCount(10);

    try testing.expectEqual(empty.count(), 0);
    try testing.expectEqual(r0_g1_b2.count(), 3);
    try testing.expectEqual(ten_of_each.count(), 30);

    try testing.expect(!empty.contains(.red));
    try testing.expect(!empty.contains(.green));
    try testing.expect(!empty.contains(.blue));

    try testing.expect(!r0_g1_b2.contains(.red));
    try testing.expect(r0_g1_b2.contains(.green));
    try testing.expect(r0_g1_b2.contains(.blue));

    try testing.expect(ten_of_each.contains(.red));
    try testing.expect(ten_of_each.contains(.green));
    try testing.expect(ten_of_each.contains(.blue));

    {
        var copy = ten_of_each;
        copy.removeAll(.red);
        try testing.expect(!copy.contains(.red));

        // removeAll second time does nothing
        copy.removeAll(.red);
        try testing.expect(!copy.contains(.red));
    }

    {
        var copy = ten_of_each;
        copy.addAssertSafe(.red, 6);
        try testing.expectEqual(copy.getCount(.red), 16);
    }

    {
        var copy = ten_of_each;
        try copy.add(.red, 6);
        try testing.expectEqual(copy.getCount(.red), 16);

        try testing.expectError(error.Overflow, copy.add(.red, std.math.maxInt(usize)));
    }

    {
        var copy = ten_of_each;
        copy.remove(.red, 4);
        try testing.expectEqual(copy.getCount(.red), 6);

        // subtracting more it contains does not underflow
        copy.remove(.green, 14);
        try testing.expectEqual(copy.getCount(.green), 0);
    }

    try testing.expectEqual(empty.getCount(.green), 0);
    try testing.expectEqual(r0_g1_b2.getCount(.green), 1);
    try testing.expectEqual(ten_of_each.getCount(.green), 10);

    {
        var copy = empty;
        copy.setCount(.red, 6);
        try testing.expectEqual(copy.getCount(.red), 6);
    }

    {
        var copy = r0_g1_b2;
        copy.addSetAssertSafe(ten_of_each);
        try testing.expectEqual(copy.getCount(.red), 10);
        try testing.expectEqual(copy.getCount(.green), 11);
        try testing.expectEqual(copy.getCount(.blue), 12);
    }

    {
        var copy = r0_g1_b2;
        try copy.addSet(ten_of_each);
        try testing.expectEqual(copy.getCount(.red), 10);
        try testing.expectEqual(copy.getCount(.green), 11);
        try testing.expectEqual(copy.getCount(.blue), 12);

        const full = EnumMultiset(Ball).initWithCount(std.math.maxInt(usize));
        try testing.expectError(error.Overflow, copy.addSet(full));
    }

    {
        var copy = ten_of_each;
        copy.removeSet(r0_g1_b2);
        try testing.expectEqual(copy.getCount(.red), 10);
        try testing.expectEqual(copy.getCount(.green), 9);
        try testing.expectEqual(copy.getCount(.blue), 8);

        copy.removeSet(ten_of_each);
        try testing.expectEqual(copy.getCount(.red), 0);
        try testing.expectEqual(copy.getCount(.green), 0);
        try testing.expectEqual(copy.getCount(.blue), 0);
    }

    try testing.expect(empty.eql(empty));
    try testing.expect(r0_g1_b2.eql(r0_g1_b2));
    try testing.expect(ten_of_each.eql(ten_of_each));
    try testing.expect(!empty.eql(r0_g1_b2));
    try testing.expect(!r0_g1_b2.eql(ten_of_each));
    try testing.expect(!ten_of_each.eql(empty));

    try testing.expect(empty.subsetOf(empty));
    try testing.expect(r0_g1_b2.subsetOf(r0_g1_b2));
    try testing.expect(empty.subsetOf(r0_g1_b2));
    try testing.expect(r0_g1_b2.subsetOf(ten_of_each));
    try testing.expect(!ten_of_each.subsetOf(r0_g1_b2));
    try testing.expect(!r0_g1_b2.subsetOf(empty));

    try testing.expect(empty.supersetOf(empty));
    try testing.expect(r0_g1_b2.supersetOf(r0_g1_b2));
    try testing.expect(r0_g1_b2.supersetOf(empty));
    try testing.expect(ten_of_each.supersetOf(r0_g1_b2));
    try testing.expect(!r0_g1_b2.supersetOf(ten_of_each));
    try testing.expect(!empty.supersetOf(r0_g1_b2));

    {
        // with multisets it could be the case where two
        // multisets are neither subset nor superset of each
        // other.

        const r10 = EnumMultiset(Ball).init(.{
            .red = 10,
        });
        const b10 = EnumMultiset(Ball).init(.{
            .blue = 10,
        });

        try testing.expect(!r10.subsetOf(b10));
        try testing.expect(!b10.subsetOf(r10));
        try testing.expect(!r10.supersetOf(b10));
        try testing.expect(!b10.supersetOf(r10));
    }

    {
        const result = r0_g1_b2.plusAssertSafe(ten_of_each);
        try testing.expectEqual(result.getCount(.red), 10);
        try testing.expectEqual(result.getCount(.green), 11);
        try testing.expectEqual(result.getCount(.blue), 12);
    }

    {
        const result = try r0_g1_b2.plus(ten_of_each);
        try testing.expectEqual(result.getCount(.red), 10);
        try testing.expectEqual(result.getCount(.green), 11);
        try testing.expectEqual(result.getCount(.blue), 12);

        const full = EnumMultiset(Ball).initWithCount(std.math.maxInt(usize));
        try testing.expectError(error.Overflow, result.plus(full));
    }

    {
        const result = ten_of_each.minus(r0_g1_b2);
        try testing.expectEqual(result.getCount(.red), 10);
        try testing.expectEqual(result.getCount(.green), 9);
        try testing.expectEqual(result.getCount(.blue), 8);
    }

    {
        const result = ten_of_each.minus(r0_g1_b2).minus(ten_of_each);
        try testing.expectEqual(result.getCount(.red), 0);
        try testing.expectEqual(result.getCount(.green), 0);
        try testing.expectEqual(result.getCount(.blue), 0);
    }

    {
        var copy = empty;
        var it = copy.iterator();
        var entry = it.next().?;
        try testing.expectEqual(entry.key, .red);
        try testing.expectEqual(entry.value.*, 0);
        entry = it.next().?;
        try testing.expectEqual(entry.key, .green);
        try testing.expectEqual(entry.value.*, 0);
        entry = it.next().?;
        try testing.expectEqual(entry.key, .blue);
        try testing.expectEqual(entry.value.*, 0);
        try testing.expectEqual(it.next(), null);
    }

    {
        var copy = r0_g1_b2;
        var it = copy.iterator();
        var entry = it.next().?;
        try testing.expectEqual(entry.key, .red);
        try testing.expectEqual(entry.value.*, 0);
        entry = it.next().?;
        try testing.expectEqual(entry.key, .green);
        try testing.expectEqual(entry.value.*, 1);
        entry = it.next().?;
        try testing.expectEqual(entry.key, .blue);
        try testing.expectEqual(entry.value.*, 2);
        try testing.expectEqual(it.next(), null);
    }
}

/// An array keyed by an enum, backed by a dense array.
/// If the enum is not dense, a mapping will be constructed from
/// enum values to dense indices.  This type does no dynamic
/// allocation and can be copied by value.
pub fn EnumArray(comptime E: type, comptime V: type) type {
    return struct {
        const Self = @This();

        /// The index mapping for this map
        pub const Indexer = EnumIndexer(E);
        /// The key type used to index this map
        pub const Key = Indexer.Key;
        /// The value type stored in this map
        pub const Value = V;
        /// The number of possible keys in the map
        pub const len = Indexer.count;

        values: [Indexer.count]Value,

        pub fn init(init_values: EnumFieldStruct(E, Value, null)) Self {
            return initDefault(null, init_values);
        }

        /// Initializes values in the enum array, with the specified default.
        pub fn initDefault(comptime default: ?Value, init_values: EnumFieldStruct(E, Value, default)) Self {
            @setEvalBranchQuota(2 * @typeInfo(E).@"enum".fields.len);
            var result: Self = .{ .values = undefined };
            inline for (0..Self.len) |i| {
                const key = comptime Indexer.keyForIndex(i);
                const tag = @tagName(key);
                result.values[i] = @field(init_values, tag);
            }
            return result;
        }

        pub fn initUndefined() Self {
            return Self{ .values = undefined };
        }

        pub fn initFill(v: Value) Self {
            var self: Self = undefined;
            @memset(&self.values, v);
            return self;
        }

        /// Returns the value in the array associated with a key.
        pub fn get(self: Self, key: Key) Value {
            return self.values[Indexer.indexOf(key)];
        }

        /// Returns a pointer to the slot in the array associated with a key.
        pub fn getPtr(self: *Self, key: Key) *Value {
            return &self.values[Indexer.indexOf(key)];
        }

        /// Returns a const pointer to the slot in the array associated with a key.
        pub fn getPtrConst(self: *const Self, key: Key) *const Value {
            return &self.values[Indexer.indexOf(key)];
        }

        /// Sets the value in the slot associated with a key.
        pub fn set(self: *Self, key: Key, value: Value) void {
            self.values[Indexer.indexOf(key)] = value;
        }

        /// Iterates over the items in the array, in index order.
        pub fn iterator(self: *Self) Iterator {
            return .{
                .values = &self.values,
            };
        }

        /// An entry in the array.
        pub const Entry = struct {
            /// The key associated with this entry.
            /// Modifying this key will not change the array.
            key: Key,

            /// A pointer to the value in the array associated
            /// with this key.  Modifications through this
            /// pointer will modify the underlying data.
            value: *Value,
        };

        pub const Iterator = struct {
            index: usize = 0,
            values: *[Indexer.count]Value,

            pub fn next(self: *Iterator) ?Entry {
                const index = self.index;
                if (index < Indexer.count) {
                    self.index += 1;
                    return Entry{
                        .key = Indexer.keyForIndex(index),
                        .value = &self.values[index],
                    };
                }
                return null;
            }
        };
    };
}

test "pure EnumSet fns" {
    const Suit = enum { spades, hearts, clubs, diamonds };

    const empty = EnumSet(Suit).empty;
    const full = EnumSet(Suit).full;
    const black = EnumSet(Suit).initMany(&[_]Suit{ .spades, .clubs });
    const red = EnumSet(Suit).initMany(&[_]Suit{ .hearts, .diamonds });

    try testing.expect(empty.eql(empty));
    try testing.expect(full.eql(full));
    try testing.expect(!empty.eql(full));
    try testing.expect(!full.eql(empty));
    try testing.expect(!empty.eql(black));
    try testing.expect(!full.eql(red));
    try testing.expect(!red.eql(empty));
    try testing.expect(!black.eql(full));

    try testing.expect(empty.subsetOf(empty));
    try testing.expect(empty.subsetOf(full));
    try testing.expect(full.subsetOf(full));
    try testing.expect(!black.subsetOf(red));
    try testing.expect(!red.subsetOf(black));

    try testing.expect(full.supersetOf(full));
    try testing.expect(full.supersetOf(empty));
    try testing.expect(empty.supersetOf(empty));
    try testing.expect(!black.supersetOf(red));
    try testing.expect(!red.supersetOf(black));

    try testing.expect(empty.complement().eql(full));
    try testing.expect(full.complement().eql(empty));
    try testing.expect(black.complement().eql(red));
    try testing.expect(red.complement().eql(black));

    try testing.expect(empty.unionWith(empty).eql(empty));
    try testing.expect(empty.unionWith(full).eql(full));
    try testing.expect(full.unionWith(full).eql(full));
    try testing.expect(full.unionWith(empty).eql(full));
    try testing.expect(black.unionWith(red).eql(full));
    try testing.expect(red.unionWith(black).eql(full));

    try testing.expect(empty.intersectWith(empty).eql(empty));
    try testing.expect(empty.intersectWith(full).eql(empty));
    try testing.expect(full.intersectWith(full).eql(full));
    try testing.expect(full.intersectWith(empty).eql(empty));
    try testing.expect(black.intersectWith(red).eql(empty));
    try testing.expect(red.intersectWith(black).eql(empty));

    try testing.expect(empty.xorWith(empty).eql(empty));
    try testing.expect(empty.xorWith(full).eql(full));
    try testing.expect(full.xorWith(full).eql(empty));
    try testing.expect(full.xorWith(empty).eql(full));
    try testing.expect(black.xorWith(red).eql(full));
    try testing.expect(red.xorWith(black).eql(full));

    try testing.expect(empty.differenceWith(empty).eql(empty));
    try testing.expect(empty.differenceWith(full).eql(empty));
    try testing.expect(full.differenceWith(full).eql(empty));
    try testing.expect(full.differenceWith(empty).eql(full));
    try testing.expect(full.differenceWith(red).eql(black));
    try testing.expect(full.differenceWith(black).eql(red));
}

test "EnumSet empty" {
    const E = enum {};
    const empty = EnumSet(E).empty;
    const full = EnumSet(E).full;

    try std.testing.expect(empty.eql(full));
    try std.testing.expect(empty.complement().eql(full));
    try std.testing.expect(empty.complement().eql(full.complement()));
    try std.testing.expect(empty.eql(full.complement()));
}

test "EnumSet const iterator" {
    const Direction = enum { up, down, left, right };
    const diag_move = init: {
        var move = EnumSet(Direction).empty;
        move.insert(.right);
        move.insert(.up);
        break :init move;
    };

    var result = EnumSet(Direction).empty;
    var it = diag_move.iterator();
    while (it.next()) |dir| {
        result.insert(dir);
    }

    try testing.expect(result.eql(diag_move));
}

test "EnumSet non-exhaustive" {
    const BitIndices = enum(u4) {
        a = 0,
        b = 1,
        c = 4,
        _,
    };
    const BitField = EnumSet(BitIndices);

    var flags = BitField.init(.{ .a = true, .b = true });
    flags.insert(.c);
    flags.remove(.a);
    try testing.expect(!flags.contains(.a));
    try testing.expect(flags.contains(.b));
    try testing.expect(flags.contains(.c));
}

pub fn EnumIndexer(comptime E: type) type {
    // n log n for `std.mem.sortUnstable` call below.
    const fields_len = @typeInfo(E).@"enum".fields.len;
    @setEvalBranchQuota(3 * fields_len * std.math.log2(@max(fields_len, 1)) + eval_branch_quota_cushion);

    if (!@typeInfo(E).@"enum".is_exhaustive) {
        const BackingInt = @typeInfo(E).@"enum".tag_type;
        if (@bitSizeOf(BackingInt) > @bitSizeOf(usize))
            @compileError("Cannot create an enum indexer for a given non-exhaustive enum, tag_type is larger than usize.");

        return struct {
            pub const Key: type = E;

            const backing_int_sign = @typeInfo(BackingInt).int.signedness;
            const min_value = std.math.minInt(BackingInt);
            const max_value = std.math.maxInt(BackingInt);

            const RangeType = std.meta.Int(.unsigned, @bitSizeOf(BackingInt));
            pub const count: comptime_int = std.math.maxInt(RangeType) + 1;

            pub fn indexOf(e: E) usize {
                if (backing_int_sign == .unsigned)
                    return @intFromEnum(e);

                return if (@intFromEnum(e) < 0)
                    @intCast(@intFromEnum(e) - min_value)
                else
                    @as(RangeType, -min_value) + @as(RangeType, @intCast(@intFromEnum(e)));
            }
            pub fn keyFo
```
