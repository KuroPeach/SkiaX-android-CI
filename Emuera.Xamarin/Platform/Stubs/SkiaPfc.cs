using System.Collections;
using System.Collections.Generic;
using SkiaSharp;

namespace MinorShift.Emuera
{
	internal class SkiaFontFamily
	{
		public string Name { get; }
		public SKTypeface Typeface { get; }

		public SkiaFontFamily(SKTypeface typeface)
		{
			Typeface = typeface;
			Name = typeface.FamilyName;
		}

		public SkiaFontFamily(string name)
		{
			Name = name;
			Typeface = SKTypeface.FromFamilyName(name) ?? SKTypeface.Default;
		}

		public static SkiaFontFamily GenericSansSerif { get; } = new SkiaFontFamily(SKTypeface.Default);
		public static SkiaFontFamily GenericSerif { get; } = new SkiaFontFamily(SKTypeface.FromFamilyName("serif") ?? SKTypeface.Default);
		public static SkiaFontFamily GenericMonospace { get; } = new SkiaFontFamily(SKTypeface.FromFamilyName("monospace") ?? SKTypeface.Default);
	}

	internal class SkiaPfc : IEnumerable<SkiaFontFamily>
	{
		private readonly List<SKTypeface> _typefaces = new();

		public SkiaFontFamily[] Families
		{
			get
			{
				var result = new List<SkiaFontFamily>();
				foreach (var tf in _typefaces)
					result.Add(new SkiaFontFamily(tf));
				return result.ToArray();
			}
		}

		/// <summary>2026-10-27：develop 的 Creator.Method 用 FamilyNames 做字体族名匹配。</summary>
		public IEnumerable<string> FamilyNames
		{
			get
			{
				foreach (var tf in _typefaces)
					if (!string.IsNullOrEmpty(tf.FamilyName))
						yield return tf.FamilyName;
			}
		}

		public void AddFontFile(string path)
		{
			try
			{
				var typeface = SKTypeface.FromFile(path);
				if (typeface != null)
					_typefaces.Add(typeface);
			}
			catch { }
		}

		public IEnumerator<SkiaFontFamily> GetEnumerator()
		{
			foreach (var tf in _typefaces)
				yield return new SkiaFontFamily(tf);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	internal class SkiaInstalledFontCollection : IEnumerable<SkiaFontFamily>
	{
		public SkiaFontFamily[] Families
		{
			get
			{
				var result = new List<SkiaFontFamily>();
				using var fm = SKFontManager.Default;
				foreach (var familyName in fm.FontFamilies)
					result.Add(new SkiaFontFamily(familyName));
				return result.ToArray();
			}
		}

		public IEnumerator<SkiaFontFamily> GetEnumerator()
		{
			foreach (var ff in Families)
				yield return ff;
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
