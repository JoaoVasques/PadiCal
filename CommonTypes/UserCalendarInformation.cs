using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes
{
    [Serializable]
    public class UserCalendarInformation
    {
        private List<UserCalendarSlot> bookedSlots = new List<UserCalendarSlot>();
        private List<UserCalendarSlot> assignedSlots = new List<UserCalendarSlot>();
        private int numberOfFreeSlots = 0;
        private int numberOfAcknowledgeSlots = 0;

        public void AddBookedSlot(UserCalendarSlot slot)
        {
            bookedSlots.Add(slot);
        }

        public void AddAssignedSlot(UserCalendarSlot slot)
        {
            assignedSlots.Add(slot);
        }

        public void IncrementNumberOfFreeSlots()
        {
            numberOfFreeSlots += 1;
        }

        public void IncrementNumberOfAcknowledgeSlots()
        {
            numberOfAcknowledgeSlots += 1;
        }

        public List<UserCalendarSlot> GetBookedSlots()
        {
            return bookedSlots;
        }

        public List<UserCalendarSlot> GetAssignedSlots()
        {
            return assignedSlots;
        }

        public int GetNumberOfFreeSlots()
        {
            return numberOfFreeSlots;
        }

        public int GetNumberOfAcknowledgeSlots()
        {
            return numberOfAcknowledgeSlots;
        }
    }
}
