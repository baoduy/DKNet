using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests
{
    public static class DateTimeExtensionsTests
    {
        public class LastDayOfMonth
        {
            [Fact]
            public void ReturnsLastDayForNonNullDate()
            {
                var date = new DateTime(new DateOnly(2024, 2, 15), TimeOnly.MinValue, DateTimeKind.Local);
                var result = date.LastDayOfMonth();
                result.ShouldBe(new DateTime(new DateOnly(2024, 2, 29), TimeOnly.MinValue, DateTimeKind.Local));
            }

            [Fact]
            public void HandlesLeapYearCorrectly()
            {
                var date = new DateTime(new DateOnly(2024, 2, 15), TimeOnly.MinValue, DateTimeKind.Local);
                var result = date.LastDayOfMonth();
                result.Day.ShouldBe(29);
            }

            [Fact]
            public void ReturnsNullForNullableNullInput()
            {
                DateTime? date = null;
                var result = date.LastDayOfMonth();
                result.ShouldBeNull();
            }

            [Theory]
            [InlineData(1, 31)]
            [InlineData(4, 30)]
            [InlineData(6, 30)]
            [InlineData(12, 31)]
            public void HandlesAllMonthsCorrectly(int month, int expectedDay)
            {
                var date = new DateTime(new DateOnly(2024, month, 15), TimeOnly.MinValue, DateTimeKind.Local);
                var result = date.LastDayOfMonth();
                result.Day.ShouldBe(expectedDay);
            }
        }

        public class Quarter
        {
            [Theory]
            [InlineData(1, 1)]
            [InlineData(3, 1)]
            [InlineData(4, 2)]
            [InlineData(6, 2)]
            [InlineData(7, 3)]
            [InlineData(9, 3)]
            [InlineData(10, 4)]
            [InlineData(12, 4)]
            public void ReturnsCorrectQuarter(int month, int expectedQuarter)
            {
                var date = new DateTime(2024, month, 1);
                var result = date.Quarter();
                result.ShouldBe(expectedQuarter);
            }

            [Fact]
            public void HandlesDateTimeMinValue()
            {
                var result = DateTime.MinValue.Quarter();
                result.ShouldBe(1);
            }
        }
    }
}