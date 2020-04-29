using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class Rs700: DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public Rs700()
        {
            // Set defaults
            DeviceTypeEnum = Type.Rs700;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new Rs700N42Parser() }
            };
        }
    }
}
