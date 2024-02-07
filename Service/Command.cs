using Feishu;
using Feishu.Event;
using Feishu.Message;
using HuaTuo.Service.EventClass;
using HuaTuoMain.CloudServe;
using HuaTuoMain.Service;
using SixLabors.ImageSharp;
using System.Reflection;
using System.Text.Json.Nodes;

namespace HuaTuo.Service
{
    public abstract class CommandProcessor
    {
        public CommandProcessor() { ConstructCommandMap(); }

        protected readonly Dictionary<string, Func<EventContent<MessageReceiveBody>, string[], LarkGroup, Task>> command_map = new();

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
                        command_map.Add(item, method.CreateDelegate<Func<EventContent<MessageReceiveBody>, string[], LarkGroup, Task>>(this));
                    }
                }
            }
        }

        protected async Task ErrPointOut(string[] text, byte index, string message, LarkID at, LarkGroup larkGroup)
        {
            var msg = new TextContent();
            msg.AddAt(at);
            msg.Add("\n");
            for (byte i = 0; i < text.Length; i++)
            {
                if (i == index) msg.Add($"<b><u>{text[i]}</u></b> ");
                else msg.Add(text[i] + " ");
            }
            msg.Add("\n" + message);
            await larkGroup.SendMessageAsync(msg);
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
            await func.Invoke(cEventContent, text_content, larkGroup);
        }

        [CommandMarker("version", "版本")]
        public async Task Version(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            await larkGroup.SendMessageAsync(new TextContent(larkGroup.botApp.configFile.Setting.VersionDesp));
        }

        [CommandMarker("简介")]
        public async Task DespGenerate(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            // 不包含日期参数，则以当天为日期
            DateTime date = DateTime.Now;
            date = new DateTime(date.Year, date.Month, date.Day);
            // 指令有效性检查
            if (param.Length > 3) 
            { 
                await ErrPointOut(param, 1, "呜呜呜，接收这么多参数会坏掉的", cEventContent.Event.Sender.Sender_id.ToLarkID(), larkGroup);
                return;
            }
            if (param.Length == 3)
            {
                if (!DateTime.TryParseExact(param[2], "MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsed))
                {
                    await ErrPointOut(param, 2, "呜呜呜，形状太奇怪了", cEventContent.Event.Sender.Sender_id.ToLarkID(), larkGroup);
                    return;
                }
                date = new DateTime(date.Year, parsed.Month, parsed.Day);
            }
            // 获得日程信息
            var t_start = Timestamp.DateToTimestamp(date);
            var event_list = await larkGroup.botApp.Calendar.GetEventList(t_start.ToString(), (t_start + 86400).ToString());
            // 没有可用的日程
            if (event_list.Data.Items.Length < 1)
            {
                var msg = new TextContent($"小伙伴你好，{date.Month}月{date.Day}日暂时没有日程哦~");
                await larkGroup.SendMessageAsync(msg);
                var sticker = new StickerContent(larkGroup.botApp.RandomSomething(larkGroup.botApp.configFile.Setting.StickerNonp));
                await larkGroup.SendMessageAsync(sticker);
                return;
            }
            var task_manager = larkGroup.SendMessageAsync(new TextContent($"小伙伴你好，{date.Month}月{date.Day}日共有{event_list.Data.Items.Length}个日程，以下为生成的简介[送心]"));
            foreach (var item in event_list.Data.Items)
            {
                var s_time = Timestamp.TimestampToDate(item.Start_time.Timestamp);
                var text = new TextContent($"开始于{s_time:HH:mm}的【{item.Summary}】\n");
                text.AddBold("当日直播间\n");
                text.Add(DespGenerator.GenerateLiveRoom(item.Color) + "\n");
                text.AddBold("简介\n");
                if (item.Summary == null) continue;
                if (item.Description == null || item.Description.Length < 2) item.Description = "标题";
                text.Add(DespGenerator.GenerateDesp(item.Summary, item.Description, date));
                await task_manager;
                task_manager = larkGroup.SendMessageAsync(text);
            }
            await task_manager;
        }

        [CommandMarker("日程")]
        public async Task ScheduleCreating(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            // 检查父消息
            if (cEventContent.Event.Message.Parent_id == null)
            {
                await larkGroup.SendMessageAsync(new TextContent("小伙伴你好，请指定一条包含日程表图片消息~"));
                return;
            }
            var parent_message = await larkGroup.botApp.Message.GetMessageAsync(cEventContent.Event.Message.Parent_id);
            if (parent_message.Data.Items[0].Msg_type != "post" && parent_message.Data.Items[0].Msg_type != "image")
            {
                await larkGroup.SendMessageAsync(new TextContent("小伙伴你好，指定的消息不是图片或富文本"));
                return;
            }

            // 抓取图片
            List<Task<byte[]>> images_task = new List<Task<byte[]>>();
            if (parent_message.Data.Items[0].Msg_type == "post")
            {
                JsonNode nPostBody = JsonNode.Parse(parent_message.Data.Items[0].Body.Content)!;
                foreach (var paragraph in nPostBody["content"]!.AsArray())
                {
                    foreach (var element in paragraph!.AsArray())
                    {
                        // 每一个element
                        if (element!["tag"]!.ToString() == "img")
                            images_task.Add(larkGroup.botApp.Message.DownloadFromMessage(parent_message.Data.Items[0].Message_id, element!["image_key"]!.ToString()));
                    }
                }
            }
            else
            {
                var file_key = JsonNode.Parse(parent_message.Data.Items[0].Body.Content)!["image_key"]!.ToString();
                images_task.Add(larkGroup.botApp.Message.DownloadFromMessage(parent_message.Data.Items[0].Message_id, file_key));
            }

            try { Task.WaitAll(images_task.ToArray()); }
            catch (Exception e) { await larkGroup.SendMessageAsync(new TextContent(e.Message)); return; }

            // 检验图片
            var text = new TextContent("现开始分析图片，此过程可能消耗时间，请小伙伴耐心等待~");
            for (byte i = 0; i < images_task.Count; i++)
            {
                var image = Image.Load(images_task[i].Result);
                if (image.Width != 3000 || image.Height != 2000)
                    text.AddBold($"\n警告：图片{i+1}的大小不符合标准（应为3000x2000，实际为{image.Width}x{image.Height}），小画仍会尝试进行分析");
                image.Dispose();
            }
            await larkGroup.SendMessageAsync(text);

            // 检验日程表
            List<Task> check_tasks = new List<Task>();
            foreach (var task in images_task)
            {
                check_tasks.Add(Task.Run(async () => {
                    var ocr_req = larkGroup.botApp.serviceOCR.RequestAsync(task.Result);
                    var image = Image.Load(task.Result);
                    // 计算位置
                    int posx = (int)(0.5 * image.Width);
                    int posy = (int)(0.08 * image.Height);
                    int width = (int)(0.267 * image.Width);
                    int height = (int)(0.085 * image.Height);

                    var ocr_tool = new OcrTools(await ocr_req);
                    var rencs = ocr_tool.SearchRectangle(posx, posy, width, height);
                    foreach (var rectangle in rencs)
                    {
                        if (rectangle.DetectedText.Contains("本周日程表"))
                            return;
                    }
                    throw new Exception("Not Confirmed");
                }));
            }
            try { Task.WaitAll(check_tasks.ToArray()); }
            catch (Exception e) { await larkGroup.SendMessageAsync(new TextContent($"至少一张图片未被确认为日程表，日程创建已取消\n{e.Message}")); return; }

            // 运行模型
            BotApp.memoryInfo.Refreash();
            if (BotApp.memoryInfo.Avalible <= 1.0f)
            {
                await larkGroup.SendMessageAsync(new TextContent(
                    $"<b>警告：可用内存过低({BotApp.memoryInfo.Avalible:0.0}GB)，运行模型可能失败</b>\n创建日程期间请尽量不调用机器人以防内存溢出"));
            }
            foreach (var task in images_task)
            {
                var process = new ScheduleProcess(new MemoryStream(task.Result));
                try
                {
                    var result = await process.StartParsing(larkGroup.botApp);
                    var image_key = larkGroup.botApp.Message.UploadImage(result.Image);

                    var show_msg = new PostContent();
                    show_msg.NewParagraph([PostContent.NewText($"共识别到{result.TotalDetected}个日程，成功创建{result.SuccessedCreated}个日程")]);
                    // result.Errors.ToArray()
                    // 不写为Errors.ToArray() 的原因是：自然段充当换行的作用
                    foreach (var item in result.Errors) show_msg.Add([item]);
                    show_msg.NewImgParagraph(await image_key);
                    await larkGroup.SendMessageAsync(show_msg);
                }
                catch (Exception e)
                {
                    var err_msg = new TextContent("创建日程时发生错误(Parsing)\n");
                    err_msg.Add(e.Message + "\n");
                    err_msg.Add($"在Task：{task}");
                    await larkGroup.SendMessageAsync(err_msg);
                }
            }
        }

        [CommandMarker("delete")]
        public async Task DebuggingFunc(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            var list = await larkGroup.botApp.Calendar.GetEventList("1707227952");
            foreach (var item in list.Data.Items)
            {
                await larkGroup.botApp.Calendar.DeleteEvent(item.Event_id);
            }
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
                    await Task.Run(() => larkGroup.MessageCallback(cEventBody));
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
