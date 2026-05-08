```
n struct {
        magic: u32 = 0x33330003,
        errorcheck: padded_pthread_spin_t = 0,
        ceiling: padded_pthread_spin_t = 0,
        owner: usize = 0,
        waiters: ?*u8 = null,
        recursed: u32 = 0,
        spare2: ?*anyopaque = null,
    },
    .haiku => extern struct {
        flags: u32 = 0,
        lock: i32 = 0,
        unused: i32 = -42,
        owner: i32 = -1,
        owner_count: i32 = 0,
    },
    .illumos => extern struct {
        flag1: u16 = 0,
        flag2: u8 = 0,
        ceiling: u8 = 0,
        type: u16 = 0,
        magic: u16 = 0x4d58,
        lock: u64 = 0,
        data: u64 = 0,
    },
    .fuchsia => extern struct {
        data: [40]u8 align(@alignOf(usize)) = [_]u8{0} ** 40,
    },
    .emscripten => extern struct {
        data: [24]u8 align(4) = [_]u8{0} ** 24,
    },
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L68-L73
    .serenity => extern struct {
        lock: u32 = 0,
        owner: pthread_t = 0,
        level: c_int = 0,
        type: c_int = 0,
    },
    else => void,
};

pub const pthread_cond_t = switch (native_os) {
    .linux => extern struct {
        data: [48]u8 align(@alignOf(usize)) = [_]u8{0} ** 48,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        sig: c_long = 0x3CB0B1BB,
        data: [data_len]u8 = [_]u8{0} ** data_len,
        const data_len = if (@sizeOf(usize) == 8) 40 else 24;
    },
    .freebsd, .dragonfly, .openbsd => extern struct {
        inner: ?*anyopaque = null,
    },
    .hermit => extern struct {
        ptr: usize = maxInt(usize),
    },
    .netbsd => extern struct {
        magic: u32 = 0x55550005,
        lock: pthread_spin_t = 0,
        waiters_first: ?*u8 = null,
        waiters_last: ?*u8 = null,
        mutex: ?*pthread_mutex_t = null,
        private: ?*anyopaque = null,
    },
    .haiku => extern struct {
        flags: u32 = 0,
        unused: i32 = -42,
        mutex: ?*anyopaque = null,
        waiter_count: i32 = 0,
        lock: i32 = 0,
    },
    .illumos => extern struct {
        flag: [4]u8 = [_]u8{0} ** 4,
        type: u16 = 0,
        magic: u16 = 0x4356,
        data: u64 = 0,
    },
    .fuchsia, .emscripten => extern struct {
        data: [48]u8 align(@alignOf(usize)) = [_]u8{0} ** 48,
    },
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L80-L84
    .serenity => extern struct {
        mutex: ?*pthread_mutex_t = null,
        value: u32 = 0,
        clockid: clockid_t = .REALTIME_COARSE,
    },
    else => void,
};

pub const pthread_rwlock_t = switch (native_os) {
    .linux => switch (native_abi) {
        .android, .androideabi => switch (@sizeOf(usize)) {
            4 => extern struct {
                data: [40]u8 align(@alignOf(usize)) = [_]u8{0} ** 40,
            },
            8 => extern struct {
                data: [56]u8 align(@alignOf(usize)) = [_]u8{0} ** 56,
            },
            else => @compileError("impossible pointer size"),
        },
        else => extern struct {
            data: [56]u8 align(@alignOf(usize)) = [_]u8{0} ** 56,
        },
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        sig: c_long = 0x2DA8B3B4,
        data: [192]u8 = [_]u8{0} ** 192,
    },
    .freebsd, .dragonfly, .openbsd => extern struct {
        ptr: ?*anyopaque = null,
    },
    .hermit => extern struct {
        ptr: usize = maxInt(usize),
    },
    .netbsd => extern struct {
        magic: c_uint = 0x99990009,
        interlock: switch (builtin.cpu.arch) {
            .aarch64, .aarch64_be, .m68k, .sparc, .sparc64, .x86, .x86_64 => u8,
            .arm, .armeb, .powerpc => c_int,
            .mips, .mipsel, .mips64, .mips64el => c_uint,
            else => unreachable,
        } = 0,
        rblocked_first: ?*u8 = null,
        rblocked_last: ?*u8 = null,
        wblocked_first: ?*u8 = null,
        wblocked_last: ?*u8 = null,
        nreaders: c_uint = 0,
        owner: ?pthread_t = null,
        private: ?*anyopaque = null,
    },
    .illumos => extern struct {
        readers: i32 = 0,
        type: u16 = 0,
        magic: u16 = 0x5257,
        mutex: pthread_mutex_t = .{},
        readercv: pthread_cond_t = .{},
        writercv: pthread_cond_t = .{},
    },
    .fuchsia => extern struct {
        size: [56]u8 align(@alignOf(usize)) = [_]u8{0} ** 56,
    },
    .emscripten => extern struct {
        size: [32]u8 align(4) = [_]u8{0} ** 32,
    },
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L86
    .serenity => extern struct {
        inner: u64 = 0,
    },
    else => void,
};

pub const pthread_attr_t = switch (native_os) {
    .linux, .emscripten, .dragonfly => extern struct {
        __size: [56]u8,
        __align: c_long,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        __sig: c_long,
        __opaque: [56]u8,
    },
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L75
    .freebsd, .openbsd, .serenity => extern struct {
        inner: ?*anyopaque = null,
    },
    .illumos => extern struct {
        mutexattr: ?*anyopaque = null,
    },
    .netbsd => extern struct {
        magic: u32,
        flags: i32,
        private: ?*anyopaque,
    },
    .haiku => extern struct {
        detach_state: i32,
        sched_priority: i32,
        stack_size: i32,
        guard_size: i32,
        stack_address: ?*anyopaque,
    },
    else => void,
};

pub const pthread_key_t = switch (native_os) {
    .linux, .emscripten => c_uint,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => c_ulong,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L65
    .openbsd, .illumos, .serenity => c_int,
    else => void,
};

pub const padded_pthread_spin_t = switch (native_os) {
    .netbsd => switch (builtin.cpu.arch) {
        .x86, .x86_64 => u32,
        .sparc, .sparc64 => u32,
        else => pthread_spin_t,
    },
    else => void,
};

pub const pthread_spin_t = switch (native_os) {
    .netbsd => switch (builtin.cpu.arch) {
        .aarch64, .aarch64_be => u8,
        .mips, .mipsel, .mips64, .mips64el => u32,
        .powerpc, .powerpc64, .powerpc64le => i32,
        .x86, .x86_64 => u8,
        .arm, .armeb, .thumb, .thumbeb => i32,
        .sparc, .sparc64 => u8,
        .riscv32, .riscv64 => u32,
        else => @compileError("undefined pthread_spin_t for this arch"),
    },
    else => void,
};

pub const sem_t = switch (native_os) {
    .linux, .emscripten => extern struct {
        __size: [4 * @sizeOf(usize)]u8 align(@alignOf(usize)),
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => c_int,
    .freebsd => extern struct {
        _magic: u32,
        _kern: extern struct {
            _count: u32,
            _flags: u32,
        },
        _padding: u32,
    },
    .illumos => extern struct {
        count: u32 = 0,
        type: u16 = 0,
        magic: u16 = 0x534d,
        __pad1: [3]u64 = [_]u64{0} ** 3,
        __pad2: [2]u64 = [_]u64{0} ** 2,
    },
    .openbsd, .netbsd, .dragonfly => ?*opaque {},
    .haiku => extern struct {
        type: i32,
        u: extern union {
            named_sem_id: i32,
            unnamed_sem: i32,
        },
        padding: [2]i32,
    },
    // https://github.com/SerenityOS/serenity/blob/aae106e37b48f2158e68902293df1e4bf7b80c0f/Userland/Libraries/LibC/semaphore.h#L23-L27
    .serenity => extern struct {
        magic: u32,
        value: u32,
        flags: u8,
    },
    else => void,
};

/// Renamed from `kevent` to `Kevent` to avoid conflict with function name.
pub const Kevent = switch (native_os) {
    .netbsd => extern struct {
        ident: usize,
        filter: i32,
        flags: u32,
        fflags: u32,
        data: i64,
        udata: usize,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        ident: usize,
        filter: i16,
        flags: u16,
        fflags: u32,
        data: isize,
        udata: usize,

        // sys/types.h on macos uses #pragma pack(4) so these checks are
        // to make sure the struct is laid out the same. These values were
        // produced from C code using the offsetof macro.
        comptime {
            assert(@offsetOf(@This(), "ident") == 0);
            assert(@offsetOf(@This(), "filter") == 8);
            assert(@offsetOf(@This(), "flags") == 10);
            assert(@offsetOf(@This(), "fflags") == 12);
            assert(@offsetOf(@This(), "data") == 16);
            assert(@offsetOf(@This(), "udata") == 24);
        }
    },
    .freebsd => extern struct {
        /// Identifier for this event.
        ident: usize,
        /// Filter for event.
        filter: i16,
        /// Action flags for kqueue.
        flags: u16,
        /// Filter flag value.
        fflags: u32,
        /// Filter data value.
        data: i64,
        /// Opaque user data identifier.
        udata: usize,
        /// Future extensions.
        _ext: [4]u64 = [_]u64{0} ** 4,
    },
    .dragonfly => extern struct {
        ident: usize,
        filter: c_short,
        flags: c_ushort,
        fflags: c_uint,
        data: isize,
        udata: usize,
    },
    .openbsd => extern struct {
        ident: usize,
        filter: c_short,
        flags: u16,
        fflags: c_uint,
        data: i64,
        udata: usize,
    },
    else => void,
};

pub const port_t = switch (native_os) {
    .illumos => c_int,
    else => void,
};

pub const port_event = switch (native_os) {
    .illumos => extern struct {
        events: u32,
        /// Event source.
        source: u16,
        __pad: u16,
        /// Source-specific object.
        object: ?*anyopaque,
        /// User cookie.
        cookie: ?*anyopaque,
    },
    else => void,
};

pub const AT = switch (native_os) {
    .linux => linux.AT,
    .windows => struct {
        /// Remove directory instead of unlinking file
        pub const REMOVEDIR = 0x200;
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        pub const FDCWD = -2;
        /// Use effective ids in access check
        pub const EACCESS = 0x0010;
        /// Act on the symlink itself not the target
        pub const SYMLINK_NOFOLLOW = 0x0020;
        /// Act on target of symlink
        pub const SYMLINK_FOLLOW = 0x0040;
        /// Path refers to directory
        pub const REMOVEDIR = 0x0080;
    },
    .freebsd => struct {
        /// Magic value that specify the use of the current working directory
        /// to determine the target of relative file paths in the openat() and
        /// similar syscalls.
        pub const FDCWD = -100;
        /// Check access using effective user and group ID
        pub const EACCESS = 0x0100;
        /// Do not follow symbolic links
        pub const SYMLINK_NOFOLLOW = 0x0200;
        /// Follow symbolic link
        pub const SYMLINK_FOLLOW = 0x0400;
        /// Remove directory instead of file
        pub const REMOVEDIR = 0x0800;
        /// Fail if not under dirfd
        pub const BENEATH = 0x1000;
    },
    .netbsd => struct {
        /// Magic value that specify the use of the current working directory
        /// to determine the target of relative file paths in the openat() and
        /// similar syscalls.
        pub const FDCWD = -100;
        /// Check access using effective user and group ID
        pub const EACCESS = 0x0100;
        /// Do not follow symbolic links
        pub const SYMLINK_NOFOLLOW = 0x0200;
        /// Follow symbolic link
        pub const SYMLINK_FOLLOW = 0x0400;
        /// Remove directory instead of file
        pub const REMOVEDIR = 0x0800;
    },
    .dragonfly => struct {
        pub const FDCWD = -328243;
        pub const SYMLINK_NOFOLLOW = 1;
        pub const REMOVEDIR = 2;
        pub const EACCESS = 4;
        pub const SYMLINK_FOLLOW = 8;
    },
    .openbsd => struct {
        /// Magic value that specify the use of the current working directory
        /// to determine the target of relative file paths in the openat() and
        /// similar syscalls.
        pub const FDCWD = -100;
        /// Check access using effective user and group ID
        pub const EACCESS = 0x01;
        /// Do not follow symbolic links
        pub const SYMLINK_NOFOLLOW = 0x02;
        /// Follow symbolic link
        pub const SYMLINK_FOLLOW = 0x04;
        /// Remove directory instead of file
        pub const REMOVEDIR = 0x08;
    },
    .haiku => struct {
        pub const FDCWD = -1;
        pub const SYMLINK_NOFOLLOW = 0x01;
        pub const SYMLINK_FOLLOW = 0x02;
        pub const REMOVEDIR = 0x04;
        pub const EACCESS = 0x08;
    },
    .illumos => struct {
        /// Magic value that specify the use of the current working directory
        /// to determine the target of relative file paths in the openat() and
        /// similar syscalls.
        pub const FDCWD: fd_t = @bitCast(@as(u32, 0xffd19553));
        /// Do not follow symbolic links
        pub const SYMLINK_NOFOLLOW = 0x1000;
        /// Follow symbolic link
        pub const SYMLINK_FOLLOW = 0x2000;
        /// Remove directory instead of file
        pub const REMOVEDIR = 0x1;
        pub const TRIGGER = 0x2;
        /// Check access using effective user and group ID
        pub const EACCESS = 0x4;
    },
    .emscripten => struct {
        pub const FDCWD = -100;
        pub const SYMLINK_NOFOLLOW = 0x100;
        pub const REMOVEDIR = 0x200;
        pub const SYMLINK_FOLLOW = 0x400;
        pub const NO_AUTOMOUNT = 0x800;
        pub const EMPTY_PATH = 0x1000;
        pub const STATX_SYNC_TYPE = 0x6000;
        pub const STATX_SYNC_AS_STAT = 0x0000;
        pub const STATX_FORCE_SYNC = 0x2000;
        pub const STATX_DONT_SYNC = 0x4000;
        pub const RECURSIVE = 0x8000;
    },
    .wasi => struct {
        // Match `AT_*` constants in lib/libc/include/wasm-wasi-musl/__header_fcntl.h
        pub const EACCESS = 0x0;
        pub const SYMLINK_NOFOLLOW = 0x1;
        pub const SYMLINK_FOLLOW = 0x2;
        pub const REMOVEDIR = 0x4;
        /// When linking libc, we follow their convention and use -2 for current working directory.
        /// However, without libc, Zig does a different convention: it assumes the
        /// current working directory is the first preopen. This behavior can be
        /// overridden with a public function called `wasi_cwd` in the root source
        /// file.
        pub const FDCWD: fd_t = if (builtin.link_libc) -2 else 3;
    },
    // https://github.com/SerenityOS/serenity/blob/2808b0376406a40e31293bb3bcb9170374e90506/Kernel/API/POSIX/fcntl.h#L49-L52
    .serenity => struct {
        pub const FDCWD = -100;
        pub const SYMLINK_NOFOLLOW = 0x100;
        pub const REMOVEDIR = 0x200;
        pub const EACCESS = 0x400;
    },
    else => void,
};

pub const O = switch (native_os) {
    .linux => linux.O,
    .emscripten => packed struct(u32) {
        ACCMODE: std.posix.ACCMODE = .RDONLY,
        _2: u4 = 0,
        CREAT: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        TRUNC: bool = false,
        APPEND: bool = false,
        NONBLOCK: bool = false,
        DSYNC: bool = false,
        ASYNC: bool = false,
        DIRECT: bool = false,
        LARGEFILE: bool = false,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        NOATIME: bool = false,
        CLOEXEC: bool = false,
        SYNC: bool = false,
        PATH: bool = false,
        /// This is typically invalid without also setting `DIRECTORY`.
        TMPFILE: bool = false,
        _: u9 = 0,
    },
    .wasi => packed struct(u32) {
        // Match `O_*` bits from lib/libc/include/wasm-wasi-musl/__header_fcntl.h
        APPEND: bool = false,
        DSYNC: bool = false,
        NONBLOCK: bool = false,
        RSYNC: bool = false,
        SYNC: bool = false,
        _5: u7 = 0,
        CREAT: bool = false,
        DIRECTORY: bool = false,
        EXCL: bool = false,
        TRUNC: bool = false,
        _16: u8 = 0,
        NOFOLLOW: bool = false,
        EXEC: bool = false,
        read: bool = false,
        SEARCH: bool = false,
        write: bool = false,
        // O_CLOEXEC, O_TTY_ININT, O_NOCTTY are 0 in wasi-musl, so they're silently
        // ignored in C code.  Thus no mapping in Zig.
        _: u3 = 0,
    },
    .illumos => packed struct(u32) {
        ACCMODE: std.posix.ACCMODE = .RDONLY,
        NDELAY: bool = false,
        APPEND: bool = false,
        SYNC: bool = false,
        _5: u1 = 0,
        DSYNC: bool = false,
        NONBLOCK: bool = false,
        CREAT: bool = false,
        TRUNC: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        _12: u1 = 0,
        LARGEFILE: bool = false,
        XATTR: bool = false,
        RSYNC: bool = false,
        _16: u1 = 0,
        NOFOLLOW: bool = false,
        NOLINKS: bool = false,
        _19: u2 = 0,
        SEARCH: bool = false,
        EXEC: bool = false,
        CLOEXEC: bool = false,
        DIRECTORY: bool = false,
        DIRECT: bool = false,
        _: u6 = 0,
    },
    .netbsd => packed struct(u32) {
        ACCMODE: std.posix.ACCMODE = .RDONLY,
        NONBLOCK: bool = false,
        APPEND: bool = false,
        SHLOCK: bool = false,
        EXLOCK: bool = false,
        ASYNC: bool = false,
        SYNC: bool = false,
        NOFOLLOW: bool = false,
        CREAT: bool = false,
        TRUNC: bool = false,
        EXCL: bool = false,
        _12: u3 = 0,
        NOCTTY: bool = false,
        DSYNC: bool = false,
        RSYNC: bool = false,
        ALT_IO: bool = false,
        DIRECT: bool = false,
        _20: u1 = 0,
        DIRECTORY: bool = false,
        CLOEXEC: bool = false,
        SEARCH: bool = false,
        _: u8 = 0,
    },
    .openbsd => packed struct(u32) {
        ACCMODE: std.posix.ACCMODE = .RDONLY,
        NONBLOCK: bool = false,
        APPEND: bool = false,
        SHLOCK: bool = false,
        EXLOCK: bool = false,
        ASYNC: bool = false,
        SYNC: bool = false,
        NOFOLLOW: bool = false,
        CREAT: bool = false,
        TRUNC: bool = false,
        EXCL: bool = false,
        _12: u3 = 0,
        NOCTTY: bool = false,
        CLOEXEC: bool = false,
        DIRECTORY: bool = false,
        _: u14 = 0,
    },
    .haiku => packed struct(u32) {
        ACCMODE: std.posix.ACCMODE = .RDONLY,
        _2: u4 = 0,
        CLOEXEC: bool = false,
        NONBLOCK: bool = false,
        EXCL: bool = false,
        CREAT: bool = false,
        TRUNC: bool = false,
        APPEND: bool = false,
        NOCTTY: bool = false,
        NOTRAVERSE: bool = false,
        _14: u2 = 0,
        SYNC: bool = false,
        RSYNC: bool = false,
        DSYNC: bool = false,
        NOFOLLOW: bool = false,
        DIRECT: bool = false,
        DIRECTORY: bool = false,
        _: u10 = 0,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => packed struct(u32) {
        ACCMODE: std.posix.ACCMODE = .RDONLY,
        NONBLOCK: bool = false,
        APPEND: bool = false,
        SHLOCK: bool = false,
        EXLOCK: bool = false,
        ASYNC: bool = false,
        SYNC: bool = false,
        NOFOLLOW: bool = false,
        CREAT: bool = false,
        TRUNC: bool = false,
        EXCL: bool = false,
        RESOLVE_BENEATH: bool = false,
        _13: u2 = 0,
        EVTONLY: bool = false,
        _16: u1 = 0,
        NOCTTY: bool = false,
        _18: u2 = 0,
        DIRECTORY: bool = false,
        SYMLINK: bool = false,
        DSYNC: bool = false,
        _23: u1 = 0,
        CLOEXEC: bool = false,
        _25: u4 = 0,
        ALERT: bool = false,
        _30: u1 = 0,
        POPUP: bool = false,
    },
    .dragonfly => packed struct(u32) {
        ACCMODE: std.posix.ACCMODE = .RDONLY,
        NONBLOCK: bool = false,
        APPEND: bool = false,
        SHLOCK: bool = false,
        EXLOCK: bool = false,
        ASYNC: bool = false,
        SYNC: bool = false,
        NOFOLLOW: bool = false,
        CREAT: bool = false,
        TRUNC: bool = false,
        EXCL: bool = false,
        _12: u3 = 0,
        NOCTTY: bool = false,
        DIRECT: bool = false,
        CLOEXEC: bool = false,
        FBLOCKING: bool = false,
        FNONBLOCKING: bool = false,
        FAPPEND: bool = false,
        FOFFSET: bool = false,
        FSYNCWRITE: bool = false,
        FASYNCWRITE: bool = false,
        _24: u3 = 0,
        DIRECTORY: bool = false,
        _: u4 = 0,
    },
    .freebsd => packed struct(u32) {
        ACCMODE: std.posix.ACCMODE = .RDONLY,
        NONBLOCK: bool = false,
        APPEND: bool = false,
        SHLOCK: bool = false,
        EXLOCK: bool = false,
        ASYNC: bool = false,
        SYNC: bool = false,
        NOFOLLOW: bool = false,
        CREAT: bool = false,
        TRUNC: bool = false,
        EXCL: bool = false,
        _12: u3 = 0,
        NOCTTY: bool = false,
        DIRECT: bool = false,
        DIRECTORY: bool = false,
        EXEC: bool = false,
        TTY_INIT: bool = false,
        CLOEXEC: bool = false,
        VERIFY: bool = false,
        PATH: bool = false,
        RESOLVE_BENEATH: bool = false,
        DSYNC: bool = false,
        EMPTY_PATH: bool = false,
        XATTR: bool = false,
        CLOFORK: bool = false,
        _28: u4 = 0,
    },
    // https://github.com/SerenityOS/serenity/blob/2808b0376406a40e31293bb3bcb9170374e90506/Kernel/API/POSIX/fcntl.h#L28-L43
    .serenity => packed struct(c_int) {
        ACCMODE: std.posix.ACCMODE = .NONE,
        EXEC: bool = false,
        CREAT: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        TRUNC: bool = false,
        APPEND: bool = false,
        NONBLOCK: bool = false,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        CLOEXEC: bool = false,
        DIRECT: bool = false,
        SYNC: bool = false,
        _: std.meta.Int(.unsigned, @bitSizeOf(c_int) - 14) = 0,
    },
    else => void,
};

pub const MAP = switch (native_os) {
    .linux => linux.MAP,
    .emscripten => packed struct(u32) {
        TYPE: enum(u4) {
            SHARED = 0x01,
            PRIVATE = 0x02,
            SHARED_VALIDATE = 0x03,
        },
        FIXED: bool = false,
        ANONYMOUS: bool = false,
        _6: u2 = 0,
        GROWSDOWN: bool = false,
        _9: u2 = 0,
        DENYWRITE: bool = false,
        EXECUTABLE: bool = false,
        LOCKED: bool = false,
        NORESERVE: bool = false,
        POPULATE: bool = false,
        NONBLOCK: bool = false,
        STACK: bool = false,
        HUGETLB: bool = false,
        SYNC: bool = false,
        FIXED_NOREPLACE: bool = false,
        _: u11 = 0,
    },
    .illumos => packed struct(u32) {
        TYPE: enum(u4) {
            SHARED = 0x01,
            PRIVATE = 0x02,
        },
        FIXED: bool = false,
        RENAME: bool = false,
        NORESERVE: bool = false,
        @"32BIT": bool = false,
        ANONYMOUS: bool = false,
        ALIGN: bool = false,
        TEXT: bool = false,
        INITDATA: bool = false,
        _: u20 = 0,
    },
    .netbsd => packed struct(u32) {
        TYPE: enum(u2) {
            SHARED = 0x01,
            PRIVATE = 0x02,
        },
        REMAPDUP: bool = false,
        _3: u1 = 0,
        FIXED: bool = false,
        RENAME: bool = false,
        NORESERVE: bool = false,
        INHERIT: bool = false,
        _8: u1 = 0,
        HASSEMAPHORE: bool = false,
        TRYFIXED: bool = false,
        WIRED: bool = false,
        ANONYMOUS: bool = false,
        STACK: bool = false,
        _: u18 = 0,
    },
    .openbsd => packed struct(u32) {
        TYPE: enum(u4) {
            SHARED = 0x01,
            PRIVATE = 0x02,
        },
        FIXED: bool = false,
        _5: u7 = 0,
        ANONYMOUS: bool = false,
        _13: u1 = 0,
        STACK: bool = false,
        CONCEAL: bool = false,
        _: u16 = 0,
    },
    .haiku => packed struct(u32) {
        TYPE: enum(u2) {
            SHARED = 0x01,
            PRIVATE = 0x02,
        },
        FIXED: bool = false,
        ANONYMOUS: bool = false,
        NORESERVE: bool = false,
        _: u27 = 0,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => packed struct(u32) {
        TYPE: enum(u4) {
            SHARED = 0x01,
            PRIVATE = 0x02,
        },
        FIXED: bool = false,
        _5: u1 = 0,
        NORESERVE: bool = false,
        _7: u2 = 0,
        HASSEMAPHORE: bool = false,
        NOCACHE: bool = false,
        JIT: bool = false,
        ANONYMOUS: bool = false,
        _: u19 = 0,
    },
    .dragonfly => packed struct(u32) {
        TYPE: enum(u4) {
            SHARED = 0x01,
            PRIVATE = 0x02,
        },
        FIXED: bool = false,
        RENAME: bool = false,
        NORESERVE: bool = false,
        INHERIT: bool = false,
        NOEXTEND: bool = false,
        HASSEMAPHORE: bool = false,
        STACK: bool = false,
        NOSYNC: bool = false,
        ANONYMOUS: bool = false,
        VPAGETABLE: bool = false,
        _14: u2 = 0,
        TRYFIXED: bool = false,
        NOCORE: bool = false,
        SIZEALIGN: bool = false,
        _: u13 = 0,
    },
    .freebsd => packed struct(u32) {
        TYPE: enum(u4) {
            SHARED = 0x01,
            PRIVATE = 0x02,
        },
        FIXED: bool = false,
        _5: u5 = 0,
        STACK: bool = false,
        NOSYNC: bool = false,
        ANONYMOUS: bool = false,
        GUARD: bool = false,
        EXCL: bool = false,
        _15: u2 = 0,
        NOCORE: bool = false,
        PREFAULT_READ: bool = false,
        @"32BIT": bool = false,
        _: u12 = 0,
    },
    // https://github.com/SerenityOS/serenity/blob/6d59d4d3d9e76e39112842ec487840828f1c9bfe/Kernel/API/POSIX/sys/mman.h#L16-L26
    .serenity => packed struct(c_int) {
        TYPE: enum(u4) {
            SHARED = 0x01,
            PRIVATE = 0x02,
        },
        FIXED: bool = false,
        ANONYMOUS: bool = false,
        STACK: bool = false,
        NORESERVE: bool = false,
        RANDOMIZED: bool = false,
        PURGEABLE: bool = false,
        FIXED_NOREPLACE: bool = false,
        _: std.meta.Int(.unsigned, @bitSizeOf(c_int) - 11) = 0,
    },
    else => void,
};

pub const MREMAP = switch (native_os) {
    .linux => linux.MREMAP,
    else => void,
};

/// Used by libc to communicate failure. Not actually part of the underlying syscall.
pub const MAP_FAILED: *anyopaque = @ptrFromInt(maxInt(usize));

pub const cc_t = u8;

/// Indices into the `cc` array in the `termios` struct.
pub const V = switch (native_os) {
    .linux => linux.V,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .netbsd, .openbsd => enum {
        EOF,
        EOL,
        EOL2,
        ERASE,
        WERASE,
        KILL,
        REPRINT,
        reserved,
        INTR,
        QUIT,
        SUSP,
        DSUSP,
        START,
        STOP,
        LNEXT,
        DISCARD,
        MIN,
        TIME,
        STATUS,
    },
    .freebsd => enum {
        EOF,
        EOL,
        EOL2,
        ERASE,
        WERASE,
        KILL,
        REPRINT,
        ERASE2,
        INTR,
        QUIT,
        SUSP,
        DSUSP,
        START,
        STOP,
        LNEXT,
        DISCARD,
        MIN,
        TIME,
        STATUS,
    },
    .haiku => enum {
        INTR,
        QUIT,
        ERASE,
        KILL,
        EOF,
        EOL,
        EOL2,
        SWTCH,
        START,
        STOP,
        SUSP,
    },
    .illumos => enum {
        INTR,
        QUIT,
        ERASE,
        KILL,
        EOF,
        EOL,
        EOL2,
        SWTCH,
        START,
        STOP,
        SUSP,
        DSUSP,
        REPRINT,
        DISCARD,
        WERASE,
        LNEXT,
        STATUS,
        ERASE2,
    },
    .emscripten, .wasi => enum {
        INTR,
        QUIT,
        ERASE,
        KILL,
        EOF,
        TIME,
        MIN,
        SWTC,
        START,
        STOP,
        SUSP,
        EOL,
        REPRINT,
        DISCARD,
        WERASE,
        LNEXT,
        EOL2,
    },
    // https://github.com/SerenityOS/serenity/blob/d277cdfd4c7ed21d5248a83217ae03b9f890c3c8/Kernel/API/POSIX/termios.h#L32-L49
    .serenity => enum {
        INTR,
        QUIT,
        ERASE,
        KILL,
        EOF,
        TIME,
        MIN,
        SWTC,
        START,
        STOP,
        SUSP,
        EOL,
        REPRINT,
        DISCARD,
        WERASE,
        LNEXT,
        EOL2,
        INFO,
    },
    else => void,
};

pub const NCCS = switch (native_os) {
    .linux => linux.NCCS,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .freebsd, .netbsd, .openbsd, .dragonfly => 20,
    .haiku => 11,
    .illumos => 19,
    // https://github.com/SerenityOS/serenity/blob/d277cdfd4c7ed21d5248a83217ae03b9f890c3c8/Kernel/API/POSIX/termios.h#L15
    .emscripten, .wasi, .serenity => 32,
    else => void,
};

pub const termios = switch (native_os) {
    .linux => linux.termios,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        iflag: tc_iflag_t,
        oflag: tc_oflag_t,
        cflag: tc_cflag_t,
        lflag: tc_lflag_t,
        cc: [NCCS]cc_t,
        ispeed: speed_t align(8),
        ospeed: speed_t,
    },
    // https://github.com/SerenityOS/serenity/blob/d277cdfd4c7ed21d5248a83217ae03b9f890c3c8/Kernel/API/POSIX/termios.h#L21-L29
    .freebsd, .netbsd, .dragonfly, .openbsd, .serenity => extern struct {
        iflag: tc_iflag_t,
        oflag: tc_oflag_t,
        cflag: tc_cflag_t,
        lflag: tc_lflag_t,
        cc: [NCCS]cc_t,
        ispeed: speed_t,
        ospeed: speed_t,
    },
    .haiku => extern struct {
        iflag: tc_iflag_t,
        oflag: tc_oflag_t,
        cflag: tc_cflag_t,
        lflag: tc_lflag_t,
        line: cc_t,
        ispeed: speed_t,
        ospeed: speed_t,
        cc: [NCCS]cc_t,
    },
    .illumos => extern struct {
        iflag: tc_iflag_t,
        oflag: tc_oflag_t,
        cflag: tc_cflag_t,
        lflag: tc_lflag_t,
        cc: [NCCS]cc_t,
    },
    .emscripten, .wasi => extern struct {
        iflag: tc_iflag_t,
        oflag: tc_oflag_t,
        cflag: tc_cflag_t,
        lflag: tc_lflag_t,
        line: cc_t,
        cc: [NCCS]cc_t,
        ispeed: speed_t,
        ospeed: speed_t,
    },
    else => void,
};

pub const tc_iflag_t = switch (native_os) {
    .linux => linux.tc_iflag_t,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => packed struct(u64) {
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
        _12: u1 = 0,
        IMAXBEL: bool = false,
        IUTF8: bool = false,
        _: u49 = 0,
    },
    .netbsd, .freebsd, .dragonfly => packed struct(u32) {
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
        _12: u1 = 0,
        IMAXBEL: bool = false,
        _: u18 = 0,
    },
    .openbsd => packed struct(u32) {
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
        _: u18 = 0,
    },
    .haiku => packed struct(u32) {
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
        _: u19 = 0,
    },
    .illumos => packed struct(u32) {
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
        _12: u1 = 0,
        IMAXBEL: bool = false,
        _14: u1 = 0,
        DOSMODE: bool = false,
        _: u16 = 0,
    },
    // https://github.com/SerenityOS/serenity/blob/d277cdfd4c7ed21d5248a83217ae03b9f890c3c8/Kernel/API/POSIX/termios.h#L52-L66
    .emscripten, .wasi, .serenity => packed struct(u32) {
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
        _: u17 = 0,
    },
    else => void,
};

pub const tc_oflag_t = switch (native_os) {
    .linux => linux.tc_oflag_t,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => packed struct(u64) {
        OPOST: bool = false,
        ONLCR: bool = false,
        OXTABS: bool = false,
        ONOEOT: bool = false,
        OCRNL: bool = false,
        ONOCR: bool = false,
        ONLRET: bool = false,
        OFILL: bool = false,
        NLDLY: u2 = 0,
        TABDLY: u2 = 0,
        CRDLY: u2 = 0,
        FFDLY: u1 = 0,
        BSDLY: u1 = 0,
        VTDLY: u1 = 0,
        OFDEL: bool = false,
        _: u46 = 0,
    },
    .netbsd => packed struct(u32) {
        OPOST: bool = false,
        ONLCR: bool = false,
        OXTABS: bool = false,
        ONOEOT: bool = false,
        OCRNL: bool = false,
        _5: u1 = 0,
        ONOCR: bool = false,
        ONLRET: bool = false,
        _: u24 = 0,
    },
    .openbsd => packed struct(u32) {
        OPOST: bool = false,
        ONLCR: bool = false,
        OXTABS: bool = false,
        ONOEOT: bool = false,
        OCRNL: bool = false,
        OLCUC: bool = false,
        ONOCR: bool = false,
        ONLRET: bool = false,
        _: u24 = 0,
    },
    .freebsd, .dragonfly => packed struct(u32) {
        OPOST: bool = false,
        ONLCR: bool = false,
        _2: u1 = 0,
        ONOEOT: bool = false,
        OCRNL: bool = false,
        ONOCR: bool = false,
        ONLRET: bool = false,
        _: u25 = 0,
    },
    .illumos => packed struct(u32) {
        OPOST: bool = false,
        OLCUC: bool = false,
        ONLCR: bool = false,
        OCRNL: bool = false,
        ONOCR: bool = false,
        ONLRET: bool = false,
        OFILL: bool = false,
        OFDEL: bool = false,
        NLDLY: u1 = 0,
        CRDLY: u2 = 0,
        TABDLY: u2 = 0,
        BSDLY: u1 = 0,
        VTDLY: u1 = 0,
        FFDLY: u1 = 0,
        PAGEOUT: bool = false,
        WRAP: bool = false,
        _: u14 = 0,
    },
    // https://github.com/SerenityOS/serenity/blob/d277cdfd4c7ed21d5248a83217ae03b9f890c3c8/Kernel/API/POSIX/termios.h#L69-L97
    .haiku, .wasi, .emscripten, .serenity => packed struct(u32) {
        OPOST: bool = false,
        OLCUC: bool = false,
        ONLCR: bool = false,
        OCRNL: bool = false,
        ONOCR: bool = false,
        ONLRET: bool = false,
        OFILL: bool = false,
        OFDEL: bool = false,
        NLDLY: u1 = 0,
        CRDLY: u2 = 0,
        TABDLY: u2 = 0,
        BSDLY: u1 = 0,
        VTDLY: u1 = 0,
        FFDLY: u1 = 0,
        _: u16 = 0,
    },
    else => void,
};

pub const CSIZE = switch (native_os) {
    .linux => linux.CSIZE,
    .haiku => enum(u1) { CS7, CS8 },
    else => enum(u2) { CS5, CS6, CS7, CS8 },
};

pub const tc_cflag_t = switch (native_os) {
    .linux => linux.tc_cflag_t,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => packed struct(u64) {
        CIGNORE: bool = false,
        _1: u5 = 0,
        CSTOPB: bool = false,
        _7: u1 = 0,
        CSIZE: CSIZE = .CS5,
        _10: u1 = 0,
        CREAD: bool = false,
        PARENB: bool = false,
        PARODD: bool = false,
        HUPCL: bool = false,
        CLOCAL: bool = false,
        CCTS_OFLOW: bool = false,
        CRTS_IFLOW: bool = false,
        CDTR_IFLOW: bool = false,
        CDSR_OFLOW: bool = false,
        CCAR_OFLOW: bool = false,
        _: u43 = 0,
    },
    .freebsd => packed struct(u32) {
        CIGNORE: bool = false,
        _1: u7 = 0,
        CSIZE: CSIZE = .CS5,
        CSTOPB: bool = false,
        CREAD: bool = false,
        PARENB: bool = false,
        PARODD: bool = false,
        HUPCL: bool = false,
        CLOCAL: bool = false,
        CCTS_OFLOW: bool = false,
        CRTS_IFLOW: bool = false,
        CDTR_IFLOW: bool = false,
        CDSR_OFLOW: bool = false,
        CCAR_OFLOW: bool = false,
        CNO_RTSDTR: bool = false,
        _: u10 = 0,
    },
    .netbsd => packed struct(u32) {
        CIGNORE: bool = false,
        _1: u7 = 0,
        CSIZE: CSIZE = .CS5,
        CSTOPB: bool = false,
        CREAD: bool = false,
        PARENB: bool = false,
        PARODD: bool = false,
        HUPCL: bool = false,
        CLOCAL: bool = false,
        CRTSCTS: bool = false,
        CDTRCTS: bool = false,
        _18: u2 = 0,
        MDMBUF: bool = false,
        _: u11 = 0,
    },
    .dragonfly => packed struct(u32) {
        CIGNORE: bool = false,
        _1: u7 = 0,
        CSIZE: CSIZE = .CS5,
        CSTOPB: bool = false,
        CREAD: bool = false,
        PARENB: bool = false,
        PARODD: bool = false,
        HUPCL: bool = false,
        CLOCAL: bool = false,
        CCTS_OFLOW: bool = false,
        CRTS_IFLOW: bool = false,
        CDTR_IFLOW: bool = false,
        CDSR_OFLOW: bool = false,
        CCAR_OFLOW: bool = false,
        _: u11 = 0,
    },
    .openbsd => packed struct(u32) {
        CIGNORE: bool = false,
        _1: u7 = 0,
        CSIZE: CSIZE = .CS5,
        CSTOPB: bool = false,
        CREAD: bool = false,
        PARENB: bool = false,
        PARODD: bool = false,
        HUPCL: bool = false,
        CLOCAL: bool = false,
        CRTSCTS: bool = false,
        _17: u3 = 0,
        MDMBUF: bool = false,
        _: u11 = 0,
    },
    .haiku => packed struct(u32) {
        _0: u5 = 0,
        CSIZE: CSIZE = .CS7,
        CSTOPB: bool = false,
        CREAD: bool = false,
        PARENB: bool = false,
        PARODD: bool = false,
        HUPCL: bool = false,
        CLOCAL: bool = false,
        XLOBLK: bool = false,
        CTSFLOW: bool = false,
        RTSFLOW: bool = false,
        _: u17 = 0,
    },
    .illumos => packed struct(u32) {
        _0: u4 = 0,
        CSIZE: CSIZE = .CS5,
        CSTOPB: bool = false,
        CREAD: bool = false,
        PARENB: bool = false,
        PARODD: bool = false,
        HUPCL: bool = false,
        CLOCAL: bool = false,
        RCV1EN: bool = false,
        XMT1EN: bool = false,
        LOBLK: bool = false,
        XCLUDE: bool = false,
        _16: u4 = 0,
        PAREXT: bool = false,
        CBAUDEXT: bool = false,
        CIBAUDEXT: bool = false,
        _23: u7 = 0,
        CRTSXOFF: bool = false,
        CRTSCTS: bool = false,
    },
    .wasi, .emscripten => packed struct(u32) {
        _0: u4 = 0,
        CSIZE: CSIZE = .CS5,
        CSTOPB: bool = false,
        CREAD: bool = false,
        PARENB: bool = false,
        PARODD: bool = false,
        HUPCL: bool = false,
        CLOCAL: bool = false,
        _: u20 = 0,
    },
    // https://github.com/SerenityOS/serenity/blob/d277cdfd4c7ed21d5248a83217ae03b9f890c3c8/Kernel/API/POSIX/termios.h#L131-L141
    .serenity => packed struct(u32) {
        _0: u4 = 0,
        CSIZE: CSIZE = .CS5,
        CSTOPB: bool = false,
        CREAD: bool = false,
        PARENB: bool = false,
        PARODD: bool = false,
        HUPCL: bool = false,
        CLOCAL: bool = false,
        CBAUDEX: bool = false,
        _: u19 = 0,
    },
    else => void,
};

pub const tc_lflag_t = switch (native_os) {
    .linux => linux.tc_lflag_t,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => packed struct(u64) {
        ECHOKE: bool = false,
        ECHOE: bool = false,
        ECHOK: bool = false,
        ECHO: bool = false,
        ECHONL: bool = false,
        ECHOPRT: bool = false,
        ECHOCTL: bool = false,
        ISIG: bool = false,
        ICANON: bool = false,
        ALTWERASE: bool = false,
        IEXTEN: bool = false,
        EXTPROC: bool = false,
        _12: u10 = 0,
        TOSTOP: bool = false,
        FLUSHO: bool = false,
        _24: u1 = 0,
        NOKERNINFO: bool = false,
        _26: u3 = 0,
        PENDIN: bool = false,
        _30: u1 = 0,
        NOFLSH: bool = false,
        _: u32 = 0,
    },
    .netbsd, .freebsd, .dragonfly => packed struct(u32) {
        ECHOKE: bool = false,
        ECHOE: bool = false,
        ECHOK: bool = false,
        ECHO: bool = false,
        ECHONL: bool = false,
        ECHOPRT: bool = false,
        ECHOCTL: bool = false,
        ISIG: bool = false,
        ICANON: bool = false,
        ALTWERASE: bool = false,
        IEXTEN: bool = false,
        EXTPROC: bool = false,
        _12: u10 = 0,
        TOSTOP: bool = false,
        FLUSHO: bool = false,
        _24: u1 = 0,
        NOKERNINFO: bool = false,
        _26: u3 = 0,
        PENDIN: bool = false,
        _30: u1 = 0,
        NOFLSH: bool = false,
    },
    .openbsd => packed struct(u32) {
        ECHOKE: bool = false,
        ECHOE: bool = false,
        ECHOK: bool = false,
        ECHO: bool = false,
        ECHONL: bool = false,
        ECHOPRT: bool = false,
        ECHOCTL: bool = false,
        ISIG: bool = false,
        ICANON: bool = false,
        ALTWERASE: bool = false,
        IEXTEN: bool = false,
        EXTPROC: bool = false,
        _12: u10 = 0,
        TOSTOP: bool = false,
        FLUSHO: bool = false,
        XCASE: bool = false,
        NOKERNINFO: bool = false,
        _26: u3 = 0,
        PENDIN: bool = false,
        _30: u1 = 0,
        NOFLSH: bool = false,
    },
    .haiku => packed struct(u32) {
        ISIG: bool = false,
        ICANON: bool = false,
        XCASE: bool = false,
        ECHO: bool = false,
        ECHOE: bool = false,
        ECHOK: bool = false,
        ECHONL: bool = false,
        NOFLSH: bool = false,
        TOSTOP: bool = false,
        IEXTEN: bool = false,
        ECHOCTL: bool = false,
        ECHOPRT: bool = false,
        ECHOKE: bool = false,
        FLUSHO: bool = false,
        PENDIN: bool = false,
        _: u17 = 0,
    },
    .illumos => packed struct(u32) {
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
        _: u16 = 0,
    },
    .wasi, .emscripten => packed struct(u32) {
        ISIG: bool = false,
        ICANON: bool = false,
        _2: u1 = 0,
        ECHO: bool = false,
        ECHOE: bool = false,
        ECHOK: bool = false,
        ECHONL: bool = false,
        NOFLSH: bool = false,
        TOSTOP: bool = false,
        _9: u6 = 0,
        IEXTEN: bool = false,
        _: u16 = 0,
    },
    // https://github.com/SerenityOS/serenity/blob/d277cdfd4c7ed21d5248a83217ae03b9f890c3c8/Kernel/API/POSIX/termios.h#L168-L189
    .serenity => packed struct(u32) {
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
        PENDIN: bool = false,
        _14: u6 = 0,
        IEXTEN: bool = false,
        EXTPROC: bool = false,
        _: u15 = 0,
    },
    else => void,
};

pub const speed_t = switch (native_os) {
    .linux => linux.speed_t,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .openbsd => enum(u64) {
        B0 = 0,
        B50 = 50,
        B75 = 75,
        B110 = 110,
        B134 = 134,
        B150 = 150,
        B200 = 200,
        B300 = 300,
        B600 = 600,
        B1200 = 1200,
        B1800 = 1800,
        B2400 = 2400,
        B4800 = 4800,
        B9600 = 9600,
        B19200 = 19200,
        B38400 = 38400,
        B7200 = 7200,
        B14400 = 14400,
        B28800 = 28800,
        B57600 = 57600,
        B76800 = 76800,
        B115200 = 115200,
        B230400 = 230400,
    },
    .freebsd, .netbsd => enum(c_uint) {
        B0 = 0,
        B50 = 50,
        B75 = 75,
        B110 = 110,
        B134 = 134,
        B150 = 150,
        B200 = 200,
        B300 = 300,
        B600 = 600,
        B1200 = 1200,
        B1800 = 1800,
        B2400 = 2400,
        B4800 = 4800,
        B9600 = 9600,
        B19200 = 19200,
        B38400 = 38400,
        B7200 = 7200,
        B14400 = 14400,
        B28800 = 28800,
        B57600 = 57600,
        B76800 = 76800,
        B115200 = 115200,
        B230400 = 230400,
        B460800 = 460800,
        B500000 = 500000,
        B921600 = 921600,
        B1000000 = 1000000,
        B1500000 = 1500000,
        B2000000 = 2000000,
        B2500000 = 2500000,
        B3000000 = 3000000,
        B3500000 = 3500000,
        B4000000 = 4000000,
    },
    .dragonfly => enum(c_uint) {
        B0 = 0,
        B50 = 50,
        B75 = 75,
        B110 = 110,
        B134 = 134,
        B150 = 150,
        B200 = 200,
        B300 = 300,
        B600 = 600,
        B1200 = 1200,
        B1800 = 1800,
        B2400 = 2400,
        B4800 = 4800,
        B9600 = 9600,
        B19200 = 19200,
        B38400 = 38400,
        B7200 = 7200,
        B14400 = 14400,
        B28800 = 28800,
        B57600 = 57600,
        B76800 = 76800,
        B115200 = 115200,
        B230400 = 230400,
        B460800 = 460800,
        B921600 = 921600,
    },
    .haiku => enum(u8) {
        B0 = 0x00,
        B50 = 0x01,
        B75 = 0x02,
        B110 = 0x03,
        B134 = 0x04,
        B150 = 0x05,
        B200 = 0x06,
        B300 = 0x07,
        B600 = 0x08,
        B1200 = 0x09,
        B1800 = 0x0A,
        B2400 = 0x0B,
        B4800 = 0x0C,
        B9600 = 0x0D,
        B19200 = 0x0E,
        B38400 = 0x0F,
        B57600 = 0x10,
        B115200 = 0x11,
        B230400 = 0x12,
        B31250 = 0x13,
    },
    .illumos => enum(c_uint) {
        B0 = 0,
        B50 = 1,
        B75 = 2,
        B110 = 3,
        B134 = 4,
        B150 = 5,
        B200 = 6,
        B300 = 7,
        B600 = 8,
        B1200 = 9,
        B1800 = 10,
        B2400 = 11,
        B4800 = 12,
        B9600 = 13,
        B19200 = 14,
        B38400 = 15,
        B57600 = 16,
        B76800 = 17,
        B115200 = 18,
        B153600 = 19,
        B230400 = 20,
        B307200 = 21,
        B460800 = 22,
        B921600 = 23,
        B1000000 = 24,
        B1152000 = 25,
        B1500000 = 26,
        B2000000 = 27,
        B2500000 = 28,
        B3000000 = 29,
        B3500000 = 30,
        B4000000 = 31,
    },
    // https://github.com/SerenityOS/serenity/blob/d277cdfd4c7ed21d5248a83217ae03b9f890c3c8/Kernel/API/POSIX/termios.h#L111-L159
    .emscripten, .wasi, .serenity => enum(u32) {
        B0 = 0o0000000,
        B50 = 0o0000001,
        B75 = 0o0000002,
        B110 = 0o0000003,
        B134 = 0o0000004,
        B150 = 0o0000005,
        B200 = 0o0000006,
        B300 = 0o0000007,
        B600 = 0o0000010,
        B1200 = 0o0000011,
        B1800 = 0o0000012,
        B2400 = 0o0000013,
        B4800 = 0o0000014,
        B9600 = 0o0000015,
        B19200 = 0o0000016,
        B38400 = 0o0000017,

        B57600 = 0o0010001,
        B115200 = 0o0010002,
        B230400 = 0o0010003,
        B460800 = 0o0010004,
        B500000 = 0o0010005,
        B576000 = 0o0010006,
        B921600 = 0o0010007,
        B1000000 = 0o0010010,
        B1152000 = 0o0010011,
        B1500000 = 0o0010012,
        B2000000 = 0o0010013,
        B2500000 = 0o0010014,
        B3000000 = 0o0010015,
        B3500000 = 0o0010016,
        B4000000 = 0o0010017,
    },
    else => void,
};

pub const whence_t = if (native_os == .wasi) std.os.wasi.whence_t else c_int;

pub const sig_atomic_t = switch (native_os) {
    // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/signal.h#L20
    .serenity => u32,
    else => c_int,
};

/// maximum signal number + 1
pub const NSIG = switch (native_os) {
    .linux => linux.NSIG,
    .windows => 23,
    .haiku => 65,
    .netbsd, .freebsd => 32,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.NSIG,
    .illumos => 75,
    // https://github.com/SerenityOS/serenity/blob/046c23f567a17758d762a33bdf04bacbfd088f9f/Kernel/API/POSIX/signal_numbers.h#L42
    .openbsd, .serenity => 33,
    .dragonfly => 64,
    else => {},
};

pub const MINSIGSTKSZ = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => 32768,
    .freebsd => switch (builtin.cpu.arch) {
        .powerpc64, .powerpc64le, .x86, .x86_64 => 2048,
        .arm, .aarch64, .riscv64 => 4096,
        else => @compileError("unsupported arch"),
    },
    .illumos => 2048,
    .haiku, .netbsd => 8192,
    .openbsd => 1 << openbsd.MAX_PAGE_SHIFT,
    // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/signal.h#L58
    .serenity => 4096,
    else => {},
};
pub const SIGSTKSZ = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => 131072,
    .netbsd, .freebsd => MINSIGSTKSZ + 32768,
    .illumos => 8192,
    .haiku => 16384,
    .openbsd => MINSIGSTKSZ + (1 << openbsd.MAX_PAGE_SHIFT) * 4,
    // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/signal.h#L59
    .serenity => 32768,
    else => {},
};
pub const SS = switch (native_os) {
    .linux => linux.SS,
    .openbsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .netbsd, .freebsd => struct {
        pub const ONSTACK = 1;
        pub const DISABLE = 4;
    },
    // https://github.com/SerenityOS/serenity/blob/ec492a1a0819e6239ea44156825c4ee7234ca3db/Kernel/API/POSIX/signal.h#L54-L55
    .haiku, .illumos, .serenity => struct {
        pub const ONSTACK = 0x1;
        pub const DISABLE = 0x2;
    },
    else => void,
};

pub const EV = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        /// add event to kq (implies enable)
        pub const ADD = 0x0001;
        /// delete event from kq
        pub const DELETE = 0x0002;
        /// enable event
        pub const ENABLE = 0x0004;
        /// disable event (not reported)
        pub const DISABLE = 0x0008;
        /// only report one occurrence
        pub const ONESHOT = 0x0010;
        /// clear event state after reporting
        pub const CLEAR = 0x0020;
        /// force immediate event output
        /// ... with or without ERROR
        /// ... use KEVENT_FLAG_ERROR_EVENTS
        ///     on syscalls supporting flags
        pub const RECEIPT = 0x0040;
        /// disable event after reporting
        pub const DISPATCH = 0x0080;
        /// unique kevent per udata value
        pub const UDATA_SPECIFIC = 0x0100;
        /// ... in combination with DELETE
        /// will defer delete until udata-specific
        /// event enabled. EINPROGRESS will be
        /// returned to indicate the deferral
        pub const DISPATCH2 = DISPATCH | UDATA_SPECIFIC;
        /// report that source has vanished
        /// ... only valid with DISPATCH2
        pub const VANISHED = 0x0200;
        /// reserved by system
        pub const SYSFLAGS = 0xF000;
        /// filter-specific flag
        pub const FLAG0 = 0x1000;
        /// filter-specific flag
        pub const FLAG1 = 0x2000;
        /// EOF detected
        pub const EOF = 0x8000;
        /// error, data contains errno
        pub const ERROR = 0x4000;
        pub const POLL = FLAG0;
        pub const OOBAND = FLAG1;
    },
    .dragonfly => struct {
        pub const ADD = 1;
        pub const DELETE = 2;
        pub const ENABLE = 4;
        pub const DISABLE = 8;
        pub const ONESHOT = 16;
        pub const CLEAR = 32;
        pub const RECEIPT = 64;
        pub const DISPATCH = 128;
        pub const NODATA = 4096;
        pub const FLAG1 = 8192;
        pub const ERROR = 16384;
        pub const EOF = 32768;
        pub const SYSFLAGS = 61440;
    },
    .netbsd => struct {
        /// add event to kq (implies enable)
        pub const ADD = 0x0001;
        /// delete event from kq
        pub const DELETE = 0x0002;
        /// enable event
        pub const ENABLE = 0x0004;
        /// disable event (not reported)
        pub const DISABLE = 0x0008;
        /// only report one occurrence
        pub const ONESHOT = 0x0010;
        /// clear event state after reporting
        pub const CLEAR = 0x0020;
        /// force EV_ERROR on success, data=0
        pub const RECEIPT = 0x0040;
        /// disable event after reporting
        pub const DISPATCH = 0x0080;
        /// filter-specific flag
        pub const FLAG1 = 0x2000;
        /// error, data contains errno
        pub const ERROR = 0x4000;
        /// EOF detected
        pub const EOF = 0x8000;
    },
    .freebsd => struct {
        /// add event to kq (implies enable)
        pub const ADD = 0x0001;
        /// delete event from kq
        pub const DELETE = 0x0002;
        /// enable event
        pub const ENABLE = 0x0004;
        /// disable event (not reported)
        pub const DISABLE = 0x0008;
        /// only report one occurrence
        pub const ONESHOT = 0x0010;
        /// clear event state after reporting
        pub const CLEAR = 0x0020;
        /// error, event data contains errno
        pub const ERROR = 0x4000;
        /// force immediate event output
        /// ... with or without ERROR
        /// ... use KEVENT_FLAG_ERROR_EVENTS
        ///     on syscalls supporting flags
        pub const RECEIPT = 0x0040;
        /// disable event after reporting
        pub const DISPATCH = 0x0080;
    },
    .openbsd => struct {
        pub const ADD = 0x0001;
        pub const DELETE = 0x0002;
        pub const ENABLE = 0x0004;
        pub const DISABLE = 0x0008;
        pub const ONESHOT = 0x0010;
        pub const CLEAR = 0x0020;
        pub const RECEIPT = 0x0040;
        pub const DISPATCH = 0x0080;
        pub const FLAG1 = 0x2000;
        pub const ERROR = 0x4000;
        pub const EOF = 0x8000;
    },
    .haiku => struct {
        /// add event to kq (implies enable)
        pub const ADD = 0x0001;
        /// delete event from kq
        pub const DELETE = 0x0002;
        /// enable event
        pub const ENABLE = 0x0004;
        /// disable event (not reported)
        pub const DISABLE = 0x0008;
        /// only report one occurrence
        pub const ONESHOT = 0x0010;
        /// clear event state after reporting
        pub const CLEAR = 0x0020;
        /// force immediate event output
        /// ... with or without ERROR
        /// ... use KEVENT_FLAG_ERROR_EVENTS
        ///     on syscalls supporting flags
        pub const RECEIPT = 0x0040;
        /// disable event after reporting
        pub const DISPATCH = 0x0080;
    },
    else => void,
};

pub const EVFILT = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        pub const READ = -1;
        pub const WRITE = -2;
        /// attached to aio requests
        pub const AIO = -3;
        /// attached to vnodes
        pub const VNODE = -4;
        /// attached to struct proc
        pub const PROC = -5;
        /// attached to struct proc
        pub const SIGNAL = -6;
        /// timers
        pub const TIMER = -7;
        /// Mach portsets
        pub const MACHPORT = -8;
        /// Filesystem events
        pub const FS = -9;
        /// User events
        pub const USER = -10;
        /// Virtual memory events
        pub const VM = -12;
        /// Exception events
        pub const EXCEPT = -15;
        pub const SYSCOUNT = 17;
    },
    .haiku => struct {
        pub const READ = -1;
        pub const WRITE = -2;
        /// attached to aio requests
        pub const AIO = -3;
        /// attached to vnodes
        pub const VNODE = -4;
        /// attached to struct proc
        pub const PROC = -5;
        /// attached to struct proc
        pub const SIGNAL = -6;
        /// timers
        pub const TIMER = -7;
        /// Process descriptors
        pub const PROCDESC = -8;
        /// Filesystem events
        pub const FS = -9;
        pub const LIO = -10;
        /// User events
        pub const USER = -11;
        /// Sendfile events
        pub const SENDFILE = -12;
        pub const EMPTY = -13;
    },
    .dragonfly => struct {
        pub const FS = -10;
        pub const USER = -9;
        pub const EXCEPT = -8;
        pub const TIMER = -7;
        pub const SIGNAL = -6;
        pub const PROC = -5;
        pub const VNODE = -4;
        pub const AIO = -3;
        pub const WRITE = -2;
        pub const READ = -1;
        pub const SYSCOUNT = 10;
        pub const MARKER = 15;
    },
    .netbsd => struct {
        pub const READ = 0;
        pub const WRITE = 1;
        /// attached to aio requests
        pub const AIO = 2;
        /// attached to vnodes
        pub const VNODE = 3;
        /// attached to struct proc
        pub const PROC = 4;
        /// attached to struct proc
        pub const SIGNAL = 5;
        /// timers
        pub const TIMER = 6;
        /// Filesystem events
        pub const FS = 7;
        /// User events
        pub const USER = 8;
        /// Empty filter
        pub const EMPTY = 9;
    },
    .freebsd => struct {
        pub const READ = -1;
        pub const WRITE = -2;
        /// attached to aio requests
        pub const AIO = -3;
        /// attached to vnodes
        pub const VNODE = -4;
        /// attached to struct proc
        pub const PROC = -5;
        /// attached to struct proc
        pub const SIGNAL = -6;
        /// timers
        pub const TIMER = -7;
        /// Process descriptors
        pub const PROCDESC = -8;
        /// Filesystem events
        pub const FS = -9;
        pub const LIO = -10;
        /// User events
        pub const USER = -11;
        /// Sendfile events
        pub const SENDFILE = -12;
        pub const EMPTY = -13;
    },
    .openbsd => struct {
        pub const READ = -1;
        pub const WRITE = -2;
        pub const AIO = -3;
        pub const VNODE = -4;
        pub const PROC = -5;
        pub const SIGNAL = -6;
        pub const TIMER = -7;
        pub const DEVICE = -8;
        pub const EXCEPT = -9;
        pub const USER = -10;
    },
    else => void,
};

pub const NOTE = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        /// On input, TRIGGER causes the event to be triggered for output.
        pub const TRIGGER = 0x01000000;
        /// ignore input fflags
        pub const FFNOP = 0x00000000;
        /// and fflags
        pub const FFAND = 0x40000000;
        /// or fflags
        pub const FFOR = 0x80000000;
        /// copy fflags
        pub const FFCOPY = 0xc0000000;
        /// mask for operations
        pub const FFCTRLMASK = 0xc0000000;
        pub const FFLAGSMASK = 0x00ffffff;
        /// low water mark
        pub const LOWAT = 0x00000001;
        /// OOB data
        pub const OOB = 0x00000002;
        /// vnode was removed
        pub const DELETE = 0x00000001;
        /// data contents changed
        pub const WRITE = 0x00000002;
        /// size increased
        pub const EXTEND = 0x00000004;
        /// attributes changed
        pub const ATTRIB = 0x00000008;
        /// link count changed
        pub const LINK = 0x00000010;
        /// vnode was renamed
        pub const RENAME = 0x00000020;
        /// vnode access was revoked
        pub const REVOKE = 0x00000040;
        /// No specific vnode event: to test for EVFILT_READ      activation
        pub const NONE = 0x00000080;
        /// vnode was unlocked by flock(2)
        pub const FUNLOCK = 0x00000100;
        /// process exited
        pub const EXIT = 0x80000000;
        /// process forked
        pub const FORK = 0x40000000;
        /// process exec'd
        pub const EXEC = 0x20000000;
        /// shared with EVFILT_SIGNAL
        pub const SIGNAL = 0x08000000;
        /// exit status to be returned, valid for child       process only
        pub const EXITSTATUS = 0x04000000;
        /// provide details on reasons for exit
        pub const EXIT_DETAIL = 0x02000000;
        /// mask for signal & exit status
        pub const PDATAMASK = 0x000fffff;
        pub const PCTRLMASK = 0xf0000000;
        pub const EXIT_DETAIL_MASK = 0x00070000;
        pub const EXIT_DECRYPTFAIL = 0x00010000;
        pub const EXIT_MEMORY = 0x00020000;
        pub const EXIT_CSERROR = 0x00040000;
        /// will react on memory          pressure
        pub const VM_PRESSURE = 0x80000000;
        /// will quit on memory       pressure, possibly after cleaning up dirty state
        pub const VM_PRESSURE_TERMINATE = 0x40000000;
        /// will quit immediately on      memory pressure
        pub const VM_PRESSURE_SUDDEN_TERMINATE = 0x20000000;
        /// there was an error
        pub const VM_ERROR = 0x10000000;
        /// data is seconds
        pub const SECONDS = 0x00000001;
        /// data is microseconds
        pub const USECONDS = 0x00000002;
        /// data is nanoseconds
        pub const NSECONDS = 0x00000004;
        /// absolute timeout
        pub const ABSOLUTE = 0x00000008;
        /// ext[1] holds leeway for power aware timers
        pub const LEEWAY = 0x00000010;
        /// system does minimal timer coalescing
        pub const CRITICAL = 0x00000020;
        /// system does maximum timer coalescing
        pub const BACKGROUND = 0x00000040;
        pub const MACH_CONTINUOUS_TIME = 0x00000080;
        /// data is mach absolute time units
        pub const MACHTIME = 0x00000100;
    },
    .dragonfly => struct {
        pub const FFNOP = 0;
        pub const TRACK = 1;
        pub const DELETE = 1;
        pub const LOWAT = 1;
        pub const TRACKERR = 2;
        pub const OOB = 2;
        pub const WRITE = 2;
        pub const EXTEND = 4;
        pub const CHILD = 4;
        pub const ATTRIB = 8;
        pub const LINK = 16;
        pub const RENAME = 32;
        pub const REVOKE = 64;
        pub const PDATAMASK = 1048575;
        pub const FFLAGSMASK = 16777215;
        pub const TRIGGER = 16777216;
        pub const EXEC = 536870912;
        pub const FFAND = 1073741824;
        pub const FORK = 1073741824;
        pub const EXIT = 2147483648;
        pub const FFOR = 2147483648;
        pub const FFCTRLMASK = 3221225472;
        pub const FFCOPY = 3221225472;
        pub const PCTRLMASK = 4026531840;
    },
    .netbsd => struct {
        /// ignore input fflags
        pub const FFNOP = 0x00000000;
        /// AND fflags
        pub const FFAND = 0x40000000;
        /// OR fflags
        pub const FFOR = 0x80000000;
        /// copy fflags
        pub const FFCOPY = 0xc0000000;
        /// masks for operations
        pub const FFCTRLMASK = 0xc0000000;
        pub const FFLAGSMASK = 0x00ffffff;
        /// On input, TRIGGER causes the event to be triggered for output.
        pub const TRIGGER = 0x01000000;
        /// low water mark
        pub const LOWAT = 0x00000001;
        /// vnode was removed
        pub const DELETE = 0x00000001;
        /// data contents changed
        pub const WRITE = 0x00000002;
        /// size increased
        pub const EXTEND = 0x00000004;
        /// attributes changed
        pub const ATTRIB = 0x00000008;
        /// link count changed
        pub const LINK = 0x00000010;
        /// vnode was renamed
        pub const RENAME = 0x00000020;
        /// vnode access was revoked
        pub const REVOKE = 0x00000040;
        /// vnode was opened
        pub const OPEN = 0x00000080;
        /// file closed (no FWRITE)
        pub const CLOSE = 0x00000100;
        /// file closed (FWRITE)
        pub const CLOSE_WRITE = 0x00000200;
        /// file was read
        pub const READ = 0x00000400;
        /// process exited
        pub const EXIT = 0x80000000;
        /// process forked
        pub const FORK = 0x40000000;
        /// process exec'd
        pub const EXEC = 0x20000000;
        /// mask for signal & exit status
        pub const PDATAMASK = 0x000fffff;
        pub const PCTRLMASK = 0xf0000000;
        /// follow across forks
        pub const TRACK = 0x00000001;
        /// could not track child
        pub const TRACKERR = 0x00000002;
        /// am a child process
        pub const CHILD = 0x00000004;
        /// shared with EVFILT_SIGNAL
        pub const SIGNAL = 0x08000000;
        /// data is milliseconds
        pub const MSECONDS = 0x00000000;
        /// data is seconds
        pub const SECONDS = 0x00000001;
        /// data is microseconds
        pub const USECONDS = 0x00000002;
        /// data is nanoseconds
        pub const NSECONDS = 0x00000003;
        /// timeout is absolute
        pub const ABSTIME = 0x00000010;
    },
    .freebsd => struct {
        /// On input, TRIGGER causes the event to be triggered for output.
        pub const TRIGGER = 0x01000000;
        /// ignore input fflags
        pub const FFNOP = 0x00000000;
        /// and fflags
        pub const FFAND = 0x40000000;
        /// or fflags
        pub const FFOR = 0x80000000;
        /// copy fflags
        pub const FFCOPY = 0xc0000000;
        /// mask for operations
        pub const FFCTRLMASK = 0xc0000000;
        pub const FFLAGSMASK = 0x00ffffff;
        /// low water mark
        pub const LOWAT = 0x00000001;
        /// behave like poll()
        pub const FILE_POLL = 0x00000002;
        /// vnode was removed
        pub const DELETE = 0x00000001;
        /// data contents changed
        pub const WRITE = 0x00000002;
        /// size increased
        pub const EXTEND = 0x00000004;
        /// attributes changed
        pub const ATTRIB = 0x00000008;
        /// link count changed
        pub const LINK = 0x00000010;
        /// vnode was renamed
        pub const RENAME = 0x00000020;
        /// vnode access was revoked
        pub const REVOKE = 0x00000040;
        /// vnode was opened
        pub const OPEN = 0x00000080;
        /// file closed, fd did not allow write
        pub const CLOSE = 0x00000100;
        /// file closed, fd did allow write
        pub const CLOSE_WRITE = 0x00000200;
        /// file was read
        pub const READ = 0x00000400;
        /// process exited
        pub const EXIT = 0x80000000;
        /// process forked
        pub const FORK = 0x40000000;
        /// process exec'd
        pub const EXEC = 0x20000000;
        /// mask for signal & exit status
        pub const PDATAMASK = 0x000fffff;
        pub const PCTRLMASK = 0xf0000000;
        /// data is seconds
        pub const SECONDS = 0x00000001;
        /// data is milliseconds
        pub const MSECONDS = 0x00000002;
        /// data is microseconds
        pub const USECONDS = 0x00000004;
        /// data is nanoseconds
        pub const NSECONDS = 0x00000008;
        /// timeout is absolute
        pub const ABSTIME = 0x00000010;
    },
    .openbsd => struct {
        // data/hint flags for EVFILT.{READ|WRITE}
        pub const LOWAT = 0x0001;
        pub const EOF = 0x0002;
        // data/hint flags for EVFILT.EXCEPT and EVFILT.{READ|WRITE}
        pub const OOB = 0x0004;
        // data/hint flags for EVFILT.VNODE
        pub const DELETE = 0x0001;
        pub const WRITE = 0x0002;
        pub const EXTEND = 0x0004;
        pub const ATTRIB = 0x0008;
        pub const LINK = 0x0010;
        pub const RENAME = 0x0020;
        pub const REVOKE = 0x0040;
        pub const TRUNCATE = 0x0080;
        // data/hint flags for EVFILT.PROC
        pub const EXIT = 0x80000000;
        pub const FORK = 0x40000000;
        pub const EXEC = 0x20000000;
        pub const PDATAMASK = 0x000fffff;
        pub const PCTRLMASK = 0xf0000000;
        pub const TRACK = 0x00000001;
        pub const TRACKERR = 0x00000002;
        pub const CHILD = 0x00000004;
        // data/hint flags for EVFILT.DEVICE
        pub const CHANGE = 0x00000001;
        // data/hint flags for EVFILT_USER
        pub const FFNOP = 0x00000000;
        pub const FFAND = 0x40000000;
        pub const FFOR = 0x80000000;
        pub const FFCOPY = 0xc0000000;
        pub const FFCTRLMASK = 0xc0000000;
        pub const FFLAGSMASK = 0x00ffffff;
        pub const TRIGGER = 0x01000000;
    },
    else => void,
};

pub const FUTEX = switch (native_os) {
    .openbsd => openbsd.FUTEX,
    .serenity => serenity.FUTEX,
    else => void,
};

pub const MFD = switch (native_os) {
    .linux => linux.MFD,
    .freebsd => freebsd.MFD,
    else => void,
};

// Unix-like systems
pub const DIR = opaque {};
pub extern "c" fn opendir(pathname: [*:0]const u8) ?*DIR;
pub extern "c" fn fdopendir(fd: c_int) ?*DIR;
pub extern "c" fn rewinddir(dp: *DIR) void;
pub extern "c" fn closedir(dp: *DIR) c_int;
pub extern "c" fn telldir(dp: *DIR) c_long;
pub extern "c" fn seekdir(dp: *DIR, loc: c_long) void;

pub extern "c" fn sigwait(set: ?*sigset_t, sig: ?*c_int) c_int;

pub extern "c" fn alarm(seconds: c_uint) c_uint;

pub const close = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.@"close$NOCANCEL",
    else => private.close,
};

pub const clock_getres = switch (native_os) {
    .netbsd => private.__clock_getres50,
    else => private.clock_getres,
};

pub const clock_gettime = switch (native_os) {
    .netbsd => private.__clock_gettime50,
    else => private.clock_gettime,
};

pub const fstat = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => switch (native_arch) {
        .x86_64 => private.@"fstat$INODE64",
        else => private.fstat,
    },
    .netbsd => private.__fstat50,
    .linux => {},
    else => private.fstat,
};

pub const fstatat = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => switch (native_arch) {
        .x86_64 => private.@"fstatat$INODE64",
        else => private.fstatat,
    },
    .linux => {},
    else => private.fstatat,
};

pub extern "c" fn statx(dirfd: fd_t, path: [*:0]const u8, flags: u32, mask: linux.STATX, buf: *linux.Statx) c_int;

pub extern "c" fn getpwent() ?*passwd;
pub extern "c" fn endpwent() void;
pub extern "c" fn setpwent() void;
pub extern "c" fn getpwnam(name: [*:0]const u8) ?*passwd;
pub extern "c" fn getpwnam_r(name: [*:0]const u8, pwd: *passwd, buf: [*]u8, buflen: usize, result: *?*passwd) c_int;
pub extern "c" fn getpwuid(uid: uid_t) ?*passwd;
pub extern "c" fn getpwuid_r(uid: uid_t, pwd: *passwd, buf: [*]u8, buflen: usize, result: *?*passwd) c_int;
pub extern "c" fn getgrent() ?*group;
pub extern "c" fn setgrent() void;
pub extern "c" fn endgrent() void;
pub extern "c" fn getgrnam(name: [*:0]const u8) ?*passwd;
pub extern "c" fn getgrnam_r(name: [*:0]const u8, grp: *group, buf: [*]u8, buflen: usize, result: *?*group) c_int;
pub extern "c" fn getgrgid(gid: gid_t) ?*group;
pub extern "c" fn getgrgid_r(gid: gid_t, grp: *group, buf: [*]u8, buflen: usize, result: *?*group) c_int;
pub extern "c" fn getrlimit64(resource: rlimit_resource, rlim: *rlimit) c_int;
pub extern "c" fn lseek64(fd: fd_t, offset: i64, whence: c_int) i64;
pub extern "c" fn mmap64(addr: ?*align(page_size) anyopaque, len: usize, prot: PROT, flags: MAP, fd: fd_t, offset: i64) *anyopaque;
pub extern "c" fn open64(path: [*:0]const u8, oflag: O, ...) c_int;
pub extern "c" fn openat64(fd: c_int, path: [*:0]const u8, oflag: O, ...) c_int;
pub extern "c" fn pread64(fd: fd_t, buf: [*]u8, nbyte: usize, offset: i64) isize;
pub extern "c" fn preadv64(fd: c_int, iov: [*]const iovec, iovcnt: c_uint, offset: i64) isize;
pub extern "c" fn pwrite64(fd: fd_t, buf: [*]const u8, nbyte: usize, offset: i64) isize;
pub extern "c" fn pwritev64(fd: c_int, iov: [*]const iovec_const, iovcnt: c_uint, offset: i64) isize;
pub extern "c" fn sendfile64(out_fd: fd_t, in_fd: fd_t, offset: ?*i64, count: usize) isize;
pub extern "c" fn setrlimit64(resource: rlimit_resource, rlim: *const rlimit) c_int;

pub const arc4random_buf = switch (native_os) {
    .linux => if (builtin.abi.isAndroid() or
        (builtin.abi.isGnu() and versionCheck(.{ .major = 2, .minor = 36, .patch = 0 })))
        private.arc4random_buf
    else {},
    .dragonfly, .netbsd, .freebsd, .illumos, .openbsd, .serenity, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => private.arc4random_buf,
    else => {},
};
pub const getentropy = switch (native_os) {
    .linux => if (builtin.abi.isAndroid() and versionCheck(.{ .major = 28, .minor = 0, .patch = 0 })) private.getentropy else {},
    .emscripten => private.getentropy,
    else => {},
};
pub const getrandom = switch (native_os) {
    .freebsd => private.getrandom,
    .linux => if (builtin.abi.isMusl() or
        (builtin.abi.isGnu() and versionCheck(.{ .major = 2, .minor = 25, .patch = 0 })) or
        (builtin.abi.isAndroid() and versionCheck(.{ .major = 28, .minor = 0, .patch = 0 })))
        private.getrandom
    else {},
    else => {},
};

pub extern "c" fn sched_getaffinity(pid: c_int, size: usize, set: *cpu_set_t) c_int;
pub extern "c" fn eventfd(initval: c_uint, flags: c_uint) c_int;

pub extern "c" fn epoll_ctl(epfd: fd_t, op: c_uint, fd: fd_t, event: ?*epoll_event) c_int;
pub extern "c" fn epoll_create1(flags: c_uint) c_int;
pub extern "c" fn epoll_wait(epfd: fd_t, events: [*]epoll_event, maxevents: c_uint, timeout: c_int) c_int;
pub extern "c" fn epoll_pwait(
    epfd: fd_t,
    events: [*]epoll_event,
    maxevents: c_int,
    timeout: c_int,
    sigmask: *const sigset_t,
) c_int;

pub extern "c" fn timerfd_create(clockid: timerfd_clockid_t, flags: c_int) c_int;
pub extern "c" fn timerfd_settime(
    fd: c_int,
    flags: c_int,
    new_value: *const itimerspec,
    old_value: ?*itimerspec,
) c_int;
pub extern "c" fn timerfd_gettime(fd: c_int, curr_value: *itimerspec) c_int;

pub extern "c" fn inotify_init1(flags: c_uint) c_int;
pub extern "c" fn inotify_add_watch(fd: fd_t, pathname: [*:0]const u8, mask: u32) c_int;
pub extern "c" fn inotify_rm_watch(fd: fd_t, wd: c_int) c_int;

pub extern "c" fn fallocate64(fd: fd_t, mode: c_int, offset: off_t, len: off_t) c_int;
pub extern "c" fn fopen64(noalias filename: [*:0]const u8, noalias modes: [*:0]const u8) ?*FILE;
pub extern "c" fn ftruncate64(fd: c_int, length: off_t) c_int;
pub extern "c" fn fallocate(fd: fd_t, mode: c_int, offset: off_t, len: off_t) c_int;
pub const sendfile = switch (native_os) {
    .freebsd => freebsd.sendfile,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.sendfile,
    .linux => private.sendfile,
    else => {},
};
/// See std.elf for constants for this
pub extern "c" fn getauxval(__type: c_ulong) c_ulong;

pub extern "c" fn dl_iterate_phdr(callback: dl_iterate_phdr_callback, data: ?*anyopaque) c_int;

pub const sigaltstack = switch (native_os) {
    .netbsd => private.__sigaltstack14,
    else => private.sigaltstack,
};

pub extern "c" fn memfd_create(name: [*:0]const u8, flags: c_uint) c_int;
pub const pipe2 = switch (native_os) {
    .dragonfly, .emscripten, .netbsd, .freebsd, .illumos, .openbsd, .linux, .serenity => private.pipe2,
    else => {},
};
pub const copy_file_range = switch (native_os) {
    .linux => private.copy_file_range,
    .freebsd => freebsd.copy_file_range,
    else => {},
};

pub extern "c" fn signalfd(fd: fd_t, mask: *const sigset_t, flags: u32) c_int;

pub extern "c" fn prlimit(pid: pid_t, resource: rlimit_resource, new_limit: *const rlimit, old_limit: *rlimit) c_int;
pub extern "c" fn mincore(
    addr: *align(page_size) anyopaque,
    length: usize,
    vec: [*]u8,
) c_int;

pub extern "c" fn madvise(
    addr: *align(page_size) anyopaque,
    length: usize,
    advice: u32,
) c_int;

pub const getdirentries = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => private.__getdirentries64,
    else => private.getdirentries,
};

pub const getdents = switch (native_os) {
    .netbsd => private.__getdents30,
    else => private.getdents,
};

pub const getrusage = switch (native_os) {
    .netbsd => private.__getrusage50,
    else => private.getrusage,
};

pub const gettimeofday = switch (native_os) {
    .netbsd => private.__gettimeofday50,
    else => private.gettimeofday,
};

pub const mlock = switch (native_os) {
    .windows, .wasi => {},
    else => private.mlock,
};

pub const mlock2 = switch (native_os) {
    .linux => private.mlock2,
    else => {},
};

pub const munlock = switch (native_os) {
    .windows, .wasi => {},
    else => private.munlock,
};

pub const mlockall = switch (native_os) {
    .linux, .freebsd, .dragonfly, .netbsd, .openbsd, .illumos => private.mlockall,
    else => {},
};

pub const munlockall = switch (native_os) {
    .linux, .freebsd, .dragonfly, .netbsd, .openbsd, .illumos => private.munlockall,
    else => {},
};

pub const msync = switch (native_os) {
    .netbsd => private.__msync13,
    else => private.msync,
};

pub const nanosleep = switch (native_os) {
    .netbsd => private.__nanosleep50,
    else => private.nanosleep,
};

pub const readdir = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => switch (native_arch) {
        .x86_64 => private.@"readdir$INODE64",
        else => private.readdir,
    },
    .windows => {},
    else => private.readdir,
};

pub const realpath = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => private.@"realpath$DARWIN_EXTSN",
    else => private.realpath,
};

pub const sched_yield = switch (native_os) {
    .netbsd => private.__libc_thr_yield,
    else => private.sched_yield,
};

pub const sigaction = switch (native_os) {
    .netbsd => private.__sigaction14,
    else => private.sigaction,
};

/// Zig's version of SIGRTMIN.  Actually a function.
pub const sigrtmin = switch (native_os) {
    .openbsd => {},
    else => sigrt_private.sigrtmin,
};

/// Zig's version of SIGRTMAX.  Actually a function.
pub const sigrtmax = switch (native_os) {
    .openbsd => {},
    else => sigrt_private.sigrtmax,
};

const sigrt_private = struct {
    pub fn sigrtmin() u8 {
        return switch (native_os) {
            .freebsd => 65,
            .netbsd => 33,
            .illumos => @truncate(sysconf(@intFromEnum(_SC.SIGRT_MIN))),
            else => @truncate(@as(c_uint, @bitCast(private.__libc_current_sigrtmin()))),
        };
    }

    pub fn sigrtmax() u8 {
        return switch (native_os) {
            .freebsd => 126,
            .netbsd => 63,
            .illumos => @truncate(sysconf(@intFromEnum(_SC.SIGRT_MAX))),
            else => @truncate(@as(c_uint, @bitCast(private.__libc_current_sigrtmax()))),
        };
    }
};

pub const sigfillset = switch (native_os) {
    .netbsd => private.__sigfillset14,
    else => private.sigfillset,
};

pub const sigaddset = private.sigaddset;
pub const sigemptyset = switch (native_os) {
    .netbsd => private.__sigemptyset14,
    else => private.sigemptyset,
};
pub const sigdelset = private.sigdelset;
pub const sigismember = private.sigismember;

pub const sigprocmask = switch (native_os) {
    .netbsd => private.__sigprocmask14,
    else => private.sigprocmask,
};

pub const socket = switch (native_os) {
    .netbsd => private.__socket30,
    else => private.socket,
};

pub const socketpair = switch (native_os) {
    // https://devblogs.microsoft.com/commandline/af_unix-comes-to-windows/#unsupported\unavailable:
    .windows => {},
    else => private.socketpair,
};

pub const stat = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => switch (native_arch) {
        .x86_64 => private.@"stat$INODE64",
        else => private.stat,
    },
    else => private.stat,
};

pub const _msize = switch (native_os) {
    .windows => private._msize,
    else => {},
};
pub const malloc_size = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .serenity => private.malloc_size,
    else => {},
};
pub const malloc_usable_size = switch (native_os) {
    .freebsd, .linux, .serenity => private.malloc_usable_size,
    else => {},
};
pub const posix_memalign = switch (native_os) {
    .dragonfly, .netbsd, .freebsd, .illumos, .openbsd, .linux, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .serenity => private.posix_memalign,
    else => {},
};
pub const sysconf = switch (native_os) {
    .illumos => illumos.sysconf,
    else => private.sysconf,
};

pub const sf_hdtr = switch (native_os) {
    .freebsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        headers: ?[*]const iovec_const,
        hdr_cnt: c_int,
        trailers: ?[*]const iovec_const,
        trl_cnt: c_int,
    },
    else => void,
};

pub const flock = switch (native_os) {
    .windows, .wasi => {},
    else => private.flock,
};

pub const futex = switch (native_os) {
    .openbsd => openbsd.futex,
    .serenity => serenity.futex,
    else => {},
};

pub extern "c" var environ: [*:null]?[*:0]u8;

pub extern "c" fn fopen(noalias filename: [*:0]const u8, noalias modes: [*:0]const u8) ?*FILE;
pub extern "c" fn fclose(stream: *FILE) c_int;
pub extern "c" fn fwrite(noalias ptr: [*]const u8, size_of_type: usize, item_count: usize, noalias stream: *FILE) usize;
pub extern "c" fn fread(noalias ptr: [*]u8, size_of_type: usize, item_count: usize, noalias stream: *FILE) usize;

pub extern "c" fn printf(format: [*:0]const u8, ...) c_int;
pub extern "c" fn abort() noreturn;
pub extern "c" fn exit(code: c_int) noreturn;
pub extern "c" fn _Exit(code: c_int) noreturn;
pub extern "c" fn _exit(code: c_int) noreturn;
pub extern "c" fn isatty(fd: fd_t) c_int;
pub extern "c" fn lseek(fd: fd_t, offset: off_t, whence: whence_t) off_t;
pub extern "c" fn open(path: [*:0]const u8, oflag: O, ...) c_int;
pub extern "c" fn openat(fd: c_int, path: [*:0]const u8, oflag: O, ...) c_int;
pub extern "c" fn ftruncate(fd: c_int, length: off_t) c_int;
pub extern "c" fn raise(sig: SIG) c_int;
pub extern "c" fn read(fd: fd_t, buf: [*]u8, nbyte: usize) isize;
pub extern "c" fn readv(fd: c_int, iov: [*]const iovec, iovcnt: c_uint) isize;
pub extern "c" fn pread(fd: fd_t, buf: [*]u8, nbyte: usize, offset: off_t) isize;
pub extern "c" fn preadv(fd: c_int, iov: [*]const iovec, iovcnt: c_uint, offset: off_t) isize;
pub extern "c" fn writev(fd: c_int, iov: [*]const iovec_const, iovcnt: c_uint) isize;
pub extern "c" fn pwritev(fd: c_int, iov: [*]const iovec_const, iovcnt: c_uint, offset: off_t) isize;
pub extern "c" fn write(fd: fd_t, buf: [*]const u8, nbyte: usize) isize;
pub extern "c" fn pwrite(fd: fd_t, buf: [*]const u8, nbyte: usize, offset: off_t) isize;
pub extern "c" fn mmap(addr: ?*align(page_size) anyopaque, len: usize, prot: PROT, flags: MAP, fd: fd_t, offset: off_t) *anyopaque;
pub extern "c" fn munmap(addr: *align(page_size) const anyopaque, len: usize) c_int;
pub extern "c" fn mremap(addr: ?*align(page_size) const anyopaque, old_len: usize, new_len: usize, flags: MREMAP, ...) *anyopaque;
pub extern "c" fn mprotect(addr: *align(page_size) anyopaque, len: usize, prot: PROT) c_int;
pub extern "c" fn link(oldpath: [*:0]const u8, newpath: [*:0]const u8) c_int;
pub extern "c" fn linkat(oldfd: fd_t, oldpath: [*:0]const u8, newfd: fd_t, newpath: [*:0]const u8, flags: c_uint) c_int;
pub extern "c" fn unlink(path: [*:0]const u8) c_int;
pub extern "c" fn unlinkat(dirfd: fd_t, path: [*:0]const u8, flags: c_uint) c_int;
pub extern "c" fn getcwd(buf: [*]u8, size: usize) ?[*]u8;
pub extern "c" fn waitpid(pid: pid_t, status: ?*c_int, options: c_int) pid_t;

pub const wait4 = switch (native_os) {
    .netbsd => private.__wait450,
    else => private.wait4,
};

pub const fork = switch (native_os) {
    .dragonfly,
    .freebsd,
    .linux,
    .driverkit,
    .ios,
    .maccatalyst,
    .macos,
    .tvos,
    .visionos,
    .watchos,
    .netbsd,
    .openbsd,
    .illumos,
    .haiku,
    .serenity,
    => private.fork,
    else => {},
};
pub extern "c" fn access(path: [*:0]const u8, mode: c_uint) c_int;
pub extern "c" fn faccessat(dirfd: fd_t, path: [*:0]const u8, mode: c_uint, flags: c_uint) c_int;
pub extern "c" fn pipe(fds: *[2]fd_t) c_int;
pub extern "c" fn mkdir(path: [*:0]const u8, mode: mode_t) c_int;
pub extern "c" fn mkdirat(dirfd: fd_t, path: [*:0]const u8, mode: mode_t) c_int;
pub extern "c" fn symlink(existing: [*:0]const u8, new: [*:0]const u8) c_int;
pub extern "c" fn symlinkat(oldpath: [*:0]const u8, newdirfd: fd_t, newpath: [*:0]const u8) c_int;
pub extern "c" fn rename(old: [*:0]const u8, new: [*:0]const u8) c_int;
pub extern "c" fn renameat(olddirfd: fd_t, old: [*:0]const u8, newdirfd: fd_t, new: [*:0]const u8) c_int;
pub extern "c" fn chdir(path: [*:0]const u8) c_int;
pub extern "c" fn fchdir(fd: fd_t) c_int;
pub extern "c" fn execve(path: [*:0]const u8, argv: [*:null]const ?[*:0]const u8, envp: [*:null]const ?[*:0]const u8) c_int;
pub extern "c" fn dup(fd: fd_t) c_int;
pub extern "c" fn dup2(old_fd: fd_t, new_fd: fd_t) c_int;
pub extern "c" fn dup3(old: c_int, new: c_int, flags: c_uint) c_int;
pub extern "c" fn readlink(noalias path: [*:0]const u8, noalias buf: [*]u8, bufsize: usize) isize;
pub extern "c" fn readlinkat(dirfd: fd_t, noalias path: [*:0]const u8, noalias buf: [*]u8, bufsize: usize) isize;
pub extern "c" fn chmod(path: [*:0]const u8, mode: mode_t) c_int;
pub extern "c" fn fchmod(fd: fd_t, mode: mode_t) c_int;
pub extern "c" fn fchmodat(fd: fd_t, path: [*:0]const u8, mode: mode_t, flags: c_uint) c_int;
pub extern "c" fn fchown(fd: fd_t, owner: uid_t, group: gid_t) c_int;
pub extern "c" fn fchownat(fd: fd_t, path: [*:0]const u8, owner: uid_t, group: gid_t, flags: c_uint) c_int;
pub extern "c" fn umask(mode: mode_t) mode_t;

pub extern "c" fn rmdir(path: [*:0]const u8) c_int;
pub extern "c" fn getenv(name: [*:0]const u8) ?[*:0]u8;
pub extern "c" fn sysctl(name: [*]const c_int, namelen: c_uint, oldp: ?*anyopaque, oldlenp: ?*usize, newp: ?*anyopaque, newlen: usize) c_int;
pub extern "c" fn sysctlbyname(name: [*:0]const u8, oldp: ?*anyopaque, oldlenp: ?*usize, newp: ?*anyopaque, newlen: usize) c_int;
pub extern "c" fn sysctlnametomib(name: [*:0]const u8, mibp: ?*c_int, sizep: ?*usize) c_int;
pub extern "c" fn tcgetattr(fd: fd_t, termios_p: *termios) c_int;
pub extern "c" fn tcsetattr(fd: fd_t, optional_action: TCSA, termios_p: *const termios) c_int;
pub extern "c" fn fcntl(fd: fd_t, cmd: c_int, ...) c_int;
pub extern "c" fn uname(buf: *utsname) c_int;

pub extern "c" fn gethostname(name: [*]u8, len: usize) c_int;
pub extern "c" fn shutdown(socket: fd_t, how: c_int) c_int;
pub extern "c" fn bind(socket: fd_t, address: ?*const sockaddr, address_len: socklen_t) c_int;
pub extern "c" fn listen(sockfd: fd_t, backlog: c_uint) c_int;
pub extern "c" fn getsockname(sockfd: fd_t, noalias addr: *sockaddr, noalias addrlen: *socklen_t) c_int;
pub extern "c" fn getpeername(sockfd: fd_t, noalias addr: *sockaddr, noalias addrlen: *socklen_t) c_int;
pub extern "c" fn connect(sockfd: fd_t, sock_addr: *const sockaddr, addrlen: socklen_t) c_int;
pub extern "c" fn accept(sockfd: fd_t, noalias addr: ?*sockaddr, noalias addrlen: ?*socklen_t) c_int;
pub extern "c" fn accept4(sockfd: fd_t, noalias addr: ?*sockaddr, noalias addrlen: ?*socklen_t, flags: c_uint) c_int;
pub extern "c" fn getsockopt(sockfd: fd_t, level: i32, optname: u32, noalias optval: ?*anyopaque, noalias optlen: *socklen_t) c_int;
pub extern "c" fn setsockopt(sockfd: fd_t, level: i32, optname: u32, optval: ?*const anyopaque, optlen: socklen_t) c_int;
pub extern "c" fn send(sockfd: fd_t, buf: *const anyopaque, len: usize, flags: u32) isize;
pub extern "c" fn sendto(
    sockfd: fd_t,
    buf: *const anyopaque,
    len: usize,
    flags: u32,
    dest_addr: ?*const sockaddr,
    addrlen: socklen_t,
) isize;
pub extern "c" fn sendmsg(sockfd: fd_t, msg: *const msghdr_const, flags: u32) isize;
pub extern "c" fn sendmmsg(sockfd: fd_t, msgvec: [*]mmsghdr, n: c_uint, flags: u32) c_int;

pub extern "c" fn recv(
    sockfd: fd_t,
    arg1: ?*anyopaque,
    arg2: usize,
    arg3: c_int,
) if (native_os == .windows) c_int else isize;
pub extern "c" fn recvfrom(
    sockfd: fd_t,
    noalias buf: *anyopaque,
    len: usize,
    flags: u32,
    noalias src_addr: ?*sockaddr,
    noalias addrlen: ?*socklen_t,
) if (native_os == .windows) c_int else isize;

pub const recvmsg = switch (native_os) {
    .windows => {},
    else => private.recvmsg,
};

pub extern "c" fn kill(pid: pid_t, sig: SIG) c_int;

pub extern "c" fn setuid(uid: uid_t) c_int;
pub extern "c" fn setgid(gid: gid_t) c_int;
pub extern "c" fn seteuid(euid: uid_t) c_int;
pub extern "c" fn setegid(egid: gid_t) c_int;
pub extern "c" fn setreuid(ruid: uid_t, euid: uid_t) c_int;
pub extern "c" fn setregid(rgid: gid_t, egid: gid_t) c_int;
pub extern "c" fn setresuid(ruid: uid_t, euid: uid_t, suid: uid_t) c_int;
pub extern "c" fn setresgid(rgid: gid_t, egid: gid_t, sgid: gid_t) c_int;
pub extern "c" fn setpgid(pid: pid_t, pgid: pid_t) c_int;
pub extern "c" fn getuid() uid_t;
pub extern "c" fn getgid() gid_t;
pub extern "c" fn geteuid() uid_t;
pub extern "c" fn getegid() gid_t;
pub extern "c" fn getresuid(ruid: *uid_t, euid: *uid_t, suid: *uid_t) c_int;
pub extern "c" fn getresgid(rgid: *gid_t, egid: *gid_t, sgid: *gid_t) c_int;

pub extern "c" fn malloc(usize) ?*anyopaque;
pub extern "c" fn calloc(usize, usize) ?*anyopaque;
pub extern "c" fn realloc(?*anyopaque, usize) ?*anyopaque;
pub extern "c" fn free(?*anyopaque) void;

pub extern "c" fn futimes(fd: fd_t, times: ?*[2]timeval) c_int;
pub extern "c" fn utimes(path: [*:0]const u8, times: ?*[2]timeval) c_int;

pub extern "c" fn utimensat(dirfd: fd_t, pathname: [*:0]const u8, times: ?*const [2]timespec, flags: u32) c_int;
pub extern "c" fn futimens(fd: fd_t, times: ?*const [2]timespec) c_int;

pub extern "c" fn pthread_create(
    noalias newthread: *pthread_t,
    noalias attr: ?*const pthread_attr_t,
    start_routine: *const fn (?*anyopaque) callconv(.c) ?*anyopaque,
    noalias arg: ?*anyopaque,
) E;
pub const pthread_cancelstate = switch (native_os) {
    .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => enum(c_int) {
        ENABLE = 1,
        DISABLE = 0,
    },
    .linux => if (native_abi.isMusl()) enum(c_int) {
        ENABLE = 0,
        DISABLE = 1,
        MASKED = 2,
    } else if (native_abi.isGnu()) enum(c_int) {
        ENABLE = 0,
        DISABLE = 1,
    },
    else => void,
};
pub extern "c" fn pthread_setcancelstate(pthread_cancelstate, ?*pthread_cancelstate) E;
pub extern "c" fn pthread_cancel(pthread_t) E;
pub extern "c" fn pthread_attr_init(attr: *pthread_attr_t) E;
pub extern "c" fn pthread_attr_setstack(attr: *pthread_attr_t, stackaddr: *anyopaque, stacksize: usize) E;
pub extern "c" fn pthread_attr_setstacksize(attr: *pthread_attr_t, stacksize: usize) E;
pub extern "c" fn pthread_attr_setguardsize(attr: *pthread_attr_t, guardsize: usize) E;
pub extern "c" fn pthread_attr_destroy(attr: *pthread_attr_t) E;
pub extern "c" fn pthread_self() pthread_t;
pub extern "c" fn pthread_join(thread: pthread_t, arg_return: ?*?*anyopaque) E;
pub extern "c" fn pthread_detach(thread: pthread_t) E;
pub extern "c" fn pthread_atfork(
    prepare: ?*const fn () callconv(.c) void,
    parent: ?*const fn () callconv(.c) void,
    child: ?*const fn () callconv(.c) void,
) c_int;
pub extern "c" fn pthread_key_create(
    key: *pthread_key_t,
    destructor: ?*const fn (value: *anyopaque) callconv(.c) void,
) E;
pub extern "c" fn pthread_key_delete(key: pthread_key_t) E;
pub extern "c" fn pthread_getspecific(key: pthread_key_t) ?*anyopaque;
pub extern "c" fn pthread_setspecific(key: pthread_key_t, value: ?*anyopaque) c_int;
pub extern "c" fn pthread_sigmask(how: c_int, set: *const sigset_t, oldset: *sigset_t) c_int;
pub const pthread_setname_np = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.pthread_setname_np,
    .illumos => illumos.pthread_setname_np,
    .netbsd => netbsd.pthread_setname_np,
    else => private.pthread_setname_np,
};

pub extern "c" fn pthread_getname_np(thread: pthread_t, name: [*:0]u8, len: usize) c_int;
pub extern "c" fn pthread_kill(pthread_t, signal: SIG) c_int;
pub extern "c" fn pthread_exit(ptr: ?*anyopaque) noreturn;

pub const pthread_threadid_np = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => private.pthread_threadid_np,
    else => {},
};

pub const caddr_t = ?[*]u8;

pub const ptrace = switch (native_os) {
    .linux, .serenity => private.ptrace,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.ptrace,
    .dragonfly => dragonfly.ptrace,
    .freebsd => freebsd.ptrace,
    .netbsd => netbsd.ptrace,
    .openbsd => openbsd.ptrace,
    else => {},
};

pub extern "c" fn sem_init(sem: *sem_t, pshared: c_int, value: c_uint) c_int;
pub extern "c" fn sem_destroy(sem: *sem_t) c_int;
pub extern "c" fn sem_open(name: [*:0]const u8, flag: c_int, mode: mode_t, value: c_uint) *sem_t;
pub extern "c" fn sem_close(sem: *sem_t) c_int;
pub extern "c" fn sem_post(sem: *sem_t) c_int;
pub extern "c" fn sem_wait(sem: *sem_t) c_int;
pub extern "c" fn sem_trywait(sem: *sem_t) c_int;
pub extern "c" fn sem_timedwait(sem: *sem_t, abs_timeout: *const timespec) c_int;
pub extern "c" fn sem_getvalue(sem: *sem_t, sval: *c_int) c_int;

pub const shm_open = switch (native_os) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.shm_open,
    else => private.shm_open,
};
pub extern "c" fn shm_unlink(name: [*:0]const u8) c_int;

pub extern "c" fn kqueue() c_int;
pub extern "c" fn kevent(
    kq: c_int,
    changelist: [*]const Kevent,
    nchanges: c_int,
    eventlist: [*]Kevent,
    nevents: c_int,
    timeout: ?*const timespec,
) c_int;

pub extern "c" fn port_create() port_t;
pub extern "c" fn port_associate(
    port: port_t,
    source: u32,
    object: usize,
    events: u32,
    user_var: ?*anyopaque,
) c_int;
pub extern "c" fn port_dissociate(port: port_t, source: u32, object: usize) c_int;
pub extern "c" fn port_send(port: port_t, events: u32, user_var: ?*anyopaque) c_int;
pub extern "c" fn port_sendn(
    ports: [*]port_t,
    errors: []u32,
    num_ports: u32,
    events: u32,
    user_var: ?*anyopaque,
) c_int;
pub extern "c" fn port_get(port: port_t, event: *port_event, timeout: ?*timespec) c_int;
pub extern "c" fn port_getn(
    port: port_t,
    event_list: []port_event,
    max_events: u32,
    events_retrieved: *u32,
    timeout: ?*timespec,
) c_int;
pub extern "c" fn port_alert(port: port_t, flags: u32, events: u32, user_var: ?*anyopaque) c_int;

pub extern "c" fn getaddrinfo(
    noalias node: ?[*:0]const u8,
    noalias service: ?[*:0]const u8,
    noalias hints: ?*const addrinfo,
    /// On Linux, `res` will not be modified on error and `freeaddrinfo` will
    /// potentially crash if you pass it an undefined pointer
    noalias res: *?*addrinfo,
) EAI;

pub extern "c" fn freeaddrinfo(res: *addrinfo) void;

pub extern "c" fn getnameinfo(
    noalias addr: *const sockaddr,
    addrlen: socklen_t,
    noalias host: ?[*]u8,
    hostlen: socklen_t,
    noalias serv: ?[*]u8,
    servlen: socklen_t,
    flags: NI,
) EAI;

pub extern "c" fn gai_strerror(errcode: EAI) [*:0]const u8;

pub extern "c" fn poll(fds: [*]pollfd, nfds: nfds_t, timeout: c_int) c_int;
pub extern "c" fn ppoll(fds: [*]pollfd, nfds: nfds_t, timeout: ?*const timespec, sigmask: ?*const sigset_t) c_int;

pub extern "c" fn dn_expand(
    msg: [*:0]const u8,
    eomorig: [*:0]const u8,
    comp_dn: [*:0]const u8,
    exp_dn: [*:0]u8,
    length: c_int,
) c_int;

pub const PTHREAD_MUTEX_INITIALIZER: pthread_mutex_t = .{};
pub extern "c" fn pthread_mutex_lock(mutex: *pthread_mutex_t) E;
pub extern "c" fn pthread_mutex_unlock(mutex: *pthread_mutex_t) E;
pub extern "c" fn pthread_mutex_trylock(mutex: *pthread_mutex_t) E;
pub extern "c" fn pthread_mutex_destroy(mutex: *pthread_mutex_t) E;

pub const PTHREAD_COND_INITIALIZER: pthread_cond_t = .{};
pub extern "c" fn pthread_cond_wait(noalias cond: *pthread_cond_t, noalias mutex: *pthread_mutex_t) E;
pub extern "c" fn pthread_cond_timedwait(noalias cond: *pthread_cond_t, noalias mutex: *pthread_mutex_t, noalias abstime: *const timespec) E;
pub extern "c" fn pthread_cond_signal(cond: *pthread_cond_t) E;
pub extern "c" fn pthread_cond_broadcast(cond: *pthread_cond_t) E;
pub extern "c" fn pthread_cond_destroy(cond: *pthread_cond_t) E;

pub extern "c" fn pthread_rwlock_destroy(rwl: *pthread_rwlock_t) callconv(.c) E;
pub extern "c" fn pthread_rwlock_rdlock(rwl: *pthread_rwlock_t) callconv(.c) E;
pub extern "c" fn pthread_rwlock_wrlock(rwl: *pthread_rwlock_t) callconv(.c) E;
pub extern "c" fn pthread_rwlock_tryrdlock(rwl: *pthread_rwlock_t) callconv(.c) E;
pub extern "c" fn pthread_rwlock_trywrlock(rwl: *pthread_rwlock_t) callconv(.c) E;
pub extern "c" fn pthread_rwlock_unlock(rwl: *pthread_rwlock_t) callconv(.c) E;

pub const pthread_t = switch (native_os) {
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L64
    .serenity => c_int,
    else => *opaque {},
};
pub const FILE = opaque {};

pub extern "c" fn dlopen(path: ?[*:0]const u8, mode: RTLD) ?*anyopaque;
pub extern "c" fn dlclose(handle: *anyopaque) c_int;
pub extern "c" fn dlsym(handle: ?*anyopaque, symbol: [*:0]const u8) ?*anyopaque;
pub extern "c" fn dlerror() ?[*:0]u8;

pub const dladdr = if (native_os.isDarwin()) darwin.dladdr else {};
pub const dl_info = if (native_os.isDarwin()) darwin.dl_info else {};

pub extern "c" fn sync() void;
pub extern "c" fn syncfs(fd: c_int) c_int;
pub extern "c" fn fsync(fd: c_int) c_int;
pub extern "c" fn fdatasync(fd: c_int) c_int;

pub extern "c" fn prctl(option: c_int, ...) c_int;

pub extern "c" fn getrlimit(resource: rlimit_resource, rlim: *rlimit) c_int;
pub extern "c" fn setrlimit(resou
```
