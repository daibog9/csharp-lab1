using System.Globalization;

namespace MyExcel.Core
{
	public readonly record struct CellAddress(int Column, int Row)
	{
		private const int AlphabetSize = 26;
		private const char FirstLetter = 'A';

		public static string GetColumnName(int columnIndex)
		{
			if (columnIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(columnIndex));
			}

			string name = string.Empty;
			int number = columnIndex + 1;

			while (number > 0)
			{
				int remainder = (number - 1) % AlphabetSize;
				name = (char)(FirstLetter + remainder) + name;
				number = (number - 1) / AlphabetSize;
			}

			return name;
		}

		public static bool TryParse(string text, out CellAddress address)
		{
			address = default;

			int lettersLength = 0;
			while (lettersLength < text.Length && IsLatinLetter(text[lettersLength]))
			{
				lettersLength++;
			}

			string letters = text.Substring(0, lettersLength);
			string digits = text.Substring(lettersLength);

			bool hasLetters = letters.Length > 0;
			bool hasDigits = digits.Length > 0 && digits.All(char.IsAsciiDigit);
			if (!hasLetters || !hasDigits)
			{
				return false;
			}

			bool isColumnValid = TryParseColumnName(letters, out int column);
			bool isRowValid = int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int rowNumber);
			if (!isColumnValid || !isRowValid || rowNumber < 1)
			{
				return false;
			}

			address = new CellAddress(column, rowNumber - 1);
			return true;
		}

		public override string ToString()
		{
			return GetColumnName(Column) + (Row + 1).ToString(CultureInfo.InvariantCulture);
		}

		private static bool TryParseColumnName(string letters, out int columnIndex)
		{
			columnIndex = 0;
			long number = 0;

			foreach (char letter in letters)
			{
				number = number * AlphabetSize + (char.ToUpperInvariant(letter) - FirstLetter + 1);
				if (number > int.MaxValue)
				{
					return false;
				}
			}

			columnIndex = (int)number - 1;
			return true;
		}

		private static bool IsLatinLetter(char c)
		{
			return char.IsAsciiLetter(c);
		}
	}
}
