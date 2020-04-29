using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    // Ortec
    public class RadEagle : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public RadEagle()
        {
            // Set defaults
            DeviceTypeEnum = Type.RadEagle;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.spe", FileExtType = FileExt.Type.SPE, FileParser = new RadEagleSpeParser() }
            };
        }
    }
}
