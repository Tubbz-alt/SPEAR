using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    public class AtomTexAT6103 : DeviceInfo
    {
        public AtomTexAT6103()
        {
            // Set defaults
            DeviceTypeEnum = Type.AtomTexAT6103;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.spe", FileExtType = FileExt.Type.SPE, FileParser = new AtomTexAT6103SpeParser() }
            };
        }
    }
}
