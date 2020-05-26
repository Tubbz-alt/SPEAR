using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    public class KromekD3SNsdd : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public KromekD3SNsdd()
        {
            // Set defaults
            DeviceTypeEnum = Type.KromekD3SNsdd;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.xml", FileExtType = FileExt.Type.N42, FileParser = new KromekN42NsddParser() }
            };
        }
    }
}
