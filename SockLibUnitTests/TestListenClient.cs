using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Babbacombe.SockLib;

namespace SockLibUnitTests {
    class TestListenClient : Client {
        public int Ident { get; private set; }
                private List<Msg> _msgs = new List<Msg>();
        private List<int> _recMsgs = new List<int>();

        private class Msg {
            public int MsgNo { get; private set; }
            public int Delay { get; private set; }

            public Msg(int msgNo, int delay) {
                MsgNo = msgNo;
                Delay = delay;
            }
        }

        public TestListenClient(int ident)
            : base("localhost", 9000, Modes.Listening) {
            Ident = ident;
            Open();
            var rnd = new Random();
            for (int i = 0; i < 50; i++) _msgs.Add(new Msg(i + 1, rnd.Next(250)));
            _msgs.Shuffle();

            Handlers.Add("Test", handleMsg);
        }


        private void handleMsg(Client c, RecMessage m) {
            var msg = (RecTextMessage)m;
            _recMsgs.Add(Convert.ToInt32(msg.Text));
        }

        public Task Exercise() {
            SendMessage(new SendTextMessage("Ident", Ident.ToString()));

            return Task.Factory.StartNew(() => {
                foreach (var msg in _msgs) {
                    Thread.Sleep(msg.Delay);
                    SendMessage(new SendTextMessage("Test", msg.MsgNo.ToString()));
                }
                Thread.Sleep(500);
            });
        }

        public IEnumerable<int> GetSentMessages() {
            return _msgs.Select(m => m.MsgNo);
        }

        public IEnumerable<int> GetRecMessages() {
            return _recMsgs;
        }
    }
}
