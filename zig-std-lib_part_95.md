```
S_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .WRITE,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .FILE = .{
                    .WRITE_DATA = true,
                    .WRITE_ATTRIBUTES = true,
                    .WRITE_EA = true,
                    .APPEND_DATA = true,
                } },
            };

            pub const GENERIC_EXECUTE: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .EXECUTE,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .FILE = .{
                    .READ_ATTRIBUTES = true,
                    .EXECUTE = true,
                } },
            };

            pub const Directory = packed struct(u16) {
                LIST: bool = false,
                ADD_FILE: bool = false,
                ADD_SUBDIRECTORY: bool = false,
                READ_EA: bool = false,
                WRITE_EA: bool = false,
                TRAVERSE: bool = false,
                DELETE_CHILD: bool = false,
                READ_ATTRIBUTES: bool = false,
                WRITE_ATTRIBUTES: bool = false,
                Reserved9: u7 = 0,
            };

            pub const Pipe = packed struct(u16) {
                READ_DATA: bool = false,
                WRITE_DATA: bool = false,
                CREATE_PIPE_INSTANCE: bool = false,
                Reserved3: u4 = 0,
                READ_ATTRIBUTES: bool = false,
                WRITE_ATTRIBUTES: bool = false,
                Reserved9: u7 = 0,
            };
        };

        pub const Key = packed struct(u16) {
            /// Required to query the values of a registry key.
            QUERY_VALUE: bool = false,
            /// Required to create, delete, or set a registry value.
            SET_VALUE: bool = false,
            /// Required to create a subkey of a registry key.
            CREATE_SUB_KEY: bool = false,
            /// Required to enumerate the subkeys of a registry key.
            ENUMERATE_SUB_KEYS: bool = false,
            /// Required to request change notifications for a registry key or for subkeys of a registry key.
            NOTIFY: bool = false,
            /// Reserved for system use.
            CREATE_LINK: bool = false,
            Reserved6: u2 = 0,
            /// Indicates that an application on 64-bit Windows should operate on the 64-bit registry view.
            /// This flag is ignored by 32-bit Windows.
            WOW64_64KEY: bool = false,
            /// Indicates that an application on 64-bit Windows should operate on the 32-bit registry view.
            /// This flag is ignored by 32-bit Windows.
            WOW64_32KEY: bool = false,
            Reserved10: u6 = 0,

            pub const WOW64_RES: ACCESS_MASK = .{
                .SPECIFIC = .{ .KEY = .{
                    .WOW64_32KEY = true,
                    .WOW64_64KEY = true,
                } },
            };

            /// Combines the STANDARD_RIGHTS_READ, KEY_QUERY_VALUE, KEY_ENUMERATE_SUB_KEYS, and KEY_NOTIFY values.
            pub const READ: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .READ,
                    .SYNCHRONIZE = false,
                },
                .SPECIFIC = .{ .KEY = .{
                    .QUERY_VALUE = true,
                    .ENUMERATE_SUB_KEYS = true,
                    .NOTIFY = true,
                } },
            };

            /// Combines the STANDARD_RIGHTS_WRITE, KEY_SET_VALUE, and KEY_CREATE_SUB_KEY access rights.
            pub const WRITE: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .WRITE,
                    .SYNCHRONIZE = false,
                },
                .SPECIFIC = .{ .KEY = .{
                    .SET_VALUE = true,
                    .CREATE_SUB_KEY = true,
                } },
            };

            /// Equivalent to KEY_READ.
            pub const EXECUTE = READ;

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .ALL,
                    .SYNCHRONIZE = false,
                },
                .SPECIFIC = .{ .KEY = .{
                    .QUERY_VALUE = true,
                    .SET_VALUE = true,
                    .CREATE_SUB_KEY = true,
                    .ENUMERATE_SUB_KEYS = true,
                    .NOTIFY = true,
                    .CREATE_LINK = true,
                } },
            };
        };

        pub const ObjectType = packed struct(u16) {
            CREATE: bool = false,
            Reserved1: u15 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .OBJECT_TYPE = .{
                    .CREATE = true,
                } },
            };
        };

        pub const Directory = packed struct(u16) {
            QUERY: bool = false,
            TRAVERSE: bool = false,
            CREATE_OBJECT: bool = false,
            CREATE_SUBDIRECTORY: bool = false,
            Reserved3: u12 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .DIRECTORY = .{
                    .QUERY = true,
                    .TRAVERSE = true,
                    .CREATE_OBJECT = true,
                    .CREATE_SUBDIRECTORY = true,
                } },
            };
        };

        pub const SymbolicLink = packed struct(u16) {
            QUERY: bool = false,
            SET: bool = false,
            Reserved2: u14 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .SYMBOLIC_LINK = .{
                    .QUERY = true,
                } },
            };

            pub const ALL_ACCESS_EX: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .SYMBOLIC_LINK = .{
                    .QUERY = true,
                    .SET = true,
                    .Reserved2 = maxInt(@FieldType(SymbolicLink, "Reserved2")),
                } },
            };
        };

        pub const Section = packed struct(u16) {
            QUERY: bool = false,
            MAP_WRITE: bool = false,
            MAP_READ: bool = false,
            MAP_EXECUTE: bool = false,
            EXTEND_SIZE: bool = false,
            /// not included in `ALL_ACCESS`
            MAP_EXECUTE_EXPLICIT: bool = false,
            Reserved6: u10 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .SECTION = .{
                    .QUERY = true,
                    .MAP_WRITE = true,
                    .MAP_READ = true,
                    .MAP_EXECUTE = true,
                    .EXTEND_SIZE = true,
                } },
            };
        };

        pub const Session = packed struct(u16) {
            QUERY_ACCESS: bool = false,
            MODIFY_ACCESS: bool = false,
            Reserved2: u14 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .SESSION = .{
                    .QUERY_ACCESS = true,
                    .MODIFY_ACCESS = true,
                } },
            };
        };

        pub const Process = packed struct(u16) {
            TERMINATE: bool = false,
            CREATE_THREAD: bool = false,
            SET_SESSIONID: bool = false,
            VM_OPERATION: bool = false,
            VM_READ: bool = false,
            VM_WRITE: bool = false,
            DUP_HANDLE: bool = false,
            CREATE_PROCESS: bool = false,
            SET_QUOTA: bool = false,
            SET_INFORMATION: bool = false,
            QUERY_INFORMATION: bool = false,
            SUSPEND_RESUME: bool = false,
            QUERY_LIMITED_INFORMATION: bool = false,
            SET_LIMITED_INFORMATION: bool = false,
            Reserved14: u2 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .PROCESS = .{
                    .TERMINATE = true,
                    .CREATE_THREAD = true,
                    .SET_SESSIONID = true,
                    .VM_OPERATION = true,
                    .VM_READ = true,
                    .VM_WRITE = true,
                    .DUP_HANDLE = true,
                    .CREATE_PROCESS = true,
                    .SET_QUOTA = true,
                    .SET_INFORMATION = true,
                    .QUERY_INFORMATION = true,
                    .SUSPEND_RESUME = true,
                    .QUERY_LIMITED_INFORMATION = true,
                    .SET_LIMITED_INFORMATION = true,
                    .Reserved14 = maxInt(@FieldType(Process, "Reserved14")),
                } },
            };
        };

        pub const Thread = packed struct(u16) {
            TERMINATE: bool = false,
            SUSPEND_RESUME: bool = false,
            ALERT: bool = false,
            GET_CONTEXT: bool = false,
            SET_CONTEXT: bool = false,
            SET_INFORMATION: bool = false,
            QUERY_INFORMATION: bool = false,
            SET_THREAD_TOKEN: bool = false,
            IMPERSONATE: bool = false,
            DIRECT_IMPERSONATION: bool = false,
            SET_LIMITED_INFORMATION: bool = false,
            QUERY_LIMITED_INFORMATION: bool = false,
            RESUME: bool = false,
            Reserved13: u3 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .THREAD = .{
                    .TERMINATE = true,
                    .SUSPEND_RESUME = true,
                    .ALERT = true,
                    .GET_CONTEXT = true,
                    .SET_CONTEXT = true,
                    .SET_INFORMATION = true,
                    .QUERY_INFORMATION = true,
                    .SET_THREAD_TOKEN = true,
                    .IMPERSONATE = true,
                    .DIRECT_IMPERSONATION = true,
                    .SET_LIMITED_INFORMATION = true,
                    .QUERY_LIMITED_INFORMATION = true,
                    .RESUME = true,
                    .Reserved13 = maxInt(@FieldType(Thread, "Reserved13")),
                } },
            };
        };

        pub const MemoryPartition = packed struct(u16) {
            QUERY_ACCESS: bool = false,
            MODIFY_ACCESS: bool = false,
            Required2: u14 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .MEMORY_PARTITION = .{
                    .QUERY_ACCESS = true,
                    .MODIFY_ACCESS = true,
                } },
            };
        };

        pub const TransactionManager = packed struct(u16) {
            QUERY_INFORMATION: bool = false,
            SET_INFORMATION: bool = false,
            RECOVER: bool = false,
            RENAME: bool = false,
            CREATE_RM: bool = false,
            /// The following right is intended for DTC's use only; it will be deprecated, and no one else should take a dependency on it.
            BIND_TRANSACTION: bool = false,
            Reserved6: u10 = 0,

            pub const GENERIC_READ: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .READ },
                .SPECIFIC = .{ .TRANSACTIONMANAGER = .{
                    .QUERY_INFORMATION = true,
                } },
            };

            pub const GENERIC_WRITE: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .WRITE },
                .SPECIFIC = .{ .TRANSACTIONMANAGER = .{
                    .SET_INFORMATION = true,
                    .RECOVER = true,
                    .RENAME = true,
                    .CREATE_RM = true,
                } },
            };

            pub const GENERIC_EXECUTE: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .EXECUTE },
                .SPECIFIC = .{ .TRANSACTIONMANAGER = .{} },
            };

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .TRANSACTIONMANAGER = .{
                    .QUERY_INFORMATION = true,
                    .SET_INFORMATION = true,
                    .RECOVER = true,
                    .RENAME = true,
                    .CREATE_RM = true,
                    .BIND_TRANSACTION = true,
                } },
            };
        };

        pub const Transaction = packed struct(u16) {
            QUERY_INFORMATION: bool = false,
            SET_INFORMATION: bool = false,
            ENLIST: bool = false,
            COMMIT: bool = false,
            ROLLBACK: bool = false,
            PROPAGATE: bool = false,
            RIGHT_RESERVED1: bool = false,
            Reserved7: u9 = 0,

            pub const GENERIC_READ: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .READ,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .TRANSACTION = .{
                    .QUERY_INFORMATION = true,
                } },
            };

            pub const GENERIC_WRITE: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .WRITE,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .TRANSACTION = .{
                    .SET_INFORMATION = true,
                    .COMMIT = true,
                    .ENLIST = true,
                    .ROLLBACK = true,
                    .PROPAGATE = true,
                } },
            };

            pub const GENERIC_EXECUTE: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .EXECUTE,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .TRANSACTION = .{
                    .COMMIT = true,
                    .ROLLBACK = true,
                } },
            };

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .TRANSACTION = .{
                    .QUERY_INFORMATION = true,
                    .SET_INFORMATION = true,
                    .COMMIT = true,
                    .ENLIST = true,
                    .ROLLBACK = true,
                    .PROPAGATE = true,
                } },
            };

            pub const RESOURCE_MANAGER_RIGHTS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .{
                        .READ_CONTROL = true,
                    },
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .TRANSACTION = .{
                    .QUERY_INFORMATION = true,
                    .SET_INFORMATION = true,
                    .ENLIST = true,
                    .ROLLBACK = true,
                    .PROPAGATE = true,
                } },
            };
        };

        pub const ResourceManager = packed struct(u16) {
            QUERY_INFORMATION: bool = false,
            SET_INFORMATION: bool = false,
            RECOVER: bool = false,
            ENLIST: bool = false,
            GET_NOTIFICATION: bool = false,
            REGISTER_PROTOCOL: bool = false,
            COMPLETE_PROPAGATION: bool = false,
            Reserved7: u9 = 0,

            pub const GENERIC_READ: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .READ,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .RESOURCEMANAGER = .{
                    .QUERY_INFORMATION = true,
                } },
            };

            pub const GENERIC_WRITE: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .WRITE,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .RESOURCEMANAGER = .{
                    .SET_INFORMATION = true,
                    .RECOVER = true,
                    .ENLIST = true,
                    .GET_NOTIFICATION = true,
                    .REGISTER_PROTOCOL = true,
                    .COMPLETE_PROPAGATION = true,
                } },
            };

            pub const GENERIC_EXECUTE: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .EXECUTE,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .RESOURCEMANAGER = .{
                    .RECOVER = true,
                    .ENLIST = true,
                    .GET_NOTIFICATION = true,
                    .COMPLETE_PROPAGATION = true,
                } },
            };

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .RESOURCEMANAGER = .{
                    .QUERY_INFORMATION = true,
                    .SET_INFORMATION = true,
                    .RECOVER = true,
                    .ENLIST = true,
                    .GET_NOTIFICATION = true,
                    .REGISTER_PROTOCOL = true,
                    .COMPLETE_PROPAGATION = true,
                } },
            };
        };

        pub const Enlistment = packed struct(u16) {
            QUERY_INFORMATION: bool = false,
            SET_INFORMATION: bool = false,
            RECOVER: bool = false,
            SUBORDINATE_RIGHTS: bool = false,
            SUPERIOR_RIGHTS: bool = false,
            Reserved5: u11 = 0,

            pub const GENERIC_READ: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .READ },
                .SPECIFIC = .{ .ENLISTMENT = .{
                    .QUERY_INFORMATION = true,
                } },
            };

            pub const GENERIC_WRITE: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .WRITE },
                .SPECIFIC = .{ .ENLISTMENT = .{
                    .SET_INFORMATION = true,
                    .RECOVER = true,
                    .SUBORDINATE_RIGHTS = true,
                    .SUPERIOR_RIGHTS = true,
                } },
            };

            pub const GENERIC_EXECUTE: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .EXECUTE },
                .SPECIFIC = .{ .ENLISTMENT = .{
                    .RECOVER = true,
                    .SUBORDINATE_RIGHTS = true,
                    .SUPERIOR_RIGHTS = true,
                } },
            };

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .ENLISTMENT = .{
                    .QUERY_INFORMATION = true,
                    .SET_INFORMATION = true,
                    .RECOVER = true,
                    .SUBORDINATE_RIGHTS = true,
                    .SUPERIOR_RIGHTS = true,
                } },
            };
        };

        pub const Event = packed struct(u16) {
            QUERY_STATE: bool = false,
            MODIFY_STATE: bool = false,
            Reserved2: u14 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .EVENT = .{
                    .QUERY_STATE = true,
                    .MODIFY_STATE = true,
                } },
            };
        };

        pub const Semaphore = packed struct(u16) {
            QUERY_STATE: bool = false,
            MODIFY_STATE: bool = false,
            Reserved2: u14 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .SEMAPHORE = .{
                    .QUERY_STATE = true,
                    .MODIFY_STATE = true,
                } },
            };
        };

        pub const Token = packed struct(u16) {
            ASSIGN_PRIMARY: bool = false,
            DUPLICATE: bool = false,
            IMPERSONATE: bool = false,
            QUERY: bool = false,
            QUERY_SOURCE: bool = false,
            ADJUST_PRIVILEGES: bool = false,
            ADJUST_GROUPS: bool = false,
            ADJUST_DEFAULT: bool = false,
            ADJUST_SESSIONID: bool = false,
            Reserved9: u7 = 0,

            pub const ALL_ACCESS_P: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .TOKEN = .{
                    .ASSIGN_PRIMARY = true,
                    .DUPLICATE = true,
                    .IMPERSONATE = true,
                    .QUERY = true,
                    .QUERY_SOURCE = true,
                    .ADJUST_PRIVILEGES = true,
                    .ADJUST_GROUPS = true,
                    .ADJUST_DEFAULT = true,
                } },
            };

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED },
                .SPECIFIC = .{ .TOKEN = .{
                    .ASSIGN_PRIMARY = true,
                    .DUPLICATE = true,
                    .IMPERSONATE = true,
                    .QUERY = true,
                    .QUERY_SOURCE = true,
                    .ADJUST_PRIVILEGES = true,
                    .ADJUST_GROUPS = true,
                    .ADJUST_DEFAULT = true,
                    .ADJUST_SESSIONID = true,
                } },
            };

            pub const READ: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .READ },
                .SPECIFIC = .{ .TOKEN = .{
                    .QUERY = true,
                } },
            };

            pub const WRITE: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .WRITE },
                .SPECIFIC = .{ .TOKEN = .{
                    .ADJUST_PRIVILEGES = true,
                    .ADJUST_GROUPS = true,
                    .ADJUST_DEFAULT = true,
                } },
            };

            pub const EXECUTE: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .EXECUTE },
                .SPECIFIC = .{ .TOKEN = .{} },
            };

            pub const TRUST_CONSTRAINT_MASK: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .READ },
                .SPECIFIC = .{ .TOKEN = .{
                    .QUERY = true,
                    .QUERY_SOURCE = true,
                } },
            };

            pub const TRUST_ALLOWED_MASK: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .READ },
                .SPECIFIC = .{ .TOKEN = .{
                    .QUERY = true,
                    .QUERY_SOURCE = true,
                    .DUPLICATE = true,
                    .IMPERSONATE = true,
                } },
            };
        };

        pub const JobObject = packed struct(u16) {
            ASSIGN_PROCESS: bool = false,
            SET_ATTRIBUTES: bool = false,
            QUERY: bool = false,
            TERMINATE: bool = false,
            SET_SECURITY_ATTRIBUTES: bool = false,
            IMPERSONATE: bool = false,
            Reserved6: u10 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .JOB_OBJECT = .{
                    .ASSIGN_PROCESS = true,
                    .SET_ATTRIBUTES = true,
                    .QUERY = true,
                    .TERMINATE = true,
                    .SET_SECURITY_ATTRIBUTES = true,
                    .IMPERSONATE = true,
                } },
            };
        };

        pub const Mutant = packed struct(u16) {
            QUERY_STATE: bool = false,
            Reserved1: u15 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .MUTANT = .{
                    .QUERY_STATE = true,
                } },
            };
        };

        pub const Timer = packed struct(u16) {
            QUERY_STATE: bool = false,
            MODIFY_STATE: bool = false,
            Reserved2: u14 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{
                    .RIGHTS = .REQUIRED,
                    .SYNCHRONIZE = true,
                },
                .SPECIFIC = .{ .TIMER = .{
                    .QUERY_STATE = true,
                    .MODIFY_STATE = true,
                } },
            };
        };

        pub const IoCompletion = packed struct(u16) {
            Reserved0: u1 = 0,
            MODIFY_STATE: bool = false,
            Reserved2: u14 = 0,

            pub const ALL_ACCESS: ACCESS_MASK = .{
                .STANDARD = .{ .RIGHTS = .REQUIRED, .SYNCHRONIZE = true },
                .SPECIFIC = .{ .IO_COMPLETION = .{
                    .Reserved0 = maxInt(@FieldType(IoCompletion, "Reserved0")),
                    .MODIFY_STATE = true,
                } },
            };
        };

        pub const RIGHTS_ALL: Specific = .{ .bits = maxInt(@FieldType(Specific, "bits")) };
    };

    pub const Standard = packed struct(u5) {
        RIGHTS: Rights = .{},
        SYNCHRONIZE: bool = false,

        pub const RIGHTS_ALL: Standard = .{
            .RIGHTS = .ALL,
            .SYNCHRONIZE = true,
        };

        pub const Rights = packed struct(u4) {
            DELETE: bool = false,
            READ_CONTROL: bool = false,
            WRITE_DAC: bool = false,
            WRITE_OWNER: bool = false,

            pub const REQUIRED: Rights = .{
                .DELETE = true,
                .READ_CONTROL = true,
                .WRITE_DAC = true,
                .WRITE_OWNER = true,
            };

            pub const READ: Rights = .{
                .READ_CONTROL = true,
            };
            pub const WRITE: Rights = .{
                .READ_CONTROL = true,
            };
            pub const EXECUTE: Rights = .{
                .READ_CONTROL = true,
            };

            pub const ALL = REQUIRED;
        };
    };

    pub const Generic = packed struct(u4) {
        ALL: bool = false,
        EXECUTE: bool = false,
        WRITE: bool = false,
        READ: bool = false,
    };
};

pub const DEVICE_TYPE = packed struct(ULONG) {
    FileDevice: CTL_CODE.FILE_DEVICE,
    Reserved16: u16 = 0,
};

pub const FS_INFORMATION_CLASS = enum(c_int) {
    Volume = 1,
    Label = 2,
    Size = 3,
    Device = 4,
    Attribute = 5,
    Control = 6,
    FullSize = 7,
    ObjectId = 8,
    DriverPath = 9,
    VolumeFlags = 10,
    SectorSize = 11,
    DataCopy = 12,
    MetadataSize = 13,
    FullSizeEx = 14,
    Guid = 15,
    _,

    pub const Maximum: @typeInfo(@This()).@"enum".tag_type = 1 + @typeInfo(@This()).@"enum".fields.len;
};

pub const SECTION_INHERIT = enum(c_int) {
    Share = 1,
    Unmap = 2,
};

pub const PAGE = packed struct(ULONG) {
    NOACCESS: bool = false,
    READONLY: bool = false,
    READWRITE: bool = false,
    WRITECOPY: bool = false,

    EXECUTE: bool = false,
    EXECUTE_READ: bool = false,
    EXECUTE_READWRITE: bool = false,
    EXECUTE_WRITECOPY: bool = false,

    GUARD: bool = false,
    NOCACHE: bool = false,
    WRITECOMBINE: bool = false,

    GRAPHICS_NOACCESS: bool = false,
    GRAPHICS_READONLY: bool = false,
    GRAPHICS_READWRITE: bool = false,
    GRAPHICS_EXECUTE: bool = false,
    GRAPHICS_EXECUTE_READ: bool = false,
    GRAPHICS_EXECUTE_READWRITE: bool = false,
    GRAPHICS_COHERENT: bool = false,
    GRAPHICS_NOCACHE: bool = false,

    Reserved19: u12 = 0,

    REVERT_TO_FILE_MAP: bool = false,

    pub fn fromProtection(protection: std.process.MemoryProtection) ?PAGE {
        // TODO https://github.com/ziglang/zig/issues/22214
        return switch (@as(u3, @bitCast(protection))) {
            0b000 => .{ .NOACCESS = true },
            0b001 => .{ .READONLY = true },
            0b010 => null,
            0b011 => .{ .READWRITE = true },
            0b100 => .{ .EXECUTE = true },
            0b101 => .{ .EXECUTE_READ = true },
            0b110 => null,
            0b111 => .{ .EXECUTE_READWRITE = true },
        };
    }
};

pub const MEM = struct {
    pub const ALLOCATE = packed struct(ULONG) {
        Reserved0: u12 = 0,
        COMMIT: bool = false,
        RESERVE: bool = false,
        REPLACE_PLACEHOLDER: bool = false,
        Reserved15: u3 = 0,
        RESERVE_PLACEHOLDER: bool = false,
        RESET: bool = false,
        TOP_DOWN: bool = false,
        WRITE_WATCH: bool = false,
        PHYSICAL: bool = false,
        Reserved23: u1 = 0,
        RESET_UNDO: bool = false,
        Reserved25: u4 = 0,
        LARGE_PAGES: bool = false,
        Reserved30: u1 = 0,
        @"4MB_PAGES": bool = false,

        pub const @"64K_PAGES": ALLOCATE = .{
            .LARGE_PAGES = true,
            .PHYSICAL = true,
        };
    };

    pub const FREE = packed struct(ULONG) {
        COALESCE_PLACEHOLDERS: bool = false,
        PRESERVE_PLACEHOLDER: bool = false,
        Reserved2: u12 = 0,
        DECOMMIT: bool = false,
        RELEASE: bool = false,
        FREE: bool = false,
        Reserved17: u15 = 0,
    };

    pub const MAP = packed struct(ULONG) {
        Reserved0: u13 = 0,
        RESERVE: bool = false,
        REPLACE_PLACEHOLDER: bool = false,
        Reserved15: u14 = 0,
        LARGE_PAGES: bool = false,
        Reserved30: u2 = 0,
    };

    pub const UNMAP = packed struct(ULONG) {
        WITH_TRANSIENT_BOOST: bool = false,
        PRESERVE_PLACEHOLDER: bool = false,
        Reserved2: u30 = 0,
    };

    pub const EXTENDED_PARAMETER = extern struct {
        s: packed struct(ULONG64) {
            Type: TYPE,
            Reserved: u56,
        },
        u: extern union {
            ULong64: ULONG64,
            Pointer: PVOID,
            Size: SIZE_T,
            Handle: HANDLE,
            ULong: ULONG,
        },

        pub const TYPE = enum(u8) {
            InvalidType = 0,
            AddressRequirements,
            NumaNode,
            PartitionHandle,
            UserPhysicalHandle,
            AttributeFlags,
            ImageMachine,
            _,

            pub const Max: @typeInfo(@This()).@"enum".tag_type = @typeInfo(@This()).@"enum".fields.len;
        };
    };
};

pub const SEC = packed struct(ULONG) {
    Reserved0: u17 = 0,
    HUGE_PAGES: bool = false,
    PARTITION_OWNER_HANDLE: bool = false,
    @"64K_PAGES": bool = false,
    Reserved19: u3 = 0,
    FILE: bool = false,
    IMAGE: bool = false,
    PROTECTED_IMAGE: bool = false,
    RESERVE: bool = false,
    COMMIT: bool = false,
    NOCACHE: bool = false,
    Reserved29: u1 = 0,
    WRITECOMBINE: bool = false,
    LARGE_PAGES: bool = false,

    pub const IMAGE_NO_EXECUTE: SEC = .{
        .IMAGE = true,
        .NOCACHE = true,
    };
};

pub const ERESOURCE = opaque {};

// ref: shared/ntdef.h

pub const EVENT_TYPE = enum(c_int) {
    Notification,
    Synchronization,
};

pub const TIMER_TYPE = enum(c_int) {
    Notification,
    Synchronization,
};

pub const WAIT_TYPE = enum(c_int) {
    All,
    Any,
};

pub const LOGICAL = ULONG;

pub const NTSTATUS = @import("windows/ntstatus.zig").NTSTATUS;

// ref: um/heapapi.h

pub fn GetProcessHeap() ?*HEAP {
    return peb().ProcessHeap;
}

// ref none

pub fn GetCurrentProcess() HANDLE {
    const process_pseudo_handle: usize = @bitCast(@as(isize, -1));
    return @ptrFromInt(process_pseudo_handle);
}

pub fn GetCurrentProcessId() DWORD {
    return @truncate(@intFromPtr(teb().ClientId.UniqueProcess));
}

pub fn GetCurrentThread() HANDLE {
    const thread_pseudo_handle: usize = @bitCast(@as(isize, -2));
    return @ptrFromInt(thread_pseudo_handle);
}

pub fn GetCurrentThreadId() DWORD {
    return @truncate(@intFromPtr(teb().ClientId.UniqueThread));
}

pub fn GetLastError() Win32Error {
    return teb().LastErrorValue;
}

pub fn CloseHandle(hObject: HANDLE) void {
    switch (ntdll.NtClose(hObject)) {
        .SUCCESS => {},
        else => |status| unexpectedStatus(status) catch {},
    }
}

pub const CreateProcessError = error{
    FileNotFound,
    AccessDenied,
    InvalidName,
    NameTooLong,
    InvalidExe,
    SystemResources,
    FileBusy,
    Unexpected,
};

pub const CreateProcessFlags = packed struct(u32) {
    debug_process: bool = false,
    debug_only_this_process: bool = false,
    create_suspended: bool = false,
    detached_process: bool = false,
    create_new_console: bool = false,
    normal_priority_class: bool = false,
    idle_priority_class: bool = false,
    high_priority_class: bool = false,
    realtime_priority_class: bool = false,
    create_new_process_group: bool = false,
    create_unicode_environment: bool = false,
    create_separate_wow_vdm: bool = false,
    create_shared_wow_vdm: bool = false,
    create_forcedos: bool = false,
    below_normal_priority_class: bool = false,
    above_normal_priority_class: bool = false,
    inherit_parent_affinity: bool = false,
    inherit_caller_priority: bool = false,
    create_protected_process: bool = false,
    extended_startupinfo_present: bool = false,
    process_mode_background_begin: bool = false,
    process_mode_background_end: bool = false,
    create_secure_process: bool = false,
    _reserved: bool = false,
    create_breakaway_from_job: bool = false,
    create_preserve_code_authz_level: bool = false,
    create_default_error_mode: bool = false,
    create_no_window: bool = false,
    profile_user: bool = false,
    profile_kernel: bool = false,
    profile_server: bool = false,
    create_ignore_system_default: bool = false,
};

pub fn teb() *TEB {
    if (builtin.zig_backend == .stage2_c) return @ptrCast(@alignCast(struct {
        /// This is a workaround for the C backend until zig has the ability to put
        /// C code in inline assembly.
        extern fn zig_windows_teb() callconv(.c) *anyopaque;
    }.zig_windows_teb()));
    switch (native_arch) {
        .thumb => return asm (
            \\ mrc p15, 0, %[ptr], c13, c0, 2
            : [ptr] "=r" (-> *TEB),
        ),
        .aarch64 => return asm (
            \\ mov %[ptr], x18
            : [ptr] "=r" (-> *TEB),
        ),
        .x86 => {
            comptime assert(
                @offsetOf(TEB, "NtTib") + @offsetOf(@FieldType(TEB, "NtTib"), "Self") == 0x18,
            );
            return asm (
                \\ movl %%fs:0x18, %[ptr]
                : [ptr] "=r" (-> *TEB),
            );
        },
        .x86_64 => {
            comptime assert(
                @offsetOf(TEB, "NtTib") + @offsetOf(@FieldType(TEB, "NtTib"), "Self") == 0x30,
            );
            return asm (
                \\ movq %%gs:0x30, %[ptr]
                : [ptr] "=r" (-> *TEB),
            );
        },
        else => @compileError("unsupported arch"),
    }
}

pub fn peb() *PEB {
    if (builtin.zig_backend == .stage2_c) switch (native_arch) {
        .x86, .x86_64 => return @ptrCast(@alignCast(struct {
            /// This is a workaround for the C backend until zig has the ability to put
            /// C code in inline assembly.
            extern fn zig_windows_peb() callconv(.c) *anyopaque;
        }.zig_windows_peb())),
        else => {},
    } else switch (native_arch) {
        .aarch64 => {
            comptime assert(@offsetOf(TEB, "ProcessEnvironmentBlock") == 0x60);
            return asm (
                \\ ldr %[ptr], [x18, #0x60]
                : [ptr] "=r" (-> *PEB),
            );
        },
        .x86 => {
            comptime assert(@offsetOf(TEB, "ProcessEnvironmentBlock") == 0x30);
            return asm (
                \\ movl %%fs:0x30, %[ptr]
                : [ptr] "=r" (-> *PEB),
            );
        },
        .x86_64 => {
            comptime assert(@offsetOf(TEB, "ProcessEnvironmentBlock") == 0x60);
            return asm (
                \\ movq %%gs:0x60, %[ptr]
                : [ptr] "=r" (-> *PEB),
            );
        },
        else => {},
    }
    return teb().ProcessEnvironmentBlock;
}

/// A file time is a 64-bit value that represents the number of 100-nanosecond
/// intervals that have elapsed since 12:00 A.M. January 1, 1601 Coordinated
/// Universal Time (UTC).
/// This function returns the number of nanoseconds since the canonical epoch,
/// which is the POSIX one (Jan 01, 1970 AD).
pub fn fromSysTime(hns: i64) Io.Timestamp {
    const adjusted_epoch: i128 = hns + std.time.epoch.windows * (std.time.ns_per_s / 100);
    return .fromNanoseconds(@intCast(adjusted_epoch * 100));
}

pub fn toSysTime(ns: Io.Timestamp) i64 {
    const hns = @divFloor(ns.nanoseconds, 100);
    return @as(i64, @intCast(hns)) - std.time.epoch.windows * (std.time.ns_per_s / 100);
}

/// Use RtlUpcaseUnicodeChar on Windows when not in comptime to avoid including a
/// redundant copy of the uppercase data.
pub inline fn toUpperWtf16(c: u16) u16 {
    return (if (builtin.os.tag != .windows or @inComptime()) nls.upcaseW else ntdll.RtlUpcaseUnicodeChar)(c);
}

/// Compares two WTF16 strings using the equivalent functionality of
/// `RtlEqualUnicodeString` (with case insensitive comparison enabled).
/// This function can be called on any target.
pub fn eqlIgnoreCaseWtf16(a: []const u16, b: []const u16) bool {
    if (@inComptime() or builtin.os.tag != .windows) {
        // This function compares the strings code unit by code unit (aka u16-to-u16),
        // so any length difference implies inequality. In other words, there's no possible
        // conversion that changes the number of WTF-16 code units needed for the uppercase/lowercase
        // version in the conversion table since only codepoints <= max(u16) are eligible
        // for conversion at all.
        if (a.len != b.len) return false;

        for (a, b) |a_c, b_c| {
            // The slices are always WTF-16 LE, so need to convert the elements to native
            // endianness for the uppercasing
            const a_c_native = std.mem.littleToNative(u16, a_c);
            const b_c_native = std.mem.littleToNative(u16, b_c);
            if (a_c != b_c and toUpperWtf16(a_c_native) != toUpperWtf16(b_c_native)) {
                return false;
            }
        }
        return true;
    }
    // Use RtlEqualUnicodeString on Windows when not in comptime to avoid including a
    // redundant copy of the uppercase data.
    return ntdll.RtlEqualUnicodeString(&.init(a), &.init(b), .TRUE).toBool();
}

/// Compares two WTF-8 strings using the equivalent functionality of
/// `RtlEqualUnicodeString` (with case insensitive comparison enabled).
/// This function can be called on any target.
/// Assumes `a` and `b` are valid WTF-8.
pub fn eqlIgnoreCaseWtf8(a: []const u8, b: []const u8) bool {
    // A length equality check is not possible here because there are
    // some codepoints that have a different length uppercase UTF-8 representations
    // than their lowercase counterparts, e.g. U+0250 (2 bytes) <-> U+2C6F (3 bytes).
    // There are 7 such codepoints in the uppercase data used by Windows.

    var a_wtf8_it = std.unicode.Wtf8View.initUnchecked(a).iterator();
    var b_wtf8_it = std.unicode.Wtf8View.initUnchecked(b).iterator();

    while (true) {
        const a_cp = a_wtf8_it.nextCodepoint() orelse break;
        const b_cp = b_wtf8_it.nextCodepoint() orelse return false;

        if (a_cp <= maxInt(u16) and b_cp <= maxInt(u16)) {
            if (a_cp != b_cp and toUpperWtf16(@intCast(a_cp)) != toUpperWtf16(@intCast(b_cp))) {
                return false;
            }
        } else if (a_cp != b_cp) {
            return false;
        }
    }
    // Make sure there are no leftover codepoints in b
    if (b_wtf8_it.nextCodepoint() != null) return false;

    return true;
}

fn testEqlIgnoreCase(comptime expect_eql: bool, comptime a: []const u8, comptime b: []const u8) !void {
    try std.testing.expectEqual(expect_eql, eqlIgnoreCaseWtf8(a, b));
    try std.testing.expectEqual(expect_eql, eqlIgnoreCaseWtf16(
        std.unicode.utf8ToUtf16LeStringLiteral(a),
        std.unicode.utf8ToUtf16LeStringLiteral(b),
    ));

    try comptime std.testing.expect(expect_eql == eqlIgnoreCaseWtf8(a, b));
    try comptime std.testing.expect(expect_eql == eqlIgnoreCaseWtf16(
        std.unicode.utf8ToUtf16LeStringLiteral(a),
        std.unicode.utf8ToUtf16LeStringLiteral(b),
    ));
}

test "eqlIgnoreCaseWtf16/Wtf8" {
    try testEqlIgnoreCase(true, "\x01 a B Λ ɐ", "\x01 A b λ Ɐ");
    // does not do case-insensitive comparison for codepoints >= U+10000
    try testEqlIgnoreCase(false, "𐓏", "𐓷");
}

/// The error type for `removeDotDirsSanitized`
pub const RemoveDotDirsError = error{TooManyParentDirs};

/// Removes '.' and '..' path components from a "sanitized relative path".
/// A "sanitized path" is one where:
///    1) all forward slashes have been replaced with back slashes
///    2) all repeating back slashes have been collapsed
///    3) the path is a relative one (does not start with a back slash)
pub fn removeDotDirsSanitized(comptime T: type, path: []T) RemoveDotDirsError!usize {
    assert(path.len == 0 or path[0] != '\\');

    var write_idx: usize = 0;
    var read_idx: usize = 0;
    while (read_idx < path.len) {
        if (path[read_idx] == '.') {
            if (read_idx + 1 == path.len)
                return write_idx;

            const after_dot = path[read_idx + 1];
            if (after_dot == '\\') {
                read_idx += 2;
                continue;
            }
            if (after_dot == '.' and (read_idx + 2 == path.len or path[read_idx + 2] == '\\')) {
                if (write_idx == 0) return error.TooManyParentDirs;
                assert(write_idx >= 2);
                write_idx -= 1;
                while (true) {
                    write_idx -= 1;
                    if (write_idx == 0) break;
                    if (path[write_idx] == '\\') {
                        write_idx += 1;
                        break;
                    }
                }
                if (read_idx + 2 == path.len)
                    return write_idx;
                read_idx += 3;
                continue;
            }
        }

        // skip to the next path separator
        while (true) : (read_idx += 1) {
            if (read_idx == path.len)
                return write_idx;
            path[write_idx] = path[read_idx];
            write_idx += 1;
            if (path[read_idx] == '\\')
                break;
        }
        read_idx += 1;
    }
    return write_idx;
}

/// Normalizes a Windows path with the following steps:
///     1) convert all forward slashes to back slashes
///     2) collapse duplicate back slashes
///     3) remove '.' and '..' directory parts
/// Returns the length of the new path.
pub fn normalizePath(comptime T: type, path: []T) RemoveDotDirsError!usize {
    mem.replaceScalar(T, path, '/', '\\');
    const new_len = mem.collapseRepeatsLen(T, path, '\\');

    const prefix_len: usize = init: {
        if (new_len >= 1 and path[0] == '\\') break :init 1;
        if (new_len >= 2 and path[1] == ':')
            break :init if (new_len >= 3 and path[2] == '\\') @as(usize, 3) else @as(usize, 2);
        break :init 0;
    };

    return prefix_len + try removeDotDirsSanitized(T, path[prefix_len..new_len]);
}

/// Returns true if the path starts with `\??\`, which is indicative of an NT path
/// but is not enough to fully distinguish between NT paths and Win32 paths, as
/// `\??\` is not actually a distinct prefix but rather the path to a special virtual
/// folder in the Object Manager.
///
/// For example, `\Device\HarddiskVolume2` and `\DosDevices\C:` are also NT paths but
/// cannot be distinguished as such by their prefix.
///
/// So, inferring whether a path is an NT path or a Win32 path is usually a mistake;
/// that information should instead be known ahead-of-time.
///
/// If `T` is `u16`, then `path` should be encoded as WTF-16LE.
pub fn hasCommonNtPrefix(comptime T: type, path: []const T) bool {
    // Must be exactly \??\, forward slashes are not allowed
    const expected_wtf8_prefix = "\\??\\";
    const expected_prefix = switch (T) {
        u8 => expected_wtf8_prefix,
        u16 => std.unicode.wtf8ToWtf16LeStringLiteral(expected_wtf8_prefix),
        else => @compileError("unsupported type: " ++ @typeName(T)),
    };
    return mem.startsWith(T, path, expected_prefix);
}

/// Similar to `RtlNtPathNameToDosPathName` but does not do any heap allocation.
/// The possible transformations are:
///   \??\C:\Some\Path -> C:\Some\Path
///   \??\UNC\server\share\foo -> \\server\share\foo
/// If the path does not have the NT namespace prefix, then `error.NotNtPath` is returned.
///
/// Functionality is based on the ReactOS test cases found here:
/// https://github.com/reactos/reactos/blob/master/modules/rostests/apitests/ntdll/RtlNtPathNameToDosPathName.c
///
/// `path` should be encoded as WTF-16LE.
///
/// Supports in-place modification (`path` and `out` may refer to the same slice).
pub fn ntToWin32Namespace(path: []const u16, out: []u16) error{ NameTooLong, NotNtPath }![]u16 {
    if (path.len > PATH_MAX_WIDE) return error.NameTooLong;
    if (!hasCommonNtPrefix(u16, path)) return error.NotNtPath;

    var dest_index: usize = 0;
    var after_prefix = path[4..]; // after the `\??\`
    // The prefix \??\UNC\ means this is a UNC path, in which case the
    // `\??\UNC\` should be replaced by `\\` (two backslashes)
    const is_unc = after_prefix.len >= 4 and
        eqlIgnoreCaseWtf16(after_prefix[0..3], std.unicode.utf8ToUtf16LeStringLiteral("UNC")) and
        std.fs.path.PathType.windows.isSep(u16, after_prefix[3]);
    const win32_len = path.len - @as(usize, if (is_unc) 6 else 4);
    if (out.len < win32_len) return error.NameTooLong;
    if (is_unc) {
        out[0] = comptime std.mem.nativeToLittle(u16, '\\');
        dest_index += 1;
        // We want to include the last `\` of `\??\UNC\`
        after_prefix = path[7..];
    }
    @memmove(out[dest_index..][0..after_prefix.len], after_prefix);
    return out[0..win32_len];
}

test ntToWin32Namespace {
    const L = std.unicode.utf8ToUtf16LeStringLiteral;

    var mutable_unc_path_buf = L("\\??\\UNC\\path1\\path2").*;
    try std.testing.expectEqualSlices(u16, L("\\\\path1\\path2"), try ntToWin32Namespace(&mutable_unc_path_buf, &mutable_unc_path_buf));

    var mutable_path_buf = L("\\??\\C:\\test\\").*;
    try std.testing.expectEqualSlices(u16, L("C:\\test\\"), try ntToWin32Namespace(&mutable_path_buf, &mutable_path_buf));

    var too_small_buf: [6]u16 = undefined;
    try std.testing.expectError(error.NameTooLong, ntToWin32Namespace(L("\\??\\C:\\test"), &too_small_buf));
}

inline fn MAKELANGID(p: c_ushort, s: c_ushort) LANGID {
    return (s << 10) | p;
}

/// Call this when you made a windows DLL call or something that does SetLastError
/// and you get an unexpected error.
pub fn unexpectedError(err: Win32Error) UnexpectedError {
    @branchHint(.cold);
    if (std.options.unexpected_error_tracing) {
        std.debug.print("error.Unexpected: GetLastError({d}): {t}\n", .{ err, err });
        std.debug.dumpCurrentStackTrace(.{ .first_address = @returnAddress() });
    }
    return error.Unexpected;
}

/// Call this when you made a windows NtDll call
/// and you get an unexpected status.
pub fn unexpectedStatus(status: NTSTATUS) UnexpectedError {
    if (std.options.unexpected_error_tracing) {
        std.debug.print("error.Unexpected NTSTATUS=0x{x} ({s})\n", .{
            @intFromEnum(status),
            std.enums.tagName(NTSTATUS, status) orelse "<unnamed>",
        });
        std.debug.dumpCurrentStackTrace(.{ .first_address = @returnAddress() });
    }
    return error.Unexpected;
}

pub fn statusBug(status: NTSTATUS) UnexpectedError {
    switch (builtin.mode) {
        .Debug => std.debug.panic("programmer bug caused syscall status: 0x{x} ({s})", .{
            @intFromEnum(status),
            std.enums.tagName(NTSTATUS, status) orelse "<unnamed>",
        }),
        else => return error.Unexpected,
    }
}

pub fn errorBug(err: Win32Error) UnexpectedError {
    switch (builtin.mode) {
        .Debug => std.debug.panic("programmer bug caused syscall error: 0x{x} ({s})", .{
            @intFromEnum(err),
            std.enums.tagName(Win32Error, err) orelse "<unnamed>",
        }),
        else => return error.Unexpected,
    }
}

pub const Win32Error = @import("windows/win32error.zig").Win32Error;
pub const LANG = @import("windows/lang.zig");
pub const SUBLANG = @import("windows/sublang.zig");

pub const BOOL = Bool(c_int);
pub const BOOLEAN = Bool(BYTE);
pub const BYTE = u8;
pub const CHAR = u8;
pub const UCHAR = u8;
pub const FLOAT = f32;
pub const HANDLE = *anyopaque;
pub const HCRYPTPROV = ULONG_PTR;
pub const ATOM = u16;
pub const HBRUSH = *opaque {};
pub const HCURSOR = *opaque {};
pub const HICON = *opaque {};
pub const HINSTANCE = *opaque {};
pub const HMENU = *opaque {};
pub const HMODULE = *opaque {};
pub const HWND = *opaque {};
pub const HDC = *opaque {};
pub const HGLRC = *opaque {};
pub const FARPROC = *opaque {};
pub const PROC = *opaque {};
pub const INT = c_int;
pub const LPCSTR = [*:0]const CHAR;
pub const LPCVOID = *const anyopaque;
pub const LPSTR = [*:0]CHAR;
pub const LPVOID = *anyopaque;
pub const LPWSTR = [*:0]WCHAR;
pub const LPCWSTR = [*:0]const WCHAR;
pub const PVOID = *anyopaque;
pub const PWSTR = [*:0]WCHAR;
pub const PCWSTR = [*:0]const WCHAR;
/// Allocated by SysAllocString, freed by SysFreeString
pub const BSTR = [*:0]WCHAR;
pub const SIZE_T = usize;
pub const UINT = c_uint;
pub const ULONG_PTR = usize;
pub const LONG_PTR = isize;
pub const DWORD_PTR = ULONG_PTR;
pub const WCHAR = u16;
pub const WORD = u16;
pub const DWORD = u32;
pub const DWORD64 = u64;
pub const LARGE_INTEGER = i64;
pub const ULARGE_INTEGER = u64;
pub const USHORT = u16;
pub const SHORT = i16;
pub const ULONG = u32;
pub const LONG = i32;
pub const ULONG64 = u64;
pub const ULONGLONG = u64;
pub const LONGLONG = i64;
pub const LANGID = c_ushort;
pub const COLORREF = DWORD;

pub const LPARAM = LONG_PTR;

pub const va_list = *opaque {};

pub const TCHAR = @compileError("Deprecated: choose between `CHAR` or `WCHAR` directly instead.");
pub const LPTSTR = @compileError("Deprecated: choose between `LPSTR` or `LPWSTR` directly instead.");
pub const LPCTSTR = @compileError("Deprecated: choose between `LPCSTR` or `LPCWSTR` directly instead.");
pub const PTSTR = @compileError("Deprecated: choose between `PSTR` or `PWSTR` directly instead.");
pub const PCTSTR = @compileError("Deprecated: choose between `PCSTR` or `PCWSTR` directly instead.");

fn STRING(comptime C: type) type {
    return extern struct {
        Length: USHORT,
        MaximumLength: USHORT,
        Buffer: ?[*]C,

        pub const empty: @This() = .{ .Length = 0, .MaximumLength = 0, .Buffer = null };

        pub fn init(string: []const C) @This() {
            const len: USHORT = @intCast(@sizeOf(C) * string.len);
            return .{
                .Length = len,
                .MaximumLength = len,
                .Buffer = @constCast(string.ptr),
            };
        }

        pub fn initZ(string: [:0]const C) @This() {
            const len: USHORT = @intCast(@sizeOf(C) * string.len);
            return .{
                .Length = len,
                .MaximumLength = len + @sizeOf(C),
                .Buffer = @constCast(string.ptr),
            };
        }

        pub fn isEmpty(string: *const @This()) bool {
            return string.Length == 0;
        }

        pub fn slice(string: *const @This()) []C {
            return if (string.isEmpty()) &.{} else string.Buffer.?[0..@divExact(string.Length, @sizeOf(C))];
        }

        pub fn sliceZ(string: *const @This()) [:0]C {
            assert(string.Length + @sizeOf(C) <= string.MaximumLength);
            return string.Buffer.?[0..@divExact(string.Length, @sizeOf(C)) :0];
        }
    };
}
pub const ANSI_STRING = STRING(CHAR);
pub const UNICODE_STRING = STRING(WCHAR);

fn Bool(comptime BackingInteger: type) type {
    return enum(Backing) {
        /// false
        FALSE = 0,
        /// true
        _,

        /// This is not the only truthy value, comparisons against this value are always a bug.
        pub const TRUE: @This() = @enumFromInt(1);

        pub const Backing = BackingInteger;

        pub fn toBool(b: @This()) bool {
            return b != .FALSE;
        }

        pub fn fromBool(b: bool) @This() {
            return @enumFromInt(@intFromBool(b));
        }
    };
}

pub const INVALID_HANDLE_VALUE: HANDLE = @ptrFromInt(maxInt(usize));

pub const INVALID_FILE_ATTRIBUTES: DWORD = maxInt(DWORD);

pub const IO_STATUS_BLOCK = extern struct {
    // "DUMMYUNIONNAME" expands to "u"
    u: extern union {
        Status: NTSTATUS,
        Pointer: ?*anyopaque,
    },
    Information: ULONG_PTR,
};

pub const MAX_PATH = 260;

pub const SECURITY_ATTRIBUTES = extern struct {
    nLength: DWORD,
    lpSecurityDescriptor: ?*anyopaque,
    bInheritHandle: BOOL,
};

pub const STARTUPINFOW = extern struct {
    cb: DWORD,
    lpReserved: ?LPWSTR,
    lpDesktop: ?LPWSTR,
    lpTitle: ?LPWSTR,
    dwX: DWORD,
    dwY: DWORD,
    dwXSize: DWORD,
    dwYSize: DWORD,
    dwXCountChars: DWORD,
    dwYCountChars: DWORD,
    dwFillAttribute: DWORD,
    dwFlags: DWORD,
    wShowWindow: WORD,
    cbReserved2: WORD,
    lpReserved2: ?*BYTE,
    hStdInput: ?HANDLE,
    hStdOutput: ?HANDLE,
    hStdError: ?HANDLE,
};

pub const STARTF_FORCEONFEEDBACK = 0x00000040;
pub const STARTF_FORCEOFFFEEDBACK = 0x00000080;
pub const STARTF_PREVENTPINNING = 0x00002000;
pub const STARTF_RUNFULLSCREEN = 0x00000020;
pub const STARTF_TITLEISAPPID = 0x00001000;
pub const STARTF_TITLEISLINKNAME = 0x00000800;
pub const STARTF_UNTRUSTEDSOURCE = 0x00008000;
pub const STARTF_USECOUNTCHARS = 0x00000008;
pub const STARTF_USEFILLATTRIBUTE = 0x00000010;
pub const STARTF_USEHOTKEY = 0x00000200;
pub const STARTF_USEPOSITION = 0x00000004;
pub const STARTF_USESHOWWINDOW = 0x00000001;
pub const STARTF_USESIZE = 0x00000002;
pub const STARTF_USESTDHANDLES = 0x00000100;

pub const THREAD_START_ROUTINE = fn (LPVOID) callconv(.winapi) DWORD;
pub const USER_THREAD_START_ROUTINE = fn (LPVOID) callconv(.winapi) NTSTATUS;

pub const FILETIME = extern struct {
    dwLowDateTime: DWORD,
    dwHighDateTime: DWORD,
};

pub const GUID = extern struct {
    Data1: u32,
    Data2: u16,
    Data3: u16,
    Data4: [8]u8,

    const hex_offsets = switch (builtin.target.cpu.arch.endian()) {
        .big => [16]u6{
            0,  2,  4,  6,
            9,  11, 14, 16,
            19, 21, 24, 26,
            28, 30, 32, 34,
        },
        .little => [16]u6{
            6,  4,  2,  0,
            11, 9,  16, 14,
            19, 21, 24, 26,
            28, 30, 32, 34,
        },
    };

    pub fn parse(s: []const u8) GUID {
        assert(s[0] == '{');
        assert(s[37] == '}');
        return parseNoBraces(s[1 .. s.len - 1]) catch @panic("invalid GUID string");
    }

    pub fn parseNoBraces(s: []const u8) !GUID {
        assert(s.len == 36);
        assert(s[8] == '-');
        assert(s[13] == '-');
        assert(s[18] == '-');
        assert(s[23] == '-');
        var bytes: [16]u8 = undefined;
        for (hex_offsets, 0..) |hex_offset, i| {
            bytes[i] = (try std.fmt.charToDigit(s[hex_offset], 16)) << 4 |
                try std.fmt.charToDigit(s[hex_offset + 1], 16);
        }
        return @as(GUID, @bitCast(bytes));
    }

    pub fn format(self: GUID, w: *std.Io.Writer) std.Io.Writer.Error!void {
        return w.print("{{{x:0>8}-{x:0>4}-{x:0>4}-{x}-{x}}}", .{
            self.Data1,
            self.Data2,
            self.Data3,
            self.Data4[0..2],
            self.Data4[2..8],
        });
    }
};

test GUID {
    try std.testing.expectEqual(
        GUID{
            .Data1 = 0x01234567,
            .Data2 = 0x89ab,
            .Data3 = 0xef10,
            .Data4 = "\x32\x54\x76\x98\xba\xdc\xfe\x91".*,
        },
        GUID.parse("{01234567-89AB-EF10-3254-7698badcfe91}"),
    );
    try std.testing.expectFmt(
        "{01234567-89ab-ef10-3254-7698badcfe91}",
        "{f}",
        .{GUID.parse("{01234567-89AB-EF10-3254-7698badcfe91}")},
    );
    try std.testing.expectFmt(
        "{00000001-0001-0001-0001-000000000001}",
        "{f}",
        .{GUID{ .Data1 = 1, .Data2 = 1, .Data3 = 1, .Data4 = [_]u8{ 0, 1, 0, 0, 0, 0, 0, 1 } }},
    );
}

pub const COORD = extern struct {
    X: SHORT,
    Y: SHORT,
};

pub const TLS_OUT_OF_INDEXES = 4294967295;
pub const IMAGE_TLS_DIRECTORY = extern struct {
    StartAddressOfRawData: usize,
    EndAddressOfRawData: usize,
    AddressOfIndex: usize,
    AddressOfCallBacks: usize,
    SizeOfZeroFill: u32,
    Characteristics: u32,
};
pub const IMAGE_TLS_DIRECTORY64 = IMAGE_TLS_DIRECTORY;
pub const IMAGE_TLS_DIRECTORY32 = IMAGE_TLS_DIRECTORY;

pub const PIMAGE_TLS_CALLBACK = ?*const fn (PVOID, DWORD, PVOID) callconv(.winapi) void;

pub const REGSAM = ACCESS_MASK;
pub const LSTATUS = LONG;

pub const HKEY = *opaque {};

pub const HKEY_CLASSES_ROOT: HKEY = @ptrFromInt(0x80000000);
pub const HKEY_CURRENT_USER: HKEY = @ptrFromInt(0x80000001);
pub const HKEY_LOCAL_MACHINE: HKEY = @ptrFromInt(0x80000002);
pub const HKEY_USERS: HKEY = @ptrFromInt(0x80000003);
pub const HKEY_PERFORMANCE_DATA: HKEY = @ptrFromInt(0x80000004);
pub const HKEY_PERFORMANCE_TEXT: HKEY = @ptrFromInt(0x80000050);
pub const HKEY_PERFORMANCE_NLSTEXT: HKEY = @ptrFromInt(0x80000060);
pub const HKEY_CURRENT_CONFIG: HKEY = @ptrFromInt(0x80000005);
pub const HKEY_DYN_DATA: HKEY = @ptrFromInt(0x80000006);
pub const HKEY_CURRENT_USER_LOCAL_SETTINGS: HKEY = @ptrFromInt(0x80000007);

pub const RTL_QUERY_REGISTRY_TABLE = extern struct {
    QueryRoutine: RTL_QUERY_REGISTRY_ROUTINE,
    Flags: ULONG,
    Name: ?PWSTR,
    EntryContext: ?*anyopaque,
    DefaultType: REG.ValueType,
    DefaultData: ?*anyopaque,
    DefaultLength: ULONG,
};

pub const RTL_QUERY_REGISTRY_ROUTINE = ?*const fn (
    PWSTR,
    ULONG,
    ?*anyopaque,
    ULONG,
    ?*anyopaque,
    ?*anyopaque,
) callconv(.winapi) NTSTATUS;

/// Path is a full path
pub const RTL_REGISTRY_ABSOLUTE = 0;
/// \Registry\Machine\System\CurrentControlSet\Services
pub const RTL_REGISTRY_SERVICES = 1;
/// \Registry\Machine\System\CurrentControlSet\Control
pub const RTL_REGISTRY_CONTROL = 2;
/// \Registry\Machine\Software\Microsoft\Windows NT\CurrentVersion
pub const RTL_REGISTRY_WINDOWS_NT = 3;
/// \Registry\Machine\Hardware\DeviceMap
pub const RTL_REGISTRY_DEVICEMAP = 4;
/// \Registry\User\CurrentUser
pub const RTL_REGISTRY_USER = 5;
pub const RTL_REGISTRY_MAXIMUM = 6;

/// Low order bits are registry handle
pub const RTL_REGISTRY_HANDLE = 0x40000000;
/// Indicates the key node is optional
pub const RTL_REGISTRY_OPTIONAL = 0x80000000;

/// Name is a subkey and remainder of table or until next subkey are value
/// names for that subkey to look at.
pub const RTL_QUERY_REGISTRY_SUBKEY = 0x00000001;

/// Reset current key to original key for this and all following table entries.
pub const RTL_QUERY_REGISTRY_TOPKEY = 0x00000002;

/// Fail if no match found for this table entry.
pub const RTL_QUERY_REGISTRY_REQUIRED = 0x00000004;

/// Used to mark a table entry that has no value name, just wants a call out, not
/// an enumeration of all values.
pub const RTL_QUERY_REGISTRY_NOVALUE = 0x00000008;

/// Used to suppress the expansion of REG_MULTI_SZ into multiple callouts or
/// to prevent the expansion of environment variable values in REG_EXPAND_SZ.
pub const RTL_QUERY_REGISTRY_NOEXPAND = 0x00000010;

/// QueryRoutine field ignored.  EntryContext field points to location to store value.
/// For null terminated strings, EntryContext points to UNICODE_STRING structure that
/// that describes maximum size of buffer. If .Buffer field is NULL then a buffer is
/// allocated.
pub const RTL_QUERY_REGISTRY_DIRECT = 0x00000020;

/// Used to delete value keys after they are queried.
pub const RTL_QUERY_REGISTRY_DELETE = 0x00000040;

/// Use this flag with the RTL_QUERY_REGISTRY_DIRECT flag to verify that the REG_XXX type
/// of the stored registry value matches the type expected by the caller.
/// If the types do not match, the call fails.
pub const RTL_QUERY_REGISTRY_TYPECHECK = 0x00000100;

/// REG_ is a crowded namespace with a lot of overlapping and unrelated
/// defines in the Windows headers, so instead of strictly following the
/// Windows headers names, extra namespaces are added here for clarity.
pub const REG = struct {
    pub const ValueType = enum(ULONG) {
        /// No value type
        NONE = 0,
        /// Unicode nul terminated string
        SZ = 1,
        /// Unicode nul terminated string (with environment variable references)
        EXPAND_SZ = 2,
        /// Free form binary
        BINARY = 3,
        /// 32-bit number
        DWORD = 4,
        /// 32-bit number
        DWORD_BIG_ENDIAN = 5,
        /// Symbolic Link (unicode)
        LINK = 6,
        /// Multiple Unicode strings
        MULTI_SZ = 7,
        /// Resource list in the resource map
        RESOURCE_LIST = 8,
        /// Resource list in the hardware description
        FULL_RESOURCE_DESCRIPTOR = 9,
        RESOURCE_REQUIREMENTS_LIST = 10,
        /// 64-bit number
        QWORD = 11,
        _,

        /// 32-bit number (same as REG_DWORD)
        pub const DWORD_LITTLE_ENDIAN: ValueType = .DWORD;
        /// 64-bit number (same as REG_QWORD)
        pub const QWORD_LITTLE_ENDIAN: ValueType = .QWORD;
    };

    /// Used with NtOpenKeyEx, maybe others
    pub const OpenOptions = packed struct(ULONG) {
        Reserved0: u2 = 0,
        /// Open for backup or restore
        /// special access rules privilege required
        BACKUP_RESTORE: bool = false,
        /// Open symbolic link
        OPEN_LINK: bool = false,
        Reserved3: u28 = 0,
    };

    /// Used with NtLoadKeyEx, maybe others
    pub const LoadOptions = packed struct(ULONG) {
        /// Restore whole hive volatile
        WHOLE_HIVE_VOLATILE: bool = false,
        /// Unwind changes to last flush
        REFRESH_HIVE: bool = false,
        /// Never lazy flush this hive
        NO_LAZY_FLUSH: bool = false,
        /// Force the restore process even when we have open handles on subkeys
        FORCE_RESTORE: bool = false,
        /// Loads the hive visible to the calling process
        APP_HIVE: bool = false,
        /// Hive cannot be mounted by any other process while in use
        PROCESS_PRIVATE: bool = false,
        /// Starts Hive Journal
        START_JOURNAL: bool = false,
        /// Grow hive file in exact 4k increments
        HIVE_EXACT_FILE_GROWTH: bool = false,
        /// No RM is started for this hive (no transactions)
        HIVE_NO_RM: bool = false,
        /// Legacy single logging is used for this hive
        HIVE_SINGLE_LOG: bool = false,
        /// This hive might be used by the OS loader
        BOOT_HIVE: bool = false,
        /// Load the hive and return a handle to its root kcb
        LOAD_HIVE_OPEN_HANDLE: bool = false,
        /// Flush changes to primary hive file size as part of all flushes
        FLUSH_HIVE_FILE_GROWTH: bool = false,
        /// Open a hive's files in read-only mode
        /// The same flag is used for REG_APP_HIVE_OPEN_READ_ONLY:
        /// Open an app hive's files in read-only mode (if the hive was not previously loaded).
        OPEN_READ_ONLY: bool = false,
        /// Load the hive, but don't allow any modification of it
        IMMUTABLE: bool = false,
        /// Do not fall back to impersonating the caller if hive file access fails
        NO_IMPERSONATION_FALLBACK: bool = false,
        Reserved16: u16 = 0,
    };
};

pub const KEY = struct {
    pub const VALUE = struct {
        /// https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/ne-wdm-_key_value_information_class
        pub const INFORMATION_CLASS = enum(c_int) {
            Basic = 0,
            Full = 1,
            Partial = 2,
            FullAlign64 = 3,
            PartialAlign64 = 4,
            Layer = 5,
            _,

            pub const Max: @typeInfo(@This()).@"enum".tag_type = @typeInfo(@This()).@"enum".fields.len;
        };

        pub const PARTIAL_INFORMATION = extern struct {
            TitleIndex: ULONG,
            Type: REG.ValueType,
            DataLength: ULONG,
            Data: [0]UCHAR,

            pub fn data(info: *const PARTIAL_INFORMATION) []const UCHAR {
                const ptr: [*]const UCHAR = @ptrCast(&info.Data);
                return ptr[0..info.DataLength];
            }
        };
    };
};

pub const ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x4;
pub const DISABLE_NEWLINE_AUTO_RETURN = 0x8;

pub const FOREGROUND_BLUE = 0x0001;
pub const FOREGROUND_GREEN = 0x0002;
pub const FOREGROUND_RED = 0x0004;
pub const FOREGROUND_INTENSITY = 0x0008;
pub const BACKGROUND_BLUE = 0x0010;
pub const BACKGROUND_GREEN = 0x0020;
pub const BACKGROUND_RED = 0x0040;
pub const BACKGROUND_INTENSITY = 0x0080;

pub const LIST_ENTRY = extern struct {
    Flink: *LIST_ENTRY,
    Blink: *LIST_ENTRY,
};

pub const RTL_CRITICAL_SECTION_DEBUG = extern struct {
    Type: WORD,
    CreatorBackTraceIndex: WORD,
    CriticalSection: *RTL_CRITICAL_SECTION,
    ProcessLocksList: LIST_ENTRY,
    EntryCount: DWORD,
    ContentionCount: DWORD,
    Flags: DWORD,
    CreatorBackTraceIndexHigh: WORD,
    SpareWORD: WORD,
};

pub const RTL_CRITICAL_SECTION = extern struct {
    DebugInfo: *RTL_CRITICAL_SECTION_DEBUG,
    LockCount: LONG,
    RecursionCount: LONG,
    OwningThread: HANDLE,
    LockSemaphore: HANDLE,
    SpinCount: ULONG_PTR,
};

pub const CRITICAL_SECTION = RTL_CRITICAL_SECTION;
pub const INIT_ONCE = RTL_RUN_ONCE;
pub const INIT_ONCE_STATIC_INIT = RTL_RUN_ONCE_INIT;
pub const INIT_ONCE_FN = *const fn (InitOnce: *INIT_ONCE, Parameter: ?*anyopaque, Context: ?*anyopaque) callconv(.winapi) BOOL;

pub const RTL_RUN_ONCE = extern struct {
    Ptr: ?*anyopaque,
};

pub const RTL_RUN_ONCE_INIT = RTL_RUN_ONCE{ .Ptr = null };

/// > The maximum path of 32,767 characters is approximate, because the "\\?\"
/// > prefix may be expanded to a longer string by the system at run time, and
/// > this expansion applies to the total length.
/// from https://docs.microsoft.com/en-us/windows/desktop/FileIO/naming-a-file#maximum-path-length-limitation
pub const PATH_MAX_WIDE = 32767;

/// > [Each file name component can be] up to the value returned in the
/// > lpMaximumComponentLength parameter of the GetVolumeInformation function
/// > (this value is commonly 255 characters)
/// from https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation
///
/// > The value that is stored in the variable that *lpMaximumComponentLength points to is
/// > used to indicate that a specified file system supports long names. For example, for
/// > a FAT file system that supports long names, the function stores the value 255, rather
/// > than the previous 8.3 indicator. Long names can also be supported on systems that use
/// > the NTFS file system.
/// from https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getvolumeinformationw
///
/// The assumption being made here is that while lpMaximumComponentLength may vary, it will never
/// be larger than 255.
///
/// TODO: More verification of this assumption.
pub const NAME_MAX = 255;

pub const EXCEPTION_DATATYPE_MISALIGNMENT = 0x80000002;
pub const EXCEPTION_ACCESS_VIOLATION = 0xc0000005;
pub const EXCEPTION_ILLEGAL_INSTRUCTION = 0xc000001d;
pub const EXCEPTION_STACK_OVERFLOW = 0xc00000fd;
pub const EXCEPTION_CONTINUE_SEARCH = 0;

pub const EXCEPTION_RECORD = extern struct {
    ExceptionCode: u32,
    ExceptionFlags: u32,
    ExceptionRecord: *EXCEPTION_RECORD,
    ExceptionAddress: *anyopaque,
    NumberParameters: u32,
    ExceptionInformation: [15]usize,
};

pub const FLOATING_SAVE_AREA = switch (native_arch) {
    .x86 => extern struct {
        ControlWord: DWORD,
        StatusWord: DWORD,
        TagWord: DWORD,
        ErrorOffset: DWORD,
        ErrorSelector: DWORD,
        DataOffset: DWORD,
        DataSelector: DWORD,
        RegisterArea: [80]BYTE,
        Cr0NpxState: DWORD,
    },
    else => @compileError("FLOATING_SAVE_AREA only defined on x86"),
};

pub const M128A = switch (native_arch) {
    .x86_64 => extern struct {
        Low: ULONGLONG,
        High: LONGLONG,
    },
    else => @compileError("M128A only defined on x86_64"),
};

pub const XMM_SAVE_AREA32 = switch (native_arch) {
    .x86_64 => extern struct {
        ControlWord: WORD,
        StatusWord: WORD,
        TagWord: BYTE,
        Reserved1: BYTE,
        ErrorOpcode: WORD,
        ErrorOffset: DWORD,
        ErrorSelector: WORD,
        Reserved2: WORD,
        DataOffset: DWORD,
        DataSelector: WORD,
        Reserved3: WORD,
        MxCsr: DWORD,
        MxCsr_Mask: DWORD,
        FloatRegisters: [8]M128A,
        XmmRegisters: [16]M128A,
        Reserved4: [96]BYTE,
    },
    else => @compileError("XMM_SAVE_AREA32 only defined on x86_64"),
};

pub const NEON128 = switch (native_arch) {
    .thumb => extern struct {
        Low: ULONGLONG,
        High: LONGLONG,
    },
    .aarch64 => extern union {
        DUMMYSTRUCTNAME: extern struct {
            Low: ULONGLONG,
            High: LONGLONG,
        },
        D: [2]f64,
        S: [4]f32,
        H: [8]WORD,
        B: [16]BYTE,
    },
    else => @compileError("NEON128 only defined on aarch64"),
};

pub const CONTEXT = switch (native_arch) {
    .x86 => extern struct {
        ContextFlags: DWORD,
        Dr0: DWORD,
        Dr1: DWORD,
        Dr2: DWORD,
        Dr3: DWORD,
        Dr6: DWORD,
        Dr7: DWORD,
        FloatSave: FLOATING_SAVE_AREA,
        SegGs: DWORD,
        SegFs: DWORD,
        SegEs: DWORD,
        SegDs: DWORD,
        Edi: DWORD,
        Esi: DWORD,
        Ebx: DWORD,
        Edx: DWORD,
        Ecx: DWORD,
        Eax: DWORD,
        Ebp: DWORD,
        Eip: DWORD,
        SegCs: DWORD,
        EFlags: DWORD,
        Esp: DWORD,
        SegSs: DWORD,
        ExtendedRegisters: [512]BYTE,

        pub fn getRegs(ctx: *const CONTEXT) struct { bp: usize, ip: usize, sp: usize } {
            return .{ .bp = ctx.Ebp, .ip = ctx.Eip, .sp = ctx.Esp };
        }
    },
    .x86_64 => extern struct {
        P1Home: DWORD64 align(16),
        P2Home: DWORD64,
        P3Home: DWORD64,
        P4Home: DWORD64,
        P5Home: DWORD64,
        P6Home: DWORD64,
        ContextFlags: DWORD,
        MxCsr: DWORD,
        SegCs: WORD,
        SegDs: WORD,
        SegEs: WORD,
        SegFs: WORD,
        SegGs: WORD,
        SegSs: WORD,
        EFlags: DWORD,
        Dr0: DWORD64,
        Dr1: DWORD64,
        Dr2: DWORD64,
        Dr3: DWORD64,
        Dr6: DWORD64,
        Dr7: DWORD64,
        Rax: DWORD64,
        Rcx: DWORD64,
        Rdx: DWORD64,
        Rbx: DWORD64,
        Rsp: DWORD64,
        Rbp: DWORD64,
        Rsi: DWORD64,
        Rdi: DWORD64,
        R8: DWORD64,
        R9: DWORD64,
        R10: DWORD64,
        R11: DWORD64,
        R12: DWORD64,
        R13: DWORD64,
        R14: DWORD64,
        R15: DWORD64,
        Rip: DWORD64,
        DUMMYUNIONNAME: extern union {
            FltSave: XMM_SAVE_AREA32,
            FloatSave: XMM_SAVE_AREA32,
            DUMMYSTRUCTNAME: extern struct {
                Header: [2]M128A,
                Legacy: [8]M128A,
                Xmm0: M128A,
                Xmm1: M128A,
                Xmm2: M128A,
                Xmm3: M128A,
                Xmm4: M128A,
                Xmm5: M128A,
                Xmm6: M128A,
                Xmm7: M128A,
                Xmm8: M128A,
                Xmm9: M128A,
                Xmm10: M128A,
                Xmm11: M128A,
                Xmm12: M128A,
                Xmm13: M128A,
                Xmm14: M128A,
                Xmm15: M128A,
            },
        },
        VectorRegister: [26]M128A,
        VectorControl: DWORD64,
        DebugControl: DWORD64,
        LastBranchToRip: DWORD64,
        LastBranchFromRip: DWORD64,
        LastExceptionToRip: DWORD64,
        LastExceptionFromRip: DWORD64,

        pub fn getRegs(ctx: *const CONTEXT) struct { bp: usize, ip: usize, sp: usize } {
            return .{ .bp = ctx.Rbp, .ip = ctx.Rip, .sp = ctx.Rsp };
        }

        pub fn setIp(ctx: *CONTEXT, ip: usize) void {
            ctx.Rip = ip;
        }

        pub fn setSp(ctx: *CONTEXT, sp: usize) void {
            ctx.Rsp = sp;
        }
    },
    .thumb => extern struct {
        ContextFlags: ULONG,
        R0: ULONG,
        R1: ULONG,
        R2: ULONG,
        R3: ULONG,
        R4: ULONG,
        R5: ULONG,
        R6: ULONG,
        R7: ULONG,
        R8: ULONG,
        R9: ULONG,
        R10: ULONG,
        R11: ULONG,
        R12: ULONG,
        Sp: ULONG,
        Lr: ULONG,
        Pc: ULONG,
        Cpsr: ULONG,
        Fpcsr: ULONG,
        Padding: ULONG,
        DUMMYUNIONNAME: extern union {
            Q: [16]NEON128,
            D: [32]ULONGLONG,
            S: [32]ULONG,
        },
        Bvr: [8]ULONG,
        Bcr: [8]ULONG,
        Wvr: [1]ULONG,
        Wcr: [1]ULONG,
        Padding2: [2]ULONG,

        pub fn getRegs(ctx: *const CONTEXT) struct { bp: usize, ip: usize, sp: usize } {
            return .{
                .bp = ctx.DUMMYUNIONNAME.S[11],
                .ip = ctx.Pc,
                .sp = ctx.Sp,
            };
        }

        pub fn setIp(ctx: *CONTEXT, ip: usize) void {
            ctx.Pc = ip;
        }

        pub fn setSp(ctx: *CONTEXT, sp: usize) void {
            ctx.Sp = sp;
        }
    },
    .aarch64 => extern struct {
        ContextFlags: ULONG align(16),
        Cpsr: ULONG,
        DUMMYUNIONNAME: extern union {
            DUMMYSTRUCTNAME: extern struct {
                X0: DWORD64,
                X1: DWORD64,
                X2: DWORD64,
                X3: DWORD64,
                X4: DWORD64,
                X5: DWORD64,
                X6: DWORD64,
                X7: DWORD64,
                X8: DWORD64,
                X9: DWORD64,
                X10: DWORD64,
                X11: DWORD64,
                X12: DWORD64,
                X13: DWORD64,
                X14: DWORD64,
                X15: DWORD64,
                X16: DWORD64,
                X17: DWORD64,
                X18: DWORD64,
                X19: DWORD64,
                X20: DWORD64,
                X21: DWORD64,
                X22: DWORD64,
                X23: DWORD64,
                X24: DWORD64,
                X25: DWORD64,
                X26: DWORD64,
                X27: DWORD64,
                X28: DWORD64,
                Fp: DWORD64,
                Lr: DWORD64,
            },
            X: [31]DWORD64,
        },
        Sp: DWORD64,
        Pc: DWORD64,
        V: [32]NEON128,
        Fpcr: DWORD,
        Fpsr: DWORD,
        Bcr: [8]DWORD,
        Bvr: [8]DWORD64,
        Wcr: [2]DWORD,
        Wvr: [2]DWORD64,

        pub fn getRegs(ctx: *const CONTEXT) struct { bp: usize, ip: usize, sp: usize } {
            return .{
                .bp = ctx.DUMMYUNIONNAME.DUMMYSTRUCTNAME.Fp,
                .ip = ctx.Pc,
                .sp = ctx.Sp,
            };
        }

        pub fn setIp(ctx: *CONTEXT, ip: usize) void {
            ctx.Pc = ip;
        }

        pub fn setSp(ctx: *CONTEXT, sp: usize) void {
            ctx.Sp = sp;
        }
    },
    else => @compileError("CONTEXT is not defined for this architecture"),
};

pub const RUNTIME_FUNCTION = switch (native_arch) {
    .x86_64 => extern struct {
        BeginAddress: DWORD,
        EndAddress: DWORD,
        UnwindData: DWORD,
    },
    .thumb => extern struct {
        BeginAddress: DWORD,
        DUMMYUNIONNAME: extern union {
            UnwindData: DWORD,
            DUMMYSTRUCTNAME: packed struct(u32) {
                Flag: u2,
                FunctionLength: u11,
                Ret: u2,
                H: u1,
                Reg: u3,
                R: u1,
                L: u1,
                C: u1,
                StackAdjust: u10,
            },
        },
    },
    .aarch64 => extern struct {
        BeginAddress: DWORD,
        DUMMYUNIONNAME: extern union {
            UnwindData: DWORD,
            DUMMYSTRUCTNAME: packed struct(u32) {
                Flag: u2,
                FunctionLength: u11,
                RegF: u3,
                RegI: u4,
                H: u1,
                CR: u2,
                FrameSize: u9,
            },
        },
    },
    else => @compileError("RUNTIME_FUNCTION is not defined for this architecture"),
};

pub const KNONVOLATILE_CONTEXT_POINTERS = switch (native_arch) {
    .x86_64 => extern struct {
        FloatingContext: [16]?*M128A,
        IntegerContext: [16]?*ULONG64,
    },
    .thumb => extern struct {
        R4: ?*DWORD,
        R5: ?*DWORD,
        R6: ?*DWORD,
        R7: ?*DWORD,
        R8: ?*DWORD,
        R9: ?*DWORD,
        R10: ?*DWORD,
        R11: ?*DWORD,
        Lr: ?*DWORD,
        D8: ?*ULONGLONG,
        D9: ?*ULONGLONG,
        D10: ?*ULONGLONG,
        D11: ?*ULONGLONG,
        D12: ?*ULONGLONG,
        D13: ?*ULONGLONG,
        D14: ?*ULONGLONG,
        D15: ?*ULONGLONG,
    },
    .aarch64 => extern struct {
        X19: ?*DWORD64,
        X20: ?*DWORD64,
        X21: ?*DWORD64,
        X22: ?*DWORD64,
        X23: ?*DWORD64,
        X24: ?*DWORD64,
        X25: ?*DWORD64,
        X26: ?*DWORD64,
        X27: ?*DWORD64,
        X28: ?*DWORD64,
        Fp: ?*DWORD64,
        Lr: ?*DWORD64,
        D8: ?*DWORD64,
        D9: ?*DWORD64,
        D10: ?*DWORD64,
        D11: ?*DWORD64,
        D12: ?*DWORD64,
        D13: ?*DWORD64,
        D14: ?*DWORD64,
        D15: ?*DWORD64,
    },
    else => @compileError("KNONVOLATILE_CONTEXT_POINTERS is not defined for this architecture"),
};

pub const EXCEPTION_POINTERS = extern struct {
    ExceptionRecord: *EXCEPTION_RECORD,
    ContextRecord: *CONTEXT,
};

pub const VECTORED_EXCEPTION_HANDLER = *const fn (ExceptionInfo: *EXCEPTION_POINTERS) callconv(.winapi) c_long;

pub const EXCEPTION_DISPOSITION = i32;
pub const EXCEPTION_ROUTINE = *const fn (
    ExceptionRecord: ?*EXCEPTION_RECORD,
    EstablisherFrame: PVOID,
    ContextRecord: *CONTEXT,
    DispatcherContext: PVOID,
) callconv(.winapi) EXCEPTION_DISPOSITION;

pub const UNWIND_HISTORY_TABLE_SIZE = 12;
pub const UNWIND_HISTORY_TABLE_ENTRY = extern struct {
    ImageBase: ULONG64,
    FunctionEntry: *RUNTIME_FUNCTION,
};

pub const UNWIND_HISTORY_TABLE = extern struct {
    Count: ULONG,
    LocalHint: BYTE,
    GlobalHint: BYTE,
    Search: BYTE,
    Once: BYTE,
    LowAddress: ULONG64,
    HighAddress: ULONG64,
    Entry: [UNWIND_HISTORY_TABLE_SIZE]UNWIND_HISTORY_TABLE_ENTRY,
};

pub const UNW_FLAG_NHANDLER = 0x0;
pub const UNW_FLAG_EHANDLER = 0x1;
pub const UNW_FLAG_UHANDLER = 0x2;
pub const UNW_FLAG_CHAININFO = 0x4;

pub const ACTIVATION_CONTEXT_DATA = opaque {};
pub const ASSEMBLY_STORAGE_MAP = opaque {};
pub const FLS_CALLBACK_INFO = opaque {};
pub const RTL_BITMAP = opaque {};
pub const KAFFINITY = usize;
pub const KPRIORITY = i32;

pub const CLIENT_ID = extern struct {
    UniqueProcess: HANDLE,
    UniqueThread: HANDLE,
};

pub const TEB = extern struct {
    NtTib: NT_TIB,
    EnvironmentPointer: PVOID,
    ClientId: CLIENT_ID,
    ActiveRpcHandle: PVOID,
    ThreadLocalStoragePointer: PVOID,
    ProcessEnvironmentBlock: *PEB,
    LastErrorValue: Win32Error,
    Reserved2: [399 * @sizeOf(PVOID) - @sizeOf(ULONG)]u8,
    Reserved3: [1952]u8,
    TlsSlots: [64]PVOID,
    Reserved4: [8]u8,
    Reserved5: [26]PVOID,
    ReservedForOle: PVOID,
    Reserved6: [4]PVOID,
    TlsExpansionSlots: PVOID,
};

comptime {
    // XXX: Without this check we cannot use `std.Io.Writer` on 16-bit platforms. `std.fmt.bufPrint` will hit the unreachable in `PEB.GdiHandleBuffer` without this guard.
    if (builtin.os.tag == .windows) {
        // Offsets taken from WinDbg info and Geoff Chappell[1] (RIP)
        // [1]: https://www.geoffchappell.com/studies/windows/km/ntoskrnl/inc/api/pebteb/teb/index.htm
        assert(@offsetOf(TEB, "NtTib") == 0x00);
        if (@sizeOf(usize) == 4) {
            assert(@offsetOf(TEB, "EnvironmentPointer") == 0x1C);
            assert(@offsetOf(TEB, "ClientId") == 0x20);
            assert(@offsetOf(TEB, "ActiveRpcHandle") == 0x28);
            assert(@offsetOf(TEB, "ThreadLocalStoragePointer") == 0x2C);
            assert(@offsetOf(TEB, "ProcessEnvironmentBlock") == 0x30);
            assert(@offsetOf(TEB, "LastErrorValue") == 0x34);
            assert(@offsetOf(TEB, "TlsSlots") == 0xe10);
        } else if (@sizeOf(usize) == 8) {
            assert(@offsetOf(TEB, "EnvironmentPointer") == 0x38);
            assert(@offsetOf(TEB, "ClientId") == 0x40);
            assert(@offsetOf(TEB, "ActiveRpcHandle") == 0x50);
            assert(@offsetOf(TEB, "ThreadLocalStoragePointer") == 0x58);
            assert(@offsetOf(TEB, "ProcessEnvironmentBlock") == 0x60);
            assert(@offsetOf(TEB, "LastErrorValue") == 0x68);
            assert(@offsetOf(TEB, "TlsSlots") == 0x1480);
        }
    }
}

pub const EXCEPTION_REGISTRATION_RECORD = extern struct {
    Next: ?*EXCEPTION_REGISTRATION_RECORD,
    Handler: ?*EXCEPTION_DISPOSITION,
};

pub const NT_TIB = extern struct {
    ExceptionList: ?*EXCEPTION_REGISTRATION_RECORD,
    StackBase: PVOID,
    StackLimit: PVOID,
    SubSystemTib: PVOID,
    DUMMYUNIONNAME: extern union { FiberData: PVOID, Version: DWORD },
    ArbitraryUserPointer: PVOID,
    Self: ?*@This(),
};

/// Process Environment Block
/// Microsoft documentation of this is incomplete, the fields here are taken from various resources including:
///  - https://github.com/wine-mirror/wine/blob/1aff1e6a370ee8c0213a0fd4b220d121da8527aa/include/winternl.h#L269
///  - https://www.geoffchappell.com/studies/windows/win32/ntdll/structs/peb/index.htm
pub const PEB = extern struct {
    // Versions: All
    InheritedAddressSpace: BOOLEAN,

    // Versions: 3.51+
    ReadImageFileExecOptions: BOOLEAN,
    BeingDebugged: BOOLEAN,

    // Versions: 5.2+ (previously was padding)
    BitField: UCHAR,

    // Versions: all
    Mutant: HANDLE,
    ImageBaseAddress: HMODULE,
    Ldr: *PEB_LDR_DATA,
    ProcessParameters: *RTL_USER_PROCESS_PARAMETERS,
    SubSystemData: PVOID,
    ProcessHeap: ?*HEAP,

    // Versions: 5.1+
    FastPebLock: *RTL_CRITICAL_SECTION,

    // Versions: 5.2+
    AtlThunkSListPtr: PVOID,
    IFEOKey: PVOID,

    // Versions: 6.0+

    /// https://www.geoffchappell.com/studies/windows/win32/ntdll/structs/peb/crossprocessflags.htm
    CrossProcessFlags: ULONG,

    // Versions: 6.0+
    union1: extern union {
        KernelCallbackTable: PVOID,
        UserSharedInfoPtr: PVOID,
    },

    // Versions: 5.1+
    SystemReserved: ULONG,

    // Versions: 5.1, (not 5.2, not 6.0), 6.1+
    AtlThunkSListPtr32: ULONG,

    // Versions: 6.1+
    ApiSetMap: PVOID,

    // Versions: all
    TlsExpansionCounter: ULONG,
    // note: there is padding here on 64 bit
    TlsBitmap: *RTL_BITMAP,
    TlsBitmapBits: [2]ULONG,
    /// Our base address of the memory region shared with the CSR server.
    ReadOnlySharedMemoryBase: PVOID,

    // Versions: 1703+
    SharedData: PVOID,

    // Versions: all
    ReadOnlyStaticServerData: *UnknownStaticServerDataIndirection,
    AnsiCodePageData: PVOID,
    OemCodePageData: PVOID,
    UnicodeCaseTableData: PVOID,

    // Versions: 3.51+
    NumberOfProcessors: ULONG,
    NtGlobalFlag: ULONG,

    // Versions: all
    CriticalSectionTimeout: LARGE_INTEGER,

    // End of Original PEB size

    // Fields appended in 3.51:
    HeapSegmentReserve: ULONG_PTR,
    HeapSegmentCommit: ULONG_PTR,
    HeapDeCommitTotalFreeThreshold: ULONG_PTR,
    HeapDeCommitFreeBlockThreshold: ULONG_PTR,
    NumberOfHeaps: ULONG,
    MaximumNumberOfHeaps: ULONG,
    ProcessHeaps: *PVOID,

    // Fields appended in 4.0:
    GdiSharedHandleTable: PVOID,
    ProcessStarterHelper: PVOID,
    GdiDCAttributeList: ULONG,
    // note: there is padding here on 64 bit
    LoaderLock: *RTL_CRITICAL_SECTION,
    OSMajorVersion: ULONG,
    OSMinorVersion: ULONG,
    OSBuildNumber: USHORT,
    OSCSDVersion: USHORT,
    OSPlatformId: ULONG,
    ImageSubSystem: ULONG,
    ImageSubSystemMajorVersion: ULONG,
    ImageSubSystemMinorVersion: ULONG,
    // note: there is padding here on 64 bit
    ActiveProcessAffinityMask: KAFFINITY,
    GdiHandleBuffer: [
        switch (@sizeOf(usize)) {
            4 => 0x22,
            8 => 0x3C,
            else => unreachable,
        }
    ]ULONG,

    // Fields appended in 5.0 (Windows 2000):
    PostProcessInitRoutine: PVOID,
    TlsExpansionBitmap: *RTL_BITMAP,
    TlsExpansionBitmapBits: [32]ULONG,
    SessionId: ULONG,
    // note: there is padding here on 64 bit
    // Versions: 5.1+
    AppCompatFlags: ULARGE_INTEGER,
    AppCompatFlagsUser: ULARGE_INTEGER,
    ShimData: PVOID,
    // Versions: 5.0+
    AppCompatInfo: PVOID,
    CSDVersion: UNICODE_STRING,

    // Fields appended in 5.1 (Windows XP):
    ActivationContextData: *const ACTIVATION_CONTEXT_DATA,
    ProcessAssemblyStorageMap: *ASSEMBLY_STORAGE_MAP,
    SystemDefaultActivationData: *const ACTIVATION_CONTEXT_DATA,
    SystemAssemblyStorageMap: *ASSEMBLY_STORAGE_MAP,
    MinimumStackCommit: ULONG_PTR,

    // Fields appended in 5.2 (Windows Server 2003):
    FlsCallback: *FLS_CALLBACK_INFO,
    FlsListHead: LIST_ENTRY,
    FlsBitmap: *RTL_BITMAP,
    FlsBitmapBits: [4]ULONG,
    FlsHighIndex: ULONG,

    // Fields appended in 6.0 (Windows Vista):
    WerRegistrationData: PVOID,
    WerShipAssertPtr: PVOID,

    // Fields appended in 6.1 (Windows 7):
    pUnused: PVOID, // previously pContextData
    pImageHeaderHash: PVOID,

    /// TODO: https://www.geoffchappell.com/studies/windows/win32/ntdll/structs/peb/tracingflags.htm
    TracingFlags: ULONG,

    // Fields appended in 6.2 (Windows 8):
    /// Base address in the CSRSS address space of the memory region shared with the CSR server.
    CsrServerReadOnlySharedMemoryBase: ULONGLONG,

    // Fields appended in 1511:
    TppWorkerpListLock: ULONG,
    TppWorkerpList: LIST_ENTRY,
    WaitOnAddressHashTable: [0x80]PVOID,

    // Fields appended in 1709:
    TelemetryCoverageHeader: PVOID,
    CloudFileFlags: ULONG,

    /// Details of this structure are unknown, but the existence of the field at offset 8 is known
    /// from experimentation and from reverse-engineering kernelbase.dll.
    const UnknownStaticServerDataIndirection = extern struct {
        unknown: u64,
        /// In the CSRSS address space.
        base_static_server_data_addr: u64,
    };
};

/// The `PEB_LDR_DATA` structure is the main record of what modules are loaded in a process.
/// It is essentially the head of three double-linked lists of `LDR.DATA_TABLE_ENTRY` structures which each represent one loaded module.
///
/// Microsoft documentation of this is incomplete, the fields here are taken from various resources including:
///  - https://www.geoffchappell.com/studies/windows/win32/ntdll/structs/peb_ldr_data.htm
pub const PEB_LDR_DATA = extern struct {
    // Versions: 3.51 and higher
    /// The size in bytes of the structure
    Length: ULONG,

    /// TRUE if the structure is prepared.
    Initialized: BOOLEAN,

    SsHandle: PVOID,
    InLoadOrderModuleList: LIST_ENTRY,
    InMemoryOrderModuleList: LIST_ENTRY,
    InInitializationOrderModuleList: LIST_ENTRY,

    // Versions: 5.1 and higher

    /// No known use of this field is known in Windows 8 and higher.
    EntryInProgress: PVOID,

    // Versions: 6.0 from Windows Vista SP1, and higher
    ShutdownInProgress: BOOLEAN,

    /// Though ShutdownThreadId is declared as a HANDLE,
    /// it is indeed the thread ID as suggested by its name.
    /// It is picked up from the UniqueThread member of the CLIENT_ID in the
    /// TEB of the thread that asks to terminate the process.
    ShutdownThreadId: HANDLE,
};

pub const LDR = struct {
    /// Microsoft documentation of this is incomplete, the fields here are taken from various resources including:
    ///  - https://docs.microsoft.com/en-us/windows/win32/api/winternl/ns-winternl-peb_ldr_data
    ///  - https://www.geoffchappell.com/studies/windows/km/ntoskrnl/inc/api/ntldr/ldr_data_table_entry.htm
    pub const DATA_TABLE_ENTRY = extern struct {
        InLoadOrderLinks: LIST_ENTRY,
        InMemoryOrderLinks: LIST_ENTRY,
        InInitializationOrderLinks: LIST_ENTRY,
        DllBase: PVOID,
        EntryPoint: PVOID,
        SizeOfImage: ULONG,
        FullDllName: UNICODE_STRING,
        BaseDllName: UNICODE_STRING,
        Reserved5: [3]PVOID,
        DUMMYUNIONNAME: extern union {
            CheckSum: ULONG,
            Reserved6: PVOID,
        },
        TimeDateStamp: ULONG,
    };

    pub const DLL_NOTIFICATION = struct {
        pub const REASON = enum(ULONG) { LOADED = 1, UNLOADED = 2 };

        pub const DATA = extern union {
            Loaded: LOADED,
            Unloaded: UNLOADED,

            pub const LOADED = extern struct {
                Flags: REGISTER,
                FullDllName: *const UNICODE_STRING,
                BaseDllName: *const UNICODE_STRING,
                DllBase: PVOID,
                SizeOfImage: ULONG,
            };

            pub const UNLOADED = extern struct {
                Flags: REGISTER,
                FullDllName: *const UNICODE_STRING,
                BaseDllName: *const UNICODE_STRING,
                DllBase: PVOID,
                SizeOfImage: ULONG,
            };
        };

        pub const COOKIE = *opaque {};

        pub const FUNCTION = fn (
            NotificationReason: REASON,
            NotificationData: *const DATA,
            Context: ?PVOID,
        ) callconv(.winapi) void;

        pub const REGISTER = packed struct(ULONG) {
            Reserved0: u32 = 0,
        };
    };

    pub const GET_DLL_HANDLE_EX = packed struct(ULONG) {
        UNCHANGED_REFCOUNT: bool = false,
        PIN: bool = false,
        Reserved2: u30 = 0,
    };

    pub const GET_PROCEDURE_ADDRESS = packed struct(ULONG) {
        DONT_RECORD_FORWARDER: bool = false,
        Reserved1: u31 = 0,
    };

    pub const LOAD = packed struct(ULONG) {
        DONT_RESOLVE_DLL_REFERENCES: bool = false,
        LIBRARY_AS_DATAFILE: bool = false,
        PACKAGED_LIBRARY: bool = false,
        WITH_ALTERED_SEARCH_PATH: bool = false,
        IGNORE_CODE_AUTHZ_LEVEL: bool = false,
        LIBRARY_AS_IMAGE_RESOURCE: bool = false,
        LIBRARY_AS_DATAFILE_EXCLUSIVE: bool = false,
        LIBRARY_REQUERE_SIGNED_TARGET: bool = false,
        LIBRARY_SEARCH_DLL_LOAD_DIR: bool = false,
        LIBRARY_SEARCH_USER_DIRS: bool = false,
        LIBRARY_SEARCH_SYSTEM32: bool = false,
        LIBRARY_SEARCH_DEFAULT_DIRS: bool = false,
    };
};

pub const RTL_USER_PROCESS_PARAMETERS = extern struct {
    AllocationSize: ULONG,
    Size: ULONG,
    Flags: ULONG,
    DebugFlags: ULONG,
    ConsoleHandle: HANDLE,
    ConsoleFlags: ULONG,
    hStdInput: HANDLE,
    hStdOutput: HANDLE,
    hStdError: HANDLE,
    CurrentDirectory: CURDIR,
    DllPath: UNICODE_STRING,
    ImagePathName: UNICODE_STRING,
    CommandLine: UNICODE_STRING,
    /// Points to a NUL-terminated sequence of NUL-terminated
    /// WTF-16 LE encoded `name=value` sequences.
    /// Example using string literal syntax:
    /// `"NAME=value\x00foo=bar\x00\x00"`
    Environment: [*:0]WCHAR,
    dwX: ULONG,
    dwY: ULONG,
    dwXSize: ULONG,
    dwYSize: ULONG,
    dwXCountChars: ULONG,
    dwYCountChars: ULONG,
    dwFillAttribute: ULONG,
    dwFlags: ULONG,
    dwShowWindow: ULONG,
    WindowTitle: UNICODE_STRING,
    Desktop: UNICODE_STRING,
    ShellInfo: UNICODE_STRING,
    RuntimeInfo: UNICODE_STRING,
    DLCurrentDirectory: [0x20]RTL_DRIVE_LETTER_CURDIR,
};

pub const RTL_DRIVE_LETTER_CURDIR = extern struct {
    Flags: c_ushort,
    Length: c_ushort,
    TimeStamp: ULONG,
    DosPath: UNICODE_STRING,
};

pub const PPS_POST_PROCESS_INIT_ROUTINE = ?*const fn () callconv(.winapi) void;

pub const FILE_DIRECTORY_INFORMATION = extern struct {
    NextEntryOffset: ULONG,
    FileIndex: ULONG,
    CreationTime: LARGE_INTEGER,
    LastAccessTime: LARGE_INTEGER,
    LastWriteTime: LARGE_INTEGER,
    ChangeTime: LARGE_INTEGER,
    EndOfFile: LARGE_INTEGER,
    AllocationSize: LARGE_INTEGER,
    FileAttributes: FILE.ATTRIBUTE,
    FileNameLength: ULONG,
    FileName: [1]WCHAR,
};

pub const FILE_BOTH_DIR_INFORMATION = extern struct {
    NextEntryOffset: ULONG,
    FileIndex: ULONG,
    CreationTime: LARGE_INTEGER,
    LastAccessTime: LARGE_INTEGER,
    LastWriteTime: LARGE_INTEGER,
    ChangeTime: LARGE_INTEGER,
    EndOfFile: LARGE_INTEGER,
    AllocationSize: LARGE_INTEGER,
    FileAttributes: FILE.ATTRIBUTE,
    FileNameLength: ULONG,
    EaSize: ULONG,
    ShortNameLength: CHAR,
    ShortName: [12]WCHAR,
    FileName: [1]WCHAR,
};
pub const FILE_BOTH_DIRECTORY_INFORMATION = FILE_BOTH_DIR_INFORMATION;

/// Helper for iterating a byte buffer of FILE_*_INFORMATION structures (from
/// things like NtQueryDirectoryFile calls).
pub fn FileInformationIterator(comptime FileInformationType: type) type {
    return struct {
        byte_offset: usize = 0,
        buf: []u8 align(@alignOf(FileInformationType)),

        pub fn next(self: *@This()) ?*FileInformationType {
            if (self.byte_offset >= self.buf.len) return null;
            const cur: *FileInformationType = @ptrCast(@alignCast(&self.buf[self.byte_offset]));
            if (cur.NextEntryOffset == 0) {
                self.byte_offset = self.buf.len;
            } else {
                self.byte_offset += cur.NextEntryOffset;
            }
            return cur;
        }
    };
}

pub const IO_APC_ROUTINE = fn (?*anyopaque, *IO_STATUS_BLOCK, ULONG) callconv(.winapi) void;

pub const CURDIR = extern struct {
    DosPath: UNICODE_STRING,
    Handle: HANDLE,
};

pub const DUPLICATE_SAME_ACCESS = 2;

pub const MODULEINFO = extern struct {
    lpBaseOfDll: LPVOID,
    SizeOfImage: DWORD,
    EntryPoint: LPVOID,
};

pub const OSVERSIONINFOW = extern struct {
    dwOSVersionInfoSize: ULONG,
    dwMajorVersion: ULONG,
    dwMinorVersion: ULONG,
    dwBuildNumber: ULONG,
    dwPlatformId: ULONG,
    szCSDVersion: [128]WCHAR,
};
pub const RTL_OSVERSIONINFOW = OSVERSIONINFOW;

pub const REPARSE_DATA_BUFFER = extern struct {
    ReparseTag: IO_REPARSE_TAG,
    ReparseDataLength: USHORT,
    Reserved: USHORT,
    DataBuffer: [1]UCHAR,
};
pub const SYMBOLIC_LINK_REPARSE_BUFFER = extern struct {
    SubstituteNameOffset: USHORT,
    SubstituteNameLength: USHORT,
    PrintNameOffset: USHORT,
    PrintNameLength: USHORT,
    Flags: ULONG,
    PathBuffer: [1]WCHAR,
};
pub const MOUNT_POINT_REPARSE_BUFFER = extern struct {
    SubstituteNameOffset: USHORT,
    SubstituteNameLength: USHORT,
    PrintNameOffset: USHORT,
    PrintNameLength: USHORT,
    PathBuffer: [1]WCHAR,
};
pub const SYMLINK_FLAG_RELATIVE: ULONG = 0x1;

pub const SYMBOLIC_LINK_FLAG_DIRECTORY: DWORD = 0x1;
pub const SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE: DWORD = 0x2;

pub const MOUNTMGR_MOUNT_POINT = extern struct {
    SymbolicLinkNameOffset: ULONG,
    SymbolicLinkNameLength: USHORT,
    Reserved1: USHORT,
    UniqueIdOffset: ULONG,
    UniqueIdLength: USHORT,
    Reserved2: USHORT,
    DeviceNameOffset: ULONG,
    DeviceNameLength: USHORT,
    Reserved3: USHORT,
};
pub const MOUNTMGR_MOUNT_POINTS = extern struct {
    Size: ULONG,
    NumberOfMountPoints: ULONG,
    MountPoints: [1]MOUNTMGR_MOUNT_POINT,
};

pub const MOUNTMGR_TARGET_NAME = extern struct {
    DeviceNameLength: USHORT,
    DeviceName: [1]WCHAR,
};
pub const MOUNTMGR_VOLUME_PATHS = extern struct {
    MultiSzLength: ULONG,
    MultiSz: [1]WCHAR,
};

pub const SRWLOCK_INIT = SRWLOCK{};
pub const SRWLOCK = extern struct {
    Ptr: ?PVOID = null,
};

pub const CONDITION_VARIABLE_INIT = CONDITION_VARIABLE{};
pub const CONDITION_VARIABLE = extern struct {
    Ptr: ?PVOID = null,
};

/// Processor feature enumeration.
pub const PF = enum(DWORD) {
    /// On a Pentium, a floating-point precision error can occur in rare circumstances.
    FLOATING_POINT_PRECISION_ERRATA = 0,

    /// Floating-point operations are emulated using software emulator.
    /// This function returns a nonzero value if floating-point operations are emulated; otherwise, it returns zero.
    FLOATING_POINT_EMULATED = 1,

    /// The atomic compare and exchange operation (cmpxchg) is available.
    COMPARE_EXCHANGE_DOUBLE = 2,

    /// The MMX instruction set is available.
    MMX_INSTRUCTIONS_AVAILABLE = 3,

    PPC_MOVEMEM_64BIT_OK = 4,
    ALPHA_BYTE_INSTRUCTIONS = 5,

    /// The SSE instruction set is available.
    XMMI_INSTRUCTIONS_AVAILABLE = 6,

    /// The 3D-Now instruction is available.
    @"3DNOW_INSTRUCTIONS_AVAILABLE" = 7,

    /// The RDTSC instruction is available.
    RDTSC_INSTRUCTION_AVAILABLE = 8,

    /// The processor is PAE-enabled.
    PAE_ENABLED = 9,

    /// The SSE2 instruction set is available.
    XMMI64_INSTRUCTIONS_AVAILABLE = 10,

    SSE_DAZ_MODE_AVAILABLE = 11,

    /// Data execution prevention is enabled.
    NX_ENABLED = 12,

    /// The SSE3 instruction set is available.
    SSE3_INSTRUCTIONS_AVAILABLE = 13,

    /// The atomic compare and exchange 128-bit operation (cmpxchg16b) is available.
    COMPARE_EXCHANGE128 = 14,

    /// The atomic compare 64 and exchange 128-bit operation (cmp8xchg16) is available.
    COMPARE64_EXCHANGE128 = 15,

    /// The processor channels are enabled.
    CHANNELS_ENABLED = 16,

    /// The processor implements the XSAVI and XRSTOR instructions.
    XSAVE_ENABLED = 17,

    /// The VFP/Neon: 32 x 64bit register bank is present.
    /// This flag has the same meaning as PF_ARM_VFP_EXTENDED_REGISTERS.
    ARM_VFP_32_REGISTERS_AVAILABLE = 18,

    /// This ARM processor implements the ARM v8 NEON instruction set.
    ARM_NEON_INSTRUCTIONS_AVAILABLE = 19,

    /// Second Level Address Translation is supported by the hardware.
    SECOND_LEVEL_ADDRESS_TRANSLATION = 20,

    /// Virtualization is enabled in the firmware and made available by the operating system.
    VIRT_FIRMWARE_ENABLED = 21,

    /// RDFSBASE, RDGSBASE, WRFSBASE, and WRGSBASE instructions are available.
    RDWRFSGBASE_AVAILABLE = 22,

    /// _fastfail() is available.
    FASTFAIL_AVAILABLE = 23,

    /// The divide instruction_available.
    ARM_DIVIDE_INSTRUCTION_AVAILABLE = 24,

    /// The 64-bit load/store atomic instructions are available.
    ARM_64BIT_LOADSTORE_ATOMIC = 25,

    /// The external cache is available.
    ARM_EXTERNAL_CACHE_AVAILABLE = 26,

    /// The floating-point multiply-accumulate instruction is available.
    ARM_FMAC_INSTRUCTIONS_AVAILABLE = 27,

    RDRAND_INSTRUCTION_AVAILABLE = 28,

    /// This ARM processor implements the ARM v8 instructions set.
    ARM_V8_INSTRUCTIONS_AVAILABLE = 29,

    /// This ARM processor implements the ARM v8 extra cryptographic instructions (i.e., AES, SHA1 and SHA2).
    ARM_V8_CRYPTO_INSTRUCTIONS_AVAILABLE = 30,

    /// This ARM processor implements the ARM v8 extra CRC32 instructions.
    ARM_V8_CRC32_INSTRUCTIONS_AVAILABLE = 31,

    RDTSCP_INSTRUCTION_AVAILABLE = 32,
    RDPID_INSTRUCTION_AVAILABLE = 33,

    /// This ARM processor implements the ARM v8.1 atomic instructions (e.g., CAS, SWP).
    ARM_V81_ATOMIC_INSTRUCTIONS_AVAILABLE = 34,

    MONITORX_INSTR
```
