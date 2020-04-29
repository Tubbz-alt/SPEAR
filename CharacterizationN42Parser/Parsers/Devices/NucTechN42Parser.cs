using SPEAR.Models;
using SPEAR.Models.N42.v2006;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace SPEAR.Parsers.Devices
{
    public class NucTechN42Parser : FileParser
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public bool ErrorsOccurred;
        private List<KeyValuePair<string, string>> fileErrors;
        
        private N42InstrumentData n42InstrumentData;
        private DeviceData deviceData;
        private List<DeviceData> deviceDatasParsed;

        private IEnumerable<string> filePaths;

        private string[] nuclideDelim = new string[] { "name:", "confidence:", "type:" };
        
        public override string FileName { get { return "NucTech_N42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public NucTechN42Parser()
        {
            ErrorsOccurred = false;
            fileErrors = new List<KeyValuePair<string, string>>();
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            return Directory.GetFiles(directoryPath, "*.n42");
        }

        public override void InitializeFilePaths(IEnumerable<string> allFilePaths)
        {
            filePaths = allFilePaths;
            deviceDatasParsed = new List<DeviceData>();
        }

        public override void Parse()
        {
            Invoke_ParsingStarted();
            ParseFiles();
            Invoke_ParsingComplete(deviceDatasParsed);
        }

        public override void Cleanup()
        {
            deviceDatasParsed = new List<DeviceData>();
            n42InstrumentData = null;
            filePaths = null;
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Private Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        private void ParseFiles()
        {
            SortedList<int, DeviceData> nucTechs = new SortedList<int, DeviceData>();

            // Start Thread that archives .N42 files
            ThreadStart threadStart = new ThreadStart(ArchiveFiles);
            Thread thread = new Thread(threadStart);
            thread.Start();

            // Parse n42 files
            int filesCompleted = 0;
            foreach (string filePath in filePaths)
            {
                Invoke_ParsingUpdate((float)filesCompleted++ / (float)filePaths.Count());

                // Check if background file
                string fileName = Path.GetFileName(filePath);
                if (fileName.Contains("bkg"))
                    continue;

                // Get trail number
                string[] splitResults = Path.GetFileNameWithoutExtension(fileName).Split(Globals.Delim_UnderLine, StringSplitOptions.RemoveEmptyEntries);
                string trialString = splitResults.LastOrDefault();
                if (trialString == null)
                    continue;
                if (int.TryParse(trialString, out int trialNumber) == false)
                    continue;

                // Clear data
                Clear();

                // Deserialize file to N42 object
                if (DeserializeN42(filePath) == false)
                    continue;

                // Create nucTech and set FileName
                deviceData = new DeviceData(DeviceInfo.Type.NucTech);
                deviceData.FileName = fileName;

                // Parse data from N42 object
                if (ParseN42File() == false)
                    continue;

                // Add to other parsed RadSeekers
                nucTechs.Add(trialNumber, deviceData);
            }

            // Renumber all the events
            int index = 1;
            foreach (KeyValuePair<int, DeviceData> nuctech in nucTechs)
            {
                nuctech.Value.TrialNumber = index++;
            }

            if (ErrorsOccurred)
            {
                StringBuilder errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("The files listed below failed to parse..");
                int errorIndex;
                for (errorIndex = 0; errorIndex < fileErrors.Count && errorIndex < 8; errorIndex += 1)
                {
                    errorBuilder.AppendLine(string.Format("\t{0}", fileErrors[errorIndex].Key));
                }
                if (errorIndex < fileErrors.Count)
                    errorBuilder.AppendLine(string.Format("\tand {0} others", fileErrors.Count - errorIndex));
                MessageBox.Show(errorBuilder.ToString(), "Parsing Error");
            }
            ClearErrors();

            // Wait for thread to zip files
            thread.Join();

            deviceDatasParsed = nucTechs.Values.ToList();
        }

        private void Clear()
        {
            n42InstrumentData = null;
            deviceData = null;
        }

        private void ClearErrors()
        {
            ErrorsOccurred = false;
            fileErrors = new List<KeyValuePair<string, string>>();
        }

        private void ArchiveFiles()
        {
            if (filePaths.Count() == 0)
                return;

            // Get base directory
            string baseDirectory = Path.GetDirectoryName(filePaths.First());

            // Create temp directory
            if (Directory.Exists(MainWindow.ArchiveName))
                Directory.Delete(MainWindow.ArchiveName, true);
            Directory.CreateDirectory(MainWindow.ArchiveName);

            // Copy files to temp archive
            foreach (string filePath in filePaths)
            {
                string destFilePath = Path.Combine(MainWindow.ArchiveName, Path.GetFileName(filePath));
                File.Copy(filePath, destFilePath);
            }

            // Zip archive
            string zipFilePath = Path.Combine(baseDirectory, Path.ChangeExtension(MainWindow.ArchiveName, ".zip"));
            if (File.Exists(zipFilePath) == true)
                File.Delete(zipFilePath);
            ZipFile.CreateFromDirectory(MainWindow.ArchiveName, zipFilePath, CompressionLevel.Optimal, false);

            // Delete temp archive
            Directory.Delete(MainWindow.ArchiveName, true);
        }

        private bool ParseN42File()
        {
            if (n42InstrumentData == null || deviceData == null)
                return false;

            try
            {
                Measurement measurement = n42InstrumentData.Measurement.FirstOrDefault();
                if (measurement == null)
                    return false;

                XmlElement element = measurement.Any.Where(x => x.Name == "InstrumentInformation").FirstOrDefault();
                if (element == null)
                    return false;
                foreach (XmlNode node in element.ChildNodes)
                {
                    // Get DeviceType
                    if (node.Name.EndsWith("Model"))
                        deviceData.DeviceType = node.InnerText;
                    // Get SerialNumber
                    else if (node.Name.EndsWith("ID"))
                        deviceData.SerialNumber = node.InnerText.Split('_').LastOrDefault();
                }

                element = measurement.Any.Where(x => x.Name == "Spectrum").FirstOrDefault();
                if (element == null)
                    return false;
                foreach (XmlNode node in element.ChildNodes)
                {
                    // Get StartDateTime
                    if (node.Name.StartsWith("StartTime"))
                    {
                        if (DateTime.TryParse(node.InnerText, out DateTime date))
                            deviceData.StartDateTime = date;
                        continue;
                    }
                    
                    // Get MeaureTime
                    if (node.Name.StartsWith("RealTime"))
                    {
                        if (TimeSpan.TryParseExact(node.InnerText, @"\P\Th\Hm\Ms\S", CultureInfo.InvariantCulture, out TimeSpan time))
                            deviceData.MeasureTime = time;
                        break;
                    }
                }

                // Get Identified Nuclides
                element = measurement.Any.Where(x => x.Name == "AnalysisResults").FirstOrDefault();
                if (element == null)
                    return true;
                foreach (XmlNode nuclideAnalsis in element.ChildNodes)
                {
                    if (nuclideAnalsis.Name == "NuclideAnalysis")
                    {
                        for (int i = 0; i < nuclideAnalsis.ChildNodes.Count; i += 1)
                        {
                            XmlNode nuclide = nuclideAnalsis.ChildNodes.Item(i);
                            string name = string.Empty;
                            double confidence = 0;
                            foreach (XmlNode item in nuclide.ChildNodes)
                            {
                                if (item.Name.EndsWith("Name") == true)
                                    name = item.InnerText.Trim().Replace(" ", string.Empty);
                                else if (item.Name.EndsWith("Indication") == true)
                                    confidence = double.Parse(item.InnerText) / 100;
                            }
                            deviceData.Nuclides[i] = new NuclideID(name, confidence);
                        }
                        break;
                    }
                }


            }
            catch (Exception ex)
            {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileName(deviceData.FileName), ex.Message));
                ErrorsOccurred = true;
                return false;
            }
            return true;
        }

        private bool DeserializeN42(string filePath)
        {
            XmlSerializer serializer;

            try
            {
                serializer = new XmlSerializer(typeof(N42InstrumentData));
                serializer.UnknownElement += new XmlElementEventHandler(Serializer_UnknownElement);
                serializer.UnknownAttribute += new XmlAttributeEventHandler(Serializer_UnknownAttribute);

                using (TextReader stream = File.OpenText(filePath))
                {
                    n42InstrumentData = serializer.Deserialize(stream) as N42InstrumentData;
                }

            }
            catch (Exception ex)
            {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(filePath), ex.Message));
                ErrorsOccurred = true;
                return false;
            }

            if (n42InstrumentData == null)
                return false;

            return true;
        }

        private void Serializer_UnknownElement(object sender, XmlElementEventArgs e)
        {
            //MessageBox.Show("Element Name: " + e.Element.Name);
            return;
        }

        private void Serializer_UnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
            //MessageBox.Show("Attribute Name: " + e.Attr.Name);
            return;
        }
    }
}
