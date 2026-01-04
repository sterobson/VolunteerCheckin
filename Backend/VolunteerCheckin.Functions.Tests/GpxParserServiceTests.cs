using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class GpxParserServiceTests
{
    #region Empty and Basic Cases

    [TestMethod]
    public void ParseGpxFile_EmptyStream_ReturnsEmptyList()
    {
        using MemoryStream ms = new();

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.ShouldNotBeNull();
        routes.Count.ShouldBe(0);
    }

    [TestMethod]
    public void ParseGpxFile_EmptyGpxDocument_ReturnsEmptyList()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.ShouldNotBeNull();
        routes.Count.ShouldBe(0);
    }

    #endregion

    #region Track Points (trkpt)

    [TestMethod]
    public void ParseGpxFile_WithTrackPoints_ReturnsRoutePoints()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <name>Test Track</name>
                <trkseg>
                  <trkpt lat="51.5074" lon="-0.1278">
                    <ele>10</ele>
                  </trkpt>
                  <trkpt lat="51.5080" lon="-0.1290">
                    <ele>15</ele>
                  </trkpt>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.ShouldNotBeNull();
        routes.Count.ShouldBe(2);
        routes[0].Lat.ShouldBe(51.5074);
        routes[0].Lng.ShouldBe(-0.1278);
        routes[1].Lat.ShouldBe(51.5080);
        routes[1].Lng.ShouldBe(-0.1290);
    }

    [TestMethod]
    public void ParseGpxFile_WithMultipleTrackSegments_ReturnsAllPoints()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="51.0" lon="-0.1"/>
                </trkseg>
                <trkseg>
                  <trkpt lat="52.0" lon="-0.2"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(2);
        routes[0].Lat.ShouldBe(51.0);
        routes[1].Lat.ShouldBe(52.0);
    }

    [TestMethod]
    public void ParseGpxFile_WithoutNamespace_ReturnsRoutePoints()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1">
              <trk>
                <trkseg>
                  <trkpt lat="51.5074" lon="-0.1278"/>
                  <trkpt lat="51.5080" lon="-0.1290"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(2);
        routes[0].Lat.ShouldBe(51.5074);
        routes[0].Lng.ShouldBe(-0.1278);
    }

    #endregion

    #region Route Points (rtept) - Fallback

    [TestMethod]
    public void ParseGpxFile_WithRoutePoints_WhenNoTrackPoints_ReturnsRoutePoints()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <rte>
                <name>Test Route</name>
                <rtept lat="51.5074" lon="-0.1278"/>
                <rtept lat="51.5080" lon="-0.1290"/>
              </rte>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(2);
        routes[0].Lat.ShouldBe(51.5074);
        routes[0].Lng.ShouldBe(-0.1278);
    }

    [TestMethod]
    public void ParseGpxFile_WithRoutePointsWithoutNamespace_ReturnsRoutePoints()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1">
              <rte>
                <rtept lat="51.5074" lon="-0.1278"/>
              </rte>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(1);
        routes[0].Lat.ShouldBe(51.5074);
    }

    [TestMethod]
    public void ParseGpxFile_PrefersTrackPoints_OverRoutePoints()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="51.0" lon="-0.1"/>
                </trkseg>
              </trk>
              <rte>
                <rtept lat="52.0" lon="-0.2"/>
              </rte>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        // Should only have track point, not route point
        routes.Count.ShouldBe(1);
        routes[0].Lat.ShouldBe(51.0);
    }

    #endregion

    #region Waypoints (wpt) - Fallback

    [TestMethod]
    public void ParseGpxFile_WithWaypoints_WhenNoTrackOrRoutePoints_ReturnsWaypoints()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <wpt lat="51.5074" lon="-0.1278">
                <name>Point A</name>
              </wpt>
              <wpt lat="51.5080" lon="-0.1290">
                <name>Point B</name>
              </wpt>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(2);
        routes[0].Lat.ShouldBe(51.5074);
        routes[1].Lat.ShouldBe(51.5080);
    }

    [TestMethod]
    public void ParseGpxFile_WithWaypointsWithoutNamespace_ReturnsWaypoints()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1">
              <wpt lat="51.5074" lon="-0.1278"/>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(1);
        routes[0].Lat.ShouldBe(51.5074);
    }

    [TestMethod]
    public void ParseGpxFile_PrefersRoutePoints_OverWaypoints()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <rte>
                <rtept lat="51.0" lon="-0.1"/>
              </rte>
              <wpt lat="52.0" lon="-0.2"/>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        // Should only have route point, not waypoint
        routes.Count.ShouldBe(1);
        routes[0].Lat.ShouldBe(51.0);
    }

    #endregion

    #region Invalid Coordinates

    [TestMethod]
    public void ParseGpxFile_WithMissingLatitude_SkipsPoint()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lon="-0.1278"/>
                  <trkpt lat="51.5080" lon="-0.1290"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(1);
        routes[0].Lat.ShouldBe(51.5080);
    }

    [TestMethod]
    public void ParseGpxFile_WithMissingLongitude_SkipsPoint()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="51.5074"/>
                  <trkpt lat="51.5080" lon="-0.1290"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(1);
        routes[0].Lat.ShouldBe(51.5080);
    }

    [TestMethod]
    public void ParseGpxFile_WithInvalidLatitude_SkipsPoint()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="not-a-number" lon="-0.1278"/>
                  <trkpt lat="51.5080" lon="-0.1290"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(1);
        routes[0].Lat.ShouldBe(51.5080);
    }

    [TestMethod]
    public void ParseGpxFile_WithInvalidLongitude_SkipsPoint()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="51.5074" lon="invalid"/>
                  <trkpt lat="51.5080" lon="-0.1290"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(1);
        routes[0].Lat.ShouldBe(51.5080);
    }

    [TestMethod]
    public void ParseGpxFile_WithNegativeCoordinates_ParsesCorrectly()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="-33.8688" lon="151.2093"/>
                  <trkpt lat="-34.0522" lon="-118.2437"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(2);
        routes[0].Lat.ShouldBe(-33.8688);
        routes[0].Lng.ShouldBe(151.2093);
        routes[1].Lat.ShouldBe(-34.0522);
        routes[1].Lng.ShouldBe(-118.2437);
    }

    #endregion

    #region Route Simplification (Ramer-Douglas-Peucker)

    [TestMethod]
    public void ParseGpxFile_WithTwoPoints_NoSimplification()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="51.0" lon="-0.1"/>
                  <trkpt lat="52.0" lon="-0.2"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(2);
    }

    [TestMethod]
    public void ParseGpxFile_WithCollinearPoints_SimplifiesToEndpoints()
    {
        // Three points on a straight line should simplify to just start and end
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="51.0" lon="0.0"/>
                  <trkpt lat="51.5" lon="0.0"/>
                  <trkpt lat="52.0" lon="0.0"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        // Collinear points should be simplified to just start and end
        routes.Count.ShouldBe(2);
        routes[0].Lat.ShouldBe(51.0);
        routes[1].Lat.ShouldBe(52.0);
    }

    [TestMethod]
    public void ParseGpxFile_WithPointFarFromLine_PreservesPoint()
    {
        // Middle point is significantly off the line - should be preserved
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="51.0" lon="0.0"/>
                  <trkpt lat="51.5" lon="1.0"/>
                  <trkpt lat="52.0" lon="0.0"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        // Middle point is far from line, should be preserved
        routes.Count.ShouldBe(3);
        routes[1].Lng.ShouldBe(1.0);
    }

    [TestMethod]
    public void ParseGpxFile_WithManyCollinearPoints_SimplifiesEfficiently()
    {
        // Many points on a straight line
        StringBuilder gpxBuilder = new();
        gpxBuilder.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        gpxBuilder.AppendLine("""<gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">""");
        gpxBuilder.AppendLine("<trk><trkseg>");

        for (int i = 0; i < 100; i++)
        {
            double lat = 51.0 + (i * 0.01);
            gpxBuilder.AppendLine($"""<trkpt lat="{lat}" lon="0.0"/>""");
        }

        gpxBuilder.AppendLine("</trkseg></trk></gpx>");

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpxBuilder.ToString()));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        // Should simplify significantly since all points are collinear
        routes.Count.ShouldBeLessThan(10);
    }

    [TestMethod]
    public void ParseGpxFile_WithZigZagRoute_PreservesCorners()
    {
        // Zig-zag pattern should preserve all corner points
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="51.0" lon="0.0"/>
                  <trkpt lat="51.0" lon="1.0"/>
                  <trkpt lat="52.0" lon="1.0"/>
                  <trkpt lat="52.0" lon="0.0"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        // Corner points should all be preserved
        routes.Count.ShouldBe(4);
    }

    #endregion

    #region Error Handling

    [TestMethod]
    public void ParseGpxFile_WithInvalidXml_ThrowsInvalidOperationException()
    {
        string invalidXml = "this is not valid xml <>";

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(invalidXml));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            GpxParserService.ParseGpxFile(ms));

        exception.Message.ShouldContain("Failed to parse GPX file");
    }

    [TestMethod]
    public void ParseGpxFile_WithMalformedXml_ThrowsInvalidOperationException()
    {
        string malformedXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx>
              <trk>
                <trkseg>
                  <trkpt lat="51.0" lon="0.0">
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(malformedXml));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            GpxParserService.ParseGpxFile(ms));

        exception.Message.ShouldContain("Failed to parse GPX file");
    }

    #endregion

    #region Real-World Scenarios

    [TestMethod]
    public void ParseGpxFile_WithTypicalGpxFromDevice_ParsesCorrectly()
    {
        // Simulates GPX output from a typical GPS device with a corner turn
        // Points form an L-shape to avoid simplification
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" creator="Garmin Connect"
                 xmlns="http://www.topografix.com/GPX/1/1"
                 xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">
              <metadata>
                <name>Morning Run</name>
                <time>2024-01-15T08:30:00Z</time>
              </metadata>
              <trk>
                <name>Morning Run</name>
                <type>running</type>
                <trkseg>
                  <trkpt lat="51.5000" lon="-0.1000">
                    <ele>11</ele>
                    <time>2024-01-15T08:30:00Z</time>
                  </trkpt>
                  <trkpt lat="51.5000" lon="-0.2000">
                    <ele>12</ele>
                    <time>2024-01-15T08:30:30Z</time>
                  </trkpt>
                  <trkpt lat="51.6000" lon="-0.2000">
                    <ele>13</ele>
                    <time>2024-01-15T08:31:00Z</time>
                  </trkpt>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(3);
        routes[0].Lat.ShouldBe(51.5);
        routes[0].Lng.ShouldBe(-0.1);
    }

    [TestMethod]
    public void ParseGpxFile_WithHighPrecisionCoordinates_PreservesPrecision()
    {
        string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="51.50735094" lon="-0.12775829"/>
                </trkseg>
              </trk>
            </gpx>
            """;

        using MemoryStream ms = new(Encoding.UTF8.GetBytes(gpx));

        List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

        routes.Count.ShouldBe(1);
        routes[0].Lat.ShouldBe(51.50735094, 0.00000001);
        routes[0].Lng.ShouldBe(-0.12775829, 0.00000001);
    }

    #endregion
}
