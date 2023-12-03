using Feishu.Serve;
using RestSharp;
using System.Text.Json;

namespace Feishu.Calendar
{
    // 通用JSON序列化配置
    internal static class JsonOption
    {
        public static JsonSerializerOptions json_option = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
    }

    public class EventDetails
    {
        public string? Summary { get; set; }
        public required EventTime Start_time { get; set; }
        public required EventTime End_time { get; set; }

    }

    public class EventTime
    {
        public required string Timestamp { get; set; }
    }

    public class EventRequest
    {
        private static readonly Uri _base_uri = new("https://open.feishu.cn/open-apis/calendar/v4/calendars/");
        private readonly RestClient _client;
        private readonly BotApp app;

        public EventRequest(BotApp app)
        {
            _client = new RestClient(_base_uri);
            this.app = app;
        }

        public async Task CreatEvent(EventDetails eventDetails)
        {
            var token = app.RefreashToken();

            var request = new RestRequest();
            request.AddBody(JsonContent.Create(eventDetails, options: JsonOption.json_option));

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            await _client.PostAsync(request);
        }
    }
}
