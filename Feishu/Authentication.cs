using System.Net.Http.Headers;

namespace Feishu.Authentication
{
    public class TenantAccessToken
    {
        // https://open.feishu.cn/document/server-docs/authentication-management/access-token/tenant_access_token_internal
        // 请求地址
        private static readonly Uri _token_url = new("https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal");
        // 请求体，本质用于存储app_id和app_secret
        private readonly JsonContent _request_body;
        // HttpClient实例 遵顼原则 静态只读
        private static readonly HttpClient _httpClient = new();
        // 存储Token
        private string _token = "";
        // 记录Token可用性
        private long _expires_at = 0;
        // 对外Token属性
        public string Token { get => _token; }


        public TenantAccessToken(string app_id, string app_secret)
        {
            _request_body = JsonContent.Create(new
            {
                app_id,
                app_secret
            });
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task Refreash()
        {
            // 调用此函数刷新Token
            if (Timestamp.GetTimestamp() - _expires_at > -100)
            {
                var resp = await _httpClient.PostAsync(_token_url, _request_body);
                resp.EnsureSuccessStatusCode();
                var resp_body = await resp.Content.ReadFromJsonAsync<TenantAccessTokenJSON>() ?? throw new Exception("反序列化失败");
                _token = resp_body.Tenant_access_token;
                _expires_at = Timestamp.GetTimestamp() + resp_body.Expire;
            }
        }
    }

    // 反序列化模板
    internal record class TenantAccessTokenJSON(
        int Code = 0,
        string Msg = "",
        string Tenant_access_token = "",
        int Expire = 0
    );
}
