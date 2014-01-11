﻿using Latino;

namespace OPA
{
    public static class Config
    {
        public static readonly string PosTaggerModel
            = Utils.GetConfigValue("PosTaggerModel", "TaggerFeb2012.bin");
        public static readonly string LemmatizerModel
            = Utils.GetConfigValue("LemmatizerModel", "LemmatizerFeb2012.bin");
        public static readonly string DataFolder
            = Utils.GetConfigValue("DataFolder", ".").TrimEnd('\\');
    }
}
