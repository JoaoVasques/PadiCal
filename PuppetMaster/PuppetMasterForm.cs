using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Messaging;
using CommonTypes;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Remoting.Channels.Tcp;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Collections;

namespace PuppetMaster {
    public partial class PadiCalTitle : Form {

        List<ReservationView> reservationList;
        UserView currentUser;
        String currentMenu = null;

        List<int> slotList = new List<int>();

        // name -> (ip,port)
        String address = "tcp://localhost:{0}/{1}";
        Dictionary<String, Tuple<String, String>> servers = new Dictionary<String, Tuple<String, String>>();
        Dictionary<String, Tuple<String, String>> users = new Dictionary<String, Tuple<String, String>>();

        TcpChannel channel = null;

        /************************************************************************/
        /*  Delegates - Async Calls
        /************************************************************************/
        public delegate bool UserRegistrationDelegate(String name, String ip, String port);

        /**
         * List of processes lauch by the Puppet Master
         */

        List<Process> launchedClients = new List<Process>();
        Dictionary<String, Process> launchedServers = new Dictionary<string, Process>();

        /// <summary>
        /// Replication variables
        /// </summary>
        int NUMBER_OF_REPLICAS = 4;
        String[] slavePorts = { "8087", "8088", "8089", "8090" }; 
        int numberOfReplicasAdded = 0;
        String currentMasterName = null;

        public PadiCalTitle() {
            this.currentMenu = "Main";
            channel = new TcpChannel();
            ChannelServices.RegisterChannel(channel, false);
            reservationList = new List<ReservationView>();
            currentUser = null;
            InitializeComponent();
        }

        private void BackToMainMenuButton_Click(object sender, EventArgs e) {
            Button1.Text = "Read Trace File";
            Button2.Text = "Client Menu";
            Button3.Text = "Server Menu";

            Button1.Visible = true;
            Button2.Visible = true;
            Button3.Visible = true;

            Button1.Enabled = true;
            Button2.Enabled = true;
            Button3.Enabled = true;

            MenuLabel.Text = "Main Menu";
            this.currentMenu = "Main";
            BackToMainMenuButton.Enabled = false;
            Label1.Text = "";
            Label1.Visible = false;
            CreateReservationGroupBox.Visible = false;
            ConnectDisconnectButton.Visible = false;
            currentMasterName = null;
            launchedClients.Clear();
            launchedServers.Clear();
            users.Clear();
            servers.Clear();
        }

        private void Button1_Click(object sender, EventArgs e) {
            /**
             *Changes to Read Trace File Menu 
             */

            if (this.currentMenu.Equals("Main")) {
                this.currentMenu = "Read Trace File";
                MenuLabel.Text = "Read Trace File Menu";
                TraceFileLabel.Visible = true;
                TraceFileTextBox.Visible = true;
                TraceFileOKButton.Visible = true;
                Button2.Enabled = false;
                Button3.Enabled = false;
                BackToMainMenuButton.Enabled = true;
                return;
            }

            if (this.currentMenu.Equals("Server")) {
                /************************************************************************/
                /*Calls server remote method to get current ticket number
                /************************************************************************/
                int ticketNumber = -1;
                Thread ticketThread = new Thread(delegate() {
                    ticketNumber = GetTicketNumber(this.Label1);
                }
                     );
                ticketThread.Name = "GetTicketNumberThread";
                ticketThread.Start();
                ticketThread.Join();

                Label1.Text = "Ticket number: " + ticketNumber;
                Label1.Visible = true;
                return;
            }

            /************************************************************************/
            /*          creates a reservation                                                                      
            /************************************************************************/

            if (this.currentMenu.Equals("Client")) {
                FillUsersCheckList(this.currentUser.getName());
                CreateReservationGroupBox.Visible = true;
                return;

            }
        }

