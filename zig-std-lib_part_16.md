```
 @truncate(s0 >> 16)), @as(u8, @truncate(s1 >> 24)));
        var t2 = mem.readInt(u32, &x, .little);
        x = sbox_lookup(&sbox_encrypt, @as(u8, @truncate(s3)), @as(u8, @truncate(s0 >> 8)), @as(u8, @truncate(s1 >> 16)), @as(u8, @truncate(s2 >> 24)));
        var t3 = mem.readInt(u32, &x, .little);

        t0 ^= round_key.repr[0];
        t1 ^= round_key.repr[1];
        t2 ^= round_key.repr[2];
        t3 ^= round_key.repr[3];

        return Block{ .repr = Repr{ t0, t1, t2, t3 } };
    }

    /// Decrypt a block with a round key.
    pub fn decrypt(block: Block, round_key: Block) Block {
        const s0 = block.repr[0];
        const s1 = block.repr[1];
        const s2 = block.repr[2];
        const s3 = block.repr[3];

        var x: [4]u32 = undefined;
        x = table_lookup(&table_decrypt, @as(u8, @truncate(s0)), @as(u8, @truncate(s3 >> 8)), @as(u8, @truncate(s2 >> 16)), @as(u8, @truncate(s1 >> 24)));
        var t0 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = table_lookup(&table_decrypt, @as(u8, @truncate(s1)), @as(u8, @truncate(s0 >> 8)), @as(u8, @truncate(s3 >> 16)), @as(u8, @truncate(s2 >> 24)));
        var t1 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = table_lookup(&table_decrypt, @as(u8, @truncate(s2)), @as(u8, @truncate(s1 >> 8)), @as(u8, @truncate(s0 >> 16)), @as(u8, @truncate(s3 >> 24)));
        var t2 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = table_lookup(&table_decrypt, @as(u8, @truncate(s3)), @as(u8, @truncate(s2 >> 8)), @as(u8, @truncate(s1 >> 16)), @as(u8, @truncate(s0 >> 24)));
        var t3 = x[0] ^ x[1] ^ x[2] ^ x[3];

        t0 ^= round_key.repr[0];
        t1 ^= round_key.repr[1];
        t2 ^= round_key.repr[2];
        t3 ^= round_key.repr[3];

        return Block{ .repr = Repr{ t0, t1, t2, t3 } };
    }

    /// Decrypt a block with a round key *WITHOUT ANY PROTECTION AGAINST SIDE CHANNELS*
    pub fn decryptUnprotected(block: Block, round_key: Block) Block {
        const s0 = block.repr[0];
        const s1 = block.repr[1];
        const s2 = block.repr[2];
        const s3 = block.repr[3];

        var x: [4]u32 = undefined;
        x = .{
            table_decrypt[0][@as(u8, @truncate(s0))],
            table_decrypt[1][@as(u8, @truncate(s3 >> 8))],
            table_decrypt[2][@as(u8, @truncate(s2 >> 16))],
            table_decrypt[3][@as(u8, @truncate(s1 >> 24))],
        };
        var t0 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = .{
            table_decrypt[0][@as(u8, @truncate(s1))],
            table_decrypt[1][@as(u8, @truncate(s0 >> 8))],
            table_decrypt[2][@as(u8, @truncate(s3 >> 16))],
            table_decrypt[3][@as(u8, @truncate(s2 >> 24))],
        };
        var t1 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = .{
            table_decrypt[0][@as(u8, @truncate(s2))],
            table_decrypt[1][@as(u8, @truncate(s1 >> 8))],
            table_decrypt[2][@as(u8, @truncate(s0 >> 16))],
            table_decrypt[3][@as(u8, @truncate(s3 >> 24))],
        };
        var t2 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = .{
            table_decrypt[0][@as(u8, @truncate(s3))],
            table_decrypt[1][@as(u8, @truncate(s2 >> 8))],
            table_decrypt[2][@as(u8, @truncate(s1 >> 16))],
            table_decrypt[3][@as(u8, @truncate(s0 >> 24))],
        };
        var t3 = x[0] ^ x[1] ^ x[2] ^ x[3];

        t0 ^= round_key.repr[0];
        t1 ^= round_key.repr[1];
        t2 ^= round_key.repr[2];
        t3 ^= round_key.repr[3];

        return Block{ .repr = Repr{ t0, t1, t2, t3 } };
    }

    /// Decrypt a block with the last round key.
    pub fn decryptLast(block: Block, round_key: Block) Block {
        const s0 = block.repr[0];
        const s1 = block.repr[1];
        const s2 = block.repr[2];
        const s3 = block.repr[3];

        // Last round uses s-box directly and XORs to produce output.
        var x: [4]u8 = undefined;
        x = sbox_lookup(&sbox_decrypt, @as(u8, @truncate(s0)), @as(u8, @truncate(s3 >> 8)), @as(u8, @truncate(s2 >> 16)), @as(u8, @truncate(s1 >> 24)));
        var t0 = mem.readInt(u32, &x, .little);
        x = sbox_lookup(&sbox_decrypt, @as(u8, @truncate(s1)), @as(u8, @truncate(s0 >> 8)), @as(u8, @truncate(s3 >> 16)), @as(u8, @truncate(s2 >> 24)));
        var t1 = mem.readInt(u32, &x, .little);
        x = sbox_lookup(&sbox_decrypt, @as(u8, @truncate(s2)), @as(u8, @truncate(s1 >> 8)), @as(u8, @truncate(s0 >> 16)), @as(u8, @truncate(s3 >> 24)));
        var t2 = mem.readInt(u32, &x, .little);
        x = sbox_lookup(&sbox_decrypt, @as(u8, @truncate(s3)), @as(u8, @truncate(s2 >> 8)), @as(u8, @truncate(s1 >> 16)), @as(u8, @truncate(s0 >> 24)));
        var t3 = mem.readInt(u32, &x, .little);

        t0 ^= round_key.repr[0];
        t1 ^= round_key.repr[1];
        t2 ^= round_key.repr[2];
        t3 ^= round_key.repr[3];

        return Block{ .repr = Repr{ t0, t1, t2, t3 } };
    }

    /// Apply the bitwise XOR operation to the content of two blocks.
    pub fn xorBlocks(block1: Block, block2: Block) Block {
        var x: Repr = undefined;
        comptime var i = 0;
        inline while (i < 4) : (i += 1) {
            x[i] = block1.repr[i] ^ block2.repr[i];
        }
        return Block{ .repr = x };
    }

    /// Apply the bitwise AND operation to the content of two blocks.
    pub fn andBlocks(block1: Block, block2: Block) Block {
        var x: Repr = undefined;
        comptime var i = 0;
        inline while (i < 4) : (i += 1) {
            x[i] = block1.repr[i] & block2.repr[i];
        }
        return Block{ .repr = x };
    }

    /// Apply the bitwise OR operation to the content of two blocks.
    pub fn orBlocks(block1: Block, block2: Block) Block {
        var x: Repr = undefined;
        comptime var i = 0;
        inline while (i < 4) : (i += 1) {
            x[i] = block1.repr[i] | block2.repr[i];
        }
        return Block{ .repr = x };
    }

    /// Apply the inverse MixColumns operation to a block.
    pub fn invMixColumns(block: Block) Block {
        var out: Repr = undefined;
        inline for (0..4) |i| {
            const col = block.repr[i];
            const b0: u8 = @truncate(col);
            const b1: u8 = @truncate(col >> 8);
            const b2: u8 = @truncate(col >> 16);
            const b3: u8 = @truncate(col >> 24);

            const r0 = mul(0x0e, b0) ^ mul(0x0b, b1) ^ mul(0x0d, b2) ^ mul(0x09, b3);
            const r1 = mul(0x09, b0) ^ mul(0x0e, b1) ^ mul(0x0b, b2) ^ mul(0x0d, b3);
            const r2 = mul(0x0d, b0) ^ mul(0x09, b1) ^ mul(0x0e, b2) ^ mul(0x0b, b3);
            const r3 = mul(0x0b, b0) ^ mul(0x0d, b1) ^ mul(0x09, b2) ^ mul(0x0e, b3);

            out[i] = @as(u32, r0) | (@as(u32, r1) << 8) | (@as(u32, r2) << 16) | (@as(u32, r3) << 24);
        }
        return Block{ .repr = out };
    }

    /// Perform operations on multiple blocks in parallel.
    pub const parallel = struct {
        /// The recommended number of AES encryption/decryption to perform in parallel for the chosen implementation.
        pub const optimal_parallel_blocks = 1;

        /// Encrypt multiple blocks in parallel, each their own round key.
        pub fn encryptParallel(comptime count: usize, blocks: [count]Block, round_keys: [count]Block) [count]Block {
            var i = 0;
            var out: [count]Block = undefined;
            while (i < count) : (i += 1) {
                out[i] = blocks[i].encrypt(round_keys[i]);
            }
            return out;
        }

        /// Decrypt multiple blocks in parallel, each their own round key.
        pub fn decryptParallel(comptime count: usize, blocks: [count]Block, round_keys: [count]Block) [count]Block {
            var i = 0;
            var out: [count]Block = undefined;
            while (i < count) : (i += 1) {
                out[i] = blocks[i].decrypt(round_keys[i]);
            }
            return out;
        }

        /// Encrypt multiple blocks in parallel with the same round key.
        pub fn encryptWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            var i = 0;
            var out: [count]Block = undefined;
            while (i < count) : (i += 1) {
                out[i] = blocks[i].encrypt(round_key);
            }
            return out;
        }

        /// Decrypt multiple blocks in parallel with the same round key.
        pub fn decryptWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            var i = 0;
            var out: [count]Block = undefined;
            while (i < count) : (i += 1) {
                out[i] = blocks[i].decrypt(round_key);
            }
            return out;
        }

        /// Encrypt multiple blocks in parallel with the same last round key.
        pub fn encryptLastWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            var i = 0;
            var out: [count]Block = undefined;
            while (i < count) : (i += 1) {
                out[i] = blocks[i].encryptLast(round_key);
            }
            return out;
        }

        /// Decrypt multiple blocks in parallel with the same last round key.
        pub fn decryptLastWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            var i = 0;
            var out: [count]Block = undefined;
            while (i < count) : (i += 1) {
                out[i] = blocks[i].decryptLast(round_key);
            }
            return out;
        }
    };
};

/// A fixed-size vector of AES blocks.
/// All operations are performed in parallel, using SIMD instructions when available.
pub fn BlockVec(comptime blocks_count: comptime_int) type {
    return struct {
        const Self = @This();

        /// The number of AES blocks the target architecture can process with a single instruction.
        pub const native_vector_size = 1;

        /// The size of the AES block vector that the target architecture can process with a single instruction, in bytes.
        pub const native_word_size = native_vector_size * 16;

        const native_words = blocks_count;

        /// Internal representation of a block vector.
        repr: [native_words]Block,

        /// Length of the block vector in bytes.
        pub const block_length: usize = blocks_count * 16;

        /// Convert a byte sequence into an internal representation.
        pub fn fromBytes(bytes: *const [blocks_count * 16]u8) Self {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = Block.fromBytes(bytes[i * native_word_size ..][0..native_word_size]);
            }
            return out;
        }

        /// Convert the internal representation of a block vector into a byte sequence.
        pub fn toBytes(block_vec: Self) [blocks_count * 16]u8 {
            var out: [blocks_count * 16]u8 = undefined;
            for (0..native_words) |i| {
                out[i * native_word_size ..][0..native_word_size].* = block_vec.repr[i].toBytes();
            }
            return out;
        }

        /// XOR the block vector with a byte sequence.
        pub fn xorBytes(block_vec: Self, bytes: *const [blocks_count * 16]u8) [32]u8 {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].xorBytes(bytes[i * native_word_size ..][0..native_word_size]);
            }
            return out;
        }

        /// Apply the forward AES operation to the block vector with a vector of round keys.
        pub fn encrypt(block_vec: Self, round_key_vec: Self) Self {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].encrypt(round_key_vec.repr[i]);
            }
            return out;
        }

        /// Apply the forward AES operation to the block vector with a vector of last round keys.
        pub fn encryptLast(block_vec: Self, round_key_vec: Self) Self {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].encryptLast(round_key_vec.repr[i]);
            }
            return out;
        }

        /// Apply the inverse AES operation to the block vector with a vector of round keys.
        pub fn decrypt(block_vec: Self, inv_round_key_vec: Self) Self {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].decrypt(inv_round_key_vec.repr[i]);
            }
            return out;
        }

        /// Apply the inverse AES operation to the block vector with a vector of last round keys.
        pub fn decryptLast(block_vec: Self, inv_round_key_vec: Self) Self {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].decryptLast(inv_round_key_vec.repr[i]);
            }
            return out;
        }

        /// Apply the bitwise XOR operation to the content of two block vectors.
        pub fn xorBlocks(block_vec1: Self, block_vec2: Self) Self {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = block_vec1.repr[i].xorBlocks(block_vec2.repr[i]);
            }
            return out;
        }

        /// Apply the bitwise AND operation to the content of two block vectors.
        pub fn andBlocks(block_vec1: Self, block_vec2: Self) Self {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = block_vec1.repr[i].andBlocks(block_vec2.repr[i]);
            }
            return out;
        }

        /// Apply the bitwise OR operation to the content of two block vectors.
        pub fn orBlocks(block_vec1: Self, block_vec2: Block) Self {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = block_vec1.repr[i].orBlocks(block_vec2.repr[i]);
            }
            return out;
        }

        /// Apply the inverse MixColumns operation to each block in the vector.
        pub fn invMixColumns(block_vec: Self) Self {
            var out: Self = undefined;
            for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].invMixColumns();
            }
            return out;
        }
    };
}

fn KeySchedule(comptime Aes: type) type {
    std.debug.assert(Aes.rounds == 10 or Aes.rounds == 14);
    const key_length = Aes.key_bits / 8;
    const rounds = Aes.rounds;

    return struct {
        const Self = @This();
        const words_in_key = key_length / 4;

        round_keys: [rounds + 1]Block,

        // Key expansion algorithm. See FIPS-197, Figure 11.
        fn expandKey(key: [key_length]u8) Self {
            const subw = struct {
                // Apply sbox_encrypt to each byte in w.
                fn func(w: u32) u32 {
                    const x = sbox_lookup(&sbox_key_schedule, @as(u8, @truncate(w)), @as(u8, @truncate(w >> 8)), @as(u8, @truncate(w >> 16)), @as(u8, @truncate(w >> 24)));
                    return mem.readInt(u32, &x, .little);
                }
            }.func;

            var round_keys: [rounds + 1]Block = undefined;
            comptime var i: usize = 0;
            inline while (i < words_in_key) : (i += 1) {
                round_keys[i / 4].repr[i % 4] = mem.readInt(u32, key[4 * i ..][0..4], .big);
            }
            inline while (i < round_keys.len * 4) : (i += 1) {
                var t = round_keys[(i - 1) / 4].repr[(i - 1) % 4];
                if (i % words_in_key == 0) {
                    t = subw(std.math.rotl(u32, t, 8)) ^ (@as(u32, powx[i / words_in_key - 1]) << 24);
                } else if (words_in_key > 6 and i % words_in_key == 4) {
                    t = subw(t);
                }
                round_keys[i / 4].repr[i % 4] = round_keys[(i - words_in_key) / 4].repr[(i - words_in_key) % 4] ^ t;
            }
            i = 0;
            inline while (i < round_keys.len * 4) : (i += 1) {
                round_keys[i / 4].repr[i % 4] = @byteSwap(round_keys[i / 4].repr[i % 4]);
            }
            return Self{ .round_keys = round_keys };
        }

        /// Invert the key schedule.
        pub fn invert(key_schedule: Self) Self {
            const round_keys = &key_schedule.round_keys;
            var inv_round_keys: [rounds + 1]Block = undefined;
            const total_words = 4 * round_keys.len;
            var i: usize = 0;
            while (i < total_words) : (i += 4) {
                const ei = total_words - i - 4;
                comptime var j: usize = 0;
                inline while (j < 4) : (j += 1) {
                    var rk = round_keys[(ei + j) / 4].repr[(ei + j) % 4];
                    if (i > 0 and i + 4 < total_words) {
                        const x = sbox_lookup(&sbox_key_schedule, @as(u8, @truncate(rk >> 24)), @as(u8, @truncate(rk >> 16)), @as(u8, @truncate(rk >> 8)), @as(u8, @truncate(rk)));
                        const y = table_lookup(&table_decrypt, x[3], x[2], x[1], x[0]);
                        rk = y[0] ^ y[1] ^ y[2] ^ y[3];
                    }
                    inv_round_keys[(i + j) / 4].repr[(i + j) % 4] = rk;
                }
            }
            return Self{ .round_keys = inv_round_keys };
        }
    };
}

/// A context to perform encryption using the standard AES key schedule.
pub fn AesEncryptCtx(comptime Aes: type) type {
    std.debug.assert(Aes.key_bits == 128 or Aes.key_bits == 256);
    const rounds = Aes.rounds;

    return struct {
        const Self = @This();
        pub const block = Aes.block;
        pub const block_length = block.block_length;
        key_schedule: KeySchedule(Aes),

        /// Create a new encryption context with the given key.
        pub fn init(key: [Aes.key_bits / 8]u8) Self {
            const key_schedule = KeySchedule(Aes).expandKey(key);
            return Self{
                .key_schedule = key_schedule,
            };
        }

        /// Encrypt a single block.
        pub fn encrypt(ctx: Self, dst: *[16]u8, src: *const [16]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var t = Block.fromBytes(src).xorBlocks(round_keys[0]);
            comptime var i = 1;
            if (side_channels_mitigations == .full) {
                inline while (i < rounds) : (i += 1) {
                    t = t.encrypt(round_keys[i]);
                }
            } else {
                inline while (i < 5) : (i += 1) {
                    t = t.encrypt(round_keys[i]);
                }
                inline while (i < rounds - 1) : (i += 1) {
                    t = t.encryptUnprotected(round_keys[i]);
                }
                t = t.encrypt(round_keys[i]);
            }
            t = t.encryptLast(round_keys[rounds]);
            dst.* = t.toBytes();
        }

        /// Encrypt+XOR a single block.
        pub fn xor(ctx: Self, dst: *[16]u8, src: *const [16]u8, counter: [16]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var t = Block.fromBytes(&counter).xorBlocks(round_keys[0]);
            comptime var i = 1;
            if (side_channels_mitigations == .full) {
                inline while (i < rounds) : (i += 1) {
                    t = t.encrypt(round_keys[i]);
                }
            } else {
                inline while (i < 5) : (i += 1) {
                    t = t.encrypt(round_keys[i]);
                }
                inline while (i < rounds - 1) : (i += 1) {
                    t = t.encryptUnprotected(round_keys[i]);
                }
                t = t.encrypt(round_keys[i]);
            }
            t = t.encryptLast(round_keys[rounds]);
            dst.* = t.xorBytes(src);
        }

        /// Encrypt multiple blocks, possibly leveraging parallelization.
        pub fn encryptWide(ctx: Self, comptime count: usize, dst: *[16 * count]u8, src: *const [16 * count]u8) void {
            var i: usize = 0;
            while (i < count) : (i += 1) {
                ctx.encrypt(dst[16 * i .. 16 * i + 16][0..16], src[16 * i .. 16 * i + 16][0..16]);
            }
        }

        /// Encrypt+XOR multiple blocks, possibly leveraging parallelization.
        pub fn xorWide(ctx: Self, comptime count: usize, dst: *[16 * count]u8, src: *const [16 * count]u8, counters: [16 * count]u8) void {
            var i: usize = 0;
            while (i < count) : (i += 1) {
                ctx.xor(dst[16 * i .. 16 * i + 16][0..16], src[16 * i .. 16 * i + 16][0..16], counters[16 * i .. 16 * i + 16][0..16].*);
            }
        }
    };
}

/// A context to perform decryption using the standard AES key schedule.
pub fn AesDecryptCtx(comptime Aes: type) type {
    std.debug.assert(Aes.key_bits == 128 or Aes.key_bits == 256);
    const rounds = Aes.rounds;

    return struct {
        const Self = @This();
        pub const block = Aes.block;
        pub const block_length = block.block_length;
        key_schedule: KeySchedule(Aes),

        /// Create a decryption context from an existing encryption context.
        pub fn initFromEnc(ctx: AesEncryptCtx(Aes)) Self {
            return Self{
                .key_schedule = ctx.key_schedule.invert(),
            };
        }

        /// Create a new decryption context with the given key.
        pub fn init(key: [Aes.key_bits / 8]u8) Self {
            const enc_ctx = AesEncryptCtx(Aes).init(key);
            return initFromEnc(enc_ctx);
        }

        /// Decrypt a single block.
        pub fn decrypt(ctx: Self, dst: *[16]u8, src: *const [16]u8) void {
            const inv_round_keys = ctx.key_schedule.round_keys;
            var t = Block.fromBytes(src).xorBlocks(inv_round_keys[0]);
            comptime var i = 1;
            if (side_channels_mitigations == .full) {
                inline while (i < rounds) : (i += 1) {
                    t = t.decrypt(inv_round_keys[i]);
                }
            } else {
                inline while (i < 5) : (i += 1) {
                    t = t.decrypt(inv_round_keys[i]);
                }
                inline while (i < rounds - 1) : (i += 1) {
                    t = t.decryptUnprotected(inv_round_keys[i]);
                }
                t = t.decrypt(inv_round_keys[i]);
            }
            t = t.decryptLast(inv_round_keys[rounds]);
            dst.* = t.toBytes();
        }

        /// Decrypt multiple blocks, possibly leveraging parallelization.
        pub fn decryptWide(ctx: Self, comptime count: usize, dst: *[16 * count]u8, src: *const [16 * count]u8) void {
            var i: usize = 0;
            while (i < count) : (i += 1) {
                ctx.decrypt(dst[16 * i .. 16 * i + 16][0..16], src[16 * i .. 16 * i + 16][0..16]);
            }
        }
    };
}

/// AES-128 with the standard key schedule.
pub const Aes128 = struct {
    pub const key_bits: usize = 128;
    pub const rounds = ((key_bits - 64) / 32 + 8);
    pub const block = Block;

    /// Create a new context for encryption.
    pub fn initEnc(key: [key_bits / 8]u8) AesEncryptCtx(Aes128) {
        return AesEncryptCtx(Aes128).init(key);
    }

    /// Create a new context for decryption.
    pub fn initDec(key: [key_bits / 8]u8) AesDecryptCtx(Aes128) {
        return AesDecryptCtx(Aes128).init(key);
    }
};

/// AES-256 with the standard key schedule.
pub const Aes256 = struct {
    pub const key_bits: usize = 256;
    pub const rounds = ((key_bits - 64) / 32 + 8);
    pub const block = Block;

    /// Create a new context for encryption.
    pub fn initEnc(key: [key_bits / 8]u8) AesEncryptCtx(Aes256) {
        return AesEncryptCtx(Aes256).init(key);
    }

    /// Create a new context for decryption.
    pub fn initDec(key: [key_bits / 8]u8) AesDecryptCtx(Aes256) {
        return AesDecryptCtx(Aes256).init(key);
    }
};

// constants

// Rijndael's irreducible polynomial.
const poly: u9 = 1 << 8 | 1 << 4 | 1 << 3 | 1 << 1 | 1 << 0; // x⁸ + x⁴ + x³ + x + 1

// Powers of x mod poly in GF(2).
const powx = init: {
    var array: [16]u8 = undefined;

    var value = 1;
    for (&array) |*power| {
        power.* = value;
        value = mul(value, 2);
    }

    break :init array;
};

const sbox_encrypt align(64) = generateSbox(false); // S-box for encryption
const sbox_key_schedule align(64) = generateSbox(false); // S-box only for key schedule, so that it uses distinct L1 cache entries than the S-box used for encryption
const sbox_decrypt align(64) = generateSbox(true); // S-box for decryption
const table_encrypt align(64) = generateTable(false); // 4-byte LUTs for encryption
const table_decrypt align(64) = generateTable(true); // 4-byte LUTs for decryption

// Generate S-box substitution values.
fn generateSbox(invert: bool) [256]u8 {
    @setEvalBranchQuota(10000);

    var sbox: [256]u8 = undefined;

    var p: u8 = 1;
    var q: u8 = 1;
    for (sbox) |_| {
        p = mul(p, 3);
        q = mul(q, 0xf6); // divide by 3

        var value: u8 = q ^ 0x63;
        value ^= math.rotl(u8, q, 1);
        value ^= math.rotl(u8, q, 2);
        value ^= math.rotl(u8, q, 3);
        value ^= math.rotl(u8, q, 4);

        if (invert) {
            sbox[value] = p;
        } else {
            sbox[p] = value;
        }
    }

    if (invert) {
        sbox[0x63] = 0x00;
    } else {
        sbox[0x00] = 0x63;
    }

    return sbox;
}

// Generate lookup tables.
fn generateTable(invert: bool) [4][256]u32 {
    @setEvalBranchQuota(50000);

    var table: [4][256]u32 = undefined;

    for (generateSbox(invert), 0..) |value, index| {
        table[0][index] = math.shl(u32, mul(value, if (invert) 0xb else 0x3), 24);
        table[0][index] |= math.shl(u32, mul(value, if (invert) 0xd else 0x1), 16);
        table[0][index] |= math.shl(u32, mul(value, if (invert) 0x9 else 0x1), 8);
        table[0][index] |= mul(value, if (invert) 0xe else 0x2);

        table[1][index] = math.rotl(u32, table[0][index], 8);
        table[2][index] = math.rotl(u32, table[0][index], 16);
        table[3][index] = math.rotl(u32, table[0][index], 24);
    }

    return table;
}

// Multiply a and b as GF(2) polynomials modulo poly.
fn mul(a: u8, b: u8) u8 {
    @setEvalBranchQuota(30000);

    var i: u8 = a;
    var j: u9 = b;
    var s: u9 = 0;

    while (i > 0) : (i >>= 1) {
        if (i & 1 != 0) {
            s ^= j;
        }

        j *= 2;
        if (j & 0x100 != 0) {
            j ^= poly;
        }
    }

    return @as(u8, @truncate(s));
}

const cache_line_bytes = std.atomic.cache_line;

fn sbox_lookup(sbox: *align(64) const [256]u8, idx0: u8, idx1: u8, idx2: u8, idx3: u8) [4]u8 {
    if (side_channels_mitigations == .none) {
        return [4]u8{
            sbox[idx0],
            sbox[idx1],
            sbox[idx2],
            sbox[idx3],
        };
    } else {
        const stride = switch (side_channels_mitigations) {
            .none => unreachable,
            .basic => sbox.len / 4,
            .medium => @min(sbox.len, 2 * cache_line_bytes),
            .full => @min(sbox.len, cache_line_bytes),
        };
        const of0 = idx0 % stride;
        const of1 = idx1 % stride;
        const of2 = idx2 % stride;
        const of3 = idx3 % stride;
        var t: [4][sbox.len / stride]u8 align(64) = undefined;
        var i: usize = 0;
        while (i < t[0].len) : (i += 1) {
            const tx = sbox[i * stride ..];
            t[0][i] = tx[of0];
            t[1][i] = tx[of1];
            t[2][i] = tx[of2];
            t[3][i] = tx[of3];
        }
        std.mem.doNotOptimizeAway(t);
        return [4]u8{
            t[0][idx0 / stride],
            t[1][idx1 / stride],
            t[2][idx2 / stride],
            t[3][idx3 / stride],
        };
    }
}

fn table_lookup(table: *align(64) const [4][256]u32, idx0: u8, idx1: u8, idx2: u8, idx3: u8) [4]u32 {
    if (side_channels_mitigations == .none) {
        return [4]u32{
            table[0][idx0],
            table[1][idx1],
            table[2][idx2],
            table[3][idx3],
        };
    } else {
        const table_len: usize = 256;
        const stride = switch (side_channels_mitigations) {
            .none => unreachable,
            .basic => table_len / 4,
            .medium => @max(1, @min(table_len, 2 * cache_line_bytes / 4)),
            .full => @max(1, @min(table_len, cache_line_bytes / 4)),
        };
        const of0 = idx0 % stride;
        const of1 = idx1 % stride;
        const of2 = idx2 % stride;
        const of3 = idx3 % stride;
        var t: [4][table_len / stride]u32 align(64) = undefined;
        var i: usize = 0;
        while (i < t[0].len) : (i += 1) {
            const tx = table[0][i * stride ..];
            t[0][i] = tx[of0];
            t[1][i] = tx[of1];
            t[2][i] = tx[of2];
            t[3][i] = tx[of3];
        }
        std.mem.doNotOptimizeAway(t);
        return [4]u32{
            t[0][idx0 / stride],
            math.rotl(u32, (&t[1])[idx1 / stride], 8),
            math.rotl(u32, (&t[2])[idx2 / stride], 16),
            math.rotl(u32, (&t[3])[idx3 / stride], 24),
        };
    }
}



---
File: /std/crypto/Certificate/Bundle/macos.zig
---

const std = @import("std");
const Io = std.Io;
const assert = std.debug.assert;
const fs = std.fs;
const mem = std.mem;
const Allocator = std.mem.Allocator;
const Bundle = @import("../Bundle.zig");

pub const RescanMacError = Allocator.Error || Io.File.OpenError || Io.File.Reader.Error || Io.File.SeekError || Bundle.ParseCertError || error{EndOfStream};

pub fn rescanMac(cb: *Bundle, gpa: Allocator, io: Io, now: Io.Timestamp) RescanMacError!void {
    cb.bytes.clearRetainingCapacity();
    cb.map.clearRetainingCapacity();

    const keychain_paths = [2][]const u8{
        "/System/Library/Keychains/SystemRootCertificates.keychain",
        "/Library/Keychains/System.keychain",
    };

    for (keychain_paths) |keychain_path| {
        const bytes = Io.Dir.cwd().readFileAlloc(io, keychain_path, gpa, .limited(std.math.maxInt(u32))) catch |err| switch (err) {
            error.StreamTooLong => return error.FileTooBig,
            else => |e| return e,
        };
        defer gpa.free(bytes);

        var reader: Io.Reader = .fixed(bytes);
        scanReader(cb, gpa, &reader, now.toSeconds()) catch |err| switch (err) {
            error.ReadFailed => unreachable, // prebuffered
            else => |e| return e,
        };
    }

    cb.bytes.shrinkAndFree(gpa, cb.bytes.items.len);
}

fn scanReader(cb: *Bundle, gpa: Allocator, reader: *Io.Reader, now_sec: i64) !void {
    const db_header = try reader.takeStruct(ApplDbHeader, .big);
    assert(mem.eql(u8, &db_header.signature, "kych"));

    reader.seek = db_header.schema_offset;

    const db_schema = try reader.takeStruct(ApplDbSchema, .big);

    var table_list = try gpa.alloc(u32, db_schema.table_count);
    defer gpa.free(table_list);

    var table_idx: u32 = 0;
    while (table_idx < table_list.len) : (table_idx += 1) {
        table_list[table_idx] = try reader.takeInt(u32, .big);
    }

    for (table_list) |table_offset| {
        reader.seek = db_header.schema_offset + table_offset;

        const table_header = try reader.takeStruct(TableHeader, .big);

        if (@as(std.c.DB_RECORDTYPE, @enumFromInt(table_header.table_id)) != .X509_CERTIFICATE) {
            continue;
        }

        var record_list = try gpa.alloc(u32, table_header.record_count);
        defer gpa.free(record_list);

        var record_idx: u32 = 0;
        while (record_idx < record_list.len) : (record_idx += 1) {
            record_list[record_idx] = try reader.takeInt(u32, .big);
        }

        for (record_list) |record_offset| {
            // An offset of zero means that the record is not present.
            // An offset that is not 4-byte-aligned is invalid.
            if (record_offset == 0 or record_offset % 4 != 0) continue;

            reader.seek = db_header.schema_offset + table_offset + record_offset;

            const cert_header = try reader.takeStruct(X509CertHeader, .big);

            if (cert_header.cert_size == 0) continue;

            const cert_start: u32 = @intCast(cb.bytes.items.len);
            const dest_buf = try cb.bytes.addManyAsSlice(gpa, cert_header.cert_size);
            try reader.readSliceAll(dest_buf);

            try cb.parseCert(gpa, cert_start, now_sec);
        }
    }
}

const ApplDbHeader = extern struct {
    signature: [4]u8,
    version: u32,
    header_size: u32,
    schema_offset: u32,
    auth_offset: u32,
};

const ApplDbSchema = extern struct {
    schema_size: u32,
    table_count: u32,
};

const TableHeader = extern struct {
    table_size: u32,
    table_id: u32,
    record_count: u32,
    records: u32,
    indexes_offset: u32,
    free_list_head: u32,
    record_numbers_count: u32,
};

const X509CertHeader = extern struct {
    record_size: u32,
    record_number: u32,
    unknown1: u32,
    unknown2: u32,
    cert_size: u32,
    unknown3: u32,
    cert_type: u32,
    cert_encoding: u32,
    print_name: u32,
    alias: u32,
    subject: u32,
    issuer: u32,
    serial_number: u32,
    subject_key_identifier: u32,
    public_key_hash: u32,
};



---
File: /std/crypto/Certificate/Bundle.zig
---

//! A set of certificates. Typically pre-installed on every operating system,
//! these are "Certificate Authorities" used to validate SSL certificates.
//! This data structure stores certificates in DER-encoded form, all of them
//! concatenated together in the `bytes` array. The `map` field contains an
//! index from the DER-encoded subject name to the index of the containing
//! certificate within `bytes`.
const Bundle = @This();
const builtin = @import("builtin");

const std = @import("../../std.zig");
const Io = std.Io;
const Dir = std.Io.Dir;
const assert = std.debug.assert;
const mem = std.mem;
const crypto = std.crypto;
const Allocator = std.mem.Allocator;
const Certificate = std.crypto.Certificate;
const der = Certificate.der;

const base64 = std.base64.standard.decoderWithIgnore(" \t\r\n");

/// The key is the contents slice of the subject.
map: std.HashMapUnmanaged(der.Element.Slice, u32, MapContext, std.hash_map.default_max_load_percentage),
bytes: std.ArrayList(u8),

pub const empty: Bundle = .{ .map = .empty, .bytes = .empty };

pub const VerifyError = Certificate.Parsed.VerifyError || error{
    CertificateIssuerNotFound,
};

pub fn verify(cb: Bundle, subject: Certificate.Parsed, now_sec: i64) VerifyError!void {
    const bytes_index = cb.find(subject.issuer()) orelse return error.CertificateIssuerNotFound;
    const issuer_cert: Certificate = .{
        .buffer = cb.bytes.items,
        .index = bytes_index,
    };
    // Every certificate in the bundle is pre-parsed before adding it, ensuring
    // that parsing will succeed here.
    const issuer = issuer_cert.parse() catch unreachable;
    try subject.verify(issuer, now_sec);
}

/// The returned bytes become invalid after calling any of the rescan functions
/// or add functions.
pub fn find(cb: Bundle, subject_name: []const u8) ?u32 {
    const Adapter = struct {
        cb: Bundle,

        pub fn hash(ctx: @This(), k: []const u8) u64 {
            _ = ctx;
            return std.hash_map.hashString(k);
        }

        pub fn eql(ctx: @This(), a: []const u8, b_key: der.Element.Slice) bool {
            const b = ctx.cb.bytes.items[b_key.start..b_key.end];
            return mem.eql(u8, a, b);
        }
    };
    return cb.map.getAdapted(subject_name, Adapter{ .cb = cb });
}

pub fn deinit(cb: *Bundle, gpa: Allocator) void {
    cb.map.deinit(gpa);
    cb.bytes.deinit(gpa);
    cb.* = undefined;
}

pub const RescanError = RescanLinuxError || RescanMacError || RescanWithPathError || RescanWindowsError;

/// Clears the set of certificates and then scans the host operating system
/// file system standard locations for certificates.
/// For operating systems that do not have standard CA installations to be
/// found, this function clears the set of certificates.
pub fn rescan(cb: *Bundle, gpa: Allocator, io: Io, now: Io.Timestamp) RescanError!void {
    switch (builtin.os.tag) {
        .linux => return rescanLinux(cb, gpa, io, now),
        .maccatalyst, .macos => return rescanMac(cb, gpa, io, now),
        .freebsd, .openbsd => return rescanWithPath(cb, gpa, io, now, "/etc/ssl/cert.pem"),
        .netbsd => return rescanWithPath(cb, gpa, io, now, "/etc/openssl/certs/ca-certificates.crt"),
        .dragonfly => return rescanWithPath(cb, gpa, io, now, "/usr/local/etc/ssl/cert.pem"),
        .illumos => return rescanWithPath(cb, gpa, io, now, "/etc/ssl/cacert.pem"),
        .haiku => return rescanWithPath(cb, gpa, io, now, "/boot/system/data/ssl/CARootCertificates.pem"),
        // https://github.com/SerenityOS/serenity/blob/222acc9d389bc6b490d4c39539761b043a4bfcb0/Ports/ca-certificates/package.sh#L19
        .serenity => return rescanWithPath(cb, gpa, io, now, "/etc/ssl/certs/ca-certificates.crt"),
        .windows => return rescanWindows(cb, gpa, io, now),
        else => {},
    }
}

const rescanMac = @import("Bundle/macos.zig").rescanMac;
const RescanMacError = @import("Bundle/macos.zig").RescanMacError;

const RescanLinuxError = AddCertsFromFilePathError || AddCertsFromDirPathError;

fn rescanLinux(cb: *Bundle, gpa: Allocator, io: Io, now: Io.Timestamp) RescanLinuxError!void {
    // Possible certificate files; stop after finding one.
    const cert_file_paths = [_][]const u8{
        "/etc/ssl/certs/ca-certificates.crt", // Debian/Ubuntu/Gentoo etc.
        "/etc/pki/tls/certs/ca-bundle.crt", // Fedora/RHEL 6
        "/etc/ssl/ca-bundle.pem", // OpenSUSE
        "/etc/pki/tls/cacert.pem", // OpenELEC
        "/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem", // CentOS/RHEL 7
        "/etc/ssl/cert.pem", // Alpine Linux
    };

    // Possible directories with certificate files; all will be read.
    const cert_dir_paths = [_][]const u8{
        "/etc/ssl/certs", // SLES10/SLES11
        "/etc/pki/tls/certs", // Fedora/RHEL
        "/system/etc/security/cacerts", // Android
    };

    cb.bytes.clearRetainingCapacity();
    cb.map.clearRetainingCapacity();

    scan: {
        for (cert_file_paths) |cert_file_path| {
            if (addCertsFromFilePathAbsolute(cb, gpa, io, now, cert_file_path)) |_| {
                break :scan;
            } else |err| switch (err) {
                error.FileNotFound => continue,
                else => |e| return e,
            }
        }

        for (cert_dir_paths) |cert_dir_path| {
            addCertsFromDirPathAbsolute(cb, gpa, io, now, cert_dir_path) catch |err| switch (err) {
                error.FileNotFound => continue,
                else => |e| return e,
            };
        }
    }

    cb.bytes.shrinkAndFree(gpa, cb.bytes.items.len);
}

const RescanWithPathError = AddCertsFromFilePathError;

fn rescanWithPath(cb: *Bundle, gpa: Allocator, io: Io, now: Io.Timestamp, cert_file_path: []const u8) RescanWithPathError!void {
    cb.bytes.clearRetainingCapacity();
    cb.map.clearRetainingCapacity();
    try addCertsFromFilePathAbsolute(cb, gpa, io, now, cert_file_path);
    cb.bytes.shrinkAndFree(gpa, cb.bytes.items.len);
}

const RescanWindowsError = Allocator.Error || ParseCertError || std.posix.UnexpectedError || error{FileNotFound};

fn rescanWindows(cb: *Bundle, gpa: Allocator, io: Io, now: Io.Timestamp) RescanWindowsError!void {
    cb.bytes.clearRetainingCapacity();
    cb.map.clearRetainingCapacity();

    _ = io;

    const w = std.os.windows;
    const GetLastError = w.GetLastError;
    const root = [4:0]u16{ 'R', 'O', 'O', 'T' };
    const store = w.crypt32.CertOpenSystemStoreW(.NULL, &root) orelse switch (GetLastError()) {
        .FILE_NOT_FOUND => return error.FileNotFound,
        else => |err| return w.unexpectedError(err),
    };
    defer assert(w.crypt32.CertCloseStore(store, .{ .CHECK = std.debug.runtime_safety }).toBool());

    const now_sec = now.toSeconds();

    var ctx = w.crypt32.CertEnumCertificatesInStore(store, null);
    while (ctx) |context| : (ctx = w.crypt32.CertEnumCertificatesInStore(store, ctx)) {
        const decoded_start = @as(u32, @intCast(cb.bytes.items.len));
        const encoded_cert = context.pbCertEncoded[0..context.cbCertEncoded];
        try cb.bytes.appendSlice(gpa, encoded_cert);
        try cb.parseCert(gpa, decoded_start, now_sec);
    }
    cb.bytes.shrinkAndFree(gpa, cb.bytes.items.len);
}

pub const AddCertsFromDirPathError = Io.File.OpenError || AddCertsFromDirError;

pub fn addCertsFromDirPath(
    cb: *Bundle,
    gpa: Allocator,
    io: Io,
    dir: Io.Dir,
    sub_dir_path: []const u8,
) AddCertsFromDirPathError!void {
    var iterable_dir = try dir.openDir(io, sub_dir_path, .{ .iterate = true });
    defer iterable_dir.close(io);
    const now = Io.Clock.real.now(io);
    return addCertsFromDir(cb, gpa, io, now, iterable_dir);
}

pub fn addCertsFromDirPathAbsolute(
    cb: *Bundle,
    gpa: Allocator,
    io: Io,
    now: Io.Timestamp,
    abs_dir_path: []const u8,
) AddCertsFromDirPathError!void {
    assert(Dir.path.isAbsolute(abs_dir_path));
    var iterable_dir = try Dir.openDirAbsolute(io, abs_dir_path, .{ .iterate = true });
    defer iterable_dir.close(io);
    return addCertsFromDir(cb, gpa, io, now, iterable_dir);
}

pub const AddCertsFromDirError = AddCertsFromFilePathError;

pub fn addCertsFromDir(cb: *Bundle, gpa: Allocator, io: Io, now: Io.Timestamp, iterable_dir: Io.Dir) AddCertsFromDirError!void {
    var it = iterable_dir.iterate();
    while (try it.next(io)) |entry| {
        switch (entry.kind) {
            .file, .sym_link => {},
            else => continue,
        }

        try addCertsFromFilePath(cb, gpa, io, now, iterable_dir, entry.name);
    }
}

pub const AddCertsFromFilePathError = Io.File.OpenError || AddCertsFromFileError;

pub fn addCertsFromFilePathAbsolute(
    cb: *Bundle,
    gpa: Allocator,
    io: Io,
    now: Io.Timestamp,
    abs_file_path: []const u8,
) AddCertsFromFilePathError!void {
    var file = try Io.Dir.openFileAbsolute(io, abs_file_path, .{});
    defer file.close(io);
    var file_reader = file.reader(io, &.{});
    return addCertsFromFile(cb, gpa, &file_reader, now.toSeconds());
}

pub fn addCertsFromFilePath(
    cb: *Bundle,
    gpa: Allocator,
    io: Io,
    now: Io.Timestamp,
    dir: Io.Dir,
    sub_file_path: []const u8,
) AddCertsFromFilePathError!void {
    var file = try dir.openFile(io, sub_file_path, .{});
    defer file.close(io);
    var file_reader = file.reader(io, &.{});
    return addCertsFromFile(cb, gpa, &file_reader, now.toSeconds());
}

pub const AddCertsFromFileError = Allocator.Error ||
    Io.File.Reader.Error ||
    Io.File.Reader.SizeError ||
    ParseCertError ||
    std.base64.Error ||
    error{ CertificateAuthorityBundleTooBig, MissingEndCertificateMarker, Streaming };

pub fn addCertsFromFile(cb: *Bundle, gpa: Allocator, file_reader: *Io.File.Reader, now_sec: i64) AddCertsFromFileError!void {
    const size = try file_reader.getSize();

    // We borrow `bytes` as a temporary buffer for the base64-encoded data.
    // This is possible by computing the decoded length and reserving the space
    // for the decoded bytes first.
    const decoded_size_upper_bound = size / 4 * 3;
    const needed_capacity = std.math.cast(u32, decoded_size_upper_bound + size) orelse
        return error.CertificateAuthorityBundleTooBig;
    try cb.bytes.ensureUnusedCapacity(gpa, needed_capacity);
    const end_reserved: u32 = @intCast(cb.bytes.items.len + decoded_size_upper_bound);
    const buffer = cb.bytes.allocatedSlice()[end_reserved..];
    const end_index = file_reader.interface.readSliceShort(buffer) catch |err| switch (err) {
        error.ReadFailed => return file_reader.err.?,
    };
    const encoded_bytes = buffer[0..end_index];

    const begin_marker = "-----BEGIN CERTIFICATE-----";
    const end_marker = "-----END CERTIFICATE-----";

    var start_index: usize = 0;
    while (mem.findPos(u8, encoded_bytes, start_index, begin_marker)) |begin_marker_start| {
        const cert_start = begin_marker_start + begin_marker.len;
        const cert_end = mem.findPos(u8, encoded_bytes, cert_start, end_marker) orelse
            return error.MissingEndCertificateMarker;
        start_index = cert_end + end_marker.len;
        const encoded_cert = mem.trim(u8, encoded_bytes[cert_start..cert_end], " \t\r\n");
        const decoded_start: u32 = @intCast(cb.bytes.items.len);
        const dest_buf = cb.bytes.allocatedSlice()[decoded_start..];
        cb.bytes.items.len += try base64.decode(dest_buf, encoded_cert);
        try cb.parseCert(gpa, decoded_start, now_sec);
    }
}

pub const ParseCertError = Allocator.Error || Certificate.ParseError;

pub fn parseCert(cb: *Bundle, gpa: Allocator, decoded_start: u32, now_sec: i64) ParseCertError!void {
    // Even though we could only partially parse the certificate to find
    // the subject name, we pre-parse all of them to make sure and only
    // include in the bundle ones that we know will parse. This way we can
    // use `catch unreachable` later.
    const parsed_cert = Certificate.parse(.{
        .buffer = cb.bytes.items,
        .index = decoded_start,
    }) catch |err| switch (err) {
        error.CertificateHasUnrecognizedObjectId => {
            cb.bytes.items.len = decoded_start;
            return;
        },
        else => |e| return e,
    };
    if (now_sec > parsed_cert.validity.not_after) {
        // Ignore expired cert.
        cb.bytes.items.len = decoded_start;
        return;
    }
    const gop = try cb.map.getOrPutContext(gpa, parsed_cert.subject_slice, .{ .cb = cb });
    if (gop.found_existing) {
        cb.bytes.items.len = decoded_start;
    } else {
        gop.value_ptr.* = decoded_start;
    }
}

const MapContext = struct {
    cb: *const Bundle,

    pub fn hash(ctx: MapContext, k: der.Element.Slice) u64 {
        return std.hash_map.hashString(ctx.cb.bytes.items[k.start..k.end]);
    }

    pub fn eql(ctx: MapContext, a: der.Element.Slice, b: der.Element.Slice) bool {
        const bytes = ctx.cb.bytes.items;
        return mem.eql(
            u8,
            bytes[a.start..a.end],
            bytes[b.start..b.end],
        );
    }
};

test "addCertsFromDirPath compiles and accepts an empty directory" {
    const io = std.testing.io;
    const gpa = std.testing.allocator;

    var tmp = std.testing.tmpDir(.{ .iterate = true });
    defer tmp.cleanup();

    var bundle: Bundle = .empty;
    defer bundle.deinit(gpa);

    try bundle.addCertsFromDirPath(gpa, io, tmp.dir, ".");
}

test "scan for OS-provided certificates" {
    if (builtin.os.tag == .wasi) return error.SkipZigTest;

    const io = std.testing.io;
    const gpa = std.testing.allocator;

    var bundle: Bundle = .empty;
    defer bundle.deinit(gpa);

    const now = Io.Clock.real.now(io);

    try bundle.rescan(gpa, io, now);
}



---
File: /std/crypto/Certificate/Chain.zig
---

//! A sequence of certificates, where each certificate is authenticated by the next certificate.
const Chain = @This();

store: ?crypt32.HCERTSTORE,
primary: ?*const crypt32.CERT_CONTEXT,

pub const empty: Chain = .{ .store = null, .primary = null };

pub fn deinit(chain: *Chain) void {
    if (chain.primary) |primary| assert(crypt32.CertFreeCertificateContext(primary).toBool());
    if (chain.store) |store| if (!crypt32.CertCloseStore(store, .{
        .CHECK = std.debug.runtime_safety,
    }).toBool()) std.os.windows.unexpectedError(std.os.windows.GetLastError()) catch unreachable;
    chain.* = .empty;
}

pub fn addCert(chain: *Chain, cert: []const u8) std.Io.UnexpectedError!void {
    const store = chain.store orelse store: {
        const store = crypt32.CertOpenStore(
            .MEMORY,
            .{},
            .NULL,
            .{},
            null,
        ) orelse return std.os.windows.unexpectedError(std.os.windows.GetLastError());
        chain.store = store;
        break :store store;
    };
    if (!crypt32.CertAddEncodedCertificateToStore(
        store,
        .{ .CERT = .ASN },
        cert.ptr,
        @intCast(cert.len),
        .ALWAYS,
        if (chain.primary) |_| null else &chain.primary,
    ).toBool()) return std.os.windows.unexpectedError(std.os.windows.GetLastError());
}

pub const VerifyError = error{
    TlsCertificateNotVerified,
} || std.Io.UnexpectedError;

pub fn verify(chain: *const Chain, now: std.Io.Timestamp) VerifyError!void {
    const now_win = @divFloor(now.nanoseconds - std.time.epoch.windows * std.time.ns_per_s, 100);
    var cert_chain: *const crypt32.CERT_CHAIN.CONTEXT = undefined;
    if (!crypt32.CertGetCertificateChain(
        .CURRENT_USER,
        chain.primary orelse return error.TlsCertificateNotVerified,
        &.{
            .dwLowDateTime = @bitCast(@as(i32, @truncate(now_win >> 0))),
            .dwHighDateTime = @bitCast(@as(i32, @intCast(now_win >> 32))),
        },
        null,
        &.{ .RequestedUsage = .{ .dwType = .AND, .Usage = .{
            .cUsageIdentifier = ALLOWED_EKUS.len,
            .rgpszUsageIdentifier = &ALLOWED_EKUS,
        } } },
        .{ .REVOCATION_CHECK_END_CERT = true, .REVOCATION_ACCUMULATIVE_TIMEOUT = true },
        null,
        &cert_chain,
    ).toBool()) return std.os.windows.unexpectedError(std.os.windows.GetLastError());
    defer crypt32.CertFreeCertificateChain(cert_chain);
    var status: crypt32.CERT_CHAIN.POLICY.STATUS = .{
        .dwError = undefined,
        .lChainIndex = undefined,
        .lElementIndex = undefined,
        .pvExtraPolicyStatus = undefined,
    };
    if (!crypt32.CertVerifyCertificateChainPolicy(
        .SSL,
        cert_chain,
        &.{
            .dwFlags = .{
                .IGNORE_END_REV_UNKNOWN = true,
                .IGNORE_CTL_SIGNER_REV_UNKNOWN = true,
                .IGNORE_CA_REV_UNKNOWN = true,
                .IGNORE_ROOT_REV_UNKNOWN = true,
            },
            .pvExtraPolicyPara = @constCast(&crypt32.HTTPSPolicyCallbackData{ .dwAuthType = .SERVER }),
        },
        &status,
    ).toBool()) return std.os.windows.unexpectedError(std.os.windows.GetLastError());
    switch (status.dwError) {
        .SUCCESS => return,
        .CERT_E_UNTRUSTEDROOT => return error.TlsCertificateNotVerified,
        else => |err| return std.os.windows.unexpectedError(err),
    }
}

const ALLOWED_EKUS = [_][*:0]const u8{"1.3.6.1.5.5.7.3.1"};

const assert = std.debug.assert;
const builtin = @import("builtin");
const std = @import("std");
const crypt32 = std.os.windows.crypt32;



---
File: /std/crypto/codecs/asn1/der/testdata/all_types.der
---

0/�� ��*asdffdsa�asdf


---
File: /std/crypto/codecs/asn1/der/testdata/id_ecc.pub.der
---

0Y0*�H�=*�H�=B �2��� S�`���̥��0���oN6N8�'��2ř��=O�l,�>>jA���<~�`f}�%


