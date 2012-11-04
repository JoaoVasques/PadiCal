using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonTypes;

namespace PuppetMaster
{
    [Serializable]
    class ReservationView
    {
        private String description;
        private List<UserView> participants;
        private List<ReservationSlot> slots;
        private String creator;
        private int ticketNumber;

        public ReservationView(String d, List<UserView> p, List<ReservationSlot> s, String c)
        {
            this.description = d;
            this.participants = p;
            this.slots = s;
            this.creator = c;
            this.ticketNumber = -1;
        }

        public String getDescription()
        {
            return this.description;
        }

        public List<UserView> getParticipants()
        {
            return this.participants;
        }

        public List<ReservationSlot> getSlotList()
        {
            return this.slots;
        }

        public String getCreator()
        {
            return this.creator;
        }

        public void setTicketNumber(int number)
        {
            if (this.ticketNumber.Equals(-1))
                this.ticketNumber = number;
            return;
        }
    }
}
