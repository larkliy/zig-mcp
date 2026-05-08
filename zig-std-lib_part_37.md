```
 "KT256 sequential and parallel produce same output with customization" {
    if (true) {
        // https://codeberg.org/ziglang/zig/issues/30676
        return error.SkipZigTest;
    }

    const allocator = std.testing.allocator;
    const io = std.testing.io;

    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    const input_size = 5 * 512 * 1024; // 2.5MB
    const input = try allocator.alloc(u8, input_size);
    defer allocator.free(input);

    // Fill with random data
    random.bytes(input);

    const customization = "test domain";
    var output_seq: [80]u8 = undefined;
    var output_par: [80]u8 = undefined;

    // Hash with sequential method
    try KT256.hash(input, &output_seq, .{ .customization = customization });

    // Hash with parallel method
    try KT256.hashParallel(input, &output_par, .{ .customization = customization }, allocator, io);

    // Verify outputs match
    try std.testing.expectEqualSlices(u8, &output_seq, &output_par);
}

/// Helper: Generate pattern data where data[i] = (i % 251)
fn generatePattern(allocator: Allocator, len: usize) ![]u8 {
    const data = try allocator.alloc(u8, len);
    for (data, 0..) |*byte, i| {
        byte.* = @intCast(i % 251);
    }
    return data;
}

test "KT128: empty message, empty customization, 32 bytes" {
    var output: [32]u8 = undefined;
    try KT128.hash(&[_]u8{}, &output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "1AC2D450FC3B4205D19DA7BFCA1B37513C0803577AC7167F06FE2CE1F0EF39E5");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: empty message, empty customization, 64 bytes" {
    var output: [64]u8 = undefined;
    try KT128.hash(&[_]u8{}, &output, .{});

    var expected: [64]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "1AC2D450FC3B4205D19DA7BFCA1B37513C0803577AC7167F06FE2CE1F0EF39E54269C056B8C82E48276038B6D292966CC07A3D4645272E31FF38508139EB0A71");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: empty message, empty customization, 10032 bytes (last 32)" {
    const allocator = std.testing.allocator;
    const output = try allocator.alloc(u8, 10032);
    defer allocator.free(output);

    try KT128.hash(&[_]u8{}, output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "E8DC563642F7228C84684C898405D3A834799158C079B12880277A1D28E2FF6D");
    try std.testing.expectEqualSlices(u8, &expected, output[10000..]);
}

test "KT128: pattern message (1 byte), empty customization, 32 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 1);
    defer allocator.free(message);

    var output: [32]u8 = undefined;
    try KT128.hash(message, &output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "2BDA92450E8B147F8A7CB629E784A058EFCA7CF7D8218E02D345DFAA65244A1F");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: pattern message (17 bytes), empty customization, 32 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 17);
    defer allocator.free(message);

    var output: [32]u8 = undefined;
    try KT128.hash(message, &output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "6BF75FA2239198DB4772E36478F8E19B0F371205F6A9A93A273F51DF37122888");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: pattern message (289 bytes), empty customization, 32 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 289);
    defer allocator.free(message);

    var output: [32]u8 = undefined;
    try KT128.hash(message, &output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "0C315EBCDEDBF61426DE7DCF8FB725D1E74675D7F5327A5067F367B108ECB67C");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: 0xFF message (1 byte), pattern customization (1 byte), 32 bytes" {
    const allocator = std.testing.allocator;
    const customization = try generatePattern(allocator, 1);
    defer allocator.free(customization);

    const message = [_]u8{0xFF};
    var output: [32]u8 = undefined;
    try KT128.hash(&message, &output, .{ .customization = customization });

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "A20B92B251E3D62443EC286E4B9B470A4E8315C156EEB24878B038ABE20650BE");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: pattern message (8191 bytes), empty customization, 32 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 8191);
    defer allocator.free(message);

    var output: [32]u8 = undefined;
    try KT128.hash(message, &output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "1B577636F723643E990CC7D6A659837436FD6A103626600EB8301CD1DBE553D6");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: pattern message (8192 bytes), empty customization, 32 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 8192);
    defer allocator.free(message);

    var output: [32]u8 = undefined;
    try KT128.hash(message, &output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "48F256F6772F9EDFB6A8B661EC92DC93B95EBD05A08A17B39AE3490870C926C3");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT256: empty message, empty customization, 64 bytes" {
    var output: [64]u8 = undefined;
    try KT256.hash(&[_]u8{}, &output, .{});

    var expected: [64]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "B23D2E9CEA9F4904E02BEC06817FC10CE38CE8E93EF4C89E6537076AF8646404E3E8B68107B8833A5D30490AA33482353FD4ADC7148ECB782855003AAEBDE4A9");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT256: empty message, empty customization, 128 bytes" {
    var output: [128]u8 = undefined;
    try KT256.hash(&[_]u8{}, &output, .{});

    var expected: [128]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "B23D2E9CEA9F4904E02BEC06817FC10CE38CE8E93EF4C89E6537076AF8646404E3E8B68107B8833A5D30490AA33482353FD4ADC7148ECB782855003AAEBDE4A9B0925319D8EA1E121A609821EC19EFEA89E6D08DAEE1662B69C840289F188BA860F55760B61F82114C030C97E5178449608CCD2CD2D919FC7829FF69931AC4D0");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT256: pattern message (1 byte), empty customization, 64 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 1);
    defer allocator.free(message);

    var output: [64]u8 = undefined;
    try KT256.hash(message, &output, .{});

    var expected: [64]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "0D005A194085360217128CF17F91E1F71314EFA5564539D444912E3437EFA17F82DB6F6FFE76E781EAA068BCE01F2BBF81EACB983D7230F2FB02834A21B1DDD0");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT256: pattern message (17 bytes), empty customization, 64 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 17);
    defer allocator.free(message);

    var output: [64]u8 = undefined;
    try KT256.hash(message, &output, .{});

    var expected: [64]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "1BA3C02B1FC514474F06C8979978A9056C8483F4A1B63D0DCCEFE3A28A2F323E1CDCCA40EBF006AC76EF0397152346837B1277D3E7FAA9C9653B19075098527B");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT256: pattern message (8191 bytes), empty customization, 64 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 8191);
    defer allocator.free(message);

    var output: [64]u8 = undefined;
    try KT256.hash(message, &output, .{});

    var expected: [64]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "3081434D93A4108D8D8A3305B89682CEBEDC7CA4EA8A3CE869FBB73CBE4A58EEF6F24DE38FFC170514C70E7AB2D01F03812616E863D769AFB3753193BA045B20");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT256: pattern message (8192 bytes), empty customization, 64 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 8192);
    defer allocator.free(message);

    var output: [64]u8 = undefined;
    try KT256.hash(message, &output, .{});

    var expected: [64]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "C6EE8E2AD3200C018AC87AAA031CDAC22121B412D07DC6E0DCCBB53423747E9A1C18834D99DF596CF0CF4B8DFAFB7BF02D139D0C9035725ADC1A01B7230A41FA");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: pattern message (8193 bytes), empty customization, 32 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 8193);
    defer allocator.free(message);

    var output: [32]u8 = undefined;
    try KT128.hash(message, &output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "BB66FE72EAEA5179418D5295EE1344854D8AD7F3FA17EFCB467EC152341284CF");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: pattern message (16384 bytes), empty customization, 32 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 16384);
    defer allocator.free(message);

    var output: [32]u8 = undefined;
    try KT128.hash(message, &output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "82778F7F7234C83352E76837B721FBDBB5270B88010D84FA5AB0B61EC8CE0956");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128: pattern message (16385 bytes), empty customization, 32 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 16385);
    defer allocator.free(message);

    var output: [32]u8 = undefined;
    try KT128.hash(message, &output, .{});

    var expected: [32]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "5F8D2B943922B451842B4E82740D02369E2D5F9F33C5123509A53B955FE177B2");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT256: pattern message (8193 bytes), empty customization, 64 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 8193);
    defer allocator.free(message);

    var output: [64]u8 = undefined;
    try KT256.hash(message, &output, .{});

    var expected: [64]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "65FF03335900E5197ACBD5F41B797F0E7E36AD4FF7D89C09FA6F28AE58D1E8BC2DF1779B86F988C3B13690172914EA172423B23EF4057255BB0836AB3A99836E");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT256: pattern message (16384 bytes), empty customization, 64 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 16384);
    defer allocator.free(message);

    var output: [64]u8 = undefined;
    try KT256.hash(message, &output, .{});

    var expected: [64]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "74604239A14847CB79069B4FF0E51070A93034C9AC4DFF4D45E0F2C5DA81D930DE6055C2134B4DF4E49F27D1B2C66E95491858B182A924BD0504DA5976BC516D");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT256: pattern message (16385 bytes), empty customization, 64 bytes" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 16385);
    defer allocator.free(message);

    var output: [64]u8 = undefined;
    try KT256.hash(message, &output, .{});

    var expected: [64]u8 = undefined;
    _ = try std.fmt.hexToBytes(&expected, "C814F23132DADBFD55379F18CB988CB39B751F119322823FD982644A897485397B9F40EB11C6E416359B8AE695A5CE0FA79D1ADA1EEC745D82E0A5AB08A9F014");
    try std.testing.expectEqualSlices(u8, &expected, &output);
}

test "KT128 incremental: empty message matches one-shot" {
    var output_oneshot: [32]u8 = undefined;
    var output_incremental: [32]u8 = undefined;

    try KT128.hash(&[_]u8{}, &output_oneshot, .{});

    var hasher = KT128.init(.{});
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT128 incremental: small message matches one-shot" {
    const message = "Hello, KangarooTwelve!";

    var output_oneshot: [32]u8 = undefined;
    var output_incremental: [32]u8 = undefined;

    try KT128.hash(message, &output_oneshot, .{});

    var hasher = KT128.init(.{});
    hasher.update(message);
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT128 incremental: multiple updates match single update" {
    const part1 = "Hello, ";
    const part2 = "Kangaroo";
    const part3 = "Twelve!";

    var output_single: [32]u8 = undefined;
    var output_multi: [32]u8 = undefined;

    // Single update
    var hasher1 = KT128.init(.{});
    hasher1.update(part1 ++ part2 ++ part3);
    hasher1.final(&output_single);

    // Multiple updates
    var hasher2 = KT128.init(.{});
    hasher2.update(part1);
    hasher2.update(part2);
    hasher2.update(part3);
    hasher2.final(&output_multi);

    try std.testing.expectEqualSlices(u8, &output_single, &output_multi);
}

test "KT128 incremental: exactly chunk_size matches one-shot" {
    const allocator = std.testing.allocator;
    const message = try allocator.alloc(u8, 8192);
    defer allocator.free(message);
    @memset(message, 0xAB);

    var output_oneshot: [32]u8 = undefined;
    var output_incremental: [32]u8 = undefined;

    try KT128.hash(message, &output_oneshot, .{});

    var hasher = KT128.init(.{});
    hasher.update(message);
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT128 incremental: larger than chunk_size matches one-shot" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 16384);
    defer allocator.free(message);

    var output_oneshot: [32]u8 = undefined;
    var output_incremental: [32]u8 = undefined;

    try KT128.hash(message, &output_oneshot, .{});

    var hasher = KT128.init(.{});
    hasher.update(message);
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT128 incremental: with customization matches one-shot" {
    const message = "Test message";
    const customization = "my custom domain";

    var output_oneshot: [32]u8 = undefined;
    var output_incremental: [32]u8 = undefined;

    try KT128.hash(message, &output_oneshot, .{ .customization = customization });

    var hasher = KT128.init(.{ .customization = customization });
    hasher.update(message);
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT128 incremental: large message with customization" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 20000);
    defer allocator.free(message);
    const customization = "test domain";

    var output_oneshot: [48]u8 = undefined;
    var output_incremental: [48]u8 = undefined;

    try KT128.hash(message, &output_oneshot, .{ .customization = customization });

    var hasher = KT128.init(.{ .customization = customization });
    hasher.update(message);
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT128 incremental: streaming chunks matches one-shot" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 25000);
    defer allocator.free(message);

    var output_oneshot: [32]u8 = undefined;
    var output_incremental: [32]u8 = undefined;

    try KT128.hash(message, &output_oneshot, .{});

    var hasher = KT128.init(.{});

    // Feed in 1KB chunks
    var offset: usize = 0;
    while (offset < message.len) {
        const chunk_size_local = @min(1024, message.len - offset);
        hasher.update(message[offset..][0..chunk_size_local]);
        offset += chunk_size_local;
    }
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT256 incremental: empty message matches one-shot" {
    var output_oneshot: [64]u8 = undefined;
    var output_incremental: [64]u8 = undefined;

    try KT256.hash(&[_]u8{}, &output_oneshot, .{});

    var hasher = KT256.init(.{});
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT256 incremental: small message matches one-shot" {
    const message = "Hello, KangarooTwelve with 256-bit security!";

    var output_oneshot: [64]u8 = undefined;
    var output_incremental: [64]u8 = undefined;

    try KT256.hash(message, &output_oneshot, .{});

    var hasher = KT256.init(.{});
    hasher.update(message);
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT256 incremental: large message matches one-shot" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 30000);
    defer allocator.free(message);

    var output_oneshot: [64]u8 = undefined;
    var output_incremental: [64]u8 = undefined;

    try KT256.hash(message, &output_oneshot, .{});

    var hasher = KT256.init(.{});
    hasher.update(message);
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT256 incremental: with customization matches one-shot" {
    const allocator = std.testing.allocator;
    const message = try generatePattern(allocator, 15000);
    defer allocator.free(message);
    const customization = "KT256 custom domain";

    var output_oneshot: [80]u8 = undefined;
    var output_incremental: [80]u8 = undefined;

    try KT256.hash(message, &output_oneshot, .{ .customization = customization });

    var hasher = KT256.init(.{ .customization = customization });
    hasher.update(message);
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT128 incremental: random small message with random chunk sizes" {
    const allocator = std.testing.allocator;

    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    const test_sizes = [_]usize{ 100, 500, 2000, 5000, 10000 };

    for (test_sizes) |total_size| {
        const message = try allocator.alloc(u8, total_size);
        defer allocator.free(message);
        random.bytes(message);

        var output_oneshot: [32]u8 = undefined;
        var output_incremental: [32]u8 = undefined;

        try KT128.hash(message, &output_oneshot, .{});

        var hasher = KT128.init(.{});
        var offset: usize = 0;

        while (offset < message.len) {
            const remaining = message.len - offset;
            const max_chunk = @min(1000, remaining);
            const chunk_size_local = if (max_chunk == 1) 1 else random.intRangeAtMost(usize, 1, max_chunk);

            hasher.update(message[offset..][0..chunk_size_local]);
            offset += chunk_size_local;
        }
        hasher.final(&output_incremental);

        try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
    }
}

test "KT128 incremental: random large message (1MB) with random chunk sizes" {
    const allocator = std.testing.allocator;

    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    const total_size: usize = 1024 * 1024; // 1 MB
    const message = try allocator.alloc(u8, total_size);
    defer allocator.free(message);
    random.bytes(message);

    var output_oneshot: [32]u8 = undefined;
    var output_incremental: [32]u8 = undefined;

    try KT128.hash(message, &output_oneshot, .{});

    var hasher = KT128.init(.{});
    var offset: usize = 0;

    while (offset < message.len) {
        const remaining = message.len - offset;
        const max_chunk = @min(10000, remaining);
        const chunk_size_local = if (max_chunk == 1) 1 else random.intRangeAtMost(usize, 1, max_chunk);

        hasher.update(message[offset..][0..chunk_size_local]);
        offset += chunk_size_local;
    }
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT256 incremental: random small message with random chunk sizes" {
    const allocator = std.testing.allocator;

    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    const test_sizes = [_]usize{ 100, 500, 2000, 5000, 10000 };

    for (test_sizes) |total_size| {
        // Generate random message
        const message = try allocator.alloc(u8, total_size);
        defer allocator.free(message);
        random.bytes(message);

        var output_oneshot: [64]u8 = undefined;
        var output_incremental: [64]u8 = undefined;

        try KT256.hash(message, &output_oneshot, .{});

        var hasher = KT256.init(.{});
        var offset: usize = 0;

        while (offset < message.len) {
            const remaining = message.len - offset;
            const max_chunk = @min(1000, remaining);
            const chunk_size_local = if (max_chunk == 1) 1 else random.intRangeAtMost(usize, 1, max_chunk);

            hasher.update(message[offset..][0..chunk_size_local]);
            offset += chunk_size_local;
        }
        hasher.final(&output_incremental);

        try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
    }
}

test "KT256 incremental: random large message (1MB) with random chunk sizes" {
    const allocator = std.testing.allocator;

    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    const total_size: usize = 1024 * 1024; // 1 MB
    const message = try allocator.alloc(u8, total_size);
    defer allocator.free(message);
    random.bytes(message);

    var output_oneshot: [64]u8 = undefined;
    var output_incremental: [64]u8 = undefined;

    try KT256.hash(message, &output_oneshot, .{});

    var hasher = KT256.init(.{});
    var offset: usize = 0;

    while (offset < message.len) {
        const remaining = message.len - offset;
        const max_chunk = @min(10000, remaining);
        const chunk_size_local = if (max_chunk == 1) 1 else random.intRangeAtMost(usize, 1, max_chunk);

        hasher.update(message[offset..][0..chunk_size_local]);
        offset += chunk_size_local;
    }
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}

test "KT128 incremental: random message with customization and random chunks" {
    const allocator = std.testing.allocator;

    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    const total_size: usize = 50000;
    const message = try allocator.alloc(u8, total_size);
    defer allocator.free(message);
    random.bytes(message);

    const customization = "random test domain";

    var output_oneshot: [48]u8 = undefined;
    var output_incremental: [48]u8 = undefined;

    try KT128.hash(message, &output_oneshot, .{ .customization = customization });

    var hasher = KT128.init(.{ .customization = customization });
    var offset: usize = 0;

    while (offset < message.len) {
        const remaining = message.len - offset;
        const max_chunk = @min(5000, remaining);
        const chunk_size_local = if (max_chunk == 1) 1 else random.intRangeAtMost(usize, 1, max_chunk);

        hasher.update(message[offset..][0..chunk_size_local]);
        offset += chunk_size_local;
    }
    hasher.final(&output_incremental);

    try std.testing.expectEqualSlices(u8, &output_oneshot, &output_incremental);
}



---
File: /std/crypto/keccak_p.zig
---

const std = @import("std");
const builtin = @import("builtin");
const assert = std.debug.assert;
const math = std.math;
const mem = std.mem;
const native_endian = builtin.cpu.arch.endian();
const mode = @import("builtin").mode;

/// The Keccak-f permutation.
pub fn KeccakF(comptime f: u11) type {
    comptime assert(f >= 200 and f <= 1600 and f % 200 == 0); // invalid bit size
    const T = std.meta.Int(.unsigned, f / 25);
    const Block = [25]T;

    const PI = [_]u5{
        10, 7, 11, 17, 18, 3, 5, 16, 8, 21, 24, 4, 15, 23, 19, 13, 12, 2, 20, 14, 22, 9, 6, 1,
    };

    return struct {
        const Self = @This();

        /// Number of bytes in the state.
        pub const block_bytes = f / 8;

        /// Maximum number of rounds for the given f parameter.
        pub const max_rounds = 12 + 2 * math.log2(f / 25);

        // Round constants
        const RC = rc: {
            const RC64 = [_]u64{
                0x0000000000000001, 0x0000000000008082, 0x800000000000808a, 0x8000000080008000,
                0x000000000000808b, 0x0000000080000001, 0x8000000080008081, 0x8000000000008009,
                0x000000000000008a, 0x0000000000000088, 0x0000000080008009, 0x000000008000000a,
                0x000000008000808b, 0x800000000000008b, 0x8000000000008089, 0x8000000000008003,
                0x8000000000008002, 0x8000000000000080, 0x000000000000800a, 0x800000008000000a,
                0x8000000080008081, 0x8000000000008080, 0x0000000080000001, 0x8000000080008008,
            };
            var rc: [max_rounds]T = undefined;
            for (&rc, RC64[0..max_rounds]) |*t, c| t.* = @as(T, @truncate(c));
            break :rc rc;
        };

        st: Block = [_]T{0} ** 25,

        /// Initialize the state from a slice of bytes.
        pub fn init(bytes: [block_bytes]u8) Self {
            var self: Self = undefined;
            inline for (&self.st, 0..) |*r, i| {
                r.* = mem.readInt(T, bytes[@sizeOf(T) * i ..][0..@sizeOf(T)], .little);
            }
            return self;
        }

        /// A representation of the state as bytes. The byte order is architecture-dependent.
        pub fn asBytes(self: *Self) *[block_bytes]u8 {
            return mem.asBytes(&self.st);
        }

        /// Byte-swap the entire state if the architecture doesn't match the required endianness.
        pub fn endianSwap(self: *Self) void {
            for (&self.st) |*w| {
                w.* = mem.littleToNative(T, w.*);
            }
        }

        /// Set bytes starting at the beginning of the state.
        pub fn setBytes(self: *Self, bytes: []const u8) void {
            var i: usize = 0;
            while (i + @sizeOf(T) <= bytes.len) : (i += @sizeOf(T)) {
                self.st[i / @sizeOf(T)] = mem.readInt(T, bytes[i..][0..@sizeOf(T)], .little);
            }
            if (i < bytes.len) {
                var padded = [_]u8{0} ** @sizeOf(T);
                @memcpy(padded[0 .. bytes.len - i], bytes[i..]);
                self.st[i / @sizeOf(T)] = mem.readInt(T, padded[0..], .little);
            }
        }

        /// XOR a byte into the state at a given offset.
        pub fn addByte(self: *Self, byte: u8, offset: usize) void {
            const z = @sizeOf(T) * @as(math.Log2Int(T), @truncate(offset % @sizeOf(T)));
            self.st[offset / @sizeOf(T)] ^= @as(T, byte) << z;
        }

        /// XOR bytes into the beginning of the state.
        pub fn addBytes(self: *Self, bytes: []const u8) void {
            var i: usize = 0;
            while (i + @sizeOf(T) <= bytes.len) : (i += @sizeOf(T)) {
                self.st[i / @sizeOf(T)] ^= mem.readInt(T, bytes[i..][0..@sizeOf(T)], .little);
            }
            if (i < bytes.len) {
                var padded = [_]u8{0} ** @sizeOf(T);
                @memcpy(padded[0 .. bytes.len - i], bytes[i..]);
                self.st[i / @sizeOf(T)] ^= mem.readInt(T, padded[0..], .little);
            }
        }

        /// Extract the first bytes of the state.
        pub fn extractBytes(self: *Self, out: []u8) void {
            var i: usize = 0;
            while (i + @sizeOf(T) <= out.len) : (i += @sizeOf(T)) {
                mem.writeInt(T, out[i..][0..@sizeOf(T)], self.st[i / @sizeOf(T)], .little);
            }
            if (i < out.len) {
                var padded = [_]u8{0} ** @sizeOf(T);
                mem.writeInt(T, padded[0..], self.st[i / @sizeOf(T)], .little);
                @memcpy(out[i..], padded[0 .. out.len - i]);
            }
        }

        /// XOR the first bytes of the state into a slice of bytes.
        pub fn xorBytes(self: *Self, out: []u8, in: []const u8) void {
            assert(out.len == in.len);

            var i: usize = 0;
            while (i + @sizeOf(T) <= in.len) : (i += @sizeOf(T)) {
                const x = mem.readInt(T, in[i..][0..@sizeOf(T)], native_endian) ^ mem.nativeToLittle(T, self.st[i / @sizeOf(T)]);
                mem.writeInt(T, out[i..][0..@sizeOf(T)], x, native_endian);
            }
            if (i < in.len) {
                var padded = [_]u8{0} ** @sizeOf(T);
                @memcpy(padded[0 .. in.len - i], in[i..]);
                const x = mem.readInt(T, &padded, native_endian) ^ mem.nativeToLittle(T, self.st[i / @sizeOf(T)]);
                mem.writeInt(T, &padded, x, native_endian);
                @memcpy(out[i..], padded[0 .. in.len - i]);
            }
        }

        /// Set the words storing the bytes of a given range to zero.
        pub fn clear(self: *Self, from: usize, to: usize) void {
            @memset(self.st[from / @sizeOf(T) .. (to + @sizeOf(T) - 1) / @sizeOf(T)], 0);
        }

        /// Clear the entire state, disabling compiler optimizations.
        pub fn secureZero(self: *Self) void {
            std.crypto.secureZero(T, &self.st);
        }

        inline fn round(self: *Self, rc: T) void {
            const st = &self.st;

            // theta
            var t = [_]T{0} ** 5;
            inline for (0..5) |i| {
                inline for (0..5) |j| {
                    t[i] ^= st[j * 5 + i];
                }
            }
            inline for (0..5) |i| {
                inline for (0..5) |j| {
                    st[j * 5 + i] ^= t[(i + 4) % 5] ^ math.rotl(T, t[(i + 1) % 5], 1);
                }
            }

            // rho+pi
            var last = st[1];
            comptime var rotc = 0;
            inline for (0..24) |i| {
                const x = PI[i];
                const tmp = st[x];
                rotc = (rotc + i + 1) % @bitSizeOf(T);
                st[x] = math.rotl(T, last, rotc);
                last = tmp;
            }
            inline for (0..5) |i| {
                inline for (0..5) |j| {
                    t[j] = st[i * 5 + j];
                }
                inline for (0..5) |j| {
                    st[i * 5 + j] = t[j] ^ (~t[(j + 1) % 5] & t[(j + 2) % 5]);
                }
            }

            // iota
            st[0] ^= rc;
        }

        /// Apply a (possibly) reduced-round permutation to the state.
        pub fn permuteR(self: *Self, comptime rounds: u5) void {
            var i = RC.len - rounds;
            while (i < RC.len - RC.len % 3) : (i += 3) {
                self.round(RC[i]);
                self.round(RC[i + 1]);
                self.round(RC[i + 2]);
            }
            while (i < RC.len) : (i += 1) {
                self.round(RC[i]);
            }
        }

        /// Apply a full-round permutation to the state.
        pub fn permute(self: *Self) void {
            self.permuteR(max_rounds);
        }
    };
}

/// A generic Keccak-P state.
pub fn State(comptime f: u11, comptime capacity: u11, comptime rounds: u5) type {
    comptime assert(f >= 200 and f <= 1600 and f % 200 == 0); // invalid state size
    comptime assert(capacity < f and capacity % 8 == 0); // invalid capacity size

    // In debug mode, track transitions to prevent insecure ones.
    const Op = enum { uninitialized, initialized, updated, absorb, squeeze };
    const TransitionTracker = if (mode == .Debug) struct {
        op: Op = .uninitialized,

        fn to(tracker: *@This(), next_op: Op) void {
            switch (next_op) {
                .updated => {
                    switch (tracker.op) {
                        .uninitialized => @panic("cannot permute before initializing"),
                        else => {},
                    }
                },
                .absorb => {
                    switch (tracker.op) {
                        .squeeze => @panic("cannot absorb right after squeezing"),
                        else => {},
                    }
                },
                .squeeze => {
                    switch (tracker.op) {
                        .uninitialized => @panic("cannot squeeze before initializing"),
                        .initialized => @panic("cannot squeeze right after initializing"),
                        .absorb => @panic("cannot squeeze right after absorbing"),
                        else => {},
                    }
                },
                .uninitialized => @panic("cannot transition to uninitialized"),
                .initialized => {},
            }
            tracker.op = next_op;
        }
    } else struct {
        // No-op in non-debug modes.
        inline fn to(tracker: *@This(), next_op: Op) void {
            _ = tracker; // no-op
            _ = next_op; // no-op
        }
    };

    return struct {
        const Self = @This();

        /// The block length, or rate, in bytes.
        pub const rate = KeccakF(f).block_bytes - capacity / 8;
        /// Keccak does not have any options.
        pub const Options = struct {};

        /// The input delimiter.
        delim: u8,

        offset: usize = 0,
        buf: [rate]u8 = undefined,

        st: KeccakF(f) = .{},

        transition: TransitionTracker = .{},

        /// Absorb a slice of bytes into the sponge.
        pub fn absorb(self: *Self, bytes: []const u8) void {
            self.transition.to(.absorb);
            var i: usize = 0;
            if (self.offset > 0) {
                const left = @min(rate - self.offset, bytes.len);
                @memcpy(self.buf[self.offset..][0..left], bytes[0..left]);
                self.offset += left;
                if (left == bytes.len) return;
                if (self.offset == rate) {
                    self.st.addBytes(self.buf[0..]);
                    self.st.permuteR(rounds);
                    self.offset = 0;
                }
                i = left;
            }
            while (i + rate < bytes.len) : (i += rate) {
                self.st.addBytes(bytes[i..][0..rate]);
                self.st.permuteR(rounds);
            }
            const left = bytes.len - i;
            if (left > 0) {
                @memcpy(self.buf[0..left], bytes[i..][0..left]);
            }
            self.offset = left;
        }

        /// Initialize the state from a slice of bytes.
        pub fn init(bytes: [f / 8]u8, delim: u8) Self {
            var st = Self{ .st = KeccakF(f).init(bytes), .delim = delim };
            st.transition.to(.initialized);
            return st;
        }

        /// Permute the state
        pub fn permute(self: *Self) void {
            if (mode == .Debug) {
                if (self.transition.op == .absorb and self.offset > 0) {
                    @panic("cannot permute with pending input - call fillBlock() or pad() instead");
                }
            }
            self.transition.to(.updated);
            self.st.permuteR(rounds);
            self.offset = 0;
        }

        /// Align the input to the rate boundary and permute.
        pub fn fillBlock(self: *Self) void {
            self.transition.to(.absorb);
            self.st.addBytes(self.buf[0..self.offset]);
            self.st.permuteR(rounds);
            self.offset = 0;
            self.transition.to(.updated);
        }

        /// Mark the end of the input.
        pub fn pad(self: *Self) void {
            self.transition.to(.absorb);
            self.st.addBytes(self.buf[0..self.offset]);
            if (self.offset == rate) {
                self.st.permuteR(rounds);
                self.offset = 0;
            }
            self.st.addByte(self.delim, self.offset);
            self.st.addByte(0x80, rate - 1);
            self.st.permuteR(rounds);
            self.offset = 0;
            self.transition.to(.updated);
        }

        /// Squeeze a slice of bytes from the sponge.
        /// The function can be called multiple times.
        pub fn squeeze(self: *Self, out: []u8) void {
            self.transition.to(.squeeze);
            var i: usize = 0;
            if (self.offset == rate) {
                self.st.permuteR(rounds);
            } else if (self.offset > 0) {
                @branchHint(.unlikely);
                var buf: [rate]u8 = undefined;
                self.st.extractBytes(buf[0..]);
                const left = @min(rate - self.offset, out.len);
                @memcpy(out[0..left], buf[self.offset..][0..left]);
                self.offset += left;
                if (left == out.len) return;
                if (self.offset == rate) {
                    self.offset = 0;
                    self.st.permuteR(rounds);
                }
                i = left;
            }
            while (i + rate < out.len) : (i += rate) {
                self.st.extractBytes(out[i..][0..rate]);
                self.st.permuteR(rounds);
            }
            const left = out.len - i;
            if (left > 0) {
                self.st.extractBytes(out[i..][0..left]);
            }
            self.offset = left;
        }
    };
}

test "Keccak-f800" {
    var st: KeccakF(800) = .{
        .st = .{
            0xE531D45D, 0xF404C6FB, 0x23A0BF99, 0xF1F8452F, 0x51FFD042, 0xE539F578, 0xF00B80A7,
            0xAF973664, 0xBF5AF34C, 0x227A2424, 0x88172715, 0x9F685884, 0xB15CD054, 0x1BF4FC0E,
            0x6166FA91, 0x1A9E599A, 0xA3970A1F, 0xAB659687, 0xAFAB8D68, 0xE74B1015, 0x34001A98,
            0x4119EFF3, 0x930A0E76, 0x87B28070, 0x11EFE996,
        },
    };
    st.permute();
    const expected: [25]u32 = .{
        0x75BF2D0D, 0x9B610E89, 0xC826AF40, 0x64CD84AB, 0xF905BDD6, 0xBC832835, 0x5F8001B9,
        0x15662CCE, 0x8E38C95E, 0x701FE543, 0x1B544380, 0x89ACDEFF, 0x51EDB5DE, 0x0E9702D9,
        0x6C19AA16, 0xA2913EEE, 0x60754E9A, 0x9819063C, 0xF4709254, 0xD09F9084, 0x772DA259,
        0x1DB35DF7, 0x5AA60162, 0x358825D5, 0xB3783BAB,
    };
    try std.testing.expectEqualSlices(u32, &st.st, &expected);
}

test "squeeze" {
    var st = State(800, 256, 22).init([_]u8{0x80} ** 100, 0x01);

    var out0: [15]u8 = undefined;
    var out1: [out0.len]u8 = undefined;
    st.permute();
    var st0 = st;
    st0.squeeze(out0[0..]);
    var st1 = st;
    st1.squeeze(out1[0 .. out1.len / 2]);
    st1.squeeze(out1[out1.len / 2 ..]);
    try std.testing.expectEqualSlices(u8, &out0, &out1);

    var out2: [100]u8 = undefined;
    var out3: [out2.len]u8 = undefined;
    var st2 = st;
    st2.squeeze(out2[0..]);
    var st3 = st;
    st3.squeeze(out3[0 .. out2.len / 2]);
    st3.squeeze(out3[out2.len / 2 ..]);
    try std.testing.expectEqualSlices(u8, &out2, &out3);
}



---
File: /std/crypto/md5.zig
---

const std = @import("../std.zig");
const mem = std.mem;
const math = std.math;

const RoundParam = struct {
    a: usize,
    b: usize,
    c: usize,
    d: usize,
    k: usize,
    s: u32,
    t: u32,
};

fn roundParam(a: usize, b: usize, c: usize, d: usize, k: usize, s: u32, t: u32) RoundParam {
    return RoundParam{
        .a = a,
        .b = b,
        .c = c,
        .d = d,
        .k = k,
        .s = s,
        .t = t,
    };
}

/// The MD5 function is now considered cryptographically broken.
/// Namely, it is trivial to find multiple inputs producing the same hash.
/// For a fast-performing, cryptographically secure hash function, see SHA512/256, BLAKE2 or BLAKE3.
pub const Md5 = struct {
    const Self = @This();
    pub const block_length = 64;
    pub const digest_length = 16;
    pub const Options = struct {};

    s: [4]u32,
    // Streaming Cache
    buf: [64]u8,
    buf_len: u8,
    total_len: u64,

    pub fn init(options: Options) Self {
        _ = options;
        return Self{
            .s = [_]u32{
                0x67452301,
                0xEFCDAB89,
                0x98BADCFE,
                0x10325476,
            },
            .buf = undefined,
            .buf_len = 0,
            .total_len = 0,
        };
    }

    pub fn hash(data: []const u8, out: *[digest_length]u8, options: Options) void {
        var d = Md5.init(options);
        d.update(data);
        d.final(out);
    }

    pub fn hashResult(data: []const u8) [digest_length]u8 {
        var out: [digest_length]u8 = undefined;
        var d = Md5.init(.{});
        d.update(data);
        d.final(&out);
        return out;
    }

    pub fn update(d: *Self, b: []const u8) void {
        var off: usize = 0;

        // Partial buffer exists from previous update. Copy into buffer then hash.
        if (d.buf_len != 0 and d.buf_len + b.len >= 64) {
            off += 64 - d.buf_len;
            @memcpy(d.buf[d.buf_len..][0..off], b[0..off]);

            d.round(&d.buf);
            d.buf_len = 0;
        }

        // Full middle blocks.
        while (off + 64 <= b.len) : (off += 64) {
            d.round(b[off..][0..64]);
        }

        // Copy any remainder for next pass.
        const b_slice = b[off..];
        @memcpy(d.buf[d.buf_len..][0..b_slice.len], b_slice);
        d.buf_len += @as(u8, @intCast(b_slice.len));

        // Md5 uses the bottom 64-bits for length padding
        d.total_len +%= b.len;
    }

    pub fn final(d: *Self, out: *[digest_length]u8) void {
        // The buffer here will never be completely full.
        @memset(d.buf[d.buf_len..], 0);

        // Append padding bits.
        d.buf[d.buf_len] = 0x80;
        d.buf_len += 1;

        // > 448 mod 512 so need to add an extra round to wrap around.
        if (64 - d.buf_len < 8) {
            d.round(d.buf[0..]);
            @memset(d.buf[0..], 0);
        }

        // Append message length.
        var i: usize = 1;
        var len = d.total_len >> 5;
        d.buf[56] = @as(u8, @intCast(d.total_len & 0x1f)) << 3;
        while (i < 8) : (i += 1) {
            d.buf[56 + i] = @as(u8, @intCast(len & 0xff));
            len >>= 8;
        }

        d.round(d.buf[0..]);

        for (d.s, 0..) |s, j| {
            mem.writeInt(u32, out[4 * j ..][0..4], s, .little);
        }
    }

    fn round(d: *Self, b: *const [64]u8) void {
        var s: [16]u32 = undefined;

        var i: usize = 0;
        while (i < 16) : (i += 1) {
            s[i] = mem.readInt(u32, b[i * 4 ..][0..4], .little);
        }

        var v: [4]u32 = [_]u32{
            d.s[0],
            d.s[1],
            d.s[2],
            d.s[3],
        };

        const round0 = comptime [_]RoundParam{
            roundParam(0, 1, 2, 3, 0, 7, 0xD76AA478),
            roundParam(3, 0, 1, 2, 1, 12, 0xE8C7B756),
            roundParam(2, 3, 0, 1, 2, 17, 0x242070DB),
            roundParam(1, 2, 3, 0, 3, 22, 0xC1BDCEEE),
            roundParam(0, 1, 2, 3, 4, 7, 0xF57C0FAF),
            roundParam(3, 0, 1, 2, 5, 12, 0x4787C62A),
            roundParam(2, 3, 0, 1, 6, 17, 0xA8304613),
            roundParam(1, 2, 3, 0, 7, 22, 0xFD469501),
            roundParam(0, 1, 2, 3, 8, 7, 0x698098D8),
            roundParam(3, 0, 1, 2, 9, 12, 0x8B44F7AF),
            roundParam(2, 3, 0, 1, 10, 17, 0xFFFF5BB1),
            roundParam(1, 2, 3, 0, 11, 22, 0x895CD7BE),
            roundParam(0, 1, 2, 3, 12, 7, 0x6B901122),
            roundParam(3, 0, 1, 2, 13, 12, 0xFD987193),
            roundParam(2, 3, 0, 1, 14, 17, 0xA679438E),
            roundParam(1, 2, 3, 0, 15, 22, 0x49B40821),
        };
        inline for (round0) |r| {
            v[r.a] = v[r.a] +% (v[r.d] ^ (v[r.b] & (v[r.c] ^ v[r.d]))) +% r.t +% s[r.k];
            v[r.a] = v[r.b] +% math.rotl(u32, v[r.a], r.s);
        }

        const round1 = comptime [_]RoundParam{
            roundParam(0, 1, 2, 3, 1, 5, 0xF61E2562),
            roundParam(3, 0, 1, 2, 6, 9, 0xC040B340),
            roundParam(2, 3, 0, 1, 11, 14, 0x265E5A51),
            roundParam(1, 2, 3, 0, 0, 20, 0xE9B6C7AA),
            roundParam(0, 1, 2, 3, 5, 5, 0xD62F105D),
            roundParam(3, 0, 1, 2, 10, 9, 0x02441453),
            roundParam(2, 3, 0, 1, 15, 14, 0xD8A1E681),
            roundParam(1, 2, 3, 0, 4, 20, 0xE7D3FBC8),
            roundParam(0, 1, 2, 3, 9, 5, 0x21E1CDE6),
            roundParam(3, 0, 1, 2, 14, 9, 0xC33707D6),
            roundParam(2, 3, 0, 1, 3, 14, 0xF4D50D87),
            roundParam(1, 2, 3, 0, 8, 20, 0x455A14ED),
            roundParam(0, 1, 2, 3, 13, 5, 0xA9E3E905),
            roundParam(3, 0, 1, 2, 2, 9, 0xFCEFA3F8),
            roundParam(2, 3, 0, 1, 7, 14, 0x676F02D9),
            roundParam(1, 2, 3, 0, 12, 20, 0x8D2A4C8A),
        };
        inline for (round1) |r| {
            v[r.a] = v[r.a] +% (v[r.c] ^ (v[r.d] & (v[r.b] ^ v[r.c]))) +% r.t +% s[r.k];
            v[r.a] = v[r.b] +% math.rotl(u32, v[r.a], r.s);
        }

        const round2 = comptime [_]RoundParam{
            roundParam(0, 1, 2, 3, 5, 4, 0xFFFA3942),
            roundParam(3, 0, 1, 2, 8, 11, 0x8771F681),
            roundParam(2, 3, 0, 1, 11, 16, 0x6D9D6122),
            roundParam(1, 2, 3, 0, 14, 23, 0xFDE5380C),
            roundParam(0, 1, 2, 3, 1, 4, 0xA4BEEA44),
            roundParam(3, 0, 1, 2, 4, 11, 0x4BDECFA9),
            roundParam(2, 3, 0, 1, 7, 16, 0xF6BB4B60),
            roundParam(1, 2, 3, 0, 10, 23, 0xBEBFBC70),
            roundParam(0, 1, 2, 3, 13, 4, 0x289B7EC6),
            roundParam(3, 0, 1, 2, 0, 11, 0xEAA127FA),
            roundParam(2, 3, 0, 1, 3, 16, 0xD4EF3085),
            roundParam(1, 2, 3, 0, 6, 23, 0x04881D05),
            roundParam(0, 1, 2, 3, 9, 4, 0xD9D4D039),
            roundParam(3, 0, 1, 2, 12, 11, 0xE6DB99E5),
            roundParam(2, 3, 0, 1, 15, 16, 0x1FA27CF8),
            roundParam(1, 2, 3, 0, 2, 23, 0xC4AC5665),
        };
        inline for (round2) |r| {
            v[r.a] = v[r.a] +% (v[r.b] ^ v[r.c] ^ v[r.d]) +% r.t +% s[r.k];
            v[r.a] = v[r.b] +% math.rotl(u32, v[r.a], r.s);
        }

        const round3 = comptime [_]RoundParam{
            roundParam(0, 1, 2, 3, 0, 6, 0xF4292244),
            roundParam(3, 0, 1, 2, 7, 10, 0x432AFF97),
            roundParam(2, 3, 0, 1, 14, 15, 0xAB9423A7),
            roundParam(1, 2, 3, 0, 5, 21, 0xFC93A039),
            roundParam(0, 1, 2, 3, 12, 6, 0x655B59C3),
            roundParam(3, 0, 1, 2, 3, 10, 0x8F0CCC92),
            roundParam(2, 3, 0, 1, 10, 15, 0xFFEFF47D),
            roundParam(1, 2, 3, 0, 1, 21, 0x85845DD1),
            roundParam(0, 1, 2, 3, 8, 6, 0x6FA87E4F),
            roundParam(3, 0, 1, 2, 15, 10, 0xFE2CE6E0),
            roundParam(2, 3, 0, 1, 6, 15, 0xA3014314),
            roundParam(1, 2, 3, 0, 13, 21, 0x4E0811A1),
            roundParam(0, 1, 2, 3, 4, 6, 0xF7537E82),
            roundParam(3, 0, 1, 2, 11, 10, 0xBD3AF235),
            roundParam(2, 3, 0, 1, 2, 15, 0x2AD7D2BB),
            roundParam(1, 2, 3, 0, 9, 21, 0xEB86D391),
        };
        inline for (round3) |r| {
            v[r.a] = v[r.a] +% (v[r.c] ^ (v[r.b] | ~v[r.d])) +% r.t +% s[r.k];
            v[r.a] = v[r.b] +% math.rotl(u32, v[r.a], r.s);
        }

        d.s[0] +%= v[0];
        d.s[1] +%= v[1];
        d.s[2] +%= v[2];
        d.s[3] +%= v[3];
    }
};

const htest = @import("test.zig");

test "single" {
    try htest.assertEqualHash(Md5, "d41d8cd98f00b204e9800998ecf8427e", "");
    try htest.assertEqualHash(Md5, "0cc175b9c0f1b6a831c399e269772661", "a");
    try htest.assertEqualHash(Md5, "900150983cd24fb0d6963f7d28e17f72", "abc");
    try htest.assertEqualHash(Md5, "f96b697d7cb7938d525a2f31aaf161d0", "message digest");
    try htest.assertEqualHash(Md5, "c3fcd3d76192e4007dfb496cca67e13b", "abcdefghijklmnopqrstuvwxyz");
    try htest.assertEqualHash(Md5, "d174ab98d277d9f5a5611c2c9f419d9f", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789");
    try htest.assertEqualHash(Md5, "57edf4a22be3c955ac49da2e2107b67a", "12345678901234567890123456789012345678901234567890123456789012345678901234567890");
}

test "streaming" {
    var h = Md5.init(.{});
    var out: [16]u8 = undefined;

    h.final(out[0..]);
    try htest.assertEqual("d41d8cd98f00b204e9800998ecf8427e", out[0..]);

    h = Md5.init(.{});
    h.update("abc");
    h.final(out[0..]);
    try htest.assertEqual("900150983cd24fb0d6963f7d28e17f72", out[0..]);

    h = Md5.init(.{});
    h.update("a");
    h.update("b");
    h.update("c");
    h.final(out[0..]);

    try htest.assertEqual("900150983cd24fb0d6963f7d28e17f72", out[0..]);
}

test "aligned final" {
    var block = [_]u8{0} ** Md5.block_length;
    var out: [Md5.digest_length]u8 = undefined;

    var h = Md5.init(.{});
    h.update(&block);
    h.final(out[0..]);
}



---
File: /std/crypto/ml_dsa.zig
---

//! Module-Lattice-Based Digital Signature Algorithm (ML-DSA) as specified in NIST FIPS 204.
//!
//! ML-DSA is a post-quantum secure digital signature scheme based on the hardness
//! of the Module Learning With Errors (MLWE) and Module Short Integer Solution (MSIS)
//! problems over module lattices.
//!
//! We provide three parameter sets:
//!
//! - ML-DSA-44: NIST security category 2 (128-bit security)
//! - ML-DSA-65: NIST security category 3 (192-bit security)
//! - ML-DSA-87: NIST security category 5 (256-bit security)

const std = @import("std");
const builtin = @import("builtin");
const testing = std.testing;
const assert = std.debug.assert;
const crypto = std.crypto;
const errors = std.crypto.errors;
const math = std.math;
const mem = std.mem;
const sha3 = crypto.hash.sha3;

const ContextTooLongError = errors.ContextTooLongError;
const EncodingError = errors.EncodingError;
const SignatureVerificationError = errors.SignatureVerificationError;

/// ML-DSA-44 (Module-Lattice-Based Digital Signature Algorithm, 44 parameter set)
/// as specified in NIST FIPS 204.
///
/// This is a post-quantum signature scheme providing NIST security category 2,
/// which is roughly equivalent to the security of SHA-256 or AES-128.
///
/// Key sizes:
///
/// - Public key: 1312 bytes
/// - Secret key: 2560 bytes
/// - Signature: 2420 bytes
///
/// Example usage:
///
/// ```zig
/// const kp = MLDSA44.KeyPair.generate();
/// const msg = "Hello, post-quantum world!";
/// const sig = try kp.sign(msg, null);
/// try sig.verify(msg, kp.public_key);
/// ```
pub const MLDSA44 = MLDSAImpl(.{
    .name = "ML-DSA-44",
    .k = 4,
    .l = 4,
    .eta = 2,
    .omega = 80,
    .tau = 39,
    .gamma1_bits = 17,
    .gamma2 = 95232, // (Q-1)/88
    .tr_size = 64,
    .ctilde_size = 32,
});

