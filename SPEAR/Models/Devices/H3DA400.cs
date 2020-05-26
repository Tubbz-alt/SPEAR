using SPEAR.Parsers.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models.Devices
{
    public class H3DA400 : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public H3DA400()
        {
            // Set defaults
            DeviceTypeEnum = Type.H3DA400;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new H3DA400Parser() }
            };
        }
    }
}
