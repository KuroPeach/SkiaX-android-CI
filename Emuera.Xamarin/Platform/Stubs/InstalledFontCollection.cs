using System;
using System.Collections;
using System.Collections.Generic;
using SkiaSharp;

namespace System.Drawing.Text
{
	internal class InstalledFontCollection : IDisposable, IEnumerable<MinorShift.Emuera.SkiaFontFamily>
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

		public void Dispose() { }

		public IEnumerator<MinorShift.Emuera.SkiaFontFamily> GetEnumerator()
		{
			foreach (var ff in Families)
				yield return new MinorShift.Emuera.SkiaFontFamily(SKTypeface.FromFamilyName(ff.Name) ?? SKTypeface.Default);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
