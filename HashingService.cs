using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace First {
    public class HashingService {
        [ThreadStatic] private static SHA256? _sha;
        [ThreadStatic] private static byte[]? _buffer;
        private static SHA256 Sha => _sha ??= SHA256.Create();

        public string ComputeHash(Block block) {
            var blockData = $"{block.Index}{block.TimeStamp.ToBinary()}{block.Data}{block.Author}{block.PreviousHash}{block.Nonce}";
            return ComputeHash(blockData);
        }

        public byte[] ComputeHashBytes(Block block) {
            var blockData = $"{block.Index}{block.TimeStamp.ToBinary()}{block.Data}{block.Author}{block.PreviousHash}{block.Nonce}";
            return ComputeHashBytes(blockData);
        }

        public byte[] ComputeHashBytes(string input) {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = Sha.ComputeHash(bytes);
            return hashBytes;
        }

        public string ComputeHash(string input) {
            return Convert.ToHexStringLower(ComputeHashBytes(input));
        }

        // once per block
        public byte[] EncodePrefix(Block block) {
            var prefix = $"{block.Index}{block.TimeStamp.ToBinary()}{block.Data}{block.Author}{block.PreviousHash}";
            return Encoding.UTF8.GetBytes(prefix);
        }

        // per nonce, allocation-free after the first call on each thread
        public void ComputeHash(ReadOnlySpan<byte> prefix, ulong nonce, Span<byte> digest) {
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
    }
}
