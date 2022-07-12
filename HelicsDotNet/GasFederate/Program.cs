using System;
using h = helics;
using System.IO;
using System.Collections.Generic;
using SAIntHelicsLib;
using System.Threading;

namespace HelicsDotNetReceiver
{
    class Program
    {
        static void Main(string[] args)
        {
            // Load Gas Model - DemoAlt_disruption - Compressor Outage
            string netfolder = @"..\..\..\..\Networks\DemoAlt_disruption\";
            string outputfolder = @"..\..\..\..\outputs\DemoAlt_disruption\";

            Directory.CreateDirectory(outputfolder);

            // Load mapping between gas nodes and power plants 
            List<Mapping> MappingList = MappingFactory.GetMappingFromFile(netfolder + "Mapping.txt");

            // Get HELICS version
            Console.WriteLine($"Gas: Helics version ={h.helicsGetVersion()}");

            // Create Federate Info object that describes the federate properties
            Console.WriteLine("Gas: Creating Federate Info");
            var fedinfo = h.helicsCreateFederateInfo();

            // Set core type from string
            Console.WriteLine("Gas: Setting Federate Core Type");
            h.helicsFederateInfoSetCoreName(fedinfo, "Gas Federate Core");
            h.helicsFederateInfoSetCoreTypeFromString(fedinfo, "tcp");

             // Federate init string
            Console.WriteLine("Gas: Setting Federate Info Init String");
            string fedinitstring = "--federates=1";
            h.helicsFederateInfoSetCoreInitString(fedinfo, fedinitstring);

            // Create value federate
            Console.WriteLine("Gas: Creating Value Federate");
            var vfed = h.helicsCreateValueFederate("Gas Federate", fedinfo);
            Console.WriteLine("Gas: Value federate created");

            // Register Publication and Subscription for coupling points
            foreach (Mapping m in MappingList) {
                m.GasPubPth = h.helicsFederateRegisterGlobalTypePublication(vfed, "PUB_Pth_" + m.GasNodeID, "double", "");
                m.GasPubPbar = h.helicsFederateRegisterGlobalTypePublication(vfed, "PUB_Pbar_" + m.GasNodeID, "double", "");

                m.ElectricSub = h.helicsFederateRegisterSubscription(vfed, "PUB_" + m.ElectricGenID, "");

                //Streamwriter for writing iteration results into file
                m.sw = new StreamWriter(new FileStream(outputfolder + m.GasNode.Name + ".txt", FileMode.Create));
                m.sw.WriteLine("tstep \t iter \t P[bar-g] \t Q [sm3/s] \t ThPow [MW] ");
            }

            // Set one second message interval
            double period = 1;
            Console.WriteLine("Electric: Setting Federate Timing");
            h.helicsFederateSetTimeProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_TIME_PERIOD, period);

            // check to make sure setting the time property worked
            double period_set = h.helicsFederateGetTimeProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_TIME_PERIOD);
            Console.WriteLine($"Time period: {period_set}");

            // set number of HELICS time steps based on scenario
            double total_time = 12;
            Console.WriteLine($"Number of time steps in scenario: {total_time}");

            double granted_time = 0;
            double requested_time;

            // set max iteration at 20
            h.helicsFederateSetIntegerProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_INT_MAX_ITERATIONS, 20);
            int iter_max = h.helicsFederateGetIntegerProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_INT_MAX_ITERATIONS);
            Console.WriteLine($"Max iterations: {iter_max}");

            // start initialization mode
            //h.helicsFederateEnterInitializingMode(vfed);
            //Console.WriteLine("Gas: Entering initialization mode");

            // enter execution mode
            h.helicsFederateEnterExecutingMode(vfed);
            Console.WriteLine("Gas: Entering execution mode");

            Int16 Iter = 0;
            List<TimeStepInfo> timestepinfo = new List<TimeStepInfo>();
            List<NotConverged> notconverged = new List<NotConverged>();
            bool IsRepeating = false;
            bool HasViolations = false;

            // Switch to release mode to enable console output to file
#if !DEBUG
            // redirect console output to log file
            FileStream ostrm;
            StreamWriter writer;
            TextWriter oldOut = Console.Out;
            ostrm = new FileStream(outputfolder + "Log_gas_federate.txt", FileMode.OpenOrCreate, FileAccess.Write);
            writer = new StreamWriter(ostrm);
            Console.SetOut(writer);
