using System.Diagnostics;
using static First.TransactionService;

namespace First {
    public sealed class MenuActions {
        private readonly BlockChain _blockChain;
        private readonly BlockChainDisplayService _displayService;
        private readonly WalletManager _walletManager;
        private readonly TransactionService _transactionService = new();
        private readonly TransactionVerificationService _verificationService = new();

        public MenuActions(BlockChain blockChain, BlockChainDisplayService displayService, WalletManager walletManager) {
            _blockChain = blockChain;
            _displayService = displayService;
            _walletManager = walletManager;
        }

        public bool GetBlockByIndex() {
            Console.Clear();
            int maxIndex = _blockChain.Chain.Count - 1;
            int index = Extensions.GetNumber(
                $"Enter block index (0-{maxIndex}): ",
                "\aError: {0} is out of range!",
                [0, maxIndex]);

            Console.Clear();
            var block = _blockChain.GetBlockByIndex(index);
            if (block is null) {
                Extensions.WriteError($"Block with index {index} was not found.");
            }
            else {
                _displayService.DisplayBlock(block);
            }

            Extensions.Pause();
            return true;
        }

        public static void WriteHighlighted(string text, string search) {
            var index = 0;
            var lastIndex = 0;

            while ((index = text.IndexOf(search, lastIndex, StringComparison.OrdinalIgnoreCase)) != -1) {
                Console.Write(text[lastIndex..index]);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(text.Substring(index, search.Length));
                Console.ResetColor();

                lastIndex = index + search.Length;
            }

            Console.WriteLine(text[lastIndex..]);
        }

        public bool SearchTransactionById() {
            Console.Clear();
            var transactions = _blockChain.Chain
                .SelectMany(b => b.Transactions)
                .ToList();

            var transaction = Extensions.AskForItem(
                transactions,
                "Search transaction: ",
                "Type to search transaction by id...\n",
                default,
                [t => t.Id]);

            return true;
        }

        public bool VerifyTransactionById() {
            Console.Clear();

            var transactions = _blockChain.Chain
                .SelectMany(b => b.Transactions)
                .Concat(_blockChain.Mempool.PendingTransactions)
                .ToList();

            if (transactions.Count == 0) {
                Extensions.WriteError("There are no transactions to verify.");
                Extensions.Pause();
                return true;
            }

            var transaction = Extensions.AskForItem(
                transactions,
                "Search transaction to verify: ",
                "Type to search transaction by id...\n",
                t => Console.WriteLine($"{t.Id} : {t.From} -> {t.To} : {t.Amount}"),
                [t => t.Id]);

            if (transaction is null) {
                Extensions.WriteError("No transaction selected.");
                Extensions.Pause();
                return true;
            }

            Console.WriteLine();
            Console.WriteLine(transaction);
            Console.WriteLine();

            PrintVerification("Result", _verificationService.Verify(transaction));
            Extensions.Pause();
            return true;
        }

        public bool DisplayVerificationReport() {
            Console.Clear();
            var report = _verificationService.GetReport();

            Console.WriteLine("Transaction verification report\n");
            Console.WriteLine($"Total checked : {report.Total}");
            Console.WriteLine($"Accepted      : {report.Accepted}");
            Console.WriteLine($"Rejected      : {report.Rejected}");
            Console.WriteLine();

            Extensions.Pause();
            return true;
        }

