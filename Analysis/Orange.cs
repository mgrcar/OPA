using System.Text;
using System.IO;
using Latino;
using Latino.Model;

namespace Analysis
{
    public static class Orange
    {
        public static void SaveTab(string[] featureNames, LabeledDataset<string, SparseVector<double>> dataset, string fileName)
        {
            using (StreamWriter w = new StreamWriter(fileName, /*append=*/false, Encoding.ASCII))
            {
                for (int i = 0; i < featureNames.Length; i++)
                {
                    w.Write(featureNames[i] + "\t");
                }
                w.WriteLine("author");
                for (int i = 0; i < featureNames.Length; i++)
                {
                    w.Write("c\t");
                }
                w.WriteLine("d");
                for (int i = 0; i < featureNames.Length; i++)
                {
                    w.Write("\t");
                }
                w.WriteLine("class");
                foreach (LabeledExample<string, SparseVector<double>> lblEx in dataset)
                {
                    foreach (IdxDat<double> item in lblEx.Example)
                    {
                        w.Write(item.Dat + "\t");
                    }
                    w.WriteLine(lblEx.Label);
                }
            }
        }
    }
}