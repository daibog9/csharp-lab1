namespace MyExcel.Core.Parsing
{
	public class SyntaxException : Exception
	{
		public SyntaxException(string description, int position)
			: base($"Синтаксична помилка в позиції {position + 1}: {description}")
		{
			Description = description;
			Position = position;
		}

		public string Description { get; }

		public int Position { get; }
	}
}
