```
           read_write == .read,
            offset,
            buffer.len,
            buffer.ptr,
        )) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Reads the current interrupt status and recycled transmit buffer status from a network interface.
    pub fn getStatus(
        self: *SimpleNetwork,
        interrupt_status: ?*InterruptStatus,
        recycled_tx_buf: ?*?[*]u8,
    ) GetStatusError!void {
        switch (self._get_status(self, interrupt_status, recycled_tx_buf)) {
            .success => {},
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Places a packet in the transmit queue of a network interface.
    pub fn transmit(
        self: *SimpleNetwork,
        header_size: usize,
        buffer: []const u8,
        src_addr: ?*const MacAddress,
        dest_addr: ?*const MacAddress,
        protocol: ?*const u16,
    ) TransmitError!void {
        switch (self._transmit(
            self,
            header_size,
            buffer.len,
            buffer.ptr,
            src_addr,
            dest_addr,
            protocol,
        )) {
            .success => {},
            .not_started => return error.NotStarted,
            .not_ready => return error.NotReady,
            .buffer_too_small => return error.BufferTooSmall,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Receives a packet from a network interface.
    pub fn receive(self: *SimpleNetwork, buffer: []u8) ReceiveError!Packet {
        var packet: Packet = undefined;
        packet.buffer = buffer;

        switch (self._receive(
            self,
            &packet.header_size,
            &packet.buffer.len,
            packet.buffer.ptr,
            &packet.src_addr,
            &packet.dst_addr,
            &packet.protocol,
        )) {
            .success => return packet,
            .not_started => return error.NotStarted,
            .not_ready => return error.NotReady,
            .buffer_too_small => return error.BufferTooSmall,
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0xa19832b9,
        .time_mid = 0xac25,
        .time_high_and_version = 0x11d3,
        .clock_seq_high_and_reserved = 0x9a,
        .clock_seq_low = 0x2d,
        .node = [_]u8{ 0x00, 0x90, 0x27, 0x3f, 0xc1, 0x4d },
    };

    pub const NvDataOperation = enum {
        read,
        write,
    };

    pub const MacAddress = [32]u8;

    pub const Mode = extern struct {
        state: State,
        hw_address_size: u32,
        media_header_size: u32,
        max_packet_size: u32,
        nvram_size: u32,
        nvram_access_size: u32,
        receive_filter_mask: ReceiveFilter,
        receive_filter_setting: ReceiveFilter,
        max_mcast_filter_count: u32,
        mcast_filter_count: u32,
        mcast_filter: [16]MacAddress,
        current_address: MacAddress,
        broadcast_address: MacAddress,
        permanent_address: MacAddress,
        if_type: u8,
        mac_address_changeable: bool,
        multiple_tx_supported: bool,
        media_present_supported: bool,
        media_present: bool,
    };

    pub const ReceiveFilter = packed struct(u32) {
        receive_unicast: bool,
        receive_multicast: bool,
        receive_broadcast: bool,
        receive_promiscuous: bool,
        receive_promiscuous_multicast: bool,
        _pad: u27 = 0,
    };

    pub const State = enum(u32) {
        stopped,
        started,
        initialized,
    };

    pub const Statistics = extern struct {
        rx_total_frames: u64,
        rx_good_frames: u64,
        rx_undersize_frames: u64,
        rx_oversize_frames: u64,
        rx_dropped_frames: u64,
        rx_unicast_frames: u64,
        rx_broadcast_frames: u64,
        rx_multicast_frames: u64,
        rx_crc_error_frames: u64,
        rx_total_bytes: u64,
        tx_total_frames: u64,
        tx_good_frames: u64,
        tx_undersize_frames: u64,
        tx_oversize_frames: u64,
        tx_dropped_frames: u64,
        tx_unicast_frames: u64,
        tx_broadcast_frames: u64,
        tx_multicast_frames: u64,
        tx_crc_error_frames: u64,
        tx_total_bytes: u64,
        collisions: u64,
        unsupported_protocol: u64,
        rx_duplicated_frames: u64,
        rx_decryptError_frames: u64,
        tx_error_frames: u64,
        tx_retry_frames: u64,
    };

    pub const InterruptStatus = packed struct(u32) {
        receive_interrupt: bool,
        transmit_interrupt: bool,
        command_interrupt: bool,
        software_interrupt: bool,
        _pad: u28 = 0,
    };

    pub const Packet = struct {
        header_size: usize,
        buffer: []u8,
        src_addr: MacAddress,
        dst_addr: MacAddress,
        protocol: u16,
    };
};



---
File: /std/os/uefi/protocol/simple_pointer.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Event = uefi.Event;
const Guid = uefi.Guid;
const Status = uefi.Status;
const cc = uefi.cc;

/// Protocol for mice.
pub const SimplePointer = struct {
    _reset: *const fn (*SimplePointer, bool) callconv(cc) Status,
    _get_state: *const fn (*const SimplePointer, *State) callconv(cc) Status,
    wait_for_input: Event,
    mode: *Mode,

    pub const ResetError = uefi.UnexpectedError || error{DeviceError};
    pub const GetStateError = uefi.UnexpectedError || error{
        NotReady,
        DeviceError,
    };

    /// Resets the pointer device hardware.
    pub fn reset(self: *SimplePointer, verify: bool) ResetError!void {
        switch (self._reset(self, verify)) {
            .success => {},
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Retrieves the current state of a pointer device.
    pub fn getState(self: *const SimplePointer) GetStateError!State {
        var state: State = undefined;
        switch (self._get_state(self, &state)) {
            .success => return state,
            .not_ready => return error.NotReady,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x31878c87,
        .time_mid = 0x0b75,
        .time_high_and_version = 0x11d5,
        .clock_seq_high_and_reserved = 0x9a,
        .clock_seq_low = 0x4f,
        .node = [_]u8{ 0x00, 0x90, 0x27, 0x3f, 0xc1, 0x4d },
    };

    pub const Mode = struct {
        resolution_x: u64,
        resolution_y: u64,
        resolution_z: u64,
        left_button: bool,
        right_button: bool,
    };

    pub const State = struct {
        relative_movement_x: i32,
        relative_movement_y: i32,
        relative_movement_z: i32,
        left_button: bool,
        right_button: bool,
    };
};



---
File: /std/os/uefi/protocol/simple_text_input_ex.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Event = uefi.Event;
const Guid = uefi.Guid;
const Status = uefi.Status;
const cc = uefi.cc;

/// Character input devices, e.g. Keyboard
pub const SimpleTextInputEx = extern struct {
    _reset: *const fn (*SimpleTextInputEx, bool) callconv(cc) Status,
    _read_key_stroke_ex: *const fn (*SimpleTextInputEx, *Key) callconv(cc) Status,
    wait_for_key_ex: Event,
    _set_state: *const fn (*SimpleTextInputEx, *const u8) callconv(cc) Status,
    _register_key_notify: *const fn (*SimpleTextInputEx, *const Key, *const fn (*const Key) callconv(cc) Status, **anyopaque) callconv(cc) Status,
    _unregister_key_notify: *const fn (*SimpleTextInputEx, *const anyopaque) callconv(cc) Status,

    pub const ResetError = uefi.UnexpectedError || error{DeviceError};
    pub const ReadKeyStrokeError = uefi.UnexpectedError || error{
        NotReady,
        DeviceError,
        Unsupported,
    };
    pub const SetStateError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };
    pub const RegisterKeyNotifyError = uefi.UnexpectedError || error{OutOfResources};
    pub const UnregisterKeyNotifyError = uefi.UnexpectedError || error{InvalidParameter};

    /// Resets the input device hardware.
    pub fn reset(self: *SimpleTextInputEx, verify: bool) ResetError!void {
        switch (self._reset(self, verify)) {
            .success => {},
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Reads the next keystroke from the input device.
    pub fn readKeyStroke(self: *SimpleTextInputEx) ReadKeyStrokeError!Key {
        var key: Key = undefined;
        switch (self._read_key_stroke_ex(self, &key)) {
            .success => return key,
            .not_ready => return error.NotReady,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Set certain state for the input device.
    pub fn setState(self: *SimpleTextInputEx, state: *const Key.State.Toggle) SetStateError!void {
        switch (self._set_state(self, @ptrCast(state))) {
            .success => {},
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Register a notification function for a particular keystroke for the input device.
    pub fn registerKeyNotify(
        self: *SimpleTextInputEx,
        key_data: *const Key,
        notify: *const fn (*const Key) callconv(cc) Status,
    ) RegisterKeyNotifyError!uefi.Handle {
        var handle: uefi.Handle = undefined;
        switch (self._register_key_notify(self, key_data, notify, &handle)) {
            .success => return handle,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Remove the notification that was previously registered.
    pub fn unregisterKeyNotify(
        self: *SimpleTextInputEx,
        handle: uefi.Handle,
    ) UnregisterKeyNotifyError!void {
        switch (self._unregister_key_notify(self, handle)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0xdd9e7534,
        .time_mid = 0x7762,
        .time_high_and_version = 0x4698,
        .clock_seq_high_and_reserved = 0x8c,
        .clock_seq_low = 0x14,
        .node = [_]u8{ 0xf5, 0x85, 0x17, 0xa6, 0x25, 0xaa },
    };

    pub const Key = extern struct {
        input: Input,
        state: State,

        pub const State = extern struct {
            shift: Shift,
            toggle: Toggle,

            pub const Shift = packed struct(u32) {
                right_shift_pressed: bool,
                left_shift_pressed: bool,
                right_control_pressed: bool,
                left_control_pressed: bool,
                right_alt_pressed: bool,
                left_alt_pressed: bool,
                right_logo_pressed: bool,
                left_logo_pressed: bool,
                menu_key_pressed: bool,
                sys_req_pressed: bool,
                _pad: u21 = 0,
                shift_state_valid: bool,
            };

            pub const Toggle = packed struct(u8) {
                scroll_lock_active: bool,
                num_lock_active: bool,
                caps_lock_active: bool,
                _pad: u3 = 0,
                key_state_exposed: bool,
                toggle_state_valid: bool,
            };
        };

        pub const Input = extern struct {
            scan_code: u16,
            unicode_char: u16,
        };
    };
};



---
File: /std/os/uefi/protocol/simple_text_input.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Event = uefi.Event;
const Guid = uefi.Guid;
const Status = uefi.Status;
const cc = uefi.cc;

/// Character input devices, e.g. Keyboard
pub const SimpleTextInput = extern struct {
    _reset: *const fn (*SimpleTextInput, bool) callconv(cc) Status,
    _read_key_stroke: *const fn (*SimpleTextInput, *Key.Input) callconv(cc) Status,
    wait_for_key: Event,

    pub const ResetError = uefi.UnexpectedError || error{DeviceError};
    pub const ReadKeyStrokeError = uefi.UnexpectedError || error{
        NotReady,
        DeviceError,
        Unsupported,
    };

    /// Resets the input device hardware.
    pub fn reset(self: *SimpleTextInput, verify: bool) ResetError!void {
        switch (self._reset(self, verify)) {
            .success => {},
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Reads the next keystroke from the input device.
    pub fn readKeyStroke(self: *SimpleTextInput) ReadKeyStrokeError!Key.Input {
        var key: Key.Input = undefined;
        switch (self._read_key_stroke(self, &key)) {
            .success => return key,
            .not_ready => return error.NotReady,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x387477c1,
        .time_mid = 0x69c7,
        .time_high_and_version = 0x11d2,
        .clock_seq_high_and_reserved = 0x8e,
        .clock_seq_low = 0x39,
        .node = [_]u8{ 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b },
    };

    pub const Key = uefi.protocol.SimpleTextInputEx.Key;
};



---
File: /std/os/uefi/protocol/simple_text_output.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Status = uefi.Status;
const cc = uefi.cc;

/// Character output devices
pub const SimpleTextOutput = extern struct {
    _reset: *const fn (*SimpleTextOutput, bool) callconv(cc) Status,
    _output_string: *const fn (*SimpleTextOutput, [*:0]const u16) callconv(cc) Status,
    _test_string: *const fn (*const SimpleTextOutput, [*:0]const u16) callconv(cc) Status,
    _query_mode: *const fn (*const SimpleTextOutput, usize, *usize, *usize) callconv(cc) Status,
    _set_mode: *const fn (*SimpleTextOutput, usize) callconv(cc) Status,
    _set_attribute: *const fn (*SimpleTextOutput, usize) callconv(cc) Status,
    _clear_screen: *const fn (*SimpleTextOutput) callconv(cc) Status,
    _set_cursor_position: *const fn (*SimpleTextOutput, usize, usize) callconv(cc) Status,
    _enable_cursor: *const fn (*SimpleTextOutput, bool) callconv(cc) Status,
    mode: *Mode,

    pub const ResetError = uefi.UnexpectedError || error{DeviceError};
    pub const OutputStringError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };
    pub const QueryModeError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };
    pub const SetModeError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };
    pub const SetAttributeError = uefi.UnexpectedError || error{DeviceError};
    pub const ClearScreenError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };
    pub const SetCursorPositionError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };
    pub const EnableCursorError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };

    /// Resets the text output device hardware.
    pub fn reset(self: *SimpleTextOutput, verify: bool) ResetError!void {
        switch (self._reset(self, verify)) {
            .success => {},
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Writes a string to the output device.
    ///
    /// Returns `true` if the string was successfully written, `false` if an unknown glyph was encountered.
    pub fn outputString(self: *SimpleTextOutput, msg: [*:0]const u16) OutputStringError!bool {
        switch (self._output_string(self, msg)) {
            .success => return true,
            .warn_unknown_glyph => return false,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Verifies that all characters in a string can be output to the target device.
    pub fn testString(self: *const SimpleTextOutput, msg: [*:0]const u16) uefi.UnexpectedError!bool {
        switch (self._test_string(self, msg)) {
            .success => return true,
            .unsupported => return false,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Returns information for an available text mode that the output device(s) supports.
    pub fn queryMode(self: *const SimpleTextOutput, mode_number: usize) QueryModeError!Geometry {
        var geo: Geometry = undefined;
        switch (self._query_mode(self, mode_number, &geo.columns, &geo.rows)) {
            .success => return geo,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Sets the output device(s) to a specified mode.
    pub fn setMode(self: *SimpleTextOutput, mode_number: usize) SetModeError!void {
        switch (self._set_mode(self, mode_number)) {
            .success => {},
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Sets the background and foreground colors for the outputString() and clearScreen() functions.
    pub fn setAttribute(self: *SimpleTextOutput, attribute: Attribute) SetAttributeError!void {
        const attr_as_num: u8 = @bitCast(attribute);
        switch (self._set_attribute(self, @intCast(attr_as_num))) {
            .success => {},
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Clears the output device(s) display to the currently selected background color.
    pub fn clearScreen(self: *SimpleTextOutput) ClearScreenError!void {
        switch (self._clear_screen(self)) {
            .success => {},
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Sets the current coordinates of the cursor position.
    pub fn setCursorPosition(
        self: *SimpleTextOutput,
        column: usize,
        row: usize,
    ) SetCursorPositionError!void {
        switch (self._set_cursor_position(self, column, row)) {
            .success => {},
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Makes the cursor visible or invisible.
    pub fn enableCursor(self: *SimpleTextOutput, visible: bool) EnableCursorError!void {
        switch (self._enable_cursor(self, visible)) {
            .success => {},
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = Guid{
        .time_low = 0x387477c2,
        .time_mid = 0x69c7,
        .time_high_and_version = 0x11d2,
        .clock_seq_high_and_reserved = 0x8e,
        .clock_seq_low = 0x39,
        .node = [_]u8{ 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b },
    };
    pub const boxdraw_horizontal: u16 = 0x2500;
    pub const boxdraw_vertical: u16 = 0x2502;
    pub const boxdraw_down_right: u16 = 0x250c;
    pub const boxdraw_down_left: u16 = 0x2510;
    pub const boxdraw_up_right: u16 = 0x2514;
    pub const boxdraw_up_left: u16 = 0x2518;
    pub const boxdraw_vertical_right: u16 = 0x251c;
    pub const boxdraw_vertical_left: u16 = 0x2524;
    pub const boxdraw_down_horizontal: u16 = 0x252c;
    pub const boxdraw_up_horizontal: u16 = 0x2534;
    pub const boxdraw_vertical_horizontal: u16 = 0x253c;
    pub const boxdraw_double_horizontal: u16 = 0x2550;
    pub const boxdraw_double_vertical: u16 = 0x2551;
    pub const boxdraw_down_right_double: u16 = 0x2552;
    pub const boxdraw_down_double_right: u16 = 0x2553;
    pub const boxdraw_double_down_right: u16 = 0x2554;
    pub const boxdraw_down_left_double: u16 = 0x2555;
    pub const boxdraw_down_double_left: u16 = 0x2556;
    pub const boxdraw_double_down_left: u16 = 0x2557;
    pub const boxdraw_up_right_double: u16 = 0x2558;
    pub const boxdraw_up_double_right: u16 = 0x2559;
    pub const boxdraw_double_up_right: u16 = 0x255a;
    pub const boxdraw_up_left_double: u16 = 0x255b;
    pub const boxdraw_up_double_left: u16 = 0x255c;
    pub const boxdraw_double_up_left: u16 = 0x255d;
    pub const boxdraw_vertical_right_double: u16 = 0x255e;
    pub const boxdraw_vertical_double_right: u16 = 0x255f;
    pub const boxdraw_double_vertical_right: u16 = 0x2560;
    pub const boxdraw_vertical_left_double: u16 = 0x2561;
    pub const boxdraw_vertical_double_left: u16 = 0x2562;
    pub const boxdraw_double_vertical_left: u16 = 0x2563;
    pub const boxdraw_down_horizontal_double: u16 = 0x2564;
    pub const boxdraw_down_double_horizontal: u16 = 0x2565;
    pub const boxdraw_double_down_horizontal: u16 = 0x2566;
    pub const boxdraw_up_horizontal_double: u16 = 0x2567;
    pub const boxdraw_up_double_horizontal: u16 = 0x2568;
    pub const boxdraw_double_up_horizontal: u16 = 0x2569;
    pub const boxdraw_vertical_horizontal_double: u16 = 0x256a;
    pub const boxdraw_vertical_double_horizontal: u16 = 0x256b;
    pub const boxdraw_double_vertical_horizontal: u16 = 0x256c;
    pub const blockelement_full_block: u16 = 0x2588;
    pub const blockelement_light_shade: u16 = 0x2591;
    pub const geometricshape_up_triangle: u16 = 0x25b2;
    pub const geometricshape_right_triangle: u16 = 0x25ba;
    pub const geometricshape_down_triangle: u16 = 0x25bc;
    pub const geometricshape_left_triangle: u16 = 0x25c4;
    pub const arrow_up: u16 = 0x2591;
    pub const arrow_down: u16 = 0x2593;

    pub const Attribute = packed struct(u8) {
        foreground: ForegroundColor = .white,
        background: BackgroundColor = .black,

        pub const ForegroundColor = enum(u4) {
            black,
            blue,
            green,
            cyan,
            red,
            magenta,
            brown,
            lightgray,
            darkgray,
            lightblue,
            lightgreen,
            lightcyan,
            lightred,
            lightmagenta,
            yellow,
            white,
        };

        pub const BackgroundColor = enum(u4) {
            black,
            blue,
            green,
            cyan,
            red,
            magenta,
            brown,
            lightgray,
        };
    };

    pub const Mode = extern struct {
        max_mode: u32, // specified as signed
        mode: u32, // specified as signed
        attribute: i32,
        cursor_column: i32,
        cursor_row: i32,
        cursor_visible: bool,
    };

    pub const Geometry = struct {
        columns: usize,
        rows: usize,
    };
};



---
File: /std/os/uefi/protocol/udp6.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const Event = uefi.Event;
const Status = uefi.Status;
const Time = uefi.Time;
const Ip6 = uefi.protocol.Ip6;
const ManagedNetworkConfigData = uefi.protocol.ManagedNetwork.Config;
const SimpleNetwork = uefi.protocol.SimpleNetwork;
const cc = uefi.cc;

pub const Udp6 = extern struct {
    _get_mode_data: *const fn (*const Udp6, ?*Config, ?*Ip6.Mode, ?*ManagedNetworkConfigData, ?*SimpleNetwork) callconv(cc) Status,
    _configure: *const fn (*const Udp6, ?*const Config) callconv(cc) Status,
    _groups: *const fn (*const Udp6, bool, ?*const Ip6.Address) callconv(cc) Status,
    _transmit: *const fn (*const Udp6, *CompletionToken) callconv(cc) Status,
    _receive: *const fn (*const Udp6, *CompletionToken) callconv(cc) Status,
    _cancel: *const fn (*const Udp6, ?*CompletionToken) callconv(cc) Status,
    _poll: *const fn (*const Udp6) callconv(cc) Status,

    pub const GetModeDataError = uefi.UnexpectedError || error{
        NotStarted,
        InvalidParameter,
    };
    pub const ConfigureError = uefi.UnexpectedError || error{
        NoMapping,
        InvalidParameter,
        AlreadyStarted,
        AccessDenied,
        OutOfResources,
        DeviceError,
    };
    pub const GroupsError = uefi.UnexpectedError || error{
        NotStarted,
        OutOfResources,
        InvalidParameter,
        AlreadyStarted,
        NotFound,
        DeviceError,
    };
    pub const TransmitError = uefi.UnexpectedError || error{
        NotStarted,
        NoMapping,
        InvalidParameter,
        AccessDenied,
        NotReady,
        OutOfResources,
        NotFound,
        BadBufferSize,
        NoMedia,
    };
    pub const ReceiveError = uefi.UnexpectedError || error{
        NotStarted,
        NoMapping,
        InvalidParameter,
        OutOfResources,
        DeviceError,
        AccessDenied,
        NotReady,
        NoMedia,
    };
    pub const CancelError = uefi.UnexpectedError || error{
        InvalidParameter,
        NotStarted,
        NotFound,
    };
    pub const PollError = uefi.UnexpectedError || error{
        InvalidParameter,
        DeviceError,
        Timeout,
    };

    pub fn getModeData(self: *const Udp6) GetModeDataError!ModeData {
        var data: ModeData = undefined;
        switch (self._get_mode_data(
            self,
            &data.udp6_config_data,
            &data.ip6_mode_data,
            &data.mnp_config_data,
            &data.snp_mode_data,
        )) {
            .success => return data,
            .not_started => return error.NotStarted,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn configure(self: *Udp6, udp6_config_data: ?*const Config) ConfigureError!void {
        switch (self._configure(self, udp6_config_data)) {
            .success => {},
            .no_mapping => return error.NoMapping,
            .invalid_parameter => return error.InvalidParameter,
            .already_started => return error.AlreadyStarted,
            .access_denied => return error.AccessDenied,
            .out_of_resources => return error.OutOfResources,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn groups(
        self: *Udp6,
        join_flag: JoinFlag,
        multicast_address: ?*const Ip6.Address,
    ) GroupsError!void {
        switch (self._groups(
            self,
            // set to TRUE to join a multicast group
            join_flag == .join,
            multicast_address,
        )) {
            .success => {},
            .not_started => return error.NotStarted,
            .out_of_resources => return error.OutOfResources,
            .invalid_parameter => return error.InvalidParameter,
            .already_started => return error.AlreadyStarted,
            .not_found => return error.NotFound,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn transmit(self: *Udp6, token: *CompletionToken) TransmitError!void {
        switch (self._transmit(self, token)) {
            .success => {},
            .not_started => return error.NotStarted,
            .no_mapping => return error.NoMapping,
            .invalid_parameter => return error.InvalidParameter,
            .access_denied => return error.AccessDenied,
            .not_ready => return error.NotReady,
            .out_of_resources => return error.OutOfResources,
            .not_found => return error.NotFound,
            .bad_buffer_size => return error.BadBufferSize,
            .no_media => return error.NoMedia,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn receive(self: *Udp6, token: *CompletionToken) ReceiveError!void {
        switch (self._receive(self, token)) {
            .success => {},
            .not_started => return error.NotStarted,
            .no_mapping => return error.NoMapping,
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            .device_error => return error.DeviceError,
            .access_denied => return error.AccessDenied,
            .not_ready => return error.NotReady,
            .no_media => return error.NoMedia,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn cancel(self: *Udp6, token: ?*CompletionToken) CancelError!void {
        switch (self._cancel(self, token)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .not_started => return error.NotStarted,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn poll(self: *Udp6) PollError!void {
        switch (self._poll(self)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .timeout => return error.Timeout,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const guid align(8) = uefi.Guid{
        .time_low = 0x4f948815,
        .time_mid = 0xb4b9,
        .time_high_and_version = 0x43cb,
        .clock_seq_high_and_reserved = 0x8a,
        .clock_seq_low = 0x33,
        .node = [_]u8{ 0x90, 0xe0, 0x60, 0xb3, 0x49, 0x55 },
    };

    pub const JoinFlag = enum {
        join,
        leave,
    };

    pub const ModeData = struct {
        udp6_config_data: Config,
        ip6_mode_data: Ip6.Mode,
        mnp_config_data: ManagedNetworkConfigData,
        snp_mode_data: SimpleNetwork,
    };

    pub const Config = extern struct {
        accept_promiscuous: bool,
        accept_any_port: bool,
        allow_duplicate_port: bool,
        traffic_class: u8,
        hop_limit: u8,
        receive_timeout: u32,
        transmit_timeout: u32,
        station_address: Ip6.Address,
        station_port: u16,
        remote_address: Ip6.Address,
        remote_port: u16,
    };

    pub const CompletionToken = extern struct {
        event: Event,
        status: usize,
        packet: extern union {
            rx_data: *ReceiveData,
            tx_data: *TransmitData,
        },
    };

    pub const ReceiveData = extern struct {
        timestamp: Time,
        recycle_signal: Event,
        udp6_session: SessionData,
        data_length: u32,
        fragment_count: u32,

        pub fn getFragments(self: *ReceiveData) []Fragment {
            return @as([*]Fragment, @ptrCast(@alignCast(@as([*]u8, @ptrCast(self)) + @sizeOf(ReceiveData))))[0..self.fragment_count];
        }
    };

    pub const TransmitData = extern struct {
        udp6_session_data: ?*SessionData,
        data_length: u32,
        fragment_count: u32,

        pub fn getFragments(self: *TransmitData) []Fragment {
            return @as([*]Fragment, @ptrCast(@alignCast(@as([*]u8, @ptrCast(self)) + @sizeOf(TransmitData))))[0..self.fragment_count];
        }
    };

    pub const SessionData = extern struct {
        source_address: Ip6.Address,
        source_port: u16,
        destination_address: Ip6.Address,
        destination_port: u16,
    };

    pub const Fragment = extern struct {
        fragment_length: u32,
        fragment_buffer: [*]u8,
    };
};



---
File: /std/os/uefi/tables/boot_services.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Event = uefi.Event;
const EventRegistration = uefi.EventRegistration;
const Guid = uefi.Guid;
const Handle = uefi.Handle;
const Page = uefi.Page;
const Pages = uefi.Pages;
const Status = uefi.Status;
const TableHeader = uefi.tables.TableHeader;
const DevicePathProtocol = uefi.protocol.DevicePath;
const AllocateLocation = uefi.tables.AllocateLocation;
const AllocateType = uefi.tables.AllocateType;
const MemoryType = uefi.tables.MemoryType;
const MemoryDescriptor = uefi.tables.MemoryDescriptor;
const MemoryMapKey = uefi.tables.MemoryMapKey;
const MemoryMapInfo = uefi.tables.MemoryMapInfo;
const MemoryMapSlice = uefi.tables.MemoryMapSlice;
const TimerDelay = uefi.tables.TimerDelay;
const InterfaceType = uefi.tables.InterfaceType;
const LocateSearch = uefi.tables.LocateSearch;
const LocateSearchType = uefi.tables.LocateSearchType;
const OpenProtocolArgs = uefi.tables.OpenProtocolArgs;
const OpenProtocolAttributes = uefi.tables.OpenProtocolAttributes;
const ProtocolInformationEntry = uefi.tables.ProtocolInformationEntry;
const EventNotify = uefi.tables.EventNotify;
const cc = uefi.cc;
const Error = Status.Error;

/// Boot services are services provided by the system's firmware until the operating system takes
/// over control over the hardware by calling exitBootServices.
///
/// Boot Services must not be used after exitBootServices has been called. The only exception is
/// getMemoryMap, which may be used after the first unsuccessful call to exitBootServices.
/// After successfully calling exitBootServices, system_table.console_in_handle, system_table.con_in,
/// system_table.console_out_handle, system_table.con_out, system_table.standard_error_handle,
/// system_table.std_err, and system_table.boot_services should be set to null. After setting these
/// attributes to null, system_table.hdr.crc32 must be recomputed.
///
/// As the boot_services table may grow with new UEFI versions, it is important to check hdr.header_size.
pub const BootServices = extern struct {
    hdr: TableHeader,

    /// Raises a task's priority level and returns its previous level.
    raiseTpl: *const fn (new_tpl: TaskPriorityLevel) callconv(cc) TaskPriorityLevel,

    /// Restores a task's priority level to its previous value.
    restoreTpl: *const fn (old_tpl: TaskPriorityLevel) callconv(cc) void,

    /// Allocates memory pages from the system.
    _allocatePages: *const fn (alloc_type: AllocateType, mem_type: MemoryType, pages: usize, memory: *[*]align(4096) Page) callconv(cc) Status,

    /// Frees memory pages.
    _freePages: *const fn (memory: [*]align(4096) Page, pages: usize) callconv(cc) Status,

    /// Returns the current memory map.
    _getMemoryMap: *const fn (mmap_size: *usize, mmap: ?[*]align(@alignOf(MemoryDescriptor)) u8, map_key: *MemoryMapKey, descriptor_size: *usize, descriptor_version: *u32) callconv(cc) Status,

    /// Allocates pool memory.
    _allocatePool: *const fn (pool_type: MemoryType, size: usize, buffer: *[*]align(8) u8) callconv(cc) Status,

    /// Returns pool memory to the system.
    _freePool: *const fn (buffer: [*]align(8) u8) callconv(cc) Status,

    /// Creates an event.
    _createEvent: *const fn (type: u32, notify_tpl: TaskPriorityLevel, notify_func: ?*const fn (Event, ?*anyopaque) callconv(cc) void, notify_ctx: ?*anyopaque, event: *Event) callconv(cc) Status,

    /// Sets the type of timer and the trigger time for a timer event.
    _setTimer: *const fn (event: Event, type: TimerDelay, trigger_time: u64) callconv(cc) Status,

    /// Stops execution until an event is signaled.
    _waitForEvent: *const fn (event_len: usize, events: [*]const Event, index: *usize) callconv(cc) Status,

    /// Signals an event.
    _signalEvent: *const fn (event: Event) callconv(cc) Status,

    /// Closes an event.
    _closeEvent: *const fn (event: Event) callconv(cc) Status,

    /// Checks whether an event is in the signaled state.
    _checkEvent: *const fn (event: Event) callconv(cc) Status,

    /// Installs a protocol interface on a device handle. If the handle does not exist, it is created
    /// and added to the list of handles in the system. installMultipleProtocolInterfaces()
    /// performs more error checking than installProtocolInterface(), so its use is recommended over this.
    _installProtocolInterface: *const fn (handle: Handle, protocol: *const Guid, interface_type: InterfaceType, interface: *anyopaque) callconv(cc) Status,

    /// Reinstalls a protocol interface on a device handle
    _reinstallProtocolInterface: *const fn (handle: Handle, protocol: *const Guid, old_interface: *anyopaque, new_interface: *anyopaque) callconv(cc) Status,

    /// Removes a protocol interface from a device handle. Usage of
    /// uninstallMultipleProtocolInterfaces is recommended over this.
    _uninstallProtocolInterface: *const fn (handle: Handle, protocol: *const Guid, interface: *anyopaque) callconv(cc) Status,

    /// Queries a handle to determine if it supports a specified protocol.
    _handleProtocol: *const fn (handle: Handle, protocol: *const Guid, interface: *?*anyopaque) callconv(cc) Status,

    _reserved: *anyopaque,

    /// Creates an event that is to be signaled whenever an interface is installed for a specified protocol.
    _registerProtocolNotify: *const fn (protocol: *const Guid, event: Event, registration: *EventRegistration) callconv(cc) Status,

    /// Returns an array of handles that support a specified protocol.
    _locateHandle: *const fn (search_type: LocateSearchType, protocol: ?*const Guid, search_key: ?*const anyopaque, buffer_size: *usize, buffer: ?[*]Handle) callconv(cc) Status,

    /// Locates the handle to a device on the device path that supports the specified protocol
    _locateDevicePath: *const fn (protocols: *const Guid, device_path: **const DevicePathProtocol, device: *?Handle) callconv(cc) Status,

    /// Adds, updates, or removes a configuration table entry from the EFI System Table.
    _installConfigurationTable: *const fn (guid: *const Guid, table: ?*anyopaque) callconv(cc) Status,

    /// Loads an EFI image into memory.
    _loadImage: *const fn (boot_policy: bool, parent_image_handle: Handle, device_path: ?*const DevicePathProtocol, source_buffer: ?[*]const u8, source_size: usize, image_handle: *Handle) callconv(cc) Status,

    /// Transfers control to a loaded image's entry point.
    _startImage: *const fn (image_handle: Handle, exit_data_size: ?*usize, exit_data: ?*[*]u16) callconv(cc) Status,

    /// Terminates a loaded EFI image and returns control to boot services.
    _exit: *const fn (image_handle: Handle, exit_status: Status, exit_data_size: usize, exit_data: ?[*]align(2) const u8) callconv(cc) Status,

    /// Unloads an image.
    _unloadImage: *const fn (image_handle: Handle) callconv(cc) Status,

    /// Terminates all boot services.
    _exitBootServices: *const fn (image_handle: Handle, map_key: MemoryMapKey) callconv(cc) Status,

    /// Returns a monotonically increasing count for the platform.
    _getNextMonotonicCount: *const fn (count: *u64) callconv(cc) Status,

    /// Induces a fine-grained stall.
    _stall: *const fn (microseconds: usize) callconv(cc) Status,

    /// Sets the system's watchdog timer.
    _setWatchdogTimer: *const fn (timeout: usize, watchdog_code: u64, data_size: usize, watchdog_data: ?[*]const u16) callconv(cc) Status,

    /// Connects one or more drives to a controller.
    _connectController: *const fn (controller_handle: Handle, driver_image_handle: ?[*:null]?Handle, remaining_device_path: ?*const DevicePathProtocol, recursive: bool) callconv(cc) Status,

    // Disconnects one or more drivers from a controller
    _disconnectController: *const fn (controller_handle: Handle, driver_image_handle: ?Handle, child_handle: ?Handle) callconv(cc) Status,

    /// Queries a handle to determine if it supports a specified protocol.
    _openProtocol: *const fn (handle: Handle, protocol: *const Guid, interface: ?*?*anyopaque, agent_handle: ?Handle, controller_handle: ?Handle, attributes: OpenProtocolAttributes) callconv(cc) Status,

    /// Closes a protocol on a handle that was opened using openProtocol().
    _closeProtocol: *const fn (handle: Handle, protocol: *const Guid, agent_handle: Handle, controller_handle: ?Handle) callconv(cc) Status,

    /// Retrieves the list of agents that currently have a protocol interface opened.
    _openProtocolInformation: *const fn (handle: Handle, protocol: *const Guid, entry_buffer: *[*]ProtocolInformationEntry, entry_count: *usize) callconv(cc) Status,

    /// Retrieves the list of protocol interface GUIDs that are installed on a handle in a buffer allocated from pool.
    _protocolsPerHandle: *const fn (handle: Handle, protocol_buffer: *[*]*const Guid, protocol_buffer_count: *usize) callconv(cc) Status,

    /// Returns an array of handles that support the requested protocol in a buffer allocated from pool.
    _locateHandleBuffer: *const fn (search_type: LocateSearchType, protocol: ?*const Guid, search_key: ?*const anyopaque, num_handles: *usize, buffer: *[*]Handle) callconv(cc) Status,

    /// Returns the first protocol instance that matches the given protocol.
    _locateProtocol: *const fn (protocol: *const Guid, registration: ?EventRegistration, interface: *?*const anyopaque) callconv(cc) Status,

    /// Installs one or more protocol interfaces into the boot services environment
    // TODO: use callconv(cc) instead once that works
    _installMultipleProtocolInterfaces: *const fn (handle: *Handle, ...) callconv(.c) Status,

    /// Removes one or more protocol interfaces into the boot services environment
    // TODO: use callconv(cc) instead once that works
    _uninstallMultipleProtocolInterfaces: *const fn (handle: *Handle, ...) callconv(.c) Status,

    /// Computes and returns a 32-bit CRC for a data buffer.
    _calculateCrc32: *const fn (data: [*]const u8, data_size: usize, *u32) callconv(cc) Status,

    /// Copies the contents of one buffer to another buffer
    _copyMem: *const fn (dest: [*]u8, src: [*]const u8, len: usize) callconv(cc) void,

    /// Fills a buffer with a specified value
    _setMem: *const fn (buffer: [*]u8, size: usize, value: u8) callconv(cc) void,

    /// Creates an event in a group.
    _createEventEx: *const fn (type: u32, notify_tpl: usize, notify_func: EventNotify, notify_ctx: *const anyopaque, event_group: *const Guid, event: *Event) callconv(cc) Status,

    pub const AllocatePagesError = uefi.UnexpectedError || error{
        OutOfResources,
        InvalidParameter,
        NotFound,
    };

    pub const FreePagesError = uefi.UnexpectedError || error{
        NotFound,
        InvalidParameter,
    };

    pub const GetMemoryMapError = uefi.UnexpectedError || error{
        InvalidParameter,
        BufferTooSmall,
    };

    pub const AllocatePoolError = uefi.UnexpectedError || error{
        OutOfResources,
        InvalidParameter,
    };

    pub const FreePoolError = uefi.UnexpectedError || error{
        InvalidParameter,
    };

    pub const CreateEventError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
    };

    pub const SetTimerError = uefi.UnexpectedError || error{
        InvalidParameter,
    };

    pub const WaitForEventError = uefi.UnexpectedError || error{
        InvalidParameter,
        Unsupported,
    };

    pub const CheckEventError = uefi.UnexpectedError || error{
        InvalidParameter,
    };

    pub const ReinstallProtocolInterfaceError = uefi.UnexpectedError || error{
        NotFound,
        AccessDenied,
        InvalidParameter,
    };

    pub const HandleProtocolError = uefi.UnexpectedError || error{
        Unsupported,
    };

    pub const RegisterProtocolNotifyError = uefi.UnexpectedError || error{
        OutOfResources,
        InvalidParameter,
    };

    pub const NumHandlesError = uefi.UnexpectedError;

    pub const LocateHandleError = uefi.UnexpectedError || error{
        BufferTooSmall,
        InvalidParameter,
    };

    pub const LocateDevicePathError = uefi.UnexpectedError || error{
        NotFound,
        InvalidParameter,
    };

    pub const InstallConfigurationTableError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
    };

    pub const UninstallConfigurationTableError = InstallConfigurationTableError || error{
        NotFound,
    };

    pub const LoadImageError = uefi.UnexpectedError || error{
        NotFound,
        InvalidParameter,
        Unsupported,
        OutOfResources,
        LoadError,
        DeviceError,
        AccessDenied,
        SecurityViolation,
    };

    pub const StartImageError = uefi.UnexpectedError || error{
        InvalidParameter,
        SecurityViolation,
    };

    pub const ExitError = uefi.UnexpectedError || error{
        InvalidParameter,
    };

    pub const ExitBootServicesError = uefi.UnexpectedError || error{
        InvalidParameter,
    };

    pub const GetNextMonotonicCountError = uefi.UnexpectedError || error{
        DeviceError,
        InvalidParameter,
    };

    pub const SetWatchdogTimerError = uefi.UnexpectedError || error{
        InvalidParameter,
        Unsupported,
        DeviceError,
    };

    pub const ConnectControllerError = uefi.UnexpectedError || error{
        InvalidParameter,
        NotFound,
        SecurityViolation,
    };

    pub const DisconnectControllerError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
        DeviceError,
    };

    pub const OpenProtocolError = uefi.UnexpectedError || error{
        InvalidParameter,
        Unsupported,
        AccessDenied,
        AlreadyStarted,
    };

    pub const CloseProtocolError = uefi.UnexpectedError || error{
        InvalidParameter,
        NotFound,
    };

    pub const OpenProtocolInformationError = uefi.UnexpectedError || error{
        OutOfResources,
    };

    pub const ProtocolsPerHandleError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
    };

    pub const LocateHandleBufferError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
    };

    pub const LocateProtocolError = uefi.UnexpectedError || error{
        InvalidParameter,
    };

    pub const InstallProtocolInterfacesError = uefi.UnexpectedError || error{
        AlreadyStarted,
        OutOfResources,
        InvalidParameter,
    };

    pub const UninstallProtocolInterfacesError = uefi.UnexpectedError || error{
        InvalidParameter,
    };

    pub const CalculateCrc32Error = uefi.UnexpectedError || error{
        InvalidParameter,
    };

    /// Allocates pages of memory.
    ///
    /// This function scans the memory map to locate free pages. When it finds a
    /// physically contiguous block of pages that is large enough and also satisfies
    /// the allocation requirements of `alloc_type`, it changes the memory map to
    /// indicate that the pages are now of type `mem_type`.
    ///
    /// In general, UEFI OS loaders and UEFI applications should allocate memory
    /// (and pool) of type `.loader_data`. UEFI boot service drivers must allocate
    /// memory (and pool) of type `.boot_services_data`. UREFI runtime drivers
    /// should allocate memory (and pool) of type `.runtime_services_data`
    /// (although such allocation can only be made during boot services time).
    ///
    /// Allocation requests of `.allocate_any_pages` allocate any available range
    /// of pages that satisfies the request.
    ///
    /// Allocation requests of `.allocate_max_address` allocate any available range
    /// of pages whose uppermost address is less than or equal to the address
    /// pointed to by the input.
    ///
    /// Allocation requests of `.allocate_address` allocate pages at the address
    /// pointed to by the input.
    pub fn allocatePages(
        self: *BootServices,
        location: AllocateLocation,
        mem_type: MemoryType,
        pages: usize,
    ) AllocatePagesError![]align(4096) Page {
        var ptr: [*]align(4096) Page = switch (location) {
            .any => undefined,
            .address, .max_address => |ptr| ptr,
        };

        switch (self._allocatePages(
            std.meta.activeTag(location),
            mem_type,
            pages,
            &ptr,
        )) {
            .success => return ptr[0..pages],
            .out_of_resources => return error.OutOfResources,
            .invalid_parameter => return error.InvalidParameter,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn freePages(self: *BootServices, pages: []align(4096) Page) FreePagesError!void {
        switch (self._freePages(pages.ptr, pages.len)) {
            .success => {},
            .not_found => return error.NotFound,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn getMemoryMapInfo(self: *const BootServices) uefi.UnexpectedError!MemoryMapInfo {
        var info: MemoryMapInfo = undefined;
        info.len = 0;

        switch (self._getMemoryMap(
            &info.len,
            null,
            &info.key,
            &info.descriptor_size,
            &info.descriptor_version,
        )) {
            .success, .buffer_too_small => {
                info.len = @divExact(info.len, info.descriptor_size);
                return info;
            },
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn getMemoryMap(
        self: *const BootServices,
        buffer: []align(@alignOf(MemoryDescriptor)) u8,
    ) GetMemoryMapError!MemoryMapSlice {
        var info: MemoryMapInfo = undefined;
        info.len = buffer.len;

        switch (self._getMemoryMap(
            &info.len,
            buffer.ptr,
            &info.key,
            &info.descriptor_size,
            &info.descriptor_version,
        )) {
            .success => {
                info.len = @divExact(info.len, info.descriptor_size);
                return .{ .info = info, .ptr = buffer.ptr };
            },
            .buffer_too_small => return error.BufferTooSmall,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Allocates a memory region of `size` bytes from memory of type `pool_type`
    /// and returns the allocated memory. Allocates pages from `.conventional_memory`
    /// as needed to grow the requested pool type.
    pub fn allocatePool(
        self: *BootServices,
        pool_type: MemoryType,
        size: usize,
    ) AllocatePoolError![]align(8) u8 {
        var ptr: [*]align(8) u8 = undefined;

        switch (self._allocatePool(pool_type, size, &ptr)) {
            .success => return ptr[0..size],
            .out_of_resources => return error.OutOfResources,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn freePool(self: *BootServices, ptr: [*]align(8) u8) FreePoolError!void {
        switch (self._freePool(ptr)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn createEvent(
        self: *BootServices,
        event_type: uefi.EventType,
        notify_opts: NotifyOpts,
    ) CreateEventError!Event {
        var evt: Event = undefined;

        switch (self._createEvent(
            @bitCast(event_type),
            notify_opts.tpl,
            notify_opts.function,
            notify_opts.context,
            &evt,
        )) {
            .success => return evt,
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Cancels any previous time trigger setting for the event, and sets a new
    /// trigger timer for the event.
    ///
    /// Returns `error.InvalidParameter` if the event is not a timer event.
    pub fn setTimer(
        self: *BootServices,
        event: Event,
        @"type": TimerDelay,
        trigger_time: u64,
    ) SetTimerError!void {
        switch (self._setTimer(event, @"type", trigger_time)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Returns the event that was signaled, along with its index in the slice.
    pub fn waitForEvent(
        self: *BootServices,
        events: []const Event,
    ) WaitForEventError!struct { *const Event, usize } {
        var idx: usize = undefined;
        switch (self._waitForEvent(events.len, events.ptr, &idx)) {
            .success => return .{ &events[idx], idx },
            .invalid_parameter => return error.InvalidParameter,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// If `event` is `EventType.signal`, then the event’s notification function
    /// is scheduled to be invoked at the event’s notification task priority level.
    /// This function may be invoked from any task priority level.
    ///
    /// If the supplied Event is a part of an event group, then all of the events
    /// in the event group are also signaled and their notification functions are
    /// scheduled.
    ///
    /// When signaling an event group, it is possible to create an event in the
    /// group, signal it and then close the event to remove it from the group.
    pub fn signalEvent(self: *BootServices, event: Event) uefi.UnexpectedError!void {
        switch (self._signalEvent(event)) {
            .success => {},
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn closeEvent(self: *BootServices, event: Event) uefi.UnexpectedError!void {
        switch (self._closeEvent(event)) {
            .success => {},
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Checks to see whether an event is signaled.
    ///
    /// The underlying function is equivalent to this pseudo-code:
    /// ```
    /// if (event.type.signal)
    ///     return error.InvalidParameter;
    ///
    /// if (event.signaled) {
    ///     event.signaled = false;
    ///     return true;
    /// }
    ///
    /// const notify = event.notification_function orelse return false;
    /// notify();
    ///
    /// if (event.signaled) {
    ///     event.signaled = false;
    ///     return true;
    /// }
    ///
    /// return false;
    /// ```
    pub fn checkEvent(self: *BootServices, event: Event) CheckEventError!bool {
        switch (self._checkEvent(event)) {
            .success => return true,
            .not_ready => return false,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// See `installProtocolInterfaces`.
    ///
    /// Does not call `self._installProtocolInterface`, because
    /// `self._installMultipleProtocolInterfaces` performs more error checks.
    pub fn installProtocolInterface(
        self: *BootServices,
        handle: ?Handle,
        interface: anytype,
    ) InstallProtocolInterfacesError!Handle {
        return self.installProtocolInterfaces(handle, .{
            interface,
        });
    }

    /// Reinstalls a protocol interface on a device handle.
    ///
    /// `new` may be the same as `old`. If it is, the registered protocol notifications
    /// occur for the handle without replacing the interface on the handle.
    ///
    /// Any process that has registered to wait for the installation of the interface
    /// is notified.
    ///
    /// The caller is responsible for ensuring that there are no references to `old`
    /// if it is being removed.
    pub fn reinstallProtocolInterface(
        self: *BootServices,
        handle: Handle,
        Protocol: type,
        old: ?*const Protocol,
        new: ?*const Protocol,
    ) ReinstallProtocolInterfaceError!void {
        if (!@hasDecl(Protocol, "guid"))
            @compileError("protocol is missing guid");

        switch (self._reinstallProtocolInterface(
            handle,
            &Protocol.guid,
            old,
            new,
        )) {
            .success => {},
            .not_found => return error.NotFound,
            .access_denied => return error.AccessDenied,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// See `uninstallProtocolInterfaces`.
    ///
    /// Does not call `self._uninstallProtocolInterface`, because
    /// `self._uninstallMultipleProtocolInterfaces` performs more error checks.
    pub fn uninstallProtocolInterface(
        self: *BootServices,
        handle: Handle,
        interface: anytype,
    ) UninstallProtocolInterfacesError!void {
        return self.uninstallProtocolInterfaces(handle, .{
            interface,
        });
    }

    /// Returns a pointer to the `Protocol` interface if it's supported by the
    /// handle.
    ///
    /// Note that UEFI implementations are no longer required to implement this
    /// function, so it's implemented using `openProtocol` instead.
    pub fn handleProtocol(
        self: *BootServices,
        Protocol: type,
        handle: Handle,
    ) HandleProtocolError!?*Protocol {
        // per https://uefi.org/specs/UEFI/2.10/07_Services_Boot_Services.html#efi-boot-services-handleprotocol
        // handleProtocol is basically `openProtocol` where:
        // 1. agent_handle is `uefi.handle` (aka handle passed to `EfiMain`)
        // 2. controller_handle is `null`
        // 3. attributes is `EFI_OPEN_PROTOCOL_BY_HANDLE_PROTOCOL`

        return self.openProtocol(
            Protocol,
            handle,
            .{ .by_handle_protocol = .{ .agent = uefi.handle } },
        ) catch |err| switch (err) {
            error.AlreadyStarted => return uefi.unexpectedStatus(.already_started),
            error.AccessDenied => return uefi.unexpectedStatus(.access_denied),
            error.InvalidParameter => return uefi.unexpectedStatus(.invalid_parameter),
            else => return @errorCast(err),
        };
    }

    pub fn registerProtocolNotify(
        self: *BootServices,
        Protocol: type,
        event: Event,
    ) RegisterProtocolNotifyError!EventRegistration {
        if (!@hasDecl(Protocol, "guid"))
            @compileError("Protocol is missing guid");

        var registration: EventRegistration = undefined;
        switch (self._registerProtocolNotify(
            &Protocol.guid,
            event,
            &registration,
        )) {
            .success => return registration,
            .out_of_resources => return error.OutOfResources,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Returns the number of handles that match the given search criteria.
    pub fn locateHandleLen(self: *const BootServices, search: LocateSearch) NumHandlesError!usize {
        var len: usize = 0;
        switch (self._locateHandle(
            std.meta.activeTag(search),
            if (search == .by_protocol) search.by_protocol else null,
            if (search == .by_register_notify) search.by_register_notify else null,
            &len,
            null,
        )) {
            // If len is zero, it should return not_found, otherwise buffer_too_small.
            // This is because it can/should only return success when a valid buffer is
            // passed with a non zero size, which is not the case.
            // Thus this status is considered unreachable and will return error.Unexpected
            // .success => unreachable,
            .buffer_too_small => return @divExact(len, @sizeOf(uefi.Handle)),
            .not_found => return 0,
            // This function accounts for all possible causes of this error code
            // as per the most recent UEFI spec 2.10A, therefore this branch is
            // considered unreachable and will return error.Unexpected instead
            // .invalid_parameter => unreachable
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// To determine the necessary size of `buffer`, call `locateHandleLen` first.
    pub fn locateHandle(
        self: *BootServices,
        search: LocateSearch,
        buffer: []Handle,
    ) LocateHandleError![]Handle {
        var len: usize = @sizeOf(Handle) * buffer.len;
        switch (self._locateHandle(
            std.meta.activeTag(search),
            if (search == .by_protocol) search.by_protocol else null,
            if (search == .by_register_notify) search.by_register_notify else null,
            &len,
            buffer.ptr,
        )) {
            .success => return buffer[0..@divExact(len, @sizeOf(Handle))],
            .not_found => return buffer[0..0],
            .buffer_too_small => return error.BufferTooSmall,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Locates all devices on `device_path` that support `Protocol`. Once the closest
    /// match to `device_path` is found, it returns the unmatched device path and handle.
    pub fn locateDevicePath(
        self: *const BootServices,
        device_path: *const DevicePathProtocol,
        Protocol: type,
    ) LocateHandleError!?struct { *const DevicePathProtocol, Handle } {
        if (!@hasDecl(Protocol, "guid"))
            @compileError("Protocol is missing guid");

        var dev_path = device_path;
        var device: ?Handle = undefined;
        switch (self._locateDevicePath(
            &Protocol.guid,
            &dev_path,
            &device,
        )) {
            .success => return .{ dev_path, device.? },
            .not_found => return null,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn installConfigurationTable(
        self: *BootServices,
        guid: *const Guid,
        table: *anyopaque,
    ) InstallConfigurationTableError!void {
        switch (self._installConfigurationTable(
            guid,
            table,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn uninstallConfigurationTable(
        self: *BootServices,
        guid: *const Guid,
    ) UninstallConfigurationTableError!void {
        switch (self._installConfigurationTable(
            guid,
            null,
        )) {
            .success => {},
            .not_found => return error.NotFound,
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const LoadImageSource = union(enum) {
        buffer: []const u8,
        device_path: *const DevicePathProtocol,
    };

    pub fn loadImage(
        self: *BootServices,
        boot_policy: bool,
        parent_image: Handle,
        source: LoadImageSource,
    ) LoadImageError!Handle {
        var handle: Handle = undefined;

        switch (self._loadImage(
            boot_policy,
            parent_image,
            if (source == .device_path) source.device_path else null,
            if (source == .buffer) source.buffer.ptr else null,
            if (source == .buffer) source.buffer.len else 0,
            &handle,
        )) {
            .success => return handle,
            .not_found => return error.NotFound,
            .invalid_parameter => return error.InvalidParameter,
            .unsupported => return error.Unsupported,
            .out_of_resources => return error.OutOfResources,
            .load_error => return error.LoadError,
            .device_error => return error.DeviceError,
            .access_denied => return error.AccessDenied,
            .security_violation => return error.SecurityViolation,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn startImage(self: *BootServices, image: Handle) StartImageError!ImageExitData {
        var exit_data_size: usize = undefined;
        var exit_data: [*]u16 = undefined;

        const exit_code = switch (self._startImage(
            image,
            &exit_data_size,
            &exit_data,
        )) {
            .invalid_parameter => return error.InvalidParameter,
            .security_violation => return error.SecurityViolation,
            else => |exit_code| exit_code,
        };

        if (exit_data_size == 0) return .{
            .code = exit_code,
            .description = null,
            .data = null,
        };

        const description_ptr: [*:0]const u16 = @ptrCast(exit_data);
        const description = std.mem.sliceTo(description_ptr, 0);

        return ImageExitData{
            .code = exit_code,
            .description = description,
            .data = exit_data[description.len + 1 .. exit_data_size],
        };
    }

    /// `message` must be allocated using `allocatePool`.
    pub fn exit(
        self: *BootServices,
        handle: Handle,
        status: Status,
        message: ?[:0]const u16,
    ) ExitError!void {
        switch (self._exit(
            handle,
            status,
            if (message) |msg| (2 * msg.len) + 1 else 0,
            if (message) |msg| @ptrCast(msg.ptr) else null,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            else => |exit_status| return uefi.unexpectedStatus(exit_status),
        }
    }

    /// `message` should be a null-terminated u16 string followed by binary data
    /// allocated using `allocatePool`.
    pub fn exitWithData(
        self: *BootServices,
        handle: Handle,
        status: Status,
        data: []align(2) const u8,
    ) ExitError!void {
        switch (self._exit(handle, status, data.len, data.ptr)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            else => |exit_status| return uefi.unexpectedStatus(exit_status),
        }
    }

    /// The result is the exit code of the unload handler. Any error codes are
    /// `try/catch`-able, leaving only success and warning codes as the result.
    pub fn unloadImage(
        self: *BootServices,
        image: Handle,
    ) Status.Error!Status {
        const status = self._unloadImage(image);
        try status.err();
        return status;
    }

    pub fn exitBootServices(
        self: *BootServices,
        image: Handle,
        map_key: MemoryMapKey,
    ) ExitBootServicesError!void {
        switch (self._exitBootServices(image, map_key)) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn getNextMonotonicCount(
        self: *const BootServices,
        count: *u64,
    ) GetNextMonotonicCountError!void {
        switch (self._getNextMonotonicCount(count)) {
            .success => {},
            .device_error => return error.DeviceError,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn stall(self: *const BootServices, microseconds: usize) uefi.UnexpectedError!void {
        switch (self._stall(microseconds)) {
            .success => {},
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn setWatchdogTimer(
        self: *BootServices,
        timeout: usize,
        watchdog_code: u64,
        data: ?[]const u16,
    ) SetWatchdogTimerError!void {
        switch (self._setWatchdogTimer(
            timeout,
            watchdog_code,
            if (data) |d| d.len else 0,
            if (data) |d| d.ptr else null,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .unsupported => return error.Unsupported,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// `driver_image` should be a null-terminated ordered list of handles.
    pub fn connectController(
        self: *BootServices,
        controller: Handle,
        driver_image: ?[*:null]?Handle,
        remaining_device_path: ?*const DevicePathProtocol,
        recursive: bool,
    ) ConnectControllerError!void {
        switch (self._connectController(
            controller,
            driver_image,
            remaining_device_path,
            recursive,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .not_found => return error.NotFound,
            .security_violation => return error.SecurityViolation,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn disconnectController(
        self: *BootServices,
        controller: Handle,
        driver_image: ?Handle,
        child: ?Handle,
    ) DisconnectControllerError!void {
        switch (self._disconnectController(
            controller,
            driver_image,
            child,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            .device_error => return error.DeviceError,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Opens a protocol with a structure as the loaded image for a UEFI application
    ///
    /// If `flag` is `.test_protocol`, then the only valid return value is `null`,
    /// and `Status.unsupported` is returned. Otherwise, if `_openProtocol` returns
    /// `Status.unsupported`, then `null` is returned.
    pub fn openProtocol(
        self: *BootServices,
        Protocol: type,
        handle: Handle,
        attributes: OpenProtocolArgs,
    ) OpenProtocolError!?*Protocol {
        if (!@hasDecl(Protocol, "guid"))
            @compileError("Protocol is missing guid: " ++ @typeName(Protocol));

        const agent_handle: ?Handle, const controller_handle: ?Handle = switch (attributes) {
            inline else => |arg| .{ arg.agent, arg.controller },
        };

        var ptr: ?*Protocol = undefined;

        switch (self._openProtocol(
            handle,
            &Protocol.guid,
            @as(*?*anyopaque, @ptrCast(&ptr)),
            agent_handle,
            controller_handle,
            std.meta.activeTag(attributes),
        )) {
            .success => return if (attributes == .test_protocol) null else ptr,
            .unsupported => return if (attributes == .test_protocol) error.Unsupported else null,
            .access_denied => return error.AccessDenied,
            .already_started => return error.AlreadyStarted,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn closeProtocol(
        self: *BootServices,
        handle: Handle,
        Protocol: type,
        agent: Handle,
        controller: ?Handle,
    ) CloseProtocolError!void {
        if (!@hasDecl(Protocol, "guid"))
            @compileError("protocol is missing guid: " ++ @typeName(Protocol));

        switch (self._closeProtocol(
            handle,
            &Protocol.guid,
            agent,
            controller,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn openProtocolInformation(
        self: *const BootServices,
        handle: Handle,
        Protocol: type,
    ) OpenProtocolInformationError!?[]ProtocolInformationEntry {
        var entries: [*]ProtocolInformationEntry = undefined;
        var len: usize = undefined;

        switch (self._openProtocolInformation(
            handle,
            &Protocol.guid,
            &entries,
            &len,
        )) {
            .success => return entries[0..len],
            .not_found => return null,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn protocolsPerHandle(
        self: *const BootServices,
        handle: Handle,
    ) ProtocolsPerHandleError![]*const Guid {
        var guids: [*]*const Guid = undefined;
        var len: usize = undefined;

        switch (self._protocolsPerHandle(
            handle,
            &guids,
            &len,
        )) {
            .success => return guids[0..len],
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn locateHandleBuffer(
        self: *const BootServices,
        search: LocateSearch,
    ) LocateHandleBufferError!?[]Handle {
        var handles: [*]Handle = undefined;
        var len: usize = undefined;

        switch (self._locateHandleBuffer(
            std.meta.activeTag(search),
            if (search == .by_protocol) search.by_protocol else null,
            if (search == .by_register_notify) search.by_register_notify else null,
            &len,
            &handles,
        )) {
            .success => return handles[0..len],
            .invalid_parameter => return error.InvalidParameter,
            .not_found => return null,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn locateProtocol(
        self: *const BootServices,
        Protocol: type,
        registration: ?EventRegistration,
    ) LocateProtocolError!?*Protocol {
        var interface: *Protocol = undefined;

        switch (self._locateProtocol(
            &Protocol.guid,
            registration,
            @ptrCast(&interface),
        )) {
            .success => return interface,
            .not_found => return null,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Installs a set of protocol interfaces into the boot services environment.
    ///
    /// This function's final argument should be a tuple of pointers to protocol
    /// interfaces. For example:
    ///
    /// ```
    /// const handle = try boot_services.installProtocolInterfaces(null, .{
    ///     &my_interface_1,
    ///     &my_interface_2,
    /// });
    /// ```
    ///
    /// The underlying function accepts a vararg list of pairs of Guid pointers
    /// and opaque pointers to the interface. To provide a guid, the interface
    /// types should declare a `guid` constant like so:
    ///
    /// ```
    /// pub const guid: uefi.Guid = .{ ... };
    /// ```
    ///
    /// See `std.os.uefi.protocol` for examples of protocol type definitions.
    pub fn installProtocolInterfaces(
        self: *BootServices,
        handle: ?Handle,
        interfaces: anytype,
    ) InstallProtocolInterfacesError!Handle {
        var hdl: ?Handle = handle;
        const args_tuple = protocolInterfaces(&hdl, interfaces);

        switch (@call(
            .auto,
            self._installMultipleProtocolInterfaces,
            args_tuple,
        )) {
            .success => return hdl.?,
            .already_started => return error.AlreadyStarted,
            .out_of_resources => return error.OutOfResources,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn uninstallProtocolInterfaces(
        self: *BootServices,
        handle: Handle,
        interfaces: anytype,
    ) UninstallProtocolInterfacesError!void {
        const args_tuple = protocolInterfaces(handle, interfaces);

        switch (@call(
            .auto,
            self._uninstallMultipleProtocolInterfaces,
            args_tuple,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn calculateCrc32(
        self: *const BootServices,
        data: []const u8,
    ) CalculateCrc32Error!u32 {
        var value: u32 = undefined;
        switch (self._calculateCrc32(data.ptr, data.len, &value)) {
            .success => return value,
            .invalid_parameter => return error.InvalidParameter,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const signature: u64 = 0x56524553544f4f42;

    pub const NotifyOpts = struct {
        tpl: TaskPriorityLevel = .application,
        function: ?*const fn (Event, ?*anyopaque) callconv(cc) void = null,
        context: ?*anyopaque = null,
    };

    pub const TaskPriorityLevel = enum(usize) {
        application = 4,
        callback = 8,
        notify = 16,
        high_level = 31,
        _,
    };

    pub const ImageExitData = struct {
        code: Status,
        description: ?[:0]const u16,
        data: ?[]const u16,
    };
};

fn protocolInterfaces(
    handle_arg: anytype,
    interfaces: anytype,
) ProtocolInterfaces(@TypeOf(handle_arg), @TypeOf(interfaces)) {
    var result: ProtocolInterfaces(
        @TypeOf(handle_arg),
        @TypeOf(interfaces),
    ) = undefined;
    result[0] = handle_arg;

    var idx: usize = 1;
    inline for (interfaces) |interface| {
        const InterfacePtr = @TypeOf(interface);
        const Interface = switch (@typeInfo(InterfacePtr)) {
            .pointer => |pointer| pointer.child,
            else => @compileError("expected tuple of '*const Protocol', got " ++ @typeName(InterfacePtr)),
        };

        if (!@hasDecl(Interface, "guid"))
            @compileError("protocol interface '" ++ @typeName(Interface) ++
                "' does not declare a 'const guid: uefi.Guid'.");

        switch (@typeInfo(Interface)) {
            .@"struct" => |struct_info| if (struct_info.layout != .@"extern")
                @compileLog("protocol interface '" ++ @typeName(Interface) ++
                    "' is not extern - this is likely a mistake"),
            else => @compileError("protocol interface must be a struct, got " ++ @typeName(Interface)),
        }

        result[idx] = &Interface.guid;
        result[idx + 1] = @ptrCast(interface);
        idx += 2;
    }

    return result;
}

fn ProtocolInterfaces(HandleType: type, Interfaces: type) type {
    const interfaces_type_info = @typeInfo(Interfaces);
    if (interfaces_type_info != .@"struct" or !interfaces_type_info.@"struct".is_tuple)
        @compileError("expected tuple of protocol interfaces, got " ++ @typeName(Interfaces));
    const interfaces_info = interfaces_type_info.@"struct";

    var tuple_types: [interfaces_info.fields.len * 2 + 1]type = undefined;
    tuple_types[0] = HandleType;
    var idx = 1;
    while (idx < tuple_types.len) : (idx += 2) {
        tuple_types[idx] = *const Guid;
        tuple_types[idx + 1] = *const anyopaque;
    }

    return std.meta.Tuple(tuple_types[0..]);
}



---
File: /std/os/uefi/tables/configuration_table.zig
---

const uefi = @import("std").os.uefi;
const Guid = uefi.Guid;

pub const ConfigurationTable = extern struct {
    vendor_guid: Guid,
    vendor_table: *anyopaque,

    pub const acpi_20_table_guid: Guid = .{
        .time_low = 0x8868e871,
        .time_mid = 0xe4f1,
        .time_high_and_version = 0x11d3,
        .clock_seq_high_and_reserved = 0xbc,
        .clock_seq_low = 0x22,
        .node = [_]u8{ 0x00, 0x80, 0xc7, 0x3c, 0x88, 0x81 },
    };
    pub const acpi_10_table_guid: Guid = .{
        .time_low = 0xeb9d2d30,
        .time_mid = 0x2d88,
        .time_high_and_version = 0x11d3,
        .clock_seq_high_and_reserved = 0x9a,
        .clock_seq_low = 0x16,
        .node = [_]u8{ 0x00, 0x90, 0x27, 0x3f, 0xc1, 0x4d },
    };
    pub const sal_system_table_guid: Guid = .{
        .time_low = 0xeb9d2d32,
        .time_mid = 0x2d88,
        .time_high_and_version = 0x113d,
        .clock_seq_high_and_reserved = 0x9a,
        .clock_seq_low = 0x16,
        .node = [_]u8{ 0x00, 0x90, 0x27, 0x3f, 0xc1, 0x4d },
    };
    pub const smbios_table_guid: Guid = .{
        .time_low = 0xeb9d2d31,
        .time_mid = 0x2d88,
        .time_high_and_version = 0x11d3,
        .clock_seq_high_and_reserved = 0x9a,
        .clock_seq_low = 0x16,
        .node = [_]u8{ 0x00, 0x90, 0x27, 0x3f, 0xc1, 0x4d },
    };
    pub const smbios3_table_guid: Guid = .{
        .time_low = 0xf2fd1544,
        .time_mid = 0x9794,
        .time_high_and_version = 0x4a2c,
        .clock_seq_high_and_reserved = 0x99,
        .clock_seq_low = 0x2e,
        .node = [_]u8{ 0xe5, 0xbb, 0xcf, 0x20, 0xe3, 0x94 },
    };
    pub const mps_table_guid: Guid = .{
        .time_low = 0xeb9d2d2f,
        .time_mid = 0x2d88,
        .time_high_and_version = 0x11d3,
        .clock_seq_high_and_reserved = 0x9a,
        .clock_seq_low = 0x16,
        .node = [_]u8{ 0x00, 0x90, 0x27, 0x3f, 0xc1, 0x4d },
    };
    pub const json_config_data_table_guid: Guid = .{
        .time_low = 0x87367f87,
        .time_mid = 0x1119,
        .time_high_and_version = 0x41ce,
        .clock_seq_high_and_reserved = 0xaa,
        .clock_seq_low = 0xec,
        .node = [_]u8{ 0x8b, 0xe0, 0x11, 0x1f, 0x55, 0x8a },
    };
    pub const json_capsule_data_table_guid: Guid = .{
        .time_low = 0x35e7a725,
        .time_mid = 0x8dd2,
        .time_high_and_version = 0x4cac,
        .clock_seq_high_and_reserved = 0x80,
        .clock_seq_low = 0x11,
        .node = [_]u8{ 0x33, 0xcd, 0xa8, 0x10, 0x90, 0x56 },
    };
    pub const json_capsule_result_table_guid: Guid = .{
        .time_low = 0xdbc461c3,
        .time_mid = 0xb3de,
        .time_high_and_version = 0x422a,
        .clock_seq_high_and_reserved = 0xb9,
        .clock_seq_low = 0xb4,
        .node = [_]u8{ 0x98, 0x86, 0xfd, 0x49, 0xa1, 0xe5 },
    };
};



---
File: /std/os/uefi/tables/runtime_services.zig
---

const std = @import("std");
const uefi = std.os.uefi;
const Guid = uefi.Guid;
const TableHeader = uefi.tables.TableHeader;
const Time = uefi.Time;
const TimeCapabilities = uefi.TimeCapabilities;
const Status = uefi.Status;
const MemoryDescriptor = uefi.tables.MemoryDescriptor;
const MemoryMapSlice = uefi.tables.MemoryMapSlice;
const ResetType = uefi.tables.ResetType;
const CapsuleHeader = uefi.tables.CapsuleHeader;
const PhysicalAddress = uefi.tables.PhysicalAddress;
const cc = uefi.cc;
const Error = Status.Error;

/// Runtime services are provided by the firmware before and after exitBootServices has been called.
///
/// As the runtime_services table may grow with new UEFI versions, it is important to check hdr.header_size.
///
/// Some functions may not be supported. Check the RuntimeServicesSupported variable using getVariable.
/// getVariable is one of the functions that may not be supported.
///
/// Some functions may not be called while other functions are running.
pub const RuntimeServices = extern struct {
    hdr: TableHeader,

    /// Returns the current time and date information, and the time-keeping capabilities of the hardware platform.
    _getTime: *const fn (time: *Time, capabilities: ?*TimeCapabilities) callconv(cc) Status,

    /// Sets the current local time and date information
    _setTime: *const fn (time: *const Time) callconv(cc) Status,

    /// Returns the current wakeup alarm clock setting
    _getWakeupTime: *const fn (enabled: *bool, pending: *bool, time: *Time) callconv(cc) Status,

    /// Sets the system wakeup alarm clock time
    _setWakeupTime: *const fn (enable: bool, time: ?*const Time) callconv(cc) Status,

    /// Changes the runtime addressing mode of EFI firmware from physical to virtual.
    _setVirtualAddressMap: *const fn (mmap_size: usize, descriptor_size: usize, descriptor_version: u32, virtual_map: [*]align(@alignOf(MemoryDescriptor)) u8) callconv(cc) Status,

    /// Determines the new virtual address that is to be used on subsequent memory accesses.
    _convertPointer: *const fn (debug_disposition: DebugDisposition, address: *?*anyopaque) callconv(cc) Status,

    /// Returns the value of a variable.
    _getVariable: *const fn (var_name: [*:0]const u16, vendor_guid: *const Guid, attributes: ?*VariableAttributes, data_size: *usize, data: ?*anyopaque) callconv(cc) Status,

    /// Enumerates the current variable names.
    _getNextVariableName: *const fn (var_name_size: *usize, var_name: ?[*:0]const u16, vendor_guid: *Guid) callconv(cc) Status,

    /// Sets the value of a variable.
    _setVariable: *const fn (var_name: [*:0]const u16, vendor_guid: *const Guid, attributes: VariableAttributes, data_size: usize, data: [*]const u8) callconv(cc) Status,

    /// Return the next high 32 bits of the platform's monotonic counter
    _getNextHighMonotonicCount: *const fn (high_count: *u32) callconv(cc) Status,

    /// Resets the entire platform.
    _resetSystem: *const fn (reset_type: ResetType, reset_status: Status, data_size: usize, reset_data: ?[*]const u16) callconv(cc) noreturn,

    /// Passes capsules to the firmware with both virtual and physical mapping.
    /// Depending on the intended consumption, the firmware may process the capsule immediately.
    /// If the payload should persist across a system reset, the reset value returned from
    /// `queryCapsuleCapabilities` must be passed into resetSystem and will cause the capsule
    /// to be processed by the firmware as part of the reset process.
    _updateCapsule: *const fn (capsule_header_array: [*]*const CapsuleHeader, capsule_count: usize, scatter_gather_list: PhysicalAddress) callconv(cc) Status,

    /// Returns if the capsule can be supported via `updateCapsule`
    _queryCapsuleCapabilities: *const fn (capsule_header_array: [*]*const CapsuleHeader, capsule_count: usize, maximum_capsule_size: *usize, reset_type: *ResetType) callconv(cc) Status,

    /// Returns information about the EFI variables
    _queryVariableInfo: *const fn (attributes: VariableAttributes, maximum_variable_storage_size: *u64, remaining_variable_storage_size: *u64, maximum_variable_size: *u64) callconv(cc) Status,

    pub const GetTimeError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };

    pub const SetTimeError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };

    pub const GetWakeupTimeError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };

    pub const SetWakeupTimeError = uefi.UnexpectedError || error{
        InvalidParameter,
        DeviceError,
        Unsupported,
    };

    pub const SetVirtualAddressMapError = uefi.UnexpectedError || error{
        Unsupported,
        NoMapping,
        NotFound,
    };

    pub const ConvertPointerError = uefi.UnexpectedError || error{
        InvalidParameter,
        Unsupported,
    };

    pub const GetVariableSizeError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };

    pub const GetVariableError = GetVariableSizeError || error{
        BufferTooSmall,
    };

    pub const SetVariableError = uefi.UnexpectedError || error{
        InvalidParameter,
        OutOfResources,
        DeviceError,
        WriteProtected,
        SecurityViolation,
        NotFound,
        Unsupported,
    };

    pub const GetNextHighMonotonicCountError = uefi.UnexpectedError || error{
        DeviceError,
        Unsupported,
    };

    pub const UpdateCapsuleError = uefi.UnexpectedError || error{
        InvalidParameter,
        DeviceError,
        Unsupported,
        OutOfResources,
    };

    pub const QueryCapsuleCapabilitiesError = uefi.UnexpectedError || error{
        Unsupported,
        OutOfResources,
    };

    pub const QueryVariableInfoError = uefi.UnexpectedError || error{
        InvalidParameter,
        Unsupported,
    };

    /// Returns the current time and the time capabilities of the platform.
    pub fn getTime(
        self: *const RuntimeServices,
    ) GetTimeError!struct { Time, TimeCapabilities } {
        var time: Time = undefined;
        var capabilities: TimeCapabilities = undefined;

        switch (self._getTime(&time, &capabilities)) {
            .success => return .{ time, capabilities },
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn setTime(self: *RuntimeServices, time: *const Time) SetTimeError!void {
        switch (self._setTime(time)) {
            .success => {},
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const GetWakeupTime = struct {
        enabled: bool,
        pending: bool,
        time: Time,
    };

    pub fn getWakeupTime(
        self: *const RuntimeServices,
    ) GetWakeupTimeError!GetWakeupTime {
        var result: GetWakeupTime = undefined;
        switch (self._getWakeupTime(
            &result.enabled,
            &result.pending,
            &result.time,
        )) {
            .success => return result,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const SetWakeupTime = union(enum) {
        enabled: *const Time,
        disabled,
    };

    pub fn setWakeupTime(
        self: *RuntimeServices,
        set: SetWakeupTime,
    ) SetWakeupTimeError!void {
        switch (self._setWakeupTime(
            set != .disabled,
            if (set == .enabled) set.enabled else null,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn setVirtualAddressMap(
        self: *RuntimeServices,
        map: MemoryMapSlice,
    ) SetVirtualAddressMapError!void {
        switch (self._setVirtualAddressMap(
            map.info.len * map.info.descriptor_size,
            map.info.descriptor_size,
            map.info.descriptor_version,
            @ptrCast(map.ptr),
        )) {
            .success => {},
            .unsupported => return error.Unsupported,
            .no_mapping => return error.NoMapping,
            .not_found => return error.NotFound,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn convertPointer(
        self: *const RuntimeServices,
        comptime disposition: DebugDisposition,
        cvt: @FieldType(PointerConversion, @tagName(disposition)),
    ) ConvertPointerError!?@FieldType(PointerConversion, @tagName(disposition)) {
        var pointer = cvt;

        switch (self._convertPointer(disposition, @ptrCast(&pointer))) {
            .success => return pointer,
            .not_found => return null,
            .invalid_parameter => return error.InvalidParameter,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// Returns the length of the variable's data and its attributes.
    pub fn getVariableSize(
        self: *const RuntimeServices,
        name: [*:0]const u16,
        guid: *const Guid,
    ) GetVariableSizeError!?struct { usize, VariableAttributes } {
        var size: usize = 0;
        var attrs: VariableAttributes = undefined;

        switch (self._getVariable(
            name,
            guid,
            &attrs,
            &size,
            null,
        )) {
            .buffer_too_small => return .{ size, attrs },
            .not_found => return null,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    /// To determine the minimum necessary buffer size for the variable, call
    /// `getVariableSize` first.
    pub fn getVariable(
        self: *const RuntimeServices,
        name: [*:0]const u16,
        guid: *const Guid,
        buffer: []u8,
    ) GetVariableError!?struct { []u8, VariableAttributes } {
        var attrs: VariableAttributes = undefined;
        var len = buffer.len;

        switch (self._getVariable(
            name,
            guid,
            &attrs,
            &len,
            buffer.ptr,
        )) {
            .success => return .{ buffer[0..len], attrs },
            .not_found => return null,
            .buffer_too_small => return error.BufferTooSmall,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn variableNameIterator(
        self: *const RuntimeServices,
        buffer: []u16,
    ) VariableNameIterator {
        buffer[0] = 0;
        return .{
            .services = self,
            .buffer = buffer,
            .guid = undefined,
        };
    }

    pub fn setVariable(
        self: *RuntimeServices,
        name: [*:0]const u16,
        guid: *const Guid,
        attributes: VariableAttributes,
        data: []const u8,
    ) SetVariableError!void {
        switch (self._setVariable(
            name,
            guid,
            attributes,
            data.len,
            data.ptr,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .out_of_resources => return error.OutOfResources,
            .device_error => return error.DeviceError,
            .write_protected => return error.WriteProtected,
            .security_violation => return error.SecurityViolation,
            .not_found => return error.NotFound,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn getNextHighMonotonicCount(self: *const RuntimeServices) GetNextHighMonotonicCountError!u32 {
        var cnt: u32 = undefined;
        switch (self._getNextHighMonotonicCount(&cnt)) {
            .success => return cnt,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn resetSystem(
        self: *RuntimeServices,
        reset_type: ResetType,
        reset_status: Status,
        data: ?[]align(2) const u8,
    ) noreturn {
        self._resetSystem(
            reset_type,
            reset_status,
            if (data) |d| d.len else 0,
            if (data) |d| @ptrCast(@alignCast(d.ptr)) else null,
        );
    }

    pub fn updateCapsule(
        self: *RuntimeServices,
        capsules: []*const CapsuleHeader,
        scatter_gather_list: PhysicalAddress,
    ) UpdateCapsuleError!void {
        switch (self._updateCapsule(
            capsules.ptr,
            capsules.len,
            scatter_gather_list,
        )) {
            .success => {},
            .invalid_parameter => return error.InvalidParameter,
            .device_error => return error.DeviceError,
            .unsupported => return error.Unsupported,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn queryCapsuleCapabilities(
        self: *const RuntimeServices,
        capsules: []*const CapsuleHeader,
    ) QueryCapsuleCapabilitiesError!struct { u64, ResetType } {
        var max_capsule_size: u64 = undefined;
        var reset_type: ResetType = undefined;

        switch (self._queryCapsuleCapabilities(
            capsules.ptr,
            capsules.len,
            &max_capsule_size,
            &reset_type,
        )) {
            .success => return .{ max_capsule_size, reset_type },
            .unsupported => return error.Unsupported,
            .out_of_resources => return error.OutOfResources,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub fn queryVariableInfo(
        self: *const RuntimeServices,
        // Note: .append_write is ignored
        attributes: VariableAttributes,
    ) QueryVariableInfoError!VariableInfo {
        var res: VariableInfo = undefined;

        switch (self._queryVariableInfo(
            attributes,
            &res.max_variable_storage_size,
            &res.remaining_variable_storage_size,
            &res.max_variable_size,
        )) {
            .success => return res,
            .invalid_parameter => return error.InvalidParameter,
            .unsupported => return error.Unsupported,
            else => |status| return uefi.unexpectedStatus(status),
        }
    }

    pub const DebugDisposition = enum(usize) {
        const Bits = packed struct(usize) {
            optional_ptr: bool = false,
            _pad: std.meta.Int(.unsigned, @bitSizeOf(usize) - 1) = 0,
        };

        pointer = @bitCast(Bits{}),
        optional = @bitCast(Bits{ .optional_ptr = true }),
        _,
    };

    pub const PointerConversion = union(DebugDisposition) {
        pointer: *anyopaque,
        optional: ?*anyopaque,
    };

    pub const VariableAttributes = packed struct(u32) {
        non_volatile: bool = false,
        bootservice_access: bool = false,
        runtime_access: bool = false,
        hardware_error_record: bool = false,
        /// Note: deprecated and should be
```
