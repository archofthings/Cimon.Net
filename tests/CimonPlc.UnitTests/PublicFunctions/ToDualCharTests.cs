using Xunit;

namespace CimonPlc.UnitTests.PublicFunctions
{
    public class ToDualCharTests
    {
        [Theory]
        [InlineData(0, '0', '0')]
        [InlineData(0xA1, 'A', '1')]
        [InlineData(188, 'B', 'C')]
        [InlineData(185, 'B', '9')]
        public void AddBCC_Should_Return_Correct_Value(int input, params char[] chars)
        {
            //Act
            var result = ((byte)input).ToDualChar();

            //Assert
            Assert.Equal(2, result.Length);
            Assert.Equal(chars[0], result[0]);
            Assert.Equal(chars[1], result[1]);
        }
    }
}
