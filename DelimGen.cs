#region Licence
/*
    Babbacombe SockLib
    https://github.com/trevorprinn/SockLib
    Copyright © 2017 Babbacombe Computers Ltd.

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301
    USA
 */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {

    /// <summary>
    /// Base class for classes that generate delimiters used to separate messages and items in messages.
    /// Note that a delimiter must never include \n characters.
    /// </summary>
    public abstract class BaseDelimGen {
        /// <summary>
        /// Creates a delimiter.
        /// </summary>
        /// <returns></returns>
        public abstract byte[] MakeDelimiter();
    }

    /// <summary>
    /// A delimiter generator for use when debugging the messages.
    /// </summary>
    public class DebugDelimGen : BaseDelimGen {

        /// <summary>
        /// Generates a delimiter of 29 dashes followed by a Guid.
        /// This is easy to see when debugging or using something like wireshark.
        /// </summary>
        /// <returns></returns>
        public override byte[] MakeDelimiter() {
            return (new string('-', 29) + Guid.NewGuid().ToString()).ConvertToBytes();
        }
    }

    /// <summary>
    /// A generator for random delimiters of slightly random length. This or a similar
    /// class should be used when the traffic is encrypted.
    /// </summary>
    public class RandomDelimGen : BaseDelimGen {

        /// <summary>
        /// Gets or sets the maximum length of the generated delimiters. Defaults to 64 bytes.
        /// </summary>
        public int MaxLength { get; set; }

        /// <summary>
        /// Creates a random delimiter generator.
        /// </summary>
        /// <param name="maxLength">The maximum length of the generated delimiters. Defaults to 64 bytes.</param>
        public RandomDelimGen(int maxLength = 64) {
            MaxLength = maxLength;
        }

        /// <summary>
        /// Generates a random delimiter of up to MaxLength bytes. The delimiter is of a random length, normally
        /// up to 10 bytes shorter than the maximum.
        /// </summary>
        /// <remarks>
        /// Some delimiters could be slightly shorter again as \n characters are
        /// removed from them.
        /// </remarks>
        /// <returns></returns>
        public override byte[] MakeDelimiter() {
            var rand = new Random();
            int length = rand.Next(MaxLength - 10, MaxLength);
            var adelim = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(adelim);
            }
            var delim = adelim.ToList();
            delim.RemoveAll(b => b == '\n');
            return delim.ToArray();
        }
    }
}