/// ML-DSA-65 (Module-Lattice-Based Digital Signature Algorithm, 65 parameter set)
/// as specified in NIST FIPS 204.
///
/// This is a post-quantum signature scheme providing NIST security category 3,
/// which is roughly equivalent to the security of SHA-384 or AES-192.
///
/// Key sizes:
///
/// - Public key: 1952 bytes
/// - Secret key: 4032 bytes
/// - Signature: 3309 bytes
///
/// This parameter set offers higher security than ML-DSA-44 at the cost of
/// larger keys and signatures.
pub const MLDSA65 = MLDSAImpl(.{
    .name = "ML-DSA-65",
    .k = 6,
    .l = 5,
    .eta = 4,
    .omega = 55,
    .tau = 49,
    .gamma1_bits = 19,
    .gamma2 = 261888, // (Q-1)/32
    .tr_size = 64,
    .ctilde_size = 48,
});

/// ML-DSA-87 (Module-Lattice-Based Digital Signature Algorithm, 87 parameter set)
/// as specified in NIST FIPS 204.
///
/// This is a post-quantum signature scheme providing NIST security category 5,
/// which is roughly equivalent to the security of SHA-512 or AES-256.
///
/// Key sizes:
///
/// - Public key: 2592 bytes
/// - Secret key: 4896 bytes
/// - Signature: 4627 bytes
///
/// This parameter set offers the highest security level among the three ML-DSA
/// variants, suitable for applications requiring maximum security assurance.
pub const MLDSA87 = MLDSAImpl(.{
    .name = "ML-DSA-87",
    .k = 8,
    .l = 7,
    .eta = 2,
    .omega = 75,
    .tau = 60,
    .gamma1_bits = 19,
    .gamma2 = 261888, // (Q-1)/32
    .tr_size = 64,
    .ctilde_size = 64,
});

