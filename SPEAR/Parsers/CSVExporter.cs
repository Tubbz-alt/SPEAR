using SPEAR.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace SPEAR.Parsers
{
    public static class CSVExporter
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Public Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public static void ExportFiles(IEnumerable<DeviceData> deviceDatas, string saveFilePath)
        {
            if (deviceDatas.Count() == 0)
                return; 

            StringBuilder stringBuilder = new StringBuilder();

            // Create column names
            stringBuilder.Append("Trial Number,");
            stringBuilder.Append("File Name,");
            stringBuilder.Append("Date,");
            stringBuilder.Append("Start Time,");
            stringBuilder.Append("Measure Time,");
            stringBuilder.Append("Count Rate,");
            stringBuilder.Append("ID1,");
            stringBuilder.Append("Con1,");
            stringBuilder.Append("ID2,");
            stringBuilder.Append("Con2,");
            stringBuilder.Append("ID3,");
            stringBuilder.Append("Con3,");
            stringBuilder.Append("ID4,");
            stringBuilder.Append("Con4,");
            stringBuilder.Append("ID5,");
            stringBuilder.Append("Con5,");
            stringBuilder.Append("ID6,");
            stringBuilder.AppendLine("Con6,");

            // Add a row for each RadSeeker
            foreach (DeviceData deviceData in deviceDatas) {
                StringBuilder rowBuilder = new StringBuilder();

                // Append TrialNumber
                rowBuilder.AppendFormat("{0},", deviceData.TrialNumber);
                
                // Append FileName
                rowBuilder.AppendFormat("{0},", deviceData.FileName);
                
                // Append Date
                rowBuilder.AppendFormat("{0:yyyy-MM-dd},", deviceData.StartDateTime);

                // Append Time
                rowBuilder.AppendFormat("{0:HH:mm:ss},", deviceData.StartDateTime);

                // Measure Time
                rowBuilder.Append((deviceData.MeasureTime == TimeSpan.MinValue) ? "," : string.Format("{0:c},", deviceData.MeasureTime));

                // Count Rate
                rowBuilder.Append((deviceData.CountRate == 0.0) ? "," : string.Format("{0:0},", deviceData.CountRate));

                // Add Existing Nuclide IDs
                int i = 0;
                while (deviceData.Nuclides[i].Confidence != -1) {
                    rowBuilder.AppendFormat("{0},{1},", deviceData.Nuclides[i].NuclideName, deviceData.Nuclides[i].Confidence);
                    i += 1;
                }

                // Add Remaaining Empty Nuclide IDs
                while (i < 6) {
                    rowBuilder.Append(",,");
                    i += 1;
                }

                // Add Row to main builder
                stringBuilder.AppendLine(rowBuilder.ToString());
            }
            
            try {
                // Write CSV text to file
                if (File.Exists(saveFilePath))
                    File.Delete(saveFilePath);
                File.WriteAllText(saveFilePath, stringBuilder.ToString());

                // Change name of zip file
                string oldName = Path.ChangeExtension(MainWindow.ArchiveName, ".zip");
                string newName = Path.ChangeExtension(saveFilePath, ".zip");
                if (File.Exists(newName))
                    File.Delete(newName);
                File.Move(oldName, newName);
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "File Error");
            }
        }
    }
}
