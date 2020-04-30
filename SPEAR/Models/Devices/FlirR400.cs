using SPEAR.Parsers.Devices;
using System;
using System.Collections.Generic;

namespace SPEAR.Models.Devices
{
    public class FlirR400 : DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public FlirR400()
        {
            // Set defaults
            DeviceTypeEnum = Type.FlirR400;

            SupportedFileExts = new List<FileExt>() {
                new FileExt() { FileExtName = ".n42", FileExtType = FileExt.Type.N42, FileParser = new FlirR400N42Parser() },
                new FileExt() { FileExtName = "*.Measurement.spe", FileExtType = FileExt.Type.SPE, FileParser = new FlirR400SpeParser() }
            };
        }
    }
}
