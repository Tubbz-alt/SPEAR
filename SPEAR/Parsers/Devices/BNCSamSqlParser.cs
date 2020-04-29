using SPEAR.Models;
using SPEAR.Models.Devices;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SPEAR.Parsers.Devices
{
    public class BNCSamSqlParser : FileParser
    {        
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public bool ErrorsOccurred;
        private List<KeyValuePair<string, string>> fileErrors;
        
        private List<DeviceData> deviceDatasParsed;

        private IEnumerable<string> sqlFilePaths;
        
        public override string FileName { get { return "BncSam950_SQL"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public BNCSamSqlParser()
        {
            fileErrors = new List<KeyValuePair<string, string>>();
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            return Directory.GetFiles(directoryPath, "EventDB.sql");
        }

        public override void InitializeFilePaths(IEnumerable<string> allFilePaths)
        {
            sqlFilePaths = allFilePaths;
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
        }
        

        /////////////////////////////////////////////////////////////////////////////////////////
        // Private Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        private void ParseFiles()
        {
            string filePath = sqlFilePaths.FirstOrDefault();
            string fileName = Path.GetFileName(filePath);
            SortedList<DateTime, DeviceData> bncSams = new SortedList<DateTime, DeviceData>();

            // Start Thread that archives .N42 files
            ThreadStart threadStart = new ThreadStart(ArchiveFiles);
            Thread thread = new Thread(threadStart);
            thread.Start();

            Invoke_ParsingUpdate(0.15f);

            try
            {
                if (filePath == string.Empty || filePath == null) {
                    MessageBox.Show("Invalid or missing EventDB.sql file", "File Error");
                    return;
                }

                // Create onnection
                string connectionString = string.Format("Data Source={0};", filePath);
                using (SQLiteConnection connection = new SQLiteConnection(connectionString))
                {
                    // Create SQL command
                    string queryString = "SELECT Instrument_Model, Date, begin, Real_AcqTime, Avg_Neutron, Person_in_charge, Identification from Event";
                    SQLiteCommand command = new SQLiteCommand(queryString, connection);

                    // Open connection
                    connection.Open();

                    Invoke_ParsingUpdate(0.30f);

                    // Execute SQL command and read values
                    SQLiteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        DeviceData deviceData = ParseBncSamID(reader);
                        if (deviceData == null)
                            continue;
                        deviceData.FileName = fileName;
                        bncSams.Add(deviceData.StartDateTime, deviceData);
                    }
                    reader.Close();

                    Invoke_ParsingUpdate(0.60f);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Parsing Error");
                return;
            }

            // Number all the events
            int trailNumber = 1;
            foreach (KeyValuePair<DateTime, DeviceData> tempDevice in bncSams) {
                tempDevice.Value.TrialNumber = trailNumber++;
            }
            Invoke_ParsingUpdate(0.70f);

            if (ErrorsOccurred)
            {
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

            Invoke_ParsingUpdate(0.85f);

            // Wait for thread to zip files
            thread.Join();

            Invoke_ParsingUpdate(0.99f);

            deviceDatasParsed = bncSams.Values.ToList();
        }

        private void ClearErrors()
        {
            ErrorsOccurred = false;
            fileErrors = new List<KeyValuePair<string, string>>();
        }

        private void ArchiveFiles()
        {
            if (sqlFilePaths.Count() == 0)
                return;

            // Get base directory
            string baseDirectory = Path.GetDirectoryName(sqlFilePaths.First());

            // Create temp directory
            if (Directory.Exists(MainWindow.ArchiveName))
                Directory.Delete(MainWindow.ArchiveName, true);
            Directory.CreateDirectory(MainWindow.ArchiveName);

            // Copy files to temp archive
            foreach (string filePath in sqlFilePaths)
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

        // reader[0] = device type
        // reader[1] = date
        // reader[2] = begin time
        // reader[3] = SN (not really just "Person_in_charge" field)
        // reader[4] = identifications delimited by '|' and subset info by ';'
        private DeviceData ParseBncSamID(SQLiteDataReader reader)
        {
            try {
                DeviceData deviceData = new DeviceData(DeviceInfo.Type.BNCSam);

                // Get DeviceType
                deviceData.DeviceType = reader[0] as string;

                // Get Date
                if (DateTime.TryParse((string)reader[1], out DateTime dateTime) == false)
                    return null;

                // Get StartTime
                TimeSpan startTime;
                if (TimeSpan.TryParse((string)reader[2], out startTime) == true)
                    dateTime = dateTime.Add(startTime);
                else
                    return null;
                deviceData.StartDateTime = dateTime;

                // Get RealTime
                double realTime = (double)reader[3];
                deviceData.MeasureTime = new TimeSpan(0, 0, 0, 0, (int)(realTime * 1000));

                // Get Count Rate
                deviceData.CountRate = double.Parse((reader[4] as string).Split(' ')[0]);
                
                // Get SerialNumber
                deviceData.SerialNumber = reader[5] as string;

                // Get identifications
                string value = reader[6] as string;
                if (value == null || value == string.Empty)
                    return deviceData;
                string[] splitResult = value.Split(Globals.Delim_Bar, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < splitResult.Length; i += 1)
                {
                    string[] splitSplitResults = splitResult[i].Split(Globals.Delim_SemiColon, StringSplitOptions.RemoveEmptyEntries);
                    string nuclideName = splitSplitResults[0];
                    string confidence = splitSplitResults[1];
                    deviceData.Nuclides[i] = new NuclideID(nuclideName, double.Parse(confidence.Remove(confidence.Length - 1)) / 100.0);
                }

                return deviceData;
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
                return null;
            }
        }
    }
}
