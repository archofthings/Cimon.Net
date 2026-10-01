using Xunit;

namespace CimonPlc.UnitTests.PublicFunctions
{
    public class ToQuadCharTests
    {
        [Theory]
        [InlineData(0, '0', '0', '0', '0')]
        [InlineData(0xA1, '0', '0', 'A', '1')]
        [InlineData(18841, '4', '9', '9', '9')]
        [InlineData(188417, 'E', '0', '0', '1')]
        public void AddBCC_Should_Return_Correct_Value(int input, params char[] chars)
        {
            //Act
            var result = input.ToQuadChar();

            //Assert
            Assert.Equal(4, result.Length);
            Assert.Equal(chars[0], result[0]);
            Assert.Equal(chars[1], result[1]);
            Assert.Equal(chars[2], result[2]);
            Assert.Equal(chars[3], result[3]);
        }
    }
}
