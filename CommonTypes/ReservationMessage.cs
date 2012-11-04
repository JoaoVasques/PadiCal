using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes
{
    [Serializable]
    public class ReservationMessage
    {
        public int messageCounter { set; get; }
        public Reservation reservation { set; get; }
        public String senderName {set; get;}
        public ReservationSlot proposedSlot {set;get;}
        public ReservationSlot slotToCommit {set;get;}

        /// <summary>
        /// Key -> userName
        /// Value -> 2PC answer ["YES";"NO"]
        /// </summary>
        public Dictionary<String, String> response2PC = new Dictionary<string, string>();
        
        /// <summary>
        /// Main key (String) -> userName
        /// Secondary key (int) -> slot number
        /// Secondary value (String) -> response
        /// </summary>
        private Dictionary<String, Dictionary<int, String>> slotResponses = null;//new Dictionary<string, Dictionary<int, string>>();
        
        /// <summary>
        /// Key - userName
        /// Value - Answer (Ack or Nak)
        /// </summary>
        private Dictionary<String, String> proposedSlotResponse = new Dictionary<string, string>();

        public ReservationMessage()
        {
            slotResponses = new Dictionary<string, Dictionary<int, string>>();
            messageCounter = 1;
        }

        public Dictionary<String, Dictionary<int, String>> GetSlotResponses()
        {
            return this.slotResponses;
        }

        public void AddSlotResponse(String userName, Dictionary<int,String> slotResponses)
        {
            if(!this.slotResponses.ContainsKey(userName))
                this.slotResponses.Add(userName, slotResponses);
        }

        public String PrintSlotResponses()
        {
            String slotResponses = "Slot Responses\n";

            foreach (KeyValuePair<String, Dictionary<int, String>> responses in this.slotResponses)
            {
                String userName = responses.Key.ToString();
                String slotR = "Responses\n";

                    foreach(KeyValuePair<int,String> sr in this.slotResponses[userName])
                    {
                        slotR = slotR + "Slot " + sr.Key + " : " + sr.Value + "\n";
                    }
                    slotResponses = slotResponses + userName + slotR + "\n";
            }

            return slotResponses;
        }

        public void AddProposedSlotResponse(String user, String response)
        {
            if(!this.proposedSlotResponse.ContainsKey(user))
                this.proposedSlotResponse.Add(user, response);
        }

        public String GetProposedSlotResponseByUser(String userName)
        {
            return this.proposedSlotResponse[userName];
        }

        public Dictionary<String,String> GetAllProposedSlotResponses()
        {
            return this.proposedSlotResponse;
        }

        public bool TimeoutResponse()
        {
            return reservation == null && slotResponses == null ? true : false;
        }

        public override string ToString()
        {
            String slotResponses = this.PrintSlotResponses();
            return "Message Counter: " + messageCounter + "\nSlot responses:\n" + slotResponses;
        }

        public Dictionary<String,Dictionary<int, String>> GetAllResponses()
        {
            return this.slotResponses;
        }

        public List<int> GetSlotsWithNAK()
        {
            Console.WriteLine("-ReservationMessage [Calling] GetSlotsWithNAK");
            List<int> result = new List<int>();

            foreach (String userName in slotResponses.Keys)
            {
                Console.WriteLine("User " + userName + " responses.");
                foreach (KeyValuePair<int,String> resp in slotResponses[userName])
                {
                    //If the user answered Nak check if that same slot is in the list.
                    //If it is not on the list, add it

                    Console.WriteLine("Slot " + resp.Key + " Response: " + resp.Value);
                    if (resp.Value.Equals("NAK"))
                    {
                        Console.WriteLine("Slot " + resp.Key + " is Nak");
                        if (!result.Contains(resp.Key))
                            result.Add(resp.Key);
                    }
                }
            }

            return result;
        }
    }
}
