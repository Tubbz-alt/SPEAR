using System.Collections.Generic;

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
            AtomTexAT6101C,
            AtomTexAT6103,
            BNCSam,
            BubbleTechFlexSpec,
            DetectiveX,
            FlirR500,
            FlirR400,
            RadEyeSprdGn,
            H3DA400,
            identiFINDER,
            KromekD3SDhs,
            KromekD3SNsdd,
            MirionSpiridentMobile,
            MirionSpirPack,
            NucSafeGuardian,
            NucTech,
            NuviaRadScout,
            NuviaSiris,
            OrtecDetectiveRemote,
            Polimaster,
            PsiPerm,
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
