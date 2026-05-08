```
its statically link edited address).

    /// offset to local relocation entries
    locreloff: u32 = 0,

    /// number of local relocation entries
    nlocrel: u32 = 0,
};

/// The linkedit_data_command contains the offsets and sizes of a blob
/// of data in the __LINKEDIT segment.
pub const linkedit_data_command = extern struct {
    /// LC_CODE_SIGNATURE, LC_SEGMENT_SPLIT_INFO, LC_FUNCTION_STARTS, LC_DATA_IN_CODE, LC_DYLIB_CODE_SIGN_DRS or LC_LINKER_OPTIMIZATION_HINT.
    cmd: LC,

    /// sizeof(struct linkedit_data_command)
    cmdsize: u32 = @sizeOf(linkedit_data_command),

    /// file offset of data in __LINKEDIT segment
    dataoff: u32 = 0,

    /// file size of data in __LINKEDIT segment
    datasize: u32 = 0,
};

/// The dyld_info_command contains the file offsets and sizes of
/// the new compressed form of the information dyld needs to
/// load the image.  This information is used by dyld on Mac OS X
/// 10.6 and later.  All information pointed to by this command
/// is encoded using byte streams, so no endian swapping is needed
/// to interpret it.
pub const dyld_info_command = extern struct {
    /// LC_DYLD_INFO or LC_DYLD_INFO_ONLY
    cmd: LC = .DYLD_INFO_ONLY,

    /// sizeof(struct dyld_info_command)
    cmdsize: u32 = @sizeOf(dyld_info_command),

    // Dyld rebases an image whenever dyld loads it at an address different
    // from its preferred address.  The rebase information is a stream
    // of byte sized opcodes whose symbolic names start with REBASE_OPCODE_.
    // Conceptually the rebase information is a table of tuples:
    //    <seg-index, seg-offset, type>
    // The opcodes are a compressed way to encode the table by only
    // encoding when a column changes.  In addition simple patterns
    // like "every n'th offset for m times" can be encoded in a few
    // bytes.

    /// file offset to rebase info
    rebase_off: u32 = 0,

    /// size of rebase info
    rebase_size: u32 = 0,

    // Dyld binds an image during the loading process, if the image
    // requires any pointers to be initialized to symbols in other images.
    // The bind information is a stream of byte sized
    // opcodes whose symbolic names start with BIND_OPCODE_.
    // Conceptually the bind information is a table of tuples:
    //    <seg-index, seg-offset, type, symbol-library-ordinal, symbol-name, addend>
    // The opcodes are a compressed way to encode the table by only
    // encoding when a column changes.  In addition simple patterns
    // like for runs of pointers initialized to the same value can be
    // encoded in a few bytes.

    /// file offset to binding info
    bind_off: u32 = 0,

    /// size of binding info
    bind_size: u32 = 0,

    // Some C++ programs require dyld to unique symbols so that all
    // images in the process use the same copy of some code/data.
    // This step is done after binding. The content of the weak_bind
    // info is an opcode stream like the bind_info.  But it is sorted
    // alphabetically by symbol name.  This enable dyld to walk
    // all images with weak binding information in order and look
    // for collisions.  If there are no collisions, dyld does
    // no updating.  That means that some fixups are also encoded
    // in the bind_info.  For instance, all calls to "operator new"
    // are first bound to libstdc++.dylib using the information
    // in bind_info.  Then if some image overrides operator new
    // that is detected when the weak_bind information is processed
    // and the call to operator new is then rebound.

    /// file offset to weak binding info
    weak_bind_off: u32 = 0,

    /// size of weak binding info
    weak_bind_size: u32 = 0,

    // Some uses of external symbols do not need to be bound immediately.
    // Instead they can be lazily bound on first use.  The lazy_bind
    // are contains a stream of BIND opcodes to bind all lazy symbols.
    // Normal use is that dyld ignores the lazy_bind section when
    // loading an image.  Instead the static linker arranged for the
    // lazy pointer to initially point to a helper function which
    // pushes the offset into the lazy_bind area for the symbol
    // needing to be bound, then jumps to dyld which simply adds
    // the offset to lazy_bind_off to get the information on what
    // to bind.

    /// file offset to lazy binding info
    lazy_bind_off: u32 = 0,

    /// size of lazy binding info
    lazy_bind_size: u32 = 0,

    // The symbols exported by a dylib are encoded in a trie.  This
    // is a compact representation that factors out common prefixes.
    // It also reduces LINKEDIT pages in RAM because it encodes all
    // information (name, address, flags) in one small, contiguous range.
    // The export area is a stream of nodes.  The first node sequentially
    // is the start node for the trie.
    //
    // Nodes for a symbol start with a uleb128 that is the length of
    // the exported symbol information for the string so far.
    // If there is no exported symbol, the node starts with a zero byte.
    // If there is exported info, it follows the length.
    //
    // First is a uleb128 containing flags. Normally, it is followed by
    // a uleb128 encoded offset which is location of the content named
    // by the symbol from the mach_header for the image.  If the flags
    // is EXPORT_SYMBOL_FLAGS_REEXPORT, then following the flags is
    // a uleb128 encoded library ordinal, then a zero terminated
    // UTF8 string.  If the string is zero length, then the symbol
    // is re-export from the specified dylib with the same name.
    // If the flags is EXPORT_SYMBOL_FLAGS_STUB_AND_RESOLVER, then following
    // the flags is two uleb128s: the stub offset and the resolver offset.
    // The stub is used by non-lazy pointers.  The resolver is used
    // by lazy pointers and must be called to get the actual address to use.
    //
    // After the optional exported symbol information is a byte of
    // how many edges (0-255) that this node has leaving it,
    // followed by each edge.
    // Each edge is a zero terminated UTF8 of the addition chars
    // in the symbol, followed by a uleb128 offset for the node that
    // edge points to.

    /// file offset to lazy binding info
    export_off: u32 = 0,

    /// size of lazy binding info
    export_size: u32 = 0,
};

/// A program that uses a dynamic linker contains a dylinker_command to identify
/// the name of the dynamic linker (LC_LOAD_DYLINKER). And a dynamic linker
/// contains a dylinker_command to identify the dynamic linker (LC_ID_DYLINKER).
/// A file can have at most one of these.
/// This struct is also used for the LC_DYLD_ENVIRONMENT load command and contains
/// string for dyld to treat like an environment variable.
pub const dylinker_command = extern struct {
    /// LC_ID_DYLINKER, LC_LOAD_DYLINKER, or LC_DYLD_ENVIRONMENT
    cmd: LC,

    /// includes pathname string
    cmdsize: u32,

    /// A variable length string in a load command is represented by an lc_str
    /// union.  The strings are stored just after the load command structure and
    /// the offset is from the start of the load command structure.  The size
    /// of the string is reflected in the cmdsize field of the load command.
    /// Once again any padded bytes to bring the cmdsize field to a multiple
    /// of 4 bytes must be zero.
    name: u32,
};

/// A dynamically linked shared library (filetype == MH_DYLIB in the mach header)
/// contains a dylib_command (cmd == LC_ID_DYLIB) to identify the library.
/// An object that uses a dynamically linked shared library also contains a
/// dylib_command (cmd == LC_LOAD_DYLIB, LC_LOAD_WEAK_DYLIB, or
/// LC_REEXPORT_DYLIB) for each library it uses.
pub const dylib_command = extern struct {
    /// LC_ID_DYLIB, LC_LOAD_WEAK_DYLIB, LC_LOAD_DYLIB, LC_REEXPORT_DYLIB
    cmd: LC,

    /// includes pathname string
    cmdsize: u32,

    /// the library identification
    dylib: dylib,
};

/// Dynamically linked shared libraries are identified by two things.  The
/// pathname (the name of the library as found for execution), and the
/// compatibility version number.  The pathname must match and the compatibility
/// number in the user of the library must be greater than or equal to the
/// library being used.  The time stamp is used to record the time a library was
/// built and copied into user so it can be use to determined if the library used
/// at runtime is exactly the same as used to build the program.
pub const dylib = extern struct {
    /// library's pathname (offset pointing at the end of dylib_command)
    name: u32,

    /// library's build timestamp
    timestamp: u32,

    /// library's current version number
    current_version: u32,

    /// library's compatibility version number
    compatibility_version: u32,
};

/// The rpath_command contains a path which at runtime should be added to the current
/// run path used to find @rpath prefixed dylibs.
pub const rpath_command = extern struct {
    /// LC_RPATH
    cmd: LC = .RPATH,

    /// includes string
    cmdsize: u32,

    /// path to add to run path
    path: u32,
};

/// The segment load command indicates that a part of this file is to be
/// mapped into the task's address space.  The size of this segment in memory,
/// vmsize, maybe equal to or larger than the amount to map from this file,
/// filesize.  The file is mapped starting at fileoff to the beginning of
/// the segment in memory, vmaddr.  The rest of the memory of the segment,
/// if any, is allocated zero fill on demand.  The segment's maximum virtual
/// memory protection and initial virtual memory protection are specified
/// by the maxprot and initprot fields.  If the segment has sections then the
/// section structures directly follow the segment command and their size is
/// reflected in cmdsize.
pub const segment_command = extern struct {
    /// LC_SEGMENT
    cmd: LC = .SEGMENT,

    /// includes sizeof section structs
    cmdsize: u32,

    /// segment name
    segname: [16]u8,

    /// memory address of this segment
    vmaddr: u32,

    /// memory size of this segment
    vmsize: u32,

    /// file offset of this segment
    fileoff: u32,

    /// amount to map from the file
    filesize: u32,

    /// maximum VM protection
    maxprot: vm_prot_t,

    /// initial VM protection
    initprot: vm_prot_t,

    /// number of sections in segment
    nsects: u32,
    flags: u32,
};

/// The 64-bit segment load command indicates that a part of this file is to be
/// mapped into a 64-bit task's address space.  If the 64-bit segment has
/// sections then section_64 structures directly follow the 64-bit segment
/// command and their size is reflected in cmdsize.
pub const segment_command_64 = extern struct {
    /// LC_SEGMENT_64
    cmd: LC = .SEGMENT_64,

    /// includes sizeof section_64 structs
    cmdsize: u32,
    // TODO lazy values in stage2
    // cmdsize: u32 = @sizeOf(segment_command_64),

    /// segment name
    segname: [16]u8,

    /// memory address of this segment
    vmaddr: u64 = 0,

    /// memory size of this segment
    vmsize: u64 = 0,

    /// file offset of this segment
    fileoff: u64 = 0,

    /// amount to map from the file
    filesize: u64 = 0,

    /// maximum VM protection
    maxprot: vm_prot_t = .{},

    /// initial VM protection
    initprot: vm_prot_t = .{},

    /// number of sections in segment
    nsects: u32 = 0,
    flags: u32 = 0,

    pub fn segName(seg: *const segment_command_64) []const u8 {
        return parseName(&seg.segname);
    }

    pub fn isWriteable(seg: segment_command_64) bool {
        return seg.initprot.write;
    }
};

pub const PROT = struct {
    /// [MC2] no permissions
    pub const NONE: vm_prot_t = 0x00;
    /// [MC2] pages can be read
    pub const READ: vm_prot_t = 0x01;
    /// [MC2] pages can be written
    pub const WRITE: vm_prot_t = 0x02;
    /// [MC2] pages can be executed
    pub const EXEC: vm_prot_t = 0x04;
    /// When a caller finds that they cannot obtain write permission on a
    /// mapped entry, the following flag can be used. The entry will be
    /// made "needs copy" effectively copying the object (using COW),
    /// and write permission will be added to the maximum protections for
    /// the associated entry.
    pub const COPY: vm_prot_t = 0x10;
};

/// A segment is made up of zero or more sections.  Non-MH_OBJECT files have
/// all of their segments with the proper sections in each, and padded to the
/// specified segment alignment when produced by the link editor.  The first
/// segment of a MH_EXECUTE and MH_FVMLIB format file contains the mach_header
/// and load commands of the object file before its first section.  The zero
/// fill sections are always last in their segment (in all formats).  This
/// allows the zeroed segment padding to be mapped into memory where zero fill
/// sections might be. The gigabyte zero fill sections, those with the section
/// type S_GB_ZEROFILL, can only be in a segment with sections of this type.
/// These segments are then placed after all other segments.
///
/// The MH_OBJECT format has all of its sections in one segment for
/// compactness.  There is no padding to a specified segment boundary and the
/// mach_header and load commands are not part of the segment.
///
/// Sections with the same section name, sectname, going into the same segment,
/// segname, are combined by the link editor.  The resulting section is aligned
/// to the maximum alignment of the combined sections and is the new section's
/// alignment.  The combined sections are aligned to their original alignment in
/// the combined section.  Any padded bytes to get the specified alignment are
/// zeroed.
///
/// The format of the relocation entries referenced by the reloff and nreloc
/// fields of the section structure for mach object files is described in the
/// header file <reloc.h>.
pub const section = extern struct {
    /// name of this section
    sectname: [16]u8,

    /// segment this section goes in
    segname: [16]u8,

    /// memory address of this section
    addr: u32,

    /// size in bytes of this section
    size: u32,

    /// file offset of this section
    offset: u32,

    /// section alignment (power of 2)
    @"align": u32,

    /// file offset of relocation entries
    reloff: u32,

    /// number of relocation entries
    nreloc: u32,

    /// flags (section type and attributes
    flags: u32,

    /// reserved (for offset or index)
    reserved1: u32,

    /// reserved (for count or sizeof)
    reserved2: u32,
};

pub const section_64 = extern struct {
    /// name of this section
    sectname: [16]u8,

    /// segment this section goes in
    segname: [16]u8,

    /// memory address of this section
    addr: u64 = 0,

    /// size in bytes of this section
    size: u64 = 0,

    /// file offset of this section
    offset: u32 = 0,

    /// section alignment (power of 2)
    @"align": u32 = 0,

    /// file offset of relocation entries
    reloff: u32 = 0,

    /// number of relocation entries
    nreloc: u32 = 0,

    /// flags (section type and attributes
    flags: u32 = S_REGULAR,

    /// reserved (for offset or index)
    reserved1: u32 = 0,

    /// reserved (for count or sizeof)
    reserved2: u32 = 0,

    /// reserved
    reserved3: u32 = 0,

    pub fn sectName(sect: *const section_64) []const u8 {
        return parseName(&sect.sectname);
    }

    pub fn segName(sect: *const section_64) []const u8 {
        return parseName(&sect.segname);
    }

    pub fn @"type"(sect: section_64) u8 {
        return @as(u8, @truncate(sect.flags & 0xff));
    }

    pub fn attrs(sect: section_64) u32 {
        return sect.flags & 0xffffff00;
    }

    pub fn isCode(sect: section_64) bool {
        const attr = sect.attrs();
        return attr & S_ATTR_PURE_INSTRUCTIONS != 0 or attr & S_ATTR_SOME_INSTRUCTIONS != 0;
    }

    pub fn isZerofill(sect: section_64) bool {
        const tt = sect.type();
        return tt == S_ZEROFILL or tt == S_GB_ZEROFILL or tt == S_THREAD_LOCAL_ZEROFILL;
    }

    pub fn isSymbolStubs(sect: section_64) bool {
        const tt = sect.type();
        return tt == S_SYMBOL_STUBS;
    }

    pub fn isDebug(sect: section_64) bool {
        return sect.attrs() & S_ATTR_DEBUG != 0;
    }

    pub fn isDontDeadStrip(sect: section_64) bool {
        return sect.attrs() & S_ATTR_NO_DEAD_STRIP != 0;
    }

    pub fn isDontDeadStripIfReferencesLive(sect: section_64) bool {
        return sect.attrs() & S_ATTR_LIVE_SUPPORT != 0;
    }
};

fn parseName(name: *const [16]u8) []const u8 {
    const len = mem.findScalar(u8, name, @as(u8, 0)) orelse name.len;
    return name[0..len];
}

pub const nlist = extern struct {
    n_strx: u32,
    n_type: u8,
    n_sect: u8,
    n_desc: i16,
    n_value: u32,
};

pub const nlist_64 = extern struct {
    n_strx: u32,
    n_type: packed union(u8) {
        bits: packed struct(u8) {
            ext: bool,
            type: enum(u3) {
                undf = 0,
                abs = 1,
                sect = 7,
                pbud = 6,
                indr = 5,
                _,
            },
            pext: bool,
            /// Any non-zero value indicates this is an stab, so the `stab` field should be used.
            is_stab: u3,
        },
        stab: enum(u8) {
            gsym = N_GSYM,
            fname = N_FNAME,
            fun = N_FUN,
            stsym = N_STSYM,
            lcsym = N_LCSYM,
            bnsym = N_BNSYM,
            ast = N_AST,
            opt = N_OPT,
            rsym = N_RSYM,
            sline = N_SLINE,
            ensym = N_ENSYM,
            ssym = N_SSYM,
            so = N_SO,
            oso = N_OSO,
            lsym = N_LSYM,
            bincl = N_BINCL,
            sol = N_SOL,
            params = N_PARAMS,
            version = N_VERSION,
            olevel = N_OLEVEL,
            psym = N_PSYM,
            eincl = N_EINCL,
            entry = N_ENTRY,
            lbrac = N_LBRAC,
            excl = N_EXCL,
            rbrac = N_RBRAC,
            bcomm = N_BCOMM,
            ecomm = N_ECOMM,
            ecoml = N_ECOML,
            leng = N_LENG,
            _,
        },
    },
    n_sect: u8,
    n_desc: packed struct(u16) {
        _pad0: u3 = 0,
        arm_thumb_def: bool,
        referenced_dynamically: bool,
        /// The meaning of this bit is contextual.
        /// See `N_DESC_DISCARDED` and `N_NO_DEAD_STRIP`.
        discarded_or_no_dead_strip: bool,
        weak_ref: bool,
        /// The meaning of this bit is contextual.
        /// See `N_WEAK_DEF` and `N_REF_TO_WEAK`.
        weak_def_or_ref_to_weak: bool,
        symbol_resolver: bool,
        alt_entry: bool,
        _pad2: u6 = 0,
    },
    n_value: u64,

    pub fn tentative(sym: nlist_64) bool {
        return sym.n_type.bits.type == .undf and sym.n_value != 0;
    }
};

/// Format of a relocation entry of a Mach-O file.  Modified from the 4.3BSD
/// format.  The modifications from the original format were changing the value
/// of the r_symbolnum field for "local" (r_extern == 0) relocation entries.
/// This modification is required to support symbols in an arbitrary number of
/// sections not just the three sections (text, data and bss) in a 4.3BSD file.
/// Also the last 4 bits have had the r_type tag added to them.
pub const relocation_info = packed struct {
    /// offset in the section to what is being relocated
    r_address: i32,

    /// symbol index if r_extern == 1 or section ordinal if r_extern == 0
    r_symbolnum: u24,

    /// was relocated pc relative already
    r_pcrel: u1,

    /// 0=byte, 1=word, 2=long, 3=quad
    r_length: u2,

    /// does not include value of sym referenced
    r_extern: u1,

    /// if not 0, machine specific relocation type
    r_type: u4,
};

/// After MacOS X 10.1 when a new load command is added that is required to be
/// understood by the dynamic linker for the image to execute properly the
/// LC_REQ_DYLD bit will be or'ed into the load command constant.  If the dynamic
/// linker sees such a load command it it does not understand will issue a
/// "unknown load command required for execution" error and refuse to use the
/// image.  Other load commands without this bit that are not understood will
/// simply be ignored.
pub const LC_REQ_DYLD = 0x80000000;

pub const LC = enum(u32) {
    /// No load command - invalid
    NONE = 0x0,

    /// segment of this file to be mapped
    SEGMENT = 0x1,

    /// link-edit stab symbol table info
    SYMTAB = 0x2,

    /// link-edit gdb symbol table info (obsolete)
    SYMSEG = 0x3,

    /// thread
    THREAD = 0x4,

    /// unix thread (includes a stack)
    UNIXTHREAD = 0x5,

    /// load a specified fixed VM shared library
    LOADFVMLIB = 0x6,

    /// fixed VM shared library identification
    IDFVMLIB = 0x7,

    /// object identification info (obsolete)
    IDENT = 0x8,

    /// fixed VM file inclusion (internal use)
    FVMFILE = 0x9,

    /// prepage command (internal use)
    PREPAGE = 0xa,

    /// dynamic link-edit symbol table info
    DYSYMTAB = 0xb,

    /// load a dynamically linked shared library
    LOAD_DYLIB = 0xc,

    /// dynamically linked shared lib ident
    ID_DYLIB = 0xd,

    /// load a dynamic linker
    LOAD_DYLINKER = 0xe,

    /// dynamic linker identification
    ID_DYLINKER = 0xf,

    /// modules prebound for a dynamically
    PREBOUND_DYLIB = 0x10,

    /// image routines
    ROUTINES = 0x11,

    /// sub framework
    SUB_FRAMEWORK = 0x12,

    /// sub umbrella
    SUB_UMBRELLA = 0x13,

    /// sub client
    SUB_CLIENT = 0x14,

    /// sub library
    SUB_LIBRARY = 0x15,

    /// two-level namespace lookup hints
    TWOLEVEL_HINTS = 0x16,

    /// prebind checksum
    PREBIND_CKSUM = 0x17,

    /// load a dynamically linked shared library that is allowed to be missing
    /// (all symbols are weak imported).
    LOAD_WEAK_DYLIB = 0x18 | LC_REQ_DYLD,

    /// 64-bit segment of this file to be mapped
    SEGMENT_64 = 0x19,

    /// 64-bit image routines
    ROUTINES_64 = 0x1a,

    /// the uuid
    UUID = 0x1b,

    /// runpath additions
    RPATH = 0x1c | LC_REQ_DYLD,

    /// local of code signature
    CODE_SIGNATURE = 0x1d,

    /// local of info to split segments
    SEGMENT_SPLIT_INFO = 0x1e,

    /// load and re-export dylib
    REEXPORT_DYLIB = 0x1f | LC_REQ_DYLD,

    /// delay load of dylib until first use
    LAZY_LOAD_DYLIB = 0x20,

    /// encrypted segment information
    ENCRYPTION_INFO = 0x21,

    /// compressed dyld information
    DYLD_INFO = 0x22,

    /// compressed dyld information only
    DYLD_INFO_ONLY = 0x22 | LC_REQ_DYLD,

    /// load upward dylib
    LOAD_UPWARD_DYLIB = 0x23 | LC_REQ_DYLD,

    /// build for MacOSX min OS version
    VERSION_MIN_MACOSX = 0x24,

    /// build for iPhoneOS min OS version
    VERSION_MIN_IPHONEOS = 0x25,

    /// compressed table of function start addresses
    FUNCTION_STARTS = 0x26,

    /// string for dyld to treat like environment variable
    DYLD_ENVIRONMENT = 0x27,

    /// replacement for LC_UNIXTHREAD
    MAIN = 0x28 | LC_REQ_DYLD,

    /// table of non-instructions in __text
    DATA_IN_CODE = 0x29,

    /// source version used to build binary
    SOURCE_VERSION = 0x2A,

    /// Code signing DRs copied from linked dylibs
    DYLIB_CODE_SIGN_DRS = 0x2B,

    /// 64-bit encrypted segment information
    ENCRYPTION_INFO_64 = 0x2C,

    /// linker options in MH_OBJECT files
    LINKER_OPTION = 0x2D,

    /// optimization hints in MH_OBJECT files
    LINKER_OPTIMIZATION_HINT = 0x2E,

    /// build for AppleTV min OS version
    VERSION_MIN_TVOS = 0x2F,

    /// build for Watch min OS version
    VERSION_MIN_WATCHOS = 0x30,

    /// arbitrary data included within a Mach-O file
    NOTE = 0x31,

    /// build for platform min OS version
    BUILD_VERSION = 0x32,

    /// used with linkedit_data_command, payload is trie
    DYLD_EXPORTS_TRIE = 0x33 | LC_REQ_DYLD,

    /// used with linkedit_data_command
    DYLD_CHAINED_FIXUPS = 0x34 | LC_REQ_DYLD,

    _,
};

/// the mach magic number
pub const MH_MAGIC = 0xfeedface;

/// NXSwapInt(MH_MAGIC)
pub const MH_CIGAM = 0xcefaedfe;

/// the 64-bit mach magic number
pub const MH_MAGIC_64 = 0xfeedfacf;

/// NXSwapInt(MH_MAGIC_64)
pub const MH_CIGAM_64 = 0xcffaedfe;

/// relocatable object file
pub const MH_OBJECT = 0x1;

/// demand paged executable file
pub const MH_EXECUTE = 0x2;

/// fixed VM shared library file
pub const MH_FVMLIB = 0x3;

/// core file
pub const MH_CORE = 0x4;

/// preloaded executable file
pub const MH_PRELOAD = 0x5;

/// dynamically bound shared library
pub const MH_DYLIB = 0x6;

/// dynamic link editor
pub const MH_DYLINKER = 0x7;

/// dynamically bound bundle file
pub const MH_BUNDLE = 0x8;

/// shared library stub for static linking only, no section contents
pub const MH_DYLIB_STUB = 0x9;

/// companion file with only debug sections
pub const MH_DSYM = 0xa;

/// x86_64 kexts
pub const MH_KEXT_BUNDLE = 0xb;

// Constants for the flags field of the mach_header

/// the object file has no undefined references
pub const MH_NOUNDEFS = 0x1;

/// the object file is the output of an incremental link against a base file and can't be link edited again
pub const MH_INCRLINK = 0x2;

/// the object file is input for the dynamic linker and can't be statically link edited again
pub const MH_DYLDLINK = 0x4;

/// the object file's undefined references are bound by the dynamic linker when loaded.
pub const MH_BINDATLOAD = 0x8;

/// the file has its dynamic undefined references prebound.
pub const MH_PREBOUND = 0x10;

/// the file has its read-only and read-write segments split
pub const MH_SPLIT_SEGS = 0x20;

/// the shared library init routine is to be run lazily via catching memory faults to its writeable segments (obsolete)
pub const MH_LAZY_INIT = 0x40;

/// the image is using two-level name space bindings
pub const MH_TWOLEVEL = 0x80;

/// the executable is forcing all images to use flat name space bindings
pub const MH_FORCE_FLAT = 0x100;

/// this umbrella guarantees no multiple definitions of symbols in its sub-images so the two-level namespace hints can always be used.
pub const MH_NOMULTIDEFS = 0x200;

/// do not have dyld notify the prebinding agent about this executable
pub const MH_NOFIXPREBINDING = 0x400;

/// the binary is not prebound but can have its prebinding redone. only used when MH_PREBOUND is not set.
pub const MH_PREBINDABLE = 0x800;

/// indicates that this binary binds to all two-level namespace modules of its dependent libraries. only used when MH_PREBINDABLE and MH_TWOLEVEL are both set.
pub const MH_ALLMODSBOUND = 0x1000;

/// safe to divide up the sections into sub-sections via symbols for dead code stripping
pub const MH_SUBSECTIONS_VIA_SYMBOLS = 0x2000;

/// the binary has been canonicalized via the unprebind operation
pub const MH_CANONICAL = 0x4000;

/// the final linked image contains external weak symbols
pub const MH_WEAK_DEFINES = 0x8000;

/// the final linked image uses weak symbols
pub const MH_BINDS_TO_WEAK = 0x10000;

/// When this bit is set, all stacks in the task will be given stack execution privilege.  Only used in MH_EXECUTE filetypes.
pub const MH_ALLOW_STACK_EXECUTION = 0x20000;

/// When this bit is set, the binary declares it is safe for use in processes with uid zero
pub const MH_ROOT_SAFE = 0x40000;

/// When this bit is set, the binary declares it is safe for use in processes when issetugid() is true
pub const MH_SETUID_SAFE = 0x80000;

/// When this bit is set on a dylib, the static linker does not need to examine dependent dylibs to see if any are re-exported
pub const MH_NO_REEXPORTED_DYLIBS = 0x100000;

/// When this bit is set, the OS will load the main executable at a random address.  Only used in MH_EXECUTE filetypes.
pub const MH_PIE = 0x200000;

/// Only for use on dylibs.  When linking against a dylib that has this bit set, the static linker will automatically not create a LC_LOAD_DYLIB load command to the dylib if no symbols are being referenced from the dylib.
pub const MH_DEAD_STRIPPABLE_DYLIB = 0x400000;

/// Contains a section of type S_THREAD_LOCAL_VARIABLES
pub const MH_HAS_TLV_DESCRIPTORS = 0x800000;

/// When this bit is set, the OS will run the main executable with a non-executable heap even on platforms (e.g. x86) that don't require it. Only used in MH_EXECUTE filetypes.
pub const MH_NO_HEAP_EXECUTION = 0x1000000;

/// The code was linked for use in an application extension.
pub const MH_APP_EXTENSION_SAFE = 0x02000000;

/// The external symbols listed in the nlist symbol table do not include all the symbols listed in the dyld info.
pub const MH_NLIST_OUTOFSYNC_WITH_DYLDINFO = 0x04000000;

/// Allow LC_MIN_VERSION_MACOS and LC_BUILD_VERSION load commands with the platforms macOS, iOSMac, iOSSimulator, tvOSSimulator and watchOSSimulator.
pub const MH_SIM_SUPPORT = 0x08000000;

/// Only for use on dylibs. When this bit is set, the dylib is part of the dyld shared cache, rather than loose in the filesystem.
pub const MH_DYLIB_IN_CACHE = 0x80000000;

// Constants for the flags field of the fat_header

/// the fat magic number
pub const FAT_MAGIC = 0xcafebabe;

/// NXSwapLong(FAT_MAGIC)
pub const FAT_CIGAM = 0xbebafeca;

/// the 64-bit fat magic number
pub const FAT_MAGIC_64 = 0xcafebabf;

/// NXSwapLong(FAT_MAGIC_64)
pub const FAT_CIGAM_64 = 0xbfbafeca;

/// Segment flags
/// The file contents for this segment is for the high part of the VM space, the low part
/// is zero filled (for stacks in core files).
pub const SG_HIGHVM = 0x1;
/// This segment is the VM that is allocated by a fixed VM library, for overlap checking in
/// the link editor.
pub const SG_FVMLIB = 0x2;
/// This segment has nothing that was relocated in it and nothing relocated to it, that is
/// it maybe safely replaced without relocation.
pub const SG_NORELOC = 0x4;
/// This segment is protected.  If the segment starts at file offset 0, the
/// first page of the segment is not protected.  All other pages of the segment are protected.
pub const SG_PROTECTED_VERSION_1 = 0x8;
/// This segment is made read-only after fixups
pub const SG_READ_ONLY = 0x10;

/// The flags field of a section structure is separated into two parts a section
/// type and section attributes.  The section types are mutually exclusive (it
/// can only have one type) but the section attributes are not (it may have more
/// than one attribute).
/// 256 section types
pub const SECTION_TYPE = 0x000000ff;

///  24 section attributes
pub const SECTION_ATTRIBUTES = 0xffffff00;

/// regular section
pub const S_REGULAR = 0x0;

/// zero fill on demand section
pub const S_ZEROFILL = 0x1;

/// section with only literal C string
pub const S_CSTRING_LITERALS = 0x2;

/// section with only 4 byte literals
pub const S_4BYTE_LITERALS = 0x3;

/// section with only 8 byte literals
pub const S_8BYTE_LITERALS = 0x4;

/// section with only pointers to
pub const S_LITERAL_POINTERS = 0x5;

/// if any of these bits set, a symbolic debugging entry
pub const N_STAB = 0xe0;

/// private external symbol bit
pub const N_PEXT = 0x10;

/// mask for the type bits
pub const N_TYPE = 0x0e;

/// external symbol bit, set for external symbols
pub const N_EXT = 0x01;

/// symbol is undefined
pub const N_UNDF = 0x0;

/// symbol is absolute
pub const N_ABS = 0x2;

/// symbol is defined in the section number given in n_sect
pub const N_SECT = 0xe;

/// symbol is undefined  and the image is using a prebound
/// value  for the symbol
pub const N_PBUD = 0xc;

/// symbol is defined to be the same as another symbol; the n_value
/// field is an index into the string table specifying the name of the
/// other symbol
pub const N_INDR = 0xa;

/// global symbol: name,,NO_SECT,type,0
pub const N_GSYM = 0x20;

/// procedure name (f77 kludge): name,,NO_SECT,0,0
pub const N_FNAME = 0x22;

/// procedure: name,,n_sect,linenumber,address
pub const N_FUN = 0x24;

/// static symbol: name,,n_sect,type,address
pub const N_STSYM = 0x26;

/// .lcomm symbol: name,,n_sect,type,address
pub const N_LCSYM = 0x28;

/// begin nsect sym: 0,,n_sect,0,address
pub const N_BNSYM = 0x2e;

/// AST file path: name,,NO_SECT,0,0
pub const N_AST = 0x32;

/// emitted with gcc2_compiled and in gcc source
pub const N_OPT = 0x3c;

/// register sym: name,,NO_SECT,type,register
pub const N_RSYM = 0x40;

/// src line: 0,,n_sect,linenumber,address
pub const N_SLINE = 0x44;

/// end nsect sym: 0,,n_sect,0,address
pub const N_ENSYM = 0x4e;

/// structure elt: name,,NO_SECT,type,struct_offset
pub const N_SSYM = 0x60;

/// source file name: name,,n_sect,0,address
pub const N_SO = 0x64;

/// object file name: name,,0,0,st_mtime
pub const N_OSO = 0x66;

/// local sym: name,,NO_SECT,type,offset
pub const N_LSYM = 0x80;

/// include file beginning: name,,NO_SECT,0,sum
pub const N_BINCL = 0x82;

/// #included file name: name,,n_sect,0,address
pub const N_SOL = 0x84;

/// compiler parameters: name,,NO_SECT,0,0
pub const N_PARAMS = 0x86;

/// compiler version: name,,NO_SECT,0,0
pub const N_VERSION = 0x88;

/// compiler -O level: name,,NO_SECT,0,0
pub const N_OLEVEL = 0x8A;

/// parameter: name,,NO_SECT,type,offset
pub const N_PSYM = 0xa0;

/// include file end: name,,NO_SECT,0,0
pub const N_EINCL = 0xa2;

/// alternate entry: name,,n_sect,linenumber,address
pub const N_ENTRY = 0xa4;

/// left bracket: 0,,NO_SECT,nesting level,address
pub const N_LBRAC = 0xc0;

/// deleted include file: name,,NO_SECT,0,sum
pub const N_EXCL = 0xc2;

/// right bracket: 0,,NO_SECT,nesting level,address
pub const N_RBRAC = 0xe0;

/// begin common: name,,NO_SECT,0,0
pub const N_BCOMM = 0xe2;

/// end common: name,,n_sect,0,0
pub const N_ECOMM = 0xe4;

/// end common (local name): 0,,n_sect,0,address
pub const N_ECOML = 0xe8;

/// second stab entry with length information
pub const N_LENG = 0xfe;

// For the two types of symbol pointers sections and the symbol stubs section
// they have indirect symbol table entries.  For each of the entries in the
// section the indirect symbol table entries, in corresponding order in the
// indirect symbol table, start at the index stored in the reserved1 field
// of the section structure.  Since the indirect symbol table entries
// correspond to the entries in the section the number of indirect symbol table
// entries is inferred from the size of the section divided by the size of the
// entries in the section.  For symbol pointers sections the size of the entries
// in the section is 4 bytes and for symbol stubs sections the byte size of the
// stubs is stored in the reserved2 field of the section structure.

/// section with only non-lazy symbol pointers
pub const S_NON_LAZY_SYMBOL_POINTERS = 0x6;

/// section with only lazy symbol pointers
pub const S_LAZY_SYMBOL_POINTERS = 0x7;

/// section with only symbol stubs, byte size of stub in the reserved2 field
pub const S_SYMBOL_STUBS = 0x8;

/// section with only function pointers for initialization
pub const S_MOD_INIT_FUNC_POINTERS = 0x9;

/// section with only function pointers for termination
pub const S_MOD_TERM_FUNC_POINTERS = 0xa;

/// section contains symbols that are to be coalesced
pub const S_COALESCED = 0xb;

/// zero fill on demand section (that can be larger than 4 gigabytes)
pub const S_GB_ZEROFILL = 0xc;

/// section with only pairs of function pointers for interposing
pub const S_INTERPOSING = 0xd;

/// section with only 16 byte literals
pub const S_16BYTE_LITERALS = 0xe;

/// section contains DTrace Object Format
pub const S_DTRACE_DOF = 0xf;

/// section with only lazy symbol pointers to lazy loaded dylibs
pub const S_LAZY_DYLIB_SYMBOL_POINTERS = 0x10;

// If a segment contains any sections marked with S_ATTR_DEBUG then all
// sections in that segment must have this attribute.  No section other than
// a section marked with this attribute may reference the contents of this
// section.  A section with this attribute may contain no symbols and must have
// a section type S_REGULAR.  The static linker will not copy section contents
// from sections with this attribute into its output file.  These sections
// generally contain DWARF debugging info.

/// a debug section
pub const S_ATTR_DEBUG = 0x02000000;

/// section contains only true machine instructions
pub const S_ATTR_PURE_INSTRUCTIONS = 0x80000000;

/// section contains coalesced symbols that are not to be in a ranlib
/// table of contents
pub const S_ATTR_NO_TOC = 0x40000000;

/// ok to strip static symbols in this section in files with the
/// MH_DYLDLINK flag
pub const S_ATTR_STRIP_STATIC_SYMS = 0x20000000;

/// no dead stripping
pub const S_ATTR_NO_DEAD_STRIP = 0x10000000;

/// blocks are live if they reference live blocks
pub const S_ATTR_LIVE_SUPPORT = 0x8000000;

/// used with x86 code stubs written on by dyld
pub const S_ATTR_SELF_MODIFYING_CODE = 0x4000000;

/// section contains some machine instructions
pub const S_ATTR_SOME_INSTRUCTIONS = 0x400;

/// section has external relocation entries
pub const S_ATTR_EXT_RELOC = 0x200;

/// section has local relocation entries
pub const S_ATTR_LOC_RELOC = 0x100;

/// template of initial values for TLVs
pub const S_THREAD_LOCAL_REGULAR = 0x11;

/// template of initial values for TLVs
pub const S_THREAD_LOCAL_ZEROFILL = 0x12;

/// TLV descriptors
pub const S_THREAD_LOCAL_VARIABLES = 0x13;

/// pointers to TLV descriptors
pub const S_THREAD_LOCAL_VARIABLE_POINTERS = 0x14;

/// functions to call to initialize TLV values
pub const S_THREAD_LOCAL_INIT_FUNCTION_POINTERS = 0x15;

/// 32-bit offsets to initializers
pub const S_INIT_FUNC_OFFSETS = 0x16;

/// CPU type targeting 64-bit Intel-based Macs
pub const CPU_TYPE_X86_64: cpu_type_t = 0x01000007;

/// CPU type targeting 64-bit ARM-based Macs
pub const CPU_TYPE_ARM64: cpu_type_t = 0x0100000C;

/// All Intel-based Macs
pub const CPU_SUBTYPE_X86_64_ALL: cpu_subtype_t = 0x3;

/// All ARM-based Macs
pub const CPU_SUBTYPE_ARM_ALL: cpu_subtype_t = 0x0;

// The following are used to encode rebasing information
pub const REBASE_TYPE_POINTER: u8 = 1;
pub const REBASE_TYPE_TEXT_ABSOLUTE32: u8 = 2;
pub const REBASE_TYPE_TEXT_PCREL32: u8 = 3;

pub const REBASE_OPCODE_MASK: u8 = 0xF0;
pub const REBASE_IMMEDIATE_MASK: u8 = 0x0F;
pub const REBASE_OPCODE_DONE: u8 = 0x00;
pub const REBASE_OPCODE_SET_TYPE_IMM: u8 = 0x10;
pub const REBASE_OPCODE_SET_SEGMENT_AND_OFFSET_ULEB: u8 = 0x20;
pub const REBASE_OPCODE_ADD_ADDR_ULEB: u8 = 0x30;
pub const REBASE_OPCODE_ADD_ADDR_IMM_SCALED: u8 = 0x40;
pub const REBASE_OPCODE_DO_REBASE_IMM_TIMES: u8 = 0x50;
pub const REBASE_OPCODE_DO_REBASE_ULEB_TIMES: u8 = 0x60;
pub const REBASE_OPCODE_DO_REBASE_ADD_ADDR_ULEB: u8 = 0x70;
pub const REBASE_OPCODE_DO_REBASE_ULEB_TIMES_SKIPPING_ULEB: u8 = 0x80;

// The following are used to encode binding information
pub const BIND_TYPE_POINTER: u8 = 1;
pub const BIND_TYPE_TEXT_ABSOLUTE32: u8 = 2;
pub const BIND_TYPE_TEXT_PCREL32: u8 = 3;

pub const BIND_SPECIAL_DYLIB_SELF: i8 = 0;
pub const BIND_SPECIAL_DYLIB_MAIN_EXECUTABLE: i8 = -1;
pub const BIND_SPECIAL_DYLIB_FLAT_LOOKUP: i8 = -2;

pub const BIND_SYMBOL_FLAGS_WEAK_IMPORT: u8 = 0x1;
pub const BIND_SYMBOL_FLAGS_NON_WEAK_DEFINITION: u8 = 0x8;

pub const BIND_OPCODE_MASK: u8 = 0xf0;
pub const BIND_IMMEDIATE_MASK: u8 = 0x0f;
pub const BIND_OPCODE_DONE: u8 = 0x00;
pub const BIND_OPCODE_SET_DYLIB_ORDINAL_IMM: u8 = 0x10;
pub const BIND_OPCODE_SET_DYLIB_ORDINAL_ULEB: u8 = 0x20;
pub const BIND_OPCODE_SET_DYLIB_SPECIAL_IMM: u8 = 0x30;
pub const BIND_OPCODE_SET_SYMBOL_TRAILING_FLAGS_IMM: u8 = 0x40;
pub const BIND_OPCODE_SET_TYPE_IMM: u8 = 0x50;
pub const BIND_OPCODE_SET_ADDEND_SLEB: u8 = 0x60;
pub const BIND_OPCODE_SET_SEGMENT_AND_OFFSET_ULEB: u8 = 0x70;
pub const BIND_OPCODE_ADD_ADDR_ULEB: u8 = 0x80;
pub const BIND_OPCODE_DO_BIND: u8 = 0x90;
pub const BIND_OPCODE_DO_BIND_ADD_ADDR_ULEB: u8 = 0xa0;
pub const BIND_OPCODE_DO_BIND_ADD_ADDR_IMM_SCALED: u8 = 0xb0;
pub const BIND_OPCODE_DO_BIND_ULEB_TIMES_SKIPPING_ULEB: u8 = 0xc0;

pub const reloc_type_x86_64 = enum(u4) {
    /// for absolute addresses
    X86_64_RELOC_UNSIGNED = 0,

    /// for signed 32-bit displacement
    X86_64_RELOC_SIGNED,

    /// a CALL/JMP instruction with 32-bit displacement
    X86_64_RELOC_BRANCH,

    /// a MOVQ load of a GOT entry
    X86_64_RELOC_GOT_LOAD,

    /// other GOT references
    X86_64_RELOC_GOT,

    /// must be followed by a X86_64_RELOC_UNSIGNED
    X86_64_RELOC_SUBTRACTOR,

    /// for signed 32-bit displacement with a -1 addend
    X86_64_RELOC_SIGNED_1,

    /// for signed 32-bit displacement with a -2 addend
    X86_64_RELOC_SIGNED_2,

    /// for signed 32-bit displacement with a -4 addend
    X86_64_RELOC_SIGNED_4,

    /// for thread local variables
    X86_64_RELOC_TLV,
};

pub const reloc_type_arm64 = enum(u4) {
    /// For pointers.
    ARM64_RELOC_UNSIGNED = 0,

    /// Must be followed by a ARM64_RELOC_UNSIGNED.
    ARM64_RELOC_SUBTRACTOR,

    /// A B/BL instruction with 26-bit displacement.
    ARM64_RELOC_BRANCH26,

    /// Pc-rel distance to page of target.
    ARM64_RELOC_PAGE21,

    /// Offset within page, scaled by r_length.
    ARM64_RELOC_PAGEOFF12,

    /// Pc-rel distance to page of GOT slot.
    ARM64_RELOC_GOT_LOAD_PAGE21,

    /// Offset within page of GOT slot, scaled by r_length.
    ARM64_RELOC_GOT_LOAD_PAGEOFF12,

    /// For pointers to GOT slots.
    ARM64_RELOC_POINTER_TO_GOT,

    /// Pc-rel distance to page of TLVP slot.
    ARM64_RELOC_TLVP_LOAD_PAGE21,

    /// Offset within page of TLVP slot, scaled by r_length.
    ARM64_RELOC_TLVP_LOAD_PAGEOFF12,

    /// Must be followed by PAGE21 or PAGEOFF12.
    ARM64_RELOC_ADDEND,
};

/// This symbol is a reference to an external non-lazy (data) symbol.
pub const REFERENCE_FLAG_UNDEFINED_NON_LAZY: u16 = 0x0;

/// This symbol is a reference to an external lazy symbol—that is, to a function call.
pub const REFERENCE_FLAG_UNDEFINED_LAZY: u16 = 0x1;

/// This symbol is defined in this module.
pub const REFERENCE_FLAG_DEFINED: u16 = 0x2;

/// This symbol is defined in this module and is visible only to modules within this shared library.
pub const REFERENCE_FLAG_PRIVATE_DEFINED: u16 = 3;

/// This symbol is defined in another module in this file, is a non-lazy (data) symbol, and is visible
/// only to modules within this shared library.
pub const REFERENCE_FLAG_PRIVATE_UNDEFINED_NON_LAZY: u16 = 4;

/// This symbol is defined in another module in this file, is a lazy (function) symbol, and is visible
/// only to modules within this shared library.
pub const REFERENCE_FLAG_PRIVATE_UNDEFINED_LAZY: u16 = 5;

/// Must be set for any defined symbol that is referenced by dynamic-loader APIs (such as dlsym and
/// NSLookupSymbolInImage) and not ordinary undefined symbol references. The strip tool uses this bit
/// to avoid removing symbols that must exist: If the symbol has this bit set, strip does not strip it.
pub const REFERENCED_DYNAMICALLY: u16 = 0x10;

/// The N_NO_DEAD_STRIP bit of the n_desc field only ever appears in a
/// relocatable .o file (MH_OBJECT filetype). And is used to indicate to the
/// static link editor it is never to dead strip the symbol.
pub const N_NO_DEAD_STRIP: u16 = 0x20;

/// Used by the dynamic linker at runtime. Do not set this bit.
pub const N_DESC_DISCARDED: u16 = 0x20;

/// Indicates that this symbol is a weak reference. If the dynamic linker cannot find a definition
/// for this symbol, it sets the address of this symbol to 0. The static linker sets this symbol given
/// the appropriate weak-linking flags.
pub const N_WEAK_REF: u16 = 0x40;

/// Indicates that this symbol is a weak definition. If the static linker or the dynamic linker finds
/// another (non-weak) definition for this symbol, the weak definition is ignored. Only symbols in a
/// coalesced section (page 23) can be marked as a weak definition.
pub const N_WEAK_DEF: u16 = 0x80;

/// The N_SYMBOL_RESOLVER bit of the n_desc field indicates that the
/// that the function is actually a resolver function and should
/// be called to get the address of the real function to use.
/// This bit is only available in .o files (MH_OBJECT filetype)
pub const N_SYMBOL_RESOLVER: u16 = 0x100;

// The following are used on the flags byte of a terminal node in the export information.
pub const EXPORT_SYMBOL_FLAGS_KIND_MASK: u8 = 0x03;
pub const EXPORT_SYMBOL_FLAGS_KIND_REGULAR: u8 = 0x00;
pub const EXPORT_SYMBOL_FLAGS_KIND_THREAD_LOCAL: u8 = 0x01;
pub const EXPORT_SYMBOL_FLAGS_KIND_ABSOLUTE: u8 = 0x02;
pub const EXPORT_SYMBOL_FLAGS_WEAK_DEFINITION: u8 = 0x04;
pub const EXPORT_SYMBOL_FLAGS_REEXPORT: u8 = 0x08;
pub const EXPORT_SYMBOL_FLAGS_STUB_AND_RESOLVER: u8 = 0x10;

// An indirect symbol table entry is simply a 32bit index into the symbol table
// to the symbol that the pointer or stub is referring to.  Unless it is for a
// non-lazy symbol pointer section for a defined symbol which strip(1) as
// removed.  In which case it has the value INDIRECT_SYMBOL_LOCAL.  If the
// symbol was also absolute INDIRECT_SYMBOL_ABS is or'ed with that.
pub const INDIRECT_SYMBOL_LOCAL: u32 = 0x80000000;
pub const INDIRECT_SYMBOL_ABS: u32 = 0x40000000;

// Codesign consts and structs taken from:
// https://opensource.apple.com/source/xnu/xnu-6153.81.5/osfmk/kern/cs_blobs.h.auto.html

/// Single Requirement blob
pub const CSMAGIC_REQUIREMENT: u32 = 0xfade0c00;
/// Requirements vector (internal requirements)
pub const CSMAGIC_REQUIREMENTS: u32 = 0xfade0c01;
/// CodeDirectory blob
pub const CSMAGIC_CODEDIRECTORY: u32 = 0xfade0c02;
/// embedded form of signature data
pub const CSMAGIC_EMBEDDED_SIGNATURE: u32 = 0xfade0cc0;
/// XXX
pub const CSMAGIC_EMBEDDED_SIGNATURE_OLD: u32 = 0xfade0b02;
/// Embedded entitlements
pub const CSMAGIC_EMBEDDED_ENTITLEMENTS: u32 = 0xfade7171;
/// Embedded DER encoded entitlements
pub const CSMAGIC_EMBEDDED_DER_ENTITLEMENTS: u32 = 0xfade7172;
/// Multi-arch collection of embedded signatures
pub const CSMAGIC_DETACHED_SIGNATURE: u32 = 0xfade0cc1;
/// CMS Signature, among other things
pub const CSMAGIC_BLOBWRAPPER: u32 = 0xfade0b01;

pub const CS_SUPPORTSSCATTER: u32 = 0x20100;
pub const CS_SUPPORTSTEAMID: u32 = 0x20200;
pub const CS_SUPPORTSCODELIMIT64: u32 = 0x20300;
pub const CS_SUPPORTSEXECSEG: u32 = 0x20400;

/// Slot index for CodeDirectory
pub const CSSLOT_CODEDIRECTORY: u32 = 0;
pub const CSSLOT_INFOSLOT: u32 = 1;
pub const CSSLOT_REQUIREMENTS: u32 = 2;
pub const CSSLOT_RESOURCEDIR: u32 = 3;
pub const CSSLOT_APPLICATION: u32 = 4;
pub const CSSLOT_ENTITLEMENTS: u32 = 5;
pub const CSSLOT_DER_ENTITLEMENTS: u32 = 7;

/// first alternate CodeDirectory, if any
pub const CSSLOT_ALTERNATE_CODEDIRECTORIES: u32 = 0x1000;
/// Max number of alternate CD slots
pub const CSSLOT_ALTERNATE_CODEDIRECTORY_MAX: u32 = 5;
/// One past the last
pub const CSSLOT_ALTERNATE_CODEDIRECTORY_LIMIT: u32 = CSSLOT_ALTERNATE_CODEDIRECTORIES + CSSLOT_ALTERNATE_CODEDIRECTORY_MAX;

/// CMS Signature
pub const CSSLOT_SIGNATURESLOT: u32 = 0x10000;
pub const CSSLOT_IDENTIFICATIONSLOT: u32 = 0x10001;
pub const CSSLOT_TICKETSLOT: u32 = 0x10002;

/// Compat with amfi
pub const CSTYPE_INDEX_REQUIREMENTS: u32 = 0x00000002;
/// Compat with amfi
pub const CSTYPE_INDEX_ENTITLEMENTS: u32 = 0x00000005;

pub const CS_HASHTYPE_SHA1: u8 = 1;
pub const CS_HASHTYPE_SHA256: u8 = 2;
pub const CS_HASHTYPE_SHA256_TRUNCATED: u8 = 3;
pub const CS_HASHTYPE_SHA384: u8 = 4;

pub const CS_SHA1_LEN: u32 = 20;
pub const CS_SHA256_LEN: u32 = 32;
pub const CS_SHA256_TRUNCATED_LEN: u32 = 20;

/// Always - larger hashes are truncated
pub const CS_CDHASH_LEN: u32 = 20;
/// Max size of the hash we'll support
pub const CS_HASH_MAX_SIZE: u32 = 48;

pub const CS_SIGNER_TYPE_UNKNOWN: u32 = 0;
pub const CS_SIGNER_TYPE_LEGACYVPN: u32 = 5;
pub const CS_SIGNER_TYPE_MAC_APP_STORE: u32 = 6;

pub const CS_ADHOC: u32 = 0x2;
pub const CS_LINKER_SIGNED: u32 = 0x20000;

pub const CS_EXECSEG_MAIN_BINARY: u32 = 0x1;

/// This CodeDirectory is tailored specifically at version 0x20400.
pub const CodeDirectory = extern struct {
    /// Magic number (CSMAGIC_CODEDIRECTORY)
    magic: u32,

    /// Total length of CodeDirectory blob
    length: u32,

    /// Compatibility version
    version: u32,

    /// Setup and mode flags
    flags: u32,

    /// Offset of hash slot element at index zero
    hashOffset: u32,

    /// Offset of identifier string
    identOffset: u32,

    /// Number of special hash slots
    nSpecialSlots: u32,

    /// Number of ordinary (code) hash slots
    nCodeSlots: u32,

    /// Limit to main image signature range
    codeLimit: u32,

    /// Size of each hash in bytes
    hashSize: u8,

    /// Type of hash (cdHashType* constants)
    hashType: u8,

    /// Platform identifier; zero if not platform binary
    platform: u8,

    /// log2(page size in bytes); 0 => infinite
    pageSize: u8,

    /// Unused (must be zero)
    spare2: u32,

    ///
    scatterOffset: u32,

    ///
    teamOffset: u32,

    ///
    spare3: u32,

    ///
    codeLimit64: u64,

    /// Offset of executable segment
    execSegBase: u64,

    /// Limit of executable segment
    execSegLimit: u64,

    /// Executable segment flags
    execSegFlags: u64,
};

/// Structure of an embedded-signature SuperBlob
pub const BlobIndex = extern struct {
    /// Type of entry
    type: u32,

    /// Offset of entry
    offset: u32,
};

/// This structure is followed by GenericBlobs in no particular
/// order as indicated by offsets in index
pub const SuperBlob = extern struct {
    /// Magic number
    magic: u32,

    /// Total length of SuperBlob
    length: u32,

    /// Number of index BlobIndex entries following this struct
    count: u32,
};

pub const GenericBlob = extern struct {
    /// Magic number
    magic: u32,

    /// Total length of blob
    length: u32,
};

/// The LC_DATA_IN_CODE load commands uses a linkedit_data_command
/// to point to an array of data_in_code_entry entries. Each entry
/// describes a range of data in a code section.
pub const data_in_code_entry = extern struct {
    /// From mach_header to start of data range.
    offset: u32,
    /// Number of bytes in data range.
    length: u16,
    /// A DICE_KIND value.
    kind: u16,
};

pub const LoadCommandIterator = struct {
    next_index: usize,
    ncmds: usize,
    r: std.Io.Reader,

    pub const LoadCommand = struct {
        hdr: load_command,
        data: []const u8,

        pub fn cast(lc: LoadCommand, comptime Cmd: type) ?Cmd {
            if (lc.data.len < @sizeOf(Cmd)) return null;
            const ptr: *align(1) const Cmd = @ptrCast(lc.data.ptr);
            var cmd = ptr.*;
            if (builtin.cpu.arch.endian() != .little) std.mem.byteSwapAllFields(Cmd, &cmd);
            return cmd;
        }

        /// Asserts LoadCommand is of type segment_command_64.
        /// If the native endian is not `.little`, the `section_64` values must be byte-swapped by the caller.
        pub fn getSections(lc: LoadCommand) []align(1) const section_64 {
            const segment_lc = lc.cast(segment_command_64).?;
            const sects_ptr: [*]align(1) const section_64 = @ptrCast(lc.data[@sizeOf(segment_command_64)..]);
            return sects_ptr[0..segment_lc.nsects];
        }

        /// Asserts LoadCommand is of type dylib_command.
        pub fn getDylibPathName(lc: LoadCommand) []const u8 {
            const dylib_lc = lc.cast(dylib_command).?;
            return mem.sliceTo(lc.data[dylib_lc.dylib.name..], 0);
        }

        /// Asserts LoadCommand is of type rpath_command.
        pub fn getRpathPathName(lc: LoadCommand) []const u8 {
            const rpath_lc = lc.cast(rpath_command).?;
            return mem.sliceTo(lc.data[rpath_lc.path..], 0);
        }

        /// Asserts LoadCommand is of type build_version_command.
        /// If the native endian is not `.little`, the `build_tool_version` values must be byte-swapped by the caller.
        pub fn getBuildVersionTools(lc: LoadCommand) []align(1) const build_tool_version {
            const build_lc = lc.cast(build_version_command).?;
            const tools_ptr: [*]align(1) const build_tool_version = @ptrCast(lc.data[@sizeOf(build_version_command)..]);
            return tools_ptr[0..build_lc.ntools];
        }
    };

    pub fn next(it: *LoadCommandIterator) error{InvalidMachO}!?LoadCommand {
        if (it.next_index >= it.ncmds) return null;

        const hdr = it.r.peekStruct(load_command, .little) catch |err| switch (err) {
            error.ReadFailed => unreachable,
            error.EndOfStream => return error.InvalidMachO,
        };
        const data = it.r.take(hdr.cmdsize) catch |err| switch (err) {
            error.ReadFailed => unreachable,
            error.EndOfStream => return error.InvalidMachO,
        };

        it.next_index += 1;
        return .{ .hdr = hdr, .data = data };
    }

    pub fn init(hdr: *const mach_header_64, cmds_buf_overlong: []const u8) error{InvalidMachO}!LoadCommandIterator {
        if (cmds_buf_overlong.len < hdr.sizeofcmds) return error.InvalidMachO;
        if (hdr.ncmds > 0 and hdr.sizeofcmds < @sizeOf(load_command)) return error.InvalidMachO;
        const cmds_buf = cmds_buf_overlong[0..hdr.sizeofcmds];
        return .{
            .next_index = 0,
            .ncmds = hdr.ncmds,
            .r = .fixed(cmds_buf),
        };
    }
};

pub const compact_unwind_encoding_t = u32;

// Relocatable object files: __LD,__compact_unwind

pub const compact_unwind_entry = extern struct {
    rangeStart: u64,
    rangeLength: u32,
    compactUnwindEncoding: u32,
    personalityFunction: u64,
    lsda: u64,
};

// Final linked images: __TEXT,__unwind_info
// The __TEXT,__unwind_info section is laid out for an efficient two level lookup.
// The header of the section contains a coarse index that maps function address
// to the page (4096 byte block) containing the unwind info for that function.

pub const UNWIND_SECTION_VERSION = 1;

pub const unwind_info_section_header = extern struct {
    /// UNWIND_SECTION_VERSION
    version: u32 = UNWIND_SECTION_VERSION,
    commonEncodingsArraySectionOffset: u32,
    commonEncodingsArrayCount: u32,
    personalityArraySectionOffset: u32,
    personalityArrayCount: u32,
    indexSectionOffset: u32,
    indexCount: u32,
    // compact_unwind_encoding_t[]
    // uint32_t personalities[]
    // unwind_info_section_header_index_entry[]
    // unwind_info_section_header_lsda_index_entry[]
};

pub const unwind_info_section_header_index_entry = extern struct {
    functionOffset: u32,

    /// section offset to start of regular or compress page
    secondLevelPagesSectionOffset: u32,

    /// section offset to start of lsda_index array for this range
    lsdaIndexArraySectionOffset: u32,
};

pub const unwind_info_section_header_lsda_index_entry = extern struct {
    functionOffset: u32,
    lsdaOffset: u32,
};

// There are two kinds of second level index pages: regular and compressed.
// A compressed page can hold up to 1021 entries, but it cannot be used if
// too many different encoding types are used. The regular page holds 511
// entries.

pub const unwind_info_regular_second_level_entry = extern struct {
    functionOffset: u32,
    encoding: compact_unwind_encoding_t,
};

pub const UNWIND_SECOND_LEVEL = enum(u32) {
    REGULAR = 2,
    COMPRESSED = 3,
    _,
};

pub const unwind_info_regular_second_level_page_header = extern struct {
    /// UNWIND_SECOND_LEVEL_REGULAR
    kind: UNWIND_SECOND_LEVEL = .REGULAR,

    entryPageOffset: u16,
    entryCount: u16,
    // entry array
};

pub const unwind_info_compressed_second_level_page_header = extern struct {
    /// UNWIND_SECOND_LEVEL_COMPRESSED
    kind: UNWIND_SECOND_LEVEL = .COMPRESSED,

    entryPageOffset: u16,
    entryCount: u16,
    encodingsPageOffset: u16,
    encodingsCount: u16,
    // 32bit entry array
    // encodings array
};

pub const UnwindInfoCompressedEntry = packed struct(u32) {
    funcOffset: u24,
    encodingIndex: u8,
};

pub const UNWIND_IS_NOT_FUNCTION_START: u32 = 0x80000000;
pub const UNWIND_HAS_LSDA: u32 = 0x40000000;
pub const UNWIND_PERSONALITY_MASK: u32 = 0x30000000;

// x86_64
pub const UNWIND_X86_64_MODE_MASK: u32 = 0x0F000000;
pub const UNWIND_X86_64_MODE = enum(u4) {
    OLD = 0,
    RBP_FRAME = 1,
    STACK_IMMD = 2,
    STACK_IND = 3,
    DWARF = 4,
};
pub const UNWIND_X86_64_RBP_FRAME_REGISTERS: u32 = 0x00007FFF;
pub const UNWIND_X86_64_RBP_FRAME_OFFSET: u32 = 0x00FF0000;

pub const UNWIND_X86_64_FRAMELESS_STACK_SIZE: u32 = 0x00FF0000;
pub const UNWIND_X86_64_FRAMELESS_STACK_ADJUST: u32 = 0x0000E000;
pub const UNWIND_X86_64_FRAMELESS_STACK_REG_COUNT: u32 = 0x00001C00;
pub const UNWIND_X86_64_FRAMELESS_STACK_REG_PERMUTATION: u32 = 0x000003FF;

pub const UNWIND_X86_64_DWARF_SECTION_OFFSET: u32 = 0x00FFFFFF;

pub const UNWIND_X86_64_REG = enum(u3) {
    NONE = 0,
    RBX = 1,
    R12 = 2,
    R13 = 3,
    R14 = 4,
    R15 = 5,
    RBP = 6,
};

// arm64
pub const UNWIND_ARM64_MODE_MASK: u32 = 0x0F000000;
pub const UNWIND_ARM64_MODE = enum(u4) {
    OLD = 0,
    FRAMELESS = 2,
    DWARF = 3,
    FRAME = 4,
};

pub const UNWIND_ARM64_FRAME_X19_X20_PAIR: u32 = 0x00000001;
pub const UNWIND_ARM64_FRAME_X21_X22_PAIR: u32 = 0x00000002;
pub const UNWIND_ARM64_FRAME_X23_X24_PAIR: u32 = 0x00000004;
pub const UNWIND_ARM64_FRAME_X25_X26_PAIR: u32 = 0x00000008;
pub const UNWIND_ARM64_FRAME_X27_X28_PAIR: u32 = 0x00000010;
pub const UNWIND_ARM64_FRAME_D8_D9_PAIR: u32 = 0x00000100;
pub const UNWIND_ARM64_FRAME_D10_D11_PAIR: u32 = 0x00000200;
pub const UNWIND_ARM64_FRAME_D12_D13_PAIR: u32 = 0x00000400;
pub const UNWIND_ARM64_FRAME_D14_D15_PAIR: u32 = 0x00000800;

pub const UNWIND_ARM64_FRAMELESS_STACK_SIZE_MASK: u32 = 0x00FFF000;
pub const UNWIND_ARM64_DWARF_SECTION_OFFSET: u32 = 0x00FFFFFF;

pub const CompactUnwindEncoding = packed struct(u32) {
    value: packed union {
        x86_64: packed union {
            frame: packed struct(u24) {
                reg4: u3,
                reg3: u3,
                reg2: u3,
                reg1: u3,
                reg0: u3,
                unused: u1 = 0,
                frame_offset: u8,
            },
            frameless: packed struct(u24) {
                stack_reg_permutation: u10,
                stack_reg_count: u3,
                stack: packed union {
                    direct: packed struct(u11) {
                        _: u3,
                        stack_size: u8,
                    },
                    indirect: packed struct(u11) {
                        stack_adjust: u3,
                        sub_offset: u8,
                    },
                },
            },
            dwarf: u24,
        },
        arm64: packed union {
            frame: packed struct(u24) {
                x_reg_pairs: packed struct(u5) {
                    x19_x20: u1,
                    x21_x22: u1,
                    x23_x24: u1,
                    x25_x26: u1,
                    x27_x28: u1,
                },
                d_reg_pairs: packed struct(u4) {
                    d8_d9: u1,
                    d10_d11: u1,
                    d12_d13: u1,
                    d14_d15: u1,
                },
                _: u15,
            },
            frameless: packed struct(u24) {
                _: u12 = 0,
                stack_size: u12,
            },
            dwarf: u24,
        },
    },
    mode: packed union {
        x86_64: UNWIND_X86_64_MODE,
        arm64: UNWIND_ARM64_MODE,
    },
    personality_index: u2,
    has_lsda: u1,
    start: u1,
};



---
File: /std/math.zig
---

const builtin = @import("builtin");
const std = @import("std.zig");
const float = @import("math/float.zig");
const assert = std.debug.assert;
const mem = std.mem;
const testing = std.testing;
const Alignment = std.mem.Alignment;

/// Euler's number (e)
pub const e = 2.71828182845904523536028747135266249775724709369995;

/// Archimedes' constant (π)
pub const pi = 3.14159265358979323846264338327950288419716939937510;

/// Phi or Golden ratio constant (Φ) = (1 + sqrt(5))/2
pub const phi = 1.6180339887498948482045868343656381177203091798057628621;

/// Circle constant (τ)
pub const tau = 2 * pi;

/// log2(e)
pub const log2e = 1.442695040888963407359924681001892137;

/// log10(e)
pub const log10e = 0.434294481903251827651128918916605082;

/// ln(2)
pub const ln2 = 0.693147180559945309417232121458176568;

/// ln(10)
pub const ln10 = 2.302585092994045684017991454684364208;

/// 2/sqrt(π)
pub const two_sqrtpi = 1.128379167095512573896158903121545172;

/// sqrt(2)
pub const sqrt2 = 1.414213562373095048801688724209698079;

/// 1/sqrt(2)
pub const sqrt1_2 = 0.707106781186547524400844362104849039;

/// pi/180.0
pub const rad_per_deg = 0.0174532925199432957692369076848861271344287188854172545609719144;

/// 180.0/pi
pub const deg_per_rad = 57.295779513082320876798154814105170332405472466564321549160243861;

pub const Sign = enum(u1) { positive, negative };
pub const FloatRepr = float.FloatRepr;
pub const floatExponentBits = float.floatExponentBits;
pub const floatMantissaBits = float.floatMantissaBits;
pub const floatFractionalBits = float.floatFractionalBits;
pub const floatExponentMin = float.floatExponentMin;
pub const floatExponentMax = float.floatExponentMax;
pub const floatTrueMin = float.floatTrueMin;
pub const floatMin = float.floatMin;
pub const floatMax = float.floatMax;
pub const floatEps = float.floatEps;
pub const floatEpsAt = float.floatEpsAt;
pub const inf = float.inf;
pub const nan = float.nan;
pub const snan = float.snan;

/// Performs an approximate comparison of two floating point values `x` and `y`.
/// Returns true if the absolute difference between them is less or equal than
/// the specified tolerance.
///
/// The `tolerance` parameter is the absolute tolerance used when determining if
/// the two numbers are close enough; a good value for this parameter is a small
/// multiple of `floatEps(T)`.
///
/// Note that this function is recommended for comparing small numbers
/// around zero; using `approxEqRel` is suggested otherwise.
///
/// NaN values are never considered equal to any value.
pub fn approxEqAbs(comptime T: type, x: T, y: T, tolerance: T) bool {
    assert(@typeInfo(T) == .float or @typeInfo(T) == .comptime_float);
    assert(tolerance >= 0);

    // Fast path for equal values (and signed zeros and infinites).
    if (x == y)
        return true;

    if (isNan(x) or isNan(y))
        return false;

    return @abs(x - y) <= tolerance;
}

/// Performs an approximate comparison of two floating point values `x` and `y`.
/// Returns true if the absolute difference between them is less or equal than
/// `max(|x|, |y|) * tolerance`, where `tolerance` is a positive number greater
/// than zero.
///
/// The `tolerance` parameter is the relative tolerance used when determining if
/// the two numbers are close enough; a good value for this parameter is usually
/// `sqrt(floatEps(T))`, meaning that the two numbers are considered equal if at
/// least half of the digits are equal.
///
/// Note that for comparisons of small numbers around zero this function won't
/// give meaningful results, use `approxEqAbs` instead.
///
/// NaN values are never considered equal to any value.
pub fn approxEqRel(comptime T: type, x: T, y: T, tolerance: T) bool {
    assert(@typeInfo(T) == .float or @typeInfo(T) == .comptime_float);
    assert(tolerance > 0);

    // Fast path for equal values (and signed zeros and infinites).
    if (x == y)
        return true;

    if (isNan(x) or isNan(y))
        return false;

    return @abs(x - y) <= @max(@abs(x), @abs(y)) * tolerance;
}

test approxEqAbs {
    inline for ([_]type{ f16, f32, f64, f128 }) |T| {
        const eps_value = comptime floatEps(T);
        const min_value = comptime floatMin(T);

        try testing.expect(approxEqAbs(T, 0.0, 0.0, eps_value));
        try testing.expect(approxEqAbs(T, -0.0, -0.0, eps_value));
        try testing.expect(approxEqAbs(T, 0.0, -0.0, eps_value));
        try testing.expect(!approxEqAbs(T, 1.0 + 2 * eps_value, 1.0, eps_value));
        try testing.expect(approxEqAbs(T, 1.0 + 1 * eps_value, 1.0, eps_value));
        try testing.expect(approxEqAbs(T, min_value, 0.0, eps_value * 2));
        try testing.expect(approxEqAbs(T, -min_value, 0.0, eps_value * 2));
    }

    comptime {
        // `comptime_float` is guaranteed to have the same precision and operations of
        // the largest other floating point type, which is f128 but it doesn't have a
        // defined layout so we can't rely on `@bitCast` to construct the smallest
        // possible epsilon value like we do in the tests above. In the same vein, we
        // also can't represent a max/min, `NaN` or `Inf` values.
        const eps_value = 1e-4;

        try testing.expect(approxEqAbs(comptime_float, 0.0, 0.0, eps_value));
        try testing.expect(approxEqAbs(comptime_float, -0.0, -0.0, eps_value));
        try testing.expect(approxEqAbs(comptime_float, 0.0, -0.0, eps_value));
        try testing.expect(!approxEqAbs(comptime_float, 1.0 + 2 * eps_value, 1.0, eps_value));
        try testing.expect(approxEqAbs(comptime_float, 1.0 + 1 * eps_value, 1.0, eps_value));
    }
}

test approxEqRel {
    inline for ([_]type{ f16, f32, f64, f128 }) |T| {
        const eps_value = comptime floatEps(T);
        const sqrt_eps_value = comptime sqrt(eps_value);
        const nan_value = comptime nan(T);
        const inf_value = comptime inf(T);
        const min_value = comptime floatMin(T);

        try testing.expect(approxEqRel(T, 1.0, 1.0, sqrt_eps_value));
        try testing.expect(!approxEqRel(T, 1.0, 0.0, sqrt_eps_value));
        try testing.expect(!approxEqRel(T, 1.0, nan_value, sqrt_eps_value));
        try testing.expect(!approxEqRel(T, nan_value, nan_value, sqrt_eps_value));
        try testing.expect(approxEqRel(T, inf_value, inf_value, sqrt_eps_value));
        try testing.expect(approxEqRel(T, min_value, min_value, sqrt_eps_value));
        try testing.expect(approxEqRel(T, -min_value, -min_value, sqrt_eps_value));
    }

    comptime {
        // `comptime_float` is guaranteed to have the same precision and operations of
        // the largest other floating point type, which is f128 but it doesn't have a
        // defined layout so we can't rely on `@bitCast` to construct the smallest
        // possible epsilon value like we do in the tests above. In the same vein, we
        // also can't represent a max/min, `NaN` or `Inf` values.
        const eps_value = 1e-4;
        const sqrt_eps_value = sqrt(eps_value);

        try testing.expect(approxEqRel(comptime_float, 1.0, 1.0, sqrt_eps_value));
        try testing.expect(!approxEqRel(comptime_float, 1.0, 0.0, sqrt_eps_value));
    }
}

pub fn raiseInvalid() void {
    // Raise INVALID fpu exception
}

pub fn raiseUnderflow() void {
    // Raise UNDERFLOW fpu exception
}

pub fn raiseOverflow() void {
    // Raise OVERFLOW fpu exception
}

pub fn raiseInexact() void {
    // Raise INEXACT fpu exception
}

pub fn raiseDivByZero() void {
    // Raise INEXACT fpu exception
}

pub const isNan = @import("math/isnan.zig").isNan;
pub const isSignalNan = @import("math/isnan.zig").isSignalNan;
pub const frexp = @import("math/frexp.zig").frexp;
pub const Frexp = @import("math/frexp.zig").Frexp;
pub const modf = @import("math/modf.zig").modf;
pub const Modf = @import("math/modf.zig").Modf;
pub const copysign = @import("math/copysign.zig").copysign;
pub const isFinite = @import("math/isfinite.zig").isFinite;
pub const isInf = @import("math/isinf.zig").isInf;
pub const isPositiveInf = @import("math/isinf.zig").isPositiveInf;
pub const isNegativeInf = @import("math/isinf.zig").isNegativeInf;
pub const isPositiveZero = @import("math/iszero.zig").isPositiveZero;
pub const isNegativeZero = @import("math/iszero.zig").isNegativeZero;
pub const isNormal = @import("math/isnormal.zig").isNormal;
pub const nextAfter = @import("math/nextafter.zig").nextAfter;
pub const signbit = @import("math/signbit.zig").signbit;
pub const scalbn = @import("math/scalbn.zig").scalbn;
pub const ldexp = @import("math/ldexp.zig").ldexp;
pub const pow = @import("math/pow.zig").pow;
pub const powi = @import("math/powi.zig").powi;
pub const sqrt = @import("math/sqrt.zig").sqrt;
pub const cbrt = @import("math/cbrt.zig").cbrt;
pub const acos = @import("math/acos.zig").acos;
pub const asin = @import("math/asin.zig").asin;
pub const atan = @import("math/atan.zig").atan;
pub const atan2 = @import("math/atan2.zig").atan2;
pub const hypot = @import("math/hypot.zig").hypot;
pub const expm1 = @import("math/expm1.zig").expm1;
pub const ilogb = @import("math/ilogb.zig").ilogb;
pub const log = @import("math/log.zig").log;
pub const log2 = @import("math/log2.zig").log2;
pub const log10 = @import("math/log10.zig").log10;
pub const log10_int = @import("math/log10.zig").log10_int;
pub const log_int = @import("math/log_int.zig").log_int;
pub const log1p = @import("math/log1p.zig").log1p;
pub const asinh = @import("math/asinh.zig").asinh;
pub const acosh = @import("math/acosh.zig").acosh;
pub const atanh = @import("math/atanh.zig").atanh;
pub const sinh = @import("math/sinh.zig").sinh;
pub const cosh = @import("math/cosh.zig").cosh;
pub const tanh = @import("math/tanh.zig").tanh;
pub const gcd = @import("math/gcd.zig").gcd;
pub const lcm = @import("math/lcm.zig").lcm;
pub const gamma = @import("math/gamma.zig").gamma;
pub const lgamma = @import("math/gamma.zig").lgamma;

/// Sine trigonometric function on a floating point number.
/// Uses a dedicated hardware instruction when available.
/// This is the same as calling the builtin @sin
pub inline fn sin(value: anytype) @TypeOf(value) {
    return @sin(value);
}

/// Cosine trigonometric function on a floating point number.
/// Uses a dedicated hardware instruction when available.
/// This is the same as calling the builtin @cos
pub inline fn cos(value: anytype) @TypeOf(value) {
    return @cos(value);
}

/// Tangent trigonometric function on a floating point number.
/// Uses a dedicated hardware instruction when available.
/// This is the same as calling the builtin @tan
pub inline fn tan(value: anytype) @TypeOf(value) {
    return @tan(value);
}

/// Converts an angle in radians to degrees. T must be a float or comptime number or a vector of floats.
pub fn radiansToDegrees(ang: anytype) if (@TypeOf(ang) == comptime_int) comptime_float else @TypeOf(ang) {
    const T = @TypeOf(ang);
    switch (@typeInfo(T)) {
        .float, .comptime_float, .comptime_int => return ang * deg_per_rad,
        .vector => |V| if (@typeInfo(V.child) == .float) return ang * @as(T, @splat(deg_per_rad)),
        else => {},
    }
    @compileError("Input must be float or a comptime number, or a vector of floats.");
}

test radiansToDegrees {
    const zero: f32 = 0;
    const half_pi: f32 = pi / 2.0;
    const neg_quart_pi: f32 = -pi / 4.0;
    const one_pi: f32 = pi;
    const two_pi: f32 = 2.0 * pi;
    try std.testing.expectApproxEqAbs(@as(f32, 0), radiansToDegrees(zero), 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, 90), radiansToDegrees(half_pi), 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, -45), radiansToDegrees(neg_quart_pi), 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, 180), radiansToDegrees(one_pi), 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, 360), radiansToDegrees(two_pi), 1e-6);

    const result = radiansToDegrees(@Vector(4, f32){
        half_pi,
        neg_quart_pi,
        one_pi,
        two_pi,
    });
    try std.testing.expectApproxEqAbs(@as(f32, 90), result[0], 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, -45), result[1], 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, 180), result[2], 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, 360), result[3], 1e-6);
}

/// Converts an angle in degrees to radians. T must be a float or comptime number or a vector of floats.
pub fn degreesToRadians(ang: anytype) if (@TypeOf(ang) == comptime_int) comptime_float else @TypeOf(ang) {
    const T = @TypeOf(ang);
    switch (@typeInfo(T)) {
        .float, .comptime_float, .comptime_int => return ang * rad_per_deg,
        .vector => |V| if (@typeInfo(V.child) == .float) return ang * @as(T, @splat(rad_per_deg)),
        else => {},
    }
    @compileError("Input must be float or a comptime number, or a vector of floats.");
}

test degreesToRadians {
    const ninety: f32 = 90;
    const neg_two_seventy: f32 = -270;
    const three_sixty: f32 = 360;
    try std.testing.expectApproxEqAbs(@as(f32, pi / 2.0), degreesToRadians(ninety), 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, -3 * pi / 2.0), degreesToRadians(neg_two_seventy), 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, 2 * pi), degreesToRadians(three_sixty), 1e-6);

    const result = degreesToRadians(@Vector(3, f32){
        ninety,
        neg_two_seventy,
        three_sixty,
    });
    try std.testing.expectApproxEqAbs(@as(f32, pi / 2.0), result[0], 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, -3 * pi / 2.0), result[1], 1e-6);
    try std.testing.expectApproxEqAbs(@as(f32, 2 * pi), result[2], 1e-6);
}

/// Base-e exponential function on a floating point number.
/// Uses a dedicated hardware instruction when available.
/// This is the same as calling the builtin @exp
pub inline fn exp(value: anytype) @TypeOf(value) {
    return @exp(value);
}

/// Base-2 exponential function on a floating point number.
/// Uses a dedicated hardware instruction when available.
/// This is the same as calling the builtin @exp2
pub inline fn exp2(value: anytype) @TypeOf(value) {
    return @exp2(value);
}

pub const complex = @import("math/complex.zig");
pub const Complex = complex.Complex;

pub const big = @import("math/big.zig");

test {
    _ = floatExponentBits;
    _ = floatMantissaBits;
    _ = floatFractionalBits;
    _ = floatExponentMin;
    _ = floatExponentMax;
    _ = floatTrueMin;
    _ = floatMin;
    _ = floatMax;
    _ = floatEps;
    _ = inf;
    _ = nan;
    _ = snan;
    _ = isNan;
    _ = isSignalNan;
    _ = frexp;
    _ = Frexp;
    _ = modf;
    _ = Modf;
    _ = copysign;
    _ = isFinite;
    _ = isInf;
    _ = isPositiveInf;
    _ = isNegativeInf;
    _ = isNormal;
    _ = nextAfter;
    _ = signbit;
    _ = scalbn;
    _ = ldexp;
    _ = pow;
    _ = powi;
    _ = sqrt;
    _ = cbrt;
    _ = acos;
    _ = asin;
    _ = atan;
    _ = atan2;
    _ = hypot;
    _ = expm1;
    _ = ilogb;
    _ = log;
    _ = log2;
    _ = log10;
    _ = log10_int;
    _ = log_int;
    _ = log1p;
    _ = asinh;
    _ = acosh;
    _ = atanh;
    _ = sinh;
    _ = cosh;
    _ = tanh;
    _ = gcd;
    _ = lcm;
    _ = gamma;
    _ = lgamma;

    _ = complex;
    _ = Complex;

    _ = big;
}

/// Given two types, returns the smallest one which is capable of holding the
/// full range of the minimum value.
pub fn Min(comptime A: type, comptime B: type) type {
    switch (@typeInfo(A)) {
        .int => |a_info| switch (@typeInfo(B)) {
            .int => |b_info| if (a_info.signedness == .unsigned and b_info.signedness == .unsigned) {
                if (a_info.bits < b_info.bits) {
                    return A;
                } else {
                    return B;
                }
            },
            else => {},
        },
        else => {},
    }
    return @TypeOf(@as(A, 0) + @as(B, 0));
}

/// Odd sawtooth function
/// ```
///         |
///      /  | /    /
///     /   |/    /
///  --/----/----/--
///   /    /|   /
///  /    / |  /
///         |
/// ```
/// Limit x to the half-open interval [-r, r).
pub fn wrap(x: anytype, r: anytype) @TypeOf(x) {
    const info_x = @typeInfo(@TypeOf(x));
    const info_r = @typeInfo(@TypeOf(r));
    if (info_x == .int and info_x.int.signedness != .signed) {
        @compileError("x must be floating point, comptime integer, or signed integer.");
    }
    switch (info_r) {
        .int => {
            // in the rare usecase of r not being comptime_int or float,
            // take the penalty of having an intermediary type conversion,
            // otherwise the alternative is to unwind iteratively to avoid overflow
            const R = @Int(.signed, info_r.int.bits + 1);
            const radius: if (info_r.int.signedness == .signed) @TypeOf(r) else R = r;
            return @intCast(@mod(x - radius, 2 * @as(R, r)) - r); // provably impossible to overflow
        },
        else => {
            return @mod(x - r, 2 * r) - r;
        },
    }
}
test wrap {
    // Within range
    try testing.expect(wrap(@as(i32, -75), @as(i32, 180)) == -75);
    try testing.expect(wrap(@as(i32, -75), @as(i32, -180)) == -75);
    // Below
    try testing.expect(wrap(@as(i32, -225), @as(i32, 180)) == 135);
    try testing.expect(wrap(@as(i32, -225), @as(i32, -180)) == 135);
    // Above
    try testing.expect(wrap(@as(i32, 361), @as(i32, 180)) == 1);
    try testing.expect(wrap(@as(i32, 361), @as(i32, -180)) == 1);

    // One period, right limit, positive r
    try testing.expect(wrap(@as(i32, 180), @as(i32, 180)) == -180);
    // One period, left limit, positive r
    try testing.expect(wrap(@as(i32, -180), @as(i32, 180)) == -180);
    // One period, right limit, negative r
    try testing.expect(wrap(@as(i32, 180), @as(i32, -180)) == 180);
    // One period, left limit, negative r
    try testing.expect(wrap(@as(i32, -180), @as(i32, -180)) == 180);

    // Two periods, right limit, positive r
    try testing.expect(wrap(@as(i32, 540), @as(i32, 180)) == -180);
    // Two periods, left limit, positive r
    try testing.expect(wrap(@as(i32, -540), @as(i32, 180)) == -180);
    // Two periods, right limit, negative r
    try testing.expect(wrap(@as(i32, 540), @as(i32, -180)) == 180);
    // Two periods, left limit, negative r
    try testing.expect(wrap(@as(i32, -540), @as(i32, -180)) == 180);

    // Floating point
    try testing.expect(wrap(@as(f32, 1.125), @as(f32, 1.0)) == -0.875);
    try testing.expect(wrap(@as(f32, -127.5), @as(f32, 180)) == -127.5);

    // Mix of comptime and non-comptime
    var i: i32 = 1;
    _ = &i;
    try testing.expect(wrap(i, 10) == 1);

    const limit: i32 = 180;
    // Within range
    try testing.expect(wrap(@as(i32, -75), limit) == -75);
    // Below
    try testing.expect(wrap(@as(i32, -225), limit) == 135);
    // Above
    try testing.expect(wrap(@as(i32, 361), limit) == 1);
}

