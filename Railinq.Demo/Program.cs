using Railinq;
using Railinq.Demo.Adapters;
using Railinq.Demo.Domain;
using Railinq.Demo.Services;

var repo = new ProductRepository();
var payment = new PaymentGateway();
var orderService = new OrderService(repo, payment);

// ============================================================
// Demo 0: Attach log handler
// ============================================================
Failure.AttachLogHandler((type, err, exc) => Console.WriteLine($"  [LOG] {type}: {err} | {exc}"));

// ============================================================
// Demo 1: Success / Failure + Match
// ============================================================
Console.WriteLine("=== Demo 1: Success & Failure ===\n");

Console.WriteLine("FindProduct(\"Laptop\"):");
var laptopResult = repo.FindProduct("Laptop");
var output1 = laptopResult.Match
(
    onFailure: err => $"  FAIL: {err}",
    onSuccess: p => $"  OK: Found '{p.Name}' — {p.Price:C}, stock: {p.Stock}"
);
Console.WriteLine(output1);

Console.WriteLine("\nFindProduct(\"Projector\"):");
var unknownResult = repo.FindProduct("Projector");
var output2 = unknownResult.Match
(
    onFailure: err => $"  FAIL: {err}",
    onSuccess: p => $"  OK: Found '{p.Name}'"
);
Console.WriteLine(output2);

// ============================================================
// Demo 2: Bind (LINQ query syntax)
// ============================================================
Console.WriteLine("\n=== Demo 2: Bind (LINQ chain) ===\n");

// 2a: successful checkout
Console.WriteLine("Checkout(\"Laptop\", qty: 2):");
var checkout1 = orderService.Checkout("Laptop", 2);
var out2a = checkout1.Match
(
    onFailure: err => $"  FAIL: {err}",
    onSuccess: order => $"  OK: Order created — {order.Items.Count} item(s), total: {order.Total:C}, status: {order.Status}"
);
Console.WriteLine(out2a);

// 2b: fails at ValidateStock (Keyboard has stock=0)
Console.WriteLine("\nCheckout(\"Keyboard\", qty: 1) — out of stock:");
var checkout2 = orderService.Checkout("Keyboard", 1);
var out2b = checkout2.Match(
    onFailure: err => $"  FAIL: {err}",
    onSuccess: order => $"  OK: Order created — total: {order.Total:C}"
);
Console.WriteLine(out2b);

// 2c: fails at FindProduct (product doesn't exist)
Console.WriteLine("\nCheckout(\"Projector\", qty: 1) — not found:");
var checkout3 = orderService.Checkout("Projector", 1);
var out2c = checkout3.Match
(
    onFailure: err => $"  FAIL: {err}",
    onSuccess: order => $"  OK: Order created — total: {order.Total:C}"
);
Console.WriteLine(out2c);

// 2d: fails at payment (amount exceeds limit)
Console.WriteLine("\nCheckout(\"Laptop\", qty: 6) — payment exceeds limit:");
var checkout4 = orderService.Checkout("Laptop", 6);
var out2d = checkout4.Match
(
    onFailure: err => $"  FAIL: {err}",
    onSuccess: order => $"  OK: Order created — total: {order.Total:C}"
);
Console.WriteLine(out2d);

// 2e: LINQ vs manual bind — passing parameters across steps
Console.WriteLine("\n--- LINQ vs manual SelectMany ---");
Console.WriteLine("LINQ: product from step 1 is available in final select via closure:");

var receiptLinq = orderService.CheckoutWithReceipt("Laptop", 2);
Console.WriteLine(receiptLinq.Match
(
    onFailure: err => $"  FAIL: {err}",
    onSuccess: receipt => $"  {receipt}"
));


// ============================================================
// Demo 3: Traverse / TraverseUnit / TraverseAll
// ============================================================
Console.WriteLine("\n=== Demo 3: Traverse, TraverseUnit, TraverseAll ===\n");

// --- Traverse: returns list of products or first error ---
Console.WriteLine("--- Traverse (short-circuit, returns values) ---");

Console.WriteLine("Traverse([Laptop, Mouse, Monitor]):");
var goodNames = new List<string> { "Laptop", "Mouse", "Monitor" };
var traverseOk = BindResult.Traverse<string, Product>(goodNames, repo.FindProduct);
var outT1 = traverseOk.Match
(
    onFailure: err => $"  FAIL: {err}",
    onSuccess: products => $"  OK: Found {products.Count} products: {string.Join(", ", products.Select(p => p.Name))}"
);
Console.WriteLine(outT1);

