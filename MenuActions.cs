using System.Diagnostics;

namespace First {
    public sealed class MenuActions {
        private readonly BlockChain _blockChain;
        private readonly BlockChainDisplayService _displayService;

        public MenuActions(BlockChain blockChain, BlockChainDisplayService displayService) {
            _blockChain = blockChain;
            _displayService = displayService;
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

        public bool SearchBlockData() {
            // Console.Clear();
            // string query = Extensions.AsyncType(
            //     typed => {
            //         if (string.IsNullOrWhiteSpace(typed)) {
            //             Console.WriteLine();
            //             Console.Write("Type to search block data...");
            //             return;
            //         }
            //
            //         var matches = _blockChain.Chain
            //             .Where(b => b.Data.Contains(typed, StringComparison.OrdinalIgnoreCase))
            //             .Take(5)
            //             .ToList();
            //
            //         Console.WriteLine();
            //         if (matches.Count == 0) {
            //             Extensions.WriteError($"No blocks contain \"{typed}\"");
            //             return;
            //         }
            //
            //         Extensions.WriteSuccess($"{matches.Count} match(es):");
            //         foreach (var match in matches) {
            //             Console.WriteLine($"[{match.Index}] {match.Data}");
            //         }
            //     },
            //     "Search block data: ");
            //
            // Console.Clear();
            // var found = _blockChain.FindBlockByData(query);
            //
            // if (string.IsNullOrWhiteSpace(query)) {
            //     Extensions.WriteError("Search cancelled: no query was entered.");
            // }
            // else if (found is null) {
            //     Extensions.WriteError($"No block contains \"{query}\".");
            // }
            // else {
            //     _displayService.DisplayBlock(found);
            // }
            //
            // Extensions.Pause();
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
            int difficulty = Extensions.GetNumber("Enter hash difficulty: ", "Error: difficulty \"{0}\" is invalid/not supported", [1, 32]);
            MiningService ms = new(singleThreaded, useGpu);

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
            var progressLock = new object();
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
    }
}
