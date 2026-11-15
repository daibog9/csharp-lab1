using MyExcel.Core;
using MyExcel.Core.Expressions;
using MyExcel.Core.Parsing;
using Xunit;

namespace MyExcel.Tests
{
	public class ParserTests
	{
		[Theory]
		[InlineData("2 + 3 * 4", "14")]
		[InlineData("(2 + 3) * 4", "20")]
		[InlineData("7 / 2", "3.5")]
		[InlineData("7 mod 3", "1")]
		[InlineData("7 div 2", "3")]
		[InlineData("-7 div 2", "-4")]
		[InlineData("-7 mod 2", "1")]
		[InlineData("7 div -2", "-3")]
		[InlineData("7 mod -2", "1")]
		[InlineData("2 - -3", "5")]
		[InlineData("inc 5", "6")]
		[InlineData("dec(10 * 2)", "19")]
		[InlineData("12345678901234567890 + 1", "12345678901234567891")]
		public void Parse_ValidExpression_EvaluatesToExpectedValue(string expression, string expected)
		{
			ExpressionNode tree = Parser.Parse(expression);

			Rational value = tree.Evaluate(new EmptyCells());

			Assert.Equal(expected, value.ToString());
		}

		[Theory]
		[InlineData("1 +", 3)]
		[InlineData("(1 + 2", 6)]
		[InlineData("2 3", 2)]
		[InlineData("5 % 2", 2)]
		public void Parse_InvalidExpression_ThrowsSyntaxExceptionWithPosition(string expression, int position)
		{
			SyntaxException exception = Assert.Throws<SyntaxException>(() => Parser.Parse(expression));

			Assert.Equal(position, exception.Position);
		}

		private sealed class EmptyCells : ICellValueProvider
		{
			public Rational GetValue(CellAddress address)
			{
				return Rational.Zero;
			}
		}
	}
}
