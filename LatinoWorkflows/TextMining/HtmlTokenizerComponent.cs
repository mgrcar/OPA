/*==========================================================================;
 *
 *  This file is part of LATINO. See http://latino.sf.net
 *
 *  File:    HtmlTokenizerComponent.cs
 *  Desc:    HTML tokenizer component
 *  Created: Mar-2012
 *
 *  Author:  Miha Grcar
 *
 *  License: MIT (http://opensource.org/licenses/MIT)
 *
 ***************************************************************************/

using System;
using System.Linq;
using System.Text;
using Latino.WebMining;
using Latino.Workflows.TextMining;
using System.Collections.Generic;

namespace Latino.Workflows.WebMining
{
    /* .-----------------------------------------------------------------------
       |
       |  Class HtmlTokenizerComponent
       |
       '-----------------------------------------------------------------------
    */
    public class HtmlTokenizerComponent : DocumentProcessor
    {
        private static Set<string> mIgnoreTags 
            = new Set<string>("ins,del,bdo,em,strong,dfn,code,samp,kbd,var,cite,abbr,acronym,q,sub,sup,tt,i,b,big,small,u,s,strike,basefont,font,a".Split(','));
        private static Set<string> mSplitTags 
            = Set<string>.Difference(
                // the following list of HTML5 tags is taken from http://www.quackit.com/html_5/tags/
                new Set<string>("!doctype,a,abbr,address,area,article,aside,audio,b,base,bb,bdo,blockquote,body,br,button,canvas,caption,cite,code,col,colgroup,command,datagrid,datalist,dd,del,details,dfn,div,dl,dt,em,embed,eventsource,fieldset,figcaption,figure,footer,form,h1,h2,h3,h4,h5,h6,head,header,hgroup,hr,html,i,iframe,img,input,ins,kbd,keygen,label,legend,li,link,mark,map,menu,meta,meter,nav,noscript,object,ol,optgroup,option,output,p,param,pre,progress,q,ruby,rp,rt,samp,script,section,select,small,source,span,strong,style,sub,summary,sup,table,tbody,td,textarea,tfoot,th,thead,time,title,tr,ul,var,video,wbr".Split(',')), 
                mIgnoreTags);

        public HtmlTokenizerComponent() : base(typeof(HtmlTokenizerComponent))
        {
        }

        public/*protected*/ override void ProcessDocument(Document document)
        {
            string contentType = document.Features.GetFeatureValue("contentType");
            if (contentType != "Html") { return; }
            try
            {
                HtmlTokenizer htmlTokenizer = new HtmlTokenizer(document.Text, /*stemmer=*/null, /*decode=*/true, /*tokenize=*/false, /*applySkipRules=*/true);
                int idx = 0;
                ArrayList<string> txtBlocks = new ArrayList<string>();
                bool merge = false;
                Stack<string> tags = new Stack<string>();
                for (HtmlTokenizer.Enumerator e = (HtmlTokenizer.Enumerator)htmlTokenizer.GetEnumerator(); e.MoveNext(); )
                {
                    if (e.CurrentToken.TokenType == HtmlTokenizer.TokenType.Text)
                    {
                        string textBlock = Utils.ToOneLine(e.Current.Trim(), /*compact=*/true);
                        if (textBlock != "")
                        {
                            string domPath = tags.Aggregate((x, y) => y + "/" + x);
                            bool isLink = tags.Contains("a");
                            if (!merge)
                            {
                                txtBlocks.Add(textBlock);
                                document.AddAnnotation(new Annotation(idx, idx + textBlock.Length - 1, "TextBlock"));
                                document.Annotations.Last.Features.SetFeatureValue("domPath", domPath);
                                document.Annotations.Last.Features.SetFeatureValue("linkToTextRatio", string.Format("{0}/{1}", isLink ? textBlock.Length : 0, textBlock.Length));
                            }
                            else
                            {
                                idx--;
                                txtBlocks.Last += " " + textBlock;
                                int oldStartIdx = document.GetAnnotationAt(document.AnnotationCount - 1).SpanStart;
                                string oldDomPath = document.Annotations.Last.Features.GetFeatureValue("domPath");
                                string oldLinkToTextRatio = document.Annotations.Last.Features.GetFeatureValue("linkToTextRatio");
                                document.RemoveAnnotationAt(document.AnnotationCount - 1);
                                document.AddAnnotation(new Annotation(oldStartIdx, idx + textBlock.Length - 1, "TextBlock"));
                                document.Annotations.Last.Features.SetFeatureValue("domPath", domPath.Length < oldDomPath.Length ? domPath : oldDomPath);
                                int linkCharCount = Convert.ToInt32(oldLinkToTextRatio.Split('/')[0]) + (isLink ? textBlock.Length : 0);
                                int textCharCount = Convert.ToInt32(oldLinkToTextRatio.Split('/')[1]) + textBlock.Length;
                                document.Annotations.Last.Features.SetFeatureValue("linkToTextRatio", string.Format("{0}/{1}", linkCharCount, textCharCount));
                            }
                            idx += textBlock.Length + 2;
                            merge = true;
                        }
                    }
                    else
                    {
                        string tagName = e.CurrentToken.TagName.ToLower();
                        if (mSplitTags.Contains(tagName))
                        {
                            merge = false;
                        }
                        if (e.CurrentToken.TokenType == HtmlTokenizer.TokenType.StartTag)
                        {
                            tags.Push(tagName);
                        }
                        else if (e.CurrentToken.TokenType == HtmlTokenizer.TokenType.EndTag)
                        {
                            string endTagName = null;
                            if (tags.Count == 0 || (endTagName = tags.Pop()) != tagName)
                            {
                                mLogger.Error("ProcessDocument", "End tag does not match start tag (found {0} instead of {1}).", endTagName == null ? "nothing" : endTagName, tagName);
                                tags.Push(endTagName);
                            }
                        }
                    }
                }
                StringBuilder sb = new StringBuilder();
                foreach (string textBlock in txtBlocks)
                {
                    sb.AppendLine(textBlock);
                }
                document.Text = sb.ToString();
                document.Features.SetFeatureValue("contentType", "Text");
            }
            catch (Exception exception)
            {
                mLogger.Error("ProcessDocument", exception);
            }
        }
    }
}