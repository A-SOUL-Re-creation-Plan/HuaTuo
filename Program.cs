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
                Console.WriteLine("需要配置文件 toml");
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
             * 在此处注册回调函数
             */
            BotApp botApp = new BotApp(app_id, app_secret);
            EventManager.RegisterHandlerClass<MessageReceiveHandler>("im.message.receive_v1");

            var app = WebApplication.Create();

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
                    string request_body;
                    // 初始化一个异步的流读取器
                    using (var stream_reader =  new StreamReader(httpContext.Request.Body))
                    {
                        request_body = await stream_reader.ReadToEndAsync();
                    }

                    // 第一步读数据，此时被加密，进行解密后parse
                    var content = EventManager.DecryptData(encrypt_key, JsonNode.Parse(request_body)!["encrypt"]!.ToString());
                    var json_data = JsonNode.Parse(content);
                    
                    // 是否是HTTP测试事件？
                    if (json_data!["type"] != null)
                    {
                        // HTTP 验证，返回challenge
                        if (json_data!["type"]!.ToString() == "url_verification") return Results.BadRequest();
                        // 验证 Verification Token
                        if (json_data!["token"]!.ToString() != verification_token) return Results.BadRequest();
                        return Results.Ok(new {challenge = json_data!["challenge"]!.ToString() });
                    }

                    // 是否是v2事件？
                    if (json_data!["schema"] == null) return Results.BadRequest();

                    // 反序列化为Event Content
                    EventContent<object> event_content = JsonSerializer.Deserialize<EventContent<object>>(content, HttpTools.JsonOption)!;
                    // 验证 Verification Token
                    if (event_content.Header.Token != verification_token) return Results.BadRequest();

                    // 寻找回调类
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