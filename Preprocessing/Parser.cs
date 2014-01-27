/*==========================================================================;
 *
 *  File:    Parser.cs
 *  Desc:    Parser interface
 *  Created: Jan-2014
 *
 *  Author:  Miha Grcar
 *
 ***************************************************************************/

using System;
using System.Diagnostics;
using System.Threading;

namespace OPA.Preprocessing
{
    public static class Parser
    {
        private static void ExecuteProcess(string dir, string cmd, string args)
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
                int count = process.StandardOutput.Read(buffer, 0, 1000);
                Console.Write(new string(buffer, 0, count));
            }
            while (!process.HasExited);
            Console.Write(process.StandardOutput.ReadToEnd()); // read the rest of the output
            Console.Write(process.StandardError.ReadToEnd()); // if parser crashes, this comes handy
        }

        public static void Parse(string inFileName, string outFileName)
        {
            Console.WriteLine("Ukaz: -jar {3} DependencyParser.jar -parse -input_type:tagged_xml -not_parsed_input_file:{0} -parser_model:{2} -parsed_output_xml:{1}", 
                inFileName, outFileName, Config.ParserModelFile, Config.JavaArgs);
            ExecuteProcess(Config.ParserFolder, "java", string.Format("-jar {3} DependencyParser.jar -parse -input_type:tagged_xml -not_parsed_input_file:{0} -parser_model:{2} -parsed_output_xml:{1}",
                inFileName, outFileName, Config.ParserModelFile, Config.JavaArgs));
        }
    }
}
