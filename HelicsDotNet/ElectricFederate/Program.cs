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
           
            //// Load Electric Model - DemoAlt_disruption - Compressor Outage
            string outputfolder = @"..\..\..\..\outputs\";
            Directory.CreateDirectory(outputfolder);

            // Get HELICS version
            Console.WriteLine($"Electric: HELICS version ={h.helicsGetVersion()}");
            Logger.WriteLog($"Electric: HELICS version ={h.helicsGetVersion()}", true);

            // Create Federate Info object that describes the federate properties
            Console.WriteLine("Electric: Creating Federate Info");
            Logger.WriteLog("Electric: Creating Federate Info", true);
            var fedinfo = h.helicsCreateFederateInfo();

            // Set core type from string
            Console.WriteLine("Electric: Setting Federate Core Type");
            Logger.WriteLog("Electric: Setting Federate Core Type", true);
            h.helicsFederateInfoSetCoreName(fedinfo, "Electric Federate Core");
            h.helicsFederateInfoSetCoreTypeFromString(fedinfo, "tcp");

            // Federate init string
            Console.WriteLine("Electric: Setting Federate Info Init String");
            Logger.WriteLog("Electric: Setting Federate Info Init String", true);
            string fedinitstring = "--federates=1";
            h.helicsFederateInfoSetCoreInitString(fedinfo, fedinitstring);

            // Create value federate
            Console.WriteLine("Electric: Creating Value Federate");
            Logger.WriteLog("Electric: Creating Value Federate", true);
            var vfed = h.helicsCreateValueFederate("Electric Federate", fedinfo);
            Console.WriteLine("Electric: Value federate created");
            Logger.WriteLog("Electric: Value federate created", true);

            // Register Publication and Subscription for coupling points
            SWIGTYPE_p_void ElectricPub = h.helicsFederateRegisterGlobalTypePublication(vfed, "ElectricPower", "double", "");
            SWIGTYPE_p_void SubToGas = h.helicsFederateRegisterSubscription(vfed, "GasThermalPower", "");
            SWIGTYPE_p_void SubToGasMin = h.helicsFederateRegisterSubscription(vfed, "GasThermalPowerMin", "");

            // Set one second message interval
            double period = 1;
            Console.WriteLine("Electric: Setting Federate Timing");
            Logger.WriteLog("Electric: Setting Federate Timing", true);
            h.helicsFederateSetTimeProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_TIME_PERIOD, period);

            // check to make sure setting the time property worked
            double period_set = h.helicsFederateGetTimeProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_TIME_PERIOD);
            Console.WriteLine($"Electric: Time period: {period_set}");
            Logger.WriteLog($"Electric: Time period: {period_set}", true);

            // set max iteration at 20
            h.helicsFederateSetIntegerProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_INT_MAX_ITERATIONS, 10);
            int iter_max = h.helicsFederateGetIntegerProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_INT_MAX_ITERATIONS);
            Console.WriteLine($"Electric: Max iterations: {iter_max}");
            Logger.WriteLog($"Electric: Max iterations: {iter_max}", true);

            // start execution mode
            h.helicsFederateEnterExecutingMode(vfed);
            Console.WriteLine("Electric: Entering execution mode");
            Logger.WriteLog("Electric: Entering execution mode", true);

            // Synthetic data
            double[] P = { 70, 50, 20, 80, 30, 100, 90, 65, 75, 70, 60, 50 };
            //double[] P = { 70, 50, 20, 30};
            double[] POld = new double[P.Length];
            double[] PNew = new double[P.Length];
            Array.Copy(P, POld, P.Length);
            Array.Copy(P, PNew, P.Length);
            // set number of HELICS time steps based on scenario
            double total_time = P.Length;
            Console.WriteLine($"Electric: Number of time steps in scenario: {total_time}");
            Logger.WriteLog($"Electric: Number of time steps in scenario: {total_time}", true);

            double granted_time = 0;
            double requested_time;

            //var iter_flag = HelicsIterationRequest.HELICS_ITERATION_REQUEST_ITERATE_IF_NEEDED;
            var iter_flag = HelicsIterationRequest.HELICS_ITERATION_REQUEST_FORCE_ITERATION;
            Logger.WriteLog($"\nElectric: iteration flag: {iter_flag}", true);

            // Artificial delay
            int delay = 0;
            int AfterTimeStep = 0;
            Logger.WriteLog($"Electric delay = {delay}", true); 

            // variables to control iterations
            short Iter = 0;
            List<TimeStepInfo> timestepinfo = new List<TimeStepInfo>();
            List<TimeStepInfo> notconverged = new List<TimeStepInfo>();
            TimeStepInfo CurrentDiverged = new TimeStepInfo();
            TimeStepInfo currenttimestep = new TimeStepInfo() { timestep = 0, itersteps = 0 };

            List<double> ElecLastVal = new List<double>();

            int TimeStep;
            bool IsRepeating;
            bool HasViolations;
            int helics_iter_status = 3;

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
            for (TimeStep = 1; TimeStep < total_time+1; TimeStep++)
            {
                //if (TimeStep == 1)
                //{
                MappingFactory.PublishElectricPower(TimeStep, 0, P[TimeStep - 1], ElectricPub);
                //}

                // non-iterative time request here to block until both federates are done iterating the last time step
                Console.WriteLine($"\nElectric: Requested Time Step {TimeStep}");
                Logger.WriteLog($"\nElectric: Requested Time Step {TimeStep}", true);                

                // HELICS time granted 
                granted_time = h.helicsFederateRequestTime(vfed, TimeStep);
                Console.WriteLine($"Electric: Granted Time: {granted_time}, Iteration Status: { helics_iter_status}");
                Logger.WriteLog($"Electric: Granted Time: {granted_time}, Iteration Status: { helics_iter_status}\n", true);

                IsRepeating = true;
                HasViolations = true;
                
                Iter = 0; // Iteration number

               ElecLastVal.Clear();  // clear the list from previous time step

                // Initial publication of thermal power request equivalent to PGMAX for time = 0 and iter = 0;
               //MappingFactory.PublishElectricPower(TimeStep, Iter, P[TimeStep - 1], ElectricPub);

                // Set time step info
                currenttimestep = new TimeStepInfo() { timestep = TimeStep, itersteps = 0 };
                timestepinfo.Add(currenttimestep);

                while (IsRepeating && Iter<iter_max)
                {

                    // Artificial delay
                    if (TimeStep > AfterTimeStep)
                    {
                        //Thread.Sleep(delay);
                        for (int i = 1; i< delay*1000; i++)
                        {
                            double JustForDelay = Math.Sqrt(i * i * Math.Exp(i));
                        }
                    }

                    // stop iterating if max iterations have been reached                    
                    Iter += 1;
                    currenttimestep.itersteps += 1;

                    if (Iter > 1)
                    {
                        Console.WriteLine($"Electric: Requested Time Iterative: {TimeStep}, iteration: {Iter}");
                        Logger.WriteLog($"Electric: Requested Time Iterative: {TimeStep}, iteration: {Iter}", true);

                        granted_time = h.helicsFederateRequestTimeIterative(vfed, TimeStep, iter_flag, out helics_iter_status);

                        Console.WriteLine($"Electric: Granted Time Iterative: {granted_time},  Iteration Status: {helics_iter_status}");
                        Logger.WriteLog($"Electric: Granted Time Iterative: {granted_time},  Iteration Status: {helics_iter_status}", true);
                    }

                //double GasMin = h.helicsInputGetDouble(SubToGasMin);
                // Console.WriteLine(String.Format("Electric-Received: Time {0} \t iter {1} \t PthgMargin = {2:0.0000} [MW]", TimeStep, Iter, GasMin));
                //Logger.WriteLog(String.Format("Electric-Received: Time {0} \t iter {1} \t PthgMargin = {2:0.0000} [MW]", TimeStep, Iter, GasMin), true);

                //MappingFactory.PublishElectricPower(TimeStep, Iter, P[TimeStep - 1], ElectricPub);
                // get available thermal power at nodes, determine if there are violations
                // granted_time = h.helicsFederateRequestTimeIterative(vfed, TimeStep, iter_flag, out helics_iter_status);
                HasViolations = MappingFactory.SubscribeToGasThermalPower(TimeStep, Iter, P[TimeStep-1], SubToGas, ElecLastVal);

                    Logger.WriteLog($"Electric - HasVioltation?: {HasViolations}",true);
                    Console.WriteLine($"Electric - HasVioltation?: {HasViolations}");

                    if (HasViolations)
                    {
                        //if (GasMin < 0 && P[TimeStep - 1] > 10)
                        if ( P[TimeStep - 1] > 80)
                        {
                            P[TimeStep - 1] -= 5;
                        }
                    }

                    PNew[TimeStep-1] = P[TimeStep-1];

                    if ((int)iter_flag == 1 && Iter == iter_max && HasViolations)
                    {
                        CurrentDiverged = new TimeStepInfo() { timestep = TimeStep, itersteps = Iter };
                        notconverged.Add(CurrentDiverged);
                    }

                    MappingFactory.PublishElectricPower(TimeStep, Iter, P[TimeStep-1], ElectricPub);

                    IsRepeating = HasViolations;

                    if (helics_iter_status != (int)HelicsIterationResult.HELICS_ITERATION_RESULT_ITERATING)
                    {
                        //Iter--;
                        if (HasViolations && (int)iter_flag == 2)
                        {
                            CurrentDiverged = new TimeStepInfo() { timestep = TimeStep, itersteps = Iter };
                            notconverged.Add(CurrentDiverged);
                        }
                        break;
                    }
                }
            }

            // request time for end of time + 1: serves as a blocking call until all federates are complete
            requested_time = total_time+1;
            Console.WriteLine($"\nElectric: Requested Time: {requested_time}");
            Logger.WriteLog($"\nElectric: Requested Time: {requested_time}", true);
            h.helicsFederateRequestTime(vfed, requested_time);
            

