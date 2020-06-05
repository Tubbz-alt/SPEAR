using SPEAR.Parsers.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models.Devices
{
    public class RapiscanMp100Gns : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public RapiscanMp100Gns()
        {
            // Set defaults
            DeviceTypeEnum = Type.RapiscanMp100Gns;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.xml", FileExtType = FileExt.Type.N42, FileParser = new RapiscanMp100GnsN42Parser() }
            };
        }
    }
}
