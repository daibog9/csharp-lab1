using System.Globalization;
using System.Numerics;

namespace MyExcel.Core.Expressions
{
	public readonly struct Rational : IEquatable<Rational>
	{
		private const int DisplayFractionDigits = 10;
		private const int DecimalBase = 10;
		private const int RoundingFactor = 2;

		private readonly BigInteger _denominator;

		public Rational(BigInteger value)
		{
			Numerator = value;
			_denominator = BigInteger.One;
		}

		public Rational(BigInteger numerator, BigInteger denominator)
		{
			if (denominator.IsZero)
			{
				throw new DivideByZeroException();
			}

			if (denominator.Sign < 0)
			{
				numerator = -numerator;
				denominator = -denominator;
			}

			BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
			Numerator = numerator / divisor;
			_denominator = denominator / divisor;
		}

		public static Rational Zero => new Rational(BigInteger.Zero);

		public static Rational One => new Rational(BigInteger.One);

		public BigInteger Numerator { get; }

		public BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;

		public bool IsZero => Numerator.IsZero;

		public bool IsInteger => Denominator.IsOne;

		public static Rational Parse(string digits)
		{
			BigInteger value = BigInteger.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
			return new Rational(value);
		}

		public static Rational operator +(Rational a, Rational b)
		{
			BigInteger numerator = a.Numerator * b.Denominator + b.Numerator * a.Denominator;
			return new Rational(numerator, a.Denominator * b.Denominator);
		}

		public static Rational operator -(Rational a, Rational b)
		{
			BigInteger numerator = a.Numerator * b.Denominator - b.Numerator * a.Denominator;
			return new Rational(numerator, a.Denominator * b.Denominator);
		}

		public static Rational operator *(Rational a, Rational b)
		{
			return new Rational(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
		}

		public static Rational operator /(Rational a, Rational b)
		{
			return new Rational(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
		}

		public static Rational operator -(Rational a)
		{
			return new Rational(-a.Numerator, a.Denominator);
		}

		public static bool operator ==(Rational a, Rational b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Rational a, Rational b)
		{
			return !a.Equals(b);
		}

		public static Rational IntegerDivide(Rational a, Rational b)
		{
			EnsureIntegers(a, b);
			BigInteger remainder = EuclideanRemainder(a.Numerator, b.Numerator);
			return new Rational((a.Numerator - remainder) / b.Numerator);
		}

		public static Rational Remainder(Rational a, Rational b)
		{
			EnsureIntegers(a, b);
			return new Rational(EuclideanRemainder(a.Numerator, b.Numerator));
		}

		public bool Equals(Rational other)
		{
			return Numerator == other.Numerator && Denominator == other.Denominator;
		}

		public override bool Equals(object? obj)
		{
			return obj is Rational other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Numerator, Denominator);
		}

		public override string ToString()
		{
			if (IsInteger)
			{
				return Numerator.ToString(CultureInfo.InvariantCulture);
			}

			BigInteger scale = BigInteger.Pow(DecimalBase, DisplayFractionDigits);
			BigInteger absoluteNumerator = BigInteger.Abs(Numerator);
			BigInteger doubledDenominator = Denominator * RoundingFactor;
			BigInteger scaled = (absoluteNumerator * scale * RoundingFactor + Denominator) / doubledDenominator;
			BigInteger integerPart = BigInteger.DivRem(scaled, scale, out BigInteger fractionPart);

			string sign = Numerator.Sign < 0 && !scaled.IsZero ? "-" : string.Empty;
			string integerText = integerPart.ToString(CultureInfo.InvariantCulture);
			string fractionText = fractionPart.ToString(CultureInfo.InvariantCulture)
				.PadLeft(DisplayFractionDigits, '0')
				.TrimEnd('0');

			if (fractionText.Length == 0)
			{
				return sign + integerText;
			}

			return sign + integerText + "." + fractionText;
		}

		private static BigInteger EuclideanRemainder(BigInteger dividend, BigInteger divisor)
		{
			BigInteger remainder = BigInteger.Remainder(dividend, divisor);
			if (remainder.Sign < 0)
			{
				remainder += BigInteger.Abs(divisor);
			}

			return remainder;
		}

		private static void EnsureIntegers(Rational a, Rational b)
		{
			if (!a.IsInteger || !b.IsInteger)
			{
				throw new InvalidOperationException("Операція визначена лише для цілих чисел.");
			}
		}
	}
}
