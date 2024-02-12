using Feishu.Message;
using Nett;

namespace HuaTuoMain.Service
{
    public static class KeywordService
    {
        public static List<KeywordItem> Keywords { get; set; } = new List<KeywordItem>();
        public static string? datebase_path;

        public static void ReadKeywordDatabase(string path)
        {
            datebase_path = path;
            KeywordDatabase file_data = Toml.ReadFile<KeywordDatabase>(path);
            Keywords = file_data.Keyword.ToList();
        }

        public static void SaveKeywordDatabase()
        {
            if (datebase_path == null) return;
            var obj = new KeywordDatabase() { Keyword = Keywords.ToArray() };
            Toml.WriteFile<KeywordDatabase>(obj, datebase_path);
        }

        public static void SaveKeywordDatabase(string path)
        {
            var obj = new KeywordDatabase() { Keyword = Keywords.ToArray() };
            Toml.WriteFile<KeywordDatabase>(obj, path);
        }

        public static void CreateNewKeyword(IMessageContent message, string trigger, string type)
        {
            var item = new KeywordItem()
            {
                TriggerContent = trigger,
                Type = type,
                Content = message.Content,
                ContentType = message.ContentType
            };
            Keywords.Add(item);
        }

        public static void CreateNewKeyword(string content, string content_type, string trigger, string type)
        {
            var message = new FeishuMessageBody(content, content_type);
            var item = new KeywordItem()
            {
                TriggerContent = trigger,
                Type = type,
                Content = message.Content,
                ContentType = message.ContentType
            };
            Keywords.Add(item);
        }

        public static void RemoveKeyword(string keyword)
        {
            List<KeywordItem> items = new List<KeywordItem>();
            foreach (var item in Keywords)
            {
                if (item.TriggerContent == keyword)
                    items.Add(item);
            }
            // ToArray 防止空的解引用
            foreach (var item in items.ToArray())
                Keywords.Remove(item);
        }

        public static IMessageContent[]? SearchItem(string content)
        {
            List<IMessageContent> items = new List<IMessageContent>();
            foreach (var item in Keywords)
            {
                var ans = item.Type switch
                {
                    "normal" => IsBingoNormal(content, item),
                    "contains" => IsBingoContains(content, item),
                    _ => null
                };
                if (ans != null) items.Add(ans);
            }
            if (items.Count == 0) return null;
            return items.ToArray();
        }

        public static IMessageContent? IsBingoNormal(string content, KeywordItem target)
        {
            if (target.TriggerContent == content)
            {
                return new FeishuMessageBody(target.Content, target.ContentType);
            }
            return null;
        }

        public static IMessageContent? IsBingoContains(string content, KeywordItem target)
        {
            if (content.Contains(target.TriggerContent))
            {
                return new FeishuMessageBody(target.Content, target.ContentType);
            }
            return null;
        }

    }

    public record FeishuMessageBody(string content, string content_type) : IMessageContent
    {
        public string Content { get; } = content;
        public string ContentType { get; } = content_type;
    }

    public record KeywordDatabase
    {
        public required KeywordItem[] Keyword { get; set; }
    }

    public record KeywordItem
    {
        public required string Type { get; set; }
        public required string TriggerContent { get; set; }
        public required string Content { get; set; }
        public required string ContentType { get; set; }
    }
}
