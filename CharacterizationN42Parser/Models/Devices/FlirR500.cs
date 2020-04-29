using System;
using System.Collections.Generic;
using SPEAR.Parsers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class FlirR500 : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public FlirR500()
        {
            // Set defaults
            DeviceTypeEnum = Type.FlirR500;
            
            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new FlirR500N42Parser() }
            };
        }
    }
}