---
File: /std/crypto/codecs/asn1/der/ArrayListReverse.zig
---

//! An ArrayList that grows backwards. Counts nested prefix length fields
//! in O(n) instead of O(n^depth) at the cost of extra buffering.
//!
//! Laid out in memory like:
//! capacity  |--------------------------|
//! data                   |-------------|

const std = @import("std");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const testing = std.testing;

data: []u8,
capacity: usize,
allocator: Allocator,

const ArrayListReverse = @This();
const Error = Allocator.Error;

pub fn init(allocator: Allocator) ArrayListReverse {
    return .{ .data = &.{}, .capacity = 0, .allocator = allocator };
}

pub fn deinit(self: *ArrayListReverse) void {
    self.allocator.free(self.allocatedSlice());
}

pub fn ensureCapacity(self: *ArrayListReverse, new_capacity: usize) Error!void {
    if (self.capacity >= new_capacity) return;

    const old_memory = self.allocatedSlice();
    // Just make a new allocation to not worry about aliasing.
    const new_memory = try self.allocator.alloc(u8, new_capacity);
    @memcpy(new_memory[new_capacity - self.data.len ..], self.data);
    self.allocator.free(old_memory);
    self.data.ptr = new_memory.ptr + new_capacity - self.data.len;
    self.capacity = new_memory.len;
}

