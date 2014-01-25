using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml;
using System.Text;
using Latino;
using Latino.Model;
using Latino.Model.Eval;
using PosTagger;

namespace Analysis
{
    public static class Program
    {
        static ChunkType MapChunkType(ChunkType chunkType)
        {
            if ((int)chunkType > 7) { chunkType = (ChunkType)((int)chunkType - 7); }
            if (chunkType == ChunkType.AdjP) { chunkType = ChunkType.NP; }
            return chunkType;
        }

        static void GetExtremes<T>(LabeledDataset<T, SparseVector<double>> dataset, out SparseVector<double> minValues, out SparseVector<double> maxValues)
        {
            minValues = new SparseVector<double>();
            maxValues = new SparseVector<double>();
            int maxIdx = ((IEnumerableList<SparseVector<double>>)dataset).Max(x => x.Max(y => y.Idx));
            for (int featureIdx = 0; featureIdx <= maxIdx; featureIdx++)
            {
                minValues[featureIdx] = ((IEnumerableList<SparseVector<double>>)dataset).Min(x => x[featureIdx]);
                maxValues[featureIdx] = ((IEnumerableList<SparseVector<double>>)dataset).Max(x => x[featureIdx]);
            }
        }

        static LabeledDataset<string, SparseVector<double>> CreateNormalizedDataset(LabeledDataset<BlogMetaData, SparseVector<double>> srcDataset, ClassType classType)
        {
            SparseVector<double> minValues, maxValues;
            GetExtremes(srcDataset, out minValues, out maxValues);
            LabeledDataset<string, SparseVector<double>> dataset = new LabeledDataset<string, SparseVector<double>>();
            ((IEnumerable<LabeledExample<BlogMetaData, SparseVector<double>>>)srcDataset).ToList()
                .ForEach(x => dataset.Add(new LabeledExample<string, SparseVector<double>>(AnalysisUtils.GetLabel(x.Label, classType),
                    new SparseVector<double>(
                        x.Example.Select(y => (y.Dat - minValues[y.Idx]) / (maxValues[y.Idx] - minValues[y.Idx])) // simple normalization
                        ))));
            return dataset;
        }

        static LabeledDataset<string, SparseVector<double>> CreateSingleFeatureDataset(LabeledDataset<BlogMetaData, SparseVector<double>> srcDataset, ClassType classType, int fIdx)
        {
            LabeledDataset<string, SparseVector<double>> dataset = new LabeledDataset<string, SparseVector<double>>();
            ((IEnumerable<LabeledExample<BlogMetaData, SparseVector<double>>>)srcDataset).ToList()
                .ForEach(x => dataset.Add(new LabeledExample<string, SparseVector<double>>(AnalysisUtils.GetLabel(x.Label, classType), new SparseVector<double>(new double[] { x.Example[fIdx] }))));
            return dataset;
        }

        class SingleFeatureSimilarity : ISimilarity<SparseVector<double>>
        {
            public double GetSimilarity(SparseVector<double> a, SparseVector<double> b)
            {
                double f1 = a[0];
                double f2 = b[0];
                return -Math.Abs(f1 - f2);
            }

            public void Save(BinarySerializer writer)
            {
                throw new NotImplementedException();
            }
        }

