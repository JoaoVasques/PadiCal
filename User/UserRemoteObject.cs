using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonTypes;
using System.Collections;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Threading;
using System.Diagnostics;

namespace User {

    class ThreadInfo {
        public UserInterface proxy { get; set; }
        public String userName { get; set; } //user name to which thC:\Users\Joao\documents\visual studio 2010\Projects\Padi-Cal\User\UserRemoteObject.cse message will be send
        public String sendUserName { get; set; } //user that sent the message
        public ReservationMessage reservationMessage { get; set; }
        public int threadNumber { get; set; }
        public bool secondDissemination { get; set; }
    }

    public enum MessageTypeToDisseminate { SEND_RESERVATION, UPDATE_USERS_RESERVATION_INFO, PROPOSE_SLOT, ABORT_RESERVATION, TWO_PHASE_COMMIT,COMMIT_RESERVATION }

    public class UserRemoteObject : MarshalByRefObject, UserInterface {

        //Threading attributes
        List<Thread> localThreadList = new List<Thread>();
        List<Thread> disseminationThreadList = new List<Thread>(); //used on 2nd time dissemination threads
        private delegate ReservationMessage RemoteAsyncDelegate(ReservationMessage m, int t);
        private delegate ReservationMessage ProposeSlotAsyncDelegate(ReservationSlot s, ReservationMessage r, int t);
        //key = thread number
        //Values 
        //      1-> thread delegate to CallSendReservation
        //      2-> response of the remote async call
        private Dictionary<int, ReservationMessage> threadResponses = new Dictionary<int, ReservationMessage>();
        /// <summary>
        /// Used by the reservation creator to try to contact the users that havent responded yet
        /// </summary>
        Dictionary<String, ReservationMessage> threadResponses2 = new Dictionary<String, ReservationMessage>();

        public string getName() {
            return User.Name;
        }

        private bool IsDisseminating(String messageType)
        {
            bool isDisseminating = false;
            lock (disseminationThreadList)
            {
                foreach (Thread t in disseminationThreadList)
                {
                    String tName = t.Name;
                    char[] separators = { ' ' };
                    String[] str = tName.Split(separators);

                    if (str[1].Equals(messageType))
                    {
                        Console.WriteLine("I am already disseminating that kind of message");
                        isDisseminating = true;
                        break;
                    }
                }

                return isDisseminating;
            }
        }

        private void CreateCalendar() {
            User.Calendar = new List<UserCalendarSlot>();
            User.ReservationPerSlot = new List<ReservationPerSlot>();
            for (int i = 1; i <= 8760; i++) {
                User.Calendar.Add(new UserCalendarSlot(i));
                User.ReservationPerSlot.Add(new ReservationPerSlot());
            }
        }

        /// <summary>
        /// Adds a new user to the list of known users and sets its trust value to the default value (1)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="port"></param>
        /// <param name="ip"></param>
        /// 
        public void AddNewKnownUsers(String name, int port, String ip) {
            Console.WriteLine("[Calling] AddNewKnownUsers");
            UserView newUser = new UserView(name, ip, port);

            lock (User.KnownUsers) {
                foreach (UserView u in User.KnownUsers.Keys) {
                    if (newUser.getName().Equals(u.getName())) {
                        return;
                    }
                }
                User.KnownUsers.Add(newUser, 1);
            }

            lock (User.KnownUsersTimeout) {
                User.KnownUsersTimeout.Add(newUser.getName(), new JacobsonKarels());
            }
            Console.WriteLine("[AddNewKnownUsers] User " + newUser.getName() + "added successfully");
        }

        public void RemoveKnownUser(String userName)
        {
            Console.WriteLine("Removing " + userName + " from the known users");
            lock (User.KnownUsers)
            {
                List<UserView> knownUsers = User.KnownUsers.Keys.ToList();
                foreach (UserView knownUser in knownUsers)
                {
                    if (knownUser.getName().Equals(userName))
                    {
                        User.KnownUsers.Remove(knownUser);
                        break;
                    }
                }
            }
        }

        public Dictionary<UserView, int> getKnownUsers() {
            Console.WriteLine("[Calling] getKnownUsers");
            return User.KnownUsers;
        }

        public void setName(String name, int port)
        {
            User.Name = name;
            connect(false,port);
        }

        public void disconnect() {
            Console.WriteLine("Disconnecting...");
            User.connected = false;

            foreach (UserView knownUser in User.KnownUsers.Keys)
            {
                UserInterface userProxy = User.GetUser(knownUser.getIPAddress(), knownUser.getPort(), knownUser.getName());
                userProxy.RemoveKnownUser(User.Name);
            }

        }

        public void Reconnect(int newPort)
        {
            MasterInterface remote = Server.Server.GetMaster();
            remote.UnRegisterUser(User.Name);
            User.Reconnect(newPort);
            remote.RegisterUser(User.Name, "localhost", User.Port.ToString());
            
        }

        public UserCalendarInformation GetCalendarInfo()
        {
            Console.WriteLine("[Calling] GetCalendarInfo");
            UserCalendarInformation calendarInformation = new UserCalendarInformation();

            if (User.Calendar == null) CreateCalendar();

            foreach (UserCalendarSlot calendarSlot in User.Calendar)
            {
                if (calendarSlot.isAssigned())
                {
                    Console.WriteLine("Slot " + calendarSlot.GetNumber());
                    calendarInformation.AddAssignedSlot(calendarSlot);
                }
                else if (calendarSlot.isBooked())
                    calendarInformation.AddBookedSlot(calendarSlot);
                else if (calendarSlot.isAcknowledge())
                    calendarInformation.IncrementNumberOfAcknowledgeSlots();
                else if (calendarSlot.isFree())
                    calendarInformation.IncrementNumberOfFreeSlots();
            }

            lock (User.userLoger)
            {
                Console.WriteLine("Number of exchanged messages: " + User.userLoger.GetNumberOfMessages());
                User.userLoger.WriteLogToFile();
            }

            return calendarInformation;
        }

        public bool AmIDisconnected() {
            Console.WriteLine("[Calling] AmIDisconnected?");
            return User.connected.Equals(false);
        }

        public void connect(bool reconnect, int port) {
            Console.WriteLine("Connecting...");
            User.connected = true;
        }

        public ReservationMessage ProposeSlot(ReservationSlot slot, ReservationMessage reservation, int timeout) {
            Console.WriteLine("[Calling] ProposeSlot");
            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("ProposeSlot");
            }
            Console.WriteLine("Timeout to send message: " + timeout);
            if (User.connected == false) {
                Console.WriteLine("I am not connected. Do not answer call");
                Thread.Sleep(2 * timeout);
                return new ReservationMessage();
            }

            bool isDisseminating = IsDisseminating("PROPOSE"); ;

            List<Reservation> rsvList = User.ReservationPerSlot[slot.GetNumber()].GetReservations();
            Console.WriteLine("Proposed slot: " + slot.GetNumber());
            ReservationMessage response = new ReservationMessage();
            response.senderName = User.Name;

            if (rsvList.Count == 1) {
                Console.WriteLine("Slot Found! Send Ack");
                if (!response.GetAllProposedSlotResponses().ContainsKey(response.senderName))
                    response.AddProposedSlotResponse(User.Name, "Ack");
            }
            else {
                bool isNak = false;

                foreach (Reservation r in rsvList) {
                    if (r.areEqual(reservation.reservation))
                        continue;

                    //exists a reservation for this slot whose ticket number is lower
                    if (r.getTicket() < reservation.reservation.getTicket()) {
                        Console.WriteLine("A reservation with a lower ticket number was found! It has higher priority! Send Nak");
                        if (!response.GetAllProposedSlotResponses().ContainsKey(response.senderName)) {
                            response.AddProposedSlotResponse(response.senderName, "Nak");
                            isNak = true;
                        }
                        //return "Nak";
                    }
                }

                if (!isNak) {
                    if (!response.GetAllProposedSlotResponses().ContainsKey(response.senderName))
                        response.AddProposedSlotResponse(response.senderName, "Ack");
                }
            }

            if (isDisseminating)
                return response;

            int lenght = 2;
            if (Thread.CurrentThread.Name == null) {
                Console.WriteLine("Thread Name is null. Original remote invocation thread");
            }
            else {
                String tName = Thread.CurrentThread.Name;
                Console.WriteLine("Thread Name: " + tName);
                char[] separator = { ' ' };
                String[] words = tName.Split(separator);
                lenght = words.Length;
            }

