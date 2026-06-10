using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

var config = ParseArgs(args);
if (config is null)
{
    PrintUsage();
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

using var http = new HttpClient();
Console.WriteLine($"Monitor appunti attivo. Endpoint: {config.BaseUrl.TrimEnd('/')}/say");
Console.WriteLine("Premi Ctrl+C per uscire.");

var sentTexts = new HashSet<string>(StringComparer.Ordinal);

while (!cancellation.IsCancellationRequested)
{
    var readResult = TryReadClipboardText();

    if (readResult.IsBusy)
    {
        await Task.Delay(config.PollingIntervalMs, cancellation.Token);
        continue;
    }

    var clipboardText = readResult.Text;
    var sanitizedText = SanitizeForSpeech(clipboardText);
    if (!string.IsNullOrWhiteSpace(sanitizedText) && !sentTexts.Contains(sanitizedText))
    {
        var requestUrl = $"{config.BaseUrl.TrimEnd('/')}/say?text={WebUtility.UrlEncode(sanitizedText)}";
        using var response = await http.GetAsync(requestUrl, cancellation.Token);
        Console.WriteLine($"Inviato ({(int)response.StatusCode}) [{DateTime.Now:HH:mm:ss}]");

        if (response.IsSuccessStatusCode)
        {
            sentTexts.Add(sanitizedText);
        }
    }
    
    await Task.Delay(config.PollingIntervalMs, cancellation.Token);
}

return 0;

static ReadClipboardResult TryReadClipboardText()
{
    string? text = null;
    var isBusy = false;
    Exception? threadException = null;

    var thread = new Thread(() =>
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                text = Clipboard.GetText(TextDataFormat.UnicodeText);
            }

        }
        catch (ExternalException)
        {
            isBusy = true;
        }
        catch (Exception ex)
        {
            threadException = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (threadException is not null)
    {
        throw new InvalidOperationException("Errore lettura appunti.", threadException);
    }

    return new ReadClipboardResult(text, isBusy);
}

static string SanitizeForSpeech(string? input)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        return string.Empty;
    }

    var text = WebUtility.HtmlDecode(input).Normalize(NormalizationForm.FormC);
    text = Regex.Replace(text, @"\[(.*?)\]\((.*?)\)", "$1");
    text = Regex.Replace(text, @"```[\s\S]*?```", " ");
    text = Regex.Replace(text, @"`([^`]+)`", "$1");
    text = Regex.Replace(text, @"(^|\s)[#>*_~`-]+", " ");
    text = Regex.Replace(text, @"[\u0300-\u036F]+", string.Empty);
    text = Regex.Replace(text, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]+", " ");
    text = Regex.Replace(text, @"\r?\n", " ");
    text = Regex.Replace(text, @"\s{2,}", " ");

    return text.Trim();
}

static MonitorConfig? ParseArgs(string[] args)
{
    var baseUrl = "http://localhost:5000";
    var pollingIntervalMs = 500;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];

        if (arg.Equals("--url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            baseUrl = args[++i];
            continue;
        }

        if (arg.StartsWith("--url=", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = arg["--url=".Length..];
            continue;
        }

        if (arg.Equals("--interval-ms", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            if (!int.TryParse(args[++i], out pollingIntervalMs) || pollingIntervalMs < 100)
            {
                return null;
            }

            continue;
        }

        if (arg.StartsWith("--interval-ms=", StringComparison.OrdinalIgnoreCase))
        {
            var value = arg["--interval-ms=".Length..];
            if (!int.TryParse(value, out pollingIntervalMs) || pollingIntervalMs < 100)
            {
                return null;
            }
        }
    }

    return new MonitorConfig(baseUrl, pollingIntervalMs);
}

static void PrintUsage()
{
    Console.Error.WriteLine("Uso: ClipboardMonitor [--url http://localhost:5000] [--interval-ms 500]");
}

sealed record MonitorConfig(string BaseUrl, int PollingIntervalMs);
sealed record ReadClipboardResult(string? Text, bool IsBusy);