#endif
            TimeStepInfo currenttimestep = new TimeStepInfo() { timestep = 0, itersteps = 0 };
            NotConverged CurrentDiverged = new NotConverged();
            int TimeStep;
            // this function is called each time the SAInt solver state changes
            for (TimeStep = 0; TimeStep < total_time; TimeStep++)
            {
                // non-iterative time request here to block until both federates are done iterating
                Console.WriteLine($"Requested time {TimeStep}");
                //Console.WriteLine($"Requested time {e.TimeStep}");

                Iter = 0; // Iteration number

                // HELICS time granted 
                granted_time = h.helicsFederateRequestTime(vfed, TimeStep);
                Console.WriteLine($"Granted time: {granted_time}");

                IsRepeating = !IsRepeating;
                HasViolations = true;

                foreach (Mapping m in MappingList)
                {
                    m.lastVal.Clear();
                }
                // Publishing initial available thermal power of zero MW and zero pressure difference
                if (TimeStep == 0)
                {
                    MappingFactory.PublishAvailableThermalPower(granted_time - 1, Iter, MappingList);
                }
                // Set time step info
                currenttimestep = new TimeStepInfo() { timestep = TimeStep, itersteps = 0 };
                timestepinfo.Add(currenttimestep);

                if (IsRepeating)
                {
                    // stop iterating if max iterations have been reached
                    IsRepeating = (Iter < iter_max);

                    if (IsRepeating)
                    {
                        Iter += 1;
                        currenttimestep.itersteps += 1;

                        int helics_iter_status;

                        // iterative HELICS time request                        
                        Console.WriteLine($"Requested time: {TimeStep}, iteration: {Iter}");

                        // HELICS time granted 
                        granted_time = h.helicsFederateRequestTimeIterative(vfed, TimeStep, HelicsIterationRequest.HELICS_ITERATION_REQUEST_FORCE_ITERATION, out helics_iter_status);
                        Console.WriteLine($"Granted time: {granted_time},  Iteration status: {helics_iter_status}");

                        // using an offset of 1 on the granted_time here because HELICS starts at t=1 and SAInt starts at t=0 
                        MappingFactory.PublishAvailableThermalPower(granted_time - 1, Iter, MappingList);

                        // get requested thermal power from connected gas plants, determine if there are violations
                        HasViolations = MappingFactory.SubscribeToRequiredThermalPower(granted_time - 1, Iter, MappingList);

                        if (Iter >= iter_max && HasViolations)
                        {
                            CurrentDiverged = new NotConverged() { timestep = TimeStep, itersteps = Iter };
                            notconverged.Add(CurrentDiverged);
                        }

                        IsRepeating = HasViolations;
                    }

                }
            }

            // request time for end of time + 1: serves as a blocking call until all federates are complete
            requested_time = total_time + 1;
            //Console.WriteLine($"Requested time: {requested_time}");
            
            Console.WriteLine($"Requested time step: {requested_time}");
            granted_time = h.helicsFederateRequestTime(vfed, requested_time);
            Console.WriteLine($"Granted time: {granted_time}");

#if !DEBUG
            // close out log file
            Console.SetOut(oldOut);
            writer.Close();
            ostrm.Close();
#endif

            // finalize federate
            h.helicsFederateFinalize(vfed);
            Console.WriteLine("Gas: Federate finalized");
            h.helicsFederateFree(vfed);
            // If all federates are disconnected from the broker, then close libraries
            h.helicsCloseLibrary();

            using (FileStream fs = new FileStream(outputfolder + "TimeStepInfo_gas_federate.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine("TimeStep \t IterStep");
                    foreach (TimeStepInfo x in timestepinfo)
                    {
                        sw.WriteLine(String.Format("{0}\t{1}", x.timestep, x.itersteps));
                    }
                }

            }
            using (FileStream fs = new FileStream(outputfolder + "NotConverged_gas_federate.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine("Date \t TimeStep \t IterStep");
                    foreach (NotConverged x in notconverged)
                    {
                        sw.WriteLine(String.Format("{0}\t{1}", x.timestep, x.itersteps));
                    }
                }
            }

            // Diverging time steps
            if (notconverged.Count == 0)
                Console.WriteLine("\n Gas: There is no diverging time step.");
            else
            {
                Console.WriteLine("Gas: the solution diverged at the following time steps:");
                foreach (NotConverged x in notconverged)
                { 
                    Console.WriteLine($"Time-step {x.timestep}"); 
                }
                Console.WriteLine($"\n Gas: The total number of diverging time steps = { notconverged.Count }");
            }

            foreach (Mapping m in MappingList)
            {
                if (m.sw != null)
                {
                    m.sw.Flush();
                    m.sw.Close();
                }
            }
      
            var k = Console.ReadKey();
        }        
    }
}