pub fn prependSlice(self: *ArrayListReverse, data: []const u8) Error!void {
    try self.ensureCapacity(self.data.len + data.len);
    const old_len = self.data.len;
    const new_len = old_len + data.len;
    assert(new_len <= self.capacity);
    self.data.len = new_len;

    const end = self.data.ptr;
    const begin = end - data.len;
    const slice = begin[0..data.len];
    @memcpy(slice, data);
    self.data.ptr = begin;
}

fn prependSliceSize(self: *ArrayListReverse, data: []const u8) Error!usize {
    try self.prependSlice(data);
    return data.len;
}

fn allocatedSlice(self: *ArrayListReverse) []u8 {
    return (self.data.ptr + self.data.len - self.capacity)[0..self.capacity];
}

/// Invalidates all element pointers.
pub fn clearAndFree(self: *ArrayListReverse) void {
    self.allocator.free(self.allocatedSlice());
    self.data.len = 0;
    self.capacity = 0;
}

/// The caller owns the returned memory.
/// Capacity is cleared, making deinit() safe but unnecessary to call.
pub fn toOwnedSlice(self: *ArrayListReverse) Error![]u8 {
    const new_memory = try self.allocator.alloc(u8, self.data.len);
    @memcpy(new_memory, self.data);
    @memset(self.data, undefined);
    self.clearAndFree();
    return new_memory;
}

