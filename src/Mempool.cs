namespace First {
    public class Mempool {
        public enum ValidationError {
            InvalidTransaction,
            DuplicateTransaction,
        }
        private readonly List<Transaction> _pending = new List<Transaction>();

        private readonly TransactionService _transactionService = new TransactionService();

        public int Count => _pending.Count;

        public IReadOnlyList<Transaction> PendingTransactions => _pending.AsReadOnly();

        public int MaxOutcomingPerWallet { get; private set; } = 3;

        public Failable<ValidationError> AddTransaction(Transaction transaction) {
            if (_transactionService.ValidateTransaction(transaction).IsFailure) {
                return ValidationError.InvalidTransaction;
            }

            if (_pending.Any(t => t.Id == transaction.Id)) {
                return ValidationError.DuplicateTransaction;
            }

            _pending.Add(transaction);
            return Result.Success();
        }

        public Failable<(int index, ValidationError reason)> AddTransactions(IEnumerable<Transaction> transactions) {
            foreach (var (i, t) in transactions.Index()) {
                if (!AddTransaction(t).Verify(out var reason)) {
                    return (i, reason);
                }
            }
            return Result.Success();
        }

        public List<Transaction> Peek(int maxCount) {
            return _pending.Take(maxCount).ToList();
        }

        public void RemoveTransactions(List<Transaction> transactions) {
            var ids = transactions.Select(t => t.Id).ToHashSet();
            _pending.RemoveAll(t => ids.Contains(t.Id));
        }
    }
}