Console.WriteLine("\nTraverse([Laptop, Projector, Tablet]):");
var mixedNames = new List<string> { "Laptop", "Projector", "Tablet" };
var traverseFail = BindResult.Traverse<string, Product>(mixedNames, repo.FindProduct);
var outT2 = traverseFail.Match
(
    onFailure: err => $"  FAIL: {err} (short-circuited, 'Tablet' was never checked)",
    onSuccess: products => $"  OK: Found {products.Count} products"
);
Console.WriteLine(outT2);

// --- TraverseUnit: validate side-effects only, first error stops ---
Console.WriteLine("\n--- TraverseUnit (short-circuit, no return values) ---");

Console.WriteLine("TraverseUnit([Laptop x1, Mouse x5]):");
var cartOk = new List<CartItem> { new("Laptop", 1), new("Mouse", 5) };
var tuOk = BindResult.TraverseUnit(cartOk, orderService.ValidateCartItemUnit);
var outTU1 = tuOk.Match
(
    onFailure: err => $"  FAIL: {err}",
    onSuccess: _ => "  OK: All items validated"
);
Console.WriteLine(outTU1);

Console.WriteLine("\nTraverseUnit([Laptop x1, Keyboard x1, Projector x1]):");
var cartBad = new List<CartItem> { new("Laptop", 1), new("Keyboard", 1), new("Projector", 1) };
var tuFail = BindResult.TraverseUnit(cartBad, orderService.ValidateCartItemUnit);
var outTU2 = tuFail.Match(
    onFailure: err => $"  FAIL: {err} (stopped at first error, 'Projector' not checked)",
    onSuccess: _ => "  OK: All items validated"
);
Console.WriteLine(outTU2);

// --- TraverseAll: accumulate ALL errors ---
Console.WriteLine("\n--- TraverseAll (accumulates all errors) ---");

Console.WriteLine("TraverseAll([Laptop x1, Keyboard x1, Projector x1, Tablet x2]):");
var cartAllBad = new List<CartItem> { new("Laptop", 1), new("Keyboard", 1), new("Projector", 1), new("Tablet", 2) };
var taResult = BindResult.TraverseCollectError
(
    cartAllBad,
    (item, _) => orderService.ValidateCartItemUnit(item)
);
var outTA = taResult.Match
(
    onFailure: err =>
    {
        var allErrors = err.GetAllFailures();
        var lines = allErrors.Select((e, i) => $"    [{i + 1}] {e.ErrorMessage}");
        return $"  FAIL: {allErrors.Count} error(s):\n{string.Join("\n", lines)}";
    },
    onSuccess: _ => "  OK: All items validated"
);
Console.WriteLine(outTA);

// ============================================================
// Demo 4: Pattern matching on custom error types
// ============================================================
Console.WriteLine("\n=== Demo 4: Pattern matching on error types ===\n");

Console.WriteLine("Checkout(\"Keyboard\", 1) — pattern match:");
var failedCheckout = orderService.Checkout("Keyboard", 1);
var out4 = failedCheckout.Match
(
    onFailure: err => err switch
    {
        OutOfStockError e => $"  STOCK: '{e.ProductName}' has {e.Stock}, need {e.Requested}",
        ProductNotFoundError e => $"  NOT FOUND: '{e.ProductName}'",
        PaymentDeclinedError => "  PAYMENT: declined",
        _ => $"  OTHER: {err.ErrorMessage}"
    },
    onSuccess: order => $"  OK: {order.Total:C}"
);
Console.WriteLine(out4);

// ============================================================
// Demo 5: Factory methods — 3 ways to create errors
// ============================================================
Console.WriteLine("\n=== Demo 5: Factory methods ===\n");

Console.WriteLine("Create<T>() — silent, no logging:");
var silent = Failure.Create<PaymentDeclinedError>();
Console.WriteLine($"  {silent.ErrorMessage}");

Console.WriteLine("\nCreateLogged<T>() — with log + caller info:");
var logged = Failure.CreateLogged<PaymentDeclinedError>("manual check failed");
Console.WriteLine($"  {logged.ErrorMessage}");

Console.WriteLine("\nCreateFromEx<T>() — from exception:");
try
{
    throw new InvalidOperationException("test exception");
}
catch (Exception ex)
{
    var fromEx = Failure.CreateFromEx<PaymentDeclinedError>(ex);
    Console.WriteLine($"  {fromEx.ErrorMessage}");
}

Console.WriteLine("\nCustom constructor with data (logs in base):");
var custom = new ProductNotFoundError("Tablet");
Console.WriteLine($"  {custom.ErrorMessage}, ProductName={custom.ProductName}");