test ArrayListReverse {
    var b = ArrayListReverse.init(testing.allocator);
    defer b.deinit();
    const data: []const u8 = &.{ 4, 5, 6 };
    try b.prependSlice(data);
    try testing.expectEqual(data.len, b.data.len);
    try testing.expectEqualSlices(u8, data, b.data);

    const data2: []const u8 = &.{ 1, 2, 3 };
    try b.prependSlice(data2);
    try testing.expectEqual(data.len + data2.len, b.data.len);
    try testing.expectEqualSlices(u8, data2 ++ data, b.data);
}



---
File: /std/crypto/codecs/asn1/der/Decoder.zig
---

//! A secure DER parser that:
//! - Prefers calling `fn decodeDer(self: @This(), decoder: *der.Decoder)`
//! - Does NOT allocate. If you wish to parse lists you can do so lazily
//!   with an opaque type.
//! - Does NOT read memory outside `bytes`.
//! - Does NOT return elements with slices outside `bytes`.
//! - Errors on values that do NOT follow DER rules:
//!   - Lengths that could be represented in a shorter form.
//!   - Booleans that are not 0xff or 0x00.
bytes: []const u8,
index: Index = 0,
/// The field tag of the most recently visited field.
/// This is needed because we might visit an implicitly tagged container with a `fn decodeDer`.
field_tag: ?FieldTag = null,

