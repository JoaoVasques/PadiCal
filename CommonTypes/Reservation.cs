using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes
{
    [Serializable]
    public class Reservation
    {
        private String description;
        private List<UserView> userList;
        private List<ReservationSlot> slotList;
        private String creator;
        private int ticketValue;

        public Reservation(String description, List<UserView> userList, List<ReservationSlot> slotList, String creator, int ticket)
        {
            this.description = description;
            this.userList = userList;
            this.slotList = slotList;
            this.creator = creator;
            this.ticketValue = ticket;
        }

        public bool areEqual(Reservation r)
        {
            return this.description.Equals(r.description) && this.creator.Equals(r.creator) && this.ticketValue.Equals(r.ticketValue);
        }

        public int getTicket()
        {
            return this.ticketValue;
        }

        public String getDescription()
        {
            return this.description;
        }

        public List<UserView> getUserList()
        {
            return this.userList;
        }

        public List<ReservationSlot> getSlotList()
        {
            return this.slotList;
        }

        public void SetUserList(List<UserView> ul)
        {
            this.userList = ul;
        }

        public String getCreator()
        {
            return this.creator;
        }
    }
}
