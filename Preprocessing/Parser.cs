using System;

namespace OPA
{
    public static class Parser
    {
        public static void Parse(string inFileName, string outFileName)
        {
            Console.WriteLine("Args: -jar {3} DependencyParser.jar -parse -input_type:tagged_xml -not_parsed_input_file:{0} -parser_model:{2} -parsed_output_xml:{1}", 
                inFileName, outFileName, Config.ParserModelFile, Config.JavaArgs);
            OPAUtils.ExecuteProcess(Config.ParserFolder, "java", string.Format("-jar {3} DependencyParser.jar -parse -input_type:tagged_xml -not_parsed_input_file:{0} -parser_model:{2} -parsed_output_xml:{1}",
                inFileName, outFileName, Config.ParserModelFile, Config.JavaArgs));
        }
    }
}
