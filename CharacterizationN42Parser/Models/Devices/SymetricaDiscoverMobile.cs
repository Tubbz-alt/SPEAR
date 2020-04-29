using SPEAR.Parsers.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models.Devices
{
    public class SymetricaDiscoverMobile : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public SymetricaDiscoverMobile()
        {
            // Set defaults
            DeviceTypeEnum = Type.SymetricaDiscoverMobile;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new SymetricaDiscoverMobileN42Parser() }
            };
        }
    }
}
