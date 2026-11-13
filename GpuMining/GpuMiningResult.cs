namespace First.GpuMining {
    public readonly record struct GpuMiningProgress(
        ulong LastNonce,
        ulong HashesTried,
        double HashesPerSecond,
        TimeSpan Elapsed);

    public readonly record struct GpuMiningResult(
        bool Found,
        ulong Nonce,
        byte[] Hash,
        ulong HashesTried,
        TimeSpan Elapsed);

    public sealed class GpuMinerOptions {
        public bool PreferCpuAccelerator { get; init; } = false;
        public int BatchSize { get; init; } = 1 << 20;
        public bool VerifyResults { get; init; } = true;
    }
}
