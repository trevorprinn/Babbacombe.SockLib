using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Babbacombe.SockLib;

namespace SockLibUnitTests {
    [TestClass]
    public class DiscoverTests {
        [TestMethod]
        public async Task BasicDiscover() {
            IPEndPoint ep;
            var client = new DiscoverClient(9000);
            using (var server = new DiscoverServer(9000, "MyTest", 9001)) {
                ep = await client.FindService("MyTest");
                Assert.IsNotNull(ep, "First Try");
                Assert.AreEqual(9001, ep.Port);

                ep = await client.FindService("Something");
                Assert.IsNull(ep);

                ep = await client.FindService("MyTest");
                Assert.IsNotNull(ep, "Second Try");
                Assert.AreEqual(9001, ep.Port);
            }

            ep = await client.FindService("MyTest");
            Assert.IsNull(ep);
        }
    }
}
