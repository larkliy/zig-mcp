```
ed = 505, // RFC7231, Section 6.6.6
    variant_also_negotiates = 506, // RFC2295
    insufficient_storage = 507, // RFC4918
    loop_detected = 508, // RFC5842
    not_extended = 510, // RFC2774
    network_authentication_required = 511, // RFC6585

    _,

    pub fn phrase(self: Status) ?[]const u8 {
        return switch (self) {
            // 1xx statuses
            .@"continue" => "Continue",
            .switching_protocols => "Switching Protocols",
            .processing => "Processing",
            .early_hints => "Early Hints",

            // 2xx statuses
            .ok => "OK",
            .created => "Created",
            .accepted => "Accepted",
            .non_authoritative_info => "Non-Authoritative Information",
            .no_content => "No Content",
            .reset_content => "Reset Content",
            .partial_content => "Partial Content",
            .multi_status => "Multi-Status",
            .already_reported => "Already Reported",
            .im_used => "IM Used",

            // 3xx statuses
            .multiple_choice => "Multiple Choice",
            .moved_permanently => "Moved Permanently",
            .found => "Found",
            .see_other => "See Other",
            .not_modified => "Not Modified",
            .use_proxy => "Use Proxy",
            .temporary_redirect => "Temporary Redirect",
            .permanent_redirect => "Permanent Redirect",

            // 4xx statuses
            .bad_request => "Bad Request",
            .unauthorized => "Unauthorized",
            .payment_required => "Payment Required",
            .forbidden => "Forbidden",
            .not_found => "Not Found",
            .method_not_allowed => "Method Not Allowed",
            .not_acceptable => "Not Acceptable",
            .proxy_auth_required => "Proxy Authentication Required",
            .request_timeout => "Request Timeout",
            .conflict => "Conflict",
            .gone => "Gone",
            .length_required => "Length Required",
            .precondition_failed => "Precondition Failed",
            .payload_too_large => "Payload Too Large",
            .uri_too_long => "URI Too Long",
            .unsupported_media_type => "Unsupported Media Type",
            .range_not_satisfiable => "Range Not Satisfiable",
            .expectation_failed => "Expectation Failed",
            .teapot => "I'm a teapot",
            .misdirected_request => "Misdirected Request",
            .unprocessable_entity => "Unprocessable Entity",
            .locked => "Locked",
            .failed_dependency => "Failed Dependency",
            .too_early => "Too Early",
            .upgrade_required => "Upgrade Required",
            .precondition_required => "Precondition Required",
            .too_many_requests => "Too Many Requests",
            .request_header_fields_too_large => "Request Header Fields Too Large",
            .unavailable_for_legal_reasons => "Unavailable For Legal Reasons",

            // 5xx statuses
            .internal_server_error => "Internal Server Error",
            .not_implemented => "Not Implemented",
            .bad_gateway => "Bad Gateway",
            .service_unavailable => "Service Unavailable",
            .gateway_timeout => "Gateway Timeout",
            .http_version_not_supported => "HTTP Version Not Supported",
            .variant_also_negotiates => "Variant Also Negotiates",
            .insufficient_storage => "Insufficient Storage",
            .loop_detected => "Loop Detected",
            .not_extended => "Not Extended",
            .network_authentication_required => "Network Authentication Required",

            else => return null,
        };
    }

    pub const Class = enum {
        informational,
        success,
        redirect,
        client_error,
        server_error,
    };

    pub fn class(self: Status) Class {
        return switch (@intFromEnum(self)) {
            100...199 => .informational,
            200...299 => .success,
            300...399 => .redirect,
            400...499 => .client_error,
            else => .server_error,
        };
    }

    test {
        try std.testing.expectEqualStrings("OK", Status.ok.phrase().?);
        try std.testing.expectEqualStrings("Not Found", Status.not_found.phrase().?);
    }

    test {
        try std.testing.expectEqual(Status.Class.success, Status.ok.class());
        try std.testing.expectEqual(Status.Class.client_error, Status.not_found.class());
    }
};

/// compression is intentionally omitted here since it is handled in `ContentEncoding`.
pub const TransferEncoding = enum {
    chunked,
    none,
};

pub const ContentEncoding = enum {
    zstd,
    gzip,
    deflate,
    compress,
    identity,

    pub fn fromString(s: []const u8) ?ContentEncoding {
        const map = std.StaticStringMap(ContentEncoding).initComptime(.{
            .{ "zstd", .zstd },
            .{ "gzip", .gzip },
            .{ "x-gzip", .gzip },
            .{ "deflate", .deflate },
            .{ "compress", .compress },
            .{ "x-compress", .compress },
            .{ "identity", .identity },
        });
        return map.get(s);
    }

    pub fn minBufferCapacity(ce: ContentEncoding) usize {
        return switch (ce) {
            .zstd => std.compress.zstd.default_window_len,
            .gzip, .deflate => std.compress.flate.max_window_len,
            .compress, .identity => 0,
        };
    }
};

pub const Connection = enum {
    keep_alive,
    close,
};

pub const Header = struct {
    name: []const u8,
    value: []const u8,
};

pub const Reader = struct {
    in: *std.Io.Reader,
    /// This is preallocated memory that might be used by `bodyReader`. That
    /// function might return a pointer to this field, or a different
    /// `*std.Io.Reader`. Advisable to not access this field directly.
    interface: std.Io.Reader,
    /// Keeps track of whether the stream is ready to accept a new request,
    /// making invalid API usage cause assertion failures rather than HTTP
    /// protocol violations.
    state: State,
    /// HTTP trailer bytes. These are at the end of a transfer-encoding:
    /// chunked message. This data is available only after calling one of the
    /// "end" functions and points to data inside the buffer of `in`, and is
    /// therefore invalidated on the next call to `receiveHead`, or any other
    /// read from `in`.
    trailers: []const u8 = &.{},
    body_err: ?BodyError = null,
    max_head_len: usize,

    pub const RemainingChunkLen = enum(u64) {
        head = 0,
        n = 1,
        rn = 2,
        _,

        pub fn init(integer: u64) RemainingChunkLen {
            return @enumFromInt(integer);
        }

        pub fn int(rcl: RemainingChunkLen) u64 {
            return @intFromEnum(rcl);
        }
    };

    pub const State = union(enum) {
        /// The stream is available to be used for the first time, or reused.
        ready,
        received_head,
        /// The stream goes until the connection is closed.
        body_none,
        body_remaining_content_length: u64,
        body_remaining_chunk_len: RemainingChunkLen,
        /// The stream would be eligible for another HTTP request, however the
        /// client and server did not negotiate a persistent connection.
        closing,
    };

    pub const BodyError = error{
        HttpChunkInvalid,
        HttpChunkTruncated,
        HttpHeadersOversize,
    };

    pub const HeadError = error{
        /// Too many bytes of HTTP headers.
        ///
        /// The HTTP specification suggests to respond with a 431 status code
        /// before closing the connection.
        HttpHeadersOversize,
        /// Partial HTTP request was received but the connection was closed
        /// before fully receiving the headers.
        HttpRequestTruncated,
        /// The client sent 0 bytes of headers before closing the stream. This
        /// happens when a keep-alive connection is finally closed.
        HttpConnectionClosing,
        /// Transitive error occurred reading from `in`.
        ReadFailed,
    };

    /// Buffers the entire head inside `in`.
    ///
    /// The resulting memory is invalidated by any subsequent consumption of
    /// the input stream.
    pub fn receiveHead(reader: *Reader) HeadError![]const u8 {
        reader.trailers = &.{};
        const in = reader.in;
        const max_head_len = reader.max_head_len;
        var hp: HeadParser = .{};
        var head_len: usize = 0;
        while (true) {
            if (head_len >= max_head_len) return error.HttpHeadersOversize;
            const remaining = in.buffered()[head_len..];
            if (remaining.len == 0) {
                in.fillMore() catch |err| switch (err) {
                    error.EndOfStream => switch (head_len) {
                        0 => return error.HttpConnectionClosing,
                        else => return error.HttpRequestTruncated,
                    },
                    error.ReadFailed => return error.ReadFailed,
                };
                continue;
            }
            head_len += hp.feed(remaining);
            if (hp.state == .finished) {
                reader.state = .received_head;
                const head_buffer = in.buffered()[0..head_len];
                in.toss(head_len);
                return head_buffer;
            }
        }
    }

    /// If compressed body has been negotiated this will return compressed bytes.
    ///
    /// Asserts only called once and after `receiveHead`.
    ///
    /// See also:
    /// * `interfaceDecompressing`
    pub fn bodyReader(
        reader: *Reader,
        transfer_buffer: []u8,
        transfer_encoding: TransferEncoding,
        content_length: ?u64,
    ) *std.Io.Reader {
        assert(reader.state == .received_head);
        switch (transfer_encoding) {
            .chunked => {
                reader.state = .{ .body_remaining_chunk_len = .head };
                reader.interface = .{
                    .buffer = transfer_buffer,
                    .seek = 0,
                    .end = 0,
                    .vtable = &.{
                        .stream = chunkedStream,
                        .discard = chunkedDiscard,
                    },
                };
                return &reader.interface;
            },
            .none => {
                if (content_length) |len| {
                    reader.state = if (len == 0) .ready else .{ .body_remaining_content_length = len };
                    reader.interface = .{
                        .buffer = transfer_buffer,
                        .seek = 0,
                        .end = 0,
                        .vtable = &.{
                            .stream = contentLengthStream,
                            .discard = contentLengthDiscard,
                        },
                    };
                    return &reader.interface;
                } else {
                    reader.state = .body_none;
                    return reader.in;
                }
            },
        }
    }

    /// If compressed body has been negotiated this will return decompressed bytes.
    ///
    /// Asserts only called once and after `receiveHead`.
    ///
    /// See also:
    /// * `interface`
    pub fn bodyReaderDecompressing(
        reader: *Reader,
        transfer_buffer: []u8,
        transfer_encoding: TransferEncoding,
        content_length: ?u64,
        content_encoding: ContentEncoding,
        decompress: *Decompress,
        decompress_buffer: []u8,
    ) *std.Io.Reader {
        if (transfer_encoding == .none and content_length == null) {
            assert(reader.state == .received_head);
            reader.state = .body_none;
            switch (content_encoding) {
                .identity => {
                    return reader.in;
                },
                .deflate => {
                    decompress.* = .{ .flate = .init(reader.in, .zlib, decompress_buffer) };
                    return &decompress.flate.reader;
                },
                .gzip => {
                    decompress.* = .{ .flate = .init(reader.in, .gzip, decompress_buffer) };
                    return &decompress.flate.reader;
                },
                .zstd => {
                    decompress.* = .{ .zstd = .init(reader.in, decompress_buffer, .{ .verify_checksum = false }) };
                    return &decompress.zstd.reader;
                },
                .compress => unreachable,
            }
        }
        const transfer_reader = bodyReader(reader, transfer_buffer, transfer_encoding, content_length);
        return decompress.init(transfer_reader, decompress_buffer, content_encoding);
    }

    fn contentLengthStream(
        io_r: *std.Io.Reader,
        w: *Writer,
        limit: std.Io.Limit,
    ) std.Io.Reader.StreamError!usize {
        const reader: *Reader = @alignCast(@fieldParentPtr("interface", io_r));
        if (reader.state == .ready) return error.EndOfStream;
        const remaining_content_length = &reader.state.body_remaining_content_length;
        const remaining = remaining_content_length.*;
        const n = try reader.in.stream(w, limit.min(.limited64(remaining)));
        if (n == remaining) {
            reader.state = .ready;
        } else {
            remaining_content_length.* = remaining - n;
        }
        return n;
    }

    fn contentLengthDiscard(io_r: *std.Io.Reader, limit: std.Io.Limit) std.Io.Reader.Error!usize {
        const reader: *Reader = @alignCast(@fieldParentPtr("interface", io_r));
        if (reader.state == .ready) return error.EndOfStream;
        const remaining_content_length = &reader.state.body_remaining_content_length;
        const remaining = remaining_content_length.*;
        const n = try reader.in.discard(limit.min(.limited64(remaining)));
        if (n == remaining) {
            reader.state = .ready;
        } else {
            remaining_content_length.* = remaining - n;
        }
        return n;
    }

    fn chunkedStream(io_r: *std.Io.Reader, w: *Writer, limit: std.Io.Limit) std.Io.Reader.StreamError!usize {
        const reader: *Reader = @alignCast(@fieldParentPtr("interface", io_r));
        const chunk_len_ptr = switch (reader.state) {
            .ready => return error.EndOfStream,
            .body_remaining_chunk_len => |*x| x,
            else => unreachable,
        };
        return chunkedReadEndless(reader, w, limit, chunk_len_ptr) catch |err| switch (err) {
            error.ReadFailed => return error.ReadFailed,
            error.WriteFailed => return error.WriteFailed,
            error.EndOfStream => {
                reader.body_err = error.HttpChunkTruncated;
                return error.ReadFailed;
            },
            else => |e| {
                reader.body_err = e;
                return error.ReadFailed;
            },
        };
    }

    fn chunkedReadEndless(
        reader: *Reader,
        w: *Writer,
        limit: std.Io.Limit,
        chunk_len_ptr: *RemainingChunkLen,
    ) (BodyError || std.Io.Reader.StreamError)!usize {
        const in = reader.in;
        len: switch (chunk_len_ptr.*) {
            .head => {
                var cp: ChunkParser = .init;
                while (true) {
                    const i = cp.feed(in.buffered());
                    switch (cp.state) {
                        .invalid => return error.HttpChunkInvalid,
                        .data => {
                            in.toss(i);
                            break;
                        },
                        else => {
                            in.toss(i);
                            try in.fillMore();
                            continue;
                        },
                    }
                }
                if (cp.chunk_len == 0) return parseTrailers(reader, 0);
                const n = try in.stream(w, limit.min(.limited64(cp.chunk_len)));
                chunk_len_ptr.* = .init(cp.chunk_len + 2 - n);
                return n;
            },
            .n => {
                if ((try in.peekByte()) != '\n') return error.HttpChunkInvalid;
                in.toss(1);
                continue :len .head;
            },
            .rn => {
                const rn = try in.peekArray(2);
                if (rn[0] != '\r' or rn[1] != '\n') return error.HttpChunkInvalid;
                in.toss(2);
                continue :len .head;
            },
            else => |remaining_chunk_len| {
                const n = try in.stream(w, limit.min(.limited64(@intFromEnum(remaining_chunk_len) - 2)));
                chunk_len_ptr.* = .init(@intFromEnum(remaining_chunk_len) - n);
                return n;
            },
        }
    }

    fn chunkedDiscard(io_r: *std.Io.Reader, limit: std.Io.Limit) std.Io.Reader.Error!usize {
        const reader: *Reader = @alignCast(@fieldParentPtr("interface", io_r));
        const chunk_len_ptr = switch (reader.state) {
            .ready => return error.EndOfStream,
            .body_remaining_chunk_len => |*x| x,
            else => unreachable,
        };
        return chunkedDiscardEndless(reader, limit, chunk_len_ptr) catch |err| switch (err) {
            error.ReadFailed => return error.ReadFailed,
            error.EndOfStream => {
                reader.body_err = error.HttpChunkTruncated;
                return error.ReadFailed;
            },
            else => |e| {
                reader.body_err = e;
                return error.ReadFailed;
            },
        };
    }

    fn chunkedDiscardEndless(
        reader: *Reader,
        limit: std.Io.Limit,
        chunk_len_ptr: *RemainingChunkLen,
    ) (BodyError || std.Io.Reader.Error)!usize {
        const in = reader.in;
        len: switch (chunk_len_ptr.*) {
            .head => {
                var cp: ChunkParser = .init;
                while (true) {
                    const i = cp.feed(in.buffered());
                    switch (cp.state) {
                        .invalid => return error.HttpChunkInvalid,
                        .data => {
                            in.toss(i);
                            break;
                        },
                        else => {
                            in.toss(i);
                            try in.fillMore();
                            continue;
                        },
                    }
                }
                if (cp.chunk_len == 0) return parseTrailers(reader, 0);
                const n = try in.discard(limit.min(.limited64(cp.chunk_len)));
                chunk_len_ptr.* = .init(cp.chunk_len + 2 - n);
                return n;
            },
            .n => {
                if ((try in.peekByte()) != '\n') return error.HttpChunkInvalid;
                in.toss(1);
                continue :len .head;
            },
            .rn => {
                const rn = try in.peekArray(2);
                if (rn[0] != '\r' or rn[1] != '\n') return error.HttpChunkInvalid;
                in.toss(2);
                continue :len .head;
            },
            else => |remaining_chunk_len| {
                const n = try in.discard(limit.min(.limited64(remaining_chunk_len.int() - 2)));
                chunk_len_ptr.* = .init(remaining_chunk_len.int() - n);
                return n;
            },
        }
    }

    /// Called when next bytes in the stream are trailers, or "\r\n" to indicate
    /// end of chunked body.
    fn parseTrailers(reader: *Reader, amt_read: usize) (BodyError || std.Io.Reader.Error)!usize {
        const in = reader.in;
        const rn = try in.peekArray(2);
        if (rn[0] == '\r' and rn[1] == '\n') {
            in.toss(2);
            reader.state = .ready;
            assert(reader.trailers.len == 0);
            return amt_read;
        }
        var hp: HeadParser = .{ .state = .seen_rn };
        var trailers_len: usize = 2;
        while (true) {
            if (in.buffer.len - trailers_len == 0) return error.HttpHeadersOversize;
            const remaining = in.buffered()[trailers_len..];
            if (remaining.len == 0) {
                try in.fillMore();
                continue;
            }
            trailers_len += hp.feed(remaining);
            if (hp.state == .finished) {
                reader.state = .ready;
                reader.trailers = in.buffered()[0..trailers_len];
                in.toss(trailers_len);
                return amt_read;
            }
        }
    }
};

pub const Decompress = union(enum) {
    flate: std.compress.flate.Decompress,
    zstd: std.compress.zstd.Decompress,
    none: *std.Io.Reader,

    pub fn init(
        decompress: *Decompress,
        transfer_reader: *std.Io.Reader,
        buffer: []u8,
        content_encoding: ContentEncoding,
    ) *std.Io.Reader {
        switch (content_encoding) {
            .identity => {
                decompress.* = .{ .none = transfer_reader };
                return transfer_reader;
            },
            .deflate => {
                decompress.* = .{ .flate = .init(transfer_reader, .zlib, buffer) };
                return &decompress.flate.reader;
            },
            .gzip => {
                decompress.* = .{ .flate = .init(transfer_reader, .gzip, buffer) };
                return &decompress.flate.reader;
            },
            .zstd => {
                decompress.* = .{ .zstd = .init(transfer_reader, buffer, .{ .verify_checksum = false }) };
                return &decompress.zstd.reader;
            },
            .compress => unreachable,
        }
    }
};

/// Request or response body.
pub const BodyWriter = struct {
    /// Until the lifetime of `BodyWriter` ends, it is illegal to modify the
    /// state of this other than via methods of `BodyWriter`.
    http_protocol_output: *Writer,
    state: State,
    writer: Writer,

    pub const Error = Writer.Error;

    /// How many zeroes to reserve for hex-encoded chunk length.
    const chunk_len_digits = 8;
    const max_chunk_len: usize = std.math.pow(u64, 16, chunk_len_digits) - 1;
    const chunk_header_template = ("0" ** chunk_len_digits) ++ "\r\n";

    comptime {
        assert(max_chunk_len == std.math.maxInt(u32));
    }

    pub const State = union(enum) {
        /// End of connection signals the end of the stream.
        none,
        /// As a debugging utility, counts down to zero as bytes are written.
        content_length: u64,
        /// Each chunk is wrapped in a header and trailer.
        /// This length is the the number of bytes to be written before the
        /// next header. This includes +2 for the `\r\n` trailer and is zero
        /// for the beginning of the stream.
        chunk_len: usize,
        /// Cleanly finished stream; connection can be reused.
        end,

        pub const init_chunked: State = .{ .chunk_len = 0 };
    };

    pub fn isEliding(w: *const BodyWriter) bool {
        return w.writer.vtable.drain == elidingDrain;
    }

    /// Sends all buffered data across `BodyWriter.http_protocol_output`.
    pub fn flush(w: *BodyWriter) Error!void {
        const out = w.http_protocol_output;
        switch (w.state) {
            .end, .none, .content_length, .chunk_len => return out.flush(),
        }
    }

    /// When using content-length, asserts that the amount of data sent matches
    /// the value sent in the header, then flushes `http_protocol_output`.
    ///
    /// When using transfer-encoding: chunked, writes the end-of-stream message
    /// with empty trailers, then flushes the stream to the system. Asserts any
    /// started chunk has been completely finished.
    ///
    /// Respects the value of `isEliding` to omit all data after the headers.
    ///
    /// See also:
    /// * `endUnflushed`
    /// * `endChunked`
    pub fn end(w: *BodyWriter) Error!void {
        try endUnflushed(w);
        try w.http_protocol_output.flush();
    }

    /// When using content-length, asserts that the amount of data sent matches
    /// the value sent in the header.
    ///
    /// Otherwise, transfer-encoding: chunked is being used, and it writes the
    /// end-of-stream message with empty trailers.
    ///
    /// Respects the value of `isEliding` to omit all data after the headers.
    ///
    /// Does not flush `http_protocol_output`, but does flush `writer`.
    ///
    /// See also:
    /// * `end`
    /// * `endChunked`
    pub fn endUnflushed(w: *BodyWriter) Error!void {
        try w.writer.flush();
        switch (w.state) {
            .end => unreachable,
            .content_length => |len| {
                assert(len == 0); // Trips when end() called before all bytes written.
                w.state = .end;
            },
            .none => {},
            .chunk_len => return endChunkedUnflushed(w, .{}),
        }
    }

    pub const EndChunkedOptions = struct {
        trailers: []const Header = &.{},
    };

    /// Writes the end-of-stream message and any optional trailers, flushing
    /// the underlying stream.
    ///
    /// Asserts that the BodyWriter is using transfer-encoding: chunked.
    ///
    /// Respects the value of `isEliding` to omit all data after the headers.
    ///
    /// See also:
    /// * `endChunkedUnflushed`
    /// * `end`
    pub fn endChunked(w: *BodyWriter, options: EndChunkedOptions) Error!void {
        try endChunkedUnflushed(w, options);
        try w.http_protocol_output.flush();
    }

    /// Writes the end-of-stream message and any optional trailers.
    ///
    /// Does not flush.
    ///
    /// Asserts that the BodyWriter is using transfer-encoding: chunked.
    ///
    /// Respects the value of `isEliding` to omit all data after the headers.
    ///
    /// See also:
    /// * `endChunked`
    /// * `endUnflushed`
    /// * `end`
    pub fn endChunkedUnflushed(w: *BodyWriter, options: EndChunkedOptions) Error!void {
        if (w.isEliding()) {
            w.state = .end;
            return;
        }
        const bw = w.http_protocol_output;
        switch (w.state.chunk_len) {
            0 => {},
            1 => unreachable, // Wrote more data than specified in chunk header.
            2 => try bw.writeAll("\r\n"),
            else => unreachable, // An earlier write call indicated more data would follow.
        }
        try bw.writeAll("0\r\n");
        for (options.trailers) |trailer| {
            try bw.writeAll(trailer.name);
            try bw.writeAll(": ");
            try bw.writeAll(trailer.value);
            try bw.writeAll("\r\n");
        }
        try bw.writeAll("\r\n");
        w.state = .end;
    }

    pub fn contentLengthDrain(w: *Writer, data: []const []const u8, splat: usize) Error!usize {
        const bw: *BodyWriter = @alignCast(@fieldParentPtr("writer", w));
        assert(!bw.isEliding());
        const out = bw.http_protocol_output;
        const n = try out.writeSplatHeader(w.buffered(), data, splat);
        bw.state.content_length -= n;
        return w.consume(n);
    }

    pub fn noneDrain(w: *Writer, data: []const []const u8, splat: usize) Error!usize {
        const bw: *BodyWriter = @alignCast(@fieldParentPtr("writer", w));
        assert(!bw.isEliding());
        const out = bw.http_protocol_output;
        const n = try out.writeSplatHeader(w.buffered(), data, splat);
        return w.consume(n);
    }

    pub fn elidingDrain(w: *Writer, data: []const []const u8, splat: usize) Error!usize {
        const bw: *BodyWriter = @alignCast(@fieldParentPtr("writer", w));
        const slice = data[0 .. data.len - 1];
        const pattern = data[slice.len];
        var written: usize = pattern.len * splat;
        for (slice) |bytes| written += bytes.len;
        switch (bw.state) {
            .content_length => |*len| len.* -= written + w.end,
            else => {},
        }
        w.end = 0;
        return written;
    }

    pub fn elidingSendFile(w: *Writer, file_reader: *File.Reader, limit: std.Io.Limit) Writer.FileError!usize {
        const bw: *BodyWriter = @alignCast(@fieldParentPtr("writer", w));
        if (File.Handle == void) return error.Unimplemented;
        if (builtin.zig_backend == .stage2_aarch64) return error.Unimplemented;
        switch (bw.state) {
            .content_length => |*len| len.* -= w.end,
            else => {},
        }
        w.end = 0;
        if (limit == .nothing) return 0;
        if (file_reader.getSize()) |size| {
            const n = limit.minInt64(size - file_reader.pos);
            if (n == 0) return error.EndOfStream;
            file_reader.seekBy(@intCast(n)) catch return error.Unimplemented;
            switch (bw.state) {
                .content_length => |*len| len.* -= n,
                else => {},
            }
            return n;
        } else |_| {
            // Error is observable on `file_reader` instance, and it is better to
            // treat the file as a pipe.
            return error.Unimplemented;
        }
    }

    /// Returns `null` if size cannot be computed without making any syscalls.
    pub fn noneSendFile(w: *Writer, file_reader: *File.Reader, limit: std.Io.Limit) Writer.FileError!usize {
        const bw: *BodyWriter = @alignCast(@fieldParentPtr("writer", w));
        assert(!bw.isEliding());
        const out = bw.http_protocol_output;
        const n = try out.sendFileHeader(w.buffered(), file_reader, limit);
        return w.consume(n);
    }

    pub fn contentLengthSendFile(w: *Writer, file_reader: *File.Reader, limit: std.Io.Limit) Writer.FileError!usize {
        const bw: *BodyWriter = @alignCast(@fieldParentPtr("writer", w));
        assert(!bw.isEliding());
        const out = bw.http_protocol_output;
        const n = try out.sendFileHeader(w.buffered(), file_reader, limit);
        bw.state.content_length -= n;
        return w.consume(n);
    }

    pub fn chunkedSendFile(w: *Writer, file_reader: *File.Reader, limit: std.Io.Limit) Writer.FileError!usize {
        const bw: *BodyWriter = @alignCast(@fieldParentPtr("writer", w));
        assert(!bw.isEliding());
        const data_len = Writer.countSendFileLowerBound(w.end, file_reader, limit) orelse {
            // If the file size is unknown, we cannot lower to a `sendFile` since we would
            // have to flush the chunk header before knowing the chunk length.
            return error.Unimplemented;
        };
        if (data_len == 0) return error.EndOfStream;
        const out = bw.http_protocol_output;
        l: switch (bw.state.chunk_len) {
            0 => {
                const header_buf = try out.writableArray(chunk_header_template.len);
                @memcpy(header_buf, chunk_header_template);
                writeHex(header_buf[0..chunk_len_digits], data_len);
                bw.state.chunk_len = data_len + 2;
                continue :l bw.state.chunk_len;
            },
            1 => unreachable, // Wrote more data than specified in chunk header.
            2 => {
                try out.writeAll("\r\n");
                bw.state.chunk_len = 0;
                continue :l 0;
            },
            else => {
                const chunk_limit: std.Io.Limit = .limited(bw.state.chunk_len - 2);
                const n = if (chunk_limit.subtract(w.buffered().len)) |sendfile_limit|
                    try out.sendFileHeader(w.buffered(), file_reader, sendfile_limit.min(limit))
                else
                    try out.write(chunk_limit.slice(w.buffered()));
                bw.state.chunk_len -= n;
                return w.consume(n);
            },
        }
    }

    pub fn chunkedDrain(w: *Writer, data: []const []const u8, splat: usize) Error!usize {
        const bw: *BodyWriter = @alignCast(@fieldParentPtr("writer", w));
        assert(!bw.isEliding());
        const out = bw.http_protocol_output;
        const data_len = w.end + Writer.countSplat(data, splat);
        l: switch (bw.state.chunk_len) {
            0 => {
                const header_buf = try out.writableArray(chunk_header_template.len);
                @memcpy(header_buf, chunk_header_template);
                writeHex(header_buf[0..chunk_len_digits], data_len);
                bw.state.chunk_len = data_len + 2;
                continue :l bw.state.chunk_len;
            },
            1 => unreachable, // Wrote more data than specified in chunk header.
            2 => {
                try out.writeAll("\r\n");
                bw.state.chunk_len = 0;
                continue :l 0;
            },
            else => {
                const n = try out.writeSplatHeaderLimit(w.buffered(), data, splat, .limited(bw.state.chunk_len - 2));
                bw.state.chunk_len -= n;
                return w.consume(n);
            },
        }
    }

    /// Writes an integer as base 16 to `buf`, right-aligned, assuming the
    /// buffer has already been filled with zeroes.
    fn writeHex(buf: []u8, x: usize) void {
        assert(std.mem.allEqual(u8, buf, '0'));
        const base = 16;
        var index: usize = buf.len;
        var a = x;
        while (a > 0) {
            const digit = a % base;
            index -= 1;
            buf[index] = std.fmt.digitToChar(@intCast(digit), .lower);
            a /= base;
        }
    }
};

test {
    _ = Server;
    _ = Status;
    _ = Method;
    _ = ChunkParser;
    _ = HeadParser;

    if (builtin.os.tag != .wasi) {
        _ = Client;
        _ = @import("http/test.zig");
    }
}



---
File: /std/Io.zig
---

//! A cross-platform interface that abstracts all I/O operations and
//! concurrency. It includes:
//! * file system
//! * networking
//! * processes
//! * time and sleeping
//! * randomness
//! * async, await, concurrent, and cancel
//! * concurrent queues
//! * wait groups and select
//! * mutexes, futexes, events, and conditions
//! * memory mapped files
//! This interface allows programmers to write optimal, reusable code while
//! participating in these operations.
const Io = @This();

const builtin = @import("builtin");

const std = @import("std.zig");
const math = std.math;
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;
const Alignment = std.mem.Alignment;

userdata: ?*anyopaque,
vtable: *const VTable,

pub const Threaded = @import("Io/Threaded.zig");

pub const fiber = @import("Io/fiber.zig");
pub const Evented = if (fiber.supported) switch (builtin.os.tag) {
    .linux => Uring,
    .dragonfly, .freebsd, .netbsd, .openbsd => Kqueue,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => Dispatch,
    else => void,
} else void; // context-switching code not implemented yet
pub const Dispatch = @import("Io/Dispatch.zig");
pub const Kqueue = @import("Io/Kqueue.zig");
pub const Uring = @import("Io/Uring.zig");

pub const Reader = @import("Io/Reader.zig");
pub const Writer = @import("Io/Writer.zig");
pub const net = @import("Io/net.zig");
pub const Dir = @import("Io/Dir.zig");
pub const File = @import("Io/File.zig");
pub const Terminal = @import("Io/Terminal.zig");

pub const RwLock = @import("Io/RwLock.zig");
pub const Semaphore = @import("Io/Semaphore.zig");

pub const VTable = struct {
    crashHandler: *const fn (?*anyopaque) void,

    /// If it returns `null` it means `result` has been already populated and
    /// `await` will be a no-op.
    ///
    /// When this function returns non-null, the implementation guarantees that
    /// a unit of concurrency has been assigned to the returned task.
    ///
    /// Thread-safe.
    async: *const fn (
        /// Corresponds to `Io.userdata`.
        userdata: ?*anyopaque,
        /// The pointer of this slice is an "eager" result value.
        /// The length is the size in bytes of the result type.
        /// This pointer's lifetime expires directly after the call to this function.
        result: []u8,
        result_alignment: std.mem.Alignment,
        /// Copied and then passed to `start`.
        context: []const u8,
        context_alignment: std.mem.Alignment,
        start: *const fn (context: *const anyopaque, result: *anyopaque) void,
    ) ?*AnyFuture,
    /// Thread-safe.
    concurrent: *const fn (
        /// Corresponds to `Io.userdata`.
        userdata: ?*anyopaque,
        result_len: usize,
        result_alignment: std.mem.Alignment,
        /// Copied and then passed to `start`.
        context: []const u8,
        context_alignment: std.mem.Alignment,
        start: *const fn (context: *const anyopaque, result: *anyopaque) void,
    ) ConcurrentError!*AnyFuture,
    /// This function is only called when `async` returns a non-null value.
    ///
    /// Thread-safe.
    await: *const fn (
        /// Corresponds to `Io.userdata`.
        userdata: ?*anyopaque,
        /// The same value that was returned from `async`.
        any_future: *AnyFuture,
        /// Points to a buffer where the result is written.
        /// The length is equal to size in bytes of result type.
        result: []u8,
        result_alignment: std.mem.Alignment,
    ) void,
    /// Equivalent to `await` but initiates cancel request.
    ///
    /// This function is only called when `async` returns a non-null value.
    ///
    /// Thread-safe.
    cancel: *const fn (
        /// Corresponds to `Io.userdata`.
        userdata: ?*anyopaque,
        /// The same value that was returned from `async`.
        any_future: *AnyFuture,
        /// Points to a buffer where the result is written.
        /// The length is equal to size in bytes of result type.
        result: []u8,
        result_alignment: std.mem.Alignment,
    ) void,

    /// When this function returns, implementation guarantees that `start` has
    /// either already been called, or a unit of concurrency has been assigned
    /// to the task of calling the function.
    ///
    /// Thread-safe.
    groupAsync: *const fn (
        /// Corresponds to `Io.userdata`.
        userdata: ?*anyopaque,
        /// Owner of the spawned async task.
        group: *Group,
        /// Copied and then passed to `start`.
        context: []const u8,
        context_alignment: std.mem.Alignment,
        start: *const fn (context: *const anyopaque) void,
    ) void,
    /// Thread-safe.
    groupConcurrent: *const fn (
        /// Corresponds to `Io.userdata`.
        userdata: ?*anyopaque,
        /// Owner of the spawned async task.
        group: *Group,
        /// Copied and then passed to `start`.
        context: []const u8,
        context_alignment: std.mem.Alignment,
        start: *const fn (context: *const anyopaque) void,
    ) ConcurrentError!void,
    groupAwait: *const fn (?*anyopaque, *Group, token: *anyopaque) Cancelable!void,
    groupCancel: *const fn (?*anyopaque, *Group, token: *anyopaque) void,

    recancel: *const fn (?*anyopaque) void,
    swapCancelProtection: *const fn (?*anyopaque, new: CancelProtection) CancelProtection,
    checkCancel: *const fn (?*anyopaque) Cancelable!void,

    futexWait: *const fn (?*anyopaque, ptr: *const u32, expected: u32, Timeout) Cancelable!void,
    futexWaitUncancelable: *const fn (?*anyopaque, ptr: *const u32, expected: u32) void,
    futexWake: *const fn (?*anyopaque, ptr: *const u32, max_waiters: u32) void,

    operate: *const fn (?*anyopaque, Operation) Cancelable!Operation.Result,
    batchAwaitAsync: *const fn (?*anyopaque, *Batch) Cancelable!void,
    batchAwaitConcurrent: *const fn (?*anyopaque, *Batch, Timeout) Batch.AwaitConcurrentError!void,
    batchCancel: *const fn (?*anyopaque, *Batch) void,

    dirCreateDir: *const fn (?*anyopaque, Dir, []const u8, Dir.Permissions) Dir.CreateDirError!void,
    dirCreateDirPath: *const fn (?*anyopaque, Dir, []const u8, Dir.Permissions) Dir.CreateDirPathError!Dir.CreatePathStatus,
    dirCreateDirPathOpen: *const fn (?*anyopaque, Dir, []const u8, Dir.Permissions, Dir.OpenOptions) Dir.CreateDirPathOpenError!Dir,
    dirOpenDir: *const fn (?*anyopaque, Dir, []const u8, Dir.OpenOptions) Dir.OpenError!Dir,
    dirStat: *const fn (?*anyopaque, Dir) Dir.StatError!Dir.Stat,
    dirStatFile: *const fn (?*anyopaque, Dir, []const u8, Dir.StatFileOptions) Dir.StatFileError!File.Stat,
    dirAccess: *const fn (?*anyopaque, Dir, []const u8, Dir.AccessOptions) Dir.AccessError!void,
    dirCreateFile: *const fn (?*anyopaque, Dir, []const u8, Dir.CreateFileOptions) File.OpenError!File,
    dirCreateFileAtomic: *const fn (?*anyopaque, Dir, []const u8, Dir.CreateFileAtomicOptions) Dir.CreateFileAtomicError!File.Atomic,
    dirOpenFile: *const fn (?*anyopaque, Dir, []const u8, Dir.OpenFileOptions) File.OpenError!File,
    dirClose: *const fn (?*anyopaque, []const Dir) void,
    dirRead: *const fn (?*anyopaque, *Dir.Reader, []Dir.Entry) Dir.Reader.Error!usize,
    dirRealPath: *const fn (?*anyopaque, Dir, out_buffer: []u8) Dir.RealPathError!usize,
    dirRealPathFile: *const fn (?*anyopaque, Dir, path_name: []const u8, out_buffer: []u8) Dir.RealPathFileError!usize,
    dirDeleteFile: *const fn (?*anyopaque, Dir, []const u8) Dir.DeleteFileError!void,
    dirDeleteDir: *const fn (?*anyopaque, Dir, []const u8) Dir.DeleteDirError!void,
    dirRename: *const fn (?*anyopaque, old_dir: Dir, old_sub_path: []const u8, new_dir: Dir, new_sub_path: []const u8) Dir.RenameError!void,
    dirRenamePreserve: *const fn (?*anyopaque, old_dir: Dir, old_sub_path: []const u8, new_dir: Dir, new_sub_path: []const u8) Dir.RenamePreserveError!void,
    dirSymLink: *const fn (?*anyopaque, Dir, target_path: []const u8, sym_link_path: []const u8, Dir.SymLinkFlags) Dir.SymLinkError!void,
    dirReadLink: *const fn (?*anyopaque, Dir, sub_path: []const u8, buffer: []u8) Dir.ReadLinkError!usize,
    dirSetOwner: *const fn (?*anyopaque, Dir, ?File.Uid, ?File.Gid) Dir.SetOwnerError!void,
    dirSetFileOwner: *const fn (?*anyopaque, Dir, []const u8, ?File.Uid, ?File.Gid, Dir.SetFileOwnerOptions) Dir.SetFileOwnerError!void,
    dirSetPermissions: *const fn (?*anyopaque, Dir, Dir.Permissions) Dir.SetPermissionsError!void,
    dirSetFilePermissions: *const fn (?*anyopaque, Dir, []const u8, File.Permissions, Dir.SetFilePermissionsOptions) Dir.SetFilePermissionsError!void,
    dirSetTimestamps: *const fn (?*anyopaque, Dir, []const u8, Dir.SetTimestampsOptions) Dir.SetTimestampsError!void,
    dirHardLink: *const fn (?*anyopaque, old_dir: Dir, old_sub_path: []const u8, new_dir: Dir, new_sub_path: []const u8, Dir.HardLinkOptions) Dir.HardLinkError!void,

    fileStat: *const fn (?*anyopaque, File) File.StatError!File.Stat,
    fileLength: *const fn (?*anyopaque, File) File.LengthError!u64,
    fileClose: *const fn (?*anyopaque, []const File) void,
    fileWritePositional: *const fn (?*anyopaque, File, header: []const u8, data: []const []const u8, splat: usize, offset: u64) File.WritePositionalError!usize,
    fileWriteFileStreaming: *const fn (?*anyopaque, File, header: []const u8, *Io.File.Reader, Io.Limit) File.Writer.WriteFileError!usize,
    fileWriteFilePositional: *const fn (?*anyopaque, File, header: []const u8, *Io.File.Reader, Io.Limit, offset: u64) File.WriteFilePositionalError!usize,
    /// Returns 0 if reading at or past the end.
    fileReadPositional: *const fn (?*anyopaque, File, data: []const []u8, offset: u64) File.ReadPositionalError!usize,
    fileSeekBy: *const fn (?*anyopaque, File, relative_offset: i64) File.SeekError!void,
    fileSeekTo: *const fn (?*anyopaque, File, absolute_offset: u64) File.SeekError!void,
    fileSync: *const fn (?*anyopaque, File) File.SyncError!void,
    fileIsTty: *const fn (?*anyopaque, File) Cancelable!bool,
    fileEnableAnsiEscapeCodes: *const fn (?*anyopaque, File) File.EnableAnsiEscapeCodesError!void,
    fileSupportsAnsiEscapeCodes: *const fn (?*anyopaque, File) Cancelable!bool,
    fileSetLength: *const fn (?*anyopaque, File, u64) File.SetLengthError!void,
    fileSetOwner: *const fn (?*anyopaque, File, ?File.Uid, ?File.Gid) File.SetOwnerError!void,
    fileSetPermissions: *const fn (?*anyopaque, File, File.Permissions) File.SetPermissionsError!void,
    fileSetTimestamps: *const fn (?*anyopaque, File, File.SetTimestampsOptions) File.SetTimestampsError!void,
    fileLock: *const fn (?*anyopaque, File, File.Lock) File.LockError!void,
    fileTryLock: *const fn (?*anyopaque, File, File.Lock) File.LockError!bool,
    fileUnlock: *const fn (?*anyopaque, File) void,
    fileDowngradeLock: *const fn (?*anyopaque, File) File.DowngradeLockError!void,
    fileRealPath: *const fn (?*anyopaque, File, out_buffer: []u8) File.RealPathError!usize,
    fileHardLink: *const fn (?*anyopaque, File, Dir, []const u8, File.HardLinkOptions) File.HardLinkError!void,

    fileMemoryMapCreate: *const fn (?*anyopaque, File, File.MemoryMap.CreateOptions) File.MemoryMap.CreateError!File.MemoryMap,
    fileMemoryMapDestroy: *const fn (?*anyopaque, *File.MemoryMap) void,
    fileMemoryMapSetLength: *const fn (?*anyopaque, *File.MemoryMap, usize) File.MemoryMap.SetLengthError!void,
    fileMemoryMapRead: *const fn (?*anyopaque, *File.MemoryMap) File.ReadPositionalError!void,
    fileMemoryMapWrite: *const fn (?*anyopaque, *File.MemoryMap) File.WritePositionalError!void,

    processExecutableOpen: *const fn (?*anyopaque, Dir.OpenFileOptions) std.process.OpenExecutableError!File,
    processExecutablePath: *const fn (?*anyopaque, buffer: []u8) std.process.ExecutablePathError!usize,
    lockStderr: *const fn (?*anyopaque, ?Terminal.Mode) Cancelable!LockedStderr,
    tryLockStderr: *const fn (?*anyopaque, ?Terminal.Mode) Cancelable!?LockedStderr,
    unlockStderr: *const fn (?*anyopaque) void,
    processCurrentPath: *const fn (?*anyopaque, buffer: []u8) std.process.CurrentPathError!usize,
    processSetCurrentDir: *const fn (?*anyopaque, Dir) std.process.SetCurrentDirError!void,
    processSetCurrentPath: *const fn (?*anyopaque, []const u8) std.process.SetCurrentPathError!void,
    processReplace: *const fn (?*anyopaque, std.process.ReplaceOptions) std.process.ReplaceError,
    processReplacePath: *const fn (?*anyopaque, Dir, std.process.ReplaceOptions) std.process.ReplaceError,
    processSpawn: *const fn (?*anyopaque, std.process.SpawnOptions) std.process.SpawnError!std.process.Child,
    processSpawnPath: *const fn (?*anyopaque, Dir, std.process.SpawnOptions) std.process.SpawnError!std.process.Child,
    childWait: *const fn (?*anyopaque, *std.process.Child) std.process.Child.WaitError!std.process.Child.Term,
    childKill: *const fn (?*anyopaque, *std.process.Child) void,

    progressParentFile: *const fn (?*anyopaque) std.Progress.ParentFileError!File,

    now: *const fn (?*anyopaque, Clock) Timestamp,
    clockResolution: *const fn (?*anyopaque, Clock) Clock.ResolutionError!Duration,
    sleep: *const fn (?*anyopaque, Timeout) Cancelable!void,

    random: *const fn (?*anyopaque, buffer: []u8) void,
    randomSecure: *const fn (?*anyopaque, buffer: []u8) RandomSecureError!void,

    netListenIp: *const fn (?*anyopaque, address: *const net.IpAddress, net.IpAddress.ListenOptions) net.IpAddress.ListenError!net.Socket,
    netAccept: *const fn (?*anyopaque, server: net.Socket.Handle, options: net.Server.AcceptOptions) net.Server.AcceptError!net.Socket,
    netBindIp: *const fn (?*anyopaque, address: *const net.IpAddress, options: net.IpAddress.BindOptions) net.IpAddress.BindError!net.Socket,
    netConnectIp: *const fn (?*anyopaque, address: *const net.IpAddress, options: net.IpAddress.ConnectOptions) net.IpAddress.ConnectError!net.Socket,
    netListenUnix: *const fn (?*anyopaque, *const net.UnixAddress, net.UnixAddress.ListenOptions) net.UnixAddress.ListenError!net.Socket.Handle,
    netConnectUnix: *const fn (?*anyopaque, *const net.UnixAddress) net.UnixAddress.ConnectError!net.Socket.Handle,
    netSocketCreatePair: *const fn (?*anyopaque, net.Socket.CreatePairOptions) net.Socket.CreatePairError![2]net.Socket,
    netSend: *const fn (?*anyopaque, net.Socket.Handle, []net.OutgoingMessage, net.SendFlags) struct { ?net.Socket.SendError, usize },
    /// Returns 0 on end of stream.
    netRead: *const fn (?*anyopaque, src: net.Socket.Handle, data: [][]u8) net.Stream.Reader.Error!usize,
    netWrite: *const fn (?*anyopaque, dest: net.Socket.Handle, header: []const u8, data: []const []const u8, splat: usize) net.Stream.Writer.Error!usize,
    netWriteFile: *const fn (?*anyopaque, net.Socket.Handle, header: []const u8, *Io.File.Reader, Io.Limit) net.Stream.Writer.WriteFileError!usize,
    netClose: *const fn (?*anyopaque, handle: []const net.Socket.Handle) void,
    netShutdown: *const fn (?*anyopaque, handle: net.Socket.Handle, how: net.ShutdownHow) net.ShutdownError!void,
    netInterfaceNameResolve: *const fn (?*anyopaque, *const net.Interface.Name) net.Interface.Name.ResolveError!net.Interface,
    netInterfaceName: *const fn (?*anyopaque, net.Interface) net.Interface.NameError!net.Interface.Name,
    netLookup: *const fn (?*anyopaque, net.HostName, *Queue(net.HostName.LookupResult), net.HostName.LookupOptions) net.HostName.LookupError!void,
};

pub const Operation = union(enum) {
    file_read_streaming: FileReadStreaming,
    file_write_streaming: FileWriteStreaming,
    /// On Windows this is NtDeviceIoControlFile. On POSIX this is ioctl. On
    /// other systems this tag is unreachable.
    device_io_control: DeviceIoControl,
    net_receive: NetReceive,

    pub const Tag = @typeInfo(Operation).@"union".tag_type.?;

    /// May return 0 reads which is different than `error.EndOfStream`.
    pub const FileReadStreaming = struct {
        file: File,
        data: []const []u8,

        pub const Error = UnendingError || error{EndOfStream};
        pub const UnendingError = error{
            InputOutput,
            SystemResources,
            /// Trying to read a directory file descriptor as if it were a file.
            IsDir,
            ConnectionResetByPeer,
            /// File was not opened with read capability.
            NotOpenForReading,
            SocketUnconnected,
            /// Non-blocking has been enabled, and reading from the file descriptor
            /// would block.
            WouldBlock,
            /// In WASI, this error occurs when the file descriptor does
            /// not hold the required rights to read from it.
            AccessDenied,
            /// Unable to read file due to lock. Depending on the `Io` implementation,
            /// reading from a locked file may return this error, or may ignore the
            /// lock.
            LockViolation,
        } || Io.UnexpectedError;

        pub const Result = Error!usize;
    };

    pub const FileWriteStreaming = struct {
        file: File,
        header: []const u8 = &.{},
        data: []const []const u8,
        splat: usize = 1,

        pub const Error = error{
            DiskQuota,
            FileTooBig,
            InputOutput,
            NoSpaceLeft,
            DeviceBusy,
            /// File descriptor does not hold the required rights to write to it.
            AccessDenied,
            PermissionDenied,
            /// File is an unconnected socket, or closed its read end.
            BrokenPipe,
            /// Insufficient kernel memory to read from in_fd.
            SystemResources,
            NotOpenForWriting,
            /// The process cannot access the file because another process has locked
            /// a portion of the file. Windows-only.
            LockViolation,
            /// Non-blocking has been enabled and this operation would block.
            WouldBlock,
            /// This error occurs when a device gets disconnected before or mid-flush
            /// while it's being written to - errno(6): No such device or address.
            NoDevice,
            FileBusy,
        } || Io.UnexpectedError;

        pub const Result = Error!usize;
    };

    pub const DeviceIoControl = switch (builtin.os.tag) {
        .wasi => noreturn,
        .windows => struct {
            file: File,
            code: std.os.windows.CTL_CODE,
            in: []const u8 = &.{},
            out: []u8 = &.{},

            pub const Result = std.os.windows.IO_STATUS_BLOCK;
        },
        else => struct {
            file: File,
            /// Device-dependent operation code.
            code: u32,
            arg: ?*anyopaque,

            /// Device and operation dependent result. Negative values are
            /// negative errno.
            pub const Result = i32;
        },
    };

    pub const NetReceive = struct {
        socket_handle: net.Socket.Handle,
        message_buffer: []net.IncomingMessage,
        data_buffer: []u8,
        flags: net.ReceiveFlags,

        pub const Error = error{
            /// Insufficient memory or other resource internal to the operating system.
            SystemResources,
            /// Per-process limit on the number of open file descriptors has been reached.
            ProcessFdQuotaExceeded,
            /// System-wide limit on the total number of open files has been reached.
            SystemFdQuotaExceeded,
            /// Local end has been shut down on a connection-oriented socket, or
            /// the socket was never connected.
            SocketUnconnected,
            /// The socket type requires that message be sent atomically, and the
            /// size of the message to be sent made this impossible. The message
            /// was not transmitted, or was partially transmitted.
            MessageOversize,
            /// Network connection was unexpectedly closed by sender.
            ConnectionResetByPeer,
            /// The local network interface used to reach the destination is offline.
            NetworkDown,
            /// A connectionless packet was previously sent successfully,
            /// however, it was not received because no service is operating at
            /// the destination port of the transport on the remote system.
            /// This caused an ICMP port unreachable packet to be returned to
            /// the OS where it was queued up to be reported at the next call
            /// to send or receive on the bound socket.
            PortUnreachable,
        } || Io.UnexpectedError;

        pub const Result = struct { ?net.Socket.ReceiveError, usize };
    };

    pub const Result = Result: {
        const operation_fields = @typeInfo(Operation).@"union".fields;
        var field_names: [operation_fields.len][]const u8 = undefined;
        var field_types: [operation_fields.len]type = undefined;
        for (operation_fields, &field_names, &field_types) |field, *field_name, *field_type| {
            field_name.* = field.name;
            field_type.* = if (field.type == noreturn) noreturn else field.type.Result;
        }
        break :Result @Union(.auto, Tag, &field_names, &field_types, &@splat(.{}));
    };

    pub const Storage = union {
        unused: List.DoubleNode,
        submission: Submission,
        pending: Pending,
        completion: Completion,

        pub const Submission = struct {
            node: List.SingleNode,
            operation: Operation,
        };

        pub const Pending = struct {
            node: List.DoubleNode,
            tag: Tag,
            userdata: Userdata align(@max(@alignOf(usize), 4)),

            pub const Userdata = [7]usize;
        };

        pub const Completion = struct {
            node: List.SingleNode,
            result: Result,
        };
    };

    pub const OptionalIndex = enum(u32) {
        none = std.math.maxInt(u32),
        _,

        pub fn fromIndex(i: usize) OptionalIndex {
            const oi: OptionalIndex = @enumFromInt(i);
            assert(oi != .none);
            return oi;
        }

        pub fn toIndex(oi: OptionalIndex) u32 {
            assert(oi != .none);
            return @intFromEnum(oi);
        }
    };
    pub const List = struct {
        head: OptionalIndex,
        tail: OptionalIndex,

        pub const empty: List = .{ .head = .none, .tail = .none };

        pub const SingleNode = struct { next: OptionalIndex };
        pub const DoubleNode = struct { prev: OptionalIndex, next: OptionalIndex };
    };
};

/// Performs one `Operation`.
pub fn operate(io: Io, operation: Operation) Cancelable!Operation.Result {
    return io.vtable.operate(io.userdata, operation);
}

pub const OperateTimeoutError = Cancelable || Timeout.Error || ConcurrentError;

/// Performs one `Operation` with provided `timeout`.
pub fn operateTimeout(io: Io, operation: Operation, timeout: Timeout) OperateTimeoutError!Operation.Result {
    var storage: [1]Operation.Storage = undefined;
    var batch: Batch = .init(&storage);
    batch.addAt(0, operation);
    try batch.awaitConcurrent(io, timeout);
    const completion = batch.next().?;
    assert(completion.index == 0);
    return completion.result;
}

/// Submits many operations together without waiting for all of them to
/// complete.
///
/// This is a low-level abstraction based on `Operation`. For a higher
/// level API that operates on `Future`, see `Select` and `Group`.
pub const Batch = struct {
    storage: []Operation.Storage,
    unused: Operation.List,
    submitted: Operation.List,
    pending: Operation.List,
    completed: Operation.List,
    userdata: ?*anyopaque align(@max(@alignOf(?*anyopaque), 4)),

    /// After calling this, it is safe to unconditionally defer a call to
    /// `cancel`. `storage` is a pre-allocated buffer of undefined memory that
    /// determines the maximum number of active operations that can be
    /// submitted via `add` and `addAt`.
    pub fn init(storage: []Operation.Storage) Batch {
        var prev: Operation.OptionalIndex = .none;
        for (storage, 0..) |*operation, index| {
            operation.* = .{ .unused = .{ .prev = prev, .next = .fromIndex(index + 1) } };
            prev = .fromIndex(index);
        }
        storage[storage.len - 1].unused.next = .none;
        return .{
            .storage = storage,
            .unused = .{
                .head = .fromIndex(0),
                .tail = .fromIndex(storage.len - 1),
            },
            .submitted = .empty,
            .pending = .empty,
            .completed = .empty,
            .userdata = null,
        };
    }

    /// Adds an operation to be performed at the next await call.
    /// Returns the index that will be returned by `next` after the operation completes.
    /// Asserts that no more than `storage.len` operations are active at a time.
    pub fn add(batch: *Batch, operation: Operation) u32 {
        const index = batch.unused.head.toIndex();
        batch.addAt(index, operation);
        return index;
    }

    /// Adds an operation to be performed at the next await call.
    /// After the operation completes, `next` will return `index`.
    /// Asserts that the operation at `index` is not active.
    pub fn addAt(batch: *Batch, index: u32, operation: Operation) void {
        const storage = &batch.storage[index];
        const unused = storage.unused;
        switch (unused.prev) {
            .none => batch.unused.head = unused.next,
            else => |prev_index| batch.storage[prev_index.toIndex()].unused.next = unused.next,
        }
        switch (unused.next) {
            .none => batch.unused.tail = unused.prev,
            else => |next_index| batch.storage[next_index.toIndex()].unused.prev = unused.prev,
        }

        switch (batch.submitted.tail) {
            .none => batch.submitted.head = .fromIndex(index),
            else => |tail_index| batch.storage[tail_index.toIndex()].submission.node.next = .fromIndex(index),
        }
        storage.* = .{ .submission = .{ .node = .{ .next = .none }, .operation = operation } };
        batch.submitted.tail = .fromIndex(index);
    }

    pub const Completion = struct {
        /// The element within the provided operation storage that completed.
        /// `addAt` can be used to re-arm the `Batch` using this `index`.
        index: u32,
        /// The return value of the operation.
        result: Operation.Result,
    };

    /// After calling `awaitAsync`, `awaitConcurrent`, or `cancel`, this
    /// function iterates over the completed operations.
    ///
    /// Each completion returned from this function dequeues from the `Batch`.
    /// It is not required to dequeue all completions before awaiting again.
    pub fn next(batch: *Batch) ?Completion {
        const index = batch.completed.head;
        if (index == .none) return null;
        const storage = &batch.storage[index.toIndex()];
        const completion = storage.completion;
        const next_index = completion.node.next;
        batch.completed.head = next_index;
        if (next_index == .none) batch.completed.tail = .none;

        const tail_index = batch.unused.tail;
        switch (tail_index) {
            .none => batch.unused.head = index,
            else => batch.storage[tail_index.toIndex()].unused.next = index,
        }
        storage.* = .{ .unused = .{ .prev = tail_index, .next = .none } };
        batch.unused.tail = index;
        return .{ .index = index.toIndex(), .result = completion.result };
    }

    /// Waits for at least one of the submitted operations to complete. After
    /// this function returns the completed operations can be iterated with
    /// `next`.
    ///
    /// This function provides opportunity for the implementation to introduce
    /// concurrency into the batched operations, but unlike `awaitConcurrent`,
    /// does not require it, and therefore cannot fail with
    /// `error.ConcurrencyUnavailable`.
    pub fn awaitAsync(batch: *Batch, io: Io) Cancelable!void {
        return io.vtable.batchAwaitAsync(io.userdata, batch);
    }

    pub const AwaitConcurrentError = ConcurrentError || Cancelable || Timeout.Error;

    /// Waits for at least one of the submitted operations to complete. After
    /// this function returns the completed operations can be iterated with
    /// `next`.
    ///
    /// Unlike `awaitAsync`, this function requires the implementation to
    /// perform the operations concurrently and therefore can fail with
    /// `error.ConcurrencyUnavailable`.
    pub fn awaitConcurrent(batch: *Batch, io: Io, timeout: Timeout) AwaitConcurrentError!void {
        return io.vtable.batchAwaitConcurrent(io.userdata, batch, timeout);
    }

    /// Requests all pending operations to be interrupted, then waits for all
    /// pending operations to complete. After this returns, the `Batch` is in a
    /// well-defined state, ready to be iterated with `next`. Successfully
    /// canceled operations will be absent from the iteration. Some operations
    /// may have successfully completed regardless of the cancel request and
    /// will appear in the iteration.
    pub fn cancel(batch: *Batch, io: Io) void {
        { // abort pending submissions
            var tail_index = batch.unused.tail;
            defer batch.unused.tail = tail_index;
            var index = batch.submitted.head;
            errdefer batch.submissions.head = index;
            while (index != .none) {
                const next_index = batch.storage[index.toIndex()].submission.node.next;
                switch (tail_index) {
                    .none => batch.unused.head = index,
                    else => batch.storage[tail_index.toIndex()].unused.next = index,
                }
                batch.storage[index.toIndex()] = .{ .unused = .{ .prev = tail_index, .next = .none } };
                tail_index = index;
                index = next_index;
            }
            batch.submitted = .{ .head = .none, .tail = .none };
        }
        io.vtable.batchCancel(io.userdata, batch);
        assert(batch.submitted.head == .none and batch.submitted.tail == .none);
        assert(batch.pending.head == .none and batch.pending.tail == .none);
        assert(batch.userdata == null); // that was the last chance to deallocate resources
    }
};

pub const Limit = enum(usize) {
    nothing = 0,
    unlimited = math.maxInt(usize),
    _,

    /// `math.maxInt(usize)` is interpreted to mean `.unlimited`.
    pub fn limited(n: usize) Limit {
        return @enumFromInt(n);
    }

    /// Any value grater than `math.maxInt(usize)` is interpreted to mean
    /// `.unlimited`.
    pub fn limited64(n: u64) Limit {
        return @enumFromInt(@min(n, math.maxInt(usize)));
    }

    pub fn countVec(data: []const []const u8) Limit {
        var total: usize = 0;
        for (data) |d| total += d.len;
        return .limited(total);
    }

    pub fn min(a: Limit, b: Limit) Limit {
        return @enumFromInt(@min(@intFromEnum(a), @intFromEnum(b)));
    }

    pub fn max(a: Limit, b: Limit) Limit {
        if (a == .unlimited or b == .unlimited) {
            return .unlimited;
        }

        return @enumFromInt(@max(@intFromEnum(a), @intFromEnum(b)));
    }

    pub fn minInt(l: Limit, n: usize) usize {
        return @min(n, @intFromEnum(l));
    }

    pub fn minInt64(l: Limit, n: u64) usize {
        return @min(n, @intFromEnum(l));
    }

    pub fn slice(l: Limit, s: []u8) []u8 {
        return s[0..l.minInt(s.len)];
    }

    pub fn sliceConst(l: Limit, s: []const u8) []const u8 {
        return s[0..l.minInt(s.len)];
    }

    pub fn toInt(l: Limit) ?usize {
        return switch (l) {
            else => @intFromEnum(l),
            .unlimited => null,
        };
    }

    /// Reduces a slice to account for the limit, leaving room for one extra
    /// byte above the limit, allowing for the use case of differentiating
    /// between end-of-stream and reaching the limit.
    pub fn slice1(l: Limit, non_empty_buffer: []u8) []u8 {
        assert(non_empty_buffer.len >= 1);
        return non_empty_buffer[0..@min(@intFromEnum(l) +| 1, non_empty_buffer.len)];
    }

    pub fn nonzero(l: Limit) bool {
        return l != .nothing;
    }

    /// Return a new limit reduced by `amount` or return `null` indicating
    /// limit would be exceeded.
    pub fn subtract(l: Limit, amount: usize) ?Limit {
        if (l == .unlimited) return .unlimited;
        if (amount > @intFromEnum(l)) return null;
        return @enumFromInt(@intFromEnum(l) - amount);
    }
};

pub const Cancelable = error{
    /// Caller has requested the async operation to stop.
    Canceled,
};

pub const UnexpectedError = error{
    /// The Operating System returned an undocumented error code.
    ///
    /// This error is in theory not possible, but it would be better
    /// to handle this error than to invoke undefined behavior.
    ///
    /// When this error code is observed, it usually means the Zig Standard
    /// Library needs a small patch to add the error code to the error set for
    /// the respective function.
    Unexpected,
};

pub const Clock = enum {
    /// A settable system-wide clock that measures real (i.e. wall-clock)
    /// time. This clock is affected by discontinuous jumps in the system
    /// time (e.g., if the system administrator manually changes the
    /// clock), and by frequency adjustments performed by NTP and similar
    /// applications.
    ///
    /// This clock normally counts the number of seconds since 1970-01-01
    /// 00:00:00 Coordinated Universal Time (UTC) except that it ignores
    /// leap seconds; near a leap second it is typically adjusted by NTP to
    /// stay roughly in sync with UTC.
    ///
    /// Timestamps returned by implementations of this clock represent time
    /// elapsed since 1970-01-01T00:00:00Z, the POSIX/Unix epoch, ignoring
    /// leap seconds. This is colloquially known as "Unix time". If the
    /// underlying OS uses a different epoch for native timestamps (e.g.,
    /// Windows, which uses 1601-01-01) they are translated accordingly.
    real,
    /// A nonsettable system-wide clock that represents time since some
    /// unspecified point in the past.
    ///
    /// Monotonic: Guarantees that the time returned by consecutive calls
    /// will not go backwards, but successive calls may return identical
    /// (not-increased) time values.
    ///
    /// Not affected by discontinuous jumps in the system time (e.g., if
    /// the system administrator manually changes the clock), but may be
    /// affected by frequency adjustments.
    ///
    /// This clock expresses intent to **exclude time that the system is
    /// suspended**. However, implementations may be unable to satisify
    /// this, and may include that time.
    ///
    /// * On Linux, corresponds `CLOCK_MONOTONIC`.
    /// * On macOS, corresponds to `CLOCK_UPTIME_RAW`.
    awake,
    /// Identical to `awake` except it expresses intent to **include time
    /// that the system is suspended**, however, due to limitations it may
    /// behave identically to `awake`.
    ///
    /// * On Linux, corresponds `CLOCK_BOOTTIME`.
    /// * On macOS, corresponds to `CLOCK_MONOTONIC_RAW`.
    boot,
    /// Tracks the amount of CPU in user or kernel mode used by the calling
    /// process.
    cpu_process,
    /// Tracks the amount of CPU in user or kernel mode used by the calling
    /// thread.
    cpu_thread,

    /// This function is not cancelable because it does not block.
    ///
    /// Resolution is determined by `resolution` which may be 0 if the
    /// clock is unsupported.
    ///
    /// See also:
    /// * `Clock.Timestamp.now`
    pub fn now(clock: Clock, io: Io) Io.Timestamp {
        return io.vtable.now(io.userdata, clock);
    }

    pub const ResolutionError = error{
        ClockUnavailable,
        Unexpected,
    };

    /// Reveals the granularity of `clock`. May be zero, indicating
    /// unsupported clock.
    pub fn resolution(clock: Clock, io: Io) ResolutionError!Io.Duration {
        return io.vtable.clockResolution(io.userdata, clock);
    }

    pub const Timestamp = struct {
        raw: Io.Timestamp,
        clock: Clock,

        /// This function is not cancelable because it does not block.
        ///
        /// Resolution is determined by `resolution` which may be 0 if
        /// the clock is unsupported.
        ///
        /// See also:
        /// * `Clock.now`
        pub fn now(io: Io, clock: Clock) Clock.Timestamp {
            return .{
                .raw = io.vtable.now(io.userdata, clock),
                .clock = clock,
            };
        }

        /// Sleeps until the timestamp arrives.
        ///
        /// See also:
        /// * `Io.sleep`
        /// * `Clock.Duration.sleep`
        /// * `Timeout.sleep`
        pub fn wait(t: Clock.Timestamp, io: Io) Cancelable!void {
            return io.vtable.sleep(io.userdata, .{ .deadline = t });
        }

        pub fn durationTo(from: Clock.Timestamp, to: Clock.Timestamp) Clock.Duration {
            assert(from.clock == to.clock);
            return .{
                .raw = from.raw.durationTo(to.raw),
                .clock = from.clock,
            };
        }

        pub fn addDuration(from: Clock.Timestamp, duration: Clock.Duration) Clock.Timestamp {
            assert(from.clock == duration.clock);
            return .{
                .raw = from.raw.addDuration(duration.raw),
                .clock = from.clock,
            };
        }

        pub fn subDuration(from: Clock.Timestamp, duration: Clock.Duration) Clock.Timestamp {
            assert(from.clock == duration.clock);
            return .{
                .raw = from.raw.subDuration(duration.raw),
                .clock = from.clock,
            };
        }

        /// Resolution is determined by `resolution` which may be 0 if
        /// the clock is unsupported.
        pub fn fromNow(io: Io, duration: Clock.Duration) Clock.Timestamp {
            return .{
                .clock = duration.clock,
                .raw = duration.clock.now(io).addDuration(duration.raw),
            };
        }

        /// Resolution is determined by `resolution` which may be 0 if
        /// the clock is unsupported.
        pub fn untilNow(timestamp: Clock.Timestamp, io: Io) Clock.Duration {
            const now_ts = Clock.Timestamp.now(io, timestamp.clock);
            return timestamp.durationTo(now_ts);
        }

        /// Resolution is determined by `resolution` which may be 0 if
        /// the clock is unsupported.
        pub fn durationFromNow(timestamp: Clock.Timestamp, io: Io) Clock.Duration {
            const now_ts = timestamp.clock.now(io);
            return .{
                .clock = timestamp.clock,
                .raw = now_ts.durationTo(timestamp.raw),
            };
        }

        /// Resolution is determined by `resolution` which may be 0 if
        /// the clock is unsupported.
        pub fn toClock(t: Clock.Timestamp, io: Io, clock: Clock) Clock.Timestamp {
            if (t.clock == clock) return t;
            const now_old = t.clock.now(io);
            const now_new = clock.now(io);
            const duration = now_old.durationTo(t);
            return .{
                .clock = clock,
                .raw = now_new.addDuration(duration),
            };
        }

        pub fn compare(lhs: Clock.Timestamp, op: math.CompareOperator, rhs: Clock.Timestamp) bool {
            assert(lhs.clock == rhs.clock);
            return math.compare(lhs.raw.nanoseconds, op, rhs.raw.nanoseconds);
        }
    };

    pub const Duration = struct {
        raw: Io.Duration,
        clock: Clock,

        /// Waits until a specified amount of time has passed on `clock`.
        ///
        /// See also:
        /// * `Io.sleep`
        /// * `Clock.Timestamp.wait`
        /// * `Timeout.sleep`
        pub fn sleep(duration: Clock.Duration, io: Io) Cancelable!void {
            return io.vtable.sleep(io.userdata, .{ .duration = duration });
        }
    };
};

pub const Timestamp = struct {
    nanoseconds: i96,

    pub fn now(io: Io, clock: Clock) Io.Timestamp {
        return io.vtable.now(io.userdata, clock);
    }

    pub const zero: Timestamp = .{ .nanoseconds = 0 };

    pub fn durationTo(from: Timestamp, to: Timestamp) Duration {
        return .{ .nanoseconds = to.nanoseconds - from.nanoseconds };
    }

    pub fn addDuration(from: Timestamp, duration: Duration) Timestamp {
        return .{ .nanoseconds = from.nanoseconds + duration.nanoseconds };
    }

    pub fn subDuration(from: Timestamp, duration: Duration) Timestamp {
        return .{ .nanoseconds = from.nanoseconds - duration.nanoseconds };
    }

    pub fn withClock(t: Timestamp, clock: Clock) Clock.Timestamp {
        return .{ .raw = t, .clock = clock };
    }

    pub fn fromNanoseconds(x: i96) Timestamp {
        return .{ .nanoseconds = x };
    }

    pub fn toMicroseconds(t: Timestamp) i64 {
        return @intCast(@divTrunc(t.nanoseconds, std.time.ns_per_us));
    }

    pub fn toMilliseconds(t: Timestamp) i64 {
        return @intCast(@divTrunc(t.nanoseconds, std.time.ns_per_ms));
    }

    pub fn toSeconds(t: Timestamp) i64 {
        return @intCast(@divTrunc(t.nanoseconds, std.time.ns_per_s));
    }

    pub fn toNanoseconds(t: Timestamp) i96 {
        return t.nanoseconds;
    }

    pub fn formatNumber(t: Timestamp, w: *std.Io.Writer, n: std.fmt.Number) std.Io.Writer.Error!void {
        return w.printInt(t.nanoseconds, n.mode.base() orelse 10, n.case, .{
            .precision = n.precision,
            .width = n.width,
            .alignment = n.alignment,
            .fill = n.fill,
        });
    }

    /// Resolution is determined by `Clock.resolution` which may be 0 if
    /// the clock is unsupported.
    pub fn untilNow(t: Timestamp, io: Io, clock: Clock) Duration {
        const now_ts = clock.now(io);
        return t.durationTo(now_ts);
    }
};

pub const Duration = struct {
    nanoseconds: i96,

    pub const zero: Duration = .{ .nanoseconds = 0 };
    pub const max: Duration = .{ .nanoseconds = math.maxInt(i96) };

    pub fn fromNanoseconds(x: i96) Duration {
        return .{ .nanoseconds = x };
    }

    pub fn fromMicroseconds(x: i64) Duration {
        return .{ .nanoseconds = @as(i96, x) * std.time.ns_per_us };
    }

    pub fn fromMilliseconds(x: i64) Duration {
        return .{ .nanoseconds = @as(i96, x) * std.time.ns_per_ms };
    }

    pub fn fromSeconds(x: i64) Duration {
        return .{ .nanoseconds = @as(i96, x) * std.time.ns_per_s };
    }

    pub fn toMicroseconds(d: Duration) i64 {
        return @intCast(@divTrunc(d.nanoseconds, std.time.ns_per_us));
    }

    pub fn toMilliseconds(d: Duration) i64 {
        return @intCast(@divTrunc(d.nanoseconds, std.time.ns_per_ms));
    }

    pub fn toSeconds(d: Duration) i64 {
        return @intCast(@divTrunc(d.nanoseconds, std.time.ns_per_s));
    }

    pub fn toNanoseconds(d: Duration) i96 {
        return d.nanoseconds;
    }

    /// Write number of nanoseconds according to its signed magnitude:
    /// `[#y][#w][#d][#h][#m]#[.###][n|u|m]s`
    pub fn format(duration: Duration, w: *Writer) Writer.Error!void {
        if (duration.nanoseconds < 0) try w.writeByte('-');
        return formatUnsigned(w, @abs(duration.nanoseconds));
    }

    fn formatUnsigned(w: *Writer, ns: u96) Writer.Error!void {
        var ns_remaining = ns;
        inline for (.{
            .{ .ns = 365 * std.time.ns_per_day, .sep = 'y' },
            .{ .ns = std.time.ns_per_week, .sep = 'w' },
            .{ .ns = std.time.ns_per_day, .sep = 'd' },
            .{ .ns = std.time.ns_per_hour, .sep = 'h' },
            .{ .ns = std.time.ns_per_min, .sep = 'm' },
        }) |unit| {
            if (ns_remaining >= unit.ns) {
                const units = ns_remaining / unit.ns;
                try w.printInt(units, 10, .lower, .{});
                try w.writeByte(unit.sep);
                ns_remaining -= units * unit.ns;
                if (ns_remaining == 0) return;
            }
        }

        inline for (.{
            .{ .ns = std.time.ns_per_s, .sep = "s" },
            .{ .ns = std.time.ns_per_ms, .sep = "ms" },
            .{ .ns = std.time.ns_per_us, .sep = "us" },
        }) |unit| {
            const kunits = ns_remaining * 1000 / unit.ns;
            if (kunits >= 1000) {
                try w.printInt(kunits / 1000, 10, .lower, .{});
                const frac = kunits % 1000;
                if (frac > 0) {
                    // Write up to 3 decimal places
                    var decimal_buf = [_]u8{ '.', 0, 0, 0 };
                    var inner: Writer = .fixed(decimal_buf[1..]);
                    inner.printInt(frac, 10, .lower, .{ .fill = '0', .width = 3 }) catch unreachable;
                    var end: usize = 4;
                    while (end > 1) : (end -= 1) {
                        if (decimal_buf[end - 1] != '0') break;
                    }
                    try w.writeAll(decimal_buf[0..end]);
                }
                return w.writeAll(unit.sep);
            }
        }

        try w.printInt(ns_remaining, 10, .lower, .{});
        try w.writeAll("ns");
    }

    test format {
        try testFormat("0ns", 0);
        try testFormat("1ns", 1);
        try testFormat("-1ns", -(1));
        try testFormat("999ns", std.time.ns_per_us - 1);
        try testFormat("-999ns", -(std.time.ns_per_us - 1));
        try testFormat("1us", std.time.ns_per_us);
        try testFormat("-1us", -(std.time.ns_per_us));
        try testFormat("1.45us", 1450);
        try testFormat("-1.45us", -(1450));
        try testFormat("1.5us", 3 * std.time.ns_per_us / 2);
        try testFormat("-1.5us", -(3 * std.time.ns_per_us / 2));
        try testFormat("14.5us", 14500);
        try testFormat("-14.5us", -(14500));
        try testFormat("145us", 145000);
        try testFormat("-145us", -(145000));
        try testFormat("999.999us", std.time.ns_per_ms - 1);
        try testFormat("-999.999us", -(std.time.ns_per_ms - 1));
        try testFormat("1ms", std.time.ns_per_ms + 1);
        try testFormat("-1ms", -(std.time.ns_per_ms + 1));
        try testFormat("1.5ms", 3 * std.time.ns_per_ms / 2);
        try testFormat("-1.5ms", -(3 * std.time.ns_per_ms / 2));
        try testFormat("1.11ms", 1110000);
        try testFormat("-1.11ms", -(1110000));
        try testFormat("1.111ms", 1111000);
        try testFormat("-1.111ms", -(1111000));
        try testFormat("1.111ms", 1111100);
        try testFormat("-1.111ms", -(1111100));
        try testFormat("999.999ms", std.time.ns_per_s - 1);
        try testFormat("-999.999ms", -(std.time.ns_per_s - 1));
        try testFormat("1s", std.time.ns_per_s);
        try testFormat("-1s", -(std.time.ns_per_s));
        try testFormat("59.999s", std.time.ns_per_min - 1);
        try testFormat("-59.999s", -(std.time.ns_per_min - 1));
        try testFormat("1m", std.time.ns_per_min);
        try testFormat("-1m", -(std.time.ns_per_min));
        try testFormat("1h", std.time.ns_per_hour);
        try testFormat("-1h", -(std.time.ns_per_hour));
        try testFormat("1d", std.time.ns_per_day);
        try testFormat("-1d", -(std.time.ns_per_day));
        try testFormat("1w", std.time.ns_per_week);
        try testFormat("-1w", -(std.time.ns_per_week));
        try testFormat("1y", 365 * std.time.ns_per_day);
        try testFormat("-1y", -(365 * std.time.ns_per_day));
        try testFormat("1y52w23h59m59.999s", 730 * std.time.ns_per_day - 1); // 365d = 52w1d
        try testFormat("-1y52w23h59m59.999s", -(730 * std.time.ns_per_day - 1)); // 365d = 52w1d
        try testFormat("1y1h1.001s", 365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_s + std.time.ns_per_ms);
        try testFormat("-1y1h1.001s", -(365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_s + std.time.ns_per_ms));
        try testFormat("1y1h1s", 365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_s + 999 * std.time.ns_per_us);
        try testFormat("-1y1h1s", -(365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_s + 999 * std.time.ns_per_us));
        try testFormat("1y1h999.999us", 365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_ms - 1);
        try testFormat("-1y1h999.999us", -(365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_ms - 1));
        try testFormat("1y1h1ms", 365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_ms);
        try testFormat("-1y1h1ms", -(365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_ms));
        try testFormat("1y1h1ms", 365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_ms + 1);
        try testFormat("-1y1h1ms", -(365 * std.time.ns_per_day + std.time.ns_per_hour + std.time.ns_per_ms + 1));
        try testFormat("1y1m999ns", 365 * std.time.ns_per_day + std.time.ns_per_min + 999);
        try testFormat("-1y1m999ns", -(365 * std.time.ns_per_day + std.time.ns_per_min + 999));
        try testFormat("292y24w3d23h47m16.854s", std.math.maxInt(i64));
        try testFormat("-292y24w3d23h47m16.854s", std.math.minInt(i64) + 1);
        try testFormat("-292y24w3d23h47m16.854s", std.math.minInt(i64));
    }

    fn testFormat(expected: []const u8, input: i96) !void {
        // worst case: "-XXXXXXXXXXXXXyXXwXXdXXhXXmXX.XXXs".len = 34
        var buf: [34]u8 = undefined;
        var w: Writer = .fixed(&buf);
        try w.print("{f}", .{Duration{ .nanoseconds = input }});
        try std.testing.expectEqualStrings(expected, w.buffered());
    }
};

