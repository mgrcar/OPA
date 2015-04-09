/*==========================================================================;
 *
 *  File:    Config.cs
 *  Desc:    Configuration setting from App.config
 *  Created: Jan-2014
 *
 *  Author:  Miha Grcar
 *
 ***************************************************************************/

using Latino;

namespace OPA.Analysis
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
