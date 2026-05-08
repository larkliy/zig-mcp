```
          ADDEND = 0x001F,
            _,
        };

        /// MIPS Processors
        /// The following relocation type indicators are defined for MIPS processors.
        pub const MIPS = enum(u16) {
            /// The relocation is ignored.
            ABSOLUTE = 0x0000,
            /// The high 16 bits of the target's 32-bit VA.
            REFHALF = 0x0001,
            /// The target's 32-bit VA.
            REFWORD = 0x0002,
            /// The low 26 bits of the target's VA.
            /// This supports the MIPS J and JAL instructions.
            JMPADDR = 0x0003,
            /// The high 16 bits of the target's 32-bit VA.
            /// This is used for the first instruction in a two-instruction sequence that loads a full address.
            /// This relocation must be immediately followed by a PAIR relocation whose SymbolTableIndex contains a signed 16-bit displacement that is added to the upper 16 bits that are taken from the location that is being relocated.
            REFHI = 0x0004,
            /// The low 16 bits of the target's VA.
            REFLO = 0x0005,
            /// A 16-bit signed displacement of the target relative to the GP register.
            GPREL = 0x0006,
            /// The same as IMAGE_REL_MIPS_GPREL.
            LITERAL = 0x0007,
            /// The 16-bit section index of the section contains the target.
            /// This is used to support debugging information.
            SECTION = 0x000A,
            /// The 32-bit offset of the target from the beginning of its section.
            /// This is used to support debugging information and static thread local storage.
            SECREL = 0x000B,
            /// The low 16 bits of the 32-bit offset of the target from the beginning of its section.
            SECRELLO = 0x000C,
            /// The high 16 bits of the 32-bit offset of the target from the beginning of its section.
            /// An IMAGE_REL_MIPS_PAIR relocation must immediately follow this one.
            /// The SymbolTableIndex of the PAIR relocation contains a signed 16-bit displacement that is added to the upper 16 bits that are taken from the location that is being relocated.
            SECRELHI = 0x000D,
            /// The low 26 bits of the target's VA.
            /// This supports the MIPS16 JAL instruction.
            JMPADDR16 = 0x0010,
            /// The target's 32-bit RVA.
            REFWORDNB = 0x0022,
            /// The relocation is valid only when it immediately follows a REFHI or SECRELHI relocation.
            /// Its SymbolTableIndex contains a displacement and not an index into the symbol table.
            PAIR = 0x0025,
            _,
        };

        /// Mitsubishi M32R
        /// The following relocation type indicators are defined for the Mitsubishi M32R processors.
        pub const M32R = enum(u16) {
            /// The relocation is ignored.
            ABSOLUTE = 0x0000,
            /// The target's 32-bit VA.
            ADDR32 = 0x0001,
            /// The target's 32-bit RVA.
            ADDR32NB = 0x0002,
            /// The target's 24-bit VA.
            ADDR24 = 0x0003,
            /// The target's 16-bit offset from the GP register.
            GPREL16 = 0x0004,
            /// The target's 24-bit offset from the program counter (PC), shifted left by 2 bits and sign-extended
            PCREL24 = 0x0005,
            /// The target's 16-bit offset from the PC, shifted left by 2 bits and sign-extended
            PCREL16 = 0x0006,
            /// The target's 8-bit offset from the PC, shifted left by 2 bits and sign-extended
            PCREL8 = 0x0007,
            /// The 16 MSBs of the target VA.
            REFHALF = 0x0008,
            /// The 16 MSBs of the target VA, adjusted for LSB sign extension.
            /// This is used for the first instruction in a two-instruction sequence that loads a full 32-bit address.
            /// This relocation must be immediately followed by a PAIR relocation whose SymbolTableIndex contains a signed 16-bit displacement that is added to the upper 16 bits that are taken from the location that is being relocated.
            REFHI = 0x0009,
            /// The 16 LSBs of the target VA.
            REFLO = 0x000A,
            /// The relocation must follow the REFHI relocation.
            /// Its SymbolTableIndex contains a displacement and not an index into the symbol table.
            PAIR = 0x000B,
            /// The 16-bit section index of the section that contains the target.
            /// This is used to support debugging information.
            SECTION = 0x000C,
            /// The 32-bit offset of the target from the beginning of its section.
            /// This is used to support debugging information and static thread local storage.
            SECREL = 0x000D,
            /// The CLR token.
            TOKEN = 0x000E,
            _,
        };
    };
};



---
File: /std/compress.zig
---

//! Compression algorithms.

/// gzip and zlib are here.
pub const flate = @import("compress/flate.zig");
pub const lzma = @import("compress/lzma.zig");
pub const lzma2 = @import("compress/lzma2.zig");
pub const xz = @import("compress/xz.zig");
pub const zstd = @import("compress/zstd.zig");

test {
    _ = flate;
    _ = lzma;
    _ = lzma2;
    _ = xz;
    _ = zstd;
}



---
File: /std/crypto.zig
---

//! Cryptography.

const std = @import("std.zig");

pub const timing_safe = @import("crypto/timing_safe.zig");

/// Authenticated Encryption with Associated Data
pub const aead = struct {
    pub const aegis = struct {
        const variants = @import("crypto/aegis.zig");

        pub const Aegis128X4 = variants.Aegis128X4;
        pub const Aegis128X2 = variants.Aegis128X2;
        pub const Aegis128L = variants.Aegis128L;

        pub const Aegis256X4 = variants.Aegis256X4;
        pub const Aegis256X2 = variants.Aegis256X2;
        pub const Aegis256 = variants.Aegis256;

        pub const Aegis128X4_256 = variants.Aegis128X4_256;
        pub const Aegis128X2_256 = variants.Aegis128X2_256;
        pub const Aegis128L_256 = variants.Aegis128L_256;

        pub const Aegis256X4_256 = variants.Aegis256X4_256;
        pub const Aegis256X2_256 = variants.Aegis256X2_256;
        pub const Aegis256_256 = variants.Aegis256_256;
    };

    pub const aes_gcm = struct {
        pub const Aes128Gcm = @import("crypto/aes_gcm.zig").Aes128Gcm;
        pub const Aes256Gcm = @import("crypto/aes_gcm.zig").Aes256Gcm;
    };

    pub const aes_gcm_siv = struct {
        pub const Aes128GcmSiv = @import("crypto/aes_gcm_siv.zig").Aes128GcmSiv;
        pub const Aes256GcmSiv = @import("crypto/aes_gcm_siv.zig").Aes256GcmSiv;
    };

    pub const aes_siv = struct {
        pub const Aes128Siv = @import("crypto/aes_siv.zig").Aes128Siv;
        pub const Aes256Siv = @import("crypto/aes_siv.zig").Aes256Siv;
    };

    pub const aes_ocb = struct {
        pub const Aes128Ocb = @import("crypto/aes_ocb.zig").Aes128Ocb;
        pub const Aes256Ocb = @import("crypto/aes_ocb.zig").Aes256Ocb;
    };

    pub const aes_ccm = @import("crypto/aes_ccm.zig");

    pub const ascon = struct {
        pub const AsconAead128 = @import("crypto/ascon.zig").AsconAead128;
    };

    pub const chacha_poly = struct {
        pub const ChaCha20Poly1305 = @import("crypto/chacha20.zig").ChaCha20Poly1305;
        pub const ChaCha12Poly1305 = @import("crypto/chacha20.zig").ChaCha12Poly1305;
        pub const ChaCha8Poly1305 = @import("crypto/chacha20.zig").ChaCha8Poly1305;
        pub const XChaCha20Poly1305 = @import("crypto/chacha20.zig").XChaCha20Poly1305;
        pub const XChaCha12Poly1305 = @import("crypto/chacha20.zig").XChaCha12Poly1305;
        pub const XChaCha8Poly1305 = @import("crypto/chacha20.zig").XChaCha8Poly1305;
    };

    pub const isap = @import("crypto/isap.zig");

    pub const salsa_poly = struct {
        pub const XSalsa20Poly1305 = @import("crypto/salsa20.zig").XSalsa20Poly1305;
    };
};

/// Authentication (MAC) functions.
pub const auth = struct {
    pub const hmac = @import("crypto/hmac.zig");
    pub const siphash = @import("crypto/siphash.zig");
    pub const aegis = struct {
        const variants = @import("crypto/aegis.zig");
        pub const Aegis128X4Mac = variants.Aegis128X4Mac;
        pub const Aegis128X2Mac = variants.Aegis128X2Mac;
        pub const Aegis128LMac = variants.Aegis128LMac;

        pub const Aegis256X4Mac = variants.Aegis256X4Mac;
        pub const Aegis256X2Mac = variants.Aegis256X2Mac;
        pub const Aegis256Mac = variants.Aegis256Mac;

        pub const Aegis128X4Mac_128 = variants.Aegis128X4Mac_128;
        pub const Aegis128X2Mac_128 = variants.Aegis128X2Mac_128;
        pub const Aegis128LMac_128 = variants.Aegis128LMac_128;

        pub const Aegis256X4Mac_128 = variants.Aegis256X4Mac_128;
        pub const Aegis256X2Mac_128 = variants.Aegis256X2Mac_128;
        pub const Aegis256Mac_128 = variants.Aegis256Mac_128;
    };
    pub const cmac = @import("crypto/cmac.zig");
    pub const cbc_mac = @import("crypto/cbc_mac.zig");
};

/// Core functions, that should rarely be used directly by applications.
pub const core = struct {
    pub const aes = @import("crypto/aes.zig");
    pub const keccak = @import("crypto/keccak_p.zig");

    pub const Ascon = @import("crypto/ascon.zig").State;

    /// Modes are generic compositions to construct encryption/decryption functions from block ciphers and permutations.
    ///
    /// These modes are designed to be building blocks for higher-level constructions, and should generally not be used directly by applications, as they may not provide the expected properties and security guarantees.
    ///
    /// Most applications may want to use AEADs instead.
    pub const modes = @import("crypto/modes.zig");
};

/// Diffie-Hellman key exchange functions.
pub const dh = struct {
    pub const X25519 = @import("crypto/25519/x25519.zig").X25519;
};

/// Key Encapsulation Mechanisms.
pub const kem = struct {
    pub const hybrid = @import("crypto/hybrid_kem.zig");
    pub const kyber_d00 = @import("crypto/ml_kem.zig").d00;
    pub const ml_kem = @import("crypto/ml_kem.zig").nist;
};

/// Elliptic-curve arithmetic.
pub const ecc = struct {
    pub const Curve25519 = @import("crypto/25519/curve25519.zig").Curve25519;
    pub const Edwards25519 = @import("crypto/25519/edwards25519.zig").Edwards25519;
    pub const P256 = @import("crypto/pcurves/p256.zig").P256;
    pub const P384 = @import("crypto/pcurves/p384.zig").P384;
    pub const Ristretto255 = @import("crypto/25519/ristretto255.zig").Ristretto255;
    pub const Secp256k1 = @import("crypto/pcurves/secp256k1.zig").Secp256k1;
};

/// Hash functions.
pub const hash = struct {
    pub const ascon = struct {
        const variants = @import("crypto/ascon.zig");
        pub const AsconHash256 = variants.AsconHash256;
        pub const AsconXof128 = variants.AsconXof128;
        pub const AsconCxof128 = variants.AsconCxof128;
    };
    pub const blake2 = @import("crypto/blake2.zig");
    pub const Blake3 = @import("crypto/blake3.zig").Blake3;
    pub const Md5 = @import("crypto/md5.zig").Md5;
    pub const Sha1 = @import("crypto/Sha1.zig");
    pub const sha2 = @import("crypto/sha2.zig");
    pub const sha3 = @import("crypto/sha3.zig");
    pub const composition = @import("crypto/hash_composition.zig");
};

/// Key derivation functions.
pub const kdf = struct {
    pub const hkdf = @import("crypto/hkdf.zig");
};

/// MAC functions requiring single-use secret keys.
pub const onetimeauth = struct {
    pub const Ghash = @import("crypto/ghash_polyval.zig").Ghash;
    pub const Polyval = @import("crypto/ghash_polyval.zig").Polyval;
    pub const Poly1305 = @import("crypto/poly1305.zig").Poly1305;
};

/// A password hashing function derives a uniform key from low-entropy input material such as passwords.
/// It is intentionally slow or expensive.
///
/// With the standard definition of a key derivation function, if a key space is small, an exhaustive search may be practical.
/// Password hashing functions make exhaustive searches way slower or way more expensive, even when implemented on GPUs and ASICs, by using different, optionally combined strategies:
///
/// - Requiring a lot of computation cycles to complete
/// - Requiring a lot of memory to complete
/// - Requiring multiple CPU cores to complete
/// - Requiring cache-local data to complete in reasonable time
/// - Requiring large static tables
/// - Avoiding precomputations and time/memory tradeoffs
/// - Requiring multi-party computations
/// - Combining the input material with random per-entry data (salts), application-specific contexts and keys
///
/// Password hashing functions must be used whenever sensitive data has to be directly derived from a password.
pub const pwhash = struct {
    pub const Encoding = enum {
        phc,
        crypt,
    };

    pub const Error = HasherError || error{AllocatorRequired};
    pub const HasherError = KdfError || phc_format.Error;
    pub const KdfError = errors.Error || std.mem.Allocator.Error || std.Thread.SpawnError || std.Io.Cancelable;

    pub const argon2 = @import("crypto/argon2.zig");
    pub const bcrypt = @import("crypto/bcrypt.zig");
    pub const scrypt = @import("crypto/scrypt.zig");
    pub const pbkdf2 = @import("crypto/pbkdf2.zig").pbkdf2;

    pub const phc_format = @import("crypto/phc_encoding.zig");
};

/// Digital signature functions.
pub const sign = struct {
    pub const Ed25519 = @import("crypto/25519/ed25519.zig").Ed25519;
    pub const ecdsa = @import("crypto/ecdsa.zig");
    pub const mldsa = @import("crypto/ml_dsa.zig");
};

/// Stream ciphers. These do not provide any kind of authentication.
/// Most applications should be using AEAD constructions instead of stream ciphers directly.
pub const stream = struct {
    pub const chacha = struct {
        pub const ChaCha20IETF = @import("crypto/chacha20.zig").ChaCha20IETF;
        pub const ChaCha12IETF = @import("crypto/chacha20.zig").ChaCha12IETF;
        pub const ChaCha8IETF = @import("crypto/chacha20.zig").ChaCha8IETF;
        pub const ChaCha20With64BitNonce = @import("crypto/chacha20.zig").ChaCha20With64BitNonce;
        pub const ChaCha12With64BitNonce = @import("crypto/chacha20.zig").ChaCha12With64BitNonce;
        pub const ChaCha8With64BitNonce = @import("crypto/chacha20.zig").ChaCha8With64BitNonce;
        pub const XChaCha20IETF = @import("crypto/chacha20.zig").XChaCha20IETF;
        pub const XChaCha12IETF = @import("crypto/chacha20.zig").XChaCha12IETF;
        pub const XChaCha8IETF = @import("crypto/chacha20.zig").XChaCha8IETF;
    };

    pub const salsa = struct {
        pub const Salsa = @import("crypto/salsa20.zig").Salsa;
        pub const XSalsa = @import("crypto/salsa20.zig").XSalsa;
        pub const Salsa20 = @import("crypto/salsa20.zig").Salsa20;
        pub const XSalsa20 = @import("crypto/salsa20.zig").XSalsa20;
    };
};

pub const nacl = struct {
    const salsa20 = @import("crypto/salsa20.zig");

    pub const Box = salsa20.Box;
    pub const SecretBox = salsa20.SecretBox;
    pub const SealedBox = salsa20.SealedBox;
};

/// Finite-field arithmetic.
pub const ff = @import("crypto/ff.zig");

/// Encoding and decoding
pub const codecs = @import("crypto/codecs.zig");

pub const errors = @import("crypto/errors.zig");

pub const tls = @import("crypto/tls.zig");
pub const Certificate = @import("crypto/Certificate.zig");

/// Side-channels mitigations.
pub const SideChannelsMitigations = enum {
    /// No additional side-channel mitigations are applied.
    /// This is the fastest mode.
    none,
    /// The `basic` mode protects against most practical attacks, provided that the
    /// application or implements proper defenses against brute-force attacks.
    /// It offers a good balance between performance and security.
    basic,
    /// The `medium` mode offers increased resilience against side-channel attacks,
    /// making most attacks unpractical even on shared/low latency environements.
    /// This is the default mode.
    medium,
    /// The `full` mode offers the highest level of protection against side-channel attacks.
    /// Note that this doesn't cover all possible attacks (especially power analysis or
    /// thread-local attacks such as cachebleed), and that the performance impact is significant.
    full,
};

pub const default_side_channels_mitigations = .medium;

test {
    _ = aead.ascon.AsconAead128;

    _ = aead.aegis.Aegis128L;
    _ = aead.aegis.Aegis256;

    _ = aead.aes_gcm.Aes128Gcm;
    _ = aead.aes_gcm.Aes256Gcm;

    _ = aead.aes_gcm_siv.Aes128GcmSiv;
    _ = aead.aes_gcm_siv.Aes256GcmSiv;

    _ = aead.aes_siv.Aes128Siv;
    _ = aead.aes_siv.Aes256Siv;

    _ = aead.aes_ocb.Aes128Ocb;
    _ = aead.aes_ocb.Aes256Ocb;

    _ = aead.chacha_poly.ChaCha20Poly1305;
    _ = aead.chacha_poly.ChaCha12Poly1305;
    _ = aead.chacha_poly.ChaCha8Poly1305;
    _ = aead.chacha_poly.XChaCha20Poly1305;
    _ = aead.chacha_poly.XChaCha12Poly1305;
    _ = aead.chacha_poly.XChaCha8Poly1305;

    _ = aead.isap;
    _ = aead.salsa_poly.XSalsa20Poly1305;

    _ = auth.hmac;
    _ = auth.cmac;
    _ = auth.siphash;

    _ = core.aes;
    _ = core.Ascon;
    _ = core.modes;

    _ = dh.X25519;

    _ = kem.kyber_d00;
    _ = kem.hybrid;
    _ = kem.kyber_d00;
    _ = kem.ml_kem;

    _ = ecc.Curve25519;
    _ = ecc.Edwards25519;
    _ = ecc.P256;
    _ = ecc.P384;
    _ = ecc.Ristretto255;
    _ = ecc.Secp256k1;

    _ = hash.ascon;
    _ = hash.blake2;
    _ = hash.Blake3;
    _ = hash.Md5;
    _ = hash.Sha1;
    _ = hash.sha2;
    _ = hash.sha3;
    _ = hash.composition;

    _ = kdf.hkdf;

    _ = onetimeauth.Ghash;
    _ = onetimeauth.Poly1305;

    _ = pwhash.Encoding;

    _ = pwhash.Error;
    _ = pwhash.HasherError;
    _ = pwhash.KdfError;

    _ = pwhash.argon2;
    _ = pwhash.bcrypt;
    _ = pwhash.scrypt;
    _ = pwhash.pbkdf2;

    _ = pwhash.phc_format;

    _ = sign.Ed25519;
    _ = sign.ecdsa;
    _ = sign.mldsa;

    _ = stream.chacha.ChaCha20IETF;
    _ = stream.chacha.ChaCha12IETF;
    _ = stream.chacha.ChaCha8IETF;
    _ = stream.chacha.ChaCha20With64BitNonce;
    _ = stream.chacha.ChaCha12With64BitNonce;
    _ = stream.chacha.ChaCha8With64BitNonce;
    _ = stream.chacha.XChaCha20IETF;
    _ = stream.chacha.XChaCha12IETF;
    _ = stream.chacha.XChaCha8IETF;

    _ = stream.salsa.Salsa20;
    _ = stream.salsa.XSalsa20;

    _ = nacl.Box;
    _ = nacl.SecretBox;
    _ = nacl.SealedBox;

    _ = secureZero;
    _ = timing_safe;
    _ = ff;
    _ = errors;
    _ = tls;
    _ = Certificate;
    _ = codecs;
}

test "issue #4532: no index out of bounds" {
    const types = [_]type{
        hash.Md5,
        hash.Sha1,
        hash.sha2.Sha224,
        hash.sha2.Sha256,
        hash.sha2.Sha384,
        hash.sha2.Sha512,
        hash.sha3.Sha3_224,
        hash.sha3.Sha3_256,
        hash.sha3.Sha3_384,
        hash.sha3.Sha3_512,
        hash.blake2.Blake2s128,
        hash.blake2.Blake2s224,
        hash.blake2.Blake2s256,
        hash.blake2.Blake2b128,
        hash.blake2.Blake2b256,
        hash.blake2.Blake2b384,
        hash.blake2.Blake2b512,
    };

    inline for (types) |Hasher| {
        var block = [_]u8{'#'} ** Hasher.block_length;
        var out1: [Hasher.digest_length]u8 = undefined;
        var out2: [Hasher.digest_length]u8 = undefined;
        const h0 = Hasher.init(.{});
        var h = h0;
        h.update(block[0..]);
        h.final(&out1);
        h = h0;
        h.update(block[0..1]);
        h.update(block[1..]);
        h.final(&out2);

        try std.testing.expectEqual(out1, out2);
    }
}

/// Sets a slice to zeroes.
/// Prevents the store from being optimized out.
pub fn secureZero(comptime T: type, s: []volatile T) void {
    @memset(s, std.mem.zeroes(T));
}

test secureZero {
    var a = [_]u8{0xfe} ** 8;
    var b = [_]u8{0xfe} ** 8;

    @memset(&a, 0);
    secureZero(u8, &b);

    try std.testing.expectEqualSlices(u8, &a, &b);
}



---
File: /std/debug.zig
---

const std = @import("std.zig");
const Io = std.Io;
const Writer = std.Io.Writer;
const math = std.math;
const mem = std.mem;
const posix = std.posix;
const fs = std.fs;
const testing = std.testing;
const Allocator = mem.Allocator;
const File = std.Io.File;
const windows = std.os.windows;

const builtin = @import("builtin");
const native_arch = builtin.cpu.arch;
const native_os = builtin.os.tag;

const root = @import("root");

pub const Dwarf = @import("debug/Dwarf.zig");
pub const Pdb = @import("debug/Pdb.zig");
pub const ElfFile = @import("debug/ElfFile.zig");
pub const MachOFile = @import("debug/MachOFile.zig");
pub const Info = @import("debug/Info.zig");
pub const Coverage = @import("debug/Coverage.zig");
pub const cpu_context = @import("debug/cpu_context.zig");

/// This type abstracts the target-specific implementation of accessing this process' own debug
/// information behind a generic interface which supports looking up source locations associated
/// with addresses, as well as unwinding the stack where a safe mechanism to do so exists.
///
/// The Zig Standard Library provides default implementations of `SelfInfo` for common targets, but
/// the implementation can be overriden by exposing `root.debug.SelfInfo`. Setting `SelfInfo` to
/// `void` indicates that the `SelfInfo` API is not supported.
///
/// This type must expose the following declarations:
///
/// ```
/// pub const init: SelfInfo;
/// pub fn deinit(si: *SelfInfo, io: Io) void;
///
/// /// Appends the symbols for the instruction at `address` to `symbols`.
/// pub fn getSymbols(si: *SelfInfo, io: Io, symbol_allocator: Allocator, text_arena: Allocator, address: usize, include_inline_callers: bool, symbols: *std.ArrayList(Symbol)) SelfInfoError!void;
/// /// Returns a name for the "module" (e.g. shared library or executable image) containing `address`.
/// pub fn getModuleName(si: *SelfInfo, io: Io, address: usize) SelfInfoError![]const u8;
/// pub fn getModuleSlide(si: *SelfInfo, io: Io, address: usize) SelfInfoError!usize;
///
/// /// Whether a reliable stack unwinding strategy, such as DWARF unwinding, is available.
/// pub const can_unwind: bool;
/// /// Only required if `can_unwind == true`.
/// pub const UnwindContext = struct {
///     /// An address representing the instruction pointer in the last frame.
///     pc: usize,
///
///     pub fn init(ctx: *cpu_context.Native) Allocator.Error!UnwindContext;
///     pub fn deinit(ctx: *UnwindContext) void;
///     /// Returns the frame pointer associated with the last unwound stack frame.
///     /// If the frame pointer is unknown, 0 may be returned instead.
///     pub fn getFp(uc: *UnwindContext) usize;
/// };
/// /// Only required if `can_unwind == true`. Unwinds a single stack frame, returning the frame's
/// /// return address, or 0 if the end of the stack has been reached.
/// pub fn unwindFrame(si: *SelfInfo, io: Io, context: *UnwindContext) SelfInfoError!usize;
/// ```
pub const SelfInfo = if (@hasDecl(root, "debug") and @hasDecl(root.debug, "SelfInfo"))
    root.debug.SelfInfo
