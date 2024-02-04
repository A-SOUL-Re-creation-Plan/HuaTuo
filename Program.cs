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

            // 加载config文件
            if (args.Length == 0) { Console.WriteLine("需要配置文件"); return; }
            else cConfigFile = Toml.ReadFile<HuaTuoConfigFile>(args[0]);

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
            public required HuaTuoConfigFileGroup[] Group {  get; set; }
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
            public required string Debug_group { get; set; }
            public required string Bot_Open_id { get; set; }
            public required string BotCalendarID { get; set; }
        }

        /// <summary>
        /// 配置文件中的Setting表
        /// </summary>
        public record HuaTuoConfigFileSetting
        {
            public required string VersionDesp { get; set; }
            public required string[] StickerNonp { get; set; }
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