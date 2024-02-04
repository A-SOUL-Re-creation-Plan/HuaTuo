using RestSharp;
using System.Text.Json;

namespace Feishu.Calendar
{
    public class CalendarClient
    {
        private static readonly Uri _base_uri = new("https://open.feishu.cn/open-apis/calendar/v4/calendars/");
        private readonly RestClient _client;
        private readonly BotApp app;

        public string CalendarId { get; init; }

        public CalendarClient(BotApp app, RestClient client, string calendar_id)
        {
            _client = client;
            this.app = app;
            this.CalendarId = calendar_id;
        }

        /// <summary>
        /// 获取日程信息
        /// </summary>
        /// <param name="event_id">日程ID</param>
        /// <returns>CalendarBody.GetEventResponse</returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<CalendarBody.GetEventResponse> GetEvent(string event_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{this.CalendarId}/events/{event_id}");

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Get);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<CalendarBody.GetEventResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        /// <summary>
        /// 获取日程列表
        /// </summary>
        /// <param name="start_time">开始时间（戳）</param>
        /// <param name="end_time">结束时间（戳）</param>
        /// <param name="page_size">页大小</param>
        /// <param name="page_token"></param>
        /// <param name="sync_token"></param>
        /// <returns>CalendarBody.GetEventListResponse</returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<CalendarBody.GetEventListResponse> GetEventList(string? start_time = null, string? end_time = null,
            int? page_size = null, string? page_token = null, string? sync_token = null)
        {
            var token = app.RefreashToken();

            RestRequest request = new RestRequest($"{_base_uri.OriginalString}{this.CalendarId}/events/");
            if (start_time != null) request.AddQueryParameter("start_time", start_time);
            if (end_time != null) request.AddQueryParameter("end_time", end_time);
            if (page_size != null) request.AddQueryParameter("page_size", page_size.ToString());
            if (page_token != null) request.AddQueryParameter("page_token", page_token);
            if (sync_token != null) request.AddQueryParameter("sync_token", sync_token);

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Get);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<CalendarBody.GetEventListResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        /// <summary>
        /// 创建日程
        /// </summary>
        /// <param name="calendarEvent">要创建的日程</param>
        /// <returns>CalendarBody.GetEventResponse</returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<CalendarBody.GetEventResponse> CreateEvent(CalendarEvent calendarEvent)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{this.CalendarId}/events/");

            request.AddBody(calendarEvent);

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Post);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<CalendarBody.GetEventResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        /// <summary>
        /// 删除日程
        /// </summary>
        /// <param name="event_id">日程ID</param>
        /// <returns></returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task DeleteEvent(string event_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{this.CalendarId}/events/{event_id}");

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Delete);
            HttpTools.EnsureSuccessful(resp);
        }

        /// <summary>
        /// 编辑日程
        /// </summary>
        /// <param name="event_id">日程ID</param>
        /// <param name="calendarEvent">要更新的日程</param>
        /// <returns></returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<CalendarBody.GetEventResponse> EditEvent(string event_id, CalendarEvent calendarEvent)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{this.CalendarId}/events/{event_id}");

            request.AddBody(calendarEvent);

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Patch);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<CalendarBody.GetEventResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }
    }

    public record CalendarEvent
    {
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public CalendarBody.StartTime? Start_time { get; set; }
        public CalendarBody.EndTime? End_time { get; set; }
        public CalendarBody.Vchat? Vchat { get; set; }
        public string? Visibility { get; set; }
        public string? Attendee_ability { get; set; }
        public int? Color { get; set; }
        public CalendarBody.Reminder[]? Reminders { get; set; }
    }
}

namespace Feishu.Calendar.CalendarBody
{

    public record GetEventResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
        public required GetData Data { get; set; }
    }

    public record GetEventListResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
        public required GetListData Data { get; set; }
    }

    public record GetData
    {
        public required CalendarEvent Event { get; set; }
    }

    public record GetListData
    {
        public bool Has_more { get; set; }
        public required string Page_token { get; set; }
        public required string Sync_token { get; set; }
        public required CalendarEvent[] Items { get; set; }
    }

    public record CalendarEvent
    {
        public required string Event_id { get; set; }
        public string? Organizer_calendar_id { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public required StartTime Start_time { get; set; }
        public required EndTime End_time { get; set; }
        public Vchat? Vchat { get; set; }
        public required string Visibility { get; set; }
        public required string Attendee_ability { get; set; }
        public required int Color { get; set; }
        public Reminder[]? Reminders { get; set; }
        public required string Status { get; set; }
        public required string Create_time { get; set; }
    }

    public record StartTime
    {
        public required string Timestamp { get; set; }
    }

    public record EndTime
    {
        public required string Timestamp { get; set; }
    }

    public record Vchat
    {
        public required string Vc_type { get; set; }
    }

    public record Reminder
    {
        public int Minutes { get; set; }
    }
}