else switch (std.Target.ObjectFormat.default(native_os, native_arch)) {
    .coff => if (native_os == .windows) @import("debug/SelfInfo/Windows.zig") else void,
    .elf => switch (native_os) {
        .freestanding, .other => void,
        else => @import("debug/SelfInfo/Elf.zig"),
    },
    .macho => @import("debug/SelfInfo/MachO.zig"),
    .plan9, .spirv, .wasm => void,
    .c, .hex, .raw => unreachable,
};

pub const SelfInfoError = error{
    /// The required debug info is invalid or corrupted.
    InvalidDebugInfo,
    /// The required debug info could not be found.
    MissingDebugInfo,
    /// The required debug info was found, and may be valid, but is not supported by this implementation.
    UnsupportedDebugInfo,
    /// The required debug info could not be read from disk due to some IO error.
    ReadFailed,
    OutOfMemory,
    Canceled,
    Unexpected,
};

pub const simple_panic = @import("debug/simple_panic.zig");
pub const no_panic = @import("debug/no_panic.zig");

/// A fully-featured panic handler namespace which lowers all panics to calls to `panicFn`.
/// Safety panics will use formatted printing to provide a meaningful error message.
/// The signature of `panicFn` should match that of `defaultPanic`.
pub fn FullPanic(comptime panicFn: fn ([]const u8, ?usize) noreturn) type {
    return struct {
        pub const call = panicFn;
        pub fn sentinelMismatch(expected: anytype, found: @TypeOf(expected)) noreturn {
            @branchHint(.cold);
            std.debug.panicExtra(@returnAddress(), "sentinel mismatch: expected {any}, found {any}", .{
                expected, found,
            });
        }
        pub fn unwrapError(err: anyerror) noreturn {
            @branchHint(.cold);
            std.debug.panicExtra(@returnAddress(), "attempt to unwrap error: {s}", .{@errorName(err)});
        }
        pub fn outOfBounds(index: usize, len: usize) noreturn {
            @branchHint(.cold);
            std.debug.panicExtra(@returnAddress(), "index out of bounds: index {d}, len {d}", .{ index, len });
        }
        pub fn startGreaterThanEnd(start: usize, end: usize) noreturn {
            @branchHint(.cold);
            std.debug.panicExtra(@returnAddress(), "start index {d} is larger than end index {d}", .{ start, end });
        }
        pub fn inactiveUnionField(active: anytype, accessed: @TypeOf(active)) noreturn {
            @branchHint(.cold);
            std.debug.panicExtra(@returnAddress(), "access of union field '{s}' while field '{s}' is active", .{
                @tagName(accessed), @tagName(active),
            });
        }
        pub fn sliceCastLenRemainder(src_len: usize) noreturn {
            @branchHint(.cold);
            std.debug.panicExtra(@returnAddress(), "slice length '{d}' does not divide exactly into destination elements", .{src_len});
        }
        pub fn reachedUnreachable() noreturn {
            @branchHint(.cold);
            call("reached unreachable code", @returnAddress());
        }
        pub fn unwrapNull() noreturn {
            @branchHint(.cold);
            call("attempt to use null value", @returnAddress());
        }
        pub fn castToNull() noreturn {
            @branchHint(.cold);
            call("cast causes pointer to be null", @returnAddress());
        }
        pub fn incorrectAlignment() noreturn {
            @branchHint(.cold);
            call("incorrect alignment", @returnAddress());
        }
        pub fn invalidErrorCode() noreturn {
            @branchHint(.cold);
            call("invalid error code", @returnAddress());
        }
        pub fn integerOutOfBounds() noreturn {
            @branchHint(.cold);
            call("integer does not fit in destination type", @returnAddress());
        }
        pub fn integerOverflow() noreturn {
            @branchHint(.cold);
            call("integer overflow", @returnAddress());
        }
        pub fn shlOverflow() noreturn {
            @branchHint(.cold);
            call("left shift overflowed bits", @returnAddress());
        }
        pub fn shrOverflow() noreturn {
            @branchHint(.cold);
            call("right shift overflowed bits", @returnAddress());
        }
        pub fn divideByZero() noreturn {
            @branchHint(.cold);
            call("division by zero", @returnAddress());
        }
        pub fn exactDivisionRemainder() noreturn {
            @branchHint(.cold);
            call("exact division produced remainder", @returnAddress());
        }
        pub fn integerPartOutOfBounds() noreturn {
            @branchHint(.cold);
            call("integer part of floating point value out of bounds", @returnAddress());
        }
        pub fn corruptSwitch() noreturn {
            @branchHint(.cold);
            call("switch on corrupt value", @returnAddress());
        }
        pub fn shiftRhsTooBig() noreturn {
            @branchHint(.cold);
            call("shift amount is greater than the type size", @returnAddress());
        }
        pub fn invalidEnumValue() noreturn {
            @branchHint(.cold);
            call("invalid enum value", @returnAddress());
        }
        pub fn forLenMismatch() noreturn {
            @branchHint(.cold);
            call("for loop over objects with non-equal lengths", @returnAddress());
        }
        pub fn copyLenMismatch() noreturn {
            @branchHint(.cold);
            call("source and destination arguments have non-equal lengths", @returnAddress());
        }
        pub fn memcpyAlias() noreturn {
            @branchHint(.cold);
            call("@memcpy arguments alias", @returnAddress());
        }
        pub fn noreturnReturned() noreturn {
            @branchHint(.cold);
            call("'noreturn' function returned", @returnAddress());
        }
    };
}

