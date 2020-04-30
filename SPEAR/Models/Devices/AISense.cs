using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    public class AISense : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public AISense()
        {
            // Set defaults
            DeviceTypeEnum = Type.AISense;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.ID", FileExtType = FileExt.Type.ID, FileParser = new AISenseIDParser() }
            };
        }
    }
}
