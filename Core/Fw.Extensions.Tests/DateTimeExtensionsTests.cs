using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests
{
    [TestClass]
    public static class DateTimeExtensionsTests
    {
        [TestClass]
        public class LastDayOfMonth
        {
            [TestMethod]
            public void ReturnsLastDayForNonNullDate()
            {
                var date = new DateTime(2024, 2, 15);
                var result = date.LastDayOfMonth();
                Assert.AreEqual(new DateTime(2024, 2, 29), result);
            }

            [TestMethod]
            public void HandlesLeapYearCorrectly()
            {
                var date = new DateTime(2020, 2, 15);
                var result = date.LastDayOfMonth();
                Assert.AreEqual(29, result.Day);
            }

            [TestMethod]
            public void ReturnsNullForNullableNullInput()
            {
                DateTime? date = null;
                var result = date.LastDayOfMonth();
                Assert.IsNull(result);
            }

            [DataTestMethod]
            [DataRow(1, 31)]
            [DataRow(4, 30)]
            [DataRow(6, 30)]
            [DataRow(12, 31)]
            public void HandlesAllMonthsCorrectly(int month, int expectedDay)
            {
                var date = new DateTime(2024, month, 1);
                var result = date.LastDayOfMonth();
                Assert.AreEqual(expectedDay, result.Day);
            }
        }

        [TestClass]
        public class Quarter
        {
            [DataTestMethod]
            [DataRow(1, 1)]
            [DataRow(3, 1)]
            [DataRow(4, 2)]
            [DataRow(6, 2)]
            [DataRow(7, 3)]
            [DataRow(9, 3)]
            [DataRow(10, 4)]
            [DataRow(12, 4)]
            public void ReturnsCorrectQuarter(int month, int expectedQuarter)
            {
                var date = new DateTime(2024, month, 1);
                var result = date.Quarter();
                Assert.AreEqual(expectedQuarter, result);
            }

            [TestMethod]
            public void HandlesDateTimeMinValue()
            {
                var result = DateTime.MinValue.Quarter();
                Assert.AreEqual(1, result);
            }
        }
    }
}