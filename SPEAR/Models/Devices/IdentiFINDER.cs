using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class IdentiFINDER : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public IdentiFINDER()
        {
            // Set defaults
            DeviceTypeEnum = Type.identiFINDER;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.Measurement.spe", FileExtType = FileExt.Type.SPE, FileParser = new IdentiFINDERSpeParser() }
            };
        }
    }
}
