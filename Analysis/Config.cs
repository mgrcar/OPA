using Latino;

namespace Analysis
{
    public static class Config
    {
        public static readonly string DataFolder
            = Utils.GetConfigValue("DataFolder", ".").TrimEnd('\\');
        public static readonly string OutputFolder
            = Utils.GetConfigValue("OutputFolder", ".").TrimEnd('\\');
        public static readonly string HtmlFolder
            = Utils.GetConfigValue("HtmlFolder", ".").TrimEnd('\\');
    }
}
