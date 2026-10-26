using MinorShift.Emuera.UI.Game.Image;
using MinorShift.Emuera.UI.Game.Rendering;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MinorShift.Emuera.UI.Ewp;

/// <summary>
/// EWP R-3a：SETIMAGELAYER 时冻结图层像素的不可变快照。
/// rev 标识一次快照，不标识 ASprite 对象；旧 rev 永远返回旧像素，快照淘汰只回 expired。
/// 所有 Capture/TryEncode/Clear 都在逻辑 UI 线程调用（EwpHost Dispatch / ImageLayer 回调）。
/// </summary>
internal sealed class EwpSpriteExport
{
    private const int DefaultMaxEntries = 96;
    private const long DefaultMaxCacheBytes = 32L * 1024 * 1024;
    private const int DefaultMaxSide = 4096;
    private const long DefaultMaxPixels = 8_388_608;
    private const int DefaultMaxEncodedBytes = 4 * 1024 * 1024;
    private const int ExpiredKeyLimit = 512;

    private sealed class Snapshot
    {
        public string Texture;
        public long Rev;
        public int W;
        public int H;
        public SKBitmap Bitmap;
        public string FailCode;
        public string Detail;
        public long Bytes;
    }

    internal readonly record struct SpriteExportStats(
        bool Enabled,
        int Snapshots,
        long Bytes,
        long Requests,
        long Failures,
        long LastRev);

    private readonly object gate = new();
    private readonly Dictionary<(string Texture, long Rev), Snapshot> map = new();
    private readonly LinkedList<(string Texture, long Rev)> lru = new();
    private readonly Dictionary<(string Texture, long Rev), LinkedListNode<(string Texture, long Rev)>> lruNodes = new();
    private readonly HashSet<(string Texture, long Rev)> expired = new();
    private readonly Queue<(string Texture, long Rev)> expiredOrder = new();

    private readonly int maxEntries;
    private readonly long maxCacheBytes;
    private readonly int maxSide;
    private readonly long maxPixels;
    private readonly int maxEncodedBytes;

    private volatile bool enabled;
    private long revSeq;
    private long cacheBytes;
    private long requests;
    private long failures;

    public EwpSpriteExport()
    {
        maxEntries = EnvInt("ERAWIRE_SPRITE_CACHE_ENTRIES", DefaultMaxEntries);
        maxCacheBytes = EnvLong("ERAWIRE_SPRITE_CACHE_MB", DefaultMaxCacheBytes / (1024 * 1024)) * 1024 * 1024;
        maxSide = EnvInt("ERAWIRE_SPRITE_MAX_SIDE", DefaultMaxSide);
        maxPixels = EnvLong("ERAWIRE_SPRITE_MAX_PIXELS", DefaultMaxPixels);
        maxEncodedBytes = EnvInt("ERAWIRE_SPRITE_MAX_BYTES", DefaultMaxEncodedBytes);
    }

    public bool Enabled
    {
        get => enabled;
        set => enabled = value;
    }

    /// <summary>捕获一个图层快照，返回该次 set 的 rev；未启用返回 0。
    /// 失败也分配 rev 并写失败墓碑，FE 请求时得到明确 fail code。</summary>
    public long Capture(ImageLayer layer)
    {
        if (!enabled || layer == null || layer.Image == null)
            return 0;

        string texture = (layer.Image.Name ?? "").ToUpperInvariant();
        long rev = System.Threading.Interlocked.Increment(ref revSeq);
        var snapshot = new Snapshot { Texture = texture, Rev = rev };

        if (string.IsNullOrEmpty(texture))
        {
            snapshot.FailCode = "not_found";
            snapshot.Detail = "empty_texture";
        }
        else
        {
            var sprite = layer.Image;
            int w = layer.Width > 0 ? layer.Width : Math.Max(0, sprite.DestBaseSize.Width);
            int h = layer.Height > 0 ? layer.Height : Math.Max(0, sprite.DestBaseSize.Height);
            snapshot.W = w;
            snapshot.H = h;

            if (sprite is not (SpriteG or SpriteF))
            {
                snapshot.FailCode = "not_exportable";
                snapshot.Detail = "animated";
            }
            else if (!sprite.IsCreated || w <= 0 || h <= 0)
            {
                snapshot.FailCode = "not_exportable";
                snapshot.Detail = "empty";
            }
            else if (layer.ColorMatrix != null)
            {
                snapshot.FailCode = "not_exportable";
                snapshot.Detail = "color_matrix";
            }
            else if (w > maxSide || h > maxSide || (long)w * h > maxPixels)
            {
                snapshot.FailCode = "too_large";
                snapshot.Detail = "dimensions";
            }
            else
            {
                SKBitmap bitmap = null;
                try
                {
                    bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
                    using var canvas = new SKCanvas(bitmap);
                    canvas.Clear(SKColors.Transparent);
                    sprite.GraphicsDraw(canvas, new Rectangle(0, 0, w, h));
                    canvas.Flush();
                    snapshot.Bitmap = bitmap;
                    snapshot.Bytes = (long)w * h * 4;
                }
                catch
                {
                    bitmap?.Dispose();
                    snapshot.FailCode = "not_exportable";
                    snapshot.Detail = "capture_error";
                }
            }
        }

        lock (gate)
        {
            if (snapshot.Bitmap != null && snapshot.Bytes > maxCacheBytes)
            {
                snapshot.Bitmap.Dispose();
                snapshot.Bitmap = null;
                snapshot.Bytes = 0;
                snapshot.FailCode = "too_large";
                snapshot.Detail = "cache";
            }
            AddLocked(snapshot);
        }
        return rev;
    }