const N: usize = 256; // Degree of polynomials
const Q: u32 = 8380417; // Modulus: 2^23 - 2^13 + 1
const Q_BITS: u32 = 23;
const D: u32 = 13; // Dropped bits in power2Round

// Montgomery constant R = 2^32 mod q
const R: u64 = 1 << 32;

// Q^(-1) mod 2^32 = -(q^-1) mod 2^32
const Q_INV: u32 = 4236238847;

// (256)^(-1) * R^2 mod q, used in inverse NTT
const R_OVER_256: u32 = 41978;

// Primitive 512th root of unity
const ZETA: u32 = 1753;

const Params = struct {
    name: []const u8,

    // Matrix dimensions
    k: u8, // Height of matrix A
    l: u8, // Width of matrix A

    // Sampling parameter
    eta: u8, // Bound for secret coefficients

    // Hint parameters
    omega: u16, // Maximum number of hint bits

    // Challenge parameter
    tau: u16, // Weight of challenge polynomial

    // Rounding parameters
    gamma1_bits: u8, // Bits for gamma1
    gamma2: u32, // Parameter for decompose

    // Sizes
    tr_size: usize, // Size of tr hash
    ctilde_size: usize, // Size of challenge hash
};

const Poly = struct {
    cs: [N]u32,

    const zero: Poly = .{ .cs = .{0} ** N };

    // Add two polynomials (no normalization)
    fn add(a: Poly, b: Poly) Poly {
        var ret: Poly = undefined;
        for (0..N) |i| {
            ret.cs[i] = a.cs[i] + b.cs[i];
        }
        return ret;
    }

    // Subtract two polynomials (assumes b coefficients < 2q)
    fn sub(a: Poly, b: Poly) Poly {
        var ret: Poly = undefined;
        for (0..N) |i| {
            ret.cs[i] = a.cs[i] +% (@as(u32, 2 * Q) -% b.cs[i]);
        }
        return ret;
    }

    // Reduce each coefficient to < 2q
    fn reduceLe2Q(p: Poly) Poly {
        var ret = p;
        for (0..N) |i| {
            ret.cs[i] = le2Q(ret.cs[i]);
        }
        return ret;
    }

    // Normalize coefficients to [0, q)
    fn normalize(p: Poly) Poly {
        var ret = p;
        for (0..N) |i| {
            ret.cs[i] = modQ(ret.cs[i]);
        }
        return ret;
    }

    // Normalize assuming coefficients already < 2q
    fn normalizeAssumingLe2Q(p: Poly) Poly {
        var ret = p;
        for (0..N) |i| {
            ret.cs[i] = le2qModQ(ret.cs[i]);
        }
        return ret;
    }

    // Pointwise multiplication in NTT domain (Montgomery form)
    fn mulHat(a: Poly, b: Poly) Poly {
        var ret: Poly = undefined;
        for (0..N) |i| {
            ret.cs[i] = montReduceLe2Q(@as(u64, a.cs[i]) * @as(u64, b.cs[i]));
        }
        return ret;
    }

    // Forward NTT
    fn ntt(p: Poly) Poly {
        var ret = p;
        ret.nttInPlace();
        return ret;
    }

    // In-place forward NTT
    fn nttInPlace(p: *Poly) void {
        var k: usize = 0;
        var l: usize = N / 2;

        while (l > 0) : (l >>= 1) {
            var offset: usize = 0;
            while (offset < N - l) : (offset += 2 * l) {
                k += 1;
                const zeta: u64 = zetas[k];

                for (offset..offset + l) |j| {
                    const t = montReduceLe2Q(zeta * @as(u64, p.cs[j + l]));
                    p.cs[j + l] = p.cs[j] +% (2 * Q -% t);
                    p.cs[j] +%= t;
                }
            }
        }
    }

    // Inverse NTT
    fn invNTT(p: Poly) Poly {
        var ret = p;
        ret.invNTTInPlace();
        return ret;
    }

    // In-place inverse NTT
    fn invNTTInPlace(p: *Poly) void {
        var k: usize = 0;
        var l: usize = 1;

        while (l < N) : (l <<= 1) {
            var offset: usize = 0;
            while (offset < N - l) : (offset += 2 * l) {
                const zeta: u64 = inv_zetas[k];
                k += 1;

                for (offset..offset + l) |j| {
                    const t = p.cs[j];
                    p.cs[j] = t +% p.cs[j + l];
                    p.cs[j + l] = montReduceLe2Q(zeta * @as(u64, t +% 256 * Q -% p.cs[j + l]));
                }
            }
        }

        for (0..N) |j| {
            p.cs[j] = montReduceLe2Q(@as(u64, R_OVER_256) * @as(u64, p.cs[j]));
        }
    }

    /// Apply Power2Round to all coefficients
    /// Returns both t0 and t1 polynomials
    fn power2RoundPoly(p: Poly) struct { t0: Poly, t1: Poly } {
        var t0 = Poly.zero;
        var t1 = Poly.zero;
        for (0..N) |i| {
            const result = power2Round(p.cs[i]);
            t0.cs[i] = result.a0_plus_q;
            t1.cs[i] = result.a1;
        }
        return .{ .t0 = t0, .t1 = t1 };
    }

    // Check if infinity norm exceeds bound
    fn exceeds(p: Poly, bound: u32) bool {
        var result: u32 = 0;
        for (0..N) |i| {
            const x = @as(i32, @intCast((Q - 1) / 2)) - @as(i32, @intCast(p.cs[i]));
            const abs_x = x ^ (x >> 31);
            const norm = @as(i32, @intCast((Q - 1) / 2)) - abs_x;
            const exceeds_bit = @intFromBool(@as(u32, @intCast(norm)) >= bound);
            result |= exceeds_bit;
        }
        return result != 0;
    }
};

