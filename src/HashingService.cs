namespace First {
    public class HashingService {
        public string ComputeHash(Block block) {
            return BlockHashing.ComputeHashHex(block);
        }
        public string ComputeMerkleRoot(List<Transaction> transactions) {
            return BlockHashing.ComputeMerkleRoot(transactions);
        }

        public MerkleProof? GetMerkleProof(List<Transaction> transactions, string transactionId) {
            return BlockHashing.GetMerkleProof(transactions, transactionId);
        }

        public bool VerifyMerkleProof(Transaction transaction, MerkleProof proof) {
            return BlockHashing.VerifyMerkleProof(transaction, proof);
        }

        public bool VerifyMerkleProof(Transaction transaction, MerkleProof proof, string expectedMerkleRoot) {
            return BlockHashing.VerifyMerkleProof(transaction, proof, expectedMerkleRoot);
        }
    }
}
