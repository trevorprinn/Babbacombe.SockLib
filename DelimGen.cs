using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {

    /// <summary>
    /// Base class for classes that generate delimiters used to separate messages and items in messages.
    /// Note that a delimiter must never include \r or \n characters.
    /// </summary>
    public abstract class BaseDelimGen {
        /// <summary>
        /// Creates a delimiter.
        /// </summary>
        /// <returns></returns>
        public abstract byte[] MakeDelimiter();
    }

    /// <summary>
    /// The default delimiter generator. Generates a delimiter of 29 dashes followed by a Guid.
    /// This is easy to see when debugging or using something like wireshark.
    /// </summary>
    public class DefaultDelimGen : BaseDelimGen {
        public override byte[] MakeDelimiter() {
            return (new string('-', 29) + Guid.NewGuid().ToString()).ConvertToBytes();
        }
    }

    /// <summary>
    /// A generator for random delimiters of slightly random length. This or a similar
    /// class should be used when the traffic is encrypted.
    /// </summary>
    public class RandomDelimGen : BaseDelimGen {
        public int MaxLength { get; set; }

        public RandomDelimGen(int maxLength = 64) {
            MaxLength = maxLength;
        }

        public override byte[] MakeDelimiter() {
            var rand = new Random();
            int length = rand.Next(MaxLength - 10, MaxLength);
            var adelim = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(adelim);
            }
            var delim = adelim.ToList();
            delim.RemoveAll(b => b.In((byte)'\r', (byte)'\n'));
            return delim.ToArray();
        }
    }
}
