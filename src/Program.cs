using System.Diagnostics;
using First;


var blockChain = new BlockChain(4);
var displayService = new BlockChainDisplayService();
var transactionService = new TransactionService();

void DisplayAndAssert(bool expected) { // NOTE: Clousing the blockChain variable
    var isV = blockChain.IsChainValid();
    displayService.DisplayChain(blockChain);
    Console.WriteLine("valid:" + isV);
    Console.WriteLine("\n\n");

    Debug.Assert(expected == isV);
}

var transactionPool = new List<Transaction> {
    transactionService.CreateTransaction("Bob", "Illya", 4),
    transactionService.CreateTransaction("Alice", "Bob", 23),
    transactionService.CreateTransaction("Tom", "Bob", 17),
};

blockChain.AddBlock(transactionPool);
transactionPool.Clear();

transactionPool.AddRange([
    transactionService.CreateTransaction("Illya", "Bob", 1),
    transactionService.CreateTransaction("Bob", "Tom", 9),
]);

blockChain.AddBlock(transactionPool);
transactionPool.Clear();

transactionPool.AddRange([
    transactionService.CreateTransaction("Tom", "Alice", 92),
    transactionService.CreateTransaction("Illya", "Alice", 83),
    transactionService.CreateTransaction("Tom", "Illya", 48),
    transactionService.CreateTransaction("Bob", "Alice", 37),
]);

blockChain.AddBlock(transactionPool);
transactionPool.Clear();

transactionPool.AddRange([
    transactionService.CreateTransaction("Illya", "Tom", 430),
    transactionService.CreateTransaction("Alice", "Bob", 12),
    transactionService.CreateTransaction("Illya", "Alice", 53),
]);

blockChain.AddBlock(transactionPool);
transactionPool.Clear();




//
// DisplayAndAssert(true);
//
// blockChain.Chain[1].Data = "hacked data";
//
// DisplayAndAssert(false);

Console.WriteLine("press any key to proceed to menu");
Console.ReadKey();

var actions = new MenuActions(blockChain, displayService);

var menu = new Menu(
    "Blockchain explorer",
    [
        "Get block by index",
        "Display difficulty history",
        "Display whole chain\n",

        "Search transaction by id",
        "Search user occurences",
        "Add transaction to the pool",
        "Mine the block",
        "Validate the chain\n",

        "Mining Benchmark\n",

        "Exit"
    ],
    [
        actions.GetBlockByIndex,
        actions.DisplayDifficultyHistory,
        actions.DisplayChain,

        actions.SearchTransactionById,
        actions.SearchUserOccurences,
        () => {
            Console.Write("Enter sender: ");
            var sender = Console.ReadLine();

            Console.Write("Enter reciever: ");
            var reciever = Console.ReadLine();

            var amount = Extensions.GetNumber("Enter amount: ", "Error: Amount cannot be negative!", [0, int.MaxValue]);

            try {
                transactionPool.Add(transactionService.CreateTransaction(sender, reciever, amount));
            }
            catch (Exception e) {
                Extensions.WriteError($"Failed to add transaction: {e}");
                return true;
            }

            Extensions.WriteSuccess("Transaction added");
            Extensions.Pause();
            return true;
        },
        () => {
            blockChain.AddBlock(transactionPool);
            transactionPool.Clear();
            Extensions.WriteSuccess("Block added");
            Extensions.Pause();
            return true;
        },
        () => {
            Extensions.WriteSuccess($"Is Valid: {blockChain.IsChainValid()}");
            Extensions.Pause();
            return true;
        },

        actions.MiningBenchmark,

        () => false
    ]);

menu.Invoke();
