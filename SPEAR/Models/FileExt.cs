using SPEAR.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models
{
    public class FileExt
    {
        public enum Type { None, N42, SPE, SPC, ID, SQL }

        public Type FileExtType { get; set; }

        public string FileExtName { get; set; }

        public FileParser FileParser { get; set; }
    }
}
