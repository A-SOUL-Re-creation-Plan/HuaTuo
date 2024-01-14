using Feishu;
using Feishu.Debug;
using Feishu.Event;
using Feishu.Message;
using HuaTuo.Service.EventClass;
using System.Reflection;
using System.Text.Json.Nodes;

namespace HuaTuo.Service
{
    public abstract class CommandProcessor
    {
        public CommandProcessor() { ConstructCommandMap(); }

        protected readonly Dictionary<string, Func<EventContent<MessageReceiveBody>, LarkGroup, Task>> command_map = new();

        protected void ConstructCommandMap()
        {
            foreach (MethodInfo method in this.GetType().GetMethods())
            {
                var attr = method.GetCustomAttribute<CommandMarkerAttribute>();
                if (attr == null) continue;
                else
                {
                    foreach (string item in attr.Keywords)
                    {
                        command_map.Add(item, method.CreateDelegate<Func<EventContent<MessageReceiveBody>, LarkGroup, Task>>(this));
                    }
                }
            }
        }

        public abstract Task CommandCallback(EventContent<MessageReceiveBody> cEventContent, LarkGroup larkGroup);
    }

    /// <summary>
    /// 计画组 指令处理类
    /// </summary>
    public class JiHuaProcessor : CommandProcessor
    {
        public JiHuaProcessor() : base() { }

        public override async Task CommandCallback(EventContent<MessageReceiveBody> cEventContent, LarkGroup larkGroup)
        {
            // 获取Content
            JsonNode nMessageContent = JsonNode.Parse(cEventContent.Event.Message.Content)!;
            // 得到文本内容
            string[] text_content = nMessageContent["text"]!.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (text_content.Length < 2) return;
            this.command_map.TryGetValue(text_content[1], out var func);
            if (func == null) return;
            await func.Invoke(cEventContent, larkGroup);
        }

        [CommandMarker("version", "版本")]
        public async Task Version(EventContent<MessageReceiveBody> cEventContent, LarkGroup larkGroup)
        {
            await larkGroup.SendMessageAsync(new TextContent("计画驼 v4.0测试版"));
        }
    }

    /// <summary>
    /// 消息接收事件的处理类
    /// </summary>
    public class MessageReceiveHandler : FeishuEventHandler
    {
        public MessageReceiveHandler(BotApp botApp) : base(botApp) { }

        /// <summary>
        /// 事件回调
        /// </summary>
        /// <param name="sRequestBody">事件JSON原文</param>
        public override async Task EventCallback(string sRequestBody)
        {
            // 反序列化为事件接收体
            var cEventBody = this.DeserializeData<MessageReceiveBody>(sRequestBody);
            // 避免超时事件
            long receive_delay = Timestamp.GetTimestamp(Timestamp.TimestampType.MilliSeconds) - cEventBody.Event.Message.CreateTimeNum();

            botApp.Logger.LogInformation("{} | 接收用时{}", cEventBody.Event.Message.Content, receive_delay);
            if (receive_delay > 20000)
            {
                botApp.Logger.LogWarning("超时消息 {} | AT {}", cEventBody.Event.Message.Content, receive_delay);
                return;
            }

            // 获取Content
            JsonNode nMessageContent = JsonNode.Parse(cEventBody.Event.Message.Content)!;

            // 目前只接收TEXT和群聊消息哦
            if (!cEventBody.Event.Message.Message_type.Equals("text")) return;
            if (!cEventBody.Event.Message.Chat_type.Equals("group")) return;

            // 得到文本内容
            string[] text_content = nMessageContent["text"]!.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // 是在@成员吗？
            if (text_content.Length != 0 && text_content[0].Equals("@_user_1") && cEventBody.Event.Message.Mentions != null)
            {
                // 是在@我吗？
                if (cEventBody.Event.Message.Mentions[0].Id.Open_id != botApp.configFile.Config.Bot_Open_id) return;
                if (text_content.Length < 2) return;
                // 找到对应群组
                botApp.TryGetGroupInstance(new LarkID(cEventBody.Event.Message.Chat_id!), out var larkGroup);
                if (larkGroup == null) return;
                if (larkGroup.Status != GroupStatus.Free)
                {
                    larkGroup.RecentReceive = new string[text_content.Length];
                    text_content.CopyTo(larkGroup.RecentReceive, 0);
                    return;
                }
                else
                {
                    await larkGroup.MessageCallback(cEventBody);
                }
            }
            // 没有在@成员的情况
            else
            {

            }
        }
    }
}

namespace HuaTuo.Service.EventClass
{
    public record MessageReceiveBody
    {
        public required Sender Sender { get; set; }
        public required Message Message { get; set; }
    }

    public record Sender
    {
        public required Sender_Id Sender_id { get; set; }
    }

    public record Sender_Id
    {
        public required string Open_id { get; set; }

        public LarkID ToLarkID() => new LarkID(Open_id);
    }

    public record Message
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

        public long CreateTimeNum() => Convert.ToInt64(Create_time);
    }

    public record Mention
    {
        public required string Key { get; set; }
        public required Sender_Id Id { get; set; }
        public required string Name { get; set; }
    }
}
