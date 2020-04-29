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
    public class AISenseIDParser : FileParser
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public bool errorsOccurred;
        private List<KeyValuePair<string, string>> fileErrors;

        private Dictionary<string, string> fileDictionary;
        private DeviceData deviceData;
        private List<DeviceData> deviceDatasParsed;

        private IEnumerable<string> filePaths;

        private readonly string dateFormat = " yyyy-MM-dd HH:mm:ss";
        
        public override string FileName { get { return "AISense_ID"; } }

        public bool HaveErrorsOccurred { get { return errorsOccurred; } }
        public List<KeyValuePair<string, string>> FileErrors { get { return fileErrors; } }

        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public AISenseIDParser()
        {
            fileErrors = new List<KeyValuePair<string, string>>();
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            return Directory.GetFiles(directoryPath, "*.ID");
        }

        public override void InitializeFilePaths(IEnumerable<string> allFilePaths)
        {
            filePaths = allFilePaths;
            fileDictionary = new Dictionary<string, string>();
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
            filePaths = null;
            fileDictionary = new Dictionary<string, string>();
            deviceDatasParsed = new List<DeviceData>();
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Private Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        private void ParseFiles()
        {
            SortedList<DateTime, DeviceData> DeviceDatas = new SortedList<DateTime, DeviceData>();

            // Start Thread that archives .ID files
            ThreadStart threadStart = new ThreadStart(ArchiveFiles);
            Thread thread = new Thread(threadStart);
            thread.Start();

            // Parse ID files
            int filesCompleted = 0;
            foreach (string filePath in filePaths)
            {
                Invoke_ParsingUpdate((float)filesCompleted++ / (float)filePaths.Count());

                // Clear data
                Clear();

                // Deserialize file to ID object
                if (SplitFile(filePath) == false)
                    continue;

                // Create RadSeeker and set FileName
                deviceData = new DeviceData(DeviceInfo.Type.AISense);
                deviceData.FileName = Path.GetFileName(filePath);

                // Parse data from N42 object
                if (ParseIDFile() == false)
                    continue;

                // Add to other parsed
                DeviceDatas.Add(deviceData.StartDateTime, deviceData);
            }

            // Number all the events
            int trailNumber = 1;
            foreach (KeyValuePair<DateTime, DeviceData> tempDevice in DeviceDatas)
            {
                tempDevice.Value.TrialNumber = trailNumber++;
            }

            if (errorsOccurred)
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
                Invoke_ParsingError("Parsing Error", errorBuilder.ToString());
            }

            ClearErrors();

            // Wait for thread to zip files
            thread.Join();

            deviceDatasParsed = DeviceDatas.Values.ToList();
        }

        private void Clear()
        {
            fileDictionary = new Dictionary<string, string>();
            deviceData = null;
        }

        private void ClearErrors()
        {
            errorsOccurred = false;
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

        private bool ParseIDFile()
        {
            if (fileDictionary == null || fileDictionary.Count == 0 || deviceData == null)
                return false;

            try
            {
                string[] splitResults = null;
                string value = null;
                List<string> nuclideNames = new List<string>() { };
                List<string> nuclideConfidences = new List<string>() { };
                char[] dateDelims = new char[] { 'T' };
                double confidence = 0;
                DateTime dateTime;


                // Get StartDateTime from parse
                if (fileDictionary.ContainsKey("TIME") == true) {
                    value = fileDictionary["TIME"];
                    value = value.Replace("T", " ");
                    if (DateTime.TryParseExact(value, dateFormat, Globals.CultureInfo, DateTimeStyles.None, out dateTime))
                        deviceData.StartDateTime = dateTime;
                    else
                        return false;
                }

                // Get DeviceType
                if (fileDictionary.ContainsKey("TYPE") == true) {
                    value = fileDictionary["TYPE"];
                    deviceData.DeviceType = value.Substring(3);
                }

                // Get DoseRate
                if (fileDictionary.ContainsKey("COUNT RATE") == true) {
                    value = fileDictionary["COUNT RATE"];
                    splitResults = value.Split(Globals.Delim_Space, 2, StringSplitOptions.RemoveEmptyEntries);
                    deviceData.CountRate = double.Parse(splitResults[0]);
                }
                
                // Get SerialNumber
                if (fileDictionary.ContainsKey("BUILD NUMBER") == true) {
                    value = fileDictionary["BUILD NUMBER"];
                    deviceData.SerialNumber = value.Substring(1);
                }

                // Get confidence level
                if (fileDictionary.ContainsKey("TRUST LEVEL") == true) {
                    value = fileDictionary["TRUST LEVEL"];
                    value = value.TrimEnd('%');
                    confidence = double.Parse(value) / 100;
                }

                // Get Identified Nuclides
                if (fileDictionary.ContainsKey("NUCLIDES IDENTIFIED") == true) {
                    value = fileDictionary["NUCLIDES IDENTIFIED"];
                    splitResults = value.Split(Globals.Delim_Space, StringSplitOptions.RemoveEmptyEntries);
                    for(int i = 0; i < splitResults.Length; i += 1)
                        deviceData.Nuclides[i] = new NuclideID(splitResults[i], confidence);
                }

                // Get MeasureTime
                if (fileDictionary.ContainsKey("REALTIME") == true) {
                    value = fileDictionary["REALTIME"];
                    deviceData.MeasureTime = new TimeSpan(0, 0, 0, 0, int.Parse(value.Remove(value.Length - 3, 3)));
                }
                
                // Get MeasureTime
                if (fileDictionary.ContainsKey("COUNT RATE") == true) {
                    value = fileDictionary["COUNT RATE"];
                    deviceData.CountRate = Math.Round(double.Parse(value.Remove(value.Length - 4, 4)));
                }
            }
            catch (Exception ex)
            {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileName(deviceData.FileName), ex.Message));
                errorsOccurred = true;
                return false;
            }
            return true;
        }

        private bool SplitFile(string filePath)
        {
            try
            {
                string[] splitResult;
                using (TextReader stream = File.OpenText(filePath)) {
                    splitResult = stream.ReadToEnd().Split(Globals.Delim_Newline, StringSplitOptions.RemoveEmptyEntries);
                }
                
                for (int i = 0; i < splitResult.Length; i++) {
                    string[] splitSplitResult = splitResult[i].Split(Globals.Delim_Colon, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (splitSplitResult[0] == "NUCLIDES IDENTIFIED") {
                        // Grab each nuclide
                        StringBuilder builder = new StringBuilder();
                        while (splitResult[++i].StartsWith("- "))
                            builder.Append(splitResult[i].Remove(0, 1));
                        fileDictionary.Add("NUCLIDES IDENTIFIED", builder.ToString());
                        i -= 1;
                        continue;
                    }
                    else if (splitSplitResult.Length < 2)
                        continue;

                    fileDictionary.Add(splitSplitResult[0], splitSplitResult[1]);
                }

                return true;
            }
            catch (Exception ex)
            {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(filePath), "Error splitting .spe file: " + ex.Message));
                errorsOccurred = true;
                return false;
            }
        }

    }
}
