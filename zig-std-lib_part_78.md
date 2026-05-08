```
;
            inline for (@typeInfo(Fields).@"struct".fields, 0..) |field, i| {
                if (@field(fields, field.name))
                    self.bits |= 1 << i;
            }
            return self;
        }

        /// `fields` is a struct with each field set to an optional value
        pub fn setMany(self: Self, p: [*]align(@alignOf(Fields)) u8, fields: FieldValues) void {
            inline for (@typeInfo(Fields).@"struct".fields, 0..) |field, i| {
                if (@field(fields, field.name)) |value|
                    self.set(p, @as(FieldEnum, @enumFromInt(i)), value);
            }
        }

        pub fn set(
            self: Self,
            p: [*]align(@alignOf(Fields)) u8,
            comptime field: FieldEnum,
            value: Field(field),
        ) void {
            self.ptr(p, field).* = value;
        }

        pub fn ptr(self: Self, p: [*]align(@alignOf(Fields)) u8, comptime field: FieldEnum) *Field(field) {
            if (@sizeOf(Field(field)) == 0)
                return undefined;
            const off = self.offset(field);
            return @ptrCast(@alignCast(p + off));
        }

        pub fn ptrConst(self: Self, p: [*]align(@alignOf(Fields)) const u8, comptime field: FieldEnum) *const Field(field) {
            if (@sizeOf(Field(field)) == 0)
                return undefined;
            const off = self.offset(field);
            return @ptrCast(@alignCast(p + off));
        }

        pub fn offset(self: Self, comptime field: FieldEnum) usize {
            var off: usize = 0;
            inline for (@typeInfo(Fields).@"struct".fields, 0..) |field_info, i| {
                const active = (self.bits & (1 << i)) != 0;
                if (i == @intFromEnum(field)) {
                    assert(active);
                    return mem.alignForward(usize, off, @alignOf(field_info.type));
                } else if (active) {
                    off = mem.alignForward(usize, off, @alignOf(field_info.type));
                    off += @sizeOf(field_info.type);
                }
            }
        }

        pub fn Field(comptime field: FieldEnum) type {
            return @typeInfo(Fields).@"struct".fields[@intFromEnum(field)].type;
        }

        pub fn sizeInBytes(self: Self) usize {
            var off: usize = 0;
            inline for (@typeInfo(Fields).@"struct".fields, 0..) |field, i| {
                if (@sizeOf(field.type) == 0)
                    continue;
                if ((self.bits & (1 << i)) != 0) {
                    off = mem.alignForward(usize, off, @alignOf(field.type));
                    off += @sizeOf(field.type);
                }
            }
            return off;
        }
    };
}

test TrailerFlags {
    const Flags = TrailerFlags(struct {
        a: i32,
        b: bool,
        c: u64,
    });
    try testing.expectEqual(u2, meta.Tag(Flags.FieldEnum));

    var flags = Flags.init(.{
        .b = true,
        .c = true,
    });
    const slice = try testing.allocator.alignedAlloc(u8, .@"8", flags.sizeInBytes());
    defer testing.allocator.free(slice);

    flags.set(slice.ptr, .b, false);
    flags.set(slice.ptr, .c, 12345678);

    try testing.expect(flags.get(slice.ptr, .a) == null);
    try testing.expect(!flags.get(slice.ptr, .b).?);
    try testing.expect(flags.get(slice.ptr, .c).? == 12345678);

    flags.setMany(slice.ptr, .{
        .b = true,
        .c = 5678,
    });

    try testing.expect(flags.get(slice.ptr, .a) == null);
    try testing.expect(flags.get(slice.ptr, .b).?);
    try testing.expect(flags.get(slice.ptr, .c).? == 5678);
}



---
File: /std/os/linux/bpf/btf_ext.zig
---

pub const Header = packed struct {
    magic: u16,
    version: u8,
    flags: u8,
    hdr_len: u32,

    /// All offsets are in bytes relative to the end of this header
    func_info_off: u32,
    func_info_len: u32,
    line_info_off: u32,
    line_info_len: u32,
};

pub const InfoSec = packed struct {
    sec_name_off: u32,
    num_info: u32,
    // TODO: communicate that there is data here
    //data: [0]u8,
};



---
File: /std/os/linux/bpf/btf.zig
---

const std = @import("../../../std.zig");

pub const magic = 0xeb9f;
pub const version = 1;

pub const ext = @import("btf_ext.zig");

/// All offsets are in bytes relative to the end of this header
pub const Header = extern struct {
    magic: u16,
    version: u8,
    flags: u8,
    hdr_len: u32,

    /// offset of type section
    type_off: u32,

    /// length of type section
    type_len: u32,

    /// offset of string section
    str_off: u32,

    /// length of string section
    str_len: u32,
};

/// Max number of type identifiers
pub const max_type = 0xfffff;

/// Max offset into string section
pub const max_name_offset = 0xffffff;

/// Max number of struct/union/enum member of func args
pub const max_vlen = 0xffff;

pub const Type = extern struct {
    name_off: u32,
    info: packed struct(u32) {
        /// number of struct's members
        vlen: u16,

        unused_1: u8,
        kind: Kind,
        unused_2: u2,

        /// used by Struct, Union, and Fwd
        kind_flag: bool,
    },

    /// size is used by Int, Enum, Struct, Union, and DataSec, it tells the size
    /// of the type it is describing
    ///
    /// type is used by Ptr, Typedef, Volatile, Const, Restrict, Func,
    /// FuncProto, and Var. It is a type_id referring to another type
    size_type: extern union { size: u32, typ: u32 },
};

/// For some kinds, Type is immediately followed by extra data
pub const Kind = enum(u5) {
    unknown,
    int,
    ptr,
    array,
    @"struct",
    @"union",
    @"enum",
    fwd,
    typedef,
    @"volatile",
    @"const",
    restrict,
    func,
    func_proto,
    @"var",
    datasec,
    float,
    decl_tag,
    type_tag,
    enum64,
};

/// int kind is followed by this struct
pub const IntInfo = packed struct(u32) {
    bits: u8,
    reserved_1: u8,
    offset: u8,
    encoding: enum(u4) {
        signed = 1 << 0,
        char = 1 << 1,
        boolean = 1 << 2,
    },
    reserved_2: u4,
};

test "IntInfo is 32 bits" {
    try std.testing.expectEqual(@bitSizeOf(IntInfo), 32);
}

/// enum kind is followed by this struct
pub const Enum = extern struct {
    name_off: u32,
    val: i32,
};

/// enum64 kind is followed by this struct
pub const Enum64 = extern struct {
    name_off: u32,
    val_lo32: i32,
    val_hi32: i32,
};

/// array kind is followed by this struct
pub const Array = extern struct {
    typ: u32,
    index_type: u32,
    nelems: u32,
};

/// struct and union kinds are followed by multiple Member structs. The exact
/// number is stored in vlen
pub const Member = extern struct {
    name_off: u32,
    typ: u32,

    /// if the kind_flag is set, offset contains both member bitfield size and
    /// bit offset, the bitfield size is set for bitfield members. If the type
    /// info kind_flag is not set, the offset contains only bit offset
    offset: packed struct(u32) {
        bit: u24,
        bitfield_size: u8,
    },
};

/// func_proto is followed by multiple Params, the exact number is stored in vlen
pub const Param = extern struct {
    name_off: u32,
    typ: u32,
};

pub const VarLinkage = enum {
    static,
    global_allocated,
    global_extern,
};

pub const FuncLinkage = enum {
    static,
    global,
    external,
};

/// var kind is followed by a single Var struct to describe additional
/// information related to the variable such as its linkage
pub const Var = extern struct {
    linkage: u32,
};

/// datasec kind is followed by multiple VarSecInfo to describe all Var kind
/// types it contains along with it's in-section offset as well as size.
pub const VarSecInfo = extern struct {
    typ: u32,
    offset: u32,
    size: u32,
};

// decl_tag kind is followed by a single DeclTag struct to describe
// additional information related to the tag applied location.
// If component_idx == -1, the tag is applied to a struct, union,
// variable or function. Otherwise, it is applied to a struct/union
// member or a func argument, and component_idx indicates which member
// or argument (0 ... vlen-1).
pub const DeclTag = extern struct {
    component_idx: u32,
};



---
File: /std/os/linux/bpf/helpers.zig
---

const std = @import("../../../std.zig");
const kern = @import("kern.zig");

const PtRegs = @compileError("TODO missing os bits: PtRegs");
const TcpHdr = @compileError("TODO missing os bits: TcpHdr");
const SkFullSock = @compileError("TODO missing os bits: SkFullSock");

// in BPF, all the helper calls
// TODO: when https://github.com/ziglang/zig/issues/1717 is here, make a nice
// function that uses the Helper enum
//
// Note, these function signatures were created from documentation found in
// '/usr/include/linux/bpf.h'
pub const map_lookup_elem: *align(1) const fn (map: *const kern.MapDef, key: ?*const anyopaque) ?*anyopaque = @ptrFromInt(1);
pub const map_update_elem: *align(1) const fn (map: *const kern.MapDef, key: ?*const anyopaque, value: ?*const anyopaque, flags: u64) c_long = @ptrFromInt(2);
pub const map_delete_elem: *align(1) const fn (map: *const kern.MapDef, key: ?*const anyopaque) c_long = @ptrFromInt(3);
pub const probe_read: *align(1) const fn (dst: ?*anyopaque, size: u32, unsafe_ptr: ?*const anyopaque) c_long = @ptrFromInt(4);
pub const ktime_get_ns: *align(1) const fn () u64 = @ptrFromInt(5);
pub const trace_printk: *align(1) const fn (fmt: [*:0]const u8, fmt_size: u32, arg1: u64, arg2: u64, arg3: u64) c_long = @ptrFromInt(6);
pub const get_prandom_u32: *align(1) const fn () u32 = @ptrFromInt(7);
pub const get_smp_processor_id: *align(1) const fn () u32 = @ptrFromInt(8);
pub const skb_store_bytes: *align(1) const fn (skb: *kern.SkBuff, offset: u32, from: ?*const anyopaque, len: u32, flags: u64) c_long = @ptrFromInt(9);
pub const l3_csum_replace: *align(1) const fn (skb: *kern.SkBuff, offset: u32, from: u64, to: u64, size: u64) c_long = @ptrFromInt(10);
pub const l4_csum_replace: *align(1) const fn (skb: *kern.SkBuff, offset: u32, from: u64, to: u64, flags: u64) c_long = @ptrFromInt(11);
pub const tail_call: *align(1) const fn (ctx: ?*anyopaque, prog_array_map: *const kern.MapDef, index: u32) c_long = @ptrFromInt(12);
pub const clone_redirect: *align(1) const fn (skb: *kern.SkBuff, ifindex: u32, flags: u64) c_long = @ptrFromInt(13);
pub const get_current_pid_tgid: *align(1) const fn () u64 = @ptrFromInt(14);
pub const get_current_uid_gid: *align(1) const fn () u64 = @ptrFromInt(15);
pub const get_current_comm: *align(1) const fn (buf: ?*anyopaque, size_of_buf: u32) c_long = @ptrFromInt(16);
pub const get_cgroup_classid: *align(1) const fn (skb: *kern.SkBuff) u32 = @ptrFromInt(17);
// Note vlan_proto is big endian
pub const skb_vlan_push: *align(1) const fn (skb: *kern.SkBuff, vlan_proto: u16, vlan_tci: u16) c_long = @ptrFromInt(18);
pub const skb_vlan_pop: *align(1) const fn (skb: *kern.SkBuff) c_long = @ptrFromInt(19);
pub const skb_get_tunnel_key: *align(1) const fn (skb: *kern.SkBuff, key: *kern.TunnelKey, size: u32, flags: u64) c_long = @ptrFromInt(20);
pub const skb_set_tunnel_key: *align(1) const fn (skb: *kern.SkBuff, key: *kern.TunnelKey, size: u32, flags: u64) c_long = @ptrFromInt(21);
pub const perf_event_read: *align(1) const fn (map: *const kern.MapDef, flags: u64) u64 = @ptrFromInt(22);
pub const redirect: *align(1) const fn (ifindex: u32, flags: u64) c_long = @ptrFromInt(23);
pub const get_route_realm: *align(1) const fn (skb: *kern.SkBuff) u32 = @ptrFromInt(24);
pub const perf_event_output: *align(1) const fn (ctx: ?*anyopaque, map: *const kern.MapDef, flags: u64, data: ?*anyopaque, size: u64) c_long = @ptrFromInt(25);
pub const skb_load_bytes: *align(1) const fn (skb: ?*anyopaque, offset: u32, to: ?*anyopaque, len: u32) c_long = @ptrFromInt(26);
pub const get_stackid: *align(1) const fn (ctx: ?*anyopaque, map: *const kern.MapDef, flags: u64) c_long = @ptrFromInt(27);
// from and to point to __be32
pub const csum_diff: *align(1) const fn (from: *u32, from_size: u32, to: *u32, to_size: u32, seed: u32) i64 = @ptrFromInt(28);
pub const skb_get_tunnel_opt: *align(1) const fn (skb: *kern.SkBuff, opt: ?*anyopaque, size: u32) c_long = @ptrFromInt(29);
pub const skb_set_tunnel_opt: *align(1) const fn (skb: *kern.SkBuff, opt: ?*anyopaque, size: u32) c_long = @ptrFromInt(30);
// proto is __be16
pub const skb_change_proto: *align(1) const fn (skb: *kern.SkBuff, proto: u16, flags: u64) c_long = @ptrFromInt(31);
pub const skb_change_type: *align(1) const fn (skb: *kern.SkBuff, skb_type: u32) c_long = @ptrFromInt(32);
pub const skb_under_cgroup: *align(1) const fn (skb: *kern.SkBuff, map: ?*const anyopaque, index: u32) c_long = @ptrFromInt(33);
pub const get_hash_recalc: *align(1) const fn (skb: *kern.SkBuff) u32 = @ptrFromInt(34);
pub const get_current_task: *align(1) const fn () u64 = @ptrFromInt(35);
pub const probe_write_user: *align(1) const fn (dst: ?*anyopaque, src: ?*const anyopaque, len: u32) c_long = @ptrFromInt(36);
pub const current_task_under_cgroup: *align(1) const fn (map: *const kern.MapDef, index: u32) c_long = @ptrFromInt(37);
pub const skb_change_tail: *align(1) const fn (skb: *kern.SkBuff, len: u32, flags: u64) c_long = @ptrFromInt(38);
pub const skb_pull_data: *align(1) const fn (skb: *kern.SkBuff, len: u32) c_long = @ptrFromInt(39);
pub const csum_update: *align(1) const fn (skb: *kern.SkBuff, csum: u32) i64 = @ptrFromInt(40);
pub const set_hash_invalid: *align(1) const fn (skb: *kern.SkBuff) void = @ptrFromInt(41);
pub const get_numa_node_id: *align(1) const fn () c_long = @ptrFromInt(42);
pub const skb_change_head: *align(1) const fn (skb: *kern.SkBuff, len: u32, flags: u64) c_long = @ptrFromInt(43);
pub const xdp_adjust_head: *align(1) const fn (xdp_md: *kern.XdpMd, delta: c_int) c_long = @ptrFromInt(44);
pub const probe_read_str: *align(1) const fn (dst: ?*anyopaque, size: u32, unsafe_ptr: ?*const anyopaque) c_long = @ptrFromInt(45);
pub const get_socket_cookie: *align(1) const fn (ctx: ?*anyopaque) u64 = @ptrFromInt(46);
pub const get_socket_uid: *align(1) const fn (skb: *kern.SkBuff) u32 = @ptrFromInt(47);
pub const set_hash: *align(1) const fn (skb: *kern.SkBuff, hash: u32) c_long = @ptrFromInt(48);
pub const setsockopt: *align(1) const fn (bpf_socket: *kern.SockOps, level: c_int, optname: c_int, optval: ?*anyopaque, optlen: c_int) c_long = @ptrFromInt(49);
pub const skb_adjust_room: *align(1) const fn (skb: *kern.SkBuff, len_diff: i32, mode: u32, flags: u64) c_long = @ptrFromInt(50);
pub const redirect_map: *align(1) const fn (map: *const kern.MapDef, key: u32, flags: u64) c_long = @ptrFromInt(51);
pub const sk_redirect_map: *align(1) const fn (skb: *kern.SkBuff, map: *const kern.MapDef, key: u32, flags: u64) c_long = @ptrFromInt(52);
pub const sock_map_update: *align(1) const fn (skops: *kern.SockOps, map: *const kern.MapDef, key: ?*anyopaque, flags: u64) c_long = @ptrFromInt(53);
pub const xdp_adjust_meta: *align(1) const fn (xdp_md: *kern.XdpMd, delta: c_int) c_long = @ptrFromInt(54);
pub const perf_event_read_value: *align(1) const fn (map: *const kern.MapDef, flags: u64, buf: *kern.PerfEventValue, buf_size: u32) c_long = @ptrFromInt(55);
pub const perf_prog_read_value: *align(1) const fn (ctx: *kern.PerfEventData, buf: *kern.PerfEventValue, buf_size: u32) c_long = @ptrFromInt(56);
pub const getsockopt: *align(1) const fn (bpf_socket: ?*anyopaque, level: c_int, optname: c_int, optval: ?*anyopaque, optlen: c_int) c_long = @ptrFromInt(57);
pub const override_return: *align(1) const fn (regs: *PtRegs, rc: u64) c_long = @ptrFromInt(58);
pub const sock_ops_cb_flags_set: *align(1) const fn (bpf_sock: *kern.SockOps, argval: c_int) c_long = @ptrFromInt(59);
pub const msg_redirect_map: *align(1) const fn (msg: *kern.SkMsgMd, map: *const kern.MapDef, key: u32, flags: u64) c_long = @ptrFromInt(60);
pub const msg_apply_bytes: *align(1) const fn (msg: *kern.SkMsgMd, bytes: u32) c_long = @ptrFromInt(61);
pub const msg_cork_bytes: *align(1) const fn (msg: *kern.SkMsgMd, bytes: u32) c_long = @ptrFromInt(62);
pub const msg_pull_data: *align(1) const fn (msg: *kern.SkMsgMd, start: u32, end: u32, flags: u64) c_long = @ptrFromInt(63);
pub const bind: *align(1) const fn (ctx: *kern.BpfSockAddr, addr: *kern.SockAddr, addr_len: c_int) c_long = @ptrFromInt(64);
pub const xdp_adjust_tail: *align(1) const fn (xdp_md: *kern.XdpMd, delta: c_int) c_long = @ptrFromInt(65);
pub const skb_get_xfrm_state: *align(1) const fn (skb: *kern.SkBuff, index: u32, xfrm_state: *kern.XfrmState, size: u32, flags: u64) c_long = @ptrFromInt(66);
pub const get_stack: *align(1) const fn (ctx: ?*anyopaque, buf: ?*anyopaque, size: u32, flags: u64) c_long = @ptrFromInt(67);
pub const skb_load_bytes_relative: *align(1) const fn (skb: ?*const anyopaque, offset: u32, to: ?*anyopaque, len: u32, start_header: u32) c_long = @ptrFromInt(68);
pub const fib_lookup: *align(1) const fn (ctx: ?*anyopaque, params: *kern.FibLookup, plen: c_int, flags: u32) c_long = @ptrFromInt(69);
pub const sock_hash_update: *align(1) const fn (skops: *kern.SockOps, map: *const kern.MapDef, key: ?*anyopaque, flags: u64) c_long = @ptrFromInt(70);
pub const msg_redirect_hash: *align(1) const fn (msg: *kern.SkMsgMd, map: *const kern.MapDef, key: ?*anyopaque, flags: u64) c_long = @ptrFromInt(71);
pub const sk_redirect_hash: *align(1) const fn (skb: *kern.SkBuff, map: *const kern.MapDef, key: ?*anyopaque, flags: u64) c_long = @ptrFromInt(72);
pub const lwt_push_encap: *align(1) const fn (skb: *kern.SkBuff, typ: u32, hdr: ?*anyopaque, len: u32) c_long = @ptrFromInt(73);
pub const lwt_seg6_store_bytes: *align(1) const fn (skb: *kern.SkBuff, offset: u32, from: ?*const anyopaque, len: u32) c_long = @ptrFromInt(74);
pub const lwt_seg6_adjust_srh: *align(1) const fn (skb: *kern.SkBuff, offset: u32, delta: i32) c_long = @ptrFromInt(75);
pub const lwt_seg6_action: *align(1) const fn (skb: *kern.SkBuff, action: u32, param: ?*anyopaque, param_len: u32) c_long = @ptrFromInt(76);
pub const rc_repeat: *align(1) const fn (ctx: ?*anyopaque) c_long = @ptrFromInt(77);
pub const rc_keydown: *align(1) const fn (ctx: ?*anyopaque, protocol: u32, scancode: u64, toggle: u32) c_long = @ptrFromInt(78);
pub const skb_cgroup_id: *align(1) const fn (skb: *kern.SkBuff) u64 = @ptrFromInt(79);
pub const get_current_cgroup_id: *align(1) const fn () u64 = @ptrFromInt(80);
pub const get_local_storage: *align(1) const fn (map: ?*anyopaque, flags: u64) ?*anyopaque = @ptrFromInt(81);
pub const sk_select_reuseport: *align(1) const fn (reuse: *kern.SkReusePortMd, map: *const kern.MapDef, key: ?*anyopaque, flags: u64) c_long = @ptrFromInt(82);
pub const skb_ancestor_cgroup_id: *align(1) const fn (skb: *kern.SkBuff, ancestor_level: c_int) u64 = @ptrFromInt(83);
pub const sk_lookup_tcp: *align(1) const fn (ctx: ?*anyopaque, tuple: *kern.SockTuple, tuple_size: u32, netns: u64, flags: u64) ?*kern.Sock = @ptrFromInt(84);
pub const sk_lookup_udp: *align(1) const fn (ctx: ?*anyopaque, tuple: *kern.SockTuple, tuple_size: u32, netns: u64, flags: u64) ?*kern.Sock = @ptrFromInt(85);
pub const sk_release: *align(1) const fn (sock: *kern.Sock) c_long = @ptrFromInt(86);
pub const map_push_elem: *align(1) const fn (map: *const kern.MapDef, value: ?*const anyopaque, flags: u64) c_long = @ptrFromInt(87);
pub const map_pop_elem: *align(1) const fn (map: *const kern.MapDef, value: ?*anyopaque) c_long = @ptrFromInt(88);
pub const map_peek_elem: *align(1) const fn (map: *const kern.MapDef, value: ?*anyopaque) c_long = @ptrFromInt(89);
pub const msg_push_data: *align(1) const fn (msg: *kern.SkMsgMd, start: u32, len: u32, flags: u64) c_long = @ptrFromInt(90);
pub const msg_pop_data: *align(1) const fn (msg: *kern.SkMsgMd, start: u32, len: u32, flags: u64) c_long = @ptrFromInt(91);
pub const rc_pointer_rel: *align(1) const fn (ctx: ?*anyopaque, rel_x: i32, rel_y: i32) c_long = @ptrFromInt(92);
pub const spin_lock: *align(1) const fn (lock: *kern.SpinLock) c_long = @ptrFromInt(93);
pub const spin_unlock: *align(1) const fn (lock: *kern.SpinLock) c_long = @ptrFromInt(94);
pub const sk_fullsock: *align(1) const fn (sk: *kern.Sock) ?*SkFullSock = @ptrFromInt(95);
pub const tcp_sock: *align(1) const fn (sk: *kern.Sock) ?*kern.TcpSock = @ptrFromInt(96);
pub const skb_ecn_set_ce: *align(1) const fn (skb: *kern.SkBuff) c_long = @ptrFromInt(97);
pub const get_listener_sock: *align(1) const fn (sk: *kern.Sock) ?*kern.Sock = @ptrFromInt(98);
pub const skc_lookup_tcp: *align(1) const fn (ctx: ?*anyopaque, tuple: *kern.SockTuple, tuple_size: u32, netns: u64, flags: u64) ?*kern.Sock = @ptrFromInt(99);
pub const tcp_check_syncookie: *align(1) const fn (sk: *kern.Sock, iph: ?*anyopaque, iph_len: u32, th: *TcpHdr, th_len: u32) c_long = @ptrFromInt(100);
pub const sysctl_get_name: *align(1) const fn (ctx: *kern.SysCtl, buf: ?*u8, buf_len: c_ulong, flags: u64) c_long = @ptrFromInt(101);
pub const sysctl_get_current_value: *align(1) const fn (ctx: *kern.SysCtl, buf: ?*u8, buf_len: c_ulong) c_long = @ptrFromInt(102);
pub const sysctl_get_new_value: *align(1) const fn (ctx: *kern.SysCtl, buf: ?*u8, buf_len: c_ulong) c_long = @ptrFromInt(103);
pub const sysctl_set_new_value: *align(1) const fn (ctx: *kern.SysCtl, buf: ?*const u8, buf_len: c_ulong) c_long = @ptrFromInt(104);
pub const strtol: *align(1) const fn (buf: *const u8, buf_len: c_ulong, flags: u64, res: *c_long) c_long = @ptrFromInt(105);
pub const strtoul: *align(1) const fn (buf: *const u8, buf_len: c_ulong, flags: u64, res: *c_ulong) c_long = @ptrFromInt(106);
pub const sk_storage_get: *align(1) const fn (map: *const kern.MapDef, sk: *kern.Sock, value: ?*anyopaque, flags: u64) ?*anyopaque = @ptrFromInt(107);
pub const sk_storage_delete: *align(1) const fn (map: *const kern.MapDef, sk: *kern.Sock) c_long = @ptrFromInt(108);
pub const send_signal: *align(1) const fn (sig: u32) c_long = @ptrFromInt(109);
pub const tcp_gen_syncookie: *align(1) const fn (sk: *kern.Sock, iph: ?*anyopaque, iph_len: u32, th: *TcpHdr, th_len: u32) i64 = @ptrFromInt(110);
pub const skb_output: *align(1) const fn (ctx: ?*anyopaque, map: *const kern.MapDef, flags: u64, data: ?*anyopaque, size: u64) c_long = @ptrFromInt(111);
pub const probe_read_user: *align(1) const fn (dst: ?*anyopaque, size: u32, unsafe_ptr: ?*const anyopaque) c_long = @ptrFromInt(112);
pub const probe_read_kernel: *align(1) const fn (dst: ?*anyopaque, size: u32, unsafe_ptr: ?*const anyopaque) c_long = @ptrFromInt(113);
pub const probe_read_user_str: *align(1) const fn (dst: ?*anyopaque, size: u32, unsafe_ptr: ?*const anyopaque) c_long = @ptrFromInt(114);
pub const probe_read_kernel_str: *align(1) const fn (dst: ?*anyopaque, size: u32, unsafe_ptr: ?*const anyopaque) c_long = @ptrFromInt(115);
pub const tcp_send_ack: *align(1) const fn (tp: ?*anyopaque, rcv_nxt: u32) c_long = @ptrFromInt(116);
pub const send_signal_thread: *align(1) const fn (sig: u32) c_long = @ptrFromInt(117);
pub const jiffies64: *align(1) const fn () u64 = @ptrFromInt(118);
pub const read_branch_records: *align(1) const fn (ctx: *kern.PerfEventData, buf: ?*anyopaque, size: u32, flags: u64) c_long = @ptrFromInt(119);
pub const get_ns_current_pid_tgid: *align(1) const fn (dev: u64, ino: u64, nsdata: *kern.PidNsInfo, size: u32) c_long = @ptrFromInt(120);
pub const xdp_output: *align(1) const fn (ctx: ?*anyopaque, map: *const kern.MapDef, flags: u64, data: ?*anyopaque, size: u64) c_long = @ptrFromInt(121);
pub const get_netns_cookie: *align(1) const fn (ctx: ?*anyopaque) u64 = @ptrFromInt(122);
pub const get_current_ancestor_cgroup_id: *align(1) const fn (ancestor_level: c_int) u64 = @ptrFromInt(123);
pub const sk_assign: *align(1) const fn (skb: *kern.SkBuff, sk: *kern.Sock, flags: u64) c_long = @ptrFromInt(124);
pub const ktime_get_boot_ns: *align(1) const fn () u64 = @ptrFromInt(125);
pub const seq_printf: *align(1) const fn (m: *kern.SeqFile, fmt: ?*const u8, fmt_size: u32, data: ?*const anyopaque, data_len: u32) c_long = @ptrFromInt(126);
pub const seq_write: *align(1) const fn (m: *kern.SeqFile, data: ?*const u8, len: u32) c_long = @ptrFromInt(127);
pub const sk_cgroup_id: *align(1) const fn (sk: *kern.BpfSock) u64 = @ptrFromInt(128);
pub const sk_ancestor_cgroup_id: *align(1) const fn (sk: *kern.BpfSock, ancestor_level: c_long) u64 = @ptrFromInt(129);
pub const ringbuf_output: *align(1) const fn (ringbuf: ?*anyopaque, data: ?*anyopaque, size: u64, flags: u64) c_long = @ptrFromInt(130);
pub const ringbuf_reserve: *align(1) const fn (ringbuf: ?*anyopaque, size: u64, flags: u64) ?*anyopaque = @ptrFromInt(131);
pub const ringbuf_submit: *align(1) const fn (data: ?*anyopaque, flags: u64) void = @ptrFromInt(132);
pub const ringbuf_discard: *align(1) const fn (data: ?*anyopaque, flags: u64) void = @ptrFromInt(133);
pub const ringbuf_query: *align(1) const fn (ringbuf: ?*anyopaque, flags: u64) u64 = @ptrFromInt(134);
pub const csum_level: *align(1) const fn (skb: *kern.SkBuff, level: u64) c_long = @ptrFromInt(135);
pub const skc_to_tcp6_sock: *align(1) const fn (sk: ?*anyopaque) ?*kern.Tcp6Sock = @ptrFromInt(136);
pub const skc_to_tcp_sock: *align(1) const fn (sk: ?*anyopaque) ?*kern.TcpSock = @ptrFromInt(137);
pub const skc_to_tcp_timewait_sock: *align(1) const fn (sk: ?*anyopaque) ?*kern.TcpTimewaitSock = @ptrFromInt(138);
pub const skc_to_tcp_request_sock: *align(1) const fn (sk: ?*anyopaque) ?*kern.TcpRequestSock = @ptrFromInt(139);
pub const skc_to_udp6_sock: *align(1) const fn (sk: ?*anyopaque) ?*kern.Udp6Sock = @ptrFromInt(140);
pub const get_task_stack: *align(1) const fn (task: ?*anyopaque, buf: ?*anyopaque, size: u32, flags: u64) c_long = @ptrFromInt(141);
pub const load_hdr_opt: *align(1) const fn (?*kern.BpfSockOps, ?*anyopaque, u32, u64) c_long = @ptrFromInt(142);
pub const store_hdr_opt: *align(1) const fn (?*kern.BpfSockOps, ?*const anyopaque, u32, u64) c_long = @ptrFromInt(143);
pub const reserve_hdr_opt: *align(1) const fn (?*kern.BpfSockOps, u32, u64) c_long = @ptrFromInt(144);
pub const inode_storage_get: *align(1) const fn (?*anyopaque, ?*anyopaque, ?*anyopaque, u64) ?*anyopaque = @ptrFromInt(145);
pub const inode_storage_delete: *align(1) const fn (?*anyopaque, ?*anyopaque) c_int = @ptrFromInt(146);
pub const d_path: *align(1) const fn (?*kern.Path, [*c]u8, u32) c_long = @ptrFromInt(147);
pub const copy_from_user: *align(1) const fn (?*anyopaque, u32, ?*const anyopaque) c_long = @ptrFromInt(148);
pub const snprintf_btf: *align(1) const fn ([*c]u8, u32, ?*kern.BTFPtr, u32, u64) c_long = @ptrFromInt(149);
pub const seq_printf_btf: *align(1) const fn (?*kern.SeqFile, ?*kern.BTFPtr, u32, u64) c_long = @ptrFromInt(150);
pub const skb_cgroup_classid: *align(1) const fn (?*kern.SkBuff) u64 = @ptrFromInt(151);
pub const redirect_neigh: *align(1) const fn (u32, ?*kern.BpfRedirNeigh, c_int, u64) c_long = @ptrFromInt(152);
pub const per_cpu_ptr: *align(1) const fn (?*const anyopaque, u32) ?*anyopaque = @ptrFromInt(153);
pub const this_cpu_ptr: *align(1) const fn (?*const anyopaque) ?*anyopaque = @ptrFromInt(154);
pub const redirect_peer: *align(1) const fn (u32, u64) c_long = @ptrFromInt(155);
pub const task_storage_get: *align(1) const fn (?*anyopaque, ?*kern.Task, ?*anyopaque, u64) ?*anyopaque = @ptrFromInt(156);
pub const task_storage_delete: *align(1) const fn (?*anyopaque, ?*kern.Task) c_long = @ptrFromInt(157);
pub const get_current_task_btf: *align(1) const fn () ?*kern.Task = @ptrFromInt(158);
pub const bprm_opts_set: *align(1) const fn (?*kern.BinPrm, u64) c_long = @ptrFromInt(159);
pub const ktime_get_coarse_ns: *align(1) const fn () u64 = @ptrFromInt(160);
pub const ima_inode_hash: *align(1) const fn (?*kern.Inode, ?*anyopaque, u32) c_long = @ptrFromInt(161);
pub const sock_from_file: *align(1) const fn (?*kern.File) ?*kern.Socket = @ptrFromInt(162);
pub const check_mtu: *align(1) const fn (?*anyopaque, u32, [*c]u32, i32, u64) c_long = @ptrFromInt(163);
pub const for_each_map_elem: *align(1) const fn (?*anyopaque, ?*anyopaque, ?*anyopaque, u64) c_long = @ptrFromInt(164);
pub const snprintf: *align(1) const fn ([*c]u8, u32, [*c]const u8, [*c]u64, u32) c_long = @ptrFromInt(165);
pub const sys_bpf: *align(1) const fn (u32, ?*anyopaque, u32) c_long = @ptrFromInt(166);
pub const btf_find_by_name_kind: *align(1) const fn ([*c]u8, c_int, u32, c_int) c_long = @ptrFromInt(167);
pub const sys_close: *align(1) const fn (u32) c_long = @ptrFromInt(168);
pub const timer_init: *align(1) const fn (?*kern.BpfTimer, ?*anyopaque, u64) c_long = @ptrFromInt(169);
pub const timer_set_callback: *align(1) const fn (?*kern.BpfTimer, ?*anyopaque) c_long = @ptrFromInt(170);
pub const timer_start: *align(1) const fn (?*kern.BpfTimer, u64, u64) c_long = @ptrFromInt(171);
pub const timer_cancel: *align(1) const fn (?*kern.BpfTimer) c_long = @ptrFromInt(172);
pub const get_func_ip: *align(1) const fn (?*anyopaque) u64 = @ptrFromInt(173);
pub const get_attach_cookie: *align(1) const fn (?*anyopaque) u64 = @ptrFromInt(174);
pub const task_pt_regs: *align(1) const fn (?*kern.Task) c_long = @ptrFromInt(175);
pub const get_branch_snapshot: *align(1) const fn (?*anyopaque, u32, u64) c_long = @ptrFromInt(176);
pub const trace_vprintk: *align(1) const fn ([*c]const u8, u32, ?*const anyopaque, u32) c_long = @ptrFromInt(177);
pub const skc_to_unix_sock: *align(1) const fn (?*anyopaque) ?*kern.UnixSock = @ptrFromInt(178);
pub const kallsyms_lookup_name: *align(1) const fn ([*c]const u8, c_int, c_int, [*c]u64) c_long = @ptrFromInt(179);
pub const find_vma: *align(1) const fn (?*kern.Task, u64, ?*anyopaque, ?*anyopaque, u64) c_long = @ptrFromInt(180);
pub const loop: *align(1) const fn (u32, ?*anyopaque, ?*anyopaque, u64) c_long = @ptrFromInt(181);
pub const strncmp: *align(1) const fn ([*c]const u8, u32, [*c]const u8) c_long = @ptrFromInt(182);
pub const get_func_arg: *align(1) const fn (?*anyopaque, u32, [*c]u64) c_long = @ptrFromInt(183);
pub const get_func_ret: *align(1) const fn (?*anyopaque, [*c]u64) c_long = @ptrFromInt(184);
pub const get_func_arg_cnt: *align(1) const fn (?*anyopaque) c_long = @ptrFromInt(185);
pub const get_retval: *align(1) const fn () c_int = @ptrFromInt(186);
pub const set_retval: *align(1) const fn (c_int) c_int = @ptrFromInt(187);
pub const xdp_get_buff_len: *align(1) const fn (?*kern.XdpMd) u64 = @ptrFromInt(188);
pub const xdp_load_bytes: *align(1) const fn (?*kern.XdpMd, u32, ?*anyopaque, u32) c_long = @ptrFromInt(189);
pub const xdp_store_bytes: *align(1) const fn (?*kern.XdpMd, u32, ?*anyopaque, u32) c_long = @ptrFromInt(190);
pub const copy_from_user_task: *align(1) const fn (?*anyopaque, u32, ?*const anyopaque, ?*kern.Task, u64) c_long = @ptrFromInt(191);
pub const skb_set_tstamp: *align(1) const fn (?*kern.SkBuff, u64, u32) c_long = @ptrFromInt(192);
pub const ima_file_hash: *align(1) const fn (?*kern.File, ?*anyopaque, u32) c_long = @ptrFromInt(193);
pub const kptr_xchg: *align(1) const fn (?*anyopaque, ?*anyopaque) ?*anyopaque = @ptrFromInt(194);
pub const map_lookup_percpu_elem: *align(1) const fn (?*anyopaque, ?*const anyopaque, u32) ?*anyopaque = @ptrFromInt(195);
pub const skc_to_mptcp_sock: *align(1) const fn (?*anyopaque) ?*kern.MpTcpSock = @ptrFromInt(196);
pub const dynptr_from_mem: *align(1) const fn (?*anyopaque, u32, u64, ?*kern.BpfDynPtr) c_long = @ptrFromInt(197);
pub const ringbuf_reserve_dynptr: *align(1) const fn (?*anyopaque, u32, u64, ?*kern.BpfDynPtr) c_long = @ptrFromInt(198);
pub const ringbuf_submit_dynptr: *align(1) const fn (?*kern.BpfDynPtr, u64) void = @ptrFromInt(199);
pub const ringbuf_discard_dynptr: *align(1) const fn (?*kern.BpfDynPtr, u64) void = @ptrFromInt(200);
pub const dynptr_read: *align(1) const fn (?*anyopaque, u32, ?*kern.BpfDynPtr, u32, u64) c_long = @ptrFromInt(201);
pub const dynptr_write: *align(1) const fn (?*kern.BpfDynPtr, u32, ?*anyopaque, u32, u64) c_long = @ptrFromInt(202);
pub const dynptr_data: *align(1) const fn (?*kern.BpfDynPtr, u32, u32) ?*anyopaque = @ptrFromInt(203);
pub const tcp_raw_gen_syncookie_ipv4: *align(1) const fn (?*kern.IpHdr, ?*TcpHdr, u32) i64 = @ptrFromInt(204);
pub const tcp_raw_gen_syncookie_ipv6: *align(1) const fn (?*kern.Ipv6Hdr, ?*TcpHdr, u32) i64 = @ptrFromInt(205);
pub const tcp_raw_check_syncookie_ipv4: *align(1) const fn (?*kern.IpHdr, ?*TcpHdr) c_long = @ptrFromInt(206);
pub const tcp_raw_check_syncookie_ipv6: *align(1) const fn (?*kern.Ipv6Hdr, ?*TcpHdr) c_long = @ptrFromInt(207);
pub const ktime_get_tai_ns: *align(1) const fn () u64 = @ptrFromInt(208);
pub const user_ringbuf_drain: *align(1) const fn (?*anyopaque, ?*anyopaque, ?*anyopaque, u64) c_long = @ptrFromInt(209);
pub const cgrp_storage_get: *align(1) const fn (?*anyopaque, ?*kern.Cgroup, ?*anyopaque, u64) ?*anyopaque = @ptrFromInt(210);
pub const cgrp_storage_delete: *align(1) const fn (?*anyopaque, ?*kern.Cgroup) c_long = @ptrFromInt(211);



---
File: /std/os/linux/bpf/kern.zig
---

const std = @import("../../../std.zig");
const builtin = @import("builtin");

const in_bpf_program = switch (builtin.cpu.arch) {
    .bpfel, .bpfeb => true,
    else => false,
};

pub const helpers = if (in_bpf_program) @import("helpers.zig") else struct {};

pub const BinPrm = opaque {};
pub const BTFPtr = opaque {};
pub const BpfDynPtr = opaque {};
pub const BpfRedirNeigh = opaque {};
pub const BpfSock = opaque {};
pub const BpfSockAddr = opaque {};
pub const BpfSockOps = opaque {};
pub const BpfTimer = opaque {};
pub const Cgroup = opaque {};
pub const FibLookup = opaque {};
pub const File = opaque {};
pub const Inode = opaque {};
pub const IpHdr = opaque {};
pub const Ipv6Hdr = opaque {};
pub const MapDef = opaque {};
pub const MpTcpSock = opaque {};
pub const Path = opaque {};
pub const PerfEventData = opaque {};
pub const PerfEventValue = opaque {};
pub const PidNsInfo = opaque {};
pub const SeqFile = opaque {};
pub const SkBuff = opaque {};
pub const SkMsgMd = opaque {};
pub const SkReusePortMd = opaque {};
pub const Sock = opaque {};
pub const Socket = opaque {};
pub const SockAddr = opaque {};
pub const SockOps = opaque {};
pub const SockTuple = opaque {};
pub const SpinLock = opaque {};
pub const SysCtl = opaque {};
pub const Task = opaque {};
pub const Tcp6Sock = opaque {};
pub const TcpRequestSock = opaque {};
pub const TcpSock = opaque {};
pub const TcpTimewaitSock = opaque {};
pub const TunnelKey = opaque {};
pub const Udp6Sock = opaque {};
pub const UnixSock = opaque {};
pub const XdpMd = opaque {};
pub const XfrmState = opaque {};



---
File: /std/os/linux/IoUring/test.zig
---




---
File: /std/os/linux/aarch64.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u64 {
    return asm volatile ("svc #0"
        : [ret] "={x0}" (-> u64),
        : [number] "{x8}" (@intFromEnum(number)),
        : .{ .memory = true });
}

pub fn syscall1(number: SYS, arg1: u64) u64 {
    return asm volatile ("svc #0"
        : [ret] "={x0}" (-> u64),
        : [number] "{x8}" (@intFromEnum(number)),
          [arg1] "{x0}" (arg1),
        : .{ .memory = true });
}

pub fn syscall2(number: SYS, arg1: u64, arg2: u64) u64 {
    return asm volatile ("svc #0"
        : [ret] "={x0}" (-> u64),
        : [number] "{x8}" (@intFromEnum(number)),
          [arg1] "{x0}" (arg1),
          [arg2] "{x1}" (arg2),
        : .{ .memory = true });
}

pub fn syscall3(number: SYS, arg1: u64, arg2: u64, arg3: u64) u64 {
    return asm volatile ("svc #0"
        : [ret] "={x0}" (-> u64),
        : [number] "{x8}" (@intFromEnum(number)),
          [arg1] "{x0}" (arg1),
          [arg2] "{x1}" (arg2),
          [arg3] "{x2}" (arg3),
        : .{ .memory = true });
}

pub fn syscall4(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64) u64 {
    return asm volatile ("svc #0"
        : [ret] "={x0}" (-> u64),
        : [number] "{x8}" (@intFromEnum(number)),
          [arg1] "{x0}" (arg1),
          [arg2] "{x1}" (arg2),
          [arg3] "{x2}" (arg3),
          [arg4] "{x3}" (arg4),
        : .{ .memory = true });
}

pub fn syscall5(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64, arg5: u64) u64 {
    return asm volatile ("svc #0"
        : [ret] "={x0}" (-> u64),
        : [number] "{x8}" (@intFromEnum(number)),
          [arg1] "{x0}" (arg1),
          [arg2] "{x1}" (arg2),
          [arg3] "{x2}" (arg3),
          [arg4] "{x3}" (arg4),
          [arg5] "{x4}" (arg5),
        : .{ .memory = true });
}

pub fn syscall6(
    number: SYS,
    arg1: u64,
    arg2: u64,
    arg3: u64,
    arg4: u64,
    arg5: u64,
    arg6: u64,
) u64 {
    return asm volatile ("svc #0"
        : [ret] "={x0}" (-> u64),
        : [number] "{x8}" (@intFromEnum(number)),
          [arg1] "{x0}" (arg1),
          [arg2] "{x1}" (arg2),
          [arg3] "{x2}" (arg3),
          [arg4] "{x3}" (arg4),
          [arg5] "{x4}" (arg5),
          [arg6] "{x5}" (arg6),
        : .{ .memory = true });
}

pub fn clone() callconv(.naked) u64 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         x0,   x1,    w2,    x3,  x4,   x5,  x6
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         x8,        x0,    x1,    x2,   x3,  x4
    asm volatile (
        \\      // align stack and save func,arg
        \\      and x1,x1,#-16
        \\      stp x0,x3,[x1,#-16]!
        \\
        \\      // syscall
        \\      uxtw x0,w2
        \\      mov x2,x4
        \\      mov x3,x5
        \\      mov x4,x6
        \\      mov x8,#220 // SYS_clone
        \\      svc #0
        \\
        \\      cbz x0,1f
        \\      // parent
        \\      ret
        \\
        \\      // child
        \\1:
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\     .cfi_undefined lr
    );
    asm volatile (
        \\      mov fp, 0
        \\      mov lr, 0
        \\
        \\      ldp x1,x0,[sp],#16
        \\      blr x1
        \\      mov x8,#93 // SYS_exit
        \\      svc #0
    );
}

pub const restore = restore_rt;

pub fn restore_rt() callconv(.naked) noreturn {
    switch (builtin.zig_backend) {
        .stage2_c => asm volatile (
            \\ mov x8, %[number]
            \\ svc #0
            :
            : [number] "i" (@intFromEnum(SYS.rt_sigreturn)),
        ),
        else => asm volatile (
            \\ svc #0
            :
            : [number] "{x8}" (@intFromEnum(SYS.rt_sigreturn)),
        ),
    }
}

pub const VDSO = struct {
    pub const CGT_SYM = "__kernel_clock_gettime";
    pub const CGT_VER = "LINUX_2.6.39";
};

pub const time_t = i64;



---
File: /std/os/linux/arm.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u32 {
    return asm volatile ("svc #0"
        : [ret] "={r0}" (-> u32),
        : [number] "{r7}" (@intFromEnum(number)),
        : .{ .memory = true });
}

pub fn syscall1(number: SYS, arg1: u32) u32 {
    return asm volatile ("svc #0"
        : [ret] "={r0}" (-> u32),
        : [number] "{r7}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
        : .{ .memory = true });
}

pub fn syscall2(number: SYS, arg1: u32, arg2: u32) u32 {
    return asm volatile ("svc #0"
        : [ret] "={r0}" (-> u32),
        : [number] "{r7}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
        : .{ .memory = true });
}

pub fn syscall3(number: SYS, arg1: u32, arg2: u32, arg3: u32) u32 {
    return asm volatile ("svc #0"
        : [ret] "={r0}" (-> u32),
        : [number] "{r7}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
          [arg3] "{r2}" (arg3),
        : .{ .memory = true });
}

pub fn syscall4(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32) u32 {
    return asm volatile ("svc #0"
        : [ret] "={r0}" (-> u32),
        : [number] "{r7}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
          [arg3] "{r2}" (arg3),
          [arg4] "{r3}" (arg4),
        : .{ .memory = true });
}

pub fn syscall5(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32, arg5: u32) u32 {
    return asm volatile ("svc #0"
        : [ret] "={r0}" (-> u32),
        : [number] "{r7}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
          [arg3] "{r2}" (arg3),
          [arg4] "{r3}" (arg4),
          [arg5] "{r4}" (arg5),
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
    return asm volatile ("svc #0"
        : [ret] "={r0}" (-> u32),
        : [number] "{r7}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
          [arg3] "{r2}" (arg3),
          [arg4] "{r3}" (arg4),
          [arg5] "{r4}" (arg5),
          [arg6] "{r5}" (arg6),
        : .{ .memory = true });
}

pub fn clone() callconv(.naked) u32 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         r0,   r1,    r2,    r3,  +0,   +4,  +8
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         r7         r0,    r1,    r2,   r3,  r4
    asm volatile (
        \\    stmfd sp!,{r4,r5,r6,r7}
        \\    mov r7,#120 // SYS_clone
        \\    mov r6,r3
        \\    mov r5,r0
        \\    mov r0,r2
        \\    and r1,r1,#-16
        \\    ldr r2,[sp,#16]
        \\    ldr r3,[sp,#20]
        \\    ldr r4,[sp,#24]
        \\    svc 0
        \\    tst r0,r0
        \\    beq 1f
        \\    ldmfd sp!,{r4,r5,r6,r7}
        \\    bx lr
        \\
        \\    // https://github.com/llvm/llvm-project/issues/115891
        \\1:  mov r7, #0
        \\    mov r11, #0
        \\    mov lr, #0
        \\
        \\    mov r0,r6
        \\    bl 3f
        \\    mov r7,#1 // SYS_exit
        \\    svc 0
        \\
        \\3:  bx r5
    );
}

pub fn restore() callconv(.naked) noreturn {
    switch (builtin.zig_backend) {
        .stage2_c => asm volatile (
            \\ mov r7, %[number]
            \\ svc #0
            :
            : [number] "I" (@intFromEnum(SYS.sigreturn)),
        ),
        else => asm volatile (
            \\ svc #0
            :
            : [number] "{r7}" (@intFromEnum(SYS.sigreturn)),
        ),
    }
}

pub fn restore_rt() callconv(.naked) noreturn {
    switch (builtin.zig_backend) {
        .stage2_c => asm volatile (
            \\ mov r7, %[number]
            \\ svc #0
            :
            : [number] "I" (@intFromEnum(SYS.rt_sigreturn)),
        ),
        else => asm volatile (
            \\ svc #0
            :
            : [number] "{r7}" (@intFromEnum(SYS.rt_sigreturn)),
        ),
    }
}

pub const VDSO = struct {
    pub const CGT_SYM = "__vdso_clock_gettime";
    pub const CGT_VER = "LINUX_2.6";
};

pub const HWCAP = struct {
    pub const SWP = 1 << 0;
    pub const HALF = 1 << 1;
    pub const THUMB = 1 << 2;
    pub const @"26BIT" = 1 << 3;
    pub const FAST_MULT = 1 << 4;
    pub const FPA = 1 << 5;
    pub const VFP = 1 << 6;
    pub const EDSP = 1 << 7;
    pub const JAVA = 1 << 8;
    pub const IWMMXT = 1 << 9;
    pub const CRUNCH = 1 << 10;
    pub const THUMBEE = 1 << 11;
    pub const NEON = 1 << 12;
    pub const VFPv3 = 1 << 13;
    pub const VFPv3D16 = 1 << 14;
    pub const TLS = 1 << 15;
    pub const VFPv4 = 1 << 16;
    pub const IDIVA = 1 << 17;
    pub const IDIVT = 1 << 18;
    pub const VFPD32 = 1 << 19;
    pub const IDIV = IDIVA | IDIVT;
    pub const LPAE = 1 << 20;
    pub const EVTSTRM = 1 << 21;
};

pub const time_t = i32;



---
File: /std/os/linux/bpf.zig
---

const std = @import("../../std.zig");
const errno = linux.errno;
const unexpectedErrno = std.posix.unexpectedErrno;
const expectEqual = std.testing.expectEqual;
const expectError = std.testing.expectError;
const expect = std.testing.expect;

const linux = std.os.linux;
const fd_t = linux.fd_t;
const pid_t = linux.pid_t;

pub const btf = @import("bpf/btf.zig");
pub const kern = @import("bpf/kern.zig");

// instruction classes
pub const LD = 0x00;
pub const LDX = 0x01;
pub const ST = 0x02;
pub const STX = 0x03;
pub const ALU = 0x04;
pub const JMP = 0x05;
pub const RET = 0x06;
pub const MISC = 0x07;

/// 32-bit
pub const W = 0x00;
/// 16-bit
pub const H = 0x08;
/// 8-bit
pub const B = 0x10;
/// 64-bit
pub const DW = 0x18;

pub const IMM = 0x00;
pub const ABS = 0x20;
pub const IND = 0x40;
pub const MEM = 0x60;
pub const LEN = 0x80;
pub const MSH = 0xa0;

// alu fields
pub const ADD = 0x00;
pub const SUB = 0x10;
pub const MUL = 0x20;
pub const DIV = 0x30;
pub const OR = 0x40;
pub const AND = 0x50;
pub const LSH = 0x60;
pub const RSH = 0x70;
pub const NEG = 0x80;
pub const MOD = 0x90;
pub const XOR = 0xa0;

// jmp fields
pub const JA = 0x00;
pub const JEQ = 0x10;
pub const JGT = 0x20;
pub const JGE = 0x30;
pub const JSET = 0x40;

//#define BPF_SRC(code)   ((code) & 0x08)
pub const K = 0x00;
pub const X = 0x08;

pub const MAXINSNS = 4096;

// instruction classes
/// jmp mode in word width
pub const JMP32 = 0x06;

/// alu mode in double word width
pub const ALU64 = 0x07;

// ld/ldx fields
/// exclusive add
pub const XADD = 0xc0;

// alu/jmp fields
/// mov reg to reg
pub const MOV = 0xb0;

/// sign extending arithmetic shift right */
pub const ARSH = 0xc0;

// change endianness of a register
/// flags for endianness conversion:
pub const END = 0xd0;

/// convert to little-endian */
pub const TO_LE = 0x00;

/// convert to big-endian
pub const TO_BE = 0x08;
pub const FROM_LE = TO_LE;
pub const FROM_BE = TO_BE;

// jmp encodings
/// jump != *
pub const JNE = 0x50;

/// LT is unsigned, '<'
pub const JLT = 0xa0;

/// LE is unsigned, '<=' *
pub const JLE = 0xb0;

/// SGT is signed '>', GT in x86
pub const JSGT = 0x60;

/// SGE is signed '>=', GE in x86
pub const JSGE = 0x70;

/// SLT is signed, '<'
pub const JSLT = 0xc0;

/// SLE is signed, '<='
pub const JSLE = 0xd0;

/// function call
pub const CALL = 0x80;

/// function return
pub const EXIT = 0x90;

/// Flag for prog_attach command. If a sub-cgroup installs some bpf program, the
/// program in this cgroup yields to sub-cgroup program.
pub const F_ALLOW_OVERRIDE = 0x1;

/// Flag for prog_attach command. If a sub-cgroup installs some bpf program,
/// that cgroup program gets run in addition to the program in this cgroup.
pub const F_ALLOW_MULTI = 0x2;

/// Flag for prog_attach command.
pub const F_REPLACE = 0x4;

/// If BPF_F_STRICT_ALIGNMENT is used in BPF_PROG_LOAD command, the verifier
/// will perform strict alignment checking as if the kernel has been built with
/// CONFIG_EFFICIENT_UNALIGNED_ACCESS not set, and NET_IP_ALIGN defined to 2.
pub const F_STRICT_ALIGNMENT = 0x1;

/// If BPF_F_ANY_ALIGNMENT is used in BPF_PROF_LOAD command, the verifier will
/// allow any alignment whatsoever. On platforms with strict alignment
/// requirements for loads and stores (such as sparc and mips) the verifier
/// validates that all loads and stores provably follow this requirement. This
/// flag turns that checking and enforcement off.
///
/// It is mostly used for testing when we want to validate the context and
/// memory access aspects of the verifier, but because of an unaligned access
/// the alignment check would trigger before the one we are interested in.
pub const F_ANY_ALIGNMENT = 0x2;

/// BPF_F_TEST_RND_HI32 is used in BPF_PROG_LOAD command for testing purpose.
/// Verifier does sub-register def/use analysis and identifies instructions
/// whose def only matters for low 32-bit, high 32-bit is never referenced later
/// through implicit zero extension. Therefore verifier notifies JIT back-ends
/// that it is safe to ignore clearing high 32-bit for these instructions. This
/// saves some back-ends a lot of code-gen. However such optimization is not
/// necessary on some arches, for example x86_64, arm64 etc, whose JIT back-ends
/// hence hasn't used verifier's analysis result. But, we really want to have a
/// way to be able to verify the correctness of the described optimization on
/// x86_64 on which testsuites are frequently exercised.
///
/// So, this flag is introduced. Once it is set, verifier will randomize high
/// 32-bit for those instructions who has been identified as safe to ignore
/// them. Then, if verifier is not doing correct analysis, such randomization
/// will regress tests to expose bugs.
pub const F_TEST_RND_HI32 = 0x4;

/// If BPF_F_SLEEPABLE is used in BPF_PROG_LOAD command, the verifier will
/// restrict map and helper usage for such programs. Sleepable BPF programs can
/// only be attached to hooks where kernel execution context allows sleeping.
/// Such programs are allowed to use helpers that may sleep like
/// bpf_copy_from_user().
pub const F_SLEEPABLE = 0x10;

/// When BPF ldimm64's insn[0].src_reg != 0 then this can have two extensions:
/// insn[0].src_reg:  BPF_PSEUDO_MAP_FD   BPF_PSEUDO_MAP_VALUE
/// insn[0].imm:      map fd              map fd
/// insn[1].imm:      0                   offset into value
/// insn[0].off:      0                   0
/// insn[1].off:      0                   0
/// ldimm64 rewrite:  address of map      address of map[0]+offset
/// verifier type:    CONST_PTR_TO_MAP    PTR_TO_MAP_VALUE
pub const PSEUDO_MAP_FD = 1;
pub const PSEUDO_MAP_VALUE = 2;

/// when bpf_call->src_reg == BPF_PSEUDO_CALL, bpf_call->imm == pc-relative
/// offset to another bpf function
pub const PSEUDO_CALL = 1;

/// flag for BPF_MAP_UPDATE_ELEM command. create new element or update existing
pub const ANY = 0;

/// flag for BPF_MAP_UPDATE_ELEM command. create new element if it didn't exist
pub const NOEXIST = 1;

/// flag for BPF_MAP_UPDATE_ELEM command. update existing element
pub const EXIST = 2;

/// flag for BPF_MAP_UPDATE_ELEM command. spin_lock-ed map_lookup/map_update
pub const F_LOCK = 4;

/// flag for BPF_MAP_CREATE command */
pub const BPF_F_NO_PREALLOC = 0x1;

/// flag for BPF_MAP_CREATE command. Instead of having one common LRU list in
/// the BPF_MAP_TYPE_LRU_[PERCPU_]HASH map, use a percpu LRU list which can
/// scale and perform better. Note, the LRU nodes (including free nodes) cannot
/// be moved across different LRU lists.
pub const BPF_F_NO_COMMON_LRU = 0x2;

/// flag for BPF_MAP_CREATE command. Specify numa node during map creation
pub const BPF_F_NUMA_NODE = 0x4;

/// flag for BPF_MAP_CREATE command. Flags for BPF object read access from
/// syscall side
pub const BPF_F_RDONLY = 0x8;

/// flag for BPF_MAP_CREATE command. Flags for BPF object write access from
/// syscall side
pub const BPF_F_WRONLY = 0x10;

/// flag for BPF_MAP_CREATE command. Flag for stack_map, store build_id+offset
/// instead of pointer
pub const BPF_F_STACK_BUILD_ID = 0x20;

/// flag for BPF_MAP_CREATE command. Zero-initialize hash function seed. This
/// should only be used for testing.
pub const BPF_F_ZERO_SEED = 0x40;

/// flag for BPF_MAP_CREATE command Flags for accessing BPF object from program
/// side.
pub const BPF_F_RDONLY_PROG = 0x80;

/// flag for BPF_MAP_CREATE command. Flags for accessing BPF object from program
/// side.
pub const BPF_F_WRONLY_PROG = 0x100;

/// flag for BPF_MAP_CREATE command. Clone map from listener for newly accepted
/// socket
pub const BPF_F_CLONE = 0x200;

/// flag for BPF_MAP_CREATE command. Enable memory-mapping BPF map
pub const BPF_F_MMAPABLE = 0x400;

/// These values correspond to "syscalls" within the BPF program's environment,
/// each one is documented in std.os.linux.BPF.kern
pub const Helper = enum(i32) {
    unspec,
    map_lookup_elem,
    map_update_elem,
    map_delete_elem,
    probe_read,
    ktime_get_ns,
    trace_printk,
    get_prandom_u32,
    get_smp_processor_id,
    skb_store_bytes,
    l3_csum_replace,
    l4_csum_replace,
    tail_call,
    clone_redirect,
    get_current_pid_tgid,
    get_current_uid_gid,
    get_current_comm,
    get_cgroup_classid,
    skb_vlan_push,
    skb_vlan_pop,
    skb_get_tunnel_key,
    skb_set_tunnel_key,
    perf_event_read,
    redirect,
    get_route_realm,
    perf_event_output,
    skb_load_bytes,
    get_stackid,
    csum_diff,
    skb_get_tunnel_opt,
    skb_set_tunnel_opt,
    skb_change_proto,
    skb_change_type,
    skb_under_cgroup,
    get_hash_recalc,
    get_current_task,
    probe_write_user,
    current_task_under_cgroup,
    skb_change_tail,
    skb_pull_data,
    csum_update,
    set_hash_invalid,
    get_numa_node_id,
    skb_change_head,
    xdp_adjust_head,
    probe_read_str,
    get_socket_cookie,
    get_socket_uid,
    set_hash,
    setsockopt,
    skb_adjust_room,
    redirect_map,
    sk_redirect_map,
    sock_map_update,
    xdp_adjust_meta,
    perf_event_read_value,
    perf_prog_read_value,
    getsockopt,
    override_return,
    sock_ops_cb_flags_set,
    msg_redirect_map,
    msg_apply_bytes,
    msg_cork_bytes,
    msg_pull_data,
    bind,
    xdp_adjust_tail,
    skb_get_xfrm_state,
    get_stack,
    skb_load_bytes_relative,
    fib_lookup,
    sock_hash_update,
    msg_redirect_hash,
    sk_redirect_hash,
    lwt_push_encap,
    lwt_seg6_store_bytes,
    lwt_seg6_adjust_srh,
    lwt_seg6_action,
    rc_repeat,
    rc_keydown,
    skb_cgroup_id,
    get_current_cgroup_id,
    get_local_storage,
    sk_select_reuseport,
    skb_ancestor_cgroup_id,
    sk_lookup_tcp,
    sk_lookup_udp,
    sk_release,
    map_push_elem,
    map_pop_elem,
    map_peek_elem,
    msg_push_data,
    msg_pop_data,
    rc_pointer_rel,
    spin_lock,
    spin_unlock,
    sk_fullsock,
    tcp_sock,
    skb_ecn_set_ce,
    get_listener_sock,
    skc_lookup_tcp,
    tcp_check_syncookie,
    sysctl_get_name,
    sysctl_get_current_value,
    sysctl_get_new_value,
    sysctl_set_new_value,
    strtol,
    strtoul,
    sk_storage_get,
    sk_storage_delete,
    send_signal,
    tcp_gen_syncookie,
    skb_output,
    probe_read_user,
    probe_read_kernel,
    probe_read_user_str,
    probe_read_kernel_str,
    tcp_send_ack,
    send_signal_thread,
    jiffies64,
    read_branch_records,
    get_ns_current_pid_tgid,
    xdp_output,
    get_netns_cookie,
    get_current_ancestor_cgroup_id,
    sk_assign,
    ktime_get_boot_ns,
    seq_printf,
    seq_write,
    sk_cgroup_id,
    sk_ancestor_cgroup_id,
    ringbuf_output,
    ringbuf_reserve,
    ringbuf_submit,
    ringbuf_discard,
    ringbuf_query,
    csum_level,
    skc_to_tcp6_sock,
    skc_to_tcp_sock,
    skc_to_tcp_timewait_sock,
    skc_to_tcp_request_sock,
    skc_to_udp6_sock,
    get_task_stack,
    load_hdr_opt,
    store_hdr_opt,
    reserve_hdr_opt,
    inode_storage_get,
    inode_storage_delete,
    d_path,
    copy_from_user,
    snprintf_btf,
    seq_printf_btf,
    skb_cgroup_classid,
    redirect_neigh,
    per_cpu_ptr,
    this_cpu_ptr,
    redirect_peer,
    task_storage_get,
    task_storage_delete,
    get_current_task_btf,
    bprm_opts_set,
    ktime_get_coarse_ns,
    ima_inode_hash,
    sock_from_file,
    check_mtu,
    for_each_map_elem,
    snprintf,
    sys_bpf,
    btf_find_by_name_kind,
    sys_close,
    timer_init,
    timer_set_callback,
    timer_start,
    timer_cancel,
    get_func_ip,
    get_attach_cookie,
    task_pt_regs,
    get_branch_snapshot,
    trace_vprintk,
    skc_to_unix_sock,
    kallsyms_lookup_name,
    find_vma,
    loop,
    strncmp,
    get_func_arg,
    get_func_ret,
    get_func_arg_cnt,
    get_retval,
    set_retval,
    xdp_get_buff_len,
    xdp_load_bytes,
    xdp_store_bytes,
    copy_from_user_task,
    skb_set_tstamp,
    ima_file_hash,
    kptr_xchg,
    map_lookup_percpu_elem,
    skc_to_mptcp_sock,
    dynptr_from_mem,
    ringbuf_reserve_dynptr,
    ringbuf_submit_dynptr,
    ringbuf_discard_dynptr,
    dynptr_read,
    dynptr_write,
    dynptr_data,
    tcp_raw_gen_syncookie_ipv4,
    tcp_raw_gen_syncookie_ipv6,
    tcp_raw_check_syncookie_ipv4,
    tcp_raw_check_syncookie_ipv6,
    ktime_get_tai_ns,
    user_ringbuf_drain,
    cgrp_storage_get,
    cgrp_storage_delete,
    _,
};

// TODO: determine that this is the expected bit layout for both little and big
// endian systems
/// a single BPF instruction
pub const Insn = packed struct {
    code: u8,
    dst: u4,
    src: u4,
    off: i16,
    imm: i32,

    /// r0 - r9 are general purpose 64-bit registers, r10 points to the stack
    /// frame
    pub const Reg = enum(u4) { r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10 };
    const Source = enum(u1) { reg, imm };

    const Mode = enum(u8) {
        imm = IMM,
        abs = ABS,
        ind = IND,
        mem = MEM,
        len = LEN,
        msh = MSH,
    };

    pub const AluOp = enum(u8) {
        add = ADD,
        sub = SUB,
        mul = MUL,
        div = DIV,
        alu_or = OR,
        alu_and = AND,
        lsh = LSH,
        rsh = RSH,
        neg = NEG,
        mod = MOD,
        xor = XOR,
        mov = MOV,
        arsh = ARSH,
    };

    pub const Size = enum(u8) {
        byte = B,
        half_word = H,
        word = W,
        double_word = DW,
    };

    pub const JmpOp = enum(u8) {
        ja = JA,
        jeq = JEQ,
        jgt = JGT,
        jge = JGE,
        jset = JSET,
        jlt = JLT,
        jle = JLE,
        jne = JNE,
        jsgt = JSGT,
        jsge = JSGE,
        jslt = JSLT,
        jsle = JSLE,
    };

    const ImmOrReg = union(Source) {
        reg: Reg,
        imm: i32,
    };

    fn imm_reg(code: u8, dst: Reg, src: anytype, off: i16) Insn {
        const imm_or_reg = if (@TypeOf(src) == Reg or @typeInfo(@TypeOf(src)) == .enum_literal)
            ImmOrReg{ .reg = @as(Reg, src) }
        else
            ImmOrReg{ .imm = src };

        const src_type: u8 = switch (imm_or_reg) {
            .imm => K,
            .reg => X,
        };

        return Insn{
            .code = code | src_type,
            .dst = @intFromEnum(dst),
            .src = switch (imm_or_reg) {
                .imm => 0,
                .reg => |r| @intFromEnum(r),
            },
            .off = off,
            .imm = switch (imm_or_reg) {
                .imm => |i| i,
                .reg => 0,
            },
        };
    }

    pub fn alu(comptime width: comptime_int, op: AluOp, dst: Reg, src: anytype) Insn {
        const width_bitfield = switch (width) {
            32 => ALU,
            64 => ALU64,
            else => @compileError("width must be 32 or 64"),
        };

        return imm_reg(width_bitfield | @intFromEnum(op), dst, src, 0);
    }

    pub fn mov(dst: Reg, src: anytype) Insn {
        return alu(64, .mov, dst, src);
    }

    pub fn add(dst: Reg, src: anytype) Insn {
        return alu(64, .add, dst, src);
    }

    pub fn sub(dst: Reg, src: anytype) Insn {
        return alu(64, .sub, dst, src);
    }

    pub fn mul(dst: Reg, src: anytype) Insn {
        return alu(64, .mul, dst, src);
    }

    pub fn div(dst: Reg, src: anytype) Insn {
        return alu(64, .div, dst, src);
    }

    pub fn alu_or(dst: Reg, src: anytype) Insn {
        return alu(64, .alu_or, dst, src);
    }

    pub fn alu_and(dst: Reg, src: anytype) Insn {
        return alu(64, .alu_and, dst, src);
    }

    pub fn lsh(dst: Reg, src: anytype) Insn {
        return alu(64, .lsh, dst, src);
    }

    pub fn rsh(dst: Reg, src: anytype) Insn {
        return alu(64, .rsh, dst, src);
    }

    pub fn neg(dst: Reg) Insn {
        return alu(64, .neg, dst, 0);
    }

    pub fn mod(dst: Reg, src: anytype) Insn {
        return alu(64, .mod, dst, src);
    }

    pub fn xor(dst: Reg, src: anytype) Insn {
        return alu(64, .xor, dst, src);
    }

    pub fn arsh(dst: Reg, src: anytype) Insn {
        return alu(64, .arsh, dst, src);
    }

    pub fn jmp(op: JmpOp, dst: Reg, src: anytype, off: i16) Insn {
        return imm_reg(JMP | @intFromEnum(op), dst, src, off);
    }

    pub fn ja(off: i16) Insn {
        return jmp(.ja, .r0, 0, off);
    }

    pub fn jeq(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jeq, dst, src, off);
    }

    pub fn jgt(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jgt, dst, src, off);
    }

    pub fn jge(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jge, dst, src, off);
    }

    pub fn jlt(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jlt, dst, src, off);
    }

    pub fn jle(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jle, dst, src, off);
    }

    pub fn jset(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jset, dst, src, off);
    }

    pub fn jne(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jne, dst, src, off);
    }

    pub fn jsgt(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jsgt, dst, src, off);
    }

    pub fn jsge(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jsge, dst, src, off);
    }

    pub fn jslt(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jslt, dst, src, off);
    }

    pub fn jsle(dst: Reg, src: anytype, off: i16) Insn {
        return jmp(.jsle, dst, src, off);
    }

    pub fn xadd(dst: Reg, src: Reg) Insn {
        return Insn{
            .code = STX | XADD | DW,
            .dst = @intFromEnum(dst),
            .src = @intFromEnum(src),
            .off = 0,
            .imm = 0,
        };
    }

    fn ld(mode: Mode, size: Size, dst: Reg, src: Reg, imm: i32) Insn {
        return Insn{
            .code = @intFromEnum(mode) | @intFromEnum(size) | LD,
            .dst = @intFromEnum(dst),
            .src = @intFromEnum(src),
            .off = 0,
            .imm = imm,
        };
    }

    pub fn ld_abs(size: Size, dst: Reg, src: Reg, imm: i32) Insn {
        return ld(.abs, size, dst, src, imm);
    }

    pub fn ld_ind(size: Size, dst: Reg, src: Reg, imm: i32) Insn {
        return ld(.ind, size, dst, src, imm);
    }

    pub fn ldx(size: Size, dst: Reg, src: Reg, off: i16) Insn {
        return Insn{
            .code = MEM | @intFromEnum(size) | LDX,
            .dst = @intFromEnum(dst),
            .src = @intFromEnum(src),
            .off = off,
            .imm = 0,
        };
    }

    fn ld_imm_impl1(dst: Reg, src: Reg, imm: u64) Insn {
        return Insn{
            .code = LD | DW | IMM,
            .dst = @intFromEnum(dst),
            .src = @intFromEnum(src),
            .off = 0,
            .imm = @as(i32, @bitCast(@as(u32, @truncate(imm)))),
        };
    }

    fn ld_imm_impl2(imm: u64) Insn {
        return Insn{
            .code = 0,
            .dst = 0,
            .src = 0,
            .off = 0,
            .imm = @as(i32, @bitCast(@as(u32, @truncate(imm >> 32)))),
        };
    }

    pub fn ld_dw1(dst: Reg, imm: u64) Insn {
        return ld_imm_impl1(dst, .r0, imm);
    }

    pub fn ld_dw2(imm: u64) Insn {
        return ld_imm_impl2(imm);
    }

    pub fn ld_map_fd1(dst: Reg, map_fd: fd_t) Insn {
        return ld_imm_impl1(dst, @as(Reg, @enumFromInt(PSEUDO_MAP_FD)), @as(u64, @intCast(map_fd)));
    }

    pub fn ld_map_fd2(map_fd: fd_t) Insn {
        return ld_imm_impl2(@as(u64, @intCast(map_fd)));
    }

    pub fn st(size: Size, dst: Reg, off: i16, imm: i32) Insn {
        return Insn{
            .code = MEM | @intFromEnum(size) | ST,
            .dst = @intFromEnum(dst),
            .src = 0,
            .off = off,
            .imm = imm,
        };
    }

    pub fn stx(size: Size, dst: Reg, off: i16, src: Reg) Insn {
        return Insn{
            .code = MEM | @intFromEnum(size) | STX,
            .dst = @intFromEnum(dst),
            .src = @intFromEnum(src),
            .off = off,
            .imm = 0,
        };
    }

    fn endian_swap(endian: std.builtin.Endian, comptime size: Size, dst: Reg) Insn {
        return Insn{
            .code = switch (endian) {
                .big => 0xdc,
                .little => 0xd4,
            },
            .dst = @intFromEnum(dst),
            .src = 0,
            .off = 0,
            .imm = switch (size) {
                .byte => @compileError("can't swap a single byte"),
                .half_word => 16,
                .word => 32,
                .double_word => 64,
            },
        };
    }

    pub fn le(comptime size: Size, dst: Reg) Insn {
        return endian_swap(.little, size, dst);
    }

    pub fn be(comptime size: Size, dst: Reg) Insn {
        return endian_swap(.big, size, dst);
    }

    pub fn call(helper: Helper) Insn {
        return Insn{
            .code = JMP | CALL,
            .dst = 0,
            .src = 0,
            .off = 0,
            .imm = @intFromEnum(helper),
        };
    }

    /// exit BPF program
    pub fn exit() Insn {
        return Insn{
            .code = JMP | EXIT,
            .dst = 0,
            .src = 0,
            .off = 0,
            .imm = 0,
        };
    }
};

test "insn bitsize" {
    try expectEqual(@bitSizeOf(Insn), 64);
}

fn expect_opcode(code: u8, insn: Insn) !void {
    try expectEqual(code, insn.code);
}

// The opcodes were grabbed from https://github.com/iovisor/bpf-docs/blob/master/eBPF.md
test "opcodes" {
    // instructions that have a name that end with 1 or 2 are consecutive for
    // loading 64-bit immediates (imm is only 32 bits wide)

    // alu instructions
    try expect_opcode(0x07, Insn.add(.r1, 0));
    try expect_opcode(0x0f, Insn.add(.r1, .r2));
    try expect_opcode(0x17, Insn.sub(.r1, 0));
    try expect_opcode(0x1f, Insn.sub(.r1, .r2));
    try expect_opcode(0x27, Insn.mul(.r1, 0));
    try expect_opcode(0x2f, Insn.mul(.r1, .r2));
    try expect_opcode(0x37, Insn.div(.r1, 0));
    try expect_opcode(0x3f, Insn.div(.r1, .r2));
    try expect_opcode(0x47, Insn.alu_or(.r1, 0));
    try expect_opcode(0x4f, Insn.alu_or(.r1, .r2));
    try expect_opcode(0x57, Insn.alu_and(.r1, 0));
    try expect_opcode(0x5f, Insn.alu_and(.r1, .r2));
    try expect_opcode(0x67, Insn.lsh(.r1, 0));
    try expect_opcode(0x6f, Insn.lsh(.r1, .r2));
    try expect_opcode(0x77, Insn.rsh(.r1, 0));
    try expect_opcode(0x7f, Insn.rsh(.r1, .r2));
    try expect_opcode(0x87, Insn.neg(.r1));
    try expect_opcode(0x97, Insn.mod(.r1, 0));
    try expect_opcode(0x9f, Insn.mod(.r1, .r2));
    try expect_opcode(0xa7, Insn.xor(.r1, 0));
    try expect_opcode(0xaf, Insn.xor(.r1, .r2));
    try expect_opcode(0xb7, Insn.mov(.r1, 0));
    try expect_opcode(0xbf, Insn.mov(.r1, .r2));
    try expect_opcode(0xc7, Insn.arsh(.r1, 0));
    try expect_opcode(0xcf, Insn.arsh(.r1, .r2));

    // atomic instructions: might be more of these not documented in the wild
    try expect_opcode(0xdb, Insn.xadd(.r1, .r2));

    // TODO: byteswap instructions
    try expect_opcode(0xd4, Insn.le(.half_word, .r1));
    try expectEqual(@as(i32, @intCast(16)), Insn.le(.half_word, .r1).imm);
    try expect_opcode(0xd4, Insn.le(.word, .r1));
    try expectEqual(@as(i32, @intCast(32)), Insn.le(.word, .r1).imm);
    try expect_opcode(0xd4, Insn.le(.double_word, .r1));
    try expectEqual(@as(i32, @intCast(64)), Insn.le(.double_word, .r1).imm);
    try expect_opcode(0xdc, Insn.be(.half_word, .r1));
    try expectEqual(@as(i32, @intCast(16)), Insn.be(.half_word, .r1).imm);
    try expect_opcode(0xdc, Insn.be(.word, .r1));
    try expectEqual(@as(i32, @intCast(32)), Insn.be(.word, .r1).imm);
    try expect_opcode(0xdc, Insn.be(.double_word, .r1));
    try expectEqual(@as(i32, @intCast(64)), Insn.be(.double_word, .r1).imm);

    // memory instructions
    try expect_opcode(0x18, Insn.ld_dw1(.r1, 0));
    try expect_opcode(0x00, Insn.ld_dw2(0));

    //   loading a map fd
    try expect_opcode(0x18, Insn.ld_map_fd1(.r1, 0));
    try expectEqual(@as(u4, @intCast(PSEUDO_MAP_FD)), Insn.ld_map_fd1(.r1, 0).src);
    try expect_opcode(0x00, Insn.ld_map_fd2(0));

    try expect_opcode(0x38, Insn.ld_abs(.double_word, .r1, .r2, 0));
    try expect_opcode(0x20, Insn.ld_abs(.word, .r1, .r2, 0));
    try expect_opcode(0x28, Insn.ld_abs(.half_word, .r1, .r2, 0));
    try expect_opcode(0x30, Insn.ld_abs(.byte, .r1, .r2, 0));

    try expect_opcode(0x58, Insn.ld_ind(.double_word, .r1, .r2, 0));
    try expect_opcode(0x40, Insn.ld_ind(.word, .r1, .r2, 0));
    try expect_opcode(0x48, Insn.ld_ind(.half_word, .r1, .r2, 0));
    try expect_opcode(0x50, Insn.ld_ind(.byte, .r1, .r2, 0));

    try expect_opcode(0x79, Insn.ldx(.double_word, .r1, .r2, 0));
    try expect_opcode(0x61, Insn.ldx(.word, .r1, .r2, 0));
    try expect_opcode(0x69, Insn.ldx(.half_word, .r1, .r2, 0));
    try expect_opcode(0x71, Insn.ldx(.byte, .r1, .r2, 0));

    try expect_opcode(0x62, Insn.st(.word, .r1, 0, 0));
    try expect_opcode(0x6a, Insn.st(.half_word, .r1, 0, 0));
    try expect_opcode(0x72, Insn.st(.byte, .r1, 0, 0));

    try expect_opcode(0x63, Insn.stx(.word, .r1, 0, .r2));
    try expect_opcode(0x6b, Insn.stx(.half_word, .r1, 0, .r2));
    try expect_opcode(0x73, Insn.stx(.byte, .r1, 0, .r2));
    try expect_opcode(0x7b, Insn.stx(.double_word, .r1, 0, .r2));

    // branch instructions
    try expect_opcode(0x05, Insn.ja(0));
    try expect_opcode(0x15, Insn.jeq(.r1, 0, 0));
    try expect_opcode(0x1d, Insn.jeq(.r1, .r2, 0));
    try expect_opcode(0x25, Insn.jgt(.r1, 0, 0));
    try expect_opcode(0x2d, Insn.jgt(.r1, .r2, 0));
    try expect_opcode(0x35, Insn.jge(.r1, 0, 0));
    try expect_opcode(0x3d, Insn.jge(.r1, .r2, 0));
    try expect_opcode(0xa5, Insn.jlt(.r1, 0, 0));
    try expect_opcode(0xad, Insn.jlt(.r1, .r2, 0));
    try expect_opcode(0xb5, Insn.jle(.r1, 0, 0));
    try expect_opcode(0xbd, Insn.jle(.r1, .r2, 0));
    try expect_opcode(0x45, Insn.jset(.r1, 0, 0));
    try expect_opcode(0x4d, Insn.jset(.r1, .r2, 0));
    try expect_opcode(0x55, Insn.jne(.r1, 0, 0));
    try expect_opcode(0x5d, Insn.jne(.r1, .r2, 0));
    try expect_opcode(0x65, Insn.jsgt(.r1, 0, 0));
    try expect_opcode(0x6d, Insn.jsgt(.r1, .r2, 0));
    try expect_opcode(0x75, Insn.jsge(.r1, 0, 0));
    try expect_opcode(0x7d, Insn.jsge(.r1, .r2, 0));
    try expect_opcode(0xc5, Insn.jslt(.r1, 0, 0));
    try expect_opcode(0xcd, Insn.jslt(.r1, .r2, 0));
    try expect_opcode(0xd5, Insn.jsle(.r1, 0, 0));
    try expect_opcode(0xdd, Insn.jsle(.r1, .r2, 0));
    try expect_opcode(0x85, Insn.call(.unspec));
    try expect_opcode(0x95, Insn.exit());
}

pub const Cmd = enum(usize) {
    /// Create  a map and return a file descriptor that refers to the map. The
    /// close-on-exec file descriptor flag is automatically enabled for the new
    /// file descriptor.
    ///
    /// uses MapCreateAttr
    map_create,

    /// Look up an element by key in a specified map and return its value.
    ///
    /// uses MapElemAttr
    map_lookup_elem,

    /// Create or update an element (key/value pair) in a specified map.
    ///
    /// uses MapElemAttr
    map_update_elem,

    /// Look up and delete an element by key in a specified map.
    ///
    /// uses MapElemAttr
    map_delete_elem,

    /// Look up an element by key in a specified map and return the key of the
    /// next element.
    map_get_next_key,

    /// Verify and load an eBPF program, returning a new file descriptor
    /// associated with the program. The close-on-exec file descriptor flag
    /// is automatically enabled for the new file descriptor.
    ///
    /// uses ProgLoadAttr
    prog_load,

    /// Pin a map or eBPF program to a path within the minimal BPF filesystem
    ///
    /// uses ObjAttr
    obj_pin,

    /// Get the file descriptor of a BPF object pinned to a certain path
    ///
    /// uses ObjAttr
    obj_get,

    /// uses ProgAttachAttr
    prog_attach,

    /// uses ProgAttachAttr
    prog_detach,

    /// uses TestRunAttr
    prog_test_run,

    /// uses GetIdAttr
    prog_get_next_id,

    /// uses GetIdAttr
    map_get_next_id,

    /// uses GetIdAttr
    prog_get_fd_by_id,

    /// uses GetIdAttr
    map_get_fd_by_id,

    /// uses InfoAttr
    obj_get_info_by_fd,

    /// uses QueryAttr
    prog_query,

    /// uses RawTracepointAttr
    raw_tracepoint_open,

    /// uses BtfLoadAttr
    btf_load,

    /// uses GetIdAttr
    btf_get_fd_by_id,

    /// uses TaskFdQueryAttr
    task_fd_query,

    /// uses MapElemAttr
    map_lookup_and_delete_elem,
    map_freeze,

    /// uses GetIdAttr
    btf_get_next_id,

    /// uses MapBatchAttr
    map_lookup_batch,

    /// uses MapBatchAttr
    map_lookup_and_delete_batch,

    /// uses MapBatchAttr
    map_update_batch,

    /// uses MapBatchAttr
    map_delete_batch,

    /// uses LinkCreateAttr
    link_create,

    /// uses LinkUpdateAttr
    link_update,

    /// uses GetIdAttr
    link_get_fd_by_id,

    /// uses GetIdAttr
    link_get_next_id,

    /// uses EnableStatsAttr
    enable_stats,

    /// uses IterCreateAttr
    iter_create,
    link_detach,
    _,
};

pub const MapType = enum(u32) {
    unspec,
    hash,
    array,
    prog_array,
    perf_event_array,
    percpu_hash,
    percpu_array,
    stack_trace,
    cgroup_array,
    lru_hash,
    lru_percpu_hash,
    lpm_trie,
    array_of_maps,
    hash_of_maps,
    devmap,
    sockmap,
    cpumap,
    xskmap,
    sockhash,
    cgroup_storage_deprecated,
    reuseport_sockarray,
    percpu_cgroup_storage,
    queue,
    stack,
    sk_storage,
    devmap_hash,
    struct_ops,

    /// An ordered and shared CPU version of perf_event_array. They have
    /// similar semantics:
    ///     - variable length records
    ///     - no blocking: when full, reservation fails
    ///     - memory mappable for ease and speed
    ///     - epoll notifications for new data, but can busy poll
    ///
    /// Ringbufs give BPF programs two sets of APIs:
    ///     - ringbuf_output() allows copy data from one place to a ring
    ///     buffer, similar to bpf_perf_event_output()
    ///     - ringbuf_reserve()/ringbuf_commit()/ringbuf_discard() split the
    ///     process into two steps. First a fixed amount of space is reserved,
    ///     if that is successful then the program gets a pointer to a chunk of
    ///     memory and can be submitted with commit() or discarded with
    ///     discard()
    ///
    /// ringbuf_output() will incur an extra memory copy, but allows to submit
    /// records of the length that's not known beforehand, and is an easy
    /// replacement for perf_event_output().
    ///
    /// ringbuf_reserve() avoids the extra memory copy but requires a known size
    /// of memory beforehand.
    ///
    /// ringbuf_query() allows to query properties of the map, 4 are currently
    /// supported:
    ///     - BPF_RB_AVAIL_DATA: amount of unconsumed data in ringbuf
    ///     - BPF_RB_RING_SIZE: returns size of ringbuf
    ///     - BPF_RB_CONS_POS/BPF_RB_PROD_POS returns current logical position
    ///     of consumer and producer respectively
    ///
    /// key size: 0
    /// value size: 0
    /// max entries: size of ringbuf, must be power of 2
    ringbuf,
    inode_storage,
    task_storage,
    bloom_filter,
    user_ringbuf,
    cgroup_storage,
    arena,

    _,
};

pub const ProgType = enum(u32) {
    unspec,

    /// context type: __sk_buff
    socket_filter,

    /// context type: bpf_user_pt_regs_t
    kprobe,

    /// context type: __sk_buff
    sched_cls,

    /// context type: __sk_buff
    sched_act,

    /// context type: u64
    tracepoint,

    /// context type: xdp_md
    xdp,

    /// context type: bpf_perf_event_data
    perf_event,

    /// context type: __sk_buff
    cgroup_skb,

    /// context type: bpf_sock
    cgroup_sock,

    /// context type: __sk_buff
    lwt_in,

    /// context type: __sk_buff
    lwt_out,

    /// context type: __sk_buff
    lwt_xmit,

    /// context type: bpf_sock_ops
    sock_ops,

    /// context type: __sk_buff
    sk_skb,

    /// context type: bpf_cgroup_dev_ctx
    cgroup_device,

    /// context type: sk_msg_md
    sk_msg,

    /// context type: bpf_raw_tracepoint_args
    raw_tracepoint,

    /// context type: bpf_sock_addr
    cgroup_sock_addr,

    /// context type: __sk_buff
    lwt_seg6local,

    /// context type: u32
    lirc_mode2,

    /// context type: sk_reuseport_md
    sk_reuseport,

    /// context type: __sk_buff
    flow_dissector,

    /// context type: bpf_sysctl
    cgroup_sysctl,

    /// context type: bpf_raw_tracepoint_args
    raw_tracepoint_writable,

    /// context type: bpf_sockopt
    cgroup_sockopt,

    /// context type: void *
    tracing,

    /// context type: void *
    struct_ops,

    /// context type: void *
    ext,

    /// context type: void *
    lsm,

    /// context type: bpf_sk_lookup
    sk_lookup,

    /// context type: void *
    syscall,

    /// context type: bpf_nf_ctx
    netfilter,

    _,
};

pub const AttachType = enum(u32) {
    cgroup_inet_ingress,
    cgroup_inet_egress,
    cgroup_inet_sock_create,
    cgroup_sock_ops,
    sk_skb_stream_parser,
    sk_skb_stream_verdict,
    cgroup_device,
    sk_msg_verdict,
    cgroup_inet4_bind,
    cgroup_inet6_bind,
    cgroup_inet4_connect,
    cgroup_inet6_connect,
    cgroup_inet4_post_bind,
    cgroup_inet6_post_bind,
    cgroup_udp4_sendmsg,
    cgroup_udp6_sendmsg,
    lirc_mode2,
    flow_dissector,
    cgroup_sysctl,
    cgroup_udp4_recvmsg,
    cgroup_udp6_recvmsg,
    cgroup_getsockopt,
    cgroup_setsockopt,
    trace_raw_tp,
    trace_fentry,
    trace_fexit,
    modify_return,
    lsm_mac,
    trace_iter,
    cgroup_inet4_getpeername,
    cgroup_inet6_getpeername,
    cgroup_inet4_getsockname,
    cgroup_inet6_getsockname,
    xdp_devmap,
    cgroup_inet_sock_release,
    xdp_cpumap,
    sk_lookup,
    xdp,
    sk_skb_verdict,
    sk_reuseport_select,
    sk_reuseport_select_or_migrate,
    perf_event,
    trace_kprobe_multi,
    lsm_cgroup,
    struct_ops,
    netfilter,
    tcx_ingress,
    tcx_egress,
    trace_uprobe_multi,
    cgroup_unix_connect,
    cgroup_unix_sendmsg,
    cgroup_unix_recvmsg,
    cgroup_unix_getpeername,
    cgroup_unix_getsockname,
    netkit_primary,
    netkit_peer,
    trace_kprobe_session,
    _,
};

const obj_name_len = 16;
/// struct used by Cmd.map_create command
pub const MapCreateAttr = extern struct {
    /// one of MapType
    map_type: u32,

    /// size of key in bytes
    key_size: u32,

    /// size of value in bytes
    value_size: u32,

    /// max number of entries in a map
    max_entries: u32,

    /// .map_create related flags
    map_flags: u32,

    /// fd pointing to the inner map
    inner_map_fd: fd_t,

    /// numa node (effective only if MapCreateFlags.numa_node is set)
    numa_node: u32,
    map_name: [obj_name_len]u8,

    /// ifindex of netdev to create on
    map_ifindex: u32,

    /// fd pointing to a BTF type data
    btf_fd: fd_t,

    /// BTF type_id of the key
    btf_key_type_id: u32,

    /// BTF type_id of the value
    bpf_value_type_id: u32,

    /// BTF type_id of a kernel struct stored as the map value
    btf_vmlinux_value_type_id: u32,
};

/// struct used by Cmd.map_*_elem commands
pub const MapElemAttr = extern struct {
    map_fd: fd_t,
    key: u64,
    result: extern union {
        value: u64,
        next_key: u64,
    },
    flags: u64,
};

/// struct used by Cmd.map_*_batch commands
pub const MapBatchAttr = extern struct {
    /// start batch, NULL to start from beginning
    in_batch: u64,

    /// output: next start batch
    out_batch: u64,
    keys: u64,
    values: u64,

    /// input/output:
    /// input: # of key/value elements
    /// output: # of filled elements
    count: u32,
    map_fd: fd_t,
    elem_flags: u64,
    flags: u64,
};

/// struct used by Cmd.prog_load command
pub const ProgLoadAttr = extern struct {
    /// one of ProgType
    prog_type: u32,
    insn_cnt: u32,
    insns: u64,
    license: u64,

    /// verbosity level of verifier
    log_level: u32,

    /// size of user buffer
    log_size: u32,

    /// user supplied buffer
    log_buf: u64,

    /// not used
    kern_version: u32,
    prog_flags: u32,
    prog_name: [obj_name_len]u8,

    /// ifindex of netdev to prep for.
    prog_ifindex: u32,

    /// For some prog types expected attach type must be known at load time to
    /// verify attach type specific parts of prog (context accesses, allowed
    /// helpers, etc).
    expected_attach_type: u32,

    /// fd pointing to BTF type data
    prog_btf_fd: fd_t,

    /// userspace bpf_func_info size
    func_info_rec_size: u32,
    func_info: u64,

    /// number of bpf_func_info records
    func_info_cnt: u32,

    /// userspace bpf_line_info size
    line_info_rec_size: u32,
    line_info: u64,

    /// number of bpf_line_info records
    line_info_cnt: u32,

    /// in-kernel BTF type id to attach to
    attact_btf_id: u32,

    /// 0 to attach to vmlinux
    attach_prog_id: u32,
};

/// struct used by Cmd.obj_* commands
pub const ObjAttr = extern struct {
    pathname: u64,
    bpf_fd: fd_t,
    file_flags: u32,
};

/// struct used by Cmd.prog_attach/detach commands
pub const ProgAttachAttr = extern struct {
    /// container object to attach to
    target_fd: fd_t,

    /// eBPF program to attach
    attach_bpf_fd: fd_t,

    attach_type: u32,
    attach_flags: u32,

    // TODO: BPF_F_REPLACE flags
    /// previously attached eBPF program to replace if .replace is used
    replace_bpf_fd: fd_t,
};

/// struct used by Cmd.prog_test_run command
pub const TestRunAttr = extern struct {
    prog_fd: fd_t,
    retval: u32,

    /// input: len of data_in
    data_size_in: u32,

    /// input/output: len of data_out. returns ENOSPC if data_out is too small.
    data_size_out: u32,
    data_in: u64,
    data_out: u64,
    repeat: u32,
    duration: u32,

    /// input: len of ctx_in
    ctx_size_in: u32,

    /// input/output: len of ctx_out. returns ENOSPC if ctx_out is too small.
    ctx_size_out: u32,
    ctx_in: u64,
    ctx_out: u64,
};

/// struct used by Cmd.*_get_*_id commands
pub const GetIdAttr = extern struct {
    id: extern union {
        start_id: u32,
        prog_id: u32,
        map_id: u32,
        btf_id: u32,
        link_id: u32,
    },
    next_id: u32,
    open_flags: u32,
};

/// struct used by Cmd.obj_get_info_by_fd command
pub const InfoAttr = extern struct {
    bpf_fd: fd_t,
    info_len: u32,
    info: u64,
};

/// struct used by Cmd.prog_query command
pub const QueryAttr = extern struct {
    /// container object to query
    target_fd: fd_t,
    attach_type: u32,
    query_flags: u32,
    attach_flags: u32,
    prog_ids: u64,
    prog_cnt: u32,
};

/// struct used by Cmd.raw_tracepoint_open command
pub const RawTracepointAttr = extern struct {
    name: u64,
    prog_fd: fd_t,
};

/// struct used by Cmd.btf_load command
pub const BtfLoadAttr = extern struct {
    btf: u64,
    btf_log_buf: u64,
    btf_size: u32,
    btf_log_size: u32,
    btf_log_level: u32,
};

/// struct used by Cmd.task_fd_query
pub const TaskFdQueryAttr = extern struct {
    /// input: pid
    pid: pid_t,

    /// input: fd
    fd: fd_t,

    /// input: flags
    flags: u32,

    /// input/output: buf len
    buf_len: u32,

    /// input/output:
    ///     tp_name for tracepoint
    ///     symbol for kprobe
    ///     filename for uprobe
    buf: u64,

    /// output: prod_id
    prog_id: u32,

    /// output: BPF_FD_TYPE
    fd_type: u32,

    /// output: probe_offset
    probe_offset: u64,

    /// output: probe_addr
    probe_addr: u64,
};

/// struct used by Cmd.link_create command
pub const LinkCreateAttr = extern struct {
    /// eBPF program to attach
    prog_fd: fd_t,

    /// object to attach to
    target_fd: fd_t,
    attach_type: u32,

    /// extra flags
    flags: u32,
};

/// struct used by Cmd.link_update command
pub const LinkUpdateAttr = extern struct {
    link_fd: fd_t,

    /// new program to update link with
    new_prog_fd: fd_t,

    /// extra flags
    flags: u32,

    /// expected link's program fd, it is specified only if BPF_F_REPLACE is
    /// set in flags
    old_prog_fd: fd_t,
};

/// struct used by Cmd.enable_stats command
pub const EnableStatsAttr = extern struct {
    type: u32,
};

/// struct used by Cmd.iter_create command
pub const IterCreateAttr = extern struct {
    link_fd: fd_t,
    flags: u32,
};

/// Mega struct that is passed to the bpf() syscall
pub const Attr = extern union {
    map_create: MapCreateAttr,
    map_elem: MapElemAttr,
    map_batch: MapBatchAttr,
    prog_load: ProgLoadAttr,
    obj: ObjAttr,
    prog_attach: ProgAttachAttr,
    test_run: TestRunAttr,
    get_id: GetIdAttr,
    info: InfoAttr,
    query: QueryAttr,
    raw_tracepoint: RawTracepointAttr,
    btf_load: BtfLoadAttr,
    task_fd_query: TaskFdQueryAttr,
    link_create: LinkCreateAttr,
    link_update: LinkUpdateAttr,
    enable_stats: EnableStatsAttr,
    iter_create: IterCreateAttr,
};

pub const Log = struct {
    level: u32,
    buf: []u8,
};

pub fn map_create(map_type: MapType, key_size: u32, value_size: u32, max_entries: u32) !fd_t {
    var attr = Attr{
        .map_create = std.mem.zeroes(MapCreateAttr),
    };

    attr.map_create.map_type = @intFromEnum(map_type);
    attr.map_create.key_size = key_size;
    attr.map_create.value_size = value_size;
    attr.map_create.max_entries = max_entries;

    const rc = linux.bpf(.map_create, &attr, @sizeOf(MapCreateAttr));
    switch (errno(rc)) {
        .SUCCESS => return @as(fd_t, @intCast(rc)),
        .INVAL => return error.MapTypeOrAttrInvalid,
        .NOMEM => return error.SystemResources,
        .PERM => return error.PermissionDenied,
        else => |err| return unexpectedErrno(err),
    }
}

test "map_create" {
    const map = try map_create(.hash, 4, 4, 32);
    defer _ = std.os.linux.close(map);
}

pub fn map_lookup_elem(fd: fd_t, key: []const u8, value: []u8) !void {
    var attr = Attr{
        .map_elem = std.mem.zeroes(MapElemAttr),
    };

    attr.map_elem.map_fd = fd;
    attr.map_elem.key = @intFromPtr(key.ptr);
    attr.map_elem.result.value = @intFromPtr(value.ptr);

    const rc = linux.bpf(.map_lookup_elem, &attr, @sizeOf(MapElemAttr));
    switch (errno(rc)) {
        .SUCCESS => return,
        .BADF => return error.BadFd,
        .FAULT => unreachable,
        .INVAL => return error.FieldInAttrNeedsZeroing,
        .NOENT => return error.NotFound,
        .PERM => return error.PermissionDenied,
        else => |err| return unexpectedErrno(err),
    }
}

pub fn map_update_elem(fd: fd_t, key: []const u8, value: []const u8, flags: u64) !void {
    var attr = Attr{
        .map_elem = std.mem.zeroes(MapElemAttr),
    };

    attr.map_elem.map_fd = fd;
    attr.map_elem.key = @intFromPtr(key.ptr);
    attr.map_elem.result = .{ .value = @intFromPtr(value.ptr) };
    attr.map_elem.flags = flags;

    const rc = linux.bpf(.map_update_elem, &attr, @sizeOf(MapElemAttr));
    switch (errno(rc)) {
        .SUCCESS => return,
        .@"2BIG" => return error.ReachedMaxEntries,
        .BADF => return error.BadFd,
        .FAULT => unreachable,
        .INVAL => return error.FieldInAttrNeedsZeroing,
        .NOMEM => return error.SystemResources,
        .PERM => return error.PermissionDenied,
        else => |err| return unexpectedErrno(err),
    }
}

pub fn map_delete_elem(fd: fd_t, key: []const u8) !void {
    var attr = Attr{
        .map_elem = std.mem.zeroes(MapElemAttr),
    };

    attr.map_elem.map_fd = fd;
    attr.map_elem.key = @intFromPtr(key.ptr);

    const rc = linux.bpf(.map_delete_elem, &attr, @sizeOf(MapElemAttr));
    switch (errno(rc)) {
        .SUCCESS => return,
        .BADF => return error.BadFd,
        .FAULT => unreachable,
        .INVAL => return error.FieldInAttrNeedsZeroing,
        .NOENT => return error.NotFound,
        .PERM => return error.PermissionDenied,
        else => |err| return unexpectedErrno(err),
    }
}

pub fn map_get_next_key(fd: fd_t, key: []const u8, next_key: []u8) !bool {
    var attr = Attr{
        .map_elem = std.mem.zeroes(MapElemAttr),
    };

    attr.map_elem.map_fd = fd;
    attr.map_elem.key = @intFromPtr(key.ptr);
    attr.map_elem.result.next_key = @intFromPtr(next_key.ptr);

    const rc = linux.bpf(.map_get_next_key, &attr, @sizeOf(MapElemAttr));
    switch (errno(rc)) {
        .SUCCESS => return true,
        .BADF => return error.BadFd,
        .FAULT => unreachable,
        .INVAL => return error.FieldInAttrNeedsZeroing,
        .NOENT => return false,
        .PERM => return error.PermissionDenied,
        else => |err| return unexpectedErrno(err),
    }
}

test "map lookup, update, and delete" {
    const key_size = 4;
    const value_size = 4;
    const map = try map_create(.hash, key_size, value_size, 1);
    defer _ = std.os.linux.close(map);

    const key = std.mem.zeroes([key_size]u8);
    var value = std.mem.zeroes([value_size]u8);

    // fails looking up value that doesn't exist
    try expectError(error.NotFound, map_lookup_elem(map, &key, &value));

    // succeed at updating and looking up element
    try map_update_elem(map, &key, &value, 0);
    try map_lookup_elem(map, &key, &value);

    // fails inserting more than max entries
    const second_key = [key_size]u8{ 0, 0, 0, 1 };
    try expectError(error.ReachedMaxEntries, map_update_elem(map, &second_key, &value, 0));

    // succeed at iterating all keys of map
    var lookup_key = [_]u8{ 1, 0, 0, 0 };
    var next_key = [_]u8{ 2, 3, 4, 5 }; // garbage value
    const status = try map_get_next_key(map, &lookup_key, &next_key);
    try expectEqual(status, true);
    try expectEqual(next_key, key);
    lookup_key = next_key;
    const status2 = try map_get_next_key(map, &lookup_key, &next_key);
    try expectEqual(status2, false);

    // succeed at deleting an existing elem
    try map_delete_elem(map, &key);
    try expectError(error.NotFound, map_lookup_elem(map, &key, &value));

    // fail at deleting a non-existing elem
    try expectError(error.NotFound, map_delete_elem(map, &key));
}

pub fn prog_load(
    prog_type: ProgType,
    insns: []const Insn,
    log: ?*Log,
    license: []const u8,
    kern_version: u32,
    flags: u32,
) !fd_t {
    var attr = Attr{
        .prog_load = std.mem.zeroes(ProgLoadAttr),
    };

    attr.prog_load.prog_type = @intFromEnum(prog_type);
    attr.prog_load.insns = @intFromPtr(insns.ptr);
    attr.prog_load.insn_cnt = @as(u32, @intCast(insns.len));
    attr.prog_load.license = @intFromPtr(license.ptr);
    attr.prog_load.kern_version = kern_version;
    attr.prog_load.prog_flags = flags;

    if (log) |l| {
        attr.prog_load.log_buf = @intFromPtr(l.buf.ptr);
        attr.prog_load.log_size = @as(u32, @intCast(l.buf.len));
        attr.prog_load.log_level = l.level;
    }

    const rc = linux.bpf(.prog_load, &attr, @sizeOf(ProgLoadAttr));
    return switch (errno(rc)) {
        .SUCCESS => @as(fd_t, @intCast(rc)),
        .ACCES => error.UnsafeProgram,
        .FAULT => unreachable,
        .INVAL => error.InvalidProgram,
        .PERM => error.PermissionDenied,
        else => |err| unexpectedErrno(err),
    };
}

test "prog_load" {
    // this should fail because it does not set r0 before exiting
    const bad_prog = [_]Insn{
        Insn.exit(),
    };

    const good_prog = [_]Insn{
        Insn.mov(.r0, 0),
        Insn.exit(),
    };

    const prog = try prog_load(.socket_filter, &good_prog, null, "MIT", 0, 0);
    defer _ = std.os.linux.close(prog);

    try expectError(error.UnsafeProgram, prog_load(.socket_filter, &bad_prog, null, "MIT", 0, 0));
}



---
File: /std/os/linux/hexagon.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u32 {
    return asm volatile ("trap0(#1)"
        : [ret] "={r0}" (-> u32),
        : [number] "{r6}" (@intFromEnum(number)),
        : .{ .memory = true });
}

pub fn syscall1(number: SYS, arg1: u32) u32 {
    return asm volatile ("trap0(#1)"
        : [ret] "={r0}" (-> u32),
        : [number] "{r6}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
        : .{ .memory = true });
}

pub fn syscall2(number: SYS, arg1: u32, arg2: u32) u32 {
    return asm volatile ("trap0(#1)"
        : [ret] "={r0}" (-> u32),
        : [number] "{r6}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
        : .{ .memory = true });
}

pub fn syscall3(number: SYS, arg1: u32, arg2: u32, arg3: u32) u32 {
    return asm volatile ("trap0(#1)"
        : [ret] "={r0}" (-> u32),
        : [number] "{r6}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
          [arg3] "{r2}" (arg3),
        : .{ .memory = true });
}

pub fn syscall4(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32) u32 {
    return asm volatile ("trap0(#1)"
        : [ret] "={r0}" (-> u32),
        : [number] "{r6}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
          [arg3] "{r2}" (arg3),
          [arg4] "{r3}" (arg4),
        : .{ .memory = true });
}

pub fn syscall5(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32, arg5: u32) u32 {
    return asm volatile ("trap0(#1)"
        : [ret] "={r0}" (-> u32),
        : [number] "{r6}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
          [arg3] "{r2}" (arg3),
          [arg4] "{r3}" (arg4),
          [arg5] "{r4}" (arg5),
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
    return asm volatile ("trap0(#1)"
        : [ret] "={r0}" (-> u32),
        : [number] "{r6}" (@intFromEnum(number)),
          [arg1] "{r0}" (arg1),
          [arg2] "{r1}" (arg2),
          [arg3] "{r2}" (arg3),
          [arg4] "{r3}" (arg4),
          [arg5] "{r4}" (arg5),
          [arg6] "{r5}" (arg6),
        : .{ .memory = true });
}

pub fn clone() callconv(.naked) u32 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         r0,   r1,    r2,    r3,  r4,   r5,  +0
    //
    // syscall(SYS_clone, flags, stack, ptid, ctid, tls)
    //         r6         r0,    r1,    r2,   r3,   r4
    asm volatile (
        \\ allocframe(#8)
        \\
        \\ r11 = r0
        \\ r10 = r3
        \\
        \\ r6 = #220 // SYS_clone
        \\ r0 = r2
        \\ r1 = and(r1, #-8)
        \\ r2 = r4
        \\ r3 = memw(r30 + #8)
        \\ r4 = r5
        \\ trap0(#1)
        \\
        \\ p0 = cmp.eq(r0, #0)
        \\ if (!p0) dealloc_return
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\ .cfi_undefined r31
    );
    asm volatile (
        \\ r30 = #0
        \\ r31 = #0
        \\
        \\ r0 = r10
        \\ callr r11
        \\
        \\ r6 = #93 // SYS_exit
        \\ r0 = #0
        \\ trap0(#1)
    );
}

pub const time_t = i64;

pub const VDSO = void;



---
File: /std/os/linux/io_uring_sqe.zig
---

//! Contains only the definition of `io_uring_sqe`.
//! Split into its own file to compartmentalize the initialization methods.

const std = @import("../../std.zig");
const linux = std.os.linux;

pub const io_uring_sqe = extern struct {
    opcode: linux.IORING_OP,
    flags: u8,
    ioprio: u16,
    fd: i32,
    off: u64,
    addr: u64,
    len: u32,
    rw_flags: u32,
    user_data: u64,
    buf_index: u16,
    personality: u16,
    splice_fd_in: i32,
    addr3: u64,
    resv: u64,

    pub fn prep_nop(sqe: *linux.io_uring_sqe) void {
        sqe.* = .{
            .opcode = .NOP,
            .flags = 0,
            .ioprio = 0,
            .fd = 0,
            .off = 0,
            .addr = 0,
            .len = 0,
            .rw_flags = 0,
            .user_data = 0,
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
    }

    pub fn prep_fsync(sqe: *linux.io_uring_sqe, fd: linux.fd_t, flags: u32) void {
        sqe.* = .{
            .opcode = .FSYNC,
            .flags = 0,
            .ioprio = 0,
            .fd = fd,
            .off = 0,
            .addr = 0,
            .len = 0,
            .rw_flags = flags,
            .user_data = 0,
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
    }

    pub fn prep_rw(
        sqe: *linux.io_uring_sqe,
        op: linux.IORING_OP,
        fd: linux.fd_t,
        addr: u64,
        len: usize,
        offset: u64,
    ) void {
        sqe.* = .{
            .opcode = op,
            .flags = 0,
            .ioprio = 0,
            .fd = fd,
            .off = offset,
            .addr = addr,
            .len = @intCast(len),
            .rw_flags = 0,
            .user_data = 0,
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
    }

    pub fn prep_read(sqe: *linux.io_uring_sqe, fd: linux.fd_t, buffer: []u8, offset: u64) void {
        sqe.prep_rw(.READ, fd, @intFromPtr(buffer.ptr), buffer.len, offset);
    }

    pub fn prep_write(sqe: *linux.io_uring_sqe, fd: linux.fd_t, buffer: []const u8, offset: u64) void {
        sqe.prep_rw(.WRITE, fd, @intFromPtr(buffer.ptr), buffer.len, offset);
    }

    pub fn prep_splice(sqe: *linux.io_uring_sqe, fd_in: linux.fd_t, off_in: u64, fd_out: linux.fd_t, off_out: u64, len: usize) void {
        sqe.prep_rw(.SPLICE, fd_out, undefined, len, off_out);
        sqe.addr = off_in;
        sqe.splice_fd_in = fd_in;
    }

    pub fn prep_readv(
        sqe: *linux.io_uring_sqe,
        fd: linux.fd_t,
        iovecs: []const std.posix.iovec,
        offset: u64,
    ) void {
        sqe.prep_rw(.READV, fd, @intFromPtr(iovecs.ptr), iovecs.len, offset);
    }

    pub fn prep_writev(
        sqe: *linux.io_uring_sqe,
        fd: linux.fd_t,
        iovecs: []const std.posix.iovec_const,
        offset: u64,
    ) void {
        sqe.prep_rw(.WRITEV, fd, @intFromPtr(iovecs.ptr), iovecs.len, offset);
    }

    pub fn prep_read_fixed(sqe: *linux.io_uring_sqe, fd: linux.fd_t, buffer: *std.posix.iovec, offset: u64, buffer_index: u16) void {
        sqe.prep_rw(.READ_FIXED, fd, @intFromPtr(buffer.base), buffer.len, offset);
        sqe.buf_index = buffer_index;
    }

    pub fn prep_write_fixed(sqe: *linux.io_uring_sqe, fd: linux.fd_t, buffer: *std.posix.iovec, offset: u64, buffer_index: u16) void {
        sqe.prep_rw(.WRITE_FIXED, fd, @intFromPtr(buffer.base), buffer.len, offset);
        sqe.buf_index = buffer_index;
    }

    pub fn prep_accept(
        sqe: *linux.io_uring_sqe,
        fd: linux.fd_t,
        addr: ?*linux.sockaddr,
        addrlen: ?*linux.socklen_t,
        flags: u32,
    ) void {
        // `addr` holds a pointer to `sockaddr`, and `addr2` holds a pointer to socklen_t`.
        // `addr2` maps to `sqe.off` (u64) instead of `sqe.len` (which is only a u32).
        sqe.prep_rw(.ACCEPT, fd, @intFromPtr(addr), 0, @intFromPtr(addrlen));
        sqe.rw_flags = flags;
    }

    pub fn prep_accept_direct(
        sqe: *linux.io_uring_sqe,
        fd: linux.fd_t,
        addr: ?*linux.sockaddr,
        addrlen: ?*linux.socklen_t,
        flags: u32,
        file_index: u32,
    ) void {
        prep_accept(sqe, fd, addr, addrlen, flags);
        __io_uring_set_target_fixed_file(sqe, file_index);
    }

    pub fn prep_multishot_accept_direct(
        sqe: *linux.io_uring_sqe,
        fd: linux.fd_t,
        addr: ?*linux.sockaddr,
        addrlen: ?*linux.socklen_t,
        flags: u32,
    ) void {
        prep_multishot_accept(sqe, fd, addr, addrlen, flags);
        __io_uring_set_target_fixed_file(sqe, linux.IORING_FILE_INDEX_ALLOC);
    }

    fn __io_uring_set_target_fixed_file(sqe: *linux.io_uring_sqe, file_index: u32) void {
        const sqe_file_index: u32 = if (file_index == linux.IORING_FILE_INDEX_ALLOC)
            linux.IORING_FILE_INDEX_ALLOC
        else
            // 0 means no fixed files, indexes should be encoded as "index + 1"
            file_index + 1;
        // This filed is overloaded in liburing:
        //   splice_fd_in: i32
        //   sqe_file_index: u32
        sqe.splice_fd_in = @bitCast(sqe_file_index);
    }

    pub fn prep_connect(

```