/// Declares under what conditions an operation should return `error.Timeout`.
pub const Timeout = union(enum) {
    none,
    duration: Clock.Duration,
    deadline: Clock.Timestamp,

    pub const Error = error{Timeout};

    pub fn toTimestamp(t: Timeout, io: Io) ?Clock.Timestamp {
        return switch (t) {
            .none => null,
            .duration => |d| .fromNow(io, d),
            .deadline => |d| d,
        };
    }

    pub fn toDeadline(t: Timeout, io: Io) Timeout {
        return switch (t) {
            .none => .none,
            .duration => |d| .{ .deadline = .fromNow(io, d) },
            .deadline => |d| .{ .deadline = d },
        };
    }

    pub fn toDurationFromNow(t: Timeout, io: Io) ?Clock.Duration {
        return switch (t) {
            .none => null,
            .duration => |d| d,
            .deadline => |d| d.durationFromNow(io),
        };
    }

    /// Waits until the timeout has passed.
    ///
    /// See also:
    /// * `Io.sleep`
    /// * `Clock.Duration.sleep`
    /// * `Clock.Timestamp.wait`
    pub fn sleep(timeout: Timeout, io: Io) Cancelable!void {
        return io.vtable.sleep(io.userdata, timeout);
    }
};

pub const AnyFuture = opaque {};

