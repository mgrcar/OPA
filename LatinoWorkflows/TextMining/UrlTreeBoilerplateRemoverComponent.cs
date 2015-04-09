/*==========================================================================;
 *
 *  This file is part of LATINO. See http://latino.sf.net
 *
 *  File:    UrlTreeBoilerplateRemoverComponent.cs
 *  Desc:    Boilerplate remover component 
 *  Created: Apr-2012
 *
 *  Authors: Borut Sluban, Miha Grcar
 *
 *  License: MIT (http://opensource.org/licenses/MIT)
 *
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Latino.WebMining;
using System.Data.SqlClient;

namespace Latino.Workflows.TextMining
{
    /* .-----------------------------------------------------------------------
       |
       |  Class UrlTreeBoilerplateRemoverComponent
       |
       '-----------------------------------------------------------------------
    */
    public class UrlTreeBoilerplateRemoverComponent : DocumentProcessor
    {
        /* .-----------------------------------------------------------------------
           |
           |  Class UrlHistoryEntry
           |
           '-----------------------------------------------------------------------
        */
        private class UrlHistoryEntry
        {
            public string mUrlKey;
            public DateTime mTime;

            public UrlHistoryEntry(string urlKey, DateTime time)
            {
                mUrlKey = urlKey;
                mTime = time;
            }
        }

        /* .-----------------------------------------------------------------------
           |
           |  Class TextBlockHistoryEntry
           |
           '-----------------------------------------------------------------------
        */
        private class TextBlockHistoryEntry
        {
            public string mResponseUrl;
            public ArrayList<ulong> mHashCodes;
            public bool mFullPath;
            public DateTime mTime;
            public bool mDecDocCount;

            public TextBlockHistoryEntry(string responseUrl, ArrayList<ulong> hashCodes, bool fullPath, DateTime time, bool decDocCount)
            {
                mResponseUrl = responseUrl;
                mHashCodes = hashCodes;
                mFullPath = fullPath;
                mTime = time;
                mDecDocCount = decDocCount;
            }
        }

        /* .-----------------------------------------------------------------------
           |
           |  Enum HeuristicsType
           |
           '-----------------------------------------------------------------------
        */
        public enum HeuristicsType
        {
            Simple,
            Slow,
            Fast
        }

        private static Dictionary<string, Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>>> mUrlInfo
            = new Dictionary<string, Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>>>();
        private static Dictionary<string, Pair<UrlTree, Queue<TextBlockHistoryEntry>>> mTextBlockInfo
            = new Dictionary<string, Pair<UrlTree, Queue<TextBlockHistoryEntry>>>();

        private static UrlNormalizer mUrlNormalizer
            = new UrlNormalizer();
        private static Dictionary<string, object> mLocks
            = new Dictionary<string, object>();

        private static int mMinQueueSize // TODO: make configurable
            = 100;
        private static int mMaxQueueSize // TODO: make configurable
            = 10000;//20000;
        private static int mHistoryAgeDays // TODO: make configurable
            = 14;//30;

        private static int mMinNodeDocCount // TODO: make configurable
            = 5;
        private static HeuristicsType mHeuristicsType // TODO: make configurable
            = HeuristicsType.Slow;
        private static int mExactDuplicateThreshold // TODO: make configurable
            = 100 - 1;

        private static Set<string> mSkipTags
            = new Set<string>("script,noscript,style,form,fieldset,legend,label,input,button,select,datalist,optgroup,option,textarea,keygen,output,progress,meter,details,summary,menuitem,menu,img,iframe,embed,object,param,video,audio,source,track,canvas,map,area,svg,math,nav,aside,header,footer,address".Split(','));

        public UrlTreeBoilerplateRemoverComponent() : base(typeof(UrlTreeBoilerplateRemoverComponent))
        {
            mBlockSelector = "TextBlock";
        }

        private static object AcquireLock(string name)
        {
            lock (mLocks)
            { 
                object lockObj;
                if (!mLocks.TryGetValue(name, out lockObj))
                {
                    mLocks.Add(name, lockObj = new object());
                }
                return lockObj;
            }
        }

        private static string GetDomainName(string urlKey)
        {
            string domainName = urlKey.Split(':')[1].Trim('/');
            string tld = UrlNormalizer.GetTldFromDomainName(domainName);
            if (tld != null)
            {
                int c = tld.Split('.').Length + 1;
                string[] parts = domainName.Split('.');
                domainName = "";
                for (int i = parts.Length - 1; c > 0; c--, i--)
                {
                    domainName = parts[i] + "." + domainName;
                }
                domainName = domainName.TrimEnd('.');
            }
            return domainName;
        }

        private static Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>> GetUrlInfo(string domainName)
        {
            lock (mUrlInfo)
            {
                if (!mUrlInfo.ContainsKey(domainName))
                {
                    Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>> urlInfo = new Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>>(new Dictionary<string, Ref<int>>(), new Queue<UrlHistoryEntry>());
                    mUrlInfo.Add(domainName, urlInfo);
                    return urlInfo;
                }
                return mUrlInfo[domainName];
            }
        }

        private static void Remove(string urlKey, Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>> urlInfo)
        {
            UrlHistoryEntry entry;
            do
            {
                entry = urlInfo.Second.Dequeue();
                if (entry.mUrlKey != null) { urlInfo.First.Remove(entry.mUrlKey); }
            } 
            while (entry.mUrlKey != urlKey);
        }

        public static void InitializeHistory(string dbConnectionString)
        {
            Logger logger = Logger.GetLogger(typeof(UrlTreeBoilerplateRemoverComponent));
            logger.Info("InitializeHistory", "Loading history ...");
            mUrlInfo.Clear();
            mTextBlockInfo.Clear();
            int domainCount = 0;
            using (SqlConnection dbConnection = new SqlConnection(dbConnectionString))
            {
                dbConnection.Open();
                DataTable domainsTbl;
                using (SqlCommand sqlCmd = new SqlCommand(string.Format(@"
                    SELECT DISTINCT domainName FROM (
                        SELECT * FROM (SELECT TOP {0} domainName FROM Documents WHERE domainName IS NOT NULL GROUP BY domainName ORDER BY MAX(time) DESC) x 
                        UNION 
                        SELECT * FROM (SELECT TOP {0} domainName FROM Documents WHERE domainName IS NOT NULL GROUP BY domainName ORDER BY COUNT(*) DESC) y
                    ) xy", 3000/*make this configurable*/), dbConnection))
                {
                    domainsTbl = new DataTable();
                    using (SqlDataReader sqlReader = sqlCmd.ExecuteReader())
                    {
                        domainsTbl.Load(sqlReader);
                    }
                }
                foreach (DataRow row in domainsTbl.Rows)
                {
                    string domainName = (string)row["domainName"];
                    DataTable urlInfoTbl;
                    using (SqlCommand sqlCmd = new SqlCommand(string.Format(string.Format(@"
                        SELECT TOP {0} d.guid, d.time, d.responseUrl, d.urlKey, d.rev, d.domainName, (SELECT TOP 1 dd.rev from Documents dd WHERE dd.urlKey = d.urlKey ORDER BY dd.time DESC, dd.rev DESC) AS maxRev, tb.hashCodes FROM Documents d 
                        INNER JOIN TextBlocks tb ON d.guid = tb.docGuid WHERE d.domainName = @domainName ORDER BY d.time DESC
                        ", mMaxQueueSize)), dbConnection))
                    {
                        sqlCmd.AssignParams("domainName", domainName);
                        urlInfoTbl = new DataTable();
                        using (SqlDataReader sqlReader = sqlCmd.ExecuteReader())
                        {
                            urlInfoTbl.Load(sqlReader);
                        }
                    }
                    if (urlInfoTbl.Rows.Count == 0) { continue; }
                    Pair<UrlTree, Queue<TextBlockHistoryEntry>> textBlockInfo = GetTextBlockInfo(domainName);
                    DateTime then = (DateTime)urlInfoTbl.Rows[0]["time"] - new TimeSpan(mHistoryAgeDays, 0, 0, 0);
                    domainCount++;
                    Console.WriteLine("* " + domainName + string.Format(" ({0}/{1})", domainCount, domainsTbl.Rows.Count));
                    Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>> urlInfo = GetUrlInfo(domainName);
                    for (int j = urlInfoTbl.Rows.Count - 1; j >= 0; j--)
                    {
                        int rev = (int)urlInfoTbl.Rows[j]["rev"];
                        int maxRev = (int)urlInfoTbl.Rows[j]["maxRev"];
                        string urlKey = (string)urlInfoTbl.Rows[j]["urlKey"];
                        Guid docId = (Guid)urlInfoTbl.Rows[j]["guid"];
                        DateTime time = (DateTime)urlInfoTbl.Rows[j]["time"];
                        if (time >= then)
                        {
                            // URL cache
                            if (rev == 1)
                            {
                                if (urlInfo.First.ContainsKey(urlKey)) { Remove(urlKey, urlInfo); }
                                urlInfo.First.Add(urlKey, new Ref<int>(maxRev));
                                urlInfo.Second.Enqueue(new UrlHistoryEntry(urlKey, time));
                            }
                            else
                            {
                                urlInfo.Second.Enqueue(new UrlHistoryEntry(/*urlKey=*/null, time)); // dummy entry into the URL queue (to ensure sync with the text blocks queue)
                            }
                            // URL tree
                            //string hashCodesBase64 = (string)urlInfoTbl.Rows[j]["hashCodesBase64"];
                            string responseUrl = (string)urlInfoTbl.Rows[j]["responseUrl"];
                            //byte[] buffer = Convert.FromBase64String(hashCodesBase64);
                            byte[] buffer = (byte[])urlInfoTbl.Rows[j]["hashCodes"];
                            BinarySerializer memSer = new BinarySerializer(new MemoryStream(buffer));
                            ArrayList<ulong> hashCodes = new ArrayList<ulong>(memSer);
                            bool fullPath = urlKey.Contains("?");
                            TextBlockHistoryEntry entry = new TextBlockHistoryEntry(responseUrl, hashCodes, fullPath, time, /*decDocCount=*/rev == 1);
                            textBlockInfo.First.Insert(responseUrl, hashCodes, mMinNodeDocCount, fullPath, /*insertUnique=*/true, /*incDocCount=*/rev == 1);
                            textBlockInfo.Second.Enqueue(entry);
                        }
                    }
                }
            }
            logger.Info("InitializeHistory", "Loaded history for {0} distinct domains.", domainCount);
        }

        private void AddToUrlCache(string urlKey, DateTime time, Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>> urlInfo)
        {
            if (urlKey != null) { urlInfo.First.Add(urlKey, new Ref<int>(1)); }
            urlInfo.Second.Enqueue(new UrlHistoryEntry(urlKey, time));
        }

        private void AddToUrlTree(Pair<UrlTree, Queue<TextBlockHistoryEntry>> textBlockInfo, string responseUrl, ArrayList<ulong> hashCodes, bool fullPath, string corpusId,
            string documentId, string domainName, DateTime time, bool incDocCount)
        {
            UrlTree urlTree = textBlockInfo.First;
            Queue<TextBlockHistoryEntry> queue = textBlockInfo.Second;
            TextBlockHistoryEntry historyEntry = new TextBlockHistoryEntry(responseUrl, hashCodes, fullPath, time, /*decDocCount=*/incDocCount);
            urlTree.Insert(responseUrl, hashCodes, mMinNodeDocCount, fullPath, /*insertUnique=*/true, incDocCount);
            queue.Enqueue(historyEntry);         
        }

        private void RemoveItems(Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>> urlInfo, Pair<UrlTree, Queue<TextBlockHistoryEntry>> textBlockInfo, DateTime time)
        {
            double ageDays = 0;
            while (urlInfo.Second.Count > mMinQueueSize && ((ageDays = (time - urlInfo.Second.Peek().mTime).TotalDays) > (double)mHistoryAgeDays || urlInfo.Second.Count > mMaxQueueSize))
            {
                string rmvUrlKey = urlInfo.Second.Dequeue().mUrlKey;
                if (rmvUrlKey != null) { urlInfo.First.Remove(rmvUrlKey); }                
                TextBlockHistoryEntry oldestEntry = textBlockInfo.Second.Dequeue();
                textBlockInfo.First.Remove(oldestEntry.mResponseUrl, oldestEntry.mHashCodes, oldestEntry.mFullPath, /*unique=*/true, oldestEntry.mDecDocCount);
                mLogger.Info("RemoveItems", "Removed entry from URL tree. UrlKey={0} QueueSize={1} Age={2}", rmvUrlKey, urlInfo.Second.Count, ageDays);
            }
        }

        private static Pair<UrlTree, Queue<TextBlockHistoryEntry>> GetTextBlockInfo(string domainName)
        {
            lock (mTextBlockInfo)
            {
                if (!mTextBlockInfo.ContainsKey(domainName))
                {
                    Pair<UrlTree, Queue<TextBlockHistoryEntry>> textBlockInfo = new Pair<UrlTree, Queue<TextBlockHistoryEntry>>(new UrlTree(), new Queue<TextBlockHistoryEntry>());
                    mTextBlockInfo.Add(domainName, textBlockInfo);
                    return textBlockInfo;
                }
                return mTextBlockInfo[domainName];
            }
        }

        private static string GetPathInfo(UrlTree.NodeInfo[] result, int i)
        {
            string pathInfo = "";
            foreach (UrlTree.NodeInfo nodeInfo in result)
            {
                pathInfo += nodeInfo.UrlPart + ": " + nodeInfo.TextBlockCounts[i] + "/" + nodeInfo.NodeDocumentCount + ", ";
            }
            return pathInfo.TrimEnd(' ', ',');
        }

        private static Pair<bool, string> BpHeuristics(UrlTree.NodeInfo[] result, int i, HeuristicsType type)
        {
            if (type == HeuristicsType.Simple)
            {
                return result[0].TextBlockCounts[i] > 1 ? new Pair<bool, string>(true, null) : new Pair<bool, string>(false, null);
            }
            else
            {
                int voters = 0;
                foreach (UrlTree.NodeInfo nodeInfo in result)
                {
                    if ((nodeInfo.NodeLocation & UrlTree.NodeLocation.WithinTld) != 0 || (nodeInfo.NodeLocation & UrlTree.NodeLocation.Root) != 0) { break; }
                    voters++;
                }
                if (voters == 0) { voters = 1; }
                int bp = 0;
                int ct = 0;
                for (int j = 0; j < voters; j++)
                {
                    if (type == HeuristicsType.Slow)
                    {
                        if (result[j].TextBlockCounts[i] > ((result[j].NodeDocumentCount / 100) + 1)) { bp += 1; }
                        else { ct += 1; }
                    }
                    else if (type == HeuristicsType.Fast)
                    {
                        if (result[j].TextBlockCounts[i] > ((result[j].NodeDocumentCount / 50) + 1)) { bp += 1; }
                        else { ct += 1; }
                    }
                }
                if (bp == ct)
                {
                    string outStr = string.Format(@"{0} : {1}", ct, bp);
                    return type == HeuristicsType.Slow ? new Pair<bool, string>(true, outStr) : new Pair<bool, string>(false, outStr);
                }
                else
                {
                    string outStr = string.Format(@"{0} : {1}", ct, bp);
                    return bp > ct ? new Pair<bool, string>(true, outStr) : new Pair<bool, string>(false, outStr);
                }
            }
        }

        private static bool IsLink(string linkToTextRatioStr)
        {
            string[] parts = linkToTextRatioStr.Split('/');
            return parts[0] == parts[1]; // linkToTextRatio == 100%
        }

        private static void SetBlockAnnotation(Document doc, UrlTree.NodeInfo[] result, HeuristicsType hType, int i, string pathInfo, TextBlock textBlock)
        {
            UrlTree.NodeInfo firstNode = result[0];
            Pair<bool, string> heurResult = BpHeuristics(result, i, hType);
            Set<string> domPath = new Set<string>(textBlock.Annotation.Features.GetFeatureValue("domPath").Split('/'));
            if (heurResult.First || IsLink(textBlock.Annotation.Features.GetFeatureValue("linkToTextRatio")) || Set<string>.Intersection(domPath, mSkipTags).Count > 0)
            {
                textBlock.Annotation.Type = "TextBlock/Boilerplate";
            }
            else if (firstNode.TextBlockCounts[i] == 0)
            {
                textBlock.Annotation.Type = "TextBlock/Content/Unseen";
            }
            else
            {
                textBlock.Annotation.Type = "TextBlock/Content";
            }
            textBlock.Annotation.Features.SetFeatureValue("bprNodeBlockCount", firstNode.TextBlockCounts[i].ToString());
            textBlock.Annotation.Features.SetFeatureValue("bprNodeLocation", firstNode.NodeLocation.ToString());
            textBlock.Annotation.Features.SetFeatureValue("bprNodeDocumentCount", firstNode.NodeDocumentCount.ToString());
            textBlock.Annotation.Features.SetFeatureValue("bprUrlPart", firstNode.UrlPart);
            textBlock.Annotation.Features.SetFeatureValue("bprPathInfo", pathInfo);
            if (hType != HeuristicsType.Simple)
            {
                textBlock.Annotation.Features.SetFeatureValue("bprContentVsBoileplateVotes", heurResult.Second);
            }
        }

        public/*protected*/ override object ProcessData(IDataProducer sender, object data)
        {
            DocumentCorpus corpus = (DocumentCorpus)data;            
            try
            {
                // split corpus according to document domain names
                Dictionary<string, ArrayList<Document>> domainDocCollections = new Dictionary<string, ArrayList<Document>>();
                foreach (Document document in corpus.Documents)
                {
                    try
                    {
                        string responseUrl = document.Features.GetFeatureValue("responseUrl");
                        if (responseUrl == null) { continue; }
                        bool blacklisted;
                        string urlKey = mUrlNormalizer.NormalizeUrl(responseUrl, document.Name, out blacklisted, UrlNormalizer.NormalizationMode.Heuristics);
                        document.Features.SetFeatureValue("blacklisted", blacklisted.ToString());
                        document.Features.SetFeatureValue("urlKey", urlKey);
                        string domainName = GetDomainName(urlKey);
                        document.Features.SetFeatureValue("domainName", domainName);
                        ArrayList<Document> domainDocs;
                        if (!domainDocCollections.TryGetValue(domainName, out domainDocs))
                        {
                            domainDocCollections.Add(domainName, domainDocs = new ArrayList<Document>());
                        }
                        domainDocs.Add(document);
                    }
                    catch (Exception exception)
                    {
                        mLogger.Error("ProcessData (ProcessDocument)", exception);
                    }
                }
                // lock and process each domain separately 
                foreach (KeyValuePair<string, ArrayList<Document>> domainInfo in domainDocCollections)
                {
                    string domainName = domainInfo.Key;
                    Pair<Dictionary<string, Ref<int>>, Queue<UrlHistoryEntry>> urlInfo = GetUrlInfo(domainName);
                    Pair<UrlTree, Queue<TextBlockHistoryEntry>> textBlockInfo = GetTextBlockInfo(domainName);
                    lock (AcquireLock(domainName)) // domain lock acquired
                    {
                        DateTime maxTime = DateTime.MinValue;
                        // detect duplicates
                        foreach (Document document in domainInfo.Value)
                        {
                            try
                            {
                                DateTime time = DateTime.Parse(document.Features.GetFeatureValue("time"));
                                if (time > maxTime) { maxTime = time; }
                                string urlKey = document.Features.GetFeatureValue("urlKey");
                                bool cached = urlInfo.First.ContainsKey(urlKey);
                                document.Features.SetFeatureValue("rev", "1");
                                if (cached) 
                                {
                                    Ref<int> revInfo = urlInfo.First[urlKey];
                                    revInfo.Val++;
                                    document.Features.SetFeatureValue("rev", revInfo.Val.ToString());
                                    continue; 
                                }
                                AddToUrlCache(urlKey, time, urlInfo);
                            }
                            catch (Exception exception)
                            {
                                mLogger.Error("ProcessData (ProcessDocument)", exception);
                            }
                        }
                        // populate URL tree
                        ArrayList<ArrayList<ulong>> corpusHashCodes = new ArrayList<ArrayList<ulong>>();
                        foreach (Document document in domainInfo.Value)
                        {
                            try
                            {
                                string contentType = document.Features.GetFeatureValue("contentType");
                                if (contentType != "Text") { continue; }
                                string docUrl = document.Features.GetFeatureValue("responseUrl");
                                string urlKey = document.Features.GetFeatureValue("urlKey");
                                TextBlock[] blocks = document.GetAnnotatedBlocks(mBlockSelector);
                                ArrayList<ulong> hashCodes = new ArrayList<ulong>();
                                for (int i = 0; i < blocks.Length; i++)
                                {
                                    TextBlock block = blocks[i];
                                    hashCodes.Add(UrlTree.ComputeHashCode(block.Text, /*alphaOnly=*/true));
                                    block.Annotation.Features.SetFeatureValue("hash", hashCodes.Last.ToString());
                                }
                                if (document.Features.GetFeatureValue("rev") == "1")
                                {
                                    bool fullPath = urlKey.Contains("?");
                                    string documentId = document.Features.GetFeatureValue("guid").Replace("-", "");
                                    string corpusId = corpus.Features.GetFeatureValue("guid").Replace("-", "");
                                    AddToUrlTree(textBlockInfo, docUrl, hashCodes, fullPath, corpusId, documentId, domainName, DateTime.Parse(document.Features.GetFeatureValue("time")), /*incDocCount=*/true);
                                }
                                corpusHashCodes.Add(hashCodes);
                            }
                            catch (Exception exception)
                            {
                                mLogger.Error("ProcessData (ProcessDocument)", exception);
                            }
                        }
                        // annotate boilerplate
                        int docIdx = 0;
                        foreach (Document document in domainInfo.Value)
                        {
                            try
                            {
                                string contentType = document.Features.GetFeatureValue("contentType");
                                if (contentType != "Text") { continue; }
                                string docUrl = document.Features.GetFeatureValue("responseUrl");
                                string urlKey = document.Features.GetFeatureValue("urlKey");                                
                                Ref<int> revInfo = urlInfo.First[urlKey]; 
                                TextBlock[] blocks = document.GetAnnotatedBlocks(mBlockSelector);
                                ArrayList<ulong> hashCodes = corpusHashCodes[docIdx++]; // document's hash codes
                                UrlTree urlTree = GetTextBlockInfo(domainName).First;
                                UrlTree.NodeInfo[] result = urlTree.Query(docUrl, hashCodes, mMinNodeDocCount, /*fullPath=*/urlKey.Contains("?"));
                                int bpCharCount = 0, contentCharCount = 0, unseenContentCharCount = 0;
                                ArrayList<ulong> unseenContentHashCodes = new ArrayList<ulong>();
                                for (int i = 0; i < blocks.Length; i++)
                                {
                                    TextBlock block = blocks[i];
                                    string pathInfo = GetPathInfo(result, i);
                                    SetBlockAnnotation(document, result, mHeuristicsType, i, pathInfo, block);
                                    if (block.Annotation.Type == "TextBlock/Boilerplate") { bpCharCount += block.Text.Length; }
                                    else { contentCharCount += block.Text.Length; }
                                    if (block.Annotation.Type == "TextBlock/Content/Unseen")
                                    {
                                        unseenContentCharCount += block.Text.Length;
                                        unseenContentHashCodes.Add(hashCodes[i]);
                                    }
                                }
                                document.Features.SetFeatureValue("bprBoilerplateCharCount", bpCharCount.ToString());
                                document.Features.SetFeatureValue("bprContentCharCount", contentCharCount.ToString());
                                if (document.Features.GetFeatureValue("rev") != "1")
                                {
                                    document.Features.SetFeatureValue("unseenContentCharCount", unseenContentCharCount.ToString());
                                    if (unseenContentCharCount > mExactDuplicateThreshold)
                                    {
                                        document.Features.SetFeatureValue("unseenContent", "Yes");
                                        string documentId = document.Features.GetFeatureValue("guid").Replace("-", "");
                                        string corpusId = corpus.Features.GetFeatureValue("guid").Replace("-", "");
                                        DateTime time = DateTime.Parse(document.Features.GetFeatureValue("time"));
                                        AddToUrlTree(textBlockInfo, docUrl, unseenContentHashCodes, /*fullPath=*/urlKey.Contains("?"), corpusId, documentId, domainName, time, /*incDocCount=*/false);
                                        AddToUrlCache(/*urlKey=*/null, time, urlInfo); // dummy entry into the URL queue (to ensure sync with the text blocks queue)
                                    }
                                    else
                                    {
                                        document.Features.SetFeatureValue("unseenContent", "No");
                                    }
                                }
                            }
                            catch (Exception exception)
                            {
                                mLogger.Error("ProcessData (ProcessDocument)", exception);
                            }
                        }
                        if (maxTime != DateTime.MinValue)
                        {
                            RemoveItems(urlInfo, textBlockInfo, maxTime);
                        }
                    } // domain lock released
                }
            }
            catch (Exception exception)
            {
                mLogger.Error("ProcessData", exception);
            }
            return corpus;
        }
    }
}