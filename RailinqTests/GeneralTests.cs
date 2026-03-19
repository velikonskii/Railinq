using FluentAssertions;
using Railinq;

namespace RailinqTests;

public class GeneralTests
{
    [Fact]
    public void Test1()
    {
        
        var res =
            from foo in GetFooData()
            from bar in GetBarData()
            from result in ConcatServiceMethod(foo, bar)
            select result;
        
        res.IsSuccess.Should().BeTrue();
        res.Value.Should().Be("FooBar");
    }


    private Result<string> ConcatServiceMethod(Foo foo, Bar bar)
    {
        return Result<string>.Success(foo.Name + bar.Name);
    }


    private Result<Foo> GetFooData(bool throwException = false)
    {
        try
        {
            //Эта часть эмулирует какую-то ошибку, например обрыв сети
            if (throwException)
            {
                throw new Exception();
            }
            return Result<Foo>.Success(new Foo(1, "Foo"));
        }
        catch(Exception) 
        {
            return Result<Foo>.Failure(Failure.AssertionFailure(""));
        }
    }
    

    private Result<Bar> GetBarData(bool throwException = false)
    {
        try
        {
            //Эта часть эмулирует какую-то ошибку, например обрыв сети
            if (throwException)
            {
                throw new Exception();
            }
            return Result<Bar>.Success(new Bar(1, "Bar"));
        }
        catch(Exception) 
        {
            return Result<Bar>.Failure(Failure.AssertionFailure(""));
        }
    }


    private record Foo(int Id, string Name);

    
    private record Bar(int Id, string Name);
    
    
}