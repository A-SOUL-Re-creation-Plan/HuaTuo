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

            // ����config�ļ�
            if (args.Length == 0) { Console.WriteLine("��Ҫ�����ļ�"); return; }
            else cConfigFile = Toml.ReadFile<HuaTuoConfigFile>(args[0]);

            // �����ؼ��ֿ�
            KeywordService.ReadKeywordDatabase(args[1]);

            var app = WebApplication.Create();

            // ע��BotApp
            BotApp botApp = new BotApp(cConfigFile, app.Logger);

            // ע��group
            foreach (var item in cConfigFile.Group)
            {
                botApp.RegisterGroup(new LarkGroup(botApp, new LarkID(item.Chat_id), new JiHuaProcessor()));
            }

            // ��Ϣ�¼�
            EventManager.RegisterHandlerClass<MessageReceiveHandler>("im.message.receive_v1");


            app.MapPost("/", async (HttpContext httpContext) =>
            {
                if (httpContext.Request.HasJsonContentType())
                {
                    /*
                     * ʲô΢���Լ����Լ��ڿӣ�����body����ô�Ѿ�
                     * ��¼�´˴� EnableBuffering ������
                     * ������ EnableBuffering �������֮��
                     * ʵ���ϻ�ʹ�� FileBufferingReadStream �滻��Ĭ�ϵ� HttpRequestStream
                     * File ��д�˶�ȡ����������ʹ��ͬ������
                     */
                    httpContext.Request.EnableBuffering();
                    string sRequestBody;
                    // ��ʼ��һ���첽������ȡ��
                    using (var stream_reader = new StreamReader(httpContext.Request.Body))
                    {
                        sRequestBody = await stream_reader.ReadToEndAsync();
                    }
                    JsonNode nJsonData = JsonNode.Parse(sRequestBody)!;

                    // ��Ƭ�ص������ܣ��˴����token���ж�
                    if (nJsonData["token"] != null)
                    {
                        if (nJsonData["type"] != null && nJsonData["type"]!.ToString() == "url_verification")
                            return Results.Ok(new { challenge = nJsonData["challenge"]!.ToString() });
                        // ��Ƭ�¼� һ��϶�ʱ�䴦�� �������߳�
                        var resp = await InteractiveCallback(sRequestBody, botApp);
                        return Results.Ok(resp);
                    }

                    // ��һ�������ݣ���ʱ�����ܣ����н��ܺ�parse
                    sRequestBody = EventManager.DecryptData(cConfigFile.Feishu.Encrypt_Key, nJsonData["encrypt"]!.ToString());
                    nJsonData = JsonNode.Parse(sRequestBody)!;

                    // �Ƿ���HTTP�����¼���
                    if (nJsonData["type"] != null)
                    {
                        // HTTP ��֤������challenge
                        if (nJsonData["type"]!.ToString() != "url_verification") return Results.BadRequest();
                        // ��֤ Verification Token
                        if (nJsonData["token"]!.ToString() != cConfigFile.Feishu.Verification_Token) return Results.BadRequest();
                        return Results.Ok(new { challenge = nJsonData["challenge"]!.ToString() });
                    }

                    // �Ƿ���v2�¼���
                    if (nJsonData["schema"] == null) return Results.BadRequest();

                    // ��֤ Verification Token
                    if (nJsonData["header"]!["token"]!.ToString() != cConfigFile.Feishu.Verification_Token) return Results.BadRequest();

                    // Ѱ�һص���
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
            // ˢ�»�ɸѡ�б�
            if (value["action"]!.ToString() == "refreash")
            {
                string? filter = content.Action.Option;
                if (filter == "all") filter = null;
                ////////////////�˶δ����ȡ�� Command.cs �� ArchiveCommand ������ͬʱ����//////////////////////////
                var bili_get = BiliAPI.ArchiveList(botApp.biliCredential, filter);
                var card = new InteractiveContent();
                var date = DateTime.Now;
                card.Header("����б�", $"��һ��ˢ�£�{date:G}", null, "wathet", [InteractiveContent.TextTag("v4.0", "purple")]);
                card.Config();
                var list_column_set = new InteractiveContent.ElementsBuilder()
                    .ColumnSet([
                        new InteractiveContent.CardColumn() {Width="weighted",Weight=3,Vertical_align="top",Elements=[new {tag="markdown",content="**����**(����)",text_align="center"}]},
                    new InteractiveContent.CardColumn() {Width="weighted",Weight=1,Vertical_align="top",Elements=[new {tag="markdown",content="**BVID**",text_align="center"}]},
                    new InteractiveContent.CardColumn() {Width="weighted",Weight=1,Vertical_align="top",Elements=[new {tag="markdown",content="**״̬**",text_align="center"}]},
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
                list_column_set.ExtraDiv($"ȫ�����:{bili_data.Data.Page.Count}  " +
                                         $"<font color='green'>ͨ��:{bili_data.Data.Class.Pubed}</font>\n" +
                                         $"<font color='grey'>������:{bili_data.Data.Class.Is_pubing}</font>  " +
                                         $"<font color='red'>δͨ��:{bili_data.Data.Class.Not_pubed}</font>",
                                new InteractiveContent.ActionBuilder()
                                    .SelectStatic("ɸѡ��ˢ����ϢŶ~", [
                                        InteractiveContent.Option("ȫ�����", "all"),
                                    InteractiveContent.Option("ͨ��", "pubed"),
                                    InteractiveContent.Option("������", "is_pubing"),
                                    InteractiveContent.Option("δͨ��", "not_pubed")
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
        /// �����ļ�����
        /// </summary>
        public record HuaTuoConfigFile
        {
            public required HuaTuoConfigFileFeishu Feishu { get; set; }
            public required HuaTuoConfigFileConfig Config { get; set; }
            public required HuaTuoConfigFileSetting Setting { get; set; }
            public required HuaTuoConfigFileGroup[] Group { get; set; }
        }

        /// <summary>
        /// �����ļ��е�Feishu��
        /// </summary>
        public record HuaTuoConfigFileFeishu
        {
            public required string App_id { get; set; }
            public required string App_secret { get; set; }
            public required string Verification_Token { get; set; }
            public required string Encrypt_Key { get; set; }
        }

        /// <summary>
        /// �����ļ��е�Config��
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
        /// �����ļ��е�Setting��
        /// </summary>
        public record HuaTuoConfigFileSetting
        {
            public required string VersionDesp { get; set; }
            public required string[] StickerNonp { get; set; }
            public required string[] StickerQuestioning { get; set; }
        }

        /// <summary>
        /// �����ļ��е�Group��
        /// </summary>
        public record HuaTuoConfigFileGroup
        {
            public required string Chat_id { get; set; }
            public required bool Debug { get; set; }
        }
    }
}