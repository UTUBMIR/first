using System.Runtime.CompilerServices;

namespace First {
    public class BlockHasher {
        private Block? _snapshot;
        private byte[]? _prefix;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ComputeHashBinary(Block block) {
            if (_snapshot is null || !block.DataEquals(_snapshot)) {
                _snapshot = block.Clone();
                _prefix = BlockHashing.EncodePrefix(block);
            }

            Span<byte> digest = stackalloc byte[32];
            BlockHashing.ComputeDigest(_prefix!, block.Nonce, digest);
            return digest.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ComputeHash(Block block) {
            return Convert.ToHexStringLower(ComputeHashBinary(block));
        }
    }
}