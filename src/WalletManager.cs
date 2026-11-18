namespace First {
    public class WalletManager {
        private readonly WalletService _walletService;
        public enum WalletCreateError {
            DuplicateWalletName
        }

        public List<Wallet> Wallets { get; set; }

        public WalletManager() {
            Wallets = [];
            _walletService = new WalletService();
        }
        public Result<Wallet, WalletCreateError> CreateWallet(string name) {
            if (Wallets.Any(w => w.Name == name)) {
                return WalletCreateError.DuplicateWalletName;
            }

            var wallet = _walletService.CreateWallet(name);
            Wallets.Add(wallet);

            return wallet;
        }
    }
}