pub fn Future(Result: type) type {
    return struct {
        any_future: ?*AnyFuture,
        result: Result,

        /// Equivalent to `await` but places a cancelation request. This causes the task to receive
        /// `error.Canceled` from its next "cancelation point" (if any). A cancelation point is a
        /// call to a function in `Io` which can return `error.Canceled`.
        ///
        /// After cancelation of a task is requested, only the next cancelation point in that task
        /// will return `error.Canceled`: future points will not re-signal the cancelation. As such,
        /// it is usually a bug to ignore `error.Canceled`. However, to defer handling cancelation
        /// requests, see also `recancel` and `CancelProtection`.
        ///
        /// Idempotent. Not threadsafe.
        pub fn cancel(f: *@This(), io: Io) Result {
            const any_future = f.any_future orelse return f.result;
            io.vtable.cancel(io.userdata, any_future, @ptrCast(&f.result), .of(Result));
            f.any_future = null;
            return f.result;
        }

        /// Idempotent. Not threadsafe.
        pub fn await(f: *@This(), io: Io) Result {
            const any_future = f.any_future orelse return f.result;
            io.vtable.await(io.userdata, any_future, @ptrCast(&f.result), .of(Result));
            f.any_future = null;
            return f.result;
        }
    };
}

/// An unordered set of tasks which can only be awaited or canceled as a whole.
/// Tasks are spawned in the group with `Group.async` and `Group.concurrent`.
///
/// The resources associated with each task are *guaranteed* to be released when
/// the individual task returns, as opposed to when the whole group completes or
/// is awaited. For this reason, it is not a resource leak to have a long-lived
/// group which concurrent tasks are repeatedly added to. However, asynchronous
/// tasks are not guaranteed to run until `Group.await` or `Group.cancel` is
/// called, so adding async tasks to a group without ever awaiting it may leak
/// resources.
pub const Group = struct {
    /// This value indicates whether or not a group has pending tasks. `null`
    /// means there are no pending tasks, and no resources associated with the
    /// group, so `await` and `cancel` return immediately without calling the
    /// implementation. This means that `token` must be accessed atomically to
    /// avoid racing with the check in `await` and `cancel`.
    token: std.atomic.Value(?*anyopaque),
    /// This value is available for the implementation to use as it wishes.
    state: usize,

    pub const init: Group = .{ .token = .init(null), .state = 0 };

    /// Equivalent to `Io.async`, except the task is spawned in this `Group`
    /// instead of becoming associated with a `Future`.
    ///
    /// The return type of `function` must be coercible to `Cancelable!void`.
    /// `function` returning `error.Canceled` does nothing because it is an
    /// cancelation propagation boundary.
    ///
    /// Once this function is called, there are resources associated with the
    /// group. To release those resources, `Group.await` or `Group.cancel` must
    /// eventually be called.
    pub fn async(g: *Group, io: Io, function: anytype, args: std.meta.ArgsTuple(@TypeOf(function))) void {
        const Args = @TypeOf(args);
        const TypeErased = struct {
            fn start(context: *const anyopaque) void {
                const args_casted: *const Args = @ptrCast(@alignCast(context));
                _ = @as(Cancelable!void, @call(.auto, function, args_casted.*)) catch {};
            }
        };
        io.vtable.groupAsync(io.userdata, g, @ptrCast(&args), .of(Args), TypeErased.start);
    }

    /// Equivalent to `Io.concurrent`, except the task is spawned in this
    /// `Group` instead of becoming associated with a `Future`.
    ///
    /// The return type of `function` must be coercible to `Cancelable!void`.
    /// `function` returning `error.Canceled` does nothing because it is an
    /// cancelation propagation boundary.
    ///
    /// Once this function is called, there are resources associated with the
    /// group. To release those resources, `Group.await` or `Group.cancel` must
    /// eventually be called.
    pub fn concurrent(g: *Group, io: Io, function: anytype, args: std.meta.ArgsTuple(@TypeOf(function))) ConcurrentError!void {
        const Args = @TypeOf(args);
        const TypeErased = struct {
            fn start(context: *const anyopaque) void {
                const args_casted: *const Args = @ptrCast(@alignCast(context));
                _ = @as(Cancelable!void, @call(.auto, function, args_casted.*)) catch {};
            }
        };
        return io.vtable.groupConcurrent(io.userdata, g, @ptrCast(&args), .of(Args), TypeErased.start);
    }

    /// Blocks until all tasks of the group finish. During this time,
    /// cancelation requests propagate to all members of the group, and
    /// will also cause `error.Canceled` to be returned when the group
    /// does ultimately finish.
    ///
    /// Idempotent. Not threadsafe.
    ///
    /// It is safe to call this function concurrently with `Group.async` or
    /// `Group.concurrent`, provided that the group does not complete until
    /// the call to `Group.async` or `Group.concurrent` returns.
    pub fn await(g: *Group, io: Io) Cancelable!void {
        const token = g.token.load(.acquire) orelse return;
        try io.vtable.groupAwait(io.userdata, g, token);
        assert(g.token.raw == null);
    }

    /// Equivalent to `await` but immediately requests cancelation on all
    /// members of the group.
    ///
    /// For a description of cancelation and cancelation points, see `Future.cancel`.
    ///
    /// Idempotent. Not threadsafe.
    ///
    /// It is safe to call this function concurrently with `Group.async` or
    /// `Group.concurrent`, provided that the group does not complete until
    /// the call to `Group.async` or `Group.concurrent` returns.
    pub fn cancel(g: *Group, io: Io) void {
        const token = g.token.load(.acquire) orelse return;
        io.vtable.groupCancel(io.userdata, g, token);
        assert(g.token.raw == null);
    }
};

