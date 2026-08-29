using System.Text;

namespace MinorShift.Emuera.Runtime.Utils;

//マルチ言語に対応可能な形式に変更
internal static class LangManager
{
    static Encoding lang;
    static Encoding japanese;

    public static void setEncode(int code)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        lang = Encoding.GetEncoding(code);
        japanese = Encoding.GetEncoding(932);
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
        // 合并保留安卓端（0.61.0 渲染管线重构）的宽字符启发式：
        // 语言编码判定为 1 字节但实为全角字符时按 2 字节计
        int byteCount = lang.GetByteCount(c.ToString());
        if (byteCount == 1 && IsWideChar(c))
            return 2;
        return byteCount;
    }

    private static int GetByteCountLang(string str)
    {
        int length = 0;
        foreach (char c in str)
        {
            string value = c.ToString();
            byte[] bytes = lang.GetBytes(value);

            if (lang.GetString(bytes) == value)
            {
                length += bytes.Length;
                continue;
            }

            byte[] japaneseBytes = japanese.GetBytes(value);

            if (japanese.GetString(japaneseBytes) == value)
            {
                length += japaneseBytes.Length;
                continue;
            }

            // 当前语言编码与日文 932 都无法往返的字符（如日文<->中文编码互不相容）：
            // 回退到安卓端宽字符启发式，避免全角字符被按 1 字节误计
            length += GetCharByteCount(c);
        }
        return length;
    }

    public static int GetStrlenLang(string str)
    {
        if (Ascii.IsValid(str))
        {
            return str.Length;
        }
        return GetByteCountLang(str);
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
            JIScnt += GetByteCountLang(str[UTFcnt].ToString());
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
                JIScnt += GetByteCountLang(str[UTFcnt].ToString());
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
            JIScnt += GetByteCountLang(str[UTFcnt].ToString());
            UTFcnt++;
            if (JIScnt >= length)
                break;
            if (UTFcnt >= str.Length)
                break;
        }
        return ret.ToString();
    }
}