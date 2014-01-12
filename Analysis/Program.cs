using System;
using System.IO;
using System.Xml;
using Latino;
using PosTagger;

namespace Analysis
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Analiza besedil...");
            foreach (string fileName in Directory.GetFiles(Config.DataFolder, "*.xml"))
            {
                // load XML
                Console.WriteLine("Datoteka {0}...", fileName);
                XmlDocument doc = new XmlDocument();
                doc.Load(fileName);
                Corpus corpus = new Corpus();
                corpus.LoadFromXmlFile(fileName, /*tagLen=*/int.MaxValue);
                Text text = new Text(corpus, doc.SelectSingleNode("//header/naslov").InnerText, doc.SelectSingleNode("//header/blog").InnerText/*blog identifier is used as author identifier*/);
                text.ComputeFeatures(); // computes Detextive features
                // TODO: compute parse tree features 
                Console.WriteLine(text.mFeatures["hlLemma"]);
            }

            // all done
            Console.WriteLine("Koncano.");
        }
    }
}
