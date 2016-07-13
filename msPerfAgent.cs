using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;

namespace Uninett.MsPerfAgent
{
    public class msPerfAgent
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static int numberOfCountersProcessed = 0;
        private static long numRuns = 0;

        private static string xPathConfigurationMsPerfAgent = "/Configuration/MsPerfAgent/";
        private static string xPathCategoryMsPerfAgent = "/Configuration/PerfCategory/";

        private static string xPathmsPerfAgentCountersGraphite = "/Configuration/PerformanceCounters/";
        private static string xPathMetricCleaningGraphite = "/Configuration/MetricCleaning/";

        // ##########################################################################################################################################################

        public void run(XmlElement xmlMsPerfAgentConfigElement, XmlElement xmlGraphiteConfigElement, List<msPerfAgentCounter> msPerfAgentCounterList, bool processOnEachRun)
        {
            log.Info("Started collection at " + DateTime.Now.ToString());

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            if (processOnEachRun && numRuns > 0)
            {
                // reload XMLConfig files & reload PerformanceCounterList

                xmlMsPerfAgentConfigElement = msPerfAgentXml.xmlGetMsPerfAgentConfigXmlElement();
                xmlGraphiteConfigElement = msPerfAgentXml.xmlGetGraphiteConfigXmlElement(xmlMsPerfAgentConfigElement);

                msPerfAgentCounterList = getMsPerfCounterList(xmlMsPerfAgentConfigElement, xmlGraphiteConfigElement);
            }

            List<msPerfAgentCounter> processedMsPerfAgentCounters = processMsPerfAgentCounterList(xmlMsPerfAgentConfigElement, msPerfAgentCounterList);
            sendMetricsToOutput(processedMsPerfAgentCounters, xmlGraphiteConfigElement, stopWatch);

            stopWatch.Stop();
            writeElapsedTime(stopWatch);

            log.Info("Number of counters: " + numberOfCountersProcessed);
            numberOfCountersProcessed = 0;

            sleepBetweenRuns(xmlGraphiteConfigElement, stopWatch);
        }

        // ##########################################################################################################################################################

        public void startConsole()
        {
            XmlElement xmlMsPerfAgentConfigElement = msPerfAgentXml.xmlGetMsPerfAgentConfigXmlElement();
            XmlElement xmlGraphiteConfigElement = msPerfAgentXml.xmlGetGraphiteConfigXmlElement(xmlMsPerfAgentConfigElement);

            List<msPerfAgentCounter> msPerfAgentCounterList = getMsPerfCounterList(xmlMsPerfAgentConfigElement, xmlGraphiteConfigElement);

            bool processOnEachRun = Convert.ToBoolean(msPerfAgentXml.getMsPerfAgentConfigFromXml(xmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "processOnEachRun"));

            while (true)
            {
                System.Console.WriteLine("Press Q to quit...");

                while (!System.Console.KeyAvailable)
                {
                    run(xmlMsPerfAgentConfigElement, xmlGraphiteConfigElement, msPerfAgentCounterList, processOnEachRun);
                    numRuns++;
                }

                if (System.Console.ReadKey().Key == ConsoleKey.Q)
                {
                    break;
                }
            }
        }

        // ##########################################################################################################################################################

        public void startService(CancellationToken cancel)
        {
            XmlElement xmlMsPerfAgentConfigElement = msPerfAgentXml.xmlGetMsPerfAgentConfigXmlElement();
            XmlElement xmlGraphiteConfigElement = msPerfAgentXml.xmlGetGraphiteConfigXmlElement(xmlMsPerfAgentConfigElement);

            List<msPerfAgentCounter> msPerfAgentCounterList = getMsPerfCounterList(xmlMsPerfAgentConfigElement, xmlGraphiteConfigElement);

            bool processOnEachRun = Convert.ToBoolean(msPerfAgentXml.getMsPerfAgentConfigFromXml(xmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "processOnEachRun"));

            while (!cancel.IsCancellationRequested)
            {
                run(xmlMsPerfAgentConfigElement, xmlGraphiteConfigElement, msPerfAgentCounterList, processOnEachRun);
                numRuns++;
            }

            throw new OperationCanceledException(cancel);

        }

        // ##########################################################################################################################################################

