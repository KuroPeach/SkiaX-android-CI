using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;

namespace MinorShift.Emuera.Runtime.Utils.EvilMask;

internal static class ColorMatrixHelper
{
	public static float[][] CreateColorMatrix(float brightness, float contrast, float saturation, float hueShift, float opacity)
	{
		return [[1f, 0f, 0f, 0f, 0f], [0f, 1f, 0f, 0f, 0f], [0f, 0f, 1f, 0f, 0f], [0f, 0f, 0f, opacity, 0f], [0f, 0f, 0f, 0f, 1f]];
	}

	public static float[]? ReadFromVariableTerm(AExpression term, ExpressionMediator exm)
	{
		return null;
	}
}