            //Calls the gossip dissemination protocol to send the message to other nodes
            //Before sending the message we need to remove the user that sent the message
            int userListSize = reservation.reservation.getUserList().Count;
            double userListDouble = (double)userListSize;
            double messageCounterDouble = (double)reservation.messageCounter;
            if ((messageCounterDouble <= (userListDouble / 2)) && (userListSize != 1)) {
                if (Thread.CurrentThread.Name == null || lenght == 2) {

                    Console.WriteLine("Disseminate!");
                    reservation.reservation = DeleteSenderFromUserList(reservation);
                    reservation.messageCounter *= 2;
                    Console.WriteLine("Thread responses size: " + threadResponses.Count);
                    DisseminateInformation(reservation, MessageTypeToDisseminate.PROPOSE_SLOT, true);

                    //add received answers to the answer we will send
                    foreach (KeyValuePair<int, ReservationMessage> responses in threadResponses) {
                        Console.WriteLine("Thread " + responses.Key + " responses");
                        foreach (KeyValuePair<String, String> x in responses.Value.GetAllProposedSlotResponses()) {
                        }
                        Console.WriteLine(responses.Value.PrintSlotResponses());

                        //only adds the response if it does not exist
                        if (!response.GetAllProposedSlotResponses().ContainsKey(responses.Value.senderName))
                            response.AddProposedSlotResponse(responses.Value.senderName, responses.Value.GetProposedSlotResponseByUser(responses.Value.senderName));

                    }
                }
                else Console.WriteLine("Thread has no permission to disseminate!");
            }

            Console.WriteLine("Printing answer");
            foreach (KeyValuePair<String, String> i in response.GetAllProposedSlotResponses()) {
                Console.WriteLine("User " + i.Key + "  response: " + i.Value);
            }
            return response;
        }

         public void CreateReservation(String description, List<UserView> userList, List<ReservationSlot> slotList, String creator, MasterInterface master) {
            if (!User.IsRegistered) {
                User.Register();
            }

            Console.WriteLine("[Calling] CreateReservation");
            Console.WriteLine("Printing slot list..");

            foreach (ReservationSlot s in slotList) {
                Console.WriteLine(s.ToString());
            }

            int ticket = master.RequestTicketNumber();

            Reservation reservation = new Reservation(description, userList, slotList, creator, ticket);

            //reservation has no participants. Medical dentist consult example
            if (userList.Count == 0)
            {
                Console.WriteLine("No participants");
                SinglePersonReservation(slotList, reservation);
                return;
            }

            User.CreatedReservations.Add(reservation);
            ChangeCalendarSlotStatus(slotList, true);

            ReservationMessage reservationMessage = new ReservationMessage();
            reservationMessage.senderName = User.Name;
            reservationMessage.reservation = reservation;
            reservationMessage.messageCounter = 1;

            //send reservation
            DisseminateInformation(reservationMessage, MessageTypeToDisseminate.SEND_RESERVATION,false);

            List<UserView> usersThatNotAnswered = UsersThatNotAnsweredSendReservationCall(reservationMessage.reservation, false);

            if (usersThatNotAnswered.Count == 0) { 
                Console.WriteLine("All users have answered"); 
            }
            else {
                List<UserView> newList = RemoveUsersThatNotAnsweredFromTheUserList(reservationMessage.reservation.getUserList(), usersThatNotAnswered);
                localThreadList.Clear();
                threadResponses.Clear();
                threadResponses2.Clear();
                disseminationThreadList.Clear();

                PrepareRetrySendReservation(usersThatNotAnswered, reservationMessage);

                usersThatNotAnswered.Clear();
                usersThatNotAnswered = UsersThatNotAnsweredSendReservationCall(reservationMessage.reservation, true);

                Console.WriteLine("Not answered retry!");
                foreach (UserView u in usersThatNotAnswered) {
                    Console.WriteLine("User " + u.getName() + " did not answered. Dead? Maybe...");
                }

                newList = RemoveUsersThatNotAnsweredFromTheUserList(reservationMessage.reservation.getUserList(), usersThatNotAnswered);
                if (newList.Count == 0) {
                    DisseminateInformation(reservationMessage, MessageTypeToDisseminate.ABORT_RESERVATION, false);
                    localThreadList.Clear(); disseminationThreadList.Clear();
                    threadResponses.Clear(); threadResponses2.Clear();
                    return;
                }
                reservationMessage.reservation.SetUserList(newList);
            }

            localThreadList.Clear();

            //changes the reservation slot status based on the answers received
            //if the reservation if null them all slots are aborted -> Abort the reservation

            Reservation temporaryReservation = null;

            temporaryReservation = ChangeReservationSlotStatus(reservationMessage.reservation);

            if (temporaryReservation == null) {
                DisseminateInformation(reservationMessage, MessageTypeToDisseminate.ABORT_RESERVATION, false);
                localThreadList.Clear();
                threadResponses.Clear();
                return;
            }

            reservationMessage.reservation = temporaryReservation;

            reservationMessage.reservation = DeleteAbortedSlotsFromReservation(reservationMessage.reservation);
            PrintReservation(reservationMessage.reservation);  //debug

            threadResponses.Clear();

            //sends information to the users about the state of the reservation slots
            DisseminateInformation(reservationMessage, MessageTypeToDisseminate.UPDATE_USERS_RESERVATION_INFO, false);

            //remove the aborted slots from the slotList and start proposing possible slots for the event
            localThreadList.Clear();
            threadResponses.Clear();

            //there are no avaible slots
            if (reservationMessage.reservation.getSlotList().Count == 0) {
                Console.WriteLine("No avaiable slots. Abort Reservation");
                DisseminateInformation(reservationMessage, MessageTypeToDisseminate.ABORT_RESERVATION, false);
                localThreadList.Clear();
                threadResponses.Clear();
                return;
            }

            bool wasAccepted = false;
            int slotListSize = reservationMessage.reservation.getSlotList().Count;
            ReservationMessage proposedSlotMessage = new ReservationMessage();
            ReservationSlot commitSlot = null;

            for (int i = 0; i <= slotListSize; i++) {
                proposedSlotMessage = CreateMessageWithProposedSlot(reservationMessage.reservation, reservationMessage.reservation.getSlotList()[i]);
                DisseminateInformation(proposedSlotMessage, MessageTypeToDisseminate.PROPOSE_SLOT, false);

                //check if the proposed slot was accepted by all participants
                wasAccepted = WasProposedSlotAccepted(proposedSlotMessage.proposedSlot);

                if (wasAccepted) {
                    {
                        commitSlot = proposedSlotMessage.proposedSlot;
                        break;
                    }
                }

                localThreadList.Clear();
                threadResponses.Clear();
            }

            Console.WriteLine("Was accepted? " + wasAccepted);
            if (!wasAccepted) {
                Console.WriteLine("No slot was accepted! Abort Reservation");
                DisseminateInformation(reservationMessage, MessageTypeToDisseminate.ABORT_RESERVATION, false);
                localThreadList.Clear();
                threadResponses.Clear();
                return;
            }

            Console.WriteLine("AcceptedSlot\n" + proposedSlotMessage.proposedSlot.ToString());
            localThreadList.Clear();
            threadResponses.Clear();

            Console.WriteLine("Starting 2PC");
            DisseminateInformation(reservationMessage, MessageTypeToDisseminate.TWO_PHASE_COMMIT, false);
            usersThatNotAnswered.Clear();
            usersThatNotAnswered = UsersThatNotAnsweredTwoPhaseCommit(reservationMessage.reservation,false);

            if (usersThatNotAnswered.Count == 0)
            {
                Console.WriteLine("All users have answered");
            }
            else
            {
                List<UserView> newList = RemoveUsersThatNotAnsweredFromTheUserList(reservationMessage.reservation.getUserList(), usersThatNotAnswered);

                PrepareRetryTwoPhaseCommit(usersThatNotAnswered, reservationMessage);

                usersThatNotAnswered.Clear();
                usersThatNotAnswered = UsersThatNotAnsweredTwoPhaseCommit(reservationMessage.reservation, true);

                foreach (UserView u in usersThatNotAnswered)
                {
                    Console.WriteLine("User " + u.getName() + " did not answered. Dead? Maybe...");
                }

                newList = RemoveUsersThatNotAnsweredFromTheUserList(reservationMessage.reservation.getUserList(), usersThatNotAnswered);
                if (newList.Count == 0)
                {
                    DisseminateInformation(reservationMessage, MessageTypeToDisseminate.ABORT_RESERVATION, false);
                    localThreadList.Clear(); disseminationThreadList.Clear();
                    threadResponses.Clear(); threadResponses2.Clear();
                    return;
                }
                reservationMessage.reservation.SetUserList(newList);
            }


            Console.WriteLine("Send Commit Reservation!");
            ReservationMessage commitMessage = new ReservationMessage();
            commitMessage.slotToCommit = commitSlot;
            commitMessage.reservation = reservationMessage.reservation;
            commitMessage.senderName = User.Name;
            commitMessage.messageCounter = 1;
            DisseminateInformation(commitMessage, MessageTypeToDisseminate.COMMIT_RESERVATION,false) ;
            CommitReservation(commitMessage, 2000);
            localThreadList.Clear();
            threadResponses.Clear();
        }

