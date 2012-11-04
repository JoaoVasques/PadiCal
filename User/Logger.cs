using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Log;
using System.IO;

namespace User
{
    [Serializable]
    public class Logger
    {
        private int numberOfExchangedMessages = 0;
        String logName;

        public void SetLogger(String userName)
        {
            logName = CommonTypes.GlobalVariables.LOG_DIR + userName + ".txt";
            if (File.Exists(logName))
            {
                File.Delete(logName);
            }
            File.Create(logName);
        }

        public int GetNumberOfMessages()
        {
            return numberOfExchangedMessages;
        }

        public void IncrementNumberOfExchangedMessages(String functionName)
        {
            numberOfExchangedMessages++;
        }

        public void WriteLogToFile()
        {
            TextWriter outputStream = new StreamWriter(logName);
            outputStream.WriteLine(User.Name.ToUpper() + " log");
            outputStream.WriteLine("---------------------------------------");
            outputStream.WriteLine("Number of exchanged messages: " + numberOfExchangedMessages);
            outputStream.Close();
        }
    }
}
