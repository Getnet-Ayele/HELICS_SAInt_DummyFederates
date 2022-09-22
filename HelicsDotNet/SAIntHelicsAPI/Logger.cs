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
        public static void WriteLogGas(string message, bool Append)
        {
            string logpath = @"..\..\..\..\outputs\LogGas.txt";
            using (StreamWriter WriterGas = new StreamWriter(logpath, Append))
            {
                WriterGas.WriteLine($"{DateTime.Now} : {message}");
            }
        }
        public static void WriteLogElec(string message, bool Append)
        {
            string logpath = @"..\..\..\..\outputs\LogElectric.txt";
            using (StreamWriter WriterElec = new StreamWriter(logpath, Append))
            {
                WriterElec.WriteLine($"{DateTime.Now} : {message}");
            }
        }

        public static void WriteLogBroker(string message, bool Append)
        {
            string logpath = @"..\..\..\..\outputs\LogBroker.txt";
            using (StreamWriter WriterBroker = new StreamWriter(logpath, Append))
            {
                WriterBroker.WriteLine($"{DateTime.Now} : {message}");
            }
        }
    }
}
