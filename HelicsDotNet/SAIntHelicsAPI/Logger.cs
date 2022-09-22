using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAIntHelicsLib
{
    public static class Logger
    {
        public static void WriteLog(string message, bool Append)
        {
            string logpath = @"..\..\..\..\outputs\Log.txt";
            using (StreamWriter writer = new StreamWriter(logpath, Append))
            {
                writer.WriteLine($"{DateTime.Now} : {message}");
            }
        }
    }
}
