using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Models
{
    public class DeviceInfo
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Enums
        /////////////////////////////////////////////////////////////////////////////////////////
        public enum Type {
            None,
            AISense,
            ArktisP2000,
            AspectMKC,
            AtomTex,
            BNCSam,
            DetectiveX,
            FlirR500,
            FlirR400,
            identiFINDER,
            KromekD3S,
            MirionSpiridentMobile,
            MirionSpirPack,
            NucTech,
            NuviaRadScout,
            NuviaSiris,
            OrtecDetectiveRemote,
            Polimaster,
            RadEagle,
            RadEyeSPRD,
            RadSeeker,
            RIIDEyeX,
            Rs700,
            RSI,
            SymetricaDiscoverMobile,
            SymetricaSN33N,
            Verifinder,
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public Type DeviceTypeEnum { get; protected set; }

        public List<FileExt> SupportedFileExts { get; protected set; }
    }
}
