using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    public class OrtecDetectiveRemote: DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public OrtecDetectiveRemote()
        {
            // Set defaults
            DeviceTypeEnum = Type.OrtecDetectiveRemote;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.xml", FileExtType = FileExt.Type.N42, FileParser = new OrtecDetectiveRemoteN42Parser() }
            };
        }
    }
}
