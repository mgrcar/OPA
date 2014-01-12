using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Collections.Generic;
using System.IO;
using Latino;
using Latino.Model;
using Latino.TextMining;
using PosTagger;

namespace OPA // taken from Detextive
{
    public class Token
    {
        public string mTokenStr;
        public string mLemma;
        public string mTag;
        public string mTagReduced;
        public bool mIsPunctuation
            = false;
        public bool mIsFollowedBySpace
            = false;
    }

    public class Sentence
    {
        public ArrayList<Token> mTokens
            = new ArrayList<Token>();
    }    

    public class Author
    {
        public string mName;
        public ArrayList<Text> mTexts
            = new ArrayList<Text>();
        public Dictionary<string, ArrayList<double>> mFeatures
            = new Dictionary<string, ArrayList<double>>();
        public Dictionary<string, SparseVector<double>> mFeatureVectors
            = new Dictionary<string, SparseVector<double>>();
        public Dictionary<string, Prediction<string>> mPredictions
            = new Dictionary<string, Prediction<string>>();
        public bool mIsTagged
            = false;

        public Author(string name)
        {
            mName = name;
        }

        public void ComputeFeatures()
        {
            foreach (Text text in mTexts)
            {
                text.ComputeFeatures();
                foreach (KeyValuePair<string, double> feature in text.mFeatures)
                {
                    AddFeatureVal(feature.Key, feature.Value);
                }
            }
            foreach (string fvName in mTexts[0].mFeatureVectors.Keys)
            {
                ArrayList<SparseVector<double>> tmp = new ArrayList<SparseVector<double>>();
                foreach (Text text in mTexts)
                {
                    tmp.Add(text.mFeatureVectors[fvName]);
                }
                mFeatureVectors.Add(fvName, ModelUtils.ComputeCentroid(tmp, CentroidType.NrmL2));
            }
        }

        public void AddFeatureVal(string featureName, double val)
        {
            ArrayList<double> values;
            if (!mFeatures.TryGetValue(featureName, out values))
            {
                mFeatures.Add(featureName, new ArrayList<double>(new double[] { val }));
            }
            else
            {
                values.Add(val);
            }
        }

        public Pair<string, double>[] GetTopVectorItems(string vectorName, int n, BowSpace bowSpc)
        {
            SparseVector<double> vec = mFeatureVectors[vectorName];
            return vec
                .OrderByDescending(x => x.Dat)
                .Take(n)
                .Select(x => new Pair<string, double>(bowSpc.Words[x.Idx].Stem, x.Dat))
                .ToArray();
        }

        public double GetAvg(string featureName)
        {
            return mFeatures[featureName].Average();
        }

        public double GetStdDev(string featureName)
        {
            return mFeatures[featureName].StdDev();
        }

        public void ComputeDistance(Author otherAuthor, out Dictionary<string, double> val, out Dictionary<string, double> stdDev, IEnumerable<string> featureNames)
        {
            val = new Dictionary<string, double>();
            stdDev = new Dictionary<string, double>();
            foreach (string featureName in featureNames)
            {
                if (mFeatures.ContainsKey(featureName))
                {
                    double avg = GetAvg(featureName);
                    double var = Math.Pow(GetStdDev(featureName), 2);
                    double otherAvg = otherAuthor.GetAvg(featureName);
                    double otherVar = Math.Pow(otherAuthor.GetStdDev(featureName), 2);
                    val.Add(featureName, Math.Abs(avg - otherAvg));
                    stdDev.Add(featureName, Math.Sqrt(var + otherVar)); // http://stattrek.com/random-variable/combination.aspx
                }
                else
                {
                    Prediction<string> p = mPredictions[featureName];
                    try { val.Add(featureName, p.First(x => x.Dat == otherAuthor.mName).Key); }
                    catch { val.Add(featureName, -666); }
                }
            }
        }
    }

