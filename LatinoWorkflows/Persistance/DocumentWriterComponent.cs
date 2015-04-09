/*==========================================================================;
 *
 *  This file is part of LATINO. See http://latino.sf.net
 *
 *  File:    DocumentWriterComponent.cs
 *  Desc:    Document writer component
 *  Created: May-2013
 *
 *  Author:  Miha Grcar
 *
 *  License: MIT (http://opensource.org/licenses/MIT)
 *
 ***************************************************************************/

using System;
using System.Security.Cryptography;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.IO.Compression;
using Latino.Workflows.TextMining;
using System.Text;

namespace Latino.Workflows.Persistance
{
    public class DocumentWriterComponent : StreamDataConsumer
    {
        private string mConnectionString;
        private string mXmlDataRoot;
        private string mHtmlDataRoot;
        private string mHtmlViewRoot;
        private int mCommandTimeout;

        private static object mStaticLock
            = new object();

        public DocumentWriterComponent(string connectionString, int cmdTimeout, string xmlDataRoot, string htmlDataRoot, string htmlViewRoot) : base(typeof(DocumentWriterComponent))
        {
            mConnectionString = connectionString;
            mXmlDataRoot = xmlDataRoot == null ? null : xmlDataRoot.TrimEnd('\\');
            mHtmlDataRoot = htmlDataRoot == null ? null : htmlDataRoot.TrimEnd('\\');
            mHtmlViewRoot = htmlViewRoot == null ? null : htmlViewRoot.TrimEnd('\\');
            mCommandTimeout = cmdTimeout;
        }

        private static DataTable CreateTable()
        {
            DataTable t = new DataTable();
            t.Columns.Add("guid", typeof(Guid));
            t.Columns.Add("hash", typeof(Guid));
            t.Columns.Add("title", typeof(string));
            t.Columns.Add("description", typeof(string));
            t.Columns.Add("snippet", typeof(string));
            t.Columns.Add("category", typeof(string));
            t.Columns.Add("link", typeof(string));
            t.Columns.Add("responseUrl", typeof(string));
            t.Columns.Add("urlkey", typeof(string));
            t.Columns.Add("time", typeof(DateTime));
            t.Columns.Add("pubDate", typeof(string));
            t.Columns.Add("mimeType", typeof(string));
            t.Columns.Add("charSet", typeof(string));
            t.Columns.Add("contentLength", typeof(int));
            t.Columns.Add("domainName", typeof(string));
            t.Columns.Add("bprBoilerplateCharCount", typeof(int));
            t.Columns.Add("bprContentCharCount", typeof(int));
            t.Columns.Add("unseenContentCharCount", typeof(int));
            t.Columns.Add("rev", typeof(int));
            t.Columns.Add("fileName", typeof(string));
            t.Columns.Add("siteId", typeof(string));
            return t;
        }

        private static DataTable CreateTextBlocksTable()
        {
            DataTable t = new DataTable();
            t.Columns.Add("docGuid", typeof(Guid));
            t.Columns.Add("hashCodes", typeof(byte[]));
            //t.Columns.Add("hashCodesBase64", typeof(string));
            return t;
        }

