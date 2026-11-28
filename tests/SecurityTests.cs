using static First.TransactionService;

namespace First.Tests;

public class SecurityTests {
    private readonly BlockChain _chain;
    private readonly TransactionService _transactionService;
    private readonly WalletManager _walletManager;
    private readonly BalanceService _balanceService;
    private readonly Wallet _aliceWallet;
    private readonly Wallet _bobWallet;
    private readonly Wallet _tomWallet;

    public SecurityTests() {
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
    public void EmptyWalletSendingMoney_GetsRejected() {
        var transaction = _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 15);
        Assert.StartsWith("Insufficient balance for transaction", _chain.AddTransaction(transaction).Error);
    }


    [Fact]
    public void WalletWithNotEnoughMoney_GetsRejected() {
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);

        List<Transaction> transactions = [
            _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 15),
            _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 15),
            _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 15),
            _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 15),
            _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 15),
        ];

        var err = _chain.AddTransactions(transactions).Error;
        Assert.StartsWith("Insufficient balance for transaction", err.reason);
        Assert.Equal(3, err.index);
    }


    [Fact]
    public void CreatingSyntheticCoinbaseTransaction_GetsRejected() {
        var coinTransaction = Transaction.CreateCoinbaseTransaction(_aliceWallet.Address, 50);
        Assert.Equal("Invalid Coinbase usage", _chain.AddTransaction(coinTransaction).Error);
    }

    [Fact]
    public void SigningTransactionWithNonSendersKey_GetsRejected() {
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);

        var transaction = new Transaction(_aliceWallet.Address, _bobWallet.Address, 15);
        transaction.SenderPublicKey = _bobWallet.PublicKey;
        transaction.Singature = _bobWallet.Sign(transaction.GetDataToSign());

        Assert.Equal("InvalidTransaction", _chain.AddTransaction(transaction).Error);
    }

    [Fact]
    public void SendingDuplicateTransaction_GetsRejected() {
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);

        var transaction = _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 15);

        Assert.True(_chain.AddTransaction(transaction).IsSuccess);
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);

        Assert.Equal("DuplicateId", _chain.AddTransaction(transaction).Error);
    }

    [Fact]
    public void ChangingAmountAfterSigning_GetsRejected() {
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);

        var transaction = _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10);
        transaction.Amount = 11;

        Assert.Equal(TransactionValidationError.NonMatchingSignature, _transactionService.ValidateTransaction(transaction).Error);
        Assert.True(_chain.AddTransaction(transaction).IsFailure);
        Assert.Empty(_chain.Mempool.PendingTransactions);
    }

    [Fact]
    public void ChangingRecipientAfterSigning_GetsRejected() {
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);

        var transaction = _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10);
        transaction.To = _tomWallet.Address;

        Assert.Equal(TransactionValidationError.NonMatchingSignature, _transactionService.ValidateTransaction(transaction).Error);
        Assert.True(_chain.AddTransaction(transaction).IsFailure);
        Assert.Empty(_chain.Mempool.PendingTransactions);
    }

    [Fact]
    public void MissingSignature_GetsRejected() {
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);

        var transaction = _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10);
        transaction.Singature = null;

        Assert.Equal(TransactionValidationError.EmptySignatureOrSenderPublicKey, _transactionService.ValidateTransaction(transaction).Error);
        Assert.True(_chain.AddTransaction(transaction).IsFailure);
        Assert.Empty(_chain.Mempool.PendingTransactions);
    }

    [Fact]
    public void GenuineOwnerAccepted_ForgedOwnerRejected() {
        Assert.True(_chain.MinePending(_aliceWallet.Address).IsSuccess);

        var genuine = _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10);

        var forged = new Transaction(_aliceWallet.Address, _bobWallet.Address, 10) {
            SenderPublicKey = _bobWallet.PublicKey
        };
        forged.Singature = _bobWallet.Sign(forged.GetDataToSign());

        Assert.True(_transactionService.ValidateTransaction(genuine).IsSuccess);
        Assert.Equal(TransactionValidationError.PublicKeyNotMatchingSender, _transactionService.ValidateTransaction(forged).Error);

        Assert.True(_chain.AddTransaction(genuine).IsSuccess);
        Assert.True(_chain.AddTransaction(forged).IsFailure);
        Assert.Single(_chain.Mempool.PendingTransactions);
    }

    [Fact]
    public void VerificationReport_CountsAcceptedAndRejected() {
        var verifier = new TransactionVerificationService();

        var valid = _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10);
        var invalid = _transactionService.CreateTransaction(_aliceWallet, _bobWallet.Address, 10);
        invalid.Amount = 999;

        Assert.True(verifier.Verify(valid).IsSuccess);
        Assert.True(verifier.Verify(invalid).IsFailure);

        var report = verifier.GetReport();
        Assert.Equal(2, report.Total);
        Assert.Equal(1, report.Accepted);
        Assert.Equal(1, report.Rejected);
    }
}
