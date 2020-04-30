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
    public class OrtecDetectiveRemoteN42Parser : FileParser
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
        private List<String> deviceNuclideLibrary;
        private List<String> foundNuclides;

        private IEnumerable<string> filePaths;


        public override string FileName { get { return "OrtecDetectiveRemote_N42"; } }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public OrtecDetectiveRemoteN42Parser()
        {
            fileErrors = new List<KeyValuePair<string, string>>();
            deviceNuclideLibrary = new List<string> {"Be-7", "Na-22", "Na-24", "Cl-38", "Ar-41", "K-40", "K-42", "Sc-46",
                "Cr-51", "Mn-54", "Fe-59", "Co-56", "Co-57", "Co-58", "Co-60", "Cu-64", "Zn-65", "As-76", "Se-75", "Br-82", "Kr-85",
                "Kr-88", "Kr-89", "Rb-86", "Rb-89", "Sr-91", "Y-88", "Y-91", "Zr-95", "Nb-94", "Nb-95", "Mo-99", "Ru-103", "Rh-106",
                "Ag-108", "Ag-110M", "Cd-109", "Sn-113", "Sb-122", "Sb-124", "Sb-125", "Sb-126", "Te-131", "Te-132", "J-131", "J-132",
                "J-133", "J-134", "J-135", "Xe-138", "Xe-131M", "Xe-133M", "Cs-134", "Cs-136", "Cs-137", "Cs-138", "Ba-133", "Ba-139",
                "Ba-140", "La-140", "Ce-139", "Ce-141", "Ce-143", "Ce-144", "Pr-144", "Nd-147", "Eu-152", "Eu-154", "Eu-155", "Gd-153",
                "Tb-160", "Yb-175", "Lu-177", "Hf-181", "Ta-182", "W-187", "Au-196", "Au-198", "Hg-203", "Tl-208", "Pb-210", "Pb-212",
                "Pb-214", "Bi-207", "Bi-212", "Bi-214", "Ra-226", "Ac-228", "Th-227", "Th-234", "Pa-234M", "U-235", "U-237", "Np-237",
                "Np-239", "Pu-238", "Pu-239", "Pu-240", "Am-241" };
            foundNuclides = new List<string>();
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public override IEnumerable<string> GetAllFilePaths(string directoryPath)
        {
            folderFilePath = directoryPath;
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

                // Create OrtecDetg and set FileName
                deviceData = new DeviceData(DeviceInfo.Type.OrtecDetectiveRemote);
                deviceData.FileName = fileName;

                // Parse data from N42 object
                if (ParseN42File(filePath) == false)
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

        private bool ParseN42File(string filePath)
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
                string deviceType = "";
                foreach (XmlNode node in element.ChildNodes)
                {
                    // Get DeviceType
                    if (node.Name.EndsWith("Manufacturer"))
                        deviceType = node.InnerText + " ";
                    else if (node.Name.EndsWith("Model"))
                        deviceType += node.InnerText;
                    // Get SerialNumber
                    else if (node.Name.EndsWith("ID"))
                        deviceData.SerialNumber = node.InnerText.Split(' ').LastOrDefault();
                }
                deviceData.DeviceType = deviceType;

                // Get MeaureTime
                element = measurement.Any.Where(x => x.Name == "Spectrum").FirstOrDefault();
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
                            break;
                        }
                        else if (mIndex != -1 && sIndex != -1)
                        {
                            deviceData.MeasureTime = deviceData.MeasureTime.Add(new TimeSpan(0,
                                int.Parse(time.Substring(0, mIndex)),
                                int.Parse(time.Substring(mIndex + 1, sIndex - mIndex - 1))));
                            break;
                        }
                        else if (mIndex != -1)
                        {
                            deviceData.MeasureTime = deviceData.MeasureTime.Add(new TimeSpan(0, int.Parse(time.Substring(0, mIndex)), 0));
                            break;
                        }
                        else if (periodIndex != -1 && sIndex != -1)
                        {
                            deviceData.MeasureTime = deviceData.MeasureTime.Add(new TimeSpan(0, 0, 0,
                                int.Parse(time.Substring(0, periodIndex)),
                                int.Parse(time.Substring(periodIndex + 1, periodIndex + 6))));
                            break;
                        }
                        else if (sIndex != -1)
                        {
                            deviceData.MeasureTime = deviceData.MeasureTime.Add(new TimeSpan(0, 0, int.Parse(time.Substring(0, sIndex))));
                            break;
                        }
                        break;
                    }
                }

                // Get StartDateTime
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
                foundNuclides = new List<string>();
                foreach (string nuclide in deviceNuclideLibrary)
                {
                    if (filePath.Contains(nuclide))
                    {
                        foundNuclides.Add(nuclide);
                    }
                }
                //This device does not output a confidence value so zero is put in as a substitute
                for (int i = 0; i < foundNuclides.Count && i < deviceData.Nuclides.Count; i += 1)
                { 
                //while (i <= 5 && i < foundNuclides.Count) {
                    deviceData.Nuclides[i] = new NuclideID(foundNuclides[i], 0);
                    i += 1;
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


        //deletes folder where copies of files are held
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

        //copies, edits, and saves current file in a new directory
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

        ///removes tags and attributes that are a hindrance to parsing
        private string EditCopyFile(string copiedFilePath)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(copiedFilePath);
            xmlDoc.DocumentElement.RemoveAllAttributes();
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
