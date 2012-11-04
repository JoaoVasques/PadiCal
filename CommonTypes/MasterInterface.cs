using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes {
    public interface MasterInterface {
        void RegisterUser(String name, String ip, String port);
        bool IsRegistered(String name);
        UserView GetUserInformation(String name);
        int GetTicketNumber();
        int RequestTicketNumber();
        List<UserView> GetRegisteredClients();
        void UnRegisterUser(String name);
    }
}
