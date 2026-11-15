using MyExcel.Core;
using MyExcel.Core.Spreadsheets;

namespace MyExcel
{
	public partial class MainPage : ContentPage
	{
		private const int HeaderIndex = 0;
		private const int HeaderOffset = 1;
		private const double RowHeaderWidth = 50;
		private const double CellWidth = 130;
		private const string SaveFolderName = "MyExcel";
		private const string DefaultFileName = "table";
		private const string ExpressionModeButtonText = "Режим: ВИРАЗ";
		private const string ValueModeButtonText = "Режим: ЗНАЧЕННЯ";
		private const string NoCellSelectedText = "Оберіть клітинку.";
		private const string FileTitle = "Файл";
		private const string TableTitle = "Таблиця";
		private const string OkText = "Гаразд";
		private const string YesText = "Так";
		private const string NoText = "Ні";

		private const string HelpTitle = "Довідка";
		private const string HelpText = """
			Режими відображення:
			• ВИРАЗ - у клітинках видно вирази, їх можна редагувати. Після введення натисніть Enter
			  або перейдіть до іншої клітинки.
			• ЗНАЧЕННЯ - у клітинках видно обчислені значення (лише для читання).
			Над таблицею показано вираз і значення вибраної клітинки або опис помилки з її позицією.

			Що можна писати у виразах:
			• цілі числа довільної довжини: 7, 12345678901234567890;
			• посилання на клітинки латинськими літерами: A1, B12, Z3;
			• круглі дужки ( );
			• бінарні операції: + - * / , mod (остача), div (ціла частка);
			• унарні операції: +x, -x, inc x (x + 1), dec x (x - 1).
			Приклад: (A1 + 5) * inc B2 div 3

			Пріоритет: спочатку унарні операції, потім * / mod div, потім + -.
			Операції одного пріоритету виконуються зліва направо.
			Результат / може бути дробовим (7 / 2 = 3.5); mod і div - лише для цілих чисел.
			Порожня клітинка в обчисленнях дорівнює 0.

			Файли: «Нова» створює порожню таблицю, «Зберегти» записує таблицю у файл .json,
			«Відкрити» читає її з файлу.
			«Зберегти на Google Drive» і «Відкрити з Google Drive» працюють із таблицями
			у папці MyExcel на вашому Google Drive (під час першого використання
			потрібно увійти в Google-акаунт у браузері).
			""";

		private const string AboutTitle = "Про програму";
		private const string AboutText = """
			MyExcel - програма для роботи з електронними таблицями
			(аналіз та обчислення виразів).
			Лабораторна робота №1* з ООП, варіант 2.
			Виконав: Нікіта Коба, К-28.
			""";

		private readonly Dictionary<Entry, CellAddress> _cellEntries = new Dictionary<Entry, CellAddress>();

		private Spreadsheet _spreadsheet = new Spreadsheet();
		private bool _isValueMode = false;
		private bool _hasUnsavedChanges = false;
		private string? _currentFilePath = null;
		private Entry? _focusedEntry = null;
		private CellAddress? _selectedAddress = null;

		public MainPage()
		{
			InitializeComponent();
			BuildGrid();
		}

		private void BuildGrid()
		{
			_focusedEntry = null;
			_cellEntries.Clear();
			SheetGrid.Children.Clear();
			SheetGrid.RowDefinitions.Clear();
			SheetGrid.ColumnDefinitions.Clear();

			SheetGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			SheetGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(RowHeaderWidth)));

