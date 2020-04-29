using SPEAR.Parsers;
using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    class AtomTex : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public AtomTex()
        {
            // Set defaults
            DeviceTypeEnum = Type.AtomTex;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.N42", FileExtType = FileExt.Type.N42, FileParser = new AtomTexN42Parser() },
                new FileExt() { FileExtName = "*.spe", FileExtType = FileExt.Type.SPE, FileParser = new AtomTexSpeParser() }
            };
        }
    }
}
