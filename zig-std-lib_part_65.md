```
           .MFILE => return syscall.fail(error.ProcessFdQuotaExceeded),
            .NFILE => return syscall.fail(error.SystemFdQuotaExceeded),
            .NOBUFS => return syscall.fail(error.SystemResources),
            .NOMEM => return syscall.fail(error.SystemResources),
            .PROTONOSUPPORT => return syscall.fail(error.ProtocolUnsupportedByAddressFamily),
            .PROTOTYPE => return syscall.fail(error.SocketModeUnsupported),
            else => |err| return syscall.unexpectedErrno(err),
        }
    };
    errdefer closeFd(socket_fd);

    if (options.ip6_only) {
        if (posix.IPV6 == void) return error.OptionUnsupported;
        try setSocketOptionPosix(socket_fd, posix.IPPROTO.IPV6, posix.IPV6.V6ONLY, 0);
    }

    return socket_fd;
}

fn setCloexec(fd: posix.fd_t) error{ Canceled, Unexpected }!void {
    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.fcntl(fd, posix.F.SETFD, @as(usize, posix.FD_CLOEXEC)))) {
        .SUCCESS => return syscall.finish(),
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        else => |err| return syscall.unexpectedErrno(err),
    };
}

fn netSocketCreatePair(
    userdata: ?*anyopaque,
    options: net.Socket.CreatePairOptions,
) net.Socket.CreatePairError![2]net.Socket {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    if (!have_networking) return error.OperationUnsupported;
    if (@TypeOf(posix.system.socketpair) == void) return error.OperationUnsupported;
    if (native_os == .haiku) @panic("TODO");

    const family: posix.sa_family_t = switch (options.family) {
        .ip4 => posix.AF.INET,
        .ip6 => posix.AF.INET6,
    };
    const mode, const protocol = try posixSocketModeProtocol(family, options.mode, options.protocol);
    const flags: u32 = mode | if (socket_flags_unsupported) 0 else posix.SOCK.CLOEXEC;

    var sockets: [2]posix.socket_t = undefined;
    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.socketpair(family, flags, protocol, &sockets))) {
        .SUCCESS => {
            syscall.finish();
            errdefer {
                closeFd(sockets[0]);
                closeFd(sockets[1]);
            }
            if (socket_flags_unsupported) {
                try setCloexec(sockets[0]);
                try setCloexec(sockets[1]);
            }
            var storages: [2]PosixAddress = undefined;
            var addr_lens: [2]posix.socklen_t = .{ @sizeOf(PosixAddress), @sizeOf(PosixAddress) };
            try posixGetSockName(sockets[0], &storages[0].any, &addr_lens[0]);
            try posixGetSockName(sockets[1], &storages[1].any, &addr_lens[1]);
            return .{
                .{ .handle = sockets[0], .address = addressFromPosix(&storages[0]) },
                .{ .handle = sockets[1], .address = addressFromPosix(&storages[1]) },
            };
        },
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .ACCES => return syscall.fail(error.AccessDenied),
        .AFNOSUPPORT => return syscall.fail(error.AddressFamilyUnsupported),
        .INVAL => return syscall.fail(error.ProtocolUnsupportedBySystem),
        .MFILE => return syscall.fail(error.ProcessFdQuotaExceeded),
        .NFILE => return syscall.fail(error.SystemFdQuotaExceeded),
        .NOBUFS => return syscall.fail(error.SystemResources),
        .NOMEM => return syscall.fail(error.SystemResources),
        .PROTONOSUPPORT => return syscall.fail(error.ProtocolUnsupportedByAddressFamily),
        .PROTOTYPE => return syscall.fail(error.SocketModeUnsupported),
        else => |err| return syscall.unexpectedErrno(err),
    };
}

fn openSocketAfd(family: ws2_32.ADDRESS_FAMILY, options: IpAddress.BindOptions) !net.Socket.Handle {
    const mode, const protocol = try posixSocketModeProtocol(family, options.mode, options.protocol);
    var handle: windows.HANDLE = undefined;
    var iosb: windows.IO_STATUS_BLOCK = undefined;
    var syscall: Syscall = try .start();
    while (true) switch (windows.ntdll.NtCreateFile(
        &handle,
        .{
            .STANDARD = .{ .RIGHTS = .{ .WRITE_DAC = true }, .SYNCHRONIZE = true },
            .GENERIC = .{ .WRITE = true, .READ = true },
        },
        &.{
            .ObjectName = @constCast(&windows.UNICODE_STRING.init(
                windows.AFD.DEVICE_NAME ++ .{ '\\', 'E', 'n', 'd', 'p', 'o', 'i', 'n', 't' },
            )),
        },
        &iosb,
        null,
        .{},
        .{ .READ = true, .WRITE = true },
        .OPEN_IF,
        .{ .IO = .ASYNCHRONOUS },
        &windows.AFD.OPEN_PACKET.FULL_EA_INFORMATION{ .Value = .{
            .EndpointType = .{
                .CONNECTIONLESS = switch (options.mode) {
                    .stream, .seqpacket, .rdm => false,
                    .dgram, .raw => true,
                },
                .MESSAGEMODE = options.mode != .stream,
                .RAW = options.mode == .raw,
            },
            .GroupID = 0,
            .AddressFamily = family,
            .SocketType = @bitCast(mode),
            .Protocol = @bitCast(protocol),
            .TransportDeviceNameLength = 0,
            .TransportDeviceName = undefined,
        } },
        @sizeOf(windows.AFD.OPEN_PACKET.FULL_EA_INFORMATION),
    )) {
        .SUCCESS => {
            syscall.finish();
            return handle;
        },
        .CANCELLED => {
            try syscall.checkCancel();
            continue;
        },
        .PROTOCOL_NOT_SUPPORTED => return syscall.fail(error.AddressFamilyUnsupported),
        .NO_SUCH_FILE => return syscall.fail(error.ProtocolUnsupportedByAddressFamily),
        else => |status| return syscall.unexpectedNtstatus(status),
    };
}

fn bindSocketIpAfd(socket_handle: net.Socket.Handle, address: *const IpAddress, mode: windows.AFD.BIND_INFO.MODE) !IpAddress {
    const Storage = extern struct { Info: windows.AFD.BIND_INFO, Address: PosixAddress };
    var storage: Storage = .{ .Info = .{ .Mode = mode }, .Address = undefined };
    const addr_len = addressToPosix(address, &storage.Address);
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = socket_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.BIND,
        .in = @as([]const u8, @ptrCast(&storage))[0 .. @offsetOf(Storage, "Address") + addr_len],
        .out = @as([]u8, @ptrCast(&storage.Address))[0..addr_len],
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        .SHARING_VIOLATION => return error.AddressInUse,
        else => |status| return windows.unexpectedStatus(status),
    }
    return addressFromPosix(&storage.Address);
}

fn bindSocketUnixAfd(socket_handle: net.Socket.Handle, address: *const net.UnixAddress) !void {
    const Storage = extern struct { Info: windows.AFD.BIND_INFO, Address: UnixAddress };
    var storage: Storage = .{ .Info = .{ .Mode = .Unix }, .Address = undefined };
    const addr_len = addressUnixToPosix(address, &storage.Address);
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = socket_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.BIND,
        .in = @as([]const u8, @ptrCast(&storage))[0 .. @offsetOf(Storage, "Address") + addr_len],
        .out = @ptrCast(&storage.Address),
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        .ADDRESS_ALREADY_EXISTS => return error.AddressInUse,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn netAcceptPosix(userdata: ?*anyopaque, listen_fd: net.Socket.Handle, options: net.Server.AcceptOptions) net.Server.AcceptError!net.Socket {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    options;
    var storage: PosixAddress = undefined;
    var addr_len: posix.socklen_t = @sizeOf(PosixAddress);
    const syscall: Syscall = try .start();
    const fd = while (true) {
        const rc = if (have_accept4)
            posix.system.accept4(listen_fd, &storage.any, &addr_len, posix.SOCK.CLOEXEC)
        else
            posix.system.accept(listen_fd, &storage.any, &addr_len);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                const fd: posix.fd_t = @intCast(rc);
                errdefer closeFd(fd);
                if (!have_accept4) try setCloexec(fd);
                break fd;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .AGAIN => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .CONNABORTED => return error.ConnectionAborted,
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => return error.SocketNotListening,
                    .NOTSOCK => |err| return errnoBug(err),
                    .MFILE => return error.ProcessFdQuotaExceeded,
                    .NFILE => return error.SystemFdQuotaExceeded,
                    .NOBUFS => return error.SystemResources,
                    .NOMEM => return error.SystemResources,
                    .OPNOTSUPP => |err| return errnoBug(err),
                    .PROTO => return error.ProtocolFailure,
                    .PERM => return error.BlockedByFirewall,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    };
    return .{ .handle = fd, .address = addressFromPosix(&storage) };
}

fn netAcceptWindows(userdata: ?*anyopaque, listen_handle: net.Socket.Handle, options: net.Server.AcceptOptions) net.Server.AcceptError!net.Socket {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const Storage = extern struct {
        Info: windows.AFD.LISTEN_RESPONSE_INFO,
        RemoteAddress: extern union { posix: PosixAddress, unix: UnixAddress },
    };
    var storage: Storage = undefined;
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = listen_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.WAIT_FOR_LISTEN,
        .out = @ptrCast(&storage),
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
    errdefer t.deferAcceptAfd(listen_handle, storage.Info);
    const accept_handle = openSocketAfd(
        storage.RemoteAddress.posix.any.family,
        .{ .mode = options.mode, .protocol = options.protocol },
    ) catch |err| switch (err) {
        error.AddressFamilyUnsupported => return error.Unexpected,
        error.ProtocolUnsupportedByAddressFamily => return error.Unexpected,
        else => |e| return e,
    };
    errdefer windows.CloseHandle(accept_handle);
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = listen_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.ACCEPT,
        .in = @ptrCast(&windows.AFD.ACCEPT_INFO{
            .UseSAN = .FALSE,
            .Sequence = storage.Info.Sequence,
            .AcceptHandle = accept_handle,
        }),
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
    return .{ .handle = accept_handle, .address = addressFromPosix(&storage.RemoteAddress.posix) };
}

fn deferAcceptAfd(t: *Threaded, listen_handle: net.Socket.Handle, info: windows.AFD.LISTEN_RESPONSE_INFO) void {
    const cancel_protection = swapCancelProtection(t, .blocked);
    defer _ = swapCancelProtection(t, cancel_protection);
    switch ((deviceIoControl(&.{
        .file = .{ .handle = listen_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.DEFER_ACCEPT,
        .in = @ptrCast(&windows.AFD.DEFER_ACCEPT_INFO{
            .Sequence = info.Sequence,
            .Reject = .FALSE,
        }),
    }) catch |err| switch (err) {
        error.Canceled => unreachable, // blocked
    }).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        else => |status| windows.unexpectedStatus(status) catch {},
    }
}

fn netReadPosix(userdata: ?*anyopaque, fd: net.Socket.Handle, data: [][]u8) net.Stream.Reader.Error!usize {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var iovecs_buffer: [max_iovecs_len]posix.iovec = undefined;
    var i: usize = 0;
    for (data) |buf| {
        if (iovecs_buffer.len - i == 0) break;
        if (buf.len != 0) {
            iovecs_buffer[i] = .{ .base = buf.ptr, .len = buf.len };
            i += 1;
        }
    }
    const dest = iovecs_buffer[0..i];
    assert(dest[0].len > 0);

    if (native_os == .wasi and !builtin.link_libc) {
        const syscall: Syscall = try .start();
        while (true) {
            var n: usize = undefined;
            switch (std.os.wasi.fd_read(fd, dest.ptr, dest.len, &n)) {
                .SUCCESS => {
                    syscall.finish();
                    return n;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .INVAL => |err| return errnoBug(err),
                        .FAULT => |err| return errnoBug(err),
                        .AGAIN => |err| return errnoBug(err),
                        .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                        .NOBUFS => return error.SystemResources,
                        .NOMEM => return error.SystemResources,
                        .NOTCONN => return error.SocketUnconnected,
                        .CONNRESET => return error.ConnectionResetByPeer,
                        .TIMEDOUT => return error.Timeout,
                        .NOTCAPABLE => return error.AccessDenied,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    const syscall: Syscall = try .start();
    while (true) {
        const rc = posix.system.readv(fd, dest.ptr, @intCast(dest.len));
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                return @intCast(rc);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .INVAL => |err| return errnoBug(err),
                    .FAULT => |err| return errnoBug(err),
                    .AGAIN => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .NOBUFS => return error.SystemResources,
                    .NOMEM => return error.SystemResources,
                    .NOTCONN => return error.SocketUnconnected,
                    .CONNRESET => return error.ConnectionResetByPeer,
                    .TIMEDOUT => return error.Timeout,
                    .PIPE => return error.SocketUnconnected,
                    .NETDOWN => return error.NetworkDown,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn netReadWindows(userdata: ?*anyopaque, socket_handle: net.Socket.Handle, data: [][]u8) net.Stream.Reader.Error!usize {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var iovecs: [max_iovecs_len]windows.AFD.WSABUF(.@"var") = undefined;
    var len: u32 = 0;
    for (data) |buf| {
        if (iovecs.len - len == 0) break;
        addAfdBuf(.@"var", &iovecs, &len, buf);
    }

    const iosb = try deviceIoControl(&.{
        .file = .{ .handle = socket_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.RECEIVE,
        .in = @ptrCast(&windows.AFD.RECV_INFO{
            .BufferArray = &iovecs,
            .BufferCount = len,
            .AfdFlags = .{ .NO_FAST_IO = true, .OVERLAPPED = true },
            .TdiFlags = .{ .NORMAL = true },
        }),
    });
    switch (iosb.u.Status) {
        .SUCCESS => return iosb.Information,
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn netSendPosix(
    userdata: ?*anyopaque,
    socket_handle: net.Socket.Handle,
    messages: []net.OutgoingMessage,
    flags: net.SendFlags,
) struct { ?net.Socket.SendError, usize } {
    if (!have_networking) return .{ error.NetworkDown, 0 };
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    const posix_flags: u32 =
        @as(u32, if (@hasDecl(posix.MSG, "CONFIRM") and flags.confirm) posix.MSG.CONFIRM else 0) |
        @as(u32, if (@hasDecl(posix.MSG, "DONTROUTE") and flags.dont_route) posix.MSG.DONTROUTE else 0) |
        @as(u32, if (@hasDecl(posix.MSG, "EOR") and flags.eor) posix.MSG.EOR else 0) |
        @as(u32, if (@hasDecl(posix.MSG, "OOB") and flags.oob) posix.MSG.OOB else 0) |
        @as(u32, if (@hasDecl(posix.MSG, "FASTOPEN") and flags.fastopen) posix.MSG.FASTOPEN else 0) |
        posix.MSG.NOSIGNAL;

    var i: usize = 0;
    while (messages.len - i != 0) {
        if (have_sendmmsg) {
            i += netSendManyPosix(socket_handle, messages[i..], posix_flags) catch |err| return .{ err, i };
            continue;
        }
        t.netSendOnePosix(socket_handle, &messages[i], posix_flags) catch |err| return .{ err, i };
        i += 1;
    }
    return .{ null, i };
}

fn netSendWindows(
    userdata: ?*anyopaque,
    socket_handle: net.Socket.Handle,
    messages: []net.OutgoingMessage,
    flags: net.SendFlags,
) struct { ?net.Socket.SendError, usize } {
    if (!have_networking) return .{ error.NetworkDown, 0 };
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    for (messages, 0..) |*m, i| {
        t.netSendOneWindows(socket_handle, m, flags) catch |err| return .{ err, i };
    }
    return .{ null, messages.len };
}

fn netSendOneWindows(
    t: *Threaded,
    socket_handle: net.Socket.Handle,
    message: *net.OutgoingMessage,
    flags: net.SendFlags,
) net.Socket.SendError!void {
    _ = t;
    _ = flags;
    const iovecs: [1]windows.AFD.WSABUF(.@"const") = .{.{
        .buf = message.data_ptr,
        .len = std.math.cast(std.os.windows.ULONG, message.data_len) orelse
            return error.MessageOversize,
    }};
    var storage: PosixAddress = undefined;
    const addr_len = addressToPosix(message.address, &storage);
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = socket_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.SEND_DATAGRAM,
        .in = @ptrCast(&windows.AFD.SEND_DATAGRAM_INFO{
            .BufferArray = &iovecs,
            .BufferCount = iovecs.len,
            .AfdFlags = .{ .NO_FAST_IO = true, .OVERLAPPED = true },
            .TdiRequest = undefined,
            .TdiConnInfo = .{
                .UserDataLength = undefined,
                .UserData = undefined,
                .OptionsLength = undefined,
                .Options = undefined,
                .RemoteAddressLength = @bitCast(addr_len),
                .RemoteAddress = &storage,
            },
        }),
    })).u.Status) {
        .SUCCESS => return,
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn netSendOnePosix(
    t: *Threaded,
    socket_handle: net.Socket.Handle,
    message: *net.OutgoingMessage,
    flags: u32,
) net.Socket.SendError!void {
    _ = t;
    var addr: PosixAddress = undefined;
    var iovec: posix.iovec_const = .{ .base = @constCast(message.data_ptr), .len = message.data_len };
    const msg: posix.msghdr_const = .{
        .name = &addr.any,
        .namelen = addressToPosix(message.address, &addr),
        .iov = (&iovec)[0..1],
        .iovlen = 1,
        // OS returns EINVAL if this pointer is invalid even if controllen is zero.
        .control = if (message.control.len == 0) null else @constCast(message.control.ptr),
        .controllen = @intCast(message.control.len),
        .flags = 0,
    };
    var syscall: if (is_windows) AlertableSyscall else Syscall = try .start();
    while (true) {
        const rc = posix.system.sendmsg(socket_handle, &msg, flags);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                message.data_len = @intCast(rc);
                return;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .ACCES => return syscall.fail(error.AccessDenied),
            .ALREADY => return syscall.fail(error.FastOpenAlreadyInProgress),
            .CONNRESET => return syscall.fail(error.ConnectionResetByPeer),
            .MSGSIZE => return syscall.fail(error.MessageOversize),
            .NOBUFS => return syscall.fail(error.SystemResources),
            .NOMEM => return syscall.fail(error.SystemResources),
            .PIPE => return syscall.fail(error.SocketUnconnected),
            .AFNOSUPPORT => return syscall.fail(error.AddressFamilyUnsupported),
            .HOSTUNREACH => return syscall.fail(error.HostUnreachable),
            .NETUNREACH => return syscall.fail(error.NetworkUnreachable),
            .NOTCONN => return syscall.fail(error.SocketUnconnected),
            .NETDOWN => return syscall.fail(error.NetworkDown),
            .BADF => |err| return syscall.errnoBug(err), // File descriptor used after closed.
            .DESTADDRREQ => |err| return syscall.errnoBug(err),
            .FAULT => |err| return syscall.errnoBug(err),
            .INVAL => |err| return syscall.errnoBug(err),
            .ISCONN => |err| return syscall.errnoBug(err),
            .NOTSOCK => |err| return syscall.errnoBug(err),
            .OPNOTSUPP => |err| return syscall.errnoBug(err),
            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

fn netSendManyPosix(
    socket_handle: net.Socket.Handle,
    messages: []net.OutgoingMessage,
    flags: u32,
) net.Socket.SendError!usize {
    var msg_buffer: [64]posix.system.mmsghdr = undefined;
    var addr_buffer: [msg_buffer.len]PosixAddress = undefined;
    var iovecs_buffer: [msg_buffer.len]posix.iovec = undefined;
    const min_len: usize = @min(messages.len, msg_buffer.len);
    const clamped_messages = messages[0..min_len];
    const clamped_msgs = (&msg_buffer)[0..min_len];
    const clamped_addrs = (&addr_buffer)[0..min_len];
    const clamped_iovecs = (&iovecs_buffer)[0..min_len];

    for (clamped_messages, clamped_msgs, clamped_addrs, clamped_iovecs) |*message, *msg, *addr, *iovec| {
        iovec.* = .{ .base = @constCast(message.data_ptr), .len = message.data_len };
        msg.* = .{
            .hdr = .{
                .name = &addr.any,
                .namelen = addressToPosix(message.address, addr),
                .iov = iovec[0..1],
                .iovlen = 1,
                .control = @constCast(message.control.ptr),
                .controllen = message.control.len,
                .flags = 0,
            },
            .len = undefined, // Populated by calling sendmmsg below.
        };
    }

    const syscall: Syscall = try .start();
    while (true) {
        const rc = posix.system.sendmmsg(socket_handle, clamped_msgs.ptr, @intCast(clamped_msgs.len), flags);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                const n: usize = @intCast(rc);
                for (clamped_messages[0..n], clamped_msgs[0..n]) |*message, *msg| {
                    message.data_len = msg.len;
                }
                return n;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .ACCES => return syscall.fail(error.AccessDenied),
            .ALREADY => return syscall.fail(error.FastOpenAlreadyInProgress),
            .CONNRESET => return syscall.fail(error.ConnectionResetByPeer),
            .MSGSIZE => return syscall.fail(error.MessageOversize),
            .NOBUFS => return syscall.fail(error.SystemResources),
            .NOMEM => return syscall.fail(error.SystemResources),
            .PIPE => return syscall.fail(error.SocketUnconnected),
            .AFNOSUPPORT => return syscall.fail(error.AddressFamilyUnsupported),
            .HOSTUNREACH => return syscall.fail(error.HostUnreachable),
            .NETUNREACH => return syscall.fail(error.NetworkUnreachable),
            .NOTCONN => return syscall.fail(error.SocketUnconnected),
            .NETDOWN => return syscall.fail(error.NetworkDown),

            .AGAIN => |err| return syscall.errnoBug(err),
            .BADF => |err| return syscall.errnoBug(err), // File descriptor used after closed.
            .DESTADDRREQ => |err| return syscall.errnoBug(err), // The socket is not connection-mode, and no peer address is set.
            .FAULT => |err| return syscall.errnoBug(err), // An invalid user space address was specified for an argument.
            .INVAL => |err| return syscall.errnoBug(err), // Invalid argument passed.
            .ISCONN => |err| return syscall.errnoBug(err), // connection-mode socket was connected already but a recipient was specified
            .NOTSOCK => |err| return syscall.errnoBug(err), // The file descriptor sockfd does not refer to a socket.
            .OPNOTSUPP => |err| return syscall.errnoBug(err), // Some bit in the flags argument is inappropriate for the socket type.

            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

fn netReceivePosix(
    socket_handle: net.Socket.Handle,
    message: *net.IncomingMessage,
    data_buffer: []u8,
    flags: net.ReceiveFlags,
    nonblocking: bool,
) (net.Socket.ReceiveError || error{WouldBlock})!void {
    // recvmmsg is useless, here's why:
    // * [timeout bug](https://bugzilla.kernel.org/show_bug.cgi?id=75371)
    // * it wants iovecs for each message but we have a better API: one data
    //   buffer to handle all the messages. The better API cannot be lowered to
    //   the split vectors though because reducing the buffer size might make
    //   some messages unreceivable.
    const posix_flags: u32 =
        @as(u32, if (flags.oob) posix.MSG.OOB else 0) |
        @as(u32, if (flags.peek) posix.MSG.PEEK else 0) |
        @as(u32, if (flags.trunc) posix.MSG.TRUNC else 0) |
        posix.MSG.NOSIGNAL |
        @as(u32, if (nonblocking) posix.MSG.DONTWAIT else 0);

    var storage: PosixAddress = undefined;
    var iov: posix.iovec = .{ .base = data_buffer.ptr, .len = data_buffer.len };
    var msg: posix.msghdr = .{
        .name = &storage.any,
        .namelen = @sizeOf(PosixAddress),
        .iov = (&iov)[0..1],
        .iovlen = 1,
        .control = message.control.ptr,
        .controllen = @intCast(message.control.len),
        .flags = undefined,
    };

    const syscall = try Syscall.start();
    while (true) {
        const rc = posix.system.recvmsg(socket_handle, &msg, posix_flags);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                const data = data_buffer[0..@intCast(rc)];
                message.* = .{
                    .from = addressFromPosix(&storage),
                    .data = data,
                    .control = if (msg.control) |ptr| @as([*]u8, @ptrCast(ptr))[0..msg.controllen] else message.control,
                    .flags = .{
                        .eor = (msg.flags & posix.MSG.EOR) != 0,
                        .trunc = (msg.flags & posix.MSG.TRUNC) != 0,
                        .ctrunc = (msg.flags & posix.MSG.CTRUNC) != 0,
                        .oob = (msg.flags & posix.MSG.OOB) != 0,
                        .errqueue = if (@hasDecl(posix.MSG, "ERRQUEUE")) (msg.flags & posix.MSG.ERRQUEUE) != 0 else false,
                    },
                };
                return;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .NFILE => return syscall.fail(error.SystemFdQuotaExceeded),
            .MFILE => return syscall.fail(error.ProcessFdQuotaExceeded),
            .NOBUFS => return syscall.fail(error.SystemResources),
            .NOMEM => return syscall.fail(error.SystemResources),
            .NOTCONN => return syscall.fail(error.SocketUnconnected),
            .MSGSIZE => return syscall.fail(error.MessageOversize),
            .PIPE => return syscall.fail(error.SocketUnconnected),
            .CONNRESET => return syscall.fail(error.ConnectionResetByPeer),
            .NETDOWN => return syscall.fail(error.NetworkDown),
            .AGAIN => return syscall.fail(error.WouldBlock),
            .BADF => |err| return syscall.errnoBug(err),
            .FAULT => |err| return syscall.errnoBug(err),
            .INVAL => |err| return syscall.errnoBug(err),
            .NOTSOCK => |err| return syscall.errnoBug(err),
            .OPNOTSUPP => |err| return syscall.errnoBug(err),
            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

fn netReceiveWindows(
    t: *Threaded,
    socket_handle: net.Socket.Handle,
    message_buffer: []net.IncomingMessage,
    data_buffer: []u8,
    flags: net.ReceiveFlags,
) struct { ?net.Socket.ReceiveError, usize } {
    t.netReceiveOneWindows(socket_handle, &message_buffer[0], data_buffer, flags) catch |err|
        return .{ err, 0 };
    return .{ null, 1 };
}

fn netReceiveOneWindows(
    t: *Threaded,
    socket_handle: net.Socket.Handle,
    message: *net.IncomingMessage,
    data_buffer: []u8,
    flags: net.ReceiveFlags,
) net.Socket.ReceiveError!void {
    if (!have_networking) return error.NetworkDown;
    _ = t;
    const iovecs: [1]windows.AFD.WSABUF(.@"var") = .{.{
        .buf = data_buffer.ptr,
        .len = std.math.cast(std.os.windows.ULONG, data_buffer.len) orelse return error.MessageOversize,
    }};
    var storage: PosixAddress = undefined;
    var addr_len: windows.ULONG = @sizeOf(PosixAddress);
    const iosb = try deviceIoControl(&.{
        .file = .{ .handle = socket_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.RECEIVE_DATAGRAM,
        .in = @ptrCast(&windows.AFD.RECV_DATAGRAM_INFO{
            .BufferArray = &iovecs,
            .BufferCount = iovecs.len,
            .AfdFlags = .{ .NO_FAST_IO = true, .OVERLAPPED = true },
            .TdiFlags = .{ .NORMAL = !flags.oob, .EXPEDITED = flags.oob, .PEEK = flags.peek },
            .Address = &storage,
            .AddressLength = &addr_len,
        }),
    });
    switch (iosb.u.Status) {
        .SUCCESS, .RECEIVE_EXPEDITED => |status| message.* = .{
            .from = addressFromPosix(&storage),
            .data = data_buffer[0..iosb.Information],
            .control = &.{},
            .flags = .{
                .eor = false,
                .trunc = false,
                .ctrunc = false,
                .oob = switch (status) {
                    else => unreachable,
                    .SUCCESS, .RECEIVE_PARTIAL, .BUFFER_OVERFLOW => false,
                    .RECEIVE_EXPEDITED, .RECEIVE_PARTIAL_EXPEDITED => true,
                },
                .errqueue = false,
            },
        },
        .RECEIVE_PARTIAL,
        .RECEIVE_PARTIAL_EXPEDITED,
        => |status| return windows.unexpectedStatus(status), // TdiFlags.PARTIAL = false
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        .BUFFER_OVERFLOW => return error.MessageOversize,
        .PORT_UNREACHABLE => return error.PortUnreachable,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn netWritePosix(
    userdata: ?*anyopaque,
    fd: net.Socket.Handle,
    header: []const u8,
    data: []const []const u8,
    splat: usize,
) net.Stream.Writer.Error!usize {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var iovecs: [max_iovecs_len]posix.iovec_const = undefined;
    var msg: posix.msghdr_const = .{
        .name = null,
        .namelen = 0,
        .iov = &iovecs,
        .iovlen = 0,
        .control = null,
        .controllen = 0,
        .flags = 0,
    };
    addBuf(&iovecs, &msg.iovlen, header);
    for (data[0 .. data.len - 1]) |bytes| addBuf(&iovecs, &msg.iovlen, bytes);
    const pattern = data[data.len - 1];

    var splat_backup_buffer: [splat_buffer_size]u8 = undefined;
    if (iovecs.len - msg.iovlen != 0) switch (splat) {
        0 => {},
        1 => addBuf(&iovecs, &msg.iovlen, pattern),
        else => switch (pattern.len) {
            0 => {},
            1 => {
                const splat_buffer = &splat_backup_buffer;
                const memset_len = @min(splat_buffer.len, splat);
                const buf = splat_buffer[0..memset_len];
                @memset(buf, pattern[0]);
                addBuf(&iovecs, &msg.iovlen, buf);
                var remaining_splat = splat - buf.len;
                while (remaining_splat > splat_buffer.len and iovecs.len - msg.iovlen != 0) {
                    assert(buf.len == splat_buffer.len);
                    addBuf(&iovecs, &msg.iovlen, splat_buffer);
                    remaining_splat -= splat_buffer.len;
                }
                addBuf(&iovecs, &msg.iovlen, splat_buffer[0..@min(remaining_splat, splat_buffer.len)]);
            },
            else => for (0..@min(splat, iovecs.len - msg.iovlen)) |_| {
                addBuf(&iovecs, &msg.iovlen, pattern);
            },
        },
    };
    const flags = posix.MSG.NOSIGNAL;

    const syscall: Syscall = try .start();
    while (true) {
        const rc = posix.system.sendmsg(fd, &msg, flags);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                return @intCast(rc);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .ACCES => |err| return errnoBug(err),
                    .AGAIN => |err| return errnoBug(err),
                    .ALREADY => return error.FastOpenAlreadyInProgress,
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .CONNRESET => return error.ConnectionResetByPeer,
                    .DESTADDRREQ => |err| return errnoBug(err), // The socket is not connection-mode, and no peer address is set.
                    .FAULT => |err| return errnoBug(err), // An invalid user space address was specified for an argument.
                    .INVAL => |err| return errnoBug(err), // Invalid argument passed.
                    .ISCONN => |err| return errnoBug(err), // connection-mode socket was connected already but a recipient was specified
                    .MSGSIZE => |err| return errnoBug(err),
                    .NOBUFS => return error.SystemResources,
                    .NOMEM => return error.SystemResources,
                    .NOTSOCK => |err| return errnoBug(err), // The file descriptor sockfd does not refer to a socket.
                    .OPNOTSUPP => |err| return errnoBug(err), // Some bit in the flags argument is inappropriate for the socket type.
                    .PIPE => return error.SocketUnconnected,
                    .AFNOSUPPORT => return error.AddressFamilyUnsupported,
                    .HOSTUNREACH => return error.HostUnreachable,
                    .NETUNREACH => return error.NetworkUnreachable,
                    .NOTCONN => return error.SocketUnconnected,
                    .NETDOWN => return error.NetworkDown,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn netWriteWindows(
    userdata: ?*anyopaque,
    handle: net.Socket.Handle,
    header: []const u8,
    data: []const []const u8,
    splat: usize,
) net.Stream.Writer.Error!usize {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var iovecs: [max_iovecs_len]windows.AFD.WSABUF(.@"const") = undefined;
    var len: u32 = 0;
    addAfdBuf(.@"const", &iovecs, &len, header);
    for (data[0 .. data.len - 1]) |bytes| addAfdBuf(.@"const", &iovecs, &len, bytes);
    const pattern = data[data.len - 1];
    var backup_buffer: [64]u8 = undefined;
    if (iovecs.len - len != 0) switch (splat) {
        0 => {},
        1 => addAfdBuf(.@"const", &iovecs, &len, pattern),
        else => switch (pattern.len) {
            0 => {},
            1 => {
                const splat_buffer = &backup_buffer;
                const memset_len = @min(splat_buffer.len, splat);
                const buf = splat_buffer[0..memset_len];
                @memset(buf, pattern[0]);
                addAfdBuf(.@"const", &iovecs, &len, buf);
                var remaining_splat = splat - buf.len;
                while (remaining_splat > splat_buffer.len and len < iovecs.len) {
                    addAfdBuf(.@"const", &iovecs, &len, splat_buffer);
                    remaining_splat -= splat_buffer.len;
                }
                addAfdBuf(.@"const", &iovecs, &len, splat_buffer[0..@min(remaining_splat, splat_buffer.len)]);
            },
            else => for (0..@min(splat, iovecs.len - len)) |_| {
                addAfdBuf(.@"const", &iovecs, &len, pattern);
            },
        },
    };

    const iosb = try deviceIoControl(&.{
        .file = .{ .handle = handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.SEND,
        .in = @ptrCast(&windows.AFD.SEND_INFO{
            .BufferArray = &iovecs,
            .BufferCount = len,
            .AfdFlags = .{ .NO_FAST_IO = true, .OVERLAPPED = true },
            .TdiFlags = .{},
        }),
    });
    switch (iosb.u.Status) {
        .SUCCESS => return iosb.Information,
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn addAfdBuf(
    comptime mutability: windows.AFD.Mutability,
    iovecs: []windows.AFD.WSABUF(mutability),
    len: *u32,
    bytes: switch (mutability) {
        .@"const" => []const u8,
        .@"var" => []u8,
    },
) void {
    if (bytes.len == 0) return;
    const cap = std.math.maxInt(u32);
    var remaining = bytes;
    while (remaining.len > cap) {
        if (iovecs.len - len.* == 0) return;
        iovecs[len.*] = .{ .buf = remaining.ptr, .len = cap };
        len.* += 1;
        remaining = remaining[cap..];
    } else {
        @branchHint(.likely);
        if (iovecs.len - len.* == 0) return;
        iovecs[len.*] = .{ .buf = remaining.ptr, .len = @intCast(remaining.len) };
        len.* += 1;
    }
}

/// This is either usize or u32. Since, either is fine, let's use the same
/// `addBuf` function for both writing to a file and sending network messages.
const iovlen_t = switch (native_os) {
    .wasi => u32,
    else => @FieldType(posix.msghdr_const, "iovlen"),
};

fn addBuf(v: []posix.iovec_const, i: *iovlen_t, bytes: []const u8) void {
    // OS checks ptr addr before length so zero length vectors must be omitted.
    if (bytes.len == 0) return;
    if (v.len - i.* == 0) return;
    v[i.*] = .{ .base = bytes.ptr, .len = bytes.len };
    i.* += 1;
}

fn netClose(userdata: ?*anyopaque, handles: []const net.Socket.Handle) void {
    if (!have_networking) unreachable;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    for (handles) |handle| switch (native_os) {
        .windows => windows.CloseHandle(handle),
        else => closeFd(handle),
    };
}

fn netShutdownPosix(userdata: ?*anyopaque, handle: net.Socket.Handle, how: net.ShutdownHow) net.ShutdownError!void {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    const posix_how: i32 = switch (how) {
        .recv => posix.SHUT.RD,
        .send => posix.SHUT.WR,
        .both => posix.SHUT.RDWR,
    };

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.shutdown(handle, posix_how))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .BADF, .NOTSOCK, .INVAL => |err| return errnoBug(err),
                    .NOTCONN => return error.SocketUnconnected,
                    .NOBUFS => return error.SystemResources,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn netShutdownWindows(userdata: ?*anyopaque, handle: net.Socket.Handle, how: net.ShutdownHow) net.ShutdownError!void {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    // shutdown does not support apcs at all
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = handle, .flags = .{ .nonblocking = false } },
        .code = windows.IOCTL.AFD.PARTIAL_DISCONNECT,
        .in = @ptrCast(&windows.AFD.PARTIAL_DISCONNECT_INFO{
            .DisconnectMode = .{ .SEND = how != .recv, .RECEIVE = how != .send },
            .Timeout = -1,
        }),
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn netInterfaceNameResolve(
    userdata: ?*anyopaque,
    name: *const net.Interface.Name,
) net.Interface.Name.ResolveError!net.Interface {
    if (!have_networking) return error.InterfaceNotFound;
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    if (native_os == .linux) {
        const sock_fd = openSocketPosix(posix.AF.UNIX, .{ .mode = .dgram }) catch |err| switch (err) {
            error.ProcessFdQuotaExceeded => return error.SystemResources,
            error.SystemFdQuotaExceeded => return error.SystemResources,
            error.AddressFamilyUnsupported => return error.Unexpected,
            error.ProtocolUnsupportedBySystem => return error.Unexpected,
            error.ProtocolUnsupportedByAddressFamily => return error.Unexpected,
            error.SocketModeUnsupported => return error.Unexpected,
            error.OptionUnsupported => return error.Unexpected,
            else => |e| return e,
        };
        defer closeFd(sock_fd);

        var ifr: posix.ifreq = .{
            .ifrn = .{ .name = @bitCast(name.bytes) },
            .ifru = undefined,
        };

        const syscall: Syscall = try .start();
        while (true) switch (posix.errno(posix.system.ioctl(sock_fd, posix.SIOCGIFINDEX, @intFromPtr(&ifr)))) {
            .SUCCESS => {
                syscall.finish();
                return .{ .index = @bitCast(ifr.ifru.ivalue) };
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .NODEV => return syscall.fail(error.InterfaceNotFound),
            else => |err| return syscall.unexpectedErrno(err),
        };
    }

    if (is_windows) {
        var ConvertInterfaceNameToLuidW = t.dl.ConvertInterfaceNameToLuidW.load(.acquire);
        var ConvertInterfaceLuidToIndex = t.dl.ConvertInterfaceLuidToIndex.load(.acquire);
        if (ConvertInterfaceNameToLuidW == null or ConvertInterfaceLuidToIndex == null) {
            const iphlpapi_dll = t.dl.iphlpapi_dll.load(.acquire) orelse iphlpapi_dll: {
                try Thread.checkCancel();
                var iphlpapi_dll: *anyopaque = undefined;
                switch (windows.ntdll.LdrLoadDll(null, null, &.init(
                    &.{ 'I', 'P', 'H', 'L', 'P', 'A', 'P', 'I', '.', 'D', 'L', 'L' },
                ), &iphlpapi_dll)) {
                    .SUCCESS => {},
                    .DLL_NOT_FOUND => return error.Unexpected,
                    else => |status| return windows.unexpectedStatus(status),
                }
                const handle = t.dl.iphlpapi_dll.cmpxchgStrong(null, iphlpapi_dll, .release, .monotonic) orelse
                    break :iphlpapi_dll iphlpapi_dll;
                switch (windows.ntdll.LdrUnloadDll(iphlpapi_dll)) {
                    .SUCCESS => break :iphlpapi_dll handle.?,
                    else => |status| return windows.unexpectedStatus(status),
                }
            };
            switch (windows.ntdll.LdrGetProcedureAddress(iphlpapi_dll, &.init(
                &.{
                    'C', 'o', 'n', 'v', 'e', 'r', 't', 'I', 'n', 't', 'e', 'r', 'f', 'a', 'c', 'e',
                    'N', 'a', 'm', 'e', 'T', 'o', 'L', 'u', 'i', 'd', 'W',
                },
            ), 0, @ptrCast(&ConvertInterfaceNameToLuidW))) {
                .SUCCESS => t.dl.ConvertInterfaceNameToLuidW.store(ConvertInterfaceNameToLuidW, .release),
                else => |status| return windows.unexpectedStatus(status),
            }
            switch (windows.ntdll.LdrGetProcedureAddress(iphlpapi_dll, &.init(
                &.{
                    'C', 'o', 'n', 'v', 'e', 'r', 't', 'I', 'n', 't', 'e', 'r', 'f', 'a', 'c', 'e',
                    'L', 'u', 'i', 'd', 'T', 'o', 'I', 'n', 'd', 'e', 'x',
                },
            ), 0, @ptrCast(&ConvertInterfaceLuidToIndex))) {
                .SUCCESS => t.dl.ConvertInterfaceLuidToIndex.store(ConvertInterfaceLuidToIndex, .release),
                else => |status| return windows.unexpectedStatus(status),
            }
        }
        try Thread.checkCancel();
        var name_w: [net.Interface.Name.max_len:0]windows.WCHAR = undefined;
        name_w[
            std.unicode.wtf8ToWtf16Le(&name_w, name.toSlice()) catch |err| switch (err) {
                error.InvalidWtf8 => return error.InterfaceNotFound,
            }
        ] = 0;
        var luid: windows.NET.LUID = undefined;
        switch (ConvertInterfaceNameToLuidW.?(&name_w, &luid)) {
            .SUCCESS => {},
            .INVALID_NAME => return error.InterfaceNotFound,
            .INVALID_PARAMETER => unreachable,
            else => |err| return windows.unexpectedError(err),
        }
        var index: windows.NET.IFINDEX = undefined;
        switch (ConvertInterfaceLuidToIndex.?(&luid, &index)) {
            .SUCCESS => {},
            .INVALID_PARAMETER => unreachable,
            else => |err| return windows.unexpectedError(err),
        }
        return .{ .index = @intFromEnum(index) };
    }

    if (builtin.link_libc) {
        try Thread.checkCancel();
        const index = std.c.if_nametoindex(&name.bytes);
        if (index == 0) return error.InterfaceNotFound;
        return .{ .index = @bitCast(index) };
    }

    @panic("unimplemented");
}

fn netInterfaceName(userdata: ?*anyopaque, interface: net.Interface) net.Interface.NameError!net.Interface.Name {
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    if (native_os == .linux) {
        try Thread.checkCancel();
        @panic("TODO implement netInterfaceName for linux");
    }

    if (is_windows) {
        var ConvertInterfaceIndexToLuid = t.dl.ConvertInterfaceIndexToLuid.load(.acquire);
        var ConvertInterfaceLuidToNameW = t.dl.ConvertInterfaceLuidToNameW.load(.acquire);
        if (ConvertInterfaceIndexToLuid == null or ConvertInterfaceLuidToNameW == null) {
            const iphlpapi_dll = t.dl.iphlpapi_dll.load(.acquire) orelse iphlpapi_dll: {
                try Thread.checkCancel();
                var iphlpapi_dll: *anyopaque = undefined;
                switch (windows.ntdll.LdrLoadDll(null, null, &.init(
                    &.{ 'I', 'P', 'H', 'L', 'P', 'A', 'P', 'I', '.', 'D', 'L', 'L' },
                ), &iphlpapi_dll)) {
                    .SUCCESS => {},
                    .DLL_NOT_FOUND => return error.Unexpected,
                    else => |status| return windows.unexpectedStatus(status),
                }
                const handle = t.dl.iphlpapi_dll.cmpxchgStrong(null, iphlpapi_dll, .release, .monotonic) orelse
                    break :iphlpapi_dll iphlpapi_dll;
                switch (windows.ntdll.LdrUnloadDll(iphlpapi_dll)) {
                    .SUCCESS => break :iphlpapi_dll handle.?,
                    else => |status| return windows.unexpectedStatus(status),
                }
            };
            switch (windows.ntdll.LdrGetProcedureAddress(iphlpapi_dll, &.init(
                &.{
                    'C', 'o', 'n', 'v', 'e', 'r', 't', 'I', 'n', 't', 'e', 'r', 'f', 'a', 'c', 'e',
                    'I', 'n', 'd', 'e', 'x', 'T', 'o', 'L', 'u', 'i', 'd',
                },
            ), 0, @ptrCast(&ConvertInterfaceIndexToLuid))) {
                .SUCCESS => t.dl.ConvertInterfaceIndexToLuid.store(ConvertInterfaceIndexToLuid, .release),
                else => |status| return windows.unexpectedStatus(status),
            }
            switch (windows.ntdll.LdrGetProcedureAddress(iphlpapi_dll, &.init(
                &.{
                    'C', 'o', 'n', 'v', 'e', 'r', 't', 'I', 'n', 't', 'e', 'r', 'f', 'a', 'c', 'e',
                    'L', 'u', 'i', 'd', 'T', 'o', 'N', 'a', 'm', 'e', 'W',
                },
            ), 0, @ptrCast(&ConvertInterfaceLuidToNameW))) {
                .SUCCESS => t.dl.ConvertInterfaceLuidToNameW.store(ConvertInterfaceLuidToNameW, .release),
                else => |status| return windows.unexpectedStatus(status),
            }
        }
        try Thread.checkCancel();
        var luid: windows.NET.LUID = undefined;
        switch (ConvertInterfaceIndexToLuid.?(@enumFromInt(interface.index), &luid)) {
            .SUCCESS => {},
            .FILE_NOT_FOUND => return error.InterfaceNotFound,
            .INVALID_PARAMETER => unreachable,
            else => |err| return windows.unexpectedError(err),
        }
        var name_w: [net.Interface.Name.max_len:0]windows.WCHAR = undefined;
        switch (ConvertInterfaceLuidToNameW.?(&luid, &name_w, name_w.len)) {
            .SUCCESS => {},
            .INVALID_PARAMETER => unreachable,
            .NOT_ENOUGH_MEMORY => return error.NameTooLong,
            else => |err| return windows.unexpectedError(err),
        }
        var name: [3 * net.Interface.Name.max_len]u8 = undefined;
        return .fromSlice(name[0..std.unicode.wtf16LeToWtf8(&name, std.mem.sliceTo(&name_w, 0))]);
    }

    if (builtin.link_libc) {
        try Thread.checkCancel();
        @panic("TODO implement netInterfaceName for libc");
    }

    @panic("unimplemented");
}

fn netLookup(
    userdata: ?*anyopaque,
    host_name: HostName,
    resolved: *Io.Queue(HostName.LookupResult),
    options: HostName.LookupOptions,
) net.HostName.LookupError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    defer resolved.close(io(t));
    t.netLookupFallible(host_name, resolved, options) catch |err| switch (err) {
        error.Closed => unreachable, // `resolved` must not be closed until `netLookup` returns
        else => |e| return e,
    };
}

fn netLookupFallible(
    t: *Threaded,
    host_name: HostName,
    resolved: *Io.Queue(HostName.LookupResult),
    options: HostName.LookupOptions,
) (net.HostName.LookupError || Io.QueueClosedError)!void {
    if (!have_networking) return error.NetworkDown;

    const t_io = t.io();
    const name = host_name.bytes;
    assert(name.len <= HostName.max_len);

    // On Linux, glibc provides getaddrinfo_a which is capable of supporting our semantics.
    // However, musl's POSIX-compliant getaddrinfo is not, so we bypass it.

    if (builtin.target.isGnuLibC()) {
        // TODO use getaddrinfo_a / gai_cancel
    }

    if (native_os == .linux or is_windows) {
        if (IpAddress.parseIp6(name, options.port)) |addr| {
            if (options.family == .ip4) return error.UnknownHostName;
            if (copyCanon(options.canonical_name_buffer, name)) |canon| {
                try resolved.putAll(t_io, &.{
                    .{ .address = addr },
                    .{ .canonical_name = canon },
                });
            } else {
                try resolved.putOne(t_io, .{ .address = addr });
            }
            return;
        } else |_| {}

        if (IpAddress.parseIp4(name, options.port)) |addr| {
            if (options.family == .ip6) return error.UnknownHostName;
            if (copyCanon(options.canonical_name_buffer, name)) |canon| {
                try resolved.putAll(t_io, &.{
                    .{ .address = addr },
                    .{ .canonical_name = canon },
                });
            } else {
                try resolved.putOne(t_io, .{ .address = addr });
            }
            return;
        } else |_| {}

        if (t.lookupHosts(host_name, resolved, options)) return else |err| switch (err) {
            error.UnknownHostName => {},
            else => |e| return e,
        }

        // RFC 6761 Section 6.3.3
        // Name resolution APIs and libraries SHOULD recognize
        // localhost names as special and SHOULD always return the IP
        // loopback address for address queries and negative responses
        // for all other query types.

        // Check for equal to "localhost(.)" or ends in ".localhost(.)"
        const localhost = if (name[name.len - 1] == '.') "localhost." else "localhost";
        if (std.mem.endsWith(u8, name, localhost) and
            (name.len == localhost.len or name[name.len - localhost.len] == '.'))
        {
            var results_buffer: [3]HostName.LookupResult = undefined;
            var results_index: usize = 0;
            if (options.family != .ip4) {
                results_buffer[results_index] = .{ .address = .{ .ip6 = .loopback(options.port) } };
                results_index += 1;
            }
            if (options.family != .ip6) {
                results_buffer[results_index] = .{ .address = .{ .ip4 = .loopback(options.port) } };
                results_index += 1;
            }
            if (options.canonical_name_buffer) |buf| {
                const canon_name = "localhost";
                const canon_name_dest = buf[0..canon_name.len];
                canon_name_dest.* = canon_name.*;
                results_buffer[results_index] = .{ .canonical_name = .{ .bytes = canon_name_dest } };
                results_index += 1;
            }
            try resolved.putAll(t_io, results_buffer[0..results_index]);
            return;
        }

        if (native_os == .linux) return t.lookupDnsSearch(host_name, resolved, options);

        comptime assert(is_windows);
        var DnsQueryEx = t.dl.DnsQueryEx.load(.acquire);
        //var DnsCancelQuery = t.dl.DnsCancelQuery.load(.acquire);
        var DnsFree = t.dl.DnsFree.load(.acquire);
        if (DnsQueryEx == null or
            //DnsCancelQuery == null or
            DnsFree == null)
        {
            const dnsapi_dll = t.dl.dnsapi_dll.load(.acquire) orelse dnsapi_dll: {
                try Thread.checkCancel();
                var dnsapi_dll: *anyopaque = undefined;
                switch (windows.ntdll.LdrLoadDll(null, null, &.init(
                    &.{ 'd', 'n', 's', 'a', 'p', 'i', '.', 'd', 'l', 'l' },
                ), &dnsapi_dll)) {
                    .SUCCESS => {},
                    .DLL_NOT_FOUND => return error.Unexpected,
                    else => |status| return windows.unexpectedStatus(status),
                }
                const handle = t.dl.dnsapi_dll.cmpxchgStrong(null, dnsapi_dll, .release, .monotonic) orelse
                    break :dnsapi_dll dnsapi_dll;
                switch (windows.ntdll.LdrUnloadDll(dnsapi_dll)) {
                    .SUCCESS => break :dnsapi_dll handle.?,
                    else => |status| return windows.unexpectedStatus(status),
                }
            };
            switch (windows.ntdll.LdrGetProcedureAddress(dnsapi_dll, &.init(
                &.{ 'D', 'n', 's', 'Q', 'u', 'e', 'r', 'y', 'E', 'x' },
            ), 0, @ptrCast(&DnsQueryEx))) {
                .SUCCESS => t.dl.DnsQueryEx.store(DnsQueryEx, .release),
                else => |status| return windows.unexpectedStatus(status),
            }
            //switch (windows.ntdll.LdrGetProcedureAddress(dnsapi_dll, &.init(
            //    &.{ 'D', 'n', 's', 'C', 'a', 'n', 'c', 'e', 'l', 'Q', 'u', 'e', 'r', 'y' },
            //), 0, @ptrCast(&DnsCancelQuery))) {
            //    .SUCCESS => t.dl.DnsCancelQuery.store(DnsCancelQuery, .release),
            //    else => |status| return windows.unexpectedStatus(status),
            //}
            switch (windows.ntdll.LdrGetProcedureAddress(dnsapi_dll, &.init(
                &.{ 'D', 'n', 's', 'F', 'r', 'e', 'e' },
            ), 0, @ptrCast(&DnsFree))) {
                .SUCCESS => t.dl.DnsFree.store(DnsFree, .release),
                else => |status| return windows.unexpectedStatus(status),
            }
        }
        try Thread.checkCancel();
        const current_thread = Thread.current;
        var lookup_dns: LookupDnsWindows = .{
            .threaded = t,
            .thread = if (current_thread) |thread| thread.handle else undefined,
            .resolved = resolved,
            .options = options,
            .results = .{
                .Version = 1,
                .QueryStatus = undefined,
                .QueryOptions = undefined,
                .pQueryRecords = undefined,
                .Reserved = undefined,
            },
            .done = false,
        };
        var host_name_w: [HostName.max_len:0]windows.WCHAR = undefined;
        host_name_w[
            std.unicode.wtf8ToWtf16Le(&host_name_w, name) catch |err| switch (err) {
                error.InvalidWtf8 => return error.UnknownHostName,
            }
        ] = 0;
        //var cancel_token: windows.DNS.QUERY.CANCEL = undefined;
        // Workaround various bugs by attempting a synchronous non-wire query first
        switch (DnsQueryEx.?(&.{
            .Version = 1,
            .QueryName = &host_name_w,
            .QueryType = if (options.family == .ip4) .A else .AAAA,
            .QueryOptions = .{
                .NO_WIRE_QUERY = true,
                .NO_HOSTS_FILE = true, // handled above
                .ADDRCONFIG = true,
                .DUAL_ADDR = options.family == null,
            },
        }, &lookup_dns.results, null)) {
            .SUCCESS => try lookup_dns.completedFallible(),
            // We must wait for the APC routine.
            .DNS_REQUEST_PENDING => unreachable, // `pQueryCompletionCallback` was `null`
            .DNS_ERROR_RECORD_DOES_NOT_EXIST => switch (DnsQueryEx.?(&.{
                .Version = 1,
                .QueryName = &host_name_w,
                .QueryType = if (options.family == .ip4) .A else .AAAA,
                .QueryOptions = .{
                    .NO_HOSTS_FILE = true, // handled above
                    .ADDRCONFIG = true,
                    .DUAL_ADDR = options.family == null,
                    .MULTICAST_WAIT = true,
                },
                .pQueryCompletionCallback = if (current_thread) |_| &LookupDnsWindows.completed else null,
            }, &lookup_dns.results,
                //&cancel_token,
                null)) {
                .SUCCESS => try lookup_dns.completedFallible(),
                // We must wait for the APC routine.
                .DNS_REQUEST_PENDING => {
                    assert(current_thread != null); // `pQueryCompletionCallback` was `null`
                    while (!@atomicLoad(bool, &lookup_dns.done, .acquire)) {
                        // Once we get here we must not return from the function until the
                        // operation completes, thereby releasing references to `host_name_w`,
                        // `lookup_dns.results`, and `cancel_token`.
                        const alertable_syscall = AlertableSyscall.start() catch |err| switch (err) {
                            error.Canceled => |e| {
                                //_ = DnsCancelQuery.?(&cancel_token);
                                while (!@atomicLoad(bool, &lookup_dns.done, .acquire)) waitForApcOrAlert();
                                return e;
                            },
                        };
                        waitForApcOrAlert();
                        alertable_syscall.finish();
                    }
                },
                else => |status| lookup_dns.results.QueryStatus = status,
            },
            else => |status| lookup_dns.results.QueryStatus = status,
        }
        switch (lookup_dns.results.QueryStatus) {
            .SUCCESS => return,
            .DNS_REQUEST_PENDING => unreachable, // already handled
            .INVALID_NAME,
            .DNS_ERROR_RCODE_NAME_ERROR,
            .DNS_INFO_NO_RECORDS,
            .DNS_ERROR_INVALID_NAME_CHAR,
            .DNS_ERROR_RECORD_DOES_NOT_EXIST,
            => return error.UnknownHostName,
            else => |err| return windows.unexpectedError(err),
        }
    }

    if (native_os == .openbsd) {
        // TODO use getaddrinfo_async / asr_abort
    }

    if (native_os == .freebsd) {
        // TODO use dnsres_getaddrinfo
    }

    if (is_darwin) {
        // TODO use CFHostStartInfoResolution / CFHostCancelInfoResolution
    }

    if (builtin.link_libc) {
        // This operating system lacks a way to resolve asynchronously. We are
        // stuck with getaddrinfo.
        var name_buffer: [HostName.max_len:0]u8 = undefined;
        @memcpy(name_buffer[0..name.len], name);
        name_buffer[name.len] = 0;
        const name_c = name_buffer[0..name.len :0];

        var port_buffer: [8]u8 = undefined;
        const port_c = std.fmt.bufPrintZ(&port_buffer, "{d}", .{options.port}) catch unreachable;

        const hints: posix.addrinfo = .{
            .flags = .{ .CANONNAME = options.canonical_name_buffer != null, .NUMERICSERV = true },
            .family = posix.AF.UNSPEC,
            .socktype = posix.SOCK.STREAM,
            .protocol = posix.IPPROTO.TCP,
            .canonname = null,
            .addr = null,
            .addrlen = 0,
            .next = null,
        };
        var res: ?*posix.addrinfo = null;
        const syscall: Syscall = try .start();
        while (true) {
            switch (posix.system.getaddrinfo(name_c.ptr, port_c.ptr, &hints, &res)) {
                @as(posix.system.EAI, @enumFromInt(0)) => {
                    syscall.finish();
                    break;
                },
                .SYSTEM => switch (posix.errno(-1)) {
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |e| {
                        syscall.finish();
                        return posix.unexpectedErrno(e);
                    },
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .ADDRFAMILY => return error.AddressFamilyUnsupported,
                        .AGAIN => return error.NameServerFailure,
                        .FAIL => return error.NameServerFailure,
                        .FAMILY => return error.AddressFamilyUnsupported,
                        .MEMORY => return error.SystemResources,
                        .NODATA => return error.UnknownHostName,
                        .NONAME => return error.UnknownHostName,
                        else => return error.Unexpected,
                    }
                },
            }
        }
        defer if (res) |some| posix.system.freeaddrinfo(some);

        var it = res;
        var canon_name: ?[*:0]const u8 = null;
        while (it) |info| : (it = info.next) {
            const addr = info.addr orelse continue;
            try resolved.putOne(t_io, .{ .address = addressFromPosix(@alignCast(@fieldParentPtr("any", addr))) });

            if (info.canonname) |n| {
                if (canon_name == null) {
                    canon_name = n;
                }
            }
        }
        if (canon_name) |n| {
            if (copyCanon(options.canonical_name_buffer, std.mem.sliceTo(n, 0))) |canon| {
                try resolved.putOne(t_io, .{ .canonical_name = canon });
            }
        }
        return;
    }

    return error.OptionUnsupported;
}

fn lockStderr(userdata: ?*anyopaque, terminal_mode: ?Io.Terminal.Mode) Io.Cancelable!Io.LockedStderr {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const current_thread_id = Thread.currentId();

    if (@atomicLoad(std.Thread.Id, &t.stderr_mutex_locker, .unordered) != current_thread_id) {
        mutexLock(&t.stderr_mutex);
        assert(t.stderr_mutex_lock_count == 0);
        @atomicStore(std.Thread.Id, &t.stderr_mutex_locker, current_thread_id, .unordered);
    }
    t.stderr_mutex_lock_count += 1;

    return initLockedStderr(t, terminal_mode);
}

fn tryLockStderr(userdata: ?*anyopaque, terminal_mode: ?Io.Terminal.Mode) Io.Cancelable!?Io.LockedStderr {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const current_thread_id = Thread.currentId();

    if (@atomicLoad(std.Thread.Id, &t.stderr_mutex_locker, .unordered) != current_thread_id) {
        if (!t.stderr_mutex.tryLock()) return null;
        assert(t.stderr_mutex_lock_count == 0);
        @atomicStore(std.Thread.Id, &t.stderr_mutex_locker, current_thread_id, .unordered);
    }
    t.stderr_mutex_lock_count += 1;

    return try initLockedStderr(t, terminal_mode);
}

fn initLockedStderr(t: *Threaded, terminal_mode: ?Io.Terminal.Mode) Io.Cancelable!Io.LockedStderr {
    if (!t.stderr_writer_initialized) {
        const io_t = io(t);
        if (is_windows) t.stderr_writer.file = .stderr();
        t.stderr_writer.io = io_t;
        t.stderr_writer_initialized = true;
        t.scanEnviron();
        const NO_COLOR = t.environ.exist.NO_COLOR;
        const CLICOLOR_FORCE = t.environ.exist.CLICOLOR_FORCE;
        t.stderr_mode = terminal_mode orelse try .detect(io_t, t.stderr_writer.file, NO_COLOR, CLICOLOR_FORCE);
    }
    return .{
        .file_writer = &t.stderr_writer,
        .terminal_mode = terminal_mode orelse t.stderr_mode,
    };
}

fn unlockStderr(userdata: ?*anyopaque) void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    if (t.stderr_writer.err == null) t.stderr_writer.interface.flush() catch {};
    if (t.stderr_writer.err) |err| {
        switch (err) {
            error.Canceled => recancelInner(),
            else => {},
        }
        t.stderr_writer.err = null;
    }
    t.stderr_writer.interface.end = 0;
    t.stderr_writer.interface.buffer = &.{};

    t.stderr_mutex_lock_count -= 1;
    if (t.stderr_mutex_lock_count == 0) {
        @atomicStore(std.Thread.Id, &t.stderr_mutex_locker, Thread.invalid_id, .unordered);
        mutexUnlock(&t.stderr_mutex);
    }
}

fn processCurrentPath(userdata: ?*anyopaque, buffer: []u8) process.CurrentPathError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    if (is_windows) {
        var wtf16le_buf: [windows.PATH_MAX_WIDE:0]u16 = undefined;
        const n = windows.ntdll.RtlGetCurrentDirectory_U(wtf16le_buf.len * 2 + 2, &wtf16le_buf) / 2;
        if (n == 0) return error.Unexpected;
        assert(n <= wtf16le_buf.len);
        const wtf16le_slice = wtf16le_buf[0..n];
        var end_index: usize = 0;
        var it = std.unicode.Wtf16LeIterator.init(wtf16le_slice);
        while (it.nextCodepoint()) |codepoint| {
            const seq_len = std.unicode.utf8CodepointSequenceLength(codepoint) catch unreachable;
            if (end_index + seq_len >= buffer.len)
                return error.NameTooLong;
            end_index += std.unicode.wtf8Encode(codepoint, buffer[end_index..]) catch unreachable;
        }
        return end_index;
    } else if (native_os == .wasi and !builtin.link_libc) {
        if (buffer.len == 0) return error.NameTooLong;
        buffer[0] = '.';
        return 1;
    }

    const err: posix.E = if (builtin.link_libc) err: {
        const c_err = if (std.c.getcwd(buffer.ptr, buffer.len)) |_| 0 else std.c._errno().*;
        break :err @enumFromInt(c_err);
    } else err: {
        break :err posix.errno(posix.system.getcwd(buffer.ptr, buffer.len));
    };
    switch (err) {
        .SUCCESS => return std.mem.findScalar(u8, buffer, 0).?,
        .NOENT => return error.CurrentDirUnlinked,
        .RANGE => return error.NameTooLong,
        .FAULT => |e| return errnoBug(e),
        .INVAL => |e| return errnoBug(e),
        else => return posix.unexpectedErrno(err),
    }
}

fn processSetCurrentDir(userdata: ?*anyopaque, dir: Dir) process.SetCurrentDirError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (native_os == .wasi) return error.OperationUnsupported;

    if (is_windows) {
        var dir_path_buf: [windows.PATH_MAX_WIDE]u16 = undefined;
        const dir_path = try GetFinalPathNameByHandle(dir.handle, .{}, &dir_path_buf);
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.RtlSetCurrentDirectory_U(&.init(dir_path))) {
            .SUCCESS => return syscall.finish(),
            .OBJECT_NAME_INVALID => return syscall.fail(error.BadPathName),
            .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .OBJECT_PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .NO_MEDIA_IN_DEVICE => return syscall.fail(error.NoDevice),
            .INVALID_PARAMETER => |err| return syscall.ntstatusBug(err),
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .OBJECT_PATH_SYNTAX_BAD => |err| return syscall.ntstatusBug(err),
            .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            else => |status| return syscall.unexpectedNtstatus(status),
        };
    }

    return fchdir(dir.handle);
}

fn processSetCurrentPath(userdata: ?*anyopaque, path: []const u8) process.SetCurrentPathError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (native_os == .wasi) return error.OperationUnsupported;

    if (is_windows) {
        var path_w_buf: [windows.PATH_MAX_WIDE]u16 = undefined;
        const len = std.unicode.calcWtf16LeLen(path) catch return error.InvalidWtf8;
        if (len > path_w_buf.len) return error.NameTooLong;
        const path_w_len = std.unicode.wtf8ToWtf16Le(&path_w_buf, path) catch |err| switch (err) {
            error.InvalidWtf8 => unreachable, // already validated
        };
        const path_w = path_w_buf[0..path_w_len];

        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.RtlSetCurrentDirectory_U(&.init(path_w))) {
            .SUCCESS => return syscall.finish(),
            .OBJECT_NAME_INVALID => return syscall.fail(error.BadPathName),
            .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .OBJECT_PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .NO_MEDIA_IN_DEVICE => return syscall.fail(error.NoDevice),
            .INVALID_PARAMETER => |err| return syscall.ntstatusBug(err),
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .OBJECT_PATH_SYNTAX_BAD => |err| return syscall.ntstatusBug(err),
            .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            else => |status| return syscall.unexpectedNtstatus(status),
        };
    }

    return chdir(path);
}

pub const PosixAddress = extern union {
    any: posix.sockaddr,
    in: posix.sockaddr.in,
    in6: posix.sockaddr.in6,
};

const UnixAddress = extern union {
    any: posix.sockaddr,
    un: posix.sockaddr.un,
};

pub fn posixAddressFamily(a: *const IpAddress) posix.sa_family_t {
    return switch (a.*) {
        .ip4 => posix.AF.INET,
        .ip6 => posix.AF.INET6,
    };
}

pub fn addressFromPosix(posix_address: *const PosixAddress) IpAddress {
    return switch (posix_address.any.family) {
        posix.AF.INET => .{ .ip4 = address4FromPosix(&posix_address.in) },
        posix.AF.INET6 => .{ .ip6 = address6FromPosix(&posix_address.in6) },
        else => .{ .ip4 = .loopback(0) },
    };
}

pub fn addressToPosix(a: *const IpAddress, storage: *PosixAddress) posix.socklen_t {
    return switch (a.*) {
        .ip4 => |ip4| {
            storage.in = address4ToPosix(ip4);
            return @sizeOf(posix.sockaddr.in);
        },
        .ip6 => |*ip6| {
            storage.in6 = address6ToPosix(ip6);
            return @sizeOf(posix.sockaddr.in6);
        },
    };
}

fn addressUnixToPosix(a: *const net.UnixAddress, storage: *UnixAddress) posix.socklen_t {
    storage.un.family = posix.AF.UNIX;
    var path_len = switch (native_os) {
        .windows => @min(a.path.len, storage.un.path.len),
        else => a.path.len,
    };
    // With the AFD API, `sockaddr.un` is purely informational, so
    // use a suffix which is usually the most relevant part of a path.
    @memcpy(storage.un.path[0..path_len], a.path[a.path.len - path_len ..]);
    if (storage.un.path.len - path_len > 0) {
        @branchHint(.likely);
        storage.un.path[path_len] = 0;
        path_len += 1;
    }
    switch (native_os) {
        .windows => {
            if (storage.un.path[0] == 0) @memset(storage.un.path[path_len..], 0);
            return @sizeOf(posix.sockaddr.un);
        },
        else => return @intCast(@offsetOf(posix.sockaddr.un, "path") + path_len),
    }
}

fn address4FromPosix(in: *const posix.sockaddr.in) net.Ip4Address {
    return .{
        .port = std.mem.bigToNative(u16, in.port),
        .bytes = @bitCast(in.addr),
    };
}

fn address6FromPosix(in6: *const posix.sockaddr.in6) net.Ip6Address {
    return .{
        .port = std.mem.bigToNative(u16, in6.port),
        .bytes = in6.addr,
        .flow = in6.flowinfo,
        .interface = .{ .index = in6.scope_id },
    };
}

fn address4ToPosix(a: net.Ip4Address) posix.sockaddr.in {
    return .{
        .port = std.mem.nativeToBig(u16, a.port),
        .addr = @bitCast(a.bytes),
    };
}

fn address6ToPosix(a: *const net.Ip6Address) posix.sockaddr.in6 {
    return .{
        .port = std.mem.nativeToBig(u16, a.port),
        .flowinfo = a.flow,
        .addr = a.bytes,
        .scope_id = a.interface.index,
    };
}

pub fn errnoBug(err: posix.E) Io.UnexpectedError {
    if (is_debug) std.debug.panic("programmer bug caused syscall error: {t}", .{err});
    return error.Unexpected;
}

pub fn posixSocketModeProtocol(family: posix.sa_family_t, mode: net.Socket.Mode, protocol: ?net.Protocol) !struct { u32, u32 } {
    return .{
        switch (mode) {
            .stream => posix.SOCK.STREAM,
            .dgram => posix.SOCK.DGRAM,
            .seqpacket => posix.SOCK.SEQPACKET,
            .raw => posix.SOCK.RAW,
            .rdm => posix.SOCK.RDM,
        },
        if (protocol) |p| @intFromEnum(p) else if (is_windows) switch (family) {
            posix.AF.UNIX => switch (mode) {
                .stream => 0,
                else => return error.ProtocolUnsupportedByAddressFamily,
            },
            posix.AF.INET, posix.AF.INET6 => @intFromEnum(@as(net.Protocol, switch (mode) {
                .stream => .tcp,
                .dgram => .udp,
                else => return error.ProtocolUnsupportedByAddressFamily,
            })),
            else => return error.ProtocolUnsupportedByAddressFamily,
        } else 0,
    };
}

pub fn recoverableOsBugDetected() void {
    if (is_debug) unreachable;
}

pub fn clockToPosix(clock: Io.Clock) posix.clockid_t {
    return switch (clock) {
        .real => posix.CLOCK.REALTIME,
        .awake => switch (native_os) {
            .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => posix.CLOCK.UPTIME_RAW,
            else => posix.CLOCK.MONOTONIC,
        },
        .boot => switch (native_os) {
            .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => posix.CLOCK.MONOTONIC_RAW,
            // On freebsd derivatives, use MONOTONIC_FAST as currently there's
            // no precision tradeoff.
            .freebsd, .dragonfly => posix.CLOCK.MONOTONIC_FAST,
            // On linux, use BOOTTIME instead of MONOTONIC as it ticks while
            // suspended.
            .linux => posix.CLOCK.BOOTTIME,
            // On other posix systems, MONOTONIC is generally the fastest and
            // ticks while suspended.
            else => posix.CLOCK.MONOTONIC,
        },
        .cpu_process => posix.CLOCK.PROCESS_CPUTIME_ID,
        .cpu_thread => posix.CLOCK.THREAD_CPUTIME_ID,
    };
}

fn clockToWasi(clock: Io.Clock) std.os.wasi.clockid_t {
    return switch (clock) {
        .real => .REALTIME,
        .awake => .MONOTONIC,
        .boot => .MONOTONIC,
        .cpu_process => .PROCESS_CPUTIME_ID,
        .cpu_thread => .THREAD_CPUTIME_ID,
    };
}

pub const linux_statx_request: std.os.linux.STATX = .{
    .TYPE = true,
    .MODE = true,
    .ATIME = true,
    .MTIME = true,
    .CTIME = true,
    .INO = true,
    .SIZE = true,
    .NLINK = true,
    .BLOCKS = true,
};

pub const linux_statx_check: std.os.linux.STATX = .{
    .TYPE = true,
    .MODE = true,
    .ATIME = false,
    .MTIME = true,
    .CTIME = true,
    .INO = true,
    .SIZE = true,
    .NLINK = true,
    .BLOCKS = false,
};

pub fn statFromLinux(stx: *const std.os.linux.Statx) Io.UnexpectedError!File.Stat {
    const actual_mask_int: u32 = @bitCast(stx.mask);
    const wanted_mask_int: u32 = @bitCast(linux_statx_check);
    if ((actual_mask_int | wanted_mask_int) != actual_mask_int) return error.Unexpected;

    return .{
        .inode = stx.ino,
        .nlink = stx.nlink,
        .size = stx.size,
        .permissions = .fromMode(stx.mode),
        .kind = statxKind(stx.mode),
        .atime = if (!stx.mask.ATIME) null else .{
            .nanoseconds = @intCast(@as(i128, stx.atime.sec) * std.time.ns_per_s + stx.atime.nsec),
        },
        .mtime = .{ .nanoseconds = @intCast(@as(i128, stx.mtime.sec) * std.time.ns_per_s + stx.mtime.nsec) },
        .ctime = .{ .nanoseconds = @intCast(@as(i128, stx.ctime.sec) * std.time.ns_per_s + stx.ctime.nsec) },
        .block_size = if (stx.mask.BLOCKS) stx.blksize else 1,
    };
}

pub fn statxKind(stx_mode: u16) File.Kind {
    return switch (stx_mode & std.os.linux.S.IFMT) {
        std.os.linux.S.IFDIR => .directory,
        std.os.linux.S.IFCHR => .character_device,
        std.os.linux.S.IFBLK => .block_device,
        std.os.linux.S.IFREG => .file,
        std.os.linux.S.IFIFO => .named_pipe,
        std.os.linux.S.IFLNK => .sym_link,
        std.os.linux.S.IFSOCK => .unix_domain_socket,
        else => .unknown,
    };
}

pub fn statFromPosix(st: *const posix.Stat) File.Stat {
    const atime = st.atime();
    const mtime = st.mtime();
    const ctime = st.ctime();
    return .{
        .inode = st.ino,
        .nlink = st.nlink,
        .size = @bitCast(st.size),
        .permissions = .fromMode(st.mode),
        .kind = k: {
            const m = st.mode & posix.S.IFMT;
            switch (m) {
                posix.S.IFBLK => break :k .block_device,
                posix.S.IFCHR => break :k .character_device,
                posix.S.IFDIR => break :k .directory,
                posix.S.IFIFO => break :k .named_pipe,
                posix.S.IFLNK => break :k .sym_link,
                posix.S.IFREG => break :k .file,
                posix.S.IFSOCK => break :k .unix_domain_socket,
                else => {},
            }
            if (native_os == .illumos) switch (m) {
                posix.S.IFDOOR => break :k .door,
                posix.S.IFPORT => break :k .event_port,
                else => {},
            };

            break :k .unknown;
        },
        .atime = timestampFromPosix(&atime),
        .mtime = timestampFromPosix(&mtime),
        .ctime = timestampFromPosix(&ctime),
        .block_size = @intCast(st.blksize),
    };
}

fn statFromWasi(st: *const std.os.wasi.filestat_t) File.Stat {
    return .{
        .inode = st.ino,
        .nlink = st.nlink,
        .size = @bitCast(st.size),
        .permissions = .default_file,
        .kind = switch (st.filetype) {
            .BLOCK_DEVICE => .block_device,
            .CHARACTER_DEVICE => .character_device,
            .DIRECTORY => .directory,
            .SYMBOLIC_LINK => .sym_link,
            .REGULAR_FILE => .file,
            .SOCKET_STREAM, .SOCKET_DGRAM => .unix_domain_socket,
            else => .unknown,
        },
        .atime = .fromNanoseconds(st.atim),
        .mtime = .fromNanoseconds(st.mtim),
        .ctime = .fromNanoseconds(st.ctim),
        .block_size = 1,
    };
}

pub fn timestampFromPosix(timespec: *const posix.timespec) Io.Timestamp {
    return .{ .nanoseconds = nanosecondsFromPosix(timespec) };
}

pub fn nanosecondsFromPosix(timespec: *const posix.timespec) i96 {
    return @intCast(@as(i128, timespec.sec) * std.time.ns_per_s + timespec.nsec);
}

fn timestampToPosix(nanoseconds: i96) posix.timespec {
    if (builtin.zig_backend == .stage2_wasm) {
        // Workaround for https://codeberg.org/ziglang/zig/issues/30575
        return .{
            .sec = @intCast(@divTrunc(nanoseconds, std.time.ns_per_s)),
            .nsec = @intCast(@rem(nanoseconds, std.time.ns_per_s)),
        };
    }
    return .{
        .sec = @intCast(@divFloor(nanoseconds, std.time.ns_per_s)),
        .nsec = @intCast(@mod(nanoseconds, std.time.ns_per_s)),
    };
}

pub fn setTimestampToPosix(set_ts: File.SetTimestamp) posix.timespec {
    return switch (set_ts) {
        .unchanged => posix.UTIME.OMIT,
        .now => posix.UTIME.NOW,
        .new => |t| timestampToPosix(t.nanoseconds),
    };
}

pub fn pathToPosix(file_path: []const u8, buffer: *[posix.PATH_MAX]u8) Dir.PathNameError![:0]u8 {
    if (std.mem.containsAtLeastScalar2(u8, file_path, 0, 1)) return error.BadPathName;
    // >= rather than > to make room for the null byte
    if (file_path.len >= buffer.len) return error.NameTooLong;
    @memcpy(buffer[0..file_path.len], file_path);
    buffer[file_path.len] = 0;
    return buffer[0..file_path.len :0];
}

fn lookupDnsSearch(
    t: *Threaded,
    host_name: HostName,
    resolved: *Io.Queue(HostName.LookupResult),
    options: HostName.LookupOptions,
) (HostName.LookupError || Io.QueueClosedError)!void {
    const t_io = io(t);
    const rc = HostName.ResolvConf.init(t_io) catch return error.ResolvConfParseFailed;

    // Count dots, suppress search when >=ndots or name ends in
    // a dot, which is an explicit request for global scope.
    const dots = std.mem.countScalar(u8, host_name.bytes, '.');
    const search_len = if (dots >= rc.ndots or std.mem.endsWith(u8, host_name.bytes, ".")) 0 else rc.search_len;
    const search = rc.search_buffer[0..search_len];

    var canon_name = host_name.bytes;

    // Strip final dot for canon, fail if multiple trailing dots.
    if (std.mem.endsWith(u8, canon_name, ".")) canon_name.len -= 1;
    if (std.mem.endsWith(u8, canon_name, ".")) return error.UnknownHostName;

    // Name with search domain appended is set up in `canon_name`. This
    // both provides the desired default canonical name (if the requested
    // name is not a CNAME record) and serves as a buffer for passing the
    // full requested name to `lookupDns`.
    var local_buf: [HostName.max_len]u8 = undefined;
    const canon_buf = options.canonical_name_buffer orelse &local_buf;
    @memcpy(canon_buf[0..canon_name.len], canon_name);
    canon_buf[canon_name.len] = '.';
    var it = std.mem.tokenizeAny(u8, search, " \t");
    while (it.next()) |token| {
        @memcpy(canon_buf[canon_name.len + 1 ..][0..token.len], token);
        const lookup_canon_name = canon_buf[0 .. canon_name.len + 1 + token.len];
        if (t.lookupDns(lookup_canon_name, &rc, resolved, options)) |result| {
            return result;
        } else |err| switch (err) {
            error.UnknownHostName, error.NoAddressReturned => continue,
            else => |e| return e,
        }
    }

    const lookup_canon_name = canon_buf[0..canon_name.len];
    return t.lookupDns(lookup_canon_name, &rc, resolved, options);
}

fn lookupDns(
    t: *Threaded,
    lookup_canon_name: []const u8,
    rc: *const HostName.ResolvConf,
    resolved: *Io.Queue(HostName.LookupResult),
    options: HostName.LookupOptions,
) (HostName.LookupError || Io.QueueClosedError)!void {
    const t_io = io(t);
    const family_records: [2]struct { af: IpAddress.Family, rr: HostName.DnsRecord } = .{
        .{ .af = .ip6, .rr = .A },
        .{ .af = .ip4, .rr = .AAAA },
    };
    var query_buffers: [2][280]u8 = undefined;
    var answer_buffer: [2 * 512]u8 = undefined;
    var queries_buffer: [2][]const u8 = undefined;
    var answers_buffer: [2][]const u8 = undefined;
    var nq: usize = 0;
    var answer_buffer_i: usize = 0;

    for (family_records) |fr| {
        if (options.family != fr.af) {
            var entropy: [2]u8 = undefined;
            random(t, &entropy);
            const len = writeResolutionQuery(&query_buffers[nq], 0, lookup_canon_name, 1, fr.rr, entropy);
            queries_buffer[nq] = query_buffers[nq][0..len];
            nq += 1;
        }
    }

    var ip4_mapped_buffer: [HostName.ResolvConf.max_nameservers]IpAddress = undefined;
    const ip4_mapped = ip4_mapped_buffer[0..rc.nameservers_len];
    var any_ip6 = false;
    for (rc.nameservers(), ip4_mapped) |*ns, *m| {
        m.* = .{ .ip6 = .fromAny(ns.*) };
        any_ip6 = any_ip6 or ns.* == .ip6;
    }
    var socket = s: {
        if (any_ip6) ip6: {
            const ip6_addr: IpAddress = .{ .ip6 = .unspecified(0) };
            const socket = ip6_addr.bind(t_io, .{ .ip6_only = true, .mode = .dgram }) catch |err| switch (err) {
                error.AddressFamilyUnsupported => break :ip6,
                else => |e| return e,
            };
            break :s socket;
        }
        any_ip6 = false;
        const ip4_addr: IpAddress = .{ .ip4 = .unspecified(0) };
        const socket = try ip4_addr.bind(t_io, .{ .mode = .dgram });
        break :s socket;
    };
    defer socket.close(t_io);

    const mapped_nameservers = if (any_ip6) ip4_mapped else rc.nameservers();
    const queries = queries_buffer[0..nq];
    const answers = answers_buffer[0..queries.len];
    var answers_remaining = answers.len;
    for (answers) |*answer| answer.len = 0;

    // boot clock is chosen because time the computer is suspended should count
    // against time spent waiting for external messages to arrive.
    const clock: Io.Clock = .boot;
    var now_ts = clock.now(t_io);
    const final_ts = now_ts.addDuration(.fromSeconds(rc.timeout_seconds));
    const attempt_duration: Io.Duration = .{
        .nanoseconds = (std.time.ns_per_s / rc.attempts) * @as(i96, rc.timeout_seconds),
    };

    send: while (now_ts.nanoseconds < final_ts.nanoseconds) : (now_ts = clock.now(t_io)) {
        const max_messages = queries_buffer.len * HostName.ResolvConf.max_nameservers;
        {
            var message_buffer: [max_messages]net.OutgoingMessage = undefined;
            var message_i: usize = 0;
            for (queries, answers) |query, *answer| {
                if (answer.len != 0) continue;
                for (mapped_nameservers) |*ns| {
                    message_buffer[message_i] = .{
                        .address = ns,
                        .data_ptr = query.ptr,
                        .data_len = query.len,
                    };
                    message_i += 1;
                }
            }
            _ = netSendPosix(t, socket.handle, message_buffer[0..message_i], .{});
        }

        const timeout: Io.Timeout = .{ .deadline = .{
            .raw = now_ts.addDuration(attempt_duration),
            .clock = clock,
        } };

        while (true) {
            var message_buffer: [max_messages]net.IncomingMessage = @splat(.init);
            const buf = answer_buffer[answer_buffer_i..];
            const recv_err, const recv_n = socket.receiveManyTimeout(t_io, &message_buffer, buf, .{}, timeout);
            for (message_buffer[0..recv_n]) |*received_message| {
                const reply = received_message.data;
                // Ignore non-identifiable packets.
                if (reply.len < 4) continue;

                // Ignore replies from addresses we didn't send to.
                const ns = for (mapped_nameservers) |*ns| {
                    if (received_message.from.eql(ns)) break ns;
                } else {
                    continue;
                };

                // Find which query this answer goes with, if any.
                const query, const answer = for (queries, answers) |query, *answer| {
                    if (reply[0] == query[0] and reply[1] == query[1]) break .{ query, answer };
                } else {
                    continue;
                };
                if (answer.len != 0) continue;

                // Only accept positive or negative responses; retry immediately on
                // server failure, and ignore all other codes such as refusal.
                switch (reply[3] & 15) {
                    0, 3 => {
                        answer.* = reply;
                        answer_buffer_i += reply.len;
                        answers_remaining -= 1;
                        if (answer_buffer.len - answer_buffer_i == 0) break :send;
                        if (answers_remaining == 0) break :send;
                    },
                    2 => {
                        var retry_message: net.OutgoingMessage = .{
                            .address = ns,
                            .data_ptr = query.ptr,
                            .data_len = query.len,
                        };
                        _ = netSendPosix(t, socket.handle, (&retry_message)[0..1], .{});
                        continue;
                    },
                    else => continue,
                }
            }
            if (recv_err) |err| switch (err) {
                error.Canceled => return error.Canceled,
                error.Timeout => continue :send,
                else => continue,
            };
        }
    } else {
        return error.NameServerFailure;
    }

    var addresses_len: usize = 0;
    var canonical_name: ?HostName = null;

    for (answers) |answer| {
        var it = HostName.DnsResponse.init(answer) catch {
            // Here we could potentially add diagnostics to the results queue.
            continue;
        };
        while (it.next() catch {
            // Here we could potentially add diagnostics to the results queue.
            continue;
        }) |record| switch (record.rr) {
            .A => {
                const data = record.packet[record.data_off..][0..record.data_len];
                if (data.len != 4) return error.InvalidDnsARecord;
                try resolved.putOne(t_io, .{ .address = .{ .ip4 = .{
                    .bytes = data[0..4].*,
                    .port = options.port,
                } } });
                addresses_len += 1;
            },
            .AAAA => {
                const data = record.packet[record.data_off..][0..record.data_len];
                if (data.len != 16) return error.InvalidDnsAAAARecord;
                try resolved.putOne(t_io, .{ .address = .{ .ip6 = .{
                    .bytes = data[0..16].*,
                    .port = options.port,
                } } });
                addresses_len += 1;
            },
            .CNAME => {
                if (options.canonical_name_buffer) |buf| {
                    _, canonical_name = HostName.expand(
                        record.packet,
                        record.data_off,
                        buf,
                    ) catch return error.InvalidDnsCnameRecord;
                }
            },
            _ => continue,
        };
    }

    if (options.canonical_name_buffer != null) {
        try resolved.putOne(t_io, .{
            .canonical_name = canonical_name orelse .{ .bytes = lookup_canon_name },
        });
    }
    if (addresses_len == 0) return error.NoAddressReturned;
}

fn lookupHosts(
    t: *Threaded,
    host_name: HostName,
    resolved: *Io.Queue(HostName.LookupResult),
    options: HostName.LookupOptions,
) !void {
    const path_w = if (is_windows) path_w: {
        var path_w_buf: [windows.PATH_MAX_WIDE:0]u16 = undefined;
        const system_dir = windows.getSystemDirectoryWtf16Le();
        const suffix = [_]u16{
            '\\', 'd', 'r', 'i', 'v', 'e', 'r', 's', '\\', 'e', 't', 'c', '\\', 'h', 'o', 's', 't', 's',
        };
        @memcpy(path_w_buf[0..system_dir.len], system_dir);
        @memcpy(path_w_buf[system_dir.len..][0..suffix.len], &suffix);
        path_w_buf[system_dir.len + suffix.len] = 0;
        break :path_w wToPrefixedFileW(null, &path_w_buf, .{}) catch |err| switch (err) {
            error.FileNotFound,
            error.AccessDenied,
            => return error.UnknownHostName,

            error.Canceled => |e| return e,

            else => {
                // Here we could add more detailed diagnostics to the results queue.
                return error.DetectingNetworkConfigurationFailed;
            },
        };
    };
    const file = (if (is_windows)
        dirOpenFileWtf16(null, path_w.span(), .{})
    else
        dirOpenFile(t, .cwd(), "/etc/hosts", .{})) catch |err| switch (err) {
        error.FileNotFound,
        error.NotDir,
        error.AccessDenied,
        => return error.UnknownHostName,

        error.Canceled => |e| return e,

        else => {
            // Here we could add more detailed diagnostics to the results queue.
            return error.DetectingNetworkConfigurationFailed;
        },
    };
    defer fileClose(t, &.{file});

    var line_buf: [512]u8 = undefined;
    var file_reader = file.reader(t.io(), &line_buf);
    return t.lookupHostsReader(host_name, resolved, options, &file_reader.interface) catch |err| switch (err) {
        error.ReadFailed => switch (file_reader.err.?) {
            error.Canceled => |e| return e,
            else => {
                // Here we could add more detailed diagnostics to the results queue.
                return error.DetectingNetworkConfigurationFailed;
            },
        },
        error.Canceled,
        error.Closed,
        error.UnknownHostName,
        => |e| return e,
    };
}

fn lookupHostsReader(
    t: *Threaded,
    host_name: HostName,
    resolved: *Io.Queue(HostName.LookupResult),
    options: HostName.LookupOptions,
    reader: *Io.Reader,
) error{ ReadFailed, Canceled, UnknownHostName, Closed }!void {
    const t_io = io(t);
    var addresses_len: usize = 0;
    var canonical_name: ?HostName = null;
    while (true) {
        const line = reader.takeDelimiterExclusive('\n') catch |err| switch (err) {
            error.StreamTooLong => {
                // Skip lines that are too long.
                _ = reader.discardDelimiterInclusive('\n') catch |e| switch (e) {
                    error.EndOfStream => break,
                    error.ReadFailed => return error.ReadFailed,
                };
                continue;
            },
            error.ReadFailed => return error.ReadFailed,
            error.EndOfStream => break,
        };
        reader.toss(@min(1, reader.bufferedLen()));
        var split_it = std.mem.splitScalar(u8, if (is_windows and std.mem.endsWith(u8, line, "\r"))
            line[0 .. line.len - 1]
        else
            line, '#');
        const no_comment_line = split_it.first();

        var line_it = std.mem.tokenizeAny(u8, no_comment_line, " \t");
        const ip_text = line_it.next() orelse continue;
        var first_name_text: ?[]const u8 = null;
        while (line_it.next()) |name_text| {
            if (std.ascii.eqlIgnoreCase(name_text, host_name.bytes)) {
                if (first_name_text == null) first_name_text = name_text;
                break;
            }
        } else continue;

        if (canonical_name == null) {
            if (options.canonical_name_buffer) |buf| {
                if (HostName.init(first_name_text.?)) |name_text| {
                    if (name_text.bytes.len <= buf.len) {
                        const canonical_name_dest = buf[0..name_text.bytes.len];
                        @memcpy(canonical_name_dest, name_text.bytes);
                        canonical_name = .{ .bytes = canonical_name_dest };
                    }
                } else |_| {}
            }
        }

        if (options.family != .ip6) {
            if (IpAddress.parseIp4(ip_text, options.port)) |addr| {
                try resolved.putOne(t_io, .{ .address = addr });
                addresses_len += 1;
            } else |_| {}
        }
        if (options.family != .ip4) {
            if (IpAddress.parseIp6(ip_text, options.port)) |addr| {
                try resolved.putOne(t_io, .{ .address = addr });
                addresses_len += 1;
            } else |_| {}
        }
    }

    if (canonical_name) |canon_name| try resolved.putOne(t_io, .{ .canonical_name = canon_name });
    if (addresses_len == 0) return error.UnknownHostName;
}

/// Writes DNS resolution query packet data to `w`; at most 280 bytes.
fn writeResolutionQuery(q: *[280]u8, op: u4, dname: []const u8, class: u8, ty: HostName.DnsRecord, entropy: [2]u8) usize {
    // This implementation is ported from musl libc.
    // A more idiomatic "ziggy" implementation would be welcome.
    var name = dname;
    if (std.mem.endsWith(u8, name, ".")) name.len -= 1;
    assert(name.len <= 253);
    const n = 17 + name.len + @intFromBool(name.len != 0);

    // Construct query template - ID will be filled later
    q[0..2].* = entropy;
    @memset(q[2..n], 0);
    q[2] = @as(u8, op) * 8 + 1;
    q[5] = 1;
    @memcpy(q[13..][0..name.len], name);
    var i: usize = 13;
    var j: usize = undefined;
    while (q[i] != 0) : (i = j + 1) {
        j = i;
        while (q[j] != 0 and q[j] != '.') : (j += 1) {}
        // TODO determine the circumstances for this and whether or
        // not this should be an error.
        if (j - i - 1 > 62) unreachable;
        q[i - 1] = @intCast(j - i);
    }
    q[i + 1] = @intFromEnum(ty);
    q[i + 3] = class;
    return n;
}

const LookupDnsWindows = struct {
    threaded: *Threaded,
    thread: Thread.Handle,
    resolved: *Io.Queue(HostName.LookupResult),
    options: HostName.LookupOptions,
    results: windows.DNS.QUERY.RESULT,
    done: bool,

    fn completed(
        pQueryContext: ?*anyopaque,
        pQueryResults: *windows.DNS.QUERY.RESU
```
