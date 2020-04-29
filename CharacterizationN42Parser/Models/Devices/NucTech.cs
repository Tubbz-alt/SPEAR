using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class NucTech : DeviceInfo
    {        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public NucTech()
        {
            // Set defaults
            DeviceTypeEnum = Type.NucTech;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.spe", FileExtType = FileExt.Type.SPE, FileParser = new NucTechSpeParser() },
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new NucTechN42Parser() }
            };
        }
    }
}
