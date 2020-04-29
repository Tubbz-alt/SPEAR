using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class RadEyeSPRD : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public RadEyeSPRD()
        {
            // Set defaults
            DeviceTypeEnum = Type.RadEyeSPRD;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.N42", FileExtType = FileExt.Type.N42, FileParser = new RadEyeSprdN42Parser() }
            };
        }
    }
}
