using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Latino;
using Latino.Model;

namespace Analysis
{
    public static class Weka
    {
        public static void SaveWekaArff(string[] featureNames, LabeledDataset<string, SparseVector<double>> dataset, string fileName)
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
                ArrayList<string> classes = new ArrayList<string>(((IEnumerable<LabeledExample<string, SparseVector<double>>>)dataset).Select(x => x.Label).Distinct());
                w.WriteLine(classes.ToString().Replace("( ", "{").Replace(" )", "}").Replace(" ", ","));
                w.WriteLine();
                w.WriteLine("@DATA");
                foreach (LabeledExample<string, SparseVector<double>> lblEx in dataset)
                {
                    foreach (IdxDat<double> item in lblEx.Example)
                    {
                        w.Write(item.Dat + ",");
                    }
                    w.WriteLine(lblEx.Label);
                }
            }
        }
    }
}
