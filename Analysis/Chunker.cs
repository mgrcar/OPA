using System.Linq;
using System.Xml;
using System.Collections.Generic;
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

        public void CollectVP(Set<ParseTreeNode> nodes)
        {
            nodes.Add(this);
            foreach (Pair<string, ParseTreeNode> link in mOutLinks.Where(x => !x.Second.mUsed && !nodes.Contains(x.Second) && (x.First == "del" || (x.First == "dol" && x.Second.mTag.StartsWith("G")))))
            {
                link.Second.CollectVP(nodes);
            }
        }

        public void CollectCON(Set<ParseTreeNode> nodes)
        {
            nodes.Add(this);
            foreach (Pair<string, ParseTreeNode> link in mOutLinks.Where(x => !x.Second.mUsed && !nodes.Contains(x.Second) && x.First == "skup"))
            {
                link.Second.CollectCON(nodes);
            }
        }

        public void CollectAll(Set<ParseTreeNode> nodes)
        {
            nodes.Add(this);
            foreach (Pair<string, ParseTreeNode> link in mOutLinks.Where(x => !x.Second.mUsed && !nodes.Contains(x.Second)))
            {
                link.Second.CollectAll(nodes);
            }
        }

        public void CollectAdjP(Set<ParseTreeNode> nodes, int depth, ArrayList<Chunk> chunks)
        {
            nodes.Add(this);
            // check if it is adjective
            if (mTag.StartsWith("P"))
            {
                Set<ParseTreeNode> chunkNodes = new Set<ParseTreeNode>();
                CollectAll(chunkNodes);
                Chunk chunk = new Chunk(ChunkType.AdjP, chunkNodes);
                chunk.mDepth = depth;
                chunks.Add(chunk);
            }
            // process children
            foreach (Pair<string, ParseTreeNode> link in mOutLinks.Where(x => !x.Second.mUsed && !nodes.Contains(x.Second) && (x.First == "dol" || x.First == "prir")))
            {
                link.Second.CollectAdjP(nodes, depth + 1, chunks);
            }
        }

        public void CollectNP_PP(Set<ParseTreeNode> nodes, int depth, ArrayList<Chunk> chunks, ParseTreeNode comingFrom)
        {
            nodes.Add(this);
            // check if it is noun
            if (mTag.StartsWith("S") /*&& (comingFrom == null || !comingFrom.mTag.StartsWith("S"))*/)
            {
                Set<ParseTreeNode> chunkNodes = new Set<ParseTreeNode>();
                CollectAll(chunkNodes);
                Chunk chunk = new Chunk(chunkNodes.Any(x => x.mTag.StartsWith("D")) ? ChunkType.PP : ChunkType.NP, chunkNodes);
                chunk.mDepth = depth;
                chunks.Add(chunk);
            }
            // process children
            foreach (Pair<string, ParseTreeNode> link in mOutLinks.Where(x => !x.Second.mUsed && !nodes.Contains(x.Second) && (x.First == "dol" || x.First == "prir")))
            {
                link.Second.CollectNP_PP(nodes, depth + 1, chunks, this);
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

        public static ArrayList<Chunk> GetChunks(XmlDocument xmlDoc)
        {
            ArrayList<Chunk> result = new ArrayList<Chunk>();
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
                    if (parseTreeNodes.ContainsKey(fromNodeId) && parseTreeNodes.ContainsKey(toNodeId)) // *** sometimes these things are punctuations - these are parser errors
                    {
                        ParseTreeNode fromNode = parseTreeNodes[fromNodeId];
                        ParseTreeNode toNode = parseTreeNodes[toNodeId];
                        fromNode.mOutLinks.Add(new Pair<string, ParseTreeNode>(type, toNode));
                        toNode.mInLinks.Add(new Pair<string, ParseTreeNode>(type, fromNode));
                    }
                }
                // create chunks
                Set<string> numericLinkTypes = new Set<string>("ena,dve,tri,štiri".Split(','));
                // extract VP
                w.WriteLine("VP:");
                bool repeat = true;
                while (repeat)
                {
                    repeat = false;
                    Set<ParseTreeNode> bestChunk = null;
                    foreach (ParseTreeNode parseTreeNode in parseTreeNodes.Values.Where(x => !x.mUsed && x.mBlue && x.mTag.StartsWith("G")))
                    {                        
                        Set<ParseTreeNode> chunkNodes = new Set<ParseTreeNode>();
                        parseTreeNode.CollectVP(chunkNodes);
                        if (bestChunk == null || chunkNodes.Count > bestChunk.Count) { bestChunk = chunkNodes; }
                    }
                    if (bestChunk != null)
                    {
                        bestChunk.ToList().ForEach(x => x.mUsed = true);
                        repeat = true;
                        w.Write("\t");
                        result.Add(new Chunk(ChunkType.VP, bestChunk));
                        w.WriteLine(result.Last);
                    }
                }
                // extract CON
                w.WriteLine("CON:");
                repeat = true;
                while (repeat)
                {
                    repeat = false;
                    Set<ParseTreeNode> bestChunk = null;
                    foreach (ParseTreeNode parseTreeNode in parseTreeNodes.Values.Where(x => !x.mUsed && x.mInLinks.Any(y => y.First == "vez")))
                    {                        
                        Set<ParseTreeNode> chunkNodes = new Set<ParseTreeNode>();
                        parseTreeNode.CollectCON(chunkNodes);
                        if (bestChunk == null || chunkNodes.Count > bestChunk.Count) { bestChunk = chunkNodes; }
                    }
                    if (bestChunk != null)
                    {
                        bestChunk.ToList().ForEach(x => x.mUsed = true);
                        repeat = true;
                        w.Write("\t");
                        result.Add(new Chunk(ChunkType.CON, bestChunk));
                        w.WriteLine(result.Last);
                    }
                }
                // extract NP/PP
                ArrayList<Chunk> allChunks = new ArrayList<Chunk>();
                repeat = true;
                while (repeat)
                {
                    repeat = false;
                    Chunk bestChunk = null;
                    foreach (ParseTreeNode parseTreeNode in parseTreeNodes.Values.Where(
                        x => !x.mUsed &&
                        (x.mInLinks.Any(y => numericLinkTypes.Contains(y.First)) ||
                        x.mInLinks.Any(y => y.First == "dol" && y.Second.mLemma.ToLower() == "biti"))))
                    {                        
                        Set<ParseTreeNode> tabu = new Set<ParseTreeNode>();
                        ArrayList<Chunk> chunks = new ArrayList<Chunk>();
                        parseTreeNode.CollectNP_PP(tabu, /*depth=*/1, chunks, null);
                        if (chunks.Count > 0)
                        {                            
                            // get max depth
                            int maxDepth = chunks.Max(x => x.mDepth);
                            // get max len within max depth
                            IEnumerable<Chunk> bestChunks = chunks.Where(x => x.mDepth == maxDepth).OrderByDescending(x => x.mItems.Count);
                            Chunk firstChunk = bestChunks.First();
                            if (bestChunk == null || firstChunk.mDepth > bestChunk.mDepth || (firstChunk.mDepth == bestChunk.mDepth && firstChunk.mItems.Count > bestChunk.mItems.Count))
                            {
                                bestChunk = firstChunk;
                            }
                        }
                    }
                    if (bestChunk != null)
                    {
                        bestChunk.mItems.ToList().ForEach(x => x.mUsed = true);
                        repeat = true;
                        allChunks.Add(bestChunk);
                    }
                }
                w.WriteLine("NP:");
                foreach (Chunk chunk in allChunks.Where(x => x.mType == ChunkType.NP))
                {
                    w.Write('\t');
                    result.Add(chunk);
                    w.WriteLine(chunk.ToString());
                }
                w.WriteLine("PP:");
                foreach (Chunk chunk in allChunks.Where(x => x.mType == ChunkType.PP))
                {
                    w.Write('\t');
                    result.Add(chunk);
                    w.WriteLine(chunk.ToString());
                }
                // extract AdjP
                repeat = true;
                w.WriteLine("AdjP:");
                while (repeat)
                {
                    repeat = false;
                    Chunk bestChunk = null;
                    foreach (ParseTreeNode parseTreeNode in parseTreeNodes.Values.Where(
                        x => !x.mUsed &&
                        (x.mInLinks.Any(y => numericLinkTypes.Contains(y.First)) ||
                        x.mInLinks.Any(y => y.First == "dol" && y.Second.mLemma.ToLower() == "biti"))))
                    {
                        Set<ParseTreeNode> tabu = new Set<ParseTreeNode>();
                        ArrayList<Chunk> chunks = new ArrayList<Chunk>();
                        parseTreeNode.CollectAdjP(tabu, /*depth=*/1, chunks);
                        if (chunks.Count > 0)
                        {
                            // get max depth
                            int maxDepth = chunks.Max(x => x.mDepth);
                            // get max len within max depth
                            IEnumerable<Chunk> bestChunks = chunks.Where(x => x.mDepth == maxDepth).OrderByDescending(x => x.mItems.Count);
                            Chunk firstChunk = bestChunks.First();
                            if (bestChunk == null || firstChunk.mDepth > bestChunk.mDepth || (firstChunk.mDepth == bestChunk.mDepth && firstChunk.mItems.Count > bestChunk.mItems.Count))
                            {
                                bestChunk = firstChunk;
                            }
                        }
                    }
                    if (bestChunk != null)
                    {
                        bestChunk.mItems.ToList().ForEach(x => x.mUsed = true);
                        repeat = true;
                        w.Write("\t");
                        result.Add(bestChunk);
                        w.WriteLine(bestChunk);
                    }
                }
                // extract AP
                w.WriteLine("AP:");
                repeat = true;
                while (repeat)
                {
                    repeat = false;
                    Set<ParseTreeNode> bestChunk = null;
                    foreach (ParseTreeNode parseTreeNode in parseTreeNodes.Values.Where(x => !x.mUsed && x.mInLinks.Any(y => numericLinkTypes.Contains(y.First)) && x.mTag.StartsWith("R")))
                    {                        
                        Set<ParseTreeNode> chunkNodes = new Set<ParseTreeNode>();
                        parseTreeNode.CollectAll(chunkNodes);
                        if (bestChunk == null || chunkNodes.Count > bestChunk.Count) { bestChunk = chunkNodes; }
                    }
                    if (bestChunk != null)
                    {
                        bestChunk.ToList().ForEach(x => x.mUsed = true);
                        repeat = true;
                        w.Write("\t");
                        result.Add(new Chunk(ChunkType.AP, bestChunk));
                        w.WriteLine(result.Last);
                    }
                }
                // extract othe chunks
                w.WriteLine("other:");
                repeat = true;
                while (repeat)
                {
                    repeat = false;
                    Set<ParseTreeNode> bestChunk = null;
                    foreach (ParseTreeNode parseTreeNode in parseTreeNodes.Values.Where(x => !x.mUsed))
                    {
                        Set<ParseTreeNode> chunkNodes = new Set<ParseTreeNode>();
                        parseTreeNode.CollectAll(chunkNodes);
                        if (bestChunk == null || chunkNodes.Count > bestChunk.Count) { bestChunk = chunkNodes; }
                    }
                    if (bestChunk != null)
                    {
                        bestChunk.ToList().ForEach(x => x.mUsed = true);
                        repeat = true;
                        w.Write("\t");
                        result.Add(new Chunk(ChunkType.Other, bestChunk));
                        w.WriteLine(result.Last);
                    }
                }
            }
            return result;
        }
    }
}
