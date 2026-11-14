namespace First.Tests;

public class BlockChainTests {
    private readonly BlockChain _chain;
    private readonly TransactionService _transactionService;

    public BlockChainTests() {
        _chain = new BlockChain(
            difficulty: 1,
            singleThreaded: true,
            useGpu: false);
        _transactionService = new TransactionService();
    }

    [Fact]
    public void NewChain_IsValid() {
        Assert.Single(_chain.Chain);
        Assert.True(_chain.IsChainValid());
    }

    [Fact]
    public void AddingBlock_KeepsChainValid() {
        _chain.AddBlock([_transactionService.CreateTransaction("alice", "bob", 10)]);

        Assert.Equal(2, _chain.Chain.Count);
        Assert.Equal(_chain.Chain[0].Hash, _chain.Chain[1].PreviousHash);
        Assert.True(_chain.IsChainValid());
    }

    [Fact]
    public void ModifyingBlockData_InvalidatesChain() {
        _chain.AddBlock([_transactionService.CreateTransaction("alice", "bob", 10)]);
        Assert.True(_chain.IsChainValid());

        _chain.Chain[1].Transactions[0].Amount = 999;

        Assert.False(_chain.IsChainValid());
        Assert.Equal((1, BlockInvalidityReason.Hash), _chain.FindFirstInvalidBlock());
    }

    [Fact]
    public void ModifyingPreviousHash_InvalidatesChain() {
        _chain.AddBlock([_transactionService.CreateTransaction("alice", "bob", 10)]);
        Assert.True(_chain.IsChainValid());

        _chain.Chain[1].PreviousHash = new string('f', 64);

        Assert.False(_chain.IsChainValid());
        Assert.Equal((1, BlockInvalidityReason.Hash), _chain.FindFirstInvalidBlock());
    }

    [Fact]
    public void ModifyingBlockAndReMiningIt_InvalidatesChain() {
        _chain.AddBlock([_transactionService.CreateTransaction("alice", "bob", 10)]);
        _chain.AddBlock([_transactionService.CreateTransaction("bob", "alice", 7)]);
        _chain.AddBlock([_transactionService.CreateTransaction("alice", "bob", 19)]);
        _chain.AddBlock([_transactionService.CreateTransaction("bob", "alice", 23)]);
        Assert.True(_chain.IsChainValid());

        _chain.Chain[2].Transactions[0].Amount = 70;

        Assert.False(_chain.IsChainValid());
        Assert.Equal((2, BlockInvalidityReason.Hash), _chain.FindFirstInvalidBlock()); // detects hash change

        var ms = new MiningService(singleThreaded: true, useGpu: false);

        _chain.Chain[2].Nonce = 0; // set nonce to 0, or else the miner will fail
        ms.MineBlock(_chain.Chain[2], _chain.Chain[2].DifficultyAtMining);

        Assert.False(_chain.IsChainValid());
        Assert.Equal((3, BlockInvalidityReason.PreviousHash), _chain.FindFirstInvalidBlock()); // hash of the block is correct, but the previousHash of the block after it reveals the truth

        Console.WriteLine("Repairing BlockChain");
        Assert.Equal(2, _chain.RepairChain(2)); // two blocks were rehashed during reparation process
        Console.WriteLine("BlockChain repaired");

        Assert.True(_chain.IsChainValid());
    }

}
