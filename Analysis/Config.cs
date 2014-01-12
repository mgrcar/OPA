using Latino;

namespace Analysis
{
    public static class Config
    {
        public static readonly string DataFolder
            = Utils.GetConfigValue("DataFolder", ".").TrimEnd('\\');
    }
}
