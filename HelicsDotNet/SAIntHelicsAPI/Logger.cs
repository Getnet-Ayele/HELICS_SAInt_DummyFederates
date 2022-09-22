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
                    using (StreamWriter WriterElec = new StreamWriter(logpath, Append))
                    {
                        WriterElec.WriteLine($"{DateTime.Now} : {message}");
                    }
                    break;
                }
                catch 
                { Thread.Sleep(1); }
            }
        }

    }
}
