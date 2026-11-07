using System.Diagnostics;
using First;

var blockChain = new BlockChain();
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

bool GetBlockByIndexAction() {
    Console.Clear();
    int maxIndex = blockChain.Chain.Count - 1;
    int index = Extensions.GetNumber(
        $"Enter block index (0-{maxIndex}): ",
        "\aError: {0} is out of range!",
        [0, maxIndex]);

    Console.Clear();
    var block = blockChain.GetBlockByIndex(index);
    if (block is null) {
        Extensions.WriteError($"Block with index {index} was not found.");
    }
    else {
        displayService.DisplayBlock(block);
    }

    Extensions.Pause();
    return true;
}

bool SearchBlockDataAction() {
    Console.Clear();
    string query = Extensions.AsyncType(
        typed => {
            if (string.IsNullOrWhiteSpace(typed)) {
                Console.WriteLine();
                Console.Write("Type to search block data...");
                return;
            }

            var matches = blockChain.Chain
                .Where(b => b.Data.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            Console.WriteLine();
            if (matches.Count == 0) {
                Extensions.WriteError($"No blocks contain \"{typed}\"");
                return;
            }

            Extensions.WriteSuccess($"{matches.Count} match(es):");
            foreach (var match in matches) {
                Console.WriteLine($"[{match.Index}] {match.Data}");
            }
        },
        "Search block data: ");

    Console.Clear();
    var found = blockChain.FindBlockByData(query);

    if (string.IsNullOrWhiteSpace(query)) {
        Extensions.WriteError("Search cancelled: no query was entered.");
    }
    else if (found is null) {
        Extensions.WriteError($"No block contains \"{query}\".");
    }
    else {
        displayService.DisplayBlock(found);
    }

    Extensions.Pause();
    return true;
}

bool DisplayChainAction() {
    Console.Clear();
    displayService.DisplayChain(blockChain);
    Extensions.Pause();
    return true;
}

var menu = new Menu(
    "Blockchain explorer",
    [
        "Get block by index",
        "Search block data",
        "Display whole chain",
        "Exit"
    ],
    [
        GetBlockByIndexAction,
        SearchBlockDataAction,
        DisplayChainAction,
        () => false
    ]);

menu.Invoke();