        public bool RunSecurityDemo() {
            Console.Clear();
            Console.WriteLine("Transaction security demo\n");

            var demoChain = new BlockChain(difficulty: 1, singleThreaded: true, useGpu: false);
            var demoWallets = new WalletManager();
            var demoTransactions = new TransactionService();
            var demoVerifier = new TransactionVerificationService();

            var alice = demoWallets.CreateWallet("Alice").Value;
            var bob = demoWallets.CreateWallet("Bob").Value;
            var tom = demoWallets.CreateWallet("Tom").Value;

            Console.WriteLine($"Wallets created: Alice={alice.Address}, Bob={bob.Address}, Tom={tom.Address}");
            var foundAlice = demoWallets.Wallets.FirstOrDefault(w => w.Address == alice.Address);
            Console.WriteLine($"Search by address {alice.Address}: {(foundAlice is null ? "not found" : foundAlice.Name)}");
            Console.WriteLine($"Public key maps to Alice's address: {new WalletService().GetAddress(alice.PublicKey) == alice.Address}\n");

            demoChain.MinePending(alice.Address); // give Alice coins so her transactions can be accepted

            var genuine = demoTransactions.CreateTransaction(alice, bob.Address, 10);
            PrintVerification("Genuine Alice transaction", demoVerifier.Verify(genuine));

            if (demoChain.AddTransaction(genuine).IsSuccess) {
                demoChain.MinePending(bob.Address);
            }

            var inBlockchain = demoChain.Chain
                .SelectMany(b => b.Transactions)
                .Any(t => t.Id == genuine.Id);
            Console.WriteLine($"Genuine transaction stored in Blockchain: {inBlockchain}\n");

            var changedAmount = demoTransactions.CreateTransaction(alice, bob.Address, 10);
            changedAmount.Amount = 11;
            PrintVerification("Changed amount after signing", demoVerifier.Verify(changedAmount));

            var changedRecipient = demoTransactions.CreateTransaction(alice, bob.Address, 10);
            changedRecipient.To = tom.Address;
            PrintVerification("Changed recipient after signing", demoVerifier.Verify(changedRecipient));

            var missingSignature = demoTransactions.CreateTransaction(alice, bob.Address, 10);
            missingSignature.Singature = null;
            PrintVerification("Missing signature", demoVerifier.Verify(missingSignature));

            var forgedOwner = new Transaction(alice.Address, bob.Address, 10) {
                SenderPublicKey = bob.PublicKey
            };
            forgedOwner.Singature = bob.Sign(forgedOwner.GetDataToSign());
            PrintVerification("Bob claims Alice's address (Bob's public key)", demoVerifier.Verify(forgedOwner));

            var forgedSignature = new Transaction(alice.Address, bob.Address, 10) {
                SenderPublicKey = alice.PublicKey
            };
            forgedSignature.Singature = bob.Sign(forgedSignature.GetDataToSign());
            PrintVerification("Bob uses Alice's public key but Bob's signature", demoVerifier.Verify(forgedSignature));

            Console.WriteLine("\nAttempting to add invalid transactions to the Blockchain:");
            (string Label, Transaction Transaction)[] invalidTransactions = [
                ("Changed amount", changedAmount),
                ("Changed recipient", changedRecipient),
                ("Missing signature", missingSignature),
                ("Forged owner (Bob's public key)", forgedOwner),
                ("Forged owner (Bob's signature)", forgedSignature)
            ];

            foreach (var (label, transaction) in invalidTransactions) {
                var result = demoChain.AddTransaction(transaction);
                if (result.IsSuccess) {
                    Extensions.WriteError($"{label}: unexpectedly accepted!");
                }
                else {
                    Extensions.WriteSuccess($"{label}: rejected ({result.Error})");
                }
            }

            var report = demoVerifier.GetReport();
            Console.WriteLine($"\nReport: checked {report.Total}, accepted {report.Accepted}, rejected {report.Rejected}");
            Extensions.Pause();
            return true;
        }

        private static void PrintVerification(string label, Failable<TransactionValidationError> result) {
            if (result.Verify(out var error)) {
                Extensions.WriteSuccess($"{label}: Valid");
            }
            else {
                Extensions.WriteError($"{label}: Invalid - {DescribeValidationError(error)}");
            }
        }

        private static string DescribeValidationError(TransactionValidationError error) => error switch {
            TransactionValidationError.EmptySender => "sender address is empty",
            TransactionValidationError.EmptyRecipient => "recipient address is empty",
            TransactionValidationError.InvalidAmount => "amount must be positive",
            TransactionValidationError.DuplicateId => "transaction id already exists",
            TransactionValidationError.EmptySignatureOrSenderPublicKey => "signature or sender public key is missing",
            TransactionValidationError.NonMatchingSignature => "signature does not match the transaction data",
            TransactionValidationError.InsufficientBalance => "insufficient balance",
            TransactionValidationError.PublicKeyNotMatchingSender => "sender address does not match the public key",
            _ => "unknown reason"
        };

