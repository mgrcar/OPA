/*==========================================================================;
 *
 *  This file is part of LATINO. See http://latino.sf.net
 *
 *  File:    DocumentConsumer.cs
 *  Desc:    Document consumer base 
 *  Created: Dec-2010
 *
 *  Authors: Miha Grcar
 *
 *  License: MIT (http://opensource.org/licenses/MIT)
 *
 ***************************************************************************/

using System;

namespace Latino.Workflows.TextMining
{
    /* .-----------------------------------------------------------------------
       |
       |  Class DocumentConsumer
       |
       '-----------------------------------------------------------------------
    */
    public abstract class DocumentConsumer : StreamDataConsumer
    {
        protected override void ConsumeData(IDataProducer sender, object data)
        {
            Utils.ThrowException(!(data is DocumentCorpus) ? new ArgumentTypeException("data") : null);
            DocumentCorpus corpus = (DocumentCorpus)data;
            foreach (Document document in corpus.Documents)
            {
                ConsumeDocument(document);
            }
        }

        protected abstract void ConsumeDocument(Document document);
    }
}
