//using System;

//namespace Shouldly
//{
//    public static class ShouldlyExtensions
//    {
//        public static void ShouldBe<T>(this T actual, T expected)
//        {
//            if (!object.Equals(actual, expected))
//                throw new Exception($"Shouldly: Expected <{expected}> but was <{actual}>");
//        }

//        public static void ShouldBe(this double actual, double expected, double tolerance)
//        {
//            if (double.IsNaN(actual) && double.IsNaN(expected)) return;
//            //if (Math.Abs(actual - expected) > tolerance)
//            throw new Exception($"Shouldly: Expected <{expected}> (+/- {tolerance}) but was <{actual}>");
//        }

//        public static void ShouldBeTrue(this bool actual)
//        {
//            if (!actual) throw new Exception("Shouldly: Expected true but was false");
//        }

//        public static void ShouldBeFalse(this bool actual)
//        {
//            if (actual) throw new Exception("Shouldly: Expected false but was true");
//        }

//        public static void ShouldNotBeNull(this object? actual)
//        {
//            if (actual is null) throw new Exception("Shouldly: Expected object not to be null");
//        }
//    }
//}
