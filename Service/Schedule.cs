﻿using Feishu;
using Feishu.Calendar;
using Feishu.Calendar.CalendarBody;
using Feishu.Message;
using HuaTuoMain.CloudServe;
using HuaTuoMain.Service;
using Microsoft.ML.Data;
using MlModel;
using RestSharp;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace HuaTuo.Service
{
    public class ScheduleProcess
    {
        // 加载字体
        private static readonly FontFamily fontFamily = new FontCollection().Add("./fonts/msyh.ttf");

        // 加载ID
        private string[] FeishuIDs = Array.Empty<string>();

        private readonly MemoryStream rawImage;

        public ScheduleProcess(Stream image_stream)
        {
            using Image image = Image.Load(image_stream);
            if (image.Width != 3000 || image.Height != 2000)
                image.Mutate(x => x.Resize(3000, 2000));
            rawImage = new MemoryStream();
            image.SaveAsJpeg(rawImage);
        }

        public async Task<ProcessedResult> StartParsing(BotApp app)
        {
            FeishuIDs = [app.configFile.Config.FeishuID_Ava,
                app.configFile.Config.FeishuID_Bella,
                app.configFile.Config.FeishuID_Diana,
                app.configFile.Config.FeishuID_Eileen,
                app.configFile.Config.FeishuID_Jihua,
                app.configFile.Config.FeishuID_ASOUL];
            // OCR识别
            rawImage.Seek(0, SeekOrigin.Begin);
            var ocr_tool_task = app.serviceOCR.RequestAsync(rawImage.ToArray());
            // 复制一份流防止资源被释放了
            rawImage.Seek(0, SeekOrigin.Begin);
            Stream predict_stream = new MemoryStream();
            rawImage.CopyTo(predict_stream);
            predict_stream.Seek(0, SeekOrigin.Begin);
            var input = new BasicSchedule.ModelInput()
            {
                Image = MLImage.CreateFromStream(predict_stream)
            };
            // 运行模型
            BasicSchedule.ModelOutput modelOutput = BasicSchedule.Predict(input);
            // var modelPredicted = BasicSchedule.ModelPredictedBox.Create(modelOutput);
            // 稍微筛选一下
            var modelPredicted = BasicSchedule.ModelPredictedBox.CreateWithFilter(modelOutput, 0.94);

            rawImage.Seek(0, SeekOrigin.Begin);
            var ocr_tool = new OcrTools(await ocr_tool_task);

            // 用于反馈创建的结果
            ProcessedResult result = new ProcessedResult();
            List<ProcessedResult> task_results = new List<ProcessedResult>();
            // List<Task<ProcessedResult>> tasks = new List<Task<ProcessedResult>>();
            result.TotalDetected = modelPredicted.Length;
            // 开始创建日程
            /**
             * 这种写法会被判为 Request failed with status code TooManyRequests
             * 我能怎么办，我也很无奈
             * foreach (var block in modelPredicted)
             * {
             *     tasks.Add(ParseIndividual(app, block, ocr_tool));
             * }
             * Task.WaitAll(tasks.ToArray());
            */
            foreach (var block in modelPredicted)
            {
                double hour = 3.0;
                // 尝试一下周边的日程数量吧
                BasicSchedule.ModelPredictedBox[] nearby_events = block.DetectionsRanging(modelPredicted);
                // 怀疑4人同一天直播的情况
                if (nearby_events.Length > 3) hour = 1.0;
                // 别超速了
                task_results.Add(await ParseIndividual(app, block, ocr_tool, hour));
            }
            // 拼接信息
            foreach (var task in task_results)
            {
                result.Errors = result.Errors.Concat(task.Errors).ToList();
                result.ProcessedResults.Add(task);
                if (task.Success) result.SuccessedCreated++;
            }
            // 画图
            DrawBoudingBox(rawImage, ref result);
            return result;
        }

        /// <summary>
        /// 拉取单个日程
        /// </summary>
        /// <param name="app">app实例</param>
        /// <param name="block">该日程块的信息</param>
        /// <param name="ocr_tool">OCR信息</param>
        /// <param name="hour">可选在默认情况下日程的时长（小时）</param>
        /// <returns>ProcessedResult</returns>
        private async Task<ProcessedResult> ParseIndividual(BotApp app,
                                                            BasicSchedule.ModelPredictedBox block,
                                                            OcrTools ocr_tool, double? hour = null)
        {
            // 用于反馈创建的结果
            ProcessedResult result = new ProcessedResult()
            {
                Box = block
            };
            var ocr_boxes = ocr_tool.SearchRectangle((int)block.XTop, (int)block.YTop,
                                                         (int)(block.XBottom - block.XTop), (int)(block.YBottom - block.YTop)).ToList();
            // 暂不处理橙色标签的直播
            if (block.Label == "Others")
            {
                result.Errors.Add(PostContent.NewText("注意：日程表存在其他类型日程，已跳过处理"));
                return result;
            }
            // 分离日程表中的信息
            bool mark = true;
            DateTime live_time = new DateTime();
            List<LarkID> attendees = new List<LarkID>();
            CalendarEventEditable calendarEvent = new CalendarEventEditable()
            {
                Summary = "",
                Description = "",
                Start_time = new StartTime() { Timestamp = "" },
                End_time = new EndTime() { Timestamp = "" },
                Color = DespGenerator.ASOUL_FeishuColor(block.Label),
                Vchat = new Vchat() { Vc_type = "no_meeting" },
                Attendee_ability = "can_modify_event",
                Visibility = "public",
                Reminders = [new Reminder() { Minutes = 5 }]
            };
            // 逐个循环识别，保证准确
            // 识别时间
            foreach (var box in ocr_boxes)
            {
                // 顺便进行一个预处理
                // 吐槽一下OCR给出的结果真是不靠谱啊，首尾还能带空格的？！
                box.DetectedText = box.DetectedText.Trim();
                if (DateTime.TryParseExact(box.DetectedText, "HH:mm", null, System.Globalization.DateTimeStyles.None, out live_time))
                { mark = false; ocr_boxes.Remove(box); break; }
                if (DateTime.TryParseExact(box.DetectedText, "HH：mm", null, System.Globalization.DateTimeStyles.None, out live_time))
                { mark = false; ocr_boxes.Remove(box); break; }
            }
            if (mark)
            {
                result.Errors.Add(PostContent.NewText("警告：一个日程的时间无法被识别到，已跳过该日程", ["bold"]));
                return result;
            }
            else
            {
                // 识别成功，计算时长
                // 日程表时基
                DateTime schedule_time = ocr_tool.SearchTimeBase();
                // 计算偏移
                int offset = GeometryExtension.CalcuOffset(block);
                int offset_day = offset < 0 ? -offset + 3 : offset - 1;
                DateTime offset_date = schedule_time.AddDays(offset_day);
                if (live_time.Hour == 19 && live_time.Minute == 30)
                {
                    calendarEvent.Start_time.Timestamp = Timestamp.DateToTimestamp(
                        new DateTime(offset_date.Year, offset_date.Month, offset_date.Day, 19, 20, 0)).ToString();
                    calendarEvent.End_time.Timestamp = Timestamp.DateToTimestamp(
                        new DateTime(offset_date.Year, offset_date.Month, offset_date.Day, 21, 10, 0)).ToString();
                }
                else if (live_time.Hour == 21 && live_time.Minute == 0)
                {
                    calendarEvent.Start_time.Timestamp = Timestamp.DateToTimestamp(
                        new DateTime(offset_date.Year, offset_date.Month, offset_date.Day, 20, 50, 0)).ToString();
                    calendarEvent.End_time.Timestamp = Timestamp.DateToTimestamp(
                        new DateTime(offset_date.Year, offset_date.Month, offset_date.Day, 22, 40, 0)).ToString();
                }
                else
                {
                    hour ??= 3.0;
                    DateTime timebase = new DateTime(offset_date.Year, offset_date.Month, offset_date.Day, live_time.Hour, live_time.Minute, 0);
                    calendarEvent.Start_time.Timestamp = Timestamp.DateToTimestamp(timebase.AddMinutes(-10)).ToString();
                    calendarEvent.End_time.Timestamp = Timestamp.DateToTimestamp(timebase.AddHours((double)hour)).ToString();
                }
            }
            mark = true;
            // 识别类型
            foreach (var box in ocr_boxes)
            {
                if (box.DetectedText.Contains("节目") || box.DetectedText.Contains("日常") || box.DetectedText.Contains("推广"))
                { mark = false; ocr_boxes.Remove(box); break; }
            }
            if (mark)
            {
                result.Errors.Add(PostContent.NewText("警告：一个日程的标签无法识别，该日程仍会被创建，请注意标签可能被拼接在标题内", ["bold"]));
                // return result;
            }
            mark = true;
            // 识别主题
            foreach (var box in ocr_boxes)
            {
                var parsed = DespGenerator.ParseLiveMember(box.DetectedText);
                if (parsed == 0xF) continue;
                else
                {
                    ocr_boxes.Remove(box);
                    mark = false;
                    calendarEvent.Summary = box.DetectedText;
                    if ((parsed >>> 4) != 0)
                    {
                        // 团播 不做修改
                        attendees.Add(new LarkID(FeishuIDs[5]));
                        break;
                    }
                    if (parsed != 0x1 && parsed != 0x2 && parsed != 0x4 && parsed != 0x8)
                        // 双播
                        calendarEvent.Summary = calendarEvent.Summary.Replace("直播", "双播");
                    else
                        // 单播
                        calendarEvent.Summary = calendarEvent.Summary.Replace("直播", "单播");
                    // 查找成员
                    for (byte i = 0; i < 4; i++)
                    {
                        if ((parsed & 1) == 1) attendees.Add(new LarkID(FeishuIDs[i]));
                        parsed >>>= 1;
                    }
                    attendees.Add(new LarkID(FeishuIDs[4]));
                    break;
                }
            }
            if (mark)
            {
                result.Errors.Add(PostContent.NewText("警告：一个日程找不到主题，已跳过该日程", ["bold"]));
                return result;
            }
            // 拼接标题
            foreach (var box in ocr_boxes)
                calendarEvent.Description += box.DetectedText;
            // 检查冲突
            var check_start = Timestamp.DateToTimestamp(
                Timestamp.TimestampToDate(calendarEvent.Start_time.Timestamp).AddMinutes(25)).ToString();
            var check_end = Timestamp.DateToTimestamp(
                Timestamp.TimestampToDate(calendarEvent.End_time.Timestamp).AddMinutes(-25)).ToString();
            var events = await app.Calendar.GetEventList(check_start, check_end);
            if (events.Data.Items.Length > 0)
            {
                result.Errors.Add(PostContent.NewText($"注意：{calendarEvent.Summary}存在冲突，已跳过该日程"));
                return result;
            }
            await CreateIndividual(calendarEvent, app, attendees.ToArray());
            result.Success = true;
            result.CalendarEvent = calendarEvent;
            return result;
        }

        private async Task CreateIndividual(CalendarEventEditable calendarEvent, BotApp app, LarkID[] attendees)
        {
            // 创建日程
            var new_event = await app.Calendar.CreateEvent(calendarEvent);
            // 拉取小伙伴
            attendees = attendees.Append(new LarkID(app.configFile.Config.Bot_Open_id)).ToArray();
            await app.Calendar.AddAttendees(new_event.Data.Event.Event_id, attendees);
            // 删除自己
            var attendees_list = await app.Calendar.GetAttendeesList(new_event.Data.Event.Event_id);
            foreach (var person in attendees_list.Data.Items)
            {
                if (person.User_id == app.configFile.Config.Bot_Open_id)
                {
                    await app.Calendar.DeleteAttendeesUser(new_event.Data.Event.Event_id, [person.Attendee_id]);
                    break;
                }
            }
        }

        public static async Task<Stream> DownloadImage(string img_url, RestClient client)
        {
            RestRequest request = new RestRequest(img_url);
            request.AddHeader("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/63.0.3239.108");
            var resp = await client.GetAsync(request);
            if (resp.RawBytes == null) throw new Exception("Null Stream");
            return new MemoryStream(resp.RawBytes);
        }

        /// <summary>
        /// 可视化模块
        /// </summary>
        /// <param name="ImageStream"></param>
        /// <param name="modelOutput"></param>
        /// <returns></returns>
        public static void DrawBoudingBox(Stream ImageStream, ref ProcessedResult result)
        {
            ImageStream.Seek(0, SeekOrigin.Begin);
            var font = fontFamily.CreateFont(50);
            using Image img = Image.Load(ImageStream);
            Color color = Color.Black;
            foreach (var event_result in result.ProcessedResults)
            {
                var box = event_result.Box!;
                var text = $"{box.Label} {box.Score:F2}";
                if (event_result.Success) color = Color.LightGreen;
                else color = Color.OrangeRed;
                TextOptions textOptions = new TextOptions(font)
                {
                    Origin = new PointF(box.XTop + 10, box.YTop - 76),
                };
                var size = TextMeasurer.MeasureSize(text, textOptions);
                PointF[] lay_points = [
                    new PointF(box.XTop, box.YTop - 80),
                    new PointF(box.XTop + size.Width + 20, box.YTop - 80),
                    new PointF(box.XTop + size.Width + 20, box.YTop),
                    new PointF(box.XTop, box.YTop)];
                PointF[] points = [
                    new PointF(box.XTop, box.YTop),
                    new PointF(box.XBottom, box.YTop),
                    new PointF(box.XBottom, box.YBottom),
                    new PointF(box.XTop, box.YBottom)];
                img.Mutate(x =>
                {
                    x.FillPolygon(color, lay_points);
                    x.DrawPolygon(color, 10.0f, points);
                    x.DrawText(text, font, Color.White, textOptions.Origin);
                });
            }
            var stream = new MemoryStream();
            img.Save(stream, new JpegEncoder());
            stream.Seek(0, SeekOrigin.Begin);
            result.Image = stream;
        }

        public record ProcessedResult
        {
            // 汇总使用
            public int TotalDetected { get; set; } = 0;
            public int SuccessedCreated { get; set; } = 0;
            public List<object> Errors { get; set; } = new List<object>();
            public MemoryStream Image { get; set; } = new MemoryStream();
            public List<ProcessedResult> ProcessedResults { get; set; } = new List<ProcessedResult>();

            // 单独使用
            public bool Success { get; set; } = false;
            public CalendarEventEditable? CalendarEvent { get; set; } = null;
            public BasicSchedule.ModelPredictedBox? Box { get; set; } = null;
        }
    }
}