        private void Button2_Click(object sender, EventArgs e) {
            /**
             *Changes to Client Menu 
            */
            if (this.currentMenu.Equals("Main")) {

                Button1.Enabled = false;
                Button3.Enabled = false;

                this.currentMenu = "Client";
                MenuLabel.Text = "Client Menu";

                ClientNameLabel.Visible = true;
                ClientNameTextBox.Visible = true;
                ClientNameOKButton.Visible = true;
                ClientNameOKButton.Enabled = true;
                ConnectDisconnectButton.Visible = true;
                return;
            }

            /************************************************************************/
            /*    Get registered clients                                                                 
            /************************************************************************/
            if (this.currentMenu.Equals("Server")) {
                if (Label1.Visible == true)
                    Label1.Visible = false;

                GetRegisteredClients();
                return;

            }

            /************************************************************************/
            /*  Read Calendar
            /************************************************************************/
            if (this.currentMenu.Equals("Client")) {

                GroupBox2.Visible = true;
                ShowCalendar(currentUser);
                return;
            }

        }

        private void Button3_Click(object sender, EventArgs e) {
            /**
             *Changes to Server Menu 
            */
            if (this.currentMenu.Equals("Main")) {

                this.currentMenu = "Server";
                MenuLabel.Text = "Server Menu";
                Button1.Text = "Get Ticket Number";
                Button2.Text = "Get Registered Clients";
                Button3.Text = "Get Active Replicas";
                BackToMainMenuButton.Enabled = true;
                return;
            }

            /************************************************************************/
            /*  Get Active Replicas
            /************************************************************************/

            if (this.currentMenu.Equals("Server")) {
                if (Label1.Visible == true)
                    Label1.Visible = false;

                GetActiveReplicas();
                return;
            }

            if (this.currentMenu.Equals("Client")) {
                GetUserInformation();
                return;
            }
        }

        private void CreateButton_Click(object sender, EventArgs e) {
            /************************************************************************/
            /*Calls client's remote method to create and disseminate information
             * Calls remote method the know all known users to the client
             * Launches a window that has debug info about the created reservation
            /************************************************************************/

            ReservationView reservationInfo = GetReservationInfo(currentUser.getName());

            CreateReservation(reservationInfo.getDescription(), reservationInfo.getSlotList(), reservationInfo.getParticipants(), reservationInfo.getCreator());

            slotList.Clear();
            CreateReservationGroupBox.Visible = false;
            DescriptionTextBox.Text = "";
            UsersCheckBoxList.Items.Clear();
            ListBoxSlot.Items.Clear();
        }

        /************************************************************************/
        /* Gets the text from the text box and calls a remote method to see if the user
         * exists in the system
        /************************************************************************/
        private String UserLogin(String userName) {
            MasterInterface remote = Server.Server.GetMaster();
            bool isRegistered = remote.IsRegistered(userName);

            if (isRegistered.Equals(true))
                return userName;
            return null;
        }

        private void CreateReservation(String description, List<ReservationSlot> slots, List<UserView> participants, String creator) {
            UserInterface remote = (UserInterface)Activator.GetObject(typeof(UserInterface), GetUser(creator));
            remote.CreateReservation(description, participants, slots, creator, Server.Server.GetMaster());
        }

        private int GetTicketNumber(Label lb) {
            MasterInterface remote = Server.Server.GetMaster();
            return remote.GetTicketNumber();
        }

