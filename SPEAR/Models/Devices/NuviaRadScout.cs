using SPEAR.Parsers.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models.Devices
{
    public class NuviaRadScout : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public NuviaRadScout()
        {
            // Set defaults
            DeviceTypeEnum = Type.NuviaRadScout;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.xml", FileExtType = FileExt.Type.N42, FileParser = new NuviaRadScoutN42Parser() }
            };
        }
    }
}