fn PolyVec(comptime len: u8) type {
    return struct {
        ps: [len]Poly,

        const Self = @This();
        const zero: Self = .{ .ps = .{Poly.zero} ** len };

        /// Apply a unary operation to each polynomial in the vector
        fn map(v: Self, comptime op: fn (Poly) Poly) Self {
            var ret: Self = undefined;
            inline for (0..len) |i| {
                ret.ps[i] = op(v.ps[i]);
            }
            return ret;
        }

        /// Apply a binary operation pairwise to two vectors
        fn mapBinary(a: Self, b: Self, comptime op: fn (Poly, Poly) Poly) Self {
            var ret: Self = undefined;
            inline for (0..len) |i| {
                ret.ps[i] = op(a.ps[i], b.ps[i]);
            }
            return ret;
        }

        /// Apply a binary operation between a vector and a scalar polynomial
        fn mapBinaryPoly(v: Self, scalar: Poly, comptime op: fn (Poly, Poly) Poly) Self {
            var ret: Self = undefined;
            inline for (0..len) |i| {
                ret.ps[i] = op(v.ps[i], scalar);
            }
            return ret;
        }

        fn add(a: Self, b: Self) Self {
            return mapBinary(a, b, Poly.add);
        }

        fn sub(a: Self, b: Self) Self {
            return mapBinary(a, b, Poly.sub);
        }

        fn ntt(v: Self) Self {
            return map(v, Poly.ntt);
        }

        fn invNTT(v: Self) Self {
            return map(v, Poly.invNTT);
        }

        fn normalize(v: Self) Self {
            return map(v, Poly.normalize);
        }

        fn reduceLe2Q(v: Self) Self {
            return map(v, Poly.reduceLe2Q);
        }

        fn normalizeAssumingLe2Q(v: Self) Self {
            return map(v, Poly.normalizeAssumingLe2Q);
        }

        // Check if any polynomial in the vector exceeds the bound
        fn exceeds(v: Self, bound: u32) bool {
            var result = false;
            for (0..len) |i| {
                result = result or v.ps[i].exceeds(bound);
            }
            return result;
        }

        /// Apply Power2Round to each polynomial in the vector
        /// Returns both t0 and t1 vectors
        fn power2Round(v: Self, t0_out: *Self) Self {
            var t1: Self = undefined;
            for (0..len) |i| {
                const result = v.ps[i].power2RoundPoly();
                t0_out.ps[i] = result.t0;
                t1.ps[i] = result.t1;
            }
            return t1;
        }

        /// Generic packing function for vectors
        fn packWith(
            v: Self,
            buf: []u8,
            comptime poly_size: usize,
            comptime pack_fn: fn (Poly, []u8) void,
        ) void {
            inline for (0..len) |i| {
                const offset = i * poly_size;
                pack_fn(v.ps[i], buf[offset..][0..poly_size]);
            }
        }

        /// Generic unpacking function for vectors
        fn unpackWith(
            comptime poly_size: usize,
            comptime unpack_fn: fn ([]const u8) Poly,
            buf: []const u8,
        ) Self {
            var result: Self = undefined;
            inline for (0..len) |i| {
                const offset = i * poly_size;
                result.ps[i] = unpack_fn(buf[offset..][0..poly_size]);
            }
            return result;
        }

        /// Pack T1 vector to bytes
        fn packT1(v: Self, buf: []u8) void {
            const poly_size = (N * (Q_BITS - D)) / 8;
            packWith(v, buf, poly_size, polyPackT1);
        }

        /// Unpack T1 vector from bytes
        fn unpackT1(bytes: []const u8) Self {
            const poly_size = (N * (Q_BITS - D)) / 8;
            return unpackWith(poly_size, polyUnpackT1, bytes);
        }

        /// Pack T0 vector to bytes
        fn packT0(v: Self, buf: []u8) void {
            const poly_size = (N * D) / 8;
            packWith(v, buf, poly_size, polyPackT0);
        }

        /// Unpack T0 vector from bytes
        fn unpackT0(buf: []const u8) Self {
            const poly_size = (N * D) / 8;
            return unpackWith(poly_size, polyUnpackT0, buf);
        }

        /// Pack vector with coefficients in [-eta, eta]
        fn packLeqEta(v: Self, comptime eta: u8, buf: []u8) void {
            const poly_size = if (eta == 2) 96 else 128;
            const pack_fn = struct {
                fn pack(p: Poly, b: []u8) void {
                    polyPackLeqEta(p, eta, b);
                }
            }.pack;
            packWith(v, buf, poly_size, pack_fn);
        }

        /// Unpack vector with coefficients in [-eta, eta]
        fn unpackLeqEta(comptime eta: u8, buf: []const u8) Self {
            const poly_size = if (eta == 2) 96 else 128;
            const unpack_fn = struct {
                fn unpack(b: []const u8) Poly {
                    return polyUnpackLeqEta(eta, b);
                }
            }.unpack;
            return unpackWith(poly_size, unpack_fn, buf);
        }

        /// Pack vector of polynomials with coefficients < gamma1
        fn packLeGamma1(v: Self, comptime gamma1_bits: u8, buf: []u8) void {
            const poly_size = ((gamma1_bits + 1) * N) / 8;
            const pack_fn = struct {
                fn pack(p: Poly, b: []u8) void {
                    polyPackLeGamma1(p, gamma1_bits, b);
                }
            }.pack;
            packWith(v, buf, poly_size, pack_fn);
        }

        /// Unpack vector of polynomials with coefficients < gamma1
        fn unpackLeGamma1(comptime gamma1_bits: u8, buf: []const u8) Self {
            const poly_size = ((gamma1_bits + 1) * N) / 8;
            const unpack_fn = struct {
                fn unpack(b: []const u8) Poly {
                    return polyUnpackLeGamma1(gamma1_bits, b);
                }
            }.unpack;
            return unpackWith(poly_size, unpack_fn, buf);
        }

        /// Pack high bits w1 for signature verification
        fn packW1(v: Self, comptime gamma1_bits: u8, buf: []u8) void {
            const poly_size = (N * (Q_BITS - gamma1_bits)) / 8;
            const pack_fn = struct {
                fn pack(p: Poly, b: []u8) void {
                    polyPackW1(p, gamma1_bits, b);
                }
            }.pack;
            packWith(v, buf, poly_size, pack_fn);
        }

        /// Decompose each polynomial in the vector into high and low bits
        fn decomposeVec(v: Self, comptime gamma2: u32, w0_out: *Self) Self {
            var w1: Self = undefined;
            for (0..len) |i| {
                for (0..N) |j| {
                    const r = decompose(v.ps[i].cs[j], gamma2);
                    w0_out.ps[i].cs[j] = r.a0_plus_q;
                    w1.ps[i].cs[j] = r.a1;
                }
            }
            return w1;
        }

        /// Create hints for vector, returns hint population count
        fn makeHintVec(w0mcs2pct0: Self, w1: Self, comptime gamma2: u32) struct { hint: Self, pop: u32 } {
            var hint: Self = undefined;
            var pop: u32 = 0;
            for (0..len) |i| {
                const result = polyMakeHint(w0mcs2pct0.ps[i], w1.ps[i], gamma2);
                hint.ps[i] = result.hint;
                pop += result.count;
            }
            return .{ .hint = hint, .pop = pop };
        }

        /// Apply hints to recover high bits
        fn useHint(v: Self, hint: Self, comptime gamma2: u32) Self {
            var result: Self = undefined;
            for (0..len) |i| {
                result.ps[i] = polyUseHint(v.ps[i], hint.ps[i], gamma2);
            }
            return result;
        }

        /// Multiply vector by 2^D (left shift)
        fn mulBy2toD(v: Self) Self {
            var result: Self = undefined;
            for (0..len) |i| {
                for (0..N) |j| {
                    result.ps[i].cs[j] = v.ps[i].cs[j] << D;
                }
            }
            return result;
        }

        /// Sample vector with coefficients uniformly in (-gamma1, gamma1]
        /// Wraps expandMask (FIPS 204: ExpandMask)
        fn deriveUniformLeGamma1(comptime gamma1_bits: u8, seed: *const [64]u8, nonce: u16) Self {
            var result: Self = undefined;
            for (0..len) |i| {
                result.ps[i] = expandMask(gamma1_bits, seed, nonce + @as(u16, @intCast(i)));
            }
            return result;
        }

        /// Pack hints into bytes
        /// Format: for each polynomial, find positions where hint[i]=1, encode those positions
        fn packHint(v: Self, comptime omega: u16, buf: []u8) bool {
            var idx: usize = 0;
            var count: u32 = 0;

            for (0..len) |i| {
                for (0..N) |j| {
                    if (v.ps[i].cs[j] != 0) {
                        count += 1;
                    }
                }
            }

            if (count > omega) {
                return false;
            }

            // Hint encoding format per FIPS 204:
            // First omega bytes: positions of set bits across all polynomials
            // Last len bytes: boundary indices showing where each polynomial's hints end
            for (0..len) |i| {
                for (0..N) |j| {
                    if (v.ps[i].cs[j] != 0) {
                        buf[idx] = @intCast(j);
                        idx += 1;
                    }
                }
                buf[omega + i] = @intCast(idx);
            }

            while (idx < omega) : (idx += 1) {
                buf[idx] = 0;
            }

            return true;
        }

        /// Unpack hints from bytes
        fn unpackHint(comptime omega: u16, buf: []const u8) ?Self {
            var result: Self = .{ .ps = .{Poly.zero} ** len };
            var prev_sop: u8 = 0; // previous switch-over-point

            for (0..len) |i| {
                const sop = buf[omega + i]; // switch-over-point
                if (sop < prev_sop or sop > omega) {
                    return null; // ensures switch-over-points are increasing
                }

                var j = prev_sop;
                while (j < sop) : (j += 1) {
                    // Validation: indices must be strictly increasing within each polynomial
                    if (j > prev_sop and buf[j] <= buf[j - 1]) {
                        return null;
                    }
                    const pos = buf[j];
                    if (pos >= N) {
                        return null;
                    }
                    result.ps[i].cs[pos] = 1;
                }
                prev_sop = sop;
            }

            var j = prev_sop;
            while (j < omega) : (j += 1) {
                if (buf[j] != 0) {
                    return null;
                }
            }

            return result;
        }
    };
}

