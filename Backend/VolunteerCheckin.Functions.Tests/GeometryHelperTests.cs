using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.Collections.Generic;
using System.Text.Json;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class GeometryHelperTests
{
    #region IsPointInPolygon - Invalid Input

    [TestMethod]
    public void IsPointInPolygon_NullPolygon_ReturnsFalse()
    {
        RoutePoint point = new(51.5, -0.1);

        bool result = GeometryHelper.IsPointInPolygon(point, null!);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPointInPolygon_EmptyPolygon_ReturnsFalse()
    {
        RoutePoint point = new(51.5, -0.1);
        List<RoutePoint> polygon = [];

        bool result = GeometryHelper.IsPointInPolygon(point, polygon);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPointInPolygon_OnePointPolygon_ReturnsFalse()
    {
        RoutePoint point = new(51.5, -0.1);
        List<RoutePoint> polygon = [new(51.5, -0.1)];

        bool result = GeometryHelper.IsPointInPolygon(point, polygon);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPointInPolygon_TwoPointPolygon_ReturnsFalse()
    {
        RoutePoint point = new(51.5, -0.1);
        List<RoutePoint> polygon = [new(51.0, -0.2), new(52.0, 0.0)];

        bool result = GeometryHelper.IsPointInPolygon(point, polygon);

        result.ShouldBeFalse();
    }

    #endregion

    #region IsPointInPolygon - Square Polygon

    [TestMethod]
    public void IsPointInPolygon_PointInsideSquare_ReturnsTrue()
    {
        // Square from (0,0) to (10,10)
        List<RoutePoint> square =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];
        RoutePoint pointInside = new(5, 5);

        bool result = GeometryHelper.IsPointInPolygon(pointInside, square);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsPointInPolygon_PointOutsideSquare_ReturnsFalse()
    {
        List<RoutePoint> square =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];
        RoutePoint pointOutside = new(15, 15);

        bool result = GeometryHelper.IsPointInPolygon(pointOutside, square);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPointInPolygon_PointAboveSquare_ReturnsFalse()
    {
        List<RoutePoint> square =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];
        RoutePoint pointAbove = new(15, 5);

        bool result = GeometryHelper.IsPointInPolygon(pointAbove, square);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPointInPolygon_PointBelowSquare_ReturnsFalse()
    {
        List<RoutePoint> square =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];
        RoutePoint pointBelow = new(-5, 5);

        bool result = GeometryHelper.IsPointInPolygon(pointBelow, square);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPointInPolygon_PointLeftOfSquare_ReturnsFalse()
    {
        List<RoutePoint> square =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];
        RoutePoint pointLeft = new(5, -5);

        bool result = GeometryHelper.IsPointInPolygon(pointLeft, square);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPointInPolygon_PointRightOfSquare_ReturnsFalse()
    {
        List<RoutePoint> square =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];
        RoutePoint pointRight = new(5, 15);

        bool result = GeometryHelper.IsPointInPolygon(pointRight, square);

        result.ShouldBeFalse();
    }

    #endregion

    #region IsPointInPolygon - Triangle

    [TestMethod]
    public void IsPointInPolygon_PointInsideTriangle_ReturnsTrue()
    {
        List<RoutePoint> triangle =
        [
            new(0, 5),   // Top
            new(10, 0),  // Bottom left
            new(10, 10)  // Bottom right
        ];
        RoutePoint pointInside = new(5, 5);

        bool result = GeometryHelper.IsPointInPolygon(pointInside, triangle);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsPointInPolygon_PointOutsideTriangle_ReturnsFalse()
    {
        List<RoutePoint> triangle =
        [
            new(0, 5),
            new(10, 0),
            new(10, 10)
        ];
        RoutePoint pointOutside = new(0, 0);

        bool result = GeometryHelper.IsPointInPolygon(pointOutside, triangle);

        result.ShouldBeFalse();
    }

    #endregion

    #region IsPointInPolygon - Concave Polygon (L-Shape)

    [TestMethod]
    public void IsPointInPolygon_PointInsideLShape_ReturnsTrue()
    {
        // L-shaped polygon
        List<RoutePoint> lShape =
        [
            new(0, 0),
            new(0, 5),
            new(5, 5),
            new(5, 10),
            new(10, 10),
            new(10, 0)
        ];
        RoutePoint pointInside = new(2, 2);

        bool result = GeometryHelper.IsPointInPolygon(pointInside, lShape);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsPointInPolygon_PointInConcavePartOfLShape_ReturnsFalse()
    {
        // L-shaped polygon - point in the "cut out" part
        List<RoutePoint> lShape =
        [
            new(0, 0),
            new(0, 5),
            new(5, 5),
            new(5, 10),
            new(10, 10),
            new(10, 0)
        ];
        RoutePoint pointInCutout = new(2, 7);

        bool result = GeometryHelper.IsPointInPolygon(pointInCutout, lShape);

        result.ShouldBeFalse();
    }

    #endregion

    #region IsPointInPolygon - Real-World Coordinates

    [TestMethod]
    public void IsPointInPolygon_PointInsideLondonArea_ReturnsTrue()
    {
        // Rough polygon around central London
        List<RoutePoint> londonArea =
        [
            new(51.52, -0.15),  // North West
            new(51.52, -0.08),  // North East
            new(51.49, -0.08),  // South East
            new(51.49, -0.15)   // South West
        ];
        RoutePoint bigBen = new(51.5007, -0.1246);

        bool result = GeometryHelper.IsPointInPolygon(bigBen, londonArea);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsPointInPolygon_PointOutsideLondonArea_ReturnsFalse()
    {
        List<RoutePoint> londonArea =
        [
            new(51.52, -0.15),
            new(51.52, -0.08),
            new(51.49, -0.08),
            new(51.49, -0.15)
        ];
        RoutePoint manchester = new(53.4808, -2.2426);

        bool result = GeometryHelper.IsPointInPolygon(manchester, londonArea);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPointInPolygon_WithNegativeCoordinates_WorksCorrectly()
    {
        // Polygon with negative lat/lng (Southern/Western hemisphere)
        List<RoutePoint> polygon =
        [
            new(-34.0, 150.0),
            new(-34.0, 152.0),
            new(-33.0, 152.0),
            new(-33.0, 150.0)
        ];
        RoutePoint sydney = new(-33.8688, 151.2093);

        bool result = GeometryHelper.IsPointInPolygon(sydney, polygon);

        result.ShouldBeTrue();
    }

    #endregion

    #region CalculateCheckpointAreas - Basic Cases

    [TestMethod]
    public void CalculateCheckpointAreas_CheckpointInOneArea_ReturnsAreaId()
    {
        List<RoutePoint> polygon =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];

        List<AreaEntity> areas =
        [
            new()
            {
                RowKey = "area-1",
                IsDefault = false,
                PolygonJson = JsonSerializer.Serialize(polygon)
            }
        ];

        List<string> result = GeometryHelper.CalculateCheckpointAreas(5, 5, areas, "default-area");

        result.ShouldBe(["area-1"]);
    }

    [TestMethod]
    public void CalculateCheckpointAreas_CheckpointOutsideAllAreas_ReturnsDefaultArea()
    {
        List<RoutePoint> polygon =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];

        List<AreaEntity> areas =
        [
            new()
            {
                RowKey = "area-1",
                IsDefault = false,
                PolygonJson = JsonSerializer.Serialize(polygon)
            }
        ];

        List<string> result = GeometryHelper.CalculateCheckpointAreas(50, 50, areas, "default-area");

        result.ShouldBe(["default-area"]);
    }

    [TestMethod]
    public void CalculateCheckpointAreas_CheckpointInMultipleOverlappingAreas_ReturnsAllAreaIds()
    {
        List<RoutePoint> largePolygon =
        [
            new(0, 0),
            new(0, 20),
            new(20, 20),
            new(20, 0)
        ];
        List<RoutePoint> smallPolygon =
        [
            new(5, 5),
            new(5, 15),
            new(15, 15),
            new(15, 5)
        ];

        List<AreaEntity> areas =
        [
            new()
            {
                RowKey = "large-area",
                IsDefault = false,
                PolygonJson = JsonSerializer.Serialize(largePolygon)
            },
            new()
            {
                RowKey = "small-area",
                IsDefault = false,
                PolygonJson = JsonSerializer.Serialize(smallPolygon)
            }
        ];

        // Point at (10, 10) is inside both polygons
        List<string> result = GeometryHelper.CalculateCheckpointAreas(10, 10, areas, "default-area");

        result.Count.ShouldBe(2);
        result.ShouldContain("large-area");
        result.ShouldContain("small-area");
    }

    [TestMethod]
    public void CalculateCheckpointAreas_SkipsDefaultArea()
    {
        List<RoutePoint> polygon =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];

        List<AreaEntity> areas =
        [
            new()
            {
                RowKey = "default-area",
                IsDefault = true,
                PolygonJson = JsonSerializer.Serialize(polygon)
            },
            new()
            {
                RowKey = "area-1",
                IsDefault = false,
                PolygonJson = JsonSerializer.Serialize(polygon)
            }
        ];

        List<string> result = GeometryHelper.CalculateCheckpointAreas(5, 5, areas, "default-area");

        // Should only include area-1, not the default area
        result.ShouldBe(["area-1"]);
    }

    [TestMethod]
    public void CalculateCheckpointAreas_EmptyAreasList_ReturnsDefaultArea()
    {
        List<AreaEntity> areas = [];

        List<string> result = GeometryHelper.CalculateCheckpointAreas(5, 5, areas, "default-area");

        result.ShouldBe(["default-area"]);
    }

    [TestMethod]
    public void CalculateCheckpointAreas_AreaWithEmptyPolygon_SkipsArea()
    {
        List<AreaEntity> areas =
        [
            new()
            {
                RowKey = "area-1",
                IsDefault = false,
                PolygonJson = "[]"
            }
        ];

        List<string> result = GeometryHelper.CalculateCheckpointAreas(5, 5, areas, "default-area");

        result.ShouldBe(["default-area"]);
    }

    [TestMethod]
    public void CalculateCheckpointAreas_OnlyDefaultAreas_ReturnsDefaultArea()
    {
        List<RoutePoint> polygon =
        [
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        ];

        List<AreaEntity> areas =
        [
            new()
            {
                RowKey = "default-1",
                IsDefault = true,
                PolygonJson = JsonSerializer.Serialize(polygon)
            },
            new()
            {
                RowKey = "default-2",
                IsDefault = true,
                PolygonJson = JsonSerializer.Serialize(polygon)
            }
        ];

        List<string> result = GeometryHelper.CalculateCheckpointAreas(5, 5, areas, "fallback-default");

        // All areas are default so they're skipped, returning the fallback default
        result.ShouldBe(["fallback-default"]);
    }

    #endregion

    #region CalculateCheckpointAreas - Real-World Scenario

    [TestMethod]
    public void CalculateCheckpointAreas_RealWorldEventSetup_WorksCorrectly()
    {
        // Simulate a marathon with multiple zones
        List<RoutePoint> startZone =
        [
            new(51.50, -0.12),
            new(51.50, -0.10),
            new(51.51, -0.10),
            new(51.51, -0.12)
        ];
        List<RoutePoint> finishZone =
        [
            new(51.52, -0.14),
            new(51.52, -0.12),
            new(51.53, -0.12),
            new(51.53, -0.14)
        ];

        List<AreaEntity> areas =
        [
            new()
            {
                RowKey = "start-zone",
                IsDefault = false,
                PolygonJson = JsonSerializer.Serialize(startZone)
            },
            new()
            {
                RowKey = "finish-zone",
                IsDefault = false,
                PolygonJson = JsonSerializer.Serialize(finishZone)
            },
            new()
            {
                RowKey = "default-zone",
                IsDefault = true,
                PolygonJson = "[]"
            }
        ];

        // Checkpoint in start zone
        List<string> startResult = GeometryHelper.CalculateCheckpointAreas(51.505, -0.11, areas, "default-zone");
        startResult.ShouldBe(["start-zone"]);

        // Checkpoint in finish zone
        List<string> finishResult = GeometryHelper.CalculateCheckpointAreas(51.525, -0.13, areas, "default-zone");
        finishResult.ShouldBe(["finish-zone"]);

        // Checkpoint outside both zones (mid-course)
        List<string> midCourseResult = GeometryHelper.CalculateCheckpointAreas(51.515, -0.15, areas, "default-zone");
        midCourseResult.ShouldBe(["default-zone"]);
    }

    #endregion
}
