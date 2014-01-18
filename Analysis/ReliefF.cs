using System;
using System.Collections.Generic;
using System.Linq;
using Latino;
using Latino.Model;

namespace Analysis
{
    public static class ReliefF
    {
        private static void GetExtremes(LabeledDataset<string, SparseVector<double>> dataset, out SparseVector<double> minValues, out SparseVector<double> maxValues) 
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

        private static double Diff(SparseVector<double> a, SparseVector<double> b, SparseVector<double> minValues, SparseVector<double> maxValues) 
        {
            Utils.ThrowException(a.LastNonEmptyIndex != b.LastNonEmptyIndex 
                || a.LastNonEmptyIndex != minValues.LastNonEmptyIndex 
                || a.LastNonEmptyIndex != maxValues.LastNonEmptyIndex ? new ArgumentException() : null);
            double diffSum = 0;
            for (int featureIdx = 0; featureIdx <= a.LastNonEmptyIndex; featureIdx++)
            {
                diffSum += Diff(featureIdx, a, b, minValues, maxValues);
            }
            return diffSum;
        }

        private static double Diff(int featureIdx, SparseVector<double> a, SparseVector<double> b, SparseVector<double> minValues, SparseVector<double> maxValues) 
        {
            return Math.Abs(a[featureIdx] - b[featureIdx]) / (maxValues[featureIdx] - minValues[featureIdx]);
        }

        private static double ClassProbability(LabeledDataset<string, SparseVector<double>> dataset, string label) 
        {
            return (double)((IEnumerable<LabeledExample<string, SparseVector<double>>>)dataset).Count(x => x.Label == label) / (double)dataset.Count; 
        }

        public static SparseVector<double> ComputeReliefF(LabeledDataset<string, SparseVector<double>> dataset, int m) 
        {
            return ComputeReliefF(dataset, m, /*k=*/5);
        }

        public static SparseVector<double> ComputeReliefF(LabeledDataset<string, SparseVector<double>> dataset) 
        {
            return ComputeReliefF(dataset, /*m=*/100, /*k=*/5); // these defaults are from Orange
        }

        // This algorithm is taken from Marko Robnik-Sikonja, Igor Kononenko: Theoretical and Empirical Analysis of ReliefF and RReliefF (2003).
        // It doesn't give the same output as Orange's ReliefF. It does however give the same output as Weka's ReliefF.
        public static SparseVector<double> ComputeReliefF(LabeledDataset<string, SparseVector<double>> dataset, int m, int k)
        {
            SparseVector<double> w = new SparseVector<double>();
            SparseVector<double> minValues, maxValues;
            GetExtremes(dataset, out minValues, out maxValues);
            for (int i = 0; i <= minValues.LastNonEmptyIndex; i++) { w[i] = 0; }
            dataset.Shuffle(new Random(1)); // *** shuffle the dataset
            for (int i = 0; i < m; i++)
            {
                // randomly select an instance Ri
                LabeledExample<string, SparseVector<double>> Ri = dataset[i];
                // find k nearest hits Hj
                IEnumerable<LabeledExample<string, SparseVector<double>>> allHits = ((IEnumerable<LabeledExample<string, SparseVector<double>>>)dataset)
                    .Where(x => x.Label == Ri.Label && x.Example != Ri.Example);
                List<LabeledExample<string, SparseVector<double>>> H = allHits
                    .OrderBy(x => Diff(Ri.Example, x.Example, minValues, maxValues))
                    .Take(k) // *** what if there's not enough instances available???
                    .ToList();
                // for each class C /= class(Ri) do ...
                Dictionary<string, List<LabeledExample<string, SparseVector<double>>>> M = new Dictionary<string, List<LabeledExample<string, SparseVector<double>>>>();
                foreach (string C in ((IEnumerable<LabeledExample<string, SparseVector<double>>>)dataset)
                    .Select(x => x.Label)
                    .Distinct()
                    .Where(x => x != Ri.Label))
                {
                    // from class C find k nearest misses Mj(C)
                    IEnumerable<LabeledExample<string, SparseVector<double>>> allMissesFromC = ((IEnumerable<LabeledExample<string, SparseVector<double>>>)dataset)
                        .Where(x => x.Label == C);
                    List<LabeledExample<string, SparseVector<double>>> MC = allMissesFromC
                        .OrderBy(x => Diff(Ri.Example, x.Example, minValues, maxValues))
                        .Take(k) // *** what if there's not enough instances available???
                        .ToList();
                    M.Add(C, MC);
                }
                // for A := 1 to a do ...
                double P_Ri = ClassProbability(dataset, Ri.Label);
                for (int A = 0; A <= minValues.LastNonEmptyIndex; A++)
                {
                    double sum1 = new double[H.Count]
                        .Select((x, j) => Diff(A, Ri.Example, H[j].Example, minValues, maxValues))
                        .Sum();
                    var sum2 = M.Keys
                        .Select(C => ClassProbability(dataset, C) / (1.0 - P_Ri) * new double[M[C].Count]
                            .Select((x, j) => Diff(A, Ri.Example, M[C][j].Example, minValues, maxValues))
                            .Sum()
                            )
                        .Sum();
                    w[A] = w[A] - sum1 / (m * k) + sum2 / (m * k);
                }
            }
            return w;
        }
    }
}