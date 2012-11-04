using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonTypes;
using System.Diagnostics;

namespace Server {
    public class Slave : Server {
        public Slave(int port) {
            Port = port;
        }

        private static void UpdateTimePeriod() {
            Period = TimeSpan.FromSeconds(5);
        }

        private static void UpdateDelay() {
            if (TimeOfContact != DateTime.MinValue) {
                Delay = DateTime.Now - TimeOfContact;
            }
        }

        private void CheckMaster() {
            if (Delay > Period) {
                throw new ItsDeadException();
            }
        }

        private bool AmITheNextMaster() {
            return NextMasterPort == Port;
        }

        private void Elections() {
            if (AmITheNextMaster()) {
                base.run("Master", Port);
                Message("Elections: I'm the next Master");
            }
            else {
                TimeOfContact = DateTime.MinValue;
                Delay = TimeSpan.FromSeconds(0);
                Message(String.Format("Elections: the next Master is {0}", NextMasterPort));
            }
        }

        public override void run() {
            Message(String.Format("Server on {0} mode on port {1}.", "Slave", MasterPort));
            while (true) {
                // slave behaviour
                try {
                    // -- update timeout
                    UpdateTimePeriod();
                    // -- update delay
                    UpdateDelay();
                    // -- check if master has made contact recently
                    CheckMaster();
                }
                catch (ItsDeadException) {
                    // -- elections
                    Elections();
                }
            }
        }
    }
}
