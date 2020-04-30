using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models
{
    public class DeviceData
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public DeviceInfo.Type DeviceTypeEnum { get; protected set; }

        public string DeviceType { get; set; }

        public string SerialNumber { get; set; }

        public int TrialNumber { get; set; }

        public string FileName { get; set; }

        public DateTime StartDateTime { get; set; }

        public TimeSpan MeasureTime { get; set; }

        public double CountRate { get; set; }

        public List<NuclideID> Nuclides { get; protected set; }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public DeviceData(DeviceInfo.Type deviceType)
        {
            DeviceTypeEnum = deviceType;
            DeviceType = string.Empty;
            SerialNumber = string.Empty;
            FileName = string.Empty;
            TrialNumber = -1;
            StartDateTime = DateTime.MinValue;

            Nuclides = new List<NuclideID>() {
                new NuclideID(string.Empty, -1),
                new NuclideID(string.Empty, -1),
                new NuclideID(string.Empty, -1),
                new NuclideID(string.Empty, -1),
                new NuclideID(string.Empty, -1),
                new NuclideID(string.Empty, -1),
            };
        }
    }
}
