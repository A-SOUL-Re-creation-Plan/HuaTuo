using System.Text.Json;
using System.Text.Json.Nodes;
using RestSharp;

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

        public void Add(string content) { Text += content; }
        public void AddAt(LarkID id) { Text += $"<at user_id=\"{id.id}\"></at>"; }
        public void AddUnerline(string content) { Text += $"<u>{content}</u>"; }
        public void AddDelete(string content) { Text += $"<s>{content}</s>"; }
        public void AddBold(string content) { Text += $"<b>{content}</b>"; }
        public void AddItalic(string content) { Text += $"<i>{content}</i>"; }
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
        private string? title;
        private readonly List<object[]> content = new List<object[]>();

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
        public void Add(object[] element_list) => this.content.Add(element_list);

        /// <summary>
        /// 为富文本添加标题
        /// </summary>
        /// <param name="title">标题内容</param>
        public void SetTitle(string title) => this.title = title;

        /// <summary>
        /// 向富文本中添加一段内容
        /// </summary>
        /// <param name="element_list">元素列表</param>
        public void NewParagraph(object[] element_list) => this.content.Add(element_list);

        public static object NewText(string text) => new { tag = "text", text };
        public static object NewText(string text, string[] style) => new { tag = "text", text, style };

        public static object NewLink(string text, string href) => new { tag = "a", text, href };
        public static object NewLink(string text, string href, string[] style) => new { tag = "a", text, href, style };

        public static object NewAt() => new { tag = "at", user_id = "all" };
        public static object NewAt(string[] style) => new { tag = "at", user_id = "all", style };
        public static object NewAt(LarkID open_id) => new { tag = "at", user_id = open_id.id };
        public static object NewAt(LarkID open_id, string[] style) => new { tag = "at", user_id = open_id.id, style };

        public void NewImgParagraph(string image_key) => NewParagraph([new { tag = "img", image_key }]);

        public static object NewEmotion(FeishuMessageEmotion emoji) => new { tag = "emotion", emoji_type = Enum.GetName(emoji) };

    }

    /// <summary>
    /// 构造一条消息卡片
    /// </summary>
    public sealed class InteractiveContent : IMessageContent
    {
        public string ContentType { get => "interactive"; }
        public string Content { get => JsonSerializer.Serialize(_cards, HttpTools.JsonOption); }
        public object RawData { get => _cards; }

        private readonly Dictionary<string, object> _cards = new Dictionary<string, object>();

        public InteractiveContent(string template_id)
        {
            _cards.Add("type", "template");
            _cards.Add("data", new { template_id });
        }

        public InteractiveContent(string template_id, object template_variable)
        {
            _cards.Add("type", "template");
            _cards.Add("data", new { template_id, template_variable });
        }

        public InteractiveContent() { }

        public static object TextTag(string tag_text, string color) =>
            new
            {
                tag = "text_tag",
                text = new { tag = "plain_text", content = tag_text },
                color,
            };

        public void Config(bool enable_forward = true, bool update_multi = true)
        {
            var obj = new { enable_forward, update_multi };
            _cards.Add("config", obj);
        }

        public void Header(string title, string? subtitle = null, string? icon_key = null, string? color = null, object[]? text_tag_list = null)
        {
            var obj = new
            {
                title = new
                {
                    tag = "plain_text",
                    content = title,
                },
                subtitle = subtitle == null ? null : new { tag = "plain_text", content = subtitle },
                icon = icon_key == null ? null : new { img_key = icon_key },
                template = color,
                text_tag_list
            };
            _cards.Add("header", obj);
        }

        public void Elements(object[] elements)
        {
            _cards.Add("elements", elements);
        }

        public record CardColumn
        {
            public string Tag { get; } = "column";
            public object[]? Elements { get; set; } = null;
            public string? Width { get; set; } = null;
            public int? Weight { get; set; } = null;
            public string? Vertical_align { get; set; } = null;
        }

        public class ElementsBuilder
        {
            private readonly List<object> elements = new List<object>();
            public object[] Build() => elements.ToArray();

            public ElementsBuilder TextDiv(string text, bool lark_md = false)
            {
                elements.Add(new
                {
                    tag = "div",
                    text = new
                    {
                        tag = lark_md ? "lark_md" : "plain_text",
                        content = text,
                    }
                });
                return this;
            }

            public ElementsBuilder MarkdownDiv(string content, string? text_align = null)
            {
                elements.Add(new
                {
                    tag = "markdown",
                    content,
                    text_align
                });
                return this;
            }

            public ElementsBuilder ImageDiv(string image_key, string alt = "", string mode = "stretch")
            {
                elements.Add(new
                {
                    tag = "img",
                    img_key = image_key,
                    mode,
                    alt = new
                    {
                        tag = "plain_text",
                        content = alt,
                    },
                });
                return this;
            }

            public ElementsBuilder DividerLine() { elements.Add(new { tag = "hr" }); return this; }

            public ElementsBuilder Actions(object[] actions)
            {
                elements.Add(new { tag = "action", actions, layout = "flow" });
                return this;
            }

            public ElementsBuilder ColumnSet(CardColumn[] columns, string background_style = "default", string flex_mode = "none", string horizontal_spacing = "default")
            {
                elements.Add(new
                {
                    tag = "column_set",
                    columns,
                    flex_mode,
                    background_style,
                    horizontal_spacing,
                });
                return this;
            }

            public ElementsBuilder ExtraDiv(string text, object extra, bool lark_md = false)
            {
                elements.Add(new
                {
                    tag = "div",
                    text = new
                    {
                        tag = lark_md ? "lark_md" : "plain_text",
                        content = text,
                    },
                    extra
                });
                return this;
            }
        }

        /// <summary>
        /// 构造一个确认主体
        /// </summary>
        /// <param name="confirm_title">确认窗口标题</param>
        /// <param name="confirm_text">确认窗口内容</param>
        /// <returns>object</returns>
        public static object Confirmer(string confirm_title, string confirm_text) =>
            new
            {
                title = new { tag = "plain_text", content = confirm_title },
                text = new { tag = "plain_text", content = confirm_text },
            };

        /// <summary>
        /// 构造一条选项
        /// </summary>
        /// <param name="text">选项展示文本</param>
        /// <param name="value">回传值</param>
        /// <returns>object</returns>
        public static object Option(string text, string value) =>
            new { text = new { tag = "plain_text", content = text }, value };

        public class ActionBuilder
        {
            private readonly List<object> actions = new List<object>();
            public object[] Build() => actions.ToArray();

            /// <summary>
            /// 构造一个按钮
            /// </summary>
            /// <param name="text">按钮显示文本</param>
            /// <param name="type">按钮类型</param>
            /// <param name="value">回传值，按照key=value格式</param>
            /// <param name="confirmer">确认主体</param>
            /// <returns></returns>
            public ActionBuilder Button(string text, string type = "default", object? value = null, object? confirmer = null)
            {
                actions.Add(new
                {
                    tag = "button",
                    text = new
                    {
                        tag = "plain_text",
                        content = text,
                    },
                    type,
                    value,
                    confirm = confirmer,
                });
                return this;
            }

            /// <summary>
            /// 构造一个折叠按钮组
            /// </summary>
            /// <param name="options">按钮组，使用option构造</param>
            /// <param name="value">回传值，按key=value格式</param>
            /// <param name="confirmer">确认主体</param>
            /// <returns></returns>
            public ActionBuilder Overflow(object[]? options = null, object? value = null, object? confirmer = null)
            {
                actions.Add(new
                {
                    tag = "overflow",
                    options,
                    value,
                    confirm = confirmer,
                });
                return this;
            }

            /// <summary>
            /// 构造一个列表选择器
            /// </summary>
            /// <param name="descript">提示文本</param>
            /// <param name="options">选项组，使用option构造</param>
            /// <param name="value">回传值，按key=value格式</param>
            /// <param name="initial_option">初始值</param>
            /// <param name="confirmer">确认主体</param>
            /// <returns></returns>
            public ActionBuilder SelectStatic(string descript, object[]? options = null, object? value = null, string? initial_option = null, object? confirmer = null)
            {
                actions.Add(new
                {
                    tag = "select_static",
                    placeholder = new
                    {
                        tag = "plain_text",
                        content = descript
                    },
                    options,
                    value,
                    initial_option,
                    confirm = confirmer,
                });
                return this;
            }
        }
    }


    /// <summary>
    /// 消息管理主体
    /// </summary>
    public class MessageClient
    {
        private static readonly Uri _base_uri = new("https://open.feishu.cn/open-apis/im/v1/messages/");
        private readonly RestClient _client;
        private readonly BotApp app;

        public MessageClient(BotApp app, RestClient client)
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
        public async Task<Response.MessageSendResponse> SendMessageAsync(IMessageContent content, LarkID receive_id, string? uuid = null)
        {
            // 获取Token
            var token = app.RefreashToken();
            // 构建请求体
            var request = new RestRequest(_base_uri);
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

        /// <summary>
        /// 获取消息内容
        /// </summary>
        /// <param name="message_id">消息ID</param>
        /// <returns>消息响应体</returns>
        /// <exception cref="Exception">反序列化发生错误</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<Response.MessageGetResponse> GetMessageAsync(string message_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{message_id}");

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Get);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<Response.MessageGetResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        /// <summary>
        /// 回复一条消息
        /// </summary>
        /// <param name="content">消息内容</param>
        /// <param name="message_id">消息ID</param>
        /// <param name="uuid">可选唯一UUID</param>
        /// <returns>响应体</returns>
        /// <exception cref="Exception">反序列化发生错误</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<Response.MessageSendResponse> ReplyMessageAsync(IMessageContent content, string message_id, string? uuid = null)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{message_id}/reply");

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

        /// <summary>
        /// 撤回一条消息
        /// </summary>
        /// <param name="message_id">消息ID</param>
        /// <returns>None</returns>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task DeleteMessage(string message_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{message_id}");

            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.ExecuteAsync(request, Method.Delete);
            HttpTools.EnsureSuccessful(resp);
        }

        /// <summary>
        /// 编辑一条消息
        /// </summary>
        /// <param name="content">编辑后内容</param>
        /// <param name="message_id">消息ID</param>
        /// <returns>消息响应体</returns>
        /// <exception cref="Exception">发生错误</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<Response.MessageSendResponse> EditMessage(IMessageContent content, string message_id)
        {
            var token = app.RefreashToken();
            var request = new RestRequest($"{_base_uri.OriginalString}{message_id}");

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

        /// <summary>
        /// 从消息中下载资源
        /// </summary>
        /// <param name="message_id">消息ID</param>
        /// <param name="file_key">资源ID</param>
        /// <returns>资源代表的流</returns>
        /// <exception cref="Exception">发错错误</exception>
        public async Task<byte[]> DownloadFromMessage(string message_id, string file_key)
        {
            var token = app.RefreashToken();
            var stream_type = file_key.StartsWith("img") ? "image" : "file";
            var request = new RestRequest($"{_base_uri.OriginalString}{message_id}/resources/{file_key}?type={stream_type}");
            await token;
            request.AddHeader("Authorization", $"Bearer {app.Token}");

            var resp = await _client.GetAsync(request);
            return resp.RawBytes ?? throw new Exception("流读取失败");
        }

        /// <summary>
        /// 上传图片
        /// </summary>
        /// <param name="img_stream">图片流</param>
        /// <returns>图片代表的file_key</returns>
        /// <exception cref="Exception">发生错误时</exception>
        public async Task<string> UploadImage(Stream img_stream)
        {
            var token = app.RefreashToken();
            var client = new RestClient("https://open.feishu.cn/open-apis/im/v1/images");
            client.AddDefaultHeader("Content-Type", "multipart/form-data");
            client.AddDefaultParameter("image_type", "message");
            var request = new RestRequest();

            img_stream.Seek(0, SeekOrigin.Begin);
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
    public record MessageSendResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
        public required MessageData Data { get; set; }
    }

    // 查询消息响应体
    public record MessageGetResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
        public required MessageGetRespItems Data { get; set; }
    }

    public record MessageGetRespItems
    {
        public required MessageData[] Items { get; set; }
    }

    // 以下为反序列用的类
    public record MessageData
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
    public record MessageBody { public required string Content { get; set; } }
    public record SenderData
    {
        public required string Id { get; set; }
        public required string Id_type { get; set; }
        public required string Sender_type { get; set; }
        public string? Tenant_key { get; set; }
    }
    public record MentionsData
    {
        public required string Key { get; set; }
        public required string? Id { get; set; }
        public required string? Id_type { get; set; }
        public required string Name { get; set; }
        public string? Tenant_key { get; set; }
    }
}

