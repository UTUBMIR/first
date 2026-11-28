using System.Diagnostics;
using First;


var blockChain = new BlockChain(4);
var displayService = new BlockChainDisplayService();
var transactionService = new TransactionService();
var walletManager = new WalletManager();

void DisplayAndAssert(bool expected) { // NOTE: Clousing the blockChain variable
    var isV = blockChain.IsChainValid();
    displayService.DisplayChain(blockChain);
    Console.WriteLine("valid:" + isV);
    Console.WriteLine("\n\n");

    Debug.Assert(expected == isV);
}

var aliceWallet = walletManager.CreateWallet("Alice").Value;
var bobWallet = walletManager.CreateWallet("Bob").Value;
var illyaWallet = walletManager.CreateWallet("Illya").Value;
var tomWallet = walletManager.CreateWallet("Tom").Value;

blockChain.MinePending(bobWallet.Address);
blockChain.MinePending(illyaWallet.Address);
blockChain.MinePending(illyaWallet.Address);
blockChain.MinePending(aliceWallet.Address);

blockChain.AddTransactions([
    transactionService.CreateTransaction(bobWallet, illyaWallet.Address, 4),
    transactionService.CreateTransaction(aliceWallet, tomWallet.Address, 23),
]);


blockChain.MinePending(illyaWallet.Address);

blockChain.AddTransactions([
    transactionService.CreateTransaction(aliceWallet, bobWallet.Address, 1),
    transactionService.CreateTransaction(bobWallet, tomWallet.Address, 19),
    transactionService.CreateTransaction(tomWallet, bobWallet.Address, 16),
]);

blockChain.MinePending(illyaWallet.Address);

blockChain.AddTransactions([
    transactionService.CreateTransaction(bobWallet, tomWallet.Address, 2),
    transactionService.CreateTransaction(illyaWallet, aliceWallet.Address, 83),
    transactionService.CreateTransaction(aliceWallet, illyaWallet.Address, 8),
    transactionService.CreateTransaction(bobWallet, aliceWallet.Address, 17),
]);

blockChain.MinePending(aliceWallet.Address);
blockChain.MinePending(aliceWallet.Address);

blockChain.AddTransactions([
    transactionService.CreateTransaction(illyaWallet, tomWallet.Address, 43),
    transactionService.CreateTransaction(illyaWallet, bobWallet.Address, 12),
    transactionService.CreateTransaction(illyaWallet, aliceWallet.Address, 53),
]);

blockChain.MinePending(bobWallet.Address);




//
// DisplayAndAssert(true);
//
// blockChain.Chain[1].Data = "hacked data";
//
// DisplayAndAssert(false);

Console.WriteLine("press any key to proceed to menu");
Console.ReadKey();

var actions = new MenuActions(blockChain, displayService, walletManager);

var menu = new Menu(
    "Blockchain explorer",
    [
        "Get block by index",
        "Display difficulty history",
        "Display mempool",
        "Display whole chain\n",

        "Search transaction by id",
        "Search user occurences",
        "Add transaction to the pool",
        "Mine the block",
        "Validate the chain\n",

        "Display all wallets",
        "Create a new wallet",
        "Search wallets",
        "View wallet balance\n",

        "Verify transaction by id",
        "Show transaction verification report",
        "Run transaction security demo\n",

        "Mining Benchmark\n",

        "Exit"
    ],
    [
        actions.GetBlockByIndex,
        actions.DisplayDifficultyHistory,
        () => {
            displayService.DisplayMempool(blockChain.Mempool);
            Extensions.Pause();

            return true;
        },
        actions.DisplayChain,

        actions.SearchTransactionById,
        actions.SearchUserOccurences,
        actions.AddTransaction,
        () => {
            blockChain.MinePending(tomWallet.Address);
            Extensions.WriteSuccess("Block added");
            Extensions.Pause();
            return true;
        },
        () => {
            var result = blockChain.FindFirstInvalidBlock();
            if (result.Verify(out var error)) {
                Extensions.WriteSuccess("The chain is valid");
            }
            else {
                Extensions.WriteError($"The chain is invalid because of block at index \"{error.blockIndex}\" and reason: {error.reason}");
            }

            Extensions.Pause();
            return true;
        },

        () => actions.DisplayAllWallets(),
        () => actions.CreateNewWallet(),
        () => actions.SearchWallet(out _),
        () => {
            actions.SearchWallet(out var wallet);
            if (wallet is null) {
                Extensions.WriteError("No wallet selected");
                Extensions.Pause();
                return true;
            }

            var balance = new BalanceService().GetBalance(wallet.Address, blockChain.Chain);

            Console.WriteLine($"Balance for \"{wallet.Name}\" is: {balance}");
            Extensions.Pause();

            return true;
        },

        actions.VerifyTransactionById,
        actions.DisplayVerificationReport,
        actions.RunSecurityDemo,

        actions.MiningBenchmark,

        () => false
    ]);

menu.Invoke();
