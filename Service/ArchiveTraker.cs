using Bilibili;
using Feishu;

namespace HuaTuoMain.Service
{
    public record ArchiveTrackInfo
    {
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
        public required long Ptime { get; set; }
        // 创建时间
        public required long Ctime { get; set; }

        public ArchiveTrackInfo Copy()
        {
            ArchiveTrackInfo copy = new ArchiveTrackInfo()
            {
                Bvid = Bvid,
                Title = Title,
                Cover = Cover,
                Tag = Tag,
                Copyright = Copyright,
                Desc = Desc,
                State = State,
                State_desc = State_desc,
                Source = Source,
                Dynamic = Dynamic,
                Ptime = Ptime,
                Ctime = Ctime,
            };
            return copy;
        }
    }

    public class ArchiveTracker
    {
        public string Bvid { get; set; }
        public List<LarkID> Notification { get; set; } = new List<LarkID>();
        public ArchiveTrackInfo LatestStatus { get => _history[^1]; }
        public long LatestUpdate { get; set; }

        private readonly BiliCredential _credential;
        private readonly List<ArchiveTrackInfo> _history = new List<ArchiveTrackInfo>();

        public ArchiveTracker(string bvid, BiliCredential credential)
        {
            Bvid = bvid;
            _credential = credential;
        }

        public async Task Update()
        {
            var info = await BiliAPI.MemberWebInfo(Bvid, _credential);
            var tarck_obj = new ArchiveTrackInfo()
            {
                Bvid = info.Data.Archive.Bvid,
                Title = info.Data.Archive.Title,
                Cover = info.Data.Archive.Cover,
                Tag = info.Data.Archive.Tag,
                Copyright = info.Data.Archive.Copyright,
                Desc = info.Data.Archive.Desc,
                State = info.Data.Archive.State,
                State_desc = info.Data.Archive.State_desc,
                Source = info.Data.Archive.Source,
                Dynamic = info.Data.Archive.Dynamic,
                Ptime = info.Data.Archive.Ptime,
                Ctime = info.Data.Archive.Ctime,
            };
            _history.Add(tarck_obj);
            LatestUpdate = Timestamp.GetTimestamp();
        }

        public void Update(BiliVideoMemberResponse.ArchiveItem info)
        {
            var tarck_obj = new ArchiveTrackInfo()
            {
                Bvid = info.Bvid,
                Title = info.Title,
                Cover = info.Cover,
                Tag = info.Tag,
                Copyright = info.Copyright,
                Desc = info.Desc,
                State = info.State,
                State_desc = info.State_desc,
                Source = info.Source,
                Dynamic = info.Dynamic,
                Ptime = info.Ptime,
                Ctime = info.Ctime,
            };
            _history.Add(tarck_obj);
            LatestUpdate = Timestamp.GetTimestamp();
        }

        public ArchiveTrackerManager.ArchiveDifLog? CompareLatest()
        {
            // count - 1
            var latest = _history[^1];
            var old = _history[^2];
            if (latest.State == old.State) return null;
            return new ArchiveTrackerManager.ArchiveDifLog()
            {
                Type = ArchiveTrackerManager.ArcDifSelect.HavingChanged,
                TrackerInfo = this,
                ObsoleteInfo = old
            };
        }
    }

    public class ArchiveTrackerManager
    {
        public LarkGroup WorkingSpace { get; set; }

        private Dictionary<string, ArchiveTracker> _trackers = new Dictionary<string, ArchiveTracker>();

        public ArchiveTrackerManager(LarkGroup working_space)
        {
            WorkingSpace = working_space;
        }

        public async Task MainLoop()
        {
            while (_trackers.Count > 0)
            {
                await Task.Delay(1000 * 60);
                ArchiveDifLog[] refresh = await UpdateAll();
                foreach (var log in refresh)
                {
                    if (log.Type == ArcDifSelect.HavingChanged)
                    {
                        foreach (var user in log.TrackerInfo.Notification)
                        {
                            
                        }
                    }
                }
            }
        }

        public void Stop()
        {
            lock (this)
            {
                _trackers.Clear();
            }
        }

        public enum ArcDifSelect
        {
            NewVideo,
            HavingChanged
        }

        public record ArchiveDifLog
        {
            public required ArcDifSelect Type { get; set; }
            public required ArchiveTracker TrackerInfo { get; set; }
            public required ArchiveTrackInfo ObsoleteInfo { get; set; }
        }

        public async Task Initialize()
        {
            var current_list = await BiliAPI.ArchiveList(WorkingSpace.botApp.biliCredential, null);
            foreach (var video in current_list.Data.Arc_audits)
            {
                var tracker = new ArchiveTracker(video.Archive.Bvid, WorkingSpace.botApp.biliCredential);
                tracker.Update(video.Archive);
                _trackers.Add(video.Archive.Bvid, tracker);
            }
        }

        public ArchiveTracker? TrackerHandle(string bvid)
        {
            if (_trackers.TryGetValue(bvid, out var tracker))
                return tracker;
            return null;
        }

        public async Task<ArchiveDifLog[]> UpdateAll()
        {
            var archive_log = new List<ArchiveDifLog>();
            var current_list = await BiliAPI.ArchiveList(WorkingSpace.botApp.biliCredential, null);
            foreach (var tracker in _trackers)
            {
                var data = current_list.Data.Search(tracker.Value.Bvid);
                if (data == null)
                    await tracker.Value.Update();
                else
                    tracker.Value.Update(data.Archive);
                var cmp = tracker.Value.CompareLatest();
                if (cmp != null) archive_log.Add(cmp);
            }
            foreach (var video in current_list.Data.Arc_audits)
            {
                if (_trackers.ContainsKey(video.Archive.Bvid))
                    continue;
                var tker = new ArchiveTracker(video.Archive.Bvid, WorkingSpace.botApp.biliCredential);
                tker.Update(video.Archive);
                _trackers.Add(video.Archive.Bvid, tker);
                archive_log.Add(new ArchiveDifLog()
                {
                    Type = ArcDifSelect.NewVideo,
                    TrackerInfo = tker,
                    ObsoleteInfo = tker.LatestStatus
                });
            }
            return archive_log.ToArray();
        }
    }
}
