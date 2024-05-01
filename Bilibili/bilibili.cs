using Feishu;
using RestSharp;
using System.Text.Json;

namespace Bilibili
{
    public class BiliCredential(string DEDE_USER_ID, string SESSDATA)
    {
        private string user_id = DEDE_USER_ID;
        private string sessdata = SESSDATA;

        public string UID { get => user_id; }
        public string SessData { get => sessdata; }
    }

    public static class BiliAPI
    {
        private static RestClient client = new RestClient();
        public static string UserAgent { get; set; } = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/63.0.3239.108";

        /// <summary>
        /// 查询视频信息
        /// </summary>
        /// <param name="bvid">BVID</param>
        /// <returns>BiliVideoInfoWebResponse.Rootobject</returns>
        /// <exception cref="Exception">HTTP错误</exception>
        public static async Task<BiliVideoInfoResponse.Rootobject> VideoWebInfo(string bvid)
        {
            var request = new RestRequest("https://api.bilibili.com/x/web-interface/view");
            request.AddHeader("User-Agent", UserAgent);
            request.AddQueryParameter("bvid", bvid);

            var resp = await client.ExecuteAsync(request, Method.Get);
            if (resp.IsSuccessful)
            {
                return JsonSerializer.Deserialize<BiliVideoInfoResponse.Rootobject>(resp.RawBytes, HttpTools.JsonOption) ??
                    throw new Exception("Deserialize Failed");
            }
            else throw resp.ErrorException ?? new Exception("Http Failed");
        }

        /// <summary>
        /// 查询稿件信息
        /// </summary>
        /// <param name="bvid">BVID</param>
        /// <param name="credential">鉴权</param>
        /// <returns>BiliVideoInfoMemberResponse.Rootobject</returns>
        /// <exception cref="Exception">HTTP错误</exception>
        public static async Task<BiliVideoMemberResponse.Rootobject> MemberWebInfo(string bvid, BiliCredential credential)
        {
            var request = new RestRequest("https://member.bilibili.com/x/vupre/web/archive/view");
            request.AddHeader("User-Agent", UserAgent);
            request.AddQueryParameter("bvid", bvid);
            request.AddCookie("DedeUserID", credential.UID, "/", ".bilibili.com");
            request.AddCookie("SESSDATA", credential.SessData, "/", ".bilibili.com");

            var resp = await client.ExecuteAsync(request, Method.Get);
            if (resp.IsSuccessful)
            {
                return JsonSerializer.Deserialize<BiliVideoMemberResponse.Rootobject>(resp.RawBytes, HttpTools.JsonOption) ??
                    throw new Exception("Deserialize Failed");
            }
            else throw resp.ErrorException ?? new Exception("Http Failed");
        }

        /// <summary>
        /// 稿件列表查询
        /// </summary>
        /// <param name="credential">鉴权</param>
        /// <param name="type">查询筛选，不可使用或运算，全选填null</param>
        /// <param name="page">第N页</param>
        /// <param name="size">页大小</param>
        /// <returns>BiliArchiveListResponse.Rootobject</returns>
        /// <exception cref="Exception">HTTP异常</exception>
        public static async Task<BiliArchiveListResponse.Rootobject> ArchiveList(BiliCredential credential, string? type, int page = 1, int size = 10)
        {
            var request = new RestRequest("https://member.bilibili.com/x/web/archives");

            request.AddQueryParameter("status", type ?? "pubed,not_pubed,is_pubing");

            request.AddQueryParameter("pn", page.ToString());
            request.AddQueryParameter("ps", size.ToString());

            request.AddHeader("User-Agent", UserAgent);
            request.AddCookie("DedeUserID", credential.UID, "/", ".bilibili.com");
            request.AddCookie("SESSDATA", credential.SessData, "/", ".bilibili.com");

            var resp = await client.ExecuteAsync(request, Method.Get);
            if (resp.IsSuccessful)
            {
                return JsonSerializer.Deserialize<BiliArchiveListResponse.Rootobject>(resp.RawBytes, HttpTools.JsonOption) ??
                    throw new Exception("Deserialize Failed");
            }
            else throw resp.ErrorException ?? new Exception("Http Failed");
        }