        private void ShowCalendar(UserView user) {

            UserInterface proxy = User.User.GetUser(user.getIPAddress(), user.getPort(), user.getName());
            UserCalendarInformation userCalendarInformation = proxy.GetCalendarInfo();

            Box2ListBox.Items.Add(user.getName().ToUpper());
            Box2ListBox.Items.Add("Number of Free Slots: " + userCalendarInformation.GetNumberOfFreeSlots());
            Box2ListBox.Items.Add("Number of Ack Slots: " + userCalendarInformation.GetNumberOfAcknowledgeSlots());

            Box2ListBox.Items.Add("Assigned Slots");
            foreach (UserCalendarSlot assignedSlot in userCalendarInformation.GetAssignedSlots())
            {
                Box2ListBox.Items.Add("Slot number: " + assignedSlot.GetNumber());
            }

            Box2ListBox.Items.Add("Booked Slots");
            foreach (UserCalendarSlot bookedSlot in userCalendarInformation.GetBookedSlots())
            {
                Box2ListBox.Items.Add("Slot number: " + bookedSlot.GetNumber());
            }

            Box2ListBox.Items.Add("--------------");
            Box2Title.Text = "Show Calendars";
            GroupBox2.Visible = true;
        }

        /************************************************************************/
        /* Call server remote method to get all the registered clients
        /************************************************************************/
        private void GetRegisteredClients() {
            Box2Title.Text = "Registered Users";
            MasterInterface remote = Server.Server.GetMaster();

            List<CommonTypes.UserView> registeredUsers = remote.GetRegisteredClients();

            foreach (CommonTypes.UserView u in registeredUsers) {
                Box2ListBox.Items.Add(u.getName());
            }

            GroupBox2.Visible = true;
        }

        /************************************************************************/
        /* Calls server remote method to get all active replicas
        /************************************************************************/
        private void GetActiveReplicas() {
            Box2Title.Text = "Active Replicas";
            Box2Label.Text = "Active Replicas:";
            GroupBox2.Visible = true;
            // TODO: implement
        }

        private void ClientNameOKButton_Click(object sender, EventArgs e) {
            String user = ClientNameTextBox.Text;
            ClientNameTextBox.ForeColor = System.Drawing.Color.Black;

            Thread userLoginThread = new Thread(delegate() {
                user = UserLogin(user);
            });

            userLoginThread.Name = "userLoginThread";
            userLoginThread.Start();
            userLoginThread.Join();

            if (user == null) {
                ClientNameTextBox.ForeColor = System.Drawing.Color.Red;
                ClientNameTextBox.Text = "User does not exist!";
                return;
            }

            MenuLabel.Text = "Welcome " + user;
            Tuple<String, String> userInfo = this.users[user];
            this.currentUser = new UserView(user, userInfo.Item1, int.Parse(userInfo.Item2)); //para debug
            Button1.Enabled = true;
            Button1.Text = "Create Event";
            Button2.Text = "Read Calendar";
            Button3.Text = "Get User Info";
            Button3.Enabled = true;
            BackToMainMenuButton.Enabled = true;
            ClientNameLabel.Visible = false;
            ClientNameTextBox.Text = "";
            ClientNameTextBox.Visible = false;
            ClientNameOKButton.Visible = false;
            ClientNameOKButton.Enabled = false;
        }

        private void Box2CloseButton_Click(object sender, EventArgs e) {
            Box2Label.Text = "User's List:";
            Box2ListBox.Items.Clear();
            GroupBox2.Visible = false;
        }

        private void GetUserInfoLabelButton_Click(object sender, EventArgs e) {
            String userName = GetUserInfoTextBox.Text;
            MasterInterface remote = Server.Server.GetMaster();
            UserView user = remote.GetUserInformation(userName);

            if (user == null) {
                GetUserInfoTextBox.ForeColor = System.Drawing.Color.Red;
                GetUserInfoTextBox.Text = "User does not exist";
                GetUserInfoTextBox.ForeColor = System.Drawing.Color.Black;
                return;
            }

            else {

                String url = GetUser(userName);
                UserInterface userProxy = (UserInterface)Activator.GetObject(typeof(UserInterface), url);
                userProxy.AddNewKnownUsers(user.getName(), user.getPort(), user.getIPAddress());
                GetUserInfoLabel.Visible = false;
                GetUserInfoTextBox.Visible = false;
                GetUserInfoLabelButton.Visible = false;
            }
        }

        private void RegisterLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Box4.Visible = true;
        }

