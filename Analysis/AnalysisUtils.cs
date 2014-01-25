using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace Analysis
{
    public class BlogMetaData
    {
        public string mBlog = "";
        public string mBlogUrl = "";
        public string mBlogTitle = "";
        public string mBlogTitleShort = "";
        public string mAuthorEMail = "";
        public string mAuthorGender = "";
        public string mAuthorAge = "";
        public string mAuthorLocation = "";
        public string mAuthorEducation = "";
    }

    public enum ClassType
    {
        AuthorName,
        AuthorGender,
        AuthorEducation,
        AuthorLocation,
        AuthorAge
    }  

    public static class AnalysisUtils
    {
        public static string GetLabel(BlogMetaData metaData, ClassType classType)
        {
            switch (classType)
            {
                case ClassType.AuthorName:
                    return metaData.mBlog;
                case ClassType.AuthorAge:
                    return metaData.mAuthorAge.Replace(' ', '_');
                case ClassType.AuthorEducation:
                    return metaData.mAuthorEducation.Replace(' ', '_').Replace('š', 's').Replace('č', 'c');
                case ClassType.AuthorGender:
                    return metaData.mAuthorGender.Replace('Ž', 'Z');
                case ClassType.AuthorLocation:
                    return metaData.mAuthorLocation;
                default:
                    return metaData.mBlog;
            }
        }

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
