﻿using SPEAR.Models;
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
    public class ThermoRadHaloN42Parser : FileParser
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

        public override string FileName { get { return "ThermoRadHalo_N42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public ThermoRadHaloN42Parser()
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
            SortedList<DateTime, List<DeviceData>> sortedDeviceDatas = new SortedList<DateTime, List<DeviceData>>();

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
                deviceData = new DeviceData(DeviceInfo.Type.ThermoRadHalo);
                deviceData.FileName = fileName;

                // Parse data from N42 object
                if (ParseN42File() == false)
                    continue;

                // This device has start times that are identical
                if (sortedDeviceDatas.ContainsKey(deviceData.StartDateTime))
                    sortedDeviceDatas[deviceData.StartDateTime].Add(deviceData);
                else
                    sortedDeviceDatas.Add(deviceData.StartDateTime, new List<DeviceData> { deviceData });
            }

            // Number all the events
            var trailNumber = 1;
            foreach (var deviceList in sortedDeviceDatas)
            {
                foreach (var device in deviceList.Value)
                {
                    device.TrialNumber = trailNumber++;
                }
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

            deviceDatasParsed = sortedDeviceDatas.Values.SelectMany(x => x).ToList();
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
                    throw new Exception("'radInstrumentInformationType' not found");

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

                    if (radMeasurementFound == false)
                    {
                        if (itemsElementNames[i] == ItemsChoiceType2.RadMeasurement)
                        {
                            radMeasurementType = radInstrumentDataType.Items[i] as RadMeasurementType;
                            // Check if correct node
                            if (radMeasurementType.MeasurementClassCode == MeasurementClassCodeSimpleType.Foreground)
                                radMeasurementFound = true;
                        }
                    }

                    if (analysisFound && analysisFound)
                        break;
                }

                if (radMeasurementFound == false)
                    throw new Exception("'radMeasurementFound' not found");

                // Get StartTime
                deviceData.StartDateTime = radMeasurementType.StartDateTime_DateTime;

                // Get MeasureTime
                string value = radMeasurementType.RealTimeDuration.Remove(0, 2);
                value = value.Remove(value.Length - 1, 1);
                deviceData.MeasureTime = new TimeSpan(0, 0, (int)Math.Round(double.Parse(value)));

                // Get CountRate
                var grossCounts = radMeasurementType.GrossCounts.SingleOrDefault(x => x.id == "ForegroundMeasurement-MCA-GrossCounts");
                if (grossCounts == null)
                    throw new Exception("'GrossCounts' not found");
                deviceData.CountRate = Convert.ToDouble(grossCounts.CountData);

                // Get Identified Nuclides
                if (analysisFound == false)
                    return true;
                var nuclides = analysisResultsType.NuclideAnalysisResults.Nuclide;
                if (nuclides == null)
                    return true;
                for (int i = 0; i < nuclides.Count(); i += 1)
                {
                    var elementNames = nuclides[i].ItemsElementName;
                    for (int c = 0; c < elementNames.Length; c += 1)
                    {
                        if (elementNames[c] == ItemsChoiceType.NuclideIDConfidenceValue)
                        {
                            deviceData.Nuclides[i] = new NuclideID(nuclides[i].NuclideName.Replace('_', '-'), (double)nuclides[i].Items[c]);
                            break;
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

                var sanitizedFileText = RemoveXmlNameSpaces(filePath, "TFS");
                using (TextReader reader = new StringReader(sanitizedFileText))
                {
                    radInstrumentDataType = serializer.Deserialize(reader) as RadInstrumentDataType;
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

        private string RemoveXmlNameSpaces(string filePath, string ns)
        {
            var sanitizedFileText = string.Empty;
            
            using (TextReader reader = File.OpenText(filePath))
            {
                var splitFile = reader.ReadToEnd()
                    .Split(Globals.Delim_Newline, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.TrimStart())
                    .ToList();

                // Remove xmlns attributes
                // Also remove invalid attribute
                splitFile = splitFile.Select(line => SteralizeLine(line))
                .ToList();

                // Remove namespaced elements
                splitFile = splitFile.Where(line => !(line.StartsWith($"<{ns}:"))).ToList();

                return splitFile.Aggregate((combined, next) => combined + next);
            }
        }

        private static string SteralizeLine(string line)
        {
            if (line.StartsWith("<?xml-model"))
                line = line.Remove(line.IndexOf("<?xml-model"));
            else if (line.StartsWith("<RadInstrumentData"))
                line = line.Remove(line.IndexOf("xmlns:xsi")) + ">";
            else if (line.Contains("__FALLBACK_"))
                line = line.Replace("__FALLBACK_TELEMETRY__", "true");
            return line;
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
