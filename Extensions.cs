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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {
    internal static class InternalExtensions {

        // Taken from http://blogs.msdn.com/b/pfxteam/archive/2012/10/05/how-do-i-cancel-non-cancelable-async-operations.aspx
        public static async Task<T> WithCancellation<T>(
            this Task<T> task, CancellationToken cancellationToken) {
			task.GrabExceptions();
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(
                        s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);
            return await task;
        }

        public static async Task<UdpReceiveResult?> ReceiveAsync(this UdpClient client, CancellationToken cancel) {
            var read = client.ReceiveAsync();
            try {
                return await read.WithCancellation(cancel);
            } catch (OperationCanceledException) { 
			}
            return null;
        }

        public static async Task SendAsync(this UdpClient client, byte[] data, IPEndPoint target, CancellationToken cancel) {
            var write = client.SendAsync(data, data.Length, target);
            try {
                await write.WithCancellation(cancel);
            } catch (OperationCanceledException) { }
        }

		public static void GrabExceptions(this Task task) {
			task.ContinueWith(t => {
				var aggException = t.Exception.Flatten();
				foreach (var exception in aggException.InnerExceptions) System.Diagnostics.Debug.WriteLine("Grabbed Exception: " + exception.Message);
			}, 
				TaskContinuationOptions.OnlyOnFaulted);
		}
    }

    static class DebugExtensions {
        public static string ToLiteral(this string input, bool addQuotes = true) {
            var literal = new StringBuilder(input.Length + 2);
            if (addQuotes) literal.Append("\"");
            foreach (var c in input) {
                switch (c) {
                    case '\'': literal.Append(@"\'"); break;
                    case '\"': literal.Append("\\\""); break;
                    case '\\': literal.Append(@"\\"); break;
                    case '\0': literal.Append(@"\0"); break;
                    case '\a': literal.Append(@"[bel]"); break;
                    case '\b': literal.Append(@"\b"); break;
                    case '\f': literal.Append(@"\f"); break;
                    case '\n': literal.Append(@"\n"); break;
                    case '\r': literal.Append(@"\r"); break;
                    case '\t': literal.Append(@"\t"); break;
                    case '\v': literal.Append(@"\v"); break;
                    case '[': literal.Append(@"\["); break;
                    case (char)2: literal.Append("[stx]"); break;
                    case (char)3: literal.Append("[etx]"); break;
                    case (char)0x7f: literal.Append("[del]"); break;
                    default:
                        if (c >= ' ' && c < (char)0x7f) {
                            literal.Append(c);
                        } else {
                            literal.AppendFormat("[{0:x4}]", (ushort)c);
                        }
                        break;
                }
            }
            if (addQuotes) literal.Append("\"");
            return literal.ToString();
        }

        public static byte[] AsBytes(this string s) {
            return s.ToCharArray().Select(c => (byte)c).ToArray();
        }

        public static string AsString(this IEnumerable<byte> bytes) {
            return new string(bytes.Select(b => (char)b).ToArray());
        }

    }

}
