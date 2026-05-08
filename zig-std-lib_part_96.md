```
UCTION_AVAILABLE = 35,

    /// The SSSE3 instruction set is available.
    SSSE3_INSTRUCTIONS_AVAILABLE = 36,

    /// The SSE4_1 instruction set is available.
    SSE4_1_INSTRUCTIONS_AVAILABLE = 37,

    /// The SSE4_2 instruction set is available.
    SSE4_2_INSTRUCTIONS_AVAILABLE = 38,

    /// The AVX instruction set is available.
    AVX_INSTRUCTIONS_AVAILABLE = 39,

    /// The AVX2 instruction set is available.
    AVX2_INSTRUCTIONS_AVAILABLE = 40,

    /// The AVX512F instruction set is available.
    AVX512F_INSTRUCTIONS_AVAILABLE = 41,

    ERMS_AVAILABLE = 42,

    /// This ARM processor implements the ARM v8.2 Dot Product (DP) instructions.
    ARM_V82_DP_INSTRUCTIONS_AVAILABLE = 43,

    /// This ARM processor implements the ARM v8.3 JavaScript conversion (JSCVT) instructions.
    ARM_V83_JSCVT_INSTRUCTIONS_AVAILABLE = 44,

    /// This Arm processor implements the Arm v8.3 LRCPC instructions (for example, LDAPR). Note that certain Arm v8.2 CPUs may optionally support the LRCPC instructions.
    ARM_V83_LRCPC_INSTRUCTIONS_AVAILABLE,
};

pub const MAX_WOW64_SHARED_ENTRIES = 16;
pub const PROCESSOR_FEATURE_MAX = 64;
pub const MAXIMUM_XSTATE_FEATURES = 64;

pub const KSYSTEM_TIME = extern struct {
    LowPart: ULONG,
    High1Time: LONG,
    High2Time: LONG,
};

pub const NT_PRODUCT_TYPE = enum(INT) {
    NtProductWinNt = 1,
    NtProductLanManNt,
    NtProductServer,
};

pub const ALTERNATIVE_ARCHITECTURE_TYPE = enum(INT) {
    StandardDesign,
    NEC98x86,
    EndAlternatives,
};

pub const XSTATE_FEATURE = extern struct {
    Offset: ULONG,
    Size: ULONG,
};

pub const XSTATE_CONFIGURATION = extern struct {
    EnabledFeatures: ULONG64,
    Size: ULONG,
    OptimizedSave: ULONG,
    Features: [MAXIMUM_XSTATE_FEATURES]XSTATE_FEATURE,
};

/// Shared Kernel User Data
pub const KUSER_SHARED_DATA = extern struct {
    TickCountLowDeprecated: ULONG,
    TickCountMultiplier: ULONG,
    InterruptTime: KSYSTEM_TIME,
    SystemTime: KSYSTEM_TIME,
    TimeZoneBias: KSYSTEM_TIME,
    ImageNumberLow: USHORT,
    ImageNumberHigh: USHORT,
    NtSystemRoot: [260]WCHAR,
    MaxStackTraceDepth: ULONG,
    CryptoExponent: ULONG,
    TimeZoneId: ULONG,
    LargePageMinimum: ULONG,
    AitSamplingValue: ULONG,
    AppCompatFlag: ULONG,
    RNGSeedVersion: ULONGLONG,
    GlobalValidationRunlevel: ULONG,
    TimeZoneBiasStamp: LONG,
    NtBuildNumber: ULONG,
    NtProductType: NT_PRODUCT_TYPE,
    ProductTypeIsValid: BOOLEAN,
    Reserved0: [1]BOOLEAN,
    NativeProcessorArchitecture: USHORT,
    NtMajorVersion: ULONG,
    NtMinorVersion: ULONG,
    ProcessorFeatures: [PROCESSOR_FEATURE_MAX]BOOLEAN,
    Reserved1: ULONG,
    Reserved3: ULONG,
    TimeSlip: ULONG,
    AlternativeArchitecture: ALTERNATIVE_ARCHITECTURE_TYPE,
    BootId: ULONG,
    SystemExpirationDate: LARGE_INTEGER,
    SuiteMaskY: ULONG,
    KdDebuggerEnabled: BOOLEAN,
    DummyUnion1: extern union {
        MitigationPolicies: UCHAR,
        Alt: packed struct(u8) {
            NXSupportPolicy: u2,
            SEHValidationPolicy: u2,
            CurDirDevicesSkippedForDlls: u2,
            Reserved: u2,
        },
    },
    CyclesPerYield: USHORT,
    ActiveConsoleId: ULONG,
    DismountCount: ULONG,
    ComPlusPackage: ULONG,
    LastSystemRITEventTickCount: ULONG,
    NumberOfPhysicalPages: ULONG,
    SafeBootMode: BOOLEAN,
    DummyUnion2: extern union {
        VirtualizationFlags: UCHAR,
        Alt: packed struct(u8) {
            ArchStartedInEl2: u1,
            QcSlIsSupported: u1,
            SpareBits: u6,
        },
    },
    Reserved12: [2]UCHAR,
    DummyUnion3: extern union {
        SharedDataFlags: ULONG,
        Alt: packed struct(u32) {
            DbgErrorPortPresent: u1,
            DbgElevationEnabled: u1,
            DbgVirtEnabled: u1,
            DbgInstallerDetectEnabled: u1,
            DbgLkgEnabled: u1,
            DbgDynProcessorEnabled: u1,
            DbgConsoleBrokerEnabled: u1,
            DbgSecureBootEnabled: u1,
            DbgMultiSessionSku: u1,
            DbgMultiUsersInSessionSku: u1,
            DbgStateSeparationEnabled: u1,
            SpareBits: u21,
        },
    },
    DataFlagsPad: [1]ULONG,
    TestRetInstruction: ULONGLONG,
    QpcFrequency: LONGLONG,
    SystemCall: ULONG,
    Reserved2: ULONG,
    SystemCallPad: [2]ULONGLONG,
    DummyUnion4: extern union {
        TickCount: KSYSTEM_TIME,
        TickCountQuad: ULONG64,
        Alt: extern struct {
            ReservedTickCountOverlay: [3]ULONG,
            TickCountPad: [1]ULONG,
        },
    },
    Cookie: ULONG,
    CookiePad: [1]ULONG,
    ConsoleSessionForegroundProcessId: LONGLONG,
    TimeUpdateLock: ULONGLONG,
    BaselineSystemTimeQpc: ULONGLONG,
    BaselineInterruptTimeQpc: ULONGLONG,
    QpcSystemTimeIncrement: ULONGLONG,
    QpcInterruptTimeIncrement: ULONGLONG,
    QpcSystemTimeIncrementShift: UCHAR,
    QpcInterruptTimeIncrementShift: UCHAR,
    UnparkedProcessorCount: USHORT,
    EnclaveFeatureMask: [4]ULONG,
    TelemetryCoverageRound: ULONG,
    UserModeGlobalLogger: [16]USHORT,
    ImageFileExecutionOptions: ULONG,
    LangGenerationCount: ULONG,
    Reserved4: ULONGLONG,
    InterruptTimeBias: ULONGLONG,
    QpcBias: ULONGLONG,
    ActiveProcessorCount: ULONG,
    ActiveGroupCount: UCHAR,
    Reserved9: UCHAR,
    DummyUnion5: extern union {
        QpcData: USHORT,
        Alt: extern struct {
            QpcBypassEnabled: UCHAR,
            QpcShift: UCHAR,
        },
    },
    TimeZoneBiasEffectiveStart: LARGE_INTEGER,
    TimeZoneBiasEffectiveEnd: LARGE_INTEGER,
    XState: XSTATE_CONFIGURATION,
    FeatureConfigurationChangeStamp: KSYSTEM_TIME,
    Spare: ULONG,
    UserPointerAuthMask: ULONG64,
};

/// Read-only user-mode address for the shared data.
/// https://www.geoffchappell.com/studies/windows/km/ntoskrnl/inc/api/ntexapi_x/kuser_shared_data/index.htm
/// https://msrc-blog.microsoft.com/2022/04/05/randomizing-the-kuser_shared_data-structure-on-windows/
pub const SharedUserData: *const KUSER_SHARED_DATA = @ptrFromInt(0x7FFE0000);

pub fn IsProcessorFeaturePresent(feature: PF) bool {
    if (@intFromEnum(feature) >= PROCESSOR_FEATURE_MAX) return false;
    return SharedUserData.ProcessorFeatures[@intFromEnum(feature)].toBool();
}

// https://github.com/reactos/reactos/blob/master/sdk/include/ndk/pstypes.h#L977-L983
pub const KERNEL_USER_TIMES = extern struct {
    CreationTime: LARGE_INTEGER,
    ExitTime: LARGE_INTEGER,
    KernelTime: LARGE_INTEGER,
    UserTime: LARGE_INTEGER,
};

pub fn wtf8ToWtf16Le(wtf16le: []u16, wtf8: []const u8) error{ BadPathName, NameTooLong }!usize {
    // Each u8 in UTF-8/WTF-8 correlates to at most one u16 in UTF-16LE/WTF-16LE.
    if (wtf16le.len < wtf8.len) {
        const utf16_len = std.unicode.calcUtf16LeLenImpl(wtf8, .can_encode_surrogate_half) catch
            return error.BadPathName;
        if (utf16_len > wtf16le.len)
            return error.NameTooLong;
    }
    return std.unicode.wtf8ToWtf16Le(wtf16le, wtf8) catch |err| switch (err) {
        error.InvalidWtf8 => return error.BadPathName,
    };
}

/// Returns the path to the system directory, typically "C:\\WINDOWS\\System32".
///
/// Equivalent to `GetSystemDirectoryW` in kernel32.
pub fn getSystemDirectoryWtf16Le() [:0]const u16 {
    const ssd: *const BASE_STATIC_SERVER_DATA = @ptrCast(@alignCast(relocateCsrssAddress(
        peb().ReadOnlyStaticServerData.base_static_server_data_addr,
    )));
    return ssd.windows_system_directory.relocate().sliceZ();
}
// https://github.com/reactos/reactos/blob/4b75ec5508d47b726d1210e24f5a849dae4e3bda/sdk/include/reactos/subsys/win/base.h#L119
const BASE_STATIC_SERVER_DATA = extern struct {
    windows_directory: ForeignString,
    windows_system_directory: ForeignString,
    named_object_directory: ForeignString,
    /// This matches the 64-bit version of `UNICODE_STRING`---even on 32-bit targets, this string is
    /// from 64-bit code (since it comes from CSRSS which is running outside of WOW64).
    const ForeignString = extern struct {
        length: u16,
        maximum_length: u16,
        /// Address in the CSRSS address space. To convert this to a valid pointer in *our* address
        /// space, see `relocateCsrssAddress` (or the `ForeignString.relocate` wrapper function).
        buffer_address: u64,
        fn relocate(str: ForeignString) UNICODE_STRING {
            return .{
                .Length = str.length,
                .MaximumLength = str.maximum_length,
                .Buffer = @ptrCast(@alignCast(@constCast(relocateCsrssAddress(str.buffer_address)))),
            };
        }
    };
};
/// Takes an address in the CSRSS address space's mapped view of the shared memory region, and
/// returns the corresponding address in *our* mapped view of the shared memory region.
fn relocateCsrssAddress(addr: u64) *const anyopaque {
    const base: [*]const u8 = @ptrCast(peb().ReadOnlySharedMemoryBase);
    const offset: usize = @intCast(addr - peb().CsrServerReadOnlySharedMemoryBase);
    return base + offset;
}



---
File: /std/posix/test.zig
---




---
File: /std/process/Args.zig
---

const Args = @This();

const builtin = @import("builtin");
const native_os = builtin.os.tag;

const std = @import("../std.zig");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const testing = std.testing;

vector: Vector,

/// On WASI without libc, this is `void` because the environment has to be
/// queried and heap-allocated at runtime.
pub const Vector = switch (native_os) {
    .windows => []const u16, // WTF-16 encoded
    .wasi => switch (builtin.link_libc) {
        false => void,
        true => []const [*:0]const u8,
    },
    .freestanding, .other => void,
    else => []const [*:0]const u8,
};

/// Cross-platform access to command line one argument at a time.
pub const Iterator = struct {
    const Inner = switch (native_os) {
        .windows => Windows,
        .wasi => if (builtin.link_libc) Posix else Wasi,
        else => Posix,
    };

    inner: Inner,

    /// Initialize the args iterator. Consider using `initAllocator` instead
    /// for cross-platform compatibility.
    pub fn init(a: Args) Iterator {
        if (native_os == .wasi) @compileError("In WASI, use initAllocator instead.");
        if (native_os == .windows) @compileError("In Windows, use initAllocator instead.");
        return .{ .inner = .init(a) };
    }

    pub const InitError = Inner.InitError;

    /// You must deinitialize iterator's internal buffers by calling `deinit` when done.
    pub fn initAllocator(a: Args, gpa: Allocator) InitError!Iterator {
        if (native_os == .wasi and !builtin.link_libc) {
            return .{ .inner = try .init(gpa) };
        }
        if (native_os == .windows) {
            return .{ .inner = try .init(gpa, a.vector) };
        }

        return .{ .inner = .init(a) };
    }

    /// Return subsequent argument, or `null` if no more remaining.
    ///
    /// Returned slice is pointing to the iterator's internal buffer.
    /// On Windows, the result is encoded as [WTF-8](https://wtf-8.codeberg.page/).
    /// On other platforms, the result is an opaque sequence of bytes with no particular encoding.
    pub fn next(it: *Iterator) ?[:0]const u8 {
        return it.inner.next();
    }

    /// Parse past 1 argument without capturing it.
    /// Returns `true` if skipped an arg, `false` if we are at the end.
    pub fn skip(it: *Iterator) bool {
        return it.inner.skip();
    }

    /// Required to release resources if the iterator was initialized with
    /// `initAllocator` function.
    pub fn deinit(it: *Iterator) void {
        // Unless we're targeting WASI or Windows, this is a no-op.
        if (native_os == .wasi and !builtin.link_libc) it.inner.deinit();
        if (native_os == .windows) it.inner.deinit();
    }

    /// Iterator that implements the Windows command-line parsing algorithm.
    ///
    /// The implementation is intended to be compatible with the post-2008 C runtime,
    /// but is *not* intended to be compatible with `CommandLineToArgvW` since
    /// `CommandLineToArgvW` uses the pre-2008 parsing rules.
    ///
    /// This iterator faithfully implements the parsing behavior observed from the C runtime with
    /// one exception: if the command-line string is empty, the iterator will immediately complete
    /// without returning any arguments (whereas the C runtime will return a single argument
    /// representing the name of the current executable).
    ///
    /// The essential parts of the algorithm are described in Microsoft's documentation:
    ///
    /// - https://learn.microsoft.com/en-us/cpp/cpp/main-function-command-line-args?view=msvc-170#parsing-c-command-line-arguments
    ///
    /// David Deley explains some additional undocumented quirks in great detail:
    ///
    /// - https://daviddeley.com/autohotkey/parameters/parameters.htm#WINCRULES
    pub const Windows = struct {
        allocator: Allocator,
        /// Encoded as WTF-16 LE.
        cmd_line: []const u16,
        index: usize = 0,
        /// Owned by the iterator. Long enough to hold contiguous NUL-terminated slices
        /// of each argument encoded as WTF-8.
        buffer: []u8,
        start: usize = 0,
        end: usize = 0,

        pub const InitError = error{OutOfMemory};

        /// `cmd_line_w` *must* be a WTF16-LE-encoded string.
        ///
        /// The iterator stores and uses `cmd_line_w`, so its memory must be valid for
        /// at least as long as the returned Windows.
        pub fn init(gpa: Allocator, cmd_line_w: []const u16) Windows.InitError!Windows {
            const wtf8_len = std.unicode.calcWtf8Len(cmd_line_w);

            // This buffer must be large enough to contain contiguous NUL-terminated slices
            // of each argument.
            // - During parsing, the length of a parsed argument will always be equal to
            //   to less than its unparsed length
            // - The first argument needs one extra byte of space allocated for its NUL
            //   terminator, but for each subsequent argument the necessary whitespace
            //   between arguments guarantees room for their NUL terminator(s).
            const buffer = try gpa.alloc(u8, wtf8_len + 1);
            errdefer gpa.free(buffer);

            return .{
                .allocator = gpa,
                .cmd_line = cmd_line_w,
                .buffer = buffer,
            };
        }

        /// Returns the next argument and advances the iterator. Returns `null` if at the end of the
        /// command-line string. The iterator owns the returned slice.
        /// The result is encoded as [WTF-8](https://wtf-8.codeberg.page/).
        pub fn next(self: *Windows) ?[:0]const u8 {
            return self.nextWithStrategy(next_strategy);
        }

        /// Skips the next argument and advances the iterator. Returns `true` if an argument was
        /// skipped, `false` if at the end of the command-line string.
        pub fn skip(self: *Windows) bool {
            return self.nextWithStrategy(skip_strategy);
        }

        const next_strategy = struct {
            const T = ?[:0]const u8;

            const eof = null;

            /// Returns '\' if any backslashes are emitted, otherwise returns `last_emitted_code_unit`.
            fn emitBackslashes(self: *Windows, count: usize, last_emitted_code_unit: ?u16) ?u16 {
                for (0..count) |_| {
                    self.buffer[self.end] = '\\';
                    self.end += 1;
                }
                return if (count != 0) '\\' else last_emitted_code_unit;
            }

            /// If `last_emitted_code_unit` and `code_unit` form a surrogate pair, then
            /// the previously emitted high surrogate is overwritten by the codepoint encoded
            /// by the surrogate pair, and `null` is returned.
            /// Otherwise, `code_unit` is emitted and returned.
            fn emitCharacter(self: *Windows, code_unit: u16, last_emitted_code_unit: ?u16) ?u16 {
                // Because we are emitting WTF-8, we need to
                // check to see if we've emitted two consecutive surrogate
                // codepoints that form a valid surrogate pair in order
                // to ensure that we're always emitting well-formed WTF-8
                // (https://wtf-8.codeberg.page/#concatenating).
                //
                // If we do have a valid surrogate pair, we need to emit
                // the UTF-8 sequence for the codepoint that they encode
                // instead of the WTF-8 encoding for the two surrogate pairs
                // separately.
                //
                // This is relevant when dealing with a WTF-16 encoded
                // command line like this:
                // "<0xD801>"<0xDC37>
                // which would get parsed and converted to WTF-8 as:
                // <0xED><0xA0><0x81><0xED><0xB0><0xB7>
                // but instead, we need to recognize the surrogate pair
                // and emit the codepoint it encodes, which in this
                // example is U+10437 (𐐷), which is encoded in UTF-8 as:
                // <0xF0><0x90><0x90><0xB7>
                if (last_emitted_code_unit != null and
                    std.unicode.utf16IsLowSurrogate(code_unit) and
                    std.unicode.utf16IsHighSurrogate(last_emitted_code_unit.?))
                {
                    const codepoint = std.unicode.utf16DecodeSurrogatePair(&.{ last_emitted_code_unit.?, code_unit }) catch unreachable;

                    // Unpaired surrogate is 3 bytes long
                    const dest = self.buffer[self.end - 3 ..];
                    const len = std.unicode.utf8Encode(codepoint, dest) catch unreachable;
                    // All codepoints that require a surrogate pair (> U+FFFF) are encoded as 4 bytes
                    assert(len == 4);
                    self.end += 1;
                    return null;
                }

                const wtf8_len = std.unicode.wtf8Encode(code_unit, self.buffer[self.end..]) catch unreachable;
                self.end += wtf8_len;
                return code_unit;
            }

            fn yieldArg(self: *Windows) [:0]const u8 {
                self.buffer[self.end] = 0;
                const arg = self.buffer[self.start..self.end :0];
                self.end += 1;
                self.start = self.end;
                return arg;
            }
        };

        const skip_strategy = struct {
            const T = bool;

            const eof = false;

            fn emitBackslashes(_: *Windows, _: usize, last_emitted_code_unit: ?u16) ?u16 {
                return last_emitted_code_unit;
            }

            fn emitCharacter(_: *Windows, _: u16, last_emitted_code_unit: ?u16) ?u16 {
                return last_emitted_code_unit;
            }

            fn yieldArg(_: *Windows) bool {
                return true;
            }
        };

        fn nextWithStrategy(self: *Windows, comptime strategy: type) strategy.T {
            var last_emitted_code_unit: ?u16 = null;
            // The first argument (the executable name) uses different parsing rules.
            if (self.index == 0) {
                if (self.cmd_line.len == 0 or self.cmd_line[0] == 0) {
                    // Immediately complete the iterator.
                    // The C runtime would return the name of the current executable here.
                    return strategy.eof;
                }

                var inside_quotes = false;
                while (true) : (self.index += 1) {
                    const char = if (self.index != self.cmd_line.len)
                        std.mem.littleToNative(u16, self.cmd_line[self.index])
                    else
                        0;
                    switch (char) {
                        0 => {
                            return strategy.yieldArg(self);
                        },
                        '"' => {
                            inside_quotes = !inside_quotes;
                        },
                        ' ', '\t' => {
                            if (inside_quotes) {
                                last_emitted_code_unit = strategy.emitCharacter(self, char, last_emitted_code_unit);
                            } else {
                                self.index += 1;
                                return strategy.yieldArg(self);
                            }
                        },
                        else => {
                            last_emitted_code_unit = strategy.emitCharacter(self, char, last_emitted_code_unit);
                        },
                    }
                }
            }

            // Skip spaces and tabs. The iterator completes if we reach the end of the string here.
            while (true) : (self.index += 1) {
                const char = if (self.index != self.cmd_line.len)
                    std.mem.littleToNative(u16, self.cmd_line[self.index])
                else
                    0;
                switch (char) {
                    0 => return strategy.eof,
                    ' ', '\t' => continue,
                    else => break,
                }
            }

            // Parsing rules for subsequent arguments:
            //
            // - The end of the string always terminates the current argument.
            // - When not in 'inside_quotes' mode, a space or tab terminates the current argument.
            // - 2n backslashes followed by a quote emit n backslashes (note: n can be zero).
            //   If in 'inside_quotes' and the quote is immediately followed by a second quote,
            //   one quote is emitted and the other is skipped, otherwise, the quote is skipped
            //   and 'inside_quotes' is toggled.
            // - 2n + 1 backslashes followed by a quote emit n backslashes followed by a quote.
            // - n backslashes not followed by a quote emit n backslashes.
            var backslash_count: usize = 0;
            var inside_quotes = false;
            while (true) : (self.index += 1) {
                const char = if (self.index != self.cmd_line.len)
                    std.mem.littleToNative(u16, self.cmd_line[self.index])
                else
                    0;
                switch (char) {
                    0 => {
                        last_emitted_code_unit = strategy.emitBackslashes(self, backslash_count, last_emitted_code_unit);
                        return strategy.yieldArg(self);
                    },
                    ' ', '\t' => {
                        last_emitted_code_unit = strategy.emitBackslashes(self, backslash_count, last_emitted_code_unit);
                        backslash_count = 0;
                        if (inside_quotes) {
                            last_emitted_code_unit = strategy.emitCharacter(self, char, last_emitted_code_unit);
                        } else return strategy.yieldArg(self);
                    },
                    '"' => {
                        const char_is_escaped_quote = backslash_count % 2 != 0;
                        last_emitted_code_unit = strategy.emitBackslashes(self, backslash_count / 2, last_emitted_code_unit);
                        backslash_count = 0;
                        if (char_is_escaped_quote) {
                            last_emitted_code_unit = strategy.emitCharacter(self, '"', last_emitted_code_unit);
                        } else {
                            if (inside_quotes and
                                self.index + 1 != self.cmd_line.len and
                                std.mem.littleToNative(u16, self.cmd_line[self.index + 1]) == '"')
                            {
                                last_emitted_code_unit = strategy.emitCharacter(self, '"', last_emitted_code_unit);
                                self.index += 1;
                            } else {
                                inside_quotes = !inside_quotes;
                            }
                        }
                    },
                    '\\' => {
                        backslash_count += 1;
                    },
                    else => {
                        last_emitted_code_unit = strategy.emitBackslashes(self, backslash_count, last_emitted_code_unit);
                        backslash_count = 0;
                        last_emitted_code_unit = strategy.emitCharacter(self, char, last_emitted_code_unit);
                    },
                }
            }
        }

        /// Frees the iterator's copy of the command-line string and all previously returned
        /// argument slices.
        pub fn deinit(self: *Windows) void {
            self.allocator.free(self.buffer);
        }
    };

    pub const Posix = struct {
        remaining: Vector,

        pub const InitError = error{};

        pub fn init(a: Args) Posix {
            return .{ .remaining = a.vector };
        }

        pub fn next(it: *Posix) ?[:0]const u8 {
            if (it.remaining.len == 0) return null;
            const arg = it.remaining[0];
            it.remaining = it.remaining[1..];
            return std.mem.sliceTo(arg, 0);
        }

        pub fn skip(it: *Posix) bool {
            if (it.remaining.len == 0) return false;
            it.remaining = it.remaining[1..];
            return true;
        }
    };

    pub const Wasi = struct {
        allocator: Allocator,
        index: usize,
        args: [][:0]u8,

        pub const InitError = error{OutOfMemory} || std.posix.UnexpectedError;

        /// You must call deinit to free the internal buffer of the
        /// iterator after you are done.
        pub fn init(allocator: Allocator) Wasi.InitError!Wasi {
            const fetched_args = try Wasi.internalInit(allocator);
            return Wasi{
                .allocator = allocator,
                .index = 0,
                .args = fetched_args,
            };
        }

        fn internalInit(allocator: Allocator) Wasi.InitError![][:0]u8 {
            var count: usize = undefined;
            var buf_size: usize = undefined;

            switch (std.os.wasi.args_sizes_get(&count, &buf_size)) {
                .SUCCESS => {},
                else => |err| return std.posix.unexpectedErrno(err),
            }

            if (count == 0) {
                return &[_][:0]u8{};
            }

            const argv = try allocator.alloc([*:0]u8, count);
            defer allocator.free(argv);

            const argv_buf = try allocator.alloc(u8, buf_size);

            switch (std.os.wasi.args_get(argv.ptr, argv_buf.ptr)) {
                .SUCCESS => {},
                else => |err| return std.posix.unexpectedErrno(err),
            }

            var result_args = try allocator.alloc([:0]u8, count);
            var i: usize = 0;
            while (i < count) : (i += 1) {
                result_args[i] = std.mem.sliceTo(argv[i], 0);
            }

            return result_args;
        }

        pub fn next(self: *Wasi) ?[:0]const u8 {
            if (self.index == self.args.len) return null;

            const arg = self.args[self.index];
            self.index += 1;
            return arg;
        }

        pub fn skip(self: *Wasi) bool {
            if (self.index == self.args.len) return false;

            self.index += 1;
            return true;
        }

        /// Call to free the internal buffer of the iterator.
        pub fn deinit(self: *Wasi) void {
            // Nothing is allocated when there are no args
            if (self.args.len == 0) return;

            const last_item = self.args[self.args.len - 1];
            const last_byte_addr = @intFromPtr(last_item.ptr) + last_item.len + 1; // null terminated
            const first_item_ptr = self.args[0].ptr;
            const len = last_byte_addr - @intFromPtr(first_item_ptr);
            self.allocator.free(first_item_ptr[0..len]);
            self.allocator.free(self.args);
        }
    };
};

/// Holds the command-line arguments, with the program name as the first entry.
/// Use `iterateAllocator` for cross-platform code.
pub fn iterate(a: Args) Iterator {
    return .init(a);
}

/// You must deinitialize iterator's internal buffers by calling `deinit` when
/// done.
pub fn iterateAllocator(a: Args, gpa: Allocator) Iterator.InitError!Iterator {
    return .initAllocator(a, gpa);
}

pub const ToSliceError = Iterator.Windows.InitError || Iterator.Wasi.InitError;

/// Returned value may reference several allocations and may point into `a`.
/// Thefore, an arena-style allocator must be used.
///
/// * On Windows, the result is encoded as
///   [WTF-8](https://wtf-8.codeberg.page/).
/// * On other platforms, the result is an opaque sequence of bytes with no
///   particular encoding.
///
/// See also:
/// * `iterate`
/// * `iterateAllocator`
pub fn toSlice(a: Args, arena: Allocator) ToSliceError![]const [:0]const u8 {
    if (native_os == .windows) {
        var it = try a.iterateAllocator(arena);
        var contents: std.ArrayList(u8) = .empty;
        var slice_list: std.ArrayList(usize) = .empty;
        while (it.next()) |arg| {
            try contents.appendSlice(arena, arg[0 .. arg.len + 1]);
            try slice_list.append(arena, arg.len);
        }
        const contents_slice = contents.items;
        const slice_sizes = slice_list.items;
        const slice_list_bytes = std.math.mul(usize, @sizeOf([]u8), slice_sizes.len) catch return error.OutOfMemory;
        const total_bytes = std.math.add(usize, slice_list_bytes, contents_slice.len) catch return error.OutOfMemory;
        const buf = try arena.alignedAlloc(u8, .of([]u8), total_bytes);
        errdefer arena.free(buf);

        const result_slice_list = std.mem.bytesAsSlice([:0]u8, buf[0..slice_list_bytes]);
        const result_contents = buf[slice_list_bytes..];
        @memcpy(result_contents[0..contents_slice.len], contents_slice);

        var contents_index: usize = 0;
        for (slice_sizes, 0..) |len, i| {
            const new_index = contents_index + len;
            result_slice_list[i] = result_contents[contents_index..new_index :0];
            contents_index = new_index + 1;
        }

        return result_slice_list;
    } else if (native_os == .wasi and !builtin.link_libc) {
        var count: usize = undefined;
        var buf_size: usize = undefined;

        switch (std.os.wasi.args_sizes_get(&count, &buf_size)) {
            .SUCCESS => {},
            else => |err| return std.posix.unexpectedErrno(err),
        }

        if (count == 0) return &.{};

        const argv = try arena.alloc([*:0]u8, count);
        const argv_buf = try arena.alloc(u8, buf_size);

        switch (std.os.wasi.args_get(argv.ptr, argv_buf.ptr)) {
            .SUCCESS => {},
            else => |err| return std.posix.unexpectedErrno(err),
        }

        const args = try arena.alloc([:0]const u8, count);
        for (args, argv) |*dst, src| dst.* = std.mem.sliceTo(src, 0);
        return args;
    } else {
        const args = try arena.alloc([:0]const u8, a.vector.len);
        for (args, a.vector) |*dst, src| dst.* = std.mem.sliceTo(src, 0);
        return args;
    }
}

test "Iterator.Windows" {
    const t = testIteratorWindows;

    try t(
        \\"C:\Program Files\zig\zig.exe" run .\src\main.zig -target x86_64-windows-gnu -O ReleaseSafe -- --emoji=🗿 --eval="new Regex(\"Dwayne \\\"The Rock\\\" Johnson\")"
    , &.{
        \\C:\Program Files\zig\zig.exe
        ,
        \\run
        ,
        \\.\src\main.zig
        ,
        \\-target
        ,
        \\x86_64-windows-gnu
        ,
        \\-O
        ,
        \\ReleaseSafe
        ,
        \\--
        ,
        \\--emoji=🗿
        ,
        \\--eval=new Regex("Dwayne \"The Rock\" Johnson")
        ,
    });

    // Empty
    try t("", &.{});

    // Separators
    try t("aa bb cc", &.{ "aa", "bb", "cc" });
    try t("aa\tbb\tcc", &.{ "aa", "bb", "cc" });
    try t("aa\nbb\ncc", &.{"aa\nbb\ncc"});
    try t("aa\r\nbb\r\ncc", &.{"aa\r\nbb\r\ncc"});
    try t("aa\rbb\rcc", &.{"aa\rbb\rcc"});
    try t("aa\x07bb\x07cc", &.{"aa\x07bb\x07cc"});
    try t("aa\x7Fbb\x7Fcc", &.{"aa\x7Fbb\x7Fcc"});
    try t("aa🦎bb🦎cc", &.{"aa🦎bb🦎cc"});

    // Leading/trailing whitespace
    try t("  ", &.{""});
    try t("  aa  bb  ", &.{ "", "aa", "bb" });
    try t("\t\t", &.{""});
    try t("\t\taa\t\tbb\t\t", &.{ "", "aa", "bb" });
    try t("\n\n", &.{"\n\n"});
    try t("\n\naa\n\nbb\n\n", &.{"\n\naa\n\nbb\n\n"});

    // Executable name with quotes/backslashes
    try t("\"aa bb\tcc\ndd\"", &.{"aa bb\tcc\ndd"});
    try t("\"", &.{""});
    try t("\"\"", &.{""});
    try t("\"\"\"", &.{""});
    try t("\"\"\"\"", &.{""});
    try t("\"\"\"\"\"", &.{""});
    try t("aa\"bb\"cc\"dd", &.{"aabbccdd"});
    try t("aa\"bb cc\"dd", &.{"aabb ccdd"});
    try t("\"aa\\\"bb\"", &.{"aa\\bb"});
    try t("\"aa\\\\\"", &.{"aa\\\\"});
    try t("aa\\\"bb", &.{"aa\\bb"});
    try t("aa\\\\\"bb", &.{"aa\\\\bb"});

    // Arguments with quotes/backslashes
    try t(". \"aa bb\tcc\ndd\"", &.{ ".", "aa bb\tcc\ndd" });
    try t(". aa\" \"bb\"\t\"cc\"\n\"dd\"", &.{ ".", "aa bb\tcc\ndd" });
    try t(". ", &.{"."});
    try t(". \"", &.{ ".", "" });
    try t(". \"\"", &.{ ".", "" });
    try t(". \"\"\"", &.{ ".", "\"" });
    try t(". \"\"\"\"", &.{ ".", "\"" });
    try t(". \"\"\"\"\"", &.{ ".", "\"\"" });
    try t(". \"\"\"\"\"\"", &.{ ".", "\"\"" });
    try t(". \" \"", &.{ ".", " " });
    try t(". \" \"\"", &.{ ".", " \"" });
    try t(". \" \"\"\"", &.{ ".", " \"" });
    try t(". \" \"\"\"\"", &.{ ".", " \"\"" });
    try t(". \" \"\"\"\"\"", &.{ ".", " \"\"" });
    try t(". \" \"\"\"\"\"\"", &.{ ".", " \"\"\"" });
    try t(". \\\"", &.{ ".", "\"" });
    try t(". \\\"\"", &.{ ".", "\"" });
    try t(". \\\"\"\"", &.{ ".", "\"" });
    try t(". \\\"\"\"\"", &.{ ".", "\"\"" });
    try t(". \\\"\"\"\"\"", &.{ ".", "\"\"" });
    try t(". \\\"\"\"\"\"\"", &.{ ".", "\"\"\"" });
    try t(". \" \\\"", &.{ ".", " \"" });
    try t(". \" \\\"\"", &.{ ".", " \"" });
    try t(". \" \\\"\"\"", &.{ ".", " \"\"" });
    try t(". \" \\\"\"\"\"", &.{ ".", " \"\"" });
    try t(". \" \\\"\"\"\"\"", &.{ ".", " \"\"\"" });
    try t(". \" \\\"\"\"\"\"\"", &.{ ".", " \"\"\"" });
    try t(". aa\\bb\\\\cc\\\\\\dd", &.{ ".", "aa\\bb\\\\cc\\\\\\dd" });
    try t(". \\\\\\\"aa bb\"", &.{ ".", "\\\"aa", "bb" });
    try t(". \\\\\\\\\"aa bb\"", &.{ ".", "\\\\aa bb" });

    // From https://learn.microsoft.com/en-us/cpp/cpp/main-function-command-line-args#results-of-parsing-command-lines
    try t(
        \\foo.exe "abc" d e
    , &.{ "foo.exe", "abc", "d", "e" });
    try t(
        \\foo.exe a\\b d"e f"g h
    , &.{ "foo.exe", "a\\\\b", "de fg", "h" });
    try t(
        \\foo.exe a\\\"b c d
    , &.{ "foo.exe", "a\\\"b", "c", "d" });
    try t(
        \\foo.exe a\\\\"b c" d e
    , &.{ "foo.exe", "a\\\\b c", "d", "e" });
    try t(
        \\foo.exe a"b"" c d
    , &.{ "foo.exe", "ab\" c d" });

    // From https://daviddeley.com/autohotkey/parameters/parameters.htm#WINCRULESEX
    try t("foo.exe CallMeIshmael", &.{ "foo.exe", "CallMeIshmael" });
    try t("foo.exe \"Call Me Ishmael\"", &.{ "foo.exe", "Call Me Ishmael" });
    try t("foo.exe Cal\"l Me I\"shmael", &.{ "foo.exe", "Call Me Ishmael" });
    try t("foo.exe CallMe\\\"Ishmael", &.{ "foo.exe", "CallMe\"Ishmael" });
    try t("foo.exe \"CallMe\\\"Ishmael\"", &.{ "foo.exe", "CallMe\"Ishmael" });
    try t("foo.exe \"Call Me Ishmael\\\\\"", &.{ "foo.exe", "Call Me Ishmael\\" });
    try t("foo.exe \"CallMe\\\\\\\"Ishmael\"", &.{ "foo.exe", "CallMe\\\"Ishmael" });
    try t("foo.exe a\\\\\\b", &.{ "foo.exe", "a\\\\\\b" });
    try t("foo.exe \"a\\\\\\b\"", &.{ "foo.exe", "a\\\\\\b" });

    // Surrogate pair encoding of 𐐷 separated by quotes.
    // Encoded as WTF-16:
    // "<0xD801>"<0xDC37>
    // Encoded as WTF-8:
    // "<0xED><0xA0><0x81>"<0xED><0xB0><0xB7>
    // During parsing, the quotes drop out and the surrogate pair
    // should end up encoded as its normal UTF-8 representation.
    try t("foo.exe \"\xed\xa0\x81\"\xed\xb0\xb7", &.{ "foo.exe", "𐐷" });
}

fn testIteratorWindows(cmd_line: []const u8, expected_args: []const []const u8) !void {
    const cmd_line_w = try std.unicode.wtf8ToWtf16LeAllocZ(testing.allocator, cmd_line);
    defer testing.allocator.free(cmd_line_w);

    // next
    {
        var it = try Iterator.Windows.init(testing.allocator, cmd_line_w);
        defer it.deinit();

        for (expected_args) |expected| {
            if (it.next()) |actual| {
                try testing.expectEqualStrings(expected, actual);
            } else {
                return error.TestUnexpectedResult;
            }
        }
        try testing.expect(it.next() == null);
    }

    // skip
    {
        var it = try Iterator.Windows.init(testing.allocator, cmd_line_w);
        defer it.deinit();

        for (0..expected_args.len) |_| {
            try testing.expect(it.skip());
        }
        try testing.expect(!it.skip());
    }
}

test "general parsing" {
    try testGeneralCmdLine("a   b\tc d", &.{ "a", "b", "c", "d" });
    try testGeneralCmdLine("\"abc\" d e", &.{ "abc", "d", "e" });
    try testGeneralCmdLine("a\\\\\\b d\"e f\"g h", &.{ "a\\\\\\b", "de fg", "h" });
    try testGeneralCmdLine("a\\\\\\\"b c d", &.{ "a\\\"b", "c", "d" });
    try testGeneralCmdLine("a\\\\\\\\\"b c\" d e", &.{ "a\\\\b c", "d", "e" });
    try testGeneralCmdLine("a   b\tc \"d f", &.{ "a", "b", "c", "d f" });
    try testGeneralCmdLine("j k l\\", &.{ "j", "k", "l\\" });
    try testGeneralCmdLine("\"\" x y z\\\\", &.{ "", "x", "y", "z\\\\" });

    try testGeneralCmdLine("\".\\..\\zig-cache\\build\" \"bin\\zig.exe\" \".\\..\" \".\\..\\zig-cache\" \"--help\"", &.{
        ".\\..\\zig-cache\\build",
        "bin\\zig.exe",
        ".\\..",
        ".\\..\\zig-cache",
        "--help",
    });

    try testGeneralCmdLine(
        \\ 'foo' "bar"
    , &.{ "'foo'", "bar" });
}

fn testGeneralCmdLine(input_cmd_line: []const u8, expected_args: []const []const u8) !void {
    var it = try IteratorGeneral(.{}).init(std.testing.allocator, input_cmd_line);
    defer it.deinit();
    for (expected_args) |expected_arg| {
        const arg = it.next().?;
        try testing.expectEqualStrings(expected_arg, arg);
    }
    try testing.expect(it.next() == null);
}

/// Optional parameters for `IteratorGeneral`
pub const IteratorGeneralOptions = struct {
    comments: bool = false,
    single_quotes: bool = false,
};

/// A general Iterator to parse a string into a set of arguments
pub fn IteratorGeneral(comptime options: IteratorGeneralOptions) type {
    return struct {
        allocator: Allocator,
        index: usize = 0,
        cmd_line: []const u8,

        /// Should the cmd_line field be free'd (using the allocator) on deinit()?
        free_cmd_line_on_deinit: bool,

        /// buffer MUST be long enough to hold the cmd_line plus a null terminator.
        /// buffer will we free'd (using the allocator) on deinit()
        buffer: []u8,
        start: usize = 0,
        end: usize = 0,

        pub const Self = @This();

        pub const InitError = error{OutOfMemory};

        /// cmd_line_utf8 MUST remain valid and constant while using this instance
        pub fn init(allocator: Allocator, cmd_line_utf8: []const u8) InitError!Self {
            const buffer = try allocator.alloc(u8, cmd_line_utf8.len + 1);
            errdefer allocator.free(buffer);

            return Self{
                .allocator = allocator,
                .cmd_line = cmd_line_utf8,
                .free_cmd_line_on_deinit = false,
                .buffer = buffer,
            };
        }

        /// cmd_line_utf8 will be free'd (with the allocator) on deinit()
        pub fn initTakeOwnership(allocator: Allocator, cmd_line_utf8: []const u8) InitError!Self {
            const buffer = try allocator.alloc(u8, cmd_line_utf8.len + 1);
            errdefer allocator.free(buffer);

            return Self{
                .allocator = allocator,
                .cmd_line = cmd_line_utf8,
                .free_cmd_line_on_deinit = true,
                .buffer = buffer,
            };
        }

        // Skips over whitespace in the cmd_line.
        // Returns false if the terminating sentinel is reached, true otherwise.
        // Also skips over comments (if supported).
        fn skipWhitespace(self: *Self) bool {
            while (true) : (self.index += 1) {
                const character = if (self.index != self.cmd_line.len) self.cmd_line[self.index] else 0;
                switch (character) {
                    0 => return false,
                    ' ', '\t', '\r', '\n' => continue,
                    '#' => {
                        if (options.comments) {
                            while (true) : (self.index += 1) {
                                switch (self.cmd_line[self.index]) {
                                    '\n' => break,
                                    0 => return false,
                                    else => continue,
                                }
                            }
                            continue;
                        } else {
                            break;
                        }
                    },
                    else => break,
                }
            }
            return true;
        }

        pub fn skip(self: *Self) bool {
            if (!self.skipWhitespace()) {
                return false;
            }

            var backslash_count: usize = 0;
            var in_quote = false;
            while (true) : (self.index += 1) {
                const character = if (self.index != self.cmd_line.len) self.cmd_line[self.index] else 0;
                switch (character) {
                    0 => return true,
                    '"', '\'' => {
                        if (!options.single_quotes and character == '\'') {
                            backslash_count = 0;
                            continue;
                        }
                        const quote_is_real = backslash_count % 2 == 0;
                        if (quote_is_real) {
                            in_quote = !in_quote;
                        }
                    },
                    '\\' => {
                        backslash_count += 1;
                    },
                    ' ', '\t', '\r', '\n' => {
                        if (!in_quote) {
                            return true;
                        }
                        backslash_count = 0;
                    },
                    else => {
                        backslash_count = 0;
                        continue;
                    },
                }
            }
        }

        /// Returns a slice of the internal buffer that contains the next argument.
        /// Returns null when it reaches the end.
        pub fn next(self: *Self) ?[:0]const u8 {
            if (!self.skipWhitespace()) {
                return null;
            }

            var backslash_count: usize = 0;
            var in_quote = false;
            while (true) : (self.index += 1) {
                const character = if (self.index != self.cmd_line.len) self.cmd_line[self.index] else 0;
                switch (character) {
                    0 => {
                        self.emitBackslashes(backslash_count);
                        self.buffer[self.end] = 0;
                        const token = self.buffer[self.start..self.end :0];
                        self.end += 1;
                        self.start = self.end;
                        return token;
                    },
                    '"', '\'' => {
                        if (!options.single_quotes and character == '\'') {
                            self.emitBackslashes(backslash_count);
                            backslash_count = 0;
                            self.emitCharacter(character);
                            continue;
                        }
                        const quote_is_real = backslash_count % 2 == 0;
                        self.emitBackslashes(backslash_count / 2);
                        backslash_count = 0;

                        if (quote_is_real) {
                            in_quote = !in_quote;
                        } else {
                            self.emitCharacter('"');
                        }
                    },
                    '\\' => {
                        backslash_count += 1;
                    },
                    ' ', '\t', '\r', '\n' => {
                        self.emitBackslashes(backslash_count);
                        backslash_count = 0;
                        if (in_quote) {
                            self.emitCharacter(character);
                        } else {
                            self.buffer[self.end] = 0;
                            const token = self.buffer[self.start..self.end :0];
                            self.end += 1;
                            self.start = self.end;
                            return token;
                        }
                    },
                    else => {
                        self.emitBackslashes(backslash_count);
                        backslash_count = 0;
                        self.emitCharacter(character);
                    },
                }
            }
        }

        fn emitBackslashes(self: *Self, emit_count: usize) void {
            var i: usize = 0;
            while (i < emit_count) : (i += 1) {
                self.emitCharacter('\\');
            }
        }

        fn emitCharacter(self: *Self, char: u8) void {
            self.buffer[self.end] = char;
            self.end += 1;
        }

        /// Call to free the internal buffer of the iterator.
        pub fn deinit(self: *Self) void {
            self.allocator.free(self.buffer);

            if (self.free_cmd_line_on_deinit) {
                self.allocator.free(self.cmd_line);
            }
        }
    };
}

test "response file arg parsing" {
    try testResponseFileCmdLine(
        \\a b
        \\c d\
    , &.{ "a", "b", "c", "d\\" });
    try testResponseFileCmdLine("a b c d\\", &.{ "a", "b", "c", "d\\" });

    try testResponseFileCmdLine(
        \\j
        \\ k l # this is a comment \\ \\\ \\\\ "none" "\\" "\\\"
        \\ "m" #another comment
        \\
    , &.{ "j", "k", "l", "m" });

    try testResponseFileCmdLine(
        \\ "" q ""
        \\ "r s # t" "u\" v" #another comment
        \\
    , &.{ "", "q", "", "r s # t", "u\" v" });

    try testResponseFileCmdLine(
        \\ -l"advapi32" a# b#c d#
        \\e\\\
    , &.{ "-ladvapi32", "a#", "b#c", "d#", "e\\\\\\" });

    try testResponseFileCmdLine(
        \\ 'foo' "bar"
    , &.{ "foo", "bar" });
}

fn testResponseFileCmdLine(input_cmd_line: []const u8, expected_args: []const []const u8) !void {
    var it = try IteratorGeneral(.{ .comments = true, .single_quotes = true })
        .init(std.testing.allocator, input_cmd_line);
    defer it.deinit();
    for (expected_args) |expected_arg| {
        const arg = it.next().?;
        try testing.expectEqualStrings(expected_arg, arg);
    }
    try testing.expect(it.next() == null);
}



---
File: /std/process/Child.zig
---

const Child = @This();

const builtin = @import("builtin");
const native_os = builtin.os.tag;

const std = @import("../std.zig");
const Io = std.Io;
const process = std.process;
const File = std.Io.File;
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;

pub const Id = switch (native_os) {
    .windows => std.os.windows.HANDLE,
    .wasi => void,
    else => std.posix.pid_t,
};

/// After `wait` or `kill` is called, this becomes `null`.
/// On Windows this is the hProcess.
/// On POSIX this is the pid.
id: ?Id,
thread_handle: if (native_os == .windows) std.os.windows.HANDLE else void,
/// The writing end of the child process's standard input pipe.
/// Usage requires `process.SpawnOptions.StdIo.pipe`.
stdin: ?File,
/// The reading end of the child process's standard output pipe.
/// Usage requires `process.SpawnOptions.StdIo.pipe`.
stdout: ?File,
/// The reading end of the child process's standard error pipe.
/// Usage requires `process.SpawnOptions.StdIo.pipe`.
stderr: ?File,
/// This is available after calling wait if
/// `request_resource_usage_statistics` was set to `true` before calling
/// `spawn`.
/// TODO move this data into `Term`
resource_usage_statistics: ResourceUsageStatistics = .{},
request_resource_usage_statistics: bool,

pub const ResourceUsageStatistics = struct {
    rusage: @TypeOf(rusage_init) = rusage_init,

    /// Returns the peak resident set size of the child process, in bytes,
    /// if available.
    pub inline fn getMaxRss(rus: ResourceUsageStatistics) ?usize {
        switch (native_os) {
            .dragonfly, .freebsd, .netbsd, .openbsd, .illumos, .linux, .serenity => {
                if (rus.rusage) |ru| {
                    return @as(usize, @intCast(ru.maxrss)) * 1024;
                } else {
                    return null;
                }
            },
            .windows => {
                if (rus.rusage) |ru| {
                    return ru.PeakWorkingSetSize;
                } else {
                    return null;
                }
            },
            .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => {
                if (rus.rusage) |ru| {
                    // Darwin oddly reports in bytes instead of kilobytes.
                    return @as(usize, @intCast(ru.maxrss));
                } else {
                    return null;
                }
            },
            else => return null,
        }
    }

    const rusage_init = switch (native_os) {
        .dragonfly,
        .freebsd,
        .netbsd,
        .openbsd,
        .illumos,
        .linux,
        .serenity,
        .driverkit,
        .ios,
        .maccatalyst,
        .macos,
        .tvos,
        .visionos,
        .watchos,
        => @as(?std.posix.rusage, null),
        .windows => @as(?std.os.windows.PROCESS.VM_COUNTERS, null),
        else => {},
    };
};

pub const Term = union(enum) {
    exited: u8,
    signal: std.posix.SIG,
    stopped: std.posix.SIG,
    unknown: u32,
};

pub const Cwd = union(enum) {
    /// CWD of the child is the same as the current CWD.
    inherit,
    /// On POSIX systems, `fchdir` is called after `fork` using this handle.
    /// On Windows, the path is inferred from the provided handle and that path is used when calling `CreateProcessW`.
    dir: Io.Dir,
    /// On POSIX systems, `chdir` is called after `fork` using this path.
    /// On Windows, this path is used when calling `CreateProcessW`.
    path: []const u8,
};

/// Requests for the operating system to forcibly terminate the child process,
/// then blocks until it terminates, then cleans up all resources.
///
/// Idempotent and does nothing after `wait` returns.
///
/// Uncancelable. Ignores unexpected errors from the operating system.
pub fn kill(child: *Child, io: Io) void {
    if (child.id == null) {
        assert(child.stdin == null);
        assert(child.stdout == null);
        assert(child.stderr == null);
        return;
    }
    io.vtable.childKill(io.userdata, child);
    assert(child.id == null);
}

pub const WaitError = error{
    AccessDenied,
} || Io.Cancelable || Io.UnexpectedError;

/// Blocks until child process terminates and then cleans up all resources.
pub fn wait(child: *Child, io: Io) WaitError!Term {
    assert(child.id != null);
    return io.vtable.childWait(io.userdata, child);
}



---
File: /std/process/Environ.zig
---

const Environ = @This();

const builtin = @import("builtin");
const native_os = builtin.os.tag;

const std = @import("../std.zig");
const Allocator = mem.Allocator;
const assert = std.debug.assert;
const testing = std.testing;
const unicode = std.unicode;
const posix = std.posix;
const mem = std.mem;

/// Unmodified, unprocessed data provided by the operating system.
block: Block,

pub const empty: Environ = .{ .block = .empty };

/// On WASI without libc, this is `void` because the environment has to be
/// queried and heap-allocated at runtime.
///
/// On Windows, the memory pointed at by the PEB changes when the environment
/// is modified, so a long-lived pointer cannot be used. Therefore, on this
/// operating system `void` is also used.
pub const Block = switch (native_os) {
    .windows => GlobalBlock,
    .wasi, .emscripten => switch (builtin.link_libc) {
        false => GlobalBlock,
        true => PosixBlock,
    },
    .freestanding, .other => GlobalBlock,
    else => PosixBlock,
};

pub const GlobalBlock = struct {
    use_global: bool,

    pub const empty: GlobalBlock = .{ .use_global = false };
    pub const global: GlobalBlock = .{ .use_global = true };

    pub fn deinit(_: GlobalBlock, _: Allocator) void {}

    pub fn isEmpty(block: GlobalBlock) bool {
        return !block.use_global;
    }
};

pub const PosixBlock = struct {
    slice: [:null]const ?[*:0]const u8,

    pub const empty: PosixBlock = .{ .slice = &.{} };

    pub fn deinit(block: PosixBlock, gpa: Allocator) void {
        for (block.slice) |entry| gpa.free(mem.span(entry.?));
        gpa.free(block.slice);
    }

    pub fn isEmpty(block: PosixBlock) bool {
        return block.slice.len == 0;
    }

    pub const View = struct {
        slice: []const [*:0]const u8,

        pub fn isEmpty(v: View) bool {
            return v.slice.len == 0;
        }
    };
    pub fn view(block: PosixBlock) View {
        return .{ .slice = @ptrCast(block.slice) };
    }
};

pub const WindowsBlock = struct {
    slice: [:0]const u16,

    pub const empty: WindowsBlock = .{ .slice = &.{0} };

    pub fn deinit(block: WindowsBlock, gpa: Allocator) void {
        gpa.free(block.slice);
    }

    pub fn isEmpty(block: WindowsBlock) bool {
        return block.slice[0] == 0;
    }

    pub const View = struct {
        ptr: [*:0]const u16,

        pub fn isEmpty(v: View) bool {
            return v.ptr[0] == 0;
        }
    };
    pub fn view(block: WindowsBlock) View {
        return .{ .ptr = block.slice.ptr };
    }
};

pub const Map = struct {
    array_hash_map: ArrayHashMap,
    allocator: Allocator,

    const ArrayHashMap = std.ArrayHashMapUnmanaged([]const u8, []const u8, EnvNameHashContext, false);

    pub const Size = usize;

    pub const EnvNameHashContext = struct {
        pub fn hash(self: @This(), s: []const u8) u32 {
            _ = self;
            switch (native_os) {
                else => return std.array_hash_map.hashString(s),
                .windows => {
                    var h = std.hash.Wyhash.init(0);
                    var it = unicode.Wtf8View.initUnchecked(s).iterator();
                    while (it.nextCodepoint()) |cp| {
                        const cp_upper = if (std.math.cast(u16, cp)) |wtf16|
                            std.os.windows.toUpperWtf16(wtf16)
                        else
                            cp;
                        h.update(&[_]u8{
                            @truncate(cp_upper >> 0),
                            @truncate(cp_upper >> 8),
                            @truncate(cp_upper >> 16),
                        });
                    }
                    return @truncate(h.final());
                },
            }
        }

        pub fn eql(self: @This(), a: []const u8, b: []const u8, b_index: usize) bool {
            _ = self;
            _ = b_index;
            return eqlKeys(a, b);
        }
    };
    fn eqlKeys(a: []const u8, b: []const u8) bool {
        return switch (native_os) {
            else => std.array_hash_map.eqlString(a, b),
            .windows => std.os.windows.eqlIgnoreCaseWtf8(a, b),
        };
    }

    pub fn validateKeyForPut(key: []const u8) bool {
        switch (native_os) {
            else => return key.len > 0 and mem.findAny(u8, key, &.{ 0, '=' }) == null,
            .windows => {
                if (!unicode.wtf8ValidateSlice(key)) return false;
                return key.len > 0 and key[0] != 0 and mem.findAnyPos(u8, key, 1, &.{ 0, '=' }) == null;
            },
        }
    }

    pub fn validateKeyForFetch(key: []const u8) bool {
        if (native_os == .windows and !unicode.wtf8ValidateSlice(key)) return false;
        return true;
    }

    /// Create a Map backed by a specific allocator.
    /// That allocator will be used for both backing allocations
    /// and string deduplication.
    pub fn init(allocator: Allocator) Map {
        return .{ .array_hash_map = .empty, .allocator = allocator };
    }

    /// Free the backing storage of the map, as well as all
    /// of the stored keys and values.
    pub fn deinit(self: *Map) void {
        const gpa = self.allocator;
        for (self.keys()) |key| gpa.free(key);
        for (self.values()) |value| gpa.free(value);
        self.array_hash_map.deinit(gpa);
        self.* = undefined;
    }

    pub fn keys(map: *const Map) [][]const u8 {
        return map.array_hash_map.keys();
    }

    pub fn values(map: *const Map) [][]const u8 {
        return map.array_hash_map.values();
    }

    pub fn putPosixBlock(map: *Map, view: PosixBlock.View) Allocator.Error!void {
        for (view.slice) |entry| {
            var entry_i: usize = 0;
            while (entry[entry_i] != 0 and entry[entry_i] != '=') : (entry_i += 1) {}
            const key = entry[0..entry_i];

            var end_i: usize = entry_i;
            while (entry[end_i] != 0) : (end_i += 1) {}
            const value = entry[entry_i + 1 .. end_i];

            try map.put(key, value);
        }
    }

    pub fn putWindowsBlock(map: *Map, view: WindowsBlock.View) Allocator.Error!void {
        var i: usize = 0;
        while (view.ptr[i] != 0) {
            const key_start = i;

            // There are some special environment variables that start with =,
            // so we need a special case to not treat = as a key/value separator
            // if it's the first character.
            // https://devblogs.microsoft.com/oldnewthing/20100506-00/?p=14133
            if (view.ptr[key_start] == '=') i += 1;

            while (view.ptr[i] != 0 and view.ptr[i] != '=') : (i += 1) {}
            const key_w = view.ptr[key_start..i];
            const key = try unicode.wtf16LeToWtf8Alloc(map.allocator, key_w);
            errdefer map.allocator.free(key);

            if (view.ptr[i] == '=') i += 1;

            const value_start = i;
            while (view.ptr[i] != 0) : (i += 1) {}
            const value_w = view.ptr[value_start..i];
            const value = try unicode.wtf16LeToWtf8Alloc(map.allocator, value_w);
            errdefer map.allocator.free(value);

            i += 1; // skip over null byte

            try map.putMove(key, value);
        }
    }

    /// Same as `put` but the key and value become owned by the Map rather
    /// than being copied.
    /// If `putMove` fails, the ownership of key and value does not transfer.
    ///
    /// Asserts that `key` is valid:
    /// - It cannot contain a NUL (`'\x00') byte.
    /// - It must have a length > 0.
    /// - It cannot contain `=`, except on Windows where only the first code point is allowed to be `=`.
    /// - On Windows, it must be valid [WTF-8](https://wtf-8.codeberg.page/).
    pub fn putMove(self: *Map, key: []u8, value: []u8) Allocator.Error!void {
        assert(validateKeyForPut(key));
        const gpa = self.allocator;
        const get_or_put = try self.array_hash_map.getOrPut(gpa, key);
        if (get_or_put.found_existing) {
            gpa.free(get_or_put.key_ptr.*);
            gpa.free(get_or_put.value_ptr.*);
            get_or_put.key_ptr.* = key;
        }
        get_or_put.value_ptr.* = value;
    }

    /// `key` and `value` are copied into the Map.
    ///
    /// Asserts that `key` is valid:
    /// - It cannot contain a NUL (`'\x00') byte.
    /// - It must have a length > 0.
    /// - It cannot contain `=`, except on Windows where only the first code point is allowed to be `=`.
    /// - On Windows, it must be valid [WTF-8](https://wtf-8.codeberg.page/).
    pub fn put(self: *Map, key: []const u8, value: []const u8) Allocator.Error!void {
        assert(validateKeyForPut(key));
        const gpa = self.allocator;
        const value_copy = try gpa.dupe(u8, value);
        errdefer gpa.free(value_copy);
        const get_or_put = try self.array_hash_map.getOrPut(gpa, key);
        errdefer {
            if (!get_or_put.found_existing) assert(self.array_hash_map.pop() != null);
        }
        if (get_or_put.found_existing) {
            gpa.free(get_or_put.value_ptr.*);
        } else {
            get_or_put.key_ptr.* = try gpa.dupe(u8, key);
        }
        get_or_put.value_ptr.* = value_copy;
    }

    /// Find the address of the value associated with a key.
    /// The returned pointer is invalidated if the map resizes.
    /// On Windows, asserts that `key` is valid [WTF-8](https://wtf-8.codeberg.page/).
    pub fn getPtr(self: Map, key: []const u8) ?*[]const u8 {
        assert(validateKeyForFetch(key));
        return self.array_hash_map.getPtr(key);
    }

    /// Return the map's copy of the value associated with
    /// a key.  The returned string is invalidated if this
    /// key is removed from the map.
    /// On Windows, asserts that `key` is valid [WTF-8](https://wtf-8.codeberg.page/).
    pub fn get(self: Map, key: []const u8) ?[]const u8 {
        assert(validateKeyForFetch(key));
        return self.array_hash_map.get(key);
    }

    /// On Windows, asserts that `key` is valid [WTF-8](https://wtf-8.codeberg.page/).
    pub fn contains(m: *const Map, key: []const u8) bool {
        assert(validateKeyForFetch(key));
        return m.array_hash_map.contains(key);
    }

    /// If there is an entry with a matching key, it is deleted from the hash
    /// map. The entry is removed from the underlying array by swapping it with
    /// the last element.
    ///
    /// Returns true if an entry was removed, false otherwise.
    ///
    /// This invalidates the value returned by get() for this key.
    /// On Windows, asserts that `key` is valid [WTF-8](https://wtf-8.codeberg.page/).
    pub fn swapRemove(self: *Map, key: []const u8) bool {
        assert(validateKeyForFetch(key));
        const kv = self.array_hash_map.fetchSwapRemove(key) orelse return false;
        const gpa = self.allocator;
        gpa.free(kv.key);
        gpa.free(kv.value);
        return true;
    }

    /// If there is an entry with a matching key, it is deleted from the map.
    /// The entry is removed from the underlying array by shifting all elements
    /// forward, thereby maintaining the current ordering.
    ///
    /// Returns true if an entry was removed, false otherwise.
    ///
    /// This invalidates the value returned by get() for this key.
    /// On Windows, asserts that `key` is valid [WTF-8](https://wtf-8.codeberg.page/).
    pub fn orderedRemove(self: *Map, key: []const u8) bool {
        assert(validateKeyForFetch(key));
        const kv = self.array_hash_map.fetchOrderedRemove(key) orelse return false;
        const gpa = self.allocator;
        gpa.free(kv.key);
        gpa.free(kv.value);
        return true;
    }

    /// Returns the number of KV pairs stored in the map.
    pub fn count(self: Map) Size {
        return self.array_hash_map.count();
    }

    /// Returns an iterator over entries in the map.
    pub fn iterator(self: *const Map) ArrayHashMap.Iterator {
        return self.array_hash_map.iterator();
    }

    /// Returns a full copy of `em` allocated with `gpa`, which is not necessarily
    /// the same allocator used to allocate `em`.
    pub fn clone(m: *const Map, gpa: Allocator) Allocator.Error!Map {
        // Since we need to dupe the keys and values, the only way for error handling to not be a
        // nightmare is to add keys to an empty map one-by-one. This could be avoided if this
        // abstraction were a bit less... OOP-esque.
        var new: Map = .init(gpa);
        errdefer new.deinit();
        try new.array_hash_map.ensureUnusedCapacity(gpa, m.array_hash_map.count());
        for (m.array_hash_map.keys(), m.array_hash_map.values()) |key, value| {
            try new.put(key, value);
        }
        return new;
    }

    /// Creates a null-delimited environment variable block in the format
    /// expected by POSIX, from a hash map plus options.
    pub fn createPosixBlock(
        map: *const Map,
        gpa: Allocator,
        options: CreatePosixBlockOptions,
    ) Allocator.Error!PosixBlock {
        const ZigProgressAction = enum { nothing, edit, delete, add };
        const zig_progress_action: ZigProgressAction = action: {
            const fd = options.zig_progress_fd orelse break :action .nothing;
            const exists = map.contains("ZIG_PROGRESS");
            if (fd >= 0) {
                break :action if (exists) .edit else .add;
            } else {
                if (exists) break :action .delete;
            }
            break :action .nothing;
        };

        const envp = try gpa.allocSentinel(?[*:0]u8, len: {
            var len: usize = map.count();
            switch (zig_progress_action) {
                .add => len += 1,
                .delete => len -= 1,
                .nothing, .edit => {},
            }
            break :len len;
        }, null);
        var envp_len: usize = 0;
        errdefer {
            envp[envp_len] = null;
            PosixBlock.deinit(.{ .slice = envp[0..envp_len :null] }, gpa);
        }

        if (zig_progress_action == .add) {
            envp[envp_len] = try std.fmt.allocPrintSentinel(gpa, "ZIG_PROGRESS={d}", .{options.zig_progress_fd.?}, 0);
            envp_len += 1;
        }

        for (map.keys(), map.values()) |key, value| {
            if (mem.eql(u8, key, "ZIG_PROGRESS")) switch (zig_progress_action) {
                .add => unreachable,
                .delete => continue,
                .edit => {
                    envp[envp_len] = try std.fmt.allocPrintSentinel(gpa, "{s}={d}", .{
                        key, options.zig_progress_fd.?,
                    }, 0);
                    envp_len += 1;
                    continue;
                },
                .nothing => {},
            };

            envp[envp_len] = try std.fmt.allocPrintSentinel(gpa, "{s}={s}", .{ key, value }, 0);
            envp_len += 1;
        }

        assert(envp_len == envp.len);
        return .{ .slice = envp };
    }

    /// Caller owns result.
    pub fn createWindowsBlock(
        map: *const Map,
        gpa: Allocator,
        options: CreateWindowsBlockOptions,
    ) error{ OutOfMemory, InvalidWtf8 }!WindowsBlock {
        // count bytes needed
        const max_chars_needed = max_chars_needed: {
            var max_chars_needed: usize = "\x00".len;
            if (options.zig_progress_handle) |handle| if (handle != std.os.windows.INVALID_HANDLE_VALUE) {
                max_chars_needed += std.fmt.count("ZIG_PROGRESS={d}\x00", .{@intFromPtr(handle)});
            };
            for (map.keys(), map.values()) |key, value| {
                if (options.zig_progress_handle != null and eqlKeys(key, "ZIG_PROGRESS")) continue;
                max_chars_needed += key.len + "=".len + value.len + "\x00".len;
            }
            break :max_chars_needed @max("\x00\x00".len, max_chars_needed);
        };
        const block = try gpa.alloc(u16, max_chars_needed);
        errdefer gpa.free(block);

        var i: usize = 0;
        if (options.zig_progress_handle) |handle| if (handle != std.os.windows.INVALID_HANDLE_VALUE) {
            @memcpy(
                block[i..][0.."ZIG_PROGRESS=".len],
                &[_]u16{ 'Z', 'I', 'G', '_', 'P', 'R', 'O', 'G', 'R', 'E', 'S', 'S', '=' },
            );
            i += "ZIG_PROGRESS=".len;
            var value_buf: [std.fmt.count("{d}", .{std.math.maxInt(usize)})]u8 = undefined;
            const value = std.fmt.bufPrint(&value_buf, "{d}", .{@intFromPtr(handle)}) catch unreachable;
            for (block[i..][0..value.len], value) |*r, v| r.* = v;
            i += value.len;
            block[i] = 0;
            i += 1;
        };
        for (map.keys(), map.values()) |key, value| {
            if (options.zig_progress_handle != null and eqlKeys(key, "ZIG_PROGRESS")) continue;
            i += try unicode.wtf8ToWtf16Le(block[i..], key);
            block[i] = '=';
            i += 1;
            i += try unicode.wtf8ToWtf16Le(block[i..], value);
            block[i] = 0;
            i += 1;
        }
        // An empty environment is a special case that requires a redundant
        // NUL terminator. CreateProcess will read the second code unit even
        // though theoretically the first should be enough to recognize that the
        // environment is empty (see https://nullprogram.com/blog/2023/08/23/)
        for (0..2) |_| {
            block[i] = 0;
            i += 1;
            if (i >= 2) break;
        } else unreachable;
        const reallocated = try gpa.realloc(block, i);
        return .{ .slice = reallocated[0 .. i - 1 :0] };
    }
};

