using System;
using System.Collections.Generic;
using SPEAR.Parsers.Devices;

namespace SPEAR.Models.Devices
{
    public class AtomTexAT6101C : DeviceInfo
    {
        public AtomTexAT6101C()
        {
            // Set defaults
            DeviceTypeEnum = Type.AtomTexAT6101C;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = "*.spe", FileExtType = FileExt.Type.SPE, FileParser = new AtomTexAT6101CSpeParser() }
            };
        }
    }
}
