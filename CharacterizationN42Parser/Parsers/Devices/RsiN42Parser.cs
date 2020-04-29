using SPEAR.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace SPEAR.Parsers.Devices
{
    public class RsiN42Parser : FileParser
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public bool ErrorsOccurred;
        private List<KeyValuePair<string, string>> fileErrors;
        
        // broken N42 files cannot be deserialized normally
        private List<KeyValuePair<string, string>> N42List;
        private DeviceData deviceData;
        private List<DeviceData> deviceDatasParsed;

        private IEnumerable<string> filePaths;
       
        private string dateFormat = "yyyy-MM-dd HH:mm:ss";
        
        public override string FileName { get { return "RsiSr10_N42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public RsiN42Parser()
        {
            fileErrors = new List<KeyValuePair<string, string>>();
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            return Directory.GetFiles(directoryPath, "*.N42");
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
            N42List = new List<KeyValuePair<string, string>>();
            filePaths = null;
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Private Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        private void ParseFiles()
        {
            //SortedList<DateTime, DeviceInfo> rsis = new SortedList<DateTime, DeviceInfo>();
            var rsis = new List<KeyValuePair<DateTime, DeviceData>>();

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
                if (SplitFile(filePath) == false)
                    continue;

                // Create RadSeeker and set FileName
                deviceData = new DeviceData(DeviceInfo.Type.RSI);
                deviceData.FileName = Path.GetFileName(filePath);

                // Parse data from N42 object
                if (ParseN42File() == false)
                    continue;

                // Add to other parsed
                rsis.Add(new KeyValuePair<DateTime, DeviceData>(deviceData.StartDateTime, deviceData));
            }

            // Number all the events
            int trailNumber = 1;
            foreach (KeyValuePair<DateTime, DeviceData> tempDevice in rsis)
            {
                tempDevice.Value.TrialNumber = trailNumber++;
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

            deviceDatasParsed = (from rsi in rsis select rsi.Value).ToList();
        }

        private void Clear()
        {
            N42List = new List<KeyValuePair<string, string>>();
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
            if (N42List == null || N42List.Count == 0 || deviceData == null)
                return false;

            try
            {
                string value = null;
                List<string> nuclideNames = new List<string>() { };
                List<string> nuclideConfidences = new List<string>() { };
                int nucCount = 0;
                bool needMeasureTime = true;


                foreach (KeyValuePair<string, string> pair in N42List)
                {
                    // Get StartDateTime from parse
                    if (pair.Key.Contains("StartTime"))
                    {
                        value = pair.Value.Replace("T", " ");
                        if (DateTime.TryParseExact(value, dateFormat, Globals.CultureInfo, DateTimeStyles.None, out DateTime dateTime))
                            deviceData.StartDateTime = dateTime;
                        else
                            return false;
                        continue;
                    }

                    // Get DeviceType
                    if (pair.Key.Contains("InstrumentType"))
                    {
                        deviceData.DeviceType = pair.Value;
                        continue;
                    }

                    // Get SerialNumber
                    if (pair.Key.Contains("InstrumentID"))
                    {
                        deviceData.SerialNumber = pair.Value.TrimEnd();
                        continue;
                    }

                    // Get Identified Nuclides
                    if (pair.Key.Contains("NuclideName"))
                    {
                        nuclideNames.Add(pair.Value);
                        continue;
                    }

                    if (pair.Key.Contains("NuclideIDConfidenceIndication"))
                    {
                        nuclideConfidences.Add(pair.Value);
                        nucCount++;
                        continue;
                    }


                    // Get MeasureTime
                    if (needMeasureTime && pair.Key.Contains("SampleRealTime"))
                    {
                        // Format is PTsss.fffS
                        value = pair.Value.Remove(0, 2);
                        value = value.Remove(value.Length - 1, 1).Split('.').FirstOrDefault();
                        deviceData.MeasureTime = new TimeSpan(0, 0, int.Parse(value));
                        needMeasureTime = false;
                        continue;
                    }
                }

                for (int i = 0; i < nucCount; i++)
                {
                    deviceData.Nuclides[i] = new NuclideID(nuclideNames[i], double.Parse(nuclideConfidences[i]) / 100);
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

        private bool SplitFile(string filePath)
        {
            string[] nuclideDelim = new string[] { ">\r\n     <", ">\r\n               <", ">\r\n       <", ">\r\n     <", ">\r\n  <" };
            try
            {
                string[] splitResult;
                using (TextReader stream = File.OpenText(filePath))
                {
                    splitResult = stream.ReadToEnd().Split(nuclideDelim, StringSplitOptions.RemoveEmptyEntries);
                }

                foreach (string split in splitResult)
                {
                    string[] splitSplitResult = split.Split(Globals.Delim_RightArrow, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (splitSplitResult.Length < 2)
                        continue;
                    string[] splitSplitResult2 = splitSplitResult[1].Split(Globals.Delim_LeftArrow, 2, StringSplitOptions.RemoveEmptyEntries);
                    N42List.Add(new KeyValuePair<string, string>(splitSplitResult[0], splitSplitResult2[0]));
                }

                return true;
            }
            catch (Exception ex)
            {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(filePath), "Error splitting .spe file: " + ex.Message));
                ErrorsOccurred = true;
                return false;
            }
        }
    }
}
