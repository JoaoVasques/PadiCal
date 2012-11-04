using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes
{
    [Serializable]
    public class UserCalendarSlot
    {

        private enum States { FREE, ACKNOWLEDGED, BOOKED, ASSIGNED }
        private States currentState;
        private int number = -1;

        public UserCalendarSlot(int n)
        {
            this.currentState = States.FREE;
            number = n;
        }

        public override string ToString()
        {
            return "Slot Number: " + number + " State: " + currentState.ToString();
        }

        public void setFree()
        {
            this.currentState = States.FREE;
        }

        public void setAcknowledge()
        {
            this.currentState = States.ACKNOWLEDGED;
        }

        public void setBooked()
        {
            this.currentState = States.BOOKED;
        }

        public void SetAssigned()
        {
            this.currentState = States.ASSIGNED;
        }

        public bool isFree()
        {
            return this.currentState.Equals(States.FREE);
        }

        public bool isAcknowledge()
        {
            return this.currentState.Equals(States.ACKNOWLEDGED);
        }

        public bool isBooked()
        {
            return this.currentState.Equals(States.BOOKED);
        }

        public bool isAssigned()
        {
            return this.currentState.Equals(States.ASSIGNED);
        }

        public String CurrentState()
        {
            return currentState.ToString();
        }

        public int GetNumber()
        {
            return number;
        }
    }

}
