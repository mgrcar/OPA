using System.Text;
using System.Linq;
using System.IO;
using System.Xml;
using Latino;
using Latino.Workflows.TextMining;

namespace Analysis
{
    public static class Html
    {
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
