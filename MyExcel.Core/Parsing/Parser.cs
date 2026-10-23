using MyExcel.Core.Expressions;

namespace MyExcel.Core.Parsing
{
	public sealed class Parser
	{
		private const int MaxNestingDepth = 100;
		private const int MaxTreeDepth = 1000;

		private readonly IReadOnlyList<Token> _tokens;
		private int _index = 0;
		private int _nestingDepth = 0;

		private Parser(IReadOnlyList<Token> tokens)
		{
			_tokens = tokens;
		}

		private Token Current => _tokens[_index];

		public static ExpressionNode Parse(string text)
		{
			IReadOnlyList<Token> tokens = Lexer.Tokenize(text);
			var parser = new Parser(tokens);
			return parser.ParseWholeExpression();
		}

		private ExpressionNode ParseWholeExpression()
		{
			if (Current.Type == TokenType.End)
			{
				throw new SyntaxException("порожній вираз", Current.Position);
			}

			ExpressionNode expression = ParseExpression();

			if (Current.Type == TokenType.RightParen)
			{
				throw new SyntaxException("зайва закривна дужка «)»", Current.Position);
			}

			if (Current.Type != TokenType.End)
			{
				throw new SyntaxException($"очікувалася операція перед «{Current.Text}»", Current.Position);
			}

			return expression;
		}

		private ExpressionNode ParseExpression()
		{
			ExpressionNode left = ParseTerm();

			while (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus)
			{
				Token operatorToken = Current;
				BinaryOperator op = operatorToken.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
				MoveNext();
				ExpressionNode right = ParseTerm();
				left = CheckDepth(new BinaryOperationNode(op, left, right), operatorToken);
			}

			return left;
		}

		private ExpressionNode ParseTerm()
		{
			ExpressionNode left = ParseFactor();

			while (TryGetMultiplicativeOperator(Current.Type, out BinaryOperator op))
			{
				Token operatorToken = Current;
				MoveNext();
				ExpressionNode right = ParseFactor();
				left = CheckDepth(new BinaryOperationNode(op, left, right), operatorToken);
			}

			return left;
		}

		private ExpressionNode ParseFactor()
		{
			bool isUnary = TryGetUnaryOperator(Current.Type, out UnaryOperator op);
			if (isUnary)
			{
				Token operatorToken = Current;
				MoveNext();
				EnterNesting(operatorToken);
				ExpressionNode operand = ParseFactor();
				_nestingDepth--;
				return CheckDepth(new UnaryOperationNode(op, operand), operatorToken);
			}

			return ParsePrimary();
		}

		private ExpressionNode ParsePrimary()
		{
			Token token = Current;

			switch (token.Type)
			{
				case TokenType.Number:
					MoveNext();
					return new NumberNode(Rational.Parse(token.Text));

				case TokenType.CellReference:
					MoveNext();
					return new CellReferenceNode(ParseCellAddress(token));

				case TokenType.LeftParen:
					MoveNext();
					EnterNesting(token);
					ExpressionNode inner = ParseExpression();
					_nestingDepth--;
					Expect(TokenType.RightParen, "очікувалася закривна дужка «)»");
					return inner;

				case TokenType.End:
					throw new SyntaxException(
						"неочікуваний кінець виразу: очікувалося число, клітинка або «(»", token.Position);

				default:
					throw new SyntaxException(
						$"очікувалося число, клітинка або «(», а знайдено «{token.Text}»", token.Position);
			}
		}

		private static CellAddress ParseCellAddress(Token token)
		{
			bool isValid = CellAddress.TryParse(token.Text, out CellAddress address);
			if (!isValid)
			{
				throw new SyntaxException($"некоректне посилання на клітинку «{token.Text}»", token.Position);
			}

			return address;
		}

		private void EnterNesting(Token token)
		{
			_nestingDepth++;
			if (_nestingDepth > MaxNestingDepth)
			{
				string description = $"занадто велика вкладеність (більше {MaxNestingDepth} рівнів)";
				throw new SyntaxException(description, token.Position);
			}
		}

		private static ExpressionNode CheckDepth(ExpressionNode node, Token operatorToken)
		{
			if (node.Depth > MaxTreeDepth)
			{
				throw new SyntaxException("вираз занадто складний (забагато операцій)", operatorToken.Position);
			}

			return node;
		}

		private void Expect(TokenType type, string errorDescription)
		{
			if (Current.Type != type)
			{
				throw new SyntaxException(errorDescription, Current.Position);
			}

			MoveNext();
		}

		private void MoveNext()
		{
			if (Current.Type != TokenType.End)
			{
				_index++;
			}
		}

		private static bool TryGetMultiplicativeOperator(TokenType type, out BinaryOperator op)
		{
			switch (type)
			{
				case TokenType.Multiply:
					op = BinaryOperator.Multiply;
					return true;
				case TokenType.Divide:
					op = BinaryOperator.Divide;
					return true;
				case TokenType.Mod:
					op = BinaryOperator.Modulo;
					return true;
				case TokenType.Div:
					op = BinaryOperator.IntegerDivide;
					return true;
				default:
					op = default;
					return false;
			}
		}

		private static bool TryGetUnaryOperator(TokenType type, out UnaryOperator op)
		{
			switch (type)
			{
				case TokenType.Plus:
					op = UnaryOperator.Plus;
					return true;
				case TokenType.Minus:
					op = UnaryOperator.Minus;
					return true;
				case TokenType.Inc:
					op = UnaryOperator.Increment;
					return true;
				case TokenType.Dec:
					op = UnaryOperator.Decrement;
					return true;
				default:
					op = default;
					return false;
			}
		}
	}
}