/// Odd ramp function
/// ```
///         |  _____
///         | /
///         |/
///  -------/-------
///        /|
///  _____/ |
///         |
/// ```
/// Limit val to the inclusive range [lower, upper].
pub fn clamp(val: anytype, lower: anytype, upper: anytype) @TypeOf(val, lower, upper) {
    const T = @TypeOf(val, lower, upper);
    switch (@typeInfo(T)) {
        .int, .float, .comptime_int, .comptime_float => assert(lower <= upper),
        .vector => |vinfo| switch (@typeInfo(vinfo.child)) {
            .int, .float => assert(@reduce(.And, lower <= upper)),
            else => @compileError("Expected vector of ints or floats, found " ++ @typeName(T)),
        },
        else => @compileError("Expected an int, float or vector of one, found " ++ @typeName(T)),
    }
    return @max(lower, @min(val, upper));
}
test clamp {
    // Within range
    try testing.expect(std.math.clamp(@as(i32, -1), @as(i32, -4), @as(i32, 7)) == -1);
    // Below
    try testing.expect(std.math.clamp(@as(i32, -5), @as(i32, -4), @as(i32, 7)) == -4);
    // Above
    try testing.expect(std.math.clamp(@as(i32, 8), @as(i32, -4), @as(i32, 7)) == 7);

    // Floating point
    try testing.expect(std.math.clamp(@as(f32, 1.1), @as(f32, 0.0), @as(f32, 1.0)) == 1.0);
    try testing.expect(std.math.clamp(@as(f32, -127.5), @as(f32, -200), @as(f32, -100)) == -127.5);

    // Vector
    try testing.expect(@reduce(.And, std.math.clamp(@as(@Vector(3, f32), .{ 1.4, 15.23, 28.3 }), @as(@Vector(3, f32), .{ 9.8, 13.2, 15.6 }), @as(@Vector(3, f32), .{ 15.2, 22.8, 26.3 })) == @as(@Vector(3, f32), .{ 9.8, 15.23, 26.3 })));

    // Mix of comptime and non-comptime
    var i: i32 = 1;
    _ = &i;
    try testing.expect(std.math.clamp(i, 0, 1) == 1);
}

