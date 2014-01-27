//
// taken from Detextive
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Latino;

namespace OPA.Analysis 
{
    public static class Features
    {
        private static Set<char> mVowels
            = new Set<char>("íéêáôóúìèàòùieaouÍÉÊÁÔÓÚÌÈÀÒÙIEAOU".ToCharArray()); 
        
        private static int CountSyllables(string word) 
        {
            int c = 0;
            int i = 0;
            foreach (char ch in word)
            {
                if (mVowels.Contains(ch)) { c++; }
                else if ((char.ToLower(ch) == 'r') && (i - 1 < 0 || !mVowels.Contains(word[i - 1])) && (i + 1 >= word.Length || !mVowels.Contains(word[i + 1]))) { c++; }
                i++;
            }
            return c;
        }

        // http://www.ideosity.com/ourblog/post/ideosphere-blog/2010/01/14/readability-tests-and-formulas
        public static void GetReadabilityFeatures(Text txt, out double ari, out double flesch, out double fog, out double rWords, out double rChars, out double rSyllables, out double rComplex)
        {
            int numWords = 0;
            int numSentences = 0;
            int numChars = 0;
            int numSyllables = 0;
            int numComplexWords = 0;
            foreach (Sentence stc in txt.mSentences)
            {
                int numWordsThis = 0;
                int numCharsThis = 0;
                int numSyllablesThis = 0;
                int numComplexWordsThis = 0;
                foreach (Token tkn in stc.mTokens)
                {
                    if (!tkn.mIsPunctuation)
                    {
                        numWordsThis++;
                        numCharsThis += tkn.mTokenStr.Length;
                        int tmp = CountSyllables(tkn.mTokenStr);
                        numSyllablesThis += tmp;
                        if (tmp > 2 && !tkn.mTag.StartsWith("Sl")) { numComplexWordsThis++; } // NTH: ignore typical suffixes when counting syllables, handle compound words
                    }
                }
                if (numWordsThis > 0) { numSentences++; }
                numWords += numWordsThis;
                numChars += numCharsThis;
                numSyllables += numSyllablesThis;
                numComplexWords += numComplexWordsThis;
            }
            rWords = numSentences == 0 ? 0.0 : ((double)numWords / (double)numSentences);
            rChars = numWords == 0 ? 0.0 : ((double)numChars / (double)numWords);
            rSyllables = numWords == 0 ? 0.0 : ((double)numSyllables / (double)numWords);
            rComplex = numWords == 0 ? 0.0 : ((double)numComplexWords / (double)numWords);
            ari = 0.5 * rWords + 4.71 * rChars - 21.43;
            flesch = 206.835 - 1.015 * rWords - 84.6 * rSyllables;
            fog = 0.4 * (rWords + 100.0 * rComplex);
        }

        public static void GetVocabularyRichness(Text text, out double ttr, out double hl, out double honore, out double brunet, bool lemmas)
        {
            // type-token ratio (TTR)
            MultiSet<string> tokens = new MultiSet<string>();          
            int n = 0;
            foreach (Sentence sentence in text.mSentences)
            {
                foreach (Token token in sentence.mTokens)
                {
                    if (!token.mIsPunctuation) 
                    {
                        if (lemmas) { tokens.Add(token.mLemma.ToLower()); }
                        else { tokens.Add(token.mTokenStr.ToLower()); }
                        n++;
                    }
                }
            }
            int v = tokens.CountUnique;
            ttr = (double)v / (double)n;
            // hapax legomena
            int v1 = tokens.ToList().Count(x => x.Key == 1);
            hl = (double)v1 / (double)n;
            // Honore's statistic: R = 100 x log(N) / (1 - V1 / V)
            honore = 100.0 * Math.Log(n) / (1.0 - (double)v1 / (double)v);
            // Brunet's index: W = N^(V^-0.165)
            brunet = Math.Pow(n, Math.Pow(v, -0.165));
        }

        public static void GetFeatureRanking(Author author, ArrayList<Author> otherAuthors, string feature)
        {
            ArrayList<double> diffsAuthor = new ArrayList<double>();
            ArrayList<double> diffsOthers = new ArrayList<double>();
            for (int i = 0; i < otherAuthors.Count; i++)
            {
                for (int j = i + 1; j < otherAuthors.Count; j++)
                {
                    Author a = otherAuthors[i];
                    Author oa = otherAuthors[j];
                    if (!a.mIsTagged && !oa.mIsTagged)
                    {
                        foreach (double aVal in a.mFeatures[feature])
                        {
                            foreach (double oaVal in oa.mFeatures[feature])
                            {
                                if (a != author && oa != author)
                                {
                                    diffsOthers.Add(Math.Abs(aVal - oaVal));
                                }
                                else
                                {
                                    diffsAuthor.Add(Math.Abs(aVal - oaVal));
                                }
                            }
                        }
                    }
                }
            }
            foreach (double diffAuthor in diffsAuthor)
            { 
                // *** this could be optimized (sorted array, bisection)
                double p = (double)diffsOthers.Count(x => x <= diffAuthor) / (double)diffsOthers.Count();
                author.AddFeatureVal("p_" + feature, p);
            }
        }
    }
}
