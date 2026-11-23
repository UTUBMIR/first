using System.Buffers.Text;
using System.Diagnostics;
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
            var merkleRoot = ComputeMerkleRoot(block.Transactions);

            var prefix = string.Create(
                CultureInfo.InvariantCulture,
                $"{block.Index}{block.TimeStamp.ToBinary()}{merkleRoot}{block.DifficultyAtMining}{block.PreviousHash}");
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

        private static string ComputeStringHash(string data) {
            return Convert.ToHexStringLower(Sha.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }

        private static List<string> ComputeNextMerkleLevel(List<string> hashes) {
            var nextLevel = new List<string>();

            for (int i = 0; i < hashes.Count; i += 2) {
                if (i + 1 < hashes.Count) {
                    nextLevel.Add(ComputeStringHash(hashes[i] + hashes[i + 1]));
                }
                else {
                    // odd node count- move the last hash to the next level unchanged
                    nextLevel.Add(hashes[i]);
                }
            }

            return nextLevel;
        }

        public static string ComputeMerkleRoot(List<Transaction> transactions) {
            if (transactions.Count == 0) {
                return string.Empty;
            }

            var hashes = transactions.ConvertAll(t => ComputeStringHash(t.ToRawString()));

            while (hashes.Count > 1) {
                hashes = ComputeNextMerkleLevel(hashes);
            }

            Debug.Assert(hashes.Count == 1);
            return hashes[0];
        }

        public static MerkleProof? GetMerkleProof(List<Transaction> transactions, string transactionId) {
            int position = transactions.FindIndex(t => t.Id == transactionId);
            if (position < 0) {
                return null;
            }

            var hashes = transactions.ConvertAll(t => ComputeStringHash(t.ToRawString()));
            var steps = new List<MerkleProofStep>();

            while (hashes.Count > 1) {
                // left index of the pair that contains 'position'
                int pairStart = position & ~1; // remove first "odd" bit

                if (pairStart + 1 < hashes.Count) {
                    bool siblingIsLeft = position != pairStart;
                    int siblingIndex = siblingIsLeft ? pairStart : pairStart + 1;

                    // the side is required because hash concatenation is order-sensitive
                    steps.Add(new MerkleProofStep(hashes[siblingIndex], siblingIsLeft));
                }

                hashes = ComputeNextMerkleLevel(hashes);
                position /= 2;
            }

            return new MerkleProof(transactionId, hashes[0], steps.AsReadOnly());
        }

        public static bool VerifyMerkleProof(Transaction transaction, MerkleProof proof, string expectedMerkleRoot) {
            if (transaction.Id != proof.TransactionId) {
                return false;
            }

            // hash the transaction data itself, so any changed field invalidates the proof
            var current = ComputeStringHash(transaction.ToRawString());

            foreach (var step in proof.Steps) {
                current = step.SiblingIsLeft
                    ? ComputeStringHash(step.SiblingHash + current)
                    : ComputeStringHash(current + step.SiblingHash);
            }

            return string.Equals(current, expectedMerkleRoot, StringComparison.Ordinal);
        }

        public static bool VerifyMerkleProof(Transaction transaction, MerkleProof proof) {
            return VerifyMerkleProof(transaction, proof, proof.MerkleRoot);
        }

        public static string ComputeHashHex(Block block) {
            Span<byte> digest = stackalloc byte[32];
            ComputeDigest(EncodePrefix(block), block.Nonce, digest);
            return Convert.ToHexStringLower(digest);
        }
    }
}
