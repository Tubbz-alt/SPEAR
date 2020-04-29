using SPEAR.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Parsers
{
    public interface IFileParserCallback
    {
        void ParsingError(string title, string message);

        void ParsingStarted();
        void ParsingUpdate(float percentComplete);
        void ParsingComplete(IEnumerable<DeviceData> deviceDatas);
    }
}
