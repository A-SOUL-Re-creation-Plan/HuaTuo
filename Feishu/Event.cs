using System.Security.Cryptography;
using System.Text;

namespace Feishu.Event
{
    public abstract class FeishuEventHandler
    {
        protected readonly BotApp botApp;

        public FeishuEventHandler(BotApp botApp) => this.botApp = botApp;

        public abstract void EventCallback(string json_content);
    }

    public static class EventManager
    {
        private static readonly Dictionary<string, Type> handler_map = new();

        public static void RegisterHandlerClass<T>(string event_type) where T : FeishuEventHandler => handler_map.Add(event_type, typeof(T));

        public static Type? GetHandlerWithType(string event_type)
        {
            bool success = handler_map.TryGetValue(event_type, out var handler);
            return success ? handler : null;
        }

        /// <summary>
        /// 解码数据
        /// </summary>
        /// <param name="encrypt_key">Encrypt Key</param>
        /// <param name="data">被加密的数据</param>
        /// <returns>解码后的字符串</returns>
        public static string DecryptData(string encrypt_key, string data)
        {
            byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(encrypt_key));
            byte[] encBytes = Convert.FromBase64String(data);
            Aes AesManaged = Aes.Create();
            AesManaged.Key = key;
            AesManaged.Mode = CipherMode.CBC;
            AesManaged.IV = encBytes.Take(16).ToArray();
            ICryptoTransform transform = AesManaged.CreateDecryptor();
            byte[] blockBytes = transform.TransformFinalBlock(encBytes, 16, encBytes.Length - 16);
            return Encoding.UTF8.GetString(blockBytes);
        }
    }


    public record EventContent<T> where T : class
    {
        public required string Schema { get; set; }
        public required Header Header { get; set; }
        public required T Event { get; set; }
    }

    public record Header
    {
        public required string Event_id { get; set; }
        public required string Token { get; set; }
        public required string Create_time { get; set; }
        public required string Event_type { get; set; }
        public required string Tenant_key { get; set; }
        public required string App_id { get; set; }
    }
}
