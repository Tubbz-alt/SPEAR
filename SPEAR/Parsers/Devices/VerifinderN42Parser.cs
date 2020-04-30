using SPEAR.Models;
using SPEAR.Models.N42.v2011;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Xml.Serialization;

namespace SPEAR.Parsers.Devices
{
    public class VerifinderN42Parser : FileParser
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
        
        public override string FileName { get { return "Verifinder_N42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public VerifinderN42Parser()
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
            radInstrumentData = null;
            filePaths = null;
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Private Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        private void ParseFiles()
        {
            SortedList<DateTime, DeviceData> verifinders = new SortedList<DateTime, DeviceData>();

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
                deviceData = new DeviceData(DeviceInfo.Type.Verifinder);
                deviceData.FileName = Path.GetFileName(filePath);

                // Parse data from N42 object
                if (ParseN42File() == false)
                    continue;

                // Add to other parsed RadSeekers
                verifinders.Add(deviceData.StartDateTime, deviceData);
            }

            // Number all the events
            int trailNumber = 1;
            foreach (KeyValuePair<DateTime, DeviceData> tempRadSeeker in verifinders)
            {
                tempRadSeeker.Value.TrialNumber = trailNumber++;
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

            deviceDatasParsed = verifinders.Values.ToList();
        }

        private void Clear()
        {
            deviceData = null;
            radInstrumentData = null;
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
            if (radInstrumentData == null || deviceData == null)
                return false;

            try
            {
                RadInstrumentInformationType instrumentInfo = radInstrumentData.RadInstrumentInformation;
                if (instrumentInfo == null)
                    return false;

                // Get DeviceType
                deviceData.DeviceType = instrumentInfo.RadInstrumentModelName;

                // Get SerialNumber
                deviceData.SerialNumber = instrumentInfo.RadInstrumentIdentifier.Split('-').LastOrDefault();

                // Find AnalysisResults and DerivedData
                AnalysisResultsType analysisResults = null;
                DerivedDataType derivedData = null;
                ItemsChoiceType2[] itemsChoices2 = radInstrumentData.ItemsElementName;
                for (int i = itemsChoices2.Length - 1; i >= 0; i -= 1) {
                    if (analysisResults == null) {
                        if (itemsChoices2[i] == ItemsChoiceType2.AnalysisResults) {
                            analysisResults = radInstrumentData.Items[i] as AnalysisResultsType;
                            // Check if correct Id
                            if (analysisResults.id.StartsWith("Identification") == true)
                                continue;
                            analysisResults = null;
                        }
                    }
                    if (derivedData == null) {
                        if (itemsChoices2[i] == ItemsChoiceType2.DerivedData) {
                            derivedData = radInstrumentData.Items[i] as DerivedDataType;
                            // Check if correct Id
                            if (derivedData.id.StartsWith("ForegroundMeasureSum") == true)
                                continue;
                            derivedData = null;
                        }
                    }
                    if (analysisResults != null && derivedData != null)
                        break;
                }

                // Get StartDateTime
                if (analysisResults == null || analysisResults.AnalysisStartDateTimeSpecified == false)
                    return false;
                deviceData.StartDateTime = analysisResults.AnalysisStartDateTime;

                // Get Identified Nuclides
                if (analysisResults.NuclideAnalysisResults == null)
                    return true;
                NuclideType[] nuclides = analysisResults.NuclideAnalysisResults.Nuclide;
                if (nuclides == null)
                    return true;
                for (int i = 0; i < nuclides.Length; i += 1)
                {
                    double confidence = -1;
                    ItemsChoiceType[] itemsChoices = nuclides[i].ItemsElementName;
                    for (int c = 0; c < itemsChoices.Length; c += 1) {
                        if (itemsChoices[c] == ItemsChoiceType.NuclideIDConfidenceValue) {
                            confidence = (double)nuclides[i].Items[c];
                            break;
                        }
                    }
                    deviceData.Nuclides[i] = new NuclideID(
                        nuclides[i].NuclideName, confidence);
                }

                // Get MeasureTime
                if (derivedData == null)
                    return false;
                // Format is PTsss.fffS
                string value = derivedData.RealTimeDuration.Remove(0, 2);
                value = value.Remove(value.Length - 1, 1);
                deviceData.MeasureTime = new TimeSpan(0, 0, (int)Math.Round(double.Parse(value)));

                // Get Count Rate
                SpectrumType spectrumType = derivedData.Spectrum.Where(s => s.id == "ForegroundSumGamma").FirstOrDefault();
                if (spectrumType == null)
                    return false;
                // Sum spectrum and divide by MeasureTime
                int sum = spectrumType.ChannelData.Text.Split(Globals.Delim_Space, StringSplitOptions.RemoveEmptyEntries).Sum(c => int.Parse(c));
                deviceData.CountRate = sum / deviceData.MeasureTime.TotalSeconds;
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
                serializer = new XmlSerializer(typeof(RadInstrumentDataType));
                serializer.UnknownElement += new XmlElementEventHandler(Serializer_UnknownElement);
                serializer.UnknownAttribute += new XmlAttributeEventHandler(Serializer_UnknownAttribute);
                
                using (TextReader stream = File.OpenText(filePath))
                {
                    radInstrumentData = serializer.Deserialize(stream) as RadInstrumentDataType;
                }
            }
            catch (Exception ex)
            {
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
