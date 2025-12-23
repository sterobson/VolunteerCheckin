//using System;

//namespace Microsoft.VisualStudio.TestTools.UnitTesting
//{
//    [AttributeUsage(AttributeTargets.Class)]
//    public sealed class TestClassAttribute : Attribute { }

//    [AttributeUsage(AttributeTargets.Method)]
//    public sealed class TestMethodAttribute : Attribute { }

//    public static class Assert
//    {
//        public static void AreEqual<T>(T expected, T actual)
//        {
//            if (!object.Equals(expected, actual))
//                throw new Exception($"Assert.AreEqual Failed. Expected:<{expected}>. Actual:<{actual}>.");
//        }

//        public static void AreEqual(double expected, double actual, double delta)
//        {
//            if (Math.Abs(expected - actual) > delta)
//                throw new Exception($"Assert.AreEqual Failed. Expected:<{expected}>. Actual:<{actual}>. Delta:<{delta}>");
//        }

//        public static void IsTrue(bool condition)
//        {
//            if (!condition) throw new Exception("Assert.IsTrue Failed.");
//        }

//        public static void IsFalse(bool condition)
//        {
//            if (condition) throw new Exception("Assert.IsFalse Failed.");
//        }

//        public static void IsNotNull(object? obj)
//        {
//            if (obj == null) throw new Exception("Assert.IsNotNull Failed.");
//        }
//    }
//}
