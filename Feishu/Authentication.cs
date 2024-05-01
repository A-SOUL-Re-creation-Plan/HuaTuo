using RestSharp;

namespace Feishu.Authentication
{
    public class TenantAccessToken
    {
        // 请求地址
        private static readonly Uri _token_url = new("https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal");
        // 请求体，本质用于存储app_id和app_secret
        private readonly RestRequest _request_body;
        // RestClient实例
        private readonly RestClient _httpClient;
        // 存储Token
        private string _token = "";
        // 记录Token可用性
        private long _expires_at = 0;

        // 对外Token属性
        public string Token { get => _token; }


        public TenantAccessToken(string app_id, string app_secret, RestClient client)
        {
            _httpClient = client;
            _request_body = new RestRequest(_token_url, Method.Post);
            _request_body.AddBody(new
            {
                app_id,
                app_secret
            });
            _httpClient.AddDefaultHeader("Content-Type", "application/json");
        }

        public async Task Refresh()
        {
            // 调用此函数刷新Token
            if (Timestamp.GetTimestamp() - _expires_at > -100)
            {
                var resp = await _httpClient.ExecuteAsync<TenantAccessTokenJSON>(_request_body);
                HttpTools.EnsureSuccessful(resp);
                _token = resp.Data!.Tenant_access_token;
                _expires_at = Timestamp.GetTimestamp() + resp.Data!.Expire;
            }
        }
    }

    /// <summary>
    /// 反序列化模板
    /// </summary>
    /// <param name="Code"></param>
    /// <param name="Msg"></param>
    /// <param name="Tenant_access_token"></param>
    /// <param name="Expire"></param>
    internal record class TenantAccessTokenJSON(
        int Code = 0,
        string Msg = "",
        string Tenant_access_token = "",
        int Expire = 0
    );
}