pub const CreateMapError = error{
    OutOfMemory,
    /// WASI-only. `environ_sizes_get` or `environ_get` failed for an
    /// unanticipated, undocumented reason.
    Unexpected,
};

/// Allocates a `Map` and copies environment block into it.
pub fn createMap(env: Environ, allocator: Allocator) CreateMapError!Map {
    var map = Map.init(allocator);
    errdefer map.deinit();
    if (native_os == .windows) empty: {
        if (!env.block.use_global) break :empty;

        const peb = std.os.windows.peb();
        assert(std.os.windows.ntdll.RtlEnterCriticalSection(peb.FastPebLock) == .SUCCESS);
        defer assert(std.os.windows.ntdll.RtlLeaveCriticalSection(peb.FastPebLock) == .SUCCESS);
        try map.putWindowsBlock(.{ .ptr = peb.ProcessParameters.Environment });
    } else if (native_os == .wasi and !builtin.link_libc) empty: {
        if (!env.block.use_global) break :empty;

        var environ_count: usize = undefined;
        var environ_buf_size: usize = undefined;

        const environ_sizes_get_ret = std.os.wasi.environ_sizes_get(&environ_count, &environ_buf_size);
        if (environ_sizes_get_ret != .SUCCESS) {
            return posix.unexpectedErrno(environ_sizes_get_ret);
        }

        if (environ_count == 0) {
            return map;
        }

        const environ = try allocator.alloc([*:0]u8, environ_count);
        defer allocator.free(environ);
        const environ_buf = try allocator.alloc(u8, environ_buf_size);
        defer allocator.free(environ_buf);

        const environ_get_ret = std.os.wasi.environ_get(environ.ptr, environ_buf.ptr);
        if (environ_get_ret != .SUCCESS) {
            return posix.unexpectedErrno(environ_get_ret);
        }

        try map.putPosixBlock(.{ .slice = environ });
    } else try map.putPosixBlock(env.block.view());
    return map;
}

