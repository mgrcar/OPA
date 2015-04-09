/*==========================================================================;
 *
 *  This file is part of LATINO. See http://latino.sf.net
 *
 *  File:    LanguageDetectorComponent.cs
 *  Desc:    Language detector component 
 *  Created: Oct-2011
 *
 *  Author:  Miha Grcar
 *
 *  License: MIT (http://opensource.org/licenses/MIT)
 *
 ***************************************************************************/

using System;
using System.Text;
using Latino.TextMining;

namespace Latino.Workflows.TextMining
{
    /* .-----------------------------------------------------------------------
       |
       |  Class LanguageDetectorComponent
       |
       '-----------------------------------------------------------------------
    */
    public class LanguageDetectorComponent : DocumentProcessor
    {
        private static LanguageDetector mLanguageDetector
            = LanguageDetector.GetLanguageDetectorPrebuilt();
        private int mMinTextLen // TODO: make this configurable
            = 100;

        public LanguageDetectorComponent() : base(typeof(LanguageDetectorComponent))
        {
            mBlockSelector = "TextBlock";
        }

        public/*protected*/ override void ProcessDocument(Document document)
        {
            string contentType = document.Features.GetFeatureValue("contentType");
            if (contentType != "Text") { return; }
            StringBuilder strBuilder = new StringBuilder();
            try
            {
                TextBlock[] blocks = document.GetAnnotatedBlocks(mBlockSelector);
                foreach (TextBlock block in blocks)
                {
                    strBuilder.AppendLine(block.Text);
                }
                string text = strBuilder.ToString();
                if (text.Length >= mMinTextLen) 
                {
                    LanguageProfile langProfile = mLanguageDetector.DetectLanguage(text);
                    if (langProfile != null)
                    {
                        document.Features.SetFeatureValue("detectedLanguage", langProfile.Language.ToString());
                    }
                }
                if (text.Length > 0)
                {
                    document.Features.SetFeatureValue("detectedCharRange", TextMiningUtils.GetCharRange(text));
                }
            }
            catch (Exception exception)
            {
                mLogger.Error("ProcessDocument", exception);
            }
        }
    }
}