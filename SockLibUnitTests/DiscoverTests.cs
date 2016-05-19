using System;
using System.Net;
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
    public class DiscoverTests {
#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(30000)]
        public async Task BasicDiscover() {
            IPEndPoint ep;
            var client = new DiscoverClient(9000);
            using (var server = new DiscoverServer(9000, "MyTest", 9001)) {
                ep = await client.FindServiceAsync("MyTest");
                Assert.IsNotNull(ep, "First Try");
                Assert.AreEqual(9001, ep.Port);

                ep = await client.FindServiceAsync("Something");
                Assert.IsNull(ep);

                ep = await client.FindServiceAsync("MyTest");
                Assert.IsNotNull(ep, "Second Try");
                Assert.AreEqual(9001, ep.Port);
            }

            ep = await client.FindServiceAsync("MyTest");
            Assert.IsNull(ep);
        }

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(30000)]
        public async Task DiscoverOpen() {
            IPEndPoint ep;
            var client = new DiscoverClient(9000);
            using (var server = new DiscoverServer(9000, "MyTest")) {
                ep = await client.FindServiceAsync("MyTest");
                Assert.IsNotNull(ep);
            }

            using (Client c = new Client(ep)) {
                Assert.IsFalse(c.Open());

                using (var server = new Server(9000)) {
                    Assert.IsTrue(c.Open());
                }
            }
        }

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(10000)]
        public void NoSuchServer() {
            using (var client = new Client("abcdefg", 9000)) {
                Assert.IsFalse(client.Open());
            }
        }

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(30000)]
        public async Task DiscoverSync() {
            IPEndPoint ep;
            var client = new DiscoverClient(9000);
            using (var server = new DiscoverServer(9000, "MyTest", 9001)) {
                ep = await client.FindServiceAsync("MyTest");
                Assert.IsNotNull(ep, "First Try");
                Assert.AreEqual(9001, ep.Port);

                ep = await client.FindServiceAsync("Something");
                Assert.IsNull(ep);

                ep = await client.FindServiceAsync("MyTest");
                Assert.IsNotNull(ep, "Second Try");
                Assert.AreEqual(9001, ep.Port);
            }

            ep = await client.FindServiceAsync("MyTest");
            Assert.IsNull(ep);
        }
    }
}
