namespace HuaTuoMain.Service
{
    enum ASOUL_Members
    {
        Ava = -15417089,
        Bella = -562844,
        Diana = -963671,
        Eileen = -10392859
    }

    enum ASOUL_LiveType
    {
        Personal,
        Couple,
        Chatting,
        Gaming,
        Theater,
        Unknown
    }

    /// <summary>
    /// 简介生成
    /// 对文字的数字化处理是麻烦的，因此：
    /// if大法好！
    /// </summary>
    public static class DespGenerator
    {
        private const string LiveRoom_Ava = "https://live.bilibili.com/22625025/";
        private const string LiveRoom_Bella = "https://live.bilibili.com/22632424/";
        private const string LiveRoom_Diana = "https://live.bilibili.com/22637261/";
        private const string LiveRoom_Eileen = "https://live.bilibili.com/22625027/";

        private const string LiveDesp_Ava = "向晚大魔王\n直播间：https://live.bilibili.com/22625025/\n个人主页：https://space.bilibili.com/672346917/";
        private const string LiveDesp_Bella = "贝拉kira\n直播间：https://live.bilibili.com/22632424/\n个人主页：https://space.bilibili.com/672353429/";
        private const string LiveDesp_Diana = "嘉然今天吃什么\n直播间：https://live.bilibili.com/22637261/\n个人主页：https://space.bilibili.com/672328094/";
        private const string LiveDesp_Eileen = "乃琳Queen\n直播间：https://live.bilibili.com/22625027/\n个人主页：https://space.bilibili.com/672342685/";

        private static ASOUL_LiveType ToGetLiveType(string summary)
        {
            return ASOUL_LiveType.Unknown;
        }
    }
}
