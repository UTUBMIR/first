namespace First {
    public class BlockChainDisplayService {

        private readonly static string sep = new('-', 50);
        public void DisplayChain(BlockChain blockChain) {
            Console.WriteLine("Blockchain:");
            foreach (var block in blockChain.Chain) {
                DisplayBlock(block);
            }
        }

        public void DisplayDifficultyHistory(BlockChain blockChain) {
            Console.WriteLine("Difficulty history:");
            foreach (var block in blockChain.Chain) {
                DisplayBlockDifficulty(block);
            }
        }

        public void DisplayBlock(Block block) {
            Console.WriteLine($"Index: {block.Index}");
            Console.WriteLine($"TimeStamp: {block.TimeStamp}");
            Console.WriteLine($"Author: {block.Author}");
            Console.WriteLine($"Data: {block.Data}");
            Console.WriteLine($"Previous Hash: {block.PreviousHash}");
            Console.WriteLine($"Difficulty: {block.DifficultyAtMining}");
            Console.WriteLine($"Hash: {block.Hash}");
            Console.WriteLine($"Nonce: {block.Nonce}");
            Console.WriteLine(sep);
        }

        public void DisplayBlockDifficulty(Block block) {
            Console.WriteLine($"Difficulty: {block.DifficultyAtMining}");
            Console.WriteLine($"Mining Duration: {block.MiningDuration}");
            // no attempts outout since miner is multithreaded
            Console.WriteLine(sep);
        }
    }
}
