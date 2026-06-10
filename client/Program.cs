using System.Net;

var config = ParseArgs(args);
if (config is null)
{
    PrintUsage();
    return 1;
}

using var http = new HttpClient();
var requestUrl = $"{config.BaseUrl.TrimEnd('/')}/say?text={WebUtility.UrlEncode(config.Text)}";
var response = await http.GetAsync(requestUrl);

Console.WriteLine((int)response.StatusCode);
return response.IsSuccessStatusCode ? 0 : 1;

static ClientConfig? ParseArgs(string[] args)
{
    var baseUrl = "http://localhost:5000";
    var textParts = new List<string>();

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

        textParts.Add(arg);
    }

    if (textParts.Count == 0)
    {
        return null;
    }

    return new ClientConfig(baseUrl, string.Join(' ', textParts));
}

static void PrintUsage()
{
    Console.Error.WriteLine("Uso: TtsClient [--url http://localhost:5000] <testo da pronunciare>");
}

sealed record ClientConfig(string BaseUrl, string Text);