/// Expect a value.
pub fn any(self: *Decoder, comptime T: type) !T {
    if (std.meta.hasFn(T, "decodeDer")) return try T.decodeDer(self);

    const tag = Tag.fromZig(T).toExpected();
    switch (@typeInfo(T)) {
        .@"struct" => {
            const ele = try self.element(tag);
            defer self.index = ele.slice.end; // don't force parsing all fields

            var res: T = undefined;

            inline for (std.meta.fields(T)) |f| {
                self.field_tag = FieldTag.fromContainer(T, f.name);

                if (self.field_tag) |ft| {
                    if (ft.explicit) {
                        const seq = try self.element(ft.toTag().toExpected());
                        self.index = seq.slice.start;
                        self.field_tag = null;
                    }
                }

                @field(res, f.name) = self.any(f.type) catch |err| brk: {
                    if (f.defaultValue()) |d| {
                        break :brk d;
                    }
                    return err;
                };
                // DER encodes null values by skipping them.
                if (@typeInfo(f.type) == .optional and @field(res, f.name) == null) {
                    if (f.defaultValue()) |d| @field(res, f.name) = d;
                }
            }

            return res;
        },
        .bool => {
            const ele = try self.element(tag);
            const bytes = self.view(ele);
            if (bytes.len != 1) return error.InvalidBool;

            return switch (bytes[0]) {
                0x00 => false,
                0xff => true,
                else => error.InvalidBool,
            };
        },
        .int => {
            const ele = try self.element(tag);
            const bytes = self.view(ele);
            return try int(T, bytes);
        },
        .@"enum" => |e| {
            const ele = try self.element(tag);
            const bytes = self.view(ele);
            if (@hasDecl(T, "oids")) {
                return T.oids.oidToEnum(bytes) orelse return error.UnknownOid;
            }
            return @enumFromInt(try int(e.tag_type, bytes));
        },
        .optional => |o| return self.any(o.child) catch return null,
        else => @compileError("cannot decode type " ++ @typeName(T)),
    }
}

//// Expect a sequence.
pub fn sequence(self: *Decoder) !Element {
    return try self.element(ExpectedTag.init(.sequence, true, .universal));
}

//// Expect an element.
pub fn element(
    self: *Decoder,
    expected: ExpectedTag,
) (error{ EndOfStream, UnexpectedElement } || Element.DecodeError)!Element {
    if (self.index >= self.bytes.len) return error.EndOfStream;

    const res = try Element.decode(self.bytes, self.index);
    var e = expected;
    if (self.field_tag) |ft| {
        e.number = @enumFromInt(ft.number);
        e.class = ft.class;
    }
    if (!e.match(res.tag)) {
        return error.UnexpectedElement;
    }

    self.index = if (res.tag.constructed) res.slice.start else res.slice.end;
    return res;
}

/// View of element bytes.
pub fn view(self: Decoder, elem: Element) []const u8 {
    return elem.slice.view(self.bytes);
}

fn int(comptime T: type, value: []const u8) error{ NonCanonical, LargeValue }!T {
    if (@typeInfo(T).int.bits % 8 != 0) @compileError("T must be byte aligned");

    var bytes = value;
    if (bytes.len >= 2) {
        if (bytes[0] == 0) {
            if (@clz(bytes[1]) > 0) return error.NonCanonical;
            bytes.ptr += 1;
        }
        if (bytes[0] == 0xff and @clz(bytes[1]) == 0) return error.NonCanonical;
    }

    if (bytes.len > @sizeOf(T)) return error.LargeValue;
    if (@sizeOf(T) == 1) return @bitCast(bytes[0]);

    return std.mem.readVarInt(T, bytes, .big);
}

test int {
    try expectEqual(@as(u8, 1), try int(u8, &[_]u8{1}));
    try expectError(error.NonCanonical, int(u8, &[_]u8{ 0, 1 }));
    try expectError(error.NonCanonical, int(u8, &[_]u8{ 0xff, 0xff }));

    const big = [_]u8{ 0xef, 0xff };
    try expectError(error.LargeValue, int(u8, &big));
    try expectEqual(0xefff, int(u16, &big));
}

test Decoder {
    var parser = Decoder{ .bytes = @embedFile("./testdata/id_ecc.pub.der") };
    const seq = try parser.sequence();

    {
        const seq2 = try parser.sequence();
        _ = try parser.element(ExpectedTag.init(.oid, false, .universal));
        _ = try parser.element(ExpectedTag.init(.oid, false, .universal));

        try std.testing.expectEqual(parser.index, seq2.slice.end);
    }
    _ = try parser.element(ExpectedTag.init(.bitstring, false, .universal));

    try std.testing.expectEqual(parser.index, seq.slice.end);
    try std.testing.expectEqual(parser.index, parser.bytes.len);
}

const std = @import("std");
const builtin = @import("builtin");
const asn1 = @import("../../asn1.zig");
const Oid = @import("../Oid.zig");

const expectEqual = std.testing.expectEqual;
const expectError = std.testing.expectError;
const Decoder = @This();
const Index = asn1.Index;
const Tag = asn1.Tag;
const FieldTag = asn1.FieldTag;
const ExpectedTag = asn1.ExpectedTag;
const Element = asn1.Element;



---
File: /std/crypto/codecs/asn1/der/Encoder.zig
---

//! A buffered DER encoder.
//!
//! Prefers calling container's `fn encodeDer(self: @This(), encoder: *der.Encoder)`.
//! That function should encode values, lengths, then tags.
buffer: ArrayListReverse,
/// The field tag set by a parent container.
/// This is needed because we might visit an implicitly tagged container with a `fn encodeDer`.
field_tag: ?FieldTag = null,

pub fn init(allocator: std.mem.Allocator) Encoder {
    return Encoder{ .buffer = ArrayListReverse.init(allocator) };
}

pub fn deinit(self: *Encoder) void {
    self.buffer.deinit();
}

/// Encode any value.
pub fn any(self: *Encoder, val: anytype) !void {
    const T = @TypeOf(val);
    try self.anyTag(Tag.fromZig(T), val);
}

fn anyTag(self: *Encoder, tag_: Tag, val: anytype) !void {
    const T = @TypeOf(val);
    if (std.meta.hasFn(T, "encodeDer")) return try val.encodeDer(self);
    const start = self.buffer.data.len;
    const merged_tag = self.mergedTag(tag_);

    switch (@typeInfo(T)) {
        .@"struct" => |info| {
            inline for (0..info.fields.len) |i| {
                const f = info.fields[info.fields.len - i - 1];
                const field_val = @field(val, f.name);
                const field_tag = FieldTag.fromContainer(T, f.name);

                // > The encoding of a set value or sequence value shall not include an encoding for any
                // > component value which is equal to its default value.
                const is_default = if (f.is_comptime) false else if (f.default_value_ptr) |v| brk: {
                    const default_val: *const f.type = @ptrCast(@alignCast(v));
                    break :brk std.mem.eql(u8, std.mem.asBytes(default_val), std.mem.asBytes(&field_val));
                } else false;

                if (!is_default) {
                    const start2 = self.buffer.data.len;
                    self.field_tag = field_tag;
                    // will merge with self.field_tag.
                    // may mutate self.field_tag.
                    try self.anyTag(Tag.fromZig(f.type), field_val);
                    if (field_tag) |ft| {
                        if (ft.explicit) {
                            try self.length(self.buffer.data.len - start2);
                            try self.tag(ft.toTag());
                            self.field_tag = null;
                        }
                    }
                }
            }
        },
        .bool => try self.buffer.prependSlice(&[_]u8{if (val) 0xff else 0}),
        .int => try self.int(T, val),
        .@"enum" => |e| {
            if (@hasDecl(T, "oids")) {
                return self.any(T.oids.enumToOid(val));
            } else {
                try self.int(e.tag_type, @intFromEnum(val));
            }
        },
        .optional => if (val) |v| return try self.anyTag(tag_, v),
        .null => {},
        else => @compileError("cannot encode type " ++ @typeName(T)),
    }

    try self.length(self.buffer.data.len - start);
    try self.tag(merged_tag);
}

