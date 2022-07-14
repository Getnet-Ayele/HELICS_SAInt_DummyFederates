using System;
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
            double HR = 5 + 0.5 * pval - 0.01 * pval * pval;
            // relation between thermal efficiency and heat rate: eta_th[-]=3.6/HR[MJ/kWh]
            double ThermalPower = HR / 3.6 * pval; //Thermal power in [MW]

            h.helicsPublicationPublishDouble(ElectricPub, ThermalPower);

            Console.WriteLine(String.Format("Electric-Sen: Time {0} \t iter {1} \t Pthe = {2:0.0000} [MW] \t P = {3:0.0000} [MW]",
                gtime, step, ThermalPower, pval));
        }

        public static void PublishGasThermalPower(double gtime, int step, double ThermalPower, SWIGTYPE_p_void GasPubPth)
        {
            h.helicsPublicationPublishDouble(GasPubPth, ThermalPower);

            Console.WriteLine(String.Format("Gas-Sent: Time {0} \t iter {1} \t Pthg = {2:0.0000} [MW]",
                gtime, step, ThermalPower));
        }

        public static bool SubscribeToGasThermalPower(double gtime, int step, double pval, SWIGTYPE_p_void SubToGas, List<double> ElecLastVal)
        {
            bool HasViolations = false;



            // subscribe to available thermal power from gas node
            double valPth = h.helicsInputGetDouble(SubToGas);

            Console.WriteLine(String.Format("Electric-R: Time {0} \t iter {1} \t Pthg = {2:0.0000} [MW]", gtime, step, valPth));

            //get currently required thermal power                 
            double HR = 5 + 0.5 * pval - 0.01 * pval * pval;
            double ThermalPower = HR / 3.6 * pval; //Thermal power in [MW]; // eta_th=3.6/HR[MJ/kWh]

            ElecLastVal.Add(valPth);

            if (Math.Abs(ThermalPower - valPth) > eps && step >= 0)
            {

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

            return HasViolations;
        }

        public static double GetActivePowerFromAvailableThermalPower(double Pth, double initVal)
        {
            Func<double, double> GetHR = (x) => 5 + 0.5 * x - 0.01 * x * x;
            Func<double, double> GetF = (x) => 3.6 * Pth - x * GetHR(x);
            Func<double, double> GetdFdx = (x) => -(5 + 2 * 0.5 * x + 3 * (-0.01) * x * x);

            double Res = Math.Abs(GetF(initVal));
            int maxiter = 20;
            int i = 0;
            double p = initVal;

            while (i < maxiter)
            {
                p -= GetF(p) / GetdFdx(p);

                Res = Math.Abs(GetF(p));

                if (Res < eps) { return p; }

                i += 1;
            }

            return p;
            //throw new Exception("No solution found for given thermal power");
        }


    }

    public class TimeStepInfo
    {
        public int timestep, itersteps;
    }
}
