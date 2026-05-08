```
const u8, num_chaining_values: usize, key: [8]u32, flags: Flags, out: []u8) usize {
    var parents_array: [max_simd_degree_or_2][*]const u8 = undefined;
    var parents_array_len: usize = 0;

    while (num_chaining_values - (2 * parents_array_len) >= 2) {
        parents_array[parents_array_len] = child_chaining_values[2 * parents_array_len * Blake3.digest_length ..].ptr;
        parents_array_len += 1;
    }

    hashMany(parents_array[0..parents_array_len], parents_array_len, 1, key, 0, false, flags.with(.{ .parent = true }), .{}, .{}, out);

    if (num_chaining_values > 2 * parents_array_len) {
        @memcpy(out[parents_array_len * Blake3.digest_length ..][0..Blake3.digest_length], child_chaining_values[2 * parents_array_len * Blake3.digest_length ..][0..Blake3.digest_length]);
        return parents_array_len + 1;
    } else {
        return parents_array_len;
    }
}

fn compressSubtreeWide(input: []const u8, key: [8]u32, chunk_counter: u64, flags: Flags, out: []u8) usize {
    if (input.len <= max_simd_degree * chunk_length) {
        return compressChunksParallel(input, key, chunk_counter, flags, out);
    }

    const left_input_len = leftSubtreeLen(input.len);
    const right_input = input[left_input_len..];
    const right_chunk_counter = chunk_counter + @as(u64, left_input_len / chunk_length);

    var cv_array: [2 * max_simd_degree_or_2 * Blake3.digest_length]u8 = undefined;
    var degree: usize = max_simd_degree;
    if (left_input_len > chunk_length and degree == 1) {
        degree = 2;
    }
    const right_cvs = cv_array[degree * Blake3.digest_length ..];

    const left_n = compressSubtreeWide(input[0..left_input_len], key, chunk_counter, flags, cv_array[0..]);
    const right_n = compressSubtreeWide(right_input, key, right_chunk_counter, flags, right_cvs);

    if (left_n == 1) {
        @memcpy(out[0 .. 2 * Blake3.digest_length], cv_array[0 .. 2 * Blake3.digest_length]);
        return 2;
    }

    const num_chaining_values = left_n + right_n;
    return compressParentsParallel(&cv_array, num_chaining_values, key, flags, out);
}

fn compressSubtreeToParentNode(input: []const u8, key: [8]u32, chunk_counter: u64, flags: Flags, out: *[2 * Blake3.digest_length]u8) void {
    var cv_array: [max_simd_degree_or_2 * Blake3.digest_length]u8 = undefined;
    var num_cvs = compressSubtreeWide(input, key, chunk_counter, flags, &cv_array);

    if (max_simd_degree_or_2 > 2) {
        var out_array: [max_simd_degree_or_2 * Blake3.digest_length / 2]u8 = undefined;
        while (num_cvs > 2) {
            num_cvs = compressParentsParallel(&cv_array, num_cvs, key, flags, &out_array);
            @memcpy(cv_array[0 .. num_cvs * Blake3.digest_length], out_array[0 .. num_cvs * Blake3.digest_length]);
        }
    }

    @memcpy(out, cv_array[0 .. 2 * Blake3.digest_length]);
}

fn leftSubtreeLen(input_len: usize) usize {
    const full_chunks = (input_len - 1) / chunk_length;
    return @intCast(roundDownToPowerOf2(full_chunks) * chunk_length);
}

const ChunkBatch = struct {
    input: []const u8,
    start_chunk: usize,
    end_chunk: usize,
    cvs: [][8]u32,
    key: [8]u32,
    flags: Flags,

    fn process(ctx: ChunkBatch) void {
        var cv_buffer: [max_simd_degree * Blake3.digest_length]u8 = undefined;
        var chunk_idx = ctx.start_chunk;

        while (chunk_idx < ctx.end_chunk) {
            const remaining = ctx.end_chunk - chunk_idx;
            const batch_size: usize = @min(remaining, max_simd_degree);
            const offset = chunk_idx * chunk_length;
            const batch_len = batch_size * chunk_length;

            const num_cvs = compressChunksParallel(
                ctx.input[offset..][0..batch_len],
                ctx.key,
                chunk_idx,
                ctx.flags,
                &cv_buffer,
            );

            for (0..num_cvs) |i| {
                const cv_bytes = cv_buffer[i * Blake3.digest_length ..][0..Blake3.digest_length];
                ctx.cvs[chunk_idx + i] = loadCvWords(cv_bytes.*);
            }

            chunk_idx += batch_size;
        }
    }
};

const ParentBatchContext = struct {
    input_cvs: [][8]u32,
    output_cvs: [][8]u32,
    start_idx: usize,
    end_idx: usize,
    key: [8]u32,
    flags: Flags,
};

fn processParentBatch(ctx: ParentBatchContext) void {
    for (ctx.start_idx..ctx.end_idx) |i| {
        const output = parentOutputFromCvs(ctx.input_cvs[i * 2], ctx.input_cvs[i * 2 + 1], ctx.key, ctx.flags);
        ctx.output_cvs[i] = output.chainingValue();
    }
}

fn processParentBatchSIMD(ctx: ParentBatchContext) void {
    const num_parents = ctx.end_idx - ctx.start_idx;
    if (num_parents == 0) return;

    // Convert input CVs to bytes for SIMD processing
    var input_bytes: [max_simd_degree * 2 * Blake3.digest_length]u8 = undefined;
    var output_bytes: [max_simd_degree * Blake3.digest_length]u8 = undefined;
    var parents_array: [max_simd_degree][*]const u8 = undefined;

    var processed: usize = 0;
    while (processed < num_parents) {
        const batch_size: usize = @min(num_parents - processed, max_simd_degree);

        // Convert CV pairs to byte blocks for this batch
        for (0..batch_size) |i| {
            const pair_idx = ctx.start_idx + processed + i;
            const left_cv = ctx.input_cvs[pair_idx * 2];
            const right_cv = ctx.input_cvs[pair_idx * 2 + 1];

            // Write left CV || right CV to form 64-byte parent block
            for (0..8) |j| {
                store32(input_bytes[i * 64 + j * 4 ..][0..4], left_cv[j]);
                store32(input_bytes[i * 64 + 32 + j * 4 ..][0..4], right_cv[j]);
            }
            parents_array[i] = input_bytes[i * 64 ..].ptr;
        }

        hashMany(parents_array[0..batch_size], batch_size, 1, ctx.key, 0, false, ctx.flags.with(.{ .parent = true }), .{}, .{}, output_bytes[0 .. batch_size * Blake3.digest_length]);

        for (0..batch_size) |i| {
            const output_idx = ctx.start_idx + processed + i;
            ctx.output_cvs[output_idx] = loadCvWords(output_bytes[i * Blake3.digest_length ..][0..Blake3.digest_length].*);
        }

        processed += batch_size;
    }
}

fn buildMerkleTreeLayerParallel(
    input_cvs: [][8]u32,
    output_cvs: [][8]u32,
    key: [8]u32,
    flags: Flags,
    io: Io,
) Io.Cancelable!void {
    const num_parents = input_cvs.len / 2;

    // Process sequentially with SIMD for smaller tree layers to avoid thread overhead
    // Tree layers shrink quickly, so only parallelize the first few large layers
    if (num_parents <= 1024) {
        processParentBatchSIMD(ParentBatchContext{
            .input_cvs = input_cvs,
            .output_cvs = output_cvs,
            .start_idx = 0,
            .end_idx = num_parents,
            .key = key,
            .flags = flags,
        });
        return;
    }

    const num_workers = Thread.getCpuCount() catch 1;
    const parents_per_worker = (num_parents + num_workers - 1) / num_workers;
    var group: Io.Group = .init;
    defer group.cancel(io);

    for (0..num_workers) |worker_id| {
        const start_idx = worker_id * parents_per_worker;
        if (start_idx >= num_parents) break;

        group.async(io, processParentBatchSIMD, .{ParentBatchContext{
            .input_cvs = input_cvs,
            .output_cvs = output_cvs,
            .start_idx = start_idx,
            .end_idx = @min(start_idx + parents_per_worker, num_parents),
            .key = key,
            .flags = flags,
        }});
    }
    try group.await(io);
}

fn parentOutput(parent_block: []const u8, key: [8]u32, flags: Flags) Output {
    var block: [Blake3.block_length]u8 = undefined;
    @memcpy(&block, parent_block[0..Blake3.block_length]);
    return Output{
        .input_cv = key,
        .block = block,
        .block_len = Blake3.block_length,
        .counter = 0,
        .flags = flags.with(.{ .parent = true }),
    };
}

fn parentOutputFromCvs(left_cv: [8]u32, right_cv: [8]u32, key: [8]u32, flags: Flags) Output {
    var block: [Blake3.block_length]u8 align(16) = undefined;
    for (0..8) |i| {
        store32(block[i * 4 ..][0..4], left_cv[i]);
        store32(block[(i + 8) * 4 ..][0..4], right_cv[i]);
    }
    return Output{
        .input_cv = key,
        .block = block,
        .block_len = Blake3.block_length,
        .counter = 0,
        .flags = flags.with(.{ .parent = true }),
    };
}

const ChunkState = struct {
    cv: [8]u32 align(16),
    chunk_counter: u64,
    buf: [Blake3.block_length]u8 align(16),
    buf_len: u8,
    blocks_compressed: u8,
    flags: Flags,

    fn init(key: [8]u32, flags: Flags) ChunkState {
        return ChunkState{
            .cv = key,
            .chunk_counter = 0,
            .buf = @splat(0),
            .buf_len = 0,
            .blocks_compressed = 0,
            .flags = flags,
        };
    }

    fn reset(self: *ChunkState, key: [8]u32, chunk_counter: u64) void {
        self.cv = key;
        self.chunk_counter = chunk_counter;
        self.blocks_compressed = 0;
        self.buf = @splat(0);
        self.buf_len = 0;
    }

    fn len(self: *const ChunkState) usize {
        return (Blake3.block_length * @as(usize, self.blocks_compressed)) + @as(usize, self.buf_len);
    }

    fn fillBuf(self: *ChunkState, input: []const u8) usize {
        const take = @min(Blake3.block_length - @as(usize, self.buf_len), input.len);
        @memcpy(self.buf[self.buf_len..][0..take], input[0..take]);
        self.buf_len += @intCast(take);
        return take;
    }

    fn maybeStartFlag(self: *const ChunkState) Flags {
        return if (self.blocks_compressed == 0) .{ .chunk_start = true } else .{};
    }

    fn update(self: *ChunkState, input: []const u8) void {
        var inp = input;

        while (inp.len > 0) {
            if (self.buf_len == Blake3.block_length) {
                compressInPlace(&self.cv, &self.buf, Blake3.block_length, self.chunk_counter, self.flags.with(self.maybeStartFlag()));
                self.blocks_compressed += 1;
                self.buf = @splat(0);
                self.buf_len = 0;
            }

            const take = self.fillBuf(inp);
            inp = inp[take..];
        }
    }

    fn output(self: *const ChunkState) Output {
        const block_flags = self.flags.with(self.maybeStartFlag()).with(.{ .chunk_end = true });
        return Output{
            .input_cv = self.cv,
            .block = self.buf,
            .block_len = self.buf_len,
            .counter = self.chunk_counter,
            .flags = block_flags,
        };
    }
};

const Output = struct {
    input_cv: [8]u32 align(16),
    block: [Blake3.block_length]u8 align(16),
    block_len: u8,
    counter: u64,
    flags: Flags,

    fn chainingValue(self: *const Output) [8]u32 {
        var cv_words = self.input_cv;
        compressInPlace(&cv_words, &self.block, self.block_len, self.counter, self.flags);
        return cv_words;
    }

    fn rootBytes(self: *const Output, seek: u64, out: []u8) void {
        if (out.len == 0) return;

        var output_block_counter = seek / 64;
        const offset_within_block = @as(usize, @intCast(seek % 64));
        var out_remaining = out;

        if (offset_within_block > 0) {
            var wide_buf: [64]u8 = undefined;
            compressXof(&self.input_cv, &self.block, self.block_len, output_block_counter, self.flags.with(.{ .root = true }), &wide_buf);
            const available_bytes = 64 - offset_within_block;
            const bytes = @min(out_remaining.len, available_bytes);
            @memcpy(out_remaining[0..bytes], wide_buf[offset_within_block..][0..bytes]);
            out_remaining = out_remaining[bytes..];
            output_block_counter += 1;
        }

        while (out_remaining.len >= 64) {
            compressXof(&self.input_cv, &self.block, self.block_len, output_block_counter, self.flags.with(.{ .root = true }), out_remaining[0..64]);
            out_remaining = out_remaining[64..];
            output_block_counter += 1;
        }

        if (out_remaining.len > 0) {
            var wide_buf: [64]u8 = undefined;
            compressXof(&self.input_cv, &self.block, self.block_len, output_block_counter, self.flags.with(.{ .root = true }), &wide_buf);
            @memcpy(out_remaining, wide_buf[0..out_remaining.len]);
        }
    }
};

/// BLAKE3 is a cryptographic hash function that produces a 256-bit digest by default but also supports extendable output.
pub const Blake3 = struct {
    pub const block_length = 64;
    pub const digest_length = 32;
    pub const key_length = 32;

    pub const Options = struct { key: ?[key_length]u8 = null };
    pub const KdfOptions = struct {};

    key: [8]u32,
    chunk: ChunkState,
    cv_stack_len: u8,
    cv_stack: [max_depth + 1][8]u32,

    /// Construct a new `Blake3` for the hash function, with an optional key
    pub fn init(options: Options) Blake3 {
        if (options.key) |key| {
            const key_words = loadKeyWords(key);
            return init_internal(key_words, .{ .keyed_hash = true });
        } else {
            return init_internal(iv, .{});
        }
    }

    /// Construct a new `Blake3` for the key derivation function. The context
    /// string should be hardcoded, globally unique, and application-specific.
    pub fn initKdf(context: []const u8, options: KdfOptions) Blake3 {
        _ = options;
        var context_hasher = init_internal(iv, .{ .derive_key_context = true });
        context_hasher.update(context);
        var context_key: [key_length]u8 = undefined;
        context_hasher.final(&context_key);
        const context_key_words = loadKeyWords(context_key);
        return init_internal(context_key_words, .{ .derive_key_material = true });
    }

    pub fn hash(b: []const u8, out: []u8, options: Options) void {
        var d = Blake3.init(options);
        d.update(b);
        d.final(out);
    }

    pub fn hashParallel(b: []const u8, out: []u8, options: Options, allocator: Allocator, io: Io) error{ OutOfMemory, Canceled }!void {
        if (b.len < parallel_threshold) {
            return hash(b, out, options);
        }

        const key_words = if (options.key) |key| loadKeyWords(key) else iv;
        const flags: Flags = if (options.key != null) .{ .keyed_hash = true } else .{};

        const num_full_chunks = b.len / chunk_length;
        const thread_count = Thread.getCpuCount() catch 1;
        if (thread_count <= 1 or num_full_chunks == 0) {
            return hash(b, out, options);
        }

        const cvs = try allocator.alloc([8]u32, num_full_chunks);
        defer allocator.free(cvs);

        // Process chunks in parallel
        const num_workers = thread_count;
        const chunks_per_worker = (num_full_chunks + num_workers - 1) / num_workers;
        var group: Io.Group = .init;
        defer group.cancel(io);

        for (0..num_workers) |worker_id| {
            const start_chunk = worker_id * chunks_per_worker;
            if (start_chunk >= num_full_chunks) break;

            group.async(io, ChunkBatch.process, .{ChunkBatch{
                .input = b,
                .start_chunk = start_chunk,
                .end_chunk = @min(start_chunk + chunks_per_worker, num_full_chunks),
                .cvs = cvs,
                .key = key_words,
                .flags = flags,
            }});
        }
        try group.await(io);

        // Build Merkle tree in parallel layers using ping-pong buffers
        const max_intermediate_size = (num_full_chunks + 1) / 2;
        const buffer0 = try allocator.alloc([8]u32, max_intermediate_size);
        defer allocator.free(buffer0);
        const buffer1 = try allocator.alloc([8]u32, max_intermediate_size);
        defer allocator.free(buffer1);

        var current_level = cvs;
        var next_level_buf = buffer0;
        var toggle = false;

        while (current_level.len > 8) {
            const num_parents = current_level.len / 2;
            const has_odd = current_level.len % 2 == 1;
            const next_level_size = num_parents + @intFromBool(has_odd);

            try buildMerkleTreeLayerParallel(
                current_level[0 .. num_parents * 2],
                next_level_buf[0..num_parents],
                key_words,
                flags,
                io,
            );

            if (has_odd) {
                next_level_buf[num_parents] = current_level[current_level.len - 1];
            }

            current_level = next_level_buf[0..next_level_size];
            next_level_buf = if (toggle) buffer0 else buffer1;
            toggle = !toggle;
        }

        // Finalize remaining small tree sequentially
        var hasher = init_internal(key_words, flags);
        for (current_level, 0..) |cv, i| hasher.pushCv(cv, i);

        hasher.chunk.chunk_counter = num_full_chunks;
        const remaining_bytes = b.len % chunk_length;
        if (remaining_bytes > 0) {
            hasher.chunk.update(b[num_full_chunks * chunk_length ..]);
            hasher.mergeCvStack(hasher.chunk.chunk_counter);
        }

        hasher.final(out);
    }

    fn init_internal(key: [8]u32, flags: Flags) Blake3 {
        return Blake3{
            .key = key,
            .chunk = ChunkState.init(key, flags),
            .cv_stack_len = 0,
            .cv_stack = undefined,
        };
    }

    fn mergeCvStack(self: *Blake3, total_len: u64) void {
        const post_merge_stack_len = @as(u8, @intCast(@popCount(total_len)));
        while (self.cv_stack_len > post_merge_stack_len) {
            const left_cv = self.cv_stack[self.cv_stack_len - 2];
            const right_cv = self.cv_stack[self.cv_stack_len - 1];
            const output = parentOutputFromCvs(left_cv, right_cv, self.key, self.chunk.flags);
            const cv = output.chainingValue();
            self.cv_stack[self.cv_stack_len - 2] = cv;
            self.cv_stack_len -= 1;
        }
    }

    fn pushCv(self: *Blake3, new_cv: [8]u32, chunk_counter: u64) void {
        self.mergeCvStack(chunk_counter);
        self.cv_stack[self.cv_stack_len] = new_cv;
        self.cv_stack_len += 1;
    }

    /// Add input to the hash state. This can be called any number of times.
    pub fn update(self: *Blake3, input: []const u8) void {
        if (input.len == 0) return;

        var inp = input;

        if (self.chunk.len() > 0) {
            const take = @min(chunk_length - self.chunk.len(), inp.len);
            self.chunk.update(inp[0..take]);
            inp = inp[take..];
            if (inp.len > 0) {
                const output = self.chunk.output();
                const chunk_cv = output.chainingValue();
                self.pushCv(chunk_cv, self.chunk.chunk_counter);
                self.chunk.reset(self.key, self.chunk.chunk_counter + 1);
            } else {
                return;
            }
        }

        while (inp.len > chunk_length) {
            var subtree_len = roundDownToPowerOf2(inp.len);
            const count_so_far = self.chunk.chunk_counter * chunk_length;

            while ((subtree_len - 1) & count_so_far != 0) {
                subtree_len /= 2;
            }

            const subtree_chunks = subtree_len / chunk_length;
            if (subtree_len <= chunk_length) {
                var chunk_state = ChunkState.init(self.key, self.chunk.flags);
                chunk_state.chunk_counter = self.chunk.chunk_counter;
                chunk_state.update(inp[0..@intCast(subtree_len)]);
                const output = chunk_state.output();
                const cv = output.chainingValue();
                self.pushCv(cv, chunk_state.chunk_counter);
            } else {
                var cv_pair: [2 * digest_length]u8 = undefined;
                compressSubtreeToParentNode(inp[0..@intCast(subtree_len)], self.key, self.chunk.chunk_counter, self.chunk.flags, &cv_pair);
                const left_cv = loadCvWords(cv_pair[0..digest_length].*);
                const right_cv = loadCvWords(cv_pair[digest_length..][0..digest_length].*);
                self.pushCv(left_cv, self.chunk.chunk_counter);
                self.pushCv(right_cv, self.chunk.chunk_counter + (subtree_chunks / 2));
            }
            self.chunk.chunk_counter += subtree_chunks;
            inp = inp[@intCast(subtree_len)..];
        }

        if (inp.len > 0) {
            self.chunk.update(inp);
            self.mergeCvStack(self.chunk.chunk_counter);
        }
    }

    /// Finalize the hash and write any number of output bytes.
    pub fn final(self: *const Blake3, out: []u8) void {
        self.finalizeSeek(0, out);
    }

    /// Finalize the hash and write any number of output bytes, starting at a given seek position.
    /// This is an XOF (extendable-output function) extension.
    pub fn finalizeSeek(self: *const Blake3, seek: u64, out: []u8) void {
        if (out.len == 0) return;

        if (self.cv_stack_len == 0) {
            const output = self.chunk.output();
            output.rootBytes(seek, out);
            return;
        }

        var output: Output = undefined;
        var cvs_remaining: usize = undefined;

        if (self.chunk.len() > 0) {
            cvs_remaining = self.cv_stack_len;
            output = self.chunk.output();
        } else {
            cvs_remaining = self.cv_stack_len - 2;
            const left_cv = self.cv_stack[cvs_remaining];
            const right_cv = self.cv_stack[cvs_remaining + 1];
            output = parentOutputFromCvs(left_cv, right_cv, self.key, self.chunk.flags);
        }

        while (cvs_remaining > 0) {
            cvs_remaining -= 1;
            const left_cv = self.cv_stack[cvs_remaining];
            const right_cv = output.chainingValue();
            output = parentOutputFromCvs(left_cv, right_cv, self.key, self.chunk.flags);
        }

        output.rootBytes(seek, out);
    }

    pub fn reset(self: *Blake3) void {
        self.chunk.reset(self.key, 0);
        self.cv_stack_len = 0;
    }
};

// Use named type declarations to workaround crash with anonymous structs (issue #4373).
const ReferenceTest = struct {
    key: *const [Blake3.key_length]u8,
    context_string: []const u8,
    cases: []const ReferenceTestCase,
};

const ReferenceTestCase = struct {
    input_len: usize,
    hash: *const [262]u8,
    keyed_hash: *const [262]u8,
    derive_key: *const [262]u8,
};

// Each test is an input length and three outputs, one for each of the `hash`, `keyed_hash`, and
// `derive_key` modes. The input in each case is filled with a 251-byte-long repeating pattern:
// 0, 1, 2, ..., 249, 250, 0, 1, ... The key used with `keyed_hash` is the 32-byte ASCII string
// given in the `key` field below. For `derive_key`, the test input is used as the input key, and
// the context string is 'BLAKE3 2019-12-27 16:29:52 test vectors context'. (As good practice for
// following the security requirements of `derive_key`, test runners should make that context
// string a hardcoded constant, and we do not provided it in machine-readable form.) Outputs are
// encoded as hexadecimal. Each case is an extended output, and implementations should also check
// that the first 32 bytes match their default-length output.
//
// Source: https://github.com/BLAKE3-team/BLAKE3/blob/92d421dea1a89e2f079f4dbd93b0dab41234b279/test_vectors/test_vectors.json
const reference_test = ReferenceTest{
    .key = "whats the Elvish word for friend",
    .context_string = "BLAKE3 2019-12-27 16:29:52 test vectors context",
    .cases = &[_]ReferenceTestCase{
        .{
            .input_len = 0,
            .hash = "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262e00f03e7b69af26b7faaf09fcd333050338ddfe085b8cc869ca98b206c08243a26f5487789e8f660afe6c99ef9e0c52b92e7393024a80459cf91f476f9ffdbda7001c22e159b402631f277ca96f2defdf1078282314e763699a31c5363165421cce14d",
            .keyed_hash = "92b2b75604ed3c761f9d6f62392c8a9227ad0ea3f09573e783f1498a4ed60d26b18171a2f22a4b94822c701f107153dba24918c4bae4d2945c20ece13387627d3b73cbf97b797d5e59948c7ef788f54372df45e45e4293c7dc18c1d41144a9758be58960856be1eabbe22c2653190de560ca3b2ac4aa692a9210694254c371e851bc8f",
            .derive_key = "2cc39783c223154fea8dfb7c1b1660f2ac2dcbd1c1de8277b0b0dd39b7e50d7d905630c8be290dfcf3e6842f13bddd573c098c3f17361f1f206b8cad9d088aa4a3f746752c6b0ce6a83b0da81d59649257cdf8eb3e9f7d4998e41021fac119deefb896224ac99f860011f73609e6e0e4540f93b273e56547dfd3aa1a035ba6689d89a0",
        },
        .{
            .input_len = 1,
            .hash = "2d3adedff11b61f14c886e35afa036736dcd87a74d27b5c1510225d0f592e213c3a6cb8bf623e20cdb535f8d1a5ffb86342d9c0b64aca3bce1d31f60adfa137b358ad4d79f97b47c3d5e79f179df87a3b9776ef8325f8329886ba42f07fb138bb502f4081cbcec3195c5871e6c23e2cc97d3c69a613eba131e5f1351f3f1da786545e5",
            .keyed_hash = "6d7878dfff2f485635d39013278ae14f1454b8c0a3a2d34bc1ab38228a80c95b6568c0490609413006fbd428eb3fd14e7756d90f73a4725fad147f7bf70fd61c4e0cf7074885e92b0e3f125978b4154986d4fb202a3f331a3fb6cf349a3a70e49990f98fe4289761c8602c4e6ab1138d31d3b62218078b2f3ba9a88e1d08d0dd4cea11",
            .derive_key = "b3e2e340a117a499c6cf2398a19ee0d29cca2bb7404c73063382693bf66cb06c5827b91bf889b6b97c5477f535361caefca0b5d8c4746441c57617111933158950670f9aa8a05d791daae10ac683cbef8faf897c84e6114a59d2173c3f417023a35d6983f2c7dfa57e7fc559ad751dbfb9ffab39c2ef8c4aafebc9ae973a64f0c76551",
        },
        .{
            .input_len = 1023,
            .hash = "10108970eeda3eb932baac1428c7a2163b0e924c9a9e25b35bba72b28f70bd11a182d27a591b05592b15607500e1e8dd56bc6c7fc063715b7a1d737df5bad3339c56778957d870eb9717b57ea3d9fb68d1b55127bba6a906a4a24bbd5acb2d123a37b28f9e9a81bbaae360d58f85e5fc9d75f7c370a0cc09b6522d9c8d822f2f28f485",
            .keyed_hash = "c951ecdf03288d0fcc96ee3413563d8a6d3589547f2c2fb36d9786470f1b9d6e890316d2e6d8b8c25b0a5b2180f94fb1a158ef508c3cde45e2966bd796a696d3e13efd86259d756387d9becf5c8bf1ce2192b87025152907b6d8cc33d17826d8b7b9bc97e38c3c85108ef09f013e01c229c20a83d9e8efac5b37470da28575fd755a10",
            .derive_key = "74a16c1c3d44368a86e1ca6df64be6a2f64cce8f09220787450722d85725dea59c413264404661e9e4d955409dfe4ad3aa487871bcd454ed12abfe2c2b1eb7757588cf6cb18d2eccad49e018c0d0fec323bec82bf1644c6325717d13ea712e6840d3e6e730d35553f59eff5377a9c350bcc1556694b924b858f329c44ee64b884ef00d",
        },
        .{
            .input_len = 1024,
            .hash = "42214739f095a406f3fc83deb889744ac00df831c10daa55189b5d121c855af71cf8107265ecdaf8505b95d8fcec83a98a6a96ea5109d2c179c47a387ffbb404756f6eeae7883b446b70ebb144527c2075ab8ab204c0086bb22b7c93d465efc57f8d917f0b385c6df265e77003b85102967486ed57db5c5ca170ba441427ed9afa684e",
            .keyed_hash = "75c46f6f3d9eb4f55ecaaee480db732e6c2105546f1e675003687c31719c7ba4a78bc838c72852d4f49c864acb7adafe2478e824afe51c8919d06168414c265f298a8094b1ad813a9b8614acabac321f24ce61c5a5346eb519520d38ecc43e89b5000236df0597243e4d2493fd626730e2ba17ac4d8824d09d1a4a8f57b8227778e2de",
            .derive_key = "7356cd7720d5b66b6d0697eb3177d9f8d73a4a5c5e968896eb6a6896843027066c23b601d3ddfb391e90d5c8eccdef4ae2a264bce9e612ba15e2bc9d654af1481b2e75dbabe615974f1070bba84d56853265a34330b4766f8e75edd1f4a1650476c10802f22b64bd3919d246ba20a17558bc51c199efdec67e80a227251808d8ce5bad",
        },
        .{
            .input_len = 1025,
            .hash = "d00278ae47eb27b34faecf67b4fe263f82d5412916c1ffd97c8cb7fb814b8444f4c4a22b4b399155358a994e52bf255de60035742ec71bd08ac275a1b51cc6bfe332b0ef84b409108cda080e6269ed4b3e2c3f7d722aa4cdc98d16deb554e5627be8f955c98e1d5f9565a9194cad0c4285f93700062d9595adb992ae68ff12800ab67a",
            .keyed_hash = "357dc55de0c7e382c900fd6e320acc04146be01db6a8ce7210b7189bd664ea69362396b77fdc0d2634a552970843722066c3c15902ae5097e00ff53f1e116f1cd5352720113a837ab2452cafbde4d54085d9cf5d21ca613071551b25d52e69d6c81123872b6f19cd3bc1333edf0c52b94de23ba772cf82636cff4542540a7738d5b930",
            .derive_key = "effaa245f065fbf82ac186839a249707c3bddf6d3fdda22d1b95a3c970379bcb5d31013a167509e9066273ab6e2123bc835b408b067d88f96addb550d96b6852dad38e320b9d940f86db74d398c770f462118b35d2724efa13da97194491d96dd37c3c09cbef665953f2ee85ec83d88b88d11547a6f911c8217cca46defa2751e7f3ad",
        },
        .{
            .input_len = 2048,
            .hash = "e776b6028c7cd22a4d0ba182a8bf62205d2ef576467e838ed6f2529b85fba24a9a60bf80001410ec9eea6698cd537939fad4749edd484cb541aced55cd9bf54764d063f23f6f1e32e12958ba5cfeb1bf618ad094266d4fc3c968c2088f677454c288c67ba0dba337b9d91c7e1ba586dc9a5bc2d5e90c14f53a8863ac75655461cea8f9",
            .keyed_hash = "879cf1fa2ea0e79126cb1063617a05b6ad9d0b696d0d757cf053439f60a99dd10173b961cd574288194b23ece278c330fbb8585485e74967f31352a8183aa782b2b22f26cdcadb61eed1a5bc144b8198fbb0c13abbf8e3192c145d0a5c21633b0ef86054f42809df823389ee40811a5910dcbd1018af31c3b43aa55201ed4edaac74fe",
            .derive_key = "7b2945cb4fef70885cc5d78a87bf6f6207dd901ff239201351ffac04e1088a23e2c11a1ebffcea4d80447867b61badb1383d842d4e79645d48dd82ccba290769caa7af8eaa1bd78a2a5e6e94fbdab78d9c7b74e894879f6a515257ccf6f95056f4e25390f24f6b35ffbb74b766202569b1d797f2d4bd9d17524c720107f985f4ddc583",
        },
        .{
            .input_len = 2049,
            .hash = "5f4d72f40d7a5f82b15ca2b2e44b1de3c2ef86c426c95c1af0b687952256303096de31d71d74103403822a2e0bc1eb193e7aecc9643a76b7bbc0c9f9c52e8783aae98764ca468962b5c2ec92f0c74eb5448d519713e09413719431c802f948dd5d90425a4ecdadece9eb178d80f26efccae630734dff63340285adec2aed3b51073ad3",
            .keyed_hash = "9f29700902f7c86e514ddc4df1e3049f258b2472b6dd5267f61bf13983b78dd5f9a88abfefdfa1e00b418971f2b39c64ca621e8eb37fceac57fd0c8fc8e117d43b81447be22d5d8186f8f5919ba6bcc6846bd7d50726c06d245672c2ad4f61702c646499ee1173daa061ffe15bf45a631e2946d616a4c345822f1151284712f76b2b0e",
            .derive_key = "2ea477c5515cc3dd606512ee72bb3e0e758cfae7232826f35fb98ca1bcbdf27316d8e9e79081a80b046b60f6a263616f33ca464bd78d79fa18200d06c7fc9bffd808cc4755277a7d5e09da0f29ed150f6537ea9bed946227ff184cc66a72a5f8c1e4bd8b04e81cf40fe6dc4427ad5678311a61f4ffc39d195589bdbc670f63ae70f4b6",
        },
        .{
            .input_len = 3072,
            .hash = "b98cb0ff3623be03326b373de6b9095218513e64f1ee2edd2525c7ad1e5cffd29a3f6b0b978d6608335c09dc94ccf682f9951cdfc501bfe47b9c9189a6fc7b404d120258506341a6d802857322fbd20d3e5dae05b95c88793fa83db1cb08e7d8008d1599b6209d78336e24839724c191b2a52a80448306e0daa84a3fdb566661a37e11",
            .keyed_hash = "044a0e7b172a312dc02a4c9a818c036ffa2776368d7f528268d2e6b5df19177022f302d0529e4174cc507c463671217975e81dab02b8fdeb0d7ccc7568dd22574c783a76be215441b32e91b9a904be8ea81f7a0afd14bad8ee7c8efc305ace5d3dd61b996febe8da4f56ca0919359a7533216e2999fc87ff7d8f176fbecb3d6f34278b",
            .derive_key = "050df97f8c2ead654d9bb3ab8c9178edcd902a32f8495949feadcc1e0480c46b3604131bbd6e3ba573b6dd682fa0a63e5b165d39fc43a625d00207607a2bfeb65ff1d29292152e26b298868e3b87be95d6458f6f2ce6118437b632415abe6ad522874bcd79e4030a5e7bad2efa90a7a7c67e93f0a18fb28369d0a9329ab5c24134ccb0",
        },
        .{
            .input_len = 3073,
            .hash = "7124b49501012f81cc7f11ca069ec9226cecb8a2c850cfe644e327d22d3e1cd39a27ae3b79d68d89da9bf25bc27139ae65a324918a5f9b7828181e52cf373c84f35b639b7fccbb985b6f2fa56aea0c18f531203497b8bbd3a07ceb5926f1cab74d14bd66486d9a91eba99059a98bd1cd25876b2af5a76c3e9eed554ed72ea952b603bf",
            .keyed_hash = "68dede9bef00ba89e43f31a6825f4cf433389fedae75c04ee9f0cf16a427c95a96d6da3fe985054d3478865be9a092250839a697bbda74e279e8a9e69f0025e4cfddd6cfb434b1cd9543aaf97c635d1b451a4386041e4bb100f5e45407cbbc24fa53ea2de3536ccb329e4eb9466ec37093a42cf62b82903c696a93a50b702c80f3c3c5",
            .derive_key = "72613c9ec9ff7e40f8f5c173784c532ad852e827dba2bf85b2ab4b76f7079081576288e552647a9d86481c2cae75c2dd4e7c5195fb9ada1ef50e9c5098c249d743929191441301c69e1f48505a4305ec1778450ee48b8e69dc23a25960fe33070ea549119599760a8a2d28aeca06b8c5e9ba58bc19e11fe57b6ee98aa44b2a8e6b14a5",
        },
        .{
            .input_len = 4096,
            .hash = "015094013f57a5277b59d8475c0501042c0b642e531b0a1c8f58d2163229e9690289e9409ddb1b99768eafe1623da896faf7e1114bebeadc1be30829b6f8af707d85c298f4f0ff4d9438aef948335612ae921e76d411c3a9111df62d27eaf871959ae0062b5492a0feb98ef3ed4af277f5395172dbe5c311918ea0074ce0036454f620",
            .keyed_hash = "befc660aea2f1718884cd8deb9902811d332f4fc4a38cf7c7300d597a081bfc0bbb64a36edb564e01e4b4aaf3b060092a6b838bea44afebd2deb8298fa562b7b597c757b9df4c911c3ca462e2ac89e9a787357aaf74c3b56d5c07bc93ce899568a3eb17d9250c20f6c5f6c1e792ec9a2dcb715398d5a6ec6d5c54f586a00403a1af1de",
            .derive_key = "1e0d7f3db8c414c97c6307cbda6cd27ac3b030949da8e23be1a1a924ad2f25b9d78038f7b198596c6cc4a9ccf93223c08722d684f240ff6569075ed81591fd93f9fff1110b3a75bc67e426012e5588959cc5a4c192173a03c00731cf84544f65a2fb9378989f72e9694a6a394a8a30997c2e67f95a504e631cd2c5f55246024761b245",
        },
        .{
            .input_len = 4097,
            .hash = "9b4052b38f1c5fc8b1f9ff7ac7b27cd242487b3d890d15c96a1c25b8aa0fb99505f91b0b5600a11251652eacfa9497b31cd3c409ce2e45cfe6c0a016967316c426bd26f619eab5d70af9a418b845c608840390f361630bd497b1ab44019316357c61dbe091ce72fc16dc340ac3d6e009e050b3adac4b5b2c92e722cffdc46501531956",
            .keyed_hash = "00df940cd36bb9fa7cbbc3556744e0dbc8191401afe70520ba292ee3ca80abbc606db4976cfdd266ae0abf667d9481831ff12e0caa268e7d3e57260c0824115a54ce595ccc897786d9dcbf495599cfd90157186a46ec800a6763f1c59e36197e9939e900809f7077c102f888caaf864b253bc41eea812656d46742e4ea42769f89b83f",
            .derive_key = "aca51029626b55fda7117b42a7c211f8c6e9ba4fe5b7a8ca922f34299500ead8a897f66a400fed9198fd61dd2d58d382458e64e100128075fc54b860934e8de2e84170734b06e1d212a117100820dbc48292d148afa50567b8b84b1ec336ae10d40c8c975a624996e12de31abbe135d9d159375739c333798a80c64ae895e51e22f3ad",
        },
        .{
            .input_len = 5120,
            .hash = "9cadc15fed8b5d854562b26a9536d9707cadeda9b143978f319ab34230535833acc61c8fdc114a2010ce8038c853e121e1544985133fccdd0a2d507e8e615e611e9a0ba4f47915f49e53d721816a9198e8b30f12d20ec3689989175f1bf7a300eee0d9321fad8da232ece6efb8e9fd81b42ad161f6b9550a069e66b11b40487a5f5059",
            .keyed_hash = "2c493e48e9b9bf31e0553a22b23503c0a3388f035cece68eb438d22fa1943e209b4dc9209cd80ce7c1f7c9a744658e7e288465717ae6e56d5463d4f80cdb2ef56495f6a4f5487f69749af0c34c2cdfa857f3056bf8d807336a14d7b89bf62bef2fb54f9af6a546f818dc1e98b9e07f8a5834da50fa28fb5874af91bf06020d1bf0120e",
            .derive_key = "7a7acac8a02adcf3038d74cdd1d34527de8a0fcc0ee3399d1262397ce5817f6055d0cefd84d9d57fe792d65a278fd20384ac6c30fdb340092f1a74a92ace99c482b28f0fc0ef3b923e56ade20c6dba47e49227166251337d80a037e987ad3a7f728b5ab6dfafd6e2ab1bd583a95d9c895ba9c2422c24ea0f62961f0dca45cad47bfa0d",
        },
        .{
            .input_len = 5121,
            .hash = "628bd2cb2004694adaab7bbd778a25df25c47b9d4155a55f8fbd79f2fe154cff96adaab0613a6146cdaabe498c3a94e529d3fc1da2bd08edf54ed64d40dcd6777647eac51d8277d70219a9694334a68bc8f0f23e20b0ff70ada6f844542dfa32cd4204ca1846ef76d811cdb296f65e260227f477aa7aa008bac878f72257484f2b6c95",
            .keyed_hash = "6ccf1c34753e7a044db80798ecd0782a8f76f33563accaddbfbb2e0ea4b2d0240d07e63f13667a8d1490e5e04f13eb617aea16a8c8a5aaed1ef6fbde1b0515e3c81050b361af6ead126032998290b563e3caddeaebfab592e155f2e161fb7cba939092133f23f9e65245e58ec23457b78a2e8a125588aad6e07d7f11a85b88d375b72d",
            .derive_key = "b07f01e518e702f7ccb44a267e9e112d403a7b3f4883a47ffbed4b48339b3c341a0add0ac032ab5aaea1e4e5b004707ec5681ae0fcbe3796974c0b1cf31a194740c14519273eedaabec832e8a784b6e7cfc2c5952677e6c3f2c3914454082d7eb1ce1766ac7d75a4d3001fc89544dd46b5147382240d689bbbaefc359fb6ae30263165",
        },
        .{
            .input_len = 6144,
            .hash = "3e2e5b74e048f3add6d21faab3f83aa44d3b2278afb83b80b3c35164ebeca2054d742022da6fdda444ebc384b04a54c3ac5839b49da7d39f6d8a9db03deab32aade156c1c0311e9b3435cde0ddba0dce7b26a376cad121294b689193508dd63151603c6ddb866ad16c2ee41585d1633a2cea093bea714f4c5d6b903522045b20395c83",
            .keyed_hash = "3d6b6d21281d0ade5b2b016ae4034c5dec10ca7e475f90f76eac7138e9bc8f1dc35754060091dc5caf3efabe0603c60f45e415bb3407db67e6beb3d11cf8e4f7907561f05dace0c15807f4b5f389c841eb114d81a82c02a00b57206b1d11fa6e803486b048a5ce87105a686dee041207e095323dfe172df73deb8c9532066d88f9da7e",
            .derive_key = "2a95beae63ddce523762355cf4b9c1d8f131465780a391286a5d01abb5683a1597099e3c6488aab6c48f3c15dbe1942d21dbcdc12115d19a8b8465fb54e9053323a9178e4275647f1a9927f6439e52b7031a0b465c861a3fc531527f7758b2b888cf2f20582e9e2c593709c0a44f9c6e0f8b963994882ea4168827823eef1f64169fef",
        },
        .{
            .input_len = 6145,
            .hash = "f1323a8631446cc50536a9f705ee5cb619424d46887f3c376c695b70e0f0507f18a2cfdd73c6e39dd75ce7c1c6e3ef238fd54465f053b25d21044ccb2093beb015015532b108313b5829c3621ce324b8e14229091b7c93f32db2e4e63126a377d2a63a3597997d4f1cba59309cb4af240ba70cebff9a23d5e3ff0cdae2cfd54e070022",
            .keyed_hash = "9ac301e9e39e45e3250a7e3b3df701aa0fb6889fbd80eeecf28dbc6300fbc539f3c184ca2f59780e27a576c1d1fb9772e99fd17881d02ac7dfd39675aca918453283ed8c3169085ef4a466b91c1649cc341dfdee60e32231fc34c9c4e0b9a2ba87ca8f372589c744c15fd6f985eec15e98136f25beeb4b13c4e43dc84abcc79cd4646c",
            .derive_key = "379bcc61d0051dd489f686c13de00d5b14c505245103dc040d9e4dd1facab8e5114493d029bdbd295aaa744a59e31f35c7f52dba9c3642f773dd0b4262a9980a2aef811697e1305d37ba9d8b6d850ef07fe41108993180cf779aeece363704c76483458603bbeeb693cffbbe5588d1f3535dcad888893e53d977424bb707201569a8d2",
        },
        .{
            .input_len = 7168,
            .hash = "61da957ec2499a95d6b8023e2b0e604ec7f6b50e80a9678b89d2628e99ada77a5707c321c83361793b9af62a40f43b523df1c8633cecb4cd14d00bdc79c78fca5165b863893f6d38b02ff7236c5a9a8ad2dba87d24c547cab046c29fc5bc1ed142e1de4763613bb162a5a538e6ef05ed05199d751f9eb58d332791b8d73fb74e4fce95",
            .keyed_hash = "b42835e40e9d4a7f42ad8cc04f85a963a76e18198377ed84adddeaecacc6f3fca2f01d5277d69bb681c70fa8d36094f73ec06e452c80d2ff2257ed82e7ba348400989a65ee8daa7094ae0933e3d2210ac6395c4af24f91c2b590ef87d7788d7066ea3eaebca4c08a4f14b9a27644f99084c3543711b64a070b94f2c9d1d8a90d035d52",
            .derive_key = "11c37a112765370c94a51415d0d651190c288566e295d505defdad895dae223730d5a5175a38841693020669c7638f40b9bc1f9f39cf98bda7a5b54ae24218a800a2116b34665aa95d846d97ea988bfcb53dd9c055d588fa21ba78996776ea6c40bc428b53c62b5f3ccf200f647a5aae8067f0ea1976391fcc72af1945100e2a6dcb88",
        },
        .{
            .input_len = 7169,
            .hash = "a003fc7a51754a9b3c7fae0367ab3d782dccf28855a03d435f8cfe74605e781798a8b20534be1ca9eb2ae2df3fae2ea60e48c6fb0b850b1385b5de0fe460dbe9d9f9b0d8db4435da75c601156df9d047f4ede008732eb17adc05d96180f8a73548522840779e6062d643b79478a6e8dbce68927f36ebf676ffa7d72d5f68f050b119c8",
            .keyed_hash = "ed9b1a922c046fdb3d423ae34e143b05ca1bf28b710432857bf738bcedbfa5113c9e28d72fcbfc020814ce3f5d4fc867f01c8f5b6caf305b3ea8a8ba2da3ab69fabcb438f19ff11f5378ad4484d75c478de425fb8e6ee809b54eec9bdb184315dc856617c09f5340451bf42fd3270a7b0b6566169f242e533777604c118a6358250f54",
            .derive_key = "554b0a5efea9ef183f2f9b931b7497995d9eb26f5c5c6dad2b97d62fc5ac31d99b20652c016d88ba2a611bbd761668d5eda3e568e940faae24b0d9991c3bd25a65f770b89fdcadabcb3d1a9c1cb63e69721cacf1ae69fefdcef1e3ef41bc5312ccc17222199e47a26552c6adc460cf47a72319cb5039369d0060eaea59d6c65130f1dd",
        },
        .{
            .input_len = 8192,
            .hash = "aae792484c8efe4f19e2ca7d371d8c467ffb10748d8a5a1ae579948f718a2a635fe51a27db045a567c1ad51be5aa34c01c6651c4d9b5b5ac5d0fd58cf18dd61a47778566b797a8c67df7b1d60b97b19288d2d877bb2df417ace009dcb0241ca1257d62712b6a4043b4ff33f690d849da91ea3bf711ed583cb7b7a7da2839ba71309bbf",
            .keyed_hash = "dc9637c8845a770b4cbf76b8daec0eebf7dc2eac11498517f08d44c8fc00d58a4834464159dcbc12a0ba0c6d6eb41bac0ed6585cabfe0aca36a375e6c5480c22afdc40785c170f5a6b8a1107dbee282318d00d915ac9ed1143ad40765ec120042ee121cd2baa36250c618adaf9e27260fda2f94dea8fb6f08c04f8f10c78292aa46102",
            .derive_key = "ad01d7ae4ad059b0d33baa3c01319dcf8088094d0359e5fd45d6aeaa8b2d0c3d4c9e58958553513b67f84f8eac653aeeb02ae1d5672dcecf91cd9985a0e67f4501910ecba25555395427ccc7241d70dc21c190e2aadee875e5aae6bf1912837e53411dabf7a56cbf8e4fb780432b0d7fe6cec45024a0788cf5874616407757e9e6bef7",
        },
        .{
            .input_len = 8193,
            .hash = "bab6c09cb8ce8cf459261398d2e7aef35700bf488116ceb94a36d0f5f1b7bc3bb2282aa69be089359ea1154b9a9286c4a56af4de975a9aa4a5c497654914d279bea60bb6d2cf7225a2fa0ff5ef56bbe4b149f3ed15860f78b4e2ad04e158e375c1e0c0b551cd7dfc82f1b155c11b6b3ed51ec9edb30d133653bb5709d1dbd55f4e1ff6",
            .keyed_hash = "954a2a75420c8d6547e3ba5b98d963e6fa6491addc8c023189cc519821b4a1f5f03228648fd983aef045c2fa8290934b0866b615f585149587dda2299039965328835a2b18f1d63b7e300fc76ff260b571839fe44876a4eae66cbac8c67694411ed7e09df51068a22c6e67d6d3dd2cca8ff12e3275384006c80f4db68023f24eebba57",
            .derive_key = "af1e0346e389b17c23200270a64aa4e1ead98c61695d917de7d5b00491c9b0f12f20a01d6d622edf3de026a4db4e4526225debb93c1237934d71c7340bb5916158cbdafe9ac3225476b6ab57a12357db3abbad7a26c6e66290e44034fb08a20a8d0ec264f309994d2810c49cfba6989d7abb095897459f5425adb48aba07c5fb3c83c0",
        },
        .{
            .input_len = 16384,
            .hash = "f875d6646de28985646f34ee13be9a576fd515f76b5b0a26bb324735041ddde49d764c270176e53e97bdffa58d549073f2c660be0e81293767ed4e4929f9ad34bbb39a529334c57c4a381ffd2a6d4bfdbf1482651b172aa883cc13408fa67758a3e47503f93f87720a3177325f7823251b85275f64636a8f1d599c2e49722f42e93893",
            .keyed_hash = "9e9fc4eb7cf081ea7c47d1807790ed211bfec56aa25bb7037784c13c4b707b0df9e601b101e4cf63a404dfe50f2e1865bb12edc8fca166579ce0c70dba5a5c0fc960ad6f3772183416a00bd29d4c6e651ea7620bb100c9449858bf14e1ddc9ecd35725581ca5b9160de04060045993d972571c3e8f71e9d0496bfa744656861b169d65",
            .derive_key = "160e18b5878cd0df1c3af85eb25a0db5344d43a6fbd7a8ef4ed98d0714c3f7e160dc0b1f09caa35f2f417b9ef309dfe5ebd67f4c9507995a531374d099cf8ae317542e885ec6f589378864d3ea98716b3bbb65ef4ab5e0ab5bb298a501f19a41ec19af84a5e6b428ecd813b1a47ed91c9657c3fba11c406bc316768b58f6802c9e9b57",
        },
        .{
            .input_len = 31744,
            .hash = "62b6960e1a44bcc1eb1a611a8d6235b6b4b78f32e7abc4fb4c6cdcce94895c47860cc51f2b0c28a7b77304bd55fe73af663c02d3f52ea053ba43431ca5bab7bfea2f5e9d7121770d88f70ae9649ea713087d1914f7f312147e247f87eb2d4ffef0ac978bf7b6579d57d533355aa20b8b77b13fd09748728a5cc327a8ec470f4013226f",
            .keyed_hash = "efa53b389ab67c593dba624d898d0f7353ab99e4ac9d42302ee64cbf9939a4193a7258db2d9cd32a7a3ecfce46144114b15c2fcb68a618a976bd74515d47be08b628be420b5e830fade7c080e351a076fbc38641ad80c736c8a18fe3c66ce12f95c61c2462a9770d60d0f77115bbcd3782b593016a4e728d4c06cee4505cb0c08a42ec",
            .derive_key = "39772aef80e0ebe60596361e45b061e8f417429d529171b6764468c22928e28e9759adeb797a3fbf771b1bcea30150a020e317982bf0d6e7d14dd9f064bc11025c25f31e81bd78a921db0174f03dd481d30e93fd8e90f8b2fee209f849f2d2a52f31719a490fb0ba7aea1e09814ee912eba111a9fde9d5c274185f7bae8ba85d300a2b",
        },
        .{
            .input_len = 102400,
            .hash = "bc3e3d41a1146b069abffad3c0d44860cf664390afce4d9661f7902e7943e085e01c59dab908c04c3342b816941a26d69c2605ebee5ec5291cc55e15b76146e6745f0601156c3596cb75065a9c57f35585a52e1ac70f69131c23d611ce11ee4ab1ec2c009012d236648e77be9295dd0426f29b764d65de58eb7d01dd42248204f45f8e",
            .keyed_hash = "1c35d1a5811083fd7119f5d5d1ba027b4d01c0c6c49fb6ff2cf75393ea5db4a7f9dbdd3e1d81dcbca3ba241bb18760f207710b751846faaeb9dff8262710999a59b2aa1aca298a032d94eacfadf1aa192418eb54808db23b56e34213266aa08499a16b354f018fc4967d05f8b9d2ad87a7278337be9693fc638a3bfdbe314574ee6fc4",
            .derive_key = "4652cff7a3f385a6103b5c260fc1593e13c778dbe608efb092fe7ee69df6e9c6d83a3e041bc3a48df2879f4a0a3ed40e7c961c73eff740f3117a0504c2dff4786d44fb17f1549eb0ba585e40ec29bf7732f0b7e286ff8acddc4cb1e23b87ff5d824a986458dcc6a04ac83969b80637562953df51ed1a7e90a7926924d2763778be8560",
        },
    },
};

fn testBlake3(hasher: *Blake3, input_len: usize, expected_hex: [262]u8) !void {
    // Save initial state
    const initial_state = hasher.*;

    // Setup input pattern
    var input_pattern: [251]u8 = undefined;
    for (&input_pattern, 0..) |*e, i| e.* = @as(u8, @truncate(i));

    // Write repeating input pattern to hasher
    var input_counter = input_len;
    while (input_counter > 0) {
        const update_len = @min(input_counter, input_pattern.len);
        hasher.update(input_pattern[0..update_len]);
        input_counter -= update_len;
    }

    // Read final hash value
    var actual_bytes: [expected_hex.len / 2]u8 = undefined;
    hasher.final(actual_bytes[0..]);

    // Compare to expected value
    var expected_bytes: [expected_hex.len / 2]u8 = undefined;
    _ = fmt.hexToBytes(expected_bytes[0..], expected_hex[0..]) catch unreachable;
    try std.testing.expectEqual(actual_bytes, expected_bytes);

    // Restore initial state
    hasher.* = initial_state;
}

test "BLAKE3 reference test cases" {
    var hash_state = Blake3.init(.{});
    const hash = &hash_state;
    var keyed_hash_state = Blake3.init(.{ .key = reference_test.key.* });
    const keyed_hash = &keyed_hash_state;
    var derive_key_state = Blake3.initKdf(reference_test.context_string, .{});
    const derive_key = &derive_key_state;

    for (reference_test.cases) |t| {
        try testBlake3(hash, t.input_len, t.hash.*);
        try testBlake3(keyed_hash, t.input_len, t.keyed_hash.*);
        try testBlake3(derive_key, t.input_len, t.derive_key.*);
    }
}

test "BLAKE3 parallel vs sequential" {
    const allocator = std.testing.allocator;
    const io = std.testing.io;

    // Test various sizes including those above the parallelization threshold
    const test_sizes = [_]usize{
        0, // Empty
        64, // One block
        1024, // One chunk
        1024 * 10, // Multiple chunks
        1024 * 100, // 100KB
        1024 * 1000, // 1MB
        1024 * 5000, // 5MB (above threshold)
        1024 * 10000, // 10MB (above threshold)
    };

    for (test_sizes) |size| {
        // Allocate and fill test data with a pattern
        const input = try allocator.alloc(u8, size);
        defer allocator.free(input);
        for (input, 0..) |*byte, i| {
            byte.* = @truncate(i);
        }

        // Test regular hash
        var expected: [32]u8 = undefined;
        Blake3.hash(input, &expected, .{});

        var actual: [32]u8 = undefined;
        try Blake3.hashParallel(input, &actual, .{}, allocator, io);

        try std.testing.expectEqualSlices(u8, &expected, &actual);

        // Test keyed hash
        const key: [32]u8 = @splat(0x42);
        var expected_keyed: [32]u8 = undefined;
        Blake3.hash(input, &expected_keyed, .{ .key = key });

        var actual_keyed: [32]u8 = undefined;
        try Blake3.hashParallel(input, &actual_keyed, .{ .key = key }, allocator, io);

        try std.testing.expectEqualSlices(u8, &expected_keyed, &actual_keyed);
    }
}



---
File: /std/crypto/cbc_mac.zig
---

const std = @import("std");
const crypto = std.crypto;
const mem = std.mem;

/// CBC-MAC with AES-128 - FIPS 113 https://csrc.nist.gov/publications/detail/fips/113/archive/1985-05-30
pub const CbcMacAes128 = CbcMac(crypto.core.aes.Aes128);

/// FIPS 113 (1985): Computer Data Authentication
/// https://csrc.nist.gov/publications/detail/fips/113/archive/1985-05-30
///
/// WARNING: CBC-MAC is insecure for variable-length messages without additional
/// protection. Only use when required by protocols like CCM that mitigate this.
pub fn CbcMac(comptime BlockCipher: type) type {
    const BlockCipherCtx = @typeInfo(@TypeOf(BlockCipher.initEnc)).@"fn".return_type.?;
    const Block = [BlockCipher.block.block_length]u8;

    return struct {
        const Self = @This();
        pub const key_length = BlockCipher.key_bits / 8;
        pub const block_length = BlockCipher.block.block_length;
        pub const mac_length = block_length;

        cipher_ctx: BlockCipherCtx,
        buf: Block = [_]u8{0} ** block_length,
        pos: usize = 0,

        pub fn create(out: *[mac_length]u8, msg: []const u8, key: *const [key_length]u8) void {
            var ctx = Self.init(key);
            ctx.update(msg);
            ctx.final(out);
        }

        pub fn init(key: *const [key_length]u8) Self {
            return Self{
                .cipher_ctx = BlockCipher.initEnc(key.*),
            };
        }

        pub fn update(self: *Self, msg: []const u8) void {
            const left = block_length - self.pos;
            var m = msg;

            // Partial buffer exists from previous update. Complete the block.
            if (m.len > left) {
                for (self.buf[self.pos..], 0..) |*b, i| b.* ^= m[i];
                m = m[left..];
                self.cipher_ctx.encrypt(&self.buf, &self.buf);
                self.pos = 0;
            }

            // Full blocks.
            while (m.len > block_length) {
                for (self.buf[0..block_length], 0..) |*b, i| b.* ^= m[i];
                m = m[block_length..];
                self.cipher_ctx.encrypt(&self.buf, &self.buf);
                self.pos = 0;
            }

            // Copy any remainder for next pass.
            if (m.len > 0) {
                for (self.buf[self.pos..][0..m.len], 0..) |*b, i| b.* ^= m[i];
                self.pos += m.len;
            }
        }

        pub fn final(self: *Self, out: *[mac_length]u8) void {
            // CBC-MAC: encrypt the current buffer state.
            // Partial blocks are implicitly zero-padded: buf[pos..] contains zeros from initialization.
            self.cipher_ctx.encrypt(out, &self.buf);
        }
    };
}

const testing = std.testing;

test "CbcMacAes128 - Empty message" {
    const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
    var msg: [0]u8 = undefined;

    // CBC-MAC of empty message = Encrypt(0)
    const expected = [_]u8{ 0x7d, 0xf7, 0x6b, 0x0c, 0x1a, 0xb8, 0x99, 0xb3, 0x3e, 0x42, 0xf0, 0x47, 0xb9, 0x1b, 0x54, 0x6f };

    var out: [CbcMacAes128.mac_length]u8 = undefined;
    CbcMacAes128.create(&out, &msg, &key);
    try testing.expectEqualSlices(u8, &out, &expected);
}

test "CbcMacAes128 - Single block (16 bytes)" {
    const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
    const msg = [_]u8{ 0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a };

    // CBC-MAC = Encrypt(msg XOR 0)
    const expected = [_]u8{ 0x3a, 0xd7, 0x7b, 0xb4, 0x0d, 0x7a, 0x36, 0x60, 0xa8, 0x9e, 0xca, 0xf3, 0x24, 0x66, 0xef, 0x97 };

    var out: [CbcMacAes128.mac_length]u8 = undefined;
    CbcMacAes128.create(&out, &msg, &key);
    try testing.expectEqualSlices(u8, &out, &expected);
}

test "CbcMacAes128 - Multiple blocks (40 bytes)" {
    const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
    const msg = [_]u8{
        0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a,
        0xae, 0x2d, 0x8a, 0x57, 0x1e, 0x03, 0xac, 0x9c, 0x9e, 0xb7, 0x6f, 0xac, 0x45, 0xaf, 0x8e, 0x51,
        0x30, 0xc8, 0x1c, 0x46, 0xa3, 0x5c, 0xe4, 0x11,
    };

    // CBC-MAC processes: block1 | block2 | block3 (last 8 bytes zero-padded)
    const expected = [_]u8{ 0x07, 0xd1, 0x92, 0xe3, 0xe6, 0xf0, 0x99, 0xed, 0xcc, 0x39, 0xfd, 0xe6, 0xd0, 0x9c, 0x76, 0x2d };

    var out: [CbcMacAes128.mac_length]u8 = undefined;
    CbcMacAes128.create(&out, &msg, &key);
    try testing.expectEqualSlices(u8, &out, &expected);
}

test "CbcMacAes128 - Incremental update" {
    const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
    const msg = [_]u8{
        0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a,
        0xae, 0x2d, 0x8a, 0x57, 0x1e, 0x03, 0xac, 0x9c, 0x9e, 0xb7, 0x6f, 0xac, 0x45, 0xaf, 0x8e, 0x51,
    };

    // Process in chunks
    var ctx = CbcMacAes128.init(&key);
    ctx.update(msg[0..10]);
    ctx.update(msg[10..20]);
    ctx.update(msg[20..]);

    var out1: [CbcMacAes128.mac_length]u8 = undefined;
    ctx.final(&out1);

    // Compare with one-shot processing
    var out2: [CbcMacAes128.mac_length]u8 = undefined;
    CbcMacAes128.create(&out2, &msg, &key);

    try testing.expectEqualSlices(u8, &out1, &out2);
}

test "CbcMacAes128 - Different from CMAC" {
    // Verify that CBC-MAC and CMAC produce different outputs
    const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
    const msg = [_]u8{ 0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a };

    var cbc_mac_out: [CbcMacAes128.mac_length]u8 = undefined;
    CbcMacAes128.create(&cbc_mac_out, &msg, &key);

    // CMAC output for same input (from RFC 4493)
    const cmac_out = [_]u8{ 0x07, 0x0a, 0x16, 0xb4, 0x6b, 0x4d, 0x41, 0x44, 0xf7, 0x9b, 0xdd, 0x9d, 0xd0, 0x4a, 0x28, 0x7c };

    // They should be different
    try testing.expect(!mem.eql(u8, &cbc_mac_out, &cmac_out));
}



---
File: /std/crypto/Certificate.zig
---

buffer: []const u8,
index: u32,

pub const Bundle = @import("Certificate/Bundle.zig");
pub const Chain = switch (builtin.os.tag) {
    else => void, // not a shim to also avoid expensive caller logic
    .windows => @import("Certificate/Chain.zig"),
};

pub const Version = enum { v1, v2, v3 };

pub const Algorithm = enum {
    sha1WithRSAEncryption,
    sha224WithRSAEncryption,
    sha256WithRSAEncryption,
    sha384WithRSAEncryption,
    sha512WithRSAEncryption,
    ecdsa_with_SHA224,
    ecdsa_with_SHA256,
    ecdsa_with_SHA384,
    ecdsa_with_SHA512,
    md2WithRSAEncryption,
    md5WithRSAEncryption,
    curveEd25519,

    pub const map = std.StaticStringMap(Algorithm).initComptime(.{
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x05 }, .sha1WithRSAEncryption },
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B }, .sha256WithRSAEncryption },
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0C }, .sha384WithRSAEncryption },
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0D }, .sha512WithRSAEncryption },
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0E }, .sha224WithRSAEncryption },
        .{ &.{ 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04, 0x03, 0x01 }, .ecdsa_with_SHA224 },
        .{ &.{ 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04, 0x03, 0x02 }, .ecdsa_with_SHA256 },
        .{ &.{ 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04, 0x03, 0x03 }, .ecdsa_with_SHA384 },
        .{ &.{ 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04, 0x03, 0x04 }, .ecdsa_with_SHA512 },
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x02 }, .md2WithRSAEncryption },
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x04 }, .md5WithRSAEncryption },
        .{ &.{ 0x2B, 0x65, 0x70 }, .curveEd25519 },
    });

    pub fn Hash(comptime algorithm: Algorithm) type {
        return switch (algorithm) {
            .sha1WithRSAEncryption => crypto.hash.Sha1,
            .ecdsa_with_SHA224, .sha224WithRSAEncryption => crypto.hash.sha2.Sha224,
            .ecdsa_with_SHA256, .sha256WithRSAEncryption => crypto.hash.sha2.Sha256,
            .ecdsa_with_SHA384, .sha384WithRSAEncryption => crypto.hash.sha2.Sha384,
            .ecdsa_with_SHA512, .sha512WithRSAEncryption, .curveEd25519 => crypto.hash.sha2.Sha512,
            .md2WithRSAEncryption => @compileError("unimplemented"),
            .md5WithRSAEncryption => crypto.hash.Md5,
        };
    }
};

pub const AlgorithmCategory = enum {
    rsaEncryption,
    rsassa_pss,
    X9_62_id_ecPublicKey,
    curveEd25519,

    pub const map = std.StaticStringMap(AlgorithmCategory).initComptime(.{
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01 }, .rsaEncryption },
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0A }, .rsassa_pss },
        .{ &.{ 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01 }, .X9_62_id_ecPublicKey },
        .{ &.{ 0x2B, 0x65, 0x70 }, .curveEd25519 },
    });
};

pub const Attribute = enum {
    commonName,
    serialNumber,
    countryName,
    localityName,
    stateOrProvinceName,
    streetAddress,
    organizationName,
    organizationalUnitName,
    postalCode,
    organizationIdentifier,
    pkcs9_emailAddress,
    domainComponent,

    pub const map = std.StaticStringMap(Attribute).initComptime(.{
        .{ &.{ 0x55, 0x04, 0x03 }, .commonName },
        .{ &.{ 0x55, 0x04, 0x05 }, .serialNumber },
        .{ &.{ 0x55, 0x04, 0x06 }, .countryName },
        .{ &.{ 0x55, 0x04, 0x07 }, .localityName },
        .{ &.{ 0x55, 0x04, 0x08 }, .stateOrProvinceName },
        .{ &.{ 0x55, 0x04, 0x09 }, .streetAddress },
        .{ &.{ 0x55, 0x04, 0x0A }, .organizationName },
        .{ &.{ 0x55, 0x04, 0x0B }, .organizationalUnitName },
        .{ &.{ 0x55, 0x04, 0x11 }, .postalCode },
        .{ &.{ 0x55, 0x04, 0x61 }, .organizationIdentifier },
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x01 }, .pkcs9_emailAddress },
        .{ &.{ 0x09, 0x92, 0x26, 0x89, 0x93, 0xF2, 0x2C, 0x64, 0x01, 0x19 }, .domainComponent },
    });
};

pub const NamedCurve = enum {
    secp384r1,
    secp521r1,
    X9_62_prime256v1,

    pub const map = std.StaticStringMap(NamedCurve).initComptime(.{
        .{ &.{ 0x2B, 0x81, 0x04, 0x00, 0x22 }, .secp384r1 },
        .{ &.{ 0x2B, 0x81, 0x04, 0x00, 0x23 }, .secp521r1 },
        .{ &.{ 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07 }, .X9_62_prime256v1 },
    });

    pub fn Curve(comptime curve: NamedCurve) type {
        return switch (curve) {
            .X9_62_prime256v1 => crypto.ecc.P256,
            .secp384r1 => crypto.ecc.P384,
            .secp521r1 => @compileError("unimplemented"),
        };
    }
};

pub const ExtensionId = enum {
    subject_key_identifier,
    key_usage,
    private_key_usage_period,
    subject_alt_name,
    issuer_alt_name,
    basic_constraints,
    crl_number,
    certificate_policies,
    authority_key_identifier,
    msCertsrvCAVersion,
    commonName,
    ext_key_usage,
    crl_distribution_points,
    info_access,
    entrustVersInfo,
    enroll_certtype,
    pe_logotype,
    netscape_cert_type,
    netscape_comment,

    pub const map = std.StaticStringMap(ExtensionId).initComptime(.{
        .{ &.{ 0x55, 0x04, 0x03 }, .commonName },
        .{ &.{ 0x55, 0x1D, 0x01 }, .authority_key_identifier },
        .{ &.{ 0x55, 0x1D, 0x07 }, .subject_alt_name },
        .{ &.{ 0x55, 0x1D, 0x0E }, .subject_key_identifier },
        .{ &.{ 0x55, 0x1D, 0x0F }, .key_usage },
        .{ &.{ 0x55, 0x1D, 0x0A }, .basic_constraints },
        .{ &.{ 0x55, 0x1D, 0x10 }, .private_key_usage_period },
        .{ &.{ 0x55, 0x1D, 0x11 }, .subject_alt_name },
        .{ &.{ 0x55, 0x1D, 0x12 }, .issuer_alt_name },
        .{ &.{ 0x55, 0x1D, 0x13 }, .basic_constraints },
        .{ &.{ 0x55, 0x1D, 0x14 }, .crl_number },
        .{ &.{ 0x55, 0x1D, 0x1F }, .crl_distribution_points },
        .{ &.{ 0x55, 0x1D, 0x20 }, .certificate_policies },
        .{ &.{ 0x55, 0x1D, 0x23 }, .authority_key_identifier },
        .{ &.{ 0x55, 0x1D, 0x25 }, .ext_key_usage },
        .{ &.{ 0x2B, 0x06, 0x01, 0x04, 0x01, 0x82, 0x37, 0x15, 0x01 }, .msCertsrvCAVersion },
        .{ &.{ 0x2B, 0x06, 0x01, 0x05, 0x05, 0x07, 0x01, 0x01 }, .info_access },
        .{ &.{ 0x2A, 0x86, 0x48, 0x86, 0xF6, 0x7D, 0x07, 0x41, 0x00 }, .entrustVersInfo },
        .{ &.{ 0x2b, 0x06, 0x01, 0x04, 0x01, 0x82, 0x37, 0x14, 0x02 }, .enroll_certtype },
        .{ &.{ 0x2b, 0x06, 0x01, 0x05, 0x05, 0x07, 0x01, 0x0c }, .pe_logotype },
        .{ &.{ 0x60, 0x86, 0x48, 0x01, 0x86, 0xf8, 0x42, 0x01, 0x01 }, .netscape_cert_type },
        .{ &.{ 0x60, 0x86, 0x48, 0x01, 0x86, 0xf8, 0x42, 0x01, 0x0d }, .netscape_comment },
    });
};

pub const GeneralNameTag = enum(u5) {
    otherName = 0,
    rfc822Name = 1,
    dNSName = 2,
    x400Address = 3,
    directoryName = 4,
    ediPartyName = 5,
    uniformResourceIdentifier = 6,
    iPAddress = 7,
    registeredID = 8,
    _,
};

pub const Parsed = struct {
    certificate: Certificate,
    issuer_slice: Slice,
    subject_slice: Slice,
    common_name_slice: Slice,
    signature_slice: Slice,
    signature_algorithm: Algorithm,
    pub_key_algo: PubKeyAlgo,
    pub_key_slice: Slice,
    message_slice: Slice,
    subject_alt_name_slice: Slice,
    validity: Validity,
    version: Version,

    pub const PubKeyAlgo = union(AlgorithmCategory) {
        rsaEncryption: void,
        rsassa_pss: void,
        X9_62_id_ecPublicKey: NamedCurve,
        curveEd25519: void,
    };

    pub const Validity = struct {
        not_before: u64,
        not_after: u64,
    };

    pub const Slice = der.Element.Slice;

    pub fn slice(p: Parsed, s: Slice) []const u8 {
        return p.certificate.buffer[s.start..s.end];
    }

    pub fn issuer(p: Parsed) []const u8 {
        return p.slice(p.issuer_slice);
    }

    pub fn subject(p: Parsed) []const u8 {
        return p.slice(p.subject_slice);
    }

    pub fn commonName(p: Parsed) []const u8 {
        return p.slice(p.common_name_slice);
    }

    pub fn signature(p: Parsed) []const u8 {
        return p.slice(p.signature_slice);
    }

    pub fn pubKey(p: Parsed) []const u8 {
        return p.slice(p.pub_key_slice);
    }

    pub fn message(p: Parsed) []const u8 {
        return p.slice(p.message_slice);
    }

    pub fn subjectAltName(p: Parsed) []const u8 {
        return p.slice(p.subject_alt_name_slice);
    }

    pub const VerifyError = error{
        CertificateIssuerMismatch,
        CertificateNotYetValid,
        CertificateExpired,
        CertificateSignatureAlgorithmUnsupported,
        CertificateSignatureAlgorithmMismatch,
        CertificateFieldHasInvalidLength,
        CertificateFieldHasWrongDataType,
        CertificatePublicKeyInvalid,
        CertificateSignatureInvalidLength,
        CertificateSignatureInvalid,
        CertificateSignatureUnsupportedBitCount,
        CertificateSignatureNamedCurveUnsupported,
    };

    /// This function verifies:
    ///  * That the subject's issuer is indeed the provided issuer.
    ///  * The time validity of the subject.
    ///  * The signature.
    pub fn verify(parsed_subject: Parsed, parsed_issuer: Parsed, now_sec: i64) VerifyError!void {
        // Check that the subject's issuer name matches the issuer's
        // subject name.
        if (!mem.eql(u8, parsed_subject.issuer(), parsed_issuer.subject())) {
            return error.CertificateIssuerMismatch;
        }

        if (now_sec < parsed_subject.validity.not_before)
            return error.CertificateNotYetValid;
        if (now_sec > parsed_subject.validity.not_after)
            return error.CertificateExpired;

        switch (parsed_subject.signature_algorithm) {
            inline .sha1WithRSAEncryption,
            .sha224WithRSAEncryption,
            .sha256WithRSAEncryption,
            .sha384WithRSAEncryption,
            .sha512WithRSAEncryption,
            => |algorithm| return verifyRsa(
                algorithm.Hash(),
                parsed_subject.message(),
                parsed_subject.signature(),
                parsed_issuer.pub_key_algo,
                parsed_issuer.pubKey(),
            ),

            inline .ecdsa_with_SHA224,
            .ecdsa_with_SHA256,
            .ecdsa_with_SHA384,
            .ecdsa_with_SHA512,
            => |algorithm| return verify_ecdsa(
                algorithm.Hash(),
                parsed_subject.message(),
                parsed_subject.signature(),
                parsed_issuer.pub_key_algo,
                parsed_issuer.pubKey(),
            ),

            .md2WithRSAEncryption, .md5WithRSAEncryption => {
                return error.CertificateSignatureAlgorithmUnsupported;
            },

            .curveEd25519 => return verifyEd25519(
                parsed_subject.message(),
                parsed_subject.signature(),
                parsed_issuer.pub_key_algo,
                parsed_issuer.pubKey(),
            ),
        }
    }

    pub const VerifyHostNameError = error{
        CertificateHostMismatch,
        CertificateFieldHasInvalidLength,
    };

    pub fn verifyHostName(parsed_subject: Parsed, host_name: []const u8) VerifyHostNameError!void {
        // If the Subject Alternative Names extension is present, this is
        // what to check. Otherwise, only the common name is checked.
        const subject_alt_name = parsed_subject.subjectAltName();
        if (subject_alt_name.len == 0) {
            if (checkHostName(host_name, parsed_subject.commonName())) {
                return;
            } else {
                return error.CertificateHostMismatch;
            }
        }

        const general_names = try der.Element.parse(subject_alt_name, 0);
        var name_i = general_names.slice.start;
        while (name_i < general_names.slice.end) {
            const general_name = try der.Element.parse(subject_alt_name, name_i);
            name_i = general_name.slice.end;
            switch (@as(GeneralNameTag, @enumFromInt(@intFromEnum(general_name.identifier.tag)))) {
                .dNSName => {
                    const dns_name = subject_alt_name[general_name.slice.start..general_name.slice.end];
                    if (checkHostName(host_name, dns_name)) return;
                },
                else => {},
            }
        }

        return error.CertificateHostMismatch;
    }

    // Check hostname according to RFC2818 specification:
    //
    // If more than one identity of a given type is present in
    // the certificate (e.g., more than one DNSName name, a match in any one
    // of the set is considered acceptable.) Names may contain the wildcard
    // character * which is considered to match any single domain name
    // component. E.g., *.a.com matches foo.a.com but not bar.foo.a.com.
    // Partial wildcards like f*.com are not supported.
    fn checkHostName(host_name: []const u8, dns_name: []const u8) bool {
        // Empty strings should not match
        if (host_name.len == 0 or dns_name.len == 0) return false;

        // RFC 6125 Section 6.4.1: Exact match (case-insensitive)
        if (std.ascii.eqlIgnoreCase(dns_name, host_name)) {
            return true; // exact match
        }

        // RFC 6125 Section 6.4.3: Wildcard certificates
        // Wildcard must be leftmost label and in the form "*.rest.of.domain"
        if (dns_name.len >= 3 and mem.startsWith(u8, dns_name, "*.")) {
            const wildcard_suffix = dns_name[2..];

            // No additional wildcards allowed in the suffix
            if (mem.find(u8, wildcard_suffix, "*") != null) return false;

            // Find the first dot in hostname to split first label from rest
            const dot_pos = mem.find(u8, host_name, ".") orelse return false;

            // Wildcard matches exactly one label, so compare the rest
            const host_suffix = host_name[dot_pos + 1 ..];

            // Match suffixes (case-insensitive per RFC 6125)
            return std.ascii.eqlIgnoreCase(wildcard_suffix, host_suffix);
        }

        return false;
    }
};

test "Parsed.checkHostName RFC 6125 compliance" {
    const expectEqual = std.testing.expectEqual;

    // Exact match tests
    try expectEqual(true, Parsed.checkHostName("ziglang.org", "ziglang.org"));
    try expectEqual(true, Parsed.checkHostName("ziglang.org", "Ziglang.org")); // case insensitive
    try expectEqual(true, Parsed.checkHostName("ZIGLANG.ORG", "ziglang.org")); // case insensitive

    // Valid wildcard matches
    try expectEqual(true, Parsed.checkHostName("bar.ziglang.org", "*.ziglang.org"));
    try expectEqual(true, Parsed.checkHostName("BAR.ziglang.org", "*.Ziglang.ORG")); // case insensitive

    // RFC 6125: Wildcard matches exactly one label
    try expectEqual(false, Parsed.checkHostName("foo.bar.ziglang.org", "*.ziglang.org"));
    try expectEqual(false, Parsed.checkHostName("ziglang.org", "*.ziglang.org")); // no empty match

    // RFC 6125: No partial wildcards allowed
    try expectEqual(false, Parsed.checkHostName("ziglang.org", "zig*.org"));
    try expectEqual(false, Parsed.checkHostName("ziglang.org", "*lang.org"));
    try expectEqual(false, Parsed.checkHostName("ziglang.org", "zi*ng.org"));

    // RFC 6125: No multiple wildcards
    try expectEqual(false, Parsed.checkHostName("foo.bar.org", "*.*.org"));

    // RFC 6125: Wildcard must be in leftmost label
    try expectEqual(false, Parsed.checkHostName("foo.bar.org", "foo.*.org"));

    // Single label hostnames should not match wildcards
    try expectEqual(false, Parsed.checkHostName("localhost", "*.local"));
    try expectEqual(false, Parsed.checkHostName("localhost", "*.localhost"));

    // Edge cases
    try expectEqual(false, Parsed.checkHostName("", ""));
    try expectEqual(false, Parsed.checkHostName("example.com", ""));
    try expectEqual(false, Parsed.checkHostName("", "*.example.com"));
    try expectEqual(false, Parsed.checkHostName("example.com", "*"));
    try expectEqual(false, Parsed.checkHostName("example.com", "*."));
}

pub const ParseError = der.Element.ParseError || ParseVersionError || ParseTimeError || ParseEnumError || ParseBitStringError;

pub fn parse(cert: Certificate) ParseError!Parsed {
    const cert_bytes = cert.buffer;
    const certificate = try der.Element.parse(cert_bytes, cert.index);
    const tbs_certificate = try der.Element.parse(cert_bytes, certificate.slice.start);
    const version_elem = try der.Element.parse(cert_bytes, tbs_certificate.slice.start);
    const version = try parseVersion(cert_bytes, version_elem);
    const serial_number = if (@as(u8, @bitCast(version_elem.identifier)) == 0xa0)
        try der.Element.parse(cert_bytes, version_elem.slice.end)
    else
        version_elem;
    // RFC 5280, section 4.1.2.3:
    // "This field MUST contain the same algorithm identifier as
    // the signatureAlgorithm field in the sequence Certificate."
    const tbs_signature = try der.Element.parse(cert_bytes, serial_number.slice.end);
    const issuer = try der.Element.parse(cert_bytes, tbs_signature.slice.end);
    const validity = try der.Element.parse(cert_bytes, issuer.slice.end);
    const not_before = try der.Element.parse(cert_bytes, validity.slice.start);
    const not_before_utc = try parseTime(cert, not_before);
    const not_after = try der.Element.parse(cert_bytes, not_before.slice.end);
    const not_after_utc = try parseTime(cert, not_after);
    const subject = try der.Element.parse(cert_bytes, validity.slice.end);

    const pub_key_info = try der.Element.parse(cert_bytes, subject.slice.end);
    const pub_key_signature_algorithm = try der.Element.parse(cert_bytes, pub_key_info.slice.start);
    const pub_key_algo_elem = try der.Element.parse(cert_bytes, pub_key_signature_algorithm.slice.start);
    const pub_key_algo: Parsed.PubKeyAlgo = switch (try parseAlgorithmCategory(cert_bytes, pub_key_algo_elem)) {
        inline else => |tag| @unionInit(Parsed.PubKeyAlgo, @tagName(tag), {}),
        .X9_62_id_ecPublicKey => pub_key_algo: {
            // RFC 5480 Section 2.1.1.1 Named Curve
            // ECParameters ::= CHOICE {
            //   namedCurve         OBJECT IDENTIFIER
            //   -- implicitCurve   NULL
            //   -- specifiedCurve  SpecifiedECDomain
            // }
            const params_elem = try der.Element.parse(cert_bytes, pub_key_algo_elem.slice.end);
            const named_curve = try parseNamedCurve(cert_bytes, params_elem);
            break :pub_key_algo .{ .X9_62_id_ecPublicKey = named_curve };
        },
    };
    const pub_key_elem = try der.Element.parse(cert_bytes, pub_key_signature_algorithm.slice.end);
    const pub_key = try parseBitString(cert, pub_key_elem);

    var common_name = der.Element.Slice.empty;
    var name_i = subject.slice.start;
    while (name_i < subject.slice.end) {
        const rdn = try der.Element.parse(cert_bytes, name_i);
        var rdn_i = rdn.slice.start;
        while (rdn_i < rdn.slice.end) {
            const atav = try der.Element.parse(cert_bytes, rdn_i);
            var atav_i = atav.slice.start;
            while (atav_i < atav.slice.end) {
                const ty_elem = try der.Element.parse(cert_bytes, atav_i);
                const val = try der.Element.parse(cert_bytes, ty_elem.slice.end);
                atav_i = val.slice.end;
                const ty = parseAttribute(cert_bytes, ty_elem) catch |err| switch (err) {
                    error.CertificateHasUnrecognizedObjectId => continue,
                    else => |e| return e,
                };
                switch (ty) {
                    .commonName => common_name = val.slice,
                    else => {},
                }
            }
            rdn_i = atav.slice.end;
        }
        name_i = rdn.slice.end;
    }

    const sig_algo = try der.Element.parse(cert_bytes, tbs_certificate.slice.end);
    const algo_elem = try der.Element.parse(cert_bytes, sig_algo.slice.start);
    const signature_algorithm = try parseAlgorithm(cert_bytes, algo_elem);
    const sig_elem = try der.Element.parse(cert_bytes, sig_algo.slice.end);
    const signature = try parseBitString(cert, sig_elem);

    // Extensions
    var subject_alt_name_slice = der.Element.Slice.empty;
    ext: {
        if (version == .v1)
            break :ext;

        if (pub_key_info.slice.end >= tbs_certificate.slice.end)
            break :ext;

        const outer_extensions = try der.Element.parse(cert_bytes, pub_key_info.slice.end);
        if (outer_extensions.identifier.tag != .bitstring)
            break :ext;

        const extensions = try der.Element.parse(cert_bytes, outer_extensions.slice.start);

        var ext_i = extensions.slice.start;
        while (ext_i < extensions.slice.end) {
            const extension = try der.Element.parse(cert_bytes, ext_i);
            ext_i = extension.slice.end;
            const oid_elem = try der.Element.parse(cert_bytes, extension.slice.start);
            const ext_id = parseExtensionId(cert_bytes, oid_elem) catch |err| switch (err) {
                error.CertificateHasUnrecognizedObjectId => continue,
                else => |e| return e,
            };
            const critical_elem = try der.Element.parse(cert_bytes, oid_elem.slice.end);
            const ext_bytes_elem = if (critical_elem.identifier.tag != .boolean)
                critical_elem
            else
                try der.Element.parse(cert_bytes, critical_elem.slice.end);
            switch (ext_id) {
                .subject_alt_name => subject_alt_name_slice = ext_bytes_elem.slice,
                else => continue,
            }
        }
    }

    return .{
        .certificate = cert,
        .common_name_slice = common_name,
        .issuer_slice = issuer.slice,
        .subject_slice = subject.slice,
        .signature_slice = signature,
        .signature_algorithm = signature_algorithm,
        .message_slice = .{ .start = certificate.slice.start, .end = tbs_certificate.slice.end },
        .pub_key_algo = pub_key_algo,
        .pub_key_slice = pub_key,
        .validity = .{
            .not_before = not_before_utc,
            .not_after = not_after_utc,
        },
        .subject_alt_name_slice = subject_alt_name_slice,
        .version = version,
    };
}

pub fn verify(subject: Certificate, issuer: Certificate, now_sec: i64) !void {
    const parsed_subject = try subject.parse();
    const parsed_issuer = try issuer.parse();
    return parsed_subject.verify(parsed_issuer, now_sec);
}

pub fn contents(cert: Certificate, elem: der.Element) []const u8 {
    return cert.buffer[elem.slice.start..elem.slice.end];
}

pub const ParseBitStringError = error{ CertificateFieldHasWrongDataType, CertificateHasInvalidBitString };

pub fn parseBitString(cert: Certificate, elem: der.Element) !der.Element.Slice {
    if (elem.identifier.tag != .bitstring) return error.CertificateFieldHasWrongDataType;
    if (cert.buffer[elem.slice.start] != 0) return error.CertificateHasInvalidBitString;
    return .{ .start = elem.slice.start + 1, .end = elem.slice.end };
}

pub const ParseTimeError = error{ CertificateTimeInvalid, CertificateFieldHasWrongDataType };

/// Returns number of seconds since epoch.
pub fn parseTime(cert: Certificate, elem: der.Element) ParseTimeError!u64 {
    const bytes = cert.contents(elem);
    switch (elem.identifier.tag) {
        .utc_time => {
            // Example: "YYMMDD000000Z"
            if (bytes.len != 13)
                return error.CertificateTimeInvalid;
            if (bytes[12] != 'Z')
                return error.CertificateTimeInvalid;

            return Date.toSeconds(.{
                .year = @as(u16, 2000) + try parseTimeDigits(bytes[0..2], 0, 99),
                .month = try parseTimeDigits(bytes[2..4], 1, 12),
                .day = try parseTimeDigits(bytes[4..6], 1, 31),
                .hour = try parseTimeDigits(bytes[6..8], 0, 23),
                .minute = try parseTimeDigits(bytes[8..10], 0, 59),
                .second = try parseTimeDigits(bytes[10..12], 0, 59),
            });
        },
        .generalized_time => {
            // Examples:
            // "19920521000000Z"
            // "19920622123421Z"
            // "19920722132100.3Z"
            if (bytes.len < 15)
                return error.CertificateTimeInvalid;
            return Date.toSeconds(.{
                .year = try parseYear4(bytes[0..4]),
                .month = try parseTimeDigits(bytes[4..6], 1, 12),
                .day = try parseTimeDigits(bytes[6..8], 1, 31),
                .hour = try parseTimeDigits(bytes[8..10], 0, 23),
                .minute = try parseTimeDigits(bytes[10..12], 0, 59),
                .second = try parseTimeDigits(bytes[12..14], 0, 59),
            });
        },
        else => return error.CertificateFieldHasWrongDataType,
    }
}

const Date = struct {
    /// example: 1999
    year: u16,
    /// range: 1 to 12
    month: u8,
    /// range: 1 to 31
    day: u8,
    /// range: 0 to 59
    hour: u8,
    /// range: 0 to 59
    minute: u8,
    /// range: 0 to 59
    second: u8,

    /// Convert to number of seconds since epoch.
    pub fn toSeconds(date: Date) u64 {
        var sec: u64 = 0;

        {
            var year: u16 = 1970;
            while (year < date.year) : (year += 1) {
                const days: u64 = std.time.epoch.getDaysInYear(year);
                sec += days * std.time.epoch.secs_per_day;
            }
        }

        {
            var month: u4 = 1;
            while (month < date.month) : (month += 1) {
                const days: u64 = std.time.epoch.getDaysInMonth(
                    date.year,
                    @enumFromInt(month),
                );
                sec += days * std.time.epoch.secs_per_day;
            }
        }

        sec += (date.day - 1) * @as(u64, std.time.epoch.secs_per_day);
        sec += date.hour * @as(u64, 60 * 60);
        sec += date.minute * @as(u64, 60);
        sec += date.second;

        return sec;
    }
};

pub fn parseTimeDigits(text: *const [2]u8, min: u8, max: u8) !u8 {
    const V = @Vector(2, u16);
    const bytes: V = text.*;
    const zero: V = @splat('0');
    const mm: V = .{ 10, 1 };
    const d = bytes -% zero;
    if (@reduce(.Or, d > @as(V, @splat(9)))) {
        @branchHint(.unlikely);
        return error.CertificateTimeInvalid;
    }
    const result = @reduce(.Add, d *% mm);
    if (result < min) return error.CertificateTimeInvalid;
    if (result > max) return error.CertificateTimeInvalid;
    return @intCast(result);
}

test parseTimeDigits {
    const expectEqual = std.testing.expectEqual;
    try expectEqual(@as(u8, 0), try parseTimeDigits("00", 0, 99));
    try expectEqual(@as(u8, 99), try parseTimeDigits("99", 0, 99));
    try expectEqual(@as(u8, 42), try parseTimeDigits("42", 0, 99));

    const expectError = std.testing.expectError;
    try expectError(error.CertificateTimeInvalid, parseTimeDigits("13", 1, 12));
    try expectError(error.CertificateTimeInvalid, parseTimeDigits("00", 1, 12));
    try expectError(error.CertificateTimeInvalid, parseTimeDigits("Di", 0, 99));
    try expectError(error.CertificateTimeInvalid, parseTimeDigits("0:", 1, 31));
}

pub fn parseYear4(text: *const [4]u8) !u16 {
    const V = @Vector(4, u32);
    const bytes: V = text.*;
    const zero: V = @splat('0');
    const mmmm: V = .{ 1000, 100, 10, 1 };
    const d = bytes -% zero;
    if (@reduce(.Or, d > @as(V, @splat(9)))) {
        @branchHint(.unlikely);
        return error.CertificateTimeInvalid;
    }
    const result = @reduce(.Add, d *% mmmm);
    return @intCast(result);
}

test parseYear4 {
    const expectEqual = std.testing.expectEqual;
    try expectEqual(@as(u16, 0), try parseYear4("0000"));
    try expectEqual(@as(u16, 9999), try parseYear4("9999"));
    try expectEqual(@as(u16, 1988), try parseYear4("1988"));

    const expectError = std.testing.expectError;
    try expectError(error.CertificateTimeInvalid, parseYear4("999b"));
    try expectError(error.CertificateTimeInvalid, parseYear4("crap"));
    try expectError(error.CertificateTimeInvalid, parseYear4("r:bQ"));
    try expectError(error.CertificateTimeInvalid, parseYear4("000:"));
    try expectError(error.CertificateTimeInvalid, parseYear4("0???"));
    try expectError(error.CertificateTimeInvalid, parseYear4("*zig"));
}

pub fn parseAlgorithm(bytes: []const u8, element: der.Element) ParseEnumError!Algorithm {
    return parseEnum(Algorithm, bytes, element);
}

pub fn parseAlgorithmCategory(bytes: []const u8, element: der.Element) ParseEnumError!AlgorithmCategory {
    return parseEnum(AlgorithmCategory, bytes, element);
}

pub fn parseAttribute(bytes: []const u8, element: der.Element) ParseEnumError!Attribute {
    return parseEnum(Attribute, bytes, element);
}

pub fn parseNamedCurve(bytes: []const u8, element: der.Element) ParseEnumError!NamedCurve {
    return parseEnum(NamedCurve, bytes, element);
}

pub fn parseExtensionId(bytes: []const u8, element: der.Element) ParseEnumError!ExtensionId {
    return parseEnum(ExtensionId, bytes, element);
}

pub const ParseEnumError = error{ CertificateFieldHasWrongDataType, CertificateHasUnrecognizedObjectId };

fn parseEnum(comptime E: type, bytes: []const u8, element: der.Element) ParseEnumError!E {
    if (element.identifier.tag != .object_identifier)
        return error.CertificateFieldHasWrongDataType;
    const oid_bytes = bytes[element.slice.start..element.slice.end];
    return E.map.get(oid_bytes) orelse return error.CertificateHasUnrecognizedObjectId;
}

pub const ParseVersionError = error{ UnsupportedCertificateVersion, CertificateFieldHasInvalidLength };

pub fn parseVersion(bytes: []const u8, version_elem: der.Element) ParseVersionError!Version {
    if (@as(u8, @bitCast(version_elem.identifier)) != 0xa0)
        return .v1;

    if (version_elem.slice.end - version_elem.slice.start != 3)
        return error.CertificateFieldHasInvalidLength;

    const encoded_version = bytes[version_elem.slice.start..version_elem.slice.end];

    if (mem.eql(u8, encoded_version, "\x02\x01\x02")) {
        return .v3;
    } else if (mem.eql(u8, encoded_version, "\x02\x01\x01")) {
        return .v2;
    } else if (mem.eql(u8, encoded_version, "\x02\x01\x00")) {
        return .v1;
    }

    return error.UnsupportedCertificateVersion;
}

fn verifyRsa(
    comptime Hash: type,
    msg: []const u8,
    sig: []const u8,
    pub_key_algo: Parsed.PubKeyAlgo,
    pub_key: []const u8,
) !void {
    if (pub_key_algo != .rsaEncryption) return error.CertificateSignatureAlgorithmMismatch;
    const pk_components = try rsa.PublicKey.parseDer(pub_key);
    const exponent = pk_components.exponent;
    const modulus = pk_components.modulus;
    if (exponent.len > modulus.len) return error.CertificatePublicKeyInvalid;
    if (sig.len != modulus.len) return error.CertificateSignatureInvalidLength;

    switch (modulus.len) {
        inline 128, 256, 384, 512 => |modulus_len| {
            const public_key = rsa.PublicKey.fromBytes(exponent, modulus) catch
                return error.CertificateSignatureInvalid;
            rsa.PKCS1v1_5Signature.verify(modulus_len, sig[0..modulus_len].*, msg, public_key, Hash) catch
                return error.CertificateSignatureInvalid;
        },
        else => return error.CertificateSignatureUnsupportedBitCount,
    }
}

fn verify_ecdsa(
    comptime Hash: type,
    message: []const u8,
    encoded_sig: []const u8,
    pub_key_algo: Parsed.PubKeyAlgo,
    sec1_pub_key: []const u8,
) !void {
    const sig_named_curve = switch (pub_key_algo) {
        .X9_62_id_ecPublicKey => |named_curve| named_curve,
        else => return error.CertificateSignatureAlgorithmMismatch,
    };

    switch (sig_named_curve) {
        .secp521r1 => {
            return error.CertificateSignatureNamedCurveUnsupported;
        },
        inline .X9_62_prime256v1,
        .secp384r1,
        => |curve| {
            const Ecdsa = crypto.sign.ecdsa.Ecdsa(curve.Curve(), Hash);
            const sig = Ecdsa.Signature.fromDer(encoded_sig) catch |err| switch (err) {
                error.InvalidEncoding => return error.CertificateSignatureInvalid,
            };
            const pub_key = Ecdsa.PublicKey.fromSec1(sec1_pub_key) catch |err| switch (err) {
                error.InvalidEncoding => return error.CertificateSignatureInvalid,
                error.NonCanonical => return error.CertificateSignatureInvalid,
                error.NotSquare => return error.CertificateSignatureInvalid,
            };
            sig.verify(message, pub_key) catch |err| switch (err) {
                error.IdentityElement => return error.CertificateSignatureInvalid,
                error.NonCanonical => return error.CertificateSignatureInvalid,
                error.SignatureVerificationFailed => return error.CertificateSignatureInvalid,
            };
        },
    }
}

fn verifyEd25519(
    message: []const u8,
    encoded_sig: []const u8,
    pub_key_algo: Parsed.PubKeyAlgo,
    encoded_pub_key: []const u8,
) !void {
    if (pub_key_algo != .curveEd25519) return error.CertificateSignatureAlgorithmMismatch;
    const Ed25519 = crypto.sign.Ed25519;
    if (encoded_sig.len != Ed25519.Signature.encoded_length) return error.CertificateSignatureInvalid;
    const sig = Ed25519.Signature.fromBytes(encoded_sig[0..Ed25519.Signature.encoded_length].*);
    if (encoded_pub_key.len != Ed25519.PublicKey.encoded_length) return error.CertificateSignatureInvalid;
    const pub_key = Ed25519.PublicKey.fromBytes(encoded_pub_key[0..Ed25519.PublicKey.encoded_length].*) catch |err| switch (err) {
        error.NonCanonical => return error.CertificateSignatureInvalid,
    };
    sig.verify(message, pub_key) catch |err| switch (err) {
        error.IdentityElement => return error.CertificateSignatureInvalid,
        error.NonCanonical => return error.CertificateSignatureInvalid,
        error.SignatureVerificationFailed => return error.CertificateSignatureInvalid,
        error.InvalidEncoding => return error.CertificateSignatureInvalid,
        error.WeakPublicKey => return error.CertificateSignatureInvalid,
    };
}

const builtin = @import("builtin");
const std = @import("../std.zig");
const crypto = std.crypto;
const mem = std.mem;
const Certificate = @This();

pub const der = struct {
    pub const Class = enum(u2) {
        universal,
        application,
        context_specific,
        private,
    };

    pub const PC = enum(u1) {
        primitive,
        constructed,
    };

    pub const Identifier = packed struct(u8) {
        tag: Tag,
        pc: PC,
        class: Class,
    };

    pub const Tag = enum(u5) {
        boolean = 1,
        integer = 2,
        bitstring = 3,
        octetstring = 4,
        null = 5,
        object_identifier = 6,
        sequence = 16,
        sequence_of = 17,
        utc_time = 23,
        generalized_time = 24,
        _,
    };

    pub const Element = struct {
        identifier: Identifier,
        slice: Slice,

        pub const Slice = struct {
            start: u32,
            end: u32,

            pub const empty: Slice = .{ .start = 0, .end = 0 };
        };

        pub const ParseError = error{CertificateFieldHasInvalidLength};

        pub fn parse(bytes: []const u8, index: u32) Element.ParseError!Element {
            var i = index;
            const identifier: Identifier = @bitCast(bytes[i]);
            i += 1;
            const size_byte = bytes[i];
            i += 1;
            if ((size_byte >> 7) == 0) {
                return .{
                    .identifier = identifier,
                    .slice = .{
                        .start = i,
                        .end = i + size_byte,
                    },
                };
            }

            const len_size: u7 = @truncate(size_byte);
            if (len_size > @sizeOf(u32)) {
                return error.CertificateFieldHasInvalidLength;
            }

            const end_i = i + len_size;
            var long_form_size: u32 = 0;
            while (i < end_i) : (i += 1) {
                long_form_size = (long_form_size << 8) | bytes[i];
            }

            return .{
                .identifier = identifier,
                .slice = .{
                    .start = i,
                    .end = i + long_form_size,
                },
            };
        }
    };
};

test {
    _ = Bundle;
}

pub const rsa = struct {
    const max_modulus_bits = 4096;
    const Uint = std.crypto.ff.Uint(max_modulus_bits);
    const Modulus = std.crypto.ff.Modulus(max_modulus_bits);
    const Fe = Modulus.Fe;

    /// RFC 3447 8.1 RSASSA-PSS
    pub const PSSSignature = struct {
        pub fn fromBytes(comptime modulus_len: usize, msg: []const u8) [modulus_len]u8 {
            var result: [modulus_len]u8 = undefined;
            @memcpy(result[0..msg.len], msg);
            @memset(result[msg.len..], 0);
            return result;
        }

        pub const VerifyError = EncryptError || error{InvalidSignature};

        pub fn verify(
            comptime modulus_len: usize,
            sig: [modulus_len]u8,
            msg: []const u8,
            public_key: PublicKey,
            comptime Hash: type,
        ) VerifyError!void {
            try concatVerify(modulus_len, sig, &.{msg}, public_key, Hash);
        }

        pub fn concatVerify(
            comptime modulus_len: usize,
            sig: [modulus_len]u8,
            msg: []const []const u8,
            public_key: PublicKey,
            comptime Hash: type,
        ) VerifyError!void {
            const mod_bits = public_key.n.bits();
            const em_dec = try encrypt(modulus_len, sig, public_key);

            try EMSA_PSS_VERIFY(msg, &em_dec, mod_bits - 1, Hash.digest_length, Hash);
        }

        fn EMSA_PSS_VERIFY(msg: []const []const u8, em: []const u8, emBit: usize, sLen: usize, comptime Hash: type) VerifyError!void {
            // 1.   If the length of M is greater than the input limitation for
            //      the hash function (2^61 - 1 octets for SHA-1), output
            //      "inconsistent" and stop.
            // All the cryptographic hash functions in the standard library have a limit of >= 2^61 - 1.
            // Even then, this check is only there for paranoia. In the context of TLS certificates, emBit cannot exceed 4096.
            if (emBit >= 1 << 61) return error.InvalidSignature;

            // emLen = \ceil(emBits/8)
            const emLen = ((emBit - 1) / 8) + 1;
            std.debug.assert(emLen == em.len);

            // 2.   Let mHash = Hash(M), an octet string of length hLen.
            var mHash: [Hash.digest_length]u8 = undefined;
            {
                var hasher: Hash = .init(.{});
                for (msg) |part| hasher.update(part);
                hasher.final(&mHash);
            }

            // 3.   If emLen < hLen + sLen + 2, output "inconsistent" and stop.
            if (emLen < Hash.digest_length + sLen + 2) {
                return error.InvalidSignature;
            }

            // 4.   If the rightmost octet of EM does not have hexadecimal value
            //      0xbc, output "inconsistent" and stop.
            if (em[em.len - 1] != 0xbc) {
                return error.InvalidSignature;
            }

            // 5.   Let maskedDB be the leftmost emLen - hLen - 1 octets of EM,
            //      and let H be the next hLen octets.
            const maskedDB = em[0..(emLen - Hash.digest_length - 1)];
            const h = em[(emLen - Hash.digest_length - 1)..(emLen - 1)][0..Hash.digest_length];

            // 6.   If the leftmost 8emLen - emBits bits of the leftmost octet in
            //      maskedDB are not all equal to zero, output "inconsistent" and
            //      stop.
            const zero_bits = emLen * 8 - emBit;
            var mask: u8 = maskedDB[0];
            var i: usize = 0;
            while (i < 8 - zero_bits) : (i += 1) {
                mask = mask >> 1;
            }
            if (mask != 0) {
                return error.InvalidSignature;
            }

            // 7.   Let dbMask = MGF(H, emLen - hLen - 1).
            const mgf_len = emLen - Hash.digest_length - 1;
            var mgf_out_buf: [512]u8 = undefined;
            if (mgf_len > mgf_out_buf.len) { // Modulus > 4096 bits
                return error.InvalidSignature;
            }
            const mgf_out = mgf_out_buf[0 .. ((mgf_len - 1) / Hash.digest_length + 1) * Hash.digest_length];
            var dbMask = try MGF1(Hash, mgf_out, h, mgf_len);

            // 8.   Let DB = maskedDB \xor dbMask.
            i = 0;
            while (i < dbMask.len) : (i += 1) {
                dbMask[i] = maskedDB[i] ^ dbMask[i];
            }

            // 9.   Set the leftmost 8emLen - emBits bits of the leftmost octet
            //      in DB to zero.
            i = 0;
            mask = 0;
            while (i < 8 - zero_bits) : (i += 1) {
                mask = mask << 1;
                mask += 1;
            }
            dbMask[0] = dbMask[0] & mask;

            // 10.  If the emLen - hLen - sLen - 2 leftmost octets of DB are not
            //      zero or if the octet at position emLen - hLen - sLen - 1 (the
            //      leftmost position is "position 1") does not have hexadecimal
            //      value 0x01, output "inconsistent" and stop.
            if (dbMask[mgf_len - sLen - 2] != 0x00) {
                return error.InvalidSignature;
            }

            if (dbMask[mgf_len - sLen - 1] != 0x01) {
                return error.InvalidSignature;
            }

            // 11.  Let salt be the last sLen octets of DB.
            const salt = dbMask[(mgf_len - sLen)..];

            // 12.  Let
            //         M' = (0x)00 00 00 00 00 00 00 00 || mHash || salt ;
            //      M' is an octet string of length 8 + hLen + sLen with eight
            //      initial zero octets.
            if (sLen > Hash.digest_length) { // A seed larger than the hash length would be useless
                return error.InvalidSignature;
            }
            var m_p_buf: [8 + Hash.digest_length + Hash.digest_length]u8 = undefined;
            var m_p = m_p_buf[0 .. 8 + Hash.digest_length + sLen];
            std.mem.copyForwards(u8, m_p, &([_]u8{0} ** 8));
            std.mem.copyForwards(u8, m_p[8..], &mHash);
            std.mem.copyForwards(u8, m_p[(8 + Hash.digest_length)..], salt);

            // 13.  Let H' = Hash(M'), an octet string of length hLen.
            var h_p: [Hash.digest_length]u8 = undefined;
            Hash.hash(m_p, &h_p, .{});

            // 14.  If H = H', output "consistent".  Otherwise, output
            //      "inconsistent".
            if (!std.mem.eql(u8, h, &h_p)) {
                return error.InvalidSignature;
            }
        }

        fn MGF1(comptime Hash: type, out: []u8, seed: *const [Hash.digest_length]u8, len: usize) ![]u8 {
            var counter: u32 = 0;
            var idx: usize = 0;
            var hash = seed.* ++ @as([4]u8, undefined);

            while (idx < len) {
                std.mem.writeInt(u32, hash[seed.len..][0..4], counter, .big);
                Hash.hash(&hash, out[idx..][0..Hash.digest_length], .{});
                idx += Hash.digest_length;
                counter += 1;
            }

            return out[0..len];
        }
    };

    /// RFC 3447 8.2 RSASSA-PKCS1-v1_5
    pub const PKCS1v1_5Signature = struct {
        pub fn fromBytes(comptime modulus_len: usize, msg: []const u8) [modulus_len]u8 {
            var result: [modulus_len]u8 = undefined;
            @memcpy(result[0..msg.len], msg);
            @memset(result[msg.len..], 0);
            return result;
        }

        pub const VerifyError = EncryptError || error{InvalidSignature};

        pub fn verify(
            comptime modulus_len: usize,
            sig: [modulus_len]u8,
            msg: []const u8,
            public_key: PublicKey,
            comptime Hash: type,
        ) VerifyError!void {
            try concatVerify(modulus_len, sig, &.{msg}, public_key, Hash);
        }

        pub fn concatVerify(
            comptime modulus_len: usize,
            sig: [modulus_len]u8,
            msg: []const []const u8,
            public_key: PublicKey,
            comptime Hash: type,
        ) VerifyError!void {
            const em_dec = try encrypt(modulus_len, sig, public_key);
            const em = try EMSA_PKCS1_V1_5_ENCODE(msg, modulus_len, Hash);
            if (!std.mem.eql(u8, &em_dec, &em)) return error.InvalidSignature;
        }

        fn EMSA_PKCS1_V1_5_ENCODE(msg: []const []const u8, comptime emLen: usize, comptime Hash: type) VerifyError![emLen]u8 {
            comptime var em_index = emLen;
            var em: [emLen]u8 = undefined;

            // 1. Apply the hash function to the message M to produce a hash value
            //    H:
            //
            //       H = Hash(M).
            //
            //    If the hash function outputs "message too long," output "message
            //    too long" and stop.
            var hasher: Hash = .init(.{});
            for (msg) |part| hasher.update(part);
            em_index -= Hash.digest_length;
            hasher.final(em[em_index..]);

            // 2. Encode the algorithm ID for the hash function and the hash value
            //    into an ASN.1 value of type DigestInfo (see Appendix A.2.4) with
            //    the Distinguished Encoding Rules (DER), where the type DigestInfo
            //    has the syntax
            //
            //    DigestInfo ::= SEQUENCE {
            //        digestAlgorithm AlgorithmIdentifier,
            //        digest OCTET STRING
            //    }
            //
            //    The first field identifies the hash function and the second
            //    contains the hash value.  Let T be the DER encoding of the
            //    DigestInfo value (see the notes below) and let tLen be the length
            //    in octets of T.
            const hash_der: []const u8 = &switch (Hash) {
                crypto.hash.Sha1 => .{
                    0x30, 0x21, 0x30, 0x09, 0x06, 0x05, 0x2b, 0x0e,
                    0x03, 0x02, 0x1a, 0x05, 0x00, 0x04, 0x14,
                },
                crypto.hash.sha2.Sha224 => .{
                    0x30, 0x2d, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86,
                    0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x04, 0x05,
                    0x00, 0x04, 0x1c,
                },
                crypto.hash.sha2.Sha256 => .{
                    0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86,
                    0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05,
                    0x00, 0x04, 0x20,
                },
   
```
