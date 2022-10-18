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
            string outputfolder = @"..\..\..\..\outputs\";
            Directory.CreateDirectory(outputfolder);

            // Get HELICS version
            Console.WriteLine($"Gas: HELICS version ={h.helicsGetVersion()}");
            Logger.WriteLog($"Gas: HELICS version ={h.helicsGetVersion()}", true);

            // Create Federate Info object that describes the federate properties
            Console.WriteLine("Gas: Creating Federate Info"); Logger.WriteLog("Gas: Creating Federate Info", true);
            var fedinfo = h.helicsCreateFederateInfo();

            // Set core type from string
            Console.WriteLine("Gas: Setting Federate Core Type"); Logger.WriteLog("Gas: Setting Federate Core Type", true);
            h.helicsFederateInfoSetCoreName(fedinfo, "Gas Federate Core");
            h.helicsFederateInfoSetCoreTypeFromString(fedinfo, "tcp");

            // Federate init string
            Console.WriteLine("Gas: Setting Federate Info Init String"); Logger.WriteLog("Gas: Setting Federate Info Init String", true);
            string fedinitstring = "--federates=1";
            h.helicsFederateInfoSetCoreInitString(fedinfo, fedinitstring);

            // Create value federate
            Console.WriteLine("Gas: Creating Value Federate"); Logger.WriteLog("Gas: Creating Value Federate", true);
            var vfed = h.helicsCreateValueFederate("Gas Federate", fedinfo);
            Console.WriteLine("Gas: Value federate created"); Logger.WriteLog("Gas: Value federate created", true);

            // Register Publication and Subscription for coupling points
            SWIGTYPE_p_void GasPubPth = h.helicsFederateRegisterGlobalTypePublication(vfed, "GasThermalPower", "double", "");
            SWIGTYPE_p_void GasPubPthMax = h.helicsFederateRegisterGlobalTypePublication(vfed, "GasThermalPowerMin", "double", "");
            SWIGTYPE_p_void SubToElectric = h.helicsFederateRegisterSubscription(vfed, "ElectricPower", "");

            // Set one second message interval
            double period = 1;
            Console.WriteLine("Gas: Setting Federate Timing");
            Logger.WriteLog("Gas: Setting Federate Timing", true);
            h.helicsFederateSetTimeProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_TIME_PERIOD, period);

            // check to make sure setting the time property worked
            double period_set = h.helicsFederateGetTimeProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_TIME_PERIOD);
            Console.WriteLine($"Gas: Time period: {period_set}");
            Logger.WriteLog($"Gas: Time period: {period_set}", true);

            // set max iteration at 20
            h.helicsFederateSetIntegerProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_INT_MAX_ITERATIONS, 20);
            int iter_max = h.helicsFederateGetIntegerProperty(vfed, (int)HelicsProperties.HELICS_PROPERTY_INT_MAX_ITERATIONS);
            Console.WriteLine($"Gas: Max iterations: {iter_max}");
            Logger.WriteLog($"Gas: Max iterations: {iter_max}", true);

            // Synthetic data
            double[] Pthermal = { 200, 200, 200, 200, 30, 200, 200, 200, 200, 200, 200, 200 };
            //double[] Pthermal = { 200, 200, 200, 30};
            double[] PthermalOld = new double[Pthermal.Length];
            double[] PthermalNew = new double[Pthermal.Length];
            Array.Copy(Pthermal, PthermalOld,Pthermal.Length);
            Array.Copy(Pthermal, PthermalNew, Pthermal.Length);
           
            double PthermalMax = 1000;
            double PthermalMin = 100;
            // set number of HELICS time steps based on scenario
            double total_time = Pthermal.Length;
            Console.WriteLine($"Gas: Number of time steps in scenario: {total_time}"); 
            Logger.WriteLog($"Gas: Number of time steps in scenario: {total_time}", true);

            short Iter = 0;
            List<TimeStepInfo> timestepinfo = new List<TimeStepInfo>();
            List<TimeStepInfo> notconverged = new List<TimeStepInfo>();
            TimeStepInfo currenttimestep = new TimeStepInfo() { timestep = 0, itersteps = 0 };
            TimeStepInfo CurrentDiverged = new TimeStepInfo() { timestep = 0, itersteps = 0 };

            List<double> GasLastVal = new List<double>();

            int TimeStep = 0;
            bool HasViolations;
            int helics_iter_status = 3;

            double granted_time = 0;
            double requested_time;

            var iter_flag = HelicsIterationRequest.HELICS_ITERATION_REQUEST_ITERATE_IF_NEEDED;
            Logger.WriteLog($"\nGas: iteration flag: {iter_flag}", true);

            // Initialization and entering iterative execution mode
            Console.WriteLine("\nGas: Entering Initialization Mode");
            Logger.WriteLog("\nGas: Entering Iitialization Mode", true);
            h.helicsFederateEnterInitializingMode(vfed);
            MappingFactory.PublishGasThermalPower(TimeStep, Iter, 1000, GasPubPth, PthermalMax, GasPubPthMax);

            while (true)
            {
                //h.helicsFederateEnterExecutingMode(vfed);
               
                Console.WriteLine("\nGas: Entering Iterative Execution Mode\n");
                Logger.WriteLog("\nGas: Entering Iterative Execution Mode\n", true);
                HelicsIterationResult itr_status = h.helicsFederateEnterExecutingModeIterative(vfed, iter_flag);

                if (itr_status == HelicsIterationResult.HELICS_ITERATION_RESULT_NEXT_STEP)
                {
                    Console.WriteLine($"Gas: Time Step {TimeStep} Initialization Completed!");
                    Logger.WriteLog($"Gas: Time Step {TimeStep} Initialization Completed!", true);

                    break;
                }

                double val = h.helicsInputGetDouble(SubToElectric);
                Console.WriteLine(String.Format("Gas-Received: Time {0} \t iter {1} \t Pthe = {2:0.0000} [MW]", TimeStep, Iter, val));
                Logger.WriteLog(String.Format("Gas-Received: Time {0} \t iter {1} \t Pthe = {2:0.0000} [MW]", TimeStep, Iter, val), true);

                if (val == 80)
                {
                    continue;
                }
                else
                {
                    Iter += 1;
                    MappingFactory.PublishGasThermalPower(TimeStep, Iter, 1000, GasPubPth, PthermalMax, GasPubPthMax);
                }
            }


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
                double val = 0;

                HasViolations = true;

                Iter = 0; // Iteration number

                GasLastVal.Clear();  // clear the list from previous time step    

                // Set time step info
                currenttimestep = new TimeStepInfo() { timestep = TimeStep, itersteps = 0 };
                timestepinfo.Add(currenttimestep);

                MappingFactory.PublishGasThermalPower(TimeStep, Iter, Pthermal[TimeStep], GasPubPth, (PthermalMax - val), GasPubPthMax);

                while (Iter < iter_max)
                {                                       
                    currenttimestep.itersteps += 1;

                     HasViolations = false;

                    Console.WriteLine($"Gas: Requested Time Iterative: {TimeStep}, iteration: {Iter}");
                    Logger.WriteLog($"Gas: Requested Time Iterative: {TimeStep}, iteration: {Iter}", true);

                    granted_time = h.helicsFederateRequestTimeIterative(vfed, TimeStep+1, iter_flag, out helics_iter_status);

                    Console.WriteLine($"Gas: Granted Time Iterative: {granted_time}, IterationStatus: {helics_iter_status}");
                    Logger.WriteLog($"Gas: Granted Time Iterative: {granted_time}, Iteration Status: {helics_iter_status}", true);

                    if (helics_iter_status == (int)HelicsIterationResult.HELICS_ITERATION_RESULT_NEXT_STEP)
                    {
                        Console.WriteLine($"Gas: Time Step {TimeStep} Iteration Completed!");
                        Logger.WriteLog($"Gas: Time Step {TimeStep} Iteration Completed!", true);

                        break;
                    }

                    val = h.helicsInputGetDouble(SubToElectric);
                    Console.WriteLine(String.Format("Gas-Received: Time {0} \t iter {1} \t Pthe = {2:0.0000} [MW]", TimeStep, Iter, val));
                    Logger.WriteLog(String.Format("Gas-Received: Time {0} \t iter {1} \t Pthe = {2:0.0000} [MW]", TimeStep, Iter, val), true);


                    GasLastVal.Add(val);

                    if (Math.Abs(Pthermal[TimeStep] - val) > 0.001)
                    {
                        HasViolations = true;

                        if (val < PthermalMin)
                        {
                            Pthermal[TimeStep] = PthermalMin;
                        }
                        else if (val > PthermalMax)
                        {
                            Pthermal[TimeStep] = PthermalMax;
                        }

                        else
                        { 
                            Pthermal[TimeStep] = val; 
                        }

                        Console.WriteLine(String.Format("Gas-Event: Time {0} \t iter {1} \t Pthe = {2:0.0000} [MW]", TimeStep, Iter, Pthermal[TimeStep]));
                        Logger.WriteLog(String.Format("Gas-Event: Time {0} \t iter {1} \t Pthe = {2:0.0000} [MW]", TimeStep, Iter, Pthermal[TimeStep]), true);
                    }
                    else
                    {
                        if (GasLastVal.Count > 3)
                        {
                            if (Math.Abs(GasLastVal[GasLastVal.Count-1] - GasLastVal[GasLastVal.Count - 2]) > 0.001)
                            {
                                HasViolations = true;
                            }
                        }
                        else
                        {
                            HasViolations = true;
                        }
                    }

                    Logger.WriteLog($"Gas - HasVioltation?: {HasViolations}", true);
                    Console.WriteLine($"Gas - HasVioltation?: {HasViolations}");

                    if (HasViolations)
                    {
                        
                        Iter += 1;
                        MappingFactory.PublishGasThermalPower(TimeStep, Iter, Pthermal[TimeStep], GasPubPth, (PthermalMax - val), GasPubPthMax);

                        PthermalNew[TimeStep] = Pthermal[TimeStep];

                    }

                    else
                    {
                        Console.WriteLine($"Gas: Time Step {TimeStep} Converged!");
                        Logger.WriteLog($"Gas: Time Step {TimeStep} Converged!", true);

                        //continue;
                    } 
                }

                if (Iter == iter_max && HasViolations)
                {
                    CurrentDiverged = new TimeStepInfo() { timestep = TimeStep, itersteps = Iter };
                    notconverged.Add(CurrentDiverged);
                }
            }

            // request time for end of time + 1: serves as a blocking call until all federates are complete
            requested_time = total_time+1;
            Console.WriteLine($"\nGas: Requested Time: {requested_time}");
            Logger.WriteLog($"\nGas: Requested Time: {requested_time}", true);
            h.helicsFederateRequestTime(vfed, requested_time);

