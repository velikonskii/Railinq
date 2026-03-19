namespace Railinq.Demo.Adapters;

public class PaymentGateway
{
    private const decimal MaxAmount = 5000m;

    public Result<ResNone> Charge(decimal amount)
    {
        return amount > MaxAmount
            ? Result<ResNone>.Failure(new Failure("Payment declined", $"Amount {amount:C} exceeds limit {MaxAmount:C}"))
            : Result<ResNone>.Success(ResNone.Get);
    }
}
