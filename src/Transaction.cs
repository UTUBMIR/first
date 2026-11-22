using System.Text;

namespace First {
    public class Transaction {
        public string Id { get; set; }
        public string From { get; set; }
        public decimal Amount { get; set; }
        public string To { get; set; }

        public byte[] SenderPublicKey { get; set; }
        public byte[]? Singature { get; set; }

        public DateTime TimeStamp { get; set; }


        public bool IsCoinbase { get; set; }

        public Transaction(string from, string to, decimal amount) {
            Id = Guid.NewGuid().ToString();
            From = from;
            To = to;
            Amount = amount;
            TimeStamp = DateTime.Now;
        }

        public static Transaction CreateCoinbaseTransaction(string to, decimal amount) {
            return new Transaction("Coinbase", to, amount) { IsCoinbase = true };
        }

        public byte[] GetDataToSign() {
            var data = $"{From}{To}{Amount}{TimeStamp}{Id}";
            return Encoding.UTF8.GetBytes(data);
        }
        public string ToRawString() {
            return $"{Id}\t{From}\t{To}\t{Amount}\t{TimeStamp}";
        }

        public override string ToString() {
            return $"Transaction ID: {Id,-20} From: {From,-15} To: {To,-15} Amount: {Amount,10:F2} Timestamp: {TimeStamp:g}";
        }
    }
}