        /// <summary>
        /// Called when the reservation is a single person one. E.g. medical consult
        /// </summary>
        /// <param name="slotList"></param>
        private void SinglePersonReservation(List<ReservationSlot> slotList, Reservation reservation)
        {
            Console.WriteLine("[Calling] SinglePersonReservation");

            bool assigned = false;

            if (User.Calendar == null) CreateCalendar();

            lock (User.Calendar)
            {
                foreach (ReservationSlot slot in slotList)
                {
                    if (!User.Calendar[slot.GetNumber()-1].isBooked() && !User.Calendar[slot.GetNumber()-1].isAssigned())
                    {
                        User.Calendar[slot.GetNumber()-1].SetAssigned();
                        assigned = true;
                        User.ReservationPerSlot[slot.GetNumber()].addReservation(reservation);
                        break;
                    }
                }
            }
            if (assigned == false) Console.WriteLine("No suitable slot was found: ABORTED");

        }

        private void CallTwoPhaseCommit(object context)
        {

            ThreadInfo threadInfo = context as ThreadInfo;
            UserInterface proxy = threadInfo.proxy;
            Console.WriteLine("Thread Number: " + threadInfo.threadNumber);
            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("CallTwoPhaseCommit");
            }
            ReservationMessage response = new ReservationMessage();
            RemoteAsyncDelegate remoteDelegate = new RemoteAsyncDelegate(proxy.TwoPhaseCommit);

            String userName = threadInfo.userName;

            JacobsonKarels timeoutAlgoritm = User.KnownUsersTimeout[userName];
            timeoutAlgoritm.ModifiedUpdateTimeout();

            int timeout = timeoutAlgoritm.GetTimeout();

            IAsyncResult result = remoteDelegate.BeginInvoke(threadInfo.reservationMessage, timeout, null, null);
            bool responseInTime = result.AsyncWaitHandle.WaitOne(timeout, true);

            Console.WriteLine("Work time is over! Response in time? " + responseInTime);

            //didn't get a response till timeout
            if (responseInTime == false)
            {
                Console.WriteLine("Timeout! Did not receive any answer!");
                if(!response.response2PC.ContainsKey(userName))
                    response.response2PC.Add(userName,"Timeout");;
                result.AsyncWaitHandle.Close();
                return;
            }

            if (!response.response2PC.ContainsKey(userName))
                 response.response2PC.Add(userName, "YES");
            IncrementUserTrust(threadInfo.userName);
            response = remoteDelegate.EndInvoke(result);
            Console.WriteLine("Can Commit!");
            result.AsyncWaitHandle.Close();

            String currentThreadName = Thread.CurrentThread.Name;
            char[] separator = { ' ' };
            String[] splitedThreadName = currentThreadName.Split(separator);
            String currentThreadNumber = splitedThreadName[0];
            int currentTNumber = int.Parse(currentThreadNumber);

            if (threadResponses.ContainsKey(currentTNumber))
            {
                threadResponses.Remove(currentTNumber);
            }

