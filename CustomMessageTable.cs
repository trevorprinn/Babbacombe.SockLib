#region Licence
/*
    Babbacombe SockLib
    https://github.com/trevorprinn/SockLib
    Copyright © 2015 Babbacombe Computers Ltd.

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
    public class CustomMessageTable : Dictionary<char, CustomMessageEntry> {

        public void Add(char messageTypeID, Type recMessage) {
            var e = new CustomMessageEntry(messageTypeID, recMessage);
            this[messageTypeID] = e;
        }

        internal RecMessage getMessage(RecMessageHeader header, Stream stream) {
            var t = ContainsKey(header.MessageType) ? this[header.MessageType].RecMessageType : null;
            if (t == null) return null;
            return (RecMessage)Activator.CreateInstance(t, new object[] { header, stream });
        }
    }

    public sealed class CustomMessageEntry {
        public char MessageTypeID { get; }
        public Type RecMessageType { get; }
        internal CustomMessageEntry(char messageTypeID, Type recMessageType) {
            if ("TSUXBFM".Contains(messageTypeID)) throw new ArgumentException($"Cannot override built in Message Type '{messageTypeID}'");
            if (recMessageType == null || !(recMessageType.IsSubclassOf(typeof(RecMessage)))) throw new ArgumentException("recMessageType must be a subclass of RecMessage");
            MessageTypeID = messageTypeID;
            RecMessageType = recMessageType;
        }
    }
}
