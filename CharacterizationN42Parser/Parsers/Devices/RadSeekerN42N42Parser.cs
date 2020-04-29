using SPEAR.Models;
using SPEAR.Models.N42.v2011;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Xml.Serialization;

namespace SPEAR.Parsers.Devices
{
    public class RadSeekerN42N42Parser : FileParser
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public bool ErrorsOccurred;
        private List<KeyValuePair<string, string>> fileErrors;

        private RadInstrumentDataType radInstrumentData;
        private DeviceData deviceData;
        private List<DeviceData> deviceDatasParsed;

        private IEnumerable<string> filePaths;
        private string dateFormat = "yyyy-MM-ddTHH:mm:ssZ";
        
        public override string FileName { get { return "RadSeeker_N42N42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public RadSeekerN42N42Parser()
        {
            ErrorsOccurred = false;
            fileErrors = new List<KeyValuePair<string, string>>();
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            return Directory.GetFiles(directoryPath, "*_N42.n42");
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
            radInstrumentData = null;
            filePaths = null;
        }

        public void ParseFiles()
        {
            SortedList<DateTime, DeviceData> radSeekers = new SortedList<DateTime, DeviceData>();

            // Start Thread that archives .N42 files
            ThreadStart threadStart = new ThreadStart(ArchiveFiles);
            Thread thread = new Thread(threadStart);
            thread.Start();

            // Parse n42 files
            int filesCompleted = 0;
            foreach (string filePath in filePaths)
            {
                Invoke_ParsingUpdate((float)filesCompleted++ / (float)filePaths.Count());

                // Clear data
                Clear();

                // Deserialize file to N42 object
                if (DeserializeN42(filePath) == false)
                    continue;

                // Create RadSeeker and set FileName
                deviceData = new DeviceData(DeviceInfo.Type.RadSeeker);
                deviceData.FileName = Path.GetFileName(filePath);

                // Parse data from N42 object
                if (ParseN42File() == false)
                    continue;

                // Add to other parsed RadSeekers
                radSeekers.Add(deviceData.StartDateTime, deviceData);
            }

            // Number all the events
            int trailNumber = 1;
            foreach (KeyValuePair<DateTime, DeviceData> tempRadSeeker in radSeekers) {
                tempRadSeeker.Value.TrialNumber = trailNumber++;
            }

            if (ErrorsOccurred) {
                StringBuilder errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("The files listed below failed to parse..");
                int errorIndex;
                for (errorIndex = 0; errorIndex < fileErrors.Count && errorIndex < 8; errorIndex += 1) {
                    errorBuilder.AppendLine(string.Format("\t{0}", fileErrors[errorIndex].Key));
                }
                if (errorIndex < fileErrors.Count) 
                    errorBuilder.AppendLine(string.Format("\tand {0} others", fileErrors.Count - errorIndex));
                MessageBox.Show(errorBuilder.ToString(), "Parsing Error");
            }

            ClearErrors();

            // Wait for thread to zip files
            thread.Join();

            deviceDatasParsed = radSeekers.Values.ToList();
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Private Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        private void Clear()
        {
            radInstrumentData = null;
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
            foreach (string filePath in filePaths) {
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
            if (radInstrumentData == null || deviceData == null)
                return false;

            try {
                var instrumentInfo = radInstrumentData.RadInstrumentInformation;
                if (instrumentInfo == null)
                    return false;

                // Get DeviceType
                deviceData.DeviceType = instrumentInfo.RadInstrumentModelName;

                // Get SerialNumber
                deviceData.SerialNumber = instrumentInfo.RadInstrumentIdentifier;

                // Get needed nodes AnalysisResultsType and RadMeasurementType
                var itemsElementNames = radInstrumentData.ItemsElementName;
                if (itemsElementNames == null)
                    return false;
                AnalysisResultsType analysisResultsType = null;
                DerivedDataType derivedDataType = null;
                bool analysisFound = false, derivedDataFound = false;
                for (int i = 0; i < itemsElementNames.Count(); i += 1)
                {
                    if (analysisFound == false)
                    {
                        if (itemsElementNames[i] == ItemsChoiceType2.AnalysisResults)
                        {
                            analysisResultsType = radInstrumentData.Items[i] as AnalysisResultsType;
                            analysisFound = true;
                        }
                    }

                    if (derivedDataFound == false)
                    {
                        if (itemsElementNames[i] == ItemsChoiceType2.DerivedData)
                        {
                            derivedDataType = radInstrumentData.Items[i] as DerivedDataType;
                            // Check if correct node
                            if (derivedDataType.id.Contains("ProcessedData"))
                                derivedDataFound = true;
                        }
                    }

                    if (analysisFound && analysisFound)
                        break;
                }

                if (derivedDataFound == false || analysisFound == false)
                    return false;
                
                // Get StartDateTime
                deviceData.StartDateTime = derivedDataType.StartDateTime;

                // Get MeaureTime
                string value = derivedDataType.RealTimeDuration.Remove(0, 2);
                value = value.Remove(value.Length - 1, 1).Split('.').FirstOrDefault();
                deviceData.MeasureTime = new TimeSpan(0, 0, int.Parse(value));
                
                // Get Identified Nuclides
                int nuclideIndex = 0;
                var nuclides = analysisResultsType.NuclideAnalysisResults.Nuclide;
                for (int i = 0; i < nuclides.Count(); i += 1)
                {
                    // Check if the nuclide was identified
                    if (nuclides[i].NuclideIdentifiedIndicator == false)
                        continue;

                    var elementNames = nuclides[i].ItemsElementName;
                    for (int c = 0; c < elementNames.Length; c += 1)
                    {
                        if (elementNames[c] == ItemsChoiceType.NuclideIDConfidenceValue)
                        {
                            deviceData.Nuclides[nuclideIndex++] = new NuclideID(
                                nuclides[i].NuclideName, (double)nuclides[i].Items[c]);
                            break;
                        }
                    }
                }


                // Get CountRate
                var grossCountAnalysisResults = analysisResultsType.GrossCountAnalysisResults;
                if (grossCountAnalysisResults == null)
                    return true;
                deviceData.CountRate = grossCountAnalysisResults.AverageCountRateValue.Value;
            }
            catch (Exception ex) {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileName(deviceData.FileName), ex.Message));
                ErrorsOccurred = true;
                return false;
            }
            return true;
        }

        private bool DeserializeN42(string filePath)
        {
            XmlSerializer serializer;

            try {
                serializer = new XmlSerializer(typeof(RadInstrumentDataType));
                serializer.UnknownElement += new XmlElementEventHandler(Serializer_UnknownElement);
                serializer.UnknownAttribute += new XmlAttributeEventHandler(Serializer_UnknownAttribute);
                
                using (TextReader stream = File.OpenText(filePath)) {
                    radInstrumentData = serializer.Deserialize(stream) as RadInstrumentDataType;
                }

            }
            catch (Exception ex) {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(filePath), ex.Message));
                ErrorsOccurred = true;
                return false;
            }

            if (radInstrumentData == null)
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
