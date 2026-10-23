namespace MyExcel.Core.Parsing
{
	public static class Lexer
	{
		private static readonly Dictionary<string, TokenType> Keywords =
			new Dictionary<string, TokenType>(StringComparer.OrdinalIgnoreCase)
			{
				{ "mod", TokenType.Mod },
				{ "div", TokenType.Div },
				{ "inc", TokenType.Inc },
				{ "dec", TokenType.Dec }
			};

		private static readonly Dictionary<char, TokenType> Symbols = new Dictionary<char, TokenType>
		{
			{ '+', TokenType.Plus },
			{ '-', TokenType.Minus },
			{ '*', TokenType.Multiply },
			{ '/', TokenType.Divide },
			{ '(', TokenType.LeftParen },
			{ ')', TokenType.RightParen }
		};

		public static IReadOnlyList<Token> Tokenize(string text)
		{
			var tokens = new List<Token>();
			int position = 0;

			while (position < text.Length)
			{
				char current = text[position];

				if (char.IsWhiteSpace(current))
				{
					position++;
				}
				else if (char.IsAsciiDigit(current))
				{
					tokens.Add(ReadNumber(text, ref position));
				}
				else if (char.IsAsciiLetter(current))
				{
					tokens.Add(ReadWord(text, ref position));
				}
				else
				{
					tokens.Add(ReadSymbol(text, ref position));
				}
			}

			tokens.Add(new Token(TokenType.End, string.Empty, text.Length));
			return tokens;
		}

		private static Token ReadNumber(string text, ref int position)
		{
			int start = position;
			SkipWhile(text, ref position, char.IsAsciiDigit);
			return new Token(TokenType.Number, text.Substring(start, position - start), start);
		}

		private static Token ReadWord(string text, ref int position)
		{
			int start = position;
			SkipWhile(text, ref position, char.IsAsciiLetter);
			string letters = text.Substring(start, position - start);

			bool isCellReference = position < text.Length && char.IsAsciiDigit(text[position]);
			if (isCellReference)
			{
				SkipWhile(text, ref position, char.IsAsciiDigit);
				string reference = text.Substring(start, position - start).ToUpperInvariant();
				return new Token(TokenType.CellReference, reference, start);
			}

			bool isKeyword = Keywords.TryGetValue(letters, out TokenType keywordType);
			if (!isKeyword)
			{
				throw new SyntaxException($"невідоме слово «{letters}»", start);
			}

			return new Token(keywordType, letters, start);
		}

		private static Token ReadSymbol(string text, ref int position)
		{
			char symbol = text[position];

			bool isKnownSymbol = Symbols.TryGetValue(symbol, out TokenType symbolType);
			if (!isKnownSymbol)
			{
				string hint = char.IsLetter(symbol) ? " (назви клітинок пишуться латинськими літерами)" : string.Empty;
				throw new SyntaxException($"неочікуваний символ «{symbol}»{hint}", position);
			}

			var token = new Token(symbolType, symbol.ToString(), position);
			position++;
			return token;
		}

		private static void SkipWhile(string text, ref int position, Func<char, bool> condition)
		{
			while (position < text.Length && condition(text[position]))
			{
				position++;
			}
		}
	}
}
