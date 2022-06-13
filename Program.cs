using System;
using System.Diagnostics;
using System.Security;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Generic;
using System.Xml;

namespace DbDifferenceGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            GenerateDeltaScript();

            var deltaObjects = GetDeltaObjects();

            if (deltaObjects.Count > 0)
                SendDeltaEmail(deltaObjects);
            else
                Console.WriteLine("No difference found.");


            Console.WriteLine("Process is completed.");
            Console.ReadKey();
        }

        private static void SendDeltaEmail(List<string> deltaObjects)
        {
            try
            {
                Console.WriteLine("Sending email notification...");

                var smtpService = new SMTPMailSenderService();
                var smtpServerAddress = ConfigurationManager.AppSettings["SMTPServerAddress"];
                var smtpServerPort = int.Parse(ConfigurationManager.AppSettings["SMTPServerPort"]);
                var from = ConfigurationManager.AppSettings["From"];
                var to = ConfigurationManager.AppSettings["To"];
                var subject = ConfigurationManager.AppSettings["Subject"] + " " + DateTime.UtcNow; // DateTime.UtcNow.ToString("dd/MM/yyyy");
                var body = GetEmailBody(deltaObjects);

                var task = Task.Run(() => smtpService.SendMessage(from, to, subject, body, true, smtpServerAddress, smtpServerPort));
                   
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Sending email notification task failed.");
            }
        }

        private static string GetEmailBody(List<string> deltaObjects)
        {
            // Load XML document
            var path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            path = path.Substring(6);
            path += "\\EmailTemplate.xml";
            XmlDocument doc = new XmlDocument();
            doc.Load(path);

            var xmlBody = doc.ChildNodes[1].OuterXml;

            var deltaObjectIntegratedXML = FormDeltaXml(xmlBody, deltaObjects);

            var sourceDB = ConfigurationManager.AppSettings["SourceDbArgs"];
            var sourceAttributeIndex = sourceDB.IndexOf("Server");
            sourceDB = sourceDB.Substring(sourceAttributeIndex, (sourceDB.IndexOf("/tf") - sourceAttributeIndex));
            
            var targetDB = ConfigurationManager.AppSettings["TargetDbArgs"];
            sourceAttributeIndex = targetDB.IndexOf("Server");
            targetDB = targetDB.Substring(sourceAttributeIndex, (targetDB.IndexOf("/tf") - sourceAttributeIndex));

            deltaObjectIntegratedXML = deltaObjectIntegratedXML.Replace("{0}", sourceDB);
            deltaObjectIntegratedXML = deltaObjectIntegratedXML.Replace("{1}", targetDB);
            deltaObjectIntegratedXML = deltaObjectIntegratedXML.Replace("\r\n", "");

            return deltaObjectIntegratedXML;
        }

        private static string FormDeltaXml(string xmlBody, List<string> deltaObjects)
        {
            var tableData = "<td>";
            var sequenceNumber = 0;
            foreach (var item in deltaObjects.Distinct())
            {
                tableData += "<tr>";
                tableData += "<td>" + ++sequenceNumber + "</td>";
                tableData += "<td>" + "Stored Procedure" + "</td>";
                tableData += "<td>" + item + "</td>";
                //tableData += "<td>" + "To be taken" + "</td>";
                tableData += "</tr>";
            }
            tableData += "</td>";
            xmlBody = xmlBody.Replace("{2}", tableData);
            return xmlBody;
        }

        private static List<string> GetDeltaObjects()
        {
            Console.WriteLine("Extracting delta objects from script...");
            var outputFile = ConfigurationManager.AppSettings["OutputFilePath"];
            var fileData = System.IO.File.ReadAllLines(outputFile);

            var deltaObjects = fileData.Where(x => x.Contains("Altering") || x.Contains("Creating") || x.Contains("Dropping"))
                .Select(x => x.Substring(17, x.Length - 22)).ToList();

            return deltaObjects;
        }

        private static void GenerateDeltaScript()
        {
            var sqlPackagePath = ConfigurationManager.AppSettings["SQLPackagePath"];
            try
            {
                Console.WriteLine("\nGenerating dacpac and delta script...");
                var startInfo = new ProcessStartInfo();
                startInfo.WorkingDirectory = sqlPackagePath;
                startInfo.FileName = @"sqlpackage.exe";
                startInfo.Arguments = ConfigurationManager.AppSettings["SourceDbArgs"];
                startInfo.CreateNoWindow = false;
                var process1 = Process.Start(startInfo);

                startInfo.Arguments = ConfigurationManager.AppSettings["TargetDbArgs"];
                var process2 = Process.Start(startInfo);

                process1.WaitForExit();
                process2.WaitForExit();

                startInfo.Arguments = ConfigurationManager.AppSettings["DeltaCommandArgs"];
                var process3 = Process.Start(startInfo);
                process3.WaitForExit();
                Console.WriteLine("\nFinished with delta generation.");
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
