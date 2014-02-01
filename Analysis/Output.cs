/*==========================================================================;
 *
 *  File:    Output.cs
 *  Desc:    Writes data to various output formats
 *  Created: Jan-2014
 *
 *  Author:  Miha Grcar
 *
 ***************************************************************************/

using System;
using System.Xml;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Latino;
using Latino.Model;
using Latino.Workflows.TextMining;

namespace OPA.Analysis
{
    public static class Output
    {
        public static void SaveArff(string[] featureNames, LabeledDataset<BlogMetaData, SparseVector<double>> dataset, ClassType classType, string fileName)
        {
            using (StreamWriter w = new StreamWriter(fileName, /*append=*/false, Encoding.ASCII))
            {
                w.WriteLine("@RELATION r" + Guid.NewGuid().ToString("N"));
                w.WriteLine();
                foreach (string featureName in featureNames)
                {
                    w.WriteLine("@ATTRIBUTE " + featureName + " NUMERIC");
                }
                w.Write("@ATTRIBUTE class ");
                ArrayList<string> classes = new ArrayList<string>();
                ((IEnumerable<LabeledExample<BlogMetaData, SparseVector<double>>>)dataset).ToList().ForEach(
                    x => classes.AddRange(AnalysisUtils.GetLabel(x.Label, classType).Split(',')));
                classes = new ArrayList<string>(classes.Distinct());
                w.WriteLine(classes.ToString().Replace("( ", "{").Replace(" )", "}").Replace(" ", ","));
                w.WriteLine();
                w.WriteLine("@DATA");
                foreach (LabeledExample<BlogMetaData, SparseVector<double>> lblEx in dataset)
                {
                    foreach (string lblStr in AnalysisUtils.GetLabel(lblEx.Label, classType).Split(','))
                    {
                        if (lblStr != "")
                        {
                            foreach (IdxDat<double> item in lblEx.Example)
                            {
                                w.Write(item.Dat + ",");
                            }
                            w.WriteLine(lblStr);
                        }
                    }
                }
            }
        }

        public static void SaveTab(string[] featureNames, LabeledDataset<BlogMetaData, SparseVector<double>> dataset, ClassType classType, string fileName)
        {
            using (StreamWriter w = new StreamWriter(fileName, /*append=*/false, Encoding.ASCII))
            {
                for (int i = 0; i < featureNames.Length; i++)
                {
                    w.Write(featureNames[i] + "\t");
                }
                w.WriteLine("author");
                for (int i = 0; i < featureNames.Length; i++)
                {
                    w.Write("c\t");
                }
                w.WriteLine("d");
                for (int i = 0; i < featureNames.Length; i++)
                {
                    w.Write("\t");
                }
                w.WriteLine("class");
                foreach (LabeledExample<BlogMetaData, SparseVector<double>> lblEx in dataset)
                {
                    foreach (string lblStr in AnalysisUtils.GetLabel(lblEx.Label, classType).Split(','))
                    {
                        if (lblStr != "")
                        {
                            foreach (IdxDat<double> item in lblEx.Example)
                            {
                                w.Write(item.Dat + "\t");
                            }
                            w.WriteLine(lblStr);
                        }
                    }
                }
            }
        }

        public static void SaveHtml(string[] featureNames, SparseVector<double> vec, XmlDocument xmlDoc, ArrayList<Chunk> chunks, string fileName)
        {
            Document doc = new Document(xmlDoc.SelectSingleNode("//header/naslov").InnerText, "");
            StringBuilder txt = new StringBuilder();
            XmlNodeList nodes = xmlDoc.SelectNodes("//text/body//p/s");
            foreach (XmlNode node in nodes) // for each sentence...
            {
                foreach (XmlNode wordNode in node.SelectNodes("w | c | S"))
                {
                    if (wordNode.Name == "S")
                    {
                        txt.Append(" ");
                    }
                    else
                    {
                        string str = wordNode.InnerText;
                        int spanStart = txt.Length;
                        int spanEnd = spanStart + str.Length - 1;
                        txt.Append(str);
                        Annotation a = new Annotation(spanStart, spanEnd, wordNode.Name == "w" ? "beseda" : "ločilo");
                        if (wordNode.Name == "w")
                        {
                            a.Features.SetFeatureValue("oznaka", wordNode.Attributes["msd"].Value);
                            a.Features.SetFeatureValue("lema", wordNode.Attributes["lemma"].Value);
                        }
                        doc.AddAnnotation(a);
                    }
                }
                txt.AppendLine();
            }
            txt.AppendLine();
            txt.AppendLine("Rezultat členitve:");
            txt.AppendLine();
            foreach (ChunkType chunkType in new ChunkType[] { ChunkType.VP, ChunkType.NP, ChunkType.PP, ChunkType.AP, ChunkType.CON, ChunkType.Other })
            {
                string chunkTypeStr = chunkType.ToString();
                if (chunkTypeStr == "Other") { chunkTypeStr = "Ostalo"; }
                txt.AppendLine(chunkTypeStr + ":");
                foreach (Chunk chunk in chunks.Where(x => x.mType == chunkType))
                {
                    txt.AppendLine("\t" + chunk.ToString());
                }
            }
            doc.Text = txt.ToString();
            int i = 0;
            foreach (string featureName in featureNames)
            {
                doc.Features.SetFeatureValue(featureName, vec[i++].ToString());
            }
            using (StreamWriter w = new StreamWriter(fileName, /*append=*/false, Encoding.UTF8))
            {
                doc.MakeHtmlPage(w, /*inlineCss=*/true);
            }
        }
    }
}
