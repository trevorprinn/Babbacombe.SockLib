using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {

    /// <summary>
    /// Interface for classes that generate delimiters used to separate messages and items in messages.
    /// </summary>
    public interface IDelimGen {
        /// <summary>
        /// Creates a delimiter.
        /// </summary>
        /// <returns></returns>
        byte[] MakeDelimiter();
    }

    /// <summary>
    /// The default delimiter generator. Generates a delimiter of 29 dashes followed by a Guid.
    /// This is easy to see when debugging or using something like wireshark.
    /// </summary>
    public class DefaultDelimGen : IDelimGen {
        public byte[] MakeDelimiter() {
            return (new string('-', 29) + Guid.NewGuid().ToString()).ConvertToBytes();
        }
    }

    /// <summary>
    /// A generator for random delimiters of slightly random length. This or a similar
    /// class should be used when the traffic is encrypted.
    /// </summary>
    public class RandomDelimGen : IDelimGen {
        public int MaxLength { get; set; }

        public RandomDelimGen(int maxLength = 64) {
            MaxLength = maxLength;
        }

        public byte[] MakeDelimiter() {
            var rand = new Random();
            int length = rand.Next(MaxLength - 10, MaxLength);
            var adelim = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(adelim);
            }
            var delim = adelim.ToList();
            delim.RemoveAll(b => b.In((byte)0x0A, (byte)0x0D));
            return delim.ToArray();
        }
    }
}