/// Unresolved source locations can be represented with a single `usize` that
/// corresponds to a virtual memory address of the program counter. Combined
/// with debug information, those values can be converted into a resolved
/// source location, including file, line, and column.
pub const SourceLocation = struct {
    line: u64,
    column: u64,
    file_name: []const u8,

    pub const invalid: SourceLocation = .{
        .line = 0,
        .column = 0,
        .file_name = &.{},
    };
};

pub const Symbol = struct {
    name: ?[]const u8,
    compile_unit_name: ?[]const u8,
    source_location: ?SourceLocation,
    pub const unknown: Symbol = .{
        .name = null,
        .compile_unit_name = null,
        .source_location = null,
    };
};

/// Deprecated because it returns the optimization mode of the standard
/// library, when the caller probably wants to use the optimization mode of
/// their own module.
pub const runtime_safety = switch (builtin.mode) {
    .Debug, .ReleaseSafe => true,
    .ReleaseFast, .ReleaseSmall => false,
};

/// Whether we can unwind the stack on this target, allowing capturing and/or printing the current
/// stack trace. It is still legal to call `captureCurrentStackTrace`, `writeCurrentStackTrace`, and
/// `dumpCurrentStackTrace` if this is `false`; it will just print an error / capture an empty
/// trace due to missing functionality. This value is just intended as a heuristic to avoid
/// pointless work e.g. capturing always-empty stack traces.
pub const sys_can_stack_trace = switch (builtin.cpu.arch) {
    // `@returnAddress()` in LLVM 10 gives
    // "Non-Emscripten WebAssembly hasn't implemented __builtin_return_address".
    // On Emscripten, Zig only supports `@returnAddress()` in debug builds
    // because Emscripten's implementation is very slow.
    .wasm32,
    .wasm64,
    => native_os == .emscripten and builtin.mode == .Debug,

    // `@returnAddress()` is unsupported in LLVM 21.
    .bpfel,
    .bpfeb,
    => false,

    else => true,
};

/// Allows the caller to freely write to stderr until `unlockStderr` is called.
///
/// During the lock, any `std.Progress` information is cleared from the terminal.
///
/// The lock is recursive, so it is valid for the same thread to call
/// `lockStderr` multiple times, allowing the panic handler to safely
/// dump the stack trace and panic message even if the mutex was held at the
/// panic site.
///
/// The returned `Writer` does not need to be manually flushed: flushing is
/// performed automatically when the matching `unlockStderr` call occurs.
///
/// This is a low-level debugging primitive that bypasses the `Io` interface,
/// writing directly to stderr using the most basic syscalls available. This
/// function does not switch threads, switch stacks, or suspend.
///
/// Alternatively, use the higher-level `Io.lockStderr` to integrate with the
/// application's chosen `Io` implementation.
pub fn lockStderr(buffer: []u8) Io.LockedStderr {
    const io = std.Options.debug_io;
    const prev = io.swapCancelProtection(.blocked);
    defer _ = io.swapCancelProtection(prev);
    return io.lockStderr(buffer, null) catch |err| switch (err) {
        error.Canceled => unreachable, // Cancel protection enabled above.
    };
}

pub fn unlockStderr() void {
    const io = std.Options.debug_io;
    io.unlockStderr();
}

/// Writes to stderr, ignoring errors.
///
/// This is a low-level debugging primitive that bypasses the `Io` interface,
/// writing directly to stderr using the most basic syscalls available. This
/// function does not switch threads, switch stacks, or suspend.
///
/// Uses a 64-byte buffer for formatted printing which is flushed before this
/// function returns.
///
/// Alternatively, use the higher-level `std.log` or `Io.lockStderr` to
/// integrate with the application's chosen `Io` implementation.
pub fn print(comptime fmt: []const u8, args: anytype) void {
    var buffer: [64]u8 = undefined;
    const stderr = lockStderr(&buffer);
    defer unlockStderr();
    stderr.file_writer.interface.print(fmt, args) catch return;
}

/// Marked `inline` to propagate a comptime-known error to callers.
pub inline fn getSelfDebugInfo() !*SelfInfo {
    if (SelfInfo == void) return error.UnsupportedTarget;
    const S = struct {
        var self_info: SelfInfo = .init;
    };
    return &S.self_info;
}

/// Tries to print a hexadecimal view of the bytes, unbuffered, and ignores any error returned.
/// Obtains the stderr mutex while dumping.
pub fn dumpHex(bytes: []const u8) void {
    const stderr = lockStderr(&.{}).terminal();
    defer unlockStderr();
    dumpHexFallible(stderr, bytes) catch {};
}

/// Prints a hexadecimal view of the bytes, returning any error that occurs.
pub fn dumpHexFallible(t: Io.Terminal, bytes: []const u8) !void {
    const w = t.writer;
    var chunks = mem.window(u8, bytes, 16, 16);
    while (chunks.next()) |window| {
        // 1. Print the address.
        const address = (@intFromPtr(bytes.ptr) + 0x10 * (std.math.divCeil(usize, chunks.index orelse bytes.len, 16) catch unreachable)) - 0x10;
        try t.setColor(.dim);
        // We print the address in lowercase and the bytes in uppercase hexadecimal to distinguish them more.
        // Also, make sure all lines are aligned by padding the address.
        try w.print("{x:0>[1]}  ", .{ address, @sizeOf(usize) * 2 });
        try t.setColor(.reset);

        // 2. Print the bytes.
        for (window, 0..) |byte, index| {
            try w.print("{X:0>2} ", .{byte});
            if (index == 7) try w.writeByte(' ');
        }
        try w.writeByte(' ');
        if (window.len < 16) {
            var missing_columns = (16 - window.len) * 3;
            if (window.len < 8) missing_columns += 1;
            try w.splatByteAll(' ', missing_columns);
        }

        // 3. Print the characters.
        for (window) |byte| {
            if (std.ascii.isPrint(byte)) {
                try w.writeByte(byte);
            } else {
                // Related: https://github.com/ziglang/zig/issues/7600
                if (t.mode == .windows_api) {
                    try w.writeByte('.');
                    continue;
                }

                // Let's print some common control codes as graphical Unicode symbols.
                // We don't want to do this for all control codes because most control codes apart from
                // the ones that Zig has escape sequences for are likely not very useful to print as symbols.
                switch (byte) {
                    '\n' => try w.writeAll("␊"),
                    '\r' => try w.writeAll("␍"),
                    '\t' => try w.writeAll("␉"),
                    else => try w.writeByte('.'),
                }
            }
        }
        try w.writeByte('\n');
    }
}

