using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;

namespace Railinq;

public record Failure
{

    public Failure
    (
        string errorMessage,
        string exceptionMessage,
        Failure? previousFailure = null
    )
    {
        ErrorMessage = errorMessage;
        PreviousFailure = previousFailure;
        Debug.WriteLine
        (
            LogTemplate,
            GetType().Name,
            errorMessage,
            exceptionMessage
        );
    }

    
    public List<Failure> GetAllFailures()
    {
        if (PreviousFailure != null)
        {
            var failures = PreviousFailure.GetAllFailures();
            var list = new List<Failure>(failures) {this};
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

    private const string LogTemplate = "[FAILURE] {0}: {1}\n  {2}";
}

public record GeneralError(string ExceptionMessage)
    : Failure("General Error", ExceptionMessage);