        static void sendMetricsToOutput(List<msPerfAgentCounter> msPerfAgentCounters, XmlElement xmlGraphiteConfigElement, Stopwatch stopWatch)
        {
            // currently only supports sending metrics to CarbonServer using UDP

            if (msPerfAgentXml.getGraphiteConfigurationFromXml(xmlGraphiteConfigElement, "SendUsingUDP") == "True")
            {
                UdpClient udpClient = createUdpClient(xmlGraphiteConfigElement);

                sendToCarbon(msPerfAgentCounters, udpClient, xmlGraphiteConfigElement);
                msPerfAgentMetricsToCarbon(stopWatch, udpClient);

                udpClient.Close();
            }
            else
            {
                log.Info("sendMetricsToOutput -> mscollectd currently only support SendUsingUDP = True");
            }

        }

        // ##########################################################################################################################################################

        static UdpClient createUdpClient(XmlElement xmlGraphiteConfigElement)
        {
            int carbonServerPort = int.Parse(msPerfAgentXml.getGraphiteConfigurationFromXml(xmlGraphiteConfigElement, "CarbonServerPort"));
            UdpClient udpClient = new UdpClient(carbonServerPort);

            try
            {
                udpClient.Connect(msPerfAgentXml.getGraphiteConfigurationFromXml(xmlGraphiteConfigElement, "CarbonServer"), carbonServerPort);
            }
            catch (Exception e)
            {
                log.Fatal("createUdpClient -> Exception -> " + e.ToString());
            }

            return udpClient;
        }

        // ##########################################################################################################################################################

        static List<msPerfAgentCounter> getMsPerfCounterList(XmlElement xmlMsPerfAgentConfigElement, XmlElement xmlGraphiteConfigElement)
        {
            string msPerfAgentMode = msPerfAgentXml.getMsPerfAgentConfigFromXml(xmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "MsPerfAgentMode");

            if (msPerfAgentMode == "all")
            {
                return getAllCounters(xmlMsPerfAgentConfigElement, xmlGraphiteConfigElement);
            }
            else if (msPerfAgentMode == "selected")
            {
                return getCountersFromStatsToGraphiteConfig(xmlMsPerfAgentConfigElement, xmlGraphiteConfigElement);
            }
            else if (msPerfAgentMode == "category")
            {
                return getCategoryCounters(xmlMsPerfAgentConfigElement, xmlGraphiteConfigElement);
            }
            else
            {
                log.Error("msPerfAgentConfigFile is not valid");
                return null;
            }
        }

        // ##########################################################################################################################################################
        // ##########################################################################################################################################################

        #region findCounters

