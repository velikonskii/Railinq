namespace Railinq;

public record Failure
{
    
    public delegate void LogHandler(string typeName, string errorMessage, string exceptionMessage);
    
    
    public Failure
    (
        string errorMessage,
        string exceptionMessage,
        Failure? previousFailure = null
    )
    {
        ErrorMessage = errorMessage;
        PreviousFailure = previousFailure;
        _logHandler?.Invoke(GetType().Name, errorMessage, exceptionMessage);
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


    public static GeneralError AssertionFailure(string exceptionMessage)
    {
        return new GeneralError(exceptionMessage);
    }

    
    public readonly string ErrorMessage;
    public Failure? PreviousFailure;

    private static volatile LogHandler? _logHandler;
}

public record GeneralError(string ExceptionMessage)
    : Failure("General Error", ExceptionMessage);
