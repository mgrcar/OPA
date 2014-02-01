using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Latino;
using Latino.Model;

namespace OPA.Analysis
{
    public static class Weka
    {
        public static void SaveArff(string[] featureNames, LabeledDataset<BlogMetaData, SparseVector<double>> dataset, ClassType classType, string fileName)
        {
            using (StreamWriter w = new StreamWriter(fileName, /*append=*/false, Encoding.ASCII))
            {
                w.WriteLine("@RELATION r" + Guid.NewGuid().ToString("N"));
                w.WriteLine();
                foreach (string featureName in featureNames)
                {
                    w.WriteLine("@ATTRIBUTE " + featureName + " NUMERIC");
                }
                w.Write("@ATTRIBUTE class ");
                ArrayList<string> classes = new ArrayList<string>();
                ((IEnumerable<LabeledExample<BlogMetaData, SparseVector<double>>>)dataset).ToList().ForEach(
                    x => classes.AddRange(AnalysisUtils.GetLabel(x.Label, classType).Split(',')));
                classes = new ArrayList<string>(classes.Distinct());
                w.WriteLine(classes.ToString().Replace("( ", "{").Replace(" )", "}").Replace(" ", ","));
                w.WriteLine();
                w.WriteLine("@DATA");
                foreach (LabeledExample<BlogMetaData, SparseVector<double>> lblEx in dataset)
                {
                    foreach (string lblStr in AnalysisUtils.GetLabel(lblEx.Label, classType).Split(','))
                    {
                        if (lblStr != "")
                        {
                            foreach (IdxDat<double> item in lblEx.Example)
                            {
                                w.Write(item.Dat + ",");
                            }
                            w.WriteLine(lblStr);
                        }
                    }
                }
            }
        }
    }
}
