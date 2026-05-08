```
      self.update(encoded_out_len.slice());
            self.cshaker.final(out);
        }

        /// Squeeze a slice of bytes from the state.
        /// `out` can be any length, and the function can be called multiple times.
        pub fn squeeze(self: *Self, out: []u8) void {
            if (!self.xof_mode) {
                const encoded_out_len = comptime NistLengthEncoding.encode(.right, 0);
                self.update(encoded_out_len.slice());
                self.xof_mode = true;
            }
            self.cshaker.squeeze(out);
        }

        /// Return an authentication tag for a message and a key, with an optional context.
        pub fn createWithOptions(out: []u8, msg: []const u8, key: []const u8, options: Options) void {
            var ctx = Self.initWithOptions(key, options);
            ctx.update(msg);
            ctx.final(out);
        }

        /// Return an authentication tag for a message and a key.
        pub fn create(out: []u8, msg: []const u8, key: []const u8) void {
            var ctx = Self.init(key);
            ctx.update(msg);
            ctx.final(out);
        }
    };
}

/// The TupleHash extendable output hash function, with domain-separated inputs.
/// TupleHash is a secure hash function with a variable output length, based on the cSHAKE function.
/// It is designed for unambiguously hashing tuples of data.
///
/// With most hash functions, calling `update("A")` followed by `update("B")`is identical to `update("AB")`.
/// With TupleHash, this is not the case: `update("A"); update("B")` is different from `update("AB")`.
///
/// Any number of inputs can be hashed, and the output depends on individual inputs and their order.
pub fn TupleHash(comptime security_level: u11) type {
    return TupleHashLike(security_level, 0x04, 24);
}

fn TupleHashLike(comptime security_level: u11, comptime default_delim: u8, comptime rounds: u5) type {
    const CShaker = CShakeLike(security_level, default_delim, rounds, "TupleHash");

    return struct {
        const Self = @This();

        /// The output length, in bytes.
        pub const digest_length = CShaker.digest_length;
        /// The block length, or rate, in bytes.
        pub const block_length = CShaker.block_length;

        cshaker: CShaker,
        xof_mode: bool = false,

        /// TupleHash options can include a context string.
        pub const Options = struct {
            context: ?[]const u8 = null,
        };

        /// Initialize a state for the TupleHash function, with an optional context.
        /// If the context is going to be reused, the structure can be initialized once, and cloned for each message.
        /// This is more efficient than reinitializing the state for each message at the cost of a small amount of memory.
        ///
        /// A key can be optionally added to the context to create a keyed TupleHash function, similar to KMAC.
        pub fn initWithOptions(options: Options) Self {
            const cshaker = CShaker.init(.{ .context = options.context });
            return Self{
                .cshaker = cshaker,
            };
        }

        /// Initialize a state for the MAC function.
        pub fn init() Self {
            return initWithOptions(.{});
        }

        /// Add data to the state, separated from previous updates.
        pub fn update(self: *Self, b: []const u8) void {
            const encoded_b_len = NistLengthEncoding.encode(.left, b.len);
            self.cshaker.update(encoded_b_len.slice());
            self.cshaker.update(b);
        }

        /// Return an authentication tag for the current state.
        pub fn final(self: *Self, out: []u8) void {
            const encoded_out_len = NistLengthEncoding.encode(.right, out.len);
            self.cshaker.update(encoded_out_len.slice());
            self.cshaker.final(out);
        }

        /// Align the input to a block boundary.
        pub fn fillBlock(self: *Self) void {
            self.cshaker.fillBlock();
        }

        /// Squeeze a slice of bytes from the state.
        /// `out` can be any length, and the function can be called multiple times.
        pub fn squeeze(self: *Self, out: []u8) void {
            if (!self.xof_mode) {
                const encoded_out_len = comptime NistLengthEncoding.encode(.right, 0);
                self.update(encoded_out_len.slice());
                self.xof_mode = true;
            }
            self.cshaker.squeeze(out);
        }
    };
}

/// The NIST SP 800-185 encoded length format.
pub const NistLengthEncoding = enum {
    left,
    right,

    /// A length encoded according to NIST SP 800-185.
    pub const Length = struct {
        /// The size of the encoded value, in bytes.
        len: usize = 0,
        /// A buffer to store the encoded length.
        buf: [@sizeOf(usize) + 1]u8 = undefined,

        /// Return the encoded length as a slice.
        pub fn slice(self: *const Length) []const u8 {
            return self.buf[0..self.len];
        }
    };

    /// Encode a length according to NIST SP 800-185.
    pub fn encode(comptime encoding: NistLengthEncoding, len: usize) Length {
        const len_bits = @bitSizeOf(@TypeOf(len)) - @clz(len) + 3;
        const len_bytes = std.math.divCeil(usize, len_bits, 8) catch unreachable;

        var res = Length{ .len = len_bytes + 1 };
        if (encoding == .right) {
            res.buf[len_bytes] = @intCast(len_bytes);
        }
        const end = if (encoding == .right) len_bytes - 1 else len_bytes;
        res.buf[end] = @truncate(len << 3);
        var len_ = len >> 5;
        for (1..len_bytes) |i| {
            res.buf[end - i] = @truncate(len_);
            len_ >>= 8;
        }
        if (encoding == .left) {
            res.buf[0] = @intCast(len_bytes);
        }
        return res;
    }
};

const htest = @import("test.zig");

test {
    _ = kangarootwelve;
}

test "sha3-224 single" {
    try htest.assertEqualHash(Sha3_224, "6b4e03423667dbb73b6e15454f0eb1abd4597f9a1b078e3f5b5a6bc7", "");
    try htest.assertEqualHash(Sha3_224, "e642824c3f8cf24ad09234ee7d3c766fc9a3a5168d0c94ad73b46fdf", "abc");
    try htest.assertEqualHash(Sha3_224, "543e6868e1666c1a643630df77367ae5a62a85070a51c14cbf665cbc", "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu");
}

test "sha3-224 streaming" {
    var h = Sha3_224.init(.{});
    var out: [28]u8 = undefined;

    h.final(out[0..]);
    try htest.assertEqual("6b4e03423667dbb73b6e15454f0eb1abd4597f9a1b078e3f5b5a6bc7", out[0..]);

    h = Sha3_224.init(.{});
    h.update("abc");
    h.final(out[0..]);
    try htest.assertEqual("e642824c3f8cf24ad09234ee7d3c766fc9a3a5168d0c94ad73b46fdf", out[0..]);

    h = Sha3_224.init(.{});
    h.update("a");
    h.update("b");
    h.update("c");
    h.final(out[0..]);
    try htest.assertEqual("e642824c3f8cf24ad09234ee7d3c766fc9a3a5168d0c94ad73b46fdf", out[0..]);
}

test "sha3-256 single" {
    try htest.assertEqualHash(Sha3_256, "a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a", "");
    try htest.assertEqualHash(Sha3_256, "3a985da74fe225b2045c172d6bd390bd855f086e3e9d525b46bfe24511431532", "abc");
    try htest.assertEqualHash(Sha3_256, "916f6061fe879741ca6469b43971dfdb28b1a32dc36cb3254e812be27aad1d18", "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu");
}

test "sha3-256 streaming" {
    var h = Sha3_256.init(.{});
    var out: [32]u8 = undefined;

    h.final(out[0..]);
    try htest.assertEqual("a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a", out[0..]);

    h = Sha3_256.init(.{});
    h.update("abc");
    h.final(out[0..]);
    try htest.assertEqual("3a985da74fe225b2045c172d6bd390bd855f086e3e9d525b46bfe24511431532", out[0..]);

    h = Sha3_256.init(.{});
    h.update("a");
    h.update("b");
    h.update("c");
    h.final(out[0..]);
    try htest.assertEqual("3a985da74fe225b2045c172d6bd390bd855f086e3e9d525b46bfe24511431532", out[0..]);
}

test "sha3-256 aligned final" {
    var block = [_]u8{0} ** Sha3_256.block_length;
    var out: [Sha3_256.digest_length]u8 = undefined;

    var h = Sha3_256.init(.{});
    h.update(&block);
    h.final(out[0..]);
}

test "sha3-384 single" {
    const h1 = "0c63a75b845e4f7d01107d852e4c2485c51a50aaaa94fc61995e71bbee983a2ac3713831264adb47fb6bd1e058d5f004";
    try htest.assertEqualHash(Sha3_384, h1, "");
    const h2 = "ec01498288516fc926459f58e2c6ad8df9b473cb0fc08c2596da7cf0e49be4b298d88cea927ac7f539f1edf228376d25";
    try htest.assertEqualHash(Sha3_384, h2, "abc");
    const h3 = "79407d3b5916b59c3e30b09822974791c313fb9ecc849e406f23592d04f625dc8c709b98b43b3852b337216179aa7fc7";
    try htest.assertEqualHash(Sha3_384, h3, "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu");
}

test "sha3-384 streaming" {
    var h = Sha3_384.init(.{});
    var out: [48]u8 = undefined;

    const h1 = "0c63a75b845e4f7d01107d852e4c2485c51a50aaaa94fc61995e71bbee983a2ac3713831264adb47fb6bd1e058d5f004";
    h.final(out[0..]);
    try htest.assertEqual(h1, out[0..]);

    const h2 = "ec01498288516fc926459f58e2c6ad8df9b473cb0fc08c2596da7cf0e49be4b298d88cea927ac7f539f1edf228376d25";
    h = Sha3_384.init(.{});
    h.update("abc");
    h.final(out[0..]);
    try htest.assertEqual(h2, out[0..]);

    h = Sha3_384.init(.{});
    h.update("a");
    h.update("b");
    h.update("c");
    h.final(out[0..]);
    try htest.assertEqual(h2, out[0..]);
}

test "sha3-512 single" {
    if (builtin.cpu.has(.riscv, .v) and builtin.zig_backend == .stage2_llvm) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/25083

    const h1 = "a69f73cca23a9ac5c8b567dc185a756e97c982164fe25859e0d1dcc1475c80a615b2123af1f5f94c11e3e9402c3ac558f500199d95b6d3e301758586281dcd26";
    try htest.assertEqualHash(Sha3_512, h1, "");
    const h2 = "b751850b1a57168a5693cd924b6b096e08f621827444f70d884f5d0240d2712e10e116e9192af3c91a7ec57647e3934057340b4cf408d5a56592f8274eec53f0";
    try htest.assertEqualHash(Sha3_512, h2, "abc");
    const h3 = "afebb2ef542e6579c50cad06d2e578f9f8dd6881d7dc824d26360feebf18a4fa73e3261122948efcfd492e74e82e2189ed0fb440d187f382270cb455f21dd185";
    try htest.assertEqualHash(Sha3_512, h3, "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu");
}

test "sha3-512 streaming" {
    var h = Sha3_512.init(.{});
    var out: [64]u8 = undefined;

    const h1 = "a69f73cca23a9ac5c8b567dc185a756e97c982164fe25859e0d1dcc1475c80a615b2123af1f5f94c11e3e9402c3ac558f500199d95b6d3e301758586281dcd26";
    h.final(out[0..]);
    try htest.assertEqual(h1, out[0..]);

    const h2 = "b751850b1a57168a5693cd924b6b096e08f621827444f70d884f5d0240d2712e10e116e9192af3c91a7ec57647e3934057340b4cf408d5a56592f8274eec53f0";
    h = Sha3_512.init(.{});
    h.update("abc");
    h.final(out[0..]);
    try htest.assertEqual(h2, out[0..]);

    h = Sha3_512.init(.{});
    h.update("a");
    h.update("b");
    h.update("c");
    h.final(out[0..]);
    try htest.assertEqual(h2, out[0..]);
}

test "sha3-512 aligned final" {
    var block = [_]u8{0} ** Sha3_512.block_length;
    var out: [Sha3_512.digest_length]u8 = undefined;

    var h = Sha3_512.init(.{});
    h.update(&block);
    h.final(out[0..]);
}

test "keccak-256 single" {
    try htest.assertEqualHash(Keccak256, "c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470", "");
    try htest.assertEqualHash(Keccak256, "4e03657aea45a94fc7d47ba826c8d667c0d1e6e33a64a036ec44f58fa12d6c45", "abc");
    try htest.assertEqualHash(Keccak256, "f519747ed599024f3882238e5ab43960132572b7345fbeb9a90769dafd21ad67", "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu");
}

test "keccak-512 single" {
    try htest.assertEqualHash(Keccak512, "0eab42de4c3ceb9235fc91acffe746b29c29a8c366b7c60e4e67c466f36a4304c00fa9caf9d87976ba469bcbe06713b435f091ef2769fb160cdab33d3670680e", "");
    try htest.assertEqualHash(Keccak512, "18587dc2ea106b9a1563e32b3312421ca164c7f1f07bc922a9c83d77cea3a1e5d0c69910739025372dc14ac9642629379540c17e2a65b19d77aa511a9d00bb96", "abc");
    try htest.assertEqualHash(Keccak512, "ac2fb35251825d3aa48468a9948c0a91b8256f6d97d8fa4160faff2dd9dfcc24f3f1db7a983dad13d53439ccac0b37e24037e7b95f80f59f37a2f683c4ba4682", "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu");
}

test "SHAKE-128 single" {
    var out: [10]u8 = undefined;
    Shake128.hash("hello123", &out, .{});
    try htest.assertEqual("1b85861510bc4d8e467d", &out);
}

test "SHAKE-128 multisqueeze" {
    var out: [10]u8 = undefined;
    var h = Shake128.init(.{});
    h.update("hello123");
    h.squeeze(out[0..4]);
    h.squeeze(out[4..]);
    try htest.assertEqual("1b85861510bc4d8e467d", &out);
}

test "SHAKE-128 multisqueeze with multiple blocks" {
    var out: [100]u8 = undefined;
    var out2: [100]u8 = undefined;

    var h = Shake128.init(.{});
    h.update("hello123");
    h.squeeze(out[0..50]);
    h.squeeze(out[50..]);

    var h2 = Shake128.init(.{});
    h2.update("hello123");
    h2.squeeze(&out2);
    try std.testing.expectEqualSlices(u8, &out, &out2);
}

test "SHAKE-256 single" {
    var out: [10]u8 = undefined;
    Shake256.hash("hello123", &out, .{});
    try htest.assertEqual("ade612ba265f92de4a37", &out);
}

test "TurboSHAKE-128" {
    var out: [32]u8 = undefined;
    TurboShake(128, 0x06).hash("\xff", &out, .{});
    try htest.assertEqual("8ec9c66465ed0d4a6c35d13506718d687a25cb05c74cca1e42501abd83874a67", &out);
}

test "SHA-3 with streaming" {
    var msg: [613]u8 = [613]u8{ 0x97, 0xd1, 0x2d, 0x1a, 0x16, 0x2d, 0x36, 0x4d, 0x20, 0x62, 0x19, 0x0b, 0x14, 0x93, 0xbb, 0xf8, 0x5b, 0xea, 0x04, 0xc2, 0x61, 0x8e, 0xd6, 0x08, 0x81, 0xa1, 0x1d, 0x73, 0x27, 0x48, 0xbf, 0xa4, 0xba, 0xb1, 0x9a, 0x48, 0x9c, 0xf9, 0x9b, 0xff, 0x34, 0x48, 0xa9, 0x75, 0xea, 0xc8, 0xa3, 0x48, 0x24, 0x9d, 0x75, 0x27, 0x48, 0xec, 0x03, 0xb0, 0xbb, 0xdf, 0x33, 0x90, 0xe3, 0x93, 0xed, 0x68, 0x24, 0x39, 0x12, 0xdf, 0xea, 0xee, 0x8c, 0x9f, 0x96, 0xde, 0x42, 0x46, 0x8c, 0x2b, 0x17, 0x83, 0x36, 0xfb, 0xf4, 0xf7, 0xff, 0x79, 0xb9, 0x45, 0x41, 0xc9, 0x56, 0x1a, 0x6b, 0x0c, 0xa4, 0x1a, 0xdd, 0x6b, 0x95, 0xe8, 0x03, 0x0f, 0x09, 0x29, 0x40, 0x1b, 0xea, 0x87, 0xfa, 0xb9, 0x18, 0xa9, 0x95, 0x07, 0x7c, 0x2f, 0x7c, 0x33, 0xfb, 0xc5, 0x11, 0x5e, 0x81, 0x0e, 0xbc, 0xae, 0xec, 0xb3, 0xe1, 0x4a, 0x26, 0x56, 0xe8, 0x5b, 0x11, 0x9d, 0x37, 0x06, 0x9b, 0x34, 0x31, 0x6e, 0xa3, 0xba, 0x41, 0xbc, 0x11, 0xd8, 0xc5, 0x15, 0xc9, 0x30, 0x2c, 0x9b, 0xb6, 0x71, 0xd8, 0x7c, 0xbc, 0x38, 0x2f, 0xd5, 0xbd, 0x30, 0x96, 0xd4, 0xa3, 0x00, 0x77, 0x9d, 0x55, 0x4a, 0x33, 0x53, 0xb6, 0xb3, 0x35, 0x1b, 0xae, 0xe5, 0xdc, 0x22, 0x23, 0x85, 0x95, 0x88, 0xf9, 0x3b, 0xbf, 0x74, 0x13, 0xaa, 0xcb, 0x0a, 0x60, 0x79, 0x13, 0x79, 0xc0, 0x4a, 0x02, 0xdb, 0x1c, 0xc9, 0xff, 0x60, 0x57, 0x9a, 0x70, 0x28, 0x58, 0x60, 0xbc, 0x57, 0x07, 0xc7, 0x47, 0x1a, 0x45, 0x71, 0x76, 0x94, 0xfb, 0x05, 0xad, 0xec, 0x12, 0x29, 0x5a, 0x44, 0x6a, 0x81, 0xd9, 0xc6, 0xf0, 0xb6, 0x9b, 0x97, 0x83, 0x69, 0xfb, 0xdc, 0x0d, 0x4a, 0x67, 0xbc, 0x72, 0xf5, 0x43, 0x5e, 0x9b, 0x13, 0xf2, 0xe4, 0x6d, 0x49, 0xdb, 0x76, 0xcb, 0x42, 0x6a, 0x3c, 0x9f, 0xa1, 0xfe, 0x5e, 0xca, 0x0a, 0xfc, 0xfa, 0x39, 0x27, 0xd1, 0x3c, 0xcb, 0x9a, 0xde, 0x4c, 0x6b, 0x09, 0x8b, 0x49, 0xfd, 0x1e, 0x3d, 0x5e, 0x67, 0x7c, 0x57, 0xad, 0x90, 0xcc, 0x46, 0x5f, 0x5c, 0xae, 0x6a, 0x9c, 0xb2, 0xcd, 0x2c, 0x89, 0x78, 0xcf, 0xf1, 0x49, 0x96, 0x55, 0x1e, 0x04, 0xef, 0x0e, 0x1c, 0xde, 0x6c, 0x96, 0x51, 0x00, 0xee, 0x9a, 0x1f, 0x8d, 0x61, 0xbc, 0xeb, 0xb1, 0xa6, 0xa5, 0x21, 0x8b, 0xa7, 0xf8, 0x25, 0x41, 0x48, 0x62, 0x5b, 0x01, 0x6c, 0x7c, 0x2a, 0xe8, 0xff, 0xf9, 0xf9, 0x1f, 0xe2, 0x79, 0x2e, 0xd1, 0xff, 0xa3, 0x2e, 0x1c, 0x3a, 0x1a, 0x5d, 0x2b, 0x7b, 0x87, 0x25, 0x22, 0xa4, 0x90, 0xea, 0x26, 0x9d, 0xdd, 0x13, 0x60, 0x4c, 0x10, 0x03, 0xf6, 0x99, 0xd3, 0x21, 0x0c, 0x69, 0xc6, 0xd8, 0xc8, 0x9e, 0x94, 0x89, 0x51, 0x21, 0xe3, 0x9a, 0xcd, 0xda, 0x54, 0x72, 0x64, 0xae, 0x94, 0x79, 0x36, 0x81, 0x44, 0x14, 0x6d, 0x3a, 0x0e, 0xa6, 0x30, 0xbf, 0x95, 0x99, 0xa6, 0xf5, 0x7f, 0x4f, 0xef, 0xc6, 0x71, 0x2f, 0x36, 0x13, 0x14, 0xa2, 0x9d, 0xc2, 0x0c, 0x0d, 0x4e, 0xc0, 0x02, 0xd3, 0x6f, 0xee, 0x98, 0x5e, 0x24, 0x31, 0x74, 0x11, 0x96, 0x6e, 0x43, 0x57, 0xe8, 0x8e, 0xa0, 0x8d, 0x3d, 0x79, 0x38, 0x20, 0xc2, 0x0f, 0xb4, 0x75, 0x99, 0x3b, 0xb1, 0xf0, 0xe8, 0xe1, 0xda, 0xf9, 0xd4, 0xe6, 0xd6, 0xf4, 0x8a, 0x32, 0x4a, 0x4a, 0x25, 0xa8, 0xd9, 0x60, 0xd6, 0x33, 0x31, 0x97, 0xb9, 0xb6, 0xed, 0x5f, 0xfc, 0x15, 0xbd, 0x13, 0xc0, 0x3a, 0x3f, 0x1f, 0x2d, 0x09, 0x1d, 0xeb, 0x69, 0x6a, 0xfe, 0xd7, 0x95, 0x3e, 0x8a, 0x4e, 0xe1, 0x6e, 0x61, 0xb2, 0x6c, 0xe3, 0x2b, 0x70, 0x60, 0x7e, 0x8c, 0xe4, 0xdd, 0x27, 0x30, 0x7e, 0x0d, 0xc7, 0xb7, 0x9a, 0x1a, 0x3c, 0xcc, 0xa7, 0x22, 0x77, 0x14, 0x05, 0x50, 0x57, 0x31, 0x1b, 0xc8, 0xbf, 0xce, 0x52, 0xaf, 0x9c, 0x8e, 0x10, 0x2e, 0xd2, 0x16, 0xb6, 0x6e, 0x43, 0x10, 0xaf, 0x8b, 0xde, 0x1d, 0x60, 0xb2, 0x7d, 0xe6, 0x2f, 0x08, 0x10, 0x12, 0x7e, 0xb4, 0x76, 0x45, 0xb6, 0xd8, 0x9b, 0x26, 0x40, 0xa1, 0x63, 0x5c, 0x7a, 0x2a, 0xb1, 0x8c, 0xd6, 0xa4, 0x6f, 0x5a, 0xae, 0x33, 0x7e, 0x6d, 0x71, 0xf5, 0xc8, 0x6d, 0x80, 0x1c, 0x35, 0xfc, 0x3f, 0xc1, 0xa6, 0xc6, 0x1a, 0x15, 0x04, 0x6d, 0x76, 0x38, 0x32, 0x95, 0xb2, 0x51, 0x1a, 0xe9, 0x3e, 0x89, 0x9f, 0x0c, 0x79 };
    var out: [Sha3_256.digest_length]u8 = undefined;

    Sha3_256.hash(&msg, &out, .{});
    try htest.assertEqual("5780048dfa381a1d01c747906e4a08711dd34fd712ecd7c6801dd2b38fd81a89", &out);

    var h = Sha3_256.init(.{});
    h.update(msg[0..64]);
    h.update(msg[64..613]);
    h.final(&out);
    try htest.assertEqual("5780048dfa381a1d01c747906e4a08711dd34fd712ecd7c6801dd2b38fd81a89", &out);
}

test "cSHAKE-128 with no context nor function name" {
    var out: [32]u8 = undefined;
    CShake128.hash("hello123", &out, .{});
    try htest.assertEqual("1b85861510bc4d8e467d6f8a92270533cbaa7ba5e06c2d2a502854bac468b8b9", &out);
}

test "cSHAKE-128 with context" {
    var out: [32]u8 = undefined;
    CShake128.hash("hello123", &out, .{ .context = "custom" });
    try htest.assertEqual("7509fa13a6bd3e38ad5c6fac042142c233996e40ebffc86c276f108b3b19cc6a", &out);
}

test "cSHAKE-128 with context and function" {
    var out: [32]u8 = undefined;
    CShake(128, "function").hash("hello123", &out, .{ .context = "custom" });
    try htest.assertEqual("ad7f4d7db2d96587fcd5047c65d37c368f5366e3afac60bb9b66b0bb95dfb675", &out);
}

test "cSHAKE-256" {
    var out: [32]u8 = undefined;
    CShake256.hash("hello123", &out, .{ .context = "custom" });
    try htest.assertEqual("dabe027eb1a6cbe3a0542d0560eb4e6b39146dd72ae1bf89c970a61bd93b1813", &out);
}

test "KMAC-128 with empty key and message" {
    var out: [KMac128.mac_length]u8 = undefined;
    const key = "";
    KMac128.create(&out, "", key);
    try htest.assertEqual("5c135c615152fb4d9784dd1155f9b6034e013fd77165c327dfa4d36701983ef7", &out);
}

test "KMAC-128" {
    var out: [KMac128.mac_length]u8 = undefined;
    const key = "A KMAC secret key";
    KMac128.create(&out, "hello123", key);
    try htest.assertEqual("1fa1c0d761129a83f9a4299ca137674de8373a3cc437799ae4c129e651627f8e", &out);
}

test "KMAC-128 with a customization string" {
    var out: [KMac128.mac_length]u8 = undefined;
    const key = "A KMAC secret key";
    KMac128.createWithOptions(&out, "hello123", key, .{ .context = "custom" });
    try htest.assertEqual("c58c6d42dc00a27dfa8e7e08f8c9307cecb5d662ddb11b6c36057fc2e0e068ba", &out);
}

test "KMACXOF-128" {
    const key = "A KMAC secret key";
    var xof = KMac128.init(key);
    xof.update("hello123");
    var out: [50]u8 = undefined;
    xof.squeeze(&out);
    try htest.assertEqual("628c2fb870d294b3673ac82d9f0d651aae6a5bb8084ea8cd8343cb888d075b9053173200a71f301141069c3c0322527981f7", &out);
    xof.squeeze(&out);
    try htest.assertEqual("7b638e178cfdac5727a4ea7694efaa967a65a1d0034501855acff506b4158d187d5a18d668e67b43f2abf61144b20ed4c09f", &out);
}

test "KMACXOF-256" {
    const key = "A KMAC secret key";
    var xof = KMac256.init(key);
    xof.update("hello123");
    var out: [50]u8 = undefined;
    xof.squeeze(&out);
    try htest.assertEqual("23fc644bc2655ba6fde7b7c11f2804f22e8d8c6bd7db856268bf3370ce2362703f6c7e91916a1b8c116e60edfbcb25613054", &out);
    xof.squeeze(&out);
    try htest.assertEqual("ff97251020ff255ee65a1c1f5f78ebe904f61211c39f973f82fbce2b196b9f51c2cb12afe51549a0f1eaf7954e657ba11af3", &out);
}

test "TupleHash-128" {
    var st = TupleHash128.init();
    st.update("hello");
    st.update("123");
    var out: [32]u8 = undefined;
    st.final(&out);
    try htest.assertEqual("3938d49ade8ec0f0c305ac63497b2d2e8b2f650714f9667cc41816b1c11ffd20", &out);
}

test "TupleHash-256" {
    var st = TupleHash256.init();
    st.update("hello");
    st.update("123");
    var out: [64]u8 = undefined;
    st.final(&out);
    try htest.assertEqual("2dca563c2882f2ba4f46a441a4c5e13fb97150d1436fe99c7e4e43a2d20d0f1cd3d38483bde4a966930606dfa6c61c4ca6400aeedfb474d1bf0d7f6a70968289", &out);
}



---
File: /std/crypto/siphash.zig
---

//
// SipHash is a moderately fast pseudorandom function, returning a 64-bit or 128-bit tag for an arbitrary long input.
//
// Typical use cases include:
// - protection against DoS attacks for hash tables and bloom filters
// - authentication of short-lived messages in online protocols
//
// https://www.aumasson.jp/siphash/siphash.pdf
const std = @import("../std.zig");
const assert = std.debug.assert;
const testing = std.testing;
const math = std.math;
const mem = std.mem;

/// SipHash function with 64-bit output.
///
/// Recommended parameters are:
/// - (c_rounds=4, d_rounds=8) for conservative security; regular hash functions such as BLAKE2 or BLAKE3 are usually a better alternative.
/// - (c_rounds=2, d_rounds=4) standard parameters.
/// - (c_rounds=1, d_rounds=3) reduced-round function. Faster, no known implications on its practical security level.
/// - (c_rounds=1, d_rounds=2) fastest option, but the output may be distinguishable from random data with related keys or non-uniform input - not suitable as a PRF.
///
/// SipHash is not a traditional hash function. If the input includes untrusted content, a secret key is absolutely necessary.
/// And due to its small output size, collisions in SipHash64 can be found with an exhaustive search.
pub fn SipHash64(comptime c_rounds: usize, comptime d_rounds: usize) type {
    return SipHash(u64, c_rounds, d_rounds);
}

/// SipHash function with 128-bit output.
///
/// Recommended parameters are:
/// - (c_rounds=4, d_rounds=8) for conservative security; regular hash functions such as BLAKE2 or BLAKE3 are usually a better alternative.
/// - (c_rounds=2, d_rounds=4) standard parameters.
/// - (c_rounds=1, d_rounds=4) reduced-round function. Recommended to hash very short, similar strings, when a 128-bit PRF output is still required.
/// - (c_rounds=1, d_rounds=3) reduced-round function. Faster, no known implications on its practical security level.
/// - (c_rounds=1, d_rounds=2) fastest option, but the output may be distinguishable from random data with related keys or non-uniform input - not suitable as a PRF.
///
/// SipHash is not a traditional hash function. If the input includes untrusted content, a secret key is absolutely necessary.
pub fn SipHash128(comptime c_rounds: usize, comptime d_rounds: usize) type {
    return SipHash(u128, c_rounds, d_rounds);
}

fn SipHashStateless(comptime T: type, comptime c_rounds: usize, comptime d_rounds: usize) type {
    assert(T == u64 or T == u128);
    assert(c_rounds > 0 and d_rounds > 0);

    return struct {
        const Self = @This();
        const block_length = 64;
        const key_length = 16;

        v0: u64,
        v1: u64,
        v2: u64,
        v3: u64,
        msg_len: u8,

        fn init(key: *const [key_length]u8) Self {
            const k0 = mem.readInt(u64, key[0..8], .little);
            const k1 = mem.readInt(u64, key[8..16], .little);

            var d = Self{
                .v0 = k0 ^ 0x736f6d6570736575,
                .v1 = k1 ^ 0x646f72616e646f6d,
                .v2 = k0 ^ 0x6c7967656e657261,
                .v3 = k1 ^ 0x7465646279746573,
                .msg_len = 0,
            };

            if (T == u128) {
                d.v1 ^= 0xee;
            }

            return d;
        }

        fn update(self: *Self, b: []const u8) void {
            std.debug.assert(b.len % 8 == 0);

            var off: usize = 0;
            while (off < b.len) : (off += 8) {
                const blob = b[off..][0..8].*;
                @call(.always_inline, round, .{ self, blob });
            }

            self.msg_len +%= @as(u8, @truncate(b.len));
        }

        fn final(self: *Self, b: []const u8) T {
            std.debug.assert(b.len < 8);

            self.msg_len +%= @as(u8, @truncate(b.len));

            var buf = [_]u8{0} ** 8;
            @memcpy(buf[0..b.len], b);
            buf[7] = self.msg_len;
            self.round(buf);

            if (T == u128) {
                self.v2 ^= 0xee;
            } else {
                self.v2 ^= 0xff;
            }

            comptime var i: usize = 0;
            inline while (i < d_rounds) : (i += 1) {
                @call(.always_inline, sipRound, .{self});
            }

            const b1 = self.v0 ^ self.v1 ^ self.v2 ^ self.v3;
            if (T == u64) {
                return b1;
            }

            self.v1 ^= 0xdd;

            comptime var j: usize = 0;
            inline while (j < d_rounds) : (j += 1) {
                @call(.always_inline, sipRound, .{self});
            }

            const b2 = self.v0 ^ self.v1 ^ self.v2 ^ self.v3;
            return (@as(u128, b2) << 64) | b1;
        }

        fn round(self: *Self, b: [8]u8) void {
            const m = mem.readInt(u64, &b, .little);
            self.v3 ^= m;

            comptime var i: usize = 0;
            inline while (i < c_rounds) : (i += 1) {
                @call(.always_inline, sipRound, .{self});
            }

            self.v0 ^= m;
        }

        fn sipRound(d: *Self) void {
            d.v0 +%= d.v1;
            d.v1 = math.rotl(u64, d.v1, @as(u64, 13));
            d.v1 ^= d.v0;
            d.v0 = math.rotl(u64, d.v0, @as(u64, 32));
            d.v2 +%= d.v3;
            d.v3 = math.rotl(u64, d.v3, @as(u64, 16));
            d.v3 ^= d.v2;
            d.v0 +%= d.v3;
            d.v3 = math.rotl(u64, d.v3, @as(u64, 21));
            d.v3 ^= d.v0;
            d.v2 +%= d.v1;
            d.v1 = math.rotl(u64, d.v1, @as(u64, 17));
            d.v1 ^= d.v2;
            d.v2 = math.rotl(u64, d.v2, @as(u64, 32));
        }

        fn hash(msg: []const u8, key: *const [key_length]u8) T {
            const aligned_len = msg.len - (msg.len % 8);
            var c = Self.init(key);
            @call(.always_inline, update, .{ &c, msg[0..aligned_len] });
            return @call(.always_inline, final, .{ &c, msg[aligned_len..] });
        }
    };
}

fn SipHash(comptime T: type, comptime c_rounds: usize, comptime d_rounds: usize) type {
    assert(T == u64 or T == u128);
    assert(c_rounds > 0 and d_rounds > 0);

    return struct {
        const State = SipHashStateless(T, c_rounds, d_rounds);
        const Self = @This();
        pub const key_length = 16;
        pub const mac_length = @sizeOf(T);
        pub const block_length = 8;

        state: State,
        buf: [8]u8,
        buf_len: usize,

        /// Initialize a state for a SipHash function
        pub fn init(key: *const [key_length]u8) Self {
            return Self{
                .state = State.init(key),
                .buf = undefined,
                .buf_len = 0,
            };
        }

        /// Add data to the state
        pub fn update(self: *Self, b: []const u8) void {
            var off: usize = 0;

            if (self.buf_len != 0 and self.buf_len + b.len >= 8) {
                off += 8 - self.buf_len;
                @memcpy(self.buf[self.buf_len..][0..off], b[0..off]);
                self.state.update(self.buf[0..]);
                self.buf_len = 0;
            }

            const remain_len = b.len - off;
            const aligned_len = remain_len - (remain_len % 8);
            self.state.update(b[off .. off + aligned_len]);

            const b_slice = b[off + aligned_len ..];
            @memcpy(self.buf[self.buf_len..][0..b_slice.len], b_slice);
            self.buf_len += @as(u8, @intCast(b_slice.len));
        }

        pub fn peek(self: Self) [mac_length]u8 {
            var copy = self;
            return copy.finalResult();
        }

        /// Return an authentication tag for the current state
        /// Assumes `out` is less than or equal to `mac_length`.
        pub fn final(self: *Self, out: *[mac_length]u8) void {
            mem.writeInt(T, out, self.state.final(self.buf[0..self.buf_len]), .little);
        }

        pub fn finalResult(self: *Self) [mac_length]u8 {
            var result: [mac_length]u8 = undefined;
            self.final(&result);
            return result;
        }

        /// Return an authentication tag for a message and a key
        pub fn create(out: *[mac_length]u8, msg: []const u8, key: *const [key_length]u8) void {
            var ctx = Self.init(key);
            ctx.update(msg);
            ctx.final(out);
        }

        /// Return an authentication tag for the current state, as an integer
        pub fn finalInt(self: *Self) T {
            return self.state.final(self.buf[0..self.buf_len]);
        }

        /// Return an authentication tag for a message and a key, as an integer
        pub fn toInt(msg: []const u8, key: *const [key_length]u8) T {
            return State.hash(msg, key);
        }
    };
}

// Test vectors from reference implementation.
// https://github.com/veorq/SipHash/blob/master/vectors.h
const test_key = "\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0a\x0b\x0c\x0d\x0e\x0f";

test "siphash64-2-4 sanity" {
    const vectors = [_][8]u8{
        "\x31\x0e\x0e\xdd\x47\xdb\x6f\x72".*, // ""
        "\xfd\x67\xdc\x93\xc5\x39\xf8\x74".*, // "\x00"
        "\x5a\x4f\xa9\xd9\x09\x80\x6c\x0d".*, // "\x00\x01" ... etc
        "\x2d\x7e\xfb\xd7\x96\x66\x67\x85".*,
        "\xb7\x87\x71\x27\xe0\x94\x27\xcf".*,
        "\x8d\xa6\x99\xcd\x64\x55\x76\x18".*,
        "\xce\xe3\xfe\x58\x6e\x46\xc9\xcb".*,
        "\x37\xd1\x01\x8b\xf5\x00\x02\xab".*,
        "\x62\x24\x93\x9a\x79\xf5\xf5\x93".*,
        "\xb0\xe4\xa9\x0b\xdf\x82\x00\x9e".*,
        "\xf3\xb9\xdd\x94\xc5\xbb\x5d\x7a".*,
        "\xa7\xad\x6b\x22\x46\x2f\xb3\xf4".*,
        "\xfb\xe5\x0e\x86\xbc\x8f\x1e\x75".*,
        "\x90\x3d\x84\xc0\x27\x56\xea\x14".*,
        "\xee\xf2\x7a\x8e\x90\xca\x23\xf7".*,
        "\xe5\x45\xbe\x49\x61\xca\x29\xa1".*,
        "\xdb\x9b\xc2\x57\x7f\xcc\x2a\x3f".*,
        "\x94\x47\xbe\x2c\xf5\xe9\x9a\x69".*,
        "\x9c\xd3\x8d\x96\xf0\xb3\xc1\x4b".*,
        "\xbd\x61\x79\xa7\x1d\xc9\x6d\xbb".*,
        "\x98\xee\xa2\x1a\xf2\x5c\xd6\xbe".*,
        "\xc7\x67\x3b\x2e\xb0\xcb\xf2\xd0".*,
        "\x88\x3e\xa3\xe3\x95\x67\x53\x93".*,
        "\xc8\xce\x5c\xcd\x8c\x03\x0c\xa8".*,
        "\x94\xaf\x49\xf6\xc6\x50\xad\xb8".*,
        "\xea\xb8\x85\x8a\xde\x92\xe1\xbc".*,
        "\xf3\x15\xbb\x5b\xb8\x35\xd8\x17".*,
        "\xad\xcf\x6b\x07\x63\x61\x2e\x2f".*,
        "\xa5\xc9\x1d\xa7\xac\xaa\x4d\xde".*,
        "\x71\x65\x95\x87\x66\x50\xa2\xa6".*,
        "\x28\xef\x49\x5c\x53\xa3\x87\xad".*,
        "\x42\xc3\x41\xd8\xfa\x92\xd8\x32".*,
        "\xce\x7c\xf2\x72\x2f\x51\x27\x71".*,
        "\xe3\x78\x59\xf9\x46\x23\xf3\xa7".*,
        "\x38\x12\x05\xbb\x1a\xb0\xe0\x12".*,
        "\xae\x97\xa1\x0f\xd4\x34\xe0\x15".*,
        "\xb4\xa3\x15\x08\xbe\xff\x4d\x31".*,
        "\x81\x39\x62\x29\xf0\x90\x79\x02".*,
        "\x4d\x0c\xf4\x9e\xe5\xd4\xdc\xca".*,
        "\x5c\x73\x33\x6a\x76\xd8\xbf\x9a".*,
        "\xd0\xa7\x04\x53\x6b\xa9\x3e\x0e".*,
        "\x92\x59\x58\xfc\xd6\x42\x0c\xad".*,
        "\xa9\x15\xc2\x9b\xc8\x06\x73\x18".*,
        "\x95\x2b\x79\xf3\xbc\x0a\xa6\xd4".*,
        "\xf2\x1d\xf2\xe4\x1d\x45\x35\xf9".*,
        "\x87\x57\x75\x19\x04\x8f\x53\xa9".*,
        "\x10\xa5\x6c\xf5\xdf\xcd\x9a\xdb".*,
        "\xeb\x75\x09\x5c\xcd\x98\x6c\xd0".*,
        "\x51\xa9\xcb\x9e\xcb\xa3\x12\xe6".*,
        "\x96\xaf\xad\xfc\x2c\xe6\x66\xc7".*,
        "\x72\xfe\x52\x97\x5a\x43\x64\xee".*,
        "\x5a\x16\x45\xb2\x76\xd5\x92\xa1".*,
        "\xb2\x74\xcb\x8e\xbf\x87\x87\x0a".*,
        "\x6f\x9b\xb4\x20\x3d\xe7\xb3\x81".*,
        "\xea\xec\xb2\xa3\x0b\x22\xa8\x7f".*,
        "\x99\x24\xa4\x3c\xc1\x31\x57\x24".*,
        "\xbd\x83\x8d\x3a\xaf\xbf\x8d\xb7".*,
        "\x0b\x1a\x2a\x32\x65\xd5\x1a\xea".*,
        "\x13\x50\x79\xa3\x23\x1c\xe6\x60".*,
        "\x93\x2b\x28\x46\xe4\xd7\x06\x66".*,
        "\xe1\x91\x5f\x5c\xb1\xec\xa4\x6c".*,
        "\xf3\x25\x96\x5c\xa1\x6d\x62\x9f".*,
        "\x57\x5f\xf2\x8e\x60\x38\x1b\xe5".*,
        "\x72\x45\x06\xeb\x4c\x32\x8a\x95".*,
    };

    const siphash = SipHash64(2, 4);

    var buffer: [64]u8 = undefined;
    for (vectors, 0..) |vector, i| {
        buffer[i] = @as(u8, @intCast(i));

        var out: [siphash.mac_length]u8 = undefined;
        siphash.create(&out, buffer[0..i], test_key);
        try testing.expectEqual(out, vector);
    }
}

test "siphash128-2-4 sanity" {
    const vectors = [_][16]u8{
        "\xa3\x81\x7f\x04\xba\x25\xa8\xe6\x6d\xf6\x72\x14\xc7\x55\x02\x93".*,
        "\xda\x87\xc1\xd8\x6b\x99\xaf\x44\x34\x76\x59\x11\x9b\x22\xfc\x45".*,
        "\x81\x77\x22\x8d\xa4\xa4\x5d\xc7\xfc\xa3\x8b\xde\xf6\x0a\xff\xe4".*,
        "\x9c\x70\xb6\x0c\x52\x67\xa9\x4e\x5f\x33\xb6\xb0\x29\x85\xed\x51".*,
        "\xf8\x81\x64\xc1\x2d\x9c\x8f\xaf\x7d\x0f\x6e\x7c\x7b\xcd\x55\x79".*,
        "\x13\x68\x87\x59\x80\x77\x6f\x88\x54\x52\x7a\x07\x69\x0e\x96\x27".*,
        "\x14\xee\xca\x33\x8b\x20\x86\x13\x48\x5e\xa0\x30\x8f\xd7\xa1\x5e".*,
        "\xa1\xf1\xeb\xbe\xd8\xdb\xc1\x53\xc0\xb8\x4a\xa6\x1f\xf0\x82\x39".*,
        "\x3b\x62\xa9\xba\x62\x58\xf5\x61\x0f\x83\xe2\x64\xf3\x14\x97\xb4".*,
        "\x26\x44\x99\x06\x0a\xd9\xba\xab\xc4\x7f\x8b\x02\xbb\x6d\x71\xed".*,
        "\x00\x11\x0d\xc3\x78\x14\x69\x56\xc9\x54\x47\xd3\xf3\xd0\xfb\xba".*,
        "\x01\x51\xc5\x68\x38\x6b\x66\x77\xa2\xb4\xdc\x6f\x81\xe5\xdc\x18".*,
        "\xd6\x26\xb2\x66\x90\x5e\xf3\x58\x82\x63\x4d\xf6\x85\x32\xc1\x25".*,
        "\x98\x69\xe2\x47\xe9\xc0\x8b\x10\xd0\x29\x93\x4f\xc4\xb9\x52\xf7".*,
        "\x31\xfc\xef\xac\x66\xd7\xde\x9c\x7e\xc7\x48\x5f\xe4\x49\x49\x02".*,
        "\x54\x93\xe9\x99\x33\xb0\xa8\x11\x7e\x08\xec\x0f\x97\xcf\xc3\xd9".*,
        "\x6e\xe2\xa4\xca\x67\xb0\x54\xbb\xfd\x33\x15\xbf\x85\x23\x05\x77".*,
        "\x47\x3d\x06\xe8\x73\x8d\xb8\x98\x54\xc0\x66\xc4\x7a\xe4\x77\x40".*,
        "\xa4\x26\xe5\xe4\x23\xbf\x48\x85\x29\x4d\xa4\x81\xfe\xae\xf7\x23".*,
        "\x78\x01\x77\x31\xcf\x65\xfa\xb0\x74\xd5\x20\x89\x52\x51\x2e\xb1".*,
        "\x9e\x25\xfc\x83\x3f\x22\x90\x73\x3e\x93\x44\xa5\xe8\x38\x39\xeb".*,
        "\x56\x8e\x49\x5a\xbe\x52\x5a\x21\x8a\x22\x14\xcd\x3e\x07\x1d\x12".*,
        "\x4a\x29\xb5\x45\x52\xd1\x6b\x9a\x46\x9c\x10\x52\x8e\xff\x0a\xae".*,
        "\xc9\xd1\x84\xdd\xd5\xa9\xf5\xe0\xcf\x8c\xe2\x9a\x9a\xbf\x69\x1c".*,
        "\x2d\xb4\x79\xae\x78\xbd\x50\xd8\x88\x2a\x8a\x17\x8a\x61\x32\xad".*,
        "\x8e\xce\x5f\x04\x2d\x5e\x44\x7b\x50\x51\xb9\xea\xcb\x8d\x8f\x6f".*,
        "\x9c\x0b\x53\xb4\xb3\xc3\x07\xe8\x7e\xae\xe0\x86\x78\x14\x1f\x66".*,
        "\xab\xf2\x48\xaf\x69\xa6\xea\xe4\xbf\xd3\xeb\x2f\x12\x9e\xeb\x94".*,
        "\x06\x64\xda\x16\x68\x57\x4b\x88\xb9\x35\xf3\x02\x73\x58\xae\xf4".*,
        "\xaa\x4b\x9d\xc4\xbf\x33\x7d\xe9\x0c\xd4\xfd\x3c\x46\x7c\x6a\xb7".*,
        "\xea\x5c\x7f\x47\x1f\xaf\x6b\xde\x2b\x1a\xd7\xd4\x68\x6d\x22\x87".*,
        "\x29\x39\xb0\x18\x32\x23\xfa\xfc\x17\x23\xde\x4f\x52\xc4\x3d\x35".*,
        "\x7c\x39\x56\xca\x5e\xea\xfc\x3e\x36\x3e\x9d\x55\x65\x46\xeb\x68".*,
        "\x77\xc6\x07\x71\x46\xf0\x1c\x32\xb6\xb6\x9d\x5f\x4e\xa9\xff\xcf".*,
        "\x37\xa6\x98\x6c\xb8\x84\x7e\xdf\x09\x25\xf0\xf1\x30\x9b\x54\xde".*,
        "\xa7\x05\xf0\xe6\x9d\xa9\xa8\xf9\x07\x24\x1a\x2e\x92\x3c\x8c\xc8".*,
        "\x3d\xc4\x7d\x1f\x29\xc4\x48\x46\x1e\x9e\x76\xed\x90\x4f\x67\x11".*,
        "\x0d\x62\xbf\x01\xe6\xfc\x0e\x1a\x0d\x3c\x47\x51\xc5\xd3\x69\x2b".*,
        "\x8c\x03\x46\x8b\xca\x7c\x66\x9e\xe4\xfd\x5e\x08\x4b\xbe\xe7\xb5".*,
        "\x52\x8a\x5b\xb9\x3b\xaf\x2c\x9c\x44\x73\xcc\xe5\xd0\xd2\x2b\xd9".*,
        "\xdf\x6a\x30\x1e\x95\xc9\x5d\xad\x97\xae\x0c\xc8\xc6\x91\x3b\xd8".*,
        "\x80\x11\x89\x90\x2c\x85\x7f\x39\xe7\x35\x91\x28\x5e\x70\xb6\xdb".*,
        "\xe6\x17\x34\x6a\xc9\xc2\x31\xbb\x36\x50\xae\x34\xcc\xca\x0c\x5b".*,
        "\x27\xd9\x34\x37\xef\xb7\x21\xaa\x40\x18\x21\xdc\xec\x5a\xdf\x89".*,
        "\x89\x23\x7d\x9d\xed\x9c\x5e\x78\xd8\xb1\xc9\xb1\x66\xcc\x73\x42".*,
        "\x4a\x6d\x80\x91\xbf\x5e\x7d\x65\x11\x89\xfa\x94\xa2\x50\xb1\x4c".*,
        "\x0e\x33\xf9\x60\x55\xe7\xae\x89\x3f\xfc\x0e\x3d\xcf\x49\x29\x02".*,
        "\xe6\x1c\x43\x2b\x72\x0b\x19\xd1\x8e\xc8\xd8\x4b\xdc\x63\x15\x1b".*,
        "\xf7\xe5\xae\xf5\x49\xf7\x82\xcf\x37\x90\x55\xa6\x08\x26\x9b\x16".*,
        "\x43\x8d\x03\x0f\xd0\xb7\xa5\x4f\xa8\x37\xf2\xad\x20\x1a\x64\x03".*,
        "\xa5\x90\xd3\xee\x4f\xbf\x04\xe3\x24\x7e\x0d\x27\xf2\x86\x42\x3f".*,
        "\x5f\xe2\xc1\xa1\x72\xfe\x93\xc4\xb1\x5c\xd3\x7c\xae\xf9\xf5\x38".*,
        "\x2c\x97\x32\x5c\xbd\x06\xb3\x6e\xb2\x13\x3d\xd0\x8b\x3a\x01\x7c".*,
        "\x92\xc8\x14\x22\x7a\x6b\xca\x94\x9f\xf0\x65\x9f\x00\x2a\xd3\x9e".*,
        "\xdc\xe8\x50\x11\x0b\xd8\x32\x8c\xfb\xd5\x08\x41\xd6\x91\x1d\x87".*,
        "\x67\xf1\x49\x84\xc7\xda\x79\x12\x48\xe3\x2b\xb5\x92\x25\x83\xda".*,
        "\x19\x38\xf2\xcf\x72\xd5\x4e\xe9\x7e\x94\x16\x6f\xa9\x1d\x2a\x36".*,
        "\x74\x48\x1e\x96\x46\xed\x49\xfe\x0f\x62\x24\x30\x16\x04\x69\x8e".*,
        "\x57\xfc\xa5\xde\x98\xa9\xd6\xd8\x00\x64\x38\xd0\x58\x3d\x8a\x1d".*,
        "\x9f\xec\xde\x1c\xef\xdc\x1c\xbe\xd4\x76\x36\x74\xd9\x57\x53\x59".*,
        "\xe3\x04\x0c\x00\xeb\x28\xf1\x53\x66\xca\x73\xcb\xd8\x72\xe7\x40".*,
        "\x76\x97\x00\x9a\x6a\x83\x1d\xfe\xcc\xa9\x1c\x59\x93\x67\x0f\x7a".*,
        "\x58\x53\x54\x23\x21\xf5\x67\xa0\x05\xd5\x47\xa4\xf0\x47\x59\xbd".*,
        "\x51\x50\xd1\x77\x2f\x50\x83\x4a\x50\x3e\x06\x9a\x97\x3f\xbd\x7c".*,
    };

    const siphash = SipHash128(2, 4);

    var buffer: [64]u8 = undefined;
    for (vectors, 0..) |vector, i| {
        buffer[i] = @as(u8, @intCast(i));

        var out: [siphash.mac_length]u8 = undefined;
        siphash.create(&out, buffer[0..i], test_key[0..]);
        try testing.expectEqual(out, vector);
    }
}

test "iterative non-divisible update" {
    var buf: [1024]u8 = undefined;
    for (&buf, 0..) |*e, i| {
        e.* = @as(u8, @truncate(i));
    }

    const key = "0x128dad08f12307";
    const Siphash = SipHash64(2, 4);

    var end: usize = 9;
    while (end < buf.len) : (end += 9) {
        const non_iterative_hash = Siphash.toInt(buf[0..end], key[0..]);

        var siphash = Siphash.init(key);
        var i: usize = 0;
        while (i < end) : (i += 7) {
            siphash.update(buf[i..@min(i + 7, end)]);
        }
        const iterative_hash = siphash.finalInt();

        try std.testing.expectEqual(iterative_hash, non_iterative_hash);
    }
}



---
File: /std/crypto/test.zig
---




---
File: /std/crypto/timing_safe.zig
---

//! Please see this accepted proposal for the long-term plans regarding
//! constant-time operations in Zig: https://github.com/ziglang/zig/issues/1776

const std = @import("../std.zig");
const assert = std.debug.assert;
const Endian = std.builtin.Endian;
const Order = std.math.Order;

/// Compares two arrays in constant time (for a given length) and returns whether they are equal.
/// This function was designed to compare short cryptographic secrets (MACs, signatures).
/// For all other applications, use mem.eql() instead.
pub fn eql(comptime T: type, a: T, b: T) bool {
    switch (@typeInfo(T)) {
        .array => |info| {
            const C = info.child;
            if (@typeInfo(C) != .int) {
                @compileError("Elements to be compared must be integers");
            }
            var acc = @as(C, 0);
            for (a, 0..) |x, i| {
                acc |= x ^ b[i];
            }
            const s = @typeInfo(C).int.bits;
            const Cu = std.meta.Int(.unsigned, s);
            const Cext = std.meta.Int(.unsigned, s + 1);
            return @as(bool, @bitCast(@as(u1, @truncate((@as(Cext, @as(Cu, @bitCast(acc))) -% 1) >> s))));
        },
        .vector => |info| {
            const C = info.child;
            if (@typeInfo(C) != .int) {
                @compileError("Elements to be compared must be integers");
            }
            const acc = @reduce(.Or, a ^ b);
            const s = @typeInfo(C).int.bits;
            const Cu = std.meta.Int(.unsigned, s);
            const Cext = std.meta.Int(.unsigned, s + 1);
            return @as(bool, @bitCast(@as(u1, @truncate((@as(Cext, @as(Cu, @bitCast(acc))) -% 1) >> s))));
        },
        else => {
            @compileError("Only arrays and vectors can be compared");
        },
    }
}

/// Compare two integers serialized as arrays of the same size, in constant time.
/// Returns .lt if a<b, .gt if a>b and .eq if a=b
pub fn compare(comptime T: type, a: []const T, b: []const T, endian: Endian) Order {
    assert(a.len == b.len);
    const bits = switch (@typeInfo(T)) {
        .int => |cinfo| if (cinfo.signedness != .unsigned) @compileError("Elements to be compared must be unsigned") else cinfo.bits,
        else => @compileError("Elements to be compared must be integers"),
    };
    const Cext = std.meta.Int(.unsigned, bits + 1);
    var gt: T = 0;
    var eq: T = 1;
    if (endian == .little) {
        var i = a.len;
        while (i != 0) {
            i -= 1;
            const x1 = a[i];
            const x2 = b[i];
            gt |= @as(T, @truncate((@as(Cext, x2) -% @as(Cext, x1)) >> bits)) & eq;
            eq &= @as(T, @truncate((@as(Cext, (x2 ^ x1)) -% 1) >> bits));
        }
    } else {
        for (a, 0..) |x1, i| {
            const x2 = b[i];
            gt |= @as(T, @truncate((@as(Cext, x2) -% @as(Cext, x1)) >> bits)) & eq;
            eq &= @as(T, @truncate((@as(Cext, (x2 ^ x1)) -% 1) >> bits));
        }
    }
    if (gt != 0) {
        return Order.gt;
    } else if (eq != 0) {
        return Order.eq;
    }
    return Order.lt;
}

/// Add two integers serialized as arrays of the same size, in constant time.
/// The result is stored into `result`, and `true` is returned if an overflow occurred.
pub fn add(comptime T: type, a: []const T, b: []const T, result: []T, endian: Endian) bool {
    const len = a.len;
    assert(len == b.len and len == result.len);
    var carry: u1 = 0;
    if (endian == .little) {
        var i: usize = 0;
        while (i < len) : (i += 1) {
            const ov1 = @addWithOverflow(a[i], b[i]);
            const ov2 = @addWithOverflow(ov1[0], carry);
            result[i] = ov2[0];
            carry = ov1[1] | ov2[1];
        }
    } else {
        var i: usize = len;
        while (i != 0) {
            i -= 1;
            const ov1 = @addWithOverflow(a[i], b[i]);
            const ov2 = @addWithOverflow(ov1[0], carry);
            result[i] = ov2[0];
            carry = ov1[1] | ov2[1];
        }
    }
    return @as(bool, @bitCast(carry));
}

/// Subtract two integers serialized as arrays of the same size, in constant time.
/// The result is stored into `result`, and `true` is returned if an underflow occurred.
pub fn sub(comptime T: type, a: []const T, b: []const T, result: []T, endian: Endian) bool {
    const len = a.len;
    assert(len == b.len and len == result.len);
    var borrow: u1 = 0;
    if (endian == .little) {
        var i: usize = 0;
        while (i < len) : (i += 1) {
            const ov1 = @subWithOverflow(a[i], b[i]);
            const ov2 = @subWithOverflow(ov1[0], borrow);
            result[i] = ov2[0];
            borrow = ov1[1] | ov2[1];
        }
    } else {
        var i: usize = len;
        while (i != 0) {
            i -= 1;
            const ov1 = @subWithOverflow(a[i], b[i]);
            const ov2 = @subWithOverflow(ov1[0], borrow);
            result[i] = ov2[0];
            borrow = ov1[1] | ov2[1];
        }
    }
    return @as(bool, @bitCast(borrow));
}

fn markSecret(ptr: anytype, comptime action: enum { classify, declassify }) void {
    const t = @typeInfo(@TypeOf(ptr));
    if (t != .pointer) @compileError("Pointer expected - Found: " ++ @typeName(@TypeOf(ptr)));
    const p = t.pointer;
    if (p.is_allowzero) @compileError("A nullable pointer is always assumed to leak information via side channels");
    const child = @typeInfo(p.child);

    switch (child) {
        .void, .null, .comptime_int, .comptime_float => return,
        .pointer => {
            if (child.pointer.size == .Slice) {
                @compileError("Found pointer to pointer. If the intent was to pass a slice, maybe remove the leading & in the function call");
            }
            @compileError("A pointer value is always assumed leak information via side channels");
        },
        else => {
            const mem8: *const [@sizeOf(@TypeOf(ptr.*))]u8 = @ptrCast(@constCast(ptr));
            if (action == .classify) {
                std.valgrind.memcheck.makeMemUndefined(mem8);
            } else {
                std.valgrind.memcheck.makeMemDefined(mem8);
            }
        },
    }
}

/// Mark a value as sensitive or secret, helping to detect potential side-channel vulnerabilities.
///
/// When Valgrind is enabled, this function allows for the detection of conditional jumps or lookups
/// that depend on secrets or secret-derived data. Violations are reported by Valgrind as operations
/// relying on uninitialized values.
///
/// If Valgrind is disabled, it has no effect.
///
/// Use this function to verify that cryptographic operations perform constant-time arithmetic on sensitive data,
/// ensuring the confidentiality of secrets and preventing information leakage through side channels.
pub fn classify(ptr: anytype) void {
    markSecret(ptr, .classify);
}

/// Mark a value as non-sensitive or public, indicating it's safe from side-channel attacks.
///
/// Signals that a value has been securely processed and is no longer confidential, allowing for
/// relaxed handling without fear of information leakage through conditional jumps or lookups.
pub fn declassify(ptr: anytype) void {
    markSecret(ptr, .declassify);
}

test eql {
    const io = std.testing.io;
    const expect = std.testing.expect;
    var a: [100]u8 = undefined;
    var b: [100]u8 = undefined;
    io.random(&a);
    io.random(&b);
    try expect(!eql([100]u8, a, b));
    a = b;
    try expect(eql([100]u8, a, b));
}

test "eql (vectors)" {
    const io = std.testing.io;
    const expect = std.testing.expect;
    var a: [100]u8 = undefined;
    var b: [100]u8 = undefined;
    io.random(&a);
    io.random(&b);
    const v1: @Vector(100, u8) = a;
    const v2: @Vector(100, u8) = b;
    try expect(!eql(@Vector(100, u8), v1, v2));
    const v3: @Vector(100, u8) = a;
    try expect(eql(@Vector(100, u8), v1, v3));
}

test compare {
    const expectEqual = std.testing.expectEqual;
    var a = [_]u8{10} ** 32;
    var b = [_]u8{10} ** 32;
    try expectEqual(compare(u8, &a, &b, .big), .eq);
    try expectEqual(compare(u8, &a, &b, .little), .eq);
    a[31] = 1;
    try expectEqual(compare(u8, &a, &b, .big), .lt);
    try expectEqual(compare(u8, &a, &b, .little), .lt);
    a[0] = 20;
    try expectEqual(compare(u8, &a, &b, .big), .gt);
    try expectEqual(compare(u8, &a, &b, .little), .lt);
}

test "add and sub" {
    const io = std.testing.io;

    const expectEqual = std.testing.expectEqual;
    const expectEqualSlices = std.testing.expectEqualSlices;
    const len = 32;
    var a: [len]u8 = undefined;
    var b: [len]u8 = undefined;
    var c: [len]u8 = undefined;
    const zero = [_]u8{0} ** len;
    var iterations: usize = 100;
    while (iterations != 0) : (iterations -= 1) {
        io.random(&a);
        io.random(&b);
        const endian = if (iterations % 2 == 0) Endian.big else Endian.little;
        _ = sub(u8, &a, &b, &c, endian); // a-b
        _ = add(u8, &c, &b, &c, endian); // (a-b)+b
        try expectEqualSlices(u8, &c, &a);
        const borrow = sub(u8, &c, &a, &c, endian); // ((a-b)+b)-a
        try expectEqualSlices(u8, &c, &zero);
        try expectEqual(borrow, false);
    }
}

test classify {
    const io = std.testing.io;
    const expect = std.testing.expect;

    var secret: [32]u8 = undefined;
    io.random(&secret);

    // Input of the hash function is marked as secret
    classify(&secret);

    var out: [32]u8 = undefined;
    std.crypto.hash.sha3.TurboShake128(null).hash(&secret, &out, .{});

    // Output of the hash function is derived from secret data, so
    // it will automatically be considered secret as well. But it can be
    // declassified; the input itself will still be considered secret.
    declassify(&out);

    // Comparing public data in non-constant time is acceptable.
    try expect(!std.mem.eql(u8, &out, &[_]u8{0} ** out.len));

    // Comparing secret data must be done in constant time. The result
    // is going to be considered as secret as well.
    var res = std.crypto.timing_safe.eql([32]u8, out, secret);

    // If we want to make a conditional jump based on a secret,
    // it has to be declassified.
    declassify(&res);
    try expect(!res);

    // Once a secret has been declassified, a comparison in
    // non-constant time is fine.
    declassify(&secret);
    try expect(!std.mem.eql(u8, &out, &secret));
}



---
File: /std/crypto/tls.zig
---

//! Plaintext:
//! * type: ContentType
//! * legacy_record_version: u16 = 0x0303,
//! * length: u16,
//!   - The length (in bytes) of the following TLSPlaintext.fragment.  The
//!     length MUST NOT exceed 2^14 bytes.
//! * fragment: opaque
//!   - the data being transmitted
//!
//! Ciphertext
//! * ContentType opaque_type = application_data; /* 23 */
//! * ProtocolVersion legacy_record_version = 0x0303; /* TLS v1.2 */
//! * uint16 length;
//! * opaque encrypted_record[TLSCiphertext.length];
//!
//! Handshake:
//! * type: HandshakeType
//! * length: u24
//! * data: opaque
//!
//! ServerHello:
//! * ProtocolVersion legacy_version = 0x0303;
//! * Random random;
//! * opaque legacy_session_id_echo<0..32>;
//! * CipherSuite cipher_suite;
//! * uint8 legacy_compression_method = 0;
//! * Extension extensions<6..2^16-1>;
//!
//! Extension:
//! * ExtensionType extension_type;
//! * opaque extension_data<0..2^16-1>;

const std = @import("../std.zig");
const Tls = @This();
const mem = std.mem;
const crypto = std.crypto;
const assert = std.debug.assert;

pub const Client = @import("tls/Client.zig");

pub const record_header_len = 5;
pub const max_ciphertext_inner_record_len = 1 << 14;
pub const max_ciphertext_len = max_ciphertext_inner_record_len + 256;
pub const max_ciphertext_record_len = max_ciphertext_len + record_header_len;
pub const hello_retry_request_sequence = [32]u8{
    0xCF, 0x21, 0xAD, 0x74, 0xE5, 0x9A, 0x61, 0x11, 0xBE, 0x1D, 0x8C, 0x02, 0x1E, 0x65, 0xB8, 0x91,
    0xC2, 0xA2, 0x11, 0x16, 0x7A, 0xBB, 0x8C, 0x5E, 0x07, 0x9E, 0x09, 0xE2, 0xC8, 0xA8, 0x33, 0x9C,
};

pub const close_notify_alert = [_]u8{
    @intFromEnum(Alert.Level.warning),
    @intFromEnum(Alert.Description.close_notify),
};

pub const ProtocolVersion = enum(u16) {
    tls_1_0 = 0x0301,
    tls_1_1 = 0x0302,
    tls_1_2 = 0x0303,
    tls_1_3 = 0x0304,
    _,
};

pub const ContentType = enum(u8) {
    invalid = 0,
    change_cipher_spec = 20,
    alert = 21,
    handshake = 22,
    application_data = 23,
    _,
};

pub const HandshakeType = enum(u8) {
    hello_request = 0,
    client_hello = 1,
    server_hello = 2,
    new_session_ticket = 4,
    end_of_early_data = 5,
    encrypted_extensions = 8,
    certificate = 11,
    server_key_exchange = 12,
    certificate_request = 13,
    server_hello_done = 14,
    certificate_verify = 15,
    client_key_exchange = 16,
    finished = 20,
    key_update = 24,
    message_hash = 254,
    _,
};

pub const ExtensionType = enum(u16) {
    /// RFC 6066
    server_name = 0,
    /// RFC 6066
    max_fragment_length = 1,
    /// RFC 6066
    status_request = 5,
    /// RFC 8422, 7919
    supported_groups = 10,
    /// RFC 8446
    signature_algorithms = 13,
    /// RFC 5764
    use_srtp = 14,
    /// RFC 6520
    heartbeat = 15,
    /// RFC 7301
    application_layer_protocol_negotiation = 16,
    /// RFC 6962
    signed_certificate_timestamp = 18,
    /// RFC 7250
    client_certificate_type = 19,
    /// RFC 7250
    server_certificate_type = 20,
    /// RFC 7685
    padding = 21,
    /// RFC 8446
    pre_shared_key = 41,
    /// RFC 8446
    early_data = 42,
    /// RFC 8446
    supported_versions = 43,
    /// RFC 8446
    cookie = 44,
    /// RFC 8446
    psk_key_exchange_modes = 45,
    /// RFC 8446
    certificate_authorities = 47,
    /// RFC 8446
    oid_filters = 48,
    /// RFC 8446
    post_handshake_auth = 49,
    /// RFC 8446
    signature_algorithms_cert = 50,
    /// RFC 8446
    key_share = 51,
    /// RFC 9000
    quic_transport_parameters = 57,

    _,
};

pub const Alert = struct {
    level: Level,
    description: Description,

    pub const Level = enum(u8) {
        warning = 1,
        fatal = 2,
        _,
    };

    pub const Description = enum(u8) {
        pub const Error = error{
            TlsAlertUnexpectedMessage,
            TlsAlertBadRecordMac,
            TlsAlertRecordOverflow,
            TlsAlertHandshakeFailure,
            TlsAlertBadCertificate,
            TlsAlertUnsupportedCertificate,
            TlsAlertCertificateRevoked,
            TlsAlertCertificateExpired,
            TlsAlertCertificateUnknown,
            TlsAlertIllegalParameter,
            TlsAlertUnknownCa,
            TlsAlertAccessDenied,
            TlsAlertDecodeError,
            TlsAlertDecryptError,
            TlsAlertProtocolVersion,
            TlsAlertInsufficientSecurity,
            TlsAlertInternalError,
            TlsAlertInappropriateFallback,
            TlsAlertMissingExtension,
            TlsAlertUnsupportedExtension,
            TlsAlertUnrecognizedName,
            TlsAlertBadCertificateStatusResponse,
            TlsAlertUnknownPskIdentity,
            TlsAlertCertificateRequired,
            TlsAlertNoApplicationProtocol,
            TlsAlertUnknown,
        };

        close_notify = 0,
        unexpected_message = 10,
        bad_record_mac = 20,
        record_overflow = 22,
        handshake_failure = 40,
        bad_certificate = 42,
        unsupported_certificate = 43,
        certificate_revoked = 44,
        certificate_expired = 45,
        certificate_unknown = 46,
        illegal_parameter = 47,
        unknown_ca = 48,
        access_denied = 49,
        decode_error = 50,
        decrypt_error = 51,
        protocol_version = 70,
        insufficient_security = 71,
        internal_error = 80,
        inappropriate_fallback = 86,
        user_canceled = 90,
        missing_extension = 109,
        unsupported_extension = 110,
        unrecognized_name = 112,
        bad_certificate_status_response = 113,
        unknown_psk_identity = 115,
        certificate_required = 116,
        no_application_protocol = 120,
        _,

        pub fn toError(description: Description) Error!void {
            switch (description) {
                .close_notify => {}, // not an error
                .unexpected_message => return error.TlsAlertUnexpectedMessage,
                .bad_record_mac => return error.TlsAlertBadRecordMac,
                .record_overflow => return error.TlsAlertRecordOverflow,
                .handshake_failure => return error.TlsAlertHandshakeFailure,
                .bad_certificate => return error.TlsAlertBadCertificate,
                .unsupported_certificate => return error.TlsAlertUnsupportedCertificate,
                .certificate_revoked => return error.TlsAlertCertificateRevoked,
                .certificate_expired => return error.TlsAlertCertificateExpired,
                .certificate_unknown => return error.TlsAlertCertificateUnknown,
                .illegal_parameter => return error.TlsAlertIllegalParameter,
                .unknown_ca => return error.TlsAlertUnknownCa,
                .access_denied => return error.TlsAlertAccessDenied,
                .decode_error => return error.TlsAlertDecodeError,
                .decrypt_error => return error.TlsAlertDecryptError,
                .protocol_version => return error.TlsAlertProtocolVersion,
                .insufficient_security => return error.TlsAlertInsufficientSecurity,
                .internal_error => return error.TlsAlertInternalError,
                .inappropriate_fallback => return error.TlsAlertInappropriateFallback,
                .user_canceled => {}, // not an error
                .missing_extension => return error.TlsAlertMissingExtension,
                .unsupported_extension => return error.TlsAlertUnsupportedExtension,
                .unrecognized_name => return error.TlsAlertUnrecognizedName,
                .bad_certificate_status_response => return error.TlsAlertBadCertificateStatusResponse,
                .unknown_psk_identity => return error.TlsAlertUnknownPskIdentity,
                .certificate_required => return error.TlsAlertCertificateRequired,
                .no_application_protocol => return error.TlsAlertNoApplicationProtocol,
                _ => return error.TlsAlertUnknown,
            }
        }
    };
};

pub const SignatureScheme = enum(u16) {
    // RSASSA-PKCS1-v1_5 algorithms
    rsa_pkcs1_sha256 = 0x0401,
    rsa_pkcs1_sha384 = 0x0501,
    rsa_pkcs1_sha512 = 0x0601,

    // ECDSA algorithms
    ecdsa_secp256r1_sha256 = 0x0403,
    ecdsa_secp384r1_sha384 = 0x0503,
    ecdsa_secp521r1_sha512 = 0x0603,

    // RSASSA-PSS algorithms with public key OID rsaEncryption
    rsa_pss_rsae_sha256 = 0x0804,
    rsa_pss_rsae_sha384 = 0x0805,
    rsa_pss_rsae_sha512 = 0x0806,

    // EdDSA algorithms
    ed25519 = 0x0807,
    ed448 = 0x0808,

    // RSASSA-PSS algorithms with public key OID RSASSA-PSS
    rsa_pss_pss_sha256 = 0x0809,
    rsa_pss_pss_sha384 = 0x080a,
    rsa_pss_pss_sha512 = 0x080b,

    // Legacy algorithms
    rsa_pkcs1_sha1 = 0x0201,
    ecdsa_sha1 = 0x0203,

    ecdsa_brainpoolP256r1tls13_sha256 = 0x081a,
    ecdsa_brainpoolP384r1tls13_sha384 = 0x081b,
    ecdsa_brainpoolP512r1tls13_sha512 = 0x081c,

    rsa_sha224 = 0x0301,
    dsa_sha224 = 0x0302,
    ecdsa_sha224 = 0x0303,
    dsa_sha256 = 0x0402,
    dsa_sha384 = 0x0502,
    dsa_sha512 = 0x0602,

    _,
};

pub const NamedGroup = enum(u16) {
    // Elliptic Curve Groups (ECDHE)
    secp256r1 = 0x0017,
    secp384r1 = 0x0018,
    secp521r1 = 0x0019,
    x25519 = 0x001D,
    x448 = 0x001E,

    // Finite Field Groups (DHE)
    ffdhe2048 = 0x0100,
    ffdhe3072 = 0x0101,
    ffdhe4096 = 0x0102,
    ffdhe6144 = 0x0103,
    ffdhe8192 = 0x0104,

    // Hybrid post-quantum key agreements
    secp256r1_ml_kem256 = 0x11EB,
    x25519_ml_kem768 = 0x11EC,

    _,
};

pub const PskKeyExchangeMode = enum(u8) {
    psk_ke = 0,
    psk_dhe_ke = 1,
    _,
};

pub const CipherSuite = enum(u16) {
    RSA_WITH_AES_128_CBC_SHA = 0x002F,
    DHE_RSA_WITH_AES_128_CBC_SHA = 0x0033,
    RSA_WITH_AES_256_CBC_SHA = 0x0035,
    DHE_RSA_WITH_AES_256_CBC_SHA = 0x0039,
    RSA_WITH_AES_128_CBC_SHA256 = 0x003C,
    RSA_WITH_AES_256_CBC_SHA256 = 0x003D,
    DHE_RSA_WITH_AES_128_CBC_SHA256 = 0x0067,
    DHE_RSA_WITH_AES_256_CBC_SHA256 = 0x006B,
    RSA_WITH_AES_128_GCM_SHA256 = 0x009C,
    RSA_WITH_AES_256_GCM_SHA384 = 0x009D,
    DHE_RSA_WITH_AES_128_GCM_SHA256 = 0x009E,
    DHE_RSA_WITH_AES_256_GCM_SHA384 = 0x009F,
    EMPTY_RENEGOTIATION_INFO_SCSV = 0x00FF,

    AES_128_GCM_SHA256 = 0x1301,
    AES_256_GCM_SHA384 = 0x1302,
    CHACHA20_POLY1305_SHA256 = 0x1303,
    AES_128_CCM_SHA256 = 0x1304,
    AES_128_CCM_8_SHA256 = 0x1305,
    AEGIS_256_SHA512 = 0x1306,
    AEGIS_128L_SHA256 = 0x1307,

    ECDHE_ECDSA_WITH_AES_128_CBC_SHA = 0xC009,
    ECDHE_ECDSA_WITH_AES_256_CBC_SHA = 0xC00A,
    ECDHE_RSA_WITH_AES_128_CBC_SHA = 0xC013,
    ECDHE_RSA_WITH_AES_256_CBC_SHA = 0xC014,
    ECDHE_ECDSA_WITH_AES_128_CBC_SHA256 = 0xC023,
    ECDHE_ECDSA_WITH_AES_256_CBC_SHA384 = 0xC024,
    ECDHE_RSA_WITH_AES_128_CBC_SHA256 = 0xC027,
    ECDHE_RSA_WITH_AES_256_CBC_SHA384 = 0xC028,
    ECDHE_ECDSA_WITH_AES_128_GCM_SHA256 = 0xC02B,
    ECDHE_ECDSA_WITH_AES_256_GCM_SHA384 = 0xC02C,
    ECDHE_RSA_WITH_AES_128_GCM_SHA256 = 0xC02F,
    ECDHE_RSA_WITH_AES_256_GCM_SHA384 = 0xC030,

    ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256 = 0xCCA8,
    ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256 = 0xCCA9,
    DHE_RSA_WITH_CHACHA20_POLY1305_SHA256 = 0xCCAA,

    _,

    pub const With = enum {
        AES_128_CBC_SHA,
        AES_256_CBC_SHA,
        AES_128_CBC_SHA256,
        AES_256_CBC_SHA256,
        AES_256_CBC_SHA384,

        AES_128_GCM_SHA256,
        AES_256_GCM_SHA384,

        CHACHA20_POLY1305_SHA256,

        AES_128_CCM_SHA256,
        AES_128_CCM_8_SHA256,

        AEGIS_256_SHA512,
        AEGIS_128L_SHA256,
    };

    pub fn with(cipher_suite: CipherSuite) With {
        return switch (cipher_suite) {
            .RSA_WITH_AES_128_CBC_SHA,
            .DHE_RSA_WITH_AES_128_CBC_SHA,
            .ECDHE_ECDSA_WITH_AES_128_CBC_SHA,
            .ECDHE_RSA_WITH_AES_128_CBC_SHA,
            => .AES_128_CBC_SHA,
            .RSA_WITH_AES_256_CBC_SHA,
            .DHE_RSA_WITH_AES_256_CBC_SHA,
            .ECDHE_ECDSA_WITH_AES_256_CBC_SHA,
            .ECDHE_RSA_WITH_AES_256_CBC_SHA,
            => .AES_256_CBC_SHA,
            .RSA_WITH_AES_128_CBC_SHA256,
            .DHE_RSA_WITH_AES_128_CBC_SHA256,
            .ECDHE_ECDSA_WITH_AES_128_CBC_SHA256,
            .ECDHE_RSA_WITH_AES_128_CBC_SHA256,
            => .AES_128_CBC_SHA256,
            .RSA_WITH_AES_256_CBC_SHA256,
            .DHE_RSA_WITH_AES_256_CBC_SHA256,
            => .AES_256_CBC_SHA256,
            .ECDHE_ECDSA_WITH_AES_256_CBC_SHA384,
            .ECDHE_RSA_WITH_AES_256_CBC_SHA384,
            => .AES_256_CBC_SHA384,

            .RSA_WITH_AES_128_GCM_SHA256,
            .DHE_RSA_WITH_AES_128_GCM_SHA256,
            .AES_128_GCM_SHA256,
            .ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
            .ECDHE_RSA_WITH_AES_128_GCM_SHA256,
            => .AES_128_GCM_SHA256,
            .RSA_WITH_AES_256_GCM_SHA384,
            .DHE_RSA_WITH_AES_256_GCM_SHA384,
            .AES_256_GCM_SHA384,
            .ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
            .ECDHE_RSA_WITH_AES_256_GCM_SHA384,
            => .AES_256_GCM_SHA384,

            .CHACHA20_POLY1305_SHA256,
            .ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
            .ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256,
            .DHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
            => .CHACHA20_POLY1305_SHA256,

            .AES_128_CCM_SHA256 => .AES_128_CCM_SHA256,
            .AES_128_CCM_8_SHA256 => .AES_128_CCM_8_SHA256,

            .AEGIS_256_SHA512 => .AEGIS_256_SHA512,
            .AEGIS_128L_SHA256 => .AEGIS_128L_SHA256,

            .EMPTY_RENEGOTIATION_INFO_SCSV => unreachable,
            _ => unreachable,
        };
    }
};

pub const CompressionMethod = enum(u8) {
    null = 0,
    _,
};

pub const CertificateType = enum(u8) {
    X509 = 0,
    RawPublicKey = 2,
    _,
};

pub const KeyUpdateRequest = enum(u8) {
    update_not_requested = 0,
    update_requested = 1,
    _,
};

pub const ChangeCipherSpecType = enum(u8) {
    change_cipher_spec = 1,
    _,
};

pub fn HandshakeCipherT(comptime AeadType: type, comptime HashType: type, comptime explicit_iv_length: comptime_int) type {
    return struct {
        pub const A = ApplicationCipherT(AeadType, HashType, explicit_iv_length);

        transcript_hash: A.Hash,
        version: union {
            tls_1_2: struct {
                expected_server_verify_data: [A.verify_data_length]u8,
                app_cipher: A.Tls_1_2,
            },
            tls_1_3: struct {
                handshake_secret: [A.Hkdf.prk_length]u8,
                master_secret: [A.Hkdf.prk_length]u8,
                client_handshake_key: [A.AEAD.key_length]u8,
                server_handshake_key: [A.AEAD.key_length]u8,
                client_finished_key: [A.Hmac.key_length]u8,
                server_finished_key: [A.Hmac.key_length]u8,
                client_handshake_iv: [A.AEAD.nonce_length]u8,
                server_handshake_iv: [A.AEAD.nonce_length]u8,
            },
        },
    };
}

pub const HandshakeCipher = union(enum) {
    AES_128_GCM_SHA256: HandshakeCipherT(crypto.aead.aes_gcm.Aes128Gcm, crypto.hash.sha2.Sha256, 8),
    AES_256_GCM_SHA384: HandshakeCipherT(crypto.aead.aes_gcm.Aes256Gcm, crypto.hash.sha2.Sha384, 8),
    CHACHA20_POLY1305_SHA256: HandshakeCipherT(crypto.aead.chacha_poly.ChaCha20Poly1305, crypto.hash.sha2.Sha256, 0),
    AEGIS_256_SHA512: HandshakeCipherT(crypto.aead.aegis.Aegis256, crypto.hash.sha2.Sha512, 0),
    AEGIS_128L_SHA256: HandshakeCipherT(crypto.aead.aegis.Aegis128L, crypto.hash.sha2.Sha256, 0),
};

pub fn ApplicationCipherT(comptime AeadType: type, comptime HashType: type, comptime explicit_iv_length: comptime_int) type {
    return union {
        pub const AEAD = AeadType;
        pub const Hash = HashType;
        pub const Hmac = crypto.auth.hmac.Hmac(Hash);
        pub const Hkdf = crypto.kdf.hkdf.Hkdf(Hmac);

        pub const enc_key_length = AEAD.key_length;
        pub const fixed_iv_length = AEAD.nonce_length - explicit_iv_length;
        pub const record_iv_length = explicit_iv_length;
        pub const mac_length = AEAD.tag_length;
        pub const mac_key_length = Hmac.key_length_min;
        pub const verify_data_length = 12;

        tls_1_2: Tls_1_2,
        tls_1_3: Tls_1_3,

        pub const Tls_1_2 = extern struct {
            client_write_MAC_key: [mac_key_length]u8,
            server_write_MAC_key: [mac_key_length]u8,
            client_write_key: [enc_key_length]u8,
            server_write_key: [enc_key_length]u8,
            client_write_IV: [fixed_iv_length]u8,
            server_write_IV: [fixed_iv_length]u8,
            // non-standard entropy
            client_salt: [record_iv_length]u8,
        };

        pub const Tls_1_3 = struct {
            client_secret: [Hash.digest_length]u8,
            server_secret: [Hash.digest_length]u8,
            client_key: [AEAD.key_length]u8,
            server_key: [AEAD.key_length]u8,
            client_iv: [AEAD.nonce_length]u8,
            server_iv: [AEAD.nonce_length]u8,
        };
    };
}

/// Encryption parameters for application traffic.
pub const ApplicationCipher = union(enum) {
    AES_128_GCM_SHA256: ApplicationCipherT(crypto.aead.aes_gcm.Aes128Gcm, crypto.hash.sha2.Sha256, 8),
    AES_256_GCM_SHA384: ApplicationCipherT(crypto.aead.aes_gcm.Aes256Gcm, crypto.hash.sha2.Sha384, 8),
    CHACHA20_POLY1305_SHA256: ApplicationCipherT(crypto.aead.chacha_poly.ChaCha20Poly1305, crypto.hash.sha2.Sha256, 0),
    AEGIS_256_SHA512: ApplicationCipherT(crypto.aead.aegis.Aegis256, crypto.hash.sha2.Sha512, 0),
    AEGIS_128L_SHA256: ApplicationCipherT(crypto.aead.aegis.Aegis128L, crypto.hash.sha2.Sha256, 0),
};

pub fn hmacExpandLabel(
    comptime Hmac: type,
    secret: []const u8,
    label_then_seed: []const []const u8,
    comptime len: usize,
) [len]u8 {
    const initial_hmac: Hmac = .init(secret);
    var a: [Hmac.mac_length]u8 = undefined;
    var result: [std.mem.alignForwardAnyAlign(usize, len, Hmac.mac_length)]u8 = undefined;
    var index: usize = 0;
    while (index < result.len) : (index += Hmac.mac_length) {
        var a_hmac = initial_hmac;
        if (index > 0) a_hmac.update(&a) else for (label_then_seed) |part| a_hmac.update(part);
        a_hmac.final(&a);

        var result_hmac = initial_hmac;
        result_hmac.update(&a);
        for (label_then_seed) |part| result_hmac.update(part);
        result_hmac.final(result[index..][0..Hmac.mac_length]);
    }
    return result[0..len].*;
}

pub fn hkdfExpandLabel(
    comptime Hkdf: type,
    key: [Hkdf.prk_length]u8,
    label: []const u8,
    context: []const u8,
    comptime len: usize,
) [len]u8 {
    const max_label_len = 255;
    const max_context_len = 255;
    const tls13 = "tls13 ";
    var buf: [2 + 1 + tls13.len + max_label_len + 1 + max_context_len]u8 = undefined;
    mem.writeInt(u16, buf[0..2], len, .big);
    buf[2] = @as(u8, @intCast(tls13.len + label.len));
    buf[3..][0..tls13.len].* = tls13.*;
    var i: usize = 3 + tls13.len;
    @memcpy(buf[i..][0..label.len], label);
    i += label.len;
    buf[i] = @as(u8, @intCast(context.len));
    i += 1;
    @memcpy(buf[i..][0..context.len], context);
    i += context.len;

    var result: [len]u8 = undefined;
    Hkdf.expand(&result, buf[0..i], key);
    return result;
}

pub fn emptyHash(comptime Hash: type) [Hash.digest_length]u8 {
    var result: [Hash.digest_length]u8 = undefined;
    Hash.hash(&.{}, &result, .{});
    return result;
}

pub fn hmac(comptime Hmac: type, message: []const u8, key: [Hmac.key_length]u8) [Hmac.mac_length]u8 {
    var result: [Hmac.mac_length]u8 = undefined;
    Hmac.create(&result, message, &key);
    return result;
}

pub fn extension(et: ExtensionType, bytes: anytype) [2 + 2 + bytes.len]u8 {
    return int(u16, @intFromEnum(et)) ++ array(u16, u8, bytes);
}

pub fn array(
    comptime Len: type,
    comptime Elem: type,
    elems: anytype,
) [@divExact(@bitSizeOf(Len), 8) + @divExact(@bitSizeOf(Elem), 8) * elems.len]u8 {
    const len_size = @divExact(@bitSizeOf(Len), 8);
    const elem_size = @divExact(@bitSizeOf(Elem), 8);
    var arr: [len_size + elem_size * elems.len]u8 = undefined;
    std.mem.writeInt(Len, arr[0..len_size], @intCast(elem_size * elems.len), .big);
    const ElemInt = @Int(.unsigned, @bitSizeOf(Elem));
    for (0.., @as([elems.len]Elem, elems)) |index, elem| {
        std.mem.writeInt(
            ElemInt,
            arr[len_size + elem_size * index ..][0..elem_size],
            switch (@typeInfo(Elem)) {
                .int => @as(Elem, elem),
                .@"enum" => @intFromEnum(@as(Elem, elem)),
                else => @bitCast(@as(Elem, elem)),
            },
            .big,
        );
    }
    return arr;
}

pub fn int(comptime Int: type, val: Int) [@divExact(@bitSizeOf(Int), 8)]u8 {
    var arr: [@divExact(@bitSizeOf(Int), 8)]u8 = undefined;
    std.mem.writeInt(Int, &arr, val, .big);
    return arr;
}

/// An abstraction to ensure that protocol-parsing code does not perform an
/// out-of-bounds read.
pub const Decoder = struct {
    buf: []u8,
    /// Points to the next byte in buffer that will be decoded.
    idx: usize = 0,
    /// Up to this point in `buf` we have already checked that `cap` is greater than it.
    our_end: usize = 0,
    /// Beyond this point in `buf` is extra tag-along bytes beyond the amount we
    /// requested with `readAtLeast`.
    their_end: usize = 0,
    /// Points to the end within buffer that has been filled. Beyond this point
    /// in buf is undefined bytes.
    cap: usize = 0,
    /// Debug helper to prevent illegal calls to read functions.
    disable_reads: bool = false,

    pub fn fromTheirSlice(buf: []u8) Decoder {
        return .{
            .buf = buf,
            .their_end = buf.len,
            .cap = buf.len,
            .disable_reads = true,
        };
    }

    /// Use this function to increase `their_end`.
    pub fn readAtLeast(d: *Decoder, stream: *std.Io.Reader, their_amt: usize) !void {
        assert(!d.disable_reads);
        const existing_amt = d.cap - d.idx;
        d.their_end = d.idx + their_amt;
        if (their_amt <= existing_amt) return;
        const request_amt = their_amt - existing_amt;
        const dest = d.buf[d.cap..];
        if (request_amt > dest.len) return error.TlsRecordOverflow;
        stream.readSlice(dest[0..request_amt]) catch |err| switch (err) {
            error.EndOfStream => return error.TlsConnectionTruncated,
            error.ReadFailed => return error.ReadFailed,
        };
        d.cap += request_amt;
    }

    /// Same as `readAtLeast` but also increases `our_end` by exactly `our_amt`.
    /// Use when `our_amt` is calculated by us, not by them.
    pub fn readAtLeastOurAmt(d: *Decoder, stream: *std.Io.Reader, our_amt: usize) !void {
        assert(!d.disable_reads);
        try readAtLeast(d, stream, our_amt);
        d.our_end = d.idx + our_amt;
    }

    /// Use this function to increase `our_end`.
    /// This should always be called with an amount provided by us, not them.
    pub fn ensure(d: *Decoder, amt: usize) !void {
        d.our_end = @max(d.idx + amt, d.our_end);
        if (d.our_end > d.their_end) return error.TlsDecodeError;
    }

    /// Use this function to increase `idx`.
    pub fn decode(d: *Decoder, comptime T: type) T {
        switch (@typeInfo(T)) {
            .int => |info| switch (info.bits) {
                8 => {
                    skip(d, 1);
                    return d.buf[d.idx - 1];
                },
                16 => {
                    skip(d, 2);
                    const b0: u16 = d.buf[d.idx - 2];
                    const b1: u16 = d.buf[d.idx - 1];
                    return (b0 << 8) | b1;
                },
                24 => {
                    skip(d, 3);
                    const b0: u24 = d.buf[d.idx - 3];
                    const b1: u24 = d.buf[d.idx - 2];
                    const b2: u24 = d.buf[d.idx - 1];
                    return (b0 << 16) | (b1 << 8) | b2;
                },
                else => @compileError("unsupported int type: " ++ @typeName(T)),
            },
            .@"enum" => |info| {
                if (info.is_exhaustive) @compileError("exhaustive enum cannot be used");
                return @enumFromInt(d.decode(info.tag_type));
            },
            else => @compileError("unsupported type: " ++ @typeName(T)),
        }
    }

    /// Use this function to increase `idx`.
    pub fn array(d: *Decoder, comptime len: usize) *[len]u8 {
        skip(d, len);
        return d.buf[d.idx - len ..][0..len];
    }

    /// Use this function to increase `idx`.
    pub fn slice(d: *Decoder, len: usize) []u8 {
        skip(d, len);
        return d.buf[d.idx - len ..][0..len];
    }

    /// Use this function to increase `idx`.
    pub fn skip(d: *Decoder, amt: usize) void {
        d.idx += amt;
        assert(d.idx <= d.our_end); // insufficient ensured bytes
    }

    pub fn eof(d: Decoder) bool {
        assert(d.our_end <= d.their_end);
        assert(d.idx <= d.our_end);
        return d.idx == d.their_end;
    }

    /// Provide the length they claim, and receive a sub-decoder specific to that slice.
    /// The parent decoder is advanced to the end.
    pub fn sub(d: *Decoder, their_len: usize) !Decoder {
        const end = d.idx + their_len;
        if (end > d.their_end) return error.TlsDecodeError;
        const sub_buf = d.buf[d.idx..end];
        d.idx = end;
        d.our_end = end;
        return fromTheirSlice(sub_buf);
    }

    pub fn rest(d: Decoder) []u8 {
        return d.buf[d.idx..d.cap];
    }
};



---
File: /std/debug/Dwarf/Unwind/VirtualMachine.zig
---

//! Virtual machine that evaluates DWARF call frame instructions

/// See section 6.4.1 of the DWARF5 specification for details on each
pub const RegisterRule = union(enum) {
    /// The spec says that the default rule for each column is the undefined rule.
    /// However, it also allows ABI / compiler authors to specify alternate defaults, so
    /// there is a distinction made here.
    default,
    undefined,
    same_value,
    /// offset(N)
    offset: i64,
    /// val_offset(N)
    val_offset: i64,
    /// register(R)
    register: u8,
    /// expression(E)
    expression: []const u8,
    /// val_expression(E)
    val_expression: []const u8,
};

pub const CfaRule = union(enum) {
    none,
    reg_off: struct {
        register: u8,
        offset: i64,
    },
    expression: []const u8,
};

/// Each row contains unwinding rules for a set of registers.
pub const Row = struct {
    /// Offset from `FrameDescriptionEntry.pc_begin`
    offset: u64 = 0,
    cfa: CfaRule = .none,
    /// The register fields in these columns define the register the rule applies to.
    columns: ColumnRange = .{ .start = undefined, .len = 0 },
};

pub const Column = struct {
    register: u8,
    rule: RegisterRule,
};

const ColumnRange = struct {
    start: usize,
    len: u8,
};

columns: std.ArrayList(Column) = .empty,
stack: std.ArrayList(struct {
    cfa: CfaRule,
    columns: ColumnRange,
}) = .empty,
current_row: Row = .{},

/// The result of executing the CIE's initial_instructions
cie_row: ?Row = null,

pub fn deinit(self: *VirtualMachine, gpa: Allocator) void {
    self.stack.deinit(gpa);
    self.columns.deinit(gpa);
    self.* = undefined;
}

pub fn reset(self: *VirtualMachine) void {
    self.stack.clearRetainingCapacity();
    self.columns.clearRetainingCapacity();
    self.current_row = .{};
    self.cie_row = null;
}

/// Return a slice backed by the row's non-CFA columns
pub fn rowColumns(self: *const VirtualMachine, row: *const Row) []Column {
    if (row.columns.len == 0) return &.{};
    return self.columns.items[row.columns.start..][0..row.columns.len];
}

/// Either retrieves or adds a column for `register` (non-CFA) in the current row.
fn getOrAddColumn(self: *VirtualMachine, gpa: Allocator, register: u8) !*Column {
    for (self.rowColumns(&self.current_row)) |*c| {
        if (c.register == register) return c;
    }

    if (self.current_row.columns.len == 0) {
        self.current_row.columns.start = self.columns.items.len;
    } else {
        assert(self.current_row.columns.start + self.current_row.columns.len == self.columns.items.len);
    }
    self.current_row.columns.len += 1;

    const column = try self.columns.addOne(gpa);
    column.* = .{
        .register = register,
        .rule = .default,
    };

    return column;
}

pub fn populateCieLastRow(
    gpa: Allocator,
    cie: *Unwind.CommonInformationEntry,
    addr_size_bytes: u8,
    endian: std.builtin.Endian,
) !void {
    assert(cie.last_row == null);

    var vm: VirtualMachine = .{};
    defer vm.deinit(gpa);

    try vm.evalInstructions(
        gpa,
        cie,
        std.math.maxInt(u64),
        cie.initial_instructions,
        addr_size_bytes,
        endian,
    );

    cie.last_row = .{
        .offset = vm.current_row.offset,
        .cfa = vm.current_row.cfa,
        .cols = try gpa.dupe(Column, vm.rowColumns(&vm.current_row)),
    };
}

/// Runs the CIE instructions, then the FDE instructions. Execution halts
/// once the row that corresponds to `pc` is known, and the row is returned.
pub fn runTo(
    vm: *VirtualMachine,
    gpa: Allocator,
    pc: u64,
    cie: *const Unwind.CommonInformationEntry,
    fde: *const Unwind.FrameDescriptionEntry,
    addr_size_bytes: u8,
    endian: std.builtin.Endian,
) !Row {
    assert(vm.cie_row == null);

    const target_offset = pc - fde.pc_begin;
    assert(target_offset < fde.pc_range);

    const instruction_bytes: []const u8 = insts: {
        if (target_offset < cie.last_row.?.offset) {
            break :insts cie.initial_instructions;
        }
        // This is the more common case: start from the CIE's last row.
        assert(vm.columns.items.len == 0);
        vm.current_row = .{
            .offset = cie.last_row.?.offset,
            .cfa = cie.last_row.?.cfa,
            .columns = .{
                .start = 0,
                .len = @intCast(cie.last_row.?.cols.len),
            },
        };
        try vm.columns.appendSlice(gpa, cie.last_row.?.cols);
        vm.cie_row = vm.current_row;
        break :insts fde.instructions;
    };

    try vm.evalInstructions(
        gpa,
        cie,
        target_offset,
        instruction_bytes,
        addr_size_bytes,
        endian,
    );
    return vm.current_row;
}

/// Evaluates instructions from `instruction_bytes` until `target_addr` is reached or all
/// instructions have been evaluated.
fn evalInstructions(
    vm: *VirtualMachine,
    gpa: Allocator,
    cie: *const Unwind.CommonInformationEntry,
    target_addr: u64,
    instruction_bytes: []const u8,
    addr_size_bytes: u8,
    endian: std.builtin.Endian,
) !void {
    var fr: std.Io.Reader = .fixed(instruction_bytes);
    while (fr.seek < fr.buffer.len) {
        switch (try Instruction.read(&fr, addr_size_bytes, endian)) {
            .nop => {
                // If there was one nop, there's a good chance we've reached the padding and so
                // everything left is a nop, which is represented by a 0 byte.
                if (std.mem.allEqual(u8, fr.buffered(), 0)) return;
            },

            .remember_state => {
                try vm.stack.append(gpa, .{
                    .cfa = vm.current_row.cfa,
                    .columns = vm.current_row.columns,
                });
                const cols_len = vm.current_row.columns.len;
                const copy_start = vm.columns.items.len;
                assert(vm.current_row.columns.start == copy_start - cols_len);
                try vm.columns.ensureUnusedCapacity(gpa, cols_len); // to prevent aliasing issues
                vm.columns.appendSliceAssumeCapacity(vm.columns.items[copy_start - cols_len ..]);
                vm.current_row.columns.start = copy_start;
            },
            .restore_state => {
                const restored = vm.stack.pop() orelse return error.InvalidOperation;
                vm.columns.shrinkRetainingCapacity(restored.columns.start + restored.columns.len);

                vm.current_row.cfa = restored.cfa;
                vm.current_row.columns = restored.columns;
            },

            .advance_loc => |delta| {
                const new_addr = vm.current_row.offset + delta * cie.code_alignment_factor;
                if (new_addr > target_addr) return;
                vm.current_row.offset = new_addr;
            },
            .set_loc => |new_addr| {
                if (new_addr <= vm.current_row.offset) return error.InvalidOperation;
                if (cie.segment_selector_size != 0) return error.InvalidOperation; // unsupported
                // TODO: Check cie.segment_selector_size != 0 for DWARFV4

                if (new_addr > target_addr) return;
                vm.current_row.offset = new_addr;
            },

            .register => |reg| {
                const column = try vm.getOrAddColumn(gpa, reg.index);
                column.rule = switch (reg.rule) {
                    .restore => rule: {
                        const cie_row = &(vm.cie_row orelse return error.InvalidOperation);
                        for (vm.rowColumns(cie_row)) |cie_col| {
                            if (cie_col.register == reg.index) break :rule cie_col.rule;
                        }
                        break :rule .default;
                    },
                    .undefined => .undefined,
                    .same_value => .same_value,
                    .offset_uf => |off| .{ .offset = @as(i64, @intCast(off)) * cie.data_alignment_factor },
                    .offset_sf => |off| .{ .offset = off * cie.data_alignment_factor },
                    .val_offset_uf => |off| .{ .val_offset = @as(i64, @intCast(off)) * cie.data_alignment_factor },
                    .val_offset_sf => |off| .{ .val_offset = off * cie.data_alignment_factor },
                    .register => |callee_reg| .{ .register = callee_reg },
                    .expr => |len| .{ .expression = try takeExprBlock(&fr, len) },
                    .val_expr => |len| .{ .val_expression = try takeExprBlock(&fr, len) },
                };
            },
            .def_cfa => |cfa| vm.current_row.cfa = .{ .reg_off = .{
                .register = cfa.register,
                .offset = @intCast(cfa.offset),
            } },
            .def_cfa_sf => |cfa| vm.current_row.cfa = .{ .reg_off = .{
                .register = cfa.register,
                .offset = cfa.offset_sf * cie.data_alignment_factor,
            } },
            .def_cfa_reg => |register| switch (vm.current_row.cfa) {
                .none => {
                    // According to the DWARF specification, this is not valid, because this
                    // instruction can only be used to replace the register if the rule is already a
                    // `.reg_off`. However, this is emitted in practice by GNU toolchains for some
                    // targets, and so by convention is interpreted as equivalent to `.def_cfa` with
                    // an offset of 0.
                    vm.current_row.cfa = .{ .reg_off = .{
                        .register = register,
                        .offset = 0,
                    } };
                },
                .expression => return error.InvalidOperation,
                .reg_off => |*ro| ro.register = register,
            },
            .def_cfa_offset => |offset| switch (vm.current_row.cfa) {
                .none, .expression => return error.InvalidOperation,
                .reg_off => |*ro| ro.offset = @intCast(offset),
            },
            .def_cfa_offset_sf => |offset_sf| switch (vm.current_row.cfa) {
                .none, .expression => return error.InvalidOperation,
                .reg_off => |*ro| ro.offset = offset_sf * cie.data_alignment_factor,
            },
            .def_cfa_expr => |len| {
                vm.current_row.cfa = .{ .expression = try takeExprBlock(&fr, len) };
            },
        }
    }
}

fn takeExprBlock(r: *std.Io.Reader, len: usize) error{ ReadFailed, InvalidOperand }![]const u8 {
    return r.take(len) catch |err| switch (err) {
        error.ReadFailed => |e| return e,
        error.EndOfStream => return error.InvalidOperand,
    };
}

const OpcodeByte = packed struct(u8) {
    low: packed union {
        operand: u6,
        extended: enum(u6) {
            nop = 0,
            set_loc = 1,
            advance_loc1 = 2,
            advance_loc2 = 3,
            advance_loc4 = 4,
            offset_extended = 5,
            restore_extended = 6,
            undefined = 7,
            same_value = 8,
            register = 9,
            remember_state = 10,
            restore_state = 11,
            def_cfa = 12,
            def_cfa_register = 13,
            def_cfa_offset = 14,
            def_cfa_expression = 15,
            expression = 16,
            offset_extended_sf = 17,
            def_cfa_sf = 18,
            def_cfa_offset_sf = 19,
            val_offset = 20,
            val_offset_sf = 21,
            val_expression = 22,
            _,
        },
    },
    opcode: enum(u2) {
        extended = 0,
        advance_loc = 1,
        offset = 2,
        restore = 3,
    },
};

pub const Instruction = union(enum) {
    nop,
    remember_state,
    restore_state,
    advance_loc: u32,
    set_loc: u64,

    register: struct {
        index: u8,
        rule: union(enum) {
            restore, // restore from cie
            undefined,
            same_value,
            offset_uf: u64,
            offset_sf: i64,
            val_offset_uf: u64,
            val_offset_sf: i64,
            register: u8,
            /// Value is the number of bytes in the DWARF expression, which the caller must read.
            expr: usize,
            /// Value is the number of bytes in the DWARF expression, which the caller must read.
            val_expr: usize,
        },
    },

    def_cfa: struct {
        register: u8,
        offset: u64,
    },
    def_cfa_sf: struct {
        register: u8,
        offset_sf: i64,
    },
    def_cfa_reg: u8,
    def_cfa_offset: u64,
    def_cfa_offset_sf: i64,
    /// Value is the number of bytes in the DWARF expression, which the caller must read.
    def_cfa_expr: usize,

    pub fn read(
        reader: *std.Io.Reader,
        addr_size_bytes: u8,
        endian: std.builtin.Endian,
    ) !Instruction {
        const inst: OpcodeByte = @bitCast(try reader.takeByte());
        return switch (inst.opcode) {
            .advance_loc => .{ .advance_loc = inst.low.operand },
            .offset => .{ .register = .{
                .index = inst.low.operand,
                .rule = .{ .offset_uf = try reader.takeLeb128(u64) },
            } },
            .restore => .{ .register = .{
                .index = inst.low.operand,
                .rule = .restore,
            } },
            .extended => switch (inst.low.extended) {
                .nop => .nop,
                .remember_state => .remember_state,
                .restore_state => .restore_state,
                .advance_loc1 => .{ .advance_loc = try reader.takeByte() },
                .advance_loc2 => .{ .advance_loc = try reader.takeInt(u16, endian) },
                .advance_loc4 => .{ .advance_loc = try reader.takeInt(u32, endian) },
                .set_loc => .{ .set_loc = switch (addr_size_bytes) {
                    2 => try reader.takeInt(u16, endian),
                    4 => try reader.takeInt(u32, endian),
                    8 => try reader.takeInt(u64, endian),
                    else => return error.UnsupportedAddrSize,
                } },

                .offset_extended => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .{ .offset_uf = try reader.takeLeb128(u64) },
                } },
                .offset_extended_sf => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .{ .offset_sf = try reader.takeLeb128(i64) },
                } },
                .restore_extended => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .restore,
                } },
                .undefined => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .undefined,
                } },
                .same_value => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .same_value,
                } },
                .register => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .{ .register = try reader.takeLeb128(u8) },
                } },
                .val_offset => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .{ .val_offset_uf = try reader.takeLeb128(u64) },
                } },
                .val_offset_sf => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .{ .val_offset_sf = try reader.takeLeb128(i64) },
                } },
                .expression => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .{ .expr = try reader.takeLeb128(usize) },
                } },
                .val_expression => .{ .register = .{
                    .index = try reader.takeLeb128(u8),
                    .rule = .{ .val_expr = try reader.takeLeb128(usize) },
                } },

                .def_cfa => .{ .def_cfa = .{
                    .register = try reader.takeLeb128(u8),
                    .offset = try reader.takeLeb128(u64),
                } },
                .def_cfa_sf => .{ .def_cfa_sf = .{
                    .register = try reader.takeLeb128(u8),
                    .offset_sf = try reader.takeLeb128(i64),
                } },
                .def_cfa_register => .{ .def_cfa_reg = try reader.takeLeb128(u8) },
                .def_cfa_offset => .{ .def_cfa_offset = try reader.takeLeb128(u64) },
                .def_cfa_offset_sf => .{ .def_cfa_offset_sf = try reader.takeLeb128(i64) },
                .def_cfa_expression => .{ .def_cfa_expr = try reader.takeLeb128(usize) },

                _ => switch (@intFromEnum(inst.low.extended)) {
                    0x1C...0x3F => return error.UnimplementedUserOpcode,
                    else => return error.InvalidOpcode,
                },
            },
        };
    }
};

const std = @import("../../../std.zig");
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;
const Unwind = std.debug.Dwarf.Unwind;

const VirtualMachine = @This();



---
File: /std/debug/Dwarf/expression.zig
---

const builtin = @import("builtin");
const native_arch = builtin.cpu.arch;
const native_endian = native_arch.endian();

const std = @import("std");
const leb = std.leb;
const OP = std.dwarf.OP;
const mem = std.mem;
const assert = std.debug.assert;
const testing = std.testing;
const Writer = std.Io.Writer;

const regNative = std.debug.Dwarf.SelfUnwinder.regNative;

const ip_reg_num = std.debug.Dwarf.ipRegNum(native_arch).?;
const fp_reg_num = std.debug.Dwarf.fpRegNum(native_arch);
const sp_reg_num = std.debug.Dwarf.spRegNum(native_arch);

/// Expressions can be evaluated in different contexts, each requiring its own set of inputs.
/// Callers should specify all the fields relevant to their context. If a field is required
/// by the expression and it isn't in the context, error.IncompleteExpressionContext is returned.
pub const Context = struct {
    /// The dwarf format of the section this expression is in
    format: std.dwarf.Format = .@"32",
    /// The compilation unit this expression relates to, if any
    compile_unit: ?*const std.debug.Dwarf.CompileUnit = null,
    /// When evaluating a user-presented expression, this is the address of the object being evaluated
    object_address: ?*const anyopaque = null,
    /// .debug_addr section
    debug_addr: ?[]const u8 = null,
    cpu_context: ?*std.debug.cpu_context.Native = null,
    /// Call frame address, if in a CFI context
    cfa: ?usize = null,
    /// This expression is a sub-expression from an OP.entry_value instruction
    entry_value_context: bool = false,
};

pub const Options = struct {
    /// The address size of the target architecture
    addr_size: u8 = @sizeOf(usize),
    /// Endianness of the target architecture
    endian: std.builtin.Endian = native_endian,
    /// Restrict the stack machine to a subset of opcodes used in call frame instructions
    call_frame_context: bool = false,
};

// Explicitly defined to support executing sub-expressions
pub const Error = error{
    UnimplementedExpressionCall,
    UnimplementedOpcode,
    UnimplementedUserOpcode,
    UnimplementedTypedComparison,
    UnimplementedTypeConversion,

    UnknownExpressionOpcode,

    IncompleteExpressionContext,

    InvalidCFAOpcode,
    InvalidExpression,
    InvalidFrameBase,
    InvalidIntegralTypeSize,
    InvalidRegister,
    InvalidSubExpression,
    InvalidTypeLength,

    TruncatedIntegralType,

    IncompatibleRegisterSize,
} || std.debug.cpu_context.DwarfRegisterError || error{ EndOfStream, Overflow, OutOfMemory, DivisionByZero, ReadFailed };

/// A stack machine that can decode and run DWARF expressions.
/// Expressions can be decoded for non-native address size and endianness,
/// but can only be executed if the current target matches the configuration.
pub fn StackMachine(comptime options: Options) type {
    const addr_type = switch (options.addr_size) {
        2 => u16,
        4 => u32,
        8 => u64,
        else => @compileError("Unsupported address size of " ++ options.addr_size),
    };

    const addr_type_signed = switch (options.addr_size) {
        2 => i16,
        4 => i32,
        8 => i64,
        else => @compileError("Unsupported address size of " ++ options.addr_size),
    };

    return struct {
        const Self = @This();

        const Operand = union(enum) {
            generic: addr_type,
            register: u8,
            type_size: u8,
            branch_offset: i16,
            base_register: struct {
                base_register: u8,
                offset: i64,
            },
            composite_location: struct {
                size: u64,
                offset: i64,
            },
            block: []const u8,
            register_type: struct {
                register: u8,
                type_offset: addr_type,
            },
            const_type: struct {
                type_offset: addr_type,
                value_bytes: []const u8,
            },
            deref_type: struct {
                size: u8,
                type_offset: addr_type,
            },
        };

        const Value = union(enum) {
            generic: addr_type,

            // Typed value with a maximum size of a register
            regval_type: struct {
                // Offset of DW_TAG_base_type DIE
                type_offset: addr_type,
                type_size: u8,
                value: addr_type,
            },

            // Typed value specified directly in the instruction stream
            const_type: struct {
                // Offset of DW_TAG_base_type DIE
                type_offset: addr_type,
                // Backed by the instruction stream
                value_bytes: []const u8,
            },

            pub fn asIntegral(self: Value) !addr_type {
                return switch (self) {
                    .generic => |v| v,

                    // TODO: For these two prongs, look up the type and assert it's integral?
                    .regval_type => |regval_type| regval_type.value,
                    .const_type => |const_type| {
                        const value: u64 = switch (const_type.value_bytes.len) {
                            1 => mem.readInt(u8, const_type.value_bytes[0..1], native_endian),
                            2 => mem.readInt(u16, const_type.value_bytes[0..2], native_endian),
                            4 => mem.readInt(u32, const_type.value_bytes[0..4], native_endian),
                            8 => mem.readInt(u64, const_type.value_bytes[0..8], native_endian),
                            else => return error.InvalidIntegralTypeSize,
                        };

                        return std.math.cast(addr_type, value) orelse error.TruncatedIntegralType;
                    },
                };
            }
        };

        stack: std.ArrayList(Value) = .empty,

        pub fn reset(self: *Self) void {
            self.stack.clearRetainingCapacity();
        }

        pub fn deinit(self: *Self, allocator: std.mem.Allocator) void {
            self.stack.deinit(allocator);
        }

        fn generic(value: anytype) Operand {
            const int_info = @typeInfo(@TypeOf(value)).int;
            if (@sizeOf(@TypeOf(value)) > options.addr_size) {
                return .{ .generic = switch (int_info.signedness) {
                    .signed => @bitCast(@as(addr_type_signed, @truncate(value))),
                    .unsigned => @truncate(value),
                } };
            } else {
                return .{ .generic = switch (int_info.signedness) {
                    .signed => @bitCast(@as(addr_type_signed, @intCast(value))),
                    .unsigned => @intCast(value),
                } };
            }
        }

        pub fn readOperand(reader: *std.Io.Reader, opcode: u8, context: Context) !?Operand {
            return switch (opcode) {
                OP.addr => generic(try reader.takeInt(addr_type, options.endian)),
                OP.call_ref => switch (context.format) {
                    .@"32" => generic(try reader.takeInt(u32, options.endian)),
                    .@"64" => generic(try reader.takeInt(u64, options.endian)),
                },
                OP.const1u,
                OP.pick,
                => generic(try
```
