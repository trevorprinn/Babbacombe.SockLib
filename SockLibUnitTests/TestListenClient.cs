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

        public TestListenClient(int ident, int msgCount, bool encrypt = false)
            : base("localhost", 9000, Modes.Listening, encrypt) {
            if (encrypt) DelimGen = new RandomDelimGen();
            Ident = ident;
            if (!Open()) throw new ApplicationException("Client socket didn't open");
            var rnd = new Random();
            for (int i = 0; i < msgCount; i++) _msgs.Add(new Msg(i + 1, rnd.Next(1000)));
            _msgs.Shuffle();

            Handlers.AddAsync<RecTextMessage>("Test", handleMsg);
        }


        private Task handleMsg(Client c, RecTextMessage msg) {
            lock (_recMsgs) {
                _recMsgs.Add(Convert.ToInt32(msg.Text));
            }
            return null;
        }

        public Task Exercise() {
            SendMessage(new SendTextMessage("Ident", Ident.ToString()));

            return Task.Run(async () => {
                foreach (var msg in _msgs) {
                    await Task.Delay(msg.Delay);
                    SendMessage(new SendTextMessage("Test", msg.MsgNo.ToString()));
                }
                while (ListenBusy) await Task.Delay(500);
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
