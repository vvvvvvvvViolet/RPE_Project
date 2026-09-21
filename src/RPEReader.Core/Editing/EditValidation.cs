namespace RPEReader.Core.Editing;

public enum EditValidationLevel
{
    /// <summary>The value is accepted with no reservation.</summary>
    Ok,

    /// <summary>
    /// Accepted, but the value no longer looks like what the file originally
    /// held. The application does not have a schema for this format, so it
    /// reports the discrepancy rather than pretending to know it is wrong.
    /// </summary>
    Warning,

    /// <summary>Rejected: the value cannot be written to a well-formed document.</summary>
    Error
}

public sealed class EditValidationResult
{
    private EditValidationResult(EditValidationLevel level, string? message)
    {
        Level = level;
        Message = message;
    }

    public EditValidationLevel Level { get; }

    public string? Message { get; }

    public bool IsAccepted => Level != EditValidationLevel.Error;

    public static EditValidationResult Ok { get; } = new(EditValidationLevel.Ok, null);

    public static EditValidationResult Warning(string message) => new(EditValidationLevel.Warning, message);

    public static EditValidationResult Error(string message) => new(EditValidationLevel.Error, message);
}