/// Returns the product of a and b. Returns an error on overflow.
pub fn mul(comptime T: type, a: T, b: T) (error{Overflow}!T) {
    if (T == comptime_int) return a * b;
    const ov = @mulWithOverflow(a, b);
    if (ov[1] != 0) return error.Overflow;
    return ov[0];
}

/// Returns the sum of a and b. Returns an error on overflow.
pub fn add(comptime T: type, a: T, b: T) (error{Overflow}!T) {
    if (T == comptime_int) return a + b;
    const ov = @addWithOverflow(a, b);
    if (ov[1] != 0) return error.Overflow;
    return ov[0];
}

/// Returns a - b, or an error on overflow.
pub fn sub(comptime T: type, a: T, b: T) (error{Overflow}!T) {
    if (T == comptime_int) return a - b;
    const ov = @subWithOverflow(a, b);
    if (ov[1] != 0) return error.Overflow;
    return ov[0];
}

pub fn negate(x: anytype) !@TypeOf(x) {
    return sub(@TypeOf(x), 0, x);
}

/// Shifts a left by shift_amt. Returns an error on overflow. shift_amt
/// is unsigned.
pub fn shlExact(comptime T: type, a: T, shift_amt: Log2Int(T)) !T {
    if (T == comptime_int) return a << shift_amt;
    const ov = @shlWithOverflow(a, shift_amt);
    if (ov[1] != 0) return error.Overflow;
    return ov[0];
}