// Matrix of k x l polynomials

fn Mat(comptime k: u8, comptime l: u8) type {
    return struct {
        rows: [k]PolyVec(l),

        const Self = @This();
        const VecL = PolyVec(l);
        const VecK = PolyVec(k);

        /// Expand matrix A from seed rho using SHAKE-128
        /// This is the ExpandA function from FIPS 204
        fn derive(rho: *const [32]u8) Self {
            var m: Self = undefined;
            for (0..k) |i| {
                if (i + 1 < k) {
                    @prefetch(&m.rows[i + 1], .{ .rw = .write, .locality = 2 });
                }
                for (0..l) |j| {
                    // Nonce is i*256 + j
                    const nonce: u16 = (@as(u16, @intCast(i)) << 8) | @as(u16, @intCast(j));
                    m.rows[i].ps[j] = polyDeriveUniform(rho, nonce);
                }
            }
            return m;
        }

        /// Multiply matrix by vector in NTT domain and return result in regular domain.
        /// Takes a vector in NTT form and returns the product in regular form.
        fn mulVec(self: Self, v_hat: VecL) VecK {
            var result = VecK.zero;
            for (0..k) |i| {
                result.ps[i] = dotHat(l, self.rows[i], v_hat);
                result.ps[i] = result.ps[i].reduceLe2Q();
                result.ps[i] = result.ps[i].invNTT();
            }
            return result;
        }

        /// Multiply matrix by vector in NTT domain and return result in NTT domain.
        /// Takes a vector in NTT form and returns the product in NTT form.
        fn mulVecHat(self: Self, v_hat: VecL) VecK {
            var result: VecK = undefined;
            for (0..k) |i| {
                result.ps[i] = dotHat(l, self.rows[i], v_hat);
            }
            return result;
        }
    };
}

