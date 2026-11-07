using System.Diagnostics;

namespace First {
    public class BlockChain {
        public List<Block> Chain { get; set; }
        private readonly HashingService _hashingService;

        public BlockChain() {
            Chain = new List<Block>();
            _hashingService = new HashingService();
            AddGenesisBlock();
        }

        private void AddGenesisBlock() {
            Debug.Assert(Chain.Count == 0, "Non empty chain");

            var genesisBlock = new Block {
                Index = 0,
                TimeStamp = DateTime.UtcNow,
                Data = "",
                Author = "",
                PreviousHash = "0"
            };

            genesisBlock.Hash = _hashingService.ComputeHash(genesisBlock);
            Chain.Add(genesisBlock);
        }

        public void AddBlock(string author, string data) {
            var lastBlock = Chain[Chain.Count - 1];

            var newBlock = new Block {
                Index = lastBlock.Index + 1,
                TimeStamp = DateTime.UtcNow,
                Data = data,
                Author = author,
                PreviousHash = lastBlock.Hash
            };

            newBlock.Hash = _hashingService.ComputeHash(newBlock);
            Chain.Add(newBlock);
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
            }
            return true;
        }
    }
}
