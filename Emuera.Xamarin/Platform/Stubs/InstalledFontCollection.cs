using System;
using System.Collections;
using System.Collections.Generic;
using SkiaSharp;

namespace System.Drawing.Text
{
	internal class InstalledFontCollection : IDisposable, IEnumerable<MinorShift.Emuera.SkiaFontFamily>
	{
		public MinorShift.Emuera.SkiaFontFamily[] Families
		{
			get
			{
				var result = new List<MinorShift.Emuera.SkiaFontFamily>();
				using var fm = SKFontManager.Default;
				foreach (var familyName in fm.FontFamilies)
					result.Add(new MinorShift.Emuera.SkiaFontFamily(familyName));
				return result.ToArray();
			}
		}

		public void Dispose() { }

		public IEnumerator<MinorShift.Emuera.SkiaFontFamily> GetEnumerator()
		{
			foreach (var ff in Families)
				yield return ff;
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
