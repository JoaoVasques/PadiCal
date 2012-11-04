using System;
using System.Linq;
using System.Text;
using CommonTypes;
using System.Collections.Generic;

namespace Server {
    public class SlaveRemoteObject : MarshalByRefObject, ServerInterface, SlaveInterface {
        public void Update(int port, Dictionary<String, UserView> users, Ticket ticket) {
            Server.UpdateTiming();
            Server.UpdateReplication(port);
            Server.UpdateUserData(users, ticket);
        }

        public void AddPort(int port) {
            Server.AddPort(port);
        }
    }
}
