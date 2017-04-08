using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {

    public interface IDelimGen {
        byte[] MakeDelimiter();
    }

    public class DefaultDelimGen : IDelimGen {
        public byte[] MakeDelimiter() {
            return Encoding.UTF8.GetBytes(new string('-', 29) + Guid.NewGuid().ToString());
        }
    }

    public class RandomDelimGen : IDelimGen {
        public int MaxLength { get; set; }

        public RandomDelimGen(int maxLength = 64) {
            MaxLength = maxLength;
        }

        public byte[] MakeDelimiter() {
            var adelim = new byte[64];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(adelim);
            }
            var delim = adelim.ToList();
            delim.RemoveAll(b => b.In((byte)0x0A, (byte)0x0D));
            return delim.ToArray();
        }
    }
}
