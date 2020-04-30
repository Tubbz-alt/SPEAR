using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class BNCSam : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public BNCSam()
        {
            // Set defaults
            DeviceTypeEnum = Type.BNCSam;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "EventDB.sql", FileExtType = FileExt.Type.SQL, FileParser = new BNCSamSqlParser() }
            };
        }
    }
}
