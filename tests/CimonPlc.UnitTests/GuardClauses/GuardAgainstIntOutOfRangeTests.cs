using CimonPlc;
using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace CimonPlc.UnitTests.GuardClauses
{
    public class GuardAgainstIntOutOfRangeTests
    {
        [Theory]
        [ClassData(typeof(CorrectClassData))]
        public void Should_Return_input_On_Valid_Data(IEnumerable<int> input, int rangeFrom, int rangeTo)
        {
            //Act
            var result = Guard.Against.OutOfRange<int>(input, nameof(input), rangeFrom, rangeTo);

            //Assert
            Assert.Equal(input, result);
        }

        [Theory]
        [ClassData(typeof(IncorrectClassData))]
        public void Should_Return_Error_On_InValid_Data(IEnumerable<int> input, int rangeFrom, int rangeTo)
        {
            //Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Against.OutOfRange<int>(input, nameof(input), rangeFrom, rangeTo));
        }

        [Theory]
        [ClassData(typeof(IncorrectRangeClassData))]
        public void Should_Return_Error_On_InValid_Range(IEnumerable<int> input, int rangeFrom, int rangeTo)
        {
            //Assert
            Assert.Throws<ArgumentException>(() => Guard.Against.OutOfRange<int>(input, nameof(input), rangeFrom, rangeTo));
        }

        public class CorrectClassData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new List<int> { 10, 12, 15 }, 10, 20 };
                yield return new object[] { new List<int> { 100, 200, 120, 180 }, 100, 200 };
                yield return new object[] { new List<int> { 10000, 12000, 10050 }, 1000, 200000 };
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class IncorrectClassData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new List<int> { 10, 12, 15 }, 10, 12 };
                yield return new object[] { new List<int> { 100, 200, 120, 180 }, 100, 150 };
                yield return new object[] { new List<int> { 10000, 12000, 10050 }, 1000, 11000 };
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class IncorrectRangeClassData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new List<int> { 10, 12, 15 }, 10, 10 };
                yield return new object[] { new List<int> { 100, 200, 120, 180 }, 200, 150 };
                yield return new object[] { new List<int> { 10000, 12000, 10050 }, 11000, 1000 };
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
