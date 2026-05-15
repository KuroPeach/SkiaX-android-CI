using SkiaSharp;
using System;

namespace MinorShift.Emuera.UI.Game.Image
{
	abstract class AContentItem
	{
		protected AContentItem(string name) { Name = name; }
		public readonly string Name;
		public abstract bool IsCreated { get; }
	}

	internal interface IFileBacked
	{
		void Evict();
		void Reload();
	}
}
