using System;
using System.IO;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    public static class TempFileResolver
    {
        private static readonly string UploadRoot =
            Path.Combine(Path.GetTempPath(), "mcp-uploads");

        private static readonly bool _isAzure =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME"));

        static TempFileResolver()
        {
            if (!Directory.Exists(UploadRoot))
                Directory.CreateDirectory(UploadRoot);
        }

        public static string ResolveToTemp(string filePath)
        {
            // ======================================================
            // Azure 모드에서는 temp:{id} 시스템 사용 금지
            // ======================================================
            if (_isAzure)
                throw new InvalidOperationException("Azure environment does not use TempFileResolver.");

            // temp:{id} → temp 파일로 해석
            if (filePath.StartsWith("temp:", StringComparison.OrdinalIgnoreCase))
            {
                string id = filePath.Substring(5);
                return Path.Combine(UploadRoot, id);
            }

            // 로컬 경로를 temp 로 복사
            if (File.Exists(filePath))
            {
                string id = Guid.NewGuid().ToString("N");
                string dest = Path.Combine(UploadRoot, id);
                File.Copy(filePath, dest, overwrite: true);
                return dest;
            }

            throw new FileNotFoundException("Invalid file input. File not found or unsupported path.", filePath);
        }

        public static string CreateTempJson(string jsonContent)
        {
            // ======================================================
            // Azure 모드에서는 temp JSON 생성 금지
            // ======================================================
            if (_isAzure)
                throw new InvalidOperationException("Azure environment does not use TempFileResolver.");

            string id = Guid.NewGuid().ToString("N");
            string path = Path.Combine(UploadRoot, id);

            File.WriteAllText(path, jsonContent);
            return path;
        }
    }
}
