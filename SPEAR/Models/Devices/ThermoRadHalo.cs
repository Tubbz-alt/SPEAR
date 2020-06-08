using SPEAR.Parsers.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models.Devices
{
    class ThermoRadHalo : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public ThermoRadHalo()
        {
            // Set defaults
            DeviceTypeEnum = Type.ThermoRadHalo;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new ThermoRadHaloN42Parser() }
            };
        }
    }
}
