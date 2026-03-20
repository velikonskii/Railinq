using Railinq.Demo.Adapters;
using Railinq.Demo.Domain;

namespace Railinq.Demo.Services;

public class OrderService(ProductRepository repo, PaymentGateway payment)
{
    public Result<Product> ValidateStock(Product product, int quantity)
    {
        return product.Stock >= quantity
            ? Result<Product>.Success(product)
            : Result<Product>.Failure(new OutOfStockError(product.Name, product.Stock, quantity));
    }

    public Result<Order> CreateOrder(Product product, int quantity)
    {
        var item = new CartItem(product.Name, quantity);
        var total = product.Price * quantity;
        return Result<Order>.Success(new Order([item], total, "Created"));
    }

    /// <summary>
    /// Demo 2: full checkout chain via LINQ bind
    /// </summary>
    public Result<Order> Checkout(string productName, int quantity)
    {
        return
            from product in repo.FindProduct(productName)
            from validated in ValidateStock(product, quantity)
            from order in CreateOrder(validated, quantity)
            from _ in payment.Charge(order.Total)
            select order;
    }

    /// <summary>
    /// Demo 2b: LINQ — product из первого шага доступен в select через замыкание
    /// </summary>
    public Result<string> CheckoutWithReceipt(string productName, int quantity)
    {
        return
            from product in repo.FindProduct(productName)
            from validated in ValidateStock(product, quantity)
            from order in CreateOrder(validated, quantity)
            from _ in payment.Charge(order.Total)
            select $"Receipt: {product.Name} x{quantity}, unit price: {product.Price:C}, total: {order.Total:C}";
    }


    /// <summary>
    /// Demo 3: validate stock for a single cart item (used in Traverse/TraverseUnit)
    /// </summary>
    public Result<Product> ValidateCartItem(CartItem item)
    {
        return
            from product in repo.FindProduct(item.ProductName)
            from validated in ValidateStock(product, item.Quantity)
            select validated;
    }

    /// <summary>
    /// Demo 3: validate stock returning ResNone (used in TraverseUnit/TraverseAll)
    /// </summary>
    public Result<ResNone> ValidateCartItemUnit(CartItem item)
    {
        return ValidateCartItem(item).Match(
            onFailure: err => Result<ResNone>.Failure(err),
            onSuccess: _ => Result<ResNone>.Success(ResNone.Get)
        );
    }
}
