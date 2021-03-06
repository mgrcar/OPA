﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Reflection;

namespace LemmaSharp {
    [Serializable()]
    public class LemmatizerPrebuiltCompressed : LemmatizerPrebuilt {

        #region Private Variables

        protected static string sFileMask = "compressed-{0}.lem";

        #endregion

        #region Constructor(s) & Destructor(s)

        public LemmatizerPrebuiltCompressed(LanguagePrebuilt lang)
            : base(lang) {
            Stream stream = GetResourceStream(ResourceFileName);
            this.Deserialize(stream, true);
            stream.Close();
        }

        #endregion

        #region Private Properties

        protected string ResourceFileName {
            get {
                return GetResourceFileName(sFileMask);
            }
        }

        #endregion

        #region Resource Management Functions

        protected override Assembly GetExecutingAssembly() {
            return Assembly.GetExecutingAssembly();
        }

        #endregion

    }

}
