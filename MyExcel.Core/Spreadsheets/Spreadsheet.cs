using MyExcel.Core.Expressions;

namespace MyExcel.Core.Spreadsheets
{
	public sealed class Spreadsheet
	{
		public const int DefaultRowCount = 10;
		public const int DefaultColumnCount = 10;
		public const int MinRowCount = 1;
		public const int MinColumnCount = 1;
		public const int MaxRowCount = 26;
		public const int MaxColumnCount = 26;

		private readonly List<List<SpreadsheetCell>> _rows = new List<List<SpreadsheetCell>>();

		public Spreadsheet()
			: this(DefaultRowCount, DefaultColumnCount)
		{
		}

		public Spreadsheet(int rowCount, int columnCount)
		{
			bool isSizeValid = IsValidSize(rowCount, columnCount);
			if (!isSizeValid)
			{
				throw new ArgumentOutOfRangeException(nameof(rowCount), "Недопустимий розмір таблиці.");
			}

			ColumnCount = columnCount;
			for (int row = 0; row < rowCount; row++)
			{
				AppendRow();
			}
		}

		public int RowCount => _rows.Count;

		public int ColumnCount { get; private set; }

		public bool CanAddRow => RowCount < MaxRowCount;

		public bool CanRemoveRow => RowCount > MinRowCount;

		public bool CanAddColumn => ColumnCount < MaxColumnCount;

		public bool CanRemoveColumn => ColumnCount > MinColumnCount;

		public static bool IsValidSize(int rowCount, int columnCount)
		{
			bool isRowCountValid = rowCount >= MinRowCount && rowCount <= MaxRowCount;
			bool isColumnCountValid = columnCount >= MinColumnCount && columnCount <= MaxColumnCount;
			return isRowCountValid && isColumnCountValid;
		}

		public bool Contains(CellAddress address)
		{
			bool isRowInside = address.Row >= 0 && address.Row < RowCount;
			bool isColumnInside = address.Column >= 0 && address.Column < ColumnCount;
			return isRowInside && isColumnInside;
		}

		public SpreadsheetCell GetCell(CellAddress address)
		{
			bool isInside = Contains(address);
			if (!isInside)
			{
				throw new ArgumentOutOfRangeException(nameof(address), $"Клітинки {address} немає в таблиці.");
			}

			return _rows[address.Row][address.Column];
		}

		public IEnumerable<SpreadsheetCell> GetAllCells()
		{
			return _rows.SelectMany(row => row);
		}

		public void SetExpression(CellAddress address, string expression)
		{
			SetExpressionWithoutRecalculation(address, expression);
			Recalculate();
		}

		public void AddRow()
		{
			if (!CanAddRow)
			{
				throw new InvalidOperationException("Досягнуто максимальну кількість рядків.");
			}

			AppendRow();
			Recalculate();
		}

		public void RemoveLastRow()
		{
			if (!CanRemoveRow)
			{
				throw new InvalidOperationException("Таблиця повинна містити хоча б один рядок.");
			}

			_rows.RemoveAt(RowCount - 1);
			Recalculate();
		}

		public void AddColumn()
		{
			if (!CanAddColumn)
			{
				throw new InvalidOperationException("Досягнуто максимальну кількість стовпчиків.");
			}

			for (int row = 0; row < RowCount; row++)
			{
				_rows[row].Add(new SpreadsheetCell(new CellAddress(ColumnCount, row)));
			}

			ColumnCount++;
			Recalculate();
		}

		public void RemoveLastColumn()
		{
			if (!CanRemoveColumn)
			{
				throw new InvalidOperationException("Таблиця повинна містити хоча б один стовпчик.");
			}

			foreach (List<SpreadsheetCell> row in _rows)
			{
				row.RemoveAt(ColumnCount - 1);
			}

			ColumnCount--;
			Recalculate();
		}

		public void Recalculate()
		{
			var evaluator = new Evaluator(this);
			evaluator.EvaluateAll();
		}

		internal void SetExpressionWithoutRecalculation(CellAddress address, string expression)
		{
			GetCell(address).SetExpression(expression);
		}

		private void AppendRow()
		{
			int rowIndex = RowCount;
			var row = new List<SpreadsheetCell>();

			for (int column = 0; column < ColumnCount; column++)
			{
				row.Add(new SpreadsheetCell(new CellAddress(column, rowIndex)));
			}

			_rows.Add(row);
		}

