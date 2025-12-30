using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class GpsServiceTests
    {
        private GpsService _gpsService = null!;

        [TestInitialize]
        public void Setup()
        {
            _gpsService = new GpsService();
        }

        [TestMethod]
        public void CalculateDistance_SamePoint_ReturnsZero()
        {
            double lat = 47.6062;
            double lon = -122.3321;

            double distance = _gpsService.CalculateDistance(lat, lon, lat, lon);

            distance.ShouldBe(0, 1e-6);
        }

        [TestMethod]
        public void IsWithinRadius_PointInsideRadius_ReturnsTrue()
        {
            double lat1 = 47.6062;
            double lon1 = -122.3321;
            double lat2 = 47.6063;
            double lon2 = -122.3320;

            bool inside = _gpsService.IsWithinRadius(lat1, lon1, lat2, lon2, 50); // 50 meters

            inside.ShouldBeTrue();
        }

        [TestMethod]
        public void IsWithinRadius_PointOutsideRadius_ReturnsFalse()
        {
            double lat1 = 47.6062;
            double lon1 = -122.3321;
            double lat2 = 40.7128;
            double lon2 = -74.0060;

            bool inside = _gpsService.IsWithinRadius(lat1, lon1, lat2, lon2, 1000); // 1 km

            inside.ShouldBeFalse();
        }
    }
}
