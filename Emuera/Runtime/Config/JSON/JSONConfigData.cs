using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using MinorShift.Emuera.Runtime.Config;

namespace MinorShift.Emuera.Runtime.Config.JSON;

/// <summary>
/// 容忍「数字」与「字符串」两种写法的枚举转换器。
/// 上游桌面版把枚举写成数字（0/1/2），但部分游戏（如 eraAkumaMaid 的 AI 修改版）
/// 的 setting.json 由别的分支写成了字符串形式（"Auto"/"OpenGL"/"CPU"），
/// System.Text.Json 默认遇到字符串会抛 JsonException，导致整局游戏加载失败。
/// 读取时两种形式都接受；写入时仍输出数字，保持与上游桌面段的兼容性。
/// </summary>
internal sealed class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		switch (reader.TokenType)
		{
			case JsonTokenType.String:
				{
					var text = reader.GetString();
					if (text != null && Enum.TryParse<T>(text, true, out var parsed))
						return parsed;
					// 无法识别的字符串：退回默认值，避免整份配置失效
					return default;
				}
			case JsonTokenType.Number:
				{
					if (reader.TryGetInt32(out var num))
						return (T)Enum.ToObject(typeof(T), num);
					return default;
				}
			default:
				return default;
		}
	}

	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(Convert.ToInt32(value));
	}
}

//JSONの定義
sealed class JSONConfigData
{
	//ボタンにカーソルを合わせたときに背景色を変更するか
	[JsonPropertyName("UseButtonFocusBackgroundColor")]
	public bool UseButtonFocusBackgroundColor { get; set; }

	[JsonPropertyName("UseNewRandom")]
	public bool UseNewRandom { get; set; }

	/// <summary>
	/// 桌面版 Emuera.NET 的「作用域变量指令」开关：打开时会额外注册
	/// VARI / VARS 两条指令，从而把这两个名字占为**指令名**。
	///
	/// 本移植端不采用这个语义：不少 era 游戏的脚本把 VARS 当普通变量用
	/// （典型：eraAkumaMaid 的 `#DIMS VARS`），一旦按 setting.json 打开，
	/// 就会触发「変数名 VARS はEmueraの変数もしくは命令で使われています」，
	/// 进而在解析阶段抛异常、中断整个脚本加载（表现为后续 @函数 找不到）。
	///
	/// 因此这里固定为 false：始终用标准 SET / GET 语义，
	/// 保证任何 era 游戏都能正常加载。
	/// </summary>
	[JsonIgnore]
	public bool UseScopedVariableInstruction => false;

	[JsonPropertyName("RenderingBackend")]
	[JsonConverter(typeof(TolerantEnumConverter<RenderingBackend>))]
	public RenderingBackend RenderingBackend { get; set; }
}
