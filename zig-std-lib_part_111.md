```
 pub fn toConst(self: Index, builder: *const Builder) Constant {
            return self.ptrConst(builder).global.toConst();
        }

        pub fn toValue(self: Index, builder: *const Builder) Value {
            return self.toConst(builder).toValue();
        }

        pub fn getAliasee(self: Index, builder: *const Builder) Global.Index {
            const aliasee = self.ptrConst(builder).aliasee.getBase(builder);
            assert(aliasee != .none);
            return aliasee;
        }

        pub fn setAliasee(self: Index, aliasee: Constant, builder: *Builder) void {
            self.ptr(builder).aliasee = aliasee;
        }
    };
};

pub const Variable = struct {
    global: Global.Index,
    thread_local: ThreadLocal = .default,
    mutability: Mutability = .global,
    init: Constant = .no_init,
    section: String = .none,
    alignment: Alignment = .default,

    pub const Index = enum(u32) {
        none = maxInt(u32),
        _,

        pub fn ptr(self: Index, builder: *Builder) *Variable {
            return &builder.variables.items[@intFromEnum(self)];
        }

        pub fn ptrConst(self: Index, builder: *const Builder) *const Variable {
            return &builder.variables.items[@intFromEnum(self)];
        }

        pub fn name(self: Index, builder: *const Builder) StrtabString {
            return self.ptrConst(builder).global.name(builder);
        }

        pub fn rename(self: Index, new_name: StrtabString, builder: *Builder) Allocator.Error!void {
            return self.ptrConst(builder).global.rename(new_name, builder);
        }

        pub fn typeOf(self: Index, builder: *const Builder) Type {
            return self.ptrConst(builder).global.typeOf(builder);
        }

        pub fn toConst(self: Index, builder: *const Builder) Constant {
            return self.ptrConst(builder).global.toConst();
        }

        pub fn toValue(self: Index, builder: *const Builder) Value {
            return self.toConst(builder).toValue();
        }

        pub fn setThreadLocal(self: Index, thread_local: ThreadLocal, builder: *Builder) void {
            self.ptr(builder).thread_local = thread_local;
        }

        pub fn setMutability(self: Index, mutability: Mutability, builder: *Builder) void {
            self.ptr(builder).mutability = mutability;
        }

        pub fn setInitializer(
            self: Index,
            initializer: Constant,
            builder: *Builder,
        ) Allocator.Error!void {
            if (initializer != .no_init) {
                const variable = self.ptrConst(builder);
                const global = variable.global.ptr(builder);
                const initializer_type = initializer.typeOf(builder);
                global.type = initializer_type;
            }
            self.ptr(builder).init = initializer;
        }

        pub fn setSection(self: Index, section: String, builder: *Builder) void {
            self.ptr(builder).section = section;
        }

        pub fn setAlignment(self: Index, alignment: Alignment, builder: *Builder) void {
            self.ptr(builder).alignment = alignment;
        }

        pub fn getAlignment(self: Index, builder: *Builder) Alignment {
            return self.ptr(builder).alignment;
        }

        pub fn setGlobalVariableExpression(self: Index, expression: Metadata, builder: *Builder) void {
            self.ptrConst(builder).global.setDebugMetadata(expression, builder);
        }

        pub fn getGlobalVariableExpression(self: Index, builder: *Builder) Metadata.Optional {
            return self.ptrConst(builder).global.getDebugMetadata(builder);
        }
    };
};

pub const Intrinsic = enum {
    // Variable Argument Handling
    va_start,
    va_end,
    va_copy,

    // Code Generator
    returnaddress,
    addressofreturnaddress,
    sponentry,
    frameaddress,
    prefetch,
    @"thread.pointer",

    // Standard C/C++ Library
    abs,
    smax,
    smin,
    umax,
    umin,
    memcpy,
    @"memcpy.inline",
    memmove,
    memset,
    @"memset.inline",
    sqrt,
    powi,
    sin,
    cos,
    pow,
    exp,
    exp10,
    exp2,
    ldexp,
    frexp,
    log,
    log10,
    log2,
    fma,
    fabs,
    minnum,
    maxnum,
    minimum,
    maximum,
    copysign,
    floor,
    ceil,
    trunc,
    rint,
    nearbyint,
    round,
    roundeven,
    lround,
    llround,
    lrint,
    llrint,

    // Bit Manipulation
    bitreverse,
    bswap,
    ctpop,
    ctlz,
    cttz,
    fshl,
    fshr,

    // Arithmetic with Overflow
    @"sadd.with.overflow",
    @"uadd.with.overflow",
    @"ssub.with.overflow",
    @"usub.with.overflow",
    @"smul.with.overflow",
    @"umul.with.overflow",

    // Saturation Arithmetic
    @"sadd.sat",
    @"uadd.sat",
    @"ssub.sat",
    @"usub.sat",
    @"sshl.sat",
    @"ushl.sat",

    // Fixed Point Arithmetic
    @"smul.fix",
    @"umul.fix",
    @"smul.fix.sat",
    @"umul.fix.sat",
    @"sdiv.fix",
    @"udiv.fix",
    @"sdiv.fix.sat",
    @"udiv.fix.sat",

    // Specialised Arithmetic
    canonicalize,
    fmuladd,

    // Vector Reduction
    @"vector.reduce.add",
    @"vector.reduce.fadd",
    @"vector.reduce.mul",
    @"vector.reduce.fmul",
    @"vector.reduce.and",
    @"vector.reduce.or",
    @"vector.reduce.xor",
    @"vector.reduce.smax",
    @"vector.reduce.smin",
    @"vector.reduce.umax",
    @"vector.reduce.umin",
    @"vector.reduce.fmax",
    @"vector.reduce.fmin",
    @"vector.reduce.fmaximum",
    @"vector.reduce.fminimum",
    @"vector.insert",
    @"vector.extract",

    // Floating-Point Test
    @"is.fpclass",

    // General
    @"var.annotation",
    @"ptr.annotation",
    annotation,
    @"codeview.annotation",
    trap,
    debugtrap,
    ubsantrap,
    stackprotector,
    stackguard,
    objectsize,
    expect,
    @"expect.with.probability",
    assume,
    @"ssa.copy",
    @"type.test",
    @"type.checked.load",
    @"type.checked.load.relative",
    @"arithmetic.fence",
    donothing,
    @"load.relative",
    sideeffect,
    @"is.constant",
    ptrmask,
    @"threadlocal.address",
    vscale,

    // Debug
    @"dbg.declare",
    @"dbg.value",

    // AMDGPU
    @"amdgcn.workitem.id.x",
    @"amdgcn.workitem.id.y",
    @"amdgcn.workitem.id.z",
    @"amdgcn.workgroup.id.x",
    @"amdgcn.workgroup.id.y",
    @"amdgcn.workgroup.id.z",
    @"amdgcn.dispatch.ptr",

    // NVPTX
    @"nvvm.read.ptx.sreg.tid.x",
    @"nvvm.read.ptx.sreg.tid.y",
    @"nvvm.read.ptx.sreg.tid.z",
    @"nvvm.read.ptx.sreg.ntid.x",
    @"nvvm.read.ptx.sreg.ntid.y",
    @"nvvm.read.ptx.sreg.ntid.z",
    @"nvvm.read.ptx.sreg.ctaid.x",
    @"nvvm.read.ptx.sreg.ctaid.y",
    @"nvvm.read.ptx.sreg.ctaid.z",

    // WebAssembly
    @"wasm.memory.size",
    @"wasm.memory.grow",

    const Signature = struct {
        ret_len: u8,
        params: []const Parameter,
        attrs: []const Attribute = &.{},

        const Parameter = struct {
            kind: Kind,
            attrs: []const Attribute = &.{},

            const Kind = union(enum) {
                type: Type,
                overloaded,
                matches: u8,
                matches_scalar: u8,
                matches_changed_scalar: struct {
                    index: u8,
                    scalar: Type,
                },
            };
        };
    };

    const signatures = std.enums.EnumArray(Intrinsic, Signature).init(.{
        .va_start = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn },
        },
        .va_end = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn },
        },
        .va_copy = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn },
        },

        .returnaddress = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .ptr } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .addressofreturnaddress = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .sponentry = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .frameaddress = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .prefetch = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded, .attrs = &.{ .nocapture, .readonly } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.readwrite) } },
        },
        .@"thread.pointer" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .ptr } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .abs = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .smax = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .smin = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .umax = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .umin = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .memcpy = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded, .attrs = &.{ .@"noalias", .nocapture, .writeonly } },
                .{ .kind = .overloaded, .attrs = &.{ .@"noalias", .nocapture, .readonly } },
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nounwind, .willreturn, .{ .memory = .{ .argmem = .readwrite } } },
        },
        .@"memcpy.inline" = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded, .attrs = &.{ .@"noalias", .nocapture, .writeonly } },
                .{ .kind = .overloaded, .attrs = &.{ .@"noalias", .nocapture, .readonly } },
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nounwind, .willreturn, .{ .memory = .{ .argmem = .readwrite } } },
        },
        .memmove = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded, .attrs = &.{ .nocapture, .writeonly } },
                .{ .kind = .overloaded, .attrs = &.{ .nocapture, .readonly } },
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nounwind, .willreturn, .{ .memory = .{ .argmem = .readwrite } } },
        },
        .memset = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded, .attrs = &.{ .nocapture, .writeonly } },
                .{ .kind = .{ .type = .i8 } },
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nounwind, .willreturn, .{ .memory = .{ .argmem = .write } } },
        },
        .@"memset.inline" = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded, .attrs = &.{ .nocapture, .writeonly } },
                .{ .kind = .{ .type = .i8 } },
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nounwind, .willreturn, .{ .memory = .{ .argmem = .write } } },
        },
        .sqrt = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .powi = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .sin = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .cos = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .pow = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .exp = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .exp2 = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .exp10 = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .ldexp = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .frexp = .{
            .ret_len = 2,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .log = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .log10 = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .log2 = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .fma = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .fabs = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .minnum = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .maxnum = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .minimum = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .maximum = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .copysign = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .floor = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .ceil = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .trunc = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .rint = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .nearbyint = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .round = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .roundeven = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .lround = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .llround = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .lrint = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .llrint = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .bitreverse = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .bswap = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .ctpop = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .ctlz = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .cttz = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .fshl = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .fshr = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .@"sadd.with.overflow" = .{
            .ret_len = 2,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches_changed_scalar = .{ .index = 0, .scalar = .i1 } } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"uadd.with.overflow" = .{
            .ret_len = 2,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches_changed_scalar = .{ .index = 0, .scalar = .i1 } } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"ssub.with.overflow" = .{
            .ret_len = 2,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches_changed_scalar = .{ .index = 0, .scalar = .i1 } } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"usub.with.overflow" = .{
            .ret_len = 2,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches_changed_scalar = .{ .index = 0, .scalar = .i1 } } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"smul.with.overflow" = .{
            .ret_len = 2,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches_changed_scalar = .{ .index = 0, .scalar = .i1 } } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"umul.with.overflow" = .{
            .ret_len = 2,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches_changed_scalar = .{ .index = 0, .scalar = .i1 } } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .@"sadd.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"uadd.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"ssub.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"usub.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"sshl.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"ushl.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .@"smul.fix" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"umul.fix" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"smul.fix.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"umul.fix.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"sdiv.fix" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"udiv.fix" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"sdiv.fix.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"udiv.fix.sat" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .canonicalize = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .fmuladd = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .@"vector.reduce.add" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.fadd" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 2 } },
                .{ .kind = .{ .matches_scalar = 2 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.mul" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.fmul" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 2 } },
                .{ .kind = .{ .matches_scalar = 2 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.and" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.or" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.xor" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.smax" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.smin" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.umax" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.umin" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.fmax" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.fmin" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.fmaximum" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.reduce.fminimum" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_scalar = 1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.insert" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i64 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"vector.extract" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i64 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .@"is.fpclass" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .matches_changed_scalar = .{ .index = 1, .scalar = .i1 } } },
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i32 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .@"var.annotation" = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 1 } },
                .{ .kind = .{ .type = .i32 } },
                .{ .kind = .{ .matches = 1 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = .{ .inaccessiblemem = .readwrite } } },
        },
        .@"ptr.annotation" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 2 } },
                .{ .kind = .{ .type = .i32 } },
                .{ .kind = .{ .matches = 2 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = .{ .inaccessiblemem = .readwrite } } },
        },
        .annotation = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 2 } },
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = .{ .inaccessiblemem = .readwrite } } },
        },
        .@"codeview.annotation" = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .{ .type = .metadata } },
            },
            .attrs = &.{ .nocallback, .noduplicate, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = .{ .inaccessiblemem = .readwrite } } },
        },
        .trap = .{
            .ret_len = 0,
            .params = &.{},
            .attrs = &.{ .cold, .noreturn, .nounwind, .{ .memory = .{ .inaccessiblemem = .write } } },
        },
        .debugtrap = .{
            .ret_len = 0,
            .params = &.{},
            .attrs = &.{.nounwind},
        },
        .ubsantrap = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .{ .type = .i8 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .cold, .noreturn, .nounwind },
        },
        .stackprotector = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .{ .type = .ptr } },
                .{ .kind = .{ .type = .ptr } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn },
        },
        .stackguard = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .ptr } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn },
        },
        .objectsize = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .expect = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"expect.with.probability" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .{ .type = .double }, .attrs = &.{.immarg} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .assume = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .{ .type = .i1 }, .attrs = &.{.noundef} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = .{ .inaccessiblemem = .write } } },
        },
        .@"ssa.copy" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 }, .attrs = &.{.returned} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"type.test" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i1 } },
                .{ .kind = .{ .type = .ptr } },
                .{ .kind = .{ .type = .metadata } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"type.checked.load" = .{
            .ret_len = 2,
            .params = &.{
                .{ .kind = .{ .type = .ptr } },
                .{ .kind = .{ .type = .i1 } },
                .{ .kind = .{ .type = .ptr } },
                .{ .kind = .{ .type = .i32 } },
                .{ .kind = .{ .type = .metadata } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"type.checked.load.relative" = .{
            .ret_len = 2,
            .params = &.{
                .{ .kind = .{ .type = .ptr } },
                .{ .kind = .{ .type = .i1 } },
                .{ .kind = .{ .type = .ptr } },
                .{ .kind = .{ .type = .i32 } },
                .{ .kind = .{ .type = .metadata } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"arithmetic.fence" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .donothing = .{
            .ret_len = 0,
            .params = &.{},
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"load.relative" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .ptr } },
                .{ .kind = .{ .type = .ptr } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = .{ .argmem = .read } } },
        },
        .sideeffect = .{
            .ret_len = 0,
            .params = &.{},
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = .{ .inaccessiblemem = .readwrite } } },
        },
        .@"is.constant" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i1 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .convergent, .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .ptrmask = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .matches = 0 } },
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"threadlocal.address" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded, .attrs = &.{.nonnull} },
                .{ .kind = .{ .matches = 0 }, .attrs = &.{.nonnull} },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .vscale = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .@"dbg.declare" = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .{ .type = .metadata } },
                .{ .kind = .{ .type = .metadata } },
                .{ .kind = .{ .type = .metadata } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"dbg.value" = .{
            .ret_len = 0,
            .params = &.{
                .{ .kind = .{ .type = .metadata } },
                .{ .kind = .{ .type = .metadata } },
                .{ .kind = .{ .type = .metadata } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .@"amdgcn.workitem.id.x" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"amdgcn.workitem.id.y" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"amdgcn.workitem.id.z" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"amdgcn.workgroup.id.x" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"amdgcn.workgroup.id.y" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"amdgcn.workgroup.id.z" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"amdgcn.dispatch.ptr" = .{
            .ret_len = 1,
            .params = &.{
                .{
                    .kind = .{ .type = Type.ptr_amdgpu_constant },
                    .attrs = &.{.{ .@"align" = .wrap(.fromByteUnits(4)) }},
                },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .speculatable, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },

        .@"nvvm.read.ptx.sreg.tid.x" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nounwind, .readnone },
        },
        .@"nvvm.read.ptx.sreg.tid.y" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nounwind, .readnone },
        },
        .@"nvvm.read.ptx.sreg.tid.z" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nounwind, .readnone },
        },

        .@"nvvm.read.ptx.sreg.ntid.x" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nounwind, .readnone },
        },
        .@"nvvm.read.ptx.sreg.ntid.y" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nounwind, .readnone },
        },
        .@"nvvm.read.ptx.sreg.ntid.z" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nounwind, .readnone },
        },

        .@"nvvm.read.ptx.sreg.ctaid.x" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nounwind, .readnone },
        },
        .@"nvvm.read.ptx.sreg.ctaid.y" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nounwind, .readnone },
        },
        .@"nvvm.read.ptx.sreg.ctaid.z" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nounwind, .readnone },
        },

        .@"wasm.memory.size" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i32 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn, .{ .memory = Attribute.Memory.all(.none) } },
        },
        .@"wasm.memory.grow" = .{
            .ret_len = 1,
            .params = &.{
                .{ .kind = .overloaded },
                .{ .kind = .{ .type = .i32 } },
                .{ .kind = .{ .matches = 0 } },
            },
            .attrs = &.{ .nocallback, .nofree, .nosync, .nounwind, .willreturn },
        },
    });
};

pub const Function = struct {
    global: Global.Index,
    call_conv: CallConv = CallConv.default,
    attributes: FunctionAttributes = .none,
    section: String = .none,
    alignment: Alignment = .default,
    blocks: []const Block = &.{},
    instructions: std.MultiArrayList(Instruction) = .empty,
    names: [*]const String = &[0]String{},
    value_indices: [*]const u32 = &[0]u32{},
    strip: bool,
    debug_locations: std.AutoHashMapUnmanaged(Instruction.Index, DebugLocation) = .empty,
    debug_values: []const Instruction.Index = &.{},
    extra: []const u32 = &.{},

    pub const Index = enum(u32) {
        none = maxInt(u32),
        _,

        pub fn ptr(self: Index, builder: *Builder) *Function {
            return &builder.functions.items[@intFromEnum(self)];
        }

        pub fn ptrConst(self: Index, builder: *const Builder) *const Function {
            return &builder.functions.items[@intFromEnum(self)];
        }

        pub fn name(self: Index, builder: *const Builder) StrtabString {
            return self.ptrConst(builder).global.name(builder);
        }

        pub fn rename(self: Index, new_name: StrtabString, builder: *Builder) Allocator.Error!void {
            return self.ptrConst(builder).global.rename(new_name, builder);
        }

        pub fn typeOf(self: Index, builder: *const Builder) Type {
            return self.ptrConst(builder).global.typeOf(builder);
        }

        pub fn toConst(self: Index, builder: *const Builder) Constant {
            return self.ptrConst(builder).global.toConst();
        }

        pub fn toValue(self: Index, builder: *const Builder) Value {
            return self.toConst(builder).toValue();
        }

        pub fn setLinkage(self: Index, linkage: Linkage, builder: *Builder) void {
            return self.ptrConst(builder).global.setLinkage(linkage, builder);
        }

        pub fn setUnnamedAddr(self: Index, unnamed_addr: UnnamedAddr, builder: *Builder) void {
            return self.ptrConst(builder).global.setUnnamedAddr(unnamed_addr, builder);
        }

        pub fn setCallConv(self: Index, call_conv: CallConv, builder: *Builder) void {
            self.ptr(builder).call_conv = call_conv;
        }

        pub fn setAttributes(
            self: Index,
            new_function_attributes: FunctionAttributes,
            builder: *Builder,
        ) void {
            self.ptr(builder).attributes = new_function_attributes;
        }

        pub fn setSection(self: Index, section: String, builder: *Builder) void {
            self.ptr(builder).section = section;
        }

        pub fn setAlignment(self: Index, alignment: Alignment, builder: *Builder) void {
            self.ptr(builder).alignment = alignment;
        }

        pub fn setSubprogram(self: Index, subprogram: Metadata, builder: *Builder) void {
            self.ptrConst(builder).global.setDebugMetadata(subprogram, builder);
        }

        pub fn getSubprogram(self: Index, builder: *const Builder) Metadata.Optional {
            return self.ptrConst(builder).global.getDebugMetadata(builder);
        }
    };

    pub const Block = struct {
        instruction: Instruction.Index,

        pub const Index = WipFunction.Block.Index;
    };

    pub const Instruction = struct {
        tag: Tag,
        data: u32,

        pub const Tag = enum(u8) {
            add,
            @"add nsw",
            @"add nuw",
            @"add nuw nsw",
            addrspacecast,
            alloca,
            @"alloca inalloca",
            @"and",
            arg,
            ashr,
            @"ashr exact",
            atomicrmw,
            bitcast,
            block,
            br,
            br_cond,
            call,
            @"call fast",
            cmpxchg,
            @"cmpxchg weak",
            extractelement,
            extractvalue,
            fadd,
            @"fadd fast",
            @"fcmp false",
            @"fcmp fast false",
            @"fcmp fast oeq",
            @"fcmp fast oge",
            @"fcmp fast ogt",
            @"fcmp fast ole",
            @"fcmp fast olt",
            @"fcmp fast one",
            @"fcmp fast ord",
            @"fcmp fast true",
            @"fcmp fast ueq",
            @"fcmp fast uge",
            @"fcmp fast ugt",
            @"fcmp fast ule",
            @"fcmp fast ult",
            @"fcmp fast une",
            @"fcmp fast uno",
            @"fcmp oeq",
            @"fcmp oge",
            @"fcmp ogt",
            @"fcmp ole",
            @"fcmp olt",
            @"fcmp one",
            @"fcmp ord",
            @"fcmp true",
            @"fcmp ueq",
            @"fcmp uge",
            @"fcmp ugt",
            @"fcmp ule",
            @"fcmp ult",
            @"fcmp une",
            @"fcmp uno",
            fdiv,
            @"fdiv fast",
            fence,
            fmul,
            @"fmul fast",
            fneg,
            @"fneg fast",
            fpext,
            fptosi,
            fptoui,
            fptrunc,
            frem,
            @"frem fast",
            fsub,
            @"fsub fast",
            getelementptr,
            @"getelementptr inbounds",
            @"icmp eq",
            @"icmp ne",
            @"icmp sge",
            @"icmp sgt",
            @"icmp sle",
            @"icmp slt",
            @"icmp uge",
            @"icmp ugt",
            @"icmp ule",
            @"icmp ult",
            indirectbr,
            insertelement,
            insertvalue,
            inttoptr,
            load,
            @"load atomic",
            lshr,
            @"lshr exact",
            mul,
            @"mul nsw",
            @"mul nuw",
            @"mul nuw nsw",
            @"musttail call",
            @"musttail call fast",
            @"notail call",
            @"notail call fast",
            @"or",
            phi,
            @"phi fast",
            ptrtoint,
            ret,
            @"ret void",
            sdiv,
            @"sdiv exact",
            select,
            @"select fast",
            sext,
            shl,
            @"shl nsw",
            @"shl nuw",
            @"shl nuw nsw",
            shufflevector,
            sitofp,
            srem,
            store,
            @"store atomic",
            sub,
            @"sub nsw",
            @"sub nuw",
            @"sub nuw nsw",
            @"switch",
            @"tail call",
            @"tail call fast",
            trunc,
            udiv,
            @"udiv exact",
            urem,
            uitofp,
            @"unreachable",
            va_arg,
            xor,
            zext,

            pub fn toBinaryOpcode(self: Tag) BinaryOpcode {
                return switch (self) {
                    .add,
                    .@"add nsw",
                    .@"add nuw",
                    .@"add nuw nsw",
                    .fadd,
                    .@"fadd fast",
                    => .add,
                    .sub,
                    .@"sub nsw",
                    .@"sub nuw",
                    .@"sub nuw nsw",
                    .fsub,
                    .@"fsub fast",
                    => .sub,
                    .sdiv,
                    .@"sdiv exact",
                    .fdiv,
                    .@"fdiv fast",
                    => .sdiv,
                    .fmul,
                    .@"fmul fast",
                    .mul,
                    .@"mul nsw",
                    .@"mul nuw",
                    .@"mul nuw nsw",
                    => .mul,
                    .srem,
                    .frem,
                    .@"frem fast",
                    => .srem,
                    .udiv,
                    .@"udiv exact",
                    => .udiv,
                    .shl,
                    .@"shl nsw",
                    .@"shl nuw",
                    .@"shl nuw nsw",
                    => .shl,
                    .lshr,
                    .@"lshr exact",
                    => .lshr,
                    .ashr,
                    .@"ashr exact",
                    => .ashr,
                    .@"and" => .@"and",
                    .@"or" => .@"or",
                    .xor => .xor,
                    .urem => .urem,
                    else => unreachable,
                };
            }

            pub fn toCastOpcode(self: Tag) CastOpcode {
                return switch (self) {
                    .trunc => .trunc,
                    .zext => .zext,
                    .sext => .sext,
                    .fptoui => .fptoui,
                    .fptosi => .fptosi,
                    .uitofp => .uitofp,
                    .sitofp => .sitofp,
                    .fptrunc => .fptrunc,
                    .fpext => .fpext,
                    .ptrtoint => .ptrtoint,
                    .inttoptr => .inttoptr,
                    .bitcast => .bitcast,
                    .addrspacecast => .addrspacecast,
                    else => unreachable,
                };
            }

            pub fn toCmpPredicate(self: Tag) CmpPredicate {
                return switch (self) {
                    .@"fcmp false",
                    .@"fcmp fast false",
                    => .fcmp_false,
                    .@"fcmp oeq",
                    .@"fcmp fast oeq",
                    => .fcmp_oeq,
                    .@"fcmp oge",
                    .@"fcmp fast oge",
                    => .fcmp_oge,
                    .@"fcmp ogt",
                    .@"fcmp fast ogt",
                    => .fcmp_ogt,
                    .@"fcmp ole",
                    .@"fcmp fast ole",
                    => .fcmp_ole,
                    .@"fcmp olt",
                    .@"fcmp fast olt",
                    => .fcmp_olt,
                    .@"fcmp one",
                    .@"fcmp fast one",
                    => .fcmp_one,
                    .@"fcmp ord",
                    .@"fcmp fast ord",
                    => .fcmp_ord,
                    .@"fcmp true",
                    .@"fcmp fast true",
                    => .fcmp_true,
                    .@"fcmp ueq",
                    .@"fcmp fast ueq",
                    => .fcmp_ueq,
                    .@"fcmp uge",
                    .@"fcmp fast uge",
                    => .fcmp_uge,
                    .@"fcmp ugt",
                    .@"fcmp fast ugt",
                    => .fcmp_ugt,
                    .@"fcmp ule",
                    .@"fcmp fast ule",
                    => .fcmp_ule,
                    .@"fcmp ult",
                    .@"fcmp fast ult",
                    => .fcmp_ult,
                    .@"fcmp une",
                    .@"fcmp fast une",
                    => .fcmp_une,
                    .@"fcmp uno",
                    .@"fcmp fast uno",
                    => .fcmp_uno,
                    .@"icmp eq" => .icmp_eq,
                    .@"icmp ne" => .icmp_ne,
                    .@"icmp sge" => .icmp_sge,
                    .@"icmp sgt" => .icmp_sgt,
                    .@"icmp sle" => .icmp_sle,
                    .@"icmp slt" => .icmp_slt,
                    .@"icmp uge" => .icmp_uge,
                    .@"icmp ugt" => .icmp_ugt,
                    .@"icmp ule" => .icmp_ule,
                    .@"icmp ult" => .icmp_ult,
                    else => unreachable,
                };
            }
        };

        pub const Index = enum(u32) {
            none = maxInt(u31),
            _,

            pub fn name(self: Instruction.Index, function: *const Function) String {
                return function.names[@intFromEnum(self)];
            }

            pub fn valueIndex(self: Instruction.Index, function: *const Function) u32 {
                return function.value_indices[@intFromEnum(self)];
            }

            pub fn toValue(self: Instruction.Index) Value {
                return @enumFromInt(@intFromEnum(self));
            }

            pub fn isTerminatorWip(self: Instruction.Index, wip: *const WipFunction) bool {
                return switch (wip.instructions.items(.tag)[@intFromEnum(self)]) {
                    .br,
                    .br_cond,
                    .indirectbr,
                    .ret,
                    .@"ret void",
                    .@"switch",
                    .@"unreachable",
                    => true,
                    else => false,
                };
            }

            pub fn hasResultWip(self: Instruction.Index, wip: *const WipFunction) bool {
                return switch (wip.instructions.items(.tag)[@intFromEnum(self)]) {
                    .br,
                    .br_cond,
                    .fence,
                    .indirectbr,
                    .ret,
                    .@"ret void",
                    .store,
                    .@"store atomic",
                    .@"switch",
                    .@"unreachable",
                    .block,
                    => false,
                    .call,
                    .@"call fast",
                    .@"musttail call",
                    .@"musttail call fast",
                    .@"notail call",
                    .@"notail call fast",
                    .@"tail call",
                    .@"tail call fast",
                    => self.typeOfWip(wip) != .void,
                    else => true,
                };
            }

            pub fn typeOfWip(self: Instruction.Index, wip: *const WipFunction) Type {
                const instruction = wip.instructions.get(@intFromEnum(self));
                return switch (instruction.tag) {
                    .add,
                    .@"add nsw",
                    .@"add nuw",
                    .@"add nuw nsw",
                    .@"and",
                    .ashr,
                    .@"ashr exact",
                    .fadd,
                    .@"fadd fast",
                    .fdiv,
                    .@"fdiv fast",
                    .fmul,
                    .@"fmul fast",
                    .frem,
                    .@"frem fast",
                    .fsub,
                    .@"fsub fast",
                    .lshr,
                    .@"lshr exact",
                    .mul,
                    .@"mul nsw",
                    .@"mul nuw",
                    .@"mul nuw nsw",
                    .@"or",
                    .sdiv,
                    .@"sdiv exact",
                    .shl,
                    .@"shl nsw",
                    .@"shl nuw",
                    .@"shl nuw nsw",
                    .srem,
                    .sub,
                    .@"sub nsw",
                    .@"sub nuw",
                    .@"sub nuw nsw",
                    .udiv,
                    .@"udiv exact",
                    .urem,
                    .xor,
                    => wip.extraData(Binary, instruction.data).lhs.typeOfWip(wip),
                    .addrspacecast,
                    .bitcast,
                    .fpext,
                    .fptosi,
                    .fptoui,
                    .fptrunc,
                    .inttoptr,
                    .ptrtoint,
                    .sext,
                    .sitofp,
                    .trunc,
                    .uitofp,
                    .zext,
                    => wip.extraData(Cast, instruction.data).type,
                    .alloca,
                    .@"alloca inalloca",
                    => wip.builder.ptrTypeAssumeCapacity(
                        wip.extraData(Alloca, instruction.data).info.addr_space,
                    ),
                    .arg => wip.function.typeOf(wip.builder)
                        .functionParameters(wip.builder)[instruction.data],
                    .atomicrmw => wip.extraData(AtomicRmw, instruction.data).val.typeOfWip(wip),
                    .block => .label,
                    .br,
                    .br_cond,
                    .fence,
                    .indirectbr,
                    .ret,
                    .@"ret void",
                    .store,
                    .@"store atomic",
                    .@"switch",
                    .@"unreachable",
                    => .none,
                    .call,
                    .@"call fast",
                    .@"musttail call",
                    .@"musttail call fast",
                    .@"notail call",
                    .@"notail call fast",
                    .@"tail call",
                    .@"tail call fast",
                    => wip.extraData(Call, instruction.data).ty.functionReturn(wip.builder),
                    .cmpxchg,
                    .@"cmpxchg weak",
                    => wip.builder.structTypeAssumeCapacity(.normal, &.{
                        wip.extraData(CmpXchg, instruction.data).cmp.typeOfWip(wip),
                        .i1,
                    }),
                    .extractelement => wip.extraData(ExtractElement, instruction.data)
                        .val.typeOfWip(wip).childType(wip.builder),
                    .extractvalue => {
                        var extra = wip.extraDataTrail(ExtractValue, instruction.data);
                        const indices = extra.trail.next(extra.data.indices_len, u32, wip);
                        return extra.data.val.typeOfWip(wip).childTypeAt(indices, wip.builder);
                    },
                    .@"fcmp false",
                    .@"fcmp fast false",
                    .@"fcmp fast oeq",
                    .@"fcmp fast oge",
                    .@"fcmp fast ogt",
                    .@"fcmp fast ole",
                    .@"fcmp fast olt",
                    .@"fcmp fast one",
                    .@"fcmp fast ord",
                    .@"fcmp fast true",
                    .@"fcmp fast ueq",
                    .@"fcmp fast uge",
                    .@"fcmp fast ugt",
                    .@"fcmp fast ule",
                    .@"fcmp fast ult",
                    .@"fcmp fast une",
                    .@"fcmp fast uno",
                    .@"fcmp oeq",
                    .@"fcmp oge",
                    .@"fcmp ogt",
                    .@"fcmp ole",
                    .@"fcmp olt",
                    .@"fcmp one",
                    .@"fcmp ord",
                    .@"fcmp true",
                    .@"fcmp ueq",
                    .@"fcmp uge",
                    .@"fcmp ugt",
                    .@"fcmp ule",
                    .@"fcmp ult",
                    .@"fcmp une",
                    .@"fcmp uno",
                    .@"icmp eq",
                    .@"icmp ne",
                    .@"icmp sge",
                    .@"icmp sgt",
                    .@"icmp sle",
                    .@"icmp slt",
                    .@"icmp uge",
                    .@"icmp ugt",
                    .@"icmp ule",
                    .@"icmp ult",
                    => wip.extraData(Binary, instruction.data).lhs.typeOfWip(wip)
                        .changeScalarAssumeCapacity(.i1, wip.builder),
                    .fneg,
                    .@"fneg fast",
                    => @as(Value, @enumFromInt(instruction.data)).typeOfWip(wip),
                    .getelementptr,
                    .@"getelementptr inbounds",
                    => {
                        var extra = wip.extraDataTrail(GetElementPtr, instruction.data);
                        const indices = extra.trail.next(extra.data.indices_len, Value, wip);
                        const base_ty = extra.data.base.typeOfWip(wip);
                        if (!base_ty.isVector(wip.builder)) for (indices) |index| {
                            const index_ty = index.typeOfWip(wip);
                            if (!index_ty.isVector(wip.builder)) continue;
                            return index_ty.changeScalarAssumeCapacity(base_ty, wip.builder);
                        };
                        return base_ty;
                    },
                    .insertelement => wip.extraData(InsertElement, instruction.data).val.typeOfWip(wip),
                    .insertvalue => wip.extraData(InsertValue, instruction.data).val.typeOfWip(wip),
                    .load,
                    .@"load atomic",
                    => wip.extraData(Load, instruction.data).type,
                    .phi,
                    .@"phi fast",
                    => wip.extraData(Phi, instruction.data).type,
                    .select,
                    .@"select fast",
                    => wip.extraData(Select, instruction.data).lhs.typeOfWip(wip),
                    .shufflevector => {
                        const extra = wip.extraData(ShuffleVector, instruction.data);
                        return extra.lhs.typeOfWip(wip).changeLengthAssumeCapacity(
                            extra.mask.typeOfWip(wip).vectorLen(wip.builder),
                            wip.builder,
                        );
                    },
                    .va_arg => wip.extraData(VaArg, instruction.data).type,
                };
            }

            pub fn typeOf(
                self: Instruction.Index,
                function_index: Function.Index,
                builder: *Builder,
            ) Type {
                const function = function_index.ptrConst(builder);
                const instruction = function.instructions.get(@intFromEnum(self));
                return switch (instruction.tag) {
                    .add,
                    .@"add nsw",
                    .@"add nuw",
                    .@"add nuw nsw",
                    .@"and",
                    .ashr,
                    .@"ashr exact",
                    .fadd,
                    .@"fadd fast",
                    .fdiv,
                    .@"fdiv fast",
                    .fmul,
                    .@"fmul fast",
                    .frem,
                    .@"frem fast",
                    .fsub,
                    .@"fsub fast",
                    .lshr,
                    .@"lshr exact",
                    .mul,
                    .@"mul nsw",
                    .@"mul nuw",
                    .@"mul nuw nsw",
                    .@"or",
                    .sdiv,
                    .@"sdiv exact",
                    .shl,
                    .@"shl nsw",
                    .@"shl nuw",
                    .@"shl nuw nsw",
                    .srem,
                    .sub,
                    .@"sub nsw",
                    .@"sub nuw",
                    .@"sub nuw nsw",
                    .udiv,
                    .@"udiv exact",
                    .urem,
                    .xor,
                    => function.extraData(Binary, instruction.data).lhs.typeOf(function_index, builder),
                    .addrspacecast,
                    .bitcast,
                    .fpext,
                    .fptosi,
                    .fptoui,
                    .fptrunc,
                    .inttoptr,
                    .ptrtoint,
                    .sext,
                    .sitofp,
                    .trunc,
                    .uitofp,
                    .zext,
                    => function.extraData(Cast, instruction.data).type,
                    .alloca,
                    .@"alloca inalloca",
                    => builder.ptrTypeAssumeCapacity(
                        function.extraData(Alloca, instruction.data).info.addr_space,
                    ),
                    .arg => function.global.typeOf(builder)
                        .functionParameters(builder)[instruction.data],
                    .atomicrmw => function.extraData(AtomicRmw, instruction.data)
                        .val.typeOf(function_index, builder),
                    .block => .label,
                    .br,
                    .br_cond,
                    .fence,
                    .indirectbr,
                    .ret,
                    .@"ret void",
                    .store,
                    .@"store atomic",
                    .@"switch",
                    .@"unreachable",
                    => .none,
                    .call,
                    .@"call fast",
                    .@"musttail call",
                    .@"musttail call fast",
                    .@"notail call",
                    .@"notail call fast",
                    .@"tail call",
                    .@"tail call fast",
                    => function.extraData(Call, instruction.data).ty.functionReturn(builder),
                    .cmpxchg,
                    .@"cmpxchg weak",
                    => builder.structTypeAssumeCapacity(.normal, &.{
                        function.extraData(CmpXchg, instruction.data)
                            .cmp.typeOf(function_index, builder),
                        .i1,
                    }),
                    .extractelement => function.extraData(ExtractElement, instruction.data)
                        .val.typeOf(function_index, builder).childType(builder),
                    .extractvalue => {
                        var extra = function.extraDataTrail(ExtractValue, instruction.data);
                        const indices = extra.trail.next(extra.data.indices_len, u32, function);
                        return extra.data.val.typeOf(function_index, builder)
                            .childTypeAt(indices, builder);
                    },
                    .@"fcmp false",
                    .@"fcmp fast false",
                    .@"fcmp fast oeq",
                    .@"fcmp fast oge",
                    .@"fcmp fast ogt",
                    .@"fcmp fast ole",
                    .@"fcmp fast olt",
                    .@"fcmp fast one",
                    .@"fcmp fast ord",
                    .@"fcmp fast true",
                    .@"fcmp fast ueq",
                    .@"fcmp fast uge",
                    .@"fcmp fast ugt",
                    .@"fcmp fast ule",
                    .@"fcmp fast ult",
                    .@"fcmp fast une",
                    .@"fcmp fast uno",
                    .@"fcmp oeq",
                    .@"fcmp oge",
                    .@"fcmp ogt",
                    .@"fcmp ole",
                    .@"fcmp olt",
                    .@"fcmp one",
                    .@"fcmp ord",
                    .@"fcmp true",
                    .@"fcmp ueq",
                    .@"fcmp uge",
                    .@"fcmp ugt",
                    .@"fcmp ule",
                    .@"fcmp ult",
                    .@"fcmp une",
                    .@"fcmp uno",
                    .@"icmp eq",
                    .@"icmp ne",
                    .@"icmp sge",
                    .@"icmp sgt",
                    .@"icmp sle",
                    .@"icmp slt",
                    .@"icmp uge",
                    .@"icmp ugt",
                    .@"icmp ule",
                    .@"icmp ult",
                    => function.extraData(Binary, instruction.data).lhs.typeOf(function_index, builder)
                        .changeScalarAssumeCapacity(.i1, builder),
                    .fneg,
                    .@"fneg fast",
                    => @as(Value, @enumFromInt(instruction.data)).typeOf(function_index, builder),
                    .getelementptr,
                    .@"getelementptr inbounds",
                    => {
                        var extra = function.extraDataTrail(GetElementPtr, instruction.data);
                        const indices = extra.trail.next(extra.data.indices_len, Value, function);
                        const base_ty = extra.data.base.typeOf(function_index, builder);
                        if (!base_ty.isVector(builder)) for (indices) |index| {
                            const index_ty = index.typeOf(function_index, builder);
                            if (!index_ty.isVector(builder)) continue;
                            return index_ty.changeScalarAssumeCapacity(base_ty, builder);
                        };
                        return base_ty;
                    },
                    .insertelement => function.extraData(InsertElement, instruction.data)
                        .val.typeOf(function_index, builder),
                    .insertvalue => function.extraData(InsertValue, instruction.data)
                        .val.typeOf(function_index, builder),
                    .load,
                    .@"load atomic",
                    => function.extraData(Load, instruction.data).type,
                    .phi,
                    .@"phi fast",
                    => function.extraData(Phi, instruction.data).type,
                    .select,
                    .@"select fast",
                    => function.extraData(Select, instruction.data).lhs.typeOf(function_index, builder),
                    .shufflevector => {
                        const extra = function.extraData(ShuffleVector, instruction.data);
                        return extra.lhs.typeOf(function_index, builder).changeLengthAssumeCapacity(
                            extra.mask.typeOf(function_index, builder).vectorLen(builder),
                            builder,
                        );
                    },
                    .va_arg => function.extraData(VaArg, instruction.data).type,
                };
            }

            const FormatData = struct {
                instruction: Instruction.Index,
                function: Function.Index,
                builder: *Builder,
                flags: FormatFlags,
            };
            fn format(data: FormatData, w: *Writer) Writer.Error!void {
                if (data.flags.comma) {
                    if (data.instruction == .none) return;
                    try w.writeByte(',');
                }
                if (data.flags.space) {
                    if (data.instruction == .none) return;
                    try w.writeByte(' ');
                }
                if (data.flags.percent) try w.print(
                    "{f} ",
                    .{data.instruction.typeOf(data.function, data.builder).fmt(data.builder, .percent)},
                );
                assert(data.instruction != .none);
                try w.print("%{f}", .{
                    data.instruction.name(data.function.ptrConst(data.builder)).fmt(data.builder),
                });
            }
            pub fn fmt(
                self: Instruction.Index,
                function: Function.Index,
                builder: *Builder,
                flags: FormatFlags,
            ) std.fmt.Alt(FormatData, format) {
                return .{ .data = .{
                    .instruction = self,
                    .function = function,
                    .builder = builder,
                    .flags = flags,
                } };
            }
        };

        pub const ExtraIndex = u32;

        pub const BrCond = struct {
            cond: Value,
            then: Block.Index,
            @"else": Block.Index,
            weights: Weights,

            pub const Weights = enum(u32) {
                none = @bitCast(Metadata.Optional.none),
                unpredictable,
                _,

                pub fn fromMetadata(metadata: Metadata) Weights {
                    assert(metadata.kind == .node);
                    return @enumFromInt(metadata.index);
                }

                pub fn toMetadata(weights: Weights) Metadata {
                    return .{ .index = @intCast(@intFromEnum(weights)), .kind = .node };
                }
            };
        };

        pub const Switch = struct {
            val: Value,
            default: Block.Index,
            cases_len: u32,
            weights: BrCond.Weights,
            //case_vals: [cases_len]Constant,
            //case_blocks: [cases_len]Block.Index,
        };

        pub const IndirectBr = struct {
            addr: Value,
            targets_len: u32,
            //targets: [targets_len]Block.Index,
        };

        pub const Binary = struct {
            lhs: Value,
            rhs: Value,
        };

        pub const ExtractElement = struct {
            val: Value,
            index: Value,
        };

        pub const InsertElement = struct {
            val: Value,
            elem: Value,
            index: Value,
        };

        pub const ShuffleVector = struct {
            lhs: Value,
            rhs: Value,
            mask: Value,
        };

        pub const ExtractValue = struct {
            val: Value,
            indices_len: u32,
            //indices: [indices_len]u32,
        };

        pub const InsertValue = struct {
            val: Value,
            elem: Value,
            indices_len: u32,
            //indices: [indices_len]u32,
        };

        pub const Alloca = struct {
            type: Type,
            len: Value,
            info: Info,

            pub const Kind = enum { normal, inalloca };
            pub const Info = packed struct(u32) {
                alignment: Alignment,
                addr_space: AddrSpace,
                _: u2 = undefined,
            };
        };

        pub const Load = struct {
            info: MemoryAccessInfo,
            type: Type,
            ptr: Value,
        };

        pub const Store = struct {
            info: MemoryAccessInfo,
            val: Value,
            ptr: Value,
        };

        pub const CmpXchg = struct {
            info: MemoryAccessInfo,
            ptr: Value,
            cmp: Value,
            new: Value,

            pub const Kind = enum { strong, weak };
        };

        pub const AtomicRmw = struct {
            info: MemoryAccessInfo,
            ptr: Value,
            val: Value,

            pub const Operation = enum(u5) {
                xchg = 0,
                add = 1,
                sub = 2,
                @"and" = 3,
                nand = 4,
                @"or" = 5,
                xor = 6,
                max = 7,
                min = 8,
                umax = 9,
                umin = 10,
                fadd = 11,
                fsub = 12,
                fmax = 13,
                fmin = 14,
                none = maxInt(u5),
            };
        };

        pub const GetElementPtr = struct {
            type: Type,
            base: Value,
            indices_len: u32,
            //indices: [indices_len]Value,

            pub const Kind = Constant.GetElementPtr.Kind;
        };

        pub const Cast = struct {
            val: Value,
            type: Type,

            pub const Signedness = Constant.Cast.Signedness;
        };

        pub const Phi = struct {
            type: Type,
            //incoming_vals: [block.incoming]Value,
            //incoming_blocks: [block.incoming]Block.Index,
        };

        pub const Select = struct {
            cond: Value,
            lhs: Value,
            rhs: Value,
        };

        pub const Call = struct {
            info: Info,
            attributes: FunctionAttributes,
            ty: Type,
            callee: Value,
            args_len: u32,
            //args: [args_len]Value,

            pub const Kind = enum {
                normal,
                fast,
                musttail,
                musttail_fast,
                notail,
                notail_fast,
                tail,
                tail_fast,
            };
            pub const Info = packed struct(u32) {
                call_conv: CallConv,
                has_op_bundle_cold: bool,
                _: u21 = undefined,
            };
        };

        pub const VaArg = struct {
            list: Value,
            type: Type,
        };
    };

    pub fn deinit(self: *Function, gpa: Allocator) void {
        gpa.free(self.extra);
        gpa.free(self.debug_values);
        self.debug_locations.deinit(gpa);
        gpa.free(self.value_indices[0..self.instructions.len]);
        gpa.free(self.names[0..self.instructions.len]);
        self.instructions.deinit(gpa);
        gpa.free(self.blocks);
        self.* = undefined;
    }

    pub fn arg(self: *const Function, index: u32) Value {
        const argument = self.instructions.get(index);
        assert(argument.tag == .arg);
        assert(argument.data == index);

        const argument_index: Instruction.Index = @enumFromInt(index);
        return argument_index.toValue();
    }

    const ExtraDataTrail = struct {
        index: Instruction.ExtraIndex,

        fn nextMut(self: *ExtraDataTrail, len: u32, comptime Item: type, function: *Function) []Item {
            const items: []Item = @ptrCast(function.extra[self.index..][0..len]);
            self.index += @intCast(len);
            return items;
        }

        fn next(
            self: *ExtraDataTrail,
            len: u32,
            comptime Item: type,
            function: *const Function,
        ) []const Item {
            const items: []const Item = @ptrCast(function.extra[self.index..][0..len]);
            self.index += @intCast(len);
            return items;
        }
    };

    fn extraDataTrail(
        self: *const Function,
        comptime T: type,
        index: Instruction.ExtraIndex,
    ) struct { data: T, trail: ExtraDataTrail } {
        var result: T = undefined;
        const fields = @typeInfo(T).@"struct".fields;
        inline for (fields, self.extra[index..][0..fields.len]) |field, value|
            @field(result, field.name) = switch (field.type) {
                u32 => value,
                Alignment,
                AtomicOrdering,
                Block.Index,
                FunctionAttributes,
                Type,
                Value,
                Instruction.BrCond.Weights,
                => @enumFromInt(value),
                MemoryAccessInfo,
                Instruction.Alloca.Info,
                Instruction.Call.Info,
                => @bitCast(value),
                else => @compileError("bad field type: " ++ field.name ++ ": " ++ @typeName(field.type)),
            };
        return .{
            .data = result,
            .trail = .{ .index = index + @as(Type.Item.ExtraIndex, @intCast(fields.len)) },
        };
    }

    fn extraData(self: *const Function, comptime T: type, index: Instruction.ExtraIndex) T {
        return self.extraDataTrail(T, index).data;
    }
};

pub const DebugLocation = union(enum) {
    no_location: void,
    location: Location,

    pub const Location = struct {
        line: u32,
        column: u32,
        scope: Builder.Metadata.Optional,
        inlined_at: Builder.Metadata.Optional,
    };

    pub fn toMetadata(self: DebugLocation, builder: *Builder) Allocator.Error!Metadata.Optional {
        return switch (self) {
            .no_location => .none,
            .location => |location| (try builder.debugLocation(
                location.line,
                location.column,
                location.scope.unwrap().?,
                location.inlined_at.unwrap(),
            )).toOptional(),
        };
    }
};

pub const WipFunction = struct {
    builder: *Builder,
    function: Function.Index,
    prev_debug_location: DebugLocation,
    debug_location: DebugLocation,
    cursor: Cursor,
    blocks: std.ArrayList(Block),
    instructions: std.MultiArrayList(Instruction),
    names: std.ArrayList(String),
    strip: bool,
    debug_locations: std.AutoArrayHashMapUnmanaged(Instruction.Index, DebugLocation),
    debug_values: std.AutoArrayHashMapUnmanaged(Instruction.Index, void),
    extra: std.ArrayList(u32),

    pub const Cursor = struct { block: Block.Index, instruction: u32 = 0 };

    pub const Block = struct {
        name: String,
        incoming: u32,
        branches: u32 = 0,
        instructions: std.ArrayList(Instruction.Index),

        const Index = enum(u32) {
            entry,
            _,

            pub fn ptr(self: Index, wip: *WipFunction) *Block {
                return &wip.blocks.items[@intFromEnum(self)];
            }

            pub fn ptrConst(self: Index, wip: *const WipFunction) *const Block {
                return &wip.blocks.items[@intFromEnum(self)];
            }

            pub fn toInst(self: Index, function: *const Function) Instruction.Index {
                return function.blocks[@intFromEnum(self)].instruction;
            }
        };
    };

    pub const Instruction = Function.Instruction;

    pub fn init(builder: *Builder, options: struct {
        function: Function.Index,
        strip: bool,
    }) Allocator.Error!WipFunction {
        var self: WipFunction = .{
            .builder = builder,
            .function = options.function,
            .prev_debug_location = .no_location,
            .debug_location = .no_location,
            .cursor = undefined,
            .blocks = .empty,
            .instructions = .empty,
            .names = .empty,
            .strip = options.strip,
            .debug_locations = .empty,
            .debug_values = .empty,
            .extra = .empty,
        };
        errdefer self.deinit();

        const params_len = options.function.typeOf(self.builder).functionParameters(self.builder).len;
        try self.ensureUnusedExtraCapacity(params_len, NoExtra, 0);
        try self.instructions.ensureUnusedCapacity(self.builder.gpa, params_len);
        if (!self.strip) {
            try self.names.ensureUnusedCapacity(self.builder.gpa, params_len);
        }
        for (0..params_len) |param_index| {
            self.instructions.appendAssumeCapacity(.{ .tag = .arg, .data = @intCast(param_index) });
            if
```
