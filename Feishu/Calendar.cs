using RestSharp;
using System.Text.Json;

namespace Feishu.Calendar
{
    public class EventClient
    {
        private static readonly Uri _base_uri = new("https://open.feishu.cn/open-apis/calendar/v4/calendars/");
        private readonly RestClient _client;
        private readonly BotApp app;

        public EventClient(BotApp app, RestClient client)
        {
            _client = client;
            this.app = app;
        }

        public async Task<CalendarBody.GetEventResponse> GetEvent(string calendar_id, string event_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/{event_id}");

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Get);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<CalendarBody.GetEventResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        public async Task<CalendarBody.GetEventListResponse> GetEventList(string calendar_id, string? start_time = null, string? end_time = null,
            int? page_size = null, string? page_token = null, string? sync_token = null)
        {
            var token = app.RefreashToken();

            RestRequest request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/");
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

        public async Task<CalendarBody.GetEventResponse> CreateEvent(string calendar_id, CalendarEvent calendarEvent)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/");

            request.AddBody(calendarEvent);

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Post);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<CalendarBody.GetEventResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        public async Task DeleteEvent(string calendar_id, string event_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/{event_id}");

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Delete);
            HttpTools.EnsureSuccessful(resp);
        }

        public async Task<CalendarBody.GetEventResponse> EditEvent(string calendar_id, string event_id, CalendarEvent calendarEvent)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/{event_id}");

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
        public string? Page_token { get; set; }
        public string? Sync_token { get; set; }
        public CalendarEvent[]? Items { get; set; }
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
