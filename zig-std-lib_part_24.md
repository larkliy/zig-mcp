```
              if (!hsd.eof()) {
                            try hsd.ensure(2);
                            const extensions_size = hsd.decode(u16);
                            var all_extd = try hsd.sub(extensions_size);
                            while (!all_extd.eof()) {
                                try all_extd.ensure(2 + 2);
                                const et = all_extd.decode(tls.ExtensionType);
                                const ext_size = all_extd.decode(u16);
                                var extd = try all_extd.sub(ext_size);
                                switch (et) {
                                    .supported_versions => {
                                        if (supported_version) |_| return error.TlsIllegalParameter;
                                        try extd.ensure(2);
                                        supported_version = extd.decode(u16);
                                    },
                                    .key_share => {
                                        if (key_share.getSharedSecret()) |_| return error.TlsIllegalParameter;
                                        try extd.ensure(4);
                                        const named_group = extd.decode(tls.NamedGroup);
                                        const key_size = extd.decode(u16);
                                        try extd.ensure(key_size);
                                        try key_share.exchange(named_group, extd.slice(key_size));
                                    },
                                    else => {},
                                }
                            }
                        }

                        tls_version = @enumFromInt(supported_version orelse legacy_version);
                        switch (tls_version) {
                            .tls_1_3 => if (!mem.eql(u8, legacy_session_id_echo, &legacy_session_id)) return error.TlsIllegalParameter,
                            .tls_1_2 => if (mem.eql(u8, server_hello_rand[24..31], "DOWNGRD") and
                                server_hello_rand[31] >> 1 == 0x00) return error.TlsIllegalParameter,
                            else => return error.TlsIllegalParameter,
                        }

                        switch (cipher_suite_tag) {
                            inline .AES_128_GCM_SHA256,
                            .AES_256_GCM_SHA384,
                            .CHACHA20_POLY1305_SHA256,
                            .AEGIS_256_SHA512,
                            .AEGIS_128L_SHA256,

                            .ECDHE_RSA_WITH_AES_128_GCM_SHA256,
                            .ECDHE_RSA_WITH_AES_256_GCM_SHA384,
                            .ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
                            => |tag| {
                                handshake_cipher = @unionInit(tls.HandshakeCipher, @tagName(tag.with()), .{
                                    .transcript_hash = .init(.{}),
                                    .version = undefined,
                                });
                                const p = &@field(handshake_cipher, @tagName(tag.with()));
                                p.transcript_hash.update(cleartext_header[tls.record_header_len..]); // Client Hello part 1
                                p.transcript_hash.update(host); // Client Hello part 2
                                p.transcript_hash.update(wrapped_handshake);
                            },

                            else => return error.TlsIllegalParameter,
                        }
                        switch (tls_version) {
                            .tls_1_3 => {
                                switch (cipher_suite_tag) {
                                    inline .AES_128_GCM_SHA256,
                                    .AES_256_GCM_SHA384,
                                    .CHACHA20_POLY1305_SHA256,
                                    .AEGIS_256_SHA512,
                                    .AEGIS_128L_SHA256,
                                    => |tag| {
                                        const sk = key_share.getSharedSecret() orelse return error.TlsIllegalParameter;
                                        const p = &@field(handshake_cipher, @tagName(tag.with()));
                                        const P = @TypeOf(p.*).A;
                                        const hello_hash = p.transcript_hash.peek();
                                        const zeroes = [1]u8{0} ** P.Hash.digest_length;
                                        const early_secret = P.Hkdf.extract(&[1]u8{0}, &zeroes);
                                        const empty_hash = tls.emptyHash(P.Hash);
                                        p.version = .{ .tls_1_3 = undefined };
                                        const pv = &p.version.tls_1_3;
                                        const hs_derived_secret = hkdfExpandLabel(P.Hkdf, early_secret, "derived", &empty_hash, P.Hash.digest_length);
                                        pv.handshake_secret = P.Hkdf.extract(&hs_derived_secret, sk);
                                        const ap_derived_secret = hkdfExpandLabel(P.Hkdf, pv.handshake_secret, "derived", &empty_hash, P.Hash.digest_length);
                                        pv.master_secret = P.Hkdf.extract(&ap_derived_secret, &zeroes);
                                        const client_secret = hkdfExpandLabel(P.Hkdf, pv.handshake_secret, "c hs traffic", &hello_hash, P.Hash.digest_length);
                                        const server_secret = hkdfExpandLabel(P.Hkdf, pv.handshake_secret, "s hs traffic", &hello_hash, P.Hash.digest_length);
                                        if (options.ssl_key_log) |key_log| logSecrets(key_log.writer, .{
                                            .client_random = &client_hello_rand,
                                        }, .{
                                            .SERVER_HANDSHAKE_TRAFFIC_SECRET = &server_secret,
                                            .CLIENT_HANDSHAKE_TRAFFIC_SECRET = &client_secret,
                                        });
                                        pv.client_finished_key = hkdfExpandLabel(P.Hkdf, client_secret, "finished", "", P.Hmac.key_length);
                                        pv.server_finished_key = hkdfExpandLabel(P.Hkdf, server_secret, "finished", "", P.Hmac.key_length);
                                        pv.client_handshake_key = hkdfExpandLabel(P.Hkdf, client_secret, "key", "", P.AEAD.key_length);
                                        pv.server_handshake_key = hkdfExpandLabel(P.Hkdf, server_secret, "key", "", P.AEAD.key_length);
                                        pv.client_handshake_iv = hkdfExpandLabel(P.Hkdf, client_secret, "iv", "", P.AEAD.nonce_length);
                                        pv.server_handshake_iv = hkdfExpandLabel(P.Hkdf, server_secret, "iv", "", P.AEAD.nonce_length);
                                    },
                                    else => return error.TlsIllegalParameter,
                                }
                                pending_cipher_state = .handshake;
                                handshake_state = .encrypted_extensions;
                            },
                            .tls_1_2 => switch (cipher_suite_tag) {
                                .ECDHE_RSA_WITH_AES_128_GCM_SHA256,
                                .ECDHE_RSA_WITH_AES_256_GCM_SHA384,
                                .ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
                                => handshake_state = .certificate,
                                else => return error.TlsIllegalParameter,
                            },
                            else => return error.TlsIllegalParameter,
                        }
                    },
                    .encrypted_extensions => {
                        if (tls_version != .tls_1_3) return error.TlsUnexpectedMessage;
                        if (cipher_state != .handshake) return error.TlsUnexpectedMessage;
                        if (handshake_state != .encrypted_extensions) return error.TlsUnexpectedMessage;
                        switch (handshake_cipher) {
                            inline else => |*p| p.transcript_hash.update(wrapped_handshake),
                        }
                        try hsd.ensure(2);
                        const total_ext_size = hsd.decode(u16);
                        var all_extd = try hsd.sub(total_ext_size);
                        while (!all_extd.eof()) {
                            try all_extd.ensure(4);
                            const et = all_extd.decode(tls.ExtensionType);
                            const ext_size = all_extd.decode(u16);
                            const extd = try all_extd.sub(ext_size);
                            _ = extd;
                            switch (et) {
                                .server_name => {},
                                else => {},
                            }
                        }
                        handshake_state = .certificate;
                    },
                    .certificate => cert: {
                        if (cipher_state == .application) return error.TlsUnexpectedMessage;
                        switch (handshake_state) {
                            .certificate => {},
                            .trust_chain_established => break :cert,
                            else => return error.TlsUnexpectedMessage,
                        }
                        switch (handshake_cipher) {
                            inline else => |*p| p.transcript_hash.update(wrapped_handshake),
                        }

                        switch (tls_version) {
                            .tls_1_3 => {
                                try hsd.ensure(1 + 3);
                                const cert_req_ctx_len = hsd.decode(u8);
                                if (cert_req_ctx_len != 0) return error.TlsIllegalParameter;
                            },
                            .tls_1_2 => try hsd.ensure(3),
                            else => unreachable,
                        }
                        const certs_size = hsd.decode(u24);
                        const certs = try hsd.sub(certs_size);

                        var certs_decoder = certs;
                        while (!certs_decoder.eof()) {
                            try certs_decoder.ensure(3);
                            const cert_size = certs_decoder.decode(u24);
                            const certd = try certs_decoder.sub(cert_size);

                            if (tls_version == .tls_1_3) {
                                try certs_decoder.ensure(2);
                                const total_ext_size = certs_decoder.decode(u16);
                                const all_extd = try certs_decoder.sub(total_ext_size);
                                _ = all_extd;
                            }

                            const subject_cert: Certificate = .{
                                .buffer = certd.buf,
                                .index = @intCast(certd.idx),
                            };
                            const subject = try subject_cert.parse();
                            if (cert_index == 0) {
                                // Verify the host on the first certificate.
                                switch (options.host) {
                                    .no_verification => {},
                                    .explicit => try subject.verifyHostName(host),
                                }

                                // Keep track of the public key for the
                                // certificate_verify message later.
                                try main_cert_pub_key.init(subject.pub_key_algo, subject.pubKey());
                            } else {
                                try prev_cert.verify(subject, now_sec);
                            }

                            switch (options.ca) {
                                .no_verification => {
                                    handshake_state = .trust_chain_established;
                                    break :cert;
                                },
                                .self_signed => {
                                    try subject.verify(subject, now_sec);
                                    handshake_state = .trust_chain_established;
                                    break :cert;
                                },
                                .bundle => |ca| if (verify: {
                                    try ca.lock.lockShared(ca.io);
                                    defer ca.lock.unlockShared(ca.io);
                                    break :verify ca.bundle.verify(subject, now_sec);
                                }) {
                                    handshake_state = .trust_chain_established;
                                    break :cert;
                                } else |err| switch (err) {
                                    error.CertificateIssuerNotFound => {},
                                    else => |e| return e,
                                },
                            }

                            prev_cert = subject;
                            cert_index += 1;
                        }

                        if (Certificate.Chain != void) {
                            certs_decoder = certs;
                            while (!certs_decoder.eof()) {
                                try certs_decoder.ensure(3);
                                const cert_size = certs_decoder.decode(u24);
                                const certd = try certs_decoder.sub(cert_size);
                                chain.addCert(certd.rest()) catch |err| switch (err) {
                                    error.Unexpected => return error.TlsCertificateNotVerified,
                                };
                                if (tls_version == .tls_1_3) {
                                    try certs_decoder.ensure(2);
                                    const total_ext_size = certs_decoder.decode(u16);
                                    const all_extd = try certs_decoder.sub(total_ext_size);
                                    _ = all_extd;
                                }
                            }
                        }

                        cert_buf_index += 1;
                    },
                    .server_key_exchange => {
                        if (tls_version != .tls_1_2) return error.TlsUnexpectedMessage;
                        if (cipher_state != .cleartext) return error.TlsUnexpectedMessage;
                        switch (handshake_state) {
                            .trust_chain_established => {},
                            .certificate => try tryDownloadRootCert(&chain, &options),
                            else => return error.TlsUnexpectedMessage,
                        }

                        switch (handshake_cipher) {
                            inline else => |*p| p.transcript_hash.update(wrapped_handshake),
                        }
                        try hsd.ensure(1 + 2 + 1);
                        const curve_type = hsd.decode(u8);
                        if (curve_type != 0x03) return error.TlsIllegalParameter; // named_curve
                        const named_group = hsd.decode(tls.NamedGroup);
                        tls12_negotiated_group = named_group;
                        const key_size = hsd.decode(u8);
                        try hsd.ensure(key_size);
                        const server_pub_key = hsd.slice(key_size);
                        try main_cert_pub_key.verifySignature(&hsd, &.{ &client_hello_rand, &server_hello_rand, hsd.buf[0..hsd.idx] });
                        try key_share.exchange(named_group, server_pub_key);
                        handshake_state = .server_hello_done;
                    },
                    .server_hello_done => {
                        if (tls_version != .tls_1_2) return error.TlsUnexpectedMessage;
                        if (cipher_state != .cleartext) return error.TlsUnexpectedMessage;
                        if (handshake_state != .server_hello_done) return error.TlsUnexpectedMessage;

                        const public_key_bytes: []const u8 = switch (tls12_negotiated_group orelse .secp256r1) {
                            .secp256r1 => &key_share.secp256r1_kp.public_key.toUncompressedSec1(),
                            .secp384r1 => &key_share.secp384r1_kp.public_key.toUncompressedSec1(),
                            .x25519 => &key_share.x25519_kp.public_key,
                            else => return error.TlsIllegalParameter,
                        };

                        const client_key_exchange_prefix = .{@intFromEnum(tls.ContentType.handshake)} ++
                            int(u16, @intFromEnum(tls.ProtocolVersion.tls_1_2)) ++
                            int(u16, @intCast(public_key_bytes.len + 5)) ++ // record length
                            .{@intFromEnum(tls.HandshakeType.client_key_exchange)} ++
                            int(u24, @intCast(public_key_bytes.len + 1)) ++ // handshake message length
                            .{@as(u8, @intCast(public_key_bytes.len))}; // public key length
                        const client_change_cipher_spec_msg = .{@intFromEnum(tls.ContentType.change_cipher_spec)} ++
                            int(u16, @intFromEnum(tls.ProtocolVersion.tls_1_2)) ++
                            array(u16, tls.ChangeCipherSpecType, .{.change_cipher_spec});
                        const pre_master_secret = key_share.getSharedSecret().?;
                        switch (handshake_cipher) {
                            inline else => |*p| {
                                const P = @TypeOf(p.*).A;
                                p.transcript_hash.update(wrapped_handshake);
                                p.transcript_hash.update(client_key_exchange_prefix[tls.record_header_len..]);
                                p.transcript_hash.update(public_key_bytes);
                                const master_secret = hmacExpandLabel(P.Hmac, pre_master_secret, &.{
                                    "master secret",
                                    &client_hello_rand,
                                    &server_hello_rand,
                                }, 48);
                                if (options.ssl_key_log) |key_log| logSecrets(key_log.writer, .{
                                    .client_random = &client_hello_rand,
                                }, .{
                                    .CLIENT_RANDOM = &master_secret,
                                });
                                const key_block = hmacExpandLabel(
                                    P.Hmac,
                                    &master_secret,
                                    &.{ "key expansion", &server_hello_rand, &client_hello_rand },
                                    @sizeOf(P.Tls_1_2),
                                );
                                const client_verify_cleartext = .{@intFromEnum(tls.HandshakeType.finished)} ++
                                    array(u24, u8, hmacExpandLabel(
                                        P.Hmac,
                                        &master_secret,
                                        &.{ "client finished", &p.transcript_hash.peek() },
                                        P.verify_data_length,
                                    ));
                                p.transcript_hash.update(&client_verify_cleartext);
                                p.version = .{ .tls_1_2 = .{
                                    .expected_server_verify_data = hmacExpandLabel(
                                        P.Hmac,
                                        &master_secret,
                                        &.{ "server finished", &p.transcript_hash.finalResult() },
                                        P.verify_data_length,
                                    ),
                                    .app_cipher = mem.bytesToValue(P.Tls_1_2, &key_block),
                                } };
                                const pv = &p.version.tls_1_2;
                                const nonce: [P.AEAD.nonce_length]u8 = nonce: {
                                    const V = @Vector(P.AEAD.nonce_length, u8);
                                    const pad = [1]u8{0} ** (P.AEAD.nonce_length - 8);
                                    const operand: V = pad ++ @as([8]u8, @bitCast(big(write_seq)));
                                    break :nonce @as(V, pv.app_cipher.client_write_IV ++ pv.app_cipher.client_salt) ^ operand;
                                };
                                var client_verify_msg = .{@intFromEnum(tls.ContentType.handshake)} ++
                                    int(u16, @intFromEnum(tls.ProtocolVersion.tls_1_2)) ++
                                    array(u16, u8, nonce[P.fixed_iv_length..].* ++
                                        @as([client_verify_cleartext.len + P.mac_length]u8, undefined));
                                P.AEAD.encrypt(
                                    client_verify_msg[client_verify_msg.len - P.mac_length -
                                        client_verify_cleartext.len ..][0..client_verify_cleartext.len],
                                    client_verify_msg[client_verify_msg.len - P.mac_length ..][0..P.mac_length],
                                    &client_verify_cleartext,
                                    mem.toBytes(big(write_seq)) ++ client_verify_msg[0 .. 1 + 2] ++ int(u16, client_verify_cleartext.len),
                                    nonce,
                                    pv.app_cipher.client_write_key,
                                );
                                var all_msgs_vec: [4][]const u8 = .{
                                    &client_key_exchange_prefix,
                                    public_key_bytes,
                                    &client_change_cipher_spec_msg,
                                    &client_verify_msg,
                                };
                                try output.writeVecAll(&all_msgs_vec);
                                try output.flush();
                            },
                        }
                        write_seq += 1;
                        pending_cipher_state = .application;
                        handshake_state = .finished;
                    },
                    .certificate_verify => {
                        if (tls_version != .tls_1_3) return error.TlsUnexpectedMessage;
                        if (cipher_state != .handshake) return error.TlsUnexpectedMessage;
                        switch (handshake_state) {
                            .trust_chain_established => {},
                            .certificate => try tryDownloadRootCert(&chain, &options),
                            else => return error.TlsUnexpectedMessage,
                        }
                        switch (handshake_cipher) {
                            inline else => |*p| {
                                try main_cert_pub_key.verifySignature(&hsd, &.{
                                    " " ** 64 ++ "TLS 1.3, server CertificateVerify\x00",
                                    &p.transcript_hash.peek(),
                                });
                                p.transcript_hash.update(wrapped_handshake);
                            },
                        }
                        handshake_state = .finished;
                    },
                    .finished => {
                        if (cipher_state == .cleartext) return error.TlsUnexpectedMessage;
                        if (handshake_state != .finished) return error.TlsUnexpectedMessage;
                        // This message is to trick buggy proxies into behaving correctly.
                        const client_change_cipher_spec_msg = .{@intFromEnum(tls.ContentType.change_cipher_spec)} ++
                            int(u16, @intFromEnum(tls.ProtocolVersion.tls_1_2)) ++
                            array(u16, tls.ChangeCipherSpecType, .{.change_cipher_spec});
                        const app_cipher = app_cipher: switch (handshake_cipher) {
                            inline else => |*p, tag| switch (tls_version) {
                                .tls_1_3 => {
                                    const pv = &p.version.tls_1_3;
                                    const P = @TypeOf(p.*).A;
                                    try hsd.ensure(P.Hmac.mac_length);
                                    const finished_digest = p.transcript_hash.peek();
                                    p.transcript_hash.update(wrapped_handshake);
                                    const expected_server_verify_data = tls.hmac(P.Hmac, &finished_digest, pv.server_finished_key);
                                    if (!std.crypto.timing_safe.eql([P.Hmac.mac_length]u8, expected_server_verify_data, hsd.array(P.Hmac.mac_length).*)) return error.TlsDecryptError;
                                    const handshake_hash = p.transcript_hash.finalResult();
                                    const verify_data = tls.hmac(P.Hmac, &handshake_hash, pv.client_finished_key);
                                    const out_cleartext = .{@intFromEnum(tls.HandshakeType.finished)} ++
                                        array(u24, u8, verify_data) ++
                                        .{@intFromEnum(tls.ContentType.handshake)};

                                    const wrapped_len = out_cleartext.len + P.AEAD.tag_length;

                                    var finished_msg = .{@intFromEnum(tls.ContentType.application_data)} ++
                                        int(u16, @intFromEnum(tls.ProtocolVersion.tls_1_2)) ++
                                        array(u16, u8, @as([wrapped_len]u8, undefined));

                                    const ad = finished_msg[0..tls.record_header_len];
                                    const ciphertext = finished_msg[tls.record_header_len..][0..out_cleartext.len];
                                    const auth_tag = finished_msg[finished_msg.len - P.AEAD.tag_length ..];
                                    const nonce = pv.client_handshake_iv;
                                    P.AEAD.encrypt(ciphertext, auth_tag, &out_cleartext, ad, nonce, pv.client_handshake_key);

                                    var all_msgs_vec: [2][]const u8 = .{
                                        &client_change_cipher_spec_msg,
                                        &finished_msg,
                                    };
                                    try output.writeVecAll(&all_msgs_vec);
                                    try output.flush();

                                    const client_secret = hkdfExpandLabel(P.Hkdf, pv.master_secret, "c ap traffic", &handshake_hash, P.Hash.digest_length);
                                    const server_secret = hkdfExpandLabel(P.Hkdf, pv.master_secret, "s ap traffic", &handshake_hash, P.Hash.digest_length);
                                    if (options.ssl_key_log) |key_log| logSecrets(key_log.writer, .{
                                        .counter = key_seq,
                                        .client_random = &client_hello_rand,
                                    }, .{
                                        .SERVER_TRAFFIC_SECRET = &server_secret,
                                        .CLIENT_TRAFFIC_SECRET = &client_secret,
                                    });
                                    key_seq += 1;
                                    break :app_cipher @unionInit(tls.ApplicationCipher, @tagName(tag), .{ .tls_1_3 = .{
                                        .client_secret = client_secret,
                                        .server_secret = server_secret,
                                        .client_key = hkdfExpandLabel(P.Hkdf, client_secret, "key", "", P.AEAD.key_length),
                                        .server_key = hkdfExpandLabel(P.Hkdf, server_secret, "key", "", P.AEAD.key_length),
                                        .client_iv = hkdfExpandLabel(P.Hkdf, client_secret, "iv", "", P.AEAD.nonce_length),
                                        .server_iv = hkdfExpandLabel(P.Hkdf, server_secret, "iv", "", P.AEAD.nonce_length),
                                    } });
                                },
                                .tls_1_2 => {
                                    const pv = &p.version.tls_1_2;
                                    const P = @TypeOf(p.*).A;
                                    try hsd.ensure(P.verify_data_length);
                                    if (!std.crypto.timing_safe.eql([P.verify_data_length]u8, pv.expected_server_verify_data, hsd.array(P.verify_data_length).*)) return error.TlsDecryptError;
                                    break :app_cipher @unionInit(tls.ApplicationCipher, @tagName(tag), .{ .tls_1_2 = pv.app_cipher });
                                },
                                else => unreachable,
                            },
                        };
                        if (options.ssl_key_log) |ssl_key_log| ssl_key_log.* = .{
                            .client_key_seq = key_seq,
                            .server_key_seq = key_seq,
                            .client_random = client_hello_rand,
                            .writer = ssl_key_log.writer,
                        };
                        return .{
                            .input = input,
                            .reader = .{
                                .buffer = options.read_buffer,
                                .vtable = &.{
                                    .stream = stream,
                                    .readVec = readVec,
                                },
                                .seek = 0,
                                .end = 0,
                            },
                            .output = output,
                            .writer = .{
                                .buffer = options.write_buffer,
                                .vtable = &.{
                                    .drain = drain,
                                    .flush = flush,
                                },
                            },
                            .tls_version = tls_version,
                            .read_seq = switch (tls_version) {
                                .tls_1_3 => 0,
                                .tls_1_2 => read_seq,
                                else => unreachable,
                            },
                            .write_seq = switch (tls_version) {
                                .tls_1_3 => 0,
                                .tls_1_2 => write_seq,
                                else => unreachable,
                            },
                            .received_close_notify = false,
                            .allow_truncation_attacks = options.allow_truncation_attacks,
                            .application_cipher = app_cipher,
                            .ssl_key_log = options.ssl_key_log,
                        };
                    },
                    else => return error.TlsUnexpectedMessage,
                }
                if (ctd.eof()) break;
                cleartext_fragment_start = ctd.idx;
            },
            else => return error.TlsUnexpectedMessage,
        }
        cleartext_fragment_start = 0;
        cleartext_fragment_end = 0;
    }
}

fn drain(w: *Writer, data: []const []const u8, splat: usize) Writer.Error!usize {
    const c: *Client = @alignCast(@fieldParentPtr("writer", w));
    const output = c.output;
    const ciphertext_buf = try output.writableSliceGreedy(min_buffer_len);
    var ciphertext_end: usize = 0;
    var total_clear: usize = 0;
    done: {
        {
            const buf = w.buffered();
            const prepared = prepareCiphertextRecord(c, ciphertext_buf[ciphertext_end..], buf, .application_data);
            total_clear += prepared.cleartext_len;
            ciphertext_end += prepared.ciphertext_end;
            if (prepared.cleartext_len < buf.len) break :done;
        }
        for (data[0 .. data.len - 1]) |buf| {
            const prepared = prepareCiphertextRecord(c, ciphertext_buf[ciphertext_end..], buf, .application_data);
            total_clear += prepared.cleartext_len;
            ciphertext_end += prepared.ciphertext_end;
            if (prepared.cleartext_len < buf.len) break :done;
        }
        const buf = data[data.len - 1];
        for (0..splat) |_| {
            const prepared = prepareCiphertextRecord(c, ciphertext_buf[ciphertext_end..], buf, .application_data);
            total_clear += prepared.cleartext_len;
            ciphertext_end += prepared.ciphertext_end;
            if (prepared.cleartext_len < buf.len) break :done;
        }
    }
    output.advance(ciphertext_end);
    return w.consume(total_clear);
}

fn flush(w: *Writer) Writer.Error!void {
    const c: *Client = @alignCast(@fieldParentPtr("writer", w));
    const output = c.output;
    const ciphertext_buf = try output.writableSliceGreedy(min_buffer_len);
    const prepared = prepareCiphertextRecord(c, ciphertext_buf, w.buffered(), .application_data);
    output.advance(prepared.ciphertext_end);
    w.end = 0;
}

/// Sends a `close_notify` alert, which is necessary for the server to
/// distinguish between a properly finished TLS session, or a truncation
/// attack.
pub fn end(c: *Client) Writer.Error!void {
    try flush(&c.writer);
    const output = c.output;
    const ciphertext_buf = try output.writableSliceGreedy(min_buffer_len);
    const prepared = prepareCiphertextRecord(c, ciphertext_buf, &tls.close_notify_alert, .alert);
    output.advance(prepared.ciphertext_end);
}

fn prepareCiphertextRecord(
    c: *Client,
    ciphertext_buf: []u8,
    bytes: []const u8,
    inner_content_type: tls.ContentType,
) struct {
    ciphertext_end: usize,
    cleartext_len: usize,
} {
    // Due to the trailing inner content type byte in the ciphertext, we need
    // an additional buffer for storing the cleartext into before encrypting.
    var cleartext_buf: [max_ciphertext_len]u8 = undefined;
    var ciphertext_end: usize = 0;
    var bytes_i: usize = 0;
    switch (c.application_cipher) {
        inline else => |*p| switch (c.tls_version) {
            .tls_1_3 => {
                const pv = &p.tls_1_3;
                const P = @TypeOf(p.*);
                const overhead_len = tls.record_header_len + P.AEAD.tag_length + 1;
                while (true) {
                    const encrypted_content_len: u16 = @min(
                        bytes.len - bytes_i,
                        tls.max_ciphertext_inner_record_len,
                        ciphertext_buf.len -| (overhead_len + ciphertext_end),
                    );
                    if (encrypted_content_len == 0) return .{
                        .ciphertext_end = ciphertext_end,
                        .cleartext_len = bytes_i,
                    };

                    @memcpy(cleartext_buf[0..encrypted_content_len], bytes[bytes_i..][0..encrypted_content_len]);
                    cleartext_buf[encrypted_content_len] = @intFromEnum(inner_content_type);
                    bytes_i += encrypted_content_len;
                    const ciphertext_len = encrypted_content_len + 1;
                    const cleartext = cleartext_buf[0..ciphertext_len];

                    const ad = ciphertext_buf[ciphertext_end..][0..tls.record_header_len];
                    ad.* = .{@intFromEnum(tls.ContentType.application_data)} ++
                        int(u16, @intFromEnum(tls.ProtocolVersion.tls_1_2)) ++
                        int(u16, ciphertext_len + P.AEAD.tag_length);
                    ciphertext_end += ad.len;
                    const ciphertext = ciphertext_buf[ciphertext_end..][0..ciphertext_len];
                    ciphertext_end += ciphertext_len;
                    const auth_tag = ciphertext_buf[ciphertext_end..][0..P.AEAD.tag_length];
                    ciphertext_end += auth_tag.len;
                    const nonce = nonce: {
                        const V = @Vector(P.AEAD.nonce_length, u8);
                        const pad = [1]u8{0} ** (P.AEAD.nonce_length - 8);
                        const operand: V = pad ++ mem.toBytes(big(c.write_seq));
                        break :nonce @as(V, pv.client_iv) ^ operand;
                    };
                    P.AEAD.encrypt(ciphertext, auth_tag, cleartext, ad, nonce, pv.client_key);
                    c.write_seq += 1; // TODO send key_update on overflow
                }
            },
            .tls_1_2 => {
                const pv = &p.tls_1_2;
                const P = @TypeOf(p.*);
                const overhead_len = tls.record_header_len + P.record_iv_length + P.mac_length;
                while (true) {
                    const message_len: u16 = @min(
                        bytes.len - bytes_i,
                        tls.max_ciphertext_inner_record_len,
                        ciphertext_buf.len -| (overhead_len + ciphertext_end),
                    );
                    if (message_len == 0) return .{
                        .ciphertext_end = ciphertext_end,
                        .cleartext_len = bytes_i,
                    };

                    @memcpy(cleartext_buf[0..message_len], bytes[bytes_i..][0..message_len]);
                    bytes_i += message_len;
                    const cleartext = cleartext_buf[0..message_len];

                    const record_header = ciphertext_buf[ciphertext_end..][0..tls.record_header_len];
                    ciphertext_end += tls.record_header_len;
                    record_header.* = .{@intFromEnum(inner_content_type)} ++
                        int(u16, @intFromEnum(tls.ProtocolVersion.tls_1_2)) ++
                        int(u16, P.record_iv_length + message_len + P.mac_length);
                    const ad = mem.toBytes(big(c.write_seq)) ++ record_header[0 .. 1 + 2] ++ int(u16, message_len);
                    const record_iv = ciphertext_buf[ciphertext_end..][0..P.record_iv_length];
                    ciphertext_end += P.record_iv_length;
                    const nonce: [P.AEAD.nonce_length]u8 = nonce: {
                        const V = @Vector(P.AEAD.nonce_length, u8);
                        const pad = [1]u8{0} ** (P.AEAD.nonce_length - 8);
                        const operand: V = pad ++ @as([8]u8, @bitCast(big(c.write_seq)));
                        break :nonce @as(V, pv.client_write_IV ++ pv.client_salt) ^ operand;
                    };
                    record_iv.* = nonce[P.fixed_iv_length..].*;
                    const ciphertext = ciphertext_buf[ciphertext_end..][0..message_len];
                    ciphertext_end += message_len;
                    const auth_tag = ciphertext_buf[ciphertext_end..][0..P.mac_length];
                    ciphertext_end += P.mac_length;
                    P.AEAD.encrypt(ciphertext, auth_tag, cleartext, ad, nonce, pv.client_write_key);
                    c.write_seq += 1; // TODO send key_update on overflow
                }
            },
            else => unreachable,
        },
    }
}

pub fn eof(c: Client) bool {
    return c.received_close_notify;
}

fn stream(r: *Reader, w: *Writer, limit: std.Io.Limit) Reader.StreamError!usize {
    // This function writes exclusively to the buffer.
    _ = w;
    _ = limit;
    const c: *Client = @alignCast(@fieldParentPtr("reader", r));
    return readIndirect(c);
}

fn readVec(r: *Reader, data: [][]u8) Reader.Error!usize {
    // This function writes exclusively to the buffer.
    _ = data;
    const c: *Client = @alignCast(@fieldParentPtr("reader", r));
    return readIndirect(c);
}

fn readIndirect(c: *Client) Reader.Error!usize {
    const r = &c.reader;
    if (c.eof()) return error.EndOfStream;
    const input = c.input;
    // If at least one full encrypted record is not buffered, read once.
    const record_header = input.peek(tls.record_header_len) catch |err| switch (err) {
        error.EndOfStream => {
            // This is either a truncation attack, a bug in the server, or an
            // intentional omission of the close_notify message due to truncation
            // detection handled above the TLS layer.
            if (c.allow_truncation_attacks) {
                c.received_close_notify = true;
                return error.EndOfStream;
            } else {
                return failRead(c, error.TlsConnectionTruncated);
            }
        },
        error.ReadFailed => return error.ReadFailed,
    };
    const ct: tls.ContentType = @enumFromInt(record_header[0]);
    const legacy_version = mem.readInt(u16, record_header[1..][0..2], .big);
    _ = legacy_version;
    const record_len = mem.readInt(u16, record_header[3..][0..2], .big);
    if (record_len > max_ciphertext_len) return failRead(c, error.TlsRecordOverflow);
    const record_end = 5 + record_len;
    if (record_end > input.buffered().len) {
        input.fillMore() catch |err| switch (err) {
            error.EndOfStream => return failRead(c, error.TlsConnectionTruncated),
            error.ReadFailed => return error.ReadFailed,
        };
        if (record_end > input.buffered().len) return 0;
    }

    const cleartext_len, const inner_ct: tls.ContentType = cleartext: switch (c.application_cipher) {
        inline else => |*p| switch (c.tls_version) {
            .tls_1_3 => {
                const pv = &p.tls_1_3;
                const P = @TypeOf(p.*);
                const ad = input.take(tls.record_header_len) catch unreachable; // already peeked
                const ciphertext_len = record_len - P.AEAD.tag_length;
                const ciphertext = input.take(ciphertext_len) catch unreachable; // already peeked
                const auth_tag = (input.takeArray(P.AEAD.tag_length) catch unreachable).*; // already peeked
                const nonce = nonce: {
                    const V = @Vector(P.AEAD.nonce_length, u8);
                    const pad = [1]u8{0} ** (P.AEAD.nonce_length - 8);
                    const operand: V = pad ++ mem.toBytes(big(c.read_seq));
                    break :nonce @as(V, pv.server_iv) ^ operand;
                };
                rebase(r, ciphertext.len);
                const cleartext = r.buffer[r.end..][0..ciphertext.len];
                P.AEAD.decrypt(cleartext, ciphertext, auth_tag, ad, nonce, pv.server_key) catch
                    return failRead(c, error.TlsBadRecordMac);
                // TODO use scalar, non-slice version
                const msg = mem.trimEnd(u8, cleartext, "\x00");
                break :cleartext .{ msg.len - 1, @enumFromInt(msg[msg.len - 1]) };
            },
            .tls_1_2 => {
                const pv = &p.tls_1_2;
                const P = @TypeOf(p.*);
                const message_len: u16 = record_len - P.record_iv_length - P.mac_length;
                const ad_header = input.take(tls.record_header_len) catch unreachable; // already peeked
                const ad = mem.toBytes(big(c.read_seq)) ++
                    ad_header[0 .. 1 + 2] ++
                    mem.toBytes(big(message_len));
                const record_iv = (input.takeArray(P.record_iv_length) catch unreachable).*; // already peeked
                const masked_read_seq = c.read_seq &
                    comptime std.math.shl(u64, std.math.maxInt(u64), 8 * P.record_iv_length);
                const nonce: [P.AEAD.nonce_length]u8 = nonce: {
                    const V = @Vector(P.AEAD.nonce_length, u8);
                    const pad = [1]u8{0} ** (P.AEAD.nonce_length - 8);
                    const operand: V = pad ++ @as([8]u8, @bitCast(big(masked_read_seq)));
                    break :nonce @as(V, pv.server_write_IV ++ record_iv) ^ operand;
                };
                const ciphertext = input.take(message_len) catch unreachable; // already peeked
                const auth_tag = (input.takeArray(P.mac_length) catch unreachable).*; // already peeked
                rebase(r, ciphertext.len);
                const cleartext = r.buffer[r.end..][0..ciphertext.len];
                P.AEAD.decrypt(cleartext, ciphertext, auth_tag, ad, nonce, pv.server_write_key) catch
                    return failRead(c, error.TlsBadRecordMac);
                break :cleartext .{ cleartext.len, ct };
            },
            else => unreachable,
        },
    };
    const cleartext = r.buffer[r.end..][0..cleartext_len];
    c.read_seq = std.math.add(u64, c.read_seq, 1) catch return failRead(c, error.TlsSequenceOverflow);
    switch (inner_ct) {
        .alert => {
            if (cleartext.len != 2) return failRead(c, error.TlsDecodeError);
            const alert: tls.Alert = .{
                .level = @enumFromInt(cleartext[0]),
                .description = @enumFromInt(cleartext[1]),
            };
            switch (alert.description) {
                .close_notify => {
                    c.received_close_notify = true;
                    return 0;
                },
                .user_canceled => {
                    // TODO: handle server-side closures
                    return failRead(c, error.TlsUnexpectedMessage);
                },
                else => {
                    c.alert = alert;
                    return failRead(c, error.TlsAlert);
                },
            }
        },
        .handshake => {
            var ct_i: usize = 0;
            while (true) {
                const handshake_type: tls.HandshakeType = @enumFromInt(cleartext[ct_i]);
                ct_i += 1;
                const handshake_len = mem.readInt(u24, cleartext[ct_i..][0..3], .big);
                ct_i += 3;
                const next_handshake_i = ct_i + handshake_len;
                if (next_handshake_i > cleartext.len) return failRead(c, error.TlsBadLength);
                const handshake = cleartext[ct_i..next_handshake_i];
                switch (handshake_type) {
                    .new_session_ticket => {
                        // This client implementation ignores new session tickets.
                    },
                    .key_update => {
                        switch (c.application_cipher) {
                            inline else => |*p| {
                                const pv = &p.tls_1_3;
                                const P = @TypeOf(p.*);
                                const server_secret = hkdfExpandLabel(P.Hkdf, pv.server_secret, "traffic upd", "", P.Hash.digest_length);
                                if (c.ssl_key_log) |key_log| logSecrets(key_log.writer, .{
                                    .counter = key_log.serverCounter(),
                                    .client_random = &key_log.client_random,
                                }, .{
                                    .SERVER_TRAFFIC_SECRET = &server_secret,
                                });
                                pv.server_secret = server_secret;
                                pv.server_key = hkdfExpandLabel(P.Hkdf, server_secret, "key", "", P.AEAD.key_length);
                                pv.server_iv = hkdfExpandLabel(P.Hkdf, server_secret, "iv", "", P.AEAD.nonce_length);
                            },
                        }
                        c.read_seq = 0;

                        switch (@as(tls.KeyUpdateRequest, @enumFromInt(handshake[0]))) {
                            .update_requested => {
                                switch (c.application_cipher) {
                                    inline else => |*p| {
                                        const pv = &p.tls_1_3;
                                        const P = @TypeOf(p.*);
                                        const client_secret = hkdfExpandLabel(P.Hkdf, pv.client_secret, "traffic upd", "", P.Hash.digest_length);
                                        if (c.ssl_key_log) |key_log| logSecrets(key_log.writer, .{
                                            .counter = key_log.clientCounter(),
                                            .client_random = &key_log.client_random,
                                        }, .{
                                            .CLIENT_TRAFFIC_SECRET = &client_secret,
                                        });
                                        pv.client_secret = client_secret;
                                        pv.client_key = hkdfExpandLabel(P.Hkdf, client_secret, "key", "", P.AEAD.key_length);
                                        pv.client_iv = hkdfExpandLabel(P.Hkdf, client_secret, "iv", "", P.AEAD.nonce_length);
                                    },
                                }
                                c.write_seq = 0;
                            },
                            .update_not_requested => {},
                            _ => return failRead(c, error.TlsIllegalParameter),
                        }
                    },
                    else => return failRead(c, error.TlsUnexpectedMessage),
                }
                ct_i = next_handshake_i;
                if (ct_i >= cleartext.len) break;
            }
            return 0;
        },
        .application_data => {
            r.end += cleartext.len;
            return 0;
        },
        else => return failRead(c, error.TlsUnexpectedMessage),
    }
}

fn rebase(r: *Reader, capacity: usize) void {
    if (r.buffer.len - r.end >= capacity) return;
    const data = r.buffer[r.seek..r.end];
    @memmove(r.buffer[0..data.len], data);
    r.seek = 0;
    r.end = data.len;
    assert(r.buffer.len - r.end >= capacity);
}

fn failRead(c: *Client, err: ReadError) error{ReadFailed} {
    c.read_err = err;
    return error.ReadFailed;
}

fn logSecrets(w: *Writer, context: anytype, secrets: anytype) void {
    inline for (@typeInfo(@TypeOf(secrets)).@"struct".fields) |field| w.print("{s}" ++
        (if (@hasField(@TypeOf(context), "counter")) "_{d}" else "") ++ " {x} {x}\n", .{field.name} ++
        (if (@hasField(@TypeOf(context), "counter")) .{context.counter} else .{}) ++ .{
        context.client_random,
        @field(secrets, field.name),
    }) catch {};
}

fn big(x: anytype) @TypeOf(x) {
    return switch (native_endian) {
        .big => x,
        .little => @byteSwap(x),
    };
}

const KeyShare = struct {
    ml_kem768_kp: crypto.kem.ml_kem.MLKem768.KeyPair,
    secp256r1_kp: crypto.sign.ecdsa.EcdsaP256Sha256.KeyPair,
    secp384r1_kp: crypto.sign.ecdsa.EcdsaP384Sha384.KeyPair,
    x25519_kp: crypto.dh.X25519.KeyPair,
    sk_buf: [sk_max_len]u8,
    sk_len: std.math.IntFittingRange(0, sk_max_len),

    const sk_max_len = @max(
        crypto.dh.X25519.shared_length + crypto.kem.ml_kem.MLKem768.shared_length,
        crypto.ecc.P256.scalar.encoded_length,
        crypto.ecc.P384.scalar.encoded_length,
        crypto.dh.X25519.shared_length,
    );

    fn init(seed: *const [176]u8) error{IdentityElement}!KeyShare {
        return .{
            .ml_kem768_kp = try .generateDeterministic(seed[0..64].*),
            .secp256r1_kp = try .generateDeterministic(seed[64..96].*),
            .secp384r1_kp = try .generateDeterministic(seed[96..144].*),
            .x25519_kp = try .generateDeterministic(seed[144..176].*),
            .sk_buf = undefined,
            .sk_len = 0,
        };
    }

    fn exchange(
        ks: *KeyShare,
        named_group: tls.NamedGroup,
        server_pub_key: []const u8,
    ) error{ TlsIllegalParameter, TlsDecryptFailure }!void {
        switch (named_group) {
            .x25519_ml_kem768 => {
                const hksl = crypto.kem.ml_kem.MLKem768.ciphertext_length;
                const xksl = hksl + crypto.dh.X25519.public_length;
                if (server_pub_key.len != xksl) return error.TlsIllegalParameter;

                const hsk = ks.ml_kem768_kp.secret_key.decaps(server_pub_key[0..hksl]) catch
                    return error.TlsDecryptFailure;
                const xsk = crypto.dh.X25519.scalarmult(ks.x25519_kp.secret_key, server_pub_key[hksl..xksl].*) catch
                    return error.TlsDecryptFailure;
                @memcpy(ks.sk_buf[0..hsk.len], &hsk);
                @memcpy(ks.sk_buf[hsk.len..][0..xsk.len], &xsk);
                ks.sk_len = hsk.len + xsk.len;
            },
            .secp256r1 => {
                const PublicKey = crypto.sign.ecdsa.EcdsaP256Sha256.PublicKey;
                const pk = PublicKey.fromSec1(server_pub_key) catch return error.TlsDecryptFailure;
                const mul = pk.p.mulPublic(ks.secp256r1_kp.secret_key.bytes, .big) catch
                    return error.TlsDecryptFailure;
                const sk = mul.affineCoordinates().x.toBytes(.big);
                @memcpy(ks.sk_buf[0..sk.len], &sk);
                ks.sk_len = sk.len;
            },
            .secp384r1 => {
                const PublicKey = crypto.sign.ecdsa.EcdsaP384Sha384.PublicKey;
                const pk = PublicKey.fromSec1(server_pub_key) catch return error.TlsDecryptFailure;
                const mul = pk.p.mulPublic(ks.secp384r1_kp.secret_key.bytes, .big) catch
                    return error.TlsDecryptFailure;
                const sk = mul.affineCoordinates().x.toBytes(.big);
                @memcpy(ks.sk_buf[0..sk.len], &sk);
                ks.sk_len = sk.len;
            },
            .x25519 => {
                const ksl = crypto.dh.X25519.public_length;
                if (server_pub_key.len != ksl) return error.TlsIllegalParameter;
                const sk = crypto.dh.X25519.scalarmult(ks.x25519_kp.secret_key, server_pub_key[0..ksl].*) catch
                    return error.TlsDecryptFailure;
                @memcpy(ks.sk_buf[0..sk.len], &sk);
                ks.sk_len = sk.len;
            },
            else => return error.TlsIllegalParameter,
        }
    }

    fn getSharedSecret(ks: *const KeyShare) ?[]const u8 {
        return if (ks.sk_len > 0) ks.sk_buf[0..ks.sk_len] else null;
    }
};

fn SchemeEcdsa(comptime scheme: tls.SignatureScheme) type {
    return switch (scheme) {
        .ecdsa_secp256r1_sha256 => crypto.sign.ecdsa.EcdsaP256Sha256,
        .ecdsa_secp384r1_sha384 => crypto.sign.ecdsa.EcdsaP384Sha384,
        else => @compileError("bad scheme"),
    };
}

fn SchemeRsa(comptime scheme: tls.SignatureScheme) type {
    return switch (scheme) {
        .rsa_pkcs1_sha256,
        .rsa_pkcs1_sha384,
        .rsa_pkcs1_sha512,
        .rsa_pkcs1_sha1,
        => Certificate.rsa.PKCS1v1_5Signature,
        .rsa_pss_rsae_sha256,
        .rsa_pss_rsae_sha384,
        .rsa_pss_rsae_sha512,
        .rsa_pss_pss_sha256,
        .rsa_pss_pss_sha384,
        .rsa_pss_pss_sha512,
        => Certificate.rsa.PSSSignature,
        else => @compileError("bad scheme"),
    };
}

fn SchemeEddsa(comptime scheme: tls.SignatureScheme) type {
    return switch (scheme) {
        .ed25519 => crypto.sign.Ed25519,
        else => @compileError("bad scheme"),
    };
}

fn SchemeHash(comptime scheme: tls.SignatureScheme) type {
    return switch (scheme) {
        .rsa_pkcs1_sha256,
        .ecdsa_secp256r1_sha256,
        .rsa_pss_rsae_sha256,
        .rsa_pss_pss_sha256,
        => crypto.hash.sha2.Sha256,
        .rsa_pkcs1_sha384,
        .ecdsa_secp384r1_sha384,
        .rsa_pss_rsae_sha384,
        .rsa_pss_pss_sha384,
        => crypto.hash.sha2.Sha384,
        .rsa_pkcs1_sha512,
        .ecdsa_secp521r1_sha512,
        .rsa_pss_rsae_sha512,
        .rsa_pss_pss_sha512,
        => crypto.hash.sha2.Sha512,
        .rsa_pkcs1_sha1,
        .ecdsa_sha1,
        => crypto.hash.Sha1,
        else => @compileError("bad scheme"),
    };
}

const CertificatePublicKey = struct {
    algo: Certificate.AlgorithmCategory,
    buf: [600]u8,
    len: u16,

    fn init(
        cert_pub_key: *CertificatePublicKey,
        algo: Certificate.AlgorithmCategory,
        pub_key: []const u8,
    ) error{CertificatePublicKeyInvalid}!void {
        if (pub_key.len > cert_pub_key.buf.len) return error.CertificatePublicKeyInvalid;
        cert_pub_key.algo = algo;
        @memcpy(cert_pub_key.buf[0..pub_key.len], pub_key);
        cert_pub_key.len = @intCast(pub_key.len);
    }

    const VerifyError = error{ TlsDecodeError, TlsBadSignatureScheme, InvalidEncoding } ||
        // ecdsa
        crypto.errors.EncodingError ||
        crypto.errors.NotSquareError ||
        crypto.errors.NonCanonicalError ||
        SchemeEcdsa(.ecdsa_secp256r1_sha256).Signature.VerifyError ||
        SchemeEcdsa(.ecdsa_secp384r1_sha384).Signature.VerifyError ||
        // rsa
        error{TlsBadRsaSignatureBitCount} ||
        Certificate.rsa.PublicKey.ParseDerError ||
        Certificate.rsa.PublicKey.FromBytesError ||
        Certificate.rsa.PSSSignature.VerifyError ||
        Certificate.rsa.PKCS1v1_5Signature.VerifyError ||
        // eddsa
        SchemeEddsa(.ed25519).Signature.VerifyError;

    fn verifySignature(
        cert_pub_key: *const CertificatePublicKey,
        sigd: *tls.Decoder,
        msg: []const []const u8,
    ) VerifyError!void {
        const pub_key = cert_pub_key.buf[0..cert_pub_key.len];

        try sigd.ensure(2 + 2);
        const scheme = sigd.decode(tls.SignatureScheme);
        const sig_len = sigd.decode(u16);
        try sigd.ensure(sig_len);
        const encoded_sig = sigd.slice(sig_len);

        if (cert_pub_key.algo != @as(Certificate.AlgorithmCategory, switch (scheme) {
            .ecdsa_secp256r1_sha256,
            .ecdsa_secp384r1_sha384,
            => .X9_62_id_ecPublicKey,
            .rsa_pkcs1_sha256,
            .rsa_pkcs1_sha384,
            .rsa_pkcs1_sha512,
            .rsa_pss_rsae_sha256,
            .rsa_pss_rsae_sha384,
            .rsa_pss_rsae_sha512,
            .rsa_pkcs1_sha1,
            => .rsaEncryption,
            .rsa_pss_pss_sha256,
            .rsa_pss_pss_sha384,
            .rsa_pss_pss_sha512,
            => .rsassa_pss,
            else => return error.TlsBadSignatureScheme,
        })) return error.TlsBadSignatureScheme;

        switch (scheme) {
            inline .ecdsa_secp256r1_sha256,
            .ecdsa_secp384r1_sha384,
            => |comptime_scheme| {
                const Ecdsa = SchemeEcdsa(comptime_scheme);
                const sig = try Ecdsa.Signature.fromDer(encoded_sig);
                const key = try Ecdsa.PublicKey.fromSec1(pub_key);
                var ver = try sig.verifier(key);
                for (msg) |part| ver.update(part);
                try ver.verify();
            },
            inline .rsa_pkcs1_sha256,
            .rsa_pkcs1_sha384,
            .rsa_pkcs1_sha512,
            .rsa_pss_rsae_sha256,
            .rsa_pss_rsae_sha384,
            .rsa_pss_rsae_sha512,
            .rsa_pss_pss_sha256,
            .rsa_pss_pss_sha384,
            .rsa_pss_pss_sha512,
            .rsa_pkcs1_sha1,
            => |comptime_scheme| {
                const RsaSignature = SchemeRsa(comptime_scheme);
                const Hash = SchemeHash(comptime_scheme);
                const PublicKey = Certificate.rsa.PublicKey;
                const components = try PublicKey.parseDer(pub_key);
                const exponent = components.exponent;
                const modulus = components.modulus;
                switch (modulus.len) {
                    inline 128, 256, 384, 512 => |modulus_len| {
                        const key: PublicKey = try .fromBytes(exponent, modulus);
                        const sig = RsaSignature.fromBytes(modulus_len, encoded_sig);
                        try RsaSignature.concatVerify(modulus_len, sig, msg, key, Hash);
                    },
                    else => return error.TlsBadRsaSignatureBitCount,
                }
            },
            inline .ed25519 => |comptime_scheme| {
                const Eddsa = SchemeEddsa(comptime_scheme);
                if (encoded_sig.len != Eddsa.Signature.encoded_length) return error.InvalidEncoding;
                const sig = Eddsa.Signature.fromBytes(encoded_sig[0..Eddsa.Signature.encoded_length].*);
                if (pub_key.len != Eddsa.PublicKey.encoded_length) return error.InvalidEncoding;
                const key = try Eddsa.PublicKey.fromBytes(pub_key[0..Eddsa.PublicKey.encoded_length].*);
                var ver = try sig.verifier(key);
                for (msg) |part| ver.update(part);
                try ver.verify();
            },
            else => unreachable,
        }
    }
};

fn tryDownloadRootCert(chain: *Certificate.Chain, options: *const Options) !void {
    if (Certificate.Chain != void) switch (options.ca) {
        else => {},
        .bundle => |ca| {
            chain.verify(options.realtime_now) catch |err| switch (err) {
                error.Unexpected => return error.TlsCertificateNotVerified,
                else => |e| return e,
            };
            var bundle: Certificate.Bundle = .empty;
            defer bundle.deinit(ca.gpa);
            if (bundle.rescan(ca.gpa, ca.io, options.realtime_now)) {
                try ca.lock.lock(ca.io);
                defer ca.lock.unlock(ca.io);
                std.mem.swap(Certificate.Bundle, ca.bundle, &bundle);
            } else |err| switch (err) {
                error.Canceled => |e| return e,
                else => {},
            }
            return; // the os has verified the certificate for us
        },
    };
    return error.TlsCertificateNotVerified;
}

/// The priority order here is chosen based on what crypto algorithms Zig has
/// available in the standard library as well as what is faster. Following are
/// a few data points on the relative performance of these algorithms.
///
/// Measurement taken with 0.11.0-dev.810+c2f5848fe
/// on x86_64-linux Intel(R) Core(TM) i9-9980HK CPU @ 2.40GHz:
/// zig run .lib/std/crypto/benchmark.zig -OReleaseFast
///       aegis-128l:      15382 MiB/s
///        aegis-256:       9553 MiB/s
///       aes128-gcm:       3721 MiB/s
///       aes256-gcm:       3010 MiB/s
/// chacha20Poly1305:        597 MiB/s
///
/// Measurement taken with 0.11.0-dev.810+c2f5848fe
/// on x86_64-linux Intel(R) Core(TM) i9-9980HK CPU @ 2.40GHz:
/// zig run .lib/std/crypto/benchmark.zig -OReleaseFast -mcpu=baseline
///       aegis-128l:        629 MiB/s
/// chacha20Poly1305:        529 MiB/s
///        aegis-256:        461 MiB/s
///       aes128-gcm:        138 MiB/s
///       aes256-gcm:        120 MiB/s
const cipher_suites = if (crypto.core.aes.has_hardware_support)
    array(u16, tls.CipherSuite, .{
        .AEGIS_128L_SHA256,
        .AEGIS_256_SHA512,
        .AES_128_GCM_SHA256,
        .ECDHE_RSA_WITH_AES_128_GCM_SHA256,
        .AES_256_GCM_SHA384,
        .ECDHE_RSA_WITH_AES_256_GCM_SHA384,
        .CHACHA20_POLY1305_SHA256,
        .ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
    })
else
    array(u16, tls.CipherSuite, .{
        .CHACHA20_POLY1305_SHA256,
        .ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
        .AEGIS_128L_SHA256,
        .AEGIS_256_SHA512,
        .AES_128_GCM_SHA256,
        .ECDHE_RSA_WITH_AES_128_GCM_SHA256,
        .AES_256_GCM_SHA384,
        .ECDHE_RSA_WITH_AES_256_GCM_SHA384,
    });



---
File: /std/crypto/aegis.zig
---

//! AEGIS is a very fast authenticated encryption system built on top of the core AES function.
//!
//! The AEGIS-128* variants have a 128 bit key and a 128 bit nonce.
//! The AEGIS-256* variants have a 256 bit key and a 256 bit nonce.
//! All of them can compute 128 and 256 bit authentication tags.
//!
//! The AEGIS cipher family offers performance that significantly exceeds that of AES-GCM with
//! hardware support for parallelizable AES block encryption.
//!
//! On high-end Intel CPUs with AVX-512 support, AEGIS-128X4 and AEGIS-256X4 are the fastest options.
//! On other modern server, desktop and mobile CPUs, AEGIS-128X2 and AEGIS-256X2 are usually the fastest options.
//! AEGIS-128L and AEGIS-256 perform well on a broad range of platforms, including WebAssembly.
//!
//! Unlike with AES-GCM, nonces can be safely chosen at random with no practical limit when using AEGIS-256*.
//! AEGIS-128* also allows for more messages to be safely encrypted when using random nonces.
//!
//! Unless the associated data can be fully controled by an adversary, AEGIS is believed to be key-committing,
//! making it a safer choice than most other AEADs when the key has low entropy, or can be controlled by an attacker.
//!
//! Finally, leaking the state does not leak the key.
//!
//! https://datatracker.ietf.org/doc/draft-irtf-cfrg-aegis-aead/

const std = @import("std");
const crypto = std.crypto;
const mem = std.mem;
const assert = std.debug.assert;
const AuthenticationError = crypto.errors.AuthenticationError;

/// AEGIS-128X4 with a 128 bit tag
pub const Aegis128X4 = Aegis128XGeneric(4, 128);
/// AEGIS-128X2 with a 128 bit tag
pub const Aegis128X2 = Aegis128XGeneric(2, 128);
/// AEGIS-128L with a 128 bit tag
pub const Aegis128L = Aegis128XGeneric(1, 128);

/// AEGIS-256X4 with a 128 bit tag
pub const Aegis256X4 = Aegis256XGeneric(4, 128);
/// AEGIS-256X2 with a 128 bit tag
pub const Aegis256X2 = Aegis256XGeneric(2, 128);
/// AEGIS-256 with a 128 bit tag
pub const Aegis256 = Aegis256XGeneric(1, 128);

/// AEGIS-128X4 with a 256 bit tag
pub const Aegis128X4_256 = Aegis128XGeneric(4, 256);
/// AEGIS-128X2 with a 256 bit tag
pub const Aegis128X2_256 = Aegis128XGeneric(2, 256);
/// AEGIS-128L with a 256 bit tag
pub const Aegis128L_256 = Aegis128XGeneric(1, 256);

/// AEGIS-256X4 with a 256 bit tag
pub const Aegis256X4_256 = Aegis256XGeneric(4, 256);
/// AEGIS-256X2 with a 256 bit tag
pub const Aegis256X2_256 = Aegis256XGeneric(2, 256);
/// AEGIS-256 with a 256 bit tag
pub const Aegis256_256 = Aegis256XGeneric(1, 256);

fn State128X(comptime degree: u7) type {
    return struct {
        const AesBlockVec = crypto.core.aes.BlockVec(degree);
        const State = @This();

        blocks: [8]AesBlockVec,

        const aes_block_length = AesBlockVec.block_length;
        const rate = aes_block_length * 2;
        const alignment = AesBlockVec.native_word_size;

        fn init(key: [16]u8, nonce: [16]u8) State {
            const c1 = AesBlockVec.fromBytes(&[16]u8{ 0xdb, 0x3d, 0x18, 0x55, 0x6d, 0xc2, 0x2f, 0xf1, 0x20, 0x11, 0x31, 0x42, 0x73, 0xb5, 0x28, 0xdd } ** degree);
            const c2 = AesBlockVec.fromBytes(&[16]u8{ 0x0, 0x1, 0x01, 0x02, 0x03, 0x05, 0x08, 0x0d, 0x15, 0x22, 0x37, 0x59, 0x90, 0xe9, 0x79, 0x62 } ** degree);
            const key_block = AesBlockVec.fromBytes(&(key ** degree));
            const nonce_block = AesBlockVec.fromBytes(&(nonce ** degree));
            const blocks = [8]AesBlockVec{
                key_block.xorBlocks(nonce_block),
                c1,
                c2,
                c1,
                key_block.xorBlocks(nonce_block),
                key_block.xorBlocks(c2),
                key_block.xorBlocks(c1),
                key_block.xorBlocks(c2),
            };
            var state = State{ .blocks = blocks };
            if (degree > 1) {
                const context_block = ctx: {
                    var contexts_bytes = [_]u8{0} ** aes_block_length;
                    for (0..degree) |i| {
                        contexts_bytes[i * 16] = @intCast(i);
                        contexts_bytes[i * 16 + 1] = @intCast(degree - 1);
                    }
                    break :ctx AesBlockVec.fromBytes(&contexts_bytes);
                };
                for (0..10) |_| {
                    state.blocks[3] = state.blocks[3].xorBlocks(context_block);
                    state.blocks[7] = state.blocks[7].xorBlocks(context_block);
                    state.update(nonce_block, key_block);
                }
            } else {
                for (0..10) |_| {
                    state.update(nonce_block, key_block);
                }
            }
            return state;
        }

        fn update(state: *State, d1: AesBlockVec, d2: AesBlockVec) void {
            const blocks = &state.blocks;
            const tmp = blocks[7];
            comptime var i: usize = 7;
            inline while (i > 0) : (i -= 1) {
                blocks[i] = blocks[i - 1].encrypt(blocks[i]);
            }
            blocks[0] = tmp.encrypt(blocks[0]);
            blocks[0] = blocks[0].xorBlocks(d1);
            blocks[4] = blocks[4].xorBlocks(d2);
        }

        fn absorb(state: *State, src: *const [rate]u8) void {
            const msg0 = AesBlockVec.fromBytes(src[0..aes_block_length]);
            const msg1 = AesBlockVec.fromBytes(src[aes_block_length..rate]);
            state.update(msg0, msg1);
        }

        fn enc(state: *State, dst: *[rate]u8, src: *const [rate]u8) void {
            const blocks = &state.blocks;
            const msg0 = AesBlockVec.fromBytes(src[0..aes_block_length]);
            const msg1 = AesBlockVec.fromBytes(src[aes_block_length..rate]);
            var tmp0 = msg0.xorBlocks(blocks[6]).xorBlocks(blocks[1]);
            var tmp1 = msg1.xorBlocks(blocks[2]).xorBlocks(blocks[5]);
            tmp0 = tmp0.xorBlocks(blocks[2].andBlocks(blocks[3]));
            tmp1 = tmp1.xorBlocks(blocks[6].andBlocks(blocks[7]));
            dst[0..aes_block_length].* = tmp0.toBytes();
            dst[aes_block_length..rate].* = tmp1.toBytes();
            state.update(msg0, msg1);
        }

        fn dec(state: *State, dst: *[rate]u8, src: *const [rate]u8) void {
            const blocks = &state.blocks;
            var msg0 = AesBlockVec.fromBytes(src[0..aes_block_length]).xorBlocks(blocks[6]).xorBlocks(blocks[1]);
            var msg1 = AesBlockVec.fromBytes(src[aes_block_length..rate]).xorBlocks(blocks[2]).xorBlocks(blocks[5]);
            msg0 = msg0.xorBlocks(blocks[2].andBlocks(blocks[3]));
            msg1 = msg1.xorBlocks(blocks[6].andBlocks(blocks[7]));
            dst[0..aes_block_length].* = msg0.toBytes();
            dst[aes_block_length..rate].* = msg1.toBytes();
            state.update(msg0, msg1);
        }

        fn decLast(state: *State, dst: []u8, src: []const u8) void {
            const blocks = &state.blocks;
            const z0 = blocks[6].xorBlocks(blocks[1]).xorBlocks(blocks[2].andBlocks(blocks[3]));
            const z1 = blocks[2].xorBlocks(blocks[5]).xorBlocks(blocks[6].andBlocks(blocks[7]));
            var pad = [_]u8{0} ** rate;
            pad[0..aes_block_length].* = z0.toBytes();
            pad[aes_block_length..].* = z1.toBytes();
            for (pad[0..src.len], src) |*p, x| p.* ^= x;
            @memcpy(dst, pad[0..src.len]);
            @memset(pad[src.len..], 0);
            const msg0 = AesBlockVec.fromBytes(pad[0..aes_block_length]);
            const msg1 = AesBlockVec.fromBytes(pad[aes_block_length..rate]);
            state.update(msg0, msg1);
        }

        fn finalize(state: *State, comptime tag_bits: u9, adlen: usize, mlen: usize) [tag_bits / 8]u8 {
            const blocks = &state.blocks;
            var sizes: [aes_block_length]u8 = undefined;
            mem.writeInt(u64, sizes[0..8], @as(u64, adlen) * 8, .little);
            mem.writeInt(u64, sizes[8..16], @as(u64, mlen) * 8, .little);
            for (1..degree) |i| {
                @memcpy(sizes[i * 16 ..][0..16], sizes[0..16]);
            }
            const tmp = AesBlockVec.fromBytes(&sizes).xorBlocks(blocks[2]);
            for (0..7) |_| {
                state.update(tmp, tmp);
            }
            switch (tag_bits) {
                128 => {
                    var tag_multi = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).xorBlocks(blocks[3]).xorBlocks(blocks[4]).xorBlocks(blocks[5]).xorBlocks(blocks[6]).toBytes();
                    var tag = tag_multi[0..16].*;
                    @memcpy(tag[0..], tag_multi[0..16]);
                    for (1..degree) |d| {
                        for (0..16) |i| {
                            tag[i] ^= tag_multi[d * 16 + i];
                        }
                    }
                    return tag;
                },
                256 => {
                    const tag_multi_1 = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).xorBlocks(blocks[3]).toBytes();
                    const tag_multi_2 = blocks[4].xorBlocks(blocks[5]).xorBlocks(blocks[6]).xorBlocks(blocks[7]).toBytes();
                    var tag = tag_multi_1[0..16].* ++ tag_multi_2[0..16].*;
                    for (1..degree) |d| {
                        for (0..16) |i| {
                            tag[i] ^= tag_multi_1[d * 16 + i];
                            tag[i + 16] ^= tag_multi_2[d * 16 + i];
                        }
                    }
                    return tag;
                },
                else => unreachable,
            }
        }

        fn finalizeMac(state: *State, comptime tag_bits: u9, datalen: usize) [tag_bits / 8]u8 {
            const blocks = &state.blocks;
            var sizes: [aes_block_length]u8 = undefined;
            mem.writeInt(u64, sizes[0..8], @as(u64, datalen) * 8, .little);
            mem.writeInt(u64, sizes[8..16], tag_bits, .little);
            for (1..degree) |i| {
                @memcpy(sizes[i * 16 ..][0..16], sizes[0..16]);
            }
            var t = blocks[2].xorBlocks(AesBlockVec.fromBytes(&sizes));
            for (0..7) |_| {
                state.update(t, t);
            }
            if (degree > 1) {
                var v = [_]u8{0} ** rate;
                switch (tag_bits) {
                    128 => {
                        const tags = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).xorBlocks(blocks[3]).xorBlocks(blocks[4]).xorBlocks(blocks[5]).xorBlocks(blocks[6]).toBytes();
                        for (0..degree / 2) |d| {
                            v[0..16].* = tags[d * 32 ..][0..16].*;
                            v[rate / 2 ..][0..16].* = tags[d * 32 ..][16..32].*;
                            state.absorb(&v);
                        }
                    },
                    256 => {
                        const tags_0 = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).xorBlocks(blocks[3]).toBytes();
                        const tags_1 = blocks[4].xorBlocks(blocks[5]).xorBlocks(blocks[6]).xorBlocks(blocks[7]).toBytes();
                        for (1..degree) |d| {
                            v[0..16].* = tags_0[d * 16 ..][0..16].*;
                            v[rate / 2 ..][0..16].* = tags_1[d * 16 ..][0..16].*;
                            state.absorb(&v);
                        }
                    },
                    else => unreachable,
                }
                mem.writeInt(u64, sizes[0..8], degree, .little);
                mem.writeInt(u64, sizes[8..16], tag_bits, .little);
                t = blocks[2].xorBlocks(AesBlockVec.fromBytes(&sizes));
                for (0..7) |_| {
                    state.update(t, t);
                }
            }
            switch (tag_bits) {
                128 => {
                    const tags = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).xorBlocks(blocks[3]).xorBlocks(blocks[4]).xorBlocks(blocks[5]).xorBlocks(blocks[6]).toBytes();
                    return tags[0..16].*;
                },
                256 => {
                    const tags_0 = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).xorBlocks(blocks[3]).toBytes();
                    const tags_1 = blocks[4].xorBlocks(blocks[5]).xorBlocks(blocks[6]).xorBlocks(blocks[7]).toBytes();
                    return tags_0[0..16].* ++ tags_1[0..16].*;
                },
                else => unreachable,
            }
        }
    };
}

/// AEGIS is a very fast authenticated encryption system built on top of the core AES function.
///
/// The 128 bits variants of AEGIS have a 128 bit key and a 128 bit nonce.
///
/// https://datatracker.ietf.org/doc/draft-irtf-cfrg-aegis-aead/
fn Aegis128XGeneric(comptime degree: u7, comptime tag_bits: u9) type {
    comptime assert(degree > 0); // degree must be greater than 0
    comptime assert(tag_bits == 128 or tag_bits == 256); // tag must be 128 or 256 bits

    return struct {
        const State = State128X(degree);

        pub const tag_length = tag_bits / 8;
        pub const nonce_length = 16;
        pub const key_length = 16;
        pub const block_length = State.rate;

        const alignment = State.alignment;

        /// c: ciphertext: output buffer should be of size m.len
        /// tag: authentication tag: output MAC
        /// m: message
        /// ad: Associated Data
        /// npub: public nonce
        /// k: private key
        pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) void {
            assert(c.len == m.len);
            var state = State.init(key, npub);
            var src: [block_length]u8 align(alignment) = undefined;
            var dst: [block_length]u8 align(alignment) = undefined;
            var i: usize = 0;
            while (i + block_length <= ad.len) : (i += block_length) {
                state.absorb(ad[i..][0..block_length]);
            }
            if (ad.len % block_length != 0) {
                @memset(src[0..], 0);
                @memcpy(src[0 .. ad.len % block_length], ad[i..][0 .. ad.len % block_length]);
                state.absorb(&src);
            }
            i = 0;
            while (i + block_length <= m.len) : (i += block_length) {
                state.enc(c[i..][0..block_length], m[i..][0..block_length]);
            }
            if (m.len % block_length != 0) {
                @memset(src[0..], 0);
                @memcpy(src[0 .. m.len % block_length], m[i..][0 .. m.len % block_length]);
                state.enc(&dst, &src);
                @memcpy(c[i..][0 .. m.len % block_length], dst[0 .. m.len % block_length]);
            }
            tag.* = state.finalize(tag_bits, ad.len, m.len);
        }

        /// `m`: Message
        /// `c`: Ciphertext
        /// `tag`: Authentication tag
        /// `ad`: Associated data
        /// `npub`: Public nonce
        /// `k`: Private key
        /// Asserts `c.len == m.len`.
        ///
        /// Contents of `m` are undefined if an error is returned.
        pub fn decrypt(m: []u8, c: []const u8, tag: [tag_length]u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) AuthenticationError!void {
            assert(c.len == m.len);
            var state = State.init(key, npub);
            var src: [block_length]u8 align(alignment) = undefined;
            var i: usize = 0;
            while (i + block_length <= ad.len) : (i += block_length) {
                state.absorb(ad[i..][0..block_length]);
            }
            if (ad.len % block_length != 0) {
                @memset(src[0..], 0);
                @memcpy(src[0 .. ad.len % block_length], ad[i..][0 .. ad.len % block_length]);
                state.absorb(&src);
            }
            i = 0;
            while (i + block_length <= m.len) : (i += block_length) {
                state.dec(m[i..][0..block_length], c[i..][0..block_length]);
            }
            if (m.len % block_length != 0) {
                state.decLast(m[i..], c[i..]);
            }
            var computed_tag = state.finalize(tag_bits, ad.len, m.len);
            const verify = crypto.timing_safe.eql([tag_length]u8, computed_tag, tag);
            if (!verify) {
                crypto.secureZero(u8, &computed_tag);
                @memset(m, undefined);
                return error.AuthenticationFailed;
            }
        }
    };
}

fn State256X(comptime degree: u7) type {
    return struct {
        const AesBlockVec = crypto.core.aes.BlockVec(degree);
        const State = @This();

        blocks: [6]AesBlockVec,

        const aes_block_length = AesBlockVec.block_length;
        const rate = aes_block_length;
        const alignment = AesBlockVec.native_word_size;

        fn init(key: [32]u8, nonce: [32]u8) State {
            const c1 = AesBlockVec.fromBytes(&[16]u8{ 0xdb, 0x3d, 0x18, 0x55, 0x6d, 0xc2, 0x2f, 0xf1, 0x20, 0x11, 0x31, 0x42, 0x73, 0xb5, 0x28, 0xdd } ** degree);
            const c2 = AesBlockVec.fromBytes(&[16]u8{ 0x0, 0x1, 0x01, 0x02, 0x03, 0x05, 0x08, 0x0d, 0x15, 0x22, 0x37, 0x59, 0x90, 0xe9, 0x79, 0x62 } ** degree);
            const key_block1 = AesBlockVec.fromBytes(key[0..16] ** degree);
            const key_block2 = AesBlockVec.fromBytes(key[16..32] ** degree);
            const nonce_block1 = AesBlockVec.fromBytes(nonce[0..16] ** degree);
            const nonce_block2 = AesBlockVec.fromBytes(nonce[16..32] ** degree);
            const kxn1 = key_block1.xorBlocks(nonce_block1);
            const kxn2 = key_block2.xorBlocks(nonce_block2);
            const blocks = [6]AesBlockVec{
                kxn1,
                kxn2,
                c1,
                c2,
                key_block1.xorBlocks(c2),
                key_block2.xorBlocks(c1),
            };
            var state = State{ .blocks = blocks };
            if (degree > 1) {
                const context_block = ctx: {
                    var contexts_bytes = [_]u8{0} ** aes_block_length;
                    for (0..degree) |i| {
                        contexts_bytes[i * 16] = @intCast(i);
                        contexts_bytes[i * 16 + 1] = @intCast(degree - 1);
                    }
                    break :ctx AesBlockVec.fromBytes(&contexts_bytes);
                };
                for (0..4) |_| {
                    state.blocks[3] = state.blocks[3].xorBlocks(context_block);
                    state.blocks[5] = state.blocks[5].xorBlocks(context_block);
                    state.update(key_block1);
                    state.blocks[3] = state.blocks[3].xorBlocks(context_block);
                    state.blocks[5] = state.blocks[5].xorBlocks(context_block);
                    state.update(key_block2);
                    state.blocks[3] = state.blocks[3].xorBlocks(context_block);
                    state.blocks[5] = state.blocks[5].xorBlocks(context_block);
                    state.update(kxn1);
                    state.blocks[3] = state.blocks[3].xorBlocks(context_block);
                    state.blocks[5] = state.blocks[5].xorBlocks(context_block);
                    state.update(kxn2);
                }
            } else {
                for (0..4) |_| {
                    state.update(key_block1);
                    state.update(key_block2);
                    state.update(kxn1);
                    state.update(kxn2);
                }
            }
            return state;
        }

        fn update(state: *State, d: AesBlockVec) void {
            const blocks = &state.blocks;
            const tmp = blocks[5].encrypt(blocks[0]);
            comptime var i: usize = 5;
            inline while (i > 0) : (i -= 1) {
                blocks[i] = blocks[i - 1].encrypt(blocks[i]);
            }
            blocks[0] = tmp.xorBlocks(d);
        }

        fn absorb(state: *State, src: *const [rate]u8) void {
            const msg = AesBlockVec.fromBytes(src);
            state.update(msg);
        }

        fn enc(state: *State, dst: *[rate]u8, src: *const [rate]u8) void {
            const blocks = &state.blocks;
            const msg = AesBlockVec.fromBytes(src);
            var tmp = msg.xorBlocks(blocks[5]).xorBlocks(blocks[4]).xorBlocks(blocks[1]);
            tmp = tmp.xorBlocks(blocks[2].andBlocks(blocks[3]));
            dst.* = tmp.toBytes();
            state.update(msg);
        }

        fn dec(state: *State, dst: *[rate]u8, src: *const [rate]u8) void {
            const blocks = &state.blocks;
            var msg = AesBlockVec.fromBytes(src).xorBlocks(blocks[5]).xorBlocks(blocks[4]).xorBlocks(blocks[1]);
            msg = msg.xorBlocks(blocks[2].andBlocks(blocks[3]));
            dst.* = msg.toBytes();
            state.update(msg);
        }

        fn decLast(state: *State, dst: []u8, src: []const u8) void {
            const blocks = &state.blocks;
            const z = blocks[5].xorBlocks(blocks[4]).xorBlocks(blocks[1]).xorBlocks(blocks[2].andBlocks(blocks[3]));
            var pad = z.toBytes();
            for (pad[0..src.len], src) |*p, x| p.* ^= x;
            @memcpy(dst, pad[0..src.len]);
            @memset(pad[src.len..], 0);
            const msg = AesBlockVec.fromBytes(pad[0..]);
            state.update(msg);
        }

        fn finalize(state: *State, comptime tag_bits: u9, adlen: usize, mlen: usize) [tag_bits / 8]u8 {
            const blocks = &state.blocks;
            var sizes: [aes_block_length]u8 = undefined;
            mem.writeInt(u64, sizes[0..8], @as(u64, adlen) * 8, .little);
            mem.writeInt(u64, sizes[8..16], @as(u64, mlen) * 8, .little);
            for (1..degree) |i| {
                @memcpy(sizes[i * 16 ..][0..16], sizes[0..16]);
            }
            const tmp = AesBlockVec.fromBytes(&sizes).xorBlocks(blocks[3]);
            for (0..7) |_| {
                state.update(tmp);
            }
            switch (tag_bits) {
                128 => {
                    var tag_multi = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).xorBlocks(blocks[3]).xorBlocks(blocks[4]).xorBlocks(blocks[5]).toBytes();
                    var tag = tag_multi[0..16].*;
                    @memcpy(tag[0..], tag_multi[0..16]);
                    for (1..degree) |d| {
                        for (0..16) |i| {
                            tag[i] ^= tag_multi[d * 16 + i];
                        }
                    }
                    return tag;
                },
                256 => {
                    const tag_multi_1 = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).toBytes();
                    const tag_multi_2 = blocks[3].xorBlocks(blocks[4]).xorBlocks(blocks[5]).toBytes();
                    var tag = tag_multi_1[0..16].* ++ tag_multi_2[0..16].*;
                    for (1..degree) |d| {
                        for (0..16) |i| {
                            tag[i] ^= tag_multi_1[d * 16 + i];
                            tag[i + 16] ^= tag_multi_2[d * 16 + i];
                        }
                    }
                    return tag;
                },
                else => unreachable,
            }
        }

        fn finalizeMac(state: *State, comptime tag_bits: u9, datalen: usize) [tag_bits / 8]u8 {
            const blocks = &state.blocks;
            var sizes: [aes_block_length]u8 = undefined;
            mem.writeInt(u64, sizes[0..8], @as(u64, datalen) * 8, .little);
            mem.writeInt(u64, sizes[8..16], tag_bits, .little);
            for (1..degree) |i| {
                @memcpy(sizes[i * 16 ..][0..16], sizes[0..16]);
            }
            var t = blocks[3].xorBlocks(AesBlockVec.fromBytes(&sizes));
            for (0..7) |_| {
                state.update(t);
            }
            if (degree > 1) {
                var v = [_]u8{0} ** rate;
                switch (tag_bits) {
                    128 => {
                        const tags = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).xorBlocks(blocks[3]).xorBlocks(blocks[4]).xorBlocks(blocks[5]).toBytes();
                        for (1..degree) |d| {
                            v[0..16].* = tags[d * 16 ..][0..16].*;
                            state.absorb(&v);
                        }
                    },
                    256 => {
                        const tags_0 = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).toBytes();
                        const tags_1 = blocks[3].xorBlocks(blocks[4]).xorBlocks(blocks[5]).toBytes();
                        for (1..degree) |d| {
                            v[0..16].* = tags_0[d * 16 ..][0..16].*;
                            state.absorb(&v);
                            v[0..16].* = tags_1[d * 16 ..][0..16].*;
                            state.absorb(&v);
                        }
                    },
                    else => unreachable,
                }
                mem.writeInt(u64, sizes[0..8], degree, .little);
                mem.writeInt(u64, sizes[8..16], tag_bits, .little);
                t = blocks[3].xorBlocks(AesBlockVec.fromBytes(&sizes));
                for (0..7) |_| {
                    state.update(t);
                }
            }
            switch (tag_bits) {
                128 => {
                    const tags = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).xorBlocks(blocks[3]).xorBlocks(blocks[4]).xorBlocks(blocks[5]).toBytes();
                    return tags[0..16].*;
                },
                256 => {
                    const tags_0 = blocks[0].xorBlocks(blocks[1]).xorBlocks(blocks[2]).toBytes();
                    const tags_1 = blocks[3].xorBlocks(blocks[4]).xorBlocks(blocks[5]).toBytes();
                    return tags_0[0..16].* ++ tags_1[0..16].*;
                },
                else => unreachable,
            }
        }
    };
}

/// AEGIS is a very fast authenticated encryption system built on top of the core AES function.
///
/// The 256 bits variants of AEGIS have a 256 bit key and a 256 bit nonce.
///
/// https://datatracker.ietf.org/doc/draft-irtf-cfrg-aegis-aead/
fn Aegis256XGeneric(comptime degree: u7, comptime tag_bits: u9) type {
    comptime assert(degree > 0); // degree must be greater than 0
    comptime assert(tag_bits == 128 or tag_bits == 256); // tag must be 128 or 256 bits

    return struct {
        const State = State256X(degree);

        pub const tag_length = tag_bits / 8;
        pub const nonce_length = 32;
        pub const key_length = 32;
        pub const block_length = State.rate;

        const alignment = State.alignment;

        /// c: ciphertext: output buffer should be of size m.len
        /// tag: authentication tag: output MAC
        /// m: message
        /// ad: Associated Data
        /// npub: public nonce
        /// k: private key
        pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) void {
            assert(c.len == m.len);
            var state = State.init(key, npub);
            var src: [block_length]u8 align(alignment) = undefined;
            var dst: [block_length]u8 align(alignment) = undefined;
            var i: usize = 0;
            while (i + block_length <= ad.len) : (i += block_length) {
                state.absorb(ad[i..][0..block_length]);
            }
            if (ad.len % block_length != 0) {
                @memset(src[0..], 0);
                @memcpy(src[0 .. ad.len % block_length], ad[i..][0 .. ad.len % block_length]);
                state.absorb(&src);
            }
            i = 0;
            while (i + block_length <= m.len) : (i += block_length) {
                state.enc(c[i..][0..block_length], m[i..][0..block_length]);
            }
            if (m.len % block_length != 0) {
                @memset(src[0..], 0);
                @memcpy(src[0 .. m.len % block_length], m[i..][0 .. m.len % block_length]);
                state.enc(&dst, &src);
                @memcpy(c[i..][0 .. m.len % block_length], dst[0 .. m.len % block_length]);
            }
            tag.* = state.finalize(tag_bits, ad.len, m.len);
        }

        /// `m`: Message
        /// `c`: Ciphertext
        /// `tag`: Authentication tag
        /// `ad`: Associated data
        /// `npub`: Public nonce
        /// `k`: Private key
        /// Asserts `c.len == m.len`.
        ///
        /// Contents of `m` are undefined if an error is returned.
        pub fn decrypt(m: []u8, c: []const u8, tag: [tag_length]u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) AuthenticationError!void {
            assert(c.len == m.len);
            var state = State.init(key, npub);
            var src: [block_length]u8 align(alignment) = undefined;
            var i: usize = 0;
            while (i + block_length <= ad.len) : (i += block_length) {
                state.absorb(ad[i..][0..block_length]);
            }
            if (ad.len % block_length != 0) {
                @memset(src[0..], 0);
                @memcpy(src[0 .. ad.len % block_length], ad[i..][0 .. ad.len % block_length]);
                state.absorb(&src);
            }
            i = 0;
            while (i + block_length <= m.len) : (i += block_length) {
                state.dec(m[i..][0..block_length], c[i..][0..block_length]);
            }
            if (m.len % block_length != 0) {
                state.decLast(m[i..], c[i..]);
            }
            var computed_tag = state.finalize(tag_bits, ad.len, m.len);
            const verify = crypto.timing_safe.eql([tag_length]u8, computed_tag, tag);
            if (!verify) {
                crypto.secureZero(u8, &computed_tag);
                @memset(m, undefined);
                return error.AuthenticationFailed;
            }
        }
    };
}

/// The `Aegis128X4Mac` message authentication function outputs 256 bit tags.
/// In addition to being extremely fast, its large state, non-linearity
/// and non-invertibility provides the following properties:
/// - 128 bit security, stronger than GHash/Polyval/Poly1305.
/// - Recovering the secret key from the state would require ~2^128 attempts,
///   which is infeasible for any practical adversary.
/// - It has a large security margin against internal collisions.
pub const Aegis128X4Mac = AegisMac(Aegis128X4_256);

/// The `Aegis128X2Mac` message authentication function outputs 256 bit tags.
/// In addition to being extremely fast, its large state, non-linearity
/// and non-invertibility provides the following properties:
/// - 128 bit security, stronger than GHash/Polyval/Poly1305.
/// - Recovering the secret key from the state would require ~2^128 attempts,
///   which is infeasible for any practical adversary.
/// - It has a large security margin against internal collisions.
pub const Aegis128X2Mac = AegisMac(Aegis128X2_256);

/// The `Aegis128LMac` message authentication function outputs 256 bit tags.
/// In addition to being extremely fast, its large state, non-linearity
/// and non-invertibility provides the following properties:
/// - 128 bit security, stronger than GHash/Polyval/Poly1305.
/// - Recovering the secret key from the state would require ~2^128 attempts,
///   which is infeasible for any practical adversary.
/// - It has a large security margin against internal collisions.
pub const Aegis128LMac = AegisMac(Aegis128L_256);

/// The `Aegis256X4Mac` message authentication function has a 256-bit key size,
/// and outputs 256 bit tags.
/// The key size is the main practical difference with `Aegis128X4Mac`.
/// AEGIS' large state, non-linearity and non-invertibility provides the
/// following properties:
/// - 256 bit security against forgery.
/// - Recovering the secret key from the state would require ~2^256 attempts,
///   which is infeasible for any practical adversary.
/// - It has a large security margin against internal collisions.
pub const Aegis256X4Mac = AegisMac(Aegis256X4_256);

/// The `Aegis256X2Mac` message authentication function has a 256-bit key size,
/// and outputs 256 bit tags.
/// The key size is the main practical difference with `Aegis128X2Mac`.
/// AEGIS' large state, non-linearity and non-invertibility provides the
/// following properties:
/// - 256 bit security against forgery.
/// - Recovering the secret key from the state would require ~2^256 attempts,
///   which is infeasible for any practical adversary.
/// - It has a large security margin against internal collisions.
pub const Aegis256X2Mac = AegisMac(Aegis256X2_256);

/// The `Aegis256Mac` message authentication function has a 256-bit key size,
/// and outputs 256 bit tags.
/// The key size is the main practical difference with `Aegis128LMac`.
/// AEGIS' large state, non-linearity and non-invertibility provides the
/// following properties:
/// - 256 bit security against forgery.
/// - Recovering the secret key from the state would require ~2^256 attempts,
///   which is infeasible for any practical adversary.
/// - It has a large security margin against internal collisions.
pub const Aegis256Mac = AegisMac(Aegis256_256);

/// AEGIS-128X4 MAC with 128-bit tags
pub const Aegis128X4Mac_128 = AegisMac(Aegis128X4);

/// AEGIS-128X2 MAC with 128-bit tags
pub const Aegis128X2Mac_128 = AegisMac(Aegis128X2);

/// AEGIS-128L MAC with 128-bit tags
pub const Aegis128LMac_128 = AegisMac(Aegis128L);

/// AEGIS-256X4 MAC with 128-bit tags
pub const Aegis256X4Mac_128 = AegisMac(Aegis256X4);

/// AEGIS-256X2 MAC with 128-bit tags
pub const Aegis256X2Mac_128 = AegisMac(Aegis256X2);

/// AEGIS-256 MAC with 128-bit tags
pub const Aegis256Mac_128 = AegisMac(Aegis256);

fn AegisMac(comptime T: type) type {
    return struct {
        const Mac = @This();

        pub const mac_length = T.tag_length;
        pub const key_length = T.key_length;
        pub const nonce_length = T.nonce_length;
        pub const block_length = T.block_length;

        state: T.State,
        buf: [block_length]u8 = undefined,
        off: usize = 0,
        msg_len: usize = 0,

        /// Initialize a state for the MAC function, with a key and a nonce
        pub fn initWithNonce(key: *const [key_length]u8, nonce: *const [nonce_length]u8) Mac {
            return Mac{
                .state = T.State.init(key.*, nonce.*),
            };
        }

        /// Initialize a state for the MAC function, with a default nonce
        pub fn init(key: *const [key_length]u8) Mac {
            return Mac{
                .state = T.State.init(key.*, [_]u8{0} ** nonce_length),
            };
        }

        /// Add data to the state
        pub fn update(self: *Mac, b: []const u8) void {
            self.msg_len += b.len;

            const len_partial = @min(b.len, block_length - self.off);
            @memcpy(self.buf[self.off..][0..len_partial], b[0..len_partial]);
            self.off += len_partial;
            if (self.off < block_length) {
                return;
            }
            self.state.absorb(&self.buf);

            var i = len_partial;
            self.off = 0;
            while (i + block_length * 2 <= b.len) : (i += block_length * 2) {
                self.state.absorb(b[i..][0..block_length]);
                self.state.absorb(b[i..][block_length .. block_length * 2]);
            }
            while (i + block_length <= b.len) : (i += block_length) {
                self.state.absorb(b[i..][0..block_length]);
            }
            if (i != b.len) {
                self.off = b.len - i;
                @memcpy(self.buf[0..self.off], b[i..]);
            }
        }

        /// Return an authentication tag for the current state
        pub fn final(self: *Mac, out: *[mac_length]u8) void {
            if (self.off > 0) {
                var pad = [_]u8{0} ** block_length;
                @memcpy(pad[0..self.off], self.buf[0..self.off]);
                self.state.absorb(&pad);
            }
            out.* = self.state.finalizeMac(T.tag_length * 8, self.msg_len);
        }

        /// Return an authentication tag for a message, a key and a nonce
        pub fn createWithNonce(out: *[mac_length]u8, msg: []const u8, key: *const [key_length]u8, nonce: *const [nonce_length]u8) void {
            var ctx = Mac.initWithNonce(key, nonce);
            ctx.update(msg);
            ctx.final(out);
        }

        /// Return an authentication tag for a message and a key
        pub fn create(out: *[mac_length]u8, msg: []const u8, key: *const [key_length]u8) void {
            var ctx = Mac.init(key);
            ctx.update(msg);
            ctx.final(out);
        }
    };
}

const htest 
```
