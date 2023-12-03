using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Feishu.Debug
{
    public class FeishuDebug
    {
        public static string PrintAllProperties(object obj)
        {
            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            return JsonSerializer.Serialize(obj, options);
        }
        
    }
}
