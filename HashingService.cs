using System.Security.Cryptography;
using System.Text;

namespace First {
    public class HashingService {
        public string ComputeHash(Block block) {
            var blockData = $"{block.Index}{block.TimeStamp}{block.Data}{block.Author}{block.PreviousHash}";
            return ComputeHash(blockData);
        }

        private string ComputeHash(string input) {
            using (var sha256 = SHA256.Create()) {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToHexStringLower(hashBytes);
            }
        }
    }
}
