using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using static MlModel.BasicSchedule;

namespace HuaTuo.Service
{
    public static class ScheduleProcess
    {
        // 加载字体
        private static readonly FontFamily fontFamily = new FontCollection().Add("./fonts/msyh.ttf");

        /// <summary>
        /// 可视化模块
        /// </summary>
        /// <param name="ImageStream"></param>
        /// <param name="modelOutput"></param>
        /// <returns></returns>
        public static Stream DrawBoudingBox(Stream ImageStream, ModelOutput modelOutput)
        {
            ImageStream.Seek(0, SeekOrigin.Begin);
            var boxes = ModelPredictedBox.Create(modelOutput);
            var font = fontFamily.CreateFont(50);
            using Image img = Image.Load(ImageStream);
            foreach (var box in boxes)
            {
                var text = $"{box.Label} {box.Score:F2}%";
                // 画框框
                img.Mutate(x => x
                    .DrawPolygon(Color.Black.WithAlpha(0.8f), 8.0f,
                    new PointF(box.XTop, box.YTop),
                    new PointF(box.XBottom, box.YTop),
                    new PointF(box.XBottom, box.YBottom),
                    new PointF(box.XTop, box.YBottom)
                    ));
                // 画文本
                var origin_point = new PointF(box.XTop, box.YTop - 50);

            }
            var stream = new MemoryStream();
            img.Save(stream, new JpegEncoder());
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
    }
}