using SPEAR.Parsers.Devices;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    public class MirionSpirdentMobile : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public MirionSpirdentMobile()
        {
            //Set defaults
            DeviceTypeEnum = Type.MirionSpiridentMobile;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new MirionSpiridentMobileN42Parser() }
            };
        }
    }
}
