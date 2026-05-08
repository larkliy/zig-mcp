```
ol = false,
    /// The right to invoke path_filestat_set_times.
    PATH_FILESTAT_SET_TIMES: bool = false,
    /// The right to invoke fd_filestat_get.
    FD_FILESTAT_GET: bool = false,
    /// The right to invoke fd_filestat_set_size.
    FD_FILESTAT_SET_SIZE: bool = false,
    /// The right to invoke fd_filestat_set_times.
    FD_FILESTAT_SET_TIMES: bool = false,
    /// The right to invoke path_symlink.
    PATH_SYMLINK: bool = false,
    /// The right to invoke path_remove_directory.
    PATH_REMOVE_DIRECTORY: bool = false,
    /// The right to invoke path_unlink_file.
    PATH_UNLINK_FILE: bool = false,
    /// If FD_READ is set, includes the right to invoke poll_oneoff to subscribe to
    /// eventtype_t.FD_READ. If FD_WRITE is set, includes the right to invoke poll_oneoff to
    /// subscribe to eventtype_t.FD_WRITE.
    POLL_FD_READWRITE: bool = false,
    /// The right to invoke sock_shutdown.
    SOCK_SHUTDOWN: bool = false,
    /// The right to invoke sock_accept.
    SOCK_ACCEPT: bool = false,
    _: u34 = 0,
};

pub const sdflags_t = packed struct(u8) {
    RD: bool = false,
    WR: bool = false,
    _: u6 = 0,
};

pub const siflags_t = u16;

pub const signal_t = enum(u8) {
    NONE = 0,
    HUP = 1,
    INT = 2,
    QUIT = 3,
    ILL = 4,
    TRAP = 5,
    ABRT = 6,
    BUS = 7,
    FPE = 8,
    KILL = 9,
    USR1 = 10,
    SEGV = 11,
    USR2 = 12,
    PIPE = 13,
    ALRM = 14,
    TERM = 15,
    CHLD = 16,
    CONT = 17,
    STOP = 18,
    TSTP = 19,
    TTIN = 20,
    TTOU = 21,
    URG = 22,
    XCPU = 23,
    XFSZ = 24,
    VTALRM = 25,
    PROF = 26,
    WINCH = 27,
    POLL = 28,
    PWR = 29,
    SYS = 30,
};

pub const subclockflags_t = u16;
pub const SUBSCRIPTION_CLOCK_ABSTIME: subclockflags_t = 0x0001;

pub const subscription_t = extern struct {
    userdata: userdata_t,
    u: subscription_u_t,
};

pub const subscription_clock_t = extern struct {
    id: clockid_t,
    timeout: timestamp_t,
    precision: timestamp_t,
    flags: subclockflags_t,
};

pub const subscription_fd_readwrite_t = extern struct {
    fd: fd_t,
};

pub const subscription_u_t = extern struct {
    tag: eventtype_t,
    u: subscription_u_u_t,
};

pub const subscription_u_u_t = extern union {
    clock: subscription_clock_t,
    fd_read: subscription_fd_readwrite_t,
    fd_write: subscription_fd_readwrite_t,
};

/// Nanoseconds.
pub const timestamp_t = u64;

pub const userdata_t = u64;

pub const whence_t = enum(u8) { SET, CUR, END };



---
File: /std/os/windows.zig
---

//! This file contains thin wrappers around Windows-specific APIs, with these
//! specific goals in mind:
//! * Convert "errno"-style error codes into Zig errors.
//! * When null-terminated or WTF16LE byte buffers are required, provide APIs which accept
//!   slices as well as APIs which accept null-terminated WTF16LE byte buffers.

const builtin = @import("builtin");
const native_arch = builtin.cpu.arch;

const std = @import("../std.zig");
const Io = std.Io;
const mem = std.mem;
const assert = std.debug.assert;
const math = std.math;
const maxInt = std.math.maxInt;
const UnexpectedError = std.posix.UnexpectedError;

pub const kernel32 = @import("windows/kernel32.zig");
pub const ntdll = @import("windows/ntdll.zig");
pub const ws2_32 = @import("windows/ws2_32.zig");
pub const crypt32 = @import("windows/crypt32.zig");
pub const nls = @import("windows/nls.zig");

pub const current_process: HANDLE = @ptrFromInt(@as(usize, @bitCast(@as(isize, -1))));

pub const PS = struct {
    pub const ATTRIBUTE = extern struct {
        Attribute: Type,
        Size: SIZE_T,
        u: extern union {
            Value: ULONG_PTR,
            ValuePtr: PVOID,
        },
        ReturnLength: ?*SIZE_T,

        /// https://ntdoc.m417z.com/ps_attribute_num
        /// Tag type is `u16` based on PS_ATTRIBUTE_NUMBER_MASK being 0xFFFF
        pub const NUM = enum(u16) {
            ParentProcess = 0,
            DebugObject,
            Token,
            ClientId,
            TebAddress,
            ImageName,
            ImageInfo,
            MemoryReserve,
            PriorityClass,
            ErrorMode,
            StdHandleInfo,
            HandleList,
            GroupAffinity,
            PreferredNode,
            IdealProcessor,
            UmsThread,
            MitigationOptions,
            ProtectionLevel,
            SecureProcess,
            JobList,
            ChildProcessPolicy,
            AllApplicationPackagesPolicy,
            Win32kFilter,
            SafeOpenPromptOriginClaim,
            BnoIsolation,
            DesktopAppPolicy,
            Chpe,
            MitigationAuditOptions,
            MachineType,
            ComponentFilter,
            EnableOptionalXStateFeatures,
            SupportedMachines,
            SveVectorLength,
        };

        /// https://ntdoc.m417z.com/psattributevalue
        pub const Type = enum(ULONG_PTR) {
            TEB_ADDRESS = construct(.TebAddress, true, false, false),
            _,

            pub fn construct(num: NUM, thread: bool, input: bool, additive: bool) ULONG_PTR {
                var val: ULONG_PTR = @intFromEnum(num);
                if (thread) val |= 0x10000;
                if (input) val |= 0x20000;
                if (additive) val |= 0x40000;
                return val;
            }
        };

        pub const LIST = extern struct {
            TotalLength: SIZE_T,
            Attributes: [1]ATTRIBUTE,
        };
    };
};

pub const OBJECT = struct {
    // ref: um/winternl.h

    pub const ATTRIBUTES = extern struct {
        Length: ULONG = @sizeOf(ATTRIBUTES),
        RootDirectory: ?HANDLE = null,
        ObjectName: ?*UNICODE_STRING = @constCast(&UNICODE_STRING.empty),
        Attributes: Flags = .{},
        SecurityDescriptor: ?*anyopaque = null,
        SecurityQualityOfService: ?*anyopaque = null,

        // Valid values for the Attributes field
        pub const Flags = packed struct(ULONG) {
            Reserved0: u1 = 0,
            INHERIT: bool = false,
            Reserved2: u2 = 0,
            PERMANENT: bool = false,
            EXCLUSIVE: bool = false,
            /// If name-lookup code should ignore the case of the ObjectName member rather than performing an exact-match search.
            CASE_INSENSITIVE: bool = true,
            OPENIF: bool = false,
            OPENLINK: bool = false,
            KERNEL_HANDLE: bool = false,
            FORCE_ACCESS_CHECK: bool = false,
            IGNORE_IMPERSONATED_DEVICEMAP: bool = false,
            DONT_REPARSE: bool = false,
            Reserved13: u19 = 0,

            pub const VALID_ATTRIBUTES: ATTRIBUTES = .{
                .INHERIT = true,
                .PERMANENT = true,
                .EXCLUSIVE = true,
                .CASE_INSENSITIVE = true,
                .OPENIF = true,
                .OPENLINK = true,
                .KERNEL_HANDLE = true,
                .FORCE_ACCESS_CHECK = true,
                .IGNORE_IMPERSONATED_DEVICEMAP = true,
                .DONT_REPARSE = true,
            };
        };
    };

    pub const INFORMATION_CLASS = enum(c_int) {
        Basic = 0,
        Name = 1,
        Type = 2,
        Types = 3,
        HandleFlag = 4,
        Session = 5,
        _,

        pub const Max: @typeInfo(@This()).@"enum".tag_type = @typeInfo(@This()).@"enum".fields.len;
    };

    pub const NAME_INFORMATION = extern struct {
        Name: UNICODE_STRING,
    };
};

pub const FILE = struct {
    // ref: km/ntddk.h

    pub const END_OF_FILE_INFORMATION = extern struct {
        EndOfFile: LARGE_INTEGER,
    };

    pub const ALIGNMENT_INFORMATION = extern struct {
        AlignmentRequirement: ULONG,
    };

    pub const NAME_INFORMATION = extern struct {
        FileNameLength: ULONG,
        FileName: [1]WCHAR,
    };

    pub const DISPOSITION = packed struct(ULONG) {
        DELETE: bool = false,
        POSIX_SEMANTICS: bool = false,
        FORCE_IMAGE_SECTION_CHECK: bool = false,
        ON_CLOSE: bool = false,
        IGNORE_READONLY_ATTRIBUTE: bool = false,
        Reserved5: u27 = 0,

        pub const DO_NOT_DELETE: DISPOSITION = .{};

        pub const INFORMATION = extern struct {
            DeleteFile: BOOLEAN,

            pub const EX = extern struct {
                Flags: DISPOSITION,
            };
        };
    };

    pub const FS_VOLUME_INFORMATION = extern struct {
        VolumeCreationTime: LARGE_INTEGER,
        VolumeSerialNumber: ULONG,
        VolumeLabelLength: ULONG,
        SupportsObjects: BOOLEAN,
        VolumeLabel: [0]WCHAR,

        pub fn getVolumeLabel(fvi: *const FS_VOLUME_INFORMATION) []const WCHAR {
            return (&fvi).ptr[0..@divExact(fvi.VolumeLabelLength, @sizeOf(WCHAR))];
        }
    };

    // ref: km/ntifs.h

    pub const NAME_FLAGS = packed struct(UCHAR) {
        NTFS: bool = false,
        DOS: bool = false,
        Reserved2: u5 = 0,
        UNSPECIFIED: bool = false,
    };

    pub const NOTIFY = struct {
        pub const CHANGE = packed struct(ULONG) {
            FILE_NAME: bool = false,
            DIR_NAME: bool = false,
            ATTRIBUTES: bool = false,
            SIZE: bool = false,
            LAST_WRITE: bool = false,
            LAST_ACCESS: bool = false,
            CREATION: bool = false,
            EA: bool = false,
            SECURITY: bool = false,
            STREAM_NAME: bool = false,
            STREAM_SIZE: bool = false,
            STREAM_WRITE: bool = false,
            Reserved12: u20 = 0,
        };

        pub const INFORMATION = extern struct {
            NextEntryOffset: ULONG,
            Action: ULONG,
            FileNameLength: ULONG,
            FileName: [0]WCHAR,

            pub fn fileName(info: *INFORMATION) []WCHAR {
                const ptr: [*]WCHAR = @ptrCast(&info.FileName);
                return ptr[0..@divExact(info.FileNameLength, @sizeOf(WCHAR))];
            }
        };

        pub const EXTENDED_INFORMATION = extern struct {
            NextEntryOffset: ULONG,
            Action: ULONG,
            CreationTime: LARGE_INTEGER,
            LastModificationTime: LARGE_INTEGER,
            LastChangeTime: LARGE_INTEGER,
            LastAccessTime: LARGE_INTEGER,
            AllocatedLength: LARGE_INTEGER,
            FileSize: LARGE_INTEGER,
            FileAttributes: ATTRIBUTE,
            u: extern union {
                ReparsePointTag: ULONG,
                EaSize: ULONG,
            },
            FileId: LARGE_INTEGER,
            ParentFileId: LARGE_INTEGER,
            FileNameLength: ULONG,
            FileName: [0]WCHAR,

            pub fn fileName(info: *INFORMATION) []WCHAR {
                const ptr: [*]WCHAR = @ptrCast(&info.FileName);
                return ptr[0..@divExact(info.FileNameLength, @sizeOf(WCHAR))];
            }
        };

        pub const FULL_INFORMATION = extern struct {
            NextEntryOffset: ULONG,
            Action: ULONG,
            CreationTime: LARGE_INTEGER,
            LastModificationTime: LARGE_INTEGER,
            LastChangeTime: LARGE_INTEGER,
            LastAccessTime: LARGE_INTEGER,
            AllocatedLength: LARGE_INTEGER,
            FileSize: LARGE_INTEGER,
            FileAttributes: ATTRIBUTE,
            u: extern union {
                ReparsePointTag: ULONG,
                EaSize: ULONG,
            },
            FileId: LARGE_INTEGER,
            ParentFileId: LARGE_INTEGER,
            FileNameLength: ULONG,
            FileNameFlags: NAME_FLAGS,
            FileName: [0]WCHAR,

            pub fn fileName(info: *INFORMATION) []WCHAR {
                const ptr: [*]WCHAR = @ptrCast(&info.FileName);
                return ptr[0..@divExact(info.FileNameLength, @sizeOf(WCHAR))];
            }
        };
    };

    pub const PIPE = struct {
        /// Define the `NamedPipeType` flags for `NtCreateNamedPipeFile`
        pub const TYPE = packed struct(ULONG) {
            TYPE: enum(u1) {
                BYTE_STREAM = 0b0,
                MESSAGE = 0b1,
            } = .BYTE_STREAM,
            REMOTE_CLIENTS: enum(u1) {
                ACCEPT = 0b0,
                REJECT = 0b1,
            } = .ACCEPT,
            Reserved2: u30 = 0,

            pub const VALID_MASK: TYPE = .{
                .TYPE = .MESSAGE,
                .REMOTE_CLIENTS = .REJECT,
            };
        };

        /// Define the `CompletionMode` flags for `NtCreateNamedPipeFile`
        pub const COMPLETION_MODE = packed struct(ULONG) {
            OPERATION: enum(u1) {
                QUEUE = 0b0,
                COMPLETE = 0b1,
            } = .QUEUE,
            Reserved1: u31 = 0,
        };

        /// Define the `ReadMode` flags for `NtCreateNamedPipeFile`
        pub const READ_MODE = packed struct(ULONG) {
            MODE: enum(u1) {
                BYTE_STREAM = 0b0,
                MESSAGE = 0b1,
            },
            Reserved1: u31 = 0,
        };

        /// Define the `NamedPipeConfiguration` flags for `NtQueryInformationFile`
        pub const CONFIGURATION = enum(ULONG) {
            INBOUND = 0x00000000,
            OUTBOUND = 0x00000001,
            FULL_DUPLEX = 0x00000002,
        };

        /// Define the `NamedPipeState` flags for `NtQueryInformationFile`
        pub const STATE = enum(ULONG) {
            DISCONNECTED = 0x00000001,
            LISTENING = 0x00000002,
            CONNECTED = 0x00000003,
            CLOSING = 0x00000004,
        };

        /// Define the `NamedPipeEnd` flags for `NtQueryInformationFile`
        pub const END = enum(ULONG) {
            CLIENT = 0x00000000,
            SERVER = 0x00000001,
        };

        pub const INFORMATION = extern struct {
            ReadMode: READ_MODE,
            CompletionMode: COMPLETION_MODE,
        };

        pub const LOCAL_INFORMATION = extern struct {
            NamedPipeType: TYPE,
            NamedPipeConfiguration: CONFIGURATION,
            MaximumInstances: ULONG,
            CurrentInstances: ULONG,
            InboundQuota: ULONG,
            ReadDataAvailable: ULONG,
            OutboundQuota: ULONG,
            WriteQuotaAvailable: ULONG,
            NamedPipeState: STATE,
            NamedPipeEnd: END,
        };

        pub const REMOTE_INFORMATION = extern struct {
            CollectDataTime: LARGE_INTEGER,
            MaximumCollectionCount: ULONG,
        };

        pub const WAIT_FOR_BUFFER = extern struct {
            Timeout: LARGE_INTEGER,
            NameLength: ULONG,
            TimeoutSpecified: BOOLEAN,
            Name: [PATH_MAX_WIDE]WCHAR,

            pub const WAIT_FOREVER: LARGE_INTEGER = std.math.minInt(LARGE_INTEGER);

            pub fn init(opts: struct {
                Timeout: ?LARGE_INTEGER = null,
                Name: []const WCHAR,
            }) WAIT_FOR_BUFFER {
                var fpwfb: WAIT_FOR_BUFFER = .{
                    .Timeout = opts.Timeout orelse undefined,
                    .NameLength = @intCast(@sizeOf(WCHAR) * opts.Name.len),
                    .TimeoutSpecified = @intFromBool(opts.Timeout != null),
                    .Name = undefined,
                };
                @memcpy(fpwfb.Name[0..opts.Name.len], opts.Name);
                return fpwfb;
            }

            pub fn getName(fpwfb: *const WAIT_FOR_BUFFER) []const WCHAR {
                return fpwfb.Name[0..@divExact(fpwfb.NameLength, @sizeOf(WCHAR))];
            }

            pub fn toBuffer(fpwfb: *const WAIT_FOR_BUFFER) []const u8 {
                const start: [*]const u8 = @ptrCast(fpwfb);
                return start[0 .. @offsetOf(WAIT_FOR_BUFFER, "Name") + fpwfb.NameLength];
            }
        };
    };

    pub const ALL_INFORMATION = extern struct {
        BasicInformation: BASIC_INFORMATION,
        StandardInformation: STANDARD_INFORMATION,
        InternalInformation: INTERNAL_INFORMATION,
        EaInformation: EA_INFORMATION,
        AccessInformation: ACCESS_INFORMATION,
        PositionInformation: POSITION_INFORMATION,
        ModeInformation: MODE.INFORMATION,
        AlignmentInformation: ALIGNMENT_INFORMATION,
        NameInformation: NAME_INFORMATION,
    };

    pub const INTERNAL_INFORMATION = extern struct {
        IndexNumber: LARGE_INTEGER,
    };

    pub const EA_INFORMATION = extern struct {
        EaSize: ULONG,
    };

    pub const ACCESS_INFORMATION = extern struct {
        AccessFlags: ACCESS_MASK,
    };

    /// This is not separated into RENAME_INFORMATION and RENAME_INFORMATION_EX because
    /// the only difference is the `Flags` type (BOOLEAN before _EX, ULONG in the _EX),
    /// which doesn't affect the struct layout--the offset of RootDirectory is the same
    /// regardless.
    pub const RENAME_INFORMATION = extern struct {
        Flags: FLAGS,
        RootDirectory: ?HANDLE,
        FileNameLength: ULONG,
        FileName: [PATH_MAX_WIDE]WCHAR,

        pub fn init(opts: struct {
            Flags: FLAGS = .{},
            RootDirectory: ?HANDLE = null,
            FileName: []const WCHAR,
        }) RENAME_INFORMATION {
            var fri: RENAME_INFORMATION = .{
                .Flags = opts.Flags,
                .RootDirectory = opts.RootDirectory,
                .FileNameLength = @intCast(@sizeOf(WCHAR) * opts.FileName.len),
                .FileName = undefined,
            };
            @memcpy(fri.FileName[0..opts.FileName.len], opts.FileName);
            return fri;
        }

        pub const FLAGS = packed struct(ULONG) {
            REPLACE_IF_EXISTS: bool = false,
            POSIX_SEMANTICS: bool = false,
            SUPPRESS_PIN_STATE_INHERITANCE: bool = false,
            SUPPRESS_STORAGE_RESERVE_INHERITANCE: bool = false,
            AVAILABLE_SPACE: enum(u2) {
                NO_PRESERVE = 0b00,
                NO_INCREASE = 0b01,
                NO_DECREASE = 0b10,
                PRESERVE = 0b11,
            } = .NO_PRESERVE,
            IGNORE_READONLY_ATTRIBUTE: bool = false,
            RESIZE_SR: enum(u2) {
                NO_FORCE = 0b00,
                FORCE_TARGET = 0b01,
                FORCE_SOURCE = 0b10,
                FORCE = 0b11,
            } = .NO_FORCE,
            Reserved9: u23 = 0,
        };

        pub fn getFileName(ri: *const RENAME_INFORMATION) []const WCHAR {
            return ri.FileName[0..@divExact(ri.FileNameLength, @sizeOf(WCHAR))];
        }

        pub fn toBuffer(fri: *RENAME_INFORMATION) []u8 {
            const start: [*]u8 = @ptrCast(fri);
            // The ABI size of the documented struct is 24 bytes, and attempting to use any size
            // less than that will trigger INFO_LENGTH_MISMATCH, so enforce a minimum in cases where,
            // for example, FileNameLength is 1 so only 22 bytes are technically needed.
            const size = @max(24, @offsetOf(RENAME_INFORMATION, "FileName") + fri.FileNameLength);
            return start[0..size];
        }
    };

    // ref: km/wdm.h

    pub const INFORMATION_CLASS = enum(c_int) {
        Directory = 1,
        FullDirectory = 2,
        BothDirectory = 3,
        Basic = 4,
        Standard = 5,
        Internal = 6,
        Ea = 7,
        Access = 8,
        Name = 9,
        Rename = 10,
        Link = 11,
        Names = 12,
        Disposition = 13,
        Position = 14,
        FullEa = 15,
        Mode = 16,
        Alignment = 17,
        All = 18,
        Allocation = 19,
        EndOfFile = 20,
        AlternateName = 21,
        Stream = 22,
        Pipe = 23,
        PipeLocal = 24,
        PipeRemote = 25,
        MailslotQuery = 26,
        MailslotSet = 27,
        Compression = 28,
        ObjectId = 29,
        Completion = 30,
        MoveCluster = 31,
        Quota = 32,
        ReparsePoint = 33,
        NetworkOpen = 34,
        AttributeTag = 35,
        Tracking = 36,
        IdBothDirectory = 37,
        IdFullDirectory = 38,
        ValidDataLength = 39,
        ShortName = 40,
        IoCompletionNotification = 41,
        IoStatusBlockRange = 42,
        IoPriorityHint = 43,
        SfioReserve = 44,
        SfioVolume = 45,
        HardLink = 46,
        ProcessIdsUsingFile = 47,
        NormalizedName = 48,
        NetworkPhysicalName = 49,
        IdGlobalTxDirectory = 50,
        IsRemoteDevice = 51,
        Unused = 52,
        NumaNode = 53,
        StandardLink = 54,
        RemoteProtocol = 55,
        RenameBypassAccessCheck = 56,
        LinkBypassAccessCheck = 57,
        VolumeName = 58,
        Id = 59,
        IdExtdDirectory = 60,
        ReplaceCompletion = 61,
        HardLinkFullId = 62,
        IdExtdBothDirectory = 63,
        DispositionEx = 64,
        RenameEx = 65,
        RenameExBypassAccessCheck = 66,
        DesiredStorageClass = 67,
        Stat = 68,
        MemoryPartition = 69,
        StatLx = 70,
        CaseSensitive = 71,
        LinkEx = 72,
        LinkExBypassAccessCheck = 73,
        StorageReserveId = 74,
        CaseSensitiveForceAccessCheck = 75,
        KnownFolder = 76,
        StatBasic = 77,
        Id64ExtdDirectory = 78,
        Id64ExtdBothDirectory = 79,
        IdAllExtdDirectory = 80,
        IdAllExtdBothDirectory = 81,
        StreamReservation = 82,
        MupProvider = 83,
        _,

        pub const Maximum: @typeInfo(@This()).@"enum".tag_type = 1 + @typeInfo(@This()).@"enum".fields.len;
    };

    pub const BASIC_INFORMATION = extern struct {
        CreationTime: LARGE_INTEGER,
        LastAccessTime: LARGE_INTEGER,
        LastWriteTime: LARGE_INTEGER,
        ChangeTime: LARGE_INTEGER,
        FileAttributes: ATTRIBUTE,
    };

    pub const STANDARD_INFORMATION = extern struct {
        AllocationSize: LARGE_INTEGER,
        EndOfFile: LARGE_INTEGER,
        NumberOfLinks: ULONG,
        DeletePending: BOOLEAN,
        Directory: BOOLEAN,
    };

    pub const POSITION_INFORMATION = extern struct {
        CurrentByteOffset: LARGE_INTEGER,
    };

    pub const FULL_EA_INFORMATION = extern struct {
        NextEntryOffset: ULONG,
        Flags: UCHAR,
        EaNameLength: UCHAR,
        EaValueLength: USHORT,
        EaName: [0]CHAR,
    };

    pub const FS_DEVICE_INFORMATION = extern struct {
        DeviceType: DEVICE_TYPE,
        Characteristics: ULONG,
    };

    pub const USE_FILE_POINTER_POSITION = -2;

    // ref: um/WinBase.h

    pub const ATTRIBUTE_TAG_INFO = extern struct {
        FileAttributes: DWORD,
        ReparseTag: IO_REPARSE_TAG,
    };

    // ref: um/winnt.h

    pub const SHARE = packed struct(ULONG) {
        /// The file can be opened for read access by other threads.
        READ: bool = false,
        /// The file can be opened for write access by other threads.
        WRITE: bool = false,
        /// The file can be opened for delete access by other threads.
        DELETE: bool = false,
        Reserved3: u29 = 0,

        pub const VALID_FLAGS: SHARE = .{
            .READ = true,
            .WRITE = true,
            .DELETE = true,
        };
    };

    pub const ATTRIBUTE = packed struct(ULONG) {
        /// The file is read only. Applications can read the file, but cannot write to or delete it.
        READONLY: bool = false,
        /// The file is hidden. Do not include it in an ordinary directory listing.
        HIDDEN: bool = false,
        /// The file is part of or used exclusively by an operating system.
        SYSTEM: bool = false,
        Reserved3: u1 = 0,
        DIRECTORY: bool = false,
        /// The file should be archived. Applications use this attribute to mark files for backup or removal.
        ARCHIVE: bool = false,
        DEVICE: bool = false,
        /// The file does not have other attributes set. This attribute is valid only if used alone.
        NORMAL: bool = false,
        /// The file is being used for temporary storage.
        TEMPORARY: bool = false,
        SPARSE_FILE: bool = false,
        REPARSE_POINT: bool = false,
        COMPRESSED: bool = false,
        /// The data of a file is not immediately available. This attribute indicates that file data is physically moved to offline storage.
        /// This attribute is used by Remote Storage, the hierarchical storage management software. Applications should not arbitrarily change this attribute.
        OFFLINE: bool = false,
        NOT_CONTENT_INDEXED: bool = false,
        /// The file or directory is encrypted. For a file, this means that all data in the file is encrypted. For a directory, this means that encryption is
        /// the default for newly created files and subdirectories. For more information, see File Encryption.
        ///
        /// This flag has no effect if `SYSTEM` is also specified.
        ///
        /// This flag is not supported on Home, Home Premium, Starter, or ARM editions of Windows.
        ENCRYPTED: bool = false,
        INTEGRITY_STREAM: bool = false,
        VIRTUAL: bool = false,
        NO_SCRUB_DATA: bool = false,
        EA_or_RECALL_ON_OPEN: bool = false,
        PINNED: bool = false,
        UNPINNED: bool = false,
        Reserved21: u1 = 0,
        RECALL_ON_DATA_ACCESS: bool = false,
        Reserved23: u6 = 0,
        STRICTLY_SEQUENTIAL: bool = false,
        Reserved30: u2 = 0,
    };

    // ref: um/winternl.h

    /// Define the create disposition values
    pub const CREATE_DISPOSITION = enum(ULONG) {
        /// If the file already exists, replace it with the given file. If it does not, create the given file.
        SUPERSEDE = 0x00000000,
        /// If the file already exists, open it instead of creating a new file.
        /// If it does not, fail the request and do not create a new file.
        OPEN = 0x00000001,
        /// If the file already exists, fail the request and do not create or
        /// open the given file. If it does not, create the given file.
        CREATE = 0x00000002,
        /// If the file already exists, open it. If it does not, create the given file.
        OPEN_IF = 0x00000003,
        /// If the file already exists, open it and overwrite it. If it does not, fail the request.
        OVERWRITE = 0x00000004,
        /// If the file already exists, open it and overwrite it. If it does not, create the given file.
        OVERWRITE_IF = 0x00000005,

        pub const MAXIMUM_DISPOSITION: CREATE_DISPOSITION = .OVERWRITE_IF;
    };

    /// Define the create/open option flags
    pub const MODE = packed struct(ULONG) {
        /// The file being created or opened is a directory file. With this
        /// flag, the CreateDisposition parameter must be set to `.CREATE`,
        /// `.FILE_OPEN`, or `.OPEN_IF`. With this flag, other compatible
        /// CreateOptions flags include only the following: `SYNCHRONOUS_IO`,
        /// `WRITE_THROUGH`, `OPEN_FOR_BACKUP_INTENT`, and `OPEN_BY_FILE_ID`.
        DIRECTORY_FILE: bool = false,
        /// Applications that write data to the file must actually transfer the
        /// data into the file before any requested write operation is
        /// considered complete. This flag is automatically set if the
        /// CreateOptions flag `NO_INTERMEDIATE_BUFFERING` is set.
        WRITE_THROUGH: bool = false,
        /// All accesses to the file are sequential.
        SEQUENTIAL_ONLY: bool = false,
        /// The file cannot be cached or buffered in a driver's internal
        /// buffers. This flag is incompatible with the DesiredAccess
        /// `FILE_APPEND_DATA` flag.
        NO_INTERMEDIATE_BUFFERING: bool = false,
        IO: enum(u2) {
            /// All operations on the file are performed asynchronously.
            ASYNCHRONOUS = 0b00,
            /// All operations on the file are performed synchronously. Any
            /// wait on behalf of the caller is subject to premature
            /// termination from alerts. This flag also causes the I/O system
            /// to maintain the file position context. If this flag is set, the
            /// DesiredAccess `SYNCHRONIZE` flag also must be set.
            SYNCHRONOUS_ALERT = 0b01,
            /// All operations on the file are performed synchronously. Waits
            /// in the system to synchronize I/O queuing and completion are not
            /// subject to alerts. This flag also causes the I/O system to
            /// maintain the file position context. If this flag is set, the
            /// DesiredAccess `SYNCHRONIZE` flag also must be set.
            SYNCHRONOUS_NONALERT = 0b10,
            _,

            pub const VALID_FLAGS: @This() = @enumFromInt(0b11);
        },
        /// The file being opened must not be a directory file or this call
        /// fails. The file object being opened can represent a data file, a
        /// logical, virtual, or physical device, or a volume.
        NON_DIRECTORY_FILE: bool = false,
        /// Create a tree connection for this file in order to open it over the
        /// network. This flag is not used by device and intermediate drivers.
        CREATE_TREE_CONNECTION: bool = false,
        /// Complete this operation immediately with an alternate success code
        /// of `STATUS_OPLOCK_BREAK_IN_PROGRESS` if the target file is
        /// oplocked, rather than blocking the caller's thread. If the file is
        /// oplocked, another caller already has access to the file. This flag
        /// is not used by device and intermediate drivers.
        COMPLETE_IF_OPLOCKED: bool = false,
        /// If the extended attributes on an existing file being opened
        /// indicate that the caller must understand EAs to properly interpret
        /// the file, fail this request because the caller does not understand
        /// how to deal with EAs. This flag is irrelevant for device and
        /// intermediate drivers.
        NO_EA_KNOWLEDGE: bool = false,
        OPEN_REMOTE_INSTANCE: bool = false,
        /// Accesses to the file can be random, so no sequential read-ahead
        /// operations should be performed on the file by FSDs or the system.
        RANDOM_ACCESS: bool = false,
        /// Delete the file when the last handle to it is passed to `NtClose`.
        /// If this flag is set, the `DELETE` flag must be set in the
        /// DesiredAccess parameter.
        DELETE_ON_CLOSE: bool = false,
        /// The file name that is specified by the `ObjectAttributes` parameter
        /// includes the 8-byte file reference number for the file. This number
        /// is assigned by and specific to the particular file system. If the
        /// file is a reparse point, the file name will also include the name
        /// of a device. Note that the FAT file system does not support this
        /// flag. This flag is not used by device and intermediate drivers.
        OPEN_BY_FILE_ID: bool = false,
        /// The file is being opened for backup intent. Therefore, the system
        /// should check for certain access rights and grant the caller the
        /// appropriate access to the file before checking the DesiredAccess
        /// parameter against the file's security descriptor. This flag not
        /// used by device and intermediate drivers.
        OPEN_FOR_BACKUP_INTENT: bool = false,
        /// Suppress inheritance of `FILE_ATTRIBUTE.COMPRESSED` from the parent
        /// directory. This allows creation of a non-compressed file in a
        /// directory that is marked compressed.
        NO_COMPRESSION: bool = false,
        /// The file is being opened and an opportunistic lock on the file is
        /// being requested as a single atomic operation. The file system
        /// checks for oplocks before it performs the create operation and will
        /// fail the create with a return code of STATUS_CANNOT_BREAK_OPLOCK if
        /// the result would be to break an existing oplock. For more
        /// information, see the Remarks section.
        ///
        /// Windows Server 2008, Windows Vista, Windows Server 2003 and Windows
        /// XP:  This flag is not supported.
        ///
        /// This flag is supported on the following file systems: NTFS, FAT,
        /// and exFAT.
        OPEN_REQUIRING_OPLOCK: bool = false,
        Reserved17: u3 = 0,
        /// This flag allows an application to request a filter opportunistic
        /// lock to prevent other applications from getting share violations.
        /// If there are already open handles, the create request will fail
        /// with STATUS_OPLOCK_NOT_GRANTED. For more information, see the
        /// Remarks section.
        RESERVE_OPFILTER: bool = false,
        /// Open a file with a reparse point and bypass normal reparse point
        /// processing for the file. For more information, see the Remarks
        /// section.
        OPEN_REPARSE_POINT: bool = false,
        /// Instructs any filters that perform offline storage or
        /// virtualization to not recall the contents of the file as a result
        /// of this open.
        OPEN_NO_RECALL: bool = false,
        /// This flag instructs the file system to capture the user associated
        /// with the calling thread. Any subsequent calls to
        /// `FltQueryVolumeInformation` or `ZwQueryVolumeInformationFile` using
        /// the returned handle will assume the captured user, rather than the
        /// calling user at the time, for purposes of computing the free space
        /// available to the caller. This applies to the following
        /// FsInformationClass values: `FileFsSizeInformation`,
        /// `FileFsFullSizeInformation`, and `FileFsFullSizeInformationEx`.
        OPEN_FOR_FREE_SPACE_QUERY: bool = false,
        Reserved24: u8 = 0,

        pub const VALID_OPTION_FLAGS: MODE = .{
            .DIRECTORY_FILE = true,
            .WRITE_THROUGH = true,
            .SEQUENTIAL_ONLY = true,
            .NO_INTERMEDIATE_BUFFERING = true,
            .IO = .VALID_FLAGS,
            .NON_DIRECTORY_FILE = true,
            .CREATE_TREE_CONNECTION = true,
            .COMPLETE_IF_OPLOCKED = true,
            .NO_EA_KNOWLEDGE = true,
            .OPEN_REMOTE_INSTANCE = true,
            .RANDOM_ACCESS = true,
            .DELETE_ON_CLOSE = true,
            .OPEN_BY_FILE_ID = true,
            .OPEN_FOR_BACKUP_INTENT = true,
            .NO_COMPRESSION = true,
            .OPEN_REQUIRING_OPLOCK = true,
            .Reserved17 = 0b111,
            .RESERVE_OPFILTER = true,
            .OPEN_REPARSE_POINT = true,
            .OPEN_NO_RECALL = true,
            .OPEN_FOR_FREE_SPACE_QUERY = true,
        };

        pub const VALID_PIPE_OPTION_FLAGS: MODE = .{
            .WRITE_THROUGH = true,
            .IO = .VALID_FLAGS,
        };

        pub const VALID_MAILSLOT_OPTION_FLAGS: MODE = .{
            .WRITE_THROUGH = true,
            .IO = .VALID_FLAGS,
        };

        pub const VALID_SET_OPTION_FLAGS: MODE = .{
            .WRITE_THROUGH = true,
            .SEQUENTIAL_ONLY = true,
            .IO = .VALID_FLAGS,
        };

        // ref: km/ntifs.h

        pub const INFORMATION = extern struct {
            /// The set of flags that specify the mode in which the file can be
            /// accessed. These flags are a subset of `MODE`.
            Mode: MODE,
        };
    };
};

pub const DIRECTORY = struct {
    pub const NOTIFY_INFORMATION_CLASS = enum(c_int) {
        Notify = 1,
        NotifyExtended = 2,
        NotifyFull = 3,
        _,

        pub const Maximum: @typeInfo(@This()).@"enum".tag_type = 1 + @typeInfo(@This()).@"enum".fields.len;
    };
};

pub const CONSOLE = struct {
    pub const USER_IO = struct {
        pub const INFO = struct {
            pub const CP = extern struct {
                /// GetCP: output
                /// SetCP: input
                CodePage: UINT,
                /// input
                Mode: MODE,

                pub const MODE = enum(BOOLEAN.Backing) {
                    Input,
                    Output,
                };
            };

            pub const WRITE = extern struct {
                /// output, in bytes
                Size: DWORD,
                /// input
                Mode: MODE,

                pub const MODE = enum(BOOLEAN.Backing) {
                    Character,
                    WideCharacter,
                };
            };

            pub const FILL = extern struct {
                /// input
                dwWriteCoord: COORD,
                /// input
                Tag: WITH.Tag,
                /// input
                With: WITH.Payload,
                /// input/output, in characters
                nLength: DWORD,

                pub const WITH = union(enum(DWORD)) {
                    Character: CHAR = 1,
                    WideCharacter: WCHAR = 2,
                    Attribute: WORD = 3,

                    pub const Tag = @typeInfo(WITH).@"union".tag_type.?;
                    pub const Payload = PAYLOAD: {
                        const with_fields = @typeInfo(WITH).@"union".fields;
                        var field_names: [with_fields.len][]const u8 = undefined;
                        var field_types: [with_fields.len]type = undefined;
                        for (with_fields, &field_names, &field_types) |field, *field_name, *field_type| {
                            field_name.* = field.name;
                            field_type.* = field.type;
                        }
                        break :PAYLOAD @Union(.@"extern", null, &field_names, &field_types, &@splat(.{}));
                    };
                };
            };

            /// all output
            pub const SCREEN_BUFFER = extern struct {
                dwSize: COORD,
                dwCursorPosition: COORD,
                dwWindowPosition: COORD,
                wAttributes: WORD,
                dwWindowSize: COORD,
                dwMaximumWindowSize: COORD,
                wPopupAttributes: WORD,
                bFullscreenSupported: BOOL,
                ColorTable: [16]COLORREF,
            };

            pub const READ_OUTPUT_CHARACTER = extern struct {
                /// input
                dwReadCoord: COORD,
                Mode: MODE,
                /// output, in characters
                nLength: DWORD,

                pub const MODE = enum(DWORD) {
                    Character = 1,
                    WideCharacter = 2,
                };
            };
        };

        pub fn GET_CP(mode: INFO.CP.MODE) Header.With(INFO.CP) {
            return .init(.GetCP, .{ .CodePage = undefined, .Mode = mode });
        }
        pub const GET_MODE: Header.With(DWORD) = .init(.GetMode, undefined);
        pub fn SET_MODE(mode: DWORD) Header.With(DWORD) {
            return .init(.SetMode, mode);
        }
        pub fn WRITE(mode: INFO.WRITE.MODE) Header.With(INFO.WRITE) {
            return .init(.Write, .{ .Size = undefined, .Mode = mode });
        }
        pub fn FILL(with: INFO.FILL.WITH, len: DWORD, coord: COORD) Header.With(INFO.FILL) {
            return .init(.Fill, .{
                .dwWriteCoord = coord,
                .Tag = with,
                .With = switch (with) {
                    inline else => |payload, tag| @unionInit(
                        INFO.FILL.WITH.Payload,
                        @tagName(tag),
                        payload,
                    ),
                },
                .nLength = len,
            });
        }
        pub fn SET_CP(mode: INFO.CP.MODE, cp: UINT) Header.With(INFO.CP) {
            return .init(.SetCP, .{ .CodePage = cp, .Mode = mode });
        }
        pub const GET_SCREEN_BUFFER_INFO: Header.With(INFO.SCREEN_BUFFER) =
            .init(.GetScreenBufferInfo, undefined);
        pub fn SET_CURSOR_POSITION(coord: COORD) Header.With(COORD) {
            return .init(.SetCursorPosition, coord);
        }
        pub fn SET_TEXT_ATTRIBUTE(attribute: WORD) Header.With(WORD) {
            return .init(.SetTextAttribute, attribute);
        }
        pub fn READ_OUTPUT_CHARACTER(
            coord: COORD,
            mode: INFO.READ_OUTPUT_CHARACTER.MODE,
        ) Header.With(INFO.READ_OUTPUT_CHARACTER) {
            return .init(.ReadOutputCharacter, .{
                .dwReadCoord = coord,
                .Mode = mode,
                .nLength = undefined,
            });
        }

        pub const InputBuffer = extern struct {
            Size: u32,
            Pointer: *const anyopaque,
        };

        pub const OutputBuffer = extern struct {
            Size: u32,
            Pointer: *anyopaque,
        };

        pub fn Request(comptime in_len: u32, comptime out_len: u32) type {
            return extern struct {
                Handle: ?HANDLE,
                InputBuffersLength: u32,
                OutputBuffersLength: u32,
                InputBuffers: [in_len]InputBuffer,
                OutputBuffers: [out_len]OutputBuffer,

                pub fn init(
                    handle: ?HANDLE,
                    in: [in_len]InputBuffer,
                    out: [out_len]OutputBuffer,
                ) @This() {
                    return .{
                        .Handle = handle,
                        .InputBuffersLength = in_len,
                        .OutputBuffersLength = out_len,
                        .InputBuffers = in,
                        .OutputBuffers = out,
                    };
                }
            };
        }

        pub const Header = extern struct {
            Operation: Operation,
            Size: u32,

            pub fn With(comptime Data: type) type {
                return extern struct {
                    Header: Header,
                    Data: Data,

                    pub fn init(operation: Operation, data: Data) @This() {
                        return .{
                            .Header = .{ .Operation = operation, .Size = @sizeOf(Data) },
                            .Data = data,
                        };
                    }

                    pub fn request(
                        with: *@This(),
                        file: ?Io.File,
                        comptime in_len: u32,
                        in: [in_len]InputBuffer,
                        comptime out_len: u32,
                        out: [out_len]OutputBuffer,
                    ) Request(1 + in_len, 1 + out_len) {
                        return .init(
                            if (file) |f| f.handle else null,
                            [1]InputBuffer{.{
                                .Size = @offsetOf(@This(), "Data") + @sizeOf(Data),
                                .Pointer = with,
                            }} ++ in,
                            [1]OutputBuffer{.{ .Size = @sizeOf(Data), .Pointer = &with.Data }} ++ out,
                        );
                    }

                    pub fn operate(with: *@This(), io: Io, file: ?Io.File) Io.Cancelable!NTSTATUS {
                        return (try io.operate(.{ .device_io_control = .{
                            .file = .{
                                .handle = peb().ProcessParameters.ConsoleHandle,
                                .flags = .{ .nonblocking = false },
                            },
                            .code = IOCTL.CONDRV.ISSUE_USER_IO,
                            .in = @ptrCast(&with.request(file, 0, .{}, 0, .{})),
                        } })).device_io_control.u.Status;
                    }
                };
            }
        };

        pub const Operation = enum(u32) {
            GetCP = 0x1000000,
            GetMode = 0x1000001,
            SetMode = 0x1000002,
            Read = 0x1000005,
            Write = 0x1000006,
            Fill = 0x2000000,
            SetCP = 0x2000004,
            GetScreenBufferInfo = 0x2000007,
            SetCursorPosition = 0x200000a,
            SetTextAttribute = 0x200000d,
            ReadOutputCharacter = 0x200000f,
            _,
        };
    };
};

pub const AFD = packed struct(ULONG) {
    NO_FAST_IO: bool = false,
    OVERLAPPED: bool = false,
    Reserved0: u30 = 0,

    pub const Mutability = enum { @"const", @"var" };
    pub fn WSABUF(comptime mutability: Mutability) type {
        return extern struct {
            len: ULONG,
            buf: switch (mutability) {
                .@"const" => [*]const u8,
                .@"var" => [*]u8,
            },
        };
    }
    pub const GUARANTEE = enum(c_int) {
        BestEffort,
        ControlledLoad,
        Predictive,
        GuaranteedDelay,
        Guaranteed,
        _,
    };
    pub const DEVICE_NAME: []const u16 = &.{ '\\', 'D', 'e', 'v', 'i', 'c', 'e', '\\', 'A', 'f', 'd' };
    pub const ENDPOINT_TYPE = packed struct(ULONG) {
        CONNECTIONLESS: bool = false,
        Reserved1: u3 = 0,
        MESSAGEMODE: bool = false,
        Reserved5: u3 = 0,
        RAW: bool = false,
        Reserved9: u22 = 0,
        REGISTERED_IO: bool = false,
    };
    pub const OPEN_PACKET = extern struct {
        EndpointType: ENDPOINT_TYPE,
        GroupID: LONG,
        AddressFamily: LONG,
        SocketType: LONG,
        Protocol: LONG,
        TransportDeviceNameLength: ULONG,
        TransportDeviceName: [1]WCHAR,

        pub const NAME = "AfdOpenPacketXX";

        pub const FULL_EA_INFORMATION = extern struct {
            Header: FILE.FULL_EA_INFORMATION = .{
                .NextEntryOffset = 0,
                .Flags = 0,
                .EaNameLength = NAME.len,
                .EaValueLength = @sizeOf(OPEN_PACKET),
                .EaName = .{},
            },
            Name: [NAME.len:0]u8 = NAME.*,
            Value: OPEN_PACKET,
        };
    };
    pub const BIND_INFO = extern struct {
        Mode: MODE,

        pub const MODE = enum(ULONG) {
            Unix = 0,
            Passive = 1,
            Active = 2,
            _,
        };
    };
    pub const LISTEN_INFO = extern struct {
        UseSAN: BOOLEAN,
        MaximumConnectionQueue: ULONG,
        UseDelayedAcceptance: BOOLEAN,
    };
    pub const LISTEN_RESPONSE_INFO = extern struct {
        Sequence: ULONG,
    };
    pub const ACCEPT_INFO = extern struct {
        UseSAN: BOOLEAN,
        Sequence: ULONG,
        AcceptHandle: HANDLE,
    };
    pub const SUPER_ACCEPT_INFO = extern struct {
        UseSAN: BOOLEAN,
        AcceptHandle: HANDLE,
        AcceptEndpoint: PVOID,
        AcceptFileObject: PVOID,
        ReceiveDataLength: ULONG,
        LocalAddressLength: ULONG,
        RemoteAddressLength: ULONG,
        ListenResponseInfo: LISTEN_RESPONSE_INFO,
    };
    pub const DEFER_ACCEPT_INFO = extern struct {
        Sequence: ULONG,
        Reject: BOOLEAN,
    };
    pub const PARTIAL_DISCONNECT_INFO = extern struct {
        DisconnectMode: MODE,
        Timeout: LARGE_INTEGER,

        pub const MODE = packed struct(ULONG) {
            SEND: bool = false,
            RECEIVE: bool = false,
            ABORTIVE: bool = false,
            UNCONNECT_DATAGRAM: bool = false,
            Reserved4: u28 = 0,
        };
    };
    pub const RECEIVE_INFORMATION = extern struct {
        BytesAvailable: ULONG,
        ExpeditedBytesAvailable: ULONG,
    };
    pub const HANDLE_INFO = extern struct {
        TdiAddressHandle: HANDLE,
        TdiConnectionHandle: HANDLE,
    };
    pub const INFORMATION = extern struct {
        InformationType: TYPE,
        Information: extern union {
            Boolean: BOOLEAN,
            Ulong: ULONG,
            LargeInteger: LARGE_INTEGER,
        },

        pub const TYPE = enum(ULONG) {
            INLINE_MODE = 0x01,
            NONBLOCKING_MODE = 0x02,
            MAX_SEND_SIZE = 0x03,
            SENDS_PENDING = 0x04,
            MAX_PATH_SEND_SIZE = 0x05,
            RECEIVE_WINDOW_SIZE = 0x06,
            SEND_WINDOW_SIZE = 0x07,
            CONNECT_TIME = 0x08,
            CIRCULAR_QUEUEING = 0x09,
            GROUP_ID_AND_TYPE = 0x0A,
            _,
        };
    };
    pub const TRANSMIT_FILE_INFO = extern struct {
        Offset: LARGE_INTEGER,
        WriteLength: LARGE_INTEGER,
        SendPacketLength: ULONG,
        FileHandle: HANDLE,
        Head: PVOID,
        HeadLength: ULONG,
        Tail: PVOID,
        TailLength: ULONG,
        Flags: FLAGS,

        pub const FLAGS = packed struct(ULONG) {
            DISCONNECT: bool = false,
            REUSE_SOCKET: bool = false,
            WRITE_BEHIND: bool = false,
            Reserved3: u25 = 0,
        };
    };
    pub const QUEUE_APC_INFO = extern struct {
        Thread: HANDLE,
        ApcRoutine: PVOID,
        ApcContext: PVOID,
        SystemArgument1: PVOID,
        SystemArgument2: PVOID,
    };
    pub const SEND_INFO = extern struct {
        BufferArray: [*]const WSABUF(.@"const"),
        BufferCount: ULONG,
        AfdFlags: AFD,
        TdiFlags: TDI.SEND,
    };
    pub const SEND_DATAGRAM_INFO = extern struct {
        BufferArray: [*]const WSABUF(.@"const"),
        BufferCount: ULONG,
        AfdFlags: AFD,
        TdiRequest: TDI.REQUEST.SEND_DATAGRAM,
        TdiConnInfo: TDI.CONNECTION.INFORMATION,
    };
    pub const RECV_INFO = extern struct {
        BufferArray: [*]const WSABUF(.@"var"),
        BufferCount: ULONG,
        AfdFlags: AFD,
        TdiFlags: TDI.RECEIVE,
    };
    pub const RECV_DATAGRAM_INFO = extern struct {
        BufferArray: [*]const WSABUF(.@"var"),
        BufferCount: ULONG,
        AfdFlags: AFD,
        TdiFlags: TDI.RECEIVE,
        Address: PVOID,
        AddressLength: *ULONG,
    };
    pub const SOCKOPT_INFO = extern struct {
        mode: Mode,
        level: i32,
        optname: u32,
        ding: u32 = 1,
        optval: *const anyopaque,
        optlen: usize,

        pub const Mode = enum(u32) { set = 1, get = 2, special = 3, _ };

        pub const UNIX_PATH = extern struct { Unknown0: usize = 0, Path: [PATH_MAX_WIDE:0]u16 };
    };
};

pub const TDI = struct {
    pub const STATUS = NTSTATUS;
    pub const CONNECTION = struct {
        pub const CONTEXT = PVOID;
        pub const INFORMATION = extern struct {
            /// length of user data buffer
            UserDataLength: LONG,
            /// pointer to user data buffer
            UserData: PVOID,
            /// length of following buffer
            OptionsLength: LONG,
            /// pointer to buffer containing options
            Options: PVOID,
            /// length of following buffer
            RemoteAddressLength: LONG,
            /// buffer containing the remote address
            RemoteAddress: PVOID,
        };
    };
    pub const ADDRESS = struct {
        pub const TYPE = enum(USHORT) {
            /// unspecified
            UNSPEC = 0,
            /// local to host (pipes, portals,
            UNIX = 1,
            /// internetwork: UDP, TCP, etc.
            IP = 2,
            /// arpanet imp addresses
            IMPLINK = 3,
            /// pup protocols: e.g. BSP
            PUP = 4,
            /// mit CHAOS protocols
            CHAOS = 5,
            /// XEROX NS protocols
            NS = 6,
            /// Netware IPX
            IPX = 6,
            /// nbs protocols
            NBS = 7,
            /// european computer manufacturers
            ECMA = 8,
            /// datakit protocols
            DATAKIT = 9,
            /// CCITT protocols, X.25 etc
            CCITT = 10,
            /// IBM SNA
            SNA = 11,
            /// DECnet
            DECnet = 12,
            /// Direct data link interface
            DLI = 13,
            /// LAT
            LAT = 14,
            /// NSC Hyperchannel
            HYLINK = 15,
            /// AppleTalk
            APPLETALK = 16,
            /// Netbios Addresses
            NETBIOS = 17,
            @"8022" = 18,
            OSI_TSAP = 19,
            /// for WzMail
            NETONE = 20,
            /// Banyan VINES IP
            VNS = 21,
            /// NETBIOS address extensions
            NETBIOS_EX = 22,
            /// IP version 6
            IP6 = 23,
            /// WCHAR Netbios address
            NETBIOS_UNICODE_EX = 24,
            _,
        };
        pub const IP = extern struct {
            sin_port: USHORT,
            in_addr: ULONG,
            sin_zero: [8]UCHAR,
        };
        pub const IP6 = extern struct {
            sin_port: USHORT,
            flowinfo: ULONG,
            addr: [8]USHORT,
            scope_id: ULONG,
        };
    };
    pub const REQUEST = extern struct {
        Handle: extern union {
            AddressHandle: HANDLE,
            ConnectionContext: CONNECTION.CONTEXT,
            ControlChannel: HANDLE,
        },
        RequestNotifyObject: PVOID,
        RequestContext: PVOID,
        TdiStatus: TDI.STATUS,

        pub const STATUS = extern struct {
            /// status of request completion
            Status: TDI.STATUS,
            /// the request context
            RequestContext: PVOID,
            /// number of bytes transferred in the request
            BytesTransferred: ULONG,
        };
        pub const ASSOCIATE = extern struct {
            Request: REQUEST,
            AddressHandle: HANDLE,
        };
        pub const CONNECT = extern struct {
            Request: REQUEST,
            RequestConnectionInformation: *CONNECTION.INFORMATION,
            ReturnConnectionInformation: *CONNECTION.INFORMATION,
            Timeout: LARGE_INTEGER,
        };
        pub const ACCEPT = extern struct {
            Request: REQUEST,
            RequestConnectionInformation: *CONNECTION.INFORMATION,
            ReturnConnectionInformation: *CONNECTION.INFORMATION,
        };
        pub const LISTEN = extern struct {
            Request: REQUEST,
            RequestConnectionInformation: *CONNECTION.INFORMATION,
            ReturnConnectionInformation: *CONNECTION.INFORMATION,
            ListenFlags: USHORT,
        };
        pub const DISCONNECT = extern struct {
            Request: REQUEST,
            Timeout: LARGE_INTEGER,
        };
        pub const SEND = extern struct {
            Request: REQUEST,
            SendFlags: USHORT,
        };
        pub const RECEIVE = extern struct {
            Request: REQUEST,
            ReceiveFlags: USHORT,
        };
        pub const SEND_DATAGRAM = extern struct {
            Request: REQUEST,
            SendDatagramInformation: *CONNECTION.INFORMATION,
        };
    };
    pub const RECEIVE = packed struct(ULONG) {
        Reserved0: u2 = 0,
        BROADCAST: bool = false,
        MULTICAST: bool = false,
        PARTIAL: bool = false,
        NORMAL: bool = false,
        EXPEDITED: bool = false,
        PEEK: bool = false,
        NO_RESPONSE_EXP: bool = false,
        COPY_LOOKAHEAD: bool = false,
        ENTIRE_MESSAGE: bool = false,
        AT_DISPATCH_LEVEL: bool = false,
        CONTROL_INFO: bool = false,
        FORCE_INDICATION: bool = false,
        NO_PUSH: bool = false,
        Reserved12: u17 = 0,
    };
    pub const SEND = packed struct(ULONG) {
        Reserved0: u5 = 0,
        EXPEDITED: bool = false,
        PARTIAL: bool = false,
        NO_RESPONSE_EXPECTED: bool = false,
        NON_BLOCKING: bool = false,
        AND_DISCONNECT: bool = false,
        Reserved10: u22 = 0,
    };
};

pub const NET = struct {
    pub const LUID = packed struct(ULONG64) { Reserved: u24 = 0, Index: u24, IfType: u16 };
    pub const IFINDEX = enum(ULONG) { _ };
};

pub const DNS = struct {
    pub const INTERFACE_SETTINGS = extern struct {
        Version: ULONG,
        Flags: ULONG64,
        Domain: PWSTR,
        NameServer: PWSTR,
        SearchList: PWSTR,
        RegistrationEnabled: ULONG,
        RegisterAdapterName: ULONG,
        EnableLLMNR: ULONG,
        QueryAdapterName: ULONG,
        ProfileNameServer: PWSTR,
    };

    // ref: shared/windnsdef.h

    pub const ADDR_MAX_SOCKADDR_LENGTH = 32;

    pub const ADDR = extern struct {
        MaxSa: [ADDR_MAX_SOCKADDR_LENGTH]CHAR,
        DnsAddrUserDword: [8]DWORD,

        pub const ARRAY = extern struct {
            MaxCount: DWORD,
            AddrCount: DWORD,
            Tag: DWORD,
            Family: WORD,
            WordReserved: WORD,
            Flags: DWORD,
            MatchFlag: DWORD,
            Reserved1: DWORD,
            Reserved2: DWORD,
            AddrArray: [0]ADDR,
        };
    };

    pub const CUSTOM_SERVER = extern struct {
        ServerType: CUSTOM_SERVER.TYPE,
        Flags: FLAGS,
        Info: extern union {
            UDP: void,
            DOH: extern struct { Template: PWSTR },
            DOT: extern struct { Hostname: PWSTR },
        },
        MaxSa: [ADDR_MAX_SOCKADDR_LENGTH]CHAR,

        pub const TYPE = enum(DWORD) { UDP = 0x1, DOH = 0x2, DOT = 0x3, _ };
        pub const FLAGS = packed struct(ULONG64) {
            UDP_FALLBACK: bool = false,
            UPGRADE_FROM_WELL_KNOWN_SERVERS: bool = false,
            Reserved2: u62 = 0,
        };
    };

    // ref: um/WinDNS.h

    pub const STATUS = Win32Error;

    pub const TYPE = enum(WORD) {
        A = 0x0001,
        NS = 0x0002,
        MD = 0x0003,
        MF = 0x0004,
        CNAME = 0x0005,
        SOA = 0x0006,
        MB = 0x0007,
        MG = 0x0008,
        MR = 0x0009,
        NULL = 0x000a,
        WKS = 0x000b,
        PTR = 0x000c,
        HINFO = 0x000d,
        MINFO = 0x000e,
        MX = 0x000f,
        TEXT = 0x0010,
        RP = 0x0011,
        AFSDB = 0x0012,
        X25 = 0x0013,
        ISDN = 0x0014,
        RT = 0x0015,
        NSAP = 0x0016,
        NSAPPTR = 0x0017,
        SIG = 0x0018,
        KEY = 0x0019,
        PX = 0x001a,
        GPOS = 0x001b,
        AAAA = 0x001c,
        LOC = 0x001d,
        NXT = 0x001e,
        EID = 0x001f,
        NIMLOC = 0x0020,
        SRV = 0x0021,
        ATMA = 0x0022,
        NAPTR = 0x0023,
        KX = 0x0024,
        CERT = 0x0025,
        A6 = 0x0026,
        DNAME = 0x0027,
        SINK = 0x0028,
        OPT = 0x0029,
        DS = 0x002B,
        RRSIG = 0x002E,
        NSEC = 0x002F,
        DNSKEY = 0x0030,
        DHCID = 0x0031,
        UINFO = 0x0064,
        UID = 0x0065,
        GID = 0x0066,
        UNSPEC = 0x0067,
        ADDRS = 0x00f8,
        TKEY = 0x00f9,
        TSIG = 0x00fa,
        IXFR = 0x00fb,
        AXFR = 0x00fc,
        MAILB = 0x00fd,
        MAILA = 0x00fe,
        ALL = 0x00ff,
        WINS = 0xff01,
        WINSR = 0xff02,
        TLSA = 0x0034,
        SVCB = 0x0040,
        HTTPS = 0x0041,
        pub const NBSTAT: TYPE = .WINSR;
        pub const ANY: TYPE = .ALL;
    };

    pub const QUERY = packed struct(ULONG64) {
        pub const STANDARD: QUERY = .{};
        ACCEPT_TRUNCATED_RESPONSE: bool = false,
        USE_TCP_ONLY: bool = false,
        NO_RECURSION: bool = false,
        BYPASS_CACHE: bool = false,
        NO_WIRE_QUERY: bool = false,
        NO_LOCAL_NAME: bool = false,
        NO_HOSTS_FILE: bool = false,
        NO_NETBT: bool = false,
        WIRE_ONLY: bool = false,
        RETURN_MESSAGE: bool = false,
        MULTICAST_ONLY: bool = false,
        NO_MULTICAST: bool = false,
        TREAT_AS_FQDN: bool = false,
        ADDRCONFIG: bool = false,
        DUAL_ADDR: bool = false,
        Reserved15: u2 = 0,
        MULTICAST_WAIT: bool = false,
        MULTICAST_VERIFY: bool = false,
        Reserved19: u1 = 0,
        DONT_RESET_TTL_VALUES: bool = false,
        DISABLE_IDN_ENCODING: bool = false,
        Reserved22: u1 = 0,
        APPEND_MULTILABEL: bool = false,
        Reserved24: u34 = 0,
        PARSE_ALL_RECORDS: bool = false,
        Reserved59: u5 = 0,

        pub const REQUEST = extern struct {
            Version: DWORD,
            QueryName: PCWSTR,
            QueryType: TYPE,
            QueryOptions: QUERY = .STANDARD,
            pDnsServerList: ?*ADDR.ARRAY = null,
            InterfaceIndex: ULONG = 0,
            pQueryCompletionCallback: ?*const COMPLETION_ROUTINE = null,
            pQueryContext: ?*anyopaque = null,

            pub const @"3" = extern struct {
                Base: REQUEST,
                IsNetworkQueryRequired: BOOL = .FALSE,
                RequiredNetworkIndex: DWORD = 0,
                cCustomServers: DWORD = 0,
                pCustomServers: ?*CUSTOM_SERVER = null,
            };
        };
        pub const RESULT = extern struct {
            Version: ULONG,
            QueryStatus: STATUS,
            QueryOptions: QUERY,
            pQueryRecords: ?*RECORD,
            Reserved: ?*anyopaque,
        };
        pub const CANCEL = extern struct {
            Reserved: [32]CHAR align(8),
        };
        pub const COMPLETION_ROUTINE = fn (
            pQueryContext: ?*anyopaque,
            pQueryResults: *RESULT,
        ) callconv(.winapi) void;
    };
    pub const FREE_TYPE = enum(c_int) { Flat = 0, RecordList, ParsedMessageFields };
    pub const RECORD = extern struct {
        pNext: ?*RECORD,
        pName: *anyopaque,
        wType: TYPE,
        wDataLength: WORD,
        Flags: FLAGS,
        dwTtl: DWORD,
        dwReserved: DWORD,
        Data: extern union { A: [4]u8, AAAA: [16]u8 },

        pub const FLAGS = packed struct(DWORD) {
            Section: SECTION,
            Delete: u1,
            CharSet: u2,
            Unused: u3,
            Reserved: u24,
        };
    };
    pub const SECTION = enum(u2) { Question, Answer, Authority, Additional };
};

// ref: km/ntddk.h

pub const SYSTEM = struct {
    pub const INFORMATION_CLASS = enum(c_int) {
        Basic = 0,
        Performance = 2,
        TimeOfDay = 3,
        Process = 5,
        ProcessorPerformance = 8,
        Interrupt = 23,
        Exception = 33,
        RegistryQuota = 37,
        Lookaside = 45,
        CodeIntegrity = 103,
        Policy = 134,
        _,
    };

    pub const BASIC_INFORMATION = extern struct {
        Reserved: ULONG,
        TimerResolution: ULONG,
        PageSize: ULONG,
        NumberOfPhysicalPages: ULONG,
        LowestPhysicalPageNumber: ULONG,
        HighestPhysicalPageNumber: ULONG,
        AllocationGranularity: ULONG,
        MinimumUserModeAddress: ULONG_PTR,
        MaximumUserModeAddress: ULONG_PTR,
        ActiveProcessorsAffinityMask: KAFFINITY,
        NumberOfProcessors: UCHAR,
    };
};

pub const PROCESS = struct {
    pub const INFORMATION = extern struct {
        hProcess: HANDLE,
        hThread: HANDLE,
        dwProcessId: DWORD,
        dwThreadId: DWORD,
    };

    pub const INFOCLASS = enum(c_int) {
        BasicInformation = 0,
        QuotaLimits = 1,
        IoCounters = 2,
        VmCounters = 3,
        Times = 4,
        BasePriority = 5,
        RaisePriority = 6,
        DebugPort = 7,
        ExceptionPort = 8,
        AccessToken = 9,
        LdtInformation = 10,
        LdtSize = 11,
        DefaultHardErrorMode = 12,
        IoPortHandlers = 13,
        PooledUsageAndLimits = 14,
        WorkingSetWatch = 15,
        UserModeIOPL = 16,
        EnableAlignmentFaultFixup = 17,
        PriorityClass = 18,
        Wx86Information = 19,
        HandleCount = 20,
        AffinityMask = 21,
        PriorityBoost = 22,
        DeviceMap = 23,
        SessionInformation = 24,
        ForegroundInformation = 25,
        Wow64Information = 26,
        ImageFileName = 27,
        LUIDDeviceMapsEnabled = 28,
        BreakOnTermination = 29,
        DebugObjectHandle = 30,
        DebugFlags = 31,
        HandleTracing = 32,
        IoPriority = 33,
        ExecuteFlags = 34,
        TlsInformation = 35,
        Cookie = 36,
        ImageInformation = 37,
        CycleTime = 38,
        PagePriority = 39,
        InstrumentationCallback = 40,
        ThreadStackAllocation = 41,
        WorkingSetWatchEx = 42,
        ImageFileNameWin32 = 43,
        ImageFileMapping = 44,
        AffinityUpdateMode = 45,
        MemoryAllocationMode = 46,
        GroupInformation = 47,
        TokenVirtualizationEnabled = 48,
        OwnerInformation = 49,
        WindowInformation = 50,
        HandleInformation = 51,
        MitigationPolicy = 52,
        DynamicFunctionTableInformation = 53,
        HandleCheckingMode = 54,
        KeepAliveCount = 55,
        RevokeFileHandles = 56,
        WorkingSetControl = 57,
        HandleTable = 58,
        CheckStackExtentsMode = 59,
        CommandLineInformation = 60,
        ProtectionInformation = 61,
        MemoryExhaustion = 62,
        FaultInformation = 63,
        TelemetryIdInformation = 64,
        CommitReleaseInformation = 65,
        Reserved1Information = 66,
        Reserved2Information = 67,
        SubsystemProcess = 68,
        InPrivate = 70,
        RaiseUMExceptionOnInvalidHandleClose = 71,
        SubsystemInformation = 75,
        Win32kSyscallFilterInformation = 79,
        EnergyTrackingState = 82,
        NetworkIoCounters = 114,
        _,

        pub const Max: @typeInfo(@This()).@"enum".tag_type = 117;
    };

    pub const BASIC_INFORMATION = extern struct {
        ExitStatus: NTSTATUS,
        PebBaseAddress: *PEB,
        AffinityMask: ULONG_PTR,
        BasePriority: KPRIORITY,
        UniqueProcessId: ULONG_PTR,
        InheritedFromUniqueProcessId: ULONG_PTR,
    };

    pub const VM_COUNTERS = extern struct {
        PeakVirtualSize: SIZE_T,
        VirtualSize: SIZE_T,
        PageFaultCount: ULONG,
        PeakWorkingSetSize: SIZE_T,
        WorkingSetSize: SIZE_T,
        QuotaPeakPagedPoolUsage: SIZE_T,
        QuotaPagedPoolUsage: SIZE_T,
        QuotaPeakNonPagedPoolUsage: SIZE_T,
        QuotaNonPagedPoolUsage: SIZE_T,
        PagefileUsage: SIZE_T,
        PeakPagefileUsage: SIZE_T,
    };
};

pub const THREAD = struct {
    pub const INFOCLASS = enum(c_int) {
        BasicInformation = 0,
        Times = 1,
        Priority = 2,
        BasePriority = 3,
        AffinityMask = 4,
        ImpersonationToken = 5,
        DescriptorTableEntry = 6,
        EnableAlignmentFaultFixup = 7,
        EventPair_Reusable = 8,
        QuerySetWin32StartAddress = 9,
        ZeroTlsCell = 10,
        PerformanceCount = 11,
        AmILastThread = 12,
        IdealProcessor = 13,
        PriorityBoost = 14,
        SetTlsArrayAddress = 15,
        IsIoPending = 16,
        // Windows 2000+ from here
        HideFromDebugger = 17,
        // Windows XP+ from here
        BreakOnTermination = 18,
        SwitchLegacyState = 19,
        IsTerminated = 20,
        // Windows Vista+ from here
        LastSystemCall = 21,
        IoPriority = 22,
        CycleTime = 23,
        PagePriority = 24,
        ActualBasePriority = 25,
        TebInformation = 26,
        CSwitchMon = 27,
        // Windows 7+ from here
        CSwitchPmu = 28,
        Wow64Context = 29,
        GroupInformation = 30,
        UmsInformation = 31,
        CounterProfiling = 32,
        IdealProcessorEx = 33,
        // Windows 8+ from here
        CpuAccountingInformation = 34,
        // Windows 8.1+ from here
        SuspendCount = 35,
        // Windows 10+ from here
        HeterogeneousCpuPolicy = 36,
        ContainerId = 37,
        NameInformation = 38,
        SelectedCpuSets = 39,
        SystemThreadInformation = 40,
        ActualGroupAffinity = 41,
        DynamicCodePolicyInfo = 42,
        SubsystemInformation = 45,
        _,

        pub const Max: @typeInfo(@This()).@"enum".tag_type = 60;
    };

    pub const BASIC_INFORMATION = extern struct {
        ExitStatus: NTSTATUS,
        TebBaseAddress: PVOID,
        ClientId: CLIENT_ID,
        AffinityMask: KAFFINITY,
        Priority: KPRIORITY,
        BasePriority: KPRIORITY,
    };

    pub const CREATE_FLAGS = packed struct(ULONG) {
        CREATE_SUSPENDED: bool = false,
        SKIP_THREAD_ATTACH: bool = false,
        HIDE_FROM_DEBUGGER: bool = false,
        LOADER_WORKER: bool = false,
        SKIP_LOADER_INIT: bool = false,
        BYPASS_PROCESS_FREEZE: bool = false,
        Reserved6: u26 = 0,

        pub const NONE: CREATE_FLAGS = .{};
    };

    pub const StackSize = enum(SIZE_T) {
        /// The default size specified in the executable header
        default = 0,
        _,
    };
};

pub const MEMORY = struct {
    pub const BASIC_INFORMATION = extern struct {
        BaseAddress: PVOID,
        AllocationBase: PVOID,
        AllocationProtect: DWORD,
        PartitionId: WORD,
        RegionSize: SIZE_T,
        State: DWORD,
        Protect: DWORD,
        Type: DWORD,
    };
};

// ref: km/ntifs.h

pub const HEAP = opaque {
    pub const FLAGS = packed struct(u8) {
        /// Serialized access is not used when the heap functions access this heap. This option
        /// applies to all subsequent heap function calls. Alternatively, you can specify this
        /// option on individual heap function calls.
        ///
        /// The low-fragmentation heap (LFH) cannot be enabled for a heap created with this option.
        ///
        /// A heap created with this option cannot be locked.
        NO_SERIALIZE: bool = false,
        /// Specifies that the heap is growable. Must be specified if `HeapBase` is `NULL`.
        GROWABLE: bool = false,
        /// The system raises an exception to indicate failure (for example, an out-of-memory
        /// condition) for calls to `HeapAlloc` and `HeapReAlloc` instead of returning `NULL`.
        ///
        /// To ensure that exceptions are generated for all calls to an allocation function, specify
        /// `GENERATE_EXCEPTIONS` in the call to `HeapCreate`. In this case, it is not necessary to
        /// additionally specify `GENERATE_EXCEPTIONS` in the allocation function calls.
        GENERATE_EXCEPTIONS: bool = false,
        /// The allocated memory will be initialized to zero. Otherwise, the memory is not
        /// initialized to zero.
        ZERO_MEMORY: bool = false,
        REALLOC_IN_PLACE_ONLY: bool = false,
        TAIL_CHECKING_ENABLED: bool = false,
        FREE_CHECKING_ENABLED: bool = false,
        DISABLE_COALESCE_ON_FREE: bool = false,

        pub const CLASS = enum(u4) {
            /// process heap
            PROCESS,
            /// private heap
            PRIVATE,
            /// Kernel Heap
            KERNEL,
            /// GDI heap
            GDI,
            /// User heap
            USER,
            /// Console heap
            CONSOLE,
            /// User Desktop heap
            USER_DESKTOP,
            /// Csrss Shared heap
            CSRSS_SHARED,
            /// Csr Port heap
            CSR_PORT,
            _,

            pub const MASK: CLASS = @enumFromInt(maxInt(@typeInfo(CLASS).@"enum".tag_type));
        };

        pub const CREATE = packed struct(ULONG) {
            COMMON: FLAGS = .{},
            SEGMENT_HEAP: bool = false,
            /// Only applies to segment heap.  Applies pointer obfuscation which is
            /// generally excessive and unnecessary but is necessary for certain insecure
            /// heaps in win32k.
            ///
            /// Specifying HEAP_CREATE_HARDENED prevents the heap from using locks as
            /// pointers would potentially be exposed in heap metadata lock variables.
            /// Callers are therefore responsible for synchronizing access to hardened heaps.
            HARDENED: bool = false,
            Reserved10: u2 = 0,
            CLASS: CLASS = @enumFromInt(0),
            /// Create heap with 16 byte alignment (obsolete)
            ALIGN_16: bool = false,
            /// Create heap call tracing enabled (obsolete)
            ENABLE_TRACING: bool = false,
            /// Create heap with executable pages
            ///
            /// All memory blocks that are allocated from this heap allow code execution, if the
            /// hardware enforces data execution prevention. Use this flag heap in applications that
            /// run code from the heap. If `ENABLE_EXECUTE` is not specified and an application
            /// attempts to run code from a protected page, the application receives an exception
            /// with the status code `STATUS_ACCESS_VIOLATION`.
            ENABLE_EXECUTE: bool = false,
            Reserved19: u13 = 0,

            pub const VALID_MASK: CREATE = .{
                .COMMON = .{
                    .NO_SERIALIZE = true,
                    .GROWABLE = true,
                    .GENERATE_EXCEPTIONS = true,
                    .ZERO_MEMORY = true,
                    .REALLOC_IN_PLACE_ONLY = true,
                    .TAIL_CHECKING_ENABLED = true,
                    .FREE_CHECKING_ENABLED = true,
                    .DISABLE_COALESCE_ON_FREE = true,
                },
                .CLASS = .MASK,
                .ALIGN_16 = true,
                .ENABLE_TRACING = true,
                .ENABLE_EXECUTE = true,
                .SEGMENT_HEAP = true,
                .HARDENED = true,
            };
        };

        pub const ALLOCATION = packed struct(ULONG) {
            COMMON: FLAGS = .{},
            SETTABLE_USER: packed struct(u4) {
                VALUE: u1 = 0,
                FLAGS: packed struct(u3) {
                    FLAG1: bool = false,
                    FLAG2: bool = false,
                    FLAG3: bool = false,
                } = .{},
            } = .{},
            CLASS: CLASS = @enumFromInt(0),
            Reserved16: u2 = 0,
            TAG: u12 = 0,
            Reserved30: u2 = 0,
        };
    };

    pub const RTL_PARAMETERS = extern struct {
        Length: ULONG,
        SegmentReserve: SIZE_T,
        SegmentCommit: SIZE_T,
        DeCommitFreeBlockThreshold: SIZE_T,
        DeCommitTotalFreeThreshold: SIZE_T,
        MaximumAllocationSize: SIZE_T,
        VirtualMemoryThreshold: SIZE_T,
        InitialCommit: SIZE_T,
        InitialReserve: SIZE_T,
        CommitRoutine: *const COMMIT_ROUTINE,
        Reserved: [2]SIZE_T = @splat(0),

        pub const COMMIT_ROUTINE = fn (
            Base: PVOID,
            CommitAddress: *PVOID,
            CommitSize: *SIZE_T,
        ) callconv(.winapi) NTSTATUS;

        pub const SEGMENT = extern struct {
            Version: VERSION,
            Size: USHORT,
            Flags: FLG,
            MemorySource: MEMORY_SOURCE,
            Reserved: [4]SIZE_T,

            pub const VERSION = enum(USHORT) {
                CURRENT = 3,
                _,
            };

            pub const FLG = packed struct(ULONG) {
                USE_PAGE_HEAP: bool = false,
                NO_LFH: bool = false,
                Reserved2: u30 = 0,

                pub const VALID_FLAGS: FLG = .{
                    .USE_PAGE_HEAP = true,
                    .NO_LFH = true,
                };
            };

            pub const MEMORY_SOURCE = extern struct {
                Flags: ULONG,
                MemoryTypeMask: TYPE,
                NumaNode: ULONG,
                u: extern union {
                    PartitionHandle: HANDLE,
                    Callbacks: *const VA_CALLBACKS,
                },
                Reserved: [2]SIZE_T = @splat(0),

                pub const TYPE = enum(ULONG) {
                    Paged,
                    NonPaged,
                    @"64KPage",
                    LargePage,
                    HugePage,
                    Custom,
                    _,

                    pub const Max: @typeInfo(@This()).@"enum".tag_type = @typeInfo(@This()).@"enum".fields.len;
                };

                pub const VA_CALLBACKS = extern struct {
                    CallbackContext: HANDLE,
                    AllocateVirtualMemory: *const ALLOCATE_VIRTUAL_MEMORY_EX_CALLBACK,
                    FreeVirtualMemory: *const FREE_VIRTUAL_MEMORY_EX_CALLBACK,
                    QueryVirtualMemory: *const QUERY_VIRTUAL_MEMORY_CALLBACK,

                    pub const ALLOCATE_VIRTUAL_MEMORY_EX_CALLBACK = fn (
                        CallbackContext: HANDLE,
                        BaseAddress: *PVOID,
                        RegionSize: *SIZE_T,
                        AllocationType: ULONG,
                        PageProtection: ULONG,
                        ExtendedParameters: ?[*]MEM.EXTENDED_PARAMETER,
                        ExtendedParameterCount: ULONG,
                    ) callconv(.c) NTSTATUS;

                    pub const FREE_VIRTUAL_MEMORY_EX_CALLBACK = fn (
                        CallbackContext: HANDLE,
                        ProcessHandle: HANDLE,
                        BaseAddress: *PVOID,
                        RegionSize: *SIZE_T,
                        FreeType: ULONG,
                    ) callconv(.c) NTSTATUS;

                    pub const QUERY_VIRTUAL_MEMORY_CALLBACK = fn (
                        CallbackContext: HANDLE,
                        ProcessHandle: HANDLE,
                        BaseAddress: *PVOID,
                        MemoryInformationClass: MEMORY_INFO_CLASS,
                        MemoryInformation: PVOID,
                        MemoryInformationLength: SIZE_T,
                        ReturnLength: ?*SIZE_T,
                    ) callconv(.c) NTSTATUS;

                    pub const MEMORY_INFO_CLASS = enum(c_int) {
                        Basic,
                        _,
                    };
                };
            };
        };
    };
};

pub const CTL_CODE = packed struct(ULONG) {
    Method: METHOD,
    Function: u12,
    Access: FILE_ACCESS,
    DeviceType: FILE_DEVICE,

    pub const METHOD = enum(u2) {
        BUFFERED = 0,
        IN_DIRECT = 1,
        OUT_DIRECT = 2,
        NEITHER = 3,
    };

    pub const FILE_ACCESS = packed struct(u2) {
        READ: bool = false,
        WRITE: bool = false,

        pub const ANY: FILE_ACCESS = .{ .READ = false, .WRITE = false };
        pub const SPECIAL = ANY;
    };

    pub const FILE_DEVICE = enum(u16) {
        BEEP = 0x00000001,
        CD_ROM = 0x00000002,
        CD_ROM_FILE_SYSTEM = 0x00000003,
        CONTROLLER = 0x00000004,
        DATALINK = 0x00000005,
        DFS = 0x00000006,
        DISK = 0x00000007,
        DISK_FILE_SYSTEM = 0x00000008,
        FILE_SYSTEM = 0x00000009,
        INPORT_PORT = 0x0000000a,
        KEYBOARD = 0x0000000b,
        MAILSLOT = 0x0000000c,
        MIDI_IN = 0x0000000d,
        MIDI_OUT = 0x0000000e,
        MOUSE = 0x0000000f,
        MULTI_UNC_PROVIDER = 0x00000010,
        NAMED_PIPE = 0x00000011,
        NETWORK = 0x00000012,
        NETWORK_BROWSER = 0x00000013,
        NETWORK_FILE_SYSTEM = 0x00000014,
        NULL = 0x00000015,
        PARALLEL_PORT = 0x00000016,
        PHYSICAL_NETCARD = 0x00000017,
        PRINTER = 0x00000018,
        SCANNER = 0x00000019,
        SERIAL_MOUSE_PORT = 0x0000001a,
        SERIAL_PORT = 0x0000001b,
        SCREEN = 0x0000001c,
        SOUND = 0x0000001d,
        STREAMS = 0x0000001e,
        TAPE = 0x0000001f,
        TAPE_FILE_SYSTEM = 0x00000020,
        TRANSPORT = 0x00000021,
        UNKNOWN = 0x00000022,
        VIDEO = 0x00000023,
        VIRTUAL_DISK = 0x00000024,
        WAVE_IN = 0x00000025,
        WAVE_OUT = 0x00000026,
        @"8042_PORT" = 0x00000027,
        NETWORK_REDIRECTOR = 0x00000028,
        BATTERY = 0x00000029,
        BUS_EXTENDER = 0x0000002a,
        MODEM = 0x0000002b,
        VDM = 0x0000002c,
        MASS_STORAGE = 0x0000002d,
        SMB = 0x0000002e,
        KS = 0x0000002f,
        CHANGER = 0x00000030,
        SMARTCARD = 0x00000031,
        ACPI = 0x00000032,
        DVD = 0x00000033,
        FULLSCREEN_VIDEO = 0x00000034,
        DFS_FILE_SYSTEM = 0x00000035,
        DFS_VOLUME = 0x00000036,
        SERENUM = 0x00000037,
        TERMSRV = 0x00000038,
        KSEC = 0x00000039,
        FIPS = 0x0000003A,
        INFINIBAND = 0x0000003B,
        VMBUS = 0x0000003E,
        CRYPT_PROVIDER = 0x0000003F,
        WPD = 0x00000040,
        BLUETOOTH = 0x00000041,
        MT_COMPOSITE = 0x00000042,
        MT_TRANSPORT = 0x00000043,
        BIOMETRIC = 0x00000044,
        PMI = 0x00000045,
        EHSTOR = 0x00000046,
        DEVAPI = 0x00000047,
        GPIO = 0x00000048,
        USBEX = 0x00000049,
        CONSOLE = 0x00000050,
        NFP = 0x00000051,
        SYSENV = 0x00000052,
        VIRTUAL_BLOCK = 0x00000053,
        POINT_OF_SERVICE = 0x00000054,
        STORAGE_REPLICATION = 0x00000055,
        TRUST_ENV = 0x00000056,
        UCM = 0x00000057,
        UCMTCPCI = 0x00000058,
        PERSISTENT_MEMORY = 0x00000059,
        NVDIMM = 0x0000005a,
        HOLOGRAPHIC = 0x0000005b,
        SDFXHCI = 0x0000005c,
        UCMUCSI = 0x0000005d,
        PRM = 0x0000005e,
        EVENT_COLLECTOR = 0x0000005f,
        USB4 = 0x00000060,
        SOUNDWIRE = 0x00000061,

        MOUNTMGRCONTROLTYPE = 'm',

        _,
    };

    pub const SET_REPARSE_POINT: CTL_CODE = .{ .DeviceType = .FILE_SYSTEM, .Function = 41, .Method = .BUFFERED, .Access = .SPECIAL };
    pub const GET_REPARSE_POINT: CTL_CODE = .{ .DeviceType = .FILE_SYSTEM, .Function = 42, .Method = .BUFFERED, .Access = .ANY };

    pub const PIPE = struct {
        pub const ASSIGN_EVENT: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 0, .Method = .BUFFERED, .Access = .ANY };
        pub const DISCONNECT: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 1, .Method = .BUFFERED, .Access = .ANY };
        pub const LISTEN: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 2, .Method = .BUFFERED, .Access = .ANY };
        pub const PEEK: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 3, .Method = .BUFFERED, .Access = .{ .READ = true } };
        pub const QUERY_EVENT: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 4, .Method = .BUFFERED, .Access = .ANY };
        pub const TRANSCEIVE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 5, .Method = .NEITHER, .Access = .{ .READ = true, .WRITE = true } };
        pub const WAIT: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 6, .Method = .BUFFERED, .Access = .ANY };
        pub const IMPERSONATE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 7, .Method = .BUFFERED, .Access = .ANY };
        pub const SET_CLIENT_PROCESS: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 8, .Method = .BUFFERED, .Access = .ANY };
        pub const QUERY_CLIENT_PROCESS: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 9, .Method = .BUFFERED, .Access = .ANY };
        pub const GET_PIPE_ATTRIBUTE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 10, .Method = .BUFFERED, .Access = .ANY };
        pub const SET_PIPE_ATTRIBUTE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 11, .Method = .BUFFERED, .Access = .ANY };
        pub const GET_CONNECTION_ATTRIBUTE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 12, .Method = .BUFFERED, .Access = .ANY };
        pub const SET_CONNECTION_ATTRIBUTE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 13, .Method = .BUFFERED, .Access = .ANY };
        pub const GET_HANDLE_ATTRIBUTE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 14, .Method = .BUFFERED, .Access = .ANY };
        pub const SET_HANDLE_ATTRIBUTE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 15, .Method = .BUFFERED, .Access = .ANY };
        pub const FLUSH: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 16, .Method = .BUFFERED, .Access = .{ .WRITE = true } };

        pub const INTERNAL_READ: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 2045, .Method = .BUFFERED, .Access = .{ .READ = true } };
        pub const INTERNAL_WRITE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 2046, .Method = .BUFFERED, .Access = .{ .WRITE = true } };
        pub const INTERNAL_TRANSCEIVE: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 2047, .Method = .NEITHER, .Access = .{ .READ = true, .WRITE = true } };
        pub const INTERNAL_READ_OVFLOW: CTL_CODE = .{ .DeviceType = .NAMED_PIPE, .Function = 2048, .Method = .BUFFERED, .Access = .{ .READ = true } };
    };
};

pub const IOCTL = struct {
    pub const AFD = struct {
        const CONTROL_CODE = packed struct {
            Method: CTL_CODE.METHOD,
            Function: u10,
            DeviceType: CTL_CODE.FILE_DEVICE,
            Reserved28: u4 = 0,
        };
        pub const BIND: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 0, .Method = .NEITHER });
        pub const CONNECT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 1, .Method = .NEITHER });
        pub const START_LISTEN: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 2, .Method = .NEITHER });
        pub const WAIT_FOR_LISTEN: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 3, .Method = .BUFFERED });
        pub const ACCEPT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 4, .Method = .BUFFERED });
        pub const RECEIVE: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 5, .Method = .NEITHER });
        pub const RECEIVE_DATAGRAM: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 6, .Method = .NEITHER });
        pub const SEND: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 7, .Method = .NEITHER });
        pub const SEND_DATAGRAM: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 8, .Method = .NEITHER });
        pub const POLL: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 9, .Method = .BUFFERED });
        pub const PARTIAL_DISCONNECT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 10, .Method = .NEITHER });

        pub const GET_ADDRESS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 11, .Method = .NEITHER });
        pub const QUERY_RECEIVE_INFO: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 12, .Method = .NEITHER });
        pub const QUERY_HANDLES: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 13, .Method = .NEITHER });
        pub const SET_INFORMATION: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 14, .Method = .NEITHER });
        pub const GET_CONTEXT_LENGTH: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 15, .Method = .NEITHER });
        pub const GET_CONTEXT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 16, .Method = .NEITHER });
        pub const SET_CONTEXT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 17, .Method = .NEITHER });

        pub const SET_CONNECT_DATA: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 18, .Method = .BUFFERED });
        pub const SET_CONNECT_OPTIONS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 19, .Method = .BUFFERED });
        pub const SET_DISCONNECT_DATA: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 20, .Method = .BUFFERED });
        pub const SET_DISCONNECT_OPTIONS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 21, .Method = .BUFFERED });
        pub const GET_CONNECT_DATA: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 22, .Method = .BUFFERED });
        pub const GET_CONNECT_OPTIONS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 23, .Method = .BUFFERED });
        pub const GET_DISCONNECT_DATA: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 24, .Method = .BUFFERED });
        pub const GET_DISCONNECT_OPTIONS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 25, .Method = .BUFFERED });
        pub const SIZE_CONNECT_DATA: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 26, .Method = .BUFFERED });
        pub const SIZE_CONNECT_OPTIONS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 27, .Method = .BUFFERED });
        pub const SIZE_DISCONNECT_DATA: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 28, .Method = .BUFFERED });
        pub const SIZE_DISCONNECT_OPTIONS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 29, .Method = .BUFFERED });

        pub const GET_INFORMATION: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 30, .Method = .NEITHER });
        pub const TRANSMIT_FILE: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 31, .Method = .NEITHER });
        pub const SUPER_ACCEPT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 32, .Method = .NEITHER });

        pub const EVENT_SELECT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 33, .Method = .BUFFERED });
        pub const ENUM_NETWORK_EVENTS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 34, .Method = .BUFFERED });

        pub const DEFER_ACCEPT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 35, .Method = .BUFFERED });
        pub const WAIT_FOR_LISTEN_LIFO: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 36, .Method = .BUFFERED });
        pub const SET_QOS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 37, .Method = .BUFFERED });
        pub const GET_QOS: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 38, .Method = .BUFFERED });
        pub const NO_OPERATION: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 39, .Method = .NEITHER });
        pub const VALIDATE_GROUP: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 40, .Method = .BUFFERED });
        pub const GET_UNACCEPTED_CONNECT_DATA: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 41, .Method = .BUFFERED });

        pub const QUEUE_APC: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 42, .Method = .BUFFERED });

        pub const SOCKOPT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 47, .Method = .NEITHER });
        pub const SUPER_CONNECT: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 49, .Method = .NEITHER });
        pub const RECV_MSG: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 51, .Method = .NEITHER });
        pub const RIO: CTL_CODE = @bitCast(CONTROL_CODE{ .DeviceType = .NETWORK, .Function = 70, .Method = .NEITHER });
    };
    pub const CONDRV = struct {
        pub const READ_IO: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 1, .Method = .OUT_DIRECT, .Access = .ANY };
        pub const COMPLETE_IO: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 2, .Method = .NEITHER, .Access = .ANY };
        pub const READ_INPUT: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 3, .Method = .NEITHER, .Access = .ANY };
        pub const WRITE_OUTPUT: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 4, .Method = .NEITHER, .Access = .ANY };
        pub const ISSUE_USER_IO: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 5, .Method = .OUT_DIRECT, .Access = .ANY };
        pub const DISCONNECT_PIPE: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 6, .Method = .NEITHER, .Access = .ANY };
        pub const SET_SERVER_INFORMATION: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 7, .Method = .NEITHER, .Access = .ANY };
        pub const GET_SERVER_PID: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 8, .Method = .NEITHER, .Access = .ANY };
        pub const GET_DISPLAY_SIZE: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 9, .Method = .NEITHER, .Access = .ANY };
        pub const UPDATE_DISPLAY: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 10, .Method = .NEITHER, .Access = .ANY };
        pub const SET_CURSOR: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 11, .Method = .NEITHER, .Access = .ANY };
        pub const ALLOW_VIA_UIACCESS: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 12, .Method = .NEITHER, .Access = .ANY };
        pub const LAUNCH_SERVER: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 13, .Method = .NEITHER, .Access = .ANY };
        pub const GET_FONT_SIZE: CTL_CODE = .{ .DeviceType = .CONSOLE, .Function = 14, .Method = .NEITHER, .Access = .ANY };
    };
    pub const KSEC = struct {
        pub const GEN_RANDOM: CTL_CODE = .{ .DeviceType = .KSEC, .Function = 2, .Method = .BUFFERED, .Access = .ANY };
    };
    pub const MOUNTMGR = struct {
        pub const QUERY_POINTS: CTL_CODE = .{ .DeviceType = .MOUNTMGRCONTROLTYPE, .Function = 2, .Method = .BUFFERED, .Access = .ANY };
        pub const QUERY_DOS_VOLUME_PATH: CTL_CODE = .{ .DeviceType = .MOUNTMGRCONTROLTYPE, .Function = 12, .Method = .BUFFERED, .Access = .ANY };
    };
};

pub const MAXIMUM_REPARSE_DATA_BUFFER_SIZE: ULONG = 16 * 1024;

pub const IO_REPARSE_TAG = packed struct(ULONG) {
    Value: u12,
    Index: u4 = 0,
    ReservedBits: u12 = 0,
    /// Can have children if a directory.
    IsDirectory: bool = false,
    /// Represents another named entity in the system.
    IsSurrogate: bool = false,
    /// Must be `false` for non-Microsoft tags.
    IsReserved: bool = false,
    /// Owned by Microsoft.
    IsMicrosoft: bool = false,

    pub const RESERVED_INVALID: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsReserved = true, .Index = 0x8, .Value = 0x000 };
    pub const MOUNT_POINT: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsSurrogate = true, .Value = 0x003 };
    pub const HSM: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsReserved = true, .Value = 0x004 };
    pub const DRIVE_EXTENDER: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x005 };
    pub const HSM2: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x006 };
    pub const SIS: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x007 };
    pub const WIM: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x008 };
    pub const CSV: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x009 };
    pub const DFS: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x00A };
    pub const FILTER_MANAGER: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x00B };
    pub const SYMLINK: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsSurrogate = true, .Value = 0x00C };
    pub const IIS_CACHE: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsSurrogate = true, .Value = 0x010 };
    pub const DFSR: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x012 };
    pub const DEDUP: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x013 };
    pub const APPXSTRM: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsReserved = true, .Value = 0x014 };
    pub const NFS: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x014 };
    pub const FILE_PLACEHOLDER: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x015 };
    pub const DFM: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x016 };
    pub const WOF: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x017 };
    pub inline fn WCI(index: u1) IO_REPARSE_TAG {
        return .{ .IsMicrosoft = true, .IsDirectory = index == 0x1, .Index = index, .Value = 0x018 };
    }
    pub const GLOBAL_REPARSE: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsSurrogate = true, .Value = 0x0019 };
    pub inline fn CLOUD(index: u4) IO_REPARSE_TAG {
        return .{ .IsMicrosoft = true, .IsDirectory = true, .Index = index, .Value = 0x01A };
    }
    pub const APPEXECLINK: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x01B };
    pub const PROJFS: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsDirectory = true, .Value = 0x01C };
    pub const LX_SYMLINK: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsSurrogate = true, .Value = 0x01D };
    pub const STORAGE_SYNC: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x01E };
    pub const WCI_TOMBSTONE: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsSurrogate = true, .Value = 0x01F };
    pub const UNHANDLED: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x020 };
    pub const ONEDRIVE: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x021 };
    pub const PROJFS_TOMBSTONE: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsSurrogate = true, .Value = 0x022 };
    pub const AF_UNIX: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x023 };
    pub const LX_FIFO: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x024 };
    pub const LX_CHR: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x025 };
    pub const LX_BLK: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .Value = 0x026 };
    pub const LX_STORAGE_SYNC_FOLDER: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsDirectory = true, .Value = 0x027 };
    pub inline fn WCI_LINK(index: u1) IO_REPARSE_TAG {
        return .{ .IsMicrosoft = true, .IsSurrogate = true, .Index = index, .Value = 0x027 };
    }
    pub const DATALESS_CIM: IO_REPARSE_TAG = .{ .IsMicrosoft = true, .IsSurrogate = true, .Value = 0x28 };
};

// ref: km/wdm.h

pub const ACCESS_MASK = packed struct(DWORD) {
    SPECIFIC: Specific = .{ .bits = 0 },
    STANDARD: Standard = .{},
    Reserved21: u3 = 0,
    ACCESS_SYSTEM_SECURITY: bool = false,
    MAXIMUM_ALLOWED: bool = false,
    Reserved26: u2 = 0,
    GENERIC: Generic = .{},

    pub const Specific = packed union {
        bits: u16,

        // ref: km/wdm.h

        /// Define access rights to files and directories
        FILE: File,
        FILE_DIRECTORY: File.Directory,
        FILE_PIPE: File.Pipe,
        /// Registry Specific Access Rights.
        KEY: Key,
        /// Object Manager Object Type Specific Access Rights.
        OBJECT_TYPE: ObjectType,
        /// Object Manager Directory Specific Access Rights.
        DIRECTORY: Directory,
        /// Object Manager Symbolic Link Specific Access Rights.
        SYMBOLIC_LINK: SymbolicLink,
        /// Section Access Rights.
        SECTION: Section,
        /// Session Specific Access Rights.
        SESSION: Session,
        /// Process Specific Access Rights.
        PROCESS: Process,
        /// Thread Specific Access Rights.
        THREAD: Thread,
        /// Partition Specific Access Rights.
        MEMORY_PARTITION: MemoryPartition,
        /// Generic mappings for transaction manager rights.
        TRANSACTIONMANAGER: TransactionManager,
        /// Generic mappings for transaction rights.
        TRANSACTION: Transaction,
        /// Generic mappings for resource manager rights.
        RESOURCEMANAGER: ResourceManager,
        /// Generic mappings for enlistment rights.
        ENLISTMENT: Enlistment,
        /// Event Specific Access Rights.
        EVENT: Event,
        /// Semaphore Specific Access Rights.
        SEMAPHORE: Semaphore,

        // ref: km/ntifs.h

        /// Token Specific Access Rights.
        TOKEN: Token,

        // um/winnt.h

        /// Job Object Specific Access Rights.
        JOB_OBJECT: JobObject,
        /// Mutant Specific Access Rights.
        MUTANT: Mutant,
        /// Timer Specific Access Rights.
        TIMER: Timer,
        /// I/O Completion Specific Access Rights.
        IO_COMPLETION: IoCompletion,

        pub const File = packed struct(u16) {
            READ_DATA: bool = false,
            WRITE_DATA: bool = false,
            APPEND_DATA: bool = false,
            READ_EA: bool = false,
            WRITE_EA: bool = false,
            EXECUTE: bool = false,
            Reserved6: u1 = 0,
            READ_ATTRIBUTES: bool = false,
            WRITE_ATTRIBUTES: bool = false,
            Reserved9: u7 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .FILE = .{
                    .READ_DATA = true,
                    .WRITE_DATA = true,
                    .APPEND_DATA = true,
                    .READ_EA = true,
                    .WRITE_EA = true,
                    .EXECUTE = true,
                    .Reserved6 = maxInt(@FieldType(File, "Reserved6")),
                    .READ_ATTRIBUTES = true,
                    .WRITE_ATTRIBUTES = true,
                } },
            };

            pub const GENERIC_READ: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .READ,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .FILE = .{
                    .READ_DATA = true,
                    .READ_ATTRIBUTES = true,
                    .READ_EA = true,
                } },
            };

            pub const GENERIC_WRITE: ACCES
```