/// Shifts left. Overflowed bits are truncated.
/// A negative shift amount results in a right shift.
pub fn shl(comptime T: type, a: T, shift_amt: anytype) T {
    const is_shl = shift_amt >= 0;
    const abs_shift_amt = @abs(shift_amt);
    const casted_shift_amt = casted_shift_amt: switch (@typeInfo(T)) {
        .int => |info| {
            if (abs_shift_amt < info.bits) break :casted_shift_amt @as(
                Log2Int(T),
                @intCast(abs_shift_amt),
            );
            if (info.signedness == .unsigned or is_shl) return 0;
            return a >> (info.bits - 1);
        },
        .vector => |info| {
            const Child = info.child;
            const child_info = @typeInfo(Child).int;
            if (abs_shift_amt < child_info.bits) break :casted_shift_amt @as(
                @Vector(info.len, Log2Int(Child)),
                @splat(@as(Log2Int(Child), @intCast(abs_shift_amt))),
            );
            if (child_info.signedness == .unsigned or is_shl) return @splat(0);
            return a >> @splat(child_info.bits - 1);
        },
        else => comptime unreachable,
    };
    return if (is_shl) a << casted_shift_amt else a >> casted_shift_amt;
}

test shl {
    try testing.expect(shl(u8, 0b11111111, @as(usize, 3)) == 0b11111000);
    try testing.expect(shl(u8, 0b11111111, @as(usize, 8)) == 0);
    try testing.expect(shl(u8, 0b11111111, @as(usize, 9)) == 0);
    try testing.expect(shl(u8, 0b11111111, @as(isize, -2)) == 0b00111111);
    try testing.expect(shl(u8, 0b11111111, 3) == 0b11111000);
    try testing.expect(shl(u8, 0b11111111, 8) == 0);
    try testing.expect(shl(u8, 0b11111111, 9) == 0);
    try testing.expect(shl(u8, 0b11111111, -2) == 0b00111111);
    try testing.expect(shl(@Vector(1, u32), @Vector(1, u32){42}, @as(usize, 1))[0] == @as(u32, 42) << 1);
    try testing.expect(shl(@Vector(1, u32), @Vector(1, u32){42}, @as(isize, -1))[0] == @as(u32, 42) >> 1);
    try testing.expect(shl(@Vector(1, u32), @Vector(1, u32){42}, 33)[0] == 0);

    try testing.expect(shl(i8, -1, -100) == -1);
    try testing.expect(shl(i8, -1, 100) == 0);
    if (builtin.cpu.arch == .hexagon and builtin.zig_backend == .stage2_llvm) return error.SkipZigTest;
    try testing.expect(@reduce(.And, shl(@Vector(2, i8), .{ -1, 1 }, -100) == @Vector(2, i8){ -1, 0 }));
    try testing.expect(@reduce(.And, shl(@Vector(2, i8), .{ -1, 1 }, 100) == @Vector(2, i8){ 0, 0 }));
}

