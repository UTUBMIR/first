using System.Diagnostics;
using static First.TransactionService;

namespace First {
    public enum BlockValidationError {
        Hash,
        PreviousHash,
        DifficultyAtMining
    }


    public class BlockChain {
        public int Difficulty { get; private set; }
        public List<Block> Chain { get; set; }
        private readonly HashingService _hashingService;
        private readonly BalanceService _balanceService;
        private readonly MiningService _miningService;
        private readonly TransactionService _transactionService;
        public Mempool Mempool { get; private set; } = new Mempool();
        public int MaxTransactionsPerBock { get; private set; } = 3;

        public readonly double _targetBlockTime = 3;
        public readonly int _adjustmentInterval = 2;

        public BlockChain(int difficulty = 4, bool singleThreaded = false, bool useGpu = true) {
            Difficulty = difficulty;
            Chain = new List<Block>();
            _hashingService = new HashingService();
            _miningService = new MiningService(singleThreaded, useGpu);
            _balanceService = new BalanceService();
            _transactionService = new TransactionService();
            AddGenesisBlock();
        }

        private void AddGenesisBlock() {
            Debug.Assert(Chain.Count == 0, "Non empty chain");

            var genesisBlock = new Block {
                Index = 0,
                TimeStamp = DateTime.UtcNow,
                Transactions = [],
                PreviousHash = "0"
            };

            _miningService.MineBlock(genesisBlock, 1);
            Chain.Add(genesisBlock);
        }

        private bool DuplicateTransactionId(Transaction transaction) {
            foreach (var block in Chain) { // WARNING: Use a parallel loop if needed in the future
                foreach (var t in block.Transactions) {
                    if (t.Id == transaction.Id) {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TransactionOrItsIdExists(Transaction transaction, int transactionLocalIndex, List<Transaction> localTransactions) {
            if (DuplicateTransactionId(transaction)) return true;

            foreach (var (i, t) in localTransactions.Index()) {
                if (transactionLocalIndex == i) continue;

                if (t.Id == transaction.Id) {
                    return true;
                }
            }
            return false;
        }

        public Failable<string> AddTransaction(Transaction transaction) {
            if (DuplicateTransactionId(transaction)) {
                return TransactionValidationError.DuplicateId.ToString();
            }

            if (!Mempool.AddTransaction(transaction).Verify(out var reason)) {
                return reason.ToString();
            }

            return Result.Success();
        }

        public Failable<(int index, string reason)> AddTransactions(IEnumerable<Transaction> transactions) {
            foreach (var (i, t) in transactions.Index()) {
                if (!AddTransaction(t).Verify(out var reason)) {
                    return (i, reason);
                }
            }
            return Result.Success();
        }

        public Failable<(int transactionIndex, TransactionValidationError reason)> MinePending(string minerAddress) {
            var batch = Mempool.Peek(MaxTransactionsPerBock);

            foreach (var (index, transaction) in batch.Index()) {
                var valid = _transactionService.ValidateTransaction(transaction);

                if (valid.IsFailure) {
                    Extensions.WriteError($"Invalid transaction: {valid.Error}");
                    return (index, valid.Error);
                }

                if (_balanceService.HasSufficientBalance(transaction.From, transaction.Amount, Chain, batch.Where(t => t != transaction).ToList()) == false) {
                    Extensions.WriteError($"Insufficient balance for transaction: \"{transaction}\"");
                    return (index, TransactionValidationError.InsufficientBalance);
                }

                if (TransactionOrItsIdExists(transaction, index, batch)) { // TODO: Combine verifications for all transactions in the block into a single loop
                    Extensions.WriteError($"Transaction with the same id already exists: \"{transaction.Id}\"");
                    return (index, TransactionValidationError.DuplicateId);
                }
            }

            var coinbaseTransaction = Transaction.CreateCoinbaseTransaction(minerAddress, 50);
            batch.Insert(0, coinbaseTransaction);

            var lastBlock = Chain[Chain.Count - 1];

            var newBlock = new Block {
                Index = lastBlock.Index + 1,
                TimeStamp = DateTime.UtcNow,
                Transactions = batch,
                PreviousHash = lastBlock.Hash
            };

            _miningService.MineBlock(newBlock, Difficulty);
            Chain.Add(newBlock);
            Mempool.RemoveTransactions(batch);

            if (newBlock.Index % _adjustmentInterval == 0) {
                AdjustDifficulty();
            }

            return Result.Success();
        }

        public Block? GetBlockByIndex(int index) { // FIXME: Why int32?
            if (index < 0 || index >= Chain.Count) {
                return null;
            }
            return Chain[index];
        }

        // public Block? FindBlockByData(string data) {
        //     return Chain.Find(b => b.Data.Contains(data, StringComparison.OrdinalIgnoreCase));
        // }

        public bool IsChainValid() {
            return FindFirstInvalidBlock().IsSuccess;
        }

        public Failable<(int blockIndex, BlockValidationError reason)> FindFirstInvalidBlock() {
            for (int i = 1; i < Chain.Count; ++i) {
                var currentBlock = Chain[i];
                var previousBlock = Chain[i - 1];

                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock)) {
                    return (i, BlockValidationError.Hash);
                }

                if (currentBlock.PreviousHash != previousBlock.Hash) {
                    return (i, BlockValidationError.PreviousHash);
                }

                if (!currentBlock.Hash.StartsWith(new string('0', currentBlock.DifficultyAtMining))) {
                    return (i, BlockValidationError.DifficultyAtMining);
                }
            }
            return Result.Success();
        }

        public int RepairChain(int startIndex) {
            // Debug.Assert(IsChainValid() == false);
            int reMinedBlocks = 0;
            for (int i = startIndex + 1; i < Chain.Count; ++i) {
                var currentBlock = Chain[i];
                var previousBlock = Chain[i - 1];


                if (previousBlock.Hash != _hashingService.ComputeHash(previousBlock) || !previousBlock.Hash.StartsWith(new string('0', previousBlock.DifficultyAtMining))) {
                    previousBlock.Nonce = 0;
                    _miningService.MineBlock(previousBlock, previousBlock.DifficultyAtMining);
                    ++reMinedBlocks;
                }


                if (currentBlock.PreviousHash != previousBlock.Hash) {
                    currentBlock.PreviousHash = previousBlock.Hash;
                    currentBlock.Nonce = 0;
                    _miningService.MineBlock(currentBlock, currentBlock.DifficultyAtMining);
                    ++reMinedBlocks;
                }
            }
            return reMinedBlocks;
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
