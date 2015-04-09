/*==========================================================================;
 *
 *  File:    DocumentSerializer.js
 *  Desc:    Creates a JSON-serializable representation of a Document
 *  Created: Apr-2013
 *
 *  Author:  Miha Grcar
 *
 *  License: MIT (http://opensource.org/licenses/MIT)
 *
 ***************************************************************************/

using System;
using System.Linq;
using System.Collections.Generic;
using System.Web;
using Latino;

namespace Latino.Workflows.TextMining
{
    /* .-----------------------------------------------------------------------
       |
       |  Class DocumentSerializer
       |
       '-----------------------------------------------------------------------
    */
    public static class DocumentSerializer
    {
        /* .-----------------------------------------------------------------------
           |
           |  Class AnnotationInfo
           |
           '-----------------------------------------------------------------------
        */
        private class AnnotationInfo
        {
            public int mIdx;
            public int mAnnotationId;
            public bool mIsSpanStart;
            public Annotation mAnnotation;
            public bool mIsLeaf;

            public AnnotationInfo(Annotation a, int id, bool isSpanStart, bool isLeaf)
            {
                mAnnotationId = id;
                mIsSpanStart = isSpanStart;
                mIdx = isSpanStart ? a.SpanStart : a.SpanEnd;
                mAnnotation = a;
                mIsLeaf = isLeaf;
            }
        }

        private static object SerializeState(Dictionary<int, Set<Annotation>> state)
        {
            ArrayList<object> stateSer = new ArrayList<object>();
            foreach (KeyValuePair<int, Set<Annotation>> stateItem in state.OrderByDescending(x => x.Key))
            {
                if (stateItem.Value.Sum(x => x.Features.Count()) > 0)
                {
                    ArrayList<object> featSer = new ArrayList<object>();
                    stateSer.Add(featSer);
                    featSer.Add(stateItem.Key);
                    int i = 1;
                    foreach (Annotation annot in stateItem.Value)
                    {
                        foreach (KeyValuePair<string, string> featInfo in annot.Features)
                        {
                            if (stateItem.Value.Count > 1)
                            {
                                featSer.Add("(" + i + ") " + featInfo.Key);
                            }
                            else
                            {
                                featSer.Add(featInfo.Key);
                            }
                            featSer.Add(featInfo.Value);
                        }
                        i++;
                    }
                }
                else { stateSer.Add(stateItem.Key); }
            }
            return stateSer;
        }

        private static string ProcessDocumentFeatureValue(string name, string val)
        {
            val = val.ToOneLine(/*compact=*/true);
            if (val.StartsWith("http://") || val.StartsWith("https://"))
            {
                return string.Format("<a target=\"_blank\" href=\"{0}\">{0}</a>", val, HttpUtility.HtmlEncode(val));
            }
            else if (val.Length > 400)
            {
                return HttpUtility.HtmlEncode(val.Truncate(400)) + "...";
            }
            return HttpUtility.HtmlEncode(val);
        }

        private static string ProcessAnnotationFeatureValue(string name, string val)
        {
            val = val.ToOneLine(/*compact=*/true);
            if (val.Length > 100)
            {
                return HttpUtility.HtmlEncode(val.Truncate(400)) + "...";
            }
            return HttpUtility.HtmlEncode(val);
        }

