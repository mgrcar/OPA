/*==========================================================================;
 *
 *  This file is part of LATINO. See http://latino.sf.net
 *
 *  File:    GenericStreamDataConsumer.cs
 *  Desc:    Generic (customizable) stream data consumer
 *  Created: Dec-2010
 *
 *  Author:  Miha Grcar
 *
 *  License: MIT (http://opensource.org/licenses/MIT)
 *
 ***************************************************************************/

namespace Latino.Workflows
{
    /* .-----------------------------------------------------------------------
       |
       |  Class GenericStreamDataConsumer
       |
       '-----------------------------------------------------------------------
    */
    public class GenericStreamDataConsumer : StreamDataConsumer
    {
        public delegate void ConsumeDataHandler(IDataProducer sender, object data);

        public event ConsumeDataHandler OnConsumeData
            = null;

        public GenericStreamDataConsumer() : base(typeof(GenericStreamDataConsumer))
        {
        }

        protected override void ConsumeData(IDataProducer sender, object data)
        {
            if (OnConsumeData != null)
            {
                OnConsumeData(sender, data);
            }
        }
    }
}
