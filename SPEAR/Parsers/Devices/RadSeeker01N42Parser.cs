using SPEAR.Models;
using SPEAR.Models.Schemas.Event;
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
    public class RadSeeker01N42Parser : FileParser
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public bool ErrorsOccurred;
        private List<KeyValuePair<string, string>> fileErrors;

        private Event eventN42;
        private DeviceData deviceData;
        private List<DeviceData> deviceDatasParsed;

        private IEnumerable<string> filePaths;
        private string dateFormat = "yyyy-MM-ddTHH:mm:ssZ";
        
        public override string FileName { get { return "RadSeeker_UN42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public RadSeeker01N42Parser()
        {
            ErrorsOccurred = false;
            fileErrors = new List<KeyValuePair<string, string>>();
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            return Directory.GetFiles(directoryPath, "*_01.n42");
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
            eventN42 = null;
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
            eventN42 = null;
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
            if (eventN42 == null || deviceData == null)
                return false;

            try {
                var analysisResults = eventN42.AnalysisResults.FirstOrDefault();
                if (analysisResults == null)
                    return false;

                // Get StartTime    
                deviceData.StartDateTime = eventN42.OnsetDateTime;

                // Get Nuclides
                var nuclides = analysisResults.RadiationDataAnalysis.Nuclide;
                for (int i = 0; i < nuclides.Length; i += 1) {
                    deviceData.Nuclides[i] = new NuclideID(nuclides[i].NuclideName, double.Parse(nuclides[i].NuclideIDConfidence));
                }

                var measurement = eventN42.N42InstrumentData.Measurement.FirstOrDefault();
                if (measurement == null)
                    return false;
                var detectorData = measurement.DetectorData.FirstOrDefault();
                if (detectorData == null)
                    return false;
                var spectrum = detectorData.Spectrum.FirstOrDefault();

                // Get MeaureTime (in format PTss.ffffffS)
                string value = spectrum.RealTime.Remove(0, 2);
                value = value.Remove(value.Length - 1, 1).Split('.').FirstOrDefault();
                deviceData.MeasureTime = new TimeSpan(0, 0, int.Parse(value));

                // Get CountRate
                List<double> spectrumData = spectrum.ChannelData.FirstOrDefault().Data
                    .Split(Globals.Delim_Space, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => double.Parse(x))
                    .ToList();
                deviceData.CountRate = spectrumData.Sum() / spectrumData.Count;


                //// Get StartDateTime and MeasureTime
                //DateTime dateTime;
                //foreach (RadMeasurement measurement in radInstrumentData.RadMeasurement) {
                //    if (measurement.id.StartsWith("Event_") == true) {
                //        // Get StartDateTime
                //        if (DateTime.TryParseExact(measurement.StartDateTime, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dateTime))
                //            deviceData.StartDateTime = dateTime;
                //        // Get MeaureTime
                //        string value = measurement.RealTimeDuration.Remove(0, 2);
                //        value = value.Remove(value.Length - 1, 1).Split('.').FirstOrDefault();
                //        deviceData.MeasureTime = new TimeSpan(0, 0, int.Parse(value));
                //        break;
                //    }
                //}

                //// Get Identified Nuclides
                //AnalysisResults analysisResults = radInstrumentData.AnalysisResults.FirstOrDefault();
                //if (analysisResults == null)
                //    return true;
                //NuclideAnalysisResultsNuclide[] nuclideAnalysisResults = analysisResults.NuclideAnalysisResults;
                //if (nuclideAnalysisResults == null)
                //    return true;
                //for (int i = 0; i < nuclideAnalysisResults.Length; i += 1) {
                //    deviceData.Nuclides[i] = new NuclideID(
                //        nuclideAnalysisResults[i].NuclideName.Replace(",", "/"), double.Parse(nuclideAnalysisResults[i].NuclideIDConfidenceValue));
                //}

                //// Get CountRate
                //GrossCountAnalysisResults grossCountAnalysisResults = analysisResults.GrossCountAnalysisResults.FirstOrDefault();
                //if (grossCountAnalysisResults == null)
                //    return true;
                //deviceData.CountRate = double.Parse(grossCountAnalysisResults.AverageCountRateValue);
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
                serializer = new XmlSerializer(typeof(Event));
                serializer.UnknownElement += new XmlElementEventHandler(Serializer_UnknownElement);
                serializer.UnknownAttribute += new XmlAttributeEventHandler(Serializer_UnknownAttribute);
                
                using (TextReader stream = File.OpenText(filePath)) {
                    eventN42 = serializer.Deserialize(stream) as Event;
                }

            }
            catch (Exception ex) {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(filePath), ex.Message));
                ErrorsOccurred = true;
                return false;
            }

            if (eventN42 == null)
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
