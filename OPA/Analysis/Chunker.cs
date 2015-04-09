/*==========================================================================;
 *
 *  File:    Chunker.cs
 *  Desc:    Determines chunks from parse tree
 *  Created: Jan-2014
 *
 *  Author:  Miha Grcar, Simon Krek
 *
 ***************************************************************************/

using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using Latino;

namespace OPA.Analysis
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
        VP = 1,
        NP = 2,
        PP = 3,
        AdjP = 4,
        AP = 5,
        CON = 6,
        Other = 7,
        // other (add 7)
        Other_VP = 8,
        Other_NP = 9,
        Other_PP = 10,
        Other_AdjP = 11,
        Other_AP = 12,
        Other_CON = 13,
        Other_Other = 14
    }

    public class Chunk
    {
        public ChunkType mType;
        public ArrayList<ParseTreeNode> mItems;
        public int mDepth 
            = -1;
        public bool mInner
            = false;

        public Chunk(ChunkType type, bool other) : this(type, new ParseTreeNode[] { }, other)
        {
        }

        public Chunk(ChunkType type, IEnumerable<ParseTreeNode> items, bool other)
        {
            mType = type;
            mItems = new ArrayList<ParseTreeNode>(items);
            if (other) { mType = (ChunkType)((int)mType + 7); }
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
#if TEST_CHUNKER
        private static StreamWriter w = new StreamWriter(@"C:\Users\Administrator\Desktop\chunkerV6.txt");
#endif 
        private static bool NoCut(ParseTreeNode node, ParseTreeNode prev, string rel)
        {
            if (prev != null)
            {
                return 
                    (rel == "ena" && prev.mTag.StartsWith("G") && Regex.Match(node.mTag, "^G..n").Success && prev.mOutLinks.Any(x => x.First == "tri" && x.Second.mTag.StartsWith("R"))) ||
                    (rel == "tri" && prev.mTag.StartsWith("G") && node.mTag.StartsWith("R") && prev.mOutLinks.Any(x => x.First == "ena" && Regex.Match(x.Second.mTag, "^G..n").Success));
            }
            return false;
        }

        private static void CreateChunksDfsInner(ParseTreeNode root, Set<ParseTreeNode> tabuRoot, ParseTreeNode node, string rel, Set<ParseTreeNode> tabu, ArrayList<Chunk> chunks)
        {
            tabu.Add(node);
            foreach (Pair<string, ParseTreeNode> childInfo in node.mOutLinks)
            {
                if (!tabu.Contains(childInfo.Second))
                {
                    CreateChunksDfsInner(root, tabuRoot, childInfo.Second, childInfo.First, tabu, chunks);
                }
            }
            // check if we should cut here
            if ((node.mTag.StartsWith("P") || node.mTag.StartsWith("R") || node.mTag.StartsWith("S") || node.mTag.StartsWith("K") || node.mTag.StartsWith("Z")) && rel == "dol" &&
                root != node)
            {
                // get the "lower" chunk
                Set<ParseTreeNode> nodesLower = new Set<ParseTreeNode>();
                Set<ParseTreeNode> tabuCopy = tabu.Clone();
                node.CollectAll(nodesLower, tabuCopy);
                ChunkType chunkType = ChunkType.NP;
                if (node.mOutLinks.Any(x => x.Second.mTag.StartsWith("D") && nodesLower.Contains(x.Second)))
                {
                    chunkType = ChunkType.PP;
                }
                else
                {
                    if (node.mTag.StartsWith("R")) { chunkType = ChunkType.AP; }
                    else if (node.mTag.StartsWith("P") || node.mTag.StartsWith("Kv")) { chunkType = ChunkType.AdjP; }
                }
                chunks.Add(new Chunk(chunkType, nodesLower, /*other=*/false));
                Console.WriteLine(chunks.Last);
                chunks.Last.mInner = true;
                // split recursively
                CreateChunksDfsInner(node, tabu, node, rel, tabu.Clone(), chunks);
                // get the "upper" chunk
                Set<ParseTreeNode> nodesUpper = new Set<ParseTreeNode>();
                tabuCopy = tabuRoot.Clone();
                tabuCopy.AddRange(nodesLower);
                root.CollectAll(nodesUpper, tabuCopy);
                chunkType = ChunkType.NP;
                if (root.mOutLinks.Any(x => x.Second.mTag.StartsWith("D") && nodesUpper.Contains(x.Second)))
                {
                    chunkType = ChunkType.PP;
                }
                else
                {
                    if (root.mTag.StartsWith("R")) { chunkType = ChunkType.AP; }
                    else if (root.mTag.StartsWith("P") || root.mTag.StartsWith("Kv")) { chunkType = ChunkType.AdjP; }
                }
                chunks.Add(new Chunk(chunkType, nodesUpper, /*other=*/false));
                Console.WriteLine(chunks.Last);
                chunks.Last.mInner = true;
                // split recursively
                tabuCopy = tabuRoot.Clone();
                tabuCopy.AddRange(nodesLower);
                CreateChunksDfsInner(root, tabuCopy, root, rel, tabuCopy.Clone(), chunks);
            }
            tabu.Remove(node);
        }

        private static void CreateChunksDfs(ParseTreeNode node, ParseTreeNode prev, string rel, Set<ParseTreeNode> tabu, ArrayList<Chunk> chunks, bool other)
        {
            tabu.Add(node);
            foreach (Pair<string, ParseTreeNode> childInfo in node.mOutLinks)
            {
                if (!tabu.Contains(childInfo.Second))
                {
                    CreateChunksDfs(childInfo.Second, node, childInfo.First, tabu, chunks, other);
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
                chunks.Add(new Chunk(ChunkType.CON, nodes, other));
                return;
            }
            // if I came here via "modra", "1", "2", "3", "4", "dol", or "prir" ...
            else if (linksToCut.Contains(rel) && !NoCut(node, prev, rel)) 
            {
                // ... and this node is "G" ...
                if (node.mTag.StartsWith("G") || (node.mTag.StartsWith("Pd") && rel == "dol" && prev != null && prev.mLemma == "biti" && prev.mTag.StartsWith("G")))
                {
                    // ... then the subtree starting at this node is VP
                    Set<ParseTreeNode> nodes = new Set<ParseTreeNode>();
                    node.CollectAll(nodes, tabu);
                    chunks.Add(new Chunk(ChunkType.VP, nodes, other));
                    return;
                }
                // ... and this node is "P", "R", "S", "K", or "Z" ...
                else if ((node.mTag.StartsWith("P") || node.mTag.StartsWith("R") || node.mTag.StartsWith("S") || node.mTag.StartsWith("K") || node.mTag.StartsWith("Z"))
                    && (rel != "dol" || (prev != null && prev.mLemma == "biti" && prev.mTag.StartsWith("G")))
                    )
                {
                    // ... then the subtree starting at this node is AP, AdjP, NP, or PP
                    // * PP: as soon as there's D in the subtree
                    // * AP: no D in the subtree & this node is R
                    // * AdjP: no D in the subtree & this node is P
                    // * NP: no D in the subtree & this node is S, K, or Z
                    Set<ParseTreeNode> nodes = new Set<ParseTreeNode>();
                    Set<ParseTreeNode> tabuCopy = tabu.Clone();
                    node.CollectAll(nodes, tabu);
                    ChunkType chunkType = ChunkType.NP;
                    if (node.mOutLinks.Any(x => x.Second.mTag.StartsWith("D")))
                    {
                        chunkType = ChunkType.PP;
                    }
                    else
                    {
                        if (node.mTag.StartsWith("R")) { chunkType = ChunkType.AP; }
                        else if (node.mTag.StartsWith("P") || node.mTag.StartsWith("Kv")) { chunkType = ChunkType.AdjP; }
                    }
                    chunks.Add(new Chunk(chunkType, nodes, other));
                    // find other potential chunks within this chunk
                    //CreateChunksDfsInner(node, tabuCopy, node, rel, tabuCopy.Clone(), chunks);
                    return;
                }
            }
            tabu.Remove(node);
        }

        private static ChunkType GetSingleNodeChunkType(ParseTreeNode node)
        {
            switch (node.mTag[0])
            {
                case 'S': // samostalnik
                    return ChunkType.NP;
                case 'G': // glagol
                    return ChunkType.VP;
                case 'P': // pridevnik
                    return ChunkType.AdjP;
                case 'R': // prislov
                    return ChunkType.AP;
                case 'Z': // zaimek
                    return ChunkType.NP;
                case 'K': // števnik
                    if (node.mTag.StartsWith("Kv")) { return ChunkType.AdjP; }
                    else { return ChunkType.NP; }
                case 'D': // predlog
                    return ChunkType.PP;
                case 'V': // veznik
                    return ChunkType.CON;
                case 'L': // členek
                    return ChunkType.AP;
                case 'M': // medmet
                    return ChunkType.Other;
                case 'O': // okrajšava
                    return ChunkType.NP;
                case 'N': // neuvrščeno
                    return ChunkType.Other;
                default:
                    return ChunkType.Other;
            }
        }

        public static ArrayList<Chunk> GetChunks(XmlDocument xmlDoc)
        {
            ArrayList<Chunk> chunks = new ArrayList<Chunk>();
            XmlNodeList nodes = xmlDoc.SelectNodes("//text/body//p/s");
            MultiSet<string> stats = new MultiSet<string>();
            foreach (XmlNode node in nodes) // for each sentence ...
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
#if TEST_CHUNKER
                w.WriteLine();
                foreach (XmlNode wordNode in node.SelectNodes("w | c")) 
                {
                    w.Write(wordNode.InnerText + " ");
                }
                w.WriteLine();
                w.WriteLine();
#endif
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
                    if (parseTreeNodes.ContainsKey(fromNodeId) && parseTreeNodes.ContainsKey(toNodeId)) // *** sometimes these things are punctuations - parser errors?
                    {
                        ParseTreeNode fromNode = parseTreeNodes[fromNodeId];
                        ParseTreeNode toNode = parseTreeNodes[toNodeId];
                        fromNode.mOutLinks.Add(new Pair<string, ParseTreeNode>(type, toNode));
                        toNode.mInLinks.Add(new Pair<string, ParseTreeNode>(type, fromNode));
                    }
                }
                ArrayList<Chunk> chunksThisSentence = new ArrayList<Chunk>();
                // find trees pointed to by "blue"
                Set<ParseTreeNode> tabu = new Set<ParseTreeNode>();
                foreach (ParseTreeNode treeNode in parseTreeNodes.Values.Where(x => x.mBlue))
                {
                    // find chunks in a DFS manner                                        
                    CreateChunksDfs(treeNode, /*prev=*/null, "modra", tabu, chunksThisSentence, /*other=*/false);
                    Set<ParseTreeNode> remainingNodes = new Set<ParseTreeNode>();
                    if (!tabu.Contains(treeNode)) // something still here ...
                    {
                        treeNode.CollectAll(remainingNodes, tabu);
                        chunksThisSentence.Add(new Chunk(GetSingleNodeChunkType(treeNode), remainingNodes, /*other=*/true));
                    }
                }
                // still something there?
                foreach (ParseTreeNode treeNode in parseTreeNodes.Values.Where(x => !tabu.Contains(x)))
                {
                    CreateChunksDfs(treeNode, /*prev=*/null, "modra", tabu, chunksThisSentence, /*other=*/true);
                    Set<ParseTreeNode> remainingNodes = new Set<ParseTreeNode>();
                    if (!tabu.Contains(treeNode)) 
                    {
                        treeNode.CollectAll(remainingNodes, tabu);
                        chunksThisSentence.Add(new Chunk(GetSingleNodeChunkType(treeNode), remainingNodes, /*other=*/true));
                    }
                }
                chunks.AddRange(chunksThisSentence);
#if TEST_CHUNKER
                // write chunks into text file
                ChunkType[] types = new ChunkType[] { ChunkType.VP, ChunkType.CON, ChunkType.NP, ChunkType.PP, ChunkType.AdjP, ChunkType.AP, /*ChunkType.Other,*/
                    ChunkType.Other_VP, ChunkType.Other_CON, ChunkType.Other_NP, ChunkType.Other_PP, ChunkType.Other_AdjP, ChunkType.Other_AP, ChunkType.Other_Other };
                foreach (ChunkType type in types)
                {
                    w.WriteLine(type + ":");
                    foreach (Chunk chunk in chunksThisSentence.Where(x => x.mType == type))
                    {
                        w.WriteLine("\t" + chunk + (chunk.mInner ? "*" : ""));
                    }
                }
#endif
            }
            return chunks;
        }
    }
}
