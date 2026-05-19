namespace MinorShift.Emuera.Runtime.Utils;

internal sealed class WinInput
{
	static readonly int[] _keyState = new int[256];
	static readonly short[] _keyToggle = new short[256];

	public static void SetKeyPressed(int keyCode)
	{
		if (keyCode < 0 || keyCode >= 256) return;
		System.Threading.Thread.VolatileWrite(ref _keyState[keyCode], 0x8000);
	}

	public static void SetKeyReleased(int keyCode)
	{
		if (keyCode < 0 || keyCode >= 256) return;
		System.Threading.Thread.VolatileWrite(ref _keyState[keyCode], 0);
	}

	public static short GetKeyState(int nVirtKey)
	{
		if (nVirtKey < 0 || nVirtKey >= 256) return 0;
		return (short)System.Threading.Thread.VolatileRead(ref _keyState[nVirtKey]);
	}

	public static short GetKeyToggle(int nVirtKey)
	{
		if (nVirtKey < 0 || nVirtKey >= 256) return 0;
		return _keyToggle[nVirtKey];
	}

	public static void SetKeyToggle(int nVirtKey, short value)
	{
		if (nVirtKey < 0 || nVirtKey >= 256) return;
		_keyToggle[nVirtKey] = value;
	}

	public static void ResetAllKeys()
	{
		for (int i = 0; i < 256; i++)
		{
			_keyState[i] = 0;
			_keyToggle[i] = 0;
		}
	}
}
