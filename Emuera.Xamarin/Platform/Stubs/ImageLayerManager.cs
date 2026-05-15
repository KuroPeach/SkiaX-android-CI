using SkiaSharp;
using System.Collections.Generic;

namespace MinorShift.Emuera.UI.Game.Rendering;

internal sealed class ImageLayer
{
	public object Image { get; set; }
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
		int width, int height, int opacity, float[]? colorMatrix, bool followScroll)
	{
	}

	public bool RemoveLayer(long depth)
	{
		return _layers.Remove(depth);
	}

	public void Clear()
	{
		_layers.Clear();
	}

	public void DrawAll(SKCanvas canvas, int scrollY)
	{
	}
}
