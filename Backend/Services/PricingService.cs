namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Calculates event pricing. Base price £10 includes 10 marshals, then 50p per additional marshal.
/// </summary>
public static class PricingService
{
    public const int BasePricePence = 1000;
    public const int IncludedMarshals = 10;
    public const int AdditionalMarshalPricePence = 50;
    public const int MinimumMarshalTier = 10;

    /// <summary>
    /// Calculate the total price in pence for a given marshal count.
    /// </summary>
    public static int CalculatePricePence(int marshalCount)
    {
        if (marshalCount < MinimumMarshalTier)
        {
            marshalCount = MinimumMarshalTier;
        }

        if (marshalCount <= IncludedMarshals)
        {
            return BasePricePence;
        }

        int additionalMarshals = marshalCount - IncludedMarshals;
        return BasePricePence + (additionalMarshals * AdditionalMarshalPricePence);
    }

    /// <summary>
    /// Calculate the upgrade price (difference) when moving from one tier to a higher tier.
    /// Returns 0 if the new tier is not higher than the current tier.
    /// </summary>
    public static int CalculateUpgradePricePence(int currentTier, int newTier)
    {
        if (newTier <= currentTier)
        {
            return 0;
        }

        int currentPrice = CalculatePricePence(currentTier);
        int newPrice = CalculatePricePence(newTier);
        return newPrice - currentPrice;
    }

    /// <summary>
    /// Break down the pricing for display purposes.
    /// </summary>
    public static PricingBreakdown GetBreakdown(int marshalCount)
    {
        if (marshalCount < MinimumMarshalTier)
        {
            marshalCount = MinimumMarshalTier;
        }

        int additionalMarshals = Math.Max(0, marshalCount - IncludedMarshals);
        int additionalPricePence = additionalMarshals * AdditionalMarshalPricePence;

        return new PricingBreakdown(
            MarshalCount: marshalCount,
            BasePricePence: BasePricePence,
            IncludedMarshals: IncludedMarshals,
            AdditionalMarshals: additionalMarshals,
            AdditionalMarshalsPricePence: additionalPricePence,
            TotalPricePence: BasePricePence + additionalPricePence
        );
    }
}

public record PricingBreakdown(
    int MarshalCount,
    int BasePricePence,
    int IncludedMarshals,
    int AdditionalMarshals,
    int AdditionalMarshalsPricePence,
    int TotalPricePence
);
