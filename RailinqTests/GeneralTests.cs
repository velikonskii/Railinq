using FluentAssertions;
using Railinq;

namespace RailinqTests;

public record ProductNotFoundError() : Failure("Product not found");

public record OutOfStockError() : Failure("Out of stock");

public record PaymentDeclinedError() : Failure("Payment declined");

public record Error1() : Failure("Error 1");

public record Error2() : Failure("Error 2");

public record Error3() : Failure("Error 3");

public record AFailedError() : Failure("A failed");

public record BFailedError() : Failure("B failed");

public record BadError() : Failure("bad");

public record DbError() : Failure("Database error");

public record DetailedError : Failure
{
    public DetailedError(string detail)
        : base($"Detailed: {detail}")
    {
        Detail = detail;
        Log($"Custom log for '{detail}'");
    }

    public string Detail { get; }
}


public class GeneralTests
{
    // ================================================================
    // Helpers: domain types & fake services mirroring Railinq.Demo
    // ================================================================

    private record Product(string Name, decimal Price, int Stock);

    private record Order(IReadOnlyList<CartItem> Items, decimal Total, string Status);

    private record CartItem(string ProductName, int Quantity);

    private readonly Dictionary<string, Product> _products = new()
    {
        ["Laptop"] = new Product("Laptop", 999.99m, 10),
        ["Mouse"] = new Product("Mouse", 29.99m, 100),
        ["Keyboard"] = new Product("Keyboard", 79.99m, 0),
        ["Monitor"] = new Product("Monitor", 499.99m, 3),
    };

    private const decimal MaxPaymentAmount = 5000m;

    private Result<Product> FindProduct(string name)
    {
        return _products.TryGetValue(name, out var product)
            ? Result<Product>.Success(product)
            : Result<Product>.Failure(Failure.CreateLogged<ProductNotFoundError>($"No product with name '{name}'"));
    }

    private static Result<Product> ValidateStock(Product product, int quantity)
    {
        return product.Stock >= quantity
            ? Result<Product>.Success(product)
            : Result<Product>.Failure(Failure.CreateLogged<OutOfStockError>($"'{product.Name}' has {product.Stock}, requested {quantity}"));
    }

    private static Result<ResNone> Charge(decimal amount)
    {
        return amount > MaxPaymentAmount
            ? Result<ResNone>.Failure(Failure.CreateLogged<PaymentDeclinedError>($"Amount {amount:C} exceeds limit"))
            : Result<ResNone>.Success(ResNone.Get);
    }

    private static Result<Order> CreateOrder(Product product, int quantity)
    {
        var item = new CartItem(product.Name, quantity);
        var total = product.Price * quantity;
        return Result<Order>.Success(new Order([item], total, "Created"));
    }

    private Result<Order> Checkout(string productName, int quantity)
    {
        return
            from product in FindProduct(productName)
            from validated in ValidateStock(product, quantity)
            from order in CreateOrder(validated, quantity)
            from _ in Charge(order.Total)
            select order;
    }

    private Result<ResNone> ValidateCartItemUnit(CartItem item)
    {
        return (
            from product in FindProduct(item.ProductName)
            from validated in ValidateStock(product, item.Quantity)
            select validated
        ).Match(
            onFailure: err => Result<ResNone>.Failure(err),
            onSuccess: _ => Result<ResNone>.Success(ResNone.Get)
        );
    }

    // ================================================================
    // Demo 1: Success / Failure + Match
    // ================================================================

    [Fact]
    public void FindProduct_ExistingProduct_ReturnsSuccess()
    {
        var result = FindProduct("Laptop");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Laptop");
        result.Value.Price.Should().Be(999.99m);
    }

    [Fact]
    public void FindProduct_UnknownProduct_ReturnsFailure()
    {
        var result = FindProduct("Projector");

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("Product not found");
    }

    [Fact]
    public void Match_OnSuccess_CallsSuccessBranch()
    {
        var result = FindProduct("Laptop");

        var output = result.Match(
            onFailure: err => $"FAIL: {err}",
            onSuccess: p => $"OK: {p.Name}"
        );

        output.Should().Be("OK: Laptop");
    }

