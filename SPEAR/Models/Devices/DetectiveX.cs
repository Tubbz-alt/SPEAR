using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    // Ortec
    public class DetectiveX : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public DetectiveX()
        {
            // Set defaults
            DeviceTypeEnum = Type.DetectiveX;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new DetectiveXN42Parser() }
            };
        }
    }
}
