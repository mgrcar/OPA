using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Text;
using Latino;
using PosTagger;

namespace OPA
{
    class BlogMetaData
    {
        public string mBlog = "";
        public string mBlogUrl = "";
        public string mBlogTitle = "";
        public string mBlogTitleShort = "";
        public string mAuthorEMail = "";
        public string mAuthorGender = "";
        public string mAuthorAge = "";
        public string mAuthorLocation = "";
        public string mAuthorEducation = "";
    }

    class Program
    {
        static Dictionary<string, BlogMetaData> mBlogMetaData
            = new Dictionary<string, BlogMetaData>();

        static string MakeOutputFileName(string fileName)
        {
            int extLen = new FileInfo(fileName).Extension.Length;
            string name = new FileInfo(fileName).Name;
            string outputFileNameFormat = name.Substring(0, name.Length - extLen) + "-out.xml";
            string outputFolder = Utils.GetConfigValue("OutputFolder", new FileInfo(fileName).DirectoryName).TrimEnd('\\') + "\\";
            return string.Format(outputFolder + outputFileNameFormat);
        }

        static void LockFile(string fileName)
        {
            string lockFileName = MakeOutputFileName(fileName) + ".locked";
            using (StreamWriter w = new StreamWriter(lockFileName))
            {
                w.Write("LOCKED"); 
            }
        }

