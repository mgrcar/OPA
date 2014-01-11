using System;
using System.Collections.Generic;
using System.Linq;

namespace OPA
{
    public static class OPAUtils
    {
        public static double StdDev(this IEnumerable<double> values) // taken from Detextive
        {
            double ret = 0;
            int count = values.Count();
            if (count > 1)
            {
                double avg = values.Average();
                double sum = values.Sum(d => (d - avg) * (d - avg));
                ret = Math.Sqrt(sum / count);
            }
            return ret;
        }
    }
}
