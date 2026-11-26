namespace First.Tests;

public class MerkleProofTests {
    private static (BlockChain chain, List<Transaction> transactions) CreateChainWithTransactions() {
        var chain = new BlockChain(
            difficulty: 1,
            singleThreaded: true,
            useGpu: false);
        var walletManager = new WalletManager();
        var transactionService = new TransactionService();

        var alice = walletManager.CreateWallet("Alice").Value;
        var bob = walletManager.CreateWallet("Bob").Value;
        var tom = walletManager.CreateWallet("Tom").Value;

        Assert.True(chain.MinePending(alice.Address).IsSuccess);

        var transactions = new List<Transaction> {
            transactionService.CreateTransaction(alice, bob.Address, 1),
            transactionService.CreateTransaction(alice, tom.Address, 2),
            transactionService.CreateTransaction(alice, bob.Address, 3),
        };

        Assert.True(chain.AddTransactions(transactions).IsSuccess);
        Assert.True(chain.MinePending(tom.Address).IsSuccess);

        return (chain, transactions);
    }

    [Fact]
    public void MerkleProof_ValidTransaction_Verifies() {
        var (chain, transactions) = CreateChainWithTransactions();
        var transaction = transactions[1];

        var proof = chain.GetMerkleProof(transaction.Id);

        Assert.True(proof.IsSuccess);
        Assert.Single(proof.Value.Steps);
        Assert.True(chain.VerifyMerkleProof(transaction, proof.Value));
    }

    [Fact]
    public void MerkleProof_ModifiedTransaction_Fails() {
        var (chain, transactions) = CreateChainWithTransactions();
        var transaction = transactions[1];
        var proof = chain.GetMerkleProof(transaction.Id).Value;

        Assert.True(chain.VerifyMerkleProof(transaction, proof));

        transaction.Amount += 1;

        Assert.False(chain.VerifyMerkleProof(transaction, proof));
    }

    [Fact]
    public void MerkleProof_UnknownTransaction_ReturnsFailure() {
        var (chain, _) = CreateChainWithTransactions();

        var proof = chain.GetMerkleProof("missing-transaction-id");

        Assert.True(proof.IsFailure);
    }
}
