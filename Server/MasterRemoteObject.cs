using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using CommonTypes;

namespace Server {
    public class MasterRemoteObject : MarshalByRefObject, ServerInterface, MasterInterface {
        public void RegisterUser(String name, String ip, String port) {
            if (Server.RegisteredUsers.ContainsKey(name)) {
                Server.Message("Duplicated user");
                throw new DuplicateUserException(name);
            }
            else {
                UserView newUser = new UserView(name, ip, int.Parse(port));
                Server.RegisteredUsers.Add(newUser.getName(), newUser);
                Server.Message("User: " + name + " registered with success!");
            }
        }

        public List<UserView> GetRegisteredClients() {
            List<UserView> users;

            lock (Server.RegisteredUsers) {
                users = Server.RegisteredUsers.Values.ToList();
            }
            return users;
        }

        public int GetTicketNumber() {
            int ticketNumber = -1;

            lock (Server.Ticket) {
                ticketNumber = Server.Ticket.Number;
            }
            return ticketNumber;
        }

        private void IncrementTicket() {
            lock (Server.Ticket) {
                Server.Ticket.IncrementNumber();
            }
        }

        public bool IsRegistered(String name) {
            bool result = false;

            lock (Server.RegisteredUsers) {
                result = Server.RegisteredUsers.Keys.Contains(name);
            }
            return result;
        }

        public UserView GetUserInformation(string name) {
            return Server.RegisteredUsers[name];
        }

        public int RequestTicketNumber() {
            int ticketNumber = GetTicketNumber();
            IncrementTicket();
            return ticketNumber;
        }

        public void UnRegisterUser(String name)
        {
            Console.WriteLine("Unregistering " + name);
            Server.RegisteredUsers.Remove(name);

        }

        public void AddPort(int port) {
            Server.AddPort(port);
        }
    }
}
