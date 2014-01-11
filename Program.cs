using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using Latino;
using PosTagger;

namespace OPA
{
    class BlogMetaData
    {
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

        static void LoadBlogMetaData()
        {
            string fileName = Config.BlogMetaDataFileName;
            string[] lines = File.ReadAllLines(fileName);
            foreach (string line in lines)
            {
                if (!line.TrimStart().StartsWith("#"))
                {
                    string[] fields = line.Split('\t');
                    BlogMetaData blogMetaData = new BlogMetaData();
                    blogMetaData.mBlogUrl = fields[0];
                    blogMetaData.mBlogTitle = fields[1];
                    blogMetaData.mBlogTitleShort = fields[2];
                    if (fields.Length > 3) { blogMetaData.mAuthorEMail = fields[3]; }
                    if (fields.Length > 4) { blogMetaData.mAuthorGender = fields[4]; }
                    if (fields.Length > 5) { blogMetaData.mAuthorAge = fields[5]; }
                    if (fields.Length > 6) { blogMetaData.mAuthorLocation = fields[6]; }
                    if (fields.Length > 7) { blogMetaData.mAuthorEducation = fields[7]; }
                    mBlogMetaData.Add(blogMetaData.mBlogUrl, blogMetaData);
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Nalagam meta-podatke o blogih...");
            LoadBlogMetaData();
            Console.WriteLine("Inicializacija Obeliksa...");
            PartOfSpeechTagger posTagger = new PartOfSpeechTagger(Config.PosTaggerModel, Config.LemmatizerModel);
            Console.WriteLine("Predobdelava besedil...");
            foreach (string fileName in Directory.GetFiles(Config.DataFolder, "*.xml"))
            {
                Console.WriteLine("Datoteka {0}...", fileName);
                XmlDocument tmpDoc = new XmlDocument();
                tmpDoc.Load(fileName);
                string text = tmpDoc.SelectSingleNode("//besedilo").InnerText;
                Corpus corpus = new Corpus();
                corpus.LoadFromTextSsjTokenizer(text);
                Console.WriteLine("Oznacujem besedilo...");
                posTagger.Tag(corpus);
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(corpus.ToString("XML"));
                XmlDocumentFragment docPart = doc.CreateDocumentFragment();
                docPart.InnerXml = tmpDoc.OuterXml;
                doc.DocumentElement.PrependChild(docPart);
                Console.WriteLine("Vstavljam meta-podatke o blogu...");
                string key = doc.SelectSingleNode("//header/blog").InnerText;
                if (!mBlogMetaData.ContainsKey(key))
                {
                    Console.WriteLine("*** Ne najdem podatkov o blogu \"{0}\".", key);
                }
                else
                { 
                    // insert blog meta-data into the header
                    BlogMetaData metaData = mBlogMetaData[key];
                    XmlNode node = doc.SelectSingleNode("//header");
                    node.AppendChild(doc.CreateElement("blogNaslov")).InnerText = metaData.mBlogTitle;
                    node.AppendChild(doc.CreateElement("blogNaslovKratek")).InnerText = metaData.mBlogTitleShort;
                    node.AppendChild(doc.CreateElement("avtorEMail")).InnerText = metaData.mAuthorEMail;
                    node.AppendChild(doc.CreateElement("avtorSpol")).InnerText = metaData.mAuthorGender;
                    node.AppendChild(doc.CreateElement("avtorStarost")).InnerText = metaData.mAuthorAge;
                    node.AppendChild(doc.CreateElement("avtorRegija")).InnerText = metaData.mAuthorLocation;
                    node.AppendChild(doc.CreateElement("avtorIzobrazba")).InnerText = metaData.mAuthorEducation;
                }
                Console.WriteLine("Zapisujem rezultate predobdelave (XML)...");
                XmlWriterSettings xmlSettings = new XmlWriterSettings();
                xmlSettings.Encoding = Encoding.UTF8;
                xmlSettings.Indent = true;
                using (XmlWriter w = XmlWriter.Create(MakeOutputFileName(fileName), xmlSettings))
                {
                    doc.Save(w);
                }
            }
            Console.WriteLine("Koncano.");
        }
    }
}
