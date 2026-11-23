using System.Diagnostics;
using First.GpuMining;

namespace First {
    public readonly record struct MiningResult(ulong Iterations, TimeSpan Duration) {
        public double HashesPerSecond =>
            Duration.TotalSeconds > 0 ? Iterations / Duration.TotalSeconds : 0;

        public static string FormatHashRate(double hashesPerSecond) {
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

    public readonly record struct MiningProgress(
        ulong Nonce,
        ulong HashesTried,
        double HashesPerSecond,
        TimeSpan Elapsed) {
        public string FormattedHashRate() => MiningResult.FormatHashRate(HashesPerSecond);
    }

    public class MiningService {
        private readonly bool _singleThreaded;
        private readonly GpuStringMiner? _gpuMiner;

        /// <summary>
        /// Whether gpu succeed initializing
        /// </summary>
        public bool UsesGpu => _gpuMiner is not null;

        public MiningService(bool singleThreaded = false, bool useGpu = true) {
            _singleThreaded = singleThreaded;
            _gpuMiner = useGpu ? GpuStringMiner.Shared : null;
        }

        private sealed class GpuProgressAdapter : IProgress<GpuMiningProgress> {
            private readonly IProgress<MiningProgress> _inner;

            public GpuProgressAdapter(IProgress<MiningProgress> inner) {
                _inner = inner;
            }

            public void Report(GpuMiningProgress value) {
                _inner.Report(new MiningProgress(
                    value.LastNonce,
                    value.HashesTried,
                    value.HashesPerSecond,
                    value.Elapsed));
            }
        }

        public Task<MiningResult> MineBlockAsync(Block block, int difficulty, IProgress<MiningProgress>? progress = null) {
            return Task.Run(() => MineBlock(block, difficulty, progress));
        }

        public MiningResult MineBlock(Block block, int difficulty, IProgress<MiningProgress>? progress = null) {
            Debug.Assert(block.Nonce == 0);

            block.DifficultyAtMining = difficulty;
            // resync just in case
            block.MerkleRoot = BlockHashing.ComputeMerkleRoot(block.Transactions);

            if (_gpuMiner is not null) {
                var prefix = BlockHashing.EncodePrefix(block);
                try {
                    var gpuProgress = progress is null ? null : new GpuProgressAdapter(progress);
                    var gpuResult = _gpuMiner.Mine(prefix, difficulty, block.Nonce, gpuProgress);
                    if (gpuResult.Found) {
                        block.Nonce = gpuResult.Nonce;
                        block.Hash = Convert.ToHexStringLower(gpuResult.Hash);
                        block.MiningDuration = gpuResult.Elapsed.TotalSeconds;

                        Debug.Assert(block.Hash == BlockHashing.ComputeHashHex(block));

                        lock (Shared.ConsoleLock) {
                            Console.WriteLine(
                                $"\nGPU mined block in {gpuResult.HashesTried:N0} checked hashes, " +
                                $"used {gpuResult.Elapsed.TotalMilliseconds:F0}ms with difficulty: {difficulty}");
                        }

                        return new MiningResult(gpuResult.HashesTried, gpuResult.Elapsed);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    lock (Shared.ConsoleLock) {
                        Console.WriteLine($"\nGPU mining failed: {ex.Message}. Falling back to CPU.");
                    }
                }
            }

            ulong iterations = 0;
            var stopwatch = Stopwatch.StartNew();

            var coreCount = _singleThreaded ? 1 : Environment.ProcessorCount;
            var coreIds = Enumerable.Range(0, coreCount).ToList();

            var options = new ParallelOptions {
                MaxDegreeOfParallelism = coreCount
            };

            var step = ulong.MaxValue / (ulong)coreCount;
            var target = DifficultyTarget.FromLeadingZeroNibbles(difficulty);

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
                        if (target.Meets(hash)) {
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

                            var elapsed = stopwatch.Elapsed;
                            var total = iterations;
                            progress?.Report(new MiningProgress(
                                _block.Nonce,
                                total,
                                elapsed.TotalSeconds > 0 ? total / elapsed.TotalSeconds : 0,
                                elapsed));

                            lock (Shared.ConsoleLock) {
                                Console.WriteLine($"Mining at ~{iterations:N0} - {Convert.ToHexStringLower(hash)}");
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
                Console.WriteLine($"\nMined block in {iterations:N0} checked hashes, used {stopwatch.ElapsedMilliseconds:N0}ms with difficulty: {difficulty}");
            }

            return new MiningResult(iterations, stopwatch.Elapsed);
        }
    }
}
