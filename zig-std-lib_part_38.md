```
y from bytes
            pub fn fromBytes(bytes: [encoded_length]u8) !PublicKey {
                var pk: PublicKey = undefined;
                @memcpy(&pk.rho, bytes[0..32]);
                @memcpy(&pk.t1_packed, bytes[32..]);

                pk.t1 = PolyVecK.unpackT1(pk.t1_packed[0..]);
                pk.A = MatKxL.derive(&pk.rho);
                pk.tr = crh(p.tr_size, .{&bytes});

                return pk;
            }
        };

        /// ML-DSA secret key
        pub const SecretKey = struct {
            /// Size of the encoded secret key in bytes
            pub const encoded_length: usize = 32 + 32 + p.tr_size +
                polyLeqEtaPackedSize() * (p.l + p.k) + polyT0PackedSize() * p.k;

            rho: [32]u8, // Seed for matrix A
            key: [32]u8, // Seed for signature generation randomness
            tr: [p.tr_size]u8, // CRH(rho || t1)
            s1: PolyVecL, // Secret vector 1
            s2: PolyVecK, // Secret vector 2
            t0: PolyVecK, // Low bits of t = As1 + s2

            // Cached values (in NTT domain)
            A: MatKxL,
            s1_hat: PolyVecL,
            s2_hat: PolyVecK,
            t0_hat: PolyVecK,

            /// Encode secret key to bytes
            pub fn toBytes(self: SecretKey) [encoded_length]u8 {
                var out: [encoded_length]u8 = undefined;
                var offset: usize = 0;

                @memcpy(out[offset .. offset + 32], &self.rho);
                offset += 32;

                @memcpy(out[offset .. offset + 32], &self.key);
                offset += 32;

                @memcpy(out[offset .. offset + p.tr_size], &self.tr);
                offset += p.tr_size;

                if (p.eta == 2) {
                    self.s1.packLeqEta(2, out[offset..][0 .. p.l * polyLeqEtaPackedSize()]);
                } else {
                    self.s1.packLeqEta(4, out[offset..][0 .. p.l * polyLeqEtaPackedSize()]);
                }
                offset += p.l * polyLeqEtaPackedSize();

                if (p.eta == 2) {
                    self.s2.packLeqEta(2, out[offset..][0 .. p.k * polyLeqEtaPackedSize()]);
                } else {
                    self.s2.packLeqEta(4, out[offset..][0 .. p.k * polyLeqEtaPackedSize()]);
                }
                offset += p.k * polyLeqEtaPackedSize();

                self.t0.packT0(out[offset..][0 .. p.k * polyT0PackedSize()]);
                offset += p.k * polyT0PackedSize();

                return out;
            }

            /// Decode secret key from bytes
            pub fn fromBytes(bytes: [encoded_length]u8) !SecretKey {
                var sk: SecretKey = undefined;
                var offset: usize = 0;

                @memcpy(&sk.rho, bytes[offset .. offset + 32]);
                offset += 32;

                @memcpy(&sk.key, bytes[offset .. offset + 32]);
                offset += 32;

                @memcpy(&sk.tr, bytes[offset .. offset + p.tr_size]);
                offset += p.tr_size;

                sk.s1 = if (p.eta == 2)
                    PolyVecL.unpackLeqEta(2, bytes[offset..][0 .. p.l * polyLeqEtaPackedSize()])
                else
                    PolyVecL.unpackLeqEta(4, bytes[offset..][0 .. p.l * polyLeqEtaPackedSize()]);
                offset += p.l * polyLeqEtaPackedSize();

                sk.s2 = if (p.eta == 2)
                    PolyVecK.unpackLeqEta(2, bytes[offset..][0 .. p.k * polyLeqEtaPackedSize()])
                else
                    PolyVecK.unpackLeqEta(4, bytes[offset..][0 .. p.k * polyLeqEtaPackedSize()]);
                offset += p.k * polyLeqEtaPackedSize();

                sk.t0 = PolyVecK.unpackT0(bytes[offset..][0 .. p.k * polyT0PackedSize()]);
                offset += p.k * polyT0PackedSize();

                // Compute cached NTT values for efficient signing
                sk.A = MatKxL.derive(&sk.rho);
                sk.s1_hat = sk.s1.ntt();
                sk.s2_hat = sk.s2.ntt();
                sk.t0_hat = sk.t0.ntt();

                return sk;
            }

            /// Compute the public key from this private key
            pub fn public(self: *const SecretKey) PublicKey {
                var pk: PublicKey = undefined;
                pk.rho = self.rho;
                pk.A = self.A;
                pk.tr = self.tr;

                // Reconstruct t = As1 + s2, then extract high bits t1
                // Using power2Round: t = t1 * 2^D + t0
                const t = computeT(self.A, self.s1_hat, self.s2);

                var t0_unused: PolyVecK = undefined;
                pk.t1 = t.power2Round(&t0_unused);
                pk.t1.packT1(&pk.t1_packed);

                return pk;
            }

            /// Create a Signer for incrementally signing a message.
            /// The noise parameter can be null for deterministic signatures,
            /// or provide randomness for hedged signatures (recommended for fault attack resistance).
            pub fn signer(self: *const SecretKey, noise: ?[noise_length]u8) !Signer {
                return self.signerWithContext(noise, "");
            }

            /// Create a Signer for incrementally signing a message with context.
            /// The noise parameter can be null for deterministic signatures,
            /// or provide randomness for hedged signatures (recommended for fault attack resistance).
            /// The context parameter is an optional context string (max 255 bytes).
            pub fn signerWithContext(self: *const SecretKey, noise: ?[noise_length]u8, context: []const u8) ContextTooLongError!Signer {
                return Signer.init(self, noise, context);
            }
        };

        /// Generate a new key pair from a seed (deterministic)
        pub fn newKeyFromSeed(seed: *const [seed_length]u8) struct { pk: PublicKey, sk: SecretKey } {
            var sk: SecretKey = undefined;
            var pk: PublicKey = undefined;

            // NIST mode: expand seed || k || l using SHAKE-256 to get 128-byte expanded seed
            const e_seed = crh(128, .{ seed, &[_]u8{ p.k, p.l } });

            @memcpy(&pk.rho, e_seed[0..32]);
            const s_seed = e_seed[32..96];
            @memcpy(&sk.key, e_seed[96..128]);
            @memcpy(&sk.rho, &pk.rho);

            sk.A = MatKxL.derive(&pk.rho);
            pk.A = sk.A;

            const s_seed_array: *const [64]u8 = s_seed[0..64];
            for (0..p.l) |i| {
                sk.s1.ps[i] = expandS(p.eta, s_seed_array, @intCast(i));
            }

            for (0..p.k) |i| {
                sk.s2.ps[i] = expandS(p.eta, s_seed_array, @intCast(p.l + i));
            }

            sk.s1_hat = sk.s1.ntt();
            sk.s2_hat = sk.s2.ntt();

            const t = computeT(sk.A, sk.s1_hat, sk.s2);

            pk.t1 = t.power2Round(&sk.t0);
            sk.t0_hat = sk.t0.ntt();
            pk.t1.packT1(&pk.t1_packed);

            // tr = H(pk) = H(rho || t1)
            const pk_bytes = pk.toBytes();
            const tr = crh(p.tr_size, .{&pk_bytes});
            sk.tr = tr;
            pk.tr = tr;

            return .{ .pk = pk, .sk = sk };
        }

        /// ML-DSA signature
        pub const Signature = struct {
            /// Size of the encoded signature in bytes
            pub const encoded_length: usize = p.ctilde_size +
                polyLeGamma1PackedSize() * p.l + p.omega + p.k;

            c_tilde: [p.ctilde_size]u8, // Challenge hash
            z: PolyVecL, // Response vector
            hint: PolyVecK, // Hint vector

            /// Encode signature to bytes
            pub fn toBytes(self: Signature) [encoded_length]u8 {
                var out: [encoded_length]u8 = undefined;
                var offset: usize = 0;

                @memcpy(out[offset .. offset + p.ctilde_size], &self.c_tilde);
                offset += p.ctilde_size;

                self.z.packLeGamma1(p.gamma1_bits, out[offset .. offset + polyLeGamma1PackedSize() * p.l]);
                offset += polyLeGamma1PackedSize() * p.l;

                _ = self.hint.packHint(p.omega, out[offset..]);

                return out;
            }

            /// Decode signature from bytes
            pub fn fromBytes(bytes: [encoded_length]u8) EncodingError!Signature {
                var sig: Signature = undefined;
                var offset: usize = 0;

                @memcpy(&sig.c_tilde, bytes[offset .. offset + p.ctilde_size]);
                offset += p.ctilde_size;

                sig.z = PolyVecL.unpackLeGamma1(p.gamma1_bits, bytes[offset .. offset + polyLeGamma1PackedSize() * p.l]);
                offset += polyLeGamma1PackedSize() * p.l;

                // Validate ||z||_inf < gamma1 - beta per FIPS 204
                if (sig.z.exceeds(gamma1 - beta)) {
                    return error.InvalidEncoding;
                }

                sig.hint = PolyVecK.unpackHint(p.omega, bytes[offset..]) orelse return error.InvalidEncoding;

                return sig;
            }

            pub const VerifyError = Verifier.InitError || Verifier.VerifyError;

            /// Verify this signature against a message and public key.
            /// Returns an error if the signature is invalid.
            pub fn verify(
                sig: Signature,
                msg: []const u8,
                public_key: PublicKey,
            ) VerifyError!void {
                return sig.verifyWithContext(msg, public_key, "");
            }

            /// Verify this signature against a message and public key with context.
            /// Returns an error if the signature is invalid.
            /// The context parameter is an optional context string (max 255 bytes).
            pub fn verifyWithContext(
                sig: Signature,
                msg: []const u8,
                public_key: PublicKey,
                context: []const u8,
            ) VerifyError!void {
                if (context.len > 255) {
                    return error.SignatureVerificationFailed;
                }

                var h = sha3.Shake256.init(.{});
                h.update(&public_key.tr);
                h.update(&[_]u8{0}); // Domain separator: 0 for pure ML-DSA
                h.update(&[_]u8{@intCast(context.len)});
                if (context.len > 0) {
                    h.update(context);
                }
                h.update(msg);
                var mu: [64]u8 = undefined;
                h.squeeze(&mu);

                const z_hat = sig.z.ntt();
                const Az = public_key.A.mulVecHat(z_hat);

                // Compute w' ≈ Az - 2^d·c·t1 (approximate w used in signing)
                var Az2dct1 = public_key.t1.mulBy2toD();
                Az2dct1 = Az2dct1.ntt();
                const c_poly = sampleInBall(p.tau, &sig.c_tilde);
                const c_hat = c_poly.ntt();
                for (0..p.k) |i| {
                    Az2dct1.ps[i] = Az2dct1.ps[i].mulHat(c_hat);
                }
                Az2dct1 = Az.sub(Az2dct1);
                Az2dct1 = Az2dct1.reduceLe2Q();
                Az2dct1 = Az2dct1.invNTT();
                Az2dct1 = Az2dct1.normalizeAssumingLe2Q();

                // Apply hints to recover high bits w1'
                var w1_prime = Az2dct1.useHint(sig.hint, p.gamma2);
                var w1_packed: [polyW1PackedSize() * p.k]u8 = undefined;
                w1_prime.packW1(p.gamma1_bits, &w1_packed);

                const c_prime = crh(p.ctilde_size, .{ &mu, &w1_packed });

                if (!mem.eql(u8, &c_prime, &sig.c_tilde)) {
                    return error.SignatureVerificationFailed;
                }
            }

            /// Create a Verifier for incrementally verifying a signature.
            pub fn verifier(self: Signature, public_key: PublicKey) !Verifier {
                return self.verifierWithContext(public_key, "");
            }

            /// Create a Verifier for incrementally verifying a signature with context.
            /// The context parameter is an optional context string (max 255 bytes).
            pub fn verifierWithContext(self: Signature, public_key: PublicKey, context: []const u8) ContextTooLongError!Verifier {
                return Verifier.init(self, public_key, context);
            }
        };

        /// A Signer is used to incrementally compute a signature over a streamed message.
        /// It can be obtained from a `SecretKey` or `KeyPair`, using the `signer()` function.
        pub const Signer = struct {
            h: sha3.Shake256, // For computing μ = CRH(tr || msg)
            secret_key: *const SecretKey,
            rnd: [32]u8,

            /// Initialize a new Signer.
            /// The noise parameter can be null for deterministic signatures,
            /// or provide randomness for hedged signatures (recommended for fault attack resistance).
            /// The context parameter is an optional context string (max 255 bytes).
            pub fn init(secret_key: *const SecretKey, noise: ?[noise_length]u8, context: []const u8) ContextTooLongError!Signer {
                if (context.len > 255) {
                    return error.ContextTooLong;
                }

                var h = sha3.Shake256.init(.{});
                h.update(&secret_key.tr);
                h.update(&[_]u8{0}); // Domain separator: 0 for pure ML-DSA
                h.update(&[_]u8{@intCast(context.len)});
                if (context.len > 0) {
                    h.update(context);
                }

                return Signer{
                    .h = h,
                    .secret_key = secret_key,
                    .rnd = noise orelse .{0} ** 32,
                };
            }

            /// Add new data to the message being signed.
            pub fn update(self: *Signer, data: []const u8) void {
                self.h.update(data);
            }

            /// Compute a signature over the entire message.
            pub fn finalize(self: *Signer) Signature {
                var mu: [64]u8 = undefined;
                self.h.squeeze(&mu);

                const rho_prime = crh(64, .{ &self.secret_key.key, &self.rnd, &mu });

                var sig: Signature = undefined;
                var y_nonce: u16 = 0;

                // Rejection sampling loop (FIPS 204 Algorithm 2, steps 5-16)
                var attempt: u32 = 0;
                while (true) {
                    attempt += 1;
                    if (attempt >= 576) { // (6/7)⁵⁷⁶ < 2⁻¹²⁸
                        @branchHint(.unlikely);
                        unreachable;
                    }

                    const y = PolyVecL.deriveUniformLeGamma1(p.gamma1_bits, &rho_prime, y_nonce);
                    y_nonce += @intCast(p.l);

                    const y_hat = y.ntt();
                    var w = self.secret_key.A.mulVec(y_hat);

                    w = w.normalize();
                    var w0: PolyVecK = undefined;
                    const w1 = w.decomposeVec(p.gamma2, &w0);
                    var w1_packed: [polyW1PackedSize() * p.k]u8 = undefined;
                    w1.packW1(p.gamma1_bits, &w1_packed);

                    sig.c_tilde = crh(p.ctilde_size, .{ &mu, &w1_packed });

                    const c_poly = sampleInBall(p.tau, &sig.c_tilde);
                    const c_hat = c_poly.ntt();

                    // Rejection check: ensure masking is effective
                    var w0mcs2: PolyVecK = undefined;
                    for (0..p.k) |i| {
                        w0mcs2.ps[i] = c_hat.mulHat(self.secret_key.s2_hat.ps[i]);
                        w0mcs2.ps[i] = w0mcs2.ps[i].invNTT();
                    }
                    w0mcs2 = w0.sub(w0mcs2);
                    w0mcs2 = w0mcs2.normalize();

                    if (w0mcs2.exceeds(p.gamma2 - beta)) {
                        continue;
                    }

                    // Compute response z = y + c·s1
                    for (0..p.l) |i| {
                        sig.z.ps[i] = c_hat.mulHat(self.secret_key.s1_hat.ps[i]);
                        sig.z.ps[i] = sig.z.ps[i].invNTT();
                    }
                    sig.z = sig.z.add(y);
                    sig.z = sig.z.normalize();

                    if (sig.z.exceeds(gamma1 - beta)) {
                        continue;
                    }

                    var ct0: PolyVecK = undefined;
                    for (0..p.k) |i| {
                        ct0.ps[i] = c_hat.mulHat(self.secret_key.t0_hat.ps[i]);
                        ct0.ps[i] = ct0.ps[i].invNTT();
                    }
                    ct0 = ct0.reduceLe2Q();
                    ct0 = ct0.normalize();

                    if (ct0.exceeds(p.gamma2)) {
                        continue;
                    }

                    // Generate hints for verification
                    var w0mcs2pct0 = w0mcs2.add(ct0);
                    w0mcs2pct0 = w0mcs2pct0.reduceLe2Q();
                    w0mcs2pct0 = w0mcs2pct0.normalizeAssumingLe2Q();
                    const hint_result = PolyVecK.makeHintVec(w0mcs2pct0, w1, p.gamma2);
                    if (hint_result.pop > p.omega) {
                        continue;
                    }
                    sig.hint = hint_result.hint;

                    return sig;
                }
            }
        };

        /// A Verifier is used to incrementally verify a signature over a streamed message.
        /// It can be obtained from a `Signature`, using the `verifier()` function.
        pub const Verifier = struct {
            h: sha3.Shake256, // For computing μ = CRH(tr || msg)
            signature: Signature,
            public_key: PublicKey,

            pub const InitError = EncodingError;
            pub const VerifyError = SignatureVerificationError;

            /// Initialize a new Verifier.
            /// The context parameter is an optional context string (max 255 bytes).
            pub fn init(signature: Signature, public_key: PublicKey, context: []const u8) ContextTooLongError!Verifier {
                if (context.len > 255) {
                    return error.ContextTooLong;
                }

                var h = sha3.Shake256.init(.{});
                h.update(&public_key.tr);
                h.update(&[_]u8{0}); // Domain separator: 0 for pure ML-DSA
                h.update(&[_]u8{@intCast(context.len)}); // Context length
                if (context.len > 0) {
                    h.update(context);
                }

                return Verifier{
                    .h = h,
                    .signature = signature,
                    .public_key = public_key,
                };
            }

            /// Add new content to the message to be verified.
            pub fn update(self: *Verifier, data: []const u8) void {
                self.h.update(data);
            }

            /// Verify that the signature is valid for the entire message.
            pub fn verify(self: *Verifier) SignatureVerificationError!void {
                var mu: [64]u8 = undefined;
                self.h.squeeze(&mu);

                const z_hat = self.signature.z.ntt();
                const Az = self.public_key.A.mulVecHat(z_hat);

                // Compute w' ≈ Az - 2^d·c·t1 (approximate w used in signing)
                var Az2dct1 = self.public_key.t1.mulBy2toD();
                Az2dct1 = Az2dct1.ntt();
                const c_poly = sampleInBall(p.tau, &self.signature.c_tilde);
                const c_hat = c_poly.ntt();
                for (0..p.k) |i| {
                    Az2dct1.ps[i] = Az2dct1.ps[i].mulHat(c_hat);
                }
                Az2dct1 = Az.sub(Az2dct1);
                Az2dct1 = Az2dct1.reduceLe2Q();
                Az2dct1 = Az2dct1.invNTT();
                Az2dct1 = Az2dct1.normalizeAssumingLe2Q();

                // Apply hints to recover high bits w1'
                var w1_prime = Az2dct1.useHint(self.signature.hint, p.gamma2);
                var w1_packed: [polyW1PackedSize() * p.k]u8 = undefined;
                w1_prime.packW1(p.gamma1_bits, &w1_packed);

                const c_prime = crh(p.ctilde_size, .{ &mu, &w1_packed });

                if (!mem.eql(u8, &c_prime, &self.signature.c_tilde)) {
                    return error.SignatureVerificationFailed;
                }
            }
        };

        /// A key pair consisting of a secret key and its corresponding public key.
        pub const KeyPair = struct {
            /// Length (in bytes) of a seed required to create a key pair.
            pub const seed_length = Self.seed_length;

            /// The public key component.
            public_key: PublicKey,

            /// The secret key component.
            secret_key: SecretKey,

            /// Generate a new random key pair.
            pub fn generate(io: std.Io) KeyPair {
                var seed: [Self.seed_length]u8 = undefined;
                io.random(&seed);
                return generateDeterministic(seed) catch unreachable;
            }

            /// Generate a key pair deterministically from a seed.
            /// Use for testing or when reproducibility is required.
            /// The seed should be generated using a cryptographically secure random source.
            pub fn generateDeterministic(seed: [32]u8) !KeyPair {
                const keys = newKeyFromSeed(&seed);
                return .{
                    .public_key = keys.pk,
                    .secret_key = keys.sk,
                };
            }

            /// Derive the public key from an existing secret key.
            /// This recomputes the public key components from the secret key.
            pub fn fromSecretKey(sk: SecretKey) !KeyPair {
                var pk: PublicKey = undefined;
                pk.rho = sk.rho;
                pk.tr = sk.tr;
                pk.A = sk.A;

                const t = computeT(sk.A, sk.s1_hat, sk.s2);

                var t0: PolyVecK = undefined;
                pk.t1 = t.power2Round(&t0);
                pk.t1.packT1(&pk.t1_packed);

                return .{
                    .public_key = pk,
                    .secret_key = sk,
                };
            }

            /// Create a Signer for incrementally signing a message.
            /// The noise parameter can be null for deterministic signatures,
            /// or provide randomness for hedged signatures (recommended for fault attack resistance).
            pub fn signer(self: *const KeyPair, noise: ?[noise_length]u8) !Signer {
                return self.secret_key.signer(noise);
            }

            /// Create a Signer for incrementally signing a message with context.
            /// The noise parameter can be null for deterministic signatures,
            /// or provide randomness for hedged signatures (recommended for fault attack resistance).
            /// The context parameter is an optional context string (max 255 bytes).
            pub fn signerWithContext(self: *const KeyPair, noise: ?[noise_length]u8, context: []const u8) ContextTooLongError!Signer {
                return self.secret_key.signerWithContext(noise, context);
            }

            /// Sign a message using this key pair.
            /// The noise parameter can be null for deterministic signatures,
            /// or provide randomness for hedged signatures (recommended for fault attack resistance).
            pub fn sign(
                kp: KeyPair,
                msg: []const u8,
                noise: ?[noise_length]u8,
            ) !Signature {
                return kp.signWithContext(msg, noise, "");
            }

            /// Sign a message using this key pair with context.
            /// The noise parameter can be null for deterministic signatures,
            /// or provide randomness for hedged signatures (recommended for fault attack resistance).
            /// The context parameter is an optional context string (max 255 bytes).
            pub fn signWithContext(
                kp: KeyPair,
                msg: []const u8,
                noise: ?[noise_length]u8,
                context: []const u8,
            ) ContextTooLongError!Signature {
                var st = try kp.signerWithContext(noise, context);
                st.update(msg);
                return st.finalize();
            }
        };
    };
}

test "modular arithmetic" {
    // Test Montgomery reduction
    const x: u64 = 12345678;
    const y = montReduceLe2Q(x);
    try testing.expect(y < 2 * Q);

    // Test modQ
    try testing.expectEqual(@as(u32, 0), modQ(Q));
    try testing.expectEqual(@as(u32, 1), modQ(Q + 1));
}

test "polynomial operations" {
    var p1 = Poly.zero;
    p1.cs[0] = 1;
    p1.cs[1] = 2;

    var p2 = Poly.zero;
    p2.cs[0] = 3;
    p2.cs[1] = 4;

    const p3 = p1.add(p2);
    try testing.expectEqual(@as(u32, 4), p3.cs[0]);
    try testing.expectEqual(@as(u32, 6), p3.cs[1]);
}

test "NTT and inverse NTT" {
    // Create a test polynomial in REGULAR FORM (not Montgomery)
    var p = Poly.zero;
    for (0..N) |i| {
        p.cs[i] = @intCast(i % Q);
    }

    // Apply NTT then inverse NTT
    // According to Dilithium spec: NTT followed by invNTT multiplies by R
    // So result will be p * R (i.e., p in Montgomery form)
    var p_ntt = p.ntt();

    // Reduce before invNTT (as Go test does)
    p_ntt = p_ntt.reduceLe2Q();

    const p_restored = p_ntt.invNTT();

    // Reduce and normalize
    const p_reduced = p_restored.reduceLe2Q();
    const p_norm = p_reduced.normalize();

    // Check if we get p * R (which equals toMont(p))
    for (0..N) |i| {
        const original: u32 = @intCast(i % Q);
        const expected = toMont(original);
        const expected_norm = modQ(expected);
        try testing.expectEqual(expected_norm, p_norm.cs[i]);
    }
}

test "parameter set instantiation" {
    // Just verify we can instantiate all three parameter sets
    const ml44 = MLDSA44;
    const ml65 = MLDSA65;
    const ml87 = MLDSA87;

    try testing.expectEqualStrings("ML-DSA-44", ml44.name);
    try testing.expectEqualStrings("ML-DSA-65", ml65.name);
    try testing.expectEqualStrings("ML-DSA-87", ml87.name);
}

test "compare zetas with Go implementation" {
    // First 16 zetas from Go implementation (in Montgomery form)
    const go_zetas = [16]u32{
        4193792, 25847,   5771523, 7861508, 237124,  7602457, 7504169,
        466468,  1826347, 2353451, 8021166, 6288512, 3119733, 5495562,
        3111497, 2680103,
    };

    // Compare our computed zetas with Go's
    for (0..16) |i| {
        try testing.expectEqual(go_zetas[i], zetas[i]);
    }
}

test "NTT with simple polynomial" {
    // Test with a very simple polynomial: just one coefficient set to 1 in regular form
    var p = Poly.zero;
    p.cs[0] = 1;

    var p_ntt = p.ntt();

    // Reduce before invNTT (as Go test does)
    p_ntt = p_ntt.reduceLe2Q();

    const p_restored = p_ntt.invNTT();

    // Result should be 1 * R = toMont(1) in Montgomery form
    const p_reduced = p_restored.reduceLe2Q();
    const p_norm = p_reduced.normalize();

    const expected = modQ(toMont(1));
    try testing.expectEqual(expected, p_norm.cs[0]);

    // All other coefficients should be 0 * R = 0
    for (1..N) |i| {
        try testing.expectEqual(@as(u32, 0), p_norm.cs[i]);
    }
}

test "Montgomery reduction correctness" {
    // Test that Montgomery reduction works correctly
    // montReduceLe2Q(a * b * R) = a * b mod q (where a, b are in Montgomery form)

    const x: u32 = 12345;
    const y: u32 = 67890;

    // Convert to Montgomery form
    const x_mont = toMont(x);
    const y_mont = toMont(y);

    // Multiply in Montgomery form
    const product_mont = montReduceLe2Q(@as(u64, x_mont) * @as(u64, y_mont));

    // Convert back from Montgomery form
    const product = montReduceLe2Q(@as(u64, product_mont));

    // Direct multiplication mod q
    const expected = modQ(@as(u32, @intCast((@as(u64, x) * @as(u64, y)) % Q)));

    try testing.expectEqual(expected, modQ(product));
}

// Removed debug test - was causing noise in output

test "compare inv_zetas with Go implementation" {
    // First 16 inv_zetas from Go implementation
    const go_inv_zetas = [16]u32{
        6403635, 846154,  6979993, 4442679, 1362209, 48306,   4460757,
        554416,  3545687, 6767575, 976891,  8196974, 2286327, 420899,
        2235985, 2939036,
    };

    // Compare our computed inv_zetas with Go's
    for (0..16) |i| {
        if (inv_zetas[i] != go_inv_zetas[i]) {
            std.debug.print("Mismatch at inv_zetas[{d}]: got {d}, expected {d}\n", .{ i, inv_zetas[i], go_inv_zetas[i] });
        }
        try testing.expectEqual(go_inv_zetas[i], inv_zetas[i]);
    }
}

test "power2Round correctness" {
    // Test that power2Round correctly splits values
    // For all a in [0, Q), we should have a = a1*2^D + a0
    // where -2^(D-1) < a0 <= 2^(D-1)

    // Test a few specific values
    const test_values = [_]u32{ 0, 1, Q / 2, Q - 1, 12345, 8380416 };

    for (test_values) |a| {
        if (a >= Q) continue;

        const result = power2Round(a);
        const a0 = @as(i32, @bitCast(result.a0_plus_q -% Q));
        const a1 = result.a1;

        // Check reconstruction: a = a1*2^D + a0
        const reconstructed = @as(i32, @bitCast(a1 << D)) + a0;
        try testing.expectEqual(@as(i32, @bitCast(a)), reconstructed);

        // Check a0 bounds: -2^(D-1) < a0 <= 2^(D-1)
        const bound: i32 = 1 << (D - 1);
        try testing.expect(a0 > -bound and a0 <= bound);
    }
}

test "decompose correctness for ML-DSA-65" {
    // Test decompose with gamma2 = 95232 (ML-DSA-44)
    const gamma2 = 95232;
    const alpha = 2 * gamma2;

    const test_values = [_]u32{ 0, 1, Q / 2, Q - 1, 12345 };

    for (test_values) |a| {
        if (a >= Q) continue;

        const result = decompose(a, gamma2);
        const a0 = @as(i32, @bitCast(result.a0_plus_q -% Q));
        const a1 = result.a1;

        // Check reconstruction: a = a1*alpha + a0 (mod Q)
        var reconstructed: i64 = @as(i64, @intCast(a1)) * @as(i64, @intCast(alpha)) + @as(i64, a0);
        reconstructed = @mod(reconstructed, @as(i64, Q));
        try testing.expectEqual(@as(i64, @intCast(a)), reconstructed);

        // Check a0 bounds (approximately)
        const bound: i32 = @intCast(alpha / 2);
        try testing.expect(@abs(a0) <= bound);
    }
}

test "decompose correctness for ML-DSA-87" {
    // Test decompose with gamma2 = 261888 (ML-DSA-65 and ML-DSA-87)
    const gamma2 = 261888;
    const alpha = 2 * gamma2;

    const test_values = [_]u32{ 0, 1, Q / 2, Q - 1, 12345 };

    for (test_values) |a| {
        if (a >= Q) continue;

        const result = decompose(a, gamma2);
        const a0 = @as(i32, @bitCast(result.a0_plus_q -% Q));
        const a1 = result.a1;

        // Check reconstruction: a = a1*alpha + a0 (mod Q)
        var reconstructed: i64 = @as(i64, @intCast(a1)) * @as(i64, @intCast(alpha)) + @as(i64, a0);
        reconstructed = @mod(reconstructed, @as(i64, Q));
        try testing.expectEqual(@as(i64, @intCast(a)), reconstructed);

        // Check a0 bounds (approximately)
        const bound: i32 = @intCast(alpha / 2);
        try testing.expect(@abs(a0) <= bound);
    }
}

test "polyDeriveUniform deterministic" {
    // Test that polyDeriveUniform produces deterministic results
    const seed: [32]u8 = .{0x01} ++ .{0x00} ** 31;
    const nonce: u16 = 0;

    const p1 = polyDeriveUniform(&seed, nonce);
    const p2 = polyDeriveUniform(&seed, nonce);

    // Should be identical
    for (0..N) |i| {
        try testing.expectEqual(p1.cs[i], p2.cs[i]);
    }

    // All coefficients should be in [0, Q)
    for (0..N) |i| {
        try testing.expect(p1.cs[i] < Q);
    }
}

test "polyDeriveUniform different nonces" {
    // Test that different nonces produce different polynomials
    const seed: [32]u8 = .{0x01} ++ .{0x00} ** 31;

    const p1 = polyDeriveUniform(&seed, 0);
    const p2 = polyDeriveUniform(&seed, 1);

    // Should be different
    var different = false;
    for (0..N) |i| {
        if (p1.cs[i] != p2.cs[i]) {
            different = true;
            break;
        }
    }
    try testing.expect(different);
}

test "expandS with eta=2" {
    // Test eta=2 sampling
    const seed: [64]u8 = .{0x02} ++ .{0x00} ** 63;
    const nonce: u16 = 0;

    const p = expandS(2, &seed, nonce);

    // All coefficients should be in [Q-eta, Q+eta]
    // The function returns coefficients as Q + eta - t, where t is in [0, 2*eta]
    // So coefficients are in [Q-eta, Q+eta]
    for (0..N) |i| {
        const c = p.cs[i];
        // Check that c is in [Q-2, Q+2]
        try testing.expect(c >= Q - 2 and c <= Q + 2);
    }
}

test "expandS with eta=4" {
    // Test eta=4 sampling
    const seed: [64]u8 = .{0x03} ++ .{0x00} ** 63;
    const nonce: u16 = 0;

    const p = expandS(4, &seed, nonce);

    // All coefficients should be in [Q-eta, Q+eta]
    for (0..N) |i| {
        const c = p.cs[i];
        // Check bounds (coefficients are around Q ± eta)
        const diff = if (c >= Q) c - Q else Q - c;
        try testing.expect(diff <= 4);
    }
}

test "sampleInBall has correct weight" {
    // Test that ball polynomial has exactly tau non-zero coefficients
    const tau = 39; // From ML-DSA-44
    const seed: [32]u8 = .{0x04} ++ .{0x00} ** 31;

    const p = sampleInBall(tau, &seed);

    // Count non-zero coefficients
    var count: u32 = 0;
    for (0..N) |i| {
        if (p.cs[i] != 0) {
            count += 1;
            // Non-zero coefficients should be 1 or Q-1
            try testing.expect(p.cs[i] == 1 or p.cs[i] == Q - 1);
        }
    }

    try testing.expectEqual(tau, count);
}

test "sampleInBall deterministic" {
    // Test that ball sampling is deterministic
    const tau = 49; // From ML-DSA-65
    const seed: [32]u8 = .{0x05} ++ .{0x00} ** 31;

    const p1 = sampleInBall(tau, &seed);
    const p2 = sampleInBall(tau, &seed);

    // Should be identical
    for (0..N) |i| {
        try testing.expectEqual(p1.cs[i], p2.cs[i]);
    }
}

test "polyPackLeqEta / polyUnpackLeqEta roundtrip for eta=2" {
    // Test packing and unpacking for eta=2
    const eta = 2;

    // Create a test polynomial with coefficients in [Q-eta, Q+eta]
    var p = Poly.zero;
    for (0..N) |i| {
        // Use various values in range
        const val = @as(u32, @intCast(i % 5)); // 0, 1, 2, 3, 4
        p.cs[i] = Q + eta - val;
    }

    // Pack it
    var buf: [96]u8 = undefined; // eta=2: 3 bits per coeff = 96 bytes
    polyPackLeqEta(p, eta, &buf);

    // Unpack it
    const p2 = polyUnpackLeqEta(eta, &buf);

    // Should be identical
    for (0..N) |i| {
        try testing.expectEqual(p.cs[i], p2.cs[i]);
    }
}

test "polyPackLeqEta / polyUnpackLeqEta roundtrip for eta=4" {
    // Test packing and unpacking for eta=4
    const eta = 4;

    // Create a test polynomial with coefficients in [Q-eta, Q+eta]
    var p = Poly.zero;
    for (0..N) |i| {
        // Use various values in range
        const val = @as(u32, @intCast(i % 9)); // 0, 1, 2, ..., 8
        p.cs[i] = Q + eta - val;
    }

    // Pack it
    var buf: [128]u8 = undefined; // eta=4: 4 bits per coeff = 128 bytes
    polyPackLeqEta(p, eta, &buf);

    // Unpack it
    const p2 = polyUnpackLeqEta(eta, &buf);

    // Should be identical
    for (0..N) |i| {
        try testing.expectEqual(p.cs[i], p2.cs[i]);
    }
}

test "polyPackT1 / polyUnpackT1 roundtrip" {
    // Create a test polynomial with coefficients < 1024
    var p = Poly.zero;
    for (0..N) |i| {
        p.cs[i] = @intCast(i % 1024);
    }

    // Pack it
    var buf: [320]u8 = undefined; // (256 * 10) / 8 = 320 bytes
    polyPackT1(p, &buf);

    // Unpack it
    const p2 = polyUnpackT1(&buf);

    // Should be identical
    for (0..N) |i| {
        try testing.expectEqual(p.cs[i], p2.cs[i]);
    }
}

test "polyPackT0 / polyUnpackT0 roundtrip" {
    // Create a test polynomial with coefficients in (Q-2^12, Q+2^12]
    // This is the range (-2^12, 2^12] represented as unsigned around Q
    const bound = 1 << 12; // 2^(D-1) where D=13
    var p = Poly.zero;
    for (0..N) |i| {
        // Cycle through valid range for T0
        // Values should be Q + offset where offset is in (-bound, bound]
        const cycle_val = @as(i32, @intCast(i % (2 * bound))); // 0 to 2*bound-1
        const offset = cycle_val - bound + 1; // (-bound+1) to bound
        p.cs[i] = @as(u32, @intCast(@as(i32, Q) + offset));
    }

    // Pack it
    var buf: [416]u8 = undefined; // (256 * 13) / 8 = 416 bytes
    polyPackT0(p, &buf);

    // Unpack it
    const p2 = polyUnpackT0(&buf);

    // Should be identical
    for (0..N) |i| {
        try testing.expectEqual(p.cs[i], p2.cs[i]);
    }
}

test "polyPackLeGamma1 / polyUnpackLeGamma1 roundtrip gamma1_bits=17" {
    const gamma1_bits = 17;
    const gamma1: u32 = @as(u32, 1) << gamma1_bits;

    // Create a test polynomial with coefficients in (-gamma1, gamma1]
    // Normalized: [0, gamma1] ∪ (Q-gamma1, Q)
    var p = Poly.zero;
    for (0..N) |i| {
        if (i % 2 == 0) {
            // Positive values: [0, gamma1]
            p.cs[i] = @intCast((i / 2) % (gamma1 + 1));
        } else {
            // Negative values: (Q-gamma1, Q)
            const neg_val: u32 = @intCast(((i / 2) % gamma1) + 1);
            p.cs[i] = Q - neg_val;
        }
    }

    // Pack it
    var buf: [576]u8 = undefined; // (256 * 18) / 8 = 576 bytes
    polyPackLeGamma1(p, gamma1_bits, &buf);

    // Unpack it
    const p2 = polyUnpackLeGamma1(gamma1_bits, &buf);

    // Should be identical
    for (0..N) |i| {
        try testing.expectEqual(p.cs[i], p2.cs[i]);
    }
}

test "polyPackLeGamma1 / polyUnpackLeGamma1 roundtrip gamma1_bits=19" {
    const gamma1_bits = 19;
    const gamma1: u32 = @as(u32, 1) << gamma1_bits;

    // Create a test polynomial with coefficients in (-gamma1, gamma1]
    var p = Poly.zero;
    for (0..N) |i| {
        if (i % 2 == 0) {
            // Positive values: [0, gamma1]
            p.cs[i] = @intCast((i / 2) % (gamma1 + 1));
        } else {
            // Negative values: (Q-gamma1, Q)
            const neg_val: u32 = @intCast(((i / 2) % gamma1) + 1);
            p.cs[i] = Q - neg_val;
        }
    }

    // Pack it
    var buf: [640]u8 = undefined; // (256 * 20) / 8 = 640 bytes
    polyPackLeGamma1(p, gamma1_bits, &buf);

    // Unpack it
    const p2 = polyUnpackLeGamma1(gamma1_bits, &buf);

    // Should be identical
    for (0..N) |i| {
        try testing.expectEqual(p.cs[i], p2.cs[i]);
    }
}

test "polyPackW1 for gamma1_bits=17" {
    const gamma1_bits = 17;

    // Create a test polynomial with small coefficients (w1 values < 64)
    var p = Poly.zero;
    for (0..N) |i| {
        p.cs[i] = @intCast(i % 64); // 6-bit values
    }

    // Pack it
    var buf: [192]u8 = undefined; // (256 * 6) / 8 = 192 bytes
    polyPackW1(p, gamma1_bits, &buf);

    // Verify basic properties
    // All bytes should be used
    var non_zero = false;
    for (buf) |b| {
        if (b != 0) {
            non_zero = true;
            break;
        }
    }
    try testing.expect(non_zero);
}

test "polyPackW1 for gamma1_bits=19" {
    const gamma1_bits = 19;

    // Create a test polynomial with small coefficients (w1 values < 16)
    var p = Poly.zero;
    for (0..N) |i| {
        p.cs[i] = @intCast(i % 16); // 4-bit values
    }

    // Pack it
    var buf: [128]u8 = undefined; // (256 * 4) / 8 = 128 bytes
    polyPackW1(p, gamma1_bits, &buf);

    // Verify basic properties
    var non_zero = false;
    for (buf) |b| {
        if (b != 0) {
            non_zero = true;
            break;
        }
    }
    try testing.expect(non_zero);
}

test "makeHint and useHint correctness for gamma2=261888" {
    // Test for ML-DSA-65 and ML-DSA-87
    const gamma2: u32 = 261888;

    // Test a selection of values to verify the hint mechanism works
    const test_values = [_]u32{ 0, 100, 1000, 10000, 100000, 1000000, Q / 2, Q - 1 };

    for (test_values) |w| {
        // Decompose w to get w0 and w1
        const decomp = decompose(w, gamma2);
        const w0_plus_q = decomp.a0_plus_q;
        const w1 = decomp.a1;

        // Test with various small perturbations f in [0, gamma2]
        const perturbations = [_]u32{ 0, 1, 10, 100, 1000, gamma2 / 2, gamma2 };

        for (perturbations) |f| {
            // Test f (positive perturbation)
            const z0_pos = (w0_plus_q +% Q -% f) % Q;
            const hint_pos = makeHint(z0_pos, w1, gamma2);
            const w_perturbed_pos = (w +% Q -% f) % Q;
            const w1_recovered_pos = useHint(w_perturbed_pos, hint_pos, gamma2);
            try testing.expectEqual(w1, w1_recovered_pos);

            // Test -f (negative perturbation)
            if (f > 0) {
                const z0_neg = (w0_plus_q +% f) % Q;
                const hint_neg = makeHint(z0_neg, w1, gamma2);
                const w_perturbed_neg = (w +% f) % Q;
                const w1_recovered_neg = useHint(w_perturbed_neg, hint_neg, gamma2);
                try testing.expectEqual(w1, w1_recovered_neg);
            }
        }
    }
}

test "makeHint and useHint correctness for gamma2=95232" {
    // Test for ML-DSA-44
    const gamma2: u32 = 95232;

    // Test a selection of values to verify the hint mechanism works
    const test_values = [_]u32{ 0, 100, 1000, 10000, 100000, 1000000, Q / 2, Q - 1 };

    for (test_values) |w| {
        // Decompose w to get w0 and w1
        const decomp = decompose(w, gamma2);
        const w0_plus_q = decomp.a0_plus_q;
        const w1 = decomp.a1;

        // Test with various small perturbations f in [0, gamma2]
        const perturbations = [_]u32{ 0, 1, 10, 100, 1000, gamma2 / 2, gamma2 };

        for (perturbations) |f| {
            // Test f (positive perturbation)
            const z0_pos = (w0_plus_q +% Q -% f) % Q;
            const hint_pos = makeHint(z0_pos, w1, gamma2);
            const w_perturbed_pos = (w +% Q -% f) % Q;
            const w1_recovered_pos = useHint(w_perturbed_pos, hint_pos, gamma2);
            try testing.expectEqual(w1, w1_recovered_pos);

            // Test -f (negative perturbation)
            if (f > 0) {
                const z0_neg = (w0_plus_q +% f) % Q;
                const hint_neg = makeHint(z0_neg, w1, gamma2);
                const w_perturbed_neg = (w +% f) % Q;
                const w1_recovered_neg = useHint(w_perturbed_neg, hint_neg, gamma2);
                try testing.expectEqual(w1, w1_recovered_neg);
            }
        }
    }
}

test "polyMakeHint basic functionality" {
    const gamma2: u32 = 261888;

    // Create test polynomials
    var p0 = Poly.zero;
    var p1 = Poly.zero;

    // Fill with test values
    for (0..N) |i| {
        p0.cs[i] = @intCast((i * 17) % Q);
        p1.cs[i] = @intCast((i * 3) % 16); // High bits are at most 15 for gamma2=261888
    }

    // Make hints
    const result = polyMakeHint(p0, p1, gamma2);
    const hint = result.hint;
    const count = result.count;

    // Verify that hints are binary
    for (0..N) |i| {
        try testing.expect(hint.cs[i] == 0 or hint.cs[i] == 1);
    }

    // Verify that count matches the number of 1s in hint
    var actual_count: u32 = 0;
    for (0..N) |i| {
        actual_count += hint.cs[i];
    }
    try testing.expectEqual(count, actual_count);
}

test "polyUseHint reconstruction" {
    const gamma2: u32 = 261888;

    // Create a test polynomial q
    var q = Poly.zero;
    for (0..N) |i| {
        q.cs[i] = @intCast((i * 123) % Q);
    }

    // Decompose q to get high and low bits
    var q0_plus_q_array: [N]u32 = undefined;
    var q1_array: [N]u32 = undefined;
    for (0..N) |i| {
        const decomp = decompose(q.cs[i], gamma2);
        q0_plus_q_array[i] = decomp.a0_plus_q;
        q1_array[i] = decomp.a1;
    }

    const q0_plus_q = Poly{ .cs = q0_plus_q_array };
    const q1 = Poly{ .cs = q1_array };

    // Create hints (in this case, they'll mostly be 0 since q and q are the same)
    const hint_result = polyMakeHint(q0_plus_q, q1, gamma2);
    const hint = hint_result.hint;

    // Use hints to recover high bits
    const recovered = polyUseHint(q, hint, gamma2);

    // Recovered should match original high bits q1
    for (0..N) |i| {
        try testing.expectEqual(q1.cs[i], recovered.cs[i]);
    }
}

test "hint roundtrip with perturbation" {
    const gamma2: u32 = 261888;

    // Create a test polynomial w
    var w = Poly.zero;
    for (0..N) |i| {
        w.cs[i] = @intCast((i * 7919) % Q);
    }

    // Decompose w to get w0 and w1
    var w0_plus_q = Poly.zero;
    var w1 = Poly.zero;
    for (0..N) |i| {
        const decomp = decompose(w.cs[i], gamma2);
        w0_plus_q.cs[i] = decomp.a0_plus_q;
        w1.cs[i] = decomp.a1;
    }

    // Apply a small perturbation
    var f = Poly.zero;
    for (0..N) |i| {
        // Small perturbation in [-gamma2, gamma2]
        const f_val = @as(u32, @intCast(i % 1000));
        f.cs[i] = if (i % 2 == 0) f_val else Q -% f_val;
    }

    // Compute w' = w - f and z0 = w0 - f
    var w_prime = Poly.zero;
    var z0 = Poly.zero;
    for (0..N) |i| {
        w_prime.cs[i] = (w.cs[i] +% Q -% f.cs[i]) % Q;
        z0.cs[i] = (w0_plus_q.cs[i] +% Q -% f.cs[i]) % Q;
    }

    // Make hints
    const hint_result = polyMakeHint(z0, w1, gamma2);
    const hint = hint_result.hint;

    // Use hints to recover w1 from w_prime
    const w1_recovered = polyUseHint(w_prime, hint, gamma2);

    // Verify that we recovered the original high bits
    for (0..N) |i| {
        try testing.expectEqual(w1.cs[i], w1_recovered.cs[i]);
    }
}

// Parameterized test helper for key generation

fn testKeyGenerationBasic(comptime MlDsa: type, seed: [32]u8) !void {
    const result = MlDsa.newKeyFromSeed(&seed);
    const pk = result.pk;
    const sk = result.sk;

    // Basic sanity checks
    try testing.expect(pk.rho.len == 32);
    try testing.expect(sk.rho.len == 32);
    try testing.expectEqualSlices(u8, &pk.rho, &sk.rho);

    // Verify tr matches between pk and sk
    try testing.expectEqualSlices(u8, &pk.tr, &sk.tr);

    // Test toBytes/fromBytes round-trip for public key
    const pk_bytes = pk.toBytes();
    const pk2 = try MlDsa.PublicKey.fromBytes(pk_bytes);
    try testing.expectEqualSlices(u8, &pk.rho, &pk2.rho);
    try testing.expectEqualSlices(u8, &pk.tr, &pk2.tr);

    // Test toBytes/fromBytes round-trip for secret key
    const sk_bytes = sk.toBytes();
    const sk2 = try MlDsa.SecretKey.fromBytes(sk_bytes);
    try testing.expectEqualSlices(u8, &sk.rho, &sk2.rho);
    try testing.expectEqualSlices(u8, &sk.key, &sk2.key);
    try testing.expectEqualSlices(u8, &sk.tr, &sk2.tr);
}

test "Key generation basic - all variants" {
    inline for (.{
        .{ .variant = MLDSA44, .seed_byte = 0x44 },
        .{ .variant = MLDSA65, .seed_byte = 0x65 },
        .{ .variant = MLDSA87, .seed_byte = 0x87 },
    }) |config| {
        const seed = [_]u8{config.seed_byte} ** 32;
        try testKeyGenerationBasic(config.variant, seed);
    }
}

test "Key generation determinism" {
    const seed = [_]u8{ 0x12, 0x34, 0x56, 0x78 } ++ [_]u8{0xAB} ** 28;

    // Generate two key pairs from the same seed
    const result1 = MLDSA44.newKeyFromSeed(&seed);
    const result2 = MLDSA44.newKeyFromSeed(&seed);

    // They should be identical
    const pk_bytes1 = result1.pk.toBytes();
    const pk_bytes2 = result2.pk.toBytes();
    try testing.expectEqualSlices(u8, &pk_bytes1, &pk_bytes2);

    const sk_bytes1 = result1.sk.toBytes();
    const sk_bytes2 = result2.sk.toBytes();
    try testing.expectEqualSlices(u8, &sk_bytes1, &sk_bytes2);
}

test "Private key can compute public key" {
    const seed = [_]u8{0xFF} ** 32;
    const result = MLDSA44.newKeyFromSeed(&seed);
    const pk = result.pk;
    const sk = result.sk;

    // Compute public key from private key
    const pk_from_sk = sk.public();

    // Pack both public keys and compare
    const pk_bytes1 = pk.toBytes();
    const pk_bytes2 = pk_from_sk.toBytes();

    try testing.expectEqualSlices(u8, &pk_bytes1, &pk_bytes2);
}

// Parameterized test helper for sign and verify
fn testSignAndVerify(comptime MlDsa: type, seed: [32]u8, message: []const u8) !void {
    const result = MlDsa.newKeyFromSeed(&seed);
    const kp = try MlDsa.KeyPair.fromSecretKey(result.sk);

    // Sign the message
    const sig = try kp.sign(message, null);

    // Verify the signature
    try sig.verify(message, kp.public_key);
}

test "Sign and verify - all variants" {
    inline for (.{
        .{ .variant = MLDSA44, .seed_byte = 0x44, .message = "Hello, ML-DSA-44!" },
        .{ .variant = MLDSA65, .seed_byte = 0x65, .message = "Hello, ML-DSA-65!" },
        .{ .variant = MLDSA87, .seed_byte = 0x87, .message = "Hello, ML-DSA-87!" },
    }) |config| {
        const seed = [_]u8{config.seed_byte} ** 32;
        try testSignAndVerify(config.variant, seed, config.message);
    }
}

test "Invalid signature rejection" {
    const seed = [_]u8{0x99} ** 32;
    const result = MLDSA44.newKeyFromSeed(&seed);
    const kp = try MLDSA44.KeyPair.fromSecretKey(result.sk);

    const message = "Original message";

    // Sign the message
    const sig = try kp.sign(message, null);

    // Verify with wrong message should fail
    const wrong_message = "Modified message";
    try testing.expectError(error.SignatureVerificationFailed, sig.verify(wrong_message, kp.public_key));

    // Modify signature and verify should fail
    var corrupted_sig_bytes = sig.toBytes();
    corrupted_sig_bytes[0] ^= 0xFF;
    const corrupted_sig = try MLDSA44.Signature.fromBytes(corrupted_sig_bytes);
    try testing.expectError(error.SignatureVerificationFailed, corrupted_sig.verify(message, kp.public_key));
}

test "Context string support" {
    const seed = [_]u8{0xAA} ** 32;
    const result = MLDSA44.newKeyFromSeed(&seed);
    const kp = try MLDSA44.KeyPair.fromSecretKey(result.sk);

    const message = "Test message";
    const context1 = "context1";
    const context2 = "context2";

    // Sign with context1
    const sig1 = try kp.signWithContext(message, null, context1);

    // Verify with correct context should succeed
    try sig1.verifyWithContext(message, kp.public_key, context1);

    // Verify with wrong context should fail
    try testing.expectError(error.SignatureVerificationFailed, sig1.verifyWithContext(message, kp.public_key, context2));

    // Verify with empty context should fail
    try testing.expectError(error.SignatureVerificationFailed, sig1.verify(message, kp.public_key));

    // Sign with empty context
    const sig2 = try kp.sign(message, null);

    // Verify with empty context should succeed
    try sig2.verify(message, kp.public_key);

    // Verify with non-empty context should fail
    try testing.expectError(error.SignatureVerificationFailed, sig2.verifyWithContext(message, kp.public_key, context1));

    // Test maximum context length (255 bytes)
    const max_context = [_]u8{0xBB} ** 255;
    const sig3 = try kp.signWithContext(message, null, &max_context);
    try sig3.verifyWithContext(message, kp.public_key, &max_context);

    // Test context too long (256 bytes should fail)
    const too_long_context = [_]u8{0xCC} ** 256;
    try testing.expectError(error.ContextTooLong, kp.signWithContext(message, null, &too_long_context));
}

test "Context string with streaming API" {
    const seed = [_]u8{0xDD} ** 32;
    const result = MLDSA44.newKeyFromSeed(&seed);
    const kp = try MLDSA44.KeyPair.fromSecretKey(result.sk);

    const context = "streaming-context";
    const message_part1 = "Hello, ";
    const message_part2 = "World!";

    // Sign using streaming API with context
    var signer = try kp.signerWithContext(null, context);
    signer.update(message_part1);
    signer.update(message_part2);
    const sig = signer.finalize();

    // Verify using streaming API with context
    var verifier = try sig.verifierWithContext(kp.public_key, context);
    verifier.update(message_part1);
    verifier.update(message_part2);
    try verifier.verify();

    // Verify with wrong context should fail
    var verifier_wrong = try sig.verifierWithContext(kp.public_key, "wrong");
    verifier_wrong.update(message_part1);
    verifier_wrong.update(message_part2);
    try testing.expectError(error.SignatureVerificationFailed, verifier_wrong.verify());
}

test "Signature determinism (same rnd)" {
    const seed = [_]u8{0x11} ** 32;
    const result = MLDSA44.newKeyFromSeed(&seed);
    const sk = result.sk;

    const message = "Deterministic test";
    const rnd = [_]u8{0x22} ** 32;

    // Sign twice with same randomness using streaming API
    var st1 = try sk.signer(rnd);
    st1.update(message);
    const sig1 = st1.finalize();

    var st2 = try sk.signer(rnd);
    st2.update(message);
    const sig2 = st2.finalize();

    // Signatures should be identical
    try testing.expectEqualSlices(u8, &sig1.toBytes(), &sig2.toBytes());
}

test "Signature toBytes/fromBytes roundtrip" {
    const seed = [_]u8{0x33} ** 32;
    const result = MLDSA44.newKeyFromSeed(&seed);
    const kp = try MLDSA44.KeyPair.fromSecretKey(result.sk);

    const message = "toBytes/fromBytes test";

    // Sign the message
    const sig = try kp.sign(message, null);
    const sig_bytes = sig.toBytes();

    // Unpack and repack
    const sig_reparsed = try MLDSA44.Signature.fromBytes(sig_bytes);

    const repacked = sig_reparsed.toBytes();

    // Should match original
    try testing.expectEqualSlices(u8, &sig_bytes, &repacked);
}

test "Empty message signing" {
    const seed = [_]u8{0x44} ** 32;
    const result = MLDSA44.newKeyFromSeed(&seed);
    const kp = try MLDSA44.KeyPair.fromSecretKey(result.sk);

    const message = "";

    // Sign empty message
    const sig = try kp.sign(message, null);

    // Verify should work
    try sig.verify(message, kp.public_key);
}

test "Long message signing" {
    const seed = [_]u8{0x55} ** 32;
    const result = MLDSA44.newKeyFromSeed(&seed);
    const kp = try MLDSA44.KeyPair.fromSecretKey(result.sk);

    // Create a long message (1KB)
    const long_message = [_]u8{0xAB} ** 1024;

    // Sign long message
    const sig = try kp.sign(&long_message, null);

    // Verify should work
    try sig.verify(&long_message, kp.public_key);
}

// Helper function to decode hex string into bytes
fn hexToBytes(comptime hex: []const u8, out: []u8) !void {
    if (hex.len != out.len * 2) return error.InvalidLength;

    var i: usize = 0;
    while (i < out.len) : (i += 1) {
        const hi = try std.fmt.charToDigit(hex[i * 2], 16);
        const lo = try std.fmt.charToDigit(hex[i * 2 + 1], 16);
        out[i] = (hi << 4) | lo;
    }
}

test "ML-DSA-44 KAT test vector 0" {
    // Test vector from NIST ML-DSA KAT (count = 0)
    // xi is the seed for key generation (Algorithm 1, line 1)
    const xi_hex = "f696484048ec21f96cf50a56d0759c448f3779752f0383d37449690694cf7a68";
    const pk_hex_start = "bd4e96f9a038ab5e36214fe69c0b1cb835ef9d7c8417e76aecd152f5cddebec8";
    const msg_hex = "6dbbc4375136df3b07f7c70e639e223e";

    // Parse xi (32-byte seed for key generation)
    var xi: [32]u8 = undefined;
    try hexToBytes(xi_hex, &xi);

    // Generate keys from xi
    const result = MLDSA44.newKeyFromSeed(&xi);
    const pk = result.pk;
    const sk = result.sk;

    // Verify public key starts with expected bytes
    const pk_bytes = pk.toBytes();

    var expected_pk_start: [32]u8 = undefined;
    try hexToBytes(pk_hex_start, &expected_pk_start);

    // Check first 32 bytes of public key match
    try testing.expectEqualSlices(u8, &expected_pk_start, pk_bytes[0..32]);

    // Parse message
    var msg: [16]u8 = undefined;
    try hexToBytes(msg_hex, &msg);

    // Sign the message (deterministic mode with fixed randomness)
    const kp = try MLDSA44.KeyPair.fromSecretKey(sk);
    const sig = try kp.sign(&msg, null);

    // Verify the signature
    try sig.verify(&msg, kp.public_key);
}

test "ML-DSA-65 KAT test vector 0" {
    // Test vector from NIST ML-DSA KAT (count = 0)
    // xi is the seed for key generation (Algorithm 1, line 1)
    const xi_hex = "f696484048ec21f96cf50a56d0759c448f3779752f0383d37449690694cf7a68";
    const pk_hex_start = "e50d03fff3b3a70961abbb92a390008dec1283f603f50cdbaaa3d00bd659bc76";
    const msg_hex = "6dbbc4375136df3b07f7c70e639e223e";

    // Parse xi (32-byte seed for key generation)
    var xi: [32]u8 = undefined;
    try hexToBytes(xi_hex, &xi);

    // Generate keys from xi
    const result = MLDSA65.newKeyFromSeed(&xi);
    const pk = result.pk;
    const sk = result.sk;

    // Verify public key starts with expected bytes
    const pk_bytes = pk.toBytes();

    var expected_pk_start: [32]u8 = undefined;
    try hexToBytes(pk_hex_start, &expected_pk_start);

    // Check first 32 bytes of public key match
    try testing.expectEqualSlices(u8, &expected_pk_start, pk_bytes[0..32]);

    // Parse message
    var msg: [16]u8 = undefined;
    try hexToBytes(msg_hex, &msg);

    // Sign the message
    const kp = try MLDSA65.KeyPair.fromSecretKey(sk);
    const sig = try kp.sign(&msg, null);

    // Verify the signature
    try sig.verify(&msg, kp.public_key);
}

test "ML-DSA-87 KAT test vector 0" {
    // Test vector from NIST ML-DSA KAT (count = 0)
    // xi is the seed for key generation (Algorithm 1, line 1)
    const xi_hex = "f696484048ec21f96cf50a56d0759c448f3779752f0383d37449690694cf7a68";
    const pk_hex_start = "bc89b367d4288f47c71a74679d0fcffbe041de41b5da2f5fc66d8e28c5899494";
    const msg_hex = "6dbbc4375136df3b07f7c70e639e223e";

    // Parse xi (32-byte seed for key generation)
    var xi: [32]u8 = undefined;
    try hexToBytes(xi_hex, &xi);

    // Generate keys from xi
    const result = MLDSA87.newKeyFromSeed(&xi);
    const pk = result.pk;
    const sk = result.sk;

    // Verify public key starts with expected bytes
    const pk_bytes = pk.toBytes();

    var expected_pk_start: [32]u8 = undefined;
    try hexToBytes(pk_hex_start, &expected_pk_start);

    // Check first 32 bytes of public key match
    try testing.expectEqualSlices(u8, &expected_pk_start, pk_bytes[0..32]);

    // Parse message
    var msg: [16]u8 = undefined;
    try hexToBytes(msg_hex, &msg);

    // Sign the message
    const kp = try MLDSA87.KeyPair.fromSecretKey(sk);
    const sig = try kp.sign(&msg, null);

    // Verify the signature
    try sig.verify(&msg, kp.public_key);
}

test "KeyPair API - generate and sign" {
    const io = std.testing.io;
    // Test the new KeyPair API with random generation
    const kp = MLDSA44.KeyPair.generate(io);
    const msg = "Test message for KeyPair API";

    // Sign with deterministic mode (no noise)
    const sig = try kp.sign(msg, null);

    // Verify using Signature.verify API
    try sig.verify(msg, kp.public_key);
}

test "KeyPair API - generateDeterministic" {
    // Test deterministic key generation
    const seed = [_]u8{42} ** 32;
    const kp1 = try MLDSA44.KeyPair.generateDeterministic(seed);
    const kp2 = try MLDSA44.KeyPair.generateDeterministic(seed);

    // Same seed should produce same keys
    const pk1_bytes = kp1.public_key.toBytes();
    const pk2_bytes = kp2.public_key.toBytes();
    try testing.expectEqualSlices(u8, &pk1_bytes, &pk2_bytes);
}

test "KeyPair API - fromSecretKey" {
    const io = std.testing.io;
    // Generate a key pair
    const kp1 = MLDSA44.KeyPair.generate(io);

    // Derive public key from secret key
    const kp2 = try MLDSA44.KeyPair.fromSecretKey(kp1.secret_key);

    // Public keys should match
    const pk1_bytes = kp1.public_key.toBytes();
    const pk2_bytes = kp2.public_key.toBytes();
    try testing.expectEqualSlices(u8, &pk1_bytes, &pk2_bytes);
}

test "Signature verification with noise" {
    const io = std.testing.io;
    // Test signing with randomness (hedged signatures)
    const kp = MLDSA65.KeyPair.generate(io);
    const msg = "Message to be signed with randomness";

    // Create some noise
    const noise = [_]u8{ 1, 2, 3, 4, 5 } ++ [_]u8{0} ** 27;

    // Sign with noise
    const sig = try kp.sign(msg, noise);

    // Verify should still work
    try sig.verify(msg, kp.public_key);
}

test "Signature verification failure" {
    const io = std.testing.io;
    // Test that invalid signatures are rejected
    const kp = MLDSA44.KeyPair.generate(io);
    const msg = "Original message";
    const sig = try kp.sign(msg, null);

    // Verify with wrong message should fail
    const wrong_msg = "Different message";
    try testing.expectError(error.SignatureVerificationFailed, sig.verify(wrong_msg, kp.public_key));
}

test "Streaming API - sign and verify" {
    const seed = [_]u8{0x55} ** 32;
    const kp = try MLDSA44.KeyPair.generateDeterministic(seed);

    const msg = "Test message for streaming API";

    // Sign using streaming API
    var signer = try kp.signer(null);
    signer.update(msg);
    const sig = signer.finalize();

    // Verify using streaming API
    var verifier = try sig.verifier(kp.public_key);
    verifier.update(msg);
    try verifier.verify();
}

test "Streaming API - chunked message" {
    const seed = [_]u8{0x66} ** 32;
    const kp = try MLDSA44.KeyPair.generateDeterministic(seed);

    // Create a message in chunks
    const chunk1 = "Hello, ";
    const chunk2 = "streaming ";
    const chunk3 = "world!";
    const full_msg = chunk1 ++ chunk2 ++ chunk3;

    // Sign with chunks
    var signer = try kp.signer(null);
    signer.update(chunk1);
    signer.update(chunk2);
    signer.update(chunk3);
    const sig_chunked = signer.finalize();

    // Sign with full message for comparison
    var signer2 = try kp.signer(null);
    signer2.update(full_msg);
    const sig_full = signer2.finalize();

    // Signatures should be identical
    try testing.expectEqualSlices(u8, &sig_chunked.toBytes(), &sig_full.toBytes());

    // Verify with chunks
    const sig = sig_chunked;
    var verifier = try sig.verifier(kp.public_key);
    verifier.update(chunk1);
    verifier.update(chunk2);
    verifier.update(chunk3);
    try verifier.verify();
}

test "Streaming API - large message" {
    const seed = [_]u8{0x77} ** 32;
    const kp = try MLDSA44.KeyPair.generateDeterministic(seed);

    // Create a large message (1MB)
    const chunk_size = 4096;
    const num_chunks = 256;
    var chunk: [chunk_size]u8 = undefined;
    for (0..chunk_size) |i| {
        chunk[i] = @intCast(i % 256);
    }

    // Sign streaming
    var signer = try kp.signer(null);
    for (0..num_chunks) |_| {
        signer.update(&chunk);
    }
    const sig = signer.finalize();

    // Verify streaming
    var verifier = try sig.verifier(kp.public_key);
    for (0..num_chunks) |_| {
        verifier.update(&chunk);
    }
    try verifier.verify();
}

test "Streaming API - all parameter sets" {
    const test_msg = "Streaming test for all ML-DSA parameter sets";

    // ML-DSA-44
    {
        const seed = [_]u8{0x44} ** 32;
        const kp = try MLDSA44.KeyPair.generateDeterministic(seed);
        var signer = try kp.signer(null);
        signer.update(test_msg);
        const sig = signer.finalize();
        var verifier = try sig.verifier(kp.public_key);
        verifier.update(test_msg);
        try verifier.verify();
    }

    // ML-DSA-65
    {
        const seed = [_]u8{0x65} ** 32;
        const kp = try MLDSA65.KeyPair.generateDeterministic(seed);
        var signer = try kp.signer(null);
        signer.update(test_msg);
        const sig = signer.finalize();
        var verifier = try sig.verifier(kp.public_key);
        verifier.update(test_msg);
        try verifier.verify();
    }

    // ML-DSA-87
    {
        const seed = [_]u8{0x87} ** 32;
        const kp = try MLDSA87.KeyPair.generateDeterministic(seed);
        var signer = try kp.signer(null);
        signer.update(test_msg);
        const sig = signer.finalize();
        var verifier = try sig.verifier(kp.public_key);
        verifier.update(test_msg);
        try verifier.verify();
    }
}

/// Extended Euclidian Algorithm
/// Only meant to be used on comptime values; correctness matters, performance doesn't.
fn extendedEuclidean(comptime T: type, comptime a_: T, comptime b_: T) struct { gcd: T, x: T, y: T } {
    var a = a_;
    var b = b_;
    var x0: T = 1;
    var x1: T = 0;
    var y0: T = 0;
    var y1: T = 1;

    while (b != 0) {
        const q = @divTrunc(a, b);
        const temp_a = a;
        a = b;
        b = temp_a - q * b;

        const temp_x = x0;
        x0 = x1;
        x1 = temp_x - q * x1;

        const temp_y = y0;
        y0 = y1;
        y1 = temp_y - q * y1;
    }

    return .{ .gcd = a, .x = x0, .y = y0 };
}

/// Modular inversion: computes a^(-1) mod p
/// Requires gcd(a,p) = 1. The result is normalized to the range [0, p).
fn modularInverse(comptime T: type, comptime a: T, comptime p: T) T {
    // Use a signed type for EEA computation
    const type_info = @typeInfo(T);
    const SignedT = if (type_info == .int and type_info.int.signedness == .unsigned)
        std.meta.Int(.signed, type_info.int.bits)
    else
        T;

    const a_signed = @as(SignedT, @intCast(a));
    const p_signed = @as(SignedT, @intCast(p));

    const r = extendedEuclidean(SignedT, a_signed, p_signed);
    assert(r.gcd == 1);

    // Normalize result to [0, p)
    var result = r.x;
    while (result < 0) {
        result += p_signed;
    }

    return @intCast(result);
}

/// Modular exponentiation: computes a^s mod p using square-and-multiply algorithm.
fn modularPow(comptime T: type, comptime a: T, s: T, comptime p: T) T {
    const type_info = @typeInfo(T);
    const bits = type_info.int.bits;
    const WideT = std.meta.Int(.unsigned, bits * 2);

    var ret: T = 1;
    var base: T = a;
    var exp = s;

    while (exp > 0) {
        if (exp & 1 == 1) {
            ret = @intCast((@as(WideT, ret) * @as(WideT, base)) % p);
        }
        base = @intCast((@as(WideT, base) * @as(WideT, base)) % p);
        exp >>= 1;
    }

    return ret;
}

/// Creates an all-ones or all-zeros mask from a single bit value.
/// Returns all 1s (0xFF...FF) if bit == 1, all 0s if bit == 0.
fn bitMask(comptime T: type, bit: T) T {
    const type_info = @typeInfo(T);
    if (type_info != .int or type_info.int.signedness != .unsigned) {
        @compileError("bitMask requires an unsigned integer type");
    }
    return -%bit;
}

/// Creates a mask from the sign bit of a signed integer.
/// Returns all 1s (0xFF...FF) if x < 0, all 0s if x >= 0.
fn signMask(comptime T: type, x: T) std.meta.Int(.unsigned, @typeInfo(T).int.bits) {
    const type_info = @typeInfo(T);
    if (type_info != .int) {
        @compileError("signMask requires an integer type");
    }

    const bits = type_info.int.bits;
    const SignedT = std.meta.Int(.signed, bits);

    // Convert to signed if needed, arithmetic right shift to propagate sign bit
    const x_signed: SignedT = if (type_info.int.signedness == .signed) x else @bitCast(x);
    const shifted = x_signed >> (bits - 1);
    return @bitCast(shifted);
}

/// Montgomery reduction: for input x, returns y where y ≡ x*R^(-1) (mod q).
/// This is a generic implementation parameterized by the modulus q, its inverse qInv,
/// the Montgomery constant R, and the result bound.
///
/// For ML-DSA: R = 2^32, returns y < 2q
/// For ML-KEM: R = 2^16, returns y in range (-q, q)
fn montgomeryReduce(
    comptime InT: type,
    comptime OutT: type,
    comptime q: comptime_int,
    comptime qInv: comptime_int,
    comptime r_bits: comptime_int,
    x: InT,
) OutT {
    const mask = (@as(InT, 1) << r_bits) - 1;
    const m_full = (x *% qInv) & mask;
    const m: OutT = @truncate(m_full);

    const yR = x -% @as(InT, m) * @as(InT, q);
    const y_shifted = @as(std.meta.Int(.unsigned, @typeInfo(InT).Int.bits), @bitCast(yR)) >> r_bits;
    return @bitCast(@as(std.meta.Int(.unsigned, @typeInfo(OutT).Int.bits), @truncate(y_shifted)));
}

/// Uniform sampling using SHAKE-128 with rejection sampling.
/// Samples polynomial coefficients uniformly from [0, q) using rejection sampling.
///
/// Parameters:
/// - PolyType: The polynomial type to return
/// - q: Modulus
/// - bits_per_coef: Number of bits per coefficient (12 or 23)
/// - n: Number of coefficients
/// - seed: Random seed
/// - domain_sep: Domain separation bytes (appended to seed)
fn sampleUniformRejection(
    comptime PolyType: type,
    comptime q: comptime_int,
    comptime bits_per_coef: comptime_int,
    comptime n: comptime_int,
    seed: []const u8,
    domain_sep: []const u8,
) PolyType {
    var h = sha3.Shake128.init(.{});
    h.update(seed);
    h.update(domain_sep);

    const buf_len = sha3.Shake128.block_length; // 168 bytes
    var buf: [buf_len]u8 = undefined;

    var ret: PolyType = undefined;
    var coef_idx: usize = 0;

    if (bits_per_coef == 12) {
        // ML-KEM path: pack 2 coefficients per 3 bytes (12 bits each)
        outer: while (true) {
            h.squeeze(&buf);

            var j: usize = 0;
            while (j < buf_len) : (j += 3) {
                const b0 = @as(u16, buf[j]);
                const b1 = @as(u16, buf[j + 1]);
                const b2 = @as(u16, buf[j + 2]);

                const ts: [2]u16 = .{
                    b0 | ((b1 & 0xf) << 8),
                    (b1 >> 4) | (b2 << 4),
                };

                inline for (ts) |t| {
                    if (t < q) {
                        ret.cs[coef_idx] = @intCast(t);
                        coef_idx += 1;
                        if (coef_idx == n) break :outer;
                    }
                }
            }
        }
    } else if (bits_per_coef == 23) {
        // ML-DSA path: 1 coefficient per 3 bytes (23 bits)
        while (coef_idx < n) {
            h.squeeze(&buf);

            var j: usize = 0;
            while (j < buf_len and coef_idx < n) : (j += 3) {
                const t = (@as(u32, buf[j]) |
                    (@as(u32, buf[j + 1]) << 8) |
                    (@as(u32, buf[j + 2]) << 16)) & 0x7fffff;

                if (t < q) {
                    ret.cs[coef_idx] = @intCast(t);
                    coef_idx += 1;
                }
            }
        }
    } else {
        @compileError("bits_per_coef must be 12 or 23");
    }

    return ret;
}

test "bitMask and signMask helpers" {
    try testing.expectEqual(@as(u32, 0x00000000), bitMask(u32, 0));
    try testing.expectEqual(@as(u32, 0xFFFFFFFF), bitMask(u32, 1));
    try testing.expectEqual(@as(u8, 0x00), bitMask(u8, 0));
    try testing.expectEqual(@as(u8, 0xFF), bitMask(u8, 1));
    try testing.expectEqual(@as(u64, 0x0000000000000000), bitMask(u64, 0));
    try testing.expectEqual(@as(u64, 0xFFFFFFFFFFFFFFFF), bitMask(u64, 1));

    try testing.expectEqual(@as(u32, 0xFFFFFFFF), signMask(i32, -1));
    try testing.expectEqual(@as(u32, 0xFFFFFFFF), signMask(i32, -100));
    try testing.expectEqual(@as(u32, 0x00000000), signMask(i32, 0));
    try testing.expectEqual(@as(u32, 0x00000000), signMask(i32, 1));
    try testing.expectEqual(@as(u32, 0x00000000), signMask(i32, 100));

    try testing.expectEqual(@as(u32, 0xFFFFFFFF), signMask(u32, 0x80000000)); // MSB set
    try testing.expectEqual(@as(u32, 0x00000000), signMask(u32, 0x7FFFFFFF)); // MSB clear
}



---
File: /std/crypto/ml_kem.zig
---

//! Implementation of the IND-CCA2 post-quantum secure key encapsulation mechanism (KEM)
//! ML-KEM (NIST FIPS-203 publication) and CRYSTALS-Kyber (v3.02/"draft00" CFRG draft).
//!
//! The namespace `d00` refers to the version currently implemented, in accordance with the CFRG draft.
//! The `nist` namespace refers to the FIPS-203 publication.
//!
//! Quoting from the CFRG I-D:
//!
//! Kyber is not a Diffie-Hellman (DH) style non-interactive key
//! agreement, but instead, Kyber is a Key Encapsulation Method (KEM).
//! In essence, a KEM is a Public-Key Encryption (PKE) scheme where the
//! plaintext cannot be specified, but is generated as a random key as
//! part of the encryption. A KEM can be transformed into an unrestricted
//! PKE using HPKE (RFC9180). On its own, a KEM can be used as a key
//! agreement method in TLS.
//!
//! Kyber is an IND-CCA2 secure KEM. It is constructed by applying a
//! Fujisaki--Okamato style transformation on InnerPKE, which is the
//! underlying IND-CPA secure Public Key Encryption scheme. We cannot
//! use InnerPKE directly, as its ciphertexts are malleable.
//!
//! ```
//!                     F.O. transform
//!     InnerPKE   ---------------------->   Kyber
//!     IND-CPA                              IND-CCA2
//! ```
//!
//! Kyber is a lattice-based scheme.  More precisely, its security is
//! based on the learning-with-errors-and-rounding problem in module
//! lattices (MLWER).  The underlying polynomial ring R (defined in
//! Section 5) is chosen such that multiplication is very fast using the
//! number theoretic transform (NTT, see Section 5.1.3).
//!
//! An InnerPKE private key is a vector _s_ over R of length k which is
//! _small_ in a particular way.  Here k is a security parameter akin to
//! the size of a prime modulus.  For Kyber512, which targets AES-128's
//! security level, the value of k is 2.
//!
//! The public key consists of two values:
//!
//! * _A_ a uniformly sampled k by k matrix over R _and_
//!
//! * _t = A s + e_, where e is a suitably small masking vector.
//!
//! Distinguishing between such A s + e and a uniformly sampled t is the
//! module learning-with-errors (MLWE) problem.  If that is hard, then it
//! is also hard to recover the private key from the public key as that
//! would allow you to distinguish between those two.
//!
//! To save space in the public key, A is recomputed deterministically
//! from a seed _rho_.
//!
//! A ciphertext for a message m under this public key is a pair (c_1,
//! c_2) computed roughly as follows:
//!
//! c_1 = Compress(A^T r + e_1, d_u)
//! c_2 = Compress(t^T r + e_2 + Decompress(m, 1), d_v)
//!
//! where
//!
//! * e_1, e_2 and r are small blinds;
//!
//! * Compress(-, d) removes some information, leaving d bits per
//!   coefficient and Decompress is such that Compress after Decompress
//!   does nothing and
//!
//! * d_u, d_v are scheme parameters.
//!
//! Distinguishing such a ciphertext and uniformly sampled (c_1, c_2) is
//! an example of the full MLWER problem, see section 4.4 of [KyberV302].
//!
//! To decrypt the ciphertext, one computes
//!
//! m = Compress(Decompress(c_2, d_v) - s^T Decompress(c_1, d_u), 1).
//!
//! It it not straight-forward to see that this formula is correct.  In
//! fact, there is negligible but non-zero probability that a ciphertext
//! does not decrypt correctly given by the DFP column in Table 4.  This
//! failure probability can be computed by a careful automated analysis
//! of the probabilities involved, see kyber_failure.py of [SecEst].
//!
//! [KyberV302](https://pq-crystals.org/kyber/data/kyber-specification-round3-20210804.pdf)
//! [I-D](https://github.com/bwesterb/draft-schwabe-cfrg-kyber)
//! [SecEst](https://github.com/pq-crystals/security-estimates)

