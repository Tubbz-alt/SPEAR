using SPEAR.Models;
using SPEAR.Models.N42.v2011;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;


namespace SPEAR.Parsers.Devices
{
    public class NuviaRadScoutN42Parser: FileParser
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

        public override string FileName { get { return "NuviaRadScout_N42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public NuviaRadScoutN42Parser()
        {
            fileErrors = new List<KeyValuePair<string, string>>();
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            return Directory.GetFiles(directoryPath, "*.xml");
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
                deviceData = new DeviceData(DeviceInfo.Type.NuviaRadScout);
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

                // Get needed nodes and necessary data from AnalysisResultsType, DerivedDataType, and RadMeasurementType
                var itemsElementNames = radInstrumentDataType.ItemsElementName;
                if (itemsElementNames == null)
                    return false;

                AnalysisResultsType analysisResultsType = null;
                DerivedDataType firstDerivedDataType = null;
                DerivedDataType tempDerivedDataType = null;
                RadMeasurementType radMeasurementType = null;

                List<DerivedDataType> derivedDataTypes = new List<DerivedDataType>();
                List<RadMeasurementType> radMeasureTypes = new List<RadMeasurementType>();

                bool analysisFound = false, firstDerivedDataFound = false;


                for (int i = 0; i < itemsElementNames.Count(); i += 1)
                {
                    if (analysisFound == false)
                    {
                        if (itemsElementNames[i] == ItemsChoiceType2.AnalysisResults)
                        {
                            analysisResultsType = radInstrumentDataType.Items[i] as AnalysisResultsType;
                            if (analysisResultsType.NuclideAnalysisResults != null)
                            {
                                analysisFound = true;
                            }
                        }
                    }

                    if (itemsElementNames[i] == ItemsChoiceType2.DerivedData)
                    {
                        if (firstDerivedDataFound == false)
                        {
                            firstDerivedDataType = radInstrumentDataType.Items[i] as DerivedDataType;
                            // Check if correct node
                            if (firstDerivedDataType.MeasurementClassCode == MeasurementClassCodeSimpleType.Foreground)
                                derivedDataTypes.Add(firstDerivedDataType);
                                firstDerivedDataFound = true;
                        }
                        else
                        {
                            tempDerivedDataType = radInstrumentDataType.Items[i] as DerivedDataType;
                            if (tempDerivedDataType.MeasurementClassCode == MeasurementClassCodeSimpleType.Foreground)
                                derivedDataTypes.Add(tempDerivedDataType);
                        }
                    }

                    if (itemsElementNames[i] == ItemsChoiceType2.RadMeasurement)
                    {
                        radMeasurementType = radInstrumentDataType.Items[i] as RadMeasurementType;
                        // Check if correct node
                        if (radMeasurementType.MeasurementClassCode == MeasurementClassCodeSimpleType.Foreground)
                            radMeasureTypes.Add(radMeasurementType);
                    }
                }

                if (firstDerivedDataFound == true)
                {
                    // Get StartTime
                    deviceData.StartDateTime = firstDerivedDataType.StartDateTime;

                    // Get MeasureTime
                    deviceData.MeasureTime = new TimeSpan(0, 0, 0);
                    foreach (DerivedDataType item in derivedDataTypes)
                    {
                        string time = item.RealTimeDuration.Remove(0, 2);
                        var mIndex = time.IndexOf("M");
                        var sIndex = time.IndexOf("S");
                        var periodIndex = time.IndexOf(".");
                        if (mIndex != -1 && periodIndex != -1 && sIndex != -1)
                        {
                            deviceData.MeasureTime = deviceData.MeasureTime.Add(new TimeSpan(0, 0,
                                int.Parse(time.Substring(0, mIndex)),
                                int.Parse(time.Substring(mIndex + 1, periodIndex - mIndex - 1)),
                                int.Parse(time.Substring(periodIndex + 1, sIndex - periodIndex - 1))));
                        }
                        else if (mIndex != -1 && sIndex != -1)
                            deviceData.MeasureTime = deviceData.MeasureTime.Add(new TimeSpan(0,
                                int.Parse(time.Substring(0, mIndex)),
                                int.Parse(time.Substring(mIndex + 1, sIndex - mIndex - 1))));
                        else if (mIndex != -1)
                            deviceData.MeasureTime = deviceData.MeasureTime.Add(new TimeSpan(0, int.Parse(time.Substring(0, mIndex)), 0));
                        else if (periodIndex != -1 && sIndex != -1)
                            deviceData.MeasureTime = deviceData.MeasureTime.Add(new TimeSpan(0, 0, 0,
                                int.Parse(time.Substring(0, periodIndex)),
                                int.Parse(time.Substring(periodIndex + 1, sIndex - periodIndex - 1))));
                        else if (sIndex != -1)
                            deviceData.MeasureTime = deviceData.MeasureTime.Add(new TimeSpan(0, 0, int.Parse(time.Substring(0, sIndex))));
                    }
                }


                // Get CountRate
                int sumCountData = 0;
                foreach (RadMeasurementType item in radMeasureTypes)
                {
                    foreach (GrossCountsType grossCounts in item.GrossCounts)
                    {
                        int num;
                        bool castSuccess = int.TryParse(grossCounts.CountData, out num);
                        if (castSuccess)
                            sumCountData += num;
                    }
                }
                deviceData.CountRate = sumCountData;

                //////// Need to edit this section to properly retrieve the nuclide results from analysisResults ////////
                // Get Identified Nuclides
                if (analysisFound == true)
                {
                    var nuclides = analysisResultsType.NuclideAnalysisResults.Nuclide;
                    for (int i = 0; i < nuclides.Count(); i += 1)
                    {
                        var elementNames = nuclides[i].ItemsElementName;
                        for (int c = 0; c < elementNames.Count(); c += 1)
                        {
                            if (elementNames[c] == ItemsChoiceType.NuclideIDConfidenceValue)
                            {
                                deviceData.Nuclides[i] = new NuclideID(nuclides[i].NuclideName, (double)nuclides[i].Items[c]);
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