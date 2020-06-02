using SPEAR.Parsers.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models.Devices
{
    public class PsiPerm : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public PsiPerm()
        {
            // Set defaults
            DeviceTypeEnum = Type.PsiPerm;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.XML", FileExtType = FileExt.Type.N42, FileParser = new PsiPermN42Parser() }
            };
        }
    }
}
