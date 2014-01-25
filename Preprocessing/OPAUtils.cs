using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Text;

namespace OPA
{
    public static class OPAUtils
    {
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
            startInfo.RedirectStandardError = true;
            Process process = Process.Start(startInfo);
            char[] buffer = new char[1000];
            do
            {
                Thread.Sleep(100);
                //Console.Write("<1>");
                int count = process.StandardOutput.Read(buffer, 0, 1000);
                //Console.Write("<2>");
                char[] tmp = new char[count];
                Array.Copy(buffer, tmp, count);
                Console.Write(new string(tmp));
            }
            while (!process.HasExited);
            Console.Write(process.StandardOutput.ReadToEnd()); // read the rest of the output
            Console.Write(process.StandardError.ReadToEnd()); // if parser crashes, this comes handy
        }
    }
}
