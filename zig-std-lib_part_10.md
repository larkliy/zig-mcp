```
le_compressed: i64,
    ledger_tag_network_volatile: i64,
    ledger_tag_network_volatile_compressed: i64,
    ledger_tag_media_footprint: i64,
    ledger_tag_media_footprint_compressed: i64,
    ledger_tag_media_nofootprint: i64,
    ledger_tag_media_nofootprint_compressed: i64,
    ledger_tag_graphics_footprint: i64,
    ledger_tag_graphics_footprint_compressed: i64,
    ledger_tag_graphics_nofootprint: i64,
    ledger_tag_graphics_nofootprint_compressed: i64,
    ledger_tag_neural_footprint: i64,
    ledger_tag_neural_footprint_compressed: i64,
    ledger_tag_neural_nofootprint: i64,
    ledger_tag_neural_nofootprint_compressed: i64,

    // added for rev4
    limit_bytes_remaining: u64,

    // added for rev5
    decompressions: integer_t,
};

pub const task_vm_info_data_t = task_vm_info;

pub const vm_prot_t = std.macho.vm_prot_t;
pub const boolean_t = c_int;

pub extern "c" fn mach_vm_protect(
    target_task: vm_map_t,
    address: mach_vm_address_t,
    size: mach_vm_size_t,
    set_maximum: boolean_t,
    new_protection: vm_prot_t,
) kern_return_t;

pub extern "c" fn mach_port_allocate(
    task: ipc_space_t,
    right: mach_port_right_t,
    name: *mach_port_name_t,
) kern_return_t;
pub extern "c" fn mach_port_deallocate(task: ipc_space_t, name: mach_port_name_t) kern_return_t;
pub extern "c" fn mach_port_insert_right(
    task: ipc_space_t,
    name: mach_port_name_t,
    right: mach_port_t,
    right_type: mach_msg_type_name_t,
) kern_return_t;

pub extern "c" fn task_info(
    target_task: task_name_t,
    flavor: task_flavor_t,
    task_info_out: task_info_t,
    task_info_outCnt: *mach_msg_type_number_t,
) kern_return_t;

pub const mach_task_basic_info = extern struct {
    /// Virtual memory size (bytes)
    virtual_size: mach_vm_size_t,
    /// Resident memory size (bytes)
    resident_size: mach_vm_size_t,
    /// Total user run time for terminated threads
    user_time: time_value_t,
    /// Total system run time for terminated threads
    system_time: time_value_t,
    /// Default policy for new threads
    policy: policy_t,
    /// Suspend count for task
    suspend_count: mach_vm_size_t,
};

pub extern "c" fn _host_page_size(task: mach_port_t, size: *vm_size_t) kern_return_t;
pub extern "c" fn vm_deallocate(target_task: vm_map_t, address: vm_address_t, size: vm_size_t) kern_return_t;
pub extern "c" fn vm_machine_attribute(
    target_task: vm_map_t,
    address: vm_address_t,
    size: vm_size_t,
    attribute: vm_machine_attribute_t,
    value: *vm_machine_attribute_val_t,
) kern_return_t;

pub extern "c" fn sendfile(
    in_fd: fd_t,
    out_fd: fd_t,
    offset: off_t,
    len: *off_t,
    sf_hdtr: ?*sf_hdtr,
    flags: u32,
) c_int;

// https://github.com/apple/darwin-xnu/blob/2ff845c2e033bd0ff64b5b6aa6063a1f8f65aa32/bsd/sys/_types.h#L74
pub const sigset_t = u32;

// https://github.com/apple/darwin-xnu/blob/2ff845c2e033bd0ff64b5b6aa6063a1f8f65aa32/bsd/sys/signal.h#L76
pub const NSIG = 32;

pub const qos_class_t = enum(c_uint) {
    /// highest priority QOS class for critical tasks
    USER_INTERACTIVE = 0x21,
    /// slightly more moderate priority QOS class
    USER_INITIATED = 0x19,
    /// default QOS class when none is set
    DEFAULT = 0x15,
    /// more energy efficient QOS class than default
    UTILITY = 0x11,
    /// QOS class more appropriate for background tasks
    BACKGROUND = 0x09,
    /// QOS class as a return value
    UNSPECIFIED = 0x00,

    _,
};

/// Undocumented futex-like API available on darwin 16+
/// (macOS 10.12+, iOS 10.0+, tvOS 10.0+, watchOS 3.0+, catalyst 13.0+).
///
/// [ulock.h]: https://github.com/apple/darwin-xnu/blob/master/bsd/sys/ulock.h
/// [sys_ulock.c]: https://github.com/apple/darwin-xnu/blob/master/bsd/kern/sys_ulock.c
pub const UL = packed struct(u32) {
    op: Op,
    WAKE_ALL: bool = false,
    WAKE_THREAD: bool = false,
    _10: u6 = 0,
    WAIT_WORKQ_DATA_CONTENTION: bool = false,
    WAIT_CANCEL_POINT: bool = false,
    WAIT_ADAPTIVE_SPIN: bool = false,
    _19: u5 = 0,
    NO_ERRNO: bool = false,
    _: u7 = 0,

    pub const Op = enum(u8) {
        COMPARE_AND_WAIT = 1,
        UNFAIR_LOCK = 2,
        COMPARE_AND_WAIT_SHARED = 3,
        UNFAIR_LOCK64_SHARED = 4,
        COMPARE_AND_WAIT64 = 5,
        COMPARE_AND_WAIT64_SHARED = 6,
        _,
    };
};

pub extern "c" fn __ulock_wait2(op: UL, addr: ?*const anyopaque, val: u64, timeout_ns: u64, val2: u64) c_int;
pub extern "c" fn __ulock_wait(op: UL, addr: ?*const anyopaque, val: u64, timeout_us: u32) c_int;
pub extern "c" fn __ulock_wake(op: UL, addr: ?*const anyopaque, val: u64) c_int;

pub const os_unfair_lock_t = *os_unfair_lock;
pub const os_unfair_lock = extern struct {
    _os_unfair_lock_opaque: u32 = 0,
};

pub extern "c" fn os_unfair_lock_lock(o: os_unfair_lock_t) void;
pub extern "c" fn os_unfair_lock_unlock(o: os_unfair_lock_t) void;
pub extern "c" fn os_unfair_lock_trylock(o: os_unfair_lock_t) bool;
pub extern "c" fn os_unfair_lock_assert_owner(o: os_unfair_lock_t) void;
pub extern "c" fn os_unfair_lock_assert_not_owner(o: os_unfair_lock_t) void;

pub const os_signpost_id_t = enum(u64) {
    NULL = 0,
    INVALID = !0,
    EXCLUSIVE = 0xeeeeb0b5b2b2eeee,
    _,
};

pub const os_log_t = *opaque {};
pub const os_log_type_t = enum(u8) {
    /// default messages always captures
    DEFAULT = 0x00,
    /// messages with additional infos
    INFO = 0x01,
    /// debug messages
    DEBUG = 0x02,
    /// error messages
    ERROR = 0x10,
    /// unexpected conditions messages
    FAULT = 0x11,

    _,
};

pub extern "c" fn os_log_create(subsystem: [*]const u8, category: [*]const u8) os_log_t;
pub extern "c" fn os_log_type_enabled(log: os_log_t, tpe: os_log_type_t) bool;
pub extern "c" fn os_signpost_id_generate(log: os_log_t) os_signpost_id_t;
pub extern "c" fn os_signpost_interval_begin(log: os_log_t, signpos: os_signpost_id_t, func: [*]const u8, ...) void;
pub extern "c" fn os_signpost_interval_end(log: os_log_t, signpos: os_signpost_id_t, func: [*]const u8, ...) void;
pub extern "c" fn os_signpost_id_make_with_pointer(log: os_log_t, ptr: ?*anyopaque) os_signpost_id_t;
pub extern "c" fn os_signpost_enabled(log: os_log_t) bool;

pub extern "c" fn pthread_setname_np(name: [*:0]const u8) c_int;
pub extern "c" fn pthread_attr_set_qos_class_np(attr: *pthread_attr_t, qos_class: qos_class_t, relative_priority: c_int) c_int;
pub extern "c" fn pthread_attr_get_qos_class_np(attr: *pthread_attr_t, qos_class: *qos_class_t, relative_priority: *c_int) c_int;
pub extern "c" fn pthread_set_qos_class_self_np(qos_class: qos_class_t, relative_priority: c_int) c_int;
pub extern "c" fn pthread_get_qos_class_np(pthread: std.c.pthread_t, qos_class: *qos_class_t, relative_priority: *c_int) c_int;

pub const mach_timebase_info_data = extern struct {
    numer: u32,
    denom: u32,
};

pub const kevent64_s = extern struct {
    ident: u64,
    filter: i16,
    flags: u16,
    fflags: u32,
    data: i64,
    udata: u64,
    ext: [2]u64,
};

// sys/types.h on macos uses #pragma pack() so these checks are
// to make sure the struct is laid out the same. These values were
// produced from C code using the offsetof macro.
comptime {
    if (builtin.target.os.tag.isDarwin()) {
        assert(@offsetOf(kevent64_s, "ident") == 0);
        assert(@offsetOf(kevent64_s, "filter") == 8);
        assert(@offsetOf(kevent64_s, "flags") == 10);
        assert(@offsetOf(kevent64_s, "fflags") == 12);
        assert(@offsetOf(kevent64_s, "data") == 16);
        assert(@offsetOf(kevent64_s, "udata") == 24);
        assert(@offsetOf(kevent64_s, "ext") == 32);
    }
}

pub const clock_serv_t = mach_port_t;
pub const clock_res_t = c_int;
pub const mach_port_name_t = natural_t;
pub const natural_t = c_uint;
pub const mach_timespec_t = extern struct {
    sec: c_uint,
    nsec: clock_res_t,
};
pub const kern_return_t = c_int;
pub const host_t = mach_port_t;
pub const integer_t = c_int;
pub const task_flavor_t = natural_t;
pub const task_info_t = *integer_t;
pub const task_name_t = mach_port_name_t;
pub const vm_address_t = vm_offset_t;
pub const vm_size_t = mach_vm_size_t;
pub const vm_machine_attribute_t = usize;
pub const vm_machine_attribute_val_t = isize;

pub const CALENDAR_CLOCK = 1;

pub const SYSPROTO_EVENT = 1;
pub const SYSPROTO_CONTROL = 2;

/// Mach msg return values
pub const mach_msg_return_t = enum(kern_return_t) {
    SUCCESS = 0x00000000,

    /// Thread is waiting to send.  (Internal use only.)
    SEND_IN_PROGRESS = 0x10000001,
    /// Bogus in-line data.
    SEND_INVALID_DATA = 0x10000002,
    /// Bogus destination port.
    SEND_INVALID_DEST = 0x10000003,
    ///  Message not sent before timeout expired.
    SEND_TIMED_OUT = 0x10000004,
    ///  Bogus voucher port.
    SEND_INVALID_VOUCHER = 0x10000005,
    ///  Software interrupt.
    SEND_INTERRUPTED = 0x10000007,
    ///  Data doesn't contain a complete message.
    SEND_MSG_TOO_SMALL = 0x10000008,
    ///  Bogus reply port.
    SEND_INVALID_REPLY = 0x10000009,
    ///  Bogus port rights in the message body.
    SEND_INVALID_RIGHT = 0x1000000a,
    ///  Bogus notify port argument.
    SEND_INVALID_NOTIFY = 0x1000000b,
    ///  Invalid out-of-line memory pointer.
    SEND_INVALID_MEMORY = 0x1000000c,
    ///  No message buffer is available.
    SEND_NO_BUFFER = 0x1000000d,
    ///  Send is too large for port
    SEND_TOO_LARGE = 0x1000000e,
    ///  Invalid msg-type specification.
    SEND_INVALID_TYPE = 0x1000000f,
    ///  A field in the header had a bad value.
    SEND_INVALID_HEADER = 0x10000010,
    ///  The trailer to be sent does not match kernel format.
    SEND_INVALID_TRAILER = 0x10000011,
    ///  The sending thread context did not match the context on the dest port
    SEND_INVALID_CONTEXT = 0x10000012,
    ///  compatibility: no longer a returned error
    SEND_INVALID_RT_OOL_SIZE = 0x10000015,
    ///  The destination port doesn't accept ports in body
    SEND_NO_GRANT_DEST = 0x10000016,
    ///  Message send was rejected by message filter
    SEND_MSG_FILTERED = 0x10000017,

    ///  Thread is waiting for receive.  (Internal use only.)
    RCV_IN_PROGRESS = 0x10004001,
    ///  Bogus name for receive port/port-set.
    RCV_INVALID_NAME = 0x10004002,
    ///  Didn't get a message within the timeout value.
    RCV_TIMED_OUT = 0x10004003,
    ///  Message buffer is not large enough for inline data.
    RCV_TOO_LARGE = 0x10004004,
    ///  Software interrupt.
    RCV_INTERRUPTED = 0x10004005,
    ///  compatibility: no longer a returned error
    RCV_PORT_CHANGED = 0x10004006,
    ///  Bogus notify port argument.
    RCV_INVALID_NOTIFY = 0x10004007,
    ///  Bogus message buffer for inline data.
    RCV_INVALID_DATA = 0x10004008,
    ///  Port/set was sent away/died during receive.
    RCV_PORT_DIED = 0x10004009,
    ///  compatibility: no longer a returned error
    RCV_IN_SET = 0x1000400a,
    ///  Error receiving message header.  See special bits (use `extractResourceError`).
    RCV_HEADER_ERROR = 0x1000400b,
    ///  Error receiving message body.  See special bits (use `extractResourceError`).
    RCV_BODY_ERROR = 0x1000400c,
    ///  Invalid msg-type specification in scatter list.
    RCV_INVALID_TYPE = 0x1000400d,
    ///  Out-of-line overwrite region is not large enough
    RCV_SCATTER_SMALL = 0x1000400e,
    ///  trailer type or number of trailer elements not supported
    RCV_INVALID_TRAILER = 0x1000400f,
    ///  Waiting for receive with timeout. (Internal use only.)
    RCV_IN_PROGRESS_TIMED = 0x10004011,
    ///  invalid reply port used in a STRICT_REPLY message
    RCV_INVALID_REPLY = 0x10004012,

    _,

    pub fn extractResourceError(ret: mach_msg_return_t) struct {
        error_code: mach_msg_return_t,
        resource_error: ?MACH.MSG,
    } {
        const return_code: mach_msg_return_t = @enumFromInt(@intFromEnum(ret) & ~MACH.MSG.MASK);
        switch (return_code) {
            .RCV_HEADER_ERROR, .RCV_BODY_ERROR => {
                const resource_error: MACH.MSG = @bitCast(@intFromEnum(ret) & MACH.MSG.MASK);
                return .{
                    .error_code = return_code,
                    .resource_error = resource_error,
                };
            },
            else => return .{
                .error_code = ret,
                .resource_error = null,
            },
        }
    }
};

pub const FCNTL_FS_SPECIFIC_BASE = 0x00010000;

/// Max open files per process
/// https://opensource.apple.com/source/xnu/xnu-4903.221.2/bsd/sys/syslimits.h.auto.html
pub const OPEN_MAX = 10240;

// CPU families mapping
pub const CPUFAMILY = enum(u32) {
    UNKNOWN = 0,
    POWERPC_G3 = 0xcee41549,
    POWERPC_G4 = 0x77c184ae,
    POWERPC_G5 = 0xed76d8aa,
    INTEL_6_13 = 0xaa33392b,
    INTEL_PENRYN = 0x78ea4fbc,
    INTEL_NEHALEM = 0x6b5a4cd2,
    INTEL_WESTMERE = 0x573b5eec,
    INTEL_SANDYBRIDGE = 0x5490b78c,
    INTEL_IVYBRIDGE = 0x1f65e835,
    INTEL_HASWELL = 0x10b282dc,
    INTEL_BROADWELL = 0x582ed09c,
    INTEL_SKYLAKE = 0x37fc219f,
    INTEL_KABYLAKE = 0x0f817246,
    ARM_9 = 0xe73283ae,
    ARM_11 = 0x8ff620d8,
    ARM_XSCALE = 0x53b005f5,
    ARM_12 = 0xbd1b0ae9,
    ARM_13 = 0x0cc90e64,
    ARM_14 = 0x96077ef1,
    ARM_15 = 0xa8511bca,
    ARM_SWIFT = 0x1e2d6381,
    ARM_CYCLONE = 0x37a09642,
    ARM_TYPHOON = 0x2c91a47e,
    ARM_TWISTER = 0x92fb37c8,
    ARM_HURRICANE = 0x67ceee93,
    ARM_MONSOON_MISTRAL = 0xe81e7ef6,
    ARM_VORTEX_TEMPEST = 0x07d34b9f,
    ARM_LIGHTNING_THUNDER = 0x462504d2,
    ARM_FIRESTORM_ICESTORM = 0x1b588bb3,
    ARM_BLIZZARD_AVALANCHE = 0xda33d83d,
    ARM_EVEREST_SAWTOOTH = 0x8765edea,
    ARM_COLL = 0x2876f5b5,
    ARM_IBIZA = 0xfa33415e,
    ARM_LOBOS = 0x5f4dea93,
    ARM_PALMA = 0x72015832,
    ARM_DONAN = 0x6f5129ac,
    ARM_BRAVA = 0x17d5b93a,
    ARM_TAHITI = 0x75d4acb9,
    ARM_TUPAI = 0x204526d0,
    _,
};

pub const PT = enum(c_int) {
    TRACE_ME = 0,
    READ_I = 1,
    READ_D = 2,
    READ_U = 3,
    WRITE_I = 4,
    WRITE_D = 5,
    WRITE_U = 6,
    CONTINUE = 7,
    KILL = 8,
    STEP = 9,
    DETACH = 11,
    SIGEXC = 12,
    THUPDATE = 13,
    ATTACHEXC = 14,
    FORCEQUOTA = 30,
    DENY_ATTACH = 31,
    _,
};

pub extern "c" fn ptrace(request: PT, pid: pid_t, addr: caddr_t, data: c_int) c_int;

pub const POSIX_SPAWN = packed struct(c_short) {
    RESETIDS: bool = false,
    SETPGROUP: bool = false,
    SETSIGDEF: bool = false,
    SETSIGMASK: bool = false,
    _4: u2 = 0,
    SETEXEC: bool = false,
    START_SUSPENDED: bool = false,
    DISABLE_ASLR: bool = false,
    _9: u1 = 0,
    SETSID: bool = false,
    RESLIDE: bool = false,
    _12: u2 = 0,
    CLOEXEC_DEFAULT: bool = false,
    _15: u1 = 0,
};

pub const posix_spawnattr_t = *opaque {};
pub const posix_spawn_file_actions_t = *opaque {};
pub extern "c" fn posix_spawnattr_init(attr: *posix_spawnattr_t) c_int;
pub extern "c" fn posix_spawnattr_destroy(attr: *posix_spawnattr_t) c_int;
pub extern "c" fn posix_spawnattr_setflags(attr: *posix_spawnattr_t, flags: POSIX_SPAWN) c_int;
pub extern "c" fn posix_spawnattr_getflags(attr: *const posix_spawnattr_t, flags: *POSIX_SPAWN) c_int;
pub extern "c" fn posix_spawn_file_actions_init(actions: *posix_spawn_file_actions_t) c_int;
pub extern "c" fn posix_spawn_file_actions_destroy(actions: *posix_spawn_file_actions_t) c_int;
pub extern "c" fn posix_spawn_file_actions_addclose(actions: *posix_spawn_file_actions_t, filedes: fd_t) c_int;
pub extern "c" fn posix_spawn_file_actions_addopen(
    actions: *posix_spawn_file_actions_t,
    filedes: fd_t,
    path: [*:0]const u8,
    oflag: c_int,
    mode: mode_t,
) c_int;
pub extern "c" fn posix_spawn_file_actions_adddup2(
    actions: *posix_spawn_file_actions_t,
    filedes: fd_t,
    newfiledes: fd_t,
) c_int;
pub extern "c" fn posix_spawn_file_actions_addinherit_np(actions: *posix_spawn_file_actions_t, filedes: fd_t) c_int;
pub extern "c" fn posix_spawn_file_actions_addchdir_np(actions: *posix_spawn_file_actions_t, path: [*:0]const u8) c_int;
pub extern "c" fn posix_spawn_file_actions_addfchdir_np(actions: *posix_spawn_file_actions_t, filedes: fd_t) c_int;
pub extern "c" fn posix_spawn(
    pid: *pid_t,
    path: [*:0]const u8,
    actions: ?*const posix_spawn_file_actions_t,
    attr: ?*const posix_spawnattr_t,
    argv: [*:null]const ?[*:0]const u8,
    env: [*:null]const ?[*:0]const u8,
) c_int;
pub extern "c" fn posix_spawnp(
    pid: *pid_t,
    path: [*:0]const u8,
    actions: ?*const posix_spawn_file_actions_t,
    attr: ?*const posix_spawnattr_t,
    argv: [*:null]const ?[*:0]const u8,
    env: [*:null]const ?[*:0]const u8,
) c_int;

pub const E = enum(u16) {
    /// No error occurred.
    SUCCESS = 0,
    /// Operation not permitted
    PERM = 1,
    /// No such file or directory
    NOENT = 2,
    /// No such process
    SRCH = 3,
    /// Interrupted system call
    INTR = 4,
    /// Input/output error
    IO = 5,
    /// Device not configured
    NXIO = 6,
    /// Argument list too long
    @"2BIG" = 7,
    /// Exec format error
    NOEXEC = 8,
    /// Bad file descriptor
    BADF = 9,
    /// No child processes
    CHILD = 10,
    /// Resource deadlock avoided
    DEADLK = 11,
    /// Cannot allocate memory
    NOMEM = 12,
    /// Permission denied
    ACCES = 13,
    /// Bad address
    FAULT = 14,
    /// Block device required
    NOTBLK = 15,
    /// Device / Resource busy
    BUSY = 16,
    /// File exists
    EXIST = 17,
    /// Cross-device link
    XDEV = 18,
    /// Operation not supported by device
    NODEV = 19,
    /// Not a directory
    NOTDIR = 20,
    /// Is a directory
    ISDIR = 21,
    /// Invalid argument
    INVAL = 22,
    /// Too many open files in system
    NFILE = 23,
    /// Too many open files
    MFILE = 24,
    /// Inappropriate ioctl for device
    NOTTY = 25,
    /// Text file busy
    TXTBSY = 26,
    /// File too large
    FBIG = 27,
    /// No space left on device
    NOSPC = 28,
    /// Illegal seek
    SPIPE = 29,
    /// Read-only file system
    ROFS = 30,
    /// Too many links
    MLINK = 31,
    /// Broken pipe
    PIPE = 32,
    // math software
    /// Numerical argument out of domain
    DOM = 33,
    /// Result too large
    RANGE = 34,
    // non-blocking and interrupt i/o
    /// Resource temporarily unavailable
    /// This is the same code used for `WOULDBLOCK`.
    AGAIN = 35,
    /// Operation now in progress
    INPROGRESS = 36,
    /// Operation already in progress
    ALREADY = 37,
    // ipc/network software -- argument errors
    /// Socket operation on non-socket
    NOTSOCK = 38,
    /// Destination address required
    DESTADDRREQ = 39,
    /// Message too long
    MSGSIZE = 40,
    /// Protocol wrong type for socket
    PROTOTYPE = 41,
    /// Protocol not available
    NOPROTOOPT = 42,
    /// Protocol not supported
    PROTONOSUPPORT = 43,
    /// Socket type not supported
    SOCKTNOSUPPORT = 44,
    /// Operation not supported
    /// The same code is used for `NOTSUP`.
    OPNOTSUPP = 45,
    /// Protocol family not supported
    PFNOSUPPORT = 46,
    /// Address family not supported by protocol family
    AFNOSUPPORT = 47,
    /// Address already in use
    ADDRINUSE = 48,
    /// Can't assign requested address
    // ipc/network software -- operational errors
    ADDRNOTAVAIL = 49,
    /// Network is down
    NETDOWN = 50,
    /// Network is unreachable
    NETUNREACH = 51,
    /// Network dropped connection on reset
    NETRESET = 52,
    /// Software caused connection abort
    CONNABORTED = 53,
    /// Connection reset by peer
    CONNRESET = 54,
    /// No buffer space available
    NOBUFS = 55,
    /// Socket is already connected
    ISCONN = 56,
    /// Socket is not connected
    NOTCONN = 57,
    /// Can't send after socket shutdown
    SHUTDOWN = 58,
    /// Too many references: can't splice
    TOOMANYREFS = 59,
    /// Operation timed out
    TIMEDOUT = 60,
    /// Connection refused
    CONNREFUSED = 61,
    /// Too many levels of symbolic links
    LOOP = 62,
    /// File name too long
    NAMETOOLONG = 63,
    /// Host is down
    HOSTDOWN = 64,
    /// No route to host
    HOSTUNREACH = 65,
    /// Directory not empty
    // quotas & mush
    NOTEMPTY = 66,
    /// Too many processes
    PROCLIM = 67,
    /// Too many users
    USERS = 68,
    /// Disc quota exceeded
    // Network File System
    DQUOT = 69,
    /// Stale NFS file handle
    STALE = 70,
    /// Too many levels of remote in path
    REMOTE = 71,
    /// RPC struct is bad
    BADRPC = 72,
    /// RPC version wrong
    RPCMISMATCH = 73,
    /// RPC prog. not avail
    PROGUNAVAIL = 74,
    /// Program version wrong
    PROGMISMATCH = 75,
    /// Bad procedure for program
    PROCUNAVAIL = 76,
    /// No locks available
    NOLCK = 77,
    /// Function not implemented
    NOSYS = 78,
    /// Inappropriate file type or format
    FTYPE = 79,
    /// Authentication error
    AUTH = 80,
    /// Need authenticator
    NEEDAUTH = 81,
    // Intelligent device errors
    /// Device power is off
    PWROFF = 82,
    /// Device error, e.g. paper out
    DEVERR = 83,
    /// Value too large to be stored in data type
    OVERFLOW = 84,
    // Program loading errors
    /// Bad executable
    BADEXEC = 85,
    /// Bad CPU type in executable
    BADARCH = 86,
    /// Shared library version mismatch
    SHLIBVERS = 87,
    /// Malformed Macho file
    BADMACHO = 88,
    /// Operation canceled
    CANCELED = 89,
    /// Identifier removed
    IDRM = 90,
    /// No message of desired type
    NOMSG = 91,
    /// Illegal byte sequence
    ILSEQ = 92,
    /// Attribute not found
    NOATTR = 93,
    /// Bad message
    BADMSG = 94,
    /// Reserved
    MULTIHOP = 95,
    /// No message available on STREAM
    NODATA = 96,
    /// Reserved
    NOLINK = 97,
    /// No STREAM resources
    NOSR = 98,
    /// Not a STREAM
    NOSTR = 99,
    /// Protocol error
    PROTO = 100,
    /// STREAM ioctl timeout
    TIME = 101,
    /// No such policy registered
    NOPOLICY = 103,
    /// State not recoverable
    NOTRECOVERABLE = 104,
    /// Previous owner died
    OWNERDEAD = 105,
    /// Interface output queue is full
    QFULL = 106,
    _,
};

/// From Common Security Services Manager
/// Security.framework/Headers/cssm*.h
pub const DB_RECORDTYPE = enum(u32) {
    // Record Types defined in the Schema Management Name Space
    SCHEMA_INFO = SCHEMA_START + 0,
    SCHEMA_INDEXES = SCHEMA_START + 1,
    SCHEMA_ATTRIBUTES = SCHEMA_START + 2,
    SCHEMA_PARSING_MODULE = SCHEMA_START + 3,

    // Record Types defined in the Open Group Application Name Space
    ANY = OPEN_GROUP_START + 0,
    CERT = OPEN_GROUP_START + 1,
    CRL = OPEN_GROUP_START + 2,
    POLICY = OPEN_GROUP_START + 3,
    GENERIC = OPEN_GROUP_START + 4,
    PUBLIC_KEY = OPEN_GROUP_START + 5,
    PRIVATE_KEY = OPEN_GROUP_START + 6,
    SYMMETRIC_KEY = OPEN_GROUP_START + 7,
    ALL_KEYS = OPEN_GROUP_START + 8,

    // AppleFileDL record types
    GENERIC_PASSWORD = APP_DEFINED_START + 0,
    INTERNET_PASSWORD = APP_DEFINED_START + 1,
    APPLESHARE_PASSWORD = APP_DEFINED_START + 2,

    X509_CERTIFICATE = APP_DEFINED_START + 0x1000,
    USER_TRUST,
    X509_CRL,
    UNLOCK_REFERRAL,
    EXTENDED_ATTRIBUTE,
    METADATA = APP_DEFINED_START + 0x8000,

    _,

    // Schema Management Name Space Range Definition
    pub const SCHEMA_START = 0x00000000;
    pub const SCHEMA_END = SCHEMA_START + 4;

    // Open Group Application Name Space Range Definition
    pub const OPEN_GROUP_START = 0x0000000A;
    pub const OPEN_GROUP_END = OPEN_GROUP_START + 8;

    // Industry At Large Application Name Space Range Definition
    pub const APP_DEFINED_START = 0x80000000;
    pub const APP_DEFINED_END = 0xffffffff;
};

pub const TCP = struct {
    /// Turn off Nagle's algorithm
    pub const NODELAY = 0x01;
    /// Limit MSS
    pub const MAXSEG = 0x02;
    /// Don't push last block of write
    pub const NOPUSH = 0x04;
    /// Don't use TCP options
    pub const NOOPT = 0x08;
    /// Idle time used when SO_KEEPALIVE is enabled
    pub const KEEPALIVE = 0x10;
    /// Connection timeout
    pub const CONNECTIONTIMEOUT = 0x20;
    /// Time after which a conection in persist timeout will terminate.
    pub const PERSIST_TIMEOUT = 0x40;
    /// Time after which TCP retransmissions will be stopped and the connection will be dropped.
    pub const RXT_CONNDROPTIME = 0x80;
    /// Drop a connection after retransmitting the FIN 3 times.
    pub const RXT_FINDROP = 0x100;
    /// Interval between keepalives
    pub const KEEPINTVL = 0x101;
    /// Number of keepalives before clsoe
    pub const KEEPCNT = 0x102;
    /// Always ack every other packet
    pub const SENDMOREACKS = 0x103;
    /// Enable ECN on a connection
    pub const ENABLE_ECN = 0x104;
    /// Enable/Disable TCP Fastopen on this socket
    pub const FASTOPEN = 0x105;
    /// State of the TCP connection
    pub const CONNECTION_INFO = 0x106;
};

pub const MSG = struct {
    /// process out-of-band data
    pub const OOB = 0x1;
    /// peek at incoming message
    pub const PEEK = 0x2;
    /// send without using routing tables
    pub const DONTROUTE = 0x4;
    /// data completes record
    pub const EOR = 0x8;
    /// data discarded before delivery
    pub const TRUNC = 0x10;
    /// control data lost before delivery
    pub const CTRUNC = 0x20;
    /// wait for full request or error
    pub const WAITALL = 0x40;
    /// this message should be nonblocking
    pub const DONTWAIT = 0x80;
    /// data completes connection
    pub const EOF = 0x100;
    /// wait up to full request, may return partial
    pub const WAITSTREAM = 0x200;
    /// Start of 'hold' seq; dump so_temp, deprecated
    pub const FLUSH = 0x400;
    /// Hold frag in so_temp, deprecated
    pub const HOLD = 0x800;
    /// Send the packet in so_temp, deprecated
    pub const SEND = 0x1000;
    /// Data ready to be read
    pub const HAVEMORE = 0x2000;
    /// Data remains in current pkt
    pub const RCVMORE = 0x4000;
    /// Fail receive if socket address cannot be allocated
    pub const NEEDSA = 0x10000;
    /// do not generate SIGPIPE on EOF
    pub const NOSIGNAL = 0x80000;
    /// Inherit upcall in sock_accept
    pub const USEUPCALL = 0x80000000;
};

// https://github.com/apple/darwin-xnu/blob/2ff845c2e033bd0ff64b5b6aa6063a1f8f65aa32/bsd/netinet/in.h#L454
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
    pub const MULTICAST_VIF = 14;
    pub const RSVP_ON = 15;
    pub const RSVP_OFF = 16;
    pub const RSVP_VIF_ON = 17;
    pub const RSVP_VIF_OFF = 18;
    pub const PORTRANGE = 19;
    pub const RECVIF = 20;
    pub const IPSEC_POLICY = 21;
    pub const FAITH = 22;
    pub const STRIPHDR = 23;
    pub const RECVTTL = 24;
    pub const BOUND_IF = 25;
    pub const PKTINFO = 26;
    pub const RECVPKTINFO = PKTINFO;
    pub const RECVTOS = 27;
    pub const DONTFRAG = 28;
    pub const FW_ADD = 40;
    pub const FW_DEL = 41;
    pub const FW_FLUSH = 42;
    pub const FW_ZERO = 43;
    pub const FW_GET = 44;
    pub const FW_RESETLOG = 45;
    pub const OLD_FW_ADD = 50;
    pub const OLD_FW_DEL = 51;
    pub const OLD_FW_FLUSH = 52;
    pub const OLD_FW_ZERO = 53;
    pub const OLD_FW_GET = 54;
    pub const OLD_FW_RESETLOG = 56;
    pub const DUMMYNET_CONFIGURE = 60;
    pub const DUMMYNET_DEL = 61;
    pub const DUMMYNET_FLUSH = 62;
    pub const DUMMYNET_GET = 64;
    pub const TRAFFIC_MGT_BACKGROUND = 65;
    pub const MULTICAST_IFINDEX = 66;
    pub const ADD_SOURCE_MEMBERSHIP = 70;
    pub const DROP_SOURCE_MEMBERSHIP = 71;
    pub const BLOCK_SOURCE = 72;
    pub const UNBLOCK_SOURCE = 73;
    pub const MSFILTER = 74;
    // Same namespace, but these are arguments rather than option names
    pub const DEFAULT_MULTICAST_TTL = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const MIN_MEMBERSHIPS = 31;
    pub const MAX_MEMBERSHIPS = 4095;
    pub const MAX_GROUP_SRC_FILTER = 512;
    pub const MAX_SOCK_SRC_FILTER = 128;
    pub const MAX_SOCK_MUTE_FILTER = 128;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/apple/darwin-xnu/blob/2ff845c2e033bd0ff64b5b6aa6063a1f8f65aa32/bsd/netinet6/in6.h#L521
pub const IPV6 = struct {
    pub const UNICAST_HOPS = 4;
    pub const MULTICAST_IF = 9;
    pub const MULTICAST_HOPS = 10;
    pub const MULTICAST_LOOP = 11;
    pub const JOIN_GROUP = 12;
    pub const LEAVE_GROUP = 13;
    pub const PORTRANGE = 14;
    pub const @"2292PKTINFO" = 19;
    pub const @"2292HOPLIMIT" = 20;
    pub const @"2292NEXTHOP" = 21;
    pub const @"2292HOPOPTS" = 22;
    pub const @"2292DSTOPTS" = 23;
    pub const @"2292RTHDR" = 24;
    pub const @"2292PKTOPTIONS" = 25;
    pub const CHECKSUM = 26;
    pub const V6ONLY = 27;
    pub const BINDV6ONLY = V6ONLY;
    pub const IPSEC_POLICY = 28;
    pub const FAITH = 29;
    pub const FW_ADD = 30;
    pub const FW_DEL = 31;
    pub const FW_FLUSH = 32;
    pub const FW_ZERO = 33;
    pub const FW_GET = 34;
    pub const RECVTCLASS = 35;
    pub const TCLASS = 36;
    pub const RTHDRDSTOPTS = 57;
    pub const RECVPKTINFO = 61;
    pub const RECVHOPLIMIT = 37;
    pub const RECVRTHDR = 38;
    pub const RECVHOPOPTS = 39;
    pub const RECVDSTOPTS = 40;
    pub const RECVRTHDRDSTOPTS = 41;
    pub const USE_MIN_MTU = 42;
    pub const RECVPATHMTU = 43;
    pub const PATHMTU = 44;
    pub const REACHCONF = 45;
    pub const @"3542PKTINFO" = 46;
    pub const @"3542HOPLIMIT" = 47;
    pub const @"3542NEXTHOP" = 48;
    pub const @"3542HOPOPTS" = 49;
    pub const @"3542DSTOPTS" = 50;
    pub const @"3542RTHDR" = 51;
    pub const PKTINFO = @"3542PKTINFO";
    pub const HOPLIMIT = @"3542HOPLIMIT";
    pub const NEXTHOP = @"3542NEXTHOP";
    pub const HOPOPTS = @"3542HOPOPTS";
    pub const DSTOPTS = @"3542DSTOPTS";
    pub const RTHDR = @"3542RTHDR";
    pub const AUTOFLOWLABEL = 59;
    pub const DONTFRAG = 62;
    pub const PREFER_TEMPADDR = 63;
    pub const MSFILTER = 74;
    pub const BOUND_IF = 125;
    // Same namespace, but these are arguments rather than option names
    pub const RTHDR_LOOSE = 0;
    pub const RTHDR_STRICT = 1;
    pub const RTHDR_TYPE_0 = 0;
    pub const DEFAULT_MULTICAST_HOPS = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const MIN_MEMBERSHIPS = 31;
    pub const MAX_MEMBERSHIPS = 4095;
    pub const MAX_GROUP_SRC_FILTER = 512;
    pub const MAX_SOCK_SRC_FILTER = 128;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/apple/darwin-xnu/blob/2ff845c2e033bd0ff64b5b6aa6063a1f8f65aa32/bsd/netinet/ip.h#L129
pub const IPTOS = struct {
    pub const LOWDELAY = 0x10;
    pub const THROUGHPUT = 0x08;
    pub const RELIABILITY = 0x04;
    pub const MINCOST = 0x02;
    pub const CE = 0x01;
    pub const ECT = 0x02;
    pub const DSCP_SHIFT = 2;
    pub const ECN_NOTECT = 0x00;
    pub const ECN_ECT1 = 0x01;
    pub const ECN_ECT0 = 0x02;
    pub const ECN_CE = 0x03;
    pub const ECN_MASK = 0x03;
    pub const PREC_NETCONTROL = 0xe0;
    pub const PREC_INTERNETCONTROL = 0xc0;
    pub const PREC_CRITIC_ECP = 0xa0;
    pub const PREC_FLASHOVERRIDE = 0x80;
    pub const PREC_FLASH = 0x60;
    pub const PREC_IMMEDIATE = 0x40;
    pub const PREC_PRIORITY = 0x20;
    pub const PREC_ROUTINE = 0x00;
};



---
File: /std/c/dragonfly.zig
---

const std = @import("../std.zig");

const SIG = std.c.SIG;
const caddr_t = std.c.caddr_t;
const gid_t = std.c.gid_t;
const iovec = std.c.iovec;
const pid_t = std.c.pid_t;
const socklen_t = std.c.socklen_t;
const uid_t = std.c.uid_t;

pub extern "c" fn lwp_gettid() c_int;
pub extern "c" fn ptrace(request: c_int, pid: pid_t, addr: caddr_t, data: c_int) c_int;
pub extern "c" fn umtx_sleep(ptr: *const volatile c_int, value: c_int, timeout: c_int) c_int;
pub extern "c" fn umtx_wakeup(ptr: *const volatile c_int, count: c_int) c_int;

pub const E = enum(u16) {
    /// No error occurred.
    SUCCESS = 0,

    PERM = 1,
    NOENT = 2,
    SRCH = 3,
    INTR = 4,
    IO = 5,
    NXIO = 6,
    @"2BIG" = 7,
    NOEXEC = 8,
    BADF = 9,
    CHILD = 10,
    DEADLK = 11,
    NOMEM = 12,
    ACCES = 13,
    FAULT = 14,
    NOTBLK = 15,
    BUSY = 16,
    EXIST = 17,
    XDEV = 18,
    NODEV = 19,
    NOTDIR = 20,
    ISDIR = 21,
    INVAL = 22,
    NFILE = 23,
    MFILE = 24,
    NOTTY = 25,
    TXTBSY = 26,
    FBIG = 27,
    NOSPC = 28,
    SPIPE = 29,
    ROFS = 30,
    MLINK = 31,
    PIPE = 32,
    DOM = 33,
    RANGE = 34,
    /// This code is also used for `WOULDBLOCK`.
    AGAIN = 35,
    INPROGRESS = 36,
    ALREADY = 37,
    NOTSOCK = 38,
    DESTADDRREQ = 39,
    MSGSIZE = 40,
    PROTOTYPE = 41,
    NOPROTOOPT = 42,
    PROTONOSUPPORT = 43,
    SOCKTNOSUPPORT = 44,
    /// This code is also used for `NOTSUP`.
    OPNOTSUPP = 45,
    PFNOSUPPORT = 46,
    AFNOSUPPORT = 47,
    ADDRINUSE = 48,
    ADDRNOTAVAIL = 49,
    NETDOWN = 50,
    NETUNREACH = 51,
    NETRESET = 52,
    CONNABORTED = 53,
    CONNRESET = 54,
    NOBUFS = 55,
    ISCONN = 56,
    NOTCONN = 57,
    SHUTDOWN = 58,
    TOOMANYREFS = 59,
    TIMEDOUT = 60,
    CONNREFUSED = 61,
    LOOP = 62,
    NAMETOOLONG = 63,
    HOSTDOWN = 64,
    HOSTUNREACH = 65,
    NOTEMPTY = 66,
    PROCLIM = 67,
    USERS = 68,
    DQUOT = 69,
    STALE = 70,
    REMOTE = 71,
    BADRPC = 72,
    RPCMISMATCH = 73,
    PROGUNAVAIL = 74,
    PROGMISMATCH = 75,
    PROCUNAVAIL = 76,
    NOLCK = 77,
    NOSYS = 78,
    FTYPE = 79,
    AUTH = 80,
    NEEDAUTH = 81,
    IDRM = 82,
    NOMSG = 83,
    OVERFLOW = 84,
    CANCELED = 85,
    ILSEQ = 86,
    NOATTR = 87,
    DOOFUS = 88,
    BADMSG = 89,
    MULTIHOP = 90,
    NOLINK = 91,
    PROTO = 92,
    NOMEDIUM = 93,
    ASYNC = 99,
    _,
};

pub const BADSIG = SIG.ERR;

pub const sig_t = *const fn (i32) callconv(.c) void;

pub const cmsgcred = extern struct {
    pid: pid_t,
    uid: uid_t,
    euid: uid_t,
    gid: gid_t,
    ngroups: c_short,
    groups: [16]gid_t,
};
pub const sf_hdtr = extern struct {
    headers: [*]iovec,
    hdr_cnt: c_int,
    trailers: [*]iovec,
    trl_cnt: c_int,
};

pub const MS_SYNC = 0;
pub const MS_ASYNC = 1;
pub const MS_INVALIDATE = 2;

pub const POSIX_MADV_SEQUENTIAL = 2;
pub const POSIX_MADV_RANDOM = 1;
pub const POSIX_MADV_DONTNEED = 4;
pub const POSIX_MADV_NORMAL = 0;
pub const POSIX_MADV_WILLNEED = 3;

// https://github.com/DragonFlyBSD/DragonFlyBSD/blob/6098912863ed4c7b3f70d7483910ce2956cf4ed3/sys/netinet/ip.h#L94
pub const IP = struct {
    pub const OPTIONS = 1;
    pub const HDRINCL = 2;
    pub const TOS = 3;
    pub const TTL = 4;
    pub const RECVOPTS = 5;
    pub const RECVRETOPTS = 6;
    pub const RECVDSTADDR = 7;
    pub const SENDSRCADDR = RECVDSTADDR;
    pub const RETOPTS = 8;
    pub const MULTICAST_IF = 9;
    pub const MULTICAST_TTL = 10;
    pub const MULTICAST_LOOP = 11;
    pub const ADD_MEMBERSHIP = 12;
    pub const DROP_MEMBERSHIP = 13;
    pub const MULTICAST_VIF = 14;
    pub const RSVP_ON = 15;
    pub const RSVP_OFF = 16;
    pub const RSVP_VIF_ON = 17;
    pub const RSVP_VIF_OFF = 18;
    pub const PORTRANGE = 19;
    pub const RECVIF = 20;
    pub const FW_TBL_CREATE = 40;
    pub const FW_TBL_DESTROY = 41;
    pub const FW_TBL_ADD = 42;
    pub const FW_TBL_DEL = 43;
    pub const FW_TBL_FLUSH = 44;
    pub const FW_TBL_GET = 45;
    pub const FW_TBL_ZERO = 46;
    pub const FW_TBL_EXPIRE = 47;
    pub const FW_X = 49;
    pub const FW_ADD = 50;
    pub const FW_DEL = 51;
    pub const FW_FLUSH = 52;
    pub const FW_ZERO = 53;
    pub const FW_GET = 54;
    pub const FW_RESETLOG = 55;
    pub const DUMMYNET_CONFIGURE = 60;
    pub const DUMMYNET_DEL = 61;
    pub const DUMMYNET_FLUSH = 62;
    pub const DUMMYNET_GET = 64;
    pub const RECVTTL = 65;
    pub const MINTTL = 66;
    pub const RECVTOS = 68;
    // Same namespace, but these are arguments rather than option names
    pub const DEFAULT_MULTICAST_TTL = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const MAX_MEMBERSHIPS = 20;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/DragonFlyBSD/DragonFlyBSD/blob/6098912863ed4c7b3f70d7483910ce2956cf4ed3/sys/netinet6/in6.h#L448
pub const IPV6 = struct {
    pub const UNICAST_HOPS = 4;
    pub const MULTICAST_IF = 9;
    pub const MULTICAST_HOPS = 10;
    pub const MULTICAST_LOOP = 11;
    pub const JOIN_GROUP = 12;
    pub const LEAVE_GROUP = 13;
    pub const PORTRANGE = 14;
    pub const @"2292PKTINFO" = 19;
    pub const @"2292HOPLIMIT" = 20;
    pub const @"2292NEXTHOP" = 21;
    pub const @"2292HOPOPTS" = 22;
    pub const @"2292DSTOPTS" = 23;
    pub const @"2292RTHDR" = 24;
    pub const @"2292PKTOPTIONS" = 25;
    pub const CHECKSUM = 26;
    pub const V6ONLY = 27;
    pub const BINDV6ONLY = V6ONLY;
    pub const FW_ADD = 30;
    pub const FW_DEL = 31;
    pub const FW_FLUSH = 32;
    pub const FW_ZERO = 33;
    pub const FW_GET = 34;
    pub const RTHDRDSTOPTS = 35;
    pub const RECVPKTINFO = 36;
    pub const RECVHOPLIMIT = 37;
    pub const RECVRTHDR = 38;
    pub const RECVHOPOPTS = 39;
    pub const RECVDSTOPTS = 40;
    pub const RECVRTHDRDSTOPTS = 41;
    pub const USE_MIN_MTU = 42;
    pub const RECVPATHMTU = 43;
    pub const PATHMTU = 44;
    pub const REACHCONF = 45;
    pub const PKTINFO = 46;
    pub const HOPLIMIT = 47;
    pub const NEXTHOP = 48;
    pub const HOPOPTS = 49;
    pub const DSTOPTS = 50;
    pub const RTHDR = 51;
    pub const PKTOPTIONS = 52;
    pub const RECVTCLASS = 57;
    pub const AUTOFLOWLABEL = 59;
    pub const TCLASS = 61;
    pub const DONTFRAG = 62;
    pub const PREFER_TEMPADDR = 63;
    pub const MSFILTER = 74;
    // Same namespace, but these are arguments rather than option names
    pub const RTHDR_LOOSE = 0;
    pub const RTHDR_STRICT = 1;
    pub const RTHDR_TYPE_0 = 0;
    pub const DEFAULT_MULTICAST_HOPS = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/DragonFlyBSD/DragonFlyBSD/blob/6098912863ed4c7b3f70d7483910ce2956cf4ed3/sys/netinet/ip.h#L94
pub const IPTOS = struct {
    pub const LOWDELAY = 0x10;
    pub const THROUGHPUT = 0x08;
    pub const RELIABILITY = 0x04;
    pub const MINCOST = 0x02;
    pub const CE = 0x01;
    pub const ECT = 0x02;
    pub const PREC_ROUTINE = DSCP_CS0;
    pub const PREC_PRIORITY = DSCP_CS1;
    pub const PREC_IMMEDIATE = DSCP_CS2;
    pub const PREC_FLASH = DSCP_CS3;
    pub const PREC_FLASHOVERRIDE = DSCP_CS4;
    pub const PREC_CRITIC_ECP = DSCP_CS5;
    pub const PREC_INTERNETCONTROL = DSCP_CS6;
    pub const PREC_NETCONTROL = DSCP_CS7;
    pub const DSCP_CS0 = 0x00;
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
    pub const DSCP_VA = 0xb0;
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
File: /std/c/freebsd.zig
---

const builtin = @import("builtin");
const std = @import("../std.zig");
const assert = std.debug.assert;

const PATH_MAX = std.c.PATH_MAX;
const blkcnt_t = std.c.blkcnt_t;
const blksize_t = std.c.blksize_t;
const caddr_t = std.c.caddr_t;
const dev_t = std.c.dev_t;
const fd_t = std.c.fd_t;
const gid_t = std.c.gid_t;
const ino_t = std.c.ino_t;
const iovec_const = std.posix.iovec_const;
const mode_t = std.c.mode_t;
const nlink_t = std.c.nlink_t;
const off_t = std.c.off_t;
const pid_t = std.c.pid_t;
const sockaddr = std.c.sockaddr;
const time_t = std.c.time_t;
const timespec = std.c.timespec;
const uid_t = std.c.uid_t;
const sf_hdtr = std.c.sf_hdtr;
const clockid_t = std.c.clockid_t;

comptime {
    assert(builtin.os.tag == .freebsd); // Prevent access of std.c symbols on wrong OS.
}

pub extern "c" fn ptrace(request: c_int, pid: pid_t, addr: caddr_t, data: c_int) c_int;

pub extern "c" fn kinfo_getfile(pid: pid_t, cntp: *c_int) ?[*]kinfo_file;
pub extern "c" fn copy_file_range(fd_in: fd_t, off_in: ?*off_t, fd_out: fd_t, off_out: ?*off_t, len: usize, flags: u32) isize;

pub extern "c" fn sendfile(
    in_fd: fd_t,
    out_fd: fd_t,
    offset: off_t,
    nbytes: usize,
    sf_hdtr: ?*sf_hdtr,
    sbytes: ?*off_t,
    flags: u32,
) c_int;

pub const UMTX_OP = enum(c_int) {
    LOCK = 0,
    UNLOCK = 1,
    WAIT = 2,
    WAKE = 3,
    MUTEX_TRYLOCK = 4,
    MUTEX_LOCK = 5,
    MUTEX_UNLOCK = 6,
    SET_CEILING = 7,
    CV_WAIT = 8,
    CV_SIGNAL = 9,
    CV_BROADCAST = 10,
    WAIT_UINT = 11,
    RW_RDLOCK = 12,
    RW_WRLOCK = 13,
    RW_UNLOCK = 14,
    WAIT_UINT_PRIVATE = 15,
    WAKE_PRIVATE = 16,
    MUTEX_WAIT = 17,
    MUTEX_WAKE = 18, // deprecated
    SEM_WAIT = 19, // deprecated
    SEM_WAKE = 20, // deprecated
    NWAKE_PRIVATE = 31,
    MUTEX_WAKE2 = 22,
    SEM2_WAIT = 23,
    SEM2_WAKE = 24,
    SHM = 25,
    ROBUST_LISTS = 26,
};

pub const UMTX_ABSTIME = 0x01;
pub const _umtx_time = extern struct {
    timeout: timespec,
    flags: u32,
    clockid: clockid_t,
};

pub extern "c" fn _umtx_op(obj: usize, op: c_int, val: c_ulong, uaddr: usize, uaddr2: usize) c_int;

pub const fflags_t = u32;

pub const Stat = extern struct {
    /// The inode's device.
    dev: dev_t,
    /// The inode's number.
    ino: ino_t,
    /// Number of hard links.
    nlink: nlink_t,
    /// Inode protection mode.
    mode: mode_t,
    __pad0: i16,
    /// User ID of the file's owner.
    uid: uid_t,
    /// Group ID of the file's group.
    gid: gid_t,
    __pad1: i32,
    /// Device type.
    rdev: dev_t,
    /// Time of last access.
    atim: timespec,
    /// Time of last data modification.
    mtim: timespec,
    /// Time of last file status change.
    ctim: timespec,
    /// Time of file creation.
    birthtim: timespec,
    /// File size, in bytes.
    size: off_t,
    /// Blocks allocated for file.
    blocks: blkcnt_t,
    /// Optimal blocksize for I/O.
    blksize: blksize_t,
    /// User defined flags for file.
    flags: fflags_t,
    /// File generation number.
    gen: u64,
    __spare: [10]u64,

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
};

pub const fsblkcnt_t = u64;
pub const fsfilcnt_t = u64;

pub const CAP_RIGHTS_VERSION = 0;

pub const cap_rights = extern struct {
    rights: [CAP_RIGHTS_VERSION + 2]u64,
};

pub const kinfo_file = extern struct {
    /// Size of this record.
    /// A zero value is for the sentinel record at the end of an array.
    structsize: c_int,
    /// Descriptor type.
    type: c_int,
    /// Array index.
    fd: fd_t,
    /// Reference count.
    ref_count: c_int,
    /// Flags.
    flags: c_int,
    // 64bit padding.
    _pad0: c_int,
    /// Seek location.
    offset: i64,
    un: extern union {
        socket: extern struct {
            /// Sendq size.
            sendq: u32,
            /// Socket domain.
            domain: c_int,
            /// Socket type.
            type: c_int,
            /// Socket protocol.
            protocol: c_int,
            /// Socket address.
            address: sockaddr.storage,
            /// Peer address.
            peer: sockaddr.storage,
            /// Address of so_pcb.
            pcb: u64,
            /// Address of inp_ppcb.
            inpcb: u64,
            /// Address of unp_conn.
            unpconn: u64,
            /// Send buffer state.
            snd_sb_state: u16,
            /// Receive buffer state.
            rcv_sb_state: u16,
            /// Recvq size.
            recvq: u32,
        },
        file: extern struct {
            /// Vnode type.
            type: i32,
            // Reserved for future use
            _spare1: [3]i32,
            _spare2: [30]u64,
            /// Vnode filesystem id.
            fsid: u64,
            /// File device.
            rdev: u64,
            /// Global file id.
            fileid: u64,
            /// File size.
            size: u64,
            /// fsid compat for FreeBSD 11.
            fsid_freebsd11: u32,
            /// rdev compat for FreeBSD 11.
            rdev_freebsd11: u32,
            /// File mode.
            mode: u16,
            // 64bit padding.
            _pad0: u16,
            _pad1: u32,
        },
        sem: extern struct {
            _spare0: [4]u32,
            _spare1: [32]u64,
            /// Semaphore value.
            value: u32,
            /// Semaphore mode.
            mode: u16,
        },
        pipe: extern struct {
            _spare1: [4]u32,
            _spare2: [32]u64,
            addr: u64,
            peer: u64,
            buffer_cnt: u32,
            // 64bit padding.
            kf_pipe_pad0: [3]u32,
        },
        proc: extern struct {
            _spare1: [4]u32,
            _spare2: [32]u64,
            pid: pid_t,
        },
        eventfd: extern struct {
            value: u64,
            flags: u32,
        },
    },
    /// Status flags.
    status: u16,
    // 32-bit alignment padding.
    _pad1: u16,
    // Reserved for future use.
    _spare: c_int,
    /// Capability rights.
    cap_rights: cap_rights,
    /// Reserved for future cap_rights
    _cap_spare: u64,
    /// Path to file, if any.
    path: [PATH_MAX]u8,

    comptime {
        assert(@sizeOf(@This()) == KINFO_FILE_SIZE);
        assert(@alignOf(@This()) == @sizeOf(u64));
    }
};

pub const KINFO_FILE_SIZE = 1392;

pub const MFD = struct {
    pub const CLOEXEC = 0x0001;
    pub const ALLOW_SEALING = 0x0002;
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
    ROFS = 30, // Read-only filesystem
    MLINK = 31, // Too many links
    PIPE = 32, // Broken pipe

    // math software
    DOM = 33, // Numerical argument out of domain
    RANGE = 34, // Result too large

    // non-blocking and interrupt i/o

    /// Resource temporarily unavailable
    /// This code is also used for `WOULDBLOCK`: operation would block.
    AGAIN = 35,
    INPROGRESS = 36, // Operation now in progress
    ALREADY = 37, // Operation already in progress

    // ipc/network software -- argument errors
    NOTSOCK = 38, // Socket operation on non-socket
    DESTADDRREQ = 39, // Destination address required
    MSGSIZE = 40, // Message too long
    PROTOTYPE = 41, // Protocol wrong type for socket
    NOPROTOOPT = 42, // Protocol not available
    PROTONOSUPPORT = 43, // Protocol not supported
    SOCKTNOSUPPORT = 44, // Socket type not supported
    /// Operation not supported
    /// This code is also used for `NOTSUP`.
    OPNOTSUPP = 45,
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
    IDRM = 82, // Identifier removed
    NOMSG = 83, // No message of desired type
    OVERFLOW = 84, // Value too large to be stored in data type
    CANCELED = 85, // Operation canceled
    ILSEQ = 86, // Illegal byte sequence
    NOATTR = 87, // Attribute not found

    DOOFUS = 88, // Programming error

    BADMSG = 89, // Bad message
    MULTIHOP = 90, // Multihop attempted
    NOLINK = 91, // Link has been severed
    PROTO = 92, // Protocol error

    NOTCAPABLE = 93, // Capabilities insufficient
    CAPMODE = 94, // Not permitted in capability mode
    NOTRECOVERABLE = 95, // State not recoverable
    OWNERDEAD = 96, // Previous owner died
    INTEGRITY = 97, // Integrity check failed
    _,
};

// https://github.com/freebsd/freebsd-src/blob/9bfbc6826f72eb385bf52f4cde8080bccf7e3ebd/sys/netinet/in.h#L436
pub const IP = struct {
    pub const OPTIONS = 1;
    pub const HDRINCL = 2;
    pub const TOS = 3;
    pub const TTL = 4;
    pub const RECVOPTS = 5;
    pub const RECVRETOPTS = 6;
    pub const RECVDSTADDR = 7;
    pub const SENDSRCADDR = RECVDSTADDR;
    pub const RETOPTS = 8;
    pub const MULTICAST_IF = 9;
    pub const MULTICAST_TTL = 10;
    pub const MULTICAST_LOOP = 11;
    pub const ADD_MEMBERSHIP = 12;
    pub const DROP_MEMBERSHIP = 13;
    pub const MULTICAST_VIF = 14;
    pub const RSVP_ON = 15;
    pub const RSVP_OFF = 16;
    pub const RSVP_VIF_ON = 17;
    pub const RSVP_VIF_OFF = 18;
    pub const PORTRANGE = 19;
    pub const RECVIF = 20;
    pub const IPSEC_POLICY = 21;
    pub const ONESBCAST = 23;
    pub const BINDANY = 24;
    pub const ORIGDSTADDR = 27;
    pub const RECVORIGDSTADDR = ORIGDSTADDR;
    pub const FW_TABLE_ADD = 40;
    pub const FW_TABLE_DEL = 41;
    pub const FW_TABLE_FLUSH = 42;
    pub const FW_TABLE_GETSIZE = 43;
    pub const FW_TABLE_LIST = 44;
    pub const FW3 = 48;
    pub const DUMMYNET3 = 49;
    pub const FW_ADD = 50;
    pub const FW_DEL = 51;
    pub const FW_FLUSH = 52;
    pub const FW_ZERO = 53;
    pub const FW_GET = 54;
    pub const FW_RESETLOG = 55;
    pub const FW_NAT_CFG = 56;
    pub const FW_NAT_DEL = 57;
    pub const FW_NAT_GET_CONFIG = 58;
    pub const FW_NAT_GET_LOG = 59;
    pub const DUMMYNET_CONFIGURE = 60;
    pub const DUMMYNET_DEL = 61;
    pub const DUMMYNET_FLUSH = 62;
    pub const DUMMYNET_GET = 64;
    pub const RECVTTL = 65;
    pub const MINTTL = 66;
    pub const DONTFRAG = 67;
    pub const RECVTOS = 68;
    pub const ADD_SOURCE_MEMBERSHIP = 70;
    pub const DROP_SOURCE_MEMBERSHIP = 71;
    pub const BLOCK_SOURCE = 72;
    pub const UNBLOCK_SOURCE = 73;
    pub const MSFILTER = 74;
    pub const VLAN_PCP = 75;
    pub const FLOWID = 90;
    pub const FLOWTYPE = 91;
    pub const RSSBUCKETID = 92;
    pub const RECVFLOWID = 93;
    pub const RECVRSSBUCKETID = 94;
    // Same namespace, but these are arguments rather than option names
    pub const DEFAULT_MULTICAST_TTL = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const MAX_MEMBERSHIPS = 4095;
    pub const MAX_GROUP_SRC_FILTER = 512;
    pub const MAX_SOCK_SRC_FILTER = 128;
    pub const MAX_SOCK_MUTE_FILTER = 128;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/freebsd/freebsd-src/blob/9bfbc6826f72eb385bf52f4cde8080bccf7e3ebd/sys/netinet6/in6.h#L402
pub const IPV6 = struct {
    pub const UNICAST_HOPS = 4;
    pub const MULTICAST_IF = 9;
    pub const MULTICAST_HOPS = 10;
    pub const MULTICAST_LOOP = 11;
    pub const JOIN_GROUP = 12;
    pub const LEAVE_GROUP = 13;
    pub const PORTRANGE = 14;
    pub const @"2292PKTINFO" = 19;
    pub const @"2292HOPLIMIT" = 20;
    pub const @"2292NEXTHOP" = 21;
    pub const @"2292HOPOPTS" = 22;
    pub const @"2292DSTOPTS" = 23;
    pub const @"2292RTHDR" = 24;
    pub const @"2292PKTOPTIONS" = 25;
    pub const CHECKSUM = 26;
    pub const V6ONLY = 27;
    pub const BINDV6ONLY = V6ONLY;
    pub const IPSEC_POLICY = 28;
    pub const FW_ADD = 30;
    pub const FW_DEL = 31;
    pub const FW_FLUSH = 32;
    pub const FW_ZERO = 33;
    pub const FW_GET = 34;
    pub const RTHDRDSTOPTS = 35;
    pub const RECVPKTINFO = 36;
    pub const RECVHOPLIMIT = 37;
    pub const RECVRTHDR = 38;
    pub const RECVHOPOPTS = 39;
    pub const RECVDSTOPTS = 40;
    pub const RECVRTHDRDSTOPTS = 41;
    pub const USE_MIN_MTU = 42;
    pub const RECVPATHMTU = 43;
    pub const PATHMTU = 44;
    pub const REACHCONF = 45;
    pub const PKTINFO = 46;
    pub const HOPLIMIT = 47;
    pub const NEXTHOP = 48;
    pub const HOPOPTS = 49;
    pub const DSTOPTS = 50;
    pub const RTHDR = 51;
    pub const PKTOPTIONS = 52;
    pub const RECVTCLASS = 57;
    pub const AUTOFLOWLABEL = 59;
    pub const TCLASS = 61;
    pub const DONTFRAG = 62;
    pub const PREFER_TEMPADDR = 63;
    pub const BINDANY = 64;
    pub const FLOWID = 67;
    pub const FLOWTYPE = 68;
    pub const RSSBUCKETID = 69;
    pub const RECVFLOWID = 70;
    pub const RECVRSSBUCKETID = 71;
    pub const ORIGDSTADDR = 72;
    pub const RECVORIGDSTADDR = ORIGDSTADDR;
    pub const MSFILTER = 74;
    pub const VLAN_PCP = 75;
    // Same namespace, but these are arguments rather than option names
    pub const RTHDR_LOOSE = 0;
    pub const RTHDR_STRICT = 1;
    pub const RTHDR_TYPE_0 = 0;
    pub const DEFAULT_MULTICAST_HOPS = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const MAX_MEMBERSHIPS = 4095;
    pub const MAX_GROUP_SRC_FILTER = 512;
    pub const MAX_SOCK_SRC_FILTER = 128;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/freebsd/freebsd-src/blob/9bfbc6826f72eb385bf52f4cde8080bccf7e3ebd/sys/netinet/ip.h#L77
pub const IPTOS = struct {
    pub const LOWDELAY = 0x10;
    pub const THROUGHPUT = 0x08;
    pub const RELIABILITY = 0x04;
    pub const MINCOST = DSCP_CS0;
    pub const PREC_ROUTINE = DSCP_CS0;
    pub const PREC_PRIORITY = DSCP_CS1;
    pub const PREC_IMMEDIATE = DSCP_CS2;
    pub const PREC_FLASH = DSCP_CS3;
    pub const PREC_FLASHOVERRIDE = DSCP_CS4;
    pub const PREC_CRITIC_ECP = DSCP_CS5;
    pub const PREC_INTERNETCONTROL = DSCP_CS6;
    pub const PREC_NETCONTROL = DSCP_CS7;
    pub const DSCP_OFFSET = 2;
    pub const DSCP_CS0 = 0x00;
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
    pub const DSCP_VA = 0xb0;
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
File: /std/c/haiku.zig
---

const std = @import("../std.zig");
const assert = std.debug.assert;
const builtin = @import("builtin");
const maxInt = std.math.maxInt;
const iovec = std.posix.iovec;
const iovec_const = std.posix.iovec_const;
const socklen_t = std.c.socklen_t;
const fd_t = std.c.fd_t;
const PATH_MAX = std.c.PATH_MAX;
const uid_t = std.c.uid_t;
const gid_t = std.c.gid_t;
const dev_t = std.c.dev_t;
const ino_t = std.c.ino_t;

comptime {
    assert(builtin.os.tag == .haiku); // Prevent access of std.c symbols on wrong OS.
}

pub extern "root" fn _errnop() *i32;
pub extern "root" fn find_directory(which: directory_which, volume: i32, createIt: bool, path_ptr: [*]u8, length: i32) u64;
pub extern "root" fn find_thread(thread_name: ?*anyopaque) i32;
pub extern "root" fn get_system_info(system_info: *system_info) usize;
pub extern "root" fn _get_team_info(team: i32, team_info: *team_info, size: usize) i32;
pub extern "root" fn _get_next_area_info(team: i32, cookie: *i64, area_info: *area_info, size: usize) i32;
pub extern "root" fn _get_next_image_info(team: i32, cookie: *i32, image_info: *image_info, size: usize) i32;
pub extern "root" fn _kern_get_current_team() team_id;
pub extern "root" fn _kern_open_dir(fd: fd_t, path: [*:0]const u8) fd_t;
pub extern "root" fn _kern_read_dir(fd: fd_t, buffer: [*]u8, bufferSize: usize, maxCount: u32) isize;
pub extern "root" fn _kern_rewind_dir(fd: fd_t) status_t;
pub extern "root" fn _kern_read_stat(fd: fd_t, path: [*:0]const u8, traverseLink: bool, stat: *std.c.Stat, statSize: usize) status_t;

pub const area_info = extern struct {
    area: u32,
    name: [32]u8,
    size: usize,
    lock: u32,
    protection: u32,
    team_id: i32,
    ram_size: u32,
    copy_count: u32,
    in_count: u32,
    out_count: u32,
    address: *anyopaque,
};

pub const image_info = extern struct {
    id: u32,
    image_type: u32,
    sequence: i32,
    init_order: i32,
    init_routine: *anyopaque,
    term_routine: *anyopaque,
    device: i32,
    node: i64,
    name: [PATH_MAX]u8,
    text: *anyopaque,
    data: *anyopaque,
    text_size: i32,
    data_size: i32,
    api_version: i32,
    abi: i32,
};

pub const system_info = extern struct {
    boot_time: i64,
    cpu_count: u32,
    max_pages: u64,
    used_pages: u64,
    cached_pages: u64,
    block_cache_pages: u64,
    ignored_pages: u64,
    needed_memory: u64,
    free_memory: u64,
    max_swap_pages: u64,
    free_swap_pages: u64,
    page_faults: u32,
    max_sems: u32,
    used_sems: u32,
    max_ports: u32,
    used_ports: u32,
    max_threads: u32,
    used_threads: u32,
    max_teams: u32,
    used_teams: u32,
    kernel_name: [256]u8,
    kernel_build_date: [32]u8,
    kernel_build_time: [32]u8,
    kernel_version: i64,
    abi: u32,
};

pub const team_info = extern struct {
    team_id: i32,
    thread_count: i32,
    image_count: i32,
    area_count: i32,
    debugger_nub_thread: i32,
    debugger_nub_port: i32,
    argc: i32,
    args: [64]u8,
    uid: uid_t,
    gid: gid_t,
};

pub const directory_which = enum(i32) {
    B_USER_SETTINGS_DIRECTORY = 0xbbe,

    _,
};

pub const area_id = i32;
pub const port_id = i32;
pub const sem_id = i32;
pub const team_id = i32;
pub const thread_id = i32;

pub const E = enum(i32) {
    pub const B_GENERAL_ERROR_BASE: i32 = std.math.minInt(i32);
    pub const B_OS_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x1000;
    pub const B_APP_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x2000;
    pub const B_INTERFACE_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x3000;
    pub const B_MEDIA_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x4000;
    pub const B_TRANSLATION_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x4800;
    pub const B_MIDI_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x5000;
    pub const B_STORAGE_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x6000;
    pub const B_POSIX_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x7000;
    pub const B_MAIL_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x8000;
    pub const B_PRINT_ERROR_BASE = B_GENERAL_ERROR_BASE + 0x9000;
    pub const B_DEVICE_ERROR_BASE = B_GENERAL_ERROR_BASE + 0xa000;

    pub const B_ERRORS_END = B_GENERAL_ERROR_BASE + 0xffff;

    pub const B_NO_MEMORY = B_GENERAL_ERROR_BASE + 0;
    pub const B_IO_ERROR = B_GENERAL_ERROR_BASE + 1;
    pub const B_PERMISSION_DENIED = B_GENERAL_ERROR_BASE + 2;
    pub const B_BAD_INDEX = B_GENERAL_ERROR_BASE + 3;
    pub const B_BAD_TYPE = B_GENERAL_ERROR_BASE + 4;
    pub const B_BAD_VALUE = B_GENERAL_ERROR_BASE + 5;
    pub const B_MISMATCHED_VALUES = B_GENERAL_ERROR_BASE + 6;
    pub const B_NAME_NOT_FOUND = B_GENERAL_ERROR_BASE + 7;
    pub const B_NAME_IN_USE = B_GENERAL_ERROR_BASE + 8;
    pub const B_TIMED_OUT = B_GENERAL_ERROR_BASE + 9;
    pub const B_INTERRUPTED = B_GENERAL_ERROR_BASE + 10;
    pub const B_WOULD_BLOCK = B_GENERAL_ERROR_BASE + 11;
    pub const B_CANCELED = B_GENERAL_ERROR_BASE + 12;
    pub const B_NO_INIT = B_GENERAL_ERROR_BASE + 13;
    pub const B_NOT_INITIALIZED = B_GENERAL_ERROR_BASE + 13;
    pub const B_BUSY = B_GENERAL_ERROR_BASE + 14;
    pub const B_NOT_ALLOWED = B_GENERAL_ERROR_BASE + 15;
    pub const B_BAD_DATA = B_GENERAL_ERROR_BASE + 16;
    pub const B_DONT_DO_THAT = B_GENERAL_ERROR_BASE + 17;

    pub const B_BAD_IMAGE_ID = B_OS_ERROR_BASE + 0x300;
    pub const B_BAD_ADDRESS = B_OS_ERROR_BASE + 0x301;
    pub const B_NOT_AN_EXECUTABLE = B_OS_ERROR_BASE + 0x302;
    pub const B_MISSING_LIBRARY = B_OS_ERROR_BASE + 0x303;
    pub const B_MISSING_SYMBOL = B_OS_ERROR_BASE + 0x304;
    pub const B_UNKNOWN_EXECUTABLE = B_OS_ERROR_BASE + 0x305;
    pub const B_LEGACY_EXECUTABLE = B_OS_ERROR_BASE + 0x306;

    pub const B_FILE_ERROR = B_STORAGE_ERROR_BASE + 0;
    pub const B_FILE_EXISTS = B_STORAGE_ERROR_BASE + 2;
    pub const B_ENTRY_NOT_FOUND = B_STORAGE_ERROR_BASE + 3;
    pub const B_NAME_TOO_LONG = B_STORAGE_ERROR_BASE + 4;
    pub const B_NOT_A_DIRECTORY = B_STORAGE_ERROR_BASE + 5;
    pub const B_DIRECTORY_NOT_EMPTY = B_STORAGE_ERROR_BASE + 6;
    pub const B_DEVICE_FULL = B_STORAGE_ERROR_BASE + 7;
    pub const B_READ_ONLY_DEVICE = B_STORAGE_ERROR_BASE + 8;
    pub const B_IS_A_DIRECTORY = B_STORAGE_ERROR_BASE + 9;
    pub const B_NO_MORE_FDS = B_STORAGE_ERROR_BASE + 10;
    pub const B_CROSS_DEVICE_LINK = B_STORAGE_ERROR_BASE + 11;
    pub const B_LINK_LIMIT = B_STORAGE_ERROR_BASE + 12;
    pub const B_BUSTED_PIPE = B_STORAGE_ERROR_BASE + 13;
    pub const B_UNSUPPORTED = B_STORAGE_ERROR_BASE + 14;
    pub const B_PARTITION_TOO_SMALL = B_STORAGE_ERROR_BASE + 15;
    pub const B_PARTIAL_READ = B_STORAGE_ERROR_BASE + 16;
    pub const B_PARTIAL_WRITE = B_STORAGE_ERROR_BASE + 17;

    SUCCESS = 0,

    @"2BIG" = B_POSIX_ERROR_BASE + 1,
    CHILD = B_POSIX_ERROR_BASE + 2,
    DEADLK = B_POSIX_ERROR_BASE + 3,
    FBIG = B_POSIX_ERROR_BASE + 4,
    MLINK = B_POSIX_ERROR_BASE + 5,
    NFILE = B_POSIX_ERROR_BASE + 6,
    NODEV = B_POSIX_ERROR_BASE + 7,
    NOLCK = B_POSIX_ERROR_BASE + 8,
    NOSYS = B_POSIX_ERROR_BASE + 9,
    NOTTY = B_POSIX_ERROR_BASE + 10,
    NXIO = B_POSIX_ERROR_BASE + 11,
    SPIPE = B_POSIX_ERROR_BASE + 12,
    SRCH = B_POSIX_ERROR_BASE + 13,
    FPOS = B_POSIX_ERROR_BASE + 14,
    SIGPARM = B_POSIX_ERROR_BASE + 15,
    DOM = B_POSIX_ERROR_BASE + 16,
    RANGE = B_POSIX_ERROR_BASE + 17,
    PROTOTYPE = B_POSIX_ERROR_BASE + 18,
    PROTONOSUPPORT = B_POSIX_ERROR_BASE + 19,
    PFNOSUPPORT = B_POSIX_ERROR_BASE + 20,
    AFNOSUPPORT = B_POSIX_ERROR_BASE + 21,
    ADDRINUSE = B_POSIX_ERROR_BASE + 22,
    ADDRNOTAVAIL = B_POSIX_ERROR_BASE + 23,
    NETDOWN = B_POSIX_ERROR_BASE + 24,
    NETUNREACH = B_POSIX_ERROR_BASE + 25,
    NETRESET = B_POSIX_ERROR_BASE + 26,
    CONNABORTED = B_POSIX_ERROR_BASE + 27,
    CONNRESET = B_POSIX_ERROR_BASE + 28,
    ISCONN = B_POSIX_ERROR_BASE + 29,
    NOTCONN = B_POSIX_ERROR_BASE + 30,
    SHUTDOWN = B_POSIX_ERROR_BASE + 31,
    CONNREFUSED = B_POSIX_ERROR_BASE + 32,
    HOSTUNREACH = B_POSIX_ERROR_BASE + 33,
    NOPROTOOPT = B_POSIX_ERROR_BASE + 34,
    NOBUFS = B_POSIX_ERROR_BASE + 35,
    INPROGRESS = B_POSIX_ERROR_BASE + 36,
    ALREADY = B_POSIX_ERROR_BASE + 37,
    ILSEQ = B_POSIX_ERROR_BASE + 38,
    NOMSG = B_POSIX_ERROR_BASE + 39,
    STALE = B_POSIX_ERROR_BASE + 40,
    OVERFLOW = B_POSIX_ERROR_BASE + 41,
    MSGSIZE = B_POSIX_ERROR_BASE + 42,
    OPNOTSUPP = B_POSIX_ERROR_BASE + 43,
    NOTSOCK = B_POSIX_ERROR_BASE + 44,
    HOSTDOWN = B_POSIX_ERROR_BASE + 45,
    BADMSG = B_POSIX_ERROR_BASE + 46,
    CANCELED = B_POSIX_ERROR_BASE + 47,
    DESTADDRREQ = B_POSIX_ERROR_BASE + 48,
    DQUOT = B_POSIX_ERROR_BASE + 49,
    IDRM = B_POSIX_ERROR_BASE + 50,
    MULTIHOP = B_POSIX_ERROR_BASE + 51,
    NODATA = B_POSIX_ERROR_BASE + 52,
    NOLINK = B_POSIX_ERROR_BASE + 53,
    NOSR = B_POSIX_ERROR_BASE + 54,
    NOSTR = B_POSIX_ERROR_BASE + 55,
    NOTSUP = B_POSIX_ERROR_BASE + 56,
    PROTO = B_POSIX_ERROR_BASE + 57,
    TIME = B_POSIX_ERROR_BASE + 58,
    TXTBSY = B_POSIX_ERROR_BASE + 59,
    NOATTR = B_POSIX_ERROR_BASE + 60,
    NOTRECOVERABLE = B_POSIX_ERROR_BASE + 61,
    OWNERDEAD = B_POSIX_ERROR_BASE + 62,

    NOMEM = B_NO_MEMORY,

    ACCES = B_PERMISSION_DENIED,
    INTR = B_INTERRUPTED,
    IO = B_IO_ERROR,
    BUSY = B_BUSY,
    FAULT = B_BAD_ADDRESS,
    TIMEDOUT = B_TIMED_OUT,
    /// Also used for WOULDBLOCK
    AGAIN = B_WOULD_BLOCK,
    BADF = B_FILE_ERROR,
    EXIST = B_FILE_EXISTS,
    INVAL = B_BAD_VALUE,
    NAMETOOLONG = B_NAME_TOO_LONG,
    NOENT = B_ENTRY_NOT_FOUND,
    PERM = B_NOT_ALLOWED,
    NOTDIR = B_NOT_A_DIRECTORY,
    ISDIR = B_IS_A_DIRECTORY,
    NOTEMPTY = B_DIRECTORY_NOT_EMPTY,
    NOSPC = B_DEVICE_FULL,
    ROFS = B_READ_ONLY_DEVICE,
    MFILE = B_NO_MORE_FDS,
    XDEV = B_CROSS_DEVICE_LINK,
    LOOP = B_LINK_LIMIT,
    NOEXEC = B_NOT_AN_EXECUTABLE,
    PIPE = B_BUSTED_PIPE,

    _,
};

pub const status_t = i32;

pub const DirEnt = extern struct {
    /// device
    dev: dev_t,
    /// parent device (only for queries)
    pdev: dev_t,
    /// inode number
    ino: ino_t,
    /// parent inode (only for queries)
    pino: ino_t,
    /// length of this record, not the name
    reclen: u16,
    /// name of the entry (null byte terminated)
    name: [0]u8,
    pub fn getName(dirent: *const DirEnt) [*:0]const u8 {
        return @ptrCast(&dirent.name);
    }
};

// https://github.com/haiku/haiku/blob/2aab5f5f14aeb3f34c3a3d9a9064cc3c0d914bea/headers/posix/netinet/in.h#L122
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
    pub const BLOCK_SOURCE = 14;
    pub const UNBLOCK_SOURCE = 15;
    pub const ADD_SOURCE_MEMBERSHIP = 16;
    pub const DROP_SOURCE_MEMBERSHIP = 17;
    pub const DONTFRAG = 38;
};

// https://github.com/haiku/haiku/blob/2aab5f5f14aeb3f34c3a3d9a9064cc3c0d914bea/headers/posix/netinet/in.h#L150
pub const IPV6 = struct {
    pub const MULTICAST_IF = 24;
    pub const MULTICAST_HOPS = 25;
    pub const MULTICAST_LOOP = 26;
    pub const UNICAST_HOPS = 27;
    pub const JOIN_GROUP = 28;
    pub const LEAVE_GROUP = 29;
    pub const V6ONLY = 30;
    pub const PKTINFO = 31;
    pub const RECVPKTINFO = 32;
    pub const HOPLIMIT = 33;
    pub const RECVHOPLIMIT = 34;
    pub const HOPOPTS = 35;
    pub const DSTOPTS = 36;
    pub const RTHDR = 37;
};

// https://github.com/haiku/haiku/blob/2aab5f5f14aeb3f34c3a3d9a9064cc3c0d914bea/headers/posix/netinet/ip.h#L36
pub const IPTOS = struct {
    pub const RELIABILITY = 0x04;
    pub const THROUGHPUT = 0x08;
    pub const LOWDELAY = 0x10;
};



---
File: /std/c/illumos.zig
---

const builtin = @import("builtin");
const std = @import("../std.zig");
const assert = std.debug.assert;
const SO = std.c.SO;
const fd_t = std.c.fd_t;
const gid_t = std.c.gid_t;
const ino_t = std.c.ino_t;
const mode_t = std.c.mode_t;
const off_t = std.c.off_t;
const pid_t = std.c.pid_t;
const pthread_t = std.c.pthread_t;
const sockaddr = std.c.sockaddr;
const socklen_t = std.c.socklen_t;
const timespec = std.c.timespec;
const uid_t = std.c.uid_t;
const IFNAMESIZE = std.c.IFNAMESIZE;

comptime {
    assert(builtin.os.tag == .illumos); // Prevent access of std.c symbols on wrong OS.
}

pub extern "c" fn pthread_setname_np(thread: pthread_t, name: [*:0]const u8, arg: ?*anyopaque) c_int;
pub extern "c" fn sysconf(sc: c_int) i64;

pub const major_t = u32;
pub const minor_t = u32;
pub const id_t = i32;
pub const taskid_t = id_t;
pub const projid_t = id_t;
pub const poolid_t = id_t;
pub const zoneid_t = id_t;
pub const ctid_t = id_t;

pub const GETCONTEXT = 0;
pub const SETCONTEXT = 1;
pub const GETUSTACK = 2;
pub const SETUSTACK = 3;

pub const POSIX_FADV = struct {
    pub const NORMAL = 0;
    pub const RANDOM = 1;
    pub const SEQUENTIAL = 2;
    pub const WILLNEED = 3;
    pub const DONTNEED = 4;
    pub const NOREUSE = 5;
};

pub const priority = enum(c_int) {
    PROCESS = 0,
    PGRP = 1,
    USER = 2,
    GROUP = 3,
    SESSION = 4,
    LWP = 5,
    TASK = 6,
    PROJECT = 7,
    ZONE = 8,
    CONTRACT = 9,
};

/// Extensions to the ELF auxiliary vector.
pub const AT_SUN = struct {
    /// effective user id
    pub const UID = 2000;
    /// real user id
    pub const RUID = 2001;
    /// effective group id
    pub const GID = 2002;
    /// real group id
    pub const RGID = 2003;
    /// dynamic linker's ELF header
    pub const LDELF = 2004;
    /// dynamic linker's section headers
    pub const LDSHDR = 2005;
    /// name of dynamic linker
    pub const LDNAME = 2006;
    /// large pagesize
    pub const LPAGESZ = 2007;
    /// platform name
    pub const PLATFORM = 2008;
    /// hints about hardware capabilities.
    pub const HWCAP = 2009;
    pub const HWCAP2 = 2023;
    /// flush icache?
    pub const IFLUSH = 2010;
    /// cpu name
    pub const CPU = 2011;
    /// exec() path name in the auxv, null terminated.
    pub const EXECNAME = 2014;
    /// mmu module name
    pub const MMU = 2015;
    /// dynamic linkers data segment
    pub const LDDATA = 2016;
    /// AF_SUN_ flags passed from the kernel
    pub const AUXFLAGS = 2017;
    /// name of the emulation binary for the linker
    pub const EMULATOR = 2018;
    /// name of the brand library for the linker
    pub const BRANDNAME = 2019;
    /// vectors for brand modules.
    pub const BRAND_AUX1 = 2020;
    pub const BRAND_AUX2 = 2021;
    pub const BRAND_AUX3 = 2022;
    pub const BRAND_AUX4 = 2025;
    pub const BRAND_NROOT = 2024;
    /// vector for comm page.
    pub const COMMPAGE = 2026;
    /// information about the x86 FPU.
    pub const FPTYPE = 2027;
    pub const FPSIZE = 2028;
};

/// ELF auxiliary vector flags.
pub const AF_SUN = struct {
    /// tell ld.so.1 to run "secure" and ignore the environment.
    pub const SETUGID = 0x00000001;
    /// hardware capabilities can be verified against AT_SUN_HWCAP
    pub const HWCAPVERIFY = 0x00000002;
    pub const NOPLM = 0x00000004;
};

pub const procfs = struct {
    pub const misc_header = extern struct {
        size: u32,
        type: enum(u32) {
            Pathname,
            Socketname,
            Peersockname,
            SockoptsBoolOpts,
            SockoptLinger,
            SockoptSndbuf,
            SockoptRcvbuf,
            SockoptIpNexthop,
            SockoptIpv6Nexthop,
            SockoptType,
            SockoptTcpCongestion,
            SockfiltersPriv = 14,
        },
    };

    pub const fdinfo = extern struct {
        fd: fd_t,
        mode: mode_t,
        ino: ino_t,
        size: off_t,
        offset: off_t,
        uid: uid_t,
        gid: gid_t,
        dev_major: major_t,
        dev_minor: minor_t,
        special_major: major_t,
        special_minor: minor_t,
        fileflags: i32,
        fdflags: i32,
        locktype: i16,
        lockpid: pid_t,
        locksysid: i32,
        peerpid: pid_t,
        __filler: [25]c_int,
        peername: [15:0]u8,
        misc: [1]u8,
    };
};

pub const SFD = struct {
    pub const CLOEXEC = 0o2000000;
    pub const NONBLOCK = 0o4000;
};

pub const signalfd_siginfo = extern struct {
    signo: u32,
    errno: i32,
    code: i32,
    pid: u32,
    uid: uid_t,
    fd: i32,
    tid: u32, // unused
    band: u32,
    overrun: u32, // unused
    trapno: u32,
    status: i32,
    int: i32, // unused
    ptr: u64, // unused
    utime: u64,
    stime: u64,
    addr: u64,
    __pad: [48]u8,
};

pub const PORT_SOURCE = struct {
    pub const AIO = 1;
    pub const TIMER = 2;
    pub const USER = 3;
    pub const FD = 4;
    pub const ALERT = 5;
    pub const MQ = 6;
    pub const FILE = 7;
};

pub const PORT_ALERT = struct {
    pub const SET = 0x01;
    pub const UPDATE = 0x02;
};

/// User watchable file events.
pub const FILE_EVENT = struct {
    pub const ACCESS = 0x00000001;
    pub const MODIFIED = 0x00000002;
    pub const ATTRIB = 0x00000004;
    pub const DELETE = 0x00000010;
    pub const RENAME_TO = 0x00000020;
    pub const RENAME_FROM = 0x00000040;
    pub const TRUNC = 0x00100000;
    pub const NOFOLLOW = 0x10000000;
    /// The filesystem holding the watched file was unmounted.
    pub const UNMOUNTED = 0x20000000;
    /// Some other file/filesystem got mounted over the watched file/directory.
    pub const MOUNTEDOVER = 0x40000000;

    pub fn isException(event: u32) bool {
        return event & (UNMOUNTED | DELETE | RENAME_TO | RENAME_FROM | MOUNTEDOVER) > 0;
    }
};

pub const port_notify = extern struct {
    /// Bind request(s) to port.
    port: u32,
    /// User defined variable.
    user: ?*void,
};

pub const file_obj = extern struct {
    /// Access time.
    atim: timespec,
    /// Modification time
    mtim: timespec,
    /// Change time
    ctim: timespec,
    __pad: [3]usize,
    name: [*:0]u8,
};

// struct ifreq is marked obsolete, with struct lifreq preferred for interface requests.
// Here we alias lifreq to ifreq to avoid chainging existing code in os and x.os.IPv6.
pub const SIOCGLIFINDEX = IOWR('i', 133, lifreq);

pub const lif_nd_req = extern struct {
    addr: sockaddr.storage,
    state_create: u8,
    state_same_lla: u8,
    state_diff_lla: u8,
    hdw_len: i32,
    flags: i32,
    __pad: i32,
    hdw_addr: [64]u8,
};

pub const lif_ifinfo_req = extern struct {
    maxhops: u8,
    reachtime: u32,
    reachretrans: u32,
    maxmtu: u32,
};

/// IP interface request. See if_tcp(7p) for more info.
pub const lifreq = extern struct {
    // Not actually in a union, but the stdlib expects one for ifreq
    ifrn: extern union {
        /// Interface name, e.g. "lo0", "en0".
        name: [IFNAMESIZE]u8,
    },
    ru1: extern union {
        /// For subnet/token etc.
        addrlen: i32,
        /// Driver's PPA (physical point of attachment).
        ppa: u32,
    },
    /// One of the IFT types, e.g. IFT_ETHER.
    type: u32,
    ifru: extern union {
        /// Address.
        addr: sockaddr.storage,
        /// Other end of a peer-to-peer link.
        dstaddr: sockaddr.storage,
        /// Broadcast address.
        broadaddr: sockaddr.storage,
        /// Address token.
        token: sockaddr.storage,
        /// Subnet prefix.
        subnet: sockaddr.storage,
        /// Interface index.
        ivalue: i32,
        /// Flags for SIOC?LIFFLAGS.
        flags: u64,
        /// Hop count metric
        metric: i32,
        /// Maximum transmission unit
        mtu: u32,
        // Technically [2]i32
        muxid: packed struct(u64) { ip: i32, arp: i32 },
        /// Neighbor reachability determination entries
        nd_req: lif_nd_req,
        /// Link info
        ifinfo_req: lif_ifinfo_req,
        /// Name of the multipath interface group
        groupname: [IFNAMESIZE]u8,
        binding: [IFNAMESIZE]u8,
        /// Zone id associated with this interface.
        zoneid: zoneid_t,
        /// Duplicate address detection state. Either in progress or completed.
        dadstate: u32,
    },
};

const IoCtlCommand = enum(u32) {
    none = 0x20000000, // no parameters
    write = 0x40000000, // copy out parameters
    read = 0x80000000, // copy in parameters
    read_write = 0xc0000000,
};

fn ioImpl(cmd: IoCtlCommand, io_type: u8, nr: u8, comptime IOT: type) i32 {
    const size = @as(u32, @intCast(@as(u8, @truncate(@sizeOf(IOT))))) << 16;
    const t = @as(u32, @intCast(io_type)) << 8;
    return @as(i32, @bitCast(@intFromEnum(cmd) | size | t | nr));
}

pub fn IO(io_type: u8, nr: u8) i32 {
    return ioImpl(.none, io_type, nr, void);
}

pub fn IOR(io_type: u8, nr: u8, comptime IOT: type) i32 {
    return ioImpl(.write, io_type, nr, IOT);
}

pub fn IOW(io_type: u8, nr: u8, comptime IOT: type) i32 {
    return ioImpl(.read, io_type, nr, IOT);
}

pub fn IOWR(io_type: u8, nr: u8, comptime IOT: type) i32 {
    return ioImpl(.read_write, io_type, nr, IOT);
}

// https://github.com/illumos/illumos-gate/blob/608eb926e14f4ba4736b2d59e891335f1cba9e1e/usr/src/uts/common/netinet/in.h#L1141
// (old OpenSolaris is very similar, it's just missing a few more-modern ones, and probably modern Solaris has those)
pub const IP = struct {
    pub const OPTIONS = 1;
    pub const HDRINCL = 2;
    pub const TOS = 3;
    pub const TTL = 4;
    pub const RECVOPTS = 0x5;
    pub const RECVRETOPTS = 0x6;
    pub const RECVDSTADDR = 0x7;
    pub const RETOPTS = 0x8;
    pub const RECVIF = 0x9;
    pub const RECVSLLA = 0xa;
    pub const RECVTTL = 0xb;
    pub const RECVTOS = 0xc;
    pub const MULTICAST_IF = 0x10;
    pub const MULTICAST_TTL = 0x11;
    pub const MULTICAST_LOOP = 0x12;
    pub const ADD_MEMBERSHIP = 0x13;
    pub const DROP_MEMBERSHIP = 0x14;
    pub const BLOCK_SOURCE = 0x15;
    pub const UNBLOCK_SOURCE = 0x16;
    pub const ADD_SOURCE_MEMBERSHIP = 0x17;
    pub const DROP_SOURCE_MEMBERSHIP = 0x18;
    pub const NEXTHOP = 0x19;
    pub const PKTINFO = 0x1a;
    pub const RECVPKTINFO = 0x1a;
    pub const DONTFRAG = 0x1b;
    pub const MINTTL = 0x1c;
    pub const SEC_OPT = 0x22;
    pub const BOUND_IF = 0x41;
    pub const UNSPEC_SRC = 0x42;
    pub const BROADCAST_TTL = 0x43;
    pub const DHCPINIT_IF = 0x45;
    pub const REUSEADDR = 0x104;
    pub const DONTROUTE = 0x105;
    pub const BROADCAST = 0x106;
    // Same namespace, but these are arguments rather than option names
    pub const DEFAULT_MULTICAST_TTL = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
};

// https://github.com/illumos/illumos-gate/blob/608eb926e14f4ba4736b2d59e891335f1cba9e1e/usr/src/uts/common/netinet/in.h#L1192
// (old OpenSolaris is very similar, it's just missing a few more-modern ones, and probably modern Solaris has those)
pub const IPV6 = struct {
    pub const UNICAST_HOPS = 0x5;
    pub const MULTICAST_IF = 0x6;
    pub const MULTICAST_HOPS = 0x7;
    pub const MULTICAST_LOOP = 0x8;
    pub const JOIN_GROUP = 0x9;
    pub const LEAVE_GROUP = 0xa;
    pub const ADD_MEMBERSHIP = 0x9;
    pub const DROP_MEMBERSHIP = 0xa;
    pub const PKTINFO = 0xb;
    pub const HOPLIMIT = 0xc;
    pub const NEXTHOP = 0xd;
    pub const HOPOPTS = 0xe;
    pub const DSTOPTS = 0xf;
    pub const RTHDR = 0x10;
    pub const RTHDRDSTOPTS = 0x11;
    pub const RECVPKTINFO = 0x12;
    pub const RECVHOPLIMIT = 0x13;
    pub const RECVHOPOPTS = 0x14;
    pub const OLD_RECVDSTOPTS = 0x15;
    pub const RECVRTHDR = 0x16;
    pub const RECVRTHDRDSTOPTS = 0x17;
    pub const CHECKSUM = 0x18;
    pub const RECVTCLASS = 0x19;
    pub const USE_MIN_MTU = 0x20;
    pub const DONTFRAG = 0x21;
    pub const SEC_OPT = 0x22;
    pub const SRC_PREFERENCES = 0x23;
    pub const RECVPATHMTU = 0x24;
    pub const PATHMTU = 0x25;
    pub const TCLASS = 0x26;
    pub const V6ONLY = 0x27;
    pub const RECVDSTOPTS = 0x28;
    pub const MINHOPCOUNT = 0x2f;
    pub const BOUND_IF = 0x41;
    pub const UNSPEC_SRC = 0x42;
    // Same namespace, but these are arguments rather than option names
    pub const RTHDR_TYPE_0 = 0;
    pub const PREFER_SRC_HOME = 0x01;
    pub const PREFER_SRC_COA = 0x02;
    pub const PREFER_SRC_PUBLIC = 0x04;
    pub const PREFER_SRC_TMP = 0x08;
    pub const PREFER_SRC_NONCGA = 0x10;
    pub const PREFER_SRC_CGA = 0x20;
    pub const PREFER_SRC_MIPMASK = PREFER_SRC_HOME | PREFER_SRC_COA;
    pub const PREFER_SRC_MIPDEFAULT = PREFER_SRC_HOME;
    pub const PREFER_SRC_TMPMASK = PREFER_SRC_PUBLIC | PREFER_SRC_TMP;
    pub const PREFER_SRC_TMPDEFAULT = PREFER_SRC_PUBLIC;
    pub const PREFER_SRC_CGAMASK = PREFER_SRC_NONCGA | PREFER_SRC_CGA;
    pub const PREFER_SRC_CGADEFAULT = PREFER_SRC_NONCGA;
    pub const PREFER_SRC_MASK = PREFER_SRC_MIPMASK | PREFER_SRC_TMPMASK | PREFER_SRC_CGAMASK;
    pub const PREFER_SRC_DEFAULT = PREFER_SRC_MIPDEFAULT | PREFER_SRC_TMPDEFAULT | PREFER_SRC_CGADEFAULT;
};

// https://github.com/illumos/illumos-gate/blob/608eb926e14f4ba4736b2d59e891335f1cba9e1e/usr/src/uts/common/netinet/ip.h#L64
pub const IPTOS = struct {
    pub const LOWDELAY = 0x10;
    pub const THROUGHPUT = 0x08;
    pub const RELIABILITY = 0x04;
    pub const ECT = 0x02;
    pub const CE = 0x01;
    pub const PREC_NETCONTROL = 0xe0;
    pub const PREC_INTERNETCONTROL = 0xc0;
    pub const PREC_CRITIC_ECP = 0xa0;
    pub const PREC_FLASHOVERRIDE = 0x80;
    pub const PREC_FLASH = 0x60;
    pub const PREC_IMMEDIATE = 0x40;
    pub const PREC_PRIORITY = 0x20;
    pub const PREC_ROUTINE = 0x00;
};



---
File: /std/c/netbsd.zig
---

const std = @import("../std.zig");
const clock_t = std.c.clock_t;
const clockid_t = std.c.clockid_t;
const pid_t = std.c.pid_t;
const pthread_t = std.c.pthread_t;
const sigval_t = std.c.sigval_t;
const uid_t = std.c.uid_t;
const timespec = std.c.timespec;

pub extern "c" fn ptrace(request: c_int, pid: pid_t, addr: ?*anyopaque, data: c_int) c_int;

pub const lwpid_t = i32;

pub extern "c" fn pthread_setname_np(thread: pthread_t, name: [*:0]const u8, arg: ?*anyopaque) c_int;

pub extern "c" fn _lwp_self() lwpid_t;

pub extern "c" fn ___lwp_park60(
    clock_id: clockid_t,
    flags: packed struct(u32) {
        ABSTIME: bool = false,
        unused: u31 = 0,
    },
    ts: ?*timespec,
    unpark: lwpid_t,
    hint: ?*const anyopaque,
    unpark_hint: ?*const anyopaque,
) c_int;

pub extern "c" fn _lwp_unpark(lwp: lwpid_t, hint: ?*const anyopaque) c_int;
pub extern "c" fn _lwp_unpark_all(targets: [*]const lwpid_t, ntargets: usize, hint: ?*const anyopaque) c_int;

pub const TCIFLUSH = 1;
pub const TCOFLUSH = 2;
pub const TCIOFLUSH = 3;
pub const TCOOFF = 1;
pub const TCOON = 2;
pub const TCIOFF = 3;
pub const TCION = 4;

pub const _ksiginfo = extern struct {
    signo: std.c.SIG,
    code: i32,
    errno: i32,
    // 64bit architectures insert 4bytes of padding here, this is done by
    // correctly aligning the reason field
    reason: extern union {
        rt: extern struct {
            pid: pid_t,
            uid: uid_t,
            value: sigval_t,
        },
        child: extern struct {
            pid: pid_t,
            uid: uid_t,
            status: i32,
            utime: clock_t,
            stime: clock_t,
        },
        fault: extern struct {
            addr: *allowzero anyopaque,
            trap: i32,
            trap2: i32,
            trap3: i32,
        },
        poll: extern struct {
            band: i32,
            fd: i32,
        },
        syscall: extern struct {
            sysnum: i32,
            retval: [2]i32,
            @"error": i32,
            args: [8]u64,
        },
        ptrace_state: extern struct {
            pe_report_event: i32,
            option: extern union {
                pe_other_pid: pid_t,
                pe_lwp: lwpid_t,
            },
        },
    } align(@sizeOf(usize)),
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

    // SystemV IPC
    IDRM = 82, // Identifier removed
    NOMSG = 83, // No message of desired type
    OVERFLOW = 84, // Value too large to be stored in data type

    // Wide/multibyte-character handling, ISO/IEC 9899/AMD1:1995
    ILSEQ = 85, // Illegal byte sequence

    // From IEEE Std 1003.1-2001
    // Base, Realtime, Threads or Thread Priority Scheduling option errors
    NOTSUP = 86, // Not supported

    // Realtime option errors
    CANCELED = 87, // Operation canceled

    // Realtime, XSI STREAMS option errors
    BADMSG = 88, // Bad or Corrupt message

    // XSI STREAMS option errors
    NODATA = 89, // No message available
    NOSR = 90, // No STREAM resources
    NOSTR = 91, // Not a STREAM
    TIME = 92, // STREAM ioctl timeout

    // File system extended attribute errors
    NOATTR = 93, // Attribute not found

    // Realtime, XSI STREAMS option errors
    MULTIHOP = 94, // Multihop attempted
    NOLINK = 95, // Link has been severed
    PROTO = 96, // Protocol error

    _,
};

// https://github.com/NetBSD/src/blob/80bf25a5691072d4755e84567ccbdf0729370dea/sys/netinet/in.h#L276
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
    pub const PORTALGO = 18;
    pub const PORTRANGE = 19;
    pub const RECVIF = 20;
    pub const ERRORMTU = 21;
    pub const IPSEC_POLICY = 22;
    pub const RECVTTL = 23;
    pub const MINTTL = 24;
    pub const PKTINFO = 25;
    pub const RECVPKTINFO = 26;
    pub const BINDANY = 27;
    pub const SENDSRCADDR = RECVDSTADDR;
    pub const DEFAULT_MULTICAST_TTL = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const MAX_MEMBERSHIPS = 20;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/NetBSD/src/blob/80bf25a5691072d4755e84567ccbdf0729370dea/sys/netinet6/in6.h#L370
pub const IPV6 = struct {
    pub const UNICAST_HOPS = 4;
    pub const MULTICAST_IF = 9;
    pub const MULTICAST_HOPS = 10;
    pub const MULTICAST_LOOP = 11;
    pub const JOIN_GROUP = 12;
    pub const LEAVE_GROUP = 13;
    pub const PORTRANGE = 14;
    pub const PORTALGO = 17;
    pub const @"2292PKTINFO" = 19;
    pub const @"2292HOPLIMIT" = 20;
    pub const @"2292NEXTHOP" = 21;
    pub const @"2292HOPOPTS" = 22;
    pub const @"2292DSTOPTS" = 23;
    pub const @"2292RTHDR" = 24;
    pub const @"2292PKTOPTIONS" = 25;
    pub const CHECKSUM = 26;
    pub const V6ONLY = 27;
    pub const IPSEC_POLICY = 28;
    pub const FAITH = 29;
    pub const RTHDRDSTOPTS = 35;
    pub const RECVPKTINFO = 36;
    pub const RECVHOPLIMIT = 37;
    pub const RECVRTHDR = 38;
    pub const RECVHOPOPTS = 39;
    pub const RECVDSTOPTS = 40;
    pub const RECVRTHDRDSTOPTS = 41;
    pub const USE_MIN_MTU = 42;
    pub const RECVPATHMTU = 43;
    pub const PATHMTU = 44;
    pub const PKTINFO = 46;
    pub const HOPLIMIT = 47;
    pub const NEXTHOP = 48;
    pub const HOPOPTS = 49;
    pub const DSTOPTS = 50;
    pub const RTHDR = 51;
    pub const RECVTCLASS = 57;
    pub const OTCLASS = 58;
    pub const TCLASS = 61;
    pub const DONTFRAG = 62;
    pub const PREFER_TEMPADDR = 63;
    pub const BINDANY = 64;
    pub const RTHDR_LOOSE = 0;
    pub const RTHDR_STRICT = 1;
    pub const RTHDR_TYPE_0 = 0;
    pub const DEFAULT_MULTICAST_HOPS = 1;
    pub const DEFAULT_MULTICAST_LOOP = 1;
    pub const PORTRANGE_DEFAULT = 0;
    pub const PORTRANGE_HIGH = 1;
    pub const PORTRANGE_LOW = 2;
};

// https://github.com/NetBSD/src/blob/80bf25a5691072d4755e84567ccbdf0729370dea/sys/netinet/ip.h#L140
pub const IPTOS = struct {
    pub const DSCP_CS0 = 0x00;
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
    pub const CLASS_CS0 = 0x00;
    pub const CLASS_CS1 = 0x20;
    pub const CLASS_CS2 = 0x40;
    pub const CLASS_CS3 = 0x60;
    pub const CLASS_CS4 = 0x80;
    pub const CLASS_CS5 = 0xa0;
    pub const CLASS_CS6 = 0xc0;
    pub const CLASS_CS7 = 0xe0;
    pub const CLASS_DEFAULT = CLASS_CS0;
    pub const CLASS_MASK = 0xe0;
    pub fn CLASS(t: anytype) @TypeOf(t) {
        return t & CLASS_MASK;
    }
    pub const DSCP_MASK = 0xfc;
    pub fn DSCP(t: anytype) @TypeOf(t) {
        return t & DSCP_MASK;
    }
    pub const ECN_NOTECT = 0x00;
    pub const ECN_ECT1 = 0x01;
    pub const ECN_ECT0 = 0x02;
    pub const ECN_CE = 0x03;
    pub const ECN_MASK = 0x03;
    pub fn ECN(t: anytype) @TypeOf(t) {
        return t & ECN_MASK;
    }
    pub const ECN_NOT_ECT = 0x00;
    pub const LOWDELAY = 0x10;
    pub const THROUGHPUT = 0x08;
    pub const RELIABILITY = 0x04;
    pub const MINCOST = 0x02;
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
};



---
File: /std/c/openbsd.zig
---

const std = @import("../std.zig");
const assert = std.debug.assert;
const maxInt = std.math.maxInt;
const builtin = @import("builtin");
const caddr_t = std.c.caddr_t;
const iovec = std.posix.iovec;
const iovec_const = std.posix.iovec_const;
const passwd = std.c.passwd;
const timespec = std.c.timespec;
const uid_t = std.c.uid_t;
const pid_t = std.c.pid_t;

comptime {
    assert(builtin.os.tag == .openbsd); // Prevent access of std.c symbols on wrong OS.
}

pub extern "c" fn ptrace(request: c_int, pid: pid_t, addr: caddr_t, data: c_int) c_int;

pub const pthread_spinlock_t = extern struct {
    inner: ?*anyopaque = null,
};

pub extern "c" fn pledge(promises: ?[*:0]const u8, execpromises: ?[*:0]const u8) c_int;
pub extern "c" fn unveil(path: ?[*:0]const u8, permissions: ?[*:0]const u8) c_int;
pub extern "c" fn getthrid() pid_t;

pub const FUTEX = struct {
    pub const WAIT = 1;
    pub const WAKE = 2;
    pub const REQUEUE = 3;
    pub const PRIVATE_FLAG = 128;
};
pub extern "c" fn futex(uaddr: ?*const volatile u32, op: c_int, val: c_int, timeout: ?*const timespec, uaddr2: ?*const volatile u32) c_int;

pub const login_cap_t = extern struct {
    class: ?[*:0]const u8,
    cap: ?[*:0]const u8,
    style: ?[*:0]const u8,
};

pub extern "c" fn login_getclass(class: ?[*:0]const u8) ?*login_cap_t;
pub extern "c" fn login_getstyle(lc: *login_cap_t, style: ?[*:0]const u8, atype: ?[*:0]const u8) ?[*:0]const u8;
pub extern "c" fn login_getcapbool(lc: *login_cap_t, cap: [*:0]const u8, def: c_int) c_int;
pub extern "c" fn login_getcapnum(lc: *login_cap_t, cap: [*:0]const u8, def: i64, err: i64) i64;
pub extern "c" fn login_getcapsize(lc: *login_cap_t, cap: [*:0]const u8, def: i64, err: i64) i64;
pub extern "c" fn login_getcapstr(lc: *login_cap_t, cap: [*:0]const u8, def: [*:0]const u8, err: [*:0]const u8) [*:0]const u8;
pub extern "c" fn login_getcaptime(lc: *login_cap_t, cap: [*:0]const u8, def: i64, err: i64) i64;
pub extern "c" fn login_close(lc: *login_cap_t) void;
pub extern "c" fn setclasscontext(class: [*:0]const u8, flags: c_uint) c_int;
pub extern "c" fn setusercontext(lc: *login_cap_t, pwd: *passwd, uid: uid_t, flags: c_uint) c_int;

pub const auth_session_t = opaque {};

pub extern "c" fn auth_userokay(name: [*:0]const u8, style: ?[*:0]const u8, arg_type: ?[*:0]const u8, password: ?[*:0]const u8) c_int;
pub extern "c" fn auth_approval(as: ?*auth_session_t, ?*login_cap_t, name: ?[*:0]const u8, type: ?[*:0]const u8) c_int;
pub extern "c" fn auth_userchallenge(name: [*:0]const u8, style: ?[*:0]const u8, arg_type: ?[*:0]const u8, chappengep: *?[*:0]const u8) ?*auth_session_t;
pub extern "c" fn auth_userresponse(as: *auth_session_t, response: [*:0]const u8, more: c_int) c_int;
pub extern "c" fn auth_usercheck(name: [*:0]const u8, style: ?[*:0]const u8, arg_type: ?[*:0]const u8, password: ?[*:0]const u8) ?*auth_session_t;
pub extern "c" fn auth_open() ?*auth_session_t;
pub extern "c" fn auth_close(as: *auth_session_t) c_int;
pub extern "c" fn auth_setdata(as: *auth_session_t, ptr: *anyopaque, len: usize) c_int;
pub extern "c" fn auth_setitem(as: *auth_session_t, item: auth_item_t, value: [*:0]const u8) c_int;
pub extern "c" fn auth_getitem(as: *auth_session_t, item: auth_item_t) ?[*:0]const u8;
pub extern "c" fn auth_setoption(as: *auth_session_t, n: [*:0]const u8, v: [*:0]const u8) c_int;
pub extern "c" fn auth_setstate(as: *auth_session_t, s: c_int) void;
pub extern "c" fn auth_getstate(as: *auth_session_t) c_int;
pub extern "c" fn auth_clean(as: *auth_session_t) void;
pub extern "c" fn auth_clrenv(as: *auth_session_t) void;
pub extern "c" fn auth_clroption(as: *auth_session_t, option: [*:0]const u8) void;
pub extern "c" fn auth_clroptions(as: *auth_session_t) void;
pub extern "c" fn auth_setenv(as: *auth_session_t) void;
pub extern "c" fn auth_getvalue(as: *auth_session_t, what: [*:0]const u8) ?[*:0]const u8;
pub extern "c" fn auth_verify(as: ?*auth_session_t, style: ?[*:0]const u8, name: ?[*:0]const u8, ...) ?*auth_session_t;
pub extern "c" fn auth_call(as: *auth_session_t, path: [*:0]const u8, ...) c_int;
pub extern "c" fn auth_challenge(as: *auth_session_t) [*:0]const u8;
pub extern "c" fn auth_check_expire(as: *auth_session_t) i64;
pub extern "c" fn auth_check_change(as: *auth_session_t) i64;
pub extern "c" fn auth_getpwd(as: *auth_session_t) ?*passwd;
pub extern "c" fn auth_setpwd(as: *auth_session_t, pwd: *passwd) c_int;
pub extern "c" fn auth_mkvalue(value: [*:0]const u8) ?[*:0]const u8;
pub extern "c" fn auth_cat(file: [*:0]const u8) c_int;
pub extern "c" fn auth_checknologin(lc: *login_cap_t) void;
// TODO: auth_set_va_list requires zig support for va_list type (#515)

pub extern "c" fn getpwuid_shadow(uid: uid_t) ?*passwd;
pub extern "c" fn getpwnam_shadow(name: [*
```