			for (int column = 0; column < _spreadsheet.ColumnCount; column++)
			{
				SheetGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));
				SheetGrid.Add(CreateHeaderLabel(CellAddress.GetColumnName(column)), column + HeaderOffset, HeaderIndex);
			}

			for (int row = 0; row < _spreadsheet.RowCount; row++)
			{
				SheetGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
				SheetGrid.Add(CreateHeaderLabel((row + 1).ToString()), HeaderIndex, row + HeaderOffset);

				for (int column = 0; column < _spreadsheet.ColumnCount; column++)
				{
					Entry entry = CreateCellEntry();
					_cellEntries.Add(entry, new CellAddress(column, row));
					SheetGrid.Add(entry, column + HeaderOffset, row + HeaderOffset);
				}
			}

			bool isSelectionLost = _selectedAddress.HasValue && !_spreadsheet.Contains(_selectedAddress.Value);
			if (isSelectionLost)
			{
				_selectedAddress = null;
			}

			RefreshCells();
		}

		private Entry CreateCellEntry()
		{
			var entry = new Entry
			{
				IsReadOnly = _isValueMode
			};

			entry.Focused += OnCellFocused;
			entry.Unfocused += OnCellUnfocused;
			entry.Completed += OnCellCompleted;
			return entry;
		}

		private static Label CreateHeaderLabel(string text)
		{
			return new Label
			{
				Text = text,
				FontAttributes = FontAttributes.Bold,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center
			};
		}

		private void RefreshCells()
		{
			foreach (KeyValuePair<Entry, CellAddress> pair in _cellEntries)
			{
				Entry entry = pair.Key;
				SpreadsheetCell cell = _spreadsheet.GetCell(pair.Value);
				string text = _isValueMode ? cell.DisplayValue : cell.Expression;

				if (entry.Text != text)
				{
					entry.Text = text;
				}

				if (cell.HasError)
				{
					entry.TextColor = Colors.Red;
				}
				else
				{
					entry.ClearValue(Entry.TextColorProperty);
				}
			}

			ShowSelectedCellInfo();
		}

		private void ShowSelectedCellInfo()
		{
			if (!_selectedAddress.HasValue)
			{
				CellInfoLabel.Text = NoCellSelectedText;
				CellInfoLabel.ClearValue(Label.TextColorProperty);
				return;
			}

			SpreadsheetCell cell = _spreadsheet.GetCell(_selectedAddress.Value);
			string result = cell.HasError ? cell.ErrorMessage! : $"Значення: {cell.DisplayValue}";
			CellInfoLabel.Text = $"Клітинка {cell.Address}   |   Вираз: {cell.Expression}   |   {result}";

			if (cell.HasError)
			{
				CellInfoLabel.TextColor = Colors.Red;
			}
			else
			{
				CellInfoLabel.ClearValue(Label.TextColorProperty);
			}
		}

		private void OnCellFocused(object? sender, FocusEventArgs e)
		{
			if (sender is Entry entry && _cellEntries.TryGetValue(entry, out CellAddress address))
			{
				_focusedEntry = entry;
				_selectedAddress = address;
				ShowSelectedCellInfo();
			}
		}

		private void OnCellUnfocused(object? sender, FocusEventArgs e)
		{
			if (sender is Entry entry)
			{
				CommitEntry(entry);
				if (_focusedEntry == entry)
				{
					_focusedEntry = null;
				}
			}
		}

		private void OnCellCompleted(object? sender, EventArgs e)
		{
			if (sender is Entry entry)
			{
				CommitEntry(entry);
			}
		}

		private void CommitEntry(Entry entry)
		{
			bool isKnownEntry = _cellEntries.TryGetValue(entry, out CellAddress address);
			if (_isValueMode || !isKnownEntry)
			{
				return;
			}

			string expression = entry.Text ?? string.Empty;
			bool isChanged = _spreadsheet.GetCell(address).Expression != expression;
			if (isChanged)
			{
				_spreadsheet.SetExpression(address, expression);
				_hasUnsavedChanges = true;
				RefreshCells();
			}
		}

		private void CommitFocusedEntry()
		{
			if (_focusedEntry != null)
			{
				CommitEntry(_focusedEntry);
			}
		}

		private void OnToggleModeClicked(object? sender, EventArgs e)
		{
			SetMode(!_isValueMode);
		}

		private void OnExpressionModeClicked(object? sender, EventArgs e)
		{
			SetMode(false);
		}

		private void OnValueModeClicked(object? sender, EventArgs e)
		{
			SetMode(true);
		}

		private void SetMode(bool isValueMode)
		{
			CommitFocusedEntry();
			_isValueMode = isValueMode;
			ModeButton.Text = isValueMode ? ValueModeButtonText : ExpressionModeButtonText;

			foreach (Entry entry in _cellEntries.Keys)
			{
				entry.IsReadOnly = isValueMode;
			}

			RefreshCells();
		}

		private async void OnAddRowClicked(object? sender, EventArgs e)
		{
			string refusal = $"Максимальна кількість рядків - {Spreadsheet.MaxRowCount}.";
			await ChangeTableSizeAsync(_spreadsheet.CanAddRow, _spreadsheet.AddRow, refusal);
		}

		private async void OnRemoveRowClicked(object? sender, EventArgs e)
		{
			string refusal = "Таблиця повинна містити хоча б один рядок.";
			await ChangeTableSizeAsync(_spreadsheet.CanRemoveRow, _spreadsheet.RemoveLastRow, refusal);
		}

		private async void OnAddColumnClicked(object? sender, EventArgs e)
		{
			string refusal = $"Максимальна кількість стовпчиків - {Spreadsheet.MaxColumnCount}.";
			await ChangeTableSizeAsync(_spreadsheet.CanAddColumn, _spreadsheet.AddColumn, refusal);
		}

		private async void OnRemoveColumnClicked(object? sender, EventArgs e)
		{
			string refusal = "Таблиця повинна містити хоча б один стовпчик.";
			await ChangeTableSizeAsync(_spreadsheet.CanRemoveColumn, _spreadsheet.RemoveLastColumn, refusal);
		}

		private async Task ChangeTableSizeAsync(bool isAllowed, Action change, string refusalMessage)
		{
			CommitFocusedEntry();
			if (!isAllowed)
			{
				await ShowMessageAsync(TableTitle, refusalMessage);
				return;
			}

			change();
			_hasUnsavedChanges = true;
			BuildGrid();
		}

		private async void OnNewClicked(object? sender, EventArgs e)
		{
			CommitFocusedEntry();

			if (_hasUnsavedChanges)
			{
				bool isNewConfirmed = await AskAsync("Нова таблиця",
					"Таблиця містить незбережені зміни, їх буде втрачено. Чи дійсно ви хочете створити нову таблицю?");
				if (!isNewConfirmed)
				{
					return;
				}
			}

			_spreadsheet = new Spreadsheet();
			_hasUnsavedChanges = false;
			_currentFilePath = null;
			_selectedAddress = null;
			BuildGrid();
		}

		private async void OnOpenClicked(object? sender, EventArgs e)
		{
			CommitFocusedEntry();

			bool canContinue = await ConfirmDiscardChangesAsync();
			if (!canContinue)
			{
				return;
			}

			var options = new PickOptions
			{
				PickerTitle = "Відкрити таблицю",
				FileTypes = CreateJsonFileType()
			};

			try
			{
				FileResult? file = await FilePicker.Default.PickAsync(options);
				if (file == null)
				{
					return;
				}

				using Stream stream = await file.OpenReadAsync();
				_spreadsheet = SpreadsheetStorage.LoadFromStream(stream);
				_hasUnsavedChanges = false;
				_currentFilePath = file.FullPath;
				_selectedAddress = null;
				BuildGrid();
			}
			catch (SpreadsheetFileException ex)
			{
				await ShowMessageAsync(FileTitle, ex.Message);
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(FileTitle, $"Помилка файлу: не вдалося відкрити файл ({ex.Message}).");
			}
		}

		private async void OnSaveClicked(object? sender, EventArgs e)
		{
			CommitFocusedEntry();

			if (_currentFilePath == null)
			{
				await SaveAsAsync();
			}
			else
			{
				await SaveToFileAsync(_currentFilePath);
			}
		}

		private async void OnSaveAsClicked(object? sender, EventArgs e)
		{
			CommitFocusedEntry();
			await SaveAsAsync();
		}

		private async Task SaveAsAsync()
		{
			string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			string folder = Path.Combine(documents, SaveFolderName);
			string? fileName = await DisplayPromptAsync("Зберегти як", $"Ім'я файлу (папка «{folder}»):",
				"Зберегти", "Скасувати", initialValue: DefaultFileName);

			if (string.IsNullOrWhiteSpace(fileName))
			{
				return;
			}

			fileName = fileName.Trim();
			bool hasInvalidCharacters = fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
			if (hasInvalidCharacters)
			{
				await ShowMessageAsync(FileTitle, "Ім'я файлу містить недопустимі символи.");
				return;
			}

			bool hasExtension = fileName.EndsWith(SpreadsheetStorage.FileExtension, StringComparison.OrdinalIgnoreCase);
			if (!hasExtension)
			{
				fileName += SpreadsheetStorage.FileExtension;
			}

			string path = Path.Combine(folder, fileName);
			bool fileExists = File.Exists(path);
			if (fileExists)
			{
				bool isReplaceConfirmed = await AskAsync(FileTitle, $"Файл «{fileName}» уже існує. Замінити його?");
				if (!isReplaceConfirmed)
				{
					return;
				}
			}

			await SaveToFileAsync(path);
		}

		private async Task SaveToFileAsync(string path)
		{
			try
			{
				SpreadsheetStorage.SaveToFile(_spreadsheet, path);
				_hasUnsavedChanges = false;
				_currentFilePath = path;
				await ShowMessageAsync(FileTitle, $"Таблицю збережено у файл:\n{path}");
			}
			catch (SpreadsheetFileException ex)
			{
				await ShowMessageAsync(FileTitle, ex.Message);
			}
		}

		private static FilePickerFileType CreateJsonFileType()
		{
			return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
			{
				{ DevicePlatform.WinUI, new[] { SpreadsheetStorage.FileExtension } },
				{ DevicePlatform.MacCatalyst, new[] { "public.json" } },
				{ DevicePlatform.iOS, new[] { "public.json" } },
				{ DevicePlatform.Android, new[] { "application/json" } }
			});
		}

		private async void OnHelpClicked(object? sender, EventArgs e)
		{
			await ShowMessageAsync(HelpTitle, HelpText);
		}

		private async void OnAboutClicked(object? sender, EventArgs e)
		{
			await ShowMessageAsync(AboutTitle, AboutText);
		}

		private async void OnExitClicked(object? sender, EventArgs e)
		{
			CommitFocusedEntry();

			string question = _hasUnsavedChanges
				? "Таблиця містить незбережені зміни, їх буде втрачено. Чи дійсно ви хочете вийти з програми?"
				: "Чи дійсно ви хочете завершити роботу з програмою?";
			bool isExitConfirmed = await AskAsync("Вихід", question);
			if (isExitConfirmed)
			{
				Application.Current?.Quit();
			}
		}

		private async Task<bool> ConfirmDiscardChangesAsync()
		{
			if (!_hasUnsavedChanges)
			{
				return true;
			}

			return await AskAsync("Незбережені зміни",
				"Таблиця містить незбережені зміни. Якщо продовжити, їх буде втрачено. Продовжити?");
		}

		private Task ShowMessageAsync(string title, string message)
		{
#if NET10_0_OR_GREATER
			return DisplayAlertAsync(title, message, OkText);
#else
			return DisplayAlert(title, message, OkText);
#endif
		}

		private Task<bool> AskAsync(string title, string question)
		{
#if NET10_0_OR_GREATER
			return DisplayAlertAsync(title, question, YesText, NoText);
#else
			return DisplayAlert(title, question, YesText, NoText);
#endif
		}
	}
}
