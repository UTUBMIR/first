namespace First {
    public class Transaction {
        public string Id { get; set; }
        public string From { get; set; }
        public decimal Amount { get; set; }
        public string To { get; set; }

        public DateTime TimeStamp { get; set; }

        public Transaction(string from, string to, decimal amount) {
            Id = Guid.NewGuid().ToString();
            From = from;
            To = to;
            Amount = amount;
            TimeStamp = DateTime.Now;
        }

        public string ToRawString() {
            return $"{Id}\t{From}\t{To}\t{Amount}\t{TimeStamp}";
        }

        public override string ToString() {
            return $"Transaction ID: {Id,-20} From: {From,-15} To: {To,-15} Amount: {Amount,10:F2} Timestamp: {TimeStamp:g}";
        }
    }
}
