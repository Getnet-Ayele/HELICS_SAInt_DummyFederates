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
            SWIGTYPE_p_void GasPubPth = h.helicsFederateRegisterGlobalTypePublication(vfed, "GasThermalPower", "double", "");
            SWIGTYPE_p_void SubToElectric = h.helicsFederateRegisterSubscription(vfed, "ElectricPower", "");

            //Streamwriter for writing iteration results into file
            StreamWriter sw = new StreamWriter(new FileStream(outputfolder + "GasThermalPowerOutputs.txt", FileMode.Create));
            sw.WriteLine("tstep \t iter \t Pth[MW] \t PthNew [MW]");


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

            // enter execution mode
            h.helicsFederateEnterExecutingMode(vfed);
            Console.WriteLine("Gas: Entering execution mode");

            // Synthetic data
            double[] Pthermal = { 200, 200, 200, 200, 30, 200, 200, 200, 200, 200, 200, 200 };
            double PthermalMax = 300;
            double PthermalMin = 150;
            // set number of HELICS time steps based on scenario
            double total_time = Pthermal.Length;
            Console.WriteLine($"Number of time steps in scenario: {total_time}");

            double granted_time = 0;
            double requested_time;

            Int16 Iter = 0;
            List<TimeStepInfo> timestepinfo = new List<TimeStepInfo>();
            List<TimeStepInfo> notconverged = new List<TimeStepInfo>();
            TimeStepInfo currenttimestep = new TimeStepInfo() { timestep = 0, itersteps = 0 };
            TimeStepInfo CurrentDiverged = new TimeStepInfo() { timestep = 0, itersteps = 0 };

            int TimeStep;
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

               // Publishing initial available thermal power of zero MW and zero pressure difference
                if (TimeStep == 0)
                {
                    MappingFactory.PublishGasThermalPower(granted_time - 1, Iter);
                }
                // Set time step info
                currenttimestep.timestep = TimeStep;
                currenttimestep.itersteps = 0;
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
                        MappingFactory.PublishGasThermalPower(granted_time - 1, Iter);

                        // get requested thermal power from connected gas plants, determine if there are violations
                        HasViolations = MappingFactory.SubscribeToElectricPower(granted_time - 1, Iter);

                        if (Iter >= iter_max && HasViolations)
                        {
                            CurrentDiverged.timestep = TimeStep;
                            CurrentDiverged.itersteps = Iter;
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
                using (StreamWriter sw2 = new StreamWriter(fs))
                {
                    sw2.WriteLine("TimeStep \t IterStep");
                    foreach (TimeStepInfo x in timestepinfo)
                    {
                        sw2.WriteLine(String.Format("{0}\t{1}", x.timestep, x.itersteps));
                    }
                }

            }
            using (FileStream fs = new FileStream(outputfolder + "NotConverged_gas_federate.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw2 = new StreamWriter(fs))
                {
                    sw2.WriteLine("Date \t TimeStep \t IterStep");
                    foreach (TimeStepInfo x in notconverged)
                    {
                        sw2.WriteLine(String.Format("{0}\t{1}", x.timestep, x.itersteps));
                    }
                }
            }

            // Diverging time steps
            if (notconverged.Count == 0)
                Console.WriteLine("\n Gas: There is no diverging time step.");
            else
            {
                Console.WriteLine("Gas: the solution diverged at the following time steps:");
                foreach (TimeStepInfo x in notconverged)
                {
                    Console.WriteLine($"Time-step {x.timestep}");
                }
                Console.WriteLine($"\n Gas: The total number of diverging time steps = { notconverged.Count }");
            }

            var k = Console.ReadKey();
        }
    }
}
