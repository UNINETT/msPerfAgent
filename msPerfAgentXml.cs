using System;
using System.Xml;

namespace Uninett.MsPerfAgent
{
    static class msPerfAgentXml
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //private static string msPerfAgentPath = @"C:\tmp\msPerfAgent\conf\";
        private static string msPerfAgentPath = @"C:\Program Files\msPerfAgent\conf\";
        
        private static string msPerfAgentConfigFile = "msPerfAgentConfig.xml";

        private static string xPathConfigurationGraphite = "/Configuration/Graphite/";
        private static string xPathConfigurationMsPerfAgent = "/Configuration/MsPerfAgent/";

        public static XmlElement xmlGetMsPerfAgentConfigXmlElement()
        {
            XmlDocument xmlDoc = new XmlDocument();

            try
            {
                xmlDoc.Load(msPerfAgentPath + msPerfAgentConfigFile);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Exception in loading XML-file: " + msPerfAgentPath + msPerfAgentConfigFile);
                System.Console.WriteLine("StackTrace: " + e.ToString());
            }

            XmlElement xmlElements = xmlDoc.DocumentElement;

            return xmlElements;
        }

        // ##########################################################################################################################################################

        public static XmlElement xmlGetGraphiteConfigXmlElement(XmlElement inputXmlMsPerfAgentConfigElement)
        {
            //Create the XmlDocument.
            XmlDocument xmlDoc = new XmlDocument();

            try
            {
                xmlDoc.Load(getMsPerfAgentConfigFromXml(inputXmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "StatsToGraphiteConfig"));
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Exception in loading XML-file: " + getMsPerfAgentConfigFromXml(inputXmlMsPerfAgentConfigElement, xPathConfigurationMsPerfAgent + "StatsToGraphiteConfig"));
                System.Console.WriteLine("StackTrace: " + e.ToString());
            }

            XmlElement xmlElements = xmlDoc.DocumentElement;

            return xmlElements;
        }

        // ##########################################################################################################################################################

        public static string getMsPerfAgentConfigFromXml(XmlElement inputXmlElement, string xmlPath)
        {
            XmlNode node = null;
            string outputString = "";

            try
            {
                node = inputXmlElement.SelectSingleNode(xmlPath);
            }
            catch (System.Xml.XPath.XPathException xe)
            {
                System.Console.WriteLine("Method getMsPerfAgentConfigFromXml -> XPathException in: " + xmlPath + msPerfAgentPath + msPerfAgentConfigFile);
                System.Console.WriteLine("Stacktrace: " + xe.ToString());
            }


            if (node != null)
            {
                try
                {
                    outputString = node.InnerText;
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("Method getMsPerfAgentConfigFromXml -> Exception StackTrace: " + e.ToString());
                    //throw;
                }
            }

            return outputString;
        }

        // ##########################################################################################################################################################

        public static string getGraphiteConfigurationFromXml(XmlElement inputXmlElement, string configOption)
        {
            string outputString = "";

            try
            {
                XmlNode node = inputXmlElement.SelectSingleNode(xPathConfigurationGraphite + configOption);
                outputString = node.InnerText;
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Method getGraphiteConfigurationFromXml -> Exception StackTrace: " + e.ToString());
            }

            return outputString;
        }
    }
}