// TODO
//
// - The bottleneck in Kyber are the various hash/xof calls:
//    - Optimize Zig's keccak implementation.
//    - Use SIMD to compute keccak in parallel.
// - Can we track bounds of coefficients using comptime types without
//   duplicating code?
// - Would be neater to have tests closer to the thing under test.
// - When generating a keypair, we have a copy of the inner public key with
//   its large matrix A in both the public key and the private key. In Go we
//   can just have a pointer in the private key to the public key, but
//   how do we do this elegantly in Zig?

const std = @import("std");
const builtin = @import("builtin");

const testing = std.testing;
const assert = std.debug.assert;
const crypto = std.crypto;
const errors = std.crypto.errors;
const math = std.math;
const mem = std.mem;
const sha3 = crypto.hash.sha3;

const RndGen = std.Random.DefaultPrng;

// Q is the modulus q ≡ 3329 = 2¹¹ + 2¹⁰ + 2⁸ + 1
const Q: i16 = 3329;

// Montgomery R = 2^16 mod Q (for Montgomery multiplication)
const R: i32 = 1 << 16;

// N is the degree of polynomials (polynomial ring dimension)
const N: usize = 256;

// eta2 is the size of "small" vectors used in encryption blinds
const eta2: u8 = 2;

const Params = struct {
    name: []const u8,

    // NIST ML-KEM variant instead of Kyber as originally submitted.
    ml_kem: bool = false,

    // Width and height of the matrix A.
    k: u8,

    // Size of "small" vectors used in private key and encryption blinds.
    eta1: u8,

    // How many bits to retain of u, the private-key independent part
    // of the ciphertext.
    du: u8,

    // How many bits to retain of v, the private-key dependent part
    // of the ciphertext.
    dv: u8,
};