#if !DEBUG
            // close out log file
            Console.SetOut(oldOut);
            writer.Close();
            ostrm.Close();
#endif

            // finalize federate
            h.helicsFederateFinalize(vfed);
            Console.WriteLine("Electric: Federate finalized");
            Logger.WriteLog("Electric: Federate finalized", true);
            h.helicsFederateFree(vfed);
            // If all federates are disconnected from the broker, then close libraries
            h.helicsCloseLibrary();

            using (FileStream fs = new FileStream(outputfolder + "TimeStepInfo_electric_federate.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw2 = new StreamWriter(fs))
                {
                    sw2.WriteLine("TimeStep \t IterStep");
                    foreach (TimeStepInfo x in timestepinfo)
                    {
                        sw2.WriteLine(String.Format("{0}\t\t\t\t{1}", x.timestep, x.itersteps));
                    }
                }

            }

            using (FileStream fs = new FileStream(outputfolder + "NotConverged_electric_federate.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw2 = new StreamWriter(fs))
                {
                    sw2.WriteLine("TimeStep \tIterStep");
                    foreach (TimeStepInfo x in notconverged)
                    {
                        sw2.WriteLine(String.Format("{0}\t\t\t{1}", x.timestep, x.itersteps));
                    }
                }

            }

            using (FileStream fs = new FileStream(outputfolder + "PelectricAndPelectricNew.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw2 = new StreamWriter(fs))
                {
                    sw2.WriteLine("TimeStep\t PelectricOld[MW]\t PelectricNew[MW]");
                    for (int t = 0; t < total_time; t++)
                    {
                        sw2.WriteLine(String.Format("{0}\t\t\t\t{1:0.00}\t\t\t\t{2:0.00}", t+1, POld[t], PNew[t]));
                    }
                }
            }


            // Diverging time steps
            if (notconverged.Count == 0)
            {
                Console.WriteLine("\n Electric: There is no diverging time step.");
                Logger.WriteLog("Electric: There is no diverging time step.", true);
            }
            else
            {
                Console.WriteLine("Electric: the solution diverged at the following time steps:");
                Logger.WriteLog("Electric: the solution diverged at the following time steps:", true);
                foreach (TimeStepInfo x in notconverged)
                {
                    Console.WriteLine($"Electric: Time-step {x.timestep}");
                    Logger.WriteLog($"Electric: Time-step {x.timestep}", true);
                }
                Console.WriteLine($"\n Electric: The total number of diverging time steps = { notconverged.Count }");
                Logger.WriteLog($"Electric: The total number of diverging time steps = { notconverged.Count }", true);
            }

            var k = Console.ReadKey();
        }
    }
}