/// Shifts right. Overflowed bits are truncated.
/// A negative shift amount results in a left shift.
pub fn shr(comptime T: type, a: T, shift_amt: anytype) T {
    const is_shl = shift_amt < 0;
    const abs_shift_amt = @abs(shift_amt);
    const casted_shift_amt = casted_shift_amt: switch (@typeInfo(T)) {
        .int => |info| {
            if (abs_shift_amt < info.bits) break :casted_shift_amt @as(
                Log2Int(T),
                @intCast(abs_shift_amt),
            );
            if (info.signedness == .unsigned or is_shl) return 0;
            return a >> (info.bits - 1);
        },
        .vector => |info| {
            const Child = info.child;
            const child_info = @typeInfo(Child).int;
            if (abs_shift_amt < child_info.bits) break :casted_shift_amt @as(
                @Vector(info.len, Log2Int(Child)),
                @splat(@as(Log2Int(Child), @intCast(abs_shift_amt))),
            );
            if (child_info.signedness == .unsigned or is_shl) return @splat(0);
            return a >> @splat(child_info.bits - 1);
        },
        else => comptime unreachable,
    };
    return if (is_shl) a << casted_shift_amt else a >> casted_shift_amt;
}

test shr {
    try testing.expect(shr(u8, 0b11111111, @as(usize, 3)) == 0b00011111);
    try testing.expect(shr(u8, 0b11111111, @as(usize, 8)) == 0);
    try testing.expect(shr(u8, 0b11111111, @as(usize, 9)) == 0);
    try testing.expect(shr(u8, 0b11111111, @as(isize, -2)) == 0b11111100);
    try testing.expect(shr(u8, 0b11111111, 3) == 0b00011111);
    try testing.expect(shr(u8, 0b11111111, 8) == 0);
    try testing.expect(shr(u8, 0b11111111, 9) == 0);
    try testing.expect(shr(u8, 0b11111111, -2) == 0b11111100);
    try testing.expect(shr(@Vector(1, u32), @Vector(1, u32){42}, @as(usize, 1))[0] == @as(u32, 42) >> 1);
    try testing.expect(shr(@Vector(1, u32), @Vector(1, u32){42}, @as(isize, -1))[0] == @as(u32, 42) << 1);
    try testing.expect(shr(@Vector(1, u32), @Vector(1, u32){42}, 33)[0] == 0);

    try testing.expect(shr(i8, -1, -100) == 0);
    try testing.expect(shr(i8, -1, 100) == -1);
    if (builtin.cpu.arch == .hexagon and builtin.zig_backend == .stage2_llvm) return error.SkipZigTest;
    try testing.expect(@reduce(.And, shr(@Vector(2, i8), .{ -1, 1 }, -100) == @Vector(2, i8){ 0, 0 }));
    try testing.expect(@reduce(.And, shr(@Vector(2, i8), .{ -1, 1 }, 100) == @Vector(2, i8){ -1, 0 }));
}

