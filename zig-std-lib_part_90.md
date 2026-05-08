```
form name is invalid.
    INVALID_FORM_NAME = 1902,
    /// The specified form size is invalid.
    INVALID_FORM_SIZE = 1903,
    /// The specified printer handle is already being waited on.
    ALREADY_WAITING = 1904,
    /// The specified printer has been deleted.
    PRINTER_DELETED = 1905,
    /// The state of the printer is invalid.
    INVALID_PRINTER_STATE = 1906,
    /// The user's password must be changed before signing in.
    PASSWORD_MUST_CHANGE = 1907,
    /// Could not find the domain controller for this domain.
    DOMAIN_CONTROLLER_NOT_FOUND = 1908,
    /// The referenced account is currently locked out and may not be logged on to.
    ACCOUNT_LOCKED_OUT = 1909,
    /// The object exporter specified was not found.
    OR_INVALID_OXID = 1910,
    /// The object specified was not found.
    OR_INVALID_OID = 1911,
    /// The object resolver set specified was not found.
    OR_INVALID_SET = 1912,
    /// Some data remains to be sent in the request buffer.
    RPC_S_SEND_INCOMPLETE = 1913,
    /// Invalid asynchronous remote procedure call handle.
    RPC_S_INVALID_ASYNC_HANDLE = 1914,
    /// Invalid asynchronous RPC call handle for this operation.
    RPC_S_INVALID_ASYNC_CALL = 1915,
    /// The RPC pipe object has already been closed.
    RPC_X_PIPE_CLOSED = 1916,
    /// The RPC call completed before all pipes were processed.
    RPC_X_PIPE_DISCIPLINE_ERROR = 1917,
    /// No more data is available from the RPC pipe.
    RPC_X_PIPE_EMPTY = 1918,
    /// No site name is available for this machine.
    NO_SITENAME = 1919,
    /// The file cannot be accessed by the system.
    CANT_ACCESS_FILE = 1920,
    /// The name of the file cannot be resolved by the system.
    CANT_RESOLVE_FILENAME = 1921,
    /// The entry is not of the expected type.
    RPC_S_ENTRY_TYPE_MISMATCH = 1922,
    /// Not all object UUIDs could be exported to the specified entry.
    RPC_S_NOT_ALL_OBJS_EXPORTED = 1923,
    /// Interface could not be exported to the specified entry.
    RPC_S_INTERFACE_NOT_EXPORTED = 1924,
    /// The specified profile entry could not be added.
    RPC_S_PROFILE_NOT_ADDED = 1925,
    /// The specified profile element could not be added.
    RPC_S_PRF_ELT_NOT_ADDED = 1926,
    /// The specified profile element could not be removed.
    RPC_S_PRF_ELT_NOT_REMOVED = 1927,
    /// The group element could not be added.
    RPC_S_GRP_ELT_NOT_ADDED = 1928,
    /// The group element could not be removed.
    RPC_S_GRP_ELT_NOT_REMOVED = 1929,
    /// The printer driver is not compatible with a policy enabled on your computer that blocks NT 4.0 drivers.
    KM_DRIVER_BLOCKED = 1930,
    /// The context has expired and can no longer be used.
    CONTEXT_EXPIRED = 1931,
    /// The current user's delegated trust creation quota has been exceeded.
    PER_USER_TRUST_QUOTA_EXCEEDED = 1932,
    /// The total delegated trust creation quota has been exceeded.
    ALL_USER_TRUST_QUOTA_EXCEEDED = 1933,
    /// The current user's delegated trust deletion quota has been exceeded.
    USER_DELETE_TRUST_QUOTA_EXCEEDED = 1934,
    /// The computer you are signing into is protected by an authentication firewall.
    /// The specified account is not allowed to authenticate to the computer.
    AUTHENTICATION_FIREWALL_FAILED = 1935,
    /// Remote connections to the Print Spooler are blocked by a policy set on your machine.
    REMOTE_PRINT_CONNECTIONS_BLOCKED = 1936,
    /// Authentication failed because NTLM authentication has been disabled.
    NTLM_BLOCKED = 1937,
    /// Logon Failure: EAS policy requires that the user change their password before this operation can be performed.
    PASSWORD_CHANGE_REQUIRED = 1938,
    /// The pixel format is invalid.
    INVALID_PIXEL_FORMAT = 2000,
    /// The specified driver is invalid.
    BAD_DRIVER = 2001,
    /// The window style or class attribute is invalid for this operation.
    INVALID_WINDOW_STYLE = 2002,
    /// The requested metafile operation is not supported.
    METAFILE_NOT_SUPPORTED = 2003,
    /// The requested transformation operation is not supported.
    TRANSFORM_NOT_SUPPORTED = 2004,
    /// The requested clipping operation is not supported.
    CLIPPING_NOT_SUPPORTED = 2005,
    /// The specified color management module is invalid.
    INVALID_CMM = 2010,
    /// The specified color profile is invalid.
    INVALID_PROFILE = 2011,
    /// The specified tag was not found.
    TAG_NOT_FOUND = 2012,
    /// A required tag is not present.
    TAG_NOT_PRESENT = 2013,
    /// The specified tag is already present.
    DUPLICATE_TAG = 2014,
    /// The specified color profile is not associated with the specified device.
    PROFILE_NOT_ASSOCIATED_WITH_DEVICE = 2015,
    /// The specified color profile was not found.
    PROFILE_NOT_FOUND = 2016,
    /// The specified color space is invalid.
    INVALID_COLORSPACE = 2017,
    /// Image Color Management is not enabled.
    ICM_NOT_ENABLED = 2018,
    /// There was an error while deleting the color transform.
    DELETING_ICM_XFORM = 2019,
    /// The specified color transform is invalid.
    INVALID_TRANSFORM = 2020,
    /// The specified transform does not match the bitmap's color space.
    COLORSPACE_MISMATCH = 2021,
    /// The specified named color index is not present in the profile.
    INVALID_COLORINDEX = 2022,
    /// The specified profile is intended for a device of a different type than the specified device.
    PROFILE_DOES_NOT_MATCH_DEVICE = 2023,
    /// The network connection was made successfully, but the user had to be prompted for a password other than the one originally specified.
    CONNECTED_OTHER_PASSWORD = 2108,
    /// The network connection was made successfully using default credentials.
    CONNECTED_OTHER_PASSWORD_DEFAULT = 2109,
    /// The specified username is invalid.
    BAD_USERNAME = 2202,
    /// This network connection does not exist.
    NOT_CONNECTED = 2250,
    /// This network connection has files open or requests pending.
    OPEN_FILES = 2401,
    /// Active connections still exist.
    ACTIVE_CONNECTIONS = 2402,
    /// The device is in use by an active process and cannot be disconnected.
    DEVICE_IN_USE = 2404,
    /// The specified print monitor is unknown.
    UNKNOWN_PRINT_MONITOR = 3000,
    /// The specified printer driver is currently in use.
    PRINTER_DRIVER_IN_USE = 3001,
    /// The spool file was not found.
    SPOOL_FILE_NOT_FOUND = 3002,
    /// A StartDocPrinter call was not issued.
    SPL_NO_STARTDOC = 3003,
    /// An AddJob call was not issued.
    SPL_NO_ADDJOB = 3004,
    /// The specified print processor has already been installed.
    PRINT_PROCESSOR_ALREADY_INSTALLED = 3005,
    /// The specified print monitor has already been installed.
    PRINT_MONITOR_ALREADY_INSTALLED = 3006,
    /// The specified print monitor does not have the required functions.
    INVALID_PRINT_MONITOR = 3007,
    /// The specified print monitor is currently in use.
    PRINT_MONITOR_IN_USE = 3008,
    /// The requested operation is not allowed when there are jobs queued to the printer.
    PRINTER_HAS_JOBS_QUEUED = 3009,
    /// The requested operation is successful.
    /// Changes will not be effective until the system is rebooted.
    SUCCESS_REBOOT_REQUIRED = 3010,
    /// The requested operation is successful.
    /// Changes will not be effective until the service is restarted.
    SUCCESS_RESTART_REQUIRED = 3011,
    /// No printers were found.
    PRINTER_NOT_FOUND = 3012,
    /// The printer driver is known to be unreliable.
    PRINTER_DRIVER_WARNED = 3013,
    /// The printer driver is known to harm the system.
    PRINTER_DRIVER_BLOCKED = 3014,
    /// The specified printer driver package is currently in use.
    PRINTER_DRIVER_PACKAGE_IN_USE = 3015,
    /// Unable to find a core driver package that is required by the printer driver package.
    CORE_DRIVER_PACKAGE_NOT_FOUND = 3016,
    /// The requested operation failed.
    /// A system reboot is required to roll back changes made.
    FAIL_REBOOT_REQUIRED = 3017,
    /// The requested operation failed.
    /// A system reboot has been initiated to roll back changes made.
    FAIL_REBOOT_INITIATED = 3018,
    /// The specified printer driver was not found on the system and needs to be downloaded.
    PRINTER_DRIVER_DOWNLOAD_NEEDED = 3019,
    /// The requested print job has failed to print.
    /// A print system update requires the job to be resubmitted.
    PRINT_JOB_RESTART_REQUIRED = 3020,
    /// The printer driver does not contain a valid manifest, or contains too many manifests.
    INVALID_PRINTER_DRIVER_MANIFEST = 3021,
    /// The specified printer cannot be shared.
    PRINTER_NOT_SHAREABLE = 3022,
    /// The operation was paused.
    REQUEST_PAUSED = 3050,
    /// Reissue the given operation as a cached IO operation.
    IO_REISSUE_AS_CACHED = 3950,

    /// DNS server unable to interpret format.
    DNS_ERROR_RCODE_FORMAT_ERROR = 9001,
    /// DNS server failure.
    DNS_ERROR_RCODE_SERVER_FAILURE = 9002,
    /// DNS name does not exist.
    DNS_ERROR_RCODE_NAME_ERROR = 9003,
    /// DNS request not supported by name server.
    DNS_ERROR_RCODE_NOT_IMPLEMENTED = 9004,
    /// DNS operation refused.
    DNS_ERROR_RCODE_REFUSED = 9005,
    /// DNS name that ought not exist, does exist.
    DNS_ERROR_RCODE_YXDOMAIN = 9006,
    /// DNS RR set that ought not exist, does exist.
    DNS_ERROR_RCODE_YXRRSET = 9007,
    /// DNS RR set that ought to exist, does not exist.
    DNS_ERROR_RCODE_NXRRSET = 9008,
    /// DNS server not authoritative for zone.
    DNS_ERROR_RCODE_NOTAUTH = 9009,
    /// DNS name in update or prereq is not in zone.
    DNS_ERROR_RCODE_NOTZONE = 9010,
    /// DNS signature failed to verify.
    DNS_ERROR_RCODE_BADSIG = 9016,
    /// DNS bad key.
    DNS_ERROR_RCODE_BADKEY = 9017,
    /// DNS signature validity expired.
    DNS_ERROR_RCODE_BADTIME = 9018,
    /// DNSSEC errors
    DNS_ERROR_DNSSEC_BASE = 9100,
    /// Only the DNS server acting as the key master for the zone may perform this operation.
    DNS_ERROR_KEYMASTER_REQUIRED = 9101,
    /// This operation is not allowed on a zone that is signed or has signing keys.
    DNS_ERROR_NOT_ALLOWED_ON_SIGNED_ZONE = 9102,
    /// NSEC3 is not compatible with the RSA-SHA-1 algorithm. Choose a different algorithm or use NSEC.
    DNS_ERROR_NSEC3_INCOMPATIBLE_WITH_RSA_SHA1 = 9103,
    /// The zone does not have enough signing keys. There must be at least one key signing key (KSK) and at least one zone signing key (ZSK).
    DNS_ERROR_NOT_ENOUGH_SIGNING_KEY_DESCRIPTORS = 9104,
    /// The specified algorithm is not supported.
    DNS_ERROR_UNSUPPORTED_ALGORITHM = 9105,
    /// The specified key size is not supported.
    DNS_ERROR_INVALID_KEY_SIZE = 9106,
    /// One or more of the signing keys for a zone are not accessible to the DNS server. Zone signing will not be operational until this error is resolved.
    DNS_ERROR_SIGNING_KEY_NOT_ACCESSIBLE = 9107,
    /// The specified key storage provider does not support DPAPI++ data protection. Zone signing will not be operational until this error is resolved.
    DNS_ERROR_KSP_DOES_NOT_SUPPORT_PROTECTION = 9108,
    /// An unexpected DPAPI++ error was encountered. Zone signing will not be operational until this error is resolved.
    DNS_ERROR_UNEXPECTED_DATA_PROTECTION_ERROR = 9109,
    /// An unexpected crypto error was encountered. Zone signing may not be operational until this error is resolved.
    DNS_ERROR_UNEXPECTED_CNG_ERROR = 9110,
    /// The DNS server encountered a signing key with an unknown version. Zone signing will not be operational until this error is resolved.
    DNS_ERROR_UNKNOWN_SIGNING_PARAMETER_VERSION = 9111,
    /// The specified key service provider cannot be opened by the DNS server.
    DNS_ERROR_KSP_NOT_ACCESSIBLE = 9112,
    /// The DNS server cannot accept any more signing keys with the specified algorithm and KSK flag value for this zone.
    DNS_ERROR_TOO_MANY_SKDS = 9113,
    /// The specified rollover period is invalid.
    DNS_ERROR_INVALID_ROLLOVER_PERIOD = 9114,
    /// The specified initial rollover offset is invalid.
    DNS_ERROR_INVALID_INITIAL_ROLLOVER_OFFSET = 9115,
    /// The specified signing key is already in process of rolling over keys.
    DNS_ERROR_ROLLOVER_IN_PROGRESS = 9116,
    /// The specified signing key does not have a standby key to revoke.
    DNS_ERROR_STANDBY_KEY_NOT_PRESENT = 9117,
    /// This operation is not allowed on a zone signing key (ZSK).
    DNS_ERROR_NOT_ALLOWED_ON_ZSK = 9118,
    /// This operation is not allowed on an active signing key.
    DNS_ERROR_NOT_ALLOWED_ON_ACTIVE_SKD = 9119,
    /// The specified signing key is already queued for rollover.
    DNS_ERROR_ROLLOVER_ALREADY_QUEUED = 9120,
    /// This operation is not allowed on an unsigned zone.
    DNS_ERROR_NOT_ALLOWED_ON_UNSIGNED_ZONE = 9121,
    /// This operation could not be completed because the DNS server listed as the current key master for this zone is down or misconfigured. Resolve the problem on the current key master for this zone or use another DNS server to seize the key master role.
    DNS_ERROR_BAD_KEYMASTER = 9122,
    /// The specified signature validity period is invalid.
    DNS_ERROR_INVALID_SIGNATURE_VALIDITY_PERIOD = 9123,
    /// The specified NSEC3 iteration count is higher than allowed by the minimum key length used in the zone.
    DNS_ERROR_INVALID_NSEC3_ITERATION_COUNT = 9124,
    /// This operation could not be completed because the DNS server has been configured with DNSSEC features disabled. Enable DNSSEC on the DNS server.
    DNS_ERROR_DNSSEC_IS_DISABLED = 9125,
    /// This operation could not be completed because the XML stream received is empty or syntactically invalid.
    DNS_ERROR_INVALID_XML = 9126,
    /// This operation completed, but no trust anchors were added because all of the trust anchors received were either invalid, unsupported, expired, or would not become valid in less than 30 days.
    DNS_ERROR_NO_VALID_TRUST_ANCHORS = 9127,
    /// The specified signing key is not waiting for parental DS update.
    DNS_ERROR_ROLLOVER_NOT_POKEABLE = 9128,
    /// Hash collision detected during NSEC3 signing. Specify a different user-provided salt, or use a randomly generated salt, and attempt to sign the zone again.
    DNS_ERROR_NSEC3_NAME_COLLISION = 9129,
    /// NSEC is not compatible with the NSEC3-RSA-SHA-1 algorithm. Choose a different algorithm or use NSEC3.
    DNS_ERROR_NSEC_INCOMPATIBLE_WITH_NSEC3_RSA_SHA1 = 9130,
    /// Packet format
    DNS_ERROR_PACKET_FMT_BASE = 9500,
    /// No records found for given DNS query.
    DNS_INFO_NO_RECORDS = 9501,
    /// Bad DNS packet.
    DNS_ERROR_BAD_PACKET = 9502,
    /// No DNS packet.
    DNS_ERROR_NO_PACKET = 9503,
    /// DNS error, check rcode.
    DNS_ERROR_RCODE = 9504,
    /// Unsecured DNS packet.
    DNS_ERROR_UNSECURE_PACKET = 9505,
    /// DNS query request is pending.
    DNS_REQUEST_PENDING = 9506,
    /// Invalid DNS type.
    DNS_ERROR_INVALID_TYPE = 9551,
    /// Invalid IP address.
    DNS_ERROR_INVALID_IP_ADDRESS = 9552,
    /// Invalid property.
    DNS_ERROR_INVALID_PROPERTY = 9553,
    /// Try DNS operation again later.
    DNS_ERROR_TRY_AGAIN_LATER = 9554,
    /// Record for given name and type is not unique.
    DNS_ERROR_NOT_UNIQUE = 9555,
    /// DNS name does not comply with RFC specifications.
    DNS_ERROR_NON_RFC_NAME = 9556,
    /// DNS name is a fully-qualified DNS name.
    DNS_STATUS_FQDN = 9557,
    /// DNS name is dotted (multi-label).
    DNS_STATUS_DOTTED_NAME = 9558,
    /// DNS name is a single-part name.
    DNS_STATUS_SINGLE_PART_NAME = 9559,
    /// DNS name contains an invalid character.
    DNS_ERROR_INVALID_NAME_CHAR = 9560,
    /// DNS name is entirely numeric.
    DNS_ERROR_NUMERIC_NAME = 9561,
    /// The operation requested is not permitted on a DNS root server.
    DNS_ERROR_NOT_ALLOWED_ON_ROOT_SERVER = 9562,
    /// The record could not be created because this part of the DNS namespace has been delegated to another server.
    DNS_ERROR_NOT_ALLOWED_UNDER_DELEGATION = 9563,
    /// The DNS server could not find a set of root hints.
    DNS_ERROR_CANNOT_FIND_ROOT_HINTS = 9564,
    /// The DNS server found root hints but they were not consistent across all adapters.
    DNS_ERROR_INCONSISTENT_ROOT_HINTS = 9565,
    /// The specified value is too small for this parameter.
    DNS_ERROR_DWORD_VALUE_TOO_SMALL = 9566,
    /// The specified value is too large for this parameter.
    DNS_ERROR_DWORD_VALUE_TOO_LARGE = 9567,
    /// This operation is not allowed while the DNS server is loading zones in the background. Please try again later.
    DNS_ERROR_BACKGROUND_LOADING = 9568,
    /// The operation requested is not permitted on against a DNS server running on a read-only DC.
    DNS_ERROR_NOT_ALLOWED_ON_RODC = 9569,
    /// No data is allowed to exist underneath a DNAME record.
    DNS_ERROR_NOT_ALLOWED_UNDER_DNAME = 9570,
    /// This operation requires credentials delegation.
    DNS_ERROR_DELEGATION_REQUIRED = 9571,
    /// Name resolution policy table has been corrupted. DNS resolution will fail until it is fixed. Contact your network administrator.
    DNS_ERROR_INVALID_POLICY_TABLE = 9572,
    /// Not allowed to remove all addresses.
    DNS_ERROR_ADDRESS_REQUIRED = 9573,
    /// Zone errors
    DNS_ERROR_ZONE_BASE = 9600,
    /// DNS zone does not exist.
    DNS_ERROR_ZONE_DOES_NOT_EXIST = 9601,
    /// DNS zone information not available.
    DNS_ERROR_NO_ZONE_INFO = 9602,
    /// Invalid operation for DNS zone.
    DNS_ERROR_INVALID_ZONE_OPERATION = 9603,
    /// Invalid DNS zone configuration.
    DNS_ERROR_ZONE_CONFIGURATION_ERROR = 9604,
    /// DNS zone has no start of authority (SOA) record.
    DNS_ERROR_ZONE_HAS_NO_SOA_RECORD = 9605,
    /// DNS zone has no Name Server (NS) record.
    DNS_ERROR_ZONE_HAS_NO_NS_RECORDS = 9606,
    /// DNS zone is locked.
    DNS_ERROR_ZONE_LOCKED = 9607,
    /// DNS zone creation failed.
    DNS_ERROR_ZONE_CREATION_FAILED = 9608,
    /// DNS zone already exists.
    DNS_ERROR_ZONE_ALREADY_EXISTS = 9609,
    /// DNS automatic zone already exists.
    DNS_ERROR_AUTOZONE_ALREADY_EXISTS = 9610,
    /// Invalid DNS zone type.
    DNS_ERROR_INVALID_ZONE_TYPE = 9611,
    /// Secondary DNS zone requires master IP address.
    DNS_ERROR_SECONDARY_REQUIRES_MASTER_IP = 9612,
    /// DNS zone not secondary.
    DNS_ERROR_ZONE_NOT_SECONDARY = 9613,
    /// Need secondary IP address.
    DNS_ERROR_NEED_SECONDARY_ADDRESSES = 9614,
    /// WINS initialization failed.
    DNS_ERROR_WINS_INIT_FAILED = 9615,
    /// Need WINS servers.
    DNS_ERROR_NEED_WINS_SERVERS = 9616,
    /// NBTSTAT initialization call failed.
    DNS_ERROR_NBSTAT_INIT_FAILED = 9617,
    /// Invalid delete of start of authority (SOA)
    DNS_ERROR_SOA_DELETE_INVALID = 9618,
    /// A conditional forwarding zone already exists for that name.
    DNS_ERROR_FORWARDER_ALREADY_EXISTS = 9619,
    /// This zone must be configured with one or more master DNS server IP addresses.
    DNS_ERROR_ZONE_REQUIRES_MASTER_IP = 9620,
    /// The operation cannot be performed because this zone is shut down.
    DNS_ERROR_ZONE_IS_SHUTDOWN = 9621,
    /// This operation cannot be performed because the zone is currently being signed. Please try again later.
    DNS_ERROR_ZONE_LOCKED_FOR_SIGNING = 9622,
    /// Datafile errors
    DNS_ERROR_DATAFILE_BASE = 9650,
    /// DNS                                   0x000025b3
    /// Primary DNS zone requires datafile.
    DNS_ERROR_PRIMARY_REQUIRES_DATAFILE = 9651,
    /// DNS                                   0x000025b4
    /// Invalid datafile name for DNS zone.
    DNS_ERROR_INVALID_DATAFILE_NAME = 9652,
    /// DNS                                   0x000025b5
    /// Failed to open datafile for DNS zone.
    DNS_ERROR_DATAFILE_OPEN_FAILURE = 9653,
    /// DNS                                   0x000025b6
    /// Failed to write datafile for DNS zone.
    DNS_ERROR_FILE_WRITEBACK_FAILED = 9654,
    /// DNS                                   0x000025b7
    /// Failure while reading datafile for DNS zone.
    DNS_ERROR_DATAFILE_PARSING = 9655,
    /// Database errors
    DNS_ERROR_DATABASE_BASE = 9700,
    /// DNS record does not exist.
    DNS_ERROR_RECORD_DOES_NOT_EXIST = 9701,
    /// DNS record format error.
    DNS_ERROR_RECORD_FORMAT = 9702,
    /// Node creation failure in DNS.
    DNS_ERROR_NODE_CREATION_FAILED = 9703,
    /// Unknown DNS record type.
    DNS_ERROR_UNKNOWN_RECORD_TYPE = 9704,
    /// DNS record timed out.
    DNS_ERROR_RECORD_TIMED_OUT = 9705,
    /// Name not in DNS zone.
    DNS_ERROR_NAME_NOT_IN_ZONE = 9706,
    /// CNAME loop detected.
    DNS_ERROR_CNAME_LOOP = 9707,
    /// Node is a CNAME DNS record.
    DNS_ERROR_NODE_IS_CNAME = 9708,
    /// A CNAME record already exists for given name.
    DNS_ERROR_CNAME_COLLISION = 9709,
    /// Record only at DNS zone root.
    DNS_ERROR_RECORD_ONLY_AT_ZONE_ROOT = 9710,
    /// DNS record already exists.
    DNS_ERROR_RECORD_ALREADY_EXISTS = 9711,
    /// Secondary DNS zone data error.
    DNS_ERROR_SECONDARY_DATA = 9712,
    /// Could not create DNS cache data.
    DNS_ERROR_NO_CREATE_CACHE_DATA = 9713,
    /// DNS name does not exist.
    DNS_ERROR_NAME_DOES_NOT_EXIST = 9714,
    /// Could not create pointer (PTR) record.
    DNS_WARNING_PTR_CREATE_FAILED = 9715,
    /// DNS domain was undeleted.
    DNS_WARNING_DOMAIN_UNDELETED = 9716,
    /// The directory service is unavailable.
    DNS_ERROR_DS_UNAVAILABLE = 9717,
    /// DNS zone already exists in the directory service.
    DNS_ERROR_DS_ZONE_ALREADY_EXISTS = 9718,
    /// DNS server not creating or reading the boot file for the directory service integrated DNS zone.
    DNS_ERROR_NO_BOOTFILE_IF_DS_ZONE = 9719,
    /// Node is a DNAME DNS record.
    DNS_ERROR_NODE_IS_DNAME = 9720,
    /// A DNAME record already exists for given name.
    DNS_ERROR_DNAME_COLLISION = 9721,
    /// An alias loop has been detected with either CNAME or DNAME records.
    DNS_ERROR_ALIAS_LOOP = 9722,
    /// Operation errors
    DNS_ERROR_OPERATION_BASE = 9750,
    /// DNS AXFR (zone transfer) complete.
    DNS_INFO_AXFR_COMPLETE = 9751,
    /// DNS zone transfer failed.
    DNS_ERROR_AXFR = 9752,
    /// Added local WINS server.
    DNS_INFO_ADDED_LOCAL_WINS = 9753,
    /// Secure update
    DNS_ERROR_SECURE_BASE = 9800,
    /// Secure update call needs to continue update request.
    DNS_STATUS_CONTINUE_NEEDED = 9801,
    /// Setup errors
    DNS_ERROR_SETUP_BASE = 9850,
    /// TCP/IP network protocol not installed.
    DNS_ERROR_NO_TCPIP = 9851,
    /// No DNS servers configured for local system.
    DNS_ERROR_NO_DNS_SERVERS = 9852,
    /// Directory partition (DP) errors
    DNS_ERROR_DP_BASE = 9900,
    /// The specified directory partition does not exist.
    DNS_ERROR_DP_DOES_NOT_EXIST = 9901,
    /// The specified directory partition already exists.
    DNS_ERROR_DP_ALREADY_EXISTS = 9902,
    /// This DNS server is not enlisted in the specified directory partition.
    DNS_ERROR_DP_NOT_ENLISTED = 9903,
    /// This DNS server is already enlisted in the specified directory partition.
    DNS_ERROR_DP_ALREADY_ENLISTED = 9904,
    /// The directory partition is not available at this time. Please wait a few minutes and try again.
    DNS_ERROR_DP_NOT_AVAILABLE = 9905,
    /// The operation failed because the domain naming master FSMO role could not be reached. The domain controller holding the domain naming master FSMO role is down or unable to service the request or is not running Windows Server 2003 or later.
    DNS_ERROR_DP_FSMO_ERROR = 9906,
    /// DNS RRL errors from 9911 to 9920
    /// The RRL is not enabled.
    DNS_ERROR_RRL_NOT_ENABLED = 9911,
    /// The window size parameter is invalid. It should be greater than or equal to 1.
    DNS_ERROR_RRL_INVALID_WINDOW_SIZE = 9912,
    /// The IPv4 prefix length parameter is invalid. It should be less than or equal to 32.
    DNS_ERROR_RRL_INVALID_IPV4_PREFIX = 9913,
    /// The IPv6 prefix length parameter is invalid. It should be less than or equal to 128.
    DNS_ERROR_RRL_INVALID_IPV6_PREFIX = 9914,
    /// The TC Rate parameter is invalid. It should be less than 10.
    DNS_ERROR_RRL_INVALID_TC_RATE = 9915,
    /// The Leak Rate parameter is invalid. It should be either 0, or between 2 and 10.
    DNS_ERROR_RRL_INVALID_LEAK_RATE = 9916,
    /// The Leak Rate or TC Rate parameter is invalid. Leak Rate should be greater than TC Rate.
    DNS_ERROR_RRL_LEAK_RATE_LESSTHAN_TC_RATE = 9917,
    /// DNS Virtualization errors from 9921 to 9950
    /// The virtualization instance already exists.
    DNS_ERROR_VIRTUALIZATION_INSTANCE_ALREADY_EXISTS = 9921,
    /// The virtualization instance does not exist.
    DNS_ERROR_VIRTUALIZATION_INSTANCE_DOES_NOT_EXIST = 9922,
    /// The virtualization tree is locked.
    DNS_ERROR_VIRTUALIZATION_TREE_LOCKED = 9923,
    /// Invalid virtualization instance name.
    DNS_ERROR_INVAILD_VIRTUALIZATION_INSTANCE_NAME = 9924,
    /// The default virtualization instance cannot be added, removed or modified.
    DNS_ERROR_DEFAULT_VIRTUALIZATION_INSTANCE = 9925,
    /// DNS ZoneScope errors from 9951 to 9970
    /// The scope already exists for the zone.
    DNS_ERROR_ZONESCOPE_ALREADY_EXISTS = 9951,
    /// The scope does not exist for the zone.
    DNS_ERROR_ZONESCOPE_DOES_NOT_EXIST = 9952,
    /// The scope is the same as the default zone scope.
    DNS_ERROR_DEFAULT_ZONESCOPE = 9953,
    /// The scope name contains invalid characters.
    DNS_ERROR_INVALID_ZONESCOPE_NAME = 9954,
    /// Operation not allowed when the zone has scopes.
    DNS_ERROR_NOT_ALLOWED_WITH_ZONESCOPES = 9955,
    /// Failed to load zone scope.
    DNS_ERROR_LOAD_ZONESCOPE_FAILED = 9956,
    /// Failed to write data file for DNS zone scope. Please verify the file exists and is writable.
    DNS_ERROR_ZONESCOPE_FILE_WRITEBACK_FAILED = 9957,
    /// The scope name contains invalid characters.
    DNS_ERROR_INVALID_SCOPE_NAME = 9958,
    /// The scope does not exist.
    DNS_ERROR_SCOPE_DOES_NOT_EXIST = 9959,
    /// The scope is the same as the default scope.
    DNS_ERROR_DEFAULT_SCOPE = 9960,
    /// The operation is invalid on the scope.
    DNS_ERROR_INVALID_SCOPE_OPERATION = 9961,
    /// The scope is locked.
    DNS_ERROR_SCOPE_LOCKED = 9962,
    /// The scope already exists.
    DNS_ERROR_SCOPE_ALREADY_EXISTS = 9963,
    /// DNS Policy errors from 9971 to 9999
    /// A policy with the same name already exists on this level (server level or zone level) on the DNS server.
    DNS_ERROR_POLICY_ALREADY_EXISTS = 9971,
    /// No policy with this name exists on this level (server level or zone level) on the DNS server.
    DNS_ERROR_POLICY_DOES_NOT_EXIST = 9972,
    /// The criteria provided in the policy are invalid.
    DNS_ERROR_POLICY_INVALID_CRITERIA = 9973,
    /// At least one of the settings of this policy is invalid.
    DNS_ERROR_POLICY_INVALID_SETTINGS = 9974,
    /// The client subnet cannot be deleted while it is being accessed by a policy.
    DNS_ERROR_CLIENT_SUBNET_IS_ACCESSED = 9975,
    /// The client subnet does not exist on the DNS server.
    DNS_ERROR_CLIENT_SUBNET_DOES_NOT_EXIST = 9976,
    /// A client subnet with this name already exists on the DNS server.
    DNS_ERROR_CLIENT_SUBNET_ALREADY_EXISTS = 9977,
    /// The IP subnet specified does not exist in the client subnet.
    DNS_ERROR_SUBNET_DOES_NOT_EXIST = 9978,
    /// The IP subnet that is being added, already exists in the client subnet.
    DNS_ERROR_SUBNET_ALREADY_EXISTS = 9979,
    /// The policy is locked.
    DNS_ERROR_POLICY_LOCKED = 9980,
    /// The weight of the scope in the policy is invalid.
    DNS_ERROR_POLICY_INVALID_WEIGHT = 9981,
    /// The DNS policy name is invalid.
    DNS_ERROR_POLICY_INVALID_NAME = 9982,
    /// The policy is missing criteria.
    DNS_ERROR_POLICY_MISSING_CRITERIA = 9983,
    /// The name of the the client subnet record is invalid.
    DNS_ERROR_INVALID_CLIENT_SUBNET_NAME = 9984,
    /// Invalid policy processing order.
    DNS_ERROR_POLICY_PROCESSING_ORDER_INVALID = 9985,
    /// The scope information has not been provided for a policy that requires it.
    DNS_ERROR_POLICY_SCOPE_MISSING = 9986,
    /// The scope information has been provided for a policy that does not require it.
    DNS_ERROR_POLICY_SCOPE_NOT_ALLOWED = 9987,
    /// The server scope cannot be deleted because it is referenced by a DNS Policy.
    DNS_ERROR_SERVERSCOPE_IS_REFERENCED = 9988,
    /// The zone scope cannot be deleted because it is referenced by a DNS Policy.
    DNS_ERROR_ZONESCOPE_IS_REFERENCED = 9989,
    /// The criterion client subnet provided in the policy is invalid.
    DNS_ERROR_POLICY_INVALID_CRITERIA_CLIENT_SUBNET = 9990,
    /// The criterion transport protocol provided in the policy is invalid.
    DNS_ERROR_POLICY_INVALID_CRITERIA_TRANSPORT_PROTOCOL = 9991,
    /// The criterion network protocol provided in the policy is invalid.
    DNS_ERROR_POLICY_INVALID_CRITERIA_NETWORK_PROTOCOL = 9992,
    /// The criterion interface provided in the policy is invalid.
    DNS_ERROR_POLICY_INVALID_CRITERIA_INTERFACE = 9993,
    /// The criterion FQDN provided in the policy is invalid.
    DNS_ERROR_POLICY_INVALID_CRITERIA_FQDN = 9994,
    /// The criterion query type provided in the policy is invalid.
    DNS_ERROR_POLICY_INVALID_CRITERIA_QUERY_TYPE = 9995,
    /// The criterion time of day provided in the policy is invalid.
    DNS_ERROR_POLICY_INVALID_CRITERIA_TIME_OF_DAY = 9996,

    /// An error occurred while performing an operation on a cryptographic message.
    CRYPT_E_MSG_ERROR = 0x80091001,
    /// Unknown cryptographic algorithm.
    CRYPT_E_UNKNOWN_ALGO = 0x80091002,
    /// The object identifier is poorly formatted.
    CRYPT_E_OID_FORMAT = 0x80091003,
    /// Invalid cryptographic message type.
    CRYPT_E_INVALID_MSG_TYPE = 0x80091004,
    /// Unexpected cryptographic message encoding.
    CRYPT_E_UNEXPECTED_ENCODING = 0x80091005,
    /// The cryptographic message does not contain an expected authenticated attribute.
    CRYPT_E_AUTH_ATTR_MISSING = 0x80091006,
    /// The hash value is not correct.
    CRYPT_E_HASH_VALUE = 0x80091007,
    /// The index value is not valid.
    CRYPT_E_INVALID_INDEX = 0x80091008,
    /// The content of the cryptographic message has already been decrypted.
    CRYPT_E_ALREADY_DECRYPTED = 0x80091009,
    /// The content of the cryptographic message has not been decrypted yet.
    CRYPT_E_NOT_DECRYPTED = 0x8009100A,
    /// The enveloped-data message does not contain the specified recipient.
    CRYPT_E_RECIPIENT_NOT_FOUND = 0x8009100B,
    /// Invalid control type.
    CRYPT_E_CONTROL_TYPE = 0x8009100C,
    /// Invalid issuer and/or serial number.
    CRYPT_E_ISSUER_SERIALNUMBER = 0x8009100D,
    /// Cannot find the original signer.
    CRYPT_E_SIGNER_NOT_FOUND = 0x8009100E,
    /// The cryptographic message does not contain all of the requested attributes.
    CRYPT_E_ATTRIBUTES_MISSING = 0x8009100F,
    /// The streamed cryptographic message is not ready to return data.
    CRYPT_E_STREAM_MSG_NOT_READY = 0x80091010,
    /// The streamed cryptographic message requires more data to complete the decode operation.
    CRYPT_E_STREAM_INSUFFICIENT_DATA = 0x80091011,
    /// The protected data needs to be re-protected.
    CRYPT_I_NEW_PROTECTION_REQUIRED = 0x00091012,
    /// The length specified for the output data was insufficient.
    CRYPT_E_BAD_LEN = 0x80092001,
    /// An error occurred during encode or decode operation.
    CRYPT_E_BAD_ENCODE = 0x80092002,
    /// An error occurred while reading or writing to a file.
    CRYPT_E_FILE_ERROR = 0x80092003,
    /// Cannot find object or property.
    CRYPT_E_NOT_FOUND = 0x80092004,
    /// The object or property already exists.
    CRYPT_E_EXISTS = 0x80092005,
    /// No provider was specified for the store or object.
    CRYPT_E_NO_PROVIDER = 0x80092006,
    /// The specified certificate is self signed.
    CRYPT_E_SELF_SIGNED = 0x80092007,
    /// The previous certificate or CRL context was deleted.
    CRYPT_E_DELETED_PREV = 0x80092008,
    /// Cannot find the requested object.
    CRYPT_E_NO_MATCH = 0x80092009,
    /// The certificate does not have a property that references a private key.
    CRYPT_E_UNEXPECTED_MSG_TYPE = 0x8009200A,
    /// Cannot find the certificate and private key for decryption.
    CRYPT_E_NO_KEY_PROPERTY = 0x8009200B,
    /// Cannot find the certificate and private key to use for decryption.
    CRYPT_E_NO_DECRYPT_CERT = 0x8009200C,
    /// Not a cryptographic message or the cryptographic message is not formatted correctly.
    CRYPT_E_BAD_MSG = 0x8009200D,
    /// The signed cryptographic message does not have a signer for the specified signer index.
    CRYPT_E_NO_SIGNER = 0x8009200E,
    /// Final closure is pending until additional frees or closes.
    CRYPT_E_PENDING_CLOSE = 0x8009200F,
    /// The certificate is revoked.
    CRYPT_E_REVOKED = 0x80092010,
    /// No Dll or exported function was found to verify revocation.
    CRYPT_E_NO_REVOCATION_DLL = 0x80092011,
    /// The revocation function was unable to check revocation for the certificate.
    CRYPT_E_NO_REVOCATION_CHECK = 0x80092012,
    /// The revocation function was unable to check revocation because the revocation server was offline.
    CRYPT_E_REVOCATION_OFFLINE = 0x80092013,
    /// The certificate is not in the revocation server's database.
    CRYPT_E_NOT_IN_REVOCATION_DATABASE = 0x80092014,
    /// The string contains a non-numeric character.
    CRYPT_E_INVALID_NUMERIC_STRING = 0x80092020,
    /// The string contains a non-printable character.
    CRYPT_E_INVALID_PRINTABLE_STRING = 0x80092021,
    /// The string contains a character not in the 7 bit ASCII character set.
    CRYPT_E_INVALID_IA5_STRING = 0x80092022,
    /// The string contains an invalid X500 name attribute key, oid, value or delimiter.
    CRYPT_E_INVALID_X500_STRING = 0x80092023,
    /// The dwValueType for the CERT_NAME_VALUE is not one of the character strings. Most likely it is either a CERT_RDN_ENCODED_BLOB or CERT_RDN_OCTET_STRING.
    CRYPT_E_NOT_CHAR_STRING = 0x80092024,
    /// The Put operation cannot continue. The file needs to be resized. However, there is already a signature present. A complete signing operation must be done.
    CRYPT_E_FILERESIZED = 0x80092025,
    /// The cryptographic operation failed due to a local security option setting.
    CRYPT_E_SECURITY_SETTINGS = 0x80092026,
    /// No DLL or exported function was found to verify subject usage.
    CRYPT_E_NO_VERIFY_USAGE_DLL = 0x80092027,
    /// The called function was unable to do a usage check on the subject.
    CRYPT_E_NO_VERIFY_USAGE_CHECK = 0x80092028,
    /// Since the server was offline, the called function was unable to complete the usage check.
    CRYPT_E_VERIFY_USAGE_OFFLINE = 0x80092029,
    /// The subject was not found in a Certificate Trust List (CT,.
    CRYPT_E_NOT_IN_CTL = 0x8009202A,
    /// None of the signers of the cryptographic message or certificate trust list is trusted.
    CRYPT_E_NO_TRUSTED_SIGNER = 0x8009202B,
    /// The public key's algorithm parameters are missing.
    CRYPT_E_MISSING_PUBKEY_PARA = 0x8009202C,
    /// An object could not be located using the object locator infrastructure with the given name.
    CRYPT_E_OBJECT_LOCATOR_OBJECT_NOT_FOUND = 0x8009202D,
    /// MessageText:
    /// OSS Certificate encode/decode error code base
    /// See asn1code.h for a definition of the OSS runtime errors. The OSS error values are offset by CRYPT_E_OSS_ERROR.
    CRYPT_E_OSS_ERROR = 0x80093000,

    /// No signature was present in the subject.
    TRUST_E_NOSIGNATURE = 0x800B0100,
    /// A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file.
    CERT_E_EXPIRED = 0x800B0101,
    /// The validity periods of the certification chain do not nest correctly.
    CERT_E_VALIDITYPERIODNESTING = 0x800B0102,
    /// A certificate that can only be used as an end-entity is being used as a CA or vice versa.
    CERT_E_ROLE = 0x800B0103,
    /// A path length constraint in the certification chain has been violated.
    CERT_E_PATHLENCONST = 0x800B0104,
    /// A certificate contains an unknown extension that is marked 'critical'.
    CERT_E_CRITICAL = 0x800B0105,
    /// A certificate being used for a purpose other than the ones specified by its CA.
    CERT_E_PURPOSE = 0x800B0106,
    /// A parent of a given certificate in fact did not issue that child certificate.
    CERT_E_ISSUERCHAINING = 0x800B0107,
    /// A certificate is missing or has an empty value for an important field, such as a subject or issuer name.
    CERT_E_MALFORMED = 0x800B0108,
    /// A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.
    CERT_E_UNTRUSTEDROOT = 0x800B0109,
    /// A certificate chain could not be built to a trusted root authority.
    CERT_E_CHAINING = 0x800B010A,
    /// Generic trust failure.
    TRUST_E_FAIL = 0x800B010B,
    /// A certificate was explicitly revoked by its issuer.
    CERT_E_REVOKED = 0x800B010C,
    /// The certification path terminates with the test root which is not trusted with the current policy settings.
    CERT_E_UNTRUSTEDTESTROOT = 0x800B010D,
    /// The revocation process could not continue - the certificate(s) could not be checked.
    CERT_E_REVOCATION_FAILURE = 0x800B010E,
    /// The certificate's CN name does not match the passed value.
    CERT_E_CN_NO_MATCH = 0x800B010F,
    /// The certificate is not valid for the requested usage.
    CERT_E_WRONG_USAGE = 0x800B0110,
    /// The certificate was explicitly marked as untrusted by the user.
    TRUST_E_EXPLICIT_DISTRUST = 0x800B0111,
    /// A certification chain processed correctly, but one of the CA certificates is not trusted by the policy provider.
    CERT_E_UNTRUSTEDCA = 0x800B0112,
    /// The certificate has invalid policy.
    CERT_E_INVALID_POLICY = 0x800B0113,
    /// The certificate has an invalid name. The name is not included in the permitted list or is explicitly excluded.
    CERT_E_INVALID_NAME = 0x800B0114,

    _,
};



---
File: /std/os/windows/ws2_32.zig
---

const std = @import("../../std.zig");
const assert = std.debug.assert;
const windows = std.os.windows;

const USHORT = windows.USHORT;
const LONG = windows.LONG;

pub const GROUP = u32;
pub const ADDRESS_FAMILY = u16;

// Microsoft use the signed c_int for this, but it should never be negative
pub const socklen_t = u32;

pub const TCP = struct {
    pub const NODELAY = 1;
    pub const EXPEDITED_1122 = 2;
    pub const OFFLOAD_NO_PREFERENCE = 0;
    pub const OFFLOAD_NOT_PREFERRED = 1;
    pub const OFFLOAD_PREFERRED = 2;
    pub const KEEPALIVE = 3;
    pub const MAXSEG = 4;
    pub const MAXRT = 5;
    pub const STDURG = 6;
    pub const NOURG = 7;
    pub const ATMARK = 8;
    pub const NOSYNRETRIES = 9;
    pub const TIMESTAMPS = 10;
    pub const OFFLOAD_PREFERENCE = 11;
    pub const CONGESTION_ALGORITHM = 12;
    pub const DELAY_FIN_ACK = 13;
    pub const MAXRTMS = 14;
    pub const FASTOPEN = 15;
    pub const KEEPCNT = 16;
    pub const KEEPINTVL = 17;
    pub const FAIL_CONNECT_ON_ICMP_ERROR = 18;
    pub const ICMP_ERROR_INFO = 19;
    pub const BSDURGENT = 28672;
};

pub const AF = struct {
    pub const UNSPEC = 0;
    pub const UNIX = 1;
    pub const INET = 2;
    pub const IMPLINK = 3;
    pub const PUP = 4;
    pub const CHAOS = 5;
    pub const NS = 6;
    pub const IPX = 6;
    pub const ISO = 7;
    pub const ECMA = 8;
    pub const DATAKIT = 9;
    pub const CCITT = 10;
    pub const SNA = 11;
    pub const DECnet = 12;
    pub const DLI = 13;
    pub const LAT = 14;
    pub const HYLINK = 15;
    pub const APPLETALK = 16;
    pub const NETBIOS = 17;
    pub const VOICEVIEW = 18;
    pub const FIREFOX = 19;
    pub const UNKNOWN1 = 20;
    pub const BAN = 21;
    pub const ATM = 22;
    pub const INET6 = 23;
    pub const CLUSTER = 24;
    pub const @"12844" = 25;
    pub const IRDA = 26;
    pub const NETDES = 28;
    pub const MAX = 29;
    pub const TCNPROCESS = 29;
    pub const TCNMESSAGE = 30;
    pub const ICLFXBM = 31;
    pub const BTH = 32;
    pub const LINK = 33;
    pub const HYPERV = 34;
};

pub const SOCK = struct {
    pub const STREAM = 1;
    pub const DGRAM = 2;
    pub const RAW = 3;
    pub const RDM = 4;
    pub const SEQPACKET = 5;
};

pub const SOL = struct {
    pub const IRLMP = 255;
    pub const SOCKET = 65535;
};

pub const SO = struct {
    pub const DEBUG = 1;
    pub const ACCEPTCONN = 2;
    pub const REUSEADDR = 4;
    pub const KEEPALIVE = 8;
    pub const DONTROUTE = 16;
    pub const BROADCAST = 32;
    pub const USELOOPBACK = 64;
    pub const LINGER = 128;
    pub const OOBINLINE = 256;
    pub const SNDBUF = 4097;
    pub const RCVBUF = 4098;
    pub const SNDLOWAT = 4099;
    pub const RCVLOWAT = 4100;
    pub const SNDTIMEO = 4101;
    pub const RCVTIMEO = 4102;
    pub const ERROR = 4103;
    pub const TYPE = 4104;
    pub const BSP_STATE = 4105;
    pub const GROUP_ID = 8193;
    pub const GROUP_PRIORITY = 8194;
    pub const MAX_MSG_SIZE = 8195;
    pub const CONDITIONAL_ACCEPT = 12290;
    pub const PAUSE_ACCEPT = 12291;
    pub const COMPARTMENT_ID = 12292;
    pub const RANDOMIZE_PORT = 12293;
    pub const PORT_SCALABILITY = 12294;
    pub const REUSE_UNICASTPORT = 12295;
    pub const REUSE_MULTICASTPORT = 12296;
    pub const ORIGINAL_DST = 12303;
    pub const PROTOCOL_INFOA = 8196;
    pub const PROTOCOL_INFOW = 8197;
    pub const CONNDATA = 28672;
    pub const CONNOPT = 28673;
    pub const DISCDATA = 28674;
    pub const DISCOPT = 28675;
    pub const CONNDATALEN = 28676;
    pub const CONNOPTLEN = 28677;
    pub const DISCDATALEN = 28678;
    pub const DISCOPTLEN = 28679;
    pub const OPENTYPE = 28680;
    pub const SYNCHRONOUS_ALERT = 16;
    pub const SYNCHRONOUS_NONALERT = 32;
    pub const MAXDG = 28681;
    pub const MAXPATHDG = 28682;
    pub const UPDATE_ACCEPT_CONTEXT = 28683;
    pub const CONNECT_TIME = 28684;
    pub const UPDATE_CONNECT_CONTEXT = 28688;

    pub const UNIX_PATH = 0x98000000;
};

pub const MSG = struct {
    pub const OOB = 0x1;
    pub const PEEK = 0x2;
    pub const DONTROUTE = 0x4;
    pub const WAITALL = 0x8;
    pub const INTERRUPT = 0x10;
    pub const PUSH_IMMEDIATE = 0x20;

    pub const TRUNC = 0x0100;
    pub const CTRUNC = 0x0200;
    pub const BCAST = 0x0400;
    pub const MCAST = 0x0800;

    pub const PARTIAL = 0x8000;

    pub const MAXIOVLEN = 16;
};

pub const IPPROTO = struct {
    pub const IP = 0;
    pub const ICMP = 1;
    pub const IGMP = 2;
    pub const GGP = 3;
    pub const TCP = 6;
    pub const PUP = 12;
    pub const UDP = 17;
    pub const IDP = 22;
    pub const ND = 77;
    pub const RM = 113;
    pub const RAW = 255;
    pub const MAX = 256;
};

pub const FLOWSPEC = extern struct {
    TokenRate: u32,
    TokenBucketSize: u32,
    PeakBandwidth: u32,
    Latency: u32,
    DelayVariation: u32,
    ServiceType: u32,
    MaxSduSize: u32,
    MinimumPolicedSize: u32,
};

pub const sockproto = extern struct {
    sp_family: u16,
    sp_protocol: u16,
};

pub const linger = extern struct {
    onoff: u16,
    linger: u16,
};

pub const sockaddr = extern struct {
    family: ADDRESS_FAMILY,
    data: [14]u8,

    pub const SS_MAXSIZE = 128;
    pub const storage = extern struct {
        family: ADDRESS_FAMILY align(8),
        padding: [SS_MAXSIZE - @sizeOf(ADDRESS_FAMILY)]u8 = undefined,

        comptime {
            assert(@sizeOf(storage) == SS_MAXSIZE);
            assert(@alignOf(storage) == 8);
        }
    };

    /// IPv4 socket address
    pub const in = extern struct {
        family: ADDRESS_FAMILY = AF.INET,
        port: USHORT,
        addr: u32,
        zero: [8]u8 = [8]u8{ 0, 0, 0, 0, 0, 0, 0, 0 },
    };

    /// IPv6 socket address
    pub const in6 = extern struct {
        family: ADDRESS_FAMILY = AF.INET6,
        port: USHORT,
        flowinfo: u32,
        addr: [16]u8,
        scope_id: u32,
    };

    /// UNIX domain socket address
    pub const un = extern struct {
        family: ADDRESS_FAMILY = AF.UNIX,
        path: [108]u8,
    };
};

pub const hostent = extern struct {
    h_name: [*]u8,
    h_aliases: **i8,
    h_addrtype: i16,
    h_length: i16,
    h_addr_list: **i8,
};

pub const timeval = extern struct {
    sec: LONG,
    usec: LONG,
};



---
File: /std/os/emscripten.zig
---

const std = @import("std");
const builtin = @import("builtin");
const wasi = std.os.wasi;
const linux = std.os.linux;
const iovec = std.posix.iovec;
const iovec_const = std.posix.iovec_const;
const c = std.c;

// TODO: go through this file and delete all the bits that are identical to linux because they can
// be merged in the std.c namespace.

pub const FILE = c.FILE;

pub const PF = linux.PF;
pub const AF = linux.AF;
pub const CLOCK = linux.CLOCK;

pub const CPU_SETSIZE = 128;
pub const cpu_set_t = [CPU_SETSIZE / @sizeOf(usize)]usize;
pub const cpu_count_t = std.meta.Int(.unsigned, std.math.log2(CPU_SETSIZE * 8));

pub fn CPU_COUNT(set: cpu_set_t) cpu_count_t {
    var sum: cpu_count_t = 0;
    for (set) |x| {
        sum += @popCount(x);
    }
    return sum;
}

pub const E = enum(u16) {
    SUCCESS = @intFromEnum(wasi.errno_t.SUCCESS),
    @"2BIG" = @intFromEnum(wasi.errno_t.@"2BIG"),
    ACCES = @intFromEnum(wasi.errno_t.ACCES),
    ADDRINUSE = @intFromEnum(wasi.errno_t.ADDRINUSE),
    ADDRNOTAVAIL = @intFromEnum(wasi.errno_t.ADDRNOTAVAIL),
    AFNOSUPPORT = @intFromEnum(wasi.errno_t.AFNOSUPPORT),
    /// This is also the error code used for `WOULDBLOCK`.
    AGAIN = @intFromEnum(wasi.errno_t.AGAIN),
    ALREADY = @intFromEnum(wasi.errno_t.ALREADY),
    BADF = @intFromEnum(wasi.errno_t.BADF),
    BADMSG = @intFromEnum(wasi.errno_t.BADMSG),
    BUSY = @intFromEnum(wasi.errno_t.BUSY),
    CANCELED = @intFromEnum(wasi.errno_t.CANCELED),
    CHILD = @intFromEnum(wasi.errno_t.CHILD),
    CONNABORTED = @intFromEnum(wasi.errno_t.CONNABORTED),
    CONNREFUSED = @intFromEnum(wasi.errno_t.CONNREFUSED),
    CONNRESET = @intFromEnum(wasi.errno_t.CONNRESET),
    DEADLK = @intFromEnum(wasi.errno_t.DEADLK),
    DESTADDRREQ = @intFromEnum(wasi.errno_t.DESTADDRREQ),
    DOM = @intFromEnum(wasi.errno_t.DOM),
    DQUOT = @intFromEnum(wasi.errno_t.DQUOT),
    EXIST = @intFromEnum(wasi.errno_t.EXIST),
    FAULT = @intFromEnum(wasi.errno_t.FAULT),
    FBIG = @intFromEnum(wasi.errno_t.FBIG),
    HOSTUNREACH = @intFromEnum(wasi.errno_t.HOSTUNREACH),
    IDRM = @intFromEnum(wasi.errno_t.IDRM),
    ILSEQ = @intFromEnum(wasi.errno_t.ILSEQ),
    INPROGRESS = @intFromEnum(wasi.errno_t.INPROGRESS),
    INTR = @intFromEnum(wasi.errno_t.INTR),
    INVAL = @intFromEnum(wasi.errno_t.INVAL),
    IO = @intFromEnum(wasi.errno_t.IO),
    ISCONN = @intFromEnum(wasi.errno_t.ISCONN),
    ISDIR = @intFromEnum(wasi.errno_t.ISDIR),
    LOOP = @intFromEnum(wasi.errno_t.LOOP),
    MFILE = @intFromEnum(wasi.errno_t.MFILE),
    MLINK = @intFromEnum(wasi.errno_t.MLINK),
    MSGSIZE = @intFromEnum(wasi.errno_t.MSGSIZE),
    MULTIHOP = @intFromEnum(wasi.errno_t.MULTIHOP),
    NAMETOOLONG = @intFromEnum(wasi.errno_t.NAMETOOLONG),
    NETDOWN = @intFromEnum(wasi.errno_t.NETDOWN),
    NETRESET = @intFromEnum(wasi.errno_t.NETRESET),
    NETUNREACH = @intFromEnum(wasi.errno_t.NETUNREACH),
    NFILE = @intFromEnum(wasi.errno_t.NFILE),
    NOBUFS = @intFromEnum(wasi.errno_t.NOBUFS),
    NODEV = @intFromEnum(wasi.errno_t.NODEV),
    NOENT = @intFromEnum(wasi.errno_t.NOENT),
    NOEXEC = @intFromEnum(wasi.errno_t.NOEXEC),
    NOLCK = @intFromEnum(wasi.errno_t.NOLCK),
    NOLINK = @intFromEnum(wasi.errno_t.NOLINK),
    NOMEM = @intFromEnum(wasi.errno_t.NOMEM),
    NOMSG = @intFromEnum(wasi.errno_t.NOMSG),
    NOPROTOOPT = @intFromEnum(wasi.errno_t.NOPROTOOPT),
    NOSPC = @intFromEnum(wasi.errno_t.NOSPC),
    NOSYS = @intFromEnum(wasi.errno_t.NOSYS),
    NOTCONN = @intFromEnum(wasi.errno_t.NOTCONN),
    NOTDIR = @intFromEnum(wasi.errno_t.NOTDIR),
    NOTEMPTY = @intFromEnum(wasi.errno_t.NOTEMPTY),
    NOTRECOVERABLE = @intFromEnum(wasi.errno_t.NOTRECOVERABLE),
    NOTSOCK = @intFromEnum(wasi.errno_t.NOTSOCK),
    /// This is also the code used for `NOTSUP`.
    OPNOTSUPP = @intFromEnum(wasi.errno_t.OPNOTSUPP),
    NOTTY = @intFromEnum(wasi.errno_t.NOTTY),
    NXIO = @intFromEnum(wasi.errno_t.NXIO),
    OVERFLOW = @intFromEnum(wasi.errno_t.OVERFLOW),
    OWNERDEAD = @intFromEnum(wasi.errno_t.OWNERDEAD),
    PERM = @intFromEnum(wasi.errno_t.PERM),
    PIPE = @intFromEnum(wasi.errno_t.PIPE),
    PROTO = @intFromEnum(wasi.errno_t.PROTO),
    PROTONOSUPPORT = @intFromEnum(wasi.errno_t.PROTONOSUPPORT),
    PROTOTYPE = @intFromEnum(wasi.errno_t.PROTOTYPE),
    RANGE = @intFromEnum(wasi.errno_t.RANGE),
    ROFS = @intFromEnum(wasi.errno_t.ROFS),
    SPIPE = @intFromEnum(wasi.errno_t.SPIPE),
    SRCH = @intFromEnum(wasi.errno_t.SRCH),
    STALE = @intFromEnum(wasi.errno_t.STALE),
    TIMEDOUT = @intFromEnum(wasi.errno_t.TIMEDOUT),
    TXTBSY = @intFromEnum(wasi.errno_t.TXTBSY),
    XDEV = @intFromEnum(wasi.errno_t.XDEV),
    NOTCAPABLE = @intFromEnum(wasi.errno_t.NOTCAPABLE),

    ENOSTR = 100,
    EBFONT = 101,
    EBADSLT = 102,
    EBADRQC = 103,
    ENOANO = 104,
    ENOTBLK = 105,
    ECHRNG = 106,
    EL3HLT = 107,
    EL3RST = 108,
    ELNRNG = 109,
    EUNATCH = 110,
    ENOCSI = 111,
    EL2HLT = 112,
    EBADE = 113,
    EBADR = 114,
    EXFULL = 115,
    ENODATA = 116,
    ETIME = 117,
    ENOSR = 118,
    ENONET = 119,
    ENOPKG = 120,
    EREMOTE = 121,
    EADV = 122,
    ESRMNT = 123,
    ECOMM = 124,
    EDOTDOT = 125,
    ENOTUNIQ = 126,
    EBADFD = 127,
    EREMCHG = 128,
    ELIBACC = 129,
    ELIBBAD = 130,
    ELIBSCN = 131,
    ELIBMAX = 132,
    ELIBEXEC = 133,
    ERESTART = 134,
    ESTRPIPE = 135,
    EUSERS = 136,
    ESOCKTNOSUPPORT = 137,
    EOPNOTSUPP = 138,
    EPFNOSUPPORT = 139,
    ESHUTDOWN = 140,
    ETOOMANYREFS = 141,
    EHOSTDOWN = 142,
    EUCLEAN = 143,
    ENOTNAM = 144,
    ENAVAIL = 145,
    EISNAM = 146,
    EREMOTEIO = 147,
    ENOMEDIUM = 148,
    EMEDIUMTYPE = 149,
    ENOKEY = 150,
    EKEYEXPIRED = 151,
    EKEYREVOKED = 152,
    EKEYREJECTED = 153,
    ERFKILL = 154,
    EHWPOISON = 155,
    EL2NSYNC = 156,
    _,
};

pub const F = struct {
    pub const DUPFD = 0;
    pub const GETFD = 1;
    pub const SETFD = 2;
    pub const GETFL = 3;
    pub const SETFL = 4;
    pub const SETOWN = 8;
    pub const GETOWN = 9;
    pub const SETSIG = 10;
    pub const GETSIG = 11;
    pub const GETLK = 12;
    pub const SETLK = 13;
    pub const SETLKW = 14;
    pub const SETOWN_EX = 15;
    pub const GETOWN_EX = 16;
    pub const GETOWNER_UIDS = 17;

    pub const RDLCK = 0;
    pub const WRLCK = 1;
    pub const UNLCK = 2;
};

pub const FD_CLOEXEC = 1;

pub const F_OK = 0;
pub const X_OK = 1;
pub const W_OK = 2;
pub const R_OK = 4;

pub const W = struct {
    pub const NOHANG = 1;
    pub const UNTRACED = 2;
    pub const STOPPED = 2;
    pub const EXITED = 4;
    pub const CONTINUED = 8;
    pub const NOWAIT = 0x1000000;

    pub fn EXITSTATUS(s: u32) u8 {
        return @as(u8, @intCast((s & 0xff00) >> 8));
    }
    pub fn TERMSIG(s: u32) SIG {
        return @enumFromInt(s & 0x7f);
    }
    pub fn STOPSIG(s: u32) u32 {
        return @enumFromInt(EXITSTATUS(s));
    }
    pub fn IFEXITED(s: u32) bool {
        return (s & 0x7f) == 0;
    }
    pub fn IFSTOPPED(s: u32) bool {
        return @as(u16, @truncate(((s & 0xffff) *% 0x10001) >> 8)) > 0x7f00;
    }
    pub fn IFSIGNALED(s: u32) bool {
        return (s & 0xffff) -% 1 < 0xff;
    }
};

pub const Flock = extern struct {
    type: i16,
    whence: i16,
    start: off_t,
    len: off_t,
    pid: pid_t,
};

pub const IFNAMESIZE = 16;

pub const NAME_MAX = 255;
pub const PATH_MAX = 4096;
pub const IOV_MAX = 1024;

pub const IPPORT_RESERVED = 1024;

pub const IPPROTO = linux.IPPROTO;

pub const LOCK = struct {
    pub const SH = 1;
    pub const EX = 2;
    pub const NB = 4;
    pub const UN = 8;
};

pub const MADV = struct {
    pub const NORMAL = 0;
    pub const RANDOM = 1;
    pub const SEQUENTIAL = 2;
    pub const WILLNEED = 3;
    pub const DONTNEED = 4;
    pub const FREE = 8;
    pub const REMOVE = 9;
    pub const DONTFORK = 10;
    pub const DOFORK = 11;
    pub const MERGEABLE = 12;
    pub const UNMERGEABLE = 13;
    pub const HUGEPAGE = 14;
    pub const NOHUGEPAGE = 15;
    pub const DONTDUMP = 16;
    pub const DODUMP = 17;
    pub const WIPEONFORK = 18;
    pub const KEEPONFORK = 19;
    pub const COLD = 20;
    pub const PAGEOUT = 21;
    pub const HWPOISON = 100;
    pub const SOFT_OFFLINE = 101;
};

pub const MSF = struct {
    pub const ASYNC = 1;
    pub const INVALIDATE = 2;
    pub const SYNC = 4;
};

pub const MSG = struct {
    pub const OOB = 0x0001;
    pub const PEEK = 0x0002;
    pub const DONTROUTE = 0x0004;
    pub const CTRUNC = 0x0008;
    pub const PROXY = 0x0010;
    pub const TRUNC = 0x0020;
    pub const DONTWAIT = 0x0040;
    pub const EOR = 0x0080;
    pub const WAITALL = 0x0100;
    pub const FIN = 0x0200;
    pub const SYN = 0x0400;
    pub const CONFIRM = 0x0800;
    pub const RST = 0x1000;
    pub const ERRQUEUE = 0x2000;
    pub const NOSIGNAL = 0x4000;
    pub const MORE = 0x8000;
    pub const WAITFORONE = 0x10000;
    pub const BATCH = 0x40000;
    pub const ZEROCOPY = 0x4000000;
    pub const FASTOPEN = 0x20000000;
    pub const CMSG_CLOEXEC = 0x40000000;
};

pub const POLL = struct {
    pub const IN = 0x001;
    pub const PRI = 0x002;
    pub const OUT = 0x004;
    pub const ERR = 0x008;
    pub const HUP = 0x010;
    pub const NVAL = 0x020;
    pub const RDNORM = 0x040;
    pub const RDBAND = 0x080;
};

pub const PROT = packed struct(u32) {
    READ: bool = false,
    WRITE: bool = false,
    EXEC: bool = false,
    _: u21 = 0,
    GROWSDOWN: bool = false,
    GROWSUP: bool = false,
    __: u6 = 0,
};

pub const rlim_t = u64;

pub const RLIM = struct {
    pub const INFINITY = ~@as(rlim_t, 0);

    pub const SAVED_MAX = INFINITY;
    pub const SAVED_CUR = INFINITY;
};

pub const rlimit = c.rlimit;

pub const rlimit_resource = enum(c_int) {
    CPU,
    FSIZE,
    DATA,
    STACK,
    CORE,
    RSS,
    NPROC,
    NOFILE,
    MEMLOCK,
    AS,
    LOCKS,
    SIGPENDING,
    MSGQUEUE,
    NICE,
    RTPRIO,
    RTTIME,
    _,
};

pub const rusage = extern struct {
    utime: timeval,
    stime: timeval,
    maxrss: isize,
    ixrss: isize,
    idrss: isize,
    isrss: isize,
    minflt: isize,
    majflt: isize,
    nswap: isize,
    inblock: isize,
    oublock: isize,
    msgsnd: isize,
    msgrcv: isize,
    nsignals: isize,
    nvcsw: isize,
    nivcsw: isize,
    __reserved: [16]isize = [1]isize{0} ** 16,

    pub const SELF = 0;
    pub const CHILDREN = -1;
    pub const THREAD = 1;
};

pub const timeval = extern struct {
    sec: i64,
    usec: i32,
};

pub const S = struct {
    pub const IFMT = 0o170000;

    pub const IFDIR = 0o040000;
    pub const IFCHR = 0o020000;
    pub const IFBLK = 0o060000;
    pub const IFREG = 0o100000;
    pub const IFIFO = 0o010000;
    pub const IFLNK = 0o120000;
    pub const IFSOCK = 0o140000;

    pub const ISUID = 0o4000;
    pub const ISGID = 0o2000;
    pub const ISVTX = 0o1000;
    pub const IRUSR = 0o400;
    pub const IWUSR = 0o200;
    pub const IXUSR = 0o100;
    pub const IRWXU = 0o700;
    pub const IRGRP = 0o040;
    pub const IWGRP = 0o020;
    pub const IXGRP = 0o010;
    pub const IRWXG = 0o070;
    pub const IROTH = 0o004;
    pub const IWOTH = 0o002;
    pub const IXOTH = 0o001;
    pub const IRWXO = 0o007;

    pub fn ISREG(m: mode_t) bool {
        return m & IFMT == IFREG;
    }

    pub fn ISDIR(m: mode_t) bool {
        return m & IFMT == IFDIR;
    }

    pub fn ISCHR(m: mode_t) bool {
        return m & IFMT == IFCHR;
    }

    pub fn ISBLK(m: mode_t) bool {
        return m & IFMT == IFBLK;
    }

    pub fn ISFIFO(m: mode_t) bool {
        return m & IFMT == IFIFO;
    }

    pub fn ISLNK(m: mode_t) bool {
        return m & IFMT == IFLNK;
    }

    pub fn ISSOCK(m: mode_t) bool {
        return m & IFMT == IFSOCK;
    }
};

pub const SA = struct {
    pub const NOCLDSTOP = 1;
    pub const NOCLDWAIT = 2;
    pub const SIGINFO = 4;
    pub const RESTART = 0x10000000;
    pub const RESETHAND = 0x80000000;
    pub const ONSTACK = 0x08000000;
    pub const NODEFER = 0x40000000;
    pub const RESTORER = 0x04000000;
};

pub const SEEK = struct {
    pub const SET = 0;
    pub const CUR = 1;
    pub const END = 2;
};

pub const SHUT = struct {
    pub const RD = 0;
    pub const WR = 1;
    pub const RDWR = 2;
};

pub const SIG = linux.SIG;

pub const Sigaction = extern struct {
    pub const handler_fn = *align(1) const fn (i32) callconv(.c) void;
    pub const sigaction_fn = *const fn (i32, *const siginfo_t, ?*anyopaque) callconv(.c) void;

    handler: extern union {
        handler: ?handler_fn,
        sigaction: ?sigaction_fn,
    },
    mask: sigset_t,
    flags: c_uint,
    restorer: ?*const fn () callconv(.c) void = null,
};

pub const sigset_t = [1024 / 32]u32;
pub fn sigemptyset() sigset_t {
    return [_]u32{0} ** @typeInfo(sigset_t).array.len;
}
pub const siginfo_t = extern struct {
    signo: i32,
    errno: i32,
    code: i32,
    fields: siginfo_fields_union,
};
const siginfo_fields_union = extern union {
    pad: [128 - 2 * @sizeOf(c_int) - @sizeOf(c_long)]u8,
    common: extern struct {
        first: extern union {
            piduid: extern struct {
                pid: pid_t,
                uid: uid_t,
            },
            timer: extern struct {
                timerid: i32,
                overrun: i32,
            },
        },
        second: extern union {
            value: sigval,
            sigchld: extern struct {
                status: i32,
                utime: clock_t,
                stime: clock_t,
            },
        },
    },
    sigfault: extern struct {
        addr: *allowzero anyopaque,
        addr_lsb: i16,
        first: extern union {
            addr_bnd: extern struct {
                lower: *anyopaque,
                upper: *anyopaque,
            },
            pkey: u32,
        },
    },
    sigpoll: extern struct {
        band: isize,
        fd: i32,
    },
    sigsys: extern struct {
        call_addr: *anyopaque,
        syscall: i32,
        native_arch: u32,
    },
};
pub const sigval = extern union {
    int: i32,
    ptr: *anyopaque,
};

pub const SIOCGIFINDEX = 0x8933;

pub const SO = struct {
    pub const DEBUG = 1;
    pub const REUSEADDR = 2;
    pub const TYPE = 3;
    pub const ERROR = 4;
    pub const DONTROUTE = 5;
    pub const BROADCAST = 6;
    pub const SNDBUF = 7;
    pub const RCVBUF = 8;
    pub const KEEPALIVE = 9;
    pub const OOBINLINE = 10;
    pub const NO_CHECK = 11;
    pub const PRIORITY = 12;
    pub const LINGER = 13;
    pub const BSDCOMPAT = 14;
    pub const REUSEPORT = 15;
    pub const PASSCRED = 16;
    pub const PEERCRED = 17;
    pub const RCVLOWAT = 18;
    pub const SNDLOWAT = 19;
    pub const RCVTIMEO = 20;
    pub const SNDTIMEO = 21;
    pub const ACCEPTCONN = 30;
    pub const PEERSEC = 31;
    pub const SNDBUFFORCE = 32;
    pub const RCVBUFFORCE = 33;
    pub const PROTOCOL = 38;
    pub const DOMAIN = 39;
    pub const SECURITY_AUTHENTICATION = 22;
    pub const SECURITY_ENCRYPTION_TRANSPORT = 23;
    pub const SECURITY_ENCRYPTION_NETWORK = 24;
    pub const BINDTODEVICE = 25;
    pub const ATTACH_FILTER = 26;
    pub const DETACH_FILTER = 27;
    pub const GET_FILTER = ATTACH_FILTER;
    pub const PEERNAME = 28;
    pub const TIMESTAMP_OLD = 29;
    pub const PASSSEC = 34;
    pub const TIMESTAMPNS_OLD = 35;
    pub const MARK = 36;
    pub const TIMESTAMPING_OLD = 37;
    pub const RXQ_OVFL = 40;
    pub const WIFI_STATUS = 41;
    pub const PEEK_OFF = 42;
    pub const NOFCS = 43;
    pub const LOCK_FILTER = 44;
    pub const SELECT_ERR_QUEUE = 45;
    pub const BUSY_POLL = 46;
    pub const MAX_PACING_RATE = 47;
    pub const BPF_EXTENSIONS = 48;
    pub const INCOMING_CPU = 49;
    pub const ATTACH_BPF = 50;
    pub const DETACH_BPF = DETACH_FILTER;
    pub const ATTACH_REUSEPORT_CBPF = 51;
    pub const ATTACH_REUSEPORT_EBPF = 52;
    pub const CNX_ADVICE = 53;
    pub const MEMINFO = 55;
    pub const INCOMING_NAPI_ID = 56;
    pub const COOKIE = 57;
    pub const PEERGROUPS = 59;
    pub const ZEROCOPY = 60;
    pub const TXTIME = 61;
    pub const BINDTOIFINDEX = 62;
    pub const TIMESTAMP_NEW = 63;
    pub const TIMESTAMPNS_NEW = 64;
    pub const TIMESTAMPING_NEW = 65;
    pub const RCVTIMEO_NEW = 66;
    pub const SNDTIMEO_NEW = 67;
    pub const DETACH_REUSEPORT_BPF = 68;
};

pub const SOCK = struct {
    pub const STREAM = 1;
    pub const DGRAM = 2;
    pub const RAW = 3;
    pub const RDM = 4;
    pub const SEQPACKET = 5;
    pub const DCCP = 6;
    pub const PACKET = 10;
    pub const CLOEXEC = 0o2000000;
    pub const NONBLOCK = 0o4000;
};

pub const SOL = struct {
    pub const SOCKET = 1;

    pub const IP = 0;
    pub const IPV6 = 41;
    pub const ICMPV6 = 58;

    pub const RAW = 255;
    pub const DECNET = 261;
    pub const X25 = 262;
    pub const PACKET = 263;
    pub const ATM = 264;
    pub const AAL = 265;
    pub const IRDA = 266;
    pub const NETBEUI = 267;
    pub const LLC = 268;
    pub const DCCP = 269;
    pub const NETLINK = 270;
    pub const TIPC = 271;
    pub const RXRPC = 272;
    pub const PPPOL2TP = 273;
    pub const BLUETOOTH = 274;
    pub const PNPIPE = 275;
    pub const RDS = 276;
    pub const IUCV = 277;
    pub const CAIF = 278;
    pub const ALG = 279;
    pub const NFC = 280;
    pub const KCM = 281;
    pub const TLS = 282;
    pub const XDP = 283;
};

pub const STDIN_FILENO = 0;
pub const STDOUT_FILENO = 1;
pub const STDERR_FILENO = 2;

pub const TCP = struct {
    pub const NODELAY = 1;
    pub const MAXSEG = 2;
    pub const CORK = 3;
    pub const KEEPIDLE = 4;
    pub const KEEPINTVL = 5;
    pub const KEEPCNT = 6;
    pub const SYNCNT = 7;
    pub const LINGER2 = 8;
    pub const DEFER_ACCEPT = 9;
    pub const WINDOW_CLAMP = 10;
    pub const INFO = 11;
    pub const QUICKACK = 12;
    pub const CONGESTION = 13;
    pub const MD5SIG = 14;
    pub const THIN_LINEAR_TIMEOUTS = 16;
    pub const THIN_DUPACK = 17;
    pub const USER_TIMEOUT = 18;
    pub const REPAIR = 19;
    pub const REPAIR_QUEUE = 20;
    pub const QUEUE_SEQ = 21;
    pub const REPAIR_OPTIONS = 22;
    pub const FASTOPEN = 23;
    pub const TIMESTAMP = 24;
    pub const NOTSENT_LOWAT = 25;
    pub const CC_INFO = 26;
    pub const SAVE_SYN = 27;
    pub const SAVED_SYN = 28;
    pub const REPAIR_WINDOW = 29;
    pub const FASTOPEN_CONNECT = 30;
    pub const ULP = 31;
    pub const MD5SIG_EXT = 32;
    pub const FASTOPEN_KEY = 33;
    pub const FASTOPEN_NO_COOKIE = 34;
    pub const ZEROCOPY_RECEIVE = 35;
    pub const INQ = 36;
    pub const CM_INQ = INQ;
    pub const TX_DELAY = 37;

    pub const REPAIR_ON = 1;
    pub const REPAIR_OFF = 0;
    pub const REPAIR_OFF_NO_WP = -1;
};

pub const TCSA = std.posix.TCSA;
pub const addrinfo = c.addrinfo;

pub const in_port_t = c.in_port_t;
pub const sa_family_t = c.sa_family_t;
pub const socklen_t = c.socklen_t;
pub const sockaddr = c.sockaddr;

pub const blksize_t = i32;
pub const nlink_t = u32;
// https://github.com/emscripten-core/emscripten/blob/946ab574ae39401b51e75cd5257d894ae732ab54/system/lib/libc/musl/arch/emscripten/bits/alltypes.h#L140
pub const time_t = c_longlong;
pub const mode_t = u32;
pub const off_t = i64;
pub const ino_t = u64;
pub const dev_t = u32;
pub const blkcnt_t = i32;

pub const pid_t = i32;
pub const fd_t = c.fd_t;
pub const uid_t = u32;
pub const gid_t = u32;
pub const clock_t = i32;

pub const dl_phdr_info = extern struct {
    addr: usize,
    name: ?[*:0]const u8,
    phdr: [*]std.elf.Phdr,
    phnum: u16,
};

pub const msghdr = std.c.msghdr;
pub const msghdr_const = std.c.msghdr;

pub const nfds_t = usize;
pub const pollfd = extern struct {
    fd: fd_t,
    events: i16,
    revents: i16,
};

pub const stack_t = extern struct {
    sp: [*]u8,
    flags: i32,
    size: usize,
};

/// For use with `utimensat` and `futimens`.
// https://github.com/emscripten-core/emscripten/blob/d72d7226f4733af8ff993dec70198cf09a24142d/system/lib/libc/musl/include/sys/stat.h#L77-L78
pub const UTIME = struct {
    pub const NOW: timespec = .{ .sec = 0, .nsec = 0x3fffffff };
    pub const OMIT: timespec = .{ .sec = 0, .nsec = 0x3ffffffe };
};

// https://github.com/emscripten-core/emscripten/blob/946ab574ae39401b51e75cd5257d894ae732ab54/system/lib/libc/musl/arch/emscripten/bits/alltypes.h#L284
pub const timespec = extern struct {
    sec: time_t,
    nsec: c_long,
};

pub const timezone = extern struct {
    minuteswest: c_int,
    dsttime: c_int,
};

pub const utsname = extern struct {
    sysname: [64:0]u8,
    nodename: [64:0]u8,
    release: [64:0]u8,
    version: [64:0]u8,
    machine: [64:0]u8,
    domainname: [64:0]u8,
};

pub const Stat = extern struct {
    dev: dev_t,
    mode: mode_t,
    nlink: nlink_t,
    uid: uid_t,
    gid: gid_t,
    rdev: dev_t,
    size: off_t,
    blksize: blksize_t,
    blocks: blkcnt_t,
    atim: timespec,
    mtim: timespec,
    ctim: timespec,
    ino: ino_t,

    pub fn atime(self: @This()) timespec {
        return self.atim;
    }

    pub fn mtime(self: @This()) timespec {
        return self.mtim;
    }

    pub fn ctime(self: @This()) timespec {
        return self.ctim;
    }
};

pub const TIMING = struct {
    pub const SETTIMEOUT = 0;
    pub const RAF = 1;
    pub const SETIMMEDIATE = 2;
};

pub const LOG = struct {
    pub const CONSOLE = 1;
    pub const WARN = 2;
    pub const ERROR = 4;
    pub const C_STACK = 8;
    pub const JS_STACK = 16;
    pub const DEMANGLE = 32;
    pub const NO_PATHS = 64;
    pub const FUNC_PARAMS = 128;
    pub const DEBUG = 256;
    pub const INFO = 512;
};

pub const em_callback_func = ?*const fn () callconv(.c) void;
pub const em_arg_callback_func = ?*const fn (?*anyopaque) callconv(.c) void;
pub const em_str_callback_func = ?*const fn ([*:0]const u8) callconv(.c) void;

pub extern "c" fn emscripten_async_wget(url: [*:0]const u8, file: [*:0]const u8, onload: em_str_callback_func, onerror: em_str_callback_func) void;

pub const em_async_wget_onload_func = ?*const fn (?*anyopaque, ?*anyopaque, c_int) callconv(.c) void;
pub extern "c" fn emscripten_async_wget_data(url: [*:0]const u8, arg: ?*anyopaque, onload: em_async_wget_onload_func, onerror: em_arg_callback_func) void;

pub const em_async_wget2_onload_func = ?*const fn (c_uint, ?*anyopaque, [*:0]const u8) callconv(.c) void;
pub const em_async_wget2_onstatus_func = ?*const fn (c_uint, ?*anyopaque, c_int) callconv(.c) void;

pub extern "c" fn emscripten_async_wget2(url: [*:0]const u8, file: [*:0]const u8, requesttype: [*:0]const u8, param: [*:0]const u8, arg: ?*anyopaque, onload: em_async_wget2_onload_func, onerror: em_async_wget2_onstatus_func, onprogress: em_async_wget2_onstatus_func) c_int;

pub const em_async_wget2_data_onload_func = ?*const fn (c_uint, ?*anyopaque, ?*anyopaque, c_uint) callconv(.c) void;
pub const em_async_wget2_data_onerror_func = ?*const fn (c_uint, ?*anyopaque, c_int, [*:0]const u8) callconv(.c) void;
pub const em_async_wget2_data_onprogress_func = ?*const fn (c_uint, ?*anyopaque, c_int, c_int) callconv(.c) void;

pub extern "c" fn emscripten_async_wget2_data(url: [*:0]const u8, requesttype: [*:0]const u8, param: [*:0]const u8, arg: ?*anyopaque, free: c_int, onload: em_async_wget2_data_onload_func, onerror: em_async_wget2_data_onerror_func, onprogress: em_async_wget2_data_onprogress_func) c_int;
pub extern "c" fn emscripten_async_wget2_abort(handle: c_int) void;
pub extern "c" fn emscripten_wget(url: [*:0]const u8, file: [*:0]const u8) c_int;
pub extern "c" fn emscripten_wget_data(url: [*:0]const u8, pbuffer: *(?*anyopaque), pnum: *c_int, perror: *c_int) void;
pub extern "c" fn emscripten_run_script(script: [*:0]const u8) void;
pub extern "c" fn emscripten_run_script_int(script: [*:0]const u8) c_int;
pub extern "c" fn emscripten_run_script_string(script: [*:0]const u8) ?[*:0]u8;
pub extern "c" fn emscripten_async_run_script(script: [*:0]const u8, millis: c_int) void;
pub extern "c" fn emscripten_async_load_script(script: [*:0]const u8, onload: em_callback_func, onerror: em_callback_func) void;
pub extern "c" fn emscripten_set_main_loop(func: em_callback_func, fps: c_int, simulate_infinite_loop: c_int) void;
pub extern "c" fn emscripten_set_main_loop_timing(mode: c_int, value: c_int) c_int;
pub extern "c" fn emscripten_get_main_loop_timing(mode: *c_int, value: *c_int) void;
pub extern "c" fn emscripten_set_main_loop_arg(func: em_arg_callback_func, arg: ?*anyopaque, fps: c_int, simulate_infinite_loop: c_int) void;
pub extern "c" fn emscripten_pause_main_loop() void;
pub extern "c" fn emscripten_resume_main_loop() void;
pub extern "c" fn emscripten_cancel_main_loop() void;

pub const em_socket_callback = ?*const fn (c_int, ?*anyopaque) callconv(.c) void;
pub const em_socket_error_callback = ?*const fn (c_int, c_int, [*:0]const u8, ?*anyopaque) callconv(.c) void;

pub extern "c" fn emscripten_set_socket_error_callback(userData: ?*anyopaque, callback: em_socket_error_callback) void;
pub extern "c" fn emscripten_set_socket_open_callback(userData: ?*anyopaque, callback: em_socket_callback) void;
pub extern "c" fn emscripten_set_socket_listen_callback(userData: ?*anyopaque, callback: em_socket_callback) void;
pub extern "c" fn emscripten_set_socket_connection_callback(userData: ?*anyopaque, callback: em_socket_callback) void;
pub extern "c" fn emscripten_set_socket_message_callback(userData: ?*anyopaque, callback: em_socket_callback) void;
pub extern "c" fn emscripten_set_socket_close_callback(userData: ?*anyopaque, callback: em_socket_callback) void;
pub extern "c" fn _emscripten_push_main_loop_blocker(func: em_arg_callback_func, arg: ?*anyopaque, name: [*:0]const u8) void;
pub extern "c" fn _emscripten_push_uncounted_main_loop_blocker(func: em_arg_callback_func, arg: ?*anyopaque, name: [*:0]const u8) void;
pub extern "c" fn emscripten_set_main_loop_expected_blockers(num: c_int) void;
pub extern "c" fn emscripten_async_call(func: em_arg_callback_func, arg: ?*anyopaque, millis: c_int) void;
pub extern "c" fn emscripten_exit_with_live_runtime() noreturn;
pub extern "c" fn emscripten_force_exit(status: c_int) noreturn;
pub extern "c" fn emscripten_get_device_pixel_ratio() f64;
pub extern "c" fn emscripten_get_window_title() [*:0]u8;
pub extern "c" fn emscripten_set_window_title([*:0]const u8) void;
pub extern "c" fn emscripten_get_screen_size(width: *c_int, height: *c_int) void;
pub extern "c" fn emscripten_hide_mouse() void;
pub extern "c" fn emscripten_set_canvas_size(width: c_int, height: c_int) void;
pub extern "c" fn emscripten_get_canvas_size(width: *c_int, height: *c_int, isFullscreen: *c_int) void;
pub extern "c" fn emscripten_get_now() f64;
pub extern "c" fn emscripten_random() f32;
pub const em_idb_onload_func = ?*const fn (?*anyopaque, ?*anyopaque, c_int) callconv(.c) void;
pub extern "c" fn emscripten_idb_async_load(db_name: [*:0]const u8, file_id: [*:0]const u8, arg: ?*anyopaque, onload: em_idb_onload_func, onerror: em_arg_callback_func) void;
pub extern "c" fn emscripten_idb_async_store(db_name: [*:0]const u8, file_id: [*:0]const u8, ptr: ?*anyopaque, num: c_int, arg: ?*anyopaque, onstore: em_arg_callback_func, onerror: em_arg_callback_func) void;
pub extern "c" fn emscripten_idb_async_delete(db_name: [*:0]const u8, file_id: [*:0]const u8, arg: ?*anyopaque, ondelete: em_arg_callback_func, onerror: em_arg_callback_func) void;
pub const em_idb_exists_func = ?*const fn (?*anyopaque, c_int) callconv(.c) void;
pub extern "c" fn emscripten_idb_async_exists(db_name: [*:0]const u8, file_id: [*:0]const u8, arg: ?*anyopaque, oncheck: em_idb_exists_func, onerror: em_arg_callback_func) void;
pub extern "c" fn emscripten_idb_load(db_name: [*:0]const u8, file_id: [*:0]const u8, pbuffer: *?*anyopaque, pnum: *c_int, perror: *c_int) void;
pub extern "c" fn emscripten_idb_store(db_name: [*:0]const u8, file_id: [*:0]const u8, buffer: *anyopaque, num: c_int, perror: *c_int) void;
pub extern "c" fn emscripten_idb_delete(db_name: [*:0]const u8, file_id: [*:0]const u8, perror: *c_int) void;
pub extern "c" fn emscripten_idb_exists(db_name: [*:0]const u8, file_id: [*:0]const u8, pexists: *c_int, perror: *c_int) void;
pub extern "c" fn emscripten_idb_load_blob(db_name: [*:0]const u8, file_id: [*:0]const u8, pblob: *c_int, perror: *c_int) void;
pub extern "c" fn emscripten_idb_store_blob(db_name: [*:0]const u8, file_id: [*:0]const u8, buffer: *anyopaque, num: c_int, perror: *c_int) void;
pub extern "c" fn emscripten_idb_read_from_blob(blob: c_int, start: c_int, num: c_int, buffer: ?*anyopaque) void;
pub extern "c" fn emscripten_idb_free_blob(blob: c_int) void;
pub extern "c" fn emscripten_run_preload_plugins(file: [*:0]const u8, onload: em_str_callback_func, onerror: em_str_callback_func) c_int;
pub const em_run_preload_plugins_data_onload_func = ?*const fn (?*anyopaque, [*:0]const u8) callconv(.c) void;
pub extern "c" fn emscripten_run_preload_plugins_data(data: [*]u8, size: c_int, suffix: [*:0]const u8, arg: ?*anyopaque, onload: em_run_preload_plugins_data_onload_func, onerror: em_arg_callback_func) void;
pub extern "c" fn emscripten_lazy_load_code() void;
pub const worker_handle = c_int;
pub extern "c" fn emscripten_create_worker(url: [*:0]const u8) worker_handle;
pub extern "c" fn emscripten_destroy_worker(worker: worker_handle) void;
pub const em_worker_callback_func = ?*const fn ([*]u8, c_int, ?*anyopaque) callconv(.c) void;
pub extern "c" fn emscripten_call_worker(worker: worker_handle, funcname: [*:0]const u8, data: [*]u8, size: c_int, callback: em_worker_callback_func, arg: ?*anyopaque) void;
pub extern "c" fn emscripten_worker_respond(data: [*]u8, size: c_int) void;
pub extern "c" fn emscripten_worker_respond_provisionally(data: [*]u8, size: c_int) void;
pub extern "c" fn emscripten_get_worker_queue_size(worker: worker_handle) c_int;
pub extern "c" fn emscripten_get_compiler_setting(name: [*:0]const u8) c_long;
pub extern "c" fn emscripten_has_asyncify() c_int;
pub extern "c" fn emscripten_debugger() void;

pub extern "c" fn emscripten_get_preloaded_image_data(path: [*:0]const u8, w: *c_int, h: *c_int) ?[*]u8;
pub extern "c" fn emscripten_get_preloaded_image_data_from_FILE(file: *FILE, w: *c_int, h: *c_int) ?[*]u8;
pub extern "c" fn emscripten_log(flags: c_int, format: [*:0]const u8, ...) void;
pub extern "c" fn emscripten_get_callstack(flags: c_int, out: ?[*]u8, maxbytes: c_int) c_int;
pub extern "c" fn emscripten_print_double(x: f64, to: ?[*]u8, max: c_int) c_int;
pub const em_scan_func = ?*const fn (?*anyopaque, ?*anyopaque) callconv(.c) void;
pub extern "c" fn emscripten_scan_registers(func: em_scan_func) void;
pub extern "c" fn emscripten_scan_stack(func: em_scan_func) void;
pub const em_dlopen_callback = ?*const fn (?*anyopaque, ?*anyopaque) callconv(.c) void;
pub extern "c" fn emscripten_dlopen(filename: [*:0]const u8, flags: c_int, user_data: ?*anyopaque, onsuccess: em_dlopen_callback, onerror: em_arg_callback_func) void;
pub extern "c" fn emscripten_dlopen_promise(filename: [*:0]const u8, flags: c_int) em_promise_t;
pub extern "c" fn emscripten_throw_number(number: f64) void;
pub extern "c" fn emscripten_throw_string(utf8String: [*:0]const u8) void;
pub extern "c" fn emscripten_sleep(ms: c_uint) void;

pub const PROMISE = struct {
    pub const FULFILL = 0;
    pub const MATCH = 1;
    pub const MATCH_RELEASE = 2;
    pub const REJECT = 3;
};

pub const struct__em_promise = opaque {};
pub const em_promise_t = ?*struct__em_promise;
pub const enum_em_promise_result_t = c_uint;
pub const em_promise_result_t = enum_em_promise_result_t;
pub const em_promise_callback_t = ?*const fn (?*?*anyopaque, ?*anyopaque, ?*anyopaque) callconv(.c) em_promise_result_t;

pub extern "c" fn emscripten_promise_create() em_promise_t;
pub extern "c" fn emscripten_promise_destroy(promise: em_promise_t) void;
pub extern "c" fn emscripten_promise_resolve(promise: em_promise_t, result: em_promise_result_t, value: ?*anyopaque) void;
pub extern "c" fn emscripten_promise_then(promise: em_promise_t, on_fulfilled: em_promise_callback_t, on_rejected: em_promise_callback_t, data: ?*anyopaque) em_promise_t;
pub extern "c" fn emscripten_promise_all(promises: [*]em_promise_t, results: ?[*]?*anyopaque, num_promises: usize) em_promise_t;

pub const struct_em_settled_result_t = extern struct {
    result: em_promise_result_t,
    value: ?*anyopaque,
};
pub const em_settled_result_t = struct_em_settled_result_t;



---
File: /std/os/linux.zig
---

//! This file provides the system interface functions for Linux matching those
//! that are provided by libc, whether or not libc is linked. The following
//! abstractions are made:
//! * Implement all the syscalls in the same way that libc functions will
//!   provide `rename` when only the `renameat` syscall exists.
const std = @import("../std.zig");
const builtin = @import("builtin");
const assert = std.debug.assert;
const maxInt = std.math.maxInt;
const elf = std.elf;
const vdso = @import("linux/vdso.zig");
const dl = @import("../dynamic_library.zig");
const native_arch = builtin.cpu.arch;
const native_abi = builtin.abi;
const native_endian = native_arch.endian();
const is_loongarch = native_arch.isLoongArch();
const is_mips = native_arch.isMIPS();
const is_ppc = native_arch.isPowerPC();
const is_riscv = native_arch.isRISCV();
const is_sparc = native_arch.isSPARC();
const iovec = std.posix.iovec;
const iovec_const = std.posix.iovec_const;
const winsize = std.posix.winsize;
const ACCMODE = std.posix.ACCMODE;

test {
    if (builtin.os.tag == .linux) {
        _ = @import("linux/test.zig");
    }
}

const arch_bits = switch (native_arch) {
    .aarch64, .aarch64_be => @import("linux/aarch64.zig"),
    .arm, .armeb, .thumb, .thumbeb => @import("linux/arm.zig"),
    .hexagon => @import("linux/hexagon.zig"),
    .loongarch32 => @import("linux/loongarch32.zig"),
    .loongarch64 => @import("linux/loongarch64.zig"),
    .m68k => @import("linux/m68k.zig"),
    .mips, .mipsel => @import("linux/mips.zig"),
    .mips64, .mips64el => switch (builtin.abi) {
        .gnuabin32, .muslabin32 => @import("linux/mipsn32.zig"),
        else => @import("linux/mips64.zig"),
    },
    .or1k => @import("linux/or1k.zig"),
    .powerpc, .powerpcle => @import("linux/powerpc.zig"),
    .powerpc64, .powerpc64le => @import("linux/powerpc64.zig"),
    .riscv32 => @import("linux/riscv32.zig"),
    .riscv64 => @import("linux/riscv64.zig"),
    .s390x => @import("linux/s390x.zig"),
    .sparc64 => @import("linux/sparc64.zig"),
    .x86 => @import("linux/x86.zig"),
    .x86_64 => switch (builtin.abi) {
        .gnux32, .muslx32 => @import("linux/x32.zig"),
        else => @import("linux/x86_64.zig"),
    },
    else => struct {},
};

const syscall_bits = if (native_arch.isThumb()) @import("linux/thumb.zig") else arch_bits;

pub const syscall0 = syscall_bits.syscall0;
pub const syscall1 = syscall_bits.syscall1;
pub const syscall2 = syscall_bits.syscall2;
pub const syscall3 = syscall_bits.syscall3;
pub const syscall4 = syscall_bits.syscall4;
pub const syscall5 = syscall_bits.syscall5;
pub const syscall6 = syscall_bits.syscall6;
pub const syscall7 = syscall_bits.syscall7;
pub const restore = syscall_bits.restore;
pub const restore_rt = syscall_bits.restore_rt;
pub const socketcall = syscall_bits.socketcall;
pub const syscall_pipe = syscall_bits.syscall_pipe;
pub const syscall_fork = syscall_bits.syscall_fork;

pub fn clone(
    func: *const fn (arg: usize) callconv(.c) u8,
    stack: usize,
    flags: u32,
    arg: usize,
    ptid: ?*i32,
    tp: usize, // aka tls
    ctid: ?*i32,
) usize {
    // Can't directly call a naked function; cast to C calling convention first.
    return @as(*const fn (
        *const fn (arg: usize) callconv(.c) u8,
        usize,
        u32,
        usize,
        ?*i32,
        usize,
        ?*i32,
    ) callconv(.c) usize, @ptrCast(&syscall_bits.clone))(func, stack, flags, arg, ptid, tp, ctid);
}

pub const ARCH = arch_bits.ARCH;
pub const HWCAP = arch_bits.HWCAP;
pub const SC = arch_bits.SC;
pub const VDSO = arch_bits.VDSO;
pub const blkcnt_t = u64;
pub const blksize_t = u32;
pub const dev_t = u64;
pub const ino_t = u64;
pub const mode_t = u32;
pub const nlink_t = u32;
pub const off_t = i64;
pub const time_t = arch_bits.time_t;
pub const user_desc = arch_bits.user_desc;

pub const tls = @import("linux/tls.zig");
pub const BPF = @import("linux/bpf.zig");
pub const IOCTL = @import("linux/ioctl.zig");
pub const SECCOMP = @import("linux/seccomp.zig");

pub const syscalls = @import("linux/syscalls.zig");
pub const SYS = switch (native_arch) {
    .arc, .arceb => syscalls.Arc,
    .aarch64, .aarch64_be => syscalls.Arm64,
    .arm, .armeb, .thumb, .thumbeb => syscalls.Arm,
    .csky => syscalls.CSky,
    .hexagon => syscalls.Hexagon,
    .loongarch32 => syscalls.LoongArch32,
    .loongarch64 => syscalls.LoongArch64,
    .m68k => syscalls.M68k,
    .mips, .mipsel => syscalls.MipsO32,
    .mips64, .mips64el => switch (builtin.abi) {
        .gnuabin32, .muslabin32 => syscalls.MipsN32,
        else => syscalls.MipsN64,
    },
    .or1k => syscalls.OpenRisc,
    .powerpc, .powerpcle => syscalls.PowerPC,
    .powerpc64, .powerpc64le => syscalls.PowerPC64,
    .riscv32 => syscalls.RiscV32,
    .riscv64 => syscalls.RiscV64,
    .s390x => syscalls.S390x,
    .sparc => syscalls.Sparc,
    .sparc64 => syscalls.Sparc64,
    .x86 => syscalls.X86,
    .x86_64 => switch (builtin.abi) {
        .gnux32, .muslx32 => syscalls.X32,
        else => syscalls.X64,
    },
    .xtensa, .xtensaeb => syscalls.Xtensa,
    else => @compileError("The Zig Standard Library is missing syscall definitions for the target CPU architecture"),
};

pub const MAP_TYPE = enum(u4) {
    SHARED = 0x01,
    PRIVATE = 0x02,
    SHARED_VALIDATE = 0x03,
    DROPPABLE = 0x08,
};

pub const MAP = switch (native_arch) {
    .x86_64, .x86 => packed struct(u32) {
        TYPE: MAP_TYPE,
        FIXED: bool = false,
        ANONYMOUS: bool = false,
        @"32BIT": bool = false,
        _7: u1 = 0,
        GROWSDOWN: bool = false,
        _9: u2 = 0,
        DENYWRITE: bool = false,
        EXECUTABLE: bool = false,
        LOCKED: bool = false,
        NORESERVE: bool = false,
        POPULATE: bool = false,
        NONBLOCK: bool = false,
        STACK: bool = false,
        HUGETLB: bool = false,
        SYNC: bool = false,
        FIXED_NOREPLACE: bool = false,
        _21: u5 = 0,
        UNINITIALIZED: bool = false,
        _: u5 = 0,
    },
    .aarch64, .aarch64_be, .arm, .armeb, .thumb, .thumbeb => packed struct(u32) {
        TYPE: MAP_TYPE,
        FIXED: bool = false,
        ANONYMOUS: bool = false,
        _6: u2 = 0,
        GROWSDOWN: bool = false,
        _9: u2 = 0,
        DENYWRITE: bool = false,
        EXECUTABLE: bool = false,
        LOCKED: bool = false,
        NORESERVE: bool = false,
        POPULATE: bool = false,
        NONBLOCK: bool = false,
        STACK: bool = false,
        HUGETLB: bool = false,
        SYNC: bool = false,
        FIXED_NOREPLACE: bool = false,
        _21: u5 = 0,
        UNINITIALIZED: bool = false,
        _: u5 = 0,
    },
    .riscv32, .riscv64, .loongarch32, .loongarch64 => packed struct(u32) {
        TYPE: MAP_TYPE,
        FIXED: bool = false,
        ANONYMOUS: bool = false,
        _6: u9 = 0,
        POPULATE: bool = false,
        NONBLOCK: bool = false,
        STACK: bool = false,
        HUGETLB: bool = false,
        SYNC: bool = false,
        FIXED_NOREPLACE: bool = false,
        _21: u5 = 0,
        UNINITIALIZED: bool = false,
        _: u5 = 0,
    },
    .sparc64 => packed struct(u32) {
        TYPE: MAP_TYPE,
        FIXED: bool = false,
        ANONYMOUS: bool = false,
        NORESERVE: bool = false,
        _7: u1 = 0,
        LOCKED: bool = false,
        GROWSDOWN: bool = false,
        _10: u1 = 0,
        DENYWRITE: bool = false,
        EXECUTABLE: bool = false,
        _13: u2 = 0,
        POPULATE: bool = false,
        NONBLOCK: bool = false,
        STACK: bool = false,
        HUGETLB: bool = false,
        SYNC: bool = false,
        FIXED_NOREPLACE: bool = false,
        _21: u5 = 0,
        UNINITIALIZED: bool = false,
        _: u5 = 0,
    },
    .mips, .mipsel, .mips64, .mips64el => packed struct(u32) {
        TYPE: MAP_TYPE,
        FIXED: bool = false,
        _5: u1 = 0,
        @"32BIT": bool = false,
        _7: u3 = 0,
        NORESERVE: bool = false,
        ANONYMOUS: bool = false,
        GROWSDOWN: bool = false,
        DENYWRITE: bool = false,
        EXECUTABLE: bool = false,
        LOCKED: bool = false,
        POPULATE: bool = false,
        NONBLOCK: bool = false,
        STACK: bool = false,
        HUGETLB: bool = false,
        FIXED_NOREPLACE: bool = false,
        _21: u5 = 0,
        UNINITIALIZED: bool = false,
        _: u5 = 0,
    },
    .powerpc, .powerpcle, .powerpc64, .powerpc64le => packed struct(u32) {
        TYPE: MAP_TYPE,
        FIXED: bool = false,
        ANONYMOUS: bool = false,
        NORESERVE: bool = false,
        LOCKED: bool = false,
        GROWSDOWN: bool = false,
        _9: u2 = 0,
        DENYWRITE: bool = false,
        EXECUTABLE: bool = false,
        _13: u2 = 0,
        POPULATE: bool = false,
        NONBLOCK: bool = false,
        STACK: bool = false,
        HUGETLB: bool = false,
        SYNC: bool = false,
        FIXED_NOREPLACE: bool = false,
        _21: u5 = 0,
        UNINITIALIZED: bool = false,
        _: u5 = 0,
    },
    .hexagon, .m68k, .or1k, .s390x => packed struct(u32) {
        TYPE: MAP_TYPE,
        FIXED: bool = false,
        ANONYMOUS: bool = false,
        _4: u1 = 0,
        _5: u1 = 0,
        GROWSDOWN: bool = false,
        _7: u1 = 0,
        _8: u1 = 0,
        DENYWRITE: bool = false,
        EXECUTABLE: bool = false,
        LOCKED: bool = false,
        NORESERVE: bool = false,
        POPULATE: bool = false,
        NONBLOCK: bool = false,
        STACK: bool = false,
        HUGETLB: bool = false,
        SYNC: bool = false,
        FIXED_NOREPLACE: bool = false,
        _19: u5 = 0,
        UNINITIALIZED: bool = false,
        _: u5 = 0,
    },
    else => @compileError("missing std.os.linux.MAP constants for this architecture"),
};

pub const MREMAP = packed struct(u32) {
    MAYMOVE: bool = false,
    FIXED: bool = false,
    DONTUNMAP: bool = false,
    _: u29 = 0,
};

pub const O = switch (native_arch) {
    .x86_64 => packed struct(u32) {
        ACCMODE: ACCMODE = .RDONLY,
        _2: u4 = 0,
        CREAT: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        TRUNC: bool = false,
        APPEND: bool = false,
        NONBLOCK: bool = false,
        DSYNC: bool = false,
        ASYNC: bool = false,
        DIRECT: bool = false,
        _15: u1 = 0,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        NOATIME: bool = false,
        CLOEXEC: bool = false,
        SYNC: bool = false,
        PATH: bool = false,
        /// This is typically invalid without also setting `DIRECTORY`.
        TMPFILE: bool = false,
        _23: u9 = 0,
    },
    .x86, .riscv32, .riscv64, .loongarch32, .loongarch64 => packed struct(u32) {
        ACCMODE: ACCMODE = .RDONLY,
        _2: u4 = 0,
        CREAT: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        TRUNC: bool = false,
        APPEND: bool = false,
        NONBLOCK: bool = false,
        DSYNC: bool = false,
        ASYNC: bool = false,
        DIRECT: bool = false,
        LARGEFILE: bool = false,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        NOATIME: bool = false,
        CLOEXEC: bool = false,
        SYNC: bool = false,
        PATH: bool = false,
        /// This is typically invalid without also setting `DIRECTORY`.
        TMPFILE: bool = false,
        _23: u9 = 0,
    },
    .aarch64, .aarch64_be, .arm, .armeb, .thumb, .thumbeb => packed struct(u32) {
        ACCMODE: ACCMODE = .RDONLY,
        _2: u4 = 0,
        CREAT: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        TRUNC: bool = false,
        APPEND: bool = false,
        NONBLOCK: bool = false,
        DSYNC: bool = false,
        ASYNC: bool = false,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        DIRECT: bool = false,
        LARGEFILE: bool = false,
        NOATIME: bool = false,
        CLOEXEC: bool = false,
        SYNC: bool = false,
        PATH: bool = false,
        /// This is typically invalid without also setting `DIRECTORY`.
        TMPFILE: bool = false,
        _23: u9 = 0,
    },
    .sparc64 => packed struct(u32) {
        ACCMODE: ACCMODE = .RDONLY,
        _2: u1 = 0,
        APPEND: bool = false,
        _4: u2 = 0,
        ASYNC: bool = false,
        _7: u2 = 0,
        CREAT: bool = false,
        TRUNC: bool = false,
        EXCL: bool = false,
        _12: u1 = 0,
        DSYNC: bool = false,
        NONBLOCK: bool = false,
        NOCTTY: bool = false,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        _18: u2 = 0,
        DIRECT: bool = false,
        NOATIME: bool = false,
        CLOEXEC: bool = false,
        SYNC: bool = false,
        PATH: bool = false,
        /// This is typically invalid without also setting `DIRECTORY`.
        TMPFILE: bool = false,
        _27: u6 = 0,
    },
    .mips, .mipsel, .mips64, .mips64el => packed struct(u32) {
        ACCMODE: ACCMODE = .RDONLY,
        _2: u1 = 0,
        APPEND: bool = false,
        DSYNC: bool = false,
        _5: u2 = 0,
        NONBLOCK: bool = false,
        CREAT: bool = false,
        TRUNC: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        ASYNC: bool = false,
        LARGEFILE: bool = false,
        SYNC: bool = false,
        DIRECT: bool = false,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        NOATIME: bool = false,
        CLOEXEC: bool = false,
        _20: u1 = 0,
        PATH: bool = false,
        /// This is typically invalid without also setting `DIRECTORY`.
        TMPFILE: bool = false,
        _23: u9 = 0,
    },
    .powerpc, .powerpcle, .powerpc64, .powerpc64le => packed struct(u32) {
        ACCMODE: ACCMODE = .RDONLY,
        _2: u4 = 0,
        CREAT: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        TRUNC: bool = false,
        APPEND: bool = false,
        NONBLOCK: bool = false,
        DSYNC: bool = false,
        ASYNC: bool = false,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        LARGEFILE: bool = false,
        DIRECT: bool = false,
        NOATIME: bool = false,
        CLOEXEC: bool = false,
        SYNC: bool = false,
        PATH: bool = false,
        /// This is typically invalid without also setting `DIRECTORY`.
        TMPFILE: bool = false,
        _23: u9 = 0,
    },
    .hexagon, .or1k, .s390x => packed struct(u32) {
        ACCMODE: ACCMODE = .RDONLY,
        _2: u4 = 0,
        CREAT: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        TRUNC: bool = false,
        APPEND: bool = false,
        NONBLOCK: bool = false,
        DSYNC: bool = false,
        ASYNC: bool = false,
        DIRECT: bool = false,
        LARGEFILE: bool = false,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        NOATIME: bool = false,
        CLOEXEC: bool = false,
        /// This is typically invalid without also setting `TMPFILE1` and `DIRECTORY`.
        TMPFILE0: bool = false,
        PATH: bool = false,
        _22: u4 = 0,
        /// This is typically invalid without also setting `TMPFILE0` and `DIRECTORY`.
        TMPFILE1: bool = false,
        _27: u5 = 0,

        // #define O_RSYNC    04010000
        // #define O_SYNC     04010000
        // #define O_NDELAY O_NONBLOCK
    },
    .m68k => packed struct(u32) {
        ACCMODE: ACCMODE = .RDONLY,
        _2: u4 = 0,
        CREAT: bool = false,
        EXCL: bool = false,
        NOCTTY: bool = false,
        TRUNC: bool = false,
        APPEND: bool = false,
        NONBLOCK: bool = false,
        DSYNC: bool = false,
        ASYNC: bool = false,
        DIRECTORY: bool = false,
        NOFOLLOW: bool = false,
        DIRECT: bool = false,
        LARGEFILE: bool = false,
        NOATIME: bool = false,
        CLOEXEC: bool = false,
        _20: u1 = 0,
        PATH: bool = false,
        _22: u10 = 0,
    },
    else => @compileError("missing std.os.linux.O constants for this architecture"),
};

pub const RENAME = packed struct(u32) {
    /// Cannot be set together with `EXCHANGE`.
    NOREPLACE: bool = false,
    /// Cannot be set together with `NOREPLACE`.
    EXCHANGE: bool = false,
    WHITEOUT: bool = false,
    _: u29 = 0,
};

/// Set by startup code, used by `getauxval`.
pub var elf_aux_maybe: ?[*]std.elf.Auxv = null;

/// Whether an external or internal getauxval implementation is used.
const extern_getauxval = switch (builtin.zig_backend) {
    // Calling extern functions is not yet supported with these backends
    .stage2_arm,
    .stage2_powerpc,
    .stage2_riscv64,
    .stage2_sparc64,
    => false,
    else => !builtin.link_libc,
};

pub const getauxval = if (extern_getauxval) struct {
    comptime {
        const root = @import("root");
        // Export this only when building an executable, otherwise it is overriding
        // the libc implementation
        if (builtin.output_mode == .Exe or @hasDecl(root, "main")) {
            @export(&getauxvalImpl, .{ .name = "getauxval", .linkage = .weak });
        }
    }
    extern fn getauxval(index: usize) usize;
}.getauxval else getauxvalImpl;

fn getauxvalImpl(index: usize) callconv(.c) usize {
    @disableInstrumentation();
    const auxv = elf_aux_maybe orelse return 0;
    var i: usize = 0;
    while (auxv[i].a_type != std.elf.AT_NULL) : (i += 1) {
        if (auxv[i].a_type == index)
            return auxv[i].a_un.a_val;
    }
    return 0;
}

// Some architectures (and some syscalls) require 64bit parameters to be passed
// in a even-aligned register pair.
const require_aligned_register_pair =
    builtin.cpu.arch.isArm() or
    builtin.cpu.arch == .hexagon or
    builtin.cpu.arch.isMIPS32() or
    builtin.cpu.arch.isPowerPC32();

// Split a 64bit value into a {LSB,MSB} pair.
// The LE/BE variants specify the endianness to assume.
fn splitValueLE64(val: i64) [2]u32 {
    const u: u64 = @bitCast(val);
    return [2]u32{
        @as(u32, @truncate(u)),
        @as(u32, @truncate(u >> 32)),
    };
}
fn splitValueBE64(val: i64) [2]u32 {
    const u: u64 = @bitCast(val);
    return [2]u32{
        @as(u32, @truncate(u >> 32)),
        @as(u32, @truncate(u)),
    };
}
fn splitValue64(val: i64) [2]u32 {
    const u: u64 = @bitCast(val);
    switch (native_endian) {
        .little => return [2]u32{
            @as(u32, @truncate(u)),
            @as(u32, @truncate(u >> 32)),
        },
        .big => return [2]u32{
            @as(u32, @truncate(u >> 32)),
            @as(u32, @truncate(u)),
        },
    }
}

/// Get the errno from a syscall return value. SUCCESS means no error.
pub fn errno(r: usize) E {
    const signed_r: isize = @bitCast(r);
    const int = if (signed_r > -4096 and signed_r < 0) -signed_r else 0;
    return @enumFromInt(int);
}

pub fn brk(addr: usize) usize {
    return syscall1(.brk, addr);
}

pub fn dup(old: i32) usize {
    return syscall1(.dup, @as(usize, @bitCast(@as(isize, old))));
}

pub fn dup2(old: i32, new: i32) usize {
    if (@hasField(SYS, "dup2")) {
        return syscall2(.dup2, @as(usize, @bitCast(@as(isize, old))), @as(usize, @bitCast(@as(isize, new))));
    } else {
        if (old == new) {
            if (std.debug.runtime_safety) {
                const rc = fcntl(F.GETFD, @as(fd_t, old), 0);
                if (@as(isize, @bitCast(rc)) < 0) return rc;
            }
            return @as(usize, @intCast(old));
        } else {
            return syscall3(.dup3, @as(usize, @bitCast(@as(isize, old))), @as(usize, @bitCast(@as(isize, new))), 0);
        }
    }
}

pub fn dup3(old: i32, new: i32, flags: u32) usize {
    return syscall3(.dup3, @as(usize, @bitCast(@as(isize, old))), @as(usize, @bitCast(@as(isize, new))), flags);
}

pub fn chdir(path: [*:0]const u8) usize {
    return syscall1(.chdir, @intFromPtr(path));
}

pub fn fchdir(fd: fd_t) usize {
    return syscall1(.fchdir, @as(usize, @bitCast(@as(isize, fd))));
}

pub fn chroot(path: [*:0]const u8) usize {
    return syscall1(.chroot, @intFromPtr(path));
}

pub fn execve(path: [*:0]const u8, argv: [*:null]const ?[*:0]const u8, envp: [*:null]const ?[*:0]const u8) usize {
    return syscall3(.execve, @intFromPtr(path), @intFromPtr(argv), @intFromPtr(envp));
}

pub const EXECVEAT = packed struct(u32) {
    _1: u8 = 0, // 0x00000001
    /// Do not follow symbolic links.
    SYMLINK_NOFOLLOW: bool, // 0x00000100
    _200: u3 = 0, // 0x00000200
    /// Allow empty relative pathname.
    EMPTY_PATH: bool, // 0x00001000
    _: u19 = 0,
};

pub fn execveat(dirfd: fd_t, path: [*:0]const u8, argv: [*:null]const ?[*:0]const u8, envp: [*:null]const ?[*:0]const u8, flags: EXECVEAT) usize {
    return syscall5(.execveat, fd_to_usize(dirfd), @intFromPtr(path), @intFromPtr(argv), @intFromPtr(envp), @as(u32, @bitCast(flags)));
}

pub fn fork() usize {
    if (comptime native_arch.isSPARC()) {
        return syscall_fork();
    } else if (@hasField(SYS, "fork")) {
        return syscall0(.fork);
    } else {
        return syscall2(.clone, @intFromEnum(SIG.CHLD), 0);
    }
}

/// This must be inline, and inline call the syscall function, because if the
/// child does a return it will clobber the parent's stack.
/// It is advised to avoid this function and use clone instead, because
/// the compiler is not aware of how vfork affects control flow and you may
/// see different results in optimized builds.
pub inline fn vfork() usize {
    return @call(.always_inline, syscall0, .{.vfork});
}

pub fn futimens(fd: i32, times: ?*const [2]timespec) usize {
    return utimensat(fd, null, times, 0);
}

pub fn utimensat(dirfd: i32, path: ?[*:0]const u8, times: ?*const [2]timespec, flags: u32) usize {
    return syscall4(
        if (@hasField(SYS, "utimensat") and native_arch != .hexagon) .utimensat else .utimensat_time64,
        @as(usize, @bitCast(@as(isize, dirfd))),
        @intFromPtr(path),
        @intFromPtr(times),
        flags,
    );
}

pub fn fallocate(fd: i32, mode: i32, offset: i64, length: i64) usize {
    if (usize_bits < 64) {
        const offset_halves = splitValue64(offset);
        const length_halves = splitValue64(length);
        return syscall6(
            .fallocate,
            @as(usize, @bitCast(@as(isize, fd))),
            @as(usize, @bitCast(@as(isize, mode))),
            offset_halves[0],
            offset_halves[1],
            length_halves[0],
            length_halves[1],
        );
    } else {
        return syscall4(
            .fallocate,
            @as(usize, @bitCast(@as(isize, fd))),
            @as(usize, @bitCast(@as(isize, mode))),
            @as(u64, @bitCast(offset)),
            @as(u64, @bitCast(length)),
        );
    }
}

// The 4th parameter to the v1 futex syscall can either be an optional
// pointer to a timespec, or a uint32, depending on which "op" is being
// performed.
pub const futex_param4 = extern union {
    timeout: ?*const timespec,
    /// On all platforms only the bottom 32-bits of `val2` are relevant.
    /// This is 64-bit to match the pointer in the union.
    val2: usize,
};

/// The futex v1 syscall, 
```
