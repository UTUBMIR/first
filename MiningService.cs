using System.Diagnostics;

namespace First {
    public readonly record struct MiningResult(ulong Iterations, TimeSpan Duration) {
        public double HashesPerSecond =>
            Duration.TotalSeconds > 0 ? Iterations / Duration.TotalSeconds : 0;

        public string FormatHashRate(double hashesPerSecond) {
            string[] units = ["H/s", "kH/s", "MH/s", "GH/s", "TH/s", "PH/s", "EH/s"];

            if (hashesPerSecond <= 0) {
                return $"0{units[0]}";
            }

            int unitIndex = 0;
            double value = hashesPerSecond;

            while (value >= 1000 && unitIndex < units.Length - 1) {
                value /= 1000;
                ++unitIndex;
            }

            return $"{value:F3}{units[unitIndex]}";
        }

        public string FormattedHashRate() {
            return FormatHashRate(HashesPerSecond);
        }
    }
    public class MiningService {
        private readonly bool _singleThreaded;

        public MiningService(bool singleThreaded = false) {
            _singleThreaded = singleThreaded;
        }

        private static bool MeetsDifficulty(ReadOnlySpan<byte> hash, int difficulty) {
            int fullBytes = difficulty / 2;
            for (int i = 0; i < fullBytes; ++i)
                if (hash[i] != 0) return false;

            return (difficulty & 1) != 1 || (hash[fullBytes] & 0xF0) == 0;
        }

        public Task<MiningResult> MineBlockAsync(Block block, int difficulty) {
            return Task.Run(() => MineBlock(block, difficulty));
        }

        public MiningResult MineBlock(Block block, int difficulty) {
            Debug.Assert(block.Nonce == 0);

            block.DifficultyAtMining = difficulty;

            ulong iterations = 0;
            var stopwatch = Stopwatch.StartNew();

            var coreCount = _singleThreaded ? 1 : Environment.ProcessorCount;
            var coreIds = Enumerable.Range(0, coreCount).ToList();

            var options = new ParallelOptions {
                MaxDegreeOfParallelism = coreCount
            };

            var step = ulong.MaxValue / (ulong)coreCount;

            Parallel.ForEach(coreIds, options, (coreId, loop) => {
                var blockHasher = new BlockHasher();

                var _block = block.Clone();
                _block.Nonce = step * (ulong)coreId;
                ulong iters = 0;

                try {
                    while (true) {
                        var hash = blockHasher.ComputeHashBinary(_block);

                        if (loop.IsStopped) {
                            break;
                        }
                        if (MeetsDifficulty(hash, difficulty)) {
                            loop.Stop();
                            stopwatch.Stop();
                            block.Nonce = _block.Nonce;
                            block.Hash = Convert.ToHexStringLower(hash);
                            block.MiningDuration = stopwatch.Elapsed.TotalSeconds;
                            break;
                        }

                        if (_block.Nonce % 10_000_000 == 0 && _block.Nonce != 0) {
                            Interlocked.Add(ref iterations, iters);
                            iters = 0;
                            lock (Shared.ConsoleLock) {
                                Console.WriteLine($"Mining at ~{iterations} - {Convert.ToHexStringLower(hash)}");
                            }
                        }

                        ++_block.Nonce;
                        ++iters;
                    }
                }
                finally {
                    Interlocked.Add(ref iterations, iters);
                }
            });

            lock (Shared.ConsoleLock) {
                Console.WriteLine($"\nMined block in {iterations} iterations, used {stopwatch.ElapsedMilliseconds}ms with difficulty: {difficulty}");
            }

            return new MiningResult(iterations, stopwatch.Elapsed);
        }
    }
}
