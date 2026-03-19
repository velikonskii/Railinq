namespace Railinq;



public class Result<T>
{
    public bool IsSuccess { get; private set; }
    
    public T Value { get; private set; }
    
    public Failure Error { get; private set; }
    

    public static Result<T> Success(T value)
    {
        return new Result<T> { IsSuccess = true, Value = value };
    }

    public Result<(T, T2)> Append<T2>(Result<T2> other) =>
        /* первой сработает ошибка из other*/
        Match<Result<(T, T2)>>
        (
            left1 => other.Match<Result<(T, T2)>>
            (
                left2  => Result<(T, T2)>.Failure(left2, previousFailure: left1),
                right2 => Result<(T, T2)>.Failure(left1)
            ),
            val1 => other.Match<Result<(T, T2)>>
            (
                left2  => Result<(T, T2)>.Failure(left2),
                val2   => Result<(T, T2)>.Success((val1, val2))
            )
        );

    public Result<T> AppendError<T2>(Result<T2> other)
    {
        return Match<Result<T>>
        (
            left1 => other.Match<Result<T>>
            (
                left2  => Result<T>.Failure(left2, previousFailure: left1),
                _      => Failure(left1)
            ),
            val1 => other.Match<Result<T>>
            (
                left2  => Result<T>.Failure(left2),
                _      => Success(val1)
            )
        );
    }

    

    public static Result<T> Failure(Failure errorMessage, Failure? previousFailure = null)
    {
        if (previousFailure == null)
        {
            return new Result<T>
            {
                IsSuccess = false, 
                Error = errorMessage
            };
        }
        
        return new Result<T>
        {
            IsSuccess = false, 
            Error = errorMessage with { PreviousFailure = previousFailure }
        };
        
    }
    
    
    public TResult Match<TResult>(Func<Failure, TResult> onFailure, Func<T, TResult> onSuccess)
    {
        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }


    public Result<TResult> Unwrap<TResult> (UnwrapDelegate<TResult> cb)
    {

        return Match<Result<TResult>>
        (
            left => Result<TResult>.Failure(left),
            cb.Invoke
        );
    }
    
    
    public async Task<Result<TResult>> UnwrapAsync<TResult>
    (
        Result<T> param,
        UnwrapAsyncDelegate<TResult> cb
    )
    {
        if (!param.IsSuccess)
        {
            return Result<TResult>.Failure(param.Error);
        }

        return await cb(param.Value);
    }

    
    public delegate Result<TResult> UnwrapDelegate<TResult>(T param);
    public delegate Task<Result<TResult>> UnwrapAsyncDelegate<TResult>(T value);

    
    private Result() { }
}