// Dot product in NTT domain
fn dotHat(comptime len: u8, a: PolyVec(len), b: PolyVec(len)) Poly {
    var ret = Poly.zero;
    for (0..len) |i| {
        const prod = a.ps[i].mulHat(b.ps[i]);
        ret = ret.add(prod);
    }
    return ret;
}

// Modular arithmetic operations

// Reduce x to [0, 2q) using the fact that 2^23 = 2^13 - 1 (mod q)
fn le2Q(x: u32) u32 {
    // Write x = x1 * 2^23 + x2 with x2 < 2^23 and x1 < 2^9
    // Then x = x2 + x1 * 2^13 - x1 (mod q)
    // and x2 + x1 * 2^13 - x1 <= 2^23 + 2^13 < 2q
    const x1 = x >> 23;
    const x2 = x & 0x7FFFFF; // 2^23 - 1
    return x2 +% (x1 << 13) -% x1;
}

// Reduce x to [0, q)
fn modQ(x: u32) u32 {
    return le2qModQ(le2Q(x));
}

// Given x < 2q, reduce to [0, q)
fn le2qModQ(x: u32) u32 {
    const r = x -% Q;
    const mask = signMask(u32, r);
    return r +% (mask & Q);
}

// Montgomery reduction: for x < q*2^32, return y < 2q where y ≡ x*R^(-1) (mod q)
// where R = 2^32. This is used for efficient modular multiplication in NTT operations.
fn montReduceLe2Q(x: u64) u32 {
    const m = (x *% Q_INV) & 0xffffffff;
    return @truncate((x +% m * @as(u64, Q)) >> 32);
}

// Precomputed zetas for NTT (Montgomery form)
// zetas[i] = zeta^brv(i) * R mod q
const zetas = computeZetas();

fn computeZetas() [N]u32 {
    @setEvalBranchQuota(100000);
    var ret: [N]u32 = undefined;

    for (0..N) |i| {
        const brv_i = @bitReverse(@as(u8, @intCast(i)));
        const power = modularPow(u32, ZETA, brv_i, Q);
        ret[i] = toMont(power);
    }

    return ret;
}

// Precomputed inverse zetas for inverse NTT
const inv_zetas = computeInvZetas();

fn computeInvZetas() [N]u32 {
    @setEvalBranchQuota(100000);
    var ret: [N]u32 = undefined;

    const inv_zeta = modularInverse(u32, ZETA, Q);

    for (0..N) |i| {
        const idx = 255 - i;
        const brv_idx = @bitReverse(@as(u8, @intCast(idx)));

        // Exponent is -(brv_idx - 256) = 256 - brv_idx
        const exp: u32 = @as(u32, 256) - brv_idx;

        // Compute inv_zeta^exp
        const power = modularPow(u32, inv_zeta, exp, Q);

        // Convert to Montgomery form
        ret[i] = toMont(power);
    }

    return ret;
}

// Convert to Montgomery form: x -> x * R mod q
fn toMont(x: u32) u32 {
    // R = 2^32, R mod q can be computed as:
    // 2^32 mod q = 2^32 mod (2^23 - 2^13 + 1)
    // Using the identity 2^23 = 2^13 - 1 (mod q), we can reduce 2^32
    // But it's easier to just do: return montReduce(x * R^2 mod q)
    // where R^2 mod q is precomputed

    // Computing R^2 mod q:
    // R = 2^32, so R^2 = 2^64
    // We can compute this by noting that R mod q first:
    // 2^32 = 2^32 mod q
    // But let's use a simpler approach: multiply x by R in the Montgomery domain
    // Actually, the simplest is: x * R mod q = montReduceLe2Q(x * R^2 mod q)

    // Precompute R^2 mod q at comptime
    const r_mod_q = comptime blk: {
        // 2^32 mod q - compute by successive squaring
        var r: u64 = 1;
        for (0..32) |_| {
            r = (r * 2) % Q;
        }
        break :blk @as(u32, @intCast(r));
    };

    const r2_mod_q = comptime blk: {
        const r = @as(u64, r_mod_q);
        break :blk @as(u32, @intCast((r * r) % Q));
    };

    return montReduceLe2Q(@as(u64, x) * @as(u64, r2_mod_q));
}

/// Splits 0 ≤ a < Q into a0 and a1 with a = a1*2^D + a0
/// and -2^(D-1) < a0 ≤ 2^(D-1). Returns a0 + Q and a1.
/// FIPS 204: Power2Round (Algorithm 19)
fn power2Round(a: u32) struct { a0_plus_q: u32, a1: u32 } {
    // We effectively compute a0 = a mod± 2^D
    //                    and a1 = (a - a0) / 2^D
    var a0 = a & ((1 << D) - 1); // a mod 2^D

    // a0 is one of 0, 1, ..., 2^(D-1)-1, 2^(D-1), 2^(D-1)+1, ..., 2^D-1
    a0 -%= (1 << (D - 1)) + 1;
    // now a0 is -2^(D-1)-1, -2^(D-1), ..., -2, -1, 0, ..., 2^(D-1)-2

    // Next, add 2^D to those a0 that are negative (seen as i32)
    a0 +%= @as(u32, @bitCast(@as(i32, @bitCast(a0)) >> 31)) & (1 << D);
    // now a0 is 2^(D-1)-1, 2^(D-1), ..., 2^D-2, 2^D-1, 0, ..., 2^(D-1)-2

    a0 -%= (1 << (D - 1)) - 1;
    // now a0 is 0, 1, 2, ..., 2^(D-1)-1, 2^(D-1), -2^(D-1)+1, ..., -1

    const a0_plus_q = Q +% a0;
    const a1 = (a -% a0) >> D;

    return .{ .a0_plus_q = a0_plus_q, .a1 = a1 };
}

/// Splits 0 ≤ a < q into a0 and a1 with a = a1*alpha + a0 with -alpha/2 < a0 ≤ alpha/2,
/// except when we would have a1 = (q-1)/alpha in which case a1=0 is taken
/// and -alpha/2 ≤ a0 < 0. Returns a0 + q. Note 0 ≤ a1 < (q-1)/alpha.
/// Recall alpha = 2*gamma2.
fn decompose(a: u32, comptime gamma2: u32) struct { a0_plus_q: u32, a1: u32 } {
    const alpha = 2 * gamma2;

    // a1 = ⌈a / 128⌉
    var a1 = (a + 127) >> 7;

    if (alpha == 523776) {
        // For ML-DSA-87: gamma2 = 261888, alpha = 523776
        // 1025/2^22 is close enough to 1/4092 so that a1 becomes a/alpha rounded down
        a1 = ((a1 * 1025 + (1 << 21)) >> 22);

        // For the corner-case a1 = (q-1)/alpha = 16, we have to set a1=0
        a1 &= 15;
    } else if (alpha == 190464) {
        // For ML-DSA-65: gamma2 = 95232, alpha = 190464
        // 11275/2^24 is close enough to 1/1488 so that a1 becomes a/alpha rounded down
        a1 = ((a1 * 11275) + (1 << 23)) >> 24;

        // For the corner-case a1 = (q-1)/alpha = 44, we have to set a1=0
        a1 ^= @as(u32, @bitCast(@as(i32, @bitCast(43 -% a1)) >> 31)) & a1;
    } else {
        @compileError("unsupported gamma2/alpha value");
    }

    var a0_plus_q = a -% a1 * alpha;

    // In the corner-case, when we set a1=0, we will incorrectly
    // have a0 > (q-1)/2 and we'll need to subtract q. As we
    // return a0 + q, that comes down to adding q if a0 < (q-1)/2.
    a0_plus_q +%= @as(u32, @bitCast(@as(i32, @bitCast(a0_plus_q -% (Q - 1) / 2)) >> 31)) & Q;

    return .{ .a0_plus_q = a0_plus_q, .a1 = a1 };
}

/// Creates a hint bit to help recover high bits after a small perturbation.
/// Given:
/// - z0: the modified low bits (r0 - f mod Q) where f is small
/// - r1: the original high bits
/// Returns 1 if a hint is needed, 0 otherwise.
///
/// This implements makeHint from FIPS 204. The hint helps recover r1 from
/// r' = r - f without knowing f explicitly.
fn makeHint(z0: u32, r1: u32, comptime gamma2: u32) u32 {
    // If -alpha/2 < r0 - f <= alpha/2, then r1*alpha + r0 - f is a valid
    // decomposition of r' with the restrictions of decompose() and so r'1 = r1.
    // So the hint should be 0. This is covered by the first two inequalities.
    // There is one other case: if r0 - f = -alpha/2, then r1*alpha + r0 - f is
    // also a valid decomposition if r1 = 0. In the other cases a one is carried
    // and the hint should be 1.

    const cond1 = @intFromBool(z0 <= gamma2);
    const cond2 = @intFromBool(z0 > Q - gamma2);
    const eq_gamma2 = @intFromBool(z0 == Q - gamma2);
    const r1_is_zero = @intFromBool(r1 == 0);
    const cond3 = eq_gamma2 & r1_is_zero;

    return 1 - (cond1 | cond2 | cond3);
}

/// Uses a hint to reconstruct high bits from a perturbed value.
/// Given:
/// - rp: the perturbed value (r' = r - f)
/// - hint: the hint bit from makeHint
/// Returns the reconstructed high bits r1.
///
/// This implements useHint from FIPS 204.
fn useHint(rp: u32, hint: u32, comptime gamma2: u32) u32 {
    const decomp = decompose(rp, gamma2);
    const rp0_plus_q = decomp.a0_plus_q;
    var rp1 = decomp.a1;

    if (hint == 0) {
        return rp1;
    }

    // Depending on gamma2, handle the adjustment differently
    if (gamma2 == 261888) {
        // ML-DSA-65 and ML-DSA-87: max r1 is 15
        if (rp0_plus_q > Q) {
            rp1 = (rp1 + 1) & 15;
        } else {
            rp1 = (rp1 -% 1) & 15;
        }
    } else if (gamma2 == 95232) {
        // ML-DSA-44: max r1 is 43
        if (rp0_plus_q > Q) {
            if (rp1 == 43) {
                rp1 = 0;
            } else {
                rp1 += 1;
            }
        } else {
            if (rp1 == 0) {
                rp1 = 43;
            } else {
                rp1 -= 1;
            }
        }
    } else {
        @compileError("unsupported gamma2 value");
    }

    return rp1;
}