test dumpHexFallible {
    const gpa = testing.allocator;
    const bytes: []const u8 = &.{ 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x01, 0x12, 0x13 };
    var aw: Writer.Allocating = .init(gpa);
    defer aw.deinit();

    try dumpHexFallible(.{ .writer = &aw.writer, .mode = .no_color }, bytes);
    const expected = try std.fmt.allocPrint(gpa,
        \\{x:0>[2]}  00 11 22 33 44 55 66 77  88 99 AA BB CC DD EE FF  .."3DUfw........
        \\{x:0>[2]}  01 12 13                                          ...
        \\
    , .{
        @intFromPtr(bytes.ptr),
        @intFromPtr(bytes.ptr) + 16,
        @sizeOf(usize) * 2,
    });
    defer gpa.free(expected);
    try testing.expectEqualStrings(expected, aw.written());
}

/// The pointer through which a `cpu_context.Native` is received from callers of stack tracing logic.
pub const CpuContextPtr = if (cpu_context.Native == noreturn) noreturn else *const cpu_context.Native;

/// Invokes detectable illegal behavior when `ok` is `false`.
///
/// In Debug and ReleaseSafe modes, calls to this function are always
/// generated, and the `unreachable` statement triggers a panic.
///
/// In ReleaseFast and ReleaseSmall modes, calls to this function are optimized
/// away, and in fact the optimizer is able to use the assertion in its
/// heuristics.
///
/// Inside a test block, it is best to use the `testing` module rather than
/// this function, because this function may not detect a test failure in
/// ReleaseFast and ReleaseSmall mode. Outside of a test block, this assert
/// function is the correct function to use.
pub fn assert(ok: bool) void {
    @disableInstrumentation();
    if (!ok) unreachable; // assertion failure
}

/// Invokes detectable illegal behavior when the provided slice is not mapped
/// or lacks read permissions.
pub fn assertReadable(slice: []const volatile u8) void {
    if (!runtime_safety) return;
    for (slice) |*byte| _ = byte.*;
}

/// Invokes detectable illegal behavior when the provided array is not aligned
/// to the provided amount.
pub fn assertAligned(ptr: anytype, comptime alignment: std.mem.Alignment) void {
    const aligned_ptr: *align(alignment.toByteUnits()) const anyopaque = @ptrCast(@alignCast(ptr));
    _ = aligned_ptr;
}

/// Equivalent to `@panic` but with a formatted message.
pub fn panic(comptime format: []const u8, args: anytype) noreturn {
    @branchHint(.cold);
    panicExtra(@returnAddress(), format, args);
}

/// Equivalent to `@panic` but with a formatted message and an explicitly provided return address
/// which will be the first address in the stack trace.
pub fn panicExtra(
    ret_addr: ?usize,
    comptime format: []const u8,
    args: anytype,
) noreturn {
    @branchHint(.cold);

    const size = 0x1000;
    const trunc_msg = "(msg truncated)";
    var buf: [size + trunc_msg.len]u8 = undefined;
    var bw: Writer = .fixed(buf[0..size]);
    // a minor annoyance with this is that it will result in the NoSpaceLeft
    // error being part of the @panic stack trace (but that error should
    // only happen rarely)
    const msg = if (bw.print(format, args)) |_| bw.buffered() else |_| blk: {
        @memcpy(buf[size..], trunc_msg);
        break :blk &buf;
    };
    std.builtin.panic.call(msg, ret_addr);
}

/// Non-zero whenever the program triggered a panic.
/// The counter is incremented/decremented atomically.
var panicking = std.atomic.Value(u8).init(0);

/// Counts how many times the panic handler is invoked by this thread.
/// This is used to catch and handle panics triggered by the panic handler.
threadlocal var panic_stage: usize = 0;

/// For backends that cannot handle the language features depended on by the
/// default panic handler, we will use a simpler implementation.
const use_trap_panic = switch (builtin.zig_backend) {
    .stage2_aarch64,
    .stage2_arm,
    .stage2_powerpc,
    .stage2_riscv64,
    .stage2_spirv,
    .stage2_wasm,
    .stage2_x86,
    => true,
    else => false,
};

/// Dumps a stack trace to standard error, then aborts.
pub fn defaultPanic(msg: []const u8, first_trace_addr: ?usize) noreturn {
    @branchHint(.cold);

    if (use_trap_panic) @trap();

    switch (builtin.os.tag) {
        .freestanding, .other, .@"3ds", .vita => {
            @trap();
        },
        .uefi => {
            const uefi = std.os.uefi;

            var utf16_buffer: [1000]u16 = undefined;
            const len_minus_3 = std.unicode.utf8ToUtf16Le(&utf16_buffer, msg) catch 0;
            utf16_buffer[len_minus_3..][0..3].* = .{ '\r', '\n', 0 };
            const len = len_minus_3 + 3;
            const exit_msg = utf16_buffer[0 .. len - 1 :0];

            // Output to both std_err and con_out, as std_err is easier
            // to read in stuff like QEMU at times, but, unlike con_out,
            // isn't visible on actual hardware if directly booted into
            inline for ([_]?*uefi.protocol.SimpleTextOutput{ uefi.system_table.std_err, uefi.system_table.con_out }) |o| {
                if (o) |out| {
                    out.setAttribute(.{ .foreground = .red }) catch {};
                    _ = out.outputString(exit_msg) catch {};
                    out.setAttribute(.{ .foreground = .white }) catch {};
                }
            }

            if (uefi.system_table.boot_services) |bs| {
                // ExitData buffer must be allocated using boot_services.allocatePool (spec: page 220)
                const exit_data = uefi.raw_pool_allocator.dupeZ(u16, exit_msg) catch @trap();
                bs.exit(uefi.handle, .aborted, exit_data) catch {};
            }
            @trap();
        },
        .cuda, .amdhsa => std.process.abort(),
        .plan9 => {
            var status: [std.os.plan9.ERRMAX]u8 = undefined;
            const len = @min(msg.len, status.len - 1);
            @memcpy(status[0..len], msg[0..len]);
            status[len] = 0;
            std.os.plan9.exits(status[0..len :0]);
        },
        else => {},
    }

    std.Options.debug_io.vtable.crashHandler(std.Options.debug_io.userdata);

    if (enable_segfault_handler) {
        // If a segfault happens while panicking, we want it to actually segfault, not trigger
        // the handler.
        resetSegfaultHandler();
    }

    // There is very similar logic to the following in `handleSegfault`.
    switch (panic_stage) {
        0 => {
            panic_stage = 1;
            _ = panicking.fetchAdd(1, .seq_cst);

            trace: {
                const stderr = lockStderr(&.{}).terminal();
                defer unlockStderr();
                const writer = stderr.writer;

                if (builtin.single_threaded) {
                    writer.print("panic: ", .{}) catch break :trace;
                } else {
                    const current_thread_id = std.Thread.getCurrentId();
                    writer.print("thread {d} panic: ", .{current_thread_id}) catch break :trace;
                }
                writer.print("{s}\n", .{msg}) catch break :trace;

                if (@errorReturnTrace()) |t| if (t.index > 0) {
                    writer.writeAll("error return context:\n") catch break :trace;
                    writeErrorReturnTrace(t, stderr) catch break :trace;
                    writer.writeAll("\nstack trace:\n") catch break :trace;
                };
                writeCurrentStackTrace(.{
                    .first_address = first_trace_addr orelse @returnAddress(),
                    .allow_unsafe_unwind = true, // we're crashing anyway, give it our all!
                }, stderr) catch break :trace;
            }

            waitForOtherThreadToFinishPanicking();
        },
        1 => {
            panic_stage = 2;
            // A panic happened while trying to print a previous panic message.
            // We're still holding the mutex but that's fine as we're going to
            // call abort().
            const stderr = lockStderr(&.{}).terminal();
            stderr.writer.writeAll("aborting due to recursive panic\n") catch {};
        },
        else => {}, // Panicked while printing the recursive panic message.
    }

    std.process.abort();
}

/// Must be called only after adding 1 to `panicking`. There are three callsites.
fn waitForOtherThreadToFinishPanicking() void {
    if (panicking.fetchSub(1, .seq_cst) != 1) {
        // Another thread is panicking, wait for the last one to finish
        // and call abort()
        if (builtin.single_threaded) unreachable;

        // Sleep forever without hammering the CPU
        var futex: u32 = 0;
        while (true) std.Options.debug_io.futexWaitUncancelable(u32, &futex, 0);
        unreachable;
    }
}

pub const StackTrace = struct {
    /// Each element is the "return address" of a function call, meaning the instruction address
    /// which control flow will return to when the function returns.
    ///
    /// The first slice element corresponds to the innermost stack frame, and the last element to
    /// the outermost.
    ///
    /// Inlined function calls do not have meaningful return addresses and are therefore not
    /// included in this slice. Instead, when printing the stack trace, the source locations of
    /// inline calls should be read from debug information and the corresponding "inline frames"
    /// printed in the appropriate locations.
    return_addresses: []usize,
    /// Indicates whether any stack frames were omitted from `return_addresses`.
    skipped: SkippedAddresses,
};

/// Indicates how many addresses were skipped in a trace.
pub const SkippedAddresses = enum(usize) {
    /// No addresses were omitted: `return_addresses` contains all stack frames, including the
    /// outermost.
    none = 0,
    /// It is not known whether any frames were omitted.
    unknown = std.math.maxInt(usize),
    /// The full stack trace was available, but some frames are not included in
    /// `return_addresses` due to buffer size limitations. The enum value is the exact number of
    /// addresses which were omitted.
    _,
};

pub const StackUnwindOptions = struct {
    /// If not `null`, we will ignore all frames up until this return address. This is typically
    /// used to omit intermediate handling code (for instance, a panic handler and its machinery)
    /// from stack traces.
    first_address: ?usize = null,
    /// If not `null`, we will unwind from this `cpu_context.Native` instead of the current top of
    /// the stack. The main use case here is printing stack traces from signal handlers, where the
    /// kernel provides a `*const cpu_context.Native` of the state before the signal.
    context: ?CpuContextPtr = null,
    /// If `true`, stack unwinding strategies which may cause crashes are used as a last resort.
    /// If `false`, only known-safe mechanisms will be attempted.
    allow_unsafe_unwind: bool = false,
};

