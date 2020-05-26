using SPEAR.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace SPEAR.Parsers.Devices
{
    public class AtomTexAT6103SpeParser : FileParser
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

        public override string FileName { get { return "AtomTexAT6103_SPE"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public AtomTexAT6103SpeParser()
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
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Private Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        private void ParseFiles()
        {
            SortedList<DateTime, DeviceData> atomTexs = new SortedList<DateTime, DeviceData>();

            // Start Thread that archives files
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

                // Split the spe file properties
                if (SplitFile(filePath) == false)
                    continue;

                // Create DeviceData and set file name
                deviceData = new DeviceData(DeviceInfo.Type.AtomTex);
                deviceData.FileName = Path.GetFileName(filePath);

                // Parse spe file
                if (ParseSpeFile() == false)
                    continue;

                // Add to other parsed
                atomTexs.Add(deviceData.StartDateTime, deviceData);
            }

            // Number all the events
            int trailNumber = 1;
            foreach (KeyValuePair<DateTime, DeviceData> tempDevice in atomTexs)
                tempDevice.Value.TrialNumber = trailNumber++;

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

            deviceDatasParsed = atomTexs.Values.ToList();
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
                deviceData.DeviceType = "AtomTex AT6103";

                // Get SerialNumber
                deviceData.SerialNumber = splitResult[0].Remove(0, 4).TrimEnd();

                // Get StartDateTime
                value = speDictionary["DATE_MEA"].Trim();
                DateTime dateTime;
                if (DateTime.TryParse(value, out dateTime) == true)
                    deviceData.StartDateTime = dateTime;

                // Get Measure Time
                value = speDictionary["MEAS_TIM"].Trim().Split(' ').FirstOrDefault();
                if (Int32.TryParse(value, out int seconds) == true)
                    deviceData.MeasureTime = new TimeSpan(0, 0, seconds);

                // Get Count Rate
                value = speDictionary["CPS"].Trim();
                if (Double.TryParse(value, out double cps) == true)
                    deviceData.CountRate = cps;

                // Get Identified Nuclides
                value = speDictionary["RADIONUCLIDES"].Trim();
                splitResult = value.Split(Globals.Delim_SemiColon, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < splitResult.Length; i += 1)
                {
                    string[] splitSplitResult = splitResult[i].Split(Globals.Delim_LeftSquareBracket, StringSplitOptions.RemoveEmptyEntries);
                    if (splitSplitResult.Length != 2)
                        continue;
                    string nuclide = splitSplitResult[0].Replace("I-", string.Empty);
                    double confidence = double.Parse(splitSplitResult[1].Substring(0, splitSplitResult[1].Length - 1));
                    deviceData.Nuclides[i] = new NuclideID(nuclide, confidence);
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
            try
            {
                string[] splitResult;
                using (TextReader stream = File.OpenText(filePath))
                {
                    splitResult = stream.ReadToEnd().Split(Globals.Delim_Dollar, StringSplitOptions.RemoveEmptyEntries);
                }

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
