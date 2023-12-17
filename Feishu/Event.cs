using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Feishu.Event
{
    /// <summary>
    /// 事件回调类的基类，继承自该类，实现EventCallback方法
    /// </summary>
    public abstract class FeishuEventHandler
    {
        protected readonly BotApp botApp;

        protected EventContent<T> DeserializeData<T>(string json_content) where T : class =>
            JsonSerializer.Deserialize<EventContent<T>>(json_content, HttpTools.JsonOption)!;

        public FeishuEventHandler(BotApp botApp) => this.botApp = botApp;

        public abstract void EventCallback(string json_content);
    }

    /// <summary>
    /// 事件管理类
    /// 后续开发希望抽象到每个BotApp实例上，而不是静态类
    /// </summary>
    public static class EventManager
    {
        private static readonly Dictionary<string, Type> handler_map = new();

        /// <summary>
        /// 注册事件服务类·
        /// </summary>
        /// <typeparam name="T">要注册的类</typeparam>
        /// <param name="event_type">事件类型</param>
        public static void RegisterHandlerClass<T>(string event_type) where T : FeishuEventHandler => handler_map.Add(event_type, typeof(T));

        /// <summary>
        /// 获取一个事件类
        /// </summary>
        /// <param name="event_type">事件类名</param>
        /// <returns>Type or null</returns>
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

    /// <summary>
    /// 构造一个事件体
    /// </summary>
    /// <typeparam name="T">事件主体，若不确定则使用object</typeparam>
    public record EventContent<T> where T : class
    {
        public required string Schema { get; set; }
        public required Header Header { get; set; }
        public required T Event { get; set; }
    }

    /// <summary>
    /// 事件头
    /// </summary>
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