        /************************************************************************/
        /* Calls remote method to register the user or server and closes the box
        /************************************************************************/
        private void RegistrationConfirmButton_Click(object sender, EventArgs e) {
            String name = RegistrationUserNameTextBox.Text;
            String ip = RegistrationIPTextBox.Text;
            String port = RegistrationPortTextBox.Text;

            //TODO adicionar validacao de argumentos

            if (currentMenu.Equals("Server")) {
                if(name.Contains("Master"))
                CreateServer(name, ip, port,true);
                else
                CreateServer(name, ip, port, false);
            }
            else {
                CreateUser(name, ip, port);

                MasterInterface remote = Server.Server.GetMaster();
                try {
                    remote.RegisterUser(name, ip, port);
                }
                catch (DuplicateUserException ex) {
                    RegistrationUserNameTextBox.Text = "Duplicated User: " + ex.getName();
                    return;
                }

                //add name to the new registered object
                UserInterface userProxy = User.User.GetUser(address, int.Parse(port), name);
                userProxy.setName(name,int.Parse(port));
            }

            RegistrationUserNameTextBox.Text = "";
            RegistrationIPTextBox.Text = "";
            RegistrationPortTextBox.Text = "";
            Box4.Visible = false;
        }

        private void CreateServer(String name, String ip, String port, bool isMaster) {

            if (!this.servers.ContainsKey(name))
            {
                String slaveName = null;
                if (isMaster) port = "8888";
                else
                {
                    slaveName = "Slave";
                }

                Process p = new Process();
                p.StartInfo.FileName = CommonTypes.GlobalVariables.SERVER_EXE_DIR;
                
                if(slaveName==null)
                p.StartInfo.Arguments = String.Format("{0} {1}", "Master", port);
                else
                    p.StartInfo.Arguments = String.Format("{0} {1}", slaveName, port);  
      
                p.Start();
                servers.Add(name, new Tuple<String, String>(ip, port));
                this.launchedServers.Add(name, p);
            }
        }

        private void CreateUser(String name, String ip, String port) {
            if (!this.users.ContainsKey(name))
            {
                Process p = new Process();
                p.StartInfo.FileName = CommonTypes.GlobalVariables.USER_EXE_DIR;
                p.StartInfo.Arguments = String.Format("{0} {1}", name, port);
                p.Start();
                users.Add(name, new Tuple<String, String>(ip, port));
                this.launchedClients.Add(p);
            }

        }

        private void GetUserInformation() {
            GetUserInfoLabel.Visible = true;
            GetUserInfoLabelButton.Visible = true;
            GetUserInfoLabelButton.Visible = true;
            GetUserInfoTextBox.Visible = true;
        }

        private void FillUsersCheckList(String userName) {
            String url = GetUser(userName);
            UserInterface remote = (UserInterface)Activator.GetObject(typeof(UserInterface), url);
            Dictionary<CommonTypes.UserView, int> knownUsers = remote.getKnownUsers();
            List<CommonTypes.UserView> knownUsersList = knownUsers.Keys.ToList();

            foreach (CommonTypes.UserView u in knownUsersList) {
                UsersCheckBoxList.Items.Add(u.getName());
            }
        }

        private ReservationView GetReservationInfo(String userName) {
            UserInterface remote = (UserInterface)Activator.GetObject(typeof(UserInterface), GetUser(userName));
            Dictionary<CommonTypes.UserView, int> knownUsers = remote.getKnownUsers();
            List<CommonTypes.UserView> knownUsersList = knownUsers.Keys.ToList();

            CheckedListBox.CheckedItemCollection objCheckedItem = UsersCheckBoxList.CheckedItems;
            List<object> participantsAux = new List<object>();
            if (objCheckedItem.Count > 0) {

                for (int iCount = 0; iCount < objCheckedItem.Count; ++iCount) {
                    participantsAux.Add(objCheckedItem[iCount]);
                }
            }

            List<UserView> participants = new List<UserView>();

            foreach (UserView u in knownUsersList) {
                if (participantsAux.Contains(u.getName())) {
                    participants.Add(u);
                }
            }

            List<ReservationSlot> slots = new List<ReservationSlot>();
            foreach (int s in slotList) {
                slots.Add(new ReservationSlot(s));
            }

            ReservationView reservationInfo = new ReservationView(DescriptionTextBox.Text, participants, slots, this.currentUser.getName());
            this.reservationList.Add(reservationInfo);
            return reservationInfo;
        }