/// Encode a tag.
pub fn tag(self: *Encoder, tag_: Tag) !void {
    const t = self.mergedTag(tag_);
    try t.encode(self.writer());
}

fn mergedTag(self: *Encoder, tag_: Tag) Tag {
    var res = tag_;
    if (self.field_tag) |ft| {
        if (!ft.explicit) {
            res.number = @enumFromInt(ft.number);
            res.class = ft.class;
        }
    }
    return res;
}

/// Encode a length.
pub fn length(self: *Encoder, len: usize) !void {
    const writer_ = self.writer();
    if (len < 128) {
        try writer_.writeInt(u8, @intCast(len), .big);
        return;
    }
    inline for ([_]type{ u8, u16, u32 }) |T| {
        if (len < std.math.maxInt(T)) {
            try writer_.writeInt(T, @intCast(len), .big);
            try writer_.writeInt(u8, @sizeOf(T) | 0x80, .big);
            return;
        }
    }
    return error.InvalidLength;
}

/// Encode a tag and length-prefixed bytes.
pub fn tagBytes(self: *Encoder, tag_: Tag, bytes: []const u8) !void {
    try self.buffer.prependSlice(bytes);
    try self.length(bytes.len);
    try self.tag(tag_);
}

/// Warning: This writer writes backwards. `fn print` will NOT work as expected.
pub fn writer(self: *Encoder) ArrayListReverse.Writer {
    return self.buffer.writer();
}

fn int(self: *Encoder, comptime T: type, value: T) !void {
    const big = std.mem.nativeTo(T, value, .big);
    const big_bytes = std.mem.asBytes(&big);

    const bits_needed = @bitSizeOf(T) - @clz(value);
    const needs_padding: u1 = if (value == 0)
        1
    else if (bits_needed > 8) brk: {
        const RightShift = std.meta.Int(.unsigned, @bitSizeOf(@TypeOf(bits_needed)) - 1);
        const right_shift: RightShift = @intCast(bits_needed - 9);
        break :brk if (value >> right_shift == 0x1ff) 1 else 0;
    } else 0;
    const bytes_needed = try std.math.divCeil(usize, bits_needed, 8) + needs_padding;

    const writer_ = self.writer();
    for (0..bytes_needed - needs_padding) |i| try writer_.writeByte(big_bytes[big_bytes.len - i - 1]);
    if (needs_padding == 1) try writer_.writeByte(0);
}

test int {
    const allocator = std.testing.allocator;
    var encoder = Encoder.init(allocator);
    defer encoder.deinit();

    try encoder.int(u8, 0);
    try std.testing.expectEqualSlices(u8, &[_]u8{0}, encoder.buffer.data);

    encoder.buffer.clearAndFree();
    try encoder.int(u16, 0x00ff);
    try std.testing.expectEqualSlices(u8, &[_]u8{0xff}, encoder.buffer.data);

    encoder.buffer.clearAndFree();
    try encoder.int(u32, 0xffff);
    try std.testing.expectEqualSlices(u8, &[_]u8{ 0, 0xff, 0xff }, encoder.buffer.data);
}

const std = @import("std");
const Oid = @import("../Oid.zig");
const asn1 = @import("../../asn1.zig");
const ArrayListReverse = @import("./ArrayListReverse.zig");
const Tag = asn1.Tag;
const FieldTag = asn1.FieldTag;
const Encoder = @This();



---
File: /std/crypto/codecs/asn1/der.zig
---

//! Distinguised Encoding Rules as defined in X.690 and X.691.
//!
//! Subset of Basic Encoding Rules (BER) which eliminates flexibility in
//! an effort to acheive normality. Used in PKI.
const std = @import("std");
const asn1 = @import("../asn1.zig");

pub const Decoder = @import("der/Decoder.zig");
pub const Encoder = @import("der/Encoder.zig");

pub fn decode(comptime T: type, encoded: []const u8) !T {
    var decoder = Decoder{ .bytes = encoded };
    const res = try decoder.any(T);
    std.debug.assert(decoder.index == encoded.len);
    return res;
}

/// Caller owns returned memory.
pub fn encode(allocator: std.mem.Allocator, value: anytype) ![]u8 {
    var encoder = Encoder.init(allocator);
    defer encoder.deinit();
    try encoder.any(value);
    return try encoder.buffer.toOwnedSlice();
}

test encode {
    // https://lapo.it/asn1js/#MAgGAyoDBAIBBA
    const Value = struct { a: asn1.Oid, b: i32 };
    const test_case = .{
        .value = Value{ .a = asn1.Oid.fromDotComptime("1.2.3.4"), .b = 4 },
        .encoded = &[_]u8{ 0x30, 0x08, 0x06, 0x03, 0x2A, 0x03, 0x04, 0x02, 0x01, 0x04 },
    };
    const allocator = std.testing.allocator;
    const actual = try encode(allocator, test_case.value);
    defer allocator.free(actual);

    try std.testing.expectEqualSlices(u8, test_case.encoded, actual);
}

test decode {
    // https://lapo.it/asn1js/#MAgGAyoDBAIBBA
    const Value = struct { a: asn1.Oid, b: i32 };
    const test_case = .{
        .value = Value{ .a = asn1.Oid.fromDotComptime("1.2.3.4"), .b = 4 },
        .encoded = &[_]u8{ 0x30, 0x08, 0x06, 0x03, 0x2A, 0x03, 0x04, 0x02, 0x01, 0x04 },
    };
    const decoded = try decode(Value, test_case.encoded);

    try std.testing.expectEqualDeep(test_case.value, decoded);
}

test {
    _ = Decoder;
    _ = Encoder;
}



---
File: /std/crypto/codecs/asn1/Oid.zig
---

//! Globally unique hierarchical identifier made of a sequence of integers.
//!
//! Commonly used to identify standards, algorithms, certificate extensions,
//! organizations, or policy documents.
encoded: []const u8,

pub const InitError = std.fmt.ParseIntError || error{MissingPrefix} || std.Io.Writer.Error;

pub fn fromDot(dot_notation: []const u8, out: []u8) InitError!Oid {
    var split = std.mem.splitScalar(u8, dot_notation, '.');
    const first_str = split.next() orelse return error.MissingPrefix;
    const second_str = split.next() orelse return error.MissingPrefix;

    const first = try std.fmt.parseInt(u8, first_str, 10);
    const second = try std.fmt.parseInt(u8, second_str, 10);

    var writer: std.Io.Writer = .fixed(out);

    try writer.writeByte(first * 40 + second);

    var i: usize = 1;
    while (split.next()) |s| {
        var parsed = try std.fmt.parseUnsigned(Arc, s, 10);
        const n_bytes = if (parsed == 0) 0 else std.math.log(Arc, encoding_base, parsed);

        for (0..n_bytes) |j| {
            const place = std.math.pow(Arc, encoding_base, n_bytes - @as(Arc, @intCast(j)));
            const digit: u8 = @intCast(@divFloor(parsed, place));

            try writer.writeByte(digit | 0x80);
            parsed -= digit * place;

            i += 1;
        }
        try writer.writeByte(@intCast(parsed));
        i += 1;
    }

    return .{ .encoded = writer.buffered() };
}

test fromDot {
    var buf: [256]u8 = undefined;
    for (test_cases) |t| {
        const actual = try fromDot(t.dot_notation, &buf);
        try std.testing.expectEqualSlices(u8, t.encoded, actual.encoded);
    }
}

pub fn toDot(self: Oid, writer: anytype) @TypeOf(writer).Error!void {
    const encoded = self.encoded;
    const first = @divTrunc(encoded[0], 40);
    const second = encoded[0] - first * 40;
    try writer.print("{d}.{d}", .{ first, second });

    var i: usize = 1;
    while (i != encoded.len) {
        const n_bytes: usize = brk: {
            var res: usize = 1;
            var j: usize = i;
            while (encoded[j] & 0x80 != 0) {
                res += 1;
                j += 1;
            }
            break :brk res;
        };

        var n: usize = 0;
        for (0..n_bytes) |j| {
            const place = std.math.pow(usize, encoding_base, n_bytes - j - 1);
            n += place * (encoded[i] & 0b01111111);
            i += 1;
        }
        try writer.print(".{d}", .{n});
    }
}

test toDot {
    var buf: [256]u8 = undefined;

    for (test_cases) |t| {
        var stream: std.Io.Writer = .fixed(&buf);
        try toDot(Oid{ .encoded = t.encoded }, &stream);
        try std.testing.expectEqualStrings(t.dot_notation, stream.written());
    }
}

const TestCase = struct {
    encoded: []const u8,
    dot_notation: []const u8,

    pub fn init(comptime hex: []const u8, dot_notation: []const u8) TestCase {
        return .{ .encoded = &hexToBytes(hex), .dot_notation = dot_notation };
    }
};

const test_cases = [_]TestCase{
    // https://learn.microsoft.com/en-us/windows/win32/seccertenroll/about-object-identifier
    TestCase.init("2b0601040182371514", "1.3.6.1.4.1.311.21.20"),
    // https://luca.ntop.org/Teaching/Appunti/asn1.html
    TestCase.init("2a864886f70d", "1.2.840.113549"),
    // https://www.sysadmins.lv/blog-en/how-to-encode-object-identifier-to-an-asn1-der-encoded-string.aspx
    TestCase.init("2a868d20", "1.2.100000"),
    TestCase.init("2a864886f70d01010b", "1.2.840.113549.1.1.11"),
    TestCase.init("2b6570", "1.3.101.112"),
};

pub const asn1_tag = asn1.Tag.init(.oid, false, .universal);

pub fn decodeDer(decoder: *der.Decoder) !Oid {
    const ele = try decoder.element(asn1_tag.toExpected());
    return Oid{ .encoded = decoder.view(ele) };
}

pub fn encodeDer(self: Oid, encoder: *der.Encoder) !void {
    try encoder.tagBytes(asn1_tag, self.encoded);
}

fn encodedLen(dot_notation: []const u8) usize {
    var buf: [256]u8 = undefined;
    const oid = fromDot(dot_notation, &buf) catch unreachable;
    return oid.encoded.len;
}

/// Returns encoded bytes of OID.
fn encodeComptime(comptime dot_notation: []const u8) [encodedLen(dot_notation)]u8 {
    @setEvalBranchQuota(4000);
    comptime var buf: [256]u8 = undefined;
    const oid = comptime fromDot(dot_notation, &buf) catch unreachable;
    return oid.encoded[0..oid.encoded.len].*;
}

test encodeComptime {
    try std.testing.expectEqual(
        hexToBytes("2b0601040182371514"),
        comptime encodeComptime("1.3.6.1.4.1.311.21.20"),
    );
}

pub fn fromDotComptime(comptime dot_notation: []const u8) Oid {
    const tmp = comptime encodeComptime(dot_notation);
    return Oid{ .encoded = &tmp };
}

/// Maps of:
/// - Oid -> enum
/// - Enum -> oid
pub fn StaticMap(comptime Enum: type) type {
    const enum_info = @typeInfo(Enum).@"enum";
    const EnumToOid = std.EnumArray(Enum, []const u8);
    const ReturnType = struct {
        oid_to_enum: std.StaticStringMap(Enum),
        enum_to_oid: EnumToOid,

        pub fn oidToEnum(self: @This(), encoded: []const u8) ?Enum {
            return self.oid_to_enum.get(encoded);
        }

        pub fn enumToOid(self: @This(), value: Enum) Oid {
            const bytes = self.enum_to_oid.get(value);
            return .{ .encoded = bytes };
        }
    };

    return struct {
        pub fn initComptime(comptime key_pairs: anytype) ReturnType {
            const struct_info = @typeInfo(@TypeOf(key_pairs)).@"struct";
            const error_msg = "Each field of '" ++ @typeName(Enum) ++ "' must map to exactly one OID";
            if (!enum_info.is_exhaustive or enum_info.fields.len != struct_info.fields.len) {
                @compileError(error_msg);
            }

            comptime var enum_to_oid = EnumToOid.initUndefined();

            const KeyPair = struct { []const u8, Enum };
            comptime var static_key_pairs: [enum_info.fields.len]KeyPair = undefined;

            comptime for (enum_info.fields, 0..) |f, i| {
                if (!@hasField(@TypeOf(key_pairs), f.name)) {
                    @compileError("Field '" ++ f.name ++ "' missing Oid.StaticMap entry");
                }
                const encoded = &encodeComptime(@field(key_pairs, f.name));
                const tag: Enum = @enumFromInt(f.value);
                static_key_pairs[i] = .{ encoded, tag };
                enum_to_oid.set(tag, encoded);
            };

            const oid_to_enum = std.StaticStringMap(Enum).initComptime(static_key_pairs);
            if (oid_to_enum.values().len != enum_info.fields.len) @compileError(error_msg);

            return ReturnType{ .oid_to_enum = oid_to_enum, .enum_to_oid = enum_to_oid };
        }
    };
}

