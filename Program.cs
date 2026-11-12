using System.Diagnostics;
using First;


var blockChain = new BlockChain(4);
var displayService = new BlockChainDisplayService();

void DisplayAndAssert(bool expected) { // NOTE: Clousing the blockChain variable
    var isV = blockChain.IsChainValid();
    displayService.DisplayChain(blockChain);
    Console.WriteLine("valid:" + isV);
    Console.WriteLine("\n\n");

    Debug.Assert(expected == isV);
}

blockChain.AddBlock("Alex", "New user alex registered");
blockChain.AddBlock("Illya", "Illya bought 16Mb DDR5");
blockChain.AddBlock("Alex", "Alex contributed to Illya's project. ");
blockChain.AddBlock("Bob", "Bob sent guaranteed random number of $ (4) to Illya");
blockChain.AddBlock("Slack", "502 Bad Gateway");

DisplayAndAssert(true);

blockChain.Chain[1].Data = "hacked data";

DisplayAndAssert(false);

Console.WriteLine("press any key to proceed to menu");
Console.ReadKey();

var actions = new MenuActions(blockChain, displayService);

var menu = new Menu(
    "Blockchain explorer",
    [
        "Get block by index",
        "Search block data",
        "Display difficulty history",
        "Display whole chain\n",

        "Mining Benchmark\n",

        "Exit"
    ],
    [
        actions.GetBlockByIndex,
        actions.SearchBlockData,
        actions.DisplayDifficultyHistory,
        actions.DisplayChain,
        actions.MiningBenchmark,
        () => false
    ]);

menu.Invoke();
