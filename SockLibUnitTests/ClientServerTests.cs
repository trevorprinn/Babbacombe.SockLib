using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if DEVICE
using NUnit.Framework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

using Babbacombe.SockLib;

namespace SockLibUnitTests {
#if DEVICE
    [TestFixture]
#else
    [TestClass]
#endif
    public class ClientServerTests {

        private static bool isDevice
#if DEVICE
            => true;
#else
            => false;
#endif

        /// <summary>
        /// Test a single transaction works with a single client.
        /// </summary>
#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        public void OneTextTransaction() {
            using (Server server = new Server(9000)) 
            using (Client client = new Client("localhost", 9000)) {
                server.Handlers.Add<RecTextMessage>("Test", echoText);

                Assert.IsFalse(client.IsOpen, "IsOpen");
                client.Open();
                Assert.IsTrue(client.IsOpen, "IsOpen");

                Thread.Sleep(500);
                Assert.AreEqual(1, server.Clients.Count(), "Should be one client open");

                var reply = client.Transaction(new SendTextMessage("Test", "abcde"));
#if DEVICE
                Assert.That(reply, Is.InstanceOf<RecTextMessage>());
#else
                Assert.IsInstanceOfType(reply, typeof(RecTextMessage));
#endif
                Assert.AreEqual("Test", reply.Command);
                Assert.AreEqual("abcde", ((RecTextMessage)reply).Text);

                client.Close();
                Assert.IsFalse(client.IsOpen, "ClientClosed");

                Thread.Sleep(100);
                Assert.AreEqual(0, server.Clients.Count(), "Should be no server clients open");
            }
        }

        private SendMessage echoText(ServerClient c, RecTextMessage r) {
#if DEVICE
            Assert.That(r, Is.InstanceOf<RecTextMessage>());
#else
            Assert.IsInstanceOfType(r, typeof(RecTextMessage));
#endif

            return new SendTextMessage(r.Command, r.Text);
        }

        /// <summary>
        /// Tests multiple clients sending multiple transactions get back the correct replies.
        /// </summary>
#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(60000)]
        public void MultipleClientTextTransactions() {
#if DEVICE
            const int clientCount = 5;
            const int transCount = 10;
#else
            const int clientCount = 25;
            const int transCount = 25;
#endif
            using (Server server = new Server(9000)) {
                server.Handlers.Add<RecTextMessage>("Test", echoText);

                List<Client> clients = new List<Client>();
                try {
                    for (int i = 0; i < clientCount; i++) {
                        var client = new Client("localhost", 9000);
                        client.Open();
                        clients.Add(client);
                    }
                    Parallel.ForEach(clients, client => {
                        int cno = clients.IndexOf(client);
                        Parallel.For(0, transCount, tno => {
                            string text = string.Format("Client: {0}, Transaction {1}", cno, tno);
                            System.Diagnostics.Debug.WriteLine($"Send {text}");
                            var reply = (RecTextMessage)client.Transaction(new SendTextMessage("Test", text));
                            System.Diagnostics.Debug.WriteLine($"Repl {text}");
                            Assert.AreEqual(text, reply.Text);
                        });
                    });
                } finally {
                    clients.ForEach(c => c.Dispose());
                }
            }
        }

        /// <summary>
        /// Tests multiple clients sending multiple transactions ansyncronously to get back the correct replies.
        /// </summary>
#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(60000)]
        public void MultipleClientTextTransactionsAsync() {
#if DEVICE
            const int clientCount = 5;
            const int transCount = 10;
#else
            const int clientCount = 25;
            const int transCount = 25;
#endif
            using (Server server = new Server(9000)) {
                server.Handlers.Add<RecTextMessage>("Test", echoText);

                List<Client> clients = new List<Client>();
                try {
                    for (int i = 0; i < clientCount; i++) {
                        var client = new Client("localhost", 9000);
                        client.Open();
                        clients.Add(client);
                    }
                    Parallel.ForEach(clients, client => {
                        int cno = clients.IndexOf(client);
                        var tasks = Enumerable.Range(0, transCount).Select(async tno => {
                            string text = string.Format("Client: {0}, Transaction {1}", cno, tno);
                            System.Diagnostics.Debug.WriteLine($"Send {text}");
                            var reply = await client.TransactionAsync<RecTextMessage>(new SendTextMessage("Test", text));
                            System.Diagnostics.Debug.WriteLine($"Repl {text}");
                            Assert.AreEqual(text, reply?.Text);
                        }).ToArray();
                        Task.WaitAll(tasks);
                    });
                } finally {
                    clients.ForEach(c => c.Dispose());
                }
            }
        }

