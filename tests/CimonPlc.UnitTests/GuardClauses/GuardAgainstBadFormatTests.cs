using CimonPlc;
using System;
using Xunit;

namespace CimonPlc.UnitTests.GuardClause
{
    public class GuardAgainstBadFormatTests
    {
        [Theory]
        [InlineData("12345", @"\d{1,6}")]
        [InlineData("50FA", @"[0-9a-fA-F]{1,6}")]
        [InlineData("abfACD", @"[a-fA-F]{1,8}")]
        [InlineData("DHSTRY", @"[A-Z]+")]
        [InlineData("3498792", @"\d+")]
        public void Should_Return_input_On_Valid_Data(string input, string regexPattern)
        {
            //Act
            var result = Guard.Against.BadFormat(input, nameof(input), regexPattern);

            //Assert
            Assert.Equal(input, result);
        }

        [Theory]
        [InlineData("aaa", @"\d{1,6}")]
        [InlineData("50XA", @"[0-9a-fA-F]{1,6}")]
        [InlineData("2GudhUtG", @"[a-fA-F]+")]
        [InlineData("sDHSTRY", @"[A-Z]+")]
        [InlineData("3F498792", @"\d+")]
        public void Should_Return_Error_On_InValid_Data(string input, string regexPattern)
        {
            //Assert
            Assert.Throws<ArgumentException>(() => Guard.Against.BadFormat(input, nameof(input), regexPattern));
        }

    }
}
