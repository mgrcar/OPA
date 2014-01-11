using System;
using System.IO;
using System.Xml;
using PosTagger;

namespace OPA
{
    class Program
    {
        static void Main(string[] args)
        {
            //PartOfSpeechTagger posTagger = new PartOfSpeechTagger(Config.PosTaggerModel, Config.LemmatizerModel);
            Console.WriteLine("Predobdelava besedil...");
            foreach (string fileName in Directory.GetFiles(Config.DataFolder, "*.xml"))
            {
                Console.WriteLine("Datoteka {0}...", fileName);
                XmlDocument doc = new XmlDocument();
                doc.Load(fileName);
                string text = doc.SelectSingleNode("//besedilo").InnerText;
                //Console.WriteLine(text);                
            }
        }
    }
}
