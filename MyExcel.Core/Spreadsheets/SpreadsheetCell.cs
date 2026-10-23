using MyExcel.Core.Expressions;
using MyExcel.Core.Parsing;

namespace MyExcel.Core.Spreadsheets
{
	public sealed class SpreadsheetCell
	{
		public const string ErrorMarker = "#ПОМИЛКА";

		private string? _syntaxError = null;

		internal SpreadsheetCell(CellAddress address)
		{
			Address = address;
		}

		public CellAddress Address { get; }

		public string Expression { get; private set; } = string.Empty;

		public bool IsEmpty => string.IsNullOrWhiteSpace(Expression);

		public Rational? Value { get; private set; } = null;

		public string? ErrorMessage { get; private set; } = null;

		public bool HasError => ErrorMessage != null;

		public string DisplayValue
		{
			get
			{
				if (HasError)
				{
					return ErrorMarker;
				}

				return Value.HasValue ? Value.Value.ToString() : string.Empty;
			}
		}

		internal ExpressionNode? ParsedExpression { get; private set; } = null;

		internal IReadOnlyList<CellAddress> References { get; private set; } = Array.Empty<CellAddress>();

		internal void SetExpression(string expression)
		{
			Expression = expression;
			ParsedExpression = null;
			References = Array.Empty<CellAddress>();
			_syntaxError = null;

			if (IsEmpty)
			{
				return;
			}

			try
			{
				ParsedExpression = Parser.Parse(expression);
				var references = new List<CellAddress>();
				ParsedExpression.CollectReferences(references);
				References = references;
			}
			catch (SyntaxException ex)
			{
				_syntaxError = ex.Message;
			}
		}

		internal void ResetValue()
		{
			Value = null;
			ErrorMessage = _syntaxError;
		}

		internal void SetValue(Rational value)
		{
			Value = value;
			ErrorMessage = null;
		}

		internal void SetEvaluationError(string description)
		{
			Value = null;
			ErrorMessage = $"Помилка обчислення: {description}";
		}
	}
}
