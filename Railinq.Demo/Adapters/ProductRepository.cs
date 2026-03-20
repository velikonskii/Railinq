using Railinq.Demo.Domain;

namespace Railinq.Demo.Adapters;

public class ProductRepository
{
    private readonly Dictionary<string, Product> _products = new()
    {
        ["Laptop"] = new Product("Laptop", 999.99m, 10),
        ["Mouse"] = new Product("Mouse", 29.99m, 100),
        ["Keyboard"] = new Product("Keyboard", 79.99m, 0),
        ["Monitor"] = new Product("Monitor", 499.99m, 3),
    };

    public Result<Product> FindProduct(string name)
    {
        return _products.TryGetValue(name, out var product)
            ? Result<Product>.Success(product)
            : Result<Product>.Failure(new ProductNotFoundError(name));
    }
}
