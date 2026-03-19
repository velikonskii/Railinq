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
var failure = Result<int>.Failure(new Failure("Not found", "Item with id=7 does not exist"));
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
| Async LINQ | Yes | N/A | Requires wrappers | N/A |
| API surface | ~4 types | ~20 types | ~200+ types | ~30 types |
| Learning curve | Minimal | Low | Steep | Low |

## License

MIT
