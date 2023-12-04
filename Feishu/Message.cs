using RestSharp;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Feishu.Message
{
    /// <summary>
    /// 发送内容的接口
    /// </summary>
    public interface IMessageContent
    {
        public string ContentType { get; }
        public string Content { get; }
    }

    /// <summary>
    /// 构造一条纯文本消息
    /// </summary>
    public sealed class TextContent : IMessageContent
    {
        public string ContentType { get => "text"; }
        public string Text { get; set; } = "";
        public string Content { get { return JsonSerializer.Serialize(new { text = Text }); } }

        public TextContent() { }
        public TextContent(string text) { Text = text; }
    }

    /// <summary>
    /// 构造一条表情消息
    /// </summary>
    public sealed class StickerContent : IMessageContent
    {
        public string ContentType { get => "sticker"; }
        public string File_key { get; set; } = "";
        public string Content { get { return JsonSerializer.Serialize(new { file_key = File_key }); } }

        public StickerContent() { }
        public StickerContent(string file_key) { File_key = file_key; }
    }


    /// <summary>
    /// 构造一条图片消息
    /// </summary>
    public sealed class ImageContent : IMessageContent
    {
        public string ContentType { get => "image"; }
        public string Image_key { get; set; } = "";
        public string Content { get { return JsonSerializer.Serialize(new { image_key = Image_key }); } }

        public ImageContent() { }
        public ImageContent(string image_key) { Image_key = image_key; }
    }


    /// <summary>
    /// 飞书表情
    /// </summary>
    public enum FeishuMessageEmotion
    {
        OK,
        THUMBSUP
    }

    /// <summary>
    /// 构造一条富文本
    /// </summary>
    public sealed class PostContent : IMessageContent
    {
        public string ContentType { get => "post"; }
        public string Content
        {
            get
            {
                return JsonSerializer.Serialize(new
                {
                    zh_cn = new
                    {
                        this.title,
                        this.content
                    }
                }, HttpTools.JsonOption);
            }
        }

        /// <summary>
        /// 向富文本中添加一段内容
        /// </summary>
        /// <param name="element_list">元素列表</param>
        public void Add(List<object> element_list) => this.content.Add(element_list);

        private string? title;
        private readonly List<List<object>> content = new List<List<object>>();

        /// <summary>
        /// 为富文本添加标题
        /// </summary>
        /// <param name="title">标题内容</param>
        public void SetTitle(string title) => this.title = title;

        /// <summary>
        /// 向富文本中添加一段内容
        /// </summary>
        /// <param name="element_list">元素列表</param>
        public void NewParagraph(List<object> element_list) => this.content.Add(element_list);

        public object NewText(string text) => new { tag = "text", text };
        public object NewText(string text, List<string> style) => new { tag = "text", text, style };

        public object NewLink(string text, string href) => new { tag = "a", text, href };
        public object NewLink(string text, string href, List<string> style) => new { tag = "a", text, href, style };

        public object NewAt() => new { tag = "at", user_id = "all" };
        public object NewAt(List<string> style) => new { tag = "at", user_id = "all", style };
        public object NewAt(LarkID open_id) => new { tag = "at", user_id = open_id.id };
        public object NewAt(LarkID open_id, List<string> style) => new { tag = "at", user_id = open_id.id, style };

        public void NewImgParagraph(string image_key) => NewParagraph(new List<object> { new { tag = "img", image_key } });

        public object NewEmotion(FeishuMessageEmotion emoji) => new { tag = "emotion", emoji_type = Enum.GetName(emoji) };

    }

    /// <summary>
    /// 构造一条消息卡片
    /// </summary>
    public sealed class InteractiveContent : IMessageContent
    {
        public string ContentType { get => "interactive"; }
        public string Content { get { return JsonSerializer.Serialize(_content); } }

        private object _content;

        public InteractiveContent(string template_id)
        {
            _content = new { type = "template", data = new { template_id } };
        }
        public InteractiveContent(string template_id, object template_variable)
        {
            _content = new { type = "template", data = new { template_id, template_variable } };
        }
    }


    /// <summary>
    /// 消息管理主体
    /// </summary>
    public class MessageRequest
    {
        private static readonly Uri _base_uri = new("https://open.feishu.cn/open-apis/im/v1/messages/");
        private readonly RestClient _client;
        private readonly BotApp app;

        public MessageRequest(BotApp app)
        {
            this._client = new RestClient(_base_uri);
            this.app = app;
        }

        public MessageRequest(BotApp app, RestClient client)
        {
            this._client = client;
            this.app = app;
        }

        /// <summary>
        /// 发送一条消息
        /// </summary>
        /// <param name="content">消息内容</param>
        /// <param name="receive_id">接收者</param>
        /// <param name="uuid">去重id，可选</param>
        /// <returns>响应体实例</returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<Response.MessageSendResponse> SendMessage(IMessageContent content, LarkID receive_id, string? uuid = null)
        {
            // 获取Token
            var token = app.RefreashToken();
            // 构建请求体
            var request = new RestRequest();
            request.AddQueryParameter("receive_id_type", receive_id.id_type);
            request.AddBody(new
            {
                receive_id = receive_id.id,
                content = content.Content,
                msg_type = content.ContentType,
                uuid
            });

            // 等待Token并添加进请求头
            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            // 请求
            var resp = await _client.ExecuteAsync(request, Method.Post);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<Response.MessageSendResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        public async Task<Response.MessageGetResponse> GetMessage(string message_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{message_id}");

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Get);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<Response.MessageGetResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        public async Task<Response.MessageSendResponse> ReplyMessage(IMessageContent content, string message_id, string? uuid = null)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{message_id}/reply");

            request.AddBody(new
            {
                content = content.Content,
                msg_type = content.ContentType,
                uuid
            });

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Post);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<Response.MessageSendResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        public async Task DeleteMessage(string message_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{message_id}");

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Delete);
            HttpTools.EnsureSuccessful(resp);
        }

        public async Task<Response.MessageSendResponse> EditMessage(IMessageContent content, string message_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{message_id}");

            request.AddBody(new
            {
                content = content.Content,
                msg_type = content.ContentType
            });

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Put);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<Response.MessageSendResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        public async Task<byte[]> DownloadFromMessage(string message_id, string file_key)
        {
            var token = app.RefreashToken();
            var stream_type = file_key.StartsWith("img") ? "image" : "file";
            var request = new RestRequest($"{message_id}/resources/{file_key}?type={stream_type}");
            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.GetAsync(request);
            return resp.RawBytes ?? throw new Exception("流读取失败");
        }

        public async Task<string> UploadImage(Stream img_stream)
        {
            var token = app.RefreashToken();
            var client = new RestClient("https://open.feishu.cn/open-apis/im/v1/images");
            client.AddDefaultHeader("Content-Type", "multipart/form-data");
            client.AddDefaultParameter("image_type", "message");
            var request = new RestRequest();

            request.AddFile("image", () => img_stream, "a.png");

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");
            var resp = await client.PostAsync(request);
            if (!resp.IsSuccessStatusCode) throw new Exception("上传失败");

            JsonNode deresp = JsonNode.Parse(resp.RawBytes) ?? throw new Exception("流读取失败");
            return deresp["data"]!["image_key"]!.ToString();
        }
    }
}