        /// <summary>
        /// 查询稿件问题
        /// </summary>
        /// <param name="credential">鉴权</param>
        /// <param name="bvid">BVID</param>
        /// <returns>BiliRejectReasonResponse.Rootobject</returns>
        /// <exception cref="Exception">HTTP异常</exception>
        public static async Task<BiliRejectReasonResponse.Rootobject> RejectReason(BiliCredential credential, string bvid)
        {
            var request = new RestRequest("https://member.bilibili.com/x/web/archive/failcode");
            request.AddHeader("User-Agent", UserAgent);
            request.AddQueryParameter("bvid", bvid);
            request.AddCookie("DedeUserID", credential.UID, "/", ".bilibili.com");
            request.AddCookie("SESSDATA", credential.SessData, "/", ".bilibili.com");

            var resp = await client.ExecuteAsync(request, Method.Get);
            if (resp.IsSuccessful)
            {
                return JsonSerializer.Deserialize<BiliRejectReasonResponse.Rootobject>(resp.RawBytes, HttpTools.JsonOption) ??
                    throw new Exception("Deserialize Failed");
            }
            else throw resp.ErrorException ?? new Exception("Http Failed");
        }
    }

    public static class BiliVideoInfoResponse
    {
        public record Rootobject
        {
            public required int Code { get; set; }
            public required string Message { get; set; }
            public Data? Data { get; set; }
        }

        public record Data
        {
            // bvid
            public required string Bvid { get; set; }
            // 分P数量
            public required int Videos { get; set; }
            // 1原创 2转载
            public required int Copyright { get; set; }
            // 封面
            public required string Pic { get; set; }
            // 标题
            public required string Title { get; set; }
            // 简介
            public required string Desc { get; set; }
            // 分P信息
            public required PageItem[] Pages { get; set; }
        }

        public record PageItem
        {
            // 计数
            public required int Page { get; set; }
            // 标题
            public required string Part { get; set; }
            // 时长（秒）
            public required int Duration { get; set; }
            // 视频参数
            public required DimensionInfo Dimension { get; set; }
            // 首帧
            public required string First_frame { get; set; }
        }

        public record DimensionInfo
        {
            public required int Width { get; set; }
            public required int Height { get; set; }
            public required int Rotate { get; set; }
        }
    }

    public static class BiliVideoMemberResponse
    {
        public record Rootobject
        {
            public required int Code { get; set; }
            public required string Message { get; set; }
            public required DataItem Data { get; set; }
        }

        public record DataItem
        {
            public required ArchiveItem Archive { get; set; }
            public required VideoItem[] Videos { get; set; }
        }

        public record ArchiveItem
        {
            // AV
            public required int Aid { get; set; }
            // BV
            public required string Bvid { get; set; }
            // 标题
            public required string Title { get; set; }
            // 封面链接
            public required string Cover { get; set; }
            // 标签，以逗号连接
            public required string Tag { get; set; }
            // 1原创 2转载
            public required int Copyright { get; set; }
            // 简介
            public required string Desc { get; set; }
            // 状态
            public required int State { get; set; }
            // 状态描述
            public required string State_desc { get; set; }
            // 转载地址
            public string? Source { get; set; }
            // 粉丝动态
            public required string Dynamic { get; set; }
            // 发布时间
            public required string Ptime { get; set; }
            // 创建时间
            public required string Ctime { get; set; }
        }

        public record VideoItem
        {
            // 单P标题
            public required string Title { get; set; }
            // 时长秒
            public required int Duration { get; set; }
            // 计数
            public required int Index { get; set; }
            // 单P状态
            public required int Status { get; set; }
        }
    }

    public static class BiliArchiveListResponse
    {

        public record Rootobject
        {
            public required int Code { get; set; }
            public required string Message { get; set; }
            public required Data Data { get; set; }
        }

        public record Data
        {
            public required ClassItem Class { get; set; }
            public required Arc_Audits[] Arc_audits { get; set; }
            public required Page Page { get; set; }

            public Arc_Audits? Search(string bvid)
            {
                foreach (var item in Arc_audits)
                {
                    if (item.Archive.Bvid == bvid)
                    {
                        return item;
                    }
                }
                return null;
            }
        }

        public record ClassItem
        {
            // 已通过
            public int Pubed { get; set; }
            // 未通过
            public int Not_pubed { get; set; }
            // 处理中
            public int Is_pubing { get; set; }
        }

        public record Page
        {
            // 第N页
            public required int Pn { get; set; }
            // 页容量
            public required int Ps { get; set; }
            // 总计
            public required int Count { get; set; }
        }

        public record Arc_Audits
        {
            public required BiliVideoMemberResponse.ArchiveItem Archive { get; set; }
        }
    }

    public static class BiliRejectReasonResponse
    {
        public record Rootobject
        {
            public required int Code { get; set; }
            public required string Message { get; set; }
            public required Data Data { get; set; }
        }

        public record Data
        {
            public required int Aid { get; set; }
            public required Video[] Videos { get; set; }
        }

        public record Video
        {
            public required int Index_order { get; set; }
            public required string Xcode_fail_msg { get; set; }
        }
    }
}