pub const ContainsError = error{
    OutOfMemory,
    /// On Windows, environment variable keys provided by the user must be
    /// valid [WTF-8](https://wtf-8.codeberg.page/). This error is unreachable
    /// if the key is statically known to be valid.
    InvalidWtf8,
    /// WASI-only. `environ_sizes_get` or `environ_get` failed for an
    /// unexpected reason.
    Unexpected,
};

/// On Windows, if `key` is not valid [WTF-8](https://wtf-8.codeberg.page/),
/// then `error.InvalidWtf8` is returned.
///
/// See also:
/// * `createMap`
/// * `containsConstant`
/// * `containsUnempty`
pub fn contains(environ: Environ, gpa: Allocator, key: []const u8) ContainsError!bool {
    if (native_os == .windows and !unicode.wtf8ValidateSlice(key)) return error.InvalidWtf8;
    var map = try createMap(environ, gpa);
    defer map.deinit();
    return map.contains(key);
}

/// On Windows, if `key` is not valid [WTF-8](https://wtf-8.codeberg.page/),
/// then `error.InvalidWtf8` is returned.
///
/// See also:
/// * `createMap`
/// * `containsUnemptyConstant`
/// * `contains`
pub fn containsUnempty(environ: Environ, gpa: Allocator, key: []const u8) ContainsError!bool {
    if (native_os == .windows and !unicode.wtf8ValidateSlice(key)) return error.InvalidWtf8;
    var map = try createMap(environ, gpa);
    defer map.deinit();
    const value = map.get(key) orelse return false;
    return value.len != 0;
}

