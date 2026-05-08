```
 \\
        \\ // d0 = func(a1)
        \\ move.l %%a1, -(%%sp)
        \\ jsr (%%a0)
        \\
        \\ // syscall(d0, d1)
        \\ move.l %%d0, %%d1
        \\ move.l #1, %%d0 // SYS_exit
        \\ trap #0
    );
}

pub const restore = restore_rt;

pub fn restore_rt() callconv(.naked) noreturn {
    asm volatile ("trap #0"
        :
        : [number] "{d0}" (@intFromEnum(SYS.rt_sigreturn)),
    );
}

pub const time_t = i32;

// No VDSO used as of glibc 112a0ae18b831bf31f44d81b82666980312511d6.
pub const VDSO = void;



---
File: /std/os/linux/mips.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
        : .{ .r1 = true, .r3 = true, .r4 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall_pipe(fd: *[2]i32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 2f
        \\ subu $v0, $zero, $v0
        \\ b 2f
        \\1:
        \\ sw $v0, 0($a0)
        \\ sw $v1, 4($a0)
        \\2:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(SYS.pipe)),
          [fd] "{$4}" (fd),
        : .{ .r1 = true, .r3 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall1(number: SYS, arg1: u32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
        : .{ .r1 = true, .r3 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall2(number: SYS, arg1: u32, arg2: u32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
        : .{ .r1 = true, .r3 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall3(number: SYS, arg1: u32, arg2: u32, arg3: u32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
        : .{ .r1 = true, .r3 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall4(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

// NOTE: The o32 calling convention requires the callee to reserve 16 bytes for
// the first four arguments even though they're passed in $a0-$a3.

pub fn syscall5(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32, arg5: u32) u32 {
    return asm volatile (
        \\ subu $sp, $sp, 24
        \\ sw %[arg5], 16($sp)
        \\ syscall
        \\ addu $sp, $sp, 24
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
          [arg5] "r" (arg5),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
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
    return asm volatile (
        \\ subu $sp, $sp, 24
        \\ sw %[arg5], 16($sp)
        \\ sw %[arg6], 20($sp)
        \\ syscall
        \\ addu $sp, $sp, 24
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
          [arg5] "r" (arg5),
          [arg6] "r" (arg6),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall7(
    number: SYS,
    arg1: u32,
    arg2: u32,
    arg3: u32,
    arg4: u32,
    arg5: u32,
    arg6: u32,
    arg7: u32,
) u32 {
    return asm volatile (
        \\ subu $sp, $sp, 32
        \\ sw %[arg5], 16($sp)
        \\ sw %[arg6], 20($sp)
        \\ sw %[arg7], 24($sp)
        \\ syscall
        \\ addu $sp, $sp, 32
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
          [arg5] "r" (arg5),
          [arg6] "r" (arg6),
          [arg7] "r" (arg7),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn clone() callconv(.naked) u32 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         a0,   a1,    a2,    a3,  +0,   +4,  +8
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         v0         a0,    a1,    a2,   a3,  +0
    asm volatile (
        \\ # Save function pointer and argument pointer on new thread stack
        \\ and $a1, $a1, -8
        \\ subu $a1, $a1, 16
        \\ sw $a0, 0($a1)
        \\ sw $a3, 4($a1)
        \\
        \\ # Shuffle (fn,sp,fl,arg,ptid,tls,ctid) to (fl,sp,ptid,tls,ctid)
        \\ move $a0, $a2
        \\ lw $a2, 16($sp)
        \\ lw $a3, 20($sp)
        \\ lw $t1, 24($sp)
        \\ subu $sp, $sp, 16
        \\ sw $t1, 16($sp)
        \\ li $v0, 4120 # SYS_clone
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 2f
        \\ subu $v0, $zero, $v0
        \\ b 2f
        \\1:
        \\ beq $v0, $zero, 3f
        \\2:
        \\ addu $sp, $sp, 16
        \\ jr $ra
        \\3:
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\ .cfi_undefined $ra
    );
    asm volatile (
        \\ move $fp, $zero
        \\ move $ra, $zero
        \\
        \\ lw $t9, 0($sp)
        \\ lw $a0, 4($sp)
        \\ jalr $t9
        \\
        \\ move $a0, $v0
        \\ li $v0, 4001 # SYS_exit
        \\ syscall
    );
}

pub const VDSO = struct {
    pub const CGT_SYM = "__vdso_clock_gettime";
    pub const CGT_VER = "LINUX_2.6";
};

pub const time_t = i32;



---
File: /std/os/linux/mips64.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u64 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ dsubu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u64),
        : [number] "{$2}" (@intFromEnum(number)),
        : .{ .r1 = true, .r3 = true, .r4 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall_pipe(fd: *[2]i32) u64 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 2f
        \\ dsubu $v0, $zero, $v0
        \\ b 2f
        \\1:
        \\ sw $v0, 0($a0)
        \\ sw $v1, 4($a0)
        \\2:
        : [ret] "={$2}" (-> u64),
        : [number] "{$2}" (@intFromEnum(SYS.pipe)),
          [fd] "{$4}" (fd),
        : .{ .r1 = true, .r3 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall1(number: SYS, arg1: u64) u64 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ dsubu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u64),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
        : .{ .r1 = true, .r3 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall2(number: SYS, arg1: u64, arg2: u64) u64 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ dsubu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u64),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
        : .{ .r1 = true, .r3 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall3(number: SYS, arg1: u64, arg2: u64, arg3: u64) u64 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ dsubu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u64),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
        : .{ .r1 = true, .r3 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall4(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64) u64 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ dsubu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u64),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall5(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64, arg5: u64) u64 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ dsubu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u64),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
          [arg5] "{$8}" (arg5),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
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
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ dsubu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u64),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
          [arg5] "{$8}" (arg5),
          [arg6] "{$9}" (arg6),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn clone() callconv(.naked) u64 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         a0,   a1,    a2,    a3,  a4,   a5,  a6
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         v0         a0,    a1,    a2,   a3,  a4
    asm volatile (
        \\ # Save function pointer and argument pointer on new thread stack
        \\ and $a1, $a1, -16
        \\ dsubu $a1, $a1, 16
        \\ sd $a0, 0($a1)
        \\ sd $a3, 8($a1)
        \\
        \\ # Shuffle (fn,sp,fl,arg,ptid,tls,ctid) to (fl,sp,ptid,tls,ctid)
        \\ move $a0, $a2
        \\ move $a2, $a4
        \\ move $a3, $a5
        \\ move $a4, $a6
        \\ li $v0, 5055 # SYS_clone
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 2f
        \\ dsubu $v0, $zero, $v0
        \\ b 2f
        \\1:
        \\ beq $v0, $zero, 3f
        \\2:
        \\ jr $ra
        \\3:
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\ .cfi_undefined $ra
    );
    asm volatile (
        \\ move $fp, $zero
        \\ move $ra, $zero
        \\
        \\ ld $t9, 0($sp)
        \\ ld $a0, 8($sp)
        \\ jalr $t9
        \\
        \\ move $a0, $v0
        \\ li $v0, 5058 # SYS_exit
        \\ syscall
    );
}

pub const VDSO = struct {
    pub const CGT_SYM = "__vdso_clock_gettime";
    pub const CGT_VER = "LINUX_2.6";
};

pub const time_t = i32;



---
File: /std/os/linux/mipsn32.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
        : .{ .r1 = true, .r3 = true, .r4 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall_pipe(fd: *[2]i32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 2f
        \\ subu $v0, $zero, $v0
        \\ b 2f
        \\1:
        \\ sw $v0, 0($a0)
        \\ sw $v1, 4($a0)
        \\2:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(SYS.pipe)),
          [fd] "{$4}" (fd),
        : .{ .r1 = true, .r3 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall1(number: SYS, arg1: u32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
        : .{ .r1 = true, .r3 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall2(number: SYS, arg1: u32, arg2: u32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
        : .{ .r1 = true, .r3 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall3(number: SYS, arg1: u32, arg2: u32, arg3: u32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
        : .{ .r1 = true, .r3 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall4(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn syscall5(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32, arg5: u32) u32 {
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
          [arg5] "{$8}" (arg5),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
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
    return asm volatile (
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 1f
        \\ subu $v0, $zero, $v0
        \\1:
        : [ret] "={$2}" (-> u32),
        : [number] "{$2}" (@intFromEnum(number)),
          [arg1] "{$4}" (arg1),
          [arg2] "{$5}" (arg2),
          [arg3] "{$6}" (arg3),
          [arg4] "{$7}" (arg4),
          [arg5] "{$8}" (arg5),
          [arg6] "{$9}" (arg6),
        : .{ .r1 = true, .r3 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .r13 = true, .r14 = true, .r15 = true, .r24 = true, .r25 = true, .hi = true, .lo = true, .memory = true });
}

pub fn clone() callconv(.naked) u32 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         a0,   a1,    a2,    a3,  a4,   a5,  a6
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         v0         a0,    a1,    a2,   a3,  a4
    asm volatile (
        \\ # Save function pointer and argument pointer on new thread stack
        \\ and $a1, $a1, -16
        \\ subu $a1, $a1, 16
        \\ sw $a0, 0($a1)
        \\ sw $a3, 4($a1)
        \\
        \\ # Shuffle (fn,sp,fl,arg,ptid,tls,ctid) to (fl,sp,ptid,tls,ctid)
        \\ move $a0, $a2
        \\ move $a2, $a4
        \\ move $a3, $a5
        \\ move $a4, $a6
        \\ li $v0, 6055 # SYS_clone
        \\ syscall
        \\ beq $a3, $zero, 1f
        \\ blez $v0, 2f
        \\ subu $v0, $zero, $v0
        \\ b 2f
        \\1:
        \\ beq $v0, $zero, 3f
        \\2:
        \\ jr $ra
        \\3:
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\ .cfi_undefined $ra
    );
    asm volatile (
        \\ move $fp, $zero
        \\ move $ra, $zero
        \\
        \\ ld $t9, 0($sp)
        \\ ld $a0, 4($sp)
        \\ jalr $t9
        \\
        \\ move $a0, $v0
        \\ li $v0, 6058 # SYS_exit
        \\ syscall
    );
}

pub const VDSO = struct {
    pub const CGT_SYM = "__vdso_clock_gettime";
    pub const CGT_VER = "LINUX_2.6";
};

pub const time_t = i32;



---
File: /std/os/linux/or1k.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u32 {
    return asm volatile (
        \\ l.sys 1
        : [ret] "={r11}" (-> u32),
        : [number] "{r11}" (@intFromEnum(number)),
        : .{ .r3 = true, .r4 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r12 = true, .r13 = true, .r15 = true, .r17 = true, .r19 = true, .r21 = true, .r23 = true, .r25 = true, .r27 = true, .r29 = true, .r31 = true, .memory = true });
}

pub fn syscall1(number: SYS, arg1: u32) u32 {
    return asm volatile (
        \\ l.sys 1
        : [ret] "={r11}" (-> u32),
        : [number] "{r11}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
        : .{ .r4 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r12 = true, .r13 = true, .r15 = true, .r17 = true, .r19 = true, .r21 = true, .r23 = true, .r25 = true, .r27 = true, .r29 = true, .r31 = true, .memory = true });
}

pub fn syscall2(number: SYS, arg1: u32, arg2: u32) u32 {
    return asm volatile (
        \\ l.sys 1
        : [ret] "={r11}" (-> u32),
        : [number] "{r11}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
        : .{ .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r12 = true, .r13 = true, .r15 = true, .r17 = true, .r19 = true, .r21 = true, .r23 = true, .r25 = true, .r27 = true, .r29 = true, .r31 = true, .memory = true });
}

pub fn syscall3(number: SYS, arg1: u32, arg2: u32, arg3: u32) u32 {
    return asm volatile (
        \\ l.sys 1
        : [ret] "={r11}" (-> u32),
        : [number] "{r11}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
        : .{ .r6 = true, .r7 = true, .r8 = true, .r12 = true, .r13 = true, .r15 = true, .r17 = true, .r19 = true, .r21 = true, .r23 = true, .r25 = true, .r27 = true, .r29 = true, .r31 = true, .memory = true });
}

pub fn syscall4(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32) u32 {
    return asm volatile (
        \\ l.sys 1
        : [ret] "={r11}" (-> u32),
        : [number] "{r11}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
          [arg4] "{r6}" (arg4),
        : .{ .r7 = true, .r8 = true, .r12 = true, .r13 = true, .r15 = true, .r17 = true, .r19 = true, .r21 = true, .r23 = true, .r25 = true, .r27 = true, .r29 = true, .r31 = true, .memory = true });
}

pub fn syscall5(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32, arg5: u32) u32 {
    return asm volatile (
        \\ l.sys 1
        : [ret] "={r11}" (-> u32),
        : [number] "{r11}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
          [arg4] "{r6}" (arg4),
          [arg5] "{r7}" (arg5),
        : .{ .r8 = true, .r12 = true, .r13 = true, .r15 = true, .r17 = true, .r19 = true, .r21 = true, .r23 = true, .r25 = true, .r27 = true, .r29 = true, .r31 = true, .memory = true });
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
    return asm volatile (
        \\ l.sys 1
        : [ret] "={r11}" (-> u32),
        : [number] "{r11}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
          [arg4] "{r6}" (arg4),
          [arg5] "{r7}" (arg5),
          [arg6] "{r8}" (arg6),
        : .{ .r12 = true, .r13 = true, .r15 = true, .r17 = true, .r19 = true, .r21 = true, .r23 = true, .r25 = true, .r27 = true, .r29 = true, .r31 = true, .memory = true });
}

pub fn clone() callconv(.naked) u32 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         r3,   r4,    r5,    r6,  r7,   r8,  +0
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         r11        r3,    r4,    r5,   r6,  r7
    asm volatile (
        \\ # Save function pointer and argument pointer on new thread stack
        \\ l.andi r4, r4, -4
        \\ l.addi r4, r4, -8
        \\ l.sw 0(r4), r3
        \\ l.sw 4(r4), r6
        \\
        \\ # Shuffle (fn,sp,fl,arg,ptid,tls,ctid) to (fl,sp,ptid,tls,ctid)
        \\ l.ori r11, r0, 220 # SYS_clone
        \\ l.ori r3, r5, 0
        \\ l.ori r5, r7, 0
        \\ l.ori r6, r8, 0
        \\ l.lwz r7, 0(r1)
        \\ l.sys 1
        \\ l.sfeqi r11, 0
        \\ l.bf 1f
        \\ l.jr r9
        \\1:
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\ .cfi_undefined r9
    );
    asm volatile (
        \\ l.ori r2, r0, 0
        \\ l.ori r9, r0, 0
        \\
        \\ l.lwz r11, 0(r1)
        \\ l.lwz r3, 4(r1)
        \\ l.jalr r11
        \\
        \\ l.ori r3, r11, 0
        \\ l.ori r11, r0, 93 # SYS_exit
        \\ l.sys 1
    );
}

pub const VDSO = void;

pub const time_t = i32;



---
File: /std/os/linux/powerpc.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u32 {
    // r0 is both an input register and a clobber. musl and glibc achieve this with
    // a "+" constraint, which isn't supported in Zig, so instead we separately list
    // r0 as both an input and an output. (Listing it as an input and a clobber would
    // cause the C backend to emit invalid code; see #25209.)
    var r0_out: u32 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u32),
          [r0_out] "={r0}" (r0_out),
        : [number] "{r0}" (@intFromEnum(number)),
        : .{ .memory = true, .cr0 = true, .r4 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall1(number: SYS, arg1: u32) u32 {
    // r0 is both an input and a clobber.
    var r0_out: u32 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u32),
          [r0_out] "={r0}" (r0_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
        : .{ .memory = true, .cr0 = true, .r4 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall2(number: SYS, arg1: u32, arg2: u32) u32 {
    // These registers are both inputs and clobbers.
    var r0_out: u32 = undefined;
    var r4_out: u32 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u32),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
        : .{ .memory = true, .cr0 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall3(number: SYS, arg1: u32, arg2: u32, arg3: u32) u32 {
    // These registers are both inputs and clobbers.
    var r0_out: u32 = undefined;
    var r4_out: u32 = undefined;
    var r5_out: u32 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u32),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
          [r5_out] "={r5}" (r5_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
        : .{ .memory = true, .cr0 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall4(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32) u32 {
    // These registers are both inputs and clobbers.
    var r0_out: u32 = undefined;
    var r4_out: u32 = undefined;
    var r5_out: u32 = undefined;
    var r6_out: u32 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u32),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
          [r5_out] "={r5}" (r5_out),
          [r6_out] "={r6}" (r6_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
          [arg4] "{r6}" (arg4),
        : .{ .memory = true, .cr0 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall5(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32, arg5: u32) u32 {
    // These registers are both inputs and clobbers.
    var r0_out: u32 = undefined;
    var r4_out: u32 = undefined;
    var r5_out: u32 = undefined;
    var r6_out: u32 = undefined;
    var r7_out: u32 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u32),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
          [r5_out] "={r5}" (r5_out),
          [r6_out] "={r6}" (r6_out),
          [r7_out] "={r7}" (r7_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
          [arg4] "{r6}" (arg4),
          [arg5] "{r7}" (arg5),
        : .{ .memory = true, .cr0 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
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
    // These registers are both inputs and clobbers.
    var r0_out: u32 = undefined;
    var r4_out: u32 = undefined;
    var r5_out: u32 = undefined;
    var r6_out: u32 = undefined;
    var r7_out: u32 = undefined;
    var r8_out: u32 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u32),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
          [r5_out] "={r5}" (r5_out),
          [r6_out] "={r6}" (r6_out),
          [r7_out] "={r7}" (r7_out),
          [r8_out] "={r8}" (r8_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
          [arg4] "{r6}" (arg4),
          [arg5] "{r7}" (arg5),
          [arg6] "{r8}" (arg6),
        : .{ .memory = true, .cr0 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn clone() callconv(.naked) u32 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         3,    4,     5,     6,   7,    8,   9
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         0          3,     4,     5,    6,   7
    asm volatile (
        \\ # store non-volatile regs r29, r30 on stack in order to put our
        \\ # start func and its arg there
        \\ stwu 29, -16(1)
        \\ stw 30, 4(1)
        \\
        \\ # save r3 (func) into r29, and r6(arg) into r30
        \\ mr 29, 3
        \\ mr 30, 6
        \\
        \\ # create initial stack frame for new thread
        \\ clrrwi 4, 4, 4
        \\ li 0, 0
        \\ stwu 0, -16(4)
        \\
        \\ #move c into first arg
        \\ mr 3, 5
        \\ #mr 4, 4
        \\ mr 5, 7
        \\ mr 6, 8
        \\ mr 7, 9
        \\
        \\ # move syscall number into r0
        \\ li 0, 120 # SYS_clone
        \\
        \\ sc
        \\
        \\ # check for syscall error
        \\ bns+ 1f # jump to label 1 if no summary overflow.
        \\ #else
        \\ neg 3, 3 #negate the result (errno)
        \\ 1:
        \\ # compare sc result with 0
        \\ cmpwi cr7, 3, 0
        \\
        \\ # if not 0, restore stack and return
        \\ beq cr7, 2f
        \\ lwz 29, 0(1)
        \\ lwz 30, 4(1)
        \\ addi 1, 1, 16
        \\ blr
        \\
        \\ #else: we're the child
        \\ 2:
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\ .cfi_undefined lr
    );
    asm volatile (
        \\ li 31, 0
        \\ mtlr 0
        \\
        \\ #call funcptr: move arg (d) into r3
        \\ mr 3, 30
        \\ #move r29 (funcptr) into CTR reg
        \\ mtctr 29
        \\ # call CTR reg
        \\ bctrl
        \\ # mov SYS_exit into r0 (the exit param is already in r3)
        \\ li 0, 1
        \\ sc
    );
}

pub fn restore() callconv(.naked) noreturn {
    switch (builtin.zig_backend) {
        .stage2_c => asm volatile (
            \\ li 0, %[number]
            \\ sc
            :
            : [number] "i" (@intFromEnum(SYS.sigreturn)),
        ),
        else => asm volatile (
            \\ sc
            :
            : [number] "{r0}" (@intFromEnum(SYS.sigreturn)),
        ),
    }
}

pub fn restore_rt() callconv(.naked) noreturn {
    switch (builtin.zig_backend) {
        .stage2_c => asm volatile (
            \\ li 0, %[number]
            \\ sc
            :
            : [number] "i" (@intFromEnum(SYS.rt_sigreturn)),
        ),
        else => asm volatile (
            \\ sc
            :
            : [number] "{r0}" (@intFromEnum(SYS.rt_sigreturn)),
        ),
    }
}

pub const VDSO = struct {
    pub const CGT_SYM = "__kernel_clock_gettime";
    pub const CGT_VER = "LINUX_2.6.15";
};

pub const time_t = i32;



---
File: /std/os/linux/powerpc64.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u64 {
    // r0 is both an input register and a clobber. musl and glibc achieve this with
    // a "+" constraint, which isn't supported in Zig, so instead we separately list
    // r0 as both an input and an output. (Listing it as an input and a clobber would
    // cause the C backend to emit invalid code; see #25209.)
    var r0_out: u64 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u64),
          [r0_out] "={r0}" (r0_out),
        : [number] "{r0}" (@intFromEnum(number)),
        : .{ .memory = true, .cr0 = true, .r4 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall1(number: SYS, arg1: u64) u64 {
    // r0 is both an input and a clobber.
    var r0_out: u64 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u64),
          [r0_out] "={r0}" (r0_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
        : .{ .memory = true, .cr0 = true, .r4 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall2(number: SYS, arg1: u64, arg2: u64) u64 {
    // These registers are both inputs and clobbers.
    var r0_out: u64 = undefined;
    var r4_out: u64 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u64),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
        : .{ .memory = true, .cr0 = true, .r5 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall3(number: SYS, arg1: u64, arg2: u64, arg3: u64) u64 {
    // These registers are both inputs and clobbers.
    var r0_out: u64 = undefined;
    var r4_out: u64 = undefined;
    var r5_out: u64 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u64),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
          [r5_out] "={r5}" (r5_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
        : .{ .memory = true, .cr0 = true, .r6 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall4(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64) u64 {
    // These registers are both inputs and clobbers.
    var r0_out: u64 = undefined;
    var r4_out: u64 = undefined;
    var r5_out: u64 = undefined;
    var r6_out: u64 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u64),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
          [r5_out] "={r5}" (r5_out),
          [r6_out] "={r6}" (r6_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
          [arg4] "{r6}" (arg4),
        : .{ .memory = true, .cr0 = true, .r7 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn syscall5(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64, arg5: u64) u64 {
    // These registers are both inputs and clobbers.
    var r0_out: u64 = undefined;
    var r4_out: u64 = undefined;
    var r5_out: u64 = undefined;
    var r6_out: u64 = undefined;
    var r7_out: u64 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u64),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
          [r5_out] "={r5}" (r5_out),
          [r6_out] "={r6}" (r6_out),
          [r7_out] "={r7}" (r7_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
          [arg4] "{r6}" (arg4),
          [arg5] "{r7}" (arg5),
        : .{ .memory = true, .cr0 = true, .r8 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
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
    // These registers are both inputs and clobbers.
    var r0_out: u64 = undefined;
    var r4_out: u64 = undefined;
    var r5_out: u64 = undefined;
    var r6_out: u64 = undefined;
    var r7_out: u64 = undefined;
    var r8_out: u64 = undefined;
    return asm volatile (
        \\ sc
        \\ bns+ 1f
        \\ neg 3, 3
        \\ 1:
        : [ret] "={r3}" (-> u64),
          [r0_out] "={r0}" (r0_out),
          [r4_out] "={r4}" (r4_out),
          [r5_out] "={r5}" (r5_out),
          [r6_out] "={r6}" (r6_out),
          [r7_out] "={r7}" (r7_out),
          [r8_out] "={r8}" (r8_out),
        : [number] "{r0}" (@intFromEnum(number)),
          [arg1] "{r3}" (arg1),
          [arg2] "{r4}" (arg2),
          [arg3] "{r5}" (arg3),
          [arg4] "{r6}" (arg4),
          [arg5] "{r7}" (arg5),
          [arg6] "{r8}" (arg6),
        : .{ .memory = true, .cr0 = true, .r9 = true, .r10 = true, .r11 = true, .r12 = true, .ctr = true, .xer = true });
}

pub fn clone() callconv(.naked) u64 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         3,    4,     5,     6,   7,    8,   9
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         0          3,     4,     5,    6,   7
    asm volatile (
        \\  # create initial stack frame for new thread
        \\  clrrdi 4, 4, 4
        \\  li     0, 0
        \\  stdu   0,-32(4)
        \\
        \\  # save fn and arg to child stack
        \\  std    3,  8(4)
        \\  std    6, 16(4)
        \\
        \\  # shuffle args into correct registers and call SYS_clone
        \\  mr    3, 5
        \\  #mr   4, 4
        \\  mr    5, 7
        \\  mr    6, 8
        \\  mr    7, 9
        \\  li    0, 120  # SYS_clone = 120
        \\  sc
        \\
        \\  # if error, negate return (errno)
        \\  bns+  1f
        \\  neg   3, 3
        \\
        \\1:
        \\  # if we're the parent, return
        \\  cmpwi cr7, 3, 0
        \\  bnelr cr7
        \\
        \\  # we're the child
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\  .cfi_undefined lr
    );
    asm volatile (
        \\  li    31, 0
        \\  mtlr   0
        \\
        \\  # call fn(arg)
        \\  ld     3, 16(1)
        \\  ld    12,  8(1)
        \\  mtctr 12
        \\  bctrl
        \\
        \\  # call SYS_exit. exit code is already in r3 from fn return value
        \\  li    0, 1    # SYS_exit = 1
        \\  sc
    );
}

pub fn restore() callconv(.naked) noreturn {
    switch (builtin.zig_backend) {
        .stage2_c => asm volatile (
            \\ li 0, %[number]
            \\ sc
            :
            : [number] "i" (@intFromEnum(SYS.sigreturn)),
        ),
        else => asm volatile (
            \\ sc
            :
            : [number] "{r0}" (@intFromEnum(SYS.sigreturn)),
        ),
    }
}

pub fn restore_rt() callconv(.naked) noreturn {
    switch (builtin.zig_backend) {
        .stage2_c => asm volatile (
            \\ li 0, %[number]
            \\ sc
            :
            : [number] "i" (@intFromEnum(SYS.rt_sigreturn)),
        ),
        else => asm volatile (
            \\ sc
            :
            : [number] "{r0}" (@intFromEnum(SYS.rt_sigreturn)),
        ),
    }
}

pub const VDSO = struct {
    pub const CGT_SYM = "__kernel_clock_gettime";
    pub const CGT_VER = "LINUX_2.6.15";
};

pub const time_t = i64;



---
File: /std/os/linux/riscv32.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u32 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u32),
        : [number] "{x17}" (@intFromEnum(number)),
        : .{ .memory = true });
}

pub fn syscall1(number: SYS, arg1: u32) u32 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u32),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
        : .{ .memory = true });
}

pub fn syscall2(number: SYS, arg1: u32, arg2: u32) u32 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u32),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
        : .{ .memory = true });
}

pub fn syscall3(number: SYS, arg1: u32, arg2: u32, arg3: u32) u32 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u32),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
          [arg3] "{x12}" (arg3),
        : .{ .memory = true });
}

pub fn syscall4(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32) u32 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u32),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
          [arg3] "{x12}" (arg3),
          [arg4] "{x13}" (arg4),
        : .{ .memory = true });
}

pub fn syscall5(number: SYS, arg1: u32, arg2: u32, arg3: u32, arg4: u32, arg5: u32) u32 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u32),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
          [arg3] "{x12}" (arg3),
          [arg4] "{x13}" (arg4),
          [arg5] "{x14}" (arg5),
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
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u32),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
          [arg3] "{x12}" (arg3),
          [arg4] "{x13}" (arg4),
          [arg5] "{x14}" (arg5),
          [arg6] "{x15}" (arg6),
        : .{ .memory = true });
}

pub fn clone() callconv(.naked) u32 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         a0,   a1,    a2,    a3,  a4,   a5,  a6
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         a7         a0,    a1,    a2,   a3,  a4
    asm volatile (
        \\    # Save func and arg to stack
        \\    addi a1, a1, -8
        \\    sw a0, 0(a1)
        \\    sw a3, 4(a1)
        \\
        \\    # Call SYS_clone
        \\    mv a0, a2
        \\    mv a2, a4
        \\    mv a3, a5
        \\    mv a4, a6
        \\    li a7, 220 # SYS_clone
        \\    ecall
        \\
        \\    beqz a0, 1f
        \\    # Parent
        \\    ret
        \\
        \\    # Child
        \\1:
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\    .cfi_undefined ra
    );
    asm volatile (
        \\    mv fp, zero
        \\    mv ra, zero
        \\
        \\    lw a1, 0(sp)
        \\    lw a0, 4(sp)
        \\    jalr a1
        \\
        \\    # Exit
        \\    li a7, 93 # SYS_exit
        \\    ecall
    );
}

pub const time_t = i64;

pub const VDSO = struct {
    pub const CGT_SYM = "__vdso_clock_gettime";
    pub const CGT_VER = "LINUX_4.15";
};



---
File: /std/os/linux/riscv64.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u64 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u64),
        : [number] "{x17}" (@intFromEnum(number)),
        : .{ .memory = true });
}

pub fn syscall1(number: SYS, arg1: u64) u64 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u64),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
        : .{ .memory = true });
}

pub fn syscall2(number: SYS, arg1: u64, arg2: u64) u64 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u64),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
        : .{ .memory = true });
}

pub fn syscall3(number: SYS, arg1: u64, arg2: u64, arg3: u64) u64 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u64),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
          [arg3] "{x12}" (arg3),
        : .{ .memory = true });
}

pub fn syscall4(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64) u64 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u64),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
          [arg3] "{x12}" (arg3),
          [arg4] "{x13}" (arg4),
        : .{ .memory = true });
}

pub fn syscall5(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64, arg5: u64) u64 {
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u64),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
          [arg3] "{x12}" (arg3),
          [arg4] "{x13}" (arg4),
          [arg5] "{x14}" (arg5),
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
    return asm volatile ("ecall"
        : [ret] "={x10}" (-> u64),
        : [number] "{x17}" (@intFromEnum(number)),
          [arg1] "{x10}" (arg1),
          [arg2] "{x11}" (arg2),
          [arg3] "{x12}" (arg3),
          [arg4] "{x13}" (arg4),
          [arg5] "{x14}" (arg5),
          [arg6] "{x15}" (arg6),
        : .{ .memory = true });
}

pub fn clone() callconv(.naked) u64 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         a0,   a1,    a2,    a3,  a4,   a5,  a6
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         a7         a0,    a1,    a2,   a3,  a4
    asm volatile (
        \\    # Save func and arg to stack
        \\    addi a1, a1, -16
        \\    sd a0, 0(a1)
        \\    sd a3, 8(a1)
        \\
        \\    # Call SYS_clone
        \\    mv a0, a2
        \\    mv a2, a4
        \\    mv a3, a5
        \\    mv a4, a6
        \\    li a7, 220 # SYS_clone
        \\    ecall
        \\
        \\    beqz a0, 1f
        \\    # Parent
        \\    ret
        \\
        \\    # Child
        \\1:
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\    .cfi_undefined ra
    );
    asm volatile (
        \\    mv fp, zero
        \\    mv ra, zero
        \\
        \\    ld a1, 0(sp)
        \\    ld a0, 8(sp)
        \\    jalr a1
        \\
        \\    # Exit
        \\    li a7, 93 # SYS_exit
        \\    ecall
    );
}

pub const time_t = i64;

pub const VDSO = struct {
    pub const CGT_SYM = "__vdso_clock_gettime";
    pub const CGT_VER = "LINUX_4.15";
};



---
File: /std/os/linux/s390x.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall0(number: SYS) u64 {
    return asm volatile ("svc 0"
        : [ret] "={r2}" (-> u64),
        : [number] "{r1}" (@intFromEnum(number)),
        : .{ .memory = true });
}

pub fn syscall1(number: SYS, arg1: u64) u64 {
    return asm volatile ("svc 0"
        : [ret] "={r2}" (-> u64),
        : [number] "{r1}" (@intFromEnum(number)),
          [arg1] "{r2}" (arg1),
        : .{ .memory = true });
}

pub fn syscall2(number: SYS, arg1: u64, arg2: u64) u64 {
    return asm volatile ("svc 0"
        : [ret] "={r2}" (-> u64),
        : [number] "{r1}" (@intFromEnum(number)),
          [arg1] "{r2}" (arg1),
          [arg2] "{r3}" (arg2),
        : .{ .memory = true });
}

pub fn syscall3(number: SYS, arg1: u64, arg2: u64, arg3: u64) u64 {
    return asm volatile ("svc 0"
        : [ret] "={r2}" (-> u64),
        : [number] "{r1}" (@intFromEnum(number)),
          [arg1] "{r2}" (arg1),
          [arg2] "{r3}" (arg2),
          [arg3] "{r4}" (arg3),
        : .{ .memory = true });
}

pub fn syscall4(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64) u64 {
    return asm volatile ("svc 0"
        : [ret] "={r2}" (-> u64),
        : [number] "{r1}" (@intFromEnum(number)),
          [arg1] "{r2}" (arg1),
          [arg2] "{r3}" (arg2),
          [arg3] "{r4}" (arg3),
          [arg4] "{r5}" (arg4),
        : .{ .memory = true });
}

pub fn syscall5(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64, arg5: u64) u64 {
    return asm volatile ("svc 0"
        : [ret] "={r2}" (-> u64),
        : [number] "{r1}" (@intFromEnum(number)),
          [arg1] "{r2}" (arg1),
          [arg2] "{r3}" (arg2),
          [arg3] "{r4}" (arg3),
          [arg4] "{r5}" (arg4),
          [arg5] "{r6}" (arg5),
        : .{ .memory = true });
}

pub fn syscall6(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64, arg5: u64, arg6: u64) u64 {
    return asm volatile ("svc 0"
        : [ret] "={r2}" (-> u64),
        : [number] "{r1}" (@intFromEnum(number)),
          [arg1] "{r2}" (arg1),
          [arg2] "{r3}" (arg2),
          [arg3] "{r4}" (arg3),
          [arg4] "{r5}" (arg4),
          [arg5] "{r6}" (arg5),
          [arg6] "{r7}" (arg6),
        : .{ .memory = true });
}

pub fn clone() callconv(.naked) u64 {
    asm volatile (
        \\# int clone(
        \\#    fn,      a = r2
        \\#    stack,   b = r3
        \\#    flags,   c = r4
        \\#    arg,     d = r5
        \\#    ptid,    e = r6
        \\#    tls,     f = *(r15+160)
        \\#    ctid)    g = *(r15+168)
        \\#
        \\# pseudo C code:
        \\# tid = syscall(SYS_clone,b,c,e,g,f);
        \\# if (!tid) syscall(SYS_exit, a(d));
        \\# return tid;
        \\
        \\# preserve call-saved register used as syscall arg
        \\stg  %%r6, 48(%%r15)
        \\
        \\# create initial stack frame for new thread
        \\nill %%r3, 0xfff8
        \\aghi %%r3, -160
        \\lghi %%r0, 0
        \\stg  %%r0, 0(%%r3)
        \\
        \\# save fn and arg to child stack
        \\stg  %%r2,  8(%%r3)
        \\stg  %%r5, 16(%%r3)
        \\
        \\# shuffle args into correct registers and call SYS_clone
        \\lgr  %%r2, %%r3
        \\lgr  %%r3, %%r4
        \\lgr  %%r4, %%r6
        \\lg   %%r5, 168(%%r15)
        \\lg   %%r6, 160(%%r15)
        \\svc  120
        \\
        \\# restore call-saved register
        \\lg   %%r6, 48(%%r15)
        \\
        \\# if error or if we're the parent, return
        \\ltgr %%r2, %%r2
        \\bnzr %%r14
        \\
        \\# we're the child
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\.cfi_undefined %%r14
    );
    asm volatile (
        \\lghi %%r11, 0
        \\lghi %%r14, 0
        \\
        \\# call fn(arg)
        \\lg   %%r1,  8(%%r15)
        \\lg   %%r2, 16(%%r15)
        \\basr %%r14, %%r1
        \\
        \\# call SYS_exit. exit code is already in r2 from fn return value
        \\svc  1
        \\
    );
}

pub fn restore() callconv(.naked) noreturn {
    asm volatile (
        \\svc 0
        :
        : [number] "{r1}" (@intFromEnum(SYS.sigreturn)),
    );
}

pub fn restore_rt() callconv(.naked) noreturn {
    asm volatile (
        \\svc 0
        :
        : [number] "{r1}" (@intFromEnum(SYS.rt_sigreturn)),
    );
}

pub const time_t = i64;

pub const VDSO = struct {
    pub const CGT_SYM = "__kernel_clock_gettime";
    pub const CGT_VER = "LINUX_2.6.29";
};



---
File: /std/os/linux/seccomp.zig
---

//! API bits for the Secure Computing facility in the Linux kernel, which allows
//! processes to restrict access to the system call API.
//!
//! Seccomp started life with a single "strict" mode, which only allowed calls
//! to read(2), write(2), _exit(2) and sigreturn(2). It turns out that this
//! isn't that useful for general-purpose applications, and so a mode that
//! utilizes user-supplied filters mode was added.
//!
//! Seccomp filters are classic BPF programs. Conceptually, a seccomp program
//! is attached to the kernel and is executed on each syscall. The "packet"
//! being validated is the `data` structure, and the verdict is an action that
//! the kernel performs on the calling process. The actions are variations on a
//! "pass" or "fail" result, where a pass allows the syscall to continue and a
//! fail blocks the syscall and returns some sort of error value. See the full
//! list of actions under ::RET for more information. Finally, only word-sized,
//! absolute loads (`ld [k]`) are supported to read from the `data` structure.
//!
//! There are some issues with the filter API that have traditionally made
//! writing them a pain:
//!
//! 1. Each CPU architecture supported by Linux has its own unique ABI and
//!    syscall API. It is not guaranteed that the syscall numbers and arguments
//!    are the same across architectures, or that they're even implemented. Thus,
//!    filters cannot be assumed to be portable without consulting documentation
//!    like syscalls(2) and testing on target hardware. This also requires
//!    checking the value of `data.arch` to make sure that a filter was compiled
//!    for the correct architecture.
//! 2. Many syscalls take an `unsigned long` or `size_t` argument, the size of
//!    which is dependant on the ABI. Since BPF programs execute in a 32-bit
//!    machine, validation of 64-bit arguments necessitates two load-and-compare
//!    instructions for the upper and lower words.
//! 3. A further wrinkle to the above is endianness. Unlike network packets,
//!    syscall data shares the endianness of the target machine. A filter
//!    compiled on a little-endian machine will not work on a big-endian one,
//!    and vice-versa. For example: Checking the upper 32-bits of `data.arg1`
//!    requires a load at `@offsetOf(data, "arg1") + 4` on big-endian systems
//!    and `@offsetOf(data, "arg1")` on little-endian systems. Endian-portable
//!    filters require adjusting these offsets at compile time, similar to how
//!    e.g. OpenSSH does[1].
//! 4. Syscalls with userspace implementations via the vDSO cannot be traced or
//!    filtered. The vDSO can be disabled or just ignored, which must be taken
//!    into account when writing filters.
//! 5. Software libraries -  especially dynamically loaded ones - tend to use
//!    more of the syscall API over time, thus filters must evolve with them.
//!    Static filters can result in reduced or even broken functionality when
//!    calling newer code from these libraries. This is known to happen with
//!    critical libraries like glibc[2].
//!
//! Some of these issues can be mitigated with help from Zig and the standard
//! library. Since the target CPU is known at compile time, the proper syscall
//! numbers are mixed into the `os` namespace under `std.os.SYS (see the code
//! for `arch_bits` in `os/linux.zig`). Referencing an unimplemented syscall
//! would be a compile error. Endian offsets can also be defined in a similar
//! manner to the OpenSSH example:
//!
//! ```zig
//! const offset = if (native_endian == .little) struct {
//!     pub const low = 0;
//!     pub const high = @sizeOf(u32);
//! } else struct {
//!     pub const low = @sizeOf(u32);
//!     pub const high = 0;
//! };
//! ```
//!
//! Unfortunately, there is no easy solution for issue 5. The most reliable
//! strategy is to keep testing; test newer Zig versions, different libcs,
//! different distros, and design your filter to accommodate all of them.
//! Alternatively, you could inject a filter at runtime. Since filters are
//! preserved across execve(2), a filter could be setup before executing your
//! program, without your program having any knowledge of this happening. This
//! is the method used by systemd[3] and Cloudflare's sandbox library[4].
//!
//! [1]: https://github.com/openssh/openssh-portable/blob/master/sandbox-seccomp-filter.c#L81
//! [2]: https://sourceware.org/legacy-ml/libc-alpha/2017-11/msg00246.html
//! [3]: https://www.freedesktop.org/software/systemd/man/systemd.exec.html#SystemCallFilter=
//! [4]: https://github.com/cloudflare/sandbox
//!
//! See Also
//! - seccomp(2), seccomp_unotify(2)
//! - https://www.kernel.org/doc/html/latest/userspace-api/seccomp_filter.html
const IOCTL = @import("ioctl.zig");

// Modes for the prctl(2) form `prctl(PR_SET_SECCOMP, mode)`
pub const MODE = struct {
    /// Seccomp not in use.
    pub const DISABLED = 0;
    /// Uses a hard-coded filter.
    pub const STRICT = 1;
    /// Uses a user-supplied filter.
    pub const FILTER = 2;
};

// Operations for the seccomp(2) form `seccomp(operation, flags, args)`
pub const SET_MODE_STRICT = 0;
pub const SET_MODE_FILTER = 1;
pub const GET_ACTION_AVAIL = 2;
pub const GET_NOTIF_SIZES = 3;

/// Bitflags for the SET_MODE_FILTER operation.
pub const FILTER_FLAG = struct {
    pub const TSYNC = 1 << 0;
    pub const LOG = 1 << 1;
    pub const SPEC_ALLOW = 1 << 2;
    pub const NEW_LISTENER = 1 << 3;
    pub const TSYNC_ESRCH = 1 << 4;
};

/// Action values for seccomp BPF programs.
/// The lower 16-bits are for optional return data.
/// The upper 16-bits are ordered from least permissive values to most.
pub const RET = struct {
    /// Kill the process.
    pub const KILL_PROCESS = 0x80000000;
    /// Kill the thread.
    pub const KILL_THREAD = 0x00000000;
    pub const KILL = KILL_THREAD;
    /// Disallow and force a SIGSYS.
    pub const TRAP = 0x00030000;
    /// Return an errno.
    pub const ERRNO = 0x00050000;
    /// Forward the syscall to a userspace supervisor to make a decision.
    pub const USER_NOTIF = 0x7fc00000;
    /// Pass to a tracer or disallow.
    pub const TRACE = 0x7ff00000;
    /// Allow after logging.
    pub const LOG = 0x7ffc0000;
    /// Allow.
    pub const ALLOW = 0x7fff0000;

    // Masks for the return value sections.
    pub const ACTION_FULL = 0xffff0000;
    pub const ACTION = 0x7fff0000;
    pub const DATA = 0x0000ffff;
};

pub const IOCTL_NOTIF = struct {
    pub const RECV = IOCTL.IOWR('!', 0, notif);
    pub const SEND = IOCTL.IOWR('!', 1, notif_resp);
    pub const ID_VALID = IOCTL.IOW('!', 2, u64);
    pub const ADDFD = IOCTL.IOW('!', 3, notif_addfd);
};

/// Tells the kernel that the supervisor allows the syscall to continue.
pub const USER_NOTIF_FLAG_CONTINUE = 1 << 0;

/// See seccomp_unotify(2).
pub const ADDFD_FLAG = struct {
    pub const SETFD = 1 << 0;
    pub const SEND = 1 << 1;
};

pub const data = extern struct {
    /// The system call number.
    nr: c_int,
    /// The CPU architecture/system call convention.
    /// One of the values defined in `std.os.linux.AUDIT`.
    arch: u32,
    instruction_pointer: u64,
    arg0: u64,
    arg1: u64,
    arg2: u64,
    arg3: u64,
    arg4: u64,
    arg5: u64,
};

/// Used with the ::GET_NOTIF_SIZES command to check if the kernel structures
/// have changed.
pub const notif_sizes = extern struct {
    /// Size of ::notif.
    notif: u16,
    /// Size of ::resp.
    notif_resp: u16,
    /// Size of ::data.
    data: u16,
};

pub const notif = extern struct {
    /// Unique notification cookie for each filter.
    id: u64,
    /// ID of the thread that triggered the notification.
    pid: u32,
    /// Bitmask for event information. Currently set to zero.
    flags: u32,
    /// The current system call data.
    data: data,
};

/// The decision payload the supervisor process sends to the kernel.
pub const notif_resp = extern struct {
    /// The filter cookie.
    id: u64,
    /// The return value for a spoofed syscall.
    val: i64,
    /// Set to zero for a spoofed success or a negative error number for a
    /// failure.
    @"error": i32,
    /// Bitmask containing the decision. Either USER_NOTIF_FLAG_CONTINUE to
    /// allow the syscall or zero to spoof the return values.
    flags: u32,
};

pub const notif_addfd = extern struct {
    id: u64,
    flags: u32,
    srcfd: u32,
    newfd: u32,
    newfd_flags: u32,
};



---
File: /std/os/linux/sparc64.zig
---

const builtin = @import("builtin");
const std = @import("../../std.zig");
const SYS = std.os.linux.SYS;

pub fn syscall_pipe(fd: *[2]i32) u64 {
    return asm volatile (
        \\ mov %[arg], %%g3
        \\ t 0x6d
        \\ bcc,pt %%xcc, 1f
        \\ nop
        \\ # Return the error code
        \\ ba 2f
        \\ neg %%o0
        \\1:
        \\ st %%o0, [%%g3+0]
        \\ st %%o1, [%%g3+4]
        \\ clr %%o0
        \\2:
        : [ret] "={o0}" (-> u64),
        : [number] "{g1}" (@intFromEnum(SYS.pipe)),
          [arg] "r" (fd),
        : .{ .memory = true, .g3 = true });
}

pub fn syscall_fork() u64 {
    // Linux/sparc64 fork() returns two values in %o0 and %o1:
    // - On the parent's side, %o0 is the child's PID and %o1 is 0.
    // - On the child's side, %o0 is the parent's PID and %o1 is 1.
    // We need to clear the child's %o0 so that the return values
    // conform to the libc convention.
    return asm volatile (
        \\ t 0x6d
        \\ bcc,pt %%xcc, 1f
        \\ nop
        \\ ba 2f
        \\ neg %%o0
        \\ 1:
        \\ # Clear the child's %%o0
        \\ dec %%o1
        \\ and %%o1, %%o0, %%o0
        \\ 2:
        : [ret] "={o0}" (-> u64),
        : [number] "{g1}" (@intFromEnum(SYS.fork)),
        : .{ .memory = true, .xcc = true, .o1 = true, .o2 = true, .o3 = true, .o4 = true, .o5 = true, .o7 = true });
}

pub fn syscall0(number: SYS) u64 {
    return asm volatile (
        \\ t 0x6d
        \\ bcc,pt %%xcc, 1f
        \\ nop
        \\ neg %%o0
        \\ 1:
        : [ret] "={o0}" (-> u64),
        : [number] "{g1}" (@intFromEnum(number)),
        : .{ .memory = true, .xcc = true, .o1 = true, .o2 = true, .o3 = true, .o4 = true, .o5 = true, .o7 = true });
}

pub fn syscall1(number: SYS, arg1: u64) u64 {
    return asm volatile (
        \\ t 0x6d
        \\ bcc,pt %%xcc, 1f
        \\ nop
        \\ neg %%o0
        \\ 1:
        : [ret] "={o0}" (-> u64),
        : [number] "{g1}" (@intFromEnum(number)),
          [arg1] "{o0}" (arg1),
        : .{ .memory = true, .xcc = true, .o1 = true, .o2 = true, .o3 = true, .o4 = true, .o5 = true, .o7 = true });
}

pub fn syscall2(number: SYS, arg1: u64, arg2: u64) u64 {
    return asm volatile (
        \\ t 0x6d
        \\ bcc,pt %%xcc, 1f
        \\ nop
        \\ neg %%o0
        \\ 1:
        : [ret] "={o0}" (-> u64),
        : [number] "{g1}" (@intFromEnum(number)),
          [arg1] "{o0}" (arg1),
          [arg2] "{o1}" (arg2),
        : .{ .memory = true, .xcc = true, .o1 = true, .o2 = true, .o3 = true, .o4 = true, .o5 = true, .o7 = true });
}

pub fn syscall3(number: SYS, arg1: u64, arg2: u64, arg3: u64) u64 {
    return asm volatile (
        \\ t 0x6d
        \\ bcc,pt %%xcc, 1f
        \\ nop
        \\ neg %%o0
        \\ 1:
        : [ret] "={o0}" (-> u64),
        : [number] "{g1}" (@intFromEnum(number)),
          [arg1] "{o0}" (arg1),
          [arg2] "{o1}" (arg2),
          [arg3] "{o2}" (arg3),
        : .{ .memory = true, .xcc = true, .o1 = true, .o2 = true, .o3 = true, .o4 = true, .o5 = true, .o7 = true });
}

pub fn syscall4(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64) u64 {
    return asm volatile (
        \\ t 0x6d
        \\ bcc,pt %%xcc, 1f
        \\ nop
        \\ neg %%o0
        \\ 1:
        : [ret] "={o0}" (-> u64),
        : [number] "{g1}" (@intFromEnum(number)),
          [arg1] "{o0}" (arg1),
          [arg2] "{o1}" (arg2),
          [arg3] "{o2}" (arg3),
          [arg4] "{o3}" (arg4),
        : .{ .memory = true, .xcc = true, .o1 = true, .o2 = true, .o3 = true, .o4 = true, .o5 = true, .o7 = true });
}

pub fn syscall5(number: SYS, arg1: u64, arg2: u64, arg3: u64, arg4: u64, arg5: u64) u64 {
    return asm volatile (
        \\ t 0x6d
        \\ bcc,pt %%xcc, 1f
        \\ nop
        \\ neg %%o0
        \\ 1:
        : [ret] "={o0}" (-> u64),
        : [number] "{g1}" (@intFromEnum(number)),
          [arg1] "{o0}" (arg1),
          [arg2] "{o1}" (arg2),
          [arg3] "{o2}" (arg3),
          [arg4] "{o3}" (arg4),
          [arg5] "{o4}" (arg5),
        : .{ .memory = true, .xcc = true, .o1 = true, .o2 = true, .o3 = true, .o4 = true, .o5 = true, .o7 = true });
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
    return asm volatile (
        \\ t 0x6d
        \\ bcc,pt %%xcc, 1f
        \\ nop
        \\ neg %%o0
        \\ 1:
        : [ret] "={o0}" (-> u64),
        : [number] "{g1}" (@intFromEnum(number)),
          [arg1] "{o0}" (arg1),
          [arg2] "{o1}" (arg2),
          [arg3] "{o2}" (arg3),
          [arg4] "{o3}" (arg4),
          [arg5] "{o4}" (arg5),
          [arg6] "{o5}" (arg6),
        : .{ .memory = true, .xcc = true, .o1 = true, .o2 = true, .o3 = true, .o4 = true, .o5 = true, .o7 = true });
}

pub fn clone() callconv(.naked) u64 {
    // __clone(func, stack, flags, arg, ptid, tls, ctid)
    //         i0,   i1,    i2,    i3,  i4,   i5,  sp
    //
    // syscall(SYS_clone, flags, stack, ptid, tls, ctid)
    //         g1         o0,    o1,    o2,   o3,  o4
    asm volatile (
        \\ save %%sp, -192, %%sp
        \\ # Save the func pointer and the arg pointer
        \\ mov %%i0, %%g2
        \\ mov %%i3, %%g3
        \\ # Shuffle the arguments
        \\ mov 217, %%g1 // SYS_clone
        \\ mov %%i2, %%o0
        \\ # Add some extra space for the initial frame
        \\ sub %%i1, 176 + 2047, %%o1
        \\ mov %%i4, %%o2
        \\ mov %%i5, %%o3
        \\ ldx [%%fp + 0x8af], %%o4
        \\ t 0x6d
        \\ bcs,pn %%xcc, 1f
        \\ nop
        \\ # The child pid is returned in o0 while o1 tells if this
        \\ # process is # the child (=1) or the parent (=0).
        \\ brnz %%o1, 2f
        \\ nop
        \\ # Parent process, return the child pid
        \\ mov %%o0, %%i0
        \\ ret
        \\ restore
        \\1:
        \\ # The syscall failed
        \\ sub %%g0, %%o0, %%i0
        \\ ret
        \\ restore
        \\2:
        \\ # Child process
    );
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (
        \\ .cfi_undefined %%i7
    );
    asm volatile (
        \\ mov %%g0, %%fp
        \\ mov %%g0, %%i7
        \\
        \\ # call func(arg)
        \\ mov %%g0, %%fp
        \\ call %%g2
        \\ mov %%g3, %%o0
        \\ # Exit
        \\ mov 1, %%g1 // SYS_exit
        \\ t 0x6d
    );
}

pub const restore = restore_rt;

// Need to use C ABI here instead of naked
// to prevent an infinite loop when calling rt_sigreturn.
pub fn restore_rt() callconv(.c) void {
    return asm volatile ("t 0x6d"
        :
        : [number] "{g1}" (@intFromEnum(SYS.rt_sigreturn)),
        : .{ .memory = true, .xcc = true, .o0 = true, .o1 = true, .o2 = true, .o3 = true, .o4 = true, .o5 = true, .o7 = true });
}

pub const VDSO = struct {
    pub const CGT_SYM = "__vdso_clock_gettime";
    pub const CGT_VER = "LINUX_2.6";
};

pub const time_t = i64;



---
File: /std/os/linux/syscalls.zig
---

// This file is automatically generated by tools/generate_linux_syscalls.zig
// This list current as of kernel: 6.19.0

pub const X86 = enum(usize) {
    restart_syscall = 0,
    exit = 1,
    fork = 2,
    read = 3,
    write = 4,
    open = 5,
    close = 6,
    waitpid = 7,
    creat = 8,
    link = 9,
    unlink = 10,
    execve = 11,
    chdir = 12,
    time = 13,
    mknod = 14,
    chmod = 15,
    lchown = 16,
    @"break" = 17,
    oldstat = 18,
    lseek = 19,
    getpid = 20,
    mount = 21,
    umount = 22,
    setuid = 23,
    getuid = 24,
    stime = 25,
    ptrace = 26,
    alarm = 27,
    oldfstat = 28,
    pause = 29,
    utime = 30,
    stty = 31,
    gtty = 32,
    access = 33,
    nice = 34,
    ftime = 35,
    sync = 36,
    kill = 37,
    rename = 38,
    mkdir = 39,
    rmdir = 40,
    dup = 41,
    pipe = 42,
    times = 43,
    prof = 44,
    brk = 45,
    setgid = 46,
    getgid = 47,
    signal = 48,
    geteuid = 49,
    getegid = 50,
    acct = 51,
    umount2 = 52,
    lock = 53,
    ioctl = 54,
    fcntl = 55,
    mpx = 56,
    setpgid = 57,
    ulimit = 58,
    oldolduname = 59,
    umask = 60,
    chroot = 61,
    ustat = 62,
    dup2 = 63,
    getppid = 64,
    getpgrp = 65,
    setsid = 66,
    sigaction = 67,
    sgetmask = 68,
    ssetmask = 69,
    setreuid = 70,
    setregid = 71,
    sigsuspend = 72,
    sigpending = 73,
    sethostname = 74,
    setrlimit = 75,
    getrlimit = 76,
    getrusage = 77,
    gettimeofday = 78,
    settimeofday = 79,
    getgroups = 80,
    setgroups = 81,
    select = 82,
    symlink = 83,
    oldlstat = 84,
    readlink = 85,
    uselib = 86,
    swapon = 87,
    reboot = 88,
    readdir = 89,
    mmap = 90,
    munmap = 91,
    truncate = 92,
    ftruncate = 93,
    fchmod = 94,
    fchown = 95,
    getpriority = 96,
    setpriority = 97,
    profil = 98,
    statfs = 99,
    fstatfs = 100,
    ioperm = 101,
    socketcall = 102,
    syslog = 103,
    setitimer = 104,
    getitimer = 105,
    stat = 106,
    lstat = 107,
    fstat = 108,
    olduname = 109,
    iopl = 110,
    vhangup = 111,
    idle = 112,
    vm86old = 113,
    wait4 = 114,
    swapoff = 115,
    sysinfo = 116,
    ipc = 117,
    fsync = 118,
    sigreturn = 119,
    clone = 120,
    setdomainname = 121,
    uname = 122,
    modify_ldt = 123,
    adjtimex = 124,
    mprotect = 125,
    sigprocmask = 126,
    create_module = 127,
    init_module = 128,
    delete_module = 129,
    get_kernel_syms = 130,
    quotactl = 131,
    getpgid = 132,
    fchdir = 133,
    bdflush = 134,
    sysfs = 135,
    personality = 136,
    afs_syscall = 137,
    setfsuid = 138,
    setfsgid = 139,
    llseek = 140,
    getdents = 141,
    newselect = 142,
    flock = 143,
    msync = 144,
    readv = 145,
    writev = 146,
    getsid = 147,
    fdatasync = 148,
    sysctl = 149,
    mlock = 150,
    munlock = 151,
    mlockall = 152,
    munlockall = 153,
    sched_setparam = 154,
    sched_getparam = 155,
    sched_setscheduler = 156,
    sched_getscheduler = 157,
    sched_yield = 158,
    sched_get_priority_max = 159,
    sched_get_priority_min = 160,
    sched_rr_get_interval = 161,
    nanosleep = 162,
    mremap = 163,
    setresuid = 164,
    getresuid = 165,
    vm86 = 166,
    query_module = 167,
    poll = 168,
    nfsservctl = 169,
    setresgid = 170,
    getresgid = 171,
    prctl = 172,
    rt_sigreturn = 173,
    rt_sigaction = 174,
    rt_sigprocmask = 175,
    rt_sigpending = 176,
    rt_sigtimedwait = 177,
    rt_sigqueueinfo = 178,
    rt_sigsuspend = 179,
    pread64 = 180,
    pwrite64 = 181,
    chown = 182,
    getcwd = 183,
    capget = 184,
    capset = 185,
    sigaltstack = 186,
    sendfile = 187,
    getpmsg = 188,
    putpmsg = 189,
    vfork = 190,
    ugetrlimit = 191,
    mmap2 = 192,
    truncate64 = 193,
    ftruncate64 = 194,
    stat64 = 195,
    lstat64 = 196,
    fstat64 = 197,
    lchown32 = 198,
    getuid32 = 199,
    getgid32 = 200,
    geteuid32 = 201,
    getegid32 = 202,
    setreuid32 = 203,
    setregid32 = 204,
    getgroups32 = 205,
    setgroups32 = 206,
    fchown32 = 207,
    setresuid32 = 208,
    getresuid32 = 209,
    setresgid32 = 210,
    getresgid32 = 211,
    chown32 = 212,
    setuid32 = 213,
    setgid32 = 214,
    setfsuid32 = 215,
    setfsgid32 = 216,
    pivot_root = 217,
    mincore = 218,
    madvise = 219,
    getdents64 = 220,
    fcntl64 = 221,
    gettid = 224,
    readahead = 225,
    setxattr = 226,
    lsetxattr = 227,
    fsetxattr = 228,
    getxattr = 229,
    lgetxattr = 230,
    fgetxattr = 231,
    listxattr = 232,
    llistxattr = 233,
    flistxattr = 234,
    removexattr = 235,
    lremovexattr = 236,
    fremovexattr = 237,
    tkill = 238,
    sendfile64 = 239,
    futex = 240,
    sched_setaffinity = 241,
    sched_getaffinity = 242,
    set_thread_area = 243,
    get_thread_area = 244,
    io_setup = 245,
    io_destroy = 246,
    io_getevents = 247,
    io_submit = 248,
    io_cancel = 249,
    fadvise64 = 250,
    exit_group = 252,
    lookup_dcookie = 253,
    epoll_create = 254,
    epoll_ctl = 255,
    epoll_wait = 256,
    remap_file_pages = 257,
    set_tid_address = 258,
    timer_create = 259,
    timer_settime = 260,
    timer_gettime = 261,
    timer_getoverrun = 262,
    timer_delete = 263,
    clock_settime = 264,
    clock_gettime = 265,
    clock_getres = 266,
    clock_nanosleep = 267,
    statfs64 = 268,
    fstatfs64 = 269,
    tgkill = 270,
    utimes = 271,
    fadvise64_64 = 272,
    vserver = 273,
    mbind = 274,
    get_mempolicy = 275,
    set_mempolicy = 276,
    mq_open = 277,
    mq_unlink = 278,
    mq_timedsend = 279,
    mq_timedreceive = 280,
    mq_notify = 281,
    mq_getsetattr = 282,
    kexec_load = 283,
    waitid = 284,
    add_key = 286,
    request_key = 287,
    keyctl = 288,
    ioprio_set = 289,
    ioprio_get = 290,
    inotify_init = 291,
    inotify_add_watch = 292,
    inotify_rm_watch = 293,
    migrate_pages = 294,
    openat = 295,
    mkdirat = 296,
    mknodat = 297,
    fchownat = 298,
    futimesat = 299,
    fstatat64 = 300,
    unlinkat = 301,
    renameat = 302,
    linkat = 303,
    symlinkat = 304,
    readlinkat = 305,
    fchmodat = 306,
    faccessat = 307,
    pselect6 = 308,
    ppoll = 309,
    unshare = 310,
    set_robust_list = 311,
    get_robust_list = 312,
    splice = 313,
    sync_file_range = 314,
    tee = 315,
    vmsplice = 316,
    move_pages = 317,
    getcpu = 318,
    epoll_pwait = 319,
    utimensat = 320,
    signalfd = 321,
    timerfd_create = 322,
    eventfd = 323,
    fallocate = 324,
    timerfd_settime = 325,
    timerfd_gettime = 326,
    signalfd4 = 327,
    eventfd2 = 328,
    epoll_create1 = 329,
    dup3 = 330,
    pipe2 = 331,
    inotify_init1 = 332,
    preadv = 333,
    pwritev = 334,
    rt_tgsigqueueinfo = 335,
    perf_event_open = 336,
    recvmmsg = 337,
    fanotify_init = 338,
    fanotify_mark = 339,
    prlimit64 = 340,
    name_to_handle_at = 341,
    open_by_handle_at = 342,
    clock_adjtime = 343,
    syncfs = 344,
    sendmmsg = 345,
    setns = 346,
    process_vm_readv = 347,
    process_vm_writev = 348,
    kcmp = 349,
    finit_module = 350,
    sched_setattr = 351,
    sched_getattr = 352,
    renameat2 = 353,
    seccomp = 354,
    getrandom = 355,
    memfd_create = 356,
    bpf = 357,
    execveat = 358,
    socket = 359,
    socketpair = 360,
    bind = 361,
    connect = 362,
    listen = 363,
    accept4 = 364,
    getsockopt = 365,
    setsockopt = 366,
    getsockname = 367,
    getpeername = 368,
    sendto = 369,
    sendmsg = 370,
    recvfrom = 371,
    recvmsg = 372,
    shutdown = 373,
    userfaultfd = 374,
    membarrier = 375,
    mlock2 = 376,
    copy_file_range = 377,
    preadv2 = 378,
    pwritev2 = 379,
    pkey_mprotect = 380,
    pkey_alloc = 381,
    pkey_free = 382,
    statx = 383,
    arch_prctl = 384,
    io_pgetevents = 385,
    rseq = 386,
    semget = 393,
    semctl = 394,
    shmget = 395,
    shmctl = 396,
    shmat = 397,
    shmdt = 398,
    msgget = 399,
    msgsnd = 400,
    msgrcv = 401,
    msgctl = 402,
    clock_gettime64 = 403,
    clock_settime64 = 404,
    clock_adjtime64 = 405,
    clock_getres_time64 = 406,
    clock_nanosleep_time64 = 407,
    timer_gettime64 = 408,
    timer_settime64 = 409,
    timerfd_gettime64 = 410,
    timerfd_settime64 = 411,
    utimensat_time64 = 412,
    pselect6_time64 = 413,
    ppoll_time64 = 414,
    io_pgetevents_time64 = 416,
    recvmmsg_time64 = 417,
    mq_timedsend_time64 = 418,
    mq_timedreceive_time64 = 419,
    semtimedop_time64 = 420,
    rt_sigtimedwait_time64 = 421,
    futex_time64 = 422,
    sched_rr_get_interval_time64 = 423,
    pidfd_send_signal = 424,
    io_uring_setup = 425,
    io_uring_enter = 426,
    io_uring_register = 427,
    open_tree = 428,
    move_mount = 429,
    fsopen = 430,
    fsconfig = 431,
    fsmount = 432,
    fspick = 433,
    pidfd_open = 434,
    clone3 = 435,
    close_range = 436,
    openat2 = 437,
    pidfd_getfd = 438,
    faccessat2 = 439,
    process_madvise = 440,
    epoll_pwait2 = 441,
    mount_setattr = 442,
    quotactl_fd = 443,
    landlock_create_ruleset = 444,
    landlock_add_rule = 445,
    landlock_restrict_self = 446,
    memfd_secret = 447,
    process_mrelease = 448,
    futex_waitv = 449,
    set_mempolicy_home_node = 450,
    cachestat = 451,
    fchmodat2 = 452,
    map_shadow_stack = 453,
    futex_wake = 454,
    futex_wait = 455,
    futex_requeue = 456,
    statmount = 457,
    listmount = 458,
    lsm_get_self_attr = 459,
    lsm_set_self_attr = 460,
    lsm_list_modules = 461,
    mseal = 462,
    setxattrat = 463,
    getxattrat = 464,
    listxattrat = 465,
    removexattrat = 466,
    open_tree_attr = 467,
    file_getattr = 468,
    file_setattr = 469,
    listns = 470,
};

pub const X64 = enum(usize) {
    read = 0,
    write = 1,
    open = 2,
    close = 3,
    stat = 4,
    fstat = 5,
    lstat = 6,
    poll = 7,
    lseek = 8,
    mmap = 9,
    mprotect = 10,
    munmap = 11,
    brk = 12,
    rt_sigaction = 13,
    rt_sigprocmask = 14,
    rt_sigreturn = 15,
    ioctl = 16,
    pread64 = 17,
    pwrite64 = 18,
    readv = 19,
    writev = 20,
    access = 21,
    pipe = 22,
    select = 23,
    sched_yield = 24,
    mremap = 25,
    msync = 26,
    mincore = 27,
    madvise = 28,
    shmget = 29,
    shmat = 30,
    shmctl = 31,
    dup = 32,
    dup2 = 33,
    pause = 34,
    nanosleep = 35,
    getitimer = 36,
    alarm = 37,
    setitimer = 38,
    getpid = 39,
    sendfile = 40,
    socket = 41,
    connect = 42,
    accept = 43,
    sendto = 44,
    recvfrom = 45,
    sendmsg = 46,
    recvmsg = 47,
    shutdown = 48,
    bind = 49,
    listen = 50,
    getsockname = 51,
    getpeername = 52,
    socketpair = 53,
    setsockopt = 54,
    getsockopt = 55,
    clone = 56,
    fork = 57,
    vfork = 58,
    execve = 59,
    exit = 60,
    wait4 = 61,
    kill = 62,
    uname = 63,
    semget = 64,
    semop = 65,
    semctl = 66,
    shmdt = 67,
    msgget = 68,
    msgsnd = 69,
    msgrcv = 70,
    msgctl = 71,
    fcntl = 72,
    flock = 73,
    fsync = 74,
    fdatasync = 75,
    truncate = 76,
    ftruncate = 77,
    getdents = 78,
    getcwd = 79,
    chdir = 80,
    fchdir = 81,
    rename = 82,
    mkdir = 83,
    rmdir = 84,
    creat = 85,
    link = 86,
    unlink = 87,
    symlink = 88,
    readlink = 89,
    chmod = 90,
    fchmod = 91,
    chown = 92,
    fchown = 93,
    lchown = 94,
    umask = 95,
    gettimeofday = 96,
    getrlimit = 97,
    getrusage = 98,
    sysinfo = 99,
    times = 100,
    ptrace = 101,
    getuid = 102,
    syslog = 103,
    getgid = 104,
    setuid = 105,
    setgid = 106,
    geteuid = 107,
    getegid = 108,
    setpgid = 109,
    getppid = 110,
    getpgrp = 111,
    setsid = 112,
    setreuid = 113,
    setregid = 114,
    getgroups = 115,
    setgroups = 116,
    setresuid = 117,
    getresuid = 118,
    setresgid = 119,
    getresgid = 120,
    getpgid = 121,
    setfsuid = 122,
    setfsgid = 123,
    getsid = 124,
    capget = 125,
    capset = 126,
    rt_sigpending = 127,
    rt_sigtimedwait = 128,
    rt_sigqueueinfo = 129,
    rt_sigsuspend = 130,
    sigaltstack = 131,
    utime = 132,
    mknod = 133,
    uselib = 134,
    personality = 135,
    ustat = 136,
    statfs = 137,
    fstatfs = 138,
    sysfs = 139,
    getpriority = 140,
    setpriority = 141,
    sched_setparam = 142,
    sched_getparam = 143,
    sched_setscheduler = 144,
    sched_getscheduler = 145,
    sched_get_priority_max = 146,
    sched_get_priority_min = 147,
    sched_rr_get_interval = 148,
    mlock = 149,
    munlock = 150,
    mlockall = 151,
    munlockall = 152,
    vhangup = 153,
    modify_ldt = 154,
    pivot_root = 155,
    sysctl = 156,
    prctl = 157,
    arch_prctl = 158,
    adjtimex = 159,
    setrlimit = 160,
    chroot = 161,
    sync = 162,
    acct = 163,
    settimeofday = 164,
    mount = 165,
    umount2 = 166,
    swapon = 167,
    swapoff = 168,
    reboot = 169,
    sethostname = 170,
    setdomainname = 171,
    iopl = 172,
    ioperm = 173,
    create_module = 174,
    init_module = 175,
    delete_module = 176,
    get_kernel_syms = 177,
    query_module = 178,
    quotactl = 179,
    nfsservctl = 180,
    getpmsg = 181,
    putpmsg = 182,
    afs_syscall = 183,
    tuxcall = 184,
    security = 185,
    gettid = 186,
    readahead = 187,
    setxattr = 188,
    lsetxattr = 189,
    fsetxattr = 190,
    getxattr = 191,
    lgetxattr = 192,
    fgetxattr = 193,
    listxattr = 194,
    llistxattr = 195,
    flistxattr = 196,
    removexattr = 197,
    lremovexattr = 198,
    fremovexattr = 199,
    tkill = 200,
    time = 201,
    futex = 202,
    sched_setaffinity = 203,
    sched_getaffinity = 204,
    set_thread_area = 205,
    io_setup = 206,
    io_destroy = 207,
    io_getevents = 208,
    io_submit = 209,
    io_cancel = 210,
    get_thread_area = 211,
    lookup_dcookie = 212,
    epoll_create = 213,
    epoll_ctl_old = 214,
    epoll_wait_old = 215,
    remap_file_pages = 216,
    getdents64 = 217,
    set_tid_address = 218,
    restart_syscall = 219,
    semtimedop = 220,
    fadvise64 = 221,
    timer_create = 222,
    timer_settime = 223,
    timer_gettime = 224,
    timer_getoverrun = 225,
    timer_delete = 226,
    clock_settime = 227,
    clock_gettime = 228,
    clock_getres = 229,
    clock_nanosleep = 230,
    exit_group = 231,
    epoll_wait = 232,
    epoll_ctl = 233,
    tgkill = 234,
    utimes = 235,
    vserver = 236,
    mbind = 237,
    set_mempolicy = 238,
    get_mempolicy = 239,
    mq_open = 240,
    mq_unlink = 241,
    mq_timedsend = 242,
    mq_timedreceive = 243,
    mq_notify = 244,
    mq_getsetattr = 245,
    kexec_load = 246,
    waitid = 247,
    add_key = 248,
    request_key = 249,
    keyctl = 250,
    ioprio_set = 251,
    ioprio_get = 252,
    inotify_init = 253,
    inotify_add_watch = 254,
    inotify_rm_watch = 255,
    migrate_pages = 256,
    openat = 257,
    mkdirat = 258,
    mknodat = 259,
    fchownat = 260,
    futimesat = 261,
    fstatat64 = 262,
    unlinkat = 263,
    renameat = 264,
    linkat = 265,
    symlinkat = 266,
    readlinkat = 267,
    fchmodat = 268,
    faccessat = 269,
    pselect6 = 270,
    ppoll = 271,
    unshare = 272,
    set_robust_list = 273,
    get_robust_list = 274,
    splice = 275,
    tee = 276,
    sync_file_range = 277,
    vmsplice = 278,
    move_pages = 279,
    utimensat = 280,
    epoll_pwait = 281,
    signalfd = 282,
    timerfd_create = 283,
    eventfd = 284,
    fallocate = 285,
    timerfd_settime = 286,
    timerfd_gettime = 287,
    accept4 = 288,
    signalfd4 = 289,
    eventfd2 = 290,
    epoll_create1 = 291,
    dup3 = 292,
    pipe2 = 293,
    inotify_init1 = 294,
    preadv = 295,
    pwritev = 296,
    rt_tgsigqueueinfo = 297,
    perf_event_open = 298,
    recvmmsg = 299,
    fanotify_init = 300,
    fanotify_mark = 301,
    prlimit64 = 302,
    name_to_handle_at = 303,
    open_by_handle_at = 304,
    clock_adjtime = 305,
    syncfs = 306,
    sendmmsg = 307,
    setns = 308,
    getcpu = 309,
    process_vm_readv = 310,
    process_vm_writev = 311,
    kcmp = 312,
    finit_module = 313,
    sched_setattr = 314,
    sched_getattr = 315,
    renameat2 = 316,
    seccomp = 317,
    getrandom = 318,
    memfd_create = 319,
    kexec_file_load = 320,
    bpf = 321,
    execveat = 322,
    userfaultfd = 323,
    membarrier = 324,
    mlock2 = 325,
    copy_file_range = 326,
    preadv2 = 327,
    pwritev2 = 328,
    pkey_mprotect = 329,
    pkey_alloc = 330,
    pkey_free = 331,
    statx = 332,
    io_pgetevents = 333,
    rseq = 334,
    uretprobe = 335,
    uprobe = 336,
    pidfd_send_signal = 424,
    io_uring_setup = 425,
    io_uring_enter = 426,
    io_uring_register = 427,
    open_tree = 428,
    move_mount = 429,
    fsopen = 430,
    fsconfig = 431,
    fsmount = 432,
    fspick = 433,
    pidfd_open = 434,
    clone3 = 435,
    close_range = 436,
    openat2 = 437,
    pidfd_getfd = 438,
    faccessat2 = 439,
    process_madvise = 440,
    epoll_pwait2 = 441,
    mount_setattr = 442,
    quotactl_fd = 443,
    landlock_create_ruleset = 444,
    landlock_add_rule = 445,
    landlock_restrict_self = 446,
    memfd_secret = 447,
    process_mrelease = 448,
    futex_waitv = 449,
    set_mempolicy_home_node = 450,
    cachestat = 451,
    fchmodat2 = 452,
    map_shadow_stack = 453,
    futex_wake = 454,
    futex_wait = 455,
    futex_requeue = 456,
    statmount = 457,
    listmount = 458,
    lsm_get_self_attr = 459,
    lsm_set_self_attr = 460,
    lsm_list_modules = 461,
    mseal = 462,
    setxattrat = 463,
    getxattrat = 464,
    listxattrat = 465,
    removexattrat = 466,
    open_tree_attr = 467,
    file_getattr = 468,
    file_setattr = 469,
    listns = 470,
};

pub const X32 = enum(usize) {
    read = 1073741824,
    write = 1073741825,
    open = 1073741826,
    close = 1073741827,
    stat = 1073741828,
    fstat = 1073741829,
    lstat = 1073741830,
    poll = 1073741831,
    lseek = 1073741832,
    mmap = 1073741833,
    mprotect = 1073741834,
    munmap = 1073741835,
    brk = 1073741836,
    rt_sigprocmask = 1073741838,
    pread64 = 1073741841,
    pwrite64 = 1073741842,
    access = 1073741845,
    pipe = 1073741846,
    select = 1073741847,
    sched_yield = 1073741848,
    mremap = 1073741849,
    msync = 1073741850,
    mincore = 1073741851,
    madvise = 1073741852,
    shmget = 1073741853,
    shmat = 1073741854,
    shmctl = 1073741855,
    dup = 1073741856,
    dup2 = 1073741857,
    pause = 1073741858,
    nanosleep = 1073741859,
    getitimer = 1073741860,
    alarm = 1073741861,
    setitimer = 1073741862,
    getpid = 1073741863,
    sendfile = 1073741864,
    socket = 1073741865,
    connect = 1073741866,
    accept = 1073741867,
    sendto = 1073741868,
    shutdown = 1073741872,
    bind = 1073741873,
    listen = 1073741874,
    getsockname = 1073741875,
    getpeername = 1073741876,
    socketpair = 1073741877,
    clone = 1073741880,
    fork = 1073741881,
    vfork = 1073741882,
    exit = 1073741884,
    wait4 = 1073741885,
    kill = 1073741886,
    uname = 1073741887,
    semget = 1073741888,
    semop = 1073741889,
    semctl = 1073741890,
    shmdt = 1073741891,
    msgget = 1073741892,
    msgsnd = 1073741893,
    msgrcv = 1073741894,
    msgctl = 1073741895,
    fcntl = 1073741896,
    flock = 1073741897,
    fsync = 1073741898,
    fdatasync = 1073741899,
    truncate = 1073741900,
    ftruncate = 1073741901,
    getdents = 1073741902,
    getcwd = 1073741903,
    chdir = 1073741904,
    fchdir = 1073741905,
    rename = 1073741906,
    mkdir = 1073741907,
    rmdir = 1073741908,
    creat = 1073741909,
    link = 1073741910,
    unlink = 1073741911,
    symlink = 1073741912,
    readlink = 1073741913,
    chmod = 1073741914,
    fchmod = 1073741915,
    chown = 1073741916,
    fchown = 1073741917,
    lchown = 1073741918,
    umask = 1073741919,
    gettimeofday = 1073741920,
    getrlimit = 1073741921,
    getrusage = 1073741922,
    sysinfo = 1073741923,
    times = 1073741924,
    getuid = 1073741926,
    syslog = 1073741927,
    getgid = 1073741928,
    setuid = 1073741929,
    setgid = 1073741930,
    geteuid = 1073741931,
    getegid = 1073741932,
    setpgid = 1073741933,
    getppid = 1073741934,
    getpgrp = 1073741935,
    setsid = 1073741936,
    setreuid = 1073741937,
    setregid = 1073741938,
    getgroups = 1073741939,
    setgroups = 1073741940,
    setresuid = 1073741941,
    getresuid = 1073741942,
    setresgid = 1073741943,
    getresgid = 1073741944,
    getpgid = 1073741945,
    setfsuid = 1073741946,
    setfsgid = 1073741947,
    getsid = 1073741948,
    capget = 1073741949,
    capset = 1073741950,
    rt_sigsuspend = 1073741954,
    utime = 1073741956,
    mknod = 1073741957,
    personality = 1073741959,
    ustat = 1073741960,
    statfs = 1073741961,
    fstatfs = 1073741962,
    sysfs = 1073741963,
    getpriority = 1073741964,
    setpriority = 1073741965,
    sched_setparam = 1073741966,
    sched_getparam = 1073741967,
    sched_setscheduler = 1073741968,
    sched_getscheduler = 1073741969,
    sched_get_priority_max = 1073741970,
    sched_get_priority_min = 1073741971,
    sched_rr_get_interval = 1073741972,
    mlock = 1073741973,
    munlock = 1073741974,
    mlockall = 1073741975,
    munlockall = 1073741976,
    vhangup = 1073741977,
    modify_ldt = 1073741978,
    pivot_root = 1073741979,
    prctl = 1073741981,
    arch_prctl = 1073741982,
    adjtimex = 1073741983,
    setrlimit = 1073741984,
    chroot = 1073741985,
    sync = 1073741986,
    acct = 1073741987,
    settimeofday = 1073741988,
    mount = 1073741989,
    umount2 = 1073741990,
    swapon = 1073741991,
    swapoff = 1073741992,
    reboot = 1073741993,
    sethostname = 1073741994,
    setdomainname = 1073741995,
    iopl = 1073741996,
    ioperm = 1073741997,
    init_module = 1073741999,
    delete_module = 1073742000,
    quotactl = 1073742003,
    getpmsg = 1073742005,
    putpmsg = 1073742006,
    afs_syscall = 1073742007,
    tuxcall = 1073742008,
    security = 1073742009,
    gettid = 1073742010,
    readahead = 1073742011,
    setxattr = 1073742012,
    lsetxattr = 1073742013,
    fsetxattr = 1073742014,
    getxattr = 1073742015,
    lgetxattr = 1073742016,
    fgetxattr = 1073742017,
    listxattr = 1073742018,
    llistxattr = 1073742019,
    flistxattr = 1073742020,
    removexattr = 1073742021,
    lremovexattr = 1073742022,
    fremovexattr = 1073742023,
    tkill = 1073742024,
    time = 1073742025,
    futex = 1073742026,
    sched_setaffinity = 1073742027,
    sched_getaffinity = 1073742028,
    io_destroy = 1073742031,
    io_getevents = 1073742032,
    io_cancel = 1073742034,
    lookup_dcookie = 1073742036,
    epoll_create = 1073742037,
    remap_file_pages = 1073742040,
    getdents64 = 1073742041,
    set_tid_address = 1073742042,
    restart_syscall = 1073742043,
    semtimedop = 1073742044,
    fadvise64 = 1073742045,
    timer_settime = 1073742047,
    timer_gettime = 1073742048,
    timer_getoverrun = 1073742049,
    timer_delete = 1073742050,
    clock_settime = 1073742051,
    clock_gettime = 1073742052,
    clock_getres = 1073742053,
    clock_nanosleep = 1073742054,
    exit_group = 1073742055,
    epoll_wait = 1073742056,
    epoll_ctl = 1073742057,
    tgkill = 1073742058,
    utimes = 1073742059,
    mbind = 1073742061,
    set_mempolicy = 1073742062,
    get_mempolicy = 1073742063,
    mq_open = 1073742064,
    mq_unlink = 1073742065,
    mq_timedsend = 1073742066,
    mq_timedreceive = 1073742067,
    mq_getsetattr = 1073742069,
    add_key = 1073742072,
    request_key = 1073742073,
    keyctl = 1073742074,
    ioprio_set = 1073742075,
    ioprio_get = 1073742076,
    inotify_init = 1073742077,
    inotify_add_watch = 1073742078,
    inotify_rm_watch = 1073742079,
    migrate_pages = 1073742080,
    openat = 1073742081,
    mkdirat = 1073742082,
    mknodat = 1073742083,
    fchownat = 1073742084,
    futimesat = 1073742085,
    fstatat64 = 1073742086,
    unlinkat = 1073742087,
    renameat = 1073742088,
    linkat = 1073742089,
    symlinkat = 1073742090,
    readlinkat = 1073742091,
    fchmodat = 1073742092,
    faccessat = 1073742093,
    pselect6 = 1073742094,
    ppoll = 1073742095,
    unshare = 1073742096,
    splice = 1073742099,
    tee = 1073742100,
    sync_file_range = 1073742101,
    utimensat = 1073742104,
    epoll_pwait = 1073742105,
    signalfd = 1073742106,
    timerfd_create = 1073742107,
    eventfd = 1073742108,
    fallocate = 1073742109,
    timerfd_settime = 1073742110,
    timerfd_gettime = 1073742111,
    accept4 = 1073742112,
    signalfd4 = 1073742113,
    eventfd2 = 1073742114,
    epoll_create1 = 1073742115,
    dup3 = 1073742116,
    pipe2 = 1073742117,
    inotify_init1 = 1073742118,
    perf_event_open = 1073742122,
    fanotify_init = 1073742124,
    fanotify_mark = 1073742125,
    prlimit64 = 1073742126,
    name_to_handle_at = 1073742127,
    open_by_handle_at = 1073742128,
    clock_adjtime = 1073742129,
    syncfs = 1073742130,
    setns = 1073742132,
    getcpu = 1073742133,
    kcmp = 1073742136,
    finit_module = 1073742137,
    sched_setattr = 1073742138,
    sched_getattr = 1073742139,
    renameat2 = 1073742140,
    seccomp = 1073742141,
    getrandom = 1073742142,
    memfd_create = 1073742143,
    kexec_file_load = 1073742144,
    bpf = 1073742145,
    userfaultfd = 1073742147,
    membarrier = 1073742148,
    mlock2 = 1073742149,
    copy_file_range = 1073742150,
    pkey_mprotect = 1073742153,
    pkey_alloc = 1073742154,
    pkey_free = 1073742155,
    statx = 1073742156,
    io_pgetevents = 1073742157,
    rseq = 1073742158,
    uretprobe = 1073742159,
    uprobe = 1073742160,
    pidfd_send_signal = 1073742248,
    io_uring_setup = 1073742249,
    io_uring_enter = 1073742250,
    io_uring_register = 1073742251,
    open_tree = 1073742252,
    move_mount = 1073742253,
    fsopen = 1073742254,
    fsconfig = 1073742255,
    fsmount = 1073742256,
    fspick = 1073742257,
    pidfd_open = 1073742258,
    clone3 = 1073742259,
    close_range = 1073742260,
    openat2 = 1073742261,
    pidfd_getfd = 1073742262,
    faccessat2 = 1073742263,
    process_madvise = 1073742264,
    epoll_pwait2 = 1073742265,
    mount_setattr = 1073742266,
    quotactl_fd = 1073742267,
    landlock_create_ruleset = 1073742268,
    landlock_add_rule = 1073742269,
    landlock_restrict_self = 1073742270,
    memfd_secret = 1073742271,
    process_mrelease = 1073742272,
    futex_waitv = 1073742273,
    set_mempolicy_home_node = 1073742274,
    cachestat = 1073742275,
    fchmodat2 = 1073742276,
    map_shadow_stack = 1073742277,
    futex_wake = 1073742278,
    futex_wait = 1073742279,
    futex_requeue = 1073742280,
    statmount = 1073742281,
    listmount = 1073742282,
    lsm_get_self_attr = 1073742283,
    lsm_set_self_attr = 1073742284,
    lsm_list_modules = 1073742285,
    mseal = 1073742286,
    setxattrat = 1073742287,
    getxattrat = 1073742288,
    listxattrat = 1073742289,
    removexattrat = 1073742290,
    open_tree_attr = 1073742291,
    file_getattr = 1073742292,
    file_setattr = 1073742293,
    listns = 1073742294,
    rt_sigaction = 1073742336,
    rt_sigreturn = 1073742337,
    ioctl = 1073742338,
    readv = 1073742339,
    writev = 1073742340,
    recvfrom = 1073742341,
    sendmsg = 1073742342,
    recvmsg = 1073742343,
    execve = 1073742344,
    ptrace = 1073742345,
    rt_sigpending = 1073742346,
    rt_sigtimedwait = 1073742347,
    rt_sigqueueinfo = 1073742348,
    sigaltstack = 1073742349,
    timer_create = 1073742350,
    mq_notify = 1073742351,
    kexec_load = 1073742352,
    waitid = 1073742353,
    set_robust_list = 1073742354,
    get_robust_list = 1073742355,
    vmsplice = 1073742356,
    move_pages = 1073742357,
    preadv = 1073742358,
    pwritev = 1073742359,
    rt_tgsigqueueinfo = 1073742360,
    recvmmsg = 1073742361,
    sendmmsg = 1073742362,
    process_vm_readv = 1073742363,
    process_vm_writev = 1073742364,
    setsockopt = 1073742365,
    getsockopt = 1073742366,
    io_setup = 1073742367,
    io_submit = 1073742368,
    execveat = 1073742369,
    preadv2 = 1073742370,
    pwritev2 = 1073742371,
};

pub const Arm = enum(usize) {
    const arm_base = 0x0f0000;

    restart_syscall = 0,
    exit = 1,
    fork = 2,
    read = 3,
    write = 4,
    open = 5,
    close = 6,
    creat = 8,
    link = 9,
    unlink = 10,
    execve = 11,
    chdir = 12,
    mknod = 14,
    chmod = 15,
    lchown = 16,
    lseek = 19,
    getpid = 20,
    mount = 21,
    setuid = 23,
    getuid = 24,
    ptrace = 26,
    pause = 29,
    access = 33,
    nice = 34,
    sync = 36,
    kill = 37,
    rename
```
