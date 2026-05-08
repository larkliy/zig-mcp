```
{Application Error} The exception %s (0x%08lx) occurred in the application at location 0x%08lx.
    UNHANDLED_EXCEPTION = 0xC0000144,
    /// {Application Error} The application failed to initialize properly (0x%lx). Click OK to terminate the application.
    APP_INIT_FAILURE = 0xC0000145,
    /// {Unable to Create Paging File} The creation of the paging file %hs failed (%lx). The requested size was %ld.
    PAGEFILE_CREATE_FAILED = 0xC0000146,
    /// {No Paging File Specified} No paging file was specified in the system configuration.
    NO_PAGEFILE = 0xC0000147,
    /// {Incorrect System Call Level} An invalid level was passed into the specified system call.
    INVALID_LEVEL = 0xC0000148,
    /// {Incorrect Password to LAN Manager Server} You specified an incorrect password to a LAN Manager 2.x or MS-NET server.
    WRONG_PASSWORD_CORE = 0xC0000149,
    /// {EXCEPTION} A real-mode application issued a floating-point instruction and floating-point hardware is not present.
    ILLEGAL_FLOAT_CONTEXT = 0xC000014A,
    /// The pipe operation has failed because the other end of the pipe has been closed.
    PIPE_BROKEN = 0xC000014B,
    /// {The Registry Is Corrupt} The structure of one of the files that contains registry data is corrupt; the image of the file in memory is corrupt; or the file could not be recovered because the alternate copy or log was absent or corrupt.
    REGISTRY_CORRUPT = 0xC000014C,
    /// An I/O operation initiated by the Registry failed and cannot be recovered.
    /// The registry could not read in, write out, or flush one of the files that contain the system's image of the registry.
    REGISTRY_IO_FAILED = 0xC000014D,
    /// An event pair synchronization operation was performed using the thread-specific client/server event pair object, but no event pair object was associated with the thread.
    NO_EVENT_PAIR = 0xC000014E,
    /// The volume does not contain a recognized file system.
    /// Be sure that all required file system drivers are loaded and that the volume is not corrupt.
    UNRECOGNIZED_VOLUME = 0xC000014F,
    /// No serial device was successfully initialized. The serial driver will unload.
    SERIAL_NO_DEVICE_INITED = 0xC0000150,
    /// The specified local group does not exist.
    NO_SUCH_ALIAS = 0xC0000151,
    /// The specified account name is not a member of the group.
    MEMBER_NOT_IN_ALIAS = 0xC0000152,
    /// The specified account name is already a member of the group.
    MEMBER_IN_ALIAS = 0xC0000153,
    /// The specified local group already exists.
    ALIAS_EXISTS = 0xC0000154,
    /// A requested type of logon (for example, interactive, network, and service) is not granted by the local security policy of the target system.
    /// Ask the system administrator to grant the necessary form of logon.
    LOGON_NOT_GRANTED = 0xC0000155,
    /// The maximum number of secrets that can be stored in a single system was exceeded.
    /// The length and number of secrets is limited to satisfy U.S. State Department export restrictions.
    TOO_MANY_SECRETS = 0xC0000156,
    /// The length of a secret exceeds the maximum allowable length.
    /// The length and number of secrets is limited to satisfy U.S. State Department export restrictions.
    SECRET_TOO_LONG = 0xC0000157,
    /// The local security authority (LSA) database contains an internal inconsistency.
    INTERNAL_DB_ERROR = 0xC0000158,
    /// The requested operation cannot be performed in full-screen mode.
    FULLSCREEN_MODE = 0xC0000159,
    /// During a logon attempt, the user's security context accumulated too many security IDs. This is a very unusual situation.
    /// Remove the user from some global or local groups to reduce the number of security IDs to incorporate into the security context.
    TOO_MANY_CONTEXT_IDS = 0xC000015A,
    /// A user has requested a type of logon (for example, interactive or network) that has not been granted.
    /// An administrator has control over who can logon interactively and through the network.
    LOGON_TYPE_NOT_GRANTED = 0xC000015B,
    /// The system has attempted to load or restore a file into the registry, and the specified file is not in the format of a registry file.
    NOT_REGISTRY_FILE = 0xC000015C,
    /// An attempt was made to change a user password in the security account manager without providing the necessary Windows cross-encrypted password.
    NT_CROSS_ENCRYPTION_REQUIRED = 0xC000015D,
    /// A domain server has an incorrect configuration.
    DOMAIN_CTRLR_CONFIG_ERROR = 0xC000015E,
    /// An attempt was made to explicitly access the secondary copy of information via a device control to the fault tolerance driver and the secondary copy is not present in the system.
    FT_MISSING_MEMBER = 0xC000015F,
    /// A configuration registry node that represents a driver service entry was ill-formed and did not contain the required value entries.
    ILL_FORMED_SERVICE_ENTRY = 0xC0000160,
    /// An illegal character was encountered.
    /// For a multibyte character set, this includes a lead byte without a succeeding trail byte.
    /// For the Unicode character set this includes the characters 0xFFFF and 0xFFFE.
    ILLEGAL_CHARACTER = 0xC0000161,
    /// No mapping for the Unicode character exists in the target multibyte code page.
    UNMAPPABLE_CHARACTER = 0xC0000162,
    /// The Unicode character is not defined in the Unicode character set that is installed on the system.
    UNDEFINED_CHARACTER = 0xC0000163,
    /// The paging file cannot be created on a floppy disk.
    FLOPPY_VOLUME = 0xC0000164,
    /// {Floppy Disk Error} While accessing a floppy disk, an ID address mark was not found.
    FLOPPY_ID_MARK_NOT_FOUND = 0xC0000165,
    /// {Floppy Disk Error} While accessing a floppy disk, the track address from the sector ID field was found to be different from the track address that is maintained by the controller.
    FLOPPY_WRONG_CYLINDER = 0xC0000166,
    /// {Floppy Disk Error} The floppy disk controller reported an error that is not recognized by the floppy disk driver.
    FLOPPY_UNKNOWN_ERROR = 0xC0000167,
    /// {Floppy Disk Error} While accessing a floppy-disk, the controller returned inconsistent results via its registers.
    FLOPPY_BAD_REGISTERS = 0xC0000168,
    /// {Hard Disk Error} While accessing the hard disk, a recalibrate operation failed, even after retries.
    DISK_RECALIBRATE_FAILED = 0xC0000169,
    /// {Hard Disk Error} While accessing the hard disk, a disk operation failed even after retries.
    DISK_OPERATION_FAILED = 0xC000016A,
    /// {Hard Disk Error} While accessing the hard disk, a disk controller reset was needed, but even that failed.
    DISK_RESET_FAILED = 0xC000016B,
    /// An attempt was made to open a device that was sharing an interrupt request (IRQ) with other devices.
    /// At least one other device that uses that IRQ was already opened.
    /// Two concurrent opens of devices that share an IRQ and only work via interrupts is not supported for the particular bus type that the devices use.
    SHARED_IRQ_BUSY = 0xC000016C,
    /// {FT Orphaning} A disk that is part of a fault-tolerant volume can no longer be accessed.
    FT_ORPHANING = 0xC000016D,
    /// The basic input/output system (BIOS) failed to connect a system interrupt to the device or bus for which the device is connected.
    BIOS_FAILED_TO_CONNECT_INTERRUPT = 0xC000016E,
    /// The tape could not be partitioned.
    PARTITION_FAILURE = 0xC0000172,
    /// When accessing a new tape of a multi-volume partition, the current blocksize is incorrect.
    INVALID_BLOCK_LENGTH = 0xC0000173,
    /// The tape partition information could not be found when loading a tape.
    DEVICE_NOT_PARTITIONED = 0xC0000174,
    /// An attempt to lock the eject media mechanism failed.
    UNABLE_TO_LOCK_MEDIA = 0xC0000175,
    /// An attempt to unload media failed.
    UNABLE_TO_UNLOAD_MEDIA = 0xC0000176,
    /// The physical end of tape was detected.
    EOM_OVERFLOW = 0xC0000177,
    /// {No Media} There is no media in the drive. Insert media into drive %hs.
    NO_MEDIA = 0xC0000178,
    /// A member could not be added to or removed from the local group because the member does not exist.
    NO_SUCH_MEMBER = 0xC000017A,
    /// A new member could not be added to a local group because the member has the wrong account type.
    INVALID_MEMBER = 0xC000017B,
    /// An illegal operation was attempted on a registry key that has been marked for deletion.
    KEY_DELETED = 0xC000017C,
    /// The system could not allocate the required space in a registry log.
    NO_LOG_SPACE = 0xC000017D,
    /// Too many SIDs have been specified.
    TOO_MANY_SIDS = 0xC000017E,
    /// An attempt was made to change a user password in the security account manager without providing the necessary LM cross-encrypted password.
    LM_CROSS_ENCRYPTION_REQUIRED = 0xC000017F,
    /// An attempt was made to create a symbolic link in a registry key that already has subkeys or values.
    KEY_HAS_CHILDREN = 0xC0000180,
    /// An attempt was made to create a stable subkey under a volatile parent key.
    CHILD_MUST_BE_VOLATILE = 0xC0000181,
    /// The I/O device is configured incorrectly or the configuration parameters to the driver are incorrect.
    DEVICE_CONFIGURATION_ERROR = 0xC0000182,
    /// An error was detected between two drivers or within an I/O driver.
    DRIVER_INTERNAL_ERROR = 0xC0000183,
    /// The device is not in a valid state to perform this request.
    INVALID_DEVICE_STATE = 0xC0000184,
    /// The I/O device reported an I/O error.
    IO_DEVICE_ERROR = 0xC0000185,
    /// A protocol error was detected between the driver and the device.
    DEVICE_PROTOCOL_ERROR = 0xC0000186,
    /// This operation is only allowed for the primary domain controller of the domain.
    BACKUP_CONTROLLER = 0xC0000187,
    /// The log file space is insufficient to support this operation.
    LOG_FILE_FULL = 0xC0000188,
    /// A write operation was attempted to a volume after it was dismounted.
    TOO_LATE = 0xC0000189,
    /// The workstation does not have a trust secret for the primary domain in the local LSA database.
    NO_TRUST_LSA_SECRET = 0xC000018A,
    /// On applicable Windows Server releases, the SAM database does not have a computer account for this workstation trust relationship.
    NO_TRUST_SAM_ACCOUNT = 0xC000018B,
    /// The logon request failed because the trust relationship between the primary domain and the trusted domain failed.
    TRUSTED_DOMAIN_FAILURE = 0xC000018C,
    /// The logon request failed because the trust relationship between this workstation and the primary domain failed.
    TRUSTED_RELATIONSHIP_FAILURE = 0xC000018D,
    /// The Eventlog log file is corrupt.
    EVENTLOG_FILE_CORRUPT = 0xC000018E,
    /// No Eventlog log file could be opened. The Eventlog service did not start.
    EVENTLOG_CANT_START = 0xC000018F,
    /// The network logon failed. This might be because the validation authority cannot be reached.
    TRUST_FAILURE = 0xC0000190,
    /// An attempt was made to acquire a mutant such that its maximum count would have been exceeded.
    MUTANT_LIMIT_EXCEEDED = 0xC0000191,
    /// An attempt was made to logon, but the NetLogon service was not started.
    NETLOGON_NOT_STARTED = 0xC0000192,
    /// The user account has expired.
    ACCOUNT_EXPIRED = 0xC0000193,
    /// {EXCEPTION} Possible deadlock condition.
    POSSIBLE_DEADLOCK = 0xC0000194,
    /// Multiple connections to a server or shared resource by the same user, using more than one user name, are not allowed.
    /// Disconnect all previous connections to the server or shared resource and try again.
    NETWORK_CREDENTIAL_CONFLICT = 0xC0000195,
    /// An attempt was made to establish a session to a network server, but there are already too many sessions established to that server.
    REMOTE_SESSION_LIMIT = 0xC0000196,
    /// The log file has changed between reads.
    EVENTLOG_FILE_CHANGED = 0xC0000197,
    /// The account used is an interdomain trust account.
    /// Use your global user account or local user account to access this server.
    NOLOGON_INTERDOMAIN_TRUST_ACCOUNT = 0xC0000198,
    /// The account used is a computer account.
    /// Use your global user account or local user account to access this server.
    NOLOGON_WORKSTATION_TRUST_ACCOUNT = 0xC0000199,
    /// The account used is a server trust account.
    /// Use your global user account or local user account to access this server.
    NOLOGON_SERVER_TRUST_ACCOUNT = 0xC000019A,
    /// The name or SID of the specified domain is inconsistent with the trust information for that domain.
    DOMAIN_TRUST_INCONSISTENT = 0xC000019B,
    /// A volume has been accessed for which a file system driver is required that has not yet been loaded.
    FS_DRIVER_REQUIRED = 0xC000019C,
    /// Indicates that the specified image is already loaded as a DLL.
    IMAGE_ALREADY_LOADED_AS_DLL = 0xC000019D,
    /// Short name settings cannot be changed on this volume due to the global registry setting.
    INCOMPATIBLE_WITH_GLOBAL_SHORT_NAME_REGISTRY_SETTING = 0xC000019E,
    /// Short names are not enabled on this volume.
    SHORT_NAMES_NOT_ENABLED_ON_VOLUME = 0xC000019F,
    /// The security stream for the given volume is in an inconsistent state. Please run CHKDSK on the volume.
    SECURITY_STREAM_IS_INCONSISTENT = 0xC00001A0,
    /// A requested file lock operation cannot be processed due to an invalid byte range.
    INVALID_LOCK_RANGE = 0xC00001A1,
    /// The specified access control entry (ACE) contains an invalid condition.
    INVALID_ACE_CONDITION = 0xC00001A2,
    /// The subsystem needed to support the image type is not present.
    IMAGE_SUBSYSTEM_NOT_PRESENT = 0xC00001A3,
    /// The specified file already has a notification GUID associated with it.
    NOTIFICATION_GUID_ALREADY_DEFINED = 0xC00001A4,
    /// A remote open failed because the network open restrictions were not satisfied.
    NETWORK_OPEN_RESTRICTION = 0xC0000201,
    /// There is no user session key for the specified logon session.
    NO_USER_SESSION_KEY = 0xC0000202,
    /// The remote user session has been deleted.
    USER_SESSION_DELETED = 0xC0000203,
    /// Indicates the specified resource language ID cannot be found in the image file.
    RESOURCE_LANG_NOT_FOUND = 0xC0000204,
    /// Insufficient server resources exist to complete the request.
    INSUFF_SERVER_RESOURCES = 0xC0000205,
    /// The size of the buffer is invalid for the specified operation.
    INVALID_BUFFER_SIZE = 0xC0000206,
    /// The transport rejected the specified network address as invalid.
    INVALID_ADDRESS_COMPONENT = 0xC0000207,
    /// The transport rejected the specified network address due to invalid use of a wildcard.
    INVALID_ADDRESS_WILDCARD = 0xC0000208,
    /// The transport address could not be opened because all the available addresses are in use.
    TOO_MANY_ADDRESSES = 0xC0000209,
    /// The transport address could not be opened because it already exists.
    ADDRESS_ALREADY_EXISTS = 0xC000020A,
    /// The transport address is now closed.
    ADDRESS_CLOSED = 0xC000020B,
    /// The transport connection is now disconnected.
    CONNECTION_DISCONNECTED = 0xC000020C,
    /// The transport connection has been reset.
    CONNECTION_RESET = 0xC000020D,
    /// The transport cannot dynamically acquire any more nodes.
    TOO_MANY_NODES = 0xC000020E,
    /// The transport aborted a pending transaction.
    TRANSACTION_ABORTED = 0xC000020F,
    /// The transport timed out a request that is waiting for a response.
    TRANSACTION_TIMED_OUT = 0xC0000210,
    /// The transport did not receive a release for a pending response.
    TRANSACTION_NO_RELEASE = 0xC0000211,
    /// The transport did not find a transaction that matches the specific token.
    TRANSACTION_NO_MATCH = 0xC0000212,
    /// The transport had previously responded to a transaction request.
    TRANSACTION_RESPONDED = 0xC0000213,
    /// The transport does not recognize the specified transaction request ID.
    TRANSACTION_INVALID_ID = 0xC0000214,
    /// The transport does not recognize the specified transaction request type.
    TRANSACTION_INVALID_TYPE = 0xC0000215,
    /// The transport can only process the specified request on the server side of a session.
    NOT_SERVER_SESSION = 0xC0000216,
    /// The transport can only process the specified request on the client side of a session.
    NOT_CLIENT_SESSION = 0xC0000217,
    /// {Registry File Failure} The registry cannot load the hive (file): %hs or its log or alternate. It is corrupt, absent, or not writable.
    CANNOT_LOAD_REGISTRY_FILE = 0xC0000218,
    /// {Unexpected Failure in DebugActiveProcess} An unexpected failure occurred while processing a DebugActiveProcess API request.
    /// Choosing OK will terminate the process, and choosing Cancel will ignore the error.
    DEBUG_ATTACH_FAILED = 0xC0000219,
    /// {Fatal System Error} The %hs system process terminated unexpectedly with a status of 0x%08x (0x%08x 0x%08x). The system has been shut down.
    SYSTEM_PROCESS_TERMINATED = 0xC000021A,
    /// {Data Not Accepted} The TDI client could not handle the data received during an indication.
    DATA_NOT_ACCEPTED = 0xC000021B,
    /// {Unable to Retrieve Browser Server List} The list of servers for this workgroup is not currently available.
    NO_BROWSER_SERVERS_FOUND = 0xC000021C,
    /// NTVDM encountered a hard error.
    VDM_HARD_ERROR = 0xC000021D,
    /// {Cancel Timeout} The driver %hs failed to complete a canceled I/O request in the allotted time.
    DRIVER_CANCEL_TIMEOUT = 0xC000021E,
    /// {Reply Message Mismatch} An attempt was made to reply to an LPC message, but the thread specified by the client ID in the message was not waiting on that message.
    REPLY_MESSAGE_MISMATCH = 0xC000021F,
    /// {Mapped View Alignment Incorrect} An attempt was made to map a view of a file, but either the specified base address or the offset into the file were not aligned on the proper allocation granularity.
    MAPPED_ALIGNMENT = 0xC0000220,
    /// {Bad Image Checksum} The image %hs is possibly corrupt.
    /// The header checksum does not match the computed checksum.
    IMAGE_CHECKSUM_MISMATCH = 0xC0000221,
    /// {Delayed Write Failed} Windows was unable to save all the data for the file %hs. The data has been lost.
    /// This error might be caused by a failure of your computer hardware or network connection. Try to save this file elsewhere.
    LOST_WRITEBEHIND_DATA = 0xC0000222,
    /// The parameters passed to the server in the client/server shared memory window were invalid.
    /// Too much data might have been put in the shared memory window.
    CLIENT_SERVER_PARAMETERS_INVALID = 0xC0000223,
    /// The user password must be changed before logging on the first time.
    PASSWORD_MUST_CHANGE = 0xC0000224,
    /// The object was not found.
    NOT_FOUND = 0xC0000225,
    /// The stream is not a tiny stream.
    NOT_TINY_STREAM = 0xC0000226,
    /// A transaction recovery failed.
    RECOVERY_FAILURE = 0xC0000227,
    /// The request must be handled by the stack overflow code.
    STACK_OVERFLOW_READ = 0xC0000228,
    /// A consistency check failed.
    FAIL_CHECK = 0xC0000229,
    /// The attempt to insert the ID in the index failed because the ID is already in the index.
    DUPLICATE_OBJECTID = 0xC000022A,
    /// The attempt to set the object ID failed because the object already has an ID.
    OBJECTID_EXISTS = 0xC000022B,
    /// Internal OFS status codes indicating how an allocation operation is handled.
    /// Either it is retried after the containing oNode is moved or the extent stream is converted to a large stream.
    CONVERT_TO_LARGE = 0xC000022C,
    /// The request needs to be retried.
    RETRY = 0xC000022D,
    /// The attempt to find the object found an object on the volume that matches by ID; however, it is out of the scope of the handle that is used for the operation.
    FOUND_OUT_OF_SCOPE = 0xC000022E,
    /// The bucket array must be grown. Retry the transaction after doing so.
    ALLOCATE_BUCKET = 0xC000022F,
    /// The specified property set does not exist on the object.
    PROPSET_NOT_FOUND = 0xC0000230,
    /// The user/kernel marshaling buffer has overflowed.
    MARSHALL_OVERFLOW = 0xC0000231,
    /// The supplied variant structure contains invalid data.
    INVALID_VARIANT = 0xC0000232,
    /// A domain controller for this domain was not found.
    DOMAIN_CONTROLLER_NOT_FOUND = 0xC0000233,
    /// The user account has been automatically locked because too many invalid logon attempts or password change attempts have been requested.
    ACCOUNT_LOCKED_OUT = 0xC0000234,
    /// NtClose was called on a handle that was protected from close via NtSetInformationObject.
    HANDLE_NOT_CLOSABLE = 0xC0000235,
    /// The transport-connection attempt was refused by the remote system.
    CONNECTION_REFUSED = 0xC0000236,
    /// The transport connection was gracefully closed.
    GRACEFUL_DISCONNECT = 0xC0000237,
    /// The transport endpoint already has an address associated with it.
    ADDRESS_ALREADY_ASSOCIATED = 0xC0000238,
    /// An address has not yet been associated with the transport endpoint.
    ADDRESS_NOT_ASSOCIATED = 0xC0000239,
    /// An operation was attempted on a nonexistent transport connection.
    CONNECTION_INVALID = 0xC000023A,
    /// An invalid operation was attempted on an active transport connection.
    CONNECTION_ACTIVE = 0xC000023B,
    /// The remote network is not reachable by the transport.
    NETWORK_UNREACHABLE = 0xC000023C,
    /// The remote system is not reachable by the transport.
    HOST_UNREACHABLE = 0xC000023D,
    /// The remote system does not support the transport protocol.
    PROTOCOL_UNREACHABLE = 0xC000023E,
    /// No service is operating at the destination port of the transport on the remote system.
    PORT_UNREACHABLE = 0xC000023F,
    /// The request was aborted.
    REQUEST_ABORTED = 0xC0000240,
    /// The transport connection was aborted by the local system.
    CONNECTION_ABORTED = 0xC0000241,
    /// The specified buffer contains ill-formed data.
    BAD_COMPRESSION_BUFFER = 0xC0000242,
    /// The requested operation cannot be performed on a file with a user mapped section open.
    USER_MAPPED_FILE = 0xC0000243,
    /// {Audit Failed} An attempt to generate a security audit failed.
    AUDIT_FAILED = 0xC0000244,
    /// The timer resolution was not previously set by the current process.
    TIMER_RESOLUTION_NOT_SET = 0xC0000245,
    /// A connection to the server could not be made because the limit on the number of concurrent connections for this account has been reached.
    CONNECTION_COUNT_LIMIT = 0xC0000246,
    /// Attempting to log on during an unauthorized time of day for this account.
    LOGIN_TIME_RESTRICTION = 0xC0000247,
    /// The account is not authorized to log on from this station.
    LOGIN_WKSTA_RESTRICTION = 0xC0000248,
    /// {UP/MP Image Mismatch} The image %hs has been modified for use on a uniprocessor system, but you are running it on a multiprocessor machine. Reinstall the image file.
    IMAGE_MP_UP_MISMATCH = 0xC0000249,
    /// There is insufficient account information to log you on.
    INSUFFICIENT_LOGON_INFO = 0xC0000250,
    /// {Invalid DLL Entrypoint} The dynamic link library %hs is not written correctly.
    /// The stack pointer has been left in an inconsistent state.
    /// The entry point should be declared as WINAPI or STDCALL.
    /// Select YES to fail the DLL load. Select NO to continue execution.
    /// Selecting NO might cause the application to operate incorrectly.
    BAD_DLL_ENTRYPOINT = 0xC0000251,
    /// {Invalid Service Callback Entrypoint} The %hs service is not written correctly.
    /// The stack pointer has been left in an inconsistent state.
    /// The callback entry point should be declared as WINAPI or STDCALL.
    /// Selecting OK will cause the service to continue operation.
    /// However, the service process might operate incorrectly.
    BAD_SERVICE_ENTRYPOINT = 0xC0000252,
    /// The server received the messages but did not send a reply.
    LPC_REPLY_LOST = 0xC0000253,
    /// There is an IP address conflict with another system on the network.
    IP_ADDRESS_CONFLICT1 = 0xC0000254,
    /// There is an IP address conflict with another system on the network.
    IP_ADDRESS_CONFLICT2 = 0xC0000255,
    /// {Low On Registry Space} The system has reached the maximum size that is allowed for the system part of the registry. Additional storage requests will be ignored.
    REGISTRY_QUOTA_LIMIT = 0xC0000256,
    /// The contacted server does not support the indicated part of the DFS namespace.
    PATH_NOT_COVERED = 0xC0000257,
    /// A callback return system service cannot be executed when no callback is active.
    NO_CALLBACK_ACTIVE = 0xC0000258,
    /// The service being accessed is licensed for a particular number of connections.
    /// No more connections can be made to the service at this time because the service has already accepted the maximum number of connections.
    LICENSE_QUOTA_EXCEEDED = 0xC0000259,
    /// The password provided is too short to meet the policy of your user account. Choose a longer password.
    PWD_TOO_SHORT = 0xC000025A,
    /// The policy of your user account does not allow you to change passwords too frequently.
    /// This is done to prevent users from changing back to a familiar, but potentially discovered, password.
    /// If you feel your password has been compromised, contact your administrator immediately to have a new one assigned.
    PWD_TOO_RECENT = 0xC000025B,
    /// You have attempted to change your password to one that you have used in the past.
    /// The policy of your user account does not allow this.
    /// Select a password that you have not previously used.
    PWD_HISTORY_CONFLICT = 0xC000025C,
    /// You have attempted to load a legacy device driver while its device instance had been disabled.
    PLUGPLAY_NO_DEVICE = 0xC000025E,
    /// The specified compression format is unsupported.
    UNSUPPORTED_COMPRESSION = 0xC000025F,
    /// The specified hardware profile configuration is invalid.
    INVALID_HW_PROFILE = 0xC0000260,
    /// The specified Plug and Play registry device path is invalid.
    INVALID_PLUGPLAY_DEVICE_PATH = 0xC0000261,
    /// {Driver Entry Point Not Found} The %hs device driver could not locate the ordinal %ld in driver %hs.
    DRIVER_ORDINAL_NOT_FOUND = 0xC0000262,
    /// {Driver Entry Point Not Found} The %hs device driver could not locate the entry point %hs in driver %hs.
    DRIVER_ENTRYPOINT_NOT_FOUND = 0xC0000263,
    /// {Application Error} The application attempted to release a resource it did not own. Click OK to terminate the application.
    RESOURCE_NOT_OWNED = 0xC0000264,
    /// An attempt was made to create more links on a file than the file system supports.
    TOO_MANY_LINKS = 0xC0000265,
    /// The specified quota list is internally inconsistent with its descriptor.
    QUOTA_LIST_INCONSISTENT = 0xC0000266,
    /// The specified file has been relocated to offline storage.
    FILE_IS_OFFLINE = 0xC0000267,
    /// {Windows Evaluation Notification} The evaluation period for this installation of Windows has expired. This system will shutdown in 1 hour.
    /// To restore access to this installation of Windows, upgrade this installation by using a licensed distribution of this product.
    EVALUATION_EXPIRATION = 0xC0000268,
    /// {Illegal System DLL Relocation} The system DLL %hs was relocated in memory. The application will not run properly.
    /// The relocation occurred because the DLL %hs occupied an address range that is reserved for Windows system DLLs.
    /// The vendor supplying the DLL should be contacted for a new DLL.
    ILLEGAL_DLL_RELOCATION = 0xC0000269,
    /// {License Violation} The system has detected tampering with your registered product type.
    /// This is a violation of your software license. Tampering with the product type is not permitted.
    LICENSE_VIOLATION = 0xC000026A,
    /// {DLL Initialization Failed} The application failed to initialize because the window station is shutting down.
    DLL_INIT_FAILED_LOGOFF = 0xC000026B,
    /// {Unable to Load Device Driver} %hs device driver could not be loaded. Error Status was 0x%x.
    DRIVER_UNABLE_TO_LOAD = 0xC000026C,
    /// DFS is unavailable on the contacted server.
    DFS_UNAVAILABLE = 0xC000026D,
    /// An operation was attempted to a volume after it was dismounted.
    VOLUME_DISMOUNTED = 0xC000026E,
    /// An internal error occurred in the Win32 x86 emulation subsystem.
    WX86_INTERNAL_ERROR = 0xC000026F,
    /// Win32 x86 emulation subsystem floating-point stack check.
    WX86_FLOAT_STACK_CHECK = 0xC0000270,
    /// The validation process needs to continue on to the next step.
    VALIDATE_CONTINUE = 0xC0000271,
    /// There was no match for the specified key in the index.
    NO_MATCH = 0xC0000272,
    /// There are no more matches for the current index enumeration.
    NO_MORE_MATCHES = 0xC0000273,
    /// The NTFS file or directory is not a reparse point.
    NOT_A_REPARSE_POINT = 0xC0000275,
    /// The Windows I/O reparse tag passed for the NTFS reparse point is invalid.
    IO_REPARSE_TAG_INVALID = 0xC0000276,
    /// The Windows I/O reparse tag does not match the one that is in the NTFS reparse point.
    IO_REPARSE_TAG_MISMATCH = 0xC0000277,
    /// The user data passed for the NTFS reparse point is invalid.
    IO_REPARSE_DATA_INVALID = 0xC0000278,
    /// The layered file system driver for this I/O tag did not handle it when needed.
    IO_REPARSE_TAG_NOT_HANDLED = 0xC0000279,
    /// The NTFS symbolic link could not be resolved even though the initial file name is valid.
    REPARSE_POINT_NOT_RESOLVED = 0xC0000280,
    /// The NTFS directory is a reparse point.
    DIRECTORY_IS_A_REPARSE_POINT = 0xC0000281,
    /// The range could not be added to the range list because of a conflict.
    RANGE_LIST_CONFLICT = 0xC0000282,
    /// The specified medium changer source element contains no media.
    SOURCE_ELEMENT_EMPTY = 0xC0000283,
    /// The specified medium changer destination element already contains media.
    DESTINATION_ELEMENT_FULL = 0xC0000284,
    /// The specified medium changer element does not exist.
    ILLEGAL_ELEMENT_ADDRESS = 0xC0000285,
    /// The specified element is contained in a magazine that is no longer present.
    MAGAZINE_NOT_PRESENT = 0xC0000286,
    /// The device requires re-initialization due to hardware errors.
    REINITIALIZATION_NEEDED = 0xC0000287,
    /// The file encryption attempt failed.
    ENCRYPTION_FAILED = 0xC000028A,
    /// The file decryption attempt failed.
    DECRYPTION_FAILED = 0xC000028B,
    /// The specified range could not be found in the range list.
    RANGE_NOT_FOUND = 0xC000028C,
    /// There is no encryption recovery policy configured for this system.
    NO_RECOVERY_POLICY = 0xC000028D,
    /// The required encryption driver is not loaded for this system.
    NO_EFS = 0xC000028E,
    /// The file was encrypted with a different encryption driver than is currently loaded.
    WRONG_EFS = 0xC000028F,
    /// There are no EFS keys defined for the user.
    NO_USER_KEYS = 0xC0000290,
    /// The specified file is not encrypted.
    FILE_NOT_ENCRYPTED = 0xC0000291,
    /// The specified file is not in the defined EFS export format.
    NOT_EXPORT_FORMAT = 0xC0000292,
    /// The specified file is encrypted and the user does not have the ability to decrypt it.
    FILE_ENCRYPTED = 0xC0000293,
    /// The GUID passed was not recognized as valid by a WMI data provider.
    WMI_GUID_NOT_FOUND = 0xC0000295,
    /// The instance name passed was not recognized as valid by a WMI data provider.
    WMI_INSTANCE_NOT_FOUND = 0xC0000296,
    /// The data item ID passed was not recognized as valid by a WMI data provider.
    WMI_ITEMID_NOT_FOUND = 0xC0000297,
    /// The WMI request could not be completed and should be retried.
    WMI_TRY_AGAIN = 0xC0000298,
    /// The policy object is shared and can only be modified at the root.
    SHARED_POLICY = 0xC0000299,
    /// The policy object does not exist when it should.
    POLICY_OBJECT_NOT_FOUND = 0xC000029A,
    /// The requested policy information only lives in the Ds.
    POLICY_ONLY_IN_DS = 0xC000029B,
    /// The volume must be upgraded to enable this feature.
    VOLUME_NOT_UPGRADED = 0xC000029C,
    /// The remote storage service is not operational at this time.
    REMOTE_STORAGE_NOT_ACTIVE = 0xC000029D,
    /// The remote storage service encountered a media error.
    REMOTE_STORAGE_MEDIA_ERROR = 0xC000029E,
    /// The tracking (workstation) service is not running.
    NO_TRACKING_SERVICE = 0xC000029F,
    /// The server process is running under a SID that is different from the SID that is required by client.
    SERVER_SID_MISMATCH = 0xC00002A0,
    /// The specified directory service attribute or value does not exist.
    DS_NO_ATTRIBUTE_OR_VALUE = 0xC00002A1,
    /// The attribute syntax specified to the directory service is invalid.
    DS_INVALID_ATTRIBUTE_SYNTAX = 0xC00002A2,
    /// The attribute type specified to the directory service is not defined.
    DS_ATTRIBUTE_TYPE_UNDEFINED = 0xC00002A3,
    /// The specified directory service attribute or value already exists.
    DS_ATTRIBUTE_OR_VALUE_EXISTS = 0xC00002A4,
    /// The directory service is busy.
    DS_BUSY = 0xC00002A5,
    /// The directory service is unavailable.
    DS_UNAVAILABLE = 0xC00002A6,
    /// The directory service was unable to allocate a relative identifier.
    DS_NO_RIDS_ALLOCATED = 0xC00002A7,
    /// The directory service has exhausted the pool of relative identifiers.
    DS_NO_MORE_RIDS = 0xC00002A8,
    /// The requested operation could not be performed because the directory service is not the master for that type of operation.
    DS_INCORRECT_ROLE_OWNER = 0xC00002A9,
    /// The directory service was unable to initialize the subsystem that allocates relative identifiers.
    DS_RIDMGR_INIT_ERROR = 0xC00002AA,
    /// The requested operation did not satisfy one or more constraints that are associated with the class of the object.
    DS_OBJ_CLASS_VIOLATION = 0xC00002AB,
    /// The directory service can perform the requested operation only on a leaf object.
    DS_CANT_ON_NON_LEAF = 0xC00002AC,
    /// The directory service cannot perform the requested operation on the Relatively Defined Name (RDN) attribute of an object.
    DS_CANT_ON_RDN = 0xC00002AD,
    /// The directory service detected an attempt to modify the object class of an object.
    DS_CANT_MOD_OBJ_CLASS = 0xC00002AE,
    /// An error occurred while performing a cross domain move operation.
    DS_CROSS_DOM_MOVE_FAILED = 0xC00002AF,
    /// Unable to contact the global catalog server.
    DS_GC_NOT_AVAILABLE = 0xC00002B0,
    /// The requested operation requires a directory service, and none was available.
    DIRECTORY_SERVICE_REQUIRED = 0xC00002B1,
    /// The reparse attribute cannot be set because it is incompatible with an existing attribute.
    REPARSE_ATTRIBUTE_CONFLICT = 0xC00002B2,
    /// A group marked "use for deny only" cannot be enabled.
    CANT_ENABLE_DENY_ONLY = 0xC00002B3,
    /// {EXCEPTION} Multiple floating-point faults.
    FLOAT_MULTIPLE_FAULTS = 0xC00002B4,
    /// {EXCEPTION} Multiple floating-point traps.
    FLOAT_MULTIPLE_TRAPS = 0xC00002B5,
    /// The device has been removed.
    DEVICE_REMOVED = 0xC00002B6,
    /// The volume change journal is being deleted.
    JOURNAL_DELETE_IN_PROGRESS = 0xC00002B7,
    /// The volume change journal is not active.
    JOURNAL_NOT_ACTIVE = 0xC00002B8,
    /// The requested interface is not supported.
    NOINTERFACE = 0xC00002B9,
    /// A directory service resource limit has been exceeded.
    DS_ADMIN_LIMIT_EXCEEDED = 0xC00002C1,
    /// {System Standby Failed} The driver %hs does not support standby mode.
    /// Updating this driver allows the system to go to standby mode.
    DRIVER_FAILED_SLEEP = 0xC00002C2,
    /// Mutual Authentication failed. The server password is out of date at the domain controller.
    MUTUAL_AUTHENTICATION_FAILED = 0xC00002C3,
    /// The system file %1 has become corrupt and has been replaced.
    CORRUPT_SYSTEM_FILE = 0xC00002C4,
    /// {EXCEPTION} Alignment Error A data type misalignment error was detected in a load or store instruction.
    DATATYPE_MISALIGNMENT_ERROR = 0xC00002C5,
    /// The WMI data item or data block is read-only.
    WMI_READ_ONLY = 0xC00002C6,
    /// The WMI data item or data block could not be changed.
    WMI_SET_FAILURE = 0xC00002C7,
    /// {Virtual Memory Minimum Too Low} Your system is low on virtual memory.
    /// Windows is increasing the size of your virtual memory paging file.
    /// During this process, memory requests for some applications might be denied. For more information, see Help.
    COMMITMENT_MINIMUM = 0xC00002C8,
    /// {EXCEPTION} Register NaT consumption faults.
    /// A NaT value is consumed on a non-speculative instruction.
    REG_NAT_CONSUMPTION = 0xC00002C9,
    /// The transport element of the medium changer contains media, which is causing the operation to fail.
    TRANSPORT_FULL = 0xC00002CA,
    /// Security Accounts Manager initialization failed because of the following error: %hs Error Status: 0x%x.
    /// Click OK to shut down this system and restart in Directory Services Restore Mode.
    /// Check the event log for more detailed information.
    DS_SAM_INIT_FAILURE = 0xC00002CB,
    /// This operation is supported only when you are connected to the server.
    ONLY_IF_CONNECTED = 0xC00002CC,
    /// Only an administrator can modify the membership list of an administrative group.
    DS_SENSITIVE_GROUP_VIOLATION = 0xC00002CD,
    /// A device was removed so enumeration must be restarted.
    PNP_RESTART_ENUMERATION = 0xC00002CE,
    /// The journal entry has been deleted from the journal.
    JOURNAL_ENTRY_DELETED = 0xC00002CF,
    /// Cannot change the primary group ID of a domain controller account.
    DS_CANT_MOD_PRIMARYGROUPID = 0xC00002D0,
    /// {Fatal System Error} The system image %s is not properly signed.
    /// The file has been replaced with the signed file. The system has been shut down.
    SYSTEM_IMAGE_BAD_SIGNATURE = 0xC00002D1,
    /// The device will not start without a reboot.
    PNP_REBOOT_REQUIRED = 0xC00002D2,
    /// The power state of the current device cannot support this request.
    POWER_STATE_INVALID = 0xC00002D3,
    /// The specified group type is invalid.
    DS_INVALID_GROUP_TYPE = 0xC00002D4,
    /// In a mixed domain, no nesting of a global group if the group is security enabled.
    DS_NO_NEST_GLOBALGROUP_IN_MIXEDDOMAIN = 0xC00002D5,
    /// In a mixed domain, cannot nest local groups with other local groups, if the group is security enabled.
    DS_NO_NEST_LOCALGROUP_IN_MIXEDDOMAIN = 0xC00002D6,
    /// A global group cannot have a local group as a member.
    DS_GLOBAL_CANT_HAVE_LOCAL_MEMBER = 0xC00002D7,
    /// A global group cannot have a universal group as a member.
    DS_GLOBAL_CANT_HAVE_UNIVERSAL_MEMBER = 0xC00002D8,
    /// A universal group cannot have a local group as a member.
    DS_UNIVERSAL_CANT_HAVE_LOCAL_MEMBER = 0xC00002D9,
    /// A global group cannot have a cross-domain member.
    DS_GLOBAL_CANT_HAVE_CROSSDOMAIN_MEMBER = 0xC00002DA,
    /// A local group cannot have another cross-domain local group as a member.
    DS_LOCAL_CANT_HAVE_CROSSDOMAIN_LOCAL_MEMBER = 0xC00002DB,
    /// Cannot change to a security-disabled group because primary members are in this group.
    DS_HAVE_PRIMARY_MEMBERS = 0xC00002DC,
    /// The WMI operation is not supported by the data block or method.
    WMI_NOT_SUPPORTED = 0xC00002DD,
    /// There is not enough power to complete the requested operation.
    INSUFFICIENT_POWER = 0xC00002DE,
    /// The Security Accounts Manager needs to get the boot password.
    SAM_NEED_BOOTKEY_PASSWORD = 0xC00002DF,
    /// The Security Accounts Manager needs to get the boot key from the floppy disk.
    SAM_NEED_BOOTKEY_FLOPPY = 0xC00002E0,
    /// The directory service cannot start.
    DS_CANT_START = 0xC00002E1,
    /// The directory service could not start because of the following error: %hs Error Status: 0x%x.
    /// Click OK to shut down this system and restart in Directory Services Restore Mode.
    /// Check the event log for more detailed information.
    DS_INIT_FAILURE = 0xC00002E2,
    /// The Security Accounts Manager initialization failed because of the following error: %hs Error Status: 0x%x.
    /// Click OK to shut down this system and restart in Safe Mode.
    /// Check the event log for more detailed information.
    SAM_INIT_FAILURE = 0xC00002E3,
    /// The requested operation can be performed only on a global catalog server.
    DS_GC_REQUIRED = 0xC00002E4,
    /// A local group can only be a member of other local groups in the same domain.
    DS_LOCAL_MEMBER_OF_LOCAL_ONLY = 0xC00002E5,
    /// Foreign security principals cannot be members of universal groups.
    DS_NO_FPO_IN_UNIVERSAL_GROUPS = 0xC00002E6,
    /// Your computer could not be joined to the domain.
    /// You have exceeded the maximum number of computer accounts you are allowed to create in this domain.
    /// Contact your system administrator to have this limit reset or increased.
    DS_MACHINE_ACCOUNT_QUOTA_EXCEEDED = 0xC00002E7,
    /// This operation cannot be performed on the current domain.
    CURRENT_DOMAIN_NOT_ALLOWED = 0xC00002E9,
    /// The directory or file cannot be created.
    CANNOT_MAKE = 0xC00002EA,
    /// The system is in the process of shutting down.
    SYSTEM_SHUTDOWN = 0xC00002EB,
    /// Directory Services could not start because of the following error: %hs Error Status: 0x%x. Click OK to shut down the system.
    /// You can use the recovery console to diagnose the system further.
    DS_INIT_FAILURE_CONSOLE = 0xC00002EC,
    /// Security Accounts Manager initialization failed because of the following error: %hs Error Status: 0x%x. Click OK to shut down the system.
    /// You can use the recovery console to diagnose the system further.
    DS_SAM_INIT_FAILURE_CONSOLE = 0xC00002ED,
    /// A security context was deleted before the context was completed. This is considered a logon failure.
    UNFINISHED_CONTEXT_DELETED = 0xC00002EE,
    /// The client is trying to negotiate a context and the server requires user-to-user but did not send a TGT reply.
    NO_TGT_REPLY = 0xC00002EF,
    /// An object ID was not found in the file.
    OBJECTID_NOT_FOUND = 0xC00002F0,
    /// Unable to accomplish the requested task because the local machine does not have any IP addresses.
    NO_IP_ADDRESSES = 0xC00002F1,
    /// The supplied credential handle does not match the credential that is associated with the security context.
    WRONG_CREDENTIAL_HANDLE = 0xC00002F2,
    /// The crypto system or checksum function is invalid because a required function is unavailable.
    CRYPTO_SYSTEM_INVALID = 0xC00002F3,
    /// The number of maximum ticket referrals has been exceeded.
    MAX_REFERRALS_EXCEEDED = 0xC00002F4,
    /// The local machine must be a Kerberos KDC (domain controller) and it is not.
    MUST_BE_KDC = 0xC00002F5,
    /// The other end of the security negotiation requires strong crypto but it is not supported on the local machine.
    STRONG_CRYPTO_NOT_SUPPORTED = 0xC00002F6,
    /// The KDC reply contained more than one principal name.
    TOO_MANY_PRINCIPALS = 0xC00002F7,
    /// Expected to find PA data for a hint of what etype to use, but it was not found.
    NO_PA_DATA = 0xC00002F8,
    /// The client certificate does not contain a valid UPN, or does not match the client name in the logon request. Contact your administrator.
    PKINIT_NAME_MISMATCH = 0xC00002F9,
    /// Smart card logon is required and was not used.
    SMARTCARD_LOGON_REQUIRED = 0xC00002FA,
    /// An invalid request was sent to the KDC.
    KDC_INVALID_REQUEST = 0xC00002FB,
    /// The KDC was unable to generate a referral for the service requested.
    KDC_UNABLE_TO_REFER = 0xC00002FC,
    /// The encryption type requested is not supported by the KDC.
    KDC_UNKNOWN_ETYPE = 0xC00002FD,
    /// A system shutdown is in progress.
    SHUTDOWN_IN_PROGRESS = 0xC00002FE,
    /// The server machine is shutting down.
    SERVER_SHUTDOWN_IN_PROGRESS = 0xC00002FF,
    /// This operation is not supported on a computer running Windows Server 2003 operating system for Small Business Server.
    NOT_SUPPORTED_ON_SBS = 0xC0000300,
    /// The WMI GUID is no longer available.
    WMI_GUID_DISCONNECTED = 0xC0000301,
    /// Collection or events for the WMI GUID is already disabled.
    WMI_ALREADY_DISABLED = 0xC0000302,
    /// Collection or events for the WMI GUID is already enabled.
    WMI_ALREADY_ENABLED = 0xC0000303,
    /// The master file table on the volume is too fragmented to complete this operation.
    MFT_TOO_FRAGMENTED = 0xC0000304,
    /// Copy protection failure.
    COPY_PROTECTION_FAILURE = 0xC0000305,
    /// Copy protection error—DVD CSS Authentication failed.
    CSS_AUTHENTICATION_FAILURE = 0xC0000306,
    /// Copy protection error—The specified sector does not contain a valid key.
    CSS_KEY_NOT_PRESENT = 0xC0000307,
    /// Copy protection error—DVD session key not established.
    CSS_KEY_NOT_ESTABLISHED = 0xC0000308,
    /// Copy protection error—The read failed because the sector is encrypted.
    CSS_SCRAMBLED_SECTOR = 0xC0000309,
    /// Copy protection error—The region of the specified DVD does not correspond to the region setting of the drive.
    CSS_REGION_MISMATCH = 0xC000030A,
    /// Copy protection error—The region setting of the drive might be permanent.
    CSS_RESETS_EXHAUSTED = 0xC000030B,
    /// The Kerberos protocol encountered an error while validating the KDC certificate during smart card logon.
    /// There is more information in the system event log.
    PKINIT_FAILURE = 0xC0000320,
    /// The Kerberos protocol encountered an error while attempting to use the smart card subsystem.
    SMARTCARD_SUBSYSTEM_FAILURE = 0xC0000321,
    /// The target server does not have acceptable Kerberos credentials.
    NO_KERB_KEY = 0xC0000322,
    /// The transport determined that the remote system is down.
    HOST_DOWN = 0xC0000350,
    /// An unsupported pre-authentication mechanism was presented to the Kerberos package.
    UNSUPPORTED_PREAUTH = 0xC0000351,
    /// The encryption algorithm that is used on the source file needs a bigger key buffer than the one that is used on the destination file.
    EFS_ALG_BLOB_TOO_BIG = 0xC0000352,
    /// An attempt to remove a processes DebugPort was made, but a port was not already associated with the process.
    PORT_NOT_SET = 0xC0000353,
    /// An attempt to do an operation on a debug port failed because the port is in the process of being deleted.
    DEBUGGER_INACTIVE = 0xC0000354,
    /// This version of Windows is not compatible with the behavior version of the directory forest, domain, or domain controller.
    DS_VERSION_CHECK_FAILURE = 0xC0000355,
    /// The specified event is currently not being audited.
    AUDITING_DISABLED = 0xC0000356,
    /// The machine account was created prior to Windows NT 4.0 operating system. The account needs to be recreated.
    PRENT4_MACHINE_ACCOUNT = 0xC0000357,
    /// An account group cannot have a universal group as a member.
    DS_AG_CANT_HAVE_UNIVERSAL_MEMBER = 0xC0000358,
    /// The specified image file did not have the correct format; it appears to be a 32-bit Windows image.
    INVALID_IMAGE_WIN_32 = 0xC0000359,
    /// The specified image file did not have the correct format; it appears to be a 64-bit Windows image.
    INVALID_IMAGE_WIN_64 = 0xC000035A,
    /// The client's supplied SSPI channel bindings were incorrect.
    BAD_BINDINGS = 0xC000035B,
    /// The client session has expired; so the client must re-authenticate to continue accessing the remote resources.
    NETWORK_SESSION_EXPIRED = 0xC000035C,
    /// The AppHelp dialog box canceled; thus preventing the application from starting.
    APPHELP_BLOCK = 0xC000035D,
    /// The SID filtering operation removed all SIDs.
    ALL_SIDS_FILTERED = 0xC000035E,
    /// The driver was not loaded because the system is starting in safe mode.
    NOT_SAFE_MODE_DRIVER = 0xC000035F,
    /// Access to %1 has been restricted by your Administrator by the default software restriction policy level.
    ACCESS_DISABLED_BY_POLICY_DEFAULT = 0xC0000361,
    /// Access to %1 has been restricted by your Administrator by location with policy rule %2 placed on path %3.
    ACCESS_DISABLED_BY_POLICY_PATH = 0xC0000362,
    /// Access to %1 has been restricted by your Administrator by software publisher policy.
    ACCESS_DISABLED_BY_POLICY_PUBLISHER = 0xC0000363,
    /// Access to %1 has been restricted by your Administrator by policy rule %2.
    ACCESS_DISABLED_BY_POLICY_OTHER = 0xC0000364,
    /// The driver was not loaded because it failed its initialization call.
    FAILED_DRIVER_ENTRY = 0xC0000365,
    /// The device encountered an error while applying power or reading the device configuration.
    /// This might be caused by a failure of your hardware or by a poor connection.
    DEVICE_ENUMERATION_ERROR = 0xC0000366,
    /// The create operation failed because the name contained at least one mount point that resolves to a volume to which the specified device object is not attached.
    MOUNT_POINT_NOT_RESOLVED = 0xC0000368,
    /// The device object parameter is either not a valid device object or is not attached to the volume that is specified by the file name.
    INVALID_DEVICE_OBJECT_PARAMETER = 0xC0000369,
    /// A machine check error has occurred.
    /// Check the system event log for additional information.
    MCA_OCCURED = 0xC000036A,
    /// Driver %2 has been blocked from loading.
    DRIVER_BLOCKED_CRITICAL = 0xC000036B,
    /// Driver %2 has been blocked from loading.
    DRIVER_BLOCKED = 0xC000036C,
    /// There was error [%2] processing the driver database.
    DRIVER_DATABASE_ERROR = 0xC000036D,
    /// System hive size has exceeded its limit.
    SYSTEM_HIVE_TOO_LARGE = 0xC000036E,
    /// A dynamic link library (DLL) referenced a module that was neither a DLL nor the process's executable image.
    INVALID_IMPORT_OF_NON_DLL = 0xC000036F,
    /// The local account store does not contain secret material for the specified account.
    NO_SECRETS = 0xC0000371,
    /// Access to %1 has been restricted by your Administrator by policy rule %2.
    ACCESS_DISABLED_NO_SAFER_UI_BY_POLICY = 0xC0000372,
    /// The system was not able to allocate enough memory to perform a stack switch.
    FAILED_STACK_SWITCH = 0xC0000373,
    /// A heap has been corrupted.
    HEAP_CORRUPTION = 0xC0000374,
    /// An incorrect PIN was presented to the smart card.
    SMARTCARD_WRONG_PIN = 0xC0000380,
    /// The smart card is blocked.
    SMARTCARD_CARD_BLOCKED = 0xC0000381,
    /// No PIN was presented to the smart card.
    SMARTCARD_CARD_NOT_AUTHENTICATED = 0xC0000382,
    /// No smart card is available.
    SMARTCARD_NO_CARD = 0xC0000383,
    /// The requested key container does not exist on the smart card.
    SMARTCARD_NO_KEY_CONTAINER = 0xC0000384,
    /// The requested certificate does not exist on the smart card.
    SMARTCARD_NO_CERTIFICATE = 0xC0000385,
    /// The requested keyset does not exist.
    SMARTCARD_NO_KEYSET = 0xC0000386,
    /// A communication error with the smart card has been detected.
    SMARTCARD_IO_ERROR = 0xC0000387,
    /// The system detected a possible attempt to compromise security.
    /// Ensure that you can contact the server that authenticated you.
    DOWNGRADE_DETECTED = 0xC0000388,
    /// The smart card certificate used for authentication has been revoked. Contact your system administrator.
    /// There might be additional information in the event log.
    SMARTCARD_CERT_REVOKED = 0xC0000389,
    /// An untrusted certificate authority was detected while processing the smart card certificate that is used for authentication. Contact your system administrator.
    ISSUING_CA_UNTRUSTED = 0xC000038A,
    /// The revocation status of the smart card certificate that is used for authentication could not be determined. Contact your system administrator.
    REVOCATION_OFFLINE_C = 0xC000038B,
    /// The smart card certificate used for authentication was not trusted. Contact your system administrator.
    PKINIT_CLIENT_FAILURE = 0xC000038C,
    /// The smart card certificate used for authentication has expired. Contact your system administrator.
    SMARTCARD_CERT_EXPIRED = 0xC000038D,
    /// The driver could not be loaded because a previous version of the driver is still in memory.
    DRIVER_FAILED_PRIOR_UNLOAD = 0xC000038E,
    /// The smart card provider could not perform the action because the context was acquired as silent.
    SMARTCARD_SILENT_CONTEXT = 0xC000038F,
    /// The delegated trust creation quota of the current user has been exceeded.
    PER_USER_TRUST_QUOTA_EXCEEDED = 0xC0000401,
    /// The total delegated trust creation quota has been exceeded.
    ALL_USER_TRUST_QUOTA_EXCEEDED = 0xC0000402,
    /// The delegated trust deletion quota of the current user has been exceeded.
    USER_DELETE_TRUST_QUOTA_EXCEEDED = 0xC0000403,
    /// The requested name already exists as a unique identifier.
    DS_NAME_NOT_UNIQUE = 0xC0000404,
    /// The requested object has a non-unique identifier and cannot be retrieved.
    DS_DUPLICATE_ID_FOUND = 0xC0000405,
    /// The group cannot be converted due to attribute restrictions on the requested group type.
    DS_GROUP_CONVERSION_ERROR = 0xC0000406,
    /// {Volume Shadow Copy Service} Wait while the Volume Shadow Copy Service prepares volume %hs for hibernation.
    VOLSNAP_PREPARE_HIBERNATE = 0xC0000407,
    /// Kerberos sub-protocol User2User is required.
    USER2USER_REQUIRED = 0xC0000408,
    /// The system detected an overrun of a stack-based buffer in this application.
    /// This overrun could potentially allow a malicious user to gain control of this application.
    STACK_BUFFER_OVERRUN = 0xC0000409,
    /// The Kerberos subsystem encountered an error.
    /// A service for user protocol request was made against a domain controller which does not support service for user.
    NO_S4U_PROT_SUPPORT = 0xC000040A,
    /// An attempt was made by this server to make a Kerberos constrained delegation request for a target that is outside the server realm.
    /// This action is not supported and the resulting error indicates a misconfiguration on the allowed-to-delegate-to list for this server. Contact your administrator.
    CROSSREALM_DELEGATION_FAILURE = 0xC000040B,
    /// The revocation status of the domain controller certificate used for smart card authentication could not be determined.
    /// There is additional information in the system event log. Contact your system administrator.
    REVOCATION_OFFLINE_KDC = 0xC000040C,
    /// An untrusted certificate authority was detected while processing the domain controller certificate used for authentication.
    /// There is additional information in the system event log. Contact your system administrator.
    ISSUING_CA_UNTRUSTED_KDC = 0xC000040D,
    /// The domain controller certificate used for smart card logon has expired.
    /// Contact your system administrator with the contents of your system event log.
    KDC_CERT_EXPIRED = 0xC000040E,
    /// The domain controller certificate used for smart card logon has been revoked.
    /// Contact your system administrator with the contents of your system event log.
    KDC_CERT_REVOKED = 0xC000040F,
    /// Data present in one of the parameters is more than the function can operate on.
    PARAMETER_QUOTA_EXCEEDED = 0xC0000410,
    /// The system has failed to hibernate (The error code is %hs).
    /// Hibernation will be disabled until the system is restarted.
    HIBERNATION_FAILURE = 0xC0000411,
    /// An attempt to delay-load a .dll or get a function address in a delay-loaded .dll failed.
    DELAY_LOAD_FAILED = 0xC0000412,
    /// Logon Failure: The machine you are logging onto is protected by an authentication firewall.
    /// The specified account is not allowed to authenticate to the machine.
    AUTHENTICATION_FIREWALL_FAILED = 0xC0000413,
    /// %hs is a 16-bit application. You do not have permissions to execute 16-bit applications.
    /// Check your permissions with your system administrator.
    VDM_DISALLOWED = 0xC0000414,
    /// {Display Driver Stopped Responding} The %hs display driver has stopped working normally.
    /// Save your work and reboot the system to restore full display functionality.
    /// The next time you reboot the machine a dialog will be displayed giving you a chance to report this failure to Microsoft.
    HUNG_DISPLAY_DRIVER_THREAD = 0xC0000415,
    /// The Desktop heap encountered an error while allocating session memory.
    /// There is more information in the system event log.
    INSUFFICIENT_RESOURCE_FOR_SPECIFIED_SHARED_SECTION_SIZE = 0xC0000416,
    /// An invalid parameter was passed to a C runtime function.
    INVALID_CRUNTIME_PARAMETER = 0xC0000417,
    /// The authentication failed because NTLM was blocked.
    NTLM_BLOCKED = 0xC0000418,
    /// The source object's SID already exists in destination forest.
    DS_SRC_SID_EXISTS_IN_FOREST = 0xC0000419,
    /// The domain name of the trusted domain already exists in the forest.
    DS_DOMAIN_NAME_EXISTS_IN_FOREST = 0xC000041A,
    /// The flat name of the trusted domain already exists in the forest.
    DS_FLAT_NAME_EXISTS_IN_FOREST = 0xC000041B,
    /// The User Principal Name (UPN) is invalid.
    INVALID_USER_PRINCIPAL_NAME = 0xC000041C,
    /// There has been an assertion failure.
    ASSERTION_FAILURE = 0xC0000420,
    /// Application verifier has found an error in the current process.
    VERIFIER_STOP = 0xC0000421,
    /// A user mode unwind is in progress.
    CALLBACK_POP_STACK = 0xC0000423,
    /// %2 has been blocked from loading due to incompatibility with this system.
    /// Contact your software vendor for a compatible version of the driver.
    INCOMPATIBLE_DRIVER_BLOCKED = 0xC0000424,
    /// Illegal operation attempted on a registry key which has already been unloaded.
    HIVE_UNLOADED = 0xC0000425,
    /// Compression is disabled for this volume.
    COMPRESSION_DISABLED = 0xC0000426,
    /// The requested operation could not be completed due to a file system limitation.
    FILE_SYSTEM_LIMITATION = 0xC0000427,
    /// The hash for image %hs cannot be found in the system catalogs.
    /// The image is likely corrupt or the victim of tampering.
    INVALID_IMAGE_HASH = 0xC0000428,
    /// The implementation is not capable of performing the request.
    NOT_CAPABLE = 0xC0000429,
    /// The requested operation is out of order with respect to other operations.
    REQUEST_OUT_OF_SEQUENCE = 0xC000042A,
    /// An operation attempted to exceed an implementation-defined limit.
    IMPLEMENTATION_LIMIT = 0xC000042B,
    /// The requested operation requires elevation.
    ELEVATION_REQUIRED = 0xC000042C,
    /// The required security context does not exist.
    NO_SECURITY_CONTEXT = 0xC000042D,
    /// The PKU2U protocol encountered an error while attempting to utilize the associated certificates.
    PKU2U_CERT_FAILURE = 0xC000042E,
    /// The operation was attempted beyond the valid data length of the file.
    BEYOND_VDL = 0xC0000432,
    /// The attempted write operation encountered a write already in progress for some portion of the range.
    ENCOUNTERED_WRITE_IN_PROGRESS = 0xC0000433,
    /// The page fault mappings changed in the middle of processing a fault so the operation must be retried.
    PTE_CHANGED = 0xC0000434,
    /// The attempt to purge this file from memory failed to purge some or all the data from memory.
    PURGE_FAILED = 0xC0000435,
    /// The requested credential requires confirmation.
    CRED_REQUIRES_CONFIRMATION = 0xC0000440,
    /// The remote server sent an invalid response for a file being opened with Client Side Encryption.
    CS_ENCRYPTION_INVALID_SERVER_RESPONSE = 0xC0000441,
    /// Client Side Encryption is not supported by the remote server even though it claims to support it.
    CS_ENCRYPTION_UNSUPPORTED_SERVER = 0xC0000442,
    /// File is encrypted and should be opened in Client Side Encryption mode.
    CS_ENCRYPTION_EXISTING_ENCRYPTED_FILE = 0xC0000443,
    /// A new encrypted file is being created and a $EFS needs to be provided.
    CS_ENCRYPTION_NEW_ENCRYPTED_FILE = 0xC0000444,
    /// The SMB client requested a CSE FSCTL on a non-CSE file.
    CS_ENCRYPTION_FILE_NOT_CSE = 0xC0000445,
    /// Indicates a particular Security ID cannot be assigned as the label of an object.
    INVALID_LABEL = 0xC0000446,
    /// The process hosting the driver for this device has terminated.
    DRIVER_PROCESS_TERMINATED = 0xC0000450,
    /// The requested system device cannot be identified due to multiple indistinguishable devices potentially matching the identification criteria.
    AMBIGUOUS_SYSTEM_DEVICE = 0xC0000451,
    /// The requested system device cannot be found.
    SYSTEM_DEVICE_NOT_FOUND = 0xC0000452,
    /// This boot application must be restarted.
    RESTART_BOOT_APPLICATION = 0xC0000453,
    /// Insufficient NVRAM resources exist to complete the API.  A reboot might be required.
    INSUFFICIENT_NVRAM_RESOURCES = 0xC0000454,
    /// No ranges for the specified operation were able to be processed.
    NO_RANGES_PROCESSED = 0xC0000460,
    /// The storage device does not support Offload Write.
    DEVICE_FEATURE_NOT_SUPPORTED = 0xC0000463,
    /// Data cannot be moved because the source device cannot communicate with the destination device.
    DEVICE_UNREACHABLE = 0xC0000464,
    /// The token representing the data is invalid or expired.
    INVALID_TOKEN = 0xC0000465,
    /// The file server is temporarily unavailable.
    SERVER_UNAVAILABLE = 0xC0000466,
    /// The specified task name is invalid.
    INVALID_TASK_NAME = 0xC0000500,
    /// The specified task index is invalid.
    INVALID_TASK_INDEX = 0xC0000501,
    /// The specified thread is already joining a task.
    THREAD_ALREADY_IN_TASK = 0xC0000502,
    /// A callback has requested to bypass native code.
    CALLBACK_BYPASS = 0xC0000503,
    /// A fail fast exception occurred.
    /// Exception handlers will not be invoked and the process will be terminated immediately.
    FAIL_FAST_EXCEPTION = 0xC0000602,
    /// Windows cannot verify the digital signature for this file.
    /// The signing certificate for this file has been revoked.
    IMAGE_CERT_REVOKED = 0xC0000603,
    /// The ALPC port is closed.
    PORT_CLOSED = 0xC0000700,
    /// The ALPC message requested is no longer available.
    MESSAGE_LOST = 0xC0000701,
    /// The ALPC message supplied is invalid.
    INVALID_MESSAGE = 0xC0000702,
    /// The ALPC message has been canceled.
    REQUEST_CANCELED = 0xC0000703,
    /// Invalid recursive dispatch attempt.
    RECURSIVE_DISPATCH = 0xC0000704,
    /// No receive buffer has been supplied in a synchronous request.
    LPC_RECEIVE_BUFFER_EXPECTED = 0xC0000705,
    /// The connection port is used in an invalid context.
    LPC_INVALID_CONNECTION_USAGE = 0xC0000706,
    /// The ALPC port does not accept new request messages.
    LPC_REQUESTS_NOT_ALLOWED = 0xC0000707,
    /// The resource requested is already in use.
    RESOURCE_IN_USE = 0xC0000708,
    /// The hardware has reported an uncorrectable memory error.
    HARDWARE_MEMORY_ERROR = 0xC0000709,
    /// Status 0x%08x was returned, waiting on handle 0x%x for wait 0x%p, in waiter 0x%p.
    THREADPOOL_HANDLE_EXCEPTION = 0xC000070A,
    /// After a callback to 0x%p(0x%p), a completion call to Set event(0x%p) failed with status 0x%08x.
    THREADPOOL_SET_EVENT_ON_COMPLETION_FAILED = 0xC000070B,
    /// After a callback to 0x%p(0x%p), a completion call to ReleaseSemaphore(0x%p, %d) failed with status 0x%08x.
    THREADPOOL_RELEASE_SEMAPHORE_ON_COMPLETION_FAILED = 0xC000070C,
    /// After a callback to 0x%p(0x%p), a completion call to ReleaseMutex(%p) failed with status 0x%08x.
    THREADPOOL_RELEASE_MUTEX_ON_COMPLETION_FAILED = 0xC000070D,
    /// After a callback to 0x%p(0x%p), a completion call to FreeLibrary(%p) failed with status 0x%08x.
    THREADPOOL_FREE_LIBRARY_ON_COMPLETION_FAILED = 0xC000070E,
    /// The thread pool 0x%p was released while a thread was posting a callback to 0x%p(0x%p) to it.
    THREADPOOL_RELEASED_DURING_OPERATION = 0xC000070F,
    /// A thread pool worker thread is impersonating a client, after a callback to 0x%p(0x%p).
    /// This is unexpected, indicating that the callback is missing a call to revert the impersonation.
    CALLBACK_RETURNED_WHILE_IMPERSONATING = 0xC0000710,
    /// A thread pool worker thread is impersonating a client, after executing an APC.
    /// This is unexpected, indicating that the APC is missing a call to revert the impersonation.
    APC_RETURNED_WHILE_IMPERSONATING = 0xC0000711,
    /// Either the target process, or the target thread's containing process, is a protected process.
    PROCESS_IS_PROTECTED = 0xC0000712,
    /// A thread is getting dispatched with MCA EXCEPTION because of MCA.
    MCA_EXCEPTION = 0xC0000713,
    /// The client certificate account mapping is not unique.
    CERTIFICATE_MAPPING_NOT_UNIQUE = 0xC0000714,
    /// The symbolic link cannot be followed because its type is disabled.
    SYMLINK_CLASS_DISABLED = 0xC0000715,
    /// Indicates that the specified string is not valid for IDN normalization.
    INVALID_IDN_NORMALIZATION = 0xC0000716,
    /// No mapping for the Unicode character exists in the target multi-byte code page.
    NO_UNICODE_TRANSLATION = 0xC0000717,
    /// The provided callback is already registered.
    ALREADY_REGISTERED = 0xC0000718,
    /// The provided context did not match the target.
    CONTEXT_MISMATCH = 0xC0000719,
    /// The specified port already has a completion list.
    PORT_ALREADY_HAS_COMPLETION_LIST = 0xC000071A,
    /// A threadpool worker thread entered a callback at thread base priority 0x%x and exited at priority 0x%x.
    /// This is unexpected, indicating that the callback missed restoring the priority.
    CALLBACK_RETURNED_THREAD_PRIORITY = 0xC000071B,
    /// An invalid thread, handle %p, is specified for this operation.
    /// Possibly, a threadpool worker thread was specified.
    INVALID_THREAD = 0xC000071C,
    /// A threadpool worker thread entered a callback, which left transaction state.
    /// This is unexpected, indicating that the callback missed clearing the transaction.
    CALLBACK_RETURNED_TRANSACTION = 0xC000071D,
    /// A threadpool worker thread entered a callback, which left the loader lock held.
    /// This is unexpected, indicating that the callback missed releasing the lock.
    CALLBACK_RETURNED_LDR_LOCK = 0xC000071E,
    /// A threadpool worker thread entered a callback, which left with preferred languages set.
    /// This is unexpected, indicating that the callback missed clearing them.
    CALLBACK_RETURNED_LANG = 0xC000071F,
    /// A threadpool worker thread entered a callback, which left with background priorities set.
    /// This is unexpected, indicating that the callback missed restoring the original priorities.
    CALLBACK_RETURNED_PRI_BACK = 0xC0000720,
    /// The attempted operation required self healing to be enabled.
    DISK_REPAIR_DISABLED = 0xC0000800,
    /// The directory service cannot perform the requested operation because a domain rename operation is in progress.
    DS_DOMAIN_RENAME_IN_PROGRESS = 0xC0000801,
    /// An operation failed because the storage quota was exceeded.
    DISK_QUOTA_EXCEEDED = 0xC0000802,
    /// An operation failed because the content was blocked.
    CONTENT_BLOCKED = 0xC0000804,
    /// The operation could not be completed due to bad clusters on disk.
    BAD_CLUSTERS = 0xC0000805,
    /// The operation could not be completed because the volume is dirty. Please run the Chkdsk utility and try again.
    VOLUME_DIRTY = 0xC0000806,
    /// This file is checked out or locked for editing by another user.
    FILE_CHECKED_OUT = 0xC0000901,
    /// The file must be checked out before saving changes.
    CHECKOUT_REQUIRED = 0xC0000902,
    /// The file type being saved or retrieved has been blocked.
    BAD_FILE_TYPE = 0xC0000903,
    /// The file size exceeds the limit allowed and cannot be saved.
    FILE_TOO_LARGE = 0xC0000904,
    /// Access Denied. Before opening files in this location, you must first browse to the e.g.
    /// site and select the option to log on automatically.
    FORMS_AUTH_REQUIRED = 0xC0000905,
    /// The operation did not complete successfully because the file contains a virus.
    VIRUS_INFECTED = 0xC0000906,
    /// This file contains a virus and cannot be opened.
    /// Due to the nature of this virus, the file has been removed from this location.
    VIRUS_DELETED = 0xC0000907,
    /// The resources required for this device conflict with the MCFG table.
    BAD_MCFG_TABLE = 0xC0000908,
    /// The operation did not complete successfully because it would cause an oplock to be broken.
    /// The caller has requested that existing oplocks not be broken.
    CANNOT_BREAK_OPLOCK = 0xC0000909,
    /// WOW Assertion Error.
    WOW_ASSERTION = 0xC0009898,
    /// The cryptographic signature is invalid.
    INVALID_SIGNATURE = 0xC000A000,
    /// The cryptographic provider does not support HMAC.
    HMAC_NOT_SUPPORTED = 0xC000A001,
    /// The IPsec queue overflowed.
    IPSEC_QUEUE_OVERFLOW = 0xC000A010,
    /// The neighbor discovery queue overflowed.
    ND_QUEUE_OVERFLOW = 0xC000A011,
    /// An Internet Control Message Protocol (ICMP) hop limit exceeded error was received.
    HOPLIMIT_EXCEEDED = 0xC000A012,
    /// The protocol is not installed on the local machine.
    PROTOCOL_NOT_SUPPORTED = 0xC000A013,
    /// {Delayed Write Failed} Windows was unable to save all the data for the file %hs; the data has been lost.
    /// This error might be caused by network connectivity issues. Try to save this file elsewhere.
    LOST_WRITEBEHIND_DATA_NETWORK_DISCONNECTED = 0xC000A080,
    /// {Delayed Write Failed} Windows was unable to save all the data for the file %hs; the data has been lost.
    /// This error was returned by the server on which the file exists. Try to save this file elsewhere.
    LOST_WRITEBEHIND_DATA_NETWORK_SERVER_ERROR = 0xC000A081,
    /// {Delayed Write Failed} Windows was unable to save all the data for the file %hs; the data has been lost.
    /// This error might be caused if the device has been removed or the media is write-protected.
    LOST_WRITEBEHIND_DATA_LOCAL_DISK_ERROR = 0xC000A082,
    /// Windows was unable to parse the requested XML data.
    XML_PARSE_ERROR = 0xC000A083,
    /// An error was encountered while processing an XML digital signature.
    XMLDSIG_ERROR = 0xC000A084,
    /// This indicates that the caller made the connection request in the wrong routing compartment.
    WRONG_COMPARTMENT = 0xC000A085,
    /// This indicates that there was an AuthIP failure when attempting to connect to the remote host.
    AUTHIP_FAILURE = 0xC000A086,
    /// OID mapped groups cannot have members.
    DS_OID_MAPPED_GROUP_CANT_HAVE_MEMBERS = 0xC000A087,
    /// The specified OID cannot be found.
    DS_OID_NOT_FOUND = 0xC000A088,
    /// Hash generation for the specified version and hash type is not enabled on server.
    HASH_NOT_SUPPORTED = 0xC000A100,
    /// The hash requests is not present or not up to date with the current file contents.
    HASH_NOT_PRESENT = 0xC000A101,
    /// A file system filter on the server has not opted in for Offload Read support.
    OFFLOAD_READ_FLT_NOT_SUPPORTED = 0xC000A2A1,
    /// A file system filter on the server has not opted in for Offload Write support.
    OFFLOAD_WRITE_FLT_NOT_SUPPORTED = 0xC000A2A2,
    /// Offload read operations cannot be performed on:
    ///   - Compressed files
    ///   - Sparse files
    ///   - Encrypted files
    ///   - File system metadata files
    OFFLOAD_READ_FILE_NOT_SUPPORTED = 0xC000A2A3,
    /// Offload write operations cannot be performed on:
    ///  - Compressed files
    ///  - Sparse files
    ///  - Encrypted files
    ///  - File system metadata files
    OFFLOAD_WRITE_FILE_NOT_SUPPORTED = 0xC000A2A4,
    /// The debugger did not perform a state change.
    DBG_NO_STATE_CHANGE = 0xC0010001,
    /// The debugger found that the application is not idle.
    DBG_APP_NOT_IDLE = 0xC0010002,
    /// The string binding is invalid.
    RPC_NT_INVALID_STRING_BINDING = 0xC0020001,
    /// The binding handle is not the correct type.
    RPC_NT_WRONG_KIND_OF_BINDING = 0xC0020002,
    /// The binding handle is invalid.
    RPC_NT_INVALID_BINDING = 0xC0020003,
    /// The RPC protocol sequence is not supported.
    RPC_NT_PROTSEQ_NOT_SUPPORTED = 0xC0020004,
    /// The RPC protocol sequence is invalid.
    RPC_NT_INVALID_RPC_PROTSEQ = 0xC0020005,
    /// The string UUID is invalid.
    RPC_NT_INVALID_STRING_UUID = 0xC0020006,
    /// The endpoint format is invalid.
    RPC_NT_INVALID_ENDPOINT_FORMAT = 0xC0020007,
    /// The network address is invalid.
    RPC_NT_INVALID_NET_ADDR = 0xC0020008,
    /// No endpoint was found.
    RPC_NT_NO_ENDPOINT_FOUND = 0xC0020009,
    /// The time-out value is invalid.
    RPC_NT_INVALID_TIMEOUT = 0xC002000A,
    /// The object UUID was not found.
    RPC_NT_OBJECT_NOT_FOUND = 0xC002000B,
    /// The object UUID has already been registered.
    RPC_NT_ALREADY_REGISTERED = 0xC002000C,
    /// The type UUID has already been registered.
    RPC_NT_TYPE_ALREADY_REGISTERED = 0xC002000D,
    /// The RPC server is already listening.
    RPC_NT_ALREADY_LISTENING = 0xC002000E,
    /// No protocol sequences have been registered.
    RPC_NT_NO_PROTSEQS_REGISTERED = 0xC002000F,
    /// The RPC server is not listening.
    RPC_NT_NOT_LISTENING = 0xC0020010,
    /// The manager type is unknown.
    RPC_NT_UNKNOWN_MGR_TYPE = 0xC0020011,
    /// The interface is unknown.
    RPC_NT_UNKNOWN_IF = 0xC0020012,
    /// There are no bindings.
    RPC_NT_NO_BINDINGS = 0xC0020013,
    /// There are no protocol sequences.
    RPC_NT_NO_PROTSEQS = 0xC0020014,
    /// The endpoint cannot be created.
    RPC_NT_CANT_CREATE_ENDPOINT = 0xC0020015,
    /// Insufficient resources are available to complete this operation.
    RPC_NT_OUT_OF_RESOURCES = 0xC0020016,
    /// The RPC server is unavailable.
    RPC_NT_SERVER_UNAVAILABLE = 0xC0020017,
    /// The RPC server is too busy to complete this operation.
    RPC_NT_SERVER_TOO_BUSY = 0xC0020018,
    /// The network options are invalid.
    RPC_NT_INVALID_NETWORK_OPTIONS = 0xC0020019,
    /// No RPCs are active on this thread.
    RPC_NT_NO_CALL_ACTIVE = 0xC002001A,
    /// The RPC failed.
    RPC_NT_CALL_FAILED = 0xC002001B,
    /// The RPC failed and did not execute.
    RPC_NT_CALL_FAILED_DNE = 0xC002001C,
    /// An RPC protocol error occurred.
    RPC_NT_PROTOCOL_ERROR = 0xC002001D,
    /// The RPC server does not support the transfer syntax.
    RPC_NT_UNSUPPORTED_TRANS_SYN = 0xC002001F,
    /// The type UUID is not supported.
    RPC_NT_UNSUPPORTED_TYPE = 0xC0020021,
    /// The tag is invalid.
    RPC_NT_INVALID_TAG = 0xC0020022,
    /// The array bounds are invalid.
    RPC_NT_INVALID_BOUND = 0xC0020023,
    /// The binding does not contain an entry name.
    RPC_NT_NO_ENTRY_NAME = 0xC0020024,
    /// The name syntax is invalid.
    RPC_NT_INVALID_NAME_SYNTAX = 0xC0020025,
    /// The name syntax is not supported.
    RPC_NT_UNSUPPORTED_NAME_SYNTAX = 0xC0020026,
    /// No network address is available to construct a UUID.
    RPC_NT_UUID_NO_ADDRESS = 0xC0020028,
    /// The endpoint is a duplicate.
    RPC_NT_DUPLICATE_ENDPOINT = 0xC0020029,
    /// The authentication type is unknown.
    RPC_NT_UNKNOWN_AUTHN_TYPE = 0xC002002A,
    /// The maximum number of calls is too small.
    RPC_NT_MAX_CALLS_TOO_SMALL = 0xC002002B,
    /// The string is too long.
    RPC_NT_STRING_TOO_LONG = 0xC002002C,
    /// The RPC protocol sequence was not found.
    RPC_NT_PROTSEQ_NOT_FOUND = 0xC002002D,
    /// The procedure number is out of range.
    RPC_NT_PROCNUM_OUT_OF_RANGE = 0xC002002E,
    /// The binding does not contain any authentication information.
    RPC_NT_BINDING_HAS_NO_AUTH = 0xC002002F,
    /// The authentication service is unknown.
    RPC_NT_UNKNOWN_AUTHN_SERVICE = 0xC0020030,
    /// The authentication level is unknown.
    RPC_NT_UNKNOWN_AUTHN_LEVEL = 0xC0020031,
    /// The security context is invalid.
    RPC_NT_INVALID_AUTH_IDENTITY = 0xC0020032,
    /// The authorization service is unknown.
    RPC_NT_UNKNOWN_AUTHZ_SERVICE = 0xC0020033,
    /// The entry is invalid.
    EPT_NT_INVALID_ENTRY = 0xC0020034,
    /// The operation cannot be performed.
    EPT_NT_CANT_PERFORM_OP = 0xC0020035,
    /// No more endpoints are available from the endpoint mapper.
    EPT_NT_NOT_REGISTERED = 0xC0020036,
    /// No interfaces have been exported.
    RPC_NT_NOTHING_TO_EXPORT = 0xC0020037,
    /// The entry name is incomplete.
    RPC_NT_INCOMPLETE_NAME = 0xC0020038,
    /// The version option is invalid.
    RPC_NT_INVALID_VERS_OPTION = 0xC0020039,
    /// There are no more members.
    RPC_NT_NO_MORE_MEMBERS = 0xC002003A,
    /// There is nothing to unexport.
    RPC_NT_NOT_ALL_OBJS_UNEXPORTED = 0xC002003B,
    /// The interface was not found.
    RPC_NT_INTERFACE_NOT_FOUND = 0xC002003C,
    /// The entry already exists.
    RPC_NT_ENTRY_ALREADY_EXISTS = 0xC002003D,
    /// The entry was not found.
    RPC_NT_ENTRY_NOT_FOUND = 0xC002003E,
    /// The name service is unavailable.
    RPC_NT_NAME_SERVICE_UNAVAILABLE = 0xC002003F,
    /// The network address family is invalid.
    RPC_NT_INVALID_NAF_ID = 0xC0020040,
    /// The requested operation is not supported.
    RPC_NT_CANNOT_SUPPORT = 0xC0020041,
    /// No security context is available to allow impersonation.
    RPC_NT_NO_CONTEXT_AVAILABLE = 0xC0020042,
    /// An internal error occurred in the RPC.
    RPC_NT_INTERNAL_ERROR = 0xC0020043,
    /// The RPC server attempted to divide an integer by zero.
    RPC_NT_ZERO_DIVIDE = 0xC0020044,
    /// An addressing error occurred in the RPC server.
    RPC_NT_ADDRESS_ERROR = 0xC0020045,
    /// A floating point operation at the RPC server caused a divide by zero.
    RPC_NT_FP_DIV_ZERO = 0xC0020046,
    /// A floating point underflow occurred at the RPC server.
    RPC_NT_FP_UNDERFLOW = 0xC0020047,
    /// A floating point overflow occurred at the RPC server.
    RPC_NT_FP_OVERFLOW = 0xC0020048,
    /// An RPC is already in progress for this thread.
    RPC_NT_CALL_IN_PROGRESS = 0xC0020049,
    /// There are no more bindings.
    RPC_NT_NO_MORE_BINDINGS = 0xC002004A,
    /// The group member was not found.
    RPC_NT_GROUP_MEMBER_NOT_FOUND = 0xC002004B,
    /// The endpoint mapper database entry could not be created.
    EPT_NT_CANT_CREATE = 0xC002004C,
    /// The object UUID is the nil UUID.
    RPC_NT_INVALID_OBJECT = 0xC002004D,
    /// No interfaces have been registered.
    RPC_NT_NO_INTERFACES = 0xC002004F,
    /// The RPC was canceled.
    RPC_NT_CALL_CANCELLED = 0xC0020050,
    /// The binding handle does not contain all the required information.
    RPC_NT_BINDING_INCOMPLETE = 0xC0020051,
    /// A communications failure occurred during an RPC.
    RPC_NT_COMM_FAILURE = 0xC0020052,
    /// The requested authentication level is not supported.
    RPC_NT_UNSUPPORTED_AUTHN_LEVEL = 0xC0020053,
    /// No principal name was registered.
    RPC_NT_NO_PRINC_NAME = 0xC0020054,
    /// The error specified is not a valid Windows RPC error code.
    RPC_NT_NOT_RPC_ERROR = 0xC0020055,
    /// A security package-specific error occurred.
    RPC_NT_SEC_PKG_ERROR = 0xC0020057,
    /// The thread was not canceled.
    RPC_NT_NOT_CANCELLED = 0xC0020058,
    /// Invalid asynchronous RPC handle.
    RPC_NT_INVALID_ASYNC_HANDLE = 0xC0020062,
    /// Invalid asynchronous RPC call handle for this operation.
    RPC_NT_INVALID_ASYNC_CALL = 0xC0020063,
    /// Access to the HTTP proxy is denied.
    RPC_NT_PROXY_ACCESS_DENIED = 0xC0020064,
    /// The list of RPC servers available for auto-handle binding has been exhausted.
    RPC_NT_NO_MORE_ENTRIES = 0xC0030001,
    /// The file designated by DCERPCCHARTRANS cannot be opened.
    RPC_NT_SS_CHAR_TRANS_OPEN_FAIL = 0xC0030002,
    /// The file containing the character translation table has fewer than 512 bytes.
    RPC_NT_SS_CHAR_TRANS_SHORT_FILE = 0xC0030003,
    /// A null context handle is passed as an [in] parameter.
    RPC_NT_SS_IN_NULL_CONTEXT = 0xC0030004,
    /// The context handle does not match any known context handles.
    RPC_NT_SS_CONTEXT_MISMATCH = 0xC0030005,
    /// The context handle changed during a call.
    RPC_NT_SS_CONTEXT_DAMAGED = 0xC0030006,
    /// The binding handles passed to an RPC do not match.
    RPC_NT_SS_HANDLES_MISMATCH = 0xC0030007,
    /// The stub is unable to get the call handle.
    RPC_NT_SS_CANNOT_GET_CALL_HANDLE = 0xC0030008,
    /// A null reference pointer was passed to the stub.
    RPC_NT_NULL_REF_POINTER = 0xC0030009,
    /// The enumeration value is out of range.
    RPC_NT_ENUM_VALUE_OUT_OF_RANGE = 0xC003000A,
    /// The byte count is too small.
    RPC_NT_BYTE_COUNT_TOO_SMALL = 0xC003000B,
    /// The stub received bad data.
    RPC_NT_BAD_STUB_DATA = 0xC003000C,
    /// Invalid operation on the encoding/decoding handle.
    RPC_NT_INVALID_ES_ACTION = 0xC0030059,
    /// Incompatible version of the serializing package.
    RPC_NT_WRONG_ES_VERSION = 0xC003005A,
    /// Incompatible version of the RPC stub.
    RPC_NT_WRONG_STUB_VERSION = 0xC003005B,
    /// The RPC pipe object is invalid or corrupt.
    RPC_NT_INVALID_PIPE_OBJECT = 0xC003005C,
    /// An invalid operation was attempted on an RPC pipe object.
    RPC_NT_INVALID_PIPE_OPERATION = 0xC003005D,
    /// Unsupported RPC pipe version.
    RPC_NT_WRONG_PIPE_VERSION = 0xC003005E,
    /// The RPC pipe object has already been closed.
    RPC_NT_PIPE_CLOSED = 0xC003005F,
    /// The RPC call completed before all pipes were processed.
    RPC_NT_PIPE_DISCIPLINE_ERROR = 0xC0030060,
    /// No more data is available from the RPC pipe.
    RPC_NT_PIPE_EMPTY = 0xC0030061,
    /// A device is missing in the system BIOS MPS table. This device will not be used.
    /// Contact your system vendor for a system BIOS update.
    PNP_BAD_MPS_TABLE = 0xC0040035,
    /// A translator failed to translate resources.
    PNP_TRANSLATION_FAILED = 0xC0040036,
    /// An IRQ translator failed to translate resources.
    PNP_IRQ_TRANSLATION_FAILED = 0xC0040037,
    /// Driver %2 returned an invalid ID for a child device (%3).
    PNP_INVALID_ID = 0xC0040038,
    /// Reissue the given operation as a cached I/O operation
    IO_REISSUE_AS_CACHED = 0xC0040039,
    /// Session name %1 is invalid.
    CTX_WINSTATION_NAME_INVALID = 0xC00A0001,
    /// The protocol driver %1 is invalid.
    CTX_INVALID_PD = 0xC00A0002,
    /// The protocol driver %1 was not found in the system path.
    CTX_PD_NOT_FOUND = 0xC00A0003,
    /// A close operation is pending on the terminal connection.
    CTX_CLOSE_PENDING = 0xC00A0006,
    /// No free output buffers are available.
    CTX_NO_OUTBUF = 0xC00A0007,
    /// The MODEM.INF file was not found.
    CTX_MODEM_INF_NOT_FOUND = 0xC00A0008,
    /// The modem (%1) was not found in the MODEM.INF file.
    CTX_INVALID_MODEMNAME = 0xC00A0009,
    /// The modem did not accept the command sent to it.
    /// Verify that the configured modem name matches the attached modem.
    CTX_RESPONSE_ERROR = 0xC00A000A,
    /// The modem did not respond to the command sent to it.
    /// Verify that the modem cable is properly attached and the modem is turned on.
    CTX_MODEM_RESPONSE_TIMEOUT = 0xC00A000B,
    /// Carrier detection has failed or the carrier has been dropped due to disconnection.
    CTX_MODEM_RESPONSE_NO_CARRIER = 0xC00A000C,
    /// A dial tone was not detected within the required time.
    /// Verify that the phone cable is properly attached and functional.
    CTX_MODEM_RESPONSE_NO_DIALTONE = 0xC00A000D,
    /// A busy signal was detected at a remote site on callback.
    CTX_MODEM_RESPONSE_BUSY = 0xC00A000E,
    /// A voice was detected at a remote site on callback.
    CTX_MODEM_RESPONSE_VOICE = 0xC00A000F,
    /// Transport driver error.
    CTX_TD_ERROR = 0xC00A0010,
    /// The client you are using is not licensed to use this system. Your logon request is denied.
    CTX_LICENSE_CLIENT_INVALID = 0xC00A0012,
    /// The system has reached its licensed logon limit. Try again later.
    CTX_LICENSE_NOT_AVAILABLE = 0xC00A0013,
    /// The system license has expired. Your logon request is denied.
    CTX_LICENSE_EXPIRED = 0xC00A0014,
    /// The specified session cannot be found.
    CTX_WINSTATION_NOT_FOUND = 0xC00A0015,
    /// The specified session name is already in use.
    CTX_WINSTATION_NAME_COLLISION = 0xC00A0016,
    /// The requested operation cannot be completed because the terminal connection is currently processing a connect, disconnect, reset, or delete operation.
    CTX_WINSTATION_BUSY = 0xC00A0017,
    /// An attempt has been made to connect to a session whose video mode is not supported by the current client.
    CTX_BAD_VIDEO_MODE = 0xC00A0018,
    /// The application attempted to enable DOS graphics mode. DOS graphics mode is not supported.
    CTX_GRAPHICS_INVALID = 0xC00A0022,
    /// The requested operation can be performed only on the system console.
    /// This is most often the result of a driver or system DLL requiring direct console access.
    CTX_NOT_CONSOLE = 0xC00A0024,
    /// The client failed to respond to the server connect message.
    CTX_CLIENT_QUERY_TIMEOUT = 0xC00A0026,
    /// Disconnecting the console session is not supported.
    CTX_CONSOLE_DISCONNECT = 0xC00A0027,
    /// Reconnecting a disconnected session to the console is not supported.
    CTX_CONSOLE_CONNECT = 0xC00A0028,
    /// The request to control another session remotely was denied.
    CTX_SHADOW_DENIED = 0xC00A002A,
    /// A process has requested access to a session, but has not been granted those access rights.
    CTX_WINSTATION_ACCESS_DENIED = 0xC00A002B,
    /// The terminal connection driver %1 is invalid.
    CTX_INVALID_WD = 0xC00A002E,
    /// The terminal connection driver %1 was not found in the system path.
    CTX_WD_NOT_FOUND = 0xC00A002F,
    /// The requested session cannot be controlled remotely.
    /// You cannot control your own session, a session that is trying to control your session, a session that has no user logged on, or other sessions from the console.
    CTX_SHADOW_INVALID = 0xC00A0030,
    /// The requested session is not configured to allow remote control.
    CTX_SHADOW_DISABLED = 0xC00A0031,
    /// The RDP protocol component %2 detected an error in the protocol stream and has disconnected the client.
    RDP_PROTOCOL_ERROR = 0xC00A0032,
    /// Your request to connect to this terminal server has been rejected.
    /// Your terminal server client license number has not been entered for this copy of the terminal client.
    /// Contact your system administrator for help in entering a valid, unique license number for this terminal server client. Click OK to continue.
    CTX_CLIENT_LICENSE_NOT_SET = 0xC00A0033,
    /// Your request to connect to this terminal server has been rejected.
    /// Your terminal server client license number is currently being used by another user.
    /// Contact your system administrator to obtain a new copy of the terminal server client with a valid, unique license number. Click OK to continue.
    CTX_CLIENT_LICENSE_IN_USE = 0xC00A0034,
    /// The remote control of the console was terminated because the display mode was changed.
    /// Changing the display mode in a remote control session is not supported.
    CTX_SHADOW_ENDED_BY_MODE_CHANGE = 0xC00A0035,
    /// Remote control could not be terminated because the specified session is not currently being remotely controlled.
    CTX_SHADOW_NOT_RUNNING = 0xC00A0036,
    /// Your interactive logon privilege has been disabled. Contact your system administrator.
    CTX_LOGON_DISABLED = 0xC00A0037,
    /// The terminal server security layer detected an error in the protocol stream and has disconnected the client.
    CTX_SECURITY_LAYER_ERROR = 0xC00A0038,
    /// The target session is incompatible with the current session.
    TS_INCOMPATIBLE_SESSIONS = 0xC00A0039,
    /// The resource loader failed to find an MUI file.
    MUI_FILE_NOT_FOUND = 0xC00B0001,
    /// The resource loader failed to load an MUI file because the file failed to pass validation.
    MUI_INVALID_FILE = 0xC00B0002,
    /// The RC manifest is corrupted with garbage data, is an unsupported version, or is missing a required item.
    MUI_INVALID_RC_CONFIG = 0xC00B0003,
    /// The RC manifest has an invalid culture name.
    MUI_INVALID_LOCALE_NAME = 0xC00B0004,
    /// The RC manifest has and invalid ultimate fallback name.
    MUI_INVALID_ULTIMATEFALLBACK_NAME = 0xC00B0005,
    /// The resource loader cache does not have a loaded MUI entry.
    MUI_FILE_NOT_LOADED = 0xC00B0006,
    /// The user stopped resource enumeration.
    RESOURCE_ENUM_USER_STOP = 0xC00B0007,
    /// The cluster node is not valid.
    CLUSTER_INVALID_NODE = 0xC0130001,
    /// The cluster node already exists.
    CLUSTER_NODE_EXISTS = 0xC0130002,
    /// A node is in the process of joining the cluster.
    CLUSTER_JOIN_IN_PROGRESS = 0xC0130003,
    /// The cluster node was not found.
    CLUSTER_NODE_NOT_FOUND = 0xC0130004,
    /// The cluster local node information was not found.
    CLUSTER_LOCAL_NODE_NOT_FOUND = 0xC0130005,
    /// The cluster network already exists.
    CLUSTER_NETWORK_EXISTS = 0xC0130006,
    /// The cluster network was not found.
    CLUSTER_NETWORK_NOT_FOUND = 0xC0130007,
    /// The cluster network interface already exists.
    CLUSTER_NETINTERFACE_EXISTS = 0xC0130008,
    /// The cluster network interface was not found.
    CLUSTER_NETINTERFACE_NOT_FOUND = 0xC0130009,
    /// The cluster request is not valid for this object.
    CLUSTER_INVALID_REQUEST = 0xC013000A,
    /// The cluster network provider is not valid.
    CLUSTER_INVALID_NETWORK_PROVIDER = 0xC013000B,
    /// The cluster node is down.
    CLUSTER_NODE_DOWN = 0xC013000C,
    /// The cluster node is not reachable.
    CLUSTER_NODE_UNREACHABLE = 0xC013000D,
    /// The cluster node is not a member of the cluster.
    CLUSTER_NODE_NOT_MEMBER = 0xC013000E,
    /// A cluster join operation is not in progress.
    CLUSTER_JOIN_NOT_IN_PROGRESS = 0xC013000F,
    /// The cluster network is not valid.
    CLUSTER_INVALID_NETWORK = 0xC0130010,
    /// No network adapters are available.
    CLUSTER_NO_NET_ADAPTERS = 0xC0130011,
    /// The cluster node is up.
    CLUSTER_NODE_UP = 0xC0130012,
    /// The cluster node is paused.
    CLUSTER_NODE_PAUSED = 0xC0130013,
    /// The cluster node is not paused.
    CLUSTER_NODE_NOT_PAUSED = 0xC0130014,
    /// No cluster security context is available.
    CLUSTER_NO_SECURITY_CONTEXT = 0xC0130015,
    /// The cluster network is not configured for internal cluster communication.
    CLUSTER_NETWORK_NOT_INTERNAL = 0xC0130016,
    /// The cluster node has been poisoned.
    CLUSTER_POISONED = 0xC0130017,
    /// An attempt was made to run an invalid AML opcode.
    ACPI_INVALID_OPCODE = 0xC0140001,
    /// The AML interpreter stack has overflowed.
    ACPI_STACK_OVERFLOW = 0xC0140002,
    /// An inconsistent state has occurred.
    ACPI_ASSERT_FAILED = 0xC0140003,
    /// An attempt was made to access an array outside its bounds.
    ACPI_INVALID_INDEX = 0xC0140004,
    /// A required argument was not specified.
    ACPI_INVALID_ARGUMENT = 0xC0140005,
    /// A fatal error has occurred.
    ACPI_FATAL = 0xC0140006,
    /// An invalid SuperName was specified.
    ACPI_INVALID_SUPERNAME = 0xC0140007,
    /// An argument with an incorrect type was specified.
    ACPI_INVALID_ARGTYPE = 0xC0140008,
    /// An object with an incorrect type was specified.
    ACPI_INVALID_OBJTYPE = 0xC0140009,
    /// A target with an incorrect type was specified.
    ACPI_INVALID_TARGETTYPE = 0xC014000A,
    /// An incorrect number of arguments was specified.
    ACPI_INCORRECT_ARGUMENT_COUNT = 0xC014000B,
    /// An address failed to translate.
    ACPI_ADDRESS_NOT_MAPPED = 0xC014000C,
    /// An incorrect event type was specified.
    ACPI_INVALID_EVENTTYPE = 0xC014000D,
    /// A handler for the target already exists.
    ACPI_HANDLER_COLLISION = 0xC014000E,
    /// Invalid data for the target was specified.
    ACPI_INVALID_DATA = 0xC014000F,
    /// An invalid region for the target was specified.
    ACPI_INVALID_REGION = 0xC0140010,
    /// An attempt was made to access a field outside the defined range.
    ACPI_INVALID_ACCESS_SIZE = 0xC0140011,
    /// The global system lock could not be acquired.
    ACPI_ACQUIRE_GLOBAL_LOCK = 0xC0140012,
    /// An attempt was made to reinitialize the ACPI subsystem.
    ACPI_ALREADY_INITIALIZED = 0xC0140013,
    /// The ACPI subsystem has not been initialized.
    ACPI_NOT_INITIALIZED = 0xC0140014,
    /// An incorrect mutex was specified.
    ACPI_INVALID_MUTEX_LEVEL = 0xC0140015,
    /// The mutex is not currently owned.
    ACPI_MUTEX_NOT_OWNED = 0xC0140016,
    /// An attempt was made to access the mutex by a process that was not the owner.
    ACPI_MUTEX_NOT_OWNER = 0xC0140017,
    /// An error occurred during an access to region space.
    ACPI_RS_ACCESS = 0xC0140018,
    /// An attempt was made to use an incorrect table.
    ACPI_INVALID_TABLE = 0xC0140019,
    /// The registration of an ACPI event failed.
    ACPI_REG_HANDLER_FAILED = 0xC0140020,
    /// An ACPI power object failed to transition state.
    ACPI_POWER_REQUEST_FAILED = 0xC0140021,
    /// The requested section is not present in the activation context.
    SXS_SECTION_NOT_FOUND = 0xC0150001,
    /// Windows was unble to process the application binding information.
    /// Refer to the system event log for further information.
    SXS_CANT_GEN_ACTCTX = 0xC0150002,
    /// The application binding data format is invalid.
    SXS_INVALID_ACTCTXDATA_FORMAT = 0xC0150003,
    /// The referenced assembly is not installed on the system.
    SXS_ASSEMBLY_NOT_FOUND = 0xC0150004,
    /// The manifest file does not begin with the required tag and format information.
    SXS_MANIFEST_FORMAT_ERROR = 0xC0150005,
    /// The manifest file contains one or more syntax errors.
    SXS_MANIFEST_PARSE_ERROR = 0xC0150006,
    /// The application attempted to activate a disabled activation context.
    SXS_ACTIVATION_CONTEXT_DISABLED = 0xC0150007,
    /// The requested lookup key was not found in any active activation context.
    SXS_KEY_NOT_FOUND = 0xC0150008,
    /// A component version required by the application conflicts with another component version that is already active.
    SXS_VERSION_CONFLICT = 0xC0150009,
    /// The type requested activation context section does not match the query API used.
    SXS_WRONG_SECTION_TYPE = 0xC015000A,
    /// Lack of system resources has required isolated activation to be disabled for the current thread of execution.
    SXS_THREAD_QUERIES_DISABLED = 0xC015000B,
    /// The referenced assembly could not be found.
    SXS_ASSEMBLY_MISSING = 0xC015000C,
    /// An attempt to set the process default activation context failed because the process default activation context was already set.
    SXS_PROCESS_DEFAULT_ALREADY_SET = 0xC015000E,
    /// The activation context being deactivated is not the most recently activated one.
    SXS_EARLY_DEACTIVATION = 0xC015000F,
    /// The activation context being deactivated is not active for the current thread of execution.
    SXS_INVALID_DEACTIVATION = 0xC0150010,
    /// The activation context being deactivated has already been deactivated.
    SXS_MULTIPLE_DEACTIVATION = 0xC0150011,
    /// The activation context of the system default assembly could not be generated.
    SXS_SYSTEM_DEFAULT_ACTIVATION_CONTEXT_EMPTY = 0xC0150012,
    /// A component used by the isolation facility has requested that the process be terminated.
    SXS_PROCESS_TERMINATION_REQUESTED = 0xC0150013,
    /// The activation context activation stack for the running thread of execution is corrupt.
    SXS_CORRUPT_ACTIVATION_STACK = 0xC0150014,
    /// The application isolation metadata for this process or thread has become corrupt.
    SXS_CORRUPTION = 0xC0150015,
    /// The value of an attribute in an identity is not within the legal range.
    SXS_INVALID_IDENTITY_ATTRIBUTE_VALUE = 0xC0150016,
    /// The name of an attribute in an identity is not within the legal range.
    SXS_INVALID_IDENTITY_ATTRIBUTE_NAME = 0xC0150017,
    /// An identity contains two definitions for the same attribute.
    SXS_IDENTITY_DUPLICATE_ATTRIBUTE = 0xC0150018,
    /// The identity string is malformed.
    /// This might be due to a trailing comma, more than two unnamed attributes, a missing attribute name, or a missing attribute value.
    SXS_IDENTITY_PARSE_ERROR = 0xC0150019,
    /// The component store has become corrupted.
    SXS_COMPONENT_STORE_CORRUPT = 0xC015001A,
    /// A component's file does not match the verification information present in the component manifest.
    SXS_FILE_HASH_MISMATCH = 0xC015001B,
    /// The identities of the manifests are identical, but their contents are different.
    SXS_MANIFEST_IDENTITY_SAME_BUT_CONTENTS_DIFFERENT = 0xC015001C,
    /// The component identities are different.
    SXS_IDENTITIES_DIFFERENT = 0xC015001D,
    /// The assembly is not a deployment.
    SXS_ASSEMBLY_IS_NOT_A_DEPLOYMENT = 0xC015001E,
    /// The file is not a part of the assembly.
    SXS_FILE_NOT_PART_OF_ASSEMBLY = 0xC015001F,
    /// An advanced installer failed during setup or servicing.
    ADVANCED_INSTALLER_FAILED = 0xC0150020,
    /// The character encoding in the XML declaration did not match the encoding used in the document.
    XML_ENCODING_MISMATCH = 0xC0150021,
    /// The size of the manifest exceeds the maximum allowed.
    SXS_MANIFEST_TOO_BIG = 0xC0150022,
    /// The setting is not registered.
    SXS_SETTING_NOT_REGISTERED = 0xC0150023,
    /// One or more required transaction members are not present.
    SXS_TRANSACTION_CLOSURE_INCOMPLETE = 0xC0150024,
    /// The SMI primitive installer failed during setup or servicing.
    SMI_PRIMITIVE_INSTALLER_FAILED = 0xC0150025,
    /// A generic command executable returned a result that indicates failure.
    GENERIC_COMMAND_FAILED = 0xC0150026,
    /// A component is missing file verification information in its manifest.
    SXS_FILE_HASH_MISSING = 0xC0150027,
    /// The function attempted to use a name that is reserved for use by another transaction.
    TRANSACTIONAL_CONFLICT = 0xC0190001,
    /// The transaction handle associated with this operation is invalid.
    INVALID_TRANSACTION = 0xC0190002,
    /// The requested operation was made in the context of a transaction that is no longer active.
    TRANSACTION_NOT_ACTIVE = 0xC0190003,
    /// The transaction manager was unable to be successfully initialized. Transacted operations are not supported.
    TM_INITIALIZATION_FAILED = 0xC0190004,
    /// Transaction support within the specified file system resource manager was not started or was shut down due to an error.
    RM_NOT_ACTIVE = 0xC0190005,
    /// The metadata of the resource manager has been corrupted. The resource manager will not function.
    RM_METADATA_CORRUPT = 0xC0190006,
    /// The resource manager attempted to prepare a transaction that it has not successfully joined.
    TRANSACTION_NOT_JOINED = 0xC0190007,
    /// The specified
```