/// Asserts that `error.Canceled` was returned from a prior cancelation point, and "re-arms" the
/// cancelation request, so that `error.Canceled` will be returned again from the next cancelation
/// point.
///
/// For a description of cancelation and cancelation points, see `Future.cancel`.
pub fn recancel(io: Io) void {
    io.vtable.recancel(io.userdata);
}

/// In rare cases, it is desirable to completely block cancelation notification, so that a region
/// of code can run uninterrupted before `error.Canceled` is potentially observed. Therefore, every
/// task has a "cancel protection" state which indicates whether or not `Io` functions can introduce
/// cancelation points.
///
/// To modify a task's cancel protection state, see `swapCancelProtection`.
///
/// For a description of cancelation and cancelation points, see `Future.cancel`.
pub const CancelProtection = enum(u1) {
    /// Any call to an `Io` function with `error.Canceled` in its error set is a cancelation point.
    ///
    /// This is the default state, which all tasks are created in.
    unblocked = 0,
    /// No `Io` function introduces a cancelation point (`error.Canceled` will never be returned).
    blocked = 1,
};
/// Updates the current task's cancel protection state (see `CancelProtection`).
///
/// The typical usage for this function is to protect a block of code from cancelation:
/// ```
/// const old_cancel_protect = io.swapCancelProtection(.blocked);
/// defer _ = io.swapCancelProtection(old_cancel_protect);
/// doSomeWork() catch |err| switch (err) {
///     error.Canceled => unreachable,
/// };
/// ```
///
/// For a description of cancelation and cancelation points, see `Future.cancel`.
pub fn swapCancelProtection(io: Io, new: CancelProtection) CancelProtection {
    return io.vtable.swapCancelProtection(io.userdata, new);
}

