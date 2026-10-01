using System.Collections.Generic;
using Xunit;

namespace CimonPlc.UnitTests.PublicFunctions
{
    public class AddBCCTests
    {
        [Theory]
        [InlineData("X00R0AD000000101", "B9")]
        [InlineData("X00R04F4AC", "B4")]
        [InlineData("X00R0AD000004001", "BC")]
        public void AddBCC_Should_Return_Correct_Value(string input, string BCC)
        {
            //Arrange
            var frame = new List<char>();
            frame.AddRange(input.ToCharArray());

            //Act
            frame.AddBCC();

            //Assert
            Assert.Equal(input.Length + 2, frame.Count);
            Assert.Equal(BCC[1], frame[^1]);
            Assert.Equal(BCC[0], frame[^2]);
        }
    }
}
