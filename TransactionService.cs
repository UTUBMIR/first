namespace First {
    public class TransactionService {

        public Transaction CreateTransaction(string from, string to, decimal amount) {
            var transaction = new Transaction(from, to, amount);

            if (ValidateTransaction(transaction) is (false, var errorMessage)) {
                throw new ArgumentException(errorMessage);
            }

            return transaction;
        }

        public (bool isValid, string errorMessage) ValidateTransaction(Transaction transaction) {
            if (string.IsNullOrEmpty(transaction.From)) {
                return (false, "Empty sender address");
            }

            if (string.IsNullOrEmpty(transaction.To)) {
                return (false, "Empty recipient address");
            }

            if (transaction.Amount <= 0) {
                return (false, "Invalid transaction amount");
            }

            return (true, string.Empty);
        }
    }
}
