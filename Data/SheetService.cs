using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace ScavengerHunt.Data;

public class SheetService
{
    private readonly SheetsService _service;
    private static readonly string[] SCOPES = { SheetsService.Scope.Spreadsheets };
    private const string ID = "1Rst_ToigfD1ParbN0VKShFSX0uIsC36prjn6-FXnQ9k";
    private readonly Random _random = Random.Shared;
    private HashSet<string> _sheetNames;
    private readonly PeriodicTimer _refreshSheetNamesTimer;

    public SheetService()
    {
        var credential = GetCredentialsFromFile() ?? GoogleCredential.GetApplicationDefault();

        _service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
        });
        _sheetNames = GetSheetNames().Result;
        _refreshSheetNamesTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        Task.Run(async () =>
        {
            await _refreshSheetNamesTimer.WaitForNextTickAsync();
            _sheetNames = await GetSheetNames();
        });
    }

    private static GoogleCredential? GetCredentialsFromFile()
    {
        try
        {
            using var stream = new FileStream("keys.json", FileMode.Open, FileAccess.Read);
            var credential = GoogleCredential.FromStream(stream).CreateScoped(SCOPES);
            return credential;
        } catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return null;
        }
    }

    private async Task<HashSet<string>> GetSheetNames()
    {
        var sheetNames = (await _service.Spreadsheets.Get(ID).ExecuteAsync())
            .Sheets
            .Select(sheet => sheet.Properties.Title)
            .ToHashSet();
        return sheetNames;
    }

    public async Task<bool> DoesUserExist(string username)
    {
        var users = (await _service.Spreadsheets
                .Values
                .Get(ID, "Users!A1:A")
                .ExecuteAsync())
            .Values
            .Select(list => list[0])
            .Select(list => ((string)list).Trim())
            .ToList();
        return users.Any(user => user.Equals(username.Trim(), StringComparison.CurrentCultureIgnoreCase));
    }

    public bool DoesClueExist(string clueId)
    {
        return _sheetNames.Contains(clueId);
    }

    public async Task RecordUserScan(string clueId, string username, DateTime dateTime)
    {
        var valueBody = new ValueRange
        {
            Values = new List<IList<object>>
            {
                new List<object>
                {
                    username, dateTime.ToString("MM/dd h:mm:ss tt")
                },
            },
            MajorDimension = "ROWS",
        };
        var updateRequest = _service.Spreadsheets
            .Values
            .Append(valueBody, ID, $"{clueId}!C:C");
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        await updateRequest.ExecuteAsync();
    }

    public async Task<string> GetRandomClue(string clueId)
    {
        var spreadsheet = await _service.Spreadsheets
            .Values
            .Get(ID, $"{clueId}!A1:A")
            .ExecuteAsync();

        if (spreadsheet is null)
        {
            throw new Exception("Sheet not found");
        }

        var values = spreadsheet.Values;
        var randomClue = values[_random.Next(values.Count)];
        return (string)randomClue[0];
    }
}
