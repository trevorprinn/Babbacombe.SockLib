#region Licence
/*
    Babbacombe SockLib
    https://github.com/trevorprinn/SockLib
    Copyright © 2015-2017 Babbacombe Computers Ltd.

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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {

    /// <summary>
    /// The table storing the Custom Message types that have been set up.
    /// </summary>
    public sealed class CustomMessageTable : Dictionary<char, CustomMessageEntry> {

        /// <summary>
        /// Adds a custom message type to the table.
        /// </summary>
        /// <param name="messageType">The character identifier for the Message Type</param>
        /// <param name="recMessage">The Type of the RecMessage object to be created for this type of message</param>
        /// <exception cref="ArgumentException">
        /// Raised if the message type is one of the built in types, or if recMessage is not a subclass of RecMessage.
        /// </exception>
        public void Add(char messageType, Type recMessage) {
            var e = new CustomMessageEntry(messageType, recMessage);
            this[messageType] = e;
        }

        internal RecMessage getMessage(RecMessageHeader header, Stream stream) {
            var t = ContainsKey(header.MessageType) ? this[header.MessageType].RecMessageType : null;
            if (t == null) return null;
            return (RecMessage)Activator.CreateInstance(t, new object[] { header, stream });
        }
    }

    /// <summary>
    /// An entry in the Custom Message table
    /// </summary>
    public class CustomMessageEntry {
        /// <summary>
        /// The character identifier for the Message Type
        /// </summary>
        public char MessageType { get; }

        /// <summary>
        /// The Type of the RecMessage object to be created for this type of message.
        /// </summary>
        public Type RecMessageType { get; }

        /// <summary>
        /// Creates a custom message type.
        /// </summary>
        /// <param name="messageType">The character identifier for the message type</param>
        /// <param name="recMessageType">The type of the RecMessage object to be created for this type of message</param>
        /// <remarks>
        /// This has to be added to the table before it can be used. It is easier to Add directly to
        /// the table, rather than construct a CustomMessageEntry and then add it.
        /// </remarks>
        public CustomMessageEntry(char messageType, Type recMessageType) {
            if ("TSUXBFM@".Contains(messageType)) throw new ArgumentException($"Cannot override built in Message Type '{messageType}'");
            if (recMessageType == null || !(recMessageType.IsSubclassOf(typeof(RecMessage)))) throw new ArgumentException("recMessageType must be a subclass of RecMessage");
            MessageType = messageType;
            RecMessageType = recMessageType;
        }
    }
}
