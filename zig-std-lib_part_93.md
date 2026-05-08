```
PROTO = struct {
    pub const IP = 0;
    pub const HOPOPTS = 0;
    pub const ICMP = 1;
    pub const IGMP = 2;
    pub const IPIP = 4;
    pub const TCP = 6;
    pub const EGP = 8;
    pub const PUP = 12;
    pub const UDP = 17;
    pub const IDP = 22;
    pub const TP = 29;
    pub const DCCP = 33;
    pub const IPV6 = 41;
    pub const ROUTING = 43;
    pub const FRAGMENT = 44;
    pub const RSVP = 46;
    pub const GRE = 47;
    pub const ESP = 50;
    pub const AH = 51;
    pub const ICMPV6 = 58;
    pub const NONE = 59;
    pub const DSTOPTS = 60;
    pub const MTP = 92;
    pub const BEETPH = 94;
    pub const ENCAP = 98;
    pub const PIM = 103;
    pub const COMP = 108;
    pub const SCTP = 132;
    pub const MH = 135;
    pub const UDPLITE = 136;
    pub const MPLS = 137;
    pub const RAW = 255;
    pub const MAX = 256;
};

pub const tcp_repair_opt = extern struct {
    opt_code: u32,
    opt_val: u32,
};

pub const tcp_repair_window = extern struct {
    snd_wl1: u32,
    snd_wnd: u32,
    max_window: u32,
    rcv_wnd: u32,
    rcv_wup: u32,
};

pub const TcpRepairOption = enum {
    TCP_NO_QUEUE,
    TCP_RECV_QUEUE,
    TCP_SEND_QUEUE,
    TCP_QUEUES_NR,
};

/// why fastopen failed from client perspective
pub const tcp_fastopen_client_fail = enum {
    /// catch-all
    TFO_STATUS_UNSPEC,
    /// if not in TFO_CLIENT_NO_COOKIE mode
    TFO_COOKIE_UNAVAILABLE,
    /// SYN-ACK did not ack SYN data
    TFO_DATA_NOT_ACKED,
    /// SYN-ACK did not ack SYN data after timeout
    TFO_SYN_RETRANSMITTED,
};

/// for TCP_INFO socket option
pub const TCPI_OPT_TIMESTAMPS = 1;
pub const TCPI_OPT_SACK = 2;
pub const TCPI_OPT_WSCALE = 4;
/// ECN was negotiated at TCP session init
pub const TCPI_OPT_ECN = 8;
/// we received at least one packet with ECT
pub const TCPI_OPT_ECN_SEEN = 16;
/// SYN-ACK acked data in SYN sent or rcvd
pub const TCPI_OPT_SYN_DATA = 32;

pub const nfds_t = usize;
pub const pollfd = extern struct {
    fd: fd_t,
    events: i16,
    revents: i16,
};

pub const POLL = struct {
    pub const IN = 0x001;
    pub const PRI = 0x002;
    pub const OUT = 0x004;
    pub const ERR = 0x008;
    pub const HUP = 0x010;
    pub const NVAL = 0x020;
    pub const RDNORM = 0x040;
    pub const RDBAND = 0x080;
};

pub const HUGETLB_FLAG_ENCODE_SHIFT = 26;
pub const HUGETLB_FLAG_ENCODE_MASK = 0x3f;
pub const HUGETLB_FLAG_ENCODE_64KB = 16 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_512KB = 19 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_1MB = 20 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_2MB = 21 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_8MB = 23 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_16MB = 24 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_32MB = 25 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_256MB = 28 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_512MB = 29 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_1GB = 30 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_2GB = 31 << HUGETLB_FLAG_ENCODE_SHIFT;
pub const HUGETLB_FLAG_ENCODE_16GB = 34 << HUGETLB_FLAG_ENCODE_SHIFT;

pub const MFD = struct {
    pub const CLOEXEC = 0x0001;
    pub const ALLOW_SEALING = 0x0002;
    pub const HUGETLB = 0x0004;
    pub const ALL_FLAGS = CLOEXEC | ALLOW_SEALING | HUGETLB;

    pub const HUGE_SHIFT = HUGETLB_FLAG_ENCODE_SHIFT;
    pub const HUGE_MASK = HUGETLB_FLAG_ENCODE_MASK;
    pub const HUGE_64KB = HUGETLB_FLAG_ENCODE_64KB;
    pub const HUGE_512KB = HUGETLB_FLAG_ENCODE_512KB;
    pub const HUGE_1MB = HUGETLB_FLAG_ENCODE_1MB;
    pub const HUGE_2MB = HUGETLB_FLAG_ENCODE_2MB;
    pub const HUGE_8MB = HUGETLB_FLAG_ENCODE_8MB;
    pub const HUGE_16MB = HUGETLB_FLAG_ENCODE_16MB;
    pub const HUGE_32MB = HUGETLB_FLAG_ENCODE_32MB;
    pub const HUGE_256MB = HUGETLB_FLAG_ENCODE_256MB;
    pub const HUGE_512MB = HUGETLB_FLAG_ENCODE_512MB;
    pub const HUGE_1GB = HUGETLB_FLAG_ENCODE_1GB;
    pub const HUGE_2GB = HUGETLB_FLAG_ENCODE_2GB;
    pub const HUGE_16GB = HUGETLB_FLAG_ENCODE_16GB;
};

pub const rusage = extern struct {
    utime: timeval,
    stime: timeval,
    maxrss: isize,
    ixrss: isize,
    idrss: isize,
    isrss: isize,
    minflt: isize,
    majflt: isize,
    nswap: isize,
    inblock: isize,
    oublock: isize,
    msgsnd: isize,
    msgrcv: isize,
    nsignals: isize,
    nvcsw: isize,
    nivcsw: isize,
    __reserved: [16]isize = [1]isize{0} ** 16,

    pub const SELF = 0;
    pub const CHILDREN = -1;
    pub const THREAD = 1;
};

pub const NCC = if (is_ppc) 10 else 8;
pub const NCCS = if (is_mips) 32 else if (is_ppc) 19 else if (is_sparc) 17 else 32;

pub const speed_t = if (is_ppc) enum(c_uint) {
    B0 = 0x0000000,
    B50 = 0x0000001,
    B75 = 0x0000002,
    B110 = 0x0000003,
    B134 = 0x0000004,
    B150 = 0x0000005,
    B200 = 0x0000006,
    B300 = 0x0000007,
    B600 = 0x0000008,
    B1200 = 0x0000009,
    B1800 = 0x000000a,
    B2400 = 0x000000b,
    B4800 = 0x000000c,
    B9600 = 0x000000d,
    B19200 = 0x000000e,
    B38400 = 0x000000f,

    B57600 = 0x00000010,
    B115200 = 0x00000011,
    B230400 = 0x00000012,
    B460800 = 0x00000013,
    B500000 = 0x00000014,
    B576000 = 0x00000015,
    B921600 = 0x00000016,
    B1000000 = 0x00000017,
    B1152000 = 0x00000018,
    B1500000 = 0x00000019,
    B2000000 = 0x0000001a,
    B2500000 = 0x0000001b,
    B3000000 = 0x0000001c,
    B3500000 = 0x0000001d,
    B4000000 = 0x0000001e,

    pub const EXTA = speed_t.B19200;
    pub const EXTB = speed_t.B38400;
} else if (is_sparc) enum(c_uint) {
    B0 = 0x0000000,
    B50 = 0x0000001,
    B75 = 0x0000002,
    B110 = 0x0000003,
    B134 = 0x0000004,
    B150 = 0x0000005,
    B200 = 0x0000006,
    B300 = 0x0000007,
    B600 = 0x0000008,
    B1200 = 0x0000009,
    B1800 = 0x000000a,
    B2400 = 0x000000b,
    B4800 = 0x000000c,
    B9600 = 0x000000d,
    B19200 = 0x000000e,
    B38400 = 0x000000f,

    B57600 = 0x00001001,
    B115200 = 0x00001002,
    B230400 = 0x00001003,
    B460800 = 0x00001004,
    B76800 = 0x00001005,
    B153600 = 0x00001006,
    B307200 = 0x00001007,
    B614400 = 0x00001008,
    B921600 = 0x00001009,
    B500000 = 0x0000100a,
    B576000 = 0x0000100b,
    B1000000 = 0x0000100c,
    B1152000 = 0x0000100d,
    B1500000 = 0x0000100e,
    B2000000 = 0x0000100f,

    pub const EXTA = speed_t.B19200;
    pub const EXTB = speed_t.B38400;
} else enum(c_uint) {
    B0 = 0x0000000,
    B50 = 0x0000001,
    B75 = 0x0000002,
    B110 = 0x0000003,
    B134 = 0x0000004,
    B150 = 0x0000005,
    B200 = 0x0000006,
    B300 = 0x0000007,
    B600 = 0x0000008,
    B1200 = 0x0000009,
    B1800 = 0x000000a,
    B2400 = 0x000000b,
    B4800 = 0x000000c,
    B9600 = 0x000000d,
    B19200 = 0x000000e,
    B38400 = 0x000000f,

    B57600 = 0x00001001,
    B115200 = 0x00001002,
    B230400 = 0x00001003,
    B460800 = 0x00001004,
    B500000 = 0x00001005,
    B576000 = 0x00001006,
    B921600 = 0x00001007,
    B1000000 = 0x00001008,
    B1152000 = 0x00001009,
    B1500000 = 0x0000100a,
    B2000000 = 0x0000100b,
    B2500000 = 0x0000100c,
    B3000000 = 0x0000100d,
    B3500000 = 0x0000100e,
    B4000000 = 0x0000100f,

    pub const EXTA = speed_t.B19200;
    pub const EXTB = speed_t.B38400;
};

pub const tcflag_t = if (native_arch == .sparc) c_ulong else c_uint;

pub const tc_iflag_t = if (is_ppc) packed struct(tcflag_t) {
    IGNBRK: bool = false,
    BRKINT: bool = false,
    IGNPAR: bool = false,
    PARMRK: bool = false,
    INPCK: bool = false,
    ISTRIP: bool = false,
    INLCR: bool = false,
    IGNCR: bool = false,
    ICRNL: bool = false,
    IXON: bool = false,
    IXOFF: bool = false,
    IXANY: bool = false,
    IUCLC: bool = false,
    IMAXBEL: bool = false,
    IUTF8: bool = false,
    _15: u17 = 0,
} else packed struct(tcflag_t) {
    IGNBRK: bool = false,
    BRKINT: bool = false,
    IGNPAR: bool = false,
    PARMRK: bool = false,
    INPCK: bool = false,
    ISTRIP: bool = false,
    INLCR: bool = false,
    IGNCR: bool = false,
    ICRNL: bool = false,
    IUCLC: bool = false,
    IXON: bool = false,
    IXANY: bool = false,
    IXOFF: bool = false,
    IMAXBEL: bool = false,
    IUTF8: bool = false,
    _15: u17 = 0,
};

pub const NLDLY = if (is_ppc) enum(u2) {
    NL0 = 0,
    NL1 = 1,
    NL2 = 2,
    NL3 = 3,
} else enum(u1) {
    NL0 = 0,
    NL1 = 1,
};

pub const CRDLY = enum(u2) {
    CR0 = 0,
    CR1 = 1,
    CR2 = 2,
    CR3 = 3,
};

pub const TABDLY = enum(u2) {
    TAB0 = 0,
    TAB1 = 1,
    TAB2 = 2,
    TAB3 = 3,

    pub const XTABS = TABDLY.TAB3;
};

pub const BSDLY = enum(u1) {
    BS0 = 0,
    BS1 = 1,
};

pub const VTDLY = enum(u1) {
    VT0 = 0,
    VT1 = 1,
};

pub const FFDLY = enum(u1) {
    FF0 = 0,
    FF1 = 1,
};

pub const tc_oflag_t = if (is_ppc) packed struct(tcflag_t) {
    OPOST: bool = false,
    ONLCR: bool = false,
    OLCUC: bool = false,
    OCRNL: bool = false,
    ONOCR: bool = false,
    ONLRET: bool = false,
    OFILL: bool = false,
    OFDEL: bool = false,
    NLDLY: NLDLY = .NL0,
    TABDLY: TABDLY = .TAB0,
    CRDLY: CRDLY = .CR0,
    FFDLY: FFDLY = .FF0,
    BSDLY: BSDLY = .BS0,
    VTDLY: VTDLY = .VT0,
    _17: u15 = 0,
} else if (is_sparc) packed struct(tcflag_t) {
    OPOST: bool = false,
    OLCUC: bool = false,
    ONLCR: bool = false,
    OCRNL: bool = false,
    ONOCR: bool = false,
    ONLRET: bool = false,
    OFILL: bool = false,
    OFDEL: bool = false,
    NLDLY: NLDLY = .NL0,
    CRDLY: CRDLY = .CR0,
    TABDLY: TABDLY = .TAB0,
    BSDLY: BSDLY = .BS0,
    VTDLY: VTDLY = .VT0,
    FFDLY: FFDLY = .FF0,
    PAGEOUT: bool = false,
    WRAP: bool = false,
    _18: u14 = 0,
} else packed struct(tcflag_t) {
    OPOST: bool = false,
    OLCUC: bool = false,
    ONLCR: bool = false,
    OCRNL: bool = false,
    ONOCR: bool = false,
    ONLRET: bool = false,
    OFILL: bool = false,
    OFDEL: bool = false,
    NLDLY: NLDLY = .NL0,
    CRDLY: CRDLY = .CR0,
    TABDLY: TABDLY = .TAB0,
    BSDLY: BSDLY = .BS0,
    VTDLY: VTDLY = .VT0,
    FFDLY: FFDLY = .FF0,
    _16: u16 = 0,
};

pub const CSIZE = enum(u2) {
    CS5 = 0,
    CS6 = 1,
    CS7 = 2,
    CS8 = 3,
};

pub const tc_cflag_t = if (is_ppc) packed struct(tcflag_t) {
    _0: u8 = 0,
    CSIZE: CSIZE = .CS5,
    CSTOPB: bool = false,
    CREAD: bool = false,
    PARENB: bool = false,
    PARODD: bool = false,
    HUPCL: bool = false,
    CLOCAL: bool = false,
    _16: u13 = 0,
    ADDRB: bool = false,
    CMSPAR: bool = false,
    CRTSCTS: bool = false,
} else packed struct(tcflag_t) {
    _0: u4 = 0,
    CSIZE: CSIZE = .CS5,
    CSTOPB: bool = false,
    CREAD: bool = false,
    PARENB: bool = false,
    PARODD: bool = false,
    HUPCL: bool = false,
    CLOCAL: bool = false,
    _12: u17 = 0,
    ADDRB: bool = false,
    CMSPAR: bool = false,
    CRTSCTS: bool = false,
};

pub const tc_lflag_t = if (is_mips) packed struct(tcflag_t) {
    ISIG: bool = false,
    ICANON: bool = false,
    XCASE: bool = false,
    ECHO: bool = false,
    ECHOE: bool = false,
    ECHOK: bool = false,
    ECHONL: bool = false,
    NOFLSH: bool = false,
    IEXTEN: bool = false,
    ECHOCTL: bool = false,
    ECHOPRT: bool = false,
    ECHOKE: bool = false,
    _12: u1 = 0,
    FLUSHO: bool = false,
    PENDIN: bool = false,
    TOSTOP: bool = false,
    EXTPROC: bool = false,
    _17: u15 = 0,
} else if (is_ppc) packed struct(tcflag_t) {
    ECHOKE: bool = false,
    ECHOE: bool = false,
    ECHOK: bool = false,
    ECHO: bool = false,
    ECHONL: bool = false,
    ECHOPRT: bool = false,
    ECHOCTL: bool = false,
    ISIG: bool = false,
    ICANON: bool = false,
    _9: u1 = 0,
    IEXTEN: bool = false,
    _11: u3 = 0,
    XCASE: bool = false,
    _15: u7 = 0,
    TOSTOP: bool = false,
    FLUSHO: bool = false,
    _24: u4 = 0,
    EXTPROC: bool = false,
    PENDIN: bool = false,
    _30: u1 = 0,
    NOFLSH: bool = false,
} else if (is_sparc) packed struct(tcflag_t) {
    ISIG: bool = false,
    ICANON: bool = false,
    XCASE: bool = false,
    ECHO: bool = false,
    ECHOE: bool = false,
    ECHOK: bool = false,
    ECHONL: bool = false,
    NOFLSH: bool = false,
    TOSTOP: bool = false,
    ECHOCTL: bool = false,
    ECHOPRT: bool = false,
    ECHOKE: bool = false,
    DEFECHO: bool = false,
    FLUSHO: bool = false,
    PENDIN: bool = false,
    IEXTEN: bool = false,
    EXTPROC: bool = false,
    _17: u15 = 0,
} else packed struct(tcflag_t) {
    ISIG: bool = false,
    ICANON: bool = false,
    XCASE: bool = false,
    ECHO: bool = false,
    ECHOE: bool = false,
    ECHOK: bool = false,
    ECHONL: bool = false,
    NOFLSH: bool = false,
    TOSTOP: bool = false,
    ECHOCTL: bool = false,
    ECHOPRT: bool = false,
    ECHOKE: bool = false,
    FLUSHO: bool = false,
    _13: u1 = 0,
    PENDIN: bool = false,
    IEXTEN: bool = false,
    EXTPROC: bool = false,
    _17: u15 = 0,
};

pub const cc_t = u8;

/// Indices into the `cc` array in the `termios` struct.
pub const V = if (is_mips) enum(u32) {
    INTR = 0,
    QUIT = 1,
    ERASE = 2,
    KILL = 3,
    MIN = 4,
    TIME = 5,
    EOL2 = 6,
    SWTC = 7,
    START = 8,
    STOP = 9,
    SUSP = 10,
    REPRINT = 12,
    DISCARD = 13,
    WERASE = 14,
    LNEXT = 15,
    EOF = 16,
    EOL = 17,
} else if (is_ppc) enum(u32) {
    INTR = 0,
    QUIT = 1,
    ERASE = 2,
    KILL = 3,
    EOF = 4,
    MIN = 5,
    EOL = 6,
    TIME = 7,
    EOL2 = 8,
    SWTC = 9,
    WERASE = 10,
    REPRINT = 11,
    SUSP = 12,
    START = 13,
    STOP = 14,
    LNEXT = 15,
    DISCARD = 16,
} else enum(u32) {
    INTR = 0,
    QUIT = 1,
    ERASE = 2,
    KILL = 3,
    EOF = 4,
    TIME = 5,
    MIN = 6,
    SWTC = 7,
    START = 8,
    STOP = 9,
    SUSP = 10,
    EOL = 11,
    REPRINT = 12,
    DISCARD = 13,
    WERASE = 14,
    LNEXT = 15,
    EOL2 = 16,
};

pub const TCSA = std.posix.TCSA;

pub const sgttyb = if (is_mips or is_ppc or is_sparc) extern struct {
    ispeed: c_char,
    ospeed: c_char,
    erase: c_char,
    kill: c_char,
    flags: if (is_mips) c_int else c_short,
} else void;

pub const tchars = if (is_mips or is_ppc or is_sparc) extern struct {
    intrc: c_char,
    quitc: c_char,
    startc: c_char,
    stopc: c_char,
    eofc: c_char,
    brkc: c_char,
} else void;

pub const ltchars = if (is_mips or is_ppc or is_sparc) extern struct {
    suspc: c_char,
    dsuspc: c_char,
    rprntc: c_char,
    flushc: c_char,
    werasc: c_char,
    lnextc: c_char,
} else void;

pub const termio = extern struct {
    iflag: c_ushort,
    oflag: c_ushort,
    cflag: c_ushort,
    lflag: c_ushort,
    line: if (is_mips) c_char else u8,
    cc: [if (is_mips) NCCS else NCC]u8,
};

pub const termios = if (is_mips or is_sparc) extern struct {
    iflag: tc_iflag_t,
    oflag: tc_oflag_t,
    cflag: tc_cflag_t,
    lflag: tc_lflag_t,
    line: cc_t,
    cc: [NCCS]cc_t,
} else if (is_ppc) extern struct {
    iflag: tc_iflag_t,
    oflag: tc_oflag_t,
    cflag: tc_cflag_t,
    lflag: tc_lflag_t,
    cc: [NCCS]cc_t,
    line: cc_t,
    ispeed: speed_t,
    ospeed: speed_t,
} else extern struct {
    iflag: tc_iflag_t,
    oflag: tc_oflag_t,
    cflag: tc_cflag_t,
    lflag: tc_lflag_t,
    line: cc_t,
    cc: [NCCS]cc_t,
    ispeed: speed_t,
    ospeed: speed_t,
};

pub const termios2 = if (is_mips) extern struct {
    iflag: tc_iflag_t,
    oflag: tc_oflag_t,
    cflag: tc_cflag_t,
    lflag: tc_lflag_t,
    cc: [NCCS]cc_t,
    line: cc_t,
    ispeed: speed_t,
    ospeed: speed_t,
} else extern struct {
    iflag: tc_iflag_t,
    oflag: tc_oflag_t,
    cflag: tc_cflag_t,
    lflag: tc_lflag_t,
    line: cc_t,
    cc: [NCCS + if (is_sparc) 2 else 0]cc_t,
    ispeed: speed_t,
    ospeed: speed_t,
};

/// Linux-specific socket ioctls
pub const SIOCINQ = T.FIONREAD;

/// Linux-specific socket ioctls
/// output queue size (not sent + not acked)
pub const SIOCOUTQ = T.IOCOUTQ;

pub const SOCK_IOC_TYPE = 0x89;

pub const SIOCGSTAMP_NEW = IOCTL.IOR(SOCK_IOC_TYPE, 0x06, [2]i64);
pub const SIOCGSTAMP_OLD = IOCTL.IOR('s', 100, timeval);

/// Get stamp (timeval)
pub const SIOCGSTAMP = if (native_arch == .x86_64 or @sizeOf(timeval) == 8) SIOCGSTAMP_OLD else SIOCGSTAMP_NEW;

pub const SIOCGSTAMPNS_NEW = IOCTL.IOR(SOCK_IOC_TYPE, 0x07, [2]i64);
pub const SIOCGSTAMPNS_OLD = IOCTL.IOR('s', 101, kernel_timespec);

/// Get stamp (timespec)
pub const SIOCGSTAMPNS = if (native_arch == .x86_64 or @sizeOf(timespec) == 8) SIOCGSTAMPNS_OLD else SIOCGSTAMPNS_NEW;

// Routing table calls.
/// Add routing table entry
pub const SIOCADDRT = 0x890B;

/// Delete routing table entry
pub const SIOCDELRT = 0x890C;

/// Unused
pub const SIOCRTMSG = 0x890D;

// Socket configuration controls.
/// Get iface name
pub const SIOCGIFNAME = 0x8910;

/// Set iface channel
pub const SIOCSIFLINK = 0x8911;

/// Get iface list
pub const SIOCGIFCONF = 0x8912;

/// Get flags
pub const SIOCGIFFLAGS = 0x8913;

/// Set flags
pub const SIOCSIFFLAGS = 0x8914;

/// Get PA address
pub const SIOCGIFADDR = 0x8915;

/// Set PA address
pub const SIOCSIFADDR = 0x8916;

/// Get remote PA address
pub const SIOCGIFDSTADDR = 0x8917;

/// Set remote PA address
pub const SIOCSIFDSTADDR = 0x8918;

/// Get broadcast PA address
pub const SIOCGIFBRDADDR = 0x8919;

/// Set broadcast PA address
pub const SIOCSIFBRDADDR = 0x891a;

/// Get network PA mask
pub const SIOCGIFNETMASK = 0x891b;

/// Set network PA mask
pub const SIOCSIFNETMASK = 0x891c;

/// Get metric
pub const SIOCGIFMETRIC = 0x891d;

/// Set metric
pub const SIOCSIFMETRIC = 0x891e;

/// Get memory address (BSD)
pub const SIOCGIFMEM = 0x891f;

/// Set memory address (BSD)
pub const SIOCSIFMEM = 0x8920;

/// Get MTU size
pub const SIOCGIFMTU = 0x8921;

/// Set MTU size
pub const SIOCSIFMTU = 0x8922;

/// Set interface name
pub const SIOCSIFNAME = 0x8923;

/// Set hardware address
pub const SIOCSIFHWADDR = 0x8924;

/// Get encapsulations
pub const SIOCGIFENCAP = 0x8925;

/// Set encapsulations
pub const SIOCSIFENCAP = 0x8926;

/// Get hardware address
pub const SIOCGIFHWADDR = 0x8927;

/// Driver slaving support
pub const SIOCGIFSLAVE = 0x8929;

/// Driver slaving support
pub const SIOCSIFSLAVE = 0x8930;

/// Add to Multicast address lists
pub const SIOCADDMULTI = 0x8931;

/// Delete from Multicast address lists
pub const SIOCDELMULTI = 0x8932;

/// name -> if_index mapping
pub const SIOCGIFINDEX = 0x8933;

/// Set extended flags set
pub const SIOCSIFPFLAGS = 0x8934;

/// Get extended flags set
pub const SIOCGIFPFLAGS = 0x8935;

/// Delete PA address
pub const SIOCDIFADDR = 0x8936;

/// Set hardware broadcast addr
pub const SIOCSIFHWBROADCAST = 0x8937;

/// Get number of devices
pub const SIOCGIFCOUNT = 0x8938;

/// Bridging support
pub const SIOCGIFBR = 0x8940;

/// Set bridging options
pub const SIOCSIFBR = 0x8941;

/// Get the tx queue length
pub const SIOCGIFTXQLEN = 0x8942;

/// Set the tx queue length
pub const SIOCSIFTXQLEN = 0x8943;

/// Ethtool interface
pub const SIOCETHTOOL = 0x8946;

/// Get address of MII PHY in use.
pub const SIOCGMIIPHY = 0x8947;

/// Read MII PHY register.
pub const SIOCGMIIREG = 0x8948;

/// Write MII PHY register.
pub const SIOCSMIIREG = 0x8949;

/// Get / Set netdev parameters
pub const SIOCWANDEV = 0x894A;

/// Output queue size (not sent only)
pub const SIOCOUTQNSD = 0x894B;

/// Get socket network namespace
pub const SIOCGSKNS = 0x894C;

// ARP cache control calls.
//  0x8950 - 0x8952 obsolete calls.
/// Delete ARP table entry
pub const SIOCDARP = 0x8953;

/// Get ARP table entry
pub const SIOCGARP = 0x8954;

/// Set ARP table entry
pub const SIOCSARP = 0x8955;

// RARP cache control calls.
/// Delete RARP table entry
pub const SIOCDRARP = 0x8960;

/// Get RARP table entry
pub const SIOCGRARP = 0x8961;

/// Set RARP table entry
pub const SIOCSRARP = 0x8962;

// Driver configuration calls
/// Get device parameters
pub const SIOCGIFMAP = 0x8970;

/// Set device parameters
pub const SIOCSIFMAP = 0x8971;

// DLCI configuration calls
/// Create new DLCI device
pub const SIOCADDDLCI = 0x8980;

/// Delete DLCI device
pub const SIOCDELDLCI = 0x8981;

/// 802.1Q VLAN support
pub const SIOCGIFVLAN = 0x8982;

/// Set 802.1Q VLAN options
pub const SIOCSIFVLAN = 0x8983;

// bonding calls
/// Enslave a device to the bond
pub const SIOCBONDENSLAVE = 0x8990;

/// Release a slave from the bond
pub const SIOCBONDRELEASE = 0x8991;

/// Set the hw addr of the bond
pub const SIOCBONDSETHWADDR = 0x8992;

/// rtn info about slave state
pub const SIOCBONDSLAVEINFOQUERY = 0x8993;

/// rtn info about bond state
pub const SIOCBONDINFOQUERY = 0x8994;

/// Update to a new active slave
pub const SIOCBONDCHANGEACTIVE = 0x8995;

// Bridge calls
/// Create new bridge device
pub const SIOCBRADDBR = 0x89a0;

/// Remove bridge device
pub const SIOCBRDELBR = 0x89a1;

/// Add interface to bridge
pub const SIOCBRADDIF = 0x89a2;

/// Remove interface from bridge
pub const SIOCBRDELIF = 0x89a3;

/// Get hardware time stamp config
pub const SIOCSHWTSTAMP = 0x89b0;

/// Set hardware time stamp config
pub const SIOCGHWTSTAMP = 0x89b1;

/// Device private ioctl calls
pub const SIOCDEVPRIVATE = 0x89F0;

/// These 16 ioctl calls are protocol private
pub const SIOCPROTOPRIVATE = 0x89E0;

pub const IFNAMESIZE = 16;

pub const IFF = packed struct(u16) {
    UP: bool = false,
    BROADCAST: bool = false,
    DEBUG: bool = false,
    LOOPBACK: bool = false,
    POINTOPOINT: bool = false,
    NOTRAILERS: bool = false,
    RUNNING: bool = false,
    NOARP: bool = false,
    PROMISC: bool = false,
    _9: u7 = 0,
};

pub const ifmap = extern struct {
    mem_start: usize,
    mem_end: usize,
    base_addr: u16,
    irq: u8,
    dma: u8,
    port: u8,
};

pub const ifreq = extern struct {
    ifrn: extern union {
        name: [IFNAMESIZE]u8,
    },
    ifru: extern union {
        addr: sockaddr,
        dstaddr: sockaddr,
        broadaddr: sockaddr,
        netmask: sockaddr,
        hwaddr: sockaddr,
        flags: IFF,
        ivalue: i32,
        mtu: i32,
        map: ifmap,
        slave: [IFNAMESIZE - 1:0]u8,
        newname: [IFNAMESIZE - 1:0]u8,
        data: ?[*]u8,
    },
};

pub const PACKET = struct {
    pub const HOST = 0;
    pub const BROADCAST = 1;
    pub const MULTICAST = 2;
    pub const OTHERHOST = 3;
    pub const OUTGOING = 4;
    pub const LOOPBACK = 5;
    pub const USER = 6;
    pub const KERNEL = 7;

    pub const ADD_MEMBERSHIP = 1;
    pub const DROP_MEMBERSHIP = 2;
    pub const RECV_OUTPUT = 3;
    pub const RX_RING = 5;
    pub const STATISTICS = 6;
    pub const COPY_THRESH = 7;
    pub const AUXDATA = 8;
    pub const ORIGDEV = 9;
    pub const VERSION = 10;
    pub const HDRLEN = 11;
    pub const RESERVE = 12;
    pub const TX_RING = 13;
    pub const LOSS = 14;
    pub const VNET_HDR = 15;
    pub const TX_TIMESTAMP = 16;
    pub const TIMESTAMP = 17;
    pub const FANOUT = 18;
    pub const TX_HAS_OFF = 19;
    pub const QDISC_BYPASS = 20;
    pub const ROLLOVER_STATS = 21;
    pub const FANOUT_DATA = 22;
    pub const IGNORE_OUTGOING = 23;
    pub const VNET_HDR_SZ = 24;

    pub const FANOUT_HASH = 0;
    pub const FANOUT_LB = 1;
    pub const FANOUT_CPU = 2;
    pub const FANOUT_ROLLOVER = 3;
    pub const FANOUT_RND = 4;
    pub const FANOUT_QM = 5;
    pub const FANOUT_CBPF = 6;
    pub const FANOUT_EBPF = 7;
    pub const FANOUT_FLAG_ROLLOVER = 0x1000;
    pub const FANOUT_FLAG_UNIQUEID = 0x2000;
    pub const FANOUT_FLAG_IGNORE_OUTGOING = 0x4000;
    pub const FANOUT_FLAG_DEFRAG = 0x8000;
};

pub const tpacket_versions = enum(u32) {
    V1 = 0,
    V2 = 1,
    V3 = 2,
};

pub const tpacket_req3 = extern struct {
    block_size: c_uint, // Minimal size of contiguous block
    block_nr: c_uint, // Number of blocks
    frame_size: c_uint, // Size of frame
    frame_nr: c_uint, // Total number of frames
    retire_blk_tov: c_uint, // Timeout in msecs
    sizeof_priv: c_uint, // Offset to private data area
    feature_req_word: c_uint,
};

pub const tpacket_bd_ts = extern struct {
    sec: c_uint,
    frac: extern union {
        usec: c_uint,
        nsec: c_uint,
    },
};

pub const TP_STATUS = extern union {
    rx: packed struct(u32) {
        USER: bool,
        COPY: bool,
        LOSING: bool,
        CSUMNOTREADY: bool,
        VLAN_VALID: bool,
        BLK_TMO: bool,
        VLAN_TPID_VALID: bool,
        CSUM_VALID: bool,
        GSO_TCP: bool,
        _: u20,
        TS_SOFTWARE: bool,
        TS_SYS_HARDWARE: bool,
        TS_RAW_HARDWARE: bool,
    },
    tx: packed struct(u32) {
        SEND_REQUEST: bool,
        SENDING: bool,
        WRONG_FORMAT: bool,
        _: u26,
        TS_SOFTWARE: bool,
        TS_SYS_HARDWARE: bool,
        TS_RAW_HARDWARE: bool,
    },
};

pub const tpacket_hdr_v1 = extern struct {
    block_status: TP_STATUS,
    num_pkts: u32,
    offset_to_first_pkt: u32,
    blk_len: u32,
    seq_num: u64 align(8),
    ts_first_pkt: tpacket_bd_ts,
    ts_last_pkt: tpacket_bd_ts,
};

pub const tpacket_bd_header_u = extern union {
    bh1: tpacket_hdr_v1,
};

pub const tpacket_block_desc = extern struct {
    version: u32,
    offset_to_priv: u32,
    hdr: tpacket_bd_header_u,
};

pub const tpacket_hdr_variant1 = extern struct {
    rxhash: u32,
    vlan_tci: u32,
    vlan_tpid: u16,
    padding: u16,
};

pub const tpacket3_hdr = extern struct {
    next_offset: u32,
    sec: u32,
    nsec: u32,
    snaplen: u32,
    len: u32,
    status: u32,
    mac: u16,
    net: u16,
    variant: extern union {
        hv1: tpacket_hdr_variant1,
    },
    padding: [8]u8,
};

pub const tpacket_stats_v3 = extern struct {
    packets: c_uint,
    drops: c_uint,
    freeze_q_cnt: c_uint,
};

// doc comments copied from musl
pub const rlimit_resource = if (native_arch.isMIPS()) enum(c_int) {
    /// Per-process CPU limit, in seconds.
    CPU = 0,

    /// Largest file that can be created, in bytes.
    FSIZE = 1,

    /// Maximum size of data segment, in bytes.
    DATA = 2,

    /// Maximum size of stack segment, in bytes.
    STACK = 3,

    /// Largest core file that can be created, in bytes.
    CORE = 4,

    /// Number of open files.
    NOFILE = 5,

    /// Address space limit.
    AS = 6,

    /// Largest resident set size, in bytes.
    /// This affects swapping; processes that are exceeding their
    /// resident set size will be more likely to have physical memory
    /// taken from them.
    RSS = 7,

    /// Number of processes.
    NPROC = 8,

    /// Locked-in-memory address space.
    MEMLOCK = 9,

    /// Maximum number of file locks.
    LOCKS = 10,

    /// Maximum number of pending signals.
    SIGPENDING = 11,

    /// Maximum bytes in POSIX message queues.
    MSGQUEUE = 12,

    /// Maximum nice priority allowed to raise to.
    /// Nice levels 19 .. -20 correspond to 0 .. 39
    /// values of this resource limit.
    NICE = 13,

    /// Maximum realtime priority allowed for non-privileged
    /// processes.
    RTPRIO = 14,

    /// Maximum CPU time in µs that a process scheduled under a real-time
    /// scheduling policy may consume without making a blocking system
    /// call before being forcibly descheduled.
    RTTIME = 15,

    _,
} else if (native_arch.isSPARC()) enum(c_int) {
    /// Per-process CPU limit, in seconds.
    CPU = 0,

    /// Largest file that can be created, in bytes.
    FSIZE = 1,

    /// Maximum size of data segment, in bytes.
    DATA = 2,

    /// Maximum size of stack segment, in bytes.
    STACK = 3,

    /// Largest core file that can be created, in bytes.
    CORE = 4,

    /// Largest resident set size, in bytes.
    /// This affects swapping; processes that are exceeding their
    /// resident set size will be more likely to have physical memory
    /// taken from them.
    RSS = 5,

    /// Number of open files.
    NOFILE = 6,

    /// Number of processes.
    NPROC = 7,

    /// Locked-in-memory address space.
    MEMLOCK = 8,

    /// Address space limit.
    AS = 9,

    /// Maximum number of file locks.
    LOCKS = 10,

    /// Maximum number of pending signals.
    SIGPENDING = 11,

    /// Maximum bytes in POSIX message queues.
    MSGQUEUE = 12,

    /// Maximum nice priority allowed to raise to.
    /// Nice levels 19 .. -20 correspond to 0 .. 39
    /// values of this resource limit.
    NICE = 13,

    /// Maximum realtime priority allowed for non-privileged
    /// processes.
    RTPRIO = 14,

    /// Maximum CPU time in µs that a process scheduled under a real-time
    /// scheduling policy may consume without making a blocking system
    /// call before being forcibly descheduled.
    RTTIME = 15,

    _,
} else enum(c_int) {
    /// Per-process CPU limit, in seconds.
    CPU = 0,
    /// Largest file that can be created, in bytes.
    FSIZE = 1,
    /// Maximum size of data segment, in bytes.
    DATA = 2,
    /// Maximum size of stack segment, in bytes.
    STACK = 3,
    /// Largest core file that can be created, in bytes.
    CORE = 4,
    /// Largest resident set size, in bytes.
    /// This affects swapping; processes that are exceeding their
    /// resident set size will be more likely to have physical memory
    /// taken from them.
    RSS = 5,
    /// Number of processes.
    NPROC = 6,
    /// Number of open files.
    NOFILE = 7,
    /// Locked-in-memory address space.
    MEMLOCK = 8,
    /// Address space limit.
    AS = 9,
    /// Maximum number of file locks.
    LOCKS = 10,
    /// Maximum number of pending signals.
    SIGPENDING = 11,
    /// Maximum bytes in POSIX message queues.
    MSGQUEUE = 12,
    /// Maximum nice priority allowed to raise to.
    /// Nice levels 19 .. -20 correspond to 0 .. 39
    /// values of this resource limit.
    NICE = 13,
    /// Maximum realtime priority allowed for non-privileged
    /// processes.
    RTPRIO = 14,
    /// Maximum CPU time in µs that a process scheduled under a real-time
    /// scheduling policy may consume without making a blocking system
    /// call before being forcibly descheduled.
    RTTIME = 15,

    _,
};

pub const rlim_t = u64;

pub const RLIM = struct {
    /// No limit
    pub const INFINITY = ~@as(rlim_t, 0);

    pub const SAVED_MAX = INFINITY;
    pub const SAVED_CUR = INFINITY;
};

pub const rlimit = extern struct {
    /// Soft limit
    cur: rlim_t,
    /// Hard limit
    max: rlim_t,
};

pub const MADV = struct {
    pub const NORMAL = 0;
    pub const RANDOM = 1;
    pub const SEQUENTIAL = 2;
    pub const WILLNEED = 3;
    pub const DONTNEED = 4;
    pub const FREE = 8;
    pub const REMOVE = 9;
    pub const DONTFORK = 10;
    pub const DOFORK = 11;
    pub const MERGEABLE = 12;
    pub const UNMERGEABLE = 13;
    pub const HUGEPAGE = 14;
    pub const NOHUGEPAGE = 15;
    pub const DONTDUMP = 16;
    pub const DODUMP = 17;
    pub const WIPEONFORK = 18;
    pub const KEEPONFORK = 19;
    pub const COLD = 20;
    pub const PAGEOUT = 21;
    pub const HWPOISON = 100;
    pub const SOFT_OFFLINE = 101;
};

pub const POSIX_FADV = switch (native_arch) {
    .s390x => if (@typeInfo(usize).int.bits == 64) struct {
        pub const NORMAL = 0;
        pub const RANDOM = 1;
        pub const SEQUENTIAL = 2;
        pub const WILLNEED = 3;
        pub const DONTNEED = 6;
        pub const NOREUSE = 7;
    } else struct {
        pub const NORMAL = 0;
        pub const RANDOM = 1;
        pub const SEQUENTIAL = 2;
        pub const WILLNEED = 3;
        pub const DONTNEED = 4;
        pub const NOREUSE = 5;
    },
    else => struct {
        pub const NORMAL = 0;
        pub const RANDOM = 1;
        pub const SEQUENTIAL = 2;
        pub const WILLNEED = 3;
        pub const DONTNEED = 4;
        pub const NOREUSE = 5;
    },
};

pub const timeval = extern struct {
    sec: isize,
    usec: i64,
};

pub const timezone = extern struct {
    minuteswest: i32,
    dsttime: i32,
};

/// The timespec struct used by the kernel.
pub const kernel_timespec = extern struct {
    sec: i64,
    nsec: i64,

    /// For use with `utimensat` and `futimens`.
    pub const NOW: timespec = .{
        .sec = 0,
        .nsec = 0x3fffffff,
    };

    /// For use with `utimensat` and `futimens`.
    pub const OMIT: timespec = .{
        .sec = 0,
        .nsec = 0x3ffffffe,
    };
};

/// For use with `utimensat` and `futimens`.
pub const UTIME = struct {
    pub const NOW: timespec = .{ .sec = 0, .nsec = 0x3fffffff };
    pub const OMIT: timespec = .{ .sec = 0, .nsec = 0x3ffffffe };
};

// https://github.com/ziglang/zig/issues/4726#issuecomment-2190337877
pub const timespec = if (native_arch == .hexagon or native_arch == .riscv32) kernel_timespec else extern struct {
    sec: isize,
    nsec: isize,
};

pub const XDP = struct {
    pub const SHARED_UMEM = (1 << 0);
    pub const COPY = (1 << 1);
    pub const ZEROCOPY = (1 << 2);
    pub const UMEM_UNALIGNED_CHUNK_FLAG = (1 << 0);
    pub const USE_NEED_WAKEUP = (1 << 3);

    pub const MMAP_OFFSETS = 1;
    pub const RX_RING = 2;
    pub const TX_RING = 3;
    pub const UMEM_REG = 4;
    pub const UMEM_FILL_RING = 5;
    pub const UMEM_COMPLETION_RING = 6;
    pub const STATISTICS = 7;
    pub const OPTIONS = 8;

    pub const OPTIONS_ZEROCOPY = (1 << 0);

    pub const PGOFF_RX_RING = 0;
    pub const PGOFF_TX_RING = 0x80000000;
    pub const UMEM_PGOFF_FILL_RING = 0x100000000;
    pub const UMEM_PGOFF_COMPLETION_RING = 0x180000000;
};

pub const xdp_ring_offset = extern struct {
    producer: u64,
    consumer: u64,
    desc: u64,
    flags: u64,
};

pub const xdp_mmap_offsets = extern struct {
    rx: xdp_ring_offset,
    tx: xdp_ring_offset,
    fr: xdp_ring_offset,
    cr: xdp_ring_offset,
};

pub const xdp_umem_reg = extern struct {
    addr: u64,
    len: u64,
    chunk_size: u32,
    headroom: u32,
    flags: u32,
};

pub const xdp_statistics = extern struct {
    rx_dropped: u64,
    rx_invalid_descs: u64,
    tx_invalid_descs: u64,
    rx_ring_full: u64,
    rx_fill_ring_empty_descs: u64,
    tx_ring_empty_descs: u64,
};

pub const xdp_options = extern struct {
    flags: u32,
};

pub const XSK_UNALIGNED_BUF_OFFSET_SHIFT = 48;
pub const XSK_UNALIGNED_BUF_ADDR_MASK = (1 << XSK_UNALIGNED_BUF_OFFSET_SHIFT) - 1;

pub const xdp_desc = extern struct {
    addr: u64,
    len: u32,
    options: u32,
};

fn issecure_mask(comptime x: comptime_int) comptime_int {
    return 1 << x;
}

pub const SECUREBITS_DEFAULT = 0x00000000;

pub const SECURE_NOROOT = 0;
pub const SECURE_NOROOT_LOCKED = 1;

pub const SECBIT_NOROOT = issecure_mask(SECURE_NOROOT);
pub const SECBIT_NOROOT_LOCKED = issecure_mask(SECURE_NOROOT_LOCKED);

pub const SECURE_NO_SETUID_FIXUP = 2;
pub const SECURE_NO_SETUID_FIXUP_LOCKED = 3;

pub const SECBIT_NO_SETUID_FIXUP = issecure_mask(SECURE_NO_SETUID_FIXUP);
pub const SECBIT_NO_SETUID_FIXUP_LOCKED = issecure_mask(SECURE_NO_SETUID_FIXUP_LOCKED);

pub const SECURE_KEEP_CAPS = 4;
pub const SECURE_KEEP_CAPS_LOCKED = 5;

pub const SECBIT_KEEP_CAPS = issecure_mask(SECURE_KEEP_CAPS);
pub const SECBIT_KEEP_CAPS_LOCKED = issecure_mask(SECURE_KEEP_CAPS_LOCKED);

pub const SECURE_NO_CAP_AMBIENT_RAISE = 6;
pub const SECURE_NO_CAP_AMBIENT_RAISE_LOCKED = 7;

pub const SECBIT_NO_CAP_AMBIENT_RAISE = issecure_mask(SECURE_NO_CAP_AMBIENT_RAISE);
pub const SECBIT_NO_CAP_AMBIENT_RAISE_LOCKED = issecure_mask(SECURE_NO_CAP_AMBIENT_RAISE_LOCKED);

pub const SECURE_ALL_BITS = issecure_mask(SECURE_NOROOT) |
    issecure_mask(SECURE_NO_SETUID_FIXUP) |
    issecure_mask(SECURE_KEEP_CAPS) |
    issecure_mask(SECURE_NO_CAP_AMBIENT_RAISE);
pub const SECURE_ALL_LOCKS = SECURE_ALL_BITS << 1;

pub const PR = enum(i32) {
    SET_PDEATHSIG = 1,
    GET_PDEATHSIG = 2,

    GET_DUMPABLE = 3,
    SET_DUMPABLE = 4,

    GET_UNALIGN = 5,
    SET_UNALIGN = 6,

    GET_KEEPCAPS = 7,
    SET_KEEPCAPS = 8,

    GET_FPEMU = 9,
    SET_FPEMU = 10,

    GET_FPEXC = 11,
    SET_FPEXC = 12,

    GET_TIMING = 13,
    SET_TIMING = 14,

    SET_NAME = 15,
    GET_NAME = 16,

    GET_ENDIAN = 19,
    SET_ENDIAN = 20,

    GET_SECCOMP = 21,
    SET_SECCOMP = 22,

    CAPBSET_READ = 23,
    CAPBSET_DROP = 24,

    GET_TSC = 25,
    SET_TSC = 26,

    GET_SECUREBITS = 27,
    SET_SECUREBITS = 28,

    SET_TIMERSLACK = 29,
    GET_TIMERSLACK = 30,

    TASK_PERF_EVENTS_DISABLE = 31,
    TASK_PERF_EVENTS_ENABLE = 32,

    MCE_KILL = 33,

    MCE_KILL_GET = 34,

    SET_MM = 35,

    SET_PTRACER = 0x59616d61,

    SET_CHILD_SUBREAPER = 36,
    GET_CHILD_SUBREAPER = 37,

    SET_NO_NEW_PRIVS = 38,
    GET_NO_NEW_PRIVS = 39,

    GET_TID_ADDRESS = 40,

    SET_THP_DISABLE = 41,
    GET_THP_DISABLE = 42,

    MPX_ENABLE_MANAGEMENT = 43,
    MPX_DISABLE_MANAGEMENT = 44,

    SET_FP_MODE = 45,
    GET_FP_MODE = 46,

    CAP_AMBIENT = 47,

    SVE_SET_VL = 50,
    SVE_GET_VL = 51,

    GET_SPECULATION_CTRL = 52,
    SET_SPECULATION_CTRL = 53,

    _,

    pub const UNALIGN_NOPRINT = 1;
    pub const UNALIGN_SIGBUS = 2;

    pub const FPEMU_NOPRINT = 1;
    pub const FPEMU_SIGFPE = 2;

    pub const FP_EXC_SW_ENABLE = 0x80;
    pub const FP_EXC_DIV = 0x010000;
    pub const FP_EXC_OVF = 0x020000;
    pub const FP_EXC_UND = 0x040000;
    pub const FP_EXC_RES = 0x080000;
    pub const FP_EXC_INV = 0x100000;
    pub const FP_EXC_DISABLED = 0;
    pub const FP_EXC_NONRECOV = 1;
    pub const FP_EXC_ASYNC = 2;
    pub const FP_EXC_PRECISE = 3;

    pub const TIMING_STATISTICAL = 0;
    pub const TIMING_TIMESTAMP = 1;

    pub const ENDIAN_BIG = 0;
    pub const ENDIAN_LITTLE = 1;
    pub const ENDIAN_PPC_LITTLE = 2;

    pub const TSC_ENABLE = 1;
    pub const TSC_SIGSEGV = 2;

    pub const MCE_KILL_CLEAR = 0;
    pub const MCE_KILL_SET = 1;

    pub const MCE_KILL_LATE = 0;
    pub const MCE_KILL_EARLY = 1;
    pub const MCE_KILL_DEFAULT = 2;

    pub const SET_MM_START_CODE = 1;
    pub const SET_MM_END_CODE = 2;
    pub const SET_MM_START_DATA = 3;
    pub const SET_MM_END_DATA = 4;
    pub const SET_MM_START_STACK = 5;
    pub const SET_MM_START_BRK = 6;
    pub const SET_MM_BRK = 7;
    pub const SET_MM_ARG_START = 8;
    pub const SET_MM_ARG_END = 9;
    pub const SET_MM_ENV_START = 10;
    pub const SET_MM_ENV_END = 11;
    pub const SET_MM_AUXV = 12;
    pub const SET_MM_EXE_FILE = 13;
    pub const SET_MM_MAP = 14;
    pub const SET_MM_MAP_SIZE = 15;

    pub const SET_PTRACER_ANY = maxInt(c_ulong);

    pub const FP_MODE_FR = 1 << 0;
    pub const FP_MODE_FRE = 1 << 1;

    pub const CAP_AMBIENT_IS_SET = 1;
    pub const CAP_AMBIENT_RAISE = 2;
    pub const CAP_AMBIENT_LOWER = 3;
    pub const CAP_AMBIENT_CLEAR_ALL = 4;

    pub const SVE_SET_VL_ONEXEC = 1 << 18;
    pub const SVE_VL_LEN_MASK = 0xffff;
    pub const SVE_VL_INHERIT = 1 << 17;

    pub const SPEC_STORE_BYPASS = 0;
    pub const SPEC_NOT_AFFECTED = 0;
    pub const SPEC_PRCTL = 1 << 0;
    pub const SPEC_ENABLE = 1 << 1;
    pub const SPEC_DISABLE = 1 << 2;
    pub const SPEC_FORCE_DISABLE = 1 << 3;
};

pub const prctl_mm_map = extern struct {
    start_code: u64,
    end_code: u64,
    start_data: u64,
    end_data: u64,
    start_brk: u64,
    brk: u64,
    start_stack: u64,
    arg_start: u64,
    arg_end: u64,
    env_start: u64,
    env_end: u64,
    auxv: *u64,
    auxv_size: u32,
    exe_fd: u32,
};

pub const NETLINK = struct {
    /// Routing/device hook
    pub const ROUTE = 0;

    /// Unused number
    pub const UNUSED = 1;

    /// Reserved for user mode socket protocols
    pub const USERSOCK = 2;

    /// Unused number, formerly ip_queue
    pub const FIREWALL = 3;

    /// socket monitoring
    pub const SOCK_DIAG = 4;

    /// netfilter/iptables ULOG
    pub const NFLOG = 5;

    /// ipsec
    pub const XFRM = 6;

    /// SELinux event notifications
    pub const SELINUX = 7;

    /// Open-iSCSI
    pub const ISCSI = 8;

    /// auditing
    pub const AUDIT = 9;

    pub const FIB_LOOKUP = 10;

    pub const CONNECTOR = 11;

    /// netfilter subsystem
    pub const NETFILTER = 12;

    pub const IP6_FW = 13;

    /// DECnet routing messages
    pub const DNRTMSG = 14;

    /// Kernel messages to userspace
    pub const KOBJECT_UEVENT = 15;

    pub const GENERIC = 16;

    // leave room for NETLINK_DM (DM Events)

    /// SCSI Transports
    pub const SCSITRANSPORT = 18;

    pub const ECRYPTFS = 19;

    pub const RDMA = 20;

    /// Crypto layer
    pub const CRYPTO = 21;

    /// SMC monitoring
    pub const SMC = 22;
};

// Flags values

/// It is request message.
pub const NLM_F_REQUEST = 0x01;

/// Multipart message, terminated by NLMSG_DONE
pub const NLM_F_MULTI = 0x02;

/// Reply with ack, with zero or error code
pub const NLM_F_ACK = 0x04;

/// Echo this request
pub const NLM_F_ECHO = 0x08;

/// Dump was inconsistent due to sequence change
pub const NLM_F_DUMP_INTR = 0x10;

/// Dump was filtered as requested
pub const NLM_F_DUMP_FILTERED = 0x20;

// Modifiers to GET request

/// specify tree root
pub const NLM_F_ROOT = 0x100;

/// return all matching
pub const NLM_F_MATCH = 0x200;

/// atomic GET
pub const NLM_F_ATOMIC = 0x400;
pub const NLM_F_DUMP = NLM_F_ROOT | NLM_F_MATCH;

// Modifiers to NEW request

/// Override existing
pub const NLM_F_REPLACE = 0x100;

/// Do not touch, if it exists
pub const NLM_F_EXCL = 0x200;

/// Create, if it does not exist
pub const NLM_F_CREATE = 0x400;

/// Add to end of list
pub const NLM_F_APPEND = 0x800;

// Modifiers to DELETE request

/// Do not delete recursively
pub const NLM_F_NONREC = 0x100;

// Flags for ACK message

/// request was capped
pub const NLM_F_CAPPED = 0x100;

/// extended ACK TVLs were included
pub const NLM_F_ACK_TLVS = 0x200;

pub const NetlinkMessageType = enum(u16) {
    /// < 0x10: reserved control messages
    pub const MIN_TYPE = 0x10;

    /// Nothing.
    NOOP = 0x1,

    /// Error
    ERROR = 0x2,

    /// End of a dump
    DONE = 0x3,

    /// Data lost
    OVERRUN = 0x4,

    // rtlink types

    RTM_NEWLINK = 16,
    RTM_DELLINK,
    RTM_GETLINK,
    RTM_SETLINK,

    RTM_NEWADDR = 20,
    RTM_DELADDR,
    RTM_GETADDR,

    RTM_NEWROUTE = 24,
    RTM_DELROUTE,
    RTM_GETROUTE,

    RTM_NEWNEIGH = 28,
    RTM_DELNEIGH,
    RTM_GETNEIGH,

    RTM_NEWRULE = 32,
    RTM_DELRULE,
    RTM_GETRULE,

    RTM_NEWQDISC = 36,
    RTM_DELQDISC,
    RTM_GETQDISC,

    RTM_NEWTCLASS = 40,
    RTM_DELTCLASS,
    RTM_GETTCLASS,

    RTM_NEWTFILTER = 44,
    RTM_DELTFILTER,
    RTM_GETTFILTER,

    RTM_NEWACTION = 48,
    RTM_DELACTION,
    RTM_GETACTION,

    RTM_NEWPREFIX = 52,

    RTM_GETMULTICAST = 58,

    RTM_GETANYCAST = 62,

    RTM_NEWNEIGHTBL = 64,
    RTM_GETNEIGHTBL = 66,
    RTM_SETNEIGHTBL,

    RTM_NEWNDUSEROPT = 68,

    RTM_NEWADDRLABEL = 72,
    RTM_DELADDRLABEL,
    RTM_GETADDRLABEL,

    RTM_GETDCB = 78,
    RTM_SETDCB,

    RTM_NEWNETCONF = 80,
    RTM_DELNETCONF,
    RTM_GETNETCONF = 82,

    RTM_NEWMDB = 84,
    RTM_DELMDB = 85,
    RTM_GETMDB = 86,

    RTM_NEWNSID = 88,
    RTM_DELNSID = 89,
    RTM_GETNSID = 90,

    RTM_NEWSTATS = 92,
    RTM_GETSTATS = 94,

    RTM_NEWCACHEREPORT = 96,

    RTM_NEWCHAIN = 100,
    RTM_DELCHAIN,
    RTM_GETCHAIN,

    RTM_NEWNEXTHOP = 104,
    RTM_DELNEXTHOP,
    RTM_GETNEXTHOP,

    _,
};

/// Netlink message header
/// Specified in RFC 3549 Section 2.3.2
pub const nlmsghdr = extern struct {
    /// Length of message including header
    len: u32,

    /// Message content
    type: NetlinkMessageType,

    /// Additional flags
    flags: u16,

    /// Sequence number
    seq: u32,

    /// Sending process port ID
    pid: u32,
};

pub const ifinfomsg = extern struct {
    family: u8,
    __pad1: u8 = 0,

    /// ARPHRD_*
    type: c_ushort,

    /// Link index
    index: c_int,

    /// IFF_* flags
    flags: c_uint,

    /// IFF_* change mask
    change: c_uint,
};

pub const rtattr = extern struct {
    /// Length of option
    len: c_ushort,

    /// Type of option
    type: extern union {
        /// IFLA_* from linux/if_link.h
        link: IFLA,
        /// IFA_* from linux/if_addr.h
        addr: IFA,
    },

    pub const ALIGNTO = 4;
};

pub const IFA = enum(c_ushort) {
    UNSPEC,
    ADDRESS,
    LOCAL,
    LABEL,
    BROADCAST,
    ANYCAST,
    CACHEINFO,
    MULTICAST,
    FLAGS,
    RT_PRIORITY,
    TARGET_NETNSID,
    PROTO,

    _,
};

pub const IFLA = enum(c_ushort) {
    UNSPEC,
    ADDRESS,
    BROADCAST,
    IFNAME,
    MTU,
    LINK,
    QDISC,
    STATS,
    COST,
    PRIORITY,
    MASTER,

    /// Wireless Extension event
    WIRELESS,

    /// Protocol specific information for a link
    PROTINFO,

    TXQLEN,
    MAP,
    WEIGHT,
    OPERSTATE,
    LINKMODE,
    LINKINFO,
    NET_NS_PID,
    IFALIAS,

    /// Number of VFs if device is SR-IOV PF
    NUM_VF,

    VFINFO_LIST,
    STATS64,
    VF_PORTS,
    PORT_SELF,
    AF_SPEC,

    /// Group the device belongs to
    GROUP,

    NET_NS_FD,

    /// Extended info mask, VFs, etc
    EXT_MASK,

    /// Promiscuity count: > 0 means acts PROMISC
    PROMISCUITY,

    NUM_TX_QUEUES,
    NUM_RX_QUEUES,
    CARRIER,
    PHYS_PORT_ID,
    CARRIER_CHANGES,
    PHYS_SWITCH_ID,
    LINK_NETNSID,
    PHYS_PORT_NAME,
    PROTO_DOWN,
    GSO_MAX_SEGS,
    GSO_MAX_SIZE,
    PAD,
    XDP,
    EVENT,

    NEW_NETNSID,
    IF_NETNSID,

    CARRIER_UP_COUNT,
    CARRIER_DOWN_COUNT,
    NEW_IFINDEX,
    MIN_MTU,
    MAX_MTU,

    _,

    pub const TARGET_NETNSID: IFLA = .IF_NETNSID;
};

pub const rtnl_link_ifmap = extern struct {
    mem_start: u64,
    mem_end: u64,
    base_addr: u64,
    irq: u16,
    dma: u8,
    port: u8,
};

pub const rtnl_link_stats = extern struct {
    /// total packets received
    rx_packets: u32,

    /// total packets transmitted
    tx_packets: u32,

    /// total bytes received
    rx_bytes: u32,

    /// total bytes transmitted
    tx_bytes: u32,

    /// bad packets received
    rx_errors: u32,

    /// packet transmit problems
    tx_errors: u32,

    /// no space in linux buffers
    rx_dropped: u32,

    /// no space available in linux
    tx_dropped: u32,

    /// multicast packets received
    multicast: u32,

    collisions: u32,

    // detailed rx_errors

    rx_length_errors: u32,

    /// receiver ring buff overflow
    rx_over_errors: u32,

    /// recved pkt with crc error
    rx_crc_errors: u32,

    /// recv'd frame alignment error
    rx_frame_errors: u32,

    /// recv'r fifo overrun
    rx_fifo_errors: u32,

    /// receiver missed packet
    rx_missed_errors: u32,

    // detailed tx_errors
    tx_aborted_errors: u32,
    tx_carrier_errors: u32,
    tx_fifo_errors: u32,
    tx_heartbeat_errors: u32,
    tx_window_errors: u32,

    // for cslip etc

    rx_compressed: u32,
    tx_compressed: u32,

    /// dropped, no handler found
    rx_nohandler: u32,
};

pub const rtnl_link_stats64 = extern struct {
    /// total packets received
    rx_packets: u64,

    /// total packets transmitted
    tx_packets: u64,

    /// total bytes received
    rx_bytes: u64,

    /// total bytes transmitted
    tx_bytes: u64,

    /// bad packets received
    rx_errors: u64,

    /// packet transmit problems
    tx_errors: u64,

    /// no space in linux buffers
    rx_dropped: u64,

    /// no space available in linux
    tx_dropped: u64,

    /// multicast packets received
    multicast: u64,

    collisions: u64,

    // detailed rx_errors

    rx_length_errors: u64,

    /// receiver ring buff overflow
    rx_over_errors: u64,

    /// recved pkt with crc error
    rx_crc_errors: u64,

    /// recv'd frame alignment error
    rx_frame_errors: u64,

    /// recv'r fifo overrun
    rx_fifo_errors: u64,

    /// receiver missed packet
    rx_missed_errors: u64,

    // detailed tx_errors
    tx_aborted_errors: u64,
    tx_carrier_errors: u64,
    tx_fifo_errors: u64,
    tx_heartbeat_errors: u64,
    tx_window_errors: u64,

    // for cslip etc

    rx_compressed: u64,
    tx_compressed: u64,

    /// dropped, no handler found
    rx_nohandler: u64,
};

pub const perf_event_attr = extern struct {
    /// Major type: hardware/software/tracepoint/etc.
    type: PERF.TYPE = undefined,
    /// Size of the attr structure, for fwd/bwd compat.
    size: u32 = @sizeOf(perf_event_attr),
    /// Type specific configuration information.
    config: u64 = 0,

    sample_period_or_freq: u64 = 0,
    sample_type: u64 = 0,
    read_format: u64 = 0,

    flags: packed struct(u64) {
        /// off by default
        disabled: bool = false,
        /// children inherit it
        inherit: bool = false,
        /// must always be on PMU
        pinned: bool = false,
        /// only group on PMU
        exclusive: bool = false,
        /// don't count user
        exclude_user: bool = false,
        /// ditto kernel
        exclude_kernel: bool = false,
        /// ditto hypervisor
        exclude_hv: bool = false,
        /// don't count when idle
        exclude_idle: bool = false,
        /// include mmap data
        mmap: bool = false,
        /// include comm data
        comm: bool = false,
        /// use freq, not period
        freq: bool = false,
        /// per task counts
        inherit_stat: bool = false,
        /// next exec enables
        enable_on_exec: bool = false,
        /// trace fork/exit
        task: bool = false,
        /// wakeup_watermark
        watermark: bool = false,
        /// precise_ip:
        ///
        ///  0 - SAMPLE_IP can have arbitrary skid
        ///  1 - SAMPLE_IP must have constant skid
        ///  2 - SAMPLE_IP requested to have 0 skid
        ///  3 - SAMPLE_IP must have 0 skid
        ///
        ///  See also PERF_RECORD_MISC_EXACT_IP
        /// skid constraint
        precise_ip: u2 = 0,
        /// non-exec mmap data
        mmap_data: bool = false,
        /// sample_type all events
        sample_id_all: bool = false,

        /// don't count in host
        exclude_host: bool = false,
        /// don't count in guest
        exclude_guest: bool = false,

        /// exclude kernel callchains
        exclude_callchain_kernel: bool = false,
        /// exclude user callchains
        exclude_callchain_user: bool = false,
        /// include mmap with inode data
        mmap2: bool = false,
        /// flag comm events that are due to an exec
        comm_exec: bool = false,
        /// use @clockid for time fields
        use_clockid: bool = false,
        /// context switch data
        context_switch: bool = false,
        /// Write ring buffer from end to beginning
        write_backward: bool = false,
        /// include namespaces data
        namespaces: bool = false,
        /// include ksymbol events
        ksymbol: bool = false,
        /// include BPF events
        bpf_event: bool = false,
        /// generate AUX records instead of events
        aux_output: bool = false,
        /// include cgroup events
        cgroup: bool = false,
        /// include text poke events
        text_poke: bool = false,
        /// use build ID in mmap2 events
        build_id: bool = false,
        /// children only inherit if cloned with CLONE_THREAD
        inherit_thread: bool = false,
        /// event is removed from task on exec
        remove_on_exec: bool = false,
        /// send synchronous SIGTRAP on event
        sigtrap: bool = false,

        __reserved_1: u26 = 0,
    } = .{},
    /// wakeup every n events, or
    /// bytes before wakeup
    wakeup_events_or_watermark: u32 = 0,

    bp_type: u32 = 0,

    /// This field is also used for:
    /// bp_addr
    /// kprobe_func for perf_kprobe
    /// uprobe_path for perf_uprobe
    config1: u64 = 0,
    /// This field is also used for:
    /// bp_len
    /// kprobe_addr when kprobe_func == null
    /// probe_offset for perf_[k,u]probe
    config2: u64 = 0,

    /// enum perf_branch_sample_type
    branch_sample_type: u64 = 0,

    /// Defines set of user regs to dump on samples.
    /// See asm/perf_regs.h for details.
    sample_regs_user: u64 = 0,

    /// Defines size of the user stack to dump on samples.
    sample_stack_user: u32 = 0,

    clockid: clockid_t = .REALTIME,
    /// Defines set of regs to dump for each sample
    /// state captured on:
    ///  - precise = 0: PMU interrupt
    ///  - precise > 0: sampled instruction
    ///
    /// See asm/perf_regs.h for details.
    sample_regs_intr: u64 = 0,

    /// Wakeup watermark for AUX area
    aux_watermark: u32 = 0,
    sample_max_stack: u16 = 0,
    /// Align to u64
    __reserved_2: u16 = 0,

    aux_sample_size: u32 = 0,
    aux_action: packed struct(u32) {
        /// start AUX area tracing paused
        start_paused: bool = false,
        /// on overflow, pause AUX area tracing
        pause: bool = false,
        /// on overflow, resume AUX area tracing
        @"resume": bool = false,

        __reserved_3: u29 = 0,
    } = .{},

    /// User provided data if sigtrap == true
    sig_data: u64 = 0,

    /// Extension of config2
    config3: u64 = 0,
};

pub const perf_event_header = extern struct {
    /// Event type: sample/mmap/fork/etc.
    type: PERF.RECORD,
    /// Additional informations on the event: kernel/user/hypervisor/etc.
    misc: packed struct(u16) {
        cpu_mode: PERF.RECORD.MISC.CPU_MODE,
        _: u9,
        PROC_MAP_PARSE_TIMEOUT: bool,
        bit13: packed union {
            MMAP_DATA: bool,
            COMM_EXEC: bool,
            FORK_EXEC: bool,
            SWITCH_OUT: bool,
        },
        bit14: packed union {
            EXACT_IP: bool,
            SWITCH_OUT_PREEMPT: bool,
            MMAP_BUILD_ID: bool,
        },
        EXT_RESERVED: bool,
    },
    /// Size of the following record
    size: u16,
};

pub const perf_event_mmap_page = extern struct {
    /// Version number of this struct
    version: u32,
    /// Lowest version this is compatible with
    compt_version: u32,
    /// Seqlock for synchronization
    lock: u32,
    /// Hardware counter identifier
    index: u32,
    /// Add to hardware counter value
    offset: i64,
    /// Time the event was active
    time_enabled: u64,
    /// Time the event was running
    time_running: u64,
    capabilities: packed struct(u64) {
        /// If kernel version < 3.12
        /// this rapresents both user_rdpmc and user_time (user_rdpmc | user_time)
        /// otherwise deprecated.
        bit0: bool,
        /// Set if bit0 is deprecated
        bit0_is_deprecated: bool,
        /// Hardware support for userspace read of performance counters
        user_rdpmc: bool,
        /// Hardware support for a constant non stop timestamp counter (Eg. TSC on x86)
        user_time: bool,
        /// The time_zero field is used
        user_time_zero: bool,
        /// The time_{cycle,mask} fields are used
        user_time_short: bool,
        ____res: u58,
    },
    /// If capabilities.user_rdpmc
    /// this field reports the bit-width of the value read with rdpmc() or equivalent
    pcm_width: u16,
    /// If capabilities.user_time the following fields can be used to compute the time
    /// delta since time_enabled (in ns) using RDTSC or similar
    time_shift: u16,
    time_mult: u32,
    time_offset: u64,
    /// If capabilities.user_time_zero the hardware clock can be calculated from
    /// sample timestamps
    time_zero: u64,
    /// Header size
    size: u32,
    __reserved_1: u32,
    /// The following fields are used to compute the timestamp when the hardware clock
    /// is less than 64bit wide
    time_cycles: u64,
    time_mask: u64,
    __reserved: [116 * 8]u8,
    /// Head in the data section
    data_head: u64,
    /// Userspace written tail
    data_tail: u64,
    /// Where the buffer starts
    data_offset: u64,
    /// Data buffer size
    data_size: u64,
    // if aux is used, head in the data section
    aux_head: u64,
    // if aux is used, userspace written tail
    aux_tail: u64,
    // if aux is used, where the buffer starts
    aux_offset: u64,
    // if aux is used, data buffer size
    aux_size: u64,
};

pub const PERF = struct {
    pub const TYPE = enum(u32) {
        HARDWARE,
        SOFTWARE,
        TRACEPOINT,
        HW_CACHE,
        RAW,
        BREAKPOINT,
        MAX,
        _,
    };

    pub const COUNT = struct {
        pub const HW = enum(u32) {
            CPU_CYCLES,
            INSTRUCTIONS,
            CACHE_REFERENCES,
            CACHE_MISSES,
            BRANCH_INSTRUCTIONS,
            BRANCH_MISSES,
            BUS_CYCLES,
            STALLED_CYCLES_FRONTEND,
            STALLED_CYCLES_BACKEND,
            REF_CPU_CYCLES,
            MAX,

            pub const CACHE = enum(u32) {
                L1D,
                L1I,
                LL,
                DTLB,
                ITLB,
                BPU,
                NODE,
                MAX,

                pub const OP = enum(u32) {
                    READ,
                    WRITE,
                    PREFETCH,
                    MAX,
                };

                pub const RESULT = enum(u32) {
                    ACCESS,
                    MISS,
                    MAX,
                };
            };
        };

        pub const SW = enum(u32) {
            CPU_CLOCK,
            TASK_CLOCK,
            PAGE_FAULTS,
            CONTEXT_SWITCHES,
            CPU_MIGRATIONS,
            PAGE_FAULTS_MIN,
            PAGE_FAULTS_MAJ,
            ALIGNMENT_FAULTS,
            EMULATION_FAULTS,
            DUMMY,
            BPF_OUTPUT,
            MAX,
        };
    };

    pub const SAMPLE = struct {
        pub const IP = 1;
        pub const TID = 2;
        pub const TIME = 4;
        pub const ADDR = 8;
        pub const READ = 16;
        pub const CALLCHAIN = 32;
        pub const ID = 64;
        pub const CPU = 128;
        pub const PERIOD = 256;
        pub const STREAM_ID = 512;
        pub const RAW = 1024;
        pub const BRANCH_STACK = 2048;
        pub const REGS_USER = 4096;
        pub const STACK_USER = 8192;
        pub const WEIGHT = 16384;
        pub const DATA_SRC = 32768;
        pub const IDENTIFIER = 65536;
        pub const TRANSACTION = 131072;
        pub const REGS_INTR = 262144;
        pub const PHYS_ADDR = 524288;
        pub const MAX = 1048576;

        pub const BRANCH = struct {
            pub const USER = 1 << 0;
            pub const KERNEL = 1 << 1;
            pub const HV = 1 << 2;
            pub const ANY = 1 << 3;
            pub const ANY_CALL = 1 << 4;
            pub const ANY_RETURN = 1 << 5;
            pub const IND_CALL = 1 << 6;
            pub const ABORT_TX = 1 << 7;
            pub const IN_TX = 1 << 8;
            pub const NO_TX = 1 << 9;
            pub const COND = 1 << 10;
            pub const CALL_STACK = 1 << 11;
            pub const IND_JUMP = 1 << 12;
            pub const CALL = 1 << 13;
            pub const NO_FLAGS = 1 << 14;
            pub const NO_CYCLES = 1 << 15;
            pub const TYPE_SAVE = 1 << 16;
            pub const MAX = 1 << 17;
        };
    };

    pub const RECORD = enum(u32) {
        MMAP = 1,
        LOST = 2,
        COMM = 3,
        EXIT = 4,
        THROTTLE = 5,
        UNTHROTTLE = 6,
        FORK = 7,
        READ = 8,
        SAMPLE = 9,
        MMAP2 = 10,
        AUX = 11,
        ITRACE_START = 12,
        LOST_SAMPLES = 13,
        SWITCH = 14,
        SWITCH_CPU_WIDE = 15,
        NAMESPACES = 16,
        KSYMBOL = 17,
        BPF_EVENT = 18,
        CGROUP = 19,
        TEXT_POKE = 20,
        AUX_OUTPUT_HW_ID = 21,

        const MISC = struct {
            pub const CPU_MODE = enum(u3) {
                UNKNOWN = 0,
                KERNEL = 1,
                USER = 2,
                HYPERVISOR = 3,
                GUEST_KERNEL = 4,
                GUEST_USER = 5,
            };
        };
    };

    pub const FLAG = struct {
        pub const FD_NO_GROUP = 1 << 0;
        pub const FD_OUTPUT = 1 << 1;
        pub const PID_CGROUP = 1 << 2;
        pub const FD_CLOEXEC = 1 << 3;
    };

    pub const EVENT_IOC = struct {
        pub const ENABLE = 9216;
        pub const DISABLE = 9217;
        pub const REFRESH = 9218;
        pub const RESET = 9219;
        pub const PERIOD = 1074275332;
        pub const SET_OUTPUT = 9221;
        pub const SET_FILTER = 1074275334;
        pub const ID = 2148017159;
        pub const SET_BPF = 1074013192;
        pub const PAUSE_OUTPUT = 1074013193;
        pub const QUERY_BPF = 3221758986;
        pub const MODIFY_ATTRIBUTES = 1074275339;
    };

    pub const IOC_FLAG_GROUP = 1;
};

// TODO: Add the rest of the AUDIT defines?
pub const AUDIT = struct {
    pub const ARCH = enum(u32) {
        const CONVENTION_MIPS64_N32 = 0x20000000;
        const @"64BIT" = 0x80000000;
        const LE = 0x40000000;

        AARCH64 = toAudit(.AARCH64, @"64BIT" | LE),
        ALPHA = toAudit(.ALPHA, @"64BIT" | LE),
        ARCOMPACT = toAudit(.ARC_COMPACT, LE),
        ARCOMPACTBE = toAudit(.ARC_COMPACT, 0),
        ARCV2 = toAudit(.ARC_COMPACT2, LE),
        ARCV2BE = toAudit(.ARC_COMPACT2, 0),
        ARM = toAudit(.ARM, LE),
        ARMEB = toAudit(.ARM, 0),
        C6X = toAudit(.TI_C6000, LE),
        C6XBE = toAudit(.TI_C6000, 0),
        CRIS = toAudit(.CRIS, LE),
        CSKY = toAudit(.CSKY, LE),
        FRV = toAudit(.FRV, 0),
        H8300 = toAudit(.H8_300, 0),
        HEXAGON = toAudit(.HEXAGON, 0),
        I386 = toAudit(.@"386", LE),
        IA64 = toAudit(.IA_64, @"64BIT" | LE),
        M32R = toAudit(.M32R, 0),
        M68K = toAudit(.@"68K", 0),
        MICROBLAZE = toAudit(.MICROBLAZE, 0),
        MIPS = toAudit(.MIPS, 0),
        MIPSEL = toAudit(.MIPS, LE),
        MIPS64 = toAudit(.MIPS, @"64BIT"),
        MIPS64N32 = toAudit(.MIPS, @"64BIT" | CONVENTION_MIPS64_N32),
        MIPSEL64 = toAudit(.MIPS, @"64BIT" | LE),
        MIPSEL64N32 = toAudit(.MIPS, @"64BIT" | LE | CONVENTION_MIPS64_N32),
        NDS32 = toAudit(.NDS32, LE),
        NDS32BE = toAudit(.NDS32, 0),
        NIOS2 = toAudit(.ALTERA_NIOS2, LE),
        OPENRISC = toAudit(.OPENRISC, 0),
        PARISC = toAudit(.PARISC, 0),
        PARISC64 = toAudit(.PARISC, @"64BIT"),
        PPC = toAudit(.PPC, 0),
        PPC64 = toAudit(.PPC64, @"64BIT"),
        PPC64LE = toAudit(.PPC64, @"64BIT" | LE),
        RISCV32 = toAudit(.RISCV, LE),
        RISCV64 = toAudit(.RISCV, @"64BIT" | LE),
        S390 = toAudit(.S390, 0),
        S390X = toAudit(.S390, @"64BIT"),
        SH = toAudit(.SH, 0),
        SHEL = toAudit(.SH, LE),
        SH64 = toAudit(.SH, @"64BIT"),
        SHEL64 = toAudit(.SH, @"64BIT" | LE),
        SPARC = toAudit(.SPARC, 0),
        SPARC64 = toAudit(.SPARCV9, @"64BIT"),
        TILEGX = toAudit(.TILEGX, @"64BIT" | LE),
        TILEGX32 = toAudit(.TILEGX, LE),
        TILEPRO = toAudit(.TILEPRO, LE),
        UNICORE = toAudit(.UNICORE, LE),
        X86_64 = toAudit(.X86_64, @"64BIT" | LE),
        XTENSA = toAudit(.XTENSA, 0),
        LOONGARCH32 = toAudit(.LOONGARCH, LE),
        LOONGARCH64 = toAudit(.LOONGARCH, @"64BIT" | LE),

        fn toAudit(em: elf.EM, flags: u32) u32 {
            return @intFromEnum(em) | flags;
        }

        pub const current: AUDIT.ARCH = switch (native_arch) {
            .arm, .thumb => .ARM,
            .armeb, .thumbeb => .ARMEB,
            .aarch64 => .AARCH64,
            .arc => .ARCV2,
            .arceb => .ARCV2BE,
            .csky => .CSKY,
            .hexagon => .HEXAGON,
            .loongarch32 => .LOONGARCH32,
            .loongarch64 => .LOONGARCH64,
            .m68k => .M68K,
            .mips => .MIPS,
            .mipsel => .MIPSEL,
            .mips64 => switch (native_abi) {
                .gnuabin32, .muslabin32 => .MIPS64N32,
                else => .MIPS64,
            },
            .mips64el => switch (native_abi) {
                .gnuabin32, .muslabin32 => .MIPSEL64N32,
                else => .MIPSEL64,
            },
            .or1k => .OPENRISC,
            .powerpc => .PPC,
            .powerpc64 => .PPC64,
            .powerpc64le => .PPC64LE,
            .riscv32 => .RISCV32,
            .riscv64 => .RISCV64,
            .sparc => .SPARC,
            .sparc64 => .SPARC64,
            .s390x => .S390X,
            .x86 => .I386,
            .x86_64 => .X86_64,
            .xtensa => .XTENSA,
            else => @compileError("unsupported architecture"),
        };
    };
};

pub const PTRACE = struct {
    pub const TRACEME = 0;
    pub const PEEKTEXT = 1;
    pub const PEEKDATA = 2;
    pub const PEEKUSER = 3;
    pub const POKETEXT = 4;
    pub const POKEDATA = 5;
    pub const POKEUSER = 6;
    pub const CONT = 7;
    pub const KILL = 8;
    pub const SINGLESTEP = 9;
    pub const GETREGS = 12;
    pub const SETREGS = 13;
    pub const GETFPREGS = 14;
    pub const SETFPREGS = 15;
    pub const ATTACH = 16;
    pub const DETACH = 17;
    pub const GETFPXREGS = 18;
    pub const SETFPXREGS = 19;
    pub const SYSCALL = 24;
    pub const SETOPTIONS = 0x4200;
    pub const GETEVENTMSG = 0x4201;
    pub const GETSIGINFO = 0x4202;
    pub const SETSIGINFO = 0x4203;
    pub const GETREGSET = 0x4204;
    pub const SETREGSET = 0x4205;
    pub const SEIZE = 0x4206;
    pub const INTERRUPT = 0x4207;
    pub const LISTEN = 0x4208;
    pub const PEEKSIGINFO = 0x4209;
    pub const GETSIGMASK = 0x420a;
    pub const SETSIGMASK = 0x420b;
    pub const SECCOMP_GET_FILTER = 0x420c;
    pub const SECCOMP_GET_METADATA = 0x420d;
    pub const GET_SYSCALL_INFO = 0x420e;

    pub const EVENT = struct {
        pub const FORK = 1;
        pub const VFORK = 2;
        pub const CLONE = 3;
        pub const EXEC = 4;
        pub const VFORK_DONE = 5;
        pub const EXIT = 6;
        pub const SECCOMP = 7;
        pub const STOP = 128;
    };

    pub const O = struct {
        pub const TRACESYSGOOD = 1;
        pub const TRACEFORK = 1 << PTRACE.EVENT.FORK;
        pub const TRACEVFORK = 1 << PTRACE.EVENT.VFORK;
        pub const TRACECLONE = 1 << PTRACE.EVENT.CLONE;
        pub const TRACEEXEC = 1 << PTRACE.EVENT.EXEC;
        pub const TRACEVFORKDONE = 1 << PTRACE.EVENT.VFORK_DONE;
        pub const TRACEEXIT = 1 << PTRACE.EVENT.EXIT;
        pub const TRACESECCOMP = 1 << PTRACE.EVENT.SECCOMP;

        pub const EXITKILL = 1 << 20;
        pub const SUSPEND_SECCOMP = 1 << 21;

        pub const MASK = 0x000000ff | PTRACE.O.EXITKILL | PTRACE.O.SUSPEND_SECCOMP;
    };
};

/// For futex2_waitv and futex2_requeue. Arrays of `futex2_waitone` allow
/// waiting on multiple futexes in one call.
pub const futex2_waitone = extern struct {
    /// Expected value at uaddr, should match size of futex.
    val: u64,
    /// User address to wait on.  Top-bits must be 0 on 32-bit.
    uaddr: u64,
    /// Flags for this waiter.
    flags: FUTEX2_FLAGS,
    /// Reserved member to preserve alignment.
    __reserved: u32 = 0,
};

pub const cache_stat_range = extern struct {
    off: u64,
    len: u64,
};

pub const cache_stat = extern struct {
    /// Number of cached pages.
    cache: u64,
    /// Number of dirty pages.
    dirty: u64,
    /// Number of pages marked for writeback.
    writeback: u64,
    /// Number of pages evicted from the cache.
    evicted: u64,
    /// Number of recently evicted pages.
    /// A page is recently evicted if its last eviction was recent enough that its
    /// reentry to the cache would indicate that it is actively being used by the
    /// system, and that there is memory pressure on the system.
    recently_evicted: u64,
};

pub const SHADOW_STACK = struct {
    /// Set up a restore token in the shadow stack.
    pub const SET_TOKEN: u64 = 1 << 0;
};

pub const msghdr = extern struct {
    name: ?*sockaddr,
    namelen: socklen_t,
    iov: [*]iovec,
    /// The kernel and glibc use `usize` for this field; POSIX and musl use `c_int`.
    iovlen: usize,
    control: ?*anyopaque,
    /// The kernel and glibc use `usize` for this field; POSIX and musl use `socklen_t`.
    controllen: usize,
    flags: u32,
};

pub const msghdr_const = extern struct {
    name: ?*const sockaddr,
    namelen: socklen_t,
    iov: [*]const iovec_const,
    iovlen: usize,
    control: ?*const anyopaque,
    controllen: usize,
    flags: u32,
};

// https://git.kernel.org/pub/scm/linux/kernel/git/torvalds/linux.git/tree/include/linux/socket.h?id=b320789d6883cc00ac78ce83bccbfe7ed58afcf0#n105
pub const cmsghdr = extern struct {
    /// The kernel and glibc use `usize` for this field; musl uses `socklen_t`.
    len: usize,
    level: i32,
    type: i32,
};

inline fn fd_to_usize(fd: fd_t) usize {
    return @as(usize, @bitCast(@as(isize, fd)));
}



---
File: /std/os/plan9.zig
---

const std = @import("../std.zig");
const builtin = @import("builtin");

pub const fd_t = i32;

pub const STDIN_FILENO = 0;
pub const STDOUT_FILENO = 1;
pub const STDERR_FILENO = 2;
pub const PATH_MAX = 1023;
pub const syscall_bits = switch (builtin.cpu.arch) {
    .x86_64 => @import("plan9/x86_64.zig"),
    else => @compileError("more plan9 syscall implementations (needs more inline asm in stage2"),
};
/// Ported from /sys/include/ape/errno.h
pub const E = enum(u16) {
    SUCCESS = 0,
    DOM = 1000,
    RANGE = 1001,
    PLAN9 = 1002,

    @"2BIG" = 1,
    ACCES = 2,
    AGAIN = 3,
    // WOULDBLOCK = 3, // TODO errno.h has 2 names for 3
    BADF = 4,
    BUSY = 5,
    CHILD = 6,
    DEADLK = 7,
    EXIST = 8,
    FAULT = 9,
    FBIG = 10,
    INTR = 11,
    INVAL = 12,
    IO = 13,
    ISDIR = 14,
    MFILE = 15,
    MLINK = 16,
    NAMETOOLONG = 17,
    NFILE = 18,
    NODEV = 19,
    NOENT = 20,
    NOEXEC = 21,
    NOLCK = 22,
    NOMEM = 23,
    NOSPC = 24,
    NOSYS = 25,
    NOTDIR = 26,
    NOTEMPTY = 27,
    NOTTY = 28,
    NXIO = 29,
    PERM = 30,
    PIPE = 31,
    ROFS = 32,
    SPIPE = 33,
    SRCH = 34,
    XDEV = 35,

    // bsd networking software
    NOTSOCK = 36,
    PROTONOSUPPORT = 37,
    // PROTOTYPE = 37, // TODO errno.h has two names for 37
    CONNREFUSED = 38,
    AFNOSUPPORT = 39,
    NOBUFS = 40,
    OPNOTSUPP = 41,
    ADDRINUSE = 42,
    DESTADDRREQ = 43,
    MSGSIZE = 44,
    NOPROTOOPT = 45,
    SOCKTNOSUPPORT = 46,
    PFNOSUPPORT = 47,
    ADDRNOTAVAIL = 48,
    NETDOWN = 49,
    NETUNREACH = 50,
    NETRESET = 51,
    CONNABORTED = 52,
    ISCONN = 53,
    NOTCONN = 54,
    SHUTDOWN = 55,
    TOOMANYREFS = 56,
    TIMEDOUT = 57,
    HOSTDOWN = 58,
    HOSTUNREACH = 59,
    GREG = 60,

    // These added in 1003.1b-1993
    CANCELED = 61,
    INPROGRESS = 62,

    // We just add these to be compatible with std.os, which uses them,
    // They should never get used.
    DQUOT,
    CONNRESET,
    OVERFLOW,
    LOOP,
    TXTBSY,
};

/// Get the errno from a syscall return value. SUCCESS means no error.
pub fn errno(r: usize) E {
    const signed_r: isize = @bitCast(r);
    const int = if (signed_r > -4096 and signed_r < 0) -signed_r else 0;
    return @enumFromInt(int);
}

// The max bytes that can be in the errstr buff
pub const ERRMAX = 128;
var errstr_buf: [ERRMAX]u8 = undefined;
/// Gets whatever the last errstr was
pub fn errstr() []const u8 {
    _ = syscall_bits.syscall2(.ERRSTR, @intFromPtr(&errstr_buf), ERRMAX);
    return std.mem.span(@as([*:0]u8, @ptrCast(&errstr_buf)));
}
pub const Plink = anyopaque;
pub const Tos = extern struct {
    /// Per process profiling
    prof: extern struct {
        /// known to be 0(ptr)
        pp: *Plink,
        /// known to be 4(ptr)
        next: *Plink,
        last: *Plink,
        first: *Plink,
        pid: u32,
        what: u32,
    },
    /// cycle clock frequency if there is one, 0 otherwise
    cyclefreq: u64,
    /// cycles spent in kernel
    kcycles: i64,
    /// cycles spent in process (kernel + user)
    pcycles: i64,
    /// might as well put the pid here
    pid: u32,
    clock: u32,
    // top of stack is here
};

pub var tos: *Tos = undefined; // set in start.zig
pub fn getpid() u32 {
    return tos.pid;
}
pub const SIG = struct {
    /// hangup
    pub const HUP = 1;
    /// interrupt
    pub const INT = 2;
    /// quit
    pub const QUIT = 3;
    /// illegal instruction (not reset when caught)
    pub const ILL = 4;
    /// used by abort
    pub const ABRT = 5;
    /// floating point exception
    pub const FPE = 6;
    /// kill (cannot be caught or ignored)
    pub const KILL = 7;
    /// segmentation violation
    pub const SEGV = 8;
    /// write on a pipe with no one to read it
    pub const PIPE = 9;
    /// alarm clock
    pub const ALRM = 10;
    /// software termination signal from kill
    pub const TERM = 11;
    /// user defined signal 1
    pub const USR1 = 12;
    /// user defined signal 2
    pub const USR2 = 13;
    /// bus error
    pub const BUS = 14;
    // The following symbols must be defined, but the signals needn't be supported
    /// child process terminated or stopped
    pub const CHLD = 15;
    /// continue if stopped
    pub const CONT = 16;
    /// stop
    pub const STOP = 17;
    /// interactive stop
    pub const TSTP = 18;
    /// read from ctl tty by member of background
    pub const TTIN = 19;
    /// write to ctl tty by member of background
    pub const TTOU = 20;
};
pub const sigset_t = c_long;
pub const siginfo_t = c_long;
// TODO plan9 doesn't have sigaction_fn. Sigaction is not a union, but we include it here to be compatible.
pub const Sigaction = extern struct {
    pub const handler_fn = *const fn (i32) callconv(.c) void;
    pub const sigaction_fn = *const fn (i32, *const siginfo_t, ?*anyopaque) callconv(.c) void;

    handler: extern union {
        handler: ?handler_fn,
        sigaction: ?sigaction_fn,
    },
    mask: sigset_t,
    flags: c_int,
};
pub const AT = struct {
    pub const FDCWD = -100; // we just make up a constant; FDCWD and openat don't actually exist in plan9
};
// Plan 9 doesn't do signals.  This is just needed to get through start.zig.
pub fn sigemptyset() sigset_t {
    return 0;
}
// TODO implement sigaction
// right now it is just a shim to allow using start.zig code
pub fn sigaction(sig: u6, noalias act: ?*const Sigaction, noalias oact: ?*Sigaction) usize {
    _ = oact;
    _ = act;
    _ = sig;
    return 0;
}
pub const SYS = enum(usize) {
    SYSR1 = 0,
    _ERRSTR = 1,
    BIND = 2,
    CHDIR = 3,
    CLOSE = 4,
    DUP = 5,
    ALARM = 6,
    EXEC = 7,
    EXITS = 8,
    _FSESSION = 9,
    FAUTH = 10,
    _FSTAT = 11,
    SEGBRK = 12,
    _MOUNT = 13,
    OPEN = 14,
    _READ = 15,
    OSEEK = 16,
    SLEEP = 17,
    _STAT = 18,
    RFORK = 19,
    _WRITE = 20,
    PIPE = 21,
    CREATE = 22,
    FD2PATH = 23,
    BRK_ = 24,
    REMOVE = 25,
    _WSTAT = 26,
    _FWSTAT = 27,
    NOTIFY = 28,
    NOTED = 29,
    SEGATTACH = 30,
    SEGDETACH = 31,
    SEGFREE = 32,
    SEGFLUSH = 33,
    RENDEZVOUS = 34,
    UNMOUNT = 35,
    _WAIT = 36,
    SEMACQUIRE = 37,
    SEMRELEASE = 38,
    SEEK = 39,
    FVERSION = 40,
    ERRSTR = 41,
    STAT = 42,
    FSTAT = 43,
    WSTAT = 44,
    FWSTAT = 45,
    MOUNT = 46,
    AWAIT = 47,
    PREAD = 50,
    PWRITE = 51,
    TSEMACQUIRE = 52,
    _NSEC = 53,
};

pub fn write(fd: i32, buf: [*]const u8, count: usize) usize {
    return syscall_bits.syscall4(.PWRITE, @bitCast(@as(isize, fd)), @intFromPtr(buf), count, @bitCast(@as(isize, -1)));
}
pub fn pwrite(fd: i32, buf: [*]const u8, count: usize, offset: isize) usize {
    return syscall_bits.syscall4(.PWRITE, @bitCast(@as(isize, fd)), @intFromPtr(buf), count, @bitCast(offset));
}

pub fn read(fd: i32, buf: [*]const u8, count: usize) usize {
    return syscall_bits.syscall4(.PREAD, @bitCast(@as(isize, fd)), @intFromPtr(buf), count, @bitCast(@as(isize, -1)));
}
pub fn pread(fd: i32, buf: [*]const u8, count: usize, offset: isize) usize {
    return syscall_bits.syscall4(.PREAD, @bitCast(@as(isize, fd)), @intFromPtr(buf), count, @bitCast(offset));
}

pub fn open(path: [*:0]const u8, flags: u32) usize {
    return syscall_bits.syscall2(.OPEN, @intFromPtr(path), @bitCast(@as(isize, flags)));
}

pub fn openat(dirfd: i32, path: [*:0]const u8, flags: u32, _: mode_t) usize {
    // we skip perms because only create supports perms
    if (dirfd == AT.FDCWD) { // openat(AT_FDCWD, ...) == open(...)
        return open(path, flags);
    }
    var dir_path_buf: [std.fs.max_path_bytes]u8 = undefined;
    var total_path_buf: [std.fs.max_path_bytes + 1]u8 = undefined;
    const rc = fd2path(dirfd, &dir_path_buf, std.fs.max_path_bytes);
    if (rc != 0) return rc;
    var fba = std.heap.FixedBufferAllocator.init(&total_path_buf);
    var alloc = fba.allocator();
    const dir_path = std.mem.span(@as([*:0]u8, @ptrCast(&dir_path_buf)));
    const total_path = std.fs.path.join(alloc, &.{ dir_path, std.mem.span(path) }) catch unreachable; // the allocation shouldn't fail because it should not exceed max_path_bytes
    fba.reset();
    const total_path_z = alloc.dupeZ(u8, total_path) catch unreachable; // should not exceed max_path_bytes + 1
    return open(total_path_z.ptr, flags);
}

pub fn fd2path(fd: i32, buf: [*]u8, nbuf: usize) usize {
    return syscall_bits.syscall3(.FD2PATH, @bitCast(@as(isize, fd)), @intFromPtr(buf), nbuf);
}

pub fn create(path: [*:0]const u8, omode: mode_t, perms: usize) usize {
    return syscall_bits.syscall3(.CREATE, @intFromPtr(path), @bitCast(@as(isize, omode)), perms);
}

pub fn exit(status: u8) noreturn {
    if (status == 0) {
        exits(null);
    } else {
        // TODO plan9 does not have exit codes. You either exit with 0 or a string
        const arr: [1:0]u8 = .{status};
        exits(&arr);
    }
}

pub fn exits(status: ?[*:0]const u8) noreturn {
    _ = syscall_bits.syscall1(.EXITS, if (status) |s| @intFromPtr(s) else 0);
    unreachable;
}

pub fn close(fd: i32) usize {
    return syscall_bits.syscall1(.CLOSE, @bitCast(@as(isize, fd)));
}
pub const mode_t = i32;

pub const AccessMode = enum(u2) {
    RDONLY,
    WRONLY,
    RDWR,
    EXEC,
};

pub const O = packed struct(u32) {
    access: AccessMode,
    _2: u2 = 0,
    TRUNC: bool = false,
    CEXEC: bool = false,
    RCLOSE: bool = false,
    _7: u5 = 0,
    EXCL: bool = false,
    _: u19 = 0,
};

pub const ExecData = struct {
    pub extern const etext: anyopaque;
    pub extern const edata: anyopaque;
    pub extern const end: anyopaque;
};

/// Brk sets the system's idea of the lowest bss location not
/// used by the program (called the break) to addr rounded up to
/// the next multiple of 8 bytes.  Locations not less than addr
/// and below the stack pointer may cause a memory violation if
/// accessed. -9front brk(2)
pub fn brk_(addr: usize) i32 {
    return @intCast(syscall_bits.syscall1(.BRK_, addr));
}
var bloc: usize = 0;
var bloc_max: usize = 0;

pub fn sbrk(n: usize) usize {
    if (bloc == 0) {
        // we are at the start
        bloc = @intFromPtr(&ExecData.end);
        bloc_max = @intFromPtr(&ExecData.end);
    }
    const bl = std.mem.alignForward(usize, bloc, std.heap.pageSize());
    const n_aligned = std.mem.alignForward(usize, n, std.heap.pageSize());
    if (bl + n_aligned > bloc_max) {
        // we need to allocate
        if (brk_(bl + n_aligned) < 0) return 0;
        bloc_max = bl + n_aligned;
    }
    bloc = bloc + n_aligned;
    return bl;
}



---
File: /std/os/uefi.zig
---

const std = @import("../std.zig");
const assert = std.debug.assert;

/// A protocol is an interface identified by a GUID.
pub const protocol = @import("uefi/protocol.zig");
pub const DevicePath = @import("uefi/device_path.zig").DevicePath;
pub const hii = @import("uefi/hii.zig");

/// Status codes returned by EFI interfaces
pub const Status = @import("uefi/status.zig").Status;
pub const Error = UnexpectedError || Status.Error;
pub const tables = @import("uefi/tables.zig");

/// The memory type to allocate when using the pool.
/// Defaults to `.loader_data`, the default data allocation type
/// used by UEFI applications to allocate pool memory.
pub var efi_pool_memory_type: tables.MemoryType = .loader_data;
pub const pool_allocator = @import("uefi/pool_allocator.zig").pool_allocator;
pub const raw_pool_allocator = @import("uefi/pool_allocator.zig").raw_pool_allocator;

/// The EFI image's handle that is passed to its entry point.
pub var handle: Handle = undefined;

/// A pointer to the EFI System Table that is passed to the EFI image's entry point.
pub var system_table: *tables.SystemTable = undefined;

/// UEFI's memory interfaces exclusively act on 4096-byte pages.
pub const Page = [4096]u8;

/// A handle to an event structure.
pub const Event = *opaque {};

pub const EventRegistration = *const opaque {};

pub const EventType = packed struct(u32) {
    lo_context: u8 = 0,
    /// If an event of this type is not already in the signaled state, then
    /// the event’s NotificationFunction will be queued at the event’s NotifyTpl
    /// whenever the event is being waited on via EFI_BOOT_SERVICES.WaitForEvent()
    /// or EFI_BOOT_SERVICES.CheckEvent() .
    wait: bool = false,
    /// The event’s NotifyFunction is queued whenever the event is signaled.
    signal: bool = false,
    hi_context: u20 = 0,
    /// The event is allocated from runtime memory. If an event is to be signaled
    /// after the call to EFI_BOOT_SERVICES.ExitBootServices() the event’s data
    /// structure and notification function need to be allocated from runtime
    /// memory.
    runtime: bool = false,
    timer: bool = false,

    /// This event should not be combined with any other event types. This event
    /// type is functionally equivalent to the EFI_EVENT_GROUP_EXIT_BOOT_SERVICES
    /// event group.
    pub const signal_exit_boot_services: EventType = .{
        .signal = true,
        .lo_context = 1,
    };

    /// The event is to be notified by the system when SetVirtualAddressMap()
    /// is performed. This event type is a composite of EVT_NOTIFY_SIGNAL,
    /// EVT_RUNTIME, and EVT_RUNTIME_CONTEXT and should not be combined with
    /// any other event types.
    pub const signal_virtual_address_change: EventType = .{
        .runtime = true,
        .hi_context = 0x20000,
        .signal = true,
        .lo_context = 2,
    };
};

/// The calling convention used for all external functions part of the UEFI API.
pub const cc: std.builtin.CallingConvention = switch (@import("builtin").target.cpu.arch) {
    .x86_64 => .{ .x86_64_win = .{} },
    else => .c,
};

pub const MacAddress = extern struct {
    address: [32]u8,
};

pub const Ipv4Address = extern struct {
    address: [4]u8,
};

pub const Ipv6Address = extern struct {
    address: [16]u8,
};

pub const IpAddress = extern union {
    v4: Ipv4Address,
    v6: Ipv6Address,
};

/// GUIDs are align(8) unless otherwise specified.
pub const Guid = extern struct {
    comptime {
        std.debug.assert(std.mem.Alignment.of(Guid) == .@"8");
    }

    time_low: u32 align(8),
    time_mid: u16,
    time_high_and_version: u16,
    clock_seq_high_and_reserved: u8,
    clock_seq_low: u8,
    node: [6]u8,

    /// Format GUID into hexadecimal lowercase xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx format
    pub fn format(self: Guid, writer: *std.Io.Writer) std.Io.Writer.Error!void {
        return writer.print("{x:0>8}-{x:0>4}-{x:0>4}-{x:0>2}{x:0>2}-{x}", .{
            self.time_low,
            self.time_mid,
            self.time_high_and_version,
            self.clock_seq_high_and_reserved,
            self.clock_seq_low,
            self.node,
        });
    }

    pub fn eql(a: Guid, b: Guid) bool {
        return a.time_low == b.time_low and
            a.time_mid == b.time_mid and
            a.time_high_and_version == b.time_high_and_version and
            a.clock_seq_high_and_reserved == b.clock_seq_high_and_reserved and
            a.clock_seq_low == b.clock_seq_low and
            std.mem.eql(u8, &a.node, &b.node);
    }
};

/// An EFI Handle represents a collection of related interfaces.
pub const Handle = *opaque {};

/// This structure represents time information.
pub const Time = extern struct {
    /// 1900 - 9999
    year: u16,

    /// 1 - 12
    month: u8,

    /// 1 - 31
    day: u8,

    /// 0 - 23
    hour: u8,

    /// 0 - 59
    minute: u8,

    /// 0 - 59
    second: u8,

    _pad1: u8,

    /// 0 - 999999999
    nanosecond: u32,

    /// The time's offset in minutes from UTC.
    /// Allowed values are -1440 to 1440 or unspecified_timezone
    timezone: i16,
    daylight: packed struct(u8) {
        /// If true, the time has been adjusted for daylight savings time.
        in_daylight: bool,

        /// If true, the time is affected by daylight savings time.
        adjust_daylight: bool,

        _: u6,
    },

    _pad2: u8,

    comptime {
        std.debug.assert(@sizeOf(Time) == 16);
    }

    /// Time is to be interpreted as local time
    pub const unspecified_timezone: i16 = 0x7ff;

    fn daysInYear(year: u16, max_month: u4) u9 {
        var days: u9 = 0;
        var month: u4 = 0;
        while (month < max_month) : (month += 1) {
            days += std.time.epoch.getDaysInMonth(year, @enumFromInt(month + 1));
        }
        return days;
    }

    pub fn toEpoch(self: std.os.uefi.Time) u64 {
        var year: u16 = 0;
        var days: u32 = 0;

        while (year < (self.year - 1971)) : (year += 1) {
            days += daysInYear(year + 1970, 12);
        }

        days += daysInYear(self.year, @as(u4, @intCast(self.month)) - 1) + self.day;
        const hours: u64 = self.hour + (days * 24);
        const minutes: u64 = self.minute + (hours * 60);
        const seconds: u64 = self.second + (minutes * std.time.s_per_min);
        return self.nanosecond + (seconds * std.time.ns_per_s);
    }
};

/// Capabilities of the clock device
pub const TimeCapabilities = extern struct {
    /// Resolution in Hz
    resolution: u32,

    /// Accuracy in an error rate of 1e-6 parts per million.
    accuracy: u32,

    /// If true, a time set operation clears the device's time below the resolution level.
    sets_to_zero: bool,
};

/// File Handle as specified in the EFI Shell Spec
pub const FileHandle = *opaque {};

test "GUID formatting" {
    const bytes = [_]u8{ 137, 60, 203, 50, 128, 128, 124, 66, 186, 19, 80, 73, 135, 59, 194, 135 };
    const guid: Guid = @bitCast(bytes);

    const str = try std.fmt.allocPrint(std.testing.allocator, "{f}", .{guid});
    defer std.testing.allocator.free(str);

    try std.testing.expectEqualStrings(str, "32cb3c89-8080-427c-ba13-5049873bc287");
}

test {
    _ = tables;
    _ = protocol;
}

pub const UnexpectedError = error{Unexpected};

pub fn unexpectedStatus(status: Status) UnexpectedError {
    // TODO: debug printing the encountered error? maybe handle warnings?
    _ = status;
    return error.Unexpected;
}



---
File: /std/os/wasi.zig
---

//! wasi_snapshot_preview1 spec available (in witx format) here:
//! * typenames -- https://github.com/WebAssembly/WASI/blob/main/legacy/preview1/witx/typenames.witx
//! * module -- https://github.com/WebAssembly/WASI/blob/main/legacy/preview1/witx/wasi_snapshot_preview1.witx
//! Note that libc API does *not* go in this file. wasi libc API goes into std/c.zig instead.
const builtin = @import("builtin");
const std = @import("std");
const assert = std.debug.assert;

comptime {
    if (builtin.os.tag == .wasi) {
        assert(@alignOf(i8) == 1);
        assert(@alignOf(u8) == 1);
        assert(@alignOf(i16) == 2);
        assert(@alignOf(u16) == 2);
        assert(@alignOf(i32) == 4);
        assert(@alignOf(u32) == 4);
        assert(@alignOf(i64) == 8);
        assert(@alignOf(u64) == 8);
    }
}

pub const iovec_t = std.posix.iovec;
pub const ciovec_t = std.posix.iovec_const;

pub extern "wasi_snapshot_preview1" fn args_get(argv: [*][*:0]u8, argv_buf: [*]u8) errno_t;
pub extern "wasi_snapshot_preview1" fn args_sizes_get(argc: *usize, argv_buf_size: *usize) errno_t;

pub extern "wasi_snapshot_preview1" fn clock_res_get(clock_id: clockid_t, resolution: *timestamp_t) errno_t;
pub extern "wasi_snapshot_preview1" fn clock_time_get(clock_id: clockid_t, precision: timestamp_t, timestamp: *timestamp_t) errno_t;

pub extern "wasi_snapshot_preview1" fn environ_get(environ: [*][*:0]u8, environ_buf: [*]u8) errno_t;
pub extern "wasi_snapshot_preview1" fn environ_sizes_get(environ_count: *usize, environ_buf_size: *usize) errno_t;

pub extern "wasi_snapshot_preview1" fn fd_advise(fd: fd_t, offset: filesize_t, len: filesize_t, advice: advice_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_allocate(fd: fd_t, offset: filesize_t, len: filesize_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_close(fd: fd_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_datasync(fd: fd_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_pread(fd: fd_t, iovs: [*]const iovec_t, iovs_len: usize, offset: filesize_t, nread: *usize) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_pwrite(fd: fd_t, iovs: [*]const ciovec_t, iovs_len: usize, offset: filesize_t, nwritten: *usize) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_read(fd: fd_t, iovs: [*]const iovec_t, iovs_len: usize, nread: *usize) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_readdir(fd: fd_t, buf: [*]u8, buf_len: usize, cookie: dircookie_t, bufused: *usize) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_renumber(from: fd_t, to: fd_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_seek(fd: fd_t, offset: filedelta_t, whence: whence_t, newoffset: *filesize_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_sync(fd: fd_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_tell(fd: fd_t, newoffset: *filesize_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_write(fd: fd_t, iovs: [*]const ciovec_t, iovs_len: usize, nwritten: *usize) errno_t;

pub extern "wasi_snapshot_preview1" fn fd_fdstat_get(fd: fd_t, buf: *fdstat_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_fdstat_set_flags(fd: fd_t, flags: fdflags_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_fdstat_set_rights(fd: fd_t, fs_rights_base: rights_t, fs_rights_inheriting: rights_t) errno_t;

pub extern "wasi_snapshot_preview1" fn fd_filestat_get(fd: fd_t, buf: *filestat_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_filestat_set_size(fd: fd_t, st_size: filesize_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_filestat_set_times(fd: fd_t, st_atim: timestamp_t, st_mtim: timestamp_t, fstflags: fstflags_t) errno_t;

pub extern "wasi_snapshot_preview1" fn fd_prestat_get(fd: fd_t, buf: *prestat_t) errno_t;
pub extern "wasi_snapshot_preview1" fn fd_prestat_dir_name(fd: fd_t, path: [*]u8, path_len: usize) errno_t;

pub extern "wasi_snapshot_preview1" fn path_create_directory(fd: fd_t, path: [*]const u8, path_len: usize) errno_t;
pub extern "wasi_snapshot_preview1" fn path_filestat_get(fd: fd_t, flags: lookupflags_t, path: [*]const u8, path_len: usize, buf: *filestat_t) errno_t;
pub extern "wasi_snapshot_preview1" fn path_filestat_set_times(fd: fd_t, flags: lookupflags_t, path: [*]const u8, path_len: usize, st_atim: timestamp_t, st_mtim: timestamp_t, fstflags: fstflags_t) errno_t;
pub extern "wasi_snapshot_preview1" fn path_link(old_fd: fd_t, old_flags: lookupflags_t, old_path: [*]const u8, old_path_len: usize, new_fd: fd_t, new_path: [*]const u8, new_path_len: usize) errno_t;
pub extern "wasi_snapshot_preview1" fn path_open(dirfd: fd_t, dirflags: lookupflags_t, path: [*]const u8, path_len: usize, oflags: oflags_t, fs_rights_base: rights_t, fs_rights_inheriting: rights_t, fs_flags: fdflags_t, fd: *fd_t) errno_t;
pub extern "wasi_snapshot_preview1" fn path_readlink(fd: fd_t, path: [*]const u8, path_len: usize, buf: [*]u8, buf_len: usize, bufused: *usize) errno_t;
pub extern "wasi_snapshot_preview1" fn path_remove_directory(fd: fd_t, path: [*]const u8, path_len: usize) errno_t;
pub extern "wasi_snapshot_preview1" fn path_rename(old_fd: fd_t, old_path: [*]const u8, old_path_len: usize, new_fd: fd_t, new_path: [*]const u8, new_path_len: usize) errno_t;
pub extern "wasi_snapshot_preview1" fn path_symlink(old_path: [*]const u8, old_path_len: usize, fd: fd_t, new_path: [*]const u8, new_path_len: usize) errno_t;
pub extern "wasi_snapshot_preview1" fn path_unlink_file(fd: fd_t, path: [*]const u8, path_len: usize) errno_t;

pub extern "wasi_snapshot_preview1" fn poll_oneoff(in: *const subscription_t, out: *event_t, nsubscriptions: usize, nevents: *usize) errno_t;

pub extern "wasi_snapshot_preview1" fn proc_exit(rval: exitcode_t) noreturn;

pub extern "wasi_snapshot_preview1" fn random_get(buf: [*]u8, buf_len: usize) errno_t;

pub extern "wasi_snapshot_preview1" fn sched_yield() errno_t;

pub extern "wasi_snapshot_preview1" fn sock_accept(sock: fd_t, flags: fdflags_t, result_fd: *fd_t) errno_t;
pub extern "wasi_snapshot_preview1" fn sock_recv(sock: fd_t, ri_data: [*]iovec_t, ri_data_len: usize, ri_flags: riflags_t, ro_datalen: *usize, ro_flags: *roflags_t) errno_t;
pub extern "wasi_snapshot_preview1" fn sock_send(sock: fd_t, si_data: [*]const ciovec_t, si_data_len: usize, si_flags: siflags_t, so_datalen: *usize) errno_t;
pub extern "wasi_snapshot_preview1" fn sock_shutdown(sock: fd_t, how: sdflags_t) errno_t;

// As defined in the wasi_snapshot_preview1 spec file:
// https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/witx/typenames.witx
pub const advice_t = enum(u8) {
    NORMAL = 0,
    SEQUENTIAL = 1,
    RANDOM = 2,
    WILLNEED = 3,
    DONTNEED = 4,
    NOREUSE = 5,
};

pub const clockid_t = enum(u32) {
    REALTIME = 0,
    MONOTONIC = 1,
    PROCESS_CPUTIME_ID = 2,
    THREAD_CPUTIME_ID = 3,
};

pub const device_t = u64;

pub const dircookie_t = u64;
pub const DIRCOOKIE_START: dircookie_t = 0;

pub const dirnamlen_t = u32;

pub const dirent_t = extern struct {
    next: dircookie_t,
    ino: inode_t,
    namlen: dirnamlen_t,
    type: filetype_t,
};

pub const errno_t = enum(u16) {
    SUCCESS = 0,
    @"2BIG" = 1,
    ACCES = 2,
    ADDRINUSE = 3,
    ADDRNOTAVAIL = 4,
    AFNOSUPPORT = 5,
    /// This is also the error code used for `WOULDBLOCK`.
    AGAIN = 6,
    ALREADY = 7,
    BADF = 8,
    BADMSG = 9,
    BUSY = 10,
    CANCELED = 11,
    CHILD = 12,
    CONNABORTED = 13,
    CONNREFUSED = 14,
    CONNRESET = 15,
    DEADLK = 16,
    DESTADDRREQ = 17,
    DOM = 18,
    DQUOT = 19,
    EXIST = 20,
    FAULT = 21,
    FBIG = 22,
    HOSTUNREACH = 23,
    IDRM = 24,
    ILSEQ = 25,
    INPROGRESS = 26,
    INTR = 27,
    INVAL = 28,
    IO = 29,
    ISCONN = 30,
    ISDIR = 31,
    LOOP = 32,
    MFILE = 33,
    MLINK = 34,
    MSGSIZE = 35,
    MULTIHOP = 36,
    NAMETOOLONG = 37,
    NETDOWN = 38,
    NETRESET = 39,
    NETUNREACH = 40,
    NFILE = 41,
    NOBUFS = 42,
    NODEV = 43,
    NOENT = 44,
    NOEXEC = 45,
    NOLCK = 46,
    NOLINK = 47,
    NOMEM = 48,
    NOMSG = 49,
    NOPROTOOPT = 50,
    NOSPC = 51,
    NOSYS = 52,
    NOTCONN = 53,
    NOTDIR = 54,
    NOTEMPTY = 55,
    NOTRECOVERABLE = 56,
    NOTSOCK = 57,
    /// This is also the code used for `NOTSUP`.
    OPNOTSUPP = 58,
    NOTTY = 59,
    NXIO = 60,
    OVERFLOW = 61,
    OWNERDEAD = 62,
    PERM = 63,
    PIPE = 64,
    PROTO = 65,
    PROTONOSUPPORT = 66,
    PROTOTYPE = 67,
    RANGE = 68,
    ROFS = 69,
    SPIPE = 70,
    SRCH = 71,
    STALE = 72,
    TIMEDOUT = 73,
    TXTBSY = 74,
    XDEV = 75,
    NOTCAPABLE = 76,
    _,
};

pub const event_t = extern struct {
    userdata: userdata_t,
    @"error": errno_t,
    type: eventtype_t,
    fd_readwrite: eventfdreadwrite_t,
};

pub const eventfdreadwrite_t = extern struct {
    nbytes: filesize_t,
    flags: eventrwflags_t,
};

pub const eventrwflags_t = u16;
pub const EVENT_FD_READWRITE_HANGUP: eventrwflags_t = 0x0001;

pub const eventtype_t = enum(u8) {
    CLOCK = 0,
    FD_READ = 1,
    FD_WRITE = 2,
};

pub const exitcode_t = u32;

pub const fd_t = i32;

pub const fdflags_t = packed struct(u16) {
    APPEND: bool = false,
    DSYNC: bool = false,
    NONBLOCK: bool = false,
    RSYNC: bool = false,
    SYNC: bool = false,
    _: u11 = 0,
};

pub const fdstat_t = extern struct {
    fs_filetype: filetype_t,
    fs_flags: fdflags_t,
    fs_rights_base: rights_t,
    fs_rights_inheriting: rights_t,
};

pub const filedelta_t = i64;

pub const filesize_t = u64;

pub const filestat_t = extern struct {
    dev: device_t,
    ino: inode_t,
    filetype: filetype_t,
    nlink: linkcount_t,
    size: filesize_t,
    atim: timestamp_t,
    mtim: timestamp_t,
    ctim: timestamp_t,
};

pub const filetype_t = enum(u8) {
    UNKNOWN,
    BLOCK_DEVICE,
    CHARACTER_DEVICE,
    DIRECTORY,
    REGULAR_FILE,
    SOCKET_DGRAM,
    SOCKET_STREAM,
    SYMBOLIC_LINK,
    _,
};

pub const fstflags_t = packed struct(u16) {
    ATIM: bool = false,
    ATIM_NOW: bool = false,
    MTIM: bool = false,
    MTIM_NOW: bool = false,
    _: u12 = 0,
};

pub const inode_t = u64;

pub const linkcount_t = u64;

pub const lookupflags_t = packed struct(u32) {
    SYMLINK_FOLLOW: bool = false,
    _: u31 = 0,
};

pub const oflags_t = packed struct(u16) {
    CREAT: bool = false,
    DIRECTORY: bool = false,
    EXCL: bool = false,
    TRUNC: bool = false,
    _: u12 = 0,
};

pub const preopentype_t = enum(u8) {
    DIR = 0,
};

pub const prestat_t = extern struct {
    pr_type: preopentype_t,
    u: prestat_u_t,
};

pub const prestat_dir_t = extern struct {
    pr_name_len: usize,
};

pub const prestat_u_t = extern union {
    dir: prestat_dir_t,
};

pub const riflags_t = u16;
pub const roflags_t = u16;

pub const SOCK = struct {
    pub const RECV_PEEK: riflags_t = 0x0001;
    pub const RECV_WAITALL: riflags_t = 0x0002;

    pub const RECV_DATA_TRUNCATED: roflags_t = 0x0001;
};

pub const rights_t = packed struct(u64) {
    /// The right to invoke fd_datasync. If PATH_OPEN is set, includes the right to invoke
    /// path_open with fdflags_t.dsync.
    FD_DATASYNC: bool = false,
    /// The right to invoke fd_read and sock_recv. If FD_SEEK is set, includes the right to invoke
    /// fd_pread.
    FD_READ: bool = false,
    /// The right to invoke fd_seek. This flag implies FD_TELL.
    FD_SEEK: bool = false,
    /// The right to invoke fd_fdstat_set_flags.
    FD_FDSTAT_SET_FLAGS: bool = false,
    /// The right to invoke fd_sync. If PATH_OPEN is set, includes the right to invoke path_open
    /// with fdflags_t.RSYNC and fdflags_t.DSYNC.
    FD_SYNC: bool = false,
    /// The right to invoke fd_seek in such a way that the file offset remains unaltered (i.e.
    /// whence_t.CUR with offset zero), or to invoke fd_tell.
    FD_TELL: bool = false,
    /// The right to invoke fd_write and sock_send. If FD_SEEK is set, includes the right to invoke
    /// fd_pwrite.
    FD_WRITE: bool = false,
    /// The right to invoke fd_advise.
    FD_ADVISE: bool = false,
    /// The right to invoke fd_allocate.
    FD_ALLOCATE: bool = false,
    /// The right to invoke path_create_directory.
    PATH_CREATE_DIRECTORY: bool = false,
    /// If PATH_OPEN is set, the right to invoke path_open with oflags_t.CREAT.
    PATH_CREATE_FILE: bool = false,
    /// The right to invoke path_link with the file descriptor as the source directory.
    PATH_LINK_SOURCE: bool = false,
    /// The right to invoke path_link with the file descriptor as the target directory.
    PATH_LINK_TARGET: bool = false,
    /// The right to invoke path_open.
    PATH_OPEN: bool = false,
    /// The right to invoke fd_readdir.
    FD_READDIR: bool = false,
    /// The right to invoke path_readlink.
    PATH_READLINK: bool = false,
    /// The right to invoke path_rename with the file descriptor as the source directory.
    PATH_RENAME_SOURCE: bool = false,
    /// The right to invoke path_rename with the file descriptor as the target directory.
    PATH_RENAME_TARGET: bool = false,
    /// The right to invoke path_filestat_get.
    PATH_FILESTAT_GET: bool = false,
    /// The right to change a file's size. If PATH_OPEN is set, includes the right to invoke
    /// path_open with oflags_t.TRUNC. Note: there is no function named path_filestat_set_size.
    /// This follows POSIX design, which only has ftruncate and does not provide ftruncateat. While
    /// such function would be desirable from the API design perspective, there are virtually no
    /// use cases for it since no code written for POSIX systems would use it. Moreover,
    /// implementing it would require multiple syscalls, leading to inferior performance.
    PATH_FILESTAT_SET_SIZE: bo
```
