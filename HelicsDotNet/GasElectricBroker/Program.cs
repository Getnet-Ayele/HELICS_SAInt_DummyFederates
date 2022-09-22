using System;
using h = helics;
using System.Threading;
using SAIntHelicsLib;
using System.Diagnostics;


namespace GasElectricBroker
{
    class Program
    {
        static void Main(string[] args)
        {
            string initBrokerString = "-f 2 --name=mainbroker";
            Console.WriteLine($"GasElectricBroker: HELICS version ={h.helicsGetVersion()}");

            //Create broker #
            Console.WriteLine("Creating Broker");
            // Creating the log file and writting to it for the first time
           
            Logger.WriteLogBroker("Creating Broker", false);
            var broker = h.helicsCreateBroker("tcp", "", initBrokerString);
            Console.WriteLine("Broker Created"); Logger.WriteLogBroker("Broker Created", true);
            Console.WriteLine("Checking if Broker is connected"); Logger.WriteLogBroker("Checking if Broker is connected", true);
            
            int isconnected = h.helicsBrokerIsConnected(broker);
            Console.WriteLine("Checked if Broker is connected"); Logger.WriteLogBroker("Checked if Broker is connected", true);

            if (isconnected == 1)
            {
                Console.WriteLine("Broker created and connected");
                Logger.WriteLogBroker("Broker created and connected", true);
            }


            while (h.helicsBrokerIsConnected(broker) > 0) Thread.Sleep(1);            
            Console.WriteLine("GasElectric: Broker disconnected");
            Logger.WriteLogBroker("GasElectric: Broker disconnected", true);
            _ = Console.ReadKey();
        }
    }
}