pub const d00 = struct {
    pub const Kyber512 = Kyber(.{
        .name = "Kyber512",
        .k = 2,
        .eta1 = 3,
        .du = 10,
        .dv = 4,
    });

    pub const Kyber768 = Kyber(.{
        .name = "Kyber768",
        .k = 3,
        .eta1 = 2,
        .du = 10,
        .dv = 4,
    });

    pub const Kyber1024 = Kyber(.{
        .name = "Kyber1024",
        .k = 4,
        .eta1 = 2,
        .du = 11,
        .dv = 5,
    });
};

pub const nist = struct {
    pub const MLKem512 = Kyber(.{
        .name = "ML-KEM-512",
        .ml_kem = true,
        .k = 2,
        .eta1 = 3,
        .du = 10,
        .dv = 4,
    });

    pub const MLKem768 = Kyber(.{
        .name = "ML-KEM-768",
        .ml_kem = true,
        .k = 3,
        .eta1 = 2,
        .du = 10,
        .dv = 4,
    });

    pub const MLKem1024 = Kyber(.{
        .name = "ML-KEM-1024",
        .ml_kem = true,
        .k = 4,
        .eta1 = 2,
        .du = 11,
        .dv = 5,
    });
};

const modes = [_]type{
    d00.Kyber512,
    d00.Kyber768,
    d00.Kyber1024,
    nist.MLKem512,
    nist.MLKem768,
    nist.MLKem1024,
};
const h_length: usize = 32;
const inner_seed_length: usize = 32;
const common_encaps_seed_length: usize = 32;
const common_shared_key_size: usize = 32;