/// This function is unavailable on WASI without libc due to the memory
/// allocation requirement.
///
/// On Windows, `key` must be valid [WTF-8](https://wtf-8.codeberg.page/),
///
/// See also:
/// * `contains`
/// * `containsUnemptyConstant`
/// * `createMap`
pub inline fn containsConstant(environ: Environ, comptime key: []const u8) bool {
    if (native_os == .windows) {
        const key_w = comptime unicode.wtf8ToWtf16LeStringLiteral(key);
        return getWindows(environ, key_w) != null;
    } else {
        return getPosix(environ, key) != null;
    }
}

/// This function is unavailable on WASI without libc due to the memory
/// allocation requirement.
///
/// On Windows, `key` must be valid [WTF-8](https://wtf-8.codeberg.page/),
///
/// See also:
/// * `containsUnempty`
/// * `containsConstant`
/// * `createMap`
pub inline fn containsUnemptyConstant(environ: Environ, comptime key: []const u8) bool {
    if (native_os == .windows) {
        const key_w = comptime unicode.wtf8ToWtf16LeStringLiteral(key);
        const value = getWindows(environ, key_w) orelse return false;
        return value.len != 0;
    } else {
        const value = getPosix(environ, key) orelse return false;
        return value.len != 0;
    }
}

/// This function is unavailable on WASI without libc due to the memory
/// allocation requirement.
///
/// See also:
/// * `getWindows`
/// * `createMap`
pub fn getPosix(environ: Environ, key: []const u8) ?[:0]const u8 {
    if (mem.findScalar(u8, key, '=') != null) return null;
    for (environ.block.view().slice) |entry| {
        var entry_i: usize = 0;
        while (entry[entry_i] != 0) : (entry_i += 1) {
            if (entry_i == key.len) break;
            if (entry[entry_i] != key[entry_i]) break;
        }
        if ((entry_i != key.len) or (entry[entry_i] != '=')) continue;

        return mem.sliceTo(entry + entry_i + 1, 0);
    }
    return null;
}