        private String GetUser(String name) {
            String port = users[name].Item2;
            return String.Format(address, port, name);
        }

        private void GetUserInfoLabelButton_Click_1(object sender, EventArgs e) {
            String userName = GetUserInfoTextBox.Text;
            MasterInterface remote = Server.Server.GetMaster();
            UserView user = remote.GetUserInformation(userName);

            if (user == null) {
                GetUserInfoTextBox.ForeColor = System.Drawing.Color.Red;
                GetUserInfoTextBox.Text = "User does not exist";
                GetUserInfoTextBox.ForeColor = System.Drawing.Color.Black;
            }
            else {
                UserInterface userProxy = (UserInterface)Activator.GetObject(typeof(UserInterface), GetUser(currentUser.getName()));
                userProxy.AddNewKnownUsers(user.getName(), user.getPort(), user.getIPAddress());
                GetUserInfoLabel.Visible = false;
                GetUserInfoTextBox.Visible = false;
                GetUserInfoLabelButton.Visible = false;
            }
        }

        private void SlotListLabel_Click(object sender, EventArgs e) {
            SlotListBox.Visible = true;
            CreateReservationGroupBox.Visible = false;
        }

        private int ConvertToSlotNumber(int hour, int day, int month) {
            int slot = 0;
            slot = hour + 24 * day;

            switch (month) {
                case 1: //January
                    break;
                case 2://February
                    slot += 31 * 24; //January days added
                    break;
                case 3: //March
                    slot += (31 + 28) * 24;
                    break;
                case 4://April
                    slot += (31 + 28 + 31) * 24;
                    break;
                case 5://May
                    slot += (31 + 28 + 31 + 30) * 24;
                    break;
                case 6://June
                    slot += (31 + 28 + 31 + 30 + 31) * 24;
                    break;
                case 7://July
                    slot += (31 + 28 + 31 + 30 + 31 + 30) * 24;
                    break;
                case 8: //August
                    slot += (31 + 28 + 31 + 30 + 31 + 30 + 31) * 24;
                    break;
                case 9://September
                    slot += (31 + 28 + 31 + 30 + 31 + 30 + 31 + 31) * 24;
                    break;
                case 10://October
                    slot += (31 + 28 + 31 + 30 + 31 + 30 + 31 + 31 + 30) * 24;
                    break;
                case 11://November
                    slot += (31 + 28 + 31 + 30 + 31 + 30 + 31 + 31 + 30 + 31) * 24;
                    break;
                case 12://December
                    slot += (31 + 28 + 31 + 30 + 31 + 30 + 31 + 31 + 30 + 31 + 30) * 24;
                    break;
            }
            return slot;
        }

        private void ConfirmNumberOfSlotsButton_Click(object sender, EventArgs e) {
            SelectSlotsOkButton.Visible = true;

            DateTime dayDate = DayDatePicker.Value;
            DateTime hourDate = HourDatePicker.Value;

            int month = dayDate.Month;
            int day = dayDate.Day;
            int hour = hourDate.Hour;

            int slot = ConvertToSlotNumber(hour, day, month);
            this.slotList.Add(slot);

            int numberOfSelectedSlots = int.Parse(NumberSelectedSlotsLabel.Text);
            numberOfSelectedSlots++;
            NumberSelectedSlotsLabel.Text = numberOfSelectedSlots.ToString();
        }

