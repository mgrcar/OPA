using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.IO;
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

        public void CollectAll(Set<ParseTreeNode> nodes, Set<ParseTreeNode> tabu)
        {
            nodes.Add(this);
            tabu.Add(this);
            foreach (Pair<string, ParseTreeNode> link in mOutLinks.Where(x => !x.Second.mUsed && !tabu.Contains(x.Second)))
            {
                link.Second.CollectAll(nodes, tabu);
            }
        }
    }

    public enum ChunkType
    {
        VP,
        NP,
        PP,
        AdjP,
        AP,
        CON,
        Other
    }

    public class Chunk
    {
        public ChunkType mType;
        public ArrayList<ParseTreeNode> mItems;
        public int mDepth = -1;

        public Chunk(ChunkType type) : this(type, new ParseTreeNode[] { })
        {
        }

        public Chunk(ChunkType type, IEnumerable<ParseTreeNode> items)
        {
            mType = type;
            mItems = new ArrayList<ParseTreeNode>(items);
        }

        public override string ToString()
        {
            int seqNum = mItems.Min(x => x.mSeqNum);
            string chunkStr = "";
            foreach (ParseTreeNode part in mItems.OrderBy(x => x.mSeqNum))
            {
                if (seqNum != part.mSeqNum) { chunkStr += "... "; }
                seqNum = part.mSeqNum + 1;
                chunkStr += part.mWord + " ";
            }
            return chunkStr;
        }
    
    }

    public static class Chunker
    {
        static StreamWriter w = new StreamWriter(@"C:\Users\Administrator\Desktop\chunkerV2.txt");

        public static void CreateChunksDfs(ParseTreeNode node, string rel, Set<ParseTreeNode> tabu, ArrayList<Chunk> chunks)
        {
            tabu.Add(node);
            foreach (Pair<string, ParseTreeNode> childInfo in node.mOutLinks)
            {
                if (!tabu.Contains(childInfo.Second))
                {
                    CreateChunksDfs(childInfo.Second, childInfo.First, tabu, chunks);
                }
            }
            // check if we should cut here
            Set<string> linksToCut = new Set<string>("modra,ena,dve,tri,štiri,dol,prir".Split(','));
            // if I came here via "vez" ...
            if (rel == "vez")
            {
                // ... then the subtree starting at this node is CON
                Set<ParseTreeNode> nodes = new Set<ParseTreeNode>();
                node.CollectAll(nodes, tabu);
                //Console.WriteLine("Found CON: " + new Chunk(ChunkType.CON, nodes));
                chunks.Add(new Chunk(ChunkType.CON, nodes));
                return;
            }
            // if I came here via "modra", "1", "2", "3", "4", "dol", or "prir" ...
            else if (linksToCut.Contains(rel)) 
            {
                // ... and this node is "G" or "Pd" ...
                if (node.mTag.StartsWith("G") || node.mTag.StartsWith("Pd"))
                {
                    // ... then the subtree starting at this node is VP
                    Set<ParseTreeNode> nodes = new Set<ParseTreeNode>();
                    node.CollectAll(nodes, tabu);
                    //Console.WriteLine("Found VP: " + new Chunk(ChunkType.VP, nodes));
                    chunks.Add(new Chunk(ChunkType.VP, nodes));
                    return;
                }
                // ... and this node is "P", "R", "S", "K", or "Z" ...
                else if (node.mTag.StartsWith("P") || node.mTag.StartsWith("R") || node.mTag.StartsWith("S") || node.mTag.StartsWith("K") || node.mTag.StartsWith("Z"))
                { 
                    // ... then the subtree starting at this node is AP, AdjP, NP, or PP
                    // * PP: as soon as there's D in the subtree
                    // * AP: no D in the subtree & this node is R
                    // * AdjP: no D in the subtree & this node is P
                    // * NP: no D in the subtree & this node is S, K, or Z
                    Set<ParseTreeNode> nodes = new Set<ParseTreeNode>();
                    node.CollectAll(nodes, tabu);
                    ChunkType chunkType = ChunkType.NP;
                    if (nodes.Any(x => x.mTag.StartsWith("D"))) 
                    { 
                        chunkType = ChunkType.PP; 
                    }
                    else
                    {
                        if (node.mTag.StartsWith("R")) { chunkType = ChunkType.AP; }
                        else if (node.mTag.StartsWith("P")) { chunkType = ChunkType.AdjP; }
                    }
                    //Console.WriteLine("Found " + chunkType + ": " + new Chunk(chunkType, nodes));
                    chunks.Add(new Chunk(chunkType, nodes));
                    return;
                }
            }
            tabu.Remove(node);
        }

        public static ArrayList<Chunk> GetChunks(XmlDocument xmlDoc)
        {
            ArrayList<Chunk> chunks = new ArrayList<Chunk>();
            XmlNodeList nodes = xmlDoc.SelectNodes("//text/body//p/s");
            MultiSet<string> stats = new MultiSet<string>();
            foreach (XmlNode node in nodes) // for each sentence...
            {
                // read word data
                int seqNum = 1;
                Dictionary<string, ParseTreeNode> parseTreeNodes = new Dictionary<string, ParseTreeNode>();                
                foreach (XmlNode wordNode in node.SelectNodes("w"))
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
                w.WriteLine();
                foreach (XmlNode wordNode in node.SelectNodes("w | c")) 
                {
                    w.Write(wordNode.InnerText + " ");
                }
                w.WriteLine();
                w.WriteLine();
                // read parse tree
                foreach (XmlNode linkNode in node.SelectNodes("links/link"))
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
                    if (parseTreeNodes.ContainsKey(fromNodeId) && parseTreeNodes.ContainsKey(toNodeId)) // *** sometimes these things are punctuations - are these parser errors?
                    {
                        ParseTreeNode fromNode = parseTreeNodes[fromNodeId];
                        ParseTreeNode toNode = parseTreeNodes[toNodeId];
                        fromNode.mOutLinks.Add(new Pair<string, ParseTreeNode>(type, toNode));
                        toNode.mInLinks.Add(new Pair<string, ParseTreeNode>(type, fromNode));
                    }
                }                
                // find trees pointed to by "blue"
                ArrayList<Chunk> chunksThisSentence = new ArrayList<Chunk>();
                foreach (ParseTreeNode treeNode in parseTreeNodes.Values.Where(x => x.mBlue))
                {
                    // find chunks in a DFS manner
                    Set<ParseTreeNode> tabu = new Set<ParseTreeNode>();                    
                    CreateChunksDfs(treeNode, "modra", tabu, chunksThisSentence);
                }
                chunks.AddRange(chunksThisSentence);
                // write chunks
                ChunkType[] types = new ChunkType[] { ChunkType.VP, ChunkType.CON, ChunkType.NP, ChunkType.PP, ChunkType.AdjP, ChunkType.AP };
                foreach (ChunkType type in types)
                {
                    w.WriteLine(type + ":");
                    foreach (Chunk chunk in chunksThisSentence.Where(x => x.mType == type))
                    {
                        w.WriteLine("\t" + chunk);
                    }
                }
            }
            return chunks;
        }
    }
}