/// Windows-only. Get an environment variable with a null-terminated, WTF-16
/// encoded name.
///
/// This function performs a Unicode-aware case-insensitive lookup using
/// RtlEqualUnicodeString.
///
/// See also:
/// * `createMap`
/// * `containsConstant`
/// * `contains`
pub fn getWindows(environ: Environ, key: [*:0]const u16) ?[:0]const u16 {
    // '=' anywhere but the start makes this an invalid environment variable name.
    const key_slice = mem.sliceTo(key, 0);
    if (key_slice.len == 0 or mem.findScalar(u16, key_slice[1..], '=') != null) return null;

    if (!environ.block.use_global) return null;

    const peb = std.os.windows.peb();
    assert(std.os.windows.ntdll.RtlEnterCriticalSection(peb.FastPebLock) == .SUCCESS);
    defer assert(std.os.windows.ntdll.RtlLeaveCriticalSection(peb.FastPebLock) == .SUCCESS);
    const ptr = peb.ProcessParameters.Environment;

    var i: usize = 0;
    while (ptr[i] != 0) {
        const key_value = mem.sliceTo(ptr[i..], 0);

        // There are some special environment variables that start with =,
        // so we need a special case to not treat = as a key/value separator
        // if it's the first character.
        // https://devblogs.microsoft.com/oldnewthing/20100506-00/?p=14133
        const equal_index = mem.findScalarPos(u16, key_value, 1, '=') orelse {
            // This is enforced by CreateProcess.
            // If violated, CreateProcess will fail with INVALID_PARAMETER.
            unreachable; // must contain a =
        };

        const this_key = key_value[0..equal_index];
        if (std.os.windows.eqlIgnoreCaseWtf16(key_slice, this_key)) {
            return key_value[equal_index + 1 ..];
        }

        // skip past the NUL terminator
        i += key_value.len + 1;
    }
    return null;
}

