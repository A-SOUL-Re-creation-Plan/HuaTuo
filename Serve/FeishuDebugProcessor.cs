using Feishu.Event;
using Feishu.Message;
using Feishu.Serve;
using Feishu.Serve.EventClass;
using Hardware.Info;
using Microsoft.ML.Data;
using MlModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace Feishu.Debug
{
    public class FeishuDebugProcessor : CommandProcessor
    {
        public FeishuDebugProcessor(BotApp botApp, LarkGroup debugGroup) : base(botApp, debugGroup) { }
        public FeishuDebugProcessor(BotApp botApp, LarkID debugGroup) : base(botApp, debugGroup) { }

        public static string PrintAllProperties(object obj)
        {
            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            return JsonSerializer.Serialize(obj, options);
        }

        [CommandMarker("info")]
        public void Info(EventContent<MessageReceiveBody> cEventContent)
        {
            if (cEventContent.Event.Message.Parent_id == null) return;
            var msg = botApp.Message.GetMessage(cEventContent.Event.Message.Parent_id).Result; if (msg == null) return;
            larkGroup.SendMessage(new TextContent(PrintAllProperties(msg))).Wait();
        }

        [CommandMarker("status")]
        public void Status(EventContent<MessageReceiveBody> cEventContent)
        {
            var hwinfo = new HardwareInfo();
            hwinfo.RefreshMemoryStatus();
            double percent = (double)hwinfo.MemoryStatus.AvailablePhysical / (double)hwinfo.MemoryStatus.TotalPhysical;
            larkGroup.SendMessage(new TextContent($"RAM：{1-percent}%")).Wait();
        }

        [CommandMarker("model")]
        public void SchedulerPredict(EventContent<MessageReceiveBody> cEventContent)
        {
            if (cEventContent.Event.Message.Parent_id == null) return;
            var msg = botApp.Message.GetMessage(cEventContent.Event.Message.Parent_id).Result; if (msg == null) return;
            msg.Data.TryGetValue("items", out var msg_item);
            if (msg_item == null || !msg_item[0].Msg_type.Equals("image")) return;
            JsonNode msg_content = JsonNode.Parse(msg_item[0].Body.Content)!;
            Byte[] image_bytes = botApp.Message.DownloadFromMessage(cEventContent.Event.Message.Parent_id, msg_content["image_key"]!.ToString()).Result;

            Stream img_stream = new MemoryStream(image_bytes);

            BasicSchedule.ModelInput modelInput = new BasicSchedule.ModelInput()
            {
                Image = MLImage.CreateFromStream(img_stream)
            };

            BasicSchedule.ModelOutput modelOutput = BasicSchedule.Predict(modelInput);

            if (modelOutput.PredictedBoundingBoxes == null)
            {
                Console.WriteLine("No Predicted Bounding Boxes");
                return;
            }

            var img = BasicSchedule.DrawBoudingBox(new MemoryStream(image_bytes), modelOutput);
            var key = botApp.Message.UploadImage(img).Result;

            larkGroup.SendMessage(new ImageContent(key)).Wait();
            // larkGroup.SendMessage(Text).Wait();
        }
    }
}
