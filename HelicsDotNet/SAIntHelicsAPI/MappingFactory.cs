﻿using System;
using System.Collections.Generic;
using System.IO;
using h = helics;

namespace SAIntHelicsLib
{
    public static class MappingFactory
    {
        public static double eps = 0.001;

        public static void PublishElectricPower(double gtime, int step, double pval, SWIGTYPE_p_void ElectricPub)
        {
            double HR = 5 + 0.5 * pval - 0 * pval * pval;
            // relation between thermal efficiency and heat rate: eta_th[-]=3.6/HR[MJ/kWh]
            double ThermalPower = HR / 3.6 * pval; //Thermal power in [MW]

            h.helicsPublicationPublishDouble(ElectricPub, ThermalPower);
            string message = String.Format("\nElectric-Sent: Time {0} \t iter {1} \t Pthe = {2:0.0000} [MW] \t P = {3:0.0000} [MW]",
                gtime, step, ThermalPower, pval);
            Console.WriteLine(message);
            Logger.WriteLog(message, true);
        }

        public static void PublishGasThermalPower(double gtime, int step, double ThermalPower, SWIGTYPE_p_void GasPubPth, double GasMin, SWIGTYPE_p_void GasPubPthMax)
        {
            h.helicsPublicationPublishDouble(GasPubPth, ThermalPower);
            string message = String.Format("\nGas-Sent: Time {0} \t iter {1} \t Pthg = {2:0.0000} [MW]",
                gtime, step, ThermalPower);
            Console.WriteLine(message);
            Logger.WriteLog(message, true);

            h.helicsPublicationPublishDouble(GasPubPthMax, GasMin);
            string message2 = String.Format("Gas-Sent: Time {0} \t iter {1} \t Reserve = {2:0.0000} [MW]", gtime, step, GasMin);
            Console.WriteLine(message2);
            Logger.WriteLog(message2, true);
        }

        public static bool SubscribeToGasThermalPower(double gtime, int step, double pval, SWIGTYPE_p_void SubToGas, List<double> ElecLastVal)
        {
            bool HasViolations = false;

            // subscribe to available thermal power from gas node
            double valPth = h.helicsInputGetDouble(SubToGas);

            string message = String.Format("Electric-Recieved: Time {0} \t iter {1} \t Pthg = {2:0.0000} [MW]", gtime, step, valPth);
            Console.WriteLine(message);
            Logger.WriteLog(message, true);

            //get currently required thermal power                 
            double HR = 5 + 0.5 * pval - 0 * pval * pval;
            double ThermalPower = HR / 3.6 * pval; //Thermal power in [MW]; // eta_th=3.6/HR[MJ/kWh]

            ElecLastVal.Add(valPth);

            if (Math.Abs(ThermalPower - valPth) > eps && step >= 0)
            {

                HasViolations = true;
            }
            else
            {
                if (ElecLastVal.Count > 3)
                {
                    if (Math.Abs(ElecLastVal[ElecLastVal.Count-2] - ElecLastVal[ElecLastVal.Count - 1]) > 0.001)
                    {
                        HasViolations = true;
                    }
                }
                else
                {
                    HasViolations = true;
                }
            }

            return HasViolations;
        }

    }

    public class TimeStepInfo
    {
        public int timestep, itersteps;
    }
}