        /// <summary>
        /// Tests that the client is closed when the server closes.
        /// This test currently fails on iOS because it is unable to determine whether the connection is established.
        /// </summary>
#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        public void CloseServer() {
            Client client;
            using (Server server = new Server(9000)) {
                server.Handlers.Add<RecTextMessage>("Test", echoText);
                client = new Client("localhost", 9000);
                client.Open();

                client.Transaction(new SendTextMessage("Test", "abc"));
            }
            Assert.IsFalse(client.IsOpen);
            client.Dispose();
        }

        /// <summary>
        /// Tests a server can send a delayed response to a message from a client.
        /// </summary>
#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(10000)]
        public async Task SimpleListen() {
            using (Server server = new Server(9000))
            using (Client client = new Client("localhost", 9000, Client.Modes.Listening)) {
                server.Handlers.AddAsync<RecTextMessage>("Test", echoTextDelayed);
                client.Open();
                int msgCount = 0;

                client.MessageReceived += (s, e) => {
                    msgCount++;
                };

                client.SendMessage(new SendTextMessage("Test"));
                await Task.Delay(4000);
                Assert.IsTrue(client.IsOpen, "Client is closed");
                Assert.AreEqual(1, msgCount);
            }
        }

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        public void CloseClient() {
            using (Server server = new Server(9000)) {
                server.Handlers.AddAsync<RecTextMessage>("Test", echoTextDelayed);

                using (Client client = new Client("localhost", 9000, Client.Modes.Listening)) {
                    client.SendPings = false;
                    client.Open();
                    Thread.Sleep(500);
                    Assert.AreEqual(1, server.Clients.Count());
                    client.SendMessage(new SendTextMessage("Test"));
                }
                // Wait because the server won't attempt to send it back for 3 seconds.
                Thread.Sleep(4000);
                Assert.AreEqual(0, server.Clients.Count());
            }
        }

        private async Task echoTextDelayed(ServerClient c, RecTextMessage r) {
            await Task.Delay(3000);
            c.SendMessage(new SendTextMessage(r.Command, r.Text));
        }

        /// <summary>
        /// Sends shuffled selections of numbers from the server to a set of clients, and from each
        /// client to the server, and tests that the correct numbers have been received by each in the
        /// correct order.
        /// </summary>
