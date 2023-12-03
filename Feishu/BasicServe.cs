using Feishu.Message;
using RestSharp;
using System.Text.Json;

namespace Feishu
{
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

    // 通用工具
    public static class HttpTools
    {
        private static readonly JsonSerializerOptions json_option = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public static JsonSerializerOptions JsonOption { get => json_option; }

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
                FeishuErrorResponse errorResponse = JsonSerializer.Deserialize<FeishuErrorResponse>(resp.RawBytes)
                    ?? throw new HttpRequestException(resp.StatusDescription, null, resp.StatusCode); ;
                throw new FeishuException(errorResponse);
            }
        }
    }

    public sealed class LarkID
    {
        public readonly string id;
        public readonly string id_type;

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

    public sealed class BotApp
    {
        public readonly string app_id;

        private readonly Authentication.TenantAccessToken tenant_accessToken;

        public BotApp(string app_id, string app_secret)
        {
            this.tenant_accessToken = new(app_id, app_secret);
            this.app_id = app_id;
            // 同时初始化功能模块
            message = new MessageRequest(this);
        }

        public async Task RefreashToken() => await tenant_accessToken.Refreash();
        public string Token { get => tenant_accessToken.Token; }

        public MessageRequest message;
    }

    public class FeishuErrorResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
    }

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
