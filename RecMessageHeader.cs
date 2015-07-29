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

    public sealed class RecMessageHeader {
        public MessageTypes Type { get; private set; }
        public string Id { get; private set; }
        public string Command { get; private set; }
        public bool IsEmpty { get; private set; }
        
        private RecMessageHeader() { }

#if TEST
        public RecMessageHeader(Stream stream) {
#else
        internal RecMessageHeader(Stream stream) {
#endif
			string line1 = null;
			try {
				line1 = readLine(stream);
			} catch (IOException) { }
            IsEmpty = string.IsNullOrEmpty(line1);
            if (IsEmpty) return;
            Type = getMessageType(line1[0]);
            if (line1.Length > 1) Id = line1.Substring(1);
            Command = readLine(stream);
        }

        private string readLine(Stream s) {
            var line = new StringBuilder();
            var ch = s.ReadByte();
            while (ch >= 0 && ch != '\n') {
                line.Append((char)ch);
                ch = s.ReadByte();
            }
            if (ch < 0) return null;
            while (line.Length > 0 && line[line.Length - 1] == '\r') line.Length--;
            return line.ToString();
        }

        private MessageTypes getMessageType(char t) {
            if (!"TSUXBFM".Contains(t)) throw new UnknownMessageTypeException(t);
            return ((MessageTypes[])Enum.GetValues(typeof(MessageTypes))).Single(mt => mt.ToString()[0] == t);
        }
    }
}
