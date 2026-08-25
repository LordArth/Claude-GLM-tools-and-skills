using System;
using System.IO;
using System.Text.Json;

namespace OrderflowBattleEngine.Core;

public sealed record ResearchRecord(string Kind, DateTime Timestamp, object Payload, string Version, string ConfigHash);

public sealed class ResearchLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public ResearchLogger(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));
        _writer.AutoFlush = true;
    }

    public void Write(ResearchRecord record)
    {
        string line = JsonSerializer.Serialize(record, _json);
        lock (_gate) _writer.WriteLine(line);
    }

    public void Dispose()
    {
        lock (_gate) _writer.Dispose();
    }
}
