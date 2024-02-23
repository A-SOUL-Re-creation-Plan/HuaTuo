using System.Text.RegularExpressions;
using Feishu.Message;
using Nett;

namespace HuaTuoMain.Service
{
    public static class KeywordService
    {
        public static List<KeywordTable> Keywords { get; set; } = new List<KeywordTable>();
        public static List<KeywordMatcher> Matchers { get; set; } = new List<KeywordMatcher>();
        public static string? datebase_path;

        public abstract class KeywordMatcher
        {
            public abstract KeywordTable Keyword { get; set; }
            public abstract bool IsMatch(string content);
        }

        internal class MatcherNormal(KeywordTable keyword) : KeywordMatcher
        {
            public override KeywordTable Keyword { get; set; } = keyword;

            public override bool IsMatch(string content)
            {
                if (content == Keyword.TriggerContent) return true;
                return false;
            }
        }

        internal class MatcherContain(KeywordTable keyword) : KeywordMatcher
        {
            public override KeywordTable Keyword { get; set; } = keyword;

            public override bool IsMatch(string content)
            {
                if (content.Contains(Keyword.TriggerContent)) return true;
                return false;
            }
        }

        internal class MatcherRegularExp(KeywordTable keyword) : KeywordMatcher
        {
            public override KeywordTable Keyword { get; set; } = keyword;
            private readonly Regex regex = new Regex(keyword.TriggerContent);

            public override bool IsMatch(string content)
            {
                if (regex.IsMatch(content)) return true;
                return false;
            }
        }

        public static void ConstructMatcher()
        {
            Matchers.Clear();
            foreach (var item in Keywords)
            {
                KeywordMatcher matcher = item.Type switch
                {
                    "normal" => new MatcherNormal(item),
                    "contain" => new MatcherContain(item),
                    "regular" => new MatcherRegularExp(item),
                    _ => new MatcherNormal(item)
                };
                Matchers.Add(matcher);
            }
        }

        public static void ReadKeywordDatabase(string path)
        {
            datebase_path = path;
            KeywordDatabase file_data = Toml.ReadFile<KeywordDatabase>(path);
            Keywords = file_data.Keyword.ToList();
            ConstructMatcher();
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
            var item = new KeywordTable()
            {
                TriggerContent = trigger,
                Type = type,
                Content = message.Content,
                ContentType = message.ContentType
            };
            Keywords.Add(item);
            ConstructMatcher();
        }

        public static void CreateNewKeyword(string content, string content_type, string trigger, string type)
        {
            var message = new FeishuMessageBody(content, content_type);
            var item = new KeywordTable()
            {
                TriggerContent = trigger,
                Type = type,
                Content = message.Content,
                ContentType = message.ContentType
            };
            Keywords.Add(item);
            ConstructMatcher();
        }

        public static void RemoveKeyword(string keyword)
        {
            List<KeywordTable> items = new List<KeywordTable>();
            foreach (var item in Keywords)
            {
                if (item.TriggerContent == keyword)
                    items.Add(item);
            }
            // ToArray 防止空的解引用
            foreach (var item in items.ToArray())
                Keywords.Remove(item);
            ConstructMatcher();
        }

        public static IMessageContent[]? SearchItem(string content)
        {
            List<IMessageContent> items = new List<IMessageContent>();
            foreach (var mathcer in Matchers)
            {
                if (mathcer.IsMatch(content))
                    items.Add(new FeishuMessageBody(mathcer.Keyword.Content, mathcer.Keyword.ContentType));
            }
            if (items.Count == 0) return null;
            return items.ToArray();
        }
    }

    public record FeishuMessageBody(string content, string content_type) : IMessageContent
    {
        public string Content { get; } = content;
        public string ContentType { get; } = content_type;
    }

    public record KeywordDatabase
    {
        public required KeywordTable[] Keyword { get; set; }
    }

    public record KeywordTable
    {
        public required string Type { get; set; }
        public required string TriggerContent { get; set; }
        public required string Content { get; set; }
        public required string ContentType { get; set; }
    }
}
