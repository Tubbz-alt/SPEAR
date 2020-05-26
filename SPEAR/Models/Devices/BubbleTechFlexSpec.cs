using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    class BubbleTechFlexSpec : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public BubbleTechFlexSpec()
        {
            // Set defaults
            DeviceTypeEnum = Type.KromekD3SDhs;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new BubbleTechFlexSpecParser() }
            };
        }
    }
}
