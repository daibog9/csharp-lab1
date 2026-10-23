using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using MyExcel.Core.Spreadsheets;
using DriveFile = Google.Apis.Drive.v3.Data.File;
using DriveFileList = Google.Apis.Drive.v3.Data.FileList;

namespace MyExcel.Core.GoogleDrive
{
	public sealed record GoogleDriveFile(string Id, string Name);

	public class GoogleDriveException : Exception
	{
		public GoogleDriveException(string description)
			: base($"Google Drive: {description}")
		{
		}
	}

	public sealed class GoogleDriveStorage
	{
		private const string ApplicationName = "MyExcel";
		private const string FolderName = "MyExcel";
		private const string FolderMimeType = "application/vnd.google-apps.folder";
		private const string FileListFields = "files(id, name)";
		private const string CreatedFileFields = "id";
		private const string FileListOrder = "name";
		private const int MaxListedFiles = 1000;
		private const string UserId = "user";
		private const string JsonMimeType = "application/json";
		private const int AuthorizationTimeoutMinutes = 3;

		private readonly Action<string> _openBrowser;
		private DriveService? _service = null;
		private string? _folderId = null;

		public GoogleDriveStorage(Action<string> openBrowser)
		{
			_openBrowser = openBrowser;
		}

		public bool IsConnected => _service != null;

		public async Task ConnectAsync(Stream clientSecretsStream, string tokenFolder)
		{
			if (IsConnected)
			{
				return;
			}

			try
			{
				GoogleClientSecrets clientSecrets = await GoogleClientSecrets.FromStreamAsync(clientSecretsStream);
				string[] scopes = { DriveService.Scope.DriveFile };
				using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(AuthorizationTimeoutMinutes));

				UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
					clientSecrets.Secrets,
					scopes,
					UserId,
					timeout.Token,
					new FileDataStore(tokenFolder, true),
					new BrowserCodeReceiver(_openBrowser));

				_service = new DriveService(new BaseClientService.Initializer
				{
					HttpClientInitializer = credential,
					ApplicationName = ApplicationName
				});
			}
			catch (OperationCanceledException)
			{
				throw new GoogleDriveException("час на вхід у Google-акаунт вичерпано, спробуйте ще раз.");
			}
			catch (Exception ex)
			{
				throw new GoogleDriveException($"не вдалося увійти в Google-акаунт ({ex.Message}).");
			}
		}

		public async Task<IReadOnlyList<GoogleDriveFile>> ListFilesAsync()
		{
			DriveService service = GetService();

			try
			{
				string folderId = await GetFolderIdAsync(service);
				FilesResource.ListRequest request = service.Files.List();
				request.Q = $"'{folderId}' in parents and trashed = false";
				request.Fields = FileListFields;
				request.OrderBy = FileListOrder;
				request.PageSize = MaxListedFiles;

				DriveFileList result = await request.ExecuteAsync();
				IEnumerable<DriveFile> files = result.Files ?? new List<DriveFile>();
				return files.Select(file => new GoogleDriveFile(file.Id, file.Name)).ToList();
			}
			catch (Exception ex)
			{
				throw new GoogleDriveException($"не вдалося отримати список файлів ({ex.Message}).");
			}
		}

		public async Task SaveAsync(Spreadsheet spreadsheet, string fileName)
		{
			DriveService service = GetService();
			IReadOnlyList<GoogleDriveFile> files = await ListFilesAsync();
			GoogleDriveFile? existingFile = files.FirstOrDefault(file => file.Name == fileName);

			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SpreadsheetStorage.ToJson(spreadsheet)));
			IUploadProgress progress;

			try
			{
				if (existingFile == null)
				{
					string folderId = await GetFolderIdAsync(service);
					var metadata = new DriveFile
					{
						Name = fileName,
						Parents = new List<string> { folderId }
					};
					progress = await service.Files.Create(metadata, stream, JsonMimeType).UploadAsync();
				}
				else
				{
					progress = await service.Files.Update(new DriveFile(), existingFile.Id, stream, JsonMimeType)
						.UploadAsync();
				}
			}
			catch (Exception ex)
			{
				throw new GoogleDriveException($"не вдалося зберегти файл ({ex.Message}).");
			}

			if (progress.Exception != null)
			{
				throw new GoogleDriveException($"не вдалося зберегти файл ({progress.Exception.Message}).");
			}
		}

		public async Task<Spreadsheet> LoadAsync(GoogleDriveFile file)
		{
			DriveService service = GetService();
			using var stream = new MemoryStream();
			IDownloadProgress progress;

			try
			{
				progress = await service.Files.Get(file.Id).DownloadAsync(stream);
			}
			catch (Exception ex)
			{
				throw new GoogleDriveException($"не вдалося завантажити файл ({ex.Message}).");
			}

			if (progress.Exception != null)
			{
				throw new GoogleDriveException($"не вдалося завантажити файл ({progress.Exception.Message}).");
			}

			stream.Position = 0;
			return SpreadsheetStorage.LoadFromStream(stream);
		}

		private async Task<string> GetFolderIdAsync(DriveService service)
		{
			if (_folderId != null)
			{
				return _folderId;
			}

			FilesResource.ListRequest request = service.Files.List();
			request.Q = $"mimeType = '{FolderMimeType}' and name = '{FolderName}' and trashed = false";
			request.Fields = FileListFields;
			DriveFileList result = await request.ExecuteAsync();
			DriveFile? folder = result.Files?.FirstOrDefault();

			if (folder == null)
			{
				var metadata = new DriveFile
				{
					Name = FolderName,
					MimeType = FolderMimeType
				};
				FilesResource.CreateRequest create = service.Files.Create(metadata);
				create.Fields = CreatedFileFields;
				folder = await create.ExecuteAsync();
			}

			_folderId = folder.Id;
			return _folderId;
		}

		private DriveService GetService()
		{
			if (_service == null)
			{
				throw new InvalidOperationException("Спочатку потрібно увійти в Google-акаунт.");
			}

			return _service;
		}

		private sealed class BrowserCodeReceiver : LocalServerCodeReceiver
		{
			private readonly Action<string> _openBrowser;

			public BrowserCodeReceiver(Action<string> openBrowser)
			{
				_openBrowser = openBrowser;
			}

			protected override bool OpenBrowser(string url)
			{
				_openBrowser(url);
				return true;
			}
		}
	}
}
