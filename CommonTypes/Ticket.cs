using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes {
    public class Ticket : MarshalByRefObject {
        public int Number;

        public Ticket(int number) {
            Number = number;
        }

        public void IncrementNumber() {
            Number += 1;
        }
    }
}
