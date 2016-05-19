//
// System.Net.NetworkInformation.IPGlobalProperties
//
// Authors:
//	Gonzalo Paniagua Javier (gonzalo@novell.com)
//	Atsushi Enomoto (atsushi@ximian.com)
//	Marek Safar (marek.safar@gmail.com)
//
// Copyright (c) 2006-2007 Novell, Inc. (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

/* Xamarin.Android returns a UnixIPGlobalProperties object for IPGlobalProperties.GetIPGlobalProperties, which
 * doesn't have much implemented in it, so accessing it throws a not implemented exception.
 * This code is extracted from https://github.com/mono/mono/blob/master/mcs/class/System/System.Net.NetworkInformation/IPGlobalProperties.cs
 */

namespace System.Net.NetworkInformation {
    // It expects /proc/net/snmp (or /usr/compat/linux/proc/net/snmp),
    // formatted like:
    // http://www.linuxdevcenter.com/linux/2000/11/16/example5.html
    // http://www.linuxdevcenter.com/linux/2000/11/16/example2.html
    class DroidIPGlobalProperties {
        public const string ProcDir = "/proc";
        public const string CompatProcDir = "/usr/compat/linux/proc";

        public readonly string StatisticsFile, StatisticsFileIPv6, TcpFile, Tcp6File, UdpFile, Udp6File;

        public DroidIPGlobalProperties(string procDir = ProcDir) {
            StatisticsFile = Path.Combine(procDir, "net/snmp");
            StatisticsFileIPv6 = Path.Combine(procDir, "net/snmp6");
            TcpFile = Path.Combine(procDir, "net/tcp");
            Tcp6File = Path.Combine(procDir, "net/tcp6");
            UdpFile = Path.Combine(procDir, "net/udp");
            Udp6File = Path.Combine(procDir, "net/udp6");
        }

        static readonly char[] wsChars = new char[] { ' ', '\t' };

        Exception CreateException(string file, string msg) {
            return new InvalidOperationException(String.Format("Unsupported (unexpected) '{0}' file format. ", file) + msg);
        }
        IPEndPoint[] GetLocalAddresses(List<string[]> list) {
            IPEndPoint[] ret = new IPEndPoint[list.Count];
            for (int i = 0; i < ret.Length; i++)
                ret[i] = ToEndpoint(list[i][1]);
            return ret;
        }

        IPEndPoint ToEndpoint(string s) {
            int idx = s.IndexOf(':');
            int port = int.Parse(s.Substring(idx + 1), NumberStyles.HexNumber);
            if (s.Length == 13)
                return new IPEndPoint(long.Parse(s.Substring(0, idx), NumberStyles.HexNumber), port);
            else {
                byte[] bytes = new byte[16];
                for (int i = 0; (i << 1) < idx; i++)
                    bytes[i] = byte.Parse(s.Substring(i << 1, 2), NumberStyles.HexNumber);
                return new IPEndPoint(new IPAddress(bytes), port);
            }
        }

        void GetRows(string file, List<string[]> list) {
            if (!File.Exists(file))
                return;
            using (StreamReader sr = new StreamReader(file, Encoding.ASCII)) {
                sr.ReadLine(); // skip first line
                while (!sr.EndOfStream) {
                    string[] item = sr.ReadLine().Split(wsChars, StringSplitOptions.RemoveEmptyEntries);
                    if (item.Length < 4)
                        throw CreateException(file, null);
                    list.Add(item);
                }
            }
        }

        public TcpConnectionInformation[] GetActiveTcpConnections() {
            List<string[]> list = new List<string[]>();
            GetRows(TcpFile, list);
            GetRows(Tcp6File, list);

            TcpConnectionInformation[] ret = new TcpConnectionInformation[list.Count];
            for (int i = 0; i < ret.Length; i++) {
                // sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode
                IPEndPoint local = ToEndpoint(list[i][1]);
                IPEndPoint remote = ToEndpoint(list[i][2]);
                TcpState state = (TcpState)int.Parse(list[i][3], NumberStyles.HexNumber);
                ret[i] = new SystemTcpConnectionInformation(local, remote, state);
            }
            return ret;
        }

        private class SystemTcpConnectionInformation : TcpConnectionInformation {
            public override IPEndPoint LocalEndPoint { get; }
            public override IPEndPoint RemoteEndPoint { get; }
            public override TcpState State { get; }

            public SystemTcpConnectionInformation(IPEndPoint local, IPEndPoint remote, TcpState state) {
                LocalEndPoint = local;
                RemoteEndPoint = remote;
                State = state;
            }
        }
    }
}
