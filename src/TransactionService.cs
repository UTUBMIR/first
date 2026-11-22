namespace First {
    public class TransactionService {
        public enum TransactionValidationError {
            EmptySender,
            EmptyRecipient,
            InvalidAmount,
            DuplicateId,
            EmptySignatureOrSenderPublicKey,
            NonMatchingSignature,
            InsufficientBalance
        }

        private readonly WalletService _walletService = new WalletService();

        public Transaction CreateTransaction(Wallet sender, string to, decimal amount) {
            var transaction = new Transaction(sender.Address, to, amount);
            transaction.SenderPublicKey = sender.PublicKey;
            transaction.Singature = sender.Sign(transaction.GetDataToSign());

            if (!ValidateTransaction(transaction).Verify(out var error)) {
                throw new ArgumentException(error.ToString());
            }

            return transaction;
        }

        public Failable<TransactionValidationError> ValidateTransaction(Transaction transaction) {
            if (string.IsNullOrEmpty(transaction.To)) {
                return TransactionValidationError.EmptyRecipient;
            }

            if (transaction.Amount <= 0) {
                return TransactionValidationError.InvalidAmount;
            }

            if (transaction.IsCoinbase) {
                return Result.Success();
            }

            if (string.IsNullOrEmpty(transaction.From)) {
                return TransactionValidationError.EmptySender;
            }

            if (transaction.Singature == null || transaction.SenderPublicKey == null) {
                return TransactionValidationError.EmptySignatureOrSenderPublicKey;
            }

            if (!_walletService.VerifySignature(transaction.GetDataToSign(), transaction.Singature, transaction.SenderPublicKey)) {
                return TransactionValidationError.NonMatchingSignature;
            }

            return Result.Success();
        }
    }
}
