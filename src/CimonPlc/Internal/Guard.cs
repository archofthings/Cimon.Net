using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CimonPlc
{
    /// <summary>
    /// Lightweight argument guards used to validate public API input.
    /// </summary>
    internal sealed class Guard
    {
        public static readonly Guard Against = new Guard();

        private Guard()
        {
        }

        public T Null<T>(T input, string parameterName) where T : class
        {
            if (input is null)
                throw new ArgumentNullException(parameterName);

            return input;
        }

        public string NullOrEmpty(string input, string parameterName)
        {
            Null(input, parameterName);
            if (input.Length == 0)
                throw new ArgumentException($"Required input {parameterName} was empty.", parameterName);

            return input;
        }

        public T[] NullOrEmpty<T>(T[] input, string parameterName)
        {
            if (input is null)
                throw new ArgumentNullException(parameterName);
            if (input.Length == 0)
                throw new ArgumentException($"Required input {parameterName} was empty.", parameterName);

            return input;
        }

        public int OutOfRange(int input, string parameterName, int rangeFrom, int rangeTo)
        {
            if (input < rangeFrom || input > rangeTo)
                throw new ArgumentOutOfRangeException(parameterName, $"Input {parameterName} was out of range, it must be between {rangeFrom} and {rangeTo}.");

            return input;
        }

        public IEnumerable<T> OutOfRange<T>(IEnumerable<T> input, string parameterName, T rangeFrom, T rangeTo) where T : IComparable<T>
        {
            var comparer = Comparer<T>.Default;

            if (comparer.Compare(rangeFrom, rangeTo) >= 0)
                throw new ArgumentException($"{nameof(rangeFrom)} should be less than {nameof(rangeTo)}");

            if (input.Any(x => comparer.Compare(x, rangeFrom) < 0 || comparer.Compare(x, rangeTo) > 0))
                throw new ArgumentOutOfRangeException(parameterName, $"Input {parameterName} was out of range, it must be between {rangeFrom} and {rangeTo}.");

            return input;
        }

        public string BadFormat(string input, string parameterName, string regexPattern)
        {
            Null(input, parameterName);
            if (!Regex.IsMatch(input, $"^(?:{regexPattern})$"))
                throw new ArgumentException($"Input {parameterName} was not in required format", parameterName);

            return input;
        }

        public T InvalidData<T>(T input, string parameterName, Func<T, bool> predicate)
        {
            if (!predicate(input))
                throw new ArgumentException($"Input {parameterName} did not satisfy the options", parameterName);

            return input;
        }

        public TEnum UndefinedEnum<TEnum>(TEnum input, string parameterName) where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), input))
                throw new ArgumentOutOfRangeException(parameterName, $"Input {parameterName} is not a defined {typeof(TEnum).Name} value.");

            return input;
        }
    }
}
