using System.Runtime.CompilerServices;

namespace First {
    public class BlockHasher {
        private Block? _snapshot;
        private byte[]? _prefix;
        private readonly byte[] _digest = new byte[32];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ComputeHashBinary(Block block) {
            if (_snapshot is null || !block.DataEquals(_snapshot)) {
                _snapshot = block.Clone();
                _prefix = BlockHashing.EncodePrefix(block);
            }

            BlockHashing.ComputeDigest(_prefix, block.Nonce, _digest);
            return _digest;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ComputeHash(Block block) {
            return Convert.ToHexStringLower(ComputeHashBinary(block));
        }
    }
}
