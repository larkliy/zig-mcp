```
);
            try testing.expectEqualStrings("100-continue", req.expect.?);

            try testing.expectEqual(true, req.keep_alive);
            try testing.expectEqual(10, req.content_length.?);
            try testing.expectEqual(.chunked, req.transfer_encoding);
            try testing.expectEqual(.deflate, req.transfer_compression);
        }

        inline fn int64(array: *const [8]u8) u64 {
            return @bitCast(array.*);
        }

        /// Help the programmer avoid bugs by calling this when the string
        /// memory of `Head` becomes invalidated.
        fn invalidateStrings(h: *Head) void {
            h.target = undefined;
            if (h.expect) |*s| s.* = undefined;
            if (h.content_type) |*s| s.* = undefined;
        }
    };

    pub fn iterateHeaders(r: *const Request) http.HeaderIterator {
        assert(r.server.reader.state == .received_head);
        return http.HeaderIterator.init(r.head_buffer);
    }

    test iterateHeaders {
        const request_bytes = "GET /hi HTTP/1.0\r\n" ++
            "content-tYpe: text/plain\r\n" ++
            "content-Length:10\r\n" ++
            "expeCt:   100-continue \r\n" ++
            "TRansfer-encoding:\tdeflate, chunked \r\n" ++
            "connectioN:\t keep-alive \r\n\r\n";

        var server: Server = .{
            .reader = .{
                .in = undefined,
                .state = .received_head,
                .interface = undefined,
                .max_head_len = 4096,
            },
            .out = undefined,
        };

        var request: Request = .{
            .server = &server,
            .head = undefined,
            .head_buffer = @constCast(request_bytes),
        };

        var it = request.iterateHeaders();
        {
            const header = it.next().?;
            try testing.expectEqualStrings("content-tYpe", header.name);
            try testing.expectEqualStrings("text/plain", header.value);
            try testing.expect(!it.is_trailer);
        }
        {
            const header = it.next().?;
            try testing.expectEqualStrings("content-Length", header.name);
            try testing.expectEqualStrings("10", header.value);
            try testing.expect(!it.is_trailer);
        }
        {
            const header = it.next().?;
            try testing.expectEqualStrings("expeCt", header.name);
            try testing.expectEqualStrings("100-continue", header.value);
            try testing.expect(!it.is_trailer);
        }
        {
            const header = it.next().?;
            try testing.expectEqualStrings("TRansfer-encoding", header.name);
            try testing.expectEqualStrings("deflate, chunked", header.value);
            try testing.expect(!it.is_trailer);
        }
        {
            const header = it.next().?;
            try testing.expectEqualStrings("connectioN", header.name);
            try testing.expectEqualStrings("keep-alive", header.value);
            try testing.expect(!it.is_trailer);
        }
        try testing.expectEqual(null, it.next());
    }

    pub const RespondOptions = struct {
        version: http.Version = .@"HTTP/1.1",
        status: http.Status = .ok,
        reason: ?[]const u8 = null,
        keep_alive: bool = true,
        extra_headers: []const http.Header = &.{},
        transfer_encoding: ?http.TransferEncoding = null,
    };

    /// Send an entire HTTP response to the client, including headers and body.
    ///
    /// Automatically handles HEAD requests by omitting the body.
    ///
    /// Unless `transfer_encoding` is specified, uses the "content-length"
    /// header.
    ///
    /// If the request contains a body and the connection is to be reused,
    /// discards the request body, leaving the Server in the `ready` state. If
    /// this discarding fails, the connection is marked as not to be reused and
    /// no error is surfaced.
    ///
    /// Asserts status is not `continue`.
    /// Asserts that "\r\n" does not occur in any header name or value.
    pub fn respond(
        request: *Request,
        content: []const u8,
        options: RespondOptions,
    ) ExpectContinueError!void {
        try respondUnflushed(request, content, options);
        try request.server.out.flush();
    }

    pub fn respondUnflushed(
        request: *Request,
        content: []const u8,
        options: RespondOptions,
    ) ExpectContinueError!void {
        assert(options.status != .@"continue");
        if (std.debug.runtime_safety) {
            for (options.extra_headers) |header| {
                assert(header.name.len != 0);
                assert(std.mem.findScalar(u8, header.name, ':') == null);
                assert(std.mem.findPosLinear(u8, header.name, 0, "\r\n") == null);
                assert(std.mem.findPosLinear(u8, header.value, 0, "\r\n") == null);
            }
        }
        try writeExpectContinue(request);

        const transfer_encoding_none = (options.transfer_encoding orelse .chunked) == .none;
        const server_keep_alive = !transfer_encoding_none and options.keep_alive;
        const keep_alive = request.discardBody(server_keep_alive);

        const phrase = options.reason orelse options.status.phrase() orelse "";

        const out = request.server.out;
        try out.print("{s} {d} {s}\r\n", .{
            @tagName(options.version), @intFromEnum(options.status), phrase,
        });

        switch (options.version) {
            .@"HTTP/1.0" => if (keep_alive) try out.writeAll("connection: keep-alive\r\n"),
            .@"HTTP/1.1" => if (!keep_alive) try out.writeAll("connection: close\r\n"),
        }

        if (options.transfer_encoding) |transfer_encoding| switch (transfer_encoding) {
            .none => {},
            .chunked => try out.writeAll("transfer-encoding: chunked\r\n"),
        } else {
            try out.print("content-length: {d}\r\n", .{content.len});
        }

        for (options.extra_headers) |header| {
            var vecs: [4][]const u8 = .{ header.name, ": ", header.value, "\r\n" };
            try out.writeVecAll(&vecs);
        }

        try out.writeAll("\r\n");

        if (request.head.method != .HEAD) {
            const is_chunked = (options.transfer_encoding orelse .none) == .chunked;
            if (is_chunked) {
                if (content.len > 0) try out.print("{x}\r\n{s}\r\n", .{ content.len, content });
                try out.writeAll("0\r\n\r\n");
            } else if (content.len > 0) {
                try out.writeAll(content);
            }
        }
    }

    pub const RespondStreamingOptions = struct {
        /// If provided, the response will use the content-length header;
        /// otherwise it will use transfer-encoding: chunked.
        content_length: ?u64 = null,
        /// Options that are shared with the `respond` method.
        respond_options: RespondOptions = .{},
    };

    /// The header is not guaranteed to be sent until `BodyWriter.flush` or
    /// `BodyWriter.end` is called.
    ///
    /// If the request contains a body and the connection is to be reused,
    /// discards the request body, leaving the Server in the `ready` state. If
    /// this discarding fails, the connection is marked as not to be reused and
    /// no error is surfaced.
    ///
    /// HEAD requests are handled transparently by setting the
    /// `BodyWriter.elide` flag on the returned `BodyWriter`, causing
    /// the response stream to omit the body. However, it may be worth noticing
    /// that flag and skipping any expensive work that would otherwise need to
    /// be done to satisfy the request.
    ///
    /// Asserts status is not `continue`.
    pub fn respondStreaming(
        request: *Request,
        buffer: []u8,
        options: RespondStreamingOptions,
    ) ExpectContinueError!http.BodyWriter {
        try writeExpectContinue(request);
        const o = options.respond_options;
        assert(o.status != .@"continue");
        const transfer_encoding_none = (o.transfer_encoding orelse .chunked) == .none;
        const server_keep_alive = !transfer_encoding_none and o.keep_alive;
        const keep_alive = request.discardBody(server_keep_alive);
        const phrase = o.reason orelse o.status.phrase() orelse "";
        const out = request.server.out;

        try out.print("{s} {d} {s}\r\n", .{
            @tagName(o.version), @intFromEnum(o.status), phrase,
        });

        switch (o.version) {
            .@"HTTP/1.0" => if (keep_alive) try out.writeAll("connection: keep-alive\r\n"),
            .@"HTTP/1.1" => if (!keep_alive) try out.writeAll("connection: close\r\n"),
        }

        if (o.transfer_encoding) |transfer_encoding| switch (transfer_encoding) {
            .chunked => try out.writeAll("transfer-encoding: chunked\r\n"),
            .none => {},
        } else if (options.content_length) |len| {
            try out.print("content-length: {d}\r\n", .{len});
        } else {
            try out.writeAll("transfer-encoding: chunked\r\n");
        }

        for (o.extra_headers) |header| {
            assert(header.name.len != 0);
            var bufs: [4][]const u8 = .{ header.name, ": ", header.value, "\r\n" };
            try out.writeVecAll(&bufs);
        }

        try out.writeAll("\r\n");
        const elide_body = request.head.method == .HEAD;
        const state: http.BodyWriter.State = if (o.transfer_encoding) |te| switch (te) {
            .chunked => .init_chunked,
            .none => .none,
        } else if (options.content_length) |len| .{
            .content_length = len,
        } else .init_chunked;

        return if (elide_body) .{
            .http_protocol_output = request.server.out,
            .state = state,
            .writer = .{
                .buffer = buffer,
                .vtable = &.{
                    .drain = http.BodyWriter.elidingDrain,
                    .sendFile = http.BodyWriter.elidingSendFile,
                },
            },
        } else .{
            .http_protocol_output = request.server.out,
            .state = state,
            .writer = .{
                .buffer = buffer,
                .vtable = switch (state) {
                    .none => &.{
                        .drain = http.BodyWriter.noneDrain,
                        .sendFile = http.BodyWriter.noneSendFile,
                    },
                    .content_length => &.{
                        .drain = http.BodyWriter.contentLengthDrain,
                        .sendFile = http.BodyWriter.contentLengthSendFile,
                    },
                    .chunk_len => &.{
                        .drain = http.BodyWriter.chunkedDrain,
                        .sendFile = http.BodyWriter.chunkedSendFile,
                    },
                    .end => unreachable,
                },
            },
        };
    }

    pub const UpgradeRequest = union(enum) {
        websocket: ?[]const u8,
        other: []const u8,
        none,
    };

    /// Does not invalidate `request.head`.
    pub fn upgradeRequested(request: *const Request) UpgradeRequest {
        switch (request.head.version) {
            .@"HTTP/1.0" => return .none,
            .@"HTTP/1.1" => if (request.head.method != .GET) return .none,
        }

        var sec_websocket_key: ?[]const u8 = null;
        var upgrade_name: ?[]const u8 = null;
        var it = request.iterateHeaders();
        while (it.next()) |header| {
            if (std.ascii.eqlIgnoreCase(header.name, "sec-websocket-key")) {
                sec_websocket_key = header.value;
            } else if (std.ascii.eqlIgnoreCase(header.name, "upgrade")) {
                upgrade_name = header.value;
            }
        }

        const name = upgrade_name orelse return .none;
        if (std.ascii.eqlIgnoreCase(name, "websocket")) return .{ .websocket = sec_websocket_key };
        return .{ .other = name };
    }

    pub const WebSocketOptions = struct {
        /// The value from `UpgradeRequest.websocket` (sec-websocket-key header value).
        key: []const u8,
        reason: ?[]const u8 = null,
        extra_headers: []const http.Header = &.{},
    };

    /// The header is not guaranteed to be sent until `WebSocket.flush` is
    /// called on the returned struct.
    pub fn respondWebSocket(request: *Request, options: WebSocketOptions) ExpectContinueError!WebSocket {
        if (request.head.expect != null) return error.HttpExpectationFailed;

        const out = request.server.out;
        const version: http.Version = .@"HTTP/1.1";
        const status: http.Status = .switching_protocols;
        const phrase = options.reason orelse status.phrase() orelse "";

        assert(request.head.version == version);
        assert(request.head.method == .GET);

        var sha1 = std.crypto.hash.Sha1.init(.{});
        sha1.update(options.key);
        sha1.update("258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
        var digest: [std.crypto.hash.Sha1.digest_length]u8 = undefined;
        sha1.final(&digest);
        try out.print("{s} {d} {s}\r\n", .{ @tagName(version), @intFromEnum(status), phrase });
        try out.writeAll("connection: upgrade\r\nupgrade: websocket\r\nsec-websocket-accept: ");
        const base64_digest = try out.writableArray(28);
        assert(std.base64.standard.Encoder.encode(base64_digest, &digest).len == base64_digest.len);
        try out.writeAll("\r\n");

        for (options.extra_headers) |header| {
            assert(header.name.len != 0);
            var bufs: [4][]const u8 = .{ header.name, ": ", header.value, "\r\n" };
            try out.writeVecAll(&bufs);
        }

        try out.writeAll("\r\n");

        return .{
            .input = request.server.reader.in,
            .output = request.server.out,
            .key = options.key,
        };
    }

    /// In the case that the request contains "expect: 100-continue", this
    /// function writes the continuation header, which means it can fail with a
    /// write error. After sending the continuation header, it sets the
    /// request's expect field to `null`.
    ///
    /// Asserts that this function is only called once.
    ///
    /// See `readerExpectNone` for an infallible alternative that cannot write
    /// to the server output stream.
    pub fn readerExpectContinue(request: *Request, buffer: []u8) ExpectContinueError!*Reader {
        const flush = request.head.expect != null;
        try writeExpectContinue(request);
        if (flush) try request.server.out.flush();
        return readerExpectNone(request, buffer);
    }

    /// Asserts the expect header is `null`. The caller must handle the
    /// expectation manually and then set the value to `null` prior to calling
    /// this function.
    ///
    /// Asserts that this function is only called once.
    ///
    /// Invalidates the string memory inside `Head`.
    pub fn readerExpectNone(request: *Request, buffer: []u8) *Reader {
        assert(request.server.reader.state == .received_head);
        assert(request.head.expect == null);
        request.head.invalidateStrings();
        if (!request.head.method.requestHasBody()) return .ending;
        return request.server.reader.bodyReader(buffer, request.head.transfer_encoding, request.head.content_length);
    }

    pub const ExpectContinueError = error{
        /// Failed to write "HTTP/1.1 100 Continue\r\n\r\n" to the stream.
        WriteFailed,
        /// The client sent an expect HTTP header value other than
        /// "100-continue".
        HttpExpectationFailed,
    };

    pub fn writeExpectContinue(request: *Request) ExpectContinueError!void {
        const expect = request.head.expect orelse return;
        if (!mem.eql(u8, expect, "100-continue")) return error.HttpExpectationFailed;
        try request.server.out.writeAll("HTTP/1.1 100 Continue\r\n\r\n");
        request.head.expect = null;
    }

    /// Returns whether the connection should remain persistent.
    ///
    /// If it would fail, it instead sets the Server state to receiving body
    /// and returns false.
    fn discardBody(request: *Request, keep_alive: bool) bool {
        // Prepare to receive another request on the same connection.
        // There are two factors to consider:
        // * Any body the client sent must be discarded.
        // * The Server's read_buffer may already have some bytes in it from
        //   whatever came after the head, which may be the next HTTP request
        //   or the request body.
        // If the connection won't be kept alive, then none of this matters
        // because the connection will be severed after the response is sent.
        const r = &request.server.reader;
        if (keep_alive and request.head.keep_alive) switch (r.state) {
            .received_head => {
                if (request.head.method.requestHasBody()) {
                    assert(request.head.transfer_encoding != .none or request.head.content_length != null);
                    const reader_interface = request.readerExpectContinue(&.{}) catch return false;
                    _ = reader_interface.discardRemaining() catch return false;
                    assert(r.state == .ready);
                } else {
                    r.state = .ready;
                }
                return true;
            },
            .body_remaining_content_length, .body_remaining_chunk_len, .body_none, .ready => return true,
            else => unreachable,
        };

        // Avoid clobbering the state in case a reading stream already exists.
        switch (r.state) {
            .received_head => r.state = .closing,
            else => {},
        }
        return false;
    }
};

/// See https://tools.ietf.org/html/rfc6455
pub const WebSocket = struct {
    key: []const u8,
    input: *Reader,
    output: *Writer,

    pub const Header0 = packed struct(u8) {
        opcode: Opcode,
        rsv3: u1 = 0,
        rsv2: u1 = 0,
        rsv1: u1 = 0,
        fin: bool,
    };

    pub const Header1 = packed struct(u8) {
        payload_len: enum(u7) {
            len16 = 126,
            len64 = 127,
            _,
        },
        mask: bool,
    };

    pub const Opcode = enum(u4) {
        continuation = 0,
        text = 1,
        binary = 2,
        connection_close = 8,
        ping = 9,
        /// "A Pong frame MAY be sent unsolicited. This serves as a unidirectional
        /// heartbeat. A response to an unsolicited Pong frame is not expected."
        pong = 10,
        _,
    };

    pub const ReadSmallTextMessageError = error{
        ConnectionClose,
        UnexpectedOpCode,
        MessageOversize,
        MissingMaskBit,
        ReadFailed,
        EndOfStream,
    };

    pub const SmallMessage = struct {
        /// Can be text, binary, or ping.
        opcode: Opcode,
        data: []u8,
    };

    /// Reads the next message from the WebSocket stream, failing if the
    /// message does not fit into the input buffer. The returned memory points
    /// into the input buffer and is invalidated on the next read.
    pub fn readSmallMessage(ws: *WebSocket) ReadSmallTextMessageError!SmallMessage {
        const in = ws.input;
        while (true) {
            const header = try in.takeArray(2);
            const h0: Header0 = @bitCast(header[0]);
            const h1: Header1 = @bitCast(header[1]);

            switch (h0.opcode) {
                .text, .binary, .pong, .ping => {},
                .connection_close => return error.ConnectionClose,
                .continuation => return error.UnexpectedOpCode,
                _ => return error.UnexpectedOpCode,
            }

            if (!h0.fin) return error.MessageOversize;
            if (!h1.mask) return error.MissingMaskBit;

            const len: usize = switch (h1.payload_len) {
                .len16 => try in.takeInt(u16, .big),
                .len64 => std.math.cast(usize, try in.takeInt(u64, .big)) orelse return error.MessageOversize,
                else => @intFromEnum(h1.payload_len),
            };
            if (len > in.buffer.len) return error.MessageOversize;
            const mask: u32 = @bitCast((try in.takeArray(4)).*);
            const payload = try in.take(len);

            // Skip pongs.
            if (h0.opcode == .pong) continue;

            // The last item may contain a partial word of unused data.
            const floored_len = (payload.len / 4) * 4;
            const u32_payload: []align(1) u32 = @ptrCast(payload[0..floored_len]);
            for (u32_payload) |*elem| elem.* ^= mask;
            const mask_bytes: []const u8 = @ptrCast(&mask);
            for (payload[floored_len..], mask_bytes[0 .. payload.len - floored_len]) |*leftover, m|
                leftover.* ^= m;

            return .{
                .opcode = h0.opcode,
                .data = payload,
            };
        }
    }

    pub fn writeMessage(ws: *WebSocket, data: []const u8, op: Opcode) Writer.Error!void {
        var bufs: [1][]const u8 = .{data};
        try writeMessageVecUnflushed(ws, &bufs, op);
        try ws.output.flush();
    }

    pub fn writeMessageUnflushed(ws: *WebSocket, data: []const u8, op: Opcode) Writer.Error!void {
        var bufs: [1][]const u8 = .{data};
        try writeMessageVecUnflushed(ws, &bufs, op);
    }

    pub fn writeMessageVec(ws: *WebSocket, data: [][]const u8, op: Opcode) Writer.Error!void {
        try writeMessageVecUnflushed(ws, data, op);
        try ws.output.flush();
    }

    pub fn writeMessageVecUnflushed(ws: *WebSocket, data: [][]const u8, op: Opcode) Writer.Error!void {
        const total_len = l: {
            var total_len: u64 = 0;
            for (data) |iovec| total_len += iovec.len;
            break :l total_len;
        };
        const out = ws.output;
        try out.writeByte(@bitCast(@as(Header0, .{
            .opcode = op,
            .fin = true,
        })));
        switch (total_len) {
            0...125 => try out.writeByte(@bitCast(@as(Header1, .{
                .payload_len = @enumFromInt(total_len),
                .mask = false,
            }))),
            126...0xffff => {
                try out.writeByte(@bitCast(@as(Header1, .{
                    .payload_len = .len16,
                    .mask = false,
                })));
                try out.writeInt(u16, @intCast(total_len), .big);
            },
            else => {
                try out.writeByte(@bitCast(@as(Header1, .{
                    .payload_len = .len64,
                    .mask = false,
                })));
                try out.writeInt(u64, total_len, .big);
            },
        }
        try out.writeVecAll(data);
    }

    pub fn flush(ws: *WebSocket) Writer.Error!void {
        try ws.output.flush();
    }
};



---
File: /std/http/test.zig
---




---
File: /std/Io/File/Atomic.zig
---

const Atomic = @This();

const std = @import("../../std.zig");
const Io = std.Io;
const File = std.Io.File;
const Dir = std.Io.Dir;
const assert = std.debug.assert;

file: File,
file_basename_hex: u64,
file_open: bool,
file_exists: bool,

dir: Dir,
close_dir_on_deinit: bool,

dest_sub_path: []const u8,

pub const InitError = File.OpenError;

/// To release all resources, always call `deinit`, even after a successful
/// `finish`.
pub fn deinit(af: *Atomic, io: Io) void {
    if (af.file_open) {
        af.file.close(io);
        af.file_open = false;
    }
    if (af.file_exists) {
        const tmp_sub_path = std.fmt.hex(af.file_basename_hex);
        af.dir.deleteFile(io, &tmp_sub_path) catch {};
        af.file_exists = false;
    }
    if (af.close_dir_on_deinit) {
        af.dir.close(io);
        af.close_dir_on_deinit = false;
    }
    af.* = undefined;
}

pub const LinkError = File.HardLinkError || Dir.RenamePreserveError;

/// Atomically materializes the file into place, failing with
/// `error.PathAlreadyExists` if something already exists there.
///
/// If this operation could not be done with an unnamed temporary file, the
/// named temporary file will be deleted in a following operation, which may
/// independently fail. The result of that operation is stored in `delete_err`.
pub fn link(af: *Atomic, io: Io) LinkError!void {
    if (af.file_exists) {
        if (af.file_open) {
            af.file.close(io);
            af.file_open = false;
        }
        const tmp_sub_path = std.fmt.hex(af.file_basename_hex);
        try af.dir.renamePreserve(&tmp_sub_path, af.dir, af.dest_sub_path, io);
        af.file_exists = false;
    } else {
        assert(af.file_open);
        try af.file.hardLink(io, af.dir, af.dest_sub_path, .{});
        af.file.close(io);
        af.file_open = false;
    }
}

pub const ReplaceError = Dir.RenameError;

/// Atomically materializes the file into place, replacing any file that
/// already exists there.
///
/// Calling this function requires setting `CreateFileAtomicOptions.replace` to
/// `true`.
///
/// On Windows, this function introduces a period of time where some file
/// system operations on the destination file will result in
/// `error.AccessDenied`, including rename operations (such as the one used in
/// this function).
pub fn replace(af: *Atomic, io: Io) ReplaceError!void {
    assert(af.file_exists); // Wrong value for `CreateFileAtomicOptions.replace`.
    if (af.file_open) {
        af.file.close(io);
        af.file_open = false;
    }
    const tmp_sub_path = std.fmt.hex(af.file_basename_hex);
    try af.dir.rename(&tmp_sub_path, af.dir, af.dest_sub_path, io);
    af.file_exists = false;
}



---
File: /std/Io/File/MemoryMap.zig
---

const MemoryMap = @This();

const builtin = @import("builtin");
const native_os = builtin.os.tag;
const is_windows = native_os == .windows;

const std = @import("../../std.zig");
const Io = std.Io;
const File = Io.File;
const Allocator = std.mem.Allocator;

file: File,
/// Byte index inside `file` where `memory` starts. Page-aligned.
offset: u64,
/// Memory that may or may not remain consistent with file contents. Use `read`
/// and `write` to ensure synchronization points. Length has no alignment
/// requirement.
memory: []align(std.heap.page_size_min) u8,
/// Tells whether it is memory-mapped or file operations. On Windows this also
/// has a section handle.
section: ?Section,

pub const Section = if (is_windows) std.os.windows.HANDLE else void;

pub const CreateError = error{
    /// One of the following:
    /// * The `File.Kind` is not `file`.
    /// * The file is not open for reading and read access protections enabled.
    /// * The file is not open for writing and write access protections enabled.
    AccessDenied,
    /// The `prot` argument asks for `PROT_EXEC` but the mapped area belongs to a file on
    /// a filesystem that was mounted no-exec.
    PermissionDenied,
    LockedMemoryLimitExceeded,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
} || Allocator.Error || File.ReadPositionalError;

pub const CreateOptions = struct {
    /// Size of the mapping, in bytes. If this is longer than the file size,
    /// `memory` beyond the file end will be filled with zeroes and it is
    /// unspecified whether, after calling `write`, the file length will be
    /// set to `len` or remain unchanged.
    ///
    /// This value has no minimum alignment requirement, but may gain
    /// efficiency benefits from being a multiple of `File.Stat.block_size`.
    len: usize,
    /// When this has read set to false, bytes that are not modified before a
    /// sync may have the original file contents, or may be set to zero.
    protection: std.process.MemoryProtection = .{ .read = true, .write = true },
    /// If set to `true`, allows bytes observed before calling `read` to be
    /// undefined, and bytes unwritten before calling `write` to write
    /// undefined memory to the file.
    undefined_contents: bool = false,
    /// Prefault the pages. If this option is unsupported, it is silently
    /// ignored. Aside from custom Io implementations, this option is only
    /// supported on Linux.
    populate: bool = true,
    /// Asserted to be a multiple of page size which can be obtained via
    /// `std.heap.pageSize`.
    offset: u64 = 0,
};

/// To release the resources associated with the returned `MemoryMap`, call
/// `destroy`.
pub fn create(io: Io, file: File, options: CreateOptions) CreateError!MemoryMap {
    return io.vtable.fileMemoryMapCreate(io.userdata, file, options);
}

/// If `write` is not called before this function, changes to `memory` may or may
/// not be synchronized to `file`.
pub fn destroy(mm: *MemoryMap, io: Io) void {
    io.vtable.fileMemoryMapDestroy(io.userdata, mm);
}

pub const SetLengthError = error{
    /// Changing the mapping length could not be done atomically. Caller must
    /// use `destroy` and `create` to resize the mapping.
    OperationUnsupported,
    /// One of the following:
    /// * The `File.Kind` is not `file`.
    /// * The file is not open for reading and read access protections enabled.
    /// * The file is not open for writing and write access protections enabled.
    AccessDenied,
    /// The `prot` argument asks for `PROT_EXEC` but the mapped area belongs to a file on
    /// a filesystem that was mounted no-exec.
    PermissionDenied,
    LockedMemoryLimitExceeded,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
} || Allocator.Error || File.SetLengthError;

/// Change the size of the mapping. This does not sync the contents. The size
/// of the file after calling this is unspecified until `write` is called.
///
/// May change the pointer address of `memory`.
pub fn setLength(mm: *MemoryMap, io: Io, new_len: usize) SetLengthError!void {
    return io.vtable.fileMemoryMapSetLength(io.userdata, mm, new_len);
}

/// Synchronizes the contents of `memory` from `file`.
pub fn read(mm: *MemoryMap, io: Io) File.ReadPositionalError!void {
    return io.vtable.fileMemoryMapRead(io.userdata, mm);
}

/// Synchronizes the contents of `memory` to `file`.
///
/// If `memory.len` is greater than file size, the bytes beyond the end of the
/// file may be dropped, or they may be written, extending the size of the
/// file.
pub fn write(mm: *MemoryMap, io: Io) File.WritePositionalError!void {
    return io.vtable.fileMemoryMapWrite(io.userdata, mm);
}



---
File: /std/Io/File/MultiReader.zig
---

const MultiReader = @This();

const std = @import("../../std.zig");
const Io = std.Io;
const File = Io.File;
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;

gpa: Allocator,
streams: *Streams,
batch: Io.Batch,

pub const Context = struct {
    mr: *MultiReader,
    fr: File.Reader,
    vec: [1][]u8,
    err: ?Error,
};

pub const Error = UnendingError || error{EndOfStream};
pub const UnendingError = Allocator.Error || File.Reader.Error || Io.ConcurrentError;

/// Trailing:
/// * `contexts: [len]Context`
/// * `storage: [len]Io.Operation.Storage`
pub const Streams = extern struct {
    len: u32,

    pub fn contexts(s: *Streams) []Context {
        const base: usize = @intFromPtr(s);
        const ptr: [*]Context = @ptrFromInt(std.mem.alignForward(usize, base + @sizeOf(Streams), @alignOf(Context)));
        return ptr[0..s.len];
    }

    pub fn storage(s: *Streams) []Io.Operation.Storage {
        const prev = contexts(s);
        const end = prev.ptr + prev.len;
        const ptr: [*]Io.Operation.Storage = @ptrFromInt(std.mem.alignForward(usize, @intFromPtr(end), @alignOf(Io.Operation.Storage)));
        return ptr[0..s.len];
    }
};

pub fn Buffer(comptime n: usize) type {
    return extern struct {
        len: u32,
        contexts: [n][@sizeOf(Context)]u8 align(@alignOf(Context)),
        storage: [n][@sizeOf(Io.Operation.Storage)]u8 align(@alignOf(Io.Operation.Storage)),

        pub fn toStreams(b: *@This()) *Streams {
            b.len = n;
            return @ptrCast(b);
        }
    };
}

/// See `Streams.Buffer` for convenience API to obtain the `streams` parameter.
pub fn init(mr: *MultiReader, gpa: Allocator, io: Io, streams: *Streams, files: []const File) void {
    const contexts = streams.contexts();
    for (contexts, files) |*context, file| context.* = .{
        .mr = mr,
        .fr = .{
            .io = io,
            .file = file,
            .mode = .streaming,
            .interface = .{
                .vtable = &.{
                    .stream = stream,
                    .discard = discard,
                    .readVec = readVec,
                    .rebase = rebase,
                },
                .buffer = &.{},
                .seek = 0,
                .end = 0,
            },
        },
        .vec = .{&.{}},
        .err = null,
    };
    mr.* = .{
        .gpa = gpa,
        .streams = streams,
        .batch = .init(streams.storage()),
    };
    for (contexts, 0..) |*context, i| {
        const r = &context.fr.interface;
        rebaseGrowing(mr, context, 1) catch |err| {
            context.err = err;
            continue;
        };
        context.vec[0] = r.buffer;
        mr.batch.addAt(@intCast(i), .{ .file_read_streaming = .{
            .file = context.fr.file,
            .data = &context.vec,
        } });
    }
}

pub fn deinit(mr: *MultiReader) void {
    const gpa = mr.gpa;
    const contexts = mr.streams.contexts();
    const io = contexts[0].fr.io;
    mr.batch.cancel(io);
    for (contexts) |*context| {
        gpa.free(context.fr.interface.buffer);
    }
}

pub fn fileReader(mr: *MultiReader, index: usize) *File.Reader {
    return &mr.streams.contexts()[index].fr;
}

pub fn reader(mr: *MultiReader, index: usize) *Io.Reader {
    return &mr.streams.contexts()[index].fr.interface;
}

/// Checks for errors in all streams, prioritizing `error.Canceled` if it
/// occurred anywhere, and ignoring `error.EndOfStream`.
pub fn checkAnyError(mr: *const MultiReader) UnendingError!void {
    const contexts = mr.streams.contexts();
    var other: UnendingError!void = {};
    for (contexts) |*context| {
        if (context.err) |err| switch (err) {
            error.Canceled => |e| return e,
            error.EndOfStream => continue,
            else => |e| other = e,
        };
    }
    return other;
}

pub fn toOwnedSlice(mr: *MultiReader, index: usize) Allocator.Error![]u8 {
    const gpa = mr.gpa;
    const r: *Io.Reader = reader(mr, index);
    if (r.seek == 0) {
        const new = try gpa.realloc(r.buffer, r.end);
        r.buffer = &.{};
        r.end = 0;
        return new;
    }
    const new = try gpa.dupe(u8, r.buffered());
    gpa.free(r.buffer);
    r.buffer = &.{};
    r.seek = 0;
    r.end = 0;
    return new;
}

fn stream(r: *Io.Reader, w: *Io.Writer, limit: Io.Limit) Io.Reader.StreamError!usize {
    _ = limit;
    _ = w;
    const fr: *File.Reader = @alignCast(@fieldParentPtr("interface", r));
    const context: *Context = @fieldParentPtr("fr", fr);
    try fillUntimed(context, 1);
    return 0;
}

fn discard(r: *Io.Reader, limit: Io.Limit) Io.Reader.Error!usize {
    _ = limit;
    const fr: *File.Reader = @alignCast(@fieldParentPtr("interface", r));
    const context: *Context = @fieldParentPtr("fr", fr);
    try fillUntimed(context, 1);
    return 0;
}

fn readVec(r: *Io.Reader, data: [][]u8) Io.Reader.Error!usize {
    _ = data;
    const fr: *File.Reader = @alignCast(@fieldParentPtr("interface", r));
    const context: *Context = @fieldParentPtr("fr", fr);
    try fillUntimed(context, 1);
    return 0;
}

fn rebase(r: *Io.Reader, capacity: usize) Io.Reader.RebaseError!void {
    const fr: *File.Reader = @alignCast(@fieldParentPtr("interface", r));
    const context: *Context = @fieldParentPtr("fr", fr);
    try fillUntimed(context, capacity);
}

fn fillUntimed(context: *Context, capacity: usize) Io.Reader.Error!void {
    fill(context.mr, capacity, .none) catch |err| switch (err) {
        error.Timeout => unreachable,
        error.Canceled, error.ConcurrencyUnavailable => |e| {
            context.err = e;
            return error.ReadFailed;
        },
        error.EndOfStream => |e| return e,
    };
    if (context.err) |err| switch (err) {
        error.EndOfStream => |e| return e,
        else => return error.ReadFailed,
    };
}

pub const FillError = Io.Batch.AwaitConcurrentError || error{
    /// `fill` was called when all streams already have failed or reached the
    /// end.
    EndOfStream,
};

/// Wait until at least one stream receives more data.
pub fn fill(mr: *MultiReader, unused_capacity: usize, timeout: Io.Timeout) FillError!void {
    const contexts = mr.streams.contexts();
    const io = contexts[0].fr.io;
    var any_completed = false;

    try mr.batch.awaitConcurrent(io, timeout);

    while (mr.batch.next()) |operation| {
        any_completed = true;
        const context = &contexts[operation.index];
        const n = operation.result.file_read_streaming catch |err| {
            context.err = err;
            continue;
        };
        const r = &context.fr.interface;
        r.end += n;
        if (r.buffer.len - r.end < unused_capacity) {
            rebaseGrowing(mr, context, r.bufferedLen() + unused_capacity) catch |err| {
                context.err = err;
                continue;
            };
            assert(r.seek == 0);
        }
        context.vec[0] = r.buffer[r.end..];
        mr.batch.addAt(operation.index, .{ .file_read_streaming = .{
            .file = context.fr.file,
            .data = &context.vec,
        } });
    }

    if (!any_completed) return error.EndOfStream;
}

/// Wait until all streams fail or reach the end.
pub fn fillRemaining(mr: *MultiReader, timeout: Io.Timeout) Io.Batch.AwaitConcurrentError!void {
    while (fill(mr, 1, timeout)) |_| {} else |err| switch (err) {
        error.EndOfStream => return,
        else => |e| return e,
    }
}

fn rebaseGrowing(mr: *MultiReader, context: *Context, capacity: usize) Allocator.Error!void {
    const gpa = mr.gpa;
    const r = &context.fr.interface;
    if (r.buffer.len >= capacity) {
        const data = r.buffer[r.seek..r.end];
        @memmove(r.buffer[0..data.len], data);
        r.seek = 0;
        r.end = data.len;
    } else {
        const adjusted_capacity = std.ArrayList(u8).growCapacity(capacity);

        if (r.seek == 0) {
            if (gpa.remap(r.buffer, adjusted_capacity)) |new_memory| {
                r.buffer = new_memory;
                return;
            }
        }

        const data = r.buffer[r.seek..r.end];
        const new = try gpa.alloc(u8, adjusted_capacity);
        @memcpy(new[0..data.len], data);
        gpa.free(r.buffer);
        r.buffer = new;
        r.seek = 0;
        r.end = data.len;
    }
}



---
File: /std/Io/File/Reader.zig
---

//! Memoizes key information about a file handle such as:
//! * The size from calling stat, or the error that occurred therein.
//! * The current seek position.
//! * The error that occurred when trying to seek.
//! * Whether reading should be done positionally or streaming.
//! * Whether reading should be done via fd-to-fd syscalls (e.g. `sendfile`)
//!   versus plain variants (e.g. `read`).
//!
//! Fulfills the `Io.Reader` interface.
const Reader = @This();

const std = @import("../../std.zig");
const Io = std.Io;
const File = std.Io.File;
const assert = std.debug.assert;

io: Io,
file: File,
err: ?Error = null,
mode: Mode = .positional,
/// Tracks the true seek position in the file. To obtain the logical position,
/// use `logicalPos`.
pos: u64 = 0,
size: ?u64 = null,
size_err: ?SizeError = null,
seek_err: ?SeekError = null,
interface: Io.Reader,

pub const Error = Io.Operation.FileReadStreaming.UnendingError || Io.Cancelable;

pub const SizeError = File.StatError || error{
    /// Occurs if, for example, the file handle is a network socket and therefore does not have a size.
    Streaming,
};

pub const SeekError = File.SeekError || error{
    /// Seeking fell back to reading, and reached the end before the requested seek position.
    /// `pos` remains at the end of the file.
    EndOfStream,
    /// Seeking fell back to reading, which failed.
    ReadFailed,
};

pub const Mode = enum {
    streaming,
    positional,
    /// Avoid syscalls other than `read` and `readv`.
    streaming_simple,
    /// Avoid syscalls other than `pread` and `preadv`.
    positional_simple,
    /// Indicates reading cannot continue because of a seek failure.
    failure,

    pub fn toStreaming(m: @This()) @This() {
        return switch (m) {
            .positional, .streaming => .streaming,
            .positional_simple, .streaming_simple => .streaming_simple,
            .failure => .failure,
        };
    }

    pub fn toSimple(m: @This()) @This() {
        return switch (m) {
            .positional, .positional_simple => .positional_simple,
            .streaming, .streaming_simple => .streaming_simple,
            .failure => .failure,
        };
    }
};

pub fn initInterface(buffer: []u8) Io.Reader {
    return .{
        .vtable = &.{
            .stream = stream,
            .discard = discard,
            .readVec = readVec,
        },
        .buffer = buffer,
        .seek = 0,
        .end = 0,
    };
}

pub fn init(file: File, io: Io, buffer: []u8) Reader {
    return .{
        .io = io,
        .file = file,
        .interface = initInterface(buffer),
    };
}

pub fn initSize(file: File, io: Io, buffer: []u8, size: ?u64) Reader {
    return .{
        .io = io,
        .file = file,
        .interface = initInterface(buffer),
        .size = size,
    };
}

/// Positional is more threadsafe, since the global seek position is not
/// affected, but when such syscalls are not available, preemptively
/// initializing in streaming mode skips a failed syscall.
pub fn initStreaming(file: File, io: Io, buffer: []u8) Reader {
    return .{
        .io = io,
        .file = file,
        .interface = Reader.initInterface(buffer),
        .mode = .streaming,
        .seek_err = error.Unseekable,
        .size_err = error.Streaming,
    };
}

pub fn getSize(r: *Reader) SizeError!u64 {
    return r.size orelse {
        if (r.size_err) |err| return err;
        if (r.file.stat(r.io)) |st| {
            if (st.kind == .file) {
                r.size = st.size;
                return st.size;
            } else {
                r.mode = r.mode.toStreaming();
                r.size_err = error.Streaming;
                return error.Streaming;
            }
        } else |err| {
            r.size_err = err;
            return err;
        }
    };
}

pub fn seekBy(r: *Reader, offset: i64) SeekError!void {
    const io = r.io;
    switch (r.mode) {
        .positional, .positional_simple => {
            setLogicalPos(r, @intCast(@as(i64, @intCast(logicalPos(r))) + offset));
        },
        .streaming, .streaming_simple => {
            const seek_err = r.seek_err orelse e: {
                if (io.vtable.fileSeekBy(io.userdata, r.file, offset)) |_| {
                    setLogicalPos(r, @intCast(@as(i64, @intCast(logicalPos(r))) + offset));
                    return;
                } else |err| {
                    r.seek_err = err;
                    break :e err;
                }
            };
            var remaining = std.math.cast(u64, offset) orelse return seek_err;
            while (remaining > 0) {
                remaining -= discard(&r.interface, .limited64(remaining)) catch |err| {
                    r.seek_err = err;
                    return err;
                };
            }
            r.interface.tossBuffered();
        },
        .failure => return r.seek_err.?,
    }
}

/// Repositions logical read offset relative to the beginning of the file.
pub fn seekTo(r: *Reader, offset: u64) SeekError!void {
    const io = r.io;
    switch (r.mode) {
        .positional, .positional_simple => {
            setLogicalPos(r, offset);
        },
        .streaming, .streaming_simple => {
            const logical_pos = logicalPos(r);
            if (offset >= logical_pos) return seekBy(r, @intCast(offset - logical_pos));
            if (r.seek_err) |err| return err;
            io.vtable.fileSeekTo(io.userdata, r.file, offset) catch |err| {
                r.seek_err = err;
                return err;
            };
            setLogicalPos(r, offset);
        },
        .failure => return r.seek_err.?,
    }
}

pub fn logicalPos(r: *const Reader) u64 {
    return r.pos - r.interface.bufferedLen();
}

fn setLogicalPos(r: *Reader, offset: u64) void {
    const logical_pos = r.logicalPos();
    if (offset < logical_pos or offset >= r.pos) {
        r.interface.tossBuffered();
        r.pos = offset;
    } else r.interface.toss(@intCast(offset - logical_pos));
}

/// Number of slices to store on the stack, when trying to send as many byte
/// vectors through the underlying read calls as possible.
const max_buffers_len = 16;

fn stream(io_reader: *Io.Reader, w: *Io.Writer, limit: Io.Limit) Io.Reader.StreamError!usize {
    const r: *Reader = @alignCast(@fieldParentPtr("interface", io_reader));
    return streamMode(r, w, limit, r.mode);
}

pub fn streamMode(r: *Reader, w: *Io.Writer, limit: Io.Limit, mode: Mode) Io.Reader.StreamError!usize {
    switch (mode) {
        .positional, .streaming => return w.sendFile(r, limit) catch |write_err| switch (write_err) {
            error.Unimplemented => {
                r.mode = r.mode.toSimple();
                return 0;
            },
            else => |e| return e,
        },
        .positional_simple => {
            const dest = limit.slice(try w.writableSliceGreedy(1));
            var data: [1][]u8 = .{dest};
            const n = try readVecPositional(r, &data);
            w.advance(n);
            return n;
        },
        .streaming_simple => {
            const dest = limit.slice(try w.writableSliceGreedy(1));
            var data: [1][]u8 = .{dest};
            const n = try readVecStreaming(r, &data);
            w.advance(n);
            return n;
        },
        .failure => return error.ReadFailed,
    }
}

fn readVec(io_reader: *Io.Reader, data: [][]u8) Io.Reader.Error!usize {
    const r: *Reader = @alignCast(@fieldParentPtr("interface", io_reader));
    switch (r.mode) {
        .positional, .positional_simple => return readVecPositional(r, data),
        .streaming, .streaming_simple => return readVecStreaming(r, data),
        .failure => return error.ReadFailed,
    }
}

fn readVecPositional(r: *Reader, data: [][]u8) Io.Reader.Error!usize {
    const io = r.io;
    var iovecs_buffer: [max_buffers_len][]u8 = undefined;
    const dest_n, const data_size = try r.interface.writableVector(&iovecs_buffer, data);
    const dest = iovecs_buffer[0..dest_n];
    assert(dest[0].len > 0);
    const n = io.vtable.fileReadPositional(io.userdata, r.file, dest, r.pos) catch |err| switch (err) {
        error.Unseekable => {
            r.mode = r.mode.toStreaming();
            const pos = r.pos;
            if (pos != 0) {
                r.pos = 0;
                r.seekBy(@intCast(pos)) catch {
                    r.mode = .failure;
                    return error.ReadFailed;
                };
            }
            return 0;
        },
        else => |e| {
            r.err = e;
            return error.ReadFailed;
        },
    };
    if (n == 0) {
        r.size = r.pos;
        return error.EndOfStream;
    }
    r.pos += n;
    if (n > data_size) {
        r.interface.end += n - data_size;
        return data_size;
    }
    return n;
}

fn readVecStreaming(r: *Reader, data: [][]u8) Io.Reader.Error!usize {
    const io = r.io;
    var iovecs_buffer: [max_buffers_len][]u8 = undefined;
    const dest_n, const data_size = try r.interface.writableVector(&iovecs_buffer, data);
    const dest = iovecs_buffer[0..dest_n];
    assert(dest[0].len > 0);
    const n = r.file.readStreaming(io, dest) catch |err| switch (err) {
        error.EndOfStream => {
            r.size = r.pos;
            return error.EndOfStream;
        },
        else => |e| {
            r.err = e;
            return error.ReadFailed;
        },
    };
    r.pos += n;
    if (n > data_size) {
        r.interface.end += n - data_size;
        return data_size;
    }
    return n;
}

fn discard(io_reader: *Io.Reader, limit: Io.Limit) Io.Reader.Error!usize {
    const r: *Reader = @alignCast(@fieldParentPtr("interface", io_reader));
    const io = r.io;
    const file = r.file;
    switch (r.mode) {
        .positional, .positional_simple => {
            const size = r.getSize() catch {
                r.mode = r.mode.toStreaming();
                return 0;
            };
            const logical_pos = logicalPos(r);
            const bytes_remaining = size - logical_pos;
            if (bytes_remaining == 0) return error.EndOfStream;
            const delta = @min(@intFromEnum(limit), bytes_remaining);
            setLogicalPos(r, logical_pos + delta);
            return delta;
        },
        .streaming, .streaming_simple => {
            // Unfortunately we can't seek forward without knowing the
            // size because the seek syscalls provided to us will not
            // return the true end position if a seek would exceed the
            // end.
            fallback: {
                if (r.size_err == null and r.seek_err == null) break :fallback;

                const buffered_len = r.interface.bufferedLen();
                var remaining = @intFromEnum(limit);
                if (remaining <= buffered_len) {
                    r.interface.seek += remaining;
                    return remaining;
                }
                remaining -= buffered_len;
                r.interface.seek = 0;
                r.interface.end = 0;

                var trash_buffer: [128]u8 = undefined;
                var data: [1][]u8 = .{trash_buffer[0..@min(trash_buffer.len, remaining)]};
                var iovecs_buffer: [max_buffers_len][]u8 = undefined;
                const dest_n, const data_size = try r.interface.writableVector(&iovecs_buffer, &data);
                const dest = iovecs_buffer[0..dest_n];
                assert(dest[0].len > 0);
                const n = file.readStreaming(io, dest) catch |err| switch (err) {
                    error.EndOfStream => {
                        r.size = r.pos;
                        return error.EndOfStream;
                    },
                    else => |e| {
                        r.err = e;
                        return error.ReadFailed;
                    },
                };
                r.pos += n;
                if (n > data_size) {
                    r.interface.end += n - data_size;
                    remaining -= data_size;
                } else {
                    remaining -= n;
                }
                return @intFromEnum(limit) - remaining;
            }
            const size = r.getSize() catch return 0;
            const n = @min(size - r.pos, std.math.maxInt(i64), @intFromEnum(limit));
            io.vtable.fileSeekBy(io.userdata, file, n) catch |err| {
                r.seek_err = err;
                return 0;
            };
            r.pos += n;
            return n;
        },
        .failure => return error.ReadFailed,
    }
}

/// Returns whether the stream is at the logical end.
pub fn atEnd(r: *Reader) bool {
    // Even if stat fails, size is set when end is encountered.
    const size = r.size orelse return false;
    return size - logicalPos(r) == 0;
}



---
File: /std/Io/File/Writer.zig
---

const Writer = @This();
const builtin = @import("builtin");
const is_windows = builtin.os.tag == .windows;

const std = @import("../../std.zig");
const Io = std.Io;
const File = std.Io.File;
const assert = std.debug.assert;

io: Io,
file: File,
err: ?Error = null,
mode: Mode = .positional,
/// Tracks the true seek position in the file. To obtain the logical position,
/// use `logicalPos`.
pos: u64 = 0,
write_file_err: ?WriteFileError = null,
seek_err: ?SeekError = null,
interface: Io.Writer,

pub const Mode = File.Reader.Mode;

pub const Error = Io.Operation.FileWriteStreaming.Error || Io.Cancelable;

pub const WriteFileError = Error || error{
    /// Descriptor is not valid or locked, or an mmap(2)-like operation is not available for in_fd.
    Unimplemented,
    /// Can happen on FreeBSD when using copy_file_range.
    CorruptedData,
    EndOfStream,
    ReadFailed,
};

pub const SeekError = Io.File.SeekError;

pub fn init(file: File, io: Io, buffer: []u8) Writer {
    return .{
        .io = io,
        .file = file,
        .interface = initInterface(buffer),
        .mode = .positional,
    };
}

/// Positional is more threadsafe, since the global seek position is not
/// affected, but when such syscalls are not available, preemptively
/// initializing in streaming mode will skip a failed syscall.
pub fn initStreaming(file: File, io: Io, buffer: []u8) Writer {
    return .{
        .io = io,
        .file = file,
        .interface = initInterface(buffer),
        .mode = .streaming,
    };
}

/// Detects if `file` is terminal and sets the mode accordingly.
pub fn initDetect(file: File, io: Io, buffer: []u8) Io.Cancelable!Writer {
    return .{
        .io = io,
        .file = file,
        .interface = initInterface(buffer),
        .mode = try .detect(io, file, true, .positional),
    };
}

pub fn initInterface(buffer: []u8) Io.Writer {
    return .{
        .vtable = &.{
            .drain = drain,
            .sendFile = sendFile,
        },
        .buffer = buffer,
    };
}

pub fn moveToReader(w: *Writer) File.Reader {
    defer w.* = undefined;
    return .{
        .io = w.io,
        .file = w.file,
        .mode = w.mode,
        .pos = w.pos,
        .interface = File.Reader.initInterface(w.interface.buffer),
        .seek_err = w.seek_err,
    };
}

pub fn drain(io_w: *Io.Writer, data: []const []const u8, splat: usize) Io.Writer.Error!usize {
    const w: *Writer = @alignCast(@fieldParentPtr("interface", io_w));
    switch (w.mode) {
        .positional, .positional_simple => return drainPositional(w, data, splat),
        .streaming, .streaming_simple => return drainStreaming(w, data, splat),
        .failure => return error.WriteFailed,
    }
}

fn drainPositional(w: *Writer, data: []const []const u8, splat: usize) Io.Writer.Error!usize {
    const io = w.io;
    const header = w.interface.buffered();
    const n = io.vtable.fileWritePositional(io.userdata, w.file, header, data, splat, w.pos) catch |err| switch (err) {
        error.Unseekable => {
            w.mode = w.mode.toStreaming();
            const pos = w.pos;
            if (pos != 0) {
                w.pos = 0;
                w.seekTo(@intCast(pos)) catch {
                    w.mode = .failure;
                    return error.WriteFailed;
                };
            }
            return 0;
        },
        else => |e| {
            w.err = e;
            return error.WriteFailed;
        },
    };
    w.pos += n;
    return w.interface.consume(n);
}

fn drainStreaming(w: *Writer, data: []const []const u8, splat: usize) Io.Writer.Error!usize {
    const io = w.io;
    const header = w.interface.buffered();
    const n = w.file.writeStreaming(io, header, data, splat) catch |err| {
        w.err = err;
        return error.WriteFailed;
    };
    w.pos += n;
    return w.interface.consume(n);
}

pub fn sendFile(io_w: *Io.Writer, file_reader: *Io.File.Reader, limit: Io.Limit) Io.Writer.FileError!usize {
    const w: *Writer = @alignCast(@fieldParentPtr("interface", io_w));
    switch (w.mode) {
        .positional => return sendFilePositional(w, file_reader, limit),
        .positional_simple => return error.Unimplemented,
        .streaming => return sendFileStreaming(w, file_reader, limit),
        .streaming_simple => return error.Unimplemented,
        .failure => return error.WriteFailed,
    }
}

fn sendFilePositional(w: *Writer, file_reader: *Io.File.Reader, limit: Io.Limit) Io.Writer.FileError!usize {
    const io = w.io;
    const header = w.interface.buffered();
    const n = io.vtable.fileWriteFilePositional(io.userdata, w.file, header, file_reader, limit, w.pos) catch |err| switch (err) {
        error.Unseekable => {
            w.mode = w.mode.toStreaming();
            const pos = w.pos;
            if (pos != 0) {
                w.pos = 0;
                w.seekTo(@intCast(pos)) catch {
                    w.mode = .failure;
                    return error.WriteFailed;
                };
            }
            return 0;
        },
        error.Canceled => {
            w.err = error.Canceled;
            return error.WriteFailed;
        },
        error.EndOfStream => return error.EndOfStream,
        error.Unimplemented => return error.Unimplemented,
        error.ReadFailed => return error.ReadFailed,
        else => |e| {
            w.write_file_err = e;
            return error.WriteFailed;
        },
    };
    w.pos += n;
    return w.interface.consume(n);
}

fn sendFileStreaming(w: *Writer, file_reader: *Io.File.Reader, limit: Io.Limit) Io.Writer.FileError!usize {
    const io = w.io;
    const header = w.interface.buffered();
    const n = io.vtable.fileWriteFileStreaming(io.userdata, w.file, header, file_reader, limit) catch |err| switch (err) {
        error.Canceled => {
            w.err = error.Canceled;
            return error.WriteFailed;
        },
        error.EndOfStream => return error.EndOfStream,
        error.Unimplemented => return error.Unimplemented,
        error.ReadFailed => return error.ReadFailed,
        else => |e| {
            w.write_file_err = e;
            return error.WriteFailed;
        },
    };
    w.pos += n;
    return w.interface.consume(n);
}

pub fn seekTo(w: *Writer, offset: u64) (SeekError || Io.Writer.Error)!void {
    try w.interface.flush();
    try seekToUnbuffered(w, offset);
}

pub fn logicalPos(w: *const Writer) u64 {
    return w.pos + w.interface.end;
}

/// Asserts that no data is currently buffered.
pub fn seekToUnbuffered(w: *Writer, offset: u64) SeekError!void {
    assert(w.interface.buffered().len == 0);
    const io = w.io;
    switch (w.mode) {
        .positional, .positional_simple => {
            w.pos = offset;
        },
        .streaming, .streaming_simple => {
            if (w.seek_err) |err| return err;
            io.vtable.fileSeekTo(io.userdata, w.file, offset) catch |err| {
                w.seek_err = err;
                return err;
            };
            w.pos = offset;
        },
        .failure => return w.seek_err.?,
    }
}

pub const EndError = File.SetLengthError || Io.Writer.Error;

/// Flushes any buffered data and sets the end position of the file.
///
/// If not overwriting existing contents, then calling `interface.flush`
/// directly is sufficient.
///
/// Flush failure is handled by setting `err` so that it can be handled
/// along with other write failures.
pub fn end(w: *Writer) EndError!void {
    const io = w.io;
    try w.interface.flush();
    switch (w.mode) {
        .positional,
        .positional_simple,
        => w.file.setLength(io, w.pos) catch |err| switch (err) {
            error.NonResizable => return,
            else => |e| return e,
        },

        .streaming,
        .streaming_simple,
        .failure,
        => {},
    }
}

/// Convenience method for calling `Io.Writer.flush` and returning the
/// underlying error.
pub fn flush(w: *Writer) Error!void {
    w.interface.flush() catch |err| switch (err) {
        error.WriteFailed => return w.err.?,
    };
}



---
File: /std/Io/net/HostName.zig
---

//! An already-validated host name. A valid host name:
//! * Has length less than or equal to `max_len`.
//! * Is valid UTF-8.
//! * Lacks ASCII characters other than alphanumeric, '-', and '.'.
const HostName = @This();

const builtin = @import("builtin");
const native_os = builtin.os.tag;

const std = @import("../../std.zig");
const Io = std.Io;
const IpAddress = Io.net.IpAddress;
const Ip6Address = Io.net.Ip6Address;
const assert = std.debug.assert;
const Stream = Io.net.Stream;

/// Externally managed memory. Already checked to be valid.
bytes: []const u8,

pub const max_len = 255;

pub const ValidateError = error{
    NameTooLong,
    InvalidHostName,
};

/// Validates a hostname according to [RFC 1123](https://www.rfc-editor.org/rfc/rfc1123)
pub fn validate(bytes: []const u8) ValidateError!void {
    if (bytes.len == 0) return error.InvalidHostName;

    // Ignore trailing dot (FQDN). It doesn't count toward our length.
    const end = if (bytes[bytes.len - 1] == '.') bytes.len - 1 else bytes.len;

    // The accepted maximum length of a hostname, including labels and dots.
    if (end > max_len) return error.NameTooLong;

    // Hostnames are divided into dot-separated "labels", which:
    //
    // - Start with a letter or digit
    // - Can contain letters, digits, or hyphens
    // - Must end with a letter or digit
    // - Have a minimum of 1 character and a maximum of 63
    var label_len: usize = 0;
    for (bytes[0..end], 0..) |c, i| {
        switch (c) {
            '.' => {
                if (label_len == 0 or label_len > 63) return error.InvalidHostName;
                if (!std.ascii.isAlphanumeric(bytes[i - 1])) return error.InvalidHostName;
                label_len = 0;
            },
            '-' => {
                if (label_len == 0) return error.InvalidHostName;
                label_len += 1;
            },
            else => {
                if (!std.ascii.isAlphanumeric(c)) return error.InvalidHostName;
                label_len += 1;
            },
        }
    }

    // Validate the final label
    if (label_len == 0 or label_len > 63) return error.InvalidHostName;
    if (!std.ascii.isAlphanumeric(bytes[end - 1])) return error.InvalidHostName;
}

test validate {
    // Valid hostnames
    try validate("example");
    try validate("example.com");
    try validate("www.example.com");
    try validate("sub.domain.example.com");
    try validate("example.com.");
    try validate("host-name.example.com.");
    try validate("123.example.com.");
    try validate("a-b.com");
    try validate("a.b.c.d.e.f.g");
    try validate("127.0.0.1"); // Also a valid hostname
    try validate("a" ** 63 ++ ".com"); // Label exactly 63 chars (valid)
    try validate("a." ** 127 ++ "a"); // Total length 255 (valid)
    try validate("a." ** 127 ++ "a."); // Total length 255 + trailing dot (valid)

    // Invalid hostnames
    try std.testing.expectError(error.InvalidHostName, validate(""));
    try std.testing.expectError(error.InvalidHostName, validate(".example.com"));
    try std.testing.expectError(error.InvalidHostName, validate("example.com.."));
    try std.testing.expectError(error.InvalidHostName, validate("host..domain"));
    try std.testing.expectError(error.InvalidHostName, validate("-hostname"));
    try std.testing.expectError(error.InvalidHostName, validate("hostname-"));
    try std.testing.expectError(error.InvalidHostName, validate("hostname-.com"));
    try std.testing.expectError(error.InvalidHostName, validate("a.-.b"));
    try std.testing.expectError(error.InvalidHostName, validate("host_name.com"));
    try std.testing.expectError(error.InvalidHostName, validate("."));
    try std.testing.expectError(error.InvalidHostName, validate(".."));
    try std.testing.expectError(error.InvalidHostName, validate("a" ** 64 ++ ".com")); // Label length 64 (too long)
    try std.testing.expectError(error.NameTooLong, validate("a." ** 127 ++ "ab")); // Total length 256 (too long)
    try std.testing.expectError(error.NameTooLong, validate("a." ** 127 ++ "ab.")); // Total length 256 + trailing dot (too long)
}

pub fn init(bytes: []const u8) ValidateError!HostName {
    try validate(bytes);
    return .{ .bytes = bytes };
}

pub fn sameParentDomain(parent_host: HostName, child_host: HostName) bool {
    const parent_bytes = parent_host.bytes;
    const child_bytes = child_host.bytes;
    if (!std.ascii.endsWithIgnoreCase(child_bytes, parent_bytes)) return false;
    if (child_bytes.len == parent_bytes.len) return true;
    if (parent_bytes.len > child_bytes.len) return false;
    return child_bytes[child_bytes.len - parent_bytes.len - 1] == '.';
}

test sameParentDomain {
    try std.testing.expect(!sameParentDomain(try .init("foo.com"), try .init("bar.com")));
    try std.testing.expect(sameParentDomain(try .init("foo.com"), try .init("foo.com")));
    try std.testing.expect(sameParentDomain(try .init("foo.com"), try .init("bar.foo.com")));
    try std.testing.expect(!sameParentDomain(try .init("bar.foo.com"), try .init("foo.com")));
}

/// Domain names are case-insensitive (RFC 5890, Section 2.3.2.4)
pub fn eql(a: HostName, b: HostName) bool {
    return std.ascii.eqlIgnoreCase(a.bytes, b.bytes);
}

pub const LookupOptions = struct {
    port: u16,
    /// `null` means either.
    canonical_name_buffer: ?*[max_len]u8 = null,
    family: ?IpAddress.Family = null,
};

pub const LookupError = error{
    UnknownHostName,
    ResolvConfParseFailed,
    InvalidDnsARecord,
    InvalidDnsAAAARecord,
    InvalidDnsCnameRecord,
    NameServerFailure,
    NoAddressReturned,
    /// Failed to open or read "/etc/hosts" or "/etc/resolv.conf".
    DetectingNetworkConfigurationFailed,
} || IpAddress.BindError || Io.Cancelable;

pub const LookupResult = union(enum) {
    address: IpAddress,
    canonical_name: HostName,
};

/// Adds any number of `LookupResult.address` into `resolved`, and exactly one
/// `LookupResult.canonical_name`.
///
/// Guaranteed not to block if provided queue has capacity at least 16.
///
/// Closes `resolved` before return, even on error.
///
/// Asserts `resolved` is not closed until this call returns.
pub fn lookup(
    host_name: HostName,
    io: Io,
    resolved: *Io.Queue(LookupResult),
    options: LookupOptions,
) LookupError!void {
    return io.vtable.netLookup(io.userdata, host_name, resolved, options);
}

pub const ExpandError = error{InvalidDnsPacket} || ValidateError;

/// Decompresses a DNS name.
///
/// Returns number of bytes consumed from `packet` starting at `i`,
/// along with the expanded `HostName`.
///
/// Asserts `buffer` is has length at least `max_len`.
pub fn expand(noalias packet: []const u8, start_i: usize, noalias dest_buffer: []u8) ExpandError!struct { usize, HostName } {
    const dest = dest_buffer[0..max_len];

    var i = start_i;
    var dest_i: usize = 0;
    var len: ?usize = null;

    // Detect reference loop using an iteration counter.
    for (0..packet.len / 2) |_| {
        if (i >= packet.len) return error.InvalidDnsPacket;

        const c = packet[i];
        if ((c & 0xc0) != 0) {
            if (i + 1 >= packet.len) return error.InvalidDnsPacket;
            const j: usize = (@as(usize, c & 0x3F) << 8) | packet[i + 1];
            if (j >= packet.len) return error.InvalidDnsPacket;
            if (len == null) len = (i + 2) - start_i;
            i = j;
        } else if (c != 0) {
            if (dest_i != 0) {
                dest[dest_i] = '.';
                dest_i += 1;
            }
            const label_len: usize = c;
            if (i + 1 + label_len > packet.len) return error.InvalidDnsPacket;
            if (dest_i + label_len + 1 > dest.len) return error.InvalidDnsPacket;
            @memcpy(dest[dest_i..][0..label_len], packet[i + 1 ..][0..label_len]);
            dest_i += label_len;
            i += 1 + label_len;
        } else {
            return .{
                len orelse i - start_i + 1,
                try .init(dest[0..dest_i]),
            };
        }
    }
    return error.InvalidDnsPacket;
}

pub const DnsRecord = enum(u8) {
    A = 1,
    CNAME = 5,
    AAAA = 28,
    _,
};

pub const DnsResponse = struct {
    bytes: []const u8,
    bytes_index: u32,
    answers_remaining: u16,

    pub const Answer = struct {
        rr: DnsRecord,
        packet: []const u8,
        data_off: u32,
        data_len: u16,
    };

    pub const Error = error{InvalidDnsPacket};

    pub fn init(r: []const u8) Error!DnsResponse {
        if (r.len < 12) return error.InvalidDnsPacket;
        if ((r[3] & 15) != 0) return .{ .bytes = r, .bytes_index = 3, .answers_remaining = 0 };
        var i: u32 = 12;
        var query_count = std.mem.readInt(u16, r[4..6], .big);
        while (query_count != 0) : (query_count -= 1) {
            while (i < r.len and r[i] -% 1 < 127) i += 1;
            if (r.len - i < 6) return error.InvalidDnsPacket;
            i = i + 5 + @intFromBool(r[i] != 0);
        }
        return .{
            .bytes = r,
            .bytes_index = i,
            .answers_remaining = std.mem.readInt(u16, r[6..8], .big),
        };
    }

    pub fn next(dr: *DnsResponse) Error!?Answer {
        if (dr.answers_remaining == 0) return null;
        dr.answers_remaining -= 1;
        const r = dr.bytes;
        var i = dr.bytes_index;
        while (i < r.len and r[i] -% 1 < 127) i += 1;
        if (r.len - i < 12) return error.InvalidDnsPacket;
        i = i + 1 + @intFromBool(r[i] != 0);
        const len = std.mem.readInt(u16, r[i + 8 ..][0..2], .big);
        if (i + 10 + len > r.len) return error.InvalidDnsPacket;
        defer dr.bytes_index = i + 10 + len;
        return .{
            .rr = @enumFromInt(r[i + 1]),
            .packet = r,
            .data_off = i + 10,
            .data_len = len,
        };
    }
};

pub const ConnectError = LookupError || IpAddress.ConnectError;

pub fn connect(
    host_name: HostName,
    io: Io,
    port: u16,
    options: IpAddress.ConnectOptions,
) ConnectError!Stream {
    var connect_many_buffer: [32]IpAddress.ConnectError!Stream = undefined;
    var connect_many_queue: Io.Queue(IpAddress.ConnectError!Stream) = .init(&connect_many_buffer);

    var connect_many = io.async(connectMany, .{ host_name, io, port, &connect_many_queue, options });
    defer {
        connect_many.cancel(io) catch {};
        while (connect_many_queue.getOneUncancelable(io)) |loser| {
            if (loser) |s| s.close(io) else |_| {}
        } else |err| switch (err) {
            error.Closed => {},
        }
    }

    var ip_connect_error: ?IpAddress.ConnectError = null;

    while (connect_many_queue.getOne(io)) |result| {
        if (result) |stream| {
            return stream;
        } else |err| switch (err) {
            error.Canceled => unreachable,

            error.SystemResources,
            error.OptionUnsupported,
            error.ProcessFdQuotaExceeded,
            error.SystemFdQuotaExceeded,
            => |e| return e,

            error.WouldBlock => return error.Unexpected,

            else => |e| ip_connect_error = e,
        }
    } else |err| switch (err) {
        error.Canceled => |e| return e,
        error.Closed => {
            // There was no successful connection attempt. If there was a lookup error, return that.
            try connect_many.await(io);
            // Otherwise, return the error from a failed IP connection attempt.
            return ip_connect_error orelse
                return error.UnknownHostName;
        },
    }
}

/// Asynchronously establishes a connection to all IP addresses associated with
/// a host name, adding them to a results queue upon completion.
///
/// `error.Canceled` will never be added to the queue, but other errors may be.
///
/// Closes `results` before return, even on error.
///
/// Asserts `results` is not closed until this call returns.
pub fn connectMany(
    host_name: HostName,
    io: Io,
    port: u16,
    results: *Io.Queue(IpAddress.ConnectError!Stream),
    options: IpAddress.ConnectOptions,
) LookupError!void {
    defer results.close(io);

    var canonical_name_buffer: [max_len]u8 = undefined;
    var lookup_buffer: [32]HostName.LookupResult = undefined;
    var lookup_queue: Io.Queue(LookupResult) = .init(&lookup_buffer);
    var lookup_future = io.async(lookup, .{ host_name, io, &lookup_queue, .{
        .port = port,
        .canonical_name_buffer = &canonical_name_buffer,
    } });
    defer lookup_future.cancel(io) catch {};

    var group: Io.Group = .init;
    defer group.cancel(io);

    while (lookup_queue.getOne(io)) |dns_result| switch (dns_result) {
        .address => |address| group.async(io, enqueueConnection, .{ address, io, results, options }),
        .canonical_name => continue,
    } else |err| switch (err) {
        error.Canceled => |e| return e,
        error.Closed => {
            try group.await(io);
            return lookup_future.await(io);
        },
    }
}
fn enqueueConnection(
    address: IpAddress,
    io: Io,
    queue: *Io.Queue(IpAddress.ConnectError!Stream),
    options: IpAddress.ConnectOptions,
) Io.Cancelable!void {
    const result = address.connect(io, options) catch |err| switch (err) {
        error.Canceled => |e| return e,
        else => |e| e, // other errors go in the result queue
    };
    errdefer if (result) |s| s.close(io) else |_| {};
    queue.putOne(io, result) catch |err| switch (err) {
        error.Canceled => |e| return e,
        error.Closed => unreachable, // `queue` must not be closed
    };
}

pub const ResolvConf = struct {
    attempts: u32,
    ndots: u32,
    timeout_seconds: u32,
    nameservers_buffer: [max_nameservers]IpAddress,
    nameservers_len: usize,
    search_buffer: [max_len]u8,
    search_len: usize,

    /// According to resolv.conf(5) there is a maximum of 3 nameservers in this
    /// file.
    pub const max_nameservers = 3;

    /// Returns `error.StreamTooLong` if a line is longer than 512 bytes.
    pub fn init(io: Io) !ResolvConf {
        var rc: ResolvConf = .{
            .nameservers_buffer = undefined,
            .nameservers_len = 0,
            .search_buffer = undefined,
            .search_len = 0,
            .ndots = 1,
            .timeout_seconds = 5,
            .attempts = 2,
        };

        const file = Io.Dir.openFileAbsolute(io, "/etc/resolv.conf", .{}) catch |err| switch (err) {
            error.FileNotFound,
            error.NotDir,
            error.AccessDenied,
            => {
                try addNumeric(&rc, io, "127.0.0.1", 53);
                return rc;
            },

            else => |e| return e,
        };
        defer file.close(io);

        var line_buf: [512]u8 = undefined;
        var file_reader = file.reader(io, &line_buf);
        parse(&rc, io, &file_reader.interface) catch |err| switch (err) {
            error.ReadFailed => return file_reader.err.?,
            else => |e| return e,
        };
        return rc;
    }

    const Directive = enum { options, nameserver, domain, search };
    const Option = enum { ndots, attempts, timeout };

    pub fn parse(rc: *ResolvConf, io: Io, reader: *Io.Reader) !void {
        while (reader.takeSentinel('\n')) |line_with_comment| {
            const line = line: {
                var split = std.mem.splitScalar(u8, line_with_comment, '#');
                break :line split.first();
            };
            var line_it = std.mem.tokenizeAny(u8, line, " \t");

            const token = line_it.next() orelse continue;
            switch (std.meta.stringToEnum(Directive, token) orelse continue) {
                .options => while (line_it.next()) |sub_tok| {
                    var colon_it = std.mem.splitScalar(u8, sub_tok, ':');
                    const name = colon_it.first();
                    const value_txt = colon_it.next() orelse continue;
                    const value = std.fmt.parseInt(u8, value_txt, 10) catch |err| switch (err) {
                        error.Overflow => 255,
                        error.InvalidCharacter => continue,
                    };
                    switch (std.meta.stringToEnum(Option, name) orelse continue) {
                        .ndots => rc.ndots = @min(value, 15),
                        .attempts => rc.attempts = @min(value, 10),
                        .timeout => rc.timeout_seconds = @min(value, 60),
                    }
                },
                .nameserver => {
                    const ip_txt = line_it.next() orelse continue;
                    try addNumeric(rc, io, ip_txt, 53);
                },
                .domain, .search => {
                    const rest = line_it.rest();
                    @memcpy(rc.search_buffer[0..rest.len], rest);
                    rc.search_len = rest.len;
                },
            }
        } else |err| switch (err) {
            error.EndOfStream => if (reader.bufferedLen() != 0) return error.EndOfStream,
            else => |e| return e,
        }

        if (rc.nameservers_len == 0) {
            try addNumeric(rc, io, "127.0.0.1", 53);
        }
    }

    fn addNumeric(rc: *ResolvConf, io: Io, name: []const u8, port: u16) !void {
        if (rc.nameservers_len < rc.nameservers_buffer.len) {
            rc.nameservers_buffer[rc.nameservers_len] = try .resolve(io, name, port);
            rc.nameservers_len += 1;
        }
    }

    pub fn nameservers(rc: *const ResolvConf) []const IpAddress {
        return rc.nameservers_buffer[0..rc.nameservers_len];
    }
};

test ResolvConf {
    const input =
        \\# Generated by resolvconf
        \\nameserver 1.0.0.1
        \\nameserver 1.1.1.1
        \\nameserver fe80::e0e:76ff:fed4:cf22
        \\options edns0
        \\
    ;
    var reader: Io.Reader = .fixed(input);

    var rc: ResolvConf = .{
        .nameservers_buffer = undefined,
        .nameservers_len = 0,
        .search_buffer = undefined,
        .search_len = 0,
        .ndots = 1,
        .timeout_seconds = 5,
        .attempts = 2,
    };

    try rc.parse(std.testing.io, &reader);
    try std.testing.expectEqual(3, rc.nameservers().len);
}



---
File: /std/Io/net/test.zig
---




---
File: /std/Io/Reader/Limited.zig
---

const Limited = @This();

const std = @import("../../std.zig");
const Reader = std.Io.Reader;
const Writer = std.Io.Writer;
const Limit = std.Io.Limit;

unlimited: *Reader,
remaining: Limit,
interface: Reader,

pub fn init(reader: *Reader, limit: Limit, buffer: []u8) Limited {
    return .{
        .unlimited = reader,
        .remaining = limit,
        .interface = .{
            .vtable = &.{
                .stream = stream,
                .discard = discard,
            },
            .buffer = buffer,
            .seek = 0,
            .end = 0,
        },
    };
}

fn stream(r: *Reader, w: *Writer, limit: Limit) Reader.StreamError!usize {
    const l: *Limited = @fieldParentPtr("interface", r);
    if (l.remaining == .nothing) return error.EndOfStream;
    const combined_limit = limit.min(l.remaining);
    const n = try l.unlimited.stream(w, combined_limit);
    l.remaining = l.remaining.subtract(n).?;
    return n;
}

test stream {
    var orig_buf: [10]u8 = undefined;
    @memcpy(&orig_buf, "test bytes");
    var fixed: std.Io.Reader = .fixed(&orig_buf);

    var limit_buf: [1]u8 = undefined;
    var limited: std.Io.Reader.Limited = .init(&fixed, @enumFromInt(4), &limit_buf);

    var result_buf: [10]u8 = undefined;
    var fixed_writer: std.Io.Writer = .fixed(&result_buf);
    const streamed = try limited.interface.stream(&fixed_writer, @enumFromInt(7));

    try std.testing.expect(streamed == 4);
    try std.testing.expectEqualStrings("test", result_buf[0..streamed]);
}

fn discard(r: *Reader, limit: Limit) Reader.Error!usize {
    const l: *Limited = @fieldParentPtr("interface", r);
    if (l.remaining == .nothing) return error.EndOfStream;
    const combined_limit = limit.min(l.remaining);
    const n = try l.unlimited.discard(combined_limit);
    l.remaining = l.remaining.subtract(n).?;
    return n;
}

test "end of stream, read, hit limit exactly" {
    var f: Reader = .fixed("i'm dying");
    var l = f.limited(.limited(4), &.{});
    const r = &l.interface;

    var buf: [2]u8 = undefined;
    try r.readSliceAll(&buf);
    try r.readSliceAll(&buf);
    try std.testing.expectError(error.EndOfStream, l.interface.readSliceAll(&buf));
}

test "end of stream, read, hit limit after partial read" {
    var f: Reader = .fixed("i'm dying");
    var l = f.limited(.limited(5), &.{});
    const r = &l.interface;

    var buf: [2]u8 = undefined;
    try r.readSliceAll(&buf);
    try r.readSliceAll(&buf);
    try std.testing.expectError(error.EndOfStream, l.interface.readSliceAll(&buf));
}

test "end of stream, discard, hit limit exactly" {
    var f: Reader = .fixed("i'm dying");
    var l = f.limited(.limited(4), &.{});
    const r = &l.interface;

    try r.discardAll(2);
    try r.discardAll(2);
    try std.testing.expectError(error.EndOfStream, l.interface.discardAll(2));
}

test "end of stream, discard, hit limit after partial read" {
    var f: Reader = .fixed("i'm dying");
    var l = f.limited(.limited(5), &.{});
    const r = &l.interface;

    try r.discardAll(2);
    try r.discardAll(2);
    try std.testing.expectError(error.EndOfStream, l.interface.discardAll(2));
}



---
File: /std/Io/Threaded/test.zig
---




---
File: /std/Io/Dir.zig
---

const Dir = @This();
const root = @import("root");

const builtin = @import("builtin");
const native_os = builtin.os.tag;

const std = @import("../std.zig");
const Io = std.Io;
const File = Io.File;
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;

handle: Handle,

pub const Handle = std.posix.fd_t;

pub const path = std.fs.path;

/// The maximum length of a file path that the operating system will accept.
///
/// Paths, including those returned from file system operations, may be longer
/// than this length, but such paths cannot be successfully passed back in
/// other file system operations. However, all path components returned by file
/// system operations are assumed to fit into a `u8` array of this length.
///
/// The byte count includes room for a null sentinel byte.
///
/// * On Windows, `[]u8` file paths are encoded as
///   [WTF-8](https://wtf-8.codeberg.page/).
/// * On WASI, `[]u8` file paths are encoded as valid UTF-8.
/// * On other platforms, `[]u8` file paths are opaque sequences of bytes with
///   no particular encoding.
pub const max_path_bytes = switch (native_os) {
    .linux, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .freebsd, .openbsd, .netbsd, .dragonfly, .haiku, .illumos, .plan9, .emscripten, .wasi, .serenity => std.posix.PATH_MAX,
    // Each WTF-16LE code unit may be expanded to 3 WTF-8 bytes.
    // If it would require 4 WTF-8 bytes, then there would be a surrogate
    // pair in the WTF-16LE, and we (over)account 3 bytes for it that way.
    // +1 for the null byte at the end, which can be encoded in 1 byte.
    .windows => std.os.windows.PATH_MAX_WIDE * 3 + 1,
    else => if (@hasDecl(root, "os") and @hasDecl(root.os, "PATH_MAX"))
        root.os.PATH_MAX
    else
        @compileError("PATH_MAX not implemented for " ++ @tagName(native_os)),
};

/// This represents the maximum size of a `[]u8` file name component that
/// the platform's common file systems support. File name components returned by file system
/// operations are likely to fit into a `u8` array of this length, but
/// (depending on the platform) this assumption may not hold for every configuration.
/// The byte count does not include a null sentinel byte.
/// On Windows, `[]u8` file name components are encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, file name components are encoded as valid UTF-8.
/// On other platforms, `[]u8` components are an opaque sequence of bytes with no particular encoding.
pub const max_name_bytes = switch (native_os) {
    .linux, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .freebsd, .openbsd, .netbsd, .dragonfly, .illumos, .serenity, .psp => std.posix.NAME_MAX,
    // Haiku's NAME_MAX includes the null terminator, so subtract one.
    .haiku => std.posix.NAME_MAX - 1,
    // Each WTF-16LE character may be expanded to 3 WTF-8 bytes.
    // If it would require 4 WTF-8 bytes, then there would be a surrogate
    // pair in the WTF-16LE, and we (over)account 3 bytes for it that way.
    .windows => std.os.windows.NAME_MAX * 3,
    // For WASI, the MAX_NAME will depend on the host OS, so it needs to be
    // as large as the largest max_name_bytes (Windows) in order to work on any host OS.
    // TODO determine if this is a reasonable approach
    .wasi => std.os.windows.NAME_MAX * 3,
    else => if (@hasDecl(root, "os") and @hasDecl(root.os, "NAME_MAX"))
        root.os.NAME_MAX
    else
        @compileError("NAME_MAX not implemented for " ++ @tagName(native_os)),
};

pub const Entry = struct {
    name: []const u8,
    kind: File.Kind,
    inode: File.INode,
};

/// Returns a handle to the current working directory.
///
/// It is not opened with iteration capability. Iterating over the result is
/// illegal behavior.
///
/// Closing the returned `Dir` is checked illegal behavior.
///
/// On POSIX targets, this function is comptime-callable.
///
/// This function is overridable via `std.Options.cwd`.
pub fn cwd() Dir {
    const cwdFn = std.Options.cwd orelse return switch (native_os) {
        .windows => .{ .handle = std.os.windows.peb().ProcessParameters.CurrentDirectory.Handle },
        .wasi => .{ .handle = 3 }, // Expect the first preopen to be current working directory.
        else => .{ .handle = std.posix.AT.FDCWD },
    };
    return cwdFn();
}

pub const Reader = struct {
    dir: Dir,
    state: State,
    /// Stores I/O implementation specific data.
    buffer: []align(@alignOf(usize)) u8,
    /// Index of next entry in `buffer`.
    index: usize,
    /// Fill position of `buffer`.
    end: usize,

    /// A length for `buffer` that allows all implementations to function.
    pub const min_buffer_len = switch (native_os) {
        .linux => std.mem.alignForward(usize, @sizeOf(std.os.linux.dirent64), 8) +
            std.mem.alignForward(usize, max_name_bytes, 8),
        .windows => len: {
            const max_info_len = @sizeOf(std.os.windows.FILE_BOTH_DIR_INFORMATION) + std.os.windows.NAME_MAX * 2;
            const info_align = @alignOf(std.os.windows.FILE_BOTH_DIR_INFORMATION);
            const reserved_len = std.mem.alignForward(usize, max_name_bytes, info_align) - max_info_len;
            break :len std.mem.alignForward(usize, reserved_len, info_align) + max_info_len;
        },
        .wasi => @sizeOf(std.os.wasi.dirent_t) +
            std.mem.alignForward(usize, max_name_bytes, @alignOf(std.os.wasi.dirent_t)),
        .openbsd => std.c.S.BLKSIZE,
        else => if (builtin.link_libc) @sizeOf(std.c.dirent) else std.mem.alignForward(usize, max_name_bytes, @alignOf(usize)),
    };

    pub const State = enum {
        /// Indicates the next call to `read` should rewind and start over the
        /// directory listing.
        reset,
        reading,
        finished,
    };

    pub const Error = error{
        AccessDenied,
        PermissionDenied,
        SystemResources,
    } || Io.UnexpectedError || Io.Cancelable;

    /// Asserts that `buffer` has length at least `min_buffer_len`.
    pub fn init(dir: Dir, buffer: []align(@alignOf(usize)) u8) Reader {
        assert(buffer.len >= min_buffer_len);
        return .{
            .dir = dir,
            .state = .reset,
            .index = 0,
            .end = 0,
            .buffer = buffer,
        };
    }

    /// All `Entry.name` are invalidated with the next call to `read` or
    /// `next`.
    pub fn read(r: *Reader, io: Io, buffer: []Entry) Error!usize {
        return io.vtable.dirRead(io.userdata, r, buffer);
    }

    /// `Entry.name` is invalidated with the next call to `read` or `next`.
    pub fn next(r: *Reader, io: Io) Error!?Entry {
        var buffer: [1]Entry = undefined;
        while (true) {
            const n = try read(r, io, &buffer);
            if (n == 1) return buffer[0];
            if (r.state == .finished) return null;
        }
    }

    pub fn reset(r: *Reader) void {
        r.state = .reset;
        r.index = 0;
        r.end = 0;
    }
};

/// This API is designed for convenience rather than performance:
/// * It chooses a buffer size rather than allowing the user to provide one.
/// * It is movable by only requesting one `Entry` at a time from the `Io`
///   implementation rather than doing batch operations.
///
/// Still, it will do a decent job of minimizing syscall overhead. For a
/// lower level abstraction, see `Reader`. For a higher level abstraction,
/// see `Walker`.
pub const Iterator = struct {
    reader: Reader,
    reader_buffer: [reader_buffer_len]u8 align(@alignOf(usize)),

    pub const reader_buffer_len = 2048;

    comptime {
        assert(reader_buffer_len >= Reader.min_buffer_len);
    }

    pub const Error = Reader.Error;

    pub fn init(dir: Dir, reader_state: Reader.State) Iterator {
        return .{
            .reader = .{
                .dir = dir,
                .state = reader_state,
                .index = 0,
                .end = 0,
                .buffer = undefined,
            },
            .reader_buffer = undefined,
        };
    }

    pub fn next(it: *Iterator, io: Io) Error!?Entry {
        it.reader.buffer = &it.reader_buffer;
        return it.reader.next(io);
    }
};

pub fn iterate(dir: Dir) Iterator {
    return .init(dir, .reset);
}

/// Like `iterate`, but will not reset the directory cursor before the first
/// iteration. This should only be used in cases where it is known that the
/// `Dir` has not had its cursor modified yet (e.g. it was just opened).
pub fn iterateAssumeFirstIteration(dir: Dir) Iterator {
    return .init(dir, .reading);
}

pub const SelectiveWalker = struct {
    stack: std.ArrayList(StackItem),
    name_buffer: std.ArrayList(u8),
    allocator: Allocator,

    pub const Error = Iterator.Error || Allocator.Error;

    const StackItem = struct {
        iter: Iterator,
        dirname_len: usize,
    };

    /// After each call to this function, and on deinit(), the memory returned
    /// from this function becomes invalid. A copy must be made in order to keep
    /// a reference to the path.
    pub fn next(self: *SelectiveWalker, io: Io) Error!?Walker.Entry {
        while (self.stack.items.len > 0) {
            const top = &self.stack.items[self.stack.items.len - 1];
            var dirname_len = top.dirname_len;
            if (top.iter.next(io) catch |err| {
                // If we get an error, then we want the user to be able to continue
                // walking if they want, which means that we need to pop the directory
                // that errored from the stack. Otherwise, all future `next` calls would
                // likely just fail with the same error.
                var item = self.stack.pop().?;
                if (self.stack.items.len != 0) {
                    item.iter.reader.dir.close(io);
                }
                return err;
            }) |entry| {
                self.name_buffer.shrinkRetainingCapacity(dirname_len);
                if (self.name_buffer.items.len != 0) {
                    try self.name_buffer.append(self.allocator, path.sep);
                    dirname_len += 1;
                }
                try self.name_buffer.ensureUnusedCapacity(self.allocator, entry.name.len + 1);
                self.name_buffer.appendSliceAssumeCapacity(entry.name);
                self.name_buffer.appendAssumeCapacity(0);
                const walker_entry: Walker.Entry = .{
                    .dir = top.iter.reader.dir,
                    .basename = self.name_buffer.items[dirname_len .. self.name_buffer.items.len - 1 :0],
                    .path = self.name_buffer.items[0 .. self.name_buffer.items.len - 1 :0],
                    .kind = entry.kind,
                };
                return walker_entry;
            } else {
                var item = self.stack.pop().?;
                if (self.stack.items.len != 0) {
                    item.iter.reader.dir.close(io);
                }
            }
        }
        return null;
    }

    /// Traverses into the directory, continuing walking one level down.
    pub fn enter(self: *SelectiveWalker, io: Io, entry: Walker.Entry) !void {
        if (entry.kind != .directory) {
            @branchHint(.cold);
            return;
        }

        var new_dir = entry.dir.openDir(io, entry.basename, .{ .iterate = true }) catch |err| {
            switch (err) {
                error.NameTooLong => unreachable,
                else => |e| return e,
            }
        };
        errdefer new_dir.close(io);

        try self.stack.append(self.allocator, .{
            .iter = new_dir.iterateAssumeFirstIteration(),
            .dirname_len = self.name_buffer.items.len - 1,
        });
    }

    pub fn deinit(self: *SelectiveWalker) void {
        self.name_buffer.deinit(self.allocator);
        self.stack.deinit(self.allocator);
    }

    /// Leaves the current directory, continuing walking one level up.
    /// If the current entry is a directory entry, then the "current directory"
    /// will pertain to that entry if `enter` is called before `leave`.
    pub fn leave(self: *SelectiveWalker, io: Io) void {
        var item = self.stack.pop().?;
        if (self.stack.items.len != 0) {
            @branchHint(.likely);
            item.iter.reader.dir.close(io);
        }
    }
};

/// Recursively iterates over a directory, but requires the user to
/// opt-in to recursing into each directory entry.
///
/// `dir` must have been opened with `OpenOptions.iterate` set to `true`.
///
/// `Walker.deinit` releases allocated memory and directory handles.
///
/// The order of returned file system entries is undefined.
///
/// `dir` will not be closed after walking it.
///
/// See also `walk`.
pub fn walkSelectively(dir: Dir, allocator: Allocator) !SelectiveWalker {
    var stack: std.ArrayList(SelectiveWalker.StackItem) = .empty;

    try stack.append(allocator, .{
        .iter = dir.iterate(),
        .dirname_len = 0,
    });

    return .{
        .stack = stack,
        .name_buffer = .empty,
        .allocator = allocator,
    };
}

pub const Walker = struct {
    inner: SelectiveWalker,

    pub const Entry = struct {
        /// The containing directory. This can be used to operate directly on `basename`
        /// rather than `path`, avoiding `error.NameTooLong` for deeply nested paths.
        /// The directory remains open until `next` or `deinit` is called.
        dir: Dir,
        basename: [:0]const u8,
        path: [:0]const u8,
        kind: File.Kind,

        /// Returns the depth of the entry relative to the initial directory.
        /// Returns 1 for a direct child of the initial directory, 2 for an entry
        /// within a direct child of the initial directory, etc.
        pub fn depth(self: Walker.Entry) usize {
            return std.mem.countScalar(u8, self.path, path.sep) + 1;
        }
    };

    /// After each call to this function, and on deinit(), the memory returned
    /// from this function becomes invalid. A copy must be made in order to keep
    /// a reference to the path.
    pub fn next(self: *Walker, io: Io) !?Walker.Entry {
        const entry = try self.inner.next(io);
        if (entry != null and entry.?.kind == .directory) {
            try self.inner.enter(io, entry.?);
        }
        return entry;
    }

    pub fn deinit(self: *Walker) void {
        self.inner.deinit();
    }

    /// Leaves the current directory, continuing walking one level up.
    /// If the current entry is a directory entry, then the "current directory"
    /// is the directory pertaining to the current entry.
    pub fn leave(self: *Walker, io: Io) void {
        self.inner.leave(io);
    }
};

/// Recursively iterates over a directory.
///
/// `dir` must have been opened with `OpenOptions.iterate` set to `true`.
///
/// `Walker.deinit` releases allocated memory and directory handles.
///
/// The order of returned file system entries is undefined.
///
/// `dir` will not be closed after walking it.
///
/// See also:
/// * `walkSelectively`
pub fn walk(dir: Dir, allocator: Allocator) Allocator.Error!Walker {
    return .{ .inner = try walkSelectively(dir, allocator) };
}

pub const PathNameError = error{
    /// Returned when an insufficient buffer is provided that cannot fit the
    /// path name.
    NameTooLong,
    /// File system cannot encode the requested file name bytes.
    /// Could be due to invalid WTF-8 on Windows, invalid UTF-8 on WASI,
    /// invalid characters on Windows, etc. Filesystem and operating specific.
    BadPathName,
};

pub const AccessError = error{
    AccessDenied,
    PermissionDenied,
    FileNotFound,
    InputOutput,
    SystemResources,
    FileBusy,
    SymLinkLoop,
    ReadOnlyFileSystem,
} || PathNameError || Io.Cancelable || Io.UnexpectedError;

pub const AccessOptions = packed struct {
    follow_symlinks: bool = true,
    read: bool = false,
    write: bool = false,
    execute: bool = false,
};

/// Test accessing `sub_path`.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
///
/// Be careful of Time-Of-Check-Time-Of-Use race conditions when using this
/// function. For example, instead of testing if a file exists and then opening
/// it, just open it and handle the error for file not found.
pub fn access(dir: Dir, io: Io, sub_path: []const u8, options: AccessOptions) AccessError!void {
    return io.vtable.dirAccess(io.userdata, dir, sub_path, options);
}

pub fn accessAbsolute(io: Io, absolute_path: []const u8, options: AccessOptions) AccessError!void {
    assert(path.isAbsolute(absolute_path));
    return access(.cwd(), io, absolute_path, options);
}

pub const OpenError = error{
    FileNotFound,
    NotDir,
    AccessDenied,
    PermissionDenied,
    SymLinkLoop,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    NoDevice,
    SystemResources,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
} || PathNameError || Io.Cancelable || Io.UnexpectedError;

pub const OpenOptions = struct {
    /// `true` means the opened directory can be used as the `Dir` parameter
    /// for functions which operate based on an open directory handle. When `false`,
    /// such operations are Illegal Behavior.
    access_sub_paths: bool = true,
    /// `true` means the opened directory can be scanned for the files and sub-directories
    /// of the result. It means the `iterate` function can be called.
    iterate: bool = false,
    /// `false` means it won't dereference the symlinks.
    follow_symlinks: bool = true,
};

/// Opens a directory at the given path. The directory is a system resource that remains
/// open until `close` is called on the result.
///
/// The directory cannot be iterated unless the `iterate` option is set to `true`.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn openDir(dir: Dir, io: Io, sub_path: []const u8, options: OpenOptions) OpenError!Dir {
    return io.vtable.dirOpenDir(io.userdata, dir, sub_path, options);
}

pub fn openDirAbsolute(io: Io, absolute_path: []const u8, options: OpenOptions) OpenError!Dir {
    assert(path.isAbsolute(absolute_path));
    return openDir(.cwd(), io, absolute_path, options);
}

pub fn close(dir: Dir, io: Io) void {
    return io.vtable.dirClose(io.userdata, (&dir)[0..1]);
}

pub fn closeMany(io: Io, dirs: []const Dir) void {
    return io.vtable.dirClose(io.userdata, dirs);
}

pub const OpenFileOptions = struct {
    mode: Mode = .read_only,
    /// Determines the behavior when opening a path that refers to a directory.
    ///
    /// If set to true, directorie
```
