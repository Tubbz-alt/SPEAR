using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class RSI : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public RSI()
        {
            // Set defaults
            DeviceTypeEnum = Type.RSI;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.N42", FileExtType = FileExt.Type.N42, FileParser = new RsiN42Parser() }
            };
        }
    }
}
