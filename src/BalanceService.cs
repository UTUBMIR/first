namespace First {
    public class BalanceService {
        public decimal GetBalance(string address, List<Block> chain) {
            decimal balance = 0;

            foreach (var block in chain) {
                foreach (var transaction in block.Transactions) {
                    if (transaction.To == address) {
                        balance += transaction.Amount;
                    }

                    if (transaction.From == address) {
                        balance -= transaction.Amount;
                    }
                }
            }

            return balance;
        }

        public bool HasSufficientBalance(string address, decimal amount, List<Block> chain, List<Transaction> transactions) {
            var balance = GetBalance(address, chain);

            foreach (var transaction in transactions) {
                if (transaction.From == address) {
                    balance -= transaction.Amount;
                }
            }

            return balance >= amount;
        }
    }
}
