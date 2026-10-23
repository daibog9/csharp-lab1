using MyExcel.Core.GoogleDrive;
using MyExcel.Core.Spreadsheets;

namespace MyExcel
{
	public partial class MainPage
	{
		private const string GoogleDriveTitle = "Google Drive";
		private const string ClientSecretsFileName = "client_secrets.json";
		private const string GoogleTokenFolderName = "GoogleDriveAuth";
		private const string CancelText = "Скасувати";

		private readonly GoogleDriveStorage _googleDrive = new GoogleDriveStorage(OpenBrowser);

		private async void OnOpenFromGoogleDriveClicked(object? sender, EventArgs e)
		{
			CommitFocusedEntry();

			bool canContinue = await ConfirmDiscardChangesAsync();
			if (!canContinue)
			{
				return;
			}

			bool isConnected = await ConnectToGoogleDriveAsync();
			if (!isConnected)
			{
				return;
			}

			try
			{
				IReadOnlyList<GoogleDriveFile> files = await _googleDrive.ListFilesAsync();
				if (files.Count == 0)
				{
					await ShowMessageAsync(GoogleDriveTitle, "На Google Drive ще немає збережених таблиць.");
					return;
				}

				string[] fileNames = files.Select(file => file.Name).ToArray();
				string? chosenName = await ChooseAsync("Відкрити з Google Drive", fileNames);
				GoogleDriveFile? chosenFile = files.FirstOrDefault(file => file.Name == chosenName);
				if (chosenFile == null)
				{
					return;
				}

				_spreadsheet = await _googleDrive.LoadAsync(chosenFile);
				_hasUnsavedChanges = false;
				_currentFilePath = null;
				_selectedAddress = null;
				BuildGrid();
			}
			catch (GoogleDriveException ex)
			{
				await ShowMessageAsync(GoogleDriveTitle, ex.Message);
			}
			catch (SpreadsheetFileException ex)
			{
				await ShowMessageAsync(GoogleDriveTitle, ex.Message);
			}
		}

		private async void OnSaveToGoogleDriveClicked(object? sender, EventArgs e)
		{
			CommitFocusedEntry();

			bool isConnected = await ConnectToGoogleDriveAsync();
			if (!isConnected)
			{
				return;
			}

			string? fileName = await DisplayPromptAsync("Зберегти на Google Drive", "Ім'я файлу:",
				"Зберегти", CancelText, initialValue: DefaultFileName);
			if (string.IsNullOrWhiteSpace(fileName))
			{
				return;
			}

			fileName = fileName.Trim();
			bool hasExtension = fileName.EndsWith(SpreadsheetStorage.FileExtension, StringComparison.OrdinalIgnoreCase);
			if (!hasExtension)
			{
				fileName += SpreadsheetStorage.FileExtension;
			}

			try
			{
				await _googleDrive.SaveAsync(_spreadsheet, fileName);
				_hasUnsavedChanges = false;
				await ShowMessageAsync(GoogleDriveTitle, $"Таблицю збережено на Google Drive у файл «{fileName}».");
			}
			catch (GoogleDriveException ex)
			{
				await ShowMessageAsync(GoogleDriveTitle, ex.Message);
			}
		}

		private async Task<bool> ConnectToGoogleDriveAsync()
		{
			if (_googleDrive.IsConnected)
			{
				return true;
			}

			try
			{
				using Stream clientSecrets = await FileSystem.OpenAppPackageFileAsync(ClientSecretsFileName);
				string tokenFolder = Path.Combine(FileSystem.AppDataDirectory, GoogleTokenFolderName);
				await _googleDrive.ConnectAsync(clientSecrets, tokenFolder);
				return true;
			}
			catch (FileNotFoundException)
			{
				string message = $"Не знайдено файл {ClientSecretsFileName}. Покладіть його в папку Resources/Raw "
					+ "проєкту MyExcel і зберіть програму заново.";
				await ShowMessageAsync(GoogleDriveTitle, message);
				return false;
			}
			catch (GoogleDriveException ex)
			{
				await ShowMessageAsync(GoogleDriveTitle, ex.Message);
				return false;
			}
		}

		private static void OpenBrowser(string url)
		{
			MainThread.BeginInvokeOnMainThread(() => Launcher.Default.OpenAsync(new Uri(url)));
		}

		private Task<string> ChooseAsync(string title, string[] options)
		{
#if NET10_0_OR_GREATER
			return DisplayActionSheetAsync(title, CancelText, null, options);
#else
			return DisplayActionSheet(title, CancelText, null, options);
#endif
		}
	}
}
