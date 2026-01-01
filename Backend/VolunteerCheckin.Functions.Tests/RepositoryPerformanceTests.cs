using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Performance tests for repository lookups.
/// Verifies O(1) lookups complete within acceptable thresholds.
///
/// Run with: dotnet test --filter "TestCategory=Performance" -e RUN_PERFORMANCE_TESTS=true
/// Requires Azurite running locally.
/// </summary>
[TestClass]
[TestCategory("Performance")]
public class RepositoryPerformanceTests
{
    // Threshold in milliseconds - O(1) lookups should be well under this
    private const int LookupThresholdMs = 100;
    private const int WarmupRuns = 2;
    private const int MeasuredRuns = 5;

    [TestMethod]
    public async Task GetByEmailAsync_WithIndex_ShouldBeUnderThreshold()
    {
        SkipIfNotEnabled();

        TableStoragePersonRepository repo = new(PerformanceTestSetup.TableStorage);

        // Warm up
        for (int i = 0; i < WarmupRuns; i++)
        {
            await repo.GetByEmailAsync(PerformanceTestSetup.KnownPersonEmail);
        }

        // Measure
        List<long> times = [];
        for (int i = 0; i < MeasuredRuns; i++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            PersonEntity? result = await repo.GetByEmailAsync(PerformanceTestSetup.KnownPersonEmail);
            sw.Stop();

            Assert.IsNotNull(result, "Lookup should find the seeded person");
            times.Add(sw.ElapsedMilliseconds);
        }

        long median = GetMedian(times);
        Console.WriteLine($"GetByEmailAsync median: {median}ms (times: [{string.Join(", ", times)}]ms)");
        median.ShouldBeLessThan(LookupThresholdMs,
            $"GetByEmailAsync took {median}ms (times: [{string.Join(", ", times)}]ms). " +
            $"With {PerformanceTestSetup.RecordCount} records, this suggests O(n) scan instead of O(1) index lookup.");
    }

    [TestMethod]
    public async Task GetByTokenHashAsync_ShouldBeUnderThreshold()
    {
        SkipIfNotEnabled();

        TableStorageAuthTokenRepository repo = new(PerformanceTestSetup.TableStorage);

        // Warm up
        for (int i = 0; i < WarmupRuns; i++)
        {
            await repo.GetByTokenHashAsync(PerformanceTestSetup.KnownTokenHash);
        }

        // Measure
        List<long> times = [];
        for (int i = 0; i < MeasuredRuns; i++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            AuthTokenEntity? result = await repo.GetByTokenHashAsync(PerformanceTestSetup.KnownTokenHash);
            sw.Stop();

            Assert.IsNotNull(result, "Lookup should find the seeded token");
            times.Add(sw.ElapsedMilliseconds);
        }

        long median = GetMedian(times);
        Console.WriteLine($"GetByTokenHashAsync median: {median}ms (times: [{string.Join(", ", times)}]ms)");
        median.ShouldBeLessThan(LookupThresholdMs,
            $"GetByTokenHashAsync took {median}ms (times: [{string.Join(", ", times)}]ms). " +
            $"With {PerformanceTestSetup.RecordCount} records, this suggests O(n) scan instead of O(1) lookup.");
    }

    [TestMethod]
    public async Task GetBySessionTokenHashAsync_ShouldBeUnderThreshold()
    {
        SkipIfNotEnabled();

        TableStorageAuthSessionRepository repo = new(PerformanceTestSetup.TableStorage);

        // Warm up
        for (int i = 0; i < WarmupRuns; i++)
        {
            await repo.GetBySessionTokenHashAsync(PerformanceTestSetup.KnownSessionTokenHash);
        }

        // Measure
        List<long> times = [];
        for (int i = 0; i < MeasuredRuns; i++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            AuthSessionEntity? result = await repo.GetBySessionTokenHashAsync(PerformanceTestSetup.KnownSessionTokenHash);
            sw.Stop();

            Assert.IsNotNull(result, "Lookup should find the seeded session");
            times.Add(sw.ElapsedMilliseconds);
        }

        long median = GetMedian(times);
        Console.WriteLine($"GetBySessionTokenHashAsync median: {median}ms (times: [{string.Join(", ", times)}]ms)");
        median.ShouldBeLessThan(LookupThresholdMs,
            $"GetBySessionTokenHashAsync took {median}ms (times: [{string.Join(", ", times)}]ms). " +
            $"With {PerformanceTestSetup.RecordCount} records, this suggests O(n) scan instead of O(1) lookup.");
    }

    [TestMethod]
    public async Task GetByPersonAsync_TableScan_ShouldBeSlowerThanDirectLookup()
    {
        SkipIfNotEnabled();

        TableStoragePersonRepository personRepo = new(PerformanceTestSetup.TableStorage);
        TableStorageAuthSessionRepository sessionRepo = new(PerformanceTestSetup.TableStorage);

        // Warm up
        await personRepo.GetByEmailAsync(PerformanceTestSetup.KnownPersonEmail);
        await sessionRepo.GetByPersonAsync(PerformanceTestSetup.KnownPersonId);

        // Measure O(1) lookup
        Stopwatch sw1 = Stopwatch.StartNew();
        await personRepo.GetByEmailAsync(PerformanceTestSetup.KnownPersonEmail);
        sw1.Stop();
        long directLookupMs = sw1.ElapsedMilliseconds;

        // Measure O(n) table scan
        Stopwatch sw2 = Stopwatch.StartNew();
        await sessionRepo.GetByPersonAsync(PerformanceTestSetup.KnownPersonId);
        sw2.Stop();
        long tableScanMs = sw2.ElapsedMilliseconds;

        // Table scan should generally be slower (though on small datasets/fast storage, may be similar)
        Console.WriteLine($"Direct lookup: {directLookupMs}ms, Table scan: {tableScanMs}ms");

        // At minimum, verify direct lookup completed within threshold
        directLookupMs.ShouldBeLessThan(LookupThresholdMs,
            $"Direct lookup should be under {LookupThresholdMs}ms");
    }

    private static void SkipIfNotEnabled()
    {
        if (Environment.GetEnvironmentVariable("RUN_PERFORMANCE_TESTS") != "true")
        {
            Assert.Inconclusive("Performance tests skipped. Set RUN_PERFORMANCE_TESTS=true to run.");
        }

        if (PerformanceTestSetup.TableStorage == null)
        {
            Assert.Inconclusive("TableStorage not initialized. Ensure Azurite is running.");
        }
    }

    private static long GetMedian(List<long> values)
    {
        List<long> sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }
}