        public bool SearchUserOccurences() {
            Console.Clear();
            var user = Extensions.AskForItem(
                    _blockChain.Chain
                        .SelectMany(b => b.Transactions)
                        .SelectMany(t => new[] { t.From, t.To })
                        .Distinct()
                        .ToList(),
                        "Search user: ",
                        "Type to search user by name...");

            if (user is null) return true;

            bool Find(bool from, bool to) {
                Console.WriteLine($"All transactions where {user}{(from ? " sent" : "")}{(from && to ? " or" : "")}{(to ? " received" : "")}:\n");

                var matches = _blockChain.Chain
                    .SelectMany(b => b.Transactions)
                    .Where(t => from && t.From.Equals(user, StringComparison.OrdinalIgnoreCase) ||
                                  to && t.To.Equals(user, StringComparison.OrdinalIgnoreCase));

                foreach (var match in matches) {
                    WriteHighlighted(match.ToString(), user);
                }
                Extensions.Pause();

                return true;

            }

            var occurences = new Menu($"Find occurences of {user}", [
                "Find all occurences",
                "Find all sent",
                "Find all received\n",

                "Exit to menu"
            ], [
                () => Find(true, true),
                () => Find(true, false),
                () => Find(false, true),

                () => false
            ]);

            occurences.Invoke();

            return true;
        }


        public bool DisplayChain() {
            Console.Clear();
            _displayService.DisplayChain(_blockChain);
            Extensions.Pause();
            return true;
        }

        public bool DisplayDifficultyHistory() {
            Console.Clear();
            _displayService.DisplayDifficultyHistory(_blockChain);
            Extensions.Pause();
            return true;
        }

        public bool MiningBenchmark() => MiningBenchmarkAsync().GetAwaiter().GetResult();

        private async Task<bool> MiningBenchmarkAsync() {
            Console.Clear();
            Console.Write("\x1b[3J");

            var (useGpu, singleThreaded) = SelectMiningBackend();
            MiningService ms = new(singleThreaded, useGpu);

            if (useGpu && !ms.UsesGpu) {
                Extensions.WriteError("Error: Failed to inititalize GPU");
                Extensions.Pause();
                return true;
            }

            int difficulty = Extensions.GetNumber("Enter hash difficulty: ", "Error: difficulty \"{0}\" is invalid/not supported", [1, 32]);


            var genesisBlock = new Block {
                Index = 0,
                TimeStamp = DateTime.FromBinary(0),
                Transactions = [],
                PreviousHash = "0"
            };

            ms.MineBlock(genesisBlock, 1);
            Console.Clear();

            var block = new Block {
                Index = 1,
                TimeStamp = DateTime.FromBinary(0),
                Transactions = [],
                PreviousHash = genesisBlock.Hash
            };

            char[] spinnerFrames = ['|', '/', '-', '\\'];
            int currentFrame = 0;

            MiningProgress? latestProgress = null;
            var progressLock = new Lock();
            var progress = new Progress<MiningProgress>(p => {
                lock (progressLock) latestProgress = p;
            });

            Console.WriteLine();
            var task = ms.MineBlockAsync(block, difficulty, progress);
            Console.CursorVisible = false;

            int left, top = 0;

            while (!task.IsCompleted) {
                MiningProgress? current;
                lock (progressLock) current = latestProgress;

                lock (Shared.ConsoleLock) {
                    (left, top) = Console.GetCursorPosition();

                    Console.SetCursorPosition(0, 0);
                    Console.ForegroundColor = ConsoleColor.Green;

                    string status = current is { } p
                        ? $"Mining the block {spinnerFrames[currentFrame % 4]} : {p.HashesTried:N0} checked hashes : {p.FormattedHashRate()}"
                        : $"Mining the block {spinnerFrames[currentFrame % 4]}";

                    Console.Write(status);
                    Console.WriteLine(new string(' ', Math.Max(0, Console.WindowWidth - Console.CursorLeft)));

                    Console.ResetColor();
                    Console.SetCursorPosition(left, top);
                }

                ++currentFrame;
                await Task.Delay(150);
            }
            Console.CursorVisible = true;


            lock (Shared.ConsoleLock) {
                Console.ForegroundColor = ConsoleColor.Green;
                (left, top) = Console.GetCursorPosition();
                Console.SetCursorPosition(0, 0);

                Console.Write("Block mined!");
                Console.WriteLine(new string(' ', Console.WindowWidth - Console.CursorLeft));
                Console.Write(new string(' ', Console.WindowWidth - Console.CursorLeft));

                Console.SetCursorPosition(left, top);
                Console.WriteLine($"Mining speed is {task.Result.FormattedHashRate()}");
                Console.ResetColor();
            }


            Extensions.Pause();
            return true;
        }

