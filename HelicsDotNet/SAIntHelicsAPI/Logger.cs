using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SAIntHelicsLib
{
    public static class Logger
    {
        public static void WriteLog(string message, bool Append)
        {
            while (true)
            {
                try
                {
                    string logpath = @"..\..\..\..\outputs\Log.txt";
                    using (StreamWriter WriterLog = new StreamWriter(logpath, Append))
                    {
                        //WriterLog.WriteLine($"{DateTime.Now} : {message}");
                        WriterLog.WriteLine(message);
                    }
                    break;
                }
                catch 
                { Thread.Sleep(1); }
            }
        }

    }
}
