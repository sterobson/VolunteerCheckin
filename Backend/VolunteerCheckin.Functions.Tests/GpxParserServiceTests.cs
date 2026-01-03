using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class GpxParserServiceTests
    {
        [TestMethod]
        public void ParseGpxFile_EmptyStream_ReturnsEmptyList()
        {
            using MemoryStream ms = new();

            List<RoutePoint> routes = GpxParserService.ParseGpxFile(ms);

            routes.ShouldNotBeNull();
            routes.Count.ShouldBe(0);
        }
    }
}
