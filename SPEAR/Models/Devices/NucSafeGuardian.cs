using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class NucSafeGuardian : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public NucSafeGuardian()
        {
            // Set defaults
            DeviceTypeEnum = Type.OrtecDetectiveRemote;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.n42", FileExtType = FileExt.Type.N42, FileParser = new NucSafeGuardianN42Parser() }
            };
        }
    }
}
