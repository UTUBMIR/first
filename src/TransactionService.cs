namespace First {
    public class TransactionService {
        public enum TransactionInvalidityReason {
            EmptySender,
            EmptyRecipient,
            InvalidAmount,
            DuplicateId
        }

        public Transaction CreateTransaction(string from, string to, decimal amount) {
            var transaction = new Transaction(from, to, amount);

            if (ValidateTransaction(transaction).Verify(out var error)) {
                return transaction;
            }
            else {
                throw new ArgumentException(error.ToString());
            }

        }

        public Failable<TransactionInvalidityReason> ValidateTransaction(Transaction transaction) {
            if (string.IsNullOrEmpty(transaction.From)) {
                return TransactionInvalidityReason.EmptySender;
            }

            if (string.IsNullOrEmpty(transaction.To)) {
                return TransactionInvalidityReason.EmptyRecipient;
            }

            if (transaction.Amount <= 0) {
                return TransactionInvalidityReason.InvalidAmount;
            }

            return Result.Success();
        }
    }
}
