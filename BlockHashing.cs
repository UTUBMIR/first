using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace First {
    internal static class BlockHashing {
        [ThreadStatic] private static SHA256? _sha;
        [ThreadStatic] private static byte[]? _buffer;

        private static SHA256 Sha => _sha ??= SHA256.Create();

        // once per block
        public static byte[] EncodePrefix(Block block) {
            var prefix = string.Create(
                CultureInfo.InvariantCulture,
                $"{block.Index}{block.TimeStamp.ToBinary()}{block.Data}{block.Author}{block.DifficultyAtMining}{block.PreviousHash}");
            return Encoding.UTF8.GetBytes(prefix);
        }

        // per nonce, allocation-free after the first call on each thread
        public static void ComputeDigest(ReadOnlySpan<byte> prefix, ulong nonce, Span<byte> digest) {
            int required = prefix.Length + 20; // max ulong digits
            var buffer = _buffer;
            if (buffer is null || buffer.Length < required) {
                buffer = new byte[required];
                _buffer = buffer;
            }

            prefix.CopyTo(buffer);
            Utf8Formatter.TryFormat(nonce, buffer.AsSpan(prefix.Length), out int written);
            Sha.TryComputeHash(buffer.AsSpan(0, prefix.Length + written), digest, out _);
        }

        public static string ComputeHashHex(Block block) {
            Span<byte> digest = stackalloc byte[32];
            ComputeDigest(EncodePrefix(block), block.Nonce, digest);
            return Convert.ToHexStringLower(digest);
        }
    }
}