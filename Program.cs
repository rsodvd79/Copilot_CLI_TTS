using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using NAudio.Wave;

var builder = WebApplication.CreateBuilder(args);

var piperExecutablePath = builder.Configuration["Piper:ExecutablePath"];
if (string.IsNullOrWhiteSpace(piperExecutablePath))
{
    var piperCandidates = new[]
    {
        Path.Combine(builder.Environment.ContentRootPath, "piper", "piper.exe"),
        Path.Combine(builder.Environment.ContentRootPath, "piper", "piper"),
        Path.Combine(builder.Environment.ContentRootPath, "piper.exe"),
        Path.Combine(builder.Environment.ContentRootPath, "piper")
    };

    foreach (var candidate in piperCandidates)
    {
        if (File.Exists(candidate))
        {
            piperExecutablePath = candidate;
            break;
        }
    }

    piperExecutablePath ??= "piper.exe";
}

var piperModelPath = builder.Configuration["Piper:ModelPath"];
if (string.IsNullOrWhiteSpace(piperModelPath))
{
    var modelCandidates = new[]
    {
        Path.Combine(builder.Environment.ContentRootPath, "piper", "it_IT-paola-high.onnx"),
        Path.Combine(builder.Environment.ContentRootPath, "piper", "it_IT-paola-medium.onnx"),
        Path.Combine(builder.Environment.ContentRootPath, "piper", "it_IT-riccardo-x_low.onnx")
    };

    foreach (var candidate in modelCandidates)
    {
        if (File.Exists(candidate) && File.Exists(candidate + ".json"))
        {
            piperModelPath = candidate;
            break;
        }
    }
}

var sampleRate = 22050;
if (!string.IsNullOrWhiteSpace(piperModelPath))
{
    var configPath = piperModelPath + ".json";
    if (File.Exists(configPath))
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (doc.RootElement.TryGetProperty("audio", out var audio) &&
                audio.TryGetProperty("sample_rate", out var rate))
            {
                sampleRate = rate.GetInt32();
            }
        }
        catch
        {
            // Mantieni il default se il config non è leggibile.
        }
    }
}

builder.Services.AddSingleton<SpeechQueue>();
builder.Services.AddSingleton(new PiperOptions(
    piperExecutablePath,
    piperModelPath,
    builder.Configuration["Piper:Speaker"],
    sampleRate));
builder.Services.AddHostedService<SpeechWorker>();

var app = builder.Build();

app.MapGet("/say", (string? text, SpeechQueue queue) =>
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return Results.BadRequest("Parametro 'text' mancante.");
    }

    queue.Enqueue(text);
    return Results.NoContent();
});

app.Run();

sealed class SpeechQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(string text) => _channel.Writer.TryWrite(text);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

sealed record PiperOptions(string ExecutablePath, string? ModelPath, string? Speaker, int SampleRate);

sealed class SpeechWorker : BackgroundService
{
    private readonly SpeechQueue _queue;
    private readonly PiperOptions _options;
    private readonly ILogger<SpeechWorker> _logger;

    private Process? _piper;
    private IWavePlayer? _output;
    private BufferedWaveProvider? _buffer;

    public SpeechWorker(SpeechQueue queue, PiperOptions options, ILogger<SpeechWorker> logger)
    {
        _queue = queue;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var text in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                EnsurePiperStarted();
                await _piper!.StandardInput.WriteLineAsync(text.AsMemory(), stoppingToken);
                await _piper.StandardInput.FlushAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la sintesi Piper per il testo: {Text}", text);
                StopPiper();
            }
        }
    }

    private void EnsurePiperStarted()
    {
        if (_piper is { HasExited: false })
        {
            return;
        }

        StopPiper();

        if (string.IsNullOrWhiteSpace(_options.ModelPath))
        {
            throw new InvalidOperationException("Piper:ModelPath non configurato.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // Piper si aspetta input UTF-8: senza questo, su Windows lo stdin
            // usa la codepage di console e le lettere accentate si perdono.
            StandardInputEncoding = new System.Text.UTF8Encoding(false),
            WorkingDirectory = Path.GetDirectoryName(_options.ExecutablePath)
                ?? AppContext.BaseDirectory
        };

        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(_options.ModelPath);
        startInfo.ArgumentList.Add("--output_raw");
        startInfo.ArgumentList.Add("--quiet");

        if (!string.IsNullOrWhiteSpace(_options.Speaker))
        {
            startInfo.ArgumentList.Add("--speaker");
            startInfo.ArgumentList.Add(_options.Speaker);
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Impossibile avviare Piper.");

        _piper = process;

        _buffer = new BufferedWaveProvider(new WaveFormat(_options.SampleRate, 16, 1))
        {
            BufferDuration = TimeSpan.FromMinutes(5),
            DiscardOnBufferOverflow = false
        };

        _output = new WaveOutEvent { DesiredLatency = 120 };
        _output.Init(_buffer);
        _output.Play();

        _ = Task.Run(() => PumpRawAudioAsync(process, _buffer));
        _ = Task.Run(() => DrainStdErrAsync(process));
    }

    private async Task PumpRawAudioAsync(Process process, BufferedWaveProvider buffer)
    {
        var stream = process.StandardOutput.BaseStream;
        var chunk = new byte[8192];

        try
        {
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                while (buffer.BufferedDuration > TimeSpan.FromMinutes(4))
                {
                    await Task.Delay(50);
                }

                buffer.AddSamples(chunk, 0, read);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nella lettura dell'audio da Piper.");
        }
    }

    private async Task DrainStdErrAsync(Process process)
    {
        try
        {
            await process.StandardError.ReadToEndAsync();
        }
        catch
        {
            // Ignora: serve solo a non riempire il buffer di stderr.
        }
    }

    private void StopPiper()
    {
        try
        {
            _output?.Stop();
            _output?.Dispose();
        }
        catch { }
        _output = null;

        try
        {
            if (_piper is { HasExited: false })
            {
                _piper.Kill(entireProcessTree: true);
            }
            _piper?.Dispose();
        }
        catch { }
        _piper = null;
        _buffer = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        StopPiper();
        await base.StopAsync(cancellationToken);
    }
}
