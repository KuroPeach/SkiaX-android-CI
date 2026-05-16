using MinorShift.Emuera.UI.Game.Image;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MinorShift.Emuera.UI.Game.Rendering;

internal sealed class ImageLayer
{
	public ASprite Image { get; set; }
	public long Depth { get; set; }
	public int X { get; set; }
	public int Y { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
	public int Opacity { get; set; } = 255;
	public float[]? ColorMatrix { get; set; }
	public bool FollowScroll { get; set; }
	public int InitialScrollY { get; set; }
	public bool IsOffScreen { get; set; }
}

internal sealed class ImageLayerManager
{
	private readonly Dictionary<long, ImageLayer> _layers = new();

	public IReadOnlyDictionary<long, ImageLayer> Layers => _layers;

	public void SetLayer(string spriteName, long depth, int x, int y,
		int width, int height, int opacity, float[]? colorMatrix, bool followScroll, int currentScrollY)
	{
		var sprite = AppContents.GetSprite(spriteName);
		if (sprite == null) return;

		_layers[depth] = new ImageLayer
		{
			Image = sprite,
			Depth = depth,
			X = x,
			Y = y,
			Width = width,
			Height = height,
			Opacity = Math.Clamp(opacity, 0, 255),
			ColorMatrix = colorMatrix,
			FollowScroll = followScroll,
			InitialScrollY = currentScrollY
		};
	}

	public void ClearLayer(long depth)
	{
		_layers.Remove(depth);
	}

	public void ClearAll()
	{
		_layers.Clear();
	}

	public bool Exists(long depth) => _layers.ContainsKey(depth);

	public void DrawTo(SKCanvas canvas, int viewportW, int viewportH, int scrollY)
	{
		if (_layers.Count == 0) return;

		var sortedLayers = new List<ImageLayer>(_layers.Values);
		sortedLayers.Sort((a, b) => a.Depth.CompareTo(b.Depth));

		foreach (var layer in sortedLayers)
		{
			if (layer.Image == null || !layer.Image.IsCreated) continue;

			int drawW = layer.Width > 0 ? layer.Width : layer.Image.DestBaseSize.Width;
			int drawH = layer.Height > 0 ? layer.Height : layer.Image.DestBaseSize.Height;

			int drawX = layer.X;
			int drawY = layer.Y + viewportH - drawH;
			if (layer.FollowScroll)
				drawY -= (scrollY - layer.InitialScrollY);

			if (drawX + drawW <= 0 || drawX >= viewportW ||
				drawY + drawH <= 0 || drawY >= viewportH)
			{
				layer.IsOffScreen = true;
				continue;
			}

			layer.IsOffScreen = false;

			var destRect = new Rectangle(drawX, drawY, drawW, drawH);
			using SKColorFilter? filter = BuildFilter(layer);
			layer.Image.GraphicsDraw(canvas, destRect, filter);
		}
	}

	private static SKColorFilter? BuildFilter(ImageLayer layer)
	{
		if (layer.ColorMatrix != null)
			return SKColorFilter.CreateColorMatrix(layer.ColorMatrix);
		if (layer.Opacity < 255)
		{
			float alpha = layer.Opacity / 255.0f;
			return SKColorFilter.CreateColorMatrix([
				1, 0, 0, 0, 0,
				0, 1, 0, 0, 0,
				0, 0, 1, 0, 0,
				0, 0, 0, alpha, 0,
			]);
		}
		return null;
	}
}
