using Bilibili;
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
            // 找不到回调
            if (func == null)
            {
                await larkGroup.SendMessageAsync(new StickerContent(larkGroup.botApp.RandomSomething(larkGroup.botApp.configFile.Setting.StickerQuestioning)));
                return;
            }
            await func.Invoke(cEventContent, text_content, larkGroup);
        }

        [CommandMarker("version", "版本")]
        public async Task Version(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            await larkGroup.SendMessageAsync(new TextContent(larkGroup.botApp.configFile.Setting.VersionDesp));
        }

        [CommandMarker("help", "帮助")]
        public async Task HelpLink(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            // 真的丑
            var help = @"简介生成 -> 简介 {date}
日程创建 -> 日程
关键词 -> 关键词 {设定/删除} {关键词} {一般/包含}*
查询关键词列表 -> 关键词
稿件列表 -> 稿件列表

每个功能的具体说明请查看机器人主页的帮助文档~";
            await larkGroup.SendMessageAsync(new TextContent(help));
        }

        [CommandMarker("delete", "撤回")]
        public async Task DeleteMessage(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            if (cEventContent.Event.Message.Parent_id == null)
            {
                await larkGroup.SendMessageAsync(new TextContent("要操作哪条消息呢？"));
                return;
            }
            try
            {
                await larkGroup.botApp.Message.DeleteMessage(cEventContent.Event.Message.Parent_id);
            }
            catch (FeishuException e)
            {
                await larkGroup.SendMessageAsync(new TextContent(e.Message));
            }
        }

        [CommandMarker("简介")]
        public async Task DespGenerate(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            // 1110 默认去除向晚（20240501）
            byte asoul_all_mem = 0xE;
            // 不包含日期参数，则以当天为日期
            DateTime date = DateTime.Now;
            date = new DateTime(date.Year, date.Month, date.Day);
            // 指令有效性检查
            if (param.Length > 6)
            {
                await ErrPointOut(param, 1, "呜呜呜，塞这么多参数会坏掉的", cEventContent.Event.Sender.Sender_id.ToLarkID(), larkGroup);
                return;
            }
            if (param.Length >= 3)
            {
                for (byte i = 2; i < param.Length; i++)
                    if (param[i].Equals("member"))
                    {
                        asoul_all_mem = Convert.ToByte(param[i+1]);
                        i++;
                    }
                    else if (!DateTime.TryParseExact(param[i], "MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsed))
                    {
                        await ErrPointOut(param, i, "呜呜呜，形状太奇怪了", cEventContent.Event.Sender.Sender_id.ToLarkID(), larkGroup);
                        return;
                    }
                    else
                    {
                        date = new DateTime(date.Year, parsed.Month, parsed.Day);
                    }
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
                text.Add(DespGenerator.GenerateDesp(item.Summary, item.Description, date, asoul_all_mem));
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
                await larkGroup.SendMessageAsync(new TextContent("要...要分析哪张图片呢"));
                return;
            }
            var parent_message = await larkGroup.botApp.Message.GetMessageAsync(cEventContent.Event.Message.Parent_id);
            if (parent_message.Data.Items[0].Msg_type != "post" && parent_message.Data.Items[0].Msg_type != "image")
            {
                await larkGroup.SendMessageAsync(new TextContent("不能分析奇怪的消息啦"));
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
                    text.AddBold($"\n警告：图片{i + 1}的大小不符合标准（应为3000x2000，实际为{image.Width}x{image.Height}），小画仍会尝试进行分析");
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
            if (BotApp.memoryInfo.Avalible <= 0.8f)
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
                    return;
                }
            }
            await larkGroup.SendMessageAsync(new TextContent("Tip：请手动拉取日程参与人"));
        }

        [CommandMarker("关键词")]
        public async Task KeywordCommand(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            // 检查指令
            if (param.Length > 5)
            {
                await ErrPointOut(param, 1, "呜呜呜，塞这么多参数会坏掉的", cEventContent.Event.Sender.Sender_id.ToLarkID(), larkGroup);
                return;
            }
            // 设定/删除
            if (param.Length < 3)
            {
                /*
                 * v4.0 build 6 更新：若不指定第二参数则查询关键词列表
                 * await ErrPointOut(param, 1, "是要设定关键词还是删除关键词呢？", cEventContent.Event.Sender.Sender_id.ToLarkID(), larkGroup);
                 * await larkGroup.SendMessageAsync(new StickerContent(larkGroup.botApp.RandomSomething(larkGroup.botApp.configFile.Setting.StickerQuestioning)));
                 * return;
                */
                if (KeywordService.Keywords.Count == 0)
                {
                    await larkGroup.SendMessageAsync(new TextContent("关键词列表为空"));
                    await larkGroup.SendMessageAsync(new StickerContent(larkGroup.botApp.RandomSomething(
                        larkGroup.botApp.configFile.Setting.StickerNonp)));
                    return;
                }
                var text = new TextContent("当前关键词列表（格式：关键词【触发个数】）\n");
                var dict = new Dictionary<string, int>();
                foreach (var item in KeywordService.Keywords)
                {
                    if(dict.TryGetValue(item.TriggerContent, out var value))
                    {
                        dict[item.TriggerContent] = value + 1;
                    }
                    else
                    {
                        dict.Add(item.TriggerContent, 1);
                    }
                }
                foreach (var item in dict.AsEnumerable())
                {
                    text.Add($"{item.Key}【{item.Value}】\n");
                }
                text.Text = text.Text.TrimEnd('\n');
                await larkGroup.SendMessageAsync(text);
                return;
            }
            var command = param[2] switch
            {
                "设定" => 1,
                "删除" => 2,
                _ => 0
            };
            if (command == 0)
            {
                await ErrPointOut(param, 2, "只能设定或者删除哦", cEventContent.Event.Sender.Sender_id.ToLarkID(), larkGroup);
                return;
            }
            // 关键词
            if (param.Length < 4)
            {
                await ErrPointOut(param, 1, "关键词是什么呢？", cEventContent.Event.Sender.Sender_id.ToLarkID(), larkGroup);
                await larkGroup.SendMessageAsync(new StickerContent(larkGroup.botApp.RandomSomething(larkGroup.botApp.configFile.Setting.StickerQuestioning)));
                return;
            }
            var keyword = param[3];
            // 类型（可选参数）
            string keyword_type = "normal";
            if (param.Length == 5)
            {
                keyword_type = param[4] switch
                {
                    "contain" => "contain",
                    "包含" => "contain",
                    "regular" => "regular",
                    "正则" => "regular",
                    _ => "normal"
                };
            }
            // 开始操作
            if (command == 1)
            {
                // 设定
                if (cEventContent.Event.Message.Parent_id == null)
                {
                    await larkGroup.SendMessageAsync(new TextContent("触发关键词的时候，发送什么比较好呢？"));
                    return;
                }
                var parent_message = await larkGroup.botApp.Message.GetMessageAsync(cEventContent.Event.Message.Parent_id);
                var message = new FeishuMessageBody(parent_message.Data.Items[0].Body.Content, parent_message.Data.Items[0].Msg_type);
                KeywordService.CreateNewKeyword(message, keyword, keyword_type);
                KeywordService.SaveKeywordDatabase();
                await larkGroup.SendMessageAsync(message);
                return;
            }
            else
            {
                // 删除
                KeywordService.RemoveKeyword(keyword);
                KeywordService.SaveKeywordDatabase();
                await larkGroup.SendMessageAsync(new TextContent($"成功删除【{keyword}】"));
            }
        }

        [CommandMarker("稿件列表")]
        public async Task ArchiveListCommand(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            ///////此段代码与 Program.cs 中 InteractiveCallback 函数内相同，同时更新/////////
            var bili_get = BiliAPI.ArchiveList(larkGroup.botApp.biliCredential, null);
            var card = new InteractiveContent();
            var date = DateTime.Now;
            card.Header("稿件列表", $"上一次刷新：{date:G}", null, "wathet", [InteractiveContent.TextTag("v4.0", "purple")]);
            card.Config();
            var list_column_set = new InteractiveContent.ElementsBuilder()
                .ColumnSet([
                    new InteractiveContent.CardColumn() {Width="weighted",Weight=3,Vertical_align="top",Elements=[new {tag="markdown",content="**标题**(封面)",text_align="center"}]},
                    new InteractiveContent.CardColumn() {Width="weighted",Weight=1,Vertical_align="top",Elements=[new {tag="markdown",content="**BVID**",text_align="center"}]},
                    new InteractiveContent.CardColumn() {Width="weighted",Weight=1,Vertical_align="top",Elements=[new {tag="markdown",content="**状态**",text_align="center"}]},
                    ], "grey");
            BiliArchiveListResponse.Rootobject bili_data;
            try
            {
                bili_data = await bili_get;
            }
            catch (Exception e)
            {
                var err_report = new TextContent("查询时发生错误\n");
                err_report.Text += e.Message;
                await larkGroup.SendMessageAsync(err_report);
                return;
            }
            foreach (var video in bili_data.Data.Arc_audits)
            {
                list_column_set.ColumnSet([
                    new InteractiveContent.CardColumn() {Width="weighted",Weight=3,Vertical_align="top",Elements=[new {tag="markdown",content=$"[{video.Archive.Title}]({video.Archive.Cover})",text_align="center"}]},
                    new InteractiveContent.CardColumn() {Width="weighted",Weight=1,Vertical_align="top",Elements=[new {tag="markdown",content=video.Archive.Bvid,text_align="center"}]},
                    new InteractiveContent.CardColumn() {Width="weighted",Weight=1,Vertical_align="top",Elements=[new {tag="markdown",content=$"{video.Archive.State}:{video.Archive.State_desc}",text_align="center"}]},
                    ]);
            }
            list_column_set.ExtraDiv($"全部稿件:{bili_data.Data.Page.Count}  " +
                                     $"<font color='green'>通过:{bili_data.Data.Class.Pubed}</font>\n" +
                                     $"<font color='grey'>处理中:{bili_data.Data.Class.Is_pubing}</font>  " +
                                     $"<font color='red'>未通过:{bili_data.Data.Class.Not_pubed}</font>",
                            new InteractiveContent.ActionBuilder()
                                .SelectStatic("筛选会刷新信息哦~", [
                                    InteractiveContent.Option("全部稿件", "all"),
                                    InteractiveContent.Option("通过", "pubed"),
                                    InteractiveContent.Option("处理中", "is_pubing"),
                                    InteractiveContent.Option("未通过", "not_pubed")
                                    ], new { action="refreash"}).Build()[0], true);
            card.Elements(list_column_set.Build());
            //////////////////////////////////////////////////////////////////////////////////
            await larkGroup.SendMessageAsync(card);
        }

        [CommandMarker("稿件跟踪")]
        public async Task ARCTrackerCommand(EventContent<MessageReceiveBody> cEventContent, string[] param, LarkGroup larkGroup)
        {
            if (param.Length > 4)
            {
                await ErrPointOut(param, 1, "呜呜呜，塞这么多参数会坏掉的", cEventContent.Event.Sender.Sender_id.ToLarkID(), larkGroup);
                return;
            }

            if (param.Length < 3)
            {
                if (larkGroup.ARCTracker == null)
                {
                    larkGroup.ARCTracker = new ArchiveTrackerManager(larkGroup);
                    await larkGroup.ARCTracker.Initialize();
                    new Thread(() => larkGroup.ARCTracker.MainLoop().Wait()).Start();
                }
                await larkGroup.SendMessageAsync(new TextContent("已启用稿件跟踪"));
                return;
            }

            if (param[2] == "off")
            {
                if (larkGroup.ARCTracker == null)
                {
                    await larkGroup.SendMessageAsync(new TextContent("稿件跟踪未在运行哦？！"));
                    return;
                }
                larkGroup.ARCTracker.Stop();
                larkGroup.ARCTracker = null;
                await larkGroup.SendMessageAsync(new TextContent("已强制关闭稿件跟踪"));
                return;
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
            if (!cEventBody.Event.Message.Chat_type.Equals("group"))
            {
                var text = new TextContent($"From {cEventBody.Event.Sender.Sender_id.Open_id}\n");
                text.Add(cEventBody.Event.Message.Content);
                await botApp.Message.SendMessageAsync(text, new LarkID(botApp.configFile.Config.Bot_Debug_id));
            }
            if (!cEventBody.Event.Message.Message_type.Equals("text")) return;
            // 找到对应群组
            botApp.TryGetGroupInstance(new LarkID(cEventBody.Event.Message.Chat_id!), out var larkGroup);
            if (larkGroup == null) return;
            // 得到文本内容
            string[] text_content = nMessageContent["text"]!.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // 是在@成员吗？
            if (text_content.Length != 0 && text_content[0].Equals("@_user_1") && cEventBody.Event.Message.Mentions != null)
            {
                // 是在@我吗？
                if (cEventBody.Event.Message.Mentions[0].Id.Open_id != botApp.configFile.Config.Bot_Open_id) return;
                if (text_content.Length < 2) return;
                
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
                // 关键词检测
                var text = nMessageContent["text"]!.ToString();
                var message = KeywordService.SearchItem(text);
                if (message == null) return;
                foreach (var item in message)
                    await larkGroup.SendMessageAsync(item);
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
