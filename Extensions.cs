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
            } catch (OperationCanceledException) { }
            return null;
        }

        public static async Task SendAsync(this UdpClient client, byte[] data, IPEndPoint target, CancellationToken cancel) {
            var write = client.SendAsync(data, data.Length, target);
            try {
                await write.WithCancellation(cancel);
            } catch (OperationCanceledException) { }
        }
    }
}