		private sealed class Evaluator : ICellValueProvider
		{
			private const string PathSeparator = " → ";

			private readonly Spreadsheet _spreadsheet;
			private readonly HashSet<CellAddress> _inProgress = new HashSet<CellAddress>();
			private readonly HashSet<CellAddress> _evaluated = new HashSet<CellAddress>();
			private readonly Dictionary<CellAddress, string> _cycleDescriptions = new Dictionary<CellAddress, string>();

			public Evaluator(Spreadsheet spreadsheet)
			{
				_spreadsheet = spreadsheet;
			}

			public void EvaluateAll()
			{
				foreach (SpreadsheetCell cell in _spreadsheet.GetAllCells())
				{
					cell.ResetValue();
				}

				foreach (SpreadsheetCell cell in _spreadsheet.GetAllCells())
				{
					bool isEvaluated = _evaluated.Contains(cell.Address);
					if (!isEvaluated)
					{
						EvaluateWithDependencies(cell);
					}
				}
			}

			public Rational GetValue(CellAddress address)
			{
				bool isInside = _spreadsheet.Contains(address);
				if (!isInside)
				{
					throw new EvaluationException($"клітинка {address} знаходиться за межами таблиці");
				}

				bool isEvaluated = _evaluated.Contains(address);
				if (!isEvaluated)
				{
					throw new EvaluationException($"циклічне посилання на клітинку {address}");
				}

				SpreadsheetCell target = _spreadsheet.GetCell(address);
				if (target.HasError)
				{
					throw new EvaluationException($"клітинка {address} містить помилку");
				}

				return target.Value ?? Rational.Zero;
			}

			private void EvaluateWithDependencies(SpreadsheetCell start)
			{
				var stack = new List<StackFrame>();
				Push(stack, start);

				while (stack.Count > 0)
				{
					StackFrame top = stack[stack.Count - 1];
					bool hasNextReference = top.References.MoveNext();

					if (hasNextReference)
					{
						VisitReference(stack, top.References.Current);
					}
					else
					{
						stack.RemoveAt(stack.Count - 1);
						_inProgress.Remove(top.Cell.Address);
						Compute(top.Cell);
						_evaluated.Add(top.Cell.Address);
					}
				}
			}

			private void VisitReference(List<StackFrame> stack, CellAddress address)
			{
				bool isInside = _spreadsheet.Contains(address);
				bool isEvaluated = _evaluated.Contains(address);
				if (!isInside || isEvaluated)
				{
					return;
				}

				bool isInStack = _inProgress.Contains(address);
				if (isInStack)
				{
					MarkCycle(stack, address);
				}
				else
				{
					Push(stack, _spreadsheet.GetCell(address));
				}
			}

			private void Push(List<StackFrame> stack, SpreadsheetCell cell)
			{
				_inProgress.Add(cell.Address);
				stack.Add(new StackFrame(cell));
			}

			private void Compute(SpreadsheetCell cell)
			{
				ExpressionNode? expression = cell.ParsedExpression;
				if (expression == null)
				{
					return;
				}

				try
				{
					cell.SetValue(expression.Evaluate(this));
				}
				catch (EvaluationException ex)
				{
					bool isInCycle = _cycleDescriptions.TryGetValue(cell.Address, out string? cycleDescription);
					cell.SetEvaluationError(isInCycle ? cycleDescription! : ex.Message);
				}
			}

			private void MarkCycle(List<StackFrame> stack, CellAddress address)
			{
				int cycleStart = stack.FindIndex(frame => frame.Cell.Address == address);
				IEnumerable<CellAddress> cycle = stack
					.Skip(cycleStart)
					.Select(frame => frame.Cell.Address)
					.Append(address);
				string description = "циклічне посилання " + string.Join(PathSeparator, cycle);

				for (int i = cycleStart; i < stack.Count; i++)
				{
					_cycleDescriptions.TryAdd(stack[i].Cell.Address, description);
				}
			}

			private sealed class StackFrame
			{
				public StackFrame(SpreadsheetCell cell)
				{
					Cell = cell;
					References = cell.References.GetEnumerator();
				}

				public SpreadsheetCell Cell { get; }

				public IEnumerator<CellAddress> References { get; }
			}
		}
	}
}
