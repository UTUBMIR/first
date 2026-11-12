using System.Diagnostics;

namespace First {
    public class BlockChain {
        public int Difficulty { get; private set; }
        public List<Block> Chain { get; set; }
        private readonly HashingService _hashingService;
        private readonly MiningService _miningService;

        public readonly double _targetBlockTime = 3;
        public readonly int _adjustmentInterval = 2;

        public BlockChain(int difficulty = 4, bool singleThreaded = false, bool syntheticGenesisBlock = false) {
            Difficulty = difficulty;
            Chain = new List<Block>();
            _hashingService = new HashingService();
            _miningService = new MiningService(singleThreaded);
            AddGenesisBlock(syntheticGenesisBlock);
        }

        private void AddGenesisBlock(bool synthetic) {
            Debug.Assert(Chain.Count == 0, "Non empty chain");

            var genesisBlock = new Block {
                Index = 0,
                TimeStamp = synthetic ? DateTime.FromBinary(0) : DateTime.UtcNow,
                Data = "",
                Author = "",
                PreviousHash = "0"
            };

            _miningService.MineBlock(genesisBlock, 1);
            Chain.Add(genesisBlock);
        }

        public void AddBlock(string author, string data, DateTime? time = null) {
            var lastBlock = Chain[Chain.Count - 1];

            var newBlock = new Block {
                Index = lastBlock.Index + 1,
                TimeStamp = time ?? DateTime.UtcNow,
                Data = data,
                Author = author,
                PreviousHash = lastBlock.Hash
            };

            _miningService.MineBlock(newBlock, Difficulty);
            Chain.Add(newBlock);

            if (newBlock.Index % _adjustmentInterval == 0) {
                AdjustDifficulty();
            }
        }

        public Block? GetBlockByIndex(int index) { // FIXME: Why int32?
            if (index < 0 || index >= Chain.Count) {
                return null;
            }
            return Chain[index];
        }

        public Block? FindBlockByData(string data) {
            return Chain.Find(b => b.Data.Contains(data, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsChainValid() {
            for (int i = 1; i < Chain.Count; i++) {
                var currentBlock = Chain[i];
                var previousBlock = Chain[i - 1];

                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock)) {
                    return false;
                }

                if (currentBlock.PreviousHash != previousBlock.Hash) {
                    return false;
                }

                if (!currentBlock.Hash.StartsWith(new string('0', currentBlock.DifficultyAtMining))) {
                    return false;
                }


            }
            return true;
        }

        private void AdjustDifficulty() {
            var recent = Chain.Where(b => b.Index > 0).TakeLast(_adjustmentInterval).ToList();
            if (recent.Count == 0) return;

            var avgTime = recent.Average(b => b.MiningDuration);

            if (avgTime < _targetBlockTime) {
                ++Difficulty;
                Console.WriteLine($"Difficulty increased to {Difficulty}");
            }
            else if (avgTime > _targetBlockTime) {
                Difficulty = Math.Max(1, Difficulty - 1);
                Console.WriteLine($"Difficulty decreased to {Difficulty}");
            }
        }
    }
}
