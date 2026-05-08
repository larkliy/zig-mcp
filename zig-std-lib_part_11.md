```
:0]const u8) ?*passwd;
pub extern "c" fn setpassent(stayopen: c_int) c_int;
pub extern "c" fn uid_from_user(name: [*:0]const u8, uid: *uid_t) c_int;
pub extern "c" fn user_from_uid(uid: uid_t, noname: c_int) ?[*:0]const u8;
pub extern "c" fn bcrypt_gensalt(log_rounds: u8) [*:0]const u8;
pub extern "c" fn bcrypt(pass: [*:0]const u8, salt: [*:0]const u8) ?[*:0]const u8;
pub extern "c" fn bcrypt_newhash(pass: [*:0]const u8, log_rounds: c_int, hash: [*]u8, hashlen: usize) c_int;
pub extern "c" fn bcrypt_checkpass(pass: [*:0]const u8, goodhash: [*:0]const u8) c_int;
pub extern "c" fn pw_dup(pw: *const passwd) ?*passwd;

pub const auth_item_t = enum(c_int) {
    ALL = 0,
    CHALLENGE = 1,
    CLASS = 2,
    NAME = 3,
    SERVICE = 4,
    STYLE = 5,
    INTERACTIVE = 6,
};

pub const BI = struct {
    pub const AUTH = "authorize"; // Accepted authentication
    pub const REJECT = "reject"; // Rejected authentication
    pub const CHALLENGE = "reject challenge"; // Reject with a challenge
    pub const SILENT = "reject silent"; // Reject silently
    pub const REMOVE = "remove"; // remove file on error
    pub const ROOTOKAY = "authorize root"; // root authenticated
    pub const SECURE = "authorize secure"; // okay on non-secure line
    pub const SETENV = "setenv"; // set environment variable
    pub const UNSETENV = "unsetenv"; // unset environment variable
    pub const VALUE = "value"; // set local variable
    pub const EXPIRED = "reject expired"; // account expired
    pub const PWEXPIRED = "reject pwexpired"; // password expired
    pub const FDPASS = "fd"; // child is passing an fd
};

pub const AUTH = struct {
    pub const OKAY: c_int = 0x01; // user authenticated
    pub const ROOTOKAY: c_int = 0x02; // authenticated as root
    pub const SECURE: c_int = 0x04; // secure login
    pub const SILENT: c_int = 0x08; // silent rejection
    pub const CHALLENGE: c_int = 0x10; // a challenge was given
    pub const EXPIRED: c_int = 0x20; // account expired
    pub const PWEXPIRED: c_int = 0x40; // password expired
    pub const ALLOW: c_int = (OKAY | ROOTOKAY | SECURE);
};

pub const TCFLUSH = enum(u32) {
    none = 0,
    I = 1,
    O = 2,
    IO = 3,
};

pub const TCIO = enum(u32) {
    OOFF = 1,
    OON = 2,
    IOFF = 3,
    ION = 4,
};

pub const E = enum(u16) {
    /// No error occurred.
    SUCCESS = 0,
    PERM = 1, // Operation not permitted
    NOENT = 2, // No such file or directory
    SRCH = 3, // No such process
    INTR = 4, // Interrupted system call
    IO = 5, // Input/output error
    NXIO = 6, // Device not configured
    @"2BIG" = 7, // Argument list too long
    NOEXEC = 8, // Exec format error
    BADF = 9, // Bad file descriptor
    CHILD = 10, // No child processes
    DEADLK = 11, // Resource deadlock avoided
    // 11 was AGAIN
    NOMEM = 12, // Cannot allocate memory
    ACCES = 13, // Permission denied
    FAULT = 14, // Bad address
    NOTBLK = 15, // Block device required
    BUSY = 16, // Device busy
    EXIST = 17, // File exists
    XDEV = 18, // Cross-device link
    NODEV = 19, // Operation not supported by device
    NOTDIR = 20, // Not a directory
    ISDIR = 21, // Is a directory
    INVAL = 22, // Invalid argument
    NFILE = 23, // Too many open files in system
    MFILE = 24, // Too many open files
    NOTTY = 25, // Inappropriate ioctl for device
    TXTBSY = 26, // Text file busy
    FBIG = 27, // File too large
    NOSPC = 28, // No space left on device
    SPIPE = 29, // Illegal seek
    ROFS = 30, // Read-only file system
    MLINK = 31, // Too many links
    PIPE = 32, // Broken pipe

    // math software
    DOM = 33, // Numerical argument out of domain
    RANGE = 34, // Result too large or too small

    // non-blocking and interrupt i/o
    // also: WOULDBLOCK: operation would block
    AGAIN = 35, // Resource temporarily unavailable
    INPROGRESS = 36, // Operation now in progress
    ALREADY = 37, // Operation already in progress

    // ipc/network software -- argument errors
    NOTSOCK = 38, // Socket operation on non-socket
    DESTADDRREQ = 39, // Destination address required
    MSGSIZE = 40, // Message too long
    PROTOTYPE = 41, // Protocol wrong type for socket
    NOPROTOOPT = 42, // Protocol option not available
    PROTONOSUPPORT = 43, // Protocol not supported
    SOCKTNOSUPPORT = 44, // Socket type not supported
    OPNOTSUPP = 45, // Operation not supported
    PFNOSUPPORT = 46, // Protocol family not supported
    AFNOSUPPORT = 47, // Address family not supported by protocol family
    ADDRINUSE = 48, // Address already in use
    ADDRNOTAVAIL = 49, // Can't assign requested address

    // ipc/network software -- operational errors
    NETDOWN = 50, // Network is down
    NETUNREACH = 51, // Network is unreachable
    NETRESET = 52, // Network dropped connection on reset
    CONNABORTED = 53, // Software caused connection abort
    CONNRESET = 54, // Connection reset by peer
    NOBUFS = 55, // No buffer space available
    ISCONN = 56, // Socket is already connected
    NOTCONN = 57, // Socket is not connected
    SHUTDOWN = 58, // Can't send after socket shutdown
    TOOMANYREFS = 59, // Too many references: can't splice
    TIMEDOUT = 60, // Operation timed out
    CONNREFUSED = 61, // Connection refused

    LOOP = 62, // Too many levels of symbolic links
    NAMETOOLONG = 63, // File name too long

    // should be rearranged
    HOSTDOWN = 64, // Host is down
    HOSTUNREACH = 65, // No route to host
    NOTEMPTY = 66, // Directory not empty

    // quotas & mush
    PROCLIM = 67, // Too many processes
    USERS = 68, // Too many users
    DQUOT = 69, // Disc quota exceeded

    // Network File System
    STALE = 70, // Stale NFS file handle
    REMOTE = 71, // Too many levels of remote in path
    BADRPC = 72, // RPC struct is bad
    RPCMISMATCH = 73, // RPC version wrong
    PROGUNAVAIL = 74, // RPC prog. not avail
    PROGMISMATCH = 75, // Program version wrong
    PROCUNAVAIL = 76, // Bad procedure for program

    NOLCK = 77, // No locks available
    NOSYS = 78, // Function not implemented

    FTYPE = 79, // Inappropriate file type or format
    AUTH = 80, // Authentication error
    NEEDAUTH = 81, // Need authenticator
    IPSEC = 82, // IPsec processing failure
    NOATTR = 83, // Attribute not found

    // Wide/multibyte-character handling, ISO/IEC 9899/AMD1:1995
    ILSEQ = 84, // Illegal byte sequence

    NOMEDIUM = 85, // No medium found
    MEDIUMTYPE = 86, // Wrong medium type
    OVERFLOW = 87, // Value too large to be stored in data type
    CANCELED = 88, // Operation canceled
    IDRM = 89, // Identifier removed
    NOMSG = 90, // No message of desired type
    NOTSUP = 91, // Not supported
    BADMSG = 92, // Bad or Corrupt message
    NOTRECOVERABLE = 93, // State not recoverable
    OWNERDEAD = 94, // Previous owner died
    PROTO = 95, // Protocol error

    _,
};

pub const MAX_PAGE_SHIFT = switch (builtin.cpu.arch) {
    .x86 => 12,
    .sparc64 => 13,
};

pub const HW = struct {
    pub const MACHINE = 1;
    pub const MODEL = 2;
    pub const NCPU = 3;
    pub const BYTEORDER = 4;
    pub const PHYSMEM = 5;
    pub const USERMEM = 6;
    pub const PAGESIZE = 7;
    pub const DISKNAMES = 8;
    pub const DISKSTATS = 9;
    pub const DISKCOUNT = 10;
    pub const SENSORS = 11;
    pub const CPUSPEED = 12;
    pub const SETPERF = 13;
    pub const VENDOR = 14;
    pub const PRODUCT = 15;
    pub const VERSION = 16;
    pub const SERIALNO = 17;
    pub const UUID = 18;
    pub const PHYSMEM64 = 19;
    pub const USERMEM64 = 20;
    pub const NCPUFOUND = 21;
    pub const ALLOWPOWERDOWN = 22;
    pub const PERFPOLICY = 23;
    pub const SMT = 24;
    pub const NCPUONLINE = 25;
    pub const POWER = 26;
};

pub const PTHREAD_STACK_MIN = switch (builtin.cpu.arch) {
    .sparc64 => 1 << 13,
    .mips64 => 1 << 14,
    else => 1 << 12,
};

// https://github.com/openbsd/src/blob/718a31b40d39fc6064de6355eb144e74633133fc/sys/netinet/in.h#L283
pub const IP = struct {
    pub const OPTIONS = 1;
    pub const HDRINCL = 2;
    pub const TOS = 3;
    pub const TTL = 4;
    pub const RECVOPTS = 5;
    pub const RECVRETOPTS = 6;
    pub const RECVDSTADDR = 7;
    pub const RETOPTS = 8;
    pub const MULTICAST_IF = 9;
    pub const MULTICAST_TTL = 10;
    pub const MULTICAST_LOOP = 11;
    pub const ADD_MEMBERSHIP = 12;
    pub const DROP_MEMBERSHIP = 13;
    pub const PORTRANGE = 19;
    pub const AUTH_LEVEL = 20;
    pub const ESP_TRANS_LEVEL = 21;
    pub const ESP_NETWORK_LEVEL = 22;
    pub const IPSEC_LOCAL_ID = 23;
    pub const IPSEC_REMOTE_ID = 24;
    pub const IPSEC_LOCAL_CRED = 25;
    pub const IPSEC_REMOTE_CRED = 26;
    pub const IPSEC_LOCAL_AUTH = 27;
    pub const IPSEC_REMOTE_AUTH = 28;
    pub const IPCOMP_LEVEL = 29;
    pub const RECVIF = 30;
    pub const RECVTTL = 31;
    pub const MINTTL = 32;
    pub const RECVDSTPORT = 33;
    pub const PIPEX = 34;
    pub const RECVRTABLE = 35;
    pub const IPSECFLOWINFO = 36;
    pub const IPDEFTTL = 37;
    pub const SENDSRCADDR = RECVDSTADDR;
    pub const RTABLE = 0x1021;
    pub const DEFAULT_MULTICAST_TTL = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const MIN_MEMBERSHIPS = 15;
    pub const MAX_MEMBERSHIPS = 4095;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/openbsd/src/blob/718a31b40d39fc6064de6355eb144e74633133fc/sys/netinet6/in6.h#L284
pub const IPV6 = struct {
    pub const UNICAST_HOPS = 4;
    pub const MULTICAST_IF = 9;
    pub const MULTICAST_HOPS = 10;
    pub const MULTICAST_LOOP = 11;
    pub const JOIN_GROUP = 12;
    pub const LEAVE_GROUP = 13;
    pub const PORTRANGE = 14;
    pub const CHECKSUM = 26;
    pub const V6ONLY = 27;
    pub const RTHDRDSTOPTS = 35;
    pub const RECVPKTINFO = 36;
    pub const RECVHOPLIMIT = 37;
    pub const RECVRTHDR = 38;
    pub const RECVHOPOPTS = 39;
    pub const RECVDSTOPTS = 40;
    pub const USE_MIN_MTU = 42;
    pub const RECVPATHMTU = 43;
    pub const PATHMTU = 44;
    pub const PKTINFO = 46;
    pub const HOPLIMIT = 47;
    pub const NEXTHOP = 48;
    pub const HOPOPTS = 49;
    pub const DSTOPTS = 50;
    pub const RTHDR = 51;
    pub const AUTH_LEVEL = 53;
    pub const ESP_TRANS_LEVEL = 54;
    pub const ESP_NETWORK_LEVEL = 55;
    pub const RECVTCLASS = 57;
    pub const AUTOFLOWLABEL = 59;
    pub const IPCOMP_LEVEL = 60;
    pub const TCLASS = 61;
    pub const DONTFRAG = 62;
    pub const PIPEX = 63;
    pub const RECVDSTPORT = 64;
    pub const MINHOPCOUNT = 65;
    pub const RTABLE = 0x1021;
    pub const RTHDR_LOOSE = 0;
    pub const RTHDR_TYPE_0 = 0;
    pub const DEFAULT_MULTICAST_HOPS = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/openbsd/src/blob/718a31b40d39fc6064de6355eb144e74633133fc/sys/netinet/ip.h#L73
pub const IPTOS = struct {
    pub const LOWDELAY = 0x10;
    pub const THROUGHPUT = 0x08;
    pub const RELIABILITY = 0x04;
    pub const CE = 0x01;
    pub const ECT = 0x02;
    pub const PREC_NETCONTROL = 0xe0;
    pub const PREC_INTERNETCONTROL = 0xc0;
    pub const PREC_CRITIC_ECP = 0xa0;
    pub const PREC_FLASHOVERRIDE = 0x80;
    pub const PREC_FLASH = 0x60;
    pub const PREC_IMMEDIATE = 0x40;
    pub const PREC_PRIORITY = 0x20;
    pub const PREC_ROUTINE = 0x00;
    pub const DSCP_CS0 = 0x00;
    pub const DSCP_LE = 0x04;
    pub const DSCP_CS1 = 0x20;
    pub const DSCP_AF11 = 0x28;
    pub const DSCP_AF12 = 0x30;
    pub const DSCP_AF13 = 0x38;
    pub const DSCP_CS2 = 0x40;
    pub const DSCP_AF21 = 0x48;
    pub const DSCP_AF22 = 0x50;
    pub const DSCP_AF23 = 0x58;
    pub const DSCP_CS3 = 0x60;
    pub const DSCP_AF31 = 0x68;
    pub const DSCP_AF32 = 0x70;
    pub const DSCP_AF33 = 0x78;
    pub const DSCP_CS4 = 0x80;
    pub const DSCP_AF41 = 0x88;
    pub const DSCP_AF42 = 0x90;
    pub const DSCP_AF43 = 0x98;
    pub const DSCP_CS5 = 0xa0;
    pub const DSCP_EF = 0xb8;
    pub const DSCP_CS6 = 0xc0;
    pub const DSCP_CS7 = 0xe0;
    pub const ECN_NOTECT = 0x00;
    pub const ECN_ECT1 = 0x01;
    pub const ECN_ECT0 = 0x02;
    pub const ECN_CE = 0x03;
    pub const ECN_MASK = 0x03;
};



---
File: /std/c/serenity.zig
---

const std = @import("../std.zig");
const assert = std.debug.assert;
const builtin = @import("builtin");
const O = std.c.O;
const clockid_t = std.c.clockid_t;
const pid_t = std.c.pid_t;
const timespec = std.c.timespec;

comptime {
    assert(builtin.os.tag == .serenity); // Prevent access of std.c symbols on wrong OS.
}

// https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/futex.h#L46-L53
pub const FUTEX = struct {
    pub const WAIT = 1;
    pub const WAKE = 2;
    pub const REQUEUE = 3;
    pub const CMP_REQUEUE = 4;
    pub const WAKE_OP = 5;
    pub const WAIT_BITSET = 9;
    pub const WAKE_BITSET = 10;

    pub const CLOCK_REALTIME = 1 << 8;
    pub const PRIVATE_FLAG = 1 << 9;
};

// https://github.com/SerenityOS/serenity/blob/54e79aa1d90bbcb69014255a59afb085802719d3/Kernel/API/POSIX/serenity.h#L18-L36
pub const PERF_EVENT = packed struct(c_int) {
    SAMPLE: bool = false,
    MALLOC: bool = false,
    FREE: bool = false,
    MMAP: bool = false,
    MUNMAP: bool = false,
    PROCESS_CREATE: bool = false,
    PROCESS_EXEC: bool = false,
    PROCESS_EXIT: bool = false,
    THREAD_CREATE: bool = false,
    THREAD_EXIT: bool = false,
    CONTEXT_SWITCH: bool = false,
    KMALLOC: bool = false,
    KFREE: bool = false,
    PAGE_FAULT: bool = false,
    SYSCALL: bool = false,
    SIGNPOST: bool = false,
    FILESYSTEM: bool = false,
};

// https://github.com/SerenityOS/serenity/blob/abc150085f532f123b598949218893cb272ccc4c/Userland/Libraries/LibC/serenity.h

pub extern "c" fn disown(pid: pid_t) c_int;

pub extern "c" fn profiling_enable(pid: pid_t, event_mask: PERF_EVENT) c_int;
pub extern "c" fn profiling_disable(pid: pid_t) c_int;
pub extern "c" fn profiling_free_buffer(pid: pid_t) c_int;

pub extern "c" fn futex(userspace_address: *u32, futex_op: c_int, value: u32, timeout: *const timespec, userspace_address2: *u32, value3: u32) c_int;
pub extern "c" fn futex_wait(userspace_address: *u32, value: u32, abstime: *const timespec, clockid: clockid_t, process_shared: c_int) c_int;
pub extern "c" fn futex_wake(userspace_address: *u32, count: u32, process_shared: c_int) c_int;

pub extern "c" fn purge(mode: c_int) c_int;

pub extern "c" fn perf_event(type: PERF_EVENT, arg1: usize, arg2: usize) c_int;
pub extern "c" fn perf_register_string(string: [*]const u8, string_length: usize) c_int;

pub extern "c" fn get_stack_bounds(user_stack_base: *usize, user_stack_size: *usize) c_int;

pub extern "c" fn anon_create(size: usize, options: O) c_int;

pub extern "c" fn serenity_readlink(path: [*]const u8, path_length: usize, buffer: [*]u8, buffer_size: usize) c_int;
pub extern "c" fn serenity_open(path: [*]const u8, path_length: usize, options: c_int, ...) c_int;

pub extern "c" fn getkeymap(name_buffer: [*]u8, name_buffer_size: usize, map: [*]u32, shift_map: [*]u32, alt_map: [*]u32, altgr_map: [*]u32, shift_altgr_map: [*]u32) c_int;
pub extern "c" fn setkeymap(name: [*]const u8, map: [*]const u32, shift_map: [*]const u32, alt_map: [*]const u32, altgr_map: [*]const u32, shift_altgr_map: [*]const u32) c_int;

// https://github.com/SerenityOS/serenity/blob/5bd8af99be0bc4b2e14f361fd7d7590e6bcfa4d6/Kernel/API/POSIX/netinet/in.h#L29
pub const IP = struct {
    pub const TOS = 1;
    pub const TTL = 2;
    pub const MULTICAST_LOOP = 3;
    pub const ADD_MEMBERSHIP = 4;
    pub const DROP_MEMBERSHIP = 5;
    pub const MULTICAST_IF = 6;
    pub const MULTICAST_TTL = 7;
    pub const BLOCK_SOURCE = 8;
    pub const ADD_SOURCE_MEMBERSHIP = 7;
    pub const DROP_SOURCE_MEMBERSHIP = 8;
    pub const UNBLOCK_SOURCE = 9;
    pub const OPTIONS = 10;
};

//  https://github.com/SerenityOS/serenity/blob/5bd8af99be0bc4b2e14f361fd7d7590e6bcfa4d6/Kernel/API/POSIX/netinet/in.h#L81
pub const IPV6 = struct {
    pub const UNICAST_HOPS = 1;
    pub const MULTICAST_HOPS = 2;
    pub const MULTICAST_LOOP = 3;
    pub const MULTICAST_IF = 4;
    pub const ADD_MEMBERSHIP = 5;
    pub const DROP_MEMBERSHIP = 6;
    pub const V6ONLY = 9;
    pub const JOIN_GROUP = 5;
    pub const LEAVE_GROUP = 6;
    pub const RECVPKTINFO = 10;
    pub const PKTINFO = 11;
    pub const RECVHOPLIMIT = 12;
    pub const HOPLIMIT = 13;
};

// https://github.com/SerenityOS/serenity/blob/5bd8af99be0bc4b2e14f361fd7d7590e6bcfa4d6/Kernel/API/POSIX/netinet/in.h#L40
pub const IPTOS = struct {
    pub const LOWDELAY = 16;
    pub const THROUGHPUT = 8;
    pub const RELIABILITY = 4;
};



---
File: /std/compress/flate/Compress.zig
---

//! Allocates statically ~224K (128K lookup, 96K tokens).
//!
//! The source of an `error.WriteFailed` is always the backing writer. After an
//! `error.WriteFailed`, the `.writer` becomes `.failing` and is unrecoverable.
//!
//! After `finish`, the writer also becomes `.failing` since the stream has
//! been finished. This behavior also applies to `Raw` and `Huffman`.

// Implementation details:
//   A chained hash table is used to find matches. `drain` always preserves `flate.history_len`
//   bytes to use as a history and avoids tokenizing the final bytes since they can be part of
//   a longer match with unwritten bytes (unless it is a `flush`). The minimum match searched
//   for is of length `seq_bytes`. If a match is made, a longer match is also checked for at
//   the next byte (lazy matching) if the last match does not meet the `Options.lazy` threshold.
//
//   Up to `block_token` tokens are accumalated in `buffered_tokens` and are outputted in
//   `write_block` which determines the optimal block type and frequencies.

const builtin = @import("builtin");
const std = @import("std");
const mem = std.mem;
const math = std.math;
const assert = std.debug.assert;
const Io = std.Io;
const Writer = Io.Writer;

const Compress = @This();
const token = @import("token.zig");
const flate = @import("../flate.zig");

/// Until #104 is implemented, a ?u15 takes 4 bytes, which is unacceptable
/// as it doubles the size of this already massive structure.
///
/// Also, there are no `to` / `from` methods because LLVM 21 does not
/// optimize away the conversion from and to `?u15`.
const PackedOptionalU15 = packed struct(u16) {
    value: u15,
    is_null: bool,

    pub fn int(p: PackedOptionalU15) u16 {
        return @bitCast(p);
    }

    pub const null_bit: PackedOptionalU15 = .{ .value = 0, .is_null = true };
};

/// After `finish` is called, all vtable calls with result in `error.WriteFailed`.
writer: Writer,
history_len: u16,
history_end_unhashed: bool,
bit_writer: BitWriter,
buffered_tokens: struct {
    /// List of `TokenBufferEntryHeader`s and their trailing data.
    list: [@as(usize, block_tokens) * 3]u8,
    pos: u32,
    n: u16,
    lit_freqs: [286]u16,
    dist_freqs: [30]u16,

    pub const empty: @This() = .{
        .list = undefined,
        .pos = 0,
        .n = 0,
        .lit_freqs = @splat(0),
        .dist_freqs = @splat(0),
    };
},
lookup: struct {
    /// Indexes are the hashes of four-bytes sequences.
    ///
    /// Values are the positions in `chain` of the previous four bytes with the same hash.
    head: [1 << lookup_hash_bits]PackedOptionalU15,
    /// Values are the non-zero number of bytes backwards in the history with the same hash.
    ///
    /// The relationship of chain indexes and bytes relative to the latest history byte is
    /// `chain_pos -% chain_index = history_index`.
    chain: [32768]PackedOptionalU15,
    /// The index in `chain` which is of the newest byte of the history.
    chain_pos: u15,
},
container: flate.Container,
hasher: flate.Container.Hasher,
opts: Options,

const BitWriter = struct {
    output: *Writer,
    buffered: u7,
    buffered_n: u3,

    pub fn init(w: *Writer) BitWriter {
        return .{
            .output = w,
            .buffered = 0,
            .buffered_n = 0,
        };
    }

    /// Asserts `bits` is zero-extended
    pub fn write(b: *BitWriter, bits: u56, n: u6) Writer.Error!void {
        assert(@as(u8, b.buffered) >> b.buffered_n == 0);
        assert(@as(u57, bits) >> n == 0); // n may be 56 so u57 is needed
        const combined = @shlExact(@as(u64, bits), b.buffered_n) | b.buffered;
        const combined_bits = @as(u6, b.buffered_n) + n;

        const out = try b.output.writableSliceGreedy(8);
        mem.writeInt(u64, out[0..8], combined, .little);
        b.output.advance(combined_bits / 8);

        b.buffered_n = @truncate(combined_bits);
        b.buffered = @intCast(combined >> (combined_bits - b.buffered_n));
    }

    /// Asserts one byte can be written to `b.output` without rebasing.
    pub fn byteAlign(b: *BitWriter) void {
        b.output.unusedCapacitySlice()[0] = b.buffered;
        b.output.advance(@intFromBool(b.buffered_n != 0));
        b.buffered = 0;
        b.buffered_n = 0;
    }

    /// Byte align using only empty flate blocks
    pub fn byteAlignBlocks(b: *BitWriter) Writer.Error!void {
        if (b.buffered_n == 0) return;

        // There are two methods to do this:
        // 1. A store block (5 or 6 bytes)
        // 2. Outputting empty 10-bit fixed blocks until aligned
        //
        // Fixed blocks advance the bit alignment by two, and so can only used for even numbers
        // requiring a maximum of four bytes (three blocks = 30 bits) to which is always more
        // efficient than store blocks.
        if (b.buffered_n & 1 == 0) {
            const splat = (8 - @as(u5, b.buffered_n)) >> 1;
            const bits = splat * 10;
            // fixed eos code is 0, so the only bits are for the block header
            const pattern: u32 = BlockHeader.int(.{ .kind = .fixed, .final = false });
            const splatted = ((pattern << 20) | (pattern << 10) | pattern) >> (30 - bits);
            try b.write(splatted, bits);
        } else {
            try b.write(BlockHeader.int(.{ .kind = .stored, .final = false }), 3);
            try b.output.rebase(0, 5);
            b.byteAlign();
            b.output.writeInt(u16, 0x0000, .little) catch unreachable;
            b.output.writeInt(u16, 0xffff, .little) catch unreachable;
        }

        assert(b.buffered_n == 0);
    }

    pub fn writeClen(
        b: *BitWriter,
        hclen: u4,
        clen_values: []u8,
        clen_extra: []u8,
        clen_codes: [19]u16,
        clen_bits: [19]u4,
    ) Writer.Error!void {
        // Write the first four clen entries seperately since they are always present,
        // and writing them all at once takes too many bits.
        try b.write(clen_bits[token.codegen_order[0]] |
            @shlExact(@as(u6, clen_bits[token.codegen_order[1]]), 3) |
            @shlExact(@as(u9, clen_bits[token.codegen_order[2]]), 6) |
            @shlExact(@as(u12, clen_bits[token.codegen_order[3]]), 9), 12);

        var i = hclen;
        var clen_bits_table: u45 = 0;
        while (i != 0) {
            i -= 1;
            clen_bits_table <<= 3;
            clen_bits_table |= clen_bits[token.codegen_order[4..][i]];
        }
        try b.write(clen_bits_table, @as(u6, hclen) * 3);

        for (clen_values, clen_extra) |value, extra| {
            try b.write(
                clen_codes[value] | @shlExact(@as(u16, extra), clen_bits[value]),
                clen_bits[value] + @as(u3, switch (value) {
                    0...15 => 0,
                    16 => 2,
                    17 => 3,
                    18 => 7,
                    else => unreachable,
                }),
            );
        }
    }
};

/// Number of tokens to accumulate before outputing as a block.
/// The maximum value is `math.maxInt(u16) - 1` since one token is reserved for end-of-block.
const block_tokens: u16 = 1 << 15;
const lookup_hash_bits = 15;
const Hash = u16; // `@Int(.unsigned, lookup_hash_bits)` is not used due to worse optimization (with LLVM 21)
const seq_bytes = 3; // not intended to be changed
const Seq = @Int(.unsigned, seq_bytes * 8);

const TokenBufferEntryHeader = packed struct(u16) {
    kind: enum(u1) {
        /// Followed by non-zero `data` byte literals.
        bytes,
        /// Followed by the length as a byte
        match,
    },
    data: u15,
};

const BlockHeader = packed struct(u3) {
    final: bool,
    kind: enum(u2) { stored, fixed, dynamic, _ },

    pub fn int(h: BlockHeader) u3 {
        return @bitCast(h);
    }

    pub const Dynamic = packed struct(u17) {
        regular: BlockHeader,
        hlit: u5,
        hdist: u5,
        hclen: u4,

        pub fn int(h: Dynamic) u17 {
            return @bitCast(h);
        }
    };
};

fn outputMatch(c: *Compress, dist: u15, len: u8) Writer.Error!void {
    // This must come first. Instead of ensuring a full block is never left buffered,
    // draining it is defered to allow end of stream to be indicated.
    if (c.buffered_tokens.n == block_tokens) {
        @branchHint(.unlikely); // LLVM 21 optimizes this branch as the more likely without
        try c.writeBlock(false);
    }
    const header: TokenBufferEntryHeader = .{ .kind = .match, .data = dist };
    c.buffered_tokens.list[c.buffered_tokens.pos..][0..2].* = @bitCast(header);
    c.buffered_tokens.list[c.buffered_tokens.pos + 2] = len;
    c.buffered_tokens.pos += 3;
    c.buffered_tokens.n += 1;

    c.buffered_tokens.lit_freqs[@as(usize, 257) + token.LenCode.fromVal(len).toInt()] += 1;
    c.buffered_tokens.dist_freqs[token.DistCode.fromVal(dist).toInt()] += 1;
}

fn outputBytes(c: *Compress, bytes: []const u8) Writer.Error!void {
    var remaining = bytes;
    while (remaining.len != 0) {
        if (c.buffered_tokens.n == block_tokens) {
            @branchHint(.unlikely); // LLVM 21 optimizes this branch as the more likely without
            try c.writeBlock(false);
        }

        const n = @min(remaining.len, block_tokens - c.buffered_tokens.n, math.maxInt(u15));
        assert(n != 0);
        const header: TokenBufferEntryHeader = .{ .kind = .bytes, .data = n };
        c.buffered_tokens.list[c.buffered_tokens.pos..][0..2].* = @bitCast(header);
        @memcpy(c.buffered_tokens.list[c.buffered_tokens.pos + 2 ..][0..n], remaining[0..n]);
        c.buffered_tokens.pos += @as(u32, 2) + n;
        c.buffered_tokens.n += n;

        for (remaining[0..n]) |b| {
            c.buffered_tokens.lit_freqs[b] += 1;
        }
        remaining = remaining[n..];
    }
}

fn hash(x: u32) Hash {
    return @intCast((x *% 0x9E3779B1) >> (32 - lookup_hash_bits));
}

/// Trades between speed and compression size.
///
/// Default paramaters are [taken from zlib]
/// (https://github.com/madler/zlib/blob/v1.3.1/deflate.c#L112)
pub const Options = struct {
    /// Perform less lookups when a match of at least this length has been found.
    good: u16,
    /// Stop when a match of at least this length has been found.
    nice: u16,
    /// Don't attempt a lazy match find when a match of at least this length has been found.
    lazy: u16,
    /// Check this many previous locations with the same hash for longer matches.
    chain: u16,

    // zig fmt: off
    pub const level_1: Options = .{ .good =  4, .nice =   8, .lazy =   0, .chain =    4 };
    pub const level_2: Options = .{ .good =  4, .nice =  16, .lazy =   0, .chain =    8 };
    pub const level_3: Options = .{ .good =  4, .nice =  32, .lazy =   0, .chain =   32 };
    pub const level_4: Options = .{ .good =  4, .nice =  16, .lazy =   4, .chain =   16 };
    pub const level_5: Options = .{ .good =  8, .nice =  32, .lazy =  16, .chain =   32 };
    pub const level_6: Options = .{ .good =  8, .nice = 128, .lazy =  16, .chain =  128 };
    pub const level_7: Options = .{ .good =  8, .nice = 128, .lazy =  32, .chain =  256 };
    pub const level_8: Options = .{ .good = 32, .nice = 258, .lazy = 128, .chain = 1024 };
    pub const level_9: Options = .{ .good = 32, .nice = 258, .lazy = 258, .chain = 4096 };
     // zig fmt: on
    pub const fastest = level_1;
    pub const default = level_6;
    pub const best = level_9;
};

/// It is asserted `buffer` is least `flate.max_window_len` bytes.
/// It is asserted `output` has a capacity of at least 8 bytes.
pub fn init(
    output: *Writer,
    buffer: []u8,
    container: flate.Container,
    opts: Options,
) Writer.Error!Compress {
    assert(output.buffer.len > 8);
    assert(buffer.len >= flate.max_window_len);

    // note that disallowing some of these simplifies matching logic
    assert(opts.chain != 0); // use `Huffman`; disallowing this simplies matching
    assert(opts.good >= 3 and opts.nice >= 3); // a match will (usually) not be found
    assert(opts.good <= 258 and opts.nice <= 258); // a longer match will not be found
    assert(opts.lazy <= opts.nice); // a longer match will (usually) not be found
    if (opts.good <= opts.lazy) assert(opts.chain >= 1 << 2); // chain can be reduced to zero

    try output.writeAll(container.header());
    return .{
        .writer = .{
            .buffer = buffer,
            .vtable = &.{
                .drain = drain,
                .flush = flush,
                .rebase = rebase,
            },
        },
        .history_len = 0,
        .history_end_unhashed = false,
        .bit_writer = .init(output),
        .buffered_tokens = .empty,
        .lookup = .{
            // init `value` is max so there is 0xff pattern
            .head = @splat(.{ .value = math.maxInt(u15), .is_null = true }),
            .chain = undefined,
            .chain_pos = math.maxInt(u15),
        },
        .container = container,
        .opts = opts,
        .hasher = .init(container),
    };
}

fn drain(w: *Writer, data: []const []const u8, splat: usize) Writer.Error!usize {
    errdefer w.* = .failing;
    // There may have not been enough space in the buffer and the write was sent directly here.
    // However, it is required that all data goes through the buffer to keep a history.
    const data_n = w.buffer.len - w.end;
    _ = w.fixedDrain(data, splat) catch {};
    assert(w.end == w.buffer.len);
    try rebaseInner(w, 0, 1, false, false);
    return data_n;
}

fn flush(w: *Writer) Writer.Error!void {
    errdefer w.* = .failing;
    try rebaseInner(w, 0, w.buffer.len - flate.history_len, true, false);
    const c: *Compress = @fieldParentPtr("writer", w);
    try c.bit_writer.byteAlignBlocks();
}

pub fn finish(c: *Compress) Writer.Error!void {
    defer c.writer = .failing;
    try rebaseInner(&c.writer, 0, c.writer.buffer.len - flate.history_len, true, true);
    try c.bit_writer.output.rebase(0, 1);
    c.bit_writer.byteAlign();
    try c.hasher.writeFooter(c.bit_writer.output);
}

fn rebase(w: *Writer, preserve: usize, capacity: usize) Writer.Error!void {
    errdefer w.* = .failing;
    return rebaseInner(w, preserve, capacity, false, false);
}

pub const rebase_min_preserve = flate.history_len;
pub const rebase_reserved_capacity = (token.max_length + 1) + seq_bytes;

fn rebaseInner(
    w: *Writer,
    preserve: usize,
    capacity: usize,
    is_flush: bool,
    is_finish: bool,
) Writer.Error!void {
    if (!is_flush) {
        assert(@max(preserve, rebase_min_preserve) + (capacity + rebase_reserved_capacity) <= w.buffer.len);
    } else {
        // Preverse is not considered for `matching_end`
        assert(preserve == 0 and capacity == w.buffer.len - flate.history_len);
    }
    if (is_finish) assert(is_flush);

    const c: *Compress = @fieldParentPtr("writer", w);
    const buffered = w.buffered();

    const start: usize = c.history_len;
    const hashable_len = buffered.len -| (seq_bytes - 1);
    const matching_end: usize = if (!is_flush)
        buffered.len - rebase_reserved_capacity - (preserve -| flate.history_len)
    else
        hashable_len;

    var i = start;
    var last_unmatched = i;
    var seq: Seq = start_seq: {
        if (c.history_end_unhashed) {
            @branchHint(.unlikely);

            assert(i != 0);
            i -|= seq_bytes - 1;
            var seq: Seq = mem.readInt(
                @Int(.unsigned, (seq_bytes - 1) * 8),
                w.buffer[i..][0 .. seq_bytes - 1],
                .big,
            );

            while (i < @min(start, hashable_len)) {
                seq <<= 8;
                seq |= buffered[i + (seq_bytes - 1)];
                c.addHash(i, hash(seq));
                i += 1;
            }

            if (i < start) {
                @branchHint(.unlikely);
                i = start;
                assert(i >= hashable_len);
                assert(i >= matching_end);
                assert(is_flush);
                break :start_seq undefined; // Unused
            }

            c.history_end_unhashed = false;
            break :start_seq seq;
        }

        if (i >= hashable_len) {
            @branchHint(.unlikely);
            assert(i >= matching_end);
            assert(is_flush);
            break :start_seq undefined; // Unused
        }

        break :start_seq mem.readInt(
            @Int(.unsigned, (seq_bytes - 1) * 8),
            buffered[i..][0 .. seq_bytes - 1],
            .big,
        );
    };

    while (i < matching_end) {
        var match_start = i;
        seq <<= 8;
        seq |= buffered[i + (seq_bytes - 1)];
        var match = c.matchAndAddHash(i, hash(seq), token.min_length - 1, c.opts.chain, c.opts.good);
        i += 1;
        if (match.len < token.min_length) continue;

        var match_unadded = match.len - 1;
        lazy: {
            if (match.len >= c.opts.lazy) break :lazy;
            if (match.len >= c.writer.buffered()[i..].len) {
                @branchHint(.unlikely); // Only end of stream
                break :lazy;
            }

            var chain = c.opts.chain;
            var good = c.opts.good;
            if (match.len >= good) {
                chain >>= 2;
                good = math.maxInt(u8); // Reduce only once
            }

            seq <<= 8;
            seq |= buffered[i + (seq_bytes - 1)];
            const lazy = c.matchAndAddHash(i, hash(seq), match.len, chain, good);
            match_unadded -= 1;
            i += 1;

            if (lazy.len > match.len) {
                match_start += 1;
                match = lazy;
                match_unadded = match.len - 1;
            }
        }

        assert(i + match_unadded == match_start + match.len);
        assert(mem.eql(
            u8,
            buffered[match_start..][0..match.len],
            buffered[match_start - 1 - match.dist ..][0..match.len],
        )); // This assert also seems to help codegen.

        try c.outputBytes(buffered[last_unmatched..match_start]);
        try c.outputMatch(@intCast(match.dist), @intCast(match.len - 3));
        last_unmatched = match_start + match.len;

        while (i < hashable_len) {
            seq <<= 8;
            seq |= buffered[i + (seq_bytes - 1)];
            c.addHash(i, hash(seq));
            i += 1;

            match_unadded -= 1;
            if (match_unadded == 0) break;
        } else {
            @branchHint(.unlikely);
            assert(is_flush);
            // `c.history_end_unhashed` is set down below
            break;
        }
        assert(i == match_start + match.len);
    }

    if (is_flush) {
        try c.outputBytes(buffered[last_unmatched..]);
        c.hasher.update(buffered[start..]);

        if (is_finish) {
            try c.writeBlock(true);
            return; // Other state does not need updated since the writer transitions to `.failing`
        }

        i = buffered.len;
        c.history_end_unhashed = i != 0;

        if (c.buffered_tokens.n != 0) {
            try c.writeBlock(false);
        }
    } else {
        try c.outputBytes(buffered[last_unmatched..i]);
        c.hasher.update(buffered[start..i]);
    }

    c.history_len = @min(i, flate.history_len);
    const preserved = buffered[i - c.history_len ..];
    if (!is_flush) assert(preserved.len >= @max(rebase_min_preserve, preserve));
    @memmove(w.buffer[0..preserved.len], preserved);
    w.end = preserved.len;
}

fn addHash(c: *Compress, i: usize, h: Hash) void {
    assert(h == hash(mem.readInt(Seq, c.writer.buffer[i..][0..seq_bytes], .big)));

    const l = &c.lookup;
    l.chain_pos +%= 1;

    // Equivilent to the below, however LLVM 21 does not optimize `@subWithOverflow` well at all.
    // const replaced_i, const no_replace = @subWithOverflow(i, flate.history_len);
    // if (no_replace == 0) {
    if (i >= flate.history_len) {
        @branchHint(.likely);
        const replaced_i = i - flate.history_len;
        // The following is the same as the below except uses a 32-bit load to help optimizations
        // const replaced_seq = mem.readInt(Seq, c.writer.buffer[replaced_i..][0..seq_bytes], .big);
        comptime assert(@sizeOf(Seq) <= @sizeOf(u32));
        const replaced_u32 = mem.readInt(u32, c.writer.buffered()[replaced_i..][0..4], .big);
        const replaced_seq: Seq = @intCast(replaced_u32 >> (32 - @bitSizeOf(Seq)));

        const replaced_h = hash(replaced_seq);
        // The following is equivilent to the below since LLVM 21 doesn't optimize it well.
        // l.head[replaced_h].is_null = l.head[replaced_h].is_null or
        //     l.head[replaced_h].int() == l.chain_pos;
        const empty_head = l.head[replaced_h].int() == l.chain_pos;
        const null_flag = PackedOptionalU15.int(.{ .is_null = empty_head, .value = 0 });
        l.head[replaced_h] = @bitCast(l.head[replaced_h].int() | null_flag);
    }

    const prev_chain_index = l.head[h];
    l.chain[l.chain_pos] = @bitCast((l.chain_pos -% prev_chain_index.value) |
        (prev_chain_index.int() & PackedOptionalU15.null_bit.int())); // Preserves null
    l.head[h] = .{ .value = l.chain_pos, .is_null = false };
}

/// If the match is shorter, the returned value can be any value `<= old`.
fn betterMatchLen(old: u16, prev: []const u8, bytes: []const u8) u16 {
    assert(old < @min(bytes.len, token.max_length));
    assert(prev.len >= bytes.len);
    assert(bytes.len >= token.min_length);

    var i: u16 = 0;
    const Block = @Int(.unsigned, @min(math.divCeil(
        comptime_int,
        math.ceilPowerOfTwoAssert(usize, @bitSizeOf(usize)),
        8,
    ) catch unreachable, 256) * 8);

    if (bytes.len < token.max_length) {
        @branchHint(.unlikely); // Only end of stream

        while (bytes[i..].len >= @sizeOf(Block)) {
            const a = mem.readInt(Block, prev[i..][0..@sizeOf(Block)], .little);
            const b = mem.readInt(Block, bytes[i..][0..@sizeOf(Block)], .little);
            const diff = a ^ b;
            if (diff != 0) {
                @branchHint(.likely);
                i += @ctz(diff) / 8;
                return i;
            }
            i += @sizeOf(Block);
        }

        while (i != bytes.len and prev[i] == bytes[i]) {
            i += 1;
        }
        assert(i < token.max_length);
        return i;
    }

    if (old >= @sizeOf(Block)) {
        // Check that a longer end is present, otherwise the match is always worse
        const a = mem.readInt(Block, prev[old + 1 - @sizeOf(Block) ..][0..@sizeOf(Block)], .little);
        const b = mem.readInt(Block, bytes[old + 1 - @sizeOf(Block) ..][0..@sizeOf(Block)], .little);
        if (a != b) return i;
    }

    while (true) {
        const a = mem.readInt(Block, prev[i..][0..@sizeOf(Block)], .little);
        const b = mem.readInt(Block, bytes[i..][0..@sizeOf(Block)], .little);
        const diff = a ^ b;
        if (diff != 0) {
            i += @ctz(diff) / 8;
            return i;
        }
        i += @sizeOf(Block);
        if (i == 256) break;
    }

    const a = mem.readInt(u16, prev[i..][0..2], .little);
    const b = mem.readInt(u16, bytes[i..][0..2], .little);
    const diff = a ^ b;
    i += @ctz(diff) / 8;
    assert(i <= token.max_length);
    return i;
}

test betterMatchLen {
    try std.testing.fuzz({}, testFuzzedMatchLen, .{});
}

fn testFuzzedMatchLen(_: void, smith: *std.testing.Smith) !void {
    @disableInstrumentation();
    var buf: [1024]u8 = undefined;
    var w: Writer = .fixed(&buf);

    while (w.unusedCapacityLen() != 0 and !smith.eosWeightedSimple(7, 1)) {
        switch (smith.value(enum(u2) { splat, copy, insert })) {
            .splat => w.splatByteAll(
                smith.value(u8),
                smith.valueRangeAtMost(u9, 1, @min(511, w.unusedCapacityLen())),
            ) catch unreachable,
            .copy => write: {
                if (w.buffered().len == 0) continue;
                const start = smith.valueRangeAtMost(u10, 0, @intCast(w.buffered().len - 1));
                const max_len = @min(w.unusedCapacityLen(), w.buffered().len - start);
                const len = smith.valueRangeAtMost(u10, 1, @intCast(max_len));
                break :write w.writeAll(w.buffered()[start..][0..len]) catch unreachable;
            },
            .insert => w.advance(smith.slice(w.unusedCapacitySlice())),
        }
    }
    w.splatByteAll(0, (1 + token.min_length) -| w.buffered().len) catch unreachable;

    const max_start = w.buffered().len - token.min_length;
    const bytes_off = smith.valueRangeAtMost(u10, 1, @intCast(max_start));
    const prev_off = smith.valueRangeAtMost(u10, 0, bytes_off - 1);
    const prev = w.buffered()[prev_off..];
    const bytes = w.buffered()[bytes_off..];
    const old = smith.valueRangeLessThan(u10, 0, @min(bytes.len, token.max_length));

    const diff_index = mem.findDiff(u8, prev, bytes).?; // unwrap since lengths are not same
    const expected_len = @min(diff_index, 258);
    errdefer std.debug.print(
        \\prev : '{any}'
        \\bytes: '{any}'
        \\old     : {}
        \\expected: {?}
        \\actual  : {}
    ++ "\n", .{
        prev,                                           bytes,                            old,
        if (old < expected_len) expected_len else null, betterMatchLen(old, prev, bytes),
    });
    if (old < expected_len) {
        try std.testing.expectEqual(expected_len, betterMatchLen(old, prev, bytes));
    } else {
        try std.testing.expect(betterMatchLen(old, prev, bytes) <= old);
    }
}

fn matchAndAddHash(c: *Compress, i: usize, h: Hash, gt: u16, max_chain: u16, good_: u16) struct {
    dist: u16,
    len: u16,
} {
    const l = &c.lookup;
    const buffered = c.writer.buffered();

    var chain_limit = max_chain;
    var best_dist: u16 = undefined;
    var best_len = gt;
    const nice = @min(c.opts.nice, buffered[i..].len);
    var good = good_;

    search: {
        if (l.head[h].is_null) break :search;
        // Actually a u15, but LLVM 21 does not optimize that as well (it truncates it each use).
        var dist: u16 = l.chain_pos -% l.head[h].value;
        while (true) {
            chain_limit -= 1;

            const match_len = betterMatchLen(best_len, buffered[i - 1 - dist ..], buffered[i..]);
            if (match_len > best_len) {
                best_dist = dist;
                best_len = match_len;
                if (best_len >= nice) break;
                if (best_len >= good) {
                    chain_limit >>= 2;
                    good = math.maxInt(u8); // Reduce only once
                }
            }

            if (chain_limit == 0) break;
            const next_chain_index = l.chain_pos -% @as(u15, @intCast(dist));
            // Equivilent to the below, however LLVM 21 optimizes the below worse.
            // if (l.chain[next_chain_index].is_null) break;
            // dist, const out_of_window = @addWithOverflow(dist, l.chain[next_chain_index].value);
            // if (out_of_window == 1) break;
            dist +%= l.chain[next_chain_index].int(); // wrapping for potential null bit
            comptime assert(flate.history_len == PackedOptionalU15.int(.null_bit));
            // Also, doing >= flate.history_len gives worse codegen with LLVM 21.
            if ((dist | l.chain[next_chain_index].int()) & flate.history_len != 0) break;
        }
    }

    c.addHash(i, h);
    return .{ .dist = best_dist, .len = best_len };
}

fn clenHlen(freqs: [19]u16) u4 {
    // Note that the first four codes (16, 17, 18, and 0) are always present.
    if (builtin.mode != .ReleaseSmall and (std.simd.suggestVectorLength(u16) orelse 1) >= 8) {
        const V = @Vector(16, u16);
        const hlen_mul: V = comptime m: {
            var hlen_mul: [16]u16 = undefined;
            for (token.codegen_order[3..], 0..) |i, hlen| {
                hlen_mul[i] = hlen;
            }
            break :m hlen_mul;
        };
        const encoded = freqs[0..16].* != @as(V, @splat(0));
        return @intCast(@reduce(.Max, @intFromBool(encoded) * hlen_mul));
    } else {
        var max: u4 = 0;
        for (token.codegen_order[4..], 1..) |i, len| {
            max = if (freqs[i] == 0) max else @intCast(len);
        }
        return max;
    }
}

test clenHlen {
    var freqs: [19]u16 = @splat(0);
    try std.testing.expectEqual(0, clenHlen(freqs));
    for (token.codegen_order, 1..) |i, len| {
        freqs[i] = 1;
        try std.testing.expectEqual(len -| 4, clenHlen(freqs));
        freqs[i] = 0;
    }
}

/// Returns the number of values followed by the bitsize of the extra bits.
fn buildClen(
    dyn_bits: []const u4,
    out_values: []u8,
    out_extra: []u8,
    out_freqs: *[19]u16,
) struct { u16, u16 } {
    assert(dyn_bits.len <= out_values.len);
    assert(out_values.len == out_extra.len);

    var len: u16 = 0;
    var extra_bitsize: u16 = 0;

    var remaining_bits = dyn_bits;
    var prev: u4 = 0;
    while (true) {
        const b = remaining_bits[0];
        const n_max = @min(@as(u8, if (b != 0)
            if (b != prev) 1 else 6
        else
            138), remaining_bits.len);
        prev = b;

        var n: u8 = 0;
        while (true) {
            remaining_bits = remaining_bits[1..];
            n += 1;
            if (n == n_max or remaining_bits[0] != b) break;
        }
        const code, const extra, const xsize = switch (n) {
            0 => unreachable,
            1...2 => .{ b, 0, 0 },
            3...10 => .{
                @as(u8, 16) + @intFromBool(b == 0),
                n - 3,
                @as(u8, 2) + @intFromBool(b == 0),
            },
            11...138 => .{ 18, n - 11, 7 },
            else => unreachable,
        };
        while (true) {
            out_values[len] = code;
            out_extra[len] = extra;
            out_freqs[code] += 1;
            extra_bitsize += xsize;
            len += 1;
            if (n != 2) {
                @branchHint(.likely);
                break;
            }
            // Code needs outputted once more
            n = 1;
        }
        if (remaining_bits.len == 0) break;
    }

    return .{ len, extra_bitsize };
}

test buildClen {
    //dyn_bits: []u4,
    //out_values: *[288 + 30]u8,
    //out_extra: *[288 + 30]u8,
    //out_freqs: *[19]u16,
    //struct { u16, u16 }
    var out_values: [288 + 30]u8 = undefined;
    var out_extra: [288 + 30]u8 = undefined;
    var out_freqs: [19]u16 = @splat(0);
    const len, const extra_bitsize = buildClen(&([_]u4{
        1, // A
        2, 2, // B
        3, 3, 3, // C
        4, 4, 4, 4, // D
        5, // E
        5, 5, 5, 5, 5, 5, //
        5, 5, 5, 5, 5, 5,
        5, 5,
        0, 1, // F
        0, 0, 1, // G
    } ++ @as([138 + 10]u4, @splat(0)) // H
    ), &out_values, &out_extra, &out_freqs);
    try std.testing.expectEqualSlices(u8, &.{
        1, // A
        2, 2, // B
        3, 3, 3, // C
        4, 16, // D
        5, 16, 16, 5, 5, // E
        0, 1, // F
        0, 0, 1, // G
        18, 17, // H
    }, out_values[0..len]);
    try std.testing.expectEqualSlices(u8, &.{
        0, // A
        0, 0, // B
        0, 0, 0, // C
        0, (0), // D
        0, (3), (3), 0, 0, // E
        0, 0, // F
        0, 0, 0, // G
        (127), (7), // H
    }, out_extra[0..len]);
    try std.testing.expectEqual(2 + 2 + 2 + 7 + 3, extra_bitsize);
    try std.testing.expectEqualSlices(u16, &.{
        3, 3, 2, 3, 1, 3, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        3, 1, 1,
    }, &out_freqs);
}

fn writeBlock(c: *Compress, eos: bool) Writer.Error!void {
    const toks = &c.buffered_tokens;
    assert(toks.lit_freqs[256] == 0);
    toks.lit_freqs[256] = 1;

    var dyn_codes_buf: [286 + 30]u16 = undefined;
    var dyn_bits_buf: [286 + 30]u4 = @splat(0);

    const dyn_lit_codes_bitsize, const dyn_last_lit = huffman.build(
        &toks.lit_freqs,
        dyn_codes_buf[0..286],
        dyn_bits_buf[0..286],
        15,
        true,
    );
    const dyn_lit_len = @max(257, dyn_last_lit + 1);

    const dyn_dist_codes_bitsize, const dyn_last_dist = huffman.build(
        &toks.dist_freqs,
        dyn_codes_buf[dyn_lit_len..][0..30],
        dyn_bits_buf[dyn_lit_len..][0..30],
        15,
        true,
    );
    const dyn_dist_len = @max(1, dyn_last_dist + 1);

    var clen_values: [288 + 30]u8 = undefined;
    var clen_extra: [288 + 30]u8 = undefined;
    var clen_freqs: [19]u16 = @splat(0);
    const clen_len, const clen_extra_bitsize = buildClen(
        dyn_bits_buf[0 .. dyn_lit_len + dyn_dist_len],
        &clen_values,
        &clen_extra,
        &clen_freqs,
    );

    var clen_codes: [19]u16 = undefined;
    var clen_bits: [19]u4 = @splat(0);
    const clen_codes_bitsize, _ = huffman.build(
        &clen_freqs,
        &clen_codes,
        &clen_bits,
        7,
        false,
    );
    const hclen = clenHlen(clen_freqs);

    const dynamic_bitsize = @as(u32, 14) +
        (4 + @as(u6, hclen)) * 3 + clen_codes_bitsize + clen_extra_bitsize +
        dyn_lit_codes_bitsize + dyn_dist_codes_bitsize;
    const fixed_bitsize = n: {
        const freq7 = 1; // eos
        var freq8: u16 = 0;
        var freq9: u16 = 0;
        var freq12: u16 = 0; // 7 + 5 - match freqs always have corresponding 5-bit dist freq
        var freq13: u16 = 0; // 8 + 5
        for (toks.lit_freqs[0..144]) |f| freq8 += f;
        for (toks.lit_freqs[144..256]) |f| freq9 += f;
        assert(toks.lit_freqs[256] == 1);
        for (toks.lit_freqs[257..280]) |f| freq12 += f;
        for (toks.lit_freqs[280..286]) |f| freq13 += f;
        break :n @as(u32, freq7) * 7 +
            @as(u32, freq8) * 8 + @as(u32, freq9) * 9 +
            @as(u32, freq12) * 12 + @as(u32, freq13) * 13;
    };

    stored: {
        for (toks.dist_freqs) |n| if (n != 0) break :stored;
        // No need to check len frequencies since they each have a corresponding dist frequency
        assert(for (toks.lit_freqs[257..]) |f| (if (f != 0) break false) else true);

        // No matches. If the stored size is smaller than the huffman-encoded version, it will be
        // outputed in a store block. This is not done with matches since the original input would
        // need to be stored since the window may slid, and it may also exceed 65535 bytes. This
        // should be OK since most inputs with matches should be more compressable anyways.
        const stored_align_bits = -%(c.bit_writer.buffered_n +% 3);
        const stored_bitsize = stored_align_bits + @as(u32, 32) + @as(u32, toks.n) * 8;
        if (@min(dynamic_bitsize, fixed_bitsize) < stored_bitsize) break :stored;

        try c.bit_writer.write(BlockHeader.int(.{ .kind = .stored, .final = eos }), 3);
        try c.bit_writer.output.rebase(0, 5);
        c.bit_writer.byteAlign();
        c.bit_writer.output.writeInt(u16, c.buffered_tokens.n, .little) catch unreachable;
        c.bit_writer.output.writeInt(u16, ~c.buffered_tokens.n, .little) catch unreachable;

        // Relatively small buffer since regular draining will
        // always consume slightly less than 2 << 15 bytes.
        var vec_buf: [4][]const u8 = undefined;
        var vec_n: usize = 0;
        var i: usize = 0;

        assert(c.buffered_tokens.pos != 0);
        while (i != c.buffered_tokens.pos) {
            const h: TokenBufferEntryHeader = @bitCast(toks.list[i..][0..2].*);
            assert(h.kind == .bytes);

            i += 2;
            vec_buf[vec_n] = toks.list[i..][0..h.data];
            i += h.data;

            vec_n += 1;
            if (i == c.buffered_tokens.pos or vec_n == vec_buf.len) {
                try c.bit_writer.output.writeVecAll(vec_buf[0..vec_n]);
                vec_n = 0;
            }
        }

        toks.* = .empty;
        return;
    }

    const lit_codes, const lit_bits, const dist_codes, const dist_bits =
        if (dynamic_bitsize < fixed_bitsize) codes: {
            try c.bit_writer.write(BlockHeader.Dynamic.int(.{
                .regular = .{ .final = eos, .kind = .dynamic },
                .hlit = @intCast(dyn_lit_len - 257),
                .hdist = @intCast(dyn_dist_len - 1),
                .hclen = hclen,
            }), 17);
            try c.bit_writer.writeClen(
                hclen,
                clen_values[0..clen_len],
                clen_extra[0..clen_len],
                clen_codes,
                clen_bits,
            );
            break :codes .{
                dyn_codes_buf[0..dyn_lit_len],
                dyn_bits_buf[0..dyn_lit_len],
                dyn_codes_buf[dyn_lit_len..][0..dyn_dist_len],
                dyn_bits_buf[dyn_lit_len..][0..dyn_dist_len],
            };
        } else codes: {
            try c.bit_writer.write(BlockHeader.int(.{ .final = eos, .kind = .fixed }), 3);
            break :codes .{
                &token.fixed_lit_codes,
                &token.fixed_lit_bits,
                &token.fixed_dist_codes,
                &token.fixed_dist_bits,
            };
        };

    var i: usize = 0;
    while (i != toks.pos) {
        const h: TokenBufferEntryHeader = @bitCast(toks.list[i..][0..2].*);
        i += 2;
        if (h.kind == .bytes) {
            for (toks.list[i..][0..h.data]) |b| {
                try c.bit_writer.write(lit_codes[b], lit_bits[b]);
            }
            i += h.data;
        } else {
            const dist = h.data;
            const len = toks.list[i];
            i += 1;
            const dist_code = token.DistCode.fromVal(dist);
            const len_code = token.LenCode.fromVal(len);
            const dist_val = dist_code.toInt();
            const lit_val = @as(u16, 257) + len_code.toInt();

            var out: u48 = lit_codes[lit_val];
            var out_bits: u6 = lit_bits[lit_val];
            out |= @shlExact(@as(u20, len - len_code.base()), @intCast(out_bits));
            out_bits += len_code.extraBits();

            out |= @shlExact(@as(u35, dist_codes[dist_val]), out_bits);
            out_bits += dist_bits[dist_val];
            out |= @shlExact(@as(u48, dist - dist_code.base()), out_bits);
            out_bits += dist_code.extraBits();

            try c.bit_writer.write(out, out_bits);
        }
    }
    try c.bit_writer.write(lit_codes[256], lit_bits[256]);

    toks.* = .empty;
}

/// Huffman tree construction.
///
/// The approach for building the huffman tree is [taken from zlib]
/// (https://github.com/madler/zlib/blob/v1.3.1/trees.c#L625) with some modifications.
const huffman = struct {
    const max_leafs = 286;
    const max_nodes = max_leafs * 2;

    const Node = packed struct(u32) {
        depth: u16,
        freq: u16,

        pub const Index = u16;

        /// `freq` is more significant than `depth`
        pub fn smaller(a: Node, b: Node) bool {
            return @as(u32, @bitCast(a)) < @as(u32, @bitCast(b));
        }
    };

    fn heapSiftDown(nodes: []Node, heap: []Node.Index, start: usize) void {
        var i = start;
        while (true) {
            var min = i;
            const l = i * 2 + 1;
            const r = l + 1;
            min = if (l < heap.len and nodes[heap[l]].smaller(nodes[heap[min]])) l else min;
            min = if (r < heap.len and nodes[heap[r]].smaller(nodes[heap[min]])) r else min;
            if (i == min) break;
            mem.swap(Node.Index, &heap[i], &heap[min]);
            i = min;
        }
    }

    fn heapRemoveRoot(nodes: []Node, heap: []Node.Index) void {
        heap[0] = heap[heap.len - 1];
        heapSiftDown(nodes, heap[0 .. heap.len - 1], 0);
    }

    /// Returns the total bits to encode `freqs` followed by the index of the last non-zero bits.
    /// For `freqs[i]` == 0, `out_codes[i]` will be undefined.
    /// It is asserted `out_bits` is zero-filled.
    /// It is asserted `out_bits.len` is at least a length of
    /// one if ncomplete trees are allowed and two otherwise.
    pub fn build(
        freqs: []const u16,
        out_codes: []u16,
        out_bits: []u4,
        max_bits: u4,
        incomplete_allowed: bool,
    ) struct { u32, u16 } {
        assert(out_codes.len - 1 >= @intFromBool(!incomplete_allowed));
        // freqs and out_codes are in the loop to assert they are all the same length
        for (freqs, out_codes, out_bits) |_, _, n| assert(n == 0);
        assert(out_codes.len <= @as(u16, 1) << max_bits);

        // Indexes 0..freqs are leafs, indexes max_leafs.. are internal nodes.
        var tree_nodes: [max_nodes]Node = undefined;
        var tree_parent_nodes: [max_nodes]Node.Index = undefined;
        var nodes_end: u16 = max_leafs;
        // Dual-purpose buffer. Nodes are ordered by least frequency or when equal, least depth.
        // The start is a min heap of level-zero nodes.
        // The end is a sorted buffer of nodes with the greatest first.
        var node_buf: [max_nodes]Node.Index = undefined;
        var heap_end: u16 = 0;
        var sorted_start: u16 = node_buf.len;

        for (0.., freqs) |n, freq| {
            tree_nodes[n] = .{ .freq = freq, .depth = 0 };
            node_buf[heap_end] = @intCast(n);
            heap_end += @intFromBool(freq != 0);
        }

        // There must be at least one code at minimum,
        node_buf[heap_end] = 0;
        heap_end += @intFromBool(heap_end == 0);
        // and at least two if incomplete must be avoided.
        if (heap_end == 1 and incomplete_allowed) {
            @branchHint(.unlikely); // LLVM 21 optimizes this branch as the more likely without

            // Codes must have at least one-bit, so this is a special case.
            out_bits[node_buf[0]] = 1;
            out_codes[node_buf[0]] = 0;
            return .{ freqs[node_buf[0]], node_buf[0] };
        }
        const last_nonzero = @max(node_buf[heap_end - 1], 1); // For heap_end > 1, last is not be 0
        node_buf[heap_end] = @intFromBool(node_buf[0] == 0);
        heap_end += @intFromBool(heap_end == 1);

        // Heapify the array of frequencies
        const heapify_final = heap_end - 1;
        const heapify_start = (heapify_final - 1) / 2; // Parent of final node
        var heapify_i = heapify_start;
        while (true) {
            heapSiftDown(&tree_nodes, node_buf[0..heap_end], heapify_i);
            if (heapify_i == 0) break;
            heapify_i -= 1;
        }

        // Build optimal tree. `max_bits` is not enforced yet.
        while (heap_end > 1) {
            const a = node_buf[0];
            heapRemoveRoot(&tree_nodes, node_buf[0..heap_end]);
            heap_end -= 1;
            const b = node_buf[0];

            sorted_start -= 2;
            node_buf[sorted_start..][0..2].* = .{ b, a };

            tree_nodes[nodes_end] = .{
                .freq = tree_nodes[a].freq + tree_nodes[b].freq,
                .depth = @max(tree_nodes[a].depth, tree_nodes[b].depth) + 1,
            };
            defer nodes_end += 1;
            tree_parent_nodes[a] = nodes_end;
            tree_parent_nodes[b] = nodes_end;

            node_buf[0] = nodes_end;
            heapSiftDown(&tree_nodes, node_buf[0..heap_end], 0);
        }
        sorted_start -= 1;
        node_buf[sorted_start] = node_buf[0];

        var bit_counts: [16]u16 = @splat(0);
        buildBits(out_bits, &bit_counts, &tree_parent_nodes, node_buf[sorted_start..], max_bits);
        return .{ buildValues(freqs, out_codes, out_bits, bit_counts), last_nonzero };
    }

    fn buildBits(
        out_bits: []u4,
        bit_counts: *[16]u16,
        parent_nodes: *[max_nodes]Node.Index,
        sorted: []Node.Index,
        max_bits: u4,
    ) void {
        var internal_node_bits: [max_nodes - max_leafs]u4 = undefined;
        var overflowed: u16 = 0;

        internal_node_bits[sorted[0] - max_leafs] = 0; // root
        for (sorted[1..]) |i| {
            const parent_bits = internal_node_bits[parent_nodes[i] - max_leafs];
            overflowed += @intFromBool(parent_bits == max_bits);
            const bits = parent_bits + @intFromBool(parent_bits != max_bits);
            bit_counts[bits] += @intFromBool(i < max_leafs);
            (if (i >= max_leafs) &internal_node_bits[i - max_leafs] else &out_bits[i]).* = bits;
        }

        if (overflowed == 0) {
            @branchHint(.likely);
            return;
        }

        outer: while (true) {
            var deepest: u4 = max_bits - 1;
            while (bit_counts[deepest] == 0) deepest -= 1;
            while (overflowed != 0) {
                // Insert an internal node under the leaf and move an overflow as its sibling
                bit_counts[deepest] -= 1;
                bit_counts[deepest + 1] += 2;
                // Only overflow moved. Its sibling's depth is one less, however is still >= depth.
                bit_counts[max_bits] -= 1;
                overflowed -= 2;

                if (overflowed == 0) break :outer;
                deepest += 1;
                if (deepest == max_bits) continue :outer;
            }
        }

        // Reassign bit lengths
        assert(bit_counts[0] == 0);
        var i: usize = 0;
        for (1.., bit_counts[1..]) |bits, all| {
            var remaining = all;
            while (remaining != 0) {
                defer i += 1;
                if (sorted[i] >= max_leafs) continue;
                out_bits[sorted[i]] = @intCast(bits);
                remaining -= 1;
            }
        }
        assert(for (sorted[i..]) |n| { // all leafs consumed
            if (n < max_leafs) break false;
        } else true);
    }

    fn buildValues(freqs: []const u16, out_codes: []u16, bits: []u4, bit_counts: [16]u16) u32 {
        var code: u16 = 0;
        var base: [16]u16 = undefined;
        assert(bit_counts[0] == 0);
        for (bit_counts[1..], base[1..]) |c, *b| {
            b.* = code;
            code +%= c;
            code <<= 1;
        }
        var freq_sums: [16]u16 = @splat(0);
        for (out_codes, bits, freqs) |*c, b, f| {
            c.* = @bitReverse(base[b]) >> -%b;
            base[b] += 1; // For `b == 0` this is fine since v is specified to be undefined.
            freq_sums[b] += f;
        }
        return @reduce(.Add, @as(@Vector(16, u32), freq_sums) * std.simd.iota(u32, 16));
    }

    test build {
        var codes: [8]u16 = undefined;
        var bits: [8]u4 = undefined;

        const regular_freqs: [8]u16 = .{ 1, 1, 0, 8, 8, 0, 2, 4 };
        // The optimal tree for the above frequencies is
        // 4             1   1
        //                \ /
        // 3           2   #
        //              \ /
        // 2   8   8 4   #
        //      \ /   \ /
        // 1     #     #
        //        \   /
        // 0        #
        bits = @splat(0);
        var n, var lnz = build(&regular_freqs, &codes, &bits, 15, true);
        codes[2] = 0;
        codes[5] = 0;
        try std.testing.expectEqualSlices(u4, &.{ 4, 4, 0, 2, 2, 0, 3, 2 }, &bits);
        try std.testing.expectEqualSlices(u16, &.{
            0b0111, 0b1111, 0, 0b00, 0b10, 0, 0b011, 0b01,
        }, &codes);
        try std.testing.expectEqual(54, n);
        try std.testing.expectEqual(7, lnz);
        // When constrained to 3 bits, it becomes
        // 3        1   1 2   4
        //           \ /   \ /
        // 2   8   8  #     #
        //      \ /    \   /
        // 1     #       #
        //        \     /
        // 0         #
        bits = @splat(0);
        n, lnz = build(&regular_freqs, &codes, &bits, 3, true);
        codes[2] = 0;
        codes[5] = 0;
        try std.testing.expectEqualSlices(u4, &.{ 3, 3, 0, 2, 2, 0, 3, 3 }, &bits);
        try std.testing.expectEqualSlices(u16, &.{
            0b001, 0b101, 0, 0b00, 0b10, 0, 0b011, 0b111,
        }, &codes);
        try std.testing.expectEqual(56, n);
        try std.testing.expectEqual(7, lnz);

        // Empty tree. At least one code should be present
        bits = @splat(0);
        n, lnz = build(&.{ 0, 0 }, codes[0..2], bits[0..2], 15, true);
        try std.testing.expectEqualSlices(u4, &.{ 1, 0 }, bits[0..2]);
        try std.testing.expectEqual(0b0, codes[0]);
        try std.testing.expectEqual(0, n);
        try std.testing.expectEqual(0, lnz);

        // Check all incompletable frequencies are completed
        for ([_][2]u16{ .{ 0, 0 }, .{ 0, 1 }, .{ 1, 0 } }) |incomplete| {
            // Empty tree. Both codes should be present to prevent incomplete trees
            bits = @splat(0);
            n, lnz = build(&incomplete, codes[0..2], bits[0..2], 15, false);
            try std.testing.expectEqualSlices(u4, &.{ 1, 1 }, bits[0..2]);
            try std.testing.expectEqualSlices(u16, &.{ 0b0, 0b1 }, codes[0..2]);
            try std.testing.expectEqual(incomplete[0] + incomplete[1], n);
            try std.testing.expectEqual(1, lnz);
        }

        try std.testing.fuzz({}, checkFuzzedBuildFreqs, .{});
    }

    fn checkFuzzedBuildFreqs(_: void, smith: *std.testing.Smith) !void {
        @disableInstrumentation();
        var freqs_limit: u16 = 65535;
        var freqs_buf: [max_leafs]u16 = undefined;
        var nfreqs: u15 = 0;

        const incomplete_allowed = smith.value(bool);
        while (nfreqs < @as(u8, @intFromBool(!incomplete_allowed)) + 1 or
            nfreqs != freqs_buf.len and freqs_limit != 0 and
                smith.eosWeightedSimple(15, 1))
        {
            const f = smith.valueWeighted(u16, &.{
                .rangeAtMost(u16, 0, @min(31, freqs_limit), @max(freqs_limit, 1)),
                .rangeAtMost(u16, 0, freqs_limit, 1),
            });
            freqs_buf[nfreqs] = f;
            freqs_limit -= f;
            nfreqs += 1;
        }

        var codes_buf: [max_leafs]u16 = undefined;
        var bits_buf: [max_leafs]u4 = @splat(0);
        const max_bits = smith.valueRangeAtMost(u4, math.log2_int_ceil(u15, nfreqs), 15);
        const total_bits, const last_nonzero = build(
            freqs_buf[0..nfreqs],
            codes_buf[0..nfreqs],
            bits_buf[0..nfreqs],
            max_bits,
            incomplete_allowed,
        );

        var has_bitlen_one: bool = false;
        var expected_total_bits: u32 = 0;
        var expected_last_nonzero: ?u16 = null;
        var weighted_sum: u32 = 0;
        for (freqs_buf[0..nfreqs], bits_buf[0..nfreqs], 0..) |f, nb, i| {
            has_bitlen_one = has_bitlen_one or nb == 1;
            weighted_sum += @shlExact(@as(u16, 1), 15 - nb) & ((1 << 15) - 1);
            expected_total_bits += @as(u32, f) * nb;
            if (nb != 0) expected_last_nonzero = @intCast(i);
        }

        errdefer std.log.err(
            \\ incomplete_allowed: {}
            \\ max_bits: {}
            \\ freqs: {any}
            \\ bits: {any}
            \\ # freqs: {}
            \\ weighted sum: {}
            \\ has_bitlen_one: {}
            \\ expected/actual total bits: {}/{}
            \\ expected/actual last nonzero: {?}/{}
        ++ "\n", .{
            incomplete_allowed,
            max_bits,
            freqs_buf[0..nfreqs],
            bits_buf[0..nfreqs],
            nfreqs,
            weighted_sum,
            has_bitlen_one,
            expected_total_bits,
            total_bits,
            expected_last_nonzero,
            last_nonzero,
        });

        try std.testing.expectEqual(expected_total_bits, total_bits);
        try std.testing.expectEqual(expected_last_nonzero, last_nonzero);
        if (weighted_sum > 1 << 15)
            return error.OversubscribedHuffmanTree;
        if (weighted_sum < 1 << 15 and
            !(incomplete_allowed and has_bitlen_one and weighted_sum == 1 << 14))
            return error.IncompleteHuffmanTree;
    }
};

test {
    _ = huffman;
}

/// [0] is a gradient where the probability of lower values decreases across it
/// [1] is completely random and hence uncompressable
fn testingFreqBufs() !*[2][65536]u8 {
    const fbufs = try std.testing.allocator.create([2][65536]u8);
    var prng: std.Random.DefaultPrng = .init(std.testing.random_seed);
    prng.random().bytes(&fbufs[0]);
    prng.random().bytes(&fbufs[1]);
    for (0.., &fbufs[0], fbufs[1]) |i, *grad, rand| {
        const prob = @as(u8, @intCast(255 - i / (fbufs[0].len * 256)));
        grad.* /= @max(1, rand / @max(1, prob));
    }
    return fbufs;
}
const FreqBufIndex = enum(u1) { gradient, random };

fn testingCheckDecompressedMatches(
    flate_bytes: []const u8,
    expected_size: u32,
    expected_hash: flate.Container.Hasher,
) !void {
    const container: flate.Container = expected_hash;
    var data_hash: flate.Container.Hasher = .init(container);
    var data_size: u32 = 0;
    var flate_r: Io.Reader = .fixed(flate_bytes);
    var deflate_buf: [flate.max_window_len]u8 = undefined;
    var deflate: flate.Decompress = .init(&flate_r, container, &deflate_buf);

    while (deflate.reader.peekGreedy(1)) |bytes| {
        data_size += @intCast(bytes.len);
        data_hash.update(bytes);
        deflate.reader.toss(bytes.len);
    } else |e| switch (e) {
        error.ReadFailed => return deflate.err.?,
        error.EndOfStream => {},
    }

    try testingCheckContainerHash(
        expected_size,
        expected_hash,
        data_hash,
        data_size,
        deflate.container_metadata,
    );
}

fn testingCheckContainerHash(
    expected_size: u32,
    expected_hash: flate.Container.Hasher,
    actual_hash: flate.Container.Hasher,
    actual_size: u32,
    actual_meta: flate.Container.Metadata,
) !void {
    try std.testing.expectEqual(expected_size, actual_size);
    switch (actual_hash) {
        .raw => {},
        .gzip => |gz| {
            const expected_crc = expected_hash.gzip.crc.final();
            try std.testing.expectEqual(expected_size, actual_meta.gzip.count);
            try std.testing.expectEqual(expected_crc, gz.crc.final());
            try std.testing.expectEqual(expected_crc, actual_meta.gzip.crc);
        },
        .zlib => |zl| {
            const expected_adler = expected_hash.zlib.adler;
            try std.testing.expectEqual(expected_adler, zl.adler);
            try std.testing.expectEqual(expected_adler, actual_meta.zlib.adler);
        },
    }
}

const PackedContainer = packed struct(u2) {
    raw: bool,
    other: enum(u1) { gzip, zlib },

    pub fn val(c: @This()) flate.Container {
        return if (c.raw) .raw else switch (c.other) {
            .gzip => .gzip,
            .zlib => .zlib,
        };
    }
};

test Compress {
    const fbufs = try testingFreqBufs();
    defer std.testing.allocator.destroy(fbufs);
    try std.testing.fuzz(fbufs, testFuzzedCompressInput, .{});
}

fn testFuzzedCompressInput(fbufs: *const [2][65536]u8, smith: *std.testing.Smith) !void {
    @disableInstrumentation();
    const container = smith.value(flate.Container);
    const good = smith.valueRangeAtMost(u16, 3, 258);
    const nice = smith.valueRangeAtMost(u16, 3, 258);
    const lazy = smith.valueRangeAtMost(u16, 3, nice);
    const chain = smith.valueWeighted(u16, &.{
        .rangeAtMost(u16, if (good <= lazy) 4 else 1, 255, 65536),
        // The following weights are greatly reduced since they increasing take more time to run
        .rangeAtMost(u16, 256, 4095, 256),
        .rangeAtMost(u16, 4096, 32767 + 256, 1),
    });
    var expected_hash: flate.Container.Hasher = .init(container);
    var expected_size: u32 = 0;

    var flate_buf: [128 * 1024]u8 = undefined;
    var flate_w: Writer = .fixed(&flate_buf);
    var deflate_buf: [flate.max_window_len * 2]u8 = undefined;
    const bufsize = smith.valueRangeAtMost(u32, flate.max_window_len, @intCast(deflate_buf.len));
    var deflate_w = try Compress.init(&flate_w, deflate_buf[0..bufsize], container, .{
        .good = good,
        .nice = nice,
        .lazy = lazy,
        .chain = chain,
    });

    var max_output: usize = 32; // Headers / footer
    while (!smith.eosWeightedSimple(7, 1)) {
        const buffered = deflate_w.writer.buffered();
        // Required for repeating patterns and since writing from `buffered` is illegal
        var copy_buf: [512]u8 = undefined;

        const bytes = bytes: switch (smith.valueRangeAtMost(
            u2,
            @intFromBool(buffered.len == 0),
            3,
        )) {
            0 => { // Copy
                const start = smith.valueRangeLessThan(u32, 0, @intCast(buffered.len));
                // Reuse the implementation's history; otherwise, our own would need maintained.
                const from = buffered[start..];
                const len = smith.valueRangeAtMost(u16, 1, copy_buf.len);

                const history_bytes = from[0..@min(from.len, len)];
                @memcpy(copy_buf[0..history_bytes.len], history_bytes);
                const repeat_len = len - history_bytes.len;
                for (
                    copy_buf[history_bytes.len..][0..repeat_len],
                    copy_buf[0..repeat_len],
                ) |*next, prev| {
                    next.* = prev;
                }
                break :bytes copy_buf[0..len];
            },
            1 => { // Bytes
                const fbuf = &fbufs[
                    smith.valueWeighted(u1, &.{
                        .value(FreqBufIndex, .gradient, 3),
                        .value(FreqBufIndex, .random, 1),
                    })
                ];
                const len = smith.valueRangeAtMost(u32, 1, fbuf.len);
                const off = smith.valueRangeAtMost(u32, 0, @intCast(fbuf.len - len));
                break :bytes fbuf[off..][0..len];
            },
            2 => { // Rebase
                const rebaseable = bufsize - rebase_reserved_capacity;
                const capacity = smith.valueRangeAtMost(u32, 1, rebaseable - rebase_min_preserve);
                const preserve = smith.valueRangeAtMost(u32, 0, rebaseable - capacity);
                const failed = deflate_w.writer.rebase(preserve, capacity);
                if (flate_w.buffered().len > max_output) return error.OverheadTooLarge;
                failed catch return; // Wrote too much data and ran out of space
                continue;
            },
            3 => { // Flush
                max_output += 8; // Alignment data
                const failed = deflate_w.writer.flush();
                if (flate_w.buffered().len > max_output) return error.OverheadTooLarge;
                failed catch return; // Wrote too much data and ran out of space
                continue;
            },
        };

        // An overhead of 64 bytes is given for each block since the implementation does not
        // gaurauntee it writes store blocks when optimal. This comes from taking less than 32
        // bytes to write an optimal dynamic block header of mostly bitlen 8 codes and the end
        // of block literal plus `(65536 / 256) / 8`, which is is the maximum number of extra
        // bytes from bitlen 9 codes.
        max_output += bytes.len + ((bytes.len + flate_buf.len - 1) / block_tokens) * 64;
        const failed = deflate_w.writer.writeAll(bytes);
        if (flate_w.buffered().len > max_output) return error.OverheadTooLarge;
        failed catch return; // Wrote too much data and ran out of space
        expected_hash.update(bytes);
        expected_size += @intCast(bytes.len);
    }

    const failed = deflate_w.finish();
    if (flate_w.buffered().len > max_output) return error.OverheadTooLarge;
    failed catch return; // Wrote too much data and ran out of space
    try testingCheckDecompressedMatches(flate_w.buffered(), expected_size, expected_hash);
}

/// Does not compress data
pub const Raw = struct {
    /// After `finish` is called, all vtable calls with result in `error.WriteFailed`.
    writer: Writer,
    output: *Writer,
    hasher: flate.Container.Hasher,

    const max_block_size: u16 = 65535;
    const full_header: [5]u8 = .{
        BlockHeader.int(.{ .final = false, .kind = .stored }),
        255,
        255,
        0,
        0,
    };

    /// While there is no minimum buffer size, it is recommended
    /// to be at least `flate.max_window_len` for optimal output.
    pub fn init(output: *Writer, buffer: []u8, container: flate.Container) Writer.Error!Raw {
        try output.writeAll(container.header());
        return .{
            .writer = .{
                .buffer = buffer,
                .vtable = &.{
                    .drain = Raw.drain,
                    .flush = Raw.flush,
                    .rebase = Raw.rebase,
                },
            },
            .output = output,
            .hasher = .init(container),
        };
    }

    fn drain(w: *Writer, data: []const []const u8, splat: usize) Writer.Error!usize {
        errdefer w.* = .failing;
        const r: *Raw = @fieldParentPtr("writer", w);
        const min_block = @min(w.buffer.len, max_block_size);
        const pattern = data[data.len - 1];
        var partial_header: [5]u8 = undefined;

        var vecs: [16][]const u8 = undefined;
        var vecs_n: usize = 0;
        const data_bytes = Writer.countSplat(data, splat);
        const total_bytes = w.end + data_bytes;
        var rem_bytes = total_bytes;
        var rem_splat = splat;
        var rem_data = data;
        var rem_data_elem: []const u8 = w.buffered();

        assert(rem_bytes > min_block);
        while (rem_bytes > min_block) { // not >= to allow `min_block` blocks to be marked as final
            // also, it handles the case of `min_block` being zero (no buffer)
            const block_size: u16 = @min(rem_bytes, max_block_size);
            rem_bytes -= block_size;

            if (vecs_n == vecs.len) {
                try r.output.writeVecAll(&vecs);
                vecs_n = 0;
            }
            vecs[vecs_n] = if (block_size == 65535)
                &full_header
            else header: {
                partial_header[0] = BlockHeader.int(.{ .final = false, .kind = .stored });
                mem.writeInt(u16, partial_header[1..3], block_size, .little);
                mem.writeInt(u16, partial_header[3..5], ~block_size, .little);
                break :header &partial_header;
            };
            vecs_n += 1;

            var block_limit: Io.Limit = .limited(block_size);
            while (true) {
                if (vecs_n == vecs.len) {
                    try r.output.writeVecAll(&vecs);
                    vecs_n = 0;
                }

                const vec = block_limit.sliceConst(rem_data_elem);
                vecs[vecs_n] = vec;
                vecs_n += 1;
                r.hasher.update(vec);

                const is_pattern = rem_splat != splat and vec.len == pattern.len;
                if (is_pattern) assert(pattern.len != 0); // exceeded countSplat

                if (!is_pattern or rem_splat == 0 or pattern.len > @intFromEnum(block_limit) / 2) {
                    rem_data_elem = rem_data_elem[vec.len..];
                    block_limit = block_limit.subtract(vec.len).?;

                    if (rem_data_elem.len == 0) {
                        rem_data_elem = rem_data[0];
                        if (rem_data.len != 1) {
                            rem_data = rem_data[1..];
                        } else if (rem_splat != 0) {
                            rem_splat -= 1;
                        } else {
                            // All of `data` has been consumed.
                            assert(block_limit == .nothing);
                            assert(rem_bytes == 0);
                            // Since `rem_bytes` and `block_limit` are zero, these won't be used.
                            rem_data = undefined;
                            rem_data_elem = undefined;
                            rem_splat = undefined;
                        }
                    }
                    if (block_limit == .nothing) break;
                } else {
                    const out_splat = @intFromEnum(block_limit) / pattern.len;
                    assert(out_splat >= 2);

                    try r.output.writeSplatAll(vecs[0..vecs_n], out_splat);
                    for (1..out_splat) |_| r.hasher.update(vec);

                    vecs_n = 0;
                    block_limit = block_limit.subtract(pattern.len * out_splat).?;
                    if (rem_splat >= out_splat) {
                        // `out_splat` contains `rem_data`, however one more needs subtracted
                        // anyways since the next pattern is also being taken.
                        rem_splat -= out_splat;
                    } else {
                        // All of `data` has been consumed.
                        assert(block_limit == .nothing);
                        assert(rem_bytes == 0);
                        // Since `rem_bytes` and `block_limit` are zero, these won't be used.
                        rem_data = undefined;
                        rem_data_elem = undefined;
                        rem_splat = undefined;
                    }
                    if (block_limit == .nothing) break;
                }
            }
        }

        if (vecs_n != 0) { // can be the case if a splat was sent
            try r.output.writeVecAll(vecs[0..vecs_n]);
        }

        if (rem_bytes > data_bytes) {
            assert(rem_bytes - data_bytes == rem_data_elem.len);
            assert(&rem_data_elem[0] == &w.buffer[total_bytes - rem_bytes]);
        }
        return w.consume(total_bytes - rem_bytes);
    }

    fn flush(w: *Writer) Writer.Error!void {
        errdefer w.* = .failing;
        try Raw.rebaseInner(w, 0, w.buffer.len, false);
    }

    fn finish(r: *Raw) Writer.Error!void {
        try Raw.rebaseInner(&r.writer, 0, r.writer.buffer.len, true);
    }

    fn rebase(w: *Writer, preserve: usize, capacity: usize) Writer.Error!void {
        errdefer w.* = .failing;
        try Raw.rebaseInner(w, preserve, capacity, false);
    }

    fn rebaseInner(w: *Writer, preserve: usize, capacity: usize, eos: bool) Writer.Error!void {
        const r: *Raw = @fieldParentPtr("writer", w);
        assert(preserve + capacity <= w.buffer.len);
        if (eos) assert(capacity == w.buffer.len);

        var partial_header: [5]u8 = undefined;
        var footer_buf: [8]u8 = undefined;
        const preserved = @min(w.end, preserve);
        var remaining = w.buffer[0 .. w.end - preserved];

        var vecs: [16][]const u8 = undefined;
        var vecs_n: usize = 0;
        while (remaining.len > max_block_size) { // not >= so there is always a block down below
            if (vecs_n == vecs.len) {
                try r.output.writeVecAll(&vecs);
                vecs_n = 0;
            }
            vecs[vecs_n + 0] = &full_header;
            vecs[vecs_n + 1] = remaining[0..max_block_size];
            r.hasher.update(vecs[vecs_n + 1]);
            vecs_n += 2;
            remaining = remaining[max_block_size..];
        }

        // eos check required for empty block
        if (w.buffer.len - (remaining.len + preserved) < capacity or eos) {
            // A partial write is necessary to reclaim enough buffer space
            const block_size: u16 = @intCast(remaining.len);
            partial_header[0] = BlockHeader.int(.{ .final = eos, .kind = .stored });
            mem.writeInt(u16, partial_header[1..3], block_size, .little);
            mem.writeInt(u16, partial_header[3..5], ~block_size, .little);

            if (vecs_n == vecs.len) {
                try r.output.writeVecAll(&vecs);
                vecs_n = 0;
            }
            vecs[vecs_n + 0] = &partial_header;
            vecs[vecs_n + 1] = remaining[0..block_size];
            r.hasher.update(vecs[vecs_n + 1]);
            vecs_n += 2;
            remaining = remaining[block_size..];
            assert(remaining.len == 0);

            if (eos and r.hasher != .raw) {
                // the footer is done here instead of `flush` so it can be included in the vector
                var footer_w: Writer = .fixed(&footer_buf);
                r.hasher.writeFooter(&footer_w) catch unreachable;
                assert(footer_w.end != 0);

                if (vecs_n == vecs.len) {
                    try r.output.writeVecAll(&vecs);
                    return r.output.writeAll(footer_w.buffered());
                } else {
                    vecs[vecs_n] = footer_w.buffered();
                    vecs_n += 1;
                }
            }
        }

        try r.output.writeVecAll(vecs[0..vecs_n]);
        _ = w.consume(w.end - preserved - remaining.len);
    }
};

test Raw {
    const data_buf = try std.testing.allocator.create([4 * 65536]u8);
    defer std.testing.allocator.destroy(data_buf);
    var prng: std.Random.DefaultPrng = .init(std.testing.random_seed);
    prng.random().bytes(data_buf);
    try std.testing.fuzz(data_buf, testFuzzedRawInput, .{});
}

fn countVec(data: []const []const u8) usize {
    var bytes: usize = 0;
    for (data) |d| bytes += d.len;
    return bytes;
}

fn testFuzzedRawInput(data_buf: *const [4 * 65536]u8, smith: *std.testing.Smith) !void {
    @disableInstrumentation();
    const HashedStoreWriter = struct {
        writer: Writer,
        state: enum {
            header,
            block_header,
            block_body,
            final_block_body,
            footer,
            end,
        },
        block_remaining: u16,
        container: flate.Container,
        data_hash: flate.Container.Hasher,
        data_size: usize,
        footer_hash: u32,
        footer_size: u32,

        pub fn init(buf: []u8, container: flate.Container) @This() {
            return .{
                .writer = .{
                    .vtable = &.{
                        .drain = @This().drain,
                        .flush = @This().flush,
                    },
                    .buffer = buf,
                },
                .state = .header,
                .block_remaining = 0,
                .container = container,
                .data_hash = .init(container),
                .data_size = 0,
                .footer_hash = undefined,
                .footer_size = undefined,
            };
        }

        /// Note that this implementation is somewhat dependent on the implementation of
        /// `Raw` by expecting headers / footers to be continous in data elements. It
        /// also expects the header to be the same as `flate.Container.header` and for
        /// multiple streams to not be concatenated.
        fn drain(w: *Writer, data: []const []const u8, splat: usize) Writer.Error!usize {
            errdefer w.* = .failing;
            var h: *@This() = @fieldParentPtr("writer", w);

            var rem_splat = splat;
            var rem_data = data;
            var rem_data_elem: []const u8 = w.buffered();

            data_loop: while (true) {
                const wanted = switch (h.state) {
                    .header => h.container.headerSize(),
                    .block_header => 5,
                    .block_body, .final_block_body => h.block_remaining,
                    .footer => h.container.footerSize(),
                    .end => 1,
                };

                if (wanted != 0) {
                    while (rem_data_elem.len == 0) {
                        rem_data_elem = rem_data[0];
                        if (rem_data.len != 1) {
                            rem_data = rem_data[1..];
                        } else {
                            if (rem_splat == 0) {
                                break :data_loop;
                            } else {
                                rem_splat -= 1;
                            }
                        }
                    }
                }

                const bytes = Io.Limit.limited(wanted).sliceConst(rem_data_elem);
                rem_data_elem = rem_data_elem[bytes.len..];

                switch (h.state) {
                    .header => {
                        if (bytes.len < wanted)
                            return error.WriteFailed; // header eos
                        if (!mem.eql(u8, bytes, h.container.header()))
                            return error.WriteFailed; // wrong header
                        h.state = .block_header;
                    },
                    .block_header => {
                        if (bytes.len < wanted)
                            return error.WriteFailed; // store block header eos
                        const header: BlockHeader = @bitCast(@as(u3, @truncate(bytes[0])));
                        if (header.kind != .stored)
                            return error.WriteFailed; // non-store block
                        const len = mem.readInt(u16, bytes[1..3], .little);
                        const nlen = mem.readInt(u16, bytes[3..5], .little);
                        if (nlen != ~len)
                            return error.WriteFailed; // wrong nlen
                        h.block_remaining = len;
                        h.state = if (!header.final) .block_body else .final_block_body;
                    },
                    .block_body, .final_block_body => {
                        h.data_hash.update(bytes);
                        h.data_size += bytes.len;
                        h.block_remaining -= @intCast(bytes.len);
                        if (h.block_remaining == 0) {
                            h.state = if (h.state != .final_block_body) .block_header else .footer;
                        }
                    },
                    .footer => {
                        if (bytes.len < wanted)
                            return error.WriteFailed; // footer eos
                        switch (h.container) {
                            .raw => {},
                            .gzip => {
                                h.footer_hash = mem.readInt(u32, bytes[0..4], .little);
                                h.footer_size = mem.readInt(u32, bytes[4..8], .little);
                            },
                            .zlib => {
                                h.footer_hash = mem.readInt(u32, bytes[0..4], .big);
                            },
                        }
                        h.state = .end;
                    },
                    .end => return error.WriteFailed, // data past end
                }
            }

            w.end = 0;
            return Writer.countSplat(data, splat);
        }

        fn flush(w: *Writer) Writer.Error!void {
            defer w.* = .failing; // Empties buffer even if state hasn't reached `end`
            _ = try @This().drain(w, &.{""}, 0);
        }
    };

    const container = smith.value(flate.Container);
    var output: HashedStoreWriter = .init(&.{}, container);
    var expected_hash: flate.Container.Hasher = .init(container);
    var expected_size: u32 = 0;
    // 10 maximum blocks is the choosen limit since it is two more
    // than the maximum the implementation can output in one drain.
    const max_size = 10 * @as(u32, Raw.max_block_size);

    var raw_buf: [2 * @as(usize, Raw.max_block_size)]u8 = undefined;
    const raw_buf_len = smith.valueWeighted(u32, &.{
        .value(u32, 0, @intCast(raw_buf.len)), // unbuffered
        .rangeAtMost(u32, 0, @intCast(raw_buf.len), 1),
    });
    var raw: Raw = try .init(&output.writer, raw_buf[0..raw_buf_len], container);

    const data_buf_len: u32 = @intCast(data_buf.len);
    var vecs: [32][]const u8 = undefined;
    var vecs_n: usize = 0;

    while (true) {
        const Op = packed struct {
            drain: bool = false,
            add_vec: bool = false,
            rebase: enum(u2) { none, rebase, flush } = .none,

            pub const drain_only: @This() = .{ .drain = true };
            pub const add_vec_only: @This() = .{ .add_vec = true };
            pub const add_vec_and_drain: @This() = .{ .add_vec = true, .drain = true };
            pub const drain_and_rebase: @This() = .{ .drain = true, .rebase = .rebase };
            pub const drain_and_flush: @This() = .{ .drain = true, .rebase = .flush };
        };

        const is_eos = expected_size == max_size or smith.eosWeightedSimple(7, 1);
        var op: Op = if (!is_eos) smith.valueWeighted(Op, &.{
            .value(Op, .add_vec_only, 5),
            .value(Op, .add_vec_and_drain, 1),
            .value(Op, .drain_and_rebase, 1),
            .value(Op, .drain_and_flush, 1),
        }) else .drain_only;

        if (op.add_vec) {
            const max_write = max_size - expected_size;
            const buffered: u32 = @intCast(raw.writer.buffered().len + countVec(vecs[0..vecs_n]));
            const to_align = Raw.max_block_size - buffered % Raw.max_block_size;
            assert(to_align != 0); // otherwise, not helpful.

            const max_data = @min(data_buf_len, max_write);
            const len = smith.valueWeighted(u32, &.{
                .rangeAtMost(u32, 0, max_data, 1),
                .rangeAtMost(u32, 0, @min(Raw.max_block_size, max_data), 4),
                .value(u32, @min(to_align, max_data), max_data), // @min 2nd arg is an edge-case
            });
            const off = smith.valueRangeAtMost(u32, 0, data_buf_len - len);

            expected_size += len;
            vecs[vecs_n] = data_buf[off..][0..len];
            vecs_n += 1;
            op.drain |= vecs_n == vecs.len;
        }

        op.drain |= is_eos;
        op.drain &= vecs_n != 0;
        if (op.drain) {
            const pattern_len: u32 = @intCast(vecs[vecs_n - 1].len);
            const pattern_len_z = @max(pattern_len, 1);

            const max_write = max_size - (expected_size - pattern_len);
            const buffered: u32 = @intCast(raw.writer.buffered().len + countVec(vecs[0 .. vecs_n - 1]));
            const to_align = Raw.max_block_size - buffered % Raw.max_block_size;
            assert(to_align != 0); // otherwise, not helpful.

            const max_splat = max_write / pattern_len_z;
            const weights: [3]std.testing.Smith.Weight = .{
                .rangeAtMost(u32, 0, max_splat, 1),
                .rangeAtMost(u32, 0, @min(
                    Raw.max_block_size + pattern_len_z,
                    max_write,
                ) / pattern_len_z, 4),
                .value(u32, to_align / pattern_len_z, max_splat * 4),
            };
            const align_weight = to_align % pattern_len_z == 0 and to_align <= max_write;
            const n_weights = @as(u8, 2) + @intFromBool(align_weight);
            const splat = smith.valueWeighted(u32, weights[0..n_weights]);

            expected_size = expected_size - pattern_len + pattern_len * splat; // splat may be zero
            for (vecs[0 .. vecs_n - 1]) |v| expected_hash.update(v);
            for (0..splat) |_| expected_hash.update(vecs[vecs_n - 1]);
            try raw.writer.writeSplatAll(vecs[0..vecs_n], splat);
            vecs_n = 0;
        }

        switch (op.rebase) {
            .none => {},
            .rebase => {
                const capacity = smith.valueRangeAtMost(u32, 0, raw_buf_len);
                const preserve = smith.valueRangeAtMost(u32, 0, raw_buf_len - capacity);
                try raw.writer.rebase(preserve, capacity);
            },
            .flush => try raw.writer.flush(),
        }

        if (is_eos) break;
    }

    try raw.finish();
    try output.writer.flush();

    try std.testing.expectEqual(.end, output.state);
    try std.testing.expectEqual(expected_size, output.data_size);
    switch (output.data_hash) {
        .raw => {},
        .gzip => |gz| {
            const expected_crc = expected_hash.gzip.crc.final();
            try std.testing.expectEqual(expected_crc, gz.crc.final());
            try std.testing.expectEqual(expected_crc, output.footer_hash);
            try std.testing.expectEqual(expected_size, output.footer_size);
        },
        .zlib => |zl| {
            const expected_adler = expected_hash.zlib.adler;
            try std.testing.expectEqual(expected_adler, zl.adler);
            try std.testing.expectEqual(expected_adler, output.footer_hash);
        },
    }
}

/// Only performs huffman compression on data, does no matching.
pub const Huffman = struct {
    /// After `finish` is called, all vtable calls with result in `error.WriteFailed`.
    writer: Writer,
    bit_writer: BitWriter,
    hasher: flate.Container.Hasher,

    const max_tokens: u16 = 65535 - 1; // one is reserved for EOF

    /// While there is no minimum buffer size, it is recommended
    /// to be at least `flate.max_window_len` to improve compression.
    ///
    /// It is asserted `output` has a capacity of at least 8 bytes.
    pub fn init(output: *Writer, buffer: []u8, container: flate.Container) Writer.Error!Huffman {
        assert(output.buffer.len > 8);

        try output.writeAll(container.header());
        return .{
            .writer = .{
                .buffer = buffer,
                .vtable = &.{
                    .drain = Huffman.drain,
                    .flush = Huffman.flush,
                    .rebase = Huffman.rebase,
                },
            },
            .bit_writer = .init(output),
            .hasher = .init(container),
        };
    }

    fn drain(w: *Writer, data: []const []const u8, splat: usize) Writer.Error!usize {
        const h: *Huffman = @fieldParentPtr("writer", w);
        const min_block = @min(w.buffer.len, max_tokens);
        const pattern = data[data.len - 1];

        const data_bytes = Writer.countSplat(data, splat);
        const total_bytes = w.end + data_bytes;
        var rem_bytes = total_bytes;
        var rem_splat = splat;
        var rem_data = data;
        var rem_data_elem: []const u8 = w.buffered();

        assert(rem_bytes > min_block);
        while (rem_bytes > min_block) { // not >= to allow `min_block` blocks to be marked as final
            // also, it handles the case of `min_block` being zero (no buffer)
            const block_size: u16 = @min(rem_bytes, max_tokens);
            rem_bytes -= block_size;

            // Count frequencies
            comptime assert(max_tokens != 65535);
            var freqs: [257]u16 = @splat(0);
            freqs[256] = 1;

            const start_splat = rem_splat;
            const start_data = rem_data;
            const start_data_elem = rem_data_elem;

            var block_limit: Io.Limit = .limited(block_size);
            while (true) {
                const bytes = block_limit.sliceConst(rem_data_elem);
                const is_pattern = rem_splat != splat and bytes.len == pattern.len;

                const mul = if (!is_pattern) 1 else @intFromEnum(block_limit) / pattern.len;
                assert(mul != 0);
                if (is_pattern) assert(mul <= rem_splat + 1); // one more for `rem_data`

                for (bytes) |b| freqs[b] += @intCast(mul);
                rem_data_elem = rem_data_elem[bytes.len..];
                block_limit = block_limit.subtract(bytes.len * mul).?;

                if (rem_data_elem.len == 0) {
                    rem_data_elem = rem_data[0];
                    if (rem_data.len != 1) {
                        rem_data = rem_data[1..];
                    } else if (rem_splat >= mul) {
                        // if the counter was not the pattern, `mul` is always one, otherwise,
                     
```
