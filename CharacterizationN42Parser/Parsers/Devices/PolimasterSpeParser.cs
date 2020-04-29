using SPEAR.Models;
using SPEAR.Models.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace SPEAR.Parsers.Devices
{
    public class PolimasterSpeParser : FileParser
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public bool ErrorsOccurred;
        private List<KeyValuePair<string, string>> fileErrors;
        
        private Dictionary<string, string> speDictionary;
        private DeviceData deviceData;
        private List<DeviceData> deviceDatasParsed;

        private IEnumerable<string> filePaths;

        private int creationSeconds = 0;
        public override string FileName { get { return "Polimaster_SPE"; } }

        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public PolimasterSpeParser()
        {
            fileErrors = new List<KeyValuePair<string, string>>();
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            return Directory.GetFiles(directoryPath, "*.spe");
        }

        public override void InitializeFilePaths(IEnumerable<string> allFilePaths)
        {
            filePaths = allFilePaths;
            speDictionary = new Dictionary<string, string>();
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
            speDictionary = new Dictionary<string, string>();
            filePaths = null;
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Private Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        private void ParseFiles()
        {
            SortedList<DateTime, DeviceData> polimasters = new SortedList<DateTime, DeviceData>();

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
                deviceData = new DeviceData(DeviceInfo.Type.Polimaster);
                deviceData.FileName = Path.GetFileName(filePath);

                // Parse data from N42 object
                if (ParseSpeFile() == false)
                    continue;

                // Add to other parsed
                polimasters.Add(deviceData.StartDateTime, deviceData);
            }

            // Number all the events
            int trailNumber = 1;
            foreach (KeyValuePair<DateTime, DeviceData> tempDevice in polimasters)
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

            deviceDatasParsed = polimasters.Values.ToList();
        }

        private void Clear()
        {
            speDictionary = new Dictionary<string, string>();
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

        private bool ParseSpeFile()
        {
            if (speDictionary == null || speDictionary.Count == 0 || deviceData == null)
                return false;

            try
            {
                string value = null;
                string[] splitResult = null;

                value = speDictionary["MCA_166_ID"].Trim();
                splitResult = value.Split(Globals.Delim_Newline, StringSplitOptions.RemoveEmptyEntries);
                if (splitResult.Length < 2)
                    return false;

                // Get DeviceType
                deviceData.DeviceType = splitResult[0].Trim();

                // Get SerialNumber
                deviceData.SerialNumber = splitResult[1].Trim();

                // Get StartDateTime
                value = speDictionary["DATE_MEA"].Trim();
                DateTime dateTime;
                if (DateTime.TryParse(value, out dateTime))
                    deviceData.StartDateTime = dateTime.AddSeconds(creationSeconds);
                else
                    return false;

                // Get CountRate
                value = speDictionary["COUNT_RATE"].Trim();
                deviceData.CountRate = double.Parse(value);

                // Get MeasureTime
                value = speDictionary["MEAS_TIM"].Trim().Split(' ').LastOrDefault();
                // get rid of decimal
                value = value.Split('.').FirstOrDefault();
                deviceData.MeasureTime = new TimeSpan(0, 0, int.Parse(value));

                // Get Identified Nuclides (no nuclides in .spe files)
                //value = speDictionary["FLIR_ANALYSIS_RESULTS"].Trim();
                //splitResult = value.Split('\n');
                //for (int i = 0; i < nucCount; i += 1)
                //{
                //    int position = (i * 4) + 1;
                //    polimaster.Nuclides[i] = new NuclideID(splitResult[position].TrimEnd(), double.Parse(splitResult[position + 3].TrimEnd()));
                //}
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
            try
            {
                // Read file and split
                string[] splitResult;
                using (TextReader stream = File.OpenText(filePath)) {
                    splitResult = stream.ReadToEnd().Split(Globals.Delim_Dollar, StringSplitOptions.RemoveEmptyEntries);
                }

                // Get seconds from file creation time (without this the SortedList will complain)
                creationSeconds = File.GetLastWriteTime(filePath).Second;

                foreach (string split in splitResult)
                {
                    string[] splitSplitResult = split.Split(Globals.Delim_Colon, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (splitSplitResult.Length < 2)
                        continue;
                    speDictionary.Add(splitSplitResult[0], splitSplitResult[1]);
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