#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(120000)]
        public async Task Listening() {
#if DEVICE
            const int clientCount = 2;
            const int serverMsgCount = 2;
            const int clientMsgCount = 2;
#else
            const int clientCount = 5;
            const int serverMsgCount = 50;
            const int clientMsgCount = 50;
#endif

            var clients = new List<TestListenClient>();
            using (var server = new TestListenServer(serverMsgCount)) {
                try {
                    for (int i = 0; i < clientCount; i++) {
                        clients.Add(new TestListenClient(i + 1, clientMsgCount));
                    }

                    // Wait for all the clients to get connected up, so that
                    // they all get broadcast to.
                    int cc;
                    while ((cc = server.Clients.Count()) < clientCount) {
                        System.Diagnostics.Debug.WriteLine($"server cc: {cc}");
                        Thread.Sleep(1000);
                    }
                    System.Diagnostics.Debug.WriteLine("Got all the clients");

                    List<Task> tasks = new List<Task>();

                    // Send loads of messages from the clients to the server.
                    foreach (var c in clients) tasks.Add(c.Exercise());
                    // Broadcast loads of messages from the server to the clients.
                    tasks.Add(server.Exercise());

                    // Wait until all the messages have been sent.
                    await Task.WhenAll(tasks);

                    // Wait for the last few messages to be received and processed.
                    await Task.Delay(2000);

                    foreach (var c in clients) {
                        // Check the client didn't drop out of listening mode due to an exception.
                        Assert.IsTrue(c.Mode == Client.Modes.Listening);
                        // Check the correct number of messages were received.
                        Assert.AreEqual(clientMsgCount, server.GetRecMessages(c.Ident).Count(), "Server received messages");
                        Assert.AreEqual(serverMsgCount, c.GetRecMessages().Count(), "Client {0} received messages", c.Ident);
                        // Check the correct messages were received at both ends in the correct order.
                        Assert.IsFalse(server.GetRecMessages(c.Ident).Zip(c.GetSentMessages(), (sm, rm) => sm == rm).Any(r => !r), "Server received messages");
                        Assert.IsFalse(c.GetRecMessages().Zip(server.GetSentMessages(), (sm, rm) => sm == rm).Any(r => !r), "Client received messages");
                    }
                } finally {
                    foreach (var c in clients) c.Dispose();
                }
            }
        }

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(90000)]
        public void TransferFiles() {
            transferFiles();
        }

        internal static void transferFiles(bool encrypt = false) {
#if ANDROID
            var filePath = "/storage/emulated/0/Android/data/Socklib.Android.UnitTests.Socklib.Android.UnitTests";
#endif
            var sendFiles = new List<RandomFile>(Enumerable.Range(1, isDevice ? 3 : 10)
                .Select(i => new RandomFile(isDevice ? 2.Megs() : 5.Megs(), null,
#if ANDROID
                Path.Combine(filePath, $"File{i}.dat")
#else
                null
#endif
                )));
            var recFiles = new List<string>();

            using (Server server = new Server(9000))
            using (Client client = new Client("localhost", 9000, Client.Modes.Transaction, encrypt)) {
                Assert.IsTrue(client.Open());

                if (encrypt) Assert.IsTrue(client.UsingCrypto, "Crypto should be true");

                server.Handlers.Add("GetNames", (c, m) => {
                    return new SendFilenamesMessage("Files", sendFiles.Select(f => f.Name));
                });

                var namesReply = client.Transaction<RecFilenamesMessage>(new SendTextMessage("GetNames"));
                var mpReply = client.Transaction<RecMultipartMessage>(new SendFilenamesMessage("GetFiles", namesReply.Filenames));
                mpReply.Manager.FileUploaded += (s, e) => {
#if ANDROID
                    string fname = Path.Combine(filePath, $"Rec{Path.GetFileName(e.Info.Filename)}");
#else
                    string fname = Path.GetTempFileName();
#endif
                    recFiles.Add(fname);
                    using (var fs = new FileStream(fname, FileMode.Create, FileAccess.Write)) {
                        e.Contents.CopyTo(fs);
                    }
                };
                mpReply.Manager.Process();

                Assert.AreEqual(sendFiles.Count, recFiles.Count);
                bool eq = sendFiles.Zip(recFiles, (sf, rf) => sf.IsEqual(rf)).All(r => r);
                Assert.IsTrue(eq, "Not all files are equal");

                if (eq) {
                    foreach (var f in sendFiles.ToArray()) f.Dispose();
                    foreach (var f in recFiles) File.Delete(f);
                }
            }
        }

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(60000)]
        public void TransferBinary() {
            using (Server server = new Server(9000))
            using (Client client = new Client("localhost", 9000)) {
                Assert.IsTrue(client.Open());
                var sendbuffer = new byte[5.Megs()];
                var rand = new Random();
                rand.NextBytes(sendbuffer);

                server.Handlers.Add("GetData", (c, m) => {
                    return new SendBinaryMessage("Data", sendbuffer);
                });

                var reply = client.Transaction<RecBinaryMessage>(new SendTextMessage("GetData"));
                var recBuffer = reply.Data;
                Assert.AreEqual(sendbuffer.Length, recBuffer.Length);
                Assert.IsTrue(sendbuffer.Zip(recBuffer, (s, r) => s == r).All(r => r));
            }
        }

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        public void AutoHandle() {
            using (Server server = new Server(9000))
            using (Client client = new Client("localhost", 9000)) {
                server.Handlers.Add("request", (sc, msg) => {
                    return new SendTextMessage("reply");
                });
                server.Handlers.Add("badrequest", (sc, msg) => {
                    return new SendTextMessage("badreply");
                });

                Assert.IsTrue(client.Open());

                int counter = 0;
                client.Handlers.Add("reply", (c, r) => {
                    counter++;
                });

                var reply = client.Transaction(new SendTextMessage("request"));
                Assert.AreEqual(0, counter);
                Assert.IsFalse(client.AutoHandled);

                client.AutoHandle = true;
                reply = client.Transaction(new SendTextMessage("request"));
                Assert.AreEqual(1, counter);
                Assert.IsTrue(client.AutoHandled);

                reply = client.Transaction(new SendTextMessage("badrequest"));
                Assert.AreEqual(1, counter);
                Assert.IsFalse(client.AutoHandled);

                client.AutoHandle = false;
                reply = client.Transaction(new SendTextMessage("request"));
                Assert.AreEqual(1, counter);
                Assert.IsFalse(client.AutoHandled);
            }
        }
    }
}
