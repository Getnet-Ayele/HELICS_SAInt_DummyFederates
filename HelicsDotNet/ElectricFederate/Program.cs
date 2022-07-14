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
            string outputfolder = @"..\..\..\..\outputs\";
            Directory.CreateDirectory(outputfolder);

            // Get HELICS version
            Console.WriteLine($"Electric: HELICS version ={h.helicsGetVersion()}");

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
            SWIGTYPE_p_void SubToGasMin = h.helicsFederateRegisterSubscription(vfed, "GasThermalPowerMin", "");

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
            double[] POld = new double[P.Length];
            double[] PNew = new double[P.Length];
            Array.Copy(P, POld, P.Length);
            Array.Copy(P, PNew, P.Length);
            // set number of HELICS time steps based on scenario
            double total_time = P.Length;
            Console.WriteLine($"Number of time steps in scenario: {total_time}");

            double granted_time = 0;
            double requested_time;

            // variables to control iterations
            short Iter = 0;
            List<TimeStepInfo> timestepinfo = new List<TimeStepInfo>();
            List<TimeStepInfo> notconverged = new List<TimeStepInfo>();
            TimeStepInfo CurrentDiverged = new TimeStepInfo();
            TimeStepInfo currenttimestep = new TimeStepInfo() { timestep = 0, itersteps = 0 };

            List<double> ElecLastVal = new List<double>();

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
            for (TimeStep = 0; TimeStep < total_time; TimeStep++)
            {
                // non-iterative time request here to block until both federates are done iterating the last time step
                Console.WriteLine($"Requested time {TimeStep}");

                // HELICS time granted 
                granted_time = h.helicsFederateRequestTime(vfed, TimeStep);
                Console.WriteLine($"Granted time: {granted_time}");
                IsRepeating = true;
                HasViolations = true;


                Iter = 0; // Iteration number

                // Initial publication of thermal power request equivalent to PGMAX for time = 0 and iter = 0;
                if (TimeStep == 0)
                {
                    MappingFactory.PublishElectricPower(granted_time - 1, Iter, P[TimeStep], ElectricPub);
                }

                // Set time step info
                currenttimestep = new TimeStepInfo() { timestep = TimeStep, itersteps = 0 };
                timestepinfo.Add(currenttimestep);

                while (IsRepeating && Iter<iter_max)
                {

                    // Artificial delay
                    if (TimeStep > 5 && Iter==0)
                    {
                        Thread.Sleep(10);
                    }

                    // stop iterating if max iterations have been reached                    
                    Iter += 1;
                    currenttimestep.itersteps += 1;

                    int helics_iter_status;
                    // iterative HELICS time request                    
                    Console.WriteLine($"Requested time: {TimeStep}, iteration: {Iter}");
                    // HELICS time granted
                    granted_time = h.helicsFederateRequestTimeIterative(vfed, TimeStep, HelicsIterationRequest.HELICS_ITERATION_REQUEST_FORCE_ITERATION, out helics_iter_status);
                    Console.WriteLine($"Granted time: {TimeStep},  Iteration status: {helics_iter_status}");

                    // Using an offset of 1 on the granted_time here because HELICS starts at t=1 and SAInt starts at t=0                        
                    MappingFactory.PublishElectricPower(granted_time - 1, Iter, P[TimeStep], ElectricPub);

                    // get available thermal power at nodes, determine if there are violations
                    HasViolations = MappingFactory.SubscribeToGasThermalPower(granted_time - 1, Iter, P[TimeStep], SubToGas, ElecLastVal);


                    HasViolations = false;
                    // subscribe to available thermal power from gas node
                    double valPth = h.helicsInputGetDouble(SubToGas);
                    double GasMin = h.helicsInputGetDouble(SubToGasMin);
                    Console.WriteLine(String.Format("Electric-Received: Time {0} \t iter {1} \t Pthg = {2:0.0000} [MW]", TimeStep, Iter, valPth));

                    //get currently required thermal power                 
                    double HR = 5 + 0.5 * P[TimeStep] - 0.01 * P[TimeStep] * P[TimeStep];
                    double ThermalPower = HR / 3.6 * P[TimeStep]; //Thermal power in [MW]; // eta_th=3.6/HR[MJ/kWh]

                    ElecLastVal.Add(valPth);

                    if (Math.Abs(ThermalPower - valPth) > 0.001)
                    {
                        if (GasMin <= 0)
                        { 
                            P[TimeStep] -=10; 
                        }

                        HasViolations = true;
                    }
                    else
                    {
                        if (ElecLastVal.Count > 1)
                        {
                            if (Math.Abs(ElecLastVal[ElecLastVal.Count - 1] - ElecLastVal[ElecLastVal.Count - 2]) > 0.001)
                            {
                                HasViolations = true;
                            }
                        }
                        else
                        {
                            HasViolations = true;
                        }
                    }

                    PNew[TimeStep] = P[TimeStep];

                    if (Iter == iter_max && HasViolations)
                    {
                        CurrentDiverged = new TimeStepInfo() { timestep = TimeStep, itersteps = Iter };
                        notconverged.Add(CurrentDiverged);
                    }

                    IsRepeating = HasViolations;
                }
            }

            // request time for end of time + 1: serves as a blocking call until all federates are complete
            requested_time = total_time;
            Console.WriteLine($"Requested time: {requested_time}");
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
                        sw2.WriteLine(String.Format("{0}\t{1}", x.timestep, x.itersteps));
                    }
                }

            }

            using (FileStream fs = new FileStream(outputfolder + "NotConverged_electric_federate.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw2 = new StreamWriter(fs))
                {
                    sw2.WriteLine("TimeStep \t IterStep");
                    foreach (TimeStepInfo x in notconverged)
                    {
                        sw2.WriteLine(String.Format("{0}\t{1}", x.timestep, x.itersteps));
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
                        sw2.WriteLine(String.Format("{0}\t\t{1:0.00}\t\t{2:0.00}", t, POld[t], PNew[t]));
                    }
                }
            }


            // Diverging time steps
            if (notconverged.Count == 0)
                Console.WriteLine("\n Electric: There is no diverging time step.");
            else
            {
                Console.WriteLine("Electric: the solution diverged at the following time steps:");
                foreach (TimeStepInfo x in notconverged)
                {
                    Console.WriteLine($"Time-step {x.timestep}");
                }
                Console.WriteLine($"\n Electric: The total number of diverging time steps = { notconverged.Count }");
            }

            var k = Console.ReadKey();
        }
    }
}