        protected override void ConsumeData(IDataProducer sender, object data)
        {
            DocumentCorpus c = (DocumentCorpus)data;
            DataTable dt = CreateTable();
            DataTable dtTextBlocks = CreateTextBlocksTable();
            foreach (Document doc in c.Documents)
            {
                Document d = doc.Clone();
                string rawHtml = d.Features.GetFeatureValue("raw");
                DateTime time = DateTime.Parse(d.Features.GetFeatureValue("time"));
                Guid cGuid = new Guid(c.Features.GetFeatureValue("guid"));
                Guid dGuid = new Guid(d.Features.GetFeatureValue("guid"));
                ArrayList<byte> buffer = new ArrayList<byte>();
                buffer.AddRange(cGuid.ToByteArray());
                buffer.AddRange(dGuid.ToByteArray());
                Guid docId = new Guid(MD5.Create().ComputeHash(buffer.ToArray()));
                d.Features.RemoveFeature("raw");
                DateTime timeEnd = DateTime.Parse(c.Features.GetFeatureValue("timeEnd"));
                d.Features.SetFeatureValue("oldId", string.Format("{0:HH}_{0:mm}_{0:ss}_{1:N}_{2:N}", timeEnd, cGuid, dGuid));
                d.Features.SetFeatureValue("hash", dGuid.ToString("N"));
                d.Features.SetFeatureValue("guid", docId.ToString("N"));
                d.Features.SetFeatureValue("rssUrl", c.Features.GetFeatureValue("sourceUrl"));
                d.Features.SetFeatureValue("siteId", c.Features.GetFeatureValue("siteId"));
                // remove boilerplate removal features, keep hash codes 
                ArrayList<ulong> hashCodes = new ArrayList<ulong>();
                foreach (Annotation annot in d.Annotations)
                {
                    if (annot.Type.StartsWith("TextBlock")) 
                    {
                        ulong hashCode = Convert.ToUInt64(annot.Features.GetFeatureValue("hash"));
                        hashCodes.Add(hashCode);
                        string linkToTextRatio = annot.Features.GetFeatureValue("linkToTextRatio");
                        string domPath = annot.Features.GetFeatureValue("domPath");
                        annot.Features.Clear();
                        annot.Features.SetFeatureValue("linkToTextRatio", linkToTextRatio);
                        annot.Features.SetFeatureValue("domPath", domPath);
                    }
                }
                // write doc XML
                if (mXmlDataRoot != null)
                {
                    string outFileName = string.Format("{0}\\{1:yyyy}\\{1:MM}\\{1:dd}\\{1:HH}_{1:mm}_{1:ss}_{2:N}.xml.gz", mXmlDataRoot, time, docId);
                    string path = new FileInfo(outFileName).DirectoryName;
                    Directory.CreateDirectory(path); 
                    d.WriteXmlCompressed(outFileName);
                }
                // write raw HTML
                if (mHtmlDataRoot != null)
                {
                    string outFileName = string.Format("{0}\\{1:yyyy}\\{1:MM}\\{1:dd}\\{1:HH}_{1:mm}_{1:ss}_{2:N}.html.gz", mHtmlDataRoot, time, docId);
                    string path = new FileInfo(outFileName).DirectoryName;
                    Directory.CreateDirectory(path); 
                    using (FileStream stream = new FileStream(outFileName, FileMode.Create))
                    {
                        using (GZipStream gzStream = new GZipStream(stream, CompressionMode.Compress))
                        {
                            using (BinaryWriter w = new BinaryWriter(gzStream))
                            {
                                w.Write(Convert.FromBase64String(rawHtml));
                            }
                        }
                    }
                }
                // write view HTML
                if (mHtmlViewRoot != null)
                {
                    string outFileName = string.Format("{0}\\{1:yyyy}\\{1:MM}\\{1:dd}\\{1:HH}_{1:mm}_{1:ss}_{2:N}.html", mHtmlViewRoot, time, docId);
                    string path = new FileInfo(outFileName).DirectoryName.TrimEnd('\\');
                    Directory.CreateDirectory(path);
                    if (!File.Exists(path + "\\Styles.css") || !File.Exists(path + "\\Code.js"))
                    {
                        string css = Utils.GetManifestResourceString(this.GetType(), "Styles.css");
                        string js = Utils.GetManifestResourceString(this.GetType(), "Code.js");
                        lock (mStaticLock)
                        {
                            File.WriteAllText(path + "\\Styles.css", css);
                            File.WriteAllText(path + "\\Code.js", js);
                        }
                    }
                    File.WriteAllText(outFileName, d.GetHtml(/*inlineCss=*/false, /*inlineJs=*/false), Encoding.UTF8); 
                }
                // prepare for bulk write
                if (mConnectionString != null) 
                {
                    string fileName = string.Format("{0:yyyy}\\{0:MM}\\{0:dd}\\{0:HH}_{0:mm}_{0:ss}_{1:N}.xml.gz", time, docId);                    
                    dt.Rows.Add(
                        new Guid(d.Features.GetFeatureValue("guid")),
                        dGuid,
                        Utils.Truncate(d.Name, 400),
                        Utils.Truncate(d.Features.GetFeatureValue("description"), 400),
                        Utils.Truncate(d.Text, 1000),
                        Utils.Truncate(d.Features.GetFeatureValue("category"), 400),
                        Utils.Truncate(d.Features.GetFeatureValue("link"), 400),
                        Utils.Truncate(d.Features.GetFeatureValue("responseUrl"), 400),
                        Utils.Truncate(d.Features.GetFeatureValue("urlKey"), 400),
                        DateTime.Parse(d.Features.GetFeatureValue("time")),
                        Utils.Truncate(d.Features.GetFeatureValue("pubDate"), 100),
                        Utils.Truncate(d.Features.GetFeatureValue("mimeType"), 80),
                        Utils.Truncate(d.Features.GetFeatureValue("charSet"), 40),
                        Convert.ToInt32(d.Features.GetFeatureValue("contentLength")),
                        Utils.Truncate(d.Features.GetFeatureValue("domainName"), 100),
                        Convert.ToInt32(d.Features.GetFeatureValue("bprBoilerplateCharCount")),
                        Convert.ToInt32(d.Features.GetFeatureValue("bprContentCharCount")),
                        Convert.ToInt32(d.Features.GetFeatureValue("unseenContentCharCount")),
                        Convert.ToInt32(d.Features.GetFeatureValue("rev")),
                        Utils.Truncate(fileName, 100),
                        Utils.Truncate(c.Features.GetFeatureValue("siteId"), 100)
                        );
                    BinarySerializer memSer = new BinarySerializer();
                    hashCodes.Save(memSer);
                    byte[] hashCodesBinary = new byte[memSer.Stream.Position];
                    Array.Copy(((MemoryStream)memSer.Stream).GetBuffer(), hashCodesBinary, hashCodesBinary.Length);
                    //string hashCodesBase64 = Convert.ToBase64String(hashCodesBinary, 0, (int)memSer.Stream.Position); // *** remove this after the transition
                    dtTextBlocks.Rows.Add(
                        new Guid(d.Features.GetFeatureValue("guid")),
                        hashCodesBinary//,
                        //hashCodesBase64
                        );
                }
            }
            // bulk write to database
            if (mConnectionString != null && dt.Rows.Count > 0)
            {
                using (SqlConnection connection = new SqlConnection(mConnectionString))
                {
                    connection.Open();
                    using (SqlBulkCopy bulkWriter = new SqlBulkCopy(connection))
                    {
                        bulkWriter.BulkCopyTimeout = mCommandTimeout;
                        bulkWriter.DestinationTableName = "Documents";
                        bulkWriter.WriteToServerRetryOnDeadlock(dt);
                        bulkWriter.DestinationTableName = "TextBlocks";
                        bulkWriter.WriteToServerRetryOnDeadlock(dtTextBlocks);
                    }
                }
            }
        }
    }
}
