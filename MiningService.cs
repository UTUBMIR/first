using System.Diagnostics;

namespace First {
    public class MiningService {
        private readonly HashingService _hashingService;
        private readonly bool _singleThreaded;

        public MiningService(HashingService hashingService, bool singleThreaded = false) {
            _hashingService = hashingService;
            _singleThreaded = singleThreaded;
        }

        private static bool MeetsDifficulty(ReadOnlySpan<byte> hash, int difficulty) {
            int fullBytes = difficulty / 2;
            for (int i = 0; i < fullBytes; ++i)
                if (hash[i] != 0) return false;

            return (difficulty & 1) != 1 || (hash[fullBytes] & 0xF0) == 0;
        }

        public Task MineBlockAsync(Block block, int difficulty) {
            return Task.Run(() => MineBlock(block, difficulty));
        }

        public void MineBlock(Block block, int difficulty) {
            Debug.Assert(block.Nonce == 0);

            ulong iterations = 0;
            var stopwatch = Stopwatch.StartNew();
            var prefix = _hashingService.EncodePrefix(block);

            if (_singleThreaded) { // avoid the parallel if we are singe-threaded anyway
                Span<byte> digest = stackalloc byte[32];
                ulong nonce = 0;

                while (true) {
                    _hashingService.ComputeHash(prefix, nonce, digest);
                    ++iterations;

                    if (MeetsDifficulty(digest, difficulty)) { // myBlock.Hash.StartsWith(prefix)
                        stopwatch.Stop();
                        block.Hash = Convert.ToHexStringLower(digest);
                        block.Nonce = nonce;
                        Console.WriteLine($"Mined block in {iterations} iterations, used {stopwatch.ElapsedMilliseconds}ms with difficulty: {difficulty}");
                        return;
                    }

                    ++nonce;
                }
            }

            var coreCount = Environment.ProcessorCount;
            var coreIds = Enumerable.Range(0, coreCount).ToList();

            var options = new ParallelOptions {
                MaxDegreeOfParallelism = coreCount
            };

            var step = ulong.MaxValue / (ulong)coreCount;

            Parallel.ForEach(coreIds, options, (coreId, loop) => {
                ulong nonce = step * (ulong)coreId;
                ulong iters = 0;
                Span<byte> digest = stackalloc byte[32];

                while (true) {
                    _hashingService.ComputeHash(prefix, nonce, digest);

                    if (loop.IsStopped) {
                        Interlocked.Add(ref iterations, iters);
                        return;
                    }
                    if (MeetsDifficulty(digest, difficulty)) {
                        loop.Stop();
                        stopwatch.Stop();

                        block.Hash = Convert.ToHexStringLower(digest);
                        block.Nonce = nonce;
                        return;
                    }

                    if (nonce % 10_000_000 == 0 && nonce != 0) {
                        Interlocked.Add(ref iterations, iters);
                        iters = 0;
                        lock (Shared.ConsoleLock) {
                            Console.WriteLine($"Mining at ~{iterations} - {Convert.ToHexStringLower(digest)}");
                        }
                    }

                    ++nonce;
                    ++iters;
                }
            });

            lock (Shared.ConsoleLock) {
                Console.WriteLine($"\nMined block in {iterations} iterations, used {stopwatch.ElapsedMilliseconds}ms with difficulty: {difficulty}");
            }

        }
    }
}

