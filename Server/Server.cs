using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters;
using System.Collections;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting;
using System.Diagnostics;
using System.Reflection;
using CommonTypes;

namespace Server {
    public class Server {
        private static String address = "tcp://localhost:{0}/{1}";
        private static HashSet<String> states;
        private static IChannel channel;

        // Replication
        public static int NumberOfReplicas;
        public static int Port;
        protected static int MasterPort;
        public static HashSet<int> Ports;
        private static Server self;
        public int NextMasterPort;
        
        // Timing
        public static TimeSpan Period;
        public static DateTime TimeOfContact;
        public static TimeSpan Delay;

        // Users
        public static Dictionary<String, UserView> RegisteredUsers = new Dictionary<String, UserView>();
        public static Ticket Ticket = new Ticket(0);

        static Server() {
            states = new HashSet<String> { "Master", "Slave" };
            NumberOfReplicas = 3;
            MasterPort = 8888;
            Ports = new HashSet<int> { 8087, 8088, 8089, 8090 };
            Debug.Assert(Ports.Count == NumberOfReplicas + 1);
        }

        public static void Message(String info) {
            Console.WriteLine(String.Format("{0} on port {1}: {2}", self, Port, info));
        }

        private static string GetAddress(String port) {
            return String.Format(address, port, "Server");
        }

        private static string GetAddress(String name, String port) {
            return String.Format(address, port, name);
        }

        public static MasterInterface GetMaster() {
            return GetMaster(MasterPort.ToString());
        }

        public static MasterInterface GetMaster(String port) {
            return (MasterInterface)Activator.GetObject(typeof(MasterInterface), GetAddress(port));
        }

        public static SlaveInterface GetSlave(String port) {
            return (SlaveInterface)Activator.GetObject(typeof(SlaveInterface), GetAddress(port));
        }

        public static ServerInterface GetServer(String port) {
            return (ServerInterface)Activator.GetObject(typeof(ServerInterface), GetAddress(port));
        }

        public static void UpdatePorts(int port, List<int> ports) { 
            foreach (int serverPort in ports) {
                if (port == serverPort) { continue; }
                GetServer(serverPort.ToString()).AddPort(port);
            }
        }

        public static void AddPort(int port) {
            Ports.Add(port);
        }

        public static void UpdateTiming() {
            TimeOfContact = DateTime.Now;
            Message(String.Format("The current delay is {0} on a max of {1}", Server.Delay, Server.Period));
        }

        public static void UpdateUserData(Dictionary<String, UserView> users, Ticket ticket) {
            Ticket = ticket;
            RegisteredUsers = users;
            Message(String.Format("The current ticket number is {0}", Server.Ticket.Number));
        }

        public static void UpdateReplication(int port) {
            UpdateNextMasterPort(port);
            Message(String.Format("The next Master is on port {0}", Server.GetNextMasterPort()));
        }

        private static int GetNextMasterPort() {
            return self.NextMasterPort;
        }

        private static void UpdateNextMasterPort(int port) {
            self.NextMasterPort = port + 1;
        }

        public static void CreateChannel(String type, int port) {
            BinaryServerFormatterSinkProvider provider = new BinaryServerFormatterSinkProvider();
            provider.TypeFilterLevel = TypeFilterLevel.Full;
            IDictionary properties = new Hashtable();
            properties["port"] = port;
            properties["name"] = String.Format("{0} Channel", type);
            channel = new TcpChannel(properties, null, provider);
        }

        private static void IsRegistered() {
            if (channel != null) {
                throw new RegisteredException();
            }
        }

        public static void Register(String state_name, int port) {
            Console.WriteLine("Registering");
            CreateChannel(state_name, port);
            ChannelServices.RegisterChannel(channel, false);
            Type typeIface = Type.GetType(String.Format("{0}.{1}RemoteObject", typeof(Server).Namespace, state_name));
            RemotingConfiguration.RegisterWellKnownServiceType(typeIface, "Server", WellKnownObjectMode.SingleCall);
        }

        protected static void UnRegister() {
            ChannelServices.UnregisterChannel(channel);
            channel = null;
        }

        private static void ChangeState(String state_name, int port) {
            Debug.Assert(states.Contains(state_name));
            Type t = Type.GetType(String.Format("{0}.{1}", typeof(Server).Namespace, state_name));
            self = (Server)Activator.CreateInstance(t, port);
        }

        public void run(String state_name, int port) {
            try {
                ChangeState(state_name, port);
                IsRegistered();
                Register(state_name, port);
            }
            catch (RegisteredException) {
                UnRegister();
            }
            self.run();
        }

        public virtual void run() {}

        public static void Main(string[] args) {
            // start either as master or slave
            String state_name = args[0];
            int port = int.Parse(args[1]);
            // call the run method
            new Server().run(state_name, port);
        }
    }
}
