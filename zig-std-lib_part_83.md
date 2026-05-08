```
(arg2),
          [arg3] "{edx}" (arg3),
          [arg4] "{esi}" (arg4),
        : .{ .memory = true });
}

pub fn syscall5(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32, arg5: u32) u32 {
    return asm volatile ("int $0x80"
        : [ret] "={eax}" (-> u32),
        : [number] "{eax}" (@intFromEnum(number)),
          [arg1] "{ebx}" (arg1),
          [arg2] "{ecx}" (arg2),
          [arg3] "{edx}" (arg3),
          [arg4] "{esi}" (arg4),
          [arg5] "{edi}" (arg5),
        : .{ .memory = true });
}

pub fn syscall6(
    number: SYS,
    arg1: u32,
    arg2: u32,
    arg3: u32,
    arg4: u32,
    arg5: u32,
    arg6: u32,
) u32 {
    // arg6 can't be passed to asm in a register because ebp might be reserved as the frame pointer
    // and there are no more GPRs available; so we'll need a memory operand for it. Adding that
    // memory operand means that on PIC we might need a reference to the GOT, which in turn needs
    // *its* own GPR, so we need to pass another arg in memory too! This is surprisingly hard to get
    // right, because we can't touch esp or ebp until we're done with the memory input (as that
    // input could be relative to esp or ebp).
    const args56: [2]u32 = .{ arg5, arg6 };
    return asm volatile (
        \\ push %[args56]
        \\ push %%ebp
        \\ mov 4(%%esp), %%ebp
        \\ mov %%edi, 4(%%esp)
        \\ // The saved %edi and %ebp are on the stack, and %ebp points to `args56`.
        \\ // Prepare the last two args, syscall, then pop the saved %ebp and %edi.
        \\ mov (%%ebp), %%edi
        \\ mov 4(%%ebp), %%ebp
        \\ int  $0x80
        \\ pop  %%ebp
        \\ pop  %%edi
        : [ret] "={eax}" (-> u32),
        : [number] "{eax}" (@intFromEnum(number)),
          [arg1] "{ebx}" (arg1),
          [arg2] "{ecx}" (arg2),
          [arg3] "{edx}" (arg3),
          [arg4] "{esi}" (arg4),
          [args56] "rm" (&args56),
        : .{ .memory = true });
}

pub fn socketcall(call: u32, args: [*]const u32) u32 {
    return asm volatile ("int $0x80"
        : [ret] "={eax}" (-> u32),
        : [number] "{eax}" (@intFromEnum(SYS.socketcall)),
          [arg1] "{ebx}" (call),
          [arg2] "{ecx}" (@intFromPtr(args)),
        : .{ .memory = true });
}

pub fn clone() callconv(.naked) u32 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         +8,   +12,   +16,   +20, +24,  +28, +32
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         eax,       ebx,   ecx,   edx,  esi, edi
    asm volatile (
        \\  pushl %%ebp
        \\  movl %%esp,%%ebp
        \\  pushl %%ebx
        \\  pushl %%esi
        \\  pushl %%edi
        \\  // Setup the arguments
        \\  movl 16(%%ebp),%%ebx
        \\  movl 12(%%ebp),%%ecx
        \\  andl $-16,%%ecx
        \\  subl $20,%%ecx
        \\  movl 20(%%ebp),%%eax
        \\  movl %%eax,4(%%ecx)
        \\  movl 8(%%ebp),%%eax
        \\  movl %%eax,0(%%ecx)
        \\  movl 24(%%ebp),%%edx
        \\  movl 28(%%ebp),%%esi
        \\  movl 32(%%ebp),%%edi
        \\  movl $120,%%eax // SYS_clone
        \\  int $128
        \\  testl %%eax,%%eax
        \\  jz 1f
        \\  popl %%edi
        \\  popl %%esi
        \\  popl %%ebx
        \\  popl %%ebp
        \\  retl
        \\
        \\1:
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\  .cfi_undefined %%eip
    );
    asm volatile (
        \\  xorl %%ebp,%%ebp
        \\
        \\  popl %%eax
        \\  calll *%%eax
        \\  movl %%eax,%%ebx
        \\  movl $1,%%eax // SYS_exit
        \\  int $128
    );
}

pub fn restore() callconv(.naked) noreturn {
    switch (builtin.zig_backend) {
        .stage2_c => asm volatile (
            \\ addl $4, %%esp
            \\ movl %[number], %%eax
            \\ int $0x80
            :
            : [number] "i" (@intFromEnum(SYS.sigreturn)),
        ),
        else => asm volatile (
            \\ addl $4, %%esp
            \\ int $0x80
            :
            : [number] "{eax}" (@intFromEnum(SYS.sigreturn)),
        ),
    }
}

pub fn restore_rt() callconv(.naked) noreturn {
    switch (builtin.zig_backend) {
        .stage2_c => asm volatile (
            \\ movl %[number], %%eax
            \\ int $0x80
            :
            : [number] "i" (@intFromEnum(SYS.rt_sigreturn)),
        ),
        else => asm volatile (
            \\ int $0x80
            :
            : [number] "{eax}" (@intFromEnum(SYS.rt_sigreturn)),
        ),
    }
}

pub const VDSO = struct {
    pub const CGT_SYM = "__vdso_clock_gettime";
    pub const CGT_VER = "LINUX_2.6";
};

pub const time_t = i32;

pub const user_desc = extern struct {
    entry_number: u32,
    base_addr: u32,
    limit: u32,
    flags: packed struct(u32) {
        seg_32bit: u1,
        contents: u2,
        read_exec_only: u1,
        limit_in_pages: u1,
        seg_not_present: u1,
        useable: u1,
        _: u25 = undefined,
    },
};

/// socketcall() call numbers
pub const SC = struct {
    pub const socket = 1;
    pub const bind = 2;
    pub const connect = 3;
    pub const listen = 4;
    pub const accept = 5;
    pub const getsockname = 6;
    pub const getpeername = 7;
    pub const socketpair = 8;
    pub const send = 9;
    pub const recv = 10;
    pub const sendto = 11;
    pub const recvfrom = 12;
    pub const shutdown = 13;
    pub const setsockopt = 14;
    pub const getsockopt = 15;
    pub const sendmsg = 16;
    pub const recvmsg = 17;
    pub const accept4 = 18;
    pub const recvmmsg = 19;
    pub const sendmmsg = 20;
};



---
File: /std/os/plan9/x86_64.zig
---

const plan9 = @import("../plan9.zig");
// TODO better inline asm

pub fn syscall1(sys: plan9.SYS, arg0: usize) usize {
    return asm volatile (
        \\push %%r8
        \\push $0
        \\syscall
        \\pop %%r11
        \\pop %%r11
        : [ret] "={rax}" (-> usize),
        : [arg0] "{r8}" (arg0),
          [syscall_number] "{rbp}" (@intFromEnum(sys)),
        : .{ .rcx = true, .rax = true, .rbp = true, .r11 = true, .memory = true });
}
pub fn syscall2(sys: plan9.SYS, arg0: usize, arg1: usize) usize {
    return asm volatile (
        \\push %%r9
        \\push %%r8
        \\push $0
        \\syscall
        \\pop %%r11
        \\pop %%r11
        \\pop %%r11
        : [ret] "={rax}" (-> usize),
        : [arg0] "{r8}" (arg0),
          [arg1] "{r9}" (arg1),
          [syscall_number] "{rbp}" (@intFromEnum(sys)),
        : .{ .rcx = true, .rax = true, .rbp = true, .r11 = true, .memory = true });
}
pub fn syscall3(sys: plan9.SYS, arg0: usize, arg1: usize, arg2: usize) usize {
    return asm volatile (
        \\push %%r10
        \\push %%r9
        \\push %%r8
        \\push $0
        \\syscall
        \\pop %%r11
        \\pop %%r11
        \\pop %%r11
        \\pop %%r11
        : [ret] "={rax}" (-> usize),
        : [arg0] "{r8}" (arg0),
          [arg1] "{r9}" (arg1),
          [arg2] "{r10}" (arg2),
          [syscall_number] "{rbp}" (@intFromEnum(sys)),
        : .{ .rcx = true, .rax = true, .rbp = true, .r11 = true, .memory = true });
}
pub fn syscall4(sys: plan9.SYS, arg0: usize, arg1: usize, arg2: usize, arg3: usize) usize {
    return asm volatile (
        \\push %%r11
        \\push %%r10
        \\push %%r9
        \\push %%r8
        \\push $0
        \\syscall
        \\pop %%r11
        \\pop %%r11
        \\pop %%r11
        \\pop %%r11
        \\pop %%r11
        : [ret] "={rax}" (-> usize),
        : [arg0] "{r8}" (arg0),
          [arg1] "{r9}" (arg1),
          [arg2] "{r10}" (arg2),
          [arg3] "{r11}" (arg3),
          [syscall_number] "{rbp}" (@intFromEnum(sys)),
        : .{ .rcx = true, .rax = true, .rbp = true, .r11 = true, .memory = true });
}



---
File: /std/os/uefi/protocol/absolute_pointer.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Event = uefi.Event;
const Guid = uefi.Guid;
const Status = uefi.Status;
const cc = uefi.cc;

/// Protocol for touchscreens.
pub const AbsolutePointer = extern struct {
    _reset: *const fn (*AbsolutePointer, bool) callconv(cc) Status,
    _get_state: *const fn (*const AbsolutePointer, *State) callconv(cc) Status,
    wait_for_input: Event,
    mode: *Mode,

    pub const ResetError = uefi.UnexpectedError || error{DeviceError};
    pub const GetStateError = uefi.UnexpectedError || error{ NotReady, DeviceError };

    /// Resets the pointer device hardware.
    pub fn reset(self: *AbsolutePointer, verify: bool) ResetError!void {
        switch (self._reset(self, verify)) {
            .success => {},
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Retrieves the current state of a pointer device.
    pub fn getState(self: *const AbsolutePointer) GetStateError!State {
        var state: State = undefined;
        switch (self._get_state(self, &state)) {
            .success => return state,
            .not_ready => return error.NotReady,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x8d59d32b,
        .time_mid = 0xc655,
        .time_high_and_version = 0x4ae9,
        .clock_seq_high_and_reserved = 0x9b,
        .clock_seq_low = 0x15,
        .node = [_]u8{ 0xf2, 0x59, 0x04, 0x99, 0x2a, 0x43 },
    };

    pub const Mode = extern struct {
        absolute_min_x: u64,
        absolute_min_y: u64,
        absolute_min_z: u64,
        absolute_max_x: u64,
        absolute_max_y: u64,
        absolute_max_z: u64,
        attributes: Attributes,

        pub const Attributes = packed struct(u32) {
            supports_alt_active: bool,
            supports_pressure_as_z: bool,
            _pad: u30 = 0,
        };
    };

    pub const State = extern struct {
        current_x: u64,
        current_y: u64,
        current_z: u64,
        active_buttons: ActiveButtons,

        pub const ActiveButtons = packed struct(u32) {
            touch_active: bool,
            alt_active: bool,
            _pad: u30 = 0,
        };
    };
};



---
File: /std/os/uefi/protocol/block_io.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Status = uefi.Status;
const cc = uefi.cc;

pub const BlockIo = extern struct {
    const Self = @This();

    revision: u64,
    media: *BlockMedia,

    _reset: *const fn (*BlockIo, extended_verification: bool) callconv(cc) Status,
    _read_blocks: *const fn (*BlockIo, media_id: u32, lba: u64, buffer_size: usize, buf: [*]u8) callconv(cc) Status,
    _write_blocks: *const fn (*BlockIo, media_id: u32, lba: u64, buffer_size: usize, buf: [*]const u8) callconv(cc) Status,
    _flush_blocks: *const fn (*BlockIo) callconv(cc) Status,

    pub const ResetError = uefi.UnexpectedError || error{DeviceError};
    pub const ReadBlocksError = uefi.UnexpectedError || error{
        DeviceError,
        NoMedia,
        BadBufferSize,
        InvalidParameter,
    };
    pub const WriteBlocksError = uefi.UnexpectedError || error{
        WriteProtected,
        NoMedia,
        MediaChanged,
        DeviceError,
        BadBufferSize,
        InvalidParameter,
    };
    pub const FlushBlocksError = uefi.UnexpectedError || error{
        DeviceError,
        NoMedia,
    };

    /// Resets the block device hardware.
    pub fn reset(self: *Self, extended_verification: bool) ResetError!void {
        switch (self._reset(self, extended_verification)) {
            .success => {},
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Reads the number of requested blocks from the device.
    pub fn readBlocks(self: *Self, media_id: u32, lba: u64, buf: []u8) ReadBlocksError!void {
        switch (self._read_blocks(self, media_id, lba, buf.len, buf.ptr)) {
            .success => {},
            .device_error => return error.DeviceError,
            .no_media => return error.NoMedia,
            .bad_buffer_size => return error.BadBufferSize,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Writes a specified number of blocks to the device.
    pub fn writeBlocks(self: *Self, media_id: u32, lba: u64, buf: []const u8) WriteBlocksError!void {
        switch (self._write_blocks(self, media_id, lba, buf.len, buf.ptr)) {
            .success => {},
            .write_protected => return error.WriteProtected,
            .no_media => return error.NoMedia,
            .media_changed => return error.MediaChanged,
            .device_error => return error.DeviceError,
            .bad_buffer_size => return error.BadBufferSize,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Flushes all modified data to a physical block device.
    pub fn flushBlocks(self: *Self) FlushBlocksError!void {
        switch (self._flush_blocks(self)) {
            .success => {},
            .device_error => return error.DeviceError,
            .no_media => return error.NoMedia,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = uefi.Guid{
        .time_low = 0x964e5b21,
        .time_mid = 0x6459,
        .time_high_and_version = 0x11d2,
        .clock_seq_high_and_reserved = 0x8e,
        .clock_seq_low = 0x39,
        .node = [_]u8{ 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b },
    };

    pub const BlockMedia = extern struct {
        /// The current media ID. If the media changes, this value is changed.
        media_id: u32,

        /// `true` if the media is removable; otherwise, `false`.
        removable_media: bool,
        /// `true` if there is a media currently present in the device
        media_present: bool,
        /// `true` if the `BlockIo` was produced to abstract
        /// partition structures on the disk. `false` if the `BlockIo` was
        /// produced to abstract the logical blocks on a hardware device.
        logical_partition: bool,
        /// `true` if the media is marked read-only otherwise, `false`. This field
        /// shows the read-only status as of the most recent `WriteBlocks()`
        read_only: bool,
        /// `true` if the WriteBlocks() function caches write data.
        write_caching: bool,

        /// The intrinsic block size of the device. If the media changes, then this
        // field is updated. Returns the number of bytes per logical block.
        block_size: u32,
        /// Supplies the alignment requirement for any buffer used in a data
        /// transfer. IoAlign values of 0 and 1 mean that the buffer can be
        /// placed anywhere in memory. Otherwise, IoAlign must be a power of
        /// 2, and the requirement is that the start address of a buffer must be
        /// evenly divisible by IoAlign with no remainder.
        io_align: u32,
        /// The last LBA on the device. If the media changes, then this field is updated.
        last_block: u64,

        // Revision 2
        lowest_aligned_lba: u64,
        logical_blocks_per_physical_block: u32,
        optimal_transfer_length_granularity: u32,
    };
};



---
File: /std/os/uefi/protocol/device_path.zig
---

const std = @import("../../../std.zig");
const mem = std.mem;
const uefi = std.os.uefi;
const Allocator = mem.Allocator;
const Guid = uefi.Guid;
const assert = std.debug.assert;

// All Device Path Nodes are byte-packed and may appear on any byte boundary.
// All code references to device path nodes must assume all fields are unaligned.

pub const DevicePath = extern struct {
    type: uefi.DevicePath.Type,
    subtype: u8,
    length: u16 align(1),

    pub const CreateFileDevicePathError = Allocator.Error;

    pub const guid align(8) = Guid{
        .time_low = 0x09576e91,
        .time_mid = 0x6d3f,
        .time_high_and_version = 0x11d2,
        .clock_seq_high_and_reserved = 0x8e,
        .clock_seq_low = 0x39,
        .node = [_]u8{ 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b },
    };

    /// Returns the next DevicePath node in the sequence, if any.
    pub fn next(self: *const DevicePath) ?*const DevicePath {
        const subtype: uefi.DevicePath.End.Subtype = @enumFromInt(self.subtype);
        if (self.type == .end and subtype == .end_entire) return null;
        const bytes: [*]const u8 = @ptrCast(self);
        return @ptrCast(bytes + self.length);
    }

    /// Calculates the total length of the device path structure in bytes, including the end of device path node.
    pub fn size(self: *const DevicePath) usize {
        var node = self;

        while (node.next()) |next_node| {
            node = next_node;
        }

        return (@intFromPtr(node) + node.length) - @intFromPtr(self);
    }

    /// Creates a file device path from the existing device path and a file path.
    pub fn createFileDevicePath(
        self: *const DevicePath,
        allocator: Allocator,
        path: []const u16,
    ) CreateFileDevicePathError!*const DevicePath {
        const path_size = self.size();

        // 2 * (path.len + 1) for the path and its null terminator, which are u16s
        // DevicePath for the extra node before the end
        var buf = try allocator.alloc(u8, path_size + 2 * (path.len + 1) + @sizeOf(DevicePath));

        @memcpy(buf[0..path_size], @as([*]const u8, @ptrCast(self))[0..path_size]);

        // Pointer to the copy of the end node of the current chain, which is - 4 from the buffer
        // as the end node itself is 4 bytes (type: u8 + subtype: u8 + length: u16).
        var new = @as(*uefi.DevicePath.Media.FilePathDevicePath, @ptrCast(buf.ptr + path_size - 4));

        new.type = .media;
        new.subtype = .file_path;
        new.length = @sizeOf(uefi.DevicePath.Media.FilePathDevicePath) + 2 * (@as(u16, @intCast(path.len)) + 1);

        // The same as new.getPath(), but not const as we're filling it in.
        var ptr = @as([*:0]align(1) u16, @ptrCast(@as([*]u8, @ptrCast(new)) + @sizeOf(uefi.DevicePath.Media.FilePathDevicePath)));

        for (path, 0..) |s, i|
            ptr[i] = s;

        ptr[path.len] = 0;

        var end = @as(*uefi.DevicePath.End.EndEntireDevicePath, @ptrCast(@constCast(@as(*DevicePath, @ptrCast(new)).next().?)));
        end.type = .end;
        end.subtype = .end_entire;
        end.length = @sizeOf(uefi.DevicePath.End.EndEntireDevicePath);

        return @as(*DevicePath, @ptrCast(buf.ptr));
    }

    pub fn getDevicePath(self: *const DevicePath) ?uefi.DevicePath {
        inline for (@typeInfo(uefi.DevicePath).@"union".fields) |ufield| {
            const enum_value = std.meta.stringToEnum(uefi.DevicePath.Type, ufield.name);

            // Got the associated union type for self.type, now
            // we need to initialize it and its subtype
            if (self.type == enum_value) {
                const subtype = self.initSubtype(ufield.type);
                if (subtype) |sb| {
                    // e.g. return .{ .hardware = .{ .pci = @ptrCast(...) } }
                    return @unionInit(uefi.DevicePath, ufield.name, sb);
                }
            }
        }

        return null;
    }

    pub fn initSubtype(self: *const DevicePath, comptime TUnion: type) ?TUnion {
        const type_info = @typeInfo(TUnion).@"union";
        const TTag = type_info.tag_type.?;

        inline for (type_info.fields) |subtype| {
            // The tag names match the union names, so just grab that off the enum
            const tag_val: u8 = @intFromEnum(@field(TTag, subtype.name));

            if (self.subtype == tag_val) {
                // e.g. expr = .{ .pci = @ptrCast(...) }
                return @unionInit(TUnion, subtype.name, @as(subtype.type, @ptrCast(self)));
            }
        }

        return null;
    }
};

comptime {
    assert(4 == @sizeOf(DevicePath));
    assert(1 == @alignOf(DevicePath));

    assert(0 == @offsetOf(DevicePath, "type"));
    assert(1 == @offsetOf(DevicePath, "subtype"));
    assert(2 == @offsetOf(DevicePath, "length"));
}



---
File: /std/os/uefi/protocol/edid.zig
---

const std = @import("../../../std.zig");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Handle = uefi.Handle;
const Status = uefi.Status;
const cc = uefi.cc;

/// EDID information for an active video output device
pub const Active = extern struct {
    size_of_edid: u32,
    edid: ?[*]u8,

    pub const guid align(8) = Guid{
        .time_low = 0xbd8c1056,
        .time_mid = 0x9f36,
        .time_high_and_version = 0x44ec,
        .clock_seq_high_and_reserved = 0x92,
        .clock_seq_low = 0xa8,
        .node = [_]u8{ 0xa6, 0x33, 0x7f, 0x81, 0x79, 0x86 },
    };
};

/// EDID information for a video output device
pub const Discovered = extern struct {
    size_of_edid: u32,
    edid: ?[*]u8,

    pub const guid align(8) = Guid{
        .time_low = 0x1c0c34f6,
        .time_mid = 0xd380,
        .time_high_and_version = 0x41fa,
        .clock_seq_high_and_reserved = 0xa0,
        .clock_seq_low = 0x49,
        .node = [_]u8{ 0x8a, 0xd0, 0x6c, 0x1a, 0x66, 0xaa },
    };
};

/// Override EDID information
pub const Override = extern struct {
    _get_edid: *const fn (*const Override, *const Handle, *Attributes, *usize, *?[*]u8) callconv(cc) Status,

    pub const GetEdidError = uefi.UnexpectedError || error{
        Unsupported,
    };

    /// Returns policy information and potentially a replacement EDID for the specified video output device.
    pub fn getEdid(self: *const Override, handle: Handle) GetEdidError!Edid {
        var size: usize = undefined;
        var ptr: ?[*]u8 = undefined;
        var attributes: Attributes = undefined;
        switch (self._get_edid(self, &handle, &attributes, &size, &ptr)) {
            .success => {},
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }

        return .{
            .attributes = attributes,
            .edid = if (ptr) |p| p[0..size] else null,
        };
    }

    pub const guid align(8) = Guid{
        .time_low = 0x48ecb431,
        .time_mid = 0xfb72,
        .time_high_and_version = 0x45c0,
        .clock_seq_high_and_reserved = 0xa9,
        .clock_seq_low = 0x22,
        .node = [_]u8{ 0xf4, 0x58, 0xfe, 0x04, 0x0b, 0xd5 },
    };

    pub const Edid = struct {
        attributes: Attributes,
        edid: ?[]u8,
    };

    pub const Attributes = packed struct(u32) {
        dont_override: bool,
        enable_hot_plug: bool,
        _pad: u30 = 0,
    };
};



---
File: /std/os/uefi/protocol/file.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Time = uefi.Time;
const Status = uefi.Status;
const cc = uefi.cc;

pub const File = extern struct {
    revision: u64,
    _open: *const fn (*const File, **File, [*:0]const u16, OpenMode, Attributes) callconv(cc) Status,
    _close: *const fn (*File) callconv(cc) Status,
    _delete: *const fn (*File) callconv(cc) Status,
    _read: *const fn (*File, *usize, [*]u8) callconv(cc) Status,
    _write: *const fn (*File, *usize, [*]const u8) callconv(cc) Status,
    _get_position: *const fn (*const File, *u64) callconv(cc) Status,
    _set_position: *const fn (*File, u64) callconv(cc) Status,
    _get_info: *const fn (*const File, *align(8) const Guid, *usize, ?[*]u8) callconv(cc) Status,
    _set_info: *const fn (*File, *align(8) const Guid, usize, [*]const u8) callconv(cc) Status,
    _flush: *const fn (*File) callconv(cc) Status,

    pub const OpenError = uefi.UnexpectedError || error{
        NotFound,
        NoMedia,
        MediaChanged,
        DeviceError,
        VolumeCorrupted,
        WriteProtected,
        AccessDenied,
        OutOfResources,
        VolumeFull,
        InvalidParameter,
    };
    pub const CloseError = uefi.UnexpectedError;
    pub const SeekError = uefi.UnexpectedError || error{
        Unsupported,
        DeviceError,
    };
    pub const ReadError = uefi.UnexpectedError || error{
        NoMedia,
        DeviceError,
        VolumeCorrupted,
        BufferTooSmall,
    };
    pub const WriteError = uefi.UnexpectedError || error{
        Unsupported,
        NoMedia,
        DeviceError,
        VolumeCorrupted,
        WriteProtected,
        AccessDenied,
        VolumeFull,
    };
    pub const GetInfoSizeError = uefi.UnexpectedError || error{
        Unsupported,
        NoMedia,
        DeviceError,
        VolumeCorrupted,
    };
    pub const GetInfoError = GetInfoSizeError || error{
        BufferTooSmall,
    };
    pub const SetInfoError = uefi.UnexpectedError || error{
        Unsupported,
        NoMedia,
        DeviceError,
        VolumeCorrupted,
        WriteProtected,
        AccessDenied,
        VolumeFull,
        BadBufferSize,
    };
    pub const FlushError = uefi.UnexpectedError || error{
        DeviceError,
        VolumeCorrupted,
        WriteProtected,
        AccessDenied,
        VolumeFull,
    };

    pub fn open(
        self: *const File,
        file_name: [*:0]const u16,
        mode: OpenMode,
        create_attributes: Attributes,
    ) OpenError!*File {
        var new: *File = undefined;
        switch (self._open(
            self,
            &new,
            file_name,
            mode,
            create_attributes,
        )) {
            .success => return new,
            .not_found => return error.NotFound,
            .no_media => return error.NoMedia,
            .media_changed => return error.MediaChanged,
            .device_error => return error.DeviceError,
            .volume_corrupted => return error.VolumeCorrupted,
            .write_protected => return error.WriteProtected,
            .access_denied => return error.AccessDenied,
            .out_of_resources => return error.OutOfResources,
            .volume_full => return error.VolumeFull,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn close(self: *File) CloseError!void {
        switch (self._close(self)) {
            .success => {},
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Delete the file.
    ///
    /// Returns true if the file was deleted, false if the file was not deleted, which is a warning
    /// according to the UEFI specification.
    pub fn delete(self: *File) uefi.UnexpectedError!bool {
        switch (self._delete(self)) {
            .success => return true,
            .warn_delete_failure => return false,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn read(self: *File, buffer: []u8) ReadError!usize {
        var size: usize = buffer.len;
        switch (self._read(self, &size, buffer.ptr)) {
            .success => return size,
            .no_media => return error.NoMedia,
            .device_error => return error.DeviceError,
            .volume_corrupted => return error.VolumeCorrupted,
            .buffer_too_small => return error.BufferTooSmall,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn write(self: *File, buffer: []const u8) WriteError!usize {
        var size: usize = buffer.len;
        switch (self._write(self, &size, buffer.ptr)) {
            .success => return size,
            .unsupported => return error.Unsupported,
            .no_media => return error.NoMedia,
            .device_error => return error.DeviceError,
            .volume_corrupted => return error.VolumeCorrupted,
            .write_protected => return error.WriteProtected,
            .access_denied => return error.AccessDenied,
            .volume_full => return error.VolumeFull,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn getPosition(self: *const File) SeekError!u64 {
        var position: u64 = undefined;
        switch (self._get_position(self, &position)) {
            .success => return position,
            .unsupported => return error.Unsupported,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn setPosition(self: *File, position: u64) SeekError!void {
        switch (self._set_position(self, position)) {
            .success => {},
            .unsupported => return error.Unsupported,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    fn seekBy(self: *File, offset: i64) SeekError!void {
        var pos = try self.getPosition();
        const seek_back = offset < 0;
        const amt = @abs(offset);
        if (seek_back) {
            pos += amt;
        } else {
            pos -= amt;
        }
        try self.setPosition(pos);
    }

    pub fn getInfoSize(self: *const File, comptime info: std.meta.Tag(Info)) GetInfoError!usize {
        const InfoType = @FieldType(Info, @tagName(info));

        var len: usize = 0;
        switch (self._get_info(self, &InfoType.guid, &len, null)) {
            .success, .buffer_too_small => return len,
            .unsupported => return error.Unsupported,
            .no_media => return error.NoMedia,
            .device_error => return error.DeviceError,
            .volume_corrupted => return error.VolumeCorrupted,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// If `buffer` is too small to contain all of the info, this function returns
    /// `Error.BufferTooSmall`. You should call `getInfoSize` first to determine
    /// how big the buffer should be to safely call this function.
    pub fn getInfo(
        self: *const File,
        comptime info: std.meta.Tag(Info),
        buffer: []align(@alignOf(@FieldType(Info, @tagName(info)))) u8,
    ) GetInfoError!*@FieldType(Info, @tagName(info)) {
        const InfoType = @FieldType(Info, @tagName(info));

        var len = buffer.len;
        switch (self._get_info(
            self,
            &InfoType.guid,
            &len,
            buffer.ptr,
        )) {
            .success => return @as(*InfoType, @ptrCast(buffer.ptr)),
            .buffer_too_small => return error.BufferTooSmall,
            .unsupported => return error.Unsupported,
            .no_media => return error.NoMedia,
            .device_error => return error.DeviceError,
            .volume_corrupted => return error.VolumeCorrupted,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn setInfo(
        self: *File,
        comptime info: std.meta.Tag(Info),
        data: *const @FieldType(Info, @tagName(info)),
    ) SetInfoError!void {
        const InfoType = @FieldType(Info, @tagName(info));

        const attached_str: [*:0]const u16 = switch (info) {
            .file => data.getFileName(),
            .file_system, .volume_label => data.getVolumeLabel(),
        };
        const attached_str_len = std.mem.sliceTo(attached_str, 0).len;

        // add the length (not +1 for sentinel) because `@sizeOf(InfoType)`
        // already contains the first utf16 char
        const len = @sizeOf(InfoType) + (attached_str_len * 2);

        switch (self._set_info(self, &InfoType.guid, len, @ptrCast(data))) {
            .success => {},
            .unsupported => return error.Unsupported,
            .no_media => return error.NoMedia,
            .device_error => return error.DeviceError,
            .volume_corrupted => return error.VolumeCorrupted,
            .write_protected => return error.WriteProtected,
            .access_denied => return error.AccessDenied,
            .volume_full => return error.VolumeFull,
            .bad_buffer_size => return error.BadBufferSize,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn flush(self: *File) FlushError!void {
        switch (self._flush(self)) {
            .success => {},
            .device_error => return error.DeviceError,
            .volume_corrupted => return error.VolumeCorrupted,
            .write_protected => return error.WriteProtected,
            .access_denied => return error.AccessDenied,
            .volume_full => return error.VolumeFull,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const OpenMode = enum(u64) {
        pub const Bits = packed struct(u64) {
            // 0x0000000000000001
            read: bool = false,
            // 0x0000000000000002
            write: bool = false,
            _pad: u61 = 0,
            // 0x8000000000000000
            create: bool = false,
        };

        read = @bitCast(Bits{ .read = true }),
        read_write = @bitCast(Bits{ .read = true, .write = true }),
        read_write_create = @bitCast(Bits{ .read = true, .write = true, .create = true }),
    };

    pub const Attributes = packed struct(u64) {
        // 0x0000000000000001
        read_only: bool = false,
        // 0x0000000000000002
        hidden: bool = false,
        // 0x0000000000000004
        system: bool = false,
        // 0x0000000000000008
        reserved: bool = false,
        // 0x0000000000000010
        directory: bool = false,
        // 0x0000000000000020
        archive: bool = false,
        _pad: u58 = 0,
    };

    pub const Info = union(enum) {
        file: Info.File,
        file_system: FileSystem,
        volume_label: VolumeLabel,

        pub const File = extern struct {
            size: u64,
            file_size: u64,
            physical_size: u64,
            create_time: Time,
            last_access_time: Time,
            modification_time: Time,
            attribute: Attributes,
            _file_name: u16,

            pub fn getFileName(self: *const Info.File) [*:0]const u16 {
                return @as([*:0]const u16, @ptrCast(&self._file_name));
            }

            pub const guid align(8) = Guid{
                .time_low = 0x09576e92,
                .time_mid = 0x6d3f,
                .time_high_and_version = 0x11d2,
                .clock_seq_high_and_reserved = 0x8e,
                .clock_seq_low = 0x39,
                .node = [_]u8{ 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b },
            };
        };

        pub const FileSystem = extern struct {
            size: u64,
            read_only: bool,
            volume_size: u64,
            free_space: u64,
            block_size: u32,
            _volume_label: u16,

            pub fn getVolumeLabel(self: *const FileSystem) [*:0]const u16 {
                return @as([*:0]const u16, @ptrCast(&self._volume_label));
            }

            pub const guid align(8) = Guid{
                .time_low = 0x09576e93,
                .time_mid = 0x6d3f,
                .time_high_and_version = 0x11d2,
                .clock_seq_high_and_reserved = 0x8e,
                .clock_seq_low = 0x39,
                .node = [_]u8{ 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b },
            };
        };

        pub const VolumeLabel = extern struct {
            _volume_label: u16,

            pub fn getVolumeLabel(self: *const VolumeLabel) [*:0]const u16 {
                return @as([*:0]const u16, @ptrCast(&self._volume_label));
            }

            pub const guid align(8) = Guid{
                .time_low = 0xdb47d7d3,
                .time_mid = 0xfe81,
                .time_high_and_version = 0x11d3,
                .clock_seq_high_and_reserved = 0x9a,
                .clock_seq_low = 0x35,
                .node = [_]u8{ 0x00, 0x90, 0x27, 0x3f, 0xc1, 0x4d },
            };
        };
    };

    const end_of_file: u64 = 0xffffffffffffffff;
};



---
File: /std/os/uefi/protocol/graphics_output.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Status = uefi.Status;
const cc = uefi.cc;

pub const GraphicsOutput = extern struct {
    _query_mode: *const fn (*const GraphicsOutput, u32, *usize, **Mode.Info) callconv(cc) Status,
    _set_mode: *const fn (*GraphicsOutput, u32) callconv(cc) Status,
    _blt: *const fn (*GraphicsOutput, ?[*]BltPixel, BltOperation, usize, usize, usize, usize, usize, usize, usize) callconv(cc) Status,
    mode: *Mode,

    pub const QueryModeError = uefi.UnexpectedError || error{
        DeviceError,
        InvalidParameter,
    };
    pub const SetModeError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };
    pub const BltError = uefi.UnexpectedError || error{
        InvalidParameter,
        DeviceError,
    };

    /// Returns information for an available graphics mode that the graphics device and the set of active video output devices supports.
    pub fn queryMode(self: *const GraphicsOutput, mode_id: u32) QueryModeError!*Mode.Info {
        var size_of_info: usize = undefined;
        var info: *Mode.Info = undefined;
        switch (self._query_mode(self, mode_id, &size_of_info, &info)) {
            .success => return info,
            .device_error => return error.DeviceError,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Set the video device into the specified mode and clears the visible portions of the output display to black.
    pub fn setMode(self: *GraphicsOutput, mode_id: u32) SetModeError!void {
        switch (self._set_mode(self, mode_id)) {
            .success => {},
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Blt a rectangle of pixels on the graphics screen. Blt stands for BLock Transfer.
    pub fn blt(
        self: *GraphicsOutput,
        blt_buffer: ?[*]BltPixel,
        blt_operation: BltOperation,
        source_x: usize,
        source_y: usize,
        destination_x: usize,
        destination_y: usize,
        width: usize,
        height: usize,
        delta: usize,
    ) BltError!void {
        switch (self._blt(
            self,
            blt_buffer,
            blt_operation,
            source_x,
            source_y,
            destination_x,
            destination_y,
            width,
            height,
            delta,
        )) {
            .success => {},
            .device_error => return error.DeviceError,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x9042a9de,
        .time_mid = 0x23dc,
        .time_high_and_version = 0x4a38,
        .clock_seq_high_and_reserved = 0x96,
        .clock_seq_low = 0xfb,
        .node = [_]u8{ 0x7a, 0xde, 0xd0, 0x80, 0x51, 0x6a },
    };

    pub const Mode = extern struct {
        max_mode: u32,
        mode: u32,
        info: *Info,
        size_of_info: usize,
        frame_buffer_base: u64,
        frame_buffer_size: usize,

        pub const Info = extern struct {
            version: u32,
            horizontal_resolution: u32,
            vertical_resolution: u32,
            pixel_format: PixelFormat,
            pixel_information: PixelBitmask,
            pixels_per_scan_line: u32,
        };
    };

    pub const PixelFormat = enum(u32) {
        red_green_blue_reserved_8_bit_per_color,
        blue_green_red_reserved_8_bit_per_color,
        bit_mask,
        blt_only,
    };

    pub const PixelBitmask = extern struct {
        red_mask: u32,
        green_mask: u32,
        blue_mask: u32,
        reserved_mask: u32,
    };

    pub const BltPixel = extern struct {
        blue: u8,
        green: u8,
        red: u8,
        reserved: u8 = undefined,
    };

    pub const BltOperation = enum(u32) {
        blt_video_fill,
        blt_video_to_blt_buffer,
        blt_buffer_to_video,
        blt_video_to_video,
        graphics_output_blt_operation_max,
    };
};



---
File: /std/os/uefi/protocol/hii_database.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Status = uefi.Status;
const hii = uefi.hii;
const cc = uefi.cc;

/// Database manager for HII-related data structures.
pub const HiiDatabase = extern struct {
    _new_package_list: Status, // TODO
    _remove_package_list: *const fn (*HiiDatabase, hii.Handle) callconv(cc) Status,
    _update_package_list: *const fn (*HiiDatabase, hii.Handle, *const hii.PackageList) callconv(cc) Status,
    _list_package_lists: *const fn (*const HiiDatabase, u8, ?*const Guid, *usize, [*]hii.Handle) callconv(cc) Status,
    _export_package_lists: *const fn (*const HiiDatabase, ?hii.Handle, *usize, [*]hii.PackageList) callconv(cc) Status,
    _register_package_notify: Status, // TODO
    _unregister_package_notify: Status, // TODO
    _find_keyboard_layouts: Status, // TODO
    _get_keyboard_layout: Status, // TODO
    _set_keyboard_layout: Status, // TODO
    _get_package_list_handle: Status, // TODO

    pub const RemovePackageListError = uefi.UnexpectedError || error{NotFound};
    pub const UpdatePackageListError = uefi.UnexpectedError || error{
        OutOfResources,
        InvalidParameter,
        NotFound,
    };
    pub const ListPackageListsError = uefi.UnexpectedError || error{
        BufferTooSmall,
        InvalidParameter,
        NotFound,
    };
    pub const ExportPackageListError = uefi.UnexpectedError || error{
        BufferTooSmall,
        InvalidParameter,
        NotFound,
    };

    /// Removes a package list from the HII database.
    pub fn removePackageList(self: *HiiDatabase, handle: hii.Handle) RemovePackageListError!void {
        switch (self._remove_package_list(self, handle)) {
            .success => {},
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Update a package list in the HII database.
    pub fn updatePackageList(
        self: *HiiDatabase,
        handle: hii.Handle,
        buffer: *const hii.PackageList,
    ) UpdatePackageListError!void {
        switch (self._update_package_list(self, handle, buffer)) {
            .success => {},
            .out_of_resources => return error.OutOfResources,
            .invalid_parameter => return error.InvalidParameter,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Determines the handles that are currently active in the database.
    pub fn listPackageLists(
        self: *const HiiDatabase,
        package_type: u8,
        package_guid: ?*const Guid,
        handles: []hii.Handle,
    ) ListPackageListsError![]hii.Handle {
        var len: usize = handles.len;
        switch (self._list_package_lists(
            self,
            package_type,
            package_guid,
            &len,
            handles.ptr,
        )) {
            .success => return handles[0..len],
            .buffer_too_small => return error.BufferTooSmall,
            .invalid_parameter => return error.InvalidParameter,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Exports the contents of one or all package lists in the HII database into a buffer.
    pub fn exportPackageLists(
        self: *const HiiDatabase,
        handle: ?hii.Handle,
        buffer: []hii.PackageList,
    ) ExportPackageListError![]hii.PackageList {
        var len = buffer.len;
        switch (self._export_package_lists(self, handle, &len, buffer.ptr)) {
            .success => return buffer[0..len],
            .buffer_too_small => return error.BufferTooSmall,
            .invalid_parameter => return error.InvalidParameter,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0xef9fc172,
        .time_mid = 0xa1b2,
        .time_high_and_version = 0x4693,
        .clock_seq_high_and_reserved = 0xb3,
        .clock_seq_low = 0x27,
        .node = [_]u8{ 0x6d, 0x32, 0xfc, 0x41, 0x60, 0x42 },
    };
};



---
File: /std/os/uefi/protocol/hii_popup.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Status = uefi.Status;
const hii = uefi.hii;
const cc = uefi.cc;

/// Display a popup window
pub const HiiPopup = extern struct {
    revision: u64,
    _create_popup: *const fn (*const HiiPopup, PopupStyle, PopupType, hii.Handle, u16, ?*PopupSelection) callconv(cc) Status,

    pub const CreatePopupError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
    };

    /// Displays a popup window.
    pub fn createPopup(
        self: *const HiiPopup,
        style: PopupStyle,
        popup_type: PopupType,
        handle: hii.Handle,
        msg: u16,
    ) CreatePopupError!PopupSelection {
        var res: PopupSelection = undefined;
        switch (self._create_popup(self, style, popup_type, handle, msg, &res)) {
            .success => return res,
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x4311edc0,
        .time_mid = 0x6054,
        .time_high_and_version = 0x46d4,
        .clock_seq_high_and_reserved = 0x9e,
        .clock_seq_low = 0x40,
        .node = [_]u8{ 0x89, 0x3e, 0xa9, 0x52, 0xfc, 0xcc },
    };

    pub const PopupStyle = enum(u32) {
        info,
        warning,
        @"error",
    };

    pub const PopupType = enum(u32) {
        ok,
        cancel,
        yes_no,
        yes_no_cancel,
    };

    pub const PopupSelection = enum(u32) {
        ok,
        cancel,
        yes,
        no,
    };
};



---
File: /std/os/uefi/protocol/ip6_config.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Event = uefi.Event;
const Status = uefi.Status;
const cc = uefi.cc;
const MacAddress = uefi.MacAddress;
const Ip6 = uefi.protocol.Ip6;

pub const Ip6Config = extern struct {
    _set_data: *const fn (*const Ip6Config, DataType, usize, *const anyopaque) callconv(cc) Status,
    _get_data: *const fn (*const Ip6Config, DataType, *usize, ?*const anyopaque) callconv(cc) Status,
    _register_data_notify: *const fn (*const Ip6Config, DataType, Event) callconv(cc) Status,
    _unregister_data_notify: *const fn (*const Ip6Config, DataType, Event) callconv(cc) Status,

    pub const SetDataError = uefi.UnexpectedError || error{
        InvalidParameter,
        WriteProtected,
        AccessDenied,
        NotReady,
        BadBufferSize,
        Unsupported,
        OutOfResources,
        DeviceError,
    };
    pub const GetDataError = uefi.UnexpectedError || error{
        InvalidParameter,
        BufferTooSmall,
        NotReady,
        NotFound,
    };
    pub const RegisterDataNotifyError = uefi.UnexpectedError || error{
        InvalidParameter,
        Unsupported,
        OutOfResources,
        AccessDenied,
    };
    pub const UnregisterDataNotifyError = uefi.UnexpectedError || error{
        InvalidParameter,
        NotFound,
    };

    pub fn setData(
        self: *const Ip6Config,
        comptime data_type: std.meta.Tag(DataType),
        payload: *const @FieldType(DataType, @tagName(data_type)),
    ) SetDataError!void {
        const data_size = @sizeOf(@TypeOf(payload));
        switch (self._set_data(self, data_type, data_size, @ptrCast(payload))) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .write_protected => return error.WriteProtected,
            .access_denied => return error.AccessDenied,
            .not_ready => return error.NotReady,
            .bad_buffer_size => return error.BadBufferSize,
            .unsupported => return error.Unsupported,
            .out_of_resources => return error.OutOfResources,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn getData(
        self: *const Ip6Config,
        comptime data_type: std.meta.Tag(DataType),
    ) GetDataError!@FieldType(DataType, @tagName(data_type)) {
        const DataPayload = @FieldType(DataType, @tagName(data_type));

        var payload: DataPayload = undefined;
        var payload_size: usize = @sizeOf(DataPayload);

        switch (self._get_data(self, data_type, &payload_size, @ptrCast(&payload))) {
            .success => return payload,
            .invalid_parameter => return error.InvalidParameter,
            .buffer_too_small => return error.BufferTooSmall,
            .not_ready => return error.NotReady,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn registerDataNotify(
        self: *const Ip6Config,
        data_type: DataType,
        event: Event,
    ) RegisterDataNotifyError!void {
        switch (self._register_data_notify(self, data_type, event)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .unsupported => return error.Unsupported,
            .out_of_resources => return error.OutOfResources,
            .access_denied => return error.AccessDenied,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn unregisterDataNotify(
        self: *const Ip6Config,
        data_type: DataType,
        event: Event,
    ) UnregisterDataNotifyError!void {
        switch (self._unregister_data_notify(self, data_type, event)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x937fe521,
        .time_mid = 0x95ae,
        .time_high_and_version = 0x4d1a,
        .clock_seq_high_and_reserved = 0x89,
        .clock_seq_low = 0x29,
        .node = [_]u8{ 0x48, 0xbc, 0xd9, 0x0a, 0xd3, 0x1a },
    };

    pub const DataType = union(enum(u32)) {
        interface_info: InterfaceInfo,
        alt_interface_id: InterfaceId,
        policy: Policy,
        dup_addr_detect_transmits: DupAddrDetectTransmits,
        manual_address: [*]ManualAddress,
        gateway: [*]Ip6.Address,
        dns_server: [*]Ip6.Address,
    };

    pub const InterfaceInfo = extern struct {
        name: [32]u16,
        if_type: u8,
        hw_address_size: u32,
        hw_address: MacAddress,
        address_info_count: u32,
        address_info: [*]Ip6.AddressInfo,
        route_count: u32,
        route_table: Ip6.RouteTable,
    };

    pub const InterfaceId = extern struct {
        id: [8]u8,
    };

    pub const Policy = enum(u32) {
        manual,
        automatic,
    };

    pub const DupAddrDetectTransmits = extern struct {
        dup_addr_detect_transmits: u32,
    };

    pub const ManualAddress = extern struct {
        address: Ip6.Address,
        is_anycast: bool,
        prefix_length: u8,
    };
};



---
File: /std/os/uefi/protocol/ip6.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Event = uefi.Event;
const Status = uefi.Status;
const MacAddress = uefi.MacAddress;
const ManagedNetworkConfigData = uefi.protocol.ManagedNetwork.Config;
const SimpleNetwork = uefi.protocol.SimpleNetwork;
const cc = uefi.cc;

pub const Ip6 = extern struct {
    _get_mode_data: *const fn (*const Ip6, ?*Mode, ?*ManagedNetworkConfigData, ?*SimpleNetwork) callconv(cc) Status,
    _configure: *const fn (*Ip6, ?*const Config) callconv(cc) Status,
    _groups: *const fn (*Ip6, bool, ?*const Address) callconv(cc) Status,
    _routes: *const fn (*Ip6, bool, ?*const Address, u8, ?*const Address) callconv(cc) Status,
    _neighbors: *const fn (*Ip6, bool, *const Address, ?*const MacAddress, u32, bool) callconv(cc) Status,
    _transmit: *const fn (*Ip6, *CompletionToken) callconv(cc) Status,
    _receive: *const fn (*Ip6, *CompletionToken) callconv(cc) Status,
    _cancel: *const fn (*Ip6, ?*CompletionToken) callconv(cc) Status,
    _poll: *const fn (*Ip6) callconv(cc) Status,

    pub const GetModeDataError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
    };
    pub const ConfigureError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
        NoMapping,
        AlreadyStarted,
        DeviceError,
        Unsupported,
    };
    pub const GroupsError = uefi.UnexpectedError || error{
        InvalidParameter,
        NotStarted,
        OutOfResources,
        Unsupported,
        AlreadyStarted,
        NotFound,
        DeviceError,
    };
    pub const RoutesError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        OutOfResources,
        NotFound,
        AccessDenied,
    };
    pub const NeighborsError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        OutOfResources,
        NotFound,
        AccessDenied,
    };
    pub const TransmitError = uefi.UnexpectedError || error{
        NotStarted,
        NoMapping,
        InvalidParameter,
        AccessDenied,
        NotReady,
        NotFound,
        OutOfResources,
        BufferTooSmall,
        BadBufferSize,
        DeviceError,
        NoMedia,
    };
    pub const ReceiveError = uefi.UnexpectedError || error{
        NotStarted,
        NoMapping,
        InvalidParameter,
        OutOfResources,
        DeviceError,
        AccessDenied,
        NotReady,
        NoMedia,
    };
    pub const CancelError = uefi.UnexpectedError || error{
        InvalidParameter,
        NotStarted,
        NotFound,
        DeviceError,
    };
    pub const PollError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        DeviceError,
        Timeout,
    };

    pub const ModeData = struct {
        ip6_mode: Mode,
        mnp_config: ManagedNetworkConfigData,
        snp_mode: SimpleNetwork,
    };

    /// Gets the current operational settings for this instance of the EFI IPv6 Protocol driver.
    pub fn getModeData(self: *const Ip6) GetModeDataError!ModeData {
        var data: ModeData = undefined;
        switch (self._get_mode_data(self, &data.ip6_mode, &data.mnp_config, &data.snp_mode)) {
            .success => return data,
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Assign IPv6 address and other configuration parameter to this EFI IPv6 Protocol driver instance.
    ///
    /// To reset the configuration, use `disable` instead.
    pub fn configure(self: *Ip6, ip6_config_data: *const Config) ConfigureError!void {
        switch (self._configure(self, ip6_config_data)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            .no_mapping => return error.NoMapping,
            .already_started => return error.AlreadyStarted,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn disable(self: *Ip6) ConfigureError!void {
        switch (self._configure(self, null)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            .no_mapping => return error.NoMapping,
            .already_started => return error.AlreadyStarted,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn leaveAllGroups(self: *Ip6) GroupsError!void {
        switch (self._groups(self, false, null)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .not_started => return error.NotStarted,
            .out_of_resources => return error.OutOfResources,
            .unsupported => return error.Unsupported,
            .already_started => return error.AlreadyStarted,
            .not_found => return error.NotFound,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Joins and leaves multicast groups.
    ///
    /// To leave all groups, use `leaveAllGroups` instead.
    pub fn groups(
        self: *Ip6,
        join_flag: JoinFlag,
        group_address: *const Address,
    ) GroupsError!void {
        switch (self._groups(
            self,
            // set to TRUE to join the multicast group session and FALSE to leave
            join_flag == .join,
            group_address,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .not_started => return error.NotStarted,
            .out_of_resources => return error.OutOfResources,
            .unsupported => return error.Unsupported,
            .already_started => return error.AlreadyStarted,
            .not_found => return error.NotFound,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Adds and deletes routing table entries.
    pub fn routes(
        self: *Ip6,
        delete_route: DeleteFlag,
        destination: ?*const Address,
        prefix_length: u8,
        gateway_address: ?*const Address,
    ) RoutesError!void {
        switch (self._routes(
            self,
            delete_route == .delete,
            destination,
            prefix_length,
            gateway_address,
        )) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            .not_found => return error.NotFound,
            .access_denied => return error.AccessDenied,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Add or delete Neighbor cache entries.
    pub fn neighbors(
        self: *Ip6,
        delete_flag: DeleteFlag,
        target_ip6_address: *const Address,
        target_link_address: ?*const MacAddress,
        timeout: u32,
        override: bool,
    ) NeighborsError!void {
        switch (self._neighbors(
            self,
            // set to TRUE to delete this route from the routing table.
            // set to FALSE to add this route to the routing table.
            delete_flag == .delete,
            target_ip6_address,
            target_link_address,
            timeout,
            override,
        )) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            .not_found => return error.NotFound,
            .access_denied => return error.AccessDenied,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Places outgoing data packets into the transmit queue.
    pub fn transmit(self: *Ip6, token: *CompletionToken) TransmitError!void {
        switch (self._transmit(self, token)) {
            .success => {},
            .not_started => return error.NotStarted,
            .no_mapping => return error.NoMapping,
            .invalid_parameter => return error.InvalidParameter,
            .access_denied => return error.AccessDenied,
            .not_ready => return error.NotReady,
            .not_found => return error.NotFound,
            .out_of_resources => return error.OutOfResources,
            .buffer_too_small => return error.BufferTooSmall,
            .bad_buffer_size => return error.BadBufferSize,
            .device_error => return error.DeviceError,
            .no_media => return error.NoMedia,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Places a receiving request into the receiving queue.
    pub fn receive(self: *Ip6, token: *CompletionToken) ReceiveError!void {
        switch (self._receive(self, token)) {
            .success => {},
            .not_started => return error.NotStarted,
            .no_mapping => return error.NoMapping,
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            .device_error => return error.DeviceError,
            .access_denied => return error.AccessDenied,
            .not_ready => return error.NotReady,
            .no_media => return error.NoMedia,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Abort an asynchronous transmits or receive request.
    pub fn cancel(self: *Ip6, token: ?*CompletionToken) CancelError!void {
        switch (self._cancel(self, token)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .not_started => return error.NotStarted,
            .not_found => return error.NotFound,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Polls for incoming data packets and processes outgoing data packets.
    ///
    /// Returns true if a packet was received or processed.
    pub fn poll(self: *Ip6) PollError!bool {
        switch (self._poll(self)) {
            .success => return true,
            .not_ready => return false,
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .timeout => return error.Timeout,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x2c8759d5,
        .time_mid = 0x5c2d,
        .time_high_and_version = 0x66ef,
        .clock_seq_high_and_reserved = 0x92,
        .clock_seq_low = 0x5f,
        .node = [_]u8{ 0xb6, 0x6c, 0x10, 0x19, 0x57, 0xe2 },
    };

    pub const DeleteFlag = enum {
        delete,
        add,
    };

    pub const JoinFlag = enum {
        join,
        leave,
    };

    pub const Mode = extern struct {
        is_started: bool,
        max_packet_size: u32,
        config_data: Config,
        is_configured: bool,
        address_count: u32,
        address_list: [*]AddressInfo,
        group_count: u32,
        group_table: [*]Address,
        route_count: u32,
        route_table: [*]RouteTable,
        neighbor_count: u32,
        neighbor_cache: [*]NeighborCache,
        prefix_count: u32,
        prefix_table: [*]AddressInfo,
        icmp_type_count: u32,
        icmp_type_list: [*]IcmpType,
    };

    pub const Config = extern struct {
        default_protocol: u8,
        accept_any_protocol: bool,
        accept_icmp_errors: bool,
        accept_promiscuous: bool,
        destination_address: Address,
        station_address: Address,
        traffic_class: u8,
        hop_limit: u8,
        flow_label: u32,
        receive_timeout: u32,
        transmit_timeout: u32,
    };

    pub const Address = [16]u8;

    pub const AddressInfo = extern struct {
        address: Address,
        prefix_length: u8,
    };

    pub const RouteTable = extern struct {
        gateway: Address,
        destination: Address,
        prefix_length: u8,
    };

    pub const NeighborState = enum(u32) {
        incomplete,
        reachable,
        stale,
        delay,
        probe,
    };

    pub const NeighborCache = extern struct {
        neighbor: Address,
        link_address: MacAddress,
        state: NeighborState,
    };

    pub const IcmpType = extern struct {
        type: u8,
        code: u8,
    };

    pub const CompletionToken = extern struct {
        event: Event,
        status: Status,
        packet: *anyopaque, // union TODO
    };
};



---
File: /std/os/uefi/protocol/loaded_image.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Handle = uefi.Handle;
const Status = uefi.Status;
const SystemTable = uefi.tables.SystemTable;
const MemoryType = uefi.tables.MemoryType;
const DevicePath = uefi.protocol.DevicePath;
const cc = uefi.cc;

pub const LoadedImage = extern struct {
    revision: u32,
    parent_handle: Handle,
    system_table: *SystemTable,
    device_handle: ?Handle,
    file_path: *DevicePath,
    reserved: *anyopaque,
    load_options_size: u32,
    load_options: ?*anyopaque,
    image_base: [*]u8,
    image_size: u64,
    image_code_type: MemoryType,
    image_data_type: MemoryType,
    _unload: *const fn (*LoadedImage, Handle) callconv(cc) Status,

    pub const UnloadError = uefi.UnexpectedError || error{InvalidParameter};

    /// Unloads an image from memory.
    pub fn unload(self: *LoadedImage, handle: Handle) UnloadError!void {
        switch (self._unload(self, handle)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x5b1b31a1,
        .time_mid = 0x9562,
        .time_high_and_version = 0x11d2,
        .clock_seq_high_and_reserved = 0x8e,
        .clock_seq_low = 0x3f,
        .node = [_]u8{ 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b },
    };

    pub const device_path_guid align(8) = Guid{
        .time_low = 0xbc62157e,
        .time_mid = 0x3e33,
        .time_high_and_version = 0x4fec,
        .clock_seq_high_and_reserved = 0x99,
        .clock_seq_low = 0x20,
        .node = [_]u8{ 0x2d, 0x3b, 0x36, 0xd7, 0x50, 0xdf },
    };
};



---
File: /std/os/uefi/protocol/managed_network.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Event = uefi.Event;
const Handle = uefi.Handle;
const Status = uefi.Status;
const Time = uefi.Time;
const SimpleNetwork = uefi.protocol.SimpleNetwork;
const MacAddress = uefi.MacAddress;
const cc = uefi.cc;
const Error = Status.Error;

pub const ManagedNetwork = extern struct {
    _get_mode_data: *const fn (*const ManagedNetwork, ?*Config, ?*SimpleNetwork) callconv(cc) Status,
    _configure: *const fn (*ManagedNetwork, ?*const Config) callconv(cc) Status,
    _mcast_ip_to_mac: *const fn (*ManagedNetwork, bool, *const anyopaque, *MacAddress) callconv(cc) Status,
    _groups: *const fn (*ManagedNetwork, bool, ?*const MacAddress) callconv(cc) Status,
    _transmit: *const fn (*ManagedNetwork, *CompletionToken) callconv(cc) Status,
    _receive: *const fn (*ManagedNetwork, *CompletionToken) callconv(cc) Status,
    _cancel: *const fn (*ManagedNetwork, ?*const CompletionToken) callconv(cc) Status,
    _poll: *const fn (*ManagedNetwork) callconv(cc) Status,

    pub const GetModeDataError = uefi.UnexpectedError || error{
        InvalidParameter,
        Unsupported,
        NotStarted,
    } || Error;
    pub const ConfigureError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
        Unsupported,
        DeviceError,
    } || Error;
    pub const McastIpToMacError = uefi.UnexpectedError || error{
        InvalidParameter,
        NotStarted,
        Unsupported,
        DeviceError,
    } || Error;
    pub const GroupsError = uefi.UnexpectedError || error{
        InvalidParameter,
        NotStarted,
        AlreadyStarted,
        NotFound,
        DeviceError,
        Unsupported,
    } || Error;
    pub const TransmitError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        AccessDenied,
        OutOfResources,
        DeviceError,
        NotReady,
        NoMedia,
    };
    pub const ReceiveError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        OutOfResources,
        DeviceError,
        AccessDenied,
        NotReady,
        NoMedia,
    };
    pub const CancelError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        NotFound,
    };
    pub const PollError = uefi.UnexpectedError || error{
        NotStarted,
        DeviceError,
        NotReady,
        Timeout,
    };

    pub const GetModeDataData = struct {
        mnp_config: Config,
        snp_mode: SimpleNetwork,
    };

    /// Returns the operational parameters for the current MNP child driver.
    /// May also support returning the underlying SNP driver mode data.
    pub fn getModeData(self: *const ManagedNetwork) GetModeDataError!GetModeDataData {
        var data: GetModeDataData = undefined;
        switch (self._get_mode_data(self, &data.mnp_config, &data.snp_mode)) {
            .success => return data,
            else => |status| {
                try status.err();
                return uefi.unexpectedStatus(status);
            },
        }
    }

    /// Sets or clears the operational parameters for the MNP child driver.
    pub fn configure(self: *ManagedNetwork, mnp_config_data: ?*const Config) ConfigureError!void {
        switch (self._configure(self, mnp_config_data)) {
            .success => {},
            else => |status| {
                try status.err();
                return uefi.unexpectedStatus(status);
            },
        }
    }

    /// Translates an IP multicast address to a hardware (MAC) multicast address.
    /// This function may be unsupported in some MNP implementations.
    pub fn mcastIpToMac(
        self: *ManagedNetwork,
        ipv6flag: bool,
        ipaddress: *const uefi.IpAddress,
    ) McastIpToMacError!MacAddress {
        var result: MacAddress = undefined;
        switch (self._mcast_ip_to_mac(self, ipv6flag, ipaddress, &result)) {
            .success => return result,
            else => |status| {
                try status.err();
                return uefi.unexpectedStatus(status);
            },
        }
    }

    /// Enables and disables receive filters for multicast address.
    /// This function may be unsupported in some MNP implementations.
    pub fn groups(
        self: *ManagedNetwork,
        join_flag: bool,
        mac_address: ?*const MacAddress,
    ) GroupsError!void {
        switch (self._groups(self, join_flag, mac_address)) {
            .success => {},
            else => |status| {
                try status.err();
                return uefi.unexpectedStatus(status);
            },
        }
    }

    /// Places asynchronous outgoing data packets into the transmit queue.
    pub fn transmit(self: *ManagedNetwork, token: *CompletionToken) TransmitError!void {
        switch (self._transmit(self, token)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .access_denied => return error.AccessDenied,
            .out_of_resources => return error.OutOfResources,
            .device_error => return error.DeviceError,
            .not_ready => return error.NotReady,
            .no_media => return error.NoMedia,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Places an asynchronous receiving request into the receiving queue.
    pub fn receive(self: *ManagedNetwork, token: *CompletionToken) TransmitError!void {
        switch (self._receive(self, token)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            .device_error => return error.DeviceError,
            .access_denied => return error.AccessDenied,
            .not_ready => return error.NotReady,
            .no_media => return error.NoMedia,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Aborts an asynchronous transmit or receive request.
    pub fn cancel(self: *ManagedNetwork, token: ?*const CompletionToken) CancelError!void {
        switch (self._cancel(self, token)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Polls for incoming data packets and processes outgoing data packets.
    pub fn poll(self: *ManagedNetwork) PollError!void {
        switch (self._poll(self)) {
            .success => {},
            .not_started => return error.NotStarted,
            .device_error => return error.DeviceError,
            .not_ready => return error.NotReady,
            .timeout => return error.Timeout,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x7ab33a91,
        .time_mid = 0xace5,
        .time_high_and_version = 0x4326,
        .clock_seq_high_and_reserved = 0xb5,
        .clock_seq_low = 0x72,
        .node = [_]u8{ 0xe7, 0xee, 0x33, 0xd3, 0x9f, 0x16 },
    };

    pub const ServiceBinding = extern struct {
        _create_child: *const fn (*const ServiceBinding, *?Handle) callconv(cc) Status,
        _destroy_child: *const fn (*const ServiceBinding, Handle) callconv(cc) Status,

        pub fn createChild(self: *const ServiceBinding, handle: *?Handle) Status {
            return self._create_child(self, handle);
        }

        pub fn destroyChild(self: *const ServiceBinding, handle: Handle) Status {
            return self._destroy_child(self, handle);
        }

        pub const guid align(8) = Guid{
            .time_low = 0xf36ff770,
            .time_mid = 0xa7e1,
            .time_high_and_version = 0x42cf,
            .clock_seq_high_and_reserved = 0x9e,
            .clock_seq_low = 0xd2,
            .node = [_]u8{ 0x56, 0xf0, 0xf2, 0x71, 0xf4, 0x4c },
        };
    };

    pub const Config = extern struct {
        received_queue_timeout_value: u32,
        transmit_queue_timeout_value: u32,
        protocol_type_filter: u16,
        enable_unicast_receive: bool,
        enable_multicast_receive: bool,
        enable_broadcast_receive: bool,
        enable_promiscuous_receive: bool,
        flush_queues_on_reset: bool,
        enable_receive_timestamps: bool,
        disable_background_polling: bool,
    };

    pub const CompletionToken = extern struct {
        event: Event,
        status: Status,
        packet: extern union {
            rx_data: *ReceiveData,
            tx_data: *TransmitData,
        },
    };

    pub const ReceiveData = extern struct {
        timestamp: Time,
        recycle_event: Event,
        packet_length: u32,
        header_length: u32,
        address_length: u32,
        data_length: u32,
        broadcast_flag: bool,
        multicast_flag: bool,
        promiscuous_flag: bool,
        protocol_type: u16,
        destination_address: [*]u8,
        source_address: [*]u8,
        media_header: [*]u8,
        packet_data: [*]u8,
    };

    pub const TransmitData = extern struct {
        destination_address: ?*MacAddress,
        source_address: ?*MacAddress,
        protocol_type: u16,
        data_length: u32,
        header_length: u16,
        fragment_count: u16,

        pub fn getFragments(self: *TransmitData) []Fragment {
            return @as([*]Fragment, @ptrCast(@alignCast(@as([*]u8, @ptrCast(self)) + @sizeOf(TransmitData))))[0..self.fragment_count];
        }
    };

    pub const Fragment = extern struct {
        fragment_length: u32,
        fragment_buffer: [*]u8,
    };
};



---
File: /std/os/uefi/protocol/rng.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Status = uefi.Status;
const cc = uefi.cc;

/// Random Number Generator protocol
pub const Rng = extern struct {
    _get_info: *const fn (*const Rng, *usize, [*]align(8) Guid) callconv(cc) Status,
    _get_rng: *const fn (*const Rng, ?*align(8) const Guid, usize, [*]u8) callconv(cc) Status,

    pub const GetInfoError = uefi.UnexpectedError || error{
        Unsupported,
        DeviceError,
        BufferTooSmall,
    };
    pub const GetRNGError = uefi.UnexpectedError || error{
        Unsupported,
        DeviceError,
        NotReady,
        InvalidParameter,
    };

    /// Returns information about the random number generation implementation.
    pub fn getInfo(self: *const Rng, list: []align(8) Guid) GetInfoError![]align(8) Guid {
        var len: usize = list.len;
        switch (self._get_info(self, &len, list.ptr)) {
            .success => return list[0..len],
            .unsupported => return error.Unsupported,
            .device_error => return error.DeviceError,
            .buffer_too_small => return error.BufferTooSmall,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Produces and returns an RNG value using either the default or specified RNG algorithm.
    pub fn getRNG(self: *const Rng, algo: ?*align(8) const Guid, value: []u8) GetRNGError!void {
        switch (self._get_rng(self, algo, value.len, value.ptr)) {
            .success => {},
            .unsupported => return error.Unsupported,
            .device_error => return error.DeviceError,
            .not_ready => return error.NotReady,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x3152bca5,
        .time_mid = 0xeade,
        .time_high_and_version = 0x433d,
        .clock_seq_high_and_reserved = 0x86,
        .clock_seq_low = 0x2e,
        .node = [_]u8{ 0xc0, 0x1c, 0xdc, 0x29, 0x1f, 0x44 },
    };
    pub const algorithm_sp800_90_hash_256 align(8) = Guid{
        .time_low = 0xa7af67cb,
        .time_mid = 0x603b,
        .time_high_and_version = 0x4d42,
        .clock_seq_high_and_reserved = 0xba,
        .clock_seq_low = 0x21,
        .node = [_]u8{ 0x70, 0xbf, 0xb6, 0x29, 0x3f, 0x96 },
    };
    pub const algorithm_sp800_90_hmac_256 align(8) = Guid{
        .time_low = 0xc5149b43,
        .time_mid = 0xae85,
        .time_high_and_version = 0x4f53,
        .clock_seq_high_and_reserved = 0x99,
        .clock_seq_low = 0x82,
        .node = [_]u8{ 0xb9, 0x43, 0x35, 0xd3, 0xa9, 0xe7 },
    };
    pub const algorithm_sp800_90_ctr_256 align(8) = Guid{
        .time_low = 0x44f0de6e,
        .time_mid = 0x4d8c,
        .time_high_and_version = 0x4045,
        .clock_seq_high_and_reserved = 0xa8,
        .clock_seq_low = 0xc7,
        .node = [_]u8{ 0x4d, 0xd1, 0x68, 0x85, 0x6b, 0x9e },
    };
    pub const algorithm_x9_31_3des align(8) = Guid{
        .time_low = 0x63c4785a,
        .time_mid = 0xca34,
        .time_high_and_version = 0x4012,
        .clock_seq_high_and_reserved = 0xa3,
        .clock_seq_low = 0xc8,
        .node = [_]u8{ 0x0b, 0x6a, 0x32, 0x4f, 0x55, 0x46 },
    };
    pub const algorithm_x9_31_aes align(8) = Guid{
        .time_low = 0xacd03321,
        .time_mid = 0x777e,
        .time_high_and_version = 0x4d3d,
        .clock_seq_high_and_reserved = 0xb1,
        .clock_seq_low = 0xc8,
        .node = [_]u8{ 0x20, 0xcf, 0xd8, 0x88, 0x20, 0xc9 },
    };
    pub const algorithm_raw align(8) = Guid{
        .time_low = 0xe43176d7,
        .time_mid = 0xb6e8,
        .time_high_and_version = 0x4827,
        .clock_seq_high_and_reserved = 0xb7,
        .clock_seq_low = 0x84,
        .node = [_]u8{ 0x7f, 0xfd, 0xc4, 0xb6, 0x85, 0x61 },
    };
};



---
File: /std/os/uefi/protocol/serial_io.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Status = uefi.Status;
const cc = uefi.cc;

pub const SerialIo = extern struct {
    revision: u64,
    _reset: *const fn (*SerialIo) callconv(cc) Status,
    _set_attribute: *const fn (*SerialIo, u64, u32, u32, ParityType, u8, StopBitsType) callconv(cc) Status,
    _set_control: *const fn (*SerialIo, u32) callconv(cc) Status,
    _get_control: *const fn (*const SerialIo, *u32) callconv(cc) Status,
    _write: *const fn (*SerialIo, *usize, *const anyopaque) callconv(cc) Status,
    _read: *const fn (*SerialIo, *usize, *anyopaque) callconv(cc) Status,
    mode: *Mode,
    device_type_guid: ?*Guid,

    pub const ResetError = uefi.UnexpectedError || error{DeviceError};
    pub const SetAttributeError = uefi.UnexpectedError || error{
        InvalidParameter,
        DeviceError,
    };
    pub const SetControlError = uefi.UnexpectedError || error{
        Unsupported,
        DeviceError,
    };
    pub const GetControlError = uefi.UnexpectedError || error{DeviceError};
    pub const WriteError = uefi.UnexpectedError || error{
        DeviceError,
        Timeout,
    };
    pub const ReadError = uefi.UnexpectedError || error{
        DeviceError,
        Timeout,
    };

    /// Resets the serial device.
    pub fn reset(self: *SerialIo) ResetError!void {
        switch (self._reset(self)) {
            .success => {},
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Sets the baud rate, receive FIFO depth, transmit/receive time out, parity, data bits, and stop bits on a serial device.
    pub fn setAttribute(
        self: *SerialIo,
        baud_rate: u64,
        receiver_fifo_depth: u32,
        timeout: u32,
        parity: ParityType,
        data_bits: u8,
        stop_bits: StopBitsType,
    ) SetAttributeError!void {
        switch (self._set_attribute(
            self,
            baud_rate,
            receiver_fifo_depth,
            timeout,
            parity,
            data_bits,
            stop_bits,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Sets the control bits on a serial device.
    pub fn setControl(self: *SerialIo, control: u32) SetControlError!void {
        switch (self._set_control(self, control)) {
            .success => {},
            .unsupported => return error.Unsupported,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Retrieves the status of the control bits on a serial device.
    pub fn getControl(self: *SerialIo) GetControlError!u32 {
        var control: u32 = undefined;
        switch (self._get_control(self, &control)) {
            .success => return control,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Writes data to a serial device.
    pub fn write(self: *SerialIo, buffer: []const u8) WriteError!usize {
        var len: usize = buffer.len;
        switch (self._write(self, &len, buffer.ptr)) {
            .success => return len,
            .device_error => return error.DeviceError,
            .timeout => return error.Timeout,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Reads data from a serial device.
    pub fn read(self: *SerialIo, buffer: []u8) ReadError!usize {
        var len: usize = buffer.len;
        switch (self._read(self, &len, buffer.ptr)) {
            .success => return len,
            .device_error => return error.DeviceError,
            .timeout => return error.Timeout,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0xBB25CF6F,
        .time_mid = 0xF1D4,
        .time_high_and_version = 0x11D2,
        .clock_seq_high_and_reserved = 0x9a,
        .clock_seq_low = 0x0c,
        .node = [_]u8{ 0x00, 0x90, 0x27, 0x3f, 0xc1, 0xfd },
    };

    pub const ParityType = enum(u32) {
        default_parity,
        no_parity,
        even_parity,
        odd_parity,
        mark_parity,
        space_parity,
    };

    pub const StopBitsType = enum(u32) {
        default_stop_bits,
        one_stop_bit,
        one_five_stop_bits,
        two_stop_bits,
    };

    pub const Mode = extern struct {
        control_mask: u32,
        timeout: u32,
        baud_rate: u64,
        receive_fifo_depth: u32,
        data_bits: u32,
        parity: u32,
        stop_bits: u32,
    };
};



---
File: /std/os/uefi/protocol/service_binding.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Handle = uefi.Handle;
const Status = uefi.Status;
const Error = Status.Error;
const cc = uefi.cc;

pub fn ServiceBinding(service_guid: Guid) type {
    return struct {
        const Self = @This();

        _create_child: *const fn (*Self, *?Handle) callconv(cc) Status,
        _destroy_child: *const fn (*Self, Handle) callconv(cc) Status,

        pub const CreateChildError = uefi.UnexpectedError || error{
            InvalidParameter,
            OutOfResources,
        } || Error;
        pub const DestroyChildError = uefi.UnexpectedError || error{
            Unsupported,
            InvalidParameter,
            AccessDenied,
        } || Error;

        /// To add this protocol to an existing handle, use `addToHandle` instead.
        pub fn createChild(self: *Self) CreateChildError!Handle {
            var handle: ?Handle = null;
            switch (self._create_child(self, &handle)) {
                .success => return handle orelse error.Unexpected,
                else => |status| {
                    try status.err();
                    return uefi.unexpectedStatus(status);
                },
            }
        }

        pub fn addToHandle(self: *Self, handle: Handle) CreateChildError!void {
            switch (self._create_child(self, @ptrCast(@constCast(&handle)))) {
                .success => {},
                else => |status| {
                    try status.err();
                    return uefi.unexpectedStatus(status);
                },
            }
        }

        pub fn destroyChild(self: *Self, handle: Handle) DestroyChildError!void {
            switch (self._destroy_child(self, handle)) {
                .success => {},
                else => |status| {
                    try status.err();
                    return uefi.unexpectedStatus(status);
                },
            }
        }

        pub const guid align(8) = service_guid;
    };
}



---
File: /std/os/uefi/protocol/shell_parameters.zig
---

const uefi = @import("std").os.uefi;
const Guid = uefi.Guid;
const FileHandle = uefi.FileHandle;

pub const ShellParameters = extern struct {
    argv: [*][*:0]const u16,
    argc: usize,
    stdin: FileHandle,
    stdout: FileHandle,
    stderr: FileHandle,

    pub const guid align(8) = Guid{
        .time_low = 0x752f3136,
        .time_mid = 0x4e16,
        .time_high_and_version = 0x4fdc,
        .clock_seq_high_and_reserved = 0xa2,
        .clock_seq_low = 0x2a,
        .node = [_]u8{ 0xe5, 0xf4, 0x68, 0x12, 0xf4, 0xca },
    };
};



---
File: /std/os/uefi/protocol/simple_file_system.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const File = uefi.protocol.File;
const Status = uefi.Status;
const cc = uefi.cc;

pub const SimpleFileSystem = extern struct {
    revision: u64,
    _open_volume: *const fn (*const SimpleFileSystem, **File) callconv(cc) Status,

    pub const OpenVolumeError = uefi.UnexpectedError || error{
        Unsupported,
        NoMedia,
        DeviceError,
        VolumeCorrupted,
        AccessDenied,
        OutOfResources,
        MediaChanged,
    };

    pub fn openVolume(self: *const SimpleFileSystem) OpenVolumeError!*File {
        var root: *File = undefined;
        switch (self._open_volume(self, &root)) {
            .success => return root,
            .unsupported => return error.Unsupported,
            .no_media => return error.NoMedia,
            .device_error => return error.DeviceError,
            .volume_corrupted => return error.VolumeCorrupted,
            .access_denied => return error.AccessDenied,
            .out_of_resources => return error.OutOfResources,
            .media_changed => return error.MediaChanged,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x0964e5b22,
        .time_mid = 0x6459,
        .time_high_and_version = 0x11d2,
        .clock_seq_high_and_reserved = 0x8e,
        .clock_seq_low = 0x39,
        .node = [_]u8{ 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b },
    };
};



---
File: /std/os/uefi/protocol/simple_network.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Event = uefi.Event;
const Guid = uefi.Guid;
const Status = uefi.Status;
const cc = uefi.cc;

pub const SimpleNetwork = extern struct {
    revision: u64,
    _start: *const fn (*SimpleNetwork) callconv(cc) Status,
    _stop: *const fn (*SimpleNetwork) callconv(cc) Status,
    _initialize: *const fn (*SimpleNetwork, usize, usize) callconv(cc) Status,
    _reset: *const fn (*SimpleNetwork, bool) callconv(cc) Status,
    _shutdown: *const fn (*SimpleNetwork) callconv(cc) Status,
    _receive_filters: *const fn (*SimpleNetwork, ReceiveFilter, ReceiveFilter, bool, usize, ?[*]const MacAddress) callconv(cc) Status,
    _station_address: *const fn (*SimpleNetwork, bool, ?*const MacAddress) callconv(cc) Status,
    _statistics: *const fn (*const SimpleNetwork, bool, ?*usize, ?*Statistics) callconv(cc) Status,
    _mcast_ip_to_mac: *const fn (*SimpleNetwork, bool, *const anyopaque, *MacAddress) callconv(cc) Status,
    _nvdata: *const fn (*SimpleNetwork, bool, usize, usize, [*]u8) callconv(cc) Status,
    _get_status: *const fn (*SimpleNetwork, ?*InterruptStatus, ?*?[*]u8) callconv(cc) Status,
    _transmit: *const fn (*SimpleNetwork, usize, usize, [*]const u8, ?*const MacAddress, ?*const MacAddress, ?*const u16) callconv(cc) Status,
    _receive: *const fn (*SimpleNetwork, ?*usize, *usize, [*]u8, ?*MacAddress, ?*MacAddress, ?*u16) callconv(cc) Status,
    wait_for_packet: Event,
    mode: *Mode,

    pub const StartError = uefi.UnexpectedError || error{
        AlreadyStarted,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const StopError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const InitializeError = uefi.UnexpectedError || error{
        NotStarted,
        OutOfResources,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const ResetError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const ShutdownError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        DeviceError,
    };
    pub const ReceiveFiltersError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const StationAddressError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const StatisticsError = uefi.UnexpectedError || error{
        NotStarted,
        BufferTooSmall,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const McastIpToMacError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const NvDataError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const GetStatusError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
        DeviceError,
    };
    pub const TransmitError = uefi.UnexpectedError || error{
        NotStarted,
        NotReady,
        BufferTooSmall,
        InvalidParameter,
        DeviceError,
        Unsupported,
    };
    pub const ReceiveError = uefi.UnexpectedError || error{
        NotStarted,
        NotReady,
        BufferTooSmall,
        InvalidParameter,
        DeviceError,
    };

    /// Changes the state of a network interface from "stopped" to "started".
    pub fn start(self: *SimpleNetwork) StartError!void {
        switch (self._start(self)) {
            .success => {},
            .already_started => return error.AlreadyStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Changes the state of a network interface from "started" to "stopped".
    pub fn stop(self: *SimpleNetwork) StopError!void {
        switch (self._stop(self)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Resets a network adapter and allocates the transmit and receive buffers required by the network interface.
    pub fn initialize(
        self: *SimpleNetwork,
        extra_rx_buffer_size: usize,
        extra_tx_buffer_size: usize,
    ) InitializeError!void {
        switch (self._initialize(self, extra_rx_buffer_size, extra_tx_buffer_size)) {
            .success => {},
            .not_started => return error.NotStarted,
            .out_of_resources => return error.OutOfResources,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Resets a network adapter and reinitializes it with the parameters that were provided in the previous call to initialize().
    pub fn reset(self: *SimpleNetwork, extended_verification: bool) ResetError!void {
        switch (self._reset(self, extended_verification)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Resets a network adapter and leaves it in a state that is safe for another driver to initialize.
    pub fn shutdown(self: *SimpleNetwork) ShutdownError!void {
        switch (self._shutdown(self)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Manages the multicast receive filters of a network interface.
    pub fn receiveFilters(
        self: *SimpleNetwork,
        enable: ReceiveFilter,
        disable: ReceiveFilter,
        reset_mcast_filter: bool,
        mcast_filter: ?[]const MacAddress,
    ) ReceiveFiltersError!void {
        const count: usize, const ptr: ?[*]const MacAddress =
            if (mcast_filter) |f|
                .{ f.len, f.ptr }
            else
                .{ 0, null };

        switch (self._receive_filters(self, enable, disable, reset_mcast_filter, count, ptr)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Modifies or resets the current station address, if supported.
    pub fn stationAddress(
        self: *SimpleNetwork,
        reset_flag: bool,
        new: ?*const MacAddress,
    ) StationAddressError!void {
        switch (self._station_address(self, reset_flag, new)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn resetStatistics(self: *SimpleNetwork) StatisticsError!void {
        switch (self._statistics(self, true, null, null)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Resets or collects the statistics on a network interface.
    pub fn statistics(self: *SimpleNetwork, reset_flag: bool) StatisticsError!Statistics {
        var stats: Statistics = undefined;
        var stats_size: usize = @sizeOf(Statistics);
        switch (self._statistics(self, reset_flag, &stats_size, &stats)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }

        if (stats_size != @sizeOf(Statistics))
            return error.Unexpected
        else
            return stats;
    }

    /// Converts a multicast IP address to a multicast HW MAC address.
    pub fn mcastIpToMac(
        self: *SimpleNetwork,
        ipv6: bool,
        ip: *const anyopaque,
    ) McastIpToMacError!MacAddress {
        var mac: MacAddress = undefined;
        switch (self._mcast_ip_to_mac(self, ipv6, ip, &mac)) {
            .success => return mac,
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Performs read and write operations on the NVRAM device attached to a network interface.
    pub fn nvData(
        self: *SimpleNetwork,
        read_write: NvDataOperation,
        offset: usize,
        buffer: []u8,
    ) NvDataError!void {
        switch (self._nvdata(
            self,
            // if ReadWrite is TRUE, a read operation is performed
 
```
