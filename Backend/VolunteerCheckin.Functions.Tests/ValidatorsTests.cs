using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions.Helpers;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class ValidatorsTests
    {
        #region Latitude Tests

        [TestMethod]
        public void IsValidLatitude_ValidValue_ReturnsTrue()
        {
            Validators.IsValidLatitude(45.5).ShouldBeTrue();
            Validators.IsValidLatitude(0).ShouldBeTrue();
            Validators.IsValidLatitude(-45.5).ShouldBeTrue();
        }

        [TestMethod]
        public void IsValidLatitude_BoundaryValues_ReturnsTrue()
        {
            Validators.IsValidLatitude(90).ShouldBeTrue();
            Validators.IsValidLatitude(-90).ShouldBeTrue();
        }

        [TestMethod]
        public void IsValidLatitude_OutOfRange_ReturnsFalse()
        {
            Validators.IsValidLatitude(90.1).ShouldBeFalse();
            Validators.IsValidLatitude(-90.1).ShouldBeFalse();
            Validators.IsValidLatitude(180).ShouldBeFalse();
            Validators.IsValidLatitude(-180).ShouldBeFalse();
        }

        #endregion

        #region Longitude Tests

        [TestMethod]
        public void IsValidLongitude_ValidValue_ReturnsTrue()
        {
            Validators.IsValidLongitude(120.5).ShouldBeTrue();
            Validators.IsValidLongitude(0).ShouldBeTrue();
            Validators.IsValidLongitude(-120.5).ShouldBeTrue();
        }

        [TestMethod]
        public void IsValidLongitude_BoundaryValues_ReturnsTrue()
        {
            Validators.IsValidLongitude(180).ShouldBeTrue();
            Validators.IsValidLongitude(-180).ShouldBeTrue();
        }

        [TestMethod]
        public void IsValidLongitude_OutOfRange_ReturnsFalse()
        {
            Validators.IsValidLongitude(180.1).ShouldBeFalse();
            Validators.IsValidLongitude(-180.1).ShouldBeFalse();
            Validators.IsValidLongitude(360).ShouldBeFalse();
        }

        #endregion

        #region Coordinates Tests

        [TestMethod]
        public void IsValidCoordinates_BothValid_ReturnsTrue()
        {
            Validators.IsValidCoordinates(45.5, 120.5).ShouldBeTrue();
            Validators.IsValidCoordinates(0, 0).ShouldBeTrue();
        }

        [TestMethod]
        public void IsValidCoordinates_InvalidLatitude_ReturnsFalse()
        {
            Validators.IsValidCoordinates(91, 120).ShouldBeFalse();
            Validators.IsValidCoordinates(-91, 120).ShouldBeFalse();
        }

        [TestMethod]
        public void IsValidCoordinates_InvalidLongitude_ReturnsFalse()
        {
            Validators.IsValidCoordinates(45, 181).ShouldBeFalse();
            Validators.IsValidCoordinates(45, -181).ShouldBeFalse();
        }

        [TestMethod]
        public void IsValidCoordinates_BothInvalid_ReturnsFalse()
        {
            Validators.IsValidCoordinates(91, 181).ShouldBeFalse();
        }

        #endregion

        #region Email Tests

        [TestMethod]
        public void IsValidEmail_ValidFormats_ReturnsTrue()
        {
            Validators.IsValidEmail("user@example.com").ShouldBeTrue();
            Validators.IsValidEmail("test.user@example.com").ShouldBeTrue();
            Validators.IsValidEmail("user+tag@example.co.uk").ShouldBeTrue();
            Validators.IsValidEmail("user_123@example-site.com").ShouldBeTrue();
        }

        [TestMethod]
        public void IsValidEmail_InvalidFormats_ReturnsFalse()
        {
            Validators.IsValidEmail("notanemail").ShouldBeFalse();
            Validators.IsValidEmail("@example.com").ShouldBeFalse();
            Validators.IsValidEmail("user@").ShouldBeFalse();
            Validators.IsValidEmail("user@.com").ShouldBeFalse();
            Validators.IsValidEmail("user @example.com").ShouldBeFalse();
            Validators.IsValidEmail("user@example .com").ShouldBeFalse();
        }

        [TestMethod]
        public void IsValidEmail_NullOrEmpty_ReturnsFalse()
        {
            Validators.IsValidEmail(null).ShouldBeFalse();
            Validators.IsValidEmail("").ShouldBeFalse();
            Validators.IsValidEmail("   ").ShouldBeFalse();
        }

        #endregion

        #region What3Words Tests

        [TestMethod]
        public void IsValidWhat3Words_ValidFormats_ReturnsTrue()
        {
            Validators.IsValidWhat3Words("filled.count.soap").ShouldBeTrue();
            Validators.IsValidWhat3Words("filled/count/soap").ShouldBeTrue();
            Validators.IsValidWhat3Words("a.b.c").ShouldBeTrue();
        }

        [TestMethod]
        public void IsValidWhat3Words_EmptyOrNull_ReturnsTrue()
        {
            // What3Words is optional
            Validators.IsValidWhat3Words(null).ShouldBeTrue();
            Validators.IsValidWhat3Words("").ShouldBeTrue();
            Validators.IsValidWhat3Words("   ").ShouldBeTrue();
        }

        [TestMethod]
        public void IsValidWhat3Words_InvalidFormats_ReturnsFalse()
        {
            Validators.IsValidWhat3Words("only.two").ShouldBeFalse();
            Validators.IsValidWhat3Words("too.many.words.here").ShouldBeFalse();
            Validators.IsValidWhat3Words("UPPER.case.words").ShouldBeFalse();
            Validators.IsValidWhat3Words("has.numbers1.test").ShouldBeFalse();
            Validators.IsValidWhat3Words("has.special!.chars").ShouldBeFalse();
        }

        [TestMethod]
        public void IsValidWhat3Words_MixedSeparators_ReturnsFalse()
        {
            Validators.IsValidWhat3Words("mixed.separators/here").ShouldBeFalse();
            Validators.IsValidWhat3Words("dot.slash/word").ShouldBeFalse();
        }

        [TestMethod]
        public void IsValidWhat3Words_TooLongWords_ReturnsFalse()
        {
            string longWord = new('a', 21);
            Validators.IsValidWhat3Words($"{longWord}.word.word").ShouldBeFalse();
            Validators.IsValidWhat3Words($"word.{longWord}.word").ShouldBeFalse();
            Validators.IsValidWhat3Words($"word.word.{longWord}").ShouldBeFalse();
        }

        #endregion

        #region Positive/NonNegative Tests

        [TestMethod]
        public void IsPositive_PositiveValue_ReturnsTrue()
        {
            Validators.IsPositive(1).ShouldBeTrue();
            Validators.IsPositive(100).ShouldBeTrue();
            Validators.IsPositive(int.MaxValue).ShouldBeTrue();
        }

        [TestMethod]
        public void IsPositive_ZeroOrNegative_ReturnsFalse()
        {
            Validators.IsPositive(0).ShouldBeFalse();
            Validators.IsPositive(-1).ShouldBeFalse();
            Validators.IsPositive(-100).ShouldBeFalse();
            Validators.IsPositive(int.MinValue).ShouldBeFalse();
        }

        [TestMethod]
        public void IsNonNegative_NonNegativeValue_ReturnsTrue()
        {
            Validators.IsNonNegative(0).ShouldBeTrue();
            Validators.IsNonNegative(1).ShouldBeTrue();
            Validators.IsNonNegative(100).ShouldBeTrue();
            Validators.IsNonNegative(int.MaxValue).ShouldBeTrue();
        }

        [TestMethod]
        public void IsNonNegative_NegativeValue_ReturnsFalse()
        {
            Validators.IsNonNegative(-1).ShouldBeFalse();
            Validators.IsNonNegative(-100).ShouldBeFalse();
            Validators.IsNonNegative(int.MinValue).ShouldBeFalse();
        }

        #endregion
    }
}