/// Capture and return the current stack trace. The returned `StackTrace` stores its addresses in
/// the given buffer, so `addr_buf` must have a lifetime at least equal to the `StackTrace`.
///
/// See `writeCurrentStackTrace` to immediately print the trace instead of capturing it.
pub noinline fn captureCurrentStackTrace(options: StackUnwindOptions, addr_buf: []usize) StackTrace {
    const empty_trace: StackTrace = .{
        .return_addresses = &.{},
        .skipped = .none,
    };
    if (!std.options.allow_stack_tracing) return empty_trace;
    var it: StackIterator = .init(options.context);
    defer it.deinit();
    if (!it.stratOk(options.allow_unsafe_unwind)) return empty_trace;

    const io = std.Options.debug_io;

    var total_frames: usize = 0;
    var index: usize = 0;
    var wait_for = options.first_address;
    // Ideally, we would iterate the whole stack so that the `index - min(buf.len, index)` would be
    // indicative of how many frames were skipped. However, this has a significant runtime cost
    // in some cases, so at least for now, we don't do that.
    const skipped: SkippedAddresses = while (index < addr_buf.len) switch (it.next(io)) {
        .switch_to_fp => if (!it.stratOk(options.allow_unsafe_unwind)) break .unknown,
        .end => break .none,
        .frame => |ret_addr| {
            if (total_frames > 10_000) {
                // Limit the number of frames in case of (e.g.) broken debug information which is
                // getting unwinding stuck in a loop.
                break .unknown;
            }
            total_frames += 1;
            if (wait_for) |target| {
                if (ret_addr != target) continue;
                wait_for = null;
            }
            addr_buf[index] = ret_addr;
            index += 1;
        },
    } else .unknown;
    return .{
        .return_addresses = addr_buf[0..index],
        .skipped = skipped,
    };
}
/// Write the current stack trace to `writer`, annotated with source locations.
///
/// See `captureCurrentStackTrace` to capture the trace addresses into a buffer instead of printing.
pub noinline fn writeCurrentStackTrace(options: StackUnwindOptions, t: Io.Terminal) Writer.Error!void {
    const writer = t.writer;

    var text_arena: std.heap.ArenaAllocator = .init(getDebugInfoAllocator());
    defer text_arena.deinit();

    if (!std.options.allow_stack_tracing) {
        t.setColor(.dim) catch {};
        try writer.print("Cannot print stack trace: stack tracing is disabled\n", .{});
        t.setColor(.reset) catch {};
        return;
    }
    const di = getSelfDebugInfo() catch |err| switch (err) {
        error.UnsupportedTarget => {
            t.setColor(.dim) catch {};
            try writer.print("Cannot print stack trace: debug info unavailable for target\n", .{});
            t.setColor(.reset) catch {};
            return;
        },
    };
    var it: StackIterator = .init(options.context);
    defer it.deinit();
    if (!it.stratOk(options.allow_unsafe_unwind)) {
        t.setColor(.dim) catch {};
        try writer.print("Cannot print stack trace: safe unwind unavailable for target\n", .{});
        t.setColor(.reset) catch {};
        return;
    }
    var total_frames: usize = 0;
    var wait_for = options.first_address;
    var printed_any_frame = false;
    const io = std.Options.debug_io;
    while (true) switch (it.next(io)) {
        .switch_to_fp => |unwind_error| {
            switch (StackIterator.fp_usability) {
                .useless, .unsafe => {},
                .safe, .ideal => continue, // no need to even warn
            }
            const module_name = di.getModuleName(io, unwind_error.address) catch "???";
            const caption: []const u8 = switch (unwind_error.err) {
                error.MissingDebugInfo => "unwind info unavailable",
                error.InvalidDebugInfo => "unwind info invalid",
                error.UnsupportedDebugInfo => "unwind info unsupported",
                error.ReadFailed => "filesystem error",
                error.OutOfMemory => "out of memory",
                error.Canceled => "operation canceled",
                error.Unexpected => "unexpected error",
            };
            if (it.stratOk(options.allow_unsafe_unwind)) {
                t.setColor(.dim) catch {};
                try writer.print(
                    "Unwind error at address `{s}:0x{x}` ({s}), remaining frames may be incorrect\n",
                    .{ module_name, unwind_error.address, caption },
                );
                t.setColor(.reset) catch {};
            } else {
                t.setColor(.dim) catch {};
                try writer.print(
                    "Unwind error at address `{s}:0x{x}` ({s}), stopping trace early\n",
                    .{ module_name, unwind_error.address, caption },
                );
                t.setColor(.reset) catch {};
                return;
            }
        },
        .end => break,
        .frame => |ret_addr| {
            if (total_frames > 10_000) {
                t.setColor(.dim) catch {};
                try writer.print(
                    "Stopping trace after {d} frames (large frame count may indicate broken debug info)\n",
                    .{total_frames},
                );
                t.setColor(.reset) catch {};
                return;
            }
            total_frames += 1;
            if (wait_for) |target| {
                if (ret_addr != target) continue;
                wait_for = null;
            }
            // `ret_addr` is the return address, which is *after* the function call.
            // Subtract 1 to get an address *in* the function call for a better source location.
            try printSourceAtAddress(io, &text_arena, di, t, .{
                .address = ret_addr -| StackIterator.ra_call_offset,
                .resolve_inline_callers = true,
            });
            printed_any_frame = true;
        },
    };
    if (!printed_any_frame) return writer.writeAll("(empty stack trace)\n");
}
/// A thin wrapper around `writeCurrentStackTrace` which writes to stderr and ignores write errors.
pub fn dumpCurrentStackTrace(options: StackUnwindOptions) void {
    const stderr = lockStderr(&.{}).terminal();
    defer unlockStderr();
    writeCurrentStackTrace(.{
        .first_address = a: {
            if (options.first_address) |a| break :a a;
            if (options.context != null) break :a null;
            break :a @returnAddress(); // don't include this frame in the trace
        },
        .context = options.context,
        .allow_unsafe_unwind = options.allow_unsafe_unwind,
    }, stderr) catch |err| switch (err) {
        error.WriteFailed => {},
    };
}

pub const FormatStackTrace = struct {
    stack_trace: StackTrace,
    terminal_mode: Io.Terminal.Mode = .no_color,

    pub fn format(fst: FormatStackTrace, writer: *Writer) Writer.Error!void {
        try writer.writeByte('\n');
        try writeStackTrace(&fst.stack_trace, .{ .writer = writer, .mode = fst.terminal_mode });
    }
};

/// Write a previously captured error return trace to `writer`, annotated with source locations.
pub fn writeErrorReturnTrace(et: *const std.builtin.StackTrace, t: Io.Terminal) Writer.Error!void {
    // We take the slice by value, preventing the length from being mutated if an error occurs while
    // writing the stack trace.
    const len = @min(et.instruction_addresses.len, et.index);
    const skipped = et.index - len;
    try writeTrace(et.instruction_addresses[0..len], @enumFromInt(skipped), t, false);
}

/// Write a previously captured stack trace to `writer`, annotated with source locations.
pub fn writeStackTrace(st: *const StackTrace, t: Io.Terminal) Writer.Error!void {
    try writeTrace(st.return_addresses, st.skipped, t, true);
}

fn writeTrace(
    addresses: []const usize,
    skipped: SkippedAddresses,
    t: Io.Terminal,
    resolve_inline_callers: bool,
) Writer.Error!void {
    var text_arena: std.heap.ArenaAllocator = .init(getDebugInfoAllocator());
    defer text_arena.deinit();

    const writer = t.writer;
    if (!std.options.allow_stack_tracing) {
        t.setColor(.dim) catch {};
        try writer.print("Cannot print stack trace: stack tracing is disabled\n", .{});
        t.setColor(.reset) catch {};
        return;
    }

    if (addresses.len == 0) return writer.writeAll("(empty stack trace)\n");
    const di = getSelfDebugInfo() catch |err| switch (err) {
        error.UnsupportedTarget => {
            t.setColor(.dim) catch {};
            try writer.print("Cannot print stack trace: debug info unavailable for target\n\n", .{});
            t.setColor(.reset) catch {};
            return;
        },
    };
    const io = std.Options.debug_io;
    for (addresses) |addr| {
        // `addr` is the return address, which is *after* the function call.
        // Subtract 1 to get an address *in* the function call for a better source location.
        try printSourceAtAddress(io, &text_arena, di, t, .{
            .address = addr -| StackIterator.ra_call_offset,
            .resolve_inline_callers = resolve_inline_callers,
        });
    }
    switch (skipped) {
        .none => {},
        .unknown => {
            t.setColor(.bold) catch {};
            try writer.writeAll("(additional stack frames may have been skipped...)\n");
            t.setColor(.reset) catch {};
        },
        else => |n| {
            t.setColor(.bold) catch {};
            try writer.print("({d} additional stack frames skipped due to buffer size limitations...)\n", .{n});
            t.setColor(.reset) catch {};
        },
    }
}
/// A thin wrapper around `writeStackTrace` which writes to stderr and ignores write errors.
pub fn dumpStackTrace(st: *const StackTrace) void {
    const stderr = lockStderr(&.{}).terminal();
    defer unlockStderr();
    writeStackTrace(st, stderr) catch |err| switch (err) {
        error.WriteFailed => {},
    };
}

/// A thin wrapper around `writeErrorReturnTrace` which writes to stderr and ignores write errors.
pub fn dumpErrorReturnTrace(et: *const std.builtin.StackTrace) void {
    const stderr = lockStderr(&.{}).terminal();
    defer unlockStderr();
    writeErrorReturnTrace(et, stderr) catch |err| switch (err) {
        error.WriteFailed => {},
    };
}

