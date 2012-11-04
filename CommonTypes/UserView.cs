using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes
{
    [Serializable]
    public class UserView
    {
        private String name;
        private int port;
        private String ip;
        private Dictionary<String, int> knownUsers;

        public UserView(String name, String ip, int port)
        {
            this.name = name;
            this.port = port;
            this.ip = ip;
            this.knownUsers = new Dictionary<string, int>();
        }

        public String getName()
        {
            return this.name;
        }

        public void addKnownUsers(UserView user, int trustValue)
        {
            knownUsers.Add(user.getName(), trustValue);
        }

        public Dictionary<String, int> GetKnownUsers()
        {
            return this.knownUsers;
        }

        public String getIPAddress()
        {
            return this.ip;
        }

        public int getPort()
        {
            return this.port;
        }

        public bool isEqual(UserView u)
        {
            return this.getName().Equals(u.getName());
        }
    }
}
