using System;
using System.Threading.Tasks;
using McpSamples.PptTranslator.HybridApp.Services;

namespace McpSamples.PptTranslator.HybridApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            var filePath = "./../../TestFiles/sample2.pptx";
            var pptService = new PptFileService();

            try
            {
                Console.WriteLine($"Open: {filePath}");
                var texts = await pptService.ExtractAllTextAsync(filePath);

                Console.WriteLine($"\n {texts.Length}text box extracted\n");
                int idx = 1;
                foreach (var text in texts) Console.WriteLine($"[{idx++}] {text}");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"오류 발생: {ex.Message}");
            }
        }
    }
}
