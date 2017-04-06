using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Babbacombe.SockLib;
using System.IO;
using Newtonsoft.Json;
#if DEVICE
using NUnit.Framework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace SockLibUnitTests {
#if DEVICE
    [TestFixture]
#else
    [TestClass]
#endif
    public class JsonMessageTests {
        private bool isDevice
#if DEVICE
            => true;
#else
            => false;
#endif

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        public void TransferJsonData() {
            RecMessage.CustomMessages.Add('J', typeof(RecJsonMessage));

            var sendData = new JsonTestModel {
                a = 5, b = "abc def",
                c = Enumerable.Range(10, 20).Select(i => new Tuple<string, int>($"Item {i}", i)).ToArray()
            };
            var reply = (RecJsonMessage)MessageTests.TransferMessage(new SendJsonMessage("TestJson", sendData));
            var recData = reply.GetData<JsonTestModel>();
            Assert.AreEqual(JsonConvert.SerializeObject(recData), JsonConvert.SerializeObject(sendData));
        }
    }

    public class SendJsonMessage : SendTextMessage {
        public SendJsonMessage(string command, object data)
            : base(command, JsonConvert.SerializeObject(data)) { }

        protected override char MessageType => 'J';
    }

    public class RecJsonMessage : RecTextMessage {
        public RecJsonMessage(RecMessageHeader header, Stream stream) : base(header, stream) { }

        public T GetData<T>() => JsonConvert.DeserializeObject<T>(Text);

        public object GetData() => JsonConvert.DeserializeObject<dynamic>(Text);
    }

    public class JsonTestModel {
        public int a { get; set; }
        public string b { get; set; }
        public Tuple<string, int>[] c { get; set; }
    }
}
