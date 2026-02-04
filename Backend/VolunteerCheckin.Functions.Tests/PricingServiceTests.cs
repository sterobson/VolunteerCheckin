using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for PricingService - event pricing calculations.
/// </summary>
[TestClass]
public class PricingServiceTests
{
    [TestMethod]
    public void CalculatePricePence_BaseTier_Returns1000()
    {
        int price = PricingService.CalculatePricePence(10);

        price.ShouldBe(1000);
    }

    [TestMethod]
    public void CalculatePricePence_BelowMinimum_ReturnsBasePrice()
    {
        int price = PricingService.CalculatePricePence(5);

        price.ShouldBe(1000);
    }

    [TestMethod]
    public void CalculatePricePence_ZeroMarshals_ReturnsBasePrice()
    {
        int price = PricingService.CalculatePricePence(0);

        price.ShouldBe(1000);
    }

    [TestMethod]
    public void CalculatePricePence_25Marshals_ReturnsCorrectPrice()
    {
        // £10 base + 15 additional * 50p = £10 + £7.50 = £17.50 = 1750p
        int price = PricingService.CalculatePricePence(25);

        price.ShouldBe(1750);
    }

    [TestMethod]
    public void CalculatePricePence_50Marshals_ReturnsCorrectPrice()
    {
        // £10 base + 40 additional * 50p = £10 + £20 = £30 = 3000p
        int price = PricingService.CalculatePricePence(50);

        price.ShouldBe(3000);
    }

    [TestMethod]
    public void CalculatePricePence_100Marshals_ReturnsCorrectPrice()
    {
        // £10 base + 90 additional * 50p = £10 + £45 = £55 = 5500p
        int price = PricingService.CalculatePricePence(100);

        price.ShouldBe(5500);
    }

    [TestMethod]
    public void CalculatePricePence_11Marshals_Adds50p()
    {
        int price = PricingService.CalculatePricePence(11);

        price.ShouldBe(1050);
    }

    [TestMethod]
    public void CalculateUpgradePricePence_10To25_ReturnsCorrectDifference()
    {
        // 25 marshals = 1750p, 10 marshals = 1000p, difference = 750p
        int price = PricingService.CalculateUpgradePricePence(10, 25);

        price.ShouldBe(750);
    }

    [TestMethod]
    public void CalculateUpgradePricePence_25To50_ReturnsCorrectDifference()
    {
        // 50 = 3000p, 25 = 1750p, difference = 1250p
        int price = PricingService.CalculateUpgradePricePence(25, 50);

        price.ShouldBe(1250);
    }

    [TestMethod]
    public void CalculateUpgradePricePence_SameTier_ReturnsZero()
    {
        int price = PricingService.CalculateUpgradePricePence(25, 25);

        price.ShouldBe(0);
    }

    [TestMethod]
    public void CalculateUpgradePricePence_Downgrade_ReturnsZero()
    {
        int price = PricingService.CalculateUpgradePricePence(50, 25);

        price.ShouldBe(0);
    }

    [TestMethod]
    public void GetBreakdown_BaseTier_ReturnsCorrectBreakdown()
    {
        PricingBreakdown breakdown = PricingService.GetBreakdown(10);

        breakdown.MarshalCount.ShouldBe(10);
        breakdown.BasePricePence.ShouldBe(1000);
        breakdown.IncludedMarshals.ShouldBe(10);
        breakdown.AdditionalMarshals.ShouldBe(0);
        breakdown.AdditionalMarshalsPricePence.ShouldBe(0);
        breakdown.TotalPricePence.ShouldBe(1000);
    }

    [TestMethod]
    public void GetBreakdown_50Marshals_ReturnsCorrectBreakdown()
    {
        PricingBreakdown breakdown = PricingService.GetBreakdown(50);

        breakdown.MarshalCount.ShouldBe(50);
        breakdown.BasePricePence.ShouldBe(1000);
        breakdown.IncludedMarshals.ShouldBe(10);
        breakdown.AdditionalMarshals.ShouldBe(40);
        breakdown.AdditionalMarshalsPricePence.ShouldBe(2000);
        breakdown.TotalPricePence.ShouldBe(3000);
    }

    [TestMethod]
    public void GetBreakdown_BelowMinimum_ClampsToMinimum()
    {
        PricingBreakdown breakdown = PricingService.GetBreakdown(3);

        breakdown.MarshalCount.ShouldBe(10);
        breakdown.TotalPricePence.ShouldBe(1000);
    }
}
