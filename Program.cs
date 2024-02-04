using Feishu;
using Feishu.Event;
using HuaTuo.Service;
using Nett;
using System.Reflection;
using System.Text.Json.Nodes;

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

                    // ��һ�������ݣ���ʱ�����ܣ����н��ܺ�parse
                    sRequestBody = EventManager.DecryptData(cConfigFile.Feishu.Encrypt_Key, JsonNode.Parse(sRequestBody)!["encrypt"]!.ToString());
                    JsonNode nJsonData = JsonNode.Parse(sRequestBody)!;

                    // �Ƿ���HTTP�����¼���
                    if (nJsonData["type"] != null)
                    {
                        // HTTP ��֤������challenge
                        if (nJsonData["type"]!.ToString() == "url_verification") return Results.BadRequest();
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
            public required HuaTuoConfigFileGroup[] Group {  get; set; }
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
            public required string Debug_group { get; set; }
            public required string Bot_Open_id { get; set; }
            public required string BotCalendarID { get; set; }
        }

        /// <summary>
        /// �����ļ��е�Setting��
        /// </summary>
        public record HuaTuoConfigFileSetting
        {
            public required string VersionDesp { get; set; }
            public required string[] StickerNonp { get; set; }
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