using Feishu.Event;
using Feishu.Message;
using System.Text.Json;

namespace Feishu.Serve
{
    public class Command
    {
    }

    public class MessageReceiveHandler : FeishuEventHandler
    {
        public MessageReceiveHandler(BotApp botApp) : base(botApp)
        {
        }

        public override async void EventCallback(string json_content)
        {
            var event_content = JsonSerializer.Deserialize<EventContent<EventClass.MessageReceiveBody>>(json_content, HttpTools.JsonOption)!;
        }
    }
}

namespace Feishu.Serve.EventClass
{
    public class MessageReceiveBody
    {
        public required Sender Sender { get; set; }
        public required Message Message { get; set; }
    }

    public class Sender
    {
        public required Sender_Id Sender_id { get; set; }
    }

    public class Sender_Id
    {
        public required string Open_id { get; set; }

        public LarkID ToLarkID() => new LarkID(Open_id);
    }

    public class Message
    {
        public required string Message_id { get; set; }
        public string? Root_id { get; set; }
        public string? Parent_id { get; set; }
        public required string Create_time { get; set; }
        public string? Update_time { get; set; }
        public string? Chat_id { get; set; }
        public required string Chat_type { get; set; }
        public required string Message_type { get; set; }
        public required string Content { get; set; }
        public Mention[]? Mentions { get; set; }
    }

    public class Mention
    {
        public required string Key { get; set; }
        public required Sender_Id Id { get; set; }
        public required string Name { get; set; }
    }
}
