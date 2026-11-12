using System.Buffers.Binary;
using System.Numerics;

namespace First.GpuMining {
    public readonly struct DifficultyTarget {
        public const int WordCount = 8;

        private readonly uint[] _words;

        private DifficultyTarget(uint[] words) {
            _words = words;
        }

        public uint[] Words => _words;

        public static DifficultyTarget FromLeadingZeroNibbles(int nibbles) {
            if (nibbles <= 0) {
                var all = new uint[WordCount];
                Array.Fill(all, uint.MaxValue);
                return new DifficultyTarget(all);
            }

            if (nibbles >= 64) {
                return new DifficultyTarget(new uint[WordCount]);
            }

            var value = (BigInteger.One << (256 - nibbles * 4)) - 1;
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);

            var padded = new byte[32];
            Buffer.BlockCopy(bytes, 0, padded, padded.Length - bytes.Length, bytes.Length);

            var words = new uint[WordCount];
            for (int i = 0; i < WordCount; ++i)
                words[i] = BinaryPrimitives.ReadUInt32BigEndian(padded.AsSpan(i * 4, 4));

            return new DifficultyTarget(words);
        }

        public bool Meets(ReadOnlySpan<byte> hash32) {
            if (hash32.Length < 32) {
                throw new ArgumentException("Hash must be at least 32 bytes.", nameof(hash32));
            }

            for (int i = 0; i < WordCount; ++i) {
                uint hw = BinaryPrimitives.ReadUInt32BigEndian(hash32.Slice(i * 4, 4));
                if (hw < _words[i]) return true;
                if (hw > _words[i]) return false;
            }
            return true;
        }
    }
}
