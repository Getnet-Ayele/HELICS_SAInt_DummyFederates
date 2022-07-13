using System;
using h = helics;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using SAIntHelicsLib;

namespace HelicsDotNetSender
{
    class Program
    {
        static void Main(string[] args)
        {

            // Load Electric Model - DemoAlt_disruption - Compressor Outage
            string netfolder = @"..\..\..\..\Networks\DemoAlt_disruption\";
            string outputfolder = @"..\..\..\..\outputs\DemoAlt_disruption\";           

            Directory.CreateDirectory(outputfolder);

            // Get HELICS version
            Console.WriteLine($"Electric: Helics version ={h.helicsGetVersion()}");

            // Create Federate Info object that describes the federate properties
            Console.WriteLine("Electric: Creating Federate Info");
            var fedinfo = h.helicsCreateFederateInfo();

            // Set core type from string
            Console.WriteLine("Electric: Setting Federate Core Type");
            h.helicsFederateInfoSetCoreName(fedinfo, "Electric Federate Core");
            h.helicsFederateInfoSetCoreTypeFromString(fedinfo, "tcp");

            // Federate init string
            Console.WriteLine("Electric: Setting Federate Info Init String");
            string fedinitstring = "--federates=1";
            h.helicsFederateInfoSetCoreInitString(fedinfo, fedinitstring);

            // Create value federate
            Console.WriteLine("Electric: Creating Value Federate");
            var vfed = h.helicsCreateValueFederate("Electric Federate", fedinfo);
            Console.WriteLine("Electric: Value federate created");

            // Register Publication and Subscription for coupling points
            SWIGTYPE_p_void ElectricPub = h.helicsFederateRegisterGlobalTypePublication(vfed, "ElectricPower", "double", "");
            SWIGTYPE_p_void SubToGas = h.helicsFederateRegisterSubscription(vfed, "GasThermalPower", "");
            
            //Streamwriter for writing iteration results into file
            StreamWriter sw = new StreamWriter(new FileStream(outputfolder + "ElectricOutputs.txt", FileMode.Create));
            sw.WriteLine("tstep \t iter \t P[MW] \t Pnew [MW] \t PGMAX [MW]");

            // Set one second message interval
            double period = 1;
            Console.WriteLine("Electric: Setting Federate Timing");
            h.helicsFederateSetTimeProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_TIME_PERIOD, period);

            // check to make sure setting the time property worked
            double period_set = h.helicsFederateGetTimeProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_TIME_PERIOD);
            Console.WriteLine($"Time period: {period_set}");

            // set max iteration at 20
            h.helicsFederateSetIntegerProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_INT_MAX_ITERATIONS, 20);
            int iter_max = h.helicsFederateGetIntegerProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_INT_MAX_ITERATIONS);
            Console.WriteLine($"Max iterations: {iter_max}");

            // start execution mode
            h.helicsFederateEnterExecutingMode(vfed);
            Console.WriteLine("Electric: Entering execution mode");

            // Synthetic data
            double[] P = { 70, 50, 20, 80, 30, 100, 90, 65, 75, 70, 60, 50 };
            // set number of HELICS time steps based on scenario
            double total_time = P.Length;
            Console.WriteLine($"Number of time steps in scenario: {total_time}");

            double granted_time = 0;
            double requested_time;

            // variables to control iterations
            Int16 Iter = 0;
            List<TimeStepInfo> timestepinfo, notconverged, CurrentDiverged = new List<TimeStepInfo>();
            TimeStepInfo currenttimestep = new TimeStepInfo() { timestep = 0, itersteps = 0 };

            int TimeStep;
            bool IsRepeating = false;
            bool HasViolations = false;

            // Switch to release mode to enable console output to file 
#if !DEBUG
            // redirect console output to log file
            FileStream ostrm;
            StreamWriter writer;
            TextWriter oldOut = Console.Out;
            ostrm = new FileStream(outputfolder + "Log_electric_federate.txt", FileMode.OpenOrCreate, FileAccess.Write);
            writer = new StreamWriter(ostrm);
            Console.SetOut(writer);
#endif
           // Main function to be executed on the input data
            for (TimeStep = 0; Iter < total_time; Iter++)
            {
                // non-iterative time request here to block until both federates are done iterating the last time step
                Console.WriteLine($"Requested time {Iter}");

                // HELICS time granted 
                granted_time = h.helicsFederateRequestTime(vfed, TimeStep);
                Console.WriteLine($"Granted time: {granted_time}");
                IsRepeating = !IsRepeating;
                HasViolations = true;

                // Reset nameplate capacity
                foreach (Mapping m in MappingList)
                {
                    m.ElectricGen.PGMAX = m.NCAP;
                    m.ElectricGen.PGMIN = 0;
                    m.lastVal.Clear();
                }

                // Initial publication of thermal power request equivalent to PGMAX for time = 0 and iter = 0;
                if (TimeStep == 0)
                {
                    MappingFactory.PublishRequiredThermalPower(granted_time - 1, TimeStep, MappingList);
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
                        Console.WriteLine($"Granted time: {TimeStep},  Iteration status: {helics_iter_status}");
                        // Using an offset of 1 on the granted_time here because HELICS starts at t=1 and SAInt starts at t=0
                        MappingFactory.PublishRequiredThermalPower(granted_time - 1, Iter, MappingList);
                        // get available thermal power at nodes, determine if there are violations
                        HasViolations = MappingFactory.SubscribeToAvailableThermalPower(granted_time - 1, Iter, MappingList);
                        if (Iter == iter_max && HasViolations)
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
            Console.WriteLine($"Requested time: {requested_time}");
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
            Console.WriteLine("Electric: Federate finalized");
            h.helicsFederateFree(vfed);
            // If all federates are disconnected from the broker, then close libraries
            h.helicsCloseLibrary();

            using (FileStream fs = new FileStream(outputfolder + "TimeStepInfo_electric_federate.txt", FileMode.OpenOrCreate, FileAccess.Write))
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

            using (FileStream fs = new FileStream(outputfolder + "NotConverged_electric_federate.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine("TimeStep \t IterStep");
                    foreach (NotConverged x in notconverged)
                    {
                        sw.WriteLine(String.Format("{0}\t{1}", x.timestep, x.itersteps));
                    }
                }

            }

            // Diverging time steps
            if (notconverged.Count == 0)
                Console.WriteLine("\n Electric: There is no diverging time step.");
            else
            {
                Console.WriteLine("Electric: the solution diverged at the following time steps:");
                foreach (NotConverged x in notconverged)
                {
                    Console.WriteLine($"Time-step {x.timestep}");
                }
                Console.WriteLine($"\n Electric: The total number of diverging time steps = { notconverged.Count }");
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