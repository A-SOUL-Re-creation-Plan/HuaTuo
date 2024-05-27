namespace HuaTuoMain.Service
{
    public enum ASOUL_Member
    {
        Ava = 0x1,
        Bella = 0x2,
        Diana = 0x4,
        Eileen = 0x8,
        ASOUL = 0xF
    }

    /// <summary>
    /// 简介生成工具
    /// 对文字的数字化处理是麻烦的，if真好用
    /// </summary>
    public static class DespGenerator
    {
        private const string LiveRoom_Ava = "https://live.bilibili.com/22625025/";
        private const string LiveRoom_Bella = "https://live.bilibili.com/22632424/";
        private const string LiveRoom_Diana = "https://live.bilibili.com/22637261/";
        private const string LiveRoom_Eileen = "https://live.bilibili.com/22625027/";
        private static readonly string[] LiveRoomInfoList = { LiveRoom_Ava, LiveRoom_Bella, LiveRoom_Diana, LiveRoom_Eileen };

        private const string LiveDesp_Ava = "\n向晚大魔王\n直播间：https://live.bilibili.com/22625025/\n个人主页：https://space.bilibili.com/672346917/";
        private const string LiveDesp_Bella = "\n贝拉kira\n直播间：https://live.bilibili.com/22632424/\n个人主页：https://space.bilibili.com/672353429/";
        private const string LiveDesp_Diana = "\n嘉然今天吃什么\n直播间：https://live.bilibili.com/22637261/\n个人主页：https://space.bilibili.com/672328094/";
        private const string LiveDesp_Eileen = "\n乃琳Queen\n直播间：https://live.bilibili.com/22625027/\n个人主页：https://space.bilibili.com/672342685/";
        private static readonly string[] LiveDespInfoList = { LiveDesp_Ava, LiveDesp_Bella, LiveDesp_Diana, LiveDesp_Eileen };

        public static string ASOUL_InfoList(byte num) => num switch
        {
            0x1 => "向晚",
            0x2 => "贝拉",
            0x4 => "嘉然",
            0x8 => "乃琳",

            0x3 => "向晚&贝拉",
            0x5 => "嘉然&向晚",
            0x9 => "向晚&乃琳",
            0x6 => "嘉然&贝拉",
            0xA => "乃琳&贝拉",
            0xC => "嘉然&乃琳",

            0x10 => "A-SOUL夜谈",
            0x20 => "A-SOUL小剧场",
            0x30 => "A-SOUL游戏室",

            _ => "A-SOUL"
        };

        public static int ASOUL_FeishuColor(string mem) => mem switch
        {
            "Ava" => -15417089, // Ava
            "Bella" => -562844,   // Bella
            "Diana" => -963671,   // Diana
            "Eileen" => -10392859, // Eileen
            _ => -14838
        };

        /// <summary>
        /// 从（日程主题）中分析简介中成员部分的信息
        /// </summary>
        /// <param name="summary">日程主题</param>
        /// <returns>byte</returns>
        public static byte ParseLiveMember(string summary, byte all_mem = 0xF)
        {
            byte mem = 0;
            if (summary.Contains("夜谈")) mem |= 0x1;
            else if (summary.Contains("小剧场")) mem |= 0x2;
            else if (summary.Contains("游戏室")) mem |= 0x3;
            mem <<= 4;
            if (summary.Contains("向晚")) mem |= 0x1;
            if (summary.Contains("贝拉")) mem |= 0x2;
            if (summary.Contains("嘉然")) mem |= 0x4;
            if (summary.Contains("乃琳")) mem |= 0x8;
            return (mem & 0xF) != 0 ? mem : (byte)(all_mem | mem);
            //return ASOUL_MemberList((byte)(mem & 0xF)) + ASOUL_MemberList((byte)(mem | 0xF));
        }

        public static string GenerateDesp(string summary, string title, DateTime date, byte all_mem = 0xF)
        {
            string desp = "";
            // 1.拼接时间信息
            desp += $"直播日期：{date.Year}年{date.Month}月{date.Day}日\n";
            // 2.拼接直播信息
            byte live_info = ParseLiveMember(summary, all_mem);
            desp += ASOUL_InfoList(live_info) + $"【{title.Replace("\n", "")}】\n";
            desp += "--------------------------------------------------------";
            // 3.拼接成员信息
            foreach (string item in LiveDespInfoList)
            {
                if ((live_info & 1) != 0) { desp += item; desp += "\n"; }
                live_info >>>= 1;
            }
            return desp.TrimEnd('\n');
        }

        public static string GenerateLiveRoom(byte num)
        {
            for (byte i = 0; i < 4; i++)
            {
                if (num == 1) return LiveRoomInfoList[i];
                num >>>= 1;
            }
            return "Out of Index";
        }

        public static string GenerateLiveRoom(string mem)
        {
            if (mem.Equals("向晚")) return LiveRoomInfoList[0];
            else if (mem.Equals("贝拉")) return LiveRoomInfoList[1];
            else if (mem.Equals("嘉然")) return LiveRoomInfoList[2];
            else if (mem.Equals("乃琳")) return LiveRoomInfoList[3];
            return "No Member Equals";
        }

        public static string GenerateLiveRoom(int color) => color switch
        {
            -15417089 => LiveRoomInfoList[0],
            -562844 => LiveRoomInfoList[1],
            -963671 => LiveRoomInfoList[2],
            -10392859 => LiveRoomInfoList[3],

            0x9AC8E2 => LiveRoomInfoList[0],
            0xDB7D74 => LiveRoomInfoList[1],
            0xE799B0 => LiveRoomInfoList[2],
            0x576690 => LiveRoomInfoList[3],

            _ => "Color Not Supported"
        };
    }
}