        static List<msPerfAgentCounter> getCountersFromStatsToGraphiteConfig(XmlElement inputXmlMsPerfAgentConfigElement, XmlElement inputXmlGraphiteConfigElement)
        {
            XmlNodeList myNodeList = inputXmlGraphiteConfigElement.SelectNodes(xPathmsPerfAgentCountersGraphite + "*");
            string carbonPrefix = createCarbonPrefix(inputXmlMsPerfAgentConfigElement, inputXmlGraphiteConfigElement);
            List<msPerfAgentCounter> msPerfAgentCounterList = new List<msPerfAgentCounter>();

            foreach (XmlNode myNode in myNodeList)
            {
                if (myNode.Name == "Counter")
                {
                    string pCatString = regexGetCategoryFrommsPerfAgentCounter(myNode.Attributes["Name"].Value);
                    string pCounterString = regexGetCounterNameFrommsPerfAgentCounter(myNode.Attributes["Name"].Value);

                    //if (categoryList.Contains(pCatString) && PerformanceCounterCategory.Exists(pCatString))
                    if (PerformanceCounterCategory.Exists(pCatString))
                    {
                        //Console.WriteLine("PerformanceCategory is on this system: " + regexGetCategoryFrommsPerfAgentCounter(myNode.Attributes["Name"].Value));

                        if (regexIsInstancemsPerfAgentCounter(myNode.Attributes["Name"].Value))
                        {
                            string pInstString = regexGetInstanceFrommsPerfAgentCounter(myNode.Attributes["Name"].Value);

                            if (pInstString == "*")
                            {
                                PerformanceCounterCategory perfCat = new PerformanceCounterCategory(pCatString);
                                string[] instances = perfCat.GetInstanceNames();

                                foreach (string pi in instances)
                                {
                                    if (PerformanceCounterCategory.InstanceExists(pi, pCatString))
                                    {
                                        if (PerformanceCounterCategory.CounterExists(pCounterString, pCatString))
                                        {
                                            string metricPath = pCatString + @"\" + pi + @"\" + pCounterString;

                                            PerformanceCounter pc = new PerformanceCounter(pCatString, pCounterString, pi);
                                            if (!filterMetric(inputXmlGraphiteConfigElement, metricPath))
                                            {
                                                msPerfAgentCounter msPerfAgentCounter = new msPerfAgentCounter(carbonPrefix, metricPath, pc);
                                                msPerfAgentCounterList.Add(msPerfAgentCounter);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (PerformanceCounterCategory.InstanceExists(pInstString, pCatString))
                                {
                                    if (PerformanceCounterCategory.CounterExists(pCounterString, pCatString))
                                    {
                                        string metricPath = pCatString + @"\" + pInstString + @"\" + pCounterString;

                                        PerformanceCounter pc = new PerformanceCounter(pCatString, pCounterString, pInstString);
                                        if (!filterMetric(inputXmlGraphiteConfigElement, metricPath))
                                        {
                                            msPerfAgentCounter msPerfAgentCounter = new msPerfAgentCounter(carbonPrefix, metricPath, pc);
                                            msPerfAgentCounterList.Add(msPerfAgentCounter);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (PerformanceCounterCategory.CounterExists(pCounterString, pCatString))
                            {
                                string metricPath = pCatString + @"\" + pCounterString;

                                PerformanceCounter pc = new PerformanceCounter(pCatString, pCounterString);
                                if (!filterMetric(inputXmlGraphiteConfigElement, metricPath))
                                {
                                    msPerfAgentCounter msPerfAgentCounter = new msPerfAgentCounter(carbonPrefix, metricPath, pc);
                                    msPerfAgentCounterList.Add(msPerfAgentCounter);
                                }
                            }
                        } // counter is not instance counter
                    }
                }
            }

            return msPerfAgentCounterList;
        }

        // ##########################################################################################################################################################

        static List<msPerfAgentCounter> getAllCounters(XmlElement inputXmlMsPerfAgentConfigElement, XmlElement inputXmlGraphiteConfigElement)
        {
            List<msPerfAgentCounter> msPerfAgentCounterList = new List<msPerfAgentCounter>();
            string carbonPrefix = createCarbonPrefix(inputXmlMsPerfAgentConfigElement, inputXmlGraphiteConfigElement);
            PerformanceCounterCategory[] categories = PerformanceCounterCategory.GetCategories();

            foreach (PerformanceCounterCategory pcc in categories)
            {
                if (!excludePerformanceCategory(inputXmlMsPerfAgentConfigElement, pcc.CategoryName))
                {
                    pcc.ReadCategory();
                    string[] instances = pcc.GetInstanceNames();

                    if (instances.Any())
                    {
                        foreach (string instance in instances)
                        {
                            if (pcc.InstanceExists(instance))
                            {
                                PerformanceCounter[] countersOfCategory = pcc.GetCounters(instance);

                                foreach (PerformanceCounter pc in countersOfCategory)
                                {
                                    String metricPath = pc.CategoryName + "." + instance + "." + pc.CounterName;

                                    if (!filterMetric(inputXmlGraphiteConfigElement, metricPath))
                                    {
                                        msPerfAgentCounter msPerfAgentCounter = new msPerfAgentCounter(carbonPrefix, metricPath, pc);
                                        msPerfAgentCounterList.Add(msPerfAgentCounter);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        PerformanceCounter[] countersOfCategory = pcc.GetCounters();

                        foreach (PerformanceCounter pc in countersOfCategory)
                        {
                            string metricPath = pc.CategoryName + "." + pc.CounterName;

                            if (!filterMetric(inputXmlGraphiteConfigElement, metricPath))
                            {
                                msPerfAgentCounter msPerfAgentCounter = new msPerfAgentCounter(carbonPrefix, metricPath, pc);
                                msPerfAgentCounterList.Add(msPerfAgentCounter);
                            }
                            else
                            {
                                msPerfAgentCounter msPerfAgentCounter = new msPerfAgentCounter(carbonPrefix, metricPath, pc);
                                msPerfAgentCounterList.Add(msPerfAgentCounter);
                            }
                        }
                    }
                }
                else
                {
                    log.Info("PerformanceCounterCategory: " + pcc.CategoryName.ToLower() + " is excluded.");
                }
            }

            return msPerfAgentCounterList;
        }

        // ##########################################################################################################################################################

        static List<msPerfAgentCounter> getCategoryCounters(XmlElement inputXmlMsPerfAgentConfigElement, XmlElement inputXmlGraphiteConfigElement)
        {
            XmlNodeList categoryList = inputXmlMsPerfAgentConfigElement.SelectNodes(xPathCategoryMsPerfAgent + "*");
            string carbonPrefix = createCarbonPrefix(inputXmlMsPerfAgentConfigElement, inputXmlGraphiteConfigElement);
            List<msPerfAgentCounter> msPerfAgentCounterList = new List<msPerfAgentCounter>();

            foreach (XmlNode node in categoryList)
            {
                if (node.Name == "PerformanceCategory")
                {
                    if (PerformanceCounterCategory.Exists(node.Attributes["Name"].Value))
                    {
                        PerformanceCounterCategory pcc = new PerformanceCounterCategory(node.Attributes["Name"].Value);

                        pcc.ReadCategory();
                        string[] instances = pcc.GetInstanceNames();

                        if (instances.Any())
                        {
                            foreach (string instance in instances)
                            {
                                if (pcc.InstanceExists(instance))
                                {
                                    PerformanceCounter[] countersOfCategory = pcc.GetCounters(instance);

                                    foreach (PerformanceCounter pc in countersOfCategory)
                                    {
                                        String metricPath = pc.CategoryName + "." + instance + "." + pc.CounterName;
                                        msPerfAgentCounter msPerfAgentCounter = new msPerfAgentCounter(carbonPrefix, metricPath, pc);
                                        msPerfAgentCounterList.Add(msPerfAgentCounter);
                                    }
                                }
                            }
                        }
                        else
                        {
                            PerformanceCounter[] countersOfCategory = pcc.GetCounters();

                            foreach (PerformanceCounter pc in countersOfCategory)
                            {
                                string metricPath = pc.CategoryName + "." + pc.CounterName;
                                msPerfAgentCounter msPerfAgentCounter = new msPerfAgentCounter(carbonPrefix, metricPath, pc);
                                msPerfAgentCounterList.Add(msPerfAgentCounter);
                            }
                        }
                    }
                }
            }

            return msPerfAgentCounterList;
        }

        #endregion

        // ##########################################################################################################################################################
        // ##########################################################################################################################################################

        static void writeElapsedTime(Stopwatch inputStopWatch)
        {
            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = inputStopWatch.Elapsed;

            // Format and display the TimeSpan value.
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
            //System.Console.WriteLine("RunTime: " + elapsedTime);
            log.Info("RunTime: " + elapsedTime);
        }

        // ##########################################################################################################################################################

        static void msPerfAgentMetricsToCarbon(Stopwatch inputStopWatch, UdpClient inputUdpClient)
        {

            XmlElement xmlMsPerfAgentConfigElement = msPerfAgentXml.xmlGetMsPerfAgentConfigXmlElement();
            XmlElement xmlGraphiteConfigElement = msPerfAgentXml.xmlGetGraphiteConfigXmlElement(xmlMsPerfAgentConfigElement);
            string carbonPrefix = createCarbonPrefix(xmlMsPerfAgentConfigElement, xmlGraphiteConfigElement);

            int metricSendIntervalMilliseconds = int.Parse(msPerfAgentXml.getGraphiteConfigurationFromXml(xmlGraphiteConfigElement, "MetricSendIntervalSeconds")) * 1000;

            sendToCarbon(carbonPrefix, "mscollectd.totalmilliseconds", (long)inputStopWatch.Elapsed.TotalMilliseconds, getSecondsSinceEpoch(xmlMsPerfAgentConfigElement), inputUdpClient);
            sendToCarbon(carbonPrefix, "mscollectd.metricsendintervalmilliseconds", metricSendIntervalMilliseconds, getSecondsSinceEpoch(xmlMsPerfAgentConfigElement), inputUdpClient);
            sendToCarbon(carbonPrefix, "mscollectd.numcountersprocessed", numberOfCountersProcessed, getSecondsSinceEpoch(xmlMsPerfAgentConfigElement), inputUdpClient);
        }

        // ##########################################################################################################################################################

        static void sleepBetweenRuns(XmlElement inputXmlGraphiteConfigElement, Stopwatch inputStopWatch)
        {
            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = inputStopWatch.Elapsed;

            int metricSendIntervalMilliseconds = int.Parse(msPerfAgentXml.getGraphiteConfigurationFromXml(inputXmlGraphiteConfigElement, "MetricSendIntervalSeconds")) * 1000;

            int sleepMilliseconds = metricSendIntervalMilliseconds - Convert.ToInt32(ts.TotalMilliseconds);
            if (sleepMilliseconds < 0) sleepMilliseconds = 0;

            if (sleepMilliseconds < 5000)
            {
                Thread.Sleep(sleepMilliseconds + 5000);
                log.Warn("Extend MetricSendIntervalSeconds. Processing counters takes too much resources! SleepMilliseconds is " + sleepMilliseconds + " < 5000");
            }
            else
            {
                Thread.Sleep(sleepMilliseconds);
            }
        }

        // ##########################################################################################################################################################

        #region regex

        static string regexGetCategoryFrommsPerfAgentCounter(string inputCounter)
        {
            //Console.WriteLine(inputCounter);

            string counterGroupName = null;
            //string pattern = @"^\\([\w\s]+)([\(|\\]).*";
            //string pattern = @"^\\([^\\^\(]+)([\(|\\]).*";
            string pattern = @"^\\([^\\^\(]+)";

            Regex rgx = new Regex(pattern);
            Match match = rgx.Match(inputCounter);

            if (match.Success)
            {
                string[] names = rgx.GetGroupNames();
                Group grp = match.Groups[names[1]];
                counterGroupName = grp.Value;
                //Console.WriteLine(counterGroupName);
            }
            //else
            //{
            //    //Console.WriteLine("No RegExp match on: " + inputCounter);
            //}
            return counterGroupName;
        }

        // ##########################################################################################################################################################

        static bool regexIsInstancemsPerfAgentCounter(string inputCounter)
        {
            //Console.WriteLine(inputCounter);

            bool isInstanceCounter = false;
            string pattern = @"(\(.*\))\\.*";
            Regex rgx = new Regex(pattern);
            Match match = rgx.Match(inputCounter);

            if (match.Success)
            {
                isInstanceCounter = true;
                //Console.WriteLine("True");
            }
            //else
            //{
            //    Console.WriteLine("False");
            //}
            return isInstanceCounter;
        }

        // ##########################################################################################################################################################

        static string regexGetInstanceFrommsPerfAgentCounter(string inputCounter)
        {
            //Console.WriteLine(inputCounter);

            string counterInstanceName = null;
            string pattern = @"\((.*)\)\\.*";

            Regex rgx = new Regex(pattern);
            Match match = rgx.Match(inputCounter);

            if (match.Success)
            {
                string[] names = rgx.GetGroupNames();
                Group grp = match.Groups[names[1]];
                counterInstanceName = grp.Value;
                //Console.WriteLine(counterInstanceName);
            }

            return counterInstanceName;
        }

        // ##########################################################################################################################################################

        static string regexGetCounterNameFrommsPerfAgentCounter(string inputCounter)
        {
            //Console.WriteLine(inputCounter);

            string countercounterName = null;

            string pattern = @"\\([^\\]+)$";
            Regex rgx = new Regex(pattern);
            Match match = rgx.Match(inputCounter);

            if (match.Success)
            {
                string[] names = rgx.GetGroupNames();
                Group grp = match.Groups[names[1]];
                countercounterName = grp.Value;
                //Console.WriteLine(countercounterName);
            }
            return countercounterName;
        }

        #endregion

        // ##########################################################################################################################################################

        static int getSecondsSinceEpoch(XmlElement inputXmlMsPerfAgentConfigElement)
        {
            bool useUtcTime = Convert.ToBoolean(msPerfAgentXml.getMsPerfAgentConfigFromXml(inputXmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "UseUtcTime"));
            DateTime dateTimeNow = DateTime.UtcNow;

            if (!useUtcTime)
            {
                //Console.WriteLine("LocalTime");
                dateTimeNow = dateTimeNow.ToLocalTime();
            }

            TimeSpan t = dateTimeNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;

            //Console.WriteLine("Seconds since UNIX epoch: " + secondsSinceEpoch);
            numberOfCountersProcessed++;

            return secondsSinceEpoch;
        }

        // ##########################################################################################################################################################

        static string createCarbonPrefix(XmlElement inputXmlMsPerfAgentConfigElement, XmlElement inputXmlGraphiteConfigElement)
        {
            // https://msdn.microsoft.com/en-us/library/system.environment.getenvironmentvariable.aspx
            // http://stackoverflow.com/questions/1233217/difference-between-systeminformation-computername-environment-machinename-and

            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            //Console.WriteLine("Computer name: {0}", ipGlobalProperties.HostName);
            //Console.WriteLine("Domain name:   {0}", ipGlobalProperties.DomainName);
            //Console.WriteLine("Node type:     {0:f}", ipGlobalProperties.NodeType);
            //string nodeHostName = System.Environment.MachineName.ToLower(); // NetBiosName

            string nodeHostName = msPerfAgentXml.getGraphiteConfigurationFromXml(inputXmlGraphiteConfigElement, "NodeHostName").ToLower();
            string metricPath = msPerfAgentXml.getGraphiteConfigurationFromXml(inputXmlGraphiteConfigElement, "MetricPath");
            string metricPathMsPerfAgent = msPerfAgentXml.getMsPerfAgentConfigFromXml(inputXmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "MetricPath");

            // Override metricPath from msPerfAgentConfig if it exists.
            if (metricPathMsPerfAgent != "")
            {
                metricPath = metricPathMsPerfAgent;
            }

            if (nodeHostName == "$env:computername")
            {
                nodeHostName = System.Environment.GetEnvironmentVariable("COMPUTERNAME").ToLower();
            }

            string carbonPrefix = null;

            bool includeDomainName = Convert.ToBoolean(msPerfAgentXml.getMsPerfAgentConfigFromXml(inputXmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "IncludeDomainNameIfExists"));

            if (((ipGlobalProperties.DomainName == null || ipGlobalProperties.DomainName == string.Empty) && !includeDomainName) || !includeDomainName)
            {
                carbonPrefix = metricPath + "." + nodeHostName;
            }
            else
            {
                carbonPrefix = metricPath + "." + ipGlobalProperties.DomainName.ToLower() + "." + nodeHostName;
            }

            return carbonPrefix;
        }

        // ##########################################################################################################################################################

        #region processAndSendToCarbon

        static void sendToCarbon(string inputCarbonPrefix, string metricPath, float calculatedmsPerfAgentCounter, long pcTimestamp, UdpClient inputUdpClient)
        {
            // http://graphite.readthedocs.org/en/latest/feeding-carbon.html#the-plaintext-protocol

            string sendtoCarbonString = inputCarbonPrefix + "." + metricPath + " " + calculatedmsPerfAgentCounter.ToString().Replace(",", ".") + " " + pcTimestamp;
            sendUdpStream(inputUdpClient, sendtoCarbonString);
        }

        static void sendToCarbon(string inputCarbonPrefix, string metricPath, long rawmsPerfAgentCounter, long pcTimestamp, UdpClient inputUdpClient)
        {
            // http://graphite.readthedocs.org/en/latest/feeding-carbon.html#the-plaintext-protocol

            string sendtoCarbonString = inputCarbonPrefix + "." + metricPath + " " + rawmsPerfAgentCounter + " " + pcTimestamp;
            sendUdpStream(inputUdpClient, sendtoCarbonString);
        }

        static void sendToCarbon(List<msPerfAgentCounter> msPerfAgentCounters, UdpClient inputUdpClient, XmlElement inputXmlGraphiteConfigElement)
        {
            // http://graphite.readthedocs.org/en/latest/feeding-carbon.html#the-plaintext-protocol

            foreach (msPerfAgentCounter pc in msPerfAgentCounters)
            {
                if (pc.counterIsHealthy)
                {
                    if (pc.isRawCounter)
                    {
                        string sendtoCarbonString = metricPathCleaning(inputXmlGraphiteConfigElement, pc.cPrefix + "." + pc.mPath) + " " + pc.vRaw + " " + pc.secondsSinceEpoch;
                        sendUdpStream(inputUdpClient, sendtoCarbonString);
                    }
                    else
                    {
                        string sendtoCarbonString = metricPathCleaning(inputXmlGraphiteConfigElement, pc.cPrefix + "." + pc.mPath) + " " + pc.vCalculated.ToString().Replace(",", ".") + " " + pc.secondsSinceEpoch;
                        sendUdpStream(inputUdpClient, sendtoCarbonString);
                    }
                }
            }
        }

        // ##########################################################################################################################################################

        static void sendUdpStream(UdpClient inputUdpClient, string inputStringToSend)
        {
            log.Debug("sendUdpStream(): " + inputStringToSend);

            try
            {
                byte[] sendBytes = Encoding.ASCII.GetBytes(inputStringToSend);
                inputUdpClient.Send(sendBytes, sendBytes.Length);
            }
            catch (Exception e)
            {
                log.Fatal("sendUdpStream(): Exception: " + e.ToString());
                inputUdpClient.Close();
                throw;
            }
            //finally
            //{
            //}

        }

        #endregion

        // ##########################################################################################################################################################

        static string metricPathCleaning(XmlElement inputXmlGraphiteConfigElement, string inputMetricPath)
        {
            string outputMetricPath = inputMetricPath.ToLower();
            //outputMetricPath = outputMetricPath.Replace(" {", "{");
            //outputMetricPath = outputMetricPath.Replace(" %", "%");
            //outputMetricPath = outputMetricPath.Replace("% ", "%");
            //outputMetricPath = outputMetricPath.Replace(" (", "(");
            //outputMetricPath = outputMetricPath.Replace(" #", "#");
            //outputMetricPath = outputMetricPath.Replace(" / sek", "/sek");
            //outputMetricPath = outputMetricPath.Replace(" /sek", "/sek");
            //outputMetricPath = outputMetricPath.Replace("æ", "e");
            //outputMetricPath = outputMetricPath.Replace("ø", "o");
            //outputMetricPath = outputMetricPath.Replace("å", "aa");
            ////outputMetricPath = outputMetricPath.Replace(", ", ".");
            //outputMetricPath = outputMetricPath.Replace(",", ".");
            //outputMetricPath = outputMetricPath.Replace(". ", ".");
            ////outputMetricPath = outputMetricPath.Replace(" ", "-");

            //outputMetricPath = regexReplace("\\s+", "-", outputMetricPath);
            //outputMetricPath = regexReplace("\\-+", "-", outputMetricPath);

            // ***********************************************************************************

            XmlNodeList myNodeList = inputXmlGraphiteConfigElement.SelectNodes(xPathMetricCleaningGraphite + "*");

            foreach (XmlNode myNode in myNodeList)
            {
                if (myNode.Name == "MetricReplace")
                {
                    outputMetricPath = regexReplace(myNode.Attributes["This"].Value, myNode.Attributes["With"].Value, outputMetricPath);
                }
            }

            return outputMetricPath;
        }

        // ##########################################################################################################################################################

        static string regexReplace(string pattern, string replacement, string inputString)
        {
            Regex rgx = new Regex(pattern);
            string outputString = rgx.Replace(inputString, replacement);

            return outputString;
        }

        // ##########################################################################################################################################################

        static bool excludePerformanceCategory(XmlElement inputXmlMsPerfAgentConfigElement, string inputCategory)
        {
            bool excludePerfCategoryBoolean = false;

            XmlNodeList myNodeList = inputXmlMsPerfAgentConfigElement.SelectNodes("/Configuration/ExcludePerfCategory/*");

            foreach (XmlNode myNode in myNodeList)
            {
                if (myNode.Name == "PerformanceCategory")
                {
                    if (inputCategory.ToLower() == myNode.Attributes["Name"].Value.ToLower())
                    {
                        excludePerfCategoryBoolean = true;
                        //log.Info("Performance counter category " + myNode.Attributes["Name"].Value + " is excluded");
                        //Console.WriteLine(myNode.Attributes["Name"].Value);
                    }
                }
            }

            return excludePerfCategoryBoolean;
        }

        // ##########################################################################################################################################################

        static bool filterMetric(XmlElement inputXmlGraphiteConfigElement, string inputMetric)
        {
            bool filterMetric = false;

            XmlNodeList myNodeList = inputXmlGraphiteConfigElement.SelectNodes("/Configuration/Filtering/*");

            foreach (XmlNode myNode in myNodeList)
            {
                if (myNode.Name == "MetricFilter")
                {
                    if (inputMetric.ToLower().Contains(myNode.Attributes["Name"].Value.ToLower()))
                    {
                        filterMetric = true;
                        //Console.WriteLine("Filtered metric: " + myNode.Attributes["Name"].Value);
                    }
                }
            }

            return filterMetric;
        }

        // ##########################################################################################################################################################

        static List<msPerfAgentCounter> processMsPerfAgentCounterList(XmlElement inputXmlMsPerfAgentConfigElement, List<msPerfAgentCounter> inputmsPerfAgentCounters)
        {
            int sampleInterval = Int32.Parse(msPerfAgentXml.getMsPerfAgentConfigFromXml(inputXmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "SampleInterval"));
            int maxDegreeOfParallelism = Int32.Parse(msPerfAgentXml.getMsPerfAgentConfigFromXml(inputXmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "MaxDegreeOfParallelism"));

            IEnumerable<msPerfAgentCounter> rawCounters = inputmsPerfAgentCounters.Where(isRc => (isRc.isRawCounter == true && isRc.counterIsHealthy == true));
            IEnumerable<msPerfAgentCounter> calculatedCounters = inputmsPerfAgentCounters.Where(isRc => (isRc.isRawCounter == false && isRc.counterIsHealthy == true));

            if (maxDegreeOfParallelism >= 1)
            {
                ParallelOptions po = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };

                // Create a new task to process rawCounters during caluclatedCounters sleep
                Task task = new Task(() => {
                    foreach (msPerfAgentCounter pc in rawCounters)
                    {
                        //pc.setCounterSample(1);
                        pc.setRawCounterValue();
                        pc.secondsSinceEpoch = getSecondsSinceEpoch(inputXmlMsPerfAgentConfigElement);
                    }
                });

                Parallel.ForEach(calculatedCounters, po, pc =>
                {
                    //pc.setCounterSample(1);
                    pc.setCalculatedCounterValue();
                });

                task.Start();
                Thread.Sleep(sampleInterval * 1000);
                task.Wait();

                Parallel.ForEach(calculatedCounters, po, pc =>
                {
                    //pc.setCounterSample(2);
                    pc.setCalculatedCounterValue();
                    pc.secondsSinceEpoch = getSecondsSinceEpoch(inputXmlMsPerfAgentConfigElement);
                });
            }
            else if(maxDegreeOfParallelism == 0)
            {
                // Create a new task to process rawCounters during caluclatedCounters sleep

                Task task = new Task(() => {
                    foreach (msPerfAgentCounter pc in rawCounters)
                    {
                        //pc.setCounterSample(1);
                        pc.setRawCounterValue();
                        pc.secondsSinceEpoch = getSecondsSinceEpoch(inputXmlMsPerfAgentConfigElement);
                    }
                });

                Parallel.ForEach(calculatedCounters, pc =>
                {
                    pc.setCalculatedCounterValue();
                });

                task.Start();
                Thread.Sleep(sampleInterval * 1000);
                task.Wait();

                Parallel.ForEach(calculatedCounters, pc =>
                {
                    pc.setCalculatedCounterValue();
                    pc.secondsSinceEpoch = getSecondsSinceEpoch(inputXmlMsPerfAgentConfigElement);
                });
            }
            else
            {
                foreach (msPerfAgentCounter pc in rawCounters)
                {
                    //pc.setCounterSample(1);
                    pc.setRawCounterValue();
                    pc.secondsSinceEpoch = getSecondsSinceEpoch(inputXmlMsPerfAgentConfigElement);
                }

                foreach (msPerfAgentCounter pc in calculatedCounters)
                {
                    pc.setCalculatedCounterValue();
                }

                Thread.Sleep(sampleInterval * 1000);

                foreach (msPerfAgentCounter pc in calculatedCounters)
                {
                    pc.setCalculatedCounterValue();
                    pc.secondsSinceEpoch = getSecondsSinceEpoch(inputXmlMsPerfAgentConfigElement);
                }
            }

            return inputmsPerfAgentCounters;
        }

        // ##########################################################################################################################################################

    }
}