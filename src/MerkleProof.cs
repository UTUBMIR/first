namespace First {
    public readonly record struct MerkleProofStep(string SiblingHash, bool SiblingIsLeft);

    public sealed record MerkleProof(
        string TransactionId,
        string MerkleRoot,
        IReadOnlyList<MerkleProofStep> Steps);
}
