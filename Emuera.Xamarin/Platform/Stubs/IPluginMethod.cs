using System.Collections.Generic;

namespace MinorShift.Emuera.Runtime.Utils.PluginSystem
{
	public interface IPluginMethod
	{
		string Name { get; }
		string Description { get; }
		void Execute(PluginMethodParameter[] args);
	}

	public class PluginMethodParameter
	{
		public PluginMethodParameter(string initialValue)
		{
			isString = true;
			strValue = initialValue;
		}

		public PluginMethodParameter(long initialValue)
		{
			isString = false;
			intValue = initialValue;
		}

		public PluginMethodParameter(double initialValue)
		{
			isString = false;
			isFloat = true;
			floatValue = initialValue;
		}

		public bool isString;
		public bool isFloat;
		public string strValue;
		public long intValue;
		public double floatValue;
	}

	public class PluginManager
	{
		private static PluginManager instance;

		public static PluginManager GetInstance()
		{
			if (instance == null)
				instance = new PluginManager();
			return instance;
		}

		private readonly Dictionary<string, IPluginMethod> methods = new();

		public void SetParent(object process, object state, object exm) { }
		public void LoadPlugins() { }

		public IPluginMethod GetMethod(string name)
		{
			return methods.TryGetValue(name, out var m) ? m : null;
		}

		public bool HasMethod(string name)
		{
			return methods.ContainsKey(name);
		}
	}

	internal static class PluginMethodParameterBuilder
	{
		public static PluginMethodParameter ConvertTerm(object term, object exm)
		{
			return new PluginMethodParameter(0L);
		}
	}
}