        static void LoadBlogMetaData()
        {
            string fileName = Config.BlogMetaDataFile;
            string[] lines = File.ReadAllLines(fileName);
            foreach (string line in lines)
            {
                if (!line.TrimStart().StartsWith("#"))
                {
                    string[] fields = line.Split('\t');
                    BlogMetaData blogMetaData = new BlogMetaData();
                    blogMetaData.mBlog = fields[0];
                    blogMetaData.mBlogUrl = fields[1];
                    blogMetaData.mBlogTitle = fields[2];
                    blogMetaData.mBlogTitleShort = fields[3];
                    if (fields.Length > 4) { blogMetaData.mAuthorEMail = fields[4]; }
                    if (fields.Length > 5) { blogMetaData.mAuthorGender = fields[5]; }
                    if (fields.Length > 6) { blogMetaData.mAuthorAge = fields[6]; }
                    if (fields.Length > 7) { blogMetaData.mAuthorLocation = fields[7]; }
                    if (fields.Length > 8) { blogMetaData.mAuthorEducation = fields[8]; }
                    mBlogMetaData.Add(blogMetaData.mBlog, blogMetaData);
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Nalagam meta-podatke o blogih...");
            LoadBlogMetaData();
            Console.WriteLine("Nalagam oznacevalnik...");
            PartOfSpeechTagger posTagger = new PartOfSpeechTagger(Config.PosTaggerModel, Config.LemmatizerModel);
            string pattern = "*.xml";
            if (args.Length > 0) { pattern = args[0]; }
            Queue<string> fileNames = new Queue<string>(Directory.GetFiles(Config.DataFolder, pattern));
            while (fileNames.Count > 0)
            {
                int n = 0;
                XmlDocument fullDoc = null;
                while (fileNames.Count > 0 && n < Config.BatchSize) 
                {                    
                    string fileName = fileNames.Dequeue();
                    if (File.Exists(MakeOutputFileName(fileName))) 
                    {
                        Console.WriteLine("Ze obdelano: {0}.", fileName);
                        continue; 
                    }
                    if (File.Exists(MakeOutputFileName(fileName) + ".locked"))
                    {
                        Console.WriteLine("Zaklenjeno: {0}.", fileName);
                        continue;
                    }     
                    // load text
                    Console.WriteLine("Datoteka {0}...", fileName);
                    XmlDocument tmpDoc = new XmlDocument();
                    string xml = File.ReadAllText(fileName);
                    xml = xml.Replace("// ]]>", "").Replace("//--><!]]>", "");
                    tmpDoc.LoadXml(xml);
                    string text = tmpDoc.SelectSingleNode("//besedilo").InnerText;
                    if (text.Trim() == "") { continue; } // *** empty documents are ignored
                    Corpus corpus = new Corpus();
                    corpus.LoadFromTextSsjTokenizer(text);
                    // tag text
                    Console.WriteLine("Oznacujem besedilo...");
                    posTagger.Tag(corpus);
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(corpus.ToString("XML-MI").Replace("xmlns=\"http://www.tei-c.org/ns/1.0\"", "")); // *** remove this f***ing namespace
                    ((XmlElement)doc.SelectSingleNode("//text")).SetAttribute("fileName", fileName);
                    // append text to fullDoc
                    if (fullDoc == null)
                    {
                        fullDoc = doc;
                    }
                    else
                    {
                        XmlDocumentFragment xmlFrag = fullDoc.CreateDocumentFragment();
                        xmlFrag.InnerXml = doc.SelectSingleNode("//text").OuterXml;
                        fullDoc.DocumentElement.AppendChild(xmlFrag);
                    }
                    n++;
                    // check if meta-data exists
                    //string key = tmpDoc.SelectSingleNode("//header/blog").InnerText;
                    //if (!mBlogMetaData.ContainsKey(key))
                    //{
                    //    Console.WriteLine("*** Cannot find meta-data for " + key);
                    //    return;
                    //}
                }
                // save tagged text for parsing
                Console.WriteLine("Pripravljam datoteke za razclenjevanje...");
                Guid tmpId = Guid.NewGuid();
                string tmpFileNameIn = Config.TmpFolder + "\\" + tmpId.ToString("N") + ".tmp";
                string tmpFileNameOut = Config.TmpFolder + "\\" + tmpId.ToString("N") + ".out.tmp";
                XmlWriterSettings xmlSettings = new XmlWriterSettings();
                xmlSettings.Encoding = Encoding.UTF8;
                xmlSettings.Indent = true;
                using (XmlWriter w = XmlWriter.Create(tmpFileNameIn, xmlSettings))
                {
                    fullDoc.Save(w);
                }
                // parse text
                Console.WriteLine("Zaganjam razclenjevalnik...");
                Parser.Parse(tmpFileNameIn, tmpFileNameOut);
                // load results
                if (!File.Exists(tmpFileNameOut))
                {
                    // lock files and continue
                    fullDoc.SelectNodes("//text").Cast<XmlElement>().ToList().ForEach(x => LockFile(x.Attributes["fileName"].Value));
                    continue;
                }
                fullDoc = new XmlDocument();
                fullDoc.Load(tmpFileNameOut);
                // create output files
                Console.WriteLine("Pisem izhodne datoteke...");
                foreach (XmlNode txtNode in fullDoc.SelectNodes("//text"))
                {
                    string fileName = txtNode.Attributes["fileName"].Value;
                    ((XmlElement)txtNode).RemoveAttribute("fileName");
                    Console.WriteLine("Datoteka {0}...", fileName);
                    XmlDocument tmpDoc = new XmlDocument();
                    string xml = File.ReadAllText(fileName);
                    xml = xml.Replace("// ]]>", "").Replace("//--><!]]>", "");
                    tmpDoc.LoadXml(xml);
                    // insert input XML into TEI-XML
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml("<TEI>" + txtNode.OuterXml + "</TEI>");
                    XmlDocumentFragment docPart = doc.CreateDocumentFragment();
                    docPart.InnerXml = tmpDoc.OuterXml;
                    doc.DocumentElement.PrependChild(docPart);
                    // insert blog meta-data
                    string key = doc.SelectSingleNode("//header/blog").InnerText;
                    BlogMetaData metaData;
                    if (!mBlogMetaData.ContainsKey(key))
                    {
                        Console.WriteLine("*** Ne najdem podatkov o blogu \"{0}\".", key);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("Vstavljam meta-podatke o blogu...");
                        metaData = mBlogMetaData[key];
                        XmlNode node = doc.SelectSingleNode("//header");
                        node.AppendChild(doc.CreateElement("blogSpletniNaslov")).InnerText = metaData.mBlogUrl;
                        node.AppendChild(doc.CreateElement("blogNaslov")).InnerText = metaData.mBlogTitle;
                        node.AppendChild(doc.CreateElement("blogNaslovKratek")).InnerText = metaData.mBlogTitleShort;
                        //node.AppendChild(doc.CreateElement("avtorEMail")).InnerText = metaData.mAuthorEMail;
                        node.AppendChild(doc.CreateElement("avtorSpol")).InnerText = metaData.mAuthorGender;
                        node.AppendChild(doc.CreateElement("avtorStarost")).InnerText = metaData.mAuthorAge;
                        node.AppendChild(doc.CreateElement("avtorRegija")).InnerText = metaData.mAuthorLocation;
                        node.AppendChild(doc.CreateElement("avtorIzobrazba")).InnerText = metaData.mAuthorEducation;
                    }
                    // write results
                    Console.WriteLine("Zapisujem rezultate...");
                    using (XmlWriter w = XmlWriter.Create(MakeOutputFileName(fileName), xmlSettings))
                    {
                        doc.Save(w);
                    }
                }
            }
            // purge temp folder
            Directory.GetFiles(Config.TmpFolder, "*.tmp").ToList().ForEach(x => File.Delete(x));
            // all done
            Console.WriteLine("Koncano.");
        }
    }
}
