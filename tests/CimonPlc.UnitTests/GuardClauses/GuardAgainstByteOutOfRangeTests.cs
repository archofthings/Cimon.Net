using CimonPlc;
using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace CimonPlc.UnitTests.GuardClauses
{
    public class GuardAgainstByteOutOfRangeTests
    {

        [Theory]
        [ClassData(typeof(CorrectClassData))]
        public void Should_Return_input_On_Valid_Data(IEnumerable<byte> input, byte rangeFrom, byte rangeTo)
        {
            //Act
            var result = Guard.Against.OutOfRange<byte>(input, nameof(input), rangeFrom, rangeTo);

            //Assert
            Assert.Equal(input, result);
        }

        [Theory]
        [ClassData(typeof(IncorrectClassData))]
        public void Should_Return_Error_On_InValid_Data(IEnumerable<byte> input, byte rangeFrom, byte rangeTo)
        {
            //Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Against.OutOfRange<byte>(input, nameof(input), rangeFrom, rangeTo));
        }

        [Theory]
        [ClassData(typeof(IncorrectRangeClassData))]
        public void Should_Return_Error_On_InValid_Range(IEnumerable<byte> input, byte rangeFrom, byte rangeTo)
        {
            //Assert
            Assert.Throws<ArgumentException>(() => Guard.Against.OutOfRange<byte>(input, nameof(input), rangeFrom, rangeTo));
        }

        public class CorrectClassData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new List<byte> { 10, 12, 15 }, 10, 20 };
                yield return new object[] { new List<byte> { 100, 200, 120, 180 }, 100, 200 };
                yield return new object[] { new List<byte> { 18, 128, 108 }, 0, 200 };
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class IncorrectClassData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new List<byte> { 10, 12, 15 }, 10, 12 };
                yield return new object[] { new List<byte> { 100, 200, 120, 180 }, 100, 150 };
                yield return new object[] { new List<byte> { 15, 120, 158 }, 10, 110 };
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class IncorrectRangeClassData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new List<byte> { 10, 12, 15 }, 10, 10 };
                yield return new object[] { new List<byte> { 100, 200, 120, 180 }, 200, 150 };
                yield return new object[] { new List<byte> { 52, 86, 250 }, 200, 100 };
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
