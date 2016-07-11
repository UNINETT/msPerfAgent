using System;
using System.Linq;
using System.Diagnostics;

namespace Uninett.MsPerfAgent
{
    public class msPerfAgentCounter
    {
        public string cPrefix { get; }
        public string mPath { get; }
        public float vCalculated { get; set; }
        public long vRaw { get; set; }
        public int secondsSinceEpoch { get; set; }
        public bool isInstanceCounter { get; set; }
        public bool counterIsHealthy { get; set; } = true;
        public bool isRawCounter { get; }
        private PerformanceCounter pCounter { get; }
        //private CounterSample cs1;
        //private CounterSample cs2;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Perf. Counter Types: https://msdn.microsoft.com/en-us/library/z573042h%28v=vs.100%29.aspx
        private static string[] counterTypes = { "NumberOfItems32", "NumberOfItems64", "NumberOfItemsHEX32", "NumberOfItemsHEX64", "RawBase" };

        public msPerfAgentCounter(string inputCarbonPrefix, string inputMetricPath, PerformanceCounter inputPc)
        {
            this.cPrefix = inputCarbonPrefix;
            this.mPath = inputMetricPath;
            this.pCounter = inputPc;

            if (counterTypes.Any(this.pCounter.CounterType.ToString().Contains))
            {
                this.isRawCounter = true;
            }
            else
            {
                this.isRawCounter = false;
            }

            //if(new PerformanceCounterCategory(this.pCounter.CategoryName).GetInstanceNames().Any())
            if (pCounter.InstanceName == "")
            {
                this.isInstanceCounter = false;
            }
            else
            {
                this.isInstanceCounter = true;
            }

            //// Check if counter is healthy
            try
            {
                this.vRaw = this.pCounter.RawValue;
            }
            catch (System.InvalidOperationException)
            {
                this.counterIsHealthy = false;
                log.Error("msPerfAgentCounter -> InvalidOperationException in counter: \"" + this.mPath + "\" - IsInstanceCounter: " + this.isInstanceCounter);
            }
            catch (Exception e)
            {
                this.counterIsHealthy = false;
                log.Fatal("{0} Exception caught.", e);
                //Console.WriteLine("C:\WINDOWS\system32>lodctr /R");
                //Console.WriteLine("C:\WINDOWS\system32>winmgmt.exe /RESYNCPERF");
            }
        }

        // ##########################################################################################################################################################

        public void setRawCounterValue()
        {
            try
            {
                this.vRaw = this.pCounter.RawValue;
            }
            catch (System.InvalidOperationException)
            {
                this.counterIsHealthy = false;
                log.Error("setRawCounterValue -> InvalidOperationException in counter: \"" + this.mPath + "\" - IsInstanceCounter: " + this.isInstanceCounter);
            }
            catch (Exception e)
            {
                this.counterIsHealthy = false;
                log.Fatal("{0} Exception caught.", e);
                //Console.WriteLine("C:\WINDOWS\system32>lodctr /R");
                //Console.WriteLine("C:\WINDOWS\system32>winmgmt.exe /RESYNCPERF");
            }
        }

        // ##########################################################################################################################################################

        public void setCalculatedCounterValue()
        {
            try
            {
                this.vCalculated = this.pCounter.NextValue();
            }
            catch (System.InvalidOperationException)
            {
                this.counterIsHealthy = false;
                log.Error("setCalculatedCounterValue -> InvalidOperationException in counter: \"" + this.mPath + "\" - IsInstanceCounter: " + this.isInstanceCounter);
            }
            catch (Exception e)
            {
                this.counterIsHealthy = false;
                log.Fatal("{0} Exception caught.", e);
                //Console.WriteLine("C:\WINDOWS\system32>lodctr /R");
                //Console.WriteLine("C:\WINDOWS\system32>winmgmt.exe /RESYNCPERF");
            }
        }

    }
}