namespace Feishu.Message.Response
{
    // 发送消息响应体
    public class MessageSendResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
        public required MessageData Data { get; set; }
    }

    // 查询消息响应体
    public class MessageGetResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
        public required Dictionary<string, MessageData[]> Data { get; set; }
    }

    // 以下为反序列用的类
    public class MessageData
    {
        public required string Message_id { get; set; }
        public string? Root_id { get; set; }
        public string? Parent_id { get; set; }
        public required string Msg_type { get; set; }
        public string? Create_time { get; set; }
        public string? Update_time { get; set; }
        public bool? Deleted { get; set; }
        public bool? Updated { get; set; }
        public string? Chat_id { get; set; }
        public required SenderData Sender { get; set; }
        public required MessageBody Body { get; set; }
        public MentionsData[]? Mentions { get; set; }
    }
    public class MessageBody { public required string Content { get; set; } }
    public class SenderData
    {
        public required string Id { get; set; }
        public required string Id_type { get; set; }
        public required string Sender_type { get; set; }
        public string? Tenant_key { get; set; }
    }
    public class MentionsData
    {
        public required string Key { get; set; }
        public required string? Id { get; set; }
        public required string? Id_type { get; set; }
        public required string Name { get; set; }
        public string? Tenant_key { get; set; }
    }
}

