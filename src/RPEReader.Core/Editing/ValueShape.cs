using System.Globalization;
using RPEReader.Core.Parsing.OpcRouter4;

namespace RPEReader.Core.Editing;

/// <summary>The kind of value a field originally held.</summary>
public enum ValueShape
{
    Empty,
    Boolean,
    Integer,
    Decimal,
    BinaryDateTime,
    Text
}

/// <summary>
/// Classifies a value by looking at it. This is a heuristic, not a schema:
/// the samples do not define one, so a mismatch is reported as a warning and
/// never as a hard failure.
/// </summary>
public static class ValueShapeClassifier
{
    public static ValueShape Classify(string? value, string fieldName)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ValueShape.Empty;
        }

        var trimmed = value.Trim();

        if (trimmed.Equals("True", StringComparison.Ordinal) || trimmed.Equals("False", StringComparison.Ordinal))
        {
            return ValueShape.Boolean;
        }

        // A binary DateTime is a long that also decodes to a plausible date.
        // Checking the decode avoids classifying ordinary large ids as dates.
        if (OpcRouter4Schema.KnownDateTimeElements.Contains(fieldName)
            && DotNetDateTimeDecoder.TryDecode(trimmed, out _, out _))
        {
            return ValueShape.BinaryDateTime;
        }

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return ValueShape.Integer;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return ValueShape.Decimal;
        }

        return ValueShape.Text;
    }

    public static string Describe(ValueShape shape) => shape switch
    {
        ValueShape.Empty => "empty",
        ValueShape.Boolean => "True or False",
        ValueShape.Integer => "a whole number",
        ValueShape.Decimal => "a number",
        ValueShape.BinaryDateTime => "a binary timestamp",
        _ => "text"
    };

    /// <summary>
    /// True when replacing a value of <paramref name="original"/> shape with one
    /// of <paramref name="replacement"/> shape is worth warning about.
    /// </summary>
    public static bool IsNotableChange(ValueShape original, ValueShape replacement)
    {
        if (original == replacement)
        {
            return false;
        }

        // Clearing a value, or filling an empty one, is ordinary editing.
        if (original == ValueShape.Empty || replacement == ValueShape.Empty)
        {
            return false;
        }

        // Widening a whole number to a decimal is not a real change of kind.
        if (original == ValueShape.Integer && replacement == ValueShape.Decimal)
        {
            return false;
        }

        return true;
    }
}