const StackIterator = union(enum) {
    /// We will first report the current PC of this `CpuContextPtr`, then we will switch to a
    /// different strategy to actually unwind.
    ctx_first: CpuContextPtr,
    /// Unwinding using debug info (e.g. DWARF CFI).
    di: if (SelfInfo != void and SelfInfo.can_unwind and fp_usability != .ideal)
        SelfInfo.UnwindContext
    else
        noreturn,
    /// Naive frame-pointer-based unwinding. Very simple, but typically unreliable.
    fp: usize,

    /// It is important that this function is marked `inline` so that it can safely use
    /// `@frameAddress` and `cpu_context.Native.current` as the caller's stack frame and
    /// our own are one and the same.
    ///
    /// `opt_context_ptr` must remain valid while the `StackIterator` is used.
    inline fn init(opt_context_ptr: ?CpuContextPtr) StackIterator {
        if (opt_context_ptr) |context_ptr| {
            // Use `ctx_first` here so we report the PC in the context before unwinding any further.
            return .{ .ctx_first = context_ptr };
        }

        // Otherwise, we're going to capture the current context or frame address, so we don't need
        // `ctx_first`, because the first PC is in `std.debug` and we need to unwind before reaching
        // a frame we want to report.

        // Workaround the C backend being unable to use inline assembly on MSVC by disabling the
        // call to `current`. This effectively constrains stack trace collection and dumping to FP
        // unwinding when building with CBE for MSVC.
        if (!(builtin.zig_backend == .stage2_c and builtin.target.abi == .msvc) and
            SelfInfo != void and
            SelfInfo.can_unwind and
            cpu_context.Native != noreturn and
            fp_usability != .ideal)
        {
            return .{ .di = .init(&.current()) };
        }
        return .{
            // On SPARC, the frame pointer will point to the previous frame's save area,
            // meaning we will read the previous return address and thus miss a frame.
            // Instead, start at the stack pointer so we get the return address from the
            // current frame's save area. The addition of the stack bias cannot fail here
            // since we know we have a valid stack pointer.
            .fp = if (native_arch.isSPARC()) sp: {
                flushSparcWindows();
                break :sp asm (""
                    : [_] "={o6}" (-> usize),
                ) + stack_bias;
            } else @frameAddress(),
        };
    }
    fn deinit(si: *StackIterator) void {
        switch (si.*) {
            .ctx_first => {},
            .fp => {},
            .di => |*unwind_context| unwind_context.deinit(),
        }
    }

    noinline fn flushSparcWindows() void {
        // Flush all register windows except the current one (hence `noinline`). This ensures that
        // we actually see meaningful data on the stack when we walk the frame chain.
        if (comptime builtin.target.cpu.has(.sparc, .v9))
            asm volatile ("flushw" ::: .{ .memory = true })
        else
            asm volatile ("ta 3" ::: .{ .memory = true }); // ST_FLUSH_WINDOWS
    }

    const FpUsability = enum {
        /// FP unwinding is impractical on this target. For example, due to its very silly ABI
        /// design decisions, it's not possible to do generic FP unwinding on MIPS without a
        /// complicated code scanning algorithm.
        useless,
        /// FP unwinding is unsafe on this target; we may crash when doing so. We will only perform
        /// FP unwinding in the case of crashes/panics, or if the user opts in.
        unsafe,
        /// FP unwinding is guaranteed to be safe on this target. We will do so if unwinding with
        /// debug info does not work, and if this compilation has frame pointers enabled.
        safe,
        /// FP unwinding is the best option on this target. This is usually because the ABI requires
        /// a backchain pointer, thus making it always available, safe, and fast.
        ideal,
    };

    const fp_usability: FpUsability = switch (builtin.target.cpu.arch) {
        .alpha,
        .avr,
        .csky,
        .microblaze,
        .microblazeel,
        .mips,
        .mipsel,
        .mips64,
        .mips64el,
        .msp430,
        .sh,
        .sheb,
        .xcore,
        => .useless,
        .hexagon,
        // The PowerPC ABIs don't actually strictly require a backchain pointer; they allow omitting
        // it when full unwind info is present. Despite this, both GCC and Clang always enforce the
        // presence of the backchain pointer no matter what options they are given. This seems to be
        // a case of "the spec is only a polite suggestion", except it works in our favor this time!
        .powerpc,
        .powerpcle,
        .powerpc64,
        .powerpc64le,
        .sparc,
        .sparc64,
        => .ideal,
        // https://developer.apple.com/documentation/xcode/writing-arm64-code-for-apple-platforms#Respect-the-purpose-of-specific-CPU-registers
        .aarch64 => if (builtin.target.os.tag.isDarwin()) .safe else .unsafe,
        else => .unsafe,
    };

    /// Whether the current unwind strategy is allowed given `allow_unsafe`.
    fn stratOk(it: *const StackIterator, allow_unsafe: bool) bool {
        return switch (it.*) {
            .ctx_first, .di => true,
            // If we omitted frame pointers from *this* compilation, FP unwinding would crash
            // immediately regardless of anything. But FPs could also be omitted from a different
            // linked object, so it's not guaranteed to be safe, unless the target specifically
            // requires it.
            .fp => switch (fp_usability) {
                .useless => false,
                .unsafe => allow_unsafe and !builtin.omit_frame_pointer,
                .safe => !builtin.omit_frame_pointer,
                .ideal => true,
            },
        };
    }

    const Result = union(enum) {
        /// A stack frame has been found; this is the corresponding return address.
        frame: usize,
        /// The end of the stack has been reached.
        end,
        /// We were using `SelfInfo.UnwindInfo`, but are now switching to FP unwinding due to this error.
        switch_to_fp: struct {
            address: usize,
            err: SelfInfoError,
        },
    };

    fn next(it: *StackIterator, io: Io) Result {
        switch (it.*) {
            .ctx_first => |context_ptr| {
                // After the first frame, start actually unwinding.
                it.* = if (SelfInfo != void and SelfInfo.can_unwind and fp_usability != .ideal)
                    .{ .di = .init(context_ptr) }
                else
                    .{ .fp = context_ptr.getFp() };

                // The caller expects *return* addresses, where they will subtract 1 to find the address of the call.
                // However, we have the actual current PC, which should not be adjusted. Compensate by adding 1.
                return .{ .frame = context_ptr.getPc() +| 1 };
            },
            .di => |*unwind_context| {
                const di = getSelfDebugInfo() catch unreachable;
                const ret_addr = di.unwindFrame(io, unwind_context) catch |err| {
                    const pc = unwind_context.pc;
                    const fp = unwind_context.getFp();
                    it.* = .{ .fp = fp };
                    return .{ .switch_to_fp = .{
                        .address = pc,
                        .err = err,
                    } };
                };
                if (ret_addr <= 1) return .end;
                return .{ .frame = ret_addr };
            },
            .fp => |fp| {
                if (fp == 0) return .end; // we reached the "sentinel" base pointer

                const bp_addr = applyOffset(fp, fp_to_bp_offset) orelse return .end;
                const ra_addr = applyOffset(fp, fp_to_ra_offset) orelse return .end;

                if (bp_addr == 0 or !mem.isAligned(bp_addr, @alignOf(usize)) or
                    ra_addr == 0 or !mem.isAligned(ra_addr, @alignOf(usize)))
                {
                    // This isn't valid, but it most likely indicates end of stack.
                    return .end;
                }

                const bp_ptr: *const usize = @ptrFromInt(bp_addr);
                const ra_ptr: *const usize = @ptrFromInt(ra_addr);
                const bp = applyOffset(bp_ptr.*, stack_bias) orelse return .end;

                // If the stack grows downwards, `bp > fp` should always hold; conversely, if it
                // grows upwards, `bp < fp` should always hold. If that is not the case, this
                // frame is invalid, so we'll treat it as though we reached end of stack. The
                // exception is address 0, which is a graceful end-of-stack signal, in which case
                // *this* return address is valid and the *next* iteration will be the last.
                if (bp != 0 and switch (comptime builtin.target.stackGrowth()) {
                    .down => bp <= fp,
                    .up => bp >= fp,
                }) return .end;

                it.fp = bp;
                const ra = stripInstructionPtrAuthCode(ra_ptr.*);
                if (ra <= 1) return .end;
                return .{ .frame = ra };
            },
        }
    }

    /// Offset of the saved base pointer (previous frame pointer) wrt the frame pointer.
    const fp_to_bp_offset = off: {
        // On 32-bit PA-RISC, the base pointer is the final word of the frame marker.
        if (native_arch == .hppa) break :off -1 * @sizeOf(usize);
        // On 64-bit PA-RISC, the frame marker was shrunk significantly; now there's just the return
        // address followed by the base pointer.
        if (native_arch == .hppa64) break :off -1 * @sizeOf(usize);
        // On LoongArch and RISC-V, the frame pointer points to the top of the saved register area,
        // in which the base pointer is the first word.
        if (native_arch.isLoongArch() or native_arch.isRISCV()) break :off -2 * @sizeOf(usize);
        // On OpenRISC, the frame pointer is stored below the return address.
        if (native_arch == .or1k) break :off -2 * @sizeOf(usize);
        // On SPARC, the frame pointer points to the save area which holds 16 slots for the local
        // and incoming registers. The base pointer (i6) is stored in its customary save slot.
        if (native_arch.isSPARC()) break :off 14 * @sizeOf(usize);
        // Everywhere else, the frame pointer points directly to the location of the base pointer.
        break :off 0;
    };

    /// Offset of the saved return address wrt the frame pointer.
    const fp_to_ra_offset = off: {
        // On 32-bit PA-RISC, the return address sits in the middle-ish of the frame marker.
        if (native_arch == .hppa) break :off -5 * @sizeOf(usize);
        // On 64-bit PA-RISC, the frame marker was shrunk significantly; now there's just the return
        // address followed by the base pointer.
        if (native_arch == .hppa64) break :off -2 * @sizeOf(usize);
        // On LoongArch and RISC-V, the frame pointer points to the top of the saved register area,
        // in which the return address is the second word.
        if (native_arch.isLoongArch() or native_arch.isRISCV()) break :off -1 * @sizeOf(usize);
        // On OpenRISC, the return address is stored below the stack parameter area.
        if (native_arch == .or1k) break :off -1 * @sizeOf(usize);
        if (native_arch.isPowerPC64()) break :off 2 * @sizeOf(usize);
        // On s390x, r14 is the link register and we need to grab it from its customary slot in the
        // register save area (ELF ABI s390x Supplement §1.2.2.2).
        if (native_arch == .s390x) break :off 14 * @sizeOf(usize);
        // On SPARC, the frame pointer points to the save area which holds 16 slots for the local
        // and incoming registers. The return address (i7) is stored in its customary save slot.
        if (native_arch.isSPARC()) break :off 15 * @sizeOf(usize);
        break :off @sizeOf(usize);
    };

    /// Value to add to the stack pointer and frame/base pointers to get the real location being
    /// pointed to. Yes, SPARC really does this.
    const stack_bias = bias: {
        if (native_arch == .sparc64) break :bias 2047;
        break :bias 0;
    };

    /// On some oddball architectures, a return address points to the call instruction rather than
    /// the instruction following it.
    const ra_call_offset = off: {
        if (native_arch.isSPARC()) break :off 0;
        break :off 1;
    };

    fn applyOffset(addr: usize, comptime off: comptime_int) ?usize {
        if (off >= 0) return math.add(usize, addr, off) catch return null;
        return math.sub(usize, addr, -off) catch return null;
    }
};

/// Some platforms use pointer authentication: the upper bits of instruction pointers contain a
/// signature. This function clears those signature bits to make the pointer directly usable.
pub inline fn stripInstructionPtrAuthCode(ptr: usize) usize {
    if (native_arch.isAARCH64()) {
        // `hint 0x07` maps to `xpaclri` (or `nop` if the hardware doesn't support it)
        // The save / restore is because `xpaclri` operates on x30 (LR)
        return asm (
            \\mov x16, x30
            \\mov x30, x15
            \\hint 0x07
            \\mov x15, x30
            \\mov x30, x16
            : [ret] "={x15}" (-> usize),
            : [ptr] "{x15}" (ptr),
            : .{ .x16 = true });
    }

    return ptr;
}

const PrintSourceAddressOptions = struct {
    address: usize,
    resolve_inline_callers: bool,
};

fn printSourceAtAddress(
    io: Io,
    text_arena: *std.heap.ArenaAllocator,
    debug_info: *SelfInfo,
    t: Io.Terminal,
    options: PrintSourceAddressOptions,
) Writer.Error!void {
    defer _ = text_arena.reset(.retain_capacity);

    // Initialize the symbol array with space for at least one element, allocating this on the stack
    // in the common case where only one element is needed
    var symbol_fallback_allocator = std.heap.stackFallback(@sizeOf(Symbol) + @alignOf(Symbol) - 1, getDebugInfoAllocator());
    const symbol_allocator = symbol_fallback_allocator.get();
    var symbols = std.ArrayList(Symbol).initCapacity(symbol_allocator, 1) catch unreachable;
    defer symbols.deinit(symbol_allocator);

    debug_info.getSymbols(
        io,
        symbol_allocator,
        text_arena.allocator(),
        options.address,
        options.resolve_inline_callers,
        &symbols,
    ) catch |err| {
        t.setColor(.dim) catch {};
        defer t.setColor(.reset) catch {};
        switch (err) {
            error.MissingDebugInfo,
            error.UnsupportedDebugInfo,
            error.InvalidDebugInfo,
            => {},
            error.ReadFailed, error.Unexpected, error.Canceled => {
                try t.writer.print("Failed to read debug info from filesystem, trace may be incomplete\n\n", .{});
            },
            error.OutOfMemory => {
                t.setColor(.dim) catch {};
                try t.writer.print("Ran out of memory loading debug info, trace may be incomplete\n\n", .{});
                t.setColor(.reset) catch {};
            },
        }
    };

    // If we failed to write any symbols, at least write the unknown symbol. Can't fail since we
    // initialized with a capacity of 1.
    if (symbols.items.len == 0) symbols.appendAssumeCapacity(.unknown);

    for (symbols.items) |symbol| {
        try printLineInfo(io, t, debug_info, options.address, symbol);
    }
}
fn printLineInfo(
    io: Io,
    t: Io.Terminal,
    debug_info: *SelfInfo,
    address: usize,
    symbol: Symbol,
) Writer.Error!void {
    const writer = t.writer;
    t.setColor(.bold) catch {};

    if (symbol.source_location) |*sl| {
        if (sl.column == 0) {
            try writer.print("{s}:{d}", .{ sl.file_name, sl.line });
        } else {
            try writer.print("{s}:{d}:{d}", .{ sl.file_name, sl.line, sl.column });
        }
    } else {
        try writer.writeAll("???:?:?");
    }

    t.setColor(.reset) catch {};
    try writer.writeAll(": ");
    t.setColor(.dim) catch {};
    try writer.print("0x{x} in {s} ({s})", .{
        address,
        symbol.name orelse "???",
        symbol.compile_unit_name orelse debug_info.getModuleName(io, address) catch "???",
    });
    t.setColor(.reset) catch {};
    try writer.writeAll("\n");

    // Show the matching source code line if possible
    if (symbol.source_location) |sl| {
        if (printLineFromFile(io, writer, sl)) {
            if (sl.column > 0) {
                // The caret already takes one char
                const space_needed = @as(usize, @intCast(sl.column - 1));

                try writer.splatByteAll(' ', space_needed);
                t.setColor(.green) catch {};
                try writer.writeAll("^");
                t.setColor(.reset) catch {};
            }
            try writer.writeAll("\n");
        } else |_| {
            // Ignore all errors; it's a better UX to just print the source location without the
            // corresponding line number. The user can always open the source file themselves.
        }
    }
}
fn printLineFromFile(io: Io, writer: *Writer, source_location: SourceLocation) !void {
    // Allow overriding the target-agnostic source line printing logic by exposing `root.debug.printLineFromFile`.
    if (@hasDecl(root, "debug") and @hasDecl(root.debug, "printLineFromFile")) {
        return root.debug.printLineFromFile(io, writer, source_location);
    }

    // Need this to always block even in async I/O mode, because this could potentially
    // be called from e.g. the event loop code crashing.
    const cwd: Io.Dir = .cwd();
    var file = try cwd.openFile(io, source_location.file_name, .{});
    defer file.close(io);

    var buffer: [4096]u8 = undefined;
    var file_reader: File.Reader = .init(file, io, &buffer);
    var line_index: usize = 0;
    const r = &file_reader.interface;
    while (true) {
        line_index += 1;
        if (line_index == source_location.line) {
            // TODO delete hard tabs from the language
            _ = try r.streamDelimiterEnding(writer, '\n');
            try writer.writeByte('\n');
            return;
        }
        _ = try r.discardDelimiterInclusive('\n');
    }
}