fn Kyber(comptime p: Params) type {
    return struct {
        // Size of a ciphertext, in bytes.
        pub const ciphertext_length = Poly.compressedSize(p.du) * p.k + Poly.compressedSize(p.dv);

        const Self = @This();
        const V = PolyVec(p.k);
        const M = Mat(p.k);

        /// Length (in bytes) of a shared secret.
        pub const shared_length = common_shared_key_size;
        /// Length (in bytes) of a seed for deterministic encapsulation.
        pub const encaps_seed_length = common_encaps_seed_length;
        /// Length (in bytes) of a seed for key generation.
        pub const seed_length: usize = inner_seed_length + shared_length;
        /// Algorithm name.
        pub const name = p.name;

        /// A shared secret, and an encapsulated (encrypted) representation of it.
        pub const EncapsulatedSecret = struct {
            shared_secret: [shared_length]u8,
            ciphertext: [ciphertext_length]u8,
        };

        /// A Kyber public key.
        pub const PublicKey = struct {
            pk: InnerPk,

            // Cached
            hpk: [h_length]u8, // H(pk)

            /// Size of a serialized representation of the key, in bytes.
            pub const encoded_length = InnerPk.encoded_length;

            /// Generates a shared secret, encapsulated for the public key,
            /// using random bytes.
            ///
            /// This is recommended over `encapsDeterministic`.
            pub fn encaps(pk: PublicKey, io: std.Io) EncapsulatedSecret {
                var m: [inner_plaintext_length]u8 = undefined;
                io.random(&m);
                return encapsInner(pk, &m);
            }

            /// Generates a shared secret, encapsulated for the public key,
            /// using the provided seed.
            ///
            /// Calling `encaps` instead is recommended.
            pub fn encapsDeterministic(pk: PublicKey, seed: *const [encaps_seed_length]u8) EncapsulatedSecret {
                var m: [inner_plaintext_length]u8 = undefined;
                if (p.ml_kem) {
                    @memcpy(&m, seed);
                } else {
                    // m = H(seed)
                    sha3.Sha3_256.hash(seed, &m, .{});
                }
                return encapsInner(pk, &m);
            }

            fn encapsInner(pk: PublicKey, m: *[inner_plaintext_length]u8) EncapsulatedSecret {
                // (K', r) = G(m ‖ H(pk))
                var kr: [inner_plaintext_length + h_length]u8 = undefined;
                var g = sha3.Sha3_512.init(.{});
                g.update(m);
                g.update(&pk.hpk);
                g.final(&kr);

                // c = innerEncrypt(pk, m, r)
                const ct = pk.pk.encrypt(m, kr[32..64]);

                if (p.ml_kem) {
                    return EncapsulatedSecret{
                        .shared_secret = kr[0..shared_length].*, // ML-KEM: K = K'
                        .ciphertext = ct,
                    };
                } else {
                    // Compute H(c) and put in second slot of kr, which will be (K', H(c)).
                    sha3.Sha3_256.hash(&ct, kr[32..], .{});

                    var ss: [shared_length]u8 = undefined;
                    sha3.Shake256.hash(&kr, &ss, .{});
                    return EncapsulatedSecret{
                        .shared_secret = ss, // Kyber: K = KDF(K' ‖ H(c))
                        .ciphertext = ct,
                    };
                }
            }

            /// Serializes the key into a byte array.
            pub fn toBytes(pk: PublicKey) [encoded_length]u8 {
                return pk.pk.toBytes();
            }

            /// Deserializes the key from a byte array.
            pub fn fromBytes(buf: *const [encoded_length]u8) errors.NonCanonicalError!PublicKey {
                var ret: PublicKey = undefined;
                ret.pk = try InnerPk.fromBytes(buf[0..InnerPk.encoded_length]);
                sha3.Sha3_256.hash(buf, &ret.hpk, .{});
                return ret;
            }
        };

        /// A Kyber secret key.
        pub const SecretKey = struct {
            sk: InnerSk,
            pk: InnerPk,
            hpk: [h_length]u8, // H(pk)
            z: [shared_length]u8,

            /// Size of a serialized representation of the key, in bytes.
            pub const encoded_length: usize =
                InnerSk.encoded_length + InnerPk.encoded_length + h_length + shared_length;

            /// Decapsulates the shared secret within ct using the private key.
            pub fn decaps(sk: SecretKey, ct: *const [ciphertext_length]u8) ![shared_length]u8 {
                // m' = innerDec(ct)
                const m2 = sk.sk.decrypt(ct);

                // (K'', r') = G(m' ‖ H(pk))
                var kr2: [64]u8 = undefined;
                var g = sha3.Sha3_512.init(.{});
                g.update(&m2);
                g.update(&sk.hpk);
                g.final(&kr2);

                // ct' = innerEnc(pk, m', r')
                const ct2 = sk.pk.encrypt(&m2, kr2[32..64]);

                if (p.ml_kem) {
                    // ML-KEM: K = K'' if ct == ct', else K = J(z || c) per FIPS 203
                    var k_bar: [shared_length]u8 = undefined;
                    var j = sha3.Shake256.init(.{});
                    j.update(&sk.z);
                    j.update(ct);
                    j.squeeze(&k_bar);
                    cmov(shared_length, kr2[0..shared_length], k_bar, ctneq(ciphertext_length, ct.*, ct2));
                    return kr2[0..shared_length].*;
                } else {
                    // Kyber: K = KDF(K''/z ‖ H(c))
                    sha3.Sha3_256.hash(ct, kr2[32..], .{});
                    cmov(32, kr2[0..32], sk.z, ctneq(ciphertext_length, ct.*, ct2));
                    var ss: [shared_length]u8 = undefined;
                    sha3.Shake256.hash(&kr2, &ss, .{});
                    return ss;
                }
            }

            /// Serializes the key into a byte array.
            pub fn toBytes(sk: SecretKey) [encoded_length]u8 {
                return sk.sk.toBytes() ++ sk.pk.toBytes() ++ sk.hpk ++ sk.z;
            }

            /// Deserializes the key from a byte array.
            pub fn fromBytes(buf: *const [encoded_length]u8) errors.NonCanonicalError!SecretKey {
                var ret: SecretKey = undefined;
                comptime var s: usize = 0;
                ret.sk = InnerSk.fromBytes(buf[s .. s + InnerSk.encoded_length]);
                s += InnerSk.encoded_length;
                ret.pk = try InnerPk.fromBytes(buf[s .. s + InnerPk.encoded_length]);
                s += InnerPk.encoded_length;
                ret.hpk = buf[s..][0..h_length].*;
                s += h_length;
                ret.z = buf[s..][0..shared_length].*;
                return ret;
            }
        };

        /// A Kyber key pair.
        pub const KeyPair = struct {
            secret_key: SecretKey,
            public_key: PublicKey,

            /// Deterministically derive a key pair from a cryptograpically secure secret seed.
            ///
            /// Except in tests, applications should generally call `generate()` instead of this function.
            pub fn generateDeterministic(seed: [seed_length]u8) !KeyPair {
                var ret: KeyPair = undefined;

                // Generate inner key
                innerKeyFromSeed(
                    seed[0..inner_seed_length].*,
                    &ret.public_key.pk,
                    &ret.secret_key.sk,
                );
                ret.secret_key.pk = ret.public_key.pk;

                // Copy over z from seed.
                ret.secret_key.z = seed[inner_seed_length..seed_length].*;

                // Compute H(pk)
                sha3.Sha3_256.hash(&ret.public_key.pk.toBytes(), &ret.secret_key.hpk, .{});
                ret.public_key.hpk = ret.secret_key.hpk;

                return ret;
            }

            /// Generate a new, random key pair.
            pub fn generate(io: std.Io) KeyPair {
                var random_seed: [seed_length]u8 = undefined;
                while (true) {
                    io.random(&random_seed);
                    return generateDeterministic(random_seed) catch {
                        @branchHint(.unlikely);
                        continue;
                    };
                }
            }
        };

        // Size of plaintexts of the in
        const inner_plaintext_length: usize = Poly.compressedSize(1);

        const InnerPk = struct {
            rho: [32]u8, // ρ, the seed for the matrix A
            th: V, // NTT(t), normalized

            // Cached values
            aT: M,

            const encoded_length = V.encoded_length + 32;

            fn encrypt(
                pk: InnerPk,
                pt: *const [inner_plaintext_length]u8,
                seed: *const [32]u8,
            ) [ciphertext_length]u8 {
                // Sample r, e₁ and e₂ appropriately
                const rh = V.noise(p.eta1, 0, seed).ntt().barrettReduce();
                const e1 = V.noise(eta2, p.k, seed);
                const e2 = Poly.noise(eta2, 2 * p.k, seed);

                // Next we compute u = Aᵀ r + e₁.  First Aᵀ.
                var u: V = undefined;
                for (0..p.k) |i| {
                    // Note that coefficients of r are bounded by q and those of Aᵀ
                    // are bounded by 4.5q and so their product is bounded by 2¹⁵q
                    // as required for multiplication.
                    u.ps[i] = pk.aT.rows[i].dotHat(rh);
                }

                // Aᵀ and r were not in Montgomery form, so the Montgomery
                // multiplications in the inner product added a factor R⁻¹ which
                // the InvNTT cancels out.
                u = u.barrettReduce().invNTT().add(e1).normalize();

                // Next, compute v = <t, r> + e₂ + Decompress_q(m, 1)
                const v = pk.th.dotHat(rh).barrettReduce().invNTT()
                    .add(Poly.decompress(1, pt)).add(e2).normalize();

                return u.compress(p.du) ++ v.compress(p.dv);
            }

            fn toBytes(pk: InnerPk) [encoded_length]u8 {
                return pk.th.toBytes() ++ pk.rho;
            }

            fn fromBytes(buf: *const [encoded_length]u8) errors.NonCanonicalError!InnerPk {
                var ret: InnerPk = undefined;

                const th_bytes = buf[0..V.encoded_length];
                ret.th = V.fromBytes(th_bytes).normalize();

                if (p.ml_kem) {
                    // Verify that the coefficients used a canonical representation.
                    if (!mem.eql(u8, &ret.th.toBytes(), th_bytes)) {
                        return error.NonCanonical;
                    }
                }

                ret.rho = buf[V.encoded_length..encoded_length].*;
                ret.aT = M.uniform(ret.rho, true);
                return ret;
            }
        };

        // Private key of the inner PKE
        const InnerSk = struct {
            sh: V, // NTT(s), normalized
            const encoded_length = V.encoded_length;

            fn decrypt(sk: InnerSk, ct: *const [ciphertext_length]u8) [inner_plaintext_length]u8 {
                const u = V.decompress(p.du, ct[0..comptime V.compressedSize(p.du)]);
                const v = Poly.decompress(
                    p.dv,
                    ct[comptime V.compressedSize(p.du)..ciphertext_length],
                );

                // Compute m = v - <s, u>
                return v.sub(sk.sh.dotHat(u.ntt()).barrettReduce().invNTT())
                    .normalize().compress(1);
            }

            fn toBytes(sk: InnerSk) [encoded_length]u8 {
                return sk.sh.toBytes();
            }

            fn fromBytes(buf: *const [encoded_length]u8) InnerSk {
                var ret: InnerSk = undefined;
                ret.sh = V.fromBytes(buf).normalize();
                return ret;
            }
        };

        // Derives inner PKE keypair from given seed.
        fn innerKeyFromSeed(seed: [inner_seed_length]u8, pk: *InnerPk, sk: *InnerSk) void {
            var expanded_seed: [64]u8 = undefined;
            var h = sha3.Sha3_512.init(.{});
            h.update(&seed);
            if (p.ml_kem) h.update(&[1]u8{p.k});
            h.final(&expanded_seed);
            pk.rho = expanded_seed[0..32].*;
            const sigma = expanded_seed[32..64];
            pk.aT = M.uniform(pk.rho, false); // Expand ρ to A; we'll transpose later on

            // Sample secret vector s.
            sk.sh = V.noise(p.eta1, 0, sigma).ntt().normalize();

            const eh = PolyVec(p.k).noise(p.eta1, p.k, sigma).ntt(); // sample blind e.
            var th: V = undefined;

            // Next, we compute t = A s + e.
            for (0..p.k) |i| {
                // Note that coefficients of s are bounded by q and those of A
                // are bounded by 4.5q and so their product is bounded by 2¹⁵q
                // as required for multiplication.
                // A and s were not in Montgomery form, so the Montgomery
                // multiplications in the inner product added a factor R⁻¹ which
                // we'll cancel out with toMont().  This will also ensure the
                // coefficients of th are bounded in absolute value by q.
                th.ps[i] = pk.aT.rows[i].dotHat(sk.sh).toMont();
            }

            pk.th = th.add(eh).normalize(); // bounded by 8q
            pk.aT = pk.aT.transpose();
        }
    };
}

