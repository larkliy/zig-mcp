```
 reader.takeByte()),
                OP.deref_size,
                OP.xderef_size,
                => .{ .type_size = try reader.takeByte() },
                OP.const1s => generic(try reader.takeByteSigned()),
                OP.const2u,
                OP.call2,
                => generic(try reader.takeInt(u16, options.endian)),
                OP.call4 => generic(try reader.takeInt(u32, options.endian)),
                OP.const2s => generic(try reader.takeInt(i16, options.endian)),
                OP.bra,
                OP.skip,
                => .{ .branch_offset = try reader.takeInt(i16, options.endian) },
                OP.const4u => generic(try reader.takeInt(u32, options.endian)),
                OP.const4s => generic(try reader.takeInt(i32, options.endian)),
                OP.const8u => generic(try reader.takeInt(u64, options.endian)),
                OP.const8s => generic(try reader.takeInt(i64, options.endian)),
                OP.constu,
                OP.plus_uconst,
                OP.addrx,
                OP.constx,
                OP.convert,
                OP.reinterpret,
                => generic(try reader.takeLeb128(u64)),
                OP.consts,
                OP.fbreg,
                => generic(try reader.takeLeb128(i64)),
                OP.lit0...OP.lit31 => |n| generic(n - OP.lit0),
                OP.reg0...OP.reg31 => |n| .{ .register = n - OP.reg0 },
                OP.breg0...OP.breg31 => |n| .{ .base_register = .{
                    .base_register = n - OP.breg0,
                    .offset = try reader.takeLeb128(i64),
                } },
                OP.regx => .{ .register = try reader.takeLeb128(u8) },
                OP.bregx => blk: {
                    const base_register = try reader.takeLeb128(u8);
                    const offset = try reader.takeLeb128(i64);
                    break :blk .{ .base_register = .{
                        .base_register = base_register,
                        .offset = offset,
                    } };
                },
                OP.regval_type => blk: {
                    const register = try reader.takeLeb128(u8);
                    const type_offset = try reader.takeLeb128(addr_type);
                    break :blk .{ .register_type = .{
                        .register = register,
                        .type_offset = type_offset,
                    } };
                },
                OP.piece => .{
                    .composite_location = .{
                        .size = try reader.takeLeb128(u8),
                        .offset = 0,
                    },
                },
                OP.bit_piece => blk: {
                    const size = try reader.takeLeb128(u8);
                    const offset = try reader.takeLeb128(i64);
                    break :blk .{ .composite_location = .{
                        .size = size,
                        .offset = offset,
                    } };
                },
                OP.implicit_value, OP.entry_value => blk: {
                    const size = try reader.takeLeb128(u8);
                    const block = try reader.take(size);
                    break :blk .{ .block = block };
                },
                OP.const_type => blk: {
                    const type_offset = try reader.takeLeb128(addr_type);
                    const size = try reader.takeByte();
                    const value_bytes = try reader.take(size);
                    break :blk .{ .const_type = .{
                        .type_offset = type_offset,
                        .value_bytes = value_bytes,
                    } };
                },
                OP.deref_type,
                OP.xderef_type,
                => .{
                    .deref_type = .{
                        .size = try reader.takeByte(),
                        .type_offset = try reader.takeLeb128(addr_type),
                    },
                },
                OP.lo_user...OP.hi_user => return error.UnimplementedUserOpcode,
                else => null,
            };
        }

        pub fn run(
            self: *Self,
            expression: []const u8,
            allocator: std.mem.Allocator,
            context: Context,
            initial_value: ?usize,
        ) Error!?Value {
            if (initial_value) |i| try self.stack.append(allocator, .{ .generic = i });
            var stream: std.Io.Reader = .fixed(expression);
            while (try self.step(&stream, allocator, context)) {}
            if (self.stack.items.len == 0) return null;
            return self.stack.items[self.stack.items.len - 1];
        }

        /// Reads an opcode and its operands from `stream`, then executes it
        pub fn step(
            self: *Self,
            stream: *std.Io.Reader,
            allocator: std.mem.Allocator,
            context: Context,
        ) Error!bool {
            if (@sizeOf(usize) != @sizeOf(addr_type) or options.endian != native_endian)
                @compileError("Execution of non-native address sizes / endianness is not supported");

            const opcode = try stream.takeByte();
            if (options.call_frame_context and !isOpcodeValidInCFA(opcode)) return error.InvalidCFAOpcode;
            const operand = try readOperand(stream, opcode, context);
            switch (opcode) {

                // 2.5.1.1: Literal Encodings
                OP.lit0...OP.lit31,
                OP.addr,
                OP.const1u,
                OP.const2u,
                OP.const4u,
                OP.const8u,
                OP.const1s,
                OP.const2s,
                OP.const4s,
                OP.const8s,
                OP.constu,
                OP.consts,
                => try self.stack.append(allocator, .{ .generic = operand.?.generic }),

                OP.const_type => {
                    const const_type = operand.?.const_type;
                    try self.stack.append(allocator, .{ .const_type = .{
                        .type_offset = const_type.type_offset,
                        .value_bytes = const_type.value_bytes,
                    } });
                },

                OP.addrx,
                OP.constx,
                => {
                    if (context.compile_unit == null) return error.IncompleteExpressionContext;
                    if (context.debug_addr == null) return error.IncompleteExpressionContext;
                    const debug_addr_index = operand.?.generic;
                    const offset = context.compile_unit.?.addr_base + debug_addr_index;
                    if (offset >= context.debug_addr.?.len) return error.InvalidExpression;
                    const value = mem.readInt(usize, context.debug_addr.?[offset..][0..@sizeOf(usize)], native_endian);
                    try self.stack.append(allocator, .{ .generic = value });
                },

                // 2.5.1.2: Register Values
                OP.fbreg => {
                    if (context.compile_unit == null) return error.IncompleteExpressionContext;
                    if (context.compile_unit.?.frame_base == null) return error.IncompleteExpressionContext;

                    const offset: i64 = @intCast(operand.?.generic);
                    _ = offset;

                    switch (context.compile_unit.?.frame_base.?.*) {
                        .exprloc => {
                            // TODO: Run this expression in a nested stack machine
                            return error.UnimplementedOpcode;
                        },
                        .loclistx => {
                            // TODO: Read value from .debug_loclists
                            return error.UnimplementedOpcode;
                        },
                        .sec_offset => {
                            // TODO: Read value from .debug_loclists
                            return error.UnimplementedOpcode;
                        },
                        else => return error.InvalidFrameBase,
                    }
                },
                OP.breg0...OP.breg31,
                OP.bregx,
                => {
                    const cpu_context = context.cpu_context orelse return error.IncompleteExpressionContext;

                    const br = operand.?.base_register;
                    const value: i64 = @intCast((try regNative(cpu_context, br.base_register)).*);
                    try self.stack.append(allocator, .{ .generic = @intCast(value + br.offset) });
                },
                OP.regval_type => {
                    const cpu_context = context.cpu_context orelse return error.IncompleteExpressionContext;
                    const rt = operand.?.register_type;
                    try self.stack.append(allocator, .{
                        .regval_type = .{
                            .type_offset = rt.type_offset,
                            .type_size = @sizeOf(addr_type),
                            .value = (try regNative(cpu_context, rt.register)).*,
                        },
                    });
                },

                // 2.5.1.3: Stack Operations
                OP.dup => {
                    if (self.stack.items.len == 0) return error.InvalidExpression;
                    try self.stack.append(allocator, self.stack.items[self.stack.items.len - 1]);
                },
                OP.drop => {
                    _ = self.stack.pop();
                },
                OP.pick, OP.over => {
                    const stack_index = if (opcode == OP.over) 1 else operand.?.generic;
                    if (stack_index >= self.stack.items.len) return error.InvalidExpression;
                    try self.stack.append(allocator, self.stack.items[self.stack.items.len - 1 - stack_index]);
                },
                OP.swap => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    mem.swap(Value, &self.stack.items[self.stack.items.len - 1], &self.stack.items[self.stack.items.len - 2]);
                },
                OP.rot => {
                    if (self.stack.items.len < 3) return error.InvalidExpression;
                    const first = self.stack.items[self.stack.items.len - 1];
                    self.stack.items[self.stack.items.len - 1] = self.stack.items[self.stack.items.len - 2];
                    self.stack.items[self.stack.items.len - 2] = self.stack.items[self.stack.items.len - 3];
                    self.stack.items[self.stack.items.len - 3] = first;
                },
                OP.deref,
                OP.xderef,
                OP.deref_size,
                OP.xderef_size,
                OP.deref_type,
                OP.xderef_type,
                => {
                    if (self.stack.items.len == 0) return error.InvalidExpression;
                    const addr = try self.stack.items[self.stack.items.len - 1].asIntegral();
                    const addr_space_identifier: ?usize = switch (opcode) {
                        OP.xderef,
                        OP.xderef_size,
                        OP.xderef_type,
                        => blk: {
                            _ = self.stack.pop();
                            if (self.stack.items.len == 0) return error.InvalidExpression;
                            break :blk try self.stack.items[self.stack.items.len - 1].asIntegral();
                        },
                        else => null,
                    };

                    // Usage of addr_space_identifier in the address calculation is implementation defined.
                    // This code will need to be updated to handle any architectures that utilize this.
                    _ = addr_space_identifier;

                    const size = switch (opcode) {
                        OP.deref,
                        OP.xderef,
                        => @sizeOf(addr_type),
                        OP.deref_size,
                        OP.xderef_size,
                        => operand.?.type_size,
                        OP.deref_type,
                        OP.xderef_type,
                        => operand.?.deref_type.size,
                        else => unreachable,
                    };

                    const value: addr_type = std.math.cast(addr_type, @as(u64, switch (size) {
                        1 => @as(*const u8, @ptrFromInt(addr)).*,
                        2 => @as(*const u16, @ptrFromInt(addr)).*,
                        4 => @as(*const u32, @ptrFromInt(addr)).*,
                        8 => @as(*const u64, @ptrFromInt(addr)).*,
                        else => return error.InvalidExpression,
                    })) orelse return error.InvalidExpression;

                    switch (opcode) {
                        OP.deref_type,
                        OP.xderef_type,
                        => {
                            self.stack.items[self.stack.items.len - 1] = .{
                                .regval_type = .{
                                    .type_offset = operand.?.deref_type.type_offset,
                                    .type_size = operand.?.deref_type.size,
                                    .value = value,
                                },
                            };
                        },
                        else => {
                            self.stack.items[self.stack.items.len - 1] = .{ .generic = value };
                        },
                    }
                },
                OP.push_object_address => {
                    // In sub-expressions, `push_object_address` is not meaningful (as per the
                    // spec), so treat it like a nop
                    if (!context.entry_value_context) {
                        if (context.object_address == null) return error.IncompleteExpressionContext;
                        try self.stack.append(allocator, .{ .generic = @intFromPtr(context.object_address.?) });
                    }
                },
                OP.form_tls_address => {
                    return error.UnimplementedOpcode;
                },
                OP.call_frame_cfa => {
                    if (context.cfa) |cfa| {
                        try self.stack.append(allocator, .{ .generic = cfa });
                    } else return error.IncompleteExpressionContext;
                },

                // 2.5.1.4: Arithmetic and Logical Operations
                OP.abs => {
                    if (self.stack.items.len == 0) return error.InvalidExpression;
                    const value: isize = @bitCast(try self.stack.items[self.stack.items.len - 1].asIntegral());
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = @abs(value),
                    };
                },
                OP.@"and" => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a = try self.stack.pop().?.asIntegral();
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = a & try self.stack.items[self.stack.items.len - 1].asIntegral(),
                    };
                },
                OP.div => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a: isize = @bitCast(try self.stack.pop().?.asIntegral());
                    const b: isize = @bitCast(try self.stack.items[self.stack.items.len - 1].asIntegral());
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = @bitCast(try std.math.divTrunc(isize, b, a)),
                    };
                },
                OP.minus => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const b = try self.stack.pop().?.asIntegral();
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = try std.math.sub(addr_type, try self.stack.items[self.stack.items.len - 1].asIntegral(), b),
                    };
                },
                OP.mod => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a: isize = @bitCast(try self.stack.pop().?.asIntegral());
                    const b: isize = @bitCast(try self.stack.items[self.stack.items.len - 1].asIntegral());
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = @bitCast(@mod(b, a)),
                    };
                },
                OP.mul => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a: isize = @bitCast(try self.stack.pop().?.asIntegral());
                    const b: isize = @bitCast(try self.stack.items[self.stack.items.len - 1].asIntegral());
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = @bitCast(@mulWithOverflow(a, b)[0]),
                    };
                },
                OP.neg => {
                    if (self.stack.items.len == 0) return error.InvalidExpression;
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = @bitCast(
                            try std.math.negate(
                                @as(isize, @bitCast(try self.stack.items[self.stack.items.len - 1].asIntegral())),
                            ),
                        ),
                    };
                },
                OP.not => {
                    if (self.stack.items.len == 0) return error.InvalidExpression;
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = ~try self.stack.items[self.stack.items.len - 1].asIntegral(),
                    };
                },
                OP.@"or" => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a = try self.stack.pop().?.asIntegral();
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = a | try self.stack.items[self.stack.items.len - 1].asIntegral(),
                    };
                },
                OP.plus => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const b = try self.stack.pop().?.asIntegral();
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = try std.math.add(addr_type, try self.stack.items[self.stack.items.len - 1].asIntegral(), b),
                    };
                },
                OP.plus_uconst => {
                    if (self.stack.items.len == 0) return error.InvalidExpression;
                    const constant = operand.?.generic;
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = try std.math.add(addr_type, try self.stack.items[self.stack.items.len - 1].asIntegral(), constant),
                    };
                },
                OP.shl => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a = try self.stack.pop().?.asIntegral();
                    const b = try self.stack.items[self.stack.items.len - 1].asIntegral();
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = std.math.shl(usize, b, a),
                    };
                },
                OP.shr => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a = try self.stack.pop().?.asIntegral();
                    const b = try self.stack.items[self.stack.items.len - 1].asIntegral();
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = std.math.shr(usize, b, a),
                    };
                },
                OP.shra => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a = try self.stack.pop().?.asIntegral();
                    const b: isize = @bitCast(try self.stack.items[self.stack.items.len - 1].asIntegral());
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = @bitCast(std.math.shr(isize, b, a)),
                    };
                },
                OP.xor => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a = try self.stack.pop().?.asIntegral();
                    self.stack.items[self.stack.items.len - 1] = .{
                        .generic = a ^ try self.stack.items[self.stack.items.len - 1].asIntegral(),
                    };
                },

                // 2.5.1.5: Control Flow Operations
                OP.le,
                OP.ge,
                OP.eq,
                OP.lt,
                OP.gt,
                OP.ne,
                => {
                    if (self.stack.items.len < 2) return error.InvalidExpression;
                    const a = self.stack.pop().?;
                    const b = self.stack.items[self.stack.items.len - 1];

                    if (a == .generic and b == .generic) {
                        const a_int: isize = @bitCast(a.asIntegral() catch unreachable);
                        const b_int: isize = @bitCast(b.asIntegral() catch unreachable);
                        const result = @intFromBool(switch (opcode) {
                            OP.le => b_int <= a_int,
                            OP.ge => b_int >= a_int,
                            OP.eq => b_int == a_int,
                            OP.lt => b_int < a_int,
                            OP.gt => b_int > a_int,
                            OP.ne => b_int != a_int,
                            else => unreachable,
                        });

                        self.stack.items[self.stack.items.len - 1] = .{ .generic = result };
                    } else {
                        // TODO: Load the types referenced by these values, find their comparison operator, and run it
                        return error.UnimplementedTypedComparison;
                    }
                },
                OP.skip, OP.bra => {
                    const branch_offset = operand.?.branch_offset;
                    const condition = if (opcode == OP.bra) blk: {
                        if (self.stack.items.len == 0) return error.InvalidExpression;
                        break :blk try self.stack.pop().?.asIntegral() != 0;
                    } else true;

                    if (condition) {
                        const new_pos = std.math.cast(
                            usize,
                            try std.math.add(isize, @as(isize, @intCast(stream.seek)), branch_offset),
                        ) orelse return error.InvalidExpression;

                        if (new_pos < 0 or new_pos > stream.buffer.len) return error.InvalidExpression;
                        stream.seek = new_pos;
                    }
                },
                OP.call2,
                OP.call4,
                OP.call_ref,
                => {
                    const debug_info_offset = operand.?.generic;
                    _ = debug_info_offset;

                    // TODO: Load a DIE entry at debug_info_offset in a .debug_info section (the spec says that it
                    //       can be in a separate exe / shared object from the one containing this expression).
                    //       Transfer control to the DW_AT_location attribute, with the current stack as input.

                    return error.UnimplementedExpressionCall;
                },

                // 2.5.1.6: Type Conversions
                OP.convert => {
                    if (self.stack.items.len == 0) return error.InvalidExpression;
                    const type_offset = operand.?.generic;

                    // TODO: Load the DW_TAG_base_type entries in context.compile_unit and verify both types are the same size
                    const value = self.stack.items[self.stack.items.len - 1];
                    if (type_offset == 0) {
                        self.stack.items[self.stack.items.len - 1] = .{ .generic = try value.asIntegral() };
                    } else {
                        // TODO: Load the DW_TAG_base_type entry in context.compile_unit, find a conversion operator
                        //       from the old type to the new type, run it.
                        return error.UnimplementedTypeConversion;
                    }
                },
                OP.reinterpret => {
                    if (self.stack.items.len == 0) return error.InvalidExpression;
                    const type_offset = operand.?.generic;

                    // TODO: Load the DW_TAG_base_type entries in context.compile_unit and verify both types are the same size
                    const value = self.stack.items[self.stack.items.len - 1];
                    if (type_offset == 0) {
                        self.stack.items[self.stack.items.len - 1] = .{ .generic = try value.asIntegral() };
                    } else {
                        self.stack.items[self.stack.items.len - 1] = switch (value) {
                            .generic => |v| .{
                                .regval_type = .{
                                    .type_offset = type_offset,
                                    .type_size = @sizeOf(addr_type),
                                    .value = v,
                                },
                            },
                            .regval_type => |r| .{
                                .regval_type = .{
                                    .type_offset = type_offset,
                                    .type_size = r.type_size,
                                    .value = r.value,
                                },
                            },
                            .const_type => |c| .{
                                .const_type = .{
                                    .type_offset = type_offset,
                                    .value_bytes = c.value_bytes,
                                },
                            },
                        };
                    }
                },

                // 2.5.1.7: Special Operations
                OP.nop => {},
                OP.entry_value => {
                    const block = operand.?.block;
                    if (block.len == 0) return error.InvalidSubExpression;

                    // TODO: The spec states that this sub-expression needs to observe the state (ie. registers)
                    //       as it was upon entering the current subprogram. If this isn't being called at the
                    //       end of a frame unwind operation, an additional cpu_context.Native with this state will be needed.

                    if (isOpcodeRegisterLocation(block[0])) {
                        const cpu_context = context.cpu_context orelse return error.IncompleteExpressionContext;

                        var block_stream: std.Io.Reader = .fixed(block);
                        const register = (try readOperand(&block_stream, block[0], context)).?.register;
                        const value = (try regNative(cpu_context, register)).*;
                        try self.stack.append(allocator, .{ .generic = value });
                    } else {
                        var stack_machine: Self = .{};
                        defer stack_machine.deinit(allocator);

                        var sub_context = context;
                        sub_context.entry_value_context = true;
                        const result = try stack_machine.run(block, allocator, sub_context, null);
                        try self.stack.append(allocator, result orelse return error.InvalidSubExpression);
                    }
                },

                // These have already been handled by readOperand
                OP.lo_user...OP.hi_user => unreachable,
                else => {
                    //std.debug.print("Unknown DWARF expression opcode: {x}\n", .{opcode});
                    return error.UnknownExpressionOpcode;
                },
            }

            return stream.seek < stream.buffer.len;
        }
    };
}

pub fn Builder(comptime options: Options) type {
    const addr_type = switch (options.addr_size) {
        2 => u16,
        4 => u32,
        8 => u64,
        else => @compileError("Unsupported address size of " ++ options.addr_size),
    };

    return struct {
        /// Zero-operand instructions
        pub fn writeOpcode(writer: *Writer, comptime opcode: u8) !void {
            if (options.call_frame_context and !comptime isOpcodeValidInCFA(opcode)) return error.InvalidCFAOpcode;
            switch (opcode) {
                OP.dup,
                OP.drop,
                OP.over,
                OP.swap,
                OP.rot,
                OP.deref,
                OP.xderef,
                OP.push_object_address,
                OP.form_tls_address,
                OP.call_frame_cfa,
                OP.abs,
                OP.@"and",
                OP.div,
                OP.minus,
                OP.mod,
                OP.mul,
                OP.neg,
                OP.not,
                OP.@"or",
                OP.plus,
                OP.shl,
                OP.shr,
                OP.shra,
                OP.xor,
                OP.le,
                OP.ge,
                OP.eq,
                OP.lt,
                OP.gt,
                OP.ne,
                OP.nop,
                OP.stack_value,
                => try writer.writeByte(opcode),
                else => @compileError("This opcode requires operands, use `write<Opcode>()` instead"),
            }
        }

        // 2.5.1.1: Literal Encodings
        pub fn writeLiteral(writer: *Writer, literal: u8) !void {
            switch (literal) {
                0...31 => |n| try writer.writeByte(n + OP.lit0),
                else => return error.InvalidLiteral,
            }
        }

        pub fn writeConst(writer: *Writer, comptime T: type, value: T) !void {
            if (@typeInfo(T) != .int) @compileError("Constants must be integers");

            switch (T) {
                u8, i8, u16, i16, u32, i32, u64, i64 => {
                    try writer.writeByte(switch (T) {
                        u8 => OP.const1u,
                        i8 => OP.const1s,
                        u16 => OP.const2u,
                        i16 => OP.const2s,
                        u32 => OP.const4u,
                        i32 => OP.const4s,
                        u64 => OP.const8u,
                        i64 => OP.const8s,
                        else => unreachable,
                    });

                    try writer.writeInt(T, value, options.endian);
                },
                else => switch (@typeInfo(T).int.signedness) {
                    .unsigned => {
                        try writer.writeByte(OP.constu);
                        try writer.writeUleb128(value);
                    },
                    .signed => {
                        try writer.writeByte(OP.consts);
                        try writer.writeLeb128(value);
                    },
                },
            }
        }

        pub fn writeConstx(writer: *Writer, debug_addr_offset: anytype) !void {
            try writer.writeByte(OP.constx);
            try writer.writeUleb128(debug_addr_offset);
        }

        pub fn writeConstType(writer: *Writer, die_offset: anytype, value_bytes: []const u8) !void {
            if (options.call_frame_context) return error.InvalidCFAOpcode;
            if (value_bytes.len > 0xff) return error.InvalidTypeLength;
            try writer.writeByte(OP.const_type);
            try writer.writeUleb128(die_offset);
            try writer.writeByte(@intCast(value_bytes.len));
            try writer.writeAll(value_bytes);
        }

        pub fn writeAddr(writer: *Writer, value: addr_type) !void {
            try writer.writeByte(OP.addr);
            try writer.writeInt(addr_type, value, options.endian);
        }

        pub fn writeAddrx(writer: *Writer, debug_addr_offset: anytype) !void {
            if (options.call_frame_context) return error.InvalidCFAOpcode;
            try writer.writeByte(OP.addrx);
            try writer.writeUleb128(debug_addr_offset);
        }

        // 2.5.1.2: Register Values
        pub fn writeFbreg(writer: *Writer, offset: anytype) !void {
            try writer.writeByte(OP.fbreg);
            try writer.writeSleb128(offset);
        }

        pub fn writeBreg(writer: *Writer, register: u8, offset: anytype) !void {
            if (register > 31) return error.InvalidRegister;
            try writer.writeByte(OP.breg0 + register);
            try writer.writeSleb128(offset);
        }

        pub fn writeBregx(writer: *Writer, register: anytype, offset: anytype) !void {
            try writer.writeByte(OP.bregx);
            try writer.writeUleb128(register);
            try writer.writeSleb128(offset);
        }

        pub fn writeRegvalType(writer: *Writer, register: anytype, offset: anytype) !void {
            if (options.call_frame_context) return error.InvalidCFAOpcode;
            try writer.writeByte(OP.regval_type);
            try writer.writeUleb128(register);
            try writer.writeUleb128(offset);
        }

        // 2.5.1.3: Stack Operations
        pub fn writePick(writer: *Writer, index: u8) !void {
            try writer.writeByte(OP.pick);
            try writer.writeByte(index);
        }

        pub fn writeDerefSize(writer: *Writer, size: u8) !void {
            try writer.writeByte(OP.deref_size);
            try writer.writeByte(size);
        }

        pub fn writeXDerefSize(writer: *Writer, size: u8) !void {
            try writer.writeByte(OP.xderef_size);
            try writer.writeByte(size);
        }

        pub fn writeDerefType(writer: *Writer, size: u8, die_offset: anytype) !void {
            if (options.call_frame_context) return error.InvalidCFAOpcode;
            try writer.writeByte(OP.deref_type);
            try writer.writeByte(size);
            try writer.writeUleb128(die_offset);
        }

        pub fn writeXDerefType(writer: *Writer, size: u8, die_offset: anytype) !void {
            try writer.writeByte(OP.xderef_type);
            try writer.writeByte(size);
            try writer.writeUleb128(die_offset);
        }

        // 2.5.1.4: Arithmetic and Logical Operations

        pub fn writePlusUconst(writer: *Writer, uint_value: anytype) !void {
            try writer.writeByte(OP.plus_uconst);
            try writer.writeUleb128(uint_value);
        }

        // 2.5.1.5: Control Flow Operations

        pub fn writeSkip(writer: *Writer, offset: i16) !void {
            try writer.writeByte(OP.skip);
            try writer.writeInt(i16, offset, options.endian);
        }

        pub fn writeBra(writer: *Writer, offset: i16) !void {
            try writer.writeByte(OP.bra);
            try writer.writeInt(i16, offset, options.endian);
        }

        pub fn writeCall(writer: *Writer, comptime T: type, offset: T) !void {
            if (options.call_frame_context) return error.InvalidCFAOpcode;
            switch (T) {
                u16 => try writer.writeByte(OP.call2),
                u32 => try writer.writeByte(OP.call4),
                else => @compileError("Call operand must be a 2 or 4 byte offset"),
            }

            try writer.writeInt(T, offset, options.endian);
        }

        pub fn writeCallRef(writer: *Writer, comptime is_64: bool, value: if (is_64) u64 else u32) !void {
            if (options.call_frame_context) return error.InvalidCFAOpcode;
            try writer.writeByte(OP.call_ref);
            try writer.writeInt(if (is_64) u64 else u32, value, options.endian);
        }

        pub fn writeConvert(writer: *Writer, die_offset: anytype) !void {
            if (options.call_frame_context) return error.InvalidCFAOpcode;
            try writer.writeByte(OP.convert);
            try writer.writeUleb128(die_offset);
        }

        pub fn writeReinterpret(writer: *Writer, die_offset: anytype) !void {
            if (options.call_frame_context) return error.InvalidCFAOpcode;
            try writer.writeByte(OP.reinterpret);
            try writer.writeUleb128(die_offset);
        }

        // 2.5.1.7: Special Operations

        pub fn writeEntryValue(writer: *Writer, expression: []const u8) !void {
            try writer.writeByte(OP.entry_value);
            try writer.writeUleb128(expression.len);
            try writer.writeAll(expression);
        }

        // 2.6: Location Descriptions
        pub fn writeReg(writer: *Writer, register: u8) !void {
            try writer.writeByte(OP.reg0 + register);
        }

        pub fn writeRegx(writer: *Writer, register: anytype) !void {
            try writer.writeByte(OP.regx);
            try writer.writeUleb128(register);
        }

        pub fn writeImplicitValue(writer: *Writer, value_bytes: []const u8) !void {
            try writer.writeByte(OP.implicit_value);
            try writer.writeUleb128(value_bytes.len);
            try writer.writeAll(value_bytes);
        }
    };
}

// Certain opcodes are not allowed in a CFA context, see 6.4.2
fn isOpcodeValidInCFA(opcode: u8) bool {
    return switch (opcode) {
        OP.addrx,
        OP.call2,
        OP.call4,
        OP.call_ref,
        OP.const_type,
        OP.constx,
        OP.convert,
        OP.deref_type,
        OP.regval_type,
        OP.reinterpret,
        OP.push_object_address,
        OP.call_frame_cfa,
        => false,
        else => true,
    };
}

fn isOpcodeRegisterLocation(opcode: u8) bool {
    return switch (opcode) {
        OP.reg0...OP.reg31, OP.regx => true,
        else => false,
    };
}

test "basics" {
    const allocator = std.testing.allocator;

    const options = Options{};
    var stack_machine = StackMachine(options){};
    defer stack_machine.deinit(allocator);

    const b = Builder(options);

    var program: std.Io.Writer.Allocating = .init(allocator);
    defer program.deinit();

    const writer = &program.writer;

    // Literals
    {
        const context = Context{};
        for (0..32) |i| {
            try b.writeLiteral(writer, @intCast(i));
        }

        _ = try stack_machine.run(program.written(), allocator, context, 0);

        for (0..32) |i| {
            const expected = 31 - i;
            try testing.expectEqual(expected, stack_machine.stack.pop().?.generic);
        }
    }

    // Constants
    {
        stack_machine.reset();
        program.clearRetainingCapacity();

        const input = [_]comptime_int{
            1,
            -1,
            @as(usize, @truncate(0x0fff)),
            @as(isize, @truncate(-0x0fff)),
            @as(usize, @truncate(0x0fffffff)),
            @as(isize, @truncate(-0x0fffffff)),
            @as(usize, @truncate(0x0fffffffffffffff)),
            @as(isize, @truncate(-0x0fffffffffffffff)),
            @as(usize, @truncate(0x8000000)),
            @as(isize, @truncate(-0x8000000)),
            @as(usize, @truncate(0x12345678_12345678)),
            @as(usize, @truncate(0xffffffff_ffffffff)),
            @as(usize, @truncate(0xeeeeeeee_eeeeeeee)),
        };

        try b.writeConst(writer, u8, input[0]);
        try b.writeConst(writer, i8, input[1]);
        try b.writeConst(writer, u16, input[2]);
        try b.writeConst(writer, i16, input[3]);
        try b.writeConst(writer, u32, input[4]);
        try b.writeConst(writer, i32, input[5]);
        try b.writeConst(writer, u64, input[6]);
        try b.writeConst(writer, i64, input[7]);
        try b.writeConst(writer, u28, input[8]);
        try b.writeConst(writer, i28, input[9]);
        try b.writeAddr(writer, input[10]);

        var mock_compile_unit: std.debug.Dwarf.CompileUnit = undefined;
        mock_compile_unit.addr_base = 1;

        var mock_debug_addr: std.Io.Writer.Allocating = .init(allocator);
        defer mock_debug_addr.deinit();

        try mock_debug_addr.writer.writeInt(u16, 0, native_endian);
        try mock_debug_addr.writer.writeInt(usize, input[11], native_endian);
        try mock_debug_addr.writer.writeInt(usize, input[12], native_endian);

        const context: Context = .{
            .compile_unit = &mock_compile_unit,
            .debug_addr = mock_debug_addr.written(),
        };

        try b.writeConstx(writer, @as(usize, 1));
        try b.writeAddrx(writer, @as(usize, 1 + @sizeOf(usize)));

        const die_offset: usize = @truncate(0xaabbccdd);
        const type_bytes: []const u8 = &.{ 1, 2, 3, 4 };
        try b.writeConstType(writer, die_offset, type_bytes);

        _ = try stack_machine.run(program.written(), allocator, context, 0);

        const const_type = stack_machine.stack.pop().?.const_type;
        try testing.expectEqual(die_offset, const_type.type_offset);
        try testing.expectEqualSlices(u8, type_bytes, const_type.value_bytes);

        const expected = .{
            .{ usize, input[12], usize },
            .{ usize, input[11], usize },
            .{ usize, input[10], usize },
            .{ isize, input[9], isize },
            .{ usize, input[8], usize },
            .{ isize, input[7], isize },
            .{ usize, input[6], usize },
            .{ isize, input[5], isize },
            .{ usize, input[4], usize },
            .{ isize, input[3], isize },
            .{ usize, input[2], usize },
            .{ isize, input[1], isize },
            .{ usize, input[0], usize },
        };

        inline for (expected) |e| {
            try testing.expectEqual(@as(e[0], e[1]), @as(e[2], @bitCast(stack_machine.stack.pop().?.generic)));
        }
    }

    // Register values
    if (std.debug.cpu_context.Native != noreturn) {
        stack_machine.reset();
        program.clearRetainingCapacity();

        var cpu_context: std.debug.cpu_context.Native = undefined;
        const context = Context{
            .cpu_context = &cpu_context,
        };

        const reg_bytes = try cpu_context.dwarfRegisterBytes(0);

        // TODO: Test fbreg (once implemented): mock a DIE and point compile_unit.frame_base at it

        mem.writeInt(usize, reg_bytes[0..@sizeOf(usize)], 0xee, native_endian);
        (try regNative(&cpu_context, fp_reg_num)).* = 1;
        (try regNative(&cpu_context, ip_reg_num)).* = 2;

        try b.writeBreg(writer, fp_reg_num, @as(usize, 100));
        try b.writeBregx(writer, ip_reg_num, @as(usize, 200));
        try b.writeRegvalType(writer, @as(u8, 0), @as(usize, 300));

        _ = try stack_machine.run(program.written(), allocator, context, 0);

        const regval_type = stack_machine.stack.pop().?.regval_type;
        try testing.expectEqual(@as(usize, 300), regval_type.type_offset);
        try testing.expectEqual(@as(u8, @sizeOf(usize)), regval_type.type_size);
        try testing.expectEqual(@as(usize, 0xee), regval_type.value);

        try testing.expectEqual(@as(usize, 202), stack_machine.stack.pop().?.generic);
        try testing.expectEqual(@as(usize, 101), stack_machine.stack.pop().?.generic);
    }

    // Stack operations
    {
        var context = Context{};

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u8, 1);
        try b.writeOpcode(writer, OP.dup);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 1), stack_machine.stack.pop().?.generic);
        try testing.expectEqual(@as(usize, 1), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u8, 1);
        try b.writeOpcode(writer, OP.drop);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expect(stack_machine.stack.pop() == null);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u8, 4);
        try b.writeConst(writer, u8, 5);
        try b.writeConst(writer, u8, 6);
        try b.writePick(writer, 2);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 4), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u8, 4);
        try b.writeConst(writer, u8, 5);
        try b.writeConst(writer, u8, 6);
        try b.writeOpcode(writer, OP.over);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 5), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u8, 5);
        try b.writeConst(writer, u8, 6);
        try b.writeOpcode(writer, OP.swap);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 5), stack_machine.stack.pop().?.generic);
        try testing.expectEqual(@as(usize, 6), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u8, 4);
        try b.writeConst(writer, u8, 5);
        try b.writeConst(writer, u8, 6);
        try b.writeOpcode(writer, OP.rot);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 5), stack_machine.stack.pop().?.generic);
        try testing.expectEqual(@as(usize, 4), stack_machine.stack.pop().?.generic);
        try testing.expectEqual(@as(usize, 6), stack_machine.stack.pop().?.generic);

        const deref_target: usize = @truncate(0xffeeffee_ffeeffee);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeAddr(writer, @intFromPtr(&deref_target));
        try b.writeOpcode(writer, OP.deref);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(deref_target, stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeLiteral(writer, 0);
        try b.writeAddr(writer, @intFromPtr(&deref_target));
        try b.writeOpcode(writer, OP.xderef);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(deref_target, stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeAddr(writer, @intFromPtr(&deref_target));
        try b.writeDerefSize(writer, 1);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, @as(*const u8, @ptrCast(&deref_target)).*), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeLiteral(writer, 0);
        try b.writeAddr(writer, @intFromPtr(&deref_target));
        try b.writeXDerefSize(writer, 1);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, @as(*const u8, @ptrCast(&deref_target)).*), stack_machine.stack.pop().?.generic);

        const type_offset: usize = @truncate(0xaabbaabb_aabbaabb);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeAddr(writer, @intFromPtr(&deref_target));
        try b.writeDerefType(writer, 1, type_offset);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        const deref_type = stack_machine.stack.pop().?.regval_type;
        try testing.expectEqual(type_offset, deref_type.type_offset);
        try testing.expectEqual(@as(u8, 1), deref_type.type_size);
        try testing.expectEqual(@as(usize, @as(*const u8, @ptrCast(&deref_target)).*), deref_type.value);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeLiteral(writer, 0);
        try b.writeAddr(writer, @intFromPtr(&deref_target));
        try b.writeXDerefType(writer, 1, type_offset);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        const xderef_type = stack_machine.stack.pop().?.regval_type;
        try testing.expectEqual(type_offset, xderef_type.type_offset);
        try testing.expectEqual(@as(u8, 1), xderef_type.type_size);
        try testing.expectEqual(@as(usize, @as(*const u8, @ptrCast(&deref_target)).*), xderef_type.value);

        context.object_address = &deref_target;

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeOpcode(writer, OP.push_object_address);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, @intFromPtr(context.object_address.?)), stack_machine.stack.pop().?.generic);

        // TODO: Test OP.form_tls_address

        context.cfa = @truncate(0xccddccdd_ccddccdd);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeOpcode(writer, OP.call_frame_cfa);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(context.cfa.?, stack_machine.stack.pop().?.generic);
    }

    // Arithmetic and Logical Operations
    {
        const context = Context{};

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, i16, -4096);
        try b.writeOpcode(writer, OP.abs);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 4096), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 0xff0f);
        try b.writeConst(writer, u16, 0xf0ff);
        try b.writeOpcode(writer, OP.@"and");
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 0xf00f), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, i16, -404);
        try b.writeConst(writer, i16, 100);
        try b.writeOpcode(writer, OP.div);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(isize, -404 / 100), @as(isize, @bitCast(stack_machine.stack.pop().?.generic)));

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 200);
        try b.writeConst(writer, u16, 50);
        try b.writeOpcode(writer, OP.minus);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 150), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 123);
        try b.writeConst(writer, u16, 100);
        try b.writeOpcode(writer, OP.mod);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 23), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 0xff);
        try b.writeConst(writer, u16, 0xee);
        try b.writeOpcode(writer, OP.mul);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 0xed12), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 5);
        try b.writeOpcode(writer, OP.neg);
        try b.writeConst(writer, i16, -6);
        try b.writeOpcode(writer, OP.neg);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 6), stack_machine.stack.pop().?.generic);
        try testing.expectEqual(@as(isize, -5), @as(isize, @bitCast(stack_machine.stack.pop().?.generic)));

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 0xff0f);
        try b.writeOpcode(writer, OP.not);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(~@as(usize, 0xff0f), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 0xff0f);
        try b.writeConst(writer, u16, 0xf0ff);
        try b.writeOpcode(writer, OP.@"or");
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 0xffff), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, i16, 402);
        try b.writeConst(writer, i16, 100);
        try b.writeOpcode(writer, OP.plus);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 502), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 4096);
        try b.writePlusUconst(writer, @as(usize, 8192));
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 4096 + 8192), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 0xfff);
        try b.writeConst(writer, u16, 1);
        try b.writeOpcode(writer, OP.shl);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 0xfff << 1), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 0xfff);
        try b.writeConst(writer, u16, 1);
        try b.writeOpcode(writer, OP.shr);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 0xfff >> 1), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 0xfff);
        try b.writeConst(writer, u16, 1);
        try b.writeOpcode(writer, OP.shr);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, @bitCast(@as(isize, 0xfff) >> 1)), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConst(writer, u16, 0xf0ff);
        try b.writeConst(writer, u16, 0xff0f);
        try b.writeOpcode(writer, OP.xor);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 0x0ff0), stack_machine.stack.pop().?.generic);
    }

    // Control Flow Operations
    {
        const context = Context{};
        const expected = .{
            .{ OP.le, 1, 1, 0 },
            .{ OP.ge, 1, 0, 1 },
            .{ OP.eq, 1, 0, 0 },
            .{ OP.lt, 0, 1, 0 },
            .{ OP.gt, 0, 0, 1 },
            .{ OP.ne, 0, 1, 1 },
        };

        inline for (expected) |e| {
            stack_machine.reset();
            program.clearRetainingCapacity();

            try b.writeConst(writer, u16, 0);
            try b.writeConst(writer, u16, 0);
            try b.writeOpcode(writer, e[0]);
            try b.writeConst(writer, u16, 0);
            try b.writeConst(writer, u16, 1);
            try b.writeOpcode(writer, e[0]);
            try b.writeConst(writer, u16, 1);
            try b.writeConst(writer, u16, 0);
            try b.writeOpcode(writer, e[0]);
            _ = try stack_machine.run(program.written(), allocator, context, null);
            try testing.expectEqual(@as(usize, e[3]), stack_machine.stack.pop().?.generic);
            try testing.expectEqual(@as(usize, e[2]), stack_machine.stack.pop().?.generic);
            try testing.expectEqual(@as(usize, e[1]), stack_machine.stack.pop().?.generic);
        }

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeLiteral(writer, 2);
        try b.writeSkip(writer, 1);
        try b.writeLiteral(writer, 3);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 2), stack_machine.stack.pop().?.generic);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeLiteral(writer, 2);
        try b.writeBra(writer, 1);
        try b.writeLiteral(writer, 3);
        try b.writeLiteral(writer, 0);
        try b.writeBra(writer, 1);
        try b.writeLiteral(writer, 4);
        try b.writeLiteral(writer, 5);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 5), stack_machine.stack.pop().?.generic);
        try testing.expectEqual(@as(usize, 4), stack_machine.stack.pop().?.generic);
        try testing.expect(stack_machine.stack.pop() == null);

        // TODO: Test call2, call4, call_ref once implemented

    }

    // Type conversions
    {
        const context = Context{};
        stack_machine.reset();
        program.clearRetainingCapacity();

        // TODO: Test typed OP.convert once implemented

        const value: usize = @truncate(0xffeeffee_ffeeffee);
        var value_bytes: [options.addr_size]u8 = undefined;
        mem.writeInt(usize, &value_bytes, value, native_endian);

        // Convert to generic type
        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConstType(writer, @as(usize, 0), &value_bytes);
        try b.writeConvert(writer, @as(usize, 0));
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(value, stack_machine.stack.pop().?.generic);

        // Reinterpret to generic type
        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConstType(writer, @as(usize, 0), &value_bytes);
        try b.writeReinterpret(writer, @as(usize, 0));
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(value, stack_machine.stack.pop().?.generic);

        // Reinterpret to new type
        const die_offset: usize = 0xffee;

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeConstType(writer, @as(usize, 0), &value_bytes);
        try b.writeReinterpret(writer, die_offset);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        const const_type = stack_machine.stack.pop().?.const_type;
        try testing.expectEqual(die_offset, const_type.type_offset);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeLiteral(writer, 0);
        try b.writeReinterpret(writer, die_offset);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        const regval_type = stack_machine.stack.pop().?.regval_type;
        try testing.expectEqual(die_offset, regval_type.type_offset);
    }

    // Special operations
    {
        var context = Context{};

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeOpcode(writer, OP.nop);
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expect(stack_machine.stack.pop() == null);

        // Sub-expression
        {
            var sub_program: std.Io.Writer.Allocating = .init(allocator);
            defer sub_program.deinit();
            const sub_writer = &sub_program.writer;
            try b.writeLiteral(sub_writer, 3);

            stack_machine.reset();
            program.clearRetainingCapacity();
            try b.writeEntryValue(writer, sub_program.written());
            _ = try stack_machine.run(program.written(), allocator, context, null);
            try testing.expectEqual(@as(usize, 3), stack_machine.stack.pop().?.generic);
        }

        // Register location description
        var cpu_context: std.debug.cpu_context.Native = undefined;
        context = .{ .cpu_context = &cpu_context };

        const reg_bytes = try cpu_context.dwarfRegisterBytes(0);
        mem.writeInt(usize, reg_bytes[0..@sizeOf(usize)], 0xee, native_endian);

        var sub_program: std.Io.Writer.Allocating = .init(allocator);
        defer sub_program.deinit();
        const sub_writer = &sub_program.writer;
        try b.writeReg(sub_writer, 0);

        stack_machine.reset();
        program.clearRetainingCapacity();
        try b.writeEntryValue(writer, sub_program.written());
        _ = try stack_machine.run(program.written(), allocator, context, null);
        try testing.expectEqual(@as(usize, 0xee), stack_machine.stack.pop().?.generic);
    }
}



---
File: /std/debug/Dwarf/SelfUnwinder.zig
---

//! Implements stack unwinding based on `Dwarf.Unwind`. The caller is responsible for providing the
//! initialized `Dwarf.Unwind` from the `.debug_frame` (or equivalent) section; this type handles
//! computing and applying the CFI register rules to evolve a `std.debug.cpu_context.Native` through
//! stack frames, hence performing the virtual unwind.
//!
//! Notably, this type is a valid implementation of `std.debug.SelfInfo.UnwindContext`.

/// The state of the CPU in the current stack frame.
cpu_state: std.debug.cpu_context.Native,
/// The value of the Program Counter in this frame. This is almost the same as the value of the IP
/// register in `cpu_state`, but may be off by one because the IP is typically a *return* address.
pc: usize,

cfi_vm: Dwarf.Unwind.VirtualMachine,
expr_vm: Dwarf.expression.StackMachine(.{ .call_frame_context = true }),

pub const CacheEntry = struct {
    const max_rules = 32;

    pc: usize,
    cie: *const Dwarf.Unwind.CommonInformationEntry,
    cfa_rule: Dwarf.Unwind.VirtualMachine.CfaRule,
    num_rules: u8,
    rules_regs: [max_rules]u16,
    rules: [max_rules]Dwarf.Unwind.VirtualMachine.RegisterRule,

    pub fn find(entries: []const CacheEntry, pc: usize) ?*const CacheEntry {
        assert(pc != 0);
        const idx = std.hash.int(pc) % entries.len;
        const entry = &entries[idx];
        return if (entry.pc == pc) entry else null;
    }

    pub fn populate(entry: *const CacheEntry, entries: []CacheEntry) void {
        const idx = std.hash.int(entry.pc) % entries.len;
        entries[idx] = entry.*;
    }

    pub const empty: CacheEntry = .{
        .pc = 0,
        .cie = undefined,
        .cfa_rule = undefined,
        .num_rules = undefined,
        .rules_regs = undefined,
        .rules = undefined,
    };
};

pub fn init(cpu_context: *const std.debug.cpu_context.Native) SelfUnwinder {
    return .{
        .cpu_state = cpu_context.*,
        .pc = stripInstructionPtrAuthCode(cpu_context.getPc()),
        .cfi_vm = .{},
        .expr_vm = .{},
    };
}

pub fn deinit(unwinder: *SelfUnwinder) void {
    const gpa = std.debug.getDebugInfoAllocator();
    unwinder.cfi_vm.deinit(gpa);
    unwinder.expr_vm.deinit(gpa);
    unwinder.* = undefined;
}

pub fn getFp(unwinder: *const SelfUnwinder) usize {
    return unwinder.cpu_state.getFp();
}

/// Compute the rule set for the address `unwinder.pc` from the information in `unwind`. The caller
/// may store the returned rule set in a simple fixed-size cache keyed on the `pc` field to avoid
/// frequently recomputing register rules when unwinding many times.
///
/// To actually apply the computed rules, see `next`.
pub fn computeRules(
    unwinder: *SelfUnwinder,
    gpa: Allocator,
    unwind: *const Dwarf.Unwind,
    load_offset: usize,
    explicit_fde_offset: ?usize,
) !CacheEntry {
    assert(unwinder.pc != 0);

    const pc_vaddr = unwinder.pc - load_offset;

    const fde_offset = explicit_fde_offset orelse try unwind.lookupPc(
        pc_vaddr,
        @sizeOf(usize),
        native_endian,
    ) orelse return error.MissingDebugInfo;
    const cie, const fde = try unwind.getFde(fde_offset, native_endian);

    // `lookupPc` can return false positives, so check if the FDE *actually* includes the pc
    if (pc_vaddr < fde.pc_begin or pc_vaddr >= fde.pc_begin + fde.pc_range) {
        return error.MissingDebugInfo;
    }

    unwinder.cfi_vm.reset();
    const row = try unwinder.cfi_vm.runTo(gpa, pc_vaddr, cie, &fde, @sizeOf(usize), native_endian);

    var entry: CacheEntry = .{
        .pc = unwinder.pc,
        .cie = cie,
        .cfa_rule = row.cfa,
        .num_rules = undefined,
        .rules_regs = undefined,
        .rules = undefined,
    };
    var i: usize = 0;
    for (unwinder.cfi_vm.rowColumns(&row)) |col| {
        if (i == CacheEntry.max_rules) return error.UnsupportedDebugInfo;

        _ = unwinder.cpu_state.dwarfRegisterBytes(col.register) catch |err| switch (err) {
            // Reading an unsupported register during unwinding will result in an error, so there is
            // no point wasting a rule slot in the cache entry for it.
            error.UnsupportedRegister => continue,
            error.InvalidRegister => return error.InvalidDebugInfo,
        };
        entry.rules_regs[i] = col.register;
        entry.rules[i] = col.rule;
        i += 1;
    }
    entry.num_rules = @intCast(i);
    return entry;
}

/// Applies the register rules given in `cache_entry` to the current state of `unwinder`. The caller
/// is responsible for ensuring that `cache_entry` contains the correct rule set for `unwinder.pc`.
///
/// `unwinder.cpu_state` and `unwinder.pc` are updated to refer to the next frame, and this frame's
/// return address is returned as a `usize`.
pub fn next(unwinder: *SelfUnwinder, gpa: Allocator, cache_entry: *const CacheEntry) std.debug.SelfInfoError!usize {
    return unwinder.nextInner(gpa, cache_entry) catch |err| switch (err) {
        error.OutOfMemory,
        error.InvalidDebugInfo,
        => |e| return e,

        error.UnsupportedRegister,
        error.UnimplementedExpressionCall,
        error.UnimplementedOpcode,
        error.UnimplementedUserOpcode,
        error.UnimplementedTypedComparison,
        error.UnimplementedTypeConversion,
        error.UnknownExpressionOpcode,
        => return error.UnsupportedDebugInfo,

        error.ReadFailed,
        error.EndOfStream,
        error.Overflow,
        error.IncompatibleRegisterSize,
        error.InvalidRegister,
        error.IncompleteExpressionContext,
        error.InvalidCFAOpcode,
        error.InvalidExpression,
        error.InvalidFrameBase,
        error.InvalidIntegralTypeSize,
        error.InvalidSubExpression,
        error.InvalidTypeLength,
        error.TruncatedIntegralType,
        error.DivisionByZero,
        => return error.InvalidDebugInfo,
    };
}

fn nextInner(unwinder: *SelfUnwinder, gpa: Allocator, cache_entry: *const CacheEntry) !usize {
    const format = cache_entry.cie.format;

    const cfa = switch (cache_entry.cfa_rule) {
        .none => return error.InvalidDebugInfo,
        .reg_off => |ro| cfa: {
            const ptr = try regNative(&unwinder.cpu_state, ro.register);
            break :cfa try applyOffset(ptr.*, ro.offset);
        },
        .expression => |expr| cfa: {
            // On most implemented architectures, the CFA is defined to be the previous frame's SP.
            //
            // On s390x, it's defined to be SP + 160 (ELF ABI s390x Supplement §1.6.3); however,
            // what this actually means is that there will be a `def_cfa r15 + 160`, so nothing
            // special for us to do.
            const prev_cfa_val = (try regNative(&unwinder.cpu_state, sp_reg_num)).*;
            unwinder.expr_vm.reset();
            const value = try unwinder.expr_vm.run(expr, gpa, .{
                .format = format,
                .cpu_context = &unwinder.cpu_state,
            }, prev_cfa_val) orelse return error.InvalidDebugInfo;
            switch (value) {
                .generic => |g| break :cfa g,
                else => return error.InvalidDebugInfo,
            }
        },
    };

    // Create a copy of the CPU state, to which we will apply the new rules.
    var new_cpu_state = unwinder.cpu_state;

    // On all implemented architectures, the CFA is defined to be the previous frame's SP
    (try regNative(&new_cpu_state, sp_reg_num)).* = cfa;

    const return_address_register = cache_entry.cie.return_address_register;
    var has_return_address = true;

    const rules_len = cache_entry.num_rules;
    for (cache_entry.rules_regs[0..rules_len], cache_entry.rules[0..rules_len]) |register, rule| {
        const new_val: union(enum) {
            same,
            undefined,
            val: usize,
            bytes: []const u8,
        } = switch (rule) {
            .default => val: {
                // The way things are supposed to work is that `.undefined` is the default rule
                // unless an ABI says otherwise (e.g. aarch64, s390x).
                //
                // Unfortunately, at some point, a decision was made to have libgcc's unwinder
                // assume `.same` as the default for all registers. Compilers then started depending
                // on this, and the practice was carried forward to LLVM's libunwind and some of its
                // backends.
                break :val .same;
            },
            .undefined => .undefined,
            .same_value => .same,
            .offset => |offset| val: {
                const ptr: *const usize = @ptrFromInt(try applyOffset(cfa, offset));
                break :val .{ .val = ptr.* };
            },
            .val_offset => |offset| .{ .val = try applyOffset(cfa, offset) },
            .register => |r| .{ .bytes = try unwinder.cpu_state.dwarfRegisterBytes(r) },
            .expression => |expr| val: {
                unwinder.expr_vm.reset();
                const value = try unwinder.expr_vm.run(expr, gpa, .{
                    .format = format,
                    .cpu_context = &unwinder.cpu_state,
                }, cfa) orelse return error.InvalidDebugInfo;
                const ptr: *const usize = switch (value) {
                    .generic => |addr| @ptrFromInt(addr),
                    else => return error.InvalidDebugInfo,
                };
                break :val .{ .val = ptr.* };
            },
            .val_expression => |expr| val: {
                unwinder.expr_vm.reset();
                const value = try unwinder.expr_vm.run(expr, gpa, .{
                    .format = format,
                    .cpu_context = &unwinder.cpu_state,
                }, cfa) orelse return error.InvalidDebugInfo;
                switch (value) {
                    .generic => |val| break :val .{ .val = val },
                    else => return error.InvalidDebugInfo,
                }
            },
        };
        switch (new_val) {
            .same => {},
            .undefined => {
                const dest = try new_cpu_state.dwarfRegisterBytes(@intCast(register));
                @memset(dest, undefined);

                // If the return address register is explicitly set to `.undefined`, it means that
                // there are no more frames to unwind.
                if (register == return_address_register) {
                    has_return_address = false;
                }
            },
            .val => |val| {
                const dest = try new_cpu_state.dwarfRegisterBytes(@intCast(register));
                if (dest.len != @sizeOf(usize)) return error.InvalidDebugInfo;
                const dest_ptr: *align(1) usize = @ptrCast(dest);
                dest_ptr.* = val;
            },
            .bytes => |src| {
                const dest = try new_cpu_state.dwarfRegisterBytes(@intCast(register));
                if (dest.len != src.len) return error.InvalidDebugInfo;
                @memcpy(dest, src);
            },
        }
    }

    const return_address = if (has_return_address)
        stripInstructionPtrAuthCode((try regNative(&new_cpu_state, return_address_register)).*)
    else
        0;

    (try regNative(&new_cpu_state, ip_reg_num)).* = return_address;

    // The new CPU state is complete; flush changes.
    unwinder.cpu_state = new_cpu_state;

    // The caller will subtract 1 from the return address to get an address corresponding to the
    // function call. However, if this is a signal frame, that's actually incorrect, because the
    // "return address" we have is the instruction which triggered the signal (if the signal
    // handler returned, the instruction would be re-run). Compensate for this by incrementing
    // the address in that case.
    const adjusted_ret_addr = if (cache_entry.cie.is_signal_frame) return_address +| 1 else return_address;

    // We also want to do that same subtraction here to get the PC for the next frame's FDE.
    // This is because if the callee was noreturn, then the function call might be the caller's
    // last instruction, so `return_address` might actually point outside of it!
    unwinder.pc = adjusted_ret_addr -| 1;

    return adjusted_ret_addr;
}

pub fn regNative(ctx: *std.debug.cpu_context.Native, num: u16) error{
    InvalidRegister,
    UnsupportedRegister,
    IncompatibleRegisterSize,
}!*align(1) usize {
    const bytes = try ctx.dwarfRegisterBytes(num);
    if (bytes.len != @sizeOf(usize)) return error.IncompatibleRegisterSize;
    return @ptrCast(bytes);
}

/// Since register rules are applied (usually) during a panic,
/// checked addition / subtraction is used so that we can return
/// an error and fall back to FP-based unwinding.
fn applyOffset(base: usize, offset: i64) !usize {
    return if (offset >= 0)
        try std.math.add(usize, base, @as(usize, @intCast(offset)))
    else
        try std.math.sub(usize, base, @as(usize, @intCast(-offset)));
}

const ip_reg_num = Dwarf.ipRegNum(builtin.target.cpu.arch).?;
const sp_reg_num = Dwarf.spRegNum(builtin.target.cpu.arch);

const std = @import("std");
const Allocator = std.mem.Allocator;
const Dwarf = std.debug.Dwarf;
const assert = std.debug.assert;
const stripInstructionPtrAuthCode = std.debug.stripInstructionPtrAuthCode;

const builtin = @import("builtin");
const native_endian = builtin.target.cpu.arch.endian();

const SelfUnwinder = @This();



---
File: /std/debug/Dwarf/Unwind.zig
---

//! Contains state relevant to stack unwinding through the DWARF `.debug_frame` section, or the
//! `.eh_frame` section which is an extension of the former specified by Linux Standard Base Core.
//! Like `Dwarf`, no assumptions are made about the host's relationship to the target of the unwind
//! information -- unwind data for any target can be read by any host.
//!
//! `Unwind` specifically deals with loading the data from CIEs and FDEs in the section, and with
//! performing fast lookups of a program counter's corresponding FDE. The CFI instructions in the
//! CIEs and FDEs can be interpreted by `VirtualMachine`.
//!
//! The typical usage of `Unwind` is as follows:
//!
//! * Initialize with `initEhFrameHdr` or `initSection`, depending on the available data
//! * Call `prepare` to scan CIEs and, if necessary, construct a search table
//! * Call `lookupPc` to find the section offset of the FDE corresponding to a PC
//! * Call `getFde` to load the corresponding FDE and CIE
//! * Check that the PC does indeed fall in that range (`lookupPc` may return a false positive)
//! * Interpret the embedded CFI instructions using `VirtualMachine`
//!
//! In some cases, such as when using the "compact unwind" data in Mach-O binaries, the FDE offsets
//! may already be known. In that case, no call to `lookupPc` is necessary, which means the call to
//! `prepare` can be optimized to only scan CIEs.

pub const VirtualMachine = @import("Unwind/VirtualMachine.zig");

frame_section: struct {
    id: Section,
    /// The virtual address of the start of the section. "Virtual address" refers to the address in
    /// the binary (e.g. `sh_addr` in an ELF file); the equivalent runtime address may be relocated
    /// in position-independent binaries.
    vaddr: u64,
    /// The full contents of the section. May have imprecise bounds depending on `section`. This
    /// memory is externally managed.
    ///
    /// For `.debug_frame`, the slice length is exactly equal to the section length. This is needed
    /// to know the number of CIEs and FDEs.
    ///
    /// For `.eh_frame`, the slice length may exceed the section length, i.e. the slice may refer to
    /// more bytes than are in the second. This restriction exists because `.eh_frame_hdr` only
    /// includes the address of the loaded `.eh_frame` data, not its length. It is not a problem
    /// because unlike `.debug_frame`, the end of the CIE/FDE list is signaled through a sentinel
    /// value. If this slice does have bounds, they will still be checked, preventing crashes when
    /// reading potentially-invalid `.eh_frame` data from files.
    bytes: []const u8,
},

/// A structure allowing fast lookups of the FDE corresponding to a particular PC. We use a binary
/// search table for the lookup; essentially, a list of all FDEs ordered by PC range. `null` means
/// the lookup data is not yet populated, so `prepare` must be called before `lookupPc`.
lookup: ?union(enum) {
    /// The `.eh_frame_hdr` section contains a pre-computed search table which we can use.
    eh_frame_hdr: struct {
        /// Virtual address of the `.eh_frame_hdr` section.
        vaddr: u64,
        table: EhFrameHeader.SearchTable,
    },
    /// There is no pre-computed search table, so we have built one ourselves.
    /// Allocated into `gpa` and freed by `deinit`.
    sorted_fdes: []SortedFdeEntry,
},

/// Initially empty; populated by `prepare`.
cie_list: std.MultiArrayList(struct {
    offset: u64,
    cie: CommonInformationEntry,
}),

const SortedFdeEntry = struct {
    /// This FDE's value of `pc_begin`.
    pc_begin: u64,
    /// Offset into the section of the corresponding FDE, including the entry header.
    fde_offset: u64,
};

pub const Section = enum { debug_frame, eh_frame };

/// Initialize with unwind information from a header loaded from an `.eh_frame_hdr` section, and a
/// pointer to the contents of the `.eh_frame` section.
///
/// `.eh_frame_hdr` may embed a binary search table of FDEs. If it does, we will use that table for
/// PC lookups rather than spending time constructing our own search table.
pub fn initEhFrameHdr(header: EhFrameHeader, section_vaddr: u64, section_bytes_ptr: [*]const u8) Unwind {
    return .{
        .frame_section = .{
            .id = .eh_frame,
            .bytes = maxSlice(section_bytes_ptr),
            .vaddr = header.eh_frame_vaddr,
        },
        .lookup = if (header.search_table) |table| .{ .eh_frame_hdr = .{
            .vaddr = section_vaddr,
            .table = table,
        } } else null,
        .cie_list = .empty,
    };
}

/// Initialize with unwind information from the contents of a `.debug_frame` or `.eh_frame` section.
///
/// If the `.eh_frame_hdr` section is available, consider instead using `initEhFrameHdr`, which
/// allows the implementation to use a search table embedded in that section if it is available.
pub fn initSection(section: Section, section_vaddr: u64, section_bytes: []const u8) Unwind {
    return .{
        .frame_section = .{
            .id = section,
            .bytes = section_bytes,
            .vaddr = section_vaddr,
        },
        .lookup = null,
        .cie_list = .empty,
    };
}

pub fn deinit(unwind: *Unwind, gpa: Allocator) void {
    if (unwind.lookup) |lookup| switch (lookup) {
        .eh_frame_hdr => {},
        .sorted_fdes => |fdes| gpa.free(fdes),
    };
    for (unwind.cie_list.items(.cie)) |*cie| {
        if (cie.last_row) |*lr| {
            gpa.free(lr.cols);
        }
    }
    unwind.cie_list.deinit(gpa);
}

/// Decoded version of the `.eh_frame_hdr` section.
pub const EhFrameHeader = struct {
    /// The virtual address (i.e. as given in the binary, before relocations) of the `.eh_frame`
    /// section. This value is important when using `.eh_frame_hdr` to find debug information for
    /// the current binary, because it allows locating where the `.eh_frame` section is loaded in
    /// memory (by adding it to the ELF module's base address).
    eh_frame_vaddr: u64,
    search_table: ?SearchTable,

    pub const SearchTable = struct {
        /// The byte offset of the search table into the `.eh_frame_hdr` section.
        offset: u8,
        encoding: EH.PE,
        fde_count: usize,
        /// The actual table entries are viewed as a plain byte slice because `encoding` causes the
        /// size of entries in the table to vary.
        entries: []const u8,

        /// Returns the vaddr of the FDE for `pc`, or `null` if no matching FDE was found.
        fn findEntry(
            table: *const SearchTable,
            eh_frame_hdr_vaddr: u64,
            pc: u64,
            addr_size_bytes: u8,
            endian: Endian,
        ) !?u64 {
            const table_vaddr = eh_frame_hdr_vaddr + table.offset;
            const entry_size = try entrySize(table.encoding, addr_size_bytes);
            var left: usize = 0;
            var len: usize = table.fde_count;
            while (len > 1) {
                const mid = left + len / 2;
                var entry_reader: Reader = .fixed(table.entries[mid * entry_size ..][0..entry_size]);
                const pc_begin = try readEhPointer(&entry_reader, table.encoding, addr_size_bytes, .{
                    .pc_rel_base = table_vaddr + left * entry_size,
                    .data_rel_base = eh_frame_hdr_vaddr,
                }, endian);
                if (pc < pc_begin) {
                    len /= 2;
                } else {
                    left = mid;
                    len -= len / 2;
                }
            }
            if (len == 0) return null;
            var entry_reader: Reader = .fixed(table.entries[left * entry_size ..][0..entry_size]);
            // Skip past `pc_begin`; we're now interested in the fde offset
            _ = try readEhPointerAbs(&entry_reader, table.encoding.type, addr_size_bytes, endian);
            const fde_ptr = try readEhPointer(&entry_reader, table.encoding, addr_size_bytes, .{
                .pc_rel_base = table_vaddr + left * entry_size,
                .data_rel_base = eh_frame_hdr_vaddr,
            }, endian);
            return fde_ptr;
        }

        fn entrySize(table_enc: EH.PE, addr_size_bytes: u8) !u8 {
            return switch (table_enc.type) {
                .absptr => 2 * addr_size_bytes,
                .udata2, .sdata2 => 4,
                .udata4, .sdata4 => 8,
                .udata8, .sdata8 => 16,
                .uleb128, .sleb128 => return bad(), // this is a binary search table; all entries must be the same size
                _ => return bad(),
            };
        }
    };

    pub fn parse(
        eh_frame_hdr_vaddr: u64,
        eh_frame_hdr_bytes: []const u8,
        addr_size_bytes: u8,
        endian: Endian,
    ) !EhFrameHeader {
        var r: Reader = .fixed(eh_frame_hdr_bytes);

        const version = try r.takeByte();
        if (version != 1) return bad();

        const eh_frame_ptr_enc: EH.PE = @bitCast(try r.takeByte());
        const fde_count_enc: EH.PE = @bitCast(try r.takeByte());
        const table_enc: EH.PE = @bitCast(try r.takeByte());

        const eh_frame_ptr = try readEhPointer(&r, eh_frame_ptr_enc, addr_size_bytes, .{
            .pc_rel_base = eh_frame_hdr_vaddr + r.seek,
        }, endian);

        const table: ?SearchTable = table: {
            if (fde_count_enc == EH.PE.omit) break :table null;
            if (table_enc == EH.PE.omit) break :table null;
            const fde_count = try readEhPointer(&r, fde_count_enc, addr_size_bytes, .{
                .pc_rel_base = eh_frame_hdr_vaddr + r.seek,
            }, endian);
            const entry_size = try SearchTable.entrySize(table_enc, addr_size_bytes);
            const bytes_offset = r.seek;
            const bytes_len = cast(usize, fde_count * entry_size) orelse return error.EndOfStream;
            const bytes = try r.take(bytes_len);
            break :table .{
                .encoding = table_enc,
                .fde_count = @intCast(fde_count),
                .entries = bytes,
                .offset = @intCast(bytes_offset),
            };
        };

        return .{
            .eh_frame_vaddr = eh_frame_ptr,
            .search_table = table,
        };
    }
};

/// The shared header of an FDE/CIE, containing a length in bytes (DWARF's "initial length field")
/// and a value which differentiates CIEs from FDEs and maps FDEs to their corresponding CIEs. The
/// `.eh_frame` format also includes a third variation, here called `.terminator`, which acts as a
/// sentinel for the whole section.
///
/// `CommonInformationEntry.parse` and `FrameDescriptionEntry.parse` expect the `EntryHeader` to
/// have been parsed first: they accept data stored in the `EntryHeader`, and only read the bytes
/// following this header.
const EntryHeader = union(enum) {
    cie: struct {
        format: Format,
        /// Remaining bytes in the CIE. These are parseable by `CommonInformationEntry.parse`.
        bytes_len: u64,
    },
    fde: struct {
        /// Offset into the section of the corresponding CIE, *including* its entry header.
        cie_offset: u64,
        /// Remaining bytes in the FDE. These are parseable by `FrameDescriptionEntry.parse`.
        bytes_len: u64,
    },
    /// The `.eh_frame` format includes terminators which indicate that the last CIE/FDE has been
    /// reached. However, `.debug_frame` does not include such a terminator, so the caller must
    /// keep track of how many section bytes remain when parsing all entries in `.debug_frame`.
    terminator,

    fn read(r: *Reader, header_section_offset: u64, section: Section, endian: Endian) !EntryHeader {
        const unit_header = try Dwarf.readUnitHeader(r, endian);
        if (unit_header.unit_length == 0) return .terminator;

        // Next is a value which will disambiguate CIEs and FDEs. Annoyingly, LSB Core makes this
        // value always 4-byte, whereas DWARF makes it depend on the `dwarf.Format`.
        const cie_ptr_or_id_size: u8 = switch (section) {
            .eh_frame => 4,
            .debug_frame => switch (unit_header.format) {
                .@"32" => 4,
                .@"64" => 8,
            },
        };
        const cie_ptr_or_id = switch (cie_ptr_or_id_size) {
            4 => try r.takeInt(u32, endian),
            8 => try r.takeInt(u64, endian),
            else => unreachable,
        };
        const remaining_bytes = unit_header.unit_length - cie_ptr_or_id_size;

        // If this entry is a CIE, then `cie_ptr_or_id` will have this value, which is different
        // between the DWARF `.debug_frame` section and the LSB Core `.eh_frame` section.
        const cie_id: u64 = switch (section) {
            .eh_frame => 0,
            .debug_frame => switch (unit_header.format) {
                .@"32" => maxInt(u32),
                .@"64" => maxInt(u64),
            },
        };
        if (cie_ptr_or_id == cie_id) {
            return .{ .cie = .{
                .format = unit_header.format,
                .bytes_len = remaining_bytes,
            } };
        }

        // This is an FDE -- `cie_ptr_or_id` points to the associated CIE. Unfortunately, the format
        // of that pointer again differs between `.debug_frame` and `.eh_frame`.
        const cie_offset = switch (section) {
            .eh_frame => try std.math.sub(u64, header_section_offset + unit_header.header_length, cie_ptr_or_id),
            .debug_frame => cie_ptr_or_id,
        };
        return .{ .fde = .{
            .cie_offset = cie_offset,
            .bytes_len = remaining_bytes,
        } };
    }
};

pub const CommonInformationEntry = struct {
    version: u8,
    format: Format,

    /// In version 4, CIEs can specify the address size used in the CIE and associated FDEs.
    /// This value must be used *only* to parse associated FDEs in `FrameDescriptionEntry.parse`.
    addr_size_bytes: u8,

    /// Always 0 for versions which do not specify this (currently all versions other than 4).
    segment_selector_size: u8,

    code_alignment_factor: u32,
    data_alignment_factor: i32,
    return_address_register: u8,

    fde_pointer_enc: EH.PE,
    is_signal_frame: bool,

    augmentation_kind: AugmentationKind,

    initial_instructions: []const u8,

    last_row: ?struct {
        offset: u64,
        cfa: VirtualMachine.CfaRule,
        cols: []VirtualMachine.Column,
    },

    pub const AugmentationKind = enum { none, gcc_eh, lsb_z };

    /// This function expects to read the CIE starting with the version field.
    /// The returned struct references memory backed by `cie_bytes`.
    ///
    /// `length_offset` specifies the offset of this CIE's length field in the
    /// .eh_frame / .debug_frame section.
    fn parse(
        format: Format,
        cie_bytes: []const u8,
        section: Section,
        default_addr_size_bytes: u8,
    ) !CommonInformationEntry {
        // We only read the data through this reader.
        var r: Reader = .fixed(cie_bytes);

        const version = try r.takeByte();
        switch (section) {
            .eh_frame => if (version != 1 and version != 3) return error.UnsupportedDwarfVersion,
            .debug_frame => if (version != 4) return error.UnsupportedDwarfVersion,
        }

        const aug_str = try r.takeSentinel(0);
        const aug_kind: AugmentationKind = aug: {
            if (aug_str.len == 0) break :aug .none;
            if (aug_str[0] == 'z') break :aug .lsb_z;
            if (std.mem.eql(u8, aug_str, "eh")) break :aug .gcc_eh;
            // We can't finish parsing the CIE if we don't know what its augmentation means.
            return bad();
        };

        switch (aug_kind) {
            .none => {}, // no extra data
            .lsb_z => {}, // no extra data yet, but there is a bit later
            .gcc_eh => try r.discardAll(default_addr_size_bytes), // unsupported data
        }

        const addr_size_bytes = if (version == 4) try r.takeByte() else default_addr_size_bytes;
        const segment_selector_size: u8 = if (version == 4) try r.takeByte() else 0;
        const code_alignment_factor = try r.takeLeb128(u32);
        const data_alignment_factor = try r.takeLeb128(i32);
        const return_address_register = if (version == 1) try r.takeByte() else try r.takeLeb128(u8);

        // This is where LSB's augmentation might add some data.
        const fde_pointer_enc: EH.PE, const is_signal_frame: bool = aug: {
            const default_fde_pointer_enc: EH.PE = .{ .type = .absptr, .rel = .abs };
            if (aug_kind != .lsb_z) break :aug .{ default_fde_pointer_enc, false };
            const aug_data_len = try r.takeLeb128(u32);
            var aug_data: Reader = .fixed(try r.take(aug_data_len));
            var fde_pointer_enc: EH.PE = default_fde_pointer_enc;
            var is_signal_frame = false;
            for (aug_str[1..]) |byte| switch (byte) {
                'L' => _ = try aug_data.takeByte(), // we ignore the LSDA pointer
                'P' => {
                    const enc: EH.PE = @bitCast(try aug_data.takeByte());
                    const endian: Endian = .little; // irrelevant because we're discarding the value anyway
                    _ = try readEhPointerAbs(&aug_data, enc.type, addr_size_bytes, endian); // we ignore the personality routine; endianness is irrelevant since we're discarding
                },
                'R' => fde_pointer_enc = @bitCast(try aug_data.takeByte()),
                'S' => is_signal_frame = true,
                'B', 'G' => {},
                else => return bad(),
            };
            break :aug .{ fde_pointer_enc, is_signal_frame };
        };

        return .{
            .format = format,
            .version = version,
            .addr_size_bytes = addr_size_bytes,
            .segment_selector_size = segment_selector_size,
            .code_alignment_factor = code_alignment_factor,
            .data_alignment_factor = data_alignment_factor,
            .return_address_register = return_address_register,
            .fde_pointer_enc = fde_pointer_enc,
            .is_signal_frame = is_signal_frame,
            .augmentation_kind = aug_kind,
            .initial_instructions = r.buffered(),
            .last_row = null,
        };
    }
};

pub const FrameDescriptionEntry = struct {
    pc_begin: u64,
    pc_range: u64,
    instructions: []const u8,

    /// This function expects to read the FDE starting at the PC Begin field.
    /// The returned struct references memory backed by `fde_bytes`.
    fn parse(
        /// The virtual address of the FDE we're parsing, *excluding* its entry header (i.e. the
        /// address is after the header). If `fde_bytes` is backed by the memory of a loaded
        /// module's `.eh_frame` section, this will equal `fde_bytes.ptr`.
        fde_vaddr: u64,
        fde_bytes: []const u8,
        cie: *const CommonInformationEntry,
        endian: Endian,
    ) !FrameDescriptionEntry {
        if (cie.segment_selector_size != 0) return error.UnsupportedAddrSize;

        var r: Reader = .fixed(fde_bytes);

        const pc_begin = try readEhPointer(&r, cie.fde_pointer_enc, cie.addr_size_bytes, .{
            .pc_rel_base = fde_vaddr,
        }, endian);

        // I swear I'm not kidding when I say that PC Range is encoded with `cie.fde_pointer_enc`, but ignoring `rel`.
        const pc_range = switch (try readEhPointerAbs(&r, cie.fde_pointer_enc.type, cie.addr_size_bytes, endian)) {
            .unsigned => |x| x,
            .signed => |x| cast(u64, x) orelse return bad(),
        };

        switch (cie.augmentation_kind) {
            .none, .gcc_eh => {},
            .lsb_z => {
                // There is augmentation data, but it's irrelevant to us -- it
                // only contains the LSDA pointer, which we don't care about.
                const aug_data_len = try r.takeLeb128(usize);
                _ = try r.discardAll(aug_data_len);
            },
        }

        return .{
            .pc_begin = pc_begin,
            .pc_range = pc_range,
            .instructions = r.buffered(),
        };
    }
};

/// Builds the CIE list and FDE lookup table if they are not already built. It is required to call
/// this function at least once before calling `lookupPc` or `getFde`. If only `getFde` is needed,
/// then `need_lookup` can be set to `false` to make this function more efficient.
pub fn prepare(
    unwind: *Unwind,
    gpa: Allocator,
    addr_size_bytes: u8,
    endian: Endian,
    need_lookup: bool,
    /// The `__eh_frame` section in Mach-O binaries deviates from the standard `.eh_frame` section
    /// in one way which this function needs to be aware of.
    is_macho: bool,
) !void {
    if (unwind.cie_list.len > 0 and (!need_lookup or unwind.lookup != null)) return;
    unwind.cie_list.clearRetainingCapacity();

    if (is_macho) assert(unwind.lookup == null or unwind.lookup.? != .eh_frame_hdr);

    const section = unwind.frame_section;

    var r: Reader = .fixed(section.bytes);
    var fde_list: std.ArrayList(SortedFdeEntry) = .empty;
    defer fde_list.deinit(gpa);

    const saw_terminator = while (r.seek < r.buffer.len) {
        const entry_offset = r.seek;
        switch (try EntryHeader.read(&r, entry_offset, section.id, endian)) {
            .cie => |cie_info| {
                // We will pre-populate a list of CIEs for efficiency: this avoids work re-parsing
                // them every time we look up an FDE. It also lets us cache the result of evaluating
                // the CIE's initial CFI instructions, which is useful because in the vast majority
                // of cases those instructions will be needed to reach the PC we are unwinding to.
                const bytes_len = cast(usize, cie_info.bytes_len) orelse return error.EndOfStream;
                const idx = unwind.cie_list.len;
                try unwind.cie_list.append(gpa, .{
                    .offset = entry_offset,
                    .cie = try .parse(cie_info.format, try r.take(bytes_len), section.id, addr_size_bytes),
                });
                errdefer _ = unwind.cie_list.pop().?;
                try VirtualMachine.populateCieLastRow(gpa, &unwind.cie_list.items(.cie)[idx], addr_size_bytes, endian);
                continue;
            },
            .fde => |fde_info| {
                const bytes_len = cast(usize, fde_info.bytes_len) orelse return error.EndOfStream;
                if (!need_lookup) {
                    try r.discardAll(bytes_len);
                    continue;
                }
                const cie = unwind.findCie(fde_info.cie_offset) orelse return error.InvalidDebugInfo;
                const fde: FrameDescriptionEntry = try .parse(section.vaddr + r.seek, try r.take(bytes_len), cie, endian);
                try fde_list.append(gpa, .{
                    .pc_begin = fde.pc_begin,
                    .fde_offset = entry_offset,
                });
            },
            .terminator => break true,
        }
    } else false;
    const expect_terminator = switch (section.id) {
        .eh_frame => !is_macho, // `.eh_frame` indicates the end of the CIE/FDE list with a sentinel entry, though macOS omits this
        .debug_frame => false, // `.debug_frame` uses the section bounds and does not specify a sentinel entry
    };
    if (saw_terminator != expect_terminator) return bad();

    if (need_lookup) {
        std.mem.sortUnstable(SortedFdeEntry, fde_list.items, {}, struct {
            fn lessThan(ctx: void, a: SortedFdeEntry, b: SortedFdeEntry) bool {
                ctx;
                return a.pc_begin < b.pc_begin;
            }
        }.lessThan);

        // This temporary is necessary to avoid an RLS footgun where `lookup` ends up non-null `undefined` on OOM.
        const final_fdes = try fde_list.toOwnedSlice(gpa);
        unwind.lookup = .{ .sorted_fdes = final_fdes };
    }
}

fn findCie(unwind: *const Unwind, offset: u64) ?*const CommonInformationEntry {
    const offsets = unwind.cie_list.items(.offset);
    if (offsets.len == 0) return null;
    var start: usize = 0;
    var len: usize = offsets.len;
    while (len > 1) {
        const mid = len / 2;
        if (offset < offsets[start + mid]) {
            len = mid;
        } else {
            start += mid;
            len -= mid;
        }
    }
    if (offsets[start] != offset) return null;
    return &unwind.cie_list.items(.cie)[start];
}

/// Given a program counter value, returns the offset of the corresponding FDE, or `null` if no
/// matching FDE was found. The returned offset can be passed to `getFde` to load the data
/// associated with the FDE.
///
/// Before calling this function, `prepare` must return successfully at least once, to ensure that
/// `unwind.lookup` is populated.
///
/// The return value may be a false positive. After loading the FDE with `loadFde`, the c
```