    [Fact]
    public void Match_OnFailure_CallsFailureBranch()
    {
        var result = FindProduct("Projector");

        var output = result.Match(
            onFailure: err => $"FAIL: {err}",
            onSuccess: p => $"OK: {p.Name}"
        );

        output.Should().StartWith("FAIL:");
    }

    // ================================================================
    // Demo 2: Bind (LINQ query syntax)
    // ================================================================

    [Fact]
    public void Checkout_Success_ReturnsOrder()
    {
        var result = Checkout("Laptop", 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Total.Should().Be(999.99m * 2);
        result.Value.Status.Should().Be("Created");
    }

    [Fact]
    public void Checkout_OutOfStock_ReturnsFailure()
    {
        var result = Checkout("Keyboard", 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("Out of stock");
    }

    [Fact]
    public void Checkout_ProductNotFound_ReturnsFailure()
    {
        var result = Checkout("Projector", 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("Product not found");
    }

    [Fact]
    public void Checkout_PaymentExceedsLimit_ReturnsFailure()
    {
        var result = Checkout("Laptop", 6);

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("Payment declined");
    }

    [Fact]
    public void CheckoutWithReceipt_ClosureCapturesEarlierSteps()
    {
        var result =
            from product in FindProduct("Laptop")
            from validated in ValidateStock(product, 2)
            from order in CreateOrder(validated, 2)
            from _ in Charge(order.Total)
            select $"Receipt: {product.Name} x2, total: {order.Total:C}";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Laptop");
        result.Value.Should().Contain("x2");
    }

    // ================================================================
    // Demo 3: Traverse — short-circuit, returns values
    // ================================================================

    [Fact]
    public void Traverse_AllSucceed_ReturnsAllValues()
    {
        var names = new List<string> { "Laptop", "Mouse", "Monitor" };

        var result = BindResult.Traverse<string, Product>(names, FindProduct);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Select(p => p.Name).Should().BeEquivalentTo("Laptop", "Mouse", "Monitor");
    }

    [Fact]
    public void Traverse_SecondFails_ShortCircuits()
    {
        var names = new List<string> { "Laptop", "Projector", "Tablet" };

        var result = BindResult.Traverse<string, Product>(names, FindProduct);

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("Product not found");
    }

    // ================================================================
    // Demo 3: TraverseUnit — short-circuit, no return values
    // ================================================================

    [Fact]
    public void TraverseUnit_AllValid_ReturnsSuccess()
    {
        var cart = new List<CartItem> { new("Laptop", 1), new("Mouse", 5) };

        var result = BindResult.TraverseUnit(cart, ValidateCartItemUnit);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TraverseUnit_SecondFails_ShortCircuitsAtFirstError()
    {
        var cart = new List<CartItem> { new("Laptop", 1), new("Keyboard", 1), new("Projector", 1) };

        var result = BindResult.TraverseUnit(cart, ValidateCartItemUnit);

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("Out of stock");
    }

    // ================================================================
    // Demo 3: TraverseCollectError — accumulates ALL errors
    // ================================================================

    [Fact]
    public void TraverseCollectError_NoErrors_ReturnsSuccess()
    {
        var cart = new List<CartItem> { new("Laptop", 1), new("Mouse", 5) };

        var result = BindResult.TraverseCollectError(
            cart,
            (item, _) => ValidateCartItemUnit(item)
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TraverseCollectError_MultipleErrors_AccumulatesAll()
    {
        var cart = new List<CartItem>
        {
            new("Laptop", 1),    // OK
            new("Keyboard", 1),  // Out of stock
            new("Projector", 1), // Not found
            new("Tablet", 2),    // Not found
        };

        var result = BindResult.TraverseCollectError(
            cart,
            (item, _) => ValidateCartItemUnit(item)
        );

        result.IsSuccess.Should().BeFalse();

        var allErrors = result.Error.GetAllFailures();
        allErrors.Should().HaveCount(3);
    }

    // ================================================================
    // Async LINQ: SelectMany overloads
    // ================================================================

    [Fact]
    public async Task AsyncSelectMany_TaskToTask_ChainsCorrectly()
    {
        var result = await (
            from product in Task.FromResult(FindProduct("Laptop"))
            from validated in Task.FromResult(ValidateStock(product, 2))
            select validated
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Laptop");
    }

    [Fact]
    public async Task AsyncSelectMany_SyncToTask_ChainsCorrectly()
    {
        var result = await (
            from product in FindProduct("Laptop")
            from validated in Task.FromResult(ValidateStock(product, 2))
            select validated
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Laptop");
    }

    [Fact]
    public async Task AsyncSelectMany_TaskToSync_ChainsCorrectly()
    {
        var result = await (
            from product in Task.FromResult(FindProduct("Laptop"))
            from validated in ValidateStock(product, 2)
            select validated
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Laptop");
    }

    [Fact]
    public async Task AsyncSelectMany_Failure_PropagatesError()
    {
        var result = await (
            from product in Task.FromResult(FindProduct("Projector"))
            from validated in Task.FromResult(ValidateStock(product, 1))
            select validated
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("Product not found");
    }

    // ================================================================
    // Async Traverse
    // ================================================================

    [Fact]
    public async Task AsyncTraverse_AllSucceed_ReturnsAllValues()
    {
        var names = new List<string> { "Laptop", "Mouse" };

        var result = await names.Traverse<string, Product>(
            name => Task.FromResult(FindProduct(name))
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task AsyncTraverse_OneFails_ShortCircuits()
    {
        var names = new List<string> { "Laptop", "Projector", "Mouse" };

        var result = await names.Traverse<string, Product>(
            name => Task.FromResult(FindProduct(name))
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("Product not found");
    }

    // ================================================================
    // Error chaining: Append / PreviousFailure
    // ================================================================

    [Fact]
    public void Failure_GetAllFailures_ReturnsChain()
    {
        var first = Failure.Create<Error1>();
        var second = Failure.Create<Error2>();
        second.PreviousFailure = first;
        var third = Failure.Create<Error3>();
        third.PreviousFailure = second;

        var all = third.GetAllFailures();

        all.Should().HaveCount(3);
        all[0].ErrorMessage.Should().Be("Error 1");
        all[1].ErrorMessage.Should().Be("Error 2");
        all[2].ErrorMessage.Should().Be("Error 3");
    }

    [Fact]
    public void Failure_ToString_ReturnsErrorMessage()
    {
        var failure = Failure.CreateLogged<Error>("details");

        failure.ToString().Should().Be("General Error");
    }

    [Fact]
    public void Append_BothSuccess_ReturnsTuple()
    {
        var a = Result<int>.Success(1);
        var b = Result<string>.Success("hello");

        var result = a.Append(b);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be((1, "hello"));
    }

    [Fact]
    public void Append_FirstFails_ReturnsFailure()
    {
        var a = Result<int>.Failure(Failure.Create<AFailedError>());
        var b = Result<string>.Success("hello");

        var result = a.Append(b);

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("A failed");
    }

    [Fact]
    public void Append_BothFail_ChainsErrors()
    {
        var a = Result<int>.Failure(Failure.Create<AFailedError>());
        var b = Result<string>.Failure(Failure.Create<BFailedError>());

        var result = a.Append(b);

        result.IsSuccess.Should().BeFalse();
        result.Error.ErrorMessage.Should().Be("B failed");
        result.Error.PreviousFailure.Should().NotBeNull();
        result.Error.PreviousFailure!.ErrorMessage.Should().Be("A failed");
    }

    // ================================================================
    // Select (map) on Result
    // ================================================================

    [Fact]
    public void Select_Success_TransformsValue()
    {
        var result = Result<int>.Success(5);

        var mapped = result.Select(x => x * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Select_Failure_PropagatesError()
    {
        var result = Result<int>.Failure(Failure.Create<BadError>());

        var mapped = result.Select(x => x * 2);

        mapped.IsSuccess.Should().BeFalse();
        mapped.Error.ErrorMessage.Should().Be("bad");
    }

    // ================================================================
    // Factory methods: Create, CreateLogged, CreateFromEx
    // ================================================================

    [Fact]
    public void Create_ReturnsCorrectType()
    {
        var error = Failure.Create<ProductNotFoundError>();

        error.Should().BeOfType<ProductNotFoundError>();
        error.ErrorMessage.Should().Be("Product not found");
    }


    [Fact]
    public void Create_DoesNotInvokeLogHandler()
    {
        var logged = false;
        Failure.AttachLogHandler((_, _, _) => logged = true);

        Failure.Create<ProductNotFoundError>();

        logged.Should().BeFalse();
        Failure.AttachLogHandler(null!);
    }


    [Fact]
    public void CreateLogged_ReturnsCorrectTypeAndLogs()
    {
        string? capturedType = null;
        string? capturedMessage = null;
        string? capturedExc = null;
        Failure.AttachLogHandler
        (
            (type, msg, exc) =>
            {
                capturedType = type;
                capturedMessage = msg;
                capturedExc = exc;
            }
        );

        var error = Failure.CreateLogged<PaymentDeclinedError>("manual check");

        error.Should().BeOfType<PaymentDeclinedError>();
        error.ErrorMessage.Should().Be("Payment declined");
        capturedType.Should().Be("PaymentDeclinedError");
        capturedMessage.Should().Be("Payment declined");
        capturedExc.Should().Contain("manual check");
        capturedExc.Should().Contain("[CreateLogged_ReturnsCorrectTypeAndLogs]");
        Failure.AttachLogHandler(null!);
    }


    [Fact]
    public void CreateFromEx_ReturnsCorrectTypeAndLogsException()
    {
        string? capturedExc = null;
        Failure.AttachLogHandler((_, _, exc) => capturedExc = exc);

        var exception = new InvalidOperationException("test boom");
        var error = Failure.CreateFromEx<DbError>(exception);

        error.Should().BeOfType<DbError>();
        error.ErrorMessage.Should().Be("Database error");
        capturedExc.Should().Contain("InvalidOperationException");
        capturedExc.Should().Contain("test boom");
        Failure.AttachLogHandler(null!);
    }

    // ================================================================
    // Pattern matching on error types
    // ================================================================

    [Fact]
    public void PatternMatch_DistinguishesErrorTypes()
    {
        var outOfStock = Result<int>.Failure(Failure.Create<OutOfStockError>());
        var notFound = Result<int>.Failure(Failure.Create<ProductNotFoundError>());
        var declined = Result<int>.Failure(Failure.Create<PaymentDeclinedError>());

        string Classify(Result<int> r) => r.Match
        (
            onFailure: err => err switch
            {
                OutOfStockError => "stock",
                ProductNotFoundError => "not_found",
                PaymentDeclinedError => "payment",
                _ => "other"
            },
            onSuccess: _ => "ok"
        );

        Classify(outOfStock).Should().Be("stock");
        Classify(notFound).Should().Be("not_found");
        Classify(declined).Should().Be("payment");
    }

    // ================================================================
    // Custom constructor with data and Log()
    // ================================================================

    [Fact]
    public void CustomConstructor_CarriesDataAndLogs()
    {
        string? capturedExc = null;
        Failure.AttachLogHandler((_, _, exc) => capturedExc = exc);

        var error = new DetailedError("connection timeout");

        error.Should().BeOfType<DetailedError>();
        error.ErrorMessage.Should().Be("Detailed: connection timeout");
        error.Detail.Should().Be("connection timeout");
        capturedExc.Should().Contain("connection timeout");
        Failure.AttachLogHandler(null!);
    }


    [Fact]
    public void CustomConstructor_WithoutLogHandler_DoesNotThrow()
    {
        Failure.AttachLogHandler(null!);

        var act = () => new DetailedError("safe call");

        act.Should().NotThrow();
    }

    // ================================================================
    // AttachLogHandler
    // ================================================================

    [Fact]
    public void AttachLogHandler_NullHandler_NoLogging()
    {
        Failure.AttachLogHandler(null!);

        var act = () => Failure.CreateLogged<Error>("test");

        act.Should().NotThrow();
    }
}