// R mod q
const r_mod_q: i32 = @rem(@as(i32, R), Q);

// R² mod q
const r2_mod_q: i32 = @rem(r_mod_q * r_mod_q, Q);

// ζ is the degree 256 primitive root of unity used for the NTT.
const zeta: i16 = 17;

// (128)⁻¹ R². Used in inverse NTT.
const r2_over_128: i32 = @mod(invertMod(128, Q) * r2_mod_q, Q);

// zetas lists precomputed powers of the primitive root of unity in
// Montgomery representation used for the NTT:
//
//  zetas[i] = ζᵇʳᵛ⁽ⁱ⁾ R mod q
//
// where ζ = 17, brv(i) is the bitreversal of a 7-bit number and R=2¹⁶ mod q.
const zetas = computeZetas();

// invNTTReductions keeps track of which coefficients to apply Barrett
// reduction to in Poly.invNTT().
//
// Generated lazily: once a butterfly is computed which is about to
// overflow the i16, the largest coefficient is reduced.  If that is
// not enough, the other coefficient is reduced as well.
//
// This is actually optimal, as proven in https://eprint.iacr.org/2020/1377.pdf
const inv_ntt_reductions = [_]i16{
    -1, // after layer 1
    -1, // after layer 2
    16,
    17,
    48,
    49,
    80,
    81,
    112,
    113,
    144,
    145,
    176,
    177,
    208,
    209,
    240, 241, -1, // after layer 3
    0,   1,   32,
    33,  34,  35,
    64,  65,  96,
    97,  98,  99,
    128, 129,
    160, 161, 162, 163, 192, 193, 224, 225, 226, 227, -1, // after layer 4
    2,   3,   66,  67,  68,  69,  70,  71,  130, 131, 194,
    195, 196, 197,
    198, 199, -1, // after layer 5
    4,   5,   6,
    7,   132, 133,
    134, 135, 136,
    137, 138, 139,
    140, 141,
    142, 143, -1, // after layer 6
    -1, //  after layer 7
};

