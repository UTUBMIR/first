namespace First {
    public class Block {
        public int Index { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Data { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string PreviousHash { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
    }
}