/// This function acts as a pure cancelation point (subject to protection; see `CancelProtection`)
/// and does nothing else. In other words, it returns `error.Canceled` if there is an outstanding
/// non-blocked cancelation request, but otherwise is a no-op.
///
/// It is rarely necessary to call this function. The primary use case is in long-running CPU-bound
/// tasks which may need to respond to cancelation before completing. Short tasks, or those which
/// perform other `Io` operations (and hence have other cancelation points), will typically already
/// respond quickly to cancelation requests.
///
/// For a description of cancelation and cancelation points, see `Future.cancel`.
pub fn checkCancel(io: Io) Cancelable!void {
    return io.vtable.checkCancel(io.userdata);
}

/// Executes tasks together, providing a mechanism to wait until one or more
/// tasks complete. Similar to `Batch` but operates at the higher level task
/// abstraction layer rather than lower level `Operation` abstraction layer.
///
/// The provided tagged union will be used as the return type of the await
/// function. When calling async or concurrent, one specifies which union field
/// the called function's result will be placed into upon completion.
pub fn Select(comptime U: type) type {
    return struct {
        io: Io,
        group: Group,
        queue: Queue(U),

        const S = @This();

        pub const Union = U;

        pub const Field = std.meta.FieldEnum(U);

        pub fn init(io: Io, buffer: []U) S {
            return .{
                .io = io,
                .queue = .init(buffer),
                .group = .init,
            };
        }

        /// Calls `function` with `args` asynchronously. The resource spawned is
        /// owned by the select.
        ///
        /// `function` must have return type matching the `field` field of `Union`.
        ///
        /// `function` *may* be called immediately, before `async` returns.
        ///
        /// When this function returns, it is guaranteed that `function` has
        /// already been called and completed, or it has successfully been
        /// assigned a unit of concurrency.
        ///
        /// After this is called, `await` or `cancel` must be called before the
        /// select is deinitialized.
        ///
        /// Threadsafe.
        ///
        /// Related:
        /// * `Io.async`
        /// * `Group.async`
        pub fn async(
            s: *S,
            comptime field: Field,
            function: anytype,
            args: std.meta.ArgsTuple(@TypeOf(function)),
        ) void {
            const Context = struct {
                select: *S,
                args: @TypeOf(args),
                fn start(type_erased_context: *const anyopaque) void {
                    const context: *const @This() = @ptrCast(@alignCast(type_erased_context));
                    const result = @call(.auto, function, context.args);
                    const elem = @unionInit(U, @tagName(field), result);
                    context.select.queue.putOneUncancelable(context.select.io, elem) catch |err| switch (err) {
                        error.Closed => {},
                    };
                }
            };
            const context: Context = .{ .select = s, .args = args };
            s.io.vtable.groupAsync(s.io.userdata, &s.group, @ptrCast(&context), .of(Context), Context.start);
        }

        /// Calls `function` with `args` concurrently. The resource spawned is
        /// owned by the select.
        ///
        /// `function` must have return type matching the `field` field of `Union`.
        ///
        /// After this function returns successfully, it is guaranteed that
        /// `function` has been assigned a unit of concurrency, and `await` or
        /// `cancel` must be called before the select is deinitialized.
        ///
        ///
        /// Threadsafe.
        ///
        /// Related:
        /// * `Io.concurrent`
        /// * `Group.concurrent`
        pub fn concurrent(
            s: *S,
            comptime field: Field,
            function: anytype,
            args: std.meta.ArgsTuple(@TypeOf(function)),
        ) ConcurrentError!void {
            const Context = struct {
                select: *S,
                args: @TypeOf(args),
                fn start(type_erased_context: *const anyopaque) void {
                    const context: *const @This() = @ptrCast(@alignCast(type_erased_context));
                    const result = @call(.auto, function, context.args);
                    const elem = @unionInit(U, @tagName(field), result);
                    context.select.queue.putOneUncancelable(context.select.io, elem) catch |err| switch (err) {
                        error.Closed => {},
                    };
                }
            };
            const context: Context = .{ .select = s, .args = args };
            try s.io.vtable.groupConcurrent(s.io.userdata, &s.group, @ptrCast(&context), .of(Context), Context.start);
        }

        /// Blocks until another task of the select finishes.
        ///
        /// It is legal to call `async` and `concurrent` after this.
        ///
        /// Threadsafe.
        pub fn await(s: *S) Cancelable!U {
            return s.queue.getOne(s.io) catch |err| switch (err) {
                error.Canceled => |e| return e,
                error.Closed => unreachable,
            };
        }

        /// Blocks until at least `min` number of results have been copied
        /// into `buffer`.
        ///
        /// Asserts that `buffer.len >= min`.
        ///
        /// It is legal to call `async` and `concurrent` after this.
        ///
        /// Threadsafe.
        pub fn awaitMany(s: *S, buffer: []U, min: usize) Cancelable!usize {
            return s.queue.get(s.io, buffer, min) catch |err| switch (err) {
                error.Canceled => |e| return e,
                error.Closed => unreachable,
            };
        }

        /// Requests cancelation on all remaining tasks owned by the select,
        /// then blocks until they all finish. If the select was initialized
        /// with insufficient buffer space for all remaining tasks to finish, a
        /// deadlock occurs.
        ///
        /// If any of the select tasks allocate resources, those tasks may have
        /// completed, meaning that this function must be called in a loop
        /// until `null` is returned in order to deallocate those resources. If
        /// there is no possibility of resource leaks, `cancelDiscard` is
        /// preferable.
        ///
        /// It is illegal to call `await` or `awaitMany` after this.
        ///
        /// It is safe to call this multiple times, even after `null` is
        /// returned.
        ///
        /// Threadsafe.
        pub fn cancel(s: *S) ?U {
            const io = s.io;
            s.group.cancel(io);
            s.queue.close(io);
            return s.queue.getOneUncancelable(io) catch |err| switch (err) {
                error.Closed => return null,
            };
        }

        /// Requests cancelation on all remaining tasks owned by the select,
        /// then blocks until they all finish.
        ///
        /// All return values from outstanding tasks are discarded. This
        /// function is therefore inappropriate to call when a tas
```
