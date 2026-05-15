using System;

namespace EnumsNET
{
	public static class Enums
	{
		public static string AsString<T>(T value) where T : struct, Enum
		{
			return value.ToString();
		}
	}
}
