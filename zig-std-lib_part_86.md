```
   0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     65508, 0,
    65520, 65520, 65520, 65520, 65520, 65520, 65520, 65520, 65520, 65520, 65520, 65520, 65520, 65520, 65520, 65520, 0,     0,     0,     0,     65535, 0,     0,     0,
    0,     0,     0,     0,     0,     0,     0,     0,     65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510,
    65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 65510, 0,     0,     0,     0,     0,     0,     65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488,
    65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488,
    65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 0,     0,     65535, 0,     0,     0,     54741, 54744, 0,
    65535, 0,     65535, 0,     65535, 0,     0,     0,     0,     0,     0,     65535, 0,     0,     65535, 0,     0,     0,     0,     0,     0,     0,     0,     0,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272,
    58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272, 58272,
    58272, 58272, 58272, 58272, 58272, 58272, 0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     0,     0,     0,     0,     0,     0,     0,
    0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     0,     0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     65535, 0,     65535, 0,     0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     0,     0,     0,     65535, 0,     0,     0,     0,     65504, 65504, 65504, 65504, 65504, 65504, 65504,
    65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 0,     0,     0,     0,     0,
};

/// Cross-platform implementation of `ntdll.RtlUpcaseUnicodeChar`.
/// Transforms the UTF-16 code unit in `c` to its uppercased version
/// if there is one. Otherwise, returns `c` unmodified.
///
/// Note: When this function is referenced, it will need to include
///       `uppercase_table.len * 2` bytes of data in the resulting binary
///       since it depends on the `uppercase_table` data. When
///       targeting Windows, `ntdll.RtlUpcaseUnicodeChar` can be
///       used instead to avoid having to include a copy of this data.
pub fn upcaseW(c: u16) u16 {
    if (c < 'a') {
        return c;
    }
    if (c <= 'z') {
        return c - ('a' - 'A');
    }
    if (c >= 0xC0) {
        var offset: u16 = 0;

        offset += @as(u8, @truncate(c >> 8));
        offset = uppercase_table[offset];
        offset += @as(u4, @truncate(c >> 4));
        offset = uppercase_table[offset];
        offset += @as(u4, @truncate(c));
        offset = uppercase_table[offset];

        return c +% offset;
    }
    return c;
}

test "upcaseW matches RtlUpcaseUnicodeChar" {
    if (builtin.os.tag != .windows) return error.SkipZigTest;

    var c: u16 = 0;
    while (true) : (c += 1) {
        std.testing.expectEqual(std.os.windows.ntdll.RtlUpcaseUnicodeChar(c), upcaseW(c)) catch |err| {
            std.debug.print("mismatch for codepoint U+{X}\n", .{c});
            return err;
        };
        if (c == 0xFFFF) break;
    }
}



---
File: /std/os/windows/ntdll.zig
---

const std = @import("../../std.zig");
const windows = std.os.windows;

const ACCESS_MASK = windows.ACCESS_MASK;
const ANSI_STRING = windows.ANSI_STRING;
const BOOL = windows.BOOL;
const BOOLEAN = windows.BOOLEAN;
const CONDITION_VARIABLE = windows.CONDITION_VARIABLE;
const CONTEXT = windows.CONTEXT;
const CRITICAL_SECTION = windows.CRITICAL_SECTION;
const CTL_CODE = windows.CTL_CODE;
const CURDIR = windows.CURDIR;
const DIRECTORY = windows.DIRECTORY;
const DWORD = windows.DWORD;
const DWORD64 = windows.DWORD64;
const ERESOURCE = windows.ERESOURCE;
const EVENT_TYPE = windows.EVENT_TYPE;
const EXCEPTION_ROUTINE = windows.EXCEPTION_ROUTINE;
const FILE = windows.FILE;
const FS_INFORMATION_CLASS = windows.FS_INFORMATION_CLASS;
const HANDLE = windows.HANDLE;
const HEAP = windows.HEAP;
const IO_APC_ROUTINE = windows.IO_APC_ROUTINE;
const IO_STATUS_BLOCK = windows.IO_STATUS_BLOCK;
const KEY = windows.KEY;
const KNONVOLATILE_CONTEXT_POINTERS = windows.KNONVOLATILE_CONTEXT_POINTERS;
const LARGE_INTEGER = windows.LARGE_INTEGER;
const LDR = windows.LDR;
const LOGICAL = windows.LOGICAL;
const LONG = windows.LONG;
const LPCVOID = windows.LPCVOID;
const LPVOID = windows.LPVOID;
const MEM = windows.MEM;
const NTSTATUS = windows.NTSTATUS;
const OBJECT = windows.OBJECT;
const PAGE = windows.PAGE;
const PCWSTR = windows.PCWSTR;
const PROCESS = windows.PROCESS;
const PVOID = windows.PVOID;
const PWSTR = windows.PWSTR;
const REG = windows.REG;
const RTL_OSVERSIONINFOW = windows.RTL_OSVERSIONINFOW;
const RTL_QUERY_REGISTRY_TABLE = windows.RTL_QUERY_REGISTRY_TABLE;
const RUNTIME_FUNCTION = windows.RUNTIME_FUNCTION;
const SEC = windows.SEC;
const SECTION_INHERIT = windows.SECTION_INHERIT;
const SIZE_T = windows.SIZE_T;
const SRWLOCK = windows.SRWLOCK;
const SYSTEM = windows.SYSTEM;
const THREAD = windows.THREAD;
const ULONG = windows.ULONG;
const ULONG_PTR = windows.ULONG_PTR;
const UNICODE_STRING = windows.UNICODE_STRING;
const UNWIND_HISTORY_TABLE = windows.UNWIND_HISTORY_TABLE;
const USHORT = windows.USHORT;
const VECTORED_EXCEPTION_HANDLER = windows.VECTORED_EXCEPTION_HANDLER;
const WORD = windows.WORD;
const USER_THREAD_START_ROUTINE = windows.USER_THREAD_START_ROUTINE;
const PS = windows.PS;
const TEB = windows.TEB;

// ref: km/ntifs.h

pub extern "ntdll" fn RtlCreateHeap(
    Flags: HEAP.FLAGS.CREATE,
    HeapBase: ?PVOID,
    ReserveSize: SIZE_T,
    CommitSize: SIZE_T,
    Lock: ?*ERESOURCE,
    Parameters: ?*const HEAP.RTL_PARAMETERS,
) callconv(.winapi) ?*HEAP;

pub extern "ntdll" fn RtlDestroyHeap(
    HeapHandle: *HEAP,
) callconv(.winapi) ?*HEAP;

pub extern "ntdll" fn RtlAllocateHeap(
    HeapHandle: *HEAP,
    Flags: HEAP.FLAGS.ALLOCATION,
    Size: SIZE_T,
) callconv(.winapi) ?PVOID;

pub extern "ntdll" fn RtlFreeHeap(
    HeapHandle: *HEAP,
    Flags: HEAP.FLAGS.ALLOCATION,
    BaseAddress: ?PVOID,
) callconv(.winapi) LOGICAL;

pub extern "ntdll" fn RtlCaptureStackBackTrace(
    FramesToSkip: ULONG,
    FramesToCapture: ULONG,
    BackTrace: **anyopaque,
    BackTraceHash: ?*ULONG,
) callconv(.winapi) USHORT;

pub extern "ntdll" fn RtlCaptureContext(
    ContextRecord: *CONTEXT,
) callconv(.winapi) void;

pub extern "ntdll" fn NtSetInformationThread(
    ThreadHandle: HANDLE,
    ThreadInformationClass: THREAD.INFOCLASS,
    ThreadInformation: *const anyopaque,
    ThreadInformationLength: ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtCreateFile(
    FileHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: *const OBJECT.ATTRIBUTES,
    IoStatusBlock: *IO_STATUS_BLOCK,
    AllocationSize: ?*const LARGE_INTEGER,
    FileAttributes: FILE.ATTRIBUTE,
    ShareAccess: FILE.SHARE,
    CreateDisposition: FILE.CREATE_DISPOSITION,
    CreateOptions: FILE.MODE,
    EaBuffer: ?*const anyopaque,
    EaLength: ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtDeviceIoControlFile(
    FileHandle: HANDLE,
    Event: ?HANDLE,
    ApcRoutine: ?*align(2) const IO_APC_ROUTINE,
    ApcContext: ?*anyopaque,
    IoStatusBlock: *IO_STATUS_BLOCK,
    IoControlCode: CTL_CODE,
    InputBuffer: ?*const anyopaque,
    InputBufferLength: ULONG,
    OutputBuffer: ?PVOID,
    OutputBufferLength: ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtFsControlFile(
    FileHandle: HANDLE,
    Event: ?HANDLE,
    ApcRoutine: ?*align(2) const IO_APC_ROUTINE,
    ApcContext: ?*anyopaque,
    IoStatusBlock: *IO_STATUS_BLOCK,
    FsControlCode: CTL_CODE,
    InputBuffer: ?*const anyopaque,
    InputBufferLength: ULONG,
    OutputBuffer: ?PVOID,
    OutputBufferLength: ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtLockFile(
    FileHandle: HANDLE,
    Event: ?HANDLE,
    ApcRoutine: ?*align(2) const IO_APC_ROUTINE,
    ApcContext: ?*anyopaque,
    IoStatusBlock: *IO_STATUS_BLOCK,
    ByteOffset: *const LARGE_INTEGER,
    Length: *const LARGE_INTEGER,
    Key: ?*const ULONG,
    FailImmediately: BOOLEAN,
    ExclusiveLock: BOOLEAN,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtOpenFile(
    FileHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: *const OBJECT.ATTRIBUTES,
    IoStatusBlock: *IO_STATUS_BLOCK,
    ShareAccess: FILE.SHARE,
    OpenOptions: FILE.MODE,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtQueryDirectoryFile(
    FileHandle: HANDLE,
    Event: ?HANDLE,
    ApcRoutine: ?*align(2) const IO_APC_ROUTINE,
    ApcContext: ?*anyopaque,
    IoStatusBlock: *IO_STATUS_BLOCK,
    FileInformation: *anyopaque,
    Length: ULONG,
    FileInformationClass: FILE.INFORMATION_CLASS,
    ReturnSingleEntry: BOOLEAN,
    FileName: ?*const UNICODE_STRING,
    RestartScan: BOOLEAN,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtQueryInformationFile(
    FileHandle: HANDLE,
    IoStatusBlock: *IO_STATUS_BLOCK,
    FileInformation: *anyopaque,
    Length: ULONG,
    FileInformationClass: FILE.INFORMATION_CLASS,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtQueryVolumeInformationFile(
    FileHandle: HANDLE,
    IoStatusBlock: *IO_STATUS_BLOCK,
    FsInformation: *anyopaque,
    Length: ULONG,
    FsInformationClass: FS_INFORMATION_CLASS,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtReadFile(
    FileHandle: HANDLE,
    Event: ?HANDLE,
    ApcRoutine: ?*align(2) const IO_APC_ROUTINE,
    ApcContext: ?*anyopaque,
    IoStatusBlock: *IO_STATUS_BLOCK,
    Buffer: *anyopaque,
    Length: ULONG,
    ByteOffset: ?*const LARGE_INTEGER,
    Key: ?*const ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtSetInformationFile(
    FileHandle: HANDLE,
    IoStatusBlock: *IO_STATUS_BLOCK,
    /// This can't be const as providing read-only memory could result in ACCESS_VIOLATION
    /// in certain scenarios. This has been seen when using FILE_DISPOSITION_INFORMATION_EX
    /// and targeting x86-windows.
    FileInformation: *anyopaque,
    Length: ULONG,
    FileInformationClass: FILE.INFORMATION_CLASS,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtWriteFile(
    FileHandle: HANDLE,
    Event: ?HANDLE,
    ApcRoutine: ?*align(2) const IO_APC_ROUTINE,
    ApcContext: ?*anyopaque,
    IoStatusBlock: *IO_STATUS_BLOCK,
    Buffer: *const anyopaque,
    Length: ULONG,
    ByteOffset: ?*const LARGE_INTEGER,
    Key: ?*const ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtUnlockFile(
    FileHandle: HANDLE,
    IoStatusBlock: *IO_STATUS_BLOCK,
    ByteOffset: *const LARGE_INTEGER,
    Length: *const LARGE_INTEGER,
    Key: ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtQueryObject(
    Handle: HANDLE,
    ObjectInformationClass: OBJECT.INFORMATION_CLASS,
    ObjectInformation: ?PVOID,
    ObjectInformationLength: ULONG,
    ReturnLength: ?*ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtClose(
    Handle: HANDLE,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtCreateSection(
    SectionHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: ?*const OBJECT.ATTRIBUTES,
    MaximumSize: ?*const LARGE_INTEGER,
    SectionPageProtection: PAGE,
    AllocationAttributes: SEC,
    FileHandle: ?HANDLE,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtExtendSection(
    SectionHandle: HANDLE,
    NewSectionSize: *LARGE_INTEGER,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtAllocateVirtualMemory(
    ProcessHandle: HANDLE,
    BaseAddress: *PVOID,
    ZeroBits: ULONG_PTR,
    RegionSize: *SIZE_T,
    AllocationType: MEM.ALLOCATE,
    Protect: PAGE,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtFreeVirtualMemory(
    ProcessHandle: HANDLE,
    BaseAddress: *PVOID,
    RegionSize: *SIZE_T,
    FreeType: MEM.FREE,
) callconv(.winapi) NTSTATUS;

// ref: km/wdm.h

pub extern "ntdll" fn RtlQueryRegistryValues(
    RelativeTo: ULONG,
    Path: PCWSTR,
    QueryTable: [*]RTL_QUERY_REGISTRY_TABLE,
    Context: ?*const anyopaque,
    Environment: ?*const anyopaque,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn RtlEqualUnicodeString(
    String1: *const UNICODE_STRING,
    String2: *const UNICODE_STRING,
    CaseInSensitive: BOOLEAN,
) callconv(.winapi) BOOLEAN;

pub extern "ntdll" fn RtlUpcaseUnicodeChar(
    SourceCharacter: u16,
) callconv(.winapi) u16;

pub extern "ntdll" fn RtlFreeUnicodeString(
    UnicodeString: *UNICODE_STRING,
) callconv(.winapi) void;

pub extern "ntdll" fn RtlGetVersion(
    lpVersionInformation: *RTL_OSVERSIONINFOW,
) callconv(.winapi) NTSTATUS;

// ref: um/winnt.h

pub extern "ntdll" fn RtlLookupFunctionEntry(
    ControlPc: usize,
    ImageBase: *usize,
    HistoryTable: *UNWIND_HISTORY_TABLE,
) callconv(.winapi) ?*RUNTIME_FUNCTION;

pub extern "ntdll" fn RtlVirtualUnwind(
    HandlerType: DWORD,
    ImageBase: usize,
    ControlPc: usize,
    FunctionEntry: *RUNTIME_FUNCTION,
    ContextRecord: *CONTEXT,
    HandlerData: *?PVOID,
    EstablisherFrame: *usize,
    ContextPointers: ?*KNONVOLATILE_CONTEXT_POINTERS,
) callconv(.winapi) *EXCEPTION_ROUTINE;

// ref: um/winternl.h

pub extern "ntdll" fn NtWaitForSingleObject(
    Handle: HANDLE,
    Alertable: BOOLEAN,
    Timeout: ?*const LARGE_INTEGER,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtQueryInformationProcess(
    ProcessHandle: HANDLE,
    ProcessInformationClass: PROCESS.INFOCLASS,
    ProcessInformation: *anyopaque,
    ProcessInformationLength: ULONG,
    ReturnLength: ?*ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtQueryInformationThread(
    ThreadHandle: HANDLE,
    ThreadInformationClass: THREAD.INFOCLASS,
    ThreadInformation: *anyopaque,
    ThreadInformationLength: ULONG,
    ReturnLength: ?*ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtQuerySystemInformation(
    SystemInformationClass: SYSTEM.INFORMATION_CLASS,
    SystemInformation: PVOID,
    SystemInformationLength: ULONG,
    ReturnLength: ?*ULONG,
) callconv(.winapi) NTSTATUS;

// ref none

pub extern "ntdll" fn RtlGetActiveActivationContext(
    ActivationContext: *?HANDLE,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn RtlActivateActivationContextEx(
    Flags: ULONG,
    Teb: *TEB,
    ActivationContext: HANDLE,
    Cookie: *ULONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn RtlReleaseActivationContext(
    ActivationContext: HANDLE,
) callconv(.winapi) void;

pub extern "ntdll" fn LdrAddRefDll(
    Flags: ULONG,
    DllHandle: PVOID,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrLoadDll(
    DllPath: ?PCWSTR,
    DllCharacteristics: ?*const ULONG,
    DllName: *const UNICODE_STRING,
    DllHandle: *PVOID,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrUnloadDll(
    DllHandle: PVOID,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn LdrFindEntryForAddress(
    DllHandle: PVOID,
    Entry: **LDR.DATA_TABLE_ENTRY,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrGetDllFullName(
    DllHandle: ?PVOID,
    FullDllName: *UNICODE_STRING,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrGetDllPath(
    DllName: PCWSTR,
    Flags: LDR.LOAD,
    DllPath: *PWSTR,
    SearchPaths: *PWSTR,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn LdrGetDllHandle(
    DllPath: ?PCWSTR,
    DllCharacteristics: ?*const ULONG,
    DllName: *const UNICODE_STRING,
    DllHandle: *PVOID,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrGetDllHandleByMapping(
    BaseAddress: PVOID,
    DllHandle: *PVOID,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrGetDllHandleByName(
    BaseDllName: *const UNICODE_STRING,
    FullDllName: *const UNICODE_STRING,
    DllHandle: *PVOID,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrGetDllHandleEx(
    Flags: LDR.GET_DLL_HANDLE_EX,
    DllPath: ?PCWSTR,
    DllCharacteristics: ?*const ULONG,
    DllName: *const UNICODE_STRING,
    DllHandle: *PVOID,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn LdrGetProcedureAddress(
    DllHandle: PVOID,
    ProcedureName: *const ANSI_STRING,
    ProcedureNumber: ULONG,
    ProcedureAddress: *PVOID,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrGetProcedureAddressEx(
    DllHandle: PVOID,
    ProcedureName: *const ANSI_STRING,
    ProcedureNumber: ULONG,
    ProcedureAddress: *PVOID,
    Flags: LDR.GET_PROCEDURE_ADDRESS,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrGetProcedureAddressForCaller(
    DllHandle: PVOID,
    ProcedureName: *const ANSI_STRING,
    ProcedureNumber: ULONG,
    ProcedureAddress: *PVOID,
    Flags: LDR.GET_PROCEDURE_ADDRESS,
    CallerAddress: PVOID,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn LdrRegisterDllNotification(
    Flags: LDR.DLL_NOTIFICATION.REGISTER,
    NotificationFunction: *const LDR.DLL_NOTIFICATION.FUNCTION,
    Context: ?PVOID,
    Cookie: *LDR.DLL_NOTIFICATION.COOKIE,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn LdrUnregisterDllNotification(
    Cookie: LDR.DLL_NOTIFICATION.COOKIE,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtQueryAttributesFile(
    ObjectAttributes: *const OBJECT.ATTRIBUTES,
    FileAttributes: *FILE.BASIC_INFORMATION,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtCreateEvent(
    EventHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: ?*const OBJECT.ATTRIBUTES,
    EventType: EVENT_TYPE,
    InitialState: BOOLEAN,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtSetEvent(
    EventHandle: HANDLE,
    PreviousState: ?*LONG,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtCreateKeyedEvent(
    KeyedEventHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: ?*const OBJECT.ATTRIBUTES,
    Flags: ULONG,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtReleaseKeyedEvent(
    EventHandle: ?HANDLE,
    Key: ?*const anyopaque,
    Alertable: BOOLEAN,
    Timeout: ?*const LARGE_INTEGER,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtWaitForKeyedEvent(
    EventHandle: ?HANDLE,
    Key: ?*const anyopaque,
    Alertable: BOOLEAN,
    Timeout: ?*const LARGE_INTEGER,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtCancelSynchronousIoFile(
    ThreadHandle: HANDLE,
    IoRequestToCancel: ?*IO_STATUS_BLOCK,
    IoStatusBlock: *IO_STATUS_BLOCK,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtCancelIoFile(
    FileHandle: HANDLE,
    IoStatusBlock: *IO_STATUS_BLOCK,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtCancelIoFileEx(
    FileHandle: HANDLE,
    IoRequestToCancel: *const IO_STATUS_BLOCK,
    IoStatusBlock: *IO_STATUS_BLOCK,
) callconv(.winapi) NTSTATUS;

/// This function has been observed to return SUCCESS on timeout on Windows 10
/// and TIMEOUT on Wine 10.0.
///
/// This function has been observed on Windows 11 such that positive interval
/// is real time, which can cause waits to be interrupted by changing system
/// time, however negative intervals are not affected by changes to system
/// time.
pub extern "ntdll" fn NtDelayExecution(
    Alertable: BOOLEAN,
    DelayInterval: *const LARGE_INTEGER,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtNotifyChangeDirectoryFileEx(
    FileHandle: HANDLE,
    Event: ?HANDLE,
    ApcRoutine: ?*align(2) const IO_APC_ROUTINE,
    ApcContext: ?*anyopaque,
    IoStatusBlock: *IO_STATUS_BLOCK,
    Buffer: *anyopaque,
    Length: ULONG,
    CompletionFilter: FILE.NOTIFY.CHANGE,
    WatchTree: BOOLEAN,
    DirectoryNotifyInformationClass: DIRECTORY.NOTIFY_INFORMATION_CLASS,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtOpenThread(
    ThreadHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: *const OBJECT.ATTRIBUTES,
    ClientId: *const windows.CLIENT_ID,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtCreateNamedPipeFile(
    FileHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: *const OBJECT.ATTRIBUTES,
    IoStatusBlock: *IO_STATUS_BLOCK,
    ShareAccess: FILE.SHARE,
    CreateDisposition: FILE.CREATE_DISPOSITION,
    CreateOptions: FILE.MODE,
    NamedPipeType: FILE.PIPE.TYPE,
    ReadMode: FILE.PIPE.READ_MODE,
    CompletionMode: FILE.PIPE.COMPLETION_MODE,
    MaximumInstances: ULONG,
    InboundQuota: ULONG,
    OutboundQuota: ULONG,
    DefaultTimeout: ?*const LARGE_INTEGER,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtFlushBuffersFile(
    FileHandle: HANDLE,
    IoStatusBlock: *IO_STATUS_BLOCK,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtMapViewOfSection(
    SectionHandle: HANDLE,
    ProcessHandle: HANDLE,
    BaseAddress: ?*PVOID,
    ZeroBits: ?*const ULONG,
    CommitSize: SIZE_T,
    SectionOffset: ?*LARGE_INTEGER,
    ViewSize: *SIZE_T,
    InheritDispostion: SECTION_INHERIT,
    AllocationType: MEM.MAP,
    PageProtection: PAGE,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtUnmapViewOfSection(
    ProcessHandle: HANDLE,
    BaseAddress: PVOID,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtUnmapViewOfSectionEx(
    ProcessHandle: HANDLE,
    BaseAddress: PVOID,
    UnmapFlags: MEM.UNMAP,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtOpenKey(
    KeyHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: *const OBJECT.ATTRIBUTES,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtQueueApcThread(
    ThreadHandle: HANDLE,
    ApcRoutine: *const IO_APC_ROUTINE,
    ApcArgument1: ?*anyopaque,
    ApcArgument2: ?*anyopaque,
    ApcArgument3: ?*anyopaque,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtReadVirtualMemory(
    ProcessHandle: HANDLE,
    BaseAddress: ?PVOID,
    Buffer: LPVOID,
    NumberOfBytesToRead: SIZE_T,
    NumberOfBytesRead: ?*SIZE_T,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtWriteVirtualMemory(
    ProcessHandle: HANDLE,
    BaseAddress: ?PVOID,
    Buffer: LPCVOID,
    NumberOfBytesToWrite: SIZE_T,
    NumberOfBytesWritten: ?*SIZE_T,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtProtectVirtualMemory(
    ProcessHandle: HANDLE,
    BaseAddress: *?PVOID,
    NumberOfBytesToProtect: *SIZE_T,
    NewAccessProtection: PAGE,
    OldAccessProtection: *PAGE,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtWaitForAlertByThreadId(
    Address: ?*const anyopaque,
    Timeout: ?*const LARGE_INTEGER,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtAlertThreadByThreadId(ThreadId: DWORD) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtAlertThread(ThreadHandle: HANDLE) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtAlertMultipleThreadByThreadId(
    ThreadIds: [*]const ULONG_PTR,
    ThreadCount: ULONG,
    Unknown1: ?*const anyopaque,
    Unknown2: ?*const anyopaque,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtYieldExecution() callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn RtlAddVectoredExceptionHandler(
    First: ULONG,
    Handler: ?VECTORED_EXCEPTION_HANDLER,
) callconv(.winapi) ?LPVOID;
pub extern "ntdll" fn RtlRemoveVectoredExceptionHandler(
    Handle: HANDLE,
) callconv(.winapi) ULONG;

pub extern "ntdll" fn RtlDosPathNameToNtPathName_U(
    DosPathName: [*:0]const u16,
    NtPathName: *UNICODE_STRING,
    NtFileNamePart: ?*?[*:0]const u16,
    DirectoryInfo: ?*CURDIR,
) callconv(.winapi) BOOL;

pub extern "ntdll" fn RtlExitUserProcess(
    ExitStatus: u32,
) callconv(.winapi) noreturn;

/// Returns the number of bytes written to `Buffer`.
/// If the returned count is larger than `BufferByteLength`, the buffer was too small.
/// If the returned count is zero, an error occurred.
pub extern "ntdll" fn RtlGetFullPathName_U(
    FileName: [*:0]const u16,
    BufferByteLength: ULONG,
    Buffer: [*]u16,
    ShortName: ?*[*:0]const u16,
) callconv(.winapi) ULONG;

pub extern "ntdll" fn RtlGetCurrentDirectory_U(
    BufferByteLength: ULONG,
    Buffer: [*]u16,
) callconv(.winapi) ULONG;

pub extern "ntdll" fn RtlGetSystemTimePrecise() callconv(.winapi) LARGE_INTEGER;

pub extern "ntdll" fn RtlInitializeCriticalSection(
    lpCriticalSection: *CRITICAL_SECTION,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn RtlEnterCriticalSection(
    lpCriticalSection: *CRITICAL_SECTION,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn RtlLeaveCriticalSection(
    lpCriticalSection: *CRITICAL_SECTION,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn RtlDeleteCriticalSection(
    lpCriticalSection: *CRITICAL_SECTION,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn RtlQueryPerformanceCounter(
    PerformanceCounter: *LARGE_INTEGER,
) callconv(.winapi) BOOL;
pub extern "ntdll" fn RtlQueryPerformanceFrequency(
    PerformanceFrequency: *LARGE_INTEGER,
) callconv(.winapi) BOOL;

pub extern "ntdll" fn RtlReAllocateHeap(
    HeapHandle: *HEAP,
    Flags: HEAP.FLAGS.ALLOCATION,
    BaseAddress: ?PVOID,
    Size: SIZE_T,
) callconv(.winapi) ?PVOID;

pub extern "ntdll" fn RtlReportSilentProcessExit(
    ProcessHandle: HANDLE,
    ExitStatus: NTSTATUS,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtTerminateProcess(
    ProcessHandle: ?HANDLE,
    ExitStatus: NTSTATUS,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn RtlSetCurrentDirectory_U(
    PathName: *const UNICODE_STRING,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn RtlTryAcquireSRWLockExclusive(
    SRWLock: *SRWLOCK,
) callconv(.winapi) BOOLEAN;
pub extern "ntdll" fn RtlAcquireSRWLockExclusive(
    SRWLock: *SRWLOCK,
) callconv(.winapi) void;
pub extern "ntdll" fn RtlReleaseSRWLockExclusive(
    SRWLock: *SRWLOCK,
) callconv(.winapi) void;

pub extern "ntdll" fn RtlWakeAddressAll(
    Address: ?*const anyopaque,
) callconv(.winapi) void;
pub extern "ntdll" fn RtlWakeAddressSingle(
    Address: ?*const anyopaque,
) callconv(.winapi) void;
pub extern "ntdll" fn RtlWaitOnAddress(
    Address: ?*const anyopaque,
    CompareAddress: ?*const anyopaque,
    AddressSize: SIZE_T,
    Timeout: ?*const LARGE_INTEGER,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn RtlWakeConditionVariable(
    ConditionVariable: *CONDITION_VARIABLE,
) callconv(.winapi) void;
pub extern "ntdll" fn RtlWakeAllConditionVariable(
    ConditionVariable: *CONDITION_VARIABLE,
) callconv(.winapi) void;

pub extern "ntdll" fn NtOpenKeyEx(
    KeyHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: *const OBJECT.ATTRIBUTES,
    OpenOptions: REG.OpenOptions,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn RtlOpenCurrentUser(
    DesiredAccess: ACCESS_MASK,
    CurrentUserKey: *HANDLE,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtQueryValueKey(
    KeyHandle: HANDLE,
    ValueName: *const UNICODE_STRING,
    KeyValueInformationClass: KEY.VALUE.INFORMATION_CLASS,
    KeyValueInformation: *anyopaque,
    /// Length of KeyValueInformation buffer in bytes
    Length: ULONG,
    /// On STATUS_SUCCESS, contains the length of the populated portion of the
    /// provided buffer. On STATUS_BUFFER_OVERFLOW or STATUS_BUFFER_TOO_SMALL,
    /// contains the minimum `Length` value that would be required to hold the information.
    ResultLength: *ULONG,
) callconv(.winapi) NTSTATUS;
pub extern "ntdll" fn NtLoadKeyEx(
    TargetKey: *const OBJECT.ATTRIBUTES,
    SourceFile: *const OBJECT.ATTRIBUTES,
    Flags: REG.LoadOptions,
    TrustClassKey: ?HANDLE,
    Event: ?HANDLE,
    DesiredAccess: ACCESS_MASK,
    RootHandle: ?*HANDLE,
    Reserved: ?*anyopaque,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtCreateThreadEx(
    ThreadHandle: *HANDLE,
    DesiredAccess: ACCESS_MASK,
    ObjectAttributes: *const OBJECT.ATTRIBUTES,
    ProcessHandle: HANDLE,
    StartRoutine: *const USER_THREAD_START_ROUTINE,
    Argument: ?PVOID,
    CreateFlags: THREAD.CREATE_FLAGS,
    ZeroBits: SIZE_T,
    /// This value is rounded up to the nearest page.
    /// If this value is larger than `StackReserve`, the reserved stack
    /// size will be the rounded value of this parameter.
    /// https://learn.microsoft.com/en-us/windows/win32/procthread/thread-stack-size
    StackCommit: THREAD.StackSize,
    StackReserve: THREAD.StackSize,
    AttributeList: ?*PS.ATTRIBUTE.LIST,
) callconv(.winapi) NTSTATUS;

pub extern "ntdll" fn NtResumeThread(
    ThreadHandle: HANDLE,
    PreviousSuspendCount: ?*ULONG,
) callconv(.winapi) NTSTATUS;



---
File: /std/os/windows/ntstatus.zig
---

/// NTSTATUS codes from https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/596a1078-e883-4972-9bbc-49e60bebca55?
pub const NTSTATUS = enum(u32) {
    /// The caller specified WaitAny for WaitType and one of the dispatcher
    /// objects in the Object array has been set to the signaled state.
    pub const WAIT_0: NTSTATUS = .SUCCESS;
    /// The caller attempted to wait for a mutex that has been abandoned.
    pub const ABANDONED_WAIT_0: NTSTATUS = .ABANDONED;
    /// The maximum number of boot-time filters has been reached.
    pub const FWP_TOO_MANY_BOOTTIME_FILTERS: NTSTATUS = .FWP_TOO_MANY_CALLOUTS;

    /// The operation completed successfully.
    SUCCESS = 0x00000000,
    /// The caller specified WaitAny for WaitType and one of the dispatcher objects in the Object array has been set to the signaled state.
    WAIT_1 = 0x00000001,
    /// The caller specified WaitAny for WaitType and one of the dispatcher objects in the Object array has been set to the signaled state.
    WAIT_2 = 0x00000002,
    /// The caller specified WaitAny for WaitType and one of the dispatcher objects in the Object array has been set to the signaled state.
    WAIT_3 = 0x00000003,
    /// The caller specified WaitAny for WaitType and one of the dispatcher objects in the Object array has been set to the signaled state.
    WAIT_63 = 0x0000003F,
    /// The caller attempted to wait for a mutex that has been abandoned.
    ABANDONED = 0x00000080,
    /// The caller attempted to wait for a mutex that has been abandoned.
    ABANDONED_WAIT_63 = 0x000000BF,
    /// A user-mode APC was delivered before the given Interval expired.
    USER_APC = 0x000000C0,
    /// The delay completed because the thread was alerted.
    ALERTED = 0x00000101,
    /// The given Timeout interval expired.
    TIMEOUT = 0x00000102,
    /// The operation that was requested is pending completion.
    PENDING = 0x00000103,
    /// A reparse should be performed by the Object Manager because the name of the file resulted in a symbolic link.
    REPARSE = 0x00000104,
    /// Returned by enumeration APIs to indicate more information is available to successive calls.
    MORE_ENTRIES = 0x00000105,
    /// Indicates not all privileges or groups that are referenced are assigned to the caller.
    /// This allows, for example, all privileges to be disabled without having to know exactly which privileges are assigned.
    NOT_ALL_ASSIGNED = 0x00000106,
    /// Some of the information to be translated has not been translated.
    SOME_NOT_MAPPED = 0x00000107,
    /// An open/create operation completed while an opportunistic lock (oplock) break is underway.
    OPLOCK_BREAK_IN_PROGRESS = 0x00000108,
    /// A new volume has been mounted by a file system.
    VOLUME_MOUNTED = 0x00000109,
    /// This success level status indicates that the transaction state already exists for the registry subtree but that a transaction commit was previously aborted. The commit has now been completed.
    RXACT_COMMITTED = 0x0000010A,
    /// Indicates that a notify change request has been completed due to closing the handle that made the notify change request.
    NOTIFY_CLEANUP = 0x0000010B,
    /// Indicates that a notify change request is being completed and that the information is not being returned in the caller's buffer.
    /// The caller now needs to enumerate the files to find the changes.
    NOTIFY_ENUM_DIR = 0x0000010C,
    /// {No Quotas} No system quota limits are specifically set for this account.
    NO_QUOTAS_FOR_ACCOUNT = 0x0000010D,
    /// {Connect Failure on Primary Transport} An attempt was made to connect to the remote server %hs on the primary transport, but the connection failed.
    /// The computer WAS able to connect on a secondary transport.
    PRIMARY_TRANSPORT_CONNECT_FAILED = 0x0000010E,
    /// The page fault was a transition fault.
    PAGE_FAULT_TRANSITION = 0x00000110,
    /// The page fault was a demand zero fault.
    PAGE_FAULT_DEMAND_ZERO = 0x00000111,
    /// The page fault was a demand zero fault.
    PAGE_FAULT_COPY_ON_WRITE = 0x00000112,
    /// The page fault was a demand zero fault.
    PAGE_FAULT_GUARD_PAGE = 0x00000113,
    /// The page fault was satisfied by reading from a secondary storage device.
    PAGE_FAULT_PAGING_FILE = 0x00000114,
    /// The cached page was locked during operation.
    CACHE_PAGE_LOCKED = 0x00000115,
    /// The crash dump exists in a paging file.
    CRASH_DUMP = 0x00000116,
    /// The specified buffer contains all zeros.
    BUFFER_ALL_ZEROS = 0x00000117,
    /// A reparse should be performed by the Object Manager because the name of the file resulted in a symbolic link.
    REPARSE_OBJECT = 0x00000118,
    /// The device has succeeded a query-stop and its resource requirements have changed.
    RESOURCE_REQUIREMENTS_CHANGED = 0x00000119,
    /// The translator has translated these resources into the global space and no additional translations should be performed.
    TRANSLATION_COMPLETE = 0x00000120,
    /// The directory service evaluated group memberships locally, because it was unable to contact a global catalog server.
    DS_MEMBERSHIP_EVALUATED_LOCALLY = 0x00000121,
    /// A process being terminated has no threads to terminate.
    NOTHING_TO_TERMINATE = 0x00000122,
    /// The specified process is not part of a job.
    PROCESS_NOT_IN_JOB = 0x00000123,
    /// The specified process is part of a job.
    PROCESS_IN_JOB = 0x00000124,
    /// {Volume Shadow Copy Service} The system is now ready for hibernation.
    VOLSNAP_HIBERNATE_READY = 0x00000125,
    /// A file system or file system filter driver has successfully completed an FsFilter operation.
    FSFILTER_OP_COMPLETED_SUCCESSFULLY = 0x00000126,
    /// The specified interrupt vector was already connected.
    INTERRUPT_VECTOR_ALREADY_CONNECTED = 0x00000127,
    /// The specified interrupt vector is still connected.
    INTERRUPT_STILL_CONNECTED = 0x00000128,
    /// The current process is a cloned process.
    PROCESS_CLONED = 0x00000129,
    /// The file was locked and all users of the file can only read.
    FILE_LOCKED_WITH_ONLY_READERS = 0x0000012A,
    /// The file was locked and at least one user of the file can write.
    FILE_LOCKED_WITH_WRITERS = 0x0000012B,
    /// The specified ResourceManager made no changes or updates to the resource under this transaction.
    RESOURCEMANAGER_READ_ONLY = 0x00000202,
    /// An operation is blocked and waiting for an oplock.
    WAIT_FOR_OPLOCK = 0x00000367,
    /// Debugger handled the exception.
    DBG_EXCEPTION_HANDLED = 0x00010001,
    /// The debugger continued.
    DBG_CONTINUE = 0x00010002,
    /// The IO was completed by a filter.
    FLT_IO_COMPLETE = 0x001C0001,
    /// The file is temporarily unavailable.
    FILE_NOT_AVAILABLE = 0xC0000467,
    /// The share is temporarily unavailable.
    SHARE_UNAVAILABLE = 0xC0000480,
    /// A threadpool worker thread entered a callback at thread affinity %p and exited at affinity %p.
    /// This is unexpected, indicating that the callback missed restoring the priority.
    CALLBACK_RETURNED_THREAD_AFFINITY = 0xC0000721,
    /// {Object Exists} An attempt was made to create an object but the object name already exists.
    OBJECT_NAME_EXISTS = 0x40000000,
    /// {Thread Suspended} A thread termination occurred while the thread was suspended. The thread resumed, and termination proceeded.
    THREAD_WAS_SUSPENDED = 0x40000001,
    /// {Working Set Range Error} An attempt was made to set the working set minimum or maximum to values that are outside the allowable range.
    WORKING_SET_LIMIT_RANGE = 0x40000002,
    /// {Image Relocated} An image file could not be mapped at the address that is specified in the image file. Local fixes must be performed on this image.
    IMAGE_NOT_AT_BASE = 0x40000003,
    /// This informational level status indicates that a specified registry subtree transaction state did not yet exist and had to be created.
    RXACT_STATE_CREATED = 0x40000004,
    /// {Segment Load} A virtual DOS machine (VDM) is loading, unloading, or moving an MS-DOS or Win16 program segment image.
    /// An exception is raised so that a debugger can load, unload, or track symbols and breakpoints within these 16-bit segments.
    SEGMENT_NOTIFICATION = 0x40000005,
    /// {Local Session Key} A user session key was requested for a local remote procedure call (RPC) connection.
    /// The session key that is returned is a constant value and not unique to this connection.
    LOCAL_USER_SESSION_KEY = 0x40000006,
    /// {Invalid Current Directory} The process cannot switch to the startup current directory %hs.
    /// Select OK to set the current directory to %hs, or select CANCEL to exit.
    BAD_CURRENT_DIRECTORY = 0x40000007,
    /// {Serial IOCTL Complete} A serial I/O operation was completed by another write to a serial port. (The IOCTL_SERIAL_XOFF_COUNTER reached zero.)
    SERIAL_MORE_WRITES = 0x40000008,
    /// {Registry Recovery} One of the files that contains the system registry data had to be recovered by using a log or alternate copy. The recovery was successful.
    REGISTRY_RECOVERED = 0x40000009,
    /// {Redundant Read} To satisfy a read request, the Windows NT operating system fault-tolerant file system successfully read the requested data from a redundant copy.
    /// This was done because the file system encountered a failure on a member of the fault-tolerant volume but was unable to reassign the failing area of the device.
    FT_READ_RECOVERY_FROM_BACKUP = 0x4000000A,
    /// {Redundant Write} To satisfy a write request, the Windows NT fault-tolerant file system successfully wrote a redundant copy of the information.
    /// This was done because the file system encountered a failure on a member of the fault-tolerant volume but was unable to reassign the failing area of the device.
    FT_WRITE_RECOVERY = 0x4000000B,
    /// {Serial IOCTL Timeout} A serial I/O operation completed because the time-out period expired.
    /// (The IOCTL_SERIAL_XOFF_COUNTER had not reached zero.)
    SERIAL_COUNTER_TIMEOUT = 0x4000000C,
    /// {Password Too Complex} The Windows password is too complex to be converted to a LAN Manager password.
    /// The LAN Manager password that returned is a NULL string.
    NULL_LM_PASSWORD = 0x4000000D,
    /// {Machine Type Mismatch} The image file %hs is valid but is for a machine type other than the current machine.
    /// Select OK to continue, or CANCEL to fail the DLL load.
    IMAGE_MACHINE_TYPE_MISMATCH = 0x4000000E,
    /// {Partial Data Received} The network transport returned partial data to its client. The remaining data will be sent later.
    RECEIVE_PARTIAL = 0x4000000F,
    /// {Expedited Data Received} The network transport returned data to its client that was marked as expedited by the remote system.
    RECEIVE_EXPEDITED = 0x40000010,
    /// {Partial Expedited Data Received} The network transport returned partial data to its client and this data was marked as expedited by the remote system. The remaining data will be sent later.
    RECEIVE_PARTIAL_EXPEDITED = 0x40000011,
    /// {TDI Event Done} The TDI indication has completed successfully.
    EVENT_DONE = 0x40000012,
    /// {TDI Event Pending} The TDI indication has entered the pending state.
    EVENT_PENDING = 0x40000013,
    /// Checking file system on %wZ.
    CHECKING_FILE_SYSTEM = 0x40000014,
    /// {Fatal Application Exit} %hs
    FATAL_APP_EXIT = 0x40000015,
    /// The specified registry key is referenced by a predefined handle.
    PREDEFINED_HANDLE = 0x40000016,
    /// {Page Unlocked} The page protection of a locked page was changed to 'No Access' and the page was unlocked from memory and from the process.
    WAS_UNLOCKED = 0x40000017,
    /// %hs
    SERVICE_NOTIFICATION = 0x40000018,
    /// {Page Locked} One of the pages to lock was already locked.
    WAS_LOCKED = 0x40000019,
    /// Application popup: %1 : %2
    LOG_HARD_ERROR = 0x4000001A,
    /// A Win32 process already exists.
    ALREADY_WIN32 = 0x4000001B,
    /// An exception status code that is used by the Win32 x86 emulation subsystem.
    WX86_UNSIMULATE = 0x4000001C,
    /// An exception status code that is used by the Win32 x86 emulation subsystem.
    WX86_CONTINUE = 0x4000001D,
    /// An exception status code that is used by the Win32 x86 emulation subsystem.
    WX86_SINGLE_STEP = 0x4000001E,
    /// An exception status code that is used by the Win32 x86 emulation subsystem.
    WX86_BREAKPOINT = 0x4000001F,
    /// An exception status code that is used by the Win32 x86 emulation subsystem.
    WX86_EXCEPTION_CONTINUE = 0x40000020,
    /// An exception status code that is used by the Win32 x86 emulation subsystem.
    WX86_EXCEPTION_LASTCHANCE = 0x40000021,
    /// An exception status code that is used by the Win32 x86 emulation subsystem.
    WX86_EXCEPTION_CHAIN = 0x40000022,
    /// {Machine Type Mismatch} The image file %hs is valid but is for a machine type other than the current machine.
    IMAGE_MACHINE_TYPE_MISMATCH_EXE = 0x40000023,
    /// A yield execution was performed and no thread was available to run.
    NO_YIELD_PERFORMED = 0x40000024,
    /// The resume flag to a timer API was ignored.
    TIMER_RESUME_IGNORED = 0x40000025,
    /// The arbiter has deferred arbitration of these resources to its parent.
    ARBITRATION_UNHANDLED = 0x40000026,
    /// The device has detected a CardBus card in its slot.
    CARDBUS_NOT_SUPPORTED = 0x40000027,
    /// An exception status code that is used by the Win32 x86 emulation subsystem.
    WX86_CREATEWX86TIB = 0x40000028,
    /// The CPUs in this multiprocessor system are not all the same revision level.
    /// To use all processors, the operating system restricts itself to the features of the least capable processor in the system.
    /// If problems occur with this system, contact the CPU manufacturer to see if this mix of processors is supported.
    MP_PROCESSOR_MISMATCH = 0x40000029,
    /// The system was put into hibernation.
    HIBERNATED = 0x4000002A,
    /// The system was resumed from hibernation.
    RESUME_HIBERNATION = 0x4000002B,
    /// Windows has detected that the system firmware (BIOS) was updated [previous firmware date = %2, current firmware date %3].
    FIRMWARE_UPDATED = 0x4000002C,
    /// A device driver is leaking locked I/O pages and is causing system degradation.
    /// The system has automatically enabled the tracking code to try and catch the culprit.
    DRIVERS_LEAKING_LOCKED_PAGES = 0x4000002D,
    /// The ALPC message being canceled has already been retrieved from the queue on the other side.
    MESSAGE_RETRIEVED = 0x4000002E,
    /// The system power state is transitioning from %2 to %3.
    SYSTEM_POWERSTATE_TRANSITION = 0x4000002F,
    /// The receive operation was successful.
    /// Check the ALPC completion list for the received message.
    ALPC_CHECK_COMPLETION_LIST = 0x40000030,
    /// The system power state is transitioning from %2 to %3 but could enter %4.
    SYSTEM_POWERSTATE_COMPLEX_TRANSITION = 0x40000031,
    /// Access to %1 is monitored by policy rule %2.
    ACCESS_AUDIT_BY_POLICY = 0x40000032,
    /// A valid hibernation file has been invalidated and should be abandoned.
    ABANDON_HIBERFILE = 0x40000033,
    /// Business rule scripts are disabled for the calling application.
    BIZRULES_NOT_ENABLED = 0x40000034,
    /// The system has awoken.
    WAKE_SYSTEM = 0x40000294,
    /// The directory service is shutting down.
    DS_SHUTTING_DOWN = 0x40000370,
    /// Debugger will reply later.
    DBG_REPLY_LATER = 0x40010001,
    /// Debugger cannot provide a handle.
    DBG_UNABLE_TO_PROVIDE_HANDLE = 0x40010002,
    /// Debugger terminated the thread.
    DBG_TERMINATE_THREAD = 0x40010003,
    /// Debugger terminated the process.
    DBG_TERMINATE_PROCESS = 0x40010004,
    /// Debugger obtained control of C.
    DBG_CONTROL_C = 0x40010005,
    /// Debugger printed an exception on control C.
    DBG_PRINTEXCEPTION_C = 0x40010006,
    /// Debugger received a RIP exception.
    DBG_RIPEXCEPTION = 0x40010007,
    /// Debugger received a control break.
    DBG_CONTROL_BREAK = 0x40010008,
    /// Debugger command communication exception.
    DBG_COMMAND_EXCEPTION = 0x40010009,
    /// A UUID that is valid only on this computer has been allocated.
    RPC_NT_UUID_LOCAL_ONLY = 0x40020056,
    /// Some data remains to be sent in the request buffer.
    RPC_NT_SEND_INCOMPLETE = 0x400200AF,
    /// The Client Drive Mapping Service has connected on Terminal Connection.
    CTX_CDM_CONNECT = 0x400A0004,
    /// The Client Drive Mapping Service has disconnected on Terminal Connection.
    CTX_CDM_DISCONNECT = 0x400A0005,
    /// A kernel mode component is releasing a reference on an activation context.
    SXS_RELEASE_ACTIVATION_CONTEXT = 0x4015000D,
    /// The transactional resource manager is already consistent. Recovery is not needed.
    RECOVERY_NOT_NEEDED = 0x40190034,
    /// The transactional resource manager has already been started.
    RM_ALREADY_STARTED = 0x40190035,
    /// The log service encountered a log stream with no restart area.
    LOG_NO_RESTART = 0x401A000C,
    /// {Display Driver Recovered From Failure} The %hs display driver has detected a failure and recovered from it. Some graphical operations might have failed.
    /// The next time you restart the machine, a dialog box appears, giving you an opportunity to upload data about this failure to Microsoft.
    VIDEO_DRIVER_DEBUG_REPORT_REQUEST = 0x401B00EC,
    /// The specified buffer is not big enough to contain the entire requested dataset.
    /// Partial data is populated up to the size of the buffer.
    /// The caller needs to provide a buffer of the size as specified in the partially populated buffer's content (interface specific).
    GRAPHICS_PARTIAL_DATA_POPULATED = 0x401E000A,
    /// The kernel driver detected a version mismatch between it and the user mode driver.
    GRAPHICS_DRIVER_MISMATCH = 0x401E0117,
    /// No mode is pinned on the specified VidPN source/target.
    GRAPHICS_MODE_NOT_PINNED = 0x401E0307,
    /// The specified mode set does not specify a preference for one of its modes.
    GRAPHICS_NO_PREFERRED_MODE = 0x401E031E,
    /// The specified dataset (for example, mode set, frequency range set, descriptor set, or topology) is empty.
    GRAPHICS_DATASET_IS_EMPTY = 0x401E034B,
    /// The specified dataset (for example, mode set, frequency range set, descriptor set, or topology) does not contain any more elements.
    GRAPHICS_NO_MORE_ELEMENTS_IN_DATASET = 0x401E034C,
    /// The specified content transformation is not pinned on the specified VidPN present path.
    GRAPHICS_PATH_CONTENT_GEOMETRY_TRANSFORMATION_NOT_PINNED = 0x401E0351,
    /// The child device presence was not reliably detected.
    GRAPHICS_UNKNOWN_CHILD_STATUS = 0x401E042F,
    /// Starting the lead adapter in a linked configuration has been temporarily deferred.
    GRAPHICS_LEADLINK_START_DEFERRED = 0x401E0437,
    /// The display adapter is being polled for children too frequently at the same polling level.
    GRAPHICS_POLLING_TOO_FREQUENTLY = 0x401E0439,
    /// Starting the adapter has been temporarily deferred.
    GRAPHICS_START_DEFERRED = 0x401E043A,
    /// The request will be completed later by an NDIS status indication.
    NDIS_INDICATION_REQUIRED = 0x40230001,
    /// {EXCEPTION} Guard Page Exception A page of memory that marks the end of a data structure, such as a stack or an array, has been accessed.
    GUARD_PAGE_VIOLATION = 0x80000001,
    /// {EXCEPTION} Alignment Fault A data type misalignment was detected in a load or store instruction.
    DATATYPE_MISALIGNMENT = 0x80000002,
    /// {EXCEPTION} Breakpoint A breakpoint has been reached.
    BREAKPOINT = 0x80000003,
    /// {EXCEPTION} Single Step A single step or trace operation has just been completed.
    SINGLE_STEP = 0x80000004,
    /// {Buffer Overflow} The data was too large to fit into the specified buffer.
    BUFFER_OVERFLOW = 0x80000005,
    /// {No More Files} No more files were found which match the file specification.
    NO_MORE_FILES = 0x80000006,
    /// {Kernel Debugger Awakened} The system debugger was awakened by an interrupt.
    WAKE_SYSTEM_DEBUGGER = 0x80000007,
    /// {Handles Closed} Handles to objects have been automatically closed because of the requested operation.
    HANDLES_CLOSED = 0x8000000A,
    /// {Non-Inheritable ACL} An access control list (ACL) contains no components that can be inherited.
    NO_INHERITANCE = 0x8000000B,
    /// {GUID Substitution} During the translation of a globally unique identifier (GUID) to a Windows security ID (SID), no administratively defined GUID prefix was found.
    /// A substitute prefix was used, which will not compromise system security.
    /// However, this might provide a more restrictive access than intended.
    GUID_SUBSTITUTION_MADE = 0x8000000C,
    /// Because of protection conflicts, not all the requested bytes could be copied.
    PARTIAL_COPY = 0x8000000D,
    /// {Out of Paper} The printer is out of paper.
    DEVICE_PAPER_EMPTY = 0x8000000E,
    /// {Device Power Is Off} The printer power has been turned off.
    DEVICE_POWERED_OFF = 0x8000000F,
    /// {Device Offline} The printer has been taken offline.
    DEVICE_OFF_LINE = 0x80000010,
    /// {Device Busy} The device is currently busy.
    DEVICE_BUSY = 0x80000011,
    /// {No More EAs} No more extended attributes (EAs) were found for the file.
    NO_MORE_EAS = 0x80000012,
    /// {Illegal EA} The specified extended attribute (EA) name contains at least one illegal character.
    INVALID_EA_NAME = 0x80000013,
    /// {Inconsistent EA List} The extended attribute (EA) list is inconsistent.
    EA_LIST_INCONSISTENT = 0x80000014,
    /// {Invalid EA Flag} An invalid extended attribute (EA) flag was set.
    INVALID_EA_FLAG = 0x80000015,
    /// {Verifying Disk} The media has changed and a verify operation is in progress; therefore, no reads or writes can be performed to the device, except those that are used in the verify operation.
    VERIFY_REQUIRED = 0x80000016,
    /// {Too Much Information} The specified access control list (ACL) contained more information than was expected.
    EXTRANEOUS_INFORMATION = 0x80000017,
    /// This warning level status indicates that the transaction state already exists for the registry subtree, but that a transaction commit was previously aborted.
    /// The commit has NOT been completed but has not been rolled back either; therefore, it can still be committed, if needed.
    RXACT_COMMIT_NECESSARY = 0x80000018,
    /// {No More Entries} No more entries are available from an enumeration operation.
    NO_MORE_ENTRIES = 0x8000001A,
    /// {Filemark Found} A filemark was detected.
    FILEMARK_DETECTED = 0x8000001B,
    /// {Media Changed} The media has changed.
    MEDIA_CHANGED = 0x8000001C,
    /// {I/O Bus Reset} An I/O bus reset was detected.
    BUS_RESET = 0x8000001D,
    /// {End of Media} The end of the media was encountered.
    END_OF_MEDIA = 0x8000001E,
    /// The beginning of a tape or partition has been detected.
    BEGINNING_OF_MEDIA = 0x8000001F,
    /// {Media Changed} The media might have changed.
    MEDIA_CHECK = 0x80000020,
    /// A tape access reached a set mark.
    SETMARK_DETECTED = 0x80000021,
    /// During a tape access, the end of the data written is reached.
    NO_DATA_DETECTED = 0x80000022,
    /// The redirector is in use and cannot be unloaded.
    REDIRECTOR_HAS_OPEN_HANDLES = 0x80000023,
    /// The server is in use and cannot be unloaded.
    SERVER_HAS_OPEN_HANDLES = 0x80000024,
    /// The specified connection has already been disconnected.
    ALREADY_DISCONNECTED = 0x80000025,
    /// A long jump has been executed.
    LONGJUMP = 0x80000026,
    /// A cleaner cartridge is present in the tape library.
    CLEANER_CARTRIDGE_INSTALLED = 0x80000027,
    /// The Plug and Play query operation was not successful.
    PLUGPLAY_QUERY_VETOED = 0x80000028,
    /// A frame consolidation has been executed.
    UNWIND_CONSOLIDATE = 0x80000029,
    /// {Registry Hive Recovered} The registry hive (file): %hs was corrupted and it has been recovered. Some data might have been lost.
    REGISTRY_HIVE_RECOVERED = 0x8000002A,
    /// The application is attempting to run executable code from the module %hs. This might be insecure.
    /// An alternative, %hs, is available. Should the application use the secure module %hs?
    DLL_MIGHT_BE_INSECURE = 0x8000002B,
    /// The application is loading executable code from the module %hs.
    /// This is secure but might be incompatible with previous releases of the operating system.
    /// An alternative, %hs, is available. Should the application use the secure module %hs?
    DLL_MIGHT_BE_INCOMPATIBLE = 0x8000002C,
    /// The create operation stopped after reaching a symbolic link.
    STOPPED_ON_SYMLINK = 0x8000002D,
    /// The device has indicated that cleaning is necessary.
    DEVICE_REQUIRES_CLEANING = 0x80000288,
    /// The device has indicated that its door is open. Further operations require it closed and secured.
    DEVICE_DOOR_OPEN = 0x80000289,
    /// Windows discovered a corruption in the file %hs. This file has now been repaired.
    /// Check if any data in the file was lost because of the corruption.
    DATA_LOST_REPAIR = 0x80000803,
    /// Debugger did not handle the exception.
    DBG_EXCEPTION_NOT_HANDLED = 0x80010001,
    /// The cluster node is already up.
    CLUSTER_NODE_ALREADY_UP = 0x80130001,
    /// The cluster node is already down.
    CLUSTER_NODE_ALREADY_DOWN = 0x80130002,
    /// The cluster network is already online.
    CLUSTER_NETWORK_ALREADY_ONLINE = 0x80130003,
    /// The cluster network is already offline.
    CLUSTER_NETWORK_ALREADY_OFFLINE = 0x80130004,
    /// The cluster node is already a member of the cluster.
    CLUSTER_NODE_ALREADY_MEMBER = 0x80130005,
    /// The log could not be set to the requested size.
    COULD_NOT_RESIZE_LOG = 0x80190009,
    /// There is no transaction metadata on the file.
    NO_TXF_METADATA = 0x80190029,
    /// The file cannot be recovered because there is a handle still open on it.
    CANT_RECOVER_WITH_HANDLE_OPEN = 0x80190031,
    /// Transaction metadata is already present on this file and cannot be superseded.
    TXF_METADATA_ALREADY_PRESENT = 0x80190041,
    /// A transaction scope could not be entered because the scope handler has not been initialized.
    TRANSACTION_SCOPE_CALLBACKS_NOT_SET = 0x80190042,
    /// {Display Driver Stopped Responding and recovered} The %hs display driver has stopped working normally. The recovery had been performed.
    VIDEO_HUNG_DISPLAY_DRIVER_THREAD_RECOVERED = 0x801B00EB,
    /// {Buffer too small} The buffer is too small to contain the entry. No information has been written to the buffer.
    FLT_BUFFER_TOO_SMALL = 0x801C0001,
    /// Volume metadata read or write is incomplete.
    FVE_PARTIAL_METADATA = 0x80210001,
    /// BitLocker encryption keys were ignored because the volume was in a transient state.
    FVE_TRANSIENT_STATE = 0x80210002,
    /// {Operation Failed} The requested operation was unsuccessful.
    UNSUCCESSFUL = 0xC0000001,
    /// {Not Implemented} The requested operation is not implemented.
    NOT_IMPLEMENTED = 0xC0000002,
    /// {Invalid Parameter} The specified information class is not a valid information class for the specified object.
    INVALID_INFO_CLASS = 0xC0000003,
    /// The specified information record length does not match the length that is required for the specified information class.
    INFO_LENGTH_MISMATCH = 0xC0000004,
    /// The instruction at 0x%08lx referenced memory at 0x%08lx. The memory could not be %s.
    ACCESS_VIOLATION = 0xC0000005,
    /// The instruction at 0x%08lx referenced memory at 0x%08lx.
    /// The required data was not placed into memory because of an I/O error status of 0x%08lx.
    IN_PAGE_ERROR = 0xC0000006,
    /// The page file quota for the process has been exhausted.
    PAGEFILE_QUOTA = 0xC0000007,
    /// An invalid HANDLE was specified.
    INVALID_HANDLE = 0xC0000008,
    /// An invalid initial stack was specified in a call to NtCreateThread.
    BAD_INITIAL_STACK = 0xC0000009,
    /// An invalid initial start address was specified in a call to NtCreateThread.
    BAD_INITIAL_PC = 0xC000000A,
    /// An invalid client ID was specified.
    INVALID_CID = 0xC000000B,
    /// An attempt was made to cancel or set a timer that has an associated APC and the specified thread is not the thread that originally set the timer with an associated APC routine.
    TIMER_NOT_CANCELED = 0xC000000C,
    /// An invalid parameter was passed to a service or function.
    INVALID_PARAMETER = 0xC000000D,
    /// A device that does not exist was specified.
    NO_SUCH_DEVICE = 0xC000000E,
    /// {File Not Found} The file %hs does not exist.
    NO_SUCH_FILE = 0xC000000F,
    /// The specified request is not a valid operation for the target device.
    INVALID_DEVICE_REQUEST = 0xC0000010,
    /// The end-of-file marker has been reached.
    /// There is no valid data in the file beyond this marker.
    END_OF_FILE = 0xC0000011,
    /// {Wrong Volume} The wrong volume is in the drive. Insert volume %hs into drive %hs.
    WRONG_VOLUME = 0xC0000012,
    /// {No Disk} There is no disk in the drive. Insert a disk into drive %hs.
    NO_MEDIA_IN_DEVICE = 0xC0000013,
    /// {Unknown Disk Format} The disk in drive %hs is not formatted properly.
    /// Check the disk, and reformat it, if needed.
    UNRECOGNIZED_MEDIA = 0xC0000014,
    /// {Sector Not Found} The specified sector does not exist.
    NONEXISTENT_SECTOR = 0xC0000015,
    /// {Still Busy} The specified I/O request packet (IRP) cannot be disposed of because the I/O operation is not complete.
    MORE_PROCESSING_REQUIRED = 0xC0000016,
    /// {Not Enough Quota} Not enough virtual memory or paging file quota is available to complete the specified operation.
    NO_MEMORY = 0xC0000017,
    /// {Conflicting Address Range} The specified address range conflicts with the address space.
    CONFLICTING_ADDRESSES = 0xC0000018,
    /// The address range to unmap is not a mapped view.
    NOT_MAPPED_VIEW = 0xC0000019,
    /// The virtual memory cannot be freed.
    UNABLE_TO_FREE_VM = 0xC000001A,
    /// The specified section cannot be deleted.
    UNABLE_TO_DELETE_SECTION = 0xC000001B,
    /// An invalid system service was specified in a system service call.
    INVALID_SYSTEM_SERVICE = 0xC000001C,
    /// {EXCEPTION} Illegal Instruction An attempt was made to execute an illegal instruction.
    ILLEGAL_INSTRUCTION = 0xC000001D,
    /// {Invalid Lock Sequence} An attempt was made to execute an invalid lock sequence.
    INVALID_LOCK_SEQUENCE = 0xC000001E,
    /// {Invalid Mapping} An attempt was made to create a view for a section that is bigger than the section.
    INVALID_VIEW_SIZE = 0xC000001F,
    /// {Bad File} The attributes of the specified mapping file for a section of memory cannot be read.
    INVALID_FILE_FOR_SECTION = 0xC0000020,
    /// {Already Committed} The specified address range is already committed.
    ALREADY_COMMITTED = 0xC0000021,
    /// {Access Denied} A process has requested access to an object but has not been granted those access rights.
    ACCESS_DENIED = 0xC0000022,
    /// {Buffer Too Small} The buffer is too small to contain the entry. No information has been written to the buffer.
    BUFFER_TOO_SMALL = 0xC0000023,
    /// {Wrong Type} There is a mismatch between the type of object that is required by the requested operation and the type of object that is specified in the request.
    OBJECT_TYPE_MISMATCH = 0xC0000024,
    /// {EXCEPTION} Cannot Continue Windows cannot continue from this exception.
    NONCONTINUABLE_EXCEPTION = 0xC0000025,
    /// An invalid exception disposition was returned by an exception handler.
    INVALID_DISPOSITION = 0xC0000026,
    /// Unwind exception code.
    UNWIND = 0xC0000027,
    /// An invalid or unaligned stack was encountered during an unwind operation.
    BAD_STACK = 0xC0000028,
    /// An invalid unwind target was encountered during an unwind operation.
    INVALID_UNWIND_TARGET = 0xC0000029,
    /// An attempt was made to unlock a page of memory that was not locked.
    NOT_LOCKED = 0xC000002A,
    /// A device parity error on an I/O operation.
    PARITY_ERROR = 0xC000002B,
    /// An attempt was made to decommit uncommitted virtual memory.
    UNABLE_TO_DECOMMIT_VM = 0xC000002C,
    /// An attempt was made to change the attributes on memory that has not been committed.
    NOT_COMMITTED = 0xC000002D,
    /// Invalid object attributes specified to NtCreatePort or invalid port attributes specified to NtConnectPort.
    INVALID_PORT_ATTRIBUTES = 0xC000002E,
    /// The length of the message that was passed to NtRequestPort or NtRequestWaitReplyPort is longer than the maximum message that is allowed by the port.
    PORT_MESSAGE_TOO_LONG = 0xC000002F,
    /// An invalid combination of parameters was specified.
    INVALID_PARAMETER_MIX = 0xC0000030,
    /// An attempt was made to lower a quota limit below the current usage.
    INVALID_QUOTA_LOWER = 0xC0000031,
    /// {Corrupt Disk} The file system structure on the disk is corrupt and unusable. Run the Chkdsk utility on the volume %hs.
    DISK_CORRUPT_ERROR = 0xC0000032,
    /// The object name is invalid.
    OBJECT_NAME_INVALID = 0xC0000033,
    /// The object name is not found.
    OBJECT_NAME_NOT_FOUND = 0xC0000034,
    /// The object name already exists.
    OBJECT_NAME_COLLISION = 0xC0000035,
    /// An attempt was made to send a message to a disconnected communication port.
    PORT_DISCONNECTED = 0xC0000037,
    /// An attempt was made to attach to a device that was already attached to another device.
    DEVICE_ALREADY_ATTACHED = 0xC0000038,
    /// The object path component was not a directory object.
    OBJECT_PATH_INVALID = 0xC0000039,
    /// {Path Not Found} The path %hs does not exist.
    OBJECT_PATH_NOT_FOUND = 0xC000003A,
    /// The object path component was not a directory object.
    OBJECT_PATH_SYNTAX_BAD = 0xC000003B,
    /// {Data Overrun} A data overrun error occurred.
    DATA_OVERRUN = 0xC000003C,
    /// {Data Late} A data late error occurred.
    DATA_LATE_ERROR = 0xC000003D,
    /// {Data Error} An error occurred in reading or writing data.
    DATA_ERROR = 0xC000003E,
    /// {Bad CRC} A cyclic redundancy check (CRC) checksum error occurred.
    CRC_ERROR = 0xC000003F,
    /// {Section Too Large} The specified section is too big to map the file.
    SECTION_TOO_BIG = 0xC0000040,
    /// The NtConnectPort request is refused.
    PORT_CONNECTION_REFUSED = 0xC0000041,
    /// The type of port handle is invalid for the operation that is requested.
    INVALID_PORT_HANDLE = 0xC0000042,
    /// A file cannot be opened because the share access flags are incompatible.
    SHARING_VIOLATION = 0xC0000043,
    /// Insufficient quota exists to complete the operation.
    QUOTA_EXCEEDED = 0xC0000044,
    /// The specified page protection was not valid.
    INVALID_PAGE_PROTECTION = 0xC0000045,
    /// An attempt to release a mutant object was made by a thread that was not the owner of the mutant object.
    MUTANT_NOT_OWNED = 0xC0000046,
    /// An attempt was made to release a semaphore such that its maximum count would have been exceeded.
    SEMAPHORE_LIMIT_EXCEEDED = 0xC0000047,
    /// An attempt was made to set the DebugPort or ExceptionPort of a process, but a port already exists in the process, or an attempt was made to set the CompletionPort of a file but a port was already set in the file, or an attempt was made to set the associated completion port of an ALPC port but it is already set.
    PORT_ALREADY_SET = 0xC0000048,
    /// An attempt was made to query image information on a section that does not map an image.
    SECTION_NOT_IMAGE = 0xC0000049,
    /// An attempt was made to suspend a thread whose suspend count was at its maximum.
    SUSPEND_COUNT_EXCEEDED = 0xC000004A,
    /// An attempt was made to suspend a thread that has begun termination.
    THREAD_IS_TERMINATING = 0xC000004B,
    /// An attempt was made to set the working set limit to an invalid value (for example, the minimum greater than maximum).
    BAD_WORKING_SET_LIMIT = 0xC000004C,
    /// A section was created to map a file that is not compatible with an already existing section that maps the same file.
    INCOMPATIBLE_FILE_MAP = 0xC000004D,
    /// A view to a section specifies a protection that is incompatible with the protection of the initial view.
    SECTION_PROTECTION = 0xC000004E,
    /// An operation involving EAs failed because the file system does not support EAs.
    EAS_NOT_SUPPORTED = 0xC000004F,
    /// An EA operation failed because the EA set is too large.
    EA_TOO_LARGE = 0xC0000050,
    /// An EA operation failed because the name or EA index is invalid.
    NONEXISTENT_EA_ENTRY = 0xC0000051,
    /// The file for which EAs were requested has no EAs.
    NO_EAS_ON_FILE = 0xC0000052,
    /// The EA is corrupt and cannot be read.
    EA_CORRUPT_ERROR = 0xC0000053,
    /// A requested read/write cannot be granted due to a conflicting file lock.
    FILE_LOCK_CONFLICT = 0xC0000054,
    /// A requested file lock cannot be granted due to other existing locks.
    LOCK_NOT_GRANTED = 0xC0000055,
    /// A non-close operation has been requested of a file object that has a delete pending.
    DELETE_PENDING = 0xC0000056,
    /// An attempt was made to set the control attribute on a file.
    /// This attribute is not supported in the destination file system.
    CTL_FILE_NOT_SUPPORTED = 0xC0000057,
    /// Indicates a revision number that was encountered or specified is not one that is known by the service.
    /// It might be a more recent revision than the service is aware of.
    UNKNOWN_REVISION = 0xC0000058,
    /// Indicates that two revision levels are incompatible.
    REVISION_MISMATCH = 0xC0000059,
    /// Indicates a particular security ID cannot be assigned as the owner of an object.
    INVALID_OWNER = 0xC000005A,
    /// Indicates a particular security ID cannot be assigned as the primary group of an object.
    INVALID_PRIMARY_GROUP = 0xC000005B,
    /// An attempt has been made to operate on an impersonation token by a thread that is not currently impersonating a client.
    NO_IMPERSONATION_TOKEN = 0xC000005C,
    /// A mandatory group cannot be disabled.
    CANT_DISABLE_MANDATORY = 0xC000005D,
    /// No logon servers are currently available to service the logon request.
    NO_LOGON_SERVERS = 0xC000005E,
    /// A specified logon session does not exist. It might already have been terminated.
    NO_SUCH_LOGON_SESSION = 0xC000005F,
    /// A specified privilege does not exist.
    NO_SUCH_PRIVILEGE = 0xC0000060,
    /// A required privilege is not held by the client.
    PRIVILEGE_NOT_HELD = 0xC0000061,
    /// The name provided is not a properly formed account name.
    INVALID_ACCOUNT_NAME = 0xC0000062,
    /// The specified account already exists.
    USER_EXISTS = 0xC0000063,
    /// The specified account does not exist.
    NO_SUCH_USER = 0xC0000064,
    /// The specified group already exists.
    GROUP_EXISTS = 0xC0000065,
    /// The specified group does not exist.
    NO_SUCH_GROUP = 0xC0000066,
    /// The specified user account is already in the specified group account.
    /// Also used to indicate a group cannot be deleted because it contains a member.
    MEMBER_IN_GROUP = 0xC0000067,
    /// The specified user account is not a member of the specified group account.
    MEMBER_NOT_IN_GROUP = 0xC0000068,
    /// Indicates the requested operation would disable or delete the last remaining administration account.
    /// This is not allowed to prevent creating a situation in which the system cannot be administrated.
    LAST_ADMIN = 0xC0000069,
    /// When trying to update a password, this return status indicates that the value provided as the current password is not correct.
    WRONG_PASSWORD = 0xC000006A,
    /// When trying to update a password, this return status indicates that the value provided for the new password contains values that are not allowed in passwords.
    ILL_FORMED_PASSWORD = 0xC000006B,
    /// When trying to update a password, this status indicates that some password update rule has been violated.
    /// For example, the password might not meet length criteria.
    PASSWORD_RESTRICTION = 0xC000006C,
    /// The attempted logon is invalid.
    /// This is either due to a bad username or authentication information.
    LOGON_FAILURE = 0xC000006D,
    /// Indicates a referenced user name and authentication information are valid, but some user account restriction has prevented successful authentication (such as time-of-day restrictions).
    ACCOUNT_RESTRICTION = 0xC000006E,
    /// The user account has time restrictions and cannot be logged onto at this time.
    INVALID_LOGON_HOURS = 0xC000006F,
    /// The user account is restricted so that it cannot be used to log on from the source workstation.
    INVALID_WORKSTATION = 0xC0000070,
    /// The user account password has expired.
    PASSWORD_EXPIRED = 0xC0000071,
    /// The referenced account is currently disabled and cannot be logged on to.
    ACCOUNT_DISABLED = 0xC0000072,
    /// None of the information to be translated has been translated.
    NONE_MAPPED = 0xC0000073,
    /// The number of LUIDs requested cannot be allocated with a single allocation.
    TOO_MANY_LUIDS_REQUESTED = 0xC0000074,
    /// Indicates there are no more LUIDs to allocate.
    LUIDS_EXHAUSTED = 0xC0000075,
    /// Indicates the sub-authority value is invalid for the particular use.
    INVALID_SUB_AUTHORITY = 0xC0000076,
    /// Indicates the ACL structure is not valid.
    INVALID_ACL = 0xC0000077,
    /// Indicates the SID structure is not valid.
    INVALID_SID = 0xC0000078,
    /// Indicates the SECURITY_DESCRIPTOR structure is not valid.
    INVALID_SECURITY_DESCR = 0xC0000079,
    /// Indicates the specified procedure address cannot be found in the DLL.
    PROCEDURE_NOT_FOUND = 0xC000007A,
    /// {Bad Image} %hs is either not designed to run on Windows or it contains an error.
    /// Try installing the program again using the original installation media or contact your system administrator or the software vendor for support.
    INVALID_IMAGE_FORMAT = 0xC000007B,
    /// An attempt was made to reference a token that does not exist.
    /// This is typically done by referencing the token that is associated with a thread when the thread is not impersonating a client.
    NO_TOKEN = 0xC000007C,
    /// Indicates that an attempt to build either an inherited ACL or ACE was not successful. This can be caused by a number of things.
    /// One of the more probable causes is the replacement of a CreatorId with a SID that did not fit into the ACE or ACL.
    BAD_INHERITANCE_ACL = 0xC000007D,
    /// The range specified in NtUnlockFile was not locked.
    RANGE_NOT_LOCKED = 0xC000007E,
    /// An operation failed because the disk was full.
    DISK_FULL = 0xC000007F,
    /// The GUID allocation server is disabled at the moment.
    SERVER_DISABLED = 0xC0000080,
    /// The GUID allocation server is enabled at the moment.
    SERVER_NOT_DISABLED = 0xC0000081,
    /// Too many GUIDs were requested from the allocation server at once.
    TOO_MANY_GUIDS_REQUESTED = 0xC0000082,
    /// The GUIDs could not be allocated because the Authority Agent was exhausted.
    GUIDS_EXHAUSTED = 0xC0000083,
    /// The value provided was an invalid value for an identifier authority.
    INVALID_ID_AUTHORITY = 0xC0000084,
    /// No more authority agent values are available for the particular identifier authority value.
    AGENTS_EXHAUSTED = 0xC0000085,
    /// An invalid volume label has been specified.
    INVALID_VOLUME_LABEL = 0xC0000086,
    /// A mapped section could not be extended.
    SECTION_NOT_EXTENDED = 0xC0000087,
    /// Specified section to flush does not map a data file.
    NOT_MAPPED_DATA = 0xC0000088,
    /// Indicates the specified image file did not contain a resource section.
    RESOURCE_DATA_NOT_FOUND = 0xC0000089,
    /// Indicates the specified resource type cannot be found in the image file.
    RESOURCE_TYPE_NOT_FOUND = 0xC000008A,
    /// Indicates the specified resource name cannot be found in the image file.
    RESOURCE_NAME_NOT_FOUND = 0xC000008B,
    /// {EXCEPTION} Array bounds exceeded.
    ARRAY_BOUNDS_EXCEEDED = 0xC000008C,
    /// {EXCEPTION} Floating-point denormal operand.
    FLOAT_DENORMAL_OPERAND = 0xC000008D,
    /// {EXCEPTION} Floating-point division by zero.
    FLOAT_DIVIDE_BY_ZERO = 0xC000008E,
    /// {EXCEPTION} Floating-point inexact result.
    FLOAT_INEXACT_RESULT = 0xC000008F,
    /// {EXCEPTION} Floating-point invalid operation.
    FLOAT_INVALID_OPERATION = 0xC0000090,
    /// {EXCEPTION} Floating-point overflow.
    FLOAT_OVERFLOW = 0xC0000091,
    /// {EXCEPTION} Floating-point stack check.
    FLOAT_STACK_CHECK = 0xC0000092,
    /// {EXCEPTION} Floating-point underflow.
    FLOAT_UNDERFLOW = 0xC0000093,
    /// {EXCEPTION} Integer division by zero.
    INTEGER_DIVIDE_BY_ZERO = 0xC0000094,
    /// {EXCEPTION} Integer overflow.
    INTEGER_OVERFLOW = 0xC0000095,
    /// {EXCEPTION} Privileged instruction.
    PRIVILEGED_INSTRUCTION = 0xC0000096,
    /// An attempt was made to install more paging files than the system supports.
    TOO_MANY_PAGING_FILES = 0xC0000097,
    /// The volume for a file has been externally altered such that the opened file is no longer valid.
    FILE_INVALID = 0xC0000098,
    /// When a block of memory is allotted for future updates, such as the memory allocated to hold discretionary access control and primary group information, successive updates might exceed the amount of memory originally allotted.
    /// Because a quota might already have been charged to several processes that have handles to the object, it is not reasonable to alter the size of the allocated memory.
    /// Instead, a request that requires more memory than has been allotted must fail and the STATUS_ALLOTTED_SPACE_EXCEEDED error returned.
    ALLOTTED_SPACE_EXCEEDED = 0xC0000099,
    /// Insufficient system resources exist to complete the API.
    INSUFFICIENT_RESOURCES = 0xC000009A,
    /// An attempt has been made to open a DFS exit path control file.
    DFS_EXIT_PATH_FOUND = 0xC000009B,
    /// There are bad blocks (sectors) on the hard disk.
    DEVICE_DATA_ERROR = 0xC000009C,
    /// There is bad cabling, non-termination, or the controller is not able to obtain access to the hard disk.
    DEVICE_NOT_CONNECTED = 0xC000009D,
    /// Virtual memory cannot be freed because the base address is not the base of the region and a region size of zero was specified.
    FREE_VM_NOT_AT_BASE = 0xC000009F,
    /// An attempt was made to free virtual memory that is not allocated.
    MEMORY_NOT_ALLOCATED = 0xC00000A0,
    /// The working set is not big enough to allow the requested pages to be locked.
    WORKING_SET_QUOTA = 0xC00000A1,
    /// {Write Protect Error} The disk cannot be written to because it is write-protected.
    /// Remove the write protection from the volume %hs in drive %hs.
    MEDIA_WRITE_PROTECTED = 0xC00000A2,
    /// {Drive Not Ready} The drive is not ready for use; its door might be open.
    /// Check drive %hs and make sure that a disk is inserted and that the drive door is closed.
    DEVICE_NOT_READY = 0xC00000A3,
    /// The specified attributes are invalid or are incompatible with the attributes for the group as a whole.
    INVALID_GROUP_ATTRIBUTES = 0xC00000A4,
    /// A specified impersonation level is invalid.
    /// Also used to indicate that a required impersonation level was not provided.
    BAD_IMPERSONATION_LEVEL = 0xC00000A5,
    /// An attempt was made to open an anonymous-level token. Anonymous tokens cannot be opened.
    CANT_OPEN_ANONYMOUS = 0xC00000A6,
    /// The validation information class requested was invalid.
    BAD_VALIDATION_CLASS = 0xC00000A7,
    /// The type of a token object is inappropriate for its attempted use.
    BAD_TOKEN_TYPE = 0xC00000A8,
    /// The type of a token object is inappropriate for its attempted use.
    BAD_MASTER_BOOT_RECORD = 0xC00000A9,
    /// An attempt was made to execute an instruction at an unaligned address and the host system does not support unaligned instruction references.
    INSTRUCTION_MISALIGNMENT = 0xC00000AA,
    /// The maximum named pipe instance count has been reached.
    INSTANCE_NOT_AVAILABLE = 0xC00000AB,
    /// An instance of a named pipe cannot be found in the listening state.
    PIPE_NOT_AVAILABLE = 0xC00000AC,
    /// The named pipe is not in the connected or closing state.
    INVALID_PIPE_STATE = 0xC00000AD,
    /// The specified pipe is set to complete operations and there are current I/O operations queued so that it cannot be changed to queue operations.
    PIPE_BUSY = 0xC00000AE,
    /// The specified handle is not open to the server end of the named pipe.
    ILLEGAL_FUNCTION = 0xC00000AF,
    /// The specified named pipe is in the disconnected state.
    PIPE_DISCONNECTED = 0xC00000B0,
    /// The specified named pipe is in the closing state.
    PIPE_CLOSING = 0xC00000B1,
    /// The specified named pipe is in the connected state.
    PIPE_CONNECTED = 0xC00000B2,
    /// The specified named pipe is in the listening state.
    PIPE_LISTENING = 0xC00000B3,
    /// The specified named pipe is not in message mode.
    INVALID_READ_MODE = 0xC00000B4,
    /// {Device Timeout} The specified I/O operation on %hs was not completed before the time-out period expired.
    IO_TIMEOUT = 0xC00000B5,
    /// The specified file has been closed by another process.
    FILE_FORCED_CLOSED = 0xC00000B6,
    /// Profiling is not started.
    PROFILING_NOT_STARTED = 0xC00000B7,
    /// Profiling is not stopped.
    PROFILING_NOT_STOPPED = 0xC00000B8,
    /// The passed ACL did not contain the minimum required information.
    COULD_NOT_INTERPRET = 0xC00000B9,
    /// The file that was specified as a target is a directory, and the caller specified that it could be anything but a directory.
    FILE_IS_A_DIRECTORY = 0xC00000BA,
    /// The request is not supported.
    NOT_SUPPORTED = 0xC00000BB,
    /// This remote computer is not listening.
    REMOTE_NOT_LISTENING = 0xC00000BC,
    /// A duplicate name exists on the network.
    DUPLICATE_NAME = 0xC00000BD,
    /// The network path cannot be located.
    BAD_NETWORK_PATH = 0xC00000BE,
    /// The network is busy.
    NETWORK_BUSY = 0xC00000BF,
    /// This device does not exist.
    DEVICE_DOES_NOT_EXIST = 0xC00000C0,
    /// The network BIOS command limit has been reached.
    TOO_MANY_COMMANDS = 0xC00000C1,
    /// An I/O adapter hardware error has occurred.
    ADAPTER_HARDWARE_ERROR = 0xC00000C2,
    /// The network responded incorrectly.
    INVALID_NETWORK_RESPONSE = 0xC00000C3,
    /// An unexpected network error occurred.
    UNEXPECTED_NETWORK_ERROR = 0xC00000C4,
    /// The remote adapter is not compatible.
    BAD_REMOTE_ADAPTER = 0xC00000C5,
    /// The print queue is full.
    PRINT_QUEUE_FULL = 0xC00000C6,
    /// Space to store the file that is waiting to be printed is not available on the server.
    NO_SPOOL_SPACE = 0xC00000C7,
    /// The requested print file has been canceled.
    PRINT_CANCELLED = 0xC00000C8,
    /// The network name was deleted.
    NETWORK_NAME_DELETED = 0xC00000C9,
    /// Network access is denied.
    NETWORK_ACCESS_DENIED = 0xC00000CA,
    /// {Incorrect Network Resource Type} The specified device type (LPT, for example) conflicts with the actual device type on the remote resource.
    BAD_DEVICE_TYPE = 0xC00000CB,
    /// {Network Name Not Found} The specified share name cannot be found on the remote server.
    BAD_NETWORK_NAME = 0xC00000CC,
    /// The name limit for the network adapter card of the local computer was exceeded.
    TOO_MANY_NAMES = 0xC00000CD,
    /// The network BIOS session limit was exceeded.
    TOO_MANY_SESSIONS = 0xC00000CE,
    /// File sharing has been temporarily paused.
    SHARING_PAUSED = 0xC00000CF,
    /// No more connections can be made to this remote computer at this time because the computer has already accepted the maximum number of connections.
    REQUEST_NOT_ACCEPTED = 0xC00000D0,
    /// Print or disk redirection is temporarily paused.
    REDIRECTOR_PAUSED = 0xC00000D1,
    /// A network data fault occurred.
    NET_WRITE_FAULT = 0xC00000D2,
    /// The number of active profiling objects is at the maximum and no more can be started.
    PROFILING_AT_LIMIT = 0xC00000D3,
    /// {Incorrect Volume} The destination file of a rename request is located on a different device than the source of the rename request.
    NOT_SAME_DEVICE = 0xC00000D4,
    /// The specified file has been renamed and thus cannot be modified.
    FILE_RENAMED = 0xC00000D5,
    /// {Network Request Timeout} The session with a remote server has been disconnected because the time-out interval for a request has expired.
    VIRTUAL_CIRCUIT_CLOSED = 0xC00000D6,
    /// Indicates an attempt was made to operate on the security of an object that does not have security associated with it.
    NO_SECURITY_ON_OBJECT = 0xC00000D7,
    /// Used to indicate that an operation cannot continue without blocking for I/O.
    CANT_WAIT = 0xC00000D8,
    /// Used to indicate that a read operation was done on an empty pipe.
    PIPE_EMPTY = 0xC00000D9,
    /// Configuration information could not be read from the domain controller, either because the machine is unavailable or access has been denied.
    CANT_ACCESS_DOMAIN_INFO = 0xC00000DA,
    /// Indicates that a thread attempted to terminate itself by default (called NtTerminateThread with NULL) and it was the last thread in the current process.
    CANT_TERMINATE_SELF = 0xC00000DB,
    /// Indicates the Sam Server was in the wrong state to perform the desired operation.
    INVALID_SERVER_STATE = 0xC00000DC,
    /// Indicates the domain was in the wrong state to perform the desired operation.
    INVALID_DOMAIN_STATE = 0xC00000DD,
    /// This operation is only allowed for the primary domain controller of the domain.
    INVALID_DOMAIN_ROLE = 0xC00000DE,
    /// The specified domain did not exist.
    NO_SUCH_DOMAIN = 0xC00000DF,
    /// The specified domain already exists.
    DOMAIN_EXISTS = 0xC00000E0,
    /// An attempt was made to exceed the limit on the number of domains per server for this release.
    DOMAIN_LIMIT_EXCEEDED = 0xC00000E1,
    /// An error status returned when the opportunistic lock (oplock) request is denied.
    OPLOCK_NOT_GRANTED = 0xC00000E2,
    /// An error status returned when an invalid opportunistic lock (oplock) acknowledgment is received by a file system.
    INVALID_OPLOCK_PROTOCOL = 0xC00000E3,
    /// This error indicates that the requested operation cannot be completed due to a catastrophic media failure or an on-disk data structure corruption.
    INTERNAL_DB_CORRUPTION = 0xC00000E4,
    /// An internal error occurred.
    INTERNAL_ERROR = 0xC00000E5,
    /// Indicates generic access types were contained in an access mask which should already be mapped to non-generic access types.
    GENERIC_NOT_MAPPED = 0xC00000E6,
    /// Indicates a security descriptor is not in the necessary format (absolute or self-relative).
    BAD_DESCRIPTOR_FORMAT = 0xC00000E7,
    /// An access to a user buffer failed at an expected point in time.
    /// This code is defined because the caller does not want to accept STATUS_ACCESS_VIOLATION in its filter.
    INVALID_USER_BUFFER = 0xC00000E8,
    /// If an I/O error that is not defined in the standard FsRtl filter is returned, it is converted to the following error, which is guaranteed to be in the filter.
    /// In this case, information is lost; however, the filter correctly handles the exception.
    UNEXPECTED_IO_ERROR = 0xC00000E9,
    /// If an MM error that is not defined in the standard FsRtl filter is returned, it is converted to one of the following errors, which are guaranteed to be in the filter.
    /// In this case, information is lost; however, the filter correctly handles the exception.
    UNEXPECTED_MM_CREATE_ERR = 0xC00000EA,
    /// If an MM error that is not defined in the standard FsRtl filter is returned, it is converted to one of the following errors, which are guaranteed to be in the filter.
    /// In this case, information is lost; however, the filter correctly handles the exception.
    UNEXPECTED_MM_MAP_ERROR = 0xC00000EB,
    /// If an MM error that is not defined in the standard FsRtl filter is returned, it is converted to one of the following errors, which are guaranteed to be in the filter.
    /// In this case, information is lost; however, the filter correctly handles the exception.
    UNEXPECTED_MM_EXTEND_ERR = 0xC00000EC,
    /// The requested action is restricted for use by logon processes only.
    /// The calling process has not registered as a logon process.
    NOT_LOGON_PROCESS = 0xC00000ED,
    /// An attempt has been made to start a new session manager or LSA logon session by using an ID that is already in use.
    LOGON_SESSION_EXISTS = 0xC00000EE,
    /// An invalid parameter was passed to a service or function as the first argument.
    INVALID_PARAMETER_1 = 0xC00000EF,
    /// An invalid parameter was passed to a service or function as the second argument.
    INVALID_PARAMETER_2 = 0xC00000F0,
    /// An invalid parameter was passed to a service or function as the third argument.
    INVALID_PARAMETER_3 = 0xC00000F1,
    /// An invalid parameter was passed to a service or function as the fourth argument.
    INVALID_PARAMETER_4 = 0xC00000F2,
    /// An invalid parameter was passed to a service or function as the fifth argument.
    INVALID_PARAMETER_5 = 0xC00000F3,
    /// An invalid parameter was passed to a service or function as the sixth argument.
    INVALID_PARAMETER_6 = 0xC00000F4,
    /// An invalid parameter was passed to a service or function as the seventh argument.
    INVALID_PARAMETER_7 = 0xC00000F5,
    /// An invalid parameter was passed to a service or function as the eighth argument.
    INVALID_PARAMETER_8 = 0xC00000F6,
    /// An invalid parameter was passed to a service or function as the ninth argument.
    INVALID_PARAMETER_9 = 0xC00000F7,
    /// An invalid parameter was passed to a service or function as the tenth argument.
    INVALID_PARAMETER_10 = 0xC00000F8,
    /// An invalid parameter was passed to a service or function as the eleventh argument.
    INVALID_PARAMETER_11 = 0xC00000F9,
    /// An invalid parameter was passed to a service or function as the twelfth argument.
    INVALID_PARAMETER_12 = 0xC00000FA,
    /// An attempt was made to access a network file, but the network software was not yet started.
    REDIRECTOR_NOT_STARTED = 0xC00000FB,
    /// An attempt was made to start the redirector, but the redirector has already been started.
    REDIRECTOR_STARTED = 0xC00000FC,
    /// A new guard page for the stack cannot be created.
    STACK_OVERFLOW = 0xC00000FD,
    /// A specified authentication package is unknown.
    NO_SUCH_PACKAGE = 0xC00000FE,
    /// A malformed function table was encountered during an unwind operation.
    BAD_FUNCTION_TABLE = 0xC00000FF,
    /// Indicates the specified environment variable name was not found in the specified environment block.
    VARIABLE_NOT_FOUND = 0xC0000100,
    /// Indicates that the directory trying to be deleted is not empty.
    DIRECTORY_NOT_EMPTY = 0xC0000101,
    /// {Corrupt File} The file or directory %hs is corrupt and unreadable. Run the Chkdsk utility.
    FILE_CORRUPT_ERROR = 0xC0000102,
    /// A requested opened file is not a directory.
    NOT_A_DIRECTORY = 0xC0000103,
    /// The logon session is not in a state that is consistent with the requested operation.
    BAD_LOGON_SESSION_STATE = 0xC0000104,
    /// An internal LSA error has occurred.
    /// An authentication package has requested the creation of a logon session but the ID of an already existing logon session has been specified.
    LOGON_SESSION_COLLISION = 0xC0000105,
    /// A specified name string is too long for its intended use.
    NAME_TOO_LONG = 0xC0000106,
    /// The user attempted to force close the files on a redirected drive, but there were opened files on the drive, and the user did not specify a sufficient level of force.
    FILES_OPEN = 0xC0000107,
    /// The user attempted to force close the files on a redirected drive, but there were opened directories on the drive, and the user did not specify a sufficient level of force.
    CONNECTION_IN_USE = 0xC0000108,
    /// RtlFindMessage could not locate the requested message ID in the message table resource.
    MESSAGE_NOT_FOUND = 0xC0000109,
    /// An attempt was made to duplicate an object handle into or out of an exiting process.
    PROCESS_IS_TERMINATING = 0xC000010A,
    /// Indicates an invalid value has been provided for the LogonType requested.
    INVALID_LOGON_TYPE = 0xC000010B,
    /// Indicates that an attempt was made to assign protection to a file system file or directory and one of the SIDs in the security descriptor could not be translated into a GUID that could be stored by the file system.
    /// This causes the protection attempt to fail, which might cause a file creation attempt to fail.
    NO_GUID_TRANSLATION = 0xC000010C,
    /// Indicates that an attempt has been made to impersonate via a named pipe that has not yet been read from.
    CANNOT_IMPERSONATE = 0xC000010D,
    /// Indicates that the specified image is already loaded.
    IMAGE_ALREADY_LOADED = 0xC000010E,
    /// Indicates that an attempt was made to change the size of the LDT for a process that has no LDT.
    NO_LDT = 0xC0000117,
    /// Indicates that an attempt was made to grow an LDT by setting its size, or that the size was not an even number of selectors.
    INVALID_LDT_SIZE = 0xC0000118,
    /// Indicates that the starting value for the LDT information was not an integral multiple of the selector size.
    INVALID_LDT_OFFSET = 0xC0000119,
    /// Indicates that the user supplied an invalid descriptor when trying to set up LDT descriptors.
    INVALID_LDT_DESCRIPTOR = 0xC000011A,
    /// The specified image file did not have the correct format. It appears to be NE format.
    INVALID_IMAGE_NE_FORMAT = 0xC000011B,
    /// Indicates that the transaction state of a registry subtree is incompatible with the requested operation.
    /// For example, a request has been made to start a new transaction with one already in progress, or a request has been made to apply a transaction when one is not currently in progress.
    RXACT_INVALID_STATE = 0xC000011C,
    /// Indicates an error has occurred during a registry transaction commit.
    /// The database has been left in an unknown, but probably inconsistent, state.
    /// The state of the registry transaction is left as COMMITTING.
    RXACT_COMMIT_FAILURE = 0xC000011D,
    /// An attempt was made to map a file of size zero with the maximum size specified as zero.
    MAPPED_FILE_SIZE_ZERO = 0xC000011E,
    /// Too many files are opened on a remote server.
    /// This error should only be returned by the Windows redirector on a remote drive.
    TOO_MANY_OPENED_FILES = 0xC000011F,
    /// The I/O request was canceled.
    CANCELLED = 0xC0000120,
    /// An attempt has been made to remove a file or directory that cannot be deleted.
    CANNOT_DELETE = 0xC0000121,
    /// Indicates a name that was specified as a remote computer name is syntactically invalid.
    INVALID_COMPUTER_NAME = 0xC0000122,
    /// An I/O request other than close was performed on a file after it was deleted, which can only happen to a request that did not complete before the last handle was closed via NtClose.
    FILE_DELETED = 0xC0000123,
    /// Indicates an operation that is incompatible with built-in accounts has been attempted on a built-in (special) SAM account. For example, built-in accounts cannot be deleted.
    SPECIAL_ACCOUNT = 0xC0000124,
    /// The operation requested cannot be performed on the specified group because it is a built-in special group.
    SPECIAL_GROUP = 0xC0000125,
    /// The operation requested cannot be performed on the specified user because it is a built-in special user.
    SPECIAL_USER = 0xC0000126,
    /// Indicates a member cannot be removed from a group because the group is currently the member's primary group.
    MEMBERS_PRIMARY_GROUP = 0xC0000127,
    /// An I/O request other than close and several other special case operations was attempted using a file object that had already been closed.
    FILE_CLOSED = 0xC0000128,
    /// Indicates a process has too many threads to perform the requested action.
    /// For example, assignment of a primary token can be performed only when a process has zero or one threads.
    TOO_MANY_THREADS = 0xC0000129,
    /// An attempt was made to operate on a thread within a specific process, but the specified thread is not in the specified process.
    THREAD_NOT_IN_PROCESS = 0xC000012A,
    /// An attempt was made to establish a token for use as a primary token but the token is already in use.
    /// A token can only be the primary token of one process at a time.
    TOKEN_ALREADY_IN_USE = 0xC000012B,
    /// The page file quota was exceeded.
    PAGEFILE_QUOTA_EXCEEDED = 0xC000012C,
    /// {Out of Virtual Memory} Your system is low on virtual memory.
    /// To ensure that Windows runs correctly, increase the size of your virtual memory paging file. For more information, see Help.
    COMMITMENT_LIMIT = 0xC000012D,
    /// The specified image file did not have the correct format: it appears to be LE format.
    INVALID_IMAGE_LE_FORMAT = 0xC000012E,
    /// The specified image file did not have the correct format: it did not have an initial MZ.
    INVALID_IMAGE_NOT_MZ = 0xC000012F,
    /// The specified image file did not have the correct format: it did not have a proper e_lfarlc in the MZ header.
    INVALID_IMAGE_PROTECT = 0xC0000130,
    /// The specified image file did not have the correct format: it appears to be a 16-bit Windows image.
    INVALID_IMAGE_WIN_16 = 0xC0000131,
    /// The Netlogon service cannot start because another Netlogon service running in the domain conflicts with the specified role.
    LOGON_SERVER_CONFLICT = 0xC0000132,
    /// The time at the primary domain controller is different from the time at the backup domain controller or member server by too large an amount.
    TIME_DIFFERENCE_AT_DC = 0xC0000133,
    /// On applicable Windows Server releases, the SAM database is significantly out of synchronization with the copy on the domain controller. A complete synchronization is required.
    SYNCHRONIZATION_REQUIRED = 0xC0000134,
    /// {Unable To Locate Component} This application has failed to start because %hs was not found.
    /// Reinstalling the application might fix this problem.
    DLL_NOT_FOUND = 0xC0000135,
    /// The NtCreateFile API failed. This error should never be returned to an application; it is a place holder for the Windows LAN Manager Redirector to use in its internal error-mapping routines.
    OPEN_FAILED = 0xC0000136,
    /// {Privilege Failed} The I/O permissions for the process could not be changed.
    IO_PRIVILEGE_FAILED = 0xC0000137,
    /// {Ordinal Not Found} The ordinal %ld could not be located in the dynamic link library %hs.
    ORDINAL_NOT_FOUND = 0xC0000138,
    /// {Entry Point Not Found} The procedure entry point %hs could not be located in the dynamic link library %hs.
    ENTRYPOINT_NOT_FOUND = 0xC0000139,
    /// {Application Exit by CTRL+C} The application terminated as a result of a CTRL+C.
    CONTROL_C_EXIT = 0xC000013A,
    /// {Virtual Circuit Closed} The network transport on your computer has closed a network connection.
    /// There might or might not be I/O requests outstanding.
    LOCAL_DISCONNECT = 0xC000013B,
    /// {Virtual Circuit Closed} The network transport on a remote computer has closed a network connection.
    /// There might or might not be I/O requests outstanding.
    REMOTE_DISCONNECT = 0xC000013C,
    /// {Insufficient Resources on Remote Computer} The remote computer has insufficient resources to complete the network request.
    /// For example, the remote computer might not have enough available memory to carry out the request at this time.
    REMOTE_RESOURCES = 0xC000013D,
    /// {Virtual Circuit Closed} An existing connection (virtual circuit) has been broken at the remote computer.
    /// There is probably something wrong with the network software protocol or the network hardware on the remote computer.
    LINK_FAILED = 0xC000013E,
    /// {Virtual Circuit Closed} The network transport on your computer has closed a network connection because it had to wait too long for a response from the remote computer.
    LINK_TIMEOUT = 0xC000013F,
    /// The connection handle that was given to the transport was invalid.
    INVALID_CONNECTION = 0xC0000140,
    /// The address handle that was given to the transport was invalid.
    INVALID_ADDRESS = 0xC0000141,
    /// {DLL Initialization Failed} Initialization of the dynamic link library %hs failed. The process is terminating abnormally.
    DLL_INIT_FAILED = 0xC0000142,
    /// {Missing System File} The required system file %hs is bad or missing.
    MISSING_SYSTEMFILE = 0xC0000143,
    /// 
```