test "invNTTReductions bounds" {
    // Checks whether the reductions proposed by invNTTReductions
    // don't overflow during invNTT().
    var xs = [_]i32{1} ** 256; // start at |x| ≤ q

    var r: usize = 0;
    var layer: math.Log2Int(usize) = 1;
    while (layer < 8) : (layer += 1) {
        const w = @as(usize, 1) << layer;
        var i: usize = 0;

        while (i + w < 256) {
            xs[i] = xs[i] + xs[i + w];
            try testing.expect(xs[i] <= 9); // we can't exceed 9q
            xs[i + w] = 1;
            i += 1;
            if (@mod(i, w) == 0) {
                i += w;
            }
        }

        while (true) {
            const j = inv_ntt_reductions[r];
            r += 1;
            if (j < 0) {
                break;
            }
            xs[@as(usize, @intCast(j))] = 1;
        }
    }
}

fn invertMod(a: anytype, p: @TypeOf(a)) @TypeOf(a) {
    const r = extendedEuclidean(@TypeOf(a), a, p);
    assert(r.gcd == 1);
    return r.x;
}

// Reduce mod q for testing.
fn modQ32(x: i32) i16 {
    var y = @as(i16, @intCast(@rem(x, @as(i32, Q))));
    if (y < 0) {
        y += Q;
    }
    return y;
}

