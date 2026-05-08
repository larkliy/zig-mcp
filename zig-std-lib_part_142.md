```

        code: c_int,
        pid: c_int,
        uid: uid_t,
        status: c_int,
        addr: *allowzero anyopaque,
        value: sigval,
        band: c_long,
        __spare__: [7]c_int,
    },
    .haiku => extern struct {
        signo: SIG,
        code: i32,
        errno: i32,

        pid: pid_t,
        uid: uid_t,
        addr: *allowzero anyopaque,
    },
    .openbsd => extern struct {
        signo: SIG,
        code: c_int,
        errno: c_int,
        data: extern union {
            proc: extern struct {
                pid: pid_t,
                pdata: extern union {
                    kill: extern struct {
                        uid: uid_t,
                        value: sigval,
                    },
                    cld: extern struct {
                        utime: clock_t,
                        stime: clock_t,
                        status: c_int,
                    },
                },
            },
            fault: extern struct {
                addr: *allowzero anyopaque,
                trapno: c_int,
            },
            __pad: [128 - 3 * @sizeOf(c_int)]u8,
        },
    },
    // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/signal.h#L27-L37
    .serenity => extern struct {
        signo: SIG,
        code: c_int,
        errno: c_int,
        pid: pid_t,
        uid: uid_t,
        addr: ?*anyopaque,
        status: c_int,
        band: c_int,
        value: sigval,
    },
    else => void,
};
pub const sigset_t = switch (native_os) {
    .linux => [1024 / @bitSizeOf(c_ulong)]c_ulong, // glibc and musl present a 1024-bit sigset_t, while kernel's is 128-bit or less.
    .emscripten => emscripten.sigset_t,
    // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/signal.h#L19
    .openbsd, .serenity => u32,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.sigset_t,
    .dragonfly, .netbsd, .illumos, .freebsd => extern struct {
        __bits: [SIG.WORDS]u32,
    },
    .haiku => u64,
    else => u0,
};

pub const sigval = switch (native_os) {
    .linux => linux.sigval,
    // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/signal.h#L22-L25
    .openbsd, .dragonfly, .freebsd, .serenity => extern union {
        int: c_int,
        ptr: ?*anyopaque,
    },
    else => void,
};

pub const addrinfo = if (builtin.abi.isAndroid()) extern struct {
    flags: AI,
    family: i32,
    socktype: i32,
    protocol: i32,
    addrlen: socklen_t,
    canonname: ?[*:0]u8,
    addr: ?*sockaddr,
    next: ?*addrinfo,
} else switch (native_os) {
    .linux, .emscripten => linux.addrinfo,
    .windows => ws2_32.addrinfo,
    .freebsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        flags: AI,
        family: i32,
        socktype: i32,
        protocol: i32,
        addrlen: socklen_t,
        canonname: ?[*:0]u8,
        addr: ?*sockaddr,
        next: ?*addrinfo,
    },
    .illumos => extern struct {
        flags: AI,
        family: i32,
        socktype: i32,
        protocol: i32,
        addrlen: socklen_t,
        canonname: ?[*:0]u8,
        addr: ?*sockaddr,
        next: ?*addrinfo,
    },
    .netbsd => extern struct {
        flags: AI,
        family: i32,
        socktype: i32,
        protocol: i32,
        addrlen: socklen_t,
        canonname: ?[*:0]u8,
        addr: ?*sockaddr,
        next: ?*addrinfo,
    },
    .dragonfly => extern struct {
        flags: AI,
        family: i32,
        socktype: i32,
        protocol: i32,
        addrlen: socklen_t,
        canonname: ?[*:0]u8,
        addr: ?*sockaddr,
        next: ?*addrinfo,
    },
    .haiku => extern struct {
        flags: AI,
        family: i32,
        socktype: i32,
        protocol: i32,
        addrlen: socklen_t,
        canonname: ?[*:0]u8,
        addr: ?*sockaddr,
        next: ?*addrinfo,
    },
    // https://github.com/SerenityOS/serenity/blob/d510d2aeb2facbd8f6c383d70fd1b033e1fee5dd/Userland/Libraries/LibC/netdb.h#L66-L75
    .openbsd, .serenity => extern struct {
        flags: AI,
        family: c_int,
        socktype: c_int,
        protocol: c_int,
        addrlen: socklen_t,
        addr: ?*sockaddr,
        canonname: ?[*:0]u8,
        next: ?*addrinfo,
    },
    else => void,
};
pub const sockaddr = switch (native_os) {
    .linux, .emscripten => linux.sockaddr,
    .windows => ws2_32.sockaddr,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        len: u8,
        family: sa_family_t,
        data: [14]u8,

        pub const SS_MAXSIZE = 128;
        pub const storage = extern struct {
            len: u8 align(8),
            family: sa_family_t,
            padding: [126]u8 = undefined,

            comptime {
                assert(@sizeOf(storage) == SS_MAXSIZE);
                assert(@alignOf(storage) == 8);
            }
        };
        pub const in = extern struct {
            len: u8 = @sizeOf(in),
            family: sa_family_t = AF.INET,
            port: in_port_t,
            addr: u32,
            zero: [8]u8 = [8]u8{ 0, 0, 0, 0, 0, 0, 0, 0 },
        };
        pub const in6 = extern struct {
            len: u8 = @sizeOf(in6),
            family: sa_family_t = AF.INET6,
            port: in_port_t,
            flowinfo: u32,
            addr: [16]u8,
            scope_id: u32,
        };

        /// UNIX domain socket
        pub const un = extern struct {
            len: u8 = @sizeOf(un),
            family: sa_family_t = AF.UNIX,
            path: [104]u8,
        };
    },
    .freebsd => extern struct {
        /// total length
        len: u8,
        /// address family
        family: sa_family_t,
        /// actually longer; address value
        data: [14]u8,

        pub const SS_MAXSIZE = 128;
        pub const storage = extern struct {
            len: u8 align(8),
            family: sa_family_t,
            padding: [126]u8 = undefined,

            comptime {
                assert(@sizeOf(storage) == SS_MAXSIZE);
                assert(@alignOf(storage) == 8);
            }
        };

        pub const in = extern struct {
            len: u8 = @sizeOf(in),
            family: sa_family_t = AF.INET,
            port: in_port_t,
            addr: u32,
            zero: [8]u8 = [8]u8{ 0, 0, 0, 0, 0, 0, 0, 0 },
        };

        pub const in6 = extern struct {
            len: u8 = @sizeOf(in6),
            family: sa_family_t = AF.INET6,
            port: in_port_t,
            flowinfo: u32,
            addr: [16]u8,
            scope_id: u32,
        };

        pub const un = extern struct {
            len: u8 = @sizeOf(un),
            family: sa_family_t = AF.UNIX,
            path: [104]u8,
        };
    },
    .illumos => extern struct {
        /// address family
        family: sa_family_t,

        /// actually longer; address value
        data: [14]u8,

        pub const SS_MAXSIZE = 256;
        pub const storage = extern struct {
            family: sa_family_t align(8),
            padding: [254]u8 = undefined,

            comptime {
                assert(@sizeOf(storage) == SS_MAXSIZE);
                assert(@alignOf(storage) == 8);
            }
        };

        pub const in = extern struct {
            family: sa_family_t = AF.INET,
            port: in_port_t,
            addr: u32,
            zero: [8]u8 = [8]u8{ 0, 0, 0, 0, 0, 0, 0, 0 },
        };

        pub const in6 = extern struct {
            family: sa_family_t = AF.INET6,
            port: in_port_t,
            flowinfo: u32,
            addr: [16]u8,
            scope_id: u32,
            __src_id: u32 = 0,
        };

        /// Definitions for UNIX IPC domain.
        pub const un = extern struct {
            family: sa_family_t = AF.UNIX,
            path: [108]u8,
        };
    },
    .netbsd => extern struct {
        /// total length
        len: u8,
        /// address family
        family: sa_family_t,
        /// actually longer; address value
        data: [14]u8,

        pub const SS_MAXSIZE = 128;
        pub const storage = extern struct {
            len: u8 align(8),
            family: sa_family_t,
            padding: [126]u8 = undefined,

            comptime {
                assert(@sizeOf(storage) == SS_MAXSIZE);
                assert(@alignOf(storage) == 8);
            }
        };

        pub const in = extern struct {
            len: u8 = @sizeOf(in),
            family: sa_family_t = AF.INET,
            port: in_port_t,
            addr: u32,
            zero: [8]u8 = [8]u8{ 0, 0, 0, 0, 0, 0, 0, 0 },
        };

        pub const in6 = extern struct {
            len: u8 = @sizeOf(in6),
            family: sa_family_t = AF.INET6,
            port: in_port_t,
            flowinfo: u32,
            addr: [16]u8,
            scope_id: u32,
        };

        /// Definitions for UNIX IPC domain.
        pub const un = extern struct {
            /// total sockaddr length
            len: u8 = @sizeOf(un),

            family: sa_family_t = AF.LOCAL,

            /// path name
            path: [104]u8,
        };
    },
    .dragonfly => extern struct {
        len: u8,
        family: sa_family_t,
        data: [14]u8,

        pub const SS_MAXSIZE = 128;
        pub const storage = extern struct {
            len: u8 align(8),
            family: sa_family_t,
            padding: [126]u8 = undefined,

            comptime {
                assert(@sizeOf(storage) == SS_MAXSIZE);
                assert(@alignOf(storage) == 8);
            }
        };

        pub const in = extern struct {
            len: u8 = @sizeOf(in),
            family: sa_family_t = AF.INET,
            port: in_port_t,
            addr: u32,
            zero: [8]u8 = [8]u8{ 0, 0, 0, 0, 0, 0, 0, 0 },
        };

        pub const in6 = extern struct {
            len: u8 = @sizeOf(in6),
            family: sa_family_t = AF.INET6,
            port: in_port_t,
            flowinfo: u32,
            addr: [16]u8,
            scope_id: u32,
        };

        pub const un = extern struct {
            len: u8 = @sizeOf(un),
            family: sa_family_t = AF.UNIX,
            path: [104]u8,
        };
    },
    .haiku => extern struct {
        /// total length
        len: u8,
        /// address family
        family: sa_family_t,
        /// actually longer; address value
        data: [14]u8,

        pub const SS_MAXSIZE = 128;
        pub const storage = extern struct {
            len: u8 align(8),
            family: sa_family_t,
            padding: [126]u8 = undefined,

            comptime {
                assert(@sizeOf(storage) == SS_MAXSIZE);
                assert(@alignOf(storage) == 8);
            }
        };

        pub const in = extern struct {
            len: u8 = @sizeOf(in),
            family: sa_family_t = AF.INET,
            port: in_port_t,
            addr: u32,
            zero: [8]u8 = [8]u8{ 0, 0, 0, 0, 0, 0, 0, 0 },
        };

        pub const in6 = extern struct {
            len: u8 = @sizeOf(in6),
            family: sa_family_t = AF.INET6,
            port: in_port_t,
            flowinfo: u32,
            addr: [16]u8,
            scope_id: u32,
        };

        pub const un = extern struct {
            len: u8 = @sizeOf(un),
            family: sa_family_t = AF.UNIX,
            path: [104]u8,
        };
    },
    .openbsd => extern struct {
        /// total length
        len: u8,
        /// address family
        family: sa_family_t,
        /// actually longer; address value
        data: [14]u8,

        pub const SS_MAXSIZE = 256;
        pub const storage = extern struct {
            len: u8 align(8),
            family: sa_family_t,
            padding: [254]u8 = undefined,

            comptime {
                assert(@sizeOf(storage) == SS_MAXSIZE);
                assert(@alignOf(storage) == 8);
            }
        };

        pub const in = extern struct {
            len: u8 = @sizeOf(in),
            family: sa_family_t = AF.INET,
            port: in_port_t,
            addr: u32,
            zero: [8]u8 = [8]u8{ 0, 0, 0, 0, 0, 0, 0, 0 },
        };

        pub const in6 = extern struct {
            len: u8 = @sizeOf(in6),
            family: sa_family_t = AF.INET6,
            port: in_port_t,
            flowinfo: u32,
            addr: [16]u8,
            scope_id: u32,
        };

        /// Definitions for UNIX IPC domain.
        pub const un = extern struct {
            /// total sockaddr length
            len: u8 = @sizeOf(un),

            family: sa_family_t = AF.LOCAL,

            /// path name
            path: [104]u8,
        };
    },
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L110-L114
    .serenity => extern struct {
        family: sa_family_t,
        data: [26]u8,

        // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/netinet/in.h
        const in_addr = u32;
        const in6_addr = [16]u8;
        pub const in = extern struct {
            family: sa_family_t = AF.INET,
            port: in_port_t,
            addr: in_addr,
            zero: [8]u8 = @splat(0),
        };
        pub const in6 = extern struct {
            family: sa_family_t = AF.INET6,
            port: in_port_t,
            flowinfo: u32,
            addr: in6_addr,
            scope_id: u32,
        };

        // https://github.com/SerenityOS/serenity/blob/b92e6b02e53b2927732f31b1442cad420b62d1ef/Kernel/API/POSIX/sys/un.h
        const UNIX_PATH_MAX = 108;
        pub const un = extern struct {
            family: sa_family_t = AF.LOCAL,
            path: [UNIX_PATH_MAX]u8,
        };
    },
    else => void,
};
pub const socklen_t = switch (native_os) {
    .linux, .emscripten => linux.socklen_t,
    .windows => ws2_32.socklen_t,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L57
    else => u32,
};
pub const in_port_t = u16;
pub const sa_family_t = switch (native_os) {
    .linux, .emscripten => linux.sa_family_t,
    .windows => ws2_32.ADDRESS_FAMILY,
    .openbsd, .haiku, .dragonfly, .netbsd, .freebsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => u8,
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L66
    .illumos, .serenity => u16,
    else => void,
};
pub const AF = if (builtin.abi.isAndroid()) struct {
    pub const UNSPEC = 0;
    pub const UNIX = 1;
    pub const LOCAL = 1;
    pub const INET = 2;
    pub const AX25 = 3;
    pub const IPX = 4;
    pub const APPLETALK = 5;
    pub const NETROM = 6;
    pub const BRIDGE = 7;
    pub const ATMPVC = 8;
    pub const X25 = 9;
    pub const INET6 = 10;
    pub const ROSE = 11;
    pub const DECnet = 12;
    pub const NETBEUI = 13;
    pub const SECURITY = 14;
    pub const KEY = 15;
    pub const NETLINK = 16;
    pub const ROUTE = NETLINK;
    pub const PACKET = 17;
    pub const ASH = 18;
    pub const ECONET = 19;
    pub const ATMSVC = 20;
    pub const RDS = 21;
    pub const SNA = 22;
    pub const IRDA = 23;
    pub const PPPOX = 24;
    pub const WANPIPE = 25;
    pub const LLC = 26;
    pub const CAN = 29;
    pub const TIPC = 30;
    pub const BLUETOOTH = 31;
    pub const IUCV = 32;
    pub const RXRPC = 33;
    pub const ISDN = 34;
    pub const PHONET = 35;
    pub const IEEE802154 = 36;
    pub const CAIF = 37;
    pub const ALG = 38;
    pub const NFC = 39;
    pub const VSOCK = 40;
    pub const KCM = 41;
    pub const QIPCRTR = 42;
    pub const MAX = 43;
} else switch (native_os) {
    .linux, .emscripten => linux.AF,
    .windows => ws2_32.AF,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        pub const UNSPEC = 0;
        pub const LOCAL = 1;
        pub const UNIX = LOCAL;
        pub const INET = 2;
        pub const SYS_CONTROL = 2;
        pub const IMPLINK = 3;
        pub const PUP = 4;
        pub const CHAOS = 5;
        pub const NS = 6;
        pub const ISO = 7;
        pub const OSI = ISO;
        pub const ECMA = 8;
        pub const DATAKIT = 9;
        pub const CCITT = 10;
        pub const SNA = 11;
        pub const DECnet = 12;
        pub const DLI = 13;
        pub const LAT = 14;
        pub const HYLINK = 15;
        pub const APPLETALK = 16;
        pub const ROUTE = 17;
        pub const LINK = 18;
        pub const XTP = 19;
        pub const COIP = 20;
        pub const CNT = 21;
        pub const RTIP = 22;
        pub const IPX = 23;
        pub const SIP = 24;
        pub const PIP = 25;
        pub const ISDN = 28;
        pub const E164 = ISDN;
        pub const KEY = 29;
        pub const INET6 = 30;
        pub const NATM = 31;
        pub const SYSTEM = 32;
        pub const NETBIOS = 33;
        pub const PPP = 34;
        pub const MAX = 40;
    },
    .freebsd => struct {
        pub const UNSPEC = 0;
        pub const UNIX = 1;
        pub const LOCAL = UNIX;
        pub const FILE = LOCAL;
        pub const INET = 2;
        pub const IMPLINK = 3;
        pub const PUP = 4;
        pub const CHAOS = 5;
        pub const NETBIOS = 6;
        pub const ISO = 7;
        pub const OSI = ISO;
        pub const ECMA = 8;
        pub const DATAKIT = 9;
        pub const CCITT = 10;
        pub const SNA = 11;
        pub const DECnet = 12;
        pub const DLI = 13;
        pub const LAT = 14;
        pub const HYLINK = 15;
        pub const APPLETALK = 16;
        pub const ROUTE = 17;
        pub const LINK = 18;
        pub const pseudo_XTP = 19;
        pub const COIP = 20;
        pub const CNT = 21;
        pub const pseudo_RTIP = 22;
        pub const IPX = 23;
        pub const SIP = 24;
        pub const pseudo_PIP = 25;
        pub const ISDN = 26;
        pub const E164 = ISDN;
        pub const pseudo_KEY = 27;
        pub const INET6 = 28;
        pub const NATM = 29;
        pub const ATM = 30;
        pub const pseudo_HDRCMPLT = 31;
        pub const NETGRAPH = 32;
        pub const SLOW = 33;
        pub const SCLUSTER = 34;
        pub const ARP = 35;
        pub const BLUETOOTH = 36;
        pub const IEEE80211 = 37;
        pub const INET_SDP = 40;
        pub const INET6_SDP = 42;
        pub const MAX = 42;
    },
    .illumos => struct {
        pub const UNSPEC = 0;
        pub const UNIX = 1;
        pub const LOCAL = UNIX;
        pub const FILE = UNIX;
        pub const INET = 2;
        pub const IMPLINK = 3;
        pub const PUP = 4;
        pub const CHAOS = 5;
        pub const NS = 6;
        pub const NBS = 7;
        pub const ECMA = 8;
        pub const DATAKIT = 9;
        pub const CCITT = 10;
        pub const SNA = 11;
        pub const DECnet = 12;
        pub const DLI = 13;
        pub const LAT = 14;
        pub const HYLINK = 15;
        pub const APPLETALK = 16;
        pub const NIT = 17;
        pub const @"802" = 18;
        pub const OSI = 19;
        pub const X25 = 20;
        pub const OSINET = 21;
        pub const GOSIP = 22;
        pub const IPX = 23;
        pub const ROUTE = 24;
        pub const LINK = 25;
        pub const INET6 = 26;
        pub const KEY = 27;
        pub const NCA = 28;
        pub const POLICY = 29;
        pub const INET_OFFLOAD = 30;
        pub const TRILL = 31;
        pub const PACKET = 32;
        pub const LX_NETLINK = 33;
        pub const MAX = 33;
    },
    .netbsd => struct {
        pub const UNSPEC = 0;
        pub const LOCAL = 1;
        pub const UNIX = LOCAL;
        pub const INET = 2;
        pub const IMPLINK = 3;
        pub const PUP = 4;
        pub const CHAOS = 5;
        pub const NS = 6;
        pub const ISO = 7;
        pub const OSI = ISO;
        pub const ECMA = 8;
        pub const DATAKIT = 9;
        pub const CCITT = 10;
        pub const SNA = 11;
        pub const DECnet = 12;
        pub const DLI = 13;
        pub const LAT = 14;
        pub const HYLINK = 15;
        pub const APPLETALK = 16;
        pub const OROUTE = 17;
        pub const LINK = 18;
        pub const COIP = 20;
        pub const CNT = 21;
        pub const IPX = 23;
        pub const INET6 = 24;
        pub const ISDN = 26;
        pub const E164 = ISDN;
        pub const NATM = 27;
        pub const ARP = 28;
        pub const BLUETOOTH = 31;
        pub const IEEE80211 = 32;
        pub const MPLS = 33;
        pub const ROUTE = 34;
        pub const CAN = 35;
        pub const ETHER = 36;
        pub const MAX = 37;
    },
    .dragonfly => struct {
        pub const UNSPEC = 0;
        pub const OSI = ISO;
        pub const UNIX = LOCAL;
        pub const LOCAL = 1;
        pub const INET = 2;
        pub const IMPLINK = 3;
        pub const PUP = 4;
        pub const CHAOS = 5;
        pub const NETBIOS = 6;
        pub const ISO = 7;
        pub const ECMA = 8;
        pub const DATAKIT = 9;
        pub const CCITT = 10;
        pub const SNA = 11;
        pub const DLI = 13;
        pub const LAT = 14;
        pub const HYLINK = 15;
        pub const APPLETALK = 16;
        pub const ROUTE = 17;
        pub const LINK = 18;
        pub const COIP = 20;
        pub const CNT = 21;
        pub const IPX = 23;
        pub const SIP = 24;
        pub const ISDN = 26;
        pub const INET6 = 28;
        pub const NATM = 29;
        pub const ATM = 30;
        pub const NETGRAPH = 32;
        pub const BLUETOOTH = 33;
        pub const MPLS = 34;
        pub const MAX = 36;
    },
    .haiku => struct {
        pub const UNSPEC = 0;
        pub const INET = 1;
        pub const APPLETALK = 2;
        pub const ROUTE = 3;
        pub const LINK = 4;
        pub const INET6 = 5;
        pub const DLI = 6;
        pub const IPX = 7;
        pub const NOTIFY = 8;
        pub const LOCAL = 9;
        pub const UNIX = LOCAL;
        pub const BLUETOOTH = 10;
        pub const MAX = 11;
    },
    .openbsd => struct {
        pub const UNSPEC = 0;
        pub const UNIX = 1;
        pub const LOCAL = UNIX;
        pub const INET = 2;
        pub const APPLETALK = 16;
        pub const INET6 = 24;
        pub const KEY = 30;
        pub const ROUTE = 17;
        pub const SNA = 11;
        pub const MPLS = 33;
        pub const BLUETOOTH = 32;
        pub const ISDN = 26;
        pub const MAX = 36;
    },
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L17-L22
    .serenity => struct {
        pub const UNSPEC = 0;
        pub const LOCAL = 1;
        pub const UNIX = LOCAL;
        pub const INET = 2;
        pub const INET6 = 3;
        pub const MAX = 4;
    },
    else => void,
};
pub const PF = if (builtin.abi.isAndroid()) struct {
    pub const UNSPEC = AF.UNSPEC;
    pub const UNIX = AF.UNIX;
    pub const LOCAL = AF.LOCAL;
    pub const INET = AF.INET;
    pub const AX25 = AF.AX25;
    pub const IPX = AF.IPX;
    pub const APPLETALK = AF.APPLETALK;
    pub const NETROM = AF.NETROM;
    pub const BRIDGE = AF.BRIDGE;
    pub const ATMPVC = AF.ATMPVC;
    pub const X25 = AF.X25;
    pub const PF_INET6 = AF.INET6;
    pub const PF_ROSE = AF.ROSE;
    pub const PF_DECnet = AF.DECnet;
    pub const PF_NETBEUI = AF.NETBEUI;
    pub const PF_SECURITY = AF.SECURITY;
    pub const PF_KEY = AF.KEY;
    pub const PF_NETLINK = AF.NETLINK;
    pub const PF_ROUTE = AF.ROUTE;
    pub const PF_PACKET = AF.PACKET;
    pub const PF_ASH = AF.ASH;
    pub const PF_ECONET = AF.ECONET;
    pub const PF_ATMSVC = AF.ATMSVC;
    pub const PF_RDS = AF.RDS;
    pub const PF_SNA = AF.SNA;
    pub const PF_IRDA = AF.IRDA;
    pub const PF_PPPOX = AF.PPPOX;
    pub const PF_WANPIPE = AF.WANPIPE;
    pub const PF_LLC = AF.LLC;
    pub const PF_CAN = AF.CAN;
    pub const PF_TIPC = AF.TIPC;
    pub const PF_BLUETOOTH = AF.BLUETOOTH;
    pub const PF_IUCV = AF.IUCV;
    pub const PF_RXRPC = AF.RXRPC;
    pub const PF_ISDN = AF.ISDN;
    pub const PF_PHONET = AF.PHONET;
    pub const PF_IEEE802154 = AF.IEEE802154;
    pub const PF_CAIF = AF.CAIF;
    pub const PF_ALG = AF.ALG;
    pub const PF_NFC = AF.NFC;
    pub const PF_VSOCK = AF.VSOCK;
    pub const PF_KCM = AF.KCM;
    pub const PF_QIPCRTR = AF.QIPCRTR;
    pub const PF_MAX = AF.MAX;
} else switch (native_os) {
    .linux, .emscripten => linux.PF,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        pub const UNSPEC = AF.UNSPEC;
        pub const LOCAL = AF.LOCAL;
        pub const UNIX = PF.LOCAL;
        pub const INET = AF.INET;
        pub const IMPLINK = AF.IMPLINK;
        pub const PUP = AF.PUP;
        pub const CHAOS = AF.CHAOS;
        pub const NS = AF.NS;
        pub const ISO = AF.ISO;
        pub const OSI = AF.ISO;
        pub const ECMA = AF.ECMA;
        pub const DATAKIT = AF.DATAKIT;
        pub const CCITT = AF.CCITT;
        pub const SNA = AF.SNA;
        pub const DECnet = AF.DECnet;
        pub const DLI = AF.DLI;
        pub const LAT = AF.LAT;
        pub const HYLINK = AF.HYLINK;
        pub const APPLETALK = AF.APPLETALK;
        pub const ROUTE = AF.ROUTE;
        pub const LINK = AF.LINK;
        pub const XTP = AF.XTP;
        pub const COIP = AF.COIP;
        pub const CNT = AF.CNT;
        pub const SIP = AF.SIP;
        pub const IPX = AF.IPX;
        pub const RTIP = AF.RTIP;
        pub const PIP = AF.PIP;
        pub const ISDN = AF.ISDN;
        pub const KEY = AF.KEY;
        pub const INET6 = AF.INET6;
        pub const NATM = AF.NATM;
        pub const SYSTEM = AF.SYSTEM;
        pub const NETBIOS = AF.NETBIOS;
        pub const PPP = AF.PPP;
        pub const MAX = AF.MAX;
    },
    .freebsd => struct {
        pub const UNSPEC = AF.UNSPEC;
        pub const LOCAL = AF.LOCAL;
        pub const UNIX = PF.LOCAL;
        pub const INET = AF.INET;
        pub const IMPLINK = AF.IMPLINK;
        pub const PUP = AF.PUP;
        pub const CHAOS = AF.CHAOS;
        pub const NETBIOS = AF.NETBIOS;
        pub const ISO = AF.ISO;
        pub const OSI = AF.ISO;
        pub const ECMA = AF.ECMA;
        pub const DATAKIT = AF.DATAKIT;
        pub const CCITT = AF.CCITT;
        pub const DECnet = AF.DECnet;
        pub const DLI = AF.DLI;
        pub const LAT = AF.LAT;
        pub const HYLINK = AF.HYLINK;
        pub const APPLETALK = AF.APPLETALK;
        pub const ROUTE = AF.ROUTE;
        pub const LINK = AF.LINK;
        pub const XTP = AF.pseudo_XTP;
        pub const COIP = AF.COIP;
        pub const CNT = AF.CNT;
        pub const SIP = AF.SIP;
        pub const IPX = AF.IPX;
        pub const RTIP = AF.pseudo_RTIP;
        pub const PIP = AF.pseudo_PIP;
        pub const ISDN = AF.ISDN;
        pub const KEY = AF.pseudo_KEY;
        pub const INET6 = AF.pseudo_INET6;
        pub const NATM = AF.NATM;
        pub const ATM = AF.ATM;
        pub const NETGRAPH = AF.NETGRAPH;
        pub const SLOW = AF.SLOW;
        pub const SCLUSTER = AF.SCLUSTER;
        pub const ARP = AF.ARP;
        pub const BLUETOOTH = AF.BLUETOOTH;
        pub const IEEE80211 = AF.IEEE80211;
        pub const INET_SDP = AF.INET_SDP;
        pub const INET6_SDP = AF.INET6_SDP;
        pub const MAX = AF.MAX;
    },
    .illumos => struct {
        pub const UNSPEC = AF.UNSPEC;
        pub const UNIX = AF.UNIX;
        pub const LOCAL = UNIX;
        pub const FILE = UNIX;
        pub const INET = AF.INET;
        pub const IMPLINK = AF.IMPLINK;
        pub const PUP = AF.PUP;
        pub const CHAOS = AF.CHAOS;
        pub const NS = AF.NS;
        pub const NBS = AF.NBS;
        pub const ECMA = AF.ECMA;
        pub const DATAKIT = AF.DATAKIT;
        pub const CCITT = AF.CCITT;
        pub const SNA = AF.SNA;
        pub const DECnet = AF.DECnet;
        pub const DLI = AF.DLI;
        pub const LAT = AF.LAT;
        pub const HYLINK = AF.HYLINK;
        pub const APPLETALK = AF.APPLETALK;
        pub const NIT = AF.NIT;
        pub const @"802" = AF.@"802";
        pub const OSI = AF.OSI;
        pub const X25 = AF.X25;
        pub const OSINET = AF.OSINET;
        pub const GOSIP = AF.GOSIP;
        pub const IPX = AF.IPX;
        pub const ROUTE = AF.ROUTE;
        pub const LINK = AF.LINK;
        pub const INET6 = AF.INET6;
        pub const KEY = AF.KEY;
        pub const NCA = AF.NCA;
        pub const POLICY = AF.POLICY;
        pub const TRILL = AF.TRILL;
        pub const PACKET = AF.PACKET;
        pub const LX_NETLINK = AF.LX_NETLINK;
        pub const MAX = AF.MAX;
    },
    .netbsd => struct {
        pub const UNSPEC = AF.UNSPEC;
        pub const LOCAL = AF.LOCAL;
        pub const UNIX = PF.LOCAL;
        pub const INET = AF.INET;
        pub const IMPLINK = AF.IMPLINK;
        pub const PUP = AF.PUP;
        pub const CHAOS = AF.CHAOS;
        pub const NS = AF.NS;
        pub const ISO = AF.ISO;
        pub const OSI = AF.ISO;
        pub const ECMA = AF.ECMA;
        pub const DATAKIT = AF.DATAKIT;
        pub const CCITT = AF.CCITT;
        pub const SNA = AF.SNA;
        pub const DECnet = AF.DECnet;
        pub const DLI = AF.DLI;
        pub const LAT = AF.LAT;
        pub const HYLINK = AF.HYLINK;
        pub const APPLETALK = AF.APPLETALK;
        pub const OROUTE = AF.OROUTE;
        pub const LINK = AF.LINK;
        pub const COIP = AF.COIP;
        pub const CNT = AF.CNT;
        pub const INET6 = AF.INET6;
        pub const IPX = AF.IPX;
        pub const ISDN = AF.ISDN;
        pub const E164 = AF.E164;
        pub const NATM = AF.NATM;
        pub const ARP = AF.ARP;
        pub const BLUETOOTH = AF.BLUETOOTH;
        pub const MPLS = AF.MPLS;
        pub const ROUTE = AF.ROUTE;
        pub const CAN = AF.CAN;
        pub const ETHER = AF.ETHER;
        pub const MAX = AF.MAX;
    },
    .dragonfly => struct {
        pub const INET6 = AF.INET6;
        pub const IMPLINK = AF.IMPLINK;
        pub const ROUTE = AF.ROUTE;
        pub const ISO = AF.ISO;
        pub const PIP = AF.pseudo_PIP;
        pub const CHAOS = AF.CHAOS;
        pub const DATAKIT = AF.DATAKIT;
        pub const INET = AF.INET;
        pub const APPLETALK = AF.APPLETALK;
        pub const SIP = AF.SIP;
        pub const OSI = AF.ISO;
        pub const CNT = AF.CNT;
        pub const LINK = AF.LINK;
        pub const HYLINK = AF.HYLINK;
        pub const MAX = AF.MAX;
        pub const KEY = AF.pseudo_KEY;
        pub const PUP = AF.PUP;
        pub const COIP = AF.COIP;
        pub const SNA = AF.SNA;
        pub const LOCAL = AF.LOCAL;
        pub const NETBIOS = AF.NETBIOS;
        pub const NATM = AF.NATM;
        pub const BLUETOOTH = AF.BLUETOOTH;
        pub const UNSPEC = AF.UNSPEC;
        pub const NETGRAPH = AF.NETGRAPH;
        pub const ECMA = AF.ECMA;
        pub const IPX = AF.IPX;
        pub const DLI = AF.DLI;
        pub const ATM = AF.ATM;
        pub const CCITT = AF.CCITT;
        pub const ISDN = AF.ISDN;
        pub const RTIP = AF.pseudo_RTIP;
        pub const LAT = AF.LAT;
        pub const UNIX = PF.LOCAL;
        pub const XTP = AF.pseudo_XTP;
        pub const DECnet = AF.DECnet;
    },
    .haiku => struct {
        pub const UNSPEC = AF.UNSPEC;
        pub const INET = AF.INET;
        pub const ROUTE = AF.ROUTE;
        pub const LINK = AF.LINK;
        pub const INET6 = AF.INET6;
        pub const LOCAL = AF.LOCAL;
        pub const UNIX = AF.UNIX;
        pub const BLUETOOTH = AF.BLUETOOTH;
    },
    .openbsd => struct {
        pub const UNSPEC = AF.UNSPEC;
        pub const LOCAL = AF.LOCAL;
        pub const UNIX = AF.UNIX;
        pub const INET = AF.INET;
        pub const APPLETALK = AF.APPLETALK;
        pub const INET6 = AF.INET6;
        pub const DECnet = AF.DECnet;
        pub const KEY = AF.KEY;
        pub const ROUTE = AF.ROUTE;
        pub const SNA = AF.SNA;
        pub const MPLS = AF.MPLS;
        pub const BLUETOOTH = AF.BLUETOOTH;
        pub const ISDN = AF.ISDN;
        pub const MAX = AF.MAX;
    },
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L24-L29
    .serenity => struct {
        pub const LOCAL = AF.LOCAL;
        pub const UNIX = AF.LOCAL;
        pub const INET = AF.INET;
        pub const INET6 = AF.INET6;
        pub const UNSPEC = AF.UNSPEC;
        pub const MAX = AF.MAX;
    },
    else => void,
};
pub const DT = switch (native_os) {
    .linux => linux.DT,
    // https://github.com/SerenityOS/serenity/blob/1262a7d1424d0d2e89d80644409721cbf056ab17/Kernel/API/POSIX/dirent.h#L16-L35
    .netbsd, .freebsd, .openbsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .serenity => struct {
        pub const UNKNOWN = 0;
        pub const FIFO = 1;
        pub const CHR = 2;
        pub const DIR = 4;
        pub const BLK = 6;
        pub const REG = 8;
        pub const LNK = 10;
        pub const SOCK = 12;
        pub const WHT = 14;
    },
    .dragonfly => struct {
        pub const UNKNOWN = 0;
        pub const FIFO = 1;
        pub const CHR = 2;
        pub const DIR = 4;
        pub const BLK = 6;
        pub const REG = 8;
        pub const LNK = 10;
        pub const SOCK = 12;
        pub const WHT = 14;
        pub const DBF = 15;
    },
    else => void,
};
pub const MSG = switch (native_os) {
    .linux => linux.MSG,
    .emscripten => emscripten.MSG,
    .windows => ws2_32.MSG,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.MSG,
    .haiku => struct {
        pub const OOB = 0x0001;
        pub const PEEK = 0x0002;
        pub const DONTROUTE = 0x0004;
        pub const EOR = 0x0008;
        pub const TRUNC = 0x0010;
        pub const CTRUNC = 0x0020;
        pub const WAITALL = 0x0040;
        pub const DONTWAIT = 0x0080;
        pub const BCAST = 0x0100;
        pub const MCAST = 0x0200;
        pub const EOF = 0x0400;
        pub const NOSIGNAL = 0x0800;
    },
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L56-L64
    .serenity => struct {
        pub const TRUNC = 0x1;
        pub const CTRUNC = 0x2;
        pub const PEEK = 0x4;
        pub const OOB = 0x8;
        pub const DONTROUTE = 0x10;
        pub const WAITALL = 0x20;
        pub const DONTWAIT = 0x40;
        pub const NOSIGNAL = 0x80;
        pub const EOR = 0x100;
    },
    .freebsd => struct {
        pub const OOB = 0x00000001;
        pub const PEEK = 0x00000002;
        pub const DONTROUTE = 0x00000004;
        pub const EOR = 0x00000008;
        pub const TRUNC = 0x00000010;
        pub const CTRUNC = 0x00000020;
        pub const WAITALL = 0x00000040;
        pub const DONTWAIT = 0x00000080;
        pub const EOF = 0x00000100;
        pub const NOTIFICATION = 0x00002000;
        pub const NBIO = 0x00004000;
        pub const COMPAT = 0x00008000;
        pub const SOCALLBCK = 0x00010000;
        pub const NOSIGNAL = 0x00020000;
        pub const CMSG_CLOEXEC = 0x00040000;
        pub const WAITFORONE = 0x00080000;
        pub const MORETOCOME = 0x00100000;
        pub const TLSAPPDATA = 0x00200000;
    },
    .netbsd => struct {
        pub const OOB = 0x0001;
        pub const PEEK = 0x0002;
        pub const DONTROUTE = 0x0004;
        pub const EOR = 0x0008;
        pub const TRUNC = 0x0010;
        pub const CTRUNC = 0x0020;
        pub const WAITALL = 0x0040;
        pub const DONTWAIT = 0x0080;
        pub const BCAST = 0x0100;
        pub const MCAST = 0x0200;
        pub const NOSIGNAL = 0x0400;
        pub const CMSG_CLOEXEC = 0x0800;
        pub const NBIO = 0x1000;
        pub const WAITFORONE = 0x2000;
        pub const NOTIFICATION = 0x4000;
    },
    // https://github.com/openbsd/src/blob/42a7be81bef70c04732f45ec573622effe56b563/sys/sys/socket.h#L506
    .openbsd => struct {
        pub const OOB = 0x1;
        pub const PEEK = 0x2;
        pub const DONTROUTE = 0x4;
        pub const EOR = 0x8;
        pub const TRUNC = 0x10;
        pub const CTRUNC = 0x20;
        pub const WAITALL = 0x40;
        pub const DONTWAIT = 0x80;
        pub const BCAST = 0x100;
        pub const MCAST = 0x200;
        pub const NOSIGNAL = 0x400;
        pub const CMSG_CLOEXEC = 0x800;
        pub const WAITFORONE = 0x1000;
        pub const CMSG_CLOFORK = 0x2000;
    },
    .dragonfly => struct {
        pub const OOB = 0x0001;
        pub const PEEK = 0x0002;
        pub const DONTROUTE = 0x0004;
        pub const EOR = 0x0008;
        pub const TRUNC = 0x0010;
        pub const CTRUNC = 0x0020;
        pub const WAITALL = 0x0040;
        pub const DONTWAIT = 0x0080;
        pub const NOSIGNAL = 0x0400;
        pub const SYNC = 0x0800;
        pub const CMSG_CLOEXEC = 0x1000;
        pub const CMSG_CLOFORK = 0x2000;
        pub const FBLOCKING = 0x10000;
        pub const FNONBLOCKING = 0x20000;
    },
    .illumos => struct {
        pub const OOB = 0x0001;
        pub const PEEK = 0x0002;
        pub const DONTROUTE = 0x0004;
        pub const EOR = 0x0008;
        pub const CTRUNC = 0x0010;
        pub const TRUNC = 0x0020;
        pub const WAITALL = 0x0040;
        pub const DONTWAIT = 0x0080;
        pub const NOTIFICATION = 0x0100;
        pub const NOSIGNAL = 0x0200;
        pub const CMSG_CLOEXEC = 0x1000;
        pub const CMSG_CLOFORK = 0x2000;
    },
    else => void,
};
pub const SOCK = switch (native_os) {
    .linux => linux.SOCK,
    .emscripten => emscripten.SOCK,
    .windows => ws2_32.SOCK,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        pub const STREAM = 1;
        pub const DGRAM = 2;
        pub const RAW = 3;
        pub const RDM = 4;
        pub const SEQPACKET = 5;
        pub const MAXADDRLEN = 255;

        /// Not actually supported by Darwin, but Zig supplies a shim.
        /// This numerical value is not ABI-stable. It need only not conflict
        /// with any other `SOCK` bits.
        pub const CLOEXEC = 1 << 15;
        /// Not actually supported by Darwin, but Zig supplies a shim.
        /// This numerical value is not ABI-stable. It need only not conflict
        /// with any other `SOCK` bits.
        pub const NONBLOCK = 1 << 16;
    },
    .freebsd => struct {
        pub const STREAM = 1;
        pub const DGRAM = 2;
        pub const RAW = 3;
        pub const RDM = 4;
        pub const SEQPACKET = 5;

        pub const CLOEXEC = 0x10000000;
        pub const NONBLOCK = 0x20000000;
    },
    .illumos => struct {
        /// Datagram.
        pub const DGRAM = 1;
        /// STREAM.
        pub const STREAM = 2;
        /// Raw-protocol interface.
        pub const RAW = 4;
        /// Reliably-delivered message.
        pub const RDM = 5;
        /// Sequenced packed stream.
        pub const SEQPACKET = 6;

        pub const NONBLOCK = 0x100000;
        pub const NDELAY = 0x200000;
        pub const CLOEXEC = 0x080000;
    },
    .netbsd => struct {
        pub const STREAM = 1;
        pub const DGRAM = 2;
        pub const RAW = 3;
        pub const RDM = 4;
        pub const SEQPACKET = 5;
        pub const CONN_DGRAM = 6;
        pub const DCCP = CONN_DGRAM;

        pub const CLOEXEC = 0x10000000;
        pub const NONBLOCK = 0x20000000;
        pub const NOSIGPIPE = 0x40000000;
        pub const FLAGS_MASK = 0xf0000000;
    },
    .dragonfly => struct {
        pub const STREAM = 1;
        pub const DGRAM = 2;
        pub const RAW = 3;
        pub const RDM = 4;
        pub const SEQPACKET = 5;
        pub const MAXADDRLEN = 255;
        pub const CLOEXEC = 0x10000000;
        pub const NONBLOCK = 0x20000000;
    },
    .haiku => struct {
        pub const STREAM = 1;
        pub const DGRAM = 2;
        pub const RAW = 3;
        pub const SEQPACKET = 5;
        pub const MISC = 255;

        pub const NONBLOCK = 0x40000;
        pub const CLOEXEC = 0x80000;
    },
    .openbsd => struct {
        pub const STREAM = 1;
        pub const DGRAM = 2;
        pub const RAW = 3;
        pub const RDM = 4;
        pub const SEQPACKET = 5;

        pub const CLOEXEC = 0x8000;
        pub const NONBLOCK = 0x4000;
    },
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L31-L38
    .serenity => struct {
        pub const STREAM = 1;
        pub const DGRAM = 2;
        pub const RAW = 3;
        pub const RDM = 4;
        pub const SEQPACKET = 5;

        pub const NONBLOCK = 0o4000;
        pub const CLOEXEC = 0o2000000;
    },
    else => void,
};
pub const TCP = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.TCP,
    .linux => linux.TCP,
    .emscripten => emscripten.TCP,
    .windows => ws2_32.TCP,
    // https://github.com/SerenityOS/serenity/blob/61ac554a3403838f79ca746bd1c65ded6f97d124/Kernel/API/POSIX/netinet/tcp.h#L13-L14
    .serenity => struct {
        pub const NODELAY = 10;
        pub const MAXSEG = 11;
    },
    else => void,
};
pub const IPPROTO = switch (native_os) {
    .linux, .emscripten => linux.IPPROTO,
    .windows => ws2_32.IPPROTO,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        pub const ICMP = 1;
        pub const ICMPV6 = 58;
        pub const TCP = 6;
        pub const UDP = 17;
        pub const IP = 0;
        pub const IPV6 = 41;
        pub const RAW = 255;
    },
    .freebsd => struct {
        /// dummy for IP
        pub const IP = 0;
        /// control message protocol
        pub const ICMP = 1;
        /// tcp
        pub const TCP = 6;
        /// user datagram protocol
        pub const UDP = 17;
        /// IP6 header
        pub const IPV6 = 41;
        /// raw IP packet
        pub const RAW = 255;
        /// IP6 hop-by-hop options
        pub const HOPOPTS = 0;
        /// group mgmt protocol
        pub const IGMP = 2;
        /// gateway^2 (deprecated)
        pub const GGP = 3;
        /// IPv4 encapsulation
        pub const IPV4 = 4;
        /// for compatibility
        pub const IPIP = IPV4;
        /// Stream protocol II
        pub const ST = 7;
        /// exterior gateway protocol
        pub const EGP = 8;
        /// private interior gateway
        pub const PIGP = 9;
        /// BBN RCC Monitoring
        pub const RCCMON = 10;
        /// network voice protocol
        pub const NVPII = 11;
        /// pup
        pub const PUP = 12;
        /// Argus
        pub const ARGUS = 13;
        /// EMCON
        pub const EMCON = 14;
        /// Cross Net Debugger
        pub const XNET = 15;
        /// Chaos
        pub const CHAOS = 16;
        /// Multiplexing
        pub const MUX = 18;
        /// DCN Measurement Subsystems
        pub const MEAS = 19;
        /// Host Monitoring
        pub const HMP = 20;
        /// Packet Radio Measurement
        pub const PRM = 21;
        /// xns idp
        pub const IDP = 22;
        /// Trunk-1
        pub const TRUNK1 = 23;
        /// Trunk-2
        pub const TRUNK2 = 24;
        /// Leaf-1
        pub const LEAF1 = 25;
        /// Leaf-2
        pub const LEAF2 = 26;
        /// Reliable Data
        pub const RDP = 27;
        /// Reliable Transaction
        pub const IRTP = 28;
        /// tp-4 w/ class negotiation
        pub const TP = 29;
        /// Bulk Data Transfer
        pub const BLT = 30;
        /// Network Services
        pub const NSP = 31;
        /// Merit Internodal
        pub const INP = 32;
        /// Datagram Congestion Control Protocol
        pub const DCCP = 33;
        /// Third Party Connect
        pub const @"3PC" = 34;
        /// InterDomain Policy Routing
        pub const IDPR = 35;
        /// XTP
        pub const XTP = 36;
        /// Datagram Delivery
        pub const DDP = 37;
        /// Control Message Transport
        pub const CMTP = 38;
        /// TP++ Transport
        pub const TPXX = 39;
        /// IL transport protocol
        pub const IL = 40;
        /// Source Demand Routing
        pub const SDRP = 42;
        /// IP6 routing header
        pub const ROUTING = 43;
        /// IP6 fragmentation header
        pub const FRAGMENT = 44;
        /// InterDomain Routing
        pub const IDRP = 45;
        /// resource reservation
        pub const RSVP = 46;
        /// General Routing Encap.
        pub const GRE = 47;
        /// Mobile Host Routing
        pub const MHRP = 48;
        /// BHA
        pub const BHA = 49;
        /// IP6 Encap Sec. Payload
        pub const ESP = 50;
        /// IP6 Auth Header
        pub const AH = 51;
        /// Integ. Net Layer Security
        pub const INLSP = 52;
        /// IP with encryption
        pub const SWIPE = 53;
        /// Next Hop Resolution
        pub const NHRP = 54;
        /// IP Mobility
        pub const MOBILE = 55;
        /// Transport Layer Security
        pub const TLSP = 56;
        /// SKIP
        pub const SKIP = 57;
        /// ICMP6
        pub const ICMPV6 = 58;
        /// IP6 no next header
        pub const NONE = 59;
        /// IP6 destination option
        pub const DSTOPTS = 60;
        /// any host internal protocol
        pub const AHIP = 61;
        /// CFTP
        pub const CFTP = 62;
        /// "hello" routing protocol
        pub const HELLO = 63;
        /// SATNET/Backroom EXPAK
        pub const SATEXPAK = 64;
        /// Kryptolan
        pub const KRYPTOLAN = 65;
        /// Remote Virtual Disk
        pub const RVD = 66;
        /// Pluribus Packet Core
        pub const IPPC = 67;
        /// Any distributed FS
        pub const ADFS = 68;
        /// Satnet Monitoring
        pub const SATMON = 69;
        /// VISA Protocol
        pub const VISA = 70;
        /// Packet Core Utility
        pub const IPCV = 71;
        /// Comp. Prot. Net. Executive
        pub const CPNX = 72;
        /// Comp. Prot. HeartBeat
        pub const CPHB = 73;
        /// Wang Span Network
        pub const WSN = 74;
        /// Packet Video Protocol
        pub const PVP = 75;
        /// BackRoom SATNET Monitoring
        pub const BRSATMON = 76;
        /// Sun net disk proto (temp.)
        pub const ND = 77;
        /// WIDEBAND Monitoring
        pub const WBMON = 78;
        /// WIDEBAND EXPAK
        pub const WBEXPAK = 79;
        /// ISO cnlp
        pub const EON = 80;
        /// VMTP
        pub const VMTP = 81;
        /// Secure VMTP
        pub const SVMTP = 82;
        /// Banyon VINES
        pub const VINES = 83;
        /// TTP
        pub const TTP = 84;
        /// NSFNET-IGP
        pub const IGP = 85;
        /// dissimilar gateway prot.
        pub const DGP = 86;
        /// TCF
        pub const TCF = 87;
        /// Cisco/GXS IGRP
        pub const IGRP = 88;
        /// OSPFIGP
        pub const OSPFIGP = 89;
        /// Strite RPC protocol
        pub const SRPC = 90;
        /// Locus Address Resoloution
        pub const LARP = 91;
        /// Multicast Transport
        pub const MTP = 92;
        /// AX.25 Frames
        pub const AX25 = 93;
        /// IP encapsulated in IP
        pub const IPEIP = 94;
        /// Mobile Int.ing control
        pub const MICP = 95;
        /// Semaphore Comm. security
        pub const SCCSP = 96;
        /// Ethernet IP encapsulation
        pub const ETHERIP = 97;
        /// encapsulation header
        pub const ENCAP = 98;
        /// any private encr. scheme
        pub const APES = 99;
        /// GMTP
        pub const GMTP = 100;
        /// payload compression (IPComp)
        pub const IPCOMP = 108;
        /// SCTP
        pub const SCTP = 132;
        /// IPv6 Mobility Header
        pub const MH = 135;
        /// UDP-Lite
        pub const UDPLITE = 136;
        /// IP6 Host Identity Protocol
        pub const HIP = 139;
        /// IP6 Shim6 Protocol
        pub const SHIM6 = 140;
        /// Protocol Independent Mcast
        pub const PIM = 103;
        /// CARP
        pub const CARP = 112;
        /// PGM
        pub const PGM = 113;
        /// MPLS-in-IP
        pub const MPLS = 137;
        /// PFSYNC
        pub const PFSYNC = 240;
        /// Reserved
        pub const RESERVED_253 = 253;
        /// Reserved
        pub const RESERVED_254 = 254;
    },
    .illumos => struct {
        /// dummy for IP
        pub const IP = 0;
        /// Hop by hop header for IPv6
        pub const HOPOPTS = 0;
        /// control message protocol
        pub const ICMP = 1;
        /// group control protocol
        pub const IGMP = 2;
        /// gateway^2 (deprecated)
        pub const GGP = 3;
        /// IP in IP encapsulation
        pub const ENCAP = 4;
        /// tcp
        pub const TCP = 6;
        /// exterior gateway protocol
        pub const EGP = 8;
        /// pup
        pub const PUP = 12;
        /// user datagram protocol
        pub const UDP = 17;
        /// xns idp
        pub const IDP = 22;
        /// IPv6 encapsulated in IP
        pub const IPV6 = 41;
        /// Routing header for IPv6
        pub const ROUTING = 43;
        /// Fragment header for IPv6
        pub const FRAGMENT = 44;
        /// rsvp
        pub const RSVP = 46;
        /// IPsec Encap. Sec. Payload
        pub const ESP = 50;
        /// IPsec Authentication Hdr.
        pub const AH = 51;
        /// ICMP for IPv6
        pub const ICMPV6 = 58;
        /// No next header for IPv6
        pub const NONE = 59;
        /// Destination options
        pub const DSTOPTS = 60;
        /// "hello" routing protocol
        pub const HELLO = 63;
        /// UNOFFICIAL net disk proto
        pub const ND = 77;
        /// ISO clnp
        pub const EON = 80;
        /// OSPF
        pub const OSPF = 89;
        /// PIM routing protocol
        pub const PIM = 103;
        /// Stream Control
        pub const SCTP = 132;
        /// raw IP packet
        pub const RAW = 255;
        /// Sockets Direct Protocol
        pub const PROTO_SDP = 257;
    },
    .netbsd => struct {
        /// dummy for IP
        pub const IP = 0;
        /// IP6 hop-by-hop options
        pub const HOPOPTS = 0;
        /// control message protocol
        pub const ICMP = 1;
        /// group mgmt protocol
        pub const IGMP = 2;
        /// gateway^2 (deprecated)
        pub const GGP = 3;
        /// IP header
        pub const IPV4 = 4;
        /// IP inside IP
        pub const IPIP = 4;
        /// tcp
        pub const TCP = 6;
        /// exterior gateway protocol
        pub const EGP = 8;
        /// pup
        pub const PUP = 12;
        /// user datagram protocol
        pub const UDP = 17;
        /// xns idp
        pub const IDP = 22;
        /// tp-4 w/ class negotiation
        pub const TP = 29;
        /// DCCP
        pub const DCCP = 33;
        /// IP6 header
        pub const IPV6 = 41;
        /// IP6 routing header
        pub const ROUTING = 43;
        /// IP6 fragmentation header
        pub const FRAGMENT = 44;
        /// resource reservation
        pub const RSVP = 46;
        /// GRE encaps RFC 1701
        pub const GRE = 47;
        /// encap. security payload
        pub const ESP = 50;
        /// authentication header
        pub const AH = 51;
        /// IP Mobility RFC 2004
        pub const MOBILE = 55;
        /// IPv6 ICMP
        pub const IPV6_ICMP = 58;
        /// ICMP6
        pub const ICMPV6 = 58;
        /// IP6 no next header
        pub const NONE = 59;
        /// IP6 destination option
        pub const DSTOPTS = 60;
        /// ISO cnlp
        pub const EON = 80;
        /// Ethernet-in-IP
        pub const ETHERIP = 97;
        /// encapsulation header
        pub const ENCAP = 98;
        /// Protocol indep. multicast
        pub const PIM = 103;
        /// IP Payload Comp. Protocol
        pub const IPCOMP = 108;
        /// VRRP RFC 2338
        pub const VRRP = 112;
        /// Common Address Resolution Protocol
        pub const CARP = 112;
        /// L2TPv3
        pub const L2TP = 115;
        /// SCTP
        pub const SCTP = 132;
        /// PFSYNC
        pub const PFSYNC = 240;
        /// raw IP packet
        pub const RAW = 255;
    },
    .dragonfly => struct {
        pub const IP = 0;
        pub const ICMP = 1;
        pub const TCP = 6;
        pub const UDP = 17;
        pub const IPV6 = 41;
        pub const RAW = 255;
        pub const HOPOPTS = 0;
        pub const IGMP = 2;
        pub const GGP = 3;
        pub const IPV4 = 4;
        pub const IPIP = IPV4;
        pub const ST = 7;
        pub const EGP = 8;
        pub const PIGP = 9;
        pub const RCCMON = 10;
        pub const NVPII = 11;
        pub const PUP = 12;
        pub const ARGUS = 13;
        pub const EMCON = 14;
        pub const XNET = 15;
        pub const CHAOS = 16;
        pub const MUX = 18;
        pub const MEAS = 19;
        pub const HMP = 20;
        pub const PRM = 21;
        pub const IDP = 22;
        pub const TRUNK1 = 23;
        pub const TRUNK2 = 24;
        pub const LEAF1 = 25;
        pub const LEAF2 = 26;
        pub const RDP = 27;
        pub const IRTP = 28;
        pub const TP = 29;
        pub const BLT = 30;
        pub const NSP = 31;
        pub const INP = 32;
        pub const SEP = 33;
        pub const @"3PC" = 34;
        pub const IDPR = 35;
        pub const XTP = 36;
        pub const DDP = 37;
        pub const CMTP = 38;
        pub const TPXX = 39;
        pub const IL = 40;
        pub const SDRP = 42;
        pub const ROUTING = 43;
        pub const FRAGMENT = 44;
        pub const IDRP = 45;
        pub const RSVP = 46;
        pub const GRE = 47;
        pub const MHRP = 48;
        pub const BHA = 49;
        pub const ESP = 50;
        pub const AH = 51;
        pub const INLSP = 52;
        pub const SWIPE = 53;
        pub const NHRP = 54;
        pub const MOBILE = 55;
        pub const TLSP = 56;
        pub const SKIP = 57;
        pub const ICMPV6 = 58;
        pub const NONE = 59;
        pub const DSTOPTS = 60;
        pub const AHIP = 61;
        pub const CFTP = 62;
        pub const HELLO = 63;
        pub const SATEXPAK = 64;
        pub const KRYPTOLAN = 65;
        pub const RVD = 66;
        pub const IPPC = 67;
        pub const ADFS = 68;
        pub const SATMON = 69;
        pub const VISA = 70;
        pub const IPCV = 71;
        pub const CPNX = 72;
        pub const CPHB = 73;
        pub const WSN = 74;
        pub const PVP = 75;
        pub const BRSATMON = 76;
        pub const ND = 77;
        pub const WBMON = 78;
        pub const WBEXPAK = 79;
        pub const EON = 80;
        pub const VMTP = 81;
        pub const SVMTP = 82;
        pub const VINES = 83;
        pub const TTP = 84;
        pub const IGP = 85;
        pub const DGP = 86;
        pub const TCF = 87;
        pub const IGRP = 88;
        pub const OSPFIGP = 89;
        pub const SRPC = 90;
        pub const LARP = 91;
        pub const MTP = 92;
        pub const AX25 = 93;
        pub const IPEIP = 94;
        pub const MICP = 95;
        pub const SCCSP = 96;
        pub const ETHERIP = 97;
        pub const ENCAP = 98;
        pub const APES = 99;
        pub const GMTP = 100;
        pub const IPCOMP = 108;
        pub const PIM = 103;
        pub const CARP = 112;
        pub const PGM = 113;
        pub const PFSYNC = 240;
        pub const DIVERT = 254;
        pub const MAX = 256;
        pub const DONE = 257;
        pub const UNKNOWN = 258;
    },
    .haiku => struct {
        pub const IP = 0;
        pub const HOPOPTS = 0;
        pub const ICMP = 1;
        pub const IGMP = 2;
        pub const TCP = 6;
        pub const UDP = 17;
        pub const IPV6 = 41;
        pub const ROUTING = 43;
        pub const FRAGMENT = 44;
        pub const ESP = 50;
        pub const AH = 51;
        pub const ICMPV6 = 58;
        pub const NONE = 59;
        pub const DSTOPTS = 60;
        pub const ETHERIP = 97;
        pub const RAW = 255;
        pub const MAX = 256;
    },
    .openbsd => struct {
        /// dummy for IP
        pub const IP = 0;
        /// IP6 hop-by-hop options
        pub const HOPOPTS = IPPROTO.IP;
        /// control message protocol
        pub const ICMP = 1;
        /// group mgmt protocol
        pub const IGMP = 2;
        /// gateway^2 (deprecated)
        pub const GGP = 3;
        /// IP header
        pub const IPV4 = IPIP;
        /// IP inside IP
        pub const IPIP = 4;
        /// tcp
        pub const TCP = 6;
        /// exterior gateway protocol
        pub const EGP = 8;
        /// pup
        pub const PUP = 12;
        /// user datagram protocol
        pub const UDP = 17;
        /// xns idp
        pub const IDP = 22;
        /// tp-4 w/ class negotiation
        pub const TP = 29;
        /// IP6 header
        pub const IPV6 = 41;
        /// IP6 routing header
        pub const ROUTING = 43;
        /// IP6 fragmentation header
        pub const FRAGMENT = 44;
        /// resource reservation
        pub const RSVP = 46;
        /// GRE encaps RFC 1701
        pub const GRE = 47;
        /// encap. security payload
        pub const ESP = 50;
        /// authentication header
        pub const AH = 51;
        /// IP Mobility RFC 2004
        pub const MOBILE = 55;
        /// IPv6 ICMP
        pub const IPV6_ICMP = 58;
        /// ICMP6
        pub const ICMPV6 = 58;
        /// IP6 no next header
        pub const NONE = 59;
        /// IP6 destination option
        pub const DSTOPTS = 60;
        /// ISO cnlp
        pub const EON = 80;
        /// Ethernet-in-IP
        pub const ETHERIP = 97;
        /// encapsulation header
        pub const ENCAP = 98;
        /// Protocol indep. multicast
        pub const PIM = 103;
        /// IP Payload Comp. Protocol
        pub const IPCOMP = 108;
        /// VRRP RFC 2338
        pub const VRRP = 112;
        /// Common Address Resolution Protocol
        pub const CARP = 112;
        /// PFSYNC
        pub const PFSYNC = 240;
        /// raw IP packet
        pub const RAW = 255;
    },
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L44-L54
    .serenity => struct {
        pub const IP = 0;
        pub const ICMP = 1;
        pub const IGMP = 2;
        pub const IPIP = 4;
        pub const TCP = 6;
        pub const UDP = 17;
        pub const IPV6 = 41;
        pub const ESP = 50;
        pub const AH = 51;
        pub const ICMPV6 = 58;
        pub const RAW = 255;
    },
    else => void,
};
pub const IP = switch (native_os) {
    .linux => linux.IP,
    .freebsd => freebsd.IP,
    .dragonfly => dragonfly.IP,
    .netbsd => netbsd.IP,
    .openbsd => openbsd.IP,
    .illumos => illumos.IP,
    .haiku => haiku.IP,
    .serenity => serenity.IP,
    else => void,
};
pub const IPV6 = switch (native_os) {
    .linux => linux.IPV6,
    .freebsd => freebsd.IPV6,
    .dragonfly => dragonfly.IPV6,
    .netbsd => netbsd.IPV6,
    .openbsd => openbsd.IPV6,
    .illumos => illumos.IPV6,
    .haiku => haiku.IPV6,
    .serenity => serenity.IPV6,
    else => void,
};
pub const IPTOS = switch (native_os) {
    .linux => linux.IPTOS,
    .freebsd => freebsd.IPTOS,
    .dragonfly => dragonfly.IPTOS,
    .netbsd => netbsd.IPTOS,
    .openbsd => openbsd.IPTOS,
    .illumos => illumos.IPTOS,
    .haiku => haiku.IPTOS,
    .serenity => serenity.IPTOS,
    else => void,
};
pub const SOL = switch (native_os) {
    .linux => linux.SOL,
    .emscripten => emscripten.SOL,
    .windows => ws2_32.SOL,
    .openbsd, .haiku, .dragonfly, .netbsd, .freebsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        pub const SOCKET = 0xffff;
    },
    .illumos => struct {
        pub const SOCKET = 0xffff;
        pub const ROUTE = 0xfffe;
        pub const PACKET = 0xfffd;
        pub const FILTER = 0xfffc;
    },
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L127
    .serenity => struct {
        pub const SOCKET = 1;
    },
    else => void,
};
pub const SO = switch (native_os) {
    .linux => linux.SO,
    .emscripten => emscripten.SO,
    .windows => ws2_32.SO,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        pub const DEBUG = 0x0001;
        pub const ACCEPTCONN = 0x0002;
        pub const REUSEADDR = 0x0004;
        pub const KEEPALIVE = 0x0008;
        pub const DONTROUTE = 0x0010;
        pub const BROADCAST = 0x0020;
        pub const USELOOPBACK = 0x0040;
        pub const LINGER = 0x0080;
        pub const OOBINLINE = 0x0100;
        pub const REUSEPORT = 0x0200;
        pub const ACCEPTFILTER = 0x1000;
        pub const SNDBUF = 0x1001;
        pub const RCVBUF = 0x1002;
        pub const SNDLOWAT = 0x1003;
        pub const RCVLOWAT = 0x1004;
        pub const SNDTIMEO = 0x1005;
        pub const RCVTIMEO = 0x1006;
        pub const ERROR = 0x1007;
        pub const TYPE = 0x1008;

        pub const NREAD = 0x1020;
        pub const NKE = 0x1021;
        pub const NOSIGPIPE = 0x1022;
        pub const NOADDRERR = 0x1023;
        pub const NWRITE = 0x1024;
        pub const REUSESHAREUID = 0x1025;
        pub const LINGER_SEC = 0x1080;
    },
    .freebsd => struct {
        pub const DEBUG = 0x00000001;
        pub const ACCEPTCONN = 0x00000002;
        pub const REUSEADDR = 0x00000004;
        pub const KEEPALIVE = 0x00000008;
        pub const DONTROUTE = 0x00000010;
        pub const BROADCAST = 0x00000020;
        pub const USELOOPBACK = 0x00000040;
        pub const LINGER = 0x00000080;
        pub const OOBINLINE = 0x00000100;
        pub const REUSEPORT = 0x00000200;
        pub const TIMESTAMP = 0x00000400;
        pub const NOSIGPIPE = 0x00000800;
        pub const ACCEPTFILTER = 0x00001000;
        pub const BINTIME = 0x00002000;
        pub const NO_OFFLOAD = 0x00004000;
        pub const NO_DDP = 0x00008000;
        pub const REUSEPORT_LB = 0x00010000;

        pub const SNDBUF = 0x1001;
        pub const RCVBUF = 0x1002;
        pub const SNDLOWAT = 0x1003;
        pub const RCVLOWAT = 0x1004;
        pub const SNDTIMEO = 0x1005;
        pub const RCVTIMEO = 0x1006;
        pub const ERROR = 0x1007;
        pub const TYPE = 0x1008;
        pub const LABEL = 0x1009;
        pub const PEERLABEL = 0x1010;
        pub const LISTENQLIMIT = 0x1011;
        pub const LISTENQLEN = 0x1012;
        pub const LISTENINCQLEN = 0x1013;
        pub const SETFIB = 0x1014;
        pub const USER_COOKIE = 0x1015;
        pub const PROTOCOL = 0x1016;
        pub const PROTOTYPE = PROTOCOL;
        pub const TS_CLOCK = 0x1017;
        pub const MAX_PACING_RATE = 0x1018;
        pub const DOMAIN = 0x1019;
    },
    .illumos => struct {
        pub const DEBUG = 0x0001;
        pub const ACCEPTCONN = 0x0002;
        pub const REUSEADDR = 0x0004;
        pub const KEEPALIVE = 0x0008;
        pub const DONTROUTE = 0x0010;
        pub const BROADCAST = 0x0020;
        pub const USELOOPBACK = 0x0040;
        pub const LINGER = 0x0080;
        pub const OOBINLINE = 0x0100;
        pub const DGRAM_ERRIND = 0x0200;
        pub const RECVUCRED = 0x0400;

        pub const SNDBUF = 0x1001;
        pub const RCVBUF = 0x1002;
        pub const SNDLOWAT = 0x1003;
        pub const RCVLOWAT = 0x1004;
        pub const SNDTIMEO = 0x1005;
        pub const RCVTIMEO = 0x1006;
        pub const ERROR = 0x1007;
        pub const TYPE = 0x1008;
        pub const PROTOTYPE = 0x1009;
        pub const ANON_MLP = 0x100a;
        pub const MAC_EXEMPT = 0x100b;
        pub const DOMAIN = 0x100c;
        pub const RCVPSH = 0x100d;

        pub const SECATTR = 0x1011;
        pub const TIMESTAMP = 0x1013;
        pub const ALLZONES = 0x1014;
        pub const EXCLBIND = 0x1015;
        pub const MAC_IMPLICIT = 0x1016;
        pub const VRRP = 0x1017;
    },
    .netbsd => struct {
        pub const DEBUG = 0x0001;
        pub const ACCEPTCONN = 0x0002;
        pub const REUSEADDR = 0x0004;
        pub const KEEPALIVE = 0x0008;
        pub const DONTROUTE = 0x0010;
        pub const BROADCAST = 0x0020;
        pub const USELOOPBACK = 0x0040;
        pub const LINGER = 0x0080;
        pub const OOBINLINE = 0x0100;
        pub const REUSEPORT = 0x0200;
        pub const NOSIGPIPE = 0x0800;
        pub const ACCEPTFILTER = 0x1000;
        pub const TIMESTAMP = 0x2000;
        pub const RERROR = 0x4000;

        pub const SNDBUF = 0x1001;
        pub const RCVBUF = 0x1002;
        pub const SNDLOWAT = 0x1003;
        pub const RCVLOWAT = 0x1004;
        pub const ERROR = 0x1007;
        pub const TYPE = 0x1008;
        pub const OVERFLOWED = 0x1009;

        pub const NOHEADER = 0x100a;
        pub const SNDTIMEO = 0x100b;
        pub const RCVTIMEO = 0x100c;
    },
    .dragonfly => struct {
        pub const DEBUG = 0x0001;
        pub const ACCEPTCONN = 0x0002;
        pub const REUSEADDR = 0x0004;
        pub const KEEPALIVE = 0x0008;
        pub const DONTROUTE = 0x0010;
        pub const BROADCAST = 0x0020;
        pub const USELOOPBACK = 0x0040;
        pub const LINGER = 0x0080;
        pub const OOBINLINE = 0x0100;
        pub const REUSEPORT = 0x0200;
        pub const TIMESTAMP = 0x0400;
        pub const NOSIGPIPE = 0x0800;
        pub const ACCEPTFILTER = 0x1000;
        pub const RERROR = 0x2000;
        pub const PASSCRED = 0x4000;

        pub const SNDBUF = 0x1001;
        pub const RCVBUF = 0x1002;
        pub const SNDLOWAT = 0x1003;
        pub const RCVLOWAT = 0x1004;
        pub const SNDTIMEO = 0x1005;
        pub const RCVTIMEO = 0x1006;
        pub const ERROR = 0x1007;
        pub const TYPE = 0x1008;
        pub const SNDSPACE = 0x100a;
        pub const CPUHINT = 0x1030;
    },
    .haiku => struct {
        pub const ACCEPTCONN = 0x00000001;
        pub const BROADCAST = 0x00000002;
        pub const DEBUG = 0x00000004;
        pub const DONTROUTE = 0x00000008;
        pub const KEEPALIVE = 0x00000010;
        pub const OOBINLINE = 0x00000020;
        pub const REUSEADDR = 0x00000040;
        pub const REUSEPORT = 0x00000080;
        pub const USELOOPBACK = 0x00000100;
        pub const LINGER = 0x00000200;

        pub const SNDBUF = 0x40000001;
        pub const SNDLOWAT = 0x40000002;
        pub const SNDTIMEO = 0x40000003;
        pub const RCVBUF = 0x40000004;
        pub const RCVLOWAT = 0x40000005;
        pub const RCVTIMEO = 0x40000006;
        pub const ERROR = 0x40000007;
        pub const TYPE = 0x40000008;
        pub const NONBLOCK = 0x40000009;
        pub const BINDTODEVICE = 0x4000000a;
        pub const PEERCRED = 0x4000000b;
    },
    .openbsd => struct {
        pub const DEBUG = 0x0001;
        pub const ACCEPTCONN = 0x0002;
        pub const REUSEADDR = 0x0004;
        pub const KEEPALIVE = 0x0008;
        pub const DONTROUTE = 0x0010;
        pub const BROADCAST = 0x0020;
        pub const USELOOPBACK = 0x0040;
        pub const LINGER = 0x0080;
        pub const OOBINLINE = 0x0100;
        pub const REUSEPORT = 0x0200;
        pub const TIMESTAMP = 0x0800;
        pub const BINDANY = 0x1000;
        pub const ZEROIZE = 0x2000;
        pub const SNDBUF = 0x1001;
        pub const RCVBUF = 0x1002;
        pub const SNDLOWAT = 0x1003;
        pub const RCVLOWAT = 0x1004;
        pub const SNDTIMEO = 0x1005;
        pub const RCVTIMEO = 0x1006;
        pub const ERROR = 0x1007;
        pub const TYPE = 0x1008;
        pub const NETPROC = 0x1020;
        pub const RTABLE = 0x1021;
        pub const PEERCRED = 0x1022;
        pub const SPLICE = 0x1023;
        pub const DOMAIN = 0x1024;
        pub const PROTOCOL = 0x1025;
    },
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L130-L150
    .serenity => struct {
        pub const RCVTIMEO = 0;
        pub const SNDTIMEO = 1;
        pub const TYPE = 2;
        pub const ERROR = 3;
        pub const PEERCRED = 4;
        pub const RCVBUF = 5;
        pub const SNDBUF = 6;
        pub const DEBUG = 7;
        pub const REUSEADDR = 8;
        pub const BINDTODEVICE = 9;
        pub const KEEPALIVE = 10;
        pub const TIMESTAMP = 11;
        pub const BROADCAST = 12;
        pub const LINGER = 13;
        pub const ACCEPTCONN = 14;
        pub const DONTROUTE = 15;
        pub const OOBINLINE = 16;
        pub const SNDLOWAT = 17;
        pub const RCVLOWAT = 18;
    },
    else => void,
};
pub const SOMAXCONN = switch (native_os) {
    .linux => linux.SOMAXCONN,
    .windows => ws2_32.SOMAXCONN,
    // https://github.com/SerenityOS/serenity/blob/ac44ec5ebc707f9dd0c3d4759a1e17e91db5d74f/Kernel/API/POSIX/sys/socket.h#L128
    .illumos, .serenity => 128,
    // https://github.com/freebsd/freebsd-src/blob/9ab31f821ad1c6bad474510447387c50bef2c24c/sys/sys/socket.h#L434
    // https://github.com/DragonFlyBSD/DragonFlyBSD/blob/fd3d1949d526ffa646e57037770acd6f2f3bb617/sys/sys/socket.h#L393
    // https://github.com/NetBSD/src/blob/a673fb3f8487e974c669216064f7588207229fea/sys/sys/socket.h#L472
    // https://github.com/openbsd/src/blob/8ba9cd88f10123fef7af805b8e5ccc2463ad8fa4/sys/sys/socket.h#L483
    // https://github.com/apple/darwin-xnu/blob/2ff845c2e033bd0ff64b5b6aa6063a1f8f65aa32/bsd/sys/socket.h#L815
    .freebsd, .dragonfly, .netbsd, .openbsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => 128,
    else => void,
};
pub const SCM = switch (native_os) {
    .linux, .emscripten => linux.SCM,
    // https://github.com/illumos/illumos-gate/blob/489f6310fe8952e87fc1dce8af87990fcfd90f18/usr/src/uts/common/sys/socket.h#L196
    .illumos => struct {
        pub const RIGHTS = 0x1010;
        pub const UCRED = 0x1012;
        pub const TIMESTAMP = SO.TIMESTAMP;
    },
    // https://github.com/haiku/haiku/blob/e3d01e53a25446d5ba4999d0ff6dff29a2418657/headers/posix/sys/socket.h#L156
    .haiku => struct {
        pub const RIGHTS = 1;
    },
    // https://github.com/SerenityOS/serenity/blob/c6618f36bf0949bd76177f202659b1f3079e0792/Kernel/API/POSIX/sys/socket.h#L171
    .serenity => struct {
        pub const TIMESTAMP = 0;
        pub const RIGHTS = 1;
    },
    // https://github.com/freebsd/freebsd-src/blob/614e9b33bf5594d9d09b5d296afa4f3aa6971823/sys/sys/socket.h#L593
    .freebsd => struct {
        pub const RIGHTS = 1;
        pub const TIMESTAMP = 2;
        pub const CREDS = 3;
        pub const BINTIME = 4;
        pub const REALTIME = 5;
        pub const MONOTONIC = 6;
        pub const TIME_INFO = 7;
        pub const CREDS2 = 8;
    },
    // https://github.com/DragonFlyBSD/DragonFlyBSD/blob/6098912863ed4c7b3f70d7483910ce2956cf4ed3/sys/sys/socket.h#L520
    .dragonfly => struct {
        pub const RIGHTS = 1;
        pub const TIMESTAMP = 2;
        pub const CREDS = 3;
    },
    // https://github.com/NetBSD/src/blob/3311177ea898ab8322292ba0e48faa9b2e834cb6/sys/sys/socket.h#L578
    .netbsd => struct {
        pub const RIGHTS = 0x01;
        pub const TIMESTAMP = 0x08;
        pub const CREDS = 0x10;
    },
    // https://github.com/openbsd/src/blob/1b1dd04c9634112eb763374379af99a68ace4328/sys/sys/socket.h#L566
    .openbsd => struct {
        pub const RIGHTS = 0x01;
        pub const TIMESTAMP = 0x04;
    },
    // https://github.com/apple/darwin-xnu/blob/2ff845c2e033bd0ff64b5b6aa6063a1f8f65aa32/bsd/sys/socket.h#L1114
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        pub const RIGHTS = 1;
        pub const TIMESTAMP = 2;
        pub const CREDS = 3;
        pub const TIMESTAMP_MONOTONIC = 4;
    },
    else => void,
};

pub const IFNAMESIZE = switch (native_os) {
    .linux => linux.IFNAMESIZE,
    .emscripten => emscripten.IFNAMESIZE,
    .windows => 30,
    // https://github.com/SerenityOS/serenity/blob/9882848e0bf783dfc8e8a6d887a848d70d9c58f4/Kernel/API/POSIX/net/if.h#L50
    .openbsd, .dragonfly, .netbsd, .freebsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .serenity => 16,
    .illumos => 32,
    else => {},
};

pub const stack_t = switch (native_os) {
    .linux => linux.stack_t,
    .emscripten => emscripten.stack_t,
    .freebsd, .openbsd => extern struct {
        /// Signal stack base.
        sp: *anyopaque,
        /// Signal stack length.
        size: usize,
        /// SS_DISABLE and/or SS_ONSTACK.
        flags: i32,
    },
    // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/signal.h#L48-L52
    .serenity => extern struct {
        sp: *anyopaque,
        flags: c_int,
        size: usize,
    },
    else => extern struct {
        sp: [*]u8,
        size: isize,
        flags: i32,
    },
};
pub const time_t = switch (native_os) {
    .linux => linux.time_t,
    .dragonfly, .illumos, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => c_long,
    .emscripten => emscripten.time_t,
    .freebsd, .haiku => if (native_arch == .x86) c_int else c_longlong,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L47
    // lib/libc/include/wasm-wasi-musl/__typedef_time_t.h
    .netbsd, .openbsd, .serenity, .wasi => c_longlong,
    else => void,
};
pub const suseconds_t = switch (native_os) {
    .dragonfly, .freebsd, .openbsd, .illumos => c_long,
    .netbsd, .haiku, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => c_int,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L49
    .serenity, .wasi => c_longlong,
    else => void,
};

pub const timeval = switch (native_os) {
    .linux => linux.timeval,
    .emscripten => emscripten.timeval,
    .windows => extern struct {
        sec: c_long,
        usec: c_long,
    },
    // https://github.com/SerenityOS/serenity/blob/6b6eca0631c893c5f8cfb8274cdfe18e2d0637c0/Kernel/API/POSIX/sys/time.h#L15-L18
    .dragonfly, .freebsd, .netbsd, .openbsd, .haiku, .illumos, .serenity, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .wasi => extern struct {
        /// seconds
        sec: time_t,
        /// microseconds
        usec: suseconds_t,
    },
    else => void,
};
pub const timezone = switch (native_os) {
    .linux => linux.timezone,
    .emscripten => emscripten.timezone,
    // https://github.com/SerenityOS/serenity/blob/ba776390b5878ec0be1a9e595a3471a6cfe0a0cf/Userland/Libraries/LibC/sys/time.h#L19-L22
    .dragonfly, .freebsd, .netbsd, .openbsd, .haiku, .illumos, .serenity, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .windows, .wasi => extern struct {
        minuteswest: c_int,
        dsttime: c_int,
    },
    else => void,
};

pub const user_desc = switch (native_os) {
    .linux => linux.user_desc,
    else => void,
};
pub const utsname = switch (native_os) {
    .linux => linux.utsname,
    .emscripten => emscripten.utsname,
    .wasi => extern struct {
        sysname: [64:0]u8,
        nodename: [64:0]u8,
        release: [64:0]u8,
        version: [64:0]u8,
        machine: [64:0]u8,
        domainname: [64:0]u8,
    },
    .illumos => extern struct {
        sysname: [256:0]u8,
        nodename: [256:0]u8,
        release: [256:0]u8,
        version: [256:0]u8,
        machine: [256:0]u8,
        domainname: [256:0]u8,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        sysname: [255:0]u8,
        nodename: [255:0]u8,
        release: [255:0]u8,
        version: [255:0]u8,
        machine: [255:0]u8,
    },
    // https://github.com/SerenityOS/serenity/blob/d794ed1de7a46482272683f8dc4c858806390f29/Kernel/API/POSIX/sys/utsname.h#L17-L23
    .serenity => extern struct {
        sysname: [UTSNAME_ENTRY_LEN:0]u8,
        nodename: [UTSNAME_ENTRY_LEN:0]u8,
        release: [UTSNAME_ENTRY_LEN:0]u8,
        version: [UTSNAME_ENTRY_LEN:0]u8,
        machine: [UTSNAME_ENTRY_LEN:0]u8,

        const UTSNAME_ENTRY_LEN = 64;
    },
    else => void,
};
pub const PR = switch (native_os) {
    .linux => linux.PR,
    else => void,
};
pub const _errno = switch (native_os) {
    .linux => switch (native_abi) {
        .android, .androideabi => private.__errno,
        else => private.__errno_location,
    },
    .emscripten => private.__errno_location,
    .wasi, .dragonfly => private.errnoFromThreadLocal,
    .windows => private._errno,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .freebsd => private.__error,
    .illumos => private.___errno,
    .openbsd, .netbsd => private.__errno,
    .haiku => haiku._errnop,
    // https://github.com/SerenityOS/serenity/blob/a353ceecf13b6f156a078e32f1ddf1d21366934c/Userland/Libraries/LibC/errno.h#L33
    .serenity => private.__errno_location,
    else => {},
};

pub const RTLD = switch (native_os) {
    .linux, .emscripten => packed struct(u32) {
        LAZY: bool = false,
        NOW: bool = false,
        NOLOAD: bool = false,
        DEEPBIND: bool = false,
        _4: u4 = 0,
        GLOBAL: bool = false,
        _9: u3 = 0,
        NODELETE: bool = false,
        _: u19 = 0,
    },
    .dragonfly, .freebsd => packed struct(u32) {
        LAZY: bool = false,
        NOW: bool = false,
        _2: u6 = 0,
        GLOBAL: bool = false,
        TRACE: bool = false,
        _10: u2 = 0,
        NODELETE: bool = false,
        NOLOAD: bool = false,
        _: u18 = 0,
    },
    .haiku => packed struct(u32) {
        NOW: bool = false,
        GLOBAL: bool = false,
        _: u30 = 0,
    },
    .netbsd => packed struct(u32) {
        LAZY: bool = false,
        NOW: bool = false,
        _2: u6 = 0,
        GLOBAL: bool = false,
        LOCAL: bool = false,
        _10: u2 = 0,
        NODELETE: bool = false,
        NOLOAD: bool = false,
        _: u18 = 0,
    },
    .illumos => packed struct(u32) {
        LAZY: bool = false,
        NOW: bool = false,
        NOLOAD: bool = false,
        _3: u5 = 0,
        GLOBAL: bool = false,
        PARENT: bool = false,
        GROUP: bool = false,
        WORLD: bool = false,
        NODELETE: bool = false,
        FIRST: bool = false,
        _14: u2 = 0,
        CONFGEN: bool = false,
        _: u15 = 0,
    },
    .openbsd => packed struct(u32) {
        LAZY: bool = false,
        NOW: bool = false,
        _2: u6 = 0,
        GLOBAL: bool = false,
        TRACE: bool = false,
        _: u22 = 0,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => packed struct(u32) {
        LAZY: bool = false,
        NOW: bool = false,
        LOCAL: bool = false,
        GLOBAL: bool = false,
        NOLOAD: bool = false,
        _5: u2 = 0,
        NODELETE: bool = false,
        FIRST: bool = false,
        _: u23 = 0,
    },
    // https://github.com/SerenityOS/serenity/blob/36a26d7fa80bc9c72b19442912d8967f448368ff/Userland/Libraries/LibC/dlfcn.h#L13-L17
    .serenity => packed struct(c_int) {
        DEFAULT: bool = false,
        _1: u1,
        LAZY: bool = false,
        NOW: bool = false,
        GLOBAL: bool = false,
        LOCAL: bool = false,
        _: std.meta.Int(.unsigned, @bitSizeOf(c_int) - 6) = 0,
    },
    else => void,
};

pub const dirent = switch (native_os) {
    .linux, .emscripten => extern struct {
        ino: ino_t,
        off: off_t,
        reclen: c_ushort,
        type: u8,
        name: [256]u8,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        ino: u64,
        seekoff: u64,
        reclen: u16,
        namlen: u16,
        type: u8,
        name: [1024]u8,
    },
    .freebsd => extern struct {
        /// File number of entry.
        fileno: ino_t,
        /// Directory offset of entry.
        off: off_t,
        /// Length of this record.
        reclen: u16,
        /// File type, one of DT_.
        type: u8,
        pad0: u8 = 0,
        /// Length of the name member.
        namlen: u16,
        pad1: u16 = 0,
        /// Name of entry.
        name: [255:0]u8,
    },
    .illumos => extern struct {
        /// Inode number of entry.
        ino: ino_t,
        /// Offset of this entry on disk.
        off: off_t,
        /// Length of this record.
        reclen: u16,
        /// File name.
        name: [MAXNAMLEN:0]u8,
    },
    .netbsd => extern struct {
        fileno: ino_t,
        reclen: u16,
        namlen: u16,
        type: u8,
        name: [MAXNAMLEN:0]u8,
    },
    .dragonfly => extern struct {
        fileno: c_ulong,
        namlen: u16,
        type: u8,
        unused1: u8,
        unused2: u32,
        name: [256]u8,

        pub fn reclen(self: dirent) u16 {
            return (@offsetOf(dirent, "name") + self.namlen + 1 + 7) & ~@as(u16, 7);
        }
    },
    .openbsd => extern struct {
        fileno: ino_t,
        off: off_t,
        reclen: u16,
        type: u8,
        namlen: u8,
        _: u32 align(1) = 0,
        name: [MAXNAMLEN:0]u8,
    },
    // https://github.com/SerenityOS/serenity/blob/abc150085f532f123b598949218893cb272ccc4c/Userland/Libraries/LibC/dirent.h#L14-L20
    .serenity => extern struct {
        ino: ino_t,
        off: off_t,
        reclen: c_ushort,
        type: u8,
        name: [255:0]u8,
    },
    else => void,
};
pub const MAXNAMLEN = switch (native_os) {
    .netbsd, .illumos => 511,
    // https://github.com/SerenityOS/serenity/blob/1262a7d1424d0d2e89d80644409721cbf056ab17/Kernel/API/POSIX/dirent.h#L37
    .haiku, .serenity => NAME_MAX,
    .openbsd => 255,
    else => {},
};
pub const dirent64 = switch (native_os) {
    .linux => extern struct {
        ino: c_ulong,
        off: c_ulong,
        reclen: c_ushort,
        type: u8,
        name: [256]u8,
    },
    else => void,
};

pub const AI = if (builtin.abi.isAndroid()) packed struct(u32) {
    PASSIVE: bool = false,
    CANONNAME: bool = false,
    NUMERICHOST: bool = false,
    NUMERICSERV: bool = false,
    _4: u4 = 0,
    ALL: bool = false,
    V4MAPPED_CFG: bool = false,
    ADDRCONFIG: bool = false,
    V4MAPPED: bool = false,
    _: u20 = 0,
} else switch (native_os) {
    .linux, .emscripten => linux.AI,
    .dragonfly, .haiku, .freebsd => packed struct(u32) {
        PASSIVE: bool = false,
        CANONNAME: bool = false,
        NUMERICHOST: bool = false,
        NUMERICSERV: bool = false,
        _4: u4 = 0,
        ALL: bool = false,
        V4MAPPED_CFG: bool = false,
        ADDRCONFIG: bool = false,
        V4MAPPED: bool = false,
        _: u20 = 0,
    },
    .netbsd => packed struct(u32) {
        PASSIVE: bool = false,
        CANONNAME: bool = false,
        NUMERICHOST: bool = false,
        NUMERICSERV: bool = false,
        _4: u6 = 0,
        ADDRCONFIG: bool = false,
        SRV: bool = false,
        _: u20 = 0,
    },
    .illumos => packed struct(u32) {
        V4MAPPED: bool = false,
        ALL: bool = false,
        ADDRCONFIG: bool = false,
        PASSIVE: bool = false,
        CANONNAME: bool = false,
        NUMERICHOST: bool = false,
        NUMERICSERV: bool = false,
        _: u25 = 0,
    },
    .openbsd => packed struct(u32) {
        PASSIVE: bool = false,
        CANONNAME: bool = false,
        NUMERICHOST: bool = false,
        EXT: bool = false,
        NUMERICSERV: bool = false,
        FQDN: bool = false,
        ADDRCONFIG: bool = false,
        _: u25 = 0,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => packed struct(u32) {
        PASSIVE: bool = false,
        CANONNAME: bool = false,
        NUMERICHOST: bool = false,
        _3: u5 = 0,
        ALL: bool = false,
        V4MAPPED_CFG: bool = false,
        ADDRCONFIG: bool = false,
        V4MAPPED: bool = false,
        NUMERICSERV: bool = false,
        _: u19 = 0,
    },
    .windows => ws2_32.AI,
    // https://github.com/SerenityOS/serenity/blob/d510d2aeb2facbd8f6c383d70fd1b033e1fee5dd/Userland/Libraries/LibC/netdb.h#L90-L96
    .serenity => packed struct(c_int) {
        PASSIVE: bool = false,
        CANONNAME: bool = false,
        NUMERICHOST: bool = false,
        NUMERICSERV: bool = false,
        V4MAPPED: bool = false,
        ALL: bool = false,
        ADDRCONFIG: bool = false,
        _: std.meta.Int(.unsigned, @bitSizeOf(c_int) - 7) = 0,
    },
    else => void,
};

pub const NI = switch (native_os) {
    .linux, .emscripten => packed struct(u32) {
        NUMERICHOST: bool = false,
        NUMERICSERV: bool = false,
        NOFQDN: bool = false,
        NAMEREQD: bool = false,
        DGRAM: bool = false,
        _5: u3 = 0,
        NUMERICSCOPE: bool = false,
        _: u23 = 0,
    },
    .illumos => packed struct(u32) {
        NOFQDN: bool = false,
        NUMERICHOST: bool = false,
        NAMEREQD: bool = false,
        NUMERICSERV: bool = false,
        DGRAM: bool = false,
        WITHSCOPEID: bool = false,
        NUMERICSCOPE: bool = false,
        _: u25 = 0,
    },
    // https://github.com/SerenityOS/serenity/blob/d510d2aeb2facbd8f6c383d70fd1b033e1fee5dd/Userland/Libraries/LibC/netdb.h#L101-L105
    .serenity => packed struct(c_int) {
        NUMERICHOST: bool = false,
        NUMERICSERV: bool = false,
        NAMEREQD: bool = false,
        NOFQDN: bool = false,
        DGRAM: bool = false,
        _: std.meta.Int(.unsigned, @bitSizeOf(c_int) - 5) = 0,
    },
    .freebsd, .haiku => packed struct(u32) {
        NOFQDN: bool = false,
        NUMERICHOST: bool = false,
        NAMEREQD: bool = false,
        NUMERICSERV: bool = false,
        DGRAM: bool = false,
        NUMERICSCOPE: bool = false,
        _: u26 = 0,
    },
    .dragonfly, .netbsd => packed struct(u32) {
        NOFQDN: bool = false,
        NUMERICHOST: bool = false,
        NAMEREQD: bool = false,
        NUMERICSERV: bool = false,
        DGRAM: bool = false,
        _5: u1 = 0,
        NUMERICSCOPE: bool = false,
        _: u25 = 0,
    },
    .openbsd => packed struct(u32) {
        NUMERICHOST: bool = false,
        NUMERICSERV: bool = false,
        NOFQDN: bool = false,
        NAMEREQD: bool = false,
        DGRAM: bool = false,
        _: u27 = 0,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => packed struct(u32) {
        NOFQDN: bool = false,
        NUMERICHOST: bool = false,
        NAMEREQD: bool = false,
        NUMERICSERV: bool = false,
        DGRAM: bool = false,
        _5: u3 = 0,
        NUMERICSCOPE: bool = false,
        _: u23 = 0,
    },
    else => void,
};

pub const EAI = if (builtin.abi.isAndroid()) enum(c_int) {
    /// address family for hostname not supported
    ADDRFAMILY = 1,
    /// temporary failure in name resolution
    AGAIN = 2,
    /// invalid value for ai_flags
    BADFLAGS = 3,
    /// non-recoverable failure in name resolution
    FAIL = 4,
    /// ai_family not supported
    FAMILY = 5,
    /// memory allocation failure
    MEMORY = 6,
    /// no address associated with hostname
    NODATA = 7,
    /// hostname nor servname provided, or not known
    NONAME = 8,
    /// servname not supported for ai_socktype
    SERVICE = 9,
    /// ai_socktype not supported
    SOCKTYPE = 10,
    /// system error returned in errno
    SYSTEM = 11,
    /// invalid value for hints
    BADHINTS = 12,
    /// resolved protocol is unknown
    PROTOCOL = 13,
    /// argument buffer overflow
    OVERFLOW = 14,

    MAX = 15,

    _,
} else switch (native_os) {
    .linux, .emscripten => enum(c_int) {
        BADFLAGS = -1,
        NONAME = -2,
        AGAIN = -3,
        FAIL = -4,
        FAMILY = -6,
        SOCKTYPE = -7,
        SERVICE = -8,
        MEMORY = -10,
        SYSTEM = -11,
        OVERFLOW = -12,

        NODATA = -5,
        ADDRFAMILY = -9,
        INPROGRESS = -100,
        CANCELED = -101,
        NOTCANCELED = -102,
        ALLDONE = -103,
        INTR = -104,
        IDN_ENCODE = -105,

        _,
    },
    .haiku, .dragonfly, .netbsd, .freebsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => enum(c_int) {
        /// address family for hostname not supported
        ADDRFAMILY = 1,
        /// temporary failure in name resolution
        AGAIN = 2,
        /// invalid value for ai_flags
        BADFLAGS = 3,
        /// non-recoverable failure in name resolution
        FAIL = 4,
        /// ai_family not supported
        FAMILY = 5,
        /// memory allocation failure
        MEMORY = 6,
        /// no address associated with hostname
        NODATA = 7,
        /// hostname nor servname provided, or not known
        NONAME = 8,
        /// servname not supported for ai_socktype
        SERVICE = 9,
        /// ai_socktype not supported
        SOCKTYPE = 10,
        /// system error returned in errno
        SYSTEM = 11,
        /// invalid value for hints
        BADHINTS = 12,
        /// resolved protocol is unknown
        PROTOCOL = 13,
        /// argument buffer overflow
        OVERFLOW = 14,
        _,
    },
    .illumos => enum(c_int) {
        /// address family for hostname not supported
        ADDRFAMILY = 1,
        /// name could not be resolved at this time
        AGAIN = 2,
        /// flags parameter had an invalid value
        BADFLAGS = 3,
        /// non-recoverable failure in name resolution
        FAIL = 4,
        /// address family not recognized
        FAMILY = 5,
        /// memory allocation failure
        MEMORY = 6,
        /// no address associated with hostname
        NODATA = 7,
        /// name does not resolve
        NONAME = 8,
        /// service not recognized for socket type
        SERVICE = 9,
        /// intended socket type was not recognized
        SOCKTYPE = 10,
        /// system error returned in errno
        SYSTEM = 11,
        /// argument buffer overflow
        OVERFLOW = 12,
        /// resolved protocol is unknown
        PROTOCOL = 13,

        _,
    },
    .openbsd => enum(c_int) {
        /// address family for hostname not supported
        ADDRFAMILY = -9,
        /// name could not be resolved at this time
        AGAIN = -3,
        /// flags parameter had an invalid value
        BADFLAGS = -1,
        /// non-recoverable failure in name resolution
        FAIL = -4,
        /// address family not recognized
        FAMILY = -6,
        /// memory allocation failure
        MEMORY = -10,
        /// no address associated with hostname
        NODATA = -5,
        /// name does not resolve
        NONAME = -2,
        /// service not recognized for socket type
        SERVICE = -8,
        /// intended socket type was not recognized
        SOCKTYPE = -7,
        /// system error returned in errno
        SYSTEM = -11,
        /// invalid value for hints
        BADHINTS = -12,
        /// resolved protocol is unknown
        PROTOCOL = -13,
        /// argument buffer overflow
        OVERFLOW = -14,
        _,
    },
    // https://github.com/SerenityOS/serenity/blob/d510d2aeb2facbd8f6c383d70fd1b033e1fee5dd/Userland/Libraries/LibC/netdb.h#L77-L88
    .serenity => enum(c_int) {
        ADDRFAMILY = 1,
        AGAIN = 2,
        BADFLAGS = 3,
        FAIL = 4,
        FAMILY = 5,
        MEMORY = 6,
        NODATA = 7,
        NONAME = 8,
        SERVICE = 9,
        SOCKTYPE = 10,
        SYSTEM = 11,
        OVERFLOW = 12,
        _,
    },
    else => void,
};

pub const dl_iterate_phdr_callback = *const fn (info: *dl_phdr_info, size: usize, data: ?*anyopaque) callconv(.c) c_int;

pub const Stat = switch (native_os) {
    .emscripten => emscripten.Stat,
    .wasi => extern struct {
        // Match wasi-libc's `struct stat` in lib/libc/include/wasm-wasi-musl/__struct_stat.h
        dev: dev_t,
        ino: ino_t,
        nlink: nlink_t,
        mode: mode_t,
        uid: uid_t,
        gid: gid_t,
        __pad0: c_uint = 0,
        rdev: dev_t,
        size: off_t,
        blksize: blksize_t,
        blocks: blkcnt_t,
        atim: timespec,
        mtim: timespec,
        ctim: timespec,
        __reserved: [3]c_longlong = [3]c_longlong{ 0, 0, 0 },

        pub fn atime(self: @This()) timespec {
            return self.atim;
        }

        pub fn mtime(self: @This()) timespec {
            return self.mtim;
        }

        pub fn ctime(self: @This()) timespec {
            return self.ctim;
        }

        pub fn fromFilestat(st: wasi.filestat_t) Stat {
            return .{
                .dev = st.dev,
                .ino = st.ino,
                .mode = switch (st.filetype) {
                    .UNKNOWN => 0,
                    .BLOCK_DEVICE => S.IFBLK,
                    .CHARACTER_DEVICE => S.IFCHR,
                    .DIRECTORY => S.IFDIR,
                    .REGULAR_FILE => S.IFREG,
                    .SOCKET_DGRAM => S.IFSOCK,
                    .SOCKET_STREAM => S.IFIFO,
                    .SYMBOLIC_LINK => S.IFLNK,
                    _ => 0,
                },
                .nlink = st.nlink,
                .size = @intCast(st.size),
                .atim = timespec.fromTimestamp(st.atim),
                .mtim = timespec.fromTimestamp(st.mtim),
                .ctim = timespec.fromTimestamp(st.ctim),

                .uid = 0,
                .gid = 0,
                .rdev = 0,
                .blksize = 0,
                .blocks = 0,
            };
        }
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        dev: i32,
        mode: u16,
        nlink: u16,
        ino: ino_t,
        uid: uid_t,
        gid: gid_t,
        rdev: i32,
        atimespec: timespec,
        mtimespec: timespec,
        ctimespec: timespec,
        birthtimespec: timespec,
        size: off_t,
        blocks: i64,
        blksize: i32,
        flags: u32,
        gen: u32,
        lspare: i32,
        qspare: [2]i64,

        pub fn atime(self: @This()) timespec {
            return self.atimespec;
        }

        pub fn mtime(self: @This()) timespec {
            return self.mtimespec;
        }

        pub fn ctime(self: @This()) timespec {
            return self.ctimespec;
        }

        pub fn birthtime(self: @This()) timespec {
            return self.birthtimespec;
        }
    },
    .freebsd => freebsd.Stat,
    .illumos => extern struct {
        dev: dev_t,
        ino: ino_t,
        mode: mode_t,
        nlink: nlink_t,
        uid: uid_t,
        gid: gid_t,
        rdev: dev_t,
        size: off_t,
        atim: timespec,
        mtim: timespec,
        ctim: timespec,
        blksize: blksize_t,
        blocks: blkcnt_t,
        fstype: [16]u8,

        pub fn atime(self: @This()) timespec {
            return self.atim;
        }

        pub fn mtime(self: @This()) timespec {
            return self.mtim;
        }

        pub fn ctime(self: @This()) timespec {
            return self.ctim;
        }
    },
    .netbsd => extern struct {
        dev: dev_t,
        mode: mode_t,
        ino: ino_t,
        nlink: nlink_t,
        uid: uid_t,
        gid: gid_t,
        rdev: dev_t,
        atim: timespec,
        mtim: timespec,
        ctim: timespec,
        birthtim: timespec,
        size: off_t,
        blocks: blkcnt_t,
        blksize: blksize_t,
        flags: u32,
        gen: u32,
        __spare: [2]u32,

        pub fn atime(self: @This()) timespec {
            return self.atim;
        }

        pub fn mtime(self: @This()) timespec {
            return self.mtim;
        }

        pub fn ctime(self: @This()) timespec {
            return self.ctim;
        }

        pub fn birthtime(self: @This()) timespec {
            return self.birthtim;
        }
    },
    .dragonfly => extern struct {
        ino: ino_t,
        nlink: c_uint,
        dev: c_uint,
        mode: c_ushort,
        padding1: u16,
        uid: uid_t,
        gid: gid_t,
        rdev: c_uint,
        atim: timespec,
        mtim: timespec,
        ctim: timespec,
        size: c_ulong,
        blocks: i64,
        blksize: u32,
        flags: u32,
        gen: u32,
        lspare: i32,
        qspare1: i64,
        qspare2: i64,
        pub fn atime(self: @This()) timespec {
            return self.atim;
        }

        pub fn mtime(self: @This()) timespec {
            return self.mtim;
        }

        pub fn ctime(self: @This()) timespec {
            return self.ctim;
        }
    },
    .haiku => extern struct {
        dev: dev_t,
        ino: ino_t,
        mode: mode_t,
        nlink: nlink_t,
        uid: uid_t,
        gid: gid_t,
        size: off_t,
        rdev: dev_t,
        blksize: blksize_t,
        atim: timespec,
        mtim: timespec,
        ctim: timespec,
        crtim: timespec,
        type: u32,
        blocks: blkcnt_t,

        pub fn atime(self: @This()) timespec {
            return self.atim;
        }
        pub fn mtime(self: @This()) timespec {
            return self.mtim;
        }
        pub fn ctime(self: @This()) timespec {
            return self.ctim;
        }
        pub fn birthtime(self: @This()) timespec {
            return self.crtim;
        }
    },
    .openbsd => extern struct {
        mode: mode_t,
        dev: dev_t,
        ino: ino_t,
        nlink: nlink_t,
        uid: uid_t,
        gid: gid_t,
        rdev: dev_t,
        atim: timespec,
        mtim: timespec,
        ctim: timespec,
        size: off_t,
        blocks: blkcnt_t,
        blksize: blksize_t,
        flags: u32,
        gen: u32,
        birthtim: timespec,

        pub fn atime(self: @This()) timespec {
            return self.atim;
        }

        pub fn mtime(self: @This()) timespec {
            return self.mtim;
        }

        pub fn ctime(self: @This()) timespec {
            return self.ctim;
        }

        pub fn birthtime(self: @This()) timespec {
            return self.birthtim;
        }
    },
    // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/sys/stat.h#L53-L67
    .serenity => extern struct {
        dev: dev_t,
        ino: ino_t,
        mode: mode_t,
        nlink: nlink_t,
        uid: uid_t,
        gid: gid_t,
        rdev: dev_t,
        size: off_t,
        blksize: blksize_t,
        blocks: blkcnt_t,
        atim: timespec,
        mtim: timespec,
        ctim: timespec,

        pub fn atime(self: @This()) timespec {
            return self.atim;
        }

        pub fn mtime(self: @This()) timespec {
            return self.mtim;
        }

        pub fn ctime(self: @This()) timespec {
            return self.ctim;
        }
    },
    else => void,
};

pub const pthread_mutex_t = switch (native_os) {
    .linux => extern struct {
        data: [data_len]u8 align(@alignOf(usize)) = [_]u8{0} ** data_len,

        const data_len = switch (native_abi) {
            .musl, .musleabi, .musleabihf => if (@sizeOf(usize) == 8) 40 else 24,
            .gnu, .gnuabin32, .gnuabi64, .gnueabi, .gnueabihf, .gnux32 => switch (native_arch) {
                .aarch64 => 48,
                .x86_64 => if (native_abi == .gnux32) 32 else 40,
                .mips64, .powerpc64, .powerpc64le, .sparc64 => 40,
                else => if (@sizeOf(usize) == 8) 40 else 24,
            },
            .android, .androideabi => if (@sizeOf(usize) == 8) 40 else 4,
            else => @compileError("unsupported ABI"),
        };
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        sig: c_long = 0x32AAABA7,
        data: [data_len]u8 = [_]u8{0} ** data_len,

        const data_len = if (@sizeOf(usize) == 8) 56 else 40;
    },
    .freebsd, .dragonfly, .openbsd => extern struct {
        inner: ?*anyopaque = null,
    },
    .hermit => extern struct {
        ptr: usize = maxInt(usize),
    },
    .netbsd => exter
```
