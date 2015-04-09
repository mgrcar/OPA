using Latino;

namespace Latino.Workflows
{
    public static class Config
    {
        public static readonly string rssReaderDefaultRssXmlEncoding
            = Utils.GetConfigValue("rssReaderDefaultRssXmlEncoding", "ISO-8859-1");
        public static readonly string rssReaderDefaultHtmlEncoding
            = Utils.GetConfigValue("rssReaderDefaultHtmlEncoding", "ISO-8859-1");
    }
}
