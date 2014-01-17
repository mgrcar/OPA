using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using Latino;
using PosTagger;
using System.Text.RegularExpressions;
using Latino.Model;

namespace Analysis
{
    class Program
    {
        static void Main(string[] args)
        {
            using (StreamWriter w = new StreamWriter(@"C:\Users\Administrator\Desktop\orange.tab", /*append=*/false, Encoding.ASCII))
            {
                
                /*
                mFeatures.Add("ttr", ttr);
                mFeatures.Add("brunet", brunet);
                mFeatures.Add("honore", honore);
                mFeatures.Add("hl", hl);
                mFeatures.Add("ttrLemma", ttrLemma);
                mFeatures.Add("brunetLemma", brunetLemma);
                mFeatures.Add("honoreLemma", honoreLemma);
                mFeatures.Add("hlLemma", hlLemma);
                mFeatures.Add("ari", ari);
                mFeatures.Add("flesch", flesch);
                mFeatures.Add("fog", fog);
                mFeatures.Add("rWords", rWords);
                mFeatures.Add("rChars", rChars);
                mFeatures.Add("rSyllables", rSyllables);
                mFeatures.Add("rComplex", rComplex);
                 */


                w.WriteLine("ttr\tbrunet\thonore\thl\tttrLemma\tbrunetLemma\thonoreLemma\thlLemma\tari\tflesch\tfog\trWords\trChars\trSyllables\trComplex\tauthor");
                w.WriteLine("c\tc\tc\tc\tc\tc\tc\tc\tc\tc\tc\tc\tc\tc\tc\td");
                w.WriteLine("\t\t\t\t\t\t\t\t\t\t\t\t\t\t\tclass");


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
                    text.ComputeFeatures(); // computes Detextive features
                    // run chunker
                    //Console.WriteLine("Razkosavam stavke (chunking)...");
                    //Chunker.Create(doc);

                    
                    SparseVector<double> vec = new SparseVector<double>();

                    int i = 0;
                    foreach (string featureName in "ttr,brunet,honore,hl,ttrLemma,brunetLemma,honoreLemma,hlLemma,ari,flesch,fog,rWords,rChars,rSyllables,rComplex".Split(','))
                    {
                        //if (double.IsNaN(text.mFeatures[featureName]) || double.IsInfinity(text.mFeatures[featureName]))
                        //{
                        //    w.Write("0.0\t");
                        //    vec[i++] = 0;
                        //}
                        //else
                        {
                            w.Write(text.mFeatures[featureName] + "\t");
                            vec[i++] = text.mFeatures[featureName];
                        }
                    }

                    string author = Regex.Replace(text.mAuthor, @"\.blogspot\.com", "");
                    w.WriteLine(author);
                    dataset.Add(new LabeledExample<string, SparseVector<double>>(author, vec));

                    

                    


                   // return;
                }
                string[] featureNames = "ttr,brunet,honore,hl,ttrLemma,brunetLemma,honoreLemma,hlLemma,ari,flesch,fog,rWords,rChars,rSyllables,rComplex".Split(',');
                foreach (IdxDat<double> item in ReliefF.ComputeReliefF(dataset, dataset.Count, 10).OrderByDescending(x => x.Dat))
                {
                    Console.WriteLine(featureNames[item.Idx] + " " + item.Dat);
                }
            }
            // all done
            Console.WriteLine("Koncano.");
        }
    }
}