        static void Main(string[] args)
        {
            string[] featureNames = "ttr,brunet,honore,hl,ttrLemma,brunetLemma,honoreLemma,hlLemma,ari,flesch,fog,rWords,rChars,rSyllables,rComplex,M04,M05,M06,M07,M08,M09,M10,M11,M12,M13".Split(',');
            LabeledDataset<BlogMetaData, SparseVector<double>> dataset = new LabeledDataset<BlogMetaData, SparseVector<double>>();
            Console.WriteLine("Analiziram besedila...");
            foreach (string fileName in Directory.GetFiles(Config.DataFolder, "*.xml"))
            {
                // load XML
                Console.WriteLine("Datoteka {0}...", fileName);
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(File.ReadAllText(fileName).Replace("xmlns=\"http://www.tei-c.org/ns/1.0\"", ""));
                Corpus corpus = new Corpus();
                corpus.LoadFromXmlFile(fileName, /*tagLen=*/int.MaxValue);
#if TEST_CHUNKER
                Text text = null;
#else
                Text text = new Text(corpus, doc.SelectSingleNode("//header/naslov").InnerText, doc.SelectSingleNode("//header/blog").InnerText/*blog identifier is used as author identifier*/);
                text.ComputeFeatures(); // compute Detextive features
#endif
                // run chunker
                Console.WriteLine("Racunam znacilke...");
                ArrayList<Chunk> chunks = Chunker.GetChunks(doc);
                chunks = new ArrayList<Chunk>(chunks.Where(x => !x.mInner)); // get non-inner chunks only
                chunks.ForEach(x => x.mType = MapChunkType(x.mType)); // move chunks from Other_* to main categories
#if TEST_CHUNKER
                return;
#endif
                // get blog meta-data
                BlogMetaData metaData = new BlogMetaData();
                metaData.mAuthorAge = doc.SelectSingleNode("//header/avtorStarost").InnerText;
                metaData.mAuthorEducation = doc.SelectSingleNode("//header/avtorIzobrazba").InnerText;
                metaData.mAuthorGender = doc.SelectSingleNode("//header/avtorSpol").InnerText;
                metaData.mAuthorLocation = doc.SelectSingleNode("//header/avtorRegija").InnerText;
                metaData.mBlog = doc.SelectSingleNode("//header/blog").InnerText;
                // compute features M04-M13 from Stamatatos et al.: Automatic Text Categorization in Terms of Genre and Author (2000)
                double totalChunks = chunks.Count;
                double[] M = new double[10];
                double numNP = chunks.Count(x => x.mType == ChunkType.NP);
                double numVP = chunks.Count(x => x.mType == ChunkType.VP);
                double numAP = chunks.Count(x => x.mType == ChunkType.AP);
                double numPP = chunks.Count(x => x.mType == ChunkType.PP);
                double numCON = chunks.Count(x => x.mType == ChunkType.CON);
                if (totalChunks > 0)
                {
                    M[0] = numNP / totalChunks;
                    M[1] = numVP / totalChunks;
                    M[2] = numAP / totalChunks;
                    M[3] = numPP / totalChunks;
                    M[4] = numCON / totalChunks;
                }
                double numWordsNP = chunks.Where(x => x.mType == ChunkType.NP).Select(x => x.mItems.Count).Sum();
                M[5] = numNP == 0 ? 0 : (numWordsNP / numNP);
                double numWordsVP = chunks.Where(x => x.mType == ChunkType.VP).Select(x => x.mItems.Count).Sum();
                M[6] = numVP == 0 ? 0 : (numWordsVP / numVP);
                double numWordsAP = chunks.Where(x => x.mType == ChunkType.AP).Select(x => x.mItems.Count).Sum();
                M[7] = numAP == 0 ? 0 : (numWordsAP / numAP);
                double numWordsPP = chunks.Where(x => x.mType == ChunkType.PP).Select(x => x.mItems.Count).Sum();
                M[8] = numPP == 0 ? 0 : (numWordsPP / numPP);
                double numWordsCON = chunks.Where(x => x.mType == ChunkType.CON).Select(x => x.mItems.Count).Sum();
                M[9] = numCON == 0 ? 0 : (numWordsCON / numCON);
                // create dataset
                SparseVector<double> vec = new SparseVector<double>();
                int i = 0;
                foreach (string featureName in "ttr,brunet,honore,hl,ttrLemma,brunetLemma,honoreLemma,hlLemma,ari,flesch,fog,rWords,rChars,rSyllables,rComplex".Split(','))
                {
                    if (double.IsNaN(text.mFeatures[featureName]) || double.IsInfinity(text.mFeatures[featureName])) { vec[i++] = 0; }
                    else { vec[i++] = text.mFeatures[featureName]; }
                }
                foreach (double val in M)
                {
                    vec[i++] = val;
                }
                dataset.Add(new LabeledExample<BlogMetaData, SparseVector<double>>(metaData, vec));
                string htmlFileName = Config.HtmlFolder + "\\" + Path.GetFileNameWithoutExtension(fileName) + ".html";
                //Html.SaveHtml(featureNames, vec, doc, chunks, htmlFileName);
            }
            // save as Orange and Weka file            
            Console.WriteLine("Zapisujem datoteke Weka ARFF in Orange TAB...");
            foreach (ClassType classType in new ClassType[] { ClassType.AuthorName, ClassType.AuthorAge, ClassType.AuthorGender, ClassType.AuthorEducation, ClassType.AuthorLocation })
            {
                Weka.SaveArff(featureNames, dataset, classType, Config.OutputFolder + "\\" + string.Format("OPA-{0}.arff", classType));
                Orange.SaveTab(featureNames, dataset, classType, Config.OutputFolder + "\\" + string.Format("OPA-{0}.tab", classType));
            }
            //foreach (IdxDat<double> item in ReliefF.ComputeReliefF(dataset, dataset.Count, /*k=*/10).OrderByDescending(x => x.Dat))
            //{
            //    Console.WriteLine(featureNames[item.Idx] + " " + item.Dat);
            //}
            // evaluate features via classification
            Console.WriteLine("Evalviram znacilke s klasifikacijskimi modeli...");
            PerfData<string> perfData = new PerfData<string>();
            ArrayList<Pair<string, IModel<string>>> models = new ArrayList<Pair<string, IModel<string>>>();
            // create classifiers
            NearestCentroidClassifier<string> ncc = new NearestCentroidClassifier<string>();
            ncc.Similarity = new SingleFeatureSimilarity();
            models.Add(new Pair<string, IModel<string>>("NCC", ncc));
            //KnnClassifier<string, SparseVector<double>> knn = new KnnClassifier<string, SparseVector<double>>(new SingleFeatureSimilarity());
            //models.Add(new Pair<string, IModel<string>>("kNN", knn));
            //SvmMulticlassFast<string> svm = new SvmMulticlassFast<string>();
            //models.Add(new Pair<string, IModel<string>>("SVM", svm));
            MajorityClassifier<string, SparseVector<double>> backupCfy = new MajorityClassifier<string, SparseVector<double>>();
            foreach (Pair<string, IModel<string>> modelInfo in models) // iterate over different classifiers
            {
                Console.WriteLine("Kasifikacijski model: {0}...", modelInfo.First);
                foreach (ClassType classType in new ClassType[] { ClassType.AuthorName, ClassType.AuthorAge, ClassType.AuthorEducation, ClassType.AuthorGender, ClassType.AuthorLocation }) // iterate over different class types
                {
                    Console.WriteLine("Ciljni razred: {0}...", classType);
                    for (int fIdx = 0; fIdx < featureNames.Count(); fIdx++) // iterate over different features
                    {
                        Console.WriteLine("Znacilka: {0}...", featureNames[fIdx]);
                        LabeledDataset<string, SparseVector<double>> datasetWithSingleFeature = CreateSingleFeatureDataset(dataset, classType, fIdx);
                        datasetWithSingleFeature.Shuffle();
                        LabeledDataset<string, SparseVector<double>> trainSet, testSet;                        
                        for (int foldNum = 1; foldNum <= 10; foldNum++)
                        {
                            Console.WriteLine("Sklop " + foldNum + " / 10...");
                            datasetWithSingleFeature.SplitForCrossValidation(/*numFolds=*/10, foldNum, out trainSet, out testSet);
                            IModel<string> model = modelInfo.Second;
                            backupCfy.Train(trainSet);
                            // if there is only one class in trainSet, switch to MajorityClassifier
                            if (((IEnumerable<LabeledExample<string, SparseVector<double>>>)trainSet).Select(x => x.Label).Distinct().Count() == 1)
                            {
                                model = backupCfy;
                            }
                            else
                            {
                                model.Train(trainSet);
                            }
                            foreach (LabeledExample<string, SparseVector<double>> lblEx in testSet)
                            {
                                Prediction<string> pred = model.Predict(lblEx.Example);
                                if (pred.Count == 0) { pred = backupCfy.Predict(lblEx.Example); } // if the model is unable to make a prediction, use MajorityClassifier instead
                                perfData.GetPerfMatrix(classType.ToString(), modelInfo.First + "-" + featureNames[fIdx], foldNum).AddCount(lblEx.Label, pred.BestClassLabel);
                            }
                        }
                    }
                }
            }
#if TRAIN_FULL_MODELS
            // train full models
            Console.WriteLine("Treniram klasifikacijske modele...");
            SvmMulticlassFast<string> svmFull = new SvmMulticlassFast<string>();
            foreach (ClassType classType in new ClassType[] { ClassType.AuthorName, ClassType.AuthorAge, ClassType.AuthorEducation, ClassType.AuthorGender, ClassType.AuthorLocation }) // iterate over different class types
            {
                Console.WriteLine("Ciljni razred: {0}...", classType);
                LabeledDataset<string, SparseVector<double>> nrmDataset = CreateNormalizedDataset(dataset, classType);
                nrmDataset.Shuffle();
                LabeledDataset<string, SparseVector<double>> trainSet, testSet;
                for (int foldNum = 1; foldNum <= 10; foldNum++)
                {
                    Console.WriteLine("Sklop " + foldNum + " / 10...");
                    nrmDataset.SplitForCrossValidation(/*numFolds=*/10, foldNum, out trainSet, out testSet);
                    IModel<string> model = svmFull;
                    backupCfy.Train(trainSet);
                    // if there is only one class in trainSet, switch to MajorityClassifier
                    if (((IEnumerable<LabeledExample<string, SparseVector<double>>>)trainSet).Select(x => x.Label).Distinct().Count() == 1)
                    {
                        model = backupCfy;
                    }
                    else
                    {
                        model.Train(trainSet);
                    }
                    foreach (LabeledExample<string, SparseVector<double>> lblEx in testSet)
                    {
                        Prediction<string> pred = model.Predict(lblEx.Example);
                        if (pred.Count == 0) { pred = backupCfy.Predict(lblEx.Example); } // if the model is unable to make a prediction, use MajorityClassifier instead
                        perfData.GetPerfMatrix(classType.ToString(), "SVM-full", foldNum).AddCount(lblEx.Label, pred.BestClassLabel);
                    }
                }
                //break;
            }
#endif
            Console.WriteLine("*** Macro F1 ***");
            Console.WriteLine(perfData.ToString(null, PerfMetric.MacroF1));
            Console.WriteLine("*** Micro F1 ***");
            Console.WriteLine(perfData.ToString(null, PerfMetric.MicroF1));
            Console.WriteLine("*** Macro accuracy ***");
            Console.WriteLine(perfData.ToString(null, PerfMetric.MacroAccuracy)); // *** I think these numbers are wrong
            Console.WriteLine("*** Micro accuracy ***");
            Console.WriteLine(perfData.ToString(null, PerfMetric.MicroAccuracy));
            // all done
            Console.WriteLine("Koncano.");
        }
    }
}
