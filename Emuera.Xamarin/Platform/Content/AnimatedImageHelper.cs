using MinorShift.Emuera.Runtime.Utils;
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

            // 包内图片：整条读到内存再解；未命中（含磁盘优先／两侧都没有）时 codec 为 null
            var pakBytes = ResourceStore.ReadAllBytes(filepath);
            using var data = pakBytes != null ? SKData.CreateCopy(pakBytes) : null;
            using var codec = data != null ? SKCodec.Create(data) : SKCodec.Create(filepath);
            if (codec == null)
            {
                // SkiaSharp 无法识别格式时，尝试平台回退（字节优先、路径兜底）
                var fallbackInfo = PlatformInterop.GetAnimInfoFallback(pakBytes, filepath);
                if (fallbackInfo != null)
                {
                    width = fallbackInfo.Value.Width;
                    height = fallbackInfo.Value.Height;
                    frameCount = fallbackInfo.Value.FrameCount;
                    delays = fallbackInfo.Value.Delays;
                    return frameCount > 1;
                }
                return false;
            }

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
            // 包内图片：整条读到内存再解；未命中（含磁盘优先／两侧都没有）时 codec 为 null
            var pakBytes = ResourceStore.ReadAllBytes(filepath);
            using var data = pakBytes != null ? SKData.CreateCopy(pakBytes) : null;
            using var codec = data != null ? SKCodec.Create(data) : SKCodec.Create(filepath);
            if (codec == null || codec.FrameCount <= 1)
            {
                // SkiaSharp 无法解码动画时，尝试平台回退（字节优先、路径兜底）
                var fallbackFrames = PlatformInterop.DecodeAnimatedFallback(pakBytes, filepath);
                return fallbackFrames;
            }

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
