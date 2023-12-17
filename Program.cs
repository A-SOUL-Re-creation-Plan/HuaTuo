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

            // 加载config文件
            if (args.Length == 0) { Console.WriteLine("需要配置文件"); return; }
            else cConfigFile = Toml.ReadFile<HuaTuoConfigFile>(args[0]);

            var app = WebApplication.Create();

            // 注册BotApp
            BotApp botApp = new BotApp(cConfigFile, app.Logger);

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

                    // 第一步读数据，此时被加密，进行解密后parse
                    sRequestBody = EventManager.DecryptData(cConfigFile.Feishu.Encrypt_Key, JsonNode.Parse(sRequestBody)!["encrypt"]!.ToString());
                    JsonNode nJsonData = JsonNode.Parse(sRequestBody)!;

                    // 是否是HTTP测试事件？
                    if (nJsonData["type"] != null)
                    {
                        // HTTP 验证，返回challenge
                        if (nJsonData["type"]!.ToString() == "url_verification") return Results.BadRequest();
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

        /// <summary>
        /// 配置文件基类
        /// </summary>
        public record HuaTuoConfigFile
        {
            public required HuaTuoConfigFileFeishu Feishu { get; set; }
            public required HuaTuoConfigFileConfig Config { get; set; }
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

        public record HuaTuoConfigFileConfig
        {
            public required string Debug_group { get; set; }
            public required string Bot_Open_id { get; set; }
            public required string[] JiHua_Serve {  get; set; }
            public required string[] Debug_Serve { get; set; }
        }
    }
}