using MyExcel.Core;
using MyExcel.Core.Spreadsheets;
using Xunit;

namespace MyExcel.Tests
{
	public class SpreadsheetStorageTests
	{
		[Fact]
		public void FromJson_SavedSpreadsheet_RestoresSizeExpressionsAndValues()
		{
			var original = new Spreadsheet(3, 2);
			original.SetExpression(Address("A1"), "7 div 2");
			original.SetExpression(Address("B3"), "A1 * 10");

			string json = SpreadsheetStorage.ToJson(original);
			Spreadsheet restored = SpreadsheetStorage.FromJson(json);

			Assert.Equal(3, restored.RowCount);
			Assert.Equal(2, restored.ColumnCount);
			Assert.Equal("A1 * 10", restored.GetCell(Address("B3")).Expression);
			Assert.Equal("30", restored.GetCell(Address("B3")).DisplayValue);
		}

		[Fact]
		public void FromJson_InvalidJson_ThrowsSpreadsheetFileException()
		{
			Assert.Throws<SpreadsheetFileException>(() => SpreadsheetStorage.FromJson("not a spreadsheet"));
		}

		private static CellAddress Address(string text)
		{
			bool isParsed = CellAddress.TryParse(text, out CellAddress address);
			Assert.True(isParsed);
			return address;
		}
	}
}
