using System.Diagnostics;

namespace First {
    public sealed class MenuActions {
        private readonly BlockChain _blockChain;
        private readonly BlockChainDisplayService _displayService;
        private readonly WalletManager _walletManager;

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

        public bool SearchUserOccurences() { // TODO: move this to a separate Extensions method, probably merge with Extensions.AskForWord
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

    }
}
