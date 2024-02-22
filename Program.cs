using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bilibili;
using Feishu;
using Feishu.Event;
using Feishu.Message;
using HuaTuo.Service;
using HuaTuoMain.Service;
using Nett;

namespace HuaTuo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            HuaTuoConfigFile cConfigFile;

            // 加载config文件
            if (args.Length == 0) { Console.WriteLine("需要配置文件"); return; }
            else cConfigFile = Toml.ReadFile<HuaTuoConfigFile>(args[0]);

            // 建立关键字库
            KeywordService.ReadKeywordDatabase(args[1]);

            var app = WebApplication.Create();

            // 注册BotApp
            BotApp botApp = new BotApp(cConfigFile, app.Logger);

            // 注册group
            foreach (var item in cConfigFile.Group)
            {
                botApp.RegisterGroup(new LarkGroup(botApp, new LarkID(item.Chat_id), new JiHuaProcessor()));
            }

            // 消息事件
            EventManager.RegisterHandlerClass<MessageReceiveHandler>("im.message.receive_v1");


            app.MapPost("/", async (HttpContext httpContext) =>
            {
                if (httpContext.Request.HasJsonContentType())
                {
                    /*
                     * 什么微软自己给自己挖坑，读个body还那么费劲
                     * 记录下此处 EnableBuffering 的作用
                     * 启用了 EnableBuffering 这个操作之后
                     * 实际上会使用 FileBufferingReadStream 替换掉默认的 HttpRequestStream
                     * File 重写了读取方法，允许使用同步操作
                     */
                    httpContext.Request.EnableBuffering();
                    string sRequestBody;
                    // 初始化一个异步的流读取器
                    using (var stream_reader = new StreamReader(httpContext.Request.Body))
                    {
                        sRequestBody = await stream_reader.ReadToEndAsync();
                    }
                    JsonNode nJsonData = JsonNode.Parse(sRequestBody)!;

                    // 卡片回调不加密，此处检测token来判断
                    if (nJsonData["token"] != null)
                    {
                        if (nJsonData["type"] != null && nJsonData["type"]!.ToString() == "url_verification")
                            return Results.Ok(new { challenge = nJsonData["challenge"]!.ToString() });
                        // 卡片事件 一般较短时间处理 不开新线程
                        var resp = await InteractiveCallback(sRequestBody, botApp);
                        return Results.Ok(resp);
                    }

                    // 第一步读数据，此时被加密，进行解密后parse
                    sRequestBody = EventManager.DecryptData(cConfigFile.Feishu.Encrypt_Key, nJsonData["encrypt"]!.ToString());
                    nJsonData = JsonNode.Parse(sRequestBody)!;

                    // 是否是HTTP测试事件？
                    if (nJsonData["type"] != null)
                    {
                        // HTTP 验证，返回challenge
                        if (nJsonData["type"]!.ToString() != "url_verification") return Results.BadRequest();
                        // 验证 Verification Token
                        if (nJsonData["token"]!.ToString() != cConfigFile.Feishu.Verification_Token) return Results.BadRequest();
                        return Results.Ok(new { challenge = nJsonData["challenge"]!.ToString() });
                    }

                    // 是否是v2事件？
                    if (nJsonData["schema"] == null) return Results.BadRequest();

                    // 验证 Verification Token
                    if (nJsonData["header"]!["token"]!.ToString() != cConfigFile.Feishu.Verification_Token) return Results.BadRequest();

                    // 寻找回调类
                    Type? event_handler = EventManager.GetHandlerWithType(nJsonData["header"]!["event_type"]!.ToString());
                    if (event_handler != null)
                    {
                        var handler = (FeishuEventHandler)Activator.CreateInstance(event_handler, botApp)!;
                        new Thread(() => handler.EventCallback(sRequestBody)).Start();
                    }

                    return Results.Ok();
                }
                else return Results.BadRequest();
            });

            app.Run("http://localhost:3000");
        }

        public static async Task<object> InteractiveCallback(string sRequestBody, BotApp botApp)
        {
            InteractiveEventContent content = JsonSerializer.Deserialize<InteractiveEventContent>(sRequestBody, HttpTools.JsonOption)!;
            var value = JsonNode.Parse(sRequestBody)!["action"]!["value"];
            if (value == null) return new { };
            if (value["action"] == null) return new { };
            // 刷新或筛选列表
            if (value["action"]!.ToString() == "refreash")
            {
                string? filter = content.Action.Option;
                if (filter == "all") filter = null;
                ////////////////此段代码截取自 Command.cs 中 ArchiveCommand 函数，同时更新//////////////////////////
                var bili_get = BiliAPI.ArchiveList(botApp.biliCredential, filter);
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
                var bili_data = await bili_get;
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
                                        ], new { action = "refreash" }, filter).Build()[0], true);
                card.Elements(list_column_set.Build());
                //////////////////////////////////////////////////////////////////////////////////////////////////
                return card.RawData;
            }
            return new { };
        }

        internal record InteractiveEventContent
        {
            public required string Open_id { get; set; }
            public required string Open_message_id { get; set; }
            public required string Token {  get; set; }
            public required InteractiveAction Action { get; set; }
        }

        internal record InteractiveAction
        {
            public object? Value { get; set; }
            public required string Tag { get; set; }
            public string? Option { get; set; }
        }

        public static T ToDeepCopy<T>(T obj)
        {
            if (obj == null)
            {
                return obj;
            }
            var type = obj.GetType();
            if (obj is string || type.IsValueType)
            {
                return obj;
            }

            var result = Activator.CreateInstance(type)!;
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            foreach (var field in fields)
            {
                field.SetValue(result, field.GetValue(obj));
            }
            return (T)result;
        }

        /// <summary>
        /// 配置文件基类
        /// </summary>
        public record HuaTuoConfigFile
        {
            public required HuaTuoConfigFileFeishu Feishu { get; set; }
            public required HuaTuoConfigFileConfig Config { get; set; }
            public required HuaTuoConfigFileSetting Setting { get; set; }
            public required HuaTuoConfigFileGroup[] Group { get; set; }
        }

        /// <summary>
        /// 配置文件中的Feishu表
        /// </summary>
        public record HuaTuoConfigFileFeishu
        {
            public required string App_id { get; set; }
            public required string App_secret { get; set; }
            public required string Verification_Token { get; set; }
            public required string Encrypt_Key { get; set; }
        }

        /// <summary>
        /// 配置文件中的Config表
        /// </summary>
        public record HuaTuoConfigFileConfig
        {
            public required string Bot_Open_id { get; set; }
            public required string Bot_Debug_id { get; set; }
            public required string BotCalendarID { get; set; }
            public required string CloudSecretID { get; set; }
            public required string CloudSecretKey { get; set; }
            public required string BiliUserID { get; set; }
            public required string BiliSESSData { get; set; }
            public required string FeishuID_Ava { get; set; }
            public required string FeishuID_Bella { get; set; }
            public required string FeishuID_Diana { get; set; }
            public required string FeishuID_Eileen { get; set; }
            public required string FeishuID_Jihua { get; set; }
            public required string FeishuID_ASOUL { get; set; }
        }

        /// <summary>
        /// 配置文件中的Setting表
        /// </summary>
        public record HuaTuoConfigFileSetting
        {
            public required string VersionDesp { get; set; }
            public required string[] StickerNonp { get; set; }
            public required string[] StickerQuestioning { get; set; }
        }

        /// <summary>
        /// 配置文件中的Group组
        /// </summary>
        public record HuaTuoConfigFileGroup
        {
            public required string Chat_id { get; set; }
            public required bool Debug { get; set; }
        }
    }
}