    public class Text
    {
        public static Dictionary<string, string> mTagMapping
            = new Dictionary<string, string>();
        public string mName;
        public string mAuthor;
        public ArrayList<Sentence> mSentences
            = new ArrayList<Sentence>();
        public Dictionary<string, double> mFeatures
            = new Dictionary<string, double>();
        public Dictionary<string, SparseVector<double>> mFeatureVectors
            = new Dictionary<string, SparseVector<double>>();
        public string mHtmlFileName
            = Guid.NewGuid().ToString("N") + ".html";
        public bool mIsTagged
            = false;

        static Text()
        {
            using (Stream s = Utils.GetManifestResourceStream(typeof(Token), "TagMapping.txt"))
            {
                using (StreamReader r = new StreamReader(s))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        string[] mapping = line.Split('\t');
                        mTagMapping.Add(mapping[0], mapping[1]);
                    }
                }
            }
        }

        public Text(Corpus corpus, string name, string author)
        {
            mName = name;
            mAuthor = author;
            Sentence sentence = new Sentence();
            foreach (TaggedWord taggedWord in corpus.TaggedWords)
            {
                string tag = taggedWord.Tag;
                Token token = new Token();
                if (tag != null && tag.EndsWith("<eos>"))
                {
                    tag = tag.Substring(0, tag.Length - 5);
                }
                if (taggedWord.MoreInfo.Punctuation)
                {
                    token.mIsPunctuation = true;
                    token.mTokenStr = token.mLemma = token.mTag = token.mTagReduced = taggedWord.Word;
                }
                else // word
                {
                    token.mTokenStr = taggedWord.Word;
                    token.mLemma = taggedWord.Lemma;
                    token.mTag = tag;
                    token.mTagReduced = mTagMapping[tag];
                }
                if (taggedWord.MoreInfo.FollowedBySpace)
                {
                    token.mIsFollowedBySpace = true;
                }
                sentence.mTokens.Add(token);
                if (taggedWord.MoreInfo.EndOfSentence)
                {
                    mSentences.Add(sentence); 
                    sentence = new Sentence();
                }
            }
            if (sentence.mTokens.Count > 0) 
            { 
                mSentences.Add(sentence); 
            }
        }

        public void ComputeFeatures()
        {
            double ttr, hl, honore, brunet;
            Features.GetVocabularyRichness(this, out ttr, out hl, out honore, out brunet, /*lemmas=*/false);
            double ttrLemma, hlLemma, honoreLemma, brunetLemma;
            Features.GetVocabularyRichness(this, out ttrLemma, out hlLemma, out honoreLemma, out brunetLemma, /*lemmas=*/true);
            double ari, flesch, fog, rWords, rChars, rSyllables, rComplex;
            Features.GetReadabilityFeatures(this, out ari, out flesch, out fog, out rWords, out rChars, out rSyllables, out rComplex);
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
        }

        public string GetHtml()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Sentence sentence in mSentences)
            {
                sb.Append("<span class='sentence'>");
                foreach (Token token in sentence.mTokens)
                {
                    if (!token.mIsPunctuation)
                    {
                        sb.Append(string.Format("<span class='token' data-toggle='tooltip' title='Oznaka: {0}&lt;br&gt;Kratka oznaka: {1}&lt;br&gt;Lema: {2}'>",
                            HttpUtility.HtmlEncode(token.mTag).Replace("'", "&#39;"),
                            HttpUtility.HtmlEncode(token.mTagReduced).Replace("'", "&#39;"), 
                            HttpUtility.HtmlEncode(token.mLemma).Replace("'", "&#39;")));
                    }
                    else
                    {
                        sb.Append(string.Format("<span class='token'>"));
                    }
                    sb.Append(HttpUtility.HtmlEncode(token.mTokenStr));
                    sb.Append("</span>");
                    if (token.mIsFollowedBySpace) { sb.Append(" "); }
                }
                sb.Append("</span>");
            }
            return sb.ToString();
        }
    }
}
