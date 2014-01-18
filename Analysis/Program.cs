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
    class Program
    {
        static void Main(string[] args)
        {
            string[] featureNames = "ttr,brunet,honore,hl,ttrLemma,brunetLemma,honoreLemma,hlLemma,ari,flesch,fog,rWords,rChars,rSyllables,rComplex,M04,M05,M06,M07,M08,M09,M10,M11,M12,M13".Split(',');
            LabeledDataset<string, SparseVector<double>> dataset = new LabeledDataset<string, SparseVector<double>>();
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
                text.ComputeFeatures(); // compute Detextive features
                // run chunker
                Console.WriteLine("Razkosavam stavke (chunking)...");
                ArrayList<Chunk> chunks = Chunker.GetChunks(doc);
                // compute features M04-M13 from Stamatatos et al.: Automatic Text Categorization in Terms of Genre and Author (2000)
                double totalChunks = chunks.Count;
                double[] M = new double[10];
                double numNP = chunks.Count(x => x.mType == ChunkType.NP || x.mType == ChunkType.AdjP);
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
                double numWordsNP = chunks.Where(x => x.mType == ChunkType.NP || x.mType == ChunkType.AdjP).Select(x => x.mItems.Count).Sum();
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
                    if (double.IsNaN(text.mFeatures[featureName]) || double.IsInfinity(text.mFeatures[featureName]))
                    {
                        vec[i++] = 0;
                    }
                    else
                    {
                        vec[i++] = text.mFeatures[featureName];
                    }
                }
                foreach (double val in M)
                {
                    vec[i++] = val;
                }
                string author = Regex.Replace(text.mAuthor, @"\.blogspot\.com", "");
                dataset.Add(new LabeledExample<string, SparseVector<double>>(author, vec));
                Html.SaveHtml(featureNames, vec, doc, chunks, fileName + ".html");
            }
            // save as Orange and Weka file            
            Orange.SaveTab(featureNames, dataset, @"C:\Users\Administrator\Desktop\orange.tab");
            Weka.SaveArff(featureNames, dataset, @"C:\Users\Administrator\Desktop\weka.arff");
            foreach (IdxDat<double> item in ReliefF.ComputeReliefF(dataset, dataset.Count, 10).OrderByDescending(x => x.Dat))
            {
                Console.WriteLine(featureNames[item.Idx] + " " + item.Dat);
            }
            // all done
            Console.WriteLine("Koncano.");
        }
    }
}
