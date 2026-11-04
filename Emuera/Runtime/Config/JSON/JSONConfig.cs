//新設したコンフィグ設定のロード、セーブ、公開を担当する。
using System.IO;
using System.Text.Json;


namespace MinorShift.Emuera.Runtime.Config.JSON;
static class JSONConfig
{
	public static JSONConfigData Data;

	const string _configFileName = "setting.json";
	static string _configFilePath = Program.ExeDir + _configFileName;

	public static void Load()
	{
		if (!File.Exists(_configFilePath))
		{
			var defaultData = new JSONConfigData();
			var defaultJson = JsonSerializer.Serialize(defaultData);
			File.WriteAllText(_configFilePath, defaultJson);
		}

		var json = File.ReadAllText(_configFilePath);

		try
		{
			Data = JsonSerializer.Deserialize<JSONConfigData>(json);
		}
		catch (JsonException ex)
		{
			// setting.json 由不同分支/版本写出时字段格式可能不兼容
			// （典型例子：RenderingBackend 写成 "Auto" 字符串）。
			// 此时退回默认配置，不要让整局游戏加载失败。
			Runtime.Utils.PlatformInterop.DebugLog?.Invoke(
				$"JSONConfig.Load: 解析 setting.json 失败，已回退默认配置：{ex.Message}");
			Data = new JSONConfigData();
		}

		// 极端情况下反序列化得到 null（文件内容为 "null"）
		Data ??= new JSONConfigData();
	}

	public static void Save()
	{
		var json = JsonSerializer.Serialize(Data);
		File.WriteAllText(_configFilePath, json);
	}
}