pub const GetAllocError = error{
    OutOfMemory,
    EnvironmentVariableMissing,
    /// On Windows, environment variable keys provided by the user must be
    /// valid [WTF-8](https://wtf-8.codeberg.page/). This error is unreachable
    /// if the key is statically known to be valid.
    InvalidWtf8,
};

/// Caller owns returned memory.
///
/// On Windows:
/// * If `key` is not valid [WTF-8](https://wtf-8.codeberg.page/), then
///   `error.InvalidWtf8` is returned.
/// * The returned value is encoded as [WTF-8](https://wtf-8.codeberg.page/).
///
/// On other platforms, the value is an opaque sequence of bytes with no
/// particular encoding.
///
/// See also:
/// * `createMap`
pub fn getAlloc(environ: Environ, gpa: Allocator, key: []const u8) GetAllocError![]u8 {
    if (native_os == .windows and !unicode.wtf8ValidateSlice(key)) return error.InvalidWtf8;
    var map = createMap(environ, gpa) catch return error.OutOfMemory;
    defer map.deinit();
    const val = map.get(key) orelse return error.EnvironmentVariableMissing;
    return gpa.dupe(u8, val);
}

pub const CreatePosixBlockOptions = struct {
    /// `null` means to leave the `ZIG_PROGRESS` environment variable unmodified.
    /// If non-null, negative means to remove the environment variable, and >= 0
    /// means to provide it with the given integer.
    zig_progress_fd: ?i32 = null,
};

