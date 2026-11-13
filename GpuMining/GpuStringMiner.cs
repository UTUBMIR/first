using System.Buffers.Binary;
using System.Buffers.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using ILGPU;
using ILGPU.Runtime;

namespace First.GpuMining {
    public sealed class GpuStringMiner : IDisposable {
        private static readonly Lock SharedLock = new();
        private static GpuStringMiner? _shared;
        private static bool _sharedFailed;

        /// <summary>
        /// Process-wide instance. Creating an ILGPU context and compiling the
        /// kernel is expensive, so reuse one instance across chains.
        /// </summary>
        public static GpuStringMiner? Shared {
            get {
                lock (SharedLock) {
                    if (_shared is null && !_sharedFailed) {
                        try {
                            _shared = new GpuStringMiner(new GpuMinerOptions());
                        }
                        catch {
                            _sharedFailed = true;
                        }
                    }
                    return _shared;
                }
            }
        }

        private readonly GpuMinerOptions _options;
        private readonly Context _context;
        private readonly Accelerator _accelerator;
        private readonly Action<
            Index1D,
            ArrayView<uint>,
            ArrayView<byte>,
            int,
            uint,
            ArrayView<uint>,
            ArrayView<uint>,
            ulong,
            ArrayView<uint>> _kernel;

        private readonly MemoryBuffer1D<uint, Stride1D.Dense> _midBuf;
        private readonly MemoryBuffer1D<byte, Stride1D.Dense> _tailBuf;
        private readonly MemoryBuffer1D<uint, Stride1D.Dense> _kBuf;
        private readonly MemoryBuffer1D<uint, Stride1D.Dense> _targetBuf;
        private readonly MemoryBuffer1D<uint, Stride1D.Dense> _resBuf;
        private readonly Lock _lock = new();

        public string DeviceName => _accelerator.Name;

        public GpuStringMiner(GpuMinerOptions? options = null) {
            _options = options ?? new GpuMinerOptions();
            if (_options.BatchSize <= 0) {
                throw new ArgumentOutOfRangeException(nameof(options), "BatchSize must be positive.");
            }

            var context = Context.CreateDefault();
            Accelerator? accelerator = null;
            try {
                var device = context.GetPreferredDevice(preferCPU: _options.PreferCpuAccelerator);

                if (!_options.PreferCpuAccelerator && device.AcceleratorType == AcceleratorType.CPU) {
                    throw new InvalidOperationException(
                        "No GPU accelerator is available. Falling back to the native CPU miner.");
                }

                accelerator = device.CreateAccelerator(context);

                _context = context;
                _accelerator = accelerator;
                _kernel = accelerator.LoadAutoGroupedStreamKernel<
                    Index1D,
                    ArrayView<uint>,
                    ArrayView<byte>,
                    int,
                    uint,
                    ArrayView<uint>,
                    ArrayView<uint>,
                    ulong,
                    ArrayView<uint>>(GpuMineKernel.Mine);

                _midBuf = accelerator.Allocate1D<uint>(8);
                _tailBuf = accelerator.Allocate1D<byte>(64);
                _kBuf = accelerator.Allocate1D<uint>(GpuSha256.K);
                _targetBuf = accelerator.Allocate1D<uint>(DifficultyTarget.WordCount);
                _resBuf = accelerator.Allocate1D<uint>(11);
            }
            catch {
                accelerator?.Dispose();
                context.Dispose();
                throw;
            }
        }

        public GpuMiningResult Mine(
            ReadOnlySpan<byte> prefix,
            int difficulty,
            ulong startNonce = 0,
            IProgress<GpuMiningProgress>? progress = null,
            CancellationToken cancellationToken = default) {

            lock (_lock) {
                var stopwatch = Stopwatch.StartNew();

                var midstate = GpuSha256.MidstatePrefix(prefix, out int consumed);
                int tailLen = prefix.Length - consumed;
                var tail = new byte[64];
                prefix.Slice(consumed, tailLen).CopyTo(tail);

                _midBuf.View.CopyFromCPU(midstate);
                _tailBuf.View.CopyFromCPU(tail);

                var target = DifficultyTarget.FromLeadingZeroNibbles(difficulty);
                _targetBuf.View.CopyFromCPU(target.Words);

                ulong hashes = 0;
                ulong start = startNonce;
                var lastProgress = TimeSpan.Zero;

                while (true) {
                    cancellationToken.ThrowIfCancellationRequested();

                    ulong count = (ulong)_options.BatchSize;
                    if (start > ulong.MaxValue - count + 1)
                        count = ulong.MaxValue - start + 1;

                    _resBuf.MemSetToZero();
                    _kernel(
                        (int)count,
                        _midBuf.View,
                        _tailBuf.View,
                        tailLen,
                        (uint)prefix.Length,
                        _kBuf.View,
                        _targetBuf.View,
                        start,
                        _resBuf.View);
                    _accelerator.Synchronize();

                    var r = _resBuf.GetAsArray1D();
                    hashes += count;

                    if (r[0] != 0) {
                        ulong nonce = ((ulong)r[2] << 32) | r[1];
                        var hash = Recompute(prefix, nonce);

                        if (_options.VerifyResults) {
                            for (int i = 0; i < 8; ++i) {
                                uint cpuWord = BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(i * 4, 4));
                                if (cpuWord != r[3 + i]) {
                                    throw new InvalidOperationException(
                                        "GPU SHA-256 result does not match CPU recomputation.");
                                }
                            }
                        }

                        if (target.Meets(hash))
                            return new GpuMiningResult(true, nonce, hash, hashes, stopwatch.Elapsed);

                        if (nonce == ulong.MaxValue)
                            return new GpuMiningResult(false, 0, Array.Empty<byte>(), hashes, stopwatch.Elapsed);

                        start = nonce + 1;
                        continue;
                    }

                    if (progress is not null && stopwatch.Elapsed - lastProgress >= TimeSpan.FromMilliseconds(600)) {
                        progress.Report(new GpuMiningProgress(
                            start + count - 1,
                            hashes,
                            stopwatch.Elapsed.TotalSeconds > 0
                                ? hashes / stopwatch.Elapsed.TotalSeconds
                                : 0,
                            stopwatch.Elapsed));
                        lastProgress = stopwatch.Elapsed;
                    }

                    if (start + count - 1 == ulong.MaxValue)
                        return new GpuMiningResult(false, 0, Array.Empty<byte>(), hashes, stopwatch.Elapsed);

                    start += count;
                }
            }
        }

        public Task<GpuMiningResult> MineAsync(
            byte[] prefix,
            int difficulty,
            ulong startNonce = 0,
            IProgress<GpuMiningProgress>? progress = null,
            CancellationToken cancellationToken = default) {
            return Task.Run(
                () => Mine(prefix, difficulty, startNonce, progress, cancellationToken),
                cancellationToken);
        }

        private static byte[] Recompute(ReadOnlySpan<byte> prefix, ulong nonce) {
            Span<byte> nonceBytes = stackalloc byte[20];
            Utf8Formatter.TryFormat(nonce, nonceBytes, out int written);

            var message = new byte[prefix.Length + written];
            prefix.CopyTo(message);
            nonceBytes.Slice(0, written).CopyTo(message.AsSpan(prefix.Length));

            return SHA256.HashData(message);
        }

        public void Dispose() {
            _resBuf.Dispose();
            _targetBuf.Dispose();
            _kBuf.Dispose();
            _tailBuf.Dispose();
            _midBuf.Dispose();
            _accelerator.Dispose();
            _context.Dispose();
        }
    }
}
