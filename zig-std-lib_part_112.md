```
 (!self.strip) {
                self.names.appendAssumeCapacity(.empty); // TODO: param names
            }
        }

        return self;
    }

    pub fn arg(self: *const WipFunction, index: u32) Value {
        const argument = self.instructions.get(index);
        assert(argument.tag == .arg);
        assert(argument.data == index);

        const argument_index: Instruction.Index = @enumFromInt(index);
        return argument_index.toValue();
    }

    pub fn block(self: *WipFunction, incoming: u32, name: []const u8) Allocator.Error!Block.Index {
        try self.blocks.ensureUnusedCapacity(self.builder.gpa, 1);

        const index: Block.Index = @enumFromInt(self.blocks.items.len);
        const final_name = if (self.strip) .empty else try self.builder.string(name);
        self.blocks.appendAssumeCapacity(.{
            .name = final_name,
            .incoming = incoming,
            .instructions = .empty,
        });
        return index;
    }

    pub fn ret(self: *WipFunction, val: Value) Allocator.Error!Instruction.Index {
        assert(val.typeOfWip(self) == self.function.typeOf(self.builder).functionReturn(self.builder));
        try self.ensureUnusedExtraCapacity(1, NoExtra, 0);
        return try self.addInst(null, .{ .tag = .ret, .data = @intFromEnum(val) });
    }

    pub fn retVoid(self: *WipFunction) Allocator.Error!Instruction.Index {
        try self.ensureUnusedExtraCapacity(1, NoExtra, 0);
        return try self.addInst(null, .{ .tag = .@"ret void", .data = undefined });
    }

    pub fn br(self: *WipFunction, dest: Block.Index) Allocator.Error!Instruction.Index {
        try self.ensureUnusedExtraCapacity(1, NoExtra, 0);
        const instruction = try self.addInst(null, .{ .tag = .br, .data = @intFromEnum(dest) });
        dest.ptr(self).branches += 1;
        return instruction;
    }

    pub fn brCond(
        self: *WipFunction,
        cond: Value,
        then: Block.Index,
        @"else": Block.Index,
        weights: enum { none, unpredictable, then_likely, else_likely },
    ) Allocator.Error!Instruction.Index {
        assert(cond.typeOfWip(self) == .i1);
        try self.ensureUnusedExtraCapacity(1, Instruction.BrCond, 0);
        const instruction = try self.addInst(null, .{
            .tag = .br_cond,
            .data = self.addExtraAssumeCapacity(Instruction.BrCond{
                .cond = cond,
                .then = then,
                .@"else" = @"else",
                .weights = weights: switch (weights) {
                    .none => .none,
                    .unpredictable => .unpredictable,
                    .then_likely, .else_likely => {
                        const branch_weights_str = try self.builder.metadataString("branch_weights");
                        const unlikely_const = try self.builder.metadataConstant(try self.builder.intConst(.i32, 1));
                        const likely_const = try self.builder.metadataConstant(try self.builder.intConst(.i32, 2000));
                        const weight_vals: [3]Metadata = switch (weights) {
                            .none, .unpredictable => unreachable,
                            .then_likely => .{ branch_weights_str.toMetadata(), likely_const, unlikely_const },
                            .else_likely => .{ branch_weights_str.toMetadata(), unlikely_const, likely_const },
                        };
                        break :weights .fromMetadata(try self.builder.metadataTuple(&weight_vals));
                    },
                },
            }),
        });
        then.ptr(self).branches += 1;
        @"else".ptr(self).branches += 1;
        return instruction;
    }

    pub const WipSwitch = struct {
        index: u32,
        instruction: Instruction.Index,

        pub fn addCase(
            self: *WipSwitch,
            val: Constant,
            dest: Block.Index,
            wip: *WipFunction,
        ) Allocator.Error!void {
            const instruction = wip.instructions.get(@intFromEnum(self.instruction));
            var extra = wip.extraDataTrail(Instruction.Switch, instruction.data);
            assert(val.typeOf(wip.builder) == extra.data.val.typeOfWip(wip));
            extra.trail.nextMut(extra.data.cases_len, Constant, wip)[self.index] = val;
            extra.trail.nextMut(extra.data.cases_len, Block.Index, wip)[self.index] = dest;
            self.index += 1;
            dest.ptr(wip).branches += 1;
        }

        pub fn finish(self: WipSwitch, wip: *WipFunction) void {
            const instruction = wip.instructions.get(@intFromEnum(self.instruction));
            const extra = wip.extraData(Instruction.Switch, instruction.data);
            assert(self.index == extra.cases_len);
        }
    };

    pub fn @"switch"(
        self: *WipFunction,
        val: Value,
        default: Block.Index,
        cases_len: u32,
        weights: Instruction.BrCond.Weights,
    ) Allocator.Error!WipSwitch {
        try self.ensureUnusedExtraCapacity(1, Instruction.Switch, cases_len * 2);
        const instruction = try self.addInst(null, .{
            .tag = .@"switch",
            .data = self.addExtraAssumeCapacity(Instruction.Switch{
                .val = val,
                .default = default,
                .cases_len = cases_len,
                .weights = weights,
            }),
        });
        _ = self.extra.addManyAsSliceAssumeCapacity(cases_len * 2);
        default.ptr(self).branches += 1;
        return .{ .index = 0, .instruction = instruction };
    }

    pub fn indirectbr(
        self: *WipFunction,
        addr: Value,
        targets: []const Block.Index,
    ) Allocator.Error!Instruction.Index {
        try self.ensureUnusedExtraCapacity(1, Instruction.IndirectBr, targets.len);
        const instruction = try self.addInst(null, .{
            .tag = .indirectbr,
            .data = self.addExtraAssumeCapacity(Instruction.IndirectBr{
                .addr = addr,
                .targets_len = @intCast(targets.len),
            }),
        });
        _ = self.extra.appendSliceAssumeCapacity(@ptrCast(targets));
        for (targets) |target| target.ptr(self).branches += 1;
        return instruction;
    }

    pub fn @"unreachable"(self: *WipFunction) Allocator.Error!Instruction.Index {
        try self.ensureUnusedExtraCapacity(1, NoExtra, 0);
        return try self.addInst(null, .{ .tag = .@"unreachable", .data = undefined });
    }

    pub fn un(
        self: *WipFunction,
        tag: Instruction.Tag,
        val: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        switch (tag) {
            .fneg,
            .@"fneg fast",
            => assert(val.typeOfWip(self).scalarType(self.builder).isFloatingPoint()),
            else => unreachable,
        }
        try self.ensureUnusedExtraCapacity(1, NoExtra, 0);
        const instruction = try self.addInst(name, .{ .tag = tag, .data = @intFromEnum(val) });
        return instruction.toValue();
    }

    pub fn not(self: *WipFunction, val: Value, name: []const u8) Allocator.Error!Value {
        const ty = val.typeOfWip(self);
        const all_ones = try self.builder.splatValue(
            ty,
            try self.builder.intConst(ty.scalarType(self.builder), -1),
        );
        return self.bin(.xor, val, all_ones, name);
    }

    pub fn neg(self: *WipFunction, val: Value, name: []const u8) Allocator.Error!Value {
        return self.bin(.sub, try self.builder.zeroInitValue(val.typeOfWip(self)), val, name);
    }

    pub fn bin(
        self: *WipFunction,
        tag: Instruction.Tag,
        lhs: Value,
        rhs: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        switch (tag) {
            .add,
            .@"add nsw",
            .@"add nuw",
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
            .@"or",
            .sdiv,
            .@"sdiv exact",
            .shl,
            .@"shl nsw",
            .@"shl nuw",
            .srem,
            .sub,
            .@"sub nsw",
            .@"sub nuw",
            .udiv,
            .@"udiv exact",
            .urem,
            .xor,
            => assert(lhs.typeOfWip(self) == rhs.typeOfWip(self)),
            else => unreachable,
        }
        try self.ensureUnusedExtraCapacity(1, Instruction.Binary, 0);
        const instruction = try self.addInst(name, .{
            .tag = tag,
            .data = self.addExtraAssumeCapacity(Instruction.Binary{ .lhs = lhs, .rhs = rhs }),
        });
        return instruction.toValue();
    }

    pub fn extractElement(
        self: *WipFunction,
        val: Value,
        index: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(val.typeOfWip(self).isVector(self.builder));
        assert(index.typeOfWip(self).isInteger(self.builder));
        try self.ensureUnusedExtraCapacity(1, Instruction.ExtractElement, 0);
        const instruction = try self.addInst(name, .{
            .tag = .extractelement,
            .data = self.addExtraAssumeCapacity(Instruction.ExtractElement{
                .val = val,
                .index = index,
            }),
        });
        return instruction.toValue();
    }

    pub fn insertElement(
        self: *WipFunction,
        val: Value,
        elem: Value,
        index: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(val.typeOfWip(self).scalarType(self.builder) == elem.typeOfWip(self));
        assert(index.typeOfWip(self).isInteger(self.builder));
        try self.ensureUnusedExtraCapacity(1, Instruction.InsertElement, 0);
        const instruction = try self.addInst(name, .{
            .tag = .insertelement,
            .data = self.addExtraAssumeCapacity(Instruction.InsertElement{
                .val = val,
                .elem = elem,
                .index = index,
            }),
        });
        return instruction.toValue();
    }

    pub fn shuffleVector(
        self: *WipFunction,
        lhs: Value,
        rhs: Value,
        mask: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(lhs.typeOfWip(self).isVector(self.builder));
        assert(lhs.typeOfWip(self) == rhs.typeOfWip(self));
        assert(mask.typeOfWip(self).scalarType(self.builder).isInteger(self.builder));
        _ = try self.ensureUnusedExtraCapacity(1, Instruction.ShuffleVector, 0);
        const instruction = try self.addInst(name, .{
            .tag = .shufflevector,
            .data = self.addExtraAssumeCapacity(Instruction.ShuffleVector{
                .lhs = lhs,
                .rhs = rhs,
                .mask = mask,
            }),
        });
        return instruction.toValue();
    }

    pub fn splatVector(
        self: *WipFunction,
        ty: Type,
        elem: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        const scalar_ty = try ty.changeLength(1, self.builder);
        const mask_ty = try ty.changeScalar(.i32, self.builder);
        const poison = try self.builder.poisonValue(scalar_ty);
        const mask = try self.builder.splatValue(mask_ty, .@"0");
        const scalar = try self.insertElement(poison, elem, .@"0", name);
        return self.shuffleVector(scalar, poison, mask, name);
    }

    pub fn extractValue(
        self: *WipFunction,
        val: Value,
        indices: []const u32,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(indices.len > 0);
        _ = val.typeOfWip(self).childTypeAt(indices, self.builder);
        try self.ensureUnusedExtraCapacity(1, Instruction.ExtractValue, indices.len);
        const instruction = try self.addInst(name, .{
            .tag = .extractvalue,
            .data = self.addExtraAssumeCapacity(Instruction.ExtractValue{
                .val = val,
                .indices_len = @intCast(indices.len),
            }),
        });
        self.extra.appendSliceAssumeCapacity(indices);
        return instruction.toValue();
    }

    pub fn insertValue(
        self: *WipFunction,
        val: Value,
        elem: Value,
        indices: []const u32,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(indices.len > 0);
        assert(val.typeOfWip(self).childTypeAt(indices, self.builder) == elem.typeOfWip(self));
        try self.ensureUnusedExtraCapacity(1, Instruction.InsertValue, indices.len);
        const instruction = try self.addInst(name, .{
            .tag = .insertvalue,
            .data = self.addExtraAssumeCapacity(Instruction.InsertValue{
                .val = val,
                .elem = elem,
                .indices_len = @intCast(indices.len),
            }),
        });
        self.extra.appendSliceAssumeCapacity(indices);
        return instruction.toValue();
    }

    pub fn buildAggregate(
        self: *WipFunction,
        ty: Type,
        elems: []const Value,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(ty.aggregateLen(self.builder) == elems.len);
        var cur = try self.builder.poisonValue(ty);
        for (elems, 0..) |elem, index|
            cur = try self.insertValue(cur, elem, &[_]u32{@intCast(index)}, name);
        return cur;
    }

    pub fn alloca(
        self: *WipFunction,
        kind: Instruction.Alloca.Kind,
        ty: Type,
        len: Value,
        alignment: Alignment,
        addr_space: AddrSpace,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(len == .none or len.typeOfWip(self).isInteger(self.builder));
        _ = try self.builder.ptrType(addr_space);
        try self.ensureUnusedExtraCapacity(1, Instruction.Alloca, 0);
        const instruction = try self.addInst(name, .{
            .tag = switch (kind) {
                .normal => .alloca,
                .inalloca => .@"alloca inalloca",
            },
            .data = self.addExtraAssumeCapacity(Instruction.Alloca{
                .type = ty,
                .len = switch (len) {
                    .none => .@"1",
                    else => len,
                },
                .info = .{ .alignment = alignment, .addr_space = addr_space },
            }),
        });
        return instruction.toValue();
    }

    pub fn load(
        self: *WipFunction,
        access_kind: MemoryAccessKind,
        ty: Type,
        ptr: Value,
        alignment: Alignment,
        name: []const u8,
    ) Allocator.Error!Value {
        return self.loadAtomic(access_kind, ty, ptr, .system, .none, alignment, name);
    }

    pub fn loadAtomic(
        self: *WipFunction,
        access_kind: MemoryAccessKind,
        ty: Type,
        ptr: Value,
        sync_scope: SyncScope,
        ordering: AtomicOrdering,
        alignment: Alignment,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(ptr.typeOfWip(self).isPointer(self.builder));
        try self.ensureUnusedExtraCapacity(1, Instruction.Load, 0);
        const instruction = try self.addInst(name, .{
            .tag = switch (ordering) {
                .none => .load,
                else => .@"load atomic",
            },
            .data = self.addExtraAssumeCapacity(Instruction.Load{
                .info = .{
                    .access_kind = access_kind,
                    .sync_scope = switch (ordering) {
                        .none => .system,
                        else => sync_scope,
                    },
                    .success_ordering = ordering,
                    .alignment = alignment,
                },
                .type = ty,
                .ptr = ptr,
            }),
        });
        return instruction.toValue();
    }

    pub fn store(
        self: *WipFunction,
        kind: MemoryAccessKind,
        val: Value,
        ptr: Value,
        alignment: Alignment,
    ) Allocator.Error!Instruction.Index {
        return self.storeAtomic(kind, val, ptr, .system, .none, alignment);
    }

    pub fn storeAtomic(
        self: *WipFunction,
        access_kind: MemoryAccessKind,
        val: Value,
        ptr: Value,
        sync_scope: SyncScope,
        ordering: AtomicOrdering,
        alignment: Alignment,
    ) Allocator.Error!Instruction.Index {
        assert(ptr.typeOfWip(self).isPointer(self.builder));
        try self.ensureUnusedExtraCapacity(1, Instruction.Store, 0);
        const instruction = try self.addInst(null, .{
            .tag = switch (ordering) {
                .none => .store,
                else => .@"store atomic",
            },
            .data = self.addExtraAssumeCapacity(Instruction.Store{
                .info = .{
                    .access_kind = access_kind,
                    .sync_scope = switch (ordering) {
                        .none => .system,
                        else => sync_scope,
                    },
                    .success_ordering = ordering,
                    .alignment = alignment,
                },
                .val = val,
                .ptr = ptr,
            }),
        });
        return instruction;
    }

    pub fn fence(
        self: *WipFunction,
        sync_scope: SyncScope,
        ordering: AtomicOrdering,
    ) Allocator.Error!Instruction.Index {
        assert(ordering != .none);
        try self.ensureUnusedExtraCapacity(1, NoExtra, 0);
        const instruction = try self.addInst(null, .{
            .tag = .fence,
            .data = @bitCast(MemoryAccessInfo{
                .sync_scope = sync_scope,
                .success_ordering = ordering,
            }),
        });
        return instruction;
    }

    pub fn cmpxchg(
        self: *WipFunction,
        kind: Instruction.CmpXchg.Kind,
        access_kind: MemoryAccessKind,
        ptr: Value,
        cmp: Value,
        new: Value,
        sync_scope: SyncScope,
        success_ordering: AtomicOrdering,
        failure_ordering: AtomicOrdering,
        alignment: Alignment,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(ptr.typeOfWip(self).isPointer(self.builder));
        const ty = cmp.typeOfWip(self);
        assert(ty == new.typeOfWip(self));
        assert(success_ordering != .none);
        assert(failure_ordering != .none);

        _ = try self.builder.structType(.normal, &.{ ty, .i1 });
        try self.ensureUnusedExtraCapacity(1, Instruction.CmpXchg, 0);
        const instruction = try self.addInst(name, .{
            .tag = switch (kind) {
                .strong => .cmpxchg,
                .weak => .@"cmpxchg weak",
            },
            .data = self.addExtraAssumeCapacity(Instruction.CmpXchg{
                .info = .{
                    .access_kind = access_kind,
                    .sync_scope = sync_scope,
                    .success_ordering = success_ordering,
                    .failure_ordering = failure_ordering,
                    .alignment = alignment,
                },
                .ptr = ptr,
                .cmp = cmp,
                .new = new,
            }),
        });
        return instruction.toValue();
    }

    pub fn atomicrmw(
        self: *WipFunction,
        access_kind: MemoryAccessKind,
        operation: Instruction.AtomicRmw.Operation,
        ptr: Value,
        val: Value,
        sync_scope: SyncScope,
        ordering: AtomicOrdering,
        alignment: Alignment,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(ptr.typeOfWip(self).isPointer(self.builder));
        assert(ordering != .none);

        try self.ensureUnusedExtraCapacity(1, Instruction.AtomicRmw, 0);
        const instruction = try self.addInst(name, .{
            .tag = .atomicrmw,
            .data = self.addExtraAssumeCapacity(Instruction.AtomicRmw{
                .info = .{
                    .access_kind = access_kind,
                    .atomic_rmw_operation = operation,
                    .sync_scope = sync_scope,
                    .success_ordering = ordering,
                    .alignment = alignment,
                },
                .ptr = ptr,
                .val = val,
            }),
        });
        return instruction.toValue();
    }

    pub fn gep(
        self: *WipFunction,
        kind: Instruction.GetElementPtr.Kind,
        ty: Type,
        base: Value,
        indices: []const Value,
        name: []const u8,
    ) Allocator.Error!Value {
        const base_ty = base.typeOfWip(self);
        const base_is_vector = base_ty.isVector(self.builder);

        const VectorInfo = struct {
            kind: Type.Vector.Kind,
            len: u32,

            fn init(vector_ty: Type, builder: *const Builder) @This() {
                return .{ .kind = vector_ty.vectorKind(builder), .len = vector_ty.vectorLen(builder) };
            }
        };
        var vector_info: ?VectorInfo =
            if (base_is_vector) VectorInfo.init(base_ty, self.builder) else null;
        for (indices) |index| {
            const index_ty = index.typeOfWip(self);
            switch (index_ty.tag(self.builder)) {
                .integer => {},
                .vector, .scalable_vector => {
                    const index_info = VectorInfo.init(index_ty, self.builder);
                    if (vector_info) |info|
                        assert(std.meta.eql(info, index_info))
                    else
                        vector_info = index_info;
                },
                else => unreachable,
            }
        }
        if (!base_is_vector) if (vector_info) |info| switch (info.kind) {
            inline else => |vector_kind| _ = try self.builder.vectorType(
                vector_kind,
                info.len,
                base_ty,
            ),
        };

        try self.ensureUnusedExtraCapacity(1, Instruction.GetElementPtr, indices.len);
        const instruction = try self.addInst(name, .{
            .tag = switch (kind) {
                .normal => .getelementptr,
                .inbounds => .@"getelementptr inbounds",
            },
            .data = self.addExtraAssumeCapacity(Instruction.GetElementPtr{
                .type = ty,
                .base = base,
                .indices_len = @intCast(indices.len),
            }),
        });
        self.extra.appendSliceAssumeCapacity(@ptrCast(indices));
        return instruction.toValue();
    }

    pub fn gepStruct(
        self: *WipFunction,
        ty: Type,
        base: Value,
        index: usize,
        name: []const u8,
    ) Allocator.Error!Value {
        assert(ty.isStruct(self.builder));
        return self.gep(.inbounds, ty, base, &.{ .@"0", try self.builder.intValue(.i32, index) }, name);
    }

    pub fn conv(
        self: *WipFunction,
        signedness: Instruction.Cast.Signedness,
        val: Value,
        ty: Type,
        name: []const u8,
    ) Allocator.Error!Value {
        const val_ty = val.typeOfWip(self);
        if (val_ty == ty) return val;
        return self.cast(self.builder.convTag(signedness, val_ty, ty), val, ty, name);
    }

    pub fn cast(
        self: *WipFunction,
        tag: Instruction.Tag,
        val: Value,
        ty: Type,
        name: []const u8,
    ) Allocator.Error!Value {
        switch (tag) {
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
            => {},
            else => unreachable,
        }
        if (val.typeOfWip(self) == ty) return val;
        try self.ensureUnusedExtraCapacity(1, Instruction.Cast, 0);
        const instruction = try self.addInst(name, .{
            .tag = tag,
            .data = self.addExtraAssumeCapacity(Instruction.Cast{
                .val = val,
                .type = ty,
            }),
        });
        return instruction.toValue();
    }

    pub fn icmp(
        self: *WipFunction,
        cond: IntegerCondition,
        lhs: Value,
        rhs: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        return self.cmpTag(switch (cond) {
            inline else => |tag| @field(Instruction.Tag, "icmp " ++ @tagName(tag)),
        }, lhs, rhs, name);
    }

    pub fn fcmp(
        self: *WipFunction,
        fast: FastMathKind,
        cond: FloatCondition,
        lhs: Value,
        rhs: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        return self.cmpTag(switch (fast) {
            inline else => |fast_tag| switch (cond) {
                inline else => |cond_tag| @field(Instruction.Tag, "fcmp " ++ switch (fast_tag) {
                    .normal => "",
                    .fast => "fast ",
                } ++ @tagName(cond_tag)),
            },
        }, lhs, rhs, name);
    }

    pub const WipPhi = struct {
        block: Block.Index,
        instruction: Instruction.Index,

        pub fn toValue(self: WipPhi) Value {
            return self.instruction.toValue();
        }

        pub fn finish(
            self: WipPhi,
            vals: []const Value,
            blocks: []const Block.Index,
            wip: *WipFunction,
        ) void {
            const incoming_len = self.block.ptrConst(wip).incoming;
            assert(vals.len == incoming_len and blocks.len == incoming_len);
            const instruction = wip.instructions.get(@intFromEnum(self.instruction));
            var extra = wip.extraDataTrail(Instruction.Phi, instruction.data);
            for (vals) |val| assert(val.typeOfWip(wip) == extra.data.type);
            @memcpy(extra.trail.nextMut(incoming_len, Value, wip), vals);
            @memcpy(extra.trail.nextMut(incoming_len, Block.Index, wip), blocks);
        }
    };

    pub fn phi(self: *WipFunction, ty: Type, name: []const u8) Allocator.Error!WipPhi {
        return self.phiTag(.phi, ty, name);
    }

    pub fn phiFast(self: *WipFunction, ty: Type, name: []const u8) Allocator.Error!WipPhi {
        return self.phiTag(.@"phi fast", ty, name);
    }

    pub fn select(
        self: *WipFunction,
        fast: FastMathKind,
        cond: Value,
        lhs: Value,
        rhs: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        return self.selectTag(switch (fast) {
            .normal => .select,
            .fast => .@"select fast",
        }, cond, lhs, rhs, name);
    }

    pub fn call(
        self: *WipFunction,
        kind: Instruction.Call.Kind,
        call_conv: CallConv,
        function_attributes: FunctionAttributes,
        ty: Type,
        callee: Value,
        args: []const Value,
        name: []const u8,
    ) Allocator.Error!Value {
        return self.callInner(kind, call_conv, function_attributes, ty, callee, args, name, false);
    }

    fn callInner(
        self: *WipFunction,
        kind: Instruction.Call.Kind,
        call_conv: CallConv,
        function_attributes: FunctionAttributes,
        ty: Type,
        callee: Value,
        args: []const Value,
        name: []const u8,
        has_op_bundle_cold: bool,
    ) Allocator.Error!Value {
        const ret_ty = ty.functionReturn(self.builder);
        assert(ty.isFunction(self.builder));
        assert(callee.typeOfWip(self).isPointer(self.builder));
        const params = ty.functionParameters(self.builder);
        for (params, args[0..params.len]) |param, arg_val| assert(param == arg_val.typeOfWip(self));

        try self.ensureUnusedExtraCapacity(1, Instruction.Call, args.len);
        const instruction = try self.addInst(switch (ret_ty) {
            .void => null,
            else => name,
        }, .{
            .tag = switch (kind) {
                .normal => .call,
                .fast => .@"call fast",
                .musttail => .@"musttail call",
                .musttail_fast => .@"musttail call fast",
                .notail => .@"notail call",
                .notail_fast => .@"notail call fast",
                .tail => .@"tail call",
                .tail_fast => .@"tail call fast",
            },
            .data = self.addExtraAssumeCapacity(Instruction.Call{
                .info = .{
                    .call_conv = call_conv,
                    .has_op_bundle_cold = has_op_bundle_cold,
                },
                .attributes = function_attributes,
                .ty = ty,
                .callee = callee,
                .args_len = @intCast(args.len),
            }),
        });
        self.extra.appendSliceAssumeCapacity(@ptrCast(args));
        return instruction.toValue();
    }

    pub fn callAsm(
        self: *WipFunction,
        function_attributes: FunctionAttributes,
        ty: Type,
        kind: Constant.Assembly.Info,
        assembly: String,
        constraints: String,
        args: []const Value,
        name: []const u8,
    ) Allocator.Error!Value {
        const callee = try self.builder.asmValue(ty, kind, assembly, constraints);
        return self.call(.normal, CallConv.default, function_attributes, ty, callee, args, name);
    }

    pub fn callIntrinsic(
        self: *WipFunction,
        fast: FastMathKind,
        function_attributes: FunctionAttributes,
        id: Intrinsic,
        overload: []const Type,
        args: []const Value,
        name: []const u8,
    ) Allocator.Error!Value {
        const intrinsic = try self.builder.getIntrinsic(id, overload);
        return self.call(
            fast.toCallKind(),
            CallConv.default,
            function_attributes,
            intrinsic.typeOf(self.builder),
            intrinsic.toValue(self.builder),
            args,
            name,
        );
    }

    pub fn callIntrinsicAssumeCold(self: *WipFunction) Allocator.Error!Value {
        const intrinsic = try self.builder.getIntrinsic(.assume, &.{});
        return self.callInner(
            .normal,
            CallConv.default,
            .none,
            intrinsic.typeOf(self.builder),
            intrinsic.toValue(self.builder),
            &.{try self.builder.intValue(.i1, 1)},
            "",
            true,
        );
    }

    pub fn callMemCpy(
        self: *WipFunction,
        dst: Value,
        dst_align: Alignment,
        src: Value,
        src_align: Alignment,
        len: Value,
        kind: MemoryAccessKind,
        @"inline": bool,
    ) Allocator.Error!Instruction.Index {
        var dst_attrs = [_]Attribute.Index{try self.builder.attr(.{ .@"align" = .wrap(dst_align) })};
        var src_attrs = [_]Attribute.Index{try self.builder.attr(.{ .@"align" = .wrap(src_align) })};
        const value = try self.callIntrinsic(
            .normal,
            try self.builder.fnAttrs(&.{
                .none,
                .none,
                try self.builder.attrs(&dst_attrs),
                try self.builder.attrs(&src_attrs),
            }),
            if (@"inline") .@"memcpy.inline" else .memcpy,
            &.{ dst.typeOfWip(self), src.typeOfWip(self), len.typeOfWip(self) },
            &.{ dst, src, len, switch (kind) {
                .normal => Value.false,
                .@"volatile" => Value.true,
            } },
            undefined,
        );
        return value.unwrap().instruction;
    }

    pub fn callMemMove(
        self: *WipFunction,
        dst: Value,
        dst_align: Alignment,
        src: Value,
        src_align: Alignment,
        len: Value,
        kind: MemoryAccessKind,
    ) Allocator.Error!Instruction.Index {
        var dst_attrs = [_]Attribute.Index{try self.builder.attr(.{ .@"align" = .wrap(dst_align) })};
        var src_attrs = [_]Attribute.Index{try self.builder.attr(.{ .@"align" = .wrap(src_align) })};
        const value = try self.callIntrinsic(
            .normal,
            try self.builder.fnAttrs(&.{
                .none,
                .none,
                try self.builder.attrs(&dst_attrs),
                try self.builder.attrs(&src_attrs),
            }),
            .memmove,
            &.{ dst.typeOfWip(self), src.typeOfWip(self), len.typeOfWip(self) },
            &.{ dst, src, len, switch (kind) {
                .normal => Value.false,
                .@"volatile" => Value.true,
            } },
            undefined,
        );
        return value.unwrap().instruction;
    }

    pub fn callMemSet(
        self: *WipFunction,
        dst: Value,
        dst_align: Alignment,
        val: Value,
        len: Value,
        kind: MemoryAccessKind,
        @"inline": bool,
    ) Allocator.Error!Instruction.Index {
        var dst_attrs = [_]Attribute.Index{try self.builder.attr(.{ .@"align" = .wrap(dst_align) })};
        const value = try self.callIntrinsic(
            .normal,
            try self.builder.fnAttrs(&.{ .none, .none, try self.builder.attrs(&dst_attrs) }),
            if (@"inline") .@"memset.inline" else .memset,
            &.{ dst.typeOfWip(self), len.typeOfWip(self) },
            &.{ dst, val, len, switch (kind) {
                .normal => Value.false,
                .@"volatile" => Value.true,
            } },
            undefined,
        );
        return value.unwrap().instruction;
    }

    pub fn vaArg(self: *WipFunction, list: Value, ty: Type, name: []const u8) Allocator.Error!Value {
        try self.ensureUnusedExtraCapacity(1, Instruction.VaArg, 0);
        const instruction = try self.addInst(name, .{
            .tag = .va_arg,
            .data = self.addExtraAssumeCapacity(Instruction.VaArg{
                .list = list,
                .type = ty,
            }),
        });
        return instruction.toValue();
    }

    pub fn debugValue(self: *WipFunction, value: Value) Allocator.Error!Metadata.Optional {
        if (self.strip) return .none;
        const metadata: Metadata = metadata: switch (value.unwrap()) {
            .instruction => |instr_index| {
                const gop = try self.debug_values.getOrPut(self.builder.gpa, instr_index);
                if (!gop.found_existing) gop.key_ptr.* = instr_index;
                break :metadata .{ .index = @intCast(gop.index), .kind = .local };
            },
            .constant => |constant| try self.builder.metadataConstant(constant),
            .metadata => |metadata| metadata,
        };
        return metadata.toOptional();
    }

    pub fn finish(self: *WipFunction) Allocator.Error!void {
        const gpa = self.builder.gpa;
        const function = self.function.ptr(self.builder);
        const params_len = self.function.typeOf(self.builder).functionParameters(self.builder).len;
        const final_instructions_len = self.blocks.items.len + self.instructions.len;

        const blocks = try gpa.alloc(Function.Block, self.blocks.items.len);
        errdefer gpa.free(blocks);

        const instructions: struct {
            items: []Instruction.Index,

            fn map(instructions: @This(), val: Value) Value {
                if (val == .none) return .none;
                return switch (val.unwrap()) {
                    .instruction => |instruction| instructions.items[
                        @intFromEnum(instruction)
                    ].toValue(),
                    .constant => |constant| constant.toValue(),
                    .metadata => |metadata| metadata.toValue(),
                };
            }
        } = .{ .items = try gpa.alloc(Instruction.Index, self.instructions.len) };
        defer gpa.free(instructions.items);

        const names = try gpa.alloc(String, final_instructions_len);
        errdefer gpa.free(names);

        const value_indices = try gpa.alloc(u32, final_instructions_len);
        errdefer gpa.free(value_indices);

        var debug_locations: std.AutoHashMapUnmanaged(Instruction.Index, DebugLocation) = .empty;
        errdefer debug_locations.deinit(gpa);
        try debug_locations.ensureUnusedCapacity(gpa, @intCast(self.debug_locations.count()));

        const debug_values = try gpa.alloc(Instruction.Index, self.debug_values.count());
        errdefer gpa.free(debug_values);

        var wip_extra: struct {
            index: Instruction.ExtraIndex = 0,
            items: []u32,

            fn addExtra(wip_extra: *@This(), extra: anytype) Instruction.ExtraIndex {
                const result = wip_extra.index;
                inline for (@typeInfo(@TypeOf(extra)).@"struct".fields) |field| {
                    const value = @field(extra, field.name);
                    wip_extra.items[wip_extra.index] = switch (field.type) {
                        u32 => value,
                        Alignment,
                        AtomicOrdering,
                        Block.Index,
                        FunctionAttributes,
                        Type,
                        Value,
                        Instruction.BrCond.Weights,
                        => @intFromEnum(value),
                        MemoryAccessInfo,
                        Instruction.Alloca.Info,
                        Instruction.Call.Info,
                        => @bitCast(value),
                        else => @compileError("bad field type: " ++ field.name ++ ": " ++ @typeName(field.type)),
                    };
                    wip_extra.index += 1;
                }
                return result;
            }

            fn appendSlice(wip_extra: *@This(), slice: anytype) void {
                if (@typeInfo(@TypeOf(slice)).pointer.child == Value)
                    @compileError("use appendMappedValues");
                const data: []const u32 = @ptrCast(slice);
                @memcpy(wip_extra.items[wip_extra.index..][0..data.len], data);
                wip_extra.index += @intCast(data.len);
            }

            fn appendMappedValues(wip_extra: *@This(), vals: []const Value, ctx: anytype) void {
                for (wip_extra.items[wip_extra.index..][0..vals.len], vals) |*extra, val|
                    extra.* = @intFromEnum(ctx.map(val));
                wip_extra.index += @intCast(vals.len);
            }

            fn finish(wip_extra: *const @This()) []const u32 {
                assert(wip_extra.index == wip_extra.items.len);
                return wip_extra.items;
            }
        } = .{ .items = try gpa.alloc(u32, self.extra.items.len) };
        errdefer gpa.free(wip_extra.items);

        gpa.free(function.blocks);
        function.blocks = &.{};
        gpa.free(function.names[0..function.instructions.len]);
        function.debug_locations.deinit(gpa);
        function.debug_locations = .empty;
        gpa.free(function.debug_values);
        function.debug_values = &.{};
        gpa.free(function.extra);
        function.extra = &.{};

        function.instructions.shrinkRetainingCapacity(0);
        try function.instructions.setCapacity(gpa, final_instructions_len);
        errdefer function.instructions.shrinkRetainingCapacity(0);

        {
            var final_instruction_index: Instruction.Index = @enumFromInt(0);
            for (0..params_len) |param_index| {
                instructions.items[param_index] = final_instruction_index;
                final_instruction_index = @enumFromInt(@intFromEnum(final_instruction_index) + 1);
            }
            for (blocks, self.blocks.items) |*final_block, current_block| {
                assert(current_block.incoming == current_block.branches);
                final_block.instruction = final_instruction_index;
                final_instruction_index = @enumFromInt(@intFromEnum(final_instruction_index) + 1);
                for (current_block.instructions.items) |instruction| {
                    instructions.items[@intFromEnum(instruction)] = final_instruction_index;
                    final_instruction_index = @enumFromInt(@intFromEnum(final_instruction_index) + 1);
                }
            }
        }

        var wip_name: struct {
            next_name: String = @enumFromInt(0),
            next_unique_name: std.AutoHashMap(String, String),
            builder: *Builder,

            fn map(wip_name: *@This(), name: String, sep: []const u8) Allocator.Error!String {
                switch (name) {
                    .none => return .none,
                    .empty => {
                        assert(wip_name.next_name != .none);
                        defer wip_name.next_name = @enumFromInt(@intFromEnum(wip_name.next_name) + 1);
                        return wip_name.next_name;
                    },
                    _ => {
                        assert(!name.isAnon());
                        const gop = try wip_name.next_unique_name.getOrPut(name);
                        if (!gop.found_existing) {
                            gop.value_ptr.* = @enumFromInt(0);
                            return name;
                        }

                        while (true) {
                            gop.value_ptr.* = @enumFromInt(@intFromEnum(gop.value_ptr.*) + 1);
                            const unique_name = try wip_name.builder.fmt("{f}{s}{f}", .{
                                name.fmtRaw(wip_name.builder),
                                sep,
                                gop.value_ptr.fmtRaw(wip_name.builder),
                            });
                            const unique_gop = try wip_name.next_unique_name.getOrPut(unique_name);
                            if (!unique_gop.found_existing) {
                                unique_gop.value_ptr.* = @enumFromInt(0);
                                return unique_name;
                            }
                        }
                    },
                }
            }
        } = .{
            .next_unique_name = std.AutoHashMap(String, String).init(gpa),
            .builder = self.builder,
        };
        defer wip_name.next_unique_name.deinit();

        var value_index: u32 = 0;
        for (0..params_len) |param_index| {
            const old_argument_index: Instruction.Index = @enumFromInt(param_index);
            const new_argument_index: Instruction.Index = @enumFromInt(function.instructions.len);
            const argument = self.instructions.get(@intFromEnum(old_argument_index));
            assert(argument.tag == .arg);
            assert(argument.data == param_index);
            value_indices[function.instructions.len] = value_index;
            value_index += 1;
            function.instructions.appendAssumeCapacity(argument);
            names[@intFromEnum(new_argument_index)] = try wip_name.map(
                if (self.strip) .empty else self.names.items[@intFromEnum(old_argument_index)],
                ".",
            );
            if (self.debug_locations.get(old_argument_index)) |location| {
                debug_locations.putAssumeCapacity(new_argument_index, location);
            }
            if (self.debug_values.getIndex(old_argument_index)) |index| {
                debug_values[index] = new_argument_index;
            }
        }
        for (self.blocks.items) |current_block| {
            const new_block_index: Instruction.Index = @enumFromInt(function.instructions.len);
            value_indices[function.instructions.len] = value_index;
            function.instructions.appendAssumeCapacity(.{
                .tag = .block,
                .data = current_block.incoming,
            });
            names[@intFromEnum(new_block_index)] = try wip_name.map(current_block.name, "");
            for (current_block.instructions.items) |old_instruction_index| {
                const new_instruction_index: Instruction.Index = @enumFromInt(function.instructions.len);
                var instruction = self.instructions.get(@intFromEnum(old_instruction_index));
                switch (instruction.tag) {
                    .add,
                    .@"add nsw",
                    .@"add nuw",
                    .@"add nuw nsw",
                    .@"and",
                    .ashr,
                    .@"ashr exact",
                    .fadd,
                    .@"fadd fast",
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
                    .fdiv,
                    .@"fdiv fast",
                    .fmul,
                    .@"fmul fast",
                    .frem,
                    .@"frem fast",
                    .fsub,
                    .@"fsub fast",
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
                    => {
                        const extra = self.extraData(Instruction.Binary, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.Binary{
                            .lhs = instructions.map(extra.lhs),
                            .rhs = instructions.map(extra.rhs),
                        });
                    },
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
                    => {
                        const extra = self.extraData(Instruction.Cast, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.Cast{
                            .val = instructions.map(extra.val),
                            .type = extra.type,
                        });
                    },
                    .alloca,
                    .@"alloca inalloca",
                    => {
                        const extra = self.extraData(Instruction.Alloca, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.Alloca{
                            .type = extra.type,
                            .len = instructions.map(extra.len),
                            .info = extra.info,
                        });
                    },
                    .arg,
                    .block,
                    => unreachable,
                    .atomicrmw => {
                        const extra = self.extraData(Instruction.AtomicRmw, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.AtomicRmw{
                            .info = extra.info,
                            .ptr = instructions.map(extra.ptr),
                            .val = instructions.map(extra.val),
                        });
                    },
                    .br,
                    .fence,
                    .@"ret void",
                    .@"unreachable",
                    => {},
                    .br_cond => {
                        const extra = self.extraData(Instruction.BrCond, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.BrCond{
                            .cond = instructions.map(extra.cond),
                            .then = extra.then,
                            .@"else" = extra.@"else",
                            .weights = extra.weights,
                        });
                    },
                    .call,
                    .@"call fast",
                    .@"musttail call",
                    .@"musttail call fast",
                    .@"notail call",
                    .@"notail call fast",
                    .@"tail call",
                    .@"tail call fast",
                    => {
                        var extra = self.extraDataTrail(Instruction.Call, instruction.data);
                        const args = extra.trail.next(extra.data.args_len, Value, self);
                        instruction.data = wip_extra.addExtra(Instruction.Call{
                            .info = extra.data.info,
                            .attributes = extra.data.attributes,
                            .ty = extra.data.ty,
                            .callee = instructions.map(extra.data.callee),
                            .args_len = extra.data.args_len,
                        });
                        wip_extra.appendMappedValues(args, instructions);
                    },
                    .cmpxchg,
                    .@"cmpxchg weak",
                    => {
                        const extra = self.extraData(Instruction.CmpXchg, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.CmpXchg{
                            .info = extra.info,
                            .ptr = instructions.map(extra.ptr),
                            .cmp = instructions.map(extra.cmp),
                            .new = instructions.map(extra.new),
                        });
                    },
                    .extractelement => {
                        const extra = self.extraData(Instruction.ExtractElement, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.ExtractElement{
                            .val = instructions.map(extra.val),
                            .index = instructions.map(extra.index),
                        });
                    },
                    .extractvalue => {
                        var extra = self.extraDataTrail(Instruction.ExtractValue, instruction.data);
                        const indices = extra.trail.next(extra.data.indices_len, u32, self);
                        instruction.data = wip_extra.addExtra(Instruction.ExtractValue{
                            .val = instructions.map(extra.data.val),
                            .indices_len = extra.data.indices_len,
                        });
                        wip_extra.appendSlice(indices);
                    },
                    .fneg,
                    .@"fneg fast",
                    .ret,
                    => instruction.data = @intFromEnum(instructions.map(@enumFromInt(instruction.data))),
                    .getelementptr,
                    .@"getelementptr inbounds",
                    => {
                        var extra = self.extraDataTrail(Instruction.GetElementPtr, instruction.data);
                        const indices = extra.trail.next(extra.data.indices_len, Value, self);
                        instruction.data = wip_extra.addExtra(Instruction.GetElementPtr{
                            .type = extra.data.type,
                            .base = instructions.map(extra.data.base),
                            .indices_len = extra.data.indices_len,
                        });
                        wip_extra.appendMappedValues(indices, instructions);
                    },
                    .indirectbr => {
                        var extra = self.extraDataTrail(Instruction.IndirectBr, instruction.data);
                        const targets = extra.trail.next(extra.data.targets_len, Block.Index, self);
                        instruction.data = wip_extra.addExtra(Instruction.IndirectBr{
                            .addr = instructions.map(extra.data.addr),
                            .targets_len = extra.data.targets_len,
                        });
                        wip_extra.appendSlice(targets);
                    },
                    .insertelement => {
                        const extra = self.extraData(Instruction.InsertElement, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.InsertElement{
                            .val = instructions.map(extra.val),
                            .elem = instructions.map(extra.elem),
                            .index = instructions.map(extra.index),
                        });
                    },
                    .insertvalue => {
                        var extra = self.extraDataTrail(Instruction.InsertValue, instruction.data);
                        const indices = extra.trail.next(extra.data.indices_len, u32, self);
                        instruction.data = wip_extra.addExtra(Instruction.InsertValue{
                            .val = instructions.map(extra.data.val),
                            .elem = instructions.map(extra.data.elem),
                            .indices_len = extra.data.indices_len,
                        });
                        wip_extra.appendSlice(indices);
                    },
                    .load,
                    .@"load atomic",
                    => {
                        const extra = self.extraData(Instruction.Load, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.Load{
                            .type = extra.type,
                            .ptr = instructions.map(extra.ptr),
                            .info = extra.info,
                        });
                    },
                    .phi,
                    .@"phi fast",
                    => {
                        const incoming_len = current_block.incoming;
                        var extra = self.extraDataTrail(Instruction.Phi, instruction.data);
                        const incoming_vals = extra.trail.next(incoming_len, Value, self);
                        const incoming_blocks = extra.trail.next(incoming_len, Block.Index, self);
                        instruction.data = wip_extra.addExtra(Instruction.Phi{
                            .type = extra.data.type,
                        });
                        wip_extra.appendMappedValues(incoming_vals, instructions);
                        wip_extra.appendSlice(incoming_blocks);
                    },
                    .select,
                    .@"select fast",
                    => {
                        const extra = self.extraData(Instruction.Select, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.Select{
                            .cond = instructions.map(extra.cond),
                            .lhs = instructions.map(extra.lhs),
                            .rhs = instructions.map(extra.rhs),
                        });
                    },
                    .shufflevector => {
                        const extra = self.extraData(Instruction.ShuffleVector, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.ShuffleVector{
                            .lhs = instructions.map(extra.lhs),
                            .rhs = instructions.map(extra.rhs),
                            .mask = instructions.map(extra.mask),
                        });
                    },
                    .store,
                    .@"store atomic",
                    => {
                        const extra = self.extraData(Instruction.Store, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.Store{
                            .val = instructions.map(extra.val),
                            .ptr = instructions.map(extra.ptr),
                            .info = extra.info,
                        });
                    },
                    .@"switch" => {
                        var extra = self.extraDataTrail(Instruction.Switch, instruction.data);
                        const case_vals = extra.trail.next(extra.data.cases_len, Constant, self);
                        const case_blocks = extra.trail.next(extra.data.cases_len, Block.Index, self);
                        instruction.data = wip_extra.addExtra(Instruction.Switch{
                            .val = instructions.map(extra.data.val),
                            .default = extra.data.default,
                            .cases_len = extra.data.cases_len,
                            .weights = extra.data.weights,
                        });
                        wip_extra.appendSlice(case_vals);
                        wip_extra.appendSlice(case_blocks);
                    },
                    .va_arg => {
                        const extra = self.extraData(Instruction.VaArg, instruction.data);
                        instruction.data = wip_extra.addExtra(Instruction.VaArg{
                            .list = instructions.map(extra.list),
                            .type = extra.type,
                        });
                    },
                }
                function.instructions.appendAssumeCapacity(instruction);
                names[@intFromEnum(new_instruction_index)] = try wip_name.map(if (self.strip)
                    if (old_instruction_index.hasResultWip(self)) .empty else .none
                else
                    self.names.items[@intFromEnum(old_instruction_index)], ".");

                if (self.debug_locations.get(old_instruction_index)) |location| {
                    debug_locations.putAssumeCapacity(new_instruction_index, location);
                }

                if (self.debug_values.getIndex(old_instruction_index)) |index| {
                    debug_values[index] = new_instruction_index;
                }

                value_indices[@intFromEnum(new_instruction_index)] = value_index;
                if (old_instruction_index.hasResultWip(self)) value_index += 1;
            }
        }

        assert(function.instructions.len == final_instructions_len);
        function.extra = wip_extra.finish();
        function.blocks = blocks;
        function.names = names.ptr;
        function.value_indices = value_indices.ptr;
        function.strip = self.strip;
        function.debug_locations = debug_locations;
        function.debug_values = debug_values;
    }

    pub fn deinit(self: *WipFunction) void {
        self.extra.deinit(self.builder.gpa);
        self.debug_values.deinit(self.builder.gpa);
        self.debug_locations.deinit(self.builder.gpa);
        self.names.deinit(self.builder.gpa);
        self.instructions.deinit(self.builder.gpa);
        for (self.blocks.items) |*b| b.instructions.deinit(self.builder.gpa);
        self.blocks.deinit(self.builder.gpa);
        self.* = undefined;
    }

    fn cmpTag(
        self: *WipFunction,
        tag: Instruction.Tag,
        lhs: Value,
        rhs: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        switch (tag) {
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
            => assert(lhs.typeOfWip(self) == rhs.typeOfWip(self)),
            else => unreachable,
        }
        _ = try lhs.typeOfWip(self).changeScalar(.i1, self.builder);
        try self.ensureUnusedExtraCapacity(1, Instruction.Binary, 0);
        const instruction = try self.addInst(name, .{
            .tag = tag,
            .data = self.addExtraAssumeCapacity(Instruction.Binary{
                .lhs = lhs,
                .rhs = rhs,
            }),
        });
        return instruction.toValue();
    }

    fn phiTag(
        self: *WipFunction,
        tag: Instruction.Tag,
        ty: Type,
        name: []const u8,
    ) Allocator.Error!WipPhi {
        switch (tag) {
            .phi, .@"phi fast" => assert(try ty.isSized(self.builder)),
            else => unreachable,
        }
        const incoming = self.cursor.block.ptrConst(self).incoming;
        assert(incoming > 0);
        try self.ensureUnusedExtraCapacity(1, Instruction.Phi, incoming * 2);
        const instruction = try self.addInst(name, .{
            .tag = tag,
            .data = self.addExtraAssumeCapacity(Instruction.Phi{ .type = ty }),
        });
        _ = self.extra.addManyAsSliceAssumeCapacity(incoming * 2);
        return .{ .block = self.cursor.block, .instruction = instruction };
    }

    fn selectTag(
        self: *WipFunction,
        tag: Instruction.Tag,
        cond: Value,
        lhs: Value,
        rhs: Value,
        name: []const u8,
    ) Allocator.Error!Value {
        switch (tag) {
            .select, .@"select fast" => {
                assert(cond.typeOfWip(self).scalarType(self.builder) == .i1);
                assert(lhs.typeOfWip(self) == rhs.typeOfWip(self));
            },
            else => unreachable,
        }
        try self.ensureUnusedExtraCapacity(1, Instruction.Select, 0);
        const instruction = try self.addInst(name, .{
            .tag = tag,
            .data = self.addExtraAssumeCapacity(Instruction.Select{
                .cond = cond,
                .lhs = lhs,
                .rhs = rhs,
            }),
        });
        return instruction.toValue();
    }

    fn ensureUnusedExtraCapacity(
        self: *WipFunction,
        count: usize,
        comptime Extra: type,
        trail_len: usize,
    ) Allocator.Error!void {
        try self.extra.ensureUnusedCapacity(
            self.builder.gpa,
            count * (@typeInfo(Extra).@"struct".fields.len + trail_len),
        );
    }

    fn addInst(
        self: *WipFunction,
        name: ?[]const u8,
        instruction: Instruction,
    ) Allocator.Error!Instruction.Index {
        const block_instructions = &self.cursor.block.ptr(self).instructions;
        try self.instructions.ensureUnusedCapacity(self.builder.gpa, 1);
        if (!self.strip) {
            try self.names.ensureUnusedCapacity(self.builder.gpa, 1);
            try self.debug_locations.ensureUnusedCapacity(self.builder.gpa, 1);
        }
        try block_instructions.ensureUnusedCapacity(self.builder.gpa, 1);
        const final_name = if (name) |n|
            if (self.strip) .empty else try self.builder.string(n)
        else
            .none;

        const index: Instruction.Index = @enumFromInt(self.instructions.len);
        self.instructions.appendAssumeCapacity(instruction);
        if (!self.strip) {
            self.names.appendAssumeCapacity(final_name);
            if (block_instructions.items.len == 0 or
                !std.meta.eql(self.debug_location, self.prev_debug_location))
            {
                self.debug_locations.putAssumeCapacity(index, self.debug_location);
                self.prev_debug_location = self.debug_location;
            }
        }
        block_instructions.insertAssumeCapacity(self.cursor.instruction, index);
        self.cursor.instruction += 1;
        return index;
    }

    fn addExtraAssumeCapacity(self: *WipFunction, extra: anytype) Instruction.ExtraIndex {
        const result: Instruction.ExtraIndex = @intCast(self.extra.items.len);
        inline for (@typeInfo(@TypeOf(extra)).@"struct".fields) |field| {
            const value = @field(extra, field.name);
            self.extra.appendAssumeCapacity(switch (field.type) {
                u32 => value,
                Alignment,
                AtomicOrdering,
                Block.Index,
                FunctionAttributes,
                Type,
                Value,
                Instruction.BrCond.Weights,
                => @intFromEnum(value),
                MemoryAccessInfo,
                Instruction.Alloca.Info,
                Instruction.Call.Info,
                => @bitCast(value),
                else => @compileError("bad field type: " ++ field.name ++ ": " ++ @typeName(field.type)),
            });
        }
        return result;
    }

    const ExtraDataTrail = struct {
        index: Instruction.ExtraIndex,

        fn nextMut(self: *ExtraDataTrail, len: u32, comptime Item: type, wip: *WipFunction) []Item {
            const items: []Item = @ptrCast(wip.extra.items[self.index..][0..len]);
            self.index += @intCast(len);
            return items;
        }

        fn next(
            self: *ExtraDataTrail,
            len: u32,
            comptime Item: type,
            wip: *const WipFunction,
        ) []const Item {
            const items: []const Item = @ptrCast(wip.extra.items[self.index..][0..len]);
            self.index += @intCast(len);
            return items;
        }
    };

    fn extraDataTrail(
        self: *const WipFunction,
        comptime T: type,
        index: Instruction.ExtraIndex,
    ) struct { data: T, trail: ExtraDataTrail } {
        var result: T = undefined;
        const fields = @typeInfo(T).@"struct".fields;
        inline for (fields, self.extra.items[index..][0..fields.len]) |field, value|
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

    fn extraData(self: *const WipFunction, comptime T: type, index: Instruction.ExtraIndex) T {
        return self.extraDataTrail(T, index).data;
    }
};

pub const FloatCondition = enum(u4) {
    oeq = 1,
    ogt = 2,
    oge = 3,
    olt = 4,
    ole = 5,
    one = 6,
    ord = 7,
    uno = 8,
    ueq = 9,
    ugt = 10,
    uge = 11,
    ult = 12,
    ule = 13,
    une = 14,
};

pub const IntegerCondition = enum(u6) {
    eq = 32,
    ne = 33,
    ugt = 34,
    uge = 35,
    ult = 36,
    ule = 37,
    sgt = 38,
    sge = 39,
    slt = 40,
    sle = 41,
};

pub const MemoryAccessKind = enum(u1) {
    normal,
    @"volatile",

    pub fn format(memory_access_kind: MemoryAccessKind, w: *Writer) Writer.Error!void {
        return Prefixed.format(.{ .memory_access_kind = memory_access_kind, .prefix = "" }, w);
    }

    pub const Prefixed = struct {
        memory_access_kind: MemoryAccessKind,
        prefix: []const u8,

        pub fn format(p: Prefixed, w: *Writer) Writer.Error!void {
            switch (p.memory_access_kind) {
                .normal => return,
                .@"volatile" => {
                    var vecs: [2][]const u8 = .{ p.prefix, "volatile" };
                    return w.writeVecAll(&vecs);
                },
            }
        }
    };

    pub fn fmt(memory_access_kind: MemoryAccessKind, prefix: []const u8) Prefixed {
        return .{ .memory_access_kind = memory_access_kind, .prefix = prefix };
    }
};

pub const SyncScope = enum(u1) {
    singlethread,
    system,

    pub fn format(sync_scope: SyncScope, w: *Writer) Writer.Error!void {
        return Prefixed.format(.{ .sync_scope = sync_scope, .prefix = "" }, w);
    }

    pub const Prefixed = struct {
        sync_scope: SyncScope,
        prefix: []const u8,

        pub fn format(p: Prefixed, w: *Writer) Writer.Error!void {
            switch (p.sync_scope) {
                .system => return,
                .singlethread => {
                    var vecs: [2][]const u8 = .{ p.prefix, "syncscope(\"singlethread\")" };
                    return w.writeVecAll(&vecs);
                },
            }
        }
    };

    pub fn fmt(sync_scope: SyncScope, prefix: []const u8) Prefixed {
        return .{ .sync_scope = sync_scope, .prefix = prefix };
    }
};

pub const AtomicOrdering = enum(u3) {
    none = 0,
    unordered = 1,
    monotonic = 2,
    acquire = 3,
    release = 4,
    acq_rel = 5,
    seq_cst = 6,

    pub fn format(atomic_ordering: AtomicOrdering, w: *Writer) Writer.Error!void {
        return Prefixed.format(.{ .atomic_ordering = atomic_ordering, .prefix = "" }, w);
    }

    pub const Prefixed = struct {
        atomic_ordering: AtomicOrdering,
        prefix: []const u8,

        pub fn format(p: Prefixed, w: *Writer) Writer.Error!void {
            switch (p.atomic_ordering) {
                .none => return,
                else => {
                    var vecs: [2][]const u8 = .{ p.prefix, @tagName(p.atomic_ordering) };
                    return w.writeVecAll(&vecs);
                },
            }
        }
    };

    pub fn fmt(atomic_ordering: AtomicOrdering, prefix: []const u8) Prefixed {
        return .{ .atomic_ordering = atomic_ordering, .prefix = prefix };
    }
};

const MemoryAccessInfo = packed struct(u32) {
    access_kind: MemoryAccessKind = .normal,
    atomic_rmw_operation: Function.Instruction.AtomicRmw.Operation = .none,
    sync_scope: SyncScope,
    success_ordering: AtomicOrdering,
    failure_ordering: AtomicOrdering = .none,
    alignment: Alignment = .default,
    _: u13 = undefined,
};

pub const FastMath = packed struct(u8) {
    unsafe_algebra: bool = false, // Legacy
    nnan: bool = false,
    ninf: bool = false,
    nsz: bool = false,
    arcp: bool = false,
    contract: bool = false,
    afn: bool = false,
    reassoc: bool = false,

    pub const fast = FastMath{
        .nnan = true,
        .ninf = true,
        .nsz = true,
        .arcp = true,
        .contract = true,
        .afn = true,
        .reassoc = true,
    };
};

pub const FastMathKind = enum {
    normal,
    fast,

    pub fn toCallKind(self: FastMathKind) Function.Instruction.Call.Kind {
        return switch (self) {
            .normal => .normal,
            .fast => .fast,
        };
    }
};

pub const Constant = enum(u32) {
    false,
    true,
    @"0",
    @"1",
    none,
    no_init = (1 << 30) - 1,
    _,

    const first_global: Constant = @enumFromInt(1 << 29);

    pub const Tag = enum(u7) {
        positive_integer,
        negative_integer,
        half,
        bfloat,
        float,
        double,
        fp128,
        x86_fp80,
        ppc_fp128,
        null,
        none,
        structure,
        packed_structure,
        array,
        string,
        vector,
        splat,
        zeroinitializer,
        undef,
        poison,
        blockaddress,
        dso_local_equivalent,
        no_cfi,
        trunc,
        ptrtoint,
        inttoptr,
        bitcast,
        addrspacecast,
        getelementptr,
        @"getelementptr inbounds",
        add,
        @"add nsw",
        @"add nuw",
        sub,
        @"sub nsw",
        @"sub nuw",
        shl,
        xor,
        @"asm",
        @"asm sideeffect",
        @"asm alignstack",
        @"asm sideeffect alignstack",
        @"asm inteldialect",
        @"asm sideeffect inteldialect",
        @"asm alignstack inteldialect",
        @"asm sideeffect alignstack inteldialect",
        @"asm unwind",
        @"asm sideeffect unwind",
        @"asm alignstack unwind",
        @"asm sideeffect alignstack unwind",
        @"asm inteldialect unwind",
        @"asm sideeffect inteldialect unwind",
        @"asm alignstack inteldialect unwind",
        @"asm sideeffect alignstack inteldialect unwind",

        pub fn toBinaryOpcode(self: Tag) BinaryOpcode {
            return switch (self) {
                .add,
                .@"add nsw",
                .@"add nuw",
                => .add,
                .sub,
                .@"sub nsw",
                .@"sub nuw",
                => .sub,
                .shl => .shl,
                .xor => .xor,
                else => unreachable,
            };
        }

        pub fn toCastOpcode(self: Tag) CastOpcode {
            return switch (self) {
                .trunc => .trunc,
                .ptrtoint => .ptrtoint,
                .inttoptr => .inttoptr,
                .bitcast => .bitcast,
                .addrspacecast => .addrspacecast,
                else => unreachable,
            };
        }
    };

    pub const Item = struct {
        tag: Tag,
        data: ExtraIndex,

        const ExtraIndex = u32;
    };

    pub const Integer = packed struct(u64) {
        type: Type,
        limbs_len: u32,

        pub const limbs = @divExact(@bitSizeOf(Integer), @bitSizeOf(std.math.big.Limb));
    };

    pub const Double = struct {
        lo: u32,
        hi: u32,
    };

    pub const Fp80 = struct {
        lo_lo: u32,
        lo_hi: u32,
        hi: u32,
    };

    pub const Fp128 = struct {
        lo_lo: u32,
        lo_hi: u32,
        hi_lo: u32,
        hi_hi: u32,
    };

    pub const Aggregate = struct {
        type: Type,
        //fields: [type.aggregateLen(builder)]Constant,
    };

    pub const Splat = extern struct {
        type: Type,
        value: Constant,
    };

    pub const BlockAddress = extern struct {
        function: Function.Index,
        block: Function.Block.Index,
    };

    pub const Cast = extern struct {
        val: Constant,
        type: Type,

        pub const Signedness = enum { unsigned, signed, unneeded };
    };

    pub const GetElementPtr = struct {
        type: Type,
        base: Constant,
        info: Info,
        //indices: [info.indices_len]Constant,

        pub const Kind = enum { normal, inbounds };
        pub const InRangeIndex = enum(u16) { none = maxInt(u16), _ };
        pub const Info = packed struct(u32) { indices_len: u16, inrange: InRangeIndex };
    };

    pub const Binary = extern struct {
        lhs: Constant,
        rhs: Constant,
    };

    pub const Assembly = extern struct {
        type: Type,
        assembly: String,
        constraints: String,

        pub const Info = packed struct {
            sideeffect: bool = false,
            alignstack: bool = false,
            inteldialect: bool = false,
            unwind: bool = false,
        };
    };

    pub fn unwrap(self: Constant) union(enum) {
        constant: u30,
        global: Global.Index,
    } {
        return if (@intFromEnum(self) < @intFromEnum(first_global))
            .{ .constant = @intCast(@intFromEnum(self)) }
        else
            .{ .global = @enumFromInt(@intFromEnum(self) - @intFromEnum(first_global)) };
    }

    pub fn toValue(self: Constant) Value {
        return @enumFromInt(Value.first_constant + @intFromEnum(self));
    }

    pub fn typeOf(self: Constant, builder: *Builder) Type {
        switch (self.unwrap()) {
            .constant => |constant| {
                const item = builder.constant_items.get(constant);
                return switch (item.tag) {
                    .positive_integer,
                    .negative_integer,
                    => @as(
                        *align(@alignOf(std.math.big.Limb)) Integer,
                        @ptrCast(builder.constant_limbs.items[item.data..][0..Integer.limbs]),
                    ).type,
                    .half => .half,
                    .bfloat => .bfloat,
                    .float => .float,
                    .double => .double,
                    .fp128 => .fp128,
                    .x86_fp80 => .x86_fp80,
                    .ppc_fp128 => .ppc_fp128,
                    .null,
                    .none,
                    .zeroinitializer,
                    .undef,
                    .poison,
                    => @enumFromInt(item.data),
                    .structure,
                    .packed_structure,
                    .array,
                    .vector,
                    => builder.constantExtraData(Aggregate, item.data).type,
                    .splat => builder.constantExtraData(Splat, item.data).type,
                    .string => builder.arrayTypeAssumeCapacity(
                        @as(String, @enumFromInt(item.data)).slice(builder).?.len,
                        .i8,
                    ),
                    .blockaddress => builder.ptrTypeAssumeCapacity(
                        builder.constantExtraData(BlockAddress, item.data)
                            .function.ptrConst(builder).global.ptrConst(builder).addr_space,
                    ),
                    .dso_local_equivalent,
                    .no_cfi,
                    => builder.ptrTypeAssumeCapacity(@as(Function.Index, @enumFromInt(item.data))
                        .ptrConst(builder).global.ptrConst(builder).addr_space),
                    .trunc,
                    .ptrtoint,
                    .inttoptr,
                    .bitcast,
                    .addrspacecast,
                    => builder.constantExtraData(Cast, item.data).type,
                    .getelementptr,
                    .@"getelementptr inbounds",
                    => {
                        var extra = builder.constantExtraDataTrail(GetElementPtr, item.data);
                        const indices =
                            extra.trail.next(extra.data.info.indices_len, Constant, builder);
                        const base_ty = extra.data.base.typeOf(builder);
                        if (!base_ty.isVector(builder)) for (indices) |index| {
                            const index_ty = index.typeOf(builder);
                            if (!index_ty.isVector(builder)) continue;
                            return index_ty.changeScalarAssumeCapacity(base_ty, builder);
                        };
                        return base_ty;
                    },
                    .add,
                    .@"add nsw",
                    .@"add nuw",
                    .sub,
                    .@"sub nsw",
                    .@"sub nuw",
                    .shl,
                    .xor,
                    => builder.constantExtraData(Binary, item.data).lhs.typeOf(builder),
                    .@"asm",
                    .@"asm sideeffect",
                    .@"asm alignstack",
                    .@"asm sideeffect alignstack",
                    .@"asm inteldialect",
                    .@"asm sideeffect inteldialect",
                    .@"asm alignstack inteldialect",
                    .@"asm sideeffect alignstack inteldialect",
                    .@"asm unwind",
                    .@"asm sideeffect unwind",
                    .@"asm alignstack unwind",
                    .@"asm sideeffect alignstack unwind",
                    .@"asm inteldialect unwind",
                    .@"asm sideeffect inteldialect unwind",
                    .@"asm alignstack inteldialect unwind",
                    .@"asm sideeffect alignstack inteldialect unwind",
                    => .ptr,
                };
            },
            .global => |global| return builder.ptrTypeAssumeCapacity(
                global.ptrConst(builder).addr_space,
            ),
        }
    }

    pub fn isZeroInit(self: Constant, builder: *const Builder) bool {
        switch (self.unwrap()) {
            .constant => |constant| {
                const item = builder.constant_items.get(constant);
                return switch (item.tag) {
                    .positive_integer => {
                        const extra: *align(@alignOf(std.math.big.Limb)) Integer =
                            @ptrCast(builder.constant_limbs.items[item.data..][0..Integer.limbs]);
                        const limbs = builder.constant_limbs
                            .items[item.data + Integer.limbs ..][0..extra.limbs_len];
                        return std.mem.eql(std.math.big.Limb, limbs, &.{0});
                    },
                    .half, .bfloat, .float => item.data == 0,
                    .double => {
                        const extra = builder.constantExtraData(Constant.Double, item.data);
                        return extra.lo == 0 and extra.hi == 0;
                    },
                    .fp128, .ppc_fp128 => {
                        const extra = builder.constantExtraData(Constant.Fp128, item.data);
                        return extra.lo_lo == 0 and extra.lo_hi == 0 and
                            extra.hi_lo == 0 and extra.hi_hi == 0;
                    },
                    .x86_fp80 => {
                        const extra = builder.constantExtraData(Constant.Fp80, item.data);
                        return extra.lo_lo == 0 and extra.lo_hi == 0 and extra.hi == 0;
                    },
                    .vector => {
                        var extra = builder.constantExtraDataTrail(Aggregate, item.data);
                        const len: u32 = @intCast(extra.data.type.aggregateLen(builder));
                        const vals = extra.trail.next(len, Constant, builder);
                        for (vals) |val| if (!val.isZeroInit(builder)) return false;
                        return true;
                    },
                    .null, .zeroinitializer => true,
                    else => false,
                };
            },
            .global => return false,
        }
    }

    pub fn getBase(self: Constant, builder: *const Builder) Global.Index {
        var cur = self;
        while (true) switch (cur.unwrap()) {
            .constant => |constant| {
                const item = builder.constant_items.get(constant);
                switch (item.tag) {
                    .ptrtoint,
                    .inttoptr,
                    .bitcast,
                    => cur = builder.constantExtraData(Cast, item.data).val,
                    .getelementptr => cur = builder.constantExtraData(GetElementPtr, item.data).base,
                    .add => {
                        const extra = builder.constantExtraData(Binary, item.data);
                        const lhs_base = extra.lhs.getBase(builder);
                        const rhs_base = extra.rhs.getBase(builder);
                        return if (lhs_base != .none and rhs_base != .none)
                            .none
                        else if (lhs_base != .none) lhs_base else rhs_base;
                    },
                    .sub => {
                        const extra = builder.constantExtraData(Binary, item.data);
                        if (extra.rhs.getBase(builder) != .none) return .none;
                        cur = extra.lhs;
                    },
                    else => return .none,
                }
            },
            .global => |global| switch (global.ptrConst(builder).kind) {
                .alias => |alias| cur = alias.ptrConst(builder).aliasee,
                .variable, .function => return global,
                .replaced => unreachable,
            },
        };
    }

    const FormatData = struct {
        constant: Constant,
        builder: *Builder,
        flags: FormatFlags,
    };
    fn format(data: FormatData, w: *Writer) Writer.Error!void {
        if (data.flags.comma) {
            if (data.constant == .no_init) return;
            try w.writeByte(',');
        }
        if (data.flags.space) {
            if (data.constant == .no_init) return;
            try w.writeByte(' ');
        }
        if (data.flags.percent)
            try w.print("{f} ", .{data.constant.typeOf(data.builder).fmt(data.builder, .percent)});
        assert(data.constant != .no_init);
        if (std.enums.tagName(Constant, data.constant)) |name| return w.writeAll(name);
        switch (data.constant.unwrap()) {
            .constant => |constant| {
                const item = data.builder.constant_items.get(constant);
                switch (item.tag) {
                    .positive_integer,
                    .negative_integer,
                    => |tag| {
                        const extra: *align(@alignOf(std.math.big.Limb)) const Integer =
                            @ptrCast(data.builder.constant_limbs.items[item.data..][0..Integer.limbs]);
                        const limbs = data.builder.constant_limbs
                            .items[item.data + Integer.limbs ..][0..extra.limbs_len];
                        const bigint: std.math.big.int.Const = .{
                            .limbs = limbs,
                            .positive = switch (tag) {
                                .positive_integer => true,
                                .negative_integer => false,
                                else => unreachable,
                            },
                        };
                        const ExpectedContents = extern struct {
                            const expected_limbs = @divExact(512, @bitSizeOf(std.math.big.Limb));
                            string: [
                                (std.math.big.int.Const{
                                    .limbs = &([1]std.math.big.Limb{
                                        maxInt(std.math.big.Limb),
                                    } ** expected_limbs),
                                    .positive = false,
                                }).sizeInBaseUpperBound(10)
                            ]u8,
                            limbs: [
                                std.math.big.int.calcToStringLimbsBufferLen(expected_limbs, 10)
                            ]std.math.big.Limb,
                        };
                        var stack align(@alignOf(ExpectedContents)) =
                            std.heap.stackFallback(@sizeOf(ExpectedContents), data.builder.gpa);
                        const allocator = stack.get();
                        const str = bigint.toStringAlloc(allocator, 10, undefined) catch return error.WriteFailed;
                        defer allocator.free(str);
                        try w.writeAll(str);
                    },
                    .half,
                    .bfloat,
                    => |tag| try w.print("0x{c}{X:0>4}", .{ @as(u8, switch (tag) {
                        .half => 'H',
                        .bfloat => 'R',
                        else => unreachable,
                    }), item.data >> switch (tag) {
                        .half => 0,
                        .bfloat => 16,
                        else => unreachable,
                    } }),
                    .float => {
                        const Float = struct {
                            fn Repr(comptime T: type) type {
                                return packed struct(std.meta.Int(.unsigned, @bitSizeOf(T))) {
                                    mantissa: std.meta.Int(.unsigned, std.math.floatMantissaBits(T)),
                                    exponent: std.meta.Int(.unsigned, std.math.floatExponentBits(T)),
                                    sign: u1,
                                };
                            }
                        };
                        const Mantissa64 = @FieldType(Float.Repr(f64), "mantissa");
                        const Exponent32 = @FieldType(Float.Repr(f32), "exponent");
                        const Exponent64 = @FieldType(Float.Repr(f64), "exponent");

                        const repr: Float.Repr(f32) = @bitCast(item.data);
                        const denormal_shift = switch (repr.exponent) {
                            std.math.minInt(Exponent32) => @as(
                                std.math.Log2Int(Mantissa64),
                                @clz(repr.mantissa),
                            ) + 1,
                            else => 0,
                        };
                        try w.print("0x{X:0>16}", .{@as(u64, @bitCast(Float.Repr(f64){
                            .mantissa = std.math.shl(
                                Mantissa64,
                                repr.mantissa,
                                std.math.floatMantissaBits(f64) - std.math.floatMantissaBits(f32) +
                                    denormal_shift,
                            ),
                            .exponent = switch (repr.exponent) {
                                std.math.minInt(Exponent32) => if (repr.mantissa > 0)
                                    @as(Exponent64, std.math.floatExponentMin(f32) +
                                        std.math.floatExponentMax(f64)) - denormal_shift
                                else
                                    std.math.minInt(Exponent64),
                                else => @as(Exponent64, repr.exponent) +
                                    (std.math.floatExponentMax(f64) - std.math.floatExponentMax(f32)),
                                maxInt(Exponent32) => maxInt(Exponent64),
                            },
                            .sign = repr.sign,
                        }))});
                    },
                    .double => {
                        const extra = data.builder.constantExtraData(Double, item.data);
                        try w.print("0x{X:0>8}{X:0>8}", .{ extra.hi, extra.lo });
                    },
                    .fp128,
                    .ppc_fp128,
                    => |tag| {
                        const extra = data.builder.constantExtraData(Fp128, item.data);
                        try w.print("0x{c}{X:0>8}{X:0>8}{X:0>8}{X:0>8}", .{
                            @as(u8, switch (tag) {
                                .fp128 => 'L',
                                .ppc_fp128 => 'M',
                                else => unreachable,
                            }),
                            extra.lo_hi,
                            extra.lo_lo,
                            extra.hi_hi,
                            extra.hi_lo,
                        });
                    },
                    .x86_fp80 => {
                        const extra = data.builder.constantExtraData(Fp80, item.data);
                        try w.print("0xK{X:0>4}{X:0>8}{X:0>8}", .{
                            extra.hi, extra.lo_hi, extra.lo_lo,
                        });
                    },
                    .null,
                    .none,
                    .zeroinitializer,
                    .undef,
                    .poison,
                    => |tag| try w.writeAll(@tagName(tag)),
                    .structure,
                    .packed_structure,
                    .array,
                    .vector,
                    => |tag| {
                        var extra = data.builder.constantExtraDataTrail(Aggregate, item.data);
                        const len: u32 = @intCast(extra.data.type.aggregateLen(data.builder));
                        const vals = extra.trail.next(len, Constant, data.builder);
                        try w.writeAll(switch (tag) {
                            .structure => "{ ",
                            .packed_structure => "<{ ",
                            .array => "[",
                            .vector => "<",
                            else => unreachable,
                        });
                        for (vals, 0..) |val, index| {
                            if (index > 0) try w.writeAll(", ");
                            try w.print("{f}", .{val.fmt(data.builder, .{ .percent = true })});
                        }
                        try w.writeAll(switch (tag) {
                            .structure => " }",
                            .packed_structure => " }>",
                            .array => "]",
                            .vector => ">",
                            else => unreachable,
                        });
                    },
                    .splat => {
                        const extra = data.builder.constantExtraData(Splat, item.data);
                        const len = extra.type.vectorLen(data.builder);
                        try w.writeByte('<');
                        for (0..len) |index| {
                            if (index > 0) try w.writeAll(", ");
                            try w.print("{f}", .{extra.value.fmt(data.builder, .{ .percent = true })});
                        }
                        try w.writeByte('>');
                    },
                    .string => try w.print("c{f}", .{
                        @as(String, @enumFromInt(item.data)).fmtQ(data.builder),
                    }),
                    .blockaddress => |tag| {
                        const extra = data.builder.constantExtraData(BlockAddress, item.data);
                        const function = extra.function.ptrConst(data.builder);
                        try w.print("{s}({f}, {f})", .{
                            @tagName(tag),
                            function.global.fmt(data.builder),
                            extra.block.toInst(function).fmt(extra.function, data.builder, .{}),
                        });
                    },
                    .dso_local_equivalent,
                    .no_cfi,
                    => |tag| {
                        const function: Function.Index = @enumFromInt(item.data);
                        try w.print("{s} {f}", .{
                            @tagName(tag),
                            function.ptrConst(data.builder).global.fmt(data.builder),
                        });
                    },
                    .trunc,
                    .ptrtoint,
                    .inttoptr,
                    .bitcast,
                    .addrspacecast,
                    => |tag| {
                        const extra = data.builder.constantExtraData(Cast, item.data);
                        try w.print("{s} ({f} to {f})", .{
                            @tagName(tag),
                            extra.val.fmt(data.builder, .{ .percent = true }),
                            extra.type.fmt(data.builder, .percent),
                        });
                    },
                    .getelementptr,
                    .@"getelementptr inbounds",
                    => |tag| {
                        var extra = data.builder.constantExtraDataTrail(GetElementPtr, item.data);
                        const indices =
                            extra.trail.next(extra.data.info.indices_len, Constant, data.builder);
                        try w.print("{s} ({f}, {f}", .{
                            @tagName(tag),
                            extra.data.type.fmt(data.builder, .percent),
                            extra.data.base.fmt(data.builder, .{ .percent = true }),
                        });
                        for (indices) |index| try w.print(", {f}", .{index.fmt(data.builder, .{ .percent = true })});
                        try w.writeByte(')');
                    },
                    .add,
                    .@"add nsw",
                    .@"add nuw",
                    .sub,
                    .@"sub nsw",
                    .@"sub nuw",
                    .shl,
                    .xor,
                    => |tag| {
                        const extra = data.builder.constantExtraData(Binary, item.data);
                        try w.print("{s} ({f}, {f})", .{
                            @tagName(tag),
                            extra.lhs.fmt(data.builder, .{ .percent = true }),
                            extra.rhs.fmt(data.builder, .{ .percent = true }),
                        });
                    },
                    .@"asm",
                    .@"asm sideeffect",
                    .@"asm alignstack",
                    .@"asm sideeffect alignstack",
                    .@"asm inteldialect",
                    .@"asm sideeffect inteldialect",
                    .@"asm alignstack inteldialect",
                    .@"asm sideeffect alignstack inteldialect",
                    .@"asm unwind",
                    .@"asm sideeffect unwind",
                    .@"asm alignstack unwind",
                    .@"asm sideeffect alignstack unwind",
                    .@"asm inteldialect unwind",
                    .@"asm sideeffect inteldialect unwind",
                    .@"asm alignstack inteldialect unwind",
                    .@"asm sideeffect alignstack inteldialect unwind",
                    => |tag| {
                        const extra = data.builder.constantExtraData(Assembly, item.data);
                        try w.print("{s} {f}, {f}", .{
                            @tagName(tag),
                            extra.assembly.fmtQ(data.builder),
                            extra.constraints.fmtQ(data.builder),
                        });
                    },
                }
            },
            .global => |global| try w.print("{f}", .{global.fmt(data.builder)}),
        }
    }
    pub fn fmt(self: Constant, builder: *Builder, flags: FormatFlags) std.fmt.Alt(FormatData, format) {
        return .{ .data = .{
            .constant = self,
            .builder = builder,
            .flags = flags,
        } };
    }
};

pub const Value = enum(u32) {
    none = maxInt(u31),
    false = first_constant + @intFromEnum(Constant.false),
    true = first_constant + @intFromEnum(Constant.true),
    @"0" = first_constant + @intFromEnum(Constant.@"0"),
    @"1" = first_constant + @intFromEnum(Constant.@"1"),
    _,

    const first_constant = 1 << 30;
    const first_metadata = 1 << 31;

    pub fn unwrap(self: Value) union(enum) {
        instruction: Function.Instruction.Index,
        constant: Constant,
        metadata: Metadata,
    } {
        return if (@intFromEnum(self) < first_constant)
            .{ .instruction = @enumFromInt(@intFromEnum(self)) }
  
```