/// Creates a null-delimited environment variable block in the format expected
/// by POSIX, from a different one.
pub fn createPosixBlock(
    existing: Environ,
    gpa: Allocator,
    options: CreatePosixBlockOptions,
) Allocator.Error!PosixBlock {
    const contains_zig_progress = for (existing.block.view().slice) |entry| {
        if (mem.eql(u8, mem.sliceTo(entry, '='), "ZIG_PROGRESS")) break true;
    } else false;

    const ZigProgressAction = enum { nothing, edit, delete, add };
    const zig_progress_action: ZigProgressAction = action: {
        const fd = options.zig_progress_fd orelse break :action .nothing;
        if (fd >= 0) {
            break :action if (contains_zig_progress) .edit else .add;
        } else {
            if (contains_zig_progress) break :action .delete;
        }
        break :action .nothing;
    };

    const envp = try gpa.allocSentinel(?[*:0]u8, len: {
        var len: usize = existing.block.slice.len;
        switch (zig_progress_action) {
            .add => len += 1,
            .delete => len -= 1,
            .nothing, .edit => {},
        }
        break :len len;
    }, null);
    var envp_len: usize = 0;
    errdefer {
        envp[envp_len] = null;
        PosixBlock.deinit(.{ .slice = envp[0..envp_len :null] }, gpa);
    }
    if (zig_progress_action == .add) {
        envp[envp_len] = try std.fmt.allocPrintSentinel(gpa, "ZIG_PROGRESS={d}", .{options.zig_progress_fd.?}, 0);
        envp_len += 1;
    }

    var existing_index: usize = 0;
    while (existing.block.slice[existing_index]) |entry| : (existing_index += 1) {
        if (mem.eql(u8, mem.sliceTo(entry, '='), "ZIG_PROGRESS")) switch (zig_progress_action) {
            .add => unreachable,
            .delete => continue,
            .edit => {
                envp[envp_len] = try std.fmt.allocPrintSentinel(gpa, "ZIG_PROGRESS={d}", .{options.zig_progress_fd.?}, 0);
                envp_len += 1;
                continue;
            },
            .nothing => {},
        };
        envp[envp_len] = try gpa.dupeZ(u8, mem.span(entry));
        envp_len += 1;
    }

    assert(envp_len == envp.len);
    return .{ .slice = envp };
}

pub const CreateWindowsBlockOptions = struct {
    /// `null` means to leave the `ZIG_PROGRESS` environment variable unmodified.
    /// If non-null, `std.os.windows.INVALID_HANDLE_VALUE` means to remove the
    /// environment variable, otherwise provide it with the given handle as an integer.
    zig_progress_handle: ?std.os.windows.HANDLE = null,
};

/// Creates a null-delimited environment variable block in the format expected
/// by POSIX, from a different one.
pub fn createWindowsBlock(
    existing: Environ,
    gpa: Allocator,
    options: CreateWindowsBlockOptions,
) Allocator.Error!WindowsBlock {
    if (!existing.block.use_global) return .{
        .slice = try gpa.dupeSentinel(u16, WindowsBlock.empty.slice, 0),
    };
    const peb = std.os.windows.peb();
    assert(std.os.windows.ntdll.RtlEnterCriticalSection(peb.FastPebLock) == .SUCCESS);
    defer assert(std.os.windows.ntdll.RtlLeaveCriticalSection(peb.FastPebLock) == .SUCCESS);
    const existing_block = peb.ProcessParameters.Environment;
    var ranges: [2]struct { start: usize, end: usize } = undefined;
    var ranges_len: usize = 0;
    ranges[ranges_len].start = 0;
    const zig_progress_key = [_]u16{ 'Z', 'I', 'G', '_', 'P', 'R', 'O', 'G', 'R', 'E', 'S', 'S', '=' };
    const needed_len = needed_len: {
        var needed_len: usize = "\x00".len;
        if (options.zig_progress_handle) |handle| if (handle != std.os.windows.INVALID_HANDLE_VALUE) {
            needed_len += std.fmt.count("ZIG_PROGRESS={d}\x00", .{@intFromPtr(handle)});
        };
        var i: usize = 0;
        while (existing_block[i] != 0) {
            const start = i;
            const entry = mem.sliceTo(existing_block[start..], 0);
            i += entry.len + "\x00".len;
            if (options.zig_progress_handle != null and entry.len >= zig_progress_key.len and
                std.os.windows.eqlIgnoreCaseWtf16(entry[0..zig_progress_key.len], &zig_progress_key))
            {
                ranges[ranges_len].end = start;
                ranges_len += 1;
                ranges[ranges_len].start = i;
            } else needed_len += entry.len + "\x00".len;
        }
        ranges[ranges_len].end = i;
        ranges_len += 1;
        break :needed_len @max("\x00\x00".len, needed_len);
    };
    const block = try gpa.alloc(u16, needed_len);
    errdefer gpa.free(block);
    var i: usize = 0;
    if (options.zig_progress_handle) |handle| if (handle != std.os.windows.INVALID_HANDLE_VALUE) {
        @memcpy(block[i..][0..zig_progress_key.len], &zig_progress_key);
        i += zig_progress_key.len;
        var value_buf: [std.fmt.count("{d}", .{std.math.maxInt(usize)})]u8 = undefined;
        const value = std.fmt.bufPrint(&value_buf, "{d}", .{@intFromPtr(handle)}) catch unreachable;
        for (block[i..][0..value.len], value) |*r, v| r.* = v;
        i += value.len;
        block[i] = 0;
        i += 1;
    };
    for (ranges[0..ranges_len]) |range| {
        const range_len = range.end - range.start;
        @memcpy(block[i..][0..range_len], existing_block[range.start..range.end]);
        i += range_len;
    }
    // An empty environment is a special case that requires a redundant
    // NUL terminator. CreateProcess will read the second code unit even
    // though theoretically the first should be enough to recognize that the
    // environment is empty (see https://nullprogram.com/blog/2023/08/23/)
    for (0..2) |_| {
        block[i] = 0;
        i += 1;
        if (i >= 2) break;
    } else unreachable;
    assert(i == block.len);
    return .{ .slice = block[0 .. i - 1 :0] };
}

test "Map.createPosixBlock" {
    const gpa = testing.allocator;

    var envmap = Map.init(gpa);
    defer envmap.deinit();

    try envmap.put("HOME", "/home/ifreund");
    try envmap.put("WAYLAND_DISPLAY", "wayland-1");
    try envmap.put("DISPLAY", ":1");
    try envmap.put("DEBUGINFOD_URLS", " ");
    try envmap.put("XCURSOR_SIZE", "24");

    const block = try envmap.createPosixBlock(gpa, .{});
    defer block.deinit(gpa);

    try testing.expectEqual(@as(usize, 5), block.slice.len);

    for (&[_][]const u8{
        "HOME=/home/ifreund",
        "WAYLAND_DISPLAY=wayland-1",
        "DISPLAY=:1",
        "DEBUGINFOD_URLS= ",
        "XCURSOR_SIZE=24",
    }, block.slice) |expected, actual| try testing.expectEqualStrings(expected, mem.span(actual.?));
}

test Map {
    const gpa = testing.allocator;

    var env: Map = .init(gpa);
    defer env.deinit();

    try env.put("SOMETHING_NEW", "hello");
    try testing.expectEqualStrings("hello", env.get("SOMETHING_NEW").?);
    try testing.expectEqual(@as(Map.Size, 1), env.count());

    // overwrite
    try env.put("SOMETHING_NEW", "something");
    try testing.expectEqualStrings("something", env.get("SOMETHING_NEW").?);
    try testing.expectEqual(@as(Map.Size, 1), env.count());

    // a new longer name to test the Windows-specific conversion buffer
    try env.put("SOMETHING_NEW_AND_LONGER", "1");
    try testing.expectEqualStrings("1", env.get("SOMETHING_NEW_AND_LONGER").?);
    try testing.expectEqual(@as(Map.Size, 2), env.count());

    // case insensitivity on Windows only
    if (native_os == .windows) {
        try testing.expectEqualStrings("1", env.get("something_New_aNd_LONGER").?);
    } else {
        try testing.expect(null == env.get("something_New_aNd_LONGER"));
    }

    var it = env.iterator();
    var count: Map.Size = 0;
    while (it.next()) |entry| {
        const is_an_expected_name = mem.eql(u8, "SOMETHING_NEW", entry.key_ptr.*) or mem.eql(u8, "SOMETHING_NEW_AND_LONGER", entry.key_ptr.*);
        try testing.expect(is_an_expected_name);
        count += 1;
    }
    try testing.expectEqual(@as(Map.Size, 2), count);

    try testing.expect(env.swapRemove("SOMETHING_NEW"));
    try testing.expect(!env.swapRemove("SOMETHING_NEW"));
    try testing.expect(env.get("SOMETHING_NEW") == null);
    try testing.expect(!env.contains("SOMETHING_NEW"));

    try testing.expectEqual(@as(Map.Size, 1), env.count());

    if (native_os == .windows) {
        // test Unicode case-insensitivity on Windows
        try env.put("КИРиллИЦА", "something else");
        try testing.expectEqualStrings("something else", env.get("кириллица").?);

        // and WTF-8 that's not valid UTF-8
        const wtf8_with_surrogate_pair = try unicode.wtf16LeToWtf8Alloc(gpa, &[_]u16{
            mem.nativeToLittle(u16, 0xD83D), // unpaired high surrogate
        });
        defer gpa.free(wtf8_with_surrogate_pair);

        try env.put(wtf8_with_surrogate_pair, wtf8_with_surrogate_pair);
        try testing.expectEqualSlices(u8, wtf8_with_surrogate_pair, env.get(wtf8_with_surrogate_pair).?);
    }
}

test "convert from Environ to Map and back again" {
    if (native_os == .windows) return;
    if (native_os == .wasi and !builtin.link_libc) return;

    const gpa = testing.allocator;

    var map: Map = .init(gpa);
    defer map.deinit();
    try map.put("FOO", "BAR");
    try map.put("A", "");

    const environ: Environ = .{ .block = try map.createPosixBlock(gpa, .{}) };
    defer environ.block.deinit(gpa);

    try testing.expectEqual(true, environ.contains(gpa, "FOO"));
    try testing.expectEqual(false, environ.contains(gpa, "BAR"));
    try testing.expectEqual(true, environ.contains(gpa, "A"));
    try testing.expectEqual(true, environ.containsConstant("A"));
    try testing.expectEqual(false, environ.containsUnempty(gpa, "A"));
    try testing.expectEqual(false, environ.containsUnemptyConstant("A"));
    try testing.expectEqual(false, environ.contains(gpa, "B"));

    try testing.expectError(error.EnvironmentVariableMissing, environ.getAlloc(gpa, "BOGUS"));
    {
        const value = try environ.getAlloc(gpa, "FOO");
        defer gpa.free(value);
        try testing.expectEqualStrings("BAR", value);
    }

    var map2 = try environ.createMap(gpa);
    defer map2.deinit();

    try testing.expectEqualDeep(map.keys(), map2.keys());
    try testing.expectEqualDeep(map.values(), map2.values());
}

