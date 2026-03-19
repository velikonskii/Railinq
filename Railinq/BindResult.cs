namespace Railinq;

public static class BindResult
{
    
    public static Result<TResult> Select<T, TResult>
    (
        this Result<T> result,
        Func<T, TResult> selector
    )
    {
        return result.Unwrap(val => Result<TResult>.Success(selector(val)));
    }

    
    public static Result<TResult> SelectMany<T, T2, TResult>
    (
        this Result<T> result,
        Func<T, Result<T2>> bind,
        Func<T, T2, TResult> project
    )
    {
        return result.Unwrap(val1 => 
            bind(val1).Unwrap(val2 => 
                Result<TResult>.Success(project(val1, val2))));
    }
    
    
    public static Result<ResNone> TraverseCollectError<T>
    (
        IReadOnlyList<T> items,
        Func<T, int, Result<ResNone>> func
    )
    {
        var errors = Result<ResNone>.Success(ResNone.Get);

        for (var i = 0; i < items.Count; i++)
        {
            var result = func(items[i], i);
            if (!result.IsSuccess)
            {
                errors = errors.AppendError(result);
            }
        }

        return errors;
    }
    
    
    public static Result<IReadOnlyList<TResult>> Traverse<T, TResult>
    (
        IReadOnlyList<T> items,
        Func<T, Result<TResult>> func
    )
    {
        var results = new List<TResult>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var result = func(items[i]);
            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<TResult>>.Failure(
                    result.Error
                );
            }

            results.Add(result.Value);
        }

        return Result<IReadOnlyList<TResult>>.Success(results);
    }


    public static Result<ResNone> TraverseUnit<T>
    (
        IReadOnlyList<T> items,
        Func<T, Result<ResNone>> func
    )
    {
        for (var i = 0; i < items.Count; i++)
        {
            var result = func(items[i]);
            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return Result<ResNone>.Success(ResNone.Get);
    } 
}


public static class BindResultAsync
{
    
    public static async Task<Result<TResult>> Select<T, TResult>
    (
        this Task<Result<T>> resultTask,
        Func<T, TResult> selector
    )
    {
        var result = await resultTask;
        return result.Select(selector);
    }

    
    public static async Task<Result<TResult>> SelectMany<T, T2, TResult>
    (
        this Task<Result<T>> resultTask,
        Func<T, Task<Result<T2>>> bind,
        Func<T, T2, TResult> project
    )
    {
        var result = await resultTask;
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error);

        var result2 = await bind(result.Value);
        if (!result2.IsSuccess)
            return Result<TResult>.Failure(result2.Error);

        return Result<TResult>.Success(project(result.Value, result2.Value));
    }

    
    public static async Task<Result<TResult>> SelectMany<T, T2, TResult>
    (
        this Result<T> result,
        Func<T, Task<Result<T2>>> bind,
        Func<T, T2, TResult> project
    )
    {
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error);

        var result2 = await bind(result.Value);
        if (!result2.IsSuccess)
            return Result<TResult>.Failure(result2.Error);

        return Result<TResult>.Success(project(result.Value, result2.Value));
    }
    
    
    public static async Task<Result<TResult>> SelectMany<T, T2, TResult>
    (
        this Task<Result<T>> resultTask,
        Func<T, Result<T2>> bind,
        Func<T, T2, TResult> project
    )
    {
        var result = await resultTask;
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error);

        var result2 = bind(result.Value);
        if (!result2.IsSuccess)
            return Result<TResult>.Failure(result2.Error);

        return Result<TResult>.Success(project(result.Value, result2.Value));
    }
    
    
    public static async Task<Result<IReadOnlyList<TResult>>> Traverse<T, TResult>
    (
        this IEnumerable<T> source,
        Func<T, Task<Result<TResult>>> func
    )
    {
        var results = new List<TResult>();

        foreach (var item in source)
        {
            var result = await func(item);
            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<TResult>>.Failure(result.Error);
            }

            results.Add(result.Value);
        }

        return Result<IReadOnlyList<TResult>>.Success(results);
    }


}