// Given -2¹⁵ q ≤ x < 2¹⁵ q, returns -q < y < q with x 2⁻¹⁶ = y (mod q).
fn montReduce(x: i32) i16 {
    const qInv = comptime invertMod(@as(i32, Q), R);
    // This is Montgomery reduction with R=2¹⁶.
    //
    // Note gcd(2¹⁶, q) = 1 as q is prime.  Write q' := 62209 = q⁻¹ mod R.
    // First we compute
    //
    // m := ((x mod R) q') mod R
    //         = x q' mod R
    //    = int16(x q')
    //    = int16(int32(x) * int32(q'))
    //
    // Note that x q' might be as big as 2³² and could overflow the int32
    // multiplication in the last line.  However for any int32s a and b,
    // we have int32(int64(a)*int64(b)) = int32(a*b) and so the result is ok.
    const m: i16 = @truncate(@as(i32, @truncate(x *% qInv)));

    // Note that x - m q is divisible by R; indeed modulo R we have
    //
    //  x - m q ≡ x - x q' q ≡ x - x q⁻¹ q ≡ x - x = 0.
    //
    // We return y := (x - m q) / R.  Note that y is indeed correct as
    // modulo q we have
    //
    //  y ≡ x R⁻¹ - m q R⁻¹ = x R⁻¹
    //
    // and as both 2¹⁵ q ≤ m q, x < 2¹⁵ q, we have
    // 2¹⁶ q ≤ x - m q < 2¹⁶ and so q ≤ (x - m q) / R < q as desired.
    const yR = x - @as(i32, m) * @as(i32, Q);
    return @bitCast(@as(u16, @truncate(@as(u32, @bitCast(yR)) >> 16)));
}