test "Map.putPosixBlock" {
    const gpa = testing.allocator;

    var map: Map = .init(gpa);
    defer map.deinit();

    try map.put("FOO", "BAR");
    try map.put("A", "");
    try map.put("ZIG_PROGRESS", "unchanged");

    const block = try map.createPosixBlock(gpa, .{});
    defer block.deinit(gpa);

    var map2: Map = .init(gpa);
    defer map2.deinit();
    try map2.putPosixBlock(block.view());

    try testing.expectEqualDeep(&[_][]const u8{ "FOO", "A", "ZIG_PROGRESS" }, map2.keys());
    try testing.expectEqualDeep(&[_][]const u8{ "BAR", "", "unchanged" }, map2.values());
}

test "Map.putWindowsBlock" {
    if (native_os != .windows) return;

    const gpa = testing.allocator;

    var map: Map = .init(gpa);
    defer map.deinit();

    try map.put("FOO", "BAR");
    try map.put("A", "");
    try map.put("=B", "");
    try map.put("ZIG_PROGRESS", "unchanged");

    const block = try map.createWindowsBlock(gpa, .{});
    defer block.deinit(gpa);

    var map2: Map = .init(gpa);
    defer map2.deinit();
    try map2.putWindowsBlock(block.view());

    try testing.expectEqualDeep(&[_][]const u8{ "FOO", "A", "=B", "ZIG_PROGRESS" }, map2.keys());
    try testing.expectEqualDeep(&[_][]const u8{ "BAR", "", "", "unchanged" }, map2.values());
}



---
File: /std/process/Preopens.zig
---

const Preopens = @This();

const builtin = @import("builtin");
const native_os = builtin.os.tag;

const std = @import("../std.zig");
const Io = std.Io;
const Allocator = std.mem.Allocator;

map: Map,

pub const empty: Preopens = switch (native_os) {
    .wasi => .{ .map = .empty },
    else => .{ .map = {} },
};

pub const Map = switch (native_os) {
    // Indexed by file descriptor number.
    .wasi => std.StringArrayHashMapUnmanaged(void),
    else => void,
};

pub const Resource = union(enum) {
    file: Io.File,
    dir: Io.Dir,
};

pub fn get(p: *const Preopens, name: []const u8) ?Resource {
    switch (native_os) {
        .wasi => {
            const index = p.map.getIndex(name) orelse return null;
            if (index <= 2) return .{ .file = .{
                .handle = @intCast(index),
                .flags = .{ .nonblocking = false },
            } };
            return .{ .dir = .{ .handle = @intCast(index) } };
        },
        else => {
            if (std.mem.eql(u8, name, "stdin")) return .{ .file = .stdin() };
            if (std.mem.eql(u8, name, "stdout")) return .{ .file = .stdout() };
            if (std.mem.eql(u8, name, "stderr")) return .{ .file = .stderr() };
            return null;
        },
    }
}

pub const InitError = Allocator.Error || error{Unexpected};

pub fn init(arena: Allocator) InitError!Preopens {
    if (native_os != .wasi) return .{ .map = {} };
    const wasi = std.os.wasi;
    var map: Map = .empty;

    try map.ensureUnusedCapacity(arena, 3);

    map.putAssumeCapacityNoClobber("stdin", {}); // 0
    map.putAssumeCapacityNoClobber("stdout", {}); // 1
    map.putAssumeCapacityNoClobber("stderr", {}); // 2
    while (true) {
        const fd: wasi.fd_t = @intCast(map.entries.len);
        var prestat: wasi.prestat_t = undefined;
        switch (wasi.fd_prestat_get(fd, &prestat)) {
            .SUCCESS => {},
            .OPNOTSUPP, .BADF => return .{ .map = map },
            else => return error.Unexpected,
        }
        try map.ensureUnusedCapacity(arena, 1);
        // This length does not include a null byte. Let's keep it this way to
        // gently encourage WASI implementations to behave properly.
        const name_len = prestat.u.dir.pr_name_len;
        const name = try arena.alloc(u8, name_len);
        switch (wasi.fd_prestat_dir_name(fd, name.ptr, name.len)) {
            .SUCCESS => {},
            else => return error.Unexpected,
        }
        map.putAssumeCapacityNoClobber(name, {});
    }
}



---
File: /std/Random/Ascon.zig
---

//! CSPRNG based on the Reverie construction, a permutation-based PRNG
//! with forward security, instantiated with the Ascon(128,12,8) permutation.
//!
//! Compared to ChaCha, this PRNG has a much smaller state, and can be
//! a better choice for constrained environments.
//!
//! References:
//! - A Robust and Sponge-Like PRNG with Improved Efficiency https://eprint.iacr.org/2016/886.pdf
//! - Ascon https://ascon.iaik.tugraz.at/files/asconv12-nist.pdf

const std = @import("std");
const mem = std.mem;
const Self = @This();

const Ascon = std.crypto.core.Ascon(.little);

state: Ascon,

const rate = 16;
pub const secret_seed_length = 32;

/// The seed must be uniform, secret and `secret_seed_length` bytes long.
pub fn init(secret_seed: [secret_seed_length]u8) Self {
    var self = Self{ .state = Ascon.initXof() };
    self.addEntropy(&secret_seed);
    return self;
}

/// Inserts entropy to refresh the internal state.
pub fn addEntropy(self: *Self, bytes: []const u8) void {
    comptime std.debug.assert(secret_seed_length % rate == 0);
    var i: usize = 0;
    while (i + rate < bytes.len) : (i += rate) {
        self.state.addBytes(bytes[i..][0..rate]);
        self.state.permuteR(8);
    }
    if (i != bytes.len) self.state.addBytes(bytes[i..]);
    self.state.permute();
}

/// Returns a `std.Random` structure backed by the current RNG.
pub fn random(self: *Self) std.Random {
    return std.Random.init(self, fill);
}

/// Fills the buffer with random bytes.
pub fn fill(self: *Self, buf: []u8) void {
    var i: usize = 0;
    while (true) {
        const left = buf.len - i;
        const n = @min(left, rate);
        self.state.extractBytes(buf[i..][0..n]);
        if (left == 0) break;
        self.state.permuteR(8);
        i += n;
    }
    self.state.permuteRatchet(6, rate);
}



---
File: /std/Random/benchmark.zig
---

// zig run -O ReleaseFast --zig-lib-dir ../.. benchmark.zig

const builtin = @import("builtin");

const std = @import("std");
const Io = std.Io;
const time = std.time;
const Random = std.Random;

const KiB = 1024;
const MiB = 1024 * KiB;
const GiB = 1024 * MiB;

const Rng = struct {
    ty: type,
    name: []const u8,
    init_u8s: ?[]const u8 = null,
    init_u64: ?u64 = null,
};

const prngs = [_]Rng{
    Rng{
        .ty = Random.Isaac64,
        .name = "isaac64",
        .init_u64 = 0,
    },
    Rng{
        .ty = Random.Pcg,
        .name = "pcg",
        .init_u64 = 0,
    },
    Rng{
        .ty = Random.RomuTrio,
        .name = "romutrio",
        .init_u64 = 0,
    },
    Rng{
        .ty = Random.Sfc64,
        .name = "sfc64",
        .init_u64 = 0,
    },
    Rng{
        .ty = Random.Xoroshiro128,
        .name = "xoroshiro128",
        .init_u64 = 0,
    },
    Rng{
        .ty = Random.Xoshiro256,
        .name = "xoshiro256",
        .init_u64 = 0,
    },
};

const csprngs = [_]Rng{
    Rng{
        .ty = Random.Ascon,
        .name = "ascon",
        .init_u8s = &[_]u8{0} ** 32,
    },
    Rng{
        .ty = Random.ChaCha,
        .name = "chacha",
        .init_u8s = &[_]u8{0} ** 32,
    },
};

const Result = struct {
    throughput: u64,
};

const long_block_size: usize = 8 * 8192;
const short_block_size: usize = 8;

pub fn benchTime(io: Io) i96 {
    return Io.Clock.awake.now(io).nanoseconds;
}

pub fn benchmark(comptime H: anytype, io: Io, bytes: usize, comptime block_size: usize) !Result {
    var rng = blk: {
        if (H.init_u8s) |init| {
            break :blk H.ty.init(init[0..].*);
        }
        if (H.init_u64) |init| {
            break :blk H.ty.init(init);
        }
        break :blk H.ty.init();
    };

    var block: [block_size]u8 = undefined;

    var offset: usize = 0;
    const start = benchTime(io);
    while (offset < bytes) : (offset += block.len) {
        rng.fill(block[0..]);
    }
    const end = benchTime(io);

    const elapsed_s = @as(f64, @floatFromInt(end - start)) / time.ns_per_s;
    const throughput = @as(u64, @intFromFloat(@as(f64, @floatFromInt(bytes)) / elapsed_s));

    std.debug.assert(rng.random().int(u64) != 0);

    return Result{
        .throughput = throughput,
    };
}

fn usage() void {
    std.debug.print(
        \\throughput_test [options]
        \\
        \\Options:
        \\  --filter    [test-name]
        \\  --count     [int]
        \\  --prngs-only
        \\  --csprngs-only
        \\  --short-only
        \\  --long-only
        \\  --help
        \\
    , .{});
}

fn mode(comptime x: comptime_int) comptime_int {
    return if (builtin.mode == .Debug) x / 64 else x;
}

pub fn main(init: std.process.Init) !void {
    const io = init.io;
    const arena = init.arena.allocator();

    var stdout_buffer: [0x100]u8 = undefined;
    var stdout_writer = Io.File.stdout().writer(io, &stdout_buffer);
    const stdout = &stdout_writer.interface;

    const args = try init.minimal.args.toSlice(arena);

    var filter: ?[]const u8 = null;
    var count: usize = mode(128 * MiB);
    var bench_prngs = true;
    var bench_csprngs = true;
    var bench_long = true;
    var bench_short = true;

    var i: usize = 1;
    while (i < args.len) : (i += 1) {
        if (std.mem.eql(u8, args[i], "--mode")) {
            try stdout.print("{}\n", .{builtin.mode});
            try stdout.flush();
            return;
        } else if (std.mem.eql(u8, args[i], "--filter")) {
            i += 1;
            if (i == args.len) {
                usage();
                std.process.exit(1);
            }

            filter = args[i];
        } else if (std.mem.eql(u8, args[i], "--count")) {
            i += 1;
            if (i == args.len) {
                usage();
                std.process.exit(1);
            }

            const c = try std.fmt.parseUnsigned(usize, args[i], 10);
            count = c * MiB;
        } else if (std.mem.eql(u8, args[i], "--csprngs-only")) {
            bench_prngs = false;
        } else if (std.mem.eql(u8, args[i], "--prngs-only")) {
            bench_csprngs = false;
        } else if (std.mem.eql(u8, args[i], "--short-only")) {
            bench_long = false;
        } else if (std.mem.eql(u8, args[i], "--long-only")) {
            bench_short = false;
        } else if (std.mem.eql(u8, args[i], "--help")) {
            usage();
            return;
        } else {
            usage();
            std.process.exit(1);
        }
    }

    if (bench_prngs) {
        if (bench_long) {
            inline for (prngs) |R| {
                if (filter == null or std.mem.find(u8, R.name, filter.?) != null) {
                    try stdout.print("{s} (long outputs)\n", .{R.name});
                    try stdout.flush();

                    const result_long = try benchmark(R, io, count, long_block_size);
                    try stdout.print("    {:5} MiB/s\n", .{result_long.throughput / (1 * MiB)});
                }
            }
        }
        if (bench_short) {
            inline for (prngs) |R| {
                if (filter == null or std.mem.find(u8, R.name, filter.?) != null) {
                    try stdout.print("{s} (short outputs)\n", .{R.name});
                    try stdout.flush();

                    const result_short = try benchmark(R, io, count, short_block_size);
                    try stdout.print("    {:5} MiB/s\n", .{result_short.throughput / (1 * MiB)});
                }
            }
        }
    }
    if (bench_csprngs) {
        if (bench_long) {
            inline for (csprngs) |R| {
                if (filter == null or std.mem.find(u8, R.name, filter.?) != null) {
                    try stdout.print("{s} (cryptographic, long outputs)\n", .{R.name});
                    try stdout.flush();

                    const result_long = try benchmark(R, io, count, long_block_size);
                    try stdout.print("    {:5} MiB/s\n", .{result_long.throughput / (1 * MiB)});
                }
            }
        }
        if (bench_short) {
            inline for (csprngs) |R| {
                if (filter == null or std.mem.find(u8, R.name, filter.?) != null) {
                    try stdout.print("{s} (cryptographic, short outputs)\n"
```
