```
 considered reserved.
        authenticated_write_access: bool = false,
        time_based_authenticated_write_access: bool = false,
        append_write: bool = false,
        /// Indicates that the variable payload begins with a EFI_VARIABLE_AUTHENTICATION_3
        /// structure, and potentially more structures as indicated by fields of
        /// this structure.
        enhanced_authenticated_access: bool = false,
        _pad: u24 = 0,
    };

    pub const VariableAuthentication3 = extern struct {
        version: u8 = 1,
        type: Type,
        metadata_size: u32,
        flags: Flags,

        pub fn payloadConst(self: *const VariableAuthentication3) []const u8 {
            return @constCast(self).payload();
        }

        pub fn payload(self: *VariableAuthentication3) []u8 {
            var ptr: [*]u8 = @ptrCast(self);
            return ptr[@sizeOf(VariableAuthentication3)..self.metadata_size];
        }

        pub const Flags = packed struct(u32) {
            update_cert: bool = false,
            _pad: u31 = 0,
        };

        pub const Type = enum(u8) {
            timestamp = 1,
            nonce = 2,
            _,
        };
    };

    pub const VariableInfo = struct {
        max_variable_storage_size: u64,
        remaining_variable_storage_size: u64,
        max_variable_size: u64,
    };

    pub const VariableNameIterator = struct {
        pub const NextSizeError = uefi.UnexpectedError || error{
            DeviceError,
            Unsupported,
        };

        pub const IterateVariableNameError = NextSizeError || error{
            BufferTooSmall,
        };

        services: *const RuntimeServices,
        buffer: []u16,
        guid: Guid,

        pub fn nextSize(self: *VariableNameIterator) NextSizeError!?usize {
            var len: usize = 0;
            switch (self.services._getNextVariableName(
                &len,
                null,
                &self.guid,
            )) {
                .buffer_too_small => return len,
                .not_found => return null,
                .device_error => return error.DeviceError,
                .unsupported => return error.Unsupported,
                else => |status| return uefi.unexpectedStatus(status),
            }
        }

        /// Call `nextSize` to get the length of the next variable name and check
        /// if `buffer` is large enough to hold the name.
        pub fn next(
            self: *VariableNameIterator,
        ) IterateVariableNameError!?[:0]const u16 {
            var len = self.buffer.len;
            switch (self.services._getNextVariableName(
                &len,
                @ptrCast(self.buffer.ptr),
                &self.guid,
            )) {
                .success => return self.buffer[0 .. len - 1 :0],
                .not_found => return null,
                .buffer_too_small => return error.BufferTooSmall,
                .device_error => return error.DeviceError,
                .unsupported => return error.Unsupported,
                else => |status| return uefi.unexpectedStatus(status),
            }
        }
    };

    pub const signature: u64 = 0x56524553544e5552;
};



---
File: /std/os/uefi/tables/system_table.zig
---

const uefi = @import("std").os.uefi;
const BootServices = uefi.tables.BootServices;
const ConfigurationTable = uefi.tables.ConfigurationTable;
const Handle = uefi.Handle;
const RuntimeServices = uefi.tables.RuntimeServices;
const SimpleTextInputProtocol = uefi.protocol.SimpleTextInput;
const SimpleTextOutputProtocol = uefi.protocol.SimpleTextOutput;
const TableHeader = uefi.tables.TableHeader;

/// The EFI System Table contains pointers to the runtime and boot services tables.
///
/// As the system_table may grow with new UEFI versions, it is important to check hdr.header_size.
///
/// After successfully calling boot_services.exitBootServices, console_in_handle,
/// con_in, console_out_handle, con_out, standard_error_handle, std_err, and
/// boot_services should be set to null. After setting these attributes to null,
/// hdr.crc32 must be recomputed.
pub const SystemTable = extern struct {
    hdr: TableHeader,

    /// A null-terminated string that identifies the vendor that produces the system firmware of the platform.
    firmware_vendor: [*:0]u16,
    firmware_revision: u32,
    console_in_handle: ?Handle,
    con_in: ?*SimpleTextInputProtocol,
    console_out_handle: ?Handle,
    con_out: ?*SimpleTextOutputProtocol,
    standard_error_handle: ?Handle,
    std_err: ?*SimpleTextOutputProtocol,
    runtime_services: *RuntimeServices,
    boot_services: ?*BootServices,
    number_of_table_entries: usize,
    configuration_table: [*]ConfigurationTable,

    pub const signature: u64 = 0x5453595320494249;
    pub const revision_1_02: u32 = (1 << 16) | 2;
    pub const revision_1_10: u32 = (1 << 16) | 10;
    pub const revision_2_00: u32 = (2 << 16);
    pub const revision_2_10: u32 = (2 << 16) | 10;
    pub const revision_2_20: u32 = (2 << 16) | 20;
    pub const revision_2_30: u32 = (2 << 16) | 30;
    pub const revision_2_31: u32 = (2 << 16) | 31;
    pub const revision_2_40: u32 = (2 << 16) | 40;
    pub const revision_2_50: u32 = (2 << 16) | 50;
    pub const revision_2_60: u32 = (2 << 16) | 60;
    pub const revision_2_70: u32 = (2 << 16) | 70;
    pub const revision_2_80: u32 = (2 << 16) | 80;
    pub const revision_2_90: u32 = (2 << 16) | 90;
    pub const revision_2_100: u32 = (2 << 16) | 100;
    pub const revision_2_110: u32 = (2 << 16) | 110;
};



---
File: /std/os/uefi/tables/table_header.zig
---

pub const TableHeader = extern struct {
    signature: u64,
    revision: u32,

    /// The size, in bytes, of the entire table including the TableHeader
    header_size: u32,
    crc32: u32,
    reserved: u32,
};



---
File: /std/os/uefi/device_path.zig
---

const std = @import("../../std.zig");
const assert = std.debug.assert;
const uefi = std.os.uefi;
const Guid = uefi.Guid;

pub const DevicePath = union(Type) {
    hardware: Hardware,
    acpi: Acpi,
    messaging: Messaging,
    media: Media,
    bios_boot_specification: BiosBootSpecification,
    end: End,

    pub const Type = enum(u8) {
        hardware = 0x01,
        acpi = 0x02,
        messaging = 0x03,
        media = 0x04,
        bios_boot_specification = 0x05,
        end = 0x7f,
        _,
    };

    pub const Hardware = union(Subtype) {
        pci: *const PciDevicePath,
        pc_card: *const PcCardDevicePath,
        memory_mapped: *const MemoryMappedDevicePath,
        vendor: *const VendorDevicePath,
        controller: *const ControllerDevicePath,
        bmc: *const BmcDevicePath,

        pub const Subtype = enum(u8) {
            pci = 1,
            pc_card = 2,
            memory_mapped = 3,
            vendor = 4,
            controller = 5,
            bmc = 6,
            _,
        };

        pub const PciDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            function: u8,
            device: u8,
        };

        comptime {
            assert(6 == @sizeOf(PciDevicePath));
            assert(1 == @alignOf(PciDevicePath));

            assert(0 == @offsetOf(PciDevicePath, "type"));
            assert(1 == @offsetOf(PciDevicePath, "subtype"));
            assert(2 == @offsetOf(PciDevicePath, "length"));
            assert(4 == @offsetOf(PciDevicePath, "function"));
            assert(5 == @offsetOf(PciDevicePath, "device"));
        }

        pub const PcCardDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            function_number: u8,
        };

        comptime {
            assert(5 == @sizeOf(PcCardDevicePath));
            assert(1 == @alignOf(PcCardDevicePath));

            assert(0 == @offsetOf(PcCardDevicePath, "type"));
            assert(1 == @offsetOf(PcCardDevicePath, "subtype"));
            assert(2 == @offsetOf(PcCardDevicePath, "length"));
            assert(4 == @offsetOf(PcCardDevicePath, "function_number"));
        }

        pub const MemoryMappedDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            memory_type: u32 align(1),
            start_address: u64 align(1),
            end_address: u64 align(1),
        };

        comptime {
            assert(24 == @sizeOf(MemoryMappedDevicePath));
            assert(1 == @alignOf(MemoryMappedDevicePath));

            assert(0 == @offsetOf(MemoryMappedDevicePath, "type"));
            assert(1 == @offsetOf(MemoryMappedDevicePath, "subtype"));
            assert(2 == @offsetOf(MemoryMappedDevicePath, "length"));
            assert(4 == @offsetOf(MemoryMappedDevicePath, "memory_type"));
            assert(8 == @offsetOf(MemoryMappedDevicePath, "start_address"));
            assert(16 == @offsetOf(MemoryMappedDevicePath, "end_address"));
        }

        pub const VendorDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            vendor_guid: Guid align(1),
        };

        comptime {
            assert(20 == @sizeOf(VendorDevicePath));
            assert(1 == @alignOf(VendorDevicePath));

            assert(0 == @offsetOf(VendorDevicePath, "type"));
            assert(1 == @offsetOf(VendorDevicePath, "subtype"));
            assert(2 == @offsetOf(VendorDevicePath, "length"));
            assert(4 == @offsetOf(VendorDevicePath, "vendor_guid"));
        }

        pub const ControllerDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            controller_number: u32 align(1),
        };

        comptime {
            assert(8 == @sizeOf(ControllerDevicePath));
            assert(1 == @alignOf(ControllerDevicePath));

            assert(0 == @offsetOf(ControllerDevicePath, "type"));
            assert(1 == @offsetOf(ControllerDevicePath, "subtype"));
            assert(2 == @offsetOf(ControllerDevicePath, "length"));
            assert(4 == @offsetOf(ControllerDevicePath, "controller_number"));
        }

        pub const BmcDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            interface_type: u8,
            base_address: u64 align(1),
        };

        comptime {
            assert(13 == @sizeOf(BmcDevicePath));
            assert(1 == @alignOf(BmcDevicePath));

            assert(0 == @offsetOf(BmcDevicePath, "type"));
            assert(1 == @offsetOf(BmcDevicePath, "subtype"));
            assert(2 == @offsetOf(BmcDevicePath, "length"));
            assert(4 == @offsetOf(BmcDevicePath, "interface_type"));
            assert(5 == @offsetOf(BmcDevicePath, "base_address"));
        }
    };

    pub const Acpi = union(Subtype) {
        acpi: *const BaseAcpiDevicePath,
        expanded_acpi: *const ExpandedAcpiDevicePath,
        adr: *const AdrDevicePath,

        pub const Subtype = enum(u8) {
            acpi = 1,
            expanded_acpi = 2,
            adr = 3,
            _,
        };

        pub const BaseAcpiDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            hid: u32 align(1),
            uid: u32 align(1),
        };

        comptime {
            assert(12 == @sizeOf(BaseAcpiDevicePath));
            assert(1 == @alignOf(BaseAcpiDevicePath));

            assert(0 == @offsetOf(BaseAcpiDevicePath, "type"));
            assert(1 == @offsetOf(BaseAcpiDevicePath, "subtype"));
            assert(2 == @offsetOf(BaseAcpiDevicePath, "length"));
            assert(4 == @offsetOf(BaseAcpiDevicePath, "hid"));
            assert(8 == @offsetOf(BaseAcpiDevicePath, "uid"));
        }

        pub const ExpandedAcpiDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            hid: u32 align(1),
            uid: u32 align(1),
            cid: u32 align(1),
            // variable length u16[*:0] strings
            // hid_str, uid_str, cid_str
        };

        comptime {
            assert(16 == @sizeOf(ExpandedAcpiDevicePath));
            assert(1 == @alignOf(ExpandedAcpiDevicePath));

            assert(0 == @offsetOf(ExpandedAcpiDevicePath, "type"));
            assert(1 == @offsetOf(ExpandedAcpiDevicePath, "subtype"));
            assert(2 == @offsetOf(ExpandedAcpiDevicePath, "length"));
            assert(4 == @offsetOf(ExpandedAcpiDevicePath, "hid"));
            assert(8 == @offsetOf(ExpandedAcpiDevicePath, "uid"));
            assert(12 == @offsetOf(ExpandedAcpiDevicePath, "cid"));
        }

        pub const AdrDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            adr: u32 align(1),

            // multiple adr entries can optionally follow
            pub fn adrs(self: *const AdrDevicePath) []align(1) const u32 {
                // self.length is a minimum of 8 with one adr which is size 4.
                const entries = (self.length - 4) / @sizeOf(u32);
                return @as([*]align(1) const u32, @ptrCast(&self.adr))[0..entries];
            }
        };

        comptime {
            assert(8 == @sizeOf(AdrDevicePath));
            assert(1 == @alignOf(AdrDevicePath));

            assert(0 == @offsetOf(AdrDevicePath, "type"));
            assert(1 == @offsetOf(AdrDevicePath, "subtype"));
            assert(2 == @offsetOf(AdrDevicePath, "length"));
            assert(4 == @offsetOf(AdrDevicePath, "adr"));
        }
    };

    pub const Messaging = union(Subtype) {
        atapi: *const AtapiDevicePath,
        scsi: *const ScsiDevicePath,
        fibre_channel: *const FibreChannelDevicePath,
        fibre_channel_ex: *const FibreChannelExDevicePath,
        @"1394": *const F1394DevicePath,
        usb: *const UsbDevicePath,
        sata: *const SataDevicePath,
        usb_wwid: *const UsbWwidDevicePath,
        lun: *const DeviceLogicalUnitDevicePath,
        usb_class: *const UsbClassDevicePath,
        i2o: *const I2oDevicePath,
        mac_address: *const MacAddressDevicePath,
        ipv4: *const Ipv4DevicePath,
        ipv6: *const Ipv6DevicePath,
        vlan: *const VlanDevicePath,
        infini_band: *const InfiniBandDevicePath,
        uart: *const UartDevicePath,
        vendor: *const VendorDefinedDevicePath,

        pub const Subtype = enum(u8) {
            atapi = 1,
            scsi = 2,
            fibre_channel = 3,
            fibre_channel_ex = 21,
            @"1394" = 4,
            usb = 5,
            sata = 18,
            usb_wwid = 16,
            lun = 17,
            usb_class = 15,
            i2o = 6,
            mac_address = 11,
            ipv4 = 12,
            ipv6 = 13,
            vlan = 20,
            infini_band = 9,
            uart = 14,
            vendor = 10,
            _,
        };

        pub const AtapiDevicePath = extern struct {
            pub const Role = enum(u8) {
                master = 0,
                slave = 1,
            };

            pub const Rank = enum(u8) {
                primary = 0,
                secondary = 1,
            };

            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            primary_secondary: Rank,
            slave_master: Role,
            logical_unit_number: u16 align(1),
        };

        comptime {
            assert(8 == @sizeOf(AtapiDevicePath));
            assert(1 == @alignOf(AtapiDevicePath));

            assert(0 == @offsetOf(AtapiDevicePath, "type"));
            assert(1 == @offsetOf(AtapiDevicePath, "subtype"));
            assert(2 == @offsetOf(AtapiDevicePath, "length"));
            assert(4 == @offsetOf(AtapiDevicePath, "primary_secondary"));
            assert(5 == @offsetOf(AtapiDevicePath, "slave_master"));
            assert(6 == @offsetOf(AtapiDevicePath, "logical_unit_number"));
        }

        pub const ScsiDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            target_id: u16 align(1),
            logical_unit_number: u16 align(1),
        };

        comptime {
            assert(8 == @sizeOf(ScsiDevicePath));
            assert(1 == @alignOf(ScsiDevicePath));

            assert(0 == @offsetOf(ScsiDevicePath, "type"));
            assert(1 == @offsetOf(ScsiDevicePath, "subtype"));
            assert(2 == @offsetOf(ScsiDevicePath, "length"));
            assert(4 == @offsetOf(ScsiDevicePath, "target_id"));
            assert(6 == @offsetOf(ScsiDevicePath, "logical_unit_number"));
        }

        pub const FibreChannelDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            reserved: u32 align(1),
            world_wide_name: u64 align(1),
            logical_unit_number: u64 align(1),
        };

        comptime {
            assert(24 == @sizeOf(FibreChannelDevicePath));
            assert(1 == @alignOf(FibreChannelDevicePath));

            assert(0 == @offsetOf(FibreChannelDevicePath, "type"));
            assert(1 == @offsetOf(FibreChannelDevicePath, "subtype"));
            assert(2 == @offsetOf(FibreChannelDevicePath, "length"));
            assert(4 == @offsetOf(FibreChannelDevicePath, "reserved"));
            assert(8 == @offsetOf(FibreChannelDevicePath, "world_wide_name"));
            assert(16 == @offsetOf(FibreChannelDevicePath, "logical_unit_number"));
        }

        pub const FibreChannelExDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            reserved: u32 align(1),
            world_wide_name: u64 align(1),
            logical_unit_number: u64 align(1),
        };

        comptime {
            assert(24 == @sizeOf(FibreChannelExDevicePath));
            assert(1 == @alignOf(FibreChannelExDevicePath));

            assert(0 == @offsetOf(FibreChannelExDevicePath, "type"));
            assert(1 == @offsetOf(FibreChannelExDevicePath, "subtype"));
            assert(2 == @offsetOf(FibreChannelExDevicePath, "length"));
            assert(4 == @offsetOf(FibreChannelExDevicePath, "reserved"));
            assert(8 == @offsetOf(FibreChannelExDevicePath, "world_wide_name"));
            assert(16 == @offsetOf(FibreChannelExDevicePath, "logical_unit_number"));
        }

        pub const F1394DevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            reserved: u32 align(1),
            guid: u64 align(1),
        };

        comptime {
            assert(16 == @sizeOf(F1394DevicePath));
            assert(1 == @alignOf(F1394DevicePath));

            assert(0 == @offsetOf(F1394DevicePath, "type"));
            assert(1 == @offsetOf(F1394DevicePath, "subtype"));
            assert(2 == @offsetOf(F1394DevicePath, "length"));
            assert(4 == @offsetOf(F1394DevicePath, "reserved"));
            assert(8 == @offsetOf(F1394DevicePath, "guid"));
        }

        pub const UsbDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            parent_port_number: u8,
            interface_number: u8,
        };

        comptime {
            assert(6 == @sizeOf(UsbDevicePath));
            assert(1 == @alignOf(UsbDevicePath));

            assert(0 == @offsetOf(UsbDevicePath, "type"));
            assert(1 == @offsetOf(UsbDevicePath, "subtype"));
            assert(2 == @offsetOf(UsbDevicePath, "length"));
            assert(4 == @offsetOf(UsbDevicePath, "parent_port_number"));
            assert(5 == @offsetOf(UsbDevicePath, "interface_number"));
        }

        pub const SataDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            hba_port_number: u16 align(1),
            port_multiplier_port_number: u16 align(1),
            logical_unit_number: u16 align(1),
        };

        comptime {
            assert(10 == @sizeOf(SataDevicePath));
            assert(1 == @alignOf(SataDevicePath));

            assert(0 == @offsetOf(SataDevicePath, "type"));
            assert(1 == @offsetOf(SataDevicePath, "subtype"));
            assert(2 == @offsetOf(SataDevicePath, "length"));
            assert(4 == @offsetOf(SataDevicePath, "hba_port_number"));
            assert(6 == @offsetOf(SataDevicePath, "port_multiplier_port_number"));
            assert(8 == @offsetOf(SataDevicePath, "logical_unit_number"));
        }

        pub const UsbWwidDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            interface_number: u16 align(1),
            device_vendor_id: u16 align(1),
            device_product_id: u16 align(1),

            pub fn serial_number(self: *const UsbWwidDevicePath) []align(1) const u16 {
                const serial_len = (self.length - @sizeOf(UsbWwidDevicePath)) / @sizeOf(u16);
                return @as([*]align(1) const u16, @ptrCast(@as([*]const u8, @ptrCast(self)) + @sizeOf(UsbWwidDevicePath)))[0..serial_len];
            }
        };

        comptime {
            assert(10 == @sizeOf(UsbWwidDevicePath));
            assert(1 == @alignOf(UsbWwidDevicePath));

            assert(0 == @offsetOf(UsbWwidDevicePath, "type"));
            assert(1 == @offsetOf(UsbWwidDevicePath, "subtype"));
            assert(2 == @offsetOf(UsbWwidDevicePath, "length"));
            assert(4 == @offsetOf(UsbWwidDevicePath, "interface_number"));
            assert(6 == @offsetOf(UsbWwidDevicePath, "device_vendor_id"));
            assert(8 == @offsetOf(UsbWwidDevicePath, "device_product_id"));
        }

        pub const DeviceLogicalUnitDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            lun: u8,
        };

        comptime {
            assert(5 == @sizeOf(DeviceLogicalUnitDevicePath));
            assert(1 == @alignOf(DeviceLogicalUnitDevicePath));

            assert(0 == @offsetOf(DeviceLogicalUnitDevicePath, "type"));
            assert(1 == @offsetOf(DeviceLogicalUnitDevicePath, "subtype"));
            assert(2 == @offsetOf(DeviceLogicalUnitDevicePath, "length"));
            assert(4 == @offsetOf(DeviceLogicalUnitDevicePath, "lun"));
        }

        pub const UsbClassDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            vendor_id: u16 align(1),
            product_id: u16 align(1),
            device_class: u8,
            device_subclass: u8,
            device_protocol: u8,
        };

        comptime {
            assert(11 == @sizeOf(UsbClassDevicePath));
            assert(1 == @alignOf(UsbClassDevicePath));

            assert(0 == @offsetOf(UsbClassDevicePath, "type"));
            assert(1 == @offsetOf(UsbClassDevicePath, "subtype"));
            assert(2 == @offsetOf(UsbClassDevicePath, "length"));
            assert(4 == @offsetOf(UsbClassDevicePath, "vendor_id"));
            assert(6 == @offsetOf(UsbClassDevicePath, "product_id"));
            assert(8 == @offsetOf(UsbClassDevicePath, "device_class"));
            assert(9 == @offsetOf(UsbClassDevicePath, "device_subclass"));
            assert(10 == @offsetOf(UsbClassDevicePath, "device_protocol"));
        }

        pub const I2oDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            tid: u32 align(1),
        };

        comptime {
            assert(8 == @sizeOf(I2oDevicePath));
            assert(1 == @alignOf(I2oDevicePath));

            assert(0 == @offsetOf(I2oDevicePath, "type"));
            assert(1 == @offsetOf(I2oDevicePath, "subtype"));
            assert(2 == @offsetOf(I2oDevicePath, "length"));
            assert(4 == @offsetOf(I2oDevicePath, "tid"));
        }

        pub const MacAddressDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            mac_address: uefi.MacAddress,
            if_type: u8,
        };

        comptime {
            assert(37 == @sizeOf(MacAddressDevicePath));
            assert(1 == @alignOf(MacAddressDevicePath));

            assert(0 == @offsetOf(MacAddressDevicePath, "type"));
            assert(1 == @offsetOf(MacAddressDevicePath, "subtype"));
            assert(2 == @offsetOf(MacAddressDevicePath, "length"));
            assert(4 == @offsetOf(MacAddressDevicePath, "mac_address"));
            assert(36 == @offsetOf(MacAddressDevicePath, "if_type"));
        }

        pub const Ipv4DevicePath = extern struct {
            pub const IpType = enum(u8) {
                dhcp = 0,
                static = 1,
            };

            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            local_ip_address: uefi.Ipv4Address align(1),
            remote_ip_address: uefi.Ipv4Address align(1),
            local_port: u16 align(1),
            remote_port: u16 align(1),
            network_protocol: u16 align(1),
            static_ip_address: IpType,
            gateway_ip_address: u32 align(1),
            subnet_mask: u32 align(1),
        };

        comptime {
            assert(27 == @sizeOf(Ipv4DevicePath));
            assert(1 == @alignOf(Ipv4DevicePath));

            assert(0 == @offsetOf(Ipv4DevicePath, "type"));
            assert(1 == @offsetOf(Ipv4DevicePath, "subtype"));
            assert(2 == @offsetOf(Ipv4DevicePath, "length"));
            assert(4 == @offsetOf(Ipv4DevicePath, "local_ip_address"));
            assert(8 == @offsetOf(Ipv4DevicePath, "remote_ip_address"));
            assert(12 == @offsetOf(Ipv4DevicePath, "local_port"));
            assert(14 == @offsetOf(Ipv4DevicePath, "remote_port"));
            assert(16 == @offsetOf(Ipv4DevicePath, "network_protocol"));
            assert(18 == @offsetOf(Ipv4DevicePath, "static_ip_address"));
            assert(19 == @offsetOf(Ipv4DevicePath, "gateway_ip_address"));
            assert(23 == @offsetOf(Ipv4DevicePath, "subnet_mask"));
        }

        pub const Ipv6DevicePath = extern struct {
            pub const Origin = enum(u8) {
                manual = 0,
                assigned_stateless = 1,
                assigned_stateful = 2,
            };

            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            local_ip_address: uefi.Ipv6Address,
            remote_ip_address: uefi.Ipv6Address,
            local_port: u16 align(1),
            remote_port: u16 align(1),
            protocol: u16 align(1),
            ip_address_origin: Origin,
            prefix_length: u8,
            gateway_ip_address: uefi.Ipv6Address,
        };

        comptime {
            assert(60 == @sizeOf(Ipv6DevicePath));
            assert(1 == @alignOf(Ipv6DevicePath));

            assert(0 == @offsetOf(Ipv6DevicePath, "type"));
            assert(1 == @offsetOf(Ipv6DevicePath, "subtype"));
            assert(2 == @offsetOf(Ipv6DevicePath, "length"));
            assert(4 == @offsetOf(Ipv6DevicePath, "local_ip_address"));
            assert(20 == @offsetOf(Ipv6DevicePath, "remote_ip_address"));
            assert(36 == @offsetOf(Ipv6DevicePath, "local_port"));
            assert(38 == @offsetOf(Ipv6DevicePath, "remote_port"));
            assert(40 == @offsetOf(Ipv6DevicePath, "protocol"));
            assert(42 == @offsetOf(Ipv6DevicePath, "ip_address_origin"));
            assert(43 == @offsetOf(Ipv6DevicePath, "prefix_length"));
            assert(44 == @offsetOf(Ipv6DevicePath, "gateway_ip_address"));
        }

        pub const VlanDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            vlan_id: u16 align(1),
        };

        comptime {
            assert(6 == @sizeOf(VlanDevicePath));
            assert(1 == @alignOf(VlanDevicePath));

            assert(0 == @offsetOf(VlanDevicePath, "type"));
            assert(1 == @offsetOf(VlanDevicePath, "subtype"));
            assert(2 == @offsetOf(VlanDevicePath, "length"));
            assert(4 == @offsetOf(VlanDevicePath, "vlan_id"));
        }

        pub const InfiniBandDevicePath = extern struct {
            pub const ResourceFlags = packed struct(u32) {
                pub const ControllerType = enum(u1) {
                    ioc = 0,
                    service = 1,
                };

                ioc_or_service: ControllerType,
                extend_boot_environment: bool,
                console_protocol: bool,
                storage_protocol: bool,
                network_protocol: bool,

                // u1 + 4 * bool = 5 bits, we need a total of 32 bits
                reserved: u27,
            };

            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            resource_flags: ResourceFlags align(1),
            port_gid: [16]u8,
            service_id: u64 align(1),
            target_port_id: u64 align(1),
            device_id: u64 align(1),
        };

        comptime {
            assert(48 == @sizeOf(InfiniBandDevicePath));
            assert(1 == @alignOf(InfiniBandDevicePath));

            assert(0 == @offsetOf(InfiniBandDevicePath, "type"));
            assert(1 == @offsetOf(InfiniBandDevicePath, "subtype"));
            assert(2 == @offsetOf(InfiniBandDevicePath, "length"));
            assert(4 == @offsetOf(InfiniBandDevicePath, "resource_flags"));
            assert(8 == @offsetOf(InfiniBandDevicePath, "port_gid"));
            assert(24 == @offsetOf(InfiniBandDevicePath, "service_id"));
            assert(32 == @offsetOf(InfiniBandDevicePath, "target_port_id"));
            assert(40 == @offsetOf(InfiniBandDevicePath, "device_id"));
        }

        pub const UartDevicePath = extern struct {
            pub const Parity = enum(u8) {
                default = 0,
                none = 1,
                even = 2,
                odd = 3,
                mark = 4,
                space = 5,
                _,
            };

            pub const StopBits = enum(u8) {
                default = 0,
                one = 1,
                one_and_a_half = 2,
                two = 3,
                _,
            };

            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            reserved: u32 align(1),
            baud_rate: u64 align(1),
            data_bits: u8,
            parity: Parity,
            stop_bits: StopBits,
        };

        comptime {
            assert(19 == @sizeOf(UartDevicePath));
            assert(1 == @alignOf(UartDevicePath));

            assert(0 == @offsetOf(UartDevicePath, "type"));
            assert(1 == @offsetOf(UartDevicePath, "subtype"));
            assert(2 == @offsetOf(UartDevicePath, "length"));
            assert(4 == @offsetOf(UartDevicePath, "reserved"));
            assert(8 == @offsetOf(UartDevicePath, "baud_rate"));
            assert(16 == @offsetOf(UartDevicePath, "data_bits"));
            assert(17 == @offsetOf(UartDevicePath, "parity"));
            assert(18 == @offsetOf(UartDevicePath, "stop_bits"));
        }

        pub const VendorDefinedDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            vendor_guid: Guid align(1),
        };

        comptime {
            assert(20 == @sizeOf(VendorDefinedDevicePath));
            assert(1 == @alignOf(VendorDefinedDevicePath));

            assert(0 == @offsetOf(VendorDefinedDevicePath, "type"));
            assert(1 == @offsetOf(VendorDefinedDevicePath, "subtype"));
            assert(2 == @offsetOf(VendorDefinedDevicePath, "length"));
            assert(4 == @offsetOf(VendorDefinedDevicePath, "vendor_guid"));
        }
    };

    pub const Media = union(Subtype) {
        hard_drive: *const HardDriveDevicePath,
        cdrom: *const CdromDevicePath,
        vendor: *const VendorDevicePath,
        file_path: *const FilePathDevicePath,
        media_protocol: *const MediaProtocolDevicePath,
        piwg_firmware_file: *const PiwgFirmwareFileDevicePath,
        piwg_firmware_volume: *const PiwgFirmwareVolumeDevicePath,
        relative_offset_range: *const RelativeOffsetRangeDevicePath,
        ram_disk: *const RamDiskDevicePath,

        pub const Subtype = enum(u8) {
            hard_drive = 1,
            cdrom = 2,
            vendor = 3,
            file_path = 4,
            media_protocol = 5,
            piwg_firmware_file = 6,
            piwg_firmware_volume = 7,
            relative_offset_range = 8,
            ram_disk = 9,
            _,
        };

        pub const HardDriveDevicePath = extern struct {
            pub const Format = enum(u8) {
                legacy_mbr = 0x01,
                guid_partition_table = 0x02,
            };

            pub const SignatureType = enum(u8) {
                no_signature = 0x00,
                /// "32-bit signature from address 0x1b8 of the type 0x01 MBR"
                mbr_signature = 0x01,
                guid_signature = 0x02,
            };

            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            partition_number: u32 align(1),
            partition_start: u64 align(1),
            partition_size: u64 align(1),
            partition_signature: [16]u8,
            partition_format: Format,
            signature_type: SignatureType,
        };

        comptime {
            assert(42 == @sizeOf(HardDriveDevicePath));
            assert(1 == @alignOf(HardDriveDevicePath));

            assert(0 == @offsetOf(HardDriveDevicePath, "type"));
            assert(1 == @offsetOf(HardDriveDevicePath, "subtype"));
            assert(2 == @offsetOf(HardDriveDevicePath, "length"));
            assert(4 == @offsetOf(HardDriveDevicePath, "partition_number"));
            assert(8 == @offsetOf(HardDriveDevicePath, "partition_start"));
            assert(16 == @offsetOf(HardDriveDevicePath, "partition_size"));
            assert(24 == @offsetOf(HardDriveDevicePath, "partition_signature"));
            assert(40 == @offsetOf(HardDriveDevicePath, "partition_format"));
            assert(41 == @offsetOf(HardDriveDevicePath, "signature_type"));
        }

        pub const CdromDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            boot_entry: u32 align(1),
            partition_start: u64 align(1),
            partition_size: u64 align(1),
        };

        comptime {
            assert(24 == @sizeOf(CdromDevicePath));
            assert(1 == @alignOf(CdromDevicePath));

            assert(0 == @offsetOf(CdromDevicePath, "type"));
            assert(1 == @offsetOf(CdromDevicePath, "subtype"));
            assert(2 == @offsetOf(CdromDevicePath, "length"));
            assert(4 == @offsetOf(CdromDevicePath, "boot_entry"));
            assert(8 == @offsetOf(CdromDevicePath, "partition_start"));
            assert(16 == @offsetOf(CdromDevicePath, "partition_size"));
        }

        pub const VendorDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            guid: Guid align(1),
        };

        comptime {
            assert(20 == @sizeOf(VendorDevicePath));
            assert(1 == @alignOf(VendorDevicePath));

            assert(0 == @offsetOf(VendorDevicePath, "type"));
            assert(1 == @offsetOf(VendorDevicePath, "subtype"));
            assert(2 == @offsetOf(VendorDevicePath, "length"));
            assert(4 == @offsetOf(VendorDevicePath, "guid"));
        }

        pub const FilePathDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),

            pub fn getPath(self: *const FilePathDevicePath) [*:0]align(1) const u16 {
                return @as([*:0]align(1) const u16, @ptrCast(@as([*]const u8, @ptrCast(self)) + @sizeOf(FilePathDevicePath)));
            }
        };

        comptime {
            assert(4 == @sizeOf(FilePathDevicePath));
            assert(1 == @alignOf(FilePathDevicePath));

            assert(0 == @offsetOf(FilePathDevicePath, "type"));
            assert(1 == @offsetOf(FilePathDevicePath, "subtype"));
            assert(2 == @offsetOf(FilePathDevicePath, "length"));
        }

        pub const MediaProtocolDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            guid: Guid align(1),
        };

        comptime {
            assert(20 == @sizeOf(MediaProtocolDevicePath));
            assert(1 == @alignOf(MediaProtocolDevicePath));

            assert(0 == @offsetOf(MediaProtocolDevicePath, "type"));
            assert(1 == @offsetOf(MediaProtocolDevicePath, "subtype"));
            assert(2 == @offsetOf(MediaProtocolDevicePath, "length"));
            assert(4 == @offsetOf(MediaProtocolDevicePath, "guid"));
        }

        pub const PiwgFirmwareFileDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            fv_filename: Guid align(1),
        };

        comptime {
            assert(20 == @sizeOf(PiwgFirmwareFileDevicePath));
            assert(1 == @alignOf(PiwgFirmwareFileDevicePath));

            assert(0 == @offsetOf(PiwgFirmwareFileDevicePath, "type"));
            assert(1 == @offsetOf(PiwgFirmwareFileDevicePath, "subtype"));
            assert(2 == @offsetOf(PiwgFirmwareFileDevicePath, "length"));
            assert(4 == @offsetOf(PiwgFirmwareFileDevicePath, "fv_filename"));
        }

        pub const PiwgFirmwareVolumeDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            fv_name: Guid align(1),
        };

        comptime {
            assert(20 == @sizeOf(PiwgFirmwareVolumeDevicePath));
            assert(1 == @alignOf(PiwgFirmwareVolumeDevicePath));

            assert(0 == @offsetOf(PiwgFirmwareVolumeDevicePath, "type"));
            assert(1 == @offsetOf(PiwgFirmwareVolumeDevicePath, "subtype"));
            assert(2 == @offsetOf(PiwgFirmwareVolumeDevicePath, "length"));
            assert(4 == @offsetOf(PiwgFirmwareVolumeDevicePath, "fv_name"));
        }

        pub const RelativeOffsetRangeDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            reserved: u32 align(1),
            start: u64 align(1),
            end: u64 align(1),
        };

        comptime {
            assert(24 == @sizeOf(RelativeOffsetRangeDevicePath));
            assert(1 == @alignOf(RelativeOffsetRangeDevicePath));

            assert(0 == @offsetOf(RelativeOffsetRangeDevicePath, "type"));
            assert(1 == @offsetOf(RelativeOffsetRangeDevicePath, "subtype"));
            assert(2 == @offsetOf(RelativeOffsetRangeDevicePath, "length"));
            assert(4 == @offsetOf(RelativeOffsetRangeDevicePath, "reserved"));
            assert(8 == @offsetOf(RelativeOffsetRangeDevicePath, "start"));
            assert(16 == @offsetOf(RelativeOffsetRangeDevicePath, "end"));
        }

        pub const RamDiskDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            start: u64 align(1),
            end: u64 align(1),
            disk_type: Guid align(1),
            instance: u16 align(1),
        };

        comptime {
            assert(38 == @sizeOf(RamDiskDevicePath));
            assert(1 == @alignOf(RamDiskDevicePath));

            assert(0 == @offsetOf(RamDiskDevicePath, "type"));
            assert(1 == @offsetOf(RamDiskDevicePath, "subtype"));
            assert(2 == @offsetOf(RamDiskDevicePath, "length"));
            assert(4 == @offsetOf(RamDiskDevicePath, "start"));
            assert(12 == @offsetOf(RamDiskDevicePath, "end"));
            assert(20 == @offsetOf(RamDiskDevicePath, "disk_type"));
            assert(36 == @offsetOf(RamDiskDevicePath, "instance"));
        }
    };

    pub const BiosBootSpecification = union(Subtype) {
        bbs101: *const BBS101DevicePath,

        pub const Subtype = enum(u8) {
            bbs101 = 1,
            _,
        };

        pub const BBS101DevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
            device_type: u16 align(1),
            status_flag: u16 align(1),

            pub fn getDescription(self: *const BBS101DevicePath) [*:0]const u8 {
                return @as([*:0]const u8, @ptrCast(self)) + @sizeOf(BBS101DevicePath);
            }
        };

        comptime {
            assert(8 == @sizeOf(BBS101DevicePath));
            assert(1 == @alignOf(BBS101DevicePath));

            assert(0 == @offsetOf(BBS101DevicePath, "type"));
            assert(1 == @offsetOf(BBS101DevicePath, "subtype"));
            assert(2 == @offsetOf(BBS101DevicePath, "length"));
            assert(4 == @offsetOf(BBS101DevicePath, "device_type"));
            assert(6 == @offsetOf(BBS101DevicePath, "status_flag"));
        }
    };

    pub const End = union(Subtype) {
        end_entire: *const EndEntireDevicePath,
        end_this_instance: *const EndThisInstanceDevicePath,

        pub const Subtype = enum(u8) {
            end_entire = 0xff,
            end_this_instance = 0x01,
            _,
        };

        pub const EndEntireDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
        };

        comptime {
            assert(4 == @sizeOf(EndEntireDevicePath));
            assert(1 == @alignOf(EndEntireDevicePath));

            assert(0 == @offsetOf(EndEntireDevicePath, "type"));
            assert(1 == @offsetOf(EndEntireDevicePath, "subtype"));
            assert(2 == @offsetOf(EndEntireDevicePath, "length"));
        }

        pub const EndThisInstanceDevicePath = extern struct {
            type: DevicePath.Type,
            subtype: Subtype,
            length: u16 align(1),
        };

        comptime {
            assert(4 == @sizeOf(EndEntireDevicePath));
            assert(1 == @alignOf(EndEntireDevicePath));

            assert(0 == @offsetOf(EndEntireDevicePath, "type"));
            assert(1 == @offsetOf(EndEntireDevicePath, "subtype"));
            assert(2 == @offsetOf(EndEntireDevicePath, "length"));
        }
    };
};



---
File: /std/os/uefi/hii.zig
---

const uefi = @import("std").os.uefi;
const Guid = uefi.Guid;

pub const Handle = *opaque {};

/// The header found at the start of each package.
pub const PackageHeader = packed struct(u32) {
    length: u24,
    type: u8,

    pub const type_all: u8 = 0x0;
    pub const type_guid: u8 = 0x1;
    pub const forms: u8 = 0x2;
    pub const strings: u8 = 0x4;
    pub const fonts: u8 = 0x5;
    pub const images: u8 = 0x6;
    pub const simple_fonsts: u8 = 0x7;
    pub const device_path: u8 = 0x8;
    pub const keyboard_layout: u8 = 0x9;
    pub const animations: u8 = 0xa;
    pub const end: u8 = 0xdf;
    pub const type_system_begin: u8 = 0xe0;
    pub const type_system_end: u8 = 0xff;
};

/// The header found at the start of each package list.
pub const PackageList = extern struct {
    package_list_guid: Guid,

    /// The size of the package list (in bytes), including the header.
    package_list_length: u32,

    // TODO implement iterator
};

pub const SimplifiedFontPackage = extern struct {
    header: PackageHeader,
    number_of_narrow_glyphs: u16,
    number_of_wide_glyphs: u16,

    pub fn getNarrowGlyphs(self: *SimplifiedFontPackage) []NarrowGlyph {
        return @as([*]NarrowGlyph, @ptrCast(@alignCast(@as([*]u8, @ptrCast(self)) + @sizeOf(SimplifiedFontPackage))))[0..self.number_of_narrow_glyphs];
    }
};

pub const NarrowGlyphAttributes = packed struct(u8) {
    non_spacing: bool,
    wide: bool,
    _pad: u6 = 0,
};

pub const NarrowGlyph = extern struct {
    unicode_weight: u16,
    attributes: NarrowGlyphAttributes,
    glyph_col_1: [19]u8,
};

pub const WideGlyphAttributes = packed struct(u8) {
    non_spacing: bool,
    wide: bool,
    _pad: u6 = 0,
};

pub const WideGlyph = extern struct {
    unicode_weight: u16,
    attributes: WideGlyphAttributes,
    glyph_col_1: [19]u8,
    glyph_col_2: [19]u8,
    _pad: [3]u8 = [_]u8{0} ** 3,
};

pub const StringPackage = extern struct {
    header: PackageHeader,
    hdr_size: u32,
    string_info_offset: u32,
    language_window: [16]u16,
    language_name: u16,
    language: [3]u8,
};



---
File: /std/os/uefi/pool_allocator.zig
---

const std = @import("std");

const mem = std.mem;
const uefi = std.os.uefi;

const assert = std.debug.assert;

const Allocator = mem.Allocator;

const UefiPoolAllocator = struct {
    fn getHeader(ptr: [*]u8) *[*]align(8) u8 {
        return @as(*[*]align(8) u8, @ptrFromInt(@intFromPtr(ptr) - @sizeOf(usize)));
    }

    fn alloc(
        _: *anyopaque,
        len: usize,
        alignment: mem.Alignment,
        ret_addr: usize,
    ) ?[*]u8 {
        _ = ret_addr;

        assert(len > 0);

        const ptr_align = alignment.toByteUnits();

        const metadata_len = mem.alignForward(usize, @sizeOf(usize), ptr_align);

        const full_len = metadata_len + len;

        const unaligned_slice = uefi.system_table.boot_services.?.allocatePool(
            uefi.efi_pool_memory_type,
            full_len,
        ) catch return null;

        const unaligned_addr = @intFromPtr(unaligned_slice.ptr);
        const aligned_addr = mem.alignForward(usize, unaligned_addr + @sizeOf(usize), ptr_align);

        const aligned_ptr = unaligned_slice.ptr + (aligned_addr - unaligned_addr);
        getHeader(aligned_ptr).* = unaligned_slice.ptr;

        return aligned_ptr;
    }

    fn resize(
        _: *anyopaque,
        buf: []u8,
        alignment: mem.Alignment,
        new_len: usize,
        ret_addr: usize,
    ) bool {
        _ = ret_addr;
        _ = alignment;

        if (new_len > buf.len) return false;
        return true;
    }

    fn remap(
        _: *anyopaque,
        buf: []u8,
        alignment: mem.Alignment,
        new_len: usize,
        ret_addr: usize,
    ) ?[*]u8 {
        _ = alignment;
        _ = ret_addr;

        if (new_len > buf.len) return null;
        return buf.ptr;
    }

    fn free(
        _: *anyopaque,
        buf: []u8,
        alignment: mem.Alignment,
        ret_addr: usize,
    ) void {
        _ = alignment;
        _ = ret_addr;
        uefi.system_table.boot_services.?.freePool(getHeader(buf.ptr).*) catch unreachable;
    }
};

/// Supports the full Allocator interface, including alignment.
/// For a direct call of `allocatePool`, see `raw_pool_allocator`.
pub const pool_allocator = Allocator{
    .ptr = undefined,
    .vtable = &pool_allocator_vtable,
};

const pool_allocator_vtable = Allocator.VTable{
    .alloc = UefiPoolAllocator.alloc,
    .resize = UefiPoolAllocator.resize,
    .remap = UefiPoolAllocator.remap,
    .free = UefiPoolAllocator.free,
};

/// Asserts allocations are 8 byte aligned and calls `boot_services.allocatePool`.
pub const raw_pool_allocator = Allocator{
    .ptr = undefined,
    .vtable = &raw_pool_allocator_table,
};

const raw_pool_allocator_table = Allocator.VTable{
    .alloc = uefi_alloc,
    .resize = uefi_resize,
    .remap = uefi_remap,
    .free = uefi_free,
};

fn uefi_alloc(
    _: *anyopaque,
    len: usize,
    alignment: mem.Alignment,
    ret_addr: usize,
) ?[*]u8 {
    _ = ret_addr;

    std.debug.assert(@intFromEnum(alignment) <= 3);

    const slice = uefi.system_table.boot_services.?.allocatePool(
        uefi.efi_pool_memory_type,
        len,
    ) catch return null;

    return slice.ptr;
}

fn uefi_resize(
    _: *anyopaque,
    buf: []u8,
    alignment: mem.Alignment,
    new_len: usize,
    ret_addr: usize,
) bool {
    _ = ret_addr;

    std.debug.assert(@intFromEnum(alignment) <= 3);

    if (new_len > buf.len) return false;
    return true;
}

fn uefi_remap(
    _: *anyopaque,
    buf: []u8,
    alignment: mem.Alignment,
    new_len: usize,
    ret_addr: usize,
) ?[*]u8 {
    _ = ret_addr;

    std.debug.assert(@intFromEnum(alignment) <= 3);

    if (new_len > buf.len) return null;
    return buf.ptr;
}

fn uefi_free(
    _: *anyopaque,
    buf: []u8,
    alignment: mem.Alignment,
    ret_addr: usize,
) void {
    _ = alignment;
    _ = ret_addr;
    uefi.system_table.boot_services.?.freePool(@alignCast(buf.ptr)) catch unreachable;
}



---
File: /std/os/uefi/protocol.zig
---

const std = @import("std");
const uefi = std.os.uefi;

pub const ServiceBinding = @import("protocol/service_binding.zig").ServiceBinding;

pub const LoadedImage = @import("protocol/loaded_image.zig").LoadedImage;
pub const DevicePath = @import("protocol/device_path.zig").DevicePath;
pub const Rng = @import("protocol/rng.zig").Rng;
pub const ShellParameters = @import("protocol/shell_parameters.zig").ShellParameters;

pub const SimpleFileSystem = @import("protocol/simple_file_system.zig").SimpleFileSystem;
pub const File = @import("protocol/file.zig").File;
pub const BlockIo = @import("protocol/block_io.zig").BlockIo;

pub const SimpleTextInput = @import("protocol/simple_text_input.zig").SimpleTextInput;
pub const SimpleTextInputEx = @import("protocol/simple_text_input_ex.zig").SimpleTextInputEx;
pub const SimpleTextOutput = @import("protocol/simple_text_output.zig").SimpleTextOutput;

pub const SimplePointer = @import("protocol/simple_pointer.zig").SimplePointer;
pub const AbsolutePointer = @import("protocol/absolute_pointer.zig").AbsolutePointer;

pub const SerialIo = @import("protocol/serial_io.zig").SerialIo;

pub const GraphicsOutput = @import("protocol/graphics_output.zig").GraphicsOutput;

pub const edid = @import("protocol/edid.zig");

pub const SimpleNetwork = @import("protocol/simple_network.zig").SimpleNetwork;
pub const ManagedNetwork = @import("protocol/managed_network.zig").ManagedNetwork;

pub const Ip6ServiceBinding = ServiceBinding(.{
    .time_low = 0xec835dd3,
    .time_mid = 0xfe0f,
    .time_high_and_version = 0x617b,
    .clock_seq_high_and_reserved = 0xa6,
    .clock_seq_low = 0x21,
    .node = [_]u8{ 0xb3, 0x50, 0xc3, 0xe1, 0x33, 0x88 },
});
pub const Ip6 = @import("protocol/ip6.zig").Ip6;
pub const Ip6Config = @import("protocol/ip6_config.zig").Ip6Config;

pub const Udp6ServiceBinding = ServiceBinding(.{
    .time_low = 0x66ed4721,
    .time_mid = 0x3c98,
    .time_high_and_version = 0x4d3e,
    .clock_seq_high_and_reserved = 0x81,
    .clock_seq_low = 0xe3,
    .node = [_]u8{ 0xd0, 0x3d, 0xd3, 0x9a, 0x72, 0x54 },
});
pub const Udp6 = @import("protocol/udp6.zig").Udp6;

pub const HiiDatabase = @import("protocol/hii_database.zig").HiiDatabase;
pub const HiiPopup = @import("protocol/hii_popup.zig").HiiPopup;

test {
    std.testing.refAllDecls(@This());
}



---
File: /std/os/uefi/status.zig
---

const testing = @import("std").testing;

const high_bit = 1 << @typeInfo(usize).int.bits - 1;

pub const Status = enum(usize) {
    /// The operation completed successfully.
    success = 0,

    /// The image failed to load.
    load_error = high_bit | 1,

    /// A parameter was incorrect.
    invalid_parameter = high_bit | 2,

    /// The operation is not supported.
    unsupported = high_bit | 3,

    /// The buffer was not the proper size for the request.
    bad_buffer_size = high_bit | 4,

    /// The buffer is not large enough to hold the requested data. The required buffer size is returned in the appropriate parameter when this error occurs.
    buffer_too_small = high_bit | 5,

    /// There is no data pending upon return.
    not_ready = high_bit | 6,

    /// The physical device reported an error while attempting the operation.
    device_error = high_bit | 7,

    /// The device cannot be written to.
    write_protected = high_bit | 8,

    /// A resource has run out.
    out_of_resources = high_bit | 9,

    /// An inconstancy was detected on the file system causing the operating to fail.
    volume_corrupted = high_bit | 10,

    /// There is no more space on the file system.
    volume_full = high_bit | 11,

    /// The device does not contain any medium to perform the operation.
    no_media = high_bit | 12,

    /// The medium in the device has changed since the last access.
    media_changed = high_bit | 13,

    /// The item was not found.
    not_found = high_bit | 14,

    /// Access was denied.
    access_denied = high_bit | 15,

    /// The server was not found or did not respond to the request.
    no_response = high_bit | 16,

    /// A mapping to a device does not exist.
    no_mapping = high_bit | 17,

    /// The timeout time expired.
    timeout = high_bit | 18,

    /// The protocol has not been started.
    not_started = high_bit | 19,

    /// The protocol has already been started.
    already_started = high_bit | 20,

    /// The operation was aborted.
    aborted = high_bit | 21,

    /// An ICMP error occurred during the network operation.
    icmp_error = high_bit | 22,

    /// A TFTP error occurred during the network operation.
    tftp_error = high_bit | 23,

    /// A protocol error occurred during the network operation.
    protocol_error = high_bit | 24,

    /// The function encountered an internal version that was incompatible with a version requested by the caller.
    incompatible_version = high_bit | 25,

    /// The function was not performed due to a security violation.
    security_violation = high_bit | 26,

    /// A CRC error was detected.
    crc_error = high_bit | 27,

    /// Beginning or end of media was reached
    end_of_media = high_bit | 28,

    /// The end of the file was reached.
    end_of_file = high_bit | 31,

    /// The language specified was invalid.
    invalid_language = high_bit | 32,

    /// The security status of the data is unknown or compromised and the data must be updated or replaced to restore a valid security status.
    compromised_data = high_bit | 33,

    /// There is an address conflict address allocation
    ip_address_conflict = high_bit | 34,

    /// A HTTP error occurred during the network operation.
    http_error = high_bit | 35,

    network_unreachable = high_bit | 100,

    host_unreachable = high_bit | 101,

    protocol_unreachable = high_bit | 102,

    port_unreachable = high_bit | 103,

    connection_fin = high_bit | 104,

    connection_reset = high_bit | 105,

    connection_refused = high_bit | 106,

    /// The string contained one or more characters that the device could not render and were skipped.
    warn_unknown_glyph = 1,

    /// The handle was closed, but the file was not deleted.
    warn_delete_failure = 2,

    /// The handle was closed, but the data to the file was not flushed properly.
    warn_write_failure = 3,

    /// The resulting buffer was too small, and the data was truncated to the buffer size.
    warn_buffer_too_small = 4,

    /// The data has not been updated within the timeframe set by localpolicy for this type of data.
    warn_stale_data = 5,

    /// The resulting buffer contains UEFI-compliant file system.
    warn_file_system = 6,

    /// The operation will be processed across a system reset.
    warn_reset_required = 7,

    _,

    pub const Error = error{
        LoadError,
        InvalidParameter,
        Unsupported,
        BadBufferSize,
        BufferTooSmall,
        NotReady,
        DeviceError,
        WriteProtected,
        OutOfResources,
        VolumeCorrupted,
        VolumeFull,
        NoMedia,
        MediaChanged,
        NotFound,
        AccessDenied,
        NoResponse,
        NoMapping,
        Timeout,
        NotStarted,
        AlreadyStarted,
        Aborted,
        IcmpError,
        TftpError,
        ProtocolError,
        IncompatibleVersion,
        SecurityViolation,
        CrcError,
        EndOfMedia,
        EndOfFile,
        InvalidLanguage,
        CompromisedData,
        IpAddressConflict,
        HttpError,
        NetworkUnreachable,
        HostUnreachable,
        ProtocolUnreachable,
        PortUnreachable,
        ConnectionFin,
        ConnectionReset,
        ConnectionRefused,
    };

    pub fn err(self: Status) Error!void {
        switch (self) {
            .load_error => return error.LoadError,
            .invalid_parameter => return error.InvalidParameter,
            .unsupported => return error.Unsupported,
            .bad_buffer_size => return error.BadBufferSize,
            .buffer_too_small => return error.BufferTooSmall,
            .not_ready => return error.NotReady,
            .device_error => return error.DeviceError,
            .write_protected => return error.WriteProtected,
            .out_of_resources => return error.OutOfResources,
            .volume_corrupted => return error.VolumeCorrupted,
            .volume_full => return error.VolumeFull,
            .no_media => return error.NoMedia,
            .media_changed => return error.MediaChanged,
            .not_found => return error.NotFound,
            .access_denied => return error.AccessDenied,
            .no_response => return error.NoResponse,
            .no_mapping => return error.NoMapping,
            .timeout => return error.Timeout,
            .not_started => return error.NotStarted,
            .already_started => return error.AlreadyStarted,
            .aborted => return error.Aborted,
            .icmp_error => return error.IcmpError,
            .tftp_error => return error.TftpError,
            .protocol_error => return error.ProtocolError,
            .incompatible_version => return error.IncompatibleVersion,
            .security_violation => return error.SecurityViolation,
            .crc_error => return error.CrcError,
            .end_of_media => return error.EndOfMedia,
            .end_of_file => return error.EndOfFile,
            .invalid_language => return error.InvalidLanguage,
            .compromised_data => return error.CompromisedData,
            .ip_address_conflict => return error.IpAddressConflict,
            .http_error => return error.HttpError,
            .network_unreachable => return error.NetworkUnreachable,
            .host_unreachable => return error.HostUnreachable,
            .protocol_unreachable => return error.ProtocolUnreachable,
            .port_unreachable => return error.PortUnreachable,
            .connection_fin => return error.ConnectionFin,
            .connection_reset => return error.ConnectionReset,
            .connection_refused => return error.ConnectionRefused,
            // success, warn_*, _
            else => {},
        }
    }

    pub fn fromError(e: Error) Status {
        return switch (e) {
            Error.Aborted => .aborted,
            Error.AccessDenied => .access_denied,
            Error.AlreadyStarted => .already_started,
            Error.BadBufferSize => .bad_buffer_size,
            Error.BufferTooSmall => .buffer_too_small,
            Error.CompromisedData => .compromised_data,
            Error.ConnectionFin => .connection_fin,
            Error.ConnectionRefused => .connection_refused,
            Error.ConnectionReset => .connection_reset,
            Error.CrcError => .crc_error,
            Error.DeviceError => .device_error,
            Error.EndOfFile => .end_of_file,
            Error.EndOfMedia => .end_of_media,
            Error.HostUnreachable => .host_unreachable,
            Error.HttpError => .http_error,
            Error.IcmpError => .icmp_error,
            Error.IncompatibleVersion => .incompatible_version,
            Error.InvalidLanguage => .invalid_language,
            Error.InvalidParameter => .invalid_parameter,
            Error.IpAddressConflict => .ip_address_conflict,
            Error.LoadError => .load_error,
            Error.MediaChanged => .media_changed,
            Error.NetworkUnreachable => .network_unreachable,
            Error.NoMapping => .no_mapping,
            Error.NoMedia => .no_media,
            Error.NoResponse => .no_response,
            Error.NotFound => .not_found,
            Error.NotReady => .not_ready,
            Error.NotStarted => .not_started,
            Error.OutOfResources => .out_of_resources,
            Error.PortUnreachable => .port_unreachable,
            Error.ProtocolError => .protocol_error,
            Error.ProtocolUnreachable => .protocol_unreachable,
            Error.SecurityViolation => .security_violation,
            Error.TftpError => .tftp_error,
            Error.Timeout => .timeout,
            Error.Unsupported => .unsupported,
            Error.VolumeCorrupted => .volume_corrupted,
            Error.VolumeFull => .volume_full,
            Error.WriteProtected => .write_protected,
        };
    }
};

test "status" {
    var st: Status = .device_error;
    try testing.expectError(error.DeviceError, st.err());
    try testing.expectEqual(st, Status.fromError(st.err()));

    st = .success;
    try st.err();

    st = .warn_unknown_glyph;
    try st.err();
}



---
File: /std/os/uefi/tables.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Handle = uefi.Handle;
const Event = uefi.Event;
const Guid = uefi.Guid;
const cc = uefi.cc;
const math = std.math;
const assert = std.debug.assert;

pub const BootServices = @import("tables/boot_services.zig").BootServices;
pub const RuntimeServices = @import("tables/runtime_services.zig").RuntimeServices;
pub const ConfigurationTable = @import("tables/configuration_table.zig").ConfigurationTable;
pub const SystemTable = @import("tables/system_table.zig").SystemTable;
pub const TableHeader = @import("tables/table_header.zig").TableHeader;

pub const EventNotify = *const fn (event: Event, ctx: *anyopaque) callconv(cc) void;

pub const TimerDelay = enum(u32) {
    cancel,
    periodic,
    relative,
};

pub const MemoryType = enum(u32) {
    pub const Oem = math.IntFittingRange(
        0,
        @intFromEnum(MemoryType.oem_end) - @intFromEnum(MemoryType.oem_start),
    );
    pub const Vendor = math.IntFittingRange(
        0,
        @intFromEnum(MemoryType.vendor_end) - @intFromEnum(MemoryType.vendor_start),
    );

    /// can only be allocated using .allocate_any_pages mode unless you are explicitly targeting an interface that states otherwise
    reserved_memory_type,
    loader_code,
    loader_data,
    boot_services_code,
    boot_services_data,
    /// can only be allocated using .allocate_any_pages mode unless you are explicitly targeting an interface that states otherwise
    runtime_services_code,
    /// can only be allocated using .allocate_any_pages mode unless you are explicitly targeting an interface that states otherwise
    runtime_services_data,
    conventional_memory,
    unusable_memory,
    /// can only be allocated using .allocate_any_pages mode unless you are explicitly targeting an interface that states otherwise
    acpi_reclaim_memory,
    /// can only be allocated using .allocate_any_pages mode unless you are explicitly targeting an interface that states otherwise
    acpi_memory_nvs,
    memory_mapped_io,
    memory_mapped_io_port_space,
    pal_code,
    persistent_memory,
    unaccepted_memory,
    max_memory_type,
    invalid_start,
    invalid_end = 0x6FFFFFFF,
    /// MemoryType values in the range 0x70000000..0x7FFFFFFF are reserved for OEM use.
    oem_start = 0x70000000,
    oem_end = 0x7FFFFFFF,
    /// MemoryType values in the range 0x80000000..0xFFFFFFFF are reserved for use by UEFI
    /// OS loaders that are provided by operating system vendors.
    vendor_start = 0x80000000,
    vendor_end = 0xFFFFFFFF,
    _,

    pub fn fromOem(value: Oem) MemoryType {
        const oem_start = @intFromEnum(MemoryType.oem_start);
        return @enumFromInt(oem_start + value);
    }

    pub fn toOem(memtype: MemoryType) ?Oem {
        const as_int = @intFromEnum(memtype);
        const oem_start = @intFromEnum(MemoryType.oem_start);
        if (as_int < oem_start) return null;
        if (as_int > @intFromEnum(MemoryType.oem_end)) return null;
        return @truncate(as_int - oem_start);
    }

    pub fn fromVendor(value: Vendor) MemoryType {
        const vendor_start = @intFromEnum(MemoryType.vendor_start);
        return @enumFromInt(vendor_start + value);
    }

    pub fn toVendor(memtype: MemoryType) ?Vendor {
        const as_int = @intFromEnum(memtype);
        const vendor_start = @intFromEnum(MemoryType.vendor_start);
        if (as_int < @intFromEnum(MemoryType.vendor_end)) return null;
        if (as_int > @intFromEnum(MemoryType.vendor_end)) return null;
        return @truncate(as_int - vendor_start);
    }

    pub fn format(self: MemoryType, w: *std.Io.Writer) std.Io.Writer.Error!void {
        if (self.toOem()) |oemval|
            try w.print("OEM({X})", .{oemval})
        else if (self.toVendor()) |vendorval|
            try w.print("Vendor({X})", .{vendorval})
        else if (std.enums.tagName(MemoryType, self)) |name|
            try w.print("{s}", .{name})
        else
            try w.print("INVALID({X})", .{@intFromEnum(self)});
    }
};

pub const MemoryDescriptorAttribute = packed struct(u64) {
    uc: bool,
    wc: bool,
    wt: bool,
    wb: bool,
    uce: bool,
    _pad1: u7 = 0,
    wp: bool,
    rp: bool,
    xp: bool,
    nv: bool,
    more_reliable: bool,
    ro: bool,
    sp: bool,
    cpu_crypto: bool,
    _pad2: u43 = 0,
    memory_runtime: bool,
};

pub const MemoryMapKey = enum(usize) { _ };

pub const MemoryDescriptor = extern struct {
    type: MemoryType,
    physical_start: u64,
    virtual_start: u64,
    number_of_pages: u64,
    attribute: MemoryDescriptorAttribute,
};

pub const MemoryMapInfo = struct {
    key: MemoryMapKey,
    descriptor_size: usize,
    descriptor_version: u32,
    /// The number of descriptors in the map.
    len: usize,
};

pub const MemoryMapSlice = struct {
    info: MemoryMapInfo,
    ptr: [*]align(@alignOf(MemoryDescriptor)) u8,

    pub fn iterator(self: MemoryMapSlice) MemoryDescriptorIterator {
        return .{ .ctx = self };
    }

    pub fn get(self: MemoryMapSlice, index: usize) ?*MemoryDescriptor {
        if (index >= self.info.len) return null;
        return self.getUnchecked(index);
    }

    pub fn getUnchecked(self: MemoryMapSlice, index: usize) *MemoryDescriptor {
        const offset: usize = index * self.info.descriptor_size;
        return @ptrCast(@alignCast(self.ptr[offset..]));
    }
};

pub const MemoryDescriptorIterator = struct {
    ctx: MemoryMapSlice,
    index: usize = 0,

    pub fn next(self: *MemoryDescriptorIterator) ?*MemoryDescriptor {
        const md = self.ctx.get(self.index) orelse return null;
        self.index += 1;
        return md;
    }
};

pub const LocateSearchType = enum(u32) {
    all_handles,
    by_register_notify,
    by_protocol,
};

pub const LocateSearch = union(LocateSearchType) {
    all_handles,
    by_register_notify: uefi.EventRegistration,
    by_protocol: *const Guid,
};

pub const OpenProtocolAttributes = enum(u32) {
    pub const Bits = packed struct(u32) {
        by_handle_protocol: bool = false,
        get_protocol: bool = false,
        test_protocol: bool = false,
        by_child_controller: bool = false,
        by_driver: bool = false,
        exclusive: bool = false,
        reserved: u26 = 0,
    };

    by_handle_protocol = @bitCast(Bits{ .by_handle_protocol = true }),
    get_protocol = @bitCast(Bits{ .get_protocol = true }),
    test_protocol = @bitCast(Bits{ .test_protocol = true }),
    by_child_controller = @bitCast(Bits{ .by_child_controller = true }),
    by_driver = @bitCast(Bits{ .by_driver = true }),
    by_driver_exclusive = @bitCast(Bits{ .by_driver = true, .exclusive = true }),
    exclusive = @bitCast(Bits{ .exclusive = true }),
    _,

    pub fn fromBits(bits: Bits) OpenProtocolAttributes {
        return @bitCast(bits);
    }

    pub fn toBits(self: OpenProtocolAttributes) Bits {
        return @bitCast(self);
    }
};

pub const OpenProtocolArgs = union(OpenProtocolAttributes) {
    /// Used in the implementation of `handleProtocol`.
    by_handle_protocol: struct { agent: ?Handle = null, controller: ?Handle = null },
    /// Used by a driver to get a protocol interface from a handle. Care must be
    /// taken when using this open mode because the driver that opens a protocol
    /// interface in this manner will not be informed if the protocol interface
    /// is uninstalled or reinstalled. The caller is also not required to close
    /// the protocol interface with `closeProtocol`.
    get_protocol: struct { agent: ?Handle = null, controller: ?Handle = null },
    /// Used by a driver to test for the existence of a protocol interface on a
    /// handle. The caller only use the return status code. The caller is also
    /// not required to close the protocol interface with `closeProtocol`.
    test_protocol: struct { agent: ?Handle = null, controller: ?Handle = null },
    /// Used by bus drivers to show that a protocol interface is being used by one
    /// of the child controllers of a bus. This information is used by
    /// `BootServices.connectController` to recursively connect all child controllers
    /// and by `BootServices.disconnectController` to get the list of child
    /// controllers that a bus driver created.
    by_child_controller: struct { agent: Handle, controller: Handle },
    /// Used by a driver to gain access to a protocol interface. When this mode
    /// is used, the driver’s Stop() function will be called by
    /// `BootServices.disconnectController` if the protocol interface is reinstalled
    /// or uninstalled. Once a protocol interface is opened by a driver with this
    /// attribute, no other drivers will be allowed to open the same protocol interface
    /// with the `.by_driver` attribute.
    by_driver: struct { agent: Handle, controller: Handle },
    /// Used by a driver to gain exclusive access to a protocol interface. If any
    /// other drivers have the protocol interface opened with an attribute of
    /// `.by_driver`, then an attempt will be made to remove them with
    /// `BootServices.disconnectController`.
    by_driver_exclusive: struct { agent: Handle, controller: Handle },
    /// Used by applications to gain exclusive access to a protocol interface. If
    /// any drivers have the protocol interface opened with an attribute of
    /// `.by_driver`, then an attempt will be made to remove them by calling the
    /// driver’s Stop() function.
    exclusive: struct { agent: Handle, controller: ?Handle = null },
};

pub const ProtocolInformationEntry = extern struct {
    agent_handle: ?Handle,
    controller_handle: ?Handle,
    attributes: OpenProtocolAttributes,
    open_count: u32,
};

pub const InterfaceType = enum(u32) {
    native,
};

pub const AllocateLocation = union(AllocateType) {
    any,
    max_address: [*]align(4096) uefi.Page,
    address: [*]align(4096) uefi.Page,
};

pub const AllocateType = enum(u32) {
    any,
    max_address,
    address,
};

pub const PhysicalAddress = u64;

pub const CapsuleHeader = extern struct {
    capsule_guid: Guid,
    header_size: u32,
    flags: u32,
    capsule_image_size: u32,
};

pub const UefiCapsuleBlockDescriptor = extern struct {
    length: u64,
    address: extern union {
        data_block: PhysicalAddress,
        continuation_pointer: PhysicalAddress,
    },
};

pub const ResetType = enum(u32) {
    cold,
    warm,
    shutdown,
    platform_specific,
};

pub const global_variable = Guid{
    .time_low = 0x8be4df61,
    .time_mid = 0x93ca,
    .time_high_and_version = 0x11d2,
    .clock_seq_high_and_reserved = 0xaa,
    .clock_seq_low = 0x0d,
    .node = [_]u8{ 0x00, 0xe0, 0x98, 0x03, 0x2b, 0x8c },
};

test {
    std.testing.refAllDecls(@This());
}



---
File: /std/os/windows/crypt32.zig
---

const std = @import("../../std.zig");
const windows = std.os.windows;

const BOOL = windows.BOOL;
const DWORD = windows.DWORD;
const BYTE = windows.BYTE;
const LONG = windows.LONG;
const LPCSTR = windows.LPCSTR;
const LPCWSTR = windows.LPCWSTR;
const FILETIME = windows.FILETIME;
const HANDLE = windows.HANDLE;

// ref: um/wincrypt.h

pub const HCRYPTPROV_LEGACY = enum(usize) { NULL = 0 };

pub const CERT_INFO = *opaque {};

pub const CTL_USAGE = extern struct {
    cUsageIdentifier: DWORD,
    rgpszUsageIdentifier: [*]const LPCSTR,
};

pub const CERT_ENHKEY_USAGE = CTL_USAGE;

pub const ENCODING = enum(u16) {
    UNSPECIFIED = 0x0000,
    ASN = 0x0001,
    NDR = 0x0002,
    _,

    pub const TYPE = packed struct(DWORD) {
        CERT: ENCODING = .UNSPECIFIED,
        CMSG: ENCODING = .UNSPECIFIED,
    };
};

pub const HCERTSTORE = *opaque {};

pub const CERT_CONTEXT = extern struct {
    dwCertEncodingType: ENCODING.TYPE,
    pbCertEncoded: [*]BYTE,
    cbCertEncoded: DWORD,
    pCertInfo: CERT_INFO,
    hCertStore: HCERTSTORE,
};

pub const CERT_STORE = struct {
    pub const PROV = enum(usize) {
        MSG = 1,
        MEMORY = 2,
        FILE = 3,
        REG = 4,

        PKCS7 = 5,
        SERIALIZED = 6,
        FILENAME_A = 7,
        FILENAME_W = 8,
        SYSTEM_A = 9,
        SYSTEM_W = 10,

        COLLECTION = 11,
        SYSTEM_REGISTRY_A = 12,
        SYSTEM_REGISTRY_W = 13,
        PHYSICAL_W = 14,

        SMART_CARD_W = 15,

        LDAP_W = 16,
        PKCS12 = 17,

        /// LPCSTR
        _,

        pub fn fromString(str: LPCSTR) PROV {
            return @enumFromInt(@intFromPtr(str));
        }
    };

    pub const FLAG = packed struct(DWORD) {
        NO_CRYPT_RELEASE: bool = false,
        SET_LOCALIZED_NAME: bool = false,
        DEFER_CLOSE_UNTIL_LAST_FREE: bool = false,
        Reserved3: u1 = 0,
        DELETE: bool = false,
        UNSAFE_PHYSICAL: bool = false,
        SHARE_STORE: bool = false,
        SHARE_CONTEXT: bool = false,
        MANIFOLD: bool = false,
        ENUM_ARCHIVED: bool = false,
        UPDATE_KEYID: bool = false,
        BACKUP_RESTORE: bool = false,
        MAXIMUM_ALLOWED: bool = false,
        CREATE_NEW: bool = false,
        OPEN_EXISTING: bool = false,
        READONLY: bool = false,
        Reserved16: u16 = 0,
    };

    pub const ADD = enum(DWORD) {
        NEW = 1,
        USE_EXISTING = 2,
        REPLACE_EXISTING = 3,
        ALWAYS = 4,
        REPLACE_EXISTING_INHERIT_PROPERTIES = 5,
        REWER = 6,
        NEWER_INHERIT_PROPERTIES = 7,
        _,
    };
};

pub extern "crypt32" fn CertOpenStore(
    lpszStoreProvider: CERT_STORE.PROV,
    dwEncodingType: ENCODING.TYPE,
    hCryptProv: HCRYPTPROV_LEGACY,
    dwFlags: CERT_STORE.FLAG,
    pvPara: ?*const anyopaque,
) callconv(.winapi) ?HCERTSTORE;

pub const CERT_CLOSE_STORE_FLAG = packed struct(DWORD) {
    FORCE: bool = false,
    CHECK: bool = false,
    Reserved2: u30 = 0,
};

pub extern "crypt32" fn CertCloseStore(
    hCertStore: HCERTSTORE,
    dwFlags: CERT_CLOSE_STORE_FLAG,
) callconv(.winapi) BOOL;

pub extern "crypt32" fn CertEnumCertificatesInStore(
    hCertStore: HCERTSTORE,
    pPrevCertContext: ?*CERT_CONTEXT,
) callconv(.winapi) ?*CERT_CONTEXT;

pub extern "crypt32" fn CertFreeCertificateContext(
    pCertContext: ?*const CERT_CONTEXT,
) callconv(.winapi) BOOL;

pub extern "crypt32" fn CertAddEncodedCertificateToStore(
    hCertStore: ?HCERTSTORE,
    dwCertEncodingType: ENCODING.TYPE,
    pbCertEncoded: [*]const BYTE,
    cbCertEncoded: DWORD,
    dwAddDisposition: CERT_STORE.ADD,
    ppCertContext: ?*?*const CERT_CONTEXT,
) callconv(.winapi) BOOL;

pub extern "crypt32" fn CertOpenSystemStoreW(
    hProv: HCRYPTPROV_LEGACY,
    szSubsystemProtocol: LPCWSTR,
) callconv(.winapi) ?HCERTSTORE;

pub const HCERTCHAINENGINE = enum(usize) {
    CURRENT_USER = 0x0,
    LOCAL_MACHINE = 0x1,
    SERIAL_LOCAL_MACHINE = 0x2,
    /// HANDLE
    _,

    pub fn fromHandle(handle: HANDLE) HCERTCHAINENGINE {
        return @enumFromInt(@intFromPtr(handle));
    }
};

pub const CERT_CHAIN = packed struct(DWORD) {
    CACHE_END_CERT: bool = false,
    THREAD_STORE_SYNC: bool = false,
    CACHE_ONLY_URL_RETRIEVAL: bool = false,
    USE_LOCAL_MACHINE_STORE: bool = false,
    ENABLE_CACHE_AUTO_UPDATE: bool = false,
    ENABLE_SHARE_STORE: bool = false,
    Reserved6: u20 = 0,
    REVOCATION_CHECK_OCSP_CERT: bool = false,
    REVOCATION_ACCUMULATIVE_TIMEOUT: bool = false,
    REVOCATION_CHECK_END_CERT: bool = false,
    REVOCATION_CHECK_CHAIN: bool = false,
    REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT: bool = false,
    REVOCATION_CHECK_CACHE_ONLY: bool = false,

    pub const CONTEXT = opaque {};

    pub const USAGE_MATCH = extern struct {
        dwType: TYPE,
        Usage: CERT_ENHKEY_USAGE,

        pub const TYPE = enum(DWORD) { AND = 0x00000000, OR = 0x00000001, _ };
    };

    pub const PARA = extern struct {
        cbSize: DWORD = @sizeOf(PARA),
        RequestedUsage: USAGE_MATCH,
    };

    pub const POLICY = enum(usize) {
        BASE = 1,
        AUTHENTICODE = 2,
        AUTHENTICODE_TS = 3,
        SSL = 4,
        BASIC_CONSTRAINTS = 5,
        NT_AUTH = 6,
        MICROSOFT_ROOT = 7,
        EV = 8,
        SSL_F12 = 9,
        SSL_HPKP_HEADER = 10,
        THIRD_PARTY_ROOT = 11,
        SSL_KEY_PIN = 12,
        CT = 13,
        /// LPCSTR
        _,

        pub fn fromString(str: LPCSTR) POLICY {
            return @enumFromInt(@intFromPtr(str));
        }

        pub const PARA = extern struct {
            cbSize: DWORD = @sizeOf(POLICY.PARA),
            dwFlags: FLAG,
            pvExtraPolicyPara: ?*anyopaque,
        };

        pub const STATUS = extern struct {
            cbSize: DWORD = @sizeOf(STATUS),
            dwError: windows.Win32Error,
            lChainIndex: LONG,
            lElementIndex: LONG,
            pvExtraPolicyStatus: ?*anyopaque,
        };

        pub const FLAG = packed struct(DWORD) {
            IGNORE_NOT_TIME_VALID: bool = false,
            IGNORE_CTL_NOT_TIME_VALID: bool = false,
            IGNORE_NOT_TIME_NESTED: bool = false,
            IGNORE_INVALID_BASIC_CONSTRAINTS: bool = false,
            ALLOW_UNKNOWN_CA: bool = false,
            IGNORE_WRONG_USAGE: bool = false,
            IGNORE_INVALID_NAME: bool = false,
            IGNORE_INVALID_POLICY: bool = false,
            IGNORE_END_REV_UNKNOWN: bool = false,
            IGNORE_CTL_SIGNER_REV_UNKNOWN: bool = false,
            IGNORE_CA_REV_UNKNOWN: bool = false,
            IGNORE_ROOT_REV_UNKNOWN: bool = false,
            IGNORE_PEER_TRUST: bool = false,
            IGNORE_NOT_SUPPORTED_CRITICAL_EXT: bool = false,
            TRUST_TESTROOT: bool = false,
            ALLOW_TESTROOT: bool = false,
            Reserved16: u11 = 0,
            IGNORE_WEAK_SIGNATURE: bool = false,
            Reserved28: u4 = 0,
        };
    };
};

pub const HTTPSPolicyCallbackData = extern struct {
    cbSize: DWORD = @sizeOf(HTTPSPolicyCallbackData),
    dwAuthType: AUTHTYPE,
    fdwChecks: DWORD = 0,
    pwszServerName: ?LPCWSTR = null,

    pub const AUTHTYPE = enum(DWORD) { CLIENT = 1, SERVER = 2, _ };
};

pub extern "crypt32" fn CertGetCertificateChain(
    hChainEngine: HCERTCHAINENGINE,
    pCertContext: *const CERT_CONTEXT,
    pTime: ?*const FILETIME,
    hAdditionalStore: ?HCERTSTORE,
    pChainPara: *const CERT_CHAIN.PARA,
    dwFlags: CERT_CHAIN,
    pvReserved: ?*const anyopaque,
    ppChainContext: **const CERT_CHAIN.CONTEXT,
) callconv(.winapi) BOOL;

pub extern "crypt32" fn CertFreeCertificateChain(
    pChainContext: *const CERT_CHAIN.CONTEXT,
) callconv(.winapi) void;

pub extern "crypt32" fn CertVerifyCertificateChainPolicy(
    pszPolicyOID: CERT_CHAIN.POLICY,
    pChainContext: *const CERT_CHAIN.CONTEXT,
    pPolicyPara: *const CERT_CHAIN.POLICY.PARA,
    pPolicyStatus: *CERT_CHAIN.POLICY.STATUS,
) callconv(.winapi) BOOL;



---
File: /std/os/windows/kernel32.zig
---

const std = @import("../../std.zig");
const windows = std.os.windows;

const BOOL = windows.BOOL;
const DWORD = windows.DWORD;
const HANDLE = windows.HANDLE;
const LPCWSTR = windows.LPCWSTR;
const LPVOID = windows.LPVOID;
const LPWSTR = windows.LPWSTR;
const PROCESS = windows.PROCESS;
const THREAD_START_ROUTINE = windows.THREAD_START_ROUTINE;
const SECURITY_ATTRIBUTES = windows.SECURITY_ATTRIBUTES;
const SIZE_T = windows.SIZE_T;
const STARTUPINFOW = windows.STARTUPINFOW;

pub extern "kernel32" fn CreateProcessW(
    lpApplicationName: ?LPCWSTR,
    lpCommandLine: ?LPWSTR,
    lpProcessAttributes: ?*SECURITY_ATTRIBUTES,
    lpThreadAttributes: ?*SECURITY_ATTRIBUTES,
    bInheritHandles: BOOL,
    dwCreationFlags: windows.CreateProcessFlags,
    lpEnvironment: ?[*:0]const u16,
    lpCurrentDirectory: ?LPCWSTR,
    lpStartupInfo: *STARTUPINFOW,
    lpProcessInformation: *PROCESS.INFORMATION,
) callconv(.winapi) BOOL;



---
File: /std/os/windows/lang.zig
---

pub const NEUTRAL = 0x00;
pub const INVARIANT = 0x7f;
pub const AFRIKAANS = 0x36;
pub const ALBANIAN = 0x1c;
pub const ALSATIAN = 0x84;
pub const AMHARIC = 0x5e;
pub const ARABIC = 0x01;
pub const ARMENIAN = 0x2b;
pub const ASSAMESE = 0x4d;
pub const AZERI = 0x2c;
pub const AZERBAIJANI = 0x2c;
pub const BANGLA = 0x45;
pub const BASHKIR = 0x6d;
pub const BASQUE = 0x2d;
pub const BELARUSIAN = 0x23;
pub const BENGALI = 0x45;
pub const BRETON = 0x7e;
pub const BOSNIAN = 0x1a;
pub const BOSNIAN_NEUTRAL = 0x781a;
pub const BULGARIAN = 0x02;
pub const CATALAN = 0x03;
pub const CENTRAL_KURDISH = 0x92;
pub const CHEROKEE = 0x5c;
pub const CHINESE = 0x04;
pub const CHINESE_SIMPLIFIED = 0x04;
pub const CHINESE_TRADITIONAL = 0x7c04;
pub const CORSICAN = 0x83;
pub const CROATIAN = 0x1a;
pub const CZECH = 0x05;
pub const DANISH = 0x06;
pub const DARI = 0x8c;
pub const DIVEHI = 0x65;
pub const DUTCH = 0x13;
pub const ENGLISH = 0x09;
pub const ESTONIAN = 0x25;
pub const FAEROESE = 0x38;
pub const FARSI = 0x29;
pub const FILIPINO = 0x64;
pub const FINNISH = 0x0b;
pub const FRENCH = 0x0c;
pub const FRISIAN = 0x62;
pub const FULAH = 0x67;
pub const GALICIAN = 0x56;
pub const GEORGIAN = 0x37;
pub const GERMAN = 0x07;
pub const GREEK = 0x08;
pub const GREENLANDIC = 0x6f;
pub const GUJARATI = 0x47;
pub const HAUSA = 0x68;
pub const HAWAIIAN = 0x75;
pub const HEBREW = 0x0d;
pub const HINDI = 0x39;
pub const HUNGARIAN = 0x0e;
pub const ICELANDIC = 0x0f;
pub const IGBO = 0x70;
pub const INDONESIAN = 0x21;
pub const INUKTITUT = 0x5d;
pub const IRISH = 0x3c;
pub const ITALIAN = 0x10;
pub const JAPANESE = 0x11;
pub const KANNADA = 0x4b;
pub const KASHMIRI = 0x60;
pub const KAZAK = 0x3f;
pub const KHMER = 0x53;
pub const KICHE = 0x86;
pub const KINYARWANDA = 0x87;
pub const KONKANI = 0x57;
pub const KOREAN = 0x12;
pub const KYRGYZ = 0x40;
pub const LAO = 0x54;
pub const LATVIAN = 0x26;
pub const LITHUANIAN = 0x27;
pub const LOWER_SORBIAN = 0x2e;
pub const LUXEMBOURGISH = 0x6e;
pub const MACEDONIAN = 0x2f;
pub const MALAY = 0x3e;
pub const MALAYALAM = 0x4c;
pub const MALTESE = 0x3a;
pub const MANIPURI = 0x58;
pub const MAORI = 0x81;
pub const MAPUDUNGUN = 0x7a;
pub const MARATHI = 0x4e;
pub const MOHAWK = 0x7c;
pub const MONGOLIAN = 0x50;
pub const NEPALI = 0x61;
pub const NORWEGIAN = 0x14;
pub const OCCITAN = 0x82;
pub const ODIA = 0x48;
pub const ORIYA = 0x48;
pub const PASHTO = 0x63;
pub const PERSIAN = 0x29;
pub const POLISH = 0x15;
pub const PORTUGUESE = 0x16;
pub const PULAR = 0x67;
pub const PUNJABI = 0x46;
pub const QUECHUA = 0x6b;
pub const ROMANIAN = 0x18;
pub const ROMANSH = 0x17;
pub const RUSSIAN = 0x19;
pub const SAKHA = 0x85;
pub const SAMI = 0x3b;
pub const SANSKRIT = 0x4f;
pub const SCOTTISH_GAELIC = 0x91;
pub const SERBIAN = 0x1a;
pub const SERBIAN_NEUTRAL = 0x7c1a;
pub const SINDHI = 0x59;
pub const SINHALESE = 0x5b;
pub const SLOVAK = 0x1b;
pub const SLOVENIAN = 0x24;
pub const SOTHO = 0x6c;
pub const SPANISH = 0x0a;
pub const SWAHILI = 0x41;
pub const SWEDISH = 0x1d;
pub const SYRIAC = 0x5a;
pub const TAJIK = 0x28;
pub const TAMAZIGHT = 0x5f;
pub const TAMIL = 0x49;
pub const TATAR = 0x44;
pub const TELUGU = 0x4a;
pub const THAI = 0x1e;
pub const TIBETAN = 0x51;
pub const TIGRIGNA = 0x73;
pub const TIGRINYA = 0x73;
pub const TSWANA = 0x32;
pub const TURKISH = 0x1f;
pub const TURKMEN = 0x42;
pub const UIGHUR = 0x80;
pub const UKRAINIAN = 0x22;
pub const UPPER_SORBIAN = 0x2e;
pub const URDU = 0x20;
pub const UZBEK = 0x43;
pub const VALENCIAN = 0x03;
pub const VIETNAMESE = 0x2a;
pub const WELSH = 0x52;
pub const WOLOF = 0x88;
pub const XHOSA = 0x34;
pub const YAKUT = 0x85;
pub const YI = 0x78;
pub const YORUBA = 0x6a;
pub const ZULU = 0x35;



---
File: /std/os/windows/nls.zig
---

//! Implementations of functionality related to National Language Support
//! on Windows.

const builtin = @import("builtin");
const std = @import("../../std.zig");

/// This corresponds to the uppercase table within the locale-independent
/// l_intl.nls data (found at system32\l_intl.nls).
/// - In l_intl.nls, this data starts at offset 0x04.
/// - In the PEB, this data starts at index [2] of peb.UnicodeCaseTableData when
///   it is casted to `[*]u16`.
///
/// Note: This data has not changed since Windows 8.1, and has become out-of-sync with
///       the Unicode standard.
const uppercase_table = [2544]u16{
    272,   288,   304,   320,   336,   352,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,
    256,   256,   256,   256,   256,   368,   384,   400,   256,   416,   256,   256,   432,   256,   256,   256,   256,   256,   256,   256,   448,   464,   256,   256,
    256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,
    256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,
    256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,
    256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,
    256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   480,   496,
    256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,
    256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,
    256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,
    256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   256,   512,   528,   528,   528,   528,   528,   528,   528,   528,
    528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   544,   560,   528,   528,   528,   576,   528,   528,   592,   608,
    624,   640,   656,   672,   688,   704,   720,   736,   752,   768,   784,   800,   816,   832,   848,   864,   880,   896,   912,   928,   944,   960,   976,   992,
    1008,  1024,  528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   1040,  528,   528,   1056,  528,   528,   1072,  1088,  1104,  1120,  1136,  1152,
    528,   528,   528,   1168,  1184,  1200,  1216,  1232,  1248,  1264,  1280,  1296,  1312,  1328,  1344,  1360,  1376,  1392,  1408,  528,   528,   528,   1424,  1440,
    1456,  528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   1472,  528,   528,   528,   528,   528,   528,   528,   528,
    1488,  1504,  1520,  1536,  1552,  1568,  1584,  1600,  1616,  1632,  1648,  1664,  1680,  1696,  1712,  1728,  1744,  1760,  1776,  1792,  1808,  1824,  1840,  1856,
    1872,  1888,  1904,  1920,  1936,  1952,  1968,  1984,  528,   528,   528,   528,   2000,  528,   528,   2016,  2032,  528,   528,   528,   528,   528,   528,   528,
    528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   2048,  2064,  528,   528,   528,   528,   2080,  2096,  2112,  2128,  2144,
    2160,  2176,  2192,  2208,  2224,  2240,  2256,  528,   2272,  2288,  2304,  528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,
    528,   528,   528,   528,   2320,  2336,  2352,  528,   2368,  2384,  528,   528,   528,   528,   528,   528,   528,   528,   2400,  2416,  2432,  2448,  2464,  2480,
    2496,  528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   528,   2512,  2528,  528,   528,   528,   528,   528,   528,   528,   528,   528,   528,
    0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     65504, 65504, 65504, 65504, 65504, 65504, 65504,
    65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 0,     0,     0,     0,     0,
    0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504,
    65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 0,     65504, 65504, 65504, 65504, 65504, 65504, 65504, 121,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,
    65535, 0,     0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     0,     65535, 0,     65535, 0,     65535, 0,     195,   0,     0,     65535, 0,     65535, 0,     0,     65535, 0,     0,     0,     65535, 0,     0,     0,
    0,     0,     65535, 0,     0,     97,    0,     0,     0,     65535, 163,   0,     0,     0,     130,   0,     0,     65535, 0,     65535, 0,     65535, 0,     0,
    65535, 0,     0,     0,     0,     65535, 0,     0,     65535, 0,     0,     0,     65535, 0,     65535, 0,     0,     65535, 0,     0,     0,     65535, 0,     56,
    0,     0,     0,     0,     0,     0,     65534, 0,     0,     65534, 0,     0,     65534, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,
    65535, 0,     65535, 0,     65535, 65457, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     0,     0,     65534, 0,     65535, 0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     0,     0,     0,
    0,     0,     0,     0,     65535, 0,     0,     0,     0,     0,     65535, 0,     0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    10783, 10780, 0,     65326, 65330, 0,     65331, 65331, 0,     65334, 0,     65333, 0,     0,     0,     0,     65331, 0,     0,     65329, 0,     0,     0,     0,
    65327, 65325, 0,     10743, 0,     0,     0,     65325, 0,     10749, 65323, 0,     0,     65322, 0,     0,     0,     0,     0,     0,     0,     10727, 0,     0,
    65318, 0,     0,     65318, 0,     0,     0,     0,     65318, 65467, 65319, 65319, 65465, 0,     0,     0,     0,     0,     65317, 0,     0,     0,     0,     0,
    0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,
    0,     65535, 0,     65535, 0,     0,     0,     65535, 0,     0,     0,     130,   130,   130,   0,     0,     0,     0,     0,     0,     0,     0,     0,     0,
    0,     0,     0,     0,     65498, 65499, 65499, 65499, 0,     65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504,
    65504, 65504, 0,     65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65472, 65473, 65473, 0,     0,     0,     0,     0,     0,     0,     0,     65528,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     0,     7,     0,     0,     0,     0,     0,     65535, 0,     0,     65535, 0,     0,     0,     0,     65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504,
    65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504, 65504,
    65456, 65456, 65456, 65456, 65456, 65456, 65456, 65456, 65456, 65456, 65456, 65456, 65456, 65456, 65456, 65456, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     0,     0,     0,     0,     0,     0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     0,     65535, 0,     65535, 0,     65535, 0,
    65535, 0,     65535, 0,     65535, 0,     65535, 65521, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     0,     0,     0,
    0,     0,     0,     0,     0,     0,     0,     0,     0,     65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488,
    65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 65488, 0,
    0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     35332, 0,     0,     0,     3814,  0,     0,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535,
    0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 0,     65535, 8,     8,     8,     8,     8,     8,     8,     8,
    0,     0,     0,     0,     0,     0,     0,     0,     8,     8,     8,     8,     8,     8,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,
    8,     8,     8,     8,     8,     8,     8,     8,     0,     0,     0,     0,     0,     0,     0,     0,     8,     8,     8,     8,     8,     8,     8,     8,
    0,     0,     0,     0,     0,     0,     0,     0,     8,     8,     8,     8,     8,     8,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,
    0,     8,     0,     8,     0,     8,     0,     8,     0,     0,     0,     0,     0,     0,     0,     0,     8,     8,     8,     8,     8,     8,     8,     8,
    0,     0,     0,     0,     0,     0,     0,     0,     74,    74,    86,    86,    86,    86,    100,   100,   128,   128,   112,   112,   126,   126,   0,     0,
    8,     8,     8,     8,     8,     8,     8,     8,     0,     0,     0,     0,     0,     0,     0,     0,     8,     8,     8,     8,     8,     8,     8,     8,
    0,     0,     0,     0,     0,     0,     0,     0,     8,     8,     8,     8,     8,     8,     8,     8,     0,     0,     0,     0,     0,     0,     0,     0,
    8,     8,     0,     9,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     9,     0,     0,     0,     0,
    0,     0,     0,     0,     0,     0,     0,     0,     8,     8,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,
    8,     8,     0,     0,     0,     7,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     9,     0,     0,     0,     0,
    0,     0,     0,  
```
