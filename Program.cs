using Feishu;
using Feishu.Event;
using Feishu.Serve;
using Nett;
using System.Text.Json;
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

        /// <summary>
        /// �����ļ�����
        /// </summary>
        public record HuaTuoConfigFile
        {
            public required HuaTuoConfigFileFeishu Feishu { get; set; }
            public required HuaTuoConfigFileConfig Config { get; set; }
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

        public record HuaTuoConfigFileConfig
        {
            public required string Debug_group { get; set; }
            public required string Bot_Open_id { get; set; }
            public required string[] JiHua_Serve {  get; set; }
            public required string[] Debug_Serve { get; set; }
        }
    }
}