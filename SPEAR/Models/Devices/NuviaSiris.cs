using SPEAR.Parsers.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models.Devices
{
    public class NuviaSiris : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public NuviaSiris() 
        {
            // Set defaults
            DeviceTypeEnum = Type.NuviaSiris;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.xml", FileExtType = FileExt.Type.N42, FileParser = new NuviaSirisN42Parser() }
            };
        }
    }
}
