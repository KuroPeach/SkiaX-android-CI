using SkiaSharp;
using System.Collections.Generic;
using System.IO;

namespace MinorShift.Emuera.Content
{
    public static class AnimatedImageHelper
    {
        public static bool GetAnimInfo(string filepath, out int width, out int height, out int frameCount, out int[] delays)
        {
            width = height = frameCount = 0;
            delays = null;

            if (!File.Exists(filepath)) return false;

            using var codec = SKCodec.Create(filepath);
            if (codec == null) return false;

            width = codec.Info.Width;
            height = codec.Info.Height;
            frameCount = codec.FrameCount;

            if (frameCount > 1)
            {
                delays = new int[frameCount];
                for (int i = 0; i < frameCount; i++)
                {
                    int duration = codec.FrameInfo[i].Duration;
                    delays[i] = duration > 0 ? duration : 100;
                }
                return true;
            }
            return false;
        }

        public static List<(SKBitmap Bitmap, int Delay)> Decode(string filepath)
        {
            if (!File.Exists(filepath)) return null;

            using var codec = SKCodec.Create(filepath);
            if (codec == null || codec.FrameCount <= 1)
                return null;

            var frames = new List<(SKBitmap, int)>();
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);

            var decodedFrames = new SKBitmap[codec.FrameCount];

            using var bitmap = new SKBitmap(info);
            var ptr = bitmap.GetPixels();

            for (int i = 0; i < codec.FrameCount; i++)
            {
                var frameInfo = codec.FrameInfo[i];
                int delay = frameInfo.Duration > 0 ? frameInfo.Duration : 100;

                int reqFrame = frameInfo.RequiredFrame;
                if (reqFrame != -1 && decodedFrames[reqFrame] != null)
                {
                    using var canvas = new SKCanvas(bitmap);
                    using var copyPaint = new SKPaint { BlendMode = SKBlendMode.Src };
                    canvas.DrawBitmap(decodedFrames[reqFrame], 0, 0, copyPaint);

                    var reqFrameInfo = codec.FrameInfo[reqFrame];
                    if (reqFrameInfo.DisposalMethod == SKCodecAnimationDisposalMethod.RestoreBackgroundColor)
                    {
                        // SkiaSharp 2.88 没有 FrameRect，退化为清除整个画布
                        using var clearPaint = new SKPaint { BlendMode = SKBlendMode.Src, Color = SKColors.Transparent };
                        canvas.DrawRect(0, 0, bitmap.Width, bitmap.Height, clearPaint);
                    }
                }
                else
                {
                    bitmap.Erase(SKColors.Transparent);
                }

                var options = reqFrame == -1 ? new SKCodecOptions(i) : new SKCodecOptions(i, reqFrame);
                codec.GetPixels(info, ptr, options);

                var frameCopy = bitmap.Copy();
                decodedFrames[i] = frameCopy;
                frames.Add((frameCopy, delay));
            }

            return frames;
        }
    }
}
