using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml;
using System.Text;
using Latino;
using Latino.Model;
using PosTagger;

namespace Analysis
{
    public class BlogMetaData
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

    public enum ClassType
    {
        AuthorName,
        AuthorGender,
        AuthorEducation,
        AuthorLocation,
        AuthorAge
    }  


    public static class Program
    {
        public static string GetLabel(BlogMetaData metaData, ClassType classType)
        {
            switch (classType)
            {
                case ClassType.AuthorName:
                    return metaData.mBlog;
                case ClassType.AuthorAge:
                    return metaData.mAuthorAge.Replace(' ', '_');
                case ClassType.AuthorEducation:
                    return metaData.mAuthorEducation.Replace(' ', '_').Replace('š', 's').Replace('č', 'c');
                case ClassType.AuthorGender:
                    return metaData.mAuthorGender.Replace('Ž', 'Z');
                case ClassType.AuthorLocation:
                    return metaData.mAuthorLocation;
                default:
                    return metaData.mBlog;
            }
        }

        static ChunkType MapChunkType(ChunkType chunkType)
        {
            if ((int)chunkType > 7) { chunkType = (ChunkType)((int)chunkType - 7); }
            if (chunkType == ChunkType.AdjP) { chunkType = ChunkType.NP; }
            return chunkType;
        }

        static void Main(string[] args)
        {
            //Console.WriteLine(Path.GetFileNameWithoutExtension(@"C:\test\test.bak"));
            //return;
            string[] featureNames = "ttr,brunet,honore,hl,ttrLemma,brunetLemma,honoreLemma,hlLemma,ari,flesch,fog,rWords,rChars,rSyllables,rComplex,M04,M05,M06,M07,M08,M09,M10,M11,M12,M13".Split(',');
            LabeledDataset<BlogMetaData, SparseVector<double>> dataset = new LabeledDataset<BlogMetaData, SparseVector<double>>();
            Console.WriteLine("Analiza besedil...");
            foreach (string fileName in Directory.GetFiles(Config.DataFolder, "*.xml"))
            {
                // load XML
                Console.WriteLine("Datoteka {0}...", fileName);
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(File.ReadAllText(fileName).Replace("xmlns=\"http://www.tei-c.org/ns/1.0\"", ""));
                Corpus corpus = new Corpus();
                corpus.LoadFromXmlFile(fileName, /*tagLen=*/int.MaxValue);
#if TEST_CHUNKER
                Text text = null;
#else
                Text text = new Text(corpus, doc.SelectSingleNode("//header/naslov").InnerText, doc.SelectSingleNode("//header/blog").InnerText/*blog identifier is used as author identifier*/);
                text.ComputeFeatures(); // compute Detextive features
#endif
                // run chunker
                Console.WriteLine("Razkosavam stavke (chunking)...");
                ArrayList<Chunk> chunks = Chunker.GetChunks(doc);
                chunks.ForEach(x => x.mType = MapChunkType(x.mType));
#if TEST_CHUNKER
                return;
#endif
                // get blog meta-data
                BlogMetaData metaData = new BlogMetaData();
                metaData.mAuthorAge = doc.SelectSingleNode("//header/avtorStarost").InnerText;
                metaData.mAuthorEducation = doc.SelectSingleNode("//header/avtorIzobrazba").InnerText;
                metaData.mAuthorGender = doc.SelectSingleNode("//header/avtorSpol").InnerText;
                metaData.mAuthorLocation = doc.SelectSingleNode("//header/avtorRegija").InnerText;
                metaData.mBlog = doc.SelectSingleNode("//header/blog").InnerText;
                // compute features M04-M13 from Stamatatos et al.: Automatic Text Categorization in Terms of Genre and Author (2000)
                double totalChunks = chunks.Count;
                double[] M = new double[10];
                double numNP = chunks.Count(x => x.mType == ChunkType.NP);
                double numVP = chunks.Count(x => x.mType == ChunkType.VP);
                double numAP = chunks.Count(x => x.mType == ChunkType.AP);
                double numPP = chunks.Count(x => x.mType == ChunkType.PP);
                double numCON = chunks.Count(x => x.mType == ChunkType.CON);
                if (totalChunks > 0)
                {
                    M[0] = numNP / totalChunks;
                    M[1] = numVP / totalChunks;
                    M[2] = numAP / totalChunks;
                    M[3] = numPP / totalChunks;
                    M[4] = numCON / totalChunks;
                }
                double numWordsNP = chunks.Where(x => x.mType == ChunkType.NP).Select(x => x.mItems.Count).Sum();
                M[5] = numNP == 0 ? 0 : (numWordsNP / numNP);
                double numWordsVP = chunks.Where(x => x.mType == ChunkType.VP).Select(x => x.mItems.Count).Sum();
                M[6] = numVP == 0 ? 0 : (numWordsVP / numVP);
                double numWordsAP = chunks.Where(x => x.mType == ChunkType.AP).Select(x => x.mItems.Count).Sum();
                M[7] = numAP == 0 ? 0 : (numWordsAP / numAP);
                double numWordsPP = chunks.Where(x => x.mType == ChunkType.PP).Select(x => x.mItems.Count).Sum();
                M[8] = numPP == 0 ? 0 : (numWordsPP / numPP);
                double numWordsCON = chunks.Where(x => x.mType == ChunkType.CON).Select(x => x.mItems.Count).Sum();
                M[9] = numCON == 0 ? 0 : (numWordsCON / numCON);
                // create dataset
                SparseVector<double> vec = new SparseVector<double>();
                int i = 0;
                foreach (string featureName in "ttr,brunet,honore,hl,ttrLemma,brunetLemma,honoreLemma,hlLemma,ari,flesch,fog,rWords,rChars,rSyllables,rComplex".Split(','))
                {
                    if (double.IsNaN(text.mFeatures[featureName]) || double.IsInfinity(text.mFeatures[featureName])) { vec[i++] = 0; }
                    else { vec[i++] = text.mFeatures[featureName]; }
                }
                foreach (double val in M)
                {
                    vec[i++] = val;
                }
                dataset.Add(new LabeledExample<BlogMetaData, SparseVector<double>>(metaData, vec));
                string htmlFileName = Config.HtmlFolder + "\\" + Path.GetFileNameWithoutExtension(fileName) + ".html";
                Html.SaveHtml(featureNames, vec, doc, chunks, htmlFileName);
            }
            // save as Orange and Weka file            
            foreach (ClassType classType in new ClassType[] { ClassType.AuthorName, ClassType.AuthorAge, ClassType.AuthorGender, ClassType.AuthorEducation, ClassType.AuthorLocation })
            {
                Weka.SaveArff(featureNames, dataset, classType, Config.OutputFolder + "\\" + string.Format("OPA-{0}.arff", classType));
                Orange.SaveTab(featureNames, dataset, classType, Config.OutputFolder + "\\" + string.Format("OPA-{0}.tab", classType));
            }
            //foreach (IdxDat<double> item in ReliefF.ComputeReliefF(dataset, dataset.Count, 10).OrderByDescending(x => x.Dat))
            //{
            //    Console.WriteLine(featureNames[item.Idx] + " " + item.Dat);
            //}
            // all done
            Console.WriteLine("Koncano.");
        }
    }
}
