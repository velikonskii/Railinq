namespace Railinq.Demo.Domain;

public record Order(IReadOnlyList<CartItem> Items, decimal Total, string Status);
