namespace First {
    public class BlockChainDisplayService {

        private readonly static string sep = new('-', 50);
        public void DisplayChain(BlockChain blockChain) {
            Console.WriteLine("Blockchain:");
            foreach (var block in blockChain.Chain) {
                DisplayBlock(block);
            }
        }

        public void DisplayBlock(Block block) {
            Console.WriteLine($"Index: {block.Index}");
            Console.WriteLine($"TimeStamp: {block.TimeStamp}");
            Console.WriteLine($"Author: {block.Author}");
            Console.WriteLine($"Data: {block.Data}");
            Console.WriteLine($"Previous Hash: {block.PreviousHash}");
            Console.WriteLine($"Hash: {block.Hash}");
            Console.WriteLine(sep);
        }
    }
}
