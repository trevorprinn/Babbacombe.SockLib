#region Licence
/*
    Babbacombe SockLib
    https://github.com/trevorprinn/SockLib
    Copyright © 2017 Babbacombe Computers Ltd.

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
using System.Text;
using System.Threading.Tasks;

using Stopwatch = System.Diagnostics.Stopwatch;
using Timer = System.Timers.Timer;

namespace Babbacombe.SockLib {
    internal abstract class PingManager : IDisposable {
        private Stopwatch _stopwatch;

        private Timer _checkTimer;
        private Timer _pingTimer;

        private bool _active;

        protected PingManager(int pingInterval, int pingTimeout) {
            _stopwatch = new Stopwatch();

            _pingTimer = new Timer(pingInterval);
            _pingTimer.AutoReset = true;
            _pingTimer.Elapsed += _pingTimer_Elapsed;

            _checkTimer = new Timer(pingTimeout);
            _checkTimer.AutoReset = true;
            _checkTimer.Elapsed += _checkTimer_Elapsed;
        }

        void _checkTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            if (!_active || !ClientIsOpen) return;
            if (Busy) {
                _stopwatch.Restart();
            } else if (_stopwatch.ElapsedMilliseconds > PingTimeout) {
                PingTimedOut();
            }
        }

        void _pingTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            if (!_active) return;
            if (Busy) {
                _stopwatch.Restart();
            } else {
                SendPing();
            }
        }

        protected abstract bool ClientIsOpen { get; }

        protected abstract void PingTimedOut();

        protected abstract void SendPing();

        /// <summary>
        /// Gets whether the client is busy sending or receiving data
        /// </summary>
        protected abstract bool Busy { get; }

        public void Start() {
            if (_active) return;
            _active = true;
            _stopwatch.Restart();
            _pingTimer.Start();
            _checkTimer.Start();
        }

        public void Reset() {
            _stopwatch.Reset();
            if (_active) _stopwatch.Start();
        }

        public void Stop() {
            _active = false;
            _checkTimer.Stop();
            _pingTimer.Stop();
            _stopwatch.Reset();
        }

        public int PingInterval {
            get { return (int)_pingTimer.Interval; }
            set {
                _pingTimer.Stop();
                _pingTimer.Interval = value;
                if (_active) _pingTimer.Start();
            }
        }

        public int PingTimeout {
            get { return (int)_checkTimer.Interval; }
            set {
                _checkTimer.Stop();
                _checkTimer.Interval = value;
                if (_active) _checkTimer.Start();
            }
        }

        protected virtual void Dispose(bool disposing) {
            _active = false;
            _pingTimer.Dispose();
            _checkTimer.Dispose();
            _stopwatch.Stop();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    internal class ClientPingManager : PingManager {
        public const int DefaultPingInterval = 500;

        public const int DefaultPingTimeout = 2000;

        public Client Client { get; private set; }

        public ClientPingManager(Client client)
            : base(DefaultPingInterval, DefaultPingTimeout) {
                Client = client;
        }

        protected override void SendPing() {
            try {
                Client.SendMessage(new SendPingMessage(false));
            } catch { }
        }

        protected override bool ClientIsOpen => Client.IsOpen;
        
        protected override void PingTimedOut() {
            Client.PingTimedOut();
        }

        protected override bool Busy => Client.Busy;
    }

    internal class ServerPingManager : PingManager {
        public ServerClient Client;

        public ServerPingManager(ServerClient client, int pingInterval, int pingTimeout) : base(pingInterval, pingTimeout) {
            Client = client;
        }

        protected override void SendPing() {
            try {
                Client.SendMessage(new SendPingMessage(false));
            } catch { }
        }

        protected override bool ClientIsOpen => true;

        protected override void PingTimedOut() {
            Client.PingTimedOut();
        }

        protected override bool Busy => Client.Busy;
    }

}