        private static (bool UseGpu, bool SingleThreaded) SelectMiningBackend() {
            bool useGpu = new Menu("Select miner", ["CPU", "GPU"]).Show() == 1;

            bool singleThreaded = false;
            if (!useGpu) {
                singleThreaded = new Menu("Select CPU variant", ["Single-threaded", "Multi-threaded"]).Show() == 0;
            }

            return (useGpu, singleThreaded);
        }

        public bool DisplayAllWallets() {
            Console.WriteLine("Wallets: \n");

            foreach (var wallet in _walletManager.Wallets) {
                Console.WriteLine($"{wallet.Name,-8} : {wallet.Address,-12} : {Convert.ToBase64String(wallet.PublicKey)[30..]}");
            }
            Console.WriteLine();

            Extensions.Pause();
            return true;
        }


        public bool CreateNewWallet() {
            string name = "";

            while (true) {
                Console.Write("Enter new wallet name: ");
                name = Console.ReadLine() ?? "";

                if (!_walletManager.CreateWallet(name).Verify(out _, out var error)) {
                    Console.Clear();
                    Extensions.WriteError($"Error: {error}");
                    continue;
                }
                break;
            }

            Extensions.WriteSuccess("Wallet created");
            Extensions.Pause();
            return true;
        }

        public bool SearchWallet(out Wallet? wallet, string prompt = "Search wallet: ") {
            Console.Clear();
            wallet = Extensions.AskForItem(
                _walletManager.Wallets,
                prompt,
                "Type to search wallets...",
                wallet => Console.WriteLine($"{wallet.Name,-8} : {wallet.Address}"),
                [w => w.Name, w => w.Address]
                );

            return true;
        }

        public bool AddTransaction() {
            SearchWallet(out var senderWallet, "Select sender-wallet: ");
            if (senderWallet is null) {
                Extensions.WriteError("No sender was selected");
                Extensions.Pause();
                return true;
            }

            SearchWallet(out var receiverWallet, "Select reciever-wallet: "); // Using wallet for simplicity, we don't actually need the wallet of the reciever - only his address
            if (receiverWallet is null) {
                Extensions.WriteError("No reciever was selected");
                Extensions.Pause();
                return true;
            }

            Console.Clear();
            var amount = Extensions.GetNumber("Enter amount: ", "Error: Amount must be greater than zero!", [1, int.MaxValue]);

            Console.Clear();
            Console.WriteLine($"Sender: {senderWallet.Name,-8} : {senderWallet.Address}");
            Console.WriteLine($"Reciever: {receiverWallet.Name,-6} : {receiverWallet.Address}");
            Console.WriteLine($"Amount: {amount}\n");

            var transaction = _transactionService.CreateTransaction(senderWallet, receiverWallet.Address, amount);
            if (!_blockChain.AddTransaction(transaction).Verify(out var error)) {
                Extensions.WriteError($"Failed to add transaction: {error}");
                Extensions.Pause();
                return true;
            }

            Extensions.WriteSuccess("Transaction added");
            Extensions.Pause();
            return true;
        }

    }
}
