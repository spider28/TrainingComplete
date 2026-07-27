namespace TrainingCompletion.Application;

public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
    public abstract string ErrorCode { get; }
}

public sealed class NotFoundException(string code, string message) : AppException(message)
{
    public override int StatusCode => 404;
    public override string ErrorCode { get; } = code;
}

public sealed class ConflictException(string code, string message) : AppException(message)
{
    public override int StatusCode => 409;
    public override string ErrorCode { get; } = code;
}

public sealed class ValidationException(string code, string message) : AppException(message)
{
    public override int StatusCode => 400;
    public override string ErrorCode { get; } = code;
}