/// Creates a hint polynomial for the difference between perturbed and original high bits.
/// Returns the number of hint bits set to 1 (the population count).
///
/// This is used during signature generation to create hints that help verification
/// recover the high bits without access to the secret.
fn polyMakeHint(p0: Poly, p1: Poly, comptime gamma2: u32) struct { hint: Poly, count: u32 } {
    var hint = Poly.zero;
    var count: u32 = 0;

    for (0..N) |i| {
        const h = makeHint(p0.cs[i], p1.cs[i], gamma2);
        hint.cs[i] = h;
        count += h;
    }

    return .{ .hint = hint, .count = count };
}

/// Applies hints to reconstruct high bits from a perturbed polynomial.
///
/// This is used during signature verification to recover the high bits
/// using the hints provided in the signature.
fn polyUseHint(q: Poly, hint: Poly, comptime gamma2: u32) Poly {
    var result = Poly.zero;

    for (0..N) |i| {
        result.cs[i] = useHint(q.cs[i], hint.cs[i], gamma2);
    }

    return result;
}

/// Pack polynomial with coefficients in [Q-eta, Q+eta] into bytes.
/// For eta=2: packs coefficients into 3 bits each (96 bytes total)
/// For eta=4: packs coefficients into 4 bits each (128 bytes total)
/// Assumes coefficients are not normalized, but in [q-η, q+η].
fn polyPackLeqEta(p: Poly, comptime eta: u8, buf: []u8) void {
    comptime {
        if (eta != 2 and eta != 4) {
            @compileError("eta must be 2 or 4");
        }
    }

    if (eta == 2) {
        // 3 bits per coefficient: pack 8 coefficients into 3 bytes
        var j: usize = 0;
        var i: usize = 0;
        while (i < buf.len) : (i += 3) {
            const c0 = Q + eta - p.cs[j];
            const c1 = Q + eta - p.cs[j + 1];
            const c2 = Q + eta - p.cs[j + 2];
            const c3 = Q + eta - p.cs[j + 3];
            const c4 = Q + eta - p.cs[j + 4];
            const c5 = Q + eta - p.cs[j + 5];
            const c6 = Q + eta - p.cs[j + 6];
            const c7 = Q + eta - p.cs[j + 7];

            buf[i] = @truncate(c0 | (c1 << 3) | (c2 << 6));
            buf[i + 1] = @truncate((c2 >> 2) | (c3 << 1) | (c4 << 4) | (c5 << 7));
            buf[i + 2] = @truncate((c5 >> 1) | (c6 << 2) | (c7 << 5));

            j += 8;
        }
    } else { // eta == 4
        // 4 bits per coefficient: pack 2 coefficients into 1 byte
        var j: usize = 0;
        for (0..buf.len) |i| {
            const c0 = Q + eta - p.cs[j];
            const c1 = Q + eta - p.cs[j + 1];
            buf[i] = @truncate(c0 | (c1 << 4));
            j += 2;
        }
    }
}

/// Unpack polynomial with coefficients in [Q-eta, Q+eta] from bytes.
/// Output coefficients will not be normalized, but in [q-η, q+η].
fn polyUnpackLeqEta(comptime eta: u8, buf: []const u8) Poly {
    comptime {
        if (eta != 2 and eta != 4) {
            @compileError("eta must be 2 or 4");
        }
    }

    var p = Poly.zero;

    if (eta == 2) {
        // 3 bits per coefficient: unpack 8 coefficients from 3 bytes
        var j: usize = 0;
        var i: usize = 0;
        while (i < buf.len) : (i += 3) {
            p.cs[j] = Q + eta - (buf[i] & 7);
            p.cs[j + 1] = Q + eta - ((buf[i] >> 3) & 7);
            p.cs[j + 2] = Q + eta - ((buf[i] >> 6) | ((buf[i + 1] << 2) & 7));
            p.cs[j + 3] = Q + eta - ((buf[i + 1] >> 1) & 7);
            p.cs[j + 4] = Q + eta - ((buf[i + 1] >> 4) & 7);
            p.cs[j + 5] = Q + eta - ((buf[i + 1] >> 7) | ((buf[i + 2] << 1) & 7));
            p.cs[j + 6] = Q + eta - ((buf[i + 2] >> 2) & 7);
            p.cs[j + 7] = Q + eta - ((buf[i + 2] >> 5) & 7);
            j += 8;
        }
    } else { // eta == 4
        // 4 bits per coefficient: unpack 2 coefficients from 1 byte
        var j: usize = 0;
        for (0..buf.len) |i| {
            p.cs[j] = Q + eta - (buf[i] & 15);
            p.cs[j + 1] = Q + eta - (buf[i] >> 4);
            j += 2;
        }
    }

    return p;
}

/// Pack polynomial with coefficients < 1024 (T1) into bytes.
/// Packs 10 bits per coefficient: 4 coefficients into 5 bytes.
/// Assumes coefficients are normalized.
fn polyPackT1(p: Poly, buf: []u8) void {
    var j: usize = 0;
    var i: usize = 0;
    while (i < buf.len) : (i += 5) {
        buf[i] = @truncate(p.cs[j]);
        buf[i + 1] = @truncate((p.cs[j] >> 8) | (p.cs[j + 1] << 2));
        buf[i + 2] = @truncate((p.cs[j + 1] >> 6) | (p.cs[j + 2] << 4));
        buf[i + 3] = @truncate((p.cs[j + 2] >> 4) | (p.cs[j + 3] << 6));
        buf[i + 4] = @truncate(p.cs[j + 3] >> 2);
        j += 4;
    }
}

/// Unpack polynomial with coefficients < 1024 (T1) from bytes.
/// Output coefficients will be normalized.
fn polyUnpackT1(buf: []const u8) Poly {
    var p = Poly.zero;
    var j: usize = 0;
    var i: usize = 0;
    while (i < buf.len) : (i += 5) {
        p.cs[j] = (@as(u32, buf[i]) | (@as(u32, buf[i + 1]) << 8)) & 0x3ff;
        p.cs[j + 1] = ((@as(u32, buf[i + 1]) >> 2) | (@as(u32, buf[i + 2]) << 6)) & 0x3ff;
        p.cs[j + 2] = ((@as(u32, buf[i + 2]) >> 4) | (@as(u32, buf[i + 3]) << 4)) & 0x3ff;
        p.cs[j + 3] = ((@as(u32, buf[i + 3]) >> 6) | (@as(u32, buf[i + 4]) << 2)) & 0x3ff;
        j += 4;
    }
    return p;
}

/// Pack polynomial with coefficients in (-2^(D-1), 2^(D-1)] (T0) into bytes.
/// Packs 13 bits per coefficient: 8 coefficients into 13 bytes.
/// Assumes coefficients are not normalized, but in (q-2^(D-1), q+2^(D-1)].
fn polyPackT0(p: Poly, buf: []u8) void {
    const bound = 1 << (D - 1);
    var j: usize = 0;
    var i: usize = 0;
    while (i < buf.len) : (i += 13) {
        const p0 = Q + bound - p.cs[j];
        const p1 = Q + bound - p.cs[j + 1];
        const p2 = Q + bound - p.cs[j + 2];
        const p3 = Q + bound - p.cs[j + 3];
        const p4 = Q + bound - p.cs[j + 4];
        const p5 = Q + bound - p.cs[j + 5];
        const p6 = Q + bound - p.cs[j + 6];
        const p7 = Q + bound - p.cs[j + 7];

        buf[i] = @truncate(p0 >> 0);
        buf[i + 1] = @truncate((p0 >> 8) | (p1 << 5));
        buf[i + 2] = @truncate(p1 >> 3);
        buf[i + 3] = @truncate((p1 >> 11) | (p2 << 2));
        buf[i + 4] = @truncate((p2 >> 6) | (p3 << 7));
        buf[i + 5] = @truncate(p3 >> 1);
        buf[i + 6] = @truncate((p3 >> 9) | (p4 << 4));
        buf[i + 7] = @truncate(p4 >> 4);
        buf[i + 8] = @truncate((p4 >> 12) | (p5 << 1));
        buf[i + 9] = @truncate((p5 >> 7) | (p6 << 6));
        buf[i + 10] = @truncate(p6 >> 2);
        buf[i + 11] = @truncate((p6 >> 10) | (p7 << 3));
        buf[i + 12] = @truncate(p7 >> 5);

        j += 8;
    }
}

/// Unpack polynomial with coefficients in (-2^(D-1), 2^(D-1)] (T0) from bytes.
/// Output coefficients will not be normalized, but in (-2^(D-1), 2^(D-1)].
fn polyUnpackT0(buf: []const u8) Poly {
    const bound = 1 << (D - 1);
    var p = Poly.zero;
    var j: usize = 0;
    var i: usize = 0;
    while (i < buf.len) : (i += 13) {
        p.cs[j] = Q + bound - ((@as(u32, buf[i]) | (@as(u32, buf[i + 1]) << 8)) & 0x1fff);
        p.cs[j + 1] = Q + bound - (((@as(u32, buf[i + 1]) >> 5) | (@as(u32, buf[i + 2]) << 3) | (@as(u32, buf[i + 3]) << 11)) & 0x1fff);
        p.cs[j + 2] = Q + bound - (((@as(u32, buf[i + 3]) >> 2) | (@as(u32, buf[i + 4]) << 6)) & 0x1fff);
        p.cs[j + 3] = Q + bound - (((@as(u32, buf[i + 4]) >> 7) | (@as(u32, buf[i + 5]) << 1) | (@as(u32, buf[i + 6]) << 9)) & 0x1fff);
        p.cs[j + 4] = Q + bound - (((@as(u32, buf[i + 6]) >> 4) | (@as(u32, buf[i + 7]) << 4) | (@as(u32, buf[i + 8]) << 12)) & 0x1fff);
        p.cs[j + 5] = Q + bound - (((@as(u32, buf[i + 8]) >> 1) | (@as(u32, buf[i + 9]) << 7)) & 0x1fff);
        p.cs[j + 6] = Q + bound - (((@as(u32, buf[i + 9]) >> 6) | (@as(u32, buf[i + 10]) << 2) | (@as(u32, buf[i + 11]) << 10)) & 0x1fff);
        p.cs[j + 7] = Q + bound - ((@as(u32, buf[i + 11]) >> 3) | (@as(u32, buf[i + 12]) << 5));
        j += 8;
    }
    return p;
}

/// Convert coefficient from centered representation to non-negative.
/// Transforms value from [0,γ₁] ∪ (Q-γ₁, Q) to [0, 2γ₁).
fn centeredToPositive(val: u32, comptime gamma1: u32) u32 {
    var result = gamma1 -% val;
    result +%= (signMask(u32, result) & Q);
    return result;
}

/// Pack polynomial with coefficients in (-gamma1, gamma1] into bytes.
/// For gamma1_bits=17: packs 18 bits per coefficient (4 coefficients into 9 bytes)
/// For gamma1_bits=19: packs 20 bits per coefficient (2 coefficients into 5 bytes)
/// Assumes coefficients are normalized.
fn polyPackLeGamma1(p: Poly, comptime gamma1_bits: u8, buf: []u8) void {
    const gamma1: u32 = @as(u32, 1) << gamma1_bits;

    if (gamma1_bits == 17) {
        // Pack 4 coefficients into 9 bytes (18 bits each)
        var j: usize = 0;
        var i: usize = 0;
        while (i < buf.len) : (i += 9) {
            // Convert from [0,γ₁] ∪ (Q-γ₁, Q) to [0, 2γ₁)
            const p0 = centeredToPositive(p.cs[j], gamma1);
            const p1 = centeredToPositive(p.cs[j + 1], gamma1);
            const p2 = centeredToPositive(p.cs[j + 2], gamma1);
            const p3 = centeredToPositive(p.cs[j + 3], gamma1);

            buf[i] = @truncate(p0);
            buf[i + 1] = @truncate(p0 >> 8);
            buf[i + 2] = @truncate((p0 >> 16) | (p1 << 2));
            buf[i + 3] = @truncate(p1 >> 6);
            buf[i + 4] = @truncate((p1 >> 14) | (p2 << 4));
            buf[i + 5] = @truncate(p2 >> 4);
            buf[i + 6] = @truncate((p2 >> 12) | (p3 << 6));
            buf[i + 7] = @truncate(p3 >> 2);
            buf[i + 8] = @truncate(p3 >> 10);

            j += 4;
        }
    } else if (gamma1_bits == 19) {
        // Pack 2 coefficients into 5 bytes (20 bits each)
        var j: usize = 0;
        var i: usize = 0;
        while (i < buf.len) : (i += 5) {
            const p0 = centeredToPositive(p.cs[j], gamma1);
            const p1 = centeredToPositive(p.cs[j + 1], gamma1);

            buf[i] = @truncate(p0);
            buf[i + 1] = @truncate(p0 >> 8);
            buf[i + 2] = @truncate((p0 >> 16) | (p1 << 4));
            buf[i + 3] = @truncate(p1 >> 4);
            buf[i + 4] = @truncate(p1 >> 12);

            j += 2;
        }
    } else {
        @compileError("gamma1_bits must be 17 or 19");
    }
}