#if !DEBUG
            // close out log file
            Console.SetOut(oldOut);
            writer.Close();
            ostrm.Close();
#endif

            // finalize federate
            h.helicsFederateFinalize(vfed);
            Console.WriteLine("Gas: Federate finalized");
            Logger.WriteLog($"Gas: Federate finalized", true);
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
                        sw2.WriteLine(String.Format("{0}\t\t\t\t{1}", x.timestep, x.itersteps));
                    }
                }

            }
            using (FileStream fs = new FileStream(outputfolder + "NotConverged_gas_federate.txt", FileMode.OpenOrCreate, FileAccess.Write))
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

            using (FileStream fs = new FileStream(outputfolder + "PthermalAndPthermalNew.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw2 = new StreamWriter(fs))
                {
                    sw2.WriteLine("TimeStep\tPthermalOld[MW]\tPthermalNew[MW]\tPthermalMin[MW]\tPthermalMax[MW]");
                    for (int t=0;t <total_time; t++)
                    {
                        sw2.WriteLine(String.Format("{0}\t\t\t{1:0.00}\t\t\t{2:0.00}\t\t\t{3:0.00}\t\t\t\t {4:0.00}", t+1, PthermalOld[t],PthermalNew[t],PthermalMin,PthermalMax));
                    }
                }
            }

            // Diverging time steps
            if (notconverged.Count == 0)
            {
                Console.WriteLine("\n Gas: There is no diverging time step.");
                Logger.WriteLog($"Gas: There is no diverging time step.", true);
            }
            else
            {
                Console.WriteLine("Gas: the solution diverged at the following time steps:");
                Logger.WriteLog($"Gas: the solution diverged at the following time steps:", true);
                foreach (TimeStepInfo x in notconverged)
                {
                    Console.WriteLine($"Time-step {x.timestep}");
                    Logger.WriteLog($"Gas: Time-step {x.timestep}", true);
                }
                Console.WriteLine($"\n Gas: The total number of diverging time steps = { notconverged.Count }");
                Logger.WriteLog($"\n Gas: The total number of diverging time steps = { notconverged.Count }", true);
            }

            var k = Console.ReadKey();
        }
    }
}
