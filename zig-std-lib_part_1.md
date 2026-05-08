```
Directory Structure:

└── ./
    └── std
        ├── Build
        │   ├── Cache
        │   │   ├── DepTokenizer.zig
        │   │   ├── Directory.zig
        │   │   └── Path.zig
        │   ├── Step
        │   │   ├── CheckFile.zig
        │   │   ├── CheckObject.zig
        │   │   ├── Compile.zig
        │   │   ├── ConfigHeader.zig
        │   │   ├── Fail.zig
        │   │   ├── Fmt.zig
        │   │   ├── InstallArtifact.zig
        │   │   ├── InstallDir.zig
        │   │   ├── InstallFile.zig
        │   │   ├── ObjCopy.zig
        │   │   ├── Options.zig
        │   │   ├── Run.zig
        │   │   ├── TranslateC.zig
        │   │   ├── UpdateSourceFiles.zig
        │   │   └── WriteFile.zig
        │   ├── Watch
        │   │   └── FsEvents.zig
        │   ├── abi.zig
        │   ├── Cache.zig
        │   ├── Fuzz.zig
        │   ├── Module.zig
        │   ├── Step.zig
        │   ├── Watch.zig
        │   └── WebServer.zig
        ├── builtin
        │   └── assembly.zig
        ├── c
        │   ├── darwin
        │   │   └── dispatch.zig
        │   ├── darwin.zig
        │   ├── dragonfly.zig
        │   ├── freebsd.zig
        │   ├── haiku.zig
        │   ├── illumos.zig
        │   ├── netbsd.zig
        │   ├── openbsd.zig
        │   └── serenity.zig
        ├── compress
        │   ├── flate
        │   │   ├── Compress.zig
        │   │   ├── Decompress.zig
        │   │   └── token.zig
        │   ├── lzma
        │   │   └── test.zig
        │   ├── xz
        │   │   ├── Decompress.zig
        │   │   └── test.zig
        │   ├── zstd
        │   │   └── Decompress.zig
        │   ├── flate.zig
        │   ├── lzma.zig
        │   ├── lzma2.zig
        │   ├── xz.zig
        │   └── zstd.zig
        ├── crypto
        │   ├── 25519
        │   │   ├── curve25519.zig
        │   │   ├── ed25519.zig
        │   │   ├── edwards25519.zig
        │   │   ├── field.zig
        │   │   ├── ristretto255.zig
        │   │   ├── scalar.zig
        │   │   └── x25519.zig
        │   ├── aes
        │   │   ├── aesni.zig
        │   │   ├── armcrypto.zig
        │   │   └── soft.zig
        │   ├── Certificate
        │   │   ├── Bundle
        │   │   │   └── macos.zig
        │   │   ├── Bundle.zig
        │   │   └── Chain.zig
        │   ├── codecs
        │   │   ├── asn1
        │   │   │   ├── der
        │   │   │   │   ├── testdata
        │   │   │   │   │   ├── all_types.der
        │   │   │   │   │   └── id_ecc.pub.der
        │   │   │   │   ├── ArrayListReverse.zig
        │   │   │   │   ├── Decoder.zig
        │   │   │   │   └── Encoder.zig
        │   │   │   ├── der.zig
        │   │   │   ├── Oid.zig
        │   │   │   └── test.zig
        │   │   ├── asn1.zig
        │   │   └── base64_hex_ct.zig
        │   ├── pcurves
        │   │   ├── p256
        │   │   │   ├── field.zig
        │   │   │   ├── p256_64.zig
        │   │   │   ├── p256_scalar_64.zig
        │   │   │   └── scalar.zig
        │   │   ├── p384
        │   │   │   ├── field.zig
        │   │   │   ├── p384_64.zig
        │   │   │   ├── p384_scalar_64.zig
        │   │   │   └── scalar.zig
        │   │   ├── secp256k1
        │   │   │   ├── field.zig
        │   │   │   ├── scalar.zig
        │   │   │   ├── secp256k1_64.zig
        │   │   │   └── secp256k1_scalar_64.zig
        │   │   ├── tests
        │   │   │   ├── p256.zig
        │   │   │   ├── p384.zig
        │   │   │   └── secp256k1.zig
        │   │   ├── common.zig
        │   │   ├── p256.zig
        │   │   ├── p384.zig
        │   │   └── secp256k1.zig
        │   ├── tls
        │   │   └── Client.zig
        │   ├── aegis.zig
        │   ├── aes_ccm.zig
        │   ├── aes_gcm_siv.zig
        │   ├── aes_gcm.zig
        │   ├── aes_ocb.zig
        │   ├── aes_siv.zig
        │   ├── aes.zig
        │   ├── argon2.zig
        │   ├── ascon.zig
        │   ├── bcrypt.zig
        │   ├── benchmark.zig
        │   ├── blake2.zig
        │   ├── blake3.zig
        │   ├── cbc_mac.zig
        │   ├── Certificate.zig
        │   ├── chacha20.zig
        │   ├── cmac.zig
        │   ├── codecs.zig
        │   ├── ecdsa.zig
        │   ├── errors.zig
        │   ├── ff.zig
        │   ├── ghash_polyval.zig
        │   ├── hash_composition.zig
        │   ├── hkdf.zig
        │   ├── hmac.zig
        │   ├── hybrid_kem.zig
        │   ├── isap.zig
        │   ├── kangarootwelve.zig
        │   ├── keccak_p.zig
        │   ├── md5.zig
        │   ├── ml_dsa.zig
        │   ├── ml_kem.zig
        │   ├── modes.zig
        │   ├── pbkdf2.zig
        │   ├── phc_encoding.zig
        │   ├── poly1305.zig
        │   ├── salsa20.zig
        │   ├── scrypt.zig
        │   ├── Sha1.zig
        │   ├── sha2.zig
        │   ├── sha3.zig
        │   ├── siphash.zig
        │   ├── test.zig
        │   ├── timing_safe.zig
        │   └── tls.zig
        ├── debug
        │   ├── Dwarf
        │   │   ├── Unwind
        │   │   │   └── VirtualMachine.zig
        │   │   ├── expression.zig
        │   │   ├── SelfUnwinder.zig
        │   │   └── Unwind.zig
        │   ├── SelfInfo
        │   │   ├── Elf.zig
        │   │   ├── MachO.zig
        │   │   └── Windows.zig
        │   ├── Coverage.zig
        │   ├── cpu_context.zig
        │   ├── Dwarf.zig
        │   ├── ElfFile.zig
        │   ├── Info.zig
        │   ├── MachOFile.zig
        │   ├── no_panic.zig
        │   ├── Pdb.zig
        │   └── simple_panic.zig
        ├── dwarf
        │   ├── AT.zig
        │   ├── ATE.zig
        │   ├── EH.zig
        │   ├── FORM.zig
        │   ├── LANG.zig
        │   ├── OP.zig
        │   └── TAG.zig
        ├── fmt
        │   ├── parse_float
        │   │   ├── common.zig
        │   │   ├── convert_eisel_lemire.zig
        │   │   ├── convert_fast.zig
        │   │   ├── convert_hex.zig
        │   │   ├── convert_slow.zig
        │   │   ├── decimal.zig
        │   │   ├── FloatInfo.zig
        │   │   ├── FloatStream.zig
        │   │   └── parse.zig
        │   ├── float.zig
        │   └── parse_float.zig
        ├── fs
        │   ├── path.zig
        │   └── test.zig
        ├── hash
        │   ├── crc
        │   │   ├── impl.zig
        │   │   └── test.zig
        │   ├── Adler32.zig
        │   ├── auto_hash.zig
        │   ├── benchmark.zig
        │   ├── cityhash.zig
        │   ├── crc.zig
        │   ├── fnv.zig
        │   ├── murmur.zig
        │   ├── verify.zig
        │   ├── wyhash.zig
        │   └── xxhash.zig
        ├── heap
        │   ├── ArenaAllocator.zig
        │   ├── BrkAllocator.zig
        │   ├── debug_allocator.zig
        │   ├── FixedBufferAllocator.zig
        │   ├── memory_pool.zig
        │   ├── PageAllocator.zig
        │   └── SmpAllocator.zig
        ├── http
        │   ├── ChunkParser.zig
        │   ├── Client.zig
        │   ├── HeaderIterator.zig
        │   ├── HeadParser.zig
        │   ├── Server.zig
        │   └── test.zig
        ├── Io
        │   ├── File
        │   │   ├── Atomic.zig
        │   │   ├── MemoryMap.zig
        │   │   ├── MultiReader.zig
        │   │   ├── Reader.zig
        │   │   └── Writer.zig
        │   ├── net
        │   │   ├── HostName.zig
        │   │   └── test.zig
        │   ├── Reader
        │   │   └── Limited.zig
        │   ├── Threaded
        │   │   └── test.zig
        │   ├── Dir.zig
        │   ├── Dispatch.zig
        │   ├── fiber.zig
        │   ├── File.zig
        │   ├── Kqueue.zig
        │   ├── net.zig
        │   ├── Reader.zig
        │   ├── RwLock.zig
        │   ├── Semaphore.zig
        │   ├── Terminal.zig
        │   ├── test.zig
        │   ├── Threaded.zig
        │   ├── Uring.zig
        │   └── Writer.zig
        ├── json
        │   ├── dynamic_test.zig
        │   ├── dynamic.zig
        │   ├── hashmap_test.zig
        │   ├── hashmap.zig
        │   ├── JSONTestSuite_test.zig
        │   ├── scanner_test.zig
        │   ├── Scanner.zig
        │   ├── static_test.zig
        │   ├── static.zig
        │   ├── Stringify.zig
        │   └── test.zig
        ├── math
        │   ├── big
        │   │   ├── int_test.zig
        │   │   └── int.zig
        │   ├── complex
        │   │   ├── abs.zig
        │   │   ├── acos.zig
        │   │   ├── acosh.zig
        │   │   ├── arg.zig
        │   │   ├── asin.zig
        │   │   ├── asinh.zig
        │   │   ├── atan.zig
        │   │   ├── atanh.zig
        │   │   ├── conj.zig
        │   │   ├── cos.zig
        │   │   ├── cosh.zig
        │   │   ├── exp.zig
        │   │   ├── ldexp.zig
        │   │   ├── log.zig
        │   │   ├── pow.zig
        │   │   ├── proj.zig
        │   │   ├── sin.zig
        │   │   ├── sinh.zig
        │   │   ├── sqrt.zig
        │   │   ├── tan.zig
        │   │   └── tanh.zig
        │   ├── acos.zig
        │   ├── acosh.zig
        │   ├── asin.zig
        │   ├── asinh.zig
        │   ├── atan.zig
        │   ├── atan2.zig
        │   ├── atanh.zig
        │   ├── big.zig
        │   ├── cbrt.zig
        │   ├── complex.zig
        │   ├── copysign.zig
        │   ├── cosh.zig
        │   ├── expm1.zig
        │   ├── expo2.zig
        │   ├── float.zig
        │   ├── frexp.zig
        │   ├── gamma.zig
        │   ├── gcd.zig
        │   ├── hypot.zig
        │   ├── ilogb.zig
        │   ├── isfinite.zig
        │   ├── isinf.zig
        │   ├── isnan.zig
        │   ├── isnormal.zig
        │   ├── iszero.zig
        │   ├── lcm.zig
        │   ├── ldexp.zig
        │   ├── log_int.zig
        │   ├── log.zig
        │   ├── log10.zig
        │   ├── log1p.zig
        │   ├── log2.zig
        │   ├── modf.zig
        │   ├── nextafter.zig
        │   ├── pow.zig
        │   ├── powi.zig
        │   ├── scalbn.zig
        │   ├── signbit.zig
        │   ├── sinh.zig
        │   ├── sqrt.zig
        │   └── tanh.zig
        ├── mem
        │   └── Allocator.zig
        ├── meta
        │   └── trailer_flags.zig
        ├── os
        │   ├── linux
        │   │   ├── bpf
        │   │   │   ├── btf_ext.zig
        │   │   │   ├── btf.zig
        │   │   │   ├── helpers.zig
        │   │   │   └── kern.zig
        │   │   ├── IoUring
        │   │   │   └── test.zig
        │   │   ├── aarch64.zig
        │   │   ├── arm.zig
        │   │   ├── bpf.zig
        │   │   ├── hexagon.zig
        │   │   ├── io_uring_sqe.zig
        │   │   ├── ioctl.zig
        │   │   ├── IoUring.zig
        │   │   ├── loongarch32.zig
        │   │   ├── loongarch64.zig
        │   │   ├── m68k.zig
        │   │   ├── mips.zig
        │   │   ├── mips64.zig
        │   │   ├── mipsn32.zig
        │   │   ├── or1k.zig
        │   │   ├── powerpc.zig
        │   │   ├── powerpc64.zig
        │   │   ├── riscv32.zig
        │   │   ├── riscv64.zig
        │   │   ├── s390x.zig
        │   │   ├── seccomp.zig
        │   │   ├── sparc64.zig
        │   │   ├── syscalls.zig
        │   │   ├── test.zig
        │   │   ├── thumb.zig
        │   │   ├── tls.zig
        │   │   ├── vdso.zig
        │   │   ├── x32.zig
        │   │   ├── x86_64.zig
        │   │   └── x86.zig
        │   ├── plan9
        │   │   └── x86_64.zig
        │   ├── uefi
        │   │   ├── protocol
        │   │   │   ├── absolute_pointer.zig
        │   │   │   ├── block_io.zig
        │   │   │   ├── device_path.zig
        │   │   │   ├── edid.zig
        │   │   │   ├── file.zig
        │   │   │   ├── graphics_output.zig
        │   │   │   ├── hii_database.zig
        │   │   │   ├── hii_popup.zig
        │   │   │   ├── ip6_config.zig
        │   │   │   ├── ip6.zig
        │   │   │   ├── loaded_image.zig
        │   │   │   ├── managed_network.zig
        │   │   │   ├── rng.zig
        │   │   │   ├── serial_io.zig
        │   │   │   ├── service_binding.zig
        │   │   │   ├── shell_parameters.zig
        │   │   │   ├── simple_file_system.zig
        │   │   │   ├── simple_network.zig
        │   │   │   ├── simple_pointer.zig
        │   │   │   ├── simple_text_input_ex.zig
        │   │   │   ├── simple_text_input.zig
        │   │   │   ├── simple_text_output.zig
        │   │   │   └── udp6.zig
        │   │   ├── tables
        │   │   │   ├── boot_services.zig
        │   │   │   ├── configuration_table.zig
        │   │   │   ├── runtime_services.zig
        │   │   │   ├── system_table.zig
        │   │   │   └── table_header.zig
        │   │   ├── device_path.zig
        │   │   ├── hii.zig
        │   │   ├── pool_allocator.zig
        │   │   ├── protocol.zig
        │   │   ├── status.zig
        │   │   └── tables.zig
        │   ├── windows
        │   │   ├── crypt32.zig
        │   │   ├── kernel32.zig
        │   │   ├── lang.zig
        │   │   ├── nls.zig
        │   │   ├── ntdll.zig
        │   │   ├── ntstatus.zig
        │   │   ├── sublang.zig
        │   │   ├── tls.zig
        │   │   ├── win32error.zig
        │   │   └── ws2_32.zig
        │   ├── emscripten.zig
        │   ├── linux.zig
        │   ├── plan9.zig
        │   ├── uefi.zig
        │   ├── wasi.zig
        │   └── windows.zig
        ├── posix
        │   └── test.zig
        ├── process
        │   ├── Args.zig
        │   ├── Child.zig
        │   ├── Environ.zig
        │   └── Preopens.zig
        ├── Random
        │   ├── Ascon.zig
        │   ├── benchmark.zig
        │   ├── ChaCha.zig
        │   ├── Isaac64.zig
        │   ├── lcg.zig
        │   ├── Pcg.zig
        │   ├── RomuTrio.zig
        │   ├── Sfc64.zig
        │   ├── SplitMix64.zig
        │   ├── test.zig
        │   ├── Xoroshiro128.zig
        │   ├── Xoshiro256.zig
        │   └── ziggurat.zig
        ├── sort
        │   ├── block.zig
        │   └── pdq.zig
        ├── tar
        │   ├── test.zig
        │   └── Writer.zig
        ├── Target
        │   ├── aarch64.zig
        │   ├── alpha.zig
        │   ├── amdgcn.zig
        │   ├── arc.zig
        │   ├── arm.zig
        │   ├── avr.zig
        │   ├── bpf.zig
        │   ├── csky.zig
        │   ├── generic.zig
        │   ├── hexagon.zig
        │   ├── hppa.zig
        │   ├── kvx.zig
        │   ├── lanai.zig
        │   ├── loongarch.zig
        │   ├── m68k.zig
        │   ├── mips.zig
        │   ├── msp430.zig
        │   ├── nvptx.zig
        │   ├── powerpc.zig
        │   ├── propeller.zig
        │   ├── Query.zig
        │   ├── riscv.zig
        │   ├── s390x.zig
        │   ├── sparc.zig
        │   ├── spirv.zig
        │   ├── ve.zig
        │   ├── wasm.zig
        │   ├── x86.zig
        │   ├── xcore.zig
        │   └── xtensa.zig
        ├── testing
        │   ├── FailingAllocator.zig
        │   └── Smith.zig
        ├── time
        │   └── epoch.zig
        ├── unicode
        │   └── throughput_test.zig
        ├── valgrind
        │   ├── cachegrind.zig
        │   ├── callgrind.zig
        │   └── memcheck.zig
        ├── zig
        │   ├── Ast
        │   │   └── Render.zig
        │   ├── c_translation
        │   │   ├── builtins.zig
        │   │   └── helpers.zig
        │   ├── llvm
        │   │   ├── bitcode_writer.zig
        │   │   ├── BitcodeReader.zig
        │   │   ├── Builder.zig
        │   │   └── ir.zig
        │   ├── system
        │   │   ├── darwin
        │   │   │   └── macos.zig
        │   │   ├── arm.zig
        │   │   ├── darwin.zig
        │   │   ├── linux.zig
        │   │   ├── loongarch.zig
        │   │   ├── NativePaths.zig
        │   │   ├── windows.zig
        │   │   └── x86.zig
        │   ├── Ast.zig
        │   ├── AstGen.zig
        │   ├── AstRlAnnotate.zig
        │   ├── AstSmith.zig
        │   ├── BuiltinFn.zig
        │   ├── Client.zig
        │   ├── ErrorBundle.zig
        │   ├── LibCDirs.zig
        │   ├── LibCInstallation.zig
        │   ├── llvm.zig
        │   ├── number_literal.zig
        │   ├── Parse.zig
        │   ├── parser_test.zig
        │   ├── perf_test.zig
        │   ├── primitives.zig
        │   ├── Server.zig
        │   ├── string_literal.zig
        │   ├── system.zig
        │   ├── target.zig
        │   ├── tokenizer.zig
        │   ├── TokenSmith.zig
        │   ├── WindowsSdk.zig
        │   ├── Zir.zig
        │   ├── Zoir.zig
        │   └── ZonGen.zig
        ├── zon
        │   ├── parse.zig
        │   ├── Serializer.zig
        │   └── stringify.zig
        ├── array_hash_map.zig
        ├── array_list.zig
        ├── ascii.zig
        ├── atomic.zig
        ├── base64.zig
        ├── bit_set.zig
        ├── BitStack.zig
        ├── buf_map.zig
        ├── buf_set.zig
        ├── Build.zig
        ├── builtin.zig
        ├── c.zig
        ├── coff.zig
        ├── compress.zig
        ├── crypto.zig
        ├── debug.zig
        ├── deque.zig
        ├── DoublyLinkedList.zig
        ├── dwarf.zig
        ├── dynamic_library.zig
        ├── elf.zig
        ├── enums.zig
        ├── fmt.zig
        ├── fs.zig
        ├── gpu.zig
        ├── hash_map.zig
        ├── hash.zig
        ├── heap.zig
        ├── http.zig
        ├── Io.zig
        ├── json.zig
        ├── leb128.zig
        ├── log.zig
        ├── macho.zig
        ├── math.zig
        ├── mem.zig
        ├── meta.zig
        ├── multi_array_list.zig
        ├── os.zig
        ├── pdb.zig
        ├── pie.zig
        ├── posix.zig
        ├── priority_dequeue.zig
        ├── priority_queue.zig
        ├── process.zig
        ├── Progress.zig
        ├── Random.zig
        ├── SemanticVersion.zig
        ├── simd.zig
        ├── SinglyLinkedList.zig
        ├── sort.zig
        ├── start.zig
        ├── static_string_map.zig
        ├── std.zig
        ├── tar.zig
        ├── Target.zig
        ├── testing.zig
        ├── Thread.zig
        ├── time.zig
        ├── treap.zig
        ├── tz.zig
        ├── unicode.zig
        ├── Uri.zig
        ├── valgrind.zig
        ├── wasm.zig
        ├── zig.zig
        ├── zip.zig
        └── zon.zig



---
File: /std/Build/Cache/DepTokenizer.zig
---

const Tokenizer = @This();

index: usize = 0,
bytes: []const u8,
state: State = .lhs,

const std = @import("std");
const testing = std.testing;
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;

pub fn next(self: *Tokenizer) ?Token {
    var start = self.index;
    var must_resolve = false;
    while (self.index < self.bytes.len) {
        const char = self.bytes[self.index];
        switch (self.state) {
            .lhs => switch (char) {
                '\t', '\n', '\r', ' ' => {
                    // silently ignore whitespace
                    self.index += 1;
                },
                else => {
                    start = self.index;
                    self.state = .target;
                },
            },
            .target => switch (char) {
                '\n', '\r' => {
                    return errorIllegalChar(.invalid_target, self.index, char);
                },
                '$' => {
                    self.state = .target_dollar_sign;
                    self.index += 1;
                },
                '\\' => {
                    self.state = .target_reverse_solidus;
                    self.index += 1;
                },
                ':' => {
                    self.state = .target_colon;
                    self.index += 1;
                },
                '\t', ' ' => {
                    self.state = .target_space;

                    const bytes = self.bytes[start..self.index];
                    std.debug.assert(bytes.len != 0);
                    self.index += 1;

                    return finishTarget(must_resolve, bytes);
                },
                else => {
                    self.index += 1;
                },
            },
            .target_reverse_solidus => switch (char) {
                '\t', '\n', '\r' => {
                    return errorIllegalChar(.bad_target_escape, self.index, char);
                },
                ' ', '#', '\\' => {
                    must_resolve = true;
                    self.state = .target;
                    self.index += 1;
                },
                '$' => {
                    self.state = .target_dollar_sign;
                    self.index += 1;
                },
                else => {
                    self.state = .target;
                    self.index += 1;
                },
            },
            .target_dollar_sign => switch (char) {
                '$' => {
                    must_resolve = true;
                    self.state = .target;
                    self.index += 1;
                },
                else => {
                    return errorIllegalChar(.expected_dollar_sign, self.index, char);
                },
            },
            .target_colon => switch (char) {
                '\n', '\r' => {
                    const bytes = self.bytes[start .. self.index - 1];
                    if (bytes.len != 0) {
                        self.state = .lhs;
                        return finishTarget(must_resolve, bytes);
                    }
                    // silently ignore null target
                    self.state = .lhs;
                },
                '/', '\\' => {
                    self.state = .target_colon_reverse_solidus;
                    self.index += 1;
                },
                else => {
                    const bytes = self.bytes[start .. self.index - 1];
                    if (bytes.len != 0) {
                        self.state = .rhs;
                        return finishTarget(must_resolve, bytes);
                    }
                    // silently ignore null target
                    self.state = .lhs;
                },
            },
            .target_colon_reverse_solidus => switch (char) {
                '\n', '\r' => {
                    const bytes = self.bytes[start .. self.index - 2];
                    if (bytes.len != 0) {
                        self.state = .lhs;
                        return finishTarget(must_resolve, bytes);
                    }
                    // silently ignore null target
                    self.state = .lhs;
                },
                else => {
                    self.state = .target;
                },
            },
            .target_space => switch (char) {
                '\t', ' ' => {
                    // silently ignore additional horizontal whitespace
                    self.index += 1;
                },
                ':' => {
                    self.state = .rhs;
                    self.index += 1;
                },
                else => {
                    return errorIllegalChar(.expected_colon, self.index, char);
                },
            },
            .rhs => switch (char) {
                '\t', ' ' => {
                    // silently ignore horizontal whitespace
                    self.index += 1;
                },
                '\n', '\r' => {
                    self.state = .lhs;
                },
                '\\' => {
                    self.state = .rhs_continuation;
                    self.index += 1;
                },
                '"' => {
                    self.state = .prereq_quote;
                    self.index += 1;
                    start = self.index;
                },
                else => {
                    start = self.index;
                    self.state = .prereq;
                },
            },
            .rhs_continuation => switch (char) {
                '\n' => {
                    self.state = .rhs;
                    self.index += 1;
                },
                '\r' => {
                    self.state = .rhs_continuation_linefeed;
                    self.index += 1;
                },
                else => {
                    return errorIllegalChar(.continuation_eol, self.index, char);
                },
            },
            .rhs_continuation_linefeed => switch (char) {
                '\n' => {
                    self.state = .rhs;
                    self.index += 1;
                },
                else => {
                    return errorIllegalChar(.continuation_eol, self.index, char);
                },
            },
            .prereq_quote => switch (char) {
                '"' => {
                    self.index += 1;
                    self.state = .rhs;
                    return finishPrereq(must_resolve, self.bytes[start .. self.index - 1]);
                },
                else => {
                    self.index += 1;
                },
            },
            .prereq => switch (char) {
                '\t', ' ' => {
                    self.state = .rhs;
                    return finishPrereq(must_resolve, self.bytes[start..self.index]);
                },
                '\n', '\r' => {
                    self.state = .lhs;
                    return finishPrereq(must_resolve, self.bytes[start..self.index]);
                },
                '\\' => {
                    self.state = .prereq_continuation;
                    self.index += 1;
                },
                else => {
                    self.index += 1;
                },
            },
            .prereq_continuation => switch (char) {
                '\n' => {
                    self.index += 1;
                    self.state = .rhs;
                    return finishPrereq(must_resolve, self.bytes[start .. self.index - 2]);
                },
                '\r' => {
                    self.state = .prereq_continuation_linefeed;
                    self.index += 1;
                },
                '\\' => {
                    // The previous \ wasn't a continuation, but this one might be.
                    self.index += 1;
                },
                ' ' => {
                    // not continuation, but escaped space must be resolved
                    must_resolve = true;
                    self.state = .prereq;
                    self.index += 1;
                },
                else => {
                    // not continuation
                    self.state = .prereq;
                    self.index += 1;
                },
            },
            .prereq_continuation_linefeed => switch (char) {
                '\n' => {
                    self.index += 1;
                    self.state = .rhs;
                    return finishPrereq(must_resolve, self.bytes[start .. self.index - 3]);
                },
                else => {
                    return errorIllegalChar(.continuation_eol, self.index, char);
                },
            },
        }
    } else {
        switch (self.state) {
            .lhs,
            .rhs,
            .rhs_continuation,
            .rhs_continuation_linefeed,
            => return null,
            .target => {
                return errorPosition(.incomplete_target, start, self.bytes[start..]);
            },
            .target_reverse_solidus,
            .target_dollar_sign,
            => {
                const idx = self.index - 1;
                return errorIllegalChar(.incomplete_escape, idx, self.bytes[idx]);
            },
            .target_colon => {
                const bytes = self.bytes[start .. self.index - 1];
                if (bytes.len != 0) {
                    self.index += 1;
                    self.state = .rhs;
                    return finishTarget(must_resolve, bytes);
                }
                // silently ignore null target
                self.state = .lhs;
                return null;
            },
            .target_colon_reverse_solidus => {
                const bytes = self.bytes[start .. self.index - 2];
                if (bytes.len != 0) {
                    self.index += 1;
                    self.state = .rhs;
                    return finishTarget(must_resolve, bytes);
                }
                // silently ignore null target
                self.state = .lhs;
                return null;
            },
            .target_space => {
                const idx = self.index - 1;
                return errorIllegalChar(.expected_colon, idx, self.bytes[idx]);
            },
            .prereq_quote => {
                return errorPosition(.incomplete_quoted_prerequisite, start, self.bytes[start..]);
            },
            .prereq => {
                self.state = .lhs;
                return finishPrereq(must_resolve, self.bytes[start..]);
            },
            .prereq_continuation => {
                self.state = .lhs;
                return finishPrereq(must_resolve, self.bytes[start .. self.index - 1]);
            },
            .prereq_continuation_linefeed => {
                self.state = .lhs;
                return finishPrereq(must_resolve, self.bytes[start .. self.index - 2]);
            },
        }
    }
    unreachable;
}

fn errorPosition(comptime id: std.meta.Tag(Token), index: usize, bytes: []const u8) Token {
    return @unionInit(Token, @tagName(id), .{ .index = index, .bytes = bytes });
}

fn errorIllegalChar(comptime id: std.meta.Tag(Token), index: usize, char: u8) Token {
    return @unionInit(Token, @tagName(id), .{ .index = index, .char = char });
}

fn finishTarget(must_resolve: bool, bytes: []const u8) Token {
    return if (must_resolve) .{ .target_must_resolve = bytes } else .{ .target = bytes };
}

fn finishPrereq(must_resolve: bool, bytes: []const u8) Token {
    return if (must_resolve) .{ .prereq_must_resolve = bytes } else .{ .prereq = bytes };
}

const State = enum {
    lhs,
    target,
    target_reverse_solidus,
    target_dollar_sign,
    target_colon,
    target_colon_reverse_solidus,
    target_space,
    rhs,
    rhs_continuation,
    rhs_continuation_linefeed,
    prereq_quote,
    prereq,
    prereq_continuation,
    prereq_continuation_linefeed,
};

pub const Token = union(enum) {
    target: []const u8,
    target_must_resolve: []const u8,
    prereq: []const u8,
    prereq_must_resolve: []const u8,

    incomplete_quoted_prerequisite: IndexAndBytes,
    incomplete_target: IndexAndBytes,

    invalid_target: IndexAndChar,
    bad_target_escape: IndexAndChar,
    expected_dollar_sign: IndexAndChar,
    continuation_eol: IndexAndChar,
    incomplete_escape: IndexAndChar,
    expected_colon: IndexAndChar,

    pub const IndexAndChar = struct {
        index: usize,
        char: u8,
    };

    pub const IndexAndBytes = struct {
        index: usize,
        bytes: []const u8,
    };

    /// Resolve escapes in target or prereq. Only valid with .target_must_resolve or .prereq_must_resolve.
    pub fn resolve(self: Token, gpa: Allocator, list: *std.ArrayList(u8)) error{OutOfMemory}!void {
        switch (self) {
            .target_must_resolve => |bytes| {
                var state: enum { start, escape, dollar } = .start;
                for (bytes) |c| {
                    switch (state) {
                        .start => {
                            switch (c) {
                                '\\' => state = .escape,
                                '$' => state = .dollar,
                                else => try list.append(gpa, c),
                            }
                        },
                        .escape => {
                            switch (c) {
                                ' ', '#', '\\' => {},
                                '$' => {
                                    try list.append(gpa, '\\');
                                    state = .dollar;
                                    continue;
                                },
                                else => try list.append(gpa, '\\'),
                            }
                            try list.append(gpa, c);
                            state = .start;
                        },
                        .dollar => {
                            try list.append(gpa, '$');
                            switch (c) {
                                '$' => {},
                                else => try list.append(gpa, c),
                            }
                            state = .start;
                        },
                    }
                }
            },
            .prereq_must_resolve => |bytes| {
                var state: enum { start, escape } = .start;
                for (bytes) |c| {
                    switch (state) {
                        .start => {
                            switch (c) {
                                '\\' => state = .escape,
                                else => try list.append(gpa, c),
                            }
                        },
                        .escape => {
                            switch (c) {
                                ' ' => {},
                                '\\' => {
                                    try list.append(gpa, c);
                                    continue;
                                },
                                else => try list.append(gpa, '\\'),
                            }
                            try list.append(gpa, c);
                            state = .start;
                        },
                    }
                }
            },
            else => unreachable,
        }
    }

    pub fn printError(self: Token, gpa: Allocator, list: *std.ArrayList(u8)) error{OutOfMemory}!void {
        switch (self) {
            .target, .target_must_resolve, .prereq, .prereq_must_resolve => unreachable, // not an error
            .incomplete_quoted_prerequisite,
            .incomplete_target,
            => |index_and_bytes| {
                try list.print(gpa, "{s} '", .{self.errStr()});
                if (self == .incomplete_target) {
                    const tmp = Token{ .target_must_resolve = index_and_bytes.bytes };
                    try tmp.resolve(gpa, list);
                } else {
                    try printCharValues(gpa, list, index_and_bytes.bytes);
                }
                try list.print(gpa, "' at position {d}", .{index_and_bytes.index});
            },
            .invalid_target,
            .bad_target_escape,
            .expected_dollar_sign,
            .continuation_eol,
            .incomplete_escape,
            .expected_colon,
            => |index_and_char| {
                try list.appendSlice(gpa, "illegal char ");
                try printUnderstandableChar(gpa, list, index_and_char.char);
                try list.print(gpa, " at position {d}: {s}", .{ index_and_char.index, self.errStr() });
            },
        }
    }

    fn errStr(self: Token) []const u8 {
        return switch (self) {
            .target, .target_must_resolve, .prereq, .prereq_must_resolve => unreachable, // not an error
            .incomplete_quoted_prerequisite => "incomplete quoted prerequisite",
            .incomplete_target => "incomplete target",
            .invalid_target => "invalid target",
            .bad_target_escape => "bad target escape",
            .expected_dollar_sign => "expecting '$'",
            .continuation_eol => "continuation expecting end-of-line",
            .incomplete_escape => "incomplete escape",
            .expected_colon => "expecting ':'",
        };
    }
};

test "empty file" {
    try depTokenizer("", "");
}

test "empty whitespace" {
    try depTokenizer("\n", "");
    try depTokenizer("\r", "");
    try depTokenizer("\r\n", "");
    try depTokenizer(" ", "");
}

test "empty colon" {
    try depTokenizer(":", "");
    try depTokenizer("\n:", "");
    try depTokenizer("\r:", "");
    try depTokenizer("\r\n:", "");
    try depTokenizer(" :", "");
}

test "empty target" {
    try depTokenizer("foo.o:", "target = {foo.o}");
    try depTokenizer(
        \\foo.o:
        \\bar.o:
        \\abcd.o:
    ,
        \\target = {foo.o}
        \\target = {bar.o}
        \\target = {abcd.o}
    );
}

test "whitespace empty target" {
    try depTokenizer("\nfoo.o:", "target = {foo.o}");
    try depTokenizer("\rfoo.o:", "target = {foo.o}");
    try depTokenizer("\r\nfoo.o:", "target = {foo.o}");
    try depTokenizer(" foo.o:", "target = {foo.o}");
}

test "escape empty target" {
    try depTokenizer("\\ foo.o:", "target = { foo.o}");
    try depTokenizer("\\#foo.o:", "target = {#foo.o}");
    try depTokenizer("\\\\foo.o:", "target = {\\foo.o}");
    try depTokenizer("$$foo.o:", "target = {$foo.o}");
}

test "empty target linefeeds" {
    try depTokenizer("\n", "");
    try depTokenizer("\r\n", "");

    const expect = "target = {foo.o}";
    try depTokenizer(
        \\foo.o:
    , expect);
    try depTokenizer(
        \\foo.o:
        \\
    , expect);
    try depTokenizer(
        \\foo.o:
    , expect);
    try depTokenizer(
        \\foo.o:
        \\
    , expect);
}

test "empty target linefeeds + continuations" {
    const expect = "target = {foo.o}";
    try depTokenizer(
        \\foo.o:\
    , expect);
    try depTokenizer(
        \\foo.o:\
        \\
    , expect);
    try depTokenizer(
        \\foo.o:\
    , expect);
    try depTokenizer(
        \\foo.o:\
        \\
    , expect);
}

test "empty target linefeeds + hspace + continuations" {
    const expect = "target = {foo.o}";
    try depTokenizer(
        \\foo.o: \
    , expect);
    try depTokenizer(
        \\foo.o: \
        \\
    , expect);
    try depTokenizer(
        \\foo.o: \
    , expect);
    try depTokenizer(
        \\foo.o: \
        \\
    , expect);
}

test "empty target + hspace + colon" {
    const expect = "target = {foo.o}";

    try depTokenizer("foo.o :", expect);
    try depTokenizer("foo.o\t\t\t:", expect);
    try depTokenizer("foo.o \t \t :", expect);
    try depTokenizer("\r\nfoo.o :", expect);
    try depTokenizer(" foo.o :", expect);
}

test "prereq" {
    const expect =
        \\target = {foo.o}
        \\prereq = {foo.c}
    ;
    try depTokenizer("foo.o: foo.c", expect);
    try depTokenizer(
        \\foo.o: \
        \\foo.c
    , expect);
    try depTokenizer(
        \\foo.o: \
        \\ foo.c
    , expect);
    try depTokenizer(
        \\foo.o:    \
        \\    foo.c
    , expect);
}

test "prereq continuation" {
    const expect =
        \\target = {foo.o}
        \\prereq = {foo.h}
        \\prereq = {bar.h}
    ;
    try depTokenizer(
        \\foo.o: foo.h\
        \\bar.h
    , expect);
    try depTokenizer(
        \\foo.o: foo.h\
        \\bar.h
    , expect);
}

test "prereq continuation (CRLF)" {
    const expect =
        \\target = {foo.o}
        \\prereq = {foo.h}
        \\prereq = {bar.h}
    ;
    try depTokenizer("foo.o: foo.h\\\r\nbar.h", expect);
}

test "multiple prereqs" {
    const expect =
        \\target = {foo.o}
        \\prereq = {foo.c}
        \\prereq = {foo.h}
        \\prereq = {bar.h}
    ;
    try depTokenizer("foo.o: foo.c foo.h bar.h", expect);
    try depTokenizer(
        \\foo.o: \
        \\foo.c foo.h bar.h
    , expect);
    try depTokenizer(
        \\foo.o: foo.c foo.h bar.h\
    , expect);
    try depTokenizer(
        \\foo.o: foo.c foo.h bar.h\
        \\
    , expect);
    try depTokenizer(
        \\foo.o: \
        \\foo.c       \
        \\     foo.h\
        \\bar.h
        \\
    , expect);
    try depTokenizer(
        \\foo.o: \
        \\foo.c       \
        \\     foo.h\
        \\bar.h\
        \\
    , expect);
    try depTokenizer(
        \\foo.o: \
        \\foo.c       \
        \\     foo.h\
        \\bar.h\
    , expect);
}

test "multiple targets and prereqs" {
    try depTokenizer(
        \\foo.o: foo.c
        \\bar.o: bar.c a.h b.h c.h
        \\abc.o: abc.c \
        \\  one.h two.h \
        \\  three.h four.h
    ,
        \\target = {foo.o}
        \\prereq = {foo.c}
        \\target = {bar.o}
        \\prereq = {bar.c}
        \\prereq = {a.h}
        \\prereq = {b.h}
        \\prereq = {c.h}
        \\target = {abc.o}
        \\prereq = {abc.c}
        \\prereq = {one.h}
        \\prereq = {two.h}
        \\prereq = {three.h}
        \\prereq = {four.h}
    );
    try depTokenizer(
        \\ascii.o: ascii.c
        \\base64.o: base64.c stdio.h
        \\elf.o: elf.c a.h b.h c.h
        \\macho.o: \
        \\  macho.c\
        \\  a.h b.h c.h
    ,
        \\target = {ascii.o}
        \\prereq = {ascii.c}
        \\target = {base64.o}
        \\prereq = {base64.c}
        \\prereq = {stdio.h}
        \\target = {elf.o}
        \\prereq = {elf.c}
        \\prereq = {a.h}
        \\prereq = {b.h}
        \\prereq = {c.h}
        \\target = {macho.o}
        \\prereq = {macho.c}
        \\prereq = {a.h}
        \\prereq = {b.h}
        \\prereq = {c.h}
    );
    try depTokenizer(
        \\a$$scii.o: ascii.c
        \\\\base64.o: "\base64.c" "s t#dio.h"
        \\e\\lf.o: "e\lf.c" "a.h$$" "$$b.h c.h$$"
        \\macho.o: \
        \\  "macho!.c" \
        \\  a.h b.h c.h
    ,
        \\target = {a$scii.o}
        \\prereq = {ascii.c}
        \\target = {\base64.o}
        \\prereq = {\base64.c}
        \\prereq = {s t#dio.h}
        \\target = {e\lf.o}
        \\prereq = {e\lf.c}
        \\prereq = {a.h$$}
        \\prereq = {$$b.h c.h$$}
        \\target = {macho.o}
        \\prereq = {macho!.c}
        \\prereq = {a.h}
        \\prereq = {b.h}
        \\prereq = {c.h}
    );
}

test "windows quoted prereqs" {
    try depTokenizer(
        \\c:\foo.o: "C:\Program Files (x86)\Microsoft Visual Studio\foo.c"
        \\c:\foo2.o: "C:\Program Files (x86)\Microsoft Visual Studio\foo2.c" \
        \\  "C:\Program Files (x86)\Microsoft Visual Studio\foo1.h" \
        \\  "C:\Program Files (x86)\Microsoft Visual Studio\foo2.h"
    ,
        \\target = {c:\foo.o}
        \\prereq = {C:\Program Files (x86)\Microsoft Visual Studio\foo.c}
        \\target = {c:\foo2.o}
        \\prereq = {C:\Program Files (x86)\Microsoft Visual Studio\foo2.c}
        \\prereq = {C:\Program Files (x86)\Microsoft Visual Studio\foo1.h}
        \\prereq = {C:\Program Files (x86)\Microsoft Visual Studio\foo2.h}
    );
}

test "windows mixed prereqs" {
    try depTokenizer(
        \\cimport.o: \
        \\  C:\msys64\home\anon\project\zig\master\zig-cache\o\qhvhbUo7GU5iKyQ5mpA8TcQpncCYaQu0wwvr3ybiSTj_Dtqi1Nmcb70kfODJ2Qlg\cimport.h \
        \\  "C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\stdio.h" \
        \\  "C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt.h" \
        \\  "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\vcruntime.h" \
        \\  "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\sal.h" \
        \\  "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\concurrencysal.h" \
        \\  C:\msys64\opt\zig\lib\zig\include\vadefs.h \
        \\  "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\vadefs.h" \
        \\  "C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_wstdio.h" \
        \\  "C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_stdio_config.h" \
        \\  "C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\string.h" \
        \\  "C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_memory.h" \
        \\  "C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_memcpy_s.h" \
        \\  "C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\errno.h" \
        \\  "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\vcruntime_string.h" \
        \\  "C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_wstring.h"
    ,
        \\target = {cimport.o}
        \\prereq = {C:\msys64\home\anon\project\zig\master\zig-cache\o\qhvhbUo7GU5iKyQ5mpA8TcQpncCYaQu0wwvr3ybiSTj_Dtqi1Nmcb70kfODJ2Qlg\cimport.h}
        \\prereq = {C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\stdio.h}
        \\prereq = {C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt.h}
        \\prereq = {C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\vcruntime.h}
        \\prereq = {C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\sal.h}
        \\prereq = {C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\concurrencysal.h}
        \\prereq = {C:\msys64\opt\zig\lib\zig\include\vadefs.h}
        \\prereq = {C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\vadefs.h}
        \\prereq = {C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_wstdio.h}
        \\prereq = {C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_stdio_config.h}
        \\prereq = {C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\string.h}
        \\prereq = {C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_memory.h}
        \\prereq = {C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_memcpy_s.h}
        \\prereq = {C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\errno.h}
        \\prereq = {C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.21.27702\lib\x64\\..\..\include\vcruntime_string.h}
        \\prereq = {C:\Program Files (x86)\Windows Kits\10\\Include\10.0.17763.0\ucrt\corecrt_wstring.h}
    );
}

test "windows funky targets" {
    try depTokenizer(
        \\C:\Users\anon\foo.o:
        \\C:\Users\anon\foo\ .o:
        \\C:\Users\anon\foo\#.o:
        \\C:\Users\anon\foo$$.o:
        \\C:\Users\anon\\\ foo.o:
        \\C:\Users\anon\\#foo.o:
        \\C:\Users\anon\$$foo.o:
        \\C:\Users\anon\\\ \ \ \ \ foo.o:
    ,
        \\target = {C:\Users\anon\foo.o}
        \\target = {C:\Users\anon\foo .o}
        \\target = {C:\Users\anon\foo#.o}
        \\target = {C:\Users\anon\foo$.o}
        \\target = {C:\Users\anon\ foo.o}
        \\target = {C:\Users\anon\#foo.o}
        \\target = {C:\Users\anon\$foo.o}
        \\target = {C:\Users\anon\     foo.o}
    );
}

test "windows funky prereqs" {
    // Note we don't support unquoted escaped spaces at the very beginning of a relative path
    // e.g. `\ SpaceAtTheBeginning.c`
    // This typically wouldn't be seen in the wild, since depfiles usually use absolute paths
    // and supporting it would degrade error messages for cases where it was meant to be a
    // continuation, but the line ending is missing.
    try depTokenizer(
        \\cimport.o: \
        \\  trailingbackslash\\
        \\  C:\Users\John\ Smith\AppData\Local\zig\p\1220d14057af1a9d6dde4643293527bd5ee5099517d655251a066666a4320737ea7c\cimport.c \
        \\  somedir\\ a.c\
        \\  somedir/\ a.c\
        \\  somedir\\ \ \ b.c\
        \\  somedir\\ \\ \c.c\
        \\
    ,
        \\target = {cimport.o}
        \\prereq = {trailingbackslash\}
        \\prereq = {C:\Users\John Smith\AppData\Local\zig\p\1220d14057af1a9d6dde4643293527bd5ee5099517d655251a066666a4320737ea7c\cimport.c}
        \\prereq = {somedir\ a.c}
        \\prereq = {somedir/ a.c}
        \\prereq = {somedir\   b.c}
        \\prereq = {somedir\ \ \c.c}
    );
}

test "windows drive and forward slashes" {
    try depTokenizer(
        \\C:/msys64/what/zig-cache\tmp\48ac4d78dd531abd-cxa_thread_atexit.obj: \
        \\  C:/msys64/opt/zig3/lib/zig/libc/mingw/crt/cxa_thread_atexit.c
    ,
        \\target = {C:/msys64/what/zig-cache\tmp\48ac4d78dd531abd-cxa_thread_atexit.obj}
        \\prereq = {C:/msys64/opt/zig3/lib/zig/libc/mingw/crt/cxa_thread_atexit.c}
    );
}

test "error incomplete escape - reverse_solidus" {
    try depTokenizer("\\",
        \\ERROR: illegal char '\' at position 0: incomplete escape
    );
    try depTokenizer("\t\\",
        \\ERROR: illegal char '\' at position 1: incomplete escape
    );
    try depTokenizer("\n\\",
        \\ERROR: illegal char '\' at position 1: incomplete escape
    );
    try depTokenizer("\r\\",
        \\ERROR: illegal char '\' at position 1: incomplete escape
    );
    try depTokenizer("\r\n\\",
        \\ERROR: illegal char '\' at position 2: incomplete escape
    );
    try depTokenizer(" \\",
        \\ERROR: illegal char '\' at position 1: incomplete escape
    );
}

test "error incomplete escape - dollar_sign" {
    try depTokenizer("$",
        \\ERROR: illegal char '$' at position 0: incomplete escape
    );
    try depTokenizer("\t$",
        \\ERROR: illegal char '$' at position 1: incomplete escape
    );
    try depTokenizer("\n$",
        \\ERROR: illegal char '$' at position 1: incomplete escape
    );
    try depTokenizer("\r$",
        \\ERROR: illegal char '$' at position 1: incomplete escape
    );
    try depTokenizer("\r\n$",
        \\ERROR: illegal char '$' at position 2: incomplete escape
    );
    try depTokenizer(" $",
        \\ERROR: illegal char '$' at position 1: incomplete escape
    );
}

test "error incomplete target" {
    try depTokenizer("foo.o",
        \\ERROR: incomplete target 'foo.o' at position 0
    );
    try depTokenizer("\tfoo.o",
        \\ERROR: incomplete target 'foo.o' at position 1
    );
    try depTokenizer("\nfoo.o",
        \\ERROR: incomplete target 'foo.o' at position 1
    );
    try depTokenizer("\rfoo.o",
        \\ERROR: incomplete target 'foo.o' at position 1
    );
    try depTokenizer("\r\nfoo.o",
        \\ERROR: incomplete target 'foo.o' at position 2
    );
    try depTokenizer(" foo.o",
        \\ERROR: incomplete target 'foo.o' at position 1
    );

    try depTokenizer("\\ foo.o",
        \\ERROR: incomplete target ' foo.o' at position 0
    );
    try depTokenizer("\\#foo.o",
        \\ERROR: incomplete target '#foo.o' at position 0
    );
    try depTokenizer("\\\\foo.o",
        \\ERROR: incomplete target '\foo.o' at position 0
    );
    try depTokenizer("$$foo.o",
        \\ERROR: incomplete target '$foo.o' at position 0
    );
}

test "error illegal char at position - bad target escape" {
    try depTokenizer("\\\t",
        \\ERROR: illegal char \x09 at position 1: bad target escape
    );
    try depTokenizer("\\\n",
        \\ERROR: illegal char \x0A at position 1: bad target escape
    );
    try depTokenizer("\\\r",
        \\ERROR: illegal char \x0D at position 1: bad target escape
    );
    try depTokenizer("\\\r\n",
        \\ERROR: illegal char \x0D at position 1: bad target escape
    );
}

test "error illegal char at position - expecting dollar_sign" {
    try depTokenizer("$\t",
        \\ERROR: illegal char \x09 at position 1: expecting '$'
    );
    try depTokenizer("$\n",
        \\ERROR: illegal char \x0A at position 1: expecting '$'
    );
    try depTokenizer("$\r",
        \\ERROR: illegal char \x0D at position 1: expecting '$'
    );
    try depTokenizer("$\r\n",
        \\ERROR: illegal char \x0D at position 1: expecting '$'
    );
}

test "error illegal char at position - invalid target" {
    try depTokenizer("foo\n.o",
        \\ERROR: illegal char \x0A at position 3: invalid target
    );
    try depTokenizer("foo\r.o",
        \\ERROR: illegal char \x0D at position 3: invalid target
    );
    try depTokenizer("foo\r\n.o",
        \\ERROR: illegal char \x0D at position 3: invalid target
    );
}

test "error target - continuation expecting end-of-line" {
    try depTokenizer("foo.o: \\\t",
        \\target = {foo.o}
        \\ERROR: illegal char \x09 at position 8: continuation expecting end-of-line
    );
    try depTokenizer("foo.o: \\ ",
        \\target = {foo.o}
        \\ERROR: illegal char ' ' at position 8: continuation expecting end-of-line
    );
    try depTokenizer("foo.o: \\x",
        \\target = {foo.o}
        \\ERROR: illegal char 'x' at position 8: continuation expecting end-of-line
    );
    try depTokenizer("foo.o: \\\x0dx",
        \\target = {foo.o}
        \\ERROR: illegal char 'x' at position 9: continuation expecting end-of-line
    );
}

test "error prereq - continuation expecting end-of-line" {
    try depTokenizer("foo.o: foo.h\\\x0dx",
        \\target = {foo.o}
        \\ERROR: illegal char 'x' at position 14: continuation expecting end-of-line
    );
}

test "error illegal char at position - expecting colon" {
    try depTokenizer("foo\t.o:",
        \\target = {foo}
        \\ERROR: illegal char '.' at position 4: expecting ':'
    );
    try depTokenizer("foo .o:",
        \\target = {foo}
        \\ERROR: illegal char '.' at position 4: expecting ':'
    );
    try depTokenizer("foo \n.o:",
        \\target = {foo}
        \\ERROR: illegal char \x0A at position 4: expecting ':'
    );
    try depTokenizer("foo.o\t\n:",
        \\target = {foo.o}
        \\ERROR: illegal char \x0A at position 6: expecting ':'
    );
}

// - tokenize input, emit textual representation, and compare to expect
fn depTokenizer(input: []const u8, expect: []const u8) !void {
    var arena_allocator = std.heap.ArenaAllocator.init(std.testing.allocator);
    const arena = arena_allocator.allocator();
    defer arena_allocator.deinit();

    var it: Tokenizer = .{ .bytes = input };
    var buffer: std.ArrayList(u8) = .empty;
    var resolve_buf: std.ArrayList(u8) = .empty;
    var i: usize = 0;
    while (it.next()) |token| {
        if (i != 0) try buffer.appendSlice(arena, "\n");
        switch (token) {
            .target, .prereq => |bytes| {
                try buffer.appendSlice(arena, @tagName(token));
                try buffer.appendSlice(arena, " = {");
                for (bytes) |b| {
                    try buffer.append(arena, printable_char_tab[b]);
                }
                try buffer.appendSlice(arena, "}");
            },
            .target_must_resolve => {
                try buffer.appendSlice(arena, "target = {");
                try token.resolve(arena, &resolve_buf);
                for (resolve_buf.items) |b| {
                    try buffer.append(arena, printable_char_tab[b]);
                }
                resolve_buf.items.len = 0;
                try buffer.appendSlice(arena, "}");
            },
            .prereq_must_resolve => {
                try buffer.appendSlice(arena, "prereq = {");
                try token.resolve(arena, &resolve_buf);
                for (resolve_buf.items) |b| {
                    try buffer.append(arena, printable_char_tab[b]);
                }
                resolve_buf.items.len = 0;
                try buffer.appendSlice(arena, "}");
            },
            else => {
                try buffer.appendSlice(arena, "ERROR: ");
                try token.printError(arena, &buffer);
                break;
            },
        }
        i += 1;
    }

    if (std.mem.eql(u8, expect, buffer.items)) {
        try testing.expect(true);
        return;
    }

    try testing.expectEqualStrings(expect, buffer.items);
}

fn printCharValues(gpa: Allocator, list: *std.ArrayList(u8), bytes: []const u8) !void {
    for (bytes) |b| try list.append(gpa, printable_char_tab[b]);
}

fn printUnderstandableChar(gpa: Allocator, list: *std.ArrayList(u8), char: u8) !void {
    if (std.ascii.isPrint(char)) {
        try list.print(gpa, "'{c}'", .{char});
    } else {
        try list.print(gpa, "\\x{X:0>2}", .{char});
    }
}

// zig fmt: off
const printable_char_tab: [256]u8 = (
    "................................ !\"#$%&'()*+,-./0123456789:;<=>?" ++
    "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~." ++
    "................................................................" ++
    "................................................................"
).*;



---
File: /std/Build/Cache/Directory.zig
---

const Directory = @This();

const std = @import("../../std.zig");
const Io = std.Io;
const fs = std.fs;
const assert = std.debug.assert;
const fmt = std.fmt;
const Allocator = std.mem.Allocator;

/// This field is redundant for operations that can act on the open directory handle
/// directly, but it is needed when passing the directory to a child process.
/// `null` means cwd.
path: ?[]const u8,
handle: Io.Dir,

pub fn clone(d: Directory, arena: Allocator) Allocator.Error!Directory {
    return .{
        .path = if (d.path) |p| try arena.dupe(u8, p) else null,
        .handle = d.handle,
    };
}

pub fn cwd() Directory {
    return .{
        .path = null,
        .handle = .cwd(),
    };
}

pub fn join(self: Directory, allocator: Allocator, paths: []const []const u8) ![]u8 {
    if (self.path) |p| {
        // TODO clean way to do this with only 1 allocation
        const part2 = try fs.path.join(allocator, paths);
        defer allocator.free(part2);
        return fs.path.join(allocator, &[_][]const u8{ p, part2 });
    } else {
        return fs.path.join(allocator, paths);
    }
}

pub fn joinZ(self: Directory, allocator: Allocator, paths: []const []const u8) ![:0]u8 {
    if (self.path) |p| {
        // TODO clean way to do this with only 1 allocation
        const part2 = try fs.path.join(allocator, paths);
        defer allocator.free(part2);
        return fs.path.joinZ(allocator, &[_][]const u8{ p, part2 });
    } else {
        return fs.path.joinZ(allocator, paths);
    }
}

/// Whether or not the handle should be closed, or the path should be freed
/// is determined by usage, however this function is provided for convenience
/// if it happens to be what the caller needs.
pub fn closeAndFree(self: *Directory, gpa: Allocator, io: Io) void {
    self.handle.close(io);
    if (self.path) |p| gpa.free(p);
    self.* = undefined;
}

pub fn format(self: Directory, writer: *std.Io.Writer) std.Io.Writer.Error!void {
    if (self.path) |p| {
        try writer.writeAll(p);
        try writer.writeAll(fs.path.sep_str);
    }
}

pub fn eql(self: Directory, other: Directory) bool {
    return self.handle.handle == other.handle.handle;
}



---
File: /std/Build/Cache/Path.zig
---

const Path = @This();

const std = @import("../../std.zig");
const Io = std.Io;
const fs = std.fs;
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;
const Cache = std.Build.Cache;

root_dir: Cache.Directory,
/// The path, relative to the root dir, that this `Path` represents.
/// Empty string means the root_dir is the path.
sub_path: []const u8 = "",

pub fn clone(p: Path, arena: Allocator) Allocator.Error!Path {
    return .{
        .root_dir = try p.root_dir.clone(arena),
        .sub_path = try arena.dupe(u8, p.sub_path),
    };
}

pub fn cwd() Path {
    return initCwd("");
}

pub fn initCwd(sub_path: []const u8) Path {
    return .{ .root_dir = Cache.Directory.cwd(), .sub_path = sub_path };
}

pub fn join(p: Path, arena: Allocator, sub_path: []const u8) Allocator.Error!Path {
    if (sub_path.len == 0) return p;
    const parts: []const []const u8 =
        if (p.sub_path.len == 0) &.{sub_path} else &.{ p.sub_path, sub_path };
    return .{
        .root_dir = p.root_dir,
        .sub_path = try fs.path.join(arena, parts),
    };
}

pub fn resolvePosix(p: Path, arena: Allocator, sub_path: []const u8) Allocator.Error!Path {
    if (sub_path.len == 0) return p;
    const new_sub_path = try fs.path.resolvePosix(arena, &.{ p.sub_path, sub_path });
    return .{
        .root_dir = p.root_dir,
        // Use "" instead of "." to represent `root_dir` itself.
        .sub_path = if (std.mem.eql(u8, new_sub_path, ".")) "" else new_sub_path,
    };
}

pub fn joinString(p: Path, gpa: Allocator, sub_path: []const u8) Allocator.Error![]u8 {
    const parts: []const []const u8 =
        if (p.sub_path.len == 0) &.{sub_path} else &.{ p.sub_path, sub_path };
    return p.root_dir.join(gpa, parts);
}

pub fn joinStringZ(p: Path, gpa: Allocator, sub_path: []const u8) Allocator.Error![:0]u8 {
    const parts: []const []const u8 =
        if (p.sub_path.len == 0) &.{sub_path} else &.{ p.sub_path, sub_path };
    return p.root_dir.joinZ(gpa, parts);
}

pub fn openFile(p: Path, io: Io, sub_path: []const u8, flags: Io.File.OpenFlags) !Io.File {
    var buf: [fs.max_path_bytes]u8 = undefined;
    const joined_path = if (p.sub_path.len == 0) sub_path else p: {
        break :p std.fmt.bufPrint(&buf, "{s}" ++ fs.path.sep_str ++ "{s}", .{
            p.sub_path, sub_path,
        }) catch return error.NameTooLong;
    };
    return p.root_dir.handle.openFile(io, joined_path, flags);
}

pub fn openDir(
    p: Path,
    io: Io,
    sub_path: []const u8,
    args: Io.Dir.OpenOptions,
) Io.Dir.OpenError!Io.Dir {
    var buf: [fs.max_path_bytes]u8 = undefined;
    const joined_path = if (p.sub_path.len == 0) sub_path else p: {
        break :p std.fmt.bufPrint(&buf, "{s}" ++ fs.path.sep_str ++ "{s}", .{
            p.sub_path, sub_path,
        }) catch return error.NameTooLong;
    };
    return p.root_dir.handle.openDir(io, joined_path, args);
}

pub fn createDirPathOpen(p: Path, io: Io, sub_path: []const u8, opts: Io.Dir.OpenOptions) !Io.Dir {
    var buf: [fs.max_path_bytes]u8 = undefined;
    const joined_path = if (p.sub_path.len == 0) sub_path else p: {
        break :p std.fmt.bufPrint(&buf, "{s}" ++ fs.path.sep_str ++ "{s}", .{
            p.sub_path, sub_path,
        }) catch return error.NameTooLong;
    };
    return p.root_dir.handle.createDirPathOpen(io, joined_path, opts);
}

pub fn statFile(p: Path, io: Io, sub_path: []const u8) !Io.Dir.Stat {
    var buf: [fs.max_path_bytes]u8 = undefined;
    const joined_path = if (p.sub_path.len == 0) sub_path else p: {
        break :p std.fmt.bufPrint(&buf, "{s}" ++ fs.path.sep_str ++ "{s}", .{
            p.sub_path, sub_path,
        }) catch return error.NameTooLong;
    };
    return p.root_dir.handle.statFile(io, joined_path, .{});
}

pub fn atomicFile(
    p: Path,
    io: Io,
    sub_path: []const u8,
    options: Io.Dir.AtomicFileOptions,
    buf: *[fs.max_path_bytes]u8,
) !fs.AtomicFile {
    const joined_path = if (p.sub_path.len == 0) sub_path else p: {
        break :p std.fmt.bufPrint(buf, "{s}" ++ fs.path.sep_str ++ "{s}", .{
            p.sub_path, sub_path,
        }) catch return error.NameTooLong;
    };
    return p.root_dir.handle.atomicFile(io, joined_path, options);
}

pub fn access(p: Path, io: Io, sub_path: []const u8, flags: Io.Dir.AccessOptions) !void {
    var buf: [fs.max_path_bytes]u8 = undefined;
    const joined_path = if (p.sub_path.len == 0) sub_path else p: {
        break :p std.fmt.bufPrint(&buf, "{s}" ++ fs.path.sep_str ++ "{s}", .{
            p.sub_path, sub_path,
        }) catch return error.NameTooLong;
    };
    return p.root_dir.handle.access(io, joined_path, flags);
}

pub fn createDirPath(p: Path, io: Io, sub_path: []const u8) !void {
    var buf: [fs.max_path_bytes]u8 = undefined;
    const joined_path = if (p.sub_path.len == 0) sub_path else p: {
        break :p std.fmt.bufPrint(&buf, "{s}" ++ fs.path.sep_str ++ "{s}", .{
            p.sub_path, sub_path,
        }) catch return error.NameTooLong;
    };
    return p.root_dir.handle.createDirPath(io, joined_path);
}

pub fn toString(p: Path, allocator: Allocator) Allocator.Error![]u8 {
    return std.fmt.allocPrint(allocator, "{f}", .{p});
}

pub fn toStringZ(p: Path, allocator: Allocator) Allocator.Error![:0]u8 {
    return std.fmt.allocPrintSentinel(allocator, "{f}", .{p}, 0);
}

pub fn fmtEscapeString(path: Path) std.fmt.Alt(Path, formatEscapeString) {
    return .{ .data = path };
}

pub fn formatEscapeString(path: Path, writer: *Io.Writer) Io.Writer.Error!void {
    if (path.root_dir.path) |p| {
        try std.zig.stringEscape(p, writer);
        if (path.sub_path.len > 0) try std.zig.stringEscape(fs.path.sep_str, writer);
    }
    if (path.sub_path.len > 0) {
        try std.zig.stringEscape(path.sub_path, writer);
    }
}

/// Deprecated, use double quoted escape to print paths.
pub fn fmtEscapeChar(path: Path) std.fmt.Alt(Path, formatEscapeChar) {
    return .{ .data = path };
}

/// Deprecated, use double quoted escape to print paths.
pub fn formatEscapeChar(path: Path, writer: *Io.Writer) Io.Writer.Error!void {
    if (path.root_dir.path) |p| {
        for (p) |byte| try std.zig.charEscape(byte, writer);
        if (path.sub_path.len > 0) try writer.writeByte(fs.path.sep);
    }
    if (path.sub_path.len > 0) {
        for (path.sub_path) |byte| try std.zig.charEscape(byte, writer);
    }
}

pub fn format(self: Path, writer: *Io.Writer) Io.Writer.Error!void {
    if (fs.path.isAbsolute(self.sub_path)) {
        try writer.writeAll(self.sub_path);
        return;
    }
    if (self.root_dir.path) |p| {
        try writer.writeAll(p);
        if (self.sub_path.len > 0) {
            try writer.writeAll(fs.path.sep_str);
            try writer.writeAll(self.sub_path);
        }
        return;
    }
    if (self.sub_path.len > 0) {
        try writer.writeAll(self.sub_path);
        return;
    }
    try writer.writeByte('.');
}

pub fn eql(self: Path, other: Path) bool {
    return self.root_dir.eql(other.root_dir) and std.mem.eql(u8, self.sub_path, other.sub_path);
}

pub fn subPathOpt(self: Path) ?[]const u8 {
    return if (self.sub_path.len == 0) null else self.sub_path;
}

pub fn subPathOrDot(self: Path) []const u8 {
    return if (self.sub_path.len == 0) "." else self.sub_path;
}

pub fn stem(p: Path) []const u8 {
    return fs.path.stem(p.sub_path);
}

pub fn basename(p: Path) []const u8 {
    return fs.path.basename(p.sub_path);
}

/// Useful to make `Path` a key in `std.ArrayHashMap`.
pub const TableAdapter = struct {
    pub const Hash = std.hash.Wyhash;

    pub fn hash(self: TableAdapter, a: Cache.Path) u32 {
        _ = self;
        const seed = switch (@typeInfo(@TypeOf(a.root_dir.handle.handle))) {
            .pointer => @intFromPtr(a.root_dir.handle.handle),
            .int => @as(u32, @bitCast(a.root_dir.handle.handle)),
            else => @compileError("unimplemented hash function"),
        };
        return @truncate(Hash.hash(seed, a.sub_path));
    }
    pub fn eql(self: TableAdapter, a: Cache.Path, b: Cache.Path, b_index: usize) bool {
        _ = self;
        _ = b_index;
        return a.eql(b);
    }
};



---
File: /std/Build/Step/CheckFile.zig
---

//! Fail the build step if a file does not match certain checks.
//! TODO: make this more flexible, supporting more kinds of checks.
//! TODO: generalize the code in std.testing.expectEqualStrings and make this
//! CheckFile step produce those helpful diagnostics when there is not a match.
const CheckFile = @This();

const std = @import("std");
const Io = std.Io;
const Step = std.Build.Step;
const fs = std.fs;
const mem = std.mem;

step: Step,
expected_matches: []const []const u8,
expected_exact: ?[]const u8,
source: std.Build.LazyPath,
max_bytes: usize = 20 * 1024 * 1024,

pub const base_id: Step.Id = .check_file;

pub const Options = struct {
    expected_matches: []const []const u8 = &.{},
    expected_exact: ?[]const u8 = null,
};

pub fn create(
    owner: *std.Build,
    source: std.Build.LazyPath,
    options: Options,
) *CheckFile {
    const check_file = owner.allocator.create(CheckFile) catch @panic("OOM");
    check_file.* = .{
        .step = Step.init(.{
            .id = base_id,
            .name = "CheckFile",
            .owner = owner,
            .makeFn = make,
        }),
        .source = source.dupe(owner),
        .expected_matches = owner.dupeStrings(options.expected_matches),
        .expected_exact = options.expected_exact,
    };
    check_file.source.addStepDependencies(&check_file.step);
    return check_file;
}

pub fn setName(check_file: *CheckFile, name: []const u8) void {
    check_file.step.name = name;
}

fn make(step: *Step, options: Step.MakeOptions) !void {
    _ = options;
    const b = step.owner;
    const io = b.graph.io;
    const check_file: *CheckFile = @fieldParentPtr("step", step);
    try step.singleUnchangingWatchInput(check_file.source);

    const src_path = check_file.source.getPath2(b, step);
    const contents = Io.Dir.cwd().readFileAlloc(io, src_path, b.allocator, .limited(check_file.max_bytes)) catch |err| {
        return step.fail("unable to read '{s}': {s}", .{
            src_path, @errorName(err),
        });
    };

    for (check_file.expected_matches) |expected_match| {
        if (mem.find(u8, contents, expected_match) == null) {
            return step.fail(
                \\
                \\========= expected to find: ===================
                \\{s}
                \\========= but file does not contain it: =======
                \\{s}
                \\===============================================
            , .{ expected_match, contents });
        }
    }

    if (check_file.expected_exact) |expected_exact| {
        if (!mem.eql(u8, expected_exact, contents)) {
            return step.fail(
                \\
                \\========= expected: =====================
                \\{s}
                \\========= but found: ====================
                \\{s}
                \\========= from the following file: ======
                \\{s}
            , .{ expected_exact, contents, src_path });
        }
    }
}



---
File: /std/Build/Step/CheckObject.zig
---

const std = @import("std");
const assert = std.debug.assert;
const elf = std.elf;
const fs = std.fs;
const macho = std.macho;
const math = std.math;
const mem = std.mem;
const testing = std.testing;
const Writer = std.Io.Writer;

const CheckObject = @This();

const Allocator = mem.Allocator;
const Step = std.Build.Step;

pub const base_id: Step.Id = .check_object;

step: Step,
source: std.Build.LazyPath,
max_bytes: usize = 20 * 1024 * 1024,
checks: std.array_list.Managed(Check),
obj_format: std.Target.ObjectFormat,

pub fn create(
    owner: *std.Build,
    source: std.Build.LazyPath,
    obj_format: std.Target.ObjectFormat,
) *CheckObject {
    const gpa = owner.allocator;
    const check_object = gpa.create(CheckObject) catch @panic("OOM");
    check_object.* = .{
        .step = .init(.{
            .id = base_id,
            .name = "CheckObject",
            .owner = owner,
            .makeFn = make,
        }),
        .source = source.dupe(owner),
        .checks = std.array_list.Managed(Check).init(gpa),
        .obj_format = obj_format,
    };
    check_object.source.addStepDependencies(&check_object.step);
    return check_object;
}

const SearchPhrase = struct {
    string: []const u8,
    lazy_path: ?std.Build.LazyPath = null,

    fn resolve(phrase: SearchPhrase, b: *std.Build, step: *Step) []const u8 {
        const lazy_path = phrase.lazy_path orelse return phrase.string;
        return b.fmt("{s} {s}", .{ phrase.string, lazy_path.getPath2(b, step) });
    }
};

/// There five types of actions currently supported:
/// .exact - will do an exact match against the haystack
/// .contains - will check for existence within the haystack
/// .not_present - will check for non-existence within the haystack
/// .extract - will do an exact match and extract into a variable enclosed within `{name}` braces
/// .compute_cmp - will perform an operation on the extracted global variables
/// using the MatchAction. It currently only supports an addition. The operation is required
/// to be specified in Reverse Polish Notation to ease in operator-precedence parsing (well,
/// to avoid any parsing really).
/// For example, if the two extracted values were saved as `vmaddr` and `entryoff` respectively
/// they could then be added with this simple program `vmaddr entryoff +`.
const Action = struct {
    tag: enum { exact, contains, not_present, extract, compute_cmp },
    phrase: SearchPhrase,
    expected: ?ComputeCompareExpected = null,

    /// Returns true if the `phrase` is an exact match with the haystack and variable was successfully extracted.
    fn extract(
        act: Action,
        b: *std.Build,
        step: *Step,
        haystack: []const u8,
        global_vars: anytype,
    ) !bool {
        assert(act.tag == .extract);
        const hay = mem.trim(u8, haystack, " ");
        const phrase = mem.trim(u8, act.phrase.resolve(b, step), " ");

        var candidate_vars: std.array_list.Managed(struct { name: []const u8, value: u64 }) = .init(b.allocator);
        var hay_it = mem.tokenizeScalar(u8, hay, ' ');
        var needle_it = mem.tokenizeScalar(u8, phrase, ' ');

        while (needle_it.next()) |needle_tok| {
            const hay_tok = hay_it.next() orelse break;
            if (mem.startsWith(u8, needle_tok, "{")) {
                const closing_brace = mem.find(u8, needle_tok, "}") orelse return error.MissingClosingBrace;
                if (closing_brace != needle_tok.len - 1) return error.ClosingBraceNotLast;

                const name = needle_tok[1..closing_brace];
                if (name.len == 0) return error.MissingBraceValue;
                const value = std.fmt.parseInt(u64, hay_tok, 16) catch return false;
                try candidate_vars.append(.{
                    .name = name,
                    .value = value,
                });
            } else {
                if (!mem.eql(u8, hay_tok, needle_tok)) return false;
            }
        }

        if (candidate_vars.items.len == 0) return false;

        for (candidate_vars.items) |cv| try global_vars.putNoClobber(cv.name, cv.value);

        return true;
    }

    /// Returns true if the `phrase` is an exact match with the haystack.
    fn exact(
        act: Action,
        b: *std.Build,
        step: *Step,
        haystack: []const u8,
    ) bool {
        assert(act.tag == .exact);
        const hay = mem.trim(u8, haystack, " ");
        const phrase = mem.trim(u8, act.phrase.resolve(b, step), " ");
        return mem.eql(u8, hay, phrase);
    }

    /// Returns true if the `phrase` exists within the haystack.
    fn contains(
        act: Action,
        b: *std.Build,
        step: *Step,
        haystack: []const u8,
    ) bool {
        assert(act.tag == .contains);
        const hay = mem.trim(u8, haystack, " ");
        const phrase = mem.trim(u8, act.phrase.resolve(b, step), " ");
        return mem.find(u8, hay, phrase) != null;
    }

    /// Returns true if the `phrase` does not exist within the haystack.
    fn notPresent(
        act: Action,
        b: *std.Build,
        step: *Step,
        haystack: []const u8,
    ) bool {
        assert(act.tag == .not_present);
        return !contains(.{
            .tag = .contains,
            .phrase = act.phrase,
            .expected = act.expected,
        }, b, step, haystack);
    }

    /// Will return true if the `phrase` is correctly parsed into an RPN program and
    /// its reduced, computed value compares using `op` with the expected value, either
    /// a literal or another extracted variable.
    fn computeCmp(act: Action, b: *std.Build, step: *Step, global_vars: anytype) !bool {
        const gpa = step.owner.allocator;
        const phrase = act.phrase.resolve(b, step);
        var op_stack = std.array_list.Managed(enum { add, sub, mod, mul }).init(gpa);
        var values = std.array_list.Managed(u64).init(gpa);

        var it = mem.tokenizeScalar(u8, phrase, ' ');
        while (it.next()) |next| {
            if (mem.eql(u8, next, "+")) {
                try op_stack.append(.add);
            } else if (mem.eql(u8, next, "-")) {
                try op_stack.append(.sub);
            } else if (mem.eql(u8, next, "%")) {
                try op_stack.append(.mod);
            } else if (mem.eql(u8, next, "*")) {
                try op_stack.append(.mul);
            } else {
                const val = std.fmt.parseInt(u64, next, 0) catch blk: {
                    break :blk global_vars.get(next) orelse {
                        try step.addError(
                            \\
                            \\========= variable was not extracted: ===========
                            \\{s}
                            \\=================================================
                        , .{next});
                        return error.UnknownVariable;
                    };
                };
                try values.append(val);
            }
        }

        var op_i: usize = 1;
        var reduced: u64 = values.items[0];
        for (op_stack.items) |op| {
            const other = values.items[op_i];
            switch (op) {
                .add => {
                    reduced += other;
                },
                .sub => {
                    reduced -= other;
                },
                .mod => {
                    reduced %= other;
                },
                .mul => {
                    reduced *= other;
                },
            }
            op_i += 1;
        }

        const exp_value = switch (act.expected.?.value) {
            .variable => |name| global_vars.get(name) orelse {
                try step.addError(
                    \\
                    \\========= variable was not extracted: ===========
                    \\{s}
                    \\=================================================
                , .{name});
                return error.UnknownVariable;
            },
            .literal => |x| x,
        };
        return math.compare(reduced, act.expected.?.op, exp_value);
    }
};

const ComputeCompareExpected = struct {
    op: math.CompareOperator,
    value: union(enum) {
        variable: []const u8,
        literal: u64,
    },

    pub fn format(value: ComputeCompareExpected, w: *Writer) Writer.Error!void {
        try w.print("{t} ", .{value.op});
        switch (value.value) {
            .variable => |name| try w.writeAll(name),
            .literal => |x| try w.print("{x}", .{x}),
        }
    }
};

const Check = struct {
    kind: Kind,
    payload: Payload,
    data: std.array_list.Managed(u8),
    actions: std.array_list.Managed(Action),

    fn create(allocator: Allocator, kind: Kind) Check {
        return .{
            .kind = kind,
            .payload = .{ .none = {} },
            .data = std.array_list.Managed(u8).init(allocator),
            .actions = std.array_list.Managed(Action).init(allocator),
        };
    }

    fn dumpSection(allocator: Allocator, name: [:0]const u8) Check {
        var check = Check.create(allocator, .dump_section);
        const off: u32 = @intCast(check.data.items.len);
        check.data.print("{s}\x00", .{name}) catch @panic("OOM");
        check.payload = .{ .dump_section = off };
        return check;
    }

    fn extract(check: *Check, phrase: SearchPhrase) void {
        check.actions.append(.{
            .tag = .extract,
            .phrase = phrase,
        }) catch @panic("OOM");
    }

    fn exact(check: *Check, phrase: SearchPhrase) void {
        check.actions.append(.{
            .tag = .exact,
            .phrase = phrase,
        }) catch @panic("OOM");
    }

    fn contains(check: *Check, phrase: SearchPhrase) void {
        check.actions.append(.{
            .tag = .contains,
            .phrase = phrase,
        }) catch @panic("OOM");
    }

    fn notPresent(check: *Check, phrase: SearchPhrase) void {
        check.actions.append(.{
            .tag = .not_present,
            .phrase = phrase,
        }) catch @panic("OOM");
    }

    fn computeCmp(check: *Check, phrase: SearchPhrase, expected: ComputeCompareExpected) void {
        check.actions.append(.{
            .tag = .compute_cmp,
            .phrase = phrase,
            .expected = expected,
        }) catch @panic("OOM");
    }

    const Kind = enum {
        headers,
        symtab,
        indirect_symtab,
        dynamic_symtab,
        archive_symtab,
        dynamic_section,
        dyld_rebase,
        dyld_bind,
        dyld_weak_bind,
        dyld_lazy_bind,
        exports,
        compute_compare,
        dump_section,
    };

    const Payload = union {
        none: void,
        /// Null-delimited string in the 'data' buffer.
        dump_section: u32,
    };
};

/// Creates a new empty sequence of actions.
fn checkStart(check_object: *CheckObject, kind: Check.Kind) void {
    const check = Check.create(check_object.step.owner.allocator, kind);
    check_object.checks.append(check) catch @panic("OOM");
}

/// Adds an exact match phrase to the latest created Check.
pub fn checkExact(check_object: *CheckObject, phrase: []const u8) void {
    check_object.checkExactInner(phrase, null);
}

/// Like `checkExact()` but takes an additional argument `LazyPath` which will be
/// resolved to a full search query in `make()`.
pub fn checkExactPath(check_object: *CheckObject, phrase: []const u8, lazy_path: std.Build.LazyPath) void {
    check_object.checkExactInner(phrase, lazy_path);
}

fn checkExactInner(check_object: *CheckObject, phrase: []const u8, lazy_path: ?std.Build.LazyPath) void {
    assert(check_object.checks.items.len > 0);
    const last = &check_object.checks.items[check_object.checks.items.len - 1];
    last.exact(.{ .string = check_object.step.owner.dupe(phrase), .lazy_path = lazy_path });
}

/// Adds a fuzzy match phrase to the latest created Check.
pub fn checkContains(check_object: *CheckObject, phrase: []const u8) void {
    check_object.checkContainsInner(phrase, null);
}

/// Like `checkContains()` but takes an additional argument `lazy_path` which will be
/// resolved to a full search query in `make()`.
pub fn checkContainsPath(
    check_object: *CheckObject,
    phrase: []const u8,
    lazy_path: std.Build.LazyPath,
) void {
    check_object.checkContainsInner(phrase, lazy_path);
}

fn checkContainsInner(check_object: *CheckObject, phrase: []const u8, lazy_path: ?std.Build.LazyPath) void {
    assert(check_object.checks.items.len > 0);
    const last = &check_object.checks.items[check_object.checks.items.len - 1];
    last.contains(.{ .string = check_object.step.owner.dupe(phrase), .lazy_path = lazy_path });
}

/// Adds an exact match phrase with variable extractor to the latest created Check.
pub fn checkExtract(check_object: *CheckObject, phrase: []const u8) void {
    check_object.checkExtractInner(phrase, null);
}

/// Like `checkExtract()` but takes an additional argument `LazyPath` which will be
/// resolved to a full search query in `make()`.
pub fn checkExtractLazyPath(check_object: *CheckObject, phrase: []const u8, lazy_path: std.Build.LazyPath) void {
    check_object.checkExtractInner(phrase, lazy_path);
}

fn checkExtractInner(check_object: *CheckObject, phrase: []const u8, lazy_path: ?std.Build.LazyPath) void {
    assert(check_object.checks.items.len > 0);
    const last = &check_object.checks.items[check_object.checks.items.len - 1];
    last.extract(.{ .string = check_object.step.owner.dupe(phrase), .lazy_path = lazy_path });
}

/// Adds another searched phrase to the latest created Check
/// however ensures there is no matching phrase in the output.
pub fn checkNotPresent(check_object: *CheckObject, phrase: []const u8) void {
    check_object.checkNotPresentInner(phrase, null);
}

/// Like `checkExtract()` but takes an additional argument `LazyPath` which will be
/// resolved to a full search query in `make()`.
pub fn checkNotPresentLazyPath(check_object: *CheckObject, phrase: []const u8, lazy_path: std.Build.LazyPath) void {
    check_object.checkNotPresentInner(phrase, lazy_path);
}

fn checkNotPresentInner(check_object: *CheckObject, phrase: []const u8, lazy_path: ?std.Build.LazyPath) void {
    assert(check_object.checks.items.len > 0);
    const last = &check_object.checks.items[check_object.checks.items.len - 1];
    last.notPresent(.{ .string = check_object.step.owner.dupe(phrase), .lazy_path = lazy_path });
}

/// Creates a new check checking in the file headers (section, program headers, etc.).
pub fn checkInHeaders(check_object: *CheckObject) void {
    check_object.checkStart(.headers);
}

/// Creates a new check checking specifically symbol table parsed and dumped from the object
/// file.
pub fn checkInSymtab(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .macho => MachODumper.symtab_label,
        .elf => ElfDumper.symtab_label,
        .wasm => WasmDumper.symtab_label,
        .coff => @panic("TODO symtab for coff"),
        else => @panic("TODO other file formats"),
    };
    check_object.checkStart(.symtab);
    check_object.checkExact(label);
}

/// Creates a new check checking specifically dyld rebase opcodes contents parsed and dumped
/// from the object file.
/// This check is target-dependent and applicable to MachO only.
pub fn checkInDyldRebase(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .macho => MachODumper.dyld_rebase_label,
        else => @panic("Unsupported target platform"),
    };
    check_object.checkStart(.dyld_rebase);
    check_object.checkExact(label);
}

/// Creates a new check checking specifically dyld bind opcodes contents parsed and dumped
/// from the object file.
/// This check is target-dependent and applicable to MachO only.
pub fn checkInDyldBind(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .macho => MachODumper.dyld_bind_label,
        else => @panic("Unsupported target platform"),
    };
    check_object.checkStart(.dyld_bind);
    check_object.checkExact(label);
}

/// Creates a new check checking specifically dyld weak bind opcodes contents parsed and dumped
/// from the object file.
/// This check is target-dependent and applicable to MachO only.
pub fn checkInDyldWeakBind(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .macho => MachODumper.dyld_weak_bind_label,
        else => @panic("Unsupported target platform"),
    };
    check_object.checkStart(.dyld_weak_bind);
    check_object.checkExact(label);
}

/// Creates a new check checking specifically dyld lazy bind opcodes contents parsed and dumped
/// from the object file.
/// This check is target-dependent and applicable to MachO only.
pub fn checkInDyldLazyBind(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .macho => MachODumper.dyld_lazy_bind_label,
        else => @panic("Unsupported target platform"),
    };
    check_object.checkStart(.dyld_lazy_bind);
    check_object.checkExact(label);
}

/// Creates a new check checking specifically exports info contents parsed and dumped
/// from the object file.
/// This check is target-dependent and applicable to MachO only.
pub fn checkInExports(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .macho => MachODumper.exports_label,
        else => @panic("Unsupported target platform"),
    };
    check_object.checkStart(.exports);
    check_object.checkExact(label);
}

/// Creates a new check checking specifically indirect symbol table parsed and dumped
/// from the object file.
/// This check is target-dependent and applicable to MachO only.
pub fn checkInIndirectSymtab(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .macho => MachODumper.indirect_symtab_label,
        else => @panic("Unsupported target platform"),
    };
    check_object.checkStart(.indirect_symtab);
    check_object.checkExact(label);
}

/// Creates a new check checking specifically dynamic symbol table parsed and dumped from the object
/// file.
/// This check is target-dependent and applicable to ELF only.
pub fn checkInDynamicSymtab(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .elf => ElfDumper.dynamic_symtab_label,
        else => @panic("Unsupported target platform"),
    };
    check_object.checkStart(.dynamic_symtab);
    check_object.checkExact(label);
}

/// Creates a new check checking specifically dynamic section parsed and dumped from the object
/// file.
/// This check is target-dependent and applicable to ELF only.
pub fn checkInDynamicSection(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .elf => ElfDumper.dynamic_section_label,
        else => @panic("Unsupported target platform"),
    };
    check_object.checkStart(.dynamic_section);
    check_object.checkExact(label);
}

/// Creates a new check checking specifically symbol table parsed and dumped from the archive
/// file.
pub fn checkInArchiveSymtab(check_object: *CheckObject) void {
    const label = switch (check_object.obj_format) {
        .elf => ElfDumper.archive_symtab_label,
        else => @panic("TODO other file formats"),
    };
    check_object.checkStart(.archive_symtab);
    check_object.checkExact(label);
}

pub fn dumpSection(check_object: *CheckObject, name: [:0]const u8) void {
    const check = Check.dumpSection(check_object.step.owner.allocator, name);
    check_object.checks.append(check) catch @panic("OOM");
}

/// Creates a new standalone, singular check which allows running simple binary operations
/// on the extracted variables. It will then compare the reduced program with the value of
/// the expected variable.
pub fn checkComputeCompare(
    check_object: *CheckObject,
    program: []const u8,
    expected: ComputeCompareExpected,
) void {
    var check = Check.create(check_object.step.owner.allocator, .compute_compare);
    check.computeCmp(.{ .string = check_object.step.owner.dupe(program) }, expected);
    check_object.checks.append(check) catch @panic("OOM");
}

fn make(step: *Step, make_options: Step.MakeOptions) !void {
    _ = make_options;
    const b = step.owner;
    const io = b.graph.io;
    const gpa = b.allocator;
    const check_object: *CheckObject = @fieldParentPtr("step", step);
    try step.singleUnchangingWatchInput(check_object.source);

    const src_path = check_object.source.getPath3(b, step);
    const contents = src_path.root_dir.handle.readFileAllocOptions(
        io,
        src_path.sub_path,
        gpa,
        .limited(check_object.max_bytes),
        .of(u64),
        null,
    ) catch |err| return step.fail("unable to read '{f}': {t}", .{
        std.fmt.alt(src_path, .formatEscapeChar), err,
    });

    var vars: std.StringHashMap(u64) = .init(gpa);
    for (check_object.checks.items) |chk| {
        if (chk.kind == .compute_compare) {
            assert(chk.actions.items.len == 1);
            const act = chk.actions.items[0];
            assert(act.tag == .compute_cmp);
            const res = act.computeCmp(b, step, vars) catch |err| switch (err) {
                error.UnknownVariable => return step.fail("Unknown variable", .{}),
                else => |e| return e,
            };
            if (!res) {
                return step.fail(
                    \\
                    \\========= comparison failed for action: ===========
                    \\{s} {f}
                    \\===================================================
                , .{ act.phrase.resolve(b, step), act.expected.? });
            }
            continue;
        }

        const output = switch (check_object.obj_format) {
            .macho => try MachODumper.parseAndDump(step, chk, contents),
            .elf => try ElfDumper.parseAndDump(step, chk, contents),
            .coff => return step.fail("TODO coff parser", .{}),
            .wasm => try WasmDumper.parseAndDump(step, chk, contents),
            else => unreachable,
        };

        // Depending on whether we requested dumping section verbatim or not,
        // we either format message string with escaped codes, or not to aid debugging
        // the failed test.
        const fmtMessageString = struct {
            fn fmtMessageString(kind: Check.Kind, msg: []const u8) std.fmt.Alt(Ctx, formatMessageString) {
                return .{ .data = .{
                    .kind = kind,
                    .msg = msg,
                } };
            }

            const Ctx = struct {
                kind: Check.Kind,
                msg: []const u8,
            };

            fn formatMessageString(ctx: Ctx, w: *Writer) !void {
                switch (ctx.kind) {
                    .dump_section => try w.print("{f}", .{std.ascii.hexEscape(ctx.msg, .lower)}),
                    else => try w.writeAll(ctx.msg),
                }
            }
        }.fmtMessageString;

        var it = mem.tokenizeAny(u8, output, "\r\n");
        for (chk.actions.items) |act| {
            switch (act.tag) {
                .exact => {
                    while (it.next()) |line| {
                        if (act.exact(b, step, line)) break;
                    } else {
                        return step.fail(
                            \\
                            \\========= expected to find: ==========================
                            \\{f}
                            \\========= but parsed file does not contain it: =======
                            \\{f}
                            \\========= file path: =================================
                            \\{f}
                        , .{
                            fmtMessageString(chk.kind, act.phrase.resolve(b, step)),
                            fmtMessageString(chk.kind, output),
                            src_path,
                        });
                    }
                },

                .contains => {
                    while (it.next()) |line| {
                        if (act.contains(b, step, line)) break;
                    } else {
                        return step.fail(
                            \\
                            \\========= expected to find: ==========================
                            \\*{f}*
                            \\========= but parsed file does not contain it: =======
                            \\{f}
                            \\========= file path: =================================
                            \\{f}
                        , .{
                            fmtMessageString(chk.kind, act.phrase.resolve(b, step)),
                            fmtMessageString(chk.kind, output),
                            src_path,
                        });
                    }
                },

                .not_present => {
                    while (it.next()) |line| {
                        if (act.notPresent(b, step, line)) continue;
                        return step.fail(
                          
```
