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

		public static SkiaFontFamily GenericSansSerif { get; } = new SkiaFontFamily(SKTypeface.Default);
		public static SkiaFontFamily GenericSerif { get; } = new SkiaFontFamily(SKTypeface.FromFamilyName("serif") ?? SKTypeface.Default);
		public static SkiaFontFamily GenericMonospace { get; } = new SkiaFontFamily(SKTypeface.FromFamilyName("monospace") ?? SKTypeface.Default);

		public static implicit operator System.Drawing.FontFamily(SkiaFontFamily ff)
			=> ff == null ? null : new System.Drawing.FontFamily(ff.Name);

		public static implicit operator SkiaFontFamily(System.Drawing.FontFamily ff)
			=> ff == null ? null : new SkiaFontFamily(SKTypeface.FromFamilyName(ff.Name) ?? SKTypeface.Default);
	}

	internal class SkiaPfc : IEnumerable<SkiaFontFamily>
	{
		private readonly List<SKTypeface> _typefaces = new();

		public System.Drawing.FontFamily[] Families
		{
			get
			{
				var result = new List<System.Drawing.FontFamily>();
				foreach (var tf in _typefaces)
					result.Add(new System.Drawing.FontFamily(tf.FamilyName));
				return result.ToArray();
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
		public System.Drawing.FontFamily[] Families
		{
			get
			{
				var result = new List<System.Drawing.FontFamily>();
				using var fm = SKFontManager.Default;
				foreach (var familyName in fm.FontFamilies)
					result.Add(new System.Drawing.FontFamily(familyName));
				return result.ToArray();
			}
		}

		public IEnumerator<SkiaFontFamily> GetEnumerator()
		{
			foreach (var ff in Families)
				yield return new SkiaFontFamily(SKTypeface.FromFamilyName(ff.Name) ?? SKTypeface.Default);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
