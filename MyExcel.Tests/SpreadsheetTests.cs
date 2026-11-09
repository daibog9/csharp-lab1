using MyExcel.Core;
using MyExcel.Core.Spreadsheets;
using Xunit;

namespace MyExcel.Tests
{
	public class SpreadsheetTests
	{
		[Fact]
		public void SetExpression_ReferencedCellChanged_RecalculatesDependentCell()
		{
			var spreadsheet = new Spreadsheet();
			spreadsheet.SetExpression(Address("A1"), "5");
			spreadsheet.SetExpression(Address("B1"), "A1 * 2");
			Assert.Equal("10", spreadsheet.GetCell(Address("B1")).DisplayValue);

			spreadsheet.SetExpression(Address("A1"), "7");

			Assert.Equal("14", spreadsheet.GetCell(Address("B1")).DisplayValue);
		}

		[Fact]
		public void SetExpression_CyclicReference_MarksCellsAsErrors()
		{
			var spreadsheet = new Spreadsheet();

			spreadsheet.SetExpression(Address("A1"), "B1 + 1");
			spreadsheet.SetExpression(Address("B1"), "A1 + 1");

			Assert.True(spreadsheet.GetCell(Address("A1")).HasError);
			Assert.True(spreadsheet.GetCell(Address("B1")).HasError);
			Assert.Equal(SpreadsheetCell.ErrorMarker, spreadsheet.GetCell(Address("B1")).DisplayValue);
		}

		[Fact]
		public void SetExpression_DivisionByZero_MarksCellAsError()
		{
			var spreadsheet = new Spreadsheet();

			spreadsheet.SetExpression(Address("A1"), "1 / 0");

			SpreadsheetCell cell = spreadsheet.GetCell(Address("A1"));
			Assert.True(cell.HasError);
			Assert.Null(cell.Value);
		}

		private static CellAddress Address(string text)
		{
			bool isParsed = CellAddress.TryParse(text, out CellAddress address);
			Assert.True(isParsed);
			return address;
		}
	}
}
