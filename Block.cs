using System.Runtime.CompilerServices;

namespace First {
    public class Block : ICloneable {
        public int Index { get; set; }
        public DateTime TimeStamp { get; set; }
        public required List<Transaction> Transactions { get; set; }
        public string PreviousHash { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public UInt64 Nonce { get; set; }
        public double MiningDuration { get; set; }
        public int DifficultyAtMining { get; set; }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Block Clone() {
            return new Block {
                Index = Index,
                TimeStamp = TimeStamp,
                Transactions = Transactions,
                PreviousHash = PreviousHash,
                Hash = Hash,
                Nonce = Nonce,
                MiningDuration = MiningDuration,
                DifficultyAtMining = DifficultyAtMining
            };
        }

        object ICloneable.Clone() => Clone();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DataEquals(Block? other) {
            if (other is null) return false;
            if (Index != other.Index) return false;
            if (TimeStamp != other.TimeStamp) return false;
            if (Transactions.SequenceEqual(other.Transactions)) return false;
            if (PreviousHash != other.PreviousHash) return false;
            if (DifficultyAtMining != other.DifficultyAtMining) return false;

            return true;
        }
    }
}
