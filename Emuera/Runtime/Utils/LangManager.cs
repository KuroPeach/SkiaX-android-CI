using System.Text;

namespace MinorShift.Emuera.Runtime.Utils;

//マルチ言語に対応可能な形式に変更
internal static class LangManager
{
	static Encoding lang;

	public static void setEncode(int code)
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		lang = Encoding.GetEncoding(code);
	}

	static bool IsWideChar(char c)
	{
		if (c >= 0x2E80 && c <= 0x9FFF) return true;
		if (c >= 0xA960 && c <= 0xA97F) return true;
		if (c >= 0xAC00 && c <= 0xD7AF) return true;
		if (c >= 0xF900 && c <= 0xFAFF) return true;
		if (c >= 0xFE30 && c <= 0xFE6F) return true;
		if (c >= 0xFF01 && c <= 0xFF60) return true;
		if (c >= 0xFFE0 && c <= 0xFFE6) return true;
		return false;
	}

	static int GetCharByteCount(char c)
	{
		int byteCount = lang.GetByteCount(c.ToString());
		if (byteCount == 1 && IsWideChar(c))
			return 2;
		return byteCount;
	}

	public static int GetStrlenLang(string str)
	{
		if (Ascii.IsValid(str))
		{
			return str.Length;
		}
		int total = 0;
		for (int i = 0; i < str.Length; i++)
		{
			total += GetCharByteCount(str[i]);
		}
		return total;
	}
	public static int GetUFTIndex(string str, int LangIndex)
	{
		if (LangIndex <= 0)
			return 0;
		int totalByte = GetStrlenLang(str);
		if (LangIndex >= totalByte)
			return str.Length;
		int UTFcnt = 0;
		int JIScnt = 0;
		for (int i = 0; i < str.Length; i++)
		{
			JIScnt += GetCharByteCount(str[UTFcnt]);
			UTFcnt++;
			if (JIScnt >= LangIndex)
				break;
		}
		return UTFcnt;
	}

	public static string GetSubStringLang(string str, int startindex, int length)
	{
		int totalByte = GetStrlenLang(str);
		if (startindex >= totalByte || length == 0)
			return "";
		if (length < 0 || length > totalByte)
			length = totalByte;

		StringBuilder ret = new();
		int UTFcnt = 0;
		int JIScnt = 0;

		if (startindex <= 0)
		{
			if (length == totalByte)
				return str;
		}
		else
		{
			for (int i = 0; i < str.Length; i++)
			{
				JIScnt += GetCharByteCount(str[UTFcnt]);
				UTFcnt++;
				if (JIScnt >= startindex)
					break;
			}
			if (UTFcnt >= str.Length)
				return "";
		}

		JIScnt = 0;
		while (true)
		{
			ret.Append(str[UTFcnt]);
			JIScnt += GetCharByteCount(str[UTFcnt]);
			UTFcnt++;
			if (JIScnt >= length)
				break;
			if (UTFcnt >= str.Length)
				break;
		}
		return ret.ToString();
	}
}
