using Feishu.Debug;
using Feishu.Event;
using Feishu.Message;
using Feishu.Serve.EventClass;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Feishu.Serve
{
    /// <summary>
    /// 指令处理基类
    /// </summary>
    public abstract class CommandProcessor
    {
        public readonly BotApp botApp;
        public readonly LarkGroup larkGroup;
        public readonly Dictionary<string, Action<EventContent<MessageReceiveBody>>> CommandMap = new();

        /// <summary>
        /// 构造指令服务与指令关键字的字典
        /// </summary>
        protected void ConstructCommandMap()
        {
            foreach (System.Reflection.MethodInfo method in this.GetType().GetMethods())
            {
                var attr = method.GetCustomAttribute<CommandMarkerAttribute>();
                if (attr == null) continue;
                else CommandMap.Add(attr.Keyword, method.CreateDelegate<Action<EventContent<MessageReceiveBody>>>(this));
            }
        }

        public CommandProcessor(BotApp botApp, LarkGroup larkGroup)
        {
            this.botApp = botApp;
            this.larkGroup = larkGroup;
            ConstructCommandMap();
        }

        public CommandProcessor(BotApp botApp, LarkID larkGroupID)
        {
            this.botApp = botApp;
            this.larkGroup = new LarkGroup(botApp, larkGroupID);
            ConstructCommandMap();
        }
    }

    public class JiHuaProcessor : CommandProcessor
    {
        public JiHuaProcessor(BotApp botApp, LarkGroup larkGroup) : base(botApp, larkGroup) { }
        public JiHuaProcessor(BotApp botApp, LarkID larkGroupID) : base(botApp, larkGroupID) { }

        [CommandMarker("version")]
        public void CommandVersion(EventContent<MessageReceiveBody> cEventContent)
        {
            larkGroup.SendMessage(new TextContent("计画驼 v4.0 测试版本")).Wait();
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
        public override async void EventCallback(string sRequestBody)
        {
            // 反序列化为事件接收体
            var cEventBody = this.DeserializeData<EventClass.MessageReceiveBody>(sRequestBody);
            // 避免超时事件
            long receive_delay = Timestamp.GetTimestamp(Timestamp.TimestampType.MilliSeconds) - cEventBody.Event.Message.CreateTimeNum();

            botApp.Logger.LogInformation("{} | 接收用时{}", cEventBody.Event.Message.Content, receive_delay);
            if (receive_delay > 10000)
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
                if (cEventBody.Event.Message.Mentions[0].Id.Open_id != botApp.Config.Bot_Open_id) return;
                if (text_content.Length < 2) return;
                // 指令 先找到服务类 再寻找对应方法
                /*
                 * 我承认我摆了，这里应该要使用一个Manager类来管理不同的服务类
                 * 但是我实在写不下去了，疑似有点过度担心通用性
                 * 为了通用性写了好多绕来绕去的逻辑，唉唉
                 * 这里直接使用if解决掉了
                 */
                if (text_content[1].Equals("debug"))
                {
                    if (botApp.Config.Debug_Serve.Contains(cEventBody.Event.Message.Chat_id!))
                    {
                        var debugger = new FeishuDebugProcessor(botApp, new LarkID(cEventBody.Event.Message.Chat_id!));
                        if (debugger.CommandMap.TryGetValue(text_content[2], out var debug))
                            await Task.Run(() => debug.Invoke(cEventBody));
                    }
                    return;
                }
                else if (botApp.Config.JiHua_Serve.Contains(cEventBody.Event.Message.Chat_id!))
                {
                    var processor = new JiHuaProcessor(botApp, new LarkID(cEventBody.Event.Message.Chat_id!));
                    if (processor.CommandMap.TryGetValue(text_content[1], out var command))
                        await Task.Run(() => command.DynamicInvoke(cEventBody));
                    return;
                }
            }
            // 没有在@成员的情况
            else
            {

            }
        }
    }
}

namespace Feishu.Serve.EventClass
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