/// Strictly for testing.
fn hexToBytes(comptime hex: []const u8) [hex.len / 2]u8 {
    var res: [hex.len / 2]u8 = undefined;
    _ = std.fmt.hexToBytes(&res, hex) catch unreachable;
    return res;
}

const std = @import("std");
const Oid = @This();
const Arc = u32;
const encoding_base = 128;
const Allocator = std.mem.Allocator;
const der = @import("der.zig");
const asn1 = @import("../asn1.zig");



---
File: /std/crypto/codecs/asn1/test.zig
---




---
File: /std/crypto/codecs/asn1.zig
---

//! ASN.1 types for public consumption.
const std = @import("std");
pub const der = @import("./asn1/der.zig");
pub const Oid = @import("./asn1/Oid.zig");

pub const Index = u32;

pub const Tag = struct {
    number: Number,
    /// Whether this ASN.1 type contains other ASN.1 types.
    constructed: bool,
    class: Class,

    /// These values apply to class == .universal.
    pub const Number = enum(u16) {
        // 0 is reserved by spec
        boolean = 1,
        integer = 2,
        bitstring = 3,
        octetstring = 4,
        null = 5,
        oid = 6,
        object_descriptor = 7,
        real = 9,
        enumerated = 10,
        embedded = 11,
        string_utf8 = 12,
        oid_relative = 13,
        time = 14,
        // 15 is reserved to mean that the tag is >= 32
        sequence = 16,
        /// Elements may appear in any order.
        sequence_of = 17,
        string_numeric = 18,
        string_printable = 19,
        string_teletex = 20,
        string_videotex = 21,
        string_ia5 = 22,
        utc_time = 23,
        generalized_time = 24,
        string_graphic = 25,
        string_visible = 26,
        string_general = 27,
        string_universal = 28,
        string_char = 29,
        string_bmp = 30,
        date = 31,
        time_of_day = 32,
        date_time = 33,
        duration = 34,
        /// IRI = Internationalized Resource Identifier
        oid_iri = 35,
        oid_iri_relative = 36,
        _,
    };

    pub const Class = enum(u2) {
        universal,
        application,
        context_specific,
        private,
    };

    pub fn init(number: Tag.Number, constructed: bool, class: Tag.Class) Tag {
        return .{ .number = number, .constructed = constructed, .class = class };
    }

    pub fn universal(number: Tag.Number, constructed: bool) Tag {
        return .{ .number = number, .constructed = constructed, .class = .universal };
    }

    pub fn decode(reader: *std.Io.Reader) !Tag {
        const tag1: FirstTag = @bitCast(try reader.takeByte());
        var number: u14 = tag1.number;

        if (tag1.number == 31) {
            const tag2: NextTag = @bitCast(try reader.takeByte());
            number = tag2.number;
            if (tag2.continues) {
                const tag3: NextTag = @bitCast(try reader.takeByte());
                number = (number << 7) + tag3.number;
                if (tag3.continues) return error.EndOfStream;
            }
        }

        return Tag{
            .number = @enumFromInt(number),
            .constructed = tag1.constructed,
            .class = tag1.class,
        };
    }

    pub fn encode(self: Tag, writer: *std.Io.Writer) @TypeOf(writer).Error!void {
        var tag1 = FirstTag{
            .number = undefined,
            .constructed = self.constructed,
            .class = self.class,
        };

        var buffer: [3]u8 = undefined;
        var writer2: std.Io.Writer = .init(&buffer);

        switch (@intFromEnum(self.number)) {
            0...std.math.maxInt(u5) => |n| {
                tag1.number = @intCast(n);
                writer2.writeByte(@bitCast(tag1)) catch unreachable;
            },
            std.math.maxInt(u5) + 1...std.math.maxInt(u7) => |n| {
                tag1.number = 15;
                const tag2 = NextTag{ .number = @intCast(n), .continues = false };
                writer2.writeByte(@bitCast(tag1)) catch unreachable;
                writer2.writeByte(@bitCast(tag2)) catch unreachable;
            },
            else => |n| {
                tag1.number = 15;
                const tag2 = NextTag{ .number = @intCast(n >> 7), .continues = true };
                const tag3 = NextTag{ .number = @truncate(n), .continues = false };
                writer2.writeByte(@bitCast(tag1)) catch unreachable;
                writer2.writeByte(@bitCast(tag2)) catch unreachable;
                writer2.writeByte(@bitCast(tag3)) catch unreachable;
            },
        }

        _ = try writer.write(writer2.buffered());
    }

    const FirstTag = packed struct(u8) { number: u5, constructed: bool, class: Tag.Class };
    const NextTag = packed struct(u8) { number: u7, continues: bool };

    pub fn toExpected(self: Tag) ExpectedTag {
        return ExpectedTag{
            .number = self.number,
            .constructed = self.constructed,
            .class = self.class,
        };
    }

    pub fn fromZig(comptime T: type) Tag {
        switch (@typeInfo(T)) {
            .@"struct", .@"enum", .@"union" => {
                if (@hasDecl(T, "asn1_tag")) return T.asn1_tag;
            },
            else => {},
        }

        switch (@typeInfo(T)) {
            .@"struct", .@"union" => return universal(.sequence, true),
            .bool => return universal(.boolean, false),
            .int => return universal(.integer, false),
            .@"enum" => |e| {
                if (@hasDecl(T, "oids")) return Oid.asn1_tag;
                return universal(if (e.is_exhaustive) .enumerated else .integer, false);
            },
            .optional => |o| return fromZig(o.child),
            .null => return universal(.null, false),
            else => @compileError("cannot map Zig type to asn1_tag " ++ @typeName(T)),
        }
    }
};

test Tag {
    const buf = [_]u8{0xa3};
    var reader: std.Io.Reader = .fixed(&buf);
    const t = Tag.decode(&reader);
    try std.testing.expectEqual(Tag.init(@enumFromInt(3), true, .context_specific), t);
}

/// A decoded view.
pub const Element = struct {
    tag: Tag,
    slice: Slice,

    pub const Slice = struct {
        start: Index,
        end: Index,

        pub fn len(self: Slice) Index {
            return self.end - self.start;
        }

        pub fn view(self: Slice, bytes: []const u8) []const u8 {
            return bytes[self.start..self.end];
        }
    };

    pub const DecodeError = error{EndOfStream};

    /// Safely decode a DER/BER/CER element at `index`:
    /// - Ensures length uses shortest form
    /// - Ensures length is within `bytes`
    /// - Ensures length is less than `std.math.maxInt(Index)`
    pub fn decode(bytes: []const u8, index: Index) DecodeError!Element {
        var reader: std.Io.Reader = .fixed(bytes[index..]);

        const tag = Tag.decode(&reader) catch |err| switch (err) {
            error.ReadFailed => unreachable, // it's all fixed buffers
            else => |e| return e,
        };
        const size_or_len_size = reader.takeByte() catch |err| switch (err) {
            error.ReadFailed => unreachable, // it's all fixed buffers
            else => |e| return e,
        };

        const len = if (size_or_len_size < 128)
            // short form between 0-127
            size_or_len_size
        else blk: {
            // long form between 0 and std.math.maxInt(u1024)
            const len_size: u7 = @truncate(size_or_len_size);
            if (len_size > @sizeOf(Index)) return error.EndOfStream;

            const len = reader.takeVarInt(Index, .big, len_size) catch |err| switch (err) {
                error.ReadFailed => unreachable, // it's all fixed buffers
                else => |e| return e,
            };
            if (len < 128) return error.EndOfStream; // should have used short form

            break :blk len;
        };

        const start = index + @as(Index, @intCast(reader.seek));
        const end = std.math.add(Index, start, len) catch return error.EndOfStream;
        if (end > bytes.len) return error.EndOfStream;

        return Element{ .tag = tag, .slice = Slice{ .start = start, .end = end } };
    }
};

test Element {
    const short_form = [_]u8{ 0x30, 0x03, 0x02, 0x01, 0x09 };
    try std.testing.expectEqual(Element{
        .tag = Tag.universal(.sequence, true),
        .slice = Element.Slice{ .start = 2, .end = short_form.len },
    }, Element.decode(&short_form, 0));

    const long_form = [_]u8{ 0x30, 129, 129 } ++ [_]u8{0} ** 129;
    try std.testing.expectEqual(Element{
        .tag = Tag.universal(.sequence, true),
        .slice = Element.Slice{ .start = 3, .end = long_form.len },
    }, Element.decode(&long_form, 0));

    const multi_byte_tag = [_]u8{ 0x1F, 0x20, 0x08, 0x30, 0x36, 0x3A, 0x32, 0x37, 0x3A, 0x31, 0x35 };
    try std.testing.expectEqual(Element{
        .tag = Tag.universal(.time_of_day, false),
        .slice = Element.Slice{ .start = 3, .end = multi_byte_tag.len },
    }, Element.decode(&multi_byte_tag, 0));
}

/// For decoding.
pub const ExpectedTag = struct {
    number: ?Tag.Number = null,
    constructed: ?bool = null,
    class: ?Tag.Class = null,

    pub fn init(number: ?Tag.Number, constructed: ?bool, class: ?Tag.Class) ExpectedTag {
        return .{ .number = number, .constructed = constructed, .class = class };
    }

    pub fn primitive(number: ?Tag.Number) ExpectedTag {
        return .{ .number = number, .constructed = false, .class = .universal };
    }

    pub fn match(self: ExpectedTag, tag: Tag) bool {
        if (self.number) |e| {
            if (tag.number != e) return false;
        }
        if (self.constructed) |e| {
            if (tag.constructed != e) return false;
        }
        if (self.class) |e| {
            if (tag.class != e) return false;
        }
        return true;
    }
};

pub const FieldTag = struct {
    number: std.meta.Tag(Tag.Number),
    class: Tag.Class,
    explicit: bool = true,

    pub fn initExplicit(number: std.meta.Tag(Tag.Number), class: Tag.Class) FieldTag {
        return .{ .number = number, .class = class, .explicit = true };
    }

    pub fn initImplicit(number: std.meta.Tag(Tag.Number), class: Tag.Class) FieldTag {
        return .{ .number = number, .class = class, .explicit = false };
    }

    pub fn fromContainer(comptime Container: type, comptime field_name: []const u8) ?FieldTag {
        if (@hasDecl(Container, "asn1_tags") and @hasField(@TypeOf(Container.asn1_tags), field_name)) {
            return @field(Container.asn1_tags, field_name);
        }

        return null;
    }

    pub fn toTag(self: FieldTag) Tag {
        return Tag.init(@enumFromInt(self.number), self.explicit, self.class);
    }
};

pub const BitString = struct {
    /// Number of bits in rightmost byte that are unused.
    right_padding: u3 = 0,
    bytes: []const u8,

    pub fn bitLen(self: BitString) usize {
        return self.bytes.len * 8 - self.right_padding;
    }

    const asn1_tag = Tag.universal(.bitstring, false);

    pub fn decodeDer(decoder: *der.Decoder) !BitString {
        const ele = try decoder.element(asn1_tag.toExpected());
        const bytes = decoder.view(ele);

        if (bytes.len < 1) return error.InvalidBitString;
        const padding = bytes[0];
        if (padding >= 8) return error.InvalidBitString;
        const right_padding: u3 = @intCast(padding);

        // DER requires that unused bits be zero.
        if (@ctz(bytes[bytes.len - 1]) < right_padding) return error.InvalidBitString;

        return BitString{ .bytes = bytes[1..], .right_padding = right_padding };
    }

    pub fn encodeDer(self: BitString, encoder: *der.Encoder) !void {
        try encoder.writer().writeAll(self.bytes);
        try encoder.writer().writeByte(self.right_padding);
        try encoder.length(self.bytes.len + 1);
        try encoder.tag(asn1_tag);
    }
};

pub fn Opaque(comptime tag: Tag) type {
    return struct {
        bytes: []const u8,

        pub fn decodeDer(decoder: *der.Decoder) !@This() {
            const ele = try decoder.element(tag.toExpected());
            if (tag.constructed) decoder.index = ele.slice.end;
            return .{ .bytes = decoder.view(ele) };
        }

        pub fn encodeDer(self: @This(), encoder: *der.Encoder) !void {
            try encoder.tagBytes(tag, self.bytes);
        }
    };
}

/// Use sparingly.
pub const Any = struct {
    tag: Tag,
    bytes: []const u8,

    pub fn decodeDer(decoder: *der.Decoder) !@This() {
        const ele = try decoder.element(ExpectedTag{});
        return .{ .tag = ele.tag, .bytes = decoder.view(ele) };
    }

    pub fn encodeDer(self: @This(), encoder: *der.Encoder) !void {
        try encoder.tagBytes(self.tag, self.bytes);
    }
};

test {
    _ = der;
    _ = Oid;
    _ = @import("asn1/test.zig");
}



