using System.Text.Encodings.Web;
using System.Text.Json;

namespace MyExcel.Core.Spreadsheets
{
	public static class SpreadsheetStorage
	{
		public const string FileExtension = ".json";

		private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		public static string ToJson(Spreadsheet spreadsheet)
		{
			var document = new SpreadsheetDocument
			{
				RowCount = spreadsheet.RowCount,
				ColumnCount = spreadsheet.ColumnCount,
				Cells = spreadsheet.GetAllCells()
					.Where(cell => !cell.IsEmpty)
					.Select(cell => new CellDocument
					{
						Address = cell.Address.ToString(),
						Expression = cell.Expression
					})
					.ToList()
			};

			return JsonSerializer.Serialize(document, JsonOptions);
		}

		public static Spreadsheet FromJson(string json)
		{
			SpreadsheetDocument? document = null;
			try
			{
				document = JsonSerializer.Deserialize<SpreadsheetDocument>(json, JsonOptions);
			}
			catch (JsonException)
			{
				throw new SpreadsheetFileException("файл не є таблицею MyExcel (неправильний формат JSON).");
			}

			if (document == null)
			{
				throw new SpreadsheetFileException("файл порожній.");
			}

			bool isSizeValid = Spreadsheet.IsValidSize(document.RowCount, document.ColumnCount);
			if (!isSizeValid)
			{
				throw new SpreadsheetFileException(
					$"недопустимий розмір таблиці {document.RowCount}×{document.ColumnCount}.");
			}

			var spreadsheet = new Spreadsheet(document.RowCount, document.ColumnCount);
			foreach (CellDocument? cell in document.Cells ?? new List<CellDocument>())
			{
				string addressText = cell?.Address ?? string.Empty;
				bool isAddressValid = CellAddress.TryParse(addressText, out CellAddress address);
				if (!isAddressValid || !spreadsheet.Contains(address))
				{
					throw new SpreadsheetFileException($"некоректна адреса клітинки «{addressText}».");
				}

				spreadsheet.SetExpressionWithoutRecalculation(address, cell?.Expression ?? string.Empty);
			}

			spreadsheet.Recalculate();
			return spreadsheet;
		}

		public static void SaveToFile(Spreadsheet spreadsheet, string path)
		{
			try
			{
				string? folder = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(folder))
				{
					Directory.CreateDirectory(folder);
				}

				File.WriteAllText(path, ToJson(spreadsheet));
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				throw new SpreadsheetFileException($"не вдалося записати файл ({ex.Message}).");
			}
		}

		public static Spreadsheet LoadFromStream(Stream stream)
		{
			string json = string.Empty;
			try
			{
				using var reader = new StreamReader(stream);
				json = reader.ReadToEnd();
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				throw new SpreadsheetFileException($"не вдалося прочитати файл ({ex.Message}).");
			}

			return FromJson(json);
		}

		private sealed class SpreadsheetDocument
		{
			public int RowCount { get; set; } = 0;

			public int ColumnCount { get; set; } = 0;

			public List<CellDocument> Cells { get; set; } = new List<CellDocument>();
		}

		private sealed class CellDocument
		{
			public string Address { get; set; } = string.Empty;

			public string Expression { get; set; } = string.Empty;
		}
	}

	public class SpreadsheetFileException : Exception
	{
		public SpreadsheetFileException(string description)
			: base($"Помилка файлу: {description}")
		{
		}
	}
}
