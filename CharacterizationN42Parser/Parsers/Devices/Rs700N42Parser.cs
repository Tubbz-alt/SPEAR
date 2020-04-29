using SPEAR.Models;
using SPEAR.Models.N42.v2006;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace SPEAR.Parsers.Devices
{
    public class Rs700N42Parser : FileParser
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public bool ErrorsOccurred;
        private string folderFilePath;
        private string copyFolderFilePath;
        private string modifiedFileFilePath;
        private List<KeyValuePair<string, string>> fileErrors;

        private N42InstrumentData n42InstrumentData;
        private DeviceData deviceData;
        private List<DeviceData> deviceDatasParsed;

        private IEnumerable<string> filePaths;


        public override string FileName { get { return "Rs700_N42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public Rs700N42Parser()
        {
            //ErrorsOccurred = false;
            fileErrors = new List<KeyValuePair<string, string>>();
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            folderFilePath = directoryPath;
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
            n42InstrumentData = null;
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

            // Parse n42 files
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

                // Create Rs700 and set FileName
                deviceData = new DeviceData(DeviceInfo.Type.Rs700);
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
            n42InstrumentData = null;
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
            if (n42InstrumentData == null || deviceData == null)
                return false;

            try
            {
                Measurement measurement = n42InstrumentData.Measurement.FirstOrDefault();
                if (measurement == null)
                    return false;

                var element = measurement.Any.Where(x => x.Name == "InstrumentInformation").FirstOrDefault();
                if (element == null)
                    return false;
                foreach (XmlNode node in element.ChildNodes)
                {
                    // Get DeviceType
                    if (node.Name.EndsWith("Model"))
                        deviceData.DeviceType = node.InnerText;
                    // Get SerialNumber
                    else if (node.Name.EndsWith("ID"))
                        deviceData.SerialNumber = node.InnerText.Split(' ').LastOrDefault();
                }

                // Get MeaureTime
                element = measurement.Any.Where(x => x.Name == "DetectorData").FirstOrDefault();
                if (element == null)
                    return false;
                foreach (XmlNode node in element.ChildNodes)
                {
                    if (node.Name.EndsWith("RealTime"))
                    {
                        string time = node.InnerText.Remove(0, 2);
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
                        break;
                    }
                }

                // Get StartDateTime
                element = measurement.Any.Where(x => x.Name == "DetectorData").FirstOrDefault();
                if (element == null)
                    return false;

                foreach (XmlNode node in element.ChildNodes)
                {
                    if (node.Name == "StartTime")
                    {
                        var attribute = node.InnerText;
                        if (DateTime.TryParse(attribute, out DateTime date))
                            deviceData.StartDateTime = date;
                        break;
                    }
                }

                // Get Identified Nuclides
                element = measurement.Any.Where(x => x.Name == "AnalysisResults").FirstOrDefault();
                if (element == null)
                    return true;
                foreach (XmlNode nuclideAnalsis in element.ChildNodes)
                {
                    if (nuclideAnalsis.Name == "NuclideAnalysis")
                    {
                        for (int i = 0; i < nuclideAnalsis.ChildNodes.Count; i += 1)
                        {
                            XmlNode nuclide = nuclideAnalsis.ChildNodes.Item(i);
                            string name = string.Empty;
                            double confidence = 0;
                            foreach (XmlNode item in nuclide.ChildNodes)
                            {
                                if (item.Name.EndsWith("NuclideName") == true)
                                {
                                    var indexOfBracket = item.InnerText.IndexOf('[');
                                    if (indexOfBracket > 0)
                                        name = item.InnerText.Substring(0, indexOfBracket);
                                    else
                                        name = item.InnerText;
                                }
                                else if (item.Name.EndsWith("NuclideIDConfidenceIndication") == true)
                                    confidence = double.Parse(item.InnerText);
                            }
                            deviceData.Nuclides[i] = new NuclideID(name, confidence);
                        }
                        break;
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
                modifiedFileFilePath = ModifyFile(filePath);

                serializer = new XmlSerializer(typeof(N42InstrumentData));
                serializer.UnknownElement += new XmlElementEventHandler(Serializer_UnknownElement);
                serializer.UnknownAttribute += new XmlAttributeEventHandler(Serializer_UnknownAttribute);

                using (TextReader stream = File.OpenText(modifiedFileFilePath))
                {
                    n42InstrumentData = serializer.Deserialize(stream) as N42InstrumentData;
                }

                DeleteFolder(copyFolderFilePath);
            }
            catch (Exception ex)
            {
                fileErrors.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(filePath), "Deserializing N42 file failed: " + ex.Message));
                ErrorsOccurred = true;
                return false;
            }

            if (n42InstrumentData == null)
                return false;

            return true;
        }
       
        // Deletes temporary folder where temporary copies of files are held
        private void DeleteFolder(string folderPath)
        {
            if (copyFolderFilePath != null)
            {
                if (Directory.Exists(folderPath))
                {
                    try
                    {
                        Directory.Delete(folderPath, true);
                    }
                    catch (System.IO.IOException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
        }

       // Copies, edits, and saves current file into a new folder
        private string ModifyFile(string singleFilePath)
        {
            string fileToEditPath = CopyFile(singleFilePath);
            return EditCopyFile(fileToEditPath);
        }

        private string CopyFile(string singleFilePath)
        {
            copyFolderFilePath = folderFilePath + "\\CopyFiles";
            Directory.CreateDirectory(copyFolderFilePath);
            File.Copy(singleFilePath, copyFolderFilePath + "\\" + Path.GetFileName(singleFilePath));
            return copyFolderFilePath + "\\" + Path.GetFileName(singleFilePath);
        }

        private string EditCopyFile(string copiedFilePath)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(copiedFilePath);
            xmlDoc.DocumentElement.RemoveAllAttributes();
            XmlNode elementNode = xmlDoc.GetElementsByTagName("rsin42o:RsiMeasurement")[0];
            xmlDoc.DocumentElement.RemoveChild(elementNode);
            string xmlStr = xmlDoc.OuterXml.Replace(" xmlns=\"http://physics.nist.gov/Divisions/Div846/Gp4/ANSIN4242/2005/ANSIN4242\"", "");
            File.WriteAllText(copiedFilePath, xmlStr);
            return copiedFilePath;
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