/// Rotates right. Only unsigned values can be rotated.  Negative shift
/// values result in shift modulo the bit count.
pub fn rotr(comptime T: type, x: T, r: anytype) T {
    if (@typeInfo(T) == .vector) {
        const C = @typeInfo(T).vector.child;
        if (C == u0) return @splat(0);

        if (@typeInfo(C).int.signedness == .signed) {
            @compileError("cannot rotate signed integers");
        }
        const ar: Log2Int(C) = @intCast(@mod(r, @typeInfo(C).int.bits));
        return (x >> @splat(ar)) | (x << @splat(1 + ~ar));
    } else if (@typeInfo(T).int.signedness == .signed) {
        @compileError("cannot rotate signed integer");
    } else {
        if (T == u0) return 0;

        if (comptime isPowerOfTwo(@typeInfo(T).int.bits)) {
            const ar: Log2Int(T) = @intCast(@mod(r, @typeInfo(T).int.bits));
            return x >> ar | x << (1 +% ~ar);
        } else {
            const ar = @mod(r, @typeInfo(T).int.bits);
            return shr(T, x, ar) | shl(T, x, @typeInfo(T).int.bits - ar);
        }
    }
}

test rotr {
    try testing.expect(rotr(u0, 0b0, @as(usize, 3)) == 0b0);
    try testing.expect(rotr(u5, 0b00001, @as(usize, 0)) == 0b00001);
    try testing.expect(rotr(u6, 0b000001, @as(usize, 7)) == 0b100000);
    try testing.expect(rotr(u8, 0b00000001, @as(usize, 0)) == 0b00000001);
    try testing.expect(rotr(u8, 0b00000001, @as(usize, 9)) == 0b10000000);
    try testing.expect(rotr(u8, 0b00000001, @as(usize, 8)) == 0b00000001);
    try testing.expect(rotr(u8, 0b00000001, @as(usize, 4)) == 0b00010000);
    try testing.expect(rotr(u8, 0b00000001, @as(isize, -1)) == 0b00000010);
    try testing.expect(rotr(u12, 0o7777, 1) == 0o7777);
    try testing.expect(rotr(@Vector(1, u32), .{1}, @as(usize, 1))[0] == @as(u32, 1) << 31);
    try testing.expect(rotr(@Vector(1, u32), .{1}, @as(isize, -1))[0] == @as(u32, 1) << 1);
    try std.testing.expect(@reduce(.And, rotr(@Vector(2, u0), .{ 0, 0 }, @as(usize, 42)) ==
        @Vector(2, u0){ 0, 0 }));
}

