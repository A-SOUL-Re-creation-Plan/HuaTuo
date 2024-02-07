using Feishu.Calendar;
using Feishu.Event;
using Feishu.Message;
using Hardware.Info;
using HuaTuo.Service;
using HuaTuo.Service.EventClass;
using HuaTuoMain.CloudServe;
using RestSharp;
using System.Text.Json;

namespace Feishu
{
    /// <summary>
    /// 时间戳模块
    /// </summary>
    public class Timestamp
    {
        public enum TimestampType
        {
            Seconds,
            MilliSeconds
        }

        public static DateTime TimestampToDate(string timestamp)
        {
            long ts = Convert.ToInt64(timestamp);
            return DateTimeOffset.FromUnixTimeSeconds(ts).DateTime + new TimeSpan(8, 0, 0);
        }
        public static DateTime TimestampToDate(string timestamp, TimestampType type)
        {
            long ts = Convert.ToInt64(timestamp);
            if (type == TimestampType.Seconds)
                return DateTimeOffset.FromUnixTimeSeconds(ts).DateTime + new TimeSpan(8, 0, 0);
            else
                return DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime + new TimeSpan(8, 0, 0);
        }
        public static DateTime TimestampToDate(long timestamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime + new TimeSpan(8, 0, 0);
        }
        public static DateTime TimestampToDate(long timestamp, TimestampType type)
        {
            if (type == TimestampType.Seconds)
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime + new TimeSpan(8, 0, 0);
            else
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime + new TimeSpan(8, 0, 0);
        }

        public static long DateToTimestamp(DateTime date)
        {
            return new DateTimeOffset(date).ToUnixTimeSeconds();
        }
        public static long DateToTimestamp(DateTime date, TimestampType type)
        {
            if (type == TimestampType.Seconds)
                return new DateTimeOffset(date).ToUnixTimeSeconds();
            else
                return new DateTimeOffset(date).ToUnixTimeMilliseconds();
        }

        public static long GetTimestamp()
        {
            return new DateTimeOffset(DateTime.Now.ToUniversalTime()).ToUnixTimeSeconds();
        }
        public static long GetTimestamp(TimestampType type)
        {
            long ts;
            if (type == TimestampType.Seconds)
                ts = new DateTimeOffset(DateTime.Now.ToUniversalTime()).ToUnixTimeSeconds();
            else
                ts = new DateTimeOffset(DateTime.Now.ToUniversalTime()).ToUnixTimeMilliseconds();
            return ts;
        }
    }

    public class MemoryInfos()
    {
        private HardwareInfo hw_info = new HardwareInfo();

        public void Refreash() => hw_info.RefreshMemoryStatus();
        public float Avalible { get => (float)hw_info.MemoryStatus.AvailablePhysical / 1073741824f; }
    }

    /// <summary>
    /// Http工具，包含序列化参数和飞书错误反序列化模板
    /// </summary>
    public static class HttpTools
    {
        private static readonly JsonSerializerOptions json_option = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public static JsonSerializerOptions JsonOption { get => json_option; }

