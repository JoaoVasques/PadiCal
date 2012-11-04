using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes {
    public interface SlaveInterface {
        void Update(int port, Dictionary<String, UserView> users, Ticket ticket);
    }
}
