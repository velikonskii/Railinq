# Railinq

Railway-Oriented Programming for C# with native LINQ support.

Chain operations that can fail, short-circuit on errors, and accumulate validation failures — all using familiar C# query syntax.

## Why Railinq?

### LINQ query syntax out of the box

Most Result libraries force you into method chains like `.Bind().Map().Bind()`. Railinq implements `Select` and `SelectMany`, so you write standard C# LINQ:

```csharp
var result =
    from user    in FindUser(id)
    from account in GetAccount(user.AccountId)
    from order   in CreateOrder(user, account)
    select order;
```

This reads like regular C#, not a functional language. Earlier variables stay in scope — `user` from step 1 is available in step 3 without manual threading through tuples.

### Error accumulation via PreviousFailure

Most libraries stop at the first error. Railinq chains failures through `PreviousFailure`, so you can validate everything and return all errors at once:

```csharp
var result = BindResult.TraverseCollectError(
    formFields,
    (field, _) => Validate(field)
);

// On failure, get the full error chain:
var allErrors = result.Error.GetAllFailures();
// => ["Email is invalid", "Age must be positive", "Name is required"]
```

### Custom error types with pattern matching

Define typed errors as one-line records. Use pattern matching in your view/domain layer to handle each case differently:

```csharp
// Define errors — one line each
public record OrderNotFoundError() : Failure("Order not found");
public record OutOfStockError() : Failure("Out of stock");
public record PaymentDeclinedError() : Failure("Payment declined");

// Pattern match in view layer
result.Match(
    onFailure: err => err switch
    {
        OutOfStockError => ShowStockWarning(),
        OrderNotFoundError => ShowNotFound(),
        PaymentDeclinedError => ShowPaymentError(),
        _ => ShowGeneral(err.ErrorMessage)
    },
    onSuccess: order => ShowOrder(order)
);
```

Errors with data — use a custom constructor:

```csharp
public record OutOfStockError : Failure
{
    public OutOfStockError(string productName, int stock, int requested)
        : base($"'{productName}' is out of stock")
    {
        ProductName = productName;
        Stock = stock;
        Requested = requested;
        Log($"'{productName}' has {stock}, requested {requested}");
    }

    public string ProductName { get; }
    public int Stock { get; }
    public int Requested { get; }
}
```

### Pluggable logging

Railinq separates two concerns: `ErrorMessage` goes to the domain/view, `exceptionMessage` goes to the log. Attach any logger — Serilog, NLog, `Debug.WriteLine`, or a custom handler:

```csharp
// Serilog
Failure.AttachLogHandler(
    (type, err, exc) => Log.Error("{Type}: {Err} | {Exc}", type, err, exc)
);

// Debug
Failure.AttachLogHandler(
    (type, err, exc) => Debug.WriteLine($"[{type}] {err}: {exc}")
);

// DI — capture ILogger from your container
Failure.AttachLogHandler(
    (type, err, exc) => logger.LogError("{Type}: {Err} | {Exc}", type, err, exc)
);
```

No logging by default — nothing happens until you attach a handler.

Three factory methods control what gets logged:

```csharp
// Silent — no logging, just the error for domain/view
Failure.Create<OrderNotFoundError>()

// Logged — custom message + automatic caller info (file, line, method)
Failure.CreateLogged<OrderNotFoundError>("orderId was null")

// From exception — logs exception.ToString()
Failure.CreateFromEx<DbError>(exception)
```

### Native async support

`Task<Result<T>>` works transparently with the same LINQ operators. Mix sync and async steps freely:

```csharp
var result =
    from user  in FindUser(id)                    // sync
    from order in await FetchOrderAsync(user.Id)  // async
    from _     in await ChargeAsync(order.Total)  // async
    select order;
```

No extra wrappers or adapter types required.

### Minimalist

Railinq does one thing well. The entire library is four files:

| Type | Purpose |
|------|---------|
| `Result<T>` | Success or Failure container |
| `Failure` | Error with optional chain to previous errors |
| `BindResult` | LINQ operators + collection traversals |
| `ResNone` | Unit type for void operations |

No custom collections, no Option/Maybe, no Try monad, no IO effects. Just `Result<T>` that works with LINQ.

## Installation

```
dotnet add package Railinq
```

## Quick start

### Creating Results

```csharp
var success = Result<int>.Success(42);
var failure = Result<int>.Failure(Failure.Create<OrderNotFoundError>());
```

### Fold over success/failure (similar to Either)

```csharp
var message = result.Match(
    onFailure: err => $"Error: {err}",
    onSuccess: val => $"Got: {val}"
);
```

### Chaining operations with LINQ

```csharp
Result<Order> Checkout(string productName, int quantity)
{
    return
        from product   in repo.FindProduct(productName)
        from validated in ValidateStock(product, quantity)
        from order     in CreateOrder(validated, quantity)
        from _         in payment.Charge(order.Total)
        select order;
}
```

If any step fails, the chain short-circuits and returns the failure. No try-catch, no null checks.

### LINQ vs manual SelectMany

With LINQ, variables from earlier steps are captured by closure:

```csharp
from product in repo.FindProduct(name)
from order   in CreateOrder(product, qty)
from _       in payment.Charge(order.Total)
select $"{product.Name}: {order.Total}";  // product is still in scope
```

Without LINQ, you must thread values through tuples manually:

```csharp
repo.FindProduct(name)
    .SelectMany(p => CreateOrder(p, qty), (p, order) => (p, order))
    .SelectMany(t => payment.Charge(t.order.Total), (t, _) =>
        $"{t.p.Name}: {t.order.Total}");
```

### Collection traversals

**Traverse** — apply a function to each item, short-circuit on first error:

```csharp
var products = BindResult.Traverse(productNames, repo.FindProduct);
// Success: IReadOnlyList<Product>
// Failure: first error encountered
```

**TraverseUnit** — same, but when you only care about pass/fail:

```csharp
var validation = BindResult.TraverseUnit(cartItems, ValidateItem);
// Success: ResNone
// Failure: first error encountered
```

**TraverseCollectError** — validate everything, accumulate all errors:

```csharp
var validation = BindResult.TraverseCollectError(
    cartItems,
    (item, index) => ValidateItem(item)
);

if (!validation.IsSuccess)
{
    foreach (var err in validation.Error.GetAllFailures())
        Console.WriteLine(err.ErrorMessage);
}
```

## Comparison

| Feature | Railinq | FluentResults | LanguageExt | OneOf |
|---------|---------|--------------|-------------|-------|
| LINQ query syntax | Yes | No | Partial | No |
| Error accumulation | Chained `PreviousFailure` | Flat list | Via `Validation` | No |
| Typed errors + pattern matching | Yes | No | Yes | Yes |
| Pluggable logging | Yes | No | No | No |
| Async LINQ | Yes | N/A | Requires wrappers | N/A |
| API surface | ~4 types | ~20 types | ~200+ types | ~30 types |
| Learning curve | Minimal | Low | Steep | Low |

## License

MIT