/// Unpack polynomial with coefficients in (-gamma1, gamma1] from bytes.
/// Output coefficients will be normalized.
fn polyUnpackLeGamma1(comptime gamma1_bits: u8, buf: []const u8) Poly {
    const gamma1: u32 = @as(u32, 1) << gamma1_bits;
    var p = Poly.zero;

    if (gamma1_bits == 17) {
        // Unpack 4 coefficients from 9 bytes (18 bits each)
        var j: usize = 0;
        var i: usize = 0;
        while (i < buf.len) : (i += 9) {
            var p0 = @as(u32, buf[i]) | (@as(u32, buf[i + 1]) << 8) | ((@as(u32, buf[i + 2]) & 0x3) << 16);
            var p1 = (@as(u32, buf[i + 2]) >> 2) | (@as(u32, buf[i + 3]) << 6) | ((@as(u32, buf[i + 4]) & 0xf) << 14);
            var p2 = (@as(u32, buf[i + 4]) >> 4) | (@as(u32, buf[i + 5]) << 4) | ((@as(u32, buf[i + 6]) & 0x3f) << 12);
            var p3 = (@as(u32, buf[i + 6]) >> 6) | (@as(u32, buf[i + 7]) << 2) | (@as(u32, buf[i + 8]) << 10);

            // Convert from [0, 2γ₁) to (-γ₁, γ₁]
            p0 = centeredToPositive(p0, gamma1);
            p1 = centeredToPositive(p1, gamma1);
            p2 = centeredToPositive(p2, gamma1);
            p3 = centeredToPositive(p3, gamma1);

            p.cs[j] = p0;
            p.cs[j + 1] = p1;
            p.cs[j + 2] = p2;
            p.cs[j + 3] = p3;

            j += 4;
        }
    } else if (gamma1_bits == 19) {
        // Unpack 2 coefficients from 5 bytes (20 bits each)
        var j: usize = 0;
        var i: usize = 0;
        while (i < buf.len) : (i += 5) {
            var p0 = @as(u32, buf[i]) | (@as(u32, buf[i + 1]) << 8) | ((@as(u32, buf[i + 2]) & 0xf) << 16);
            var p1 = (@as(u32, buf[i + 2]) >> 4) | (@as(u32, buf[i + 3]) << 4) | (@as(u32, buf[i + 4]) << 12);

            p0 = centeredToPositive(p0, gamma1);
            p1 = centeredToPositive(p1, gamma1);

            p.cs[j] = p0;
            p.cs[j + 1] = p1;

            j += 2;
        }
    } else {
        @compileError("gamma1_bits must be 17 or 19");
    }

    return p;
}

/// Pack W1 polynomial for verification.
/// For gamma1_bits=17: packs 6 bits per coefficient (4 coefficients into 3 bytes)
/// For gamma1_bits=19: packs 4 bits per coefficient (2 coefficients into 1 byte)
/// Assumes coefficients are normalized.
fn polyPackW1(p: Poly, comptime gamma1_bits: u8, buf: []u8) void {
    if (gamma1_bits == 17) {
        // Pack 4 coefficients into 3 bytes (6 bits each)
        var j: usize = 0;
        var i: usize = 0;
        while (i < buf.len) : (i += 3) {
            buf[i] = @truncate(p.cs[j] | (p.cs[j + 1] << 6));
            buf[i + 1] = @truncate((p.cs[j + 1] >> 2) | (p.cs[j + 2] << 4));
            buf[i + 2] = @truncate((p.cs[j + 2] >> 4) | (p.cs[j + 3] << 2));
            j += 4;
        }
    } else if (gamma1_bits == 19) {
        // Pack 2 coefficients into 1 byte (4 bits each) - equivalent to packLe16
        var j: usize = 0;
        for (0..buf.len) |i| {
            buf[i] = @truncate(p.cs[j] | (p.cs[j + 1] << 4));
            j += 2;
        }
    } else {
        @compileError("gamma1_bits must be 17 or 19");
    }
}

fn polyDeriveUniform(seed: *const [32]u8, nonce: u16) Poly {
    var domain_sep: [2]u8 = undefined;
    domain_sep[0] = @truncate(nonce);
    domain_sep[1] = @truncate(nonce >> 8);

    return sampleUniformRejection(
        Poly,
        Q,
        23,
        N,
        seed,
        &domain_sep,
    );
}

/// Sample p uniformly with coefficients of norm less than or equal to η,
/// using the given seed and nonce with SHAKE-256.
/// The polynomial will not be normalized, but will have coefficients in [q-η, q+η].
/// FIPS 204: ExpandS (Algorithm 27)
fn expandS(comptime eta: u8, seed: *const [64]u8, nonce: u16) Poly {
    comptime {
        if (eta != 2 and eta != 4) {
            @compileError("eta must be 2 or 4");
        }
    }

    var p = Poly.zero;
    var i: usize = 0;

    var buf: [sha3.Shake256.block_length]u8 = undefined; // SHAKE-256 rate is 136 bytes

    // Prepare input: seed || nonce (little-endian u16)
    var input: [66]u8 = undefined;
    @memcpy(input[0..64], seed);
    input[64] = @truncate(nonce);
    input[65] = @truncate(nonce >> 8);

    var h = sha3.Shake256.init(.{});
    h.update(&input);

    while (i < N) {
        h.squeeze(&buf);

        // Process buffer: extract two samples per byte (4-bit nibbles)
        var j: usize = 0;
        while (j < buf.len and i < N) : (j += 1) {
            var t1 = @as(u32, buf[j]) & 15;
            var t2 = @as(u32, buf[j]) >> 4;

            if (eta == 2) {
                // For eta=2: reject if t > 14, then reduce mod 5
                if (t1 <= 14) {
                    t1 -%= ((205 * t1) >> 10) * 5; // reduce mod 5
                    p.cs[i] = Q + eta - t1;
                    i += 1;
                }
                if (t2 <= 14 and i < N) {
                    t2 -%= ((205 * t2) >> 10) * 5; // reduce mod 5
                    p.cs[i] = Q + eta - t2;
                    i += 1;
                }
            } else if (eta == 4) {
                // For eta=4: accept if t <= 2*eta = 8
                if (t1 <= 2 * eta) {
                    p.cs[i] = Q + eta - t1;
                    i += 1;
                }
                if (t2 <= 2 * eta and i < N) {
                    p.cs[i] = Q + eta - t2;
                    i += 1;
                }
            }
        }
    }

    return p;
}

/// Sample p uniformly with τ non-zero coefficients in {Q-1, 1} using SHAKE-256.
/// This creates a "ball" polynomial with exactly tau non-zero ±1 coefficients.
/// The polynomial will be normalized with coefficients in {0, 1, Q-1}.
/// FIPS 204: SampleInBall (Algorithm 18)
fn sampleInBall(comptime tau: u16, seed: []const u8) Poly {
    var p = Poly.zero;

    var buf: [sha3.Shake256.block_length]u8 = undefined; // SHAKE-256 rate is 136 bytes

    var h = sha3.Shake256.init(.{});
    h.update(seed);
    h.squeeze(&buf);

    // Extract signs from first 8 bytes
    var signs: u64 = 0;
    for (0..8) |j| {
        signs |= @as(u64, buf[j]) << @intCast(j * 8);
    }
    var buf_off: usize = 8;

    // Generate tau non-zero coefficients using Fisher-Yates shuffle
    // Start with N-tau zeros, then add tau ±1 values
    var i: u16 = N - tau;
    while (i < N) : (i += 1) {
        var b: u16 = undefined;

        // Find location using rejection sampling
        while (true) {
            if (buf_off >= buf.len) {
                h.squeeze(&buf);
                buf_off = 0;
            }

            b = buf[buf_off];
            buf_off += 1;

            if (b <= i) {
                break;
            }
        }

        // Shuffle: move existing value to position i
        p.cs[i] = p.cs[b];

        // Set position b to ±1 based on sign bit
        p.cs[b] = 1;
        const sign_bit: u1 = @truncate(signs);
        const mask = bitMask(u32, sign_bit);
        p.cs[b] ^= mask & (1 | (Q - 1));
        signs >>= 1;
    }

    return p;
}

/// Sample a polynomial with coefficients uniformly distributed in (-gamma1, gamma1]
/// Used for sampling the masking vector y during signing
/// FIPS 204: ExpandMask (Algorithm 28)
fn expandMask(comptime gamma1_bits: u8, seed: *const [64]u8, nonce: u16) Poly {
    const packed_size = ((gamma1_bits + 1) * N) / 8;
    var buf: [packed_size]u8 = undefined;

    // Construct IV: seed || nonce (little-endian)
    var iv: [66]u8 = undefined;
    @memcpy(iv[0..64], seed);
    iv[64] = @truncate(nonce & 0xFF);
    iv[65] = @truncate(nonce >> 8);

    var h = sha3.Shake256.init(.{});
    h.update(&iv);
    h.squeeze(&buf);

    // Unpack the polynomial
    return polyUnpackLeGamma1(gamma1_bits, &buf);
}

fn MLDSAImpl(comptime p: Params) type {
    return struct {
        pub const params = p;
        pub const name = p.name;
        pub const gamma1: u32 = @as(u32, 1) << p.gamma1_bits;
        pub const beta: u32 = p.tau * p.eta;
        pub const alpha: u32 = 2 * p.gamma2;

        const Self = @This();
        const PolyVecL = PolyVec(p.l);
        const PolyVecK = PolyVec(p.k);
        const MatKxL = Mat(p.k, p.l);

        /// Length of the seed used for deterministic key generation (32 bytes).
        pub const seed_length: usize = 32;

        /// Length (in bytes) of optional random bytes, for non-deterministic signatures.
        pub const noise_length = 32;

        /// Size of an encoded public key in bytes.
        pub const public_key_bytes: usize = 32 + polyT1PackedSize() * p.k;

        /// Size of an encoded secret key in bytes.
        pub const private_key_bytes: usize = 32 + 32 + p.tr_size +
            polyLeqEtaPackedSize() * (p.l + p.k) + polyT0PackedSize() * p.k;

        /// Size of an encoded signature in bytes.
        pub const signature_bytes: usize = p.ctilde_size +
            polyLeGamma1PackedSize() * p.l + p.omega + p.k;

        // Packed sizes for different polynomial representations
        fn polyLeqEtaPackedSize() usize {
            // For eta=2: 3 bits per coefficient (values in [0,4])
            // For eta=4: 4 bits per coefficient (values in [0,8])
            const double_eta_bits = if (p.eta == 2) 3 else 4;
            return (N * double_eta_bits) / 8;
        }

        fn polyLeGamma1PackedSize() usize {
            return ((p.gamma1_bits + 1) * N) / 8;
        }

        fn polyT1PackedSize() usize {
            return (N * (Q_BITS - D)) / 8;
        }

        fn polyT0PackedSize() usize {
            return (N * D) / 8;
        }

        fn polyW1PackedSize() usize {
            return (N * (Q_BITS - p.gamma1_bits)) / 8;
        }

        /// Helper function to compute CRH (Collision Resistant Hash) using SHAKE-256.
        /// This consolidates the repeated pattern of init-update-squeeze for hash operations.
        fn crh(comptime outsize: usize, inputs: anytype) [outsize]u8 {
            var h = sha3.Shake256.init(.{});
            inline for (inputs) |input| {
                h.update(input);
            }
            var out: [outsize]u8 = undefined;
            h.squeeze(&out);
            return out;
        }

        /// Helper function to compute t = As1 + s2.
        /// This is used during key generation and public key reconstruction.
        fn computeT(A: MatKxL, s1_hat: PolyVecL, s2: PolyVecK) PolyVecK {
            const t = A.mulVec(s1_hat).add(s2);
            return t.normalize();
        }

        /// ML-DSA public key
        pub const PublicKey = struct {
            /// Size of the encoded public key in bytes
            pub const encoded_length: usize = 32 + polyT1PackedSize() * p.k;

            rho: [32]u8, // Seed for matrix A
            t1: PolyVecK, // High bits of t = As1 + s2

            // Cached values
            t1_packed: [polyT1PackedSize() * p.k]u8,
            A: MatKxL,
            tr: [p.tr_size]u8, // CRH(rho || t1)

            /// Encode public key to bytes
            pub fn toBytes(self: PublicKey) [encoded_length]u8 {
                var out: [encoded_length]u8 = undefined;
                @memcpy(out[0..32], &self.rho);
                @memcpy(out[32..], &self.t1_packed);
                return out;
            }

            /// Decode public ke
```