            int originalMessageCounter = threadInfo.reservationMessage.messageCounter;
            //if is the retry
            if (originalMessageCounter < 0)
            {
                if (threadResponses2.ContainsKey(threadInfo.userName))
                    threadResponses2.Remove(threadInfo.userName);
                threadResponses2.Add(threadInfo.userName, response);
            }
            else
                threadResponses.Add(threadInfo.threadNumber, response);
        }

        private void CallCommitReservation(object context)
        {
            ThreadInfo threadInfo = context as ThreadInfo;
            UserInterface proxy = threadInfo.proxy;
            Console.WriteLine("Thread Number: " + threadInfo.threadNumber);
            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("CallCommitReservation");
            }
            ReservationMessage response = null;
            RemoteAsyncDelegate remoteDelegate = new RemoteAsyncDelegate(proxy.CommitReservation);

            String userName = threadInfo.userName;

            JacobsonKarels timeoutAlgoritm = User.KnownUsersTimeout[userName];
            timeoutAlgoritm.ModifiedUpdateTimeout();

            int timeout = timeoutAlgoritm.GetTimeout();

            IAsyncResult result = remoteDelegate.BeginInvoke(threadInfo.reservationMessage, timeout, null, null);
            bool responseInTime = result.AsyncWaitHandle.WaitOne(timeout, true);

            Console.WriteLine("Work time is over! Response in time? " + responseInTime);

            //didn't get a response till timeout
            if (responseInTime == false)
            {
                Console.WriteLine("Timeout! Did not receive any answer!");
                result.AsyncWaitHandle.Close();
                return;
            }
            IncrementUserTrust(threadInfo.userName);
            response = remoteDelegate.EndInvoke(result);
            Console.WriteLine("Commit!");
            result.AsyncWaitHandle.Close();
            return;
        }

        private void PrepareRetryTwoPhaseCommit(List<UserView> listToSend, ReservationMessage reservation)
        {
            Console.WriteLine("[Calling] PrepareRetryTwoPhaseCommit");
            List<UserInterface> usersProxy = new List<UserInterface>();

            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("TwoPhaseCommit");
            }

            foreach (UserView u in listToSend)
            {
                UserInterface proxy = User.GetUser(u.getIPAddress(), u.getPort(), u.getName());
                usersProxy.Add(proxy);
            }

            Console.WriteLine("Creating Threads...");
            int i = 0;
            foreach (UserInterface ui in usersProxy)
            {
                ThreadInfo threadInfo = new ThreadInfo();
                threadInfo.proxy = ui;
                threadInfo.userName = ui.getName();
                threadInfo.sendUserName = User.Name;
                threadInfo.reservationMessage = reservation;
                threadInfo.threadNumber = i++;

                //send a message counter equal to the user list size to avoid further dissemination
                threadInfo.reservationMessage.messageCounter = -1;
                Thread t = new Thread(delegate()
                {
                    CallTwoPhaseCommit(threadInfo);
                });

                t.Name = threadInfo.threadNumber + " 2PC";
                localThreadList.Add(t);
                t.Start();
            }

            Console.WriteLine("Waiting for threads to finish...");
            foreach (Thread t in localThreadList)
            {
                t.Join();
            }
            Console.WriteLine("all threads have finished!");
        }

        private void PrepareRetrySendReservation(List<UserView> listToSend, ReservationMessage reservation) {
            Console.WriteLine("[Calling] PrepareRetrySendReservation");
            List<UserInterface> usersProxy = new List<UserInterface>();

            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("TwoPhaseCommit");
            }

            foreach (UserView u in listToSend) {
                UserInterface proxy = User.GetUser(u.getIPAddress(), u.getPort(), u.getName());
                usersProxy.Add(proxy);
            }

            Console.WriteLine("Creating Threads...");
            int i = 0;
            foreach (UserInterface ui in usersProxy) {
                ThreadInfo threadInfo = new ThreadInfo();
                threadInfo.proxy = ui;
                threadInfo.userName = ui.getName();
                threadInfo.sendUserName = User.Name;
                threadInfo.reservationMessage = reservation;
                threadInfo.threadNumber = i++;

                //send a message counter equal to the user list size to avoid further dissemination
                threadInfo.reservationMessage.messageCounter = -1;
                Thread t = new Thread(delegate() {
                        CallSendReservation(threadInfo);
                    });

                t.Name = threadInfo.threadNumber + " SEND_RESERVATION";
                localThreadList.Add(t);
                t.Start();
            }

            Console.WriteLine("Waiting for threads to finish...");
            foreach (Thread t in localThreadList) {
                t.Join();
            }
            Console.WriteLine("all threads have finished!");
        }

        private void CallAbortReservation(object context) {
            Console.WriteLine("[Calling] CallAbortReservation");
            Console.WriteLine("Reservation is going to be aborted. No slot consensus achieved");
            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("CallAbortReservation");
            }
            ThreadInfo threadInfo = context as ThreadInfo;
            UserInterface proxy = threadInfo.proxy;
            Console.WriteLine("Thread Number: " + threadInfo.threadNumber);

            ReservationMessage response = null;
            RemoteAsyncDelegate remoteDelegate = new RemoteAsyncDelegate(proxy.AbortReservation);

            String userName = threadInfo.userName;

            JacobsonKarels timeoutAlgoritm = User.KnownUsersTimeout[userName];
            timeoutAlgoritm.ModifiedUpdateTimeout();

            int timeout = timeoutAlgoritm.GetTimeout();

            IAsyncResult result = remoteDelegate.BeginInvoke(threadInfo.reservationMessage, timeout, null, null);
            bool responseInTime = result.AsyncWaitHandle.WaitOne(timeout, true);

            Console.WriteLine("Work time is over! Response in time? " + responseInTime);

            //didn't get a response till timeout
            if (responseInTime == false) {
                //Console.WriteLine("Timeout! Did not receive any answer!");
                result.AsyncWaitHandle.Close();
                return;
            }
            IncrementUserTrust(threadInfo.userName);
            response = remoteDelegate.EndInvoke(result);
            Console.WriteLine("User " + userName + " aborted reservation sucessfully");
            result.AsyncWaitHandle.Close();
            return;
        }

        private bool WasProposedSlotAccepted(ReservationSlot proposedSlot) {
            Console.WriteLine("[Calling] WasProposedSlotAccepted");
            bool result = true;

            foreach (KeyValuePair<int, ReservationMessage> responses in threadResponses) {
                Console.WriteLine("Thread " + responses.Key);

                foreach (KeyValuePair<String, String> answers in responses.Value.GetAllProposedSlotResponses()) {
                    if (answers.Value.Equals("Nak")) {
                        result = false;
                        break;
                    }
                }

                if (!result)
                    break;
            }

            return result;
        }

        private ReservationMessage CreateMessageWithProposedSlot(Reservation reservation, ReservationSlot proposedSlot) {
            Console.WriteLine("[Calling] CreateMessageWithProposedSlot\nSlot: " + proposedSlot.GetNumber());
            ReservationMessage messageWithProposedSlot = new ReservationMessage();
            messageWithProposedSlot.reservation = reservation;
            messageWithProposedSlot.proposedSlot = proposedSlot;
            return messageWithProposedSlot;
        }

        private void CallProposeSlot(object context) {
            Console.WriteLine("[Calling] CallProposeSlot");
            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("CallProposeSlot");
            }
            ThreadInfo threadInfo = context as ThreadInfo;
            UserInterface proxy = threadInfo.proxy;

            Console.WriteLine("Thread Number: " + threadInfo.threadNumber);

            ReservationMessage response = null;
            ProposeSlotAsyncDelegate remoteDelegate = new ProposeSlotAsyncDelegate(proxy.ProposeSlot);

            String userName = threadInfo.userName;

            JacobsonKarels timeoutAlgoritm = User.KnownUsersTimeout[userName];
            timeoutAlgoritm.ModifiedUpdateTimeout();

            int timeout = timeoutAlgoritm.GetTimeout();

            IAsyncResult result = remoteDelegate.BeginInvoke(threadInfo.reservationMessage.proposedSlot, threadInfo.reservationMessage, timeout, null, null);
            bool responseInTime = result.AsyncWaitHandle.WaitOne(timeout, true);

            Console.WriteLine("Work time is over! Response in time? " + responseInTime);

            //didn't get a response till timeout
            if (responseInTime == false) {
                Console.WriteLine("Timeout! Did not receive any answer!");
                result.AsyncWaitHandle.Close();
                response = new ReservationMessage();
                response.AddProposedSlotResponse(userName, "Timeout");
                result.AsyncWaitHandle.Close();
            }
            else {
                IncrementUserTrust(threadInfo.userName);
                response = remoteDelegate.EndInvoke(result);
                Console.WriteLine("User " + userName);
                Console.WriteLine("Proposed slot answer: " + response.GetProposedSlotResponseByUser(userName));
                result.AsyncWaitHandle.Close();

            }

            String currentThreadName = Thread.CurrentThread.Name;
            char[] separator = { ' ' };
            String[] splitedThreadName = currentThreadName.Split(separator);
            String currentThreadNumber = splitedThreadName[0];
            int currentTNumber = int.Parse(currentThreadNumber);


            if (threadResponses.ContainsKey(currentTNumber)) {
                threadResponses.Remove(currentTNumber);
            }
            threadResponses.Add(threadInfo.threadNumber, response);
            return;
        }

        /// <summary>
        /// Sends information to the users about the new status of the reservation slots
        /// </summary>
        /// <param name="reservation"></param>
        private void CallUpdateUsersReservationInfo(object context) {
            ThreadInfo threadInfo = context as ThreadInfo;
            UserInterface proxy = threadInfo.proxy;
            Console.WriteLine("Thread Number: " + threadInfo.threadNumber);
            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("CallUpdateUsersReservationInfo");
            }
            ReservationMessage response = null;
            RemoteAsyncDelegate remoteDelegate = new RemoteAsyncDelegate(proxy.ReservationSlotsUpdate);

            String userName = threadInfo.userName;

            JacobsonKarels timeoutAlgoritm = User.KnownUsersTimeout[userName];
            timeoutAlgoritm.ModifiedUpdateTimeout();

            int timeout = timeoutAlgoritm.GetTimeout();

            IAsyncResult result = remoteDelegate.BeginInvoke(threadInfo.reservationMessage, timeout, null, null);
            bool responseInTime = result.AsyncWaitHandle.WaitOne(timeout, true);

            Console.WriteLine("Work time is over! Response in time? " + responseInTime);

            //didn't get a response till timeout
            if (responseInTime == false) {
                Console.WriteLine("Timeout! Did not receive any answer!");
                result.AsyncWaitHandle.Close();
                return;
            }
            IncrementUserTrust(threadInfo.userName);
            response = remoteDelegate.EndInvoke(result);
            Console.WriteLine("Slots were changed on the calendar");
            result.AsyncWaitHandle.Close();
            return;
        }

        /// <summary>
        /// Changes the slots that are TB on the reservation to Booked on the Calendar
        /// </summary>
        /// <param name="reservation"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public ReservationMessage ReservationSlotsUpdate(ReservationMessage reservation, int timeout) {
            if (User.Calendar == null) CreateCalendar();

            Console.WriteLine("[Calling] Reservation Slots Update");
            if (User.connected == false) {
                Console.WriteLine("I am not connected. Do not answer call");
                Thread.Sleep(2 * timeout);
                return new ReservationMessage();
            }

            lock (User.Calendar) {
                foreach (ReservationSlot slot in reservation.reservation.getSlotList()) {
                    UserCalendarSlot calendarSlot = User.Calendar[slot.GetNumber()-1];
                    if (slot.isTentativelyBooked() && !calendarSlot.isBooked() && !calendarSlot.isAssigned()) {
                        Console.WriteLine("Setting slot " + slot.GetNumber() + " to Booked");
                        User.Calendar[slot.GetNumber()-1].setBooked();
                    }

                    else
                        Console.WriteLine("Slot is not TB. Do not set calendar slot to Booked");
                }
            }
            return reservation;
        }

        private Reservation DeleteAbortedSlotsFromReservation(Reservation reservation) {
            Console.WriteLine("[Calling] DeleteAbortedSlotsFromReservation");
            List<ReservationSlot> newSlotList = new List<ReservationSlot>();

            foreach (ReservationSlot slot in reservation.getSlotList()) {
                if (slot.isAborted() == false) {
                    newSlotList.Add(slot);
                }
                else
                    Console.WriteLine("Slot " + slot.GetNumber() + " is aborted! Remove it");
            }

            Reservation reservationWithoutAbortedSlots = new Reservation(reservation.getDescription(), reservation.getUserList(), newSlotList, reservation.getCreator(), reservation.getTicket());
            return reservationWithoutAbortedSlots;
        }

        /// <summary>
        /// Checks all the answers for each slot. If the answers were all Ack's them that slot changes
        /// its state to Tentatively-Booked. Otherwise, it changes its state to Aborted
        /// </summary>
        /// <param name="reservation"></param>
        /// <returns></returns>
        private Reservation ChangeReservationSlotStatus(Reservation reservation) {


            List<int> slotsWithNaks = AnyNAKReceived(threadResponses);

            Console.WriteLine("Print slot with naks");
            foreach (int i in slotsWithNaks) {
                Console.WriteLine("Slot " + i);
            }

            foreach (ReservationSlot slot in reservation.getSlotList()) {
                if (slotsWithNaks.Contains(slot.GetNumber())) {
                    slot.SetAborted();
                }
                else
                    slot.setTentativelyBooked();
            }

            bool areAllTheSlotsAborted = true;

            foreach (ReservationSlot s in reservation.getSlotList()) {
                if (!s.isAborted()) {
                    Console.WriteLine("Slot " + s.GetNumber() + " is not aborted!");
                    areAllTheSlotsAborted = false; break;
                }
            }

            if (areAllTheSlotsAborted) return null;
            return reservation;
        }

        private Dictionary<int, String> ChangeCalendarSlotStatus(List<ReservationSlot> slotList, bool createdByMe) {
            Console.WriteLine("[Calling] ChangeCalendarSlotStatus");

            //if calendar is not initialized initialize it
            if (User.Calendar == null) {
                Console.WriteLine("[Info] Calendar is null. Begin initialization");
                CreateCalendar();
            }

            Dictionary<int, String> slotResponse = new Dictionary<int, string>();

            foreach (ReservationSlot s in slotList) {

                lock (User.Calendar[s.GetNumber()-1]) {
                    if (User.Calendar[s.GetNumber()-1].isFree()) {
                        Console.WriteLine("Slot is free. Send ACK");
                        User.Calendar[s.GetNumber()-1].setAcknowledge();
                        slotResponse.Add(s.GetNumber(), "ACK");
                    }

                    else if (User.Calendar[s.GetNumber()-1].isAcknowledge()) {
                        Console.WriteLine("Slot is Acknowledge. Send ACK");
                        User.Calendar[s.GetNumber()-1].setAcknowledge();
                        slotResponse.Add(s.GetNumber(), "ACK");
                    }

                    else {
                        Console.WriteLine("Slot is not Free or Acknowledge. Send NAK");
                        slotResponse.Add(s.GetNumber(), "NAK");
                    }
                }
            }

            return slotResponse;
        }

        private void DisseminateInformation(ReservationMessage reservationMessage, MessageTypeToDisseminate typeOfMessage, bool secondDissemination) {
            Console.WriteLine("[Calling] DisseminateReservation");
            Console.WriteLine("Mode-> " + typeOfMessage.ToString());
            Console.WriteLine("");

            List<UserView> chosenUsers = null;
            if (typeOfMessage.Equals(MessageTypeToDisseminate.UPDATE_USERS_RESERVATION_INFO) || typeOfMessage.Equals(MessageTypeToDisseminate.ABORT_RESERVATION) || typeOfMessage.Equals(MessageTypeToDisseminate.COMMIT_RESERVATION)) {
                chosenUsers = reservationMessage.reservation.getUserList();
            }
            else
                chosenUsers = ChooseUsers(reservationMessage.reservation,reservationMessage.messageCounter);

            Console.WriteLine("Printing chosen users..");
            foreach (UserView u in chosenUsers) {
                Console.WriteLine(u.getName() + " port: " + u.getPort());
            }

            List<UserInterface> chosenUsersProxys = new List<UserInterface>();

            foreach (UserView u in chosenUsers) {
                UserInterface proxy = User.GetUser(u.getIPAddress(), u.getPort(), u.getName());
                chosenUsersProxys.Add(proxy);
            }


            Console.WriteLine("Creating Threads...");
            int i = 0;
            foreach (UserInterface ui in chosenUsersProxys) {
                ThreadInfo threadInfo = new ThreadInfo();
                threadInfo.proxy = ui;
                threadInfo.userName = ui.getName();  //caso
                threadInfo.sendUserName = User.Name;
                threadInfo.reservationMessage = reservationMessage;
                threadInfo.threadNumber = i++;

                if (secondDissemination) threadInfo.secondDissemination = true;
                else threadInfo.secondDissemination = false;

                Thread t;
                if (typeOfMessage.Equals(MessageTypeToDisseminate.SEND_RESERVATION)) {

                    t = new Thread(delegate() {
                         CallSendReservation(threadInfo);
                     });
                    if (secondDissemination)
                        t.Name = threadInfo.threadNumber + " SEND_RESERVATION HAS_DISSEMINATED";
                    else
                        t.Name = threadInfo.threadNumber + " SEND_RESERVATION";
                }

                else if (typeOfMessage.Equals(MessageTypeToDisseminate.UPDATE_USERS_RESERVATION_INFO)) {
                    t = new Thread(delegate() {
                        CallUpdateUsersReservationInfo(threadInfo);
                    });

                    if (secondDissemination)
                        t.Name = threadInfo.threadNumber + " UPDATE HAS_DISSEMINATED";
                    else
                        t.Name = threadInfo.threadNumber + " UPDATE";
                }

                else if (typeOfMessage.Equals(MessageTypeToDisseminate.ABORT_RESERVATION)) {
                    t = new Thread(delegate() {
                            CallAbortReservation(threadInfo);
                        });
                    t.Name = threadInfo.threadNumber + " ABORT";
                }

                else if (typeOfMessage.Equals(MessageTypeToDisseminate.COMMIT_RESERVATION))
                {
                    t = new Thread(delegate()
                        {
                            CallCommitReservation(threadInfo);
                        });
                    if (secondDissemination)
                        t.Name = threadInfo.threadNumber + " COMMIT HAS_DISSEMINATED";
                    else
                        t.Name = threadInfo.threadNumber + " COMMIT";
                }

                else if (typeOfMessage.Equals(MessageTypeToDisseminate.TWO_PHASE_COMMIT))
                {
                    t = new Thread(delegate()
                        {
                            CallTwoPhaseCommit(threadInfo);
                        });
                    if (secondDissemination)
                        t.Name = threadInfo.threadNumber + " 2PC HAS_DISSEMINATED";
                    else
                        t.Name = threadInfo.threadNumber + " 2PC";                    
                }
                else
                {
                    t = new Thread(delegate()
                    {
                        CallProposeSlot(threadInfo);
                    });

                    if (secondDissemination)
                        t.Name = threadInfo.threadNumber + " PROPOSE HAS_DISSEMINATED";
                    else
                        t.Name = threadInfo.threadNumber + " PROPOSE";
                }


                //needs to check if there is a thread with the same number on the list
                //if there is...remove it and add the new one (t)

                String currentThreadName = t.Name;
                char[] separator = { ' ' };
                String[] splitedThreadName = currentThreadName.Split(separator);
                String currentThreadNumber = splitedThreadName[0];
                int currentTNumber = int.Parse(currentThreadNumber);

                Thread td = ThreadExistsInThreadList(currentTNumber);

                //exists...
                if (td != null) {
                    localThreadList.Remove(td);
                }

                if (secondDissemination.Equals(true)) {

                    disseminationThreadList.Add(t);
                }
                else
                    localThreadList.Add(t);

                Console.WriteLine("Created thread " + t.Name);
                t.Start();
            }

            Console.WriteLine("Waiting for threads to finish...");
            if (secondDissemination.Equals(true)) {

                foreach (Thread t in disseminationThreadList) {
                    t.Join();
                }
            }
            else {
                foreach (Thread t in localThreadList) {
                    t.Join();
                }
            }

            Console.WriteLine("all threads have finished!");
        }


        private Thread ThreadExistsInThreadList(int threadNumber) {
            Thread thread = null;

            foreach (Thread t in localThreadList) {
                String tName = t.Name;
                char[] separator = { ' ' };
                String[] splitedThreadName = tName.Split(separator);
                String tNumber = splitedThreadName[0];
                int number = int.Parse(tNumber);

                if (number.Equals(threadNumber)) {
                    thread = t;
                    break;
                }
            }

            return thread;
        }

        /// <summary>
        /// Method the will be called by the threads the DisseminateReservationw will create.
        /// This method will make a remote call to a specific user (proxy)
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="reservation"></param>
        /// 
        private void CallSendReservation(object context) {
            Console.WriteLine("[Calling] CallSendReservation");
            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("CallSendReservation");
            }
            ThreadInfo threadInfo = context as ThreadInfo;
            UserInterface proxy = threadInfo.proxy;
            Reservation reservation = threadInfo.reservationMessage.reservation;
            Console.WriteLine("Thread Number: " + threadInfo.threadNumber);

            ReservationMessage response = null;
            RemoteAsyncDelegate remoteDelegate = new RemoteAsyncDelegate(proxy.SendReservation);

            String userName = threadInfo.userName;
            int originalMessageCounter = threadInfo.reservationMessage.messageCounter;

            String threadName = Thread.CurrentThread.Name;

            JacobsonKarels timeoutAlgoritm = User.KnownUsersTimeout[userName];
            timeoutAlgoritm.ModifiedUpdateTimeout();

            int timeout = timeoutAlgoritm.GetTimeout();

            //dissemination
            if (threadName.Length == 3)
            {
                timeout = timeout / 2;
            }

            IAsyncResult result = remoteDelegate.BeginInvoke(threadInfo.reservationMessage, timeout, null, null);
            //colocar timeout dinamico!
            bool responseInTime = result.AsyncWaitHandle.WaitOne(timeout, true);

            Console.WriteLine("Work time is over! Response in time? " + responseInTime);

            //didn't get a response till timeout
            if (responseInTime == false) {
                Console.WriteLine("Timeout! Did not receive any answer!");
                result.AsyncWaitHandle.Close();
                response = new ReservationMessage();
                //add an empty dicionary indicating that the user did not answered
                response.AddSlotResponse(userName, new Dictionary<int, String>());
                result.AsyncWaitHandle.Close();
            }
            else {
                IncrementUserTrust(threadInfo.userName);
                response = remoteDelegate.EndInvoke(result);
                Console.WriteLine("Received: " + response);
                result.AsyncWaitHandle.Close();
            }

            String currentThreadName = Thread.CurrentThread.Name;
            char[] separator = { ' ' };
            String[] splitedThreadName = currentThreadName.Split(separator);
            String currentThreadNumber = splitedThreadName[0];
            int currentTNumber = int.Parse(currentThreadNumber);

            if (threadResponses.ContainsKey(currentTNumber)) {
                threadResponses.Remove(currentTNumber);
            }

            //adds the response the response dicionary
            //if is the retry
            if (originalMessageCounter < 0) {
                if (threadResponses2.ContainsKey(threadInfo.userName))
                    threadResponses2.Remove(threadInfo.userName);
                threadResponses2.Add(threadInfo.userName, response);
            }
            else
                threadResponses.Add(threadInfo.threadNumber, response);
        }

        private void IncrementUserTrust(String userName) {
            Console.WriteLine("Incrementing " + userName + " trust");
            UserView user = null;
            foreach (UserView u in User.KnownUsers.Keys.ToList()) {
                if (u.getName().Equals(userName)) {
                    user = u;
                    break;
                }
            }
            User.KnownUsers[user] += 1; //increment trust
        }


        public ReservationMessage AbortReservation(ReservationMessage message, int timeout) {
            Console.WriteLine("[Calling] AbortReservation");
            Console.WriteLine("Cleaning Thread Lists");

            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("TwoPhaseCommit");
            }

            localThreadList.Clear();
            disseminationThreadList.Clear();
            threadResponses.Clear();
            threadResponses2.Clear();

            int reservationTicketNumber = message.reservation.getTicket();

            List<ReservationSlot> reservationSlotList = message.reservation.getSlotList();

            lock (User.ReservationPerSlot) {
                foreach (ReservationSlot rslot in reservationSlotList) {
                    //remove each reservation from the slots it was assigned to
                    ReservationPerSlot reservationOnTheSlot = User.ReservationPerSlot[rslot.GetNumber()];
                    foreach (Reservation r in reservationOnTheSlot.GetReservations()) {
                        if (r.getTicket().Equals(reservationTicketNumber)) {
                            User.ReservationPerSlot[rslot.GetNumber()].RemoveReservation(message.reservation);

                            if (!User.ReservationPerSlot[rslot.GetNumber()].HasReservations()) {
                                Console.WriteLine("Slot " + rslot.GetNumber() + " does not have any reservation. Set state to Free");
                                lock (User.Calendar) {
                                    User.Calendar[rslot.GetNumber()-1].setFree();
                                }
                            }
                        }
                    }
                }
            }

            return message;
        }

         public ReservationMessage SendReservation(ReservationMessage reservationMessage, int timeout) {
            Console.WriteLine("[Calling] SendReservation");

            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("SendReservation");
            }
            if (User.Calendar == null) {
                Console.WriteLine("Calendar is null...initializing it");
                CreateCalendar();
            }

            Console.WriteLine("Timeout to send message: " + timeout);
            if (User.connected == false) {
                Console.WriteLine("I am not connected. Do not answer call");
                Thread.Sleep(2 * timeout);
                return new ReservationMessage();
            }

            List<ReservationSlot> slotList = reservationMessage.reservation.getSlotList();
            bool isDisseminating = IsDisseminating("SEND_RESERVATION");
            
                if (isDisseminating)
                {
                    Dictionary<int, String> sResponse = ChangeCalendarSlotStatus(slotList, false);

                    ReservationMessage a = new ReservationMessage();
                    a.reservation = reservationMessage.reservation;
                    a.messageCounter *= 2;
                    a.senderName = User.Name;
                    a.AddSlotResponse(User.Name, sResponse);
                    return a;
                }

            List<UserView> unknownUsers = GetUnkownUsersFromReservationUserList(reservationMessage.reservation.getUserList());

            Console.WriteLine("Printing unknown users...");
            foreach (UserView u in unknownUsers) {
                Console.WriteLine("User: " + u.getName());
                AddNewKnownUsers(u.getName(), u.getPort(), u.getIPAddress());
            }

            Console.WriteLine("Print received slots");
            foreach (ReservationSlot s in slotList) {
                Console.WriteLine(s.ToString());

                if (!ExistsReservationInTheList(reservationMessage.reservation, s.GetNumber())) {
                    User.ReservationPerSlot[s.GetNumber()].addReservation(reservationMessage.reservation);
                    Console.WriteLine("Added Reservation: " + User.ReservationPerSlot[s.GetNumber()].GetReservations()[0].ToString());
                }
                else Console.WriteLine("Reservation was already in the slot list. Do not add it");
            }
            Dictionary<int, String> slotResponse = ChangeCalendarSlotStatus(slotList, false);

            ReservationMessage answer = new ReservationMessage();
            answer.reservation = reservationMessage.reservation;
            answer.messageCounter *= 2;
            answer.senderName = User.Name;
            answer.AddSlotResponse(User.Name, slotResponse);


            int lenght = 2;
            if (Thread.CurrentThread.Name == null) {
                Console.WriteLine("Thread Name is null. Original remote invocation thread");
            }
            else {
                String tName = Thread.CurrentThread.Name;
                Console.WriteLine("Thread Name: " + tName);
                char[] separator = { ' ' };
                String[] words = tName.Split(separator);
                lenght = words.Length;
            }

            //Calls the gossip dissemination protocol to send the message to other nodes
            //Before sending the message we need to remove the user that sent the message
            int userListSize = reservationMessage.reservation.getUserList().Count;
            double userListDouble = (double)userListSize;
            double messageCounterDouble = (double)reservationMessage.messageCounter;
            if ((messageCounterDouble <= (userListDouble / 2)) && (userListSize != 1) && messageCounterDouble > 0) {

                if (Thread.CurrentThread.Name == null || lenght == 2) {
                    Console.WriteLine("Disseminate!");
                    reservationMessage.reservation = DeleteSenderFromUserList(reservationMessage);
                    reservationMessage.messageCounter *= 2;
                    PrintReservation(reservationMessage.reservation);
                    DisseminateInformation(reservationMessage, MessageTypeToDisseminate.SEND_RESERVATION, true);

                    //add received answers to the answer we will send
                    foreach (KeyValuePair<int, ReservationMessage> responses in threadResponses) {
                        Console.WriteLine("Thread " + responses.Key + " responses");
                        Console.WriteLine(responses.Value.PrintSlotResponses());

                        foreach (KeyValuePair<String, Dictionary<int, String>> rpsIndex in responses.Value.GetAllResponses()) {
                            answer.AddSlotResponse(rpsIndex.Key, rpsIndex.Value);
                        }
                    }
                }
                else Console.WriteLine("Thread has no permission to disseminate!");
            }

            Console.WriteLine("Answer to send");
            Console.WriteLine(answer.ToString());
            return answer;
        }

         public ReservationMessage TwoPhaseCommit(ReservationMessage message, int timeout)
         {

             Console.WriteLine("[Calling] TwoPhaseCommit");
             lock (User.userLoger)
             {
                 User.userLoger.IncrementNumberOfExchangedMessages("TwoPhaseCommit");
             }
             ReservationMessage response = new ReservationMessage();

                if (User.connected == false) 
                {
                    Console.WriteLine("I am not connect. Do not answer call");
                    Thread.Sleep(2 * timeout);
                    return response;
                }
                
                
                response.response2PC.Add(User.Name, "YES");
                response.messageCounter += 1;
                response.senderName = User.Name;

                bool isDisseminating = IsDisseminating("2PC");

                if (isDisseminating) return response;

                int lenght = 2;
                if (Thread.CurrentThread.Name == null)
                {
                    Console.WriteLine("Thread Name is null. Original remote invocation thread");
                }
                else
                {
                    String tName = Thread.CurrentThread.Name;
                    Console.WriteLine("Thread Name: " + tName);
                    char[] separator = { ' ' };
                    String[] words = tName.Split(separator);
                    lenght = words.Length;
                }

                //Calls the gossip dissemination protocol to send the message to other nodes
                //Before sending the message we need to remove the user that sent the message
                int userListSize = message.reservation.getUserList().Count;
                double userListDouble = (double)userListSize;
                double messageCounterDouble = (double)message.messageCounter;
                if ((messageCounterDouble <= (userListDouble / 2)) && (userListSize != 1) && messageCounterDouble > 0)
                {

                    if (Thread.CurrentThread.Name == null || lenght == 2)
                    {
                        Console.WriteLine("Disseminate!");
                        message.reservation = DeleteSenderFromUserList(message);
                        message.messageCounter *= 2;
                        DisseminateInformation(message, MessageTypeToDisseminate.TWO_PHASE_COMMIT, true);

                        //add received answers to the answer we will send
                        foreach (KeyValuePair<int, ReservationMessage> responses in threadResponses)
                        {
                            
                            foreach (KeyValuePair<String, String> index in responses.Value.response2PC)
                            {
                                if(!response.response2PC.ContainsKey(index.Key))
                                response.response2PC.Add(index.Key,index.Value);
                            }
                        }
                    }
                    else Console.WriteLine("Thread has no permission to disseminate!");
                }
             return response;
         }

        public ReservationMessage CommitReservation(ReservationMessage message, int timeout)
        {
            Console.WriteLine("[Calling] CommitReservation");
            lock (User.userLoger)
            {
                User.userLoger.IncrementNumberOfExchangedMessages("CommitReservation");
            }
            ReservationMessage response=null;
            ReservationSlot slotToCommit = message.slotToCommit;

            lock (User.Calendar)
            {
                User.Calendar[slotToCommit.GetNumber() - 1].SetAssigned();
            }
            return response;
        }

        private bool ExistsReservationInTheList(Reservation reservation, int slotNumber) {
            Console.WriteLine("[Calling] ExistsReservationInTheList");
            Console.WriteLine("Reservation ticket number: " + reservation.getTicket());
            bool result = false;
            ReservationPerSlot reservationsPerTheSlotSelected;

            lock (User.ReservationPerSlot) {
                reservationsPerTheSlotSelected = User.ReservationPerSlot[slotNumber];
            }

            foreach (Reservation r in reservationsPerTheSlotSelected.GetReservations()) {
                if (r.getTicket().Equals(reservation.getTicket())) {
                    Console.WriteLine("Exists. Do not add");
                    result = true; break;
                }
            }

            return result;
        }

        private List<UserView> GetUnkownUsersFromReservationUserList(List<UserView> reservationUserList) {
            Console.WriteLine("[Calling] GetUnkownUsersFromReservationUserList");
            List<UserView> unkownUsers = new List<UserView>();

            lock (User.KnownUsers) {
                foreach (UserView u in reservationUserList) {
                    Console.WriteLine("Checking " + u.getName());

                    if (User.KnownUsers.ContainsKey(u) == false) {
                        Console.WriteLine("Going to add " + u.getName());
                        unkownUsers.Add(u);
                    }
                }
            }
            return unkownUsers;
        }

        private void AddUnkownUsers(List<UserView> unkownUserList) {
            Console.WriteLine("[Calling] AddUnkownUsers");
            lock (User.KnownUsers) {
                foreach (UserView u in unkownUserList) {
                    if (!User.KnownUsers.ContainsKey(u))
                        User.KnownUsers.Add(u, 1);

                    if (!User.KnownUsersTimeout.ContainsKey(u.getName()))
                        User.KnownUsersTimeout.Add(u.getName(), new JacobsonKarels());
                }
            }
        }

        /// <summary>
        /// Deletes the sender from the reservation user's list. This must be done to proceed with the dissemination
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private Reservation DeleteSenderFromUserList(ReservationMessage message) {
            Reservation reservation = message.reservation;
            UserView sender = null;

            foreach (UserView user in User.KnownUsers.Keys.ToList()) {
                if (user.getName().Equals(message.senderName)) {
                    sender = user;
                    break;
                }
            }

            reservation.getUserList().Remove(sender);
            return reservation;
        }

          private List<UserView>  ChooseUsers(Reservation reservation, int messageCounter) {
            Console.WriteLine("[Calling] ChooseUsers");

            int userListSize = reservation.getUserList().Count;
            float numberOfUsersToSelect = (float)System.Math.Round((float)userListSize /(2*(messageCounter)));

            if (userListSize == 1 || userListSize == 0) numberOfUsersToSelect = 1;

            Console.WriteLine("Number of User's to Select: " + numberOfUsersToSelect);
            List<UserView> selectedUsers = new List<UserView>();

            Dictionary<UserView, int> potencialUsers = new Dictionary<UserView, int>(); //contains the list of candidate users
            lock (User.KnownUsers)
            {
                foreach (KeyValuePair<UserView, int> u in User.KnownUsers)
                {

                    Console.WriteLine("Current User: " + u.Key.getName() + " Trust: " + u.Value);
                    //if the user is in the reservation list and I know it, then he is a potential candidates
                    foreach (UserView r in reservation.getUserList())
                    {
                        if (r.getName().Equals(u.Key.getName()) && !r.getName().Equals(User.Name))
                        {
                            potencialUsers.Add(u.Key, u.Value);
                        }
                    }
                }
            }

            for (int i = 1; i <= numberOfUsersToSelect; i++) {
                int totalSum = CalculateTrustSum(potencialUsers);

                Dictionary<UserView, float> updatedPotentialUsers = new Dictionary<UserView, float>();

                foreach (KeyValuePair<UserView, int> u in potencialUsers) {
                    float trustRatio = CalculateTrustRatio(totalSum, u);
                    KeyValuePair<UserView, float> updatedUserwithTrustRatio = new KeyValuePair<UserView, float>(u.Key, trustRatio);
                    updatedPotentialUsers.Add(updatedUserwithTrustRatio.Key, updatedUserwithTrustRatio.Value);
                    //debug
                    Console.WriteLine("User " + updatedUserwithTrustRatio.Key.getName() + " Trust Ratio " + updatedPotentialUsers[updatedUserwithTrustRatio.Key]);
                }

                Dictionary<UserView, Tuple<float, float>> cumulativeDistributionFunction = new Dictionary<UserView, Tuple<float, float>>();

                //debug!
                Console.WriteLine("Printing potential users..");
                foreach (KeyValuePair<UserView, float> p in updatedPotentialUsers) {
                    Console.WriteLine("User: " + p.Key.getName() + " Trust Ratio: " + p.Value);
                }

                cumulativeDistributionFunction = BuildCumulativeDistributionFunction(updatedPotentialUsers);
                PrintUsersInterval(cumulativeDistributionFunction);

                //generate a random number and sees which user cumulative function interval that number is in
                //that user will be the chosen one!

                System.Random randomNumberGenerator = new System.Random();
                float randomNumber = (float)randomNumberGenerator.NextDouble();

                Console.WriteLine("Random Number: " + randomNumber);

                foreach (KeyValuePair<UserView, Tuple<float, float>> u in cumulativeDistributionFunction) {
                    if (Between(randomNumber, u.Value.Item1, u.Value.Item2)) {
                        selectedUsers.Add(u.Key);
                        potencialUsers.Remove(u.Key);
                        break;
                    }
                }
            }//closes first for

            return selectedUsers;
        }

        private bool Between(float number, float lowerLimit, float upperLimit) {
            return number >= lowerLimit && number <= upperLimit ? true : false;
        }

        private int CalculateTrustSum(Dictionary<UserView, int> potentialCandidates) {
            Console.WriteLine("[Calling] CalculateTrustSum");

            int totalTrust = 0;

            foreach (KeyValuePair<UserView, int> u in potentialCandidates) {
                totalTrust += u.Value;
            }
            return totalTrust;
        }

        //Calculates user's trust ratio
        private float CalculateTrustRatio(int trustSum, KeyValuePair<UserView, int> user) {
            Console.WriteLine("[Calling] CalculateTrustRatio");
            return ((float)user.Value) / trustSum;
        }


        private Dictionary<UserView, Tuple<float, float>> BuildCumulativeDistributionFunction(Dictionary<UserView, float> potentialUsers) {
            Console.WriteLine("[Calling] BuildCumulativeDistributionFunction");
            //UserView -> potential user to be selected
            //Tuple<float,float>
            //      - item 1 -> lower interval value
            //      - item 2 -> upper interval value

            Dictionary<UserView, Tuple<float, float>> usersInterval = new Dictionary<UserView, Tuple<float, float>>();
            float previousUpperIntervalValue = 0;

            foreach (KeyValuePair<UserView, float> u in potentialUsers) {
                usersInterval.Add(u.Key, new Tuple<float, float>(previousUpperIntervalValue, previousUpperIntervalValue + u.Value));
                previousUpperIntervalValue += u.Value;
            }

            return usersInterval;
        }

        private void PrintKnownUsers() {
            Console.WriteLine("[Calling] PrintKnownUsers");
            foreach (KeyValuePair<UserView, int> u in User.KnownUsers) {
                Console.WriteLine("User: " + u.Key.getName() + " Trust: " + u.Value);
            }
        }

        /// <summary>
        /// checks if each slot received at leat one Nak
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private List<int> AnyNAKReceived(Dictionary<int, ReservationMessage> content) {
            Console.WriteLine("[Calling] AnyNAKReceived");
            List<int> result = new List<int>();

            foreach (KeyValuePair<int, ReservationMessage> i in content) {
                if (i.Value.GetSlotsWithNAK().Count != 0) {
                    //add slots with NAK's to the result list
                    result.AddRange(i.Value.GetSlotsWithNAK());
                }
            }
            return result;
        }

        /// <summary>
        /// Debug Method
        /// Prints reservation information
        /// </summary>
        /// <param name="reservation"></param>
        private void PrintReservation(Reservation reservation) {
            Console.WriteLine("[Calling] PrintReservation");
            Console.WriteLine("RESERVATION");
            if (reservation.getCreator() == null) {
                Console.WriteLine("[Error]: Creator is Null");
                return;
            }

            if (reservation.getDescription() == null) {
                Console.WriteLine("[Error]: Description is null");
                return;
            }
            Console.WriteLine("Description: " + reservation.getDescription());


            if (reservation.getUserList().Count == 0) {
                Console.WriteLine("[Error]: User list is empty");
                return;
            }
            Console.WriteLine("User List");
            foreach (UserView u in reservation.getUserList()) {
                Console.WriteLine(u.getName());
            }


            if (reservation.getSlotList().Count == 0) {
                Console.WriteLine("[Error]: Slot list is empty");
                return;
            }
            foreach (ReservationSlot s in reservation.getSlotList()) {
                Console.WriteLine(s.ToString());
            }
        }


        private List<UserView> RemoveUsersThatNotAnsweredFromTheUserList(List<UserView> userList, List<UserView> usersThatNotAnsweredList) {
            Console.WriteLine("[Calling] RemoveUsersThatNotAnsweredSendReservationFromTheUserList");
            List<UserView> newUserList = new List<UserView>();

            if (usersThatNotAnsweredList.Count == 0)
                return userList;

            bool isInTheList = false;
            foreach (UserView u in userList) {

                foreach (UserView uNotAnswered in usersThatNotAnsweredList) {
                    if (uNotAnswered.getName().Equals(u.getName())) {
                        isInTheList = true; break;
                    }
                }

                if (isInTheList == false) newUserList.Add(u);
                isInTheList = false;
            }

            return newUserList;
        }

        private List<UserView> UsersThatNotAnsweredTwoPhaseCommit(Reservation reservationn, bool retry)
        {
            List<UserView> listOfUsers = new List<UserView>();
            List<UserView> orignalList = reservationn.getUserList();
            List<UserView> auxList = new List<UserView>();


            if (retry == false)
            {
                Console.WriteLine("No retry");
                foreach (KeyValuePair<int, ReservationMessage> answers in threadResponses)
                {
                    foreach (KeyValuePair<String, String> index in answers.Value.response2PC)
                    {
                        UserView u = GetKnownUserByName(index.Key);
                        //if the dicionary is empty then the user has not answered
                        if (index.Value.Equals("Timeout"))
                        {
                            Console.WriteLine("User " + u.getName() + " has not answered");
                            listOfUsers.Add(u);
                        }
                        else
                            auxList.Add(u); //if the user has answered add it to the aux list
                    }
                }

            }

            else
            {
                Console.WriteLine("Retry.");
                foreach (KeyValuePair<String, ReservationMessage> answers in threadResponses2)
                {
                    foreach (KeyValuePair<String,String> index in answers.Value.response2PC)
                    {
                        UserView u = GetKnownUserByName(index.Key);
                        //if the dicionary is empty then the user has not answered
                        if (index.Value.Equals("Timeout"))
                        {
                            Console.WriteLine("User " + u.getName() + " has not answered");
                            listOfUsers.Add(u);
                        }
                        else
                            auxList.Add(u); //if the user has answered add it to the aux list
                    }
                }
            }

            foreach (UserView u in orignalList)
            {
                if (!IsUserInTheList(u, auxList) && !IsUserInTheList(u, listOfUsers))
                    listOfUsers.Add(u);
            }

            Console.WriteLine("users that have not answered...");
            foreach (UserView uka in listOfUsers)
            {
                Console.WriteLine(uka.getName());
            }

            return listOfUsers;
        }

        private List<UserView> UsersThatNotAnsweredSendReservationCall(Reservation originalReservation, bool retrySend) {
            Console.WriteLine("[Calling] UsersThatNotAnsweredSendReservationCall");

            List<UserView> listOfUsers = new List<UserView>();
            List<UserView> orignalList = originalReservation.getUserList();
            List<UserView> auxList = new List<UserView>();


            if (retrySend == false) {
                Console.WriteLine("No retry");
                foreach (KeyValuePair<int, ReservationMessage> answers in threadResponses) {
                    foreach (KeyValuePair<String, Dictionary<int, String>> index in answers.Value.GetAllResponses()) {
                        UserView u = GetKnownUserByName(index.Key);
                        //if the dicionary is empty then the user has not answered
                        if (index.Value.Count == 0) {
                            Console.WriteLine("User " + u.getName() + " has not answered");
                            listOfUsers.Add(u);
                        }
                        else
                            auxList.Add(u); //if the user has answered add it to the aux list
                    }
                }

            }

            else {
                Console.WriteLine("Retry.");
                foreach (KeyValuePair<String, ReservationMessage> answers in threadResponses2) {
                    foreach (KeyValuePair<String, Dictionary<int, String>> index in answers.Value.GetAllResponses()) {
                        UserView u = GetKnownUserByName(index.Key);
                        //if the dicionary is empty then the user has not answered
                        if (index.Value.Count == 0)
                        {
                            Console.WriteLine("User " + u.getName() + " has not answered");
                            listOfUsers.Add(u);
                        }
                        else
                        {
                            Console.WriteLine(u.getName() + " has answered");
                            auxList.Add(u); //if the user has answered add it to the aux list
                        }
                    }
                }
            }

            foreach (UserView u in orignalList) {
                if (!IsUserInTheList(u, auxList) && !IsUserInTheList(u, listOfUsers))
                    listOfUsers.Add(u);
            }

            Console.WriteLine("users that have not answered...");
            foreach (UserView uka in listOfUsers) {
                Console.WriteLine(uka.getName());
            }

            return listOfUsers;
        }

        private bool IsUserInTheList(UserView u, List<UserView> list) {
            Console.WriteLine("[Calling] IsUserInTheList");
            bool result = false;

            foreach (UserView uv in list) {
                if (u.getName().Equals(uv.getName())) {
                    Console.WriteLine("User " + u.getName() + " is in the list");
                    result = true; break;
                }
            }

            return result;
        }

        UserView GetKnownUserByName(String name) {
            UserView knownUser = null;

            foreach (KeyValuePair<UserView, int> u in User.KnownUsers) {
                if (u.Key.getName().Equals(name)) {
                    knownUser = u.Key;
                    break;
                }
            }

            return knownUser;
        }

        /// <summary>
        /// Debug Method
        /// Prints cumulative distribution function intervals
        /// </summary>
        /// <param name="?"></param>
        private void PrintUsersInterval(Dictionary<UserView, Tuple<float, float>> usersInterval) {
            Console.WriteLine("[Calling] PrintUsersInterval");
            foreach (KeyValuePair<UserView, Tuple<float, float>> ui in usersInterval) {
                Console.WriteLine("User: " + ui.Key.getName());
                Console.WriteLine("Lower Limit: " + ui.Value.Item1);
                Console.WriteLine("Upper Limit: " + ui.Value.Item2);
                Console.WriteLine("");
            }
        }
    }
}