        private void SelectSlotsOkButton_Click(object sender, EventArgs e) {
            foreach (int slot in slotList) {
                ListBoxSlot.Items.Add("Slot " + slot);
            }
            ListBoxSlot.Visible = true;
            SlotListBox.Visible = false;
            CreateReservationGroupBox.Visible = true;
        }

        private void AddSlotButton_Click(object sender, EventArgs e) {
            DayDatePicker.ResetText();
            HourDatePicker.ResetText();
        }

        private void TraceFileOKButton_Click(object sender, EventArgs e) {
            TraceFileTextBox.ForeColor = System.Drawing.Color.Black;
            if (TraceFileTextBox.Text == "") {
                TraceFileTextBox.ForeColor = System.Drawing.Color.Red;
                TraceFileTextBox.Text = "Please insert trace file name!";
                return;
            }

            String fileName = TraceFileTextBox.Text;
            if (File.Exists(fileName)) {
                TextReader inputTraceFile = new StreamReader(fileName);
                ReadTraceFile(inputTraceFile);
                TraceFileLabel.Visible = false;
                TraceFileTextBox.Visible = false;
                TraceFileOKButton.Visible = false;
            }
            else {
                TraceFileTextBox.ForeColor = System.Drawing.Color.Red;
                TraceFileTextBox.Text = "File does not exist!";
            }
        }

        private void ReadTraceFile(TextReader traceFile) {
            String line;
            char[] delimiterChars = { ' ', '{', '}' };

            while ((line = traceFile.ReadLine()) != null) {
                String[] commands = line.Split(delimiterChars);
                if (commands[0].Equals("connect"))
                {
                    Connect(commands[1], commands[2]);
                }
                else if (commands[0].Equals("disconnect"))
                    Disconnect(commands[1], commands[2]);
                else if (commands[0].Equals("shutdown"))
                    ShutDown();
                else if (commands[0].Equals("wait"))
                    Wait(commands[1]);
                else if (commands[0].Equals("readCalendar"))
                    ReadCalendar(commands[1]);
                else if (commands[0].Equals("reservation"))
                    CreateReservationFromTraceFile(line);
            }
            traceFile.Close();
        }

        private void Connect(String name, String ipAndPort) {
            //Verificar se existe algum objecto registado no porto
            //Se nao existir, cria-se um processo novo. Se ja existir muda-se o boleano para o objecto estar activo

            char[] delimiterChars = { ':' };
            String[] words = ipAndPort.Split(delimiterChars);
            String ip = words[0];
            String port = words[1];

            //it is a server
            if (name.Contains("central")) {

                if (currentMasterName == null)
                {
                    currentMasterName = name;
                    CreateServer(name, ip, "8888", true);
                }
           
                else { 

                    String slavePort = slavePorts[numberOfReplicasAdded%NUMBER_OF_REPLICAS];
                    numberOfReplicasAdded ++;
                    CreateServer(name, ip, slavePort, false);
                }            
            }
            else {
                CreateUser(name, ip, port);

                //Get connected data from the previous user

                UserInterface userProxy = User.User.GetUser(users[name].Item1, int.Parse(users[name].Item2), name);
                MasterInterface remote = Server.Server.GetMaster();

                //if the user is registered them this is just a new connect request 
                if(this.users.ContainsKey(name))
                {
                    userProxy.Reconnect(int.Parse(port));
                    this.users.Remove(name);
                    this.users.Add(name, new Tuple<String, String>(address,port));
                }

                else
                    userProxy.connect(false, int.Parse(port));

                //add name to the new registered object        
                userProxy.setName(name,int.Parse(port));

                //register object in the server
                try
                {
                    remote.RegisterUser(name, ip, port);
                }

                catch (DuplicateUserException)
                {

                }
            }
        }

