using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class RadSeeker : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public RadSeeker()
        {
            // Set defaults
            DeviceTypeEnum = Type.RadSeeker;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*_N42.n42", FileExtType = FileExt.Type.N42, FileParser = new RadSeekerN42N42Parser() },
                new FileExt() { FileExtName = "*_01.n42", FileExtType = FileExt.Type.N42, FileParser = new RadSeeker01N42Parser() }
            };
        }
    }
}
