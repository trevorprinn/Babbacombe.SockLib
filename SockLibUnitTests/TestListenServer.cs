using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Babbacombe.SockLib;

namespace SockLibUnitTests {
    class TestListenServer : Server {

        private class Msg {
            public int MsgNo { get; private set; }
            public int Delay { get; private set; }

            public Msg(int msgNo, int delay) {
                MsgNo = msgNo;
                Delay = delay;
            }
        }
        private List<Msg> _msgs = new List<Msg>();

        private class TestServerClient : ServerClient {
            public int Ident { get; set; }
            public List<int> RecMsgs { get; private set; }
            public TestServerClient() {
                RecMsgs = new List<int>();
            }
        }

        public TestListenServer()
            : base(9000) {
            var rnd = new Random();
            for (int i = 0; i < 50; i++) _msgs.Add(new Msg(i + 1, rnd.Next(250)));
            _msgs.Shuffle();

            Handlers.Add("Test", handleMsg);
            Handlers.Add("Ident", handleIdent);
        }

        protected override ServerClient CreateClient() {
            return new TestServerClient();
        }

        private SendMessage handleMsg(ServerClient c, RecMessage m) {
            var msg = (RecTextMessage)m;
            ((TestServerClient)c).RecMsgs.Add(Convert.ToInt32(msg.Text));
            return null;
        }

        private SendMessage handleIdent(ServerClient c, RecMessage m) {
            var msg = (RecTextMessage)m;
            ((TestServerClient)c).Ident = Convert.ToInt32(msg.Text);
            return null;
        }

        public Task Exercise() {
            return Task.Factory.StartNew(() => {
                foreach (var msg in _msgs) {
                    Thread.Sleep(msg.Delay);
                    Broadcast(new SendTextMessage("Test", msg.MsgNo.ToString()));
                }
                Thread.Sleep(500);
            });
        }

        public IEnumerable<int> GetSentMessages() {
            return _msgs.Select(m => m.MsgNo);
        }

        public IEnumerable<int> GetRecMessages(int ident) {
            return Clients.Cast<TestServerClient>().Single(c => c.Ident == ident).RecMsgs;
        }
    }
}
