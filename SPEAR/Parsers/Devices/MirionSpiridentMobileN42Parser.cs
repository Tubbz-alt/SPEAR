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
    public class MirionSpiridentMobileN42Parser : FileParser
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public bool ErrorsOccurred;
        private List<KeyValuePair<string, string>> fileErrors;

        private RadInstrumentDataType radInstrumentDataType;
        private DeviceData deviceData;
        private List<DeviceData> deviceDatasParsed;

        private IEnumerable<string> filePaths;

        public override string FileName { get { return "MirionSpiridentMobile_N42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public MirionSpiridentMobileN42Parser()
        {
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
            radInstrumentDataType = null;
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Private Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        private void ParseFiles()
        {
            SortedList<DateTime, DeviceData> sortedDeviceDatas = new SortedList<DateTime, DeviceData>();

            // Start Thread that archives .N42 files
            ThreadStart threadStart = new ThreadStart(ArchiveFiles);
            Thread thread = new Thread(threadStart);
            thread.Start();

            // Parse spe files
            int filesCompleted = 0;
            foreach (string filePath in filePaths)
            {
                Invoke_ParsingUpdate((float)filesCompleted++ / (float)filePaths.Count());

                // Check if background file
                string fileName = Path.GetFileName(filePath);

                // Clear data
                Clear();

                // Deserialize file to N42 object
                if (DeserializeN42(filePath) == false)
                    continue;

                // Create RadSeeker and set FileName
                deviceData = new DeviceData(DeviceInfo.Type.MirionSpiridentMobile);
                deviceData.FileName = fileName;

                // Parse data from N42 object
                if (ParseN42File() == false)
                    continue;

                // Add to other parsed
                if (sortedDeviceDatas.ContainsKey(deviceData.StartDateTime))
                    continue;
                sortedDeviceDatas.Add(deviceData.StartDateTime, deviceData);
            }

            // Number all the events
            int trailNumber = 1;
            foreach (KeyValuePair<DateTime, DeviceData> device in sortedDeviceDatas)
                device.Value.TrialNumber = trailNumber++;

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

            deviceDatasParsed = sortedDeviceDatas.Values.ToList();
        }

        private void Clear()
        {
            radInstrumentDataType = null;
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
            if (radInstrumentDataType == null || deviceData == null)
                return false;

            try
            {
                var radInstrumentInformationType = radInstrumentDataType.RadInstrumentInformation;
                if (radInstrumentInformationType == null)
                    return false;

                // Get DeviceType
                deviceData.DeviceType = radInstrumentInformationType.RadInstrumentManufacturerName + " " + radInstrumentInformationType.RadInstrumentModelName;

                // Get SerialNumber
                deviceData.SerialNumber = radInstrumentInformationType.RadInstrumentIdentifier;

                // Get needed nodes AnalysisResultsType and RadMeasurementType
                var itemsElementNames = radInstrumentDataType.ItemsElementName;
                if (itemsElementNames == null)
                    return false;

                AnalysisResultsType analysisResultsType = null;
                RadMeasurementType radMeasurementType = null;
                RadMeasurementType lastRadMeasurementType = null;
                List<RadMeasurementType> radMeasureTypes = new List<RadMeasurementType>();
                bool analysisFound = false, radMeasurementFound = false;

                for (int i = 0; i < itemsElementNames.Count(); i += 1)
                {
                    if (analysisFound == false)
                    {
                        if (itemsElementNames[i] == ItemsChoiceType2.AnalysisResults)
                        {
                            analysisResultsType = radInstrumentDataType.Items[i] as AnalysisResultsType;
                            analysisFound = true;
                        }
                    }

                    if (itemsElementNames[i] == ItemsChoiceType2.RadMeasurement)
                    {
                        radMeasurementType = radInstrumentDataType.Items[i] as RadMeasurementType;
                        // Check if correct node
                        if (radMeasurementType.MeasurementClassCode == MeasurementClassCodeSimpleType.Foreground)
                        {
                            radMeasureTypes.Add(radMeasurementType);
                            lastRadMeasurementType = radMeasurementType;
                            radMeasurementFound = true;
                        }
                    }
                }

                if (radMeasurementFound == true)
                {
                    // Get StartTime
                    deviceData.StartDateTime = radMeasureTypes[0].StartDateTime_DateTime;

                    // Get MeasureTime
                    var value = lastRadMeasurementType.RealTimeDuration.Remove(0, 2);
                    var mIndex = value.IndexOf("M");
                    var sIndex = value.IndexOf("S");
                    var periodIndex = value.IndexOf(".");
                    if (mIndex != -1 && periodIndex != -1 && sIndex != -1)
                    {
                        deviceData.MeasureTime = new TimeSpan(0, 0,
                            int.Parse(value.Substring(0, mIndex)),
                            int.Parse(value.Substring(mIndex + 1, periodIndex - mIndex - 1)),
                            int.Parse(value.Substring(periodIndex + 1, 3)));
                    }
                    else if (mIndex != -1 && sIndex != -1)
                        deviceData.MeasureTime = new TimeSpan(0,
                            int.Parse(value.Substring(0, mIndex)),
                            int.Parse(value.Substring(mIndex + 1, sIndex - mIndex - 1)));
                    else if (mIndex != -1)
                        deviceData.MeasureTime = new TimeSpan(0, int.Parse(value.Substring(0, mIndex)), 0);
                    else if (sIndex != -1)
                        deviceData.MeasureTime = new TimeSpan(0, 0, int.Parse(value.Substring(0, sIndex)));

                    // Get CountRate
                    // Grabbed the first neutron gross count value from lastRadMeasurementFound.
                    foreach (GrossCountsType item in lastRadMeasurementType.GrossCounts)
                    {
                        if (string.Equals(item.radDetectorInformationReference, "Neutron"))
                        {
                            deviceData.CountRate = Double.Parse(item.CountData);
                        }
                    }
                }

                if (analysisFound == true)
                {
                    // Get Nuclides
                    var nuclides = analysisResultsType.NuclideAnalysisResults.Nuclide;
                    for (int i = 0; i < nuclides.Count(); i += 1)
                    {
                        var elementNames = nuclides[i].ItemsElementName;
                        for (int c = 0; c < elementNames.Length; c += 1)
                        {
                            if (elementNames[c] == ItemsChoiceType.NuclideIDConfidenceValue)
                            {
                                deviceData.Nuclides[i] = new NuclideID(nuclides[i].NuclideName, Convert.ToDouble(nuclides[i].Items[c]));
                                break;
                            }
                        }
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
                serializer = new XmlSerializer(typeof(RadInstrumentDataType));
                serializer.UnknownElement += new XmlElementEventHandler(Serializer_UnknownElement);
                serializer.UnknownAttribute += new XmlAttributeEventHandler(Serializer_UnknownAttribute);

                using (TextReader stream = File.OpenText(filePath))
                {
                    radInstrumentDataType = serializer.Deserialize(stream) as RadInstrumentDataType;
                }
            }
            catch (Exception ex)
            {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(filePath), "Deserializing N42 file failed: " + ex.Message));
                ErrorsOccurred = true;
                return false;
            }

            if (radInstrumentDataType == null)
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