        public static void SerializeDocument(Document d, out ArrayList<object> treeItemsParam, out ArrayList<object> featuresParam, out ArrayList<object> contentParam)
        {
            Dictionary<string, int> idMapping = new Dictionary<string, int>();
            // GetId
            Func<string, int> GetId = delegate(string name)
            {
                int id;
                if (idMapping.TryGetValue(name, out id)) { return id; }
                idMapping.Add(name, id = idMapping.Count);
                return id;
            };
            ArrayList<AnnotationInfo> data = new ArrayList<AnnotationInfo>();
            Set<string> tabu = new Set<string>();
            ArrayList<Pair<ArrayList<int>, string>> treeItems = new ArrayList<Pair<ArrayList<int>, string>>();
            foreach (Annotation a in d.Annotations)
            {
                if (!tabu.Contains(a.Type))
                {
                    tabu.Add(a.Type);
                    string[] fullPath = a.Type.Split('/', '\\');
                    string path = "";
                    ArrayList<int> idList = new ArrayList<int>();
                    foreach (string pathItem in fullPath)
                    {
                        path += "/" + pathItem;
                        idList.Add(GetId(path));
                        if (!tabu.Contains(path))
                        {
                            tabu.Add(path);
                            treeItems.Add(new Pair<ArrayList<int>, string>(idList.Clone(), path));
                        }
                    }
                }
            }
            treeItems.Sort(delegate(Pair<ArrayList<int>, string> a, Pair<ArrayList<int>, string> b)
            {
                int n = Math.Min(a.First.Count, b.First.Count);
                for (int i = 0; i < n; i++)
                {
                    if (a.First[i] != b.First[i])
                    {
                        return a.First[i].CompareTo(b.First[i]);
                    }
                }
                if (a.First.Count > b.First.Count) { return 1; }
                else if (b.First.Count > a.First.Count) { return -1; }
                else { return 0; }
            });
            idMapping.Clear();
            treeItemsParam = new ArrayList<object>();
            foreach (Pair<ArrayList<int>, string> item in treeItems)
            {
                ArrayList<string> pathItems = new ArrayList<string>(item.Second.Split('/'));
                treeItemsParam.Add(new object[] { pathItems.Count - 1, pathItems.Last, GetId(item.Second) });
            }
            foreach (Annotation a in d.Annotations)
            {
                string path = "";
                string[] fullPath = a.Type.Split('/', '\\');
                for (int i = 0; i < fullPath.Length; i++)
                {
                    path += "/" + fullPath[i];
                    int pathId = GetId(path);
                    bool isLeaf = i == fullPath.Length - 1;
                    data.Add(new AnnotationInfo(a, pathId, /*isSpanStart=*/true, isLeaf));
                    data.Add(new AnnotationInfo(a, pathId, /*isSpanStart=*/false, isLeaf));
                }
            }
            data.Sort(delegate(AnnotationInfo a, AnnotationInfo b)
            {
                int c = a.mIdx.CompareTo(b.mIdx);
                if (c != 0) { return c; }
                return -a.mIsSpanStart.CompareTo(b.mIsSpanStart);
            });
            string text = d.Text;
            Dictionary<int, Set<Annotation>> state = new Dictionary<int, Set<Annotation>>();
            // AddToState
            Action<AnnotationInfo> AddToState = delegate(AnnotationInfo annotInfo)
            {
                Set<Annotation> annots;
                if (!state.TryGetValue(annotInfo.mAnnotationId, out annots))
                {
                    state.Add(annotInfo.mAnnotationId, annots = new Set<Annotation>());
                }
                if (annotInfo.mIsLeaf) { annots.Add(annotInfo.mAnnotation); }
            };
            // RemoveFromState
            Action<AnnotationInfo> RemoveFromState = delegate(AnnotationInfo annotInfo)
            {
                Set<Annotation> annots;
                if (state.TryGetValue(annotInfo.mAnnotationId, out annots))
                {
                    if (annotInfo.mIsLeaf) { annots.Remove(annotInfo.mAnnotation); }
                    if (annots.Count == 0)
                    {
                        state.Remove(annotInfo.mAnnotationId);
                    }
                }
            };
            featuresParam = new ArrayList<object>();
            foreach (KeyValuePair<string, string> f in d.Features)
            {
                string val = ProcessDocumentFeatureValue(f.Key, f.Value);
                if (val != null)
                {
                    featuresParam.Add(new string[] { f.Key, val });
                }
            }
            int cIdx = 0;
            contentParam = new ArrayList<object>();
            foreach (AnnotationInfo item in data)
            {
                if (item.mIsSpanStart)
                {
                    string part = text.Substring(cIdx, item.mIdx - cIdx);
                    if (part != "") { contentParam.Add(new object[] { part, SerializeState(state) }); }
                    cIdx = item.mIdx;
                    AddToState(item);
                }
                else
                {
                    string part = text.Substring(cIdx, item.mIdx - cIdx + 1);
                    if (part != "") { contentParam.Add(new object[] { part, SerializeState(state) }); }
                    cIdx = item.mIdx + 1;
                    RemoveFromState(item);
                }
            }
            if (text.Length - cIdx > 0)
            {
                contentParam.Add(new object[] { text.Substring(cIdx, text.Length - cIdx), SerializeState(state) });
            }
        }
    }
}