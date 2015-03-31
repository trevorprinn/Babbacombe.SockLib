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

        internal RecMessageHeader(Stream stream) {
            var line1 = readLine(stream);
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
            return ((MessageTypes[])Enum.GetValues(typeof(MessageTypes))).Single(mt => mt.ToString()[0] == t);
        }
    }
}
