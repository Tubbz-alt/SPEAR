using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    public class Rs350 : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public Rs350()
        {
            // Set defaults
            DeviceTypeEnum = Type.Rs350;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new Rs350N42Parser() }
            };
        }
    }
}