        private void Disconnect(String name, String ipAndPort) {

            char[] delimiterChars = { ':' };
            String[] words = ipAndPort.Split(delimiterChars);
            String ip = words[0];
            String port = words[1];

            //it is a server
            if (name.Contains("central"))
            {
                Process p = launchedServers[name];
                p.Kill();
                launchedServers.Remove(name);
            }
            else
            {
                //disconnect
                UserInterface userProxy = User.User.GetUser(address, int.Parse(port), name);
                userProxy.disconnect();
            }
            
        }

        private void ShutDown()
        {
            foreach (Process p in  launchedClients)
            {
                p.Kill();
            }

            foreach (KeyValuePair<String,Process> server in launchedServers)
            {
                server.Value.Kill();
            }
            servers.Clear();
            users.Clear();
            currentUser = null;
            currentMasterName = null;
            launchedServers.Clear();
            launchedClients.Clear();
        }

        private void Wait(String seconds)
        {
            int waitTime = int.Parse(seconds)* 1000;
            Thread.Sleep(waitTime);
        }

        private void ReadCalendar(String name) 
        {
            MasterInterface master = Server.Master.GetMaster();
            UserView user = master.GetUserInformation(name);
            GroupBox2.Visible = true;
            ShowCalendar(user);
        }


        private void CreateReservationFromTraceFile(String lineToProcess)
        {
            char[] separators = { '{', '}' };
            String[] words = lineToProcess.Split(separators);
            String reservationContent = words[1];
            char[] reservationContentSeparators = {';'};
            String[] splitedReservatinContent = reservationContent.Split(reservationContentSeparators);
            String description = splitedReservatinContent[0];
            String userListBeforeProcessing = splitedReservatinContent[1];
            String slotListBeforeProcessing = splitedReservatinContent[2];
            char[] listSeparators = { ' ', ',' };
            String[] userListAfterProcessing = userListBeforeProcessing.Split(listSeparators);
            String[] slotListAfterProcessing = slotListBeforeProcessing.Split(listSeparators);

            List<UserView> userList = new List<UserView>();
            MasterInterface master = Server.Server.GetMaster();
            UserView reservationCreator = null;
            foreach (String user in userListAfterProcessing)
            {
                if (user != null && user.Length != 0)
                {
                    UserView u = master.GetUserInformation(user);

                    if (reservationCreator == null)
                        reservationCreator = u;                    
                    else
                    userList.Add(u);
                }
            }

            List<ReservationSlot> slotList = new List<ReservationSlot>();
            foreach (String slot in slotListAfterProcessing)
            {
                if (slot != null && slot.Length != 0)
                    slotList.Add(new ReservationSlot(int.Parse(slot)));
            }

            UserInterface reservationCreatorProxy = User.User.GetUser(reservationCreator.getIPAddress(),reservationCreator.getPort(),reservationCreator.getName());
            
            //creator needs to know all the users in the reservation list
            foreach (UserView participant in userList)
            {
                reservationCreatorProxy.AddNewKnownUsers(participant.getName(), participant.getPort(), participant.getIPAddress());
            }
            
            reservationCreatorProxy.CreateReservation(description,userList,slotList,reservationCreator.getName(),master);
        }


        private void ConnectDisconnectButton_Click(object sender, EventArgs e) {
            String currentOption = ConnectDisconnectButton.Text;
            UserInterface userProxy = User.User.GetUser(this.currentUser.getIPAddress(), this.currentUser.getPort(), this.currentUser.getName());

            if (currentOption.Equals("Disconnect")) {
                //chamar 
                userProxy.disconnect();
                ConnectDisconnectButton.Text = "Connect";
            }
            else {
               
                MasterInterface remote = Server.Server.GetMaster();

                //if the user is registered them this is just a new connect request 
                /*if (remote.IsRegistered(this.currentUser.getName()))
                {
                    userProxy.connect(true);
                }*/
                
               ConnectDisconnectButton.Text = "Disconnect";
            }
        }
    }
}