test "Test montReduce" {
    var rnd = RndGen.init(0);
    for (0..1000) |_| {
        const bound = comptime @as(i32, Q) * (1 << 15);
        const x = rnd.random().intRangeLessThan(i32, -bound, bound);
        const y = montReduce(x);
        try testing.expect(-Q < y and y < Q);
        try testing.expectEqual(modQ32(x), modQ32(@as(i32, y) * R));
    }
}

// Given any x, return x R mod q where R=2¹⁶.
fn feToMont(x: i16) i16 {
    // Note |1353 x| ≤ 1353 2¹⁵ ≤ 13318 q ≤ 2¹⁵ q and so we're within
    // the bounds of montReduce.
    return montReduce(@as(i32, x) * r2_mod_q);
}

test "Test feToMont" {
    var x: i32 = -(1 << 15);
    while (x < 1 << 15) : (x += 1) {
        const y = feToMont(@as(i16, @intCast(x)));
        try testing.expectEqual(modQ32(@as(i32, y)), modQ32(x * r_mod_q));
    }
}

// Given any x, compute 0 ≤ y ≤ q with x = y (mod q).
//
// Beware: we might have feBarrettReduce(x) = q ≠ 0 for some x.  In fact,
// this happens if and only if x = -nq for some positive integer n.
fn feBarrettReduce(x: i16) i16 {
    // This is standard Barrett reduction.
    //
    // For any x we have x mod q = x - ⌊x/q⌋ q.  We will use 20159/2²⁶ as
    // an approximation of 1/q. Note that  0 ≤ 20159/2²⁶ - 1/q ≤ 0.135/2²⁶
    // and so | x 20156/2²⁶ - x/q | ≤ 2⁻¹⁰ for |x| ≤ 2¹⁶.  For all x
    // not a multiple of q, the number x/q is further than 1/q from any integer
    // and so ⌊x 20156/2²⁶⌋ = ⌊x/q⌋.  If x is a multiple of q and x is positive,
    // then x 20156/2²⁶ is larger than x/q so ⌊x 20156/2²⁶⌋ = ⌊x/q⌋ as well.
    // Finally, if x is negative multiple of q, then ⌊x 20156/2²⁶⌋ = ⌊x/q⌋-1.
    // Thus
    //                        [ q        if x=-nq for pos. integer n
    //  x - ⌊x 20156/2²⁶⌋ q = [
    //                        [ x mod q  otherwise
    //
    // To actually compute this, note that
    //
    //  ⌊x 20156/2²⁶⌋ = (20159 x) >> 26.
    return x -% @as(i16, @intCast((@as(i32, x) * 20159) >> 26)) *% Q;
}

test "Test Barrett reduction" {
    var x: i32 = -(1 << 15);
    while (x < 1 << 15) : (x += 1) {
        var y1 = feBarrettReduce(@as(i16, @intCast(x)));
        const y2 = @mod(@as(i16, @intCast(x)), Q);
        if (x < 0 and @rem(-x, Q) == 0) {
            y1 -= Q;
        }
        try testing.expectEqual(y1, y2);
    }
}

// Returns x if x < q and x - q otherwise.  Assumes x ≥ -29439.
fn csubq(x: i16) i16 {
    var r = x;
    r -= Q;
    r += (r >> 15) & Q;
    return r;
}

test "Test csubq" {
    var x: i32 = -29439;
    while (x < 1 << 15) : (x += 1) {
        const y1 = csubq(@as(i16, @intCast(x)));
        var y2 = @as(i16, @intCast(x));
        if (@as(i16, @intCast(x)) >= Q) {
            y2 -= Q;
        }
        try testing.expectEqual(y1, y2);
    }
}

// Computes zetas table used by ntt and invNTT.
fn computeZetas() [128]i16 {
    @setEvalBranchQuota(10000);
    var ret: [128]i16 = undefined;
    for (&ret, 0..) |*r, i| {
        const t = @as(i16, @intCast(modularPow(i32, zeta, @bitReverse(@as(u7, @intCast(i))), Q)));
        r.* = csubq(feBarrettReduce(feToMont(t)));
    }
    return ret;
}

// An element of our base ring R which are polynomials over ℤ_q
// modulo the equation Xᴺ = -1, where q=3329 and N=256.
//
// This type is also used to store NTT-transformed polynomials,
// see Poly.NTT().
//
// Coefficients aren't always reduced.  See Normalize().
const Poly = struct {
    cs: [N]i16,

    const encoded_length = N / 2 * 3;
    const zero: Poly = .{ .cs = .{0} ** N };

    // Add two polynomials (coefficients not normalized)
    fn add(a: Poly, b: Poly) Poly {
        var ret: Poly = undefined;
        for (0..N) |i| {
            ret.cs[i] = a.cs[i] + b.cs[i];
        }
        return ret;
    }

    // Subtract two polynomials (coefficients not normalized)
    fn sub(a: Poly, b: Poly) Poly {
        var ret: Poly = undefined;
        for (0..N)
```