test printLineFromFile {
    const io = testing.io;
    const gpa = testing.allocator;

    var aw: Writer.Allocating = .init(gpa);
    defer aw.deinit();
    const output_stream = &aw.writer;

    const join = std.fs.path.join;
    const expectError = testing.expectError;
    const expectEqualStrings = testing.expectEqualStrings;

    var test_dir = testing.tmpDir(.{});
    defer test_dir.cleanup();
    // Relies on testing.tmpDir internals which is not ideal, but SourceLocation requires paths.
    const test_dir_path = try join(gpa, &.{ ".zig-cache", "tmp", test_dir.sub_path[0..] });
    defer gpa.free(test_dir_path);

    // Cases
    {
        const path = try join(gpa, &.{ test_dir_path, "one_line.zig" });
        defer gpa.free(path);
        try test_dir.dir.writeFile(io, .{ .sub_path = "one_line.zig", .data = "no new lines in this file, but one is printed anyway" });

        try expectError(error.EndOfStream, printLineFromFile(io, output_stream, .{ .file_name = path, .line = 2, .column = 0 }));

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = 1, .column = 0 });
        try expectEqualStrings("no new lines in this file, but one is printed anyway\n", aw.written());
        aw.clearRetainingCapacity();
    }
    {
        const path = try fs.path.join(gpa, &.{ test_dir_path, "three_lines.zig" });
        defer gpa.free(path);
        try test_dir.dir.writeFile(io, .{
            .sub_path = "three_lines.zig",
            .data =
            \\1
            \\2
            \\3
            ,
        });

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = 1, .column = 0 });
        try expectEqualStrings("1\n", aw.written());
        aw.clearRetainingCapacity();

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = 3, .column = 0 });
        try expectEqualStrings("3\n", aw.written());
        aw.clearRetainingCapacity();
    }
    {
        const file = try test_dir.dir.createFile(io, "line_overlaps_page_boundary.zig", .{});
        defer file.close(io);
        const path = try fs.path.join(gpa, &.{ test_dir_path, "line_overlaps_page_boundary.zig" });
        defer gpa.free(path);

        const overlap = 10;
        var buf: [16]u8 = undefined;
        var file_writer = file.writer(io, &buf);
        const writer = &file_writer.interface;
        try writer.splatByteAll('a', std.heap.page_size_min - overlap);
        try writer.writeByte('\n');
        try writer.splatByteAll('a', overlap);
        try writer.flush();

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = 2, .column = 0 });
        try expectEqualStrings(("a" ** overlap) ++ "\n", aw.written());
        aw.clearRetainingCapacity();
    }
    {
        const file = try test_dir.dir.createFile(io, "file_ends_on_page_boundary.zig", .{});
        defer file.close(io);
        const path = try fs.path.join(gpa, &.{ test_dir_path, "file_ends_on_page_boundary.zig" });
        defer gpa.free(path);

        var file_writer = file.writer(io, &.{});
        const writer = &file_writer.interface;
        try writer.splatByteAll('a', std.heap.page_size_max);

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = 1, .column = 0 });
        try expectEqualStrings(("a" ** std.heap.page_size_max) ++ "\n", aw.written());
        aw.clearRetainingCapacity();
    }
    {
        const file = try test_dir.dir.createFile(io, "very_long_first_line_spanning_multiple_pages.zig", .{});
        defer file.close(io);
        const path = try fs.path.join(gpa, &.{ test_dir_path, "very_long_first_line_spanning_multiple_pages.zig" });
        defer gpa.free(path);

        var file_writer = file.writer(io, &.{});
        const writer = &file_writer.interface;
        try writer.splatByteAll('a', 3 * std.heap.page_size_max);

        try expectError(error.EndOfStream, printLineFromFile(io, output_stream, .{ .file_name = path, .line = 2, .column = 0 }));

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = 1, .column = 0 });
        try expectEqualStrings(("a" ** (3 * std.heap.page_size_max)) ++ "\n", aw.written());
        aw.clearRetainingCapacity();

        try writer.writeAll("a\na");

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = 1, .column = 0 });
        try expectEqualStrings(("a" ** (3 * std.heap.page_size_max)) ++ "a\n", aw.written());
        aw.clearRetainingCapacity();

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = 2, .column = 0 });
        try expectEqualStrings("a\n", aw.written());
        aw.clearRetainingCapacity();
    }
    {
        const file = try test_dir.dir.createFile(io, "file_of_newlines.zig", .{});
        defer file.close(io);
        const path = try fs.path.join(gpa, &.{ test_dir_path, "file_of_newlines.zig" });
        defer gpa.free(path);

        var file_writer = file.writer(io, &.{});
        const writer = &file_writer.interface;
        const real_file_start = 3 * std.heap.page_size_min;
        try writer.splatByteAll('\n', real_file_start);
        try writer.writeAll("abc\ndef");

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = real_file_start + 1, .column = 0 });
        try expectEqualStrings("abc\n", aw.written());
        aw.clearRetainingCapacity();

        try printLineFromFile(io, output_stream, .{ .file_name = path, .line = real_file_start + 2, .column = 0 });
        try expectEqualStrings("def\n", aw.written());
        aw.clearRetainingCapacity();
    }
}

/// The returned allocator should be thread-safe if the compilation is multi-threaded, because
/// multiple threads could capture and/or print stack traces simultaneously.
pub fn getDebugInfoAllocator() Allocator {
    // Allow overriding the debug info allocator by exposing `root.debug.getDebugInfoAllocator`.
    if (@hasDecl(root, "debug") and @hasDecl(root.debug, "getDebugInfoAllocator")) {
        return root.debug.getDebugInfoAllocator();
    }
    // Otherwise, use a global arena backed by the page allocator
    const S = struct {
        var arena: std.heap.ArenaAllocator = .init(std.heap.page_allocator);
    };
    return S.arena.allocator();
}

/// Whether or not the current target can print useful debug information when a segfault occurs.
pub const have_segfault_handling_support = switch (native_os) {
    .haiku,
    .linux,
    .serenity,

    .dragonfly,
    .freebsd,
    .netbsd,
    .openbsd,

    .driverkit,
    .ios,
    .maccatalyst,
    .macos,
    .tvos,
    .visionos,
    .watchos,

    .illumos,

    .windows,
    => true,

    else => false,
};

const enable_segfault_handler = std.options.enable_segfault_handler;
pub const default_enable_segfault_handler = runtime_safety and have_segfault_handling_support;

pub fn maybeEnableSegfaultHandler() void {
    if (enable_segfault_handler) {
        attachSegfaultHandler();
    }
}

var windows_segfault_handle: ?windows.HANDLE = null;

pub fn updateSegfaultHandler(act: ?*const posix.Sigaction) void {
    posix.sigaction(.SEGV, act, null);
    posix.sigaction(.ILL, act, null);
    posix.sigaction(.BUS, act, null);
    posix.sigaction(.FPE, act, null);
}

/// Attaches a global handler for several signals which, when triggered, prints output to stderr
/// similar to the default panic handler, with a message containing the type of signal and a stack
/// trace if possible. This implementation does not just call the panic handler, because unwinding
/// the stack (for a stack trace) when a signal is received requires special target-specific logic.
///
/// On POSIX targets, the signal handler is configured to use the alternative signal stack. Such a
/// stack is configured by the Zig Standard Library if `std.options.signal_stack_size` is set.
///
/// The signals for which a handler is installed are:
/// * SIGSEGV (segmentation fault)
/// * SIGILL (illegal instruction)
/// * SIGBUS (bus error)
/// * SIGFPE (arithmetic exception)
pub fn attachSegfaultHandler() void {
    if (!have_segfault_handling_support) {
        @compileError("segfault handler not supported for this target");
    }
    if (native_os == .windows) {
        windows_segfault_handle = windows.ntdll.RtlAddVectoredExceptionHandler(0, handleSegfaultWindows);
        return;
    }
    const act: posix.Sigaction = .{
        .handler = .{ .sigaction = handleSegfaultPosix },
        .mask = posix.sigemptyset(),
        .flags = (posix.SA.SIGINFO | posix.SA.RESTART | posix.SA.RESETHAND | posix.SA.ONSTACK),
    };
    updateSegfaultHandler(&act);
}

fn resetSegfaultHandler() void {
    if (native_os == .windows) {
        if (windows_segfault_handle) |handle| {
            assert(windows.ntdll.RtlRemoveVectoredExceptionHandler(handle) != 0);
            windows_segfault_handle = null;
        }
        return;
    }
    const act = posix.Sigaction{
        .handler = .{ .handler = posix.SIG.DFL },
        .mask = posix.sigemptyset(),
        .flags = 0,
    };
    updateSegfaultHandler(&act);
}

fn handleSegfaultPosix(sig: posix.SIG, info: *const posix.siginfo_t, ctx_ptr: ?*anyopaque) callconv(.c) noreturn {
    if (use_trap_panic) @trap();
    const addr: ?usize, const name: []const u8 = info: {
        if (native_os == .linux and native_arch == .x86_64) {
            // x86_64 doesn't have a full 64-bit virtual address space.
            // Addresses outside of that address space are non-canonical
            // and the CPU won't provide the faulting address to us.
            // This happens when accessing memory addresses such as 0xaaaaaaaaaaaaaaaa
            // but can also happen when no addressable memory is involved;
            // for example when reading/writing model-specific registers
            // by executing `rdmsr` or `wrmsr` in user-space (unprivileged mode).
            const SI_KERNEL = 0x80;
            if (sig == .SEGV and info.code == SI_KERNEL) {
                break :info .{ null, "General protection exception" };
            }
        }
        const addr: usize = switch (native_os) {
            .serenity,
            .dragonfly,
            .freebsd,
            .driverkit,
            .ios,
            .maccatalyst,
            .macos,
            .tvos,
            .visionos,
            .watchos,
            => @intFromPtr(info.addr),
            .linux,
            => @intFromPtr(info.fields.sigfault.addr),
            .netbsd,
            => @intFromPtr(info.info.reason.fault.addr),
            .haiku,
            .openbsd,
            => @intFromPtr(info.data.fault.addr),
            .illumos,
            => @intFromPtr(info.reason.fault.addr),
            else => comptime unreachable,
        };
        const name = switch (sig) {
            .SEGV => "Segmentation fault",
            .ILL => "Illegal instruction",
            .BUS => "Bus error",
            .FPE => "Arithmetic exception",
            else => unreachable,
        };
        break :info .{ addr, name };
    };
    const opt_cpu_context: ?cpu_context.Native = cpu_context.fromPosixSignalContext(ctx_ptr);

    if (native_arch.isSPARC()) {
        // It's unclear to me whether this is a QEMU bug or also real kernel behavior, but in the
        // former, I observed that the most recent register window wasn't getting spilled on the
        // stack as expected when a signal arrived. A `flushw` from the signal handler does not
        // appear to be sufficient either. On the other hand, when doing a synchronous stack trace
        // and using `flushw`, this all appears to work as expected. So, *probably* a QEMU bug, but
        // someone with real SPARC hardware should verify.
        //
        // In any case, the register save area exists specifically so that register windows can be
        // spilled asynchronously. This means that it should be perfectly fine for us to manually do
        // so here.
        const ctx = opt_cpu_context.?;
        @as(*[16]usize, @ptrFromInt(ctx.o[6] + StackIterator.stack_bias)).* = ctx.l ++ ctx.i;
    }

    handleSegfault(addr, name, if (opt_cpu_context) |*ctx| ctx else null);
}

