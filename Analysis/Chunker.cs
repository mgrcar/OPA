using System;
using System.Linq;
using System.Xml;
using System.Collections.Generic;
using Latino;

namespace Analysis
{
    public class ParseTreeNode
    {
        // properties
        public bool mBlue
            = false;
        public string mWord;
        public string mLemma;
        public string mTag;
        public string mId;
        public int mSeqNum;
        public bool mUsed
            = false;
        // links
        public ArrayList<Pair<string, ParseTreeNode>> mInLinks
            = new ArrayList<Pair<string, ParseTreeNode>>();
        public ArrayList<Pair<string, ParseTreeNode>> mOutLinks
            = new ArrayList<Pair<string, ParseTreeNode>>();

        public ParseTreeNode(string word, string lemma, string tag, string id, int seqNum)
        {
            mWord = word;
            mLemma = lemma;
            mTag = tag;
            mId = id;
            mSeqNum = seqNum;
        }

        public void CollectVP(ArrayList<ParseTreeNode> nodes)
        {
            nodes.Add(this);
            //mUsed = true;
            foreach (Pair<string, ParseTreeNode> link in mOutLinks.Where(x => !x.Second.mUsed && (x.First == "del" || (x.First == "dol" && x.Second.mTag.StartsWith("G")))))
            {
                link.Second.CollectVP(nodes);
            }
        }

        public void CollectCON(ArrayList<ParseTreeNode> nodes)
        {
            nodes.Add(this);
            //mUsed = true;
            foreach (Pair<string, ParseTreeNode> link in mOutLinks.Where(x => !x.Second.mUsed && x.First == "skup"))
            {
                link.Second.CollectCON(nodes);
            }
        }

        public void CollectAll(ArrayList<ParseTreeNode> nodes)
        {
            nodes.Add(this);
            //mUsed = true;
            foreach (Pair<string, ParseTreeNode> link in mOutLinks.Where(x => !x.Second.mUsed))
            {
                link.Second.CollectAll(nodes);
            }
        }

        public override string ToString()
        {
            return mWord;
        }
    }

    public enum ChunkType
    {
        VP,
        NP,
        PP,
        AdjP,
        AP,
        CON
    }

    public class Chunk
    {
        public ChunkType mType;
        public ArrayList<ParseTreeNode> mItems;

        public Chunk(ChunkType type) : this(type, new ParseTreeNode[] { })
        {
        }

        public Chunk(ChunkType type, IEnumerable<ParseTreeNode> items)
        {
            mType = type;
            mItems = new ArrayList<ParseTreeNode>(items);
        }
    }

    public class Chunker
    {
        public static Chunker Create(XmlDocument xmlDoc)
        {
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");
            XmlNodeList nodes = xmlDoc.SelectNodes("//tei:text/tei:body//tei:p/tei:s", nsmgr);
            Console.WriteLine(nodes.Count);
            foreach (XmlNode node in nodes.Cast<XmlNode>().Take(20)) // for each sentence...
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                // read word data
                int seqNum = 1;
                Dictionary<string, ParseTreeNode> parseTreeNodes = new Dictionary<string, ParseTreeNode>();                
                foreach (XmlNode wordNode in node.SelectNodes("tei:w", nsmgr))
                {
                    ParseTreeNode parseTreeNode = new ParseTreeNode(
                        wordNode.InnerText,
                        wordNode.Attributes["lemma"].Value,
                        wordNode.Attributes["msd"].Value,
                        wordNode.Attributes["xml:id"].Value,
                        seqNum++
                        );
                    parseTreeNodes.Add(parseTreeNode.mId, parseTreeNode);
                }
                // read parse tree
                foreach (XmlNode linkNode in node.SelectNodes("tei:links/tei:link", nsmgr))
                {
                    string type = linkNode.Attributes["afun"].Value;
                    string fromNodeId = linkNode.Attributes["from"].Value;
                    string toNodeId = linkNode.Attributes["dep"].Value;                    
                    if (type == "modra")
                    {
                        if (parseTreeNodes.ContainsKey(toNodeId)) // *** sometimes these things are punctuations but that's OK
                        { 
                            parseTreeNodes[toNodeId].mBlue = true; 
                        }
                        continue;
                    }                    
                    if (parseTreeNodes.ContainsKey(fromNodeId) && parseTreeNodes.ContainsKey(toNodeId)) // *** sometimes these things are punctuations - parser errors? 
                    {
                        ParseTreeNode fromNode = parseTreeNodes[fromNodeId];
                        ParseTreeNode toNode = parseTreeNodes[toNodeId];
                        fromNode.mOutLinks.Add(new Pair<string, ParseTreeNode>(type, toNode));
                        toNode.mInLinks.Add(new Pair<string, ParseTreeNode>(type, fromNode));
                    }
                }
                // create chunks
                // extract VP
                foreach (ParseTreeNode parseTreeNode in parseTreeNodes.Values.Where(x => !x.mUsed && x.mBlue && x.mTag.StartsWith("G")))
                {
                    ArrayList<ParseTreeNode> chunkNodes = new ArrayList<ParseTreeNode>();
                    parseTreeNode.CollectVP(chunkNodes);
                    Console.WriteLine("Found VP of len " + chunkNodes.Count);
                    Console.WriteLine(chunkNodes);
                }
                // extract NP, PP, AdjP

                // extract AP
                Set<string> linkTypes = new Set<string>("ena,dve,tri,štiri".Split(','));
                foreach (ParseTreeNode parseTreeNode in parseTreeNodes.Values.Where(x => !x.mUsed && x.mInLinks.Any(y => linkTypes.Contains(y.First)) && x.mTag.StartsWith("R")))
                {
                    ArrayList<ParseTreeNode> chunkNodes = new ArrayList<ParseTreeNode>();
                    parseTreeNode.CollectAll(chunkNodes);
                    Console.WriteLine("Found AP of len " + chunkNodes.Count);
                    Console.WriteLine(chunkNodes);
                }
                // extract CON
                foreach (ParseTreeNode parseTreeNode in parseTreeNodes.Values.Where(x => !x.mUsed && x.mInLinks.Any(y => y.First == "vez")))
                {
                    ArrayList<ParseTreeNode> chunkNodes = new ArrayList<ParseTreeNode>();
                    parseTreeNode.CollectCON(chunkNodes);
                    Console.WriteLine("Found CON of len " + chunkNodes.Count);
                    Console.WriteLine(chunkNodes);
                }
                
            }
            return null;
        }
    }
}
