using Nett;
using System.Text.Json.Nodes;
using Feishu.Event;
using System.Text.Json;
using Feishu;
using Feishu.Serve;

namespace HuaTuo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string app_id = "", app_secret = "", verification_token = "", encrypt_key = "";

            if (args.Length == 0)
            {
                Console.WriteLine("��Ҫ�����ļ� toml");
                Environment.Exit(0);
            }
            else
            {
                    var toml_file = Toml.ReadFile(args[0]);
                    var feishu_table = toml_file.Get<TomlTable>("feishu");
                    var config_table = toml_file.Get<TomlTable>("config");

                    app_id = feishu_table.Get<string>("app_id");
                    app_secret = feishu_table.Get<string>("app_secret");
                    verification_token = feishu_table.Get<string>("Verification_Token");
                    encrypt_key = feishu_table.Get<string>("Encrypt_Key");
            }

            /*
             * �ڴ˴�ע��ص�����
             */
            BotApp botApp = new BotApp(app_id, app_secret);
            EventManager.RegisterHandlerClass<MessageReceiveHandler>("im.message.receive_v1");

            var app = WebApplication.Create();

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
                    string request_body;
                    // ��ʼ��һ���첽������ȡ��
                    using (var stream_reader =  new StreamReader(httpContext.Request.Body))
                    {
                        request_body = await stream_reader.ReadToEndAsync();
                    }

                    // ��һ�������ݣ���ʱ�����ܣ����н��ܺ�parse
                    var content = EventManager.DecryptData(encrypt_key, JsonNode.Parse(request_body)!["encrypt"]!.ToString());
                    var json_data = JsonNode.Parse(content);
                    
                    // �Ƿ���HTTP�����¼���
                    if (json_data!["type"] != null)
                    {
                        // HTTP ��֤������challenge
                        if (json_data!["type"]!.ToString() == "url_verification") return Results.BadRequest();
                        // ��֤ Verification Token
                        if (json_data!["token"]!.ToString() != verification_token) return Results.BadRequest();
                        return Results.Ok(new {challenge = json_data!["challenge"]!.ToString() });
                    }

                    // �Ƿ���v2�¼���
                    if (json_data!["schema"] == null) return Results.BadRequest();

                    // �����л�ΪEvent Content
                    EventContent<object> event_content = JsonSerializer.Deserialize<EventContent<object>>(content, HttpTools.JsonOption)!;
                    // ��֤ Verification Token
                    if (event_content.Header.Token != verification_token) return Results.BadRequest();

                    // Ѱ�һص���
                    Type? event_handler = EventManager.GetHandlerWithType(event_content.Header.Event_type);
                    if (event_handler != null)
                    {
                        var handler = (FeishuEventHandler)Activator.CreateInstance(event_handler, botApp )!;
                        new Thread(() => handler.EventCallback(content)).Start();
                    } 

                    return Results.Ok();
                }
                else return Results.BadRequest();
            });

            app.Run("http://localhost:3000");
        }
    }
}