using Railinq;

namespace Railinq.Demo.Domain;

public record ProductNotFoundError : Failure
{
    public ProductNotFoundError(string productName)
        : base($"Product '{productName}' not found")
    {
        ProductName = productName;
        Log($"Lookup failed for '{productName}'");
    }

    public string ProductName { get; }
}

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

public record PaymentDeclinedError() : Failure("Payment declined");
