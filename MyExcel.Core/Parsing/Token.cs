namespace MyExcel.Core.Parsing
{
	public enum TokenType
	{
		Number,
		CellReference,
		Plus,
		Minus,
		Multiply,
		Divide,
		Mod,
		Div,
		Inc,
		Dec,
		LeftParen,
		RightParen,
		End
	}

	public sealed class Token
	{
		public Token(TokenType type, string text, int position)
		{
			Type = type;
			Text = text;
			Position = position;
		}

		public TokenType Type { get; }

		public string Text { get; }

		public int Position { get; }
	}
}
