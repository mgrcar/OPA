﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;

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

        public static void ExecuteProcess(string dir, string cmd, string args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = dir;
            startInfo.FileName = cmd;
            startInfo.Arguments = args;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            Process process = Process.Start(startInfo);
            do
            {
                Thread.Sleep(300);
                Console.Write(process.StandardOutput.ReadToEnd());
            }
            while (!process.HasExited);
            Console.Write(process.StandardOutput.ReadToEnd()); // not sure if this is needed but just in case...
        }
    }
}