        /// <summary>
        /// 确认正常响应，尝试提取信息
        /// </summary>
        /// <param name="resp">响应体</param>
        /// <exception cref="HttpRequestException">Http请求时发生错误</exception>
        /// <exception cref="FeishuException">请求成功，但飞书端抛出错误</exception>
        public static void EnsureSuccessful(RestResponse resp)
        {
            // 未完成响应
            if (resp.ResponseStatus != ResponseStatus.Completed)
            {
                if (resp.ErrorException != null) throw resp.ErrorException;
                else throw new HttpRequestException(resp.StatusDescription, null, resp.StatusCode);
            }
            else if (!resp.IsSuccessful)
            {
                FeishuErrorResponse errorResponse = JsonSerializer.Deserialize<FeishuErrorResponse>(resp.RawBytes, JsonOption)
                    ?? throw new HttpRequestException(resp.StatusDescription, null, resp.StatusCode);
                throw new FeishuException(errorResponse);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class CommandMarkerAttribute : Attribute
    {
        public string[] Keywords { get; set; }
        public CommandMarkerAttribute(params string[] keyword) => this.Keywords = keyword;
    }

    /// <summary>
    /// 飞书ID
    /// </summary>
    public sealed class LarkID
    {
        public readonly string id;
        public readonly string id_type;

        public override string ToString() => id;

        public LarkID(string id)
        {
            this.id = id;
            if (id.StartsWith("ou"))
                this.id_type = "open_id";
            else if (id.StartsWith("oc"))
                this.id_type = "chat_id";
            else if (id.StartsWith("on"))
                this.id_type = "union_id";
            else if (id.StartsWith("cli_"))
                this.id_type = "app_id";
            else
                throw new NotSupportedException();
        }
        public LarkID(string id, string id_type)
        {
            this.id = id;
            this.id_type = id_type;
        }
    }

    /// <summary>
    /// 机器人主体管理类
    /// </summary>
    public sealed class BotApp
    {
        // 基本信息
        public readonly HuaTuo.Program.HuaTuoConfigFile configFile;
        public readonly ILogger Logger;
        public string Token { get => tenant_accessToken.Token; }

        // 附加模块
        public readonly ServiceOCR serviceOCR;
        public static readonly MemoryInfos memoryInfo = new MemoryInfos();

        // 功能模块
        public async Task RefreashToken() => await tenant_accessToken.Refreash();
        public readonly MessageClient Message;
        public readonly CalendarClient Calendar;

        // 群组
        private readonly Dictionary<string, LarkGroup> Groups = new Dictionary<string, LarkGroup>();
        public void RegisterGroup(LarkGroup group) => this.Groups.Add(group.Chat_id.id, group);
        public void TryGetGroupInstance(LarkID chat_id, out LarkGroup? group) => this.Groups.TryGetValue(chat_id.id, out group);

        // 公用HTTP池
        private readonly RestClient restClient;
        private readonly Authentication.TenantAccessToken tenant_accessToken;

        public BotApp(HuaTuo.Program.HuaTuoConfigFile cfg_file, ILogger logger)
        {
            Logger = logger;
            restClient = new();
            this.configFile = cfg_file;
            this.tenant_accessToken = new(cfg_file.Feishu.App_id, cfg_file.Feishu.App_secret, restClient);
            // 同时初始化功能模块
            this.Message = new MessageClient(this, restClient);
            this.Calendar = new CalendarClient(this, restClient, configFile.Config.BotCalendarID);
            this.serviceOCR = new ServiceOCR(configFile.Config.CloudSecretID, configFile.Config.CloudSecretKey);
        }

        public string RandomSomething(string[] list)
        {
            return list[Random.Shared.Next(0, list.Length - 1)];
        }
    }

    public enum GroupStatus
    {
        Free,
        Busy,
        Waiting
    }

    /// <summary>
    /// 群组类
    /// </summary>
    public class LarkGroup
    {
        // 记录一些基本信息
        public readonly LarkID Chat_id;
        public readonly BotApp botApp;

        // 附加模块

        // 简易状态机与消息处理
        private GroupStatus status = GroupStatus.Free;
        public GroupStatus Status { get => this.status; }
        private CommandProcessor messageProcessor;
        public string[]? RecentReceive { get; set; }

        // 消息处理
        public async Task MessageCallback(EventContent<MessageReceiveBody> cEventContent)
        {
            lock (this)
            {
                if (this.status == GroupStatus.Free)
                    this.status = GroupStatus.Busy;
                else return;
            }
            try
            {
                await Task.Run(() => messageProcessor.CommandCallback(cEventContent, this));
            }
            finally { this.status = GroupStatus.Free; }
        }

        public LarkGroup(BotApp botApp, LarkID chat_id, CommandProcessor processor)
        {
            this.botApp = botApp;
            Chat_id = chat_id;
            this.messageProcessor = processor;
        }

        public async Task<Message.Response.MessageSendResponse> SendMessageAsync(IMessageContent content, string? uuid = null) =>
            await this.botApp.Message.SendMessageAsync(content, Chat_id, uuid);
    }

    /// <summary>
    /// 飞书反馈的错误内容
    /// </summary>
    public record FeishuErrorResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
    }

    /// <summary>
    /// 飞书反馈的错误
    /// </summary>
    public sealed class FeishuException : Exception
    {
        public FeishuErrorResponse Response { get; }

        public FeishuException() : base() => this.Response = new FeishuErrorResponse() { Code = -1, Msg = "unknown" };
        public FeishuException(string message) : base(message) => this.Response = new FeishuErrorResponse() { Code = -1, Msg = message };
        public FeishuException(string message, Exception innerException) : base(message, innerException) => this.Response = new FeishuErrorResponse() { Code = -1, Msg = message };
        public FeishuException(FeishuErrorResponse response) => this.Response = response;

        public override string Message { get => this.Response.Msg; }
    }
}
