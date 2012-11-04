using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters;
using System.Collections;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting;
using System.Threading;
using CommonTypes;
using System.Runtime.Remoting.Channels.Http;

namespace User {
    public class ReservationPerSlot {
        private List<Reservation> reservationsOfThisSlot;

        public ReservationPerSlot() {
            this.reservationsOfThisSlot = new List<Reservation>();
        }

        public void addReservation(Reservation r) {
            this.reservationsOfThisSlot.Add(r);
        }

        public List<Reservation> GetReservations() {
            return this.reservationsOfThisSlot;
        }

        public void RemoveReservation(Reservation r) {
            Console.WriteLine("[ReservationPerSlot] - Removing reservation with ticket number: " + r.getTicket());
            this.reservationsOfThisSlot.Remove(r);
        }

        public bool HasReservations() {
            if (this.reservationsOfThisSlot.Count == 0)
                return false;
            return true;
        }
    }

    public class User {
        public static List<UserCalendarSlot> Calendar = null;
        public static List<ReservationPerSlot> ReservationPerSlot = null;
        /// <summary>
        /// int = trustValue
        /// </summary>
        public static Dictionary<UserView, int> KnownUsers = new Dictionary<UserView, int>();

        public static Dictionary<String, JacobsonKarels> KnownUsersTimeout = new Dictionary<String, JacobsonKarels>();

        public static IChannel channel;
        public static List<Reservation> CreatedReservations = new List<Reservation>();
        public static bool IsRegistered = false; //checks if the channel is registered

        //Connected boolean variable
        public static bool connected;

        public static String Name;
        public static int Port;
        private static String address = "tcp://localhost:{0}/{1}";

        //Logger
        public static Logger userLoger = new Logger();

        public User(String userName) {
            Name = userName;
            userLoger.SetLogger(userName);
        }

        public static UserInterface GetUser(String addr, int port, String name) {
            return (UserInterface)Activator.GetObject(typeof(UserInterface), String.Format(address, port, name));
        }

        private static void CreateChannel(int port) {
            BinaryServerFormatterSinkProvider provider = new BinaryServerFormatterSinkProvider();
            provider.TypeFilterLevel = TypeFilterLevel.Full;
            IDictionary props = new Hashtable();
            props["port"] = port;
            props["name"] = "User Channel";
            channel = new TcpChannel(props, null, provider);
        }

        public static void Register() {
            Console.WriteLine("Registering...");
            CreateChannel(Port);
            ChannelServices.RegisterChannel(channel, false);
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(UserRemoteObject), Name, WellKnownObjectMode.SingleCall);
            IsRegistered = true;
        }

        public static void Reconnect(int newPort)
        {
            Console.WriteLine("Reconnecting to new port " + newPort);
            UnRegister();
            Port = newPort;
            Register();
        }

        public static void UnRegister() {
            ChannelServices.UnregisterChannel(channel);
            channel = null;
            IsRegistered = false;
        }

        public void run() {
            Console.WriteLine("Welcome " + Name);
            Console.ReadLine();
        }

        public void run(int port) {
            Port = port;
            Register();
            this.run();
        }

        static void Main(string[] args) {
            String name = args[0];
            int port = int.Parse(args[1]);
            new User(name).run(port);
        }
    }
}
