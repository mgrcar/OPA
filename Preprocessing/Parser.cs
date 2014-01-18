using System;

namespace OPA
{
    public static class Parser
    {
        public static void Parse(string inFileName, string outFileName)
        {
            //Console.WriteLine("java -jar -Xmx256m DependencyParser.jar -parse -input_type:tagged_xml -not_parsed_input_file:{0} -parser_model:{2} -parsed_output_xml:{1}", 
            //    inFileName, outFileName, Config.ParserModelFile);
            OPAUtils.ExecuteProcess(Config.ParserFolder, "java", string.Format("-jar DependencyParser.jar -parse -input_type:tagged_xml -not_parsed_input_file:{0} -parser_model:{2} -parsed_output_xml:{1}",
                inFileName, outFileName, Config.ParserModelFile));
        }
    }
}
