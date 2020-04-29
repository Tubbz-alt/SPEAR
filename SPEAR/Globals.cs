using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR
{
    public static class Globals
    {
        public static CultureInfo CultureInfo = new CultureInfo("en-US");

        // Single Delimiters
        public static char[] Delim_Newline= new char[] { '\n' };
        public static char[] Delim_LeftSquareBracket = new char[] { '[' };
        public static char[] Delim_Colon = new char[] { ':' };
        public static char[] Delim_Dollar = new char[] { '$' };
        public static char[] Delim_LeftArrow = new char[] { '<' };
        public static char[] Delim_RightArrow = new char[] { '>' };
        public static char[] Delim_Space = new char[] { ' ' };
        public static char[] Delim_SemiColon = new char[] { ';' };
        public static char[] Delim_Hashtag = new char[] { '#' };        // lol
        public static char[] Delim_Bar = new char[] { '|' };
        public static char[] Delim_UnderLine = new char[] { '_' };

        // Multiple Delimiters
        public static char[] Delims_NewLine_Space = new char[] { '\n', ' ' };


    }
}