    /// <summary>按 (texture,rev) 取快照并编码 PNG。不返回近似图；淘汰/未知/不支持全部失败。</summary>
    public bool TryEncode(string texture, long rev, out byte[] png, out int width, out int height,
        out string code, out string detail)
    {
        png = null;
        width = 0;
        height = 0;
        code = null;
        detail = null;
        string keyTexture = (texture ?? "").ToUpperInvariant();
        var key = (keyTexture, rev);

        lock (gate)
        {
            requests++;
            if (!map.TryGetValue(key, out var snapshot))
            {
                failures++;
                code = expired.Contains(key) ? "expired" : "not_found";
                return false;
            }
            TouchLocked(key);
            width = snapshot.W;
            height = snapshot.H;
            if (snapshot.FailCode != null)
            {
                failures++;
                code = snapshot.FailCode;
                detail = snapshot.Detail;
                return false;
            }
            if (snapshot.Bitmap == null)
            {
                failures++;
                code = "not_exportable";
                detail = "empty";
                return false;
            }
            try
            {
                using var image = SKImage.FromBitmap(snapshot.Bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                png = data.ToArray();
                if (png.Length > maxEncodedBytes)
                {
                    png = null;
                    width = 0;
                    height = 0;
                    failures++;
                    code = "too_large";
                    detail = "encoded";
                    return false;
                }
                return true;
            }
            catch
            {
                png = null;
                width = 0;
                height = 0;
                failures++;
                code = "not_exportable";
                detail = "encode_error";
                return false;
            }
        }
    }

    public SpriteExportStats Stats()
    {
        lock (gate)
        {
            return new SpriteExportStats(enabled, map.Count, cacheBytes, requests, failures, System.Threading.Interlocked.Read(ref revSeq));
        }
    }

    /// <summary>全量清场时释放快照；rev 序列不重置，避免同会话 rev 复用。</summary>
    public void Clear()
    {
        lock (gate)
        {
            foreach (var snapshot in map.Values)
                snapshot.Bitmap?.Dispose();
            map.Clear();
            lru.Clear();
            lruNodes.Clear();
            expired.Clear();
            expiredOrder.Clear();
            cacheBytes = 0;
        }
    }

    private void AddLocked(Snapshot snapshot)
    {
        var key = (snapshot.Texture, snapshot.Rev);
        var node = lru.AddLast(key);
        lruNodes[key] = node;
        map[key] = snapshot;
        cacheBytes += snapshot.Bytes;

        while (map.Count > maxEntries || cacheBytes > maxCacheBytes)
        {
            var first = lru.First;
            if (first == null)
                break;
            var oldKey = first.Value;
            lru.RemoveFirst();
            lruNodes.Remove(oldKey);
            if (map.Remove(oldKey, out var old))
            {
                cacheBytes -= old.Bytes;
                old.Bitmap?.Dispose();
                MarkExpiredLocked(oldKey);
            }
        }
    }

    private void TouchLocked((string Texture, long Rev) key)
    {
        if (!lruNodes.TryGetValue(key, out var node))
            return;
        lru.Remove(node);
        lru.AddLast(node);
    }

    private void MarkExpiredLocked((string Texture, long Rev) key)
    {
        if (!expired.Add(key))
            return;
        expiredOrder.Enqueue(key);
        while (expiredOrder.Count > ExpiredKeyLimit)
            expired.Remove(expiredOrder.Dequeue());
    }

    private static int EnvInt(string name, int fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
    }

    private static long EnvLong(string name, long fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return long.TryParse(value, out long parsed) && parsed > 0 ? parsed : fallback;
    }
}
