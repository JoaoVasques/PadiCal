using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;

namespace Server {
    public class Master: Server {
        private delegate void NotifySlaveDelegate(int port);

        public Master(int port) {
            Port = port;
           // UnRegister();
           // Register("Master", MasterPort);
        }

        private void NotifySlave(int port) {
            GetSlave(port.ToString()).Update(Port, RegisteredUsers, Ticket);
        }

        private void NotifySlaves() {
            foreach (int port in Ports) {
                if (port == Port) { continue; } // it's the Master's regular port
                try {
                    NotifySlaveDelegate remoteDelegate = new NotifySlaveDelegate(NotifySlave);
                    IAsyncResult result = remoteDelegate.BeginInvoke(port, null, null);
                }
                catch (KeyNotFoundException) {
                    Message(String.Format("Host {0} is not yet available", port));
                }
                catch (SocketException) {
                    Message(String.Format("Host {0} unavailable", port));
                }
                catch (IOException) {
                    Message(String.Format("Operation Interrupted"));
                }
            }
        }

        public override void run() {
            Message(String.Format("Server on {0} mode on port {1}.", "Master", Port));
            while (true) {
                // master behaviour
                // -- send keep-alives
                NotifySlaves();
            }
        }
    }
}