/// Rotates left. Only unsigned values can be rotated.  Negative shift
/// values result in shift modulo the bit count.
pub fn rotl(comptime T: type, x: T, r: anytype) T {
    if (@typeInfo(T) == .vector) {
        const C = @typeInfo(T).vector.child;
        if (C == u0) return @splat(0);

        if (@typeInfo(C).int.signedness == .signed) {
            @compileError("cannot rotate signed integers");
        }
        const ar: Log2Int(C) = @intCast(@mod(r, @typeInfo(C).int.bits));
        return (x << @splat(ar)) | (x >> @splat(1 +% ~ar));
    } else if (@typeInfo(T).int.signedness == .signed) {
        @compileError("cannot rotate signed integer");
    } else {
        if (T == u0) return 0;

        if (comptime isPowerOfTwo(@typeInfo(T).int.bits)) {
            const ar: Log2Int(T) = @intCast(@mod(r, @typeInfo(T).int.bits));
            return x << ar | x >> 1 +% ~ar;
        } else {
            const ar = @mod(r, @typeInfo(T).int.bits);
            return shl(T, x, ar) | shr(T, x, @typeInfo(T).int.bits - ar);
        }
    }
}

test rotl {
    try testing.expect(rotl(u0, 0b0, @as(usize, 3)) == 0b0);
    try testing.expect(rotl(u5, 0b00001, @as(usize, 0)) == 0b00001);
    try testing.expect(rotl(u6, 0b000001, @as(usize, 7)) == 0b000010);
    try testing.expect(rotl(u8, 0b00000001, @as(usize, 0)) == 0b00000001);
    try testing.expect(rotl(u8, 0b00000001, @as(usize, 9)) == 0b00000010);
    try testing.expect(rotl(u8, 0b00000001, @as(usize, 8)) == 0b00000001);
    try testing.expect(rotl(u8, 0b00000001, @as(usize, 4)) == 0b00010000);
    try testing.expect(rotl(u8, 0b00000001, @as(isize, -1)) == 0b10000000);
    try testing.expect(rotl(u12, 0o7777, 1) == 0o7777);
    try testing.expect(rotl(@Vector(1, u32), .{1 << 31}, @as(usize, 1))[0] == 1);
    try testing.expect(rotl(@Vector(1, u32), .{1 << 31}, @as(isize, -1))[0] == @as(u32, 1) << 30);
    try std.testing.expect(@reduce(.And, rotl(@Vector(2, u0), .{ 0, 0 }, @as(usize, 42)) ==
        @Vector(2, u0){ 0, 0 }));
}

