using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes
{
    [Serializable]
    public class ReservationSlot
    {
        private enum State { INITIATED, TENTATIVELY_BOOKED, COMMITED, ABORTED }
        private State currentState;
        private int slotNumber;

        public ReservationSlot(int snumber)
        {
            this.currentState = State.INITIATED;
            this.slotNumber = snumber;
        }

        public int GetNumber()
        {
            return slotNumber;
        }
        
        public override String ToString()
        {
            return "Slot number: " + slotNumber + "\nState: " + currentState.ToString() + "\n";
        }

        public void setInitiated()
        {
            this.currentState = State.INITIATED;
        }

        public void setTentativelyBooked()
        {
            this.currentState = State.TENTATIVELY_BOOKED;
        }

        public void setCommited()
        {
            this.currentState = State.COMMITED;
        }

        public void SetAborted()
        {
            this.currentState = State.ABORTED;
        }


        public bool isTentativelyBooked()
        {
            return this.currentState.Equals(State.TENTATIVELY_BOOKED);
        }

        public bool isCommited()
        {
            return this.currentState.Equals(State.COMMITED);
        }

        public bool isAborted()
        {
            return this.currentState.Equals(State.ABORTED);
        }
    }
}