---
File: /std/crypto/codecs/base64_hex_ct.zig
---

//! Hexadecimal and Base64 codecs designed for cryptographic use.
//! This file provides (best-effort) constant-time encoding and decoding functions for hexadecimal and Base64 formats.
//! This is designed to be used in cryptographic applications where timing attacks are a concern.
const std = @import("std");
const testing = std.testing;
const StaticBitSet = std.StaticBitSet;

pub const Error = error{
    /// An invalid character was found in the input.
    InvalidCharacter,
    /// The input is not properly padded.
    InvalidPadding,
    /// The input buffer is too small to hold the output.
    NoSpaceLeft,
    /// The input and output buffers are not the same size.
    SizeMismatch,
};

/// (best-effort) constant time hexadecimal encoding and decoding.
pub const hex = struct {
    /// Encodes a binary buffer into a hexadecimal string.
    /// The output buffer must be twice the size of the input buffer.
    pub fn encode(encoded: []u8, bin: []const u8, comptime case: std.fmt.Case) error{SizeMismatch}!void {
        if (encoded.len / 2 != bin.len) {
            return error.SizeMismatch;
        }
        for (bin, 0..) |v, i| {
            const b: u16 = v >> 4;
            const c: u16 = v & 0xf;
            const off = if (case == .upper) 32 else 0;
            const x =
                ((87 - off + c + (((c -% 10) >> 8) & ~@as(u16, 38 - off))) & 0xff) << 8 |
                ((87 - off + b + (((b -% 10) >> 8) & ~@as(u16, 38 - off))) & 0xff);
            encoded[i * 2] = @truncate(x);
            encoded[i * 2 + 1] = @truncate(x >> 8);
        }
    }

    /// Decodes a hexadecimal string into a binary buffer.
    /// The output buffer must be half the size of the input buffer.
    pub fn decode(bin: []u8, encoded: []const u8) error{ SizeMismatch, InvalidCharacter, InvalidPadding }!void {
        if (encoded.len % 2 != 0) {
            return error.InvalidPadding;
        }
        if (bin.len < encoded.len / 2) {
            return error.SizeMismatch;
        }
        _ = decodeAny(bin, encoded, null) catch |err| {
            switch (err) {
                error.InvalidCharacter => return error.InvalidCharacter,
                error.InvalidPadding => return error.InvalidPadding,
                else => unreachable,
            }
        };
    }

    /// A decoder that ignores certain characters.
    /// The decoder will skip any characters that are in the ignore list.
    pub const DecoderWithIgnore = struct {
        /// The characters to ignore.
        ignored_chars: StaticBitSet(256) = undefined,

        /// Decodes a hexadecimal string into a binary buffer.
        /// The output buffer must be half the size of the input buffer.
        pub fn decode(
            self: DecoderWithIgnore,
            bin: []u8,
            encoded: []const u8,
        ) error{ NoSpaceLeft, InvalidCharacter, InvalidPadding }![]const u8 {
            return decodeAny(bin, encoded, self.ignored_chars);
        }

        /// Returns the decoded length of a hexadecimal string, ignoring any characters in the ignore list.
        /// This operation does not run in constant time, but it aims to avoid leaking information about the underlying hexadecimal string.
        pub fn decodedLenForSlice(decoder: DecoderWithIgnore, encoded: []const u8) !usize {
            var hex_len = encoded.len;
            for (encoded) |c| {
                if (decoder.ignored_chars.isSet(c)) hex_len -= 1;
            }
            if (hex_len % 2 != 0) {
                return error.InvalidPadding;
            }
            return hex_len / 2;
        }

        /// Returns the maximum possible decoded size for a given input length after skipping ignored characters.
        pub fn decodedLenUpperBound(hex_len: usize) usize {
            return hex_len / 2;
        }
    };

    /// Creates a new decoder that ignores certain characters.
    /// The decoder will skip any characters that are in the ignore list.
    /// The ignore list must not contain any valid hexadecimal characters.
    pub fn decoderWithIgnore(ignore_chars: []const u8) error{InvalidCharacter}!DecoderWithIgnore {
        var ignored_chars = StaticBitSet(256).empty;
        for (ignore_chars) |c| {
            switch (c) {
                '0'...'9', 'a'...'f', 'A'...'F' => return error.InvalidCharacter,
                else => if (ignored_chars.isSet(c)) return error.InvalidCharacter,
            }
            ignored_chars.set(c);
        }
        return DecoderWithIgnore{ .ignored_chars = ignored_chars };
    }

    fn decodeAny(
        bin: []u8,
        encoded: []const u8,
        ignored_chars: ?StaticBitSet(256),
    ) error{ NoSpaceLeft, InvalidCharacter, InvalidPadding }![]const u8 {
        var bin_pos: usize = 0;
        var state: bool = false;
        var c_acc: u8 = 0;
        for (encoded) |c| {
            const c_num = c ^ 48;
            const c_num0: u8 = @truncate((@as(u16, c_num) -% 10) >> 8);
            const c_alpha: u8 = (c & ~@as(u8, 32)) -% 55;
            const c_alpha0: u8 = @truncate(((@as(u16, c_alpha) -% 10) ^ (@as(u16, c_alpha) -% 16)) >> 8);
            if ((c_num0 | c_alpha0) == 0) {
                if (ignored_chars) |set| {
                    if (set.isSet(c)) {
                        continue;
                    }
                }
                return error.InvalidCharacter;
            }
            const c_val = (c_num0 & c_num) | (c_alpha0 & c_alpha);
            if (bin_pos >= bin.len) {
                return error.NoSpaceLeft;
            }
            if (!state) {
                c_acc = c_val << 4;
            } else {
                bin[bin_pos] = c_acc | c_val;
                bin_pos += 1;
            }
            state = !state;
        }
        if (state) {
            return error.InvalidPadding;
        }
        return bin[0..bin_pos];
    }
};

/// (best-effort) constant time base64 encoding and decoding.
pub const base64 = struct {
    /// The base64 variant to use.
    pub const Variant = packed struct {
        /// Use the URL-safe alphabet instead of the standard alphabet.
        urlsafe_alphabet: bool = false,
        /// Enable padding with '=' characters.
        padding: bool = true,

        /// The standard base64 variant.
        pub const standard: Variant = .{ .urlsafe_alphabet = false, .padding = true };
        /// The URL-safe base64 variant.
        pub const urlsafe: Variant = .{ .urlsafe_alphabet = true, .padding = true };
        /// The standard base64 variant without padding.
        pub const standard_nopad: Variant = .{ .urlsafe_alphabet = false, .padding = false };
        /// The URL-safe base64 variant without padding.
        pub const urlsafe_nopad: Variant = .{ .urlsafe_alphabet = true, .padding = false };
    };

    /// Returns the length of the encoded base64 string for a given length.
    pub fn encodedLen(bin_len: usize, variant: Variant) usize {
        if (variant.padding) {
            return (bin_len + 2) / 3 * 4;
        } else {
            const leftover = bin_len % 3;
            return bin_len / 3 * 4 + (leftover * 4 + 2) / 3;
        }
    }

    /// Returns the maximum possible decoded size for a given input length - The actual length may be less if the input includes padding.
    /// `InvalidPadding` is returned if the input length is not valid.
    pub fn decodedLen(b64_len: usize, variant: Variant) !usize {
        var result = b64_len / 4 * 3;
        const leftover = b64_len % 4;
        if (variant.padding) {
            if (leftover % 4 != 0) return error.InvalidPadding;
        } else {
            if (leftover % 4 == 1) return error.InvalidPadding;
            result += leftover * 3 / 4;
        }
        return result;
    }

    /// Encodes a binary buffer into a base64 string.
    /// The output buffer must be at least `encodedLen(bin.len)` bytes long.
    pub fn encode(encoded: []u8, bin: []const u8, comptime variant: Variant) error{NoSpaceLeft}![]const u8 {
        var acc_len: u4 = 0;
        var b64_pos: usize = 0;
        var acc: u16 = 0;
        const nibbles = bin.len / 3;
        const remainder = bin.len - 3 * nibbles;
        var b64_len = nibbles * 4;
        if (remainder != 0) {
            b64_len += if (variant.padding) 4 else 2 + (remainder >> 1);
        }
        if (encoded.len < b64_len) {
            return error.NoSpaceLeft;
        }
        const urlsafe = variant.urlsafe_alphabet;
        for (bin) |v| {
            acc = (acc << 8) + v;
            acc_len += 8;
            while (acc_len >= 6) {
                acc_len -= 6;
                encoded[b64_pos] = charFromByte(@as(u6, @truncate(acc >> acc_len)), urlsafe);
                b64_pos += 1;
            }
        }
        if (acc_len > 0) {
            encoded[b64_pos] = charFromByte(@as(u6, @truncate(acc << (6 - acc_len))), urlsafe);
            b64_pos += 1;
        }
        while (b64_pos < b64_len) {
            encoded[b64_pos] = '=';
            b64_pos += 1;
        }
        return encoded[0..b64_pos];
    }

    /// Decodes a base64 string into a binary buffer.
    /// The output buffer must be at least `decodedLenUpperBound(encoded.len)` bytes long.
    pub fn decode(bin: []u8, encoded: []const u8, comptime variant: Variant) error{ InvalidCharacter, InvalidPadding }![]const u8 {
        return decodeAny(bin, encoded, variant, null) catch |err| {
            switch (err) {
                error.InvalidCharacter => return error.InvalidCharacter,
                error.InvalidPadding => return error.InvalidPadding,
                else => unreachable,
            }
        };
    }

    //// A decoder that ignores certain characters.
    pub const DecoderWithIgnore = struct {
        /// The characters to ignore.
        ignored_chars: StaticBitSet(256) = undefined,

        /// Decodes a base64 string into a binary buffer.
        /// The output buffer must be at least `decodedLenUpperBound(encoded.len)` bytes long.
        pub fn decode(
            self: DecoderWithIgnore,
            bin: []u8,
            encoded: []const u8,
            comptime variant: Variant,
        ) error{ NoSpaceLeft, InvalidCharacter, InvalidPadding }![]const u8 {
            return decodeAny(bin, encoded, variant, self.ignored_chars);
        }

        /// Returns the decoded length of a base64 string, ignoring any characters in the ignore list.
        /// This operation does not run in constant time, but it aims to avoid leaking information about the underlying base64 string.
        pub fn decodedLenForSlice(decoder: DecoderWithIgnore, encoded: []const u8, variant: Variant) !usize {
            var b64_len = encoded.len;
            for (encoded) |c| {
                if (decoder.ignored_chars.isSet(c)) b64_len -= 1;
            }
            return base64.decodedLen(b64_len, variant);
        }

        /// Returns the maximum possible decoded size for a given input length after skipping ignored characters.
        pub fn decodedLenUpperBound(b64_len: usize) usize {
            return b64_len / 3 * 4;
        }
    };

    /// Creates a new decoder that ignores certain characters.
    pub fn decoderWithIgnore(ignore_chars: []const u8) error{InvalidCharacter}!DecoderWithIgnore {
        var ignored_chars = StaticBitSet(256).empty;
        for (ignore_chars) |c| {
            switch (c) {
                'A'...'Z', 'a'...'z', '0'...'9' => return error.InvalidCharacter,
                else => if (ignored_chars.isSet(c)) return error.InvalidCharacter,
            }
            ignored_chars.set(c);
        }
        return DecoderWithIgnore{ .ignored_chars = ignored_chars };
    }

    fn eq(x: u8, y: u8) u8 {
        return ~@as(u8, @truncate((0 -% (@as(u16, x) ^ @as(u16, y))) >> 8));
    }

    fn gt(x: u8, y: u8) u8 {
        return @truncate((@as(u16, y) -% @as(u16, x)) >> 8);
    }

    fn ge(x: u8, y: u8) u8 {
        return ~gt(y, x);
    }

    fn lt(x: u8, y: u8) u8 {
        return gt(y, x);
    }

    fn le(x: u8, y: u8) u8 {
        return ge(y, x);
    }

    fn charFromByte(x: u8, comptime urlsafe: bool) u8 {
        return (lt(x, 26) & (x +% 'A')) |
            (ge(x, 26) & lt(x, 52) & (x +% 'a' -% 26)) |
            (ge(x, 52) & lt(x, 62) & (x +% '0' -% 52)) |
            (eq(x, 62) & if (urlsafe) '-' else '+') | (eq(x, 63) & if (urlsafe) '_' else '/');
    }

    fn byteFromChar(c: u8, comptime urlsafe: bool) u8 {
        const x =
            (ge(c, 'A') & le(c, 'Z') & (c -% 'A')) |
            (ge(c, 'a') & le(c, 'z') & (c -% 'a' +% 26)) |
            (ge(c, '0') & le(c, '9') & (c -% '0' +% 52)) |
            (eq(c, if (urlsafe) '-' else '+') & 62) | (eq(c, if (urlsafe) '_' else '/') & 63);
        return x | (eq(x, 0) & ~eq(c, 'A'));
    }

    fn skipPadding(
        encoded: []const u8,
        padding_l
```