/// Returns an unsigned int type that can hold the number of bits in T - 1.
/// Suitable for 0-based bit indices of T.
pub fn Log2Int(comptime T: type) type {
    // comptime ceil log2
    if (T == comptime_int) return comptime_int;
    const bits: u16 = @typeInfo(T).int.bits;
    const log2_bits = 16 - @clz(bits - 1);
    return std.meta.Int(.unsigned, log2_bits);
}

/// Returns an unsigned int type that can hold the number of bits in T.
pub fn Log2IntCeil(comptime T: type) type {
    // comptime ceil log2
    if (T == comptime_int) return comptime_int;
    const bits: u16 = @typeInfo(T).int.bits;
    const log2_bits = 16 - @clz(bits);
    return std.meta.Int(.unsigned, log2_bits);
}

/// Returns the smallest integer type that can hold both from and to.
pub fn IntFittingRange(comptime from: comptime_int, comptime to: comptime_int) type {
    assert(from <= to);
    const signedness: std.builtin.Signedness = if (from < 0) .signed else .unsigned;
    return @Int(
        signedness,
        @as(u16, @intFromBool(signedness == .signed)) +
            switch (if (from < 0) @max(@abs(from) - 1, to) else to) {
                0 => 0,
                else => |pos_max| 1 + log2(pos_max),
            },
    );
}

test IntFittingRange {
    try testing.expect(IntFittingRange(0, 0) == u0);
    try testing.expect(IntFittingRange(0, 1) == u1);
    try testing.expect(IntFittingRange(0, 2) == u2);
    try testing.expect(IntFittingRange(0, 3) == u2);
    try testing.expect(IntFittingRange(0, 4) == u3);
    try testing.expect(IntFittingRange(0, 7) == u3);
    try testing.expect(IntFittingRange(0, 8) == u4);
    try testing.expect(IntFittingRange(0, 9) == u4);
    try testing.expect(IntFittingRange(0, 15) == u4);
    try testing.expect(IntFittingRange(0, 16) == u5);
    try testing.expect(IntFittingRange(0, 17) == u5);
    try testing.expect(IntFittingRange(0, 4095) == u12);
    try testing.expect(IntFittingRange(2000, 4095) == u12);
    try testing.expect(IntFittingRange(0, 4096) == u13);
    try testing.expect(IntFittingRange(2000, 4096) == u13);
    try testing.expect(IntFittingRange(0, 4097) == u13);
    try testing.expect(IntFittingRange(2000, 4097) == u13);
    try testing.expect(IntFittingRange(0, 123456789123456798123456789) == u87);
    try testing.expect(IntFittingRange(0, 123456789123456798123456789123456789123456798123456789) == u177);

    try testing.expect(IntFittingRange(-1, -1) == i1);
    try testing.expect(IntFittingRange(-1, 0) == i1);
    try testing.expect(IntFittingRange(-1, 1) == i2);
    try testing.expect(IntFittingRange(-2, -2) == i2);
    try testing.expect(IntFittingRange(-2, -1) == i2);
    try testing.expect(IntFittingRange(-2, 0) == i2);
    try testing.expect(IntFittingRange(-2, 1) == i2);
    try testing.expect(IntFittingRange(-2, 2) == i3);
    try testing.expect(IntFittingRange(-1, 2) == i3);
    try testing.expect(IntFittingRange(-1, 3) == i3);
    try testing.expect(IntFittingRange(-1, 4) == i4);
    try testing.expect(IntFittingRange(-1, 7) == i4);
    try testing.expect(IntFittingRange(-1, 8) == i5);
    try testing.expect(IntFittingRange(-1, 9) == i5);
    try testing.expect(IntFittingRange(-1, 15) == i5);
    try testing.expect(IntFittingRange(-1, 16) == i6);
    try testing.expect(IntFittingRange(-1, 17) == i6);
    try testing.expect(IntFittingRange(-1, 4095) == i13);
    try testing.expect(IntFittingRange(-4096, 4095) == i13);
    try testing.expect(IntFittingRange(-1, 4096) == i14);
    try testing.expect(IntFittingRange(-4097, 4095) == i14);
    try testing.expect(IntFittingRange(-1, 4097) == i14);
    try testing.expect(IntFittingRange(-1, 123456789123456798123456789) == i88);
    try testing.expect(IntFittingRange(-1, 123456789123456798123456789123456789123456798123456789) == i178);
}

test "overflow functions" {
    try testOverflow();
    try comptime testOverflow();
}

fn testOverflow() !void {
    try testing.expect((mul(i32, 3, 4) catch unreachable) == 12);
    try testing.expect((add(i32, 3, 4) catch unreachable) == 7);
    try testing.expect((sub(i32, 3, 4) catch unreachable) == -1);
    try testing.expect((shlExact(i32, 0b11, 4) catch unreachable) == 0b110000);
}

/// Divide numerator by denominator, rounding toward zero. Returns an
/// error on overflow or when denominator is zero.
pub fn divTrunc(comptime T: type, numerator: T, denominator: T) !T {
    @setRuntimeSafety(false);
    if (denominator == 0) return error.DivisionByZero;
    if (@typeInfo(T) == .int and @typeInfo(T).int.signedness == .signed and numerator == minInt(T) and denominator == -1) return error.Overflow;
    return @divTrunc(numerator, denominator);
}

test divTrunc {
    try testDivTrunc();
    try comptime testDivTrunc();
}
fn testDivTrunc() !void {
    try testing.expect((divTrunc(i32, 5, 3) catch unreachable) == 1);
    try testing.expect((divTrunc(i32, -5, 3) catch unreachable) == -1);
    try testing.expectError(error.DivisionByZero, divTrunc(i8, -5, 0));
    try testing.expectError(error.Overflow, divTrunc(i8, -128, -1));

    try testing.expect((divTrunc(f32, 5.0, 3.0) catch unreachable) == 1.0);
    try testing.expect((divTrunc(f32, -5.0, 3.0) catch unreachable) == -1.0);
}

/// Divide numerator by denominator, rounding toward negative
/// infinity. Returns an error on overflow or when denominator is
/// zero.
pub fn divFloor(comptime T: type, numerator: T, denominator: T) !T {
    @setRuntimeSafety(false);
    if (denominator == 0) return error.DivisionByZero;
    if (@typeInfo(T) == .int and @typeInfo(T).int.signedness == .signed and numerator == minInt(T) and denominator == -1) return error.Overflow;
    return @divFloor(numerator, denominator);
}

test divFloor {
    try testDivFloor();
    try comptime testDivFloor();
}
fn testDivFloor() !void {
    try testing.expect((divFloor(i32, 5, 3) catch unreachable) == 1);
    try testing.expect((divFloor(i32, -5, 3) catch unreachable) == -2);
    try testing.expectError(error.DivisionByZero, divFloor(i8, -5, 0));
    try testing.expectError(error.Overflow, divFloor(i8, -128, -1));

    try testing.expect((divFloor(f32, 5.0, 3.0) catch unreachable) == 1.0);
    try testing.expect((divFloor(f32, -5.0, 3.0) catch unreachable) == -2.0);
}

/// Divide numerator by denominator, rounding toward positive
/// infinity. Returns an error on overflow or when denominator is
/// zero.
pub fn divCeil(comptime T: type, numerator: T, denominator: T) !T {
    @setRuntimeSafety(false);
    if (denominator == 0) return error.DivisionByZero;
    const info = @typeInfo(T);
    switch (info) {
        .comptime_float, .float => return @ceil(numerator / denominator),
        .comptime_int, .int => {
            if (numerator < 0 and denominator < 0) {
                if (info == .int and numerator == minInt(T) and denominator == -1)
                    return error.Overflow;
                return @divFloor(numerator + 1, denominator) + 1;
            }
            if (numerator > 0 and denominator > 0)
                return @divFloor(numerator - 1, denominator) + 1;
            return @divTrunc(numerator, denominator);
        },
        else => @compileError("divCeil unsupported on " ++ @typeName(T)),
    }
}

test divCeil {
    try testDivCeil();
    try comptime testDivCeil();
}
fn testDivCeil() !void {
    try testing.expectEqual(@as(i32, 2), divCeil(i32, 5, 3) catch unreachable);
    try testing.expectEqual(@as(i32, -1), divCeil(i32, -5, 3) catch unreachable);
    try testing.expectEqual(@as(i32, -1), divCeil(i32, 5, -3) catch unreachable);
    try testing.expectEqual(@as(i32, 2), divCeil(i32, -5, -3) catch unreachable);
    try testing.expectEqual(@as(i32, 0), divCeil(i32, 0, 5) catch unreachable);
    try testing.expectEqual(@as(u32, 0), divCeil(u32, 0, 5) catch unreachable);
    try testing.expectError(error.DivisionByZero, divCeil(i8, -5, 0));
    try testing.expectError(error.Overflow, divCeil(i8, -128, -1));

    try testing.expectEqual(@as(f32, 0.0), divCeil(f32, 0.0, 5.0) catch unreachable);
    try testing.expectEqual(@as(f32, 2.0), divCeil(f32, 5.0, 3.0) catch unreachable);
    try testing.expectEqual(@as(f32, -1.0), divCeil(f32, -5.0, 3.0) catch unreachable);
    try testing.expectEqual(@as(f32, -1.0), divCeil(f32, 5.0, -3.0) catch unreachable);
    try testing.expectEqual(@as(f32, 2.0), divCeil(f32, -5.0, -3.0) catch unreachable);

    try testing.expectEqual(6, divCeil(comptime_int, 23, 4) catch unreachable);
    try testing.expectEqual(-5, divCeil(comptime_int, -23, 4) catch unreachable);
    try testing.expectEqual(-5, divCeil(comptime_int, 23, -4) catch unreachable);
    try testing.expectEqual(6, divCeil(comptime_int, -23, -4) catch unreachable);
    try testing.expectError(error.DivisionByZero, divCeil(comptime_int, 23, 0));

    try testing.expectEqual(6.0, divCeil(comptime_float, 23.0, 4.0) catch unreachable);
    try testing.expectEqual(-5.0, divCeil(comptime_float, -23.0, 4.0) catch unreachable);
    try testing.expectEqual(-5.0, divCeil(comptime_float, 23.0, -4.0) catch unreachable);
    try testing.expectEqual(6.0, divCeil(comptime_float, -23.0, -4.0) catch unreachable);
    try testing.expectError(error.DivisionByZero, divCeil(comptime_float, 23.0, 0.0));
}

/// Divide numerator by denominator. Return an error if quotient is
/// not an integer, denominator is zero, or on overflow.
pub fn divExact(comptime T: type, numerator: T, denominator: T) !T {
    @setRuntimeSafety(false);
    if (denominator == 0) return error.DivisionByZero;
    if (@typeInfo(T) == .int and @typeInfo(T).int.signedness == .signed and numerator == minInt(T) and denominator == -1) return error.Overflow;
    const result = @divTrunc(numerator, denominator);
    if (result * denominator != numerator) return error.UnexpectedRemainder;
    return result;
}

test divExact {
    try testDivExact();
    try comptime testDivExact();
}
fn testDivExact() !void {
    try tes
```
