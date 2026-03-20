using System.Runtime.CompilerServices;

namespace Railinq;

public record Failure
{
    
    public delegate void LogHandler(string typeName, string errorMessage, string exceptionMessage);
    
    
    protected Failure(string errorMessage, Failure? previousFailure = null)
    {
        ErrorMessage = errorMessage;
        PreviousFailure = previousFailure;
    }


    protected void Log(string exceptionMessage) 
        => _logHandler?.Invoke(GetType().Name, ErrorMessage, exceptionMessage);


    public static T Create<T>() where T : Failure, new() => new();


    public static T CreateLogged<T>
    (
        string loggedMessage,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0
    )
        where T : Failure, new()
    {
        var failure = new T();
        var callerInfo = $"{filePath}:{lineNumber} [{memberName}]";
        _logHandler?.Invoke(failure.GetType().Name, failure.ErrorMessage, $"{loggedMessage} | {callerInfo}");
        return failure;
    }


    public static T CreateFromEx<T>(Exception exception) where T : Failure, new()
    {
        var failure = new T();
        _logHandler?.Invoke(failure.GetType().Name, failure.ErrorMessage, exception.ToString());
        return failure;
    }


    public static void AttachLogHandler(LogHandler logHandler)
    {
        _logHandler = logHandler;
    }


    public List<Failure> GetAllFailures()
    {
        if (PreviousFailure != null)
        {
            var failures = PreviousFailure.GetAllFailures();
            var list = new List<Failure>(failures) { this };
            return list;
        }
        return [this];
    }


    public sealed override string ToString()
    {
        return ErrorMessage;
    }


    public static Error AssertionFailure(string exceptionMessage)
    {
        return CreateLogged<Error>(exceptionMessage);
    }

    
    public readonly string ErrorMessage;
    public Failure? PreviousFailure;

    private static volatile LogHandler? _logHandler;
}

public record Error() : Failure("General Error");
