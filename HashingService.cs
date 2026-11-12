namespace First {
    public class HashingService {
        public string ComputeHash(Block block) {
            return BlockHashing.ComputeHashHex(block);
        }
    }
}