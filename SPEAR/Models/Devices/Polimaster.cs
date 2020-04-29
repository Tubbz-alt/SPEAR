using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class Polimaster : DeviceInfo
    {        
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public Polimaster()
        {
            // Set defaults
            DeviceTypeEnum = Type.Polimaster;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.spe", FileExtType = FileExt.Type.SPE, FileParser = new PolimasterSpeParser() }
            };
        }
    }
}