fn handleSegfaultWindows(info: *windows.EXCEPTION_POINTERS) callconv(.winapi) c_long {
    if (use_trap_panic) @trap();
    const name: []const u8, const addr: ?usize = switch (info.ExceptionRecord.ExceptionCode) {
        windows.EXCEPTION_DATATYPE_MISALIGNMENT => .{ "Unaligned memory access", null },
        windows.EXCEPTION_ACCESS_VIOLATION => .{ "Segmentation fault", info.ExceptionRecord.ExceptionInformation[1] },
        windows.EXCEPTION_ILLEGAL_INSTRUCTION => .{ "Illegal instruction", info.ContextRecord.getRegs().ip },
        windows.EXCEPTION_STACK_OVERFLOW => .{ "Stack overflow", null },
        else => return windows.EXCEPTION_CONTINUE_SEARCH,
    };
    handleSegfault(addr, name, &cpu_context.fromWindowsContext(info.ContextRecord));
}

fn handleSegfault(addr: ?usize, name: []const u8, opt_ctx: ?CpuContextPtr) noreturn {
    // Allow overriding the target-agnostic segfault handler by exposing `root.debug.handleSegfault`.
    if (@hasDecl(root, "debug") and @hasDecl(root.debug, "handleSegfault")) {
        return root.debug.handleSegfault(addr, name, opt_ctx);
    }
    return defaultHandleSegfault(addr, name, opt_ctx);
}

pub fn defaultHandleSegfault(addr: ?usize, name: []const u8, opt_ctx: ?CpuContextPtr) noreturn {
    std.Options.debug_io.vtable.crashHandler(std.Options.debug_io.userdata);

    // There is very similar logic to the following in `defaultPanic`.
    switch (panic_stage) {
        0 => {
            panic_stage = 1;
            _ = panicking.fetchAdd(1, .seq_cst);

            trace: {
                const stderr = lockStderr(&.{}).terminal();
                defer unlockStderr();

                if (addr) |a| {
                    stderr.writer.print("{s} at address 0x{x}\n", .{ name, a }) catch break :trace;
                } else {
                    stderr.writer.print("{s} (no address available)\n", .{name}) catch break :trace;
                }
                if (opt_ctx) |context| {
                    writeCurrentStackTrace(.{
                        .context = context,
                        .allow_unsafe_unwind = true, // we're crashing anyway, give it our all!
                    }, stderr) catch break :trace;
                }
            }
        },
        1 => {
            panic_stage = 2;
            // A segfault happened while trying to print a previous panic message.
            // We're still holding the mutex but that's fine as we're going to
            // call abort().
            const stderr = lockStderr(&.{}).terminal();
            stderr.writer.writeAll("aborting due to recursive panic\n") catch {};
        },
        else => {}, // Panicked while printing the recursive panic message.
    }

    // We cannot allow the signal handler to return because when it runs the original instruction
    // again, the memory may be mapped and undefined behavior would occur rather than repeating
    // the segfault. So we simply abort here.
    std.process.abort();
}

pub fn dumpStackPointerAddr(prefix: []const u8) void {
    const sp = asm (""
        : [argc] "={rsp}" (-> usize),
    );
    print("{s} sp = 0x{x}\n", .{ prefix, sp });
}

test "manage resources correctly" {
    if (SelfInfo == void) return error.SkipZigTest;
    if (builtin.zig_backend == .stage2_c) {
        // The C backend emits an extremely large C source file, meaning it has a huge
        // amount of debug information. Parsing this debug information makes this test
        // take too long to be worth running.
        return error.SkipZigTest;
    }

    const S = struct {
        noinline fn showMyTrace() usize {
            return @returnAddress();
        }
    };
    const io = testing.io;

    var discarding: Writer.Discarding = .init(&.{});
    var di: SelfInfo = .init;
    defer di.deinit(io);
    const t: Io.Terminal = .{ .writer = &discarding.writer, .mode = .no_color };
    var text_arena: std.heap.ArenaAllocator = .init(std.testing.allocator);
    defer text_arena.deinit();
    try printSourceAtAddress(io, &text_arena, &di, t, .{
        .address = S.showMyTrace(),
        .resolve_inline_callers = true,
    });
}

/// This API helps you track where a value originated and where it was mutated,
/// or any other points of interest.
/// In debug mode, it adds a small size penalty (104 bytes on 64-bit architectures)
/// to the aggregate that you add it to.
/// In release mode, it is size 0 and all methods are no-ops.
/// This is a pre-made type with default settings.
/// For more advanced usage, see `ConfigurableTrace`.
pub const Trace = ConfigurableTrace(2, 4, builtin.mode == .Debug);

pub fn ConfigurableTrace(comptime size: usize, comptime stack_frame_count: usize, comptime is_enabled: bool) type {
    return struct {
        addrs: [actual_size][stack_frame_count]usize,
        notes: [actual_size][]const u8,
        index: Index,

        const actual_size = if (enabled) size else 0;
        const Index = if (enabled) usize else u0;

        pub const init: @This() = .{
            .addrs = undefined,
            .notes = undefined,
            .index = 0,
        };

        pub const enabled = is_enabled;

        pub const add = if (enabled) addNoInline else addNoOp;

        pub noinline fn addNoInline(t: *@This(), note: []const u8) void {
            comptime assert(enabled);
            return addAddr(t, @returnAddress(), note);
        }

        pub inline fn addNoOp(t: *@This(), note: []const u8) void {
            _ = t;
            _ = note;
            comptime assert(!enabled);
        }

        pub fn addAddr(t: *@This(), addr: usize, note: []const u8) void {
            if (!enabled) return;

            if (t.index < size) {
                t.notes[t.index] = note;
                const addrs = &t.addrs[t.index];
                const st = captureCurrentStackTrace(.{ .first_address = addr }, addrs);
                if (st.return_addresses.len < addrs.len) {
                    @memset(addrs[st.return_addresses.len..], 0); // zero unused frames to indicate end of trace
                }
            }
            // Keep counting even if the end is reached so that the
            // user can find out how much more size they need.
            t.index += 1;
        }

        pub fn dump(t: @This()) void {
            if (!enabled) return;

            const stderr = lockStderr(&.{}).terminal();
            defer unlockStderr();
            const end = @min(t.index, size);
            for (t.addrs[0..end], 0..) |frames_array, i| {
                stderr.writer.print("{s}:\n", .{t.notes[i]}) catch return;
                var frames_array_mutable = frames_array;
                const frames = mem.sliceTo(frames_array_mutable[0..], 0);
                const len = @min(t.index, frames.len);
                const stack_trace: StackTrace = .{
                    .return_addresses = frames[0..len],
                    .skipped = if (len < frames.len) .none else .unknown,
                };
                writeStackTrace(&stack_trace, stderr) catch return;
            }
            if (t.index > end) {
                stderr.writer.print("{d} more traces not shown; consider increasing trace size\n", .{
                    t.index - end,
                }) catch return;
            }
        }

        pub fn format(
            t: @This(),
            comptime fmt: []const u8,
            options: std.fmt.Options,
            writer: *Writer,
        ) !void {
            if (fmt.len != 0) std.fmt.invalidFmtError(fmt, t);
            _ = options;
            if (enabled) {
                try writer.writeAll("\n");
                t.dump();
                try writer.writeAll("\n");
            } else {
                return writer.writeAll("(value tracing disabled)");
            }
        }
    };
}

pub const SafetyLock = struct {
    state: State = if (runtime_safety) .unlocked else .unknown,

    pub const State = if (runtime_safety) enum { unlocked, locked } else enum { unknown };

    pub fn lock(l: *SafetyLock) void {
        if (!runtime_safety) return;
        assert(l.state == .unlocked);
        l.state = .locked;
    }

    pub fn unlock(l: *SafetyLock) void {
        if (!runtime_safety) return;
        assert(l.state == .locked);
        l.state = .unlocked;
    }

    pub fn assertUnlocked(l: SafetyLock) void {
        if (!runtime_safety) return;
        assert(l.state == .unlocked);
    }

    pub fn assertLocked(l: SafetyLock) void {
        if (!runtime_safety) return;
        assert(l.state == .locked);
    }
};

test SafetyLock {
    var safety_lock: SafetyLock = .{};
    safety_lock.assertUnlocked();
    safety_lock.lock();
    safety_lock.assertLocked();
    safety_lock.unlock();
    safety_lock.assertUnlocked();
}

/// Detect whether the program is being executed in the Valgrind virtual machine.
///
/// When Valgrind integrations are disabled, this returns comptime-known false.
/// Otherwise, the result is runtime-known.
pub inline fn inValgrind() bool {
    if (@inComptime()) return false;
    if (!builtin.valgrind_support) return false;
    return std.valgrind.runningOnValgrind() > 0;
}

test {
    _ = &Dwarf;
    _ = &Pdb;
    _ = &SelfInfo;
    _ = &dumpHex;
}



---
File: /std/deque.zig
---

const std = @import("std");
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;

/// A contiguous, growable, double-ended queue.
///
/// Pushing/popping items from either end of the queue is O(1).
pub fn Deque(comptime T: type) type {
    return struct {
        const Self = @This();

        /// A ring buffer.
        buffer: []T,
        /// The index in buffer where the first item in the logical deque is stored.
        head: usize,
        /// The number of items stored in the logical deque.
        len: usize,

        /// A Deque containing no elements.
        pub const empty: Self = .{
            .buffer = &.{},
            .head = 0,
            .len = 0,
        };

        /// Initialize with capacity to hold `capacity` elements.
        /// The resulting capacity will equal `capacity` exactly.
        /// Deinitialize with `deinit`.
        pub fn initCapacity(gpa: Allocator, capacity: usize) Allocator.Error!Self {
            var deque: Self = .empty;
            try deque.ensureTotalCapacityPrecise(gpa, capacity);
            return deque;
        }

        /// Initialize with externally-managed memory. The buffer determines the
        /// capacity and the deque is initially empty.
        ///
        /// When initialized this way, all functions that accept an Allocator
        /// argument cause illegal behavior.
        pub fn initBuffer(buffer: []T) Self {
            return .{
                .buffer = buffer,
                .head = 0,
                .len = 0,
            };
        }

        /// Release all allocated memory.
        pub fn deinit(deque: *Self, gpa: Allocator) void {
            gpa.free(deque.buffer);
            deque.* = undefined;
        }

        /// Modify the deque so that it can hold at least `new_capacity` items.
        /// Implements super-linear growth to achieve amortized O(1) push/pop operations.
        /// Invalidates element pointers if additional memory is needed.
        pub fn ensureTotalCapacity(deque: *Self, gpa: Allocator, new_capacity: usize) Allocator.Error!void {
            if (deque.buffer.len >= new_capacity) return;
            return deque.ensureTotalCapacityPrecise(gpa, std.ArrayList(T).growCapacity(new_capacity));
        }

        /// If the current capacity is less than `new_capacity`, this function will
        /// modify the deque so that it can hold exactly `new_capacity` items.
        /// Invalidates element pointers if additional memory is needed.
        pub fn ensureTotalCapacityPrecise(deque: *Self, gpa: Allocator, new_capacity: usize) Allocator.Error!void {
            if (deque.buffer.len >= new_capacity) return;
            const old_buffer = deque.buffer;
            if (gpa.remap(old_buffer, new_capacity)) |new_buffer| {
                // If the items wrap around the end of the buffer we need to do
                // a memcpy to prevent a gap after resizing the buffer.
                if (deque.head > old_buffer.len - deque.len) {
                    // The gap splits the items in the deque into head and tail parts.
       
```
