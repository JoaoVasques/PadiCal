using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes {
    public class JacobsonKarels {
        private int deviation;
        private int estimatedRTT;
        private int timeout;
        private bool firstUseOfTimeOut;

        public JacobsonKarels() {
            deviation = 0;
            estimatedRTT = 0;
            timeout = 4000;
            firstUseOfTimeOut = true;
        }

        public void ModifiedUpdateTimeout() {
            if (firstUseOfTimeOut) {
                firstUseOfTimeOut = false;
                return;
            }

            int sampleRTT = timeout / 4;
            int difference = sampleRTT - estimatedRTT;
            estimatedRTT += estimatedRTT + deviation;
            deviation = deviation + Math.Abs(difference - deviation);
            timeout = estimatedRTT + 4 * deviation;
        }

        public int GetTimeout() {
            return this.timeout;
        }

        /// <summary>
        /// No used do to project specifications -> all processes run on the same machine.
        /// </summary>
        /// <param name="sampleRTT"></param>
        public void UpdateTimeout(int sampleRTT) {
            int difference = sampleRTT - estimatedRTT;
            estimatedRTT += estimatedRTT + deviation;
            deviation = deviation + Math.Abs(difference - deviation);
            timeout = estimatedRTT + 4 * deviation;
        }
    }
}
