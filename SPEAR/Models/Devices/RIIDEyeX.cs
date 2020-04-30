using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    public class RIIDEyeX : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public RIIDEyeX()
        {
            // Set defaults
            DeviceTypeEnum = Type.RIIDEyeX;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.N42", FileExtType = FileExt.Type.N42, FileParser = new RIIDEyeN42Parser() }
            };
        }
    }
}
