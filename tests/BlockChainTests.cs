using static First.TransactionService;

namespace First.Tests;

public class BlockChainTests {
    private readonly BlockChain _chain;
    private readonly TransactionService _transactionService;
    private readonly WalletManager _walletManager;
    private readonly BalanceService _balanceService;
    private readonly Wallet _aliceWallet;
    private readonly Wallet _bobWallet;
    private readonly Wallet _tomWallet;

    public BlockChainTests() {
        _chain = new BlockChain(
            difficulty: 1,
            singleThreaded: true,
            useGpu: false);
        _transactionService = new TransactionService();
        _walletManager = new WalletManager();
        _balanceService = new BalanceService();

        _aliceWallet = _walletManager.CreateWallet("Alice").Value;
        _bobWallet = _walletManager.CreateWallet("Bob").Value;
        _tomWallet = _walletManager.CreateWallet("Tom").Value;
    }

    [Fact]
    public void NewChain_IsValid() {
        Assert.Single(_chain.Chain);
        Assert.True(_chain.IsChainValid());
    }

    [Fact]
    public void AddingBlock_KeepsChainValid() {
        // Fund Alice first: mining rejects transactions from addresses with no balance
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);
        Assert.True(_chain.AddTransactions([_transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10)]).IsSuccess);
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);

        Assert.Equal(50, _balanceService.GetBalance(_tomWallet.Address, _chain.Chain)); // mining reward
        Assert.Equal(3, _chain.Chain.Count);
        Assert.Equal(_chain.Chain[1].Hash, _chain.Chain[2].PreviousHash);
        Assert.True(_chain.IsChainValid());
    }

    [Fact]
    public void ModifyingBlockData_InvalidatesChain() {
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);
        Assert.True(_chain.AddTransactions([_transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10)]).IsSuccess);
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);

        Assert.Equal(50, _balanceService.GetBalance(_tomWallet.Address, _chain.Chain)); // mining reward

        Assert.True(_chain.IsChainValid());

        _chain.Chain[2].Transactions[0].Amount = 999;

        Assert.False(_chain.IsChainValid());
        Assert.Equal((2, BlockValidationError.Hash), _chain.FindFirstInvalidBlock());
    }

    [Fact]
    public void ModifyingPreviousHash_InvalidatesChain() {
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);
        Assert.True(_chain.AddTransactions([_transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10)]).IsSuccess);
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);

        Assert.Equal(50, _balanceService.GetBalance(_tomWallet.Address, _chain.Chain)); // mining reward

        Assert.True(_chain.IsChainValid());

        _chain.Chain[2].PreviousHash = new string('f', 64);

        Assert.False(_chain.IsChainValid());
        Assert.Equal((2, BlockValidationError.Hash), _chain.FindFirstInvalidBlock());
    }

    [Fact]
    public void ModifyingBlockAndReMiningIt_InvalidatesChain() {
        // Fund Alice and Bob first: mining rejects transactions from addresses with no balance
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);
        Assert.True(_chain.MinePending(_bobWallet.Address).IsSuccess);
        Assert.True(_chain.AddTransactions([_transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10)]).IsSuccess);
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);
        Assert.True(_chain.AddTransactions([_transactionService.CreateTransaction(_bobWallet, _aliceWallet.Address, 7)]).IsSuccess);
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);
        Assert.True(_chain.AddTransactions([_transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 19)]).IsSuccess);
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);
        Assert.True(_chain.AddTransactions([_transactionService.CreateTransaction(_bobWallet, _aliceWallet.Address, 23)]).IsSuccess);
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);

        Assert.Equal(150, _balanceService.GetBalance(_tomWallet.Address, _chain.Chain)); // mining reward

        Assert.True(_chain.IsChainValid());

        _chain.Chain[3].Transactions[0].Amount = 70;

        Assert.False(_chain.IsChainValid());
        Assert.Equal((3, BlockValidationError.Hash), _chain.FindFirstInvalidBlock()); // detects hash change

        var ms = new MiningService(singleThreaded: true, useGpu: false);

        _chain.Chain[3].Nonce = 0; // set nonce to 0, or else the miner will fail
        ms.MineBlock(_chain.Chain[3], _chain.Chain[3].DifficultyAtMining);

        Assert.False(_chain.IsChainValid());
        Assert.Equal((3, BlockValidationError.InvalidTransactions), _chain.FindFirstInvalidBlock()); // hash of the block is correct, but the previousHash of the block after it reveals the truth

        Console.WriteLine("Repairing BlockChain");
        var repairResult = _chain.RepairChain(3);
        Assert.Equal(4, repairResult); // four blocks were rehashed during reparation process
        Console.WriteLine("BlockChain repaired");

        Assert.True(_chain.IsChainValid());
    }


    // [Fact]
    // public void InvalidTransactionSender_RejectsBlock() {
    //     var invalid = _transactionService.CreateTransaction(_tomWallet, _aliceWallet.Address, 37);
    //     var valid = _transactionService.CreateTransaction(_tomWallet, _aliceWallet.Address, 92);
    //
    //     Assert.True(_chain.AddTransactions([invalid, valid]).IsFailure);
    //
    //     invalid.From = ""; // mutate after adding to bypass mempool validation and exercise block validation
    //
    //     var result = _chain.MinePending(_tomWallet.Address);
    //
    //     Assert.Equal(50, _balanceService.GetBalance(_tomWallet.Address, _chain.Chain)); // mining reward
    //
    //     Assert.True(result.IsSuccess);
    //     Assert.Single(_chain.Chain);
    // }

    [Fact]
    public void TransactionWithDuplicateId_RejectsBlock() {
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);
        Assert.True(_chain.MinePending(_tomWallet.Address).IsSuccess);
        Assert.Equal(150, _balanceService.GetBalance(_tomWallet.Address, _chain.Chain)); // mining reward

        var first = _transactionService.CreateTransaction(_tomWallet, _aliceWallet.Address, 37);
        var second = _transactionService.CreateTransaction(_tomWallet, _aliceWallet.Address, 92);

        Assert.True(_chain.AddTransactions([first, second]).IsSuccess);

        second.Id = first.Id; // mutate after adding to bypass mempool duplicate validation
        second.Singature = _tomWallet.Sign(second.GetDataToSign());

        var result = _chain.MinePending(_tomWallet.Address);

        Assert.Equal(150, _balanceService.GetBalance(_tomWallet.Address, _chain.Chain)); // mining reward

        Assert.Equal((0, TransactionValidationError.DuplicateId), result.Error);
        Assert.Equal(4, _chain.Chain.Count);
    }
}
