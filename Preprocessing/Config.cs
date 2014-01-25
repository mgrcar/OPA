using Latino;

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
        public static readonly string OutputFolder
            = Utils.GetConfigValue("OutputFolder", ".").TrimEnd('\\');
        public static readonly string BlogMetaDataFile
            = Utils.GetConfigValue("BlogMetaDataFile", "BlogMetaData.txt");
        public static readonly string TmpFolder
            = Utils.GetConfigValue("TmpFolder", @".\Tmp").TrimEnd('\\');
        public static readonly string ParserModelFile
            = Utils.GetConfigValue("ParserModelFile", "light.model");
        public static readonly string ParserFolder
           = Utils.GetConfigValue("ParserFolder", @".").TrimEnd('\\');
        public static readonly string JavaArgs
           = Utils.GetConfigValue("JavaArgs", "-Xmx4g");
        public static readonly int BatchSize
            = Utils.GetConfigValue<int>("BatchSize", "300");
    }
}
