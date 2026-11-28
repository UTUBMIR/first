using static First.TransactionService;

namespace First {
    public readonly record struct VerificationReport(int Total, int Accepted, int Rejected);

    public sealed class TransactionVerificationService {
        private readonly TransactionService _transactionService = new();

        public int TotalChecked { get; private set; }
        public int Accepted { get; private set; }
        public int Rejected { get; private set; }

        public Failable<TransactionValidationError> Verify(Transaction transaction) {
            var result = _transactionService.ValidateTransaction(transaction);

            ++TotalChecked;
            if (result.IsSuccess) {
                ++Accepted;
            }
            else {
                ++Rejected;
            }

            return result;
        }

        public VerificationReport GetReport() => new(TotalChecked, Accepted, Rejected);

        public void ResetReport() {
            TotalChecked = 0;
            Accepted = 0;
            Rejected = 0;
        }